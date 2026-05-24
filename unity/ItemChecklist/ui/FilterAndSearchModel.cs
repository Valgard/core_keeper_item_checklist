using System;
using System.Collections.Generic;

namespace ItemChecklist.UI
{
    public enum DiscoveryFilter { All, Discovered, Undiscovered }

    /// <summary>
    /// Pure C# filter+search model. Holds current search string + filter
    /// enum, computes the visible catalog-index array. Re-runs on setter
    /// change or external Refresh() — e.g. when DiscoveredState.Changed
    /// fires after a live pickup.
    /// </summary>
    public sealed class FilterAndSearchModel
    {
        private readonly ItemCatalog catalog;
        private readonly DiscoveredState state;

        private string searchText = "";
        private DiscoveryFilter filter = DiscoveryFilter.All;

        public int[] VisibleIndices { get; private set; } = Array.Empty<int>();
        public int DiscoveredInFilter { get; private set; }

        public event Action OnResultsChanged;

        public FilterAndSearchModel(ItemCatalog catalog, DiscoveredState state)
        {
            this.catalog = catalog;
            this.state = state;
            Recompute();
        }

        public string SearchText
        {
            get => searchText;
            set { if (value != searchText) { searchText = value ?? ""; Recompute(); } }
        }

        /// <summary>
        /// Action-compatible setter for UI wiring (e.g. InputField.onValueChanged).
        /// </summary>
        public void SetSearch(string value) => SearchText = value;

        public DiscoveryFilter Filter
        {
            get => filter;
            set { if (value != filter) { filter = value; Recompute(); } }
        }

        public void Refresh() => Recompute();

        private void Recompute()
        {
            var list = new List<int>();
            int discovered = 0;
            string needle = searchText.Trim().ToLowerInvariant();

            for (int i = 0; i < catalog.Count; i++)
            {
                var e = catalog.GetByIndex(i);
                bool isDisc = state.IsDiscovered(e.ObjectId);

                // Filter
                if (filter == DiscoveryFilter.Discovered && !isDisc) continue;
                if (filter == DiscoveryFilter.Undiscovered && isDisc) continue;

                // Search — only against display name, only for discovered
                // items (undiscovered are spoiler-masked, can't search "???")
                if (needle.Length > 0)
                {
                    if (!isDisc) continue;
                    if (e.DisplayName.ToLowerInvariant().IndexOf(needle, StringComparison.Ordinal) < 0)
                        continue;
                }

                list.Add(i);
                if (isDisc) discovered++;
            }

            VisibleIndices = list.ToArray();
            DiscoveredInFilter = discovered;
            OnResultsChanged?.Invoke();
        }
    }
}
