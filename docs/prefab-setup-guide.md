# Prefab Setup Guide

Step-by-step instructions for building `ItemChecklistWindow.prefab` and
`ItemRow.prefab` inside the Unity Editor. The C# code is already pivoted
to load these via `AssetBundle.LoadAsset<GameObject>` — the runtime UI is
broken until both prefabs exist in the bundle.

**Prerequisite:** Core Keeper must be closed before launching the Unity
Editor (Editor locks the project; the batchmode `build.sh` also locks it,
so the two cannot run simultaneously).

## 1. Open the SDK project and link the mod

```bash
# In a terminal (project root): re-create symlinks so the SDK sees our
# mod folder. build.sh does this automatically; for manual Editor work
# you need to run it once.
cd item-checklist
source .envrc
../utils/link.sh
```

Then open Unity Hub → **Add Project** → point at
`/Users/valgard/Projects/private/core_keeper/CoreKeeperModSDK` → open
with Unity Editor `6000.0.59f2`.

In the Project window, navigate to `Assets/ItemChecklist/`. You should
see our `Art/`, `Editor/`, `Scripts/` (i.e. `*.cs`), and `ui/` folders
symlinked in.

## 2. Verify the atlas sub-sprites are visible

In Project → `Assets/ItemChecklist/Art/Bridge/ui_classic.png` →
**expand the arrow** next to the asset. You should see 12 sub-sprites:
`ui_panel`, `ui_tab`, `ui_slot_background`, `ui_slot_border`,
`ui_slot_selected_border`, `ui_slot_toggled_border`,
`ui_scrollbar_handle`, `ui_scrollbar_background`,
`ui_scrollbar_selected_border`, `ui_slot_favorited_border`,
`ui_slot_highlight_border_0`, `ui_slot_highlight_border_1`.

If you only see the parent texture: the `.meta` is single-sprite. Stop
and re-run the meta swap from Phase 0 before continuing.

## 3. Create `ItemRow.prefab` (the recycled row template)

The row prefab is recycled by `VirtualScrollList` — only `Bind()` mutates
its appearance at runtime.

1. In Project → `Assets/ItemChecklist/` → right-click → **Create →
   Folder → "Prefabs"**.
2. Create a temporary GameObject in the scene to design the row:
   - Hierarchy → right-click → **UI → Image** (creates a Canvas + Image
     under it; we'll use just the Image)
   - Rename the Image to `ItemRow`.
   - On `ItemRow`'s `RectTransform`: width 400, height 40 (matches
     `ItemRowView.RowHeight = 40f`)
   - Image component: leave sprite empty for now (the row itself is
     transparent — visual is provided by children)
3. Add children under `ItemRow`:

   | Name             | Component             | Sprite                                  | Position / Size                                |
   |------------------|-----------------------|-----------------------------------------|------------------------------------------------|
   | Checkbox         | Image                 | `ui_slot_background` (Type: Sliced)     | Anchored left-middle, 20×20, offset (8, 0)     |
   | Icon             | Image                 | (none — assigned per bind)              | Anchored left-middle, 32×32, offset (36, 0)    |
   | Icon/Placeholder | Text                  | (n/a)                                   | Anchored full-stretch inside Icon              |
   | Label            | Text                  | (n/a)                                   | Anchored left-stretch, offset (76, 0) to (-8,0)|

   Text settings (Placeholder + Label):
   - Font: `LegacyRuntime` (the Unity built-in legacy font)
   - Placeholder: text `?`, size 20, center-middle alignment, color
     grey `(180, 180, 180)`, initially disabled (uncheck the component
     checkbox)
   - Label: size 16, left-middle alignment, color white

4. With `ItemRow` selected: **Add Component → Item Row View** (this is
   our `ItemRowView` MonoBehaviour).
5. In the Inspector, drag the children into the matching `[SerializeField]`
   slots:
   - `Checkbox Image` ← Checkbox GameObject's Image
   - `Icon Image` ← Icon GameObject's Image
   - `Icon Placeholder Text` ← Placeholder GameObject's Text
   - `Label` ← Label GameObject's Text
6. **Drag `ItemRow` from Hierarchy → `Assets/ItemChecklist/Prefabs/`**.
   This makes it a Prefab Asset and converts the Hierarchy instance to a
   blue Prefab Instance.
7. Delete the now-unneeded Canvas + EventSystem from the temporary
   scene; we only needed them to host the design preview.

## 4. Create `ItemChecklistWindow.prefab` (the main window)

**Hierarchy goal:**

```
ItemChecklistWindow      [RectTransform + ItemChecklistWindowView (UIelement subclass)]
└─ Window                [RectTransform + Image(sprite=ui_panel, Sliced)]
   ├─ Header             [RectTransform + Image(sprite=ui_tab, Sliced)]
   │   ├─ Title          [PugText  "Item Checklist"]
   │   └─ CloseButton    [Button + Image]
   ├─ SearchField        [InputField + Image(sprite=ui_slot_background, Sliced)]
   ├─ FilterDropdown     [Dropdown + Image(sprite=ui_slot_background, Sliced)]
   ├─ ScrollView         [ScrollRect]
   │   ├─ Viewport       [Image + RectMask2D]
   │   │   └─ Content    [VerticalLayoutGroup + ContentSizeFitter]
   │   └─ Scrollbar      [Scrollbar Vertical]
   └─ CounterLabel       [PugText]
```

### 4.1 Scene scaffold

The window root does **not** have its own Canvas. At runtime we parent
it under `API.Rendering.UICamera.transform`, which provides:
- CK's UI Canvas + Layer-Sortierung (cursor stays on top automatically)
- Modal-UI registration via UIelement (gameplay input gets blocked when shown)

That removes the need for our own Canvas, CanvasScaler, GraphicRaycaster
or EventSystem.

1. In **Hierarchy**: right-click in empty area → `UI → Image`. This
   creates `Canvas` + child `Image` + `EventSystem`. We'll dismantle
   most of this.
2. **Rename `Canvas`** to `ItemChecklistWindow` (single click + F2, or
   double-click the name).
3. Select `ItemChecklistWindow`, in Inspector:
   - **Remove `Canvas` component** (⋮ → Remove Component)
   - **Remove `Canvas Scaler` component**
   - **Remove `Graphic Raycaster` component**
   - The RectTransform cannot be removed and stays.
4. **Rename the auto-created `Image` child** (currently selected) to
   `Window`. This is now our main visible panel.

You can also leave the temporary `EventSystem` in the scene for now —
it's only there for the scene preview, won't be part of the saved
prefab.

### 4.2 `Window` panel

The window is **modal-style, full-screen with margin**: stretches over
the entire UI surface with a fixed pixel padding on all sides. The
content blocks the play view fully — intentional, because the
checklist is meant to take over while open, not be a side-panel.
(Live-tracking the progress counter without opening the modal is a
separate, future `LiveTrackerBar` prefab — see Phase 5 stub.)

Select `Window`, set in Inspector:

| Component       | Setting           | Value                                 |
|-----------------|-------------------|---------------------------------------|
| Rect Transform  | Anchors Min       | X 0, Y 0                              |
| Rect Transform  | Anchors Max       | X 1, Y 1                              |
| Rect Transform  | Pivot             | X 0.5, Y 0.5                          |
| Rect Transform  | Left / Right      | 80 / 80                               |
| Rect Transform  | Top / Bottom      | 80 / 80                               |
| Image           | Source Image      | `ui_panel` (sub-sprite of `ui_classic`)|
| Image           | Image Type        | `Sliced`                              |
| Image           | Fill Center       | ✓ (checked)                           |

Inspector shows `Left / Top / Right / Bottom` (instead of `Pos X /
Width`) once both anchor axes are stretched.

This scales resolution-independent: on 1920×1080 you get a 1760×920
effective window, on 4K (3840×2160) a 3680×2000 window — always 80 px
margin around. The 9-slice borders stay pixel-crisp, only the dark
center fills more.

### 4.3 `Header` (title bar)

Right-click `Window` → `UI → Image`. Rename to `Header`. Settings:

| Component       | Setting           | Value                                 |
|-----------------|-------------------|---------------------------------------|
| Rect Transform  | Anchors Min       | X 0, Y 1                              |
| Rect Transform  | Anchors Max       | X 1, Y 1                              |
| Rect Transform  | Pivot             | X 0.5, Y 1                            |
| Rect Transform  | Pos X / Y         | 0 / 0                                 |
| Rect Transform  | Width / Height    | (Width from stretch) / 40             |
| Image           | Source Image      | `ui_tab`                              |
| Image           | Image Type        | `Sliced`                              |

#### 4.3.1 `Title` text

Right-click `Header` → `UI → Legacy → Text`. Rename to `Title`. Then on
the new GameObject:
1. Remove the auto-added `Text` component (⋮ → Remove Component)
2. `Add Component → Pug Text`
3. PugText settings:
   - Text String: `Item Checklist`
   - Style → Font Face: `Bold Large`
   - Style → Horizontal Alignment: `Center`
   - Style → Vertical Alignment: `Center`
   - Style → Color: white
4. Rect Transform: Anchors stretch (Min 0/0, Max 1/1), Left/Top/Right/Bottom = 0/0/40/0 (leave 40px on the right for the close button)

#### 4.3.2 `CloseButton`

Right-click `Header` → `UI → Legacy → Button`. Rename to `CloseButton`.

| Setting         | Value                                  |
|-----------------|----------------------------------------|
| Anchors Min     | X 1, Y 0.5                             |
| Anchors Max     | X 1, Y 0.5                             |
| Pivot           | X 1, Y 0.5                             |
| Pos X / Y       | -8 / 0                                 |
| Width / Height  | 32 / 32                                |
| Image source    | `ui_slot_background` (or leave for now)|

Delete the `Text` child the Button-template auto-creates (we just need
the click-target; the close icon can be added later).

### 4.4 `SearchField`

Right-click `Window` → `UI → Legacy → Input Field`. Rename to
`SearchField`.

| Component       | Setting           | Value                                 |
|-----------------|-------------------|---------------------------------------|
| Rect Transform  | Anchors Min       | X 0, Y 1                              |
| Rect Transform  | Anchors Max       | X 0.65, Y 1                           |
| Rect Transform  | Pivot             | X 0.5, Y 1                            |
| Rect Transform  | Pos X / Y         | 0 / -48                               |
| Rect Transform  | Width / Height    | (from stretch) / 32                   |
| Image           | Source Image      | `ui_slot_background`                  |
| Image           | Image Type        | `Sliced`                              |
| Input Field     | Content Type      | `Standard`                            |

The `Input Field` template auto-creates two children, `Placeholder` and
`Text`. **Both keep their default Legacy Text components** (we don't
SerializeField them in our View class, so the InputField only needs the
right outward type — its internal children stay as Unity ships them).

Note: SearchField and FilterDropdown themselves stay as Legacy
`InputField` and `Dropdown` — our `ItemChecklistWindowView` references
them by those exact types in `[SerializeField]`. PugText only replaces
standalone text labels (Title, CounterLabel, ItemRow's Label /
Placeholder).

- `Placeholder` → set its Text to `Search…`
- `Text` → leave empty

### 4.5 `FilterDropdown`

Right-click `Window` → `UI → Legacy → Dropdown`. Rename to
`FilterDropdown`.

| Component       | Setting           | Value                                 |
|-----------------|-------------------|---------------------------------------|
| Rect Transform  | Anchors Min       | X 0.65, Y 1                           |
| Rect Transform  | Anchors Max       | X 1, Y 1                              |
| Rect Transform  | Pivot             | X 0.5, Y 1                            |
| Rect Transform  | Pos X / Y         | 0 / -48                               |
| Rect Transform  | Width / Height    | (from stretch) / 32                   |
| Image           | Source Image      | `ui_slot_background`                  |
| Image           | Image Type        | `Sliced`                              |
| Dropdown        | Options           | 3 entries: `All`, `Discovered`, `Undiscovered` |

The Dropdown auto-creates an internal `Template` GameObject for the
dropdown menu — leave that as-is for now (Legacy Text inside).

### 4.6 `ScrollView`

Right-click `Window` → `UI → Scroll View`. Rename to `ScrollView`.

| Component       | Setting           | Value                                 |
|-----------------|-------------------|---------------------------------------|
| Rect Transform  | Anchors stretch   | Min 0/0, Max 1/1                      |
| Rect Transform  | Left/Top/Right/Bottom | 4 / 88 / 4 / 32                  |
| Scroll Rect     | Horizontal        | ☐ (unchecked)                         |
| Scroll Rect     | Vertical          | ✓ (checked)                           |

The ScrollView template creates three nested children: `Viewport` →
`Content`, plus `Scrollbar Horizontal` and `Scrollbar Vertical`.

- **Delete `Scrollbar Horizontal`** (we only scroll vertically)
- On `Viewport`: leave the `Image` (used as mask) and `Mask` components
- On `Content`:
  - Anchors: Min 0/1, Max 1/1, Pivot 0/1 (top-stretch, top-anchored)
  - Add Component → `Vertical Layout Group` (Padding/Spacing 0 is fine)
  - Add Component → `Content Size Fitter` → Vertical Fit: `Preferred Size`

### 4.7 `CounterLabel`

Right-click `Window` → `UI → Legacy → Text`. Rename to `CounterLabel`.
Remove the auto-added `Text` component (⋮ → Remove Component), then
`Add Component → Pug Text`.

| Component       | Setting           | Value                                 |
|-----------------|-------------------|---------------------------------------|
| Rect Transform  | Anchors Min       | X 0, Y 0                              |
| Rect Transform  | Anchors Max       | X 1, Y 0                              |
| Rect Transform  | Pivot             | X 0.5, Y 0                            |
| Rect Transform  | Pos X / Y         | 0 / 4                                 |
| Rect Transform  | Width / Height    | (from stretch) / 24                   |
| PugText         | Text String       | `0 / 0 (0%)`                          |
| PugText         | Style → Font Face | `Bold Medium` (or `Score` for tabular numerals) |
| PugText         | Horizontal Alignment | `Center`                           |
| PugText         | Vertical Alignment | `Center`                             |
| PugText         | Color             | white                                 |

### 4.8 Attach `ItemChecklistWindowView` + wire slots

1. Select `ItemChecklistWindow` (the root) in Hierarchy
2. Inspector → `Add Component` → "Item Checklist Window View" (this is
   a UIelement subclass — Editor auto-shows the inherited UIelement
   fields like `topUIElements` / `bottomUIElements` etc. below our own;
   leave those empty for now)
3. The component appears with 7 empty drop-slots **for our SerializeField
   members**. Drag GameObjects from Hierarchy into them:

| Slot in Inspector  | Drag-Source from Hierarchy                       |
|--------------------|--------------------------------------------------|
| `Search Field`     | `SearchField` (the GameObject — Unity auto-picks the InputField) |
| `Filter Dropdown`  | `FilterDropdown`                                 |
| `Scroll Rect`      | `ScrollView`                                     |
| `Row Container`    | `Content` (inside ScrollView → Viewport → Content)|
| `Row Prefab`       | drag `ItemRow.prefab` **from the Project window** (not from Hierarchy) |
| `Counter Label`    | `CounterLabel`                                   |
| `Close Button`     | `CloseButton` (optional — can leave empty)       |

### 4.9 Save as Prefab

1. **Drag `ItemChecklistWindow` from Hierarchy** → into `Assets/ItemChecklist/Prefabs/` in Project window
2. The Hierarchy entry turns blue (= prefab instance)
3. Delete the temporary Canvas + EventSystem from the Hierarchy
   (`ItemChecklistWindow` and `EventSystem` — both no longer needed
   once the Prefab Asset exists)
4. `File → Save` to persist the scene (just to avoid the "unsaved
   changes" warning when closing the editor)

## Phase 5 (later) — `LiveTrackerBar.prefab`

A second, smaller prefab that shows the progress counter **without**
opening the modal. Always visible whenever a character is active.

Design open: position (top-right? bottom-center? top-left under
health bar?), exact format (`291 / 1745 (16.7%)` vs. abbreviated vs.
icon-prefixed). See conversation with the user — bring this up after
Phase 1's modal window is verified working in-game.

## 5. Build and test

Close the Unity Editor (it locks the project), then:

```bash
cd item-checklist
source .envrc && ../utils/build.sh
```

`BuildAssets` in `ModBuilder.cs:436-450` automatically packs every
`.prefab` under `modPath` (`Assets/ItemChecklist/`) into the AssetBundle
— no `assets: []` entry needed in `ItemChecklist.asset`.

Launch Core Keeper, load a world, press F1. Expected log lines:

```
[ItemChecklist] BuildUi: loading prefab
[ItemChecklist] BuildUi: prefab instantiated and wired
```

If you see `Window prefab not in bundle: Assets/ItemChecklist/Prefabs/...`,
the prefab path or filename doesn't match what `UiController.cs` expects
(`WindowPrefabPath` constant).

## Pitfalls

- **Editor open during build → build hangs.** Close Unity before
  `build.sh`.
- **Sprite Editor "Apply" required:** if you re-cut sub-sprites in the
  Sprite Editor, hit the Apply button or the meta won't persist.
- **Multi-sub-sprite picker:** when assigning sprites from
  `ui_classic.png`, the sprite picker shows the parent texture and the
  12 sub-sprites — pick the **named sub-sprite** (e.g. `ui_panel`), not
  the parent.
- **Canvas Render Mode:** Screen Space - Overlay works without a camera
  reference; Screen Space - Camera needs the UI camera set explicitly.
  Overlay is simpler.
- **`DontDestroyOnLoad` semantics:** the runtime keeps the instantiated
  prefab alive across scene loads. If a code change to `BuildUi` makes
  you suspect stale state, restart Core Keeper completely (not just
  reload a world).
- **`ItemRowView` in the Inspector is missing fields:** if Unity says
  "Script Missing" on the Item Row component, the asmdef hasn't picked
  up the new MonoBehaviour. Try `Assets → Reimport All`.

## After this works

The code-built fallback paths have been deleted from `UiController.cs`
(see commit `1e356f8`). The Prefab is the only way the UI builds. Once
this is stable, the bridge sprites can be replaced one-for-one with
final art per `docs/sprite-spec.md`'s pre-publish checklist.
