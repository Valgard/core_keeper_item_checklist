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

        private static void OnLocalize()
        {
            if (ItemChecklistMod.Catalog == null) return;
            try
            {
                ItemChecklistMod.Catalog.Bake();
                ItemChecklistWindow.Instance?.RebindRows();
            }
            catch (NullReferenceException ex)
            {
                Debug.LogError($"[ItemChecklist] Loc-change rebake threw NullReferenceException: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItemChecklist] Loc-change rebake threw (non-NRE): {ex.Message}");
            }
        }
    }
}
