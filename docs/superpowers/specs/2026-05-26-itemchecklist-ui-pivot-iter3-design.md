# ItemChecklist UI Pivot — Iteration 3 Design

**Date:** 2026-05-26
**Status:** Design approved. Pending writing-plans skill invocation.
**Branch:** `iter-3` (fresh from `main`, which is at Iter-2-partial commit `2b4b824`)
**Prerequisite reading:**
- `docs/superpowers/specs/2026-05-25-itemchecklist-ui-pivot-iter2-design.md`
  (Iter-2 spec with Partial Status Disclosure)
- `docs/research/spike-4-ui-architecture.md`

## Context

Iter-2 (commit `2b4b824` on main) delivered the display layer but left
three bugs disclosed in its spec:

1. Mouse-wheel scroll did not trigger despite `_scrollable` wired
2. PugText labels disappeared on 2nd+ window open + after returning to
   main menu
3. Rows extended past window boundary (follow-on from broken scroll)

Iter-3 is a minimal-patch iteration that adds two missing API calls
identified by Spike A + Spike B (cross-mod analysis of ItemBrowser).

## Goals

- Fix mouse-wheel scrolling within the rows region
- Fix PugText pool leak that breaks multi-open and main menu after first
  window open
- Window-boundary clipping is expected to be auto-fixed once scroll
  works (UIScrollWindow handles clipping internally)

## Non-Goals

- No filter UI (Iter-4)
- No search input field (Iter-4 — has its own sandbox investigation)
- No click-to-toggle-discovery on rows (Iter-4 or later)
- No EntriesListRenderer / pool-pattern refactor (YAGNI for current scope;
  could be Iter-4+ if filter or dynamic content makes it valuable)
- No architectural restructuring of the Window/ItemRow/Content layer

## Decisions Made During Brainstorming

| Question | Decision |
|---|---|
| Scope | **A — Only bug fixes**. Features deferred to Iter-4+ |
| Architecture | **A — Minimal-Fix**. Two API call additions, no refactor |
| Verification approach | **Spike-first then design**. User mandated full
ItemBrowser analysis before architecture choice — yielded the
specific API findings below |

## Spike Findings (the architectural basis)

Two spikes ran before architecture decision:

**Spike A — Scroll-Patterns Cross-Mod:**
- `UIScrollWindow` is used productively by ItemBrowser (heavy) and
  MapMarkersPlus (lighter — `MarkerList : UIelement, IScrollable`)
- Both use the same `[UIelement, IScrollable]` + `API.Reflection` pattern
- **The missing piece:** ItemBrowser's `EntriesList.SetEntries()` calls
  three APIs in order after RenderList:
  ```csharp
  API.Reflection.SetValue(MiScrollable, scrollWindow, this);
  API.Reflection.Invoke(MiUpdateScrollHeight, scrollWindow);
  scrollWindow.SetScrollValue(scrollProgress);   // ← THIRD CALL
  ```
  We do calls 1+2 in Iter-2 SpawnRows; we miss call 3. `SetScrollValue`
  is what activates the scroll system to respond to mouse wheel.

**Spike B — PugText Destroy-Patterns:**
- IB's `BasicEntriesListRenderer.ClearList` does:
  ```csharp
  foreach (var pugText in element.GetComponentsInChildren<PugText>(true)) {
      var wasActive = pugText.gameObject.activeSelf;
      pugText.Clear();                              // ← THE MISSING API
      pugText.gameObject.SetActive(wasActive);
  }
  ItemBrowserAPI.FreePooledElement(element);
  ```
- **The missing piece:** `pugText.Clear()` releases the PugText's
  internal pool resources back to the pool. Without it, pool leaks on
  every Destroy. This is the root cause of our multi-open-text and
  main-menu-text-disappear bugs.
- IB also uses its own pool (FreePooledElement) instead of Destroy, but
  the `pugText.Clear()` is the load-bearing fix; pool-vs-Destroy is
  orthogonal.

## Reference Coverage

Per `[[reference-analysis-mandatory-when-provided]]` memory: explicit
declaration of what the IB reference analysis covered vs what was
deliberately skipped, with reasoning.

### Analyzed in full (Was / Wie / Warum)

| File / API | What | How | Why |
|---|---|---|---|
| `EntriesList.SetEntries()` 3-call sequence | Wires IScrollable, refreshes scroll height, sets initial scroll position | `API.Reflection.SetValue(MiScrollable, scrollWindow, this)` → `API.Reflection.Invoke(MiUpdateScrollHeight, scrollWindow)` → `scrollWindow.SetScrollValue(scrollProgress)` | Without call 3, scrollWindow has the data but doesn't activate its input-listening for the mouse wheel. Confirmed by Iter-2 where calls 1+2 alone produced a non-responsive scroll |
| `BasicEntriesListRenderer.ClearList()` PugText cleanup | Releases pooled text resources before destroying GameObject | `foreach pugText in GetComponentsInChildren<PugText>(true): pugText.Clear()`, then destroy/release | PugText holds references into a shared static pool. Naive Destroy orphans the references; pool starvation manifests as silent text-disappearing in unrelated PugTexts. Confirmed by Iter-2 main-menu regression |
| `MarkerList.cs` (MapMarkersPlus) | Cross-mod confirmation of IScrollable+UIScrollWindow pattern | Same `[UIelement, IScrollable] + [DefaultExecutionOrder(-100)] + Awake-wire via API.Reflection` pattern as IB | Confirms the pattern is general CK-modder convention, not an IB-only quirk. Increases confidence that our adoption is correct |

### Deliberately skipped (with reasoning)

| Skipped | Reason |
|---|---|
| IB's full `EntriesListRenderer<TItem,TEntry>` pool architecture (`_inactivePooledElements` queue, `FreePooledElement`, generic `<TItem,TEntry>` type machinery) | YAGNI for Iter-3 scope. Our row count (~50–500) is static-per-open, not dynamically filtered/searched. The bug-fix scope is bug-fix only; pool architecture would be cargo-culting. Re-evaluate if Iter-4 filter+search creates dynamic row turnover within an open window |
| IB's `FilterAndSearchModel` event-driven re-render | Out of Iter-3 scope per scope-decision A. Belongs in Iter-4 with the filter UI |
| IB's `BasicEntriesListRenderer.SetActive(wasActive)` after-Clear() restore call | Defensive in IB because pooled elements may be inactive at clear time. Our rows are always active when in `_spawnedRows` (we destroy immediately after Clear), so the active-state restore is irrelevant |
| `UIScrollWindow` internals (`Pug.UnityExtensions.dll` decompile) | Spike A established that the public API (`SetScrollValue` + the 2 reflection calls) is sufficient via IB+MapMarkersPlus production use. If Iter-3 Phase 4 fails despite SetScrollValue, escalate to decompile in Iter-3.5 |

## Architecture

Single-file-change: `unity/ItemChecklist/ui/ItemChecklistWindow.cs`. No
new files, no prefab changes, no editor finishing work. Estimated diff:
~10 lines added.

## Components

### `ItemChecklistWindow.cs` — `ClearRows` modification

Before:
```csharp
private void ClearRows()
{
    foreach (var r in _spawnedRows)
        if (r != null) Object.Destroy(r.gameObject);
    _spawnedRows.Clear();
}
```

After:
```csharp
private void ClearRows()
{
    foreach (var r in _spawnedRows)
    {
        if (r == null) continue;
        // IB-pattern (BasicEntriesListRenderer.ClearList): release PugText
        // pool resources before destroying the GameObject. Without this,
        // PugText's internal pool leaks on every Destroy, which manifests
        // as text disappearing on 2nd+ open + main menu breaking after
        // first open.
        foreach (var pugText in r.GetComponentsInChildren<PugText>(true))
            pugText.Clear();
        Object.Destroy(r.gameObject);
    }
    _spawnedRows.Clear();
}
```

### `ItemChecklistWindow.cs` — `SpawnRows` addition

After the existing `API.Reflection.Invoke(MiUpdateScrollHeight, scrollWindow);`
call, add:

```csharp
        // IB-pattern (EntriesList.SetEntries): activates the scroll system
        // after content is in place. Without this, UIScrollWindow is wired
        // but doesn't respond to mouse wheel. 0f = top of list; flip to 1f
        // if scroll lands at bottom (direction not yet empirically verified).
        scrollWindow.SetScrollValue(0f);
```

(Placed inside the existing `if (scrollWindow != null && content != null)`
block, after the Invoke.)

## Data Flow

Unchanged from Iter-2 except:

**Open cycle:**
```
F1 → ShowUI → ApplyTheme → SpawnRows:
  ├── ClearRows (defensive; iterates empty _spawnedRows on first open)
  ├── loop: Instantiate(rowPrefab) per catalog entry
  ├── content.RowCount = _spawnedRows.Count
  ├── API.Reflection.SetValue(MiScrollable, scrollWindow, content)
  ├── API.Reflection.Invoke(MiUpdateScrollHeight, scrollWindow)
  └── scrollWindow.SetScrollValue(0f)   ← NEW IN ITER-3
```

**Close cycle:**
```
Escape → HideUI → ClearRows:
  └── foreach row:
       ├── foreach pugText in row.GetComponentsInChildren<PugText>(true):
       │   └── pugText.Clear()   ← NEW IN ITER-3
       └── Object.Destroy(row.gameObject)
```

All other paths (CoreLib registration, theme application, IScrollable
wiring via `[DefaultExecutionOrder(-100)]`) are unchanged from Iter-2.

## Error Handling

| # | Risk | Trigger | Symptom | Handling |
|---|---|---|---|---|
| 1 | `pugText.Clear()` sandbox-blocked | Mod load | `CompileFailed` | Unlikely — IB uses it in production. If blocked: pivot to Spawn-Once architecture (option B from brainstorming) — no Destroy means no Clear() needed |
| 2 | `scrollWindow.SetScrollValue(0f)` scrolls to bottom instead of top | First F1 open | List starts at last item instead of first | Trivial fix: `0f` → `1f`. Direction verified by first test |
| 3 | `GetComponentsInChildren<PugText>` returns empty for a row | Runtime | foreach is no-op; row destroyed without Clear() — minor leak per orphan row | Defensive — should not happen since all rows have PugText children. If observed: add Debug.LogWarning |
| 4 | Scroll still does not work after SetScrollValue | First F1 open + wheel | No wheel response | Means SetScrollValue is also not enough. Next step: decompile-spike on Pug.UnityExtensions.dll UIScrollWindow class. Iter-3.5 |
| 5 | Pool-leak persists despite Clear() | Multi-open | Texts disappear on 2nd+ open | Means Clear() alone is insufficient (IB might also need FreePooledElement). Pivot to Spawn-Once architecture |

## Testing

### Build + install pipeline (unchanged)

```bash
cd item-checklist/.worktrees/iter-3
source .envrc
../../../utils/build.sh
```

### Test phases — 4 phases for 3 bugs + 1 regression

**Phase 1 — Sandbox compile check:**
- ✅ `Successfully compiled ItemChecklist safetyCheck=True` in Player.log
- ✅ Zero `CompileFailed` for ItemChecklist or cascade-mods
- ✅ No `TitleMenuIncompatibleModWarning`

**Phase 2 — Iter-1+2 regression check:**
- ✅ F1 opens window centered with theme background + Title
- ✅ Rows render with icons + names (discovered) and `?`+`???` (undiscovered)
- ✅ Cursor visible, WASD blocked

**Phase 3 — Multi-open text-fix verification:**
- ✅ F1 → Escape → F1: all row labels still visible on second open
- ✅ Quit to main menu after first open: main menu texts (Play button etc.) still visible

**Phase 4 — Scroll-fix verification:**
- ✅ F1 → mouse wheel over window: content scrolls vertically
- ✅ Scroll bounds respected (no over-scroll)
- ✅ Rows that scroll past viewport are clipped (auto-fix from working scroll)

### Definition of Done

Phases 1-4 all pass. Then:
- Update memory `item_checklist_ui_pivot_state` to "Iter-3 done; Iter-4 (filter) pending"
- Update spike-4 status header
- Suggested commit, then FF-merge iter-3 → main

### Failure-mode stop condition

- Phase 1 fails after 3 attempts → stop, re-brainstorm
- Phase 3 fails (text still disappears) after 1 attempt → pivot to
  Spawn-Once architecture (option B from brainstorming)
- Phase 4 fails (scroll still dead) after 1 attempt → trigger
  decompile-spike on UIScrollWindow class

## Iteration 4+ scope (deferred)

- Wire FilterAndSearchModel.OnResultsChanged to re-render rows based on
  filtered VisibleIndices
- Filter dropdown UI (All / Discovered / Undiscovered)
- Search input field — investigate CK's TextInputInterface; resolve
  UnityInputFieldAdapter (currently dead-code in repo)
- Click-to-toggle-discovery on rows
- Optional: pool / renderer-pattern refactor if filter dynamics make it
  valuable

## References

- Iter-2 Spec: `docs/superpowers/specs/2026-05-25-itemchecklist-ui-pivot-iter2-design.md`
- Iter-2 Plan: `docs/superpowers/plans/2026-05-25-itemchecklist-ui-pivot-iter2.md`
- Iter-1 Spec: `docs/superpowers/specs/2026-05-25-itemchecklist-ui-pivot-iter1-design.md`
- Spike-4 (architecture research): `docs/research/spike-4-ui-architecture.md`
- IB reference (Spike A + B source):
  - `/tmp/ck-ui-research/ItemBrowser/ItemBrowserPackage/Scripts/Common/UserInterface/Browser/Details/Entries/EntriesList.cs`
    (SetEntries with the 3-call sequence, lines ~38-60)
  - `/tmp/ck-ui-research/ItemBrowser/ItemBrowserPackage/Scripts/Common/UserInterface/Browser/Details/Entries/BasicEntriesListRenderer.cs`
    (ClearList with pugText.Clear() + FreePooledElement, lines ~70-88)
- Cross-mod scroll reference:
  - `/tmp/ck-ui-research/MapMarkersPlus/Scripts/Common/UserInterface/MarkerList.cs`
    (non-IB UIelement+IScrollable+UIScrollWindow user)
- Memory: `[[item-checklist-ui-pivot-state]]`, `[[corekeeper-ui-pattern]]`,
  `[[reference-analysis-mandatory-when-provided]]` (lesson from this brainstorm),
  `[[deep-spike-unfamiliar-internals]]`
