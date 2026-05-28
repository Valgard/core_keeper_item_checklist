using HarmonyLib;

namespace ItemChecklist
{
    /// <summary>
    /// Harmony postfix on <c>SaveManager.SetObjectAsDiscovered</c>. CK
    /// returns <c>true</c> from this method iff the discovery was new
    /// (the HashSet.Add returned true). On every true result, mirror the
    /// objectID into <see cref="DiscoveredState"/>.
    ///
    /// Hook validated live on 2026-05-24: 7 pickups -> 7 postfix calls,
    /// no Harmony errors.
    /// </summary>
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SetObjectAsDiscovered))]
    internal static class SaveManagerDiscoveryHook
    {
        [HarmonyPostfix]
        static void After(ObjectDataCD objectData, bool __result)
        {
            if (!__result) return;
            DiscoveredState.Instance.AddOne((int) objectData.objectID, objectData.variation);
        }
    }
}
