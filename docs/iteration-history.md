# ItemChecklist — Iteration History

Full per-iteration narrative of ItemChecklist's development (Iter-3.5 through
Iter-11), moved out of `CLAUDE.md` to keep that file focused. See `git log` for
canonical per-iter merge points and `docs/superpowers/specs/` for design docs.

As of 2026-06-04: Iter-3.5 through 8 (incl. the 3.x/7.1 point-iters) are DONE on main; Iter-9 (UI layout/behaviour polish) is DONE on main. Iter-3.8
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

**Iter-10 (DONE):** sort & filter redesign — supersedes the Iter-7/8 option set.
Four sort modes: **Name**, **Rarity**, **Level**, **Value** (dropped Found + Category
sorts from Iter-7 — both are now filter dimensions, not sort axes). Level is read
from `LevelCD.level` via `PugDatabase.TryGetComponent<LevelCD>` (else 0) — NOT
`ObjectInfo.level`, which is dead/legacy and read nowhere in the game (confirmed via
decompile + IB `ObjectUtility.GetBaseLevel`). Value is a faithful port of IB's
`ObjectUtility.GetValue` (sell mode) in `ItemCatalog.ComputeSellValue`: `sellValue
== -1` is CK's "auto-compute from rarity + ingredients" marker, NOT "unsellable";
truly unsellable = `CantBeSoldAuthoring` OR rarity Legendary → 0 → rendered `—`.
Filter dimension replaced by `FacetedFilterWidget`: a single "Filter (N)" header
dropdown opening a sectioned popup with four OR-within / AND-across dimensions —
**Discovery** (Discovered/Undiscovered), **Category** (10-bucket taxonomy from
`ObjectType` ranges in `ItemCategory`/`ItemCategories.Of`: Weapons/ArmorAccessories/
Tools/Food/Placeables/Materials/Valuables/KeyItems/Instruments/Other), **Rarity**
(Poor…Epic), **Craftable** (Craftable/Not craftable). `ItemListViewModel` holds four
static `HashSet` dimensions; `Recompute` applies each as a `continue` predicate. A
dedicated "Clear all" action row (drawn from `actionTemplate` — its own pool, no
checkbox) resets all dimensions. Three pools in `FacetedFilterWidget`: checkbox rows,
action rows, section-header PugTexts; `RebuildList` re-syncs all checkbox visuals on
every click. Companion files: `FacetCheckboxButton`, `FacetToggleButton`.
Each row now shows right-aligned **Level** (`Lv N`) and **Value** (sell value)
columns + an **Ancient Coin** glyph (`ObjectID.AncientCoin` icon, loaded once via
`PugDatabase.GetObjectInfo` and shared across all rows). Em-dash `—` for level ≤ 0,
value ≤ 0, and undiscovered rows (spoiler guard). Labels are placeholder-English,
structured for Iter-11 (localisation) routing. Three new `ItemCatalog.Entry` fields
baked: `Level`, `SellValue`, `IsCraftable`.

**Iter-11 (DONE):** native localisation via CK's `TextDataBlock` / `ScriptableData`
mechanism. Term strings live in
`unity/ItemChecklist/Localization/localization.yaml`; the shared Editor helper
`utils/LocalizationGenerator.cs` (namespace `CoreKeeperModUtils`, symlinked by
`link.sh`, gated behind `.envrc:USE_SHARED_EDITOR_HELPERS=1`) reads that YAML and
templates raw `.asset` YAML for each language — **Option II: raw asset templating**
— keyed by `utils/ck-language-addresses.json` (13 runtime languages, address→ISO,
runtime-dumped because `LanguageDataBlock`s are runtime-only and the SDK editor API
cannot enumerate them at build time). At runtime, terms are resolved via
`API.Localization.GetLocalizedTerm` through the `Loc.T` / `Loc.F` helpers, with
the raw term key as fallback. EN + DE shipped; adding a language later = add a YAML
language key and rebuild. The F1 keybind display name uses CK's own
`ControlMapper/ItemChecklist-ToggleChecklistPC` term. Language changes re-bake the
catalog (deferred to the next `Update` tick, guarded on `Manager.main.player !=
null`). This mod is the **pilot** for the shared `utils/` editor helpers
(`CLIBuildHelper`, `CLIPublishHelper`, `LocalizationGenerator`); `disable-durability`
and `faster-talents` still use per-mod helpers and migrate later. Hard-won findings:
`LanguageDataBlock` is runtime-only (no SDK API at build time → Option II), the
`m_Script.guid` for `ScriptableData.dll` is per-SDK-clone-local and must be resolved
via `AssetDatabase.AssetPathToGUID` at generation time (not copied from IB), and
`PugFont` crashes on labels exceeding `maxWidth > 0f` with longer translations →
set `PugText.maxWidth = 0f` on all localised single-line labels. Full details in
`docs/gotchas.md § Localisation (Iter-11)`.

**Iter-11.5 (DONE):** always-on HUD discovery counter — the window footer's
`N / M (p.p%)` mirrored as a permanent top-right HUD readout (above the minimap),
with a checkbox-framed icon (`ui_slot_toggled_border` box + `ui_icon_requirement`
tick at 0.7 scale, like a discovered list row). This is the mod's first
**non-modal** UI: a dedicated `ItemChecklistHud : UIelement` in its own
`Prefabs/ItemChecklistHUD.prefab`, instantiated directly by `ItemChecklistMod`
(routed by GameObject name in `ModObjectLoaded`, **not** via CoreLib
`RegisterModUI`) and parented under `chestInventoryUI.transform.parent`
(`IngameUI`). The counter string comes from a new shared `ProgressFormat.Counter`
helper that the window footer (`FormatTitle`) also adopts — one source of truth,
no drift. Live refresh via `DiscoveredState.Changed` plus both bake hooks
(world-load + loc-change). Three hard-won in-game findings (see
`docs/gotchas.md § HUD Counter (Iter-11.5)`): **(1)** the renderers must sit on
the **HUD Unity layer (27)** — on layer 5 (UI) the uiCamera never draws them during
plain gameplay (the modal window only renders because CoreLib's open-path activates
it); **(2)** content must sit at local **z=10** (world z≈0), the plane CoreLib
positions modal UIs to via `initialInterfacePosition` — at the parent origin
(world z=-10) it is outside the uiCamera frustum (`SpriteRenderer.isVisible ==
false`); **(3)** `Manager.ui.CalcGameplayUITargetScaleMultiplier()` (CK's "native
HUD idiom") returns `(0,0,0)` for a mod HUD here, so visibility is explicit:
`isInGame && Manager.main.player != null` (the player term suppresses the
world-load screen — the Iter-15 bug class) `&& !Manager.ui.isAnyInventoryShowing
&& !Manager.menu.IsAnyMenuActive()` (the CoreLib-patched aggregate
`isAnyInventoryShowing` covers inventory, crafting **and** the checklist window).
Bonus: HUD-layer membership means CK's own `CameraManager.ShowHUD(false)` culls the
counter together with the rest of the gameplay HUD, for free. Full mechanism in
`docs/architecture.md § HUD Counter (Iter-11.5)`.
