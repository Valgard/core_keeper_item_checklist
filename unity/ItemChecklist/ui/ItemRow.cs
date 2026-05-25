using UnityEngine;

namespace ItemChecklist.UI
{
    public sealed class ItemRow : UIelement
    {
        // Editor-wired serialized fields (4-slot structure preserved from ItemRowView)
        public SpriteRenderer background;
        public SpriteRenderer icon;
        public PugText label;
        public PugText placeholder;
        public SpriteRenderer checkmark;

        public const float RowHeight = 2.5f; // world units (~40px at 16 PPU)

        public void Bind(int objectId, Sprite iconSprite, string name, bool isDiscovered)
        {
            if (isDiscovered)
            {
                if (icon != null) { icon.sprite = iconSprite; icon.enabled = true; }
                if (label != null) label.Render(name);
                if (placeholder != null) placeholder.gameObject.SetActive(false);
                if (checkmark != null) checkmark.enabled = true;
            }
            else
            {
                if (icon != null) icon.enabled = false;
                if (label != null) label.Render("???");
                if (placeholder != null) placeholder.gameObject.SetActive(true);
                if (checkmark != null) checkmark.enabled = false;
            }
        }
    }
}
