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
    /// PC only (PLAN_PC 27.3): what the user sees and reads when a fault happens.
    ///  - the explanation card (<see cref="DesktopFaultCard"/>) inside the Faults popover (with a picker of the four faults, so every source can read
    ///    what it does with each one), expanded under an alert in the Alerts popover, and opened from the banner;
    ///  - a banner when a synthetic fault is injected or a critical alert appears, with «Ver qué pasa»;
    ///  - a marker pinned on the affected part (<see cref="DesktopFaultMarkers"/>) while the fault is active or a critical alert stands on it.
    /// Honesty: the card is labelled «Explicación didáctica · no es una medición»; «falla sintética» only for the local replay's own faults; the
    /// simulator injects no faults and in PX4 SITL the card says what PX4 / Gazebo really do (approved wording, PLAN_CONTROL_REAL_Y_ENTORNOS 2.4).
    /// </summary>
    internal sealed partial class DesktopTelemetryUI
    {
        private DesktopFaultCard _cardFaults;
        private VisualElement _fxPickRow;
        private ScrollView _fxScroll;
        private readonly Button[] _fxPickBtns = new Button[4];
        private static readonly FaultTopic[] FxTopics = { FaultTopic.MotorFailure, FaultTopic.MotorOverheat, FaultTopic.BatterySag, FaultTopic.Vibration };
        private FaultTopic _fxTopic = FaultTopic.MotorFailure;

        private readonly HashSet<string> _alertOpen = new HashSet<string>();

        private VisualElement _banner;
        private Label _bannerText;
        private Button _bannerView;
        private float _bannerUntil;
        private Func<string, string> _bannerMake;
        private string _bannerLang;
        private bool _bannerIsFault;
        private FaultTopic _bannerTopic;
        private int _bannerMotor;
        private string _bannerAlertId;
        private string _bannerComponent;
        private float _bannerFaultAt = -100f;
        private readonly HashSet<string> _bnFaults = new HashSet<string>();
        private readonly HashSet<string> _bnAlerts = new HashSet<string>();
        private readonly List<string> _bnGone = new List<string>();

        private DesktopFaultMarkers _markers;
        private readonly List<DesktopFaultMarkers.Target> _fxTargets = new List<DesktopFaultMarkers.Target>();

        // ── read by the verification harness ──
        public DesktopFaultCard FaultsCard => _cardFaults;
        public bool BannerVisible => _banner != null && _banner.style.display != DisplayStyle.None;
        public string BannerTextNow => _bannerText?.text;
        public Button BannerViewButton => _bannerView;
        public DesktopFaultMarkers FaultMarkers => _markers;
        public FaultTopic ExplainTopic => _fxTopic;
        public bool AlertExpanded(string id) => _alertOpen.Contains(id);
        public IReadOnlyList<DesktopFaultCard> AlertCards
        {
            get
            {
                var l = new List<DesktopFaultCard>();
                foreach (AlertRow r in _alertRows) if (r.Card != null) l.Add(r.Card);
                return l;
            }
        }

        // ───────────────────────── build ─────────────────────────

        private void BuildFaultVisuals()
        {
            _banner = new VisualElement { name = "DkFaultBanner", pickingMode = PickingMode.Position };
            _banner.AddToClassList("dk-fx-banner");
            _banner.style.display = DisplayStyle.None;
            var icon = new DesktopVectorIcon("fault", 16) { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("dk-fx-bannericon");
            _banner.Add(icon);
            _bannerText = new Label { name = "DkFaultBannerText", pickingMode = PickingMode.Ignore };
            _bannerText.AddToClassList("dk-fx-bannertext");
            _banner.Add(_bannerText);
            _bannerView = new Button(OnBannerView) { name = "DkFaultBannerView" };
            _bannerView.AddToClassList("dk-b");
            _bannerView.AddToClassList("dk-b--primary");
            _bannerView.AddToClassList("dk-fx-bannerbtn");
            _banner.Add(_bannerView);
            var close = _chrome.IconButtonPublic("close", "DkFaultBannerClose", HideBanner, 14);
            close.AddToClassList("dk-ibtn--small");
            _banner.Add(close);
            // a click on the banner never reaches the 3D scene (same guard as the shortcuts pill)
            _banner.RegisterCallback<PointerDownEvent>(evt => { if (evt.button == 0) InputManager.InputBlocked = true; evt.StopPropagation(); });
            _banner.RegisterCallback<PointerUpEvent>(evt => { if (evt.button == 0) InputManager.InputBlocked = false; evt.StopPropagation(); });
            _root.Add(_banner);
            _markers = new DesktopFaultMarkers(_root);
        }

        private void DisposeFaultVisuals()
        {
            _banner?.RemoveFromHierarchy();
            _markers?.Dispose();
        }

        /// <summary>The explanation block of the Faults popover: a picker of the four faults and the card (scrolls when the window is short).</summary>
        private void BuildFaultExplain(VisualElement pop)
        {
            _fxScroll = new ScrollView(ScrollViewMode.Vertical) { name = "DkFxScroll" };
            _fxScroll.AddToClassList("dk-fx-scroll");
            _fxPickRow = new VisualElement { name = "DkFxPick" };
            _fxPickRow.AddToClassList("dk-fx-pick");
            for (int i = 0; i < 4; i++)
            {
                FaultTopic t = FxTopics[i];
                var b = new Button(() => { _fxTopic = t; _nextPopTick = 0f; }) { name = "DkFxPick_" + WireNames.Of(FaultExplain.KindOf(t)) };
                b.AddToClassList("dk-b");
                b.AddToClassList("dk-b--pill");
                b.AddToClassList("dk-fx-pickbtn");
                _fxPickRow.Add(b);
                _fxPickBtns[i] = b;
            }
            _fxScroll.Add(_fxPickRow);
            _cardFaults = new DesktopFaultCard("DkFxCardFaults");
            _fxScroll.Add(_cardFaults.Root);
            pop.Add(_fxScroll);
            RelabelFaultExplain();
        }

        private void RelabelFaultExplain()
        {
            for (int i = 0; i < 4; i++) if (_fxPickBtns[i] != null) _fxPickBtns[i].text = FaultExplain.KindTab(FxTopics[i], Lang);
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

        /// <summary>
        /// A scrolling block of a popover (the fault card, the alert list) takes ALL the room left under its top edge, down to the status bar: in a short
        /// window the approved texts (above all «En esta fuente») are as visible as the window allows and the rest scrolls.
        /// </summary>
        private void FitScroll(VisualElement scroll, float reserveBelow)
        {
            if (scroll == null || _root.panel == null) return;
            Rect wb = scroll.worldBound;
            if (float.IsNaN(wb.yMin) || wb.height <= 0f) return;
            float want = Mathf.Max(140f, _root.worldBound.yMax - wb.yMin - reserveBelow - 40f);
            if (scroll.style.maxHeight.keyword != StyleKeyword.Undefined || Mathf.Abs(scroll.style.maxHeight.value.value - want) > 1f) scroll.style.maxHeight = want;
        }

        /// <summary>Updates the card of the Faults popover (called by TickFaults).</summary>
        private void TickFaultCard(TelemetryClient client)
        {
            if (_cardFaults == null) return;
            for (int i = 0; i < 4; i++) _fxPickBtns[i].EnableInClassList("dk-b--on", FxTopics[i] == _fxTopic);
            string what = WhatNow(client, _fxTopic, _faultMotor, out int motor);
            _cardFaults.Set(CardInput(_fxTopic, motor != 0 ? motor : _faultMotor, null, what), Lang, HonestyLabels.Load());
        }

        // ───────────────────────── open the card ─────────────────────────

        /// <summary>Opens the Faults popover on the explanation of a fault (the banner's «Ver qué pasa»).</summary>
        public void OpenFaultCard(FaultTopic topic, int motor)
        {
            _fxTopic = topic;
            if (motor >= 1 && motor <= 4) { _faultMotor = motor; RefreshMotorButtons(); }
            OpenMode();
            if (!ModeOpen) return;
            if (_openPop != "faults") TogglePopover("faults");
            _nextPopTick = 0f;
            TickPopovers(true);
        }

        /// <summary>Opens the Alerts popover with that alert expanded (the banner's «Ver qué pasa» for a critical alert).</summary>
        public void OpenAlertCard(string alertId)
        {
            if (!string.IsNullOrEmpty(alertId)) _alertOpen.Add(alertId);
            OpenMode();
            if (!ModeOpen) return;
            if (_openPop != "alerts") TogglePopover("alerts");
            _alertSig = null;
            TickPopovers(true);
        }

        public void ToggleAlertCard(string alertId)
        {
            if (string.IsNullOrEmpty(alertId)) return;
            if (!_alertOpen.Remove(alertId)) _alertOpen.Add(alertId);
            _nextPopTick = 0f;
        }

        // ───────────────────────── banner ─────────────────────────

        private void ShowBanner(Func<string, string> make, bool isFault, FaultTopic topic, int motor, string alertId, string component)
        {
            _bannerMake = make;
            _bannerIsFault = isFault;
            _bannerTopic = topic;
            _bannerMotor = motor;
            _bannerAlertId = alertId;
            _bannerComponent = component;
            _bannerLang = null;
            _bannerUntil = Time.unscaledTime + 10f;
            _banner.style.display = DisplayStyle.Flex;
            _banner.EnableInClassList("dk-fx-banner--fault", isFault);
            _banner.EnableInClassList("dk-fx-banner--in", DesktopMotion.Reduce);
            if (!DesktopMotion.Reduce) { _banner.RemoveFromClassList("dk-fx-banner--in"); _banner.schedule.Execute(() => _banner.AddToClassList("dk-fx-banner--in")).StartingIn(30); }
            _banner.BringToFront();
            RefreshBannerText();
            Debug.Log("[Faults] banner: " + _bannerText.text);
        }

        private void RefreshBannerText()
        {
            string lang = Lang;
            if (_banner == null || _bannerMake == null || _bannerLang == lang) return;
            _bannerLang = lang;
            _bannerText.text = _bannerMake(lang);
            _bannerView.text = TelemetryStrings.T("fx.banner.view", lang);
        }

        public void HideBanner()
        {
            if (_banner == null) return;
            _banner.style.display = DisplayStyle.None;
            _banner.RemoveFromClassList("dk-fx-banner--in");
        }

        private void OnBannerView()
        {
            if (_bannerIsFault) OpenFaultCard(_bannerTopic, _bannerMotor);
            else OpenAlertCard(_bannerAlertId);
            HideBanner();
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

        // ───────────────────────── per frame: banner triggers and part markers ─────────────────────────

        private void TickFaultVisuals()
        {
            if (_banner == null) return;
            var client = DesktopTelemetry.Client;
            bool viewer = !_ui.IsHeroVisible && !_ui.IsOnboardingOpen;
            if (client == null)
            {
                if (BannerVisible) HideBanner();
                if (_bnFaults.Count > 0) _bnFaults.Clear();
                if (_bnAlerts.Count > 0) _bnAlerts.Clear();
                if (_fxTargets.Count > 0 || _markers.Count > 0) { _fxTargets.Clear(); _markers.SetTargets(_fxTargets); }
                return;
            }

            string lang = Lang;
            TelemetryFrame disp = client.Displayed;
            double now = disp != null ? disp.ts : ReplaySchedule.WallClockMs();
            _fxTargets.Clear();

            // synthetic faults (only the local replay has them): a marker while active and a banner the first time they are seen
            List<string> coveredComponents = null;
            if (DesktopTelemetry.IsLocalReplay && client.Faults.Count > 0)
            {
                coveredComponents = new List<string>(2);
                foreach (Fault ft in Faults.Active(client.Faults, now))
                {
                    string comp = FaultComponent(ft);
                    string key = ft.id + "@" + ft.since;
                    coveredComponents.Add(comp);
                    _fxTargets.Add(new DesktopFaultMarkers.Target
                    {
                        Key = "f:" + key,
                        Component = comp,
                        Label = FaultExplain.PinName(ft.kind, ft.motor, lang) + " · " + AlertText.SyntheticFault(lang, HonestyLabels.Load()),
                        Synthetic = true,
                    });
                    if (_bnFaults.Add(key))
                    {
                        Fault captured = ft.Clone();
                        _bannerFaultAt = Time.unscaledTime;
                        ShowBanner(l => TelemetryStrings.F("fx.banner.fault", l, "name", FaultExplain.StateName(captured.kind, captured.motor, l), "syn", AlertText.SyntheticFault(l, HonestyLabels.Load())),
                                   true, FaultExplain.TopicOf(captured.kind), captured.motor, null, comp);
                    }
                }
            }

            // critical alerts: a marker while they stand; a banner when a new one appears (not when a fault banner already covers that part)
            var alerts = client.Alerts;
            for (int i = 0; i < alerts.Count; i++)
            {
                Alert a = alerts[i];
                if (a.severity != AlertSeverity.Critical || string.IsNullOrEmpty(a.component)) continue;
                bool covered = coveredComponents != null && coveredComponents.Contains(a.component);
                if (!covered)
                {
                    string part = PartName(a.component, lang);
                    _fxTargets.Add(new DesktopFaultMarkers.Target
                    {
                        Key = "a:" + a.id,
                        Component = a.component,
                        Label = TelemetryStrings.F("fx.pin.alert", lang, "part", part),
                        Synthetic = false,
                    });
                }
                if (_bnAlerts.Add(a.id) && !covered && Time.unscaledTime - _bannerFaultAt > 8f)
                {
                    Alert captured = a;
                    ShowBanner(l => TelemetryStrings.F("fx.banner.alert", l, "part", PartName(captured.component, l)), false, FaultExplain.TopicOfAlert(a.id), MotorOfComponent(a.component), a.id, a.component);
                }
            }

            if (_bnAlerts.Count > 0)
            {
                _bnGone.Clear();
                foreach (string id in _bnAlerts)
                {
                    bool present = false;
                    for (int i = 0; i < alerts.Count; i++) if (alerts[i].id == id) { present = true; break; }
                    if (!present) _bnGone.Add(id);
                }
                foreach (string id in _bnGone) _bnAlerts.Remove(id);
            }

            _markers.SetTargets(_fxTargets);
            _markers.Tick(viewer);

            if (BannerVisible)
            {
                if (!viewer || Time.unscaledTime >= _bannerUntil) HideBanner();
                else RefreshBannerText();
            }
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
