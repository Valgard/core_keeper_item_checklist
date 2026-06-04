using PugMod;
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
        public SpriteRenderer checkmark;     // empty checkbox, shown on every row
        public SpriteRenderer checkFill;     // requirement icon inside the box, discovered only
        public SpriteRenderer rarityBorder;   // Iter-6: rarity frame, shown for Uncommon+
        public PugText levelText;    // Iter-10: right-aligned "Lv N" column ("—" if level 0 / undiscovered)
        public PugText valueText;    // Iter-10: right-aligned sell-value column ("—" if unsellable / undiscovered)
        public SpriteRenderer coinIcon;   // Iter-10: Ancient Coin glyph beside the value (shown only when a value is shown)

        public const float RowHeight = 1.5f; // world units (~24px at 16 PPU)

        // Ancient Coin icon, resolved once from the game database and shared by
        // every row (the coin shown beside sell values).
        private static Sprite s_coinSprite;
        private static bool s_coinResolved;

        private static Sprite CoinSprite()
        {
            if (!s_coinResolved)
            {
                s_coinResolved = true;
                var info = PugDatabase.GetObjectInfo(ObjectID.AncientCoin, 0);
                if (info != null)
                    s_coinSprite = info.smallIcon != null ? info.smallIcon : info.icon;
            }
            return s_coinSprite;
        }

        public void Bind(int objectId, Sprite iconSprite, string name, bool isDiscovered,
            Color rarityColor, Rarity rarity, int level, int sellValue)
        {
            if (isDiscovered)
            {
                if (icon != null) { icon.sprite = iconSprite; icon.enabled = true; }
                if (label != null) label.Render(name);
                if (placeholder != null) placeholder.gameObject.SetActive(false);
            }
            else
            {
                if (icon != null) icon.enabled = false;
                if (label != null) label.Render("???");
                if (placeholder != null) placeholder.gameObject.SetActive(true);
            }

            // Checkbox: empty box on every row; the requirement icon fills it only
            // when the item is discovered (the checklist "done" tick).
            if (checkmark != null) checkmark.enabled = true;
            if (checkFill != null) checkFill.enabled = isDiscovered;

            // Iter-6 rarity colouring. Set the colour AFTER Render(): SetTempColor
            // writes the glyph SpriteRenderers that Render() rebuilds, so a colour
            // set before Render() would be discarded. keepColorOnStart:true makes the
            // tint survive PugText's renderOnStart re-render (first open after a fresh
            // row instantiate), which would otherwise reset glyphs to style.color and
            // leave the tint blank until the next RefreshVisible.
            if (label != null) label.SetTempColor(rarityColor, keepColorOnStart: true);
            if (rarityBorder != null)
            {
                rarityBorder.color = rarityColor;
                rarityBorder.enabled = rarity >= Rarity.Uncommon;   // Poor + Common: no border
            }

            // Iter-10: Level + Value columns. Undiscovered = "—"/"—" (no spoiler).
            // Level 0 and unsellable (sellValue < 0) render "—".
            const string Dash = "—";
            if (levelText != null)
                levelText.Render(isDiscovered && level > 0 ? $"Lv {level}" : Dash);
            if (valueText != null)
                valueText.Render(isDiscovered && sellValue > 0 ? sellValue.ToString() : Dash);

            if (coinIcon != null)
            {
                bool showCoin = isDiscovered && sellValue > 0;
                if (showCoin && coinIcon.sprite == null)
                    coinIcon.sprite = CoinSprite();
                coinIcon.enabled = showCoin && coinIcon.sprite != null;
            }
        }
    }
}
