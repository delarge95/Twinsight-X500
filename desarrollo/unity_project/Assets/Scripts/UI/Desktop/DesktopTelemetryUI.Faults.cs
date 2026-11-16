#if UNITY_STANDALONE
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.Telemetry;
using WebGL.UI.ProceduralIcons;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PC only (PLAN_PC 27.3, reworked in 28.1): what the user sees and reads when a fault happens.
    ///  - INJECTION is discreet: a small marker pinned on the affected part (<see cref="DesktopFaultMarkers"/>) and the compact status in the Faults
    ///    popover. Nothing opens by itself: no banner, no toast, no auto-opened card.
    ///  - the EXPLANATION card (<see cref="DesktopFaultCard"/>, same approved texts) lives in the docked inspector, as a collapsible section at the
    ///    top of the «Vuelo» tab, COLLAPSED by default. Only an explicit action opens it: the «Explicación» button of the Faults popover, the
    ///    «Explicación» button of an alert, or the section header itself. It never covers the canvas.
    /// Honesty: the card is labelled «Explicación didáctica · no es una medición»; «falla sintética» only for the local replay's own faults; the
    /// simulator injects no faults and in PX4 SITL the card says what PX4 / Gazebo really do (approved wording, PLAN_CONTROL_REAL_Y_ENTORNOS 2.4).
    /// </summary>
    internal sealed partial class DesktopTelemetryUI
    {
        // docked explanation (a section of the Flight tab)
        private VisualElement _fxDock, _fxDockBody, _fxPickRow;
        private Button _fxDockHead;
        private Label _fxDockTitle, _fxDockState, _fxDockHint;
        private DesktopFaultCard _cardDock;
        private readonly Button[] _fxPickBtns = new Button[4];
        private static readonly FaultTopic[] FxTopics = { FaultTopic.MotorFailure, FaultTopic.MotorOverheat, FaultTopic.BatterySag, FaultTopic.Vibration };
        private FaultTopic _fxTopic = FaultTopic.MotorFailure;
        private bool _dockOpen;
        private string _dockAlertId;

        private DesktopFaultMarkers _markers;
        private readonly List<DesktopFaultMarkers.Target> _fxTargets = new List<DesktopFaultMarkers.Target>();

        // ── read by the verification harness ──
        public DesktopFaultCard FaultsCard => _cardDock;
        public DesktopFaultCard DockCard => _cardDock;
        public bool DockOpen => _dockOpen;
        public string DockAlertId => _dockAlertId;
        public VisualElement FaultDock => _fxDock;
        public DesktopFaultMarkers FaultMarkers => _markers;
        public FaultTopic ExplainTopic => _fxTopic;
        public bool AlertExpanded(string id) => _dockOpen && _dockAlertId == id;

        // ───────────────────────── build ─────────────────────────

        private void BuildFaultVisuals()
        {
            _markers = new DesktopFaultMarkers(_root);
        }

        private void DisposeFaultVisuals()
        {
            _markers?.Dispose();
            _fxDock?.RemoveFromHierarchy();
        }

        /// <summary>
        /// The docked explanation: a collapsible section at the top of the Flight tab (collapsed by default). Header = title + «Mostrar/Ocultar»;
        /// body = a picker of the four faults, the replay note and the card. Built after the sheet tabs (it lives inside the Flight panel).
        /// </summary>
        private void BuildFaultDock()
        {
            if (_flightPanel == null) return;
            _fxDock = new VisualElement { name = "DkFxDock", pickingMode = PickingMode.Ignore };
            _fxDock.AddToClassList("dk-fxdock");

            _fxDockHead = new Button(() => SetDockOpen(!_dockOpen)) { name = "DkFxDockHead" };
            _fxDockHead.AddToClassList("dk-fxdock-head");
            var icon = new DesktopVectorIcon("fault", 14) { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("dk-fxdock-icon");
            _fxDockHead.Add(icon);
            _fxDockTitle = new Label { pickingMode = PickingMode.Ignore };
            _fxDockTitle.AddToClassList("dk-fxdock-title");
            _fxDockHead.Add(_fxDockTitle);
            _fxDockState = new Label { pickingMode = PickingMode.Ignore };
            _fxDockState.AddToClassList("dk-fxdock-state");
            _fxDockHead.Add(_fxDockState);
            _fxDock.Add(_fxDockHead);

            _fxDockBody = new VisualElement { name = "DkFxDockBody", pickingMode = PickingMode.Ignore };
            _fxDockBody.AddToClassList("dk-fxdock-body");
            _fxDockBody.style.display = DisplayStyle.None;
            _fxDockHint = Text("dk-note", _fxDockBody);
            _fxDockHint.AddToClassList("dk-note--muted");
            _faultNote = Text("dk-note", _fxDockBody);
            _faultNote.AddToClassList("dk-note--muted");
            _fxPickRow = new VisualElement { name = "DkFxPick" };
            _fxPickRow.AddToClassList("dk-fx-pick");
            for (int i = 0; i < 4; i++)
            {
                FaultTopic t = FxTopics[i];
                var b = new Button(() => { _fxTopic = t; _dockAlertId = null; TickFaultDock(DesktopTelemetry.Client); }) { name = "DkFxPick_" + WireNames.Of(FaultExplain.KindOf(t)) };
                b.AddToClassList("dk-b");
                b.AddToClassList("dk-b--pill");
                b.AddToClassList("dk-fx-pickbtn");
                _fxPickRow.Add(b);
                _fxPickBtns[i] = b;
            }

            _fxDockBody.Add(_fxPickRow);
            _cardDock = new DesktopFaultCard("DkFxCardDock");
            _fxDockBody.Add(_cardDock.Root);
            _fxDock.Add(_fxDockBody);
            _flightPanel.Insert(0, _fxDock);
            RelabelFaultExplain();
        }

        private void RelabelFaultExplain()
        {
            if (_fxDock == null) return;
            for (int i = 0; i < 4; i++) if (_fxPickBtns[i] != null) _fxPickBtns[i].text = FaultExplain.KindTab(FxTopics[i], Lang);
            _fxDockTitle.text = T("fx.dock.title");
            _fxDockState.text = T(_dockOpen ? "fx.collapse" : "fx.show");
            _fxDockHint.text = T("fx.dock.hint");
            _faultNote.text = T("faults.note");
            TickFaultDock(DesktopTelemetry.Client);
        }

        // ───────────────────────── open / close the docked explanation ─────────────────────────

        /// <summary>Opens or closes the docked section. Never called by the telemetry itself: only a click or an explicit action does.</summary>
        public void SetDockOpen(bool open)
        {
            if (_fxDock == null) return;
            _dockOpen = open;
            _fxDockBody.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            _fxDockHead.EnableInClassList("dk-fxdock-head--on", open);
            _fxDockState.text = T(open ? "fx.collapse" : "fx.show");
            if (open) TickFaultDock(DesktopTelemetry.Client);
        }

        /// <summary>
        /// The explicit actions («Explicación» in the Faults popover or on an alert): selects the topic (and the alert, when it came from one),
        /// opens the inspector on the Flight tab and expands the section. The canvas is not touched.
        /// </summary>
        public void ShowFaultExplanation(FaultTopic topic, int motor, string alertId)
        {
            _fxTopic = topic;
            if (motor >= 1 && motor <= 4) { _faultMotor = motor; RefreshMotorButtons(); }
            _dockAlertId = alertId;
            ShowFlightTab(true);
            SetDockOpen(true);
            if (_flightPanel != null) _flightPanel.scrollOffset = Vector2.zero;
            _alertSig = null;
        }

        // ───────────────────────── card content ─────────────────────────

        private static ExplainSource ExplainSourceNow()
        {
            switch (DesktopTelemetryUIAccess.Active())
            {
                case TelSource.LocalReplay: return ExplainSource.LocalReplay;
                case TelSource.Simulator: return ExplainSource.Simulator;
                case TelSource.Sitl: return ExplainSource.Sitl;
                default: return ExplainSource.None;
            }
        }

        private DesktopFaultCard.Input CardInput(FaultTopic topic, int motor, string alertId, string what)
        {
            var client = DesktopTelemetry.Client;
            return new DesktopFaultCard.Input
            {
                Topic = topic,
                Motor = motor,
                AlertId = alertId,
                WhatText = what,
                Source = ExplainSourceNow(),
                SourceBadge = DesktopTelemetry.IsRunning ? DesktopTelemetry.BadgeText(Lang) : null,
                Hello = client?.Hello,
                Classification = client != null ? client.Classification : SourceClassification.Inspector,
                Simulator = DesktopTelemetry.IsSimulator,
            };
        }

        private static int MotorOfComponent(string component)
        {
            if (component != null && component.StartsWith("motor", StringComparison.Ordinal) && int.TryParse(component.Substring(5), out int m)) return m;
            return 0;
        }

        /// <summary>The active synthetic fault of the local replay that this topic stands for (the selected motor wins), or null.</summary>
        private Fault ActiveFaultOf(TelemetryClient client, FaultTopic topic, int preferredMotor, double now)
        {
            if (client == null || !DesktopTelemetry.IsLocalReplay) return null;
            Fault found = null;
            foreach (Fault f in Faults.Active(client.Faults, now))
            {
                if (FaultExplain.TopicOf(f.kind) != topic) continue;
                if (found == null || (f.HasMotor && f.motor == preferredMotor)) found = f;
            }
            return found;
        }

        private Alert AlertOfTopic(TelemetryClient client, FaultTopic topic, int motor)
        {
            if (client == null) return null;
            var list = client.Alerts;
            Alert any = null;
            for (int i = 0; i < list.Count; i++)
            {
                if (FaultExplain.TopicOfAlert(list[i].id) != topic) continue;
                int am = MotorOfComponent(list[i].component);
                if (am == 0 || motor == 0 || am == motor) return list[i];
                any = any ?? list[i];
            }
            return any;
        }

        /// <summary>The first block for a topic: the synthetic fault with its countdown and/or the alert sentence (with origin). Empty when neither is active.</summary>
        private string WhatNow(TelemetryClient client, FaultTopic topic, int motor, out int usedMotor)
        {
            usedMotor = motor;
            if (client == null) return string.Empty;
            string lang = Lang;
            double now = client.Displayed != null ? client.Displayed.ts : ReplaySchedule.WallClockMs();
            var labels = HonestyLabels.Load();
            var parts = new List<string>();
            // Lote 1a: the simulator's own cut (a simplified model): «Motor N · cortado · simulado»
            int simCut = DesktopTelemetry.IsSimulator ? SimCutMotor() : 0;
            if (simCut != 0 && topic == FaultTopic.MotorFailure)
            {
                parts.Add(TelemetryStrings.F("simfault.status", lang, "n", simCut));
                usedMotor = simCut;
            }
            Fault f = ActiveFaultOf(client, topic, motor, now);
            if (f != null)
            {
                parts.Add(FaultExplain.FaultLine(f, now, lang));
                if (f.HasMotor && f.motor != 0) usedMotor = f.motor;
            }
            Alert a = AlertOfTopic(client, topic, usedMotor);
            if (a != null && client.Displayed != null)
            {
                bool synthetic = DesktopTelemetry.IsLocalReplay && AlertText.IsSynthetic(a, client.Faults, now);
                parts.Add(AlertText.Build(a, client.Displayed, client.Hello, client.Classification, synthetic, lang, labels));
                int am = MotorOfComponent(a.component);
                if (am != 0) usedMotor = am;
            }
            return string.Join("\n", parts);
        }

        /// <summary>The first block when the section was opened from one alert: the alert's own sentence (with origin) plus the synthetic fault line that hits its part.</summary>
        private string WhatForAlert(TelemetryClient client, Alert a, FaultTopic topic, double now)
        {
            string lang = Lang;
            var labels = HonestyLabels.Load();
            bool synthetic = DesktopTelemetry.IsLocalReplay && AlertText.IsSynthetic(a, client.Faults, now);
            string what = AlertText.Build(a, client.Displayed, client.Hello, client.Classification, synthetic, lang, labels);
            Fault af = FaultExplain.IsApproved(topic) ? ActiveFaultOf(client, topic, MotorOfComponent(a.component), now) : null;
            if (af != null && AlertText.FaultHitsComponent(af, a.component)) what += "\n" + FaultExplain.FaultLine(af, now, lang);
            return what;
        }

        /// <summary>
        /// A scrolling block of a popover (the alert list) takes ALL the room left under its top edge, down to the status bar: in a short
        /// window the list is as visible as the window allows and the rest scrolls.
        /// </summary>
        private void FitScroll(VisualElement scroll, float reserveBelow)
        {
            if (scroll == null || _root.panel == null) return;
            Rect wb = scroll.worldBound;
            if (float.IsNaN(wb.yMin) || wb.height <= 0f) return;
            float want = Mathf.Max(140f, _root.worldBound.yMax - wb.yMin - reserveBelow - 40f);
            if (scroll.style.maxHeight.keyword != StyleKeyword.Undefined || Mathf.Abs(scroll.style.maxHeight.value.value - want) > 1f) scroll.style.maxHeight = want;
        }

        /// <summary>Updates the docked card (called by the Flight tab tick; costs nothing while the section is collapsed).</summary>
        private void TickFaultDock(TelemetryClient client)
        {
            if (_cardDock == null || !_dockOpen) return;
            _faultNote.style.display = DesktopTelemetry.IsLocalReplay ? DisplayStyle.Flex : DisplayStyle.None;

            double now = client != null && client.Displayed != null ? client.Displayed.ts : ReplaySchedule.WallClockMs();
            string what;
            int motor;
            string alertId = null;
            Alert from = null;
            if (!string.IsNullOrEmpty(_dockAlertId) && client != null)
            {
                var list = client.Alerts;
                for (int i = 0; i < list.Count; i++) if (list[i].id == _dockAlertId) { from = list[i]; break; }
                if (from == null) _dockAlertId = null; // the alert went away: the card keeps the topic and falls back to «ninguna activa»
            }

            if (from != null && client.Displayed != null)
            {
                alertId = from.id;
                _fxTopic = FaultExplain.TopicOfAlert(from.id);
                motor = MotorOfComponent(from.component);
                what = WhatForAlert(client, from, _fxTopic, now);
            }
            else
            {
                what = WhatNow(client, _fxTopic, _faultMotor, out motor);
            }

            for (int i = 0; i < 4; i++) _fxPickBtns[i].EnableInClassList("dk-b--on", FxTopics[i] == _fxTopic);
            _cardDock.Set(CardInput(_fxTopic, motor != 0 ? motor : _faultMotor, alertId, what), Lang, HonestyLabels.Load());
        }

        private static string FaultComponent(Fault f)
        {
            switch (f.kind)
            {
                case FaultKind.MotorFailure:
                case FaultKind.MotorOverheat: return "motor" + (f.HasMotor && f.motor != 0 ? f.motor : 1);
                case FaultKind.BatterySag: return "battery";
                default: return "fc";
            }
        }

        // ───────────────────────── per frame: the markers pinned on the parts (no banner, no toast, nothing opens) ─────────────────────────

        private void TickFaultVisuals()
        {
            if (_markers == null) return;
            var client = DesktopTelemetry.Client;
            bool viewer = !_ui.IsHeroVisible && !_ui.IsOnboardingOpen;
            if (client == null)
            {
                if (_fxTargets.Count > 0 || _markers.Count > 0) { _fxTargets.Clear(); _markers.SetTargets(_fxTargets); }
                return;
            }

            string lang = Lang;
            TelemetryFrame disp = client.Displayed;
            double now = disp != null ? disp.ts : ReplaySchedule.WallClockMs();
            _fxTargets.Clear();

            // synthetic faults (only the local replay has them): a marker while they are active
            List<string> coveredComponents = null;
            if (DesktopTelemetry.IsLocalReplay && client.Faults.Count > 0)
            {
                coveredComponents = new List<string>(2);
                foreach (Fault ft in Faults.Active(client.Faults, now))
                {
                    string comp = FaultComponent(ft);
                    coveredComponents.Add(comp);
                    _fxTargets.Add(new DesktopFaultMarkers.Target
                    {
                        Key = "f:" + ft.id + "@" + ft.since,
                        Component = comp,
                        Label = FaultExplain.PinName(ft.kind, ft.motor, lang) + " · " + AlertText.SyntheticFault(lang, HonestyLabels.Load()),
                        Synthetic = true,
                    });
                }
            }

            // Lote 1a: the simulator's cut motor: a marker pinned on it with «Motor N · cortado · simulado» (the propeller stops by itself: its rpm is 0)
            int simCut = DesktopTelemetry.IsSimulator ? SimCutMotor() : 0;
            if (simCut != 0)
            {
                string comp = "motor" + simCut;
                if (coveredComponents == null) coveredComponents = new List<string>(1);
                coveredComponents.Add(comp);
                _fxTargets.Add(new DesktopFaultMarkers.Target
                {
                    Key = "s:cut" + simCut,
                    Component = comp,
                    Label = TelemetryStrings.F("simfault.status", lang, "n", simCut),
                    Synthetic = true,
                });
            }

            // critical alerts: a marker while they stand (not when a fault marker already covers that part)
            var alerts = client.Alerts;
            for (int i = 0; i < alerts.Count; i++)
            {
                Alert a = alerts[i];
                if (a.severity != AlertSeverity.Critical || string.IsNullOrEmpty(a.component)) continue;
                if (coveredComponents != null && coveredComponents.Contains(a.component)) continue;
                _fxTargets.Add(new DesktopFaultMarkers.Target
                {
                    Key = "a:" + a.id,
                    Component = a.component,
                    Label = TelemetryStrings.F("fx.pin.alert", lang, "part", PartName(a.component, lang)),
                    Synthetic = false,
                });
            }

            _markers.SetTargets(_fxTargets);
            _markers.Tick(viewer);
        }
    }

    /// <summary>Static access to the active source for the static helpers of the telemetry UI.</summary>
    internal static class DesktopTelemetryUIAccess
    {
        public static TelSource Active()
        {
            if (!DesktopTelemetry.IsRunning) return TelSource.Inspector;
            if (DesktopTelemetry.IsSimulator) return TelSource.Simulator;
            if (DesktopTelemetry.Client.Classification == SourceClassification.Sitl) return TelSource.Sitl;
            return TelSource.LocalReplay;
        }
    }
}
#endif
