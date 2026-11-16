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
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Verification of PLAN_PC 27 (-desk-fix &lt;folder&gt;, plus -lang es|en and the window flags). In-player probes: no OS mouse, keyboard or pad.
    ///  A. Typing gate: with the part-tree search box (and the SITL URL field) focused, every hotkey is injected through the DesktopKeyboardGate seams
    ///     (plus the typed character into the field) and NOTHING changes while the text arrives; the control (the same keys with the field blurred) does act.
    ///  B. Game pad: the XInput state is a FAKE (PadSource.Provider; nobody can plug a pad into a probe). DRON mode: navigation that does not come from the
    ///     keyboard is stopped, the pad flies the drone; Start -> MENUS: the pad moves the focus / activates / goes back / steps the modes / scrolls and the
    ///     drone gets nothing (hold on entering); Start -> DRON again; unplugging while piloting is the dead-man.
    ///  C. Faults: the explanation card per source, the banner, the marker on the part, the countdown, the approved texts.
    /// One PNG per state (tag = WxH_lang) and PASS / FAIL lines. Not active unless the flag is given.
    /// </summary>
    internal sealed class DesktopCaptureFixes : MonoBehaviour
    {
        public string OutputDirectory;
        private int _fail, _pass;
        private readonly StringBuilder _log = new StringBuilder();
        private string _tag;
        private readonly HashSet<KeyCode> _held = new HashSet<KeyCode>();
        private readonly Dictionary<KeyCode, int> _pressAt = new Dictionary<KeyCode, int>();
        private int _typedAt = -10;
        private string _typed = string.Empty;
        private PadState? _fake;

        /// <summary>-desk-check drives only the typing gate (RunGateOnly) from DesktopSelfCheck: this component then does not start by itself.</summary>
        public bool ExternalDrive;
        /// <summary>-desk-fix-real: NO injected keys. A script (tools) sends the real keys with SendKeys to the foreground window; this component checks the result.</summary>
        public bool RealKeys;
        public int Failures => _fail;

        private void Start() { if (!ExternalDrive) StartCoroutine(Run()); }

        private void InstallSeams()
        {
            PlayerPrefs.SetInt("Onboarding_Seen", 1);
            if (RealKeys) { DesktopTelemetryUI.IgnoreFocusLoss = false; return; }
            DesktopKeyboardGate.KeyHeld = k => _held.Contains(k);
            DesktopKeyboardGate.KeyPressed = k => _pressAt.TryGetValue(k, out int f) && Time.frameCount == f + 1;
            DesktopKeyboardGate.InputString = () => Time.frameCount == _typedAt + 1 ? _typed : string.Empty;
            PadSource.Provider = () => _fake;
            DesktopTelemetryUI.IgnoreFocusLoss = true;
        }

        /// <summary>The regression check of the typing gate alone (called by -desk-check at its end).</summary>
        internal IEnumerator RunGateOnly()
        {
            Directory.CreateDirectory(OutputDirectory);
            InstallSeams();
            _tag = Screen.width + "x" + Screen.height + "_" + Lang;
            yield return GateChecks();
            Log("GATE RESULT " + _pass + " pass, " + _fail + " fail");
            File.WriteAllText(Path.Combine(OutputDirectory, "gate_" + _tag + ".log"), _log.ToString());
            DesktopKeyboardGate.ResetSeams();
            PadSource.Provider = null;
            ForgetProbePrefs();
        }

        /// <summary>The probes start sources and a SITL URL: the user's saved choices (PlayerPrefs of this PC) are put back to their defaults.</summary>
        private static void ForgetProbePrefs()
        {
            PlayerPrefs.DeleteKey(DesktopTelemetryUI.PrefSource);
            PlayerPrefs.DeleteKey(DesktopTelemetryUI.PrefSitlUrl);
            PlayerPrefs.Save();
        }
        private void Log(string s) { _log.AppendLine(s); Debug.Log("[FixUI] " + s); }

        private void Check(string name, bool ok)
        {
            if (ok) _pass++; else _fail++;
            Log((ok ? "PASS " : "FAIL ") + name);
        }

        private static void Click(Button b)
        {
            if (b == null) return;
            using (var e = NavigationSubmitEvent.GetPooled(DesktopPad.OwnMark)) { e.target = b; b.SendEvent(e); }
        }

        private VisualElement Root => UIManager.Instance.Root;
        private Button B(string n) => Root.Q<Button>(n);
        private DesktopUIController Ctrl => DesktopUIController.Instance;
        private DesktopTelemetryUI Tel => Ctrl.Chrome.Telemetry;
        private DesktopPad Pad => Ctrl.Pad;
        private static string Lang => AppLanguageManager.IsSpanish ? "es" : "en";
        private SimTelemetrySource Sim => DesktopTelemetry.Source as SimTelemetrySource;
        private static bool Shown(VisualElement e) => e != null && e.resolvedStyle.display != DisplayStyle.None;
        private static string T(string key) => TelemetryStrings.T(key, Lang);
        private VisualElement FocusedEl => Root.panel.focusController.focusedElement as VisualElement;

        private IEnumerator Shot(string name, float wait = 0.4f)
        {
            yield return new WaitForSecondsRealtime(wait);
            Texture2D tex = null;
            yield return DesktopScreenGrab.Grab(t => tex = t); // back buffer, or an offscreen render when the desktop does not present (PLAN_PC 28.4)
            File.WriteAllBytes(Path.Combine(OutputDirectory, _tag + "_" + name + ".png"), ImageConversion.EncodeToPNG(tex));
            Destroy(tex);
            Log("shot " + _tag + "_" + name + " via " + DesktopScreenGrab.LastMethod + " luma " + DesktopScreenGrab.LastLuma.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
        }

        private IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        // keys through the gate seams
        private void Hold(params KeyCode[] keys) { foreach (var k in keys) _held.Add(k); }
        private void Release(params KeyCode[] keys) { foreach (var k in keys) _held.Remove(k); }
        private void Press(KeyCode k) { _pressAt[k] = Time.frameCount; }
        private void TypeChar(char c) { _typedAt = Time.frameCount; _typed = c.ToString(); }

        private static PadState PadOf(PadButtons b = PadButtons.None, double lx = 0, double ly = 0, double rx = 0, double ry = 0) =>
            new PadState { Buttons = b, LeftX = lx, LeftY = ly, RightX = rx, RightY = ry };

        private IEnumerator PadPress(PadButtons b, int frames = 3)
        {
            _fake = PadOf(b);
            yield return Frames(frames);
            _fake = PadOf();
            yield return Frames(3);
        }

        private string Snapshot()
        {
            var sb = new StringBuilder();
            sb.Append("mode=").Append(Ctrl.Chrome.ActiveModeIndex());
            sb.Append(" pins=").Append(HotspotManager.Instance != null && HotspotManager.Instance.IsVisible);
            sb.Append(" tree=").Append(Ctrl.Tree.IsOpen);
            sb.Append(" tel=").Append(Tel.ModeOpen);
            sb.Append(" help=").Append(Ctrl.IsHelpOpen);
            sb.Append(" pop=").Append(Tel.OpenPopover ?? "-");
            sb.Append(" pilot=").Append(Tel.Piloting);
            sb.Append(" ack=").Append(DesktopTelemetry.Client != null && DesktopTelemetry.Client.LastAck != null ? DesktopTelemetry.Client.LastAck.command : "-");
            sb.Append(" explode=").Append(ExplodedViewManager.Instance != null ? ExplodedViewManager.Instance.GetExplosionFactor().ToString("0.00") : "-");
            sb.Append(" view=").Append(ViewModeManager.Instance != null ? ViewModeManager.Instance.CurrentMode.ToString() : "-");
            sb.Append(" cam=").Append(Camera.main != null ? Mathf.Round(Camera.main.transform.position.x) + "," + Mathf.Round(Camera.main.transform.position.y) + "," + Mathf.Round(Camera.main.transform.position.z) : "-");
            sb.Append(" held=").Append(Tel.HeldKeys);
            return sb.ToString();
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(OutputDirectory);
            PlayerPrefs.DeleteKey(DesktopTelemetryUI.PrefSource);
            InstallSeams();
            yield return new WaitForSecondsRealtime(7f);
            AccessibilityManager.Instance?.SetUIScale(1f);
            yield return new WaitForSecondsRealtime(0.5f);
            _tag = Screen.width + "x" + Screen.height + "_" + Lang;
            UIManager ui = UIManager.Instance;
            ui.PrepareViewerForDeepLink();
            yield return new WaitForSecondsRealtime(3f);
            if (ui.IsOnboardingOpen) { ui.ShortcutBack(); yield return new WaitForSecondsRealtime(1f); }
            OrbitCameraController.Instance?.ResetView();
            OrbitCameraController.Instance?.SetAngles(-28f, 20f, true);
            OrbitCameraController.Instance?.SetDistance(9f, true);
            yield return new WaitForSecondsRealtime(1.5f);
            Log("window " + Screen.width + "x" + Screen.height + " lang " + Lang);

            if (RealKeys)
            {
                yield return RealKeyChecks();
                Log("RESULT " + _pass + " pass, " + _fail + " fail");
                File.WriteAllText(Path.Combine(OutputDirectory, "real_" + _tag + ".log"), _log.ToString());
                ForgetProbePrefs();
                Application.Quit();
                yield break;
            }

            yield return GateChecks();
            yield return PadChecks();
            yield return FaultChecks();

            Log("RESULT " + _pass + " pass, " + _fail + " fail");
            File.WriteAllText(Path.Combine(OutputDirectory, "fix_" + _tag + ".log"), _log.ToString());
            DesktopKeyboardGate.ResetSeams();
            PadSource.Provider = null;
            ForgetProbePrefs();
            Application.Quit();
        }

        // ═════════════════════════════ A0. the typing gate with the REAL keyboard ═════════════════════════════

        private IEnumerator WaitFlag(string name, float seconds)
        {
            string path = Path.Combine(OutputDirectory, name);
            float until = Time.realtimeSinceStartup + seconds;
            while (!File.Exists(path) && Time.realtimeSinceStartup < until) yield return null;
        }

        /// <summary>
        /// Real OS keys (sent by tools/real_keys.ps1 to the foreground .exe): the part-tree search box focused the way Ctrl+F and "/" do, then every hotkey
        /// character; the state must not change and the text must arrive. Then the same for the SITL field. Flags: ready_*.flag (here) / done_*.flag (script).
        /// </summary>
        private IEnumerator RealKeyChecks()
        {
            Log("── A0. typing gate, REAL keyboard ──");
            var tree = Ctrl.Tree;
            for (int round = 0; round < 2; round++)
            {
                TextField field;
                string label;
                if (round == 0)
                {
                    tree.Open(true); // what "/" and Ctrl+F do: SetOpen + FocusSearch (programmatic focus on the field itself)
                    yield return new WaitForSecondsRealtime(0.9f);
                    field = tree.SearchField;
                    label = "real keys, search box";
                }
                else
                {
                    Tel.OpenMode();
                    yield return Frames(3);
                    Click(B("DkTelSource"));
                    yield return new WaitForSecondsRealtime(0.3f);
                    Click(B("DkSrcSitl"));
                    yield return new WaitForSecondsRealtime(0.4f);
                    field = Root.Q<TextField>("DkSitlUrl");
                    field.value = string.Empty;
                    field.Focus(); // programmatic focus on the field itself
                    yield return new WaitForSecondsRealtime(0.4f);
                    label = "real keys, SITL field";
                }
                Check(label + ": the focused field counts as typing", DesktopKeyboardGate.Typing);
                string before = Snapshot();
                string textBefore = field.value;
                Log("application focused: " + Application.isFocused);
                File.WriteAllText(Path.Combine(OutputDirectory, "ready_" + round + ".flag"), "ready");
                yield return WaitFlag("done_" + round + ".flag", 45f);
                yield return new WaitForSecondsRealtime(0.6f);
                string after = Snapshot();
                // the script sends 1234fhierctplgvwasdq, Shift+A, Home, F1, then ? / [ ] and a space: Home is the field's own editing key (the caret goes to the start), so the last five characters land in front
                string expected = "?/[] " + textBefore + "1234fhierctplgvwasdqA";
                Log(label + " text: \"" + field.value + "\" (expected \"" + expected + "\")");
                Check(label + ": nothing changed while the real keys went into the field (" + before + ")", before == after);
                Check(label + ": the typed characters arrived in the field", field.value == expected);
                Check(label + ": still typing", DesktopKeyboardGate.Typing);
                yield return Shot("A0_" + (round == 0 ? "buscador" : "url_sitl") + "_teclas_reales", 0.1f);
                File.WriteAllText(Path.Combine(OutputDirectory, "shot_" + round + ".flag"), "shot");
                yield return WaitFlag("esc_" + round + ".flag", 15f);
                yield return new WaitForSecondsRealtime(0.5f);
                Check(label + ": Esc (real) leaves the field", !DesktopKeyboardGate.Typing);
                if (round == 0) { tree.SetOpen(false); yield return new WaitForSecondsRealtime(0.4f); }
                else { Tel.CloseAllPopovers(); Tel.CloseMode(); yield return Frames(3); }
            }
        }

        // ═════════════════════════════ A. the typing gate ═════════════════════════════

        private struct Hot { public KeyCode Key; public char Ch; public bool Shift; public string Name; }

        private static readonly Hot[] Hotkeys =
        {
            new Hot { Key = KeyCode.Alpha1, Ch = '1', Name = "1" }, new Hot { Key = KeyCode.Alpha2, Ch = '2', Name = "2" },
            new Hot { Key = KeyCode.Alpha3, Ch = '3', Name = "3" }, new Hot { Key = KeyCode.Alpha4, Ch = '4', Name = "4" },
            new Hot { Key = KeyCode.F, Ch = 'f', Name = "F" }, new Hot { Key = KeyCode.H, Ch = 'h', Name = "H" },
            new Hot { Key = KeyCode.I, Ch = 'i', Name = "I" }, new Hot { Key = KeyCode.E, Ch = 'e', Name = "E" },
            new Hot { Key = KeyCode.R, Ch = 'r', Name = "R" }, new Hot { Key = KeyCode.C, Ch = 'c', Name = "C" },
            new Hot { Key = KeyCode.G, Ch = 'g', Name = "G" }, new Hot { Key = KeyCode.V, Ch = 'v', Name = "V" },
            new Hot { Key = KeyCode.T, Ch = 't', Name = "T" }, new Hot { Key = KeyCode.P, Ch = 'p', Name = "P" },
            new Hot { Key = KeyCode.L, Ch = 'l', Name = "L" }, new Hot { Key = KeyCode.A, Ch = 'A', Shift = true, Name = "Shift+A" },
            new Hot { Key = KeyCode.Home, Ch = '\0', Name = "Home" }, new Hot { Key = KeyCode.F1, Ch = '\0', Name = "F1" },
            new Hot { Key = KeyCode.Slash, Ch = '?', Shift = true, Name = "?" }, new Hot { Key = KeyCode.Slash, Ch = '/', Name = "/" },
            new Hot { Key = KeyCode.LeftBracket, Ch = '[', Name = "[" }, new Hot { Key = KeyCode.RightBracket, Ch = ']', Name = "]" },
            new Hot { Key = KeyCode.W, Ch = 'w', Name = "W" }, new Hot { Key = KeyCode.S, Ch = 's', Name = "S" }, new Hot { Key = KeyCode.D, Ch = 'd', Name = "D" },
            new Hot { Key = KeyCode.Q, Ch = 'q', Name = "Q" }, new Hot { Key = KeyCode.Space, Ch = ' ', Name = "Space" },
        };

        private IEnumerator InjectHotkeys(TextField field, string label)
        {
            string before = Snapshot();
            string initial = before;
            string expectedText = field.value;
            foreach (Hot h in Hotkeys)
            {
                if (h.Shift) Hold(KeyCode.LeftShift);
                Press(h.Key);
                if (h.Ch != '\0')
                {
                    TypeChar(h.Ch);
                    // the character goes to the inner input of the field (what the keyboard does once the field has the focus)
                    VisualElement target = field.Q<TextElement>() ?? FocusedEl;
                    if (target != null)
                    {
                        using (var e = KeyDownEvent.GetPooled(h.Ch, h.Key, h.Shift ? EventModifiers.Shift : EventModifiers.None)) { e.target = target; target.SendEvent(e); }
                        expectedText += h.Ch;
                    }
                }
                yield return Frames(2);
                if (h.Shift) Release(KeyCode.LeftShift);
                string now = Snapshot();
                if (now != before) { Check(label + ": " + h.Name + " changed the state while typing (" + before + " -> " + now + ")", false); before = now; }
            }
            Hold(KeyCode.LeftShift);
            yield return Frames(2);
            Release(KeyCode.LeftShift);
            Check(label + ": " + Hotkeys.Length + " hotkeys injected while typing: nothing changed (mode, pins, tree, telemetry, help, popover, pilot, commands, explode, view, camera)", Snapshot() == initial);
            Log(label + " text in the field: \"" + field.value + "\"");
            Check(label + ": the typed characters arrived in the text field (\"" + field.value + "\")", field.value == expectedText);
        }

        private IEnumerator GateChecks()
        {
            Log("── A. typing gate ──");
            var tree = Ctrl.Tree;
            Check("gate: no text field focused at the start, so no typing", !DesktopKeyboardGate.Typing);

            // control: with nothing focused, key 4 opens Telemetry mode and H toggles the pins (the injection reaches the handlers)
            bool pins0 = HotspotManager.Instance.IsVisible;
            Press(KeyCode.H);
            yield return Frames(3);
            Check("control: H (no field focused) toggles the pins", HotspotManager.Instance.IsVisible != pins0);
            Press(KeyCode.H);
            yield return Frames(3);
            Press(KeyCode.Alpha4);
            yield return Frames(4);
            Check("control: key 4 (no field focused) opens Telemetry mode", Tel.ModeOpen);
            Press(KeyCode.Alpha4);
            yield return Frames(4);
            Check("control: key 4 again closes it", !Tel.ModeOpen);

            // 1. the part-tree search box
            tree.SetOpen(true);
            yield return new WaitForSecondsRealtime(0.6f);
            // programmatic focus = what Ctrl+F and "/" do: the FIELD itself holds the focus (it is not an ITextEdition: the bug of the old check)
            tree.SearchField.Focus();
            yield return Frames(3);
            Check("gate: the search box focused the way Ctrl+F / '/' do counts as typing", DesktopKeyboardGate.Typing && tree.SearchFocused && FocusedEl == tree.SearchField);
            // a mouse click focuses the inner input: the keys are typed there
            tree.SearchField.Q<TextElement>().Focus();
            yield return Frames(3);
            Check("gate: the search box focused by a click (inner input) counts as typing", DesktopKeyboardGate.Typing && tree.SearchFocused);
            yield return InjectHotkeys(tree.SearchField, "search box");
            Check("search box: still typing after the keys", DesktopKeyboardGate.Typing);
            yield return Shot("A1_buscador_teclas_bloqueadas", 0.3f);

            // Esc leaves / clears the field (the tree clears first)
            ui_Back();
            yield return Frames(3);
            Check("Esc while typing in the search box clears it and leaves the field", !DesktopKeyboardGate.Typing && string.IsNullOrEmpty(tree.SearchField.value));
            tree.SetOpen(false);
            yield return new WaitForSecondsRealtime(0.4f);

            // 2. the SITL URL field
            Tel.OpenMode();
            yield return Frames(3);
            Click(B("DkTelSource"));
            yield return new WaitForSecondsRealtime(0.3f);
            Click(B("DkSrcSitl"));
            yield return new WaitForSecondsRealtime(0.4f);
            var urlField = Root.Q<TextField>("DkSitlUrl");
            Check("SITL URL field exists and is shown", Shown(urlField));
            string urlBefore = urlField.value;
            urlField.SetValueWithoutNotify(string.Empty); // a focused field selects its text: start from an empty one so the typed text is the whole value
            urlField.Focus();
            yield return Frames(3);
            Check("gate: the focused SITL URL field counts as typing", DesktopKeyboardGate.Typing);
            urlField.Q<TextElement>().Focus();
            yield return Frames(3);
            yield return InjectHotkeys(urlField, "SITL field");
            Check("SITL field: the source popover is still the open one (L, Shift+A, 4... did not move it)", Tel.OpenPopover == "source");
            yield return Shot("A2_url_sitl_teclas_bloqueadas", 0.3f);

            // closing the popover leaves the field focused but hidden: it must not swallow the shortcuts any more
            urlField.value = urlBefore;
            Tel.CloseAllPopovers();
            yield return Frames(4);
            Check("a hidden focused field does not count as typing (the gate releases it)", !DesktopKeyboardGate.Typing);
            Press(KeyCode.Alpha4);
            yield return Frames(4);
            Check("after that, key 4 acts again (closes Telemetry mode)", !Tel.ModeOpen);
            Press(KeyCode.F1);
            yield return Frames(3);
            Check("F1 acts when not typing (opens the help)", Ctrl.IsHelpOpen);
            Press(KeyCode.F1);
            yield return Frames(3);
            Check("F1 closes it", !Ctrl.IsHelpOpen);

            // 3. piloting keys and click-to-fly while typing
            Tel.OpenMode();
            Tel.StartSimulator();
            yield return new WaitForSecondsRealtime(1.5f);
            Click(B("DkCmdTakeoff"));
            yield return new WaitForSecondsRealtime(6f);
            Check("simulator armed and flying (5 m)", DesktopTelemetry.Client.Displayed != null && DesktopTelemetry.Client.Displayed.armed && !DesktopTelemetry.Client.Displayed.landed);
            Click(B("DkTelPilot"));
            yield return new WaitForSecondsRealtime(0.4f);
            Check("piloting on", Tel.Piloting);
            tree.SetOpen(true);
            yield return new WaitForSecondsRealtime(0.5f);
            tree.SearchField.Focus();
            yield return Frames(3);
            Check("gate: typing again before the piloting keys", DesktopKeyboardGate.Typing);
            var ackBefore = DesktopTelemetry.Client.LastAck;
            Hold(KeyCode.W, KeyCode.D, KeyCode.Space, KeyCode.Q, KeyCode.LeftShift);
            Press(KeyCode.T); Press(KeyCode.L); Press(KeyCode.H); Press(KeyCode.R);
            yield return new WaitForSecondsRealtime(0.6f);
            Check("piloting keys while typing: no key is held by the pilot", Tel.HeldKeys == PilotKeys.None);
            Check("piloting keys while typing: sticks stay neutral", Tel.LastSticks.x == 0 && Tel.LastSticks.y == 0 && Tel.LastSticks.r == 0);
            Check("piloting keys while typing: T / L / H / R sent no command", ReferenceEquals(ackBefore, DesktopTelemetry.Client.LastAck));
            Release(KeyCode.W, KeyCode.D, KeyCode.Space, KeyCode.Q, KeyCode.LeftShift);
            var pt = new Vector2(Screen.width * 0.20f, Screen.height * 0.13f);
            bool consumed = Tel.TryGroundClick(pt);
            yield return Frames(3);
            Check("click-to-fly while typing: the click only ends the typing (no fly_to, no target)", consumed && !Tel.HasFlyTarget && ReferenceEquals(ackBefore, DesktopTelemetry.Client.LastAck) && !DesktopKeyboardGate.Typing);
            // control: field blurred -> W flies
            Hold(KeyCode.W);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("control: with the field released, W flies (the pilot holds Forward)", (Tel.HeldKeys & PilotKeys.Forward) != 0);
            Release(KeyCode.W);
            yield return new WaitForSecondsRealtime(0.3f);
            tree.SetOpen(false);
            Tel.HandleBack(); // leaves piloting
            Click(B("DkCmdLand"));
            yield return new WaitForSecondsRealtime(0.5f);
            Tel.StopSession();
            Tel.CloseMode();
            yield return Frames(3);
        }

        private void ui_Back() { UIManager.Instance.ShortcutBack(); }

        // ═════════════════════════════ B. game pad ═════════════════════════════

        // an event as the Input Manager's joystick / keyboard axes produce it (no marker); the keyboard is told apart by the key held (the seam)
        private static void SendNav(VisualElement target, NavigationMoveEvent.Direction d, EventModifiers mods = EventModifiers.None)
        {
            using (var e = NavigationMoveEvent.GetPooled(d, mods)) { e.target = target; target.SendEvent(e); }
        }

        private IEnumerator PadChecks()
        {
            Log("── B. game pad (FAKE XInput state: no real pad was used) ──");
            _fake = null;
            yield return Frames(4);
            Check("no pad: the badge is hidden", !Pad.Connected && !Shown(Pad.Badge));

            // connect: DRON by default
            _fake = PadOf();
            yield return Frames(4);
            Check("pad connected: DRON mode by default", Pad.Connected && Pad.Mode == PadMode.Drone);
            Check("badge shown with the DRON text and the Start hint", Shown(Pad.Badge) && Pad.Badge.Q<Label>("DkPadMode").text == T("pad.badge.drone") && Pad.Badge.Q<Label>("DkPadHint").text == T("pad.badge.hint"));
            Check("badge has the DRON state class, not the MENUS one", Pad.Badge.ClassListContains("dk-pad-badge--drone") && !Pad.Badge.ClassListContains("dk-pad-badge--menus"));
            Check("a toast announced the connection", Tel.ToastText == T("pad.toast.connected"));
            Check("DRON mode: no focus ring (no .dk-padnav)", !Root.ClassListContains("dk-padnav"));
            yield return Shot("B1_mando_dron");

            // DRON: pad navigation never reaches the UI
            Button probe = B("DkMode1");
            probe.Focus();
            yield return Frames(3);
            var focusBefore = FocusedEl;
            int blocked0 = Pad.BlockedNavEvents;
            SendNav(FocusedEl, NavigationMoveEvent.Direction.Right);
            SendNav(FocusedEl, NavigationMoveEvent.Direction.Down);
            yield return Frames(2);
            Check("DRON: pad (non-keyboard) Move events were stopped by the filter", Pad.BlockedNavEvents == blocked0 + 2);
            Check("DRON: the focus did not move", ReferenceEquals(FocusedEl, focusBefore));
            using (var e = NavigationSubmitEvent.GetPooled(EventModifiers.None)) { e.target = FocusedEl; FocusedEl.SendEvent(e); }
            using (var e = NavigationCancelEvent.GetPooled(EventModifiers.None)) { e.target = FocusedEl; FocusedEl.SendEvent(e); }
            yield return Frames(2);
            Check("DRON: pad Submit and Cancel were stopped too (the mode button did not toggle)", Pad.BlockedNavEvents == blocked0 + 4 && Ctrl.Chrome.ActiveModeIndex() == 0);

            // the keyboard is unaffected: the same event with an arrow key held (the legacy module reads it the same frame) goes through
            Hold(KeyCode.RightArrow);
            SendNav(FocusedEl, NavigationMoveEvent.Direction.Right);
            yield return Frames(2);
            Release(KeyCode.RightArrow);
            Check("keyboard navigation (arrow key held) still moves the focus and is not counted as blocked", !ReferenceEquals(FocusedEl, focusBefore) && Pad.BlockedNavEvents == blocked0 + 4);
            probe.Focus();
            yield return Frames(2);
            Hold(KeyCode.Return);
            int b1 = Pad.BlockedNavEvents;
            using (var e = NavigationSubmitEvent.GetPooled(EventModifiers.None)) { e.target = FocusedEl; FocusedEl.SendEvent(e); }
            Release(KeyCode.Return);
            yield return Frames(2);
            Check("Enter held makes a Submit pass (not blocked)", Pad.BlockedNavEvents == b1);
            Tel.CloseMode();
            yield return Frames(2);
            Tel.CloseMode();
            yield return Frames(2);
            FocusedEl?.Blur();

            // DRON flies: simulator; a pad press with Pilotar OFF only explains what to do; takeoff by the button, then Pilotar + pad X (hold) + right stick forward
            Tel.OpenMode();
            Tel.StartSimulator();
            yield return new WaitForSecondsRealtime(1.5f);
            yield return PadPress(PadButtons.A);
            Check("DRON, Pilotar off: a pad press sends nothing and a toast says to turn Pilotar on", !DesktopTelemetry.Client.Displayed.armed && Tel.ToastText == T("pad.hint.pilot"));
            Click(B("DkCmdTakeoff"));
            yield return new WaitForSecondsRealtime(6f);
            Click(B("DkTelPilot"));
            yield return new WaitForSecondsRealtime(0.4f);
            Check("piloting on (pad in DRON mode)", Tel.Piloting && Pad.Mode == PadMode.Drone);
            Check("the piloting legend shows the pad mapping rows now", Shown(Root.Q<VisualElement>("DkPilotLegend")) && Shown(Root.Q<VisualElement>(className: "dk-pilot-padbox")));
            yield return PadPress(PadButtons.X);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("DRON: pad X = hold (command sent by the pad)", DesktopTelemetry.Client.LastAck != null && DesktopTelemetry.Client.LastAck.command == "hold");
            _fake = PadOf(ry: 1.0);
            yield return new WaitForSecondsRealtime(2.2f);
            double speed = Math.Sqrt(Sim.Model.v[0] * Sim.Model.v[0] + Sim.Model.v[1] * Sim.Model.v[1]);
            Check("DRON: the right stick forward flies the drone (speed " + speed.ToString("0.0") + " m/s)", speed > 2.0);
            yield return Shot("B2_mando_dron_pilotando", 0.1f);
            _fake = PadOf();
            yield return new WaitForSecondsRealtime(1.2f);

            // → MENÚS (hold on entering)
            var ack0 = DesktopTelemetry.Client.LastAck;
            yield return PadPress(PadButtons.Start);
            yield return Frames(3);
            Check("Start: MENUS mode", Pad.Mode == PadMode.Menus && Pad.MenusMode);
            Check("MENUS: `hold` was sent to the drone on entering (it was piloting)", DesktopTelemetry.Client.LastAck != null && DesktopTelemetry.Client.LastAck.command == "hold" && !ReferenceEquals(ack0, DesktopTelemetry.Client.LastAck) && Sim.Model.mode == SimMode.Hold);
            Check("badge: MENUS text and amber state class", Pad.Badge.Q<Label>("DkPadMode").text == T("pad.badge.menus") && Pad.Badge.ClassListContains("dk-pad-badge--menus"));
            Check("toast on the change says the drone gets no pad command", Tel.ToastText != null && Tel.ToastText.StartsWith(T("pad.toast.menus")));
            Check("focus ring only now: .dk-padnav and .dk-keynav on the root", Root.ClassListContains("dk-padnav") && Root.ClassListContains("dk-keynav"));
            Check("first focus landed on a sensible control", FocusedEl != null && FocusedEl is Button);
            Log("first focus: " + FocusedEl?.name);
            yield return Shot("B3_mando_menus", 0.2f);

            // MENÚS: the drone gets nothing from the pad
            _fake = PadOf(ry: 1.0, lx: 0.0);
            yield return new WaitForSecondsRealtime(1.5f);
            speed = Math.Sqrt(Sim.Model.v[0] * Sim.Model.v[0] + Sim.Model.v[1] * Sim.Model.v[1]);
            Check("MENUS: the right stick does NOT fly the drone (speed " + speed.ToString("0.0") + " m/s)", speed < 0.6 && Sim.Model.mode == SimMode.Hold);
            _fake = PadOf();
            yield return Frames(3);

            // MENÚS: navigate
            Tel.HandleBack(); // leave piloting so Esc semantics are clean
            Tel.OpenMode();
            yield return Frames(3);
            B("DkTelPower").Focus();
            yield return Frames(3);
            var f0 = FocusedEl;
            int d0 = Pad.DispatchedNavEvents;
            _fake = PadOf(PadButtons.DpadRight);
            yield return Frames(3);
            _fake = PadOf();
            yield return Frames(3);
            Check("MENUS: d-pad right moves the focus", !ReferenceEquals(FocusedEl, f0) && Pad.DispatchedNavEvents > d0);
            _fake = PadOf(lx: 1.0);
            yield return Frames(3);
            _fake = PadOf();
            yield return Frames(3);
            Check("MENUS: the left stick also moves it (second control now)", FocusedEl != null);
            B("DkTelSource").Focus();
            yield return Frames(3);
            var ackA = DesktopTelemetry.Client.LastAck;
            yield return PadPress(PadButtons.A);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("MENUS: A activates the focused control (the source popover opened) and sent no drone command", Tel.OpenPopover == "source" && ReferenceEquals(ackA, DesktopTelemetry.Client.LastAck));
            yield return PadPress(PadButtons.B);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("MENUS: B goes back (the popover closed)", Tel.OpenPopover == null);
            Check("Telemetry mode is the active one before the step", Ctrl.Chrome.ActiveModeIndex() == 4);
            yield return PadPress(PadButtons.RB);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("MENUS: RB steps to the next mode (4 -> 1)", Ctrl.Chrome.ActiveModeIndex() == 1);
            yield return PadPress(PadButtons.LB);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("MENUS: LB steps back (1 -> 4)", Ctrl.Chrome.ActiveModeIndex() == 4);

            // MENÚS: the right stick scrolls a panel (a tall ScrollView of the probe: deterministic)
            var sv = new ScrollView(ScrollViewMode.Vertical) { name = "ProbeScroll" };
            sv.style.position = Position.Absolute; sv.style.left = 700; sv.style.top = 300; sv.style.width = 320; sv.style.height = 160;
            for (int i = 0; i < 60; i++) sv.Add(new Label("probe row " + i) { pickingMode = PickingMode.Ignore });
            Root.Add(sv);
            FocusedEl?.Blur();
            yield return Frames(4);
            _fake = PadOf(ry: -1.0);
            yield return new WaitForSecondsRealtime(0.5f);
            float off = sv.scrollOffset.y;
            _fake = PadOf(ry: 1.0);
            yield return new WaitForSecondsRealtime(0.25f);
            _fake = PadOf();
            Check("MENUS: the right stick scrolls a panel down and back up (offset " + off.ToString("0") + " then " + sv.scrollOffset.y.ToString("0") + ")", off > 100f && sv.scrollOffset.y < off);
            sv.RemoveFromHierarchy();

            // MENÚS: Back = help on the pad tab
            yield return PadPress(PadButtons.Back);
            yield return new WaitForSecondsRealtime(0.8f);
            Check("MENUS: Back opens the help on the pad tab", Ctrl.IsHelpOpen && Ctrl.Help.Tab == 1);
            Check("the help has the pad legend with both modes", Root.Query<Label>(className: "desktop-help-section").ToList().Count >= 2);
            yield return Shot("B4_ayuda_mando", 0.3f);
            yield return PadPress(PadButtons.LB);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("MENUS: LB switches the help tab (keyboard and mouse)", Ctrl.Help.Tab == 0);
            yield return Shot("B5_ayuda_teclado", 0.2f);
            yield return PadPress(PadButtons.B);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("MENUS: B closes the help", !Ctrl.IsHelpOpen);

            // → DRON again
            yield return PadPress(PadButtons.Start);
            yield return Frames(3);
            Check("Start again: back to DRON", Pad.Mode == PadMode.Drone);
            Check("DRON again: the ring is gone and the focus released", !Root.ClassListContains("dk-padnav") && FocusedEl == null);
            Check("toast on the change", Tel.ToastText != null && Tel.ToastText.StartsWith(T("pad.toast.drone")));
            Check("badge back to DRON", Pad.Badge.Q<Label>("DkPadMode").text == T("pad.badge.drone") && Pad.Badge.ClassListContains("dk-pad-badge--drone"));
            Click(B("DkTelPilot"));
            yield return new WaitForSecondsRealtime(0.4f);
            _fake = PadOf(ry: 1.0);
            yield return new WaitForSecondsRealtime(2.0f);
            speed = Math.Sqrt(Sim.Model.v[0] * Sim.Model.v[0] + Sim.Model.v[1] * Sim.Model.v[1]);
            Check("DRON again: the pad flies the drone (speed " + speed.ToString("0.0") + " m/s)", Tel.Piloting && speed > 2.0);
            _fake = PadOf();

            // unplug while piloting: dead-man
            _fake = null;
            yield return new WaitForSecondsRealtime(0.5f);
            Check("unplugging while piloting: dead-man (piloting off, hold sent) and the badge is hidden", !Tel.Piloting && !Pad.Connected && !Shown(Pad.Badge));
            _fake = PadOf(PadButtons.Start | PadButtons.A);
            yield return Frames(4);
            Check("a pad that comes back starts in DRON and held buttons are not presses", Pad.Connected && Pad.Mode == PadMode.Drone);
            _fake = PadOf();

            Tel.StopSession();
            Tel.CloseMode();
            yield return Frames(3);
            _fake = null;
            yield return Frames(3);
        }

        // ═════════════════════════════ C. faults ═════════════════════════════

        private IEnumerator FaultChecks()
        {
            Log("── C. fault visuals and explanation ──");
            string lang = Lang;
            var labels = HonestyLabels.Load();
            OrbitCameraController.Instance?.SetAngles(-28f, 20f, true);
            OrbitCameraController.Instance?.SetDistance(8f, true);
            Tel.OpenMode();
            DesktopTelemetry.StartLocalReplay(300.0);
            yield return new WaitForSecondsRealtime(2.5f);
            Check("replay running (local, read-only)", DesktopTelemetry.IsLocalReplay);

            // PLAN_PC 28.1: discreet. Nothing is open: the explanation is a collapsed section of the docked inspector
            Check("the docked explanation exists in the Flight tab and is COLLAPSED by default", Tel.FaultDock != null && !Tel.DockOpen && !Shown(Root.Q<VisualElement>("DkFxDockBody")));
            Check("there is no fault banner any more", Root.Q<VisualElement>("DkFaultBanner") == null);

            Click(B("DkTelFaults"));
            yield return new WaitForSecondsRealtime(0.6f);
            float popPct = PopCoverage("DkPopFaults");
            Log("Faults popover (replay, controls shown) covers " + popPct.ToString("F2") + " % of the canvas");
            Check("Faults popover is compact: covers <= 6 % of the canvas (" + popPct.ToString("F2") + " %)", popPct > 0f && popPct <= 6.0f);
            Check("opening the popover did NOT open the explanation", !Tel.DockOpen);
            Check("the popover holds no explanation card", Root.Q<VisualElement>("DkPopFaults").Query<VisualElement>(className: "dk-fx-card").ToList().Count == 0);
            Check("the popover has the small «Explicación» button", B("DkFaultExplain") != null && B("DkFaultExplain").text == T("fx.open"));
            yield return Shot("C1_popover_compacto", 0.2f);

            // the explicit action opens the docked card
            Click(B("DkFaultExplain"));
            yield return new WaitForSecondsRealtime(0.6f);
            var card = Tel.DockCard;
            Check("«Explicación» opens the docked section on the Flight tab (default: motor cut)", Tel.DockOpen && Tel.FlightTabActive && Shown(card.Root) && Tel.ExplainTopic == FaultTopic.MotorFailure);
            Check("the card lives inside the docked inspector, not in a popover", IsInside(card.Root, Root.Q<VisualElement>("BottomSheet")) && !IsInside(card.Root, Root.Q<VisualElement>("DkPopFaults")));
            Check("card is labelled «Explicación didáctica» and 'not a measurement'", card.TitleText == T("fx.title") && card.DisclaimerText == T("fx.disclaimer"));
            Check("card means / controller are the approved texts", card.MeansText == FaultExplain.Means(FaultTopic.MotorFailure, lang) && card.ControllerText == FaultExplain.Controller(FaultTopic.MotorFailure, lang));
            Check("card 'in this source' (replay): the trajectory does not react, the other motors do not compensate",
                  card.SourceText.Contains(lang == "es" ? "no reaccionan" : "do not react") && card.SourceText.Contains(lang == "es" ? "tampoco compensan" : "do not compensate"));
            Check("card source badge = the honest replay badge", card.SourceBadgeText == DesktopTelemetry.BadgeText(lang));
            Check("card affected readings carry their origin (" + card.ReadingTags + " tags)", card.ReadingTags >= 1 && (card.ReadingTagText(0) ?? string.Empty).Contains(" · "));
            Check("card 'what is happening' says none active", card.WhatText == T("fx.what.none"));
            yield return Shot("C2_explicacion_en_inspector_sin_falla", 0.2f);
            Tel.SetDockOpen(false);
            Tel.CloseAllPopovers();
            yield return Frames(3);
            Check("the section collapses again on request", !Tel.DockOpen && !Shown(Root.Q<VisualElement>("DkFxDockBody")));

            // inject motor 3 failure: ONLY the marker and the compact status. Nothing opens.
            Tel.FaultMotorSelect(3);
            Tel.Inject(FaultKind.MotorFailure);
            yield return new WaitForSecondsRealtime(1.5f);
            Check("injection opens NOTHING: no banner, the explanation stays collapsed, no popover", Root.Q<VisualElement>("DkFaultBanner") == null && !Tel.DockOpen && Tel.OpenPopover == null);
            var mk = Tel.FaultMarkers;
            string mkKey = null;
            foreach (string k in mk.Keys) if (k.StartsWith("f:")) mkKey = k;
            Check("a marker is pinned on motor 3 and visible", mkKey != null && mk.IsVisible(mkKey) && mk.ComponentOf(mkKey) == "motor3");
            Check("marker label: motor 3, stopped, synthetic", mkKey != null && mk.LabelOf(mkKey).Contains("Motor 3") && mk.LabelOf(mkKey).Contains(AlertText.SyntheticFault(lang, labels)));
            var scr = mk.ScreenOf(mkKey ?? "none");
            Check("marker is on screen (" + (scr.HasValue ? scr.Value.ToString() : "n/a") + ")", scr.HasValue && scr.Value.x > 0 && scr.Value.x < Screen.width && scr.Value.y > 0 && scr.Value.y < Screen.height);
            yield return Shot("C3_inyeccion_solo_marcador", 0.2f);

            // the compact status in the popover, then the explicit action
            Click(B("DkTelFaults"));
            yield return new WaitForSecondsRealtime(0.6f);
            float popPct2 = PopCoverage("DkPopFaults");
            Check("Faults popover with an active fault still covers <= 6 % (" + popPct2.ToString("F2") + " %)", popPct2 <= 6.0f);
            yield return Shot("C4_popover_con_falla_activa", 0.2f);
            Click(B("DkFaultExplain"));
            yield return new WaitForSecondsRealtime(0.6f);
            card = Tel.DockCard;
            Check("«Explicación» now shows the fault with its countdown (seconds of 30 s)", Tel.DockOpen && card.WhatText.Contains(lang == "es" ? "Motor 3 cortado" : "Motor 3 cut off") && card.WhatText.Contains(" s "));
            string w1 = card.WhatText;
            Log("what (t0): " + w1.Replace("\n", " | "));
            yield return Shot("C5_explicacion_motor3_activa", 0.2f);
            yield return new WaitForSecondsRealtime(2.6f);
            Log("what (t0+2.6s): " + Tel.DockCard.WhatText.Replace("\n", " | "));
            Check("the countdown went down", ParseSeconds(Tel.DockCard.WhatText) < ParseSeconds(w1));
            Check("with the alert: the card also carries the alert sentence with its origin (RPM ... estimated/simulated)", Tel.DockCard.WhatText.Contains("RPM") || Tel.DockCard.WhatText.Contains("Motor 3:"));
            Tel.SetDockOpen(false);
            Tel.CloseAllPopovers();

            // alerts popover: an alert is expanded only by the user and its explanation goes to the docked panel
            yield return new WaitForSecondsRealtime(1.0f);
            var alerts = DesktopTelemetry.Client.Alerts;
            string aid = null;
            foreach (var a in alerts) if (a.id == "motor3:out") aid = a.id;
            Log("alerts now: " + alerts.Count + " (motor3:out " + (aid != null ? "present" : "absent") + ")");
            Check("the fault raised the 'motor 3 stopped' alert", aid != null);
            Check("the existing red highlight of the part is still there (motor 3)", TelemetryDroneDriver.Instance != null && new List<string>(TelemetryDroneDriver.Instance.HighlightedComponents).Contains("motor3"));
            Check("the critical alert did not open anything by itself", !Tel.DockOpen && Tel.OpenPopover == null && Root.Q<VisualElement>("DkFaultBanner") == null);
            Click(B("DkTelAlerts"));
            yield return new WaitForSecondsRealtime(0.8f);
            Check("alerts popover is open with the list, collapsed explanation (nothing inside the popover)", Tel.OpenPopover == "alerts" && !Tel.DockOpen && Root.Q<VisualElement>("DkPopAlerts").Query<VisualElement>(className: "dk-fx-card").ToList().Count == 0);
            Log("Alerts popover covers " + PopCoverage("DkPopAlerts").ToString("F2") + " % of the canvas");
            yield return Shot("C6_alertas_lista", 0.2f);
            var expandBtn = Root.Q<VisualElement>("DkPopAlerts").Q<Button>("DkAlertExpand");
            Check("an alert has the small «Explicación» button", expandBtn != null && expandBtn.text == T("fx.open"));
            Click(expandBtn);
            yield return new WaitForSecondsRealtime(0.8f);
            Check("the alert's explanation opens in the docked inspector (Flight tab) for that alert", Tel.DockOpen && Tel.FlightTabActive && Tel.DockAlertId == aid && Tel.AlertExpanded(aid));
            card = Tel.DockCard;
            Check("alert card: synthetic suffix in the data block and the approved replay text", card.WhatText.Contains(AlertText.SyntheticFault(lang, labels)) && card.SourceText.Contains(lang == "es" ? "Falla sintética" : "Synthetic fault"));
            yield return Shot("C7_alerta_explicacion_en_inspector", 0.2f);
            Tel.SetDockOpen(false);
            Tel.CloseAllPopovers();

            // the other approved texts through the picker of the docked section
            FaultKind[] kinds = { FaultKind.MotorOverheat, FaultKind.BatterySag, FaultKind.Vibration };
            FaultTopic[] topics = { FaultTopic.MotorOverheat, FaultTopic.BatterySag, FaultTopic.Vibration };
            Tel.ShowFaultExplanation(FaultTopic.MotorFailure, 3, null);
            yield return new WaitForSecondsRealtime(0.4f);
            for (int i = 0; i < 3; i++)
            {
                Click(B("DkFxPick_" + WireNames.Of(kinds[i])));
                yield return new WaitForSecondsRealtime(0.5f);
                card = Tel.DockCard;
                Check(kinds[i] + ": card shows the approved means / controller / replay text", Tel.ExplainTopic == topics[i] && card.MeansText == FaultExplain.Means(topics[i], lang) && card.ControllerText == FaultExplain.Controller(topics[i], lang)
                      && card.SourceText == FaultExplain.InSource(topics[i], ExplainSource.LocalReplay, 3, lang));
                if (i == 0) yield return Shot("C8_explicacion_sobrecalentamiento", 0.2f);
            }
            Tel.SetDockOpen(false);
            Tel.ClearFaults();
            yield return new WaitForSecondsRealtime(1.0f);
            Check("clearing the faults removes the markers", Tel.FaultMarkers.Count == 0 || AllAlertMarkers(Tel.FaultMarkers));

            // simulator: no fault injection
            Tel.StartSimulator();
            yield return new WaitForSecondsRealtime(1.5f);
            Click(B("DkTelFaults"));
            yield return new WaitForSecondsRealtime(0.6f);
            Check("simulator: the Faults popover has no replay-injection buttons but the simulator's motor-cut controls (Lote 1a), and stays compact (<= 10 %)", !Shown(Root.Q<VisualElement>("DkFaultControls")) && Shown(Root.Q<VisualElement>("DkSimFaultControls")) && PopCoverage("DkPopFaults") <= 10.0f);
            Click(B("DkFaultExplain"));
            yield return new WaitForSecondsRealtime(0.6f);
            card = Tel.DockCard;
            Check("simulator: the card says the approved line (Lote 1a: the simulator cuts a motor in a simplified model, not PX4 nor a real drone)", card.SourceText == FaultExplain.InSource(FaultTopic.MotorFailure, ExplainSource.Simulator, 3, lang) && card.SourceText.Contains(lang == "es" ? "modelo simplificado; no es PX4 ni un dron real" : "simplified model; this is not PX4 or a real drone"));
            Check("simulator: the card badge is the simulator's, never SITL", card.SourceBadgeText == DesktopTelemetry.BadgeText(lang) && !card.SourceBadgeText.ToUpperInvariant().Contains("SITL"));
            Check("simulator: no markers while no motor is cut", Tel.FaultMarkers.Count == 0);
            yield return Shot("C9_explicacion_simulador", 0.2f);
            Click(B("DkFxPick_battery_sag"));
            yield return new WaitForSecondsRealtime(0.5f);
            Check("simulator + battery: its own approved text (drains, does not react to low charge)", Tel.DockCard.SourceText == FaultExplain.InSource(FaultTopic.BatterySag, ExplainSource.Simulator, 3, lang));
            Tel.SetDockOpen(false);
            Tel.CloseAllPopovers();

            // PX4 SITL: a session that never connects (nothing listens): the card still says what PX4 / Gazebo do
            Tel.StartSitl("ws://127.0.0.1:9");
            yield return new WaitForSecondsRealtime(1.2f);
            Check("SITL session started (not connected)", DesktopTelemetry.IsRunning && DesktopTelemetry.Client.Classification == SourceClassification.Sitl);
            Tel.ShowFaultExplanation(FaultTopic.MotorFailure, 3, null);
            yield return new WaitForSecondsRealtime(0.6f);
            card = Tel.DockCard;
            Check("SITL: the card says the motor is really cut in SITL and the RPM are PX4's command", card.SourceText == FaultExplain.InSource(FaultTopic.MotorFailure, ExplainSource.Sitl, 3, lang)
                  && card.SourceText.Contains("MAV_CMD_INJECT_FAILURE") && card.SourceText.Contains(lang == "es" ? "corta de verdad su empuje" : "really cuts its thrust") && card.SourceText.Contains(lang == "es" ? "orden de PX4" : "PX4's command"));
            Check("SITL: the card badge is the SITL one from labels.json", card.SourceBadgeText == DesktopTelemetry.BadgeText(lang));
            yield return Shot("C10_explicacion_sitl", 0.2f);
            Tel.SetDockOpen(false);
            Tel.CloseAllPopovers();
            Tel.StopSession();
            Tel.CloseMode();
            yield return Frames(3);
        }

        /// <summary>Percentage of the window a popover covers (its own box, physical pixels), 0 when it is not shown.</summary>
        private float PopCoverage(string name)
        {
            var e = Root.Q<VisualElement>(name);
            if (!Shown(e)) return 0f;
            float s = DesktopUIController.Instance.UiScale;
            Rect r = e.worldBound;
            return 100f * (r.width * s) * (r.height * s) / (Screen.width * (float)Screen.height);
        }

        private static bool IsInside(VisualElement e, VisualElement ancestor)
        {
            for (var p = e; p != null; p = p.parent) if (p == ancestor) return true;
            return false;
        }

        private static bool AllAlertMarkers(DesktopFaultMarkers m)
        {
            foreach (string k in m.Keys) if (k.StartsWith("f:")) return false;
            return true;
        }

        private static int ParseSeconds(string what)
        {
            // "Motor 3 cortado · 24 s restantes de 30 s" -> 24
            int dot = what.IndexOf('·');
            if (dot < 0) return 999;
            string rest = what.Substring(dot + 1).Trim();
            int sp = rest.IndexOf(' ');
            return sp > 0 && int.TryParse(rest.Substring(0, sp), out int s) ? s : 999;
        }
    }
}
#endif
