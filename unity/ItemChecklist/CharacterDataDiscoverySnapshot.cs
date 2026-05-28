using System.Collections.Generic;
using HarmonyLib;

namespace ItemChecklist
{
    /// <summary>
    /// Harmony postfix on <c>CharacterData.OnAfterDeserialize</c>.
    ///
    /// <para>Timing reality (verified live 2026-05-24): CK fires this for
    /// ALL character slots on boot / char-select / world-load. EVERY fire
    /// happens BEFORE <c>Manager.main.player</c> is spawned. We cannot
    /// filter live; we cache and resolve later.</para>
    ///
    /// <para>Cache key: <c>playerName</c> from <c>CharacterCustomization</c>.
    /// The active-character resolution happens in
    /// <see cref="ItemChecklistMod.Update"/> once the player spawns and
    /// exposes <c>playerName</c>.</para>
    ///
    /// <para><b>Same-name trade-off:</b> if two characters share a
    /// display name on the same save, the cache key collides and the
    /// last deserialized "Hans" wins. We tried three more accurate
    /// alternatives — ALL sandbox-blocked:</para>
    /// <list type="bullet">
    ///   <item><c>PlayerController.characterGuid</c> — does not exist</item>
    ///   <item><c>Manager.saves.GetCharacterGuid()</c> — whole
    ///     <c>SaveManager</c> instance-access banned</item>
    ///   <item><c>HarmonyLib.Traverse</c> on the private
    ///     <c>characterData[]</c> field — 1 type + 3 member illegal refs
    ///     (verified live 2026-05-24)</item>
    ///   <item><c>EntityManager.HasComponent&lt;CharacterGuidCD&gt;(playerEntity)</c>
    ///     + <c>GetComponentData&lt;CharacterGuidCD&gt;</c> — 1 namespace +
    ///     1 type + 1 member illegal refs (verified live 2026-05-24)</item>
    /// </list>
    /// <para>Acceptable for single-player target use-case. Mitigation:
    /// real new pickups still flow through <see cref="SaveManagerDiscoveryHook"/>
    /// and CK's <c>SetObjectAsDiscovered</c> is the source of truth — so
    /// a wrong initial snapshot self-heals on the next pickup of any
    /// not-yet-cached item.</para>
    /// </summary>
    [HarmonyPatch(typeof(CharacterData), nameof(CharacterData.OnAfterDeserialize))]
    internal static class CharacterDataDiscoverySnapshot
    {
        /// <summary>player-name → discovered objectIDs, populated as CK
        /// deserializes each character slot. Read by
        /// <see cref="ItemChecklistMod.Update"/> once the active player
        /// spawns.</summary>
        internal static readonly Dictionary<string, long[]> Cache = new Dictionary<string, long[]>();

        [HarmonyPostfix]
        static void After(CharacterData __instance)
        {
            if (__instance == null || __instance.discoveredObjects2 == null) return;

            string guid = __instance.characterGuid;
            if (string.IsNullOrEmpty(guid)) return;

            int count = __instance.discoveredObjects2.Count;
            long[] packedKeys;
            if (count == 0)
            {
                packedKeys = System.Array.Empty<long>();
            }
            else
            {
                packedKeys = new long[count];
                for (int i = 0; i < count; i++)
                {
                    var record = __instance.discoveredObjects2[i];
                    packedKeys[i] = DiscoveredState.PackKey((int) record.objectID, record.variation);
                }
            }

            // Cache by guid for later lookup.
            Cache[guid] = packedKeys;

            // Active-char detection: if SetCharacterId(id) was just called,
            // this deserialize is for the active char (per CK's sequential
            // code path: SetCharacterId → file read → JsonOverwrite →
            // OnAfterDeserialize on that specific instance).
            if (SaveManagerActiveSelectHook.AwaitingActiveDeserialize)
            {
                SaveManagerActiveSelectHook.ActiveGuid = guid;
                SaveManagerActiveSelectHook.AwaitingActiveDeserialize = false;
            }
        }
    }
}
