#if UNITY_STANDALONE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Content;
using WebGL.Core.Managers;
using WebGL.Telemetry;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Selection regression probe (-desk-pickprobe &lt;folder&gt;; add -no-physsync to see the old behaviour). Walks the app states in the
    /// player (no OS mouse: UI clicks are NavigationSubmitEvents, keys go through the pilot seams) and, in each state, evaluates the REAL
    /// click path (PickDiagnostics = SelectionManager.HandleClick's checks + its raycast) at 8 points that lie on the visible surface of
    /// the drone (sampled once at rest and carried with the parts, so they follow the drone when it flies). Per point it logs the UI element
    /// under the pointer (Panel.Pick), InputBlocked / UI gesture ownership, the raycast hit and how far that collider's physics pose is from
    /// the drawn mesh. A state FAILS when a point that is not under UI misses the drone or hits a collider that is not where it is drawn,
    /// or when every point is blocked by UI without a modal layer open.
    /// </summary>
    internal sealed class DesktopPickProbe : MonoBehaviour
    {
        public string OutputDirectory;
        private readonly StringBuilder _log = new StringBuilder();
        private int _pass, _fail;
        private readonly HashSet<KeyCode> _held = new HashSet<KeyCode>();
        private string _tag;

        private struct Anchor { public Transform T; public Vector3 Local; public string Part; }
        private readonly List<Anchor> _anchors = new List<Anchor>();

        private void Start() { StartCoroutine(Run()); }
        private void Log(string s) { _log.AppendLine(s); Debug.Log("[PickProbe] " + s); }
        private VisualElement Root => UIManager.Instance.Root;
        private Button B(string n) => Root.Q<Button>(n);
        private DesktopTelemetryUI Tel => DesktopUIController.Instance.Chrome.Telemetry;

        private static void Click(Button b)
        {
            if (b == null) return;
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(OutputDirectory, _tag + "_" + name + ".png"), ImageConversion.EncodeToPNG(tex));
            Destroy(tex);
        }

        /// <summary>Samples a screen grid over the drone at rest and keeps up to 8 surface points on different parts.</summary>
        private void SampleAnchors()
        {
            Camera cam = Camera.main;
            var parts = FindObjectsByType<ExplodablePart>(FindObjectsSortMode.None);
            Bounds b = default; bool has = false;
            foreach (var p in parts) foreach (var r in p.GetComponentsInChildren<Renderer>()) { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
            if (!has || cam == null) return;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 s = cam.WorldToScreenPoint(c);
                min = Vector2.Min(min, s); max = Vector2.Max(max, s);
            }
            Physics.SyncTransforms();
            var seen = new HashSet<string>();
            var candidates = new List<Anchor>();
            for (int gy = 1; gy < 24; gy++)
                for (int gx = 1; gx < 24; gx++)
                {
                    Vector2 sp = new Vector2(Mathf.Lerp(min.x, max.x, gx / 24f), Mathf.Lerp(min.y, max.y, gy / 24f));
                    if (!Physics.Raycast(cam.ScreenPointToRay(sp), out RaycastHit hit, SelectionManager.Instance.MaxRayDistance, SelectionManager.Instance.SelectionLayerMask)) continue;
                    var ep = hit.collider.GetComponentInParent<ExplodablePart>();
                    if (ep == null || seen.Contains(ep.name)) continue;
                    if (InputManager.Instance.IsScreenPositionOverSelectionBlockingUI(sp)) continue;
                    seen.Add(ep.name);
                    // a little inside the surface, so a later view direction does not graze the silhouette
                    Vector3 inside = hit.point - hit.normal * (0.002f * hit.distance);
                    candidates.Add(new Anchor { T = hit.collider.transform, Local = hit.collider.transform.InverseTransformPoint(inside), Part = ep.name });
                }
            int step = Mathf.Max(1, candidates.Count / 8);
            for (int i = 0; i < candidates.Count && _anchors.Count < 8; i += step) _anchors.Add(candidates[i]);
            foreach (var a in _anchors) Log("anchor " + a.Part + " on " + a.T.name);
        }

        private IEnumerator Probe(string state, bool modal = false, bool shot = false)
        {
            yield return null;
            Camera cam = Camera.main;
            int ok = 0, bad = 0, ui = 0, off = 0;
            float maxDrift = 0f;
            foreach (var c in FindObjectsByType<MeshCollider>(FindObjectsSortMode.None))
                if (c.GetComponentInParent<ExplodablePart>() != null) maxDrift = Mathf.Max(maxDrift, PickDiagnostics.ColliderDrift(c));
            var sb = new StringBuilder();
            foreach (var a in _anchors)
            {
                if (a.T == null || !a.T.gameObject.activeInHierarchy) { off++; continue; }
                Vector3 w = a.T.TransformPoint(a.Local);
                Vector3 s = cam.WorldToScreenPoint(w);
                if (s.z <= 0f || s.x < 2 || s.y < 2 || s.x > Screen.width - 2 || s.y > Screen.height - 2) { off++; sb.AppendLine("    " + a.Part + " offscreen"); continue; }
                var r = PickDiagnostics.Describe(s);
                if (r.Outcome == "UI") ui++;
                else if (r.Outcome == "PART") ok++;
                else bad++;
                sb.AppendLine("    " + a.Part + " @(" + s.x.ToString("F0") + "," + s.y.ToString("F0") + ") " + PickDiagnostics.Format(r));
            }

            // the real click path once: resolve the first good point and select it, then deselect
            string selected = "-";
            foreach (var a in _anchors)
            {
                if (a.T == null) continue;
                Vector3 s = cam.WorldToScreenPoint(a.T.TransformPoint(a.Local));
                var r = PickDiagnostics.Describe(s);
                if (r.Outcome == "UI" || s.z <= 0f) continue;
                if (Physics.Raycast(cam.ScreenPointToRay(s), out RaycastHit hit, SelectionManager.Instance.MaxRayDistance, SelectionManager.Instance.SelectionLayerMask))
                {
                    SelectionManager.Instance.SimulateClickOn(hit.collider.transform);
                    selected = SelectionManager.Instance.CurrentSelection != null ? SelectionManager.Instance.CurrentSelection.name : "none";
                    SelectionManager.Instance.Deselect();
                }
                else selected = "background (click would deselect / fly_to)";
                break;
            }

            bool pass = bad == 0 && maxDrift <= PickDiagnostics.StaleDriftMetres && (modal || ok > 0);
            if (pass) _pass++; else _fail++;
            Log("STATE " + (pass ? "PASS" : "FAIL") + " " + state + (modal ? " [modal]" : "") + ": part=" + ok + " stale/miss=" + bad + " ui=" + ui + " off=" + off
                + " maxColliderDrift=" + maxDrift.ToString("F3") + "m firstClick=" + selected);
            _log.Append(sb);
            if (shot) yield return Shot(state.Replace(' ', '_').Replace('/', '_'));
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(OutputDirectory);
            DesktopTelemetryUI.KeyHeld = k => _held.Contains(k);
            DesktopTelemetryUI.IgnoreFocusLoss = true;
            PlayerPrefs.SetInt("Onboarding_Seen", 1);
            _tag = PickPhysicsSync.Enabled ? "despues" : "antes";
            yield return new WaitForSecondsRealtime(6f);
            UIManager ui = UIManager.Instance;
            DesktopUIController ctrl = DesktopUIController.Instance;
            Log("window " + Screen.width + "x" + Screen.height + " physSync=" + PickPhysicsSync.Enabled + " simulationMode=" + Physics.simulationMode + " build=" + Application.version);
            Click(B("HeroExploreBtn"));
            yield return new WaitForSecondsRealtime(3f);
            if (ui.IsOnboardingOpen) { ui.ShortcutBack(); yield return new WaitForSecondsRealtime(1f); }
            OrbitCameraController.Instance?.ResetView();
            yield return new WaitForSecondsRealtime(1.5f);
            SampleAnchors();

            yield return Probe("01 idle viewer (mode 1)", shot: true);
            for (int m = 2; m <= 4; m++) { Click(B("DkMode" + m)); yield return new WaitForSecondsRealtime(0.8f); yield return Probe("02 mode " + m); }
            Click(B("DkMode3")); yield return new WaitForSecondsRealtime(0.6f);
            Click(B("DkStudioRender")); yield return new WaitForSecondsRealtime(0.6f); yield return Probe("03 mode 3 render popover open");
            Click(B("DkStudioRender")); Click(B("DkStudioEnv")); yield return new WaitForSecondsRealtime(0.6f); yield return Probe("03 mode 3 env popover open");
            Click(B("DkStudioEnv")); Click(B("DkMode1")); yield return new WaitForSecondsRealtime(0.6f);

            // inspector tabs with a part selected
            GameObject arm = GameObject.Find("x500v2_arm_FL");
            if (arm != null) SelectionManager.Instance.SimulateClickOn(arm.GetComponentInChildren<Collider>()?.transform ?? arm.transform);
            yield return new WaitForSecondsRealtime(1.2f);
            Click(B("DkTabFlight")); yield return new WaitForSecondsRealtime(0.4f); yield return Probe("04 inspector tab Vuelo");
            Click(B("DkTabAnalysis")); yield return new WaitForSecondsRealtime(0.4f); yield return Probe("04 inspector tab Analisis");
            Click(B("DkTabPart")); yield return new WaitForSecondsRealtime(0.4f); yield return Probe("04 inspector tab Pieza");
            SelectionManager.Instance.Deselect(); OrbitCameraController.Instance?.ResetView(); yield return new WaitForSecondsRealtime(1.2f);

            ctrl.Tree.SetOpen(true); yield return new WaitForSecondsRealtime(0.6f); yield return Probe("05 part tree open");
            ctrl.Tree.SetOpen(false); yield return new WaitForSecondsRealtime(0.5f);
            ctrl.SetHelpOpen(true); yield return new WaitForSecondsRealtime(0.6f); yield return Probe("06 help overlay open", modal: true);
            ctrl.SetHelpOpen(false); yield return new WaitForSecondsRealtime(0.6f); yield return Probe("06 help overlay closed");

            ui.ShortcutToggleExplode(); yield return new WaitForSecondsRealtime(0.6f);
            ExplodedViewManager.Instance.SetExplosionFactor(0.5f); yield return new WaitForSecondsRealtime(1f); yield return Probe("07 exploded 0.5");
            Click(B("DkChipExplode")); yield return new WaitForSecondsRealtime(1.2f); yield return Probe("07 after explode chip (reassembled)");
            CrossSectionManager.Instance.EnableCrossSection(); yield return new WaitForSecondsRealtime(0.6f); yield return Probe("08 cut on");
            Click(B("DkChipCut")); yield return new WaitForSecondsRealtime(0.6f);

            // PLAN_PC 38 (paso 3): the simulated power card lives in Studio > Render now (INSPECT has Pins, Isolate and Focus)
            Click(B("DkMode3")); yield return new WaitForSecondsRealtime(0.8f);
            Click(B("ToolPowerBtn")); yield return new WaitForSecondsRealtime(3f); yield return Probe("09 manual power on (hover bob)", shot: true);
            Click(B("DkChipPower")); yield return new WaitForSecondsRealtime(2f); yield return Probe("09 manual power off");
            Click(B("DkMode3")); yield return new WaitForSecondsRealtime(0.6f);

            // telemetry: replay
            Tel.OpenMode(); yield return new WaitForSecondsRealtime(0.6f); yield return Probe("10 mode 4 strip, no session");
            Tel.OpenSourcePopover(); yield return new WaitForSecondsRealtime(0.6f); yield return Probe("10 source popover open"); // PLAN_PC 42: no «Fuente» button (L / the gear of the active source)
            Click(B("DkSrcReplay")); yield return new WaitForSecondsRealtime(4f); Tel.CloseAllPopovers();
            yield return Probe("11 replay running t=4s");
            yield return new WaitForSecondsRealtime(8f);
            yield return Probe("11 replay running t=12s", shot: true);
            Click(B("DkTelAlerts")); yield return new WaitForSecondsRealtime(0.5f); yield return Probe("11 replay + alerts popover"); Tel.CloseAllPopovers();
            Click(B("DkTelFaults")); yield return new WaitForSecondsRealtime(0.5f); yield return Probe("11 replay + faults popover"); Tel.CloseAllPopovers();
            Tel.ShowAnalysisTab(); yield return new WaitForSecondsRealtime(0.6f); yield return Probe("11 replay + Analisis tab"); Tel.ShowFlightTab(true);

            // simulator + piloting
            Tel.OpenSourcePopover(); yield return new WaitForSecondsRealtime(0.6f); // PLAN_PC 42: no «Fuente» button (L / the gear of the active source)
            Click(B("DkSrcSim")); yield return new WaitForSecondsRealtime(1.6f); Tel.CloseAllPopovers();
            yield return Probe("12 simulator on the ground");
            Click(B("DkTelPilot")); yield return new WaitForSecondsRealtime(0.5f);
            Click(Tel.CommandButton(PilotCommand.Takeoff)); yield return new WaitForSecondsRealtime(5.5f);
            yield return Probe("12 piloting, hovering ~5 m (camera follow)", shot: true);
            _held.Add(KeyCode.W); yield return new WaitForSecondsRealtime(1.6f); _held.Remove(KeyCode.W); yield return new WaitForSecondsRealtime(1f);
            yield return Probe("12 piloting after W forward");
            Tel.HandleBack(); yield return new WaitForSecondsRealtime(0.6f);
            yield return Probe("12 piloting OFF, flying");
            Click(B("DkSrcPillSim")); yield return new WaitForSecondsRealtime(1f); // PLAN_PC 42: the active source pill turns it off
            yield return Probe("13 session stopped");
            Tel.CloseMode(); yield return new WaitForSecondsRealtime(0.6f);
            Click(B("DkReset")); yield return new WaitForSecondsRealtime(2f);
            yield return Probe("14 after reset", shot: true);

            Log("RESULT physSync=" + PickPhysicsSync.Enabled + " pass=" + _pass + " fail=" + _fail);
            File.WriteAllText(Path.Combine(OutputDirectory, "pickprobe_" + _tag + ".log"), _log.ToString());
            Application.Quit();
        }
    }
}
#endif
