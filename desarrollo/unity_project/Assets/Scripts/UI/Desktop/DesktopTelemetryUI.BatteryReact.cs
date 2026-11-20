#if UNITY_STANDALONE
using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Telemetry;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// MVP P2 (PC only): what the FLIGHT SIMULATOR does when its battery runs down. A strip button «Batería» opens a popover with the approved note («Simulador: con la
    /// batería baja el dron avisa, vuelve solo y, al agotarse, aterriza y se desarma. Es un modelo simplificado; no es PX4 ni un dron real.»), the switch «Ignorar batería»
    /// (default: the battery acts; persisted in PlayerPrefs Desk_SimBatteryReact), the visible thresholds («Aviso 15 % · crítica 7 % … · agotada 0 % …») and the state of the
    /// reaction now. With the switch on «Ignorar batería» a chip «Quitar …» appears in the top bar (DesktopActiveEffects) that turns the reaction back on. Honesty: all of
    /// it is local state of the simulator (every text says «simulado»), a simplified imitation of what PX4 SITL did, NOT PX4 and NOT a real drone; with PX4 SITL or the replay
    /// the button is dimmed. The logic is the pure <see cref="BatteryReact"/> / <see cref="FlightSim"/> (golden-tested against contracts/golden/batteryreact.json).
    /// </summary>
    internal sealed partial class DesktopTelemetryUI
    {
        private Button _btnBattery;
        private Label _batteryBtnLabel;
        private VisualElement _popBattery;
        private Label _brTitle, _brTag, _brNote, _brParams, _brState;
        private Button _brIgnore;
        private bool _batteryReact = true;

        // ── read by the verification harness ──
        public Button BatteryButton => _btnBattery;
        public VisualElement BatteryPopover => _popBattery;
        public Button BatteryIgnoreButton => _brIgnore;
        /// <summary>The switch as the person left it (true = the battery acts on the flight). With a simulator running, what the model has.</summary>
        public bool BatteryReactOn => BatteryReactNow();
        public string BatteryStateText => _brState?.text;
        public string BatteryParamsText => _brParams?.text;

        partial void BuildBatteryReactButton(VisualElement strip)
        {
            _batteryReact = SimSettings.LoadBatteryReact();
            _btnBattery = StripButton("DkTelBatteryReact", "battery", out _batteryBtnLabel, () => TogglePopover("batteryreact"));
            strip.Add(_btnBattery);
        }

        partial void BuildBatteryReactPopover()
        {
            _popBattery = NewPopover("DkPopBatteryReact", "dk-pop--w280", out _brTitle);
            var tagRow = new VisualElement();
            tagRow.AddToClassList("dk-fault-tagrow");
            _brTag = Text("dk-tag", tagRow);
            _brTag.AddToClassList("dk-tag--simulated");
            _popBattery.Add(tagRow);
            _brNote = Text("dk-note", _popBattery);
            _brIgnore = ToolButton("DkBatteryIgnore", "dk-b--secondary", () => ApplyBatteryReact(!BatteryReactNow()));
            _popBattery.Add(_brIgnore);
            _brParams = Text("dk-note", _popBattery);
            _brParams.AddToClassList("dk-note--muted");
            _brState = Text("dk-fault-row", _popBattery);
            DesktopInfo.Beside(_brTag, false, "DkInfoBattery", _brNote); // UX step 5: the long note («no es PX4 ni un dron real») behind an ⓘ next to the «simulado» tag; the thresholds stay visible
            RegisterToolPopover("batteryreact", _popBattery);
            RefreshBatteryControls();
        }

        partial void DisposeBatteryReact()
        {
            _popBattery?.RemoveFromHierarchy();
        }

        partial void RelabelBatteryReact()
        {
            if (_btnBattery == null || _popBattery == null) return;
            _batteryBtnLabel.text = T("tools.battery");
            _brTitle.text = T("simbat.title");
            _brTag.text = T("simbat.hint");
            _brNote.text = T("simbat.note");
            _brIgnore.text = T("simbat.ignore");
            RefreshBatteryControls();
        }

        /// <summary>The switch as the model has it while a simulator runs, else the remembered one.</summary>
        private bool BatteryReactNow()
        {
            var src = RunningSimulator;
            return src != null ? src.Model.cfg.battery.react : _batteryReact;
        }

        private static string Pc(double x) => JsMath.Round(x * 100).ToString("0", CultureInfo.InvariantCulture);

        /// <summary>Turns the reaction on or off (a running simulator takes it; it is remembered either way).</summary>
        public ConfigCheck ApplyBatteryReact(bool react)
        {
            var src = RunningSimulator;
            ConfigCheck r = src != null ? src.SetConfig(new SimConfigPatch { battery = new BatteryPatch { react = react } }) : ConfigCheck.Ok;
            if (r.ok)
            {
                _batteryReact = react;
                SimSettings.SaveBatteryReact(react);
            }
            RefreshBatteryControls();
            return r;
        }

        private void RefreshBatteryControls()
        {
            if (_brIgnore == null) return;
            var src = RunningSimulator;
            BatteryReactConfig c = src != null ? src.Model.cfg.battery : new BatteryReactConfig { react = _batteryReact };
            string lang = Lang;
            _brIgnore.EnableInClassList("dk-b--on", !c.react);
            _brParams.text = TelemetryStrings.F("simbat.params", lang, "low", Pc(c.lowRem), "crit", Pc(c.critRem), "empty", Pc(c.emptyRem));
            int stage = src != null ? src.Model.batReact.stage : 0;
            _brState.text = !c.react ? T("simbat.ignored") : T("simbat.state." + (stage <= 0 ? "ok" : stage == 1 ? "low" : stage == 2 ? "crit" : "empty"));
        }

        partial void TickBatteryReactPopover() => RefreshBatteryControls();

        partial void TickBatteryReact(bool viewer)
        {
            if (_btnBattery == null) return;
            bool sim = DesktopTelemetry.IsRunning && DesktopTelemetry.IsSimulator;
            _btnBattery.SetEnabled(sim);
            _btnBattery.EnableInClassList("dk-strip-btn--on", _openPop == "batteryreact");
            string tip = sim ? T("simbat.note") : T("tools.battery.need");
            if (_btnBattery.tooltip != tip) _btnBattery.tooltip = tip;
            if (!sim && _openPop == "batteryreact") CloseAllPopovers();
        }
    }
}
#endif
