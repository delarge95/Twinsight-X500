#if UNITY_STANDALONE
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    /// Verification aid for Lote 1b (-lote1b-capture &lt;folder&gt;, plus -lang es|en and the window flags). In-player probes, no OS mouse or keyboard (the M key goes through the
    /// <see cref="DesktopKeyboardGate"/> seams; clicks are UI Toolkit events). Walks the three tools:
    ///  - map (tool 1): the dimmed button without telemetry, the M key, the panel, the trail of a flight, the geofence ring, the course, the wind, north / heading up, 220 / 360 px,
    ///    «Encajar» / «Seguir», the honest captions (takeoff point + simulated in the simulator; local origin + measured in the replay);
    ///  - pre-flight (tool 3): the verdict dot, the popover rows with their origins, the boxes the user ticks, the take-off warning with a failing check («Cancelar» /
    ///    «Despegar igualmente»), the replay (a note, no boxes, never asks because it takes no commands);
    ///  - battery (tool 8): the estimated rows of the «Vuelo» tab (resistance, sag, rest voltage, time left), the simulator-only row, «Estimado sobre datos simulados.», the
    ///    voltage-against-current chart; then the same over the replay of the real flight.
    /// One PNG per state (tag = WxH_lang) and PASS / FAIL lines; a black capture fails the run. Not active unless the flag is given.
    /// </summary>
    internal sealed class DesktopCaptureLote1B : MonoBehaviour
    {
        public string OutputDirectory;
        private int _fail, _pass, _blackShots;
        private readonly StringBuilder _log = new StringBuilder();
        private string _tag;

        private void Start() { StartCoroutine(Run()); }
        private void Log(string s) { _log.AppendLine(s); Debug.Log("[Lote1B] " + s); }

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
        private DesktopTelemetryUI Tel => DesktopUIController.Instance.Chrome.Telemetry;
        private static string Lang => AppLanguageManager.IsSpanish ? "es" : "en";
        private static string T(string k) => TelemetryStrings.T(k, Lang);
        private SimTelemetrySource Sim => DesktopTelemetry.Source as SimTelemetrySource;
        private static bool Shown(VisualElement e) => e != null && e.resolvedStyle.display != DisplayStyle.None;
        private static OrbitCameraController Cam => OrbitCameraController.Instance;
        private static TelemetryFrame Frame => DesktopTelemetry.Client?.Displayed;
        private double Alt => Frame != null ? -Frame.p[2] : 0;
        private static string F1(double x) => x.ToString("F1", CultureInfo.InvariantCulture);
        private static string F2(double x) => x.ToString("F2", CultureInfo.InvariantCulture);
        private static double Hyp(double a, double b) => Math.Sqrt(a * a + b * b);

        private readonly HashSet<KeyCode> _held = new HashSet<KeyCode>();
        private readonly Dictionary<KeyCode, int> _pressedAt = new Dictionary<KeyCode, int>();

        private IEnumerator PressKey(KeyCode k)
        {
            _pressedAt[k] = Time.frameCount + 1;
            _held.Add(k);
            yield return null; yield return null; yield return null;
            _held.Remove(k);
            yield return null;
        }

        private IEnumerator Shot(string name, float wait = 0.4f)
        {
            if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
            Texture2D use = null;
            yield return DesktopScreenGrab.Grab(t => use = t);
            float luma = DesktopScreenGrab.LastLuma;
            if (luma < 3f) { _blackShots++; Log("BLACK " + _tag + "_" + name + " (" + DesktopScreenGrab.LastMethod + ", luma " + F1(luma) + ")"); }
            File.WriteAllBytes(Path.Combine(OutputDirectory, _tag + "_" + name + ".png"), ImageConversion.EncodeToPNG(use));
            Log("shot " + _tag + "_" + name + " " + Screen.width + "x" + Screen.height + " via " + DesktopScreenGrab.LastMethod + " luma " + F1(luma));
            Destroy(use);
        }

        private IEnumerator WaitAlt(double target, float timeout)
        {
            float until = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < until && Math.Abs(Alt - target) > 0.25) yield return null;
        }

        private IEnumerator FlyTo(double n, double e, double alt, float timeout)
        {
            DesktopTelemetry.Client.Send(ClientMessage.FlyTo(n, e, alt), out _);
            float until = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < until && Hyp(Frame.p[0] - n, Frame.p[1] - e) > 1.0) yield return null;
        }

        private IEnumerator Landed()
        {
            float until = Time.realtimeSinceStartup + 40f;
            while (Time.realtimeSinceStartup < until && !Frame.landed) yield return null;
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(OutputDirectory);
            PlayerPrefs.SetInt("Onboarding_Seen", 1);
            PlayerPrefs.DeleteKey(DesktopTelemetryUI.PrefSource);
            PlayerPrefs.DeleteKey(DesktopTelemetryUI.PrefGround);
            PlayerPrefs.DeleteKey(SimSettings.PrefFence);
            PlayerPrefs.DeleteKey(SimSettings.PrefWind);
            PlayerPrefs.DeleteKey(DesktopTelemetryUI.PrefMapOn);
            PlayerPrefs.DeleteKey(DesktopTelemetryUI.PrefMapBig);
            PlayerPrefs.DeleteKey(DesktopTelemetryUI.PrefMapHeadingUp);
            DesktopTelemetryUI.IgnoreFocusLoss = true;
            DesktopKeyboardGate.KeyHeld = k => _held.Contains(k);
            DesktopKeyboardGate.KeyPressed = k => _pressedAt.TryGetValue(k, out int f) && f == Time.frameCount;
            yield return new WaitForSecondsRealtime(7f);
            AccessibilityManager.Instance?.SetUIScale(1f);
            yield return new WaitForSecondsRealtime(0.5f);
            _tag = Screen.width + "x" + Screen.height + "_" + Lang;
            UIManager ui = UIManager.Instance;
            ui.PrepareViewerForDeepLink();
            yield return new WaitForSecondsRealtime(3f);
            if (ui.IsOnboardingOpen) { ui.ShortcutBack(); yield return new WaitForSecondsRealtime(1f); }
            Cam?.ResetView();
            yield return new WaitForSecondsRealtime(1.0f);
            Log("window " + Screen.width + "x" + Screen.height + " fullscreen=" + Screen.fullScreen + " lang " + Lang + " tier " + RenderQualityController.ResolvedTier);

            var tel = Tel;
            tel.OpenMode();
            yield return new WaitForSecondsRealtime(0.6f);
            Check("without telemetry «Mapa» and «Comprobar» are dimmed with their reason", B("DkTelMap") != null && B("DkTelPreflight") != null && !B("DkTelMap").enabledSelf && !B("DkTelPreflight").enabledSelf && B("DkTelPreflight").tooltip == T("tools.needTelemetry"));
            Check("the third pill of the strip fits inside the window", Root.Q("DkTelToolsStrip") != null && Root.Q("DkTelToolsStrip").worldBound.xMax <= Root.worldBound.xMax + 0.5f && Root.Q("DkTelToolsStrip").worldBound.xMin >= -0.5f);
            yield return Shot("01_sin_telemetria", 0.2f);

            tel.StartSimulator();
            yield return new WaitForSecondsRealtime(2.5f);
            Check("simulator running; «Mapa» and «Comprobar» enabled", DesktopTelemetry.IsSimulator && B("DkTelMap").enabledSelf && B("DkTelPreflight").enabledSelf);

            yield return PreflightChecks();
            yield return MapAndBatteryChecks();
            yield return ReplayChecks();

            DesktopKeyboardGate.KeyHeld = UnityEngine.Input.GetKey;
            DesktopKeyboardGate.KeyPressed = UnityEngine.Input.GetKeyDown;
            Log("black captures: " + _blackShots);
            Check("no capture came out black", _blackShots == 0);
            Log("RESULT " + _pass + " pass, " + _fail + " fail");
            File.WriteAllText(Path.Combine(OutputDirectory, "lote1b_" + _tag + ".log"), _log.ToString());
            Application.Quit();
        }

        // ───────────────────────── 3. pre-flight ─────────────────────────

        private IEnumerator PreflightChecks()
        {
            Log("── pre-flight (tool 3) ──");
            var tel = Tel;
            yield return new WaitForSecondsRealtime(1.5f);
            PreflightResult r = tel.EvaluatePreflight();
            Check("on the ground in the simulator: nothing fails, three warnings (the boxes nobody ticked yet): «Con avisos»", r != null && r.summary.fail == 0 && r.summary.warn == 3 && r.summary.verdict == PreflightVerdict.Warn);
            Log("summary pass " + r.summary.pass + " warn " + r.summary.warn + " fail " + r.summary.fail + " na " + r.summary.na);
            Check("the strip dot is amber with warnings", Root.Q("DkPfDot") != null && Root.Q("DkPfDot").ClassListContains("dk-dot--warn"));
            Click(B("DkTelPreflight"));
            yield return new WaitForSecondsRealtime(0.6f);
            Check("the button opens the popover «Comprobaciones previas»", tel.OpenPopover == "preflight" && Shown(tel.PreflightPopover) && tel.PreflightPopover.Q<Label>(className: "dk-pop-title").text == T("pf.title"));
            Check("the popover fits inside the window", tel.PreflightPopover.worldBound.yMax <= Root.worldBound.yMax + 0.5f && tel.PreflightPopover.worldBound.xMax <= Root.worldBound.xMax + 0.5f && tel.PreflightPopover.worldBound.yMin >= 0f);
            foreach (string id in new[] { "link", "bat.rem", "bat.cell", "temp", "vib", "armed", "fence", "faults", "gps" }) Log("row " + id + ": " + tel.PreflightRowText(id));
            string sim = FlightOrigins.Text(ReadingOrigin.Simulated, Lang, HonestyLabels.Load());
            Check("the readings of the simulator carry the origin «simulado» (battery, temperatures, vibration, state, fence)", tel.PreflightRowText("bat.rem").EndsWith("| " + sim) && tel.PreflightRowText("temp").EndsWith("| " + sim) && tel.PreflightRowText("vib").EndsWith("| " + sim) && tel.PreflightRowText("armed").EndsWith("| " + sim) && tel.PreflightRowText("fence").EndsWith("| " + sim));
            Check("the link is the app's own measure («app») and reads a rate in Hz", tel.PreflightRowText("link").Contains(" Hz") && tel.PreflightRowText("link").EndsWith("| " + T("pf.origin.app")));
            Check("GPS is honest: «Sin dato», «El cable no trae GPS»", tel.PreflightRowText("gps").Contains(T("pf.status.na")) && tel.PreflightRowText("gps").Contains(T("pf.gps.na")));
            Check("the fence row gives the nearest margin of the default fence (the 60 m ceiling: «60 m de margen»)", tel.PreflightRowText("fence").Contains(TelemetryStrings.F("pf.fence.margin", Lang, "m", "60")));
            Check("state «Desarmado en tierra» passes", tel.PreflightRowText("armed").Contains(T("pf.armed.disarmed")) && tel.PreflightRowText("armed").Contains(T("pf.status.pass")));
            Check("summary line: «Con avisos» and the counts", tel.PreflightVerdictText == T("pf.warn") && tel.PreflightCountsText == TelemetryStrings.F("pf.counts", Lang, "pass", r.summary.pass, "warn", 3, "fail", 0));
            Check("the three boxes are offered in a live source", tel.PreflightManualShown);
            yield return Shot("10_comprobaciones_avisos", 0.2f);

            for (int i = 0; i < 3; i++) Click(tel.PreflightManualButton(i));
            yield return new WaitForSecondsRealtime(0.5f);
            r = tel.PreflightNow;
            Check("ticking the three boxes leaves no warning: «Listo», green dot", r.summary.warn == 0 && r.summary.fail == 0 && r.summary.verdict == PreflightVerdict.Ready && tel.PreflightVerdictText == T("pf.ready") && Root.Q("DkPfDot").ClassListContains("dk-dot--live"));
            yield return Shot("11_comprobaciones_listo", 0.2f);

            // a failing check: the battery of the simulator at 20 % (below the 30 % of the checklist)
            Sim.Model.rem = 0.2;
            yield return new WaitForSecondsRealtime(1.0f);
            r = tel.PreflightNow;
            Check("battery at 20 %: «Carga de la batería» fails, verdict «No apto», red dot", r.Find("bat.rem").status == PreflightStatus.Fail && r.summary.verdict == PreflightVerdict.Fail && tel.PreflightVerdictText == T("pf.fail") && Root.Q("DkPfDot").ClassListContains("dk-dot--crit"));
            yield return Shot("12_comprobaciones_no_apto", 0.2f);
            tel.CloseAllPopovers();
            Click(B("DkCmdTakeoff"));
            yield return new WaitForSecondsRealtime(0.5f);
            Check("«Despegar» with a failing check asks first and sends nothing", tel.PreflightAskShown && Frame.landed && !Frame.armed);
            Check("the warning names the failing check", tel.PreflightAskBody != null && tel.PreflightAskBody.Contains(T("pf.item.bat.rem")));
            Check("the warning fits inside the window", Root.Q("DkPfAsk").worldBound.xMin >= 0f && Root.Q("DkPfAsk").worldBound.xMax <= Root.worldBound.xMax + 0.5f);
            yield return Shot("13_despegar_pregunta", 0.2f);
            Click(tel.PreflightAskCancel);
            yield return new WaitForSecondsRealtime(1.0f);
            Check("«Cancelar» leaves things as they were (on the ground, disarmed)", !tel.PreflightAskShown && Frame.landed && !Frame.armed);
            Click(B("DkCmdTakeoff"));
            yield return new WaitForSecondsRealtime(0.4f);
            Click(tel.PreflightAskGo);
            yield return new WaitForSecondsRealtime(2.0f);
            Check("«Despegar igualmente» sends the take-off (the checklist never blocks)", !tel.PreflightAskShown && Frame.armed && !Frame.landed);
            yield return WaitAlt(PilotControls.TakeoffAltitudeM, 20f);
            Sim.Model.rem = 1.0;
            yield return new WaitForSecondsRealtime(1.0f);
            r = tel.PreflightNow;
            Check("in flight the state says «En vuelo: no es prevuelo» and fails", r.Find("armed").status == PreflightStatus.Fail && r.Find("armed").detail == "flying");
        }

        // ───────────────────────── 1. map + 8. battery ─────────────────────────

        private IEnumerator MapAndBatteryChecks()
        {
            Log("── map (tool 1) ──");
            var tel = Tel;
            Check("the map starts hidden", !tel.MapVisible);
            yield return PressKey(KeyCode.M);
            yield return new WaitForSecondsRealtime(0.6f);
            Check("the M key shows the map (220 px, north up, fitted)", tel.MapVisible && tel.MapOn && !tel.MapBig && !tel.MapHeadingUp && tel.MapFitted);
            Check("the map panel sits inside the window", tel.MapPanel.worldBound.xMin >= 0f && tel.MapPanel.worldBound.yMax <= Root.worldBound.yMax + 0.5f);
            Check("simulator: the origin is «Punto de despegue» and the caption says «Posición: simulado»", tel.MapOriginLabel == T("map.originTakeoff") && tel.MapCaption != null && tel.MapCaption.Contains(T("map.posSimulated")));
            tel.ShowFlightTab(true);

            // a flight with turns and climbs: the trail of the map and the current steps the battery estimator needs
            double[][] legs = { new double[] { 14, 0, 6 }, new double[] { 14, 12, 9 }, new double[] { -6, 12, 5 }, new double[] { -6, -8, 10 }, new double[] { 8, -8, 6 }, new double[] { 0, 0, 8 } };
            int at = 0;
            foreach (double[] leg in legs)
            {
                yield return FlyTo(leg[0], leg[1], leg[2], 25f);
                at++;
                if (at == 2) yield return Shot("20_mapa_rastro_norte_arriba", 0.2f);
            }
            yield return new WaitForSecondsRealtime(1.5f);
            Check("the trail of the flight is on the map (" + tel.MapTrailNow.Count + " points)", tel.MapTrailNow.Count > 40);
            Check("the geofence ring of the simulator is on the map", tel.MapCanvas.Fence != null && tel.MapCanvas.Fence.radius == 120);
            Check("the readout and the scale bar have text", !string.IsNullOrEmpty(tel.MapReadout) && !string.IsNullOrEmpty(tel.MapScaleText));
            Log("map readout: " + tel.MapReadout + " | scale " + tel.MapScaleText + " | caption " + tel.MapCaption);
            Click(tel.MapOrientButton);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("the orientation button switches to heading up", tel.MapHeadingUp);
            Click(tel.MapFollowButton);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("«Seguir» keeps the drone at the centre", tel.MapFollowing && Vector2.Distance(tel.MapCanvas.MePx, new Vector2(tel.MapCanvas.Size / 2f, tel.MapCanvas.Size / 2f)) < 2f);
            tel.SetMapBig(true);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("the large size is 360 px and still inside the window", tel.MapBig && Mathf.Abs(Root.Q("DkMapDisc").resolvedStyle.width - 360f) < 1f && tel.MapPanel.worldBound.yMin >= 0f && tel.MapPanel.worldBound.yMax <= Root.worldBound.yMax + 0.5f);
            Log("map large: disc " + F1(Root.Q("DkMapDisc").resolvedStyle.width) + " px, canvas " + F1(tel.MapCanvas.Size) + " px, panel y " + F1(tel.MapPanel.worldBound.yMin) + ".." + F1(tel.MapPanel.worldBound.yMax) + " of " + F1(Root.worldBound.yMax));
            yield return Shot("21_mapa_grande_rumbo_arriba_seguir", 0.2f);
            Click(tel.MapFitButton);
            Click(tel.MapOrientButton);
            tel.SetMapBig(false);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("«Encajar» goes back to the fitted view, north up, 220 px", tel.MapFitted && !tel.MapHeadingUp && !tel.MapBig);
            tel.MapZoom(1.2);
            tel.MapPan(10f, -6f);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("zoom and pan leave the fitted view (free view) and send no command", !tel.MapFitted && !tel.MapFollowing);
            tel.MapFit();

            // wind on the map
            SimTelemetrySource sim = Sim;
            sim.SetConfig(new SimConfigPatch { wind = new WindPatch { speed = 6, fromDeg = 315, gustSigma = 0 } });
            yield return new WaitForSecondsRealtime(1.2f);
            Check("with simulated wind the map shows its arrow and reading", tel.MapWindText != null && (tel.MapCanvas.WindN != 0 || tel.MapCanvas.WindE != 0));
            Log("map wind: " + tel.MapWindText);
            yield return Shot("22_mapa_viento", 0.2f);
            sim.SetConfig(new SimConfigPatch { wind = new WindPatch { speed = 0, fromDeg = 315, gustSigma = 0 } });

            // the test course on the map
            tel.SetCourse(true);
            yield return new WaitForSecondsRealtime(1.5f);
            Check("the test course is drawn on the map while it is on", tel.MapCanvas.Course != null);
            yield return Shot("23_mapa_escenario", 0.2f);
            tel.SetCourse(false);
            yield return new WaitForSecondsRealtime(0.5f);

            Log("── battery (tool 8) ──");
            BatteryReport b = tel.BatteryReportNow;
            Log("battery: accepted pairs " + b.quality + ", R " + (b.rHat.HasValue ? F1(b.rHat.Value * 1000) + " mOhm" : "calculating") + ", sag " + (b.dropV.HasValue ? F2(b.dropV.Value) : "-") + " V, rest " + (b.restV.HasValue ? F2(b.restV.Value) : "-") + " V, slope time " + (b.bySlopeS.HasValue ? F1(b.bySlopeS.Value) : "-") + " s, current time " + (b.byCurrentS.HasValue ? F1(b.byCurrentS.Value) : "-") + " s, samples " + tel.BatteryState.samples.Count);
            foreach (string id in new[] { "r", "drop", "rest", "time", "timeI" }) Log("row " + id + ": " + tel.BatteryRowText(id));
            string est = FlightOrigins.Text(ReadingOrigin.Estimated, Lang, HonestyLabels.Load());
            Check("the sampler filled the ring at 4 Hz while flying", tel.BatteryState.samples.Count > 60);
            Check("the resistance is estimated from the current steps of the flight: about 11.5 mΩ in the simulator (web golden 0.0115 Ω)", b.rHat.HasValue && Math.Abs(b.rHat.Value - 0.0115) < 0.003);
            Check("every derived row carries the origin «estimado»", tel.BatteryRowText("r").EndsWith("| " + est) && tel.BatteryRowText("drop").EndsWith("| " + est) && tel.BatteryRowText("rest").EndsWith("| " + est) && tel.BatteryRowText("time").EndsWith("| " + est));
            Check("the section says «Estimado sobre datos simulados.» in the simulator", tel.BatterySimDataNote == T("battery.simData"));
            Check("«Por corriente» exists only here (the simulator knows its 5 Ah) and reads a time in flight", tel.BatteryRowText("timeI") != null && b.byCurrentS.HasValue);
            Check("the chart «Tensión frente a corriente» is drawn with its fitted line", tel.BatteryChartShown && tel.BatteryChart.Drawn > 20 && tel.BatteryChart.LineDrawn && tel.BatteryFitText.Contains("mΩ"));
            VisualElement vi = Root.Q("DkBatVi");
            Root.Q<ScrollView>("DkFlightPanel").ScrollTo(vi);
            yield return Shot("30_bateria_simulador", 0.5f);

            DesktopTelemetry.Client.Send(ClientMessage.Land(), out _);
            yield return Landed();
            yield return new WaitForSecondsRealtime(1.0f);
            Check("on the ground the time left is not extrapolated («—», «solo en vuelo y con la carga bajando»)", tel.BatteryRowText("time").Contains("—"));
        }

        // ───────────────────────── the replay of the real flight ─────────────────────────

        private IEnumerator ReplayChecks()
        {
            Log("── replay of the real flight ──");
            var tel = Tel;
            tel.StartLocalReplay();
            yield return new WaitForSecondsRealtime(3.0f);
            Check("replay: the map stays on, a new trail, origin «Origen local (EKF)», «Posición: estimado» (D-10, contrato 1.2), no geofence ring", tel.MapVisible && tel.MapOriginLabel == T("map.originLocal") && tel.MapCaption.Contains(T("map.posEstimated")) && tel.MapCanvas.Fence == null);
            Click(B("DkTelPreflight"));
            yield return new WaitForSecondsRealtime(0.6f);
            PreflightResult r = tel.PreflightNow;
            Check("replay: the checklist evaluates the recorded flight at this instant (note), without boxes", tel.PreflightReplayNote == T("pf.replayNote") && !tel.PreflightManualShown && r.Find("manual.props").status == PreflightStatus.Na);
            string measured = FlightOrigins.Text(ReadingOrigin.Measured, Lang, HonestyLabels.Load());
            Check("replay: battery and vibration are «medido»; the fence «Solo existe en el Simulador»; faults «sintética»", tel.PreflightRowText("bat.cell").EndsWith("| " + measured) && tel.PreflightRowText("vib").EndsWith("| " + measured) && tel.PreflightRowText("fence").Contains(T("pf.fence.simOnly")) && tel.PreflightRowText("faults").EndsWith("| " + T("pf.origin.synthetic")));
            foreach (string id in new[] { "link", "bat.rem", "bat.cell", "temp", "vib", "armed", "fence", "faults", "gps" }) Log("row " + id + ": " + tel.PreflightRowText(id));
            yield return Shot("40_repeticion_comprobaciones_y_mapa", 0.2f);
            tel.CloseAllPopovers();
            Click(B("DkCmdTakeoff"));
            yield return new WaitForSecondsRealtime(0.4f);
            Check("replay: «Despegar» is refused as before (it takes no commands) and the warning does not appear", !tel.PreflightAskShown);

            // the real flight: 20 s of it for the estimator
            tel.ShowFlightTab(true);
            yield return new WaitForSecondsRealtime(20f);
            BatteryReport b = tel.BatteryReportNow;
            Log("replay battery: accepted pairs " + b.quality + ", R " + (b.rHat.HasValue ? F1(b.rHat.Value * 1000) + " mOhm (the whole TWF gives 82 mOhm in the web golden)" : "calculating") + ", samples " + tel.BatteryState.samples.Count);
            foreach (string id in new[] { "r", "drop", "rest", "time", "timeI" }) Log("row " + id + ": " + tel.BatteryRowText(id));
            Check("replay: no «Por corriente» row and no «datos simulados» note (the readings are the real ones)", tel.BatteryRowText("timeI") == null && tel.BatterySimDataNote == null);
            Check("replay: the derived rows are «estimado», never «medido»", tel.BatteryRowText("r").EndsWith("| " + FlightOrigins.Text(ReadingOrigin.Estimated, Lang, HonestyLabels.Load())));
            Check("replay: the chart shows the cloud of the real flight", tel.BatteryChartShown && tel.BatteryChart.Drawn > 20);
            Root.Q<ScrollView>("DkFlightPanel").ScrollTo(Root.Q("DkBatVi"));
            yield return Shot("41_repeticion_bateria", 0.5f);
            yield return PressKey(KeyCode.M);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("M hides the map again", !tel.MapVisible);
            tel.StopSession();
            yield return new WaitForSecondsRealtime(0.8f);
            Check("without a session the tools are dimmed again and the warning is gone", !B("DkTelMap").enabledSelf && !B("DkTelPreflight").enabledSelf && !tel.PreflightAskShown);
        }
    }
}
#endif
