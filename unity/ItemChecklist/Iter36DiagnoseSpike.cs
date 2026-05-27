using System;
using HarmonyLib;
using UnityEngine;

namespace ItemChecklist
{
    /// <summary>
    /// TEMPORARY — Phase-0 diagnose for Iter-3.6. Logs four things:
    ///   D1: outcome of PlayerController.GetObjectName called in Init()
    ///   D2a: when PugDatabase.UpdateEntityMonos first runs + GetObjectName result there
    ///   D2b: when SaveManager.SetWorldId first runs + GetObjectName result there
    ///   D2c: when PlayerController.OnOccupied first runs + GetObjectName result there
    /// Removed in Plan-Task 9 once the diagnose-doc is written.
    /// </summary>
    public static class Iter36DiagnoseSpike
    {
        // A known-vanilla objectID that should resolve to "Wood" or similar in EN.
        private const int ProbeObjectId = 5; // Wood — small ID, almost certainly vanilla

        public static void LogProbe(string anchorTag)
        {
            try
            {
                var fields = PlayerController.GetObjectName(
                    new ContainedObjectsBuffer { objectData = new ObjectDataCD { objectID = (ObjectID)ProbeObjectId } },
                    false);
                string text = fields.text ?? "<null>";
                Debug.Log($"[Iter36Diag] anchor={anchorTag} probeID={ProbeObjectId} text=\"{text}\" managerMainPlayerNull={Manager.main?.player == null}");
            }
            catch (NullReferenceException ex)
            {
                Debug.Log($"[Iter36Diag] anchor={anchorTag} probeID={ProbeObjectId} THREW NullReferenceException: {ex.Message} managerMainPlayerNull={Manager.main?.player == null}");
            }
            catch (Exception ex)
            {
                // Sandbox-safe: do NOT call ex.GetType().Name — Type.Name resolves
                // to MemberInfo.get_Name() and is blocked by the Roslyn sandbox.
                Debug.Log($"[Iter36Diag] anchor={anchorTag} probeID={ProbeObjectId} THREW (non-NRE): {ex.Message} managerMainPlayerNull={Manager.main?.player == null}");
            }
        }
    }

    [HarmonyPatch(typeof(PugDatabase), nameof(PugDatabase.UpdateEntityMonos))]
    internal static class Iter36DiagD2A_UpdateEntityMonos
    {
        private static bool fired;
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (fired) return;
            fired = true;
            Iter36DiagnoseSpike.LogProbe("D2a:PugDatabase.UpdateEntityMonos");
        }
    }

    // NOTE: SaveManager.OnSaveLoaded does not exist in Pug.Other.dll (verified by
    // ilspycmd decompile). Substituted with SaveManager.SetWorldId — CK calls this
    // method when the player selects a world to load, which is the earliest reliable
    // signal that a save is being entered (fires before the world scene loads).
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SetWorldId))]
    internal static class Iter36DiagD2B_SetWorldId
    {
        private static bool fired;
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (fired) return;
            fired = true;
            Iter36DiagnoseSpike.LogProbe("D2b:SaveManager.SetWorldId");
        }
    }

    // NOTE: PlayerController.OnSpawn does not exist in Pug.Other.dll (verified by
    // ilspycmd decompile). Substituted with PlayerController.OnOccupied — this is
    // the actual spawn entry point: it calls PlayerInit() and is the first method
    // CK invokes on PlayerController when the local player entity is spawned into
    // the world.
    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.OnOccupied))]
    internal static class Iter36DiagD2C_OnOccupied
    {
        private static bool fired;
        [HarmonyPostfix]
        private static void Postfix()
        {
            if (fired) return;
            fired = true;
            Iter36DiagnoseSpike.LogProbe("D2c:PlayerController.OnOccupied");
        }
    }
}
