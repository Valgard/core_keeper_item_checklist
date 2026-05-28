# Iter-3.7 Cooked-Food Discovery — Empirical Spike Results

**Date:** 2026-05-28
**Spike file:** `unity/ItemChecklist/Iter37DiscoverySpike.cs` (removed after analysis)
**Branch:** `iter-3-6` (preparation work for Iter-3.7)

## Context

During Iter-3.6 in-game testing the user reported that CK shows a separate
"new item" notification for each concrete recipe permutation (Pilz-Suppe,
Tomaten-Suppe, etc.), suggesting CK tracks them as distinct discovery
events even though they share an `objectID` like `CookedSoup`. Decompile
of `SaveManager.SetObjectAsDiscovered` (`Pug.Other.dll` Line 475403)
showed a separate `GetDiscoveredCookedFoods()` tracker plus a hint that
ingredients are encoded into `objectData.variation`:

```csharp
ObjectID primary   = CookedFoodCD.GetPrimaryIngredientFromVariation(objectData.variation);
ObjectID secondary = CookedFoodCD.GetSecondaryIngredientFromVariation(objectData.variation);
objectData.Variation = CookedFoodCD.GetFoodVariation(primary, secondary);
```

The spike harnessed `SaveManager.SetObjectAsDiscovered` with a Harmony
postfix that decoded the variation back to ingredient `ObjectID`s and
logged every discovery event. The user loaded a save where Pilz-Suppe was
not yet discovered, cooked one fresh recipe, then ate / cooked a second
recipe to confirm the encoding distinguishes variants.

## Findings

### 1. Recipe-Permutations are encoded in `ObjectDataCD.variation`

```
[ItemChecklist DISCOVER] objectID=CookedSoup   variation=360453500  newly=True  cookedFood=True  primary=Mushroom            secondary=Mushroom
[ItemChecklist DISCOVER] objectID=CookedSalad  variation=524686716  newly=True  cookedFood=True  primary=GlowingTulipFlower  secondary=Mushroom
```

- `objectID` stays constant across all permutations of a recipe family
- `variation` is a 32-bit int encoding `(primary_ingredient_objectID,
  secondary_ingredient_objectID)` via `CookedFoodCD.GetFoodVariation(p, s)`
- The encoding is invertible: `GetPrimaryIngredientFromVariation` and
  `GetSecondaryIngredientFromVariation` decode the original ObjectIDs
- A Mushroom-Mushroom Soup and a GlowingTulipFlower-Mushroom Salad have
  **different `(objectID, variation)` pairs**, so CK considers them
  separate discoveries

### 2. CK's Discovery API is sandbox-safe to call from mod code

The spike successfully called the following from inside a Harmony-postfix
body in mod code (= Roslyn-sandboxed):

- `PugDatabase.HasComponent<CookedFoodCD>(ObjectDataCD)` → returns `bool`
- `CookedFoodCD.GetPrimaryIngredientFromVariation(int variation)` → returns `ObjectID`
- `CookedFoodCD.GetSecondaryIngredientFromVariation(int variation)` → returns `ObjectID`

No `code security verification failed` events in Player.log — all three
APIs are usable for Iter-3.7's Cooked-Food enumeration + tracking code.

We do not yet have empirical confirmation that the forward direction —
`CookedFoodCD.GetFoodVariation(ObjectID primary, ObjectID secondary)` —
is also sandbox-safe; the decompile shows CK using it freely, but the
spike only exercised the decoders. Iter-3.7 will test it at build time
and confirm in the first sandbox-smoke-test.

### 3. `SetObjectAsDiscovered` fires very frequently (most events not-new)

The spike captured 261 `[ItemChecklist DISCOVER]` events in roughly 30
seconds of gameplay, of which only a handful had `newly=True`. The
not-new firings come from `DetectUndiscoveredObjectsInInventory`
(Pug.Other.dll Line 403341+) which polls every inventory slot on every
inventory update — including mouse-hover inventory operations.

**Iter-3.7 implication:** the new `SaveManagerDiscoveryHook` must filter
on `__result == true` (= newly discovered this call) before mirroring
into `DiscoveredState`, otherwise it duplicates every poll.

### 4. `objectData.variation == 0` for non-Cooked items

Standard items (Wood, CopperOre, WallSandBlock, etc.) all had
`variation=0 cookedFood=False`. Iter-3.6's current behavior — treating
all items as objectID-only — is correct **for non-Cooked items**. The
Iter-3.7 refactor only needs to special-case items with
`CookedFoodCD`-component for variation-aware tracking.

## Iter-3.7 Implementation Spec (rough — full brainstorm in separate session)

### `ItemCatalog.Bake()` — Cooked-Food enumeration

```
for each od in PugDatabase.objectsByType.Keys (variation == 0 only):
    if not categorically-non-item:
        if PugDatabase.HasComponent<CookedFoodCD>(od):
            for each (primary, secondary) in valid-ingredient-pairs:
                variation = CookedFoodCD.GetFoodVariation(primary, secondary)
                resolve DisplayName via GetObjectName(buf{objectData=ObjectDataCD{od.objectID, variation}}, true)
                add Entry(od.objectID, variation, displayName, icon, modOrigin)
        else:
            // existing path: add as one Entry with variation=0
            add Entry(od.objectID, 0, displayName, icon, modOrigin)
```

**Open question for brainstorming:** how to enumerate "valid ingredient pairs"?

- Option A: enumerate every PugDatabase item with `Cookable`/`Ingredient`-component flag
- Option B: query CK's recipe registry directly (location TBD via decompile)
- Option C: discovery-time growth — catalog starts with base-items, hook
  `SetObjectAsDiscovered` adds the concrete permutation as a new Entry
  on first encounter (Strategy B from Iter-3.6 follow-up discussion)

Strategy C might be the most elegant fit — the catalog only contains what
the player has actually encountered, no upfront combinatorial explosion,
no UI wildwuchs for unreachable recipes.

### `DiscoveredState` — variation-aware tracking

Replace `HashSet<int>` with `HashSet<long>` keyed by:

```
((long)objectID << 32) | (uint)variation
```

Plus a helper `IsDiscovered(objectID, variation)` that builds the key.

### `SaveManagerDiscoveryHook` — capture full `ObjectDataCD`

```csharp
static void After(ObjectDataCD objectData, bool __result)
{
    if (!__result) return;   // filter non-new — avoid the inventory-poll flood
    DiscoveredState.Instance.AddOne(objectData.objectID, objectData.variation);
}
```

### `CharacterDataDiscoverySnapshot` — pull initial state including Cooked-Foods

The current snapshot reads from `characterData.discoveredObjects` (the
HashSet). For variation-aware tracking, both that set AND
`Manager.saves.GetDiscoveredCookedFoods()` must be merged. Decompile
shows the latter is just a separate `List<DiscoveredObjectData>` derived
from the same source set — but verifying we get all entries from the
combined sources is important.

### Backwards-compatibility / Save-Migration

Existing IterChecklist saves (Iter-3.6 and earlier) have objectID-only
tracking. Iter-3.7's new `(objectID, variation)`-keyed state can read
those as `(objectID, 0)` entries — non-Cooked items remain correctly
discovered, but Cooked-Food entries from older sessions resolve to
"variation 0" which is the empty-recipe template. **That is fine**:
those base-items remain marked as known, and the player gets new
discovery events for each concrete recipe they cook from then on.
No migration needed.

## Cross-references

- Iter-3.6 design + plan: `docs/superpowers/specs/2026-05-28-itemchecklist-displayname-iter3-6-design.md`
- Iter-3.6 plan: `docs/superpowers/plans/2026-05-28-itemchecklist-displayname-iter3-6.md`
- Iter-3.6 diagnose D1/D2/D3: `docs/research/iter-3-6-diagnose.md`
- DE-locale `" -base"` template artifact + TrimStart fix: commit `0183afb`
- Pug.Other.dll decompile reference lines: 475403 (`SetObjectAsDiscovered`),
  123729, 225653, 428054 (`GetDiscoveredCookedFoods` UI iteration)

## Known still-open items for Iter-3.7

- Rare/Epic-Tier-Items show "Epische  -Torte" with double space (Iter-3.6
  TrimStart doesn't address — leading char is "E", not whitespace). Likely
  fixed automatically by the variation-aware refactor since concrete
  recipes substitute ingredient into the `{0}` placeholder. If not, a
  collapse-double-spaces polish step is trivial to add.
- Performance: 14 Bake() calls per language change (each switch step
  triggers `OnLocalizeEvent`). Acceptable for an infrequent user action,
  but a debounce pattern (`yield WaitForSeconds(0.1)`, reset on additional
  event) would be cleaner.
