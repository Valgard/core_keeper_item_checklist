using UnityEngine;
using UnityEngine.UI;

namespace ItemChecklist.UI
{
    /// <summary>
    /// MonoBehaviour attached to the root of <c>ItemRow.prefab</c>. The
    /// four child references are wired in the Editor's Inspector. Recycled
    /// by <see cref="VirtualScrollList"/> — <see cref="Bind"/> is called
    /// when the row is re-used for a different catalog entry.
    /// </summary>
    public sealed class ItemRowView : MonoBehaviour
    {
        public const float RowHeight = 40f;

        [SerializeField] public Image checkboxImage;
        [SerializeField] public Image iconImage;
        [SerializeField] public Text iconPlaceholderText;
        [SerializeField] public Text label;

        public void Bind(int objectId, Sprite icon, string name, bool isDiscovered)
        {
            if (isDiscovered)
            {
                checkboxImage.color = new Color(0.4f, 0.8f, 0.4f, 1f);
                iconImage.sprite = icon;
                iconImage.color = Color.white;
                iconImage.enabled = icon != null;
                iconPlaceholderText.enabled = false;
                label.text = name;
                label.color = Color.white;
            }
            else
            {
                checkboxImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
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
