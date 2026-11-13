#if UNITY_STANDALONE
using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.Telemetry;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>The test-course half of the PLAN_PC 28.3 verification (-env-capture): the scripted lap, the deliberate collision, the finished lap, reset and the hidden course with another source.</summary>
    internal sealed partial class DesktopCaptureEnv
    {
        private IEnumerator RunCourseChecks()
        {
            string lang = Lang;
            Camera cam = Camera.main;
            // ── 5. the test course ──
            Log("── test course ──");
            Check("«Escenario» button exists and starts OFF, enabled (the simulator runs)", B("DkTelCourse") != null && !Tel.CourseOn && B("DkTelCourse").enabledSelf && !Tel.CourseActive && !Tel.CoursePillVisible);
            DesktopTelemetry.Client.Send(ClientMessage.Rtl(), out _);
            float rtlUntil = Time.realtimeSinceStartup + 75f;
            while (Time.realtimeSinceStartup < rtlUntil && Sim.Model.mode != SimMode.Idle && !Sim.Model.landed) yield return null;
            yield return new WaitForSecondsRealtime(1.0f);
            View("high", 70f);
            Click(B("DkTelCourse"));
            yield return new WaitForSecondsRealtime(1.0f);
            Check("«Escenario» on: the course is placed and the HUD pill shows the honest label", Tel.CourseOn && Tel.CourseActive && Tel.CoursePillVisible && Tel.CourseCaption == TelemetryStrings.T("course.label", lang));
            var scene = Tel.CourseScene;
            Check("the course has NO colliders, all on the Ignore Raycast layer", scene != null && !scene.HasColliders && AllOnLayer2(scene.transform));
            Check("the layout: 4 rings, 3 zones, 5 pylons, a wall with a gap, 7 ordered checkpoints, all within the 120 m geofence", scene.Layout.Gates.Count == 4 && scene.Layout.Zones.Count == 3 && scene.Layout.Pylons.Count == 5 && scene.Layout.Wall.Count == 4 && scene.Layout.Checkpoints.Count == 7 && scene.Layout.FarthestFromOrigin() < 120);
            Check("the HUD waits for the take-off", Tel.CourseRunNow.State == CourseState.Waiting && Tel.CourseHud.Contains(TelemetryStrings.T("course.waiting", lang)));
            Cam.SetAngles(-35f, 62f, true);
            Cam.SetDistance(2300f, true);
            yield return Shot("10_escenario_vista_general", 0.6f);
            Cam.SetAngles(-28f, 25f, true);
            Cam.SetDistance(110f, true);
            yield return Shot("11_escenario_helipuerto", 0.4f);

            // scripted lap: take off, fly the route; at the second waypoint a deliberate hit on the first pylon, then the rest of the lap
            DesktopTelemetry.Client.Send(ClientMessage.Takeoff(3.5), out _);
            yield return WaitAlt(3.5, 15f);
            yield return new WaitForSecondsRealtime(0.8f);
            Check("lap started with the take-off (timer running)", Tel.CourseRunNow.State == CourseState.Running);
            var route = scene.Layout.Route;
            Cam.SetAngles(-28f, 32f, true);
            Cam.SetDistance(420f, true);
            bool shotGate = false, shotHit = false, shotWall = false, shotSlalom = false;
            int collisionsBefore = 0;
            for (int i = 1; i < route.Count; i++)
            {
                double[] w = route[i];
                if (i == 3)
                {
                    // deliberate hit: straight into the first pylon of the slalom (north 32, east -2.5)
                    Log("deliberate collision: fly_to the first pylon");
                    collisionsBefore = Tel.CourseRunNow.Collisions;
                    yield return FlyTo(32, -2.5, 3.5, 12f, 0.5);
                    yield return new WaitForSecondsRealtime(0.25f);
                    Check("the collision is counted (1) and flashes: the HUD pill goes red", Tel.CourseRunNow.Collisions == collisionsBefore + 1 && Tel.CourseHitFlashing && Tel.CourseScene.StateOf("pylon:0") == "hit");
                    yield return Shot("12_colision_pilon", 0.02f);
                    shotHit = true;
                }
                yield return FlyTo(w[0], w[1], w[2], 25f);
                if (i == 1 && !shotGate) { yield return Shot("13_primer_aro", 0.05f); shotGate = true; }
                if (i == 4 && !shotSlalom) { Cam.SetAngles(-60f, 40f, true); Cam.SetDistance(300f, true); yield return Shot("14_slalom", 0.05f); shotSlalom = true; Cam.SetAngles(-28f, 32f, true); Cam.SetDistance(420f, true); }
                if (i == 9 && !shotWall) { Cam.SetAngles(-90f, 20f, true); Cam.SetDistance(300f, true); yield return Shot("15_muro_hueco", 0.05f); shotWall = true; Cam.SetAngles(-28f, 32f, true); Cam.SetDistance(420f, true); }
                if (Tel.CourseRunNow.State == CourseState.Finished) break;
            }
            yield return new WaitForSecondsRealtime(1.0f);
            var run = Tel.CourseRunNow;
            Log("lap: state " + run.State + ", checkpoints " + run.Reached + "/" + run.Total + ", time " + run.FinishTime.ToString("F1", CultureInfo.InvariantCulture) + " s, collisions " + run.Collisions + "; HUD: " + Tel.CourseHud);
            Check("the whole lap: 7/7 ordered checkpoints and the lap finished", run.State == CourseState.Finished && run.Reached == run.Total);
            Check("exactly one collision (the deliberate one); the clean parts of the route (gates, wall gap, other pylons) cost none", run.Collisions == 1);
            Check("the result message is shown: lap time and collisions", Tel.CourseResultText != null && Tel.CourseResultText.Contains(DesktopTelemetryUI.FormatLap(run.FinishTime)) && Tel.CourseResultText.Contains(": 1"));
            Check("every ring and zone turned «done»", scene.StateOf("gate:0") == "done" && scene.StateOf("gate:3") == "done" && scene.StateOf("zone:2") == "done");
            Cam.SetAngles(-35f, 62f, true);
            Cam.SetDistance(2300f, true);
            yield return Shot("16_vuelta_completa", 0.5f);

            // the simulator was not altered: it still flies the same way (a hold at the finish, no extra stop)
            Check("the simulator is unaffected: still armed and flying, in hold, inside the geofence", Sim.Model.armed && !Sim.Model.landed && Math.Sqrt(Sim.Model.p[0] * Sim.Model.p[0] + Sim.Model.p[1] * Sim.Model.p[1]) < 120);

            // reset
            Click(Tel.CourseResetButton);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("«Reiniciar»: timer, checkpoints and collisions back to zero, result hidden", Tel.CourseRunNow.State == CourseState.Waiting && Tel.CourseRunNow.Collisions == 0 && Tel.CourseRunNow.Reached == 0 && Tel.CourseResultText == null);
            yield return Shot("17_reiniciar", 0.2f);

            // hidden with another source / toggle off
            Tel.StartLocalReplay();
            yield return new WaitForSecondsRealtime(2.0f);
            Check("with the local replay the course is gone, the pill hidden and «Escenario» dimmed", !Tel.CourseActive && !Tel.CoursePillVisible && !B("DkTelCourse").enabledSelf == (!Tel.CourseOn));
            Tel.StartSimulator();
            yield return new WaitForSecondsRealtime(2.0f);
            Check("back to the simulator: the course is placed again, fresh", Tel.CourseActive && Tel.CourseRunNow.State == CourseState.Waiting);
            Click(B("DkTelCourse"));
            yield return new WaitForSecondsRealtime(0.5f);
            Check("«Escenario» off: course and pill removed", !Tel.CourseOn && !Tel.CourseActive && !Tel.CoursePillVisible);
            Tel.StopSession();
            yield return new WaitForSecondsRealtime(0.5f);

        }

        private static bool AllOnLayer2(Transform t)
        {
            foreach (var r in t.GetComponentsInChildren<Transform>(true)) if (r.gameObject.layer != 2) return false;
            return true;
        }
    }
}
#endif
