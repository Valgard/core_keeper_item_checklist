# CLAUDE.md — ItemChecklist mod

ItemChecklist is a Core Keeper mod that tracks which items the player has
discovered, showing them as a scrollable checklist UI in-game. Parent
guidance (build setup, sandbox rules, macOS/CrossOver workflow,
`utils/build.sh`, fake-ID install) lives in the parent directory's
`CLAUDE.md` (sibling to this mod's repo root). This file holds
**ItemChecklist-specific** detail that other Core Keeper mods would not
need.

## Architecture (post-Iter-3.8)

Discovery state is split across four collaborating classes:

| Class | Responsibility |
|---|---|
| `ItemCatalog` | Static catalog of every discoverable item, baked once per world-load. Iter-3.7: two-loop architecture — Loop 1 enumerates standard items from `PugDatabase.objectsByType.Keys` (skipping items with `IsCookedFood()`); Loop 2 (α-enumeration) cartesians ingredient-pairs to emit cooked-food permutations × 3 tier-variants. Catalog grows to ~10720 entries. |
| `DiscoveredState` | In-memory mirror of `CharacterData.discoveredObjects2` for the active character. Keyed on packed `long` (`(objectId << 32) \| (uint)variation`) via `PackKey`. Two events: `Discovered(int, int)` per new pickup, `Changed` after any mutation. |
| `SaveManagerDiscoveryHook` | Harmony postfix on `SaveManager.SetObjectAsDiscovered`. Filters `__result == true` (CK fires the method ~261×/30s including non-new from `DetectUndiscoveredObjectsInInventory`). Mirrors `(objectID, variation)` into `DiscoveredState`. |
| `CharacterDataDiscoverySnapshot` | Harmony postfix on `CharacterData.OnAfterDeserialize`. Cache keyed on `characterGuid` (read directly via `__instance.characterGuid` field-access — the sandbox-safe path after several banned alternatives). Active-char resolution piggybacks on `SaveManagerActiveSelectHook.AwaitingActiveDeserialize`. |

Lifecycle:
- `IMod.EarlyInit` loads the CoreLib `UserInterfaceModule` **and**
  `ControlMappingModule` submodules — the latter registers the F1 keybind
  (polled in `IMod.Update` to open the window).
- `IMod.ModObjectLoaded` registers the window prefab via
  `UserInterfaceModule.RegisterModUI`.
- `IMod.Init` subscribes the loc-change hook; **no Bake-call here** (too
  early — `PugDatabase.objectsByType` is null).
- `PlayerController.OnOccupied` (D2 anchor) Harmony-postfix kicks the
  bake coroutine. `Manager.main.player` is non-null at this point;
  earlier anchors (`PugDatabase.UpdateEntityMonos`, `SaveManager.SetWorldId`,
  `IMod.Init`) all produce NRE. The bake call is launched via
  `__instance.StartCoroutine`; the coroutine does
  `WaitUntil(() => ClientWorldStateSystem.HasRunAtLeastOnce)` before calling
  `ItemCatalog.Bake()`. Never call Bake synchronously inside the postfix —
  that races ECS world readiness.
- `LocalizationManager.OnLocalizeEvent` re-bakes synchronously and
  triggers `ItemChecklistWindow.Instance.RebindRows()`.
- `ItemChecklistWindow.Awake` subscribes `DiscoveredState.Changed`
  for live title-quote refresh.

## CK Decompile References (relevant for ItemChecklist)

Re-derivation via ILSpy: `~/.dotnet/tools/ilspycmd -t <Type> <DLL>`.
DLLs are in CK's installation: `…/Core Keeper/CoreKeeper_Data/Managed/`.

| Type | DLL | Key facts |
|---|---|---|
| `CookedFoodCD` | `Pug.ECS.Components.dll` | `GetFoodVariation(p, s) = (primary << 16) \| secondary`, symmetric via deterministic `FirstIngredientIsPrimary` tiebreaker (Seed 87931). `rareVersion`/`epicVersion` fields point to tier-variant ObjectIDs. |
| `CookingIngredientCD` | `Pug.ECS.Components.dll` | Discriminator for ingredients. Fields `turnsIntoFood: ObjectID` (default family per ingredient) + `ingredientType: IngredientType`. |
| `IngredientType` (enum) | `Pug.Base.dll` | `{None, Plant, Fish, Meat}` — 4 values, 3 nutzbar. |
| `ObjectIDExtensions.IsCookedFood` | `Pug.Base.dll` | Range check `objectID ∈ [9500, 9599]` (max 100 family-item slots). |
| `ObjectIDExtensions.IsGoldenPlant` | `Pug.Base.dll` | Range `[8100, 8149]`. Used in `GetPrimaryIngredient` tiebreaker. |
| `InventoryUtility` (~line 1626) | `Pug.Other.dll` | Pick-family logic in non-Burst form: `family = ingredientLookup[primaryPrefabEntity].turnsIntoFood`. The Burst-compiled `ConvertCookedFoodsSystem` does the same. |
| `CookBookUI` | `Pug.Other.dll` | CK's own cooked-food browser. **Not** a viewport recycler: `ItemSlotsUIContainer.InstantiateItemSlots` builds a *fixed* pool of `MAX_ROWS × MAX_COLUMNS` slots (CookBook: 50×5=250) once, and `UpdateFilter` **breaks at `num >= itemSlots.Count`** — entries past slot 250 are never shown; it scrolls by translating the whole pool under the clip mask, recycling nothing. Fine for ≤250 recipes, unusable for ItemChecklist's ~10720 entries. True viewport virtualization (Iter-3.8) is mod-built on `IScrollable.UpdateContainingElements`; CK ships no recycler template. |
| `I2.Loc.LocalizationManager.OnLocalizeEvent` | `I2.Loc.dll` | Public static `event Action` (no params). Fires after language change via `DoLocalizeAll()` / `Coroutine_LocalizeAll()`. Sandbox-safe (public static event on trusted I2.dll). Fallback if banned: poll `LocalizationManager.CurrentLanguage` in a `ManagedUpdate` postfix (ItemBrowser-proven pattern — compare against cached value, trigger on change). |
| `PlayerController.GetObjectName` | `Pug.Other.dll` | `GetObjectName(buf, localize: bool)` — second param is `bool localize`. Passing `false` yields the raw I2 term path (e.g. `"Items/LargeWaterCan"`), not the display name. IB pattern (ObjectUtility.cs:97–108): `localizedName = GetObjectName(buf, true).text`; `unlocalizedName = GetObjectName(buf, false).text`; if `fields.dontLocalize` → use unlocalizedName as fallback. |
| `UIScrollWindow.SetScrollValue` | `Pug.Other.dll` | `SetScrollValue(float normalizedScrollValue)`: `1f` → top of list (lerp → minScrollPos=0, content.localY=0); `0f` → bottom (content shifted up by full height). Use `scrollWindow.ResetScroll()` for "go to top" — equivalent to `SetScrollValue(1f)`. Post-content-spawn sequence (IB EntriesList.SetEntries pattern): set scrollable via `API.Reflection.SetValue`, invoke `UpdateScrollHeight` via `API.Reflection.Invoke`, then call `SetScrollValue(1f)`. Full internals in `docs/architecture.md § UIScrollWindow Reference`. |
| `ScrollBar` / `ScrollBarHandle` | `Pug.Other.dll` | Native scrollbar (Iter-5, prefab-wired). `ScrollBar` fields: `scrollWindow`, `root` (a **child** GO it toggles — not the component's own GO, else it can self-deactivate before `ScrollHeight` is set), `background` (track `SpriteRenderer`), `handle` (`ScrollBarHandle`). `ScrollBarHandle : ButtonUIElement` fields: `handleSpriteRenderer`, `handleCollider` (**3D `BoxCollider`** `!u!65`, not `!u!61` — CK `UIMouse` raycasts in 3D), `handleSpritesToResize`. **No mod C#:** `UIScrollWindow.LateUpdate → UpdateScrollbar → ScrollBar.UpdateScrollBarPosition` does sizing + position + mouse-wheel sync once `UIScrollWindow.scrollBar` is wired (verified against Item Browser, which ships no scrollbar C#). Handle drag = `ScrollBarHandle.onLeftClick` UnityEvent → `ScrollBar.OnHandleLeftClick` (`m_TargetAssemblyTypeName: ScrollBar, Pug.Other`, `m_Mode: 1`). Handle size ∝ `VisibleRatio`, min 0.625. **`ButtonUIElement.LateUpdate` toggles GO activity** of `spritesShownUnpressed` (active when `!leftClickIsHeldDown`) and `spritesShownPressed` (active when held) — a GO in **both** lists ends up visible only while held; with one handle sprite keep both lists empty and let `handleSpriteRenderer` be the always-on handle, with the selected-border as `optionalSelectedMarker`. Renderers need `maskInteraction: None`. See `docs/architecture.md § Scrollbar (Iter-5)` and the `project-corekeeper-script-fileid-derivation` memory. |
| `CoreLib UserInterfaceModule` | `CoreLib.UserInterface.dll` | Version 4.0.4 (stable Feb–May 2026). `LoadSubmodule` in `EarlyInit`; `RegisterModUI(GameObject)` in `ModObjectLoaded`. UI class must extend `UIelement` AND implement `IModUI`. Mount: auto into `UIManager.chestInventoryUI.transform.parent`. Open: `UserInterfaceModule.OpenModUI("ItemChecklist:Window")`. Auto-hide on vanilla `HideAllInventoryAndCraftingUI`; zero patches needed for cursor/WASD-block/Escape. `Awake()` must call `HideUI()`. **Iter-4 F1 toggle + menu-exclusion:** `OpenModUI` is not toggle-capable, so the mod toggles itself — close via `Manager.ui.HideAllInventoryAndCraftingUI(forceClose: false)` (mirrors `PlayerController.CloseAnyOpenInventory`; CoreLib's postfix clears `currentInterface`), open-state read from `Instance.Root.activeSelf` not `currentInterface`. Guard with `Manager.ui.isPlayerInventoryShowing` (per-UI `isShowing` getters on `UIManager` in `Pug.Other.dll` are **unpatched** — CoreLib only patches the aggregate `isAnyInventoryShowing`). `InventoryOpenAutoHidePatch` postfixes `UIManager.OnPlayerInventoryOpen` — the single funnel every Vanilla menu open routes through — with a **bare** `HideUI()` to enforce no-overlap; coherent only while `ShowWithPlayerInventory == false`. Background: `Manager.ui.GetCraftingUITheme(UIManager.CraftingUIThemeType.Wood).background` (enum param, not string → 9-slice wood frame, zero custom art). `BoxCollider2D` required on root. Production refs: limoka/BookMod (~145 IMod LoC + ~162 UI LoC), limoka/DummyMod (~87+84). |
| `UIManager.GetSlotBorderRarityColor` / `ObjectInfo.rarity` / `enum Rarity` | `Pug.Other.dll` / `Pug.Base.dll` | Iter-6 rarity colouring. `enum Rarity { Poor=-1, Common, Uncommon, Rare, Epic, Legendary }`; `ObjectInfo.rarity` is a plain `public Rarity rarity` field. `Color GetSlotBorderRarityColor(Rarity rarity, bool useDefaultColorForCommon, Color defaultColor)` returns `defaultColor` when `useDefaultColorForCommon && (rarity == Common \|\| rarity == Poor)`, else `Manager.ui.slotBorderRarityColors[(int)(rarity + 1)]` (a `List<Color>`). `Manager.ui.*` is sandbox-safe (already used in this mod). `PugText.color` setter = `SetTempColor(value)` (paints the glyph SpriteRenderers `Render()` rebuilds → set after Render; pass `keepColorOnStart: true` to survive `renderOnStart`). See `docs/architecture.md § Rarity Colouring (Iter-6)` + `docs/gotchas.md § PugText tint`. |
| `ButtonUIElement` | `Pug.Other.dll` | Iter-7 dropdown/toggle buttons. Clickable widget base class. Override `public override void OnLeftClicked(bool mod1, bool mod2)` — **guard `if (!canBeClicked) return;` FIRST, then call `base.OnLeftClicked(mod1, mod2)`** (the uniform ItemChecklist convention across `DropdownOptionButton`/`DropdownToggleButton`/`AscDescToggle`/`ClearSearchButton`; when not clickable, base is not run). Requires a **3D `BoxCollider`** (`!u!65`) — CK `UIMouse` raycasts in 3D. Leave `spritesShownUnpressed` and `spritesShownPressed` empty so `ButtonUIElement.LateUpdate` doesn't toggle GO activity and hide the button's SpriteRenderer (same rule as `ScrollBarHandle`). |
| `TextInputField` / `CharacterMarkBlinker` | `Pug.Other.dll` | Iter-8 search field — **CK-native text input, NOT uGUI**. `TextInputField : UIelement, InputManager.TextInputInterface` renders via `pugText`/`hintText` (PugText), caret via `characterMarkBlinker` (a `CharacterMarkBlinker` whose single serialized field `sr` is the caret SpriteRenderer); `OnLeftClicked` self-activates (`Manager.input.SetActiveInputField(this)`). Subclass it (`SearchBar`) + poll `GetInputText()` in `LateUpdate`. Key serialized fields: `maxWidth`; **`trim` — keep `0`** (else leading/trailing spaces are stripped per keystroke, breaking multi-word search); **`dontDeactivateOnDeselect` — set `true`** so the field stays focused when the mouse leaves its collider (CK selection is hover-based), then call `Deactivate(false)` on window close or WASD stays blocked. uGUI `InputField` is the wrong abstraction (`UnityInputFieldAdapter` deleted). See `docs/architecture.md § Filter & Search (Iter-8)`. |

Iter-3.7's α-algorithm derives directly from `InventoryUtility.cs:~1626`:
for any ingredient pair `(i1, i2)`, the resulting family is
`CookedFoodCD.GetPrimaryIngredient(i1, i2).turnsIntoFood`, and the
variation is `CookedFoodCD.GetFoodVariation(i1, i2)`. Tiers (Base/Rare/
Epic) are looked up via `tierMap[baseFamily]` from `CookedFoodCD`.

## Mod-Specific Gotchas

- **No unit-test framework** — "testing" is build (`utils/build.sh`) +
  in-game smoke-test via Player.log grep + manual UI verification.
  Every Iter ends with a multi-point acceptance test list. See
  `docs/conventions.md § Testing Conventions` for the canonical 7-phase
  pattern.
- **PugText pixel-font U+2014 (em-dash) renders as U+002D (hyphen).**
  Cosmetic only — title format `"Item Checklist — N / M"` shows as
  `"… - N / M"` in-game.
- **`Manager.saves.*` property-access is sandbox-banned**, but
  field-access on serialized struct-fields (e.g.
  `__instance.characterGuid`, `objectData.variation`) is OK. This
  unlocks the character-GUID cache key.
- **PugText pool-leak:** spawned rows must `Clear()` their `PugText`
  children, or the shared pool leaks and main-menu PugTexts go blank. As
  of Iter-3.8 rows live in a persistent pool (no per-close Destroy), so
  the `Clear()` moved to `ItemChecklistContent.OnDestroy` (teardown only)
  and the blanking symptom can no longer occur. Detail in
  `docs/architecture.md § Viewport Virtualization`.
- **`ex.GetType().Name` inside a catch block is sandbox-banned.** `Type.Name`
  resolves to `MemberInfo.get_Name()` which is blocked by Roslyn's code-security
  verification. Symptom: `Illegal Member References = '1'`, `CompileFailed`.
  Fix: use typed catches (`catch (NullReferenceException ex)`) or log only
  `ex.Message` (sandbox-safe). Never call any `.Name` property on a reflected
  type in mod code.
- **uGUI (Canvas/Image) structurally fails in CK** — no `Collider`, so
  CK's `Physics.Raycast`-based `UIMouse` never sees it. Use
  `SpriteRenderer` + Layer 5 + `UIelement` (all 10 surveyed CK UI mods do).
  Full explanation: `docs/gotchas.md § uGUI structurally fails in CK`.
- **`PugMod.MemberInfo` vs `System.Reflection.MemberInfo` conflict.** Adding
  `using System.Reflection;` in any mod file causes `CS0104` ambiguity because
  `PugMod` also exports a `MemberInfo`. Solution: never add
  `using System.Reflection;` in mod code. Use `API.Reflection.SetValue` /
  `API.Reflection.Invoke` wrappers directly — they accept `PugMod.MemberInfo`.

## UI Clipping Pattern

SpriteMask + Custom Sorting-Layer Range (`"GUI"` layer, range `40..55`, all
renderers + PugText `style.sortingLayer` forced to `"GUI"`, mask sprite
`spritePixelsToUnits: 1`). Full working recipe (Iter-3.5c) and the aborted
Iter-3.5b lessons: `docs/gotchas.md § SpriteMask Clipping`.

## Iter-Roadmap (live)

As of 2026-06-04: Iter-3.5 through 8 (incl. the 3.x/7.1 point-iters) are DONE on main; Iter-9 is DONE on branch `iter-9` (pending ff-merge). Iter-3.8
replaced the per-entry SpawnRows (one GameObject per ~10718 catalog
entries, ~905 ms open freeze) with viewport virtualization: a fixed ~5-row
pool recycled from `IScrollable.UpdateContainingElements`, reporting the
full catalog height via `GetCurrentWindowHeight`. Open latency dropped to
~0-7 ms. Invariant from the geometry fix: `UIScrollWindow.windowHeight`
must equal the SpriteMask height (and the mask top align to row 0's top)
for the first/last rows to sit flush. **Iter-4 (DONE):** F1 is now a real toggle (one key opens and closes), and
the checklist is mutually exclusive with CK's inventory/crafting UI — opening
a Vanilla menu auto-hides it, and F1 won't open it over an open menu. See the
`CoreLib UserInterfaceModule` row above and `docs/architecture.md § UI
Architecture` for the mechanism. **Iter-5 (DONE):** a working, draggable
scrollbar is wired into the window prefab using CK's native `ScrollBar` +
`ScrollBarHandle`, with the Item-Browser bridge scrollbar sprites (track +
handle + selected-border, sub-sprites of the `ui_classic` atlas). **Pure
prefab change — zero C#:** once `UIScrollWindow.scrollBar` references the
`ScrollBar`, CK's `UIScrollWindow.LateUpdate → UpdateScrollbar →
ScrollBar.UpdateScrollBarPosition` drives handle sizing, position, and
mouse-wheel sync itself (verified against Item Browser, which has no scrollbar
C#). Scroll arrows stay unwired (`fileID: 0`); track-position fine-tuning and
real sprites fold into Iter-12 (pixel-art). Two non-obvious facts proven during the build:
the scrollbar SpriteRenderers must use **`maskInteraction: None`** to stay
unclipped by the row SpriteMask (orders 46/47 sit inside the 40..55 mask
range), and `ButtonUIElement.LateUpdate` toggles **GameObject activity** of
`spritesShownUnpressed`/`spritesShownPressed` each frame — a GO must never be
in both lists, and with a single handle sprite both lists stay **empty** so
`handleSpriteRenderer` (rendered by `ScrollBar` itself) is the always-visible
handle, with the selected-border wired only as `optionalSelectedMarker`
(hover/selection highlight). Script-ref rule for hand-wired CK components:
`m_Script.fileID` is a portable class-name MD4 hash, but the `guid` is this
install's `Pug.Other.dll.meta` guid — see the
`project-corekeeper-script-fileid-derivation` memory. **Iter-6 (DONE):**
item rarity colouring — each row's CK rarity (`ObjectInfo.rarity`) shows as a
name tint (all rarities; Common/Poor keep the default text colour, Uncommon+
get their rarity colour) **and** a rarity border around the icon for Uncommon+
(Common/Poor get no border, matching CK's `GetSlotBorderRarityColor`
`useDefaultColorForCommon` grouping). Applies to undiscovered `???` rows too.
`Rarity` baked into `ItemCatalog.Entry` (via a `rarityCache` mirroring
`iconCache`); colour resolved at rebind in `ItemChecklistContent` via
`Manager.ui.GetSlotBorderRarityColor(rarity, useDefaultColorForCommon: true,
defaultColor)`; `ItemRow.Bind` paints it. **Distinct axis** from the Iter-3.7
cooked-food tiers (`CookedFoodCD.rareVersion`/`epicVersion`). Two non-obvious
facts proven in-game (see `docs/gotchas.md`): the label tint must use
`SetTempColor(c, keepColorOnStart: true)` after `Render()` or it blanks on the
first open (PugText `renderOnStart`), and the shipped `ui_rarity_border.png`
placeholder was fully transparent (fixed to a white hollow frame, rendered as a
9-slice via `spriteBorder {1,1,1,1}` so the ring stays thin; real pixel-art
border remains **Iter-12** (pixel-art) polish).
Full mechanism in `docs/architecture.md § Rarity Colouring (Iter-6)`. **Iter-7
(DONE):** runtime-switchable list sorting — four modes (Name, Rarity, Found,
Category/ObjectType), each with ascending/descending direction. A reusable
`DropdownWidget : UIelement` shows the active sort mode in a header and lists
the remaining modes in a popup; an `AscDescToggle : ButtonUIElement` flips
direction. Sort state is static per session (resets on game restart). The view
model `ItemListViewModel` owns the `int[] Order` indirection (display position →
catalog index), runs `Recompute()` on three triggers (mode/direction change,
`DiscoveredState.Changed` only when Mode=Found, and re-bake), and keeps the
filter/search seam (`DiscoveryFilter`, `SearchText`) at no-op defaults for
Iter-8. `ItemCatalog.Entry` gained `ObjectType ObjectType` (via a new
`objectTypeCache`) for the Category comparator. `ItemChecklistMod.ListView` is
(re-)constructed after each bake. Full mechanism in `docs/architecture.md §
List View-Model & Sorting (Iter-7)`. **Iter-7.1 (DONE):** catalog-completeness
fix — `ItemCatalog.Bake` Loop 1 blanket-excluded `ObjectType.NonUsable` as
"garbage", but CK files raw materials (ores, bars, raw wood, scrap) under that
type, so they were silently missing. Replaced with a narrow guard that drops a
`NonUsable` item only when it has no icon (`smallIcon` and `icon` both null);
verified in-game (1.2.1.4) that the 126 `NonUsable` items are 117 real
materials (all have an icon) + 9 internal engine entities with no icon and no
localized name (territory spawners, `TheCore`, the `DroppedItem` entity,
boss-statue stubs). Catalog 10844 → 10835. IB's full `IsNonObtainable` can't be
reused (needs ECS/registry APIs the sandbox blocks). Full reasoning in
`docs/gotchas.md § Catalog / Bake (Iter-7.1)`. **Iter-8 (DONE):**
runtime discovery filter (All/Discovered/Undiscovered) + name search, wired to
the `ItemListViewModel` filter/search seam Iter-7 left at no-op defaults. Filter
= a second `DropdownWidget` instance (the `SortDropdown` subtree duplicated via a
deterministic fileID slot-remap, `ui_icon_filter` glyph). Search =
`SearchBar : TextInputField` (CK-native — PugText/caret/focus inherited; **not**
uGUI, the orphaned `UnityInputFieldAdapter` was deleted) + a `ClearSearchButton`,
in a 9-slice `Display` slot matching the dropdowns. **Option A** semantics:
search matches the *real* name of all items; undiscovered matches still render
`???`. One-line ViewModel change (dropped the discovered-only guard) +
`IsFiltered`-driven title `· N shown`. Focus persists while typing
(`dontDeactivateOnDeselect = true` + `HideUI` deactivates to free WASD on close).
Hard-won prefab traps (dead default material → invisible, Default-vs-GUI sorting
layer, caret PPU scale, duplicate-and-strip leftover button hijacks clicks) in
`docs/gotchas.md § Search Field / Header (Iter-8)`; full mechanism in
`docs/architecture.md § Filter & Search (Iter-8)`. **Iter-9 (Polish) — DONE (2026-06-04, branch `iter-9`).** A large UI
layout/behaviour pass:
- **Window + suppression:** near-fullscreen window, thin uniform 0.25u border
  (matching CK's inventory margin); **fixed** size — CK's orthographic UI camera
  shows a constant world area (`orthographicSize 8.4375`, 16:9 -> 30x16.875u) with
  no UI-scale option, so a fixed size is correct on every resolution. Help panel
  (`ShortCutsWindow`, toggled by **S**) + "?" prompt suppressed while open via two
  auto-discovered Harmony patches (the `ShortCutsWindow.LateUpdate` **prefix** is
  load-bearing -- `ShortcutsCanBeToggled` only gates the prompt, not the S keybind);
  HUD hidden via `Manager.ui.TemporarilyDisableGameplayUI()` (never
  `Manager.prefs.hideInGameUI`, which persists to disk) + an
  `InGameButtonHintsUI.LateUpdate` prefix; cursor restored via a `UIMouse.LateUpdate`
  postfix; ESC->pause race fixed by forcing `MenuManager.IsPauseDisabled` while open.
- **Header:** Sort/Filter dropdowns sized to the widest entry, right-aligned cluster
  flush to the scrollbar (x 14.2); popups widened to the full dropdown footprint;
  field heights 0.7 (bottom-fixed); square 0.7x0.7 toggle/ascdesc buttons; search
  field to 1/3 width with a full-field controller-focus highlight (the
  `TextInputField.selectedMarker`); footer split (counter right / "N shown" left);
  first-open input-leak fixed (ASCII "..." search hint avoided a thinTiny word-wrap
  crash that aborted ShowUI before CoreLib set currentInterface); the clear button
  at an off-grid x (4.005) to dodge the on-grid point-filter distortion (see
  `docs/gotchas.md` + the `project-corekeeper-sprite-ongrid-distortion` memory) and
  pulled forward in Z (`m_Center.z`) so its click wins the `UIMouse` raycast over
  the field collider.
- **Margins:** window side margins reduced + equalised, content symmetric +/-14.2.
- **Item rows:** height 2.5->1.5; **`RowHeight` read from the row prefab's
  background** at `Init` (single source of truth -- change the bg `m_Size.y` alone
  and the list re-spaces); **checklist-style checkbox** (empty box on every row +
  `ui_icon_requirement` inside when discovered); viewport pool buffer +2->+4;
  **RowHeight-independent flush** (row 0's TOP pinned to a fixed `MaskTopLocalY`,
  each row centre offset by `RowHeight/2`) so list start/end stay flush at any row
  height; row background ends at the scrollbar's left edge (no overlap). Mechanism
  in `docs/architecture.md` / `docs/gotchas.md`.

### Future roadmap (frozen 2026-06-04)
- **Iter-10 -- re-evaluate Sort & Filter settings.** Are the current sort modes
  (Name/Rarity/Found/Category) and filters (All/Discovered/Undiscovered) sensible,
  and should other options be added? Finalise the option set *before* Iter-11
  (localisation) so the labels are stable.
- **Iter-11 -- localisation.** Translatable "N shown", keybind display name, search
  hint + sort/filter labels via CoreLib `LocalizationModule`.
- **Iter-12 -- real pixel-art sprites.** Replace the placeholder rarity border
  (white 9-slice hollow frame) + scrollbar track/handle sprites.
- **Iter-13 -- `DropdownWidget` prefab extraction.** Extract the widget into a
  standalone/nested prefab for true reuse.
- **Iter-14 -- code refactor / optimisations + search caret vertical alignment.**
  General C# cleanup; plus the search caret sits a few px too low --
  `TextInputField` forces `characterMarkBlinker.transform.position =
  pugText.position` every frame, so the fix is to move the white_pixel into a child
  GO with a +Y offset and rewire `CharacterMarkBlinker.sr`.
- **Iter-15 (tentative) -- F1 guard misses loading screen & cutscenes.** The F1
  toggle in `ItemChecklistMod.Update` only blocks opening when a Vanilla
  menu/inventory/text-field/chat is active; it does **not** block during the world
  loading screen or in-game cutscenes, so F1 pops the checklist over both. Fix =
  add the missing state guards (loading: `ClientWorldStateSystem.HasRunAtLeastOnce`
  / `Manager.main.player != null` or a screen-fade flag; cutscenes: a cutscene /
  input-locked flag -- all need ILSpy + sandbox verification). Bugfix follow-up to
  Iter-4's toggle guard.
- **Iter-16 (tentative) -- pet/creature discovery.** The bake blanket-excludes
  `ObjectType.Creature`/`Critter`, so tamed pets/critters never get a row -- same
  bug class as the Iter-7.1 NonUsable fix. IB keeps anything with `PetCD`
  (`ObjectUtility.cs:390`) and craftable non-cattle creatures (`CraftingCD &&
  !CattleCD`, `:393`); a fix would mirror those, still dropping wild mobs.
  `PugDatabase.HasComponent<T>` is sandbox-safe here. Sibling to Iter-7.1.
- **Iter-17 (tentative) -- per-variation/skin tracking.** The bake collapses every
  family to its `variation == 0` entry (`ItemCatalog.cs:130`), so colour/skin/state
  variants never get their own row. CK tracks discovery per `(objectID, variation)`
  and IB exposes `ignoreVariation` (`ObjectUtility.cs:422`); we hardwired "ignore
  variation" to keep a one-tick-per-item checklist. Revisit only with a UI story
  for grouping/expanding variants. Distinct from the Iter-7.1 catalog fix.
See `git log` for canonical per-iter merge points and `docs/superpowers/specs/`
for design docs.

## Conventions

- Documentation files (`README.md`, this `CLAUDE.md`, `docs/`) are
  English. Inline code-comments are mixed (English for class/method
  doc-comments, occasional German in research/spec narrative where
  shorter to express).
- Branch naming: `iter-<n>[.<m>[-letter]]`, e.g. `iter-3-7`, `iter-3-5c`.
  Each iter ends with a ff-merge to main (no squash, per parent global
  convention). See `docs/conventions.md` for full commit-type conventions,
  worktree hygiene, and the canonical per-iter test-phase structure.
- File layout: `docs/conventions.md § File Layout` for the authoritative
  `unity/ItemChecklist/` directory map.
- Build-verify (Editor compile ≠ sandbox pass): after a build, grep
  `Player.log` for the load/compile markers —
  `grep -iE "error CS|Build complete|Install complete|CompileFailed" Player.log`.
  A clean Editor build can still `CompileFailed` in the runtime sandbox.
- Spec lives in `docs/superpowers/specs/`, plan in
  `docs/superpowers/plans/`, exploratory research in `docs/research/`.
  Every iter has a 1:1:1 spec/plan/(optional)research mapping.
