# ItemChecklist

ItemChecklist is a Core Keeper mod that tracks which items the player has
discovered. A scrollable in-game checklist (open with F1) lists all
discoverable items — vanilla and mod-added alike — showing
discovered/undiscovered state with item icons and localized names. Cooked-food
permutations are tracked per ingredient pair. Requires CoreLib.

## Requirements

- Core Keeper game (verified on 1.2.1.4)
- CoreLib mod (declared as dependency in ModBuilderSettings)

## Building

```bash
cd item-checklist
source .envrc && ../utils/build.sh
# On macOS: auto-runs install-macos.sh at the end.
# Unity Editor must be closed before build.sh (the Editor locks the project).
```

The build script refreshes SDK symlinks, runs a Unity batchmode build, and on
macOS places the freshly built mod into the fake-ID loader locations so Core
Keeper picks it up on next launch. See the parent `CLAUDE.md` for the full
build/install system and CrossOver/macOS specifics.

**Building from a git worktree:** `../utils/build.sh` is correct from the mod
root, but from inside a worktree (`.worktrees/<branch>/`) the same relative
path resolves to `.worktrees/utils/`, which does not exist. From a worktree,
use an absolute path to the shared `utils/` directory, or the worktree-relative
`../../../utils/build.sh`.

## Art / Publishing

Before publishing to mod.io, replace all sprites in
`unity/ItemChecklist/Art/Bridge/` with original or commissioned art. The
current Bridge sprites are derived from Item Browser (MIT-licensed) and must
not ship in the published mod.

## Known Limitations

- **No visible scrollbar yet.** The window prefab has no wired scrollbar
  (`scrollBar: {fileID: 0}`), so the scroll track does not render. Scrolling
  works via the mouse wheel; a visible scrollbar is deferred to Iter-9.

## Documentation

- [Architecture overview](docs/architecture.md)
- [Code conventions](docs/conventions.md)
- [Known gotchas](docs/gotchas.md)
- [Mod-internal CLAUDE.md](CLAUDE.md) (for AI assistants working in this repo)
