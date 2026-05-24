using HarmonyLib;
using UnityEngine;

namespace ItemChecklist
{
    /// <summary>
    /// SANDBOX PROBE: Tests whether <c>[HarmonyPatch(typeof(SaveManager), ...)]</c>
    /// passes the Roslyn sandbox. <c>SaveManager</c> as a property/method-access
    /// path (<c>Manager.saves.X()</c>) has been verified banned. Open question:
    /// is <c>typeof(SaveManager)</c> as a type-reference for a Harmony attribute
    /// also banned, or only the instance-property-access path?
    ///
    /// If this assembly loads with "passed code security verification", the
    /// pivot to CK's native discovery system is viable — we hook
    /// <c>SetObjectAsDiscovered</c> and let CK do the heavy lifting.
    ///
    /// If the assembly fails verification, this whole approach is dead and
    /// we fall back to the multi-handler polling design on the initial-impl
    /// branch.
    /// </summary>
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SetObjectAsDiscovered))]
    internal static class SaveManagerDiscoveryHook
    {
        [HarmonyPostfix]
        static void OnSetObjectAsDiscovered(ObjectDataCD objectData, bool __result)
        {
            if (__result)
                Debug.Log($"[ItemChecklist] PROBE: new discovery — objectID={(int)objectData.objectID} variation={objectData.variation}");
        }
    }
}
