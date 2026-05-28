# ItemChecklist — Iteration 3.7 Design (Variation-aware Cooked-Food Tracking)

**Date:** 2026-05-28
**Status:** Design approved (5 sections). Pending: spec self-review → user review → writing-plans.
**Branch:** `iter-3-7` (Worktree `REPO_ROOT/.worktrees/iter-3-7/` aus `main` @ `94baf8c`)
**Prerequisite reading:**
- Iter-3.6 Spec (DisplayName-Resolution, schließt Iter-3-Quartett ab):
  `docs/superpowers/specs/2026-05-28-itemchecklist-displayname-iter3-6-design.md`
- Iter-3.7 Cooked-Food Discovery Spike (empirische Pre-Work):
  `docs/research/iter-3-7-cooked-food-discovery-spike.md`
- `Iter37PoolSpike.cs` Output (siehe Player.log mit `[I37-POOL]` und `[I37-ALPHA]`
  prefixes vom 2026-05-28-Spike-Run) — Spike-Code wurde nach Verifikation entfernt
  (git-History bewahrt ihn)
- ItemCatalog aktueller Stand: `unity/ItemChecklist/ItemCatalog.cs`
- ItemBrowser-Vergleich `CookBookUI`: CKs eigenes Cooked-Food-UI mit
  Slot-Pool-Recycling-Pattern als Iter-3.8-Virtualisierungs-Referenz

**Environment-Kontext:**
- CK Game-Version: `1.2.1.4-7f74` (Patch 4 freigeschaltet via `corekeeper-patch`)
- CoreLib Runtime: `4.0.3` (mod.io-display "4.0.4")
- Iter-3.6-Stand: produktiv auf main (`94baf8c`), DisplayName-Resolution
  abgeschlossen inkl. Loc-Change-Hook + WorldLoad-Hook + DE-Locale-TrimStart-Fix
- HEAD vor Iter-3.7-Start: `94baf8c` (Iter-3.6 Spike-5-Close)

## Context

CK trackt entdeckte Cooked-Food-Items pro **Permutation** der zwei Ingredients
(Pilz-Suppe ≠ Tomaten-Suppe), nicht pro Familie. Iter-3.6's ItemChecklist
behandelt sie alle als denselben `objectID` (z.B. `CookedSoup`) und zeigt nur
einen einzigen Catalog-Entry pro Cooked-Food-Familie — der bei der ersten
beliebigen Permutation als „entdeckt" gilt. Das User-Reported-Symptom war
„CK zeigt separate New-Item-Notifications für jede Permutation, aber
ItemChecklist sieht nur eine".

Der Iter-3.7-Refactor stellt das auf **variation-aware** Tracking um, sodass
jede konkrete `(objectID, variation)`-Permutation als eigener Catalog-Entry
und Discovery-Token gezählt wird. Vom Datenmodell (Catalog + State + Hooks
+ UI) bleibt fast alles strukturell gleich — der Schlüssel wird von `int`
(objectID) auf gepackt-`long` (`objectID << 32 | variation`) erweitert.

Catalog wächst dadurch von heute ~200 Entries auf ~9680 (200 Non-Cooked +
9480 Cooked-Food-Permutationen über 15 Base-Familien × 3 Tiers).

## Goals

- **Variation-aware Discovery-Tracking**: jede konkrete `(objectID,
  variation)`-Permutation ist eigener Catalog-Entry und Discovery-Token —
  Pilz-Suppe ≠ Tomaten-Suppe in Quote und Listing
- **Alle 3 Tiers (Base/Rare/Epic) separat enumeriert**: Rare/Epic-Tiers droppen
  talent- und RNG-abhängig; alle drei gehören in die Gesamt-Quote, sonst kann
  ein Player mit hohem Cooking-Talent seine zusätzlichen Discoveries nicht
  reflektieren
- **Deterministic Pick-Family (α-Algorithmus)**: jede Pair → 1 Family wird via
  `primary.turnsIntoFood` aufgelöst, ohne Brute-Force-Enumeration. ~5ms
  Pick-Overhead für die volle 9480-Permutationen-Liste (Spike-empirisch belegt)
- **Eine einzige Gesamt-Quote**: Title zeigt `Item Checklist — N / M` (z.B.
  `47 / 9680`), keine Familien-/Tier-Sub-Counter
- **Zero-Regression**: alle bisherigen Iter-Wins (Pool-Leak-Fix, Scroll,
  Clipping, DisplayName-Resolution, Loc-Change-Hook, WorldLoad-Hook) bleiben
  intakt
- **Save-Kompatibilität ohne Migration**: existierende Iter-3.6-Saves laden
  korrekt, weil CK's Save-Format `(objectID, variation)` schon immer enthielt
  — Iter-3.6 hat nur die variation beim Lesen verworfen, Iter-3.7 liest beides

## Non-Goals

- UI-Virtualisierung mit Slot-Pool-Recycling (Iter-3.8 falls Performance-
  Mess-Plan triggert; Reference-Pattern: CKs `CookBookUI.UpdateFilter`)
- Coroutine-Bake (yield über GetObjectName-Batches) — Iter-3.8 falls
  sync-Bake-Lag > 8s gemessen wird
- Single-Row-Live-Update bei Discovery — Iter-3.7 refreshed nur Title; Row-
  Update beim nächsten Window-Open ist heutiges UX-Verhalten
- Mod-aware Origin-Aggregation für Cooked-Food (Mod-Ingredient + Vanilla-Output
  bleibt als „Vanilla" gemarkt) — Iter-3.8-Polish
- Per-Skin-Tracking für Tool-Variations (Iter-3.8+; „Phase 2 may revisit" laut
  Iter-3.6-Code-Comment)
- F1-Toggle (Iter-4)
- Listen-Sortierung über alphabetisch hinaus (Iter-5)
- Filter-Dropdown + Suche (Iter-6) — bemerkenswert weil CKs CookBookUI das
  bereits hat (`CookBookIngredientTypeFilterUI`, `CookBookIngredientFilterUI`)
- Window-/Style-Polish (Iter-7)

## Empirical Findings (Pre-Design-Lock-in)

### ILSpy Decompile (`Pug.ECS.Components.dll`, `Pug.Base.dll`, `Pug.Other.dll`)

- `CookedFoodCD.GetFoodVariation(a, b)` = `(primary << 16) | secondary` mit
  deterministischem Tiebreaker via `Random.CreateFromIndex` (Seed `87931`).
  Resultat: **symmetrisch** — `GetFoodVariation(a, b) == GetFoodVariation(b, a)`
- `CookedFoodCD.rareVersion` / `epicVersion`: ObjectID-Felder auf jedem Family-
  Item, zeigen auf die Tier-Varianten. Tier-Items selbst zeigen mit
  rareVersion/epicVersion auf sich selbst (Selbst-Bezug bei CookedSoupRare:
  `rare=CookedSoupRare, epic=CookedSoupEpic`)
- `ObjectIDExtensions.IsCookedFood`: `objectID ∈ [9500, 9599]` (max 100 Family-
  Item-Slots reserviert)
- `CookingIngredientCD`: hat Felder `turnsIntoFood: ObjectID` (Default-Family
  des Ingredients) und `ingredientType: IngredientType` (None/Plant/Fish/Meat)
- **Pick-Family-Logik** in nicht-Burst-Form aus `InventoryUtility.cs:~1626`:
  ```csharp
  ObjectID turnsIntoFood = ingredientLookup[primaryPrefabEntity].turnsIntoFood;
  ```
  Family des Outputs = `turnsIntoFood`-Feld des Primary-Ingredients. **Keine
  Tiebreaker-Tabelle**, kein Multi-Stage-Picking. Primary wird via
  `CookedFoodCD.GetPrimaryIngredient(i1, i2)` ausgewählt
- `IsIngredientObsolete`: explizite Skip-Liste (`GiantMushroom`, `AmberLarva`).
  Iter-3.7 filtert ebenfalls via dieser Methode

### Runtime-Spike (`Iter37PoolSpike.cs`, 2026-05-28-Run)

- **45 Family-Items in PugDatabase**, alle mit `CookedFoodCD` (15 base × 3
  Tiers): Cake, Cereal, Cheese, DipSnack, Fillet, FishBalls, PanCurry,
  Pudding, Salad, Sandwich, Smoothie, Soup, Steak, Sushi, Wrap
- **79 Cooking-Ingredients in PugDatabase**: 26 Plant + 44 Fish + 9 Meat
- **3160 unique Pairs**: `79 × 80 / 2`
- **9480 Cooked-Food-Permutationen**: `3160 pairs × 3 tiers`
- **Cooked-Food-Permutationen sind NICHT in `PugDatabase.objectsByType.Keys`
  registriert** — sie werden runtime beim Cooking generiert. Iter-3.7 muss sie
  aktiv enumerieren (sync via α im Bake)
- **α-Pick-Korrektheit empirisch verifiziert**:
  - `Mushroom×Mushroom → CookedSoup, var=360453500` (OK)
  - `Mushroom×GlowingTulipFlower → CookedSalad, var=524686716` (OK)
- **α-Performance**: 3160 Pair-Iterationen in 1,7ms (= 0,0005ms/pair)
- **Sandbox-Coverage**: alle benötigten APIs (`GetPrimaryIngredient`,
  `GetFoodVariation`, `IsIngredientObsolete`, `TryGetComponent<CookingIngredientCD>`)
  sandbox-safe verifiziert

### Konservativ-Annahme

**× 3 Tier-Multiplikator nicht direkt empirisch verifiziert**: der Discovery-
Spike sah nur Base-Tier-Events, weil der Test-Char zu wenig Cooking-Talent
für Rare/Epic-Drops hatte. Iter-3.7 nimmt konservativ an, dass alle 3 Tiers
separate Discovery-Tokens sind (= 9480 Catalog-Entries). Falls in der Praxis
nur Base-Tier discoverable ist, schrumpft die effektive Reach auf 3160 — die
unentdeckten Tier-Entries bleiben als grayed-out Rows sichtbar, kein
Correctness-Issue.

## Architecture: Data-Model

### DiscoveredState — Key-Schema-Update

**Iter-3.6:**
```csharp
private readonly HashSet<int> ids;
public bool IsDiscovered(int objectId);
internal void AddOne(int objectId);
public event Action<int> Discovered;
```

**Iter-3.7:**
```csharp
private readonly HashSet<long> keys;     // packed (objectId << 32) | (uint)variation

public static long PackKey(int objectId, int variation) =>
    ((long)objectId << 32) | (uint)variation;

public bool IsDiscovered(int objectId, int variation) =>
    keys.Contains(PackKey(objectId, variation));
internal void AddOne(int objectId, int variation);
internal void Snapshot(IEnumerable<long> packedKeys);
public event Action<int, int> Discovered;   // (objectId, variation)
public event Action Changed;                 // unchanged
public int Count => keys.Count;              // O(1)
```

Begründung für `long`-Packing statt `(int, int)`-Tuple-Struct als Key:
- `HashSet<long>` ist GC-frei, equality + hashing primitiv
- `(int)variation` cast preserved 32-bit-Identität — negative Werte sind via
  CookedFoodCD's `>>> 16` möglich, deshalb `(uint)variation` für Lower-Word
- Trivial debugbar: lower-32 = variation (0 für non-cooked), upper-32 = objectId

### ItemCatalog — Key-Schema-Update

**Iter-3.6:**
```csharp
private readonly Dictionary<int, int> idToIndex;
public bool TryGetIndex(int objectId, out int index);
```

**Iter-3.7:**
```csharp
private readonly Dictionary<long, int> keyToIndex;
public bool TryGetIndex(int objectId, int variation, out int index);
```

`Entry`-Struct bleibt strukturell unverändert — das `Variation`-Feld existiert
seit Iter-3.6, wurde nur immer `0` gesetzt. Iter-3.7 schreibt jetzt echte
Werte hinein.

## ItemCatalog.Bake() — Refactor mit α-Enumeration

Zwei-Loop-Architektur: bestehender Standard-Loop für Non-Cooked-Items
unverändert, neuer α-Loop für Cooked-Food-Permutationen.

```csharp
public void Bake() {
    // Re-entrance + PugDatabase-readiness guards bleiben (Iter-3.6 Code).

    var modIdToName = BuildModIdMap();                                  // unverändert
    var localizedNames   = new Dictionary<long, string>();              // key = PackKey
    var unlocalizedNames = new Dictionary<long, string>();
    var iconCache        = new Dictionary<long, Sprite>();
    var accepted         = new List<(ObjectID id, int variation)>();

    // ─── Loop 1: Standard-Items (~200) ──────────────────────────────
    foreach (var od in PugDatabase.objectsByType.Keys) {
        if (od.variation != 0) continue;
        if (od.objectID.IsCookedFood()) continue;            // NEU — Family-Items rausfiltern
        // … objectType-Discrimination wie Iter-3.6 …
        ResolveNamesAndIcon(od);
        accepted.Add((od.objectID, 0));
    }

    // ─── Loop 2: α-Enumeration für Cooked-Food-Permutationen ────────
    // Pre-cache: ingredient → turnsIntoFood + family → (rareId, epicId)
    var turnsInto = new Dictionary<ObjectID, ObjectID>();
    var tierMap = new Dictionary<ObjectID, (ObjectID rare, ObjectID epic)>();
    foreach (var od in PugDatabase.objectsByType.Keys) {
        if (od.variation != 0) continue;
        if (PugDatabase.TryGetComponent<CookingIngredientCD>(od, out var ing)
            && !CookedFoodCD.IsIngredientObsolete(od.objectID)) {
            turnsInto[od.objectID] = ing.turnsIntoFood;
        }
        if (od.objectID.IsCookedFood()
            && PugDatabase.TryGetComponent<CookedFoodCD>(od, out var cf)) {
            tierMap[od.objectID] = (cf.rareVersion, cf.epicVersion);
        }
    }

    var ingredients = turnsInto.Keys.ToList();
    for (int i = 0; i < ingredients.Count; i++)
    for (int j = i; j < ingredients.Count; j++) {            // symmetry: j >= i
        var i1 = ingredients[i];
        var i2 = ingredients[j];
        var primary = CookedFoodCD.GetPrimaryIngredient(i1, i2);
        var baseFamily = turnsInto[primary];
        var variation = CookedFoodCD.GetFoodVariation(i1, i2);

        // 3 Tier-Variants pro Pair, gleiche variation, verschiedene objectIDs
        AddCookedEntry(baseFamily, variation);
        if (tierMap.TryGetValue(baseFamily, out var tiers)) {
            if (tiers.rare != ObjectID.None) AddCookedEntry(tiers.rare, variation);
            if (tiers.epic != ObjectID.None) AddCookedEntry(tiers.epic, variation);
        }
    }

    // ─── Final-Pass: Conflict-Disambiguation + Sort + keyToIndex ────
    // Bestehende Iter-3.6-Logik, mit `long`-Key statt `int`.
    // …
}

void AddCookedEntry(ObjectID family, int variation) {
    var od = new ObjectDataCD { objectID = family, variation = variation };
    ResolveNamesAndIcon(od);
    accepted.Add((family, variation));
}
```

### Resultierende Catalog-Größe (konsistent mit Spike-Bench)

| Kategorie | Anzahl |
|---|---|
| Non-Cooked-Items (Loop 1) | ~200 |
| Cooked-Food-Permutationen Base-Tier (Loop 2) | 3160 |
| Cooked-Food-Permutationen Rare-Tier | 3160 |
| Cooked-Food-Permutationen Epic-Tier | 3160 |
| **Total Catalog-Entries** | **~9680** |

### Gelöschter Code aus Iter-3.6

- `ItemCatalog.cs:155-159` TrimStart-Workaround für `" -Suppe"`-Garbage:
  nicht mehr nötig, weil Loop 1 die 45 Family-Items via `IsCookedFood()`-Filter
  ausschließt und Loop 2 immer konkrete Pairs in den Placeholder einsetzt.
  Bonus: das Iter-3.6-Known-Issue „Epische -Torte" mit Doppel-Space wird damit
  automatisch fixiert

### Erwartete Bake-Zeit

Basierend auf Spike-Bench + Hochrechnung der `GetObjectName`-Kosten:

| Komponente | 200 Items (Iter-3.6) | 9680 Items (Iter-3.7) |
|---|---|---|
| Pick-Logik (α) | n/a | ~5ms |
| `GetObjectName` × 2 pro Entry | ~100-300ms | ~3-6s (linear hochgerechnet) |
| Sortierung + Dict-Building | ~5ms | ~50-100ms |
| **Total Bake** | ~150-300ms | **~3.5-6s** |

Akzeptabel als sync-Bake; Coroutine-Refactor ist Iter-3.8-Punt falls
empirisch nötig (Trigger-Schwelle: > 8s).

## Hooks

### SaveManagerDiscoveryHook (1-Zeilen-Edit)

```csharp
[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SetObjectAsDiscovered))]
internal static class SaveManagerDiscoveryHook {
    [HarmonyPostfix]
    static void After(ObjectDataCD objectData, bool __result) {
        if (!__result) return;
        DiscoveredState.Instance.AddOne(
            (int)objectData.objectID,
            objectData.variation);                  // ← einzige neue Zeile
    }
}
```

Der `__result == true`-Filter bleibt — Iter-3.6-Befund: ohne ihn flutet
`DetectUndiscoveredObjectsInInventory` den Hook (~261× in 30s).

### CharacterDataDiscoverySnapshot

Heute (`CharacterDataDiscoverySnapshot.cs:63-68`):
```csharp
ids = new int[count];
for (int i = 0; i < count; i++)
    ids[i] = (int) __instance.discoveredObjects2[i].objectID;
```

Iter-3.7:
```csharp
var keys = new long[count];
for (int i = 0; i < count; i++) {
    var record = __instance.discoveredObjects2[i];
    keys[i] = DiscoveredState.PackKey((int)record.objectID, record.variation);
}
Cache[guid] = keys;
```

`Cache: Dictionary<string, long[]>` (war `int[]` in Iter-3.6).

### Save-Migration

**Keine.** CK's `discoveredObjects2`-Save-Format enthält seit immer beide
Felder. Iter-3.6 hat nur die variation beim Lesen verworfen, im Save selbst
standen schon immer korrekte `(objectID, variation)`-Tupel.

Phantom-Key-Edge-Case (`(CookedSoup, 0)` aus hypothetischem CK-Bug):
DiscoveredState toleriert solche Keys (landen im HashSet), Catalog hat keinen
Entry dafür, UI rendert sie nicht. Quote-Berechnung bleibt konsistent.

## UI-Anpassungen

### ItemRow.Bind — keine Signatur-Änderung

```csharp
// Bleibt:
public void Bind(int objectId, Sprite iconSprite, string name, bool isDiscovered)
```

Bind macht selbst kein State-Lookup; `objectId` wird in der Methode aktuell
nicht genutzt (nur `isDiscovered`-Branch). Bleibt aus API-Konsistenz drin.

### ItemChecklistWindow.SpawnRows — Einzeiler-Edit

Heute (`ItemChecklistWindow.cs:104`):
```csharp
row.Bind(entry.ObjectId, entry.Icon, entry.DisplayName,
    state.IsDiscovered(entry.ObjectId));
```

Iter-3.7:
```csharp
row.Bind(entry.ObjectId, entry.Icon, entry.DisplayName,
    state.IsDiscovered(entry.ObjectId, entry.Variation));
```

### Quote-Display im Title

Heute (`ItemChecklistWindow.cs:75`):
```csharp
title.Render("Item Checklist");
```

Iter-3.7:
```csharp
title.Render(FormatTitle());

private string FormatTitle() {
    var catalog = ItemChecklistMod.Catalog;
    var state = DiscoveredState.Instance;
    if (catalog == null || catalog.Count == 0)
        return "Item Checklist";
    return $"Item Checklist — {state.Count} / {catalog.Count}";
}
```

Format: eine einzige Gesamt-Zahl (z.B. `"Item Checklist — 47 / 9680"`).

Quote-Berechnung-Strategie: O(1) via `state.Count / catalog.Count`. Phantom-
Keys werden in der Praxis nicht vorkommen; falls doch, ist die Quote 0.1%
off — Quality-Issue, kein Correctness-Issue. Catalog-Scan-Variante als
1-Zeilen-Fallback bei empirischen Phantoms verfügbar.

### Live-Refresh-Wiring

Bei jedem `DiscoveredState.Changed`-Event refreshed der Title:
```csharp
DiscoveredState.Instance.Changed += OnDiscoveryChanged;

private void OnDiscoveryChanged() {
    if (!gameObject.activeSelf) return;
    // KEIN SpawnRows() — wäre 9680 GameObjects neu spawnen
    title.Render(FormatTitle());
}
```

Single-Row-Live-Update bei Discovery ist Iter-3.8-Polish. Iter-3.7-UX:
betroffene Row wird beim nächsten Window-Open korrekt gerendert (gleich wie
heute).

### Was sich nicht ändert

- `IScrollable` + `UIScrollWindow`-Setup (`ItemChecklistContent.cs`)
- `ApplyTheme` (CraftingUI-Theme)
- `ClearRows` + `PugText.Clear`-Pool-Leak-Fix
- `ItemCatalogLocChangeHook` → Bake + RebindRows
- `ItemCatalogWorldLoadHook` → Bake-Trigger
- `SaveManagerActiveSelectHook` Active-Character-Resolution via
  `AwaitingActiveDeserialize`-Pattern

## Querschnitts-Themen

### Sandbox-Risiken & Mitigations

| Risiko | Mitigation |
|---|---|
| `CookedFoodCD.GetPrimaryIngredient` blockiert | Spike-verifiziert sandbox-safe ✓ |
| `CookedFoodCD.GetFoodVariation` blockiert | Spike-verifiziert sandbox-safe ✓ |
| `CookedFoodCD.IsIngredientObsolete` blockiert | Spike-verifiziert sandbox-safe ✓ |
| `PugDatabase.TryGetComponent<CookingIngredientCD>` blockiert | Spike-verifiziert sandbox-safe ✓ |
| Custom-Mod-Item im Cooked-Food-Range (9500-9599) ohne `CookedFoodCD` | Loop 2 skipped via TryGetComponent — graceful |
| Custom-Mod-Ingredient ohne `CookingIngredientCD` | Pre-Cache nimmt es nicht auf → keine Permutationen damit — graceful |
| `GetObjectName` wirft NRE für Permutation | Iter-3.6 try-catch + `warnedIds`-Cache + `PascalCaseSplitter`-Fallback ✓ |

### Performance-Mess-Plan

Iter-3.7 baut `Time.realtimeSinceStartup`-Brackets ein für drei Hot-Operations,
loggt mit Prefix `[ItemChecklist] PERF`:

```csharp
// In Bake():
float t0 = Time.realtimeSinceStartup;
// … Loop 1 …
Debug.Log($"[ItemChecklist] PERF bake-loop1={1000f*(Time.realtimeSinceStartup-t0):F0}ms");
// … Loop 2 + sort + dict-build …
Debug.Log($"[ItemChecklist] PERF bake-total={...}ms catalog-size={entries.Length}");

// In SpawnRows():
Debug.Log($"[ItemChecklist] PERF spawn={...}ms rows={_spawnedRows.Count}");
```

**Trigger-Schwellen für Iter-3.8-Scope:**
- `bake-total > 8s` → Coroutine-Bake-Refactor (yield über GetObjectName-Batches)
- `spawn > 2s` → UI-Virtualisierung mit `CookBookUI`-Pool-Pattern
- PugText-Pool-Exhaust-Warning im Log → Virtualisierung sofort

### Mod-Origin für Cooked-Food-Permutationen

Heutige `ResolveModOrigin` schaut nur auf den Output-ObjectID (`CookedSoup` →
Vanilla). Iter-3.7-Pragmatik: heutige Logik beibehalten — alle Permutationen
einer Vanilla-Family werden als Vanilla gemarkt, auch wenn die Pair-Combo
ein Mod-Ingredient nutzt. Mod-aware Origin-Aggregation
(`OR(ingredient1.modOrigin, ingredient2.modOrigin, output.modOrigin)`) ist
Iter-3.8-Polish.

### Testing-Strategie (Manual-Plan)

ItemChecklist hat kein Unit-Test-Framework im Build-Setup — Iter-3.7-
Validierung läuft empirisch im Spiel:

1. **Bake-Sanity**: Welt laden → `[ItemChecklist] ItemCatalog baked: ~9680 items`
   in Player.log (± einige je nach mod-set)
2. **Sandbox-Smoke**: `grep -i "code security verification failed" Player.log`
   → erwartet leer
3. **Discovery-Flow**: Mushroom-Suppe kochen → SaveManagerDiscoveryHook log
   + Title-Counter erhöht
4. **Loc-Refresh**: in-game Sprache switchen (DE↔EN) → Bake re-läuft, Window
   zeigt neue Sprache
5. **Performance-Check**: Window öffnen, `[ItemChecklist] PERF bake-total=…
   spawn=…` werte vergleichen mit Iter-3.8-Trigger-Schwellen
6. **Old-Save-Compat**: Iter-3.6-Welt mit existierenden Cooked-Food-Discoveries
   laden → Quote zeigt einige Permutationen als entdeckt
7. **Multi-Char-Slot**: Welt mit 2+ Characters → Snapshot füllt korrekten
   Cache-Entry per `characterGuid`

## Cross-references

- Iter-3.6 Design: `docs/superpowers/specs/2026-05-28-itemchecklist-displayname-iter3-6-design.md`
- Iter-3.7 Empirical Spike: `docs/research/iter-3-7-cooked-food-discovery-spike.md`
- ILSpy Decompile Locations (für Re-Verifikation):
  - `CookedFoodCD`: `Pug.ECS.Components.dll`
  - `CookingIngredientCD`: `Pug.ECS.Components.dll`
  - `IngredientType`: `Pug.Base.dll`
  - `ObjectIDExtensions.IsCookedFood`: `Pug.Base.dll`
  - `InventoryUtility.TryConvert*` Pick-Family-Logic: `Pug.Other.dll:~1626`
  - `CookBookUI` (Iter-3.8-Virtualisierungs-Reference): `Pug.Other.dll`

## Known still-open items (revisit during implementation)

- **Tier-Discovery-Granularität nicht empirisch verifiziert** — konservative
  × 3-Annahme kann sich in der Praxis als 2× oder 1× herausstellen, falls
  Rare/Epic-Tiers nicht als separate `(objectID, variation)`-Discovery-
  Events feuern. Catalog würde dann mit grayed-out unreachable Entries leben
  — funktionell OK, kosmetisch suboptimal. Verifikation erfordert einen
  Test-Char mit hohem Cooking-Talent + RNG-Glück, der aktuell nicht verfügbar
  ist; alternativ Iter-3.7-Implementation deployen und über Wochen
  beobachten welche Tier-Discoveries der `SaveManagerDiscoveryHook` loggt
