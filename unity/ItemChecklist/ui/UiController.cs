using PugMod;
using UnityEngine;
using UnityEngine.EventSystems;
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
        // Lazily-built TextInputInterface adapter handed to Manager.input
        // on toggle-open. One-shot, lifetime = UiController instance.
        private UnityInputFieldAdapter inputAdapter;
        private Dropdown filterDropdown;

        // Sprites loaded from our AssetBundle (Item-Browser pattern).
        // Cached on first BuildUi so we don't re-load per show/hide.
        public static Sprite WindowBgSprite;
        public static Sprite TextBgSprite;
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

            // Input-freeze: tell CK's InputManager that a text input is
            // active. CK's polling code (movement, hotbar, etc.) checks
            // activeInputField != null and skips game-input dispatch when
            // set. We wrap our Unity InputField in an adapter that
            // satisfies CK's TextInputInterface.
            try
            {
                if (root.activeSelf)
                {
                    if (inputAdapter == null && searchField != null)
                        inputAdapter = new UnityInputFieldAdapter(searchField);
                    if (inputAdapter != null)
                    {
                        Manager.input.SetActiveInputField(inputAdapter);
                        searchField?.ActivateInputField();
                        Debug.Log("[ItemChecklist] Input freeze ON — registered TextInputInterface adapter");
                    }
                }
                else
                {
                    Manager.input.SetActiveInputField(null);
                    searchField?.DeactivateInputField();
                    Debug.Log("[ItemChecklist] Input freeze OFF — cleared adapter");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ItemChecklist] Input-freeze toggle failed: {e.Message}");
            }
        }

        private static Sprite LoadSprite(string name)
        {
            // Unity AssetBundles lowercase all paths and may strip the
            // canonical `Assets/...` prefix depending on how the bundle
            // was built. Try the canonical path first, then a lowercase
            // variant, then fall back to a basename scan over
            // GetAllAssetNames() — robust against either packaging.
            var bundle = ItemChecklistMod.AssetBundle;
            if (bundle == null) return null;

            var canonical = $"Assets/ItemChecklist/Art/Bridge/{name}";
            var sprite = bundle.LoadAsset<Sprite>(canonical)
                      ?? bundle.LoadAsset<Sprite>(canonical.ToLowerInvariant());
            if (sprite != null) return sprite;

            var needle = name.ToLowerInvariant();
            foreach (var asset in bundle.GetAllAssetNames())
            {
                if (asset.EndsWith(needle))
                {
                    sprite = bundle.LoadAsset<Sprite>(asset);
                    if (sprite != null) return sprite;
                }
            }
            Debug.LogWarning($"[ItemChecklist] Sprite not in bundle (tried canonical + lowercase + basename scan): {name}");
            return null;
        }

        // Load a named sub-sprite out of a multi-sprite atlas PNG. Needed
        // for ui_classic.png, which Unity packs as 12+ named sub-sprites
        // (ui_panel, ui_slot_background, ui_scrollbar_handle, ...).
        // LoadAsset<Sprite>(path) only returns the first sub-sprite, so
        // we need LoadAssetWithSubAssets + filter by .name to pick a
        // specific one.
        private static Sprite LoadSubSprite(string atlasFile, string subSpriteName)
        {
            var bundle = ItemChecklistMod.AssetBundle;
            if (bundle == null) return null;
            var canonical = $"Assets/ItemChecklist/Art/Bridge/{atlasFile}";
            var sprites = bundle.LoadAssetWithSubAssets<Sprite>(canonical);
            if (sprites != null)
            {
                foreach (var s in sprites)
                    if (s != null && s.name == subSpriteName) return s;
            }
            Debug.LogWarning($"[ItemChecklist] Sub-sprite '{subSpriteName}' not found in atlas '{atlasFile}' (loaded {sprites?.Length ?? 0} sub-sprites)");
            return null;
        }

        private void BuildUi()
        {
            Debug.Log("[ItemChecklist] BuildUi: starting");

            var catalog = ItemChecklistMod.Catalog;
            if (catalog == null) { Debug.LogWarning("[ItemChecklist] Catalog not baked yet"); return; }

            // Lazy-load bridge sprites on first build.
            // ui_classic.png is a multi-sprite atlas (12+ named sub-sprites).
            // For each UI surface, we pick the right sub-sprite so the
            // 9-slice borders defined in the atlas .meta are correct
            // automatically. ui_panel  = window background frame,
            // ui_slot_background  = sunken inset (input/dropdown/slots).
            if (WindowBgSprite == null) WindowBgSprite = LoadSubSprite("ui_classic.png", "ui_panel");
            if (TextBgSprite   == null) TextBgSprite   = LoadSubSprite("ui_classic.png", "ui_slot_background");
            if (UnknownItemSprite == null) UnknownItemSprite = LoadSprite("ui_unknown_item.png");

            root = new GameObject("ItemChecklist.Root");
            UnityEngine.Object.DontDestroyOnLoad(root);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            root.AddComponent<CanvasScaler>();
            root.AddComponent<GraphicRaycaster>();

            // CK uses Rewired for input — it likely has no standard
            // UnityEngine.EventSystems.EventSystem in the scene, so our
            // uGUI controls (InputField, Dropdown) get no clicks/keys
            // routed to them. Create our own EventSystem if none exists.
            if (EventSystem.current == null)
            {
                var esGo = new GameObject("ItemChecklist.EventSystem");
                UnityEngine.Object.DontDestroyOnLoad(esGo);
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<StandaloneInputModule>();
                Debug.Log("[ItemChecklist] Created own EventSystem (no existing one found)");
            }
            else
            {
                Debug.Log($"[ItemChecklist] EventSystem already present: {EventSystem.current.name}");
            }

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Window background — wood-theme 9-slice from bridge bundle.
            var window = new GameObject("Window", typeof(RectTransform), typeof(Image));
            window.transform.SetParent(root.transform, worldPositionStays: false);
            var wrt = (RectTransform) window.transform;
            wrt.anchorMin = new Vector2(0.7f, 0.1f);
            wrt.anchorMax = new Vector2(0.95f, 0.9f);
            wrt.offsetMin = Vector2.zero; wrt.offsetMax = Vector2.zero;
            var windowImg = window.GetComponent<Image>();
            if (WindowBgSprite != null)
            {
                windowImg.sprite = WindowBgSprite;
                windowImg.type = Image.Type.Sliced;
                windowImg.color = Color.white;
            }
            else
            {
                windowImg.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);     // fallback
            }

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
            var bg = go.GetComponent<Image>();
            if (TextBgSprite != null)
            {
                bg.sprite = TextBgSprite;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
            }
            else
            {
                bg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            }

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
            var bg = go.GetComponent<Image>();
            if (TextBgSprite != null)
            {
                bg.sprite = TextBgSprite;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
            }
            else
            {
                bg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            }

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
