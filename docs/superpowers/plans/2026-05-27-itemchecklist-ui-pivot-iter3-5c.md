# ItemChecklist UI Pivot — Iter-3.5c Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add viewport-clipping to ItemChecklist UI via IB-1:1-konforme Prefab-Edits + statisches `ContentsMask`-GameObject — Zero-Code-Iteration. Rows die durch Scroll aus dem Wood-Theme-Rechteck herauslaufen werden unsichtbar; Title und Vanilla-UI bleiben unaffected.

**Architecture:** Statisches `ContentsMask` GameObject mit SpriteMask Component im Window-Prefab als Geschwister vom scrollenden Content. Custom-Range Layer GUI (uniqueID `1241602095`) Orders 40..55. ItemRow.prefab SpriteRenderer/PugText alle auf Layer GUI mit Orders 45/48/49/49/50 (matched IB EntriesDivider+UnavailableHeader). Sprite-Quelle ist neues `Art/Bridge/mask_sprite.png` (1×1 weiß). Keine .cs-Änderungen.

**Tech Stack:** Unity 6000.0.59f2 Editor (für ContentsMask-Roundtrip), Edit-Tool für YAML-direct-edits an ItemRow.prefab, ImageMagick für PNG-Generierung, `../../utils/build.sh` Unity batchmode Build-Pipeline, `../../utils/install-macos.sh` fake-ID dev-install.

**Reference spec:** `docs/superpowers/specs/2026-05-27-itemchecklist-ui-pivot-iter3-5c-design.md` (commit `f0d2dfd`)

**Memory anchors:** [[item-checklist-ui-pivot-state]], [[corekeeper-ui-pattern]], [[pugstorm-modbuilder-sprite-meta]], [[reference-analysis-mandatory-when-provided]], [[avoid-inverse-inference-fallacy]], [[worktree-remove-preflight-check]], [[subagent-build-verify-install]], [[frequent-wip-commits-for-bisect]]

---

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `unity/ItemChecklist/Art/Bridge/mask_sprite.png` | **Create** in main-Repo (NOT worktree) | 1×1 weißes PNG für SpriteMask-Geometry |
| `unity/ItemChecklist/Art/Bridge/mask_sprite.png.meta` | **Create** | Sprite-Asset-Konfiguration: `textureType: 8`, `spriteMode: 1`, eigene GUID |
| `unity/ItemChecklist/Art/Bridge/white_pixel.png` | **Delete** | Text-Stub, kein valides PNG, unreferenziert |
| `unity/ItemChecklist/Art/Bridge/white_pixel.png.meta` | **Delete** | Orphan |
| `.worktrees/iter-3-5c/` | **Create** (worktree) | Isolated workspace |
| `.worktrees/iter-3-5c/.envrc` | **Create** (copy from main) | Env vars (gitignored) |
| `unity/ItemChecklist/Prefabs/ItemRow.prefab` | **YAML-direct-edit** | 5 Renderer-Blöcke: Layer GUI + Orders 45/48/49/49/50 + MaskInteraction=1 |
| `unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab` | **Editor-Roundtrip** | Neues ContentsMask GameObject + SpriteMask Component |

**Unchanged:** alle `unity/ItemChecklist/ui/*.cs`, `unity/ItemChecklist/*.cs`, `unity/ItemChecklist.asmdef`, `ModManifest.json`.

---

## Phase 0 — Pre-Flight + Asset-Setup (im main-Repo)

### Task 0: TagManager + IB-uniqueID Sanity-Check

**Files:**
- Read-only: `CoreKeeperModSDK/ProjectSettings/TagManager.asset`
- Read-only: `/tmp/ck-ui-research/ItemBrowser/ItemBrowserPackage/Prefabs/Browser/EntriesDivider.prefab`
- No code/asset changes.

- [ ] **Step 1: Verify CK + Unity Editor not running**

```bash
/usr/bin/pgrep -fl "Core Keeper" || echo "ck-not-running"
/usr/bin/pgrep -fl "Unity.app/Contents/MacOS/Unity" || echo "editor-not-running"
```

Wenn eines läuft: STOP, User um Close bitten. Editor-Lock blockiert Asset-Operations.

- [ ] **Step 2: Verify GUI uniqueID `1241602095` in TagManager**

```bash
grep -nE 'name: GUI|uniqueID: 1241602095' /Users/valgard/Projects/private/core_keeper/CoreKeeperModSDK/ProjectSettings/TagManager.asset
```

Expected: zwei Zeilen, eine `name: GUI`, direkt darunter `uniqueID: 1241602095`. Falls Mismatch (z.B. uniqueID hat sich geändert nach CK-Update): STOP, re-resolve den Wert + alle 5 YAML-Edits + Editor-Mask-Setting im Plan aktualisieren.

- [ ] **Step 3: Verify IB EntriesDivider als ground-truth Reference**

```bash
grep -nE 'm_SortingLayerID: 1241602095|m_SortingOrder: 49' /tmp/ck-ui-research/ItemBrowser/ItemBrowserPackage/Prefabs/Browser/EntriesDivider.prefab
```

Expected: beide Zeilen vorhanden (Z77 + Z79). Bestätigt dass IB diese exakten Werte nutzt.

- [ ] **Step 4: Verify ItemRow.prefab current state** (vor Edits) — Zeilen-Anchors stimmen mit Spec

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist
grep -nE 'm_SortingLayerID: 0|m_SortingOrder: 15|m_SortingOrder: 20|sortingLayer: -2147483648|orderInLayer: 9999|m_MaskInteraction: 0' unity/ItemChecklist/Prefabs/ItemRow.prefab
```

Expected: matched current state aus Spec (Background Order 15, Icon+Checkmark Order 20, 2× PugText sortingLayer sentinel + orderInLayer 9999, 3× MaskInteraction 0). Falls anders: Pre-Flight-Daten sind stale, Spec re-validieren bevor Edits.

---

### Task 1: Asset-Erstellung `mask_sprite.png` + `.meta` (im main-Repo)

**Files:**
- Create: `unity/ItemChecklist/Art/Bridge/mask_sprite.png`
- Create: `unity/ItemChecklist/Art/Bridge/mask_sprite.png.meta`

**Wichtig:** Diese Schritte laufen **im main-Repo** (`/Users/valgard/Projects/private/core_keeper/item-checklist/`), **NICHT im Worktree** — der Worktree existiert in diesem Plan-Step noch nicht, und das Asset muss im main persistent landen, damit `utils/link.sh` es transitiv via symlink in den Worktree mitbringt.

- [ ] **Step 1: Generate 1×1 white PNG via ImageMagick**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/unity/ItemChecklist/Art/Bridge
which convert || brew install imagemagick
convert -size 1x1 xc:white mask_sprite.png
file mask_sprite.png
```

Expected: `file` reports `PNG image data, 1 x 1, ...`. Falls noch `ASCII text`: convert failed — fallback via Python:

```bash
python3 -c "from PIL import Image; Image.new('RGBA', (1,1), (255,255,255,255)).save('mask_sprite.png')"
file mask_sprite.png
```

- [ ] **Step 2: Sanity-Check Pixel-Color**

```bash
python3 -c "from PIL import Image; im=Image.open('mask_sprite.png'); print('size:', im.size); print('mode:', im.mode); print('pixel[0,0]:', im.getpixel((0,0)))"
```

Expected: `size: (1, 1)`, `mode: RGBA` (oder `RGB`), `pixel[0,0]: (255, 255, 255, 255)` (oder `(255, 255, 255)`). Falls nicht weiß: STOP, PNG regenerieren.

- [ ] **Step 3: Generate `.meta` from white_pixel.png template + neue GUID**

```bash
NEW_GUID=$(uuidgen | tr -d - | tr A-Z a-z)
echo "New GUID: $NEW_GUID"
sed "s|guid: d3662226ae3e24840af75686534a6ad9|guid: $NEW_GUID|" \
    white_pixel.png.meta > mask_sprite.png.meta
grep -nE '^guid:|textureType:|spriteMode:' mask_sprite.png.meta
```

Expected: 3 lines — `guid: <new 32-hex>`, `textureType: 8`, `spriteMode: 1`. Die neue GUID muss als Variable bewahrt werden für Step 4 (Verifikation) und für Task 9 Step 5 (Sprite-Reference im Editor).

- [ ] **Step 4: Verify .meta integrity**

```bash
grep -cE '^guid:' mask_sprite.png.meta
grep -nE 'textureType: 8|spriteMode: 1' mask_sprite.png.meta
```

Expected: `1` (genau ein guid-Field), zwei Lines für textureType + spriteMode. Falls miss: revertieren + neu starten Step 3.

- [ ] **Step 5: Record GUID für spätere Verifikation**

```bash
mkdir -p /tmp/iter-3-5c-spike
grep '^guid:' /Users/valgard/Projects/private/core_keeper/item-checklist/unity/ItemChecklist/Art/Bridge/mask_sprite.png.meta > /tmp/iter-3-5c-spike/mask-sprite-guid.txt
cat /tmp/iter-3-5c-spike/mask-sprite-guid.txt
```

Diese GUID wird in Task 9 + Task 11 referenziert.

---

### Task 2: Delete `white_pixel.png` + `.meta` (im main-Repo)

**Files:**
- Delete: `unity/ItemChecklist/Art/Bridge/white_pixel.png`
- Delete: `unity/ItemChecklist/Art/Bridge/white_pixel.png.meta`

- [ ] **Step 1: Sanity-Check Asset ist unreferenziert** (nochmal vor Delete)

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist
grep -rln 'd3662226ae3e24840af75686534a6ad9' unity/ 2>/dev/null
```

Expected: nur `unity/ItemChecklist/Art/Bridge/white_pixel.png.meta` (das eigene .meta). Falls ein Prefab das Asset referenziert: STOP, Abklären — möglicherweise hat Sven zwischenzeitlich eine Reference gesetzt.

- [ ] **Step 2: Delete beide Files**

```bash
rm unity/ItemChecklist/Art/Bridge/white_pixel.png
rm unity/ItemChecklist/Art/Bridge/white_pixel.png.meta
ls unity/ItemChecklist/Art/Bridge/ | grep -E 'mask_sprite|white_pixel'
```

Expected: nur `mask_sprite.png` + `mask_sprite.png.meta` aufgelistet, `white_pixel*` weg. Bridge-Folder ist gitignored → kein git-Commit nötig für diese Asset-Operations.

---

## Phase 1 — Worktree-Setup

### Task 3: Worktree iter-3-5c + .envrc

**Files:**
- Create: `.worktrees/iter-3-5c/`
- Create: `.worktrees/iter-3-5c/.envrc`

- [ ] **Step 1: Create worktree**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist
git worktree add .worktrees/iter-3-5c -b iter-3-5c
git -C .worktrees/iter-3-5c rev-parse --abbrev-ref HEAD
```

Expected: directory created, `HEAD` is `iter-3-5c` (from main @ `f0d2dfd`).

- [ ] **Step 2: Copy `.envrc`** (gitignored, fehlt im fresh Worktree)

```bash
cp /Users/valgard/Projects/private/core_keeper/item-checklist/.envrc \
   /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5c/.envrc
ls -la /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5c/.envrc
```

Expected: `.envrc` ~2kb present.

- [ ] **Step 3: Verify mask_sprite.png im worktree via symlink-Mechanik**

```bash
ls -la /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5c/unity/ItemChecklist/Art/Bridge/ | grep -E 'mask_sprite|white_pixel'
```

Expected: `mask_sprite.png` + `mask_sprite.png.meta` sichtbar (entweder als Datei oder symlink). `white_pixel.*` weg. Bridge-Folder selbst ist nicht gitignored im worktree (war im Branch-Snapshot).

Wenn `mask_sprite.png` fehlt: utils/link.sh nutzt eventuell SDK-side symlinks, nicht Bridge. Dann: explizit kopieren:

```bash
cp /Users/valgard/Projects/private/core_keeper/item-checklist/unity/ItemChecklist/Art/Bridge/mask_sprite.png* \
   /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5c/unity/ItemChecklist/Art/Bridge/
```

---

## Phase 2 — ItemRow.prefab YAML-Edits (5 Blöcke)

**Alle Tasks in dieser Phase laufen im Worktree:**
`cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5c`

### Task 4: ItemRow.prefab — Background SpriteRenderer

**Files:**
- Modify: `unity/ItemChecklist/Prefabs/ItemRow.prefab:143-155`

- [ ] **Step 1: Edit Background SpriteRenderer Layer/Order/MaskInteraction**

Use Edit-Tool with sufficient unique context. Old string (around Z143-156):
```yaml
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 15
  m_Sprite: {fileID: -1077625897, guid: 29505f6265ca17c439c0108555d66242, type: 3}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_FlipX: 0
  m_FlipY: 0
  m_DrawMode: 1
  m_Size: {x: 10, y: 2.5}
  m_AdaptiveModeThreshold: 0.5
  m_SpriteTileMode: 0
  m_WasSpriteAssigned: 1
  m_MaskInteraction: 0
  m_SpriteSortPoint: 0
```

New string:
```yaml
  m_SortingLayerID: 1241602095
  m_SortingLayer: 5
  m_SortingOrder: 45
  m_Sprite: {fileID: -1077625897, guid: 29505f6265ca17c439c0108555d66242, type: 3}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_FlipX: 0
  m_FlipY: 0
  m_DrawMode: 1
  m_Size: {x: 10, y: 2.5}
  m_AdaptiveModeThreshold: 0.5
  m_SpriteTileMode: 0
  m_WasSpriteAssigned: 1
  m_MaskInteraction: 1
  m_SpriteSortPoint: 0
```

Vier Field-Changes: Layer (0→1241602095), SortingLayer-byte (0→5), Order (15→45), MaskInteraction (0→1). Der `m_Sprite`-Block (mit dem unique guid `29505f6...` + DrawMode 1 + Size 10×2.5) ist die unique Diskriminierungs-Markierung gegenüber Icon/Checkmark.

- [ ] **Step 2: Verify edit applied**

```bash
sed -n '143,156p' unity/ItemChecklist/Prefabs/ItemRow.prefab
```

Expected: alle vier Field-Changes sichtbar.

- [ ] **Step 3: Commit WIP**

```bash
git add unity/ItemChecklist/Prefabs/ItemRow.prefab
git commit -m "wip(prefab): itemrow Background sprite → layer GUI order 45 + maskInteraction 1"
```

---

### Task 5: ItemRow.prefab — Icon SpriteRenderer

**Files:**
- Modify: `unity/ItemChecklist/Prefabs/ItemRow.prefab:230-243`

- [ ] **Step 1: Edit Icon SpriteRenderer Layer/Order/MaskInteraction**

Old string (around Z230-243):
```yaml
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 20
  m_Sprite: {fileID: 0}
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

New string (Order 15 → 48, Layer 0 → 1241602095/5, MaskInteraction 0 → 1):
```yaml
  m_SortingLayerID: 1241602095
  m_SortingLayer: 5
  m_SortingOrder: 48
  m_Sprite: {fileID: 0}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_FlipX: 0
  m_FlipY: 0
  m_DrawMode: 0
  m_Size: {x: 1, y: 1}
  m_AdaptiveModeThreshold: 0.5
  m_SpriteTileMode: 0
  m_WasSpriteAssigned: 0
  m_MaskInteraction: 1
  m_SpriteSortPoint: 0
```

Unique-Discriminator: `m_Sprite: {fileID: 0}` (Icon hat keinen Sprite-Asset im Prefab, wird runtime gesetzt) + `m_DrawMode: 0` + `m_Size: {x: 1, y: 1}` + `m_WasSpriteAssigned: 0`.

- [ ] **Step 2: Verify edit applied**

```bash
sed -n '230,243p' unity/ItemChecklist/Prefabs/ItemRow.prefab
```

Expected: alle vier Field-Changes sichtbar.

- [ ] **Step 3: Commit WIP**

```bash
git add unity/ItemChecklist/Prefabs/ItemRow.prefab
git commit -m "wip(prefab): itemrow Icon sprite → layer GUI order 48 + maskInteraction 1"
```

---

### Task 6: ItemRow.prefab — Label PugText.style

**Files:**
- Modify: `unity/ItemChecklist/Prefabs/ItemRow.prefab:323-329`

- [ ] **Step 1: Edit Label PugText.style sortingLayer + orderInLayer**

Old string (around Z323-330):
```yaml
    color: {r: 1, g: 1, b: 1, a: 1}
    outline: 0
    outlineColor: {r: 0, g: 0, b: 0, a: 0}
    supportColorTags: 0
    sortingLayer: -2147483648
    orderInLayer: 9999
    maskInteraction: 0
  styleOverrides: []
```

New string:
```yaml
    color: {r: 1, g: 1, b: 1, a: 1}
    outline: 0
    outlineColor: {r: 0, g: 0, b: 0, a: 0}
    supportColorTags: 0
    sortingLayer: 1241602095
    orderInLayer: 49
    maskInteraction: 1
  styleOverrides: []
```

Drei Changes: sortingLayer sentinel → explicit 1241602095, orderInLayer 9999 → 49, maskInteraction 0 → 1. Unique-Discriminator zu Placeholder ist `color: {r: 1, g: 1, b: 1, a: 1}` (Label ist weiß; Placeholder ist grau, siehe Task 7).

- [ ] **Step 2: Verify edit applied**

```bash
sed -n '323,330p' unity/ItemChecklist/Prefabs/ItemRow.prefab
```

Expected: drei Field-Changes sichtbar.

- [ ] **Step 3: Commit WIP**

```bash
git add unity/ItemChecklist/Prefabs/ItemRow.prefab
git commit -m "wip(prefab): itemrow Label PugText.style → layer GUI order 49 + maskInteraction 1"
```

---

### Task 7: ItemRow.prefab — Placeholder PugText.style

**Files:**
- Modify: `unity/ItemChecklist/Prefabs/ItemRow.prefab:412-418`

- [ ] **Step 1: Edit Placeholder PugText.style sortingLayer + orderInLayer**

Old string (around Z412-419):
```yaml
    color: {r: 0.7058824, g: 0.7058824, b: 0.7058824, a: 1}
    outline: 0
    outlineColor: {r: 0, g: 0, b: 0, a: 0}
    supportColorTags: 0
    sortingLayer: -2147483648
    orderInLayer: 9999
    maskInteraction: 0
  styleOverrides: []
```

New string:
```yaml
    color: {r: 0.7058824, g: 0.7058824, b: 0.7058824, a: 1}
    outline: 0
    outlineColor: {r: 0, g: 0, b: 0, a: 0}
    supportColorTags: 0
    sortingLayer: 1241602095
    orderInLayer: 49
    maskInteraction: 1
  styleOverrides: []
```

Drei Changes wie Task 6. Unique-Discriminator zu Label ist `color: {r: 0.7058824, ...}` (Placeholder ist grau RGBA ≈ 180/180/180).

- [ ] **Step 2: Verify edit applied**

```bash
sed -n '412,419p' unity/ItemChecklist/Prefabs/ItemRow.prefab
```

- [ ] **Step 3: Commit WIP**

```bash
git add unity/ItemChecklist/Prefabs/ItemRow.prefab
git commit -m "wip(prefab): itemrow Placeholder PugText.style → layer GUI order 49 + maskInteraction 1"
```

---

### Task 8: ItemRow.prefab — Checkmark SpriteRenderer

**Files:**
- Modify: `unity/ItemChecklist/Prefabs/ItemRow.prefab:495-508`

- [ ] **Step 1: Edit Checkmark SpriteRenderer Layer/Order/MaskInteraction**

Old string (around Z495-508):
```yaml
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 20
  m_Sprite: {fileID: 467336100, guid: 29505f6265ca17c439c0108555d66242, type: 3}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_FlipX: 0
  m_FlipY: 0
  m_DrawMode: 0
  m_Size: {x: 0.5, y: 0.5}
  m_AdaptiveModeThreshold: 0.5
  m_SpriteTileMode: 0
  m_WasSpriteAssigned: 1
  m_MaskInteraction: 0
  m_SpriteSortPoint: 0
```

New string (Order 20 → 50, Layer 0 → 1241602095/5, MaskInteraction 0 → 1):
```yaml
  m_SortingLayerID: 1241602095
  m_SortingLayer: 5
  m_SortingOrder: 50
  m_Sprite: {fileID: 467336100, guid: 29505f6265ca17c439c0108555d66242, type: 3}
  m_Color: {r: 1, g: 1, b: 1, a: 1}
  m_FlipX: 0
  m_FlipY: 0
  m_DrawMode: 0
  m_Size: {x: 0.5, y: 0.5}
  m_AdaptiveModeThreshold: 0.5
  m_SpriteTileMode: 0
  m_WasSpriteAssigned: 1
  m_MaskInteraction: 1
  m_SpriteSortPoint: 0
```

Unique-Discriminator: `m_Sprite: {fileID: 467336100, ...}` + `m_Size: {x: 0.5, y: 0.5}` (Checkmark ist klein, 0.5×0.5 Unity-units).

- [ ] **Step 2: Verify edit applied**

```bash
sed -n '495,508p' unity/ItemChecklist/Prefabs/ItemRow.prefab
```

- [ ] **Step 3: Verify all 5 ItemRow-Blöcke konsistent**

```bash
grep -nE 'm_SortingLayerID: 1241602095|m_SortingLayer: 5|sortingLayer: 1241602095|m_MaskInteraction: 1|maskInteraction: 1' unity/ItemChecklist/Prefabs/ItemRow.prefab
```

Expected: 5 Blöcke × 3-4 Lines = ~15 Treffer (3 SpriteRenderer mit je `m_SortingLayerID + m_SortingLayer + m_MaskInteraction`, 2 PugText mit je `sortingLayer + maskInteraction`).

Plus verifizieren dass keine alten Werte stehen geblieben sind:

```bash
grep -cE 'm_SortingLayerID: 0|sortingLayer: -2147483648|orderInLayer: 9999|m_MaskInteraction: 0|maskInteraction: 0' unity/ItemChecklist/Prefabs/ItemRow.prefab
```

Expected: `0` (keine Old-State-Lines mehr). Wenn > 0: ein Edit hat versäumt — Source identifizieren + fixen.

- [ ] **Step 4: Commit WIP**

```bash
git add unity/ItemChecklist/Prefabs/ItemRow.prefab
git commit -m "wip(prefab): itemrow Checkmark sprite → layer GUI order 50 + maskInteraction 1"
```

---

## Phase 3 — Window-Prefab Editor-Roundtrip (USER)

### Task 9: ItemChecklistWindow.prefab — ContentsMask via Unity Editor

**Files:**
- Modify: `unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab` (strukturelle Änderung)

**Diese Task braucht User-Interaktion (Unity Editor öffnen + Save Project).** Subagent kann das nicht autonom. Plan ruft hier User-zu-Subagent-Handoff aus.

- [ ] **Step 1: Prepare — UIScrollWindow-Bounds aus dem Window-Prefab ablesen** (für Mask-Position + -Scale)

```bash
grep -nE 'windowHeight|windowWidth|windowLocalCenter' unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab
```

Expected: drei Lines mit den konkreten Werten (z.B. `windowHeight: 5`, `windowWidth: 10`, `windowLocalCenter: {x: 0, y: -1}`). Diese Werte braucht der User in Step 6 (Transform-Settings).

- [ ] **Step 2: User-Action — Unity Editor öffnen**

Aus dem Worktree:
1. Unity Hub → `Open project` → Wähle `/Users/valgard/Projects/private/core_keeper/CoreKeeperModSDK/` (NICHT den Worktree direkt — der Editor läuft gegen das SDK-Projekt, mit symlinks ins Worktree)
2. Warte bis Editor lädt (~30-60s je nach Cache-Stand)
3. Navigate to `Assets/ItemChecklist/Prefabs/ItemChecklistWindow.prefab` im Project-Panel
4. Double-click → Prefab opens in scene view

- [ ] **Step 3: User-Action — ContentsMask GameObject erstellen**

1. Im Hierarchy: `RowsContainer` GameObject selektieren
2. Right-click → `Create Empty Child` → wird "GameObject" benannt
3. Im Inspector: Name auf `ContentsMask` ändern
4. Tag-Layer Dropdown (Inspector top right): auf `UI` (= numerischer Tag 5) setzen für Konsistenz mit anderen UI-Elementen

- [ ] **Step 4: User-Action — SpriteMask Component hinzufügen**

1. ContentsMask noch selektiert
2. Inspector: `Add Component` → search `Sprite Mask` → Add
3. SpriteMask sollte nun gelistet sein unter Transform

- [ ] **Step 5: User-Action — SpriteMask Settings**

Im SpriteMask Inspector folgende Felder setzen:

| Field | Value |
|---|---|
| Sprite | Drag `Assets/ItemChecklist/Art/Bridge/mask_sprite.png` (= das Asset aus Task 1) ins Feld; oder via kleinem Selector-Icon auswählen |
| Mask Source | `Sprite` (Default OK) |
| Alpha Cutoff | `0.2` |
| Custom Range | ☑ enable (Checkbox) |
| Front Sorting Layer | `GUI` (Dropdown) |
| Front Order in Layer | `55` |
| Back Sorting Layer | `GUI` |
| Back Order in Layer | `40` |

- [ ] **Step 6: User-Action — Transform Settings**

Im Transform-Inspector der ContentsMask:

| Field | Value | Note |
|---|---|---|
| Position X | (windowLocalCenter.x aus Step 1) | z.B. `0` |
| Position Y | (windowLocalCenter.y aus Step 1) | z.B. `-1` |
| Position Z | `0` | |
| Rotation | (0, 0, 0) | Default, nicht anfassen |
| Scale X | `(windowWidth + 0.5)` | z.B. wenn windowWidth=10: Scale X = 10.5 |
| Scale Y | `(windowHeight + 0.5)` | z.B. wenn windowHeight=5: Scale Y = 5.5 |
| Scale Z | `1` | |

- [ ] **Step 7: User-Action — Save Project**

`File` Menu → `Save Project` (oder `Cmd-S`)

Editor speichert die Prefab-Änderung. Geduldig warten (~5-15s) bis Asset-Database-Reimport durch ist.

- [ ] **Step 8: User-Action — Editor schließen**

`File` → `Quit Unity` (oder `Cmd-Q`)

Wichtig vor dem Build (Task 10): batchmode-Build erwartet Editor closed.

- [ ] **Step 9: Verify Window-Prefab changes (CLI)**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5c
git diff --stat unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab
```

Expected: 1 file changed, mit Insertions (typisch +60-100 lines für neues GameObject + SpriteMask + Transform-Block).

```bash
grep -nE 'ContentsMask|SpriteMask|m_IsCustomRangeActive|m_FrontSortingOrder: 55|m_BackSortingOrder: 40|m_MaskAlphaCutoff: 0.2' unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab
```

Expected: alle 6 Marker im Prefab gefunden:
- `m_Name: ContentsMask`
- `SpriteMask:` (yaml-anchor)
- `m_IsCustomRangeActive: 1`
- `m_FrontSortingOrder: 55`
- `m_BackSortingOrder: 40`
- `m_MaskAlphaCutoff: 0.2`

Plus: die mask_sprite.png GUID muss im SpriteMask m_Sprite-Field stehen:

```bash
MASK_GUID=$(cat /tmp/iter-3-5c-spike/mask-sprite-guid.txt | awk '{print $2}')
grep -c "$MASK_GUID" unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab
```

Expected: `1` (exakt eine Referenz im SpriteMask). Falls 0: Sprite-Reference im Editor nicht gesetzt → Step 5 wiederholen.

- [ ] **Step 10: Verify no unintended sideeffects from Editor reimport**

```bash
git diff --stat
```

Expected: nur `unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab` modified (+ optional `unity/ItemChecklist/Art/Bridge/mask_sprite.png.meta` falls Unity die .meta nochmal angefasst hat). Andere modifizierte Files: investigieren bevor commit.

- [ ] **Step 11: Commit**

```bash
git add unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab unity/ItemChecklist/Art/Bridge/mask_sprite.png.meta
git commit -m "feat(prefab): add ContentsMask + SpriteMask to ItemChecklistWindow"
```

Note: das `.meta`-Add ist conditional — nur wenn Unity es modifiziert hat. `git status` vor commit checken.

---

## Phase 4 — Build + Bundle-Inclusion-Verify

### Task 10: Build via utils/build.sh + Install-Verify

**Files:** None modified. Build only.

- [ ] **Step 1: Verify CK + Unity Editor closed** (build requires both closed)

```bash
/usr/bin/pgrep -fl "Core Keeper" || echo "ck-not-running"
/usr/bin/pgrep -fl "Unity.app/Contents/MacOS/Unity" || echo "editor-not-running"
```

Beide: "not-running".

- [ ] **Step 2: Build via batchmode**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5c
source .envrc
../../utils/build.sh
```

Expected: Output endet mit `BUILD SUCCEEDED` (oder ähnlicher Erfolgs-Indikator). Duration typischerweise 30-90s.

Falls Build fails: STOP, Output inspizieren. Häufige Failure-Modes:
- AssetBundle-Build error (Sprite nicht resolvable) → Risk #1: Sprite-Reference im Prefab broken, Task 9 Step 5 wiederholen
- Asmdef-Compile-Error → unbeabsichtigte Code-Änderung, `git diff` inspizieren

- [ ] **Step 3: Install-Verify on macOS bottle (per [[subagent-build-verify-install]])**

```bash
INSTALL_DIR=$(ls -d "/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Steam/"*/mod.io/5289/mods/9999997_1/ 2>/dev/null | head -1)
echo "Install dir: $INSTALL_DIR"
ls -la "$INSTALL_DIR"
```

Expected: Directory exists with `ModManifest.json`, `Scripts/`, `Bundles/`. Falls fehlt: install-macos.sh nicht gelaufen → manuell ausführen:

```bash
../../utils/install-macos.sh
```

- [ ] **Step 4: Verify Code unchanged**

```bash
ls "$INSTALL_DIR/Scripts/"
grep -l 'ItemChecklistWindow' "$INSTALL_DIR/Scripts/"*.cs
```

Expected: `ItemChecklistWindow.cs`, `ItemRow.cs`, `ItemChecklistContent.cs`, `ItemChecklistMod.cs` etc. — alle die gleichen .cs-Files wie vor Iter-3.5c (Zero-Code-Iteration).

---

### Task 11: Phase 1 Test — Asset-Bundle Inclusion Check

**Files:** None modified. Test only.

- [ ] **Step 1: Find Bundle-File**

```bash
INSTALL_DIR=$(ls -d "/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper/Steam/"*/mod.io/5289/mods/9999997_1/ | head -1)
ls "$INSTALL_DIR/Bundles/"
```

Expected: mindestens ein Bundle-File (typisch `itemchecklist` oder ähnlich) plus `*.manifest`.

- [ ] **Step 2: Inspect Bundle manifest for mask_sprite reference**

```bash
MASK_GUID=$(cat /tmp/iter-3-5c-spike/mask-sprite-guid.txt | awk '{print $2}')
grep -l "$MASK_GUID" "$INSTALL_DIR/Bundles/"*.manifest 2>/dev/null
```

Expected: mindestens ein Manifest-File enthält die GUID. Bundle-Manifests sind text-readable YAML und referenzieren alle gebundelten Asset-GUIDs.

Falls 0 matches: das Asset wurde NICHT ins Bundle aufgenommen → SpriteMask-Reference im Window-Prefab war broken im Build. Task 9 Steps 5+9 re-do.

- [ ] **Step 3: Verify Window-Prefab im Bundle** (sanity)

```bash
strings "$INSTALL_DIR/Bundles/"*.bundle 2>/dev/null | grep -E 'ContentsMask|ItemChecklistWindow' | head -3
```

Expected: mindestens "ItemChecklistWindow" + "ContentsMask" Strings im Bundle. Bundle ist binary; `strings` extrahiert Names.

- [ ] **Step 4: Phase 1 PASS marker**

```bash
git commit --allow-empty -m "test: phase-1 asset-bundle inclusion PASS"
```

---

## Phase 5 — Visual Tests (USER, Test-Phasen 2-6)

**Diese Tasks brauchen User-Interaktion am Spiel.** Subagent kann nicht autonom CK starten und visuell verifizieren.

### Task 12: Phase 2 Test — Iter-2/3/3.5 Regression (First-Open)

**Files:** None modified. Test only.

- [ ] **Step 1: Truncate Player.log**

```bash
LOG_DIR="/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper"
: > "$LOG_DIR/Player.log"
: > "$LOG_DIR/Player-prev.log"
```

- [ ] **Step 2: User-Action — CK starten, Test-Welt laden**

User-action: CrossOver Core Keeper starten; nach Hauptmenü die persönliche Test-Welt laden (wie in vorherigen Iter-Tests). NICHT in-game Mods menu öffnen (würde fake-ID dev-install wipen).

- [ ] **Step 3: User-Action — F1 drücken**

Visual verify:
- Window root visible an erwarteter Screen-Position
- Wood-Theme 9-slice Background sichtbar
- Title-Text "Item Checklist" sichtbar (NICHT geclippt)
- Mindestens 5-10 Rows rendern (Icon + Label sichtbar innerhalb Window)
- Cursor sichtbar
- WASD-Bewegung blockiert
- Scroll-Position startet oben (ResetScroll wirksam)

- [ ] **Step 4: Log-grep für errors**

```bash
LOG_DIR="/Users/valgard/Library/Application Support/CrossOver/Bottles/Core Keeper/drive_c/users/crossover/AppData/LocalLow/Pugstorm/Core Keeper"
grep -nE 'NullReferenceException|Exception|MissingComponentException|CompileFailed' "$LOG_DIR/Player.log"
```

Expected: keine Treffer (oder nur unrelated Vanilla-Engine-Warnings).

- [ ] **Step 5: F1 zweites Mal (Close-Test)**

Verify: Window schließt ohne Log-Errors; UI restored zu Pre-F1-State.

- [ ] **Step 6: Commit PASS marker**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5c
git commit --allow-empty -m "test: phase-2 iter-regression PASS"
```

---

### Task 13: Phase 3 Test — Multi-Open Pool Regression

**Files:** None modified. Test only.

- [ ] **Step 1: User-Action — F1→Esc→F1 ×3 in-world**

Drei Cycles. Nach jedem: Visual verify dass alle Row-Labels sichtbar bleiben (PugText-Pool-Fix aus Iter-3 nicht regressed).

- [ ] **Step 2: User-Action — Disconnect zur Hauptmenü**

`ESC` → `Quit to Main Menu`. Verify: Main-Menu PugText (Title, Button-Labels) rendern korrekt.

- [ ] **Step 3: Log-grep für PugText pool errors**

```bash
grep -niE 'PugText|pool' "$LOG_DIR/Player.log"
```

Expected: nur INFO-level Lines, keine Errors.

- [ ] **Step 4: Commit PASS marker**

```bash
git commit --allow-empty -m "test: phase-3 multi-open pool PASS"
```

---

### Task 14: Phase 4 Test — Scroll Regression

**Files:** None modified. Test only.

- [ ] **Step 1: User-Action — Re-enter Test-Welt, F1**

- [ ] **Step 2: User-Action — Mouse-wheel scroll im Window**

Verify:
- Scroll bewegt Rows vertikal innerhalb des Window-Viewports
- Bounds respected (kein run-off; bei Erreichen von top/bottom scrollt nicht weiter)
- F1 schließen + F1 öffnen: Scroll-Position resetted zu top (ResetScroll-Hook)

- [ ] **Step 3: Commit PASS marker**

```bash
git commit --allow-empty -m "test: phase-4 scroll regression PASS"
```

---

### Task 15: Phase 5 Test — Clipping Visual-Verification (Iter-3.5c-Kern, 0-Tolerance)

**Files:** None modified. Test only.

- [ ] **Step 1: F1 öffnen, scroll-Position = top**

Verify: oberste Rows voll sichtbar am Window-Top.

- [ ] **Step 2: Mouse-wheel DOWN — Rows sollen oben aus dem Wood-Theme-Rechteck herauslaufen**

Visual verify (0-Tolerance):
- Rows oberhalb der Wood-Theme-Top-Edge sind UNSICHTBAR (sowohl Icon-SpriteRenderer als auch Label-PugText als auch Checkmark)
- Keine half-rendered Rows partial above edge
- Smooth Übergang: Rows fade ein/aus an der Edge ohne Flicker

- [ ] **Step 3: Continue scrolling — Rows unten aus dem Rechteck herauslaufen**

Visual verify:
- Rows unterhalb der Wood-Theme-Bottom-Edge sind UNSICHTBAR (alle 3 Renderer-Types)

- [ ] **Step 4: Optional — Screenshot für die record**

User-action: Screenshot des Mid-Scroll-States für visuelle Dokumentation.

- [ ] **Step 5: 0-Tolerance Pass/Fail**

Falls ANY Row pokes outside the Wood-Theme rectangle (top oder bottom, beliebiges Element):
- **STRIKE 1 → revertieren**

```bash
git log --oneline -10
# Identify last commit before Task 4 (Background-Edit)
git reset --hard <commit-before-wip-prefab>
```

Dann eskalieren zu **Iter-3.5d** (Camera-Clip-Plane, uGUI-Pivot, oder Doppel-Mask). Iter-3.5c-Output bleibt als Reference.

- [ ] **Step 6: Commit PASS marker**

```bash
git commit --allow-empty -m "test: phase-5 clipping visual-verification PASS"
```

---

### Task 16: Phase 6 Test — Layout-Side-Effects-Check (Iter-3.5c-Kern, 0-Tolerance)

**Files:** None modified. Test only.

- [ ] **Step 1: Window noch offen — Title sichtbar?**

Title-Text "Item Checklist" muss voll lesbar sein, NICHT geclippt (sitzt im selben Layer wie Mask, aber Order 9999 ist outside `40..55`-Range).

- [ ] **Step 2: Wood-Theme-Background voll gerendert?**

Wood-Theme 9-slice darf NICHT geclippt sein (Layer Default Order 10 — Mask wirkt nicht auf diesen Layer).

- [ ] **Step 3: F1 schließen — keine Artefakte?**

Window aktiv-aus: keine sichtbare Mask-Region, keine residual Rows.

- [ ] **Step 4: Vanilla CK-UI öffnen (Inventory via `I`, Hotbar etc.)**

Verify: Player-Inventory grid + Hotbar rendern korrekt. Items in Inventory-Slots sind sichtbar, kein "verschwundenes" Item. Falls Vanilla-UI geclippt ist → Mask leakt aus dem ItemChecklist-Window in die globale UI-Hierarchie.

- [ ] **Step 5: 0-Tolerance Pass/Fail**

Falls Title, Background, oder Vanilla CK-UI geclippt ist:
- **STRIKE 1 → revertieren + Sorting-Order-Audit aus Task 0 Step 3 + Spec Risk #4 re-evaluieren**

- [ ] **Step 6: Commit PASS marker**

```bash
git commit --allow-empty -m "test: phase-6 layout-side-effects PASS"
```

---

## Phase 6 — Merge + Cleanup

### Task 17: Merge iter-3-5c → main + Worktree-Cleanup

**Files:** None modified. Git only.

- [ ] **Step 1: Rebase WIP commits in clean public history**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5c
git log --oneline main..HEAD
# Expected: 6 wip-prefab + 1 feat-prefab + 6 test-pass-marker = ~13 commits
git rebase -i main
```

Im interaktiven Rebase: alle wip + test commits zu EINEM finalen `feat`-Commit zusammenfassen (`squash` oder `fixup`).

Final commit message:
```
feat(prefab): clip itemchecklist rows via SpriteMask + sorting-layer GUI

Iter-3.5c. IB-1:1 portierte Clipping-Strategie:
- New ContentsMask GameObject + SpriteMask in ItemChecklistWindow.prefab
- Sprite source: new Art/Bridge/mask_sprite.png (1×1 weiß)
- ItemRow.prefab: alle 5 Renderer (Background/Icon/Label/Placeholder/
  Checkmark) auf Layer GUI (uniqueID 1241602095) mit Orders 45/48/49/49/50
  + MaskInteraction=1
- Window.Background bleibt auf Layer Default (unaffected by Mask)
- Title PugText sentinel-resolved auf Layer GUI Order 9999 (mask-immune)
- white_pixel.png + .meta entfernt (war Text-Stub, unreferenziert)

Zero-Code-Iteration: keine .cs-Änderungen.

6 Test-Phasen all PASS (asset-bundle inclusion, Iter-2/3/3.5 regression,
multi-open pool, scroll, clipping visual, layout-side-effects).

Spec: docs/superpowers/specs/2026-05-27-itemchecklist-ui-pivot-iter3-5c-design.md
```

- [ ] **Step 2: CWD aus dem Worktree raus**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist
pwd  # verify NOT inside .worktrees/iter-3-5c
```

(Per global CLAUDE.md: vor `git worktree remove` immer CWD aus dem Worktree.)

- [ ] **Step 3: FF-merge zu main**

```bash
git checkout main
git merge --ff-only iter-3-5c
git log --oneline -5
```

Expected: HEAD jetzt mit Iter-3.5c-Commit auf main. Falls FF fails: main divergiert seitdem — STOP, User fragen.

- [ ] **Step 4: Worktree pre-flight check** (per [[worktree-remove-preflight-check]])

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5c
git ls-files --others --exclude-standard
git ls-files --others -i --exclude-standard
```

Expected: untracked-list leer. Gitignored: nur `.envrc`. Falls andere Files: STOP, User fragen.

- [ ] **Step 5: md5-Compare .envrc**

```bash
md5 /Users/valgard/Projects/private/core_keeper/item-checklist/.envrc \
    /Users/valgard/Projects/private/core_keeper/item-checklist/.worktrees/iter-3-5c/.envrc
```

Expected: identische Hashes.

- [ ] **Step 6: CWD aus dem Worktree raus + Remove**

```bash
cd /Users/valgard/Projects/private/core_keeper/item-checklist
git worktree remove .worktrees/iter-3-5c
git branch -d iter-3-5c
git worktree list
git branch
```

Expected: nur main-worktree, nur main-branch.

- [ ] **Step 7: Memory-Update Suggestion**

Empfehle dem User folgendes Memory-Update in `~/.claude/projects/-Users-valgard-Projects-private-core-keeper/memory/project_item_checklist_ui_pivot_state.md`:
- Status: "Iter-3.5c DONE 2026-05-27 (Clipping via Prefab-Edits IB-1:1)"
- Pending entfernen: Iter-3.5c-Eintrag
- Pending bleibt: Iter-3.6 (DisplayName), Iter-4 (F1-Toggle UX), Iter-3.7 latent

Plus globaler Memory-Index in `~/.claude/projects/.../memory/MEMORY.md` entsprechend updaten.

Update wird **vom User selbst durchgeführt** (Memory-Files sind nicht im Repo).

- [ ] **Step 8: Spike-5-Addendum optional erweitern**

Falls Iter-3.5c erfolgreich (alle 6 Phasen PASS), kann Spike-5 ein Closure-Addendum bekommen:

```bash
cat >> docs/research/spike-5-uiscrollwindow-decompile.md << 'EOF'

## Closure 2026-05-27 — Iter-3.5c Clipping erfolgreich

IB's SpriteMask + Layer-GUI + Custom-Range 40..55 funktioniert 1:1 in
ItemChecklist nach Prefab-Edit auf ItemRow + Window. Zero-Code.
Clipping + Layout-Side-Effects-Check beide PASS.

Final commit: <commit-hash> on main.
EOF
git add docs/research/spike-5-uiscrollwindow-decompile.md
git commit -m "docs(spike-5): close iter-3.5c addendum"
```

---

## Spec Coverage Check (Self-Review)

| Spec Section | Covered by Task |
|---|---|
| Context (Iter-3.5b-Pivot, IB-1:1) | Plan Header + Architecture Section |
| Goals: Clipping | Tasks 4-9 (Prefab-Edits) + Tasks 15+16 (Visual-Verify) |
| Goals: Pattern-Treue | Tasks 4-9 (1:1 IB EntriesDivider+UnavailableHeader-Werte) |
| Goals: Zero-Code | File-Structure-Tabelle ("Unchanged: alle .cs"), Task 10 Step 4 (Code unchanged verify) |
| Goals: Zero-Regression | Tasks 12, 13, 14 (Phasen 2, 3, 4) |
| Non-Goals: keine Code-Änderungen | enforced by File-Structure (Unchanged) |
| Decision: Sprite-Quelle mask_sprite.png | Task 1 (Asset-Erstellung) + Task 2 (white_pixel-Delete) |
| Decision: Hybrid YAML+Editor | Phase 2 (YAML) + Task 9 (Editor) |
| Decision: Mask-Layer GUI uniqueID 1241602095 | Task 0 Step 2 (verify) + Task 4-8 (YAML-Edits) + Task 9 Step 5 (Editor) |
| Decision: Orders 45/48/49/49/50 | Task 4-8 (jeweils der Order-Wert im New-String) |
| Decision: Zero-Code | gesamter Plan |
| Architecture: ContentsMask Hierarchie | Task 9 Step 3 (RowsContainer als Parent) |
| Components: File-Inventur | Plan File-Structure-Tabelle |
| ItemRow.prefab Edits | Tasks 4-8 (jeder Renderer ein Task) |
| Window-Prefab Editor-Roundtrip | Task 9 |
| Asset-Creation | Task 1 |
| Testing Phasen 1-6 | Tasks 11-16 |
| Risk #1 (Bundle-Inclusion) | Task 11 (Phase 1 Test) |
| Risk #2 (TagManager uniqueID Drift) | Task 0 Step 2 (Pre-Build verify) |
| Risk #3 (Editor unintended changes) | Task 9 Step 10 (git diff stat sanity) |
| Risk #4 (Sorting-Konflikte) | Task 16 (Phase 6 0-Tolerance) |
| Risk #5 (1×1 Mask zu klein) | Task 1 Step 1 (1×1 sufficient by design; SpriteMask skaliert) |
| Risk #6 (YAML-edit integrity) | Tasks 4-8 (jeder mit verify-Step) |
| Risk #7 (Worktree-Hygiene) | Task 17 Step 4+5 (pre-flight + md5) |
| Risk #8 (Asset im main-Repo) | Task 1 Note + Task 2 (im main, NICHT worktree) |
| Risk #9 (Budget Overrun) | Plan-Budget implicit (18 Tasks, jeweils 2-5 min) |
| Worktree-Setup | Task 3 |
| Lessons-driven defaults | Throughout — WIP-Commits nach jedem Task, grep-on-install in Task 10, TagManager-Verify in Task 0 |

**No spec gaps found.**

**Placeholder scan:** keine TBD/TODO/vague gefunden. Alle `<commit-hash>`-Placeholder sind in Step 7+8 explizit als "fill at execution time" deklariert.

**Type consistency:** Layer-Werte (`1241602095`, byte-form `5`), Order-Werte (`45/48/49/49/50` + Mask-Range `40/55`), MaskInteraction-Wert (`1`) — alle konsistent zwischen Tasks 4-8 und Task 9. `mask_sprite.png` GUID wird in Task 1 generiert, in `/tmp/iter-3-5c-spike/mask-sprite-guid.txt` gecached, und in Tasks 9 + 11 referenziert.
