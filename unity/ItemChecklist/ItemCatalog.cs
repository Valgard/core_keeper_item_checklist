using System;
using System.Collections.Generic;
using System.Linq;
using PugMod;
using UnityEngine;

namespace ItemChecklist
{
    /// <summary>
    /// Single immutable list of every item the game knows about, sorted
    /// alphabetically by display name. Built once per world load via
    /// <see cref="Bake"/>. Subsequent lookups are O(1) by id or by index.
    ///
    /// API surface verified against moorowl/ItemBrowser
    /// (ItemBrowserPackage/Scripts/Utilities/{ObjectUtility,ModUtility}.cs):
    ///   * <c>PugDatabase.objectsByType.Keys</c> enumerates every
    ///     <c>ObjectDataCD</c> the loaded database knows about.
    ///   * <c>PugDatabase.GetObjectInfo(objectID, variation)</c> returns the
    ///     <c>ObjectInfo</c> carrying <c>objectType</c>, <c>icon</c>,
    ///     <c>smallIcon</c>.
    ///   * <c>PlayerController.GetObjectName(ContainedObjectsBuffer, bool)</c>
    ///     is the official path to the localized display name; the bool is
    ///     <c>localize</c> (true = localized name, false = raw I2.Loc term).
    ///     Returns <c>TextAndFormatFields</c> with <c>.text</c> +
    ///     <c>.dontLocalize</c>. When <c>.dontLocalize</c> is true on the
    ///     localized call, ItemBrowser swaps in the unlocalized text instead
    ///     (player-names, ad-hoc items).
    ///   * <c>API.ModLoader.LoadedMods</c> exposes the mod-id → display-name
    ///     mapping for mod-origin tagging.
    /// </summary>
    public sealed class ItemCatalog
    {
        // Per-objectID one-time warning cache. Static so it survives re-bakes
        // (loc-change, world-reload) — the symptom is build-level, not world-level.
        // Not reset by Bake().
        private static readonly System.Collections.Generic.HashSet<ObjectID> warnedIds = new();

        // Re-entrance guard. Bake() must be safe against nested calls when
        // a Loc-change event fires during an in-flight bake. Single-threaded
        // assumption (Unity main thread for Harmony + I2.Loc events).
        private bool baking;

        public readonly struct Entry
        {
            public readonly int ObjectId;
            public readonly int Variation;
            public readonly string DisplayName;
            public readonly Sprite Icon;
            public readonly string ModOrigin;   // empty string = vanilla

            public Entry(int objectId, int variation, string displayName, Sprite icon, string modOrigin)
            {
                ObjectId = objectId;
                Variation = variation;
                DisplayName = displayName;
                Icon = icon;
                ModOrigin = modOrigin ?? string.Empty;
            }
        }

        private Entry[] entries = Array.Empty<Entry>();
        private readonly Dictionary<long, int> keyToIndex = new Dictionary<long, int>();

        public int Count => entries.Length;
        public Entry GetByIndex(int index) => entries[index];
        public bool TryGetIndex(int objectId, int variation, out int index) =>
            keyToIndex.TryGetValue(DiscoveredState.PackKey(objectId, variation), out index);

        /// <summary>
        /// Build the catalog. Iterates every <c>ObjectDataCD</c> in
        /// <c>PugDatabase.objectsByType.Keys</c>, keeps only the canonical
        /// (variation == 0) entries whose <c>ObjectType</c> is not one of
        /// the categorically-non-item discriminators (NonObtainable, Creature,
        /// Critter, PlayerType), resolves the localized display name via two
        /// GetObjectName passes (localized + unlocalized), detects name
        /// conflicts and appends a disambiguation note, then sorts alphabetically
        /// and builds the id → index map. Safe to call multiple times — replaces
        /// the previous catalog atomically from a single-caller perspective.
        /// </summary>
        public void Bake()
        {
            if (baking)
            {
                Debug.LogWarning("[ItemChecklist] Bake() re-entered — skipping nested call");
                return;
            }
            baking = true;
            try
            {
                // PugDatabase.objectsByType is null until UpdateEntityMonos runs at
                // least once. Bake() called too early (before the world is ready)
                // hits this — fail soft so the consumer can retry.
                if (PugDatabase.objectsByType == null)
                {
                    Debug.LogWarning("[ItemChecklist] ItemCatalog.Bake called before PugDatabase ready — skipping");
                    return;
                }

                // Pre-resolve mod-id → display-name once so the per-entry loop
                // is a single Dictionary lookup.
                var modIdToName = new Dictionary<long, string>();
                foreach (var mod in API.ModLoader.LoadedMods)
                {
                    string name = !string.IsNullOrWhiteSpace(mod.Metadata.displayName)
                        ? mod.Metadata.displayName
                        : mod.Metadata.name;
                    modIdToName[mod.ModId] = name ?? string.Empty;
                }

                // First pass: collect localized + unlocalized names per accepted od.
                var localizedNames   = new Dictionary<long, string>();  // key = PackKey(objId, variation)
                var unlocalizedNames = new Dictionary<long, string>();
                var accepted         = new List<ObjectDataCD>();
                var iconCache        = new Dictionary<long, Sprite>();

                foreach (var od in PugDatabase.objectsByType.Keys)
                {
                    // Phase-1 scope: one tick per item family. Skip colour/skin
                    // variations (variation > 0). Phase-2 may revisit if per-skin
                    // tracking becomes desirable.
                    if (od.variation != 0) continue;

                    // Iter-3.7: Cooked-Food family-items (IDs in [9500,9599]) are
                    // handled by the α-enumeration loop further down — skip them here
                    // so they don't appear as variation=0 placeholder entries.
                    if (od.objectID.IsCookedFood()) continue;

                    var info = PugDatabase.GetObjectInfo(od.objectID, od.variation);
                    if (info == null) continue;

                    // ObjectType has no single "Item" value — it discriminates the
                    // kind of thing (Helm, MiningPick, Ring, …). Items are
                    // everything that *isn't* a categorically-non-item type. The
                    // exclusion list mirrors the start of ItemBrowser's
                    // ObjectUtility.IsNonObtainable.
                    // NonUsable=0 is the default value of any DB entry registered
                    // without an explicit type — test fixtures, prefab stubs, etc.
                    // Almost always garbage from a player-facing checklist's POV.
                    if (info.objectType == ObjectType.NonUsable)    continue;
                    if (info.objectType == ObjectType.NonObtainable) continue;
                    if (info.objectType == ObjectType.Creature)      continue;
                    if (info.objectType == ObjectType.Critter)       continue;
                    if (info.objectType == ObjectType.PlayerType)    continue;

                    // Two-pass name resolution (ItemBrowser ObjectUtility.cs pattern).
                    // localize=true  → I2.Loc resolved display name (e.g. "Large Water Can")
                    // localize=false → raw I2.Loc term path (e.g. "Items/LargeWaterCan")
                    var (locText, locDontLocalize) = ResolveOne(od, localize: true);
                    var (rawText, _)               = ResolveOne(od, localize: false);

                    // ItemBrowser dontLocalize-output-flag fallback (ObjectUtility.cs:107-108):
                    // when the game signals this item can't be localized, swap to the raw term.
                    if (locDontLocalize && !string.IsNullOrEmpty(rawText))
                        locText = rawText;

                    // PascalCaseSplitter fallback when both passes failed to produce a name.
                    if (string.IsNullOrEmpty(locText))
                        locText = PascalCaseSplitter.Split(od.objectID.ToString());
                    if (string.IsNullOrEmpty(rawText))
                        rawText = PascalCaseSplitter.Split(od.objectID.ToString());

                    long key = DiscoveredState.PackKey((int)od.objectID, od.variation);
                    localizedNames[key]   = locText;
                    unlocalizedNames[key] = rawText;
                    accepted.Add(od);
                    iconCache[key] = info.smallIcon != null ? info.smallIcon : info.icon;
                }

                // ─── Loop 2: α-enumeration for Cooked-Food permutations ─────────
                // Pre-cache: ingredient → turnsIntoFood (Base-Tier-Family)
                //            family → (rareId, epicId) tier-versions
                // Note: tierMap is populated for ALL 45 Cooked-Food items
                // (incl. Rare/Epic), but only base-tier IDs are ever looked up
                // below — Rare/Epic items' tierMap entries are inert. Cost is
                // ~30 wasted dict insertions out of ~45 — not worth filtering.
                var turnsInto = new Dictionary<ObjectID, ObjectID>();
                var tierMap = new Dictionary<ObjectID, (ObjectID rare, ObjectID epic)>();
                foreach (var od in PugDatabase.objectsByType.Keys)
                {
                    if (od.variation != 0) continue;
                    if (PugDatabase.TryGetComponent<CookingIngredientCD>(od, out var ing)
                        && !CookedFoodCD.IsIngredientObsolete(od.objectID))
                    {
                        turnsInto[od.objectID] = ing.turnsIntoFood;
                    }
                    if (od.objectID.IsCookedFood()
                        && PugDatabase.TryGetComponent<CookedFoodCD>(od, out var cf))
                    {
                        tierMap[od.objectID] = (cf.rareVersion, cf.epicVersion);
                    }
                }

                // Symmetric cartesian: GetFoodVariation(a,b) == GetFoodVariation(b,a),
                // so we only iterate j >= i and add each pair once.
                var ingredients = new List<ObjectID>(turnsInto.Keys);
                for (int i = 0; i < ingredients.Count; i++)
                for (int j = i; j < ingredients.Count; j++)
                {
                    var i1 = ingredients[i];
                    var i2 = ingredients[j];
                    var primary = CookedFoodCD.GetPrimaryIngredient(i1, i2);
                    if (!turnsInto.TryGetValue(primary, out var baseFamily)) continue;
                    int variation = CookedFoodCD.GetFoodVariation(i1, i2);

                    // 3 tier-variants per pair, same variation, different objectIDs.
                    AddCookedEntry(
                        new ObjectDataCD { objectID = baseFamily, variation = variation },
                        localizedNames, unlocalizedNames, iconCache, accepted);
                    if (tierMap.TryGetValue(baseFamily, out var tiers))
                    {
                        if (tiers.rare != ObjectID.None)
                            AddCookedEntry(
                                new ObjectDataCD { objectID = tiers.rare, variation = variation },
                                localizedNames, unlocalizedNames, iconCache, accepted);
                        if (tiers.epic != ObjectID.None)
                            AddCookedEntry(
                                new ObjectDataCD { objectID = tiers.epic, variation = variation },
                                localizedNames, unlocalizedNames, iconCache, accepted);
                    }
                }

                // Conflict detection: count occurrences of each localized name.
                var nameCount = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var locName in localizedNames.Values)
                    nameCount[locName] = nameCount.TryGetValue(locName, out var c) ? c + 1 : 1;

                // Second pass: build final entries, appending disambiguation note on conflict.
                var list = new List<Entry>(accepted.Count);
                foreach (var od in accepted)
                {
                    long key = DiscoveredState.PackKey((int)od.objectID, od.variation);
                    string finalName = localizedNames[key];
                    if (nameCount[finalName] > 1)
                    {
                        string rawName = unlocalizedNames[key];
                        if (!string.IsNullOrEmpty(rawName) && rawName != finalName)
                            finalName = $"{finalName} ({rawName})";
                    }
                    string modOrigin = ResolveModOrigin(od, modIdToName);
                    list.Add(new Entry((int)od.objectID, od.variation, finalName, iconCache[key], modOrigin));
                }

                entries = list
                    .OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                keyToIndex.Clear();
                for (int i = 0; i < entries.Length; i++)
                    keyToIndex[DiscoveredState.PackKey(entries[i].ObjectId, entries[i].Variation)] = i;

                Debug.Log($"[ItemChecklist] ItemCatalog baked: {entries.Length} items");
            }
            finally
            {
                baking = false;
            }
        }

        /// <summary>
        /// Resolve a name for an object via <see cref="PlayerController.GetObjectName"/>.
        /// Pass <c>localize=true</c> for the I2.Loc-resolved display name; <c>false</c>
        /// for the raw term path (used as conflict-disambiguation note).
        /// Returns the text and the output struct's <c>dontLocalize</c> flag.
        /// Falls back to <c>(null, false)</c> when GetObjectName throws; the caller
        /// applies <see cref="PascalCaseSplitter.Split"/> as the final safety net.
        /// First exception per objectID is logged once via <c>warnedIds</c>.
        /// </summary>
        private static (string text, bool dontLocalize) ResolveOne(ObjectDataCD od, bool localize)
        {
            try
            {
                var fields = PlayerController.GetObjectName(
                    new ContainedObjectsBuffer { objectData = od },
                    localize);
                return (fields.text?.Replace("\n", " "), fields.dontLocalize);
            }
            catch (NullReferenceException ex)
            {
                if (warnedIds.Add(od.objectID))
                    Debug.LogWarning(
                        $"[ItemChecklist] GetObjectName({od.objectID}, localize={localize}) threw NullReferenceException: {ex.Message}");
            }
            catch (Exception ex)
            {
                if (warnedIds.Add(od.objectID))
                    Debug.LogWarning(
                        $"[ItemChecklist] GetObjectName({od.objectID}, localize={localize}) threw (non-NRE): {ex.Message}");
            }
            return (null, false);
        }

        /// <summary>
        /// Resolve the originating mod's display name for an object, or "" if
        /// the object is vanilla. Uses ItemBrowser's approach: scan
        /// <c>Manager.mod.ExtraAuthoring</c> (the runtime-registered
        /// authoring components added by mods) and match the entry by
        /// <c>ObjectAuthoring</c>. Vanilla items are not in that list, so they
        /// naturally fall through to the empty string.
        /// </summary>
        private static string ResolveModOrigin(ObjectDataCD od, Dictionary<long, string> modIdToName)
        {
            try
            {
                if (Manager.mod == null || Manager.mod.ExtraAuthoring == null)
                    return string.Empty;

                foreach (var authoring in Manager.mod.ExtraAuthoring)
                {
                    if (authoring == null) continue;
                    var go = authoring.gameObject;
                    if (go == null) continue;

                    if (!go.TryGetComponent<ObjectAuthoring>(out var objectAuthoring))
                        continue;

                    var entryObjectId = API.Authoring.GetObjectID(objectAuthoring.objectName);
                    if (entryObjectId != od.objectID) continue;
                    if (objectAuthoring.variation != od.variation) continue;

                    // Found a mod-side authoring for this object. We don't have a
                    // direct asset → mod-id link here (ItemBrowser uses a cached
                    // InstanceID map for that). Fall back to the first loaded
                    // non-game mod whose name matches a colon-prefix in
                    // objectName ("ModName:Object"), else return empty.
                    var internalName = objectAuthoring.objectName ?? string.Empty;
                    var colonIdx = internalName.IndexOf(':');
                    if (colonIdx > 0)
                    {
                        var sourceMod = Normalize(internalName.Substring(0, colonIdx));
                        foreach (var mod in API.ModLoader.LoadedMods)
                        {
                            if (Normalize(mod.Metadata.name) == sourceMod)
                                return modIdToName.TryGetValue(mod.ModId, out var displayName)
                                    ? displayName
                                    : mod.Metadata.name;
                        }
                    }

                    return string.Empty;
                }
            }
            catch
            {
                // Manager.mod / ExtraAuthoring may not be initialized in some
                // contexts — treat as vanilla.
            }

            return string.Empty;
        }

        private static string Normalize(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            return name.ToLowerInvariant().Replace("-", "").Replace("_", "").Replace(" ", "");
        }

        /// <summary>
        /// Resolve+add a concrete cooked-food permutation entry. Shares the
        /// two-pass name/icon resolution + accepted-list with Loop 1 by writing
        /// into the same Dictionary instances (passed via parameters since they
        /// are local to Bake()).
        /// </summary>
        private void AddCookedEntry(
            ObjectDataCD od,
            Dictionary<long, string> localizedNames,
            Dictionary<long, string> unlocalizedNames,
            Dictionary<long, Sprite> iconCache,
            List<ObjectDataCD> accepted)
        {
            var info = PugDatabase.GetObjectInfo(od.objectID, od.variation);
            if (info == null) return;

            var (locText, locDontLocalize) = ResolveOne(od, localize: true);
            var (rawText, _)               = ResolveOne(od, localize: false);
            if (locDontLocalize && !string.IsNullOrEmpty(rawText))
                locText = rawText;
            if (string.IsNullOrEmpty(locText))
                locText = PascalCaseSplitter.Split(od.objectID.ToString());
            if (string.IsNullOrEmpty(rawText))
                rawText = PascalCaseSplitter.Split(od.objectID.ToString());

            long key = DiscoveredState.PackKey((int)od.objectID, od.variation);
            localizedNames[key]   = locText;
            unlocalizedNames[key] = rawText;
            accepted.Add(od);
            iconCache[key] = info.smallIcon != null ? info.smallIcon : info.icon;
        }
    }
}
