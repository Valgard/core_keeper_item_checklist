using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace ItemChecklist
{
    /// <summary>
    /// Harmony postfix on <c>PlayerController.OnOccupied</c> — the D2 winner
    /// from the Iter-3.6 diagnose. Starts a coroutine that waits one frame
    /// (until <c>Manager.main.player</c> is confirmed non-null) before
    /// triggering <see cref="ItemCatalog.Bake"/>. The synchronous-postfix
    /// variant from the original plan-task-6 snippet is insufficient: even
    /// though <c>Manager.main.player</c> is already non-null at <c>OnOccupied</c>,
    /// deferring bake to the next frame gives the ECS client world one update
    /// cycle to process singletons (localization sources, database bank) before
    /// <c>GetObjectName(localize:true)</c> is called.
    ///
    /// <para>Pattern from ItemBrowser <c>ItemBrowserAPI.cs</c> (lines 194-215):
    /// postfix kicks off a coroutine via <c>__instance.StartCoroutine</c>;
    /// the coroutine yields until the world is ready, then calls the bake method.</para>
    ///
    /// <para><strong>NOTE — ClientWorldStateSystem:</strong> ItemBrowser gates
    /// its coroutine on <c>ClientWorldStateSystem.HasRunAtLeastOnce</c>, but
    /// that class (<c>ItemBrowser.Common.Api.ClientWorldStateSystem</c>) is
    /// ItemBrowser's <em>own</em> <c>partial struct ISystem</c> — not a Core
    /// Keeper game type. It is not accessible from this mod. The equivalent
    /// game-side signal is <c>Manager.main != null &amp;&amp;
    /// Manager.main.player != null</c>; this is already true when
    /// <c>OnOccupied</c> fires (confirmed empirically by Iter-3.6 diagnose D2c),
    /// so the <c>WaitUntil</c> resolves on the very next frame, producing the
    /// same one-frame ECS-settle guard that ItemBrowser's pattern intends.</para>
    /// </summary>
    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.OnOccupied))]
    internal static class ItemCatalogWorldLoadHook
    {
        [HarmonyPostfix]
        private static void Postfix(PlayerController __instance)
        {
            // Only the local player triggers a bake — remote players share
            // the same PugDatabase but are not the authoritative session owner.
            // Pattern mirrors ItemBrowser ItemBrowserAPI.cs:196-198.
            if (!__instance.isLocal) return;

            // EarlyInit-race defense: Catalog must exist before we can bake it.
            if (ItemChecklistMod.Catalog == null) return;

            __instance.StartCoroutine(BakeWhenReady());
        }

        private static IEnumerator BakeWhenReady()
        {
            // NOTE: We cannot use ClientWorldStateSystem.HasRunAtLeastOnce here —
            // that type (ItemBrowser.Common.Api.ClientWorldStateSystem) is an
            // ItemBrowser-internal ISystem, not a Core Keeper game type.
            //
            // Manager.main.player != null is the equivalent world-ready signal:
            // confirmed non-null at PlayerController.OnOccupied by Iter-3.6
            // diagnose D2c. The WaitUntil therefore resolves on the first frame
            // after OnOccupied, giving the ECS client world one update cycle
            // (localization singletons, database bank) before Bake() runs.
            yield return new WaitUntil(() => Manager.main != null && Manager.main.player != null);

            try
            {
                ItemChecklistMod.Catalog.Bake();
                ItemChecklistMod.ListView = new ItemChecklist.UI.ItemListViewModel(ItemChecklistMod.Catalog, DiscoveredState.Instance);
                ItemChecklist.UI.ItemChecklistHud.Instance?.Refresh();   // show the total as soon as the catalog is baked
            }
            catch (NullReferenceException ex)
            {
                Debug.LogError($"[ItemChecklist] World-load bake threw NullReferenceException: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Sandbox-safe: do NOT call ex.GetType().Name — Type.Name resolves
                // to MemberInfo.get_Name() and is blocked by the Roslyn sandbox.
                // Log only ex.Message (sandbox-safe string property).
                Debug.LogError($"[ItemChecklist] World-load bake threw (non-NRE): {ex.Message}");
            }
        }
    }
}
