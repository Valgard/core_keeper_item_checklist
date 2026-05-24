using System;
using System.Collections.Generic;

namespace ItemChecklist
{
    /// <summary>
    /// In-memory mirror of <c>CharacterData.discoveredObjects2</c> for the
    /// currently active character. Populated by:
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

        private readonly HashSet<int> ids = new HashSet<int>();

        public int Count => ids.Count;
        public bool IsDiscovered(int objectId) => ids.Contains(objectId);

        /// <summary>Raised when a single new id is added.</summary>
        public event Action<int> Discovered;
        /// <summary>Raised after any mutation (Snapshot or AddOne).</summary>
        public event Action Changed;

        internal void AddOne(int objectId)
        {
            if (ids.Add(objectId))
            {
                // Diagnostic — remove once UI gives visible feedback (Phase F).
                UnityEngine.Debug.Log($"[ItemChecklist] AddOne: {objectId} (total {ids.Count})");
                Discovered?.Invoke(objectId);
                Changed?.Invoke();
            }
        }

        internal void Snapshot(IEnumerable<int> objectIds)
        {
            ids.Clear();
            foreach (var id in objectIds) ids.Add(id);
            Changed?.Invoke();
        }
    }
}
