#if UNITY_STANDALONE
using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.Telemetry;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// The "Vuelo" tab of the inspector (docked right, PC). Instantaneous readings of the running session, each with the origin the web
    /// shows (hello.provenance; see <see cref="FlightOrigins"/>), units, refreshed at 10 Hz (labels are only written when their text
    /// changes). Without a session: an empty state that says so. The tab strip ("Pieza" / "Vuelo") is always in the inspector.
    /// </summary>
    internal sealed partial class DesktopTelemetryUI
    {
        private const float FlightPeriod = 0.1f;

        private VisualElement _sheet;
        private VisualElement _tabs;
        private Button _tabPart, _tabFlight;
        private VisualElement _tabDot;
        private ScrollView _flightPanel;
        private bool _flightTab;
        private Transform _lastSelection;
        private float _nextFlight;

        // empty state
        private VisualElement _flEmpty;
        private Label _flEmptyTitle, _flEmptyText;

        // body
        private VisualElement _flBody, _flData;
        private Label _flBadge, _flBadgeDetail, _flWaiting, _flStarved, _flSource;
        private Label _secState, _secPos, _secAtt, _secBat, _secMotors, _secFc;
        private DesktopFold _flAdv;
        private Reading _rMode, _rTime, _rAlt, _rHSpeed, _rVSpeed, _rHeading, _rRollPitch, _rBatLevel, _rBatVolt, _rBatCurrent, _rBatTemp, _rFcTemp, _rVib;
        private VisualElement _batFill;
        private readonly MotorRow[] _motors = new MotorRow[4];
        private Label _motorRpmHead, _motorTempHead;
        private Label _tagRpm, _tagMotorTemp;
        private Label _propNote;
        private HelloMessage _hello;

        private sealed class Reading
        {
            public VisualElement Root;
            public Label Name, Value, Tag;
            public string Key;
        }

        private sealed class MotorRow
        {
            public VisualElement Root;
            public Label Name, Rpm, Temp;
            public VisualElement UFill;
            public int Motor;
        }

        public bool FlightTabActive => _flightTab;
        /// <summary>One of the telemetry tabs (Vuelo, Analisis) is shown: the inspector stays open without a selection.</summary>
        public bool TelemetryTabActive => _flightTab || _analysisTab;

        // ───────────────────────── build ─────────────────────────

        private void BuildSheetTabs()
        {
            _sheet = _root.Q<VisualElement>("BottomSheet");
            if (_sheet == null) return;

            _tabs = new VisualElement { name = "DkSheetTabs" };
            _tabs.AddToClassList("dk-sheet-tabs");
            _tabPart = MakeTab("DkTabPart", () => ShowFlightTab(false));
            _tabFlight = MakeTab("DkTabFlight", () => ShowFlightTab(true));
            _tabAnalysis = MakeTab("DkTabAnalysis", () => ShowAnalysisTab());
            _tabDot = new VisualElement { name = "DkTabDot", pickingMode = PickingMode.Ignore };
            _tabDot.AddToClassList("dk-dot");
            _tabDot.AddToClassList("dk-dot--off");
            _tabFlight.Insert(0, _tabDot);
            _tabs.Add(_tabPart);
            _tabs.Add(_tabFlight);
            _tabs.Add(_tabAnalysis);

            // top of the sheet, under the (absolute) handle and before the title
            VisualElement header = _sheet.Q<VisualElement>(className: "sheet-header");
            int at = header != null ? _sheet.IndexOf(header) : 1;
            _sheet.Insert(Math.Max(0, at), _tabs);

            _flightPanel = new ScrollView(ScrollViewMode.Vertical) { name = "DkFlightPanel" };
            _flightPanel.AddToClassList("dk-flight");
            _flightPanel.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            VisualElement details = _sheet.Q<VisualElement>("SheetContent_Details");
            int di = details != null ? _sheet.IndexOf(details) + 1 : _sheet.childCount;
            _sheet.Insert(di, _flightPanel);
            BuildFlightBody();
            BuildAnalysisTab();
            _lastSelection = SelectionManager.Instance != null ? SelectionManager.Instance.CurrentSelection : null;
        }

        private Button MakeTab(string name, Action click)
        {
            var b = new Button(click) { name = name };
            b.AddToClassList("dk-tab");
            var l = new Label { pickingMode = PickingMode.Ignore };
            l.AddToClassList("dk-tab-text");
            b.Add(l);
            return b;
        }

        private Reading AddReading(VisualElement parent, string key, bool tag = true)
        {
            var r = new Reading { Key = key };
            r.Root = new VisualElement { pickingMode = PickingMode.Ignore };
            r.Root.AddToClassList("dk-reading");
            r.Name = Text("dk-reading-name", r.Root);
            r.Value = Text("dk-reading-value", r.Root);
            if (tag)
            {
                r.Tag = Text("dk-tag", r.Root);
            }
            parent.Add(r.Root);
            return r;
        }

        private Label Section(VisualElement parent)
        {
            var l = Text("dk-fl-sec");
            parent.Add(l);
            return l;
        }

        private void BuildFlightBody()
        {
            // empty state
            _flEmpty = new VisualElement { name = "DkFlEmpty", pickingMode = PickingMode.Ignore };
            _flEmpty.AddToClassList("dk-fl-empty");
            var emptyIcon = _chrome.NewIconPublic("flight", 24);
            emptyIcon.AddToClassList("dk-fl-empty-icon");
            _flEmpty.Add(emptyIcon);
            _flEmptyTitle = Text("dk-fl-empty-title", _flEmpty);
            _flEmptyText = Text("dk-note", _flEmpty);
            _flightPanel.Add(_flEmpty);

            _flBody = new VisualElement { name = "DkFlBody", pickingMode = PickingMode.Ignore };
            _flBody.AddToClassList("dk-fl-body");
            _flBadge = Text("dk-fl-badge", _flBody);
            _flBadgeDetail = Text("dk-note", _flBody);
            _flBadgeDetail.AddToClassList("dk-note--muted");
            _flWaiting = Text("dk-note", _flBody);
            _flStarved = Text("dk-note", _flBody);
            _flStarved.AddToClassList("dk-note--warn");

            _flData = new VisualElement { name = "DkFlData", pickingMode = PickingMode.Ignore };
            _flBody.Add(_flData);
            _secState = Section(_flData);
            _rMode = AddReading(_flData, "fl.mode", false);
            _rMode.Value.pickingMode = PickingMode.Position; // so that the tooltip with the raw PX4 name can show
            _rTime = AddReading(_flData, "fl.time", false);

            _secPos = Section(_flData);
            _rAlt = AddReading(_flData, "fl.altitude");
            _rHSpeed = AddReading(_flData, "fl.hspeed");
            _rVSpeed = AddReading(_flData, "fl.vspeed");
            _rHeading = AddReading(_flData, "fl.heading");

            _secAtt = Section(_flData);
            _rRollPitch = AddReading(_flData, "fl.rollPitch");

            _secBat = Section(_flData);
            _rBatLevel = AddReading(_flData, "fl.level");
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("dk-bat-bar");
            _batFill = new VisualElement { pickingMode = PickingMode.Ignore };
            _batFill.AddToClassList("dk-bat-fill");
            bar.Add(_batFill);
            _flData.Add(bar);
            _rBatVolt = AddReading(_flData, "fl.voltage");
            _rBatCurrent = AddReading(_flData, "fl.current");
            _rBatTemp = AddReading(_flData, "fl.batteryTemp");
            // UX step 5: the health of the battery, the motors and the controller / vibration go in the «Avanzado» fold (closed). The tags of origin stay in the tree.
            _flAdv = new DesktopFold("flight", "DkFlFold");
            _flData.Add(_flAdv);
            VisualElement adv = _flAdv.Body;
            BuildBatteryRows(adv); // Lote 1b, tool 8: the estimated health of the battery

            _secMotors = Section(adv);
            var heads = new VisualElement { pickingMode = PickingMode.Ignore };
            heads.AddToClassList("dk-motor-heads");
            _motorRpmHead = Text("dk-reading-name", heads);
            _tagRpm = Text("dk-tag", heads);
            _motorTempHead = Text("dk-reading-name", heads);
            _tagMotorTemp = Text("dk-tag", heads);
            adv.Add(heads);
            for (int i = 0; i < 4; i++)
            {
                var m = new MotorRow { Motor = i + 1 };
                m.Root = new VisualElement { pickingMode = PickingMode.Ignore };
                m.Root.AddToClassList("dk-motor");
                var line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.AddToClassList("dk-motor-line");
                m.Name = Text("dk-motor-name", line);
                m.Rpm = Text("dk-motor-val", line);
                m.Temp = Text("dk-motor-val", line);
                m.Root.Add(line);
                var u = new VisualElement { pickingMode = PickingMode.Ignore };
                u.AddToClassList("dk-motor-u");
                m.UFill = new VisualElement { pickingMode = PickingMode.Ignore };
                m.UFill.AddToClassList("dk-motor-ufill");
                u.Add(m.UFill);
                m.Root.Add(u);
                adv.Add(m.Root);
                _motors[i] = m;
            }

            _propNote = Text("dk-note", adv);
            _propNote.AddToClassList("dk-note--muted");

            _secFc = Section(adv);
            _rFcTemp = AddReading(adv, "fl.fcTemp");
            _rVib = AddReading(adv, "fl.vibration");
            _flSource = Text("dk-note", _flData);
            _flSource.AddToClassList("dk-note--muted");
            DesktopInfo.Beside(_flBadge, true, "DkInfoFlBadge", _flBadgeDetail, _flSource);
            DesktopInfo.Beside(_secMotors, true, "DkInfoFlMotors", _propNote);
            _flightPanel.Add(_flBody);
        }

        private void DisposeFlightTab()
        {
            DisposeAnalysisTab();
            _tabs?.RemoveFromHierarchy();
            _flightPanel?.RemoveFromHierarchy();
            _sheet?.RemoveFromClassList("dk-sheet--flight");
            _sheet?.RemoveFromClassList("dk-sheet--analysis");
        }

        // ───────────────────────── tab control ─────────────────────────

        /// <summary>Selects the tab of the inspector. flight = true also opens the inspector (the readings need no selection).</summary>
        public void ShowFlightTab(bool flight) => ShowTab(flight ? 1 : 0);

        /// <summary>The Analysis tab (charts of the last minute); opens the inspector like the Flight tab does.</summary>
        public void ShowAnalysisTab() => ShowTab(2);

        /// <summary>0 = Pieza, 1 = Vuelo, 2 = Analisis.</summary>
        private void ShowTab(int tab)
        {
            if (_sheet == null) return;
            bool wasAnalysis = _analysisTab;
            _flightTab = tab == 1;
            _analysisTab = tab == 2;
            ApplyTabClasses();
            if (tab != 0)
            {
                UIDetailsSheet.Instance?.ShowInfo();
                _nextFlight = 0f;
                _nextAnalysis = 0f;
                _anForce = true;
            }

            if (_analysisTab && !wasAnalysis && !DesktopMotion.Reduce)
            {
                for (int i = 0; i < _anCells.Count; i++) DesktopMotion.Enter(_anCells[i].Root, 25 + i * 35);
            }
        }

        private void ApplyTabClasses()
        {
            _sheet.EnableInClassList("dk-sheet--flight", _flightTab);
            _sheet.EnableInClassList("dk-sheet--analysis", _analysisTab);
            _tabFlight.EnableInClassList("dk-tab--on", _flightTab);
            _tabAnalysis.EnableInClassList("dk-tab--on", _analysisTab);
            _tabPart.EnableInClassList("dk-tab--on", !_flightTab && !_analysisTab);
        }

        /// <summary>Hook kept in Tick: a new selection brings the Part tab back (the user clicked a part to read about it).</summary>
        private void UIDetailsSheetFlightSync()
        {
            if (_sheet == null) return;
            Transform sel = SelectionManager.Instance != null ? SelectionManager.Instance.CurrentSelection : null;
            if (sel != _lastSelection)
            {
                _lastSelection = sel;
                if (sel != null && (_flightTab || _analysisTab)) ShowFlightTab(false);
            }

            // the sheet closed by itself (close button, Esc): nothing to show on the flight tab any more
            if ((_flightTab || _analysisTab) && _sheet.ClassListContains("details-sheet--hidden") && !ModeOpen) { _flightTab = false; _analysisTab = false; }
            ApplyTabClasses();
        }

        // ───────────────────────── texts ─────────────────────────

        private void RelabelFlightTab()
        {
            if (_tabs == null) return;
            ((Label)_tabPart[0]).text = T("tab.part");
            ((Label)_tabFlight[1]).text = T("tab.flight");
            RelabelAnalysisTab();
            _flEmptyTitle.text = T("fl.empty.title");
            _flEmptyText.text = T("fl.empty");
            _flWaiting.text = T("fl.waiting");
            _secState.text = T("fl.sec.state");
            _flAdv?.SetTitle(T("ux.advanced"));
            _secPos.text = T("fl.sec.pos");
            _secAtt.text = T("fl.sec.att");
            _secBat.text = T("fl.sec.bat");
            _secMotors.text = T("fl.sec.motors");
            _secFc.text = T("fl.sec.fc");
            foreach (Reading r in new[] { _rMode, _rTime, _rAlt, _rHSpeed, _rVSpeed, _rHeading, _rRollPitch, _rBatLevel, _rBatVolt, _rBatCurrent, _rBatTemp, _rFcTemp, _rVib })
                r.Name.text = T(r.Key);
            _motorRpmHead.text = T("fl.rpm");
            _motorTempHead.text = T("fl.temp");
            _propNote.text = T("fl.propnote");
            RelabelBattery();
            _nextFlight = 0f;
        }

        // ───────────────────────── per frame ─────────────────────────

        private void TickFlightTab()
        {
            if (_sheet == null) return;
            bool running = DesktopTelemetry.IsRunning;
            _tabDot.RemoveFromClassList("dk-dot--off");
            foreach (string c in new[] { "dk-dot--live", "dk-dot--connecting", "dk-dot--stale", "dk-dot--error", "dk-dot--off" }) _tabDot.RemoveFromClassList(c);
            _tabDot.AddToClassList(DotClass(running, DesktopTelemetry.LinkState));

            if (!_flightTab) return;
            if (Time.unscaledTime < _nextFlight) return;
            _nextFlight = Time.unscaledTime + FlightPeriod;
            TickFaultDock(DesktopTelemetry.Client); // the docked explanation (open only on request; a no-op while collapsed)

            var client = DesktopTelemetry.Client;
            _flEmpty.style.display = running ? DisplayStyle.None : DisplayStyle.Flex;
            _flBody.style.display = running ? DisplayStyle.Flex : DisplayStyle.None;
            if (!running) return;

            string lang = Lang;
            var labels = HonestyLabels.Load();
            SourceClassification cls = client.Classification;
            _flBadge.text = DesktopTelemetry.BadgeText(lang);
            bool sim = DesktopTelemetry.ReadingsSimulated; // E-5 (and own sessions): the flight simulator is NOT SITL and NOT a shadow: its own approved texts, every reading simulated
            _flBadgeDetail.text = DesktopTelemetry.IsOwnReplay ? T("own.detail") + " " + T("own.readings")
                : sim && labels.TrySimText("detail", lang, out string simDetail)
                ? TelemetryStrings.ForDesktop(simDetail, lang)
                : cls == SourceClassification.Replay && DesktopTelemetry.IsLocalReplay
                    ? TelemetryStrings.LocalReplayDetail(labels, lang)
                    : TelemetryStrings.ForDesktop(labels.ClassificationDetail(cls, lang), lang);

            TelemetryFrame f = client.Displayed;
            HelloMessage h = client.Hello;
            _flWaiting.style.display = f == null ? DisplayStyle.Flex : DisplayStyle.None;
            _flStarved.style.display = client.StarvedMs > TelemetryClient.StaleAfterMs ? DisplayStyle.Flex : DisplayStyle.None;
            if (client.StarvedMs > TelemetryClient.StaleAfterMs) _flStarved.text = TelemetryStrings.F("fl.starved", lang, "s", (client.StarvedMs / 1000.0).ToString("0.0", CultureInfo.InvariantCulture));
            // no frame yet (a session just started or switched): never show the readings of the previous one
            _flData.style.display = f == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (f == null) return;

            ReadingOrigin baseOrigin = FlightOrigins.ForSession(FlightOrigins.Base(cls), sim); // the simulator and an own recording: every reading is simulated
            string baseTag = FlightOrigins.Text(baseOrigin, lang, labels);

            // state
            // PX4 nav_state in the language of the interface (TelemetryStrings.NavStateText); the raw PX4 name stays as the tooltip
            string navRaw = labels.NavState(f.nav);
            string nav = TelemetryStrings.NavStateText(f.nav, navRaw, lang);
            Set(_rMode, nav + " · " + T(f.armed ? "fl.armed" : "fl.disarmed") + (f.landed ? " · " + T("fl.landed") : string.Empty));
            string navTip = nav == navRaw ? string.Empty : "PX4 nav_state " + f.nav + ": " + navRaw;
            if (_rMode.Value.tooltip != navTip) _rMode.Value.tooltip = navTip;
            string time = Clock(f.lt) + (h != null && h.source.HasDurationS ? " / " + Clock(h.source.durationS) : string.Empty);
            Set(_rTime, time);

            // pose and speed
            // contract 1.2 (D-10): the hello says attitude, position and velocity are an EKF2 estimate ("estimado"); without that field the old origin stays
            ReadingOrigin poseOrigin = FlightOrigins.ForSession(FlightOrigins.Pose(h, cls), sim), velOrigin = FlightOrigins.ForSession(FlightOrigins.Velocity(h, cls), sim);
            string poseTag = FlightOrigins.Text(poseOrigin, lang, labels), velTag = FlightOrigins.Text(velOrigin, lang, labels);
            Set(_rAlt, F1(-f.p[2]) + " m", poseOrigin, poseTag);
            Set(_rHSpeed, F1(Math.Sqrt(f.v[0] * f.v[0] + f.v[1] * f.v[1])) + " m/s", velOrigin, velTag);
            Set(_rVSpeed, F1(-f.v[2]) + " m/s", velOrigin, velTag);
            NedFrames.Px4Euler(f.q, out double roll, out double pitch, out double yaw);
            Set(_rHeading, yaw.ToString("0", CultureInfo.InvariantCulture) + " °", poseOrigin, poseTag);
            Set(_rRollPitch, F1(roll) + " ° / " + F1(pitch) + " °", poseOrigin, poseTag);

            // battery
            int cells = h != null && h.batteryCells > 0 ? h.batteryCells : 4;
            Set(_rBatLevel, (f.bat.rem * 100).ToString("0", CultureInfo.InvariantCulture) + " %", baseOrigin, baseTag);
            float fill = (float)Vitals.BatteryFill(f.bat.rem);
            _batFill.style.width = Length.Percent(fill * 100f);
            BatteryClass bc = Vitals.ClassOf(f.bat.rem);
            _batFill.EnableInClassList("dk-bat-fill--low", bc == BatteryClass.Low);
            _batFill.EnableInClassList("dk-bat-fill--crit", bc == BatteryClass.Critical);
            Set(_rBatVolt, f.bat.v.ToString("0.00", CultureInfo.InvariantCulture) + " V · " + (f.bat.v / cells).ToString("0.00", CultureInfo.InvariantCulture) + " V/" + (lang == "en" ? "cell" : "celda"), baseOrigin, baseTag);
            ReadingOrigin oi = FlightOrigins.ForSession(FlightOrigins.BatteryCurrent(h, cls), sim);
            Set(_rBatCurrent, F1(f.bat.i) + " A", oi, FlightOrigins.Text(oi, lang, labels));
            ReadingOrigin obt = FlightOrigins.ForSession(FlightOrigins.BatteryTemp(h, cls), sim);
            Set(_rBatTemp, F1(f.temp[4]) + " °C", obt, FlightOrigins.Text(obt, lang, labels));

            TickBatteryRows(client, f, h, cells, lang, labels);

            // motors
            ReadingOrigin orpm = FlightOrigins.ForSession(FlightOrigins.Rpm(h, cls), sim);
            ReadingOrigin omt = FlightOrigins.ForSession(FlightOrigins.MotorTemp(h, cls), sim);
            SetTag(_tagRpm, orpm, FlightOrigins.Text(orpm, lang, labels));
            SetTag(_tagMotorTemp, omt, FlightOrigins.Text(omt, lang, labels));
            RotorGeometry[] rotors = h != null && h.rotors != null && h.rotors.Length == 4 ? h.rotors : QuadX.Px4QuadX;
            for (int i = 0; i < 4; i++)
            {
                MotorRow m = _motors[i];
                RotorGeometry r = rotors[i];
                int idx = r.motor - 1;
                if (idx < 0 || idx > 3) idx = i;
                string pos = T(r.px >= 0 ? "fl.front" : "fl.rear") + " " + T(r.py >= 0 ? "fl.right" : "fl.left");
                SetText(m.Name, "M" + r.motor + " · " + pos + " · " + (QuadX.SpinSign(r) > 0 ? "CCW" : "CW"));
                SetText(m.Rpm, Math.Round(f.rpm[idx]).ToString("0", CultureInfo.InvariantCulture));
                SetText(m.Temp, F1(f.temp[idx]) + " °C");
                m.UFill.style.width = Length.Percent((float)(Math.Min(1.0, Math.Max(0.0, f.u[idx])) * 100.0));
            }

            // controller and vibration
            ReadingOrigin ofc = FlightOrigins.ForSession(FlightOrigins.FcTemp(h, cls), sim);
            Set(_rFcTemp, ofc == ReadingOrigin.Unavailable ? "—" : F1(f.temp[5]) + " °C", ofc, FlightOrigins.Text(ofc, lang, labels));
            Set(_rVib, F1(f.vib) + " m/s²", baseOrigin, baseTag);

            string src = h != null ? WireText.SourceForUi(h, lang) : null; // contract 1.2: by source.code when the dictionary has it, else the server label
            string srcLine = T("fl.source") + " " + (src ?? string.Empty) + (h != null && h.source.HasVehicle ? " · " + h.source.vehicle : string.Empty) + (h != null && h.source.HasPx4 ? " · PX4 " + h.source.px4 : string.Empty);
            SetText(_flSource, srcLine);
            _hello = h;
        }

        private static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        private static string Clock(double s)
        {
            int total = (int)Math.Max(0, Math.Floor(s));
            return (total / 60) + ":" + (total % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        private static void SetText(Label l, string text)
        {
            if (l.text != text) l.text = text;
        }

        private static void Set(Reading r, string value)
        {
            SetText(r.Value, value);
        }

        private static void Set(Reading r, string value, ReadingOrigin o, string tagText)
        {
            SetText(r.Value, value);
            SetTag(r.Tag, o, tagText);
        }

        private static void SetTag(Label tag, ReadingOrigin o, string text)
        {
            if (tag == null) return;
            SetText(tag, text);
            tag.EnableInClassList("dk-tag--measured", o == ReadingOrigin.Measured);
            tag.EnableInClassList("dk-tag--estimated", o == ReadingOrigin.Estimated);
            tag.EnableInClassList("dk-tag--simulated", o == ReadingOrigin.Simulated);
            tag.EnableInClassList("dk-tag--command", o == ReadingOrigin.Command);
            tag.EnableInClassList("dk-tag--na", o == ReadingOrigin.Unavailable);
        }
    }
}
#endif
