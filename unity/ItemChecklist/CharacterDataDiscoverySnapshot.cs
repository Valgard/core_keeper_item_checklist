using HarmonyLib;
using UnityEngine;

namespace ItemChecklist
{
    /// <summary>
    /// Harmony postfix on <c>CharacterData.OnAfterDeserialize</c>. Fires
    /// once per character slot on boot/save-load — CK deserializes all
    /// ~60 character slots, most of which are empty (Count=0). We only
    /// care about the ACTIVE character.
    ///
    /// <para>Filter strategy: only snapshot when <c>Manager.main.player</c>
    /// exists (i.e. we are past boot and a player is spawned) and the
    /// deserialized <c>CharacterData</c> matches the active player. Boot-
    /// time deserialize calls (before any player is spawned) are ignored —
    /// the active character will deserialize again later when the world
    /// loads.</para>
    ///
    /// <para>Active-character match: the plan called for matching on
    /// <c>characterGuid</c>, but decompiling <c>PlayerController</c> shows
    /// it does not expose <c>characterGuid</c> (only <c>playerName</c> and
    /// <c>networkName</c>; the canonical lookup <c>Manager.saves.
    /// GetCharacterGuid()</c> is blocked by the Roslyn sandbox). We fall
    /// back to comparing the character-customization <c>name</c>
    /// (<c>FixedString32Bytes</c>, accessible from sandbox) against
    /// <c>PlayerController.playerName</c>. This is correct for the
    /// single-player target use-case; if two characters share a name, the
    /// snapshot can over-trigger ("last-wins" between same-name characters)
    /// — acceptable because real new pickups still flow through the
    /// <see cref="SaveManagerDiscoveryHook"/> postfix.</para>
    ///
    /// Hook validated live on 2026-05-24: fired ~12 times during boot
    /// (Count=0 for empty slots), then once with Count=25 after world
    /// load for the active character.
    /// </summary>
    [HarmonyPatch(typeof(CharacterData), nameof(CharacterData.OnAfterDeserialize))]
    internal static class CharacterDataDiscoverySnapshot
    {
        [HarmonyPostfix]
        static void After(CharacterData __instance)
        {
            if (__instance == null || __instance.discoveredObjects2 == null) return;

            // Filter to active character. If player isn't spawned yet
            // (boot time), ignore — the active char will deserialize
            // again on world load.
            if (Manager.main == null || Manager.main.player == null) return;

            string activeName = Manager.main.player.playerName;
            if (string.IsNullOrEmpty(activeName)) return;

            // CharacterData.CharacterCustomization is a property returning
            // characterCustomizationNew; .name is a FixedString32Bytes whose
            // .Value getter materialises a string. Comparing via .ToString()
            // is equally valid; .Value is more explicit about intent.
            var customization = __instance.CharacterCustomization;
            string thisName = customization.name.Value;
            if (string.IsNullOrEmpty(thisName)) return;
            if (thisName != activeName) return;

            // Project DiscoveredObjectData.objectID to ints for our set.
            var ids = new System.Collections.Generic.List<int>(__instance.discoveredObjects2.Count);
            foreach (var d in __instance.discoveredObjects2)
                ids.Add((int) d.objectID);
            DiscoveredState.Instance.Snapshot(ids);
            Debug.Log($"[ItemChecklist] Snapshot: {ids.Count} discovered ids for char '{activeName}'");
        }
    }
}
