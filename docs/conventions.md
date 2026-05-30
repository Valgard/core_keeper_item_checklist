# ItemChecklist Code & Process Conventions

## Branch + Commit Conventions

**Branch naming:** `iter-<n>[.<m>[-letter]]`, e.g. `iter-3-7`, `iter-3-5c`.
The letter suffix marks a within-iteration pivot (e.g. `iter-3-5b` was an
aborted approach, `iter-3-5c` was the successful re-design).

**Commit types** (conventional commit style with scopes):

| Type | When to use |
|---|---|
| `feat` | New user-visible behavior (e.g. a new iter's core feature) |
| `fix` | Bug fix — regression or correctness issue |
| `refactor` | Internal restructure without behavior change |
| `docs` | Documentation-only change |
| `wip` | In-progress checkpoint (within a plan-branch) |

WIP commits in plan-branches document the development story (e.g.
`wip(spike): first decompile pass on UIScrollWindow`). They are kept
individually — no squash before merge.

**Merge policy:** ff-merge to main; no squash; no merge commits. Each commit
stays `git log`/`git blame`/`git bisect`-friendly. Spec and plan are committed
as early, separate commits on each branch.

**Force-push:** after a rebase of a pushed feature branch, force-push with
`--force-with-lease`.

## Worktree Conventions

**Setup:** every new worktree needs two gitignored files copied from the main
checkout:

```bash
cp .envrc .worktrees/<branch>/.envrc
rsync -a unity/ItemChecklist/Art/ .worktrees/<branch>/unity/ItemChecklist/Art/
# rsync with trailing slashes — cp -R creates a double-nested directory
```

`.envrc` carries the machine-local build env (`UNITY_BIN`, `SDK_PATH`, etc.)
and is never tracked in git. The Art directory carries the bridge sprites
(also gitignored) needed to build.

**superpowers specs/plans live only in the worktree.** The
`superpowers:brainstorming` / `writing-plans` skills default their spec and
plan output to `docs/superpowers/`, which is **gitignored** in this repo (to
avoid colliding with that default location). Because the worktree's
`docs/superpowers/` is not tracked, the per-iter spec and plan must be copied
back to the main checkout **before** `git worktree remove` destroys the
worktree — otherwise they are lost. Treat copying `docs/superpowers/specs/`
and `docs/superpowers/plans/` to main as part of worktree teardown.

**Before destroying a worktree:**

```bash
# Verify CWD is NOT inside the worktree
cd /path/to/main-repo-root
git worktree remove .worktrees/<branch>
```

Destroying a worktree while the shell CWD is inside it can block the shell
session irreversibly.

**Build verification:** after install, grep `Player.log` for the expected log
markers (e.g. `Loading mod with ID`, `ItemChecklist: Bake complete`). Do not
trust build output alone — a build duration under 60 s or an install mtime
older than the source file is suspicious and warrants a re-run.

## Testing Conventions

Each iter ends with a structured acceptance test. The canonical 7-phase
structure (established in Iter-3.6/3.7):

| Phase | What to check | Zero-tolerance? |
|---|---|---|
| 1. Sandbox compile | Player.log grep for `CompileFailed` — must be zero | Yes |
| 2. First-open regression | Visual baseline from previous iter must be intact | Yes (1-strike) |
| 3. Pool-leak regression | Multi-open `F1 → ESC → F1` × N; main-menu PugTexts must not go blank | Yes |
| 4. Feature verification | The new iter's core addition works as specified | Yes |
| 5. Layout side-effects | Nothing looks worse than before (scroll, clipping, row spacing) | Yes |
| 6. Localization check | Switch language in CK settings; checklist rebakes with new names | Yes if loc work was done |
| 7. Cooked-food spot-check | Open checklist; verify cooked-food entries appear and tick on pickup | Yes if food work was done |

**Failure budget:** 3 sandbox-compile attempts (Phase 1) before escalating to
a fresh batchmode build from a clean state. 1-strike for Phase 2 and later.

### Throwaway test-scaffold pattern

To accelerate a manual in-game test, it is acceptable to layer a *throwaway*
debug scaffold *in front of* the reviewed core — then remove it via
`git restore` before merge so the merged code stays byte-identical to the
reviewed version.

Iter-3.8 example: reaching the end of the ~10720-entry list by scrolling takes
too long to verify flush-geometry on the last row. A throwaway
`DebugDiscoveredOnly` index-remap was added in front of the recycler
(`catalogIdx = _useMap ? _indexMap[idx] : idx`), making any slice of the list
reachable in seconds while exercising the identical flush-geometry path. The
scaffold lived only in uncommitted working-tree edits; the reviewed row logic
was already committed, so a `git restore` of the modified `.cs` removed the
scaffold cleanly without touching reviewed code. (The same remap can later
seed the real Iter-8 discovered-only filter.)

Rule: scaffolds never get committed onto a story commit. Add them to the
working tree, use them, then `git restore` before the iter's ff-merge.

**Recovery:** if Core Keeper hangs on the loading screen (quit-deadlock in
`ModManager` — symptom: `Exit blocked by ModManager` in Player.log), use:

```bash
pkill -KILL -f "Core Keeper"   # GNU pgrep/pkill -f variant; macOS-safe
```

Do not use the normal quit path — it blocks and the process must be killed.

## File Layout

Canonical layout under `unity/ItemChecklist/` (source of truth is the git
tree):

```
unity/ItemChecklist/
  ItemChecklistMod.cs             IMod bootstrap (EarlyInit/Init/Update/Shutdown)
  ItemCatalog.cs                  catalog bake + lookup
  ItemCatalogLocChangeHook.cs     Harmony patch — re-bake on language change
  ItemCatalogWorldLoadHook.cs     Harmony patch — kick bake on world load (OnOccupied)
  DiscoveredState.cs              in-memory mirror of CK discovery state
  SaveManagerDiscoveryHook.cs     Harmony patch on SaveManager.SetObjectAsDiscovered
  SaveManagerActiveSelectHook.cs  Harmony patch for active-character resolution
  CharacterDataDiscoverySnapshot.cs  initial-state reader on OnAfterDeserialize
  PascalCaseSplitter.cs           pure utility (display-name fallback formatting)
  Data/                           baked catalog / lookup data assets
  ModManifest.json                mod manifest (displayName, requiredOn, etc.)
  ItemChecklist.asmdef            runtime assembly definition
  ui/
    ItemChecklistWindow.cs        IModUI implementation (UIelement subclass)
    ItemRow.cs                    row MonoBehaviour (Bind API)
    ItemChecklistContent.cs       IScrollable implementation (viewport recycler)
    FilterAndSearchModel.cs       filter/search state (deferred Iter-8)
    UnityInputFieldAdapter.cs     input-field adapter for search (deferred Iter-8)
  Prefabs/
    ItemChecklistWindow.prefab    window hierarchy
    ItemRow.prefab                row template (recycled by scroll list)
  Art/Bridge/                     placeholder sprites (gitignored; replace before publish)
  Editor/
    ItemChecklist.Editor.asmdef   editor-only assembly for CLIBuildHelper
    CLIBuildHelper.cs             -executeMethod entry point for build.sh
    CLIPublishHelper.cs           -executeMethod entry point for upload.sh
```

**Naming patterns:**

- `*Hook.cs` — top-level Harmony patches
- `*Snapshot.cs` — top-level initial-state readers
- `ui/*.cs` — IModUI, IScrollable, and row logic
- `Editor/*.cs` — editor-only build/publish helpers (in separate `.asmdef`)
