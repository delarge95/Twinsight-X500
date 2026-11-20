#if UNITY_STANDALONE
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.Telemetry;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>Source selector, alerts list and synthetic faults: the three popovers of the Telemetry strip (one open at a time).</summary>
    internal sealed partial class DesktopTelemetryUI
    {
        private string _openPop;
        private VisualElement _popSource, _popAlerts, _popFaults;
        private Label _srcTitle, _alTitle, _flTitle;

        // source popover
        private Button _rowInspector, _rowReplay, _rowSim, _rowSitl;
        private Label _simDetail;
        private Label _inspTitle, _inspDetail, _repTitle, _repBadge, _repDetail, _simTitle, _simSub, _sitlTitle, _sitlBadge, _sitlDetail, _sitlNote, _sitlUrlLabel, _sitlMsg;
        private TextField _sitlField;
        private Button _sitlConnect;
        private VisualElement _sitlBlock;

        // alerts popover
        private VisualElement _alertList;
        private Label _alertsEmpty, _alertsHint;
        private readonly List<AlertRow> _alertRows = new List<AlertRow>();
        private string _alertSig;

        // faults popover
        private Label _faultNote, _faultOff, _faultMotorLabel, _faultActiveTitle, _faultAck, _faultSyntheticTag;
        private readonly Button[] _motorBtns = new Button[5];
        private readonly Button[] _kindBtns = new Button[4];
        private Button _faultClear, _faultExplain;
        private VisualElement _faultFoot;
        private VisualElement _faultControls;
        private VisualElement _faultActiveList;
        private readonly List<Label> _faultActiveRows = new List<Label>();
        private int _faultMotor = 3;
        private AckMessage _lastAckShown;
        private string _ackText = string.Empty;

        private sealed class AlertRow
        {
            public VisualElement Wrap;
            public Button Root, Expand;
            public VisualElement Dot;
            public Label Message, Meta;
            public string Id, Component;
        }

        // ───────────────────────── build ─────────────────────────

        private VisualElement NewPopover(string name, string width, out Label title)
        {
            var pop = new VisualElement { name = name, pickingMode = PickingMode.Position };
            pop.AddToClassList("dk-pop");
            pop.AddToClassList("dk-pop--tel");
            pop.AddToClassList(width);
            pop.style.display = DisplayStyle.None;
            var head = new VisualElement();
            head.AddToClassList("dk-pop-head");
            title = new Label { pickingMode = PickingMode.Ignore };
            title.AddToClassList("dk-pop-title");
            head.Add(title);
            var close = _chrome.IconButtonPublic("close", name + "Close", () => CloseAllPopovers(), 16);
            close.AddToClassList("dk-ibtn--small");
            head.Add(close);
            pop.Add(head);
            DesktopMotion.WatchVisible(pop);
            return pop;
        }

        private static Label Text(string cls, VisualElement parent = null)
        {
            var l = new Label { pickingMode = PickingMode.Ignore };
            l.AddToClassList(cls);
            parent?.Add(l);
            return l;
        }

        private Button SourceRow(string name, string icon, out Label title, out Label sub, Action click)
        {
            var b = new Button(click) { name = name };
            b.AddToClassList("dk-src-row");
            var ic = _chrome.NewIconPublic(icon, 20);
            ic.AddToClassList("dk-src-icon");
            b.Add(ic);
            var col = new VisualElement { pickingMode = PickingMode.Ignore };
            col.AddToClassList("dk-src-col");
            title = Text("dk-src-title", col);
            sub = Text("dk-src-sub", col);
            b.Add(col);
            b.RegisterCallback<PointerEnterEvent>(_ => ic.MarkDirtyRepaint());
            b.RegisterCallback<PointerLeaveEvent>(_ => ic.MarkDirtyRepaint());
            return b;
        }

        private void BuildPopovers()
        {
            BuildSourcePopover();
            BuildAlertsPopover();
            BuildFaultsPopover();
            _container.Add(_popSource);
            _container.Add(_popAlerts);
            _container.Add(_popFaults);
            BuildToolPopovers(); // Lote 1a: the simulator tools (geofence, wind) register their popovers here
        }

        private void BuildSourcePopover()
        {
            _popSource = NewPopover("DkPopSource", "dk-pop--w360", out _srcTitle);

            _rowInspector = SourceRow("DkSrcInspector", "inspect", out _inspTitle, out _inspDetail, () => { _sitlBlockOpen = false; StopSession(); });
            _popSource.Add(_rowInspector);

            _rowReplay = SourceRow("DkSrcReplay", "replay", out _repTitle, out _repBadge, () => { StartLocalReplay(); });
            _repBadge.AddToClassList("dk-src-badge");
            _repDetail = Text("dk-src-detail");
            _rowReplay.Q<VisualElement>(className: "dk-src-col").Add(_repDetail);
            _popSource.Add(_rowReplay);

            _rowSim = SourceRow("DkSrcSim", "flight", out _simTitle, out _simSub, () => { StartSimulator(); });
            _simSub.AddToClassList("dk-src-badge");
            _simDetail = Text("dk-src-detail");
            _rowSim.Q<VisualElement>(className: "dk-src-col").Add(_simDetail);
            _popSource.Add(_rowSim);

            _rowSitl = SourceRow("DkSrcSitl", "sitl", out _sitlTitle, out _sitlBadge, () => SelectSitl());
            _sitlBadge.AddToClassList("dk-src-badge");
            _sitlDetail = Text("dk-src-detail");
            _rowSitl.Q<VisualElement>(className: "dk-src-col").Add(_sitlDetail);
            _popSource.Add(_rowSitl);

            _sitlBlock = new VisualElement { name = "DkSitlBlock" };
            _sitlBlock.AddToClassList("dk-sitl-block");
            _sitlNote = Text("dk-note", _sitlBlock);
            _sitlUrlLabel = Text("dk-field-label", _sitlBlock);
            var line = new VisualElement();
            line.AddToClassList("dk-sitl-line");
            _sitlField = new TextField { name = "DkSitlUrl", value = _sitlUrl };
            _sitlField.AddToClassList("dk-sitl-field");
            _sitlField.RegisterValueChangedCallback(e => { _sitlUrl = e.newValue; });
            line.Add(_sitlField);
            _sitlConnect = new Button(() => { StartSitl(_sitlField.value); }) { name = "DkSitlConnect" };
            _sitlConnect.AddToClassList("dk-b");
            _sitlConnect.AddToClassList("dk-b--primary");
            line.Add(_sitlConnect);
            _sitlBlock.Add(line);
            _sitlMsg = Text("dk-note", _sitlBlock);
            _sitlMsg.AddToClassList("dk-note--warn");
            _popSource.Add(_sitlBlock);

            // UX step 5: the long detail of each source moves behind an ⓘ next to its short badge (the badge, with the honesty label of the origin, stays visible);
            // the SITL note goes behind an ⓘ next to the URL label. The texts stay in the tree (hidden labels + the tooltip of the ⓘ).
            DesktopInfo.Beside(_repBadge, true, "DkInfoSrcReplay", _repDetail);
            DesktopInfo.Beside(_simSub, true, "DkInfoSrcSim", _simDetail);
            DesktopInfo.Beside(_sitlBadge, true, "DkInfoSrcSitl", _sitlDetail);
            DesktopInfo.Beside(_sitlUrlLabel, true, "DkInfoSitlNote", _sitlNote);
        }

        private void SelectSitl()
        {
            _preferred = TelSource.Sitl;
            PlayerPrefs.SetString(PrefSource, "sitl");
            _sitlBlockOpen = true;
        }

        private bool _sitlBlockOpen;

        private void BuildAlertsPopover()
        {
            _popAlerts = NewPopover("DkPopAlerts", "dk-pop--w360", out _alTitle);
            _alertList = new ScrollView(ScrollViewMode.Vertical) { name = "DkAlertList" };
            _alertList.AddToClassList("dk-alert-list");
            _popAlerts.Add(_alertList);
            _alertsEmpty = Text("dk-note");
            _popAlerts.Add(_alertsEmpty);
            _alertsHint = Text("dk-note");
            _alertsHint.AddToClassList("dk-note--muted");
            _popAlerts.Add(_alertsHint);
        }

        private void BuildFaultsPopover()
        {
            _popFaults = NewPopover("DkPopFaults", "dk-pop--w280", out _flTitle);
            var tagRow = new VisualElement();
            tagRow.AddToClassList("dk-fault-tagrow");
            _faultSyntheticTag = Text("dk-tag", tagRow);
            _faultSyntheticTag.AddToClassList("dk-tag--simulated");
            _popFaults.Add(tagRow);
            _faultOff = Text("dk-note", _popFaults);
            _faultOff.AddToClassList("dk-note--warn");

            _faultControls = new VisualElement { name = "DkFaultControls" };
            var motorRow = new VisualElement();
            motorRow.AddToClassList("dk-fault-motor");
            _faultMotorLabel = Text("dk-field-label", motorRow);
            for (int m = 1; m <= 4; m++)
            {
                int mm = m;
                var b = new Button(() => { _faultMotor = mm; RefreshMotorButtons(); }) { text = m.ToString(), name = "DkFaultMotor" + m };
                b.AddToClassList("dk-b");
                b.AddToClassList("dk-b--pill");
                b.AddToClassList("dk-fault-mbtn");
                motorRow.Add(b);
                _motorBtns[m] = b;
            }

            _faultControls.Add(motorRow);
            var grid = new VisualElement();
            grid.AddToClassList("dk-fault-grid");
            FaultKind[] kinds = { FaultKind.MotorFailure, FaultKind.MotorOverheat, FaultKind.BatterySag, FaultKind.Vibration };
            for (int i = 0; i < 4; i++)
            {
                FaultKind k = kinds[i];
                var b = new Button(() => Inject(k)) { name = "DkFault_" + WireNames.Of(k) };
                b.AddToClassList("dk-b");
                b.AddToClassList("dk-b--secondary");
                b.AddToClassList("dk-fault-btn");
                grid.Add(b);
                _kindBtns[i] = b;
            }

            _faultControls.Add(grid);
            _popFaults.Add(_faultControls);
            BuildSimFaultControls(); // Lote 1a, tool 10: «Cortar motor N», «Restaurar» and the didactic compensation, only with the simulator
            BuildSitlFaultControls(); // MVP P4: «Agotar batería» (battery_off) of PX4 SITL, only when the bridge announces it
            _faultActiveTitle = Text("dk-field-label", _popFaults);
            _faultActiveList = new VisualElement();
            _faultActiveList.AddToClassList("dk-fault-active");
            _popFaults.Add(_faultActiveList);
            _faultAck = Text("dk-note", _popFaults);
            _faultAck.AddToClassList("dk-note--muted");
            // foot: Clear (only where faults can be injected) and «Explicación», which opens the docked explanation (never opened by itself)
            _faultFoot = new VisualElement { name = "DkFaultFoot" };
            _faultFoot.AddToClassList("dk-fault-foot");
            _faultFoot.Add(_faultAck); // the ack line sits in the foot row, left of the buttons (one row less in the popover)
            _faultClear = new Button(() => ClearFaults()) { name = "DkFaultClear" };
            _faultClear.AddToClassList("dk-b");
            _faultClear.AddToClassList("dk-b--ghost");
            _faultClear.AddToClassList("dk-fault-footbtn");
            _faultFoot.Add(_faultClear);
            _faultExplain = new Button(() => ShowFaultExplanation(_fxTopic, _faultMotor, null)) { name = "DkFaultExplain" };
            _faultExplain.AddToClassList("dk-b");
            _faultExplain.AddToClassList("dk-b--secondary");
            _faultExplain.AddToClassList("dk-fault-footbtn");
            _faultFoot.Add(_faultExplain);
            _popFaults.Add(_faultFoot);
            RefreshMotorButtons();
        }

        /// <summary>Selects the motor of the injection buttons and of the card (verification harness and the banner action).</summary>
        public void FaultMotorSelect(int motor) { if (motor >= 1 && motor <= 4) { _faultMotor = motor; RefreshMotorButtons(); } }

        private void RefreshMotorButtons()
        {
            for (int m = 1; m <= 4; m++) _motorBtns[m]?.EnableInClassList("dk-b--on", m == _faultMotor);
            RelabelFaultButtons();
        }

        // ───────────────────────── open / close ─────────────────────────

        public void TogglePopover(string which)
        {
            if (_container == null) return;
            if (_openPop == which) { CloseAllPopovers(); return; }
            OpenMode();
            if (!ModeOpen) return;
            CloseAllPopovers();
            VisualElement pop = which == "source" ? _popSource : which == "alerts" ? _popAlerts : which == "faults" ? _popFaults : _toolPops.TryGetValue(which, out var tp) ? tp : null;
            if (pop == null) return;
            _openPop = which;
            _alertSig = null;
            pop.style.display = DisplayStyle.Flex;
            pop.BringToFront();
            RelabelPopovers();
            TickPopovers(true);
        }

        public void OpenSourcePopover() { if (_openPop != "source") TogglePopover("source"); else CloseAllPopovers(); }
        public void OpenAlertsPopover() { if (_openPop != "alerts") TogglePopover("alerts"); else CloseAllPopovers(); }

        public void CloseAllPopovers()
        {
            _openPop = null;
            if (_popSource != null) _popSource.style.display = DisplayStyle.None;
            if (_popAlerts != null) _popAlerts.style.display = DisplayStyle.None;
            if (_popFaults != null) _popFaults.style.display = DisplayStyle.None;
            HideToolPopovers();
        }

        public string OpenPopover => _openPop;

        // ───────────────────────── texts ─────────────────────────

        private void RelabelPopovers()
        {
            if (_popSource == null) return;
            string lang = Lang;
            var labels = HonestyLabels.Load();

            _srcTitle.text = T("src.title");
            _inspTitle.text = T("src.inspector");
            _inspDetail.text = T("src.inspector.detail");
            _repTitle.text = T("src.replay");
            _repBadge.text = labels.TryLocalReplayText(lang, out string local) ? local : string.Empty;
            _repDetail.text = TelemetryStrings.LocalReplayDetail(labels, lang);
            _simTitle.text = T("src.sim");
            _simSub.text = labels.TrySimText("title", lang, out string sim) ? sim : string.Empty;
            _simDetail.text = (labels.TrySimText("detail", lang, out string simDetail) ? TelemetryStrings.ForDesktop(simDetail, lang) : string.Empty) + " " + T("src.sim.params");
            _sitlTitle.text = T("src.sitl");
            _sitlBadge.text = labels.ClassificationTitle(SourceClassification.Sitl, lang);
            _sitlDetail.text = TelemetryStrings.ForDesktop(labels.ClassificationDetail(SourceClassification.Sitl, lang), lang);
            _sitlNote.text = T("src.sitl.note");
            _sitlUrlLabel.text = T("src.sitl.url");
            _sitlConnect.text = T("src.sitl.connect");

            _alTitle.text = T("alerts.title");
            _alertsHint.text = T("alerts.click");

            _flTitle.text = T("faults.title");
            RelabelFaultExplain();
            _faultSyntheticTag.text = labels.FaultSynthetic(lang);
            RelabelSimFaults();
            RelabelSitlFaults();
            _faultExplain.text = T("fx.open");
            _faultMotorLabel.text = T("faults.motor");
            _faultActiveTitle.text = T("faults.active");
            _faultClear.text = T("faults.clear");
            RelabelFaultButtons();
            _alertSig = null;
            RelabelTools();
        }

        private void RelabelFaultButtons()
        {
            if (_kindBtns[0] == null) return;
            _kindBtns[0].text = T("faults.motor_failure") + " " + _faultMotor;
            _kindBtns[1].text = T("faults.motor_overheat") + " " + _faultMotor;
            _kindBtns[2].text = T("faults.battery_sag");
            _kindBtns[3].text = T("faults.vibration");
        }

        // ───────────────────────── per frame ─────────────────────────

        private float _nextPopTick;

        private void TickPopovers(bool force = false)
        {
            if (_openPop == null) return;
            if (!force && Time.unscaledTime < _nextPopTick) return;
            _nextPopTick = Time.unscaledTime + 0.2f;

            var client = DesktopTelemetry.Client;
            bool running = client != null;

            if (_openPop == "source") TickSource(running);
            else if (_openPop == "alerts") TickAlerts(client);
            else if (_openPop == "faults") TickFaults(client);
            else TickToolPopover(_openPop);
        }

        private void TickSource(bool running)
        {
            TelSource active = ActiveSource;
            _rowInspector.EnableInClassList("dk-src-row--on", active == TelSource.Inspector);
            _rowReplay.EnableInClassList("dk-src-row--on", active == TelSource.LocalReplay);
            _rowSim.EnableInClassList("dk-src-row--on", active == TelSource.Simulator);
            _rowSitl.EnableInClassList("dk-src-row--on", active == TelSource.Sitl);

            bool showSitl = _sitlBlockOpen || active == TelSource.Sitl || _preferred == TelSource.Sitl;
            _sitlBlock.style.display = showSitl ? DisplayStyle.Flex : DisplayStyle.None;
            if (_sitlField.value != _sitlUrl && !IsFieldFocused()) _sitlField.SetValueWithoutNotify(_sitlUrl);

            string msg = _sitlError;
            if (msg == null && active == TelSource.Sitl && DesktopTelemetry.LinkState != LinkState.Live)
                msg = T("src.sitl.help") + (DesktopTelemetry.Client?.LastError != null ? " (" + DesktopTelemetry.Client.LastError + ")" : string.Empty);
            if (msg == null && _startError != null && active == TelSource.Inspector) msg = T("src.replay.missing") + ": " + _startError;
            _sitlMsg.text = msg ?? string.Empty;
            _sitlMsg.style.display = string.IsNullOrEmpty(msg) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private bool IsFieldFocused()
        {
            var fe = _sitlField.panel?.focusController?.focusedElement as VisualElement;
            for (VisualElement e = fe; e != null; e = e.parent) if (e == _sitlField) return true;
            return false;
        }

        // alerts ---------------------------------------------------------------------------------------------

        private void TickAlerts(TelemetryClient client)
        {
            var list = client != null ? client.Alerts : null;
            int n = list != null ? list.Count : 0;
            var sb = new StringBuilder();
            for (int i = 0; i < n; i++) sb.Append(list[i].id).Append(list[i].severity == AlertSeverity.Critical ? 'c' : 'w').Append('|');
            string sig = sb.ToString();
            bool rebuild = sig != _alertSig;
            if (rebuild)
            {
                _alertSig = sig;
                while (_alertRows.Count > n) { _alertRows[_alertRows.Count - 1].Wrap.RemoveFromHierarchy(); _alertRows.RemoveAt(_alertRows.Count - 1); }
                while (_alertRows.Count < n) _alertRows.Add(MakeAlertRow());
            }

            _alertsEmpty.style.display = n == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _alertsEmpty.text = client == null ? T("alerts.off") : T("alerts.none");
            _alertsHint.style.display = n > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (n == 0) return;
            FitScroll(_alertList, 34f); // the hint under the list

            var labels = HonestyLabels.Load();
            string lang = Lang;
            double now = client.Displayed != null ? client.Displayed.ts : ReplaySchedule.WallClockMs();
            for (int i = 0; i < n; i++)
            {
                Alert a = list[i];
                AlertRow row = _alertRows[i];
                row.Id = a.id;
                row.Component = a.component;
                bool synthetic = DesktopTelemetry.IsLocalReplay && AlertText.IsSynthetic(a, client.Faults, now);
                row.Message.text = AlertText.Build(a, client.Displayed, client.Hello, client.Classification, synthetic, lang, labels);
                row.Meta.text = PartName(a.component, lang) + " · " + T(a.severity == AlertSeverity.Critical ? "alerts.critical" : "alerts.warning");
                row.Dot.EnableInClassList("dk-dot--crit", a.severity == AlertSeverity.Critical);
                row.Dot.EnableInClassList("dk-dot--warn", a.severity != AlertSeverity.Critical);
                row.Root.tooltip = row.Meta.text;

                // PLAN_PC 28.1: «Explicación» opens the didactic card in the docked inspector (never over the canvas, never by itself)
                row.Expand.text = T_("fx.open");
                row.Expand.EnableInClassList("dk-fx-expand--on", AlertExpanded(a.id));
            }
        }

        public static string PartName(string component, string lang)
        {
            if (component != null && component.StartsWith("motor", StringComparison.Ordinal)) return "Motor " + component.Substring(5);
            if (component == "battery") return lang == "en" ? "Battery" : "Batería";
            if (component == "fc") return lang == "en" ? "Flight controller" : "Controladora de vuelo";
            return component ?? string.Empty;
        }

        private static string T_(string key) => T(key);

        private AlertRow MakeAlertRow()
        {
            var r = new AlertRow();
            r.Wrap = new VisualElement();
            r.Wrap.AddToClassList("dk-alert-wrap");
            var line = new VisualElement();
            line.AddToClassList("dk-alert-line");
            r.Root = new Button();
            r.Root.AddToClassList("dk-alert-row");
            r.Dot = new VisualElement { pickingMode = PickingMode.Ignore };
            r.Dot.AddToClassList("dk-dot");
            r.Root.Add(r.Dot);
            var col = new VisualElement { pickingMode = PickingMode.Ignore };
            col.AddToClassList("dk-alert-col");
            r.Message = Text("dk-alert-msg", col);
            r.Meta = Text("dk-alert-meta", col);
            r.Root.Add(col);
            AlertRow captured = r;
            r.Root.clicked += () => SelectComponent(captured.Component);
            line.Add(r.Root);
            r.Expand = new Button { name = "DkAlertExpand" };
            r.Expand.AddToClassList("dk-b");
            r.Expand.AddToClassList("dk-b--pill");
            r.Expand.AddToClassList("dk-fx-expand");
            r.Expand.clicked += () => ShowFaultExplanation(FaultExplain.TopicOfAlert(captured.Id), MotorOfComponent(captured.Component), captured.Id);
            line.Add(r.Expand);
            r.Wrap.Add(line);
            _alertList.Add(r.Wrap);
            return r;
        }

        /// <summary>Alert click: select and focus the part named by the alert's component (same selection system as a click on the model).</summary>
        public void SelectComponent(string component)
        {
            Transform t = TelemetryDroneDriver.Instance != null ? TelemetryDroneDriver.Instance.PartFor(component) : null;
            if (t == null || SelectionManager.Instance == null) return;
            SelectionManager.Instance.SelectObject(t);
            OrbitCameraController.Instance?.FocusOnObject(t);
            ShowFlightTab(false);
            UIDetailsSheet.Instance?.ShowInfo();
        }

        // faults ---------------------------------------------------------------------------------------------

        private void TickFaults(TelemetryClient client)
        {
            bool local = DesktopTelemetry.IsLocalReplay;
            bool simulator = DesktopTelemetry.IsSimulator;
            bool sitl = client != null && client.Classification == SourceClassification.Sitl && !simulator;
            bool canInject = local && client.Hello != null && client.Hello.Accepts("inject");

            _faultControls.style.display = canInject ? DisplayStyle.Flex : DisplayStyle.None;
            // the "sintética" tag belongs to the local replay's injection only: PX4 SITL has none and the simulator's cut is «simulado» (Lote 1a)
            _faultSyntheticTag.style.display = local || simulator ? DisplayStyle.Flex : DisplayStyle.None;
            if (simulator) { _faultSyntheticTag.text = T("simfault.hint"); _flTitle.text = T("simfault.title"); }
            else { _faultSyntheticTag.text = HonestyLabels.Load().FaultSynthetic(Lang); _flTitle.text = T("faults.title"); }
            string off = client == null ? T("faults.off") : simulator ? string.Empty : sitl ? T("faults.sitl") : !canInject && !local ? T("faults.off") : string.Empty;
            _faultOff.text = off;
            _faultOff.style.display = string.IsNullOrEmpty(off) ? DisplayStyle.None : DisplayStyle.Flex;
            _faultClear.style.display = canInject ? DisplayStyle.Flex : DisplayStyle.None;
            _faultActiveTitle.style.display = DisplayStyle.None; // the rows say «… · 28 s · sintética» themselves: the label is not needed (compact popover, PLAN_PC 28.1)

            // active faults with the seconds left
            double now = client != null && client.Displayed != null ? client.Displayed.ts : ReplaySchedule.WallClockMs();
            var active = canInject ? Faults.Active(client.Faults, now) : new List<Fault>();
            while (_faultActiveRows.Count > active.Count) { _faultActiveRows[_faultActiveRows.Count - 1].RemoveFromHierarchy(); _faultActiveRows.RemoveAt(_faultActiveRows.Count - 1); }
            while (_faultActiveRows.Count < active.Count)
            {
                var l = Text("dk-fault-row");
                l.pickingMode = PickingMode.Position; // a click on an active fault shows its explanation
                Label captured = l;
                l.RegisterCallback<ClickEvent>(_ =>
                {
                    if (!(captured.userData is Fault ff)) return;
                    // a click on an active fault is an explicit request: its explanation opens in the docked inspector
                    ShowFaultExplanation(FaultExplain.TopicOf(ff.kind), ff.HasMotor ? ff.motor : 0, null);
                });
                _faultActiveList.Add(l);
                _faultActiveRows.Add(l);
            }
            for (int i = 0; i < active.Count; i++)
            {
                Fault f = active[i];
                string kindName = T("faults." + WireNames.Of(f.kind));
                if (f.HasMotor && f.motor != 0) kindName += " " + f.motor;
                _faultActiveRows[i].userData = f;
                _faultActiveRows[i].text = kindName + " · " + Math.Max(0, (int)Math.Ceiling((f.until - now) / 1000.0)) + " s · " + AlertText.SyntheticFault(Lang, HonestyLabels.Load());
            }

            if (client != null && client.LastAck != null && !ReferenceEquals(client.LastAck, _lastAckShown))
            {
                _lastAckShown = client.LastAck;
                AckMessage a = client.LastAck;
                _ackText = a.command + ": " + (a.ok ? T(a.command == "motor_off" ? "faults.ack.okMotorOff" : "faults.ack.ok") : T("faults.ack.rejected") + (a.HasDetail ? " (" + a.detail + ")" : string.Empty));
            }

            _faultAck.text = canInject ? _ackText : string.Empty;
            _faultAck.style.display = canInject && !string.IsNullOrEmpty(_ackText) ? DisplayStyle.Flex : DisplayStyle.None;
            TickSimFaults(client, simulator);
            TickSitlFaults(client, sitl);
        }

        /// <summary>Synthetic injection on the local replay: validated like the web (Faults.Validate), sent as a normal client message.</summary>
        public bool Inject(FaultKind kind)
        {
            var client = DesktopTelemetry.Client;
            if (client == null || !DesktopTelemetry.IsLocalReplay) return false;
            bool needsMotor = kind == FaultKind.MotorFailure || kind == FaultKind.MotorOverheat;
            if (!Faults.Validate(WireNames.Of(kind), needsMotor ? (object)(double)_faultMotor : null, out FaultKind k, out int? m))
            {
                _ackText = "inject: " + T("faults.ack.rejected");
                return false;
            }

            bool sent = client.Send(ClientMessage.Inject(k, m), out string reason);
            _fxTopic = FaultExplain.TopicOf(k); // the card follows the fault that was just injected
            if (!sent) _ackText = "inject: " + T("faults.ack.rejected") + " (" + reason + ")";
            _nextPopTick = 0f;
            return sent;
        }

        public bool ClearFaults()
        {
            var client = DesktopTelemetry.Client;
            if (client == null || !DesktopTelemetry.IsLocalReplay) return false;
            bool sent = client.Send(ClientMessage.Clear(), out string reason);
            if (!sent) _ackText = "clear: " + T("faults.ack.rejected") + " (" + reason + ")";
            _nextPopTick = 0f;
            return sent;
        }
    }
}
#endif
