using HarmonyLib;
using UnityEngine;

namespace ItemChecklist
{
    /// <summary>
    /// Harmony postfix on <c>SaveManager.SetCharacterId(int id)</c> — CK
    /// calls this every time the user selects a character from the char-
    /// select menu (and clears it with id=-1 on return to the main menu).
    ///
    /// <para>We use this to resolve the active character's
    /// <c>characterGuid</c> (a unique string field on
    /// <c>CharacterData</c>). The slot-id alone is not enough because we
    /// cache by guid, not by slot. CK keeps the deserialized
    /// <c>CharacterData[]</c> array as a private field on
    /// <c>SaveManager</c>; we read it via <c>HarmonyLib.Traverse</c>,
    /// which is reflection inside the trusted <c>0Harmony.dll</c> and
    /// therefore passes the Roslyn sandbox.</para>
    ///
    /// <para>This makes the snapshot lookup same-name-resilient: two
    /// characters named "Hans" have different <c>characterGuid</c>s and
    /// resolve to different cache entries in
    /// <see cref="CharacterDataDiscoverySnapshot.Cache"/>.</para>
    /// </summary>
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SetCharacterId))]
    internal static class SaveManagerActiveCharHook
    {
        /// <summary>The active character's GUID, or <c>null</c> when no
        /// character is selected (main menu).</summary>
        public static string ActiveGuid { get; private set; }

        [HarmonyPostfix]
        static void After(SaveManager __instance, int id)
        {
            if (id < 0)
            {
                ActiveGuid = null;
                return;
            }

            try
            {
                var characterData = Traverse.Create(__instance)
                    .Field("characterData")
                    .GetValue<CharacterData[]>();
                if (characterData == null || id >= characterData.Length || characterData[id] == null)
                {
                    ActiveGuid = null;
                    return;
                }
                ActiveGuid = characterData[id].characterGuid;
                Debug.Log($"[ItemChecklist] Active char set: slot={id} guid={ActiveGuid}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ItemChecklist] SetCharacterId hook failed: {e.Message}");
                ActiveGuid = null;
            }
        }
    }
}
