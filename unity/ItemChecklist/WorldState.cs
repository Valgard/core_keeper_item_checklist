namespace ItemChecklist
{
    /// <summary>
    /// Shared "is the local player actually in a playable world right now?"
    /// predicate, mirroring Core Keeper's own gameplay-active gate
    /// (<c>PlayerController.PlayerInputBlocked</c>, decompile Pug.Other ~line
    /// 130335: <c>currentSceneHandler.isSceneHandlerReady &amp;&amp;
    /// !Manager.load.IsLoadingAndScreenBlack()</c>).
    ///
    /// <para><strong>Why not <c>Manager.main.player != null</c>:</strong> the
    /// player object is instantiated at <c>PlayerController.OnOccupied</c> —
    /// the very anchor that kicks our catalog bake — which fires while the
    /// world-load screen is still up, and it survives into the exit-to-menu
    /// transition. So <c>player != null</c> is true across BOTH load screens
    /// and cannot suppress them. The reliable signal is
    /// <c>!Manager.load.IsLoading()</c> (<c>loadingQueue != null</c>), which is
    /// true from the moment a scene load is queued until it completes — for
    /// both entering and leaving a world.</para>
    ///
    /// <para>CK's own <c>IsLoadingAndScreenBlack()</c> is deliberately NOT used
    /// here: it is only true while the screen is <em>fully</em> black, so during
    /// the exit fade-out (screen still partly visible, load already queued) it
    /// returns false and the HUD would briefly flash. <c>IsLoading()</c> covers
    /// that window. <c>isSceneHandlerReady</c> complements it for the few frames
    /// where the queue is already cleared but the scene is not yet fully set up.</para>
    ///
    /// <para>Used by both the always-on HUD (<see cref="UI.ItemChecklistHud"/>)
    /// and the F1 open-guard (<see cref="ItemChecklistMod"/>). Cutscenes/intro
    /// are a separate input-locked signal and remain out of scope (roadmap
    /// Iter-15).</para>
    /// </summary>
    internal static class WorldState
    {
        public static bool IsInPlayableWorld
        {
            get
            {
                var sceneHandler = Manager.sceneHandler;
                return sceneHandler != null
                    && sceneHandler.isInGame
                    && sceneHandler.isSceneHandlerReady       // scene fully set up (false during load)
                    && Manager.main != null
                    && Manager.main.player != null            // sanity / NRE guard (NOT a load-screen signal)
                    && Manager.load != null
                    && !Manager.load.IsLoading();             // no scene load queued/in-flight — covers entry load screen AND exit-to-menu fade
            }
        }
    }
}
