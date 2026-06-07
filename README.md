# Item Checklist

A Core Keeper mod that tracks which items you have discovered in a world. Press
**F1** for a scrollable checklist of every discoverable item — vanilla and
mod-added alike — that auto-checks items on pickup, hides undiscovered items to
avoid spoilers, and keeps a live discovery counter on the HUD.

Discovery is tracked **per world × per player**.

## Features

- **Discovery checklist (F1)** — every discoverable item with its icon, localized
  name and discovered/undiscovered state; undiscovered items show as `???`.
- **Always-on HUD counter** — `N / M (p.p%)` discovered, top-right above the
  minimap, updating live.
- **Sort** by name, rarity, level or value (ascending/descending).
- **Faceted filter** — discovery, category, rarity and craftability
  (multi-select; OR within a dimension, AND across dimensions).
- **Name search** — matches the localized name in your game's language;
  undiscovered matches stay `???`.
- **Per-row Level and Value** columns (sell value in Ancient Coins).
- **Rarity colouring** of item names and icon borders (undiscovered rows too).
- **Per-permutation cooked-food tracking** (~10,800 catalog entries; Mushroom
  Soup ≠ Tomato Soup, across Base/Rare/Epic tiers).
- **English and German**, following the in-game language; switches live.

## Requirements

- Core Keeper (verified on 1.2.1.4)
- [CoreLib](https://mod.io/g/corekeeper/m/corelib) — required dependency

## Installation

Subscribe in-game through the **Mods** menu (or on the mod.io website) and
restart. [CoreLib](https://mod.io/g/corekeeper/m/corelib) must be installed
alongside it.

## Known Limitations

- **Placeholder art.** The rarity border and the scrollbar track/handle use
  placeholder sprites (see [Credits](#credits)). Real pixel-art is planned for a
  later version.
- **No per-variation tracking.** Each item family is tracked once; colour / skin
  / state variants do not get their own row.
- **Cooked-food Rare/Epic tiers** are included but not yet verified against live
  cooking events — unreachable tiers, if any, simply stay greyed out.

## Localisation

The mod UI is localised in **English and German** and follows the in-game
language. Additional languages can be added as data — add one entry per language
in `localization/localization.yaml` and rebuild.

## Credits

Some UI bridge sprites (the rarity border and the scrollbar track/handle) are
derived from [Item Browser](https://github.com/moorowl/ItemBrowser) by **moorowl**,
used under the MIT License (© 2026 moorowl). These are placeholders pending the
mod's own pixel-art (see [Known Limitations](#known-limitations)).

## License

Personal-use, non-commercial — Pugstorm Core Keeper EULA. Built against the
official `CoreKeeperModSDK`. Source on GitHub; contributions and translations
welcome.

---

## Development

### Building

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
root, but from inside a worktree (`.worktrees/<branch>/`) the same relative path
resolves to `.worktrees/utils/`, which does not exist. From a worktree, use an
absolute path to the shared `utils/` directory, or the worktree-relative
`../../../utils/build.sh`.

### Publishing

Publishing runs through the SDK's mod.io plugin via `../utils/upload.sh`, which
reads the published version and changelog from the topmost `## [x.y.z]` entry in
`CHANGELOG.md`. See the parent `CLAUDE.md` (sections "mod.io publishing" and
"The three mod IDs") for the full flow.

### Documentation

- [Architecture overview](docs/architecture.md)
- [Code conventions](docs/conventions.md)
- [Known gotchas](docs/gotchas.md)
- [Iteration history](docs/iteration-history.md)
- [Future roadmap](docs/roadmap.md)
- [Mod-internal CLAUDE.md](CLAUDE.md) (for AI assistants working in this repo)
