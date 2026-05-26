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

        private readonly System.Collections.Generic.List<ItemRow> _spawnedRows = new System.Collections.Generic.List<ItemRow>();

        private static readonly MemberInfo MiScrollable = typeof(UIScrollWindow).GetMembersChecked().FirstOrDefault(x => x.GetNameChecked() == "_scrollable");
        private static readonly MemberInfo MiUpdateScrollHeight = typeof(UIScrollWindow).GetMembersChecked().FirstOrDefault(x => x.GetNameChecked() == "UpdateScrollHeight");

        // IModUI implementation
        public GameObject Root => root;
        public bool ShowWithPlayerInventory => false;
        public bool ShouldPlayerCraftingShow => false;

        protected void Awake()
        {
            HideUI();
        }

        public void ShowUI()
        {
            root.SetActive(true);
            ApplyTheme();
            SpawnRows();
        }

        public void HideUI()
        {
            ClearRows();
            root.SetActive(false);
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
                title.Render("Item Checklist");
        }
        private void SpawnRows()
        {
            ClearRows();

            var catalog = ItemChecklistMod.Catalog;
            var state = DiscoveredState.Instance;
            if (catalog == null || state == null || rowPrefab == null) return;

            float y = 0f;
            for (int i = 0; i < catalog.Count; i++)
            {
                var entry = catalog.GetByIndex(i);
                var go = Object.Instantiate(rowPrefab, rowsContent);
                go.transform.localPosition = new Vector3(0, y, 0);
                var row = go.GetComponent<ItemRow>();
                if (row != null)
                    row.Bind(entry.ObjectId, entry.Icon, entry.DisplayName, state.IsDiscovered(entry.ObjectId));
                _spawnedRows.Add(row);
                y -= ItemRow.RowHeight;
            }

            // Wire IScrollable so UIScrollWindow knows the content height.
            // Uses API.Reflection (sandbox-safe), NOT System.Reflection on MemberInfo.Name.
            if (scrollWindow != null && rowsContent != null)
            {
                var content = rowsContent.GetComponent<ItemChecklistContent>();
                if (content != null)
                {
                    content.RowCount = _spawnedRows.Count;
                    API.Reflection.SetValue(MiScrollable, scrollWindow, content);
                    API.Reflection.Invoke(MiUpdateScrollHeight, scrollWindow);
                }
            }
        }

        private void ClearRows()
        {
            foreach (var r in _spawnedRows)
            {
                if (r == null) continue;
                // IB-pattern (BasicEntriesListRenderer.ClearList): release PugText pool
                // resources before destroying the GameObject. Without this, PugText's
                // internal shared pool leaks on every Destroy, which manifests as text
                // disappearing on 2nd+ open and main menu PugTexts going blank after
                // first window open.
                foreach (var pugText in r.GetComponentsInChildren<PugText>(true))
                    pugText.Clear();
                Object.Destroy(r.gameObject);
            }
            _spawnedRows.Clear();
        }
    }
}
