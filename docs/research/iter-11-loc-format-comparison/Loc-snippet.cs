// Option A — CoreLib AddTerm (code dictionaries).
// The SAME term ("SearchHint") as the TextDataBlock .asset next door.
//
// All terms live in ONE file (Loc.cs). Registered once from IMod.EarlyInit/Init.
// No .asset, no .meta, no GUIDs, no language-address table, no AssetBundle import.

using System.Collections.Generic;
using CoreLib.Submodule.Localization;

namespace ItemChecklist
{
    internal static class Loc
    {
        public static void Register()
        {
            // One line per term. Language identified by readable ISO code.
            // English is mandatory (CoreLib throws without "en") and is the fallback
            // for every not-yet-translated language.
            LocalizationModule.AddTerm("ItemChecklist/SearchHint", new Dictionary<string, string>
            {
                { "en", "Search..." },
                { "de", "Suchen..." },
                // MT later: just add more pairs here — { "fr", "..." }, { "pt-br", "..." }, ...
            });

            // ...the other ~29 terms follow, each one block like the above.
        }

        // Resolve at render time:
        //   pugText.Render(LocalizationManager.GetTranslation("ItemChecklist/SearchHint"));
        // (Plan verifies whether API.Localization.GetLocalizedTerm also resolves
        //  AddTerm-registered terms; if not, use LocalizationManager.GetTranslation.)
    }
}
