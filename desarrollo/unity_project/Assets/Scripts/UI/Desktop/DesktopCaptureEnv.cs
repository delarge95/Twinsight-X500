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
    /// Verification aid for PLAN_PC 28.2 / 28.3 (-env-capture &lt;folder&gt;, plus -lang es|en, the window flags and -ground-legacy for the «before» strips).
    /// In-player probes, no OS mouse or keyboard. Walks: the capture path itself (two methods, black detection), the ground at altitudes 0 / 5 / 20 / 50 m
    /// from a low, a high and a top-down orbit angle and while moving, the toggles (Suelo, quality PC_Low), the click-to-fly ray on the new ground and the
    /// selection layer, and the test course (scripted lap: gates, a deliberate collision, a finished lap, reset, hidden with another source).
    /// One PNG per state (tag = WxH_lang[_legacy]) and PASS / FAIL lines. Not active unless the flag is given.
    /// </summary>
    internal sealed partial class DesktopCaptureEnv : MonoBehaviour
    {
        public string OutputDirectory;
        private int _fail, _pass;
        private readonly StringBuilder _log = new StringBuilder();
        private string _tag;
        private bool _legacy;
        private int _blackShots;

        private void Start() { StartCoroutine(Run()); }
        private void Log(string s) { _log.AppendLine(s); Debug.Log("[EnvCap] " + s); }

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

        // ───────────────────────── capture path ─────────────────────────

        private static float MeanLuma(Texture2D t)
        {
            Color32[] px = t.GetPixels32();
            double sum = 0;
            int n = 0;
            for (int i = 0; i < px.Length; i += 37) { sum += 0.2126 * px[i].r + 0.7152 * px[i].g + 0.0722 * px[i].b; n++; }
            return n > 0 ? (float)(sum / n) : 0f;
        }

        /// <summary>
        /// Waits and writes a PNG through <see cref="DesktopScreenGrab"/>: the back buffer when the desktop presents it, otherwise an offscreen render
        /// of the camera and the UI (the cause of the black 1080p captures of the earlier rounds is in that class). Logs the method and the luma; a shot
        /// that is still black counts as a failure of the run.
        /// </summary>
        private IEnumerator Shot(string name, float wait = 0.4f)
        {
            yield return new WaitForSecondsRealtime(wait);
            Texture2D use = null;
            yield return DesktopScreenGrab.Grab(t => use = t);
            float luma = DesktopScreenGrab.LastLuma;
            if (luma < 3f) { _blackShots++; Log("BLACK " + _tag + "_" + name + " (" + DesktopScreenGrab.LastMethod + ", luma " + luma.ToString("F1", CultureInfo.InvariantCulture) + ")"); }
            File.WriteAllBytes(Path.Combine(OutputDirectory, _tag + "_" + name + ".png"), ImageConversion.EncodeToPNG(use));
            Log("shot " + _tag + "_" + name + " " + Screen.width + "x" + Screen.height + " " + Screen.fullScreenMode + " via " + DesktopScreenGrab.LastMethod + " luma " + luma.ToString("F1", CultureInfo.InvariantCulture));
            LastGroundFraction = GroundFraction(use);
            Destroy(use);
        }

        private float LastGroundFraction;

        /// <summary>Fraction of the central region of the canvas (clear of the bars and rails) whose pixels are clearly lighter than the studio's black sky: with the world ground in top-down view it is ~1, with the old finite grid it is small.</summary>
        private static float GroundFraction(Texture2D t)
        {
            int x0 = (int)(t.width * 0.22f), x1 = (int)(t.width * 0.78f), y0 = (int)(t.height * 0.10f), y1 = (int)(t.height * 0.88f);
            int n = 0, lit = 0;
            for (int y = y0; y < y1; y += 6)
                for (int x = x0; x < x1; x += 6)
                {
                    Color32 c = t.GetPixel(x, y);
                    float l = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                    n++;
                    if (l > 14f) lit++;
                }
            return n > 0 ? lit / (float)n : 0f;
        }

        // ───────────────────────── flight helpers ─────────────────────────

        private IEnumerator WaitAlt(double target, float timeout)
        {
            float until = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < until && Math.Abs(Alt - target) > 0.25) yield return null;
        }

        private IEnumerator FlyTo(double n, double e, double alt, float timeout, double radius = 1.6)
        {
            DesktopTelemetry.Client.Send(ClientMessage.FlyTo(n, e, alt), out _);
            float until = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < until)
            {
                var p = DesktopTelemetry.Client.Displayed.p;
                if (Math.Sqrt((p[0] - n) * (p[0] - n) + (p[1] - e) * (p[1] - e)) < radius) break;
                yield return null;
            }
        }

        private void View(string kind, float distance)
        {
            switch (kind)
            {
                case "low": Cam.SetAngles(-28f, 6f, true); break;
                case "high": Cam.SetAngles(-28f, 35f, true); break;
                default: Cam.SetAngles(-28f, 86f, true); break;
            }
            Cam.SetDistance(distance, true);
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
            _legacy = TelemetryGroundReference.Legacy;
            _tag = Screen.width + "x" + Screen.height + "_" + Lang + (_legacy ? "_antes" : "");
            UIManager ui = UIManager.Instance;
            ui.PrepareViewerForDeepLink();
            yield return new WaitForSecondsRealtime(3f);
            if (ui.IsOnboardingOpen) { ui.ShortcutBack(); yield return new WaitForSecondsRealtime(1f); }
            Cam?.ResetView();
            yield return new WaitForSecondsRealtime(1.0f);
            string lang = Lang;
            Log("window " + Screen.width + "x" + Screen.height + " fullscreen=" + Screen.fullScreen + " mode=" + Screen.fullScreenMode + " lang " + lang + (_legacy ? " (legacy ground)" : "") + " tier " + RenderQualityController.ResolvedTier + " renderScale " + (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null ? "urp" : "builtin"));

            // ── 0. the simulator on the ground, the camera framing the drone ──
            Tel.OpenMode();
            yield return new WaitForSecondsRealtime(0.6f);
            Tel.StartSimulator();
            yield return new WaitForSecondsRealtime(2.5f);
            Check("simulator running and the drone model is engaged", DesktopTelemetry.IsSimulator && Drv != null && Drv.Engaged);
            Check("the ground exists" + (_legacy ? " (legacy mesh grid)" : " (world shader ground)"), Drv.GroundRef != null && Drv.GroundRef.IsLegacy == _legacy);
            Camera cam = Camera.main;
            Check("camera far plane covers the geofence while a session runs (" + cam.farClipPlane.ToString("F0", CultureInfo.InvariantCulture) + " units)", _legacy || cam.farClipPlane >= TelemetryDroneDriver.SessionFarClip - 1f);
            Check("camera near plane " + cam.nearClipPlane.ToString("F3", CultureInfo.InvariantCulture) + " (<= 0.01)", cam.nearClipPlane <= 0.0101f);
            yield return Shot("00_capture_path");

            // ── 1. ground matrix: altitudes 0, 5, 20, 50 m x low / high / top-down ──
            Log("── ground matrix ──");
            double[] alts = { 0, 5, 20, 50 };
            string[] views = { "low", "high", "top" };
            float[] dist = { 30f, 30f, 40f };
            var coverage = new Dictionary<string, float>();
            for (int ai = 0; ai < alts.Length; ai++)
            {
                double a = alts[ai];
                if (a == 5) { DesktopTelemetry.Client.Send(ClientMessage.Takeoff(5), out _); yield return WaitAlt(5, 15f); yield return new WaitForSecondsRealtime(1.0f); }
                else if (a > 5) { DesktopTelemetry.Client.Send(ClientMessage.SetAltitude(a), out _); yield return WaitAlt(a, 40f); yield return new WaitForSecondsRealtime(1.2f); }
                Check("altitude " + a + " m reached (" + Alt.ToString("F2", CultureInfo.InvariantCulture) + ")", Math.Abs(Alt - a) < 0.5);
                for (int vi = 0; vi < views.Length; vi++)
                {
                    View(views[vi], dist[vi]);
                    yield return new WaitForSecondsRealtime(0.5f);
                    string nm = "01_suelo_" + a.ToString("0", CultureInfo.InvariantCulture) + "m_" + views[vi];
                    yield return Shot(nm, 0.3f);
                    coverage[nm] = LastGroundFraction;
                    if (!_legacy && a > 0.5)
                    {
                        var chip = Tel.AltitudeLabel;
                        double shown = double.TryParse((chip.Text ?? "").Replace(" m", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out double sv) ? sv : -999;
                        Check(a + " m " + views[vi] + ": the altitude label shows the Flight-tab altitude (" + chip.Text + " vs " + Alt.ToString("0.0", CultureInfo.InvariantCulture) + " m, visible " + chip.Visible + ")", chip.Visible && Math.Abs(shown - Alt) < 0.35);
                    }
                }
            }
            Log("canvas fraction lighter than the sky (top-down, 50 m): " + coverage["01_suelo_50m_top"].ToString("F2", CultureInfo.InvariantCulture) + "; low 0 m: " + coverage["01_suelo_0m_low"].ToString("F2", CultureInfo.InvariantCulture));
            if (!_legacy)
            {
                Check("top-down at 50 m: the ground fills the canvas (>= 0.9 of the region is clearly lighter than the sky)", coverage["01_suelo_50m_top"] >= 0.9f);
                Check("high orbit at 50 m: no part of the lower canvas is cut off (>= 0.45 lighter than the sky)", coverage["01_suelo_50m_high"] >= 0.45f);
                // horizon: the projected horizon line is on screen for the low view and the ground is below it
                View("low", 30f);
                yield return new WaitForSecondsRealtime(0.5f);
                Vector3 flat = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                Vector3 hp = cam.WorldToScreenPoint(cam.transform.position + flat * 1.0e6f);
                Log("analytic horizon at screen y = " + hp.y.ToString("F0", CultureInfo.InvariantCulture) + " of " + Screen.height + " (camera at y " + cam.transform.position.y.ToString("F1", CultureInfo.InvariantCulture) + ", ground " + Drv.GroundY.ToString("F2", CultureInfo.InvariantCulture) + ")");
                Check("low view at 50 m: the horizon is inside the frame", hp.z > 0 && hp.y > 0 && hp.y < Screen.height);
            }

            // ── 2. moving: a fly_to at 20 m and the follow camera, high and low, two instants ──
            Log("── moving ──");
            DesktopTelemetry.Client.Send(ClientMessage.SetAltitude(20), out _);
            yield return WaitAlt(20, 20f);
            View("high", 28f);
            DesktopTelemetry.Client.Send(ClientMessage.FlyTo(60, 25, 20), out _);
            yield return new WaitForSecondsRealtime(2.5f);
            Log("speed " + Math.Sqrt(DesktopTelemetry.Client.Displayed.v[0] * DesktopTelemetry.Client.Displayed.v[0] + DesktopTelemetry.Client.Displayed.v[1] * DesktopTelemetry.Client.Displayed.v[1]).ToString("F1", CultureInfo.InvariantCulture) + " m/s");
            yield return Shot("02_movimiento_a", 0.1f);
            Vector3 g0 = Drv.GroundRef != null && Drv.GroundRef.GroundMaterial != null ? Drv.GroundRef.transform.position : Vector3.zero;
            yield return new WaitForSecondsRealtime(1.6f);
            View("low", 28f);
            yield return Shot("02_movimiento_b", 0.1f);
            Check("the ground does not move with the drone (world fixed): transform unchanged while flying", _legacy || Drv.GroundRef.transform.position == g0);
            DesktopTelemetry.Client.Send(ClientMessage.Hold(), out _);
            yield return new WaitForSecondsRealtime(2f);

            // ── 3. toggles: ground off / on, PC_Low ──
            Log("── toggles ──");
            if (!_legacy)
            {
                View("high", 30f);
                Check("«Suelo» button exists, on by default", B("DkTelGround") != null && TelemetryDroneDriver.ShowGroundReference && B("DkTelGround").ClassListContains("dk-strip-btn--on"));
                yield return Shot("03_suelo_on", 0.2f);
                Click(B("DkTelGround"));
                yield return new WaitForSecondsRealtime(0.6f);
                Check("«Suelo» off: ground, shadow and the altitude line are hidden, the label is gone", !Drv.GroundShown && !Drv.GroundRef.CueVisible && !Tel.AltitudeLabel.Visible);
                Check("«Suelo» off: click-to-fly has no ground to hit", !Drv.TryGroundPoint(new Vector2(Screen.width * 0.5f, Screen.height * 0.2f), out _, out _, out _));
                yield return Shot("03_suelo_off", 0.2f);
                Click(B("DkTelGround"));
                yield return new WaitForSecondsRealtime(0.5f);
                Check("«Suelo» on again", Drv.GroundShown && Tel.AltitudeLabel.Visible);

                QualityTier prev = RenderQualityController.SavedTier;
                RenderQualityController.SetTier(QualityTier.Low);
                yield return new WaitForSecondsRealtime(2.5f);
                Check("PC_Low: the ground is simplified (_Quality = 0)", Drv.GroundRef.GroundMaterial.GetFloat("_Quality") < 0.5f);
                yield return Shot("04_pc_low", 0.3f);
                RenderQualityController.SetTier(prev);
                yield return new WaitForSecondsRealtime(2.5f);
                Check("quality restored: full ground (_Quality = 1) at " + RenderQualityController.ResolvedTier, Drv.GroundRef.GroundMaterial.GetFloat("_Quality") > 0.5f);
            }

            // ── 4. click-to-fly on the ground and the selection layer ──
            Log("── click to fly / selection ──");
            View("high", 30f);
            yield return new WaitForSecondsRealtime(0.6f);
            var clickAt = new Vector2(Screen.width * 0.30f, Screen.height * 0.20f);
            bool got = Drv.TryGroundPoint(clickAt, out double tn, out double te, out Vector3 wp);
            Check("the ray at a screen point reaches the ground plane (N " + tn.ToString("F1", CultureInfo.InvariantCulture) + " E " + te.ToString("F1", CultureInfo.InvariantCulture) + ")", got);
            bool hit = Tel.TryGroundClick(clickAt);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("a click on the ground is consumed as fly_to and the model goes to the point", hit && Sim.Model.mode == SimMode.Goto && Math.Abs(Sim.Model.target.n - tn) < 0.4 && Math.Abs(Sim.Model.target.e - te) < 0.4);
            Check("the ground and the cue are on the Ignore Raycast layer and have no collider", Drv.GroundRef.gameObject.layer == 2 && Drv.GroundRef.GetComponentsInChildren<Collider>(true).Length == 0 && Drv.GroundRef.GetComponent<Collider>() == null);
            int sel = SelectionManager.Instance != null ? SelectionManager.Instance.SelectionLayerMask.value : -1;
            Log("selection layer mask = " + sel);
            Check("the selection mask does not include the Ignore Raycast layer (2)", sel < 0 || (sel & (1 << 2)) == 0);
            yield return Shot("05_clic_para_volar", 0.5f);
            DesktopTelemetry.Client.Send(ClientMessage.Hold(), out _);
            yield return new WaitForSecondsRealtime(1.0f);

            // ── 5. the test course (DesktopCaptureEnv.Course.cs) ──
            if (!_legacy) yield return RunCourseChecks();

            Log("black captures: " + _blackShots);
            Check("no capture came out black", _blackShots == 0);
            Log("RESULT " + _pass + " pass, " + _fail + " fail");
            File.WriteAllText(Path.Combine(OutputDirectory, "env_" + _tag + ".log"), _log.ToString());
            Application.Quit();
        }


        // the test course checks arrive with PLAN_PC 28.3 (DesktopCaptureEnv.Course.cs)
        private IEnumerator RunCourseChecks() { yield break; }
    }
}
#endif
