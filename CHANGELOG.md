# Changelog

All notable changes to this mod are documented in this file. The format is
loosely based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), without strict adherence — entries 
describe what shipped per release, not every commit.

## [0.9.0] - 2026-06-05

First public release — a deliberate pre-1.0 version. The feature set is
complete; what remains for 1.0 is cosmetic (real pixel-art) and internal
cleanup.

### Added

- **Discovery checklist window (F1).** A scrollable, near-fullscreen window
  listing every discoverable item — vanilla and mod-added alike — each with its
  icon, localized name and discovered/undiscovered state. Undiscovered items are
  spoiler-hidden (shown as `???`). Discovery is tracked per world × per player
  and auto-checked on pickup or initial container scan.
- **Always-on HUD counter.** A permanent readout in the top-right corner (above
  the minimap) shows `N / M (p.p%)` discovered during gameplay and updates live.
  It hides automatically while an inventory, a menu, or the checklist window is
  open, and during the world-load screen.
- **Sorting.** Sort the list by **name**, **rarity**, **level** or **value**,
  each ascending or descending.
- **Faceted filtering.** A multi-select filter across four dimensions —
  **discovery** (discovered / undiscovered), **category** (weapons, armor &
  accessories, tools, food, placeables, materials, valuables, key items,
  instruments, other), **rarity** (Poor … Epic) and **craftability**. Options
  are OR within a dimension and AND across dimensions; a "Clear all" action
  resets every dimension at once.
- **Name search.** Filter the list by typing; matches the localized name in your
  game's language (a search in German finds German names). Undiscovered matches
  still appear as `???`.
- **Per-row Level and Value columns.** Each row shows the item's level (`Lv N`)
  and its sell value in Ancient Coins (with the coin glyph). Both show `—` for
  undiscovered rows and for items that have no level or no sell value.
- **Rarity colouring.** Item names are tinted by rarity, and Uncommon-and-above
  items get a rarity-coloured border around their icon — applied to undiscovered
  rows too, so an unfound Legendary is already distinguishable.
- **Per-permutation cooked-food tracking.** Each concrete `(ingredient1,
  ingredient2)` cooking permutation is tracked separately (e.g. Mushroom Soup ≠
  Tomato Soup), across the Base/Rare/Epic tiers, mirroring Core Keeper's own
  per-permutation discovery. The catalog totals ~10,800 entries.
- **Localisation (English + German).** All checklist UI text follows the game
  language and ships in English and German — including the F1 keybind name in the
  game's Controls menu. Switching the game language updates the checklist live.
  More languages can be added later as data.
- **Functional scrollbar.** A draggable, proportionally-sized scrollbar using
  Core Keeper's native widget, with mouse-wheel support.
