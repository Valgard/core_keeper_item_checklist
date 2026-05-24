# Sprite Spec — Item Checklist Mod

This document specifies every sprite needed for the final published
mod. It serves three purposes:

1. **Reference during AssetBundle Prefab design** (which sprites slot
   where, what 9-slice borders, etc.)
2. **Replacement checklist for the Gemini-or-other-artist pass**
   before mod.io publish — the bridge sprites in
   `unity/ItemChecklist/Art/Bridge/` are placeholders that MUST be
   replaced one-for-one with sprites of identical dimensions and
   purpose
3. **Visual-quality acceptance criterion** — once all sprites match
   this spec and look CK-native, the mod is publish-ready visually

## Bridge sources (DEV ONLY)

All current sprites in `unity/ItemChecklist/Art/Bridge/` are from
[Item Browser](https://github.com/moorowl/ItemBrowser) (MIT-licensed,
©2026 moorowl). Gitignored. Replace before mod.io publish so the final
distribution contains zero IB-derived bits.

## Style guide

- **Pixel art**, no anti-aliasing
- **CK palette** — earthy browns, warm wood-tones for default theme;
  cool greys for surfaces; the game's UI is generally low-saturation
- **Pixel grid** — 16×16 base tile (CK convention); UI atlas tiles
  are typically 8×8 or 16×16 corners
- **9-slice friendly** — every "frame" sprite must have clearly
  defined corner / edge / center regions so Unity's `Image.type =
  Sliced` can scale them to arbitrary window sizes without distortion

## Sprite inventory

### `ui_classic.png` — Wood-theme window atlas (PRIMARY)

- **Dimensions:** 48×41 px
- **Purpose:** Background frame for the main window. Used by Unity's
  9-slice on a single `Image` component covering the full window.
- **9-slice borders (left/top/right/bottom):** measured from the
  Item-Browser source as approximately `8/8/8/8` (each corner is an
  8×8 tile, center is the 32×25 fill).
- **Visual:** brown wooden plank look, riveted corners, slight
  inner-shadow, matches CK's chest-UI background

### `ui_unknown_item.png` — Undiscovered item placeholder

- **Dimensions:** 16×16 px
- **Purpose:** Stand-in icon for items the player has never picked
  up. Replaces the real item icon in `ItemRowView` when
  `!DiscoveredState.IsDiscovered(id)`.
- **9-slice borders:** none (atomic sprite, never stretched)
- **Visual:** dark silhouette with `?` cutout, or pure grey square
  with `?` overlay — matches CK's "unknown" iconography from the
  Bestiary

### `ui_text_background.png` — Input/search-field background

- **Dimensions:** 16×16 px
- **Purpose:** Background for the search `InputField` and the filter
  `Dropdown` caption area.
- **9-slice borders:** `4/4/4/4` (small inner padding, edges remain
  crisp when stretched horizontally)
- **Visual:** sunken inset look, darker than the window background,
  thin border to imply "you can type here"

### `ui_rarity_border.png` — Item-icon rarity frame (OPTIONAL — Phase 2)

- **Dimensions:** 40×8 px (sprite-strip with one frame per rarity tier)
- **Purpose:** Wraps the item icon in the list row to show rarity.
  CK has rarity colors (Poor/Common/Uncommon/Rare/Epic/Legendary).
- **Not required for Phase 1.** Skip unless we want to surface rarity
  visually in the list.

### `white_pixel.png`, `grid.png`, `grid_cell.png` — Utility (LOW PRIORITY)

- **`white_pixel.png` (1×1)**: utility for color-tinted UI primitives.
  Useful if any UI element needs a pure color rect without a
  meaningful sprite. Probably not needed if we just leave `Image.sprite
  = null` (Unity renders a white square by default in that case).
- **`grid.png`, `grid_cell.png`** (~16×16): grid backgrounds for list
  rows, if we want alternating-row highlights. Optional cosmetic.

### Checkbox sprites — TO BE CREATED (not in Item Browser bridge)

Item Browser doesn't use traditional checkboxes (their browser is a
catalog, not a tick-list), so the bridge has no checkbox art. We need
to create:

- **`checkbox_unchecked.png`** — 16×16 — empty grey-bordered square
- **`checkbox_checked.png`** — 16×16 — same border + green check mark
  in CK pixel-style

For the bridge phase: use Unity-default white squares with color
tints (green / grey) in `ItemRowView`. Acceptable until real sprites
land.

### Scrollbar — TO BE CREATED OR REUSED

CK has its own scrollbar visual in chest-UIs. Either:

1. Bridge phase: Unity default `Scrollbar` look (functional, looks
   alien)
2. Adopt `ui_classic.png` 9-slice for scrollbar track
3. Create `scrollbar_track.png` (4×16 9-slice) + `scrollbar_thumb.png`
   (4×8 9-slice) for final phase

## 9-slice configuration reference

In the Unity Editor's Sprite Editor (per imported sprite):

```
Image Type: Sliced
Border:
  L: 8 px from left edge
  T: 8 px from top edge
  R: 8 px from right edge
  B: 8 px from bottom edge
Pixels Per Unit: 16 (matches CK base tile)
Filter Mode: Point (no filter)   — required for pixel-art crispness
Compression: None                — keep PNG as-is, no DXT
```

For `ui_unknown_item.png` and other atomic sprites: same settings
except `Image Type: Simple` (no 9-slice).

## Pre-publish checklist

Before pushing a build to mod.io, verify ALL of:

- [ ] Every PNG in `unity/ItemChecklist/Art/` is own/commissioned work
      (no Item-Browser bridges remain)
- [ ] No file in the published AssetBundle has a checksum matching the
      original Item Browser sprites (sample-diff if uncertain)
- [ ] `.gitignore` no longer excludes `unity/ItemChecklist/Art/Bridge/`
      and `Bridge.meta` (so successor sprites get committed and shipped)
- [ ] `unity/ItemChecklist/Art/Bridge/PLACEHOLDER.md` is deleted
- [ ] Mod's `README.md` and `modio-description.md` carry no leftover
      "powered by Item Browser sprites" line
- [ ] Smoke-test in-game with the new sprites: the window looks
      visually consistent with CK's own chest/crafting UI
