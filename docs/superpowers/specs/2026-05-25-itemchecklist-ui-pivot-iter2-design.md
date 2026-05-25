# ItemChecklist UI Pivot — Iteration 2 Design

**Date:** 2026-05-25
**Status:** **Iter-2 PARTIAL — Display works on FIRST open only.** Scroll
and multi-open-stability deferred to Iter-3 (will adopt ItemBrowser's
EntriesListRenderer pattern + UIScrollWindow proper setup). See
"Partial Status Disclosure" section at end.
**Branch:** `initial-impl` (continue forward from HEAD `1e80b15` — Iter-1 commit)
**Prerequisite reading:**
- `docs/superpowers/specs/2026-05-25-itemchecklist-ui-pivot-iter1-design.md` (Iter-1 spec)
- `docs/research/spike-4-ui-architecture.md` (UI architecture research)

## Context

Iteration 1 delivered a minimal sanity-check window: themed background +
PugText title, no content. CoreLib + SpriteRenderer architecture is now
empirically validated (all 4 test phases passed; bonus:
`Manager.ui.GetCraftingUITheme(...)` proved working in sandbox via Stone-
switch + diagnostic log).

Iteration 2 introduces the **content display layer**: rebuild
`ItemRow.prefab` as SpriteRenderer + Layer 5, add a scrollable
`RowsContainer` to the window, spawn all catalog items as rows, render
discovered/undiscovered visual differentiation. Filter and search are
deferred to Iteration 3 (the search input field has known sandbox
risks worth isolating from this iteration).

The deletion of `VirtualScrollList.cs` + `UiController.cs` is part of
this iteration's scope — they're replaced by CK's native
`UIScrollWindow` component (used in production by ItemBrowser, verified
during brainstorming) and the IModUI Show/Hide pattern from Iter-1.

## Goals

- Render all catalog items as rows in a scrollable container within
  the existing Iter-1 window
- Visual differentiation between discovered (icon + name + checkmark)
  and undiscovered items (`?` placeholder + `???` label, no checkmark)
- Mouse-wheel scrolling within the rows region
- Empirically validate the no-virtualization-+-CK-UIScrollWindow approach
  (performance and content-height-handling)
- Delete obsolete code (`VirtualScrollList.cs`, `UiController.cs`,
  `ItemRowView.cs`) — confirms disconnect-not-delete strategy was a
  successful interim stage

## Non-Goals

- No filter dropdown (Iter-3)
- No search input field (Iter-3 — requires sandbox investigation of
  CK input field primitives)
- No click-to-toggle-discovery on rows (Iter-3 or later)
- No tooltips, no detail-view per item
- No persistent scroll position across open/close cycles (default: reset
  to top each open)
- No pool pattern for row instances (default: Destroy on hide, Spawn on
  show — pool can be added in Iter-3 if perf testing shows need)
- No removal of `UnityInputFieldAdapter.cs` (kept for Iter-3 search work
  context)

## Decisions Made During Brainstorming

| Question | Decision |
|---|---|
| Iter-2 scope | **A — Display-only first**: ItemRow + Scroll only. Filter+Search → Iter-3 |
| Scroll strategy | **B — CK `UIScrollWindow`** (native): empirically validated by ItemBrowser production use (`ItemBrowserButton`/`ItemBrowserSlot`/`EntriesList`/`OptionsView` all reference it) |
| Row pool vs Destroy | **Destroy on hide, Spawn on show**: simpler for Iter-2; pool optionally deferred to Iter-3 |
| Old `ui/` files | **DELETE** this time (not Disconnect like Iter-1): `VirtualScrollList.cs`, `UiController.cs`, `ItemRowView.cs` are explicitly replaced |
| Scroll position persistence | **No** — reset to top on each open |

## Architecture

This iteration touches **13 files** in the mod repo. No cross-repo changes
(unlike Iter-1's SDK manifest edit).

| # | File | Operation |
|---|---|---|
| 1 | `unity/ItemChecklist/Prefabs/ItemRow.prefab` | Replace uGUI version with SpriteRenderer + Layer 5 hierarchy; 4-slot structure preserved (Placeholder + Label + Icon + Checkbox), all renderers converted |
| 2 | `unity/ItemChecklist/Prefabs/ItemRow.prefab.meta` | Preserved (Unity holds GUID stable) |
| 3 | `unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab` | Add `RowsContainer` GameObject under `root` with UIScrollWindow component + `Content` grandchild as spawn-parent |
| 4 | `unity/ItemChecklist/ui/ItemRow.cs` | **New** (~80 LoC): `class ItemRow : UIelement`; `Bind(int objectId, Sprite icon, string name, bool isDiscovered)` method preserves old `ItemRowView` API |
| 5 | `unity/ItemChecklist/ui/ItemRow.cs.meta` | New with fixed UUID (same pattern as Iter-1 ItemChecklistWindow.cs.meta) |
| 6 | `unity/ItemChecklist/ui/ItemChecklistWindow.cs` | Extend with `rowsContent: Transform` + `rowPrefab: GameObject` serialized fields; `SpawnRows()` + `ClearRows()` private helpers; ShowUI calls SpawnRows (pulls catalog/state lazy from `ItemChecklistMod` statics); HideUI calls ClearRows |
| 7 | `unity/ItemChecklist/ItemChecklistMod.cs` | Minor: add `public static ItemCatalog Catalog` + `public static DiscoveredState State` accessors (mirrors existing `AssetBundle` pattern); assign in existing Init |
| 8 | `unity/ItemChecklist/ui/ItemRowView.cs` | **DELETE** (replaced by `ItemRow.cs`) |
| 9 | `unity/ItemChecklist/ui/ItemRowView.cs.meta` | DELETE |
| 10 | `unity/ItemChecklist/ui/VirtualScrollList.cs` | **DELETE** (replaced by UIScrollWindow + flat-list rendering) |
| 11 | `unity/ItemChecklist/ui/VirtualScrollList.cs.meta` | DELETE |
| 12 | `unity/ItemChecklist/ui/UiController.cs` | **DELETE** (functionality now in `ItemChecklistWindow` + `IModUI`) |
| 13 | `unity/ItemChecklist/ui/UiController.cs.meta` | DELETE |

**Not touched (mod logic and deferred-Iter-3 files):**
- `ui/FilterAndSearchModel.cs` — pure mod logic, Iter-3 wires it
- `ui/UnityInputFieldAdapter.cs` — kept for Iter-3 search work context
- `DiscoveredState.cs`, `ItemCatalog.cs`, `CharacterDataDiscoverySnapshot.cs`,
  Save-Hooks, Harmony-State-Machine — unchanged

## Components

### `unity/ItemChecklist/ui/ItemRow.cs` (new)

```csharp
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
```

### `unity/ItemChecklist/Prefabs/ItemRow.prefab` (replace)

YAML skeleton (full YAML produced at implementation time):

```
ItemRow (Layer 5, Root)
├── Components:
│   ├── Transform                    (localPosition runtime-set)
│   └── ItemRow (MonoBehaviour, 5 child fields wired)
│       — NO BoxCollider2D (display-only, no click target)
│
├── Background  → Transform + SpriteRenderer (DrawMode=Sliced, sortingOrder=15)
├── Icon        → Transform (-1.0, 0, 0) + SpriteRenderer (sortingOrder=20)
├── Label       → Transform (0.5, 0, 0) + PugText (fontFace=16777232, alignment=left, sortingOrder=25)
├── Placeholder → Transform (-1.0, 0, 0) + PugText ("?", fontFace=16777232, sortingOrder=20)
└── Checkmark   → Transform (2.0, 0, 0) + SpriteRenderer (sortingOrder=20)
```

Editor-finish tasks (handed off to user — these are deliberate hand-off
markers, not unresolved design questions):
- Background.sprite assignment (e.g. `ui_slot_background.png` or empty)
- Background.size (suggested: `(10, 2.5)`)
- Checkmark.sprite assignment (a checkmark from ui_classic or Bridge atlas)
- Label.PugText.style → fontFace 16777232, horizontalAlignment Left, maxWidth ~6
- Placeholder.PugText.style → fontFace 16777232, horizontalAlignment Center, light-grey color

### `unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab` (extend)

New subtree under `root`, between `Title` and (future Iter-3) FilterBar:

```
ItemChecklistWindow (existing, unchanged)
└── root (existing, unchanged)
    ├── Background (existing)
    ├── Title (existing)
    └── RowsContainer                [NEW]
        ├── Transform (0, -1, 0)              (below Title)
        └── UIScrollWindow                    (script-ref from CK SDK, GUID lookup at impl time)
            └── Content                       [NEW grandchild — spawn parent for ItemRows]
                └── Transform (0, 0, 0)
```

### `unity/ItemChecklist/ui/ItemChecklistWindow.cs` (extend)

Additive diff to Iter-1 version. Catalog and state are pulled lazily from
`ItemChecklistMod`'s static accessors at ShowUI time, so timing of mod-load
vs. world-load does not matter.

```csharp
// New serialized fields (Editor-wired):
public Transform rowsContent;
public GameObject rowPrefab;

// New runtime state:
private readonly System.Collections.Generic.List<ItemRow> _spawnedRows = new();

// Extend ShowUI:
public void ShowUI()
{
    root.SetActive(true);
    ApplyTheme();
    SpawnRows();
}

private void SpawnRows()
{
    ClearRows();

    var catalog = ItemChecklistMod.Catalog;
    var state = ItemChecklistMod.State;
    if (catalog == null || state == null || rowPrefab == null) return;

    float y = 0f;
    for (int i = 0; i < catalog.Count; i++)
    {
        var entry = catalog.GetAt(i);
        var go = Object.Instantiate(rowPrefab, rowsContent);
        go.transform.localPosition = new Vector3(0, y, 0);
        var row = go.GetComponent<ItemRow>();
        row.Bind(entry.ObjectId, entry.Icon, entry.Name, state.IsDiscovered(entry.ObjectId));
        _spawnedRows.Add(row);
        y -= ItemRow.RowHeight;
    }
}

// Extend HideUI:
public void HideUI()
{
    ClearRows();
    root.SetActive(false);
}

private void ClearRows()
{
    foreach (var r in _spawnedRows)
        if (r != null) Object.Destroy(r.gameObject);
    _spawnedRows.Clear();
}
```

### `unity/ItemChecklist/ItemChecklistMod.cs` (minor extend)

Add static accessors so Window can lazy-lookup at ShowUI time without
needing tight ModObjectLoaded timing:

```csharp
// New static accessors (mirror existing `public static AssetBundle AssetBundle` pattern):
public static ItemCatalog Catalog { get; private set; }
public static DiscoveredState State { get; private set; }

// In existing Init() or wherever catalog/state are constructed:
Catalog = _catalog;   // assign to static so Window can read it
State = _state;
```

No change needed to `ModObjectLoaded` for the data-wiring — the
`RegisterModUI(go)` call from Iter-1 is sufficient. Window pulls
catalog/state at ShowUI time.

If catalog/state are still null at first ShowUI (e.g. user presses F1
before any world is loaded), `SpawnRows` silently skips spawning —
window shows empty list. Once world is loaded and statics are
populated, the next F1 press renders rows correctly.

## Data Flow

### Lifecycle: Mod init → first window show

```
Game launch
  ↓
Pugstorm loader sandbox-compile (Iter-1 validated)
  ↓
IMod.EarlyInit():  (Iter-1: CoreLibMod.LoadSubmodule + AssetBundle + Discovery init)
IMod.Init():       (existing mod-logic: Construct ItemCatalog + DiscoveredState)
  ↓
AssetBundle iteration → ModObjectLoaded(GameObject):
  ├── ItemChecklistWindow.prefab → RegisterModUI (no data wiring needed — ShowUI pulls statics lazy)
  └── ItemRow.prefab → RegisterModUI no-op (no ModUIAuthoring)
  ↓
World load → UIManager.Init() → CoreLib UIManagerPatch.OnInit:
  ├── Instantiate window in chestInventoryUI.transform.parent
  └── Awake() → HideUI() → root.SetActive(false), no rows spawned yet
```

### Open cycle: F1 → SpawnRows → UIScrollWindow

```
Player presses F1
  ↓
IMod.Update() guard chain (existing, preserved from Iter-1)
  ↓
UserInterfaceModule.OpenModUI("ItemChecklist:Window")
  ↓
ItemChecklistWindow.ShowUI():
  ├── root.SetActive(true)
  ├── ApplyTheme()  (Iter-1: Wood theme background)
  └── SpawnRows():
      ├── ClearRows() (defensive for re-open without prior close)
      └── for i = 0..catalog.Count:
          ├── Instantiate(rowPrefab, rowsContent)
          ├── transform.localPosition = (0, -i * RowHeight, 0)
          └── row.Bind(objectId, icon, name, IsDiscovered)

CK vanilla reacts to currentInterface != null:
  ├── isAnyInventoryShowing → true (CoreLib postfix)
  ├── Cursor visible, WASD blocked
  └── UIScrollWindow wheel-handler is active for cursor-over-window

User scrolls mouse-wheel over window:
  └── UIScrollWindow.UpdateScroll → Content y-offset within scrollable bounds
      → Unity 2D camera frustum culls out-of-view SpriteRenderers automatically
```

### Close cycle: Escape → ClearRows

```
Player presses Escape
  ↓
Vanilla UIManager.HideAllInventoryAndCraftingUI()
  ↓
CoreLib UIManagerPatch.OnHide → iterate registered ModUIs:
  └── ItemChecklistWindow.HideUI():
      ├── ClearRows():
      │   ├── foreach row in _spawnedRows: Destroy(row.gameObject)
      │   └── _spawnedRows.Clear()
      └── root.SetActive(false)

→ Next F1 press: full SpawnRows cycle again (scroll position resets to top)
```

### What we do not patch (inherited from Iter-1)

| Behavior | Source |
|---|---|
| Cursor visible on open | CoreLib `isAnyInventoryShowing` postfix |
| WASD blocked | CoreLib postfix |
| Escape closes window | CoreLib `HideAllInventoryAndCraftingUI` postfix → our HideUI |
| Mouse-wheel scroll within RowsContainer | UIScrollWindow vanilla component |
| Vanilla theme background | `Manager.ui.GetCraftingUITheme(...)` (Iter-1 validated) |

## Error Handling

| # | Risk | Trigger | Symptom | Handling |
|---|---|---|---|---|
| 1 | ItemRow.prefab PugText/UIelement GUIDs wrong | Editor load | "Missing (Mono Script)" | Reuse Iter-1 GUIDs: PugText `3519ac58e5ff54941a4a69512016923c`, ItemChecklistWindow-style fixed UUID for ItemRow.cs.meta |
| 2 | ItemRow PugText fontFace = Editor-default (67108896) | Runtime | Label/Placeholder invisible | Set `16777232` directly in YAML, plus `renderOnStart: 1` + `keepEnabledOnStart: 1` (Iter-1 lesson) |
| 3 | UIScrollWindow does not detect content-size change | Runtime, after SpawnRows | Only first ~10 items visible, rest unscrollable | Default assumption: UIScrollWindow measures content bounds in Update loop. If wrong: Harmony-postfix on `UIScrollWindow.UpdateScroll` to inject our content height. **No private-field reflection** (sandbox risk per Iter-1) |
| 4 | F1 pressed before world is loaded | Pre-world session | Window opens with empty list | Expected behavior (not a bug). Lazy lookup of `ItemChecklistMod.Catalog`/`State` guarded: SpawnRows silently skips when null. Once world loads + statics populate, next F1 renders correctly |
| 5 | 500+ SpriteRenderer lag on Wine/CrossOver | First ShowUI | Stutter/freeze on F1-open | Phase-3 test measures. If > 500ms: implement pool pattern (~15 LoC). If > 2s: stop Iter-2, brainstorm recycler variant |
| 6 | Memory leak from Destroy + Re-Spawn | Long session | Unity heap grows over many F1 cycles | Unity GC reclaims destroyed GameObjects. Verify via `mono_crash.mem*` analysis if symptom appears in tests. If real: pool pattern |
| 7 | Sandbox block on new API (e.g. ItemCatalog method) | Mod load | CompileFailed + ModManager hang | SIGKILL ready. Catalog/State are our own code, no sandbox block expected unless they internally use banned APIs (Manager.saves etc.). Pre-verify with grep |
| 8 | UIScrollWindow class not in Pug DLL exports | Compile-time | `CS0246: UIScrollWindow not found` | Confirmed exists per spike-4 (ItemBrowser uses it). First Iter-2 build must compile without wide-mass-cleanup |
| 9 | Rows positioned wrong (overflow, off-screen) | Runtime | First row invisible or duplicated | LocalPosition (0, -i * RowHeight, 0) is y-down. Verify content origin in Editor — if pivot top-left vs center, math differs. Phase-3 visual covers this |
| 10 | UIScrollWindow Inspector fields missing (Editor-finish forgotten) | Editor save | UIScrollWindow renders but doesn't scroll | Explicit hand-off checklist for hybrid workflow: Inspector fields, content bounds, scrollbar sprite. User inspection before build |

### Prerequisites (verify before YAML writing)

1. UIScrollWindow script GUID lookup — search
   `CoreKeeperModSDK/Library/PackageCache` for Pug.Other.dll or
   Pug.UnityExtensions.dll meta-files + extract GUID. Needed for prefab
   YAML.
2. PugText GUID confirmation — `3519ac58e5ff54941a4a69512016923c` should
   still be the local value.
3. `ItemCatalog` public API check — verify `Count`, `GetAt(i)`,
   `GetAt(i).ObjectId/Icon/Name` exist as we use them.
4. `DiscoveredState` public API check — verify `IsDiscovered(int objectId)`
   exists.
5. `Manager.saves.*`-grep in our own mod code — sandbox-banned pattern
   check (Iter-1 memory).

### Failure-mode stop condition

An "attempt" = one round-trip code change → rebuild → install → CK
launch → Player.log diagnose.

If Phase 1 (sandbox compile) does not pass after **3 attempts**: stop
and re-brainstorm. If Phase 3 (UIScrollWindow visual) does not pass
after 3 attempts despite fixes: pivot to a DIY-scroll variant (option A
from brainstorming) — backup-fähig without complete rebuild because
ItemRow + Window extensions stay; only Scroll-strategy changes.

## Testing

### Build + install pipeline (unchanged from Iter-1)

```bash
cd item-checklist
source .envrc
../../../utils/build.sh
# Launch CK via CrossOver — DO NOT open in-game Mods menu (fake-ID wipe risk)
```

### Test phases with hard acceptance criteria

#### Phase 1 — Sandbox compile check (title screen)

Acceptance:
- ✅ Title screen reached without `TitleMenuIncompatibleModWarning`
- ✅ Player.log: `[Item Checklist] EarlyInit`
- ✅ Player.log: `Successfully compiled ItemChecklist safetyCheck=True`
- ✅ Zero `CompileFailed` lines (for ItemChecklist and cascade-mods)
- ✅ Zero `Exception` mentioning ItemChecklist in stack

Fail handling: SIGKILL CK, analyze log. Budget: 3 attempts.

#### Phase 2 — Mod registration + WireData

Acceptance:
- ✅ Player.log: `Registering ItemChecklist:Window Modded UI!`
- ✅ Player.log: no error/warning from our code at mod-load
- ✅ `ItemChecklistMod.Catalog` and `ItemChecklistMod.State` are non-null after world load (verifiable via debug log if needed)
- ✅ WASD moves character normally (negative test, window not yet open)

#### Phase 3 — Open + Visual + Scroll (the architecture validation)

Press F1 in a loaded world.

Acceptance:
- ✅ Window appears centered with wood-frame + title (Iter-1 baseline)
- ✅ Rows visible under the title — at least the first ~5-10 items
- ✅ Discovered items show icon + label (item name)
- ✅ Undiscovered items show `?` placeholder + label `???`
- ✅ Checkmark visible on discovered, hidden on undiscovered
- ✅ Mouse-wheel scrolls content vertically, items leave the window edge
- ✅ Scroll bounds: no over-scroll
- ✅ Cursor + WASD-block + mouse-click-silence (Iter-1 baseline)

Fail handling: risk table above (risks 3-9 are Phase-3 candidates).

#### Phase 4 — Close + Reopen + Row cleanup

Press Escape → F1 → Escape.

Acceptance:
- ✅ Escape closes window, cursor disappears, WASD re-enabled
- ✅ Re-F1 reopens window with same items (same order), no duplicate rows
- ✅ Scroll position back to top
- ✅ Player.log: no NullReferenceException or "destroyed but referenced"

#### Phase 5 — Performance + stability (optional but recommended)

F1-spam test: 10× rapid open/close.

Acceptance:
- ✅ No frame drops < 30fps during spam cycles
- ✅ Player.log: no progressive memory warning or GC spam
- ✅ Activity Monitor: memory stabilizes (no linear growth over 10 cycles)

If fail: insert pool pattern as Iter-2 polish or defer to Iter-3.

### Definition of Done

Phase 1-4 all pass + Phase 5 OK or explicit "Iter-3 polish" deferral.
Then:
- Update memory `item_checklist_ui_pivot_state` to "Iter-2 done; Iter-3 pending"
- Update spike-4 doc status
- Iter-2 suggested commit (atomic or phase-boundary)

## Iteration 3 scope (deferred)

Documented here so future-me knows what's pending:

- Wire `FilterAndSearchModel` into `ItemChecklistWindow` (subscribe
  `OnResultsChanged`, re-render rows based on `VisibleIndices`)
- Filter dropdown UI (All / Discovered / Undiscovered selector)
- Search input field — investigate CK input field primitives (likely
  `TextInputInterface`-compatible); resolve whether
  `UnityInputFieldAdapter.cs` should be repaired or replaced
- Delete `UnityInputFieldAdapter.cs` (after search is wired through
  proper CK pattern)
- Optional: row pool pattern if Iter-2 perf testing showed leak/lag
- Optional: persistent scroll position across open/close cycles
- Optional: click-to-toggle-discovery on rows

## Partial Status Disclosure (added post-implementation)

Iter-2 was implemented and partially validated. Honest assessment:

**Works:**
- Architecture: CoreLib + SpriteRenderer + UIelement + IModUI all functional
- Rows render on FIRST open with proper discovered/undiscovered visual
  differentiation
- Title + theme background preserved from Iter-1
- Cursor + WASD-block + Window-mounting all work
- Code cleanup: 4 obsolete uGUI files deleted, ~+170 LoC new in Window
  + ItemRow + ItemChecklistContent

**Does NOT work (deferred to Iter-3):**
- **Mouse-wheel scroll:** UIScrollWindow setup is incomplete. We wired
  `_scrollable` via DefaultExecutionOrder + API.Reflection (eliminates
  the "disabling UIScrollWindow" log), but scroll still doesn't trigger.
  Spike showed ItemBrowser uses an `EntriesListRenderer` pattern + more
  complex Inspector setup we didn't replicate.
- **Multi-open text-disappear:** PugText components rendered correctly
  on first open, but on SECOND open all row labels are blank (icons +
  checkmarks still render). Same bug affects main menu after first
  window open. Root cause: `usePooledResources: 1` (CK default) +
  naive `Object.Destroy(row.gameObject)` doesn't properly return pooled
  resources to PugText's internal pool. ItemBrowser uses Pool=1 too,
  but with a Renderer pattern that handles cleanup correctly.
- **Out-of-window clipping:** without working scroll, rows extend past
  the window viewport (cosmetic but visible).

**Iter-3 plan** (will be brainstormed separately): adopt ItemBrowser's
`EntriesListRenderer` pattern. Rows live in a recycler pool — instead of
Destroy+Re-Instantiate, hide+rebind. Renderer manages pool lifecycle +
calls UpdateScrollHeight after content changes. Should fix both scroll
and multi-open-text issues with the same architectural pivot.

**Empirical validations from Iter-2 to carry into Iter-3:**
- `PugMod.MemberInfo` (not `System.Reflection.MemberInfo`) for
  Reflection-based field access — `using System.Reflection;` causes
  CS0104 ambiguity
- `API.Reflection.SetValue(MiScrollable, sw, this)` is the
  sandbox-safe wrapper
- `[DefaultExecutionOrder(-100)]` ensures Awake fires before
  UIScrollWindow's own Awake (avoids the "disabling" warning)
- `usePooledResources: 0` makes PugText render NOTHING (probably needs
  Pool=1 for any rendering at all in dynamic-spawn context)

## References

- Iter-1 spec: `docs/superpowers/specs/2026-05-25-itemchecklist-ui-pivot-iter1-design.md`
- Iter-1 plan: `docs/superpowers/plans/2026-05-25-itemchecklist-ui-pivot-iter1.md`
- Architecture research: `docs/research/spike-4-ui-architecture.md`
- Reference codebase: `/tmp/ck-ui-research/limoka-CoreKeeperMods/SDK Mods/Assets/Mods/DummyMod/`
- UIScrollWindow production reference:
  `/tmp/ck-ui-research/ItemBrowser/ItemBrowserPackage/Scripts/Common/UserInterface/Browser/ItemBrowserButton.cs`
  + `ItemBrowserSlot.cs` + `Details/Entries/EntriesList.cs` + `Main/Options/OptionsView.cs`
- Memory: `[[item-checklist-ui-pivot-state]]`, `[[corekeeper-ui-pattern]]`
  (9 Iter-1-empirical gotchas), `[[project_pugstorm_sandbox_rules]]`,
  `[[project_corekeeper_compile_fail_cascade]]`
