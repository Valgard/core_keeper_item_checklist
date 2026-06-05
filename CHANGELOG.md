# Changelog

All notable changes to this mod will be documented in this file. The format is
loosely based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), without strict adherence — entries 
describe what shipped per release, not every commit.

## [Unreleased]

### Added

- **Always-on HUD counter.** The discovered/total counter from the checklist
  window footer now also appears as a permanent readout in the top-right corner
  (above the minimap) during gameplay: a checkbox-framed icon plus
  `N / M (p.p%)`. It hides automatically while the inventory, a menu, or the
  checklist window is open, and during the world-load screen, and updates live as
  you discover items.
- **Localisation (English + German).** All checklist UI text — search hint,
  sort modes, the faceted filter (section headers, rarity, category, discovery
  and craftable options, "Clear all"), the footer "N shown" counter, the per-row
  level column, and the F1 keybind name in the game's Controls menu — now follows
  the game language and ships in English and German. Switching the game language
  updates the checklist live. More languages can be added later as data. Built on
  Core Keeper's native localisation: terms are authored in a single
  `localization.yaml` and generated into native `TextDataBlock` assets at build
  time.
- **Level and Value columns per row.** Each checklist row now shows a
  right-aligned **Level** column (`Lv N`) and a **Value** column (sell
  value in Ancient Coins, shown with the coin glyph). Both display `—` for
  undiscovered rows (no spoilers) and for items that have no level or no
  sell value. Level is read from CK's `LevelCD.level` component. Value is
  computed via the same logic ItemBrowser uses (`GetValue` sell mode):
  `sellValue == -1` means "auto-compute from rarity + ingredients", not
  "unsellable"; truly unsellable items (carrying `CantBeSoldAuthoring` or
  rarity Legendary) show `—`.
- **Faceted multi-select filter.** Replaces the previous single-dimension
  Discovery dropdown with a combined **"Filter (N)"** popup covering four
  dimensions: **Discovery** (Discovered / Undiscovered), **Category**
  (Weapons / Armor & Accessories / Tools / Food / Placeables / Materials /
  Valuables / Key Items / Instruments / Other), **Rarity** (Poor … Epic),
  and **Craftable** (Craftable / Not craftable). Semantics: OR within a
  dimension, AND across dimensions; an empty dimension is no constraint. A
  **Clear all** action row at the top of the popup resets all dimensions at
  once. The header counts active selections (`Filter (N)`); clearing returns
  it to `Filter`.
- **Level and Value sort modes.** Alongside Name and Rarity, the sort
  dropdown now offers **Level** and **Value** (both ascending/descending).
  The old Found and Category sort modes are removed — discovery state is now
  a filter dimension, and category is likewise a filter dimension.

- **Bigger, near-fullscreen window.** The checklist window is now much larger —
  wider entries and more rows visible — sized to a thin, uniform border that
  matches Core Keeper's own inventory margin. Because Core Keeper's UI camera
  shows a constant world area, the fixed size stays correct (and bordered) on
  every resolution. Font and row height are unchanged (more content, not zoom).
- **Cleaner full-screen view while the checklist is open.** The game HUD is
  hidden while the checklist is up and restored on close: the top-left
  health/food/ability bars (via Core Keeper's own non-persisting menu-open
  mechanism) and the bottom-right button-hint prompts (Tab/E…). Core Keeper's
  keyboard-shortcuts help panel (and its "?" prompt) also no longer appears over
  the checklist. All of this is scoped to the checklist being on screen —
  opening a vanilla inventory restores normal HUD/help behaviour.
- **Discovery filter and name search.** A filter dropdown narrows the
  checklist to **All**, **Discovered**, or **Undiscovered** items. A search box
  filters the list by item name as you type — matched against the localized
  name in your game's language, so a search in German finds German names;
  undiscovered matches still appear as `???`. A clear (✕) button empties the
  search. The window title shows a `· N shown` count while the list is narrowed.

- **Runtime-switchable list sorting.** The checklist can now be sorted by
  four modes — **Name** (display name), **Rarity** (Poor → Legendary),
  **Found** (discovered entries first), or **Category** (`ObjectType`) — each
  with an **ascending/descending** direction. The UI is a dropdown next to the
  scroll controls: the header shows the active sort mode; opening it lists only
  the remaining three modes; selecting one re-sorts and promotes it to the
  header. A separate asc/desc toggle button flips the direction. Sort state is
  in-memory per session (static fields) — survives window close/reopen and a
  catalog re-bake, resets on game restart.
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

- **`SortMode` enum updated.** The enum is now `{ Name, Rarity, Level,
  Value }`. Sessions that had a saved `Found` or `Category` sort mode reset
  to Name on game restart (static state, no persistence).
- **`ItemCatalog.Entry` gained `Level`, `SellValue`, and `IsCraftable`
  fields**, baked from `LevelCD`, `ComputeSellValue` (IB port), and
  `requiredObjectsToCraft` respectively. Used by the new sort modes and
  faceted filter.

- **`ItemChecklistContent` now indexes through `ItemListViewModel.Order`.**
  Row binding reads `model.Order[displayIdx]` (catalog index for that display
  position) instead of accessing the catalog directly by display index.
  `_count` is taken from `model.Count`. No user-visible change — prerequisite
  for sorting and future filtering.
- **`ItemCatalog.Entry` gained an `ObjectType ObjectType` field**, resolved at
  bake from a new `objectTypeCache` (parallel to `rarityCache`). Used by the
  Category sort comparator.
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

- **Raw materials now appear in the checklist.** Ores, bars, raw wood, scrap
  and similar were silently missing because the catalog bake wrongly excluded
  every `ObjectType.NonUsable` item as "garbage". Core Keeper actually files
  raw materials under that type, so they are now included (catalog grows by
  ~126 entries). The handful of internal engine entities also typed
  `NonUsable` (territory spawners, the world Core, the dropped-item entity,
  boss-statue prefab stubs) are still excluded — they have no icon and no
  localized name, so they are not player-facing items.
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
