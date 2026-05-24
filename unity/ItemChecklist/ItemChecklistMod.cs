using CoreLib;
using CoreLib.Submodule.ControlMapping;
using PugMod;
using UnityEngine;

namespace ItemChecklist
{
    /// <summary>
    /// Mod bootstrap. After the Harmony pivot, the heavy lifting is in
    /// the two patch classes (<see cref="SaveManagerDiscoveryHook"/> and
    /// <see cref="CharacterDataDiscoverySnapshot"/>) that mirror CK's
    /// native discovery system into <see cref="DiscoveredState"/>.
    ///
    /// <para>This class has very little to do:</para>
    /// <list type="bullet">
    ///   <item>Load CoreLib's ControlMappingModule (for the future hotkey)</item>
    ///   <item>Bake the <see cref="ItemCatalog"/> once on Init</item>
    /// </list>
    /// The UI layer (Phase F) wires the hotkey and reads
    /// <see cref="DiscoveredState"/> + <see cref="ItemCatalog"/>.
    /// </summary>
    public sealed class ItemChecklistMod : IMod
    {
        public static ItemCatalog Catalog { get; private set; }

        public void EarlyInit()
        {
            Debug.Log("[ItemChecklist] EarlyInit");
            CoreLibMod.LoadSubmodule(typeof(ControlMappingModule));
        }

        public void Init()
        {
            Debug.Log("[ItemChecklist] Init");
            Catalog = new ItemCatalog();
            Catalog.Bake();
        }

        public void ModObjectLoaded(Object obj) { }
        public void Shutdown() { }
        public void Update() { }
    }
}
