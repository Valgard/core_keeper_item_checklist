# Spike #4 — UI Architecture (Reißbrett)

**Date:** 2026-05-25
**Status:** Decision: **Option A — CoreLib + SpriteRenderer**.
**Iter-1 validated 2026-05-25** (window opens centered, themed
9-slice wood background, PugText title visible, cursor on top, WASD/mouse
blocked, Escape closes, F1 reopens).
**Iter-2 PARTIAL 2026-05-25** (commit `2b4b824`): ItemRow.prefab +
display-layer + IScrollable wiring. 3 bugs deferred: mouse-wheel scroll
dead, multi-open PugText pool leak, viewport clipping.
**Iter-3 PARTIAL DONE 2026-05-26**: `pugText.Clear()` loop in `ClearRows`
fixes the pool leak under current mod.io CoreLib runtime (4.0.3-Code).
Phase 3 PASS verified (13× F1 + disconnect, texts persist; main menu
texts intact after first open). Scroll-fix and background-bug deferred:
- **Iter-3.5** (Scroll): `SetScrollValue(0f)` was tested and has
  destructive side-effects on layout (rows start at window-top instead
  of below title). Needs decompile-spike on `Pug.UnityExtensions.dll`'s
  `UIScrollWindow` for a side-effect-free activation path.
- **Iter-3.6** (Background quadrat small): empirically falsified all
  reachable hypotheses (Iter-1+2+3 code, CoreLib 4.0.0 vs 4.0.3 vs 4.0.4
  in every Build-Time/Runtime combination, all other 17 mods disabled).
  Cause lies in CK game files, Wine bottle sprite-atlas cache, or
  unknown environmental factor. Requires forensic Iter with new
  hypotheses.
- **Iter-3.7** (latent): empirically verified that `pugText.Clear()`
  fix works under CoreLib 4.0.3 but NOT under real 4.0.4 (UI pool
  behaviour differs measurably). Currently mod.io hosts a build labeled
  "4.0.4" but binary-identical to commit `3c99dc44` (CoreLib 4.0.3, 30
  April 2026). If mod.io ever publishes the real 4.0.4 (commit
  `31242dbe`, 6 May 2026), Iter-3 fix becomes obsolete.

See [Architecture Options](#architecture-options); Options B and C are
documented below as considered alternatives.

## Question

After five failed UI pivots (code-built → prefab → UIelement+UICamera-parent
→ Canvas-Overlay → Canvas-Screen-Space-Camera → back to Overlay), which UI
architecture do successful Core Keeper UI mods actually use, and what
should ItemChecklist adopt?

The current `initial-impl` branch (last commit `9197a51`) has a uGUI-based
window prefab that renders partially but leaks input through to the player
character and renders the cursor below the window.

## Method

Cloned 10 Core Keeper mod repos plus the CoreLib framework and CoreLib's
official documentation repo. Inspected every UI-related prefab (43,808
lines of YAML total), every IMod bootstrap, every UI MonoBehaviour, and
cross-checked against mod.io dependency declarations.

## Universal Findings

### 1. Zero uGUI in the entire ecosystem

`grep` for `Canvas` and `RectTransform` across all `.prefab` files in all
10 cloned mods: **zero hits**. Not "uncommon" — non-existent. Every
successful UI mod uses `SpriteRenderer` + `Transform` (not RectTransform)
+ Unity layer 5 (UI) + custom sorting layer + sorting order.

### 2. Why uGUI structurally fails on CK

CK's `UIMouse` does Physics-Raycast in the UI layer. Canvas/Image elements
have no Collider — they are structurally invisible to the raycast, so they
never receive cursor focus and never block input. uGUI and CK's UI
coexist in rendering but not in input. This is the root cause of "WASD
goes through" and "cursor under window" in the current pivot.

### 3. Two canonical mount strategies

All UI mods mount their window as a child of an existing vanilla UI
hierarchy via a Harmony postfix:

- **Standalone window:** child of `UIManager.chestInventoryUI.transform.parent`
  (this is the parent CoreLib uses automatically)
- **In-place extension:** child of a specific vanilla UI path
  (e.g. `Manager.ui.mapUI.transform.Find("container/largeMapBorder")`)

Mounting in a vanilla parent inherits the correct Canvas-equivalent
camera setup, Z-order, layer mask, scale, and `UIMouse` visibility — for
free. None of our five pivots did this; all five built a parallel UI
stack.

### 4. `UIelement` inheritance + `bottomUIElements`/`topUIElements`
linking

Required for controller navigation and `UIMouse` integration. Without
the linkage, the slots are invisible to the navigation graph even if
they render correctly.

### 5. Cursor/input/pause blocking is a separate, small patch layer

Not part of UI layout. Two patterns:

- **With CoreLib:** 2 patches (`UIManager.isAnyInventoryShowing` postfix
  + `HideAllInventoryAndCraftingUI` postfix) — vanilla CK logic does the
  rest.
- **Without CoreLib (DIY, ItemBrowser-style):** 11 hand-written
  Harmony patches (pause-disable, interaction-block, hotbar-show,
  shortcut-hide, scroll-filter, controller-mouse-force, etc.).

## Pattern Matrix (UI mods only)

| Mod | Author | UI type | Renderer | Layer | Base class | Mount | Uses CoreLib UI? |
|---|---|---|---|---|---|---|---|
| ItemBrowser | moorowl | full window | SpriteRenderer | 5 | `: UIelement` | DIY in `Awake` | no — DIY, 11 hand patches |
| MapMarkersPlus | moorowl | drawer extension | SpriteRenderer | 5 | `: UIelement` | postfix `MapUI.Awake` | no |
| NameChests / "More Labels" | moorowl | world labels | SpriteRenderer | 5 | vanilla `WorldText` | world-mounted | no |
| HealthBars (moorowl) | moorowl | entity overlay | SpriteRenderer | 5 | `ResourceBar` | per-entity spawn | no |
| HealthBars (AylanJ) | AylanJ123 | entity overlay | SpriteRenderer | — | EntityMonoBehaviour patch | per-entity | no |
| MapExtras | super-miner | in-place map ext. | extends vanilla MapUI | — | `MapManager` | empty GameObject + MonoBehaviour | no |
| PlacementPlus | limoka | mini toggle UI | SpriteRenderer | 5 | `: UIelement` | DIY | only `ControlMappingModule` |
| **BookMod** | **limoka** | **standalone window** | **SpriteRenderer** | **5** | **`: UIelement, IModUI`** | **CoreLib auto** | **yes — `UserInterfaceModule`** |
| **DummyMod / Beating Dummy** | **limoka** | **standalone window** | **SpriteRenderer** | **5** | **`: UIelement, IModUI`** | **CoreLib auto** | **yes — `UserInterfaceModule`** |

**Not in this matrix because they build no UI of their own** (they are
gameplay-only mods from the original list, included earlier only for
completeness):

- `moorowl/DoubleChest` (= "Paintable Double Chest") — adds a craftable
  block. Uses vanilla chest UI / depends on Expanded Chest UI for display.
  Pure crafting/content patch.
- `Minepatcher/Double-Chest-Inventory` — patches the slot count on chest
  entities only. Uses Expanded Chest UI for display. Pure data/authoring
  patch.

**Source not publicly found** (excluded from analysis): Item Signs
(ceschmitt.de), Expanded Chest UI, ShredderPlus, StoragePlus.

## CoreLib UserInterface — Authoritative Facts (Official Docs)

| Question | Answer |
|---|---|
| CoreLib version | **4.0.4** (2026-05-06), actively maintained |
| Submodule status | **`pre-release`** — explicit "API may break" disclaimer in docs |
| Submodule load | `CoreLibMod.LoadSubmodule(typeof(UserInterfaceModule));` in `EarlyInit()` |
| UI class signature | `class MyUI : UIelement, IModUI { }` (inheritance **and** interface) |
| Alternative bases | `InventoryUI`, `CharacterWindowUI`, `ItemSlotsUIContainer` (slot containers) |
| Prefab structure | Root GO with `ModUIAuthoring` + `IModUI` component → child named `root` → all UI elements under `root` |
| Registration | `if (obj is GameObject go) UserInterfaceModule.RegisterModUI(go);` in `ModObjectLoaded()` |
| UI ID format | `"MyMod:UIName"` (Namespace:Name) |
| Opening | `UserInterfaceModule.OpenModUI("ItemChecklist:Window");` |
| Sprite constraint | Pixels-Per-Unit = **16**, snap to **1/16 grid** |
| Snap helper | `PixelSnap` component from CoreLib |
| Mount point | CoreLib mounts automatically in `chestInventoryUI.transform.parent` |
| Auto-hide | On vanilla `HideAllInventoryAndCraftingUI` (postfix) |
| Auto cursor/input block | Via `isAnyInventoryShowing` postfix → vanilla logic triggers correctly |
| Player-inventory link | Optional via `LinkToPlayerInventory` component |
| Migration 3.x → 4.x | 3 class renames (`LoadModules` → `LoadSubmodule`, etc.) |

### Distribution

- **For mod authors:** add the CoreLib git package to your SDK project's
  `Packages/manifest.json`. Source is linked into the build via the
  package's `package.json` (`name: ck.modding.corelib`).
- **For end users:** subscribe to CoreLib on mod.io. Mods that depend on
  CoreLib declare it as a Required Mod in their mod.io profile; the
  in-game subscription sync pulls it automatically.
- **Wine/CrossOver:** no Fake-ID complication — CoreLib follows the
  standard mod.io path. The Fake-ID workflow is only needed for our own
  unpublished dev builds, not for external dependencies.

### Note on "CoreLib.UserInterface" mod.io URL

A WebSearch result pointed at `mod.io/g/corekeeper/m/corelib-userinterface`.
That page returns **"has been deleted"** — the UserInterface module is
**not** distributed as a separate end-user subscription. It is part of the
main CoreLib package.

## Production Validation: BookMod + DummyMod

Two limoka mods use `UserInterfaceModule` productively. Both follow the
documented pattern with zero deviations. Sizes are representative of the
ItemChecklist scope.

| Mod | IMod LoC | UI LoC | Pattern |
|---|---|---|---|
| BookMod | 145 | 162 | Standalone window (`ShowWithPlayerInventory=false`), reads books, custom pagination |
| DummyMod (= **Beating Dummy** from original mod list) | 87 | 84 | Window beside inventory (`ShowWithPlayerInventory=true`), DPS stats, entity-context via `UserInterfaceModule.GetInteractionEntity()` |

**Key practice details from these two mods:**

- `Awake()` calls `HideUI()` — initial state is invisible.
- `BookUI.background.sprite = Manager.ui.GetCraftingUITheme(theme).background`
  — uses **vanilla CraftingUI themes** for the 9-slice window background.
  This solves the "grey box instead of wood frame" issue from
  `[[project_item_checklist_ui_pivot_state]]` without building any custom
  sprite.
- `DummyUI.OnResetClicked()` / `OnKillClicked()` are public parameterless
  methods — typical Unity-Event targets, wired to Button OnClick in the
  Editor. No code-side input plumbing.
- `world.EntityManager` is available as an inherited property from
  `UIelement`.
- **Zero patches** for cursor / pause / hotkey / mouse in either mod.
  CoreLib's internal `isAnyInventoryShowing` postfix is enough — the
  game does the rest. This is the answer to "WASD goes through" in the
  current pivot.

## File Layout Pattern (from DummyMod)

```
DummyMod/
├── DummyMod.asmdef
├── DummyMod.asset                ← ModBuilderSettings
├── DummyMod_modio.asset          ← mod.io profile reference
├── DummyMod_Steam.asset
├── SpriteAssetManifest.asset
├── Scripts/
│   ├── DummyMod.cs               ← IMod bootstrap
│   └── UI/
│       └── DummyUI.cs            ← IModUI implementation
├── UI/
│   └── DummyUI.prefab            ← window prefab
├── Prefab/
│   └── ...
└── Data/
    └── ...
```

Maps 1:1 to ItemChecklist's existing layout.

## Code Pattern Blueprint for ItemChecklist

```csharp
// Scripts/ItemChecklistMod.cs
public class ItemChecklistMod : IMod
{
    public const string MOD_ID = "ItemChecklist";
    public const string WINDOW_UI_ID = MOD_ID + ":Window";
    public static AssetBundle[] bundles;

    public void EarlyInit()
    {
        CoreLibMod.LoadSubmodule(typeof(UserInterfaceModule));
        bundles = this.GetModInfo().AssetBundles.ToArray();
        // Discovery / snapshot bootstrap (existing code) stays
    }

    public void ModObjectLoaded(Object obj)
    {
        if (obj is not GameObject go) return;
        UserInterfaceModule.RegisterModUI(go);
    }
    // Init / Shutdown / Update unchanged
}

// Scripts/UI/ItemChecklistWindow.cs
public class ItemChecklistWindow : UIelement, IModUI
{
    public GameObject root;
    public GameObject Root => root;
    public bool ShowWithPlayerInventory => false;   // standalone
    public bool ShouldPlayerCraftingShow => false;

    public SpriteRenderer background;               // sprite from Manager.ui.GetCraftingUITheme(...).background
    public PugText title;
    public Transform rowsRoot;
    public ItemRow rowTemplate;
    // existing serialized fields stay

    protected void Awake() { HideUI(); }
    public void ShowUI()  { root.SetActive(true); RefreshList(); }
    public void HideUI()  { root.SetActive(false); }
    // existing discovery / filter / snapshot methods stay
}

// Hotkey trigger from Update():
if (HotkeyPressed) UserInterfaceModule.OpenModUI(ItemChecklistMod.WINDOW_UI_ID);
```

## Architecture Options

### Option A — CoreLib + SpriteRenderer (recommended)

- **What:** Pivot the window prefab to SpriteRenderer + Layer 5; window
  class extends `UIelement` and implements `IModUI`; CoreLib bootstrap
  + `ModObjectLoaded` registration; vanilla CraftingUI theme for the
  background sprite.
- **Pro:** Smallest code surface (~170 LoC for bootstrap + UI shell,
  per BookMod/DummyMod evidence). Cursor / input / pause / mouse-mode
  are handled by CoreLib's internal patches — zero hand-written
  patches needed. DummyMod is a 1:1 reference codebase in the source
  tree. No Wine/CrossOver complication. CoreLib auto-mounts in the
  correct parent and auto-syncs hide on vanilla
  `HideAllInventoryAndCraftingUI`.
- **Contra:** Prefab rebuild from uGUI → SpriteRenderer is real work
  (ItemChecklistWindow.prefab + ItemRow.prefab must be reauthored —
  Transform instead of RectTransform, SpriteRenderer instead of Image,
  Layer 5, sorting layer/order). CoreLib becomes a Required Mod
  dependency in the mod.io profile. UserInterfaceModule is formally
  `pre-release` (though stable across 4.0.0 → 4.0.4 per Changelog,
  and used in 2 production mods).

### Option B — DIY SpriteRenderer (ItemBrowser-style)

- **What:** Same SpriteRenderer + Layer 5 + UIelement structure, but
  no CoreLib dependency. We write our own Harmony patches for cursor /
  pause / input / mouse and instantiate the window ourselves via a
  postfix on a vanilla UI's `Awake`.
- **Pro:** No external dependency — ItemChecklist is self-contained.
  Full control over every patch site.
- **Contra:** ~11 hand-written Harmony patches against CK internals
  (more surface area for breakage when CK updates). Reimplements what
  CoreLib already does. More code to maintain. ItemBrowser-level
  complexity for a feature scope that doesn't justify it.

### Option C — Continue with uGUI

- **What:** Keep the current Canvas/Image-based prefabs, keep searching
  for the right Canvas Render Mode / UICamera stack / RectTransform
  anchor configuration.
- **Pro:** Existing ItemRow.prefab + ItemChecklistWindow.prefab stay
  (already YAML-validated for hierarchy / anchors / sprites / WindowView
  wiring).
- **Contra:** 0/10 comparable mods do this. UIMouse Physics-Raycast
  structurally does not find Canvas/Image (= "input passes through"
  cannot be solved by further configuration tuning). 9-slice mismatch
  because uGUI Image.type=Sliced uses different border math than
  SpriteRenderer drawMode=Sliced. Cursor Z-order requires a parallel
  UICamera stack on our side. Working against the ecosystem.

## What Survives Any Pivot

Mod logic and Harmony hooks are orthogonal to the UI layer. These stay
unchanged regardless of architecture choice:

- Discovery-state tracking (`InventoryAddPatch`)
- Snapshot pattern
- Filter / search model
- Hotkey handling
- Harmony state-machine pattern for active-character GUID resolution
- Sub-sprite loading from the `ui_classic` atlas (mechanism stays;
  may need PPU adjustment to 16)
- PugText API usage (`Render`, `SetTempColor`, `dimensions`)

Estimated: ~60–70% of the existing code survives a UI pivot. The pivot
work is concentrated in the prefab YAML and the window root MonoBehaviour.

## Open Risks

- **CoreLib `pre-release` API.** Both production mods that use it
  (BookMod, DummyMod) have been stable across CoreLib 4.0.0 → 4.0.4
  (Feb–May 2026), and the Changelog shows no UserInterface changes in
  that range. But the disclaimer is explicit in the docs — a 4.1 or 5.0
  break would force us to update. Mitigation: pin CoreLib to a specific
  tag in `Packages/manifest.json` rather than tracking `main`.
- **mod.io dependency declaration.** End users get CoreLib auto-pulled
  on subscription sync, but the subscription sync caveat from
  `[[corekeeper-compile-fail-cascade]]` still applies — opening the
  Mods menu re-syncs everything, can wipe Fake-ID dev installs. For
  publishing, this is fine; for dev work, the existing
  `utils/install-macos.sh` / Fake-ID workflow remains unchanged for
  our own mod.
- **Sprite PPU mismatch.** Existing sub-sprite extraction from
  `ui_classic` may not be PPU=16. If not, sprite assets need
  re-importing with PPU=16 + 1/16 grid snap. Without this, sprites
  render at wrong scale and / or misaligned.

## Sources

### Cloned repositories (`/tmp/ck-ui-research/`)

- `CoreKeeperMods/CoreLib` — framework source + UserInterface submodule
- `CoreKeeperMods/corekeepermods.github.io` — official docs (v3 + v4)
- `moorowl/ItemBrowser` — DIY SpriteRenderer reference (35,701-line prefab)
- `moorowl/HealthBars`
- `moorowl/MapMarkersPlus`
- `moorowl/DoubleChest`
- `moorowl/NameChests` (= "More Labels")
- `limoka/CoreKeeperMods` — collection including PlacementPlus, BookMod,
  DummyMod, KeepFarming, SecureAttachment, MovableSpawners,
  CustomizeWaterPriority, UnityExplorer
- `Minepatcher/Double-Chest-Inventory`
- `AylanJ123/some-nice-health-bars`
- `super-miner/MapExtras`

### Official documentation

- `https://corekeepermods.github.io` (renders client-side; source is
  in the cloned `corekeepermods.github.io` repo under `v3/` and `v4/`)
- `v3/UserInterface.md` — concise narrative guide
- `v4/modules/user-interface/README.md` — current API reference
- `v4/getting-started/install.md` — `Packages/manifest.json` git-package
  setup
- `v4/getting-started/migration-guide.md` — 3.x → 4.x rename list

### mod.io dependency checks (via Jina Reader proxy)

- `mod.io/g/corekeeper/m/core-lib` — "No dependencies found"; this is
  the end-user CoreLib subscription
- `mod.io/g/corekeeper/m/placementplus` — depends on CoreLib
- `mod.io/g/corekeeper/m/storageplus` — depends on CoreLib
- `mod.io/g/corekeeper/m/shredderplus` — "No dependencies"
- `mod.io/g/corekeeper/m/mapmarkersplus` — "No dependencies"
- `mod.io/g/corekeeper/m/itembrowser` — "No dependencies"
- `mod.io/g/corekeeper/m/healthbars` — "No dependencies"
- `mod.io/g/corekeeper/m/corelib-userinterface` — **deleted**, not a
  separate subscription

### Cross-references

- `[[project_item_checklist_ui_pivot_state]]` — state of the failed
  pivots
- `[[project_pugstorm_sandbox_rules]]` — sandbox constraints
- `[[feedback_native_first_then_harmony]]` — research-first mindset
- `[[project_corekeeper_compile_fail_cascade]]` — mod.io subscription
  sync caveat
- `[[project_pugstorm_modbuilder_sprite_meta]]` — sprite import gotcha
