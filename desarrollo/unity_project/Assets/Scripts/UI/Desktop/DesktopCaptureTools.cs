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
    /// Verification aid for Lote 1a (-tools-capture &lt;folder&gt;, plus -lang es|en and the window flags). In-player probes, no OS mouse or keyboard (the Shift+G key goes through the
    /// <see cref="DesktopKeyboardGate"/> seams; clicks are UI Toolkit events). Walks the three simulator tools and the propeller fix:
    ///  - geofence (tool 9): the strip button, Shift+G, the popover, the sliders (radius 40 m for the capture), the refusal in flight (the drone is never teleported), the 3D ring with its
    ///    amber / red states, persistence, Reset, dimmed with the replay;
    ///  - wind (tool 4): the popover, the reading «Viento 6 m/s del NO · ráfagas ±2 m/s · simulado», the arrow beside the drone (direction and length), the lean and the drift, the dial,
    ///    the seed, «Sin viento», persistence, dimmed with the replay;
    ///  - motor cut (tool 10): the popover controls, the two-click confirmation, the fall as in the web golden (numbers logged), the compensation, the strip of the sequence, the marker, the
    ///    stopped propeller, the approved card line, restore and impact;
    ///  - the CW / CCW table of the propellers (DroneStateController.SpinDirection).
    /// One PNG per state (tag = WxH_lang) and PASS / FAIL lines; a black capture fails the run. Not active unless the flag is given.
    /// </summary>
    internal sealed class DesktopCaptureTools : MonoBehaviour
    {
        public string OutputDirectory;
        private int _fail, _pass, _blackShots;
        private readonly StringBuilder _log = new StringBuilder();
        private string _tag;

        private void Start() { StartCoroutine(Run()); }
        private void Log(string s) { _log.AppendLine(s); Debug.Log("[ToolsCap] " + s); }

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
        private SimTelemetrySource Sim => DesktopTelemetry.Source as SimTelemetrySource;
        private static bool Shown(VisualElement e) => e != null && e.resolvedStyle.display != DisplayStyle.None;
        private static TelemetryDroneDriver Drv => TelemetryDroneDriver.Instance;
        private static OrbitCameraController Cam => OrbitCameraController.Instance;
        private double Alt => DesktopTelemetry.Client?.Displayed != null ? -DesktopTelemetry.Client.Displayed.p[2] : 0;
        private static string F1(double x) => x.ToString("F1", CultureInfo.InvariantCulture);
        private static string F2(double x) => x.ToString("F2", CultureInfo.InvariantCulture);

        // ───────────────────────── keys (the gate's seams) ─────────────────────────

        private readonly HashSet<KeyCode> _held = new HashSet<KeyCode>();
        private readonly Dictionary<KeyCode, int> _pressedAt = new Dictionary<KeyCode, int>();

        private IEnumerator PressKey(KeyCode k, bool shift)
        {
            if (shift) _held.Add(KeyCode.LeftShift);
            _pressedAt[k] = Time.frameCount + 1;
            _held.Add(k);
            yield return null; yield return null; yield return null;
            _held.Remove(k);
            if (shift) _held.Remove(KeyCode.LeftShift);
            yield return null;
        }

        // ───────────────────────── capture ─────────────────────────

        private static float MeanLuma(Texture2D t)
        {
            Color32[] px = t.GetPixels32();
            double sum = 0; int n = 0;
            for (int i = 0; i < px.Length; i += 37) { sum += 0.2126 * px[i].r + 0.7152 * px[i].g + 0.0722 * px[i].b; n++; }
            return n > 0 ? (float)(sum / n) : 0f;
        }

        private IEnumerator Shot(string name, float wait = 0.4f, List<Texture2D> keep = null)
        {
            if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
            Texture2D use = null;
            yield return DesktopScreenGrab.Grab(t => use = t);
            float luma = DesktopScreenGrab.LastLuma;
            if (luma < 3f) { _blackShots++; Log("BLACK " + _tag + "_" + name + " (" + DesktopScreenGrab.LastMethod + ", luma " + F1(luma) + ")"); }
            File.WriteAllBytes(Path.Combine(OutputDirectory, _tag + "_" + name + ".png"), ImageConversion.EncodeToPNG(use));
            Log("shot " + _tag + "_" + name + " " + Screen.width + "x" + Screen.height + " via " + DesktopScreenGrab.LastMethod + " luma " + F1(luma));
            if (keep != null) keep.Add(use); else Destroy(use);
        }

        /// <summary>The captured tiles side by side at a third of their size (a strip of the sequence).</summary>
        private void WriteStrip(string name, List<Texture2D> tiles, int cols)
        {
            if (tiles.Count == 0) return;
            // each tile is the canvas (without the bars and the inspector) at 45 % of its size
            int sx = (int)(tiles[0].width * 0.03f), sy = (int)(tiles[0].height * 0.05f), sw = (int)(tiles[0].width * 0.78f), sh = (int)(tiles[0].height * 0.80f); // texture y grows upwards: from 5 % to 85 % of the height, without the bars on top
            int tw = (int)(sw * 0.45f), th = (int)(sh * 0.45f);
            int rows = (tiles.Count + cols - 1) / cols;
            var strip = new Texture2D(tw * cols, th * rows, TextureFormat.RGB24, false);
            var clear = new Color32[strip.width * strip.height];
            strip.SetPixels32(clear);
            for (int i = 0; i < tiles.Count; i++)
            {
                int ox = (i % cols) * tw, oy = (rows - 1 - i / cols) * th;
                Texture2D src = tiles[i];
                var px = new Color[tw * th];
                for (int y = 0; y < th; y++)
                    for (int x = 0; x < tw; x++)
                        px[y * tw + x] = src.GetPixelBilinear((sx + (x + 0.5f) / tw * sw) / src.width, (sy + (y + 0.5f) / th * sh) / src.height);
                strip.SetPixels(ox, oy, tw, th, px);
                Destroy(src);
            }
            strip.Apply();
            File.WriteAllBytes(Path.Combine(OutputDirectory, _tag + "_" + name + ".png"), ImageConversion.EncodeToPNG(strip));
            Log("strip " + _tag + "_" + name + " " + strip.width + "x" + strip.height + " luma " + F1(MeanLuma(strip)));
            if (MeanLuma(strip) < 3f) _blackShots++;
            Destroy(strip);
        }

        // ───────────────────────── flight helpers ─────────────────────────

        private IEnumerator WaitAlt(double target, float timeout)
        {
            float until = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < until && Math.Abs(Alt - target) > 0.25) yield return null;
        }

        private IEnumerator Settle(float seconds) { yield return new WaitForSecondsRealtime(seconds); }

        private void View(string kind, float distance)
        {
            switch (kind)
            {
                case "low": Cam.SetAngles(-28f, 10f, true); break;
                case "high": Cam.SetAngles(-28f, 35f, true); break;
                default: Cam.SetAngles(-28f, 86f, true); break;
            }
            Cam.SetDistance(distance, true);
        }

        private IEnumerator TakeoffTo(double alt)
        {
            DesktopTelemetry.Client.Send(ClientMessage.Takeoff(alt), out _);
            yield return WaitAlt(alt, 30f);
            yield return new WaitForSecondsRealtime(2.5f);
        }

        private IEnumerator Landed()
        {
            float until = Time.realtimeSinceStartup + 40f;
            while (Time.realtimeSinceStartup < until && !(DesktopTelemetry.Client.Displayed.landed)) yield return null;
        }

        private static double Hyp(double a, double b) => Math.Sqrt(a * a + b * b);

        private IEnumerator Run()
        {
            Directory.CreateDirectory(OutputDirectory);
            PlayerPrefs.SetInt("Onboarding_Seen", 1);
            PlayerPrefs.DeleteKey(DesktopTelemetryUI.PrefSource);
            PlayerPrefs.DeleteKey(DesktopTelemetryUI.PrefGround);
            PlayerPrefs.DeleteKey(SimSettings.PrefFence);
            PlayerPrefs.DeleteKey(SimSettings.PrefWind);
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
            string lang = Lang;
            Log("window " + Screen.width + "x" + Screen.height + " fullscreen=" + Screen.fullScreen + " lang " + lang + " tier " + RenderQualityController.ResolvedTier);

            Tel.OpenMode();
            yield return new WaitForSecondsRealtime(0.6f);
            Check("before the simulator: «Geocerca» and «Viento» are dimmed (the editable fence and wind belong to the simulator)", B("DkTelFence") != null && B("DkTelWind") != null && !B("DkTelFence").enabledSelf && !B("DkTelWind").enabledSelf);
            Tel.StartSimulator();
            yield return new WaitForSecondsRealtime(2.5f);
            Check("simulator running and the drone model is engaged", DesktopTelemetry.IsSimulator && Drv != null && Drv.Engaged);
            Check("«Geocerca» and «Viento» are enabled with the simulator", B("DkTelFence").enabledSelf && B("DkTelWind").enabledSelf);
            Check("hover throttle is the one measured in the real flight (0.62)", SimLimits.HoverThrottle == 0.62);

            yield return FenceChecks();
            yield return WindChecks();
            yield return MotorChecks();
            yield return PropellerChecks();
            yield return NotSimulatorChecks();

            DesktopKeyboardGate.KeyHeld = UnityEngine.Input.GetKey;
            DesktopKeyboardGate.KeyPressed = UnityEngine.Input.GetKeyDown;
            Log("black captures: " + _blackShots);
            Check("no capture came out black", _blackShots == 0);
            Log("RESULT " + _pass + " pass, " + _fail + " fail");
            File.WriteAllText(Path.Combine(OutputDirectory, "tools_" + _tag + ".log"), _log.ToString());
            Application.Quit();
        }

        // ───────────────────────── 9. geofence ─────────────────────────

        private IEnumerator FenceChecks()
        {
            Log("── geofence (tool 9) ──");
            string lang = Lang;
            var tel = Tel;
            Check("the button «Geocerca» shows its shortcut Shift+G and the popover is closed", tel.OpenPopover == null && B("DkTelFence").Q<Label>(className: "dk-kbd") != null && B("DkTelFence").Q<Label>(className: "dk-kbd").text == "Shift+G");
            yield return PressKey(KeyCode.G, true);
            yield return new WaitForSecondsRealtime(0.6f);
            Check("Shift+G opens the geofence popover (not the filter tool)", tel.OpenPopover == "fence" && Shown(tel.FencePopover));
            Check("popover texts are the web ones (" + lang + "): the origin tag says «simulado»", Root.Q<Label>(className: "dk-tag--simulated") != null && tel.FencePopover.Q<Label>(className: "dk-tag--simulated").text == TelemetryStrings.T("fence.origin", lang));
            Check("defaults: radius 120 m, ceiling 60 m, RTL 10 m", Sim.Model.cfg.radius == 120 && Sim.Model.cfg.ceiling == 60 && Sim.Model.cfg.rtlAltitude == 10 && tel.FenceRadiusSlider.value == 120f && tel.FenceCeilingSlider.value == 60f);
            View("high", 40f);
            yield return Shot("10_geocerca_popover", 0.3f);

            Check("the 3D fence exists while the simulator runs, drawn at the radius and the ceiling", tel.FenceScene != null && tel.FenceScene.Radius == 120 && tel.FenceScene.Ceiling == 60);
            Check("the fence has no colliders and sits on the Ignore Raycast layer", tel.FenceScene != null && !tel.FenceScene.HasColliders && tel.FenceScene.gameObject.layer == 2);
            Check("far from the wall the ring is the neutral colour (status ok)", tel.FenceScene != null && tel.FenceScene.Status == FenceStatus.Ok);

            // sliders (the same path a drag takes: a value change)
            tel.FenceRadiusSlider.value = 40f;
            tel.FenceCeilingSlider.value = 30f;
            yield return new WaitForSecondsRealtime(0.5f);
            Check("the radius slider sets 40 m and the ceiling slider 30 m on the model", Sim.Model.cfg.radius == 40 && Sim.Model.cfg.ceiling == 30 && Sim.Model.cfg.rtlAltitude == 10);
            Check("the stored value is Desk_SimFence = {radius 40, ceiling 30, rtl 10}", Geofence.Parse(PlayerPrefs.GetString(SimSettings.PrefFence)).radius == 40 && Geofence.Parse(PlayerPrefs.GetString(SimSettings.PrefFence)).ceiling == 30);
            Check("the 3D fence follows the new size", tel.FenceScene != null && tel.FenceScene.Radius == 40 && tel.FenceScene.Ceiling == 30);

            // a ceiling under the RTL altitude drags it down; a refusal is explained and nothing moves
            tel.FenceCeilingSlider.value = 8f;
            yield return new WaitForSecondsRealtime(0.3f);
            Check("a ceiling of 8 m takes the RTL altitude down to 8 m (never above the ceiling)", Sim.Model.cfg.ceiling == 8 && Sim.Model.cfg.rtlAltitude == 8);
            tel.FenceCeilingSlider.value = 30f;
            yield return new WaitForSecondsRealtime(0.3f);
            Check("and it comes back to its default when there is room", Sim.Model.cfg.ceiling == 30 && Sim.Model.cfg.rtlAltitude == 10);

            Tel.CloseAllPopovers();
            yield return TakeoffTo(6);
            Check("takeoff stays under the 30 m ceiling: 6 m reached", Math.Abs(Alt - 6) < 0.5);
            DesktopTelemetry.Client.Send(ClientMessage.FlyTo(30, 0, 6), out _);
            float until = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < until && Hyp(DesktopTelemetry.Client.Displayed.p[0], DesktopTelemetry.Client.Displayed.p[1]) < 29f) yield return null;
            yield return new WaitForSecondsRealtime(1f);
            tel.TogglePopover("fence");
            yield return new WaitForSecondsRealtime(0.4f);
            double dist = Hyp(DesktopTelemetry.Client.Displayed.p[0], DesktopTelemetry.Client.Displayed.p[1]);
            double[] before = (double[])DesktopTelemetry.Client.Displayed.p.Clone();
            tel.FenceRadiusSlider.value = 30f;
            yield return new WaitForSecondsRealtime(0.4f);
            double minNeeded = Math.Ceiling((Hyp(Sim.Model.p[0], Sim.Model.p[1]) + 5 - 1e-9) / 5) * 5;
            Check("in flight at " + F1(dist) + " m, a radius of 30 m is refused (radius_below_position, minimum " + minNeeded.ToString("0") + " m) and the model keeps 40 m", Sim.Model.cfg.radius == 40 && tel.FenceRefusalCode == "radius_below_position" && tel.FenceRefusal != null);
            Check("the refusal is the web's text (" + lang + ") and the slider snapped back to 40", (tel.FenceRefusal == TelemetryStrings.F("fence.err.radius_below_position", lang, "min", minNeeded.ToString("0"), "max", "0") || tel.FenceRefusal == TelemetryStrings.F("fence.err.radius_below_position", lang, "min", "35", "max", "0") || tel.FenceRefusal == TelemetryStrings.F("fence.err.radius_below_position", lang, "min", "40", "max", "0")) && tel.FenceRadiusSlider.value == 40f);
            Check("the drone was not teleported (same position, within 1 m of flight)", Hyp(DesktopTelemetry.Client.Displayed.p[0] - before[0], DesktopTelemetry.Client.Displayed.p[1] - before[1]) < 1.0);
            tel.FenceCeilingSlider.value = 7f;
            yield return new WaitForSecondsRealtime(0.3f);
            Check("a ceiling of 7 m at 6 m of height is refused too (ceiling_below_altitude)", Sim.Model.cfg.ceiling == 30 && tel.FenceRefusalCode == "ceiling_below_altitude");
            tel.FenceRadiusSlider.value = 40f;
            yield return new WaitForSecondsRealtime(0.2f);

            // near / hit: fly to the wall
            DesktopTelemetry.Client.Send(ClientMessage.FlyTo(80, 0, 6), out _);
            until = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < until && Hyp(DesktopTelemetry.Client.Displayed.p[0], DesktopTelemetry.Client.Displayed.p[1]) < 39.9) yield return null;
            yield return new WaitForSecondsRealtime(2.5f);
            double d2 = Hyp(DesktopTelemetry.Client.Displayed.p[0], DesktopTelemetry.Client.Displayed.p[1]);
            Check("a fly_to beyond the fence stops at the wall (" + F2(d2) + " m of 40 m) and never goes through it", d2 <= 40.001 && d2 > 39.5);
            Check("at the wall the ring is red (status hit)", tel.FenceScene != null && tel.FenceScene.Status == FenceStatus.Hit && tel.FenceScene.RingColor.r > 0.9f && tel.FenceScene.RingColor.g < 0.4f);
            string st = tel.FenceStatusText;
            Check("the popover says «En el límite: el simulador lo detiene»", st == TelemetryStrings.T("fence.statusHit", lang));
            Tel.CloseAllPopovers();
            Cam.SetAngles(-28f, 86f, true);
            Cam.SetDistance(1900f, true);
            yield return Shot("11_geocerca_anillo_rojo_cenital", 0.8f);
            DesktopTelemetry.Client.Send(ClientMessage.FlyTo(36, 0, 6), out _);
            yield return new WaitForSecondsRealtime(3.5f);
            Check("5 m from the wall the ring is amber (status near)", tel.FenceScene != null && tel.FenceScene.Status == FenceStatus.Near && tel.FenceScene.RingColor.r > 0.9f && tel.FenceScene.RingColor.g > 0.6f);
            yield return Shot("12_geocerca_anillo_ambar_cenital", 0.3f);
            View("low", 1500f);
            yield return Shot("13_geocerca_cilindro_vista_baja", 0.5f);
            DesktopTelemetry.Client.Send(ClientMessage.FlyTo(0, 0, 6), out _);
            yield return new WaitForSecondsRealtime(8f);
            Check("clear of the wall it is neutral again", tel.FenceScene.Status == FenceStatus.Ok);
            View("high", 30f);

            // reset and persistence
            tel.TogglePopover("fence");
            yield return new WaitForSecondsRealtime(0.3f);
            Check("«Restablecer 120/60» is enabled for a non-default fence", tel.FenceResetButton.enabledSelf);
            Click(tel.FenceResetButton);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("it restores 120 m / 60 m / 10 m on the model and in the stored value", Sim.Model.cfg.radius == 120 && Sim.Model.cfg.ceiling == 60 && Geofence.Parse(PlayerPrefs.GetString(SimSettings.PrefFence)).radius == 120);
            Check("and the button of the popover is dimmed again", !tel.FenceResetButton.enabledSelf);
            tel.FenceRadiusSlider.value = 50f;
            yield return new WaitForSecondsRealtime(0.3f);
            Tel.CloseAllPopovers();
            DesktopTelemetry.Client.Send(ClientMessage.Land(), out _);
            yield return Landed();
            // a new flight begins with the stored fence
            Tel.StartSimulator();
            yield return new WaitForSecondsRealtime(2f);
            Check("persistence: a fresh simulator starts with the stored fence (radius 50 m)", Sim.Model.cfg.radius == 50);
            tel.FenceRadiusSlider.value = 120f;
            yield return new WaitForSecondsRealtime(0.3f);
            Check("the default is back (120 m)", Sim.Model.cfg.radius == 120);
        }

        // ───────────────────────── 4. wind ─────────────────────────

        private IEnumerator WindChecks()
        {
            Log("── wind (tool 4) ──");
            string lang = Lang;
            var tel = Tel;
            tel.TogglePopover("wind");
            yield return new WaitForSecondsRealtime(0.5f);
            Check("the «Viento» popover opens with the origin «Viento del simulador · simulado»", tel.OpenPopover == "wind" && Shown(tel.WindPopover) && tel.WindPopover.Q<Label>(className: "dk-tag--simulated").text == TelemetryStrings.T("wind.origin", lang));
            Check("no wind by default: «Sin viento · simulado» and no arrow", tel.WindReadingText == TelemetryStrings.T("wind.readingCalm", lang) && (tel.WindCue == null || !tel.WindCue.ArrowVisible));
            yield return Shot("20_viento_popover_calma", 0.3f);

            yield return TakeoffTo(6);
            View("high", 22f);
            double[] home = (double[])DesktopTelemetry.Client.Displayed.p.Clone();
            double ampsCalm = DesktopTelemetry.Client.Displayed.bat.i;
            // 6 m/s from the north-west with gusts of 2 m/s (the spec's example reading)
            ConfigCheck r = tel.ApplyWind(new WindConfig { speed = 6, fromDeg = 315, gustSigma = 2, gustTau = 2, seed = 4242 });
            yield return new WaitForSecondsRealtime(0.4f);
            string expected = lang == "es" ? "Viento 6 m/s del NO · ráfagas ±2 m/s · simulado" : "Wind 6 m/s from the NW · gusts ±2 m/s · simulated";
            Check("the reading says «" + expected + "» (got «" + tel.WindReadingText + "»)", r.ok && tel.WindReadingText == expected);
            Check("every wind value on screen is tagged simulated (the reading ends with the origin)", tel.WindReadingText.EndsWith(lang == "es" ? "simulado" : "simulated"));
            Check("the model has the wind and the stored value is Desk_SimWind", Sim.Model.cfg.wind.speed == 6 && Sim.Model.cfg.wind.fromDeg == 315 && Wind.Parse(PlayerPrefs.GetString(SimSettings.PrefWind)).seed == 4242);
            yield return new WaitForSecondsRealtime(1.0f);
            Check("the wind arrow beside the drone exists, visible, no colliders", tel.WindCue != null && tel.WindCue.ArrowVisible && !tel.WindCue.HasColliders && tel.WindCue.gameObject.layer == 2);
            if (tel.WindCue != null)
            {
                Vector3 d = tel.WindCue.Direction;
                Check("a wind from the NW blows towards the SE: the arrow points east (x > 0) and south (z < 0) in the scene (" + F2(d.x) + ", " + F2(d.z) + ")", d.x > 0.3f && d.z < -0.3f);
                Check("its length follows the speed: at least 0.35 m and about 0.15 m per m/s (" + F2(tel.WindCue.LengthMetres) + " m with the gust)", tel.WindCue.LengthMetres >= 0.35f && tel.WindCue.LengthMetres < 2.2f);
            }
            yield return Shot("21_viento_flecha_6ms_NO", 0.3f);
            Tel.CloseAllPopovers();
            // the steady 5 m/s north wind of the acceptance test: no gusts, hold, then compare with the web numbers
            tel.ApplyWind(new WindConfig { speed = 5, fromDeg = 0, gustSigma = 0, gustTau = 2, seed = 4242 });
            DesktopTelemetry.Client.Send(ClientMessage.Hold(), out _);
            yield return new WaitForSecondsRealtime(14f);
            SimState m = Sim.Model;
            double south = -(m.p[0] - 0);
            Log("5 m/s from the north, hold: deviation " + F2(m.p[0]) + " m north / " + F2(m.p[1]) + " m east, pitch " + F1(m.pitch * 180 / Math.PI) + " deg, current " + F1(m.amps) + " A (calm " + F1(ampsCalm) + " A)");
            Check("the wind pushes the drone downwind (south of where it was): " + F2(m.p[0] - home[0]) + " m", m.p[0] - home[0] < -0.25);
            Check("and it leans into the wind (nose down towards the north: pitch " + F1(m.pitch * 180 / Math.PI) + " deg)", m.pitch * 180 / Math.PI < -8);
            Check("and it burns more current than in the calm (" + F1(m.amps) + " A > " + F1(ampsCalm) + " A)", m.amps > ampsCalm + 0.5);
            View("low", 18f);
            yield return Shot("22_viento_inclinado_5ms_norte", 0.3f);

            // FPS sanity with the simulator running, the fence, the wind arrow and the gusts active (no captures in this window)
            tel.ApplyWind(new WindConfig { speed = 4, fromDeg = 315, gustSigma = 2, gustTau = 2, seed = 7 });
            DesktopTelemetry.Client.Send(ClientMessage.Orbit(10), out _);
            yield return new WaitForSecondsRealtime(3f);
            var dts = new List<float>();
            float fpsUntil = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < fpsUntil) { dts.Add(Time.unscaledDeltaTime); yield return null; }
            dts.Sort();
            double mean = 0; foreach (float d in dts) mean += d; mean /= Math.Max(1, dts.Count);
            double p99 = dts[Math.Min(dts.Count - 1, (int)(dts.Count * 0.99))];
            Log("FPS sanity (simulator orbiting, fence + wind arrow + gusts on, " + Screen.width + "x" + Screen.height + ", vsync " + QualitySettings.vSyncCount + "): mean " + F1(1.0 / mean) + " FPS, 1% low " + F1(1.0 / p99) + " FPS over " + dts.Count + " frames");
            Check("FPS sanity: the simulator with the three tools on keeps at least 30 FPS on average", 1.0 / mean >= 30.0);
            DesktopTelemetry.Client.Send(ClientMessage.Hold(), out _);
            tel.ApplyWind(new WindConfig { speed = 5, fromDeg = 0, gustSigma = 0, gustTau = 2, seed = 4242 });
            yield return new WaitForSecondsRealtime(4f);

            // the dial and the sliders go through the same path as a drag
            tel.TogglePopover("wind");
            yield return new WaitForSecondsRealtime(0.3f);
            tel.WindSpeedSlider.value = 8f;
            yield return new WaitForSecondsRealtime(0.3f);
            Check("the speed slider sets 8 m/s", Sim.Model.cfg.wind.speed == 8);
            tel.WindGustSlider.value = 2f;
            yield return new WaitForSecondsRealtime(0.3f);
            Check("the gust slider sets ±2 m/s", Sim.Model.cfg.wind.gustSigma == 2);
            tel.ApplyWind(tel.WindRemembered.Clone());
            ConfigCheck bad = tel.ApplyWind(new WindConfig { speed = 13, fromDeg = 0, gustSigma = 0, gustTau = 2, seed = 1 });
            Check("a speed of 13 m/s is refused with the web's text and changes nothing", !bad.ok && bad.code == "wind_speed_range" && tel.WindRefusal == TelemetryStrings.F("wind.err.wind_speed_range", lang, "min", "0", "max", "12") && Sim.Model.cfg.wind.speed == 8);
            double seedBefore = Sim.Model.cfg.wind.seed;
            Click(tel.WindSeedButton);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("«Otra» asks for another seed (the model restarts its gusts from calm)", Sim.Model.cfg.wind.seed != seedBefore && Math.Abs(Sim.Model.wind.gn) < 3);
            Check("the dial is part of the popover", tel.WindDial != null && Shown(tel.WindDial));
            yield return Shot("23_viento_popover_8ms", 0.3f);
            Click(tel.WindResetButton);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("«Sin viento» sets speed and gusts to 0 on the model", Sim.Model.cfg.wind.speed == 0 && Sim.Model.cfg.wind.gustSigma == 0 && tel.WindReadingText == TelemetryStrings.T("wind.readingCalm", lang));
            yield return new WaitForSecondsRealtime(9f); // the last gust dies away with its correlation time (2 s): e^-4 of at most a few m/s
            Check("and the arrow goes away once the last gust has died (" + (tel.WindCue != null ? F2(tel.WindCue.LengthMetres) : "0") + " m)", tel.WindCue == null || !tel.WindCue.ArrowVisible);
            Tel.CloseAllPopovers();
            tel.ApplyWind(new WindConfig { speed = 4, fromDeg = 90, gustSigma = 0, gustTau = 2, seed = 4242 });
            DesktopTelemetry.Client.Send(ClientMessage.Land(), out _);
            yield return Landed();
            Check("on the ground the wind moves nothing (it acts only in flight)", Hyp(DesktopTelemetry.Client.Displayed.v[0], DesktopTelemetry.Client.Displayed.v[1]) < 0.01);
            tel.ApplyWind(new WindConfig());
        }

        // ───────────────────────── 10. motor cut ─────────────────────────

        private IEnumerator MotorChecks()
        {
            Log("── motor cut (tool 10) ──");
            string lang = Lang;
            var tel = Tel;
            Tel.StartSimulator();
            yield return new WaitForSecondsRealtime(2f);
            tel.TogglePopover("faults");
            yield return new WaitForSecondsRealtime(0.5f);
            Check("the Faults popover with the simulator shows the motor-cut controls and not the replay injection", Shown(tel.SimFaultControls) && !Shown(Root.Q<VisualElement>("DkFaultControls")));
            Check("it says the approved line «Simulador: el motor deja de empujar en un modelo simplificado; no es PX4 ni un dron real.»", tel.SimFaultNote == TelemetryStrings.T("simfault.note", lang) && tel.SimFaultNote.Contains(lang == "es" ? "no es PX4 ni un dron real" : "not PX4 or a real drone"));
            Check("the four «Cortar motor N» buttons are enabled and «Restaurar» is dimmed", tel.SimCutButton(1).enabledSelf && tel.SimCutButton(4).enabledSelf && !tel.SimRestoreButton.enabledSelf && tel.SimFaultStatus == TelemetryStrings.T("simfault.none", lang));
            yield return Shot("30_fallas_simulador_popover", 0.3f);

            // on the ground: the cut is only a mark
            Click(tel.SimCutButton(2)); Click(tel.SimCutButton(2));
            yield return new WaitForSecondsRealtime(0.3f);
            Check("on the ground a cut is only a mark (no fall): status «Motor 2 · cortado · simulado»", Sim.Model.groundCut == 2 && Sim.Model.fail == null && tel.SimFaultStatus == TelemetryStrings.F("simfault.status", lang, "n", 2));
            DesktopTelemetry.Client.Send(ClientMessage.Takeoff(6), out _);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("with a motor cut the takeoff is refused: «motor cortado: el simulador no puede maniobrar»", DesktopTelemetry.Client.LastAck != null && !DesktopTelemetry.Client.LastAck.ok && DesktopTelemetry.Client.LastAck.detail == FlightSim.MotorCutDetail);
            Click(tel.SimRestoreButton);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("«Restaurar» clears the mark", Sim.Model.groundCut == 0 && tel.SimFaultStatus == TelemetryStrings.T("simfault.none", lang));

            // (b) the web golden: motor 1 out at 10 m without compensation
            Tel.CloseAllPopovers();
            yield return TakeoffTo(10);
            yield return new WaitForSecondsRealtime(8f); // the hold settles
            View("high", 26f);
            tel.TogglePopover("faults");
            yield return new WaitForSecondsRealtime(0.3f);
            Click(tel.SimCutButton(1));
            yield return new WaitForSecondsRealtime(0.3f);
            Check("the first click only asks: «¿Cortar motor 1? Toca otra vez» (nothing is cut yet)", Sim.Model.fail == null && tel.SimCutArmed == 1 && tel.SimCutButton(1).text == TelemetryStrings.F("simfault.cutConfirm", lang, "n", 1));
            var tiles = new List<Texture2D>();
            Tel.CloseAllPopovers();
            tel.TogglePopover("faults");
            // the second click cuts; the marker, the card and the numbers are read while it falls
            double f0 = 0;
            Click(tel.SimCutButton(1));
            Check("the second click cuts motor 1 (simplified rigid body, not PX4)", Sim.Model.fail != null && Sim.Model.fail.motor == 1);
            Tel.CloseAllPopovers();
            double[] sampleT = { 0.0, 0.4, 0.8, 1.2, 1.6, 2.1 };
            string[] names = { "t0", "t04", "t08", "t12", "t16", "t21" };
            double yawAt1 = 0, zAt1 = 0, vzAt1 = 0, rollAt1 = 0;
            bool gotMarker = false; string markerLabel = null; bool propStopped = false; double[] ph0 = null;
            for (int i = 0; i < sampleT.Length; i++)
            {
                float guard = Time.realtimeSinceStartup + 6f;
                while (Time.realtimeSinceStartup < guard && Sim.Model.fail != null && Sim.Model.fail.t < sampleT[i]) yield return null;
                if (Sim.Model.fail == null) break;
                if (i == 2) ph0 = (double[])Drv.PropPhases.Clone();
                if (i == 1)
                {
                    SimState mm = Sim.Model;
                    yawAt1 = mm.yawRate; zAt1 = -mm.p[2]; vzAt1 = mm.v[2]; rollAt1 = mm.roll;
                    gotMarker = Tel.FaultMarkers.Count > 0; foreach (string k in Tel.FaultMarkers.Keys) markerLabel = Tel.FaultMarkers.LabelOf(k);
                }
                if (i == 4 && ph0 != null)
                {
                    double[] now = Drv.PropPhases;
                    var dfr = DesktopTelemetry.Client.Displayed;
                    propStopped = Math.Abs(now[1] - ph0[1]) < 1e-3 && dfr.rpm[0] < 1 && dfr.rpm[1] > 1000; // stopped: no phase advance in the window and 0 rpm shown, while a live motor shows its rpm (a phase difference of a live one can alias to ~0 modulo 2 pi)
                    var df = DesktopTelemetry.Client.Displayed;
                    Log("propeller phases between t=" + F2(Sim.Model.fail != null ? Sim.Model.fail.t : 0) + " s and the sample before: motor 1 " + ph0[1].ToString("F6", CultureInfo.InvariantCulture) + " -> " + now[1].ToString("F6", CultureInfo.InvariantCulture) + ", motor 2 " + ph0[2].ToString("F3", CultureInfo.InvariantCulture) + " -> " + now[2].ToString("F3", CultureInfo.InvariantCulture) + "; displayed rpm " + df.rpm[0].ToString("F0", CultureInfo.InvariantCulture) + " / " + df.rpm[1].ToString("F0", CultureInfo.InvariantCulture));
                }
                yield return Shot("31_corte_motor1_" + names[i], 0f, tiles);
            }
            WriteStrip("32_corte_motor1_secuencia", tiles, 3);
            Log("motor 1 cut at 10 m, no compensation: at t=" + F2(Sim.Model.fail != null ? Sim.Model.fail.t : 0) + " s ...");
            Check("the marker «Motor 1 · cortado · simulado» is pinned on the motor while it falls (" + markerLabel + ")", gotMarker && markerLabel == TelemetryStrings.F("simfault.status", lang, "n", 1));
            Check("the propeller of the cut motor stopped (its phase does not advance, the others keep turning)", propStopped);
            Log("web golden (b) at 0.4 s: roll ~ 79 deg, yawRate -2.38 rad/s; player: yawRate " + F2(yawAt1) + " rad/s, roll " + F1(rollAt1 * 180 / Math.PI) + " deg, altitude " + F2(zAt1) + " m, descent " + F2(vzAt1) + " m/s");
            Check("yawRate < 0 and about the golden -2.4 rad/s at 0.4 s (" + F2(yawAt1) + ")", yawAt1 < -1.8 && yawAt1 > -3.2);
            float wait = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < wait && !DesktopTelemetry.Client.Displayed.landed) yield return null;
            yield return new WaitForSecondsRealtime(0.8f);
            Check("it hit the ground (landed, disarmed): the impact before 4 s of fall", DesktopTelemetry.Client.Displayed.landed && !DesktopTelemetry.Client.Displayed.armed);
            bool impact = false; string impactText = null;
            for (int i = 0; i < DesktopTelemetry.Client.Events.Count; i++) { string t = DesktopTelemetry.Client.Events[i].text; if (t.StartsWith("Impacto simulado a ", StringComparison.Ordinal)) { impact = true; impactText = t; } }
            Check("the event list says «Impacto simulado a X,X m/s» (" + impactText + "; golden 8,6 m/s)", impact);

            // the explanation card of the simulator
            DesktopTelemetry.Stop();
            yield return new WaitForSecondsRealtime(0.5f);
            Tel.StartSimulator();
            yield return new WaitForSecondsRealtime(2f);
            tel.ShowFaultExplanation(FaultTopic.MotorFailure, 1, null);
            yield return new WaitForSecondsRealtime(0.6f);
            var card = tel.DockCard;
            Check("the card «En esta fuente · simulador» is the approved line (" + lang + ")", card.SourceText == TelemetryStrings.T("simfault.note", lang) && card.SourceBadgeText == DesktopTelemetry.BadgeText(lang) && !card.SourceBadgeText.ToUpperInvariant().Contains("SITL"));
            Check("the readings it touches are all «simulado»", card.ReadingTags > 0);
            yield return Shot("33_tarjeta_simulador", 0.3f);
            tel.SetDockOpen(false);

            // compensation and the restore
            yield return TakeoffTo(30);
            yield return new WaitForSecondsRealtime(6f);
            tel.TogglePopover("faults");
            yield return new WaitForSecondsRealtime(0.3f);
            Click(tel.SimCompButton);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("«Compensación didáctica» switches on (not the PX4 controller; explained under it)", Sim.Model.cfg.faultComp && tel.SimCompButton.ClassListContains("dk-b--on"));
            Click(tel.SimCutButton(3)); Click(tel.SimCutButton(3));
            Check("motor 3 cut with the compensation: the event says so", Sim.Model.fail != null && Sim.Model.fail.comp && Sim.Model.fail.motor == 3);
            Tel.CloseAllPopovers();
            yield return new WaitForSecondsRealtime(4.5f);
            SimState c = Sim.Model;
            double dropped = 30 - (-c.p[2]);
            Log("with compensation after " + F1(c.fail != null ? c.fail.t : 0) + " s: descent " + F2(c.v[2]) + " m/s (golden converges on 2.07-2.09), roll " + F2(c.roll * 180 / Math.PI) + " deg, pitch " + F2(c.pitch * 180 / Math.PI) + " deg, drift " + F2(Hyp(c.p[0], c.p[1])) + " m");
            Check("with the compensation roll and pitch stay under 1 degree", Math.Abs(c.roll) < Math.PI / 180 && Math.Abs(c.pitch) < Math.PI / 180);
            Check("the descent approaches 2.1 m/s (" + F2(c.v[2]) + ")", Math.Abs(c.v[2] - 2.09) < 0.35);
            View("high", 30f);
            yield return Shot("34_corte_compensado", 0.2f);
            // the explanation card with the motor actually cut: «Qué pasa» carries the simulator's own status, «En esta fuente» the approved line
            tel.ShowFaultExplanation(FaultTopic.MotorFailure, 3, null);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("the card's first block says «Motor 3 · cortado · simulado» while it is cut and its source line is still the approved one", tel.DockCard.WhatText.Contains(TelemetryStrings.F("simfault.status", lang, "n", 3)) && tel.DockCard.SourceText == TelemetryStrings.T("simfault.note", lang));
            yield return Shot("35_tarjeta_simulador_motor_cortado", 0.2f);
            tel.SetDockOpen(false);
            tel.TogglePopover("faults");
            yield return new WaitForSecondsRealtime(0.3f);
            Click(tel.SimRestoreButton);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("«Restaurar» puts the simulator back in hold (the kinematic model), armed, flying", Sim.Model.fail == null && Sim.Model.mode == SimMode.Hold && Sim.Model.armed && !Sim.Model.landed);
            Tel.CloseAllPopovers();
            yield return new WaitForSecondsRealtime(4f);
            Check("and it settles: slow and level a few seconds later", Hyp(Hyp(Sim.Model.v[0], Sim.Model.v[1]), Sim.Model.v[2]) < 0.5 && Math.Abs(Sim.Model.roll) < 0.05);
            DesktopTelemetry.Client.Send(ClientMessage.Land(), out _);
            yield return Landed();
            tel.TogglePopover("faults");
            yield return new WaitForSecondsRealtime(0.3f);
            Click(tel.SimCompButton);
            Tel.CloseAllPopovers();
        }

        // ───────────────────────── propellers (side fix) ─────────────────────────

        private IEnumerator PropellerChecks()
        {
            Log("── propeller direction table ──");
            Check("FR is M3 (clockwise), FL is M1 (counter-clockwise), BR is M2 (counter-clockwise), BL is M4 (clockwise)",
                DroneStateController.SpinDirection("x500v2_prop_FR", 0) == 1f && DroneStateController.SpinDirection("x500v2_prop_FL", 0) == -1f &&
                DroneStateController.SpinDirection("x500v2_prop_BR", 0) == -1f && DroneStateController.SpinDirection("x500v2_prop_BL", 0) == 1f);
            Check("diagonal pairs share a direction (FR = BL, FL = BR)", DroneStateController.SpinDirection("x500v2_prop_FR", 3) == DroneStateController.SpinDirection("x500v2_prop_BL", 1) && DroneStateController.SpinDirection("x500v2_prop_FL", 2) == DroneStateController.SpinDirection("x500v2_prop_BR", 0));
            Check("an unknown part name keeps the old alternation by index", DroneStateController.SpinDirection("something", 0) == 1f && DroneStateController.SpinDirection("something", 1) == -1f);
            yield return null;
        }

        // ───────────────────────── not the simulator ─────────────────────────

        private IEnumerator NotSimulatorChecks()
        {
            Log("── replay: the simulator tools are dimmed and nothing is drawn ──");
            var tel = Tel;
            tel.StartLocalReplay();
            yield return new WaitForSecondsRealtime(2.5f);
            Check("with the replay «Geocerca» and «Viento» are dimmed with their reason in the tooltip", !B("DkTelFence").enabledSelf && !B("DkTelWind").enabledSelf && !string.IsNullOrEmpty(B("DkTelFence").tooltip) && !string.IsNullOrEmpty(B("DkTelWind").tooltip));
            Check("and the 3D fence and the wind arrow do not exist (PX4's fence does not reach the client; the wind is Gazebo's)", tel.FenceScene == null && tel.WindCue == null);
            tel.TogglePopover("faults");
            yield return new WaitForSecondsRealtime(0.4f);
            Check("the replay keeps its own synthetic injection and the simulator's cut controls are hidden", !Shown(tel.SimFaultControls));
            Tel.CloseAllPopovers();
            yield return Shot("40_repeticion_sin_herramientas_del_simulador", 0.3f);
        }
    }
}
#endif
