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
        public DropdownWidget filterDropdown;
        public SearchBar searchBar;
        public ClearSearchButton clearSearchButton;   // declared here; wired to its SearchBar in the window prefab (later task)

        private ItemChecklistContent _content;
        private ItemListViewModel _wiredModel;   // last model whose OnResultsChanged we subscribed to (for clean re-bake unsubscribe)

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
        private static readonly string[] FilterLabels = { "All", "Discovered", "Undiscovered" };

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
            if (_wiredModel != null && !ReferenceEquals(_wiredModel, model))
                _wiredModel.OnResultsChanged -= OnViewResultsChanged;   // release the discarded model from the previous bake
            model.OnResultsChanged -= OnViewResultsChanged;
            model.OnResultsChanged += OnViewResultsChanged;
            _wiredModel = model;
            if (ascDescToggle != null)
                ascDescToggle.Configure(model.Ascending, asc => { model.Ascending = asc; });
            if (sortDropdown != null)
                sortDropdown.Configure(SortLabels, (int)model.Mode, i => { model.Mode = (SortMode)i; });
            if (filterDropdown != null)
                filterDropdown.Configure(FilterLabels, (int)model.Filter, i => { model.Filter = (DiscoveryFilter)i; });
            if (searchBar != null)
                searchBar.SyncFrom(model.SearchText);
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
            // Recompute the sort order on every open. Discoveries happen while the
            // window is CLOSED (the player can't pick items up with the UI open), so
            // OnDiscoveryChanged's active-window guard skips them — without this, a
            // reopen would show a stale order (e.g. a freshly-picked-up item not yet
            // moved into the Found-mode discovered block). Cheap (~one sort of the
            // catalog) and makes every open reflect the current discovered state.
            model?.Recompute();
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
            string headline = $"Item Checklist — {state.Count} / {catalog.Count} ({percent:F1}%)";
            var model = ItemChecklistMod.ListView;
            if (model != null && model.IsFiltered)
                headline += $" · {model.Count} shown";
            return headline;
        }

        private void OnDiscoveryChanged()
        {
            // root (a child) carries visibility; the Window component sits on the
            // parent GameObject, which stays active even when hidden.
            if (root == null || !root.activeSelf) return;
            if (title != null) title.Render(FormatTitle());
            var model = ItemChecklistMod.ListView;
            if (model != null && model.Mode == SortMode.Found)
                model.Recompute();          // Found order depends on discovery → re-sort (OnViewResultsChanged rebinds)
            else
                Content?.RefreshVisible();   // other modes: order unchanged, just repaint the affected row
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
            // After PopulateContent (which Recomputes the fresh model and refreshes content
            // synchronously), re-wire so the control callbacks capture the new ItemListViewModel
            // and the search field re-syncs. Order matters: PopulateContent must run first so its
            // Recompute's OnResultsChanged isn't double-handled before the subscription is (re)set.
            WireControls();
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
            if (title != null) title.Render(FormatTitle());
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
