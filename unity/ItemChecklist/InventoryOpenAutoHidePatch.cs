using HarmonyLib;
using ItemChecklist.UI;

namespace ItemChecklist
{
    /// <summary>
    /// Harmony postfix on <c>UIManager.OnPlayerInventoryOpen</c> — the single
    /// funnel every Vanilla inventory/crafting/vendor open routes through:
    /// the plain TAB inventory (PlayerController.OpenPlayerInventory), plus
    /// OnChestInventoryOpen / OnSalvageAndRepairOpen / OnUpgradeForgeOpen /
    /// OnVanitySlotsOpen / OnVendorOpen / OnBuyWindowOpen, which all delegate
    /// to it. When a Vanilla menu opens while our checklist is showing, hide
    /// the checklist so the two never overlap.
    ///
    /// Deliberately a bare <c>HideUI()</c>, NOT HideAllInventoryAndCraftingUI:
    /// we are inside the open of the Vanilla menu, so a full hide would close
    /// the menu being opened. The short-lived dangling
    /// <c>UserInterfaceModule.currentInterface</c> is harmless — the
    /// just-opened menu covers it and its close clears it; ItemChecklistMod's
    /// visibility-based toggle detection ignores the dangling state.
    ///
    /// <para>Depends on <c>ItemChecklistWindow.ShowWithPlayerInventory == false</c>:
    /// that early-returns OpenModUI before OnPlayerInventoryOpen runs, so opening
    /// the checklist does not trip this postfix. If that flag is ever set true,
    /// the open path would call OnPlayerInventoryOpen and this postfix would hide
    /// the window it just opened — revisit then.</para>
    /// </summary>
    [HarmonyPatch(typeof(UIManager), nameof(UIManager.OnPlayerInventoryOpen))]
    internal static class InventoryOpenAutoHidePatch
    {
        [HarmonyPostfix]
        static void After()
        {
            var window = ItemChecklistWindow.Instance;
            if (window != null && window.Root.activeSelf)
                window.HideUI();
        }
    }
}
