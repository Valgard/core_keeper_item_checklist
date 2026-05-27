using System.Text;

namespace ItemChecklist
{
    /// <summary>
    /// Pure, sandbox-safe splitter for CamelCase/PascalCase identifiers.
    /// Used as the final fallback in ItemCatalog.ResolveOne when
    /// PlayerController.GetObjectName cannot resolve a localized name.
    ///
    /// Boundary rules (insert space BEFORE the boundary character):
    ///   (a) Upper after Lower:    "AbandonedC..."  → "Abandoned C..."
    ///   (b) Upper after Digit:    "T1S..."         → "T1 S..."
    ///   (c) Upper after Upper, next is Lower:
    ///       "IOP..." where next is "ort" → "IO Port" (initialism ends)
    ///   (d) Digit after Letter:   "AbandonedT1..." → "Abandoned T1..."
    /// </summary>
    public static class PascalCaseSplitter
    {
        public static string Split(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var sb = new StringBuilder(input.Length + 8);
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (i > 0)
                {
                    char prev = input[i - 1];
                    bool isUpper = char.IsUpper(c);
                    bool isDigit = char.IsDigit(c);
                    bool prevLower = char.IsLower(prev);
                    bool prevDigit = char.IsDigit(prev);
                    bool prevUpper = char.IsUpper(prev);
                    bool prevLetter = char.IsLetter(prev);

                    bool boundary = false;
                    if (isUpper && prevLower) boundary = true;                     // (a)
                    else if (isUpper && prevDigit) boundary = true;                // (b)
                    else if (isUpper && prevUpper && i + 1 < input.Length
                             && char.IsLower(input[i + 1])) boundary = true;       // (c)
                    else if (isDigit && prevLetter) boundary = true;               // (d)

                    if (boundary) sb.Append(' ');
                }
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
