using UnityEngine;
using UnityEngine.UI;

namespace ItemChecklist.UI
{
    /// <summary>
    /// Top-level UI controller. Builds the window (top-bar + scroll list
    /// + counter) on first Toggle(); shows/hides on subsequent calls.
    /// Read-only display of DiscoveredState — no manual-toggle UI per
    /// post-pivot spec (CK is source of truth).
    /// </summary>
    public sealed class UiController
    {
        private GameObject root;
        private FilterAndSearchModel model;
        private VirtualScrollList list;
        private Text counterLabel;
        private InputField searchField;
        private Dropdown filterDropdown;

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
        }

        private void BuildUi()
        {
            Debug.Log("[ItemChecklist] BuildUi: starting");

            var catalog = ItemChecklistMod.Catalog;
            if (catalog == null) { Debug.LogWarning("[ItemChecklist] Catalog not baked yet"); return; }

            root = new GameObject("ItemChecklist.Root");
            UnityEngine.Object.DontDestroyOnLoad(root);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            root.AddComponent<CanvasScaler>();
            root.AddComponent<GraphicRaycaster>();

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Window background
            var window = new GameObject("Window", typeof(RectTransform), typeof(Image));
            window.transform.SetParent(root.transform, worldPositionStays: false);
            var wrt = (RectTransform) window.transform;
            wrt.anchorMin = new Vector2(0.7f, 0.1f);
            wrt.anchorMax = new Vector2(0.95f, 0.9f);
            wrt.offsetMin = Vector2.zero; wrt.offsetMax = Vector2.zero;
            window.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

            // Top bar
            var topBar = new GameObject("TopBar", typeof(RectTransform));
            topBar.transform.SetParent(window.transform, worldPositionStays: false);
            var trt = (RectTransform) topBar.transform;
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1);
            trt.pivot = new Vector2(0.5f, 1);
            trt.sizeDelta = new Vector2(0, 40);
            trt.anchoredPosition = Vector2.zero;

            // Search field
            searchField = BuildInputField(topBar.transform, font,
                new Vector2(0, 0.1f), new Vector2(0.65f, 0.9f), new Vector2(8, 0), new Vector2(-4, 0));

            // Filter dropdown
            filterDropdown = BuildDropdown(topBar.transform, font,
                new Vector2(0.65f, 0.1f), new Vector2(1, 0.9f), new Vector2(4, 0), new Vector2(-8, 0),
                new System.Collections.Generic.List<string> { "All", "Discovered", "Undiscovered" });

            // Scroll list
            var (sr, content) = BuildScrollRect(window.transform,
                new Vector2(0, 0.05f), new Vector2(1, 0.92f), new Vector2(4, 0), new Vector2(-4, 0));

            // Counter
            var counterGo = new GameObject("Counter", typeof(RectTransform), typeof(Text));
            counterGo.transform.SetParent(window.transform, worldPositionStays: false);
            var crt = (RectTransform) counterGo.transform;
            crt.anchorMin = new Vector2(0, 0); crt.anchorMax = new Vector2(1, 0.05f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            counterLabel = counterGo.GetComponent<Text>();
            counterLabel.font = font;
            counterLabel.alignment = TextAnchor.MiddleCenter;
            counterLabel.color = Color.white;
            counterLabel.fontSize = 14;

            // Model + list
            model = new FilterAndSearchModel(catalog, DiscoveredState.Instance);
            list = new VirtualScrollList(sr, content, idx =>
            {
                var entry = catalog.GetByIndex(idx);
                return (entry.ObjectId, entry.Icon, entry.DisplayName, DiscoveredState.Instance.IsDiscovered(entry.ObjectId));
            });

            // Wire events
            searchField.onValueChanged.AddListener(model.SetSearch);
            filterDropdown.onValueChanged.AddListener(i => model.Filter = (DiscoveryFilter) i);
            model.OnResultsChanged += () => { list.SetIndices(model.VisibleIndices); UpdateCounter(); };
            DiscoveredState.Instance.Changed += () => model.Refresh();

            // Initial paint
            list.SetIndices(model.VisibleIndices);
            UpdateCounter();
        }

        private void UpdateCounter()
        {
            int total = model.VisibleIndices.Length;
            int disc = model.DiscoveredInFilter;
            float pct = total == 0 ? 0f : 100f * disc / total;
            counterLabel.text = $"{disc} / {total} ({pct:F1}%)";
        }

        // ---- small builder helpers ----

        private static InputField BuildInputField(Transform parent, Font font,
            Vector2 amin, Vector2 amax, Vector2 omin, Vector2 omax)
        {
            var go = new GameObject("Search", typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, worldPositionStays: false);
            var rt = (RectTransform) go.transform;
            rt.anchorMin = amin; rt.anchorMax = amax;
            rt.offsetMin = omin; rt.offsetMax = omax;
            go.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, worldPositionStays: false);
            var trt = (RectTransform) textGo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(4, 0); trt.offsetMax = new Vector2(-4, 0);
            var text = textGo.GetComponent<Text>();
            text.font = font; text.color = Color.white; text.fontSize = 14;
            text.alignment = TextAnchor.MiddleLeft;

            var inp = go.GetComponent<InputField>();
            inp.textComponent = text;
            return inp;
        }

        private static Dropdown BuildDropdown(Transform parent, Font font,
            Vector2 amin, Vector2 amax, Vector2 omin, Vector2 omax,
            System.Collections.Generic.List<string> options)
        {
            var go = new GameObject("Filter", typeof(RectTransform), typeof(Image), typeof(Dropdown));
            go.transform.SetParent(parent, worldPositionStays: false);
            var rt = (RectTransform) go.transform;
            rt.anchorMin = amin; rt.anchorMax = amax;
            rt.offsetMin = omin; rt.offsetMax = omax;
            go.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // Dropdown requires a Label child for the displayed option text
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, worldPositionStays: false);
            var lrt = (RectTransform) labelGo.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(8, 0); lrt.offsetMax = new Vector2(-20, 0);
            var label = labelGo.GetComponent<Text>();
            label.font = font; label.color = Color.white; label.fontSize = 14;
            label.alignment = TextAnchor.MiddleLeft;

            var dd = go.GetComponent<Dropdown>();
            dd.captionText = label;
            dd.options.Clear();
            foreach (var opt in options) dd.options.Add(new Dropdown.OptionData(opt));
            dd.value = 0; dd.RefreshShownValue();
            return dd;
        }

        private static (ScrollRect, RectTransform) BuildScrollRect(Transform parent,
            Vector2 amin, Vector2 amax, Vector2 omin, Vector2 omax)
        {
            var go = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(Mask));
            go.transform.SetParent(parent, worldPositionStays: false);
            var rt = (RectTransform) go.transform;
            rt.anchorMin = amin; rt.anchorMax = amax;
            rt.offsetMin = omin; rt.offsetMax = omax;
            go.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 1f);
            go.GetComponent<Mask>().showMaskGraphic = true;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(go.transform, worldPositionStays: false);
            var crt = (RectTransform) contentGo.transform;
            crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1);
            crt.pivot = new Vector2(0.5f, 1);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = new Vector2(0, 0);

            var sr = go.GetComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true;
            sr.viewport = rt;
            sr.content = crt;
            return (sr, crt);
        }
    }
}
