# Changelog

All notable changes to this mod will be documented in this file. The format is
loosely based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), without strict adherence — entries 
describe what shipped per release, not every commit.

## [Unreleased]

### Added

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

### Notes

- **Tier-3 (Rare/Epic) tracking is included but empirically unverified
  for live cooking events** (the verifier-character had insufficient
  cooking-talent for RNG-rolling Rare/Epic at test time). Conservative
  assumption: each cooking pair can yield 3 distinct discovery tokens
  (Base/Rare/Epic). If the assumption is wrong, unreachable tier-entries
  remain grayed-out — functionally correct, cosmetically suboptimal.
