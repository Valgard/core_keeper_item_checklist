using CoreLib;
using CoreLib.Submodule.ControlMapping;
using PugMod;
using UnityEngine;

namespace ItemChecklist
{
    /// <summary>
    /// Mod bootstrap. The Pugstorm mod loader instantiates this on game
    /// start and calls the IMod lifecycle methods.
    ///
    /// G1a wires the data + trigger layers (state, store, catalog, scanner,
    /// poller). UI is not wired yet (Phase F). The wiring uses polling
    /// rather than world-load events because the exact PugMod world-event
    /// API is a live-verification TODO — Manager.main.player going from null
    /// to non-null is a robust proxy.
    /// </summary>
    public sealed class ItemChecklistMod : IMod
    {
        private const float DebugLogIntervalSec = 5f;
        private float nextDebugLogTime;

        public void EarlyInit()
        {
            Debug.Log("[ItemChecklist] EarlyInit");
            CoreLibMod.LoadSubmodule(typeof(ControlMappingModule));
        }

        public void Init()
        {
            Debug.Log("[ItemChecklist] Init");
        }

        public void ModObjectLoaded(Object obj) { }

        public void Shutdown()
        {
            ChecklistRuntime.End();
        }

        public void Update()
        {
            // World-begin detection by polling: Manager.main.player flips
            // from null to non-null when a world is loaded and the player
            // entity is spawned. Cheap check per frame; no allocations.
            bool playerReady = Manager.main != null && Manager.main.player != null;
            bool runtimeUp = ChecklistRuntime.Instance != null;

            if (playerReady && !runtimeUp)
            {
                ChecklistRuntime.Begin(ResolveLocalPlayerId(), ResolveCurrentWorldId());
            }
            else if (!playerReady && runtimeUp)
            {
                ChecklistRuntime.End();
            }

            // Per-frame tick the polling + auto-save loops.
            var rt = ChecklistRuntime.Instance;
            if (rt != null)
            {
                float t = Time.unscaledTime;
                rt.Poller.Tick(t);
                rt.Store.Tick(t);

                if (t >= nextDebugLogTime)
                {
                    nextDebugLogTime = t + DebugLogIntervalSec;
                    Debug.Log($"[ItemChecklist] tick: owned={rt.State.OwnedCount} discovered={rt.State.DiscoveredCount} catalog={rt.Catalog.Count}");
                }
            }
        }

        private static string ResolveLocalPlayerId()
        {
            // TODO (live-verify): pick a stable per-player key — Steam ID,
            // PugMod auth ID, player display name. For now "local" works
            // for single-player; Coop will need a real ID.
            return "local";
        }

        private static string ResolveCurrentWorldId()
        {
            // Decompile probe of Pug.Other.dll/SaveManager.cs shows
            // `Manager.saves.GetWorldId()` is the canonical surface — it
            // returns the active save-slot id (int, 0..29). That is what
            // the game uses internally to distinguish worlds, and is
            // exactly the partition key we want for PlayerPrefs.
            //
            // TODO (live-verify, multiplayer): on a remote server the
            // save-slot id is the host's local id, not unique across
            // hosts. If we ever want per-server isolation in Coop, fold
            // in Manager.networking.serverGuid (see SaveManager.GetServerId).
            //
            // Fall back to "default" if any hop is null so the mod still
            // works (single PlayerPrefs key for the whole machine) when
            // we get called before SaveManager is fully wired.
            try
            {
                if (Manager.saves != null)
                    return Manager.saves.GetWorldId().ToString();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ItemChecklist] ResolveCurrentWorldId fell back to 'default': {ex.GetType().Name}: {ex.Message}");
            }
            return "default";
        }
    }
}
