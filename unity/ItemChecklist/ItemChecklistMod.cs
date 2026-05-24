using CoreLib;
using CoreLib.Submodule.ControlMapping;
using PugMod;
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
    /// spawn. Each frame, if a player has spawned and we haven't yet
    /// pushed their cached snapshot to <see cref="DiscoveredState"/>,
    /// look up the cache by name and apply it.</para>
    /// </summary>
    public sealed class ItemChecklistMod : IMod
    {
        public static ItemCatalog Catalog { get; private set; }

        // Last character name we applied a snapshot for. Reset when
        // Manager.main.player goes back to null (user returns to main
        // menu) so the next char-load picks up its own cache entry.
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

            string name = Manager.main.player.playerName;
            if (string.IsNullOrEmpty(name)) return;
            if (name == lastAppliedFor) return;     // already applied this load

            if (!CharacterDataDiscoverySnapshot.Cache.TryGetValue(name, out var ids))
                return;     // cache miss — wait until CK deserializes this char

            DiscoveredState.Instance.Snapshot(ids);
            lastAppliedFor = name;
            Debug.Log($"[ItemChecklist] Snapshot applied: {ids.Length} ids for '{name}'");
        }
    }
}
