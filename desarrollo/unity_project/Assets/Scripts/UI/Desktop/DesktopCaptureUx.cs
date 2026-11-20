#if UNITY_STANDALONE
using System;
using System.Collections;
using System.Collections.Generic;
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
    /// Verification aid for the desktop UX cut (-ux-capture &lt;folder&gt;, plus -lang es|en and the window flags; PLAN_PC 38). In-player probe, no OS mouse or
    /// keyboard. For each source (Inspector, repeticion, simulador, sesion propia, PX4 SITL con una fuente manual) opens the Telemetry mode and compares the
    /// controls that are SHOWN with <see cref="TelemetryMenu"/> (what the table says exists), checks that nothing vanished from the tree (hidden is not removed),
    /// that what is hidden cannot be clicked or focused, that every shown button fits in the window and that the pad still has a valid first focus.
    /// One PNG per source (tag = WxH_lang) and PASS / FAIL lines.
    /// </summary>
    internal sealed partial class DesktopCaptureUx : MonoBehaviour
    {
        public string OutputDirectory;
        private int _fail, _pass, _blackShots;
        private readonly StringBuilder _log = new StringBuilder();
        private string _tag;

        private static readonly Dictionary<ToolId, string> Names = new Dictionary<ToolId, string>
        {
            { ToolId.Pilot, "DkTelPilot" }, { ToolId.Takeoff, "DkCmdTakeoff" }, { ToolId.Land, "DkCmdLand" }, { ToolId.Hold, "DkCmdHold" }, { ToolId.Rtl, "DkCmdRtl" },
            { ToolId.Follow, "DkCamMode" }, { ToolId.Game, "DkGameMode" }, { ToolId.Ground, "DkTelGround" }, { ToolId.Course, "DkTelCourse" }, { ToolId.Fence, "DkTelFence" },
            { ToolId.Wind, "DkTelWind" }, { ToolId.Battery, "DkTelBatteryReact" }, { ToolId.Map, "DkTelMap" }, { ToolId.Preflight, "DkTelPreflight" },
            { ToolId.Mission, "DkTelMission" }, { ToolId.Sessions, "DkTelRecord" }, { ToolId.Train, "DkTelTraining" }, { ToolId.Record, "DkRecToggle" },
        };

        private void Start() { StartCoroutine(Run()); }
        private void Log(string s) { _log.AppendLine(s); Debug.Log("[UxProbe] " + s); }

        private void Check(string name, bool ok)
        {
            if (ok) _pass++; else _fail++;
            Log((ok ? "PASS " : "FAIL ") + name);
        }

        private VisualElement Root => UIManager.Instance.Root;
        private Button B(string n) => Root.Q<Button>(n);
        private DesktopTelemetryUI Tel => DesktopUIController.Instance.Chrome.Telemetry;
        private static string Lang => AppLanguageManager.IsSpanish ? "es" : "en";
        private static bool Shown(VisualElement e) => e != null && e.resolvedStyle.display != DisplayStyle.None;
        private static string F1(double x) => x.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

        private static void Click(Button b)
        {
            if (b == null) return;
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
        }

        /// <summary>Shown = no ancestor up to the Telemetry container has display none and the button itself has none.</summary>
        private static bool Visible(VisualElement e)
        {
            for (VisualElement p = e; p != null; p = p.parent)
            {
                if (p.resolvedStyle.display == DisplayStyle.None) return false;
                if (p.name == "DkTelemetry") break;
            }
            return true;
        }

        private IEnumerator Shot(string name, float wait = 0.4f)
        {
            if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
            Texture2D use = null;
            yield return DesktopScreenGrab.Grab(t => use = t);
            float luma = DesktopScreenGrab.LastLuma;
            if (luma < 3f) { _blackShots++; Log("BLACK " + _tag + "_" + name); }
            File.WriteAllBytes(Path.Combine(OutputDirectory, _tag + "_" + name + ".png"), ImageConversion.EncodeToPNG(use));
            Log("shot " + _tag + "_" + name + " " + Screen.width + "x" + Screen.height + " luma " + F1(luma));
            Destroy(use);
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(OutputDirectory);
            PlayerPrefs.SetInt("Onboarding_Seen", 1);
            PlayerPrefs.DeleteKey(DesktopTelemetryUI.PrefSource);
            PlayerPrefs.DeleteKey(DesktopTelemetryUI.PrefGround);
            DesktopTelemetryUI.IgnoreFocusLoss = true;
            yield return new WaitForSecondsRealtime(7f);
            AccessibilityManager.Instance?.SetUIScale(1f);
            yield return new WaitForSecondsRealtime(0.5f);
            _tag = Screen.width + "x" + Screen.height + "_" + Lang;
            UIManager ui = UIManager.Instance;
            ui.PrepareViewerForDeepLink();
            yield return new WaitForSecondsRealtime(3f);
            if (ui.IsOnboardingOpen) { UIManager.Instance.ShortcutBack(); yield return new WaitForSecondsRealtime(1f); }
            Log("window " + Screen.width + "x" + Screen.height + " lang " + Lang);

            Tel.OpenMode();
            yield return new WaitForSecondsRealtime(0.8f);

            // every control still exists in the tree on every source: hidden, not removed
            foreach (var kv in Names) Check("the control " + kv.Value + " is in the tree (hidden, not removed)", B(kv.Value) != null);

            // 1. Inspector
            yield return CheckSurface("inspector", MenuSurface.Inspector);

            // 2. repeticion (vuelo de muestra)
            Tel.StartLocalReplay();
            yield return new WaitForSecondsRealtime(2f);
            yield return CheckSurface("repeticion", MenuSurface.Replay);

            // 3. simulador (y de paso graba una sesion propia de unos segundos)
            Tel.StartSimulator();
            yield return new WaitForSecondsRealtime(2.5f);
            yield return CheckSurface("simulador", MenuSurface.Sim);
            string path = null;
            if (Tel.CanRecord)
            {
                Tel.StartRecording();
                yield return new WaitForSecondsRealtime(3.5f);
                path = Tel.StopRecording();
            }
            Check("a short session was recorded for the own-session surface", !string.IsNullOrEmpty(path) && File.Exists(path));

            // 4. sesion propia
            if (!string.IsNullOrEmpty(path)) Check("the recorded session opens as an own replay", Tel.OpenSessionPath(path));
            yield return new WaitForSecondsRealtime(2f);
            yield return CheckSurface("sesion_propia", MenuSurface.Own);

            // 5. PX4 SITL (a manual source that announces the commands)
            DesktopTelemetry.Stop();
            yield return new WaitForSecondsRealtime(0.5f);
            var src = new ManualTelemetrySource(SourceClassification.Sitl, false);
            DesktopTelemetry.Start(src);
            HelloMessage hello = new SimTelemetrySource().BuildHello(ReplaySchedule.WallClockMs());
            hello.commands = new[] { "takeoff", "set_altitude", "land", "fly_to", "yaw", "orbit", "rtl", "hold", "motor_off", "restore", "battery_off", "pilot" };
            src.Push(hello);
            var flying = FlightSim.Create();
            for (int i = 0; i < 8; i++) { src.Push(new FramesMessage { frames = new[] { FlightSim.Frame(flying, ReplaySchedule.WallClockMs()) } }); yield return new WaitForSecondsRealtime(0.1f); }
            yield return new WaitForSecondsRealtime(1f);
            yield return CheckSurface("px4_sitl", MenuSurface.Sitl);

            // a loop of source changes keeps the table in force (no stale visibility)
            for (int k = 0; k < 3; k++)
            {
                DesktopTelemetry.Stop();
                yield return new WaitForSecondsRealtime(0.4f);
                Check("loop " + k + ": Inspector again, only the ground and the sessions hub", ShownIds().SetEquals(TelemetryMenu.StripToolsFor(MenuSurface.Inspector)));
                Tel.StartSimulator();
                yield return new WaitForSecondsRealtime(1.2f);
                Check("loop " + k + ": simulator again, every control", ShownIds().SetEquals(TelemetryMenu.StripToolsFor(MenuSurface.Sim)));
            }
            DesktopTelemetry.Stop();
            yield return new WaitForSecondsRealtime(0.4f);

            yield return InspectAndPowerChecks();
            yield return InfoAndFoldChecks();
            yield return HonestyAndAdvancedChecks();

            Log("black captures: " + _blackShots);
            Check("no capture came out black", _blackShots == 0);
            Log("RESULT " + _pass + " pass, " + _fail + " fail");
            File.WriteAllText(Path.Combine(OutputDirectory, "ux_" + _tag + ".log"), _log.ToString());
            Application.Quit();
        }

        /// <summary>Paso 3 (PLAN_PC 38): INSPECT = Pins, Isolate, Focus; the simulated power (switch + load slider) lives in Studio > Render; the power chip keeps working.</summary>
        private IEnumerator InspectAndPowerChecks()
        {
            Log("── paso 3: Inspeccionar y Energia ──");
            UIManager ui = UIManager.Instance;
            Click(B("DkMode1"));
            yield return new WaitForSecondsRealtime(0.8f);
            VisualElement grid = Root.Q<VisualElement>("ToolsCardGrid");
            var cards = new List<string>();
            foreach (Button b in grid.Query<Button>().ToList()) if (b.resolvedStyle.display != DisplayStyle.None && b.name != "SubmenuBackBtn") cards.Add(b.name);
            Log("INSPECT cards: " + string.Join(", ", cards));
            Check("INSPECT has exactly three cards: Pins, Isolate, Focus", cards.Count == 3 && cards.Contains("ToolHotspotBtn") && cards.Contains("ToolIsolateBtn") && cards.Contains("ToolFrameBtn"));
            Check("the power card is no longer in INSPECT", B("ToolPowerBtn") != null && !grid.Contains(B("ToolPowerBtn")) && !grid.Contains(Root.Q<VisualElement>("ToolPowerLoadPanel")));
            Check("the focus card says «Enfocar» / «Focus» and has a tooltip", !string.IsNullOrEmpty(B("ToolFrameBtn").Q<Label>(className: "submenu-label").text) && !string.IsNullOrEmpty(B("ToolFrameBtn").tooltip));
            yield return Shot("ux_inspeccionar_tres_tarjetas", 0.2f);

            // pins card: toggles
            bool pinsBefore = B("ToolHotspotBtn").ClassListContains("submenu-card--active");
            Click(B("ToolHotspotBtn"));
            yield return new WaitForSecondsRealtime(0.8f);
            Check("the Pins card still toggles the pins", B("ToolHotspotBtn").ClassListContains("submenu-card--active") != pinsBefore);
            Click(B("ToolHotspotBtn"));
            yield return new WaitForSecondsRealtime(0.8f);

            // focus card: with the camera moved, it brings it back (no selection: reset the view)
            Camera cam = Camera.main;
            OrbitCameraController.Instance?.SetAngles(-5f, 120f, true);
            yield return new WaitForSecondsRealtime(1.2f);
            Vector3 moved = cam.transform.position;
            Click(B("ToolFrameBtn"));
            yield return new WaitForSecondsRealtime(2.0f);
            Check("the Focus card moves the camera back to the framed view", (cam.transform.position - moved).magnitude > 0.05f);

            // Studio > Render: the power card and its load panel
            Click(B("DkMode3"));
            yield return new WaitForSecondsRealtime(0.9f);
            Button power = B("ToolPowerBtn");
            VisualElement panel = Root.Q<VisualElement>("ToolPowerLoadPanel");
            Check("the power card is inside the Studio panel, after the render modes", power != null && Root.Q<VisualElement>("StudioPanel").Contains(power) && Root.Q<VisualElement>("DkPowerBlock") != null);
            Check("the render modes (with the thermal view) are in the same panel", Root.Q<VisualElement>("StudioPanel").Contains(B("ShaderMode_Thermal")) && Visible2(B("ShaderMode_Thermal")));
            Check("the power card is shown in Render", Visible2(power));
            Check("the load panel is hidden while the power is off", panel != null && panel.style.display == DisplayStyle.None && !(DroneStateController.Instance?.IsOn ?? true));
            yield return Shot("ux_estudio_energia_apagada", 0.2f);
            Click(power);
            yield return new WaitForSecondsRealtime(3.0f);
            Check("the switch turns the simulated power on", DroneStateController.Instance != null && DroneStateController.Instance.IsOn);
            Check("the load panel shows while the power is on", panel.style.display != DisplayStyle.None && panel.enabledSelf);
            Check("the power chip of the top bar appears", Visible2(Root.Q<Button>("DkChipPower")));
            Slider slider = Root.Q<Slider>("ToolPowerLoadSlider");
            slider.value = 0.6f;
            yield return new WaitForSecondsRealtime(0.8f);
            Check("the load slider sets the load command (0.6)", Mathf.Abs(DroneStateController.Instance.LoadCommandNormalized - 0.6f) < 0.02f);
            Check("the load value reads 60%", Root.Q<Label>("ToolPowerLoadValue").text == "60%");
            Check("the state label says the simulated high load", Root.Q<Label>("ToolPowerStateLabel").text.ToUpperInvariant().Contains(AppLanguageManager.IsSpanish ? "SIMUL" : "SIMULATED"));
            yield return Shot("ux_estudio_energia_encendida", 0.3f);

            // Environment section hides the power block
            Click(B("DkStudioEnv"));
            yield return new WaitForSecondsRealtime(0.8f);
            Check("the Environment section does not show the power block", !Visible2(Root.Q<VisualElement>("DkPowerBlock")));
            Click(B("DkStudioRender"));
            yield return new WaitForSecondsRealtime(0.8f);
            Check("back in Render the power block shows again", Visible2(Root.Q<VisualElement>("DkPowerBlock")));

            // the chip still switches it off
            Click(Root.Q<Button>("DkChipPower"));
            yield return new WaitForSecondsRealtime(2.0f);
            Check("the power chip still switches the simulated power off", DroneStateController.Instance != null && !DroneStateController.Instance.IsOn);
            Check("the load panel hides again", panel.style.display == DisplayStyle.None);
            Click(B("DkMode3"));
            yield return new WaitForSecondsRealtime(0.6f);
        }

        private static bool Visible2(VisualElement e) => e != null && e.resolvedStyle.display != DisplayStyle.None && e.resolvedStyle.opacity > 0.01f;

        private HashSet<ToolId> ShownIds()
        {
            var set = new HashSet<ToolId>();
            foreach (var kv in Names)
            {
                if (kv.Key == ToolId.Record) continue;
                Button b = B(kv.Value);
                if (b != null && Visible(b)) set.Add(kv.Key);
            }
            return set;
        }

        private IEnumerator CheckSurface(string label, MenuSurface expected)
        {
            Log("── " + label + " ──");
            Check(label + ": the app reads the source as " + expected, Tel.CurrentSurface == expected);
            var want = new HashSet<ToolId>(TelemetryMenu.StripToolsFor(expected));
            HashSet<ToolId> got = ShownIds();
            Check(label + ": the shown buttons are exactly the table's (" + got.Count + " of " + want.Count + ")", got.SetEquals(want));
            foreach (var id in TelemetryMenu.ToolIds)
            {
                if (id == ToolId.Record) continue;
                Button b = B(Names[id]);
                bool should = want.Contains(id);
                if (!should && b != null) Check(label + ": " + Names[id] + " is hidden and cannot take the focus or a click", !Visible(b) && !b.enabledSelf);
                if (should && b != null) Check(label + ": " + Names[id] + " is shown", Visible(b));
            }
            Check(label + ": the Telemetry mode is open", Tel.ModeOpen);

            // the record switch lives in the popover of «Grabar»
            Tel.TogglePopover("record");
            yield return new WaitForSecondsRealtime(0.5f);
            Button rec = B("DkRecToggle");
            Check(label + ": the record switch " + (TelemetryMenu.Has(ToolId.Record, expected) ? "is shown" : "is hidden"), rec != null && Visible(rec) == TelemetryMenu.Has(ToolId.Record, expected));
            Check(label + ": «Mis sesiones» is reachable from «Grabar»", Visible(B("DkTelRecord")) && Tel.OpenPopover == "record");
            Tel.CloseAllPopovers();
            yield return new WaitForSecondsRealtime(0.2f);

            // the pad: there is always a first focus inside the Telemetry strip
            Button first = DesktopUIController.Instance.Pad?.PickFirstFocus();
            Check(label + ": the pad has a valid first focus (" + (first != null ? first.name : "none") + ")", first != null && first.enabledSelf && first.resolvedStyle.display != DisplayStyle.None);
            yield return Shot("ux_" + label, 0.3f);
        }
    }
}
#endif
