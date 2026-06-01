using System.Linq;
using CoreLib.Submodule.UserInterface.Interface;
using PugMod;
using UnityEngine;

namespace ItemChecklist.UI
{
    public class ItemChecklistWindow : UIelement, IModUI
    {
        // Editor-wired serialized fields
        public GameObject root;
        public SpriteRenderer background;
        public PugText title;
        public Transform rowsContent;     // assigned to RowsContainer/Content in Editor
        public GameObject rowPrefab;      // assigned to ItemRow.prefab in Editor
        public UIScrollWindow scrollWindow;
        public AscDescToggle ascDescToggle;
        public DropdownWidget sortDropdown;

        private ItemChecklistContent _content;

        private ItemChecklistContent Content
        {
            get
            {
                if (_content == null && rowsContent != null)
                    _content = rowsContent.GetComponent<ItemChecklistContent>();
                return _content;
            }
        }

        private static readonly string[] SortLabels = { "Name", "Rarity", "Found", "Category" };

        private static readonly MemberInfo MiScrollable = typeof(UIScrollWindow).GetMembersChecked().FirstOrDefault(x => x.GetNameChecked() == "_scrollable");
        private static readonly MemberInfo MiUpdateScrollHeight = typeof(UIScrollWindow).GetMembersChecked().FirstOrDefault(x => x.GetNameChecked() == "UpdateScrollHeight");

        // IModUI implementation
        public GameObject Root => root;
        public bool ShowWithPlayerInventory => false;
        public bool ShouldPlayerCraftingShow => false;

        public static ItemChecklistWindow Instance { get; private set; }

        protected void Awake()
        {
            Instance = this;
            DiscoveredState.Instance.Changed += OnDiscoveryChanged;
            HideUI();
        }

        private void OnDestroy()
        {
            DiscoveredState.Instance.Changed -= OnDiscoveryChanged;
            if (Instance == this) Instance = null;
        }

        public void ShowUI()
        {
            root.SetActive(true);
            ApplyTheme();
            PopulateContent();
            WireControls();
        }

        private void WireControls()
        {
            var model = ItemChecklistMod.ListView;
            if (model == null) return;
            model.OnResultsChanged -= OnViewResultsChanged;
            model.OnResultsChanged += OnViewResultsChanged;
            if (ascDescToggle != null)
                ascDescToggle.Configure(model.Ascending, asc => { model.Ascending = asc; });
            if (sortDropdown != null)
                sortDropdown.Configure(SortLabels, (int)model.Mode, i => { model.Mode = (SortMode)i; });
        }

        public void HideUI()
        {
            // Rows persist in the pool across hide/show; no per-entry Destroy.
            root.SetActive(false);
        }

        private void PopulateContent()
        {
            var content = Content;
            var catalog = ItemChecklistMod.Catalog;
            if (content == null || catalog == null || rowPrefab == null) return;

            float perfT0 = UnityEngine.Time.realtimeSinceStartup;

            content.Init(rowPrefab);
            content.EnsurePool();
            var model = ItemChecklistMod.ListView;
            content.SetCount(model != null ? model.Count : catalog.Count);

            // Wire IScrollable + refresh scroll range, then snap to top. Uses
            // API.Reflection (sandbox-safe), NOT System.Reflection on MemberInfo.Name.
            if (scrollWindow != null)
            {
                API.Reflection.SetValue(MiScrollable, scrollWindow, content);
                API.Reflection.Invoke(MiUpdateScrollHeight, scrollWindow);
                scrollWindow.ResetScroll();   // SetScrollValue(1f) = top
            }
            // Forced rebind so states changed while hidden are reflected even
            // though firstIndex is again 0 after ResetScroll.
            content.RefreshVisible();

            float perfMs = (UnityEngine.Time.realtimeSinceStartup - perfT0) * 1000f;
            UnityEngine.Debug.Log(
                $"[ItemChecklist] PERF spawn={perfMs:F0}ms pool={content.PoolSize} count={catalog.Count}");
        }

        private void ApplyTheme()
        {
            // Vanilla CK CraftingUI theme as 9-slice background.
            // GetCraftingUITheme takes UIManager.CraftingUIThemeType enum
            // (verified from BookMod/Scripts/UI/BookUI.cs:71). Wood is an
            // educated guess from naming convention; if invalid, fallback
            // path loads our own atlas sprite.
            try
            {
                var theme = Manager.ui.GetCraftingUITheme(UIManager.CraftingUIThemeType.Wood);
                if (theme != null && background != null)
                    background.sprite = theme.background;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ItemChecklist] GetCraftingUITheme failed: {ex.Message} — falling back to atlas sprite");
                if (background != null && ItemChecklistMod.AssetBundle != null)
                    background.sprite = ItemChecklistMod.AssetBundle.LoadAsset<Sprite>("ui_panel");
            }

            if (title != null)
                title.Render(FormatTitle());
        }

        private string FormatTitle()
        {
            var catalog = ItemChecklistMod.Catalog;
            var state = DiscoveredState.Instance;
            if (catalog == null || catalog.Count == 0)
                return "Item Checklist";
            float percent = 100f * state.Count / catalog.Count;
            return $"Item Checklist — {state.Count} / {catalog.Count} ({percent:F1}%)";
        }

        private void OnDiscoveryChanged()
        {
            // root (a child) carries visibility; the Window component sits on the
            // parent GameObject, which stays active even when hidden.
            if (root == null || !root.activeSelf) return;
            if (title != null) title.Render(FormatTitle());
            Content?.RefreshVisible();
        }

        /// <summary>
        /// Re-binds the visible rows from the current ItemCatalog. Called by
        /// ItemCatalogLocChangeHook after a synchronous re-bake. No-op when the
        /// window is not active — there is nothing to refresh.
        /// </summary>
        public void RebindRows()
        {
            if (root == null || !root.activeSelf) return;
            PopulateContent();   // SetCount + UpdateScrollHeight + ResetScroll + RefreshVisible
        }

        /// <summary>
        /// Called when ItemListViewModel.OnResultsChanged fires (sort/filter change).
        /// Updates the row count, refreshes the scroll range, resets to top, and
        /// force-rebinds the visible rows. No-op when the window is not active.
        /// </summary>
        // ReSharper disable once UnusedMember.Local
        private void OnViewResultsChanged()
        {
            if (root == null || !root.activeSelf) return;
            var content = Content;
            if (content == null) return;
            var model = ItemChecklistMod.ListView;
            content.SetCount(model != null ? model.Count : 0);
            if (scrollWindow != null)
            {
                API.Reflection.SetValue(MiScrollable, scrollWindow, content);
                API.Reflection.Invoke(MiUpdateScrollHeight, scrollWindow);
                scrollWindow.ResetScroll();
            }
            content.RefreshVisible();
        }
    }
}
