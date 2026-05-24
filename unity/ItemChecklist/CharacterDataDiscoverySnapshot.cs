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
    /// <para>Cache key: <c>CharacterData.characterGuid</c> (a public
    /// <c>string</c> field — eindeutig pro Char, same-name-resilient).
    /// The active-character resolution happens in
    /// <see cref="ItemChecklistMod.Update"/> via the player entity's
    /// <c>CharacterGuidCD</c> ECS component, whose <c>Hash128.ToString()</c>
    /// produces the same string format as <c>CharacterData.characterGuid</c>
    /// (CK itself does <c>Hash128.Parse(characterData[id].characterGuid)</c>
    /// in <c>SaveManager.GetCharacterGuid</c>, so they're round-trip
    /// equivalent).</para>
    /// </summary>
    [HarmonyPatch(typeof(CharacterData), nameof(CharacterData.OnAfterDeserialize))]
    internal static class CharacterDataDiscoverySnapshot
    {
        /// <summary>characterGuid → discovered objectIDs, populated as CK
        /// deserializes each character slot. Read by
        /// <see cref="ItemChecklistMod.Update"/> once the active player's
        /// ECS entity carries a matching <c>CharacterGuidCD</c>.</summary>
        internal static readonly Dictionary<string, int[]> Cache = new Dictionary<string, int[]>();

        [HarmonyPostfix]
        static void After(CharacterData __instance)
        {
            if (__instance == null || __instance.discoveredObjects2 == null) return;
            string guid = __instance.characterGuid;
            if (string.IsNullOrEmpty(guid)) return;

            int count = __instance.discoveredObjects2.Count;
            if (count == 0)
            {
                Cache[guid] = System.Array.Empty<int>();
                return;
            }

            var ids = new int[count];
            for (int i = 0; i < count; i++)
                ids[i] = (int) __instance.discoveredObjects2[i].objectID;
            Cache[guid] = ids;
        }
    }
}
