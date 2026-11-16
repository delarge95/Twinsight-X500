#if UNITY_STANDALONE
using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.Telemetry;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Lote 1a, tool 10 (PC only): «motor cortado» of the FLIGHT SIMULATOR in the Faults popover. With the simulator as the source the popover shows four «Cortar motor N»
    /// buttons (two clicks: the first asks «¿Cortar motor N? …», the second cuts), «Restaurar» and the switch «Compensación didáctica», the status
    /// «Motor N · cortado · simulado» and the approved line «Simulador: el motor deja de empujar en un modelo simplificado; no es PX4 ni un dron real.».
    /// The commands are the existing wire orders (motor_off, restore) sent through the client; the model is the simplified rigid body of <see cref="MotorFail"/>
    /// (roll, yaw and fall; impact event) coupled in <see cref="FlightSim"/>. It is NOT PX4 and NOT a real drone; the compensation is a didactic example, not the PX4 controller.
    /// The explanation card («En esta fuente · simulador») and the marker on the cut motor follow the same state. With PX4 SITL the faults are PX4's (motor_off is real there)
    /// and the replay keeps its synthetic injection: this file only acts while the simulator runs.
    /// </summary>
    internal sealed partial class DesktopTelemetryUI
    {
        private VisualElement _simFaultControls;
        private Label _sfNote, _sfStatus, _sfAck, _sfCompNote;
        private readonly Button[] _sfCut = new Button[5];
        private Button _sfRestore, _sfComp;
        private int _sfArmed;
        private float _sfArmedUntil;
        private AckMessage _sfLastAck;
        private string _sfAckText = string.Empty;

        // ── read by the verification harness ──
        public VisualElement SimFaultControls => _simFaultControls;
        public Button SimCutButton(int motor) => motor >= 1 && motor <= 4 ? _sfCut[motor] : null;
        public Button SimRestoreButton => _sfRestore;
        public Button SimCompButton => _sfComp;
        public string SimFaultStatus => _sfStatus?.text;
        public string SimFaultNote => _sfNote?.text;
        public int SimCutArmed => _sfArmed;

        /// <summary>The cut motor of the running simulator (a fall in progress or a mark on the ground), 0 when none.</summary>
        private static int SimCutMotor()
        {
            var src = RunningSimulator;
            if (src == null) return 0;
            SimState m = src.Model;
            return m.fail != null ? m.fail.motor : m.groundCut;
        }

        private void BuildSimFaultControls()
        {
            _simFaultControls = new VisualElement { name = "DkSimFaultControls" };
            _simFaultControls.style.display = DisplayStyle.None;
            _sfNote = Text("dk-note", _simFaultControls);
            var grid = new VisualElement();
            grid.AddToClassList("dk-fault-grid");
            for (int m = 1; m <= 4; m++)
            {
                int mm = m;
                var b = new Button(() => OnSimCut(mm)) { name = "DkSimCut" + m };
                b.AddToClassList("dk-b");
                b.AddToClassList("dk-b--secondary");
                b.AddToClassList("dk-fault-btn");
                grid.Add(b);
                _sfCut[m] = b;
            }
            _sfRestore = new Button(() => OnSimRestore()) { name = "DkSimRestore" };
            _sfRestore.AddToClassList("dk-b");
            _sfRestore.AddToClassList("dk-b--secondary");
            _sfRestore.AddToClassList("dk-fault-btn");
            grid.Add(_sfRestore);
            _sfComp = new Button(() => OnSimComp()) { name = "DkSimComp" };
            _sfComp.AddToClassList("dk-b");
            _sfComp.AddToClassList("dk-b--secondary");
            _sfComp.AddToClassList("dk-fault-btn");
            grid.Add(_sfComp);
            _simFaultControls.Add(grid);
            _sfCompNote = Text("dk-note", _simFaultControls);
            _sfCompNote.AddToClassList("dk-note--muted");
            _sfStatus = Text("dk-fault-row", _simFaultControls);
            _sfAck = Text("dk-note", _simFaultControls);
            _sfAck.AddToClassList("dk-note--muted");
            _popFaults.Add(_simFaultControls);
        }

        private void RelabelSimFaults()
        {
            if (_simFaultControls == null) return;
            _sfNote.text = T("simfault.note");
            _sfRestore.text = T("simfault.restore");
            _sfComp.text = T("simfault.comp");
            _sfCompNote.text = T("simfault.compNote");
            for (int m = 1; m <= 4; m++) _sfCut[m].text = TelemetryStrings.F(_sfArmed == m ? "simfault.cutConfirm" : "simfault.cut", Lang, "n", m);
        }

        private void OnSimCut(int motor)
        {
            var client = DesktopTelemetry.Client;
            if (client == null || RunningSimulator == null) return;
            if (_sfArmed != motor || Time.unscaledTime > _sfArmedUntil)
            {
                _sfArmed = motor;
                _sfArmedUntil = Time.unscaledTime + 4f; // one tap to ask, a second one to cut; the question goes away by itself
                RelabelSimFaults();
                return;
            }
            _sfArmed = 0;
            client.Send(ClientMessage.MotorOff(motor), out _);
            _fxTopic = FaultTopic.MotorFailure; // the card follows the fault that was just caused
            _faultMotor = motor;
            RelabelSimFaults();
            _nextPopTick = 0f;
        }

        private void OnSimRestore()
        {
            _sfArmed = 0;
            DesktopTelemetry.Client?.Send(ClientMessage.Restore(), out _);
            RelabelSimFaults();
            _nextPopTick = 0f;
        }

        private void OnSimComp()
        {
            var src = RunningSimulator;
            if (src == null) return;
            src.SetConfig(new SimConfigPatch { faultComp = !src.Model.cfg.faultComp });
            _nextPopTick = 0f;
        }

        /// <summary>Per tick of the open Faults popover: shows the simulator controls only while the simulator runs, and keeps their state.</summary>
        private void TickSimFaults(TelemetryClient client, bool simulator)
        {
            if (_simFaultControls == null) return;
            bool show = simulator && client != null && client.Hello != null && client.Hello.Accepts("motor_off");
            _simFaultControls.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) { _sfArmed = 0; return; }
            if (_sfArmed != 0 && Time.unscaledTime > _sfArmedUntil) { _sfArmed = 0; RelabelSimFaults(); }
            SimState m = RunningSimulator.Model;
            int cut = m.fail != null ? m.fail.motor : m.groundCut;
            for (int i = 1; i <= 4; i++)
            {
                _sfCut[i].SetEnabled(cut == 0);
                _sfCut[i].EnableInClassList("dk-b--on", _sfArmed == i);
            }
            _sfRestore.SetEnabled(cut != 0);
            _sfComp.EnableInClassList("dk-b--on", m.cfg.faultComp);
            _sfStatus.text = cut == 0 ? T("simfault.none") : TelemetryStrings.F("simfault.status", Lang, "n", cut);
            if (client.LastAck != null && !ReferenceEquals(client.LastAck, _sfLastAck))
            {
                _sfLastAck = client.LastAck;
                AckMessage a = client.LastAck;
                _sfAckText = (a.command == "motor_off" || a.command == "restore")
                    ? a.command + ": " + (a.ok ? T("simfault.ack.ok") : T("faults.ack.rejected") + (a.HasDetail ? " (" + a.detail + ")" : string.Empty))
                    : _sfAckText;
            }
            _sfAck.text = _sfAckText;
            _sfAck.style.display = string.IsNullOrEmpty(_sfAckText) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
#endif
