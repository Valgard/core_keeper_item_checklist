using HarmonyLib;
using ItemChecklist.UI;

namespace ItemChecklist
{
    /// <summary>
    /// While the ItemChecklist window is open, force
    /// InventoryShortCutsButton.ShortcutsCanBeToggled() to return false. Verified
    /// against the decompile: this `public static bool` drives the bottom-right
    /// "?" prompt visuals (UpdateVisuals sets textContainer/icon from its result),
    /// so forcing false hides the prompt — no dead prompt left over.
    ///
    /// <para>It does <b>not</b> by itself disable the S toggle (CK's
    /// ToggleInventoryShortcuts checks isAnyInventoryShowing directly, not this
    /// method); the actual panel stays down via
    /// <see cref="ShortCutsWindowSuppressPatch"/>. The two patches are
    /// complementary: this hides the prompt, that suppresses the panel.</para>
    ///
    /// <para>Released the moment a vanilla inventory opens (our window auto-hides →
    /// Root.activeSelf false), so the prompt works normally for real inventories.</para>
    /// </summary>
    [HarmonyPatch(typeof(InventoryShortCutsButton), "ShortcutsCanBeToggled")]
    internal static class InventoryShortCutsButtonSuppressPatch
    {
        [HarmonyPostfix]
        static void After(ref bool __result)
        {
            var w = ItemChecklistWindow.Instance;
            if (w != null && w.Root != null && w.Root.activeSelf)
                __result = false;
        }
    }
}
