using System;
using System.Collections.Generic;

namespace ItemChecklist.UI
{
    /// <summary>
    /// Shared ordering view-model. Owns Order (display-position -> catalog-index),
    /// produced by collecting the visible catalog indices (Iter-7: all of them,
    /// filter/search at no-op defaults) and sorting them by the active SortMode +
    /// direction. Iter-8 will activate the filter/search dimension; the sort
    /// dimension is Iter-7.
    ///
    /// Sort mode + direction are kept in static fields so they survive window
    /// close/open AND a re-bake within one game session; a fresh process resets
    /// them to the defaults (Name, ascending).
    /// </summary>
    public sealed class ItemListViewModel
    {
        private readonly ItemCatalog catalog;
        private readonly DiscoveredState state;

        // In-memory per-session sort state (static: survives re-bake; resets on
        // process restart). Default: Name, ascending.
        private static SortMode s_mode = SortMode.Name;
        private static bool s_ascending = true;

        // Faceted filter dimensions (Iter-10). OR within a set, AND across sets.
        // Empty set = no constraint. Static: survive reopen + re-bake, reset on
        // process restart (mirrors the sort state).
        private static readonly HashSet<bool> s_discovery = new HashSet<bool>();      // true=discovered
        private static readonly HashSet<Rarity> s_rarity  = new HashSet<Rarity>();
        private static readonly HashSet<ItemCategory> s_category = new HashSet<ItemCategory>();
        private static readonly HashSet<bool> s_craft = new HashSet<bool>();          // true=craftable

        private string searchText = "";

        public int[] Order { get; private set; } = Array.Empty<int>();
        public int Count => Order.Length;
        public int DiscoveredInView { get; private set; }

        public event Action OnResultsChanged;

        public ItemListViewModel(ItemCatalog catalog, DiscoveredState state)
        {
            this.catalog = catalog;
            this.state = state;
            Recompute();
        }

        public SortMode Mode
        {
            get => s_mode;
            set { if (value != s_mode) { s_mode = value; Recompute(); } }
        }

        public bool Ascending
        {
            get => s_ascending;
            set { if (value != s_ascending) { s_ascending = value; Recompute(); } }
        }

        public void ToggleDirection() => Ascending = !Ascending;

        // --- Filter dimensions (Iter-10) ---
        public bool DiscoverySelected(bool discovered) => s_discovery.Contains(discovered);
        public bool RaritySelected(Rarity r)           => s_rarity.Contains(r);
        public bool CategorySelected(ItemCategory c)   => s_category.Contains(c);
        public bool CraftSelected(bool craftable)      => s_craft.Contains(craftable);

        public void ToggleDiscovery(bool discovered) => Toggle(s_discovery, discovered);
        public void ToggleRarity(Rarity r)           => Toggle(s_rarity, r);
        public void ToggleCategory(ItemCategory c)   => Toggle(s_category, c);
        public void ToggleCraft(bool craftable)      => Toggle(s_craft, craftable);

        public int ActiveFilterCount =>
            s_discovery.Count + s_rarity.Count + s_category.Count + s_craft.Count;

        public void ClearAllFilters()
        {
            bool any = ActiveFilterCount > 0;
            s_discovery.Clear(); s_rarity.Clear(); s_category.Clear(); s_craft.Clear();
            if (any) Recompute();
        }

        private void Toggle<T>(HashSet<T> set, T value)
        {
            if (!set.Remove(value)) set.Add(value);
            Recompute();
        }

        // --- Search (Iter-8) ---
        public string SearchText
        {
            get => searchText;
            set { if (value != searchText) { searchText = value ?? ""; Recompute(); } }
        }

        /// <summary>
        /// True when any filter dimension or the name search is narrowing the view
        /// (i.e. the result set is a strict subset of the catalog for a reason the
        /// player chose). Distinct from `Count != catalog.Count`, which is only
        /// accidentally equivalent — a fully-completed "Discovered" filter has
        /// `Count == catalog.Count` yet is still filtered.
        /// </summary>
        public bool IsFiltered => ActiveFilterCount > 0 || searchText.Trim().Length > 0;

        public void Refresh() => Recompute();

        public void Recompute()
        {
            // 1. Collect visible catalog indices, applying the active discovery filter + name search.
            var indices = new List<int>(catalog.Count);
            int discovered = 0;
            string needle = searchText.Trim().ToLowerInvariant();

            for (int i = 0; i < catalog.Count; i++)
            {
                var e = catalog.GetByIndex(i);
                bool isDisc = state.IsDiscovered(e.ObjectId, e.Variation);

                if (s_discovery.Count > 0 && !s_discovery.Contains(isDisc)) continue;
                if (s_rarity.Count   > 0 && !s_rarity.Contains(e.Rarity))   continue;
                if (s_category.Count > 0 && !s_category.Contains(ItemCategories.Of(e.ObjectType))) continue;
                if (s_craft.Count    > 0 && !s_craft.Contains(e.IsCraftable)) continue;
                if (needle.Length > 0)
                {
                    if (e.DisplayName.ToLowerInvariant().IndexOf(needle, StringComparison.Ordinal) < 0)
                        continue;
                }

                indices.Add(i);
                if (isDisc) discovered++;
            }

            // 2. Sort by the active mode; reverse for descending.
            indices.Sort(Compare);
            if (!s_ascending) indices.Reverse();

            Order = indices.ToArray();
            DiscoveredInView = discovered;
            OnResultsChanged?.Invoke();
        }

        /// <summary>
        /// Ascending comparison for the active mode. Tiebreak is always
        /// DisplayName (InvariantCultureIgnoreCase — locale-aware, so "Ü" sorts
        /// under U, not after Z). Descending is applied by reversing the sorted
        /// list in Recompute, so this stays a pure ascending compare.
        /// </summary>
        private int Compare(int ia, int ib)
        {
            var a = catalog.GetByIndex(ia);
            var b = catalog.GetByIndex(ib);
            int c;
            switch (s_mode)
            {
                case SortMode.Rarity:
                    c = ((int)a.Rarity).CompareTo((int)b.Rarity);
                    break;
                case SortMode.Level:
                    c = a.Level.CompareTo(b.Level);
                    break;
                case SortMode.Value:
                    // -1 (unsellable) coerced to 0 so unsellable items cluster at the
                    // bottom in descending / top in ascending, consistently.
                    c = ValueKey(a).CompareTo(ValueKey(b));
                    break;
                default: // SortMode.Name
                    c = 0;
                    break;
            }
            if (c != 0) return c;
            // Locale-aware: InvariantCulture weights "Ü" as a U-variant (diacritic =
            // secondary weight) so it sorts under U, not after Z (which Ordinal,
            // comparing raw codepoints, would do). Invariant matches the
            // corekeeper-patch-forced UI culture (no de-DE satellite lookup).
            c = string.Compare(a.DisplayName, b.DisplayName, StringComparison.InvariantCultureIgnoreCase);
            if (c != 0) return c;
            return ia.CompareTo(ib);   // final tiebreak: total order, stable under Reverse()
        }

        private static int ValueKey(ItemCatalog.Entry e) => e.SellValue < 0 ? 0 : e.SellValue;
    }
}
