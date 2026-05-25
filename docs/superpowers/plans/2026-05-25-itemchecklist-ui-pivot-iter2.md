# ItemChecklist UI Pivot — Iteration 2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the content display layer to the Iter-1 window — rebuild `ItemRow.prefab` as SpriteRenderer, add a `RowsContainer` with CK's `UIScrollWindow` component, spawn all catalog items as rows with discovered/undiscovered visual differentiation. No filter/search yet (Iter-3).

**Architecture:** Window pulls catalog/state via static singletons at `ShowUI` time (no explicit wire-up). All ~500-1000 ItemRow instances spawn as parallel SpriteRenderer children of `RowsContainer/Content`. CK's native `UIScrollWindow` handles mouse-wheel scrolling + content-bounds. Old `ui/` files (VirtualScrollList.cs, UiController.cs, ItemRowView.cs) are deleted — disconnect-not-delete was Iter-1's strategy; Iter-2 finishes the cleanup.

**Tech Stack:** Unity 6000.0.59f2 SDK, C# 9 (Roslyn sandbox), CoreLib 4.0.4 UserInterfaceModule, CK vanilla `UIScrollWindow` + `PugText`, Harmony 2.x.

---

## Pre-Plan Findings (verified before writing this plan)

| # | Question | Finding | Plan impact |
|---|---|---|---|
| 1 | UIScrollWindow script GUID | DLL GUIDs known (Pug.UnityExtensions = `b689ed266d9e4f745b4e089d825a145c`, Pug.Other = `3519ac58e5ff54941a4a69512016923c`), but UIScrollWindow's **class-fileID inside the DLL is unknown** without inspecting a CK vanilla prefab that uses it (we don't have one accessible) | **Editor-side workflow**: user adds UIScrollWindow component via Unity's Add-Component menu — Unity resolves the correct fileID+guid automatically. We do NOT pre-write UIScrollWindow into the prefab YAML |
| 2 | PugText GUID still local | `3519ac58e5ff54941a4a69512016923c` — unchanged from Iter-1 | Use directly in ItemRow.prefab YAML |
| 3 | ItemCatalog public API | Method is **`GetByIndex(int)`** (not `GetAt`); Entry has **`DisplayName`** (not `Name`), `ObjectId`, `Icon`, `Variation`, `ModOrigin`; class has `Count` property; no static `Instance` accessor | Window code uses `catalog.GetByIndex(i)` + `entry.DisplayName`; we add static accessor in `ItemChecklistMod` |
| 4 | DiscoveredState public API | Already has **`public static DiscoveredState Instance`** + `IsDiscovered(int)` + `Discovered`+`Changed` events | Window uses `DiscoveredState.Instance.IsDiscovered(...)` directly — no extra accessor on Mod needed |
| 5 | `Manager.saves.*` in our code | Only 1 hit and it's in a doc-comment, no actual call | Sandbox-OK, no risk |

---

## File Structure

13 files touched. 5 deletions (cleanup), 4 modifications, 2 new files, 2 prefab rebuilds. No cross-repo changes.

| File | Responsibility | Operation |
|---|---|---|
| `unity/ItemChecklist/ui/UiController.cs` (+ `.meta`) | Old uGUI orchestration — replaced by `ItemChecklistWindow` + `IModUI` | DELETE |
| `unity/ItemChecklist/ui/ItemRowView.cs` (+ `.meta`) | Old uGUI row view — replaced by `ItemRow.cs` | DELETE |
| `unity/ItemChecklist/ui/VirtualScrollList.cs` (+ `.meta`) | Custom recycler with uGUI ScrollRect — replaced by CK `UIScrollWindow` + flat list | DELETE |
| `unity/ItemChecklist/ItemChecklistMod.cs` | Mod bootstrap (existing) — add static `Catalog` accessor | MODIFY |
| `unity/ItemChecklist/ui/ItemRow.cs` (+ `.meta`) | New 4-slot SpriteRenderer row component | CREATE |
| `unity/ItemChecklist/ui/ItemChecklistWindow.cs` | Iter-1 window class — add `rowsContent` + `rowPrefab` fields + `SpawnRows`/`ClearRows` | MODIFY |
| `unity/ItemChecklist/Prefabs/ItemRow.prefab` | Old uGUI row — rebuild as SpriteRenderer skeleton | REPLACE |
| `unity/ItemChecklist/Prefabs/ItemRow.prefab.meta` | Preserved (Unity holds GUID stable across content replacement) | PRESERVE |
| `unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab` | Iter-1 window — add `RowsContainer` + `Content` child GameObjects | EXTEND |

**Not touched** (mod logic + deferred-to-Iter-3 files):
- `ui/FilterAndSearchModel.cs` — Iter-3 wires it
- `ui/UnityInputFieldAdapter.cs` — Iter-3 search work context
- `DiscoveredState.cs`, `ItemCatalog.cs`, `CharacterDataDiscoverySnapshot.cs`, Save-Hooks, Harmony-State-Machine — unchanged

---

## Note on Commits and TDD

- **Commits:** user's CLAUDE.md says NEVER commit without explicit approval. Each phase below ends with a "Suggested commit" — surface it, do NOT run `git commit`. User runs it after reviewing.
- **TDD:** Unity mod work has no easy unit-test surface (sandbox compile at game load, integration via play-test through CrossOver). Each task ends with a deterministic build-verify step (grep, file inspection, log scan).

---

## Phase 1: Cleanup obsolete files

Goal: delete the 3 dead-code files that Iter-1 left as references. The Window class and IModUI pattern have replaced their function.

**Order matters:** Iter-1's disconnect strategy commented out call sites but left field declarations live. So `ItemChecklistMod.cs:40` has `private static readonly UiController Ui = new UiController();` and `ItemRowView.cs:36-38` references `UiController.UnknownItemSprite`. Deleting UiController.cs without cleaning these first breaks the compile (CS0246: type not found). Task 1 below handles both the Ui-field cleanup and the UiController delete together; Tasks 2-3 are then safe.

### Task 1: Remove dead Ui field + Delete UiController.cs

**Files:**
- Modify: `unity/ItemChecklist/ItemChecklistMod.cs` (remove the dead `Ui` field)
- Delete: `unity/ItemChecklist/ui/UiController.cs`
- Delete: `unity/ItemChecklist/ui/UiController.cs.meta`

- [ ] **Step 1: Inventory live references to UiController**

Run: `grep -n "UiController" unity/ItemChecklist/*.cs unity/ItemChecklist/ui/*.cs 2>/dev/null | grep -v "UiController.cs"`

Expected output: at minimum, ONE live reference in `ItemChecklistMod.cs` (the `private static readonly UiController Ui = new UiController();` field declaration that Iter-1's disconnect strategy left in place) and possibly references in `ItemRowView.cs` (e.g. `UiController.UnknownItemSprite`) that Task 2 will handle by deleting ItemRowView entirely. Comment lines (starting with `//`) are acceptable. Note all live references for the next step.

- [ ] **Step 2: Remove the dead `Ui` field from ItemChecklistMod.cs**

Use Edit to remove the line `private static readonly UiController Ui = new UiController();` (or whichever exact form it takes) from `unity/ItemChecklist/ItemChecklistMod.cs`. Also remove any commented-out lines that referenced `Ui.*` if they are no longer informative.

Important: do NOT delete other code in ItemChecklistMod.cs. The only target is the `Ui` field declaration plus any closely-tied comment block from Iter-1's disconnect.

- [ ] **Step 3: Verify ItemChecklistMod.cs no longer references UiController directly**

Run: `grep -n "UiController" unity/ItemChecklist/ItemChecklistMod.cs | grep -v "^[[:space:]]*//"`

Expected: empty (no live references; commented-out historical references are acceptable but ideally also cleaned).

- [ ] **Step 4: Delete UiController.cs + its meta**

```bash
rm unity/ItemChecklist/ui/UiController.cs
rm unity/ItemChecklist/ui/UiController.cs.meta
```

- [ ] **Step 5: Verify deletion**

```bash
ls unity/ItemChecklist/ui/UiController.cs 2>&1
```
Expected: `No such file or directory`

Skip a full build for now (Phase 6's build task does the consolidated verification after all deletions + code changes).

Note: At this point `ItemRowView.cs:36-38` still references `UiController.UnknownItemSprite` — that's a transient compile-break, fixed by Task 2 which deletes ItemRowView entirely.

### Task 2: Delete ItemRowView.cs

**Files:**
- Delete: `unity/ItemChecklist/ui/ItemRowView.cs`
- Delete: `unity/ItemChecklist/ui/ItemRowView.cs.meta`

- [ ] **Step 1: Verify file is unused at runtime**

Run: `grep -n "ItemRowView" unity/ItemChecklist/ unity/ItemChecklist/ui/ -r 2>/dev/null | grep -v "ItemRowView.cs" | grep -v "^[[:space:]]*//"`

Expected output: nothing live (the prefab `ItemRow.prefab` will be rebuilt in Task 8 — its current MonoBehaviour reference to ItemRowView.cs will be replaced with ItemRow.cs reference).

- [ ] **Step 2: Delete both files**

```bash
rm unity/ItemChecklist/ui/ItemRowView.cs
rm unity/ItemChecklist/ui/ItemRowView.cs.meta
```

- [ ] **Step 3: Verify**

```bash
ls unity/ItemChecklist/ui/ItemRowView.cs 2>&1
```
Expected: `No such file or directory`

### Task 3: Delete VirtualScrollList.cs + ItemChecklistWindowView.cs

**Files:**
- Delete: `unity/ItemChecklist/ui/VirtualScrollList.cs`
- Delete: `unity/ItemChecklist/ui/VirtualScrollList.cs.meta`
- Delete: `unity/ItemChecklist/ui/ItemChecklistWindowView.cs` (spec oversight — old uGUI window view, orphan, has dangling ItemRowView field reference)
- Delete: `unity/ItemChecklist/ui/ItemChecklistWindowView.cs.meta`

- [ ] **Step 1: Verify file is unused at runtime**

Run: `grep -n "VirtualScrollList" unity/ItemChecklist/ unity/ItemChecklist/ui/ -r 2>/dev/null | grep -v "VirtualScrollList.cs" | grep -v "^[[:space:]]*//"`

Expected output: nothing live.

- [ ] **Step 2: Delete both files**

```bash
rm unity/ItemChecklist/ui/VirtualScrollList.cs
rm unity/ItemChecklist/ui/VirtualScrollList.cs.meta
```

- [ ] **Step 3: Verify**

```bash
ls unity/ItemChecklist/ui/VirtualScrollList.cs 2>&1
```
Expected: `No such file or directory`

**Suggested commit for Phase 1 (DO NOT run without user approval):**

```
git add -u unity/ItemChecklist/ui/UiController.cs unity/ItemChecklist/ui/UiController.cs.meta \
            unity/ItemChecklist/ui/ItemRowView.cs unity/ItemChecklist/ui/ItemRowView.cs.meta \
            unity/ItemChecklist/ui/VirtualScrollList.cs unity/ItemChecklist/ui/VirtualScrollList.cs.meta
git commit -m "Iter-2 cleanup: delete obsolete uGUI ui/ files

UiController.cs, ItemRowView.cs, VirtualScrollList.cs were kept by
Iter-1's disconnect-not-delete strategy as references. Iter-2 replaces
their functionality (IModUI pattern + ItemRow.cs + CK UIScrollWindow)
so they go.

Refs: docs/superpowers/specs/2026-05-25-itemchecklist-ui-pivot-iter2-design.md"
```

---

## Phase 2: Static accessor on ItemChecklistMod

### Task 4: Add static Catalog accessor to ItemChecklistMod.cs

**Files:**
- Modify: `unity/ItemChecklist/ItemChecklistMod.cs`

- [ ] **Step 1: Read the file to find where Catalog is constructed**

Run: `grep -n "ItemCatalog\|_catalog\|catalog" unity/ItemChecklist/ItemChecklistMod.cs | head -20`

Note the variable name and where it's assigned. Likely a private field assigned in `Init()` or similar.

- [ ] **Step 2: Add public static accessor (mirrors existing `AssetBundle` pattern)**

Add a new line near the top of the class (next to `public static AssetBundle AssetBundle`):

```csharp
public static ItemCatalog Catalog { get; private set; }
```

- [ ] **Step 3: Assign the static when the private is set**

Find the line that assigns the private catalog field (likely in `Init()` or after world-load). Add the static assignment right after:

```csharp
// Existing line, e.g.:
//   _catalog = new ItemCatalog(...);
// or
//   _catalog.Bake();
Catalog = _catalog;
```

(If the private field is named something different, e.g. `itemCatalog` or `catalog`, adapt accordingly. The point: keep the existing private working as-is, just mirror to the static.)

- [ ] **Step 4: Verify**

Run: `grep -n "public static ItemCatalog\|Catalog = " unity/ItemChecklist/ItemChecklistMod.cs`
Expected: 2 lines — the static accessor definition + the assignment.

**Suggested commit for Phase 2 (DO NOT run without user approval):**

```
git add unity/ItemChecklist/ItemChecklistMod.cs
git commit -m "Add static ItemChecklistMod.Catalog accessor for UI lazy lookup

Mirrors the AssetBundle accessor pattern (Iter-1). ItemChecklistWindow
will pull catalog at ShowUI time, no explicit WireData needed.
DiscoveredState already has its own static Instance singleton."
```

---

## Phase 3: New ItemRow.cs + Window extensions

### Task 5: Create ItemRow.cs + .meta

**Files:**
- Create: `unity/ItemChecklist/ui/ItemRow.cs`
- Create: `unity/ItemChecklist/ui/ItemRow.cs.meta`

- [ ] **Step 1: Write the script file**

Create `unity/ItemChecklist/ui/ItemRow.cs`:

```csharp
using UnityEngine;

namespace ItemChecklist.UI
{
    public sealed class ItemRow : UIelement
    {
        // Editor-wired serialized fields (4-slot structure preserved from ItemRowView)
        public SpriteRenderer background;
        public SpriteRenderer icon;
        public PugText label;
        public PugText placeholder;
        public SpriteRenderer checkmark;

        public const float RowHeight = 2.5f; // world units (~40px at 16 PPU)

        public void Bind(int objectId, Sprite iconSprite, string name, bool isDiscovered)
        {
            if (isDiscovered)
            {
                if (icon != null) { icon.sprite = iconSprite; icon.enabled = true; }
                if (label != null) label.Render(name);
                if (placeholder != null) placeholder.gameObject.SetActive(false);
                if (checkmark != null) checkmark.enabled = true;
            }
            else
            {
                if (icon != null) icon.enabled = false;
                if (label != null) label.Render("???");
                if (placeholder != null) placeholder.gameObject.SetActive(true);
                if (checkmark != null) checkmark.enabled = false;
            }
        }
    }
}
```

- [ ] **Step 2: Write the .meta file with a fixed UUID**

Create `unity/ItemChecklist/ui/ItemRow.cs.meta`:

```yaml
fileFormatVersion: 2
guid: b7f3d2a8c4e94b6a8d1c7f5e3a2b9d8c
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
```

The GUID `b7f3d2a8c4e94b6a8d1c7f5e3a2b9d8c` is a fresh UUID. The prefab YAML in Task 7 will reference this exact GUID.

- [ ] **Step 3: Verify**

Run: `ls -la unity/ItemChecklist/ui/ItemRow.cs unity/ItemChecklist/ui/ItemRow.cs.meta && grep -c "ItemRow : UIelement" unity/ItemChecklist/ui/ItemRow.cs`
Expected: both files listed with non-zero size, grep count = 1.

### Task 6: Extend ItemChecklistWindow.cs with SpawnRows + ClearRows

**Files:**
- Modify: `unity/ItemChecklist/ui/ItemChecklistWindow.cs`

- [ ] **Step 1: Read the current file**

Run: `cat unity/ItemChecklist/ui/ItemChecklistWindow.cs`

Note the existing structure — Iter-1's Awake/ShowUI/HideUI/ApplyTheme.

- [ ] **Step 2: Add 2 serialized fields + 1 runtime list near the top of the class**

Find the existing serialized fields section (after `public PugText title;`). Add:

```csharp
public Transform rowsContent;     // assigned to RowsContainer/Content in Editor
public GameObject rowPrefab;      // assigned to ItemRow.prefab in Editor

private readonly System.Collections.Generic.List<ItemRow> _spawnedRows = new System.Collections.Generic.List<ItemRow>();
```

- [ ] **Step 3: Extend ShowUI() to call SpawnRows**

Find the existing `ShowUI` method. After `ApplyTheme();`, add:

```csharp
SpawnRows();
```

- [ ] **Step 4: Extend HideUI() to call ClearRows**

Find the existing `HideUI` method. Before `root.SetActive(false);`, add:

```csharp
ClearRows();
```

- [ ] **Step 5: Add SpawnRows + ClearRows private methods at the end of the class**

```csharp
private void SpawnRows()
{
    ClearRows();

    var catalog = ItemChecklistMod.Catalog;
    var state = DiscoveredState.Instance;
    if (catalog == null || state == null || rowPrefab == null) return;

    float y = 0f;
    for (int i = 0; i < catalog.Count; i++)
    {
        var entry = catalog.GetByIndex(i);
        var go = Object.Instantiate(rowPrefab, rowsContent);
        go.transform.localPosition = new Vector3(0, y, 0);
        var row = go.GetComponent<ItemRow>();
        if (row != null)
            row.Bind(entry.ObjectId, entry.Icon, entry.DisplayName, state.IsDiscovered(entry.ObjectId));
        _spawnedRows.Add(row);
        y -= ItemRow.RowHeight;
    }
}

private void ClearRows()
{
    foreach (var r in _spawnedRows)
        if (r != null) Object.Destroy(r.gameObject);
    _spawnedRows.Clear();
}
```

Note: `catalog.GetByIndex(i)` (not `GetAt`) and `entry.DisplayName` (not `entry.Name`) — verified APIs per Pre-Plan Findings.

- [ ] **Step 6: Verify**

Run: `grep -n "SpawnRows\|ClearRows\|rowsContent\|rowPrefab\|GetByIndex\|DisplayName" unity/ItemChecklist/ui/ItemChecklistWindow.cs`
Expected: ~10 matches across the file.

Run: `grep -c "ApplyTheme()" unity/ItemChecklist/ui/ItemChecklistWindow.cs`
Expected: `1` (the call in ShowUI — confirms we didn't accidentally duplicate or remove the Iter-1 logic).

**Suggested commit for Phase 3 (DO NOT run without user approval):**

```
git add unity/ItemChecklist/ui/ItemRow.cs unity/ItemChecklist/ui/ItemRow.cs.meta \
        unity/ItemChecklist/ui/ItemChecklistWindow.cs
git commit -m "Add ItemRow + Window SpawnRows/ClearRows

- New ItemRow.cs (UIelement-derived, 4-slot Bind API preserved from ItemRowView)
- ItemChecklistWindow extended with rowsContent+rowPrefab serialized fields
  and SpawnRows/ClearRows helpers; ShowUI spawns, HideUI clears
- Catalog pulled via static ItemChecklistMod.Catalog,
  state via existing DiscoveredState.Instance singleton"
```

---

## Phase 4: Prefab YAML work

### Task 7: Rebuild ItemRow.prefab as SpriteRenderer

**Files:**
- Replace: `unity/ItemChecklist/Prefabs/ItemRow.prefab`
- Preserve: `unity/ItemChecklist/Prefabs/ItemRow.prefab.meta` (Unity holds asset GUID stable)

- [ ] **Step 1: Replace prefab content**

Replace the entire content of `unity/ItemChecklist/Prefabs/ItemRow.prefab` with:

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &2000000000000000001
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 4000000000000000010}
  - component: {fileID: 1140000000000000010}
  m_Layer: 5
  m_Name: ItemRow
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &4000000000000000010
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 2000000000000000001}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {fileID: 4000000000000000011}
  - {fileID: 4000000000000000012}
  - {fileID: 4000000000000000013}
  - {fileID: 4000000000000000014}
  - {fileID: 4000000000000000015}
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!114 &1140000000000000010
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 2000000000000000001}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: b7f3d2a8c4e94b6a8d1c7f5e3a2b9d8c, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  background: {fileID: 2120000000000000010}
  icon: {fileID: 2120000000000000011}
  label: {fileID: 1140000000000000011}
  placeholder: {fileID: 1140000000000000012}
  checkmark: {fileID: 2120000000000000012}
--- !u!1 &2000000000000000010
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 4000000000000000011}
  - component: {fileID: 2120000000000000010}
  m_Layer: 5
  m_Name: Background
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &4000000000000000011
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 2000000000000000010}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 4000000000000000010}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!212 &2120000000000000010
SpriteRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 2000000000000000010}
  m_Enabled: 1
  m_CastShadows: 0
  m_ReceiveShadows: 0
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 0
  m_ReflectionProbeUsage: 0
  m_RayTracingMode: 0
  m_RayTraceProcedural: 0
  m_RayTracingAccelStructBuildFlagsOverride: 0
  m_RayTracingAccelStructBuildFlags: 1
  m_SmallMeshCulling: 1
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {fileID: 10754, guid: 0000000000000000f000000000000000, type: 0}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {fileID: 0}
  m_ProbeAnchor: {fileID: 0}
  m_LightProbeVolumeOverride: {fileID: 0}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 0
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {fileID: 0}
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 15
  m_Sprite: {fileID: 0, guid: 00000000000000000000000000000000, type: 0}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_FlipX: 0
  m_FlipY: 0
  m_DrawMode: 1
  m_Size: {x: 10, y: 2.5}
  m_AdaptiveModeThreshold: 0.5
  m_SpriteTileMode: 0
  m_WasSpriteAssigned: 0
  m_MaskInteraction: 0
  m_SpriteSortPoint: 0
--- !u!1 &2000000000000000011
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 4000000000000000012}
  - component: {fileID: 2120000000000000011}
  m_Layer: 5
  m_Name: Icon
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &4000000000000000012
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 2000000000000000011}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: -4, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 4000000000000000010}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!212 &2120000000000000011
SpriteRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 2000000000000000011}
  m_Enabled: 1
  m_CastShadows: 0
  m_ReceiveShadows: 0
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 0
  m_ReflectionProbeUsage: 0
  m_RayTracingMode: 0
  m_RayTraceProcedural: 0
  m_RayTracingAccelStructBuildFlagsOverride: 0
  m_RayTracingAccelStructBuildFlags: 1
  m_SmallMeshCulling: 1
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {fileID: 10754, guid: 0000000000000000f000000000000000, type: 0}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {fileID: 0}
  m_ProbeAnchor: {fileID: 0}
  m_LightProbeVolumeOverride: {fileID: 0}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 0
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {fileID: 0}
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 20
  m_Sprite: {fileID: 0, guid: 00000000000000000000000000000000, type: 0}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_FlipX: 0
  m_FlipY: 0
  m_DrawMode: 0
  m_Size: {x: 1, y: 1}
  m_AdaptiveModeThreshold: 0.5
  m_SpriteTileMode: 0
  m_WasSpriteAssigned: 0
  m_MaskInteraction: 0
  m_SpriteSortPoint: 0
--- !u!1 &2000000000000000012
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 4000000000000000013}
  - component: {fileID: 1140000000000000011}
  m_Layer: 5
  m_Name: Label
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &4000000000000000013
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 2000000000000000012}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: -1, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 4000000000000000010}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!114 &1140000000000000011
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 2000000000000000012}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 1873953792, guid: 3519ac58e5ff54941a4a69512016923c, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  centerInParent: 0
  activeInPlatforms: -1
  activeInStoreFronts: -1
  renderOnStart: 1
  keepEnabledOnStart: 1
  alwaysUpdateDynamicTextPixelPos: 0
  isWrittenToByUser: 0
  checkForProfanity: 0
  isHidden: 0
  trackDynamicTextCharacterEndPositions: 0
  localize: 0
  topAligned: 0
  languagesToForceLatinFont:
  localizePlaceholders: 1
  dontResetEffectsOnRender: 0
  hideInStreamerMode: 0
  maxWidth: 6
  textString:
  textSuffix:
  formatFields: []
  overrideMaterial: {fileID: 0}
  offsetKey:
  style:
    fontFace: 16777232
    capitalization: 0
    horizontalAlignment: 0
    verticalAlignment: 1
    extraCharSpacing: 0
    extraSpaceWidth: 0
    extraLineSpacing: 0
    extraEmptyLineSpacing: 0
    forceMonospace: 0
    wrapAtComma: 0
    rightToLeftXOffset: 0
    invertHorizontalAlignment: 0
    color: {r: 1, g: 1, b: 1, a: 1}
    outline: 0
    outlineColor: {r: 0, g: 0, b: 0, a: 0}
    supportColorTags: 0
    sortingLayer: -2147483648
    orderInLayer: 9999
    maskInteraction: 0
  styleOverrides: []
  usePooledResources: 1
  freeResourcesOnDisable: 0
--- !u!1 &2000000000000000013
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 4000000000000000014}
  - component: {fileID: 1140000000000000012}
  m_Layer: 5
  m_Name: Placeholder
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 0
--- !u!4 &4000000000000000014
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 2000000000000000013}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: -4, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 4000000000000000010}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!114 &1140000000000000012
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 2000000000000000013}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 1873953792, guid: 3519ac58e5ff54941a4a69512016923c, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  centerInParent: 0
  activeInPlatforms: -1
  activeInStoreFronts: -1
  renderOnStart: 1
  keepEnabledOnStart: 1
  alwaysUpdateDynamicTextPixelPos: 0
  isWrittenToByUser: 0
  checkForProfanity: 0
  isHidden: 0
  trackDynamicTextCharacterEndPositions: 0
  localize: 0
  topAligned: 0
  languagesToForceLatinFont:
  localizePlaceholders: 1
  dontResetEffectsOnRender: 0
  hideInStreamerMode: 0
  maxWidth: 1
  textString: "?"
  textSuffix:
  formatFields: []
  overrideMaterial: {fileID: 0}
  offsetKey:
  style:
    fontFace: 16777232
    capitalization: 0
    horizontalAlignment: 1
    verticalAlignment: 1
    extraCharSpacing: 0
    extraSpaceWidth: 0
    extraLineSpacing: 0
    extraEmptyLineSpacing: 0
    forceMonospace: 0
    wrapAtComma: 0
    rightToLeftXOffset: 0
    invertHorizontalAlignment: 0
    color: {r: 0.7058824, g: 0.7058824, b: 0.7058824, a: 1}
    outline: 0
    outlineColor: {r: 0, g: 0, b: 0, a: 0}
    supportColorTags: 0
    sortingLayer: -2147483648
    orderInLayer: 9999
    maskInteraction: 0
  styleOverrides: []
  usePooledResources: 1
  freeResourcesOnDisable: 0
--- !u!1 &2000000000000000014
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 4000000000000000015}
  - component: {fileID: 2120000000000000012}
  m_Layer: 5
  m_Name: Checkmark
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &4000000000000000015
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 2000000000000000014}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 4, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 4000000000000000010}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!212 &2120000000000000012
SpriteRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 2000000000000000014}
  m_Enabled: 1
  m_CastShadows: 0
  m_ReceiveShadows: 0
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 0
  m_ReflectionProbeUsage: 0
  m_RayTracingMode: 0
  m_RayTraceProcedural: 0
  m_RayTracingAccelStructBuildFlagsOverride: 0
  m_RayTracingAccelStructBuildFlags: 1
  m_SmallMeshCulling: 1
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {fileID: 10754, guid: 0000000000000000f000000000000000, type: 0}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {fileID: 0}
  m_ProbeAnchor: {fileID: 0}
  m_LightProbeVolumeOverride: {fileID: 0}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 0
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {fileID: 0}
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 20
  m_Sprite: {fileID: 0, guid: 00000000000000000000000000000000, type: 0}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_FlipX: 0
  m_FlipY: 0
  m_DrawMode: 0
  m_Size: {x: 1, y: 1}
  m_AdaptiveModeThreshold: 0.5
  m_SpriteTileMode: 0
  m_WasSpriteAssigned: 0
  m_MaskInteraction: 0
  m_SpriteSortPoint: 0
```

Note:
- ItemRow.cs script GUID `b7f3d2a8c4e94b6a8d1c7f5e3a2b9d8c` matches Task 5 Step 2
- PugText script GUID `3519ac58e5ff54941a4a69512016923c` + fileID `1873953792` matches Iter-1 verified local value
- `renderOnStart: 1` + `keepEnabledOnStart: 1` + `fontFace: 16777232` per Iter-1 lessons
- Placeholder GameObject has `m_IsActive: 0` (starts hidden — Bind() toggles based on discovered state)
- All 4 GameObjects on `m_Layer: 5` (UI)
- No BoxCollider2D (display-only, no click target)

- [ ] **Step 2: Verify structure**

Run: `grep -c "^--- !u!" unity/ItemChecklist/Prefabs/ItemRow.prefab`
Expected: `18` — counted: 6 GameObjects (ItemRow root + Background + Icon + Label + Placeholder + Checkmark) + 6 Transforms + 3 SpriteRenderers (Background + Icon + Checkmark) + 1 ItemRow MonoBehaviour + 2 PugText MonoBehaviours (Label + Placeholder).

Run: `grep -c "m_Layer: 5" unity/ItemChecklist/Prefabs/ItemRow.prefab`
Expected: `6` (one per GameObject: root + 5 children)

Run: `grep "guid: b7f3d2a8\|guid: 3519ac58" unity/ItemChecklist/Prefabs/ItemRow.prefab`
Expected: 1 line with `b7f3d2a8...` (ItemRow script) + 2 lines with `3519ac58...` (PugText for Label + Placeholder)

### Task 8: Extend ItemChecklistWindow.prefab with RowsContainer + Content

**Files:**
- Modify: `unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab`

This task adds 2 new child GameObjects to the existing window prefab WITHOUT touching the existing structure (root + Background + Title from Iter-1 + user's Editor-finishing).

The UIScrollWindow component is NOT added here — user adds it via Unity's Add-Component menu in Phase 5 (so Unity resolves the correct class fileID + guid).

- [ ] **Step 1: Identify the "root" Transform fileID in current prefab**

Run: `grep -B5 "m_Name: root" unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab | head`

Note the GameObject fileID (e.g. `&1000000000000000002`) and find its Transform fileID by scanning forward in the file. Per Iter-1, the root Transform is `&4000000000000000002`.

- [ ] **Step 2: Find the m_Children list of the root Transform**

Run: `grep -A8 "&4000000000000000002" unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab | head -20`

This should show the Transform block with `m_Children: - {fileID: 4000000000000000003}` (Background) and `- {fileID: 4000000000000000004}` (Title) — these are the existing children from Iter-1.

- [ ] **Step 3: Add 2 new fileIDs to root's m_Children list**

Use Edit on `unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab` to find:

```yaml
  m_Children:
  - {fileID: 4000000000000000003}
  - {fileID: 4000000000000000004}
  m_Father: {fileID: 4000000000000000001}
```

and replace with:

```yaml
  m_Children:
  - {fileID: 4000000000000000003}
  - {fileID: 4000000000000000004}
  - {fileID: 4000000000000000020}
  - {fileID: 4000000000000000021}
  m_Father: {fileID: 4000000000000000001}
```

(The 2 new fileIDs are RowsContainer's Transform and Content's Transform — defined in Step 4.)

- [ ] **Step 4: Append RowsContainer + Content GameObjects to the prefab**

Append to the end of `unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab` (after the last `--- !u!` block from Iter-1):

```yaml
--- !u!1 &1000000000000000020
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 4000000000000000020}
  m_Layer: 5
  m_Name: RowsContainer
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &4000000000000000020
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1000000000000000020}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: -1, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {fileID: 4000000000000000021}
  m_Father: {fileID: 4000000000000000002}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!1 &1000000000000000021
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 4000000000000000021}
  m_Layer: 5
  m_Name: Content
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &4000000000000000021
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 1000000000000000021}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 4000000000000000020}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
```

- [ ] **Step 5: Verify**

Run: `grep -E "RowsContainer|Content" unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab`
Expected: 2 `m_Name: RowsContainer` lines and 2 `m_Name: Content` lines (actually 1 each — the names appear once in their respective GameObject blocks).

Run: `grep -c "^--- !u!" unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab`
Expected: Iter-1 had 13; we added 4 new docs (2 GameObjects + 2 Transforms) = `17`.

**Suggested commit for Phase 4 (DO NOT run without user approval):**

```
git add unity/ItemChecklist/Prefabs/ItemRow.prefab \
        unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab
git commit -m "Iter-2 prefabs: rebuild ItemRow + extend Window with RowsContainer

- ItemRow.prefab: replace uGUI with SpriteRenderer + Layer 5;
  5 children (Background/Icon/Label/Placeholder/Checkmark);
  PugText fontFace 16777232 + renderOnStart=1 (Iter-1 lessons)
- ItemChecklistWindow.prefab: add RowsContainer + Content under root
  (Editor adds UIScrollWindow component as next step)"
```

---

## Phase 5: User Editor finishing

This phase is **user-driven**. Claude cannot drive the Unity Editor.

### Task 9: User finishes ItemRow.prefab in Editor

- [ ] **Step 1: Open the prefab in Prefab edit mode**

User opens Unity Editor on the CoreKeeperModSDK project. Double-click `Assets/ItemChecklist/Prefabs/ItemRow.prefab` in Project tab.

- [ ] **Step 2: Verify no Missing Scripts**

Check each child GameObject:
- `ItemRow` (root): ItemRow MonoBehaviour visible, NOT "Missing"
- `Background`: SpriteRenderer
- `Icon`: SpriteRenderer
- `Label`: PugText
- `Placeholder`: PugText
- `Checkmark`: SpriteRenderer

If any "Missing (Mono Script)" appears: stop, report the offending GUID — likely PugText GUID has changed locally or ItemRow.cs.meta GUID got reassigned by Unity.

- [ ] **Step 3: Assign Background sprite**

Select `Background` child. In SpriteRenderer, assign a sprite to the `Sprite` field. Suggestion: `Assets/ItemChecklist/Art/Bridge/ui_slot_background.png` for a subtle row background. Or leave null for transparent rows (background sprite is optional).

- [ ] **Step 4: Assign Checkmark sprite**

Select `Checkmark` child. Assign a sprite for the "discovered" indicator. Use any checkmark/tick sprite from the ui_classic atlas or Bridge folder.

- [ ] **Step 5: (Optional) Assign Icon placeholder sprite**

Optional — leave as null (runtime Bind will assign per-item). If you want an Editor-preview sprite, assign any small sprite.

- [ ] **Step 6: Save the prefab**

`Cmd+S` while in Prefab edit mode. Exit Prefab edit (back arrow in Hierarchy).

### Task 10: User adds UIScrollWindow to RowsContainer + wires Window fields

- [ ] **Step 1: Open ItemChecklistWindow.prefab in Prefab edit mode**

Double-click `Assets/ItemChecklist/Prefabs/ItemChecklistWindow.prefab` in Project tab.

- [ ] **Step 2: Verify hierarchy includes the new RowsContainer + Content children**

Hierarchy should show:
```
ItemChecklistWindow
└── root
    ├── Background
    ├── Title
    └── RowsContainer
        └── Content
```

- [ ] **Step 3: Add UIScrollWindow component to RowsContainer**

Select `RowsContainer` GameObject. In Inspector, click "Add Component" → search "UIScrollWindow" → click to add. Unity resolves the correct script reference (Pug.UnityExtensions.dll or Pug.Other.dll).

- [ ] **Step 4: Configure UIScrollWindow Inspector fields**

UIScrollWindow has Inspector fields whose meaning depends on its internal API. Default values are usually a good starting point. Key fields likely include:
- Scroll direction (Vertical/Horizontal) — set to **Vertical**
- Scrollable target / content reference — drag the **Content** child GameObject into this field if such a field exists
- Wheel sensitivity — leave default

If unsure: copy values from `/tmp/ck-ui-research/ItemBrowser/ItemBrowserPackage/Prefabs/Browser/ItemBrowserUI.prefab` by inspecting one of its UIScrollWindow instances in YAML.

- [ ] **Step 5: Wire ItemChecklistWindow fields**

Select `ItemChecklistWindow` (the root GameObject). In Inspector, find the ItemChecklistWindow component. New fields from Task 6:
- `Rows Content` field → drag the `Content` GameObject (the grandchild of RowsContainer) into it
- `Row Prefab` field → drag `Assets/ItemChecklist/Prefabs/ItemRow.prefab` from Project tab into it

- [ ] **Step 6: Save + close Editor**

`Cmd+S`, exit Prefab edit, `Cmd+Q` to close Unity entirely (so batchmode build can take the project lock).

- [ ] **Step 7: Verify save (from terminal)**

Run: `grep -c "UIScrollWindow\|scrollWindow\|rowsContent\|rowPrefab" unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab`
Expected: at least 4 hits — UIScrollWindow component reference + rowsContent + rowPrefab fields assigned to non-zero fileIDs.

**Suggested commit for Phase 5 (DO NOT run without user approval):**

```
git add unity/ItemChecklist/Prefabs/ItemRow.prefab \
        unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab
git commit -m "Editor finishing: ItemRow sprites + UIScrollWindow + Window wiring

- ItemRow.prefab: Background sprite, Checkmark sprite assigned
- ItemChecklistWindow.prefab: UIScrollWindow component added to
  RowsContainer (script resolved to vanilla CK class); ItemChecklistWindow
  fields rowsContent + rowPrefab wired"
```

---

## Phase 6: Build + Test

### Task 11: Build + Test Phase 1 (sandbox compile check)

**Files:** none modified — runs build pipeline + verification.

- [ ] **Step 1: Confirm Unity Editor is closed**

Run: `pgrep -f "Unity.app/Contents/MacOS/Unity" || echo "Editor closed"`

If anything is printed (Editor running), report BLOCKED — user closes Editor.

- [ ] **Step 2: Clear Player.log + Player-prev.log**

```bash
LOG_DIR="$HOME/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper"
> "$LOG_DIR/Player.log"
> "$LOG_DIR/Player-prev.log"
```

- [ ] **Step 3: Build + install**

```bash
source .envrc
../../../utils/build.sh
```

Expected: `✓ Build complete.` + `✓ Install complete.` at the end. Build duration ~20-40s.

- [ ] **Step 4: User launches CK + waits for title screen**

User launches Core Keeper via CrossOver. DO NOT open in-game Mods menu. Wait until title screen visible.

- [ ] **Step 5: Phase 1 acceptance check**

```bash
LOG_DIR="$HOME/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper"
grep "Item Checklist\|ItemChecklist" "$LOG_DIR/Player.log" | head -10
grep "CompileFailed\|Exception" "$LOG_DIR/Player.log" | head -10
```

Acceptance:
- ✅ `[Item Checklist] EarlyInit` appears
- ✅ `Successfully compiled ItemChecklist safetyCheck=True` appears
- ✅ Zero `CompileFailed` for ItemChecklist OR CoreLib OR other mods
- ✅ Zero `Exception` mentioning ItemChecklist
- ✅ No `TitleMenuIncompatibleModWarning` dialog (user-visual check)

**Fail handling:** SIGKILL CK (`pkill -KILL -f "Core Keeper"`), analyze Player.log, identify cause. Budget: **3 attempts**. If still fails after 3 → stop, re-brainstorm.

### Task 12: Test Phase 2+3 (mod registration + visual + scroll)

- [ ] **Step 1: User loads world from title screen**

Click "Play" → load any world. Wait for in-game state.

- [ ] **Step 2: Phase 2 acceptance check (mod load + statics)**

```bash
LOG_DIR="$HOME/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper"
grep "Registering ItemChecklist:Window\|ItemChecklist" "$LOG_DIR/Player.log" | head -15
```

Acceptance:
- ✅ `Registering ItemChecklist:Window Modded UI!` (CoreLib's log)
- ✅ No errors/exceptions from our code
- ✅ WASD moves character normally (negative test: window not open)

- [ ] **Step 3: User presses F1 + visual check (Phase 3)**

Acceptance (visual):
- ✅ Window appears centered with wood-frame + title (Iter-1 baseline preserved)
- ✅ Rows visible under the title — at least the first ~5-10 items
- ✅ Discovered items show icon + label (item DisplayName)
- ✅ Undiscovered items show `?` placeholder + label `???`
- ✅ Checkmark visible on discovered, hidden on undiscovered
- ✅ Mouse-wheel scrolls content vertically
- ✅ Scroll bounds respected (no over-scroll)
- ✅ Cursor + WASD-block + mouse-click-silence (Iter-1 baseline)

**Fail handling per Risk:**
- Rows not visible at all → check `rowsContent` + `rowPrefab` are wired in Window Inspector (Phase 5 Task 10 Step 5)
- Rows visible but wrong text → API mismatch (e.g. used `Name` instead of `DisplayName`) → fix Window.cs and rebuild
- Scroll doesn't work → UIScrollWindow Inspector config issue → see Risk #3 in spec
- Labels invisible → fontFace not 16777232 or renderOnStart=0 → verify in Editor

### Task 13: Test Phase 4 (close + reopen + cleanup)

- [ ] **Step 1: Press Escape**

Acceptance:
- ✅ Window closes
- ✅ Cursor disappears, WASD re-enabled

- [ ] **Step 2: Press F1 again**

Acceptance:
- ✅ Window reopens with same items, no duplicates
- ✅ Scroll position back to top (reset on each open is expected per spec)

- [ ] **Step 3: Press Escape again, check log**

```bash
LOG_DIR="$HOME/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper"
grep -i "nullref\|destroyed.*referenc\|exception" "$LOG_DIR/Player.log" | grep -i "itemchecklist\|itemrow" | head
```

Acceptance:
- ✅ No NullReferenceException or "destroyed but referenced" warnings

### Task 14: (Optional) Test Phase 5 (perf + stability)

- [ ] **Step 1: F1-spam test**

User presses F1 + Escape rapidly 10 times.

Acceptance:
- ✅ No visible frame drops (subjective, eyeball test)
- ✅ Player.log: no progressive memory warning or GC spam
- ✅ Activity Monitor: memory stable (no linear growth)

**Fail handling:** add pool pattern (defer to Iter-3 if not blocking).

**Suggested commit for Phase 6 (DO NOT run without user approval, only if all phases pass):**

```
git commit --allow-empty -m "Iter-2 validated: display layer with UIScrollWindow

All test phases pass:
- Phase 1 (sandbox): ItemChecklist + ItemRow + Window extensions compile clean
- Phase 2 (registration): CoreLib registers window, statics populate
- Phase 3 (visual+scroll): rows render with discovered/undiscovered
  differentiation, mouse-wheel scrolls within content bounds
- Phase 4 (close/reopen): clean state, no row leak
- Phase 5 (perf): stable across F1-spam (or deferred to Iter-3 polish)

Iter-3 next: Filter dropdown + Search input field.
See docs/superpowers/specs/2026-05-25-itemchecklist-ui-pivot-iter2-design.md."
```

---

## Phase 7: Wrap-up

### Task 15: Update memory + spike-4 status

**Files:**
- Modify: `~/.claude/projects/-Users-valgard-Projects-private-core-keeper/memory/project_item_checklist_ui_pivot_state.md`
- Modify: `docs/research/spike-4-ui-architecture.md`

- [ ] **Step 1: Update the memory file's frontmatter description**

Edit `~/.claude/projects/-Users-valgard-Projects-private-core-keeper/memory/project_item_checklist_ui_pivot_state.md`.

Replace the frontmatter `description:` line with:

```yaml
description: ItemChecklist mod — Iter-2 (display layer with UIScrollWindow) validated; Iter-3 (Filter+Search) pending
```

- [ ] **Step 2: Update the Status section body**

Replace the `## Status` section body with:

```markdown
**Iter-2 vollständig validiert 2026-MM-DD**: ItemRow.prefab als
SpriteRenderer rebuild, UIScrollWindow als nativer Scroll-Container,
~500-1000 ItemRows spawnen sandbox-OK, mouse-wheel-scroll funktioniert,
discovered/undiscovered visuell differenziert. Mod-Logik (Catalog +
DiscoveredState.Instance) unverändert.

**Iter-3 pending**: Filter-Dropdown + Search-Input. Search-Input bringt
eigene Sandbox-Unknowns (TextInputInterface, UnityInputFieldAdapter
war in Iter-1 deshalb tot).
```

Replace `MM-DD` with actual completion date from `date +%Y-%m-%d`.

- [ ] **Step 3: Update MEMORY.md index line**

Edit `~/.claude/projects/-Users-valgard-Projects-private-core-keeper/memory/MEMORY.md`. Find the line starting with `- [ItemChecklist UI-Pivot Stand` and replace its description with:

```markdown
- [ItemChecklist UI-Pivot Stand 2026-05-25](project_item_checklist_ui_pivot_state.md) — **Iter-2 validiert** (UIScrollWindow + ItemRow Display-Layer). Iter-3 (Filter + Search) pending. Volldoku in spec/plan/spike-4.
```

- [ ] **Step 4: Update spike-4 status header**

Edit `docs/research/spike-4-ui-architecture.md`. Find the `**Status:**` line, append `Iter-2 validated 2026-MM-DD (display layer with UIScrollWindow + ItemRow spawning).` to the existing status block.

- [ ] **Step 5: Report Iter-2 done**

Surface completion to user. Iter-3 (Filter + Search) can be started via writing-plans skill.

**Suggested final commit (DO NOT run without user approval):**

```
git add docs/research/spike-4-ui-architecture.md
git commit -m "docs(spike-4): mark Iter-2 validated"
```

---

## References

- Iter-2 Spec: `docs/superpowers/specs/2026-05-25-itemchecklist-ui-pivot-iter2-design.md`
- Iter-1 Spec: `docs/superpowers/specs/2026-05-25-itemchecklist-ui-pivot-iter1-design.md`
- Iter-1 Plan: `docs/superpowers/plans/2026-05-25-itemchecklist-ui-pivot-iter1.md`
- Spike-4: `docs/research/spike-4-ui-architecture.md`
- Reference codebase (UIScrollWindow patterns):
  `/tmp/ck-ui-research/ItemBrowser/ItemBrowserPackage/Scripts/Common/UserInterface/Browser/ItemBrowserButton.cs`
  + `ItemBrowserSlot.cs` + `Details/Entries/EntriesList.cs` + `Main/Options/OptionsView.cs`
- Memory: `[[item-checklist-ui-pivot-state]]`, `[[corekeeper-ui-pattern]]` (9 Iter-1 empirical gotchas), `[[project_pugstorm_sandbox_rules]]`, `[[project_corekeeper_compile_fail_cascade]]`, `[[feedback_targeted_rollback_on_crash]]`
