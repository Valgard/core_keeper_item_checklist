using PugMod;

namespace ItemChecklist
{
    /// <summary>Resolves an ItemChecklist localisation term for the current
    /// language, falling back to the term key itself if unresolved.</summary>
    internal static class Loc
    {
        public static string T(string term) => API.Localization.GetLocalizedTerm(term) ?? term;
        public static string F(string term, object arg0) => string.Format(T(term), arg0);
    }
}
