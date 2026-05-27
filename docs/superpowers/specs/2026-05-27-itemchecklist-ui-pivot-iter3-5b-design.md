# ItemChecklist UI Pivot — Iteration 3.5b Design (Clipping-Fix via SpriteMask)

**Date:** 2026-05-27
**Status:** Design approved (3 blocks). Pending: spec self-review → user review → writing-plans.
**Branch:** `iter-3-5b` (Worktree `REPO_ROOT/.worktrees/iter-3-5b/` aus `main` @ `968eef7`)
**Prerequisite reading:**
- Iter-3.5 Spec: `docs/superpowers/specs/2026-05-26-itemchecklist-ui-pivot-iter3-5-design.md`
- Iter-3 Spec: `docs/superpowers/specs/2026-05-26-itemchecklist-ui-pivot-iter3-design.md`
- Iter-2 Spec: `docs/superpowers/specs/2026-05-25-itemchecklist-ui-pivot-iter2-design.md`
- Spike-5 (UIScrollWindow Decompile + IB Deep-Analyse): `docs/research/spike-5-uiscrollwindow-decompile.md`
- Spike-4 (UI Architecture): `docs/research/spike-4-ui-architecture.md`

**Environment-Kontext:**
- CK Game-Version: `1.2.1.3-4986`
- CoreLib Runtime: `4.0.3` (binary identisch mit GitHub-Commit `3c99dc44`, von mod.io als "4.0.4"-Display gelabelt)
- CoreLib SDK Build-Time: `4.0.4` (Tag `31242dbe`)
- Iter-3.5 Mod-Stand: `ResetScroll()` + Prefab-Field-Wiring (commit `968eef7` auf main)

## Context

Iter-3.5 hat den Scroll-Mechanismus repariert. Das Window hat jetzt funktionierende Mouse-wheel-Scroll-Aktivierung via `ResetScroll()`. Es bleibt **ein** offener Bug aus dem Iter-3.5-Risk-Catalog (Pivot-Branch "Iter-3.5b"):

**Iter-3.5b — Viewport-Clipping fehlt.** Rows, die durch Scroll oben/unten aus dem sichtbaren Wood-Theme-Rechteck herauslaufen, bleiben sichtbar — sie erscheinen über/unter dem Window-Hintergrund, überlappen mit dem Title und mit Welt-UI ausserhalb des Windows.

Spike-5 hatte das IB-Window-Prefab nur "partial — 991 KB" durchgrept und die `ContentsMask`-Komponente nicht erfasst. Der neue Spike (dokumentiert in diesem Design + zu ergänzen in Spike-5) hat das Pattern lokalisiert:

- **IB clippt via `SpriteMask` + Sorting-Layer-Custom-Range**, NICHT via uGUI-RectMask2D.
- Mask hängt als **Geschwister** des scrollenden Containers (nicht als Kind) → bewegt sich nicht mit dem Content.
- **MaskInteraction wird runtime gesetzt**, nicht statisch im Display-Prefab: Alle 18 IB-Display-Prefabs haben `m_MaskInteraction: 0`. Das Runtime-Setup passiert in `ItemBrowserRegistry.AddEntryDisplay` (Z66-77): ein einfacher Loop über alle SpriteRenderer + PugText des Display-Objekts.

## Goals

- **Clipping**: Rows ausserhalb des UIScrollWindow-Viewports sind unsichtbar (sowohl SpriteRenderer als auch PugText).
- **Pattern-Treue**: 1:1-Port des IB-`SpriteMask`+CustomRange-Mechanismus, runtime-materialisiert.
- **Zero-Regression**: Iter-2/3/3.5-Wins (Pool-Leak-Fix, Scroll-Aktivierung, `ResetScroll`, Layout) bleiben intakt.

## Non-Goals

- Layout-Refactor des Window-Prefab (Mask-GameObject kommt ausschliesslich aus Code; kein Editor-Roundtrip).
- DisplayName-Fallback-Strategy (Iter-3.6, separat).
- Iter-4-Filter-Panel/History (auch wenn `ContentsMaskInstaller` für Iter-4 wiederverwendbar designt wird, wird er hier nicht für Iter-4 instanziiert).
- 4.0.4-CoreLib-Future-Compat (Iter-3.7, latent).

## Decisions Made During Brainstorming

| Question | Decision |
|---|---|
| Wie eng an IB's Mask-Pattern bleiben? | **A — IB-Pattern 1:1 portieren** (per [[reference-analysis-mandatory-when-provided]]) |
| Sprite-Quelle für ContentsMask | **A — Runtime-generiert via Texture2D + Sprite.Create** (vermeidet [[pugstorm-modbuilder-sprite-meta]]-Trap) |
| Mask-Materialisation | **A — Pure Runtime** (kein Unity-Editor-Roundtrip; Mask-Bounds runtime-derived aus `UIScrollWindow`) |
| Iter-Scope | **A — Strict Clipping-Fix only** |
| Code-Strukturierung | **A — Static Helper `ContentsMaskInstaller`** (separate File, wiederverwendbar für Iter-4) |

## Architecture — Hierarchie nach Iter-3.5b

```
ItemChecklistWindow (root, GameObject)
└── RowsContainer (GameObject mit UIScrollWindow)
    ├── ContentsMask  ← NEU, Kind von RowsContainer, Geschwister von Content
    │   └── (Component: SpriteMask)
    │         m_Sprite                    = runtime-generiertes 1×1 white-rect
    │         m_IsCustomRangeActive       = true
    │         m_FrontSortingLayerID/Order = UI / 55  (matched IB)
    │         m_BackSortingLayerID/Order  = UI / 40  (matched IB)
    │         localScale                  = (windowWidth + 0.5, windowHeight + 0.5, 1)  (ε = 0.5u buffer)
    │         localPosition               = (windowLocalCenter.x, windowLocalCenter.y, 0)
    │         alphaCutoff                 = 0.2f  (matched IB)
    └── Content (GameObject mit ItemChecklistContent : IScrollable)  ← scrollt
        └── ItemRow_0…N  (jeweils SpriteRenderer + PugText runtime auf VisibleInsideMask gesetzt)
```

**Anchor-Begründung:** Die Mask hängt am `RowsContainer` (= Geschwister vom scrollenden `Content`), nicht am `root` oder am `Content`:
- Wäre sie am `Content`, würde sie mitscrollen — Mask-Region würde sich mit dem Content verschieben → Clipping statisch falsch.
- Wäre sie am `root`, müsste die `RowsContainer.localPosition`-Translation zusätzlich für die Mask-Position addiert werden — mehr Coordinate-Math, mehr Bugs.

**Sorting-Layer-Range-Begründung:** Der Custom-Range (`Front Order > Row-Order > Back Order`) ist die zweite Selektor-Achse neben `MaskInteraction=VisibleInsideMask`. Nur Renderer, die BEIDE Bedingungen erfüllen, werden geclippt. Schützt Title/Background davor, fälschlich vom Mask betroffen zu werden, auch wenn jemand später `MaskInteraction=VisibleInsideMask` auf ihnen setzt.

## Components

### Neu: `unity/ItemChecklist/ui/ContentsMaskInstaller.cs`

```csharp
public static class ContentsMaskInstaller
{
    private static Texture2D _whiteTex;     // static cache
    private static Sprite    _whiteSprite;

    // One-shot per UIScrollWindow. Returns the created SpriteMask so callers
    // can reference it for tests or future per-frame updates.
    public static SpriteMask Install(Transform anchor, UIScrollWindow scrollWindow);

    // Per-row hook — mirrors ItemBrowserRegistry.AddEntryDisplay loop.
    public static void SetMaskInteractionRecursive(GameObject row, SpriteMaskInteraction mode);

    // Lazy-init of the 1×1 white-rect sprite. Static-cached for the AppDomain.
    private static Sprite GetOrCreateWhiteSprite();
}
```

### Update: `unity/ItemChecklist/ui/ItemChecklistWindow.cs`

Zwei Hooks:

| Hook | Stelle | Code |
|---|---|---|
| Einmaliger Mask-Setup | `Awake()`, nach `HideUI()` | `_contentsMask = ContentsMaskInstaller.Install(rowsContent.parent, scrollWindow);` |
| Per-Row MaskInteraction | `SpawnRows()`, nach `Object.Instantiate(rowPrefab, rowsContent)` | `ContentsMaskInstaller.SetMaskInteractionRecursive(go, SpriteMaskInteraction.VisibleInsideMask);` |

Neues Field: `private SpriteMask _contentsMask;` (cached für späteren Reference-Zugriff).

### Unverändert

- `unity/ItemChecklist/ui/ItemRow.cs`
- `unity/ItemChecklist/Prefabs/ItemRow.prefab` (MaskInteraction bleibt 0 im Prefab → wird runtime gesetzt; matched IB-Pattern, wo Display-Prefabs MaskInteraction=0 haben)
- `unity/ItemChecklist/Prefabs/ItemChecklistWindow.prefab` (keine neuen GameObjects)
- `unity/ItemChecklist/ui/ItemChecklistContent.cs`

## Data Flow

```
Awake (einmalig pro Window-Instanz)
  HideUI()
  Install(anchor = rowsContent.parent, scrollWindow)
    → new GameObject("ContentsMask")
       Layer 5 (UI), SetParent(anchor)
       localPosition = read(scrollWindow.windowLocalCenter)
       localScale    = read(scrollWindow.windowWidth, windowHeight) × ε-buffer
    → AddComponent<SpriteMask>()
       sprite               = GetOrCreateWhiteSprite()
       isCustomRangeActive  = true
       frontSortingLayerID  = SortingLayer.NameToID("UI") || 0
       frontSortingOrder    = 55  (matched IB)
       backSortingLayerID   = frontSortingLayerID
       backSortingOrder     = 40  (matched IB)
       alphaCutoff          = 0.2f

ShowUI (jedes F1-Open)
  root.SetActive(true)
  ApplyTheme()
  SpawnRows()
    ClearRows()
    foreach catalog item:
      go = Instantiate(rowPrefab, rowsContent)
      SetMaskInteractionRecursive(go, VisibleInsideMask)        ← NEU
        foreach SpriteRenderer in go.GetComponentsInChildren:    sr.maskInteraction = mode
        foreach PugText        in go.GetComponentsInChildren:    if (pt.style != null) pt.style.maskInteraction = mode
      row.Bind(...)
    scrollWindow-wiring (unverändert: SetScrollable + UpdateScrollHeight + ResetScroll)

HideUI / Cleanup
  ClearRows (unverändert: pugText.Clear() + Destroy)
  root.SetActive(false)  → deaktiviert ContentsMask transitiv (gleiche Hierarchie)
  Bei Mod-Shutdown: Window-GameObject zerstört → SpriteMask + (transitiv) Sprite/Texture cleanup via Unity-Object-Tree
```

## Error Handling & Edge Cases

| # | Edge Case | Mitigation |
|---|---|---|
| 1 | `windowHeight`/`windowWidth`/`windowLocalCenter` evtl. private | Pre-Flight Phase 0: ILSpyCmd-Decompile-Check (~2 min). Falls private → API.Reflection-Pfad analog `MiScrollable` mit cached MemberInfo. Im IB-Prefab YAML stehen die Werte als plaintext-fields → 90% Confidence: serialized + public. |
| 2 | Sorting-Layer "UI" existiert nicht als named Sorting-Layer (nur als Tag-Layer 5) | `SortingLayer.NameToID("UI")` → falls 0/-1: Fallback auf Default-SortingLayer (ID 0). Funktional gleich, solange Rows und Mask im selben SortingLayer sind. Warn-Log statt Crash. |
| 3 | `pugText.style == null` (NRE-Risk analog IB) | Null-Guard: `if (pugText.style != null)`. IB hat den nicht; wir bauen ihn als defensive Massnahme ein (Mod-Sandbox-NRE → CompileFailed-Cascade pro [[corekeeper-compile-fail-cascade]]). |
| 4 | Mask-Awake-Order vs UIScrollWindow.Awake | Irrelevant: wir lesen serialized Fields (Editor-set), nicht computed Fields. Funktioniert vor und nach UIScrollWindow.Awake. |
| 5 | Texture2D-Memory-Leak bei Mod-Reload | Static Cache (`_whiteTex`/`_whiteSprite`) → einmalig erzeugt pro AppDomain (~12 Bytes für 1×1-Texture). |
| 6 | Iter-4 Reuse (FilterPanel mit eigenem Mask) | API ist designt: `Install(anchor, scrollWindow)` ist generic — kein Window-spezifischer Code im Installer. |
| 7 | Sandbox-Block auf `SortingLayer.NameToID` oder `SpriteMask.AddComponent` | Phase-1 Smoke-Test mit nur Installer-Init (ohne Row-Hook); falls geblockt → [[pugstorm-sandbox-rules]]-Pattern: Method via Harmony auf vanilla-System patchen. |
| 8 | Sorting-Order-Konflikt mit Background/Title | Pre-Implementation Audit: Background-Renderer + Title-PugText im Window-Prefab gehören NICHT in den `40..55` Custom-Range (oder haben MaskInteraction=None). YAML-Check vor Implementation. |

## Testing — 7 Phasen

| # | Phase | Pass-Kriterium | Failure-Stop |
|---|---|---|---|
| **0** | **Pre-Flight** (vor Implementation) | `UIScrollWindow.windowHeight/Width/LocalCenter` Public-Access via 2-min ILSpyCmd-Check; `SortingLayer.NameToID("UI")` returns plausibel; Background/Title-Sorting-Order ausserhalb 40..55 | 1-Strike → Reflection-Pfad pre-emptiv einbauen statt nachträglich |
| 1 | Sandbox Compile | `Successfully compiled ItemChecklist safetyCheck=True`; kein CompileFailed Cascade ([[corekeeper-compile-fail-cascade]]) | 3-Strike → re-brainstorm |
| 2 | Iter-2/3/3.5 Regression (First-Open) | Window + Wood-Theme + Title + Rows + Cursor + WASD-Block + `ResetScroll` intakt auf erstem F1 | 1-Strike → STOP, Fix revertieren |
| 3 | Multi-Open Pool Regression | `pugText.Clear()`-Fix wirksam: F1→Escape→F1 ×3 + Disconnect-zu-Hauptmenü; alle Texte bleiben sichtbar | 1-Strike → STOP |
| 4 | Scroll Regression | Mouse-wheel scrollt vertikal innerhalb Viewport; `ResetScroll` greift bei jedem F1 | 1-Strike → STOP |
| **5** | **Clipping Visual-Verification** (Iter-3.5b-Kern) | Rows, die durch Scroll aus dem Wood-Theme-Rechteck herauslaufen (oben & unten), sind UNSICHTBAR — sowohl Icon-SpriteRenderer als auch Label-PugText | **0-Tolerance** → sofort revertieren, Pivot zu Iter-3.5c |
| **6** | **Layout-Side-Effects** | Title NICHT geclippt; Window-Background NICHT geclippt; andere UI-Mods/Vanilla-UI unaffected | **0-Tolerance** → revertieren (das wäre "Window kaputt" — schlimmer als kein Clipping) |

**Sequenz-Regel:** Phase 0 muss vor Phase 1 laufen; Phase 5/6 sind kritischer als Phase 4 (Clipping-Bug = User-sichtbarer Layout-Schaden).

**Test-Tool-Setup pro Phase:**
- Player.log + Player-prev.log truncate vor jedem Phase-Cycle
- Grep nach phase-spezifischen markern nach jedem Test
- Visual-Verification durch User (Screenshot wenn möglich) speziell für Phasen 5 + 6

## Risks + Failure-Modes

| # | Risk | Mitigation |
|---|---|---|
| 1 | Sandbox blockiert `SpriteMask`-Construction oder `AddComponent<SpriteMask>` | Phase-1 Smoke-Test mit nur Installer-Init (ohne Row-Hook); falls geblockt → [[pugstorm-sandbox-rules]]: Harmony auf vanilla-CK-Mask-Construction patchen |
| 2 | Sorting-Layer-IDs Editor↔Runtime divergent | Phase-0 Pre-Flight loggt `SortingLayer.layers`; fallback Hardcoded IB-Werte (`frontSortingLayerID=1241602095`, Orders 55/40) |
| 3 | Mask clippt Title/Background versehentlich | 0-Tolerance Phase 6 + Pre-Implementation Sorting-Order-Audit (Edge-Case #8) |
| 4 | `windowHeight/Width/LocalCenter` private | Phase-0 Pre-Flight; Reflection-Pfad analog `MiScrollable` |
| 5 | `pugText.style == null` NRE | Defensive Null-Guard im `SetMaskInteractionRecursive` |
| 6 | Texture2D Memory-Leak | Static `_whiteTex`/`_whiteSprite`-Cache; einmalige Allocation per AppDomain |
| 7 | Worktree-Setup-Hygiene | [[worktree-remove-preflight-check]] + `.envrc` copy ins Worktree; bridge-sprites bleiben gitignored im main-Repo |
| 8 | Subagent-Build-Report False-Positives | [[subagent-build-verify-install]] — grep auf source-marker-line nach jedem Build (verdächtige Symptome: Build-Dauer <60s, Install-mtime älter als Source) |
| 9 | Iter-Budget-Overrun | Budget: 0.5 d Pre-Flight + Spike-Validierung, 1 d Implementation + Phasen 1–4, 0.5 d Phasen 5–6. Bei Fail in Phase 5/6 → Iter-3.5c (alternative Clipping-Strategy, z.B. Camera-Clip-Plane oder uGUI-RectMask2D-Pivot) |

## Worktree-Setup (Pre-Flight)

```
1. Branch:        iter-3-5b
2. Worktree:      REPO_ROOT/.worktrees/iter-3-5b/  (aus main @ 968eef7; Iter-3.5 ist gemergt)
3. cp .envrc      → .worktrees/iter-3-5b/.envrc
4. Verify bridge-sprites in main-Repo Art/Bridge/ vorhanden (gitignored, persistent)
5. which ilspycmd (Decompile-Tool für Phase 0, install via brew falls fehlt)
```

## Lessons-driven Defaults

- WIP-Commit-Vorschläge nach jedem Plan-Step (Phase 0, Installer-Skeleton, Window-Hook, Phase-Tests) per [[frequent-wip-commits-for-bisect]]
- Subagent-Build-Reports mit grep gegenchecken per [[subagent-build-verify-install]]
- Worktree pre-flight check vor remove per [[worktree-remove-preflight-check]]
- Reference-Analyse Spike-5-Lücke aktiv geschlossen (Was/Wie/Warum für IB's SpriteMask + ItemBrowserRegistry-Hook) per [[reference-analysis-mandatory-when-provided]]
- Sandbox-Constraints up-front beachten (Phase-1 Smoke-Test) per [[pugstorm-sandbox-rules]]
- Deep-Spike vor Trial-and-Error (Phase 0 Pre-Flight statt Re-Iteration-Cycle) per [[deep-spike-unfamiliar-internals]]

## References

- Iter-3.5 Spec: `docs/superpowers/specs/2026-05-26-itemchecklist-ui-pivot-iter3-5-design.md`
- Iter-3 Spec: `docs/superpowers/specs/2026-05-26-itemchecklist-ui-pivot-iter3-design.md`
- Iter-2 Spec: `docs/superpowers/specs/2026-05-25-itemchecklist-ui-pivot-iter2-design.md`
- Iter-1 Spec: `docs/superpowers/specs/2026-05-25-itemchecklist-ui-pivot-iter1-design.md`
- Spike-5 (UIScrollWindow Decompile + IB Deep-Analyse): `docs/research/spike-5-uiscrollwindow-decompile.md` — Spike-5 wird in Iter-3.5b um den Mask-Mechanismus erweitert (separate Spike-Sub-Section oder Spike-6, je nach Plan).
- Spike-4 (UI Architecture): `docs/research/spike-4-ui-architecture.md`
- IB Source (Reference): `/tmp/ck-ui-research/ItemBrowser/ItemBrowserPackage/`
  - `Prefabs/Browser/ItemBrowserUI.prefab` (Z3037-3123: ContentsMask + SpriteMask)
  - `Scripts/Common/Api/ItemBrowserRegistry.cs` (Z66-77: AddEntryDisplay-Mask-Hook)
- Memory: `[[reference-analysis-mandatory-when-provided]]`, `[[deep-spike-unfamiliar-internals]]`, `[[subagent-build-verify-install]]`, `[[worktree-remove-preflight-check]]`, `[[frequent-wip-commits-for-bisect]]`, `[[item-checklist-ui-pivot-state]]`, `[[corekeeper-ui-pattern]]`, `[[pugstorm-sandbox-rules]]`, `[[pugstorm-modbuilder-sprite-meta]]`, `[[corekeeper-compile-fail-cascade]]`
