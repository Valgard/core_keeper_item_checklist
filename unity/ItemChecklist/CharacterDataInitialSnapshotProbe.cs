using HarmonyLib;
using UnityEngine;

namespace ItemChecklist
{
    /// <summary>
    /// SECOND SANDBOX PROBE: Tests whether
    /// <c>[HarmonyPatch(typeof(CharacterData), ...)]</c> passes the sandbox
    /// AND whether the postfix gives us readable access to the
    /// <c>discoveredObjects2</c> field on the instance.
    ///
    /// If this logs the count of pre-existing discoveries when the user
    /// loads a saved character, we have our initial-snapshot mechanism
    /// — combined with the SetObjectAsDiscovered hook (already validated),
    /// we have full discovery-state mirroring via Harmony alone.
    /// </summary>
    [HarmonyPatch(typeof(CharacterData), nameof(CharacterData.OnAfterDeserialize))]
    internal static class CharacterDataInitialSnapshotProbe
    {
        [HarmonyPostfix]
        static void OnAfterDeserialize(CharacterData __instance)
        {
            int count = __instance?.discoveredObjects2?.Count ?? -1;
            Debug.Log($"[ItemChecklist] PROBE: CharacterData.OnAfterDeserialize → discoveredObjects2.Count = {count}");
        }
    }
}
