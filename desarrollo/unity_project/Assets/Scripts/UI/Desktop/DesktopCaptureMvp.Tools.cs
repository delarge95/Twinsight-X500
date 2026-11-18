#if UNITY_STANDALONE
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.Telemetry;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    internal sealed partial class DesktopCaptureMvp
    {
        private DesktopChrome Chrome => DesktopUIController.Instance.Chrome;

        private bool ChipLive(string name)
        {
            Button b = B(name);
            return b != null && b.ClassListContains("dk-chip--live") && b.resolvedStyle.display != DisplayStyle.None;
        }

        private bool ChipGone(string name)
        {
            Button b = B(name);
            return b != null && !b.ClassListContains("dk-chip--live") && b.resolvedStyle.display == DisplayStyle.None;
        }

        private static string Tip(string en, string es) => AppLanguageManager.IsSpanish ? es : en;

        // ───────────────────────── P1: the registry of active-effect chips ─────────────────────────

        private IEnumerator ChipChecks()
        {
            Log("── P1: active-effect chips ──");
            var fx = Chrome.Effects;
            Check("the registry holds the four old chips and the tool chips (" + fx.Effects.Count + " effects)", fx != null && fx.Effects.Count >= 13);
            Check("at rest no tool chip is shown", fx.ActiveCount == 0 || (fx.ActiveCount == 1 && ChipLive("DkChipPower")));
            Cam.ResetView();
            yield return new WaitForSecondsRealtime(0.5f);

            // wind and a custom geofence (simulator)
            var w = Tel.WindRemembered.Clone(); w.speed = 4; w.gustSigma = 1; w.fromDeg = 90;
            Check("the simulator accepts a wind", Tel.ApplyWind(w).ok);
            var fence = new FenceConfig(80, 40, 10);
            Check("the simulator accepts a custom geofence", Tel.ApplyFence(fence).ok);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("the wind chip appears with its tooltip", ChipLive("DkChipWind") && B("DkChipWind").tooltip == Tip("Remove wind", "Quitar viento"));
            Check("the geofence chip appears with its tooltip", ChipLive("DkChipFence") && B("DkChipFence").tooltip == Tip("Remove custom geofence", "Quitar geocerca personalizada"));

            // the map by its shortcut M, the circuit by its button, the ground off, a view mode, piloting
            yield return PressKey(KeyCode.M);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("the key M opens the map and its chip follows (the registry reads the state, not the click)", Tel.MapOn && ChipLive("DkChipMap") && B("DkChipMap").tooltip == Tip("Remove map", "Quitar mapa"));
            Click(B("DkTelCourse"));
            yield return new WaitForSecondsRealtime(1.0f);
            Check("the circuit chip appears with the circuit", Tel.CourseOn && ChipLive("DkChipCourse") && B("DkChipCourse").tooltip == Tip("Remove circuit", "Quitar circuito"));
            Tel.SetGround(false);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("with the ground off a chip offers it back", ChipLive("DkChipGround") && B("DkChipGround").tooltip == Tip("Show ground", "Mostrar suelo"));
            ViewModeManager.Instance.SetViewMode(ViewMode.XRay);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("a render mode other than realistic has its chip", ChipLive("DkChipView"));
            Tel.SetPilot(true, "probe");
            yield return new WaitForSecondsRealtime(0.4f);
            Check("piloting has its chip", ChipLive("DkChipPilot") && B("DkChipPilot").tooltip == Tip("Stop piloting", "Dejar de pilotar"));
            Cam.SetDistance(300f, true);
            yield return new WaitForSecondsRealtime(0.6f);
            Check("seven tool chips + the session chip: the top bar does not overflow", !Chrome.TopBarOverflows());
            Log("top bar level " + Chrome.BarLevel + ", effects on " + fx.ActiveCount);
            yield return Shot("40_p1_chips_activos", 0.4f);

            // the chips switch their tools off, one click each
            Click(B("DkChipPilot")); yield return new WaitForSecondsRealtime(0.5f);
            Check("the pilot chip stops piloting and leaves", !Tel.Piloting && ChipGone("DkChipPilot"));
            Click(B("DkChipView")); yield return new WaitForSecondsRealtime(0.8f);
            Check("the view chip returns to the realistic view and leaves", ViewModeManager.Instance.CurrentMode == ViewMode.Realistic && ChipGone("DkChipView"));
            Click(B("DkChipGround")); yield return new WaitForSecondsRealtime(0.5f);
            Check("the ground chip puts the ground back and leaves", Tel.GroundOn && ChipGone("DkChipGround"));
            Click(B("DkChipCourse")); yield return new WaitForSecondsRealtime(0.8f);
            Check("the circuit chip removes the circuit and leaves", !Tel.CourseOn && ChipGone("DkChipCourse"));
            Click(B("DkChipMap")); yield return new WaitForSecondsRealtime(0.5f);
            Log("map: on=" + Tel.MapOn + " live=" + B("DkChipMap").ClassListContains("dk-chip--live") + " display=" + B("DkChipMap").resolvedStyle.display); Check("the map chip closes the map and leaves", !Tel.MapOn && ChipGone("DkChipMap"));
            Click(B("DkChipFence")); yield return new WaitForSecondsRealtime(0.5f);
            Log("fence: " + Tel.FenceRemembered.radius + "/" + Tel.FenceRemembered.ceiling + " live=" + B("DkChipFence").ClassListContains("dk-chip--live") + " display=" + B("DkChipFence").resolvedStyle.display); Check("the geofence chip restores the default geofence and leaves", Tel.FenceRemembered.radius == 120 && Tel.FenceRemembered.ceiling == 60 && ChipGone("DkChipFence"));
            Click(B("DkChipWind")); yield return new WaitForSecondsRealtime(0.5f);
            Log("wind: " + Tel.WindRemembered.speed + "/" + Tel.WindRemembered.gustSigma + " live=" + B("DkChipWind").ClassListContains("dk-chip--live") + " display=" + B("DkChipWind").resolvedStyle.display); Check("the wind chip calms the wind and leaves", Tel.WindRemembered.speed == 0 && Tel.WindRemembered.gustSigma == 0 && ChipGone("DkChipWind"));

            // synchronised with the shortcuts: M again and off again by the key; the old chips still work (explode)
            yield return PressKey(KeyCode.M);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("M on: chip on", ChipLive("DkChipMap"));
            yield return PressKey(KeyCode.M);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("M off: chip off", ChipGone("DkChipMap"));
            UIManager.Instance.ShortcutToggleExplode();
            yield return new WaitForSecondsRealtime(1.2f);
            Check("the explode chip (one of the registry's first citizens) still appears", ChipLive("DkChipExplode"));
            Click(B("DkChipExplode")); yield return new WaitForSecondsRealtime(1.2f);
            Check("... and a click on it assembles the drone again", ChipGone("DkChipExplode"));

            // mission and training chips
            DesktopTelemetry.Client.Send(ClientMessage.Takeoff(5), out _);
            yield return WaitAlt(5, 20f);
            while (Tel.MissionNow.waypoints.Count > 0) { Tel.MissionSelect(0); if (!Tel.MissionDeleteSelected()) break; }
            Tel.MissionAddPoint(25, 0); Tel.MissionAddPoint(25, 25);
            Check("a mission starts", Tel.MissionDo(MissionControl.Start));
            yield return new WaitForSecondsRealtime(1.0f);
            Check("the mission chip appears while it runs (tooltip «Detener misión»)", ChipLive("DkChipMission") && B("DkChipMission").tooltip == Tip("Stop mission", "Detener misión"));
            Click(B("DkChipMission")); yield return new WaitForSecondsRealtime(0.6f);
            Log("mission: busy=" + Tel.MissionBusy + " live=" + B("DkChipMission").ClassListContains("dk-chip--live") + " display=" + B("DkChipMission").resolvedStyle.display); Check("the chip aborts the mission and leaves", !Tel.MissionBusy && ChipGone("DkChipMission"));
            DesktopTelemetry.Client.Send(ClientMessage.Hold(), out _);
            Check("a lesson starts", Tel.StartTraining("takeoff-hold", false));
            yield return new WaitForSecondsRealtime(0.6f);
            Check("the training chip appears", ChipLive("DkChipTraining") && B("DkChipTraining").tooltip == Tip("Leave training", "Salir del entrenamiento"));
            Click(B("DkChipTraining")); yield return new WaitForSecondsRealtime(0.5f);
            Log("training: on=" + Tel.TrainingActive + " live=" + B("DkChipTraining").ClassListContains("dk-chip--live") + " display=" + B("DkChipTraining").resolvedStyle.display); Check("the chip leaves the lesson and goes away", !Tel.TrainingActive && ChipGone("DkChipTraining"));
            DesktopTelemetry.Client.Send(ClientMessage.Land(), out _);
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("41_p1_sin_chips", 0.2f);
        }

    }
}
#endif
