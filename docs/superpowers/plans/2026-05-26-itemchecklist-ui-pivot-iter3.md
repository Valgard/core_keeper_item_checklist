# ItemChecklist UI Pivot — Iter-3 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add two missing API calls to `ItemChecklistWindow.cs` so that mouse-wheel scrolling becomes responsive and PugText pool resources are correctly released, fixing the three bugs left open by Iter-2.

**Architecture:** Single-file additive diff (~10 LoC) to `unity/ItemChecklist/ui/ItemChecklistWindow.cs`. `ClearRows` gets an inner loop that calls `pugText.Clear()` for each PugText in a row before `Object.Destroy`. `SpawnRows` gets one extra call `scrollWindow.SetScrollValue(0f)` after the existing `UpdateScrollHeight` invoke. No new files, no prefab changes, no Unity Editor finishing work.

**Tech Stack:** C# (Unity 6000.0.59f2), PugMod RoslynCSharp sandbox, Harmony 2.x (unchanged from Iter-2), CoreLib UserInterfaceModule (unchanged), `../../../utils/build.sh` Unity batchmode pipeline, CrossOver-hosted Core Keeper `1.2.1.3` (`pkill -KILL -f "Core Keeper"` recovery).

**Reference spec:** `docs/superpowers/specs/2026-05-26-itemchecklist-ui-pivot-iter3-design.md`

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `unity/ItemChecklist/ui/ItemChecklistWindow.cs` | Modify | Two additive edits: `ClearRows` PugText cleanup loop, `SpawnRows` SetScrollValue activation |
| `docs/research/spike-4-ui-architecture.md` | Modify | Update Iter-2-PARTIAL status header to Iter-3-DONE |
| `~/.claude/projects/-Users-valgard-Projects-private-core-keeper/memory/project_item_checklist_ui_pivot_state.md` | Modify | Bump status to "Iter-3 done; Iter-4 (filter+search) pending" |

No new files. No prefab changes. No `.meta` edits. No `.asmdef` edits.

---

## Phase 1 — Code change

### Task 1: Apply both edits to `ItemChecklistWindow.cs`

**Files:**
- Modify: `unity/ItemChecklist/ui/ItemChecklistWindow.cs` (two locations: end of `SpawnRows` `if (content != null)` block at line 100, and `ClearRows` body at lines 105–108)

The two edits are tightly coupled (single file, both committed together) and small enough to be one task. There is **no automated test layer** for Unity mod code — verification happens through the Core Keeper runtime in Phase 2. So this task is "make the edits, verify they compile in the build step, defer behavior verification to Phase 2".

- [ ] **Step 1: Read the file to confirm current state**

Run: `cat unity/ItemChecklist/ui/ItemChecklistWindow.cs | sed -n '99,109p'`

Expected output (the two regions about to be modified):

```csharp
                    API.Reflection.Invoke(MiUpdateScrollHeight, scrollWindow);
                }
            }
        }

        private void ClearRows()
        {
            foreach (var r in _spawnedRows)
                if (r != null) Object.Destroy(r.gameObject);
            _spawnedRows.Clear();
        }
```

If the output does not match, STOP — the file has drifted from the plan's assumptions; re-read the full file and reconcile with the spec before continuing.

- [ ] **Step 2: Add `SetScrollValue(0f)` after the `UpdateScrollHeight` invoke**

Locate this exact block in `unity/ItemChecklist/ui/ItemChecklistWindow.cs` (lines 95–101 of current file):

```csharp
                if (content != null)
                {
                    content.RowCount = _spawnedRows.Count;
                    API.Reflection.SetValue(MiScrollable, scrollWindow, content);
                    API.Reflection.Invoke(MiUpdateScrollHeight, scrollWindow);
                }
```

Replace with:

```csharp
                if (content != null)
                {
                    content.RowCount = _spawnedRows.Count;
                    API.Reflection.SetValue(MiScrollable, scrollWindow, content);
                    API.Reflection.Invoke(MiUpdateScrollHeight, scrollWindow);
                    // IB-pattern (EntriesList.SetEntries): activates the scroll system after
                    // content is in place. Without this, UIScrollWindow is wired but does not
                    // respond to mouse wheel. 0f = top of list; flip to 1f if scroll lands
                    // at bottom (direction not yet empirically verified).
                    scrollWindow.SetScrollValue(0f);
                }
```

- [ ] **Step 3: Rewrite `ClearRows` to release PugText pool resources**

Locate this exact block in `unity/ItemChecklist/ui/ItemChecklistWindow.cs` (lines 104–109 of current file):

```csharp
        private void ClearRows()
        {
            foreach (var r in _spawnedRows)
                if (r != null) Object.Destroy(r.gameObject);
            _spawnedRows.Clear();
        }
```

Replace with:

```csharp
        private void ClearRows()
        {
            foreach (var r in _spawnedRows)
            {
                if (r == null) continue;
                // IB-pattern (BasicEntriesListRenderer.ClearList): release PugText pool
                // resources before destroying the GameObject. Without this, PugText's
                // internal shared pool leaks on every Destroy, which manifests as text
                // disappearing on 2nd+ open and main menu PugTexts going blank after
                // first window open.
                foreach (var pugText in r.GetComponentsInChildren<PugText>(true))
                    pugText.Clear();
                Object.Destroy(r.gameObject);
            }
            _spawnedRows.Clear();
        }
```

- [ ] **Step 4: Verify no `using System.Reflection;` was accidentally added**

Run: `grep -n "using System.Reflection" unity/ItemChecklist/ui/ItemChecklistWindow.cs`

Expected: no output (exit code 1).

If it returns a match: remove it. The file uses `PugMod`'s own `MemberInfo` type (already imported via `using PugMod;`). Adding `using System.Reflection;` causes `CS0104: 'MemberInfo' is an ambiguous reference between 'PugMod.MemberInfo' and 'System.Reflection.MemberInfo'`.

- [ ] **Step 5: Verify the diff is exactly the expected two regions**

Run: `git diff unity/ItemChecklist/ui/ItemChecklistWindow.cs`

Expected: two hunks. First hunk inserts the 5-line `// IB-pattern (EntriesList…` comment + `scrollWindow.SetScrollValue(0f);` line. Second hunk rewrites `ClearRows` from 5 lines to 14 lines (adds `{...}` braces, `continue;` guard, the 7-line nested `foreach` block with comment). **No other files changed.** No `using` imports added or removed.

If the diff contains additional changes — STOP and revert (`git checkout -- unity/ItemChecklist/ui/ItemChecklistWindow.cs`) before continuing.

- [ ] **Step 6: Do NOT commit yet**

Per project convention: NEVER commit without explicit user approval. The commit lives at the end of Phase 3 once all tests have passed, with a single grouped suggestion to the user.

---

## Phase 2 — Build + four-phase runtime verification

### Task 2: Build the mod and install to the CrossOver fake-ID slot

**Files:**
- Modify (via build pipeline): `<MOD_INSTALL_PATH>/<FAKE_MOD_ID>_1/` files

- [ ] **Step 1: Ensure Core Keeper is not running and Unity Editor is not running**

Run: `pgrep -f "Core Keeper" || echo "ck-not-running"` and `pgrep -f "Unity.app" || echo "unity-not-running"`

Expected: both `ck-not-running` and `unity-not-running` echoed.

If Core Keeper is running: `pkill -KILL -f "Core Keeper"` (the loader's `ModManager` blocks `Exit`, so plain quit hangs).
If Unity Editor is running: ask the user to close it — Editor holds an exclusive project lock and batchmode builds will fail.

- [ ] **Step 2: Source `.envrc` and run the build**

Run from worktree root (`/Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3`):

```bash
source .envrc && ../../../utils/build.sh
```

Note: the worktree is 3 levels deep below `core_keeper/`, hence `../../../utils/build.sh` (Iter-1 had `../utils/build.sh` which was wrong for worktrees — corrected in Iter-2; same correction applies here).

Expected stdout near the end:
```
[build] Unity batchmode build complete
[install-macos] Mod installed to <fake-id> slot
```

Expected exit code: 0.

- [ ] **Step 3: Verify the compiled DLL was installed**

Run: `ls -la "$MOD_INSTALL_PATH/${FAKE_MOD_ID}_1/" | grep -E "(ItemChecklist|ModManifest)"`

Expected: at least `ItemChecklist.dll` (or the per-mod naming) and `ModManifest.json` present, with mtime within the last 60 seconds.

- [ ] **Step 4: Inspect the mod's source files that the loader will compile from**

Run: `ls "$MOD_INSTALL_PATH/${FAKE_MOD_ID}_1/Scripts/" 2>/dev/null | head -20`

Expected: the `.cs` files copied alongside the prebuilt DLL (the loader compiles `Scripts/*.cs` at runtime via the Roslyn sandbox — this is the compilation that triggers `CompileFailed` if the sandbox rejects an API).

If the directory is absent or empty: the build step skipped script copy. Re-check `utils/build.sh` output for warnings; do not proceed to Task 3 until the Scripts/ directory contains the modified `ItemChecklistWindow.cs`.

### Task 3: Test Phase 1 — Sandbox compile check

**Files:**
- Read: `$MOD_INSTALL_PATH/../Player.log` (the CrossOver bottle's CK log)

- [ ] **Step 1: Locate Player.log + Player-prev.log, then clear both**

`Player.log` lives at `…/LocalLow/Pugstorm/Core Keeper/Player.log` within the CrossOver bottle — a different Wine-prefix subtree from `MOD_INSTALL_PATH` (which is under `Roaming/…/mod.io/`), so do NOT derive it relatively from `MOD_INSTALL_PATH`.

If `.envrc` defines a `PLAYER_LOG` variable, use that. Otherwise ask the user:

> "Bitte gib den absoluten Pfad zu Player.log aus dem CrossOver-Bottle — typischerweise `…/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Player.log`."

Once the path is known, truncate both:

```bash
PLAYER_LOG="<path-from-user-or-envrc>"
PLAYER_PREV_LOG="$(dirname "$PLAYER_LOG")/Player-prev.log"
> "$PLAYER_LOG"
> "$PLAYER_PREV_LOG"
```

Verify both are zero-byte: `ls -la "$PLAYER_LOG" "$PLAYER_PREV_LOG"` — both should show `0 bytes`.

- [ ] **Step 2: Launch Core Keeper**

Ask the user: "Bitte starte Core Keeper, warte bis das Hauptmenü voll geladen ist, dann melden." Pause for user confirmation.

- [ ] **Step 3: Grep the log for compile status**

Run: `grep -E "(Successfully compiled ItemChecklist|CompileFailed|safetyCheck)" "$PLAYER_LOG"`

Expected: one line matching `Successfully compiled ItemChecklist safetyCheck=True`.

Counter-indicators (any of these = STOP):
- `CompileFailed` mentioning `ItemChecklist`
- `Successfully compiled ItemChecklist safetyCheck=False` (sandbox bypass — not what we want; means `skipSafetyChecks: true` slipped into `ModManifest.json`)
- `CompileFailed` mentioning any other mod (Iter-2 memory: `project_corekeeper_compile_fail_cascade` — one mod failing can cascade-break unrelated mods via the shared script domain)
- `TitleMenuIncompatibleModWarning` shown in CK UI

- [ ] **Step 4: Decide outcome**

If Phase 1 passed: proceed to Task 4.

If Phase 1 failed for the **first** or **second** time: read the `CompileFailed` line for the rejected API call. Most likely cause: `pugText.Clear()` is sandbox-blocked despite IB using it in production. Diagnostic: search the same Player.log for any other mod that calls `PugText.Clear` and verify it loaded. If `Clear()` is truly blocked, pivot to spec Risk #1 fallback (Spawn-Once architecture — drop the inner foreach entirely and never destroy/respawn rows; track this as Iter-3.5 brainstorm material, not as a silent retry).

If Phase 1 failed for the **third** time: STOP execution. Per spec failure-mode: "Phase 1 fails after 3 attempts → stop, re-brainstorm". Surface the failure to the user with the exact log lines; do not attempt a fourth build.

### Task 4: Test Phase 2 — Iter-1 + Iter-2 regression check

**Files:** runtime only.

- [ ] **Step 1: From the main menu, load any character + world**

Ask the user: "Bitte lade einen Charakter + Welt, warte bis du im Spiel bist (Hotbar sichtbar)."

- [ ] **Step 2: Open the ItemChecklist window**

Ask the user: "Drücke F1. Bitte beschreibe was du siehst: Fenster mit Hintergrund, Titel, Rows mit Icons + Namen (entdeckt) bzw `?` + `???` (nicht entdeckt)?"

Expected user-reportable observations:
- Window appears centered with vanilla CK Wood-theme 9-slice background
- Title text "Item Checklist" visible at top
- One row per catalog item, with icon + name for discovered items, `?` placeholder + `???` for undiscovered
- Mouse cursor visible while window is open (no immersive-mode camera capture)
- WASD does not move the character (input is captured by the window)

- [ ] **Step 3: Compare against Iter-1+2 baseline**

If user reports any visible regression (text missing on FIRST open, theme not applied, cursor invisible, input not captured): STOP. The Iter-3 changes touch only `ClearRows` (close-path) and the tail of `SpawnRows` (open-path) — none of them should affect open-state visuals on the **first** open. Any regression here is either a code typo from Phase 1 or a cascade from another mod (check Player.log).

If clean: proceed to Task 5.

### Task 5: Test Phase 3 — Multi-open text-fix verification

**Files:** runtime only.

This is the verification for the `pugText.Clear()` edit.

- [ ] **Step 1: Close + reopen the window**

Ask the user: "Drücke Escape (Fenster schließt). Dann F1 wieder. Sind alle Texte (Titel + Row-Namen) auf dem 2. Öffnen weiterhin sichtbar?"

Expected: YES. All texts still render. Iter-2 bug: row labels and title went blank on 2nd+ open. Iter-3 fix: `pugText.Clear()` releases the pool slot back so the next open can re-acquire it.

- [ ] **Step 2: Do the close+reopen at least 3 times in a row**

Ask the user: "F1 → Escape → F1 → Escape → F1 — alle Öffnungen mit voller Text-Darstellung?"

Expected: YES, all three openings render text correctly. (Iter-2 bug worsened with each cycle as pool starvation accumulated.)

- [ ] **Step 3: Verify main-menu regression is gone**

Ask the user: "Beende das Spiel jetzt ins Hauptmenü (`Disconnect` oder `Exit to Main Menu`). Sind die Hauptmenü-Texte (Play, Settings, Quit) sichtbar?"

Expected: YES. Iter-2 bug: after the first window open, the **main menu's own** PugTexts went blank because they shared the same starved pool. Iter-3 fix: pool is correctly released, main menu retains its slots.

- [ ] **Step 4: Decide outcome**

If Phase 3 passed: proceed to Task 6.

If Phase 3 failed (any of the 3 sub-checks reports blank text): STOP. Per spec failure-mode: "Phase 3 fails 1 attempt → pivot to Spawn-Once architecture". This means `pugText.Clear()` alone is not sufficient to release the pool — IB additionally uses its own `FreePooledElement` instead of `Destroy`. Drop into Iter-3.5 brainstorm: design a Spawn-Once architecture where rows are instantiated once on first open and re-bound (icon/name/checkmark) on subsequent opens instead of destroyed+respawned.

### Task 6: Test Phase 4 — Scroll-fix + clipping verification

**Files:** runtime only.

This is the verification for the `SetScrollValue(0f)` edit.

- [ ] **Step 1: Reopen the checklist window**

Ask the user: "Re-load Char+Welt falls nötig. F1 drücken — Fenster öffnen."

- [ ] **Step 2: Mouse-wheel scroll inside the window**

Ask the user: "Maus über das Fenster, dann Mausrad nach unten scrollen. Bewegt sich der Row-Inhalt vertikal?"

Expected: YES, rows scroll. Iter-2 bug: wheel input was registered (no UIScrollWindow warnings in log) but nothing moved. Iter-3 fix: `SetScrollValue(0f)` activates the scroll system to react to input.

- [ ] **Step 3: Verify scroll bounds**

Ask the user: "Scroll bis ganz nach unten — stoppt es am letzten Row, oder scrollt es endlos? Dann scroll wieder nach oben."

Expected: stops at last row (bottom), stops at first row (top). No over-scroll past either bound.

- [ ] **Step 4: Verify clipping (bug #3)**

Ask the user: "Während du scrollst — werden Rows, die oberhalb/unterhalb des Fenster-Rahmens liegen, abgeschnitten (clipped) oder ragen sie sichtbar über den Rahmen hinaus?"

Expected: clipped. UIScrollWindow handles clipping internally once scroll is active, so this should be auto-fixed by Phase 4 working — no separate code change needed.

- [ ] **Step 5: Decide outcome**

If Phase 4 passed: all three bugs fixed. Proceed to Task 7.

If "scroll lands at bottom instead of top" on first open: trivial fix — change `scrollWindow.SetScrollValue(0f)` to `scrollWindow.SetScrollValue(1f)` in `ItemChecklistWindow.cs`. Rebuild (`source .envrc && ../../../utils/build.sh`), re-test Phase 4 only. The direction is documented as "not yet empirically verified" in both spec Risk #2 and the code comment — this is the verification.

If "scroll still doesn't work" after the 0f→1f flip too: STOP. Per spec failure-mode: "Phase 4 fails 1 attempt → trigger decompile-spike on UIScrollWindow class". Defer to Iter-3.5 brainstorm: open `CoreKeeperModSDK/Library/PackageCache/Pug.UnityExtensions/UIScrollWindow.cs` via ILSpy and trace what `SetScrollValue` actually does internally. There may be additional state setup IB does that we haven't replicated.

If clipping fails despite scroll working: STOP and inspect. UIScrollWindow's clipping is internal — its absence would mean the `Mask` component or `RectMask2D` on the scroll viewport is missing from the prefab. That's a prefab issue, not a code issue, and lives outside the scope of Iter-3 (would be Iter-3.5).

---

## Phase 3 — Wrap-up

### Task 7: Update memory + spike-4 status + suggest commit

**Files:**
- Modify: `docs/research/spike-4-ui-architecture.md` (status header at top of file)
- Modify: `~/.claude/projects/-Users-valgard-Projects-private-core-keeper/memory/project_item_checklist_ui_pivot_state.md` (the entry body — update Stand-line)

- [ ] **Step 1: Update `spike-4-ui-architecture.md` status header**

Open `docs/research/spike-4-ui-architecture.md` and locate the existing status block near the top that has `Iter-1 VALIDATED ...` and `Iter-2 PARTIAL ...` markers.

Find this line (exact text may vary by exactly what Iter-2 wrote — read first, then edit):

```
**Iter-2 PARTIAL (commit 2b4b824, 2026-05-25):** ItemRow.prefab + display layer ✓; mouse-wheel scroll ✗; multi-open PugText pool leak ✗; clipping ✗ (follow-on from scroll)
```

Replace the `Iter-2 PARTIAL` line with:

```
**Iter-2 PARTIAL (commit 2b4b824, 2026-05-25):** ItemRow.prefab + display layer ✓; mouse-wheel scroll ✗; multi-open PugText pool leak ✗; clipping ✗ (follow-on from scroll) — superseded by Iter-3
**Iter-3 DONE (2026-05-26):** All three Iter-2 bugs fixed via two API-call additions to ItemChecklistWindow.cs: pugText.Clear() in ClearRows (pool release before Destroy) + scrollWindow.SetScrollValue(0f) in SpawnRows (activates scroll input). Bug-fix-only iteration — no new features. Filter+search deferred to Iter-4.
```

If the exact phrasing of the Iter-2 line in the file differs from what's shown above, preserve its content and only append the supersedence note + the new Iter-3 line.

- [ ] **Step 2: Update the project memory**

Open `~/.claude/projects/-Users-valgard-Projects-private-core-keeper/memory/project_item_checklist_ui_pivot_state.md`.

Update the body so the current status reads "Iter-3 DONE 2026-05-26", listing what was fixed (the three Iter-2 bugs), and what's now pending (Iter-4: filter UI, search input, click-to-toggle).

Concretely: the file currently leads with "Iter-1 validiert 2026-05-25". Append/replace its current "Stand" sentence to:

```
**Iter-3 DONE 2026-05-26** — Alle drei Iter-2-Bugs (Mouse-wheel scroll dead, PugText-Pool-Leak bei Multi-Open + Hauptmenü-Text-Disappear, Out-of-Window-Clipping) durch zwei zusätzliche API-Calls in ItemChecklistWindow.cs gefixt: `pugText.Clear()` vor `Object.Destroy` in `ClearRows` + `scrollWindow.SetScrollValue(0f)` nach `UpdateScrollHeight` in `SpawnRows`. Iter-4 pending: Filter-Dropdown (All/Discovered/Undiscovered), Search-Input (TextInputInterface-Spike erforderlich), Click-to-Toggle-Discovery. Branch `iter-3` pending FF-merge zu main nach User-Approval.
```

Also update the MEMORY.md index entry for this memory:

Open `~/.claude/projects/-Users-valgard-Projects-private-core-keeper/memory/MEMORY.md`. Find the existing `item-checklist-ui-pivot-state` line and update its one-line hook to reflect Iter-3 DONE.

- [ ] **Step 3: Surface the commit suggestion to the user**

Show the user this proposed commit message + git command:

```bash
git add unity/ItemChecklist/ui/ItemChecklistWindow.cs docs/research/spike-4-ui-architecture.md docs/superpowers/specs/2026-05-26-itemchecklist-ui-pivot-iter3-design.md docs/superpowers/plans/2026-05-26-itemchecklist-ui-pivot-iter3.md
git commit -m "$(cat <<'EOF'
fix(ui): release PugText pool slots and activate scroll on row spawn

Iter-3 — minimal bug-fix iteration closing the three issues Iter-2 left
open:

1. ClearRows now calls pugText.Clear() on every PugText child of a row
   before destroying the GameObject. Without this, PugText's shared
   internal pool leaked on every Destroy, which manifested as row
   labels going blank on 2nd+ window open and the main menu's own
   PugTexts going blank after the first window open.

2. SpawnRows now calls scrollWindow.SetScrollValue(0f) after wiring
   the IScrollable + invoking UpdateScrollHeight. Without this,
   UIScrollWindow was wired correctly (no warnings) but did not respond
   to mouse wheel input. The 0f value sets initial scroll to top.

Both fixes are direct ports of the patterns used by ItemBrowser
(BasicEntriesListRenderer.ClearList for the pool release;
EntriesList.SetEntries for the SetScrollValue call). Spike + spec +
plan documented in docs/superpowers/ and docs/research/spike-4.

Filter UI, search input, and click-to-toggle are deferred to Iter-4.
EOF
)"
```

Ask the user: "Phase 4 hat alle drei Bugs als gefixt verifiziert. Möchtest du den Commit so ausführen, oder soll ich vorher etwas anpassen?"

Wait for explicit user confirmation. Do NOT run `git commit` yourself unless the user explicitly says so. Per project convention: NEVER commit without explicit user approval.

If the user requests amendments to the commit message: edit, re-show, wait again.

If the user confirms: run `git add ...` + `git commit -m ...` as shown.

- [ ] **Step 4: Surface the FF-merge suggestion for after the commit**

Once the commit is made, show the user this proposed merge command (do NOT run it):

```bash
# Run from item-checklist repo root (not from inside the worktree)
cd ../../  # leaves .worktrees/iter-3 → goes to item-checklist repo root
git checkout main
git merge --ff-only iter-3
git worktree remove .worktrees/iter-3
git branch -d iter-3
```

Note: the `cd ../../` is critical per CLAUDE.md ("Before `git worktree remove`, ALWAYS ensure the Bash CWD is NOT inside the worktree"). Removing the worktree while CWD is inside it irreversibly blocks the shell session.

Ask the user: "Möchtest du den FF-merge nach main jetzt machen, oder zuerst noch was am iter-3-Branch testen?"

Wait for user decision. If they confirm: execute. If they want to wait: leave the suggestion and end the plan.

---

## Test plan summary

All four test phases live in Phase 2 above; this is the recap for the wrap-up commit message and the memory update:

| Phase | What it verifies | Pass criterion |
|---|---|---|
| 1. Sandbox compile | `pugText.Clear()` + `SetScrollValue(0f)` are sandbox-permitted | `Successfully compiled ItemChecklist safetyCheck=True` in Player.log, no CompileFailed |
| 2. Iter-1+2 regression | Code changes did not break existing first-open behavior | Window opens with theme + title + rows + cursor + input-block on first F1, identical to Iter-2-partial |
| 3. Multi-open text-fix | `pugText.Clear()` releases pool slot back | Texts render on 2nd+ open; main menu PugTexts intact after first window open |
| 4. Scroll-fix + clipping | `SetScrollValue(0f)` activates scroll input; clipping is internal | Mouse wheel scrolls content; bounds respected; out-of-viewport rows clipped by UIScrollWindow |

Failure-mode escalations per spec are documented inline in each task.
