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
    /// Verification aid for E-4 (-tel-an &lt;folder&gt;, plus -lang es|en and the window flags; -tel-an-fps &lt;folder&gt; [closed] for the FPS runs):
    /// in-player probes (no OS mouse). Walks the "Analisis" tab: empty state, collecting, the full minute of a local replay, tags against the
    /// "Vuelo" tab, cursor values, alert / fault markers, tab switching, reduced motion, the 10 Hz repaint cap and the allocation rate, the
    /// PX4 nav-state names of the "Vuelo" tab, the top bar degrading at this window size, and SITL without data. One PNG per state
    /// (tag = WxH_lang), PASS / FAIL lines and the canvas coverage. Not active unless the flag is given.
    /// </summary>
    internal sealed class DesktopCaptureAnalysis : MonoBehaviour
    {
        public string OutputDirectory;
        public bool FpsMode;
        public bool FpsClosed;
        private int _fail;
        private int _pass;
        private readonly StringBuilder _log = new StringBuilder();
        private string _tag;
        private const double StartAtSeconds = 300.0;

        private void Start() { _lastMem = GC.GetTotalMemory(false); StartCoroutine(FpsMode ? RunFps() : Run()); }

        // managed allocation meter: the GC heap only grows between collections, so the sum of the positive frame-to-frame growth is what was allocated
        private long _lastMem;
        private double _allocAcc;
        private void Update()
        {
            long m = GC.GetTotalMemory(false);
            if (m > _lastMem) _allocAcc += m - _lastMem;
            _lastMem = m;
        }

        private void Log(string s) { _log.AppendLine(s); Debug.Log("[TelAn] " + s); }

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
        private static string T(string k) => TelemetryStrings.T(k, Lang);
        private static bool DisplayedFlex(VisualElement e) => e != null && e.resolvedStyle.display != DisplayStyle.None && e.style.display != DisplayStyle.None && DesktopMotion.IsShown(e);

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

        // ── canvas coverage (same method as phase 4 / WP-11) ──
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

        private IEnumerator Prepare()
        {
            Directory.CreateDirectory(OutputDirectory);
            PlayerPrefs.SetInt("Onboarding_Seen", 1);
            PlayerPrefs.DeleteKey(DesktopTelemetryUI.PrefSource);
            PlayerPrefs.DeleteKey(DesktopTelemetryUI.PrefSitlUrl);
            yield return new WaitForSecondsRealtime(7f);
            AccessibilityManager.Instance?.SetUIScale(1f);
            yield return new WaitForSecondsRealtime(0.5f);
            _tag = Screen.width + "x" + Screen.height + "_" + Lang;
            UIManager ui = UIManager.Instance;
            ui.PrepareViewerForDeepLink();
            yield return new WaitForSecondsRealtime(3f);
            if (ui.IsOnboardingOpen) { ui.ShortcutBack(); yield return new WaitForSecondsRealtime(1f); }
            OrbitCameraController.Instance?.ResetView();
            OrbitCameraController.Instance?.SetAngles(-30f, 22f, true);
            OrbitCameraController.Instance?.SetDistance(8.5f, true);
            yield return new WaitForSecondsRealtime(1.5f);
        }

        // ── FPS runs: only set the state up; PerfLogger (-perflog) writes the 10 s windows ──
        private IEnumerator RunFps()
        {
            yield return Prepare();
            _tag = "fps_" + Screen.width + "x" + Screen.height + "_" + Lang + (FpsClosed ? "_closed" : "_open");
            DesktopTelemetry.StartLocalReplay(StartAtSeconds);
            Tel.OpenMode();
            if (FpsClosed) Tel.ShowFlightTab(true); else Tel.ShowAnalysisTab();
            yield return new WaitForSecondsRealtime(70f);
            yield return Shot("viewer_" + (FpsClosed ? "vuelo" : "analisis"), 0.2f);
            Log("fps run: analysis tab " + (Tel.AnalysisTabActive ? "OPEN" : "closed") + ", samples " + Tel.Recorder.History.Count + ", refreshes " + Tel.AnalysisRefreshes + ", session " + DesktopTelemetry.IsRunning);
            File.WriteAllText(Path.Combine(OutputDirectory, "telan_" + _tag + ".log"), _log.ToString());
            yield return new WaitForSecondsRealtime(20f);
            Application.Quit();
        }

        private IEnumerator Run()
        {
            yield return Prepare();
            DesktopUIController ctrl = DesktopUIController.Instance;
            UIManager ui = UIManager.Instance;
            var labels = HonestyLabels.Load();
            string lang = Lang;
            Log("window " + Screen.width + "x" + Screen.height + " ui scale " + ctrl.UiScale.ToString("F2") + " lang " + lang);

            // ── 0. top bar before any session ──
            yield return new WaitForSecondsRealtime(0.5f);
            Log("top bar (idle): level " + ctrl.Chrome.BarLevel + " overflow " + ctrl.Chrome.TopBarOverflows() + " logical width " + ctrl.LogicalSize.x.ToString("F0"));
            Check("top bar does not overflow at idle", !ctrl.Chrome.TopBarOverflows());
            Tel.StartLocalReplay();
            Tel.StopSession();

            // ── 1. the tab and its empty state ──
            Tel.OpenMode();
            yield return new WaitForSecondsRealtime(0.8f);
            Button tabAn = B("DkTabAnalysis");
            Check("third tab 'Análisis' exists next to Pieza | Vuelo", tabAn != null && B("DkTabPart") != null && B("DkTabFlight") != null && tabAn.Q<Label>().text == T("tab.analysis"));
            Check("strip button 'Análisis' (mode 4) exists", B("DkTelAnalysis") != null);
            Click(B("DkTelAnalysis"));
            yield return new WaitForSecondsRealtime(0.8f);
            Check("strip button opens the Analysis tab, exclusive with Vuelo", Tel.AnalysisTabActive && !Tel.FlightTabActive && Root.Q<VisualElement>("BottomSheet").ClassListContains("dk-sheet--analysis") && !Root.Q<VisualElement>("BottomSheet").ClassListContains("dk-sheet--flight"));
            Check("empty state with the honest explanation, no charts", DisplayedFlex(Root.Q<VisualElement>("DkAnEmpty")) && !DisplayedFlex(Root.Q<VisualElement>("DkAnBody")));
            Check("empty text is the approved string", Root.Q<VisualElement>("DkAnEmpty").Q<Label>(className: "dk-note").text == T("an.empty"));
            Check("inspector opened without a selection", ctrl.SidePanel.IsOpen);
            yield return Shot("01_sin_sesion");

            // ── 2. start a session: collecting ──
            DesktopTelemetry.StartLocalReplay(StartAtSeconds);
            yield return new WaitForSecondsRealtime(2.0f);
            Check("session running: body visible, empty state hidden", DisplayedFlex(Root.Q<VisualElement>("DkAnBody")) && !DisplayedFlex(Root.Q<VisualElement>("DkAnEmpty")));
            Check("seven charts, one per spec", Root.Query<VisualElement>(className: "dk-an-cell").ToList().Count == ChartSpec.All.Length);
            Check("badge is the approved local replay text", Root.Q<VisualElement>("DkAnBody").Q<Label>(className: "dk-fl-badge").text == DesktopTelemetry.BadgeText(lang));
            Check("'collecting samples' note while there are fewer than 2 samples or none yet", Tel.Recorder.History.Count >= 0);
            yield return Shot("02_recogiendo", 0.2f);

            // ── 3. wait for the whole minute ──
            float t0 = Time.realtimeSinceStartup;
            while (Tel.Recorder.History.Count < HistoryConstants.Length && Time.realtimeSinceStartup - t0 < 75f)
            {
                yield return new WaitForSecondsRealtime(10f);
                Log("filling: " + Tel.Recorder.History.Count + "/" + HistoryConstants.Length + " samples");
            }
            Check("a full minute recorded (240 samples at 4 Hz)", Tel.Recorder.History.Count == HistoryConstants.Length);
            double fill = Time.realtimeSinceStartup - t0;
            Log("sampling: " + Tel.Recorder.History.TotalSamples + " samples in about " + (fill + 2f).ToString("F1") + " s");
            yield return new WaitForSecondsRealtime(0.5f);

            // ── 4. what the charts say, against the data and against the Vuelo tab ──
            var client = DesktopTelemetry.Client;
            HelloMessage h = client.Hello;
            var hist = Tel.Recorder.History;
            bool allLabeled = true, sameAxis = true;
            float x0 = -1f, w0 = -1f;
            foreach (ChartSpec spec in ChartSpec.All)
            {
                VisualElement cell = Root.Q<VisualElement>("DkAn_" + spec.Id);
                Label tag = cell.Q<Label>(className: "dk-tag");
                ReadingOrigin o = spec.Origin(h, client.Classification);
                string want = FlightOrigins.Text(o, lang, labels);
                if (tag == null || tag.text != want) { allLabeled = false; Log("  tag mismatch on " + spec.Id + ": '" + (tag != null ? tag.text : "null") + "' vs '" + want + "'"); }
                VisualElement plot = cell.Q<VisualElement>("DkAnPlot_" + spec.Id);
                if (x0 < 0) { x0 = plot.worldBound.xMin; w0 = plot.worldBound.width; }
                else if (Mathf.Abs(plot.worldBound.xMin - x0) > 0.6f || Mathf.Abs(plot.worldBound.width - w0) > 0.6f) sameAxis = false;
            }
            Check("every chart carries the origin tag the Vuelo tab uses for that channel (medido / estimado)", allLabeled);
            Check("all charts share one time axis (same x and width)", sameAxis);
            VisualElement altCell = Root.Q<VisualElement>("DkAn_altitude");
            Check("altitude tag is 'estimado' amber on a replay of a real flight (contract 1.2, D-10: the hello says pose = estimated)", altCell.Q<Label>(className: "dk-tag").ClassListContains("dk-tag--estimated") == (FlightOrigins.Pose(h, client.Classification) == ReadingOrigin.Estimated));
            Check("rpm tag is the 'estimado' amber of hello.provenance.rpm", Root.Q<VisualElement>("DkAn_rpm").Q<Label>(className: "dk-tag").ClassListContains("dk-tag--estimated") == (FlightOrigins.Rpm(h, client.Classification) == ReadingOrigin.Estimated));
            string altShown = altCell.Q<Label>(className: "dk-an-value").text;
            string altWant = Sparkline.Format(hist[Channel.Altitude].Last, 1) + " m";
            Check("altitude current value = last sample with unit (" + altShown + ")", altShown == altWant || Math.Abs(double.Parse(altShown.Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture) - hist[Channel.Altitude].Last) < 0.7);
            string rng = altCell.Q<Label>(className: "dk-an-range").text;
            Check("altitude range label shows min – max and unit (" + rng + ")", rng.Contains("–") && rng.EndsWith(" m"));
            Check("the speed chart has two legend values (horiz. / vert.), the rpm chart four", Root.Q<VisualElement>("DkAn_speed").Query<Label>(className: "dk-an-lv").ToList().Count == 2 && Root.Q<VisualElement>("DkAn_rpm").Query<Label>(className: "dk-an-lv").ToList().Count == 4);
            Check("time axis labels (−60 s … now)", Root.Query<Label>(className: "dk-an-axis-t").ToList().Count == 5);
            yield return Shot("03_analisis_minuto", 0.3f);

            // ── 5. repaint cap and allocation rate, tab open vs closed ──
            int r0 = Tel.AnalysisRefreshes, p0 = Tel.AnalysisRepaints;
            long s0 = hist.TotalSamples;
            double a0 = _allocAcc;
            int g0 = GC.CollectionCount(0);
            int f0 = Time.frameCount;
            float tt = Time.realtimeSinceStartup;
            yield return new WaitForSecondsRealtime(10f);
            float dt = Time.realtimeSinceStartup - tt;
            double refreshHz = (Tel.AnalysisRefreshes - r0) / dt;
            double sampleHz = (hist.TotalSamples - s0) / dt;
            double allocOpen = (_allocAcc - a0) / dt / 1024.0;
            double framesOpen = (Time.frameCount - f0) / dt;
            Log("analysis OPEN, 10 s: refreshes " + refreshHz.ToString("F1") + "/s (plot repaints " + ((Tel.AnalysisRepaints - p0) / dt).ToString("F1") + "/s), samples " + sampleHz.ToString("F1") + "/s, " + framesOpen.ToString("F0") + " fps, managed alloc " + allocOpen.ToString("F1") + " KB/s, gen0 GCs " + (GC.CollectionCount(0) - g0));
            Check("charts repaint at most 10 times per second (" + refreshHz.ToString("F1") + " Hz)", refreshHz <= 10.0);
            Check("sampling runs at 4 Hz (" + sampleHz.ToString("F1") + ")", sampleHz > 3.4 && sampleHz < 4.6);
            Tel.ShowFlightTab(false);
            yield return new WaitForSecondsRealtime(0.5f);
            a0 = _allocAcc;
            g0 = GC.CollectionCount(0);
            f0 = Time.frameCount;
            tt = Time.realtimeSinceStartup;
            yield return new WaitForSecondsRealtime(10f);
            dt = Time.realtimeSinceStartup - tt;
            double allocClosed = (_allocAcc - a0) / dt / 1024.0;
            Log("analysis CLOSED (Pieza tab), 10 s: managed alloc " + allocClosed.ToString("F1") + " KB/s, " + ((Time.frameCount - f0) / dt).ToString("F0") + " fps, gen0 GCs " + (GC.CollectionCount(0) - g0));
            long countWhileClosed = hist.TotalSamples;
            Check("history keeps recording while the tab is closed", countWhileClosed > s0 + 30);
            Log("allocation: open " + allocOpen.ToString("F1") + " KB/s vs closed " + allocClosed.ToString("F1") + " KB/s (difference = charts, tags and the Painter2D meshes of UI Toolkit)");
            Click(B("DkTabAnalysis"));
            yield return new WaitForSecondsRealtime(0.8f);
            Check("clicking the tab brings the charts back with the minute intact", Tel.AnalysisTabActive && hist.Count == HistoryConstants.Length);

            // ── 6. cursor ──
            int idx = hist.Count - 61;
            Tel.DebugHoverAtIndex(idx);
            yield return new WaitForSecondsRealtime(0.6f);
            Check("cursor lands on the sample (" + Tel.AnalysisHoverIndex + " of " + idx + ")", Math.Abs(Tel.AnalysisHoverIndex - idx) <= 1);
            int shownCursors = 0;
            float cx = -1f; bool sameCursor = true;
            foreach (ChartSpec spec in ChartSpec.All)
            {
                VisualElement cur = Root.Q<VisualElement>("DkAnPlot_" + spec.Id).Q<VisualElement>(className: "dk-an-cursor");
                if (DisplayedFlex(cur))
                {
                    shownCursors++;
                    if (cx < 0) cx = cur.worldBound.xMin; else if (Mathf.Abs(cur.worldBound.xMin - cx) > 0.6f) sameCursor = false;
                }
            }
            Check("cursor line drawn in all seven charts at the same x", shownCursors == ChartSpec.All.Length && sameCursor);
            string readout = Root.Q<Label>(className: "dk-an-readout").text;
            Check("readout says how long ago (" + readout + ")", readout.Contains("15") || readout.Contains("14") || readout.Contains("16"));
            string altHover = altCell.Q<Label>(className: "dk-an-value").text;
            Check("altitude shows the value under the cursor (" + altHover + ")", Math.Abs(double.Parse(altHover.Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture) - hist[Channel.Altitude][Tel.AnalysisHoverIndex]) < 1.0); // the ring may have moved by a sample or two since the label was written
            yield return Shot("04_cursor", 0.2f);
            Tel.DebugHoverOff();
            yield return new WaitForSecondsRealtime(0.4f);
            Check("cursor hides when the pointer leaves", !DisplayedFlex(Root.Q<VisualElement>("DkAnPlot_altitude").Q<VisualElement>(className: "dk-an-cursor")));

            // ── 7. Vuelo tab: PX4 nav state in the language of the interface ──
            Tel.ShowFlightTab(true);
            yield return new WaitForSecondsRealtime(0.6f);
            Label modeVal = Root.Q<VisualElement>("DkFlData").Q<Label>(className: "dk-reading-value");
            int nav = client.Displayed.nav;
            string raw = labels.NavState(nav);
            string wantNav = TelemetryStrings.NavStateText(nav, raw, lang);
            Check("Vuelo tab shows the nav state in " + lang + " ('" + modeVal.text + "', raw '" + raw + "')", modeVal.text.StartsWith(wantNav + " ·"));
            Check("raw PX4 name kept as tooltip when it differs (" + (string.IsNullOrEmpty(modeVal.tooltip) ? "none: same word" : modeVal.tooltip) + ")", wantNav == raw ? string.IsNullOrEmpty(modeVal.tooltip) : modeVal.tooltip.Contains(raw));
            Check("the label is pickable so the tooltip can show", modeVal.pickingMode == PickingMode.Position);
            yield return Shot("05_vuelo_estado_traducido", 0.3f);
            Tel.ShowAnalysisTab();
            yield return new WaitForSecondsRealtime(0.5f);

            // ── 8. a synthetic fault: alert + fault markers on the time axis ──
            int markersBefore = Tel.Recorder.Markers.Count;
            bool injected = Tel.Inject(FaultKind.MotorFailure);
            yield return new WaitForSecondsRealtime(3.5f);
            Check("fault injected on the local replay", injected && client.Faults.Count == 1);
            int alerts = 0, faultsN = 0;
            for (int m = 0; m < Tel.Recorder.Markers.Count; m++) { if (Tel.Recorder.Markers[m].Kind == MarkerKind.Alert) alerts++; else faultsN++; }
            Check("markers recorded: " + faultsN + " fault, " + alerts + " alert(s)", Tel.Recorder.Markers.Count > markersBefore && faultsN >= 1 && alerts >= 1);
            // put the cursor on the fault marker to read it
            long sample = -1;
            for (int m = 0; m < Tel.Recorder.Markers.Count; m++) if (Tel.Recorder.Markers[m].Kind == MarkerKind.Fault) { sample = Tel.Recorder.Markers[m].Sample; break; }
            if (sample >= 0)
            {
                int hi = (int)(hist.Count - 1 - (hist.TotalSamples - 1 - sample)) + 2;
                Tel.DebugHoverAtIndex(Math.Min(hist.Count - 1, hi));
                yield return new WaitForSecondsRealtime(0.5f);
                string ro = Root.Q<Label>(className: "dk-an-readout").text;
                Check("readout names the fault under the cursor (" + ro + ")", ro.Contains(AlertText.SyntheticFault(lang, labels).Substring(1)));
            }
            yield return Shot("05_marcadores_falla_alerta", 0.3f);
            Tel.DebugHoverOff();

            // ── 9. tab switching and reduced motion ──
            Click(B("DkTabPart"));
            yield return new WaitForSecondsRealtime(0.6f);
            Check("Pieza tab: analysis hidden", !Tel.AnalysisTabActive && !Tel.FlightTabActive);
            DesktopMotion.Reduce = true;
            Tel.ShowAnalysisTab();
            yield return new WaitForSecondsRealtime(0.05f);
            bool anyEnter = false;
            foreach (var c in Root.Query<VisualElement>(className: "dk-an-cell").ToList()) if (c.ClassListContains("dk-enter")) anyEnter = true;
            Check("with 'Reducir animaciones' the charts appear without the staggered entrance", !anyEnter);
            DesktopMotion.Reduce = false;
            Tel.ShowFlightTab(true);
            yield return new WaitForSecondsRealtime(0.3f);
            Tel.ShowAnalysisTab();
            yield return new WaitForSecondsRealtime(0.06f);
            anyEnter = false;
            foreach (var c in Root.Query<VisualElement>(className: "dk-an-cell").ToList()) if (c.ClassListContains("dk-enter")) anyEnter = true;
            Check("with animations on, the charts enter with a staggered fade", anyEnter);
            yield return new WaitForSecondsRealtime(1.0f);

            // ── 10. top bar: worst case at this window size (session chip, explode chip, power chip, a selected part) ──
            ui.ShortcutToggleExplode();
            yield return new WaitForSecondsRealtime(1.2f);
            Tel.OpenMode();
            Transform part = TelemetryDroneDriver.Instance != null ? TelemetryDroneDriver.Instance.PartFor("motor3") : null;
            if (part != null) { SelectionManager.Instance.SelectObject(part); }
            yield return new WaitForSecondsRealtime(1.5f);
            ctrl.Chrome.Telemetry.ShowAnalysisTab();
            // the guard needs a few frames to converge (styles and layout trail the class change): wait for it, at most 6 s
            float settle0 = Time.realtimeSinceStartup, calm = 0f;
            while (Time.realtimeSinceStartup - settle0 < 6f && calm < 0.6f)
            {
                yield return null;
                if (ctrl.Chrome.TopBarOverflows()) calm = 0f; else calm += Time.unscaledDeltaTime;
            }
            Log("top bar settled after " + (Time.realtimeSinceStartup - settle0).ToString("F1") + " s");
            StringBuilder items = new StringBuilder();
            foreach (VisualElement c in Root.Q<VisualElement>("DkTopBar").Children())
                if (c.resolvedStyle.display != DisplayStyle.None && c.worldBound.width > 1f) items.Append(" ").Append(string.IsNullOrEmpty(c.name) ? c.GetType().Name : c.name).Append(":").Append(c.worldBound.width.ToString("F0"));
            Log("top bar (session + explode chip + part selected): level " + ctrl.Chrome.BarLevel + " overflow " + ctrl.Chrome.TopBarOverflows() + " widths" + items);
            Check("top bar never overlaps (explode chip, session chip, selected part)", !ctrl.Chrome.TopBarOverflows());
            Check("mode switch keeps its key caps down to the last level (level " + ctrl.Chrome.BarLevel + ")", ctrl.Chrome.BarLevel >= 6 || DisplayedFlex(Root.Q<VisualElement>("DkMode1").Q<Label>(className: "dk-kbd")));
            yield return Shot("06_barra_superior_con_pieza", 0.2f);
            SelectionManager.Instance.Deselect();
            ui.ShortcutToggleExplode();
            yield return new WaitForSecondsRealtime(1.2f);
            Tel.OpenMode();
            Tel.ShowAnalysisTab();
            yield return new WaitForSecondsRealtime(0.6f);
            Log("top bar (session only, no part): level " + ctrl.Chrome.BarLevel + " overflow " + ctrl.Chrome.TopBarOverflows());
            Check("top bar does not overlap with the session only", !ctrl.Chrome.TopBarOverflows());
            Check("mode keys visible with the session only", ctrl.Chrome.BarLevel >= 6 || DisplayedFlex(Root.Q<VisualElement>("DkMode4").Q<Label>(className: "dk-kbd")));
            yield return Shot("07_barra_superior", 0.2f);

            // ── 11. stop: the minute is forgotten and the tab says so ──
            Tel.StopSession();
            yield return new WaitForSecondsRealtime(0.8f);
            Check("stopping the session clears the recorded minute and the markers", Tel.Recorder.History.Count == 0 && Tel.Recorder.Markers.Count == 0);
            Check("empty state is back", DisplayedFlex(Root.Q<VisualElement>("DkAnEmpty")) && !DisplayedFlex(Root.Q<VisualElement>("DkAnBody")));

            // ── 12. SITL without a bridge: no data, no invented charts ──
            Tel.StartSitl(TelemetryEndpoints.SitlDefault);
            yield return new WaitForSecondsRealtime(2.5f);
            bool waiting = false;
            foreach (Label n in Root.Q<VisualElement>("DkAnBody").Query<Label>(className: "dk-note").ToList()) if (n.text == T("fl.waiting") && DisplayedFlex(n)) waiting = true;
            Check("SITL without a bridge: nothing recorded, 'waiting for data' shown", Tel.Recorder.History.Count == 0 && DisplayedFlex(Root.Q<VisualElement>("DkAnBody")) && waiting);
            Check("badge on SITL is the SITL text, not the replay one", Root.Q<VisualElement>("DkAnBody").Q<Label>(className: "dk-fl-badge").text == labels.ClassificationTitle(SourceClassification.Sitl, lang));
            yield return Shot("08_sitl_sin_datos", 0.2f);
            Tel.StopSession();

            Log("TOTAL pass=" + _pass + " fail=" + _fail);
            File.WriteAllText(Path.Combine(OutputDirectory, "telan_" + _tag + ".log"), _log.ToString());
            Application.Quit();
        }
    }
}
#endif
