using UnityEngine;
using UnityEngine.UI;

namespace ItemChecklist.UI
{
    /// <summary>
    /// Single row: [checkbox-display] [icon 32×32] [name]. Recycled by
    /// VirtualScrollList — Bind() is called when the row is re-used for
    /// a different catalog entry.
    /// </summary>
    public sealed class ItemRowView : MonoBehaviour
    {
        public const float RowHeight = 40f;

        private Image checkboxImage;
        private Image iconImage;
        private Text iconPlaceholderText;
        private Text label;

        // Static shared font reference (cached on first row creation)
        private static Font sharedFont;

        public static ItemRowView Create(Transform parent)
        {
            if (sharedFont == null) sharedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var go = new GameObject("ItemRow", typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            var rt = (RectTransform) go.transform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(0, RowHeight);

            var row = go.AddComponent<ItemRowView>();
            row.BuildChildren();
            return row;
        }

        private void BuildChildren()
        {
            // Checkbox indicator (simple coloured square — F3 may swap for sprite)
            var cbGo = new GameObject("Checkbox", typeof(RectTransform), typeof(Image));
            cbGo.transform.SetParent(transform, worldPositionStays: false);
            var crt = (RectTransform) cbGo.transform;
            crt.anchorMin = new Vector2(0, 0.5f);
            crt.anchorMax = new Vector2(0, 0.5f);
            crt.pivot = new Vector2(0, 0.5f);
            crt.anchoredPosition = new Vector2(8, 0);
            crt.sizeDelta = new Vector2(20, 20);
            checkboxImage = cbGo.GetComponent<Image>();

            // Icon (32×32, sprite assigned per-bind)
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(transform, worldPositionStays: false);
            var irt = (RectTransform) iconGo.transform;
            irt.anchorMin = new Vector2(0, 0.5f);
            irt.anchorMax = new Vector2(0, 0.5f);
            irt.pivot = new Vector2(0, 0.5f);
            irt.anchoredPosition = new Vector2(36, 0);
            irt.sizeDelta = new Vector2(32, 32);
            iconImage = iconGo.GetComponent<Image>();
            iconImage.preserveAspect = true;

            // "?" placeholder text centered on the icon slot. Shown only
            // for undiscovered items; the icon Image then renders as a
            // plain grey square (no sprite) with this "?" overlayed.
            var phGo = new GameObject("IconPlaceholder", typeof(RectTransform), typeof(Text));
            phGo.transform.SetParent(iconGo.transform, worldPositionStays: false);
            var phrt = (RectTransform) phGo.transform;
            phrt.anchorMin = Vector2.zero; phrt.anchorMax = Vector2.one;
            phrt.offsetMin = Vector2.zero; phrt.offsetMax = Vector2.zero;
            iconPlaceholderText = phGo.GetComponent<Text>();
            iconPlaceholderText.font = sharedFont;
            iconPlaceholderText.alignment = TextAnchor.MiddleCenter;
            iconPlaceholderText.fontSize = 20;
            iconPlaceholderText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            iconPlaceholderText.text = "?";
            iconPlaceholderText.enabled = false;     // default off; Bind sets per row

            // Label (fills the rest)
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(transform, worldPositionStays: false);
            var lrt = (RectTransform) labelGo.transform;
            lrt.anchorMin = new Vector2(0, 0);
            lrt.anchorMax = new Vector2(1, 1);
            lrt.offsetMin = new Vector2(76, 0);
            lrt.offsetMax = new Vector2(-8, 0);
            label = labelGo.AddComponent<Text>();
            label.font = sharedFont;
            label.alignment = TextAnchor.MiddleLeft;
            label.fontSize = 16;
        }

        public void Bind(int objectId, Sprite icon, string name, bool isDiscovered)
        {
            if (isDiscovered)
            {
                checkboxImage.color = new Color(0.4f, 0.8f, 0.4f, 1f);    // green
                iconImage.sprite = icon;
                iconImage.color = Color.white;
                iconImage.enabled = icon != null;
                iconPlaceholderText.enabled = false;
                label.text = name;
                label.color = Color.white;
            }
            else
            {
                checkboxImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);    // grey
                // Spoiler-mask: use the bridge "?" placeholder sprite if
                // available; otherwise fall back to a grey square with
                // the centered "?" text overlay.
                if (UiController.UnknownItemSprite != null)
                {
                    iconImage.sprite = UiController.UnknownItemSprite;
                    iconImage.color = Color.white;
                    iconImage.enabled = true;
                    iconPlaceholderText.enabled = false;
                }
                else
                {
                    iconImage.sprite = null;
                    iconImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
                    iconImage.enabled = true;
                    iconPlaceholderText.enabled = true;
                }
                label.text = "???";
                label.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            }
        }
    }
}
