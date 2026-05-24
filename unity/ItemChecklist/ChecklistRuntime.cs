namespace ItemChecklist
{
    /// <summary>
    /// Holds the active per-world runtime components for one
    /// (world × player) pairing. Lives between world-begin and world-end.
    /// Singleton-via-static-field so the Harmony-less polling pipeline
    /// (no UI in G1a) can find it from IMod.Update().
    /// </summary>
    public sealed class ChecklistRuntime
    {
        public static ChecklistRuntime Instance { get; private set; }

        public string PlayerId { get; }
        public string WorldId { get; }
        public ChecklistState State { get; }
        public ItemCatalog Catalog { get; }
        public ChecklistStore Store { get; }
        public InitialContainerScanner Scanner { get; }
        public InventoryPoller Poller { get; }

        private ChecklistRuntime(string playerId, string worldId)
        {
            PlayerId = playerId;
            WorldId = worldId;
            State = new ChecklistState();
            Catalog = new ItemCatalog();
            Store = new ChecklistStore(State, playerId, worldId);
            Scanner = new InitialContainerScanner(State);
            Poller = new InventoryPoller(State);
        }

        /// <summary>
        /// Called by the IMod bootstrap once Manager.main.player is
        /// available. Steps:
        ///   1. Bake the item catalog (now that PugDatabase is populated).
        ///   2. Load any prior state from the PlayerPrefs blob.
        ///   3. If discovered is empty, run the initial container scan
        ///      (first time this (world × player) ever sees the mod).
        ///   4. Reseed the inventory poller so its first Tick doesn't
        ///      fire spurious pickup events for already-scanned items.
        /// </summary>
        public static void Begin(string playerId, string worldId)
        {
            if (Instance != null)
            {
                UnityEngine.Debug.LogWarning("[ItemChecklist] Runtime.Begin called while Instance was non-null — replacing");
                End();
            }

            Instance = new ChecklistRuntime(playerId, worldId);
            Instance.Catalog.Bake();
            Instance.Store.Load();

            if (Instance.State.DiscoveredCount == 0)
            {
                UnityEngine.Debug.Log("[ItemChecklist] First mod-load in this (player × world) — running initial scan");
                Instance.Scanner.Run();
            }
            else
            {
                UnityEngine.Debug.Log($"[ItemChecklist] Resumed prior state ({Instance.State.OwnedCount}/{Instance.State.DiscoveredCount} owned/discovered)");
            }

            // Seed the poller's snapshot AFTER the scan so the first
            // Tick doesn't re-fire SetOwned for items the scan added.
            Instance.Poller.Reseed();
        }

        /// <summary>
        /// Called by IMod bootstrap on world-end / Shutdown. Flushes any
        /// pending state to PlayerPrefs and clears the Instance.
        /// </summary>
        public static void End()
        {
            if (Instance == null) return;
            Instance.Store.FlushIfDirty();
            Instance = null;
        }
    }
}
