# Iter-11 localisation — storage format comparison (same term, both ways)

Decision aid for brainstorming, **not** mod source. The same term —
`SearchHint` (the search field's placeholder) — written in both candidate
storage formats so the cost asymmetry is concrete.

## Option A — CoreLib `AddTerm` (code dictionaries)

File: `Loc-snippet.cs`. The term in full:

```csharp
LocalizationModule.AddTerm("ItemChecklist/SearchHint", new Dictionary<string, string>
{
    { "en", "Search..." },
    { "de", "Suchen..." },
    // MT later: { "fr", "..." }, { "pt-br", "..." }, ...
});
```

- **6 lines**, all in **one shared file** (`Loc.cs`).
- Language identified by readable ISO code.
- No `.asset`, no `.meta`, no GUIDs, no language-address table, no AssetBundle.
- MT later = add one `{ "xx", "..." }` line per language, in place.

## Option B — IB-native `TextDataBlock` asset

Files: `ItemChecklist-General/SearchHint.asset` (+ `.meta`). The term in full
is **154 lines of YAML + an 8-line `.meta`** = **2 files**.

Two things had to be reverse-engineered even for this one tiny string:

1. **`m_Script.guid`** is the ScriptableData assembly GUID — per the
   `script-fileID-derivation` memory it is *per-SDK-clone-local*, so it must be
   verified against this clone or the asset won't deserialise as a
   `TextDataBlock` at all.
2. **The German language address is unknown.** The `m_localizedTexts` map is
   keyed by opaque language-address GUID pairs (`m_low`/`m_high`). Only `en`
   (#1) and `pt-br` (#4) are confirmable from IB — IB ships **no German**, so
   the `de` address is a *guess* (marked `ASSUMED de — UNVERIFIED` in the file).

### Which string belongs to which language?

The authoritative map is `LanguageDataBlock.m_address ↔ ISO6391` (`"de"`, `"en"`,
…). How you obtain it depends on the workflow:

- **In the SDK Editor (IB's real workflow):** you never see the GUIDs. The
  TextDataBlock inspector resolves each address to its `ISO6391`/`displayOrder`
  and shows labelled fields ("English", "German"…). The opacity is purely a
  property of the serialised YAML.
- **Hand-authoring the raw YAML (our path for option B):** the YAML stores only
  the opaque address. The `address→ISO6391` table is **not** in the decompiled
  C# (addresses are data), **not** loose on disk in the SDK (baked into the
  game's data bundles), and recoverable only by (a) extracting CK's
  `LanguageDataBlock` assets with a Unity asset tool (AssetStudio/UABE), or
  (b) a one-off runtime dump via `ScriptableData.GetDataBlocks<LanguageDataBlock>()`
  (each block exposes `m_address` + `ISO6391`). From data alone only `en` (#1)
  and `pt-br` (#4) are inferable.

So option B is comfortable **only inside the Editor inspector** — the very
asset-editor workflow that makes it heavyweight. Hand-authoring to bypass that
editor reintroduces the opaque-address problem. Option A has no such question:
the language is the literal ISO string `"de"` beside the text.

### Extracting the address→ISO table once (unblocks hand-authoring)

A clean way to recover the full table without AssetStudio or a runtime-dump mod:
create **one** TextDataBlock in the SDK Editor and type each language's ISO code
into its own field (`en` into English, `de` into German, …). Save, then read the
serialised YAML — each `m_language.m_address` is now paired with a readable
marker `title`, giving the complete `address→ISO6391` table. The same
Editor-created asset also carries the **correct `m_Script.guid` for this clone**,
so one ~5-minute round-trip resolves both reverse-engineered unknowns. After
that, hand-authoring the remaining term YAML is deterministic (no more
`UNVERIFIED` addresses).

Note: game-DLL assembly GUIDs like ScriptableData's `e853a5af…` are **portable**
(IB ships assets referencing them and they resolve for all users); the
per-clone-GUID caveat applies only to SDK-side mod scripts.

This makes option B genuinely practical. It does **not** change the ongoing
maintainability asymmetry (1 file + inline ISO vs ~60 files + address-keyed;
MT expansion across ~30 assets) — that remains the deciding axis.

## Scaled to the ~30 ItemChecklist terms

| | Option A (code dicts) | Option B (TextDataBlock) |
|---|---|---|
| Files | **1** (`Loc.cs`) | **~60** (~30 `.asset` + ~30 `.meta`) |
| Lines | ~30 short blocks | ~30 × ~130 lines YAML |
| Per-language id | readable ISO code | opaque GUID pair |
| Add a language (MT) | one line per term, in place | edit ~30 assets; place text at the right opaque address |
| Prereqs | none | this-clone script GUID + full address↔language table |
| git diff | "de: Suchen..." | a `title:` under an opaque address pair |
| Runtime | I2 via CoreLib | `API.Localization` (same as item names) |

## Takeaway

Both resolve identically at runtime (one I2 `LanguageSourceData`). The IB-native
asset workflow earns its overhead at IB's scale (hundreds of terms, a team, the
SDK editor). For ItemChecklist's ~30 short UI strings with an EN+DE-now /
MT-later plan, the code-dict format is dramatically lighter on every axis that
matters here — especially the MT expansion, where Option B forces edits across
~30 opaque-addressed assets.
