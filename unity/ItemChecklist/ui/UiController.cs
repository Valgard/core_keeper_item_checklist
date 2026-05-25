using PugMod;
using UnityEngine;
using UnityEngine.EventSystems;
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

            // No Manager.input.SetActiveInputField anymore — UIelement
            // takes care of blocking gameplay input automatically. CK's
            // UIMouse casts every activeInputField to UIelement, which
            // would throw InvalidCastException every frame for our
            // TextInputInterface-only adapter. The search field's text
            // focus comes from the standard EventSystem (auto-created
            // by Unity if absent on the UICamera).
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

            // Standalone Instantiate — Canvas at root drives rendering.
            // Item Browser's UICamera-parent pattern only works with
            // SpriteRenderer-based UI (no uGUI). We use uGUI, so we keep
            // our own Canvas but route it through the UI camera in
            // Screen-Space-Camera mode (Option B) — Canvas integrates into
            // CK's UI sort hierarchy (helps cursor z-order) while uGUI
            // still renders pixel-perfect.
            root = UnityEngine.Object.Instantiate(prefab);
            UnityEngine.Object.DontDestroyOnLoad(root);
            view = root.GetComponent<ItemChecklistWindowView>();
            if (view == null)
            {
                Debug.LogError("[ItemChecklist] Prefab root missing ItemChecklistWindowView component");
                return;
            }

            var canvas = root.GetComponent<Canvas>();
            if (canvas != null)
            {
                var uiCameraPug = API.Rendering.UICamera;
                var renderCam = uiCameraPug != null ? uiCameraPug.GetComponent<Camera>() : null;
                if (renderCam != null)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = renderCam;
                    canvas.sortingOrder = 50;
                    Debug.Log("[ItemChecklist] Canvas wired to API.Rendering.UICamera (Screen Space - Camera)");
                }
                else
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 32000;
                    Debug.LogWarning("[ItemChecklist] UICamera missing Camera component — falling back to Screen Space - Overlay");
                }
            }
            else
            {
                Debug.LogError("[ItemChecklist] Window prefab root missing Canvas component — add Canvas + CanvasScaler + GraphicRaycaster in the Editor");
                return;
            }

            // uGUI needs an EventSystem for InputField/Dropdown/Button mouse routing
            if (EventSystem.current == null)
            {
                var esGo = new GameObject("ItemChecklist.EventSystem");
                UnityEngine.Object.DontDestroyOnLoad(esGo);
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<StandaloneInputModule>();
                Debug.Log("[ItemChecklist] Created own EventSystem");
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
