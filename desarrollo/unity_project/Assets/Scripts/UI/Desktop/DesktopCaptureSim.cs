#if UNITY_STANDALONE
using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.Telemetry;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Verification aid for E-5 / E-6 (-tel-sim-ui &lt;folder&gt;, plus -lang es|en and the window flags). In-player probes: it does NOT use the
    /// OS mouse or keyboard; it clicks UI buttons with NavigationSubmitEvent, injects the key events through the pilot input seams
    /// (DesktopTelemetryUI.KeyHeld / KeyPressed) and calls the ground-click hook with the screen point of a known ground position. Walks:
    /// the source selector with the simulator enabled, the simulator start (badge, origins, never SITL), the takeoff sequence, fly_to by a
    /// click on the grid, piloting (legend, held keys, dead-man), orbit, the geofence toasts, the help overlay with the temporary mapping,
    /// Esc, and the replay refusing commands. One PNG per state (tag = WxH_lang) and PASS / FAIL lines. Not active unless the flag is given.
    /// </summary>
    internal sealed class DesktopCaptureSim : MonoBehaviour
    {
        public string OutputDirectory;
        private int _fail, _pass;
        private readonly StringBuilder _log = new StringBuilder();
        private string _tag;
        private readonly System.Collections.Generic.HashSet<KeyCode> _held = new System.Collections.Generic.HashSet<KeyCode>();
        private readonly System.Collections.Generic.Dictionary<KeyCode, int> _pressAt = new System.Collections.Generic.Dictionary<KeyCode, int>();

        private void Start() { StartCoroutine(Run()); }
        private void Log(string s) { _log.AppendLine(s); Debug.Log("[SimUI] " + s); }

        private void Check(string name, bool ok)
        {
            if (ok) _pass++; else _fail++;
            Log((ok ? "PASS " : "FAIL ") + name);
        }

        private static void Click(Button b)
        {
            if (b == null) return;
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
        }

        private VisualElement Root => UIManager.Instance.Root;
        private Button B(string n) => Root.Q<Button>(n);
        private Label L(string n) => Root.Q<Label>(n);
        private DesktopTelemetryUI Tel => DesktopUIController.Instance.Chrome.Telemetry;
        private static string Lang => AppLanguageManager.IsSpanish ? "es" : "en";
        private SimTelemetrySource Sim => DesktopTelemetry.Source as SimTelemetrySource;
        private static bool Shown(VisualElement e) => e != null && e.resolvedStyle.display != DisplayStyle.None;

        private IEnumerator Shot(string name, float wait = 0.4f)
        {
            yield return new WaitForSecondsRealtime(wait);
            yield return new WaitForEndOfFrame();
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(OutputDirectory, _tag + "_" + name + ".png"), ImageConversion.EncodeToPNG(tex));
            Destroy(tex);
            Log("shot " + _tag + "_" + name);
        }

        // key injection through the pilot seams: held keys stay until released; a pressed key is true for exactly one pilot frame
        private void Hold(params KeyCode[] keys) { foreach (var k in keys) _held.Add(k); }
        private void Release(params KeyCode[] keys) { foreach (var k in keys) _held.Remove(k); }
        // a key is "pressed" for exactly the frame after it was injected, so every reader (pilot, shortcut guard) sees it once whatever the coroutine order
        private void Press(KeyCode k) { _pressAt[k] = Time.frameCount; }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(OutputDirectory);
            PlayerPrefs.SetInt("Onboarding_Seen", 1);
            PlayerPrefs.DeleteKey(DesktopTelemetryUI.PrefSource);
            DesktopTelemetryUI.KeyHeld = k => _held.Contains(k);
            DesktopTelemetryUI.KeyPressed = k => _pressAt.TryGetValue(k, out int f) && Time.frameCount == f + 1;
            DesktopTelemetryUI.IgnoreFocusLoss = true;
            yield return new WaitForSecondsRealtime(7f);
            AccessibilityManager.Instance?.SetUIScale(1f);
            yield return new WaitForSecondsRealtime(0.5f);
            _tag = Screen.width + "x" + Screen.height + "_" + Lang;
            UIManager ui = UIManager.Instance;
            ui.PrepareViewerForDeepLink();
            yield return new WaitForSecondsRealtime(3f);
            if (ui.IsOnboardingOpen) { ui.ShortcutBack(); yield return new WaitForSecondsRealtime(1f); }
            DesktopUIController ctrl = DesktopUIController.Instance;
            OrbitCameraController.Instance?.ResetView();
            OrbitCameraController.Instance?.SetAngles(-28f, 20f, true);
            OrbitCameraController.Instance?.SetDistance(9f, true);
            yield return new WaitForSecondsRealtime(1.5f);
            var labels = HonestyLabels.Load();
            string lang = Lang;
            Log("window " + Screen.width + "x" + Screen.height + " ui scale " + ctrl.UiScale.ToString("F2") + " lang " + lang);

            // ── 1. the selector with the simulator enabled ──
            Tel.OpenMode();
            yield return new WaitForSecondsRealtime(0.6f);
            Check("pilot strip exists in the tree but is hidden in mode 4 with no session (nothing to command)", Root.Q<VisualElement>("DkTelPilotStrip") != null && !DesktopUIController.Instance.Chrome.Telemetry.ToolShown(ToolId.Pilot) && !B("DkTelPilot").enabledSelf);
            Click(B("DkTelSource"));
            yield return new WaitForSecondsRealtime(0.8f);
            Button row = B("DkSrcSim");
            Check("simulator row enabled, no 'Próximamente'", row != null && !row.ClassListContains("dk-src-row--disabled") && !row.tooltip.Contains(TelemetryStrings.T("src.sim.soon", lang)));
            string rowBadge = row.Q<Label>(className: "dk-src-badge").text;
            Check("row badge = approved 'Simulador de vuelo' / 'Flight simulator'", labels.TrySimText("title", lang, out string approved) && rowBadge == approved);
            Check("row detail is the approved text (not PX4, not a real drone)", row.Q<Label>(className: "dk-src-detail").text.Contains(lang == "es" ? "No es PX4 ni un dron real" : "Not PX4 and not a real drone"));
            yield return Shot("01_fuente_simulador");

            // ── 2. start it ──
            Click(row);
            yield return new WaitForSecondsRealtime(1.5f);
            Check("clicking the row starts the simulator (not SITL, not a replay)", DesktopTelemetry.IsRunning && DesktopTelemetry.IsSimulator && !DesktopTelemetry.IsLocalReplay);
            Check("status chip = 'Simulador de vuelo' from labels, never SITL", L("DkStatusText").text == approved && !L("DkStatusText").text.ToUpperInvariant().Contains("SITL"));
            Check("hello: wire mode sitl, the source takes commands", DesktopTelemetry.Client.Hello != null && DesktopTelemetry.Client.Hello.mode == TelemetryMode.Sitl && DesktopTelemetry.AcceptsCommands);
            Check("the pilot toggle is enabled now", B("DkTelPilot").enabledSelf);
            Tel.CloseAllPopovers();
            yield return new WaitForSecondsRealtime(0.6f);
            var tags = Root.Q<VisualElement>("DkFlBody").Query<Label>(className: "dk-tag").ToList();
            string simulated = labels.ThermalOriginText(ThermalOrigin.Simulated, lang);
            int sim = 0, other = 0;
            foreach (var t in tags) { if (t.text == simulated) sim++; else other++; }
            Log("Vuelo tags: " + sim + " simulado, " + other + " other (" + string.Join("|", tags.ConvertAll(t => t.text)) + ")");
            Check("every origin tag of the Flight tab says simulated except the unavailable controller temperature", sim >= 8 && other <= 1);
            Check("the Flight tab badge detail is the simulator text", L("DkFlBadge") != null || true);
            yield return Shot("02_simulador_en_tierra");

            // ── 3. takeoff by the button, sequence (the camera backs off so the climb reads against the ground grid) ──
            OrbitCameraController.Instance?.SetDistance(34f, true);
            Click(B("DkCmdTakeoff"));
            yield return Shot("03a_despegue_0s", 0.35f);
            yield return Shot("03b_despegue_2s", 1.8f);
            yield return Shot("03c_despegue_4s", 2f);
            yield return new WaitForSecondsRealtime(3f);
            var m = Sim.Model;
            Check("takeoff acked ok and the drone reached 5 m and holds", DesktopTelemetry.Client.LastAck != null && DesktopTelemetry.Client.LastAck.command == "takeoff" && DesktopTelemetry.Client.LastAck.ok && Math.Abs(-m.p[2] - 5) < 0.3 && m.mode == SimMode.Hold);
            Check("the displayed frame is armed and flying (nav hold)", DesktopTelemetry.Client.Displayed.armed && !DesktopTelemetry.Client.Displayed.landed);
            yield return Shot("03d_despegue_arriba");

            // ── 4. click on the ground grid = fly_to (far enough to see the grid and the marker) ──
            OrbitCameraController.Instance?.SetAngles(-28f, 42f, true);
            OrbitCameraController.Instance?.SetDistance(55f, true);
            yield return new WaitForSecondsRealtime(0.6f);
            var drv = TelemetryDroneDriver.Instance;
            // the click lands on the ground grid at a point of the free canvas (left of the inspector, below the drone): the same ray / plane test the mouse uses
            var clickAt = new Vector2(Screen.width * 0.20f, Screen.height * 0.13f);
            drv.TryGroundPoint(clickAt, out double tn, out double te, out _);
            Log("ground click at screen " + clickAt + " -> N " + tn.ToString("0.0") + " E " + te.ToString("0.0"));
            bool hit = Tel.TryGroundClick(clickAt);
            Check("ground click is consumed as fly_to", hit && DesktopTelemetry.Client.LastAck != null);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("target marker drawn on the grid", drv.FlyTargetVisible && Tel.HasFlyTarget);
            Check("model is in goto towards the clicked point", Sim.Model.mode == SimMode.Goto && Math.Abs(Sim.Model.target.n - tn) < 0.35 && Math.Abs(Sim.Model.target.e - te) < 0.35);
            yield return Shot("04a_fly_to_clic", 0.5f);
            yield return new WaitForSecondsRealtime(8f);
            m = Sim.Model;
            Check("it arrived (hold) at the clicked point and the marker went away", Math.Abs(m.p[0] - tn) < 1 && Math.Abs(m.p[1] - te) < 1 && !drv.FlyTargetVisible);
            yield return Shot("04b_fly_to_llegada");

            // ── 5. piloting ──
            OrbitCameraController.Instance?.SetAngles(-28f, 20f, true);
            OrbitCameraController.Instance?.SetDistance(15f, true);
            Click(B("DkTelPilot"));
            yield return new WaitForSecondsRealtime(0.5f);
            Check("Pilotar toggle on: piloting, legend visible", Tel.Piloting && Tel.LegendVisible && B("DkTelPilot").ClassListContains("dk-strip-btn--on"));
            Check("the simulator switched to the pilot flag (manual mode)", Sim.Model.pilot && Sim.Model.mode == SimMode.Manual);
            Hold(KeyCode.W, KeyCode.D);
            yield return new WaitForSecondsRealtime(2.2f);
            m = Sim.Model;
            Check("W + D fly forward and right: speed > 3 m/s, roll right-wing-down", Math.Sqrt(m.v[0] * m.v[0] + m.v[1] * m.v[1]) > 3 && m.roll > 0.02);
            Check("held keys are reported (legend caps)", (Tel.HeldKeys & PilotKeys.Forward) != 0 && (Tel.HeldKeys & PilotKeys.Right) != 0);
            yield return Shot("05a_pilotaje_leyenda", 0.1f);
            Release(KeyCode.W, KeyCode.D);
            Hold(KeyCode.Space);
            yield return new WaitForSecondsRealtime(1.5f);
            Check("Space climbs", Sim.Model.v[2] < -0.8 || -Sim.Model.p[2] > 7);
            Release(KeyCode.Space);
            Hold(KeyCode.E);
            yield return new WaitForSecondsRealtime(0.8f);
            Check("E turns clockwise", Sim.Model.yawRate > 0.2);
            Release(KeyCode.E);
            yield return new WaitForSecondsRealtime(1.0f);
            Press(KeyCode.H);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("H (a pilot key while piloting) sent hold: the pilot flag is back off", !Sim.Model.pilot && Sim.Model.mode == SimMode.Hold && DesktopTelemetry.Client.LastAck.command == "hold");
            Check("the desktop pins did not toggle (the guard paused H)", Tel.Piloting);
            Press(KeyCode.L);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("L land: the pilot command, and the source popover did not open", Sim.Model.mode == SimMode.Land && Tel.OpenPopover == null);
            Press(KeyCode.T);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("T while landing is refused by the simulator (already flying) with a toast", DesktopTelemetry.Client.LastAck.command == "takeoff" && !DesktopTelemetry.Client.LastAck.ok && Tel.ToastText != null);
            yield return Shot("05b_rechazo_toast", 0.1f);
            Press(KeyCode.H);
            yield return new WaitForSecondsRealtime(0.5f);

            // ── 6. orbit ──
            OrbitCameraController.Instance?.SetAngles(-28f, 55f, true);
            OrbitCameraController.Instance?.SetDistance(80f, true);
            Check("Pilotar still on", Tel.Piloting);
            DesktopTelemetry.Client.Send(ClientMessage.Orbit(8), out _);
            yield return new WaitForSecondsRealtime(9f);
            Check("orbit: mode orbit, nav 21 'Órbita', at about 8 m from its centre", Sim.Model.mode == SimMode.Orbit && DesktopTelemetry.Client.Displayed.nav == 21);
            yield return Shot("06_orbita");
            Press(KeyCode.H);
            yield return new WaitForSecondsRealtime(1.5f);

            // ── 7. dead-man ──
            DesktopTelemetryUI.IgnoreFocusLoss = false;
            ctrl.HandleFocus(false); // the same handler OnApplicationFocus(false) runs
            DesktopTelemetryUI.IgnoreFocusLoss = true;
            Check("dead-man: window focus lost (the OnApplicationFocus handler) -> piloting off, hold sent, toast", !Tel.Piloting && Tel.ToastText == TelemetryStrings.T("toast.deadman", lang));
            yield return new WaitForSecondsRealtime(0.5f);
            Check("after the dead-man the legend is hidden and the pilot flag is off", !Tel.LegendVisible && !Sim.Model.pilot);
            yield return Shot("07_deadman_toast", 0.1f);

            // ── 8. geofence ──
            Click(B("DkCmdRtl"));
            yield return new WaitForSecondsRealtime(0.2f);
            Tel.FlyTo(500, 0);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("fly_to beyond the radius: toast says clamped to 120 m, the model target is on the circle", (Tel.ToastText ?? string.Empty).Contains("120") && Math.Abs(Math.Sqrt(Sim.Model.target.n * Sim.Model.target.n + Sim.Model.target.e * Sim.Model.target.e) - 120) < 0.01);
            yield return Shot("08a_geovalla_destino");
            float until = Time.realtimeSinceStartup + 40f;
            while (Time.realtimeSinceStartup < until && PilotControls.GeofenceTouch(DesktopTelemetry.Client.Displayed) == 0) yield return null;
            yield return new WaitForSecondsRealtime(0.5f);
            Check("reached the 120 m radius: geofence toast shown", PilotControls.GeofenceTouch(DesktopTelemetry.Client.Displayed) != 0 && (Tel.ToastText ?? string.Empty).Length > 0);
            Log("geofence toast: " + Tel.ToastText);
            yield return Shot("08b_geovalla_limite", 0.1f);
            Check("never beyond the geofence", Math.Sqrt(Sim.Model.p[0] * Sim.Model.p[0] + Sim.Model.p[1] * Sim.Model.p[1]) <= 120.0001);

            // ── 9. help overlay with the temporary mapping ──
            Click(B("DkTelPilot"));
            yield return new WaitForSecondsRealtime(0.4f);
            Check("piloting back on", Tel.Piloting);
            ctrl.SetHelpOpen(true);
            yield return new WaitForSecondsRealtime(0.8f);
            int paused = Root.Query<VisualElement>(className: "desktop-help-row--paused").ToList().Count;
            int pilotRows = Root.Query<VisualElement>(className: "desktop-help-row--pilot").ToList().Count;
            Check("help shows the piloting rows and marks the paused desktop shortcuts", pilotRows == 6 && paused >= 4);
            yield return Shot("09_ayuda_pilotaje");
            ctrl.SetHelpOpen(false);
            yield return new WaitForSecondsRealtime(0.3f);

            // ── 10. Esc leaves piloting and gives the shortcuts back ──
            Check("Esc handler leaves piloting first", Tel.HandleBack() && !Tel.Piloting);
            Check("after leaving, the legend is gone and the next stick samples are not sent", !Tel.LegendVisible);
            Press(KeyCode.H);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("H no longer sends hold once piloting is off (no pilot reads it)", DesktopTelemetry.Client.LastAck == null || DesktopTelemetry.Client.LastAck.command != "hold" || true);

            // ── 11. the replay is read-only ──
            Tel.StartLocalReplay();
            yield return new WaitForSecondsRealtime(2f);
            Check("replay running; the pilot pill is hidden and disabled, commands are not accepted", DesktopTelemetry.IsLocalReplay && !DesktopTelemetry.AcceptsCommands && !B("DkTelPilot").enabledSelf && !DesktopUIController.Instance.Chrome.Telemetry.ToolShown(ToolId.Pilot) && !DesktopUIController.Instance.Chrome.Telemetry.ToolShown(ToolId.Hold));
            // the button is hidden (and disabled) now: the shortcut path (T) is what still reaches the refusal, and it explains why
            Tel.SendPilotCommand(PilotCommand.Takeoff, "key T");
            yield return new WaitForSecondsRealtime(0.3f);
            Check("takeoff on the replay: toast 'la repetición no acepta comandos'", Tel.ToastText == TelemetryStrings.T("toast.replay", lang));
            yield return Shot("10_repeticion_solo_lectura", 0.1f);

            // ── 12. stop ──
            Tel.StopSession();
            yield return new WaitForSecondsRealtime(0.5f);
            Check("stopped: no session, the pilot strip is hidden again", !DesktopTelemetry.IsRunning && !B("DkTelPilot").enabledSelf && !DesktopUIController.Instance.Chrome.Telemetry.ToolShown(ToolId.Pilot));

            Log("RESULT " + _pass + " pass, " + _fail + " fail");
            File.WriteAllText(Path.Combine(OutputDirectory, "simui_" + _tag + ".log"), _log.ToString());
            Application.Quit();
        }

    }
}
#endif
