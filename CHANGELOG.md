# Changelog

All notable changes to this mod will be documented in this file. The format is
loosely based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), without strict adherence — entries 
describe what shipped per release, not every commit.

## [Unreleased]

### Added

- **Item rarity colouring.** Each row now surfaces its Core Keeper rarity
  (`ObjectInfo.rarity`): the item name is tinted by rarity for **all** items
  (Common/Poor keep the default text colour; Uncommon and above get their
  rarity colour), and a rarity-coloured border frames the icon for
  **Uncommon and above** (Common/Poor show no border, matching CK's own
  `useDefaultColorForCommon` grouping). Colours come from CK's authoritative
  `Manager.ui.GetSlotBorderRarityColor`. Applies to undiscovered (`???`) rows
  too, so an unfound Legendary is already distinguishable. This is a distinct
  axis from the Iter-3.7 cooked-food Base/Rare/Epic tiers. The border sprite is
  a placeholder (a white 9-slice ring, tinted by rarity); real pixel-art is
  deferred to a later iteration.
- **Variation-aware Cooked-Food tracking.** Each concrete `(ingredient1,
  ingredient2)`-permutation is now a separate discovery token in the
  checklist (e.g. Mushroom-Soup ≠ Tomato-Soup), mirroring CK's own
  per-permutation tracking. Catalog grows from ~1750 to ~10720 entries:
  3160 symmetric ingredient pairs × 3 tier-variants (Base/Rare/Epic) =
  9480 cooked-food permutations, plus ~1240 non-cooked items. Each pair
  maps deterministically to one of 15 base-recipe families via
  `primary.turnsIntoFood`; obsolete ingredients
  (`GiantMushroom`, `AmberLarva`) are filtered out.
- **Discovery-quote display in window title:** `"Item Checklist — N / M
  (X.Y%)"` where N = discovered count, M = catalog total. Percent
  rendered in current locale (DE: `0,3%` / EN: `0.3%`).
- **Live title-refresh on discovery events.** Title counter updates
  immediately when the player cooks a new permutation, without
  window-reopen.
- **Performance instrumentation** via `[ItemChecklist] PERF` log entries
  in `Catalog.Bake()` and `Window.SpawnRows()`.
- **Functional scrollbar.** A draggable scrollbar is now wired into the
  checklist window, using Core Keeper's native scrollbar widget with
  placeholder sprites. The handle is draggable, sized proportionally to the
  list, and follows mouse-wheel scrolling. (Visual polish — real sprites,
  exact positioning — is planned for a later iteration.)

### Changed

- **`DiscoveredState` key schema:** `HashSet<int>` → `HashSet<long>` with
  packed `(objectId, variation)` keys via new `PackKey(int, int)` helper.
- **`ItemCatalog.Bake()` two-loop architecture:** standard items (Loop 1
  with `IsCookedFood()`-skip) + new α-enumeration (Loop 2) for cooked-food
  permutations using `CookedFoodCD.GetPrimaryIngredient` +
  `CookingIngredientCD.turnsIntoFood`.
- **Removed DE-locale TrimStart-workaround** (no longer needed since
  family-items with variation=0 are filtered out via `IsCookedFood()`
  before name resolution).
- **Viewport virtualization (Iter-3.8).** The checklist no longer
  instantiates one GameObject per catalog entry on open (the old
  `SpawnRows` froze the window ~905 ms for ~10720 entries). A fixed ~N-row
  pool (`ItemChecklistContent`) is now recycled from the per-frame
  `IScrollable.UpdateContainingElements(scroll)` callback, while
  `GetCurrentWindowHeight()` reports the full catalog height so the
  scroll range still covers every entry. Open latency dropped from
  ~905 ms to ~0–7 ms. Rows persist across close (`HideUI` only deactivates
  the window root) instead of being destroyed and re-spawned.

### Fixed

- **F1 now toggles the window.** Pressing F1 while the checklist is open
  closes it (just like Escape or E); previously F1 only ever opened it, and
  re-pressing it also reset the scroll position.
- **No more overlap with the inventory/crafting UI.** The checklist and
  Core Keeper's own inventory/crafting menus are now mutually exclusive:
  opening a Vanilla menu (inventory, chest, crafting station, vendor…)
  auto-hides the checklist, and F1 no longer opens the checklist on top of
  an already-open Vanilla menu.

### Notes

- **Tier-3 (Rare/Epic) tracking is included but empirically unverified
  for live cooking events** (the verifier-character had insufficient
  cooking-talent for RNG-rolling Rare/Epic at test time). Conservative
  assumption: each cooking pair can yield 3 distinct discovery tokens
  (Base/Rare/Epic). If the assumption is wrong, unreachable tier-entries
  remain grayed-out — functionally correct, cosmetically suboptimal.
