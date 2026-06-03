# ItemChecklist Architecture

This document describes the design and data flow of the ItemChecklist mod
beyond what fits in the mod's `CLAUDE.md` overview.

## UI Architecture

ItemChecklist uses Core Keeper's native `SpriteRenderer`-based UI stack,
mediated by CoreLib's `UserInterfaceModule`. This is the only viable approach
in CK — uGUI (Canvas/Image) is structurally incompatible because CK's
`UIMouse` does a `Physics.Raycast` in the UI layer and Canvas elements have no
Collider. See `CLAUDE.md § Mod-Specific Gotchas` for the structural
explanation and the survey of 10 CK UI mods (all use SpriteRenderer).

### Mod Load → Registration → Mount → Open → Close

**Mod load + registration (EarlyInit → ModObjectLoaded):**

1. `IMod.EarlyInit` — `UserInterfaceModule.LoadSubmodule()`. Must precede
   any `RegisterModUI` call.
2. `IMod.ModObjectLoaded` — `UserInterfaceModule.RegisterModUI(windowPrefab)`.
   Called once when the mod's `GameObject` (from the AssetBundle) is
   available.
3. CoreLib's postfix on `UIManager.Init` instantiates the prefab into
   `UIManager.chestInventoryUI.transform.parent` — this is the canonical
   CK UI mount point used by every IModUI implementation.

**Open path (F1 → visible window):**

4. F1 is a toggle (Iter-4). The keybind (wired since Iter-1 via the CoreLib
   `ControlMappingModule`, polled in `IMod.Update`) drives a 3-way branch
   keyed off *real visibility* (`ItemChecklistWindow.Instance.Root.activeSelf`,
   **not** CoreLib's `currentInterface`, which the auto-hide patch can leave
   transiently stale):
   - window visible → close via
     `Manager.ui.HideAllInventoryAndCraftingUI(forceClose: false)` (the same
     path Escape/E use — see Close path);
   - else a Vanilla menu/inventory is open
     (`Manager.ui.isPlayerInventoryShowing`) → ignore, so F1 never opens on
     top of one;
   - else → `UserInterfaceModule.OpenModUI("ItemChecklist:Window")`.
5. CoreLib calls `ItemChecklistWindow.ShowUI()`.
6. `ShowUI` delegates to `ItemChecklistContent.PopulateContent` (Iter-3.8):
   it lazily grows the fixed row pool (`EnsurePool`), sets the catalog count
   (`SetCount`), reflection-wires the scrollable + invokes `UpdateScrollHeight`,
   then `ResetScroll` + forced `RefreshVisible`. No per-entry instantiation
   happens anymore — the persistent ~N-row pool is recycled by the scroll
   system. See § Viewport Virtualization and the UIScrollWindow Reference
   below.

**Close path (Escape / E / F1 → hidden window):**

7. Player presses Escape or E, **or** F1 while the window is open →
   `HideAllInventoryAndCraftingUI` is called (F1 via the Iter-4 toggle,
   with `forceClose: false` to mirror `PlayerController.CloseAnyOpenInventory`).
8. CoreLib's postfix on `HideAllInventoryAndCraftingUI` calls
   `IModUI.HideUI()` for every registered mod UI **and** clears
   `UserInterfaceModule.currentInterface` (via `ClearModUIData`). Clearing
   it releases the player from menu-state — a bare `HideUI()` on the F1-close
   path would leave `currentInterface` dangling and freeze movement.
9. `ItemChecklistWindow.HideUI()` calls `root.SetActive(false)` only
   (Iter-3.8) — the persistent row pool is **not** destroyed on close. The
   `PugText.Clear()` pool-teardown (the old per-destroy leak fix) moved to
   `ItemChecklistContent.OnDestroy`, which runs only on full pool teardown.

**Mutual exclusion with Vanilla menus (Iter-4):** `InventoryOpenAutoHidePatch`
postfixes `UIManager.OnPlayerInventoryOpen` — the single funnel every Vanilla
inventory/crafting/vendor open routes through (plain TAB via
`PlayerController.OpenPlayerInventory`; chests/stations/vendors via wrappers
that all delegate to it). If the checklist is visible when a Vanilla menu
opens, it calls a **bare** `HideUI()` (not `HideAllInventoryAndCraftingUI`,
which would re-close the just-opening menu). The briefly-dangling
`currentInterface` is harmless: the Vanilla menu covers it and its close
clears it, and the F1 toggle reads `Root.activeSelf`, not `currentInterface`.
This coherence holds **only** while `ShowWithPlayerInventory == false` — that
early-returns `OpenModUI` before `OnPlayerInventoryOpen`, so opening the
checklist does not trip its own postfix.

**Cursor/WASD-block/Escape handling:** all handled by CoreLib and CK's
`isAnyInventoryShowing` postfix chain — zero patches for those. Iter-4 adds
exactly one Harmony postfix (the mutual-exclusion patch above).

### Pattern Matrix (10 surveyed CK UI mods)

| Approach | Mods using it |
|---|---|
| SpriteRenderer + Layer GUI + UIelement | 10 / 10 |
| uGUI (Canvas, Image, Text) | 0 / 10 |
| CoreLib UserInterfaceModule | ~3 (BookMod, DummyMod, ItemChecklist) |
| Custom Harmony-based open/close | ~7 |

Production reference implementations:
- **limoka/BookMod** — ~145 IMod LoC + ~162 UI LoC, uses UserInterfaceModule
- **limoka/DummyMod** — ~87 IMod LoC + ~84 UI LoC, minimal template

---

## UIScrollWindow Reference

This section captures the decompile findings for `UIScrollWindow` (in
`Pug.Other.dll`). Key behavioral facts that affect ItemChecklist are also
summarized in `CLAUDE.md § CK Decompile References`; this section gives the
full internal picture for anyone implementing a new `IScrollable` or
diagnosing scroll behavior.

### Awake Logic

`UIScrollWindow.Awake()` calls `GetComponent<IScrollable>()`. If the result
is `null`, the scroll window **permanently disables itself** — `enabled = false`
and the component stops processing input. The `IScrollable` implementor
(`ItemChecklistContent`) must be on the same `GameObject` as
`UIScrollWindow`, or `Awake` must fire after the component is added.

### UpdateScrollHeight

```
scrollHeight = scrollable.TotalHeight - scrollWindow.windowHeight
```

Called via reflection (`API.Reflection.Invoke(MiUpdateScrollHeight, ...)`).
Must be called after content changes before `SetScrollValue`, or the scroll
range is stale.

### SetScrollValue Semantics (inverted)

`SetScrollValue(float normalizedScrollValue)`:

- `1f` → **top** of list: lerps content `localY` toward `minScrollPos = 0`.
- `0f` → **bottom** of list: lerps content `localY` toward `ScrollHeight`
  (content shifted up by full scroll height).

This is counter-intuitive. Iter-3 passed `0f` and caused rows to overlap
the title element (content shifted ~20 units up). **Always use `1f` or
`ResetScroll()` to go to the top.**

`ResetScroll()` is a public method equivalent to `SetScrollValue(1f)` — use
it as the canonical "go to top" call.

### Post-Content-Spawn Sequence

After spawning or replacing scroll content, call in this order (ItemBrowser
`EntriesList.SetEntries` pattern):

```csharp
API.Reflection.SetValue(MiScrollable, scrollWindow, scrollableImpl);
API.Reflection.Invoke(MiUpdateScrollHeight, scrollWindow);
scrollWindow.SetScrollValue(1f);  // or scrollWindow.ResetScroll()
```

### LateUpdate Scroll Processing

`UIScrollWindow.LateUpdate` applies the current scroll delta to
`content.localPosition.y`. It reads mouse scroll wheel input only when the
cursor is inside the window bounds (checked via `bounds.Contains`).

### IScrollable Contract

```csharp
public interface IScrollable
{
    void UpdateContainingElements(float scroll);  // per-frame recycle callback
    bool IsBottomElementSelected { get; }
    bool IsTopElementSelected { get; }
    float GetCurrentWindowHeight();
    float TotalHeight { get; }        // negative: grows downward from 0
}
```

**Key implementation notes:**

- `UpdateContainingElements(float scroll)` is the per-frame callback the
  scroll system invokes with the current scroll offset. IB's `EntriesList`
  treats it as a no-op (content manages itself), but ItemChecklist's
  `ItemChecklistContent` uses it as the **viewport recycle driver** — see
  § Viewport Virtualization. Do not mistake it for arg-less.
- `TotalHeight` is negative and grows more negative as rows are added.
  Row placement formula: `row.localY = TotalHeight` before adding each
  row's height. TotalHeight starts at 0 and goes negative.
- `GetCurrentWindowHeight()` returns the window's clipping height in world
  units (used by `UpdateScrollHeight` formula).
- Hierarchy comparison:
  - **IB:** `ScrollWindow` → `ScrollContainer` → individual row GameObjects
  - **ItemChecklist:** `RowsContainer` → `Content` → individual row GameObjects
  Both hierarchies work; the key is that `Content` is the object whose
  `localY` is manipulated by the scroll system.

---

## Viewport Virtualization (Iter-3.8)

The catalog grows to ~10720 entries. The pre-Iter-3.8 design instantiated one
`ItemRow` GameObject per entry on every open (`SpawnRows`), which froze the
window ~905 ms. Iter-3.8 replaced that with a fixed-size pool of row
GameObjects recycled as the user scrolls, so the GameObject count is bounded
by the *visible* window, not the catalog size. Open latency dropped from
~905 ms to ~0–7 ms.

**Why hand-built — CK ships no recycler template.** `CookBookUI` (CK's own
cooked-food browser) is **not** a viewport recycler: it builds a *fixed* pool
of `MAX_ROWS × MAX_COLUMNS` slots (50×5 = 250) once and breaks at
`num >= itemSlots.Count`, so entries past slot 250 are simply never shown. It
scrolls by translating the whole pool under the clip mask, recycling nothing.
That is fine for ≤250 recipes but unusable for ~10720 entries. No CK class
recycles rows by index, so ItemChecklist implements its own on top of the
`IScrollable` contract.

### Pool model

`ItemChecklistContent` (the `IScrollable` implementor, which sits on
`scrollingContent`) owns a fixed pool of

```
N = ceil(windowHeight / RowHeight) + 2     // ~5–6 rows (RowHeight = 2.5)
```

row GameObjects. The pool only ever *grows* — `EnsurePool` grows toward
`ComputePoolSize()` and never early-returns short (an early-return bug would
leave the pool undersized after a window resize). Pool rows are children of
`scrollingContent` and are positioned at content-local `-(idx * RowHeight)`.

### Recycle driver — `UpdateContainingElements(float scroll)`

The scroll system invokes `IScrollable.UpdateContainingElements(scroll)` every
frame with the current scroll offset. `ItemChecklistContent` uses it as the
recycle driver:

```
firstIndex = floor(scroll / RowHeight)
if firstIndex == _lastFirstIndex: return    // per-frame no-op guard
_lastFirstIndex = firstIndex
// rebind each of the N pool rows to catalog entry (firstIndex + i)
```

The `_lastFirstIndex` guard skips the rebind when the first-visible index has
not changed since the last frame (most frames). A separate `RefreshVisible()`
forces an unconditional rebind (it resets `_lastFirstIndex = -1` first), used
on window-open, on a discovery event, and on re-bake — cases where the catalog
*contents* changed even though the scroll offset did not.

### Full-height reporting

Only ~N row GameObjects exist, but the scrollbar / scroll range must reflect
the whole catalog. `ItemChecklistContent.GetCurrentWindowHeight()` returns

```
count * RowHeight
```

i.e. the height the full catalog *would* occupy. `UIScrollWindow.UpdateScrollHeight`
(`scrollHeight = TotalHeight - windowHeight`) then computes a scroll range that
spans every entry, so scrolling reaches the last row even though no GameObject
exists for it until it scrolls into view.

### Open/close lifecycle

`ItemChecklistWindow` no longer spawns or destroys rows; it delegates to
`ItemChecklistContent.PopulateContent`:

1. `EnsurePool` — lazily grows the pool to `ComputePoolSize()` (grows-only).
2. `SetCount(catalogCount)` — records the full entry count for height reporting.
3. Reflection-wire the scrollable + invoke `UpdateScrollHeight` (the
   `UIScrollWindow` post-content-spawn sequence; see § Post-Content-Spawn
   Sequence). Order matters: `UpdateScrollHeight` **before** `ResetScroll`.
4. `ResetScroll()` — go to top.
5. `RefreshVisible()` — forced rebind so the pool shows the correct entries.

`HideUI` no longer destroys anything — it calls `root.SetActive(false)` only,
so the pool survives across opens. The `PugText.Clear()` pool-teardown (the
former per-destroy leak fix) moved to `ItemChecklistContent.OnDestroy`, which
runs only on full pool teardown. Because rows are no longer destroyed per
close, the old main-menu-PugText-blanking symptom can no longer occur.

> **Load-bearing invariant:** `UIScrollWindow.Awake` sets `enabled = false`
> *permanently* if its serialized `scrollable` reference does not resolve to a
> component on the same GameObject. The doc-comment recording this in the code
> is load-bearing — do not remove it.

### Prefab geometry invariant

For the first and last rows to sit flush against the window edges:

- `UIScrollWindow.windowHeight` **must equal** the SpriteMask height.
- The mask top **must align** to row 0's top.

To grow the mask **top-only** (keeping the bottom edge fixed), change both the
scale and the position in a 2:1 ratio:

```
mask.scale.y    += X
mask.localPos.y += X / 2
```

Changing only one of the two shifts the whole mask instead of extending it
upward. This was settled by empirical 4-build calibration, not by a static
reading of the prefab — the Iter-3.8 values were `windowHeight = 6.5`,
mask `scale.y = 6.5`, mask `localPos.y = -2.0`.

**Iter-9 enlargement.** The window was sized to near-fullscreen with a thin
**uniform border matching CK's inventory margin** (0.25 world-units). Final
values: background + root collider `29.5 × 16.375`, `windowHeight = 13`, mask
`scale.y = 13` / `localPos.y = -5.25`, `RowsContainer.y = 3.75`, row width
`27.5`. The window is centered in the camera's **30 × 16.875-unit viewport**
(measured once via a temporary log of `Manager.camera.uiCamera.orthographicSize`
= `8.4375`, aspect 16:9; `world_height = 2·orthoSize`, `world_width = height·aspect`).
**No runtime sizing logic:** CK's orthographic UI camera shows a constant world
area regardless of resolution, and CK has no UI-scale option, so a fixed prefab
size is "fullscreen with border" on every resolution (empirically confirmed).
`RowHeight` stays `2.5` (native), so the recycled pool auto-grew via
`ComputePoolSize` (`pool = 7` at `windowHeight 13`). The bottom shows a
deliberately clipped partial row as a "more below" affordance (whole-row flush is
a separate later Iter-9 slice).

### Scrollbar (Iter-5)

The window prefab now wires CK's native `ScrollBar` + `ScrollBarHandle` into
`UIScrollWindow.scrollBar`. This is a **pure prefab change — no mod C#**: once
`scrollBar` is set, `UIScrollWindow.LateUpdate` calls `UpdateScrollbar()` every
frame the scroll position or height changes, which calls
`ScrollBar.UpdateScrollBarPosition(normalizedPosition)` — driving handle
sizing, position, **and** mouse-wheel sync. (Verified against Item Browser,
whose working scrollbar has zero scrollbar C#.) Mouse-wheel scrolling already
worked before; the new wiring just makes the handle reflect it.

**Prefab subtree** (under the window `root` GO): `ScrollBar` GO (holds the
`ScrollBar` component; localPos `(4.9, -0.5, 0)`) → `ScrollBarRoot` child
(= `ScrollBar.root`, the GO CK toggles) → `ScrollBarTrack` (track
`SpriteRenderer` = `ScrollBar.background`, size `(0.25, 6.5)` = viewport
height) + `ScrollBarHandle` (`ScrollBarHandle` + 3D `BoxCollider` =
`ScrollBar.handle`) → `ScrollBarHandleSprite` (= `handleSpriteRenderer`) +
`ScrollBarHandleSelected` (= `optionalSelectedMarker`). Sprites are the
`ui_scrollbar_*` sub-sprites of the `ui_classic` atlas.

**Three load-bearing facts (proven during Iter-5 builds):**

1. **`maskInteraction: None` on every scrollbar `SpriteRenderer`.** They sit at
   sorting orders 46/47/48 inside the SpriteMask custom range (40..55), so
   without `maskInteraction = None` the row mask would clip them. `None`
   exempts them entirely.
2. **`handleCollider` is a 3D `BoxCollider`** (`!u!65`), not `BoxCollider2D` —
   CK's `UIMouse` does a 3D `Physics.Raycast`. Size `(0.5, 1.25, 4)`; the `y`
   is overwritten each frame by `ScrollBar.UpdateHandleSize`, the `z: 4` is
   raycast depth.
3. **`ButtonUIElement.LateUpdate` toggles GameObject *activity*** of
   `spritesShownUnpressed` (active when `!leftClickIsHeldDown`) and
   `spritesShownPressed` (active when held). The same GO must never be in both
   lists (the pressed loop runs last and wins, so it would only show while
   held → handle vanishes at idle). With a single handle sprite, both lists are
   left **empty**: `handleSpriteRenderer` is rendered independently by
   `ScrollBar` and stays always-visible, while the selected-border shows on
   hover/selection via `optionalSelectedMarker` (toggled by
   `OnSelected`/`OnDeselected`).

For hand-wiring the CK component `m_Script` refs (portable fileID, install-local
guid), see the `project-corekeeper-script-fileid-derivation` memory. Scroll
arrows stay unwired (`arrowUp`/`arrowDown` = `{fileID: 0}`); track-position
fine-tuning + real sprites are deferred to Iter-9.

---

## Data Architecture

### ItemCatalog Two-Loop Bake (Iter-3.7)

`ItemCatalog.Bake()` runs once per world-load, triggered from the
`PlayerController.OnOccupied` coroutine (after
`ClientWorldStateSystem.HasRunAtLeastOnce`).

**Pre-cache phase** (before the loops):

```
turnsIntoMap: ObjectID → ObjectID   (ingredientLookup[entity].turnsIntoFood)
tierMap:      ObjectID → (base, rare, epic)  (from CookedFoodCD fields)
```

**Loop 1 — Standard items:**

```
for objectId in PugDatabase.objectsByType.Keys:
    if IsCookedFood(objectId): skip   // handled by Loop 2
    emit CatalogEntry(objectId, variation=0)
```

**Loop 2 — Cooked-food α-enumeration:**

```
for i1 in ingredients:
    for i2 in ingredients where i2 >= i1:   // upper triangle, symmetric
        family = turnsIntoMap[GetPrimaryIngredient(i1, i2)]
        var = GetFoodVariation(i1, i2)
        for tier in [base, rare, epic]:
            emit CatalogEntry(tier_objectId, variation=var)
```

**Resulting catalog size:** ~10720 entries (~1240 standard + ~9480
cooked-food permutations: 3160 pairs × 3 tiers).

**Expected bake time:** < 200 ms on a typical machine (empirically ~384 ms
on this machine for the full ~10720-entry bake). Bake time is independent
of the Iter-3.8 open/render-time work: Iter-3.8 virtualized the row
*rendering* (the open-latency fix — see § Viewport Virtualization), not the
catalog bake. The bake still runs once per world-load in the
`PlayerController.OnOccupied` coroutine.

### DiscoveredState Packed-Long Key Schema

`DiscoveredState` stores discovered items as `HashSet<long>` where each
key is a packed long: `(objectId << 32) | (uint)variation`.

**Design rationale:**

- GC-free: boxing-free value type, no `(int, int)` tuple allocation per
  lookup.
- Negative-variation-safe: casting to `uint` before OR-ing prevents sign
  extension from corrupting the upper 32 bits.
- Trivially debuggable: `key >> 32` = objectId, `(int)(uint)key` = variation.

**API evolution:**

| Version | Type | Notes |
|---|---|---|
| Iter-3.6 | `HashSet<int>` | objectId only, no variation support |
| Iter-3.7+ | `HashSet<long>` | packed `(objectId, variation)` |

`PackKey(int objectId, int variation)` is the canonical factory. Never
construct the key inline — go through `PackKey` so the casting logic
stays in one place.

### Cooked-Food α-Enumeration

The α-algorithm derives entirely from `InventoryUtility.cs:~1626`
(`turnsIntoFood` lookup) and `CookedFoodCD` (variation + tiers).

**Pick-family derivation:**

For ingredient pair `(i1, i2)`, the result family is:
```
primary   = CookedFoodCD.GetPrimaryIngredient(i1, i2)
family    = ingredientLookup[primary.prefabEntity].turnsIntoFood
variation = CookedFoodCD.GetFoodVariation(i1, i2)
           = (primaryObjectId << 16) | secondaryObjectId
```

**Symmetry:** `GetFoodVariation(a, b) == GetFoodVariation(b, a)` because
`GetPrimaryIngredient` uses a deterministic tiebreaker (Seed 87931 RNG).
The upper-triangle iteration `(i2 >= i1)` safely deduplicates symmetric
pairs.

**Three-tier multiplication:**

Each `(family, variation)` tuple maps to three `CatalogEntry` rows:
base family ObjectID, `CookedFoodCD.rareVersion`, `CookedFoodCD.epicVersion`.

**Empirical validation:** runtime spike (May 2026) confirmed 3160 unique
pairs × 3 tiers = 9480 cooked-food entries, matching the expected catalog
size.

**`ObjectIDExtensions.IsCookedFood`** range: `objectId ∈ [9500, 9599]`
(max 100 family-item slots). Loop 1 uses this to skip standard-item
enumeration of cooked-food ObjectIDs — they are emitted by Loop 2 instead.

---

## Rarity Colouring (Iter-6)

Each row surfaces its CK rarity on two axes: a tinted item name (all
rarities) and a rarity border around the icon (Uncommon and above). The
rarity is a **distinct axis** from the Iter-3.7 cooked-food Base/Rare/Epic
tiers — each cooked-food tier carries its own `ObjectInfo.rarity`.

### Data flow

1. **Bake** — `ItemCatalog.Entry` carries a `Rarity Rarity` field, resolved
   from `PugDatabase.GetObjectInfo(objectId, variation).rarity` via a
   `rarityCache` that mirrors the existing `iconCache` (populated in both
   bake loops, read at the single `new Entry(...)` site). UI-independent: the
   bake never touches `Manager.ui`.
2. **Rebind** — `ItemChecklistContent.Rebind` resolves the colour per visible
   row: `Manager.ui.GetSlotBorderRarityColor(entry.Rarity,
   useDefaultColorForCommon: true, defaultColor: _defaultLabelColor)`. With
   `true`, Common/Poor return `_defaultLabelColor` (the label's prefab default,
   captured once from the first pool row) → no visible tint; Uncommon+ return
   `slotBorderRarityColors[(int)(rarity + 1)]`. The resolved `Color` and the
   `Rarity` enum are passed to `ItemRow.Bind`.
3. **Paint** — `ItemRow.Bind` sets `label.SetTempColor(colour,
   keepColorOnStart: true)` (after `Render`; see `gotchas.md § PugText tint`)
   and toggles `rarityBorder.enabled = rarity >= Rarity.Uncommon`, colouring it
   with the same `Color`. `ItemRow` stays decoupled from `Manager.ui` — it
   paints the colour it is handed.

`enum Rarity` (`Pug.Base.dll`): `Poor = -1, Common, Uncommon, Rare, Epic,
Legendary`; the colour-list index is `(int)(rarity + 1)`.

### Border SpriteRenderer

The `RarityBorder` child of the `ItemRow` prefab uses **`maskInteraction: 1`
(VisibleInsideMask)** — it scrolls and clips *with* the rows (the opposite of
the Iter-5 scrollbar's `None`). Sorting order 49 places the hollow frame above
the icon (order 48). It defaults to `m_Enabled: 0` (hidden until `Bind` proves
rarity ≥ Uncommon). The sprite is the placeholder `ui_rarity_border` (a white
1-px hollow frame, tinted at runtime), rendered as a proper **9-slice**
(`spriteBorder {1,1,1,1}` in its `.meta`, `m_DrawMode: 1`) so the 1-px ring
stays a thin fixed-pixel frame at any `m_Size` instead of thickening with the
sprite. Real pixel-art (a designed border in place of the tinted white ring)
remains Iter-9 polish.

---

## List View-Model & Sorting (Iter-7)

### ItemListViewModel

`ItemListViewModel` (revived from the orphaned `FilterAndSearchModel.cs`,
renamed) owns the **display-order indirection**: an `int[] Order` array where
`Order[displayIndex]` gives the catalog index for that display position. It
decouples row rendering from catalog order.

**`Recompute()`** rebuilds `Order` from scratch:

1. Collects the set of *visible* catalog indices. Iter-7: all indices. The
   filter/search seam — `DiscoveryFilter` and `SearchText` — is present but
   at no-op defaults; Iter-8 will activate them without changing the recompute
   contract.
2. Sorts by the active `SortMode` comparator (ascending):
   - **Name** — `DisplayName` (OrdinalIgnoreCase)
   - **Rarity** — `(int)Rarity` ascending (Poor → Legendary)
   - **Found** — discovered entries first (`db.CompareTo(da)` — true sorts before false)
   - **Category** — `ObjectType.ToString()` (Ordinal)
3. Tiebreak: `DisplayName` (OrdinalIgnoreCase) then catalog index — total
   order, stable under reversal.
4. **Descending** = reverse the sorted list.

**Static per-session state:** `s_mode` (`SortMode`) and `s_ascending` (`bool`)
are static fields. They survive window close/reopen and a catalog re-bake;
reset on game restart.

**Three recompute triggers:**

- Mode or direction change via the dropdown / toggle callbacks.
- `DiscoveredState.Changed` — **only** when `Mode == Found` (other modes are
  discovery-independent, so firing on every pickup would be wasteful).
- Re-bake: `ItemChecklistMod.ListView` is reassigned (static, `internal set`)
  after each bake, which constructs a fresh `ItemListViewModel` and calls
  `Recompute()`.

**`ItemChecklistContent` reads through `Order`:** `Rebind` at display index
`displayIdx` resolves `catalog.GetByIndex(model.Order[displayIdx])` instead
of `catalog.GetByIndex(displayIdx)`. `_count` comes from `model.Count`.

**`ItemCatalog.Entry.ObjectType`:** a new `ObjectType ObjectType` field,
resolved at bake via an `objectTypeCache` built in both bake loops (mirroring
`rarityCache`). Used by the Category comparator.

---

### Sort UI Components

All sort controls live in the header strip of the checklist window, authored
on the `…040`–`…060` fileID band in the prefab. Every clickable uses a **3D
`BoxCollider`** (`!u!65`) and every `SpriteRenderer` uses `m_MaskInteraction: 0`
(None) + the `"GUI"` sorting layer, matching the scrollbar rules.

#### DropdownWidget

`DropdownWidget : UIelement` is a reusable, self-contained dropdown. It is
**not** CK-native — it is mod-authored.

**API:**
```csharp
Configure(IReadOnlyList<string> labels, int selectedIndex, Action<int> onSelected)
```
Subsequent calls to `Configure` (e.g. after a sort mode change) re-initialise
the header and pool without reinstantiation.

**Header:** the selected option label shown at all times. A
`DropdownToggleButton` on the Display GO (plus a separate caret button) opens
and closes the popup on click.

**Popup:** lists only the *non-selected* options, flush under the header at
`-(pos + 1) * rowSpacing`. `EnsurePool` clones a `rowTemplate` to fill the
non-selected slots; `RebuildList` lays them out. Selecting an option (via
`DropdownOptionButton.OnLeftClicked → SelectOption`) updates the header,
closes the popup, fires `onSelected`, and re-`Configure`s to shift the new
selection. The popup closes via `TogglePopup` (header/caret click or
`SelectOption`).

**Click-outside-to-close (`LateUpdate`):** an `_armed` guard skips the
opening frame so the click that opens the popup does not immediately close it
(`LateUpdate` runs after CK's `UIMouse`; option clicks and the toggle already
call `SelectOption`/`TogglePopup` before this check runs, so they are not
double-fired). When `_open && !_armed && !ClickedInsidePopup()` → close.

**Iter-8 reuse:** `DropdownWidget` is the intended host for the
discovery-filter dropdown; `ui_icon_filter` / `ui_icon_clear_search` /
`ui_text_background` sprites already ship in the IB atlases.

#### DropdownToggleButton / DropdownOptionButton

Each lives in its **own `.cs` file** (`DropdownToggleButton.cs`,
`DropdownOptionButton.cs`). Both subclass `ButtonUIElement` and override
`OnLeftClicked`. See `gotchas.md § Multiple MonoBehaviours in one file` for
why splitting is mandatory.

#### AscDescToggle

`AscDescToggle : ButtonUIElement` — flips `ItemListViewModel.s_ascending`,
swaps the asc/desc glyph (`ui_icon_sort_order_asc` / `_desc` sub-sprites),
and triggers `Recompute()` + `RefreshVisible()`.

---

### ButtonUIElement Click Pattern

`ButtonUIElement` (in `Pug.Other.dll`) is CK's standard clickable base class.
**ItemChecklist convention — guard FIRST, then base** (verified consistent
across `DropdownOptionButton`, `DropdownToggleButton`, `AscDescToggle`, and the
Iter-8 `ClearSearchButton`):

```csharp
public override void OnLeftClicked(bool mod1, bool mod2)
{
    if (!canBeClicked) return;       // guard first: when not clickable, base is NOT run
    base.OnLeftClicked(mod1, mod2);  // (no selection effect / sound on a dead click)
    // … custom logic …
}
```

> Order matters and the repo is uniform on guard-first. (An earlier draft of
> this note showed base-first, copied from IB; the actual ItemChecklist code —
> and every button in it — guards first. Match the repo, not the old snippet.)

**Required prefab rules (same as ScrollBarHandle):**

- **3D `BoxCollider`** (`!u!65`): CK `UIMouse` raycasts in 3D. A
  `BoxCollider2D` (`!u!61`) is never hit.
- **`m_MaskInteraction: 0`** on every `SpriteRenderer`: keeps the button
  visible inside a SpriteMask range (orders within 40..55).
- **`"GUI"` sorting layer**: matches the window's sorting layer so the mask
  range applies.
- **Leave `spritesShownUnpressed` / `spritesShownPressed` empty**: if any GO
  is listed in both, `ButtonUIElement.LateUpdate` toggles it off at idle →
  the sprite disappears. Empty lists let the button's own `SpriteRenderer`
  stay always-visible (same rule as `handleSpriteRenderer` on `ScrollBarHandle`).

---

### Sprites — Sheet Atlases (Iter-7)

ItemBrowser's `ui_icon.png` and `ui_group.png` are **multiple-mode sheet
atlases** (`textureType: 8`, `spriteMode: 2`) carrying named sub-sprites:
`ui_icon_sort`, `ui_icon_sort_order_asc`, `ui_icon_sort_order_desc`,
`ui_icon_filter`, `ui_icon_clear_search`, `ui_group_expand`,
`ui_group_collapse`, and others.

Reference sub-sprites in prefab YAML by `{fileID: <internalID>, guid: <atlas
guid>, type: 3}`. Bundle inclusion is by **dependency-pull**: a bundled prefab
referencing the atlas GUID pulls the whole atlas in; set `assetBundleName`
to empty on the atlas asset itself (same as `ui_classic`). Do NOT copy
individual PNGs from the atlas — extracted singles lose their sheet-atlas meta
(see `gotchas.md § Bridge sprite trap`).

Button backgrounds use `ui_scrollbar_handle` (raised look; `~{1,1}` `m_Size`
for correct 9-slice reading). Slot/list backgrounds use `ui_slot_background`.

---

## Filter & Search (Iter-8)

Activates the `ItemListViewModel` filter/search seam left in place by Iter-7.
The data layer needed only a one-line change; Iter-8 is UI wiring + prefab
authoring.

### ViewModel change (Option A semantics)

`Recompute()`'s search block dropped its `if (!isDisc) continue;` guard, so the
name search now matches the **real** `DisplayName` of *every* entry (discovered
and undiscovered alike); undiscovered matches still render `???` in `ItemRow`.
This is the deliberate **Option A** choice — a checklist's "have I found X?"
question is answered unambiguously (the item appears; readable if found, `???`
if not), consistent with the mod already showing `???` rows openly. A runtime
toggle to switch Option A ↔ discovered-only is deferred to **Iter-8.1**.

`ItemListViewModel.IsFiltered` (`filter != All || searchText.Trim().Length > 0`)
drives the title's `· N shown` suffix — semantically correct, unlike
`Count != catalog.Count` which is only accidentally equivalent (a 100%-complete
`Discovered` filter has `Count == catalog.Count` yet is filtered).

### SearchBar : TextInputField

The search field is **CK-native**, not uGUI. `SearchBar` subclasses
`TextInputField` (`Pug.Other.dll`): PugText rendering, the blinking caret
(`CharacterMarkBlinker`), click-to-focus, and WASD-suppression are all
inherited. `OnLeftClicked` (base) calls `Manager.input.SetActiveInputField(this)`.
Our subclass only:
- polls `GetInputText()` in `LateUpdate`, pushing changes to `model.SearchText`
  (with a `_lastPushed` change-cache so unchanged frames don't re-`Recompute`);
- exposes `SyncFrom(text)` — sets the field text **and** `_lastPushed` so the
  window can sync the field to the model on open / after a re-bake without the
  next frame pushing the synced value back.

The orphaned `UnityInputFieldAdapter` (a uGUI `InputField` wrapper) was the
wrong abstraction and is **deleted**. IB's `SearchBar : TextInputField` is the
proven precedent; its controller-deselect / double-click-highlight / snap-point
extras are intentionally omitted (single-player M&K).

### Focus model — staying focused while typing

CK's UI selection is **hover-based**: `currentSelectedUIElement` follows the
mouse, and leaving an element's collider fires `OnDeselected`, which by default
calls `Deactivate`. So with the stock setting, typing stopped the moment the
mouse left the field. Fix: set **`dontDeactivateOnDeselect = true`** on the
field (stays active off-hover). The trade-off is it then never self-deactivates,
so `ItemChecklistWindow.HideUI()` calls `searchBar.Deactivate(false)` (guarded
by `inputIsActive`) — every close path funnels through `HideUI`, so closing the
window always frees gameplay input (WASD). Net behaviour: click → focus → type
freely → close (F1/Esc) frees the field.

### ClearSearchButton

`ClearSearchButton : ButtonUIElement` (guard-first click pattern). Its
`searchBar` ref is prefab-wired; on click it `ResetText()`s the field and clears
`model.SearchText`. Glyph `ui_icon_clear_search`.

### Filter dropdown — DropdownWidget reuse

A second `DropdownWidget` instance (`FilterDropdown`), authored by **duplicating
the `SortDropdown` subtree** via a deterministic fileID slot-remap (slots 50–60
→ 70–80; the prefab's hand-authored `<classId><zeros><slot>` scheme makes the
copy a pure `+20` on every in-subtree fileID). Glyph swapped to `ui_icon_filter`.
`WireControls()` configures it with `FilterLabels = {All, Discovered,
Undiscovered}` (index 1:1 with the `DiscoveryFilter` enum). No new widget code.

### Control re-wire on re-bake

`RebindRows()` now re-calls `WireControls()` after `PopulateContent()` so the
dropdown/search callbacks re-capture the fresh `ItemListViewModel` instance
after a re-bake (loc change / world reload while the window is open).
`WireControls` tracks the previously-wired model (`_wiredModel`) and
unsubscribes its `OnResultsChanged` before subscribing the new one, so the
discarded model isn't retained by a dangling delegate.

### Search-field prefab structure (the "Display" container)

Mirrors the dropdown's slot look:
```
SearchField   (SearchBar + 3D BoxCollider = click/focus target; no SpriteRenderer)
├─ Display    (SpriteRenderer: ui_classic 9-slice slot, Sprites-Default mat, GUI/order 52)
│  ├─ Text    (PugText, pugText)
│  └─ Hint    (PugText, hintText, "Search…")
├─ SelectedMarker
├─ Caret      (CharacterMarkBlinker + white_pixel SpriteRenderer)
└─ ClearButton (ClearSearchButton + ui_icon_clear_search + 3D BoxCollider)
```
The `Display` was produced by duplicating the dropdown's `Display` (to inherit
its correct sprite/material/sorting/9-slice) and **stripping** its
`DropdownToggleButton` + `BoxCollider` (else the leftover button hijacks clicks
and fires the *original* dropdown's popup — its `owner` still points there).
Text/Hint are children of `Display`; SearchBar's `pugText`/`hintText` refs are
fileID-based, so re-parenting doesn't break wiring.

Pixel-exact spacing, a real pixel-art caret, and overlap-free header geometry
are **Iter-9** polish; this iteration places controls in the approved L→R order
(Search · Sort+AscDesc · Filter) and confirms they're clickable.

---

## Shortcut-Panel & HUD Suppression (Iter-9)

While the checklist is open, three things that would otherwise show over it are
suppressed; all are scoped to `ItemChecklistWindow.Instance.Root.activeSelf` and
release automatically when the window closes/auto-hides (so a vanilla inventory
sees normal behaviour).

**Why they appear at all:** CoreLib forces `UIManager.isAnyInventoryShowing ==
true` for any mod UI (it patches the aggregate getter; per-UI `isShowing` getters
stay unpatched). That makes CK treat the checklist like an inventory — enabling
the keyboard-shortcuts panel's toggle key (S) and keeping the HUD's
inventory-context elements live.

| Target | Mechanism | File |
|---|---|---|
| `ShortCutsWindow` ("Tastenkürzel" panel) | `LateUpdate` **prefix** → `__instance.HideUI()` + `return false`. **Load-bearing:** runs every frame, so it beats the S-key toggle (which `ShortcutsCanBeToggled` does *not* gate). | `ShortCutsWindowSuppressPatch.cs` |
| `InventoryShortCutsButton` ("?" prompt) | `ShortcutsCanBeToggled` **postfix** → `__result = false`. Gates the prompt visuals (`UpdateVisuals`), so the prompt disappears. | `InventoryShortCutsButtonSuppressPatch.cs` |
| Top-left HUD (health/food/ability bars) | `ShowUI` → `Manager.ui.TemporarilyDisableGameplayUI()`; `HideUI` → `EnableTemporarilyDisabledGameplayUI()` (guarded for the Awake-time `HideUI`). | `ui/ItemChecklistWindow.cs` |
| Bottom-right button hints (Tab/E…) | `InGameButtonHintsUI.LateUpdate` **prefix** → forces the **public** `container` GameObject inactive + `return false` (the stock `LateUpdate` re-asserts `container.SetActive(showKeyHints)` every frame, so a one-shot hide is overwritten). | `InGameButtonHintsSuppressPatch.cs` |

**Decompile facts (verified against this install's `Pug.Other.dll`):**
`ShortCutsWindow.LateUpdate` is a `protected override` declared on the type (so
the patch binds); `HideUI()` is `public` (`root.SetActive(false)`).
`InventoryShortCutsButton.ShortcutsCanBeToggled()` is `public static bool` and
drives only the prompt visuals — **not** the S keybind (`ToggleInventoryShortcuts`
checks `isAnyInventoryShowing` directly), which is why the panel needs the
per-frame `LateUpdate` prefix. `TemporarilyDisableGameplayUI()` flips a private
**runtime** scale-multiplier field (CK's own RadicalMenu-open mechanism, ~51 HUD
elements self-scale to zero) — **not** `Manager.prefs.hideInGameUI`, which
`SetDirty()`s to disk. All four patch targets are sandbox-safe (Harmony
attributes run in trusted `0Harmony.dll`; the HUD calls are public `Manager.ui`
methods).


## Item-Row Layout (Iter-9)

- **RowHeight is a single source of truth, read from the prefab.**
  `ItemChecklistContent.Init` reads `RowHeight` from the row prefab's
  `background` SpriteRenderer (`size.y`, authoritative in Sliced draw mode);
  `ItemRow.RowHeight` is only a compile-time fallback. Change the row background
  `m_Size.y` in `ItemRow.prefab` alone and the pool size, row spacing and total
  scroll height all follow.
- **Flush is RowHeight-independent.** Rows are placed by
  `y = MaskTopLocalY - RowHeight*(displayIdx + 0.5)`: row 0's TOP is pinned to the
  fixed `MaskTopLocalY` (1.25, the content-local mask top) and each centre is
  offset by `RowHeight/2`, so the list start/end stay flush for any row height
  (windowHeight = maskHeight and content = count*RowHeight do the rest). Replaces
  the old `-(displayIdx*RowHeight)`, which only stayed flush at the original 2.5.
- **Checklist checkbox.** `ItemRow.Bind` enables the empty checkbox sprite
  (`checkmark`) on **every** row; discovered rows additionally enable a
  `checkFill` child (the `ui_icon_requirement` glyph, sorting order above the box)
  -- empty box = todo, box+check = done.
- **Side margins + scrollbar.** Window content is symmetric +/-14.2; the row
  background spans `[-14.2, 13.95]` so it ends at the scrollbar's left edge
  instead of running under it. Header field heights are 0.7 (contents centred);
  the search field's `selectedMarker` is a full-field controller-focus highlight
  rendered in front of the field background.
