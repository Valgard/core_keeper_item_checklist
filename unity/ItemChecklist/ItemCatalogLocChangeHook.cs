using System;
using I2.Loc;
using ItemChecklist.UI;
using UnityEngine;

namespace ItemChecklist
{
    /// <summary>
    /// Subscribes to I2.Loc's language-change event (verified in Iter-3.6
    /// diagnose D3 as `I2.Loc.LocalizationManager.OnLocalizeEvent` —
    /// `public static event Action`). On change, synchronously re-bakes
    /// the ItemCatalog and (if the window is currently active) re-binds
    /// its rows. Idle if Catalog is not yet initialized (e.g.
    /// language-change fires in the main menu before the first world is
    /// loaded — the WorldLoadHook will pick up the new language on the
    /// next world load).
    ///
    /// Subscribe() is called once from ItemChecklistMod.Init() (Plan-Task 9).
    /// Subscription to a public static event on a trusted DLL is presumed
    /// sandbox-safe; if Plan-Task 10's smoke-test shows a sandbox failure,
    /// the fallback plan is to drop this subscription and instead poll
    /// LocalizationManager.CurrentLanguage in PlayerController.ManagedUpdate
    /// (ItemBrowser's proven-safe approach).
    /// </summary>
    internal static class ItemCatalogLocChangeHook
    {
        private static bool subscribed;

        public static void Subscribe()
        {
            if (subscribed) return;
            LocalizationManager.OnLocalizeEvent += OnLocalize;
            subscribed = true;
        }

        /// <summary>Set when a language change requests a re-bake; consumed once
        /// by <see cref="ProcessPending"/> from the mod's Update loop. The re-bake
        /// must NOT run synchronously inside OnLocalizeEvent: I2 fires the event
        /// mid-`DoLocalizeAll`, and the bake's `GetObjectName` re-enters the
        /// half-rebuilt localisation source and throws NRE. Deferring to the next
        /// Update tick (post-DoLocalizeAll) avoids that, and coalesces rapid
        /// successive switches into a single re-bake.</summary>
        public static bool RebakePending;

        private static void OnLocalize()
        {
            if (ItemChecklistMod.Catalog == null) return;   // menu / pre-world: WorldLoadHook bakes with the right language
            RebakePending = true;
        }

        /// <summary>Called from ItemChecklistMod.Update(): performs the deferred
        /// re-bake once, in a stable post-localize frame.</summary>
        public static void ProcessPending()
        {
            if (!RebakePending) return;
            RebakePending = false;   // consume (avoids a stale flag re-baking later)
            if (ItemChecklistMod.Catalog == null) return;
            // Only re-bake while actually in a world. Catalog can be non-null in
            // the main menu (baked in a prior world this session); baking there
            // NREs (no ECS/player). The next world-load bakes in the current
            // language anyway. Same readiness signal as ItemCatalogWorldLoadHook.
            if (Manager.main == null || Manager.main.player == null) return;
            try
            {
                ItemChecklistMod.Catalog.Bake();
                ItemChecklistMod.ListView = new ItemChecklist.UI.ItemListViewModel(ItemChecklistMod.Catalog, DiscoveredState.Instance);
                ItemChecklistWindow.Instance?.RebindRows();
                ItemChecklistHud.Instance?.Refresh();   // re-render counter after a language-change re-bake
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItemChecklist] Deferred loc-change rebake threw: {ex}");   // full stack for diagnosis
            }
        }
    }
}
