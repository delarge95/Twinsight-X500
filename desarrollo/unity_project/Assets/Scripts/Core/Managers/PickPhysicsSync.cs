using System;
using UnityEngine;

namespace WebGL.Core.Managers
{
    /// <summary>
    /// Keeps the physics colliders where the drone is DRAWN, so clicks and hover land on the visible parts.
    ///
    /// Root cause of "I can't click the drone" (PLAN_PC §26): the project runs physics with SimulationMode = Script
    /// (nothing is ever simulated) and AutoSyncTransforms = off. With that pair, moving a Transform does NOT move its collider for
    /// queries: Physics.Raycast keeps hitting the collider where it was at the last sync (only ExplodedViewManager called
    /// Physics.SyncTransforms). The telemetry driver (replay / simulator / SITL) and the manual power hover move the drone root every
    /// frame, so the picking colliders stayed at the rest pose: a click on the flying drone hit nothing (or a stale part), there was no
    /// hover, and the background click deselected / flew to the ground. Selecting from the part tree did not raycast, so it worked.
    ///
    /// Fix (desktop): auto-sync transforms before every physics query (only dirty transforms are pushed, so the cost is the moved
    /// drone hierarchy). Test flag <c>-no-physsync</c> restores the old behaviour.
    /// </summary>
    public static class PickPhysicsSync
    {
        /// <summary>True when queries see the current transforms.</summary>
        public static bool Enabled => Physics.autoSyncTransforms;

        /// <summary>Turns automatic transform syncing for physics queries on or off (pure seam for tests).</summary>
        public static void Apply(bool enabled)
        {
            Physics.autoSyncTransforms = enabled;
            if (enabled)
            {
                Physics.SyncTransforms();
            }
        }

#if UNITY_STANDALONE
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            bool disabled = Array.IndexOf(Environment.GetCommandLineArgs(), "-no-physsync") >= 0;
            Apply(!disabled);
            Debug.Log("[Pick] physics transform sync for picking: " + (disabled ? "OFF (-no-physsync, old behaviour)" : "on"));
        }
#endif
    }
}
