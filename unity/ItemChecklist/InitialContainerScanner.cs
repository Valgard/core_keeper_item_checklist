using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace ItemChecklist
{
    /// <summary>
    /// Two-pass scanner for the initial state-population on first mod-load
    /// in a world. Pass 1 walks the local player's own inventory (hotbar +
    /// bag + equipment slots — all share one <c>ContainedObjectsBuffer</c>
    /// on the player's inventory entity). Pass 2 walks every entity in the
    /// world that has a <c>ContainedObjectsBuffer</c> and is not a crafting
    /// station, then reads each slot. Every non-empty slot's objectID flows
    /// through <see cref="ChecklistState.SetOwned"/> with source
    /// <see cref="OwnSource.InitialScan"/>.
    ///
    /// Heuristic — see <c>docs/research/spike-2-container-type-heuristic.md</c>:
    ///
    /// * <b>Confirmed component:</b> <c>CraftingCD</c> — the ECS-side
    ///   IComponentData marker for crafting stations. Lives in
    ///   <c>Pug.ECS.Components.dll</c> and <c>Pug.Other.dll</c>.
    /// * <b>NOT confirmed:</b> the spike's hypothesised
    ///   <c>{Ancient,Boss,Eerie,Mold,Locked,NonPaintable,SeaBiome}Chest</c>
    ///   exclusions. A binary probe (`strings` on every Pug DLL) shows those
    ///   names live only in <c>Pug.Objects.dll</c> as
    ///   <c>EntityMonoBehaviour</c> authoring scripts attached to the
    ///   prefabs — they are NOT IComponentData and cannot be used in an
    ///   <c>EntityQueryDesc.None</c> filter. No clean <c>PlayerPlaced</c>
    ///   marker was found in any DLL.
    /// * <b>Working compromise:</b> exclude only <c>CraftingCD</c>. This
    ///   over-scans (world-spawn chests like puzzle pedestals and dungeon
    ///   loot will be counted as "owned" on the first scan in a world that
    ///   already has them placed). For Phase 1 this trade-off is acceptable
    ///   per the project plan: better to ship something that runs and
    ///   over-scans than to block on the heuristic. The exact ECS-side
    ///   discriminator for player-placed containers is a Live-Verification
    ///   TODO (see spike doc) and will narrow this query in a follow-up.
    /// </summary>
    public sealed class InitialContainerScanner
    {
        private readonly ChecklistState state;

        public InitialContainerScanner(ChecklistState state)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>
        /// Run both passes. Logs how many items each pass contributed.
        /// Safe to call from a non-IMod-loop context (e.g. world-loaded hook
        /// or UI re-scan button). Each pass returns 0 gracefully if its
        /// prerequisites are not yet available (world not loaded, player not
        /// spawned, ECS world not initialised).
        /// </summary>
        public void Run()
        {
            int pass1 = ScanLocalPlayerInventory();
            int pass2 = ScanPlayerPlacedContainers();
            Debug.Log($"[ItemChecklist] InitialScan: pass1 {pass1} items, pass2 {pass2} items");
        }

        /// <summary>
        /// Pass 1: walk the local player's own inventory buffer. Hotbar,
        /// bag and equipment slots all live in a single
        /// <c>ContainedObjectsBuffer</c> on the player's inventory entity
        /// (resolved via <c>Manager.main.player.playerInventoryHandler.inventoryEntity</c>).
        /// </summary>
        private int ScanLocalPlayerInventory()
        {
            try
            {
                if (Manager.main == null || Manager.main.player == null)
                    return 0;

                var handler = Manager.main.player.playerInventoryHandler;
                if (handler == null) return 0;

                var entity = handler.inventoryEntity;
                if (entity == Entity.Null) return 0;

                var em = GetEntityManager();
                if (em == null) return 0;

                if (!em.Value.HasBuffer<ContainedObjectsBuffer>(entity))
                    return 0;

                var buf = em.Value.GetBuffer<ContainedObjectsBuffer>(entity);
                return ConsumeBuffer(buf);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ItemChecklist] InitialScan pass1 failed: {e.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Pass 2: walk every entity in the default world that carries a
        /// <c>ContainedObjectsBuffer</c> and is NOT a crafting station.
        /// </summary>
        private int ScanPlayerPlacedContainers()
        {
            EntityQuery query = default;
            bool queryCreated = false;
            NativeArray<Entity> entities = default;
            try
            {
                var em = GetEntityManager();
                if (em == null) return 0;

                var queryDesc = new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<ContainedObjectsBuffer>() },
                    None = new[]
                    {
                        // Crafting stations (workbenches, furnaces, anvils, …).
                        // CraftingCD is the only ECS-side IComponentData we
                        // could verify by symbol probe.
                        ComponentType.ReadOnly<CraftingCD>(),

                        // TODO (live-verify): narrow further to exclude
                        // world-spawn chests (puzzle pedestals, dungeon
                        // chests, locked treasure). The {Ancient,Boss,Eerie,
                        // Mold,Locked,NonPaintable,SeaBiome}Chest names from
                        // the spike are EntityMonoBehaviour authoring
                        // scripts, not IComponentData — they can't filter
                        // the query. Verifying the correct discriminator
                        // requires logging em.GetComponentTypes(chestEntity)
                        // on a freshly placed wooden chest vs. a world-spawn
                        // chest and diff'ing — see spike doc §Live-Verification.
                    }
                };

                query = em.Value.CreateEntityQuery(queryDesc);
                queryCreated = true;
                entities = query.ToEntityArray(Allocator.Temp);

                int count = 0;
                foreach (var entity in entities)
                {
                    if (!em.Value.HasBuffer<ContainedObjectsBuffer>(entity))
                        continue;
                    var buf = em.Value.GetBuffer<ContainedObjectsBuffer>(entity);
                    count += ConsumeBuffer(buf);
                }
                return count;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ItemChecklist] InitialScan pass2 failed: {e.Message}");
                return 0;
            }
            finally
            {
                if (entities.IsCreated) entities.Dispose();
                if (queryCreated) query.Dispose();
            }
        }

        /// <summary>
        /// Walks a <c>ContainedObjectsBuffer</c> and forwards every
        /// non-empty slot's objectID to the checklist state. Returns the
        /// number of slots that were forwarded (not the number of items
        /// newly added — <see cref="ChecklistState.SetOwned"/> de-dupes
        /// internally).
        /// </summary>
        private int ConsumeBuffer(DynamicBuffer<ContainedObjectsBuffer> buf)
        {
            int count = 0;
            for (int i = 0; i < buf.Length; i++)
            {
                int id = (int) buf[i].objectData.objectID;
                if (id == 0) continue;
                state.SetOwned(id, true, OwnSource.InitialScan);
                count++;
            }
            return count;
        }

        /// <summary>
        /// Resolves the default ECS world's <c>EntityManager</c> by value,
        /// returning <c>null</c> if the world is not yet up. Returned as a
        /// nullable so call-sites can guard with a single null-check.
        /// </summary>
        private static EntityManager? GetEntityManager()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return null;
            return world.EntityManager;
        }
    }
}
