using PugMod;
using UnityEngine;
using UnityEngine.UI;

namespace ItemChecklist.UI
{
    /// <summary>
    /// Top-level UI controller. On first <see cref="Toggle"/> instantiates
    /// the editor-baked <c>ItemChecklistWindow.prefab</c>, wires up the
    /// already-verkabelten view fields, and binds them to the data model.
    /// Subsequent toggles only flip <c>SetActive</c>.
    /// </summary>
    public sealed class UiController
    {
        private const string WindowPrefabPath = "Assets/ItemChecklist/Prefabs/ItemChecklistWindow.prefab";

        private GameObject root;
        private ItemChecklistWindowView view;
        private FilterAndSearchModel model;
        private VirtualScrollList list;
        private UnityInputFieldAdapter inputAdapter;

        // UnknownItemSprite is still loaded out of the AssetBundle here
        // (rather than via [SerializeField] on the row prefab) because
        // ItemRowView.Bind needs to swap between the real item icon and
        // the spoiler placeholder *per row* at runtime — a single shared
        // sprite reference is the simplest plumbing.
        public static Sprite UnknownItemSprite;

        public bool IsVisible => root != null && root.activeSelf;

        public void Toggle()
        {
            Debug.Log($"[ItemChecklist] Toggle() called (root={(root == null ? "null" : "exists")})");
            if (root == null)
            {
                try { BuildUi(); }
                catch (System.Exception e)
                {
                    Debug.LogError($"[ItemChecklist] BuildUi threw: {e}");
                    return;
                }
            }
            if (root == null) { Debug.LogWarning("[ItemChecklist] root null after BuildUi"); return; }
            root.SetActive(!root.activeSelf);
            Debug.Log($"[ItemChecklist] window {(root.activeSelf ? "shown" : "hidden")}");

            try
            {
                if (root.activeSelf)
                {
                    if (inputAdapter == null && view?.searchField != null)
                        inputAdapter = new UnityInputFieldAdapter(view.searchField);
                    if (inputAdapter != null)
                    {
                        Manager.input.SetActiveInputField(inputAdapter);
                        view.searchField?.ActivateInputField();
                        Debug.Log("[ItemChecklist] Input freeze ON");
                    }
                }
                else
                {
                    Manager.input.SetActiveInputField(null);
                    view?.searchField?.DeactivateInputField();
                    Debug.Log("[ItemChecklist] Input freeze OFF");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ItemChecklist] Input-freeze toggle failed: {e.Message}");
            }
        }

        private void BuildUi()
        {
            Debug.Log("[ItemChecklist] BuildUi: loading prefab");

            var catalog = ItemChecklistMod.Catalog;
            if (catalog == null) { Debug.LogWarning("[ItemChecklist] Catalog not baked yet"); return; }

            var bundle = ItemChecklistMod.AssetBundle;
            if (bundle == null) { Debug.LogError("[ItemChecklist] AssetBundle null — cannot load prefab"); return; }

            // Spoiler placeholder for undiscovered items (still standalone
            // sprite, not part of the ui_classic atlas).
            if (UnknownItemSprite == null)
                UnknownItemSprite = bundle.LoadAsset<Sprite>("Assets/ItemChecklist/Art/Bridge/ui_unknown_item.png");

            var prefab = bundle.LoadAsset<GameObject>(WindowPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[ItemChecklist] Window prefab not in bundle: {WindowPrefabPath}");
                return;
            }

            // Parent under CK's UI camera so we inherit its Canvas, its
            // sort order (cursor stays on top), and so UIelement
            // registers in the modal-UI hierarchy that blocks gameplay
            // input while shown. Item Browser pattern.
            var uiCamera = API.Rendering.UICamera;
            if (uiCamera == null)
            {
                Debug.LogError("[ItemChecklist] API.Rendering.UICamera null — cannot parent UI");
                return;
            }
            root = UnityEngine.Object.Instantiate(prefab, uiCamera.transform);
            view = root.GetComponent<ItemChecklistWindowView>();
            if (view == null)
            {
                Debug.LogError("[ItemChecklist] Prefab root missing ItemChecklistWindowView component");
                return;
            }

            // Model + virtual list
            model = new FilterAndSearchModel(catalog, DiscoveredState.Instance);
            list = new VirtualScrollList(view.scrollRect, view.rowContainer, view.rowPrefab, idx =>
            {
                var entry = catalog.GetByIndex(idx);
                return (entry.ObjectId, entry.Icon, entry.DisplayName, DiscoveredState.Instance.IsDiscovered(entry.ObjectId));
            });

            // Wire events
            view.searchField.onValueChanged.AddListener(model.SetSearch);
            view.filterDropdown.onValueChanged.AddListener(i => model.Filter = (DiscoveryFilter) i);
            if (view.closeButton != null)
                view.closeButton.onClick.AddListener(() => { if (root.activeSelf) Toggle(); });
            model.OnResultsChanged += () => { list.SetIndices(model.VisibleIndices); UpdateCounter(); };
            DiscoveredState.Instance.Changed += () => model.Refresh();

            // Initial paint
            list.SetIndices(model.VisibleIndices);
            UpdateCounter();
            Debug.Log("[ItemChecklist] BuildUi: prefab instantiated and wired");
        }

        private void UpdateCounter()
        {
            int total = model.VisibleIndices.Length;
            int disc = model.DiscoveredInFilter;
            float pct = total == 0 ? 0f : 100f * disc / total;
            view.counterLabel.SetText($"{disc} / {total} ({pct:F1}%)");
        }
    }
}
