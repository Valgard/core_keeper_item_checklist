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
    ///     is the official path to the localized display name; it returns a
    ///     <c>TextAndFormatFields</c> with <c>.text</c> + <c>.dontLocalize</c>.
    ///   * <c>API.ModLoader.LoadedMods</c> exposes the mod-id → display-name
    ///     mapping for mod-origin tagging.
    /// </summary>
    public sealed class ItemCatalog
    {
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
        private readonly Dictionary<int, int> idToIndex = new Dictionary<int, int>();

        public int Count => entries.Length;
        public Entry GetByIndex(int index) => entries[index];
        public bool TryGetIndex(int objectId, out int index) => idToIndex.TryGetValue(objectId, out index);

        /// <summary>
        /// Build the catalog. Iterates every <c>ObjectDataCD</c> in
        /// <c>PugDatabase.objectsByType.Keys</c>, keeps only the canonical
        /// (variation == 0) entries whose <c>ObjectType</c> is not one of
        /// the categorically-non-item discriminators (NonObtainable, Creature,
        /// Critter, PlayerType), resolves the localized display name, then
        /// sorts alphabetically and builds the id → index map. Safe to call
        /// multiple times — replaces the previous catalog atomically from a
        /// single-caller perspective.
        /// </summary>
        public void Bake()
        {
            var list = new List<Entry>();

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

            foreach (var od in PugDatabase.objectsByType.Keys)
            {
                // For a checklist we only care about the canonical variant of
                // each item — skip colour/skin variations.
                if (od.variation != 0) continue;

                var info = PugDatabase.GetObjectInfo(od.objectID, od.variation);
                if (info == null) continue;

                // ObjectType has no single "Item" value — it discriminates the
                // kind of thing (Helm, MiningPick, Ring, …). Items are
                // everything that *isn't* a categorically-non-item type. The
                // exclusion list mirrors the start of ItemBrowser's
                // ObjectUtility.IsNonObtainable.
                if (info.objectType == ObjectType.NonObtainable) continue;
                if (info.objectType == ObjectType.Creature) continue;
                if (info.objectType == ObjectType.Critter) continue;
                if (info.objectType == ObjectType.PlayerType) continue;

                string name = ResolveDisplayName(od);
                if (string.IsNullOrWhiteSpace(name)) continue;   // unnamed entries are placeholders

                Sprite icon = info.smallIcon != null ? info.smallIcon : info.icon;
                string modOrigin = ResolveModOrigin(od, modIdToName);

                list.Add(new Entry((int) od.objectID, od.variation, name, icon, modOrigin));
            }

            entries = list
                .OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            idToIndex.Clear();
            for (int i = 0; i < entries.Length; i++)
                idToIndex[entries[i].ObjectId] = i;

            Debug.Log($"[ItemChecklist] ItemCatalog baked: {entries.Length} items");
        }

        /// <summary>
        /// Resolve the localized display name for an object. Uses
        /// <see cref="PlayerController.GetObjectName"/> the same way
        /// ItemBrowser does (it handles the I2.Loc term lookup, RTL,
        /// the dontLocalize fallback). Returns the raw objectID name if the
        /// game can't provide a localized string.
        /// </summary>
        private static string ResolveDisplayName(ObjectDataCD od)
        {
            try
            {
                var fields = PlayerController.GetObjectName(new ContainedObjectsBuffer { objectData = od }, true);
                string text = fields.text;
                if (!string.IsNullOrEmpty(text))
                    return text.Replace("\n", " ");
            }
            catch
            {
                // PlayerController may not be fully wired up on first call —
                // fall through to the ObjectID-based fallback.
            }

            return od.objectID.ToString();
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
    }
}
