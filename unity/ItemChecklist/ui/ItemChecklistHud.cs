using UnityEngine;

namespace ItemChecklist.UI
{
    /// <summary>
    /// Always-on HUD counter (top-right) mirroring the window footer's
    /// discovered/total counter. This is NOT a modal CoreLib UI — it is
    /// instantiated directly by <see cref="ItemChecklistMod"/> and parented
    /// under the in-game HUD root, so it must never be passed to
    /// UserInterfaceModule.RegisterModUI.
    ///
    /// <para>Visibility is explicit (not scale-based): <see cref="hudRoot"/> is
    /// activated only while <c>Manager.sceneHandler.isInGame</c> AND
    /// <c>Manager.main.player != null</c> (suppresses the world-load screen) AND
    /// <c>!Manager.ui.isAnyInventoryShowing</c> (covers inventory, crafting and
    /// the checklist window) AND <c>!Manager.menu.IsAnyMenuActive()</c>.
    /// <c>Manager.ui.CalcGameplayUITargetScaleMultiplier()</c> — CK's own HUD
    /// idiom — returns (0,0,0) for a mod HUD and is deliberately NOT used.
    /// Render visibility additionally relies on the prefab being on the HUD Unity
    /// layer (27) at local z=10; see docs/gotchas.md § HUD Counter.</para>
    /// </summary>
    public class ItemChecklistHud : UIelement
    {
        // Editor-wired serialized fields (set in ItemChecklistHUD.prefab).
        public GameObject hudRoot;    // the scaled/toggled container
        public PugText counterText;   // renders the "N / M (p.p%)" string

        public static ItemChecklistHud Instance { get; private set; }

        protected void Awake()
        {
            Instance = this;
            DiscoveredState.Instance.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            DiscoveredState.Instance.Changed -= Refresh;
            if (Instance == this) Instance = null;
        }

        protected override void LateUpdate()
        {
            if (hudRoot != null)
            {
                // Good-HUD-citizen visibility via explicit, proven signals (the
                // CalcGameplayUITargetScaleMultiplier idiom returns (0,0,0) here —
                // it is not a drop-in scale source for a mod HUD). Hidden when the
                // player inventory / crafting / the checklist window (a CoreLib mod
                // UI, covered by the aggregate isAnyInventoryShowing) or any menu is
                // open; shown only while actually in a world.
                bool show = Manager.sceneHandler != null && Manager.sceneHandler.isInGame
                            && Manager.main != null && Manager.main.player != null   // not during the world load screen (isInGame is already true there)
                            && !Manager.ui.isAnyInventoryShowing
                            && !Manager.menu.IsAnyMenuActive();
                if (hudRoot.activeSelf != show) hudRoot.SetActive(show);
            }
            base.LateUpdate();
        }

        /// <summary>Re-render the counter from the current catalog + state.
        /// Cheap to call: runs only on discovery changes and the one-shot
        /// post-bake refresh, never per frame (PugText.Render rebuilds glyph
        /// SpriteRenderers).</summary>
        public void Refresh()
        {
            if (counterText == null) return;
            var catalog = ItemChecklistMod.Catalog;
            int total = (catalog == null) ? 0 : catalog.Count;
            counterText.Render(ProgressFormat.Counter(DiscoveredState.Instance.Count, total));
        }
    }
}
