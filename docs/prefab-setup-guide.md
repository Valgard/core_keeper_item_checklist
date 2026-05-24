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

1. Project → `Assets/ItemChecklist/Prefabs/` → right-click → **Create →
   Prefab** (or design in scene as above and drag in).
2. Open the prefab for editing (double-click).
3. Build the hierarchy:

   ```
   ItemChecklistWindow              [Canvas + CanvasScaler + GraphicRaycaster]
   └─ Window                        [RectTransform + Image(sprite=ui_panel, Sliced)]
      ├─ Header                     [RectTransform + Image(sprite=ui_tab, Sliced)]
      │   ├─ Title                  [Text "Item Checklist"]
      │   └─ CloseButton            [Button + Image]
      ├─ SearchField                [InputField + Image(sprite=ui_slot_background, Sliced)]
      │   └─ Text                   [Text — InputField's text component]
      │   └─ Placeholder            [Text "Search…"]
      ├─ FilterDropdown             [Dropdown + Image(sprite=ui_slot_background, Sliced)]
      ├─ ScrollView                 [ScrollRect]
      │   ├─ Viewport               [RectTransform + Image (Mask) + RectMask2D]
      │   │   └─ Content            [RectTransform + VerticalLayoutGroup + ContentSizeFitter]
      │   ├─ Scrollbar Vertical     [Scrollbar — sprite=ui_scrollbar_handle for the handle]
      │   └─ (Scrollbar Horizontal — optional, can remove)
      └─ CounterLabel               [Text — bottom-center "0 / 0 (0%)"]
   ```

4. **Sprite assignment** (for each Image with a sprite column above):
   - Click the Image component → next to `Source Image` → click the
     small ⊙ button → search for the sub-sprite by name (e.g.
     `ui_panel`)
   - Set `Image Type: Sliced` (borders come from the atlas meta
     automatically — 8/7/7/8 for `ui_panel`, 3/3/3/3 for
     `ui_slot_background`)

5. **Anchor + size suggestions** (RectTransform-relative to parent):
   - Window: anchorMin (0, 0), anchorMax (1, 1), full stretch — let
     Canvas Scaler handle on-screen size; or anchor top-right to a
     fixed 400×800 panel if you prefer a side-window
   - Header: top-stretch, height 40
   - SearchField: top-stretch, height 32, offset down by 48
   - FilterDropdown: same row as SearchField (split horizontally if you
     want — anchor SearchField (0,1)-(0.65,1), FilterDropdown
     (0.65,1)-(1,1))
   - ScrollView: stretch fill the middle, leave room for CounterLabel
     at bottom
   - CounterLabel: bottom-stretch, height 24

6. **Add the `ItemChecklistWindowView` component** to the root
   (`ItemChecklistWindow` GameObject) → Inspector → **Add Component →
   Item Checklist Window View**.

7. **Wire the SerializeField slots** by dragging GameObjects from the
   Hierarchy:
   - `searchField` ← SearchField (the InputField component)
   - `filterDropdown` ← FilterDropdown
   - `scrollRect` ← ScrollView
   - `rowContainer` ← Content (the inner GameObject with
     VerticalLayoutGroup, not the Viewport or ScrollRect itself)
   - `rowPrefab` ← drag `ItemRow.prefab` from Project window
   - `counterLabel` ← CounterLabel (the Text component)
   - `closeButton` ← CloseButton (optional — can leave empty)

8. **Configure the FilterDropdown options**:
   - Select FilterDropdown → in its Dropdown component, expand
     `Options` and set 3 entries: `All`, `Discovered`, `Undiscovered`
     (must match the order of `DiscoveryFilter` enum)

9. **Save** the prefab (`CMD+S`) and exit prefab edit mode.

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
