#if UNITY_STANDALONE
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Telemetry;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// MVP P4 (PC only): «Agotar batería» of PX4 SITL in the Faults popover (contract 1.1, command <c>battery_off</c>). Shown only while PX4 SITL is the source AND the bridge lists
    /// <c>battery_off</c> in its <c>hello</c> (a 1.0.0 bridge does not: then nothing is offered); enabled only while the drone is armed. Two taps like the simulator's motor cut: the first
    /// asks «¿Agotar batería? Toca otra vez», the second sends the order (it expires by itself after 4 s). Beside it «Restaurar motores» (the approved web label), because the approved
    /// note says it also clears this fault. The note under the buttons is the approved <c>cmd.batteryNote</c> (the order is PX4's MAV_CMD_INJECT_FAILURE and what follows is PX4's own
    /// battery simulator: «Es lo que ocurre en la simulación, no una afirmación sobre un dron físico ni sobre una batería real»); the ack reads «aceptado por PX4; en SITL la batería
    /// simulada se vacía y PX4 aterriza y desarma» (cmd.ackOkBatteryOff). Nothing here applies to the simulator or the replay.
    /// </summary>
    internal sealed partial class DesktopTelemetryUI
    {
        private VisualElement _sitlFaultControls;
        private Label _sbNote, _sbAck;
        private Button _sbBatteryOff, _sbRestore;
        private bool _sbArmed;
        private float _sbArmedUntil;
        private AckMessage _sbLastAck;
        private string _sbAckText = string.Empty;

        // ── read by the verification harness ──
        public VisualElement SitlFaultControls => _sitlFaultControls;
        public Button SitlBatteryOffButton => _sbBatteryOff;
        public Button SitlRestoreButton => _sbRestore;
        public bool SitlBatteryOffArmed => _sbArmed;
        public string SitlFaultNote => _sbNote?.text;
        public string SitlFaultAck => _sbAckText;

        private void BuildSitlFaultControls()
        {
            _sitlFaultControls = new VisualElement { name = "DkSitlFaultControls" };
            _sitlFaultControls.style.display = DisplayStyle.None;
            _sbNote = Text("dk-note", _sitlFaultControls);
            _sbNote.AddToClassList("dk-note--muted");
            var grid = new VisualElement();
            grid.AddToClassList("dk-fault-grid");
            _sbBatteryOff = new Button(OnSitlBatteryOff) { name = "DkSitlBatteryOff" };
            _sbBatteryOff.AddToClassList("dk-b");
            _sbBatteryOff.AddToClassList("dk-b--secondary");
            _sbBatteryOff.AddToClassList("dk-fault-btn");
            grid.Add(_sbBatteryOff);
            _sbRestore = new Button(OnSitlRestore) { name = "DkSitlRestore" };
            _sbRestore.AddToClassList("dk-b");
            _sbRestore.AddToClassList("dk-b--secondary");
            _sbRestore.AddToClassList("dk-fault-btn");
            grid.Add(_sbRestore);
            _sitlFaultControls.Add(grid);
            _sbAck = Text("dk-note", _sitlFaultControls);
            _sbAck.AddToClassList("dk-note--muted");
            _popFaults.Add(_sitlFaultControls);
        }

        private void RelabelSitlFaults()
        {
            if (_sitlFaultControls == null) return;
            _sbNote.text = T("cmd.batteryNote");
            _sbBatteryOff.text = T(_sbArmed ? "cmd.batteryOffConfirm" : "cmd.batteryOff");
            _sbRestore.text = T("cmd.restore");
        }

        private void OnSitlBatteryOff()
        {
            var client = DesktopTelemetry.Client;
            if (client == null || client.Classification != SourceClassification.Sitl) return;
            if (!_sbArmed || Time.unscaledTime > _sbArmedUntil)
            {
                _sbArmed = true;
                _sbArmedUntil = Time.unscaledTime + 4f; // one tap to ask, a second one to send; the question goes away by itself
                RelabelSitlFaults();
                return;
            }
            _sbArmed = false;
            client.Send(ClientMessage.BatteryOff(), out _);
            _fxTopic = FaultTopic.BatterySag; // the card follows the fault that was just caused
            RelabelSitlFaults();
            _nextPopTick = 0f;
        }

        private void OnSitlRestore()
        {
            _sbArmed = false;
            DesktopTelemetry.Client?.Send(ClientMessage.Restore(), out _);
            RelabelSitlFaults();
            _nextPopTick = 0f;
        }

        /// <summary>Per tick of the open Faults popover: the controls exist only with PX4 SITL whose bridge announces battery_off.</summary>
        private void TickSitlFaults(TelemetryClient client, bool sitl)
        {
            if (_sitlFaultControls == null) return;
            bool announced = sitl && client != null && client.Hello != null && client.Hello.Accepts("battery_off");
            _sitlFaultControls.style.display = announced ? DisplayStyle.Flex : DisplayStyle.None;
            if (!announced) { _sbArmed = false; return; }
            if (_sbArmed && Time.unscaledTime > _sbArmedUntil) { _sbArmed = false; RelabelSitlFaults(); }
            bool armed = client.Displayed != null && client.Displayed.armed;
            _sbBatteryOff.SetEnabled(armed);
            _sbRestore.SetEnabled(armed);
            _sbBatteryOff.EnableInClassList("dk-b--on", _sbArmed);
            if (client.LastAck != null && !ReferenceEquals(client.LastAck, _sbLastAck))
            {
                _sbLastAck = client.LastAck;
                AckMessage a = client.LastAck;
                if (a.command == "battery_off" || a.command == "restore")
                    _sbAckText = a.command + ": " + (a.ok ? T(a.command == "battery_off" ? "cmd.ackOkBatteryOff" : "cmd.ackOk") : T("faults.ack.rejected") + (a.HasDetail ? " (" + a.detail + ")" : string.Empty));
            }
            _sbAck.text = _sbAckText;
            _sbAck.style.display = string.IsNullOrEmpty(_sbAckText) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
#endif
