using System;
using System.Collections.Generic;

namespace ItemChecklist.UI
{
    public enum DiscoveryFilter { All, Discovered, Undiscovered }

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

        private string searchText = "";
        private DiscoveryFilter filter = DiscoveryFilter.All;

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

        // --- Iter-8 seam (kept at no-op defaults until Iter-8 wires the UI) ---
        public string SearchText
        {
            get => searchText;
            set { if (value != searchText) { searchText = value ?? ""; Recompute(); } }
        }
        public DiscoveryFilter Filter
        {
            get => filter;
            set { if (value != filter) { filter = value; Recompute(); } }
        }

        /// <summary>
        /// True when the discovery filter or the name search is narrowing the view
        /// (i.e. the result set is a strict subset of the catalog for a reason the
        /// player chose). Distinct from `Count != catalog.Count`, which is only
        /// accidentally equivalent — a fully-completed "Discovered" filter has
        /// `Count == catalog.Count` yet is still filtered.
        /// </summary>
        public bool IsFiltered => filter != DiscoveryFilter.All || searchText.Trim().Length > 0;

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

                if (filter == DiscoveryFilter.Discovered && !isDisc) continue;
                if (filter == DiscoveryFilter.Undiscovered && isDisc) continue;
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
                case SortMode.Found:
                    // ascending = discovered first (user choice). true sorts
                    // before false, so invert the bool compare.
                    bool da = state.IsDiscovered(a.ObjectId, a.Variation);
                    bool db = state.IsDiscovered(b.ObjectId, b.Variation);
                    c = db.CompareTo(da);   // discovered (true) -> earlier
                    break;
                case SortMode.Category:
                    c = string.Compare(a.ObjectType.ToString(), b.ObjectType.ToString(),
                        StringComparison.Ordinal);
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
    }
}
