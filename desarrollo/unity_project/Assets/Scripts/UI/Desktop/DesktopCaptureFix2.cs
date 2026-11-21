#if UNITY_STANDALONE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.UI.Panels;
using WebGL.Telemetry;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Verification aid for PLAN_PC 42 (-fix2-capture &lt;folder&gt;, plus -lang es|en, -uiscale and the window flags). In-player probe, no OS mouse or keyboard.
    /// (A) off = only the four source pills; pressing the active pill turns it off. (B) per source, the visible buttons and pills against the table.
    /// (C) the five switches of «Escena» toggle and paint blue, the gear of Geocerca / Viento / Batería opens the fine settings. (D) side windows: beside the
    /// tree / inspector, never over the rows or the centre of the canvas, moved and remembered, reset, Esc. (E) piloting is compact (Mantener and Volver in
    /// sight), «Más» brings the rest. One PNG per state (tag = WxH_lang[_xS]) and PASS / FAIL lines.
    /// </summary>
    internal sealed class DesktopCaptureFix2 : MonoBehaviour
    {
        public string OutputDirectory;
        private int _fail, _pass, _blackShots;
        private readonly StringBuilder _log = new StringBuilder();
        private string _tag;

        private void Start() { StartCoroutine(Run()); }
        private void Log(string s) { _log.AppendLine(s); Debug.Log("[Fix2Probe] " + s); }

        private void Check(string name, bool ok)
        {
            if (ok) _pass++; else _fail++;
            Log((ok ? "PASS " : "FAIL ") + name);
        }

        private VisualElement Root => UIManager.Instance.Root;
        private Button B(string n) => Root.Q<Button>(n);
        private DesktopTelemetryUI Tel => DesktopUIController.Instance.Chrome.Telemetry;
        private static string Lang => AppLanguageManager.IsSpanish ? "es" : "en";
        private static string F1(double x) => x.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

        private static void Click(Button b)
        {
            if (b == null) return;
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
        }

        private static bool Visible(VisualElement e)
        {
            if (e == null) return false;
            for (VisualElement p = e; p != null; p = p.parent)
                if (p.resolvedStyle.display == DisplayStyle.None) return false;
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
            Log("shot " + _tag + "_" + name + " luma " + F1(luma));
            Destroy(use);
        }

        private static float UiScale()
        {
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i].Equals("-uiscale", StringComparison.OrdinalIgnoreCase) && float.TryParse(a[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float s)) return s;
            return 1f;
        }

        /// <summary>The shown buttons of the rows (source pills, gears, sections, «Más» and tools), by name.</summary>
        private List<Button> ShownButtons()
        {
            var l = new List<Button>();
            foreach (Button b in Root.Q<VisualElement>("DkTelemetry").Query<Button>().ToList())
                if (Visible(b) && !(b.parent is VisualElement p && p.ClassListContains("dk-fold-body"))) l.Add(b);
            return l;
        }

        private int CountPills()
        {
            int n = 0;
            foreach (Button b in ShownButtons()) if (b.ClassListContains("dk-srcpill")) n++;
            return n;
        }

        private HashSet<ToolId> ShownTools()
        {
            var s = new HashSet<ToolId>();
            foreach (ToolId id in TelemetryMenu.ToolIds)
            {
                if (id == ToolId.Record) continue;
                Button b = Tel.StripButtonOf(id);
                bool shown = b != null;
                for (VisualElement e = b; e != null && shown; e = e.parent)
                {
                    if (e.name == "DkTelemetry") break;
                    if (e.resolvedStyle.display == DisplayStyle.None && !(e.parent is DesktopFold fold && fold.Body == e)) shown = false; // a closed «Avanzado» counts as shown (one click away)
                }
                if (shown) s.Add(id);
            }
            return s;
        }

        private Vector2 Panel => new Vector2(Root.layout.width, Root.layout.height);

        /// <summary>The rows fit in the window, side by side with the tree and the inspector: no row wider than the room between them, none off screen.</summary>
        private void CheckRowsFit(string st)
        {
            float w = Panel.x;
            bool ok = true;
            string bad = "";
            foreach (VisualElement row in Root.Q<VisualElement>("DkTelemetry").Children())
            {
                if (!Visible(row) || row.worldBound.width < 2f) continue;
                Rect r = row.worldBound;
                if (r.xMin < -0.5f || r.xMax > w + 0.5f) { ok = false; bad += row.name + " " + F1(r.xMin) + ".." + F1(r.xMax) + " "; }
                foreach (Button b in row.Query<Button>().ToList())
                    if (Visible(b) && b.worldBound.width > 1f && (b.worldBound.yMax > r.yMax + 1f || b.worldBound.yMin < r.yMin - 1f)) { ok = false; bad += b.name + " wraps "; }
            }
            Check(st + ": the rows fit in the window (" + (ok ? "ok" : bad) + ")", ok);
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(OutputDirectory);
            PlayerPrefs.SetInt("Onboarding_Seen", 1);
            PlayerPrefs.DeleteKey(DesktopTelemetryUI.PrefGround);
            PlayerPrefs.DeleteKey(SimSettings.PrefFenceOn);
            PlayerPrefs.DeleteKey(SimSettings.PrefWindOn);
            PlayerPrefs.DeleteKey(SimSettings.PrefBatteryReact);
            foreach (string id in DesktopWindow.Ids) DesktopWindow.Forget(id);
            DesktopTelemetryUI.IgnoreFocusLoss = true;
            yield return new WaitForSecondsRealtime(7f);
            AccessibilityManager.Instance?.SetUIScale(UiScale());
            yield return new WaitForSecondsRealtime(0.5f);
            _tag = Screen.width + "x" + Screen.height + "_" + Lang + (UiScale() != 1f ? "_x" + F1(UiScale()) : "");
            UIManager ui = UIManager.Instance;
            ui.PrepareViewerForDeepLink();
            yield return new WaitForSecondsRealtime(3f);
            if (ui.IsOnboardingOpen) { ui.ShortcutBack(); yield return new WaitForSecondsRealtime(1f); }
            Log("window " + Screen.width + "x" + Screen.height + " panel " + F1(Panel.x) + "x" + F1(Panel.y) + " lang " + Lang + " scale " + F1(UiScale()));
            Check("no «last source» preference is kept", !PlayerPrefs.HasKey(DesktopTelemetryUI.PrefSource));

            yield return PartOff();
            yield return PartReplay();
            yield return PartSim();
            yield return PartWindows();
            yield return PartPilot();
            yield return PartSitl();

            DesktopTelemetry.Stop();
            // leave the defaults for the next probes (the switches and the windows are remembered)
            foreach (string k in new[] { SimSettings.PrefFenceOn, SimSettings.PrefWindOn, SimSettings.PrefWind, SimSettings.PrefBatteryReact, DesktopTelemetryUI.PrefGround }) PlayerPrefs.DeleteKey(k);
            foreach (string id in DesktopWindow.Ids) DesktopWindow.Forget(id);
            PlayerPrefs.Save();
            Log("black captures: " + _blackShots);
            Check("no capture came out black", _blackShots == 0);
            Log("RESULT " + _pass + " pass, " + _fail + " fail");
            File.WriteAllText(Path.Combine(OutputDirectory, "fix2_" + _tag + ".log"), _log.ToString());
            Application.Quit();
        }

        // ───────────────────────── (A) off ─────────────────────────

        private IEnumerator PartOff()
        {
            Log("── A: telemetry off ──");
            DesktopTelemetry.Stop();
            Tel.OpenMode();
            yield return new WaitForSecondsRealtime(0.8f);
            List<Button> shown = ShownButtons();
            Check("off: exactly the four source pills and nothing else (" + shown.Count + " buttons)", CountPills() == 4 && shown.Count == 4);
            Check("off: no «Encendido» and no «Fuente» in the tree", B("DkTelPower") == null && B("DkTelSource") == null);
            Check("off: «Suelo», «Alertas», «Fallas», «Grabar» are not offered", !Tel.ToolShown(ToolId.Ground) && !Tel.ToolShown(ToolId.Alerts) && !Tel.ToolShown(ToolId.Faults) && !Tel.ToolShown(ToolId.Sessions));
            Check("off: the «Disponible en» line is hidden", !Visible(Tel.AvailRow));
            Button first = DesktopUIController.Instance.Pad?.PickFirstFocus();
            Check("off: the pad's first focus is a source pill (" + (first != null ? first.name : "none") + ")", first != null && first.ClassListContains("dk-srcpill"));
            CheckRowsFit("off");
            yield return Shot("fix2_apagada");
        }

        // ───────────────────────── (B) replay ─────────────────────────

        private IEnumerator PartReplay()
        {
            Log("── B: local replay ──");
            Click(Tel.SourcePillButton(SourcePill.Replay));
            yield return new WaitForSecondsRealtime(2f);
            Check("the «Repetición local» pill starts the replay", DesktopTelemetry.IsLocalReplay);
            Check("replay: only its own pill, in blue, with the link state", CountPills() == 1 && Tel.SourcePillButton(SourcePill.Replay).ClassListContains("dk-strip-btn--on")
                && !string.IsNullOrEmpty(Tel.SourcePillButton(SourcePill.Replay).Q<Label>(className: "dk-strip-state").text));
            Check("replay: no Fallas, no Grabar; Alertas and Análisis are there", !Tel.ToolShown(ToolId.Faults) && !Tel.ToolShown(ToolId.Sessions) && Tel.ToolShown(ToolId.Alerts) && Tel.ToolShown(ToolId.Analysis));
            Check("replay: the shown tools are the table's", ShownTools().SetEquals(TelemetryMenu.ViewFor(MenuSurface.Replay, Tel.OpenSection, false, false).Tools));
            Tel.SetOpenSection(TSection.Scene);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("replay: «Escena» shows only «Suelo»", Tel.ToolShown(ToolId.Ground) && !Tel.ToolShown(ToolId.Fence) && !Tel.ToolShown(ToolId.Wind));
            CheckRowsFit("replay");
            yield return Shot("fix2_repeticion");
            Click(Tel.SourcePillButton(SourcePill.Replay));
            yield return new WaitForSecondsRealtime(0.8f);
            Check("pressing the active pill turns the telemetry off (four pills again)", !DesktopTelemetry.IsRunning && CountPills() == 4);
            Click(Tel.SourcePillButton(SourcePill.Own));
            yield return new WaitForSecondsRealtime(0.6f);
            VisualElement rec = Tel.OpenPopoverElement;
            Check("«Mis sesiones» opens the list of own sessions in a side window", Tel.OpenPopover == "record" && Visible(rec) && rec.ClassListContains("dk-win"));
            Check("«Mis sesiones»: the record switch is not offered with the telemetry off", !Visible(B("DkRecToggle")));
            yield return Shot("fix2_mis_sesiones");
            Tel.CloseAllPopovers();
        }

        // ───────────────────────── (C) simulator switches ─────────────────────────

        private IEnumerator PartSim()
        {
            Log("── C: simulator ──");
            Click(Tel.SourcePillButton(SourcePill.Sim));
            yield return new WaitForSecondsRealtime(2f);
            Check("the «Simulador» pill starts the simulator", DesktopTelemetry.IsSimulator);
            Check("simulator: «Mando» open by default, Mantener and Volver in sight", Tel.OpenSection == TSection.Cmd && Tel.ToolShown(ToolId.Hold) && Tel.ToolShown(ToolId.Rtl));
            Check("simulator: Fallas is there (the model reacts)", Tel.ToolShown(ToolId.Faults));
            Button first = DesktopUIController.Instance.Pad?.PickFirstFocus();
            Check("simulator: the pad's first focus is the active pill (" + (first != null ? first.name : "none") + ")", first == Tel.SourcePillButton(SourcePill.Sim));
            CheckRowsFit("simulator [Mando]");
            yield return Shot("fix2_simulador_mando");
            Click(Tel.SectionButton(TSection.Scene));
            yield return new WaitForSecondsRealtime(0.4f);
            Check("one section at a time: «Escena» replaces «Mando»", Tel.OpenSection == TSection.Scene && !Tel.ToolShown(ToolId.Takeoff) && Tel.ToolShown(ToolId.Fence));
            var sim = DesktopTelemetry.Source as SimTelemetrySource;

            // every switch: press = flips and paints blue (dk-strip-btn--on), press again = back
            (string name, Func<bool> state)[] sw =
            {
                ("DkTelGround", () => Tel.GroundOn),
                ("DkTelCourse", () => Tel.CourseOn),
                ("DkTelFence", () => sim.Model.cfg.fenceOn),
                ("DkTelWind", () => Tel.WindOn && (sim.Model.cfg.wind.speed > 0 || sim.Model.cfg.wind.gustSigma > 0)),
                ("DkTelBatteryReact", () => sim.Model.cfg.battery.react),
            };
            foreach (var s in sw)
            {
                Button b = B(s.name);
                bool before = s.state();
                Click(b); yield return new WaitForSecondsRealtime(0.35f);
                bool mid = s.state();
                bool blueMid = b.ClassListContains("dk-strip-btn--on");
                Click(b); yield return new WaitForSecondsRealtime(0.35f);
                bool after = s.state();
                Check(s.name + ": a switch (" + before + " -> " + mid + " -> " + after + "), blue exactly when on", mid != before && after == before && blueMid == mid && b.ClassListContains("dk-strip-btn--on") == after);
            }
            // all on, then the gears
            foreach (var s in sw) if (!s.state()) { Click(B(s.name)); yield return new WaitForSecondsRealtime(0.3f); }
            bool allOn = true;
            foreach (var s in sw) allOn &= s.state() && B(s.name).ClassListContains("dk-strip-btn--on");
            Check("all five on and blue", allOn);
            Check("gears of Geocerca, Viento and Batería shown while on", Visible(Tel.FenceGear) && Visible(Tel.WindGear) && Visible(Tel.BatteryGear));
            CheckRowsFit("simulator [Escena, all on]");
            yield return Shot("fix2_simulador_escena_activos");
            (Button gear, string pop)[] gears = { (Tel.FenceGear, "fence"), (Tel.WindGear, "wind"), (Tel.BatteryGear, "batteryreact") };
            foreach (var g in gears)
            {
                Click(g.gear); yield return new WaitForSecondsRealtime(0.5f);
                VisualElement w = Tel.OpenPopoverElement;
                Check("gear " + g.gear.name + " opens its settings as a side window", Tel.OpenPopover == g.pop && Visible(w) && w.ClassListContains("dk-win"));
                CheckSide(g.pop, false);
                if (g.pop == "fence") yield return Shot("fix2_geocerca_ajustes");
                Tel.CloseAllPopovers();
            }
            Click(B("DkTelFence")); yield return new WaitForSecondsRealtime(0.3f);
            Check("Geocerca off: its gear hides and the model's fence stops acting", !Visible(Tel.FenceGear) && !sim.Model.cfg.fenceOn && sim.Model.cfg.FenceRadius == Geofence.RadiusMax);
            Click(B("DkTelFence")); yield return new WaitForSecondsRealtime(0.3f);
            Click(B("DkTelCourse")); yield return new WaitForSecondsRealtime(0.3f); // the course off again (it places a scene)
        }

        /// <summary>A side window: next to the left side (tree) or the right one (inspector), below the rows, not over the centre of the canvas.</summary>
        private void CheckSide(string key, bool right)
        {
            DesktopWindowDrag w = Tel.WindowOf(key);
            VisualElement win = w?.Window;
            if (win == null) { Check(key + ": window exists", false); return; }
            Rect r = win.worldBound;
            float rowsBottom = 0f;
            foreach (VisualElement row in Root.Q<VisualElement>("DkTelemetry").Children()) if (Visible(row) && row.worldBound.height > 1f) rowsBottom = Mathf.Max(rowsBottom, row.worldBound.yMax);
            var chrome = DesktopUIController.Instance.Chrome;
            float cx = (chrome.LeftOccupied + Panel.x - chrome.RightOccupied) * 0.5f;
            bool side = right ? r.xMax <= Panel.x - chrome.RightOccupied + 1f : r.xMin >= chrome.LeftOccupied - 1f;
            Check(key + ": a side window (" + F1(r.xMin) + ".." + F1(r.xMax) + " x " + F1(r.yMin) + ".." + F1(r.yMax) + "), below the rows (" + F1(rowsBottom) + "), not over the centre (" + F1(cx) + ")",
                side && r.yMin >= rowsBottom - 1f && !(r.xMin < cx && r.xMax > cx) && r.yMax <= Panel.y + 1f);
        }

        // ───────────────────────── (D) windows ─────────────────────────

        private IEnumerator PartWindows()
        {
            Log("── D: side windows ──");
            Tel.SetOpenSection(TSection.Tools);
            yield return new WaitForSecondsRealtime(0.3f);
            Click(B("DkTelMission")); yield return new WaitForSecondsRealtime(0.6f);
            Check("Misión opens as a side window", Tel.OpenPopover == "mission" && Tel.OpenPopoverElement.ClassListContains("dk-win"));
            CheckSide("mission", false);
            DesktopWindowDrag w = Tel.WindowOf("mission");
            Vector2 def = w.Position;
            w.MoveTo(new Vector2(def.x + 260f, def.y + 40f));
            yield return new WaitForSecondsRealtime(0.3f);
            Check("moved: the place is remembered in Desk_Win_mission", DesktopWindow.TryLoad("mission", out Vector2 saved) && Mathf.Abs(saved.x - (def.x + 260f)) < 1.5f);
            yield return Shot("fix2_ventana_movida");
            Tel.HandleBack();
            yield return new WaitForSecondsRealtime(0.3f);
            Check("Esc closes the window (the tab stays)", Tel.OpenPopover == null && Tel.ModeOpen);
            Click(B("DkTelMission")); yield return new WaitForSecondsRealtime(0.6f);
            Check("reopened where it was left", (w.Position - saved).magnitude < 1.5f);
            w.MoveTo(new Vector2(99999f, 99999f));
            yield return new WaitForSecondsRealtime(0.2f);
            Rect r = w.Window.worldBound;
            Check("dragged off screen: kept inside the window (" + F1(r.xMax) + " <= " + F1(Panel.x) + ")", r.xMax <= Panel.x + 1f && r.yMax <= Panel.y + 1f);
            w.ResetPlace();
            yield return new WaitForSecondsRealtime(0.2f);
            Check("double click on the title bar: back to its place, forgotten", !DesktopWindow.TryLoad("mission", out _) && (w.Position - def).magnitude < 1.5f);
            w.MoveTo(new Vector2(def.x + 100f, def.y + 20f));
            DesktopUIController.Instance.Chrome.OpenSettings();
            yield return new WaitForSecondsRealtime(0.4f);
            Button reset = B("DkWinReset");
            Check("Ajustes has «Restablecer ventanas»", Visible(reset) && !string.IsNullOrEmpty(reset.text));
            Click(reset);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("«Restablecer ventanas» forgets every place", !DesktopWindow.TryLoad("mission", out _) && (w.Position - def).magnitude < 1.5f);
            DesktopUIController.Instance.Chrome.CloseSettings();
            Tel.CloseAllPopovers();
            Click(B("DkTelAlerts")); yield return new WaitForSecondsRealtime(0.5f);
            Check("Alertas opens as a side window", Tel.OpenPopover == "alerts");
            CheckSide("alerts", true);
            yield return Shot("fix2_alertas_derecha");
            Tel.CloseAllPopovers();
            foreach (string k in new[] { "preflight", "record", "training" })
            {
                Tel.TogglePopover(k); yield return new WaitForSecondsRealtime(0.5f);
                CheckSide(k, false);
                Tel.CloseAllPopovers();
            }
        }

        // ───────────────────────── (E) piloting ─────────────────────────

        private IEnumerator PartPilot()
        {
            Log("── E: piloting ──");
            Tel.SetOpenSection(TSection.Tools);
            Click(B("DkTelPilot"));
            yield return new WaitForSecondsRealtime(0.6f);
            Check("piloting on", Tel.Piloting);
            var want = new HashSet<ToolId>(TelemetryMenu.ViewFor(MenuSurface.Sim, Tel.OpenSection, true, false).Tools);
            Check("piloting: compact, the shown tools are the table's (" + ShownTools().Count + " of " + want.Count + ")", ShownTools().SetEquals(want));
            Check("piloting: Despegar, Aterrizar, Mantener, Volver in sight", Tel.ToolShown(ToolId.Takeoff) && Tel.ToolShown(ToolId.Land) && Tel.ToolShown(ToolId.Hold) && Tel.ToolShown(ToolId.Rtl));
            Check("piloting: sections folded away behind «Más»", Visible(Tel.MoreButton) && !Visible(Tel.SectionButton(TSection.Scene)) && !Tel.ToolShown(ToolId.Map));
            int rows = 0;
            foreach (VisualElement row in Root.Q<VisualElement>("DkTelemetry").Children()) if (Visible(row) && row.ClassListContains("dk-strip")) rows++;
            Check("piloting: two rows at most (" + rows + ")", rows <= 2);
            CheckRowsFit("piloting");
            yield return Shot("fix2_pilotando_compacto");
            Click(Tel.MoreButton);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("«Más»: the sections come back, Mantener and Volver stay", Visible(Tel.SectionButton(TSection.Scene)) && Tel.ToolShown(ToolId.Hold) && Tel.ToolShown(ToolId.Rtl) && Tel.ToolShown(ToolId.Map));
            CheckRowsFit("piloting + Más");
            yield return Shot("fix2_pilotando_mas");
            Tel.HandleBack();
            yield return new WaitForSecondsRealtime(0.4f);
            Check("Esc leaves piloting and the full rows come back", !Tel.Piloting && Visible(Tel.SectionButton(TSection.Cmd)) && !Visible(Tel.MoreButton));
        }

        // ───────────────────────── SITL (manual source) ─────────────────────────

        private IEnumerator PartSitl()
        {
            Log("── B: PX4 SITL (manual source) ──");
            DesktopTelemetry.Stop();
            yield return new WaitForSecondsRealtime(0.4f);
            var src = new ManualTelemetrySource(SourceClassification.Sitl, false);
            DesktopTelemetry.Start(src);
            HelloMessage hello = new SimTelemetrySource().BuildHello(ReplaySchedule.WallClockMs());
            hello.commands = new[] { "takeoff", "set_altitude", "land", "fly_to", "yaw", "orbit", "rtl", "hold", "battery_off", "pilot" };
            src.Push(hello);
            var flying = FlightSim.Create();
            for (int i = 0; i < 8; i++) { src.Push(new FramesMessage { frames = new[] { FlightSim.Frame(flying, ReplaySchedule.WallClockMs()) } }); yield return new WaitForSecondsRealtime(0.1f); }
            yield return new WaitForSecondsRealtime(0.8f);
            Check("SITL: only its pill, with «Gemelo digital SITL» on its tooltip", CountPills() == 1 && Visible(Tel.SourcePillButton(SourcePill.Sitl))
                && Tel.SourcePillButton(SourcePill.Sitl).tooltip.Contains(HonestyLabels.Load().ClassificationTitle(SourceClassification.Sitl, Lang)));
            Check("SITL: the shown tools are the table's", ShownTools().SetEquals(TelemetryMenu.ViewFor(MenuSurface.Sitl, Tel.OpenSection, false, false).Tools));
            Check("SITL: Fallas and Grabar exist, the simulator's switches do not", TelemetryMenu.Has(ToolId.Faults, MenuSurface.Sitl) && Tel.ToolShown(ToolId.Faults) && !Tel.ToolShown(ToolId.Fence));
            CheckRowsFit("sitl");
            yield return Shot("fix2_px4_sitl");
            Click(Tel.SourceGear);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("the gear of the active source opens «Fuente» (URL of the bridge) as a side window", Tel.OpenPopover == "source" && Visible(Root.Q<VisualElement>("DkSitlBlock")));
            CheckSide("source", false);
            yield return Shot("fix2_fuente_sitl");
            Tel.CloseAllPopovers();
        }
    }
}
#endif
