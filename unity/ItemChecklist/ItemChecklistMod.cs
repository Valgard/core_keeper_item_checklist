using System.Linq;
using CoreLib;
using CoreLib.Submodule.ControlMapping;
using ItemChecklist.UI;
using PugMod;
using Rewired;
using UnityEngine;

namespace ItemChecklist
{
    /// <summary>
    /// Mod bootstrap. After the Harmony pivot, the heavy lifting is in
    /// the two patch classes (<see cref="SaveManagerDiscoveryHook"/> and
    /// <see cref="CharacterDataDiscoverySnapshot"/>) that mirror CK's
    /// native discovery system into <see cref="DiscoveredState"/>.
    ///
    /// <para>The only non-trivial work this class does is bridge the
    /// timing gap between CK's <c>OnAfterDeserialize</c> (which fires
    /// before <c>Manager.main.player</c> exists) and the active player's
    /// spawn. Each frame, if a player has spawned, look up the cached
    /// snapshot by <c>playerName</c> and apply it. See
    /// <see cref="CharacterDataDiscoverySnapshot"/> for the rationale
    /// behind the name-based cache key (more accurate alternatives all
    /// turned out sandbox-blocked).</para>
    /// </summary>
    public sealed class ItemChecklistMod : IMod
    {
        public static ItemCatalog Catalog { get; private set; }

        // AssetBundle reference — set in EarlyInit. UI code loads sprites
        // (window background, placeholder icon, etc.) via
        // AssetBundle.LoadAsset<Sprite>("Assets/ItemChecklist/Art/..."),
        // same pattern Item Browser uses.
        public static AssetBundle AssetBundle { get; private set; }
        public static LoadedMod ModInfo { get; private set; }

        // F1-Toggle UI controller. Single instance; builds the window on
        // first Toggle() call.
        private static readonly UiController Ui = new UiController();

        // Rewired player captured via ControlMappingModule.rewiredStart
        // (Rewired is not ready at EarlyInit). Used to poll the bound
        // toggle action each frame.
        private static Player rewiredPlayer;

        private const string ToggleActionName = "ItemChecklist.Toggle";

        // Last character name we applied a snapshot for. Reset when
        // Manager.main.player goes back to null (main menu) so the next
        // char-load gets its own snapshot pushed.
        private string lastAppliedFor;

        public void EarlyInit()
        {
            Debug.Log("[ItemChecklist] EarlyInit");

            // Grab our AssetBundle handle (sprites for the UI live here).
            // Pattern from Item Browser's Main.EarlyInit.
            ModInfo = API.ModLoader.LoadedMods.FirstOrDefault(m => m.Handlers.Contains(this));
            if (ModInfo != null && ModInfo.AssetBundles != null && ModInfo.AssetBundles.Count > 0)
            {
                AssetBundle = ModInfo.AssetBundles[0];
                var names = AssetBundle.GetAllAssetNames();
                Debug.Log($"[ItemChecklist] AssetBundle loaded with {names.Length} assets:");
                foreach (var n in names) Debug.Log($"[ItemChecklist]   asset: {n}");
            }
            else
            {
                Debug.LogWarning("[ItemChecklist] AssetBundle not available — UI will fall back to default Unity sprites");
            }

            CoreLibMod.LoadSubmodule(typeof(ControlMappingModule));

            // Register the toggle keybind (default F1). CoreLib's
            // ControlMappingModule wires this into Rewired's UserData so the
            // user can rebind it through the game's input-settings UI.
            ControlMappingModule.AddKeyboardBind(
                keyBindName: ToggleActionName,
                defaultKeyCode: KeyboardKeyCode.F1,
                modifier: ModifierKey.None,
                modifier2: ModifierKey.None,
                modifier3: ModifierKey.None,
                categoryId: -1);     // -1 = default "Mods" category

            // Rewired isn't initialized at EarlyInit; subscribe to the
            // rewiredStart hook so we grab the player handle as soon as it
            // exists. Mirrors CoreLib's own CommandModule pattern.
            ControlMappingModule.rewiredStart += () =>
            {
                rewiredPlayer = ReInput.players.GetPlayer(0);
                Debug.Log("[ItemChecklist] Rewired player captured");
            };
        }

        public void Init()
        {
            Debug.Log("[ItemChecklist] Init");
            Catalog = new ItemCatalog();
            Catalog.Bake();
        }

        public void ModObjectLoaded(Object obj) { }
        public void Shutdown() { }

        public void Update()
        {
            string activeGuid = SaveManagerActiveSelectHook.ActiveGuid;

            // No active character (main menu) — clear "applied for"
            // memory so the next char-select pushes a fresh snapshot.
            if (string.IsNullOrEmpty(activeGuid))
            {
                if (lastAppliedFor != null) lastAppliedFor = null;
            }
            else if (activeGuid != lastAppliedFor
                && CharacterDataDiscoverySnapshot.Cache.TryGetValue(activeGuid, out var ids))
            {
                DiscoveredState.Instance.Snapshot(ids);
                lastAppliedFor = activeGuid;
                Debug.Log($"[ItemChecklist] Snapshot applied: {ids.Length} ids for guid {activeGuid}");
            }

            // Hotkey poll — dual-path while we're verifying which one
            // actually fires in-game. Rewired is the production target
            // (rebindable via game settings); raw Input is the diagnostic
            // fallback. Whichever fires first wins.
            bool rewiredFired = rewiredPlayer != null && rewiredPlayer.GetButtonDown(ToggleActionName);
            bool rawFired = Input.GetKeyDown(KeyCode.F1);
            if (rewiredFired || rawFired)
            {
                // Game-state guard — pattern from Item Browser
                // (ItemBrowserUI.cs:201). Block the hotkey when another
                // menu (Main, Inventory, Pause, Char-Select…) is active,
                // when another text input has focus, or when the chat
                // window is open. Skip the guard when our own window is
                // already visible so the user can always close it.
                if (!Ui.IsVisible
                    && (Manager.menu.IsAnyMenuActive()
                        || Manager.input.textInputIsActive
                        || ReferenceEquals(Manager.input.activeInputField, Manager.ui.chatWindow)))
                {
                    Debug.Log("[ItemChecklist] Hotkey ignored (other menu/input active)");
                    return;
                }
                Debug.Log($"[ItemChecklist] Hotkey: rewired={rewiredFired} raw={rawFired} — toggling UI");
                Ui.Toggle();
            }
        }
    }
}
