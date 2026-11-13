#if UNITY_STANDALONE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text;
using UnityEngine;
using WebGL.Core.Managers;
using WebGL.Telemetry;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Verification aid for the camera framing during a telemetry session (-tel-frame &lt;folder&gt; [-tel-frame-secs N] [-cam-legacy]).
    /// Plays the local replay from its start and, every second, projects the bounds of the drone (and of the selected part, when one is
    /// focused) onto the screen and compares them with the FREE area (what the docked tree / inspector, the top bar and the status bar
    /// leave). A scripted timeline exercises the view: default framing, a full orbit, zoom in / out, an alert click that focuses motor 3,
    /// Reset, the exploded view. -cam-legacy switches back to the follow of WP-10 (offsets fixed in the world, no snap) to measure the
    /// drift that it had. Writes framing_&lt;tag&gt;.csv (one row per second), a summary per phase and some PNGs. Not active unless the flag is given.
    /// </summary>
    internal sealed class DesktopCaptureFraming : MonoBehaviour
    {
        public string OutputDirectory;
        /// <summary>Set by code (the Editor has no command line): run length and -cam-legacy.</summary>
        public float SecsOverride;
        public bool LegacyOverride;
        private readonly StringBuilder _csv = new StringBuilder();
        private readonly StringBuilder _log = new StringBuilder();
        private string _tag;
        private bool _legacy;
        private float _secs = 490f;

        private void Start()
        {
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].Equals("-cam-legacy", StringComparison.OrdinalIgnoreCase)) _legacy = true;
                if (a[i].Equals("-tel-frame-secs", StringComparison.OrdinalIgnoreCase) && i + 1 < a.Length) float.TryParse(a[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _secs);
            }
            if (SecsOverride > 0f) _secs = SecsOverride;
            if (LegacyOverride) _legacy = true;
            StartCoroutine(Run());
        }

        private static string F(float v, string fmt = "F3") => float.IsNaN(v) ? string.Empty : v.ToString(fmt, CultureInfo.InvariantCulture);

        private void Log(string s) { _log.AppendLine(s); Debug.Log("[Framing] " + s); }

        private DesktopTelemetryUI Tel => DesktopUIController.Instance.Chrome.Telemetry;
        private static string Lang => AppLanguageManager.IsSpanish ? "es" : "en";

        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(OutputDirectory, _tag + "_" + name + ".png"), ImageConversion.EncodeToPNG(tex));
            Destroy(tex);
        }

        /// <summary>The area of the screen the docked UI leaves free, in pixels (y down).</summary>
        private Rect FreeArea()
        {
            DesktopUIController c = DesktopUIController.Instance;
            float s = c.UiScale;
            float left = (c.Tree != null && c.Tree.IsOpen ? DesktopPartTree.Width : DesktopChrome.RailWidth) * s;
            float right = (c.SidePanel.IsOpen ? c.SidePanel.OccupiedWidth : DesktopChrome.RailWidth) * s;
            float top = DesktopChrome.TopHeight * s;
            float bottom = DesktopChrome.StatusHeight * s;
            return new Rect(left, top, Screen.width - left - right, Screen.height - top - bottom);
        }

        private static bool Bounds3(Transform root, out Bounds b)
        {
            b = default;
            bool any = false;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any;
        }

        /// <summary>Screen position (pixels, y down) of a world point; false when it is behind the camera.</summary>
        private static bool ToScreen(Camera cam, Vector3 world, out Vector2 px)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            px = new Vector2(sp.x, Screen.height - sp.y);
            return sp.z > 0.01f;
        }

        /// <summary>
        /// Share of the drone inside the free area: the fraction of its enabled renderers whose centre projects inside it (a robust measure when
        /// the camera is close and the perspective of a bounding box would explode).
        /// </summary>
        private static float InsideFraction(Camera cam, Transform root, Rect free)
        {
            int n = 0, inside = 0;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                n++;
                if (ToScreen(cam, r.bounds.center, out Vector2 px) && free.Contains(px)) inside++;
            }
            return n == 0 ? 0f : inside / (float)n;
        }

        private sealed class PhaseStat
        {
            public string Name;
            public int N, Out;
            public float MaxDx, MaxDy, MinInside = 1f, SumDist, MaxSelDx, MaxSelDy, MaxFocusErr, SumFocusErr;
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(OutputDirectory);
            PlayerPrefs.SetInt("Onboarding_Seen", 1);
            OrbitCameraController.LegacyTelemetryFollow = _legacy;
            yield return new WaitForSecondsRealtime(7f);
            AccessibilityManager.Instance?.SetUIScale(1f);
            yield return new WaitForSecondsRealtime(0.5f);
            _tag = (_legacy ? "antes_" : "despues_") + Screen.width + "x" + Screen.height + "_" + Lang;
            UIManager ui = UIManager.Instance;
            ui.PrepareViewerForDeepLink();
            yield return new WaitForSecondsRealtime(3f);
            if (ui.IsOnboardingOpen) { ui.ShortcutBack(); yield return new WaitForSecondsRealtime(1f); }
            OrbitCameraController orbit = OrbitCameraController.Instance;
            orbit.ResetView();
            orbit.SetAngles(-30f, 22f, true);
            yield return new WaitForSecondsRealtime(1.5f);
            orbit.DesktopGetTargets(out _, out float d0);   // the default framing distance: the whole drone fits
            Log("framing run " + _tag + ": follow = " + (_legacy ? "LEGACY (WP-10)" : "rigid body follow") + ", window " + Screen.width + "x" + Screen.height + ", " + _secs.ToString("F0") + " s");

            // docked inspector open on the Vuelo tab: the framing must respect it
            Tel.OpenMode();
            DesktopTelemetry.StartLocalReplay(0);
            Tel.ShowFlightTab(true);
            yield return new WaitForSecondsRealtime(2.5f);

            Camera cam = Camera.main;
            Transform root = TelemetryDroneDriver.Instance != null ? TelemetryDroneDriver.Instance.Root : null;
            if (root == null) { Log("FAIL: the drone root is not engaged"); Application.Quit(); yield break; }
            _csv.AppendLine("t,phase,seg,lt,alt_m,speed_ms,dx,dy,inside,camDist,selDx,selDy,focusErr_units,drone_size_units");

            var phases = new List<PhaseStat>();
            PhaseStat cur = null;
            string phaseName = "default";
            float t0 = Time.realtimeSinceStartup;
            int shotsTaken = 0;
            bool fault = false, focused = false, deselected = false, reset = false, exploded = false, unexploded = false;
            float sweepStart = 150f, zoomStart = 190f, alertAt = 230f, resetAt = 300f, explodeAt = 335f, explodeOff = 365f;
            while (Time.realtimeSinceStartup - t0 < _secs)
            {
                float t = Time.realtimeSinceStartup - t0;
                // timeline
                string want = t < sweepStart ? "default" : t < zoomStart ? "orbit" : t < alertAt ? "zoom" : t < resetAt ? "alert_focus" : t < explodeAt ? "reset" : t < explodeOff ? "explode" : "default2";
                if (want != phaseName || cur == null)
                {
                    phaseName = want;
                    cur = new PhaseStat { Name = phaseName };
                    phases.Add(cur);
                }
                if (phaseName == "orbit") orbit.SetAngles(-30f + (t - sweepStart) * 9f, 22f, false);                       // 360 degrees in 40 s
                if (phaseName == "zoom") orbit.SetDistance(d0 * (0.95f + 0.5f * Mathf.Sin((t - zoomStart) / 40f * Mathf.PI * 2f)), false); // 0.45 .. 1.45 x the default
                if (t >= alertAt && !fault) { fault = Tel.Inject(FaultKind.MotorFailure); orbit.SetAngles(-30f, 22f, false); orbit.SetDistance(d0, false); }
                if (t >= alertAt + 5f && !focused) { Tel.SelectComponent("motor3"); focused = true; }
                if (t >= alertAt + 35f && !deselected) { SelectionManager.Instance.Deselect(); deselected = true; }
                if (t >= resetAt && !reset) { ui.ShortcutResetView(); reset = true; }
                if (t >= explodeAt && !exploded) { ui.ShortcutToggleExplode(); exploded = true; }
                if (t >= explodeOff && !unexploded) { ui.ShortcutToggleExplode(); unexploded = true; }

                yield return new WaitForSecondsRealtime(1f);
                t = Time.realtimeSinceStartup - t0;
                var client = DesktopTelemetry.Client;
                TelemetryFrame f = client != null ? client.Displayed : null;
                if (f == null || !Bounds3(root, out Bounds b) || !ToScreen(cam, b.center, out Vector2 c)) continue;
                Rect free = FreeArea();
                float dx = (c.x - free.center.x) / free.width;
                float dy = (c.y - free.center.y) / free.height;
                float inside = InsideFraction(cam, root, free);
                float selDx = float.NaN, selDy = float.NaN;
                Transform sel = SelectionManager.Instance != null ? SelectionManager.Instance.CurrentSelection : null;
                if (sel != null && Bounds3(sel, out Bounds sb) && ToScreen(cam, sb.center, out Vector2 sc))
                {
                    selDx = (sc.x - free.center.x) / free.width;
                    selDy = (sc.y - free.center.y) / free.height;
                }
                orbit.DesktopGetTargets(out Vector3 settledFocus, out float dist);
                float focusErr = (settledFocus - b.center).magnitude;
                _csv.AppendLine(string.Join(",", F(t, "F0"), phaseName, f.seg.ToString(CultureInfo.InvariantCulture), F((float)f.lt, "F1"), F((float)(-f.p[2]), "F1"),
                    F((float)Math.Sqrt(f.v[0] * f.v[0] + f.v[1] * f.v[1]), "F1"), F(dx), F(dy), F(inside, "F2"), F(dist, "F1"), F(selDx), F(selDy), F(focusErr, "F2"), F(b.size.magnitude, "F1")));
                cur.N++;
                cur.MaxFocusErr = Mathf.Max(cur.MaxFocusErr, focusErr);
                cur.SumFocusErr += focusErr;
                cur.MaxDx = Mathf.Max(cur.MaxDx, Mathf.Abs(dx));
                cur.MaxDy = Mathf.Max(cur.MaxDy, Mathf.Abs(dy));
                cur.MinInside = Mathf.Min(cur.MinInside, inside);
                if (inside < 0.6f) cur.Out++;
                if (!float.IsNaN(selDx)) { cur.MaxSelDx = Mathf.Max(cur.MaxSelDx, Mathf.Abs(selDx)); cur.MaxSelDy = Mathf.Max(cur.MaxSelDy, Mathf.Abs(selDy)); }

                // pictures: default framing, orbit, alert focus, reset, explode, end
                if (shotsTaken < 9 && (t >= 60f && shotsTaken == 0 || t >= 120f && shotsTaken == 1 || t >= 170f && shotsTaken == 2 || t >= 215f && shotsTaken == 3 ||
                                       t >= 260f && shotsTaken == 4 || t >= 285f && shotsTaken == 5 || t >= 320f && shotsTaken == 6 || t >= 352f && shotsTaken == 7 || t >= 420f && shotsTaken == 8))
                {
                    yield return Shot(string.Format("{0:00}_t{1:000}_{2}", shotsTaken + 1, (int)t, phaseName));
                    shotsTaken++;
                }
            }

            Log("summary per phase (focus-to-drone-centre = distance in world units between the settled camera focus and the centre of the drone's bounds, the cause-level measure; dx, dy = offset of the centre of the drone from the centre of the free area, as a fraction of its width / height; inside = share of the drone's parts whose centre is inside the free area; out = seconds with less than 60 % inside; sel = same for the focused part):");
            foreach (PhaseStat p in phases)
                Log(string.Format(CultureInfo.InvariantCulture, "  {0,-12} n={1,3}  max|dx|={2:F3}  max|dy|={3:F3}  min inside={4:F2}  out={5,3} s  focus-to-drone-centre: max {8:F2} mean {9:F2} units  sel max|dx|={6:F3} max|dy|={7:F3}", p.Name, p.N, p.MaxDx, p.MaxDy, p.MinInside, p.Out, p.MaxSelDx, p.MaxSelDy, p.MaxFocusErr, p.N > 0 ? p.SumFocusErr / p.N : 0f));
            File.WriteAllText(Path.Combine(OutputDirectory, "framing_" + _tag + ".csv"), _csv.ToString());
            File.WriteAllText(Path.Combine(OutputDirectory, "framing_" + _tag + ".log"), _log.ToString());
            Application.Quit();
        }
    }

    /// <summary>One-shot measure of where the drone is on the screen relative to the free area (used by the -tel-ui run too).</summary>
    internal static class FramingProbe
    {
        public static string Measure()
        {
            Camera cam = Camera.main;
            Transform root = TelemetryDroneDriver.Instance != null ? TelemetryDroneDriver.Instance.Root : null;
            if (cam == null || root == null) return "no drone";
            DesktopUIController c = DesktopUIController.Instance;
            float sc = c.UiScale;
            float left = (c.Tree != null && c.Tree.IsOpen ? DesktopPartTree.Width : DesktopChrome.RailWidth) * sc;
            float right = (c.SidePanel.IsOpen ? c.SidePanel.OccupiedWidth : DesktopChrome.RailWidth) * sc;
            float top = DesktopChrome.TopHeight * sc, bottom = DesktopChrome.StatusHeight * sc;
            Rect free = new Rect(left, top, Screen.width - left - right, Screen.height - top - bottom);
            Bounds b = default; bool any = false;
            int n = 0, inside = 0;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                n++;
                Vector3 sp = cam.WorldToScreenPoint(r.bounds.center);
                if (sp.z > 0.01f && free.Contains(new Vector2(sp.x, Screen.height - sp.y))) inside++;
            }
            if (!any) return "no renderers";
            Vector3 cp = cam.WorldToScreenPoint(b.center);
            float dx = (cp.x - free.center.x) / free.width, dy = ((Screen.height - cp.y) - free.center.y) / free.height;
            return string.Format(CultureInfo.InvariantCulture, "drone centre offset from the free area centre dx={0:F3} dy={1:F3} (fractions of its width/height), parts inside the free area {2:F2}, follow={3}",
                dx, dy, n == 0 ? 0f : inside / (float)n, OrbitCameraController.LegacyTelemetryFollow ? "LEGACY" : "rigid");
        }
    }
}
#endif
