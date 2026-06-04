namespace ItemChecklist
{
    /// <summary>
    /// Single source of truth for the discovered/total progress string shown in
    /// both the window footer (<see cref="ItemChecklist.UI.ItemChecklistWindow"/>)
    /// and the HUD counter (<see cref="ItemChecklist.UI.ItemChecklistHud"/>).
    /// One formatter keeps the two surfaces from drifting apart.
    /// </summary>
    internal static class ProgressFormat
    {
        /// <summary>
        /// "discovered / total (pp.p%)", or "0 / 0" when <paramref name="total"/>
        /// is non-positive (pre-bake / empty catalog) — mirrors the window's
        /// historical guard. percent uses F1; corekeeper-patch forces
        /// InvariantCulture so the decimal separator is a dot.
        /// </summary>
        public static string Counter(int discovered, int total)
        {
            if (total <= 0) return "0 / 0";
            float percent = 100f * discovered / total;
            return $"{discovered} / {total} ({percent:F1}%)";
        }
    }
}
