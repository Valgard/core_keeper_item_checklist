using System;
using System.Collections.Generic;

namespace ItemChecklist
{
    /// <summary>
    /// In-memory mirror of <c>CharacterData.discoveredObjects2</c> for the
    /// currently active character. Keys are packed (objectId, variation)
    /// tuples — see <see cref="PackKey"/>.
    ///
    /// Populated by:
    /// <list type="bullet">
    ///   <item><see cref="CharacterDataDiscoverySnapshot"/> on save-load
    ///     (Harmony postfix on <c>CharacterData.OnAfterDeserialize</c>)</item>
    ///   <item><see cref="SaveManagerDiscoveryHook"/> on every new pickup
    ///     (Harmony postfix on <c>SaveManager.SetObjectAsDiscovered</c>)</item>
    /// </list>
    /// Read-only from the consumer's perspective — no public mutator. Both
    /// hook classes are in the same assembly so they can call the
    /// <c>internal</c> mutators.
    ///
    /// Persistence is handled by CK itself (the character save's
    /// <c>discoveredObjects2</c> serialization). We never write to
    /// PlayerPrefs or any file.
    /// </summary>
    public sealed class DiscoveredState
    {
        private static readonly DiscoveredState _instance = new DiscoveredState();
        public static DiscoveredState Instance => _instance;

        private readonly HashSet<long> keys = new HashSet<long>();

        /// <summary>
        /// Pack an (objectId, variation) pair into a single long key.
        /// Upper 32 bits: objectId. Lower 32 bits: variation (as uint, to
        /// preserve sign-bit identity since CookedFoodCD encodes via
        /// <c>(primary &lt;&lt; 16) | secondary</c>).
        /// </summary>
        public static long PackKey(int objectId, int variation) =>
            ((long)objectId << 32) | (uint)variation;

        public int Count => keys.Count;
        public bool IsDiscovered(int objectId, int variation) =>
            keys.Contains(PackKey(objectId, variation));

        /// <summary>Raised when a single new (objectId, variation) is added.</summary>
        public event Action<int, int> Discovered;
        /// <summary>Raised after any mutation (Snapshot or AddOne).</summary>
        public event Action Changed;

        internal void AddOne(int objectId, int variation)
        {
            if (keys.Add(PackKey(objectId, variation)))
            {
                UnityEngine.Debug.Log(
                    $"[ItemChecklist] AddOne: ({objectId}, {variation}) (total {keys.Count})");
                Discovered?.Invoke(objectId, variation);
                Changed?.Invoke();
            }
        }

        internal void Snapshot(IEnumerable<long> packedKeys)
        {
            keys.Clear();
            foreach (var k in packedKeys) keys.Add(k);
            Changed?.Invoke();
        }
    }
}
