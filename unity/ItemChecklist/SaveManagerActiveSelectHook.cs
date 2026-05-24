using HarmonyLib;
using UnityEngine;

namespace ItemChecklist
{
    /// <summary>
    /// Harmony postfix on <c>SaveManager.SetCharacterId(int id)</c>.
    ///
    /// <para>Pugstorm's sandbox blocks every direct read-path from our
    /// mod code to the active character's GUID (see
    /// <see cref="CharacterDataDiscoverySnapshot"/> for the four
    /// alternatives we tested and found banned). This class implements a
    /// state-machine workaround that needs NO sandbox-blocked APIs in
    /// our code path — only the <c>int id</c> parameter and a string field
    /// on <c>__instance</c> of the OnAfterDeserialize hook.</para>
    ///
    /// <para>How it works (mechanic verified from CK's
    /// <c>SaveManager.UseCustomCharacterDataProvider</c> decompile):</para>
    /// <list type="number">
    ///   <item>User selects char → CK calls <c>SaveManager.SetCharacterId(id)</c></item>
    ///   <item>CK proceeds to <c>Read(characterFiles[id])</c> + <c>DecodeJson</c>
    ///     + <c>CharacterData.OnAfterDeserialize</c> on the active char.</item>
    ///   <item>Our postfix on <c>SetCharacterId</c> sets
    ///     <see cref="AwaitingActiveDeserialize"/>.</item>
    ///   <item>The very next <c>OnAfterDeserialize</c> call with non-empty
    ///     <c>discoveredObjects2</c> (handled by
    ///     <see cref="CharacterDataDiscoverySnapshot"/>) is therefore the
    ///     active char — its <c>characterGuid</c> string becomes
    ///     <see cref="ActiveGuid"/>.</item>
    /// </list>
    ///
    /// <para>Edge case: a newly created character with zero discoveries.
    /// The "non-empty filter" would skip it. Handled by also accepting
    /// the first non-null-name deserialize after SetCharacterId, even
    /// with count=0 — see
    /// <see cref="CharacterDataDiscoverySnapshot.After"/>.</para>
    /// </summary>
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SetCharacterId))]
    internal static class SaveManagerActiveSelectHook
    {
        /// <summary>The active character's GUID (string form), or
        /// <c>null</c> if no char selected (main menu).</summary>
        public static string ActiveGuid { get; internal set; }

        /// <summary>Set true by SetCharacterId postfix, reset to false
        /// by the OnAfterDeserialize postfix once it has captured the
        /// active char's guid.</summary>
        internal static bool AwaitingActiveDeserialize;

        [HarmonyPostfix]
        static void After(int id)
        {
            if (id < 0)
            {
                // Return to main menu — clear state.
                ActiveGuid = null;
                AwaitingActiveDeserialize = false;
                return;
            }
            // A specific char slot was selected. The next
            // OnAfterDeserialize is (per CK's code path) for THIS char.
            AwaitingActiveDeserialize = true;
        }
    }
}
