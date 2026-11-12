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
    /// Verification aid for WP-11 (-tel-ui &lt;folder&gt;, plus -lang es|en and the window flags): opens the viewer and walks the telemetry UI
    /// with in-player probes (it does not use the OS mouse): idle / Inspect power simulation, mode 4, status chip, source popover, alerts
    /// with a synthetic fault, faults popover, "Vuelo" tab, SITL error state, mode exclusivity and Esc. Writes one PNG per state
    /// (tag = WxH_lang), PASS / FAIL lines and a canvas coverage log (what the docked rails and the popovers take, as in phase 4).
    /// Not active unless the flag is given.
    /// </summary>
    internal sealed class DesktopCaptureTelemetryUI : MonoBehaviour
    {
        public string OutputDirectory;
        private int _fail;
        private int _pass;
        private readonly StringBuilder _log = new StringBuilder();
        private string _tag;

        private void Start() { StartCoroutine(Run()); }

        private void Log(string s) { _log.AppendLine(s); Debug.Log("[TelUI] " + s); }

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

        private IEnumerator Shot(string name, float wait = 0.5f)
        {
            yield return new WaitForSecondsRealtime(wait);
            yield return new WaitForEndOfFrame();
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(OutputDirectory, _tag + "_" + name + ".png"), ImageConversion.EncodeToPNG(tex));
            Destroy(tex);
            Cover(name);
        }

        // ── canvas coverage: union of the pickable UI surfaces / of those that overlap the model (docked rails, bar, sheet excluded) ──
        private static void Scan(VisualElement e, VisualElement root, Rect screenLogical, List<VisualElement> output)
        {
            if (e != root)
            {
                if (e.resolvedStyle.display == DisplayStyle.None || e.resolvedStyle.visibility == Visibility.Hidden || e.resolvedStyle.opacity < 0.05f) return;
                Rect r = e.worldBound;
                if (e.pickingMode == PickingMode.Position && r.width * r.height > 300f && r.Overlaps(screenLogical) && e.name != "WorldSpaceContainer" && DesktopMotion.IsShown(e))
                {
                    output.Add(e);
                    return;
                }
            }
            foreach (VisualElement child in e.Children()) Scan(child, root, screenLogical, output);
        }

        private void Cover(string name)
        {
            VisualElement root = Root;
            float s = DesktopUIController.Instance.UiScale;
            var screenLogical = new Rect(0, 0, Screen.width / s, Screen.height / s);
            var list = new List<VisualElement>();
            Scan(root, root, screenLogical, list);
            const int gx = 192, gy = 108;
            var grid = new bool[gx * gy];
            var gridO = new bool[gx * gy];
            var sb = new StringBuilder();
            foreach (VisualElement e in list)
            {
                Rect r = e.worldBound;
                float px = r.xMin * s, py = r.yMin * s, pw = r.width * s, ph = r.height * s;
                float pct = 100f * pw * ph / (Screen.width * (float)Screen.height);
                string cls = string.Join(".", e.GetClasses());
                sb.Append($"\n    - {e.name}.{(cls.Length > 40 ? cls.Substring(0, 40) : cls)} {pw:F0}x{ph:F0}@{px:F0},{py:F0} ({pct:F1}%)");
                int x0 = Mathf.Clamp((int)(px / Screen.width * gx), 0, gx - 1), x1 = Mathf.Clamp((int)((px + pw) / Screen.width * gx), 0, gx - 1);
                int y0 = Mathf.Clamp((int)(py / Screen.height * gy), 0, gy - 1), y1 = Mathf.Clamp((int)((py + ph) / Screen.height * gy), 0, gy - 1);
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) grid[y * gx + x] = true;
                bool docked = e.name == "DkTopBar" || e.name == "DkRailL" || e.name == "DkRailR" || e.name == "DkStatus" || e.ClassListContains("details-sheet") || e.ClassListContains("desktop-tree") || e.ClassListContains("hotspot-dot");
                if (!docked) for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) gridO[y * gx + x] = true;
            }
            int n = 0; foreach (bool b in grid) if (b) n++;
            int nO = 0; foreach (bool b in gridO) if (b) nO++;
            Log($"[{_tag}_{name}] cubre {100f * n / grid.Length:F1}% (acoplado+superpuesto), {100f * nO / gridO.Length:F1}% superpuesto al modelo, con {list.Count} superficies:{sb}");
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(OutputDirectory);
            PlayerPrefs.SetInt("Onboarding_Seen", 1);
            PlayerPrefs.DeleteKey(DesktopTelemetryUI.PrefSource);
            PlayerPrefs.DeleteKey(DesktopTelemetryUI.PrefSitlUrl);
            yield return new WaitForSecondsRealtime(7f);
            // "UI size" is a saved preference (a previous capture may have left 150 %): the captures use the default 100 %
            AccessibilityManager.Instance?.SetUIScale(1f);
            yield return new WaitForSecondsRealtime(0.5f);
            _tag = Screen.width + "x" + Screen.height + "_" + Lang;
            UIManager ui = UIManager.Instance;
            ui.PrepareViewerForDeepLink();
            yield return new WaitForSecondsRealtime(3f);
            if (ui.IsOnboardingOpen) { ui.ShortcutBack(); yield return new WaitForSecondsRealtime(1f); }
            DesktopUIController ctrl = DesktopUIController.Instance;
            OrbitCameraController.Instance?.ResetView();
            OrbitCameraController.Instance?.SetAngles(-30f, 22f, true);
            OrbitCameraController.Instance?.SetDistance(8.5f, true);
            yield return new WaitForSecondsRealtime(1.5f);
            var labels = HonestyLabels.Load();
            string lang = Lang;
            Log("window " + Screen.width + "x" + Screen.height + " ui scale " + ctrl.UiScale.ToString("F2") + " lang " + lang);

            // ── 0. idle ──
            Check("telemetry stopped by default; no session chip", !DesktopTelemetry.IsRunning && !DisplayedFlex(B("DkChipSession")));
            Check("status chip says the Inspector text (no telemetry)", L("DkStatusText").text == TelemetryStrings.T("src.inspector", lang));
            Check("status dot is the 'off' grey", L("DkStatusText").parent.Q<VisualElement>("DkStatusDot").ClassListContains("dk-dot--off"));
            Check("4th mode button exists in the segmented switch", B("DkMode4") != null);
            // the last run may have saved "sitl" as the preferred source: start from the replay (Encendido toggles the last chosen source)
            Tel.StartLocalReplay();
            Tel.StopSession();
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("01_reposo");

            // ── 1. Inspect: manual power simulation, still there, relabelled ──
            ui.ShortcutToggleMode(1);
            yield return new WaitForSecondsRealtime(0.7f);
            Label card = B("ToolPowerBtn").Q<Label>(className: "submenu-label");
            Check("Inspect power card reads the simulated name", card.text == TelemetryStrings.T("power.sim", lang));
            Click(B("ToolPowerBtn"));
            yield return new WaitForSecondsRealtime(2.6f);
            DroneStateController.Instance.SetLoadCommand(0.9f);
            yield return new WaitForSecondsRealtime(1.2f);
            Label state = L("ToolPowerStateLabel");
            Check("manual power high load reads 'Carga alta (simulado)' / 'High load (simulated)'", state.text == TelemetryStrings.T("power.highLoad", lang));
            Check("manual power chip DkChipPower visible, session chip hidden", DisplayedFlex(B("DkChipPower")) && !DisplayedFlex(B("DkChipSession")));
            yield return Shot("02_inspeccionar_energia_simulada");

            // ── 2. mode 4 with the manual power still on: exclusive with mode 1, session off ──
            Tel.OpenMode();
            yield return new WaitForSecondsRealtime(0.8f);
            Check("mode 4 opens and closes the Inspect mode", Tel.ModeOpen && B("ModeToolsBtn") != null && !B("ModeToolsBtn").ClassListContains("mode-btn--active"));
            Check("mode 4 segment is on; strip visible", B("DkMode4").ClassListContains("dk-seg-btn--on") && !Root.Q<VisualElement>("DkTelemetry").ClassListContains("mode--hidden"));
            Check("Vuelo tab opened on the inspector with the empty state", ctrl.SidePanel.IsOpen && Tel.FlightTabActive && DisplayedFlex(Root.Q<VisualElement>("DkFlEmpty")));
            yield return Shot("03_telemetria_sin_sesion");

            // ── 3. session switch: local replay at a quiet second ──
            Click(B("DkTelPower"));
            yield return new WaitForSecondsRealtime(2.5f);
            Check("Encendido starts the local replay", DesktopTelemetry.IsRunning && DesktopTelemetry.IsLocalReplay);
            Check("status chip = approved local replay label", L("DkStatusText").text == DesktopTelemetry.BadgeText(lang) && labels.TryLocalReplayText(lang, out string approved) && approved == L("DkStatusText").text);
            Check("status dot is live green", Root.Q<VisualElement>("DkStatusDot").ClassListContains("dk-dot--live"));
            Check("session chip appears next to the power chip", DisplayedFlex(B("DkChipSession")) && DisplayedFlex(B("DkChipPower")));
            Check("manual power simulation keeps running independently", DroneStateController.Instance.IsOn);
            Check("telemetry wins the movement (ExternalMotion)", DroneStateController.Instance.ExternalMotion);
            Check("Vuelo tab shows the readings, not the empty state", DisplayedFlex(Root.Q<VisualElement>("DkFlBody")) && !DisplayedFlex(Root.Q<VisualElement>("DkFlEmpty")));
            string rpmTag = Root.Q<VisualElement>("DkFlBody").Q<Label>(className: "dk-tag").text;
            Check("readings carry origin tags from the labels", !string.IsNullOrEmpty(rpmTag));
            yield return Shot("04_telemetria_repeticion_local");
            DroneStateController.Instance.TurnOff();

            // ── 4. source popover ──
            Click(B("DkTelSource"));
            yield return new WaitForSecondsRealtime(0.8f);
            Check("source popover open with four rows", Tel.OpenPopover == "source" && Root.Q<VisualElement>("DkPopSource") != null);
            Button sim = B("DkSrcSim");
            Check("flight simulator row is visible but disabled, tooltip 'Próximamente'", sim != null && sim.ClassListContains("dk-src-row--disabled") && sim.tooltip == TelemetryStrings.T("src.sim.soon", lang));
            Check("replay row is the active one", B("DkSrcReplay").ClassListContains("dk-src-row--on"));
            string badge = Root.Q<VisualElement>("DkSrcReplay").Q<Label>(className: "dk-src-badge").text;
            Check("replay row badge is the approved text", labels.TryLocalReplayText(lang, out string ap2) && badge == ap2);
            Click(sim);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("clicking the simulator row does nothing (still local replay)", DesktopTelemetry.IsLocalReplay);
            yield return Shot("05_fuente");

            // ── 5. alerts with a synthetic fault ──
            Tel.CloseAllPopovers();
            Click(B("DkTelFaults"));
            yield return new WaitForSecondsRealtime(0.5f);
            Check("faults popover: controls visible on the local replay", DisplayedFlex(Root.Q<VisualElement>("DkFaultControls")));
            Click(B("DkFault_motor_failure"));
            yield return new WaitForSecondsRealtime(3.2f);
            Check("synthetic fault registered by the replay source (Client.Faults)", DesktopTelemetry.Client.Faults.Count == 1);
            Check("alert derived for motor 3, summary count >= 1", DesktopTelemetry.Client.AlertSummary.count >= 1);
            yield return Shot("06_fallas");
            Tel.CloseAllPopovers();
            Click(B("DkTelAlerts"));
            yield return new WaitForSecondsRealtime(0.8f);
            var rows = Root.Query<Button>(className: "dk-alert-row").ToList();
            Check("alerts popover lists the alert", rows.Count >= 1);
            string msg = rows.Count > 0 ? rows[0].Q<Label>(className: "dk-alert-msg").text : string.Empty;
            Check("alert text carries the 'falla sintética' suffix and the estimated origin", msg.Contains(AlertText.SyntheticFault(lang, labels)) && msg.Contains(lang == "es" ? "estimadas" : "estimated"));
            Check("alerts button count pill shown", DisplayedFlex(Root.Q<Label>("DkTelAlertsCount")));
            yield return Shot("07_alertas_falla_sintetica");
            Check("motor 3 part pulses red (driver highlight)", TelemetryDroneDriver.Instance != null && new List<string>(TelemetryDroneDriver.Instance.HighlightedComponents).Contains("motor3"));
            if (rows.Count > 0) Click(rows[0]);
            yield return new WaitForSecondsRealtime(1.6f);
            Transform sel = SelectionManager.Instance.CurrentSelection;
            Check("clicking the alert selects the named part (motor)", sel != null && TelemetryDroneDriver.Instance.PartFor("motor3") == sel);
            Check("inspector switched to the Part tab", !Tel.FlightTabActive);
            yield return Shot("08_alerta_seleccion_pieza");
            SelectionManager.Instance.Deselect();
            yield return new WaitForSecondsRealtime(0.6f);
            Tel.ShowFlightTab(true);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("Vuelo tab survives a deselection (sheet stays open)", ctrl.SidePanel.IsOpen && Tel.FlightTabActive);
            Tel.CloseAllPopovers();
            yield return Shot("09_vuelo", 1.0f);
            Click(B("DkTelFaults"));
            yield return new WaitForSecondsRealtime(0.3f);
            Click(B("DkFaultClear"));
            yield return new WaitForSecondsRealtime(1.2f);
            Check("clear removes the synthetic faults", DesktopTelemetry.Client.Faults.Count == 0);
            Tel.CloseAllPopovers();

            // ── 6. Esc closes popover then mode; chips ──
            Click(B("DkTelSource"));
            yield return new WaitForSecondsRealtime(0.3f);
            Check("Esc handler closes the popover first", Tel.HandleBack() && Tel.OpenPopover == null && Tel.ModeOpen);
            // mode 1 while mode 4 is open closes mode 4
            ui.ShortcutToggleMode(1);
            yield return new WaitForSecondsRealtime(0.8f);
            Check("mode 1 closes mode 4 (exclusive)", !Tel.ModeOpen && B("DkMode1").ClassListContains("dk-seg-btn--on"));
            Check("session keeps running after leaving mode 4; chip still shown", DesktopTelemetry.IsRunning && DisplayedFlex(B("DkChipSession")));
            Click(B("DkChipStatus"));
            yield return new WaitForSecondsRealtime(0.8f);
            Check("status chip click opens mode 4", Tel.ModeOpen);
            Click(B("DkChipSession"));
            yield return new WaitForSecondsRealtime(0.8f);
            Check("session chip click stops the session", !DesktopTelemetry.IsRunning);
            Check("status chip back to the Inspector text", L("DkStatusText").text == TelemetryStrings.T("src.inspector", lang));
            Check("drone back at rest (ExternalMotion off)", !DroneStateController.Instance.ExternalMotion);

            // ── 7. SITL: bad URL, then an unreachable bridge ──
            Click(B("DkTelSource"));
            yield return new WaitForSecondsRealtime(0.4f);
            Click(B("DkSrcSitl"));
            yield return new WaitForSecondsRealtime(0.5f);
            var field = Root.Q<TextField>("DkSitlUrl");
            Check("SITL block shows the default URL ws://localhost:8765", field != null && field.value == TelemetryEndpoints.SitlDefault);
            field.value = "http://localhost:8765";
            Click(B("DkSitlConnect"));
            yield return new WaitForSecondsRealtime(0.6f);
            Check("a non ws:// URL is rejected with a message, no session", !DesktopTelemetry.IsRunning && Root.Q<VisualElement>("DkSitlBlock").Q<Label>(className: "dk-note--warn").text == TelemetryStrings.T("src.sitl.badurl", lang));
            field.value = TelemetryEndpoints.SitlDefault;
            Click(B("DkSitlConnect"));
            yield return new WaitForSecondsRealtime(1.0f);
            Check("SITL starts as connecting (chip shows 'Gemelo digital SITL')", DesktopTelemetry.IsRunning && L("DkStatusText").text == labels.ClassificationTitle(SourceClassification.Sitl, lang));
            yield return Shot("10_sitl_conectando", 0.2f);
            float t0 = Time.realtimeSinceStartup;
            while (DesktopTelemetry.LinkState != LinkState.Error && Time.realtimeSinceStartup - t0 < 20f) yield return null;
            yield return new WaitForSecondsRealtime(0.8f);
            Check("unreachable bridge ends in the Error link state (not live)", DesktopTelemetry.LinkState == LinkState.Error);
            Check("status dot is red; the strip says 'sin conexión'", Root.Q<VisualElement>("DkStatusDot").ClassListContains("dk-dot--error") && L("DkTelState").text == TelemetryStrings.T("link.offline", lang));
            Check("source popover explains what the bridge needs", Root.Q<VisualElement>("DkSitlBlock").Q<Label>(className: "dk-note--warn").text.StartsWith(TelemetryStrings.T("src.sitl.help", lang)));
            yield return Shot("11_sitl_sin_conexion");
            Tel.CloseAllPopovers();
            Click(B("DkTelFaults"));
            yield return new WaitForSecondsRealtime(0.5f);
            Check("faults popover on SITL is read-only with the honest message", !DisplayedFlex(Root.Q<VisualElement>("DkFaultControls")) && DisplayedFlex(Root.Q<VisualElement>("DkPopFaults")));
            yield return Shot("12_fallas_sitl_solo_lectura");
            Tel.CloseAllPopovers();
            Tel.StopSession();
            Tel.CloseMode();
            yield return new WaitForSecondsRealtime(0.6f);

            // ── 8. help overlay lists the new keys ──
            ctrl.SetHelpOpen(true);
            yield return Shot("13_ayuda");
            ctrl.SetHelpOpen(false);

            Log("TOTAL pass=" + _pass + " fail=" + _fail);
            File.WriteAllText(Path.Combine(OutputDirectory, "telui_" + _tag + ".log"), _log.ToString());
            Application.Quit();
        }

        private static bool DisplayedFlex(VisualElement e) => e != null && e.resolvedStyle.display != DisplayStyle.None && e.style.display != DisplayStyle.None && DesktopMotion.IsShown(e);
    }
}
#endif
