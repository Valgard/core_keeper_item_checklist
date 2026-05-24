using CoreLib;
using CoreLib.Submodule.ControlMapping;
using PugMod;
using Unity.Entities;
using UnityEngine;

namespace ItemChecklist
{
    /// <summary>
    /// Mod bootstrap. After the Harmony pivot, the heavy lifting is in
    /// the two patch classes (<see cref="SaveManagerDiscoveryHook"/> and
    /// <see cref="CharacterDataDiscoverySnapshot"/>) that mirror CK's
    /// native discovery system into <see cref="DiscoveredState"/>.
    ///
    /// <para>The only non-trivial work this class does is bridge the
    /// timing gap between CK's <c>OnAfterDeserialize</c> (which fires
    /// before <c>Manager.main.player</c> exists) and the active player's
    /// spawn. Each frame, if a player has spawned, look up the cached
    /// snapshot by the player's <c>CharacterGuidCD</c> (ECS component on
    /// the player entity, same-name-resilient).</para>
    /// </summary>
    public sealed class ItemChecklistMod : IMod
    {
        public static ItemCatalog Catalog { get; private set; }

        // Last character GUID we applied a snapshot for. Reset when the
        // player goes back to null (main menu) so the next char-load
        // gets its own snapshot pushed.
        private string lastAppliedFor;

        public void EarlyInit()
        {
            Debug.Log("[ItemChecklist] EarlyInit");
            CoreLibMod.LoadSubmodule(typeof(ControlMappingModule));
        }

        public void Init()
        {
            Debug.Log("[ItemChecklist] Init");
            Catalog = new ItemCatalog();
            Catalog.Bake();
        }

        public void ModObjectLoaded(Object obj) { }
        public void Shutdown() { }

        public void Update()
        {
            // Player went away — clear "applied for" memory so the next
            // character-load gets its own snapshot pushed.
            if (Manager.main == null || Manager.main.player == null)
            {
                if (lastAppliedFor != null) lastAppliedFor = null;
                return;
            }

            // Resolve active char's GUID from the ECS-side CharacterGuidCD.
            // This is the same hash CK stores as CharacterData.characterGuid,
            // so it doubles as our cache key.
            string activeGuid;
            try
            {
                var playerEntity = Manager.main.player.entity;
                if (playerEntity == Entity.Null) return;
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated) return;
                var em = world.EntityManager;
                if (!em.HasComponent<CharacterGuidCD>(playerEntity)) return;
                var hash = em.GetComponentData<CharacterGuidCD>(playerEntity).Value;
                if (hash == default(Unity.Entities.Hash128)) return;
                activeGuid = hash.ToString();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ItemChecklist] CharacterGuidCD lookup failed: {e.Message}");
                return;
            }

            if (string.IsNullOrEmpty(activeGuid)) return;
            if (activeGuid == lastAppliedFor) return;     // already applied

            if (!CharacterDataDiscoverySnapshot.Cache.TryGetValue(activeGuid, out var ids))
                return;     // cache miss — wait until CK deserializes this char

            DiscoveredState.Instance.Snapshot(ids);
            lastAppliedFor = activeGuid;
            Debug.Log($"[ItemChecklist] Snapshot applied: {ids.Length} ids for char {activeGuid}");
        }
    }
}
