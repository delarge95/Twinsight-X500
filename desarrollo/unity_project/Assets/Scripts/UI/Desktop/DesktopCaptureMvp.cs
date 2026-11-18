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
    /// Verification aid for the MVP priorities (-mvp-capture &lt;folder&gt;, plus -lang es|en and the window flags). In-player probes, no OS mouse or keyboard: keys go through the
    /// <see cref="DesktopKeyboardGate"/> seams, the pad is a FAKE XInput state (<see cref="PadSource.Provider"/>), the mouse movement of «Modo juego» comes from
    /// <see cref="DesktopGameMode.MouseDeltaOverride"/>; clicks are UI Toolkit events. One PNG per state (tag = WxH_lang), PASS / FAIL lines; a black capture fails the run.
    ///  P0: the camera while piloting (chase «Seguir», «Libre», pad orbit with LB + right stick and recentre after 1.5 s, game mode with the cursor locked, Esc, the gates).
    /// </summary>
    internal sealed partial class DesktopCaptureMvp : MonoBehaviour
    {
        public string OutputDirectory;
        private int _fail, _pass, _blackShots;
        private readonly StringBuilder _log = new StringBuilder();
        private string _tag;

        private void Start() { StartCoroutine(Run()); }
        private void Log(string s) { _log.AppendLine(s); Debug.Log("[Mvp] " + s); }

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
        private static OrbitCameraController Cam => OrbitCameraController.Instance;
        private static TelemetryFrame Frame => DesktopTelemetry.Client?.Displayed;
        private double Alt => Frame != null ? -Frame.p[2] : 0;
        private static string F1(double x) => x.ToString("F1", CultureInfo.InvariantCulture);

        private readonly HashSet<KeyCode> _held = new HashSet<KeyCode>();
        private readonly Dictionary<KeyCode, int> _pressedAt = new Dictionary<KeyCode, int>();
        private PadState _pad;
        private bool _padOn;

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

        private void SetPad(bool connected, PadButtons b = PadButtons.None, double lx = 0, double ly = 0, double rx = 0, double ry = 0)
        {
            _padOn = connected;
            _pad = new PadState { Buttons = b, LeftX = lx, LeftY = ly, RightX = rx, RightY = ry };
        }

        /// <summary>Viewport position of the visible centre of the drone (0..1, origin bottom-left).</summary>
        private static Vector3 DroneViewport()
        {
            Transform root = TelemetryDroneDriver.Instance != null ? TelemetryDroneDriver.Instance.Root : null;
            Camera cam = Camera.main;
            if (root == null || cam == null) return new Vector3(-1, -1, -1);
            Bounds b = default;
            bool any = false;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return cam.WorldToViewportPoint(any ? b.center : root.position);
        }

        private static float CamHeading()
        {
            Vector3 f = Camera.main.transform.forward;
            ChaseCamera.TryHeading(f.x, f.z, out float y);
            return y;
        }

        private static float DroneHeading()
        {
            Transform root = TelemetryDroneDriver.Instance.Root;
            Quaternion rel = root.rotation * Quaternion.Inverse(TelemetryDroneDriver.Instance.RestPose.RootRotation);
            Vector3 fwd = rel * Vector3.back;
            ChaseCamera.TryHeading(fwd.x, fwd.z, out float y);
            return y;
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(OutputDirectory);
            PlayerPrefs.SetInt("Onboarding_Seen", 1);
            PlayerPrefs.DeleteKey(DesktopTelemetryUI.PrefSource);
            PlayerPrefs.DeleteKey(DesktopTelemetryUI.PrefGround);
            PlayerPrefs.DeleteKey("Desk_CamMode");
            PlayerPrefs.DeleteKey("Desk_ChaseDist");
            DesktopTelemetryUI.IgnoreFocusLoss = true;
            DesktopKeyboardGate.KeyHeld = k => _held.Contains(k);
            DesktopKeyboardGate.KeyPressed = k => _pressedAt.TryGetValue(k, out int f) && f == Time.frameCount;
            PadSource.Provider = () => _padOn ? _pad : (PadState?)null;
            yield return new WaitForSecondsRealtime(7f);
            AccessibilityManager.Instance?.SetUIScale(1f);
            yield return new WaitForSecondsRealtime(0.5f);
            _tag = Screen.width + "x" + Screen.height + "_" + Lang;
            UIManager ui = UIManager.Instance;
            ui.PrepareViewerForDeepLink();
            yield return new WaitForSecondsRealtime(3f);
            if (ui.IsOnboardingOpen) { UIManager.Instance.ShortcutBack(); yield return new WaitForSecondsRealtime(1f); }
            Cam?.ResetView();
            yield return new WaitForSecondsRealtime(1.0f);
            Log("window " + Screen.width + "x" + Screen.height + " fullscreen=" + Screen.fullScreen + " lang " + Lang + " tier " + RenderQualityController.ResolvedTier);

            Tel.OpenMode();
            yield return new WaitForSecondsRealtime(0.6f);
            Tel.StartSimulator();
            yield return new WaitForSecondsRealtime(2.5f);
            Check("simulator running", DesktopTelemetry.IsSimulator);

            yield return CameraChecks();
            yield return ChipChecks();
            yield return BatteryChecks();
            yield return ThermalChecks();
            yield return SitlBatteryChecks();

            DesktopKeyboardGate.KeyHeld = UnityEngine.Input.GetKey;
            DesktopKeyboardGate.KeyPressed = UnityEngine.Input.GetKeyDown;
            PadSource.Provider = null;
            DesktopGameMode.MouseDeltaOverride = null;
            Log("black captures: " + _blackShots);
            Check("no capture came out black", _blackShots == 0);
            Log("RESULT " + _pass + " pass, " + _fail + " fail");
            File.WriteAllText(Path.Combine(OutputDirectory, "mvp_" + _tag + ".log"), _log.ToString());
            Application.Quit();
        }

        // ───────────────────────── P0: camera while piloting ─────────────────────────

        private IEnumerator CameraChecks()
        {
            Log("── P0: camera while piloting ──");
            Check("the camera buttons exist in the piloting strip (mode and game mode)", B("DkCamMode") != null && B("DkGameMode") != null);
            Check("before piloting the chase is off and «Modo juego» is dimmed", !Cam.ChaseActive && B("DkGameMode").ClassListContains("dk-strip-btn--dim"));

            // not piloting: X is refused with a toast and the cursor stays free
            yield return PressKey(KeyCode.X);
            Check("X without piloting does not capture the cursor", !DesktopGameMode.Active && !DesktopGameMode.CursorLocked);

            DesktopTelemetry.Client.Send(ClientMessage.Takeoff(6), out _);
            yield return WaitAlt(6, 20f);
            yield return new WaitForSecondsRealtime(1f);
            Cam.SetAngles(-28f, 32f, true);
            Tel.SetPilot(true, "probe");
            yield return new WaitForSecondsRealtime(2.2f);
            Check("piloting on: the chase camera is engaged by default («Seguir»)", Tel.Piloting && Tel.CamMode == CameraMode.Follow && Cam.ChaseActive);
            Check("the strip button says «Seguir»", B("DkCamMode").Q<Label>(className: "dk-strip-text").text == T("cam.follow"));
            float diff0 = Mathf.Abs(Mathf.DeltaAngle(CamHeading(), DroneHeading()));
            Log("heading camera " + F1(CamHeading()) + " drone " + F1(DroneHeading()) + " diff " + F1(diff0));
            Check("the camera is behind the drone (same heading within 8 degrees)", diff0 < 8f);
            Vector3 vp = DroneViewport();
            Check("the drone is centred in the viewport (0.35..0.65)", vp.z > 0 && vp.x > 0.30f && vp.x < 0.70f && vp.y > 0.30f && vp.y < 0.70f);
            yield return Shot("30_p0_seguir_detras", 0.2f);

            // the drone turns: the camera turns with it, smoothly, and the drone never leaves the frame
            DesktopTelemetry.Client.Send(ClientMessage.Yaw(120), out _);
            int outside = 0; float maxStep = 0f, prev = CamHeading(); float until = Time.realtimeSinceStartup + 4.5f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                Vector3 v = DroneViewport();
                if (!(v.z > 0 && v.x > 0.12f && v.x < 0.88f && v.y > 0.12f && v.y < 0.88f)) outside++;
                float h = CamHeading();
                maxStep = Mathf.Max(maxStep, Mathf.Abs(Mathf.DeltaAngle(prev, h)));
                prev = h;
            }
            float diff1 = Mathf.Abs(Mathf.DeltaAngle(CamHeading(), DroneHeading()));
            Log("after a 120 degree turn: heading camera " + F1(CamHeading()) + " drone " + F1(DroneHeading()) + " diff " + F1(diff1) + ", frames with the drone outside 0.12..0.88: " + outside + ", largest step per frame " + F1(maxStep));
            Check("the camera turned with the drone (behind it again)", diff1 < 8f);
            Check("during the turn the drone never left the frame", outside == 0);
            Check("the camera turned smoothly (never more than 12 degrees in one frame, a screenshot frame included)", maxStep < 12f);
            yield return Shot("31_p0_giro_del_dron", 0.1f);

            // pad: LB + right stick orbits, the drone gets nothing from the right stick, the orbit glides back
            SetPad(true);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("a pad is connected in DRON mode", DesktopPad.Instance != null && DesktopPad.Instance.Connected && DesktopPad.Instance.Mode == PadMode.Drone);
            Check("the pad badge shows the camera mapping in DRON mode", DesktopPad.Instance.Badge.Q<Label>("DkPadCam") != null && DesktopPad.Instance.Badge.Q<Label>("DkPadCam").text == T("pad.badge.cam") && DesktopPad.Instance.Badge.Q<Label>("DkPadCam").resolvedStyle.display != DisplayStyle.None);
            SetPad(true, PadButtons.LB, 0, 0, 1.0, 0);
            yield return new WaitForSecondsRealtime(0.9f);
            Cam.DesktopChaseState(out _, out _, out float uy, out _);
            Check("LB + right stick right orbits the camera (offset " + F1(uy) + " degrees)", Mathf.Abs(uy) > 30f);
            SimSticks st = Tel.LastSticks;
            Check("the drone got no pitch / roll from the right stick while LB is held", st.x == 0 && st.y == 0);
            float diffOrbit = Mathf.Abs(Mathf.DeltaAngle(CamHeading(), DroneHeading()));
            Check("the camera is away from behind the drone while orbiting", diffOrbit > 15f);
            Vector3 vpo = DroneViewport();
            Check("orbiting keeps the drone in frame", vpo.z > 0 && vpo.x > 0.2f && vpo.x < 0.8f && vpo.y > 0.2f && vpo.y < 0.8f);
            yield return Shot("32_p0_orbita_con_mando", 0.05f);
            SetPad(true);
            yield return new WaitForSecondsRealtime(1.0f);
            Cam.DesktopChaseState(out _, out _, out float uy1, out _);
            Check("within 1.0 s of letting go the offset is still held (1.5 s): " + F1(uy1), Mathf.Abs(uy1) > 20f);
            yield return new WaitForSecondsRealtime(3.0f);
            Cam.DesktopChaseState(out _, out _, out float uy2, out float up2);
            Check("after letting go the camera is back behind the drone (offset " + F1(uy2) + ", " + F1(up2) + ")", Mathf.Abs(uy2) < 1f && Mathf.Abs(up2) < 1f);
            Check("... and its heading is the drone's", Mathf.Abs(Mathf.DeltaAngle(CamHeading(), DroneHeading())) < 8f);

            // D-pad orbits in steps
            SetPad(true, PadButtons.DpadRight);
            yield return new WaitForSecondsRealtime(0.5f);
            SetPad(true);
            Cam.DesktopChaseState(out _, out _, out float udp, out _);
            Check("the D-pad orbits the camera too (offset " + F1(udp) + ")", udp > 20f);
            yield return new WaitForSecondsRealtime(3.2f);

            // R3 switches the mode; in «Libre» the orbit stays where it is
            SetPad(true, PadButtons.RightStick);
            yield return new WaitForSecondsRealtime(0.2f);
            SetPad(true);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("R3 switches to «Libre»: the chase is off, the button says «Libre»", Tel.CamMode == CameraMode.Free && !Cam.ChaseActive && B("DkCamMode").Q<Label>(className: "dk-strip-text").text == T("cam.free"));
            Check("the mode is remembered (PlayerPrefs)", PlayerPrefs.GetString("Desk_CamMode") == "free");
            Cam.DesktopChaseState(out float yawFree0, out _, out _, out _);
            SetPad(true, PadButtons.LB, 0, 0, 1.0, 0);
            yield return new WaitForSecondsRealtime(0.6f);
            SetPad(true);
            yield return new WaitForSecondsRealtime(2.5f);
            Cam.DesktopChaseState(out float yawFree1, out _, out _, out _);
            Check("in «Libre» LB + right stick orbits and the orbit STAYS (no recentre): " + F1(yawFree0) + " -> " + F1(yawFree1), Mathf.Abs(Mathf.DeltaAngle(yawFree0, yawFree1)) > 25f);
            yield return Shot("33_p0_libre", 0.1f);
            // the key B goes back to «Seguir»
            yield return PressKey(KeyCode.B);
            yield return new WaitForSecondsRealtime(3.5f);
            Check("B switches back to «Seguir» and the camera returns behind the drone", Tel.CamMode == CameraMode.Follow && Cam.ChaseActive && Mathf.Abs(Mathf.DeltaAngle(CamHeading(), DroneHeading())) < 8f);

            // game mode
            yield return PressKey(KeyCode.X);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("X enters «Modo juego»: the cursor is locked", DesktopGameMode.Active && DesktopGameMode.CursorLocked && !UnityEngine.Cursor.visible);
            Check("the indicator and the shield are visible", Tel.GameBadge.resolvedStyle.display != DisplayStyle.None && Tel.GameShield.resolvedStyle.display != DisplayStyle.None && Tel.GameShield.resolvedStyle.width > Root.resolvedStyle.width * 0.9f);
            Check("the strip button is lit", B("DkGameMode").ClassListContains("dk-strip-btn--on"));
            Vector2 mouse = new Vector2(2.5f, 0f);
            DesktopGameMode.MouseDeltaOverride = () => mouse;
            yield return new WaitForSecondsRealtime(0.5f);
            Cam.DesktopChaseState(out _, out _, out float uyg, out _);
            Check("the mouse (cursor locked) orbits the camera (offset " + F1(uyg) + ")", Mathf.Abs(uyg) > 20f);
            Vector3 vpg = DroneViewport();
            Check("the drone stays in frame in game mode", vpg.z > 0 && vpg.x > 0.2f && vpg.x < 0.8f && vpg.y > 0.2f && vpg.y < 0.8f);
            yield return Shot("34_p0_modo_juego", 0.05f);
            // the scene ignores clicks while the cursor is locked: no fly_to marker, no selection
            bool flewBefore = TelemetryDroneDriver.Instance.FlyTargetVisible;
            Check("a ground click in game mode does not fly the drone (SelectionManager is gated)", !flewBefore && !(SelectionManager.Instance != null && SelectionManager.Instance.HoveredObject != null));
            mouse = Vector2.zero;
            yield return new WaitForSecondsRealtime(0.2f);
            // Esc: first the cursor, then (second Esc) piloting
            UIManager.Instance.ShortcutBack();
            yield return new WaitForSecondsRealtime(0.3f);
            Check("Esc releases the cursor first and piloting continues", !DesktopGameMode.Active && !DesktopGameMode.CursorLocked && UnityEngine.Cursor.visible && Tel.Piloting);
            Check("the indicator and shield are gone", Tel.GameBadge.resolvedStyle.display == DisplayStyle.None || Tel.GameShield.resolvedStyle.display == DisplayStyle.None);
            yield return new WaitForSecondsRealtime(3.0f);

            // the gate: a text field owns the keyboard -> X does nothing; the shortcut toggles; help releases
            DesktopKeyboardGate.TypingOverride = () => true;
            yield return PressKey(KeyCode.X);
            yield return PressKey(KeyCode.B);
            DesktopKeyboardGate.TypingOverride = null;
            Check("with a text field typing, X and B do nothing (DesktopKeyboardGate)", !DesktopGameMode.Active && Tel.CamMode == CameraMode.Follow);
            yield return PressKey(KeyCode.X);
            Check("X enters again", DesktopGameMode.Active);
            yield return PressKey(KeyCode.X);
            Check("the same key X releases it", !DesktopGameMode.Active && !DesktopGameMode.CursorLocked);
            yield return PressKey(KeyCode.X);
            DesktopUIController.Instance.SetHelpOpen(true);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("opening the help releases the cursor", !DesktopGameMode.Active && DesktopUIController.Instance.IsHelpOpen);
            yield return Shot("35_p0_ayuda_pilotaje", 0.3f);
            Check("the help lists the camera keys (B, X) in the piloting column", Root.Q<Label>(className: "desktop-help-desc") != null && HelpHas(T("cam.game") == null ? "" : (AppLanguageManager.IsSpanish ? "Modo juego" : "Game mode")));
            DesktopUIController.Instance.Help.OpenPadTab();
            yield return new WaitForSecondsRealtime(0.3f);
            Check("the pad tab of the help lists LB + RS, D-pad and R3 for the camera", HelpHas(AppLanguageManager.IsSpanish ? "Cámara: mantén LB" : "Camera: hold LB") && HelpHas(AppLanguageManager.IsSpanish ? "Seguir / Libre" : "Follow / Free"));
            yield return Shot("36_p0_ayuda_mando", 0.2f);
            DesktopUIController.Instance.SetHelpOpen(false);
            yield return new WaitForSecondsRealtime(0.3f);

            // Reset view, isolate, focus on a part, explode with the chase on
            Tel.ShowToast(" ", false, 0.01f);
            Cam.DesktopOrbitBy(50f, 10f);
            UIManager.Instance.ShortcutResetView();
            yield return new WaitForSecondsRealtime(0.3f);
            Cam.DesktopChaseState(out _, out _, out float ur, out _);
            Check("Reset view drops the orbit offset and the chase stays on", Mathf.Abs(ur) < 1f && Cam.ChaseActive);
            UIManager.Instance.ShortcutToggleExplode();
            yield return new WaitForSecondsRealtime(1.2f);
            Vector3 vpe = DroneViewport();
            Check("explode with the chase on: still in frame and the chase on", Cam.ChaseActive && vpe.z > 0 && vpe.x > 0.1f && vpe.x < 0.9f && vpe.y > 0.1f && vpe.y < 0.9f);
            yield return Shot("37_p0_explosion_en_seguir", 0.4f);
            UIManager.Instance.ShortcutToggleExplode();
            yield return new WaitForSecondsRealtime(0.8f);
            Transform part = TelemetryDroneDriver.Instance.MotorPart(1);
            Cam.FocusOnObject(part);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("focusing a part suspends the chase (the part is the user's to inspect)", !Cam.ChaseActive);
            UIManager.Instance.ShortcutResetView();
            yield return new WaitForSecondsRealtime(1.5f);
            Check("Reset gives the whole drone back and the chase resumes", Cam.ChaseActive);

            // click to fly and the selection still work with the chase on
            Check("click-to-fly hook is installed", SelectionManager.GroundClickHook != null);
            Tel.SetPilot(false, "probe");
            yield return new WaitForSecondsRealtime(0.8f);
            Check("leaving piloting: the chase is off and the cursor is free", !Cam.ChaseActive && !DesktopGameMode.Active && !Tel.Piloting);
            Tel.SetPilot(true, "probe");
            yield return new WaitForSecondsRealtime(1.0f);
            Check("piloting again engages the chase again", Cam.ChaseActive);
            Tel.SetPilot(false, "probe");
            SetPad(false);
            DesktopTelemetry.Client.Send(ClientMessage.Land(), out _);
            yield return new WaitForSecondsRealtime(1f);
        }

        private bool HelpHas(string fragment)
        {
            foreach (Label l in Root.Query<Label>().ToList())
                if (l.text != null && l.text.Contains(fragment)) return true;
            return false;
        }
    }
}
#endif
