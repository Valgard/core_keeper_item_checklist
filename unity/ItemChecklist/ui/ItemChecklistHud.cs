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
    /// <para>Visibility follows CK's native HUD idiom (mirrors
    /// PlayerHealthBarUI): each frame the root's localScale is set to
    /// <c>Manager.ui.CalcGameplayUITargetScaleMultiplier()</c> — zero whenever
    /// the gameplay UI is disabled (inventory/menu open, or the checklist
    /// window open via Iter-9's TemporarilyDisableGameplayUI) — and the root is
    /// active only while <c>Manager.sceneHandler.isInGame</c>.</para>
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
                hudRoot.transform.localScale = Manager.ui.CalcGameplayUITargetScaleMultiplier();
                bool inGame = Manager.sceneHandler != null && Manager.sceneHandler.isInGame;
                if (hudRoot.activeSelf != inGame) hudRoot.SetActive(inGame);
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
