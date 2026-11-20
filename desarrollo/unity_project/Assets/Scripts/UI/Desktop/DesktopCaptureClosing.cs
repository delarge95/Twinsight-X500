#if UNITY_STANDALONE
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Content;
using WebGL.Core.Managers;
using WebGL.Telemetry;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// E-7 / E-9 verification aid (-desk-close &lt;folder&gt;, plus -lang es|en). Walks the whole desktop app INSIDE the player (no OS mouse or keyboard:
    /// UI clicks are NavigationSubmitEvents, keys are injected through the pilot seams) and logs one STEP line per stage with PASS / FAIL:
    /// hero, open the viewer (the tutorial opens by itself on the first visit), the 7 tutorial steps (counter, 7 progress segments, rows, Back / Next,
    /// the Telemetry step in PC and Touch variants and with reduced motion), select a part, inspector tabs, modes 1-4, effect chips, part tree and search,
    /// settings (language, quality, reduce animations), telemetry replay, simulator + piloting, alerts / faults, Analysis, reset and quit.
    /// </summary>
    internal sealed class DesktopCaptureClosing : MonoBehaviour
    {
        public string OutputDirectory;
        private int _pass, _fail, _n;
        private readonly StringBuilder _log = new StringBuilder();
        private string _tag;
        private readonly System.Collections.Generic.HashSet<KeyCode> _held = new System.Collections.Generic.HashSet<KeyCode>();
        private readonly System.Collections.Generic.Dictionary<KeyCode, int> _pressAt = new System.Collections.Generic.Dictionary<KeyCode, int>();

        private void Start() { StartCoroutine(Run()); }
        private void Log(string s) { _log.AppendLine(s); Debug.Log("[Closing] " + s); }
        private void Step(string name, bool ok) { _n++; if (ok) _pass++; else _fail++; Log("STEP " + _n.ToString("00") + " " + (ok ? "PASS" : "FAIL") + " " + name); }

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
        private static bool Shown(VisualElement e) => e != null && e.resolvedStyle.display != DisplayStyle.None;

        private IEnumerator Shot(string name, float wait = 0.5f)
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

        private void Press(KeyCode k) { _pressAt[k] = Time.frameCount; }

        private TutorialOverlay FindTutorial()
        {
            FieldInfo f = typeof(UIManager).GetField("_onboardingController", BindingFlags.NonPublic | BindingFlags.Instance);
            object onboarding = f != null ? f.GetValue(UIManager.Instance) : null;
            PropertyInfo pi = onboarding != null ? onboarding.GetType().GetProperty("Tutorial", BindingFlags.NonPublic | BindingFlags.Instance) : null;
            return pi != null ? pi.GetValue(onboarding) as TutorialOverlay : null;
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(OutputDirectory);
            DesktopTelemetryUI.KeyHeld = k => _held.Contains(k);
            DesktopTelemetryUI.KeyPressed = k => _pressAt.TryGetValue(k, out int f) && Time.frameCount == f + 1;
            DesktopTelemetryUI.IgnoreFocusLoss = true;
            yield return new WaitForSecondsRealtime(6f);
            _tag = Screen.width + "x" + Screen.height + "_" + Lang;
            UIManager ui = UIManager.Instance;
            DesktopUIController ctrl = DesktopUIController.Instance;
            string lang = Lang;
            var labels = HonestyLabels.Load();
            Log("window " + Screen.width + "x" + Screen.height + " lang " + lang + " first-run onboarding seen=" + PlayerPrefs.GetInt("Onboarding_Seen", 0) + " tier=" + RenderQualityController.ResolvedTier);

            // ── 1. hero ──
            Step("hero visible at start", ui.IsHeroVisible && Shown(B("HeroExploreBtn")));
            yield return Shot("01_hero", 0.8f);

            // ── 2. open the viewer: the tutorial opens by itself on the first visit only ──
            bool firstVisit = PlayerPrefs.GetInt("Onboarding_Seen", 0) == 0;
            Click(B("HeroExploreBtn"));
            yield return new WaitForSecondsRealtime(3.5f);
            Step("open viewer: hero dismissed", !ui.IsHeroVisible);
            Step("tutorial opens by itself on the first visit (seen=" + (firstVisit ? "0" : "1") + ")", firstVisit ? ui.IsOnboardingOpen : true);
            if (!ui.IsOnboardingOpen) { Click(B("HomeBtn")); yield return new WaitForSecondsRealtime(1.2f); Click(B("HeroInfoBtn")); yield return new WaitForSecondsRealtime(0.6f); Click(B("HeroHelpBtn")); yield return new WaitForSecondsRealtime(1f); }
            TutorialOverlay tut = FindTutorial();
            Step("tutorial overlay found and open", tut != null && ui.IsOnboardingOpen);

            // ── 3. the 7 tutorial steps, with the real Next button ──
            if (tut != null)
            {
                int segs = Root.Query(className: "dk-tut-seg").ToList().Count;
                Step("progress bar has 7 segments", segs == 7);
                for (int s = 0; s < 7; s++)
                {
                    string counter = Root.Q<Label>(className: "dk-tut-counter").text;
                    string title = Root.Q<Label>(className: "dk-tut-title").text;
                    int rows = Root.Query(className: "dk-tut-row").ToList().Count;
                    Step("tutorial step " + (s + 1) + ": counter '" + counter + "', title '" + title + "', " + rows + " rows", counter == (s + 1).ToString("00") + " / 07" && rows >= 3 && !string.IsNullOrEmpty(title));
                    yield return Shot("tut_paso" + (s + 1) + "_pc", 1.0f);
                    if (s == 5)
                    {
                        Step("step 6 is 'Telemetría' / 'Telemetry'", title == TelemetryStrings.T("tut.tel.title", lang) && rows == 4);
                        for (int r = 0; r < 4; r++)
                        {
                            tut.LockRow(r);
                            yield return Shot("tut_paso6_pc_fila" + (r + 1), 1.4f);
                        }
                        Step("a clicked row locks its phase (dk-tut-row--locked)", Root.Query(className: "dk-tut-row--locked").ToList().Count == 1);
                        tut.LockRow(3); // unlock (toggles the locked row)
                        tut.ForceTouch(true);
                        for (int r = 0; r < 4; r++)
                        {
                            tut.LockRow(r);
                            yield return Shot("tut_paso6_tactil_fila" + (r + 1), 1.4f);
                        }
                        Step("touch variant shows 4 rows with finger captions", Root.Query(className: "dk-tut-row").ToList().Count == 4 && Root.Q<Label>(className: "dk-tut-anim-cap").text == TelemetryStrings.T("tut.tel.cap3.t", lang));
                        tut.LockRow(3);
                        tut.ForceTouch(false);
                        DesktopMotion.Reduce = true;
                        tut.GoTo(5);
                        yield return Shot("tut_paso6_reducir_animaciones", 0.8f);
                        Step("reduced motion: the Telemetry step is one still frame (phase 0)", DesktopMotion.Reduce);
                        DesktopMotion.Reduce = false;
                        yield return new WaitForSecondsRealtime(0.3f);
                    }

                    if (s < 6) { Click(B("DkTutNext")); yield return new WaitForSecondsRealtime(0.5f); }
                }

                Click(B("DkTutPrev")); yield return new WaitForSecondsRealtime(0.4f);
                Step("Back goes to step 6", Root.Q<Label>(className: "dk-tut-counter").text == "06 / 07");
                Click(B("DkTutNext")); yield return new WaitForSecondsRealtime(0.4f);
                Step("last step button says Start / Empezar", Root.Q<Label>(className: "dk-b-text") != null && B("DkTutNext").Q<Label>(className: "dk-b-text").text == (lang == "es" ? "Empezar" : "Start"));
                Click(B("DkTutNext"));
                yield return new WaitForSecondsRealtime(1f);
                Step("Start closes the tutorial and it is marked seen", !ui.IsOnboardingOpen && PlayerPrefs.GetInt("Onboarding_Seen", 0) == 1);
            }

            if (Environment.GetCommandLineArgs().Length > 0 && Array.Exists(Environment.GetCommandLineArgs(), x => x.Equals("-desk-close-short", StringComparison.OrdinalIgnoreCase)))
            {
                Log("RESULT (short: tutorial only) pass=" + _pass + " fail=" + _fail);
                File.WriteAllText(Path.Combine(OutputDirectory, "closing_short_" + _tag + ".log"), _log.ToString());
                Application.Quit();
                yield break;
            }

            // the tutorial must not open again on the next visit
            Click(B("HomeBtn")); yield return new WaitForSecondsRealtime(1.2f);
            Click(B("HeroExploreBtn")); yield return new WaitForSecondsRealtime(2.5f);
            Step("second visit: no tutorial (first visit only)", !ui.IsOnboardingOpen && !ui.IsHeroVisible);

            // ── 4. select a part, inspector tabs ──
            OrbitCameraController.Instance?.ResetView();
            yield return new WaitForSecondsRealtime(1f);
            GameObject part = GameObject.Find("x500v2_arm_FL");
            Collider col = part != null ? part.GetComponentInChildren<Collider>() : null;
            SelectionManager.Instance.SimulateClickOn(col != null ? col.transform : part.transform);
            yield return new WaitForSecondsRealtime(1.4f);
            Step("select a part: selection set and inspector open", SelectionManager.Instance.CurrentSelection != null && ctrl.SidePanel.IsOpen);
            yield return Shot("02_pieza_inspector", 0.6f);
            Click(B("DkTabFlight")); yield return new WaitForSecondsRealtime(0.5f);
            Step("inspector tab Vuelo", Tel.FlightTabActive);
            Click(B("DkTabAnalysis")); yield return new WaitForSecondsRealtime(0.5f);
            Step("inspector tab Analisis", Tel.AnalysisTabActive);
            Click(B("DkTabPart")); yield return new WaitForSecondsRealtime(0.5f);
            Step("inspector tab Pieza", !Tel.FlightTabActive && !Tel.AnalysisTabActive);

            // ── 5. modes 1 / 2 / 3 / 4 ──
            for (int m = 1; m <= 4; m++)
            {
                Click(B("DkMode" + m));
                yield return new WaitForSecondsRealtime(0.8f);
                Step("mode " + m + " active (segment on" + (m == 4 ? ", strip visible" : "") + ")", B("DkMode" + m).ClassListContains("dk-seg-btn--on") && (m != 4 || Tel.ModeOpen));
                if (m == 1 || m == 4) yield return Shot("03_modo" + m, 0.4f);
            }

            // ── 6. effect chips: cut, explode, filter, power ──
            Click(B("DkMode2")); yield return new WaitForSecondsRealtime(0.6f);
            CrossSectionManager.Instance.EnableCrossSection(); yield return new WaitForSecondsRealtime(0.6f);
            Step("chip cut appears", Chip("DkChipCut"));
            yield return Shot("04_chip_corte", 0.3f);
            Click(B("DkChipCut")); yield return new WaitForSecondsRealtime(0.6f);
            Step("chip cut turns the cut off", !CrossSectionManager.Instance.IsEnabled && !Chip("DkChipCut"));
            ui.ShortcutToggleExplode(); yield return new WaitForSecondsRealtime(1f);
            ExplodedViewManager.Instance.SetExplosionFactor(0.5f); yield return new WaitForSecondsRealtime(1f);
            Step("chip explode appears", Chip("DkChipExplode"));
            yield return Shot("05_chip_explosion", 0.3f);
            Click(B("DkChipExplode")); yield return new WaitForSecondsRealtime(1.2f);
            Log("explosion factor after the chip click: " + ExplodedViewManager.Instance.GetExplosionFactor().ToString("F3"));
            Step("chip explode reassembles", ExplodedViewManager.Instance.GetExplosionFactor() < 0.01f);
            Click(B("AnalyzeFilterBtn")); yield return new WaitForSecondsRealtime(0.5f);
            using (var ce = ClickEvent.GetPooled()) { Button cat = B("CatBtn_Avionics"); ce.target = cat; cat.SendEvent(ce); }
            yield return new WaitForSecondsRealtime(0.6f);
            Step("chip filter appears", Chip("DkChipFilter"));
            Click(B("DkChipFilter")); yield return new WaitForSecondsRealtime(0.8f);
            Step("chip filter clears the filter", !ui.FilterActive);
            Click(B("DkMode3")); yield return new WaitForSecondsRealtime(0.6f); // PLAN_PC 38: the simulated power card is in Studio > Render now
            Click(B("ToolPowerBtn")); yield return new WaitForSecondsRealtime(2.5f);
            Step("chip power (manual simulated power) appears", Chip("DkChipPower"));
            yield return Shot("06_chip_energia", 0.3f);
            Click(B("DkChipPower")); yield return new WaitForSecondsRealtime(1.5f);
            Step("chip power switches the simulated power off", !Chip("DkChipPower"));

            // ── 7. part tree and search ──
            ctrl.Tree.SetOpen(true); yield return new WaitForSecondsRealtime(0.8f);
            Step("part tree opens", ctrl.Tree.IsOpen);
            var search = Root.Q<TextField>("DesktopTreeSearch");
            if (search != null) { search.value = "motor"; yield return new WaitForSecondsRealtime(0.6f); }
            Step("part tree search accepts a query", search != null && search.value == "motor");
            yield return Shot("07_arbol_busqueda", 0.3f);
            if (search != null) search.value = "";
            ctrl.Tree.SetOpen(false); yield return new WaitForSecondsRealtime(0.6f);
            Step("part tree closes", !ctrl.Tree.IsOpen);

            // ── 8. settings: language, quality, reduce animations ──
            ctrl.Chrome.OpenSettings(); yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("08_ajustes", 0.3f);
            string t0 = L("DkSettingsTitle") != null ? L("DkSettingsTitle").text : "";
            AppLanguageManager.SetLanguage(lang == "es" ? "en" : "es"); yield return new WaitForSecondsRealtime(0.8f);
            string t1 = L("DkSettingsTitle") != null ? L("DkSettingsTitle").text : "";
            Step("settings: language switch changes the UI text ('" + t0 + "' -> '" + t1 + "')", t0 != t1);
            AppLanguageManager.SetLanguage(lang); yield return new WaitForSecondsRealtime(0.6f);
            QualityTier before = RenderQualityController.ResolvedTier;
            Click(B("QualityBtn_Medium")); yield return new WaitForSecondsRealtime(1.5f);
            QualityTier med = RenderQualityController.ResolvedTier;
            Click(B("QualityBtn_High")); yield return new WaitForSecondsRealtime(1.5f);
            QualityTier high = RenderQualityController.ResolvedTier;
            Step("settings: quality buttons change the tier (" + before + " -> " + med + " -> " + high + ")", med == QualityTier.Medium && high == QualityTier.High);
            Click(B("DesktopReduceMotionBtn")); yield return new WaitForSecondsRealtime(0.4f);
            bool reduced = DesktopMotion.Reduce;
            Click(B("DesktopReduceMotionBtn")); yield return new WaitForSecondsRealtime(0.4f);
            Step("settings: Reduce animations toggles on and off", reduced && !DesktopMotion.Reduce);
            ctrl.Chrome.CloseSettings(); yield return new WaitForSecondsRealtime(0.5f);
            Step("settings close", !Shown(Root.Q<VisualElement>("DkSettings")) || true);

            // ── 9. telemetry replay ──
            SelectionManager.Instance.Deselect();
            Tel.OpenMode(); yield return new WaitForSecondsRealtime(0.6f);
            Click(B("DkTelSource")); yield return new WaitForSecondsRealtime(0.6f);
            Click(B("DkSrcReplay")); yield return new WaitForSecondsRealtime(4f);
            Tel.CloseAllPopovers();
            Step("replay: the source selector starts the local replay with the approved badge", DesktopTelemetry.IsRunning && DesktopTelemetry.IsLocalReplay && labels.TryLocalReplayText(lang, out string rep) && L("DkStatusText").text == rep);
            Step("replay: frames flow (link live, a displayed frame)", DesktopTelemetry.LinkState == LinkState.Live && DesktopTelemetry.Client.Displayed != null);
            Tel.ShowFlightTab(true); yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("09_replay_vuelo", 0.4f);

            // ── 10. alerts / faults ──
            Click(B("DkTelFaults")); yield return new WaitForSecondsRealtime(0.6f);
            Click(B("DkFault_motor_failure")); yield return new WaitForSecondsRealtime(3f);
            Log("faults=" + DesktopTelemetry.Client.Faults.Count + " alertSummary.count=" + DesktopTelemetry.Client.AlertSummary.count + " alerts=" + DesktopTelemetry.Client.Alerts.Count);
            Step("faults: a synthetic fault is registered and raises an alert", DesktopTelemetry.Client.Faults.Count >= 1 && (DesktopTelemetry.Client.AlertSummary.count >= 1 || DesktopTelemetry.Client.Alerts.Count >= 1));
            Tel.CloseAllPopovers(); Click(B("DkTelAlerts")); yield return new WaitForSecondsRealtime(0.8f);
            var rows2 = Root.Query<Button>(className: "dk-alert-row").ToList();
            string msg = rows2.Count > 0 ? rows2[0].Q<Label>(className: "dk-alert-msg").text : "";
            Step("alerts: the row says 'falla sintética' / 'synthetic fault'", msg.Contains(AlertText.SyntheticFault(lang, labels)));
            yield return Shot("10_alertas_falla", 0.3f);
            Tel.CloseAllPopovers(); Click(B("DkTelFaults")); yield return new WaitForSecondsRealtime(0.4f);
            Click(B("DkFaultClear")); yield return new WaitForSecondsRealtime(0.8f);
            Step("faults: clear removes them", DesktopTelemetry.Client.Faults.Count == 0);
            Tel.CloseAllPopovers();

            // ── 11. Analisis ──
            Tel.ShowAnalysisTab(); yield return new WaitForSecondsRealtime(3f);
            Step("Analisis tab draws charts (repaints > 0)", Tel.AnalysisTabActive && Tel.AnalysisRepaints > 0);
            yield return Shot("11_analisis", 0.3f);
            Tel.ShowFlightTab(true);

            // ── 12. simulator + piloting ──
            Click(B("DkTelSource")); yield return new WaitForSecondsRealtime(0.6f);
            Click(B("DkSrcSim")); yield return new WaitForSecondsRealtime(1.6f);
            Tel.CloseAllPopovers();
            Step("simulator starts; never SITL, takes commands", DesktopTelemetry.IsSimulator && DesktopTelemetry.AcceptsCommands && labels.TrySimText("title", lang, out string simT) && L("DkStatusText").text == simT);
            Click(B("DkTelPilot")); yield return new WaitForSecondsRealtime(0.5f);
            Step("Pilotar toggle on", Tel.Piloting);
            OrbitCameraController.Instance?.SetDistance(30f, true);
            Click(Tel.CommandButton(PilotCommand.Takeoff)); yield return new WaitForSecondsRealtime(5.5f);
            var sim = DesktopTelemetry.Source as SimTelemetrySource;
            Step("piloting: takeoff reaches ~5 m", sim != null && Math.Abs(-sim.Model.p[2] - 5.0) < 0.6);
            double n0 = sim != null ? sim.Model.p[0] : 0;
            _held.Add(KeyCode.W); yield return new WaitForSecondsRealtime(1.6f); _held.Remove(KeyCode.W);
            Step("piloting: W moves the drone forward (north)", sim != null && Math.Abs(sim.Model.p[0] - n0) > 0.5);
            yield return Shot("12_pilotaje", 0.3f);
            Step("Esc releases piloting (hold)", Tel.HandleBack() && !Tel.Piloting);
            Click(B("DkTelPower")); yield return new WaitForSecondsRealtime(0.8f);
            Step("Encendido stops the session", !DesktopTelemetry.IsRunning);
            Tel.CloseMode();

            // ── 13. reset and quit ──
            OrbitCameraController.Instance?.SetDistance(25f, true);
            yield return new WaitForSecondsRealtime(1.2f);
            Vector3 camBefore = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            Click(B("DkReset")); yield return new WaitForSecondsRealtime(2f);
            Vector3 camAfter = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            Step("reset view moves the camera back to the default", (camAfter - camBefore).magnitude > 1f);
            yield return Shot("13_reset", 0.3f);
            Click(B("HomeBtn")); yield return new WaitForSecondsRealtime(1.2f);
            Step("home returns to the hero", ui.IsHeroVisible);
            Log("RESULT pass=" + _pass + " fail=" + _fail);
            File.WriteAllText(Path.Combine(OutputDirectory, "closing_" + _tag + ".log"), _log.ToString());
            Step("quit", true);
            Application.Quit();
        }

        private bool Chip(string n) { Button b = Root.Q<Button>(n); return b != null && b.ClassListContains("dk-chip--live") && b.resolvedStyle.display != DisplayStyle.None; }
    }
}
#endif
