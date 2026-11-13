#if UNITY_STANDALONE
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.Telemetry;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PC only (E-6, PLAN_PC 24). Piloting in the Telemetry tool strip:
    ///  - a second pill under the strip: the "Pilotar" toggle and the four command buttons (takeoff, land, hold, return), enabled when the
    ///    active source takes commands (the flight simulator now, PX4 SITL when connected; never a replay, which is read-only);
    ///  - while piloting is ON the keys W A S D / arrows, Q E, Space / Shift fly the drone and T L H R send takeoff / land / hold / return.
    ///    Those four letters (and Shift+A) override the desktop shortcuts ONLY while piloting is on: DesktopUIController asks <see cref="Piloting"/>;
    ///    leaving piloting (the toggle, Esc, the source stopping, the window losing focus) gives every shortcut back;
    ///  - a small key legend on the viewport (keys light up while held), a toast for refusals and geofence limits;
    ///  - click on the ground grid = fly_to (SelectionManager background click hook -> <see cref="TryGroundClick"/>);
    ///  - dead-man: the window losing focus, the app pausing, piloting being switched off or the pad disconnecting sends `hold`; the
    ///    simulator itself also holds after 0.6 s without stick samples;
    ///  - Windows pad through XInput (Telemetry/Sim/XInputPad.cs): mode 2 sticks, A/B/X/Y = takeoff / land / hold / return.
    /// All maths (keys to sticks, expo, geofence) is the pure <see cref="PilotControls"/>; the model is the simulator or SITL behind the same messages.
    /// </summary>
    internal sealed partial class DesktopTelemetryUI
    {
        private VisualElement _pilotStrip, _legend, _legendKeys;
        private Button _btnPilot;
        private readonly Button[] _btnCmd = new Button[4];
        private readonly Label[] _cmdLabels = new Label[4];
        private Label _pilotLabel, _toast, _legendTitle, _legendPad;
        private readonly Dictionary<string, Label> _caps = new Dictionary<string, Label>();
        private readonly List<Label> _legendTexts = new List<Label>();

        /// <summary>Input seams: the verification harness (in-player probes) injects the same key events the pilot reads. Default = the old Input Manager.</summary>
        internal static Func<KeyCode, bool> KeyHeld = Input.GetKey;
        internal static Func<KeyCode, bool> KeyPressed = Input.GetKeyDown;
        /// <summary>Verification only: the harness runs in a window that may not have the OS focus, so it asks for focus loss to be ignored except in its own dead-man probe.</summary>
        internal static bool IgnoreFocusLoss;

        private bool _pilotOn;
        private bool _pilotFlagLost;       // land / hold / RTL gave the sticks back to the autopilot: the next stick touch sends `pilot` again
        private bool _pilotAuto;            // a click-to-fly is flying: no stick stream until the pilot touches a stick again
        private PilotKeys _held;
        private double _nextManual;
        private bool _padSeen;
        private bool[] _padButtons = new bool[4];
        private AckMessage _lastAckToast;
        private int _geoTouch;
        private float _geoToastAt = -10f;
        private float _toastUntil;
        private bool _hasTarget;
        private double _targetN, _targetE;
        private SimSticks _lastSticks = SimSticks.Neutral;

        /// <summary>True while piloting is ON: the keys of <see cref="PilotControls"/> belong to the pilot, not to the desktop shortcuts.</summary>
        public bool Piloting => _pilotOn;
        public Button PilotButton => _btnPilot;
        public Button CommandButton(PilotCommand c) => _btnCmd[(int)c];
        public SimSticks LastSticks => _lastSticks;
        public PilotKeys HeldKeys => _held;
        public bool HasFlyTarget => _hasTarget;
        public string ToastText => _toast != null && _toast.style.display != DisplayStyle.None ? _toast.text : null;
        public bool LegendVisible => _legend != null && _legend.style.display != DisplayStyle.None;
        public VisualElement PilotStrip => _pilotStrip;

        // ───────────────────────── build ─────────────────────────

        private void BuildPilotStrip()
        {
            _pilotStrip = new VisualElement { name = "DkTelPilotStrip" };
            _pilotStrip.AddToClassList("dk-strip");
            _pilotStrip.AddToClassList("dk-strip--tel");
            _pilotStrip.AddToClassList("dk-strip--pilot");

            _btnPilot = StripButton("DkTelPilot", "gamepad", out _pilotLabel, TogglePilot);
            _pilotStrip.Add(_btnPilot);
            _pilotStrip.Add(MakeSep());

            PilotCommand[] cmds = { PilotCommand.Takeoff, PilotCommand.Land, PilotCommand.Hold, PilotCommand.Rtl };
            string[] icons = { "takeoff", "land", "hold", "rtl" };
            string[] names = { "DkCmdTakeoff", "DkCmdLand", "DkCmdHold", "DkCmdRtl" };
            string[] keys = { "T", "L", "H", "R" };
            for (int i = 0; i < 4; i++)
            {
                PilotCommand c = cmds[i];
                Label lbl;
                Button b = StripButton(names[i], icons[i], out lbl, () => SendPilotCommand(c, "button"));
                b.Add(Kbd(keys[i]));
                _btnCmd[i] = b;
                _cmdLabels[i] = lbl;
                _pilotStrip.Add(b);
            }

            _container.Add(_pilotStrip);
        }

        private void BuildPilotOverlay()
        {
            // key legend on the viewport (bottom centre, above the status bar); never takes the pointer
            _legend = new VisualElement { name = "DkPilotLegend", pickingMode = PickingMode.Ignore };
            _legend.AddToClassList("dk-pilot-legend");
            _legend.style.display = DisplayStyle.None;

            var head = new VisualElement { pickingMode = PickingMode.Ignore };
            head.AddToClassList("dk-pilot-head");
            var dot = new VisualElement { pickingMode = PickingMode.Ignore };
            dot.AddToClassList("dk-dot");
            dot.AddToClassList("dk-dot--live");
            head.Add(dot);
            _legendTitle = new Label { pickingMode = PickingMode.Ignore };
            _legendTitle.AddToClassList("dk-pilot-title");
            head.Add(_legendTitle);
            _legendPad = new Label { pickingMode = PickingMode.Ignore };
            _legendPad.AddToClassList("dk-pilot-pad");
            head.Add(_legendPad);
            _legend.Add(head);

            _legendKeys = new VisualElement { pickingMode = PickingMode.Ignore };
            _legendKeys.AddToClassList("dk-pilot-rows");
            _legend.Add(_legendKeys);
            // rows are filled by RelabelPilot (language)

            _toast = new Label { name = "DkToast", pickingMode = PickingMode.Ignore };
            _toast.AddToClassList("dk-toast");
            _toast.style.display = DisplayStyle.None;

            _root.Add(_legend);
            _root.Add(_toast);
        }

        private VisualElement LegendRow(params object[] parts)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("dk-pilot-row");
            foreach (object p in parts)
            {
                if (p is string[] caps)
                {
                    foreach (string c in caps)
                    {
                        var cap = new Label(CapText(c)) { pickingMode = PickingMode.Ignore };
                        cap.AddToClassList("dk-kbd");
                        cap.AddToClassList("dk-pilot-cap");
                        row.Add(cap);
                        _caps[c] = cap;
                    }
                }
                else
                {
                    var t = new Label((string)p) { pickingMode = PickingMode.Ignore };
                    t.AddToClassList("dk-pilot-text");
                    row.Add(t);
                    _legendTexts.Add(t);
                }
            }
            return row;
        }

        private string CapText(string id)
        {
            switch (id)
            {
                case "Space": return T("pilot.key.space");
                case "Shift": return T("pilot.key.shift");
                default: return id;
            }
        }

        // ───────────────────────── texts ─────────────────────────

        private void RelabelPilot()
        {
            if (_pilotStrip == null) return;
            _pilotLabel.text = T("pilot.toggle");
            string[] ck = { "cmd.takeoff", "cmd.land", "cmd.hold", "cmd.rtl" };
            for (int i = 0; i < 4; i++)
            {
                _cmdLabels[i].text = T(ck[i]);
                _btnCmd[i].tooltip = T(ck[i] + ".tip");
            }
            _btnPilot.tooltip = T(DesktopTelemetry.AcceptsCommands || _pilotOn ? "pilot.toggle.tip" : "pilot.toggle.off.tip");

            _legendTitle.text = T("pilot.legend.title");
            _legendKeys.Clear();
            _caps.Clear();
            _legendTexts.Clear();
            _legendKeys.Add(LegendRow(new[] { "W", "A", "S", "D" }, T("pilot.legend.move"), new[] { "Q", "E" }, T("pilot.legend.yaw"), new[] { "Space", "Shift" }, T("pilot.legend.alt")));
            _legendKeys.Add(LegendRow(new[] { "T" }, T("cmd.takeoff"), new[] { "L" }, T("cmd.land"), new[] { "H" }, T("cmd.hold"), new[] { "R" }, T("cmd.rtl"), new[] { "Esc" }, T("pilot.legend.release")));
            _lastPadText = null;
        }

        private string _lastPadText;

        // ───────────────────────── toggle, commands, dead-man ─────────────────────────

        /// <summary>The honest reason a command cannot be sent now (a replay is read-only; no session; SITL not connected yet).</summary>
        private string NotAcceptedText()
        {
            if (!DesktopTelemetry.IsRunning) return T("toast.nosession");
            if (DesktopTelemetry.IsLocalReplay || DesktopTelemetry.Client.Classification == SourceClassification.Replay) return T("toast.replay");
            return T("toast.nolink");
        }

        public void TogglePilot()
        {
            SetPilot(!_pilotOn, "toggle");
            _btnPilot?.Blur(); // Space must fly the drone, not press this button again
        }

        /// <summary>Turns piloting on or off. On needs a source that takes commands; off sends `hold` (the dead-man of every way out).</summary>
        public bool SetPilot(bool on, string why)
        {
            if (on == _pilotOn) return _pilotOn;
            var client = DesktopTelemetry.Client;
            if (on)
            {
                if (!DesktopTelemetry.AcceptsCommands) { ShowToast(NotAcceptedText(), true); return false; }
                _pilotOn = true;
                _pilotAuto = false;
                _pilotFlagLost = false;
                _held = PilotKeys.None;
                _nextManual = 0;
                _lastSticks = SimSticks.Neutral;
                client.Send(ClientMessage.Pilot(), out _);
                BlurFocus();
                Debug.Log("[Pilot] ON (" + why + "): the keys W A S D Q E Space Shift T L H R belong to the pilot; L E H R T and Shift+A are paused as desktop shortcuts");
            }
            else
            {
                _pilotOn = false;
                _held = PilotKeys.None;
                _lastSticks = SimSticks.Neutral;
                if (client != null && DesktopTelemetry.AcceptsCommands) client.Send(ClientMessage.Hold(), out _);
                Debug.Log("[Pilot] OFF (" + why + "): hold sent; the desktop shortcuts are back");
            }
            _btnPilot.EnableInClassList("dk-strip-btn--on", _pilotOn);
            _legend.style.display = _pilotOn ? DisplayStyle.Flex : DisplayStyle.None;
            RelabelPilot();
            return _pilotOn;
        }

        /// <summary>Window focus lost / app paused / pad gone: piloting ends and the drone holds (the web does the same).</summary>
        public void PilotDeadMan(string reason)
        {
            if (!_pilotOn) return;
            SetPilot(false, "dead-man: " + reason);
            ShowToast(T("toast.deadman"), true);
        }

        public bool SendPilotCommand(PilotCommand c, string via)
        {
            var client = DesktopTelemetry.Client;
            if (client == null || !DesktopTelemetry.AcceptsCommands)
            {
                ShowToast(NotAcceptedText(), true);
                Debug.Log("[Pilot] " + PilotControls.NameOf(c) + " refused (" + via + "): " + NotAcceptedText());
                return false;
            }
            bool ok = client.Send(PilotControls.CommandMessage(c), out string reason);
            _pilotAuto = false;
            if (ok && _pilotOn && c != PilotCommand.Takeoff) _pilotFlagLost = true; // the model drops the pilot flag on hold / land / rtl
            if (c != PilotCommand.Takeoff) ClearFlyTarget();
            if (!ok) ShowToast(reason, true);
            Debug.Log("[Pilot] " + PilotControls.NameOf(c) + " sent (" + via + "): " + (ok ? "ok" : "not sent: " + reason));
            return ok;
        }

        private void BlurFocus()
        {
            var fe = _root?.panel?.focusController?.focusedElement as VisualElement;
            if (fe != null && !(fe is ITextEdition) && !IsInsideTextField(fe)) fe.Blur();
        }

        private static bool IsInsideTextField(VisualElement e)
        {
            for (; e != null; e = e.parent) if (e is ITextEdition) return true;
            return false;
        }

        private bool TypingNow()
        {
            var fe = _root?.panel?.focusController?.focusedElement as VisualElement;
            return IsInsideTextField(fe);
        }

        // ───────────────────────── toast ─────────────────────────

        public void ShowToast(string text, bool warn, float seconds = 3.5f)
        {
            if (_toast == null || string.IsNullOrEmpty(text)) return;
            _toast.text = text;
            _toast.EnableInClassList("dk-toast--warn", warn);
            _toast.style.display = DisplayStyle.Flex;
            _toast.BringToFront();
            _toastUntil = Time.unscaledTime + seconds;
            Debug.Log("[Pilot] toast: " + text);
        }

        // ───────────────────────── click to fly ─────────────────────────

        /// <summary>
        /// Background click hook of the selection system: a click on the ground (no part, nothing selected) sends `fly_to` there when the
        /// source takes commands and the drone is flying. True = consumed (no deselection).
        /// </summary>
        public bool TryGroundClick(Vector2 screen)
        {
            var client = DesktopTelemetry.Client;
            var drv = TelemetryDroneDriver.Instance;
            if (client == null || drv == null || !DesktopTelemetry.AcceptsCommands) return false;
            TelemetryFrame f = client.Displayed;
            if (f == null || !f.armed || f.landed) return false;
            if (!drv.TryGroundPoint(screen, out double n, out double e, out Vector3 world)) return false;
            return FlyTo(n, e, world);
        }

        /// <summary>fly_to a NED point (metres from the take-off point), at the current altitude; beyond the geofence it is clamped and a toast says so.</summary>
        public bool FlyTo(double north, double east, Vector3? worldForMarker = null)
        {
            var client = DesktopTelemetry.Client;
            if (client == null || !DesktopTelemetry.AcceptsCommands) { ShowToast(NotAcceptedText(), true); return false; }
            bool beyond = DesktopTelemetry.IsSimulator && PilotControls.BeyondGeofence(north, east);
            if (beyond)
            {
                PilotControls.ClampToGeofence(ref north, ref east);
                ShowToast(TelemetryStrings.F("toast.geofence.target", Lang, "r", SimLimits.Radius.ToString("0")), true);
            }
            double alt = PilotControls.FlyToAltitude(client.Displayed);
            bool ok = client.Send(ClientMessage.FlyTo(north, east, alt), out string reason);
            Debug.Log("[Pilot] fly_to N " + north.ToString("0.0") + " E " + east.ToString("0.0") + " alt " + alt.ToString("0") + (beyond ? " (clamped to the geofence)" : string.Empty) + ": " + (ok ? "sent" : reason));
            if (!ok) { ShowToast(reason, true); return false; }
            _pilotAuto = true; // the click flies: do not override it with a neutral stick stream until the pilot touches a stick
            _hasTarget = true;
            _targetN = north;
            _targetE = east;
            var d = TelemetryDroneDriver.Instance;
            if (d != null) d.SetFlyTarget(north, east);
            return true;
        }

        private void ClearFlyTarget()
        {
            if (!_hasTarget) return;
            _hasTarget = false;
            TelemetryDroneDriver.Instance?.SetFlyTarget(null, null);
        }

        // ───────────────────────── per frame ─────────────────────────

        private void TickPilot()
        {
            if (_pilotStrip == null) return;
            bool accepts = DesktopTelemetry.AcceptsCommands;
            var client = DesktopTelemetry.Client;

            // buttons: the toggle needs a source that takes commands; the command buttons stay clickable (a refusal explains why)
            _btnPilot.SetEnabled(accepts || _pilotOn);
            for (int i = 0; i < 4; i++) _btnCmd[i].EnableInClassList("dk-strip-btn--dim", !accepts);
            _btnPilot.EnableInClassList("dk-strip-btn--on", _pilotOn);
            string tip = T(accepts || _pilotOn ? "pilot.toggle.tip" : "pilot.toggle.off.tip");
            if (_btnPilot.tooltip != tip) _btnPilot.tooltip = tip;

            if (_toast.style.display != DisplayStyle.None && Time.unscaledTime >= _toastUntil) _toast.style.display = DisplayStyle.None;

            if (_pilotOn)
            {
                if (!accepts) SetPilot(false, "the source no longer takes commands (stopped or switched)");
                else if (!Application.isFocused && !IgnoreFocusLoss) PilotDeadMan("the window lost focus");
            }
            else if (_held != PilotKeys.None) _held = PilotKeys.None;

            if (client == null) { ClearFlyTarget(); _lastAckToast = null; _padSeen = false; return; }

            if (_pilotOn) ReadPilotInput(client);
            TickAckToasts(client);
            TickFlyTarget(client);
        }

        private void ReadPilotInput(TelemetryClient client)
        {
            BlurFocus();
            bool ctrl = KeyHeld(KeyCode.LeftControl) || KeyHeld(KeyCode.RightControl) || KeyHeld(KeyCode.LeftAlt) || KeyHeld(KeyCode.RightAlt);
            bool help = DesktopUIController.Instance != null && DesktopUIController.Instance.IsHelpOpen;
            bool listen = !ctrl && !help && !TypingNow();

            PilotKeys held = PilotKeys.None;
            if (listen)
            {
                if (KeyHeld(KeyCode.W) || KeyHeld(KeyCode.UpArrow)) held |= PilotKeys.Forward;
                if (KeyHeld(KeyCode.S) || KeyHeld(KeyCode.DownArrow)) held |= PilotKeys.Back;
                if (KeyHeld(KeyCode.A) || KeyHeld(KeyCode.LeftArrow)) held |= PilotKeys.Left;
                if (KeyHeld(KeyCode.D) || KeyHeld(KeyCode.RightArrow)) held |= PilotKeys.Right;
                if (KeyHeld(KeyCode.Q)) held |= PilotKeys.YawLeft;
                if (KeyHeld(KeyCode.E)) held |= PilotKeys.YawRight;
                if (KeyHeld(KeyCode.Space)) held |= PilotKeys.Up;
                if (KeyHeld(KeyCode.LeftShift) || KeyHeld(KeyCode.RightShift)) held |= PilotKeys.Down;
                if (KeyPressed(KeyCode.T)) SendPilotCommand(PilotCommand.Takeoff, "key T");
                if (KeyPressed(KeyCode.L)) SendPilotCommand(PilotCommand.Land, "key L");
                if (KeyPressed(KeyCode.H)) SendPilotCommand(PilotCommand.Hold, "key H");
                if (KeyPressed(KeyCode.R)) SendPilotCommand(PilotCommand.Rtl, "key R");
            }
            if (held != _held) { _held = held; RefreshCaps(); }

            SimSticks sticks = PilotControls.KeysToSticks(_held);
#if UNITY_STANDALONE_WIN
            if (listen && XInputPad.TryRead(out XInputPad.Reading pad))
            {
                _padSeen = true;
                for (int i = 0; i < 4; i++)
                {
                    bool now = pad.Button(i);
                    if (now && !_padButtons[i]) { PilotCommand? c = PilotControls.ButtonToCommand(i); if (c.HasValue) SendPilotCommand(c.Value, "pad " + "ABXY"[i]); }
                    _padButtons[i] = now;
                }
                if (PilotControls.PadActive(pad.LeftX, pad.LeftY, pad.RightX, pad.RightY))
                {
                    sticks = PilotControls.PadToSticks(pad.LeftX, pad.LeftY, pad.RightX, pad.RightY);
                }
                SetPadText(T("pilot.legend.pad") + ": " + T("pilot.legend.pad.on"));
            }
            else if (_padSeen)
            {
                _padSeen = false;
                Array.Clear(_padButtons, 0, 4);
                PilotDeadMan("the pad disconnected");
                return;
            }
            else SetPadText(string.Empty);
#else
            SetPadText(string.Empty);
#endif
            bool neutral = sticks.x == 0 && sticks.y == 0 && sticks.z == PilotControls.ThrottleHold && sticks.r == 0;
            if (!neutral) _pilotAuto = false; // the pilot took the sticks back from a click-to-fly
            if (!neutral && _pilotFlagLost) { _pilotFlagLost = false; client.Send(ClientMessage.Pilot(), out _); } // touching the sticks after land / hold / RTL takes control again
            _lastSticks = sticks;

            if (_pilotAuto) return; // fly_to in progress: do not send neutral samples over it
            double now2 = Time.unscaledTimeAsDouble;
            if (now2 >= _nextManual)
            {
                _nextManual = Math.Max(_nextManual + 1.0 / PilotControls.StreamHz, now2 - 0.1);
                client.Send(PilotControls.ToMessage(sticks), out _);
            }
        }

        private void SetPadText(string text)
        {
            if (_lastPadText == text) return;
            _lastPadText = text;
            _legendPad.text = text;
        }

        private void RefreshCaps()
        {
            foreach (var kv in _caps)
            {
                bool on = false;
                switch (kv.Key)
                {
                    case "W": on = (_held & PilotKeys.Forward) != 0; break;
                    case "S": on = (_held & PilotKeys.Back) != 0; break;
                    case "A": on = (_held & PilotKeys.Left) != 0; break;
                    case "D": on = (_held & PilotKeys.Right) != 0; break;
                    case "Q": on = (_held & PilotKeys.YawLeft) != 0; break;
                    case "E": on = (_held & PilotKeys.YawRight) != 0; break;
                    case "Space": on = (_held & PilotKeys.Up) != 0; break;
                    case "Shift": on = (_held & PilotKeys.Down) != 0; break;
                }
                kv.Value.EnableInClassList("dk-kbd--held", on);
            }
        }

        /// <summary>Negative acks and geofence limits as toasts.</summary>
        private void TickAckToasts(TelemetryClient client)
        {
            AckMessage ack = client.LastAck;
            if (ack != null && !ReferenceEquals(ack, _lastAckToast))
            {
                _lastAckToast = ack;
                if (!ack.ok)
                {
                    ShowToast(AckText(ack), true);
                    if (ack.command == "fly_to") ClearFlyTarget();
                }
                else if (ack.command == "rtl" || ack.command == "hold" || ack.command == "orbit" || ack.command == "land") ClearFlyTarget();
            }

            if (DesktopTelemetry.IsSimulator)
            {
                int touch = PilotControls.GeofenceTouch(client.Displayed);
                if (touch != 0 && touch != _geoTouch && Time.unscaledTime - _geoToastAt > 2.5f)
                {
                    _geoToastAt = Time.unscaledTime;
                    string msg = (touch & 1) != 0 && (touch & 2) != 0 ? "toast.geofence.both" : (touch & 1) != 0 ? "toast.geofence.radius" : "toast.geofence.ceiling";
                    ShowToast(TelemetryStrings.F(msg, Lang, "r", SimLimits.Radius.ToString("0"), "h", SimLimits.Ceiling.ToString("0")), true);
                }
                _geoTouch = touch;
            }
        }

        private string AckText(AckMessage ack)
        {
            string d = ack.HasDetail ? TelemetryStrings.AckDetail(ack.detail, Lang) : T("faults.ack.rejected");
            return ack.command + ": " + d;
        }

        /// <summary>Clears the target marker on arrival (1.5 m, like the web), on landing and when the flight stops.</summary>
        private void TickFlyTarget(TelemetryClient client)
        {
            if (!_hasTarget) return;
            TelemetryFrame f = client.Displayed;
            if (f == null || f.landed) { ClearFlyTarget(); return; }
            double dn = f.p[0] - _targetN, de = f.p[1] - _targetE;
            if (Math.Sqrt(dn * dn + de * de) < 1.5) { ClearFlyTarget(); _pilotAuto = false; }
        }

        /// <summary>Called when the session stops: piloting is off without a message (there is nobody to send `hold` to).</summary>
        private void OnPilotSessionEnded()
        {
            if (_pilotOn) { _pilotOn = false; _held = PilotKeys.None; Debug.Log("[Pilot] OFF (session stopped): the desktop shortcuts are back"); }
            if (_legend != null) _legend.style.display = DisplayStyle.None;
            _btnPilot?.EnableInClassList("dk-strip-btn--on", false);
            ClearFlyTarget();
        }
    }
}
#endif
