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
    /// Verification aid for Lote 1c (-lote1c-capture &lt;folder&gt;, plus -lang es|en and the window flags). In-player probes, no OS mouse or keyboard (keys go through the
    /// <see cref="DesktopKeyboardGate"/> seams; clicks are UI Toolkit events). Walks:
    ///  - mission planner (tool 2): the strip button, waypoints added on the ground, the route, a 4-waypoint square flown by the runner, progress, the end;
    ///  - course from data (tool 5): the layout comes from the bundled basic.json (not the C# fallback), a lap with one deliberate collision, the penalised time and the
    ///    records kept in records.json;
    ///  - own sessions (tool 7): record the simulator, the red chip, the saved TWF1, play it back as an OWN session («Repetición · sesión propia (simulada)», never
    ///    «sombra digital», every reading simulated, it takes no commands, it loops);
    ///  - guided training (tool 6): the lesson list, the card, the hint after the (shortened) idle time, the objective on the map, lesson 1 completed with the simulator,
    ///    the progress kept.
    /// One PNG per state (tag = WxH_lang) and PASS / FAIL lines; a black capture fails the run. Not active unless the flag is given.
    /// </summary>
    internal sealed class DesktopCaptureLote1C : MonoBehaviour
    {
        public string OutputDirectory;
        private int _fail, _pass, _blackShots;
        private readonly StringBuilder _log = new StringBuilder();
        private string _tag;

        private void Start() { StartCoroutine(Run()); }
        private void Log(string s) { _log.AppendLine(s); Debug.Log("[Lote1C] " + s); }

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
        private static OrbitCameraController Cam => OrbitCameraController.Instance;
        private static TelemetryFrame Frame => DesktopTelemetry.Client?.Displayed;
        private double Alt => Frame != null ? -Frame.p[2] : 0;
        private static string F1(double x) => x.ToString("F1", CultureInfo.InvariantCulture);
        private static double Hyp(double a, double b) => Math.Sqrt(a * a + b * b);

        private readonly HashSet<KeyCode> _held = new HashSet<KeyCode>();
        private readonly Dictionary<KeyCode, int> _pressedAt = new Dictionary<KeyCode, int>();

        private IEnumerator PressKey(KeyCode k, bool shift = false)
        {
            if (shift) _held.Add(KeyCode.LeftShift);
            _pressedAt[k] = Time.frameCount + 1;
            _held.Add(k);
            yield return null; yield return null; yield return null;
            _held.Remove(k);
            if (shift) _held.Remove(KeyCode.LeftShift);
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

        /// <summary>
        /// The active mode and the active language highlight THEMSELVES (own background and border; the old sliding pills measured the layout and ended up as a circle on the
        /// wrong item when the narrow top bar squeezed it): the active item has a visible highlight, no other item of its group has one, and the old pills stay hidden.
        /// </summary>
        private void PillCheck(string when)
        {
            DesktopChrome ch = DesktopUIController.Instance.Chrome;
            foreach (string group in new[] { "mode switch", "language toggle" })
            {
                VisualElement item = group == "mode switch" ? ch.ActiveModeSeg : ch.ActiveLangButton;
                int others = 0;
                if (item != null && item.parent != null)
                    foreach (VisualElement c in item.parent.Children())
                        if (c != item && c.ClassListContains(group == "mode switch" ? "dk-seg-btn" : "dk-lang-btn") && c.resolvedStyle.backgroundColor.a > 0.1f) others++;
                float a = item != null ? item.resolvedStyle.backgroundColor.a : 0f;
                Log("highlight " + group + " (" + when + "): " + (item != null ? item.name + " '" + (item is Button b ? b.text : "") + "'" : "none") + " background alpha " + a.ToString("F2", CultureInfo.InvariantCulture) + ", other items highlighted " + others);
                Check("the " + group + ": the active item is highlighted by itself and no other item is (" + when + ")", item != null && a > 0.1f && others == 0);
            }
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(OutputDirectory);
            PlayerPrefs.SetInt("Onboarding_Seen", 1);
            PlayerPrefs.DeleteKey(DesktopTelemetryUI.PrefSource);
            PlayerPrefs.DeleteKey(DesktopTelemetryUI.PrefGround);
            PlayerPrefs.DeleteKey(SimSettings.PrefFence);
            PlayerPrefs.DeleteKey(SimSettings.PrefWind);
            PlayerPrefs.DeleteKey(DesktopTelemetryUI.PrefMission);
            // a clean slate for what the probe keeps on disk (the probe's own data only)
            foreach (string f in new[] { DesktopTelemetryUI.TrainingFile, DesktopTelemetryUI.CourseRecordsFile }) { try { if (File.Exists(f)) File.Delete(f); } catch { } }
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

            Tel.OpenMode();
            yield return new WaitForSecondsRealtime(0.6f);
            Check("without telemetry training and mission are hidden (not dimmed)", !DesktopUIController.Instance.Chrome.Telemetry.ToolShown(ToolId.Train) && !DesktopUIController.Instance.Chrome.Telemetry.ToolShown(ToolId.Mission) && B("DkTelTraining") != null && !B("DkTelTraining").enabledSelf && B("DkTelMission") != null);
            Check("the third pill of the strip exists with its five buttons (the fit inside the window is checked on the capture: the layout is not measurable this early)", Root.Q("DkTelToolsStrip") != null && B("DkTelMap") != null && B("DkTelPreflight") != null && B("DkTelMission") != null && B("DkTelRecord") != null && B("DkTelTraining") != null);
            PillCheck("no telemetry");
            yield return Shot("01_tira_sin_telemetria", 0.2f);

            Tel.StartSimulator();
            yield return new WaitForSecondsRealtime(2.5f);
            Check("simulator running", DesktopTelemetry.IsSimulator && Sim != null);
            PillCheck("simulator running");

            yield return MissionChecks();
            yield return CourseChecks();
            yield return RecordChecks();
            yield return TrainingChecks();

            DesktopKeyboardGate.KeyHeld = UnityEngine.Input.GetKey;
            DesktopKeyboardGate.KeyPressed = UnityEngine.Input.GetKeyDown;
            Log("black captures: " + _blackShots);
            Check("no capture came out black", _blackShots == 0);
            Log("RESULT " + _pass + " pass, " + _fail + " fail");
            File.WriteAllText(Path.Combine(OutputDirectory, "lote1c_" + _tag + ".log"), _log.ToString());
            Application.Quit();
        }

        // ───────────────────────── 2. mission ─────────────────────────

        private IEnumerator MissionChecks()
        {
            Log("── mission (tool 2) ──");
            Cam.SetAngles(-28f, 60f, true);
            Cam.SetDistance(500f, true);
            yield return PressKey(KeyCode.M, true);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("Shift+M opens the mission popover", Tel.OpenPopover == "mission" && Tel.MissionPopover != null && Tel.MissionPopover.resolvedStyle.display != DisplayStyle.None);
            while (Tel.MissionNow.waypoints.Count > 0) { Tel.MissionSelect(0); if (!Tel.MissionDeleteSelected()) break; } // a mission left by an earlier run
            Tel.SetMissionAdding(true);
            Check("adding mode on", Tel.MissionAdding);
            Tel.MissionAddPoint(20, 0); Tel.MissionAddPoint(20, 20); Tel.MissionAddPoint(0, 20); Tel.MissionAddPoint(0, 0);
            Tel.SetMissionAdding(false);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("four waypoints in the document", Tel.MissionNow.waypoints.Count == 4);
            yield return Shot("10_mision_lista", 0.2f);
            Tel.CloseAllPopovers();

            DesktopTelemetry.Client.Send(ClientMessage.Takeoff(5), out _);
            yield return WaitAlt(5, 20f);
            yield return new WaitForSecondsRealtime(1f);
            Check("the mission runs: Start is accepted", Tel.MissionDo(MissionControl.Start));
            yield return new WaitForSecondsRealtime(4f);
            Check("progress is shown while it flies", Tel.MissionProgressText != null || Tel.MissionBusy);
            yield return Shot("11_mision_en_curso", 0.1f);
            float until = Time.realtimeSinceStartup + 90f;
            while (Time.realtimeSinceStartup < until && Tel.MissionRunNow.phase != MissionPhase.Done && Tel.MissionRunNow.phase != MissionPhase.Failed) yield return null;
            Log("mission end phase " + Tel.MissionRunNow.phase + " at (" + F1(Frame.p[0]) + ", " + F1(Frame.p[1]) + ")");
            Check("the square finishes (phase Done) and the drone is near the last waypoint", Tel.MissionRunNow.phase == MissionPhase.Done && Hyp(Frame.p[0], Frame.p[1]) < 3.0);
            yield return Shot("12_mision_terminada", 0.3f);
            yield return FlyTo(0, 0, 3.5, 20f);
        }

        // ───────────────────────── 5. course ─────────────────────────

        private IEnumerator CourseChecks()
        {
            Log("── course from data and records (tool 5) ──");
            string err;
            CourseLayout bundled = CourseLayout.LoadBundled(out err);
            Check("basic.json is bundled and loads (" + (err ?? "ok") + ")", bundled != null && bundled.Source != CourseLayout.BuiltinSource);
            Click(B("DkTelCourse"));
            yield return new WaitForSecondsRealtime(1.2f);
            Check("the course is placed from the JSON layout (not the C# fallback)", Tel.CourseActive && Tel.CourseScene.Layout.Source != CourseLayout.BuiltinSource);
            Check("the layout is the web's: 4 rings, 3 zones, 5 pylons, wall, 7 checkpoints", Tel.CourseScene.Layout.Gates.Count == 4 && Tel.CourseScene.Layout.Zones.Count == 3 && Tel.CourseScene.Layout.Pylons.Count == 5 && Tel.CourseScene.Layout.Checkpoints.Count == 7);
            var route = Tel.CourseScene.Layout.Route;
            yield return FlyTo(0, 0, 3.5, 20f);
            Tel.ResetCourse();
            Cam.SetAngles(-28f, 32f, true);
            Cam.SetDistance(420f, true);
            for (int i = 1; i < route.Count; i++)
            {
                if (i == 3) yield return FlyTo(32, -2.5, 3.5, 12f); // a deliberate hit on the first pylon
                yield return FlyTo(route[i][0], route[i][1], route[i][2], 25f);
                if (Tel.CourseRunNow.State == CourseState.Finished) break;
            }
            yield return new WaitForSecondsRealtime(1.0f);
            var run = Tel.CourseRunNow;
            Log("lap: " + run.State + " " + run.Reached + "/" + run.Total + " in " + F1(run.FinishTime) + " s, collisions " + run.Collisions);
            Check("the lap finished with at least one collision", run.State == CourseState.Finished && run.Collisions >= 1);
            string result = Tel.CourseResultText ?? string.Empty;
            double pen = run.FinishTime + CourseRecords.PenaltyS * run.Collisions;
            Check("the result shows the time with the penalty (2 s per collision)", result.Contains(T("course.penalised")) && result.Contains(DesktopTelemetryUI.FormatLap(pen)));
            CourseRecord rec = Tel.CourseRecordsNow.Of(run.Layout.Id);
            Check("the records of the course are kept (one attempt, best with penalty set, no clean best)", rec != null && rec.Attempts.Count == 1 && rec.BestPenalised.HasValue && !rec.BestClean.HasValue);
            Check("records.json was written with the web's JSON", File.Exists(DesktopTelemetryUI.CourseRecordsFile) && CourseRecords.Parse(File.ReadAllText(DesktopTelemetryUI.CourseRecordsFile)).Of(run.Layout.Id) != null);
            Cam.SetAngles(-35f, 62f, true);
            Cam.SetDistance(1500f, true);
            yield return Shot("20_circuito_resultado_records", 0.5f);
            Click(B("DkTelCourse"));
            yield return new WaitForSecondsRealtime(0.5f);
        }

        // ───────────────────────── 7. own sessions ─────────────────────────

        private IEnumerator RecordChecks()
        {
            Log("── own sessions (tool 7) ──");
            Cam.ResetView();
            Check("the Record button exists", B("DkTelRecord") != null);
            yield return PressKey(KeyCode.R, true);
            Check("Shift+R starts recording the simulator", Tel.Recording);
            yield return new WaitForSecondsRealtime(0.8f);
            Check("the red chip is visible in the top bar", Tel.RecordChip.resolvedStyle.display != DisplayStyle.None);
            DesktopTelemetry.Client.Send(ClientMessage.Takeoff(4), out _);
            yield return new WaitForSecondsRealtime(6f);
            DesktopTelemetry.Client.Send(ClientMessage.Orbit(8), out _);
            yield return new WaitForSecondsRealtime(6f);
            yield return Shot("30_grabando_chip_rojo", 0.1f);
            int frames = Tel.RecordedFrames;
            Log("recorded frames " + frames);
            Check("about 20 rows per second were taken (>= 200 in ~13 s)", frames >= 200 && frames <= 20 * 20);
            yield return PressKey(KeyCode.R, true);
            yield return new WaitForSecondsRealtime(0.4f);
            string path = Tel.LastSavedSession;
            Check("Shift+R again stops and saves a .twf in the sessions folder", !Tel.Recording && path != null && File.Exists(path) && Path.GetFullPath(Path.GetDirectoryName(path)) == Path.GetFullPath(DesktopTelemetryUI.SessionsFolder));
            Check("the chip is gone", !Tel.RecordChip.ClassListContains("dk-chip--live"));
            TwfFile file = null; string kind = null;
            var fail = TwfWriter.ParseOwn(File.ReadAllBytes(path), out file, out kind);
            Check("the file reads back as an own session of kind sim with the 20 Hz header", fail == TwfWriter.OwnFail.None && kind == "sim" && file.Header.rate_hz == 20 && file.Header.frame_count == Tel.RecordedFrames && file.Header.frame_count >= frames);
            Tel.TogglePopover("record");
            yield return new WaitForSecondsRealtime(1.3f);
            Check("«Mis sesiones» lists the file", Tel.RecordList.childCount >= 1);
            yield return Shot("31_mis_sesiones", 0.1f);
            Tel.CloseAllPopovers();
            Check("opening the file plays it as an own session", Tel.OpenSessionPath(path) && DesktopTelemetry.IsOwnReplay);
            yield return new WaitForSecondsRealtime(2.5f);
            string badge = DesktopTelemetry.BadgeText(Lang);
            Log("badge: " + badge);
            string want;
            bool haveWant = HonestyLabels.Load().TryOwnReplayText("sim", Lang, out want);
            Check("the badge is the approved own-session label", haveWant && badge == want);
            string low = badge.ToLowerInvariant();
            Check("the badge never calls it a shadow or a twin", !low.Contains("sombra") && !low.Contains("shadow") && !low.Contains("gem" + "elo") && !low.Contains("twin"));
            Check("it takes no commands", !DesktopTelemetry.AcceptsCommands && DesktopTelemetry.Client.Hello.commands.Length == 0);
            Tel.ShowFlightTab(true);
            yield return new WaitForSecondsRealtime(0.6f);
            double lt0 = Frame != null ? Frame.lt : 0;
            yield return new WaitForSecondsRealtime(1.5f);
            Check("it plays (the log time advances)", Frame != null && Frame.lt != lt0);
            Check("the source is the own session, not the replay of the real flight", Tel.ActiveSource == TelSource.OwnReplay);
            Check("no reading of the Flight tab says «medido»: every tag is simulated or estimated", Tel.FlightTabActive && Root.Q<VisualElement>("DkFlightPanel").Q<VisualElement>(className: "dk-tag--measured") == null);
            yield return Shot("32_sesion_propia_repeticion", 0.2f);
            Tel.StopSession();
            yield return new WaitForSecondsRealtime(0.5f);
            Check("a file that is not an own session is refused (the bundled flight is a real log)", !Tel.OpenSessionPath(LocalReplayTelemetrySource.BundledPath) && !DesktopTelemetry.IsRunning);
            Tel.StartSimulator();
            yield return new WaitForSecondsRealtime(2.5f);
            yield return FlyTo(0, 0, 4, 20f);
            DesktopTelemetry.Client.Send(ClientMessage.Land(), out _);
            yield return Landed();
        }

        // ───────────────────────── 6. training ─────────────────────────

        private IEnumerator TrainingChecks()
        {
            Log("── guided training (tool 6) ──");
            Check("the training button is enabled with the simulator", B("DkTelTraining").enabledSelf);
            yield return PressKey(KeyCode.T, true);
            yield return new WaitForSecondsRealtime(0.7f);
            Check("Shift+T opens the lesson popover (and the drone did not take off)", Tel.OpenPopover == "training" && Frame.landed);
            Check("the lesson list has the six lessons and the approved disclaimer", Tel.TrainingLessons != null && Tel.TrainingLessons.Count == 6 && Tel.TrainingDisclaimerText == T("training.disclaimer"));
            yield return Shot("40_lecciones", 0.2f);
            Tel.TrainingHintSeconds = 3.0;
            Check("lesson 1 starts", Tel.StartTraining("takeoff-hold", false));
            yield return new WaitForSecondsRealtime(0.5f);
            Check("the card is shown with the objective", Tel.TrainingCard.resolvedStyle.display != DisplayStyle.None && !string.IsNullOrEmpty(Tel.TrainingCardObjective));
            yield return PressKey(KeyCode.M);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("the objective of the take-off step is marked on the map", Tel.MapCanvas != null && Tel.MapCanvas.Target != null && Tel.MapCanvas.Target.kind == "point");
            yield return Shot("41_tarjeta_paso1_mapa", 0.2f);
            yield return new WaitForSecondsRealtime(4f);
            Check("after the idle time the hint is raised (button visible)", Tel.TrainingRun != null && Tel.TrainingRun.hint != null && Tel.TrainingHintButton.resolvedStyle.display != DisplayStyle.None);
            Click(Tel.TrainingHintButton);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("pressing «Pista» shows the hint text", !string.IsNullOrEmpty(Tel.TrainingHintText));
            yield return Shot("42_pista", 0.1f);
            DesktopTelemetry.Client.Send(ClientMessage.Takeoff(3), out _);
            float until = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < until && Tel.TrainingRun.step < 1) yield return null;
            Check("step 1 (take off) is completed from the frames alone", Tel.TrainingRun.step >= 1);
            yield return Shot("43_paso2_mantener", 0.5f);
            until = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < until && !Tel.TrainingRun.done) yield return null;
            Check("the lesson is completed (hold 5 s)", Tel.TrainingRun.done);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("the progress is kept: done with a best time", Tel.TrainingProgressNow.TryGetValue("takeoff-hold", out LessonRecord r) && r.done && r.bestS.HasValue);
            Check("training.json was written with the web's JSON", File.Exists(DesktopTelemetryUI.TrainingFile) && Training.ParseProgress(File.ReadAllText(DesktopTelemetryUI.TrainingFile)).ContainsKey("takeoff-hold"));
            yield return Shot("44_leccion_completada", 0.2f);
            Tel.QuitTraining();
            yield return PressKey(KeyCode.M);
            Tel.TogglePopover("training");
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("45_lecciones_progreso", 0.1f);
            Tel.CloseAllPopovers();
            Tel.StopSession();
            yield return new WaitForSecondsRealtime(0.3f);
        }
    }
}
#endif
