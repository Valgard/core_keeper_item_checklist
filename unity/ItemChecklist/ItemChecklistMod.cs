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

        // Single-step diagnostic for the Update loop. Logs ONCE per state
        // change so we can see why the snapshot isn't applying without
        // spamming the log every frame.
        private string lastLoggedDiag;

        public void Update()
        {
            if (Manager.main == null || Manager.main.player == null)
            {
                if (lastAppliedFor != null) lastAppliedFor = null;
                LogDiag("no-player");
                return;
            }

            string activeGuid = null;
            string diag = "ok";
            try
            {
                var playerEntity = Manager.main.player.entity;
                if (playerEntity == Entity.Null) { LogDiag("entity-null"); return; }
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated) { LogDiag("world-not-ready"); return; }
                var em = world.EntityManager;
                bool hasGuid = em.HasComponent<CharacterGuidCD>(playerEntity);
                if (!hasGuid)
                {
                    LogDiag($"no-CharacterGuidCD on entity {playerEntity.Index}");
                    return;
                }
                var hash = em.GetComponentData<CharacterGuidCD>(playerEntity).Value;
                if (hash == default(Unity.Entities.Hash128)) { LogDiag("default-hash"); return; }
                activeGuid = hash.ToString();
            }
            catch (System.Exception e)
            {
                LogDiag($"exception: {e.GetType().Name}: {e.Message}");
                return;
            }

            if (string.IsNullOrEmpty(activeGuid)) { LogDiag("empty-guid"); return; }
            if (activeGuid == lastAppliedFor) return;     // already applied, silent

            if (!CharacterDataDiscoverySnapshot.Cache.TryGetValue(activeGuid, out var ids))
            {
                LogDiag($"cache-miss for guid {activeGuid} (cache has {CharacterDataDiscoverySnapshot.Cache.Count} entries)");
                return;
            }

            DiscoveredState.Instance.Snapshot(ids);
            lastAppliedFor = activeGuid;
            Debug.Log($"[ItemChecklist] Snapshot applied: {ids.Length} ids for char {activeGuid}");
            lastLoggedDiag = diag;
        }

        private void LogDiag(string reason)
        {
            if (reason == lastLoggedDiag) return;
            lastLoggedDiag = reason;
            Debug.Log($"[ItemChecklist] Update diag: {reason}");
        }
    }
}
