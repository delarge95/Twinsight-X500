#if UNITY_STANDALONE
using System;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.Telemetry;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    internal enum TelSource { Inspector, LocalReplay, Simulator, Sitl, OwnReplay }

    /// <summary>
    /// PC only (WP-11, PLAN_PC 22). The desktop face of the telemetry session (<see cref="DesktopTelemetry"/>):
    ///  - mode 4 "Telemetria" (key 4, 4th segment of the mode switch): a tool strip with the session switch ("Encendido"), the source selector
    ///    (key L), the alerts list (Shift+A) and the synthetic fault injection;
    ///  - the status chip of the top bar (DkTopSlotFuture): link-state dot + the honest badge text (HonestyLabels), click opens the mode;
    ///    plus a second chip, the active session, click = stop;
    ///  - the "Vuelo" tab of the inspector (see DesktopTelemetryUI.Flight.cs).
    /// All logic stays in <see cref="DesktopTelemetry"/> / <see cref="TelemetryClient"/>; the honesty texts come from HonestyLabels and the
    /// other strings from <see cref="TelemetryStrings"/> (ES/EN). Nothing here exists in WebGL.
    /// </summary>
    internal sealed partial class DesktopTelemetryUI
    {
        public const string PrefSource = "Desk_Source";
        public const string PrefSitlUrl = "Desk_SitlUrl";

        private readonly VisualElement _root;
        private readonly DesktopChrome _chrome;
        private readonly UIManager _ui;

        // top bar
        private Button _statusChip;
        private VisualElement _statusDot;
        private Label _statusText;
        private Button _sessionChip;

        // mode 4 strip
        private VisualElement _container;
        private VisualElement _strip;
        private Button _btnPower, _btnSource, _btnAlerts, _btnFaults;   // + _btnAnalysis (DesktopTelemetryUI.Analysis.cs)
        private Label _powerLabel, _stateLabel, _sourceLabel, _alertsLabel, _alertsCount, _faultsLabel;
        private VisualElement _bar2;

        // status bar
        private Label _statusLink;

        // session state
        private TelSource _preferred = TelSource.LocalReplay;
        private string _sitlUrl;
        private string _sitlError;
        private string _startError;
        private bool _wasRunning;
        private LinkState _lastLink = LinkState.Disconnected;
        private string _lastBadge;
        private string _lastLang;
        private IVisualElementScheduledItem _pulse;
        private float _nextSlowTick;

        public bool ModeOpen { get; private set; }
        public Button StatusChip => _statusChip;
        public Button SessionChip => _sessionChip;
        public bool IsRunning => DesktopTelemetry.IsRunning;
        private static string Lang => AppLanguageManager.IsSpanish ? "es" : "en";
        private static string T(string key) => TelemetryStrings.T(key, Lang);

        public DesktopTelemetryUI(VisualElement root, DesktopChrome chrome, UIManager ui)
        {
            _root = root;
            _chrome = chrome;
            _ui = ui;

            _sitlUrl = PlayerPrefs.GetString(PrefSitlUrl, TelemetryEndpoints.Sitl);
            if (TelemetryEndpoints.CommandLine("-sitl-url") != null) _sitlUrl = TelemetryEndpoints.CommandLine("-sitl-url");
            string pref = PlayerPrefs.GetString(PrefSource, "replay");
            _preferred = pref == "sitl" ? TelSource.Sitl : pref == "sim" ? TelSource.Simulator : TelSource.LocalReplay;

            BuildChips();
            AppLanguageManager.LanguageChanged += OnLanguage;
            DesktopTelemetry.RunningChanged += OnRunningChanged;
            SelectionManager.GroundClickHook = TryGroundClick; // E-6: click on the ground grid = fly_to
        }

        public void Dispose()
        {
            AppLanguageManager.LanguageChanged -= OnLanguage;
            DesktopTelemetry.RunningChanged -= OnRunningChanged;
            if (SelectionManager.GroundClickHook == (System.Func<Vector2, bool>)TryGroundClick) SelectionManager.GroundClickHook = null;
            _legend?.RemoveFromHierarchy();
            _toast?.RemoveFromHierarchy();
            DisposeFaultVisuals();
            DisposeEnv();
            _pulse?.Pause();
            _statusChip?.RemoveFromHierarchy();
            _sessionChip?.RemoveFromHierarchy();
            _container?.RemoveFromHierarchy();
            DisposeFlightTab();
            UIDetailsSheet.DesktopKeepOpenOnDeselect = null;
        }

        // ───────────────────────── top bar chips ─────────────────────────

        private void BuildChips()
        {
            _statusChip = new Button(() => OpenMode()) { name = "DkChipStatus" };
            _statusChip.AddToClassList("dk-stchip");
            _statusDot = new VisualElement { name = "DkStatusDot", pickingMode = PickingMode.Ignore };
            _statusDot.AddToClassList("dk-dot");
            _statusDot.AddToClassList("dk-dot--off");
            _statusText = new Label { name = "DkStatusText", pickingMode = PickingMode.Ignore };
            _statusText.AddToClassList("dk-stchip-text");
            _statusChip.Add(_statusDot);
            _statusChip.Add(_statusText);

            // the active-session chip: same chip as the filter / explode / cut / power ones (entry + exit animation), click = stop
            _sessionChip = _chrome.MakeChipPublic("flight", "DkChipSession", () => StopSession());
        }

        // ───────────────────────── mode 4: strip ─────────────────────────

        /// <summary>Builds the strip container inside the BottomBar (the same host as the other three modes) and the sheet tabs.</summary>
        public void BuildMode()
        {
            VisualElement bar = _root.Q<VisualElement>("BottomBar");
            if (bar == null) return;

            _container = new VisualElement { name = "DkTelemetry", pickingMode = PickingMode.Ignore };
            _container.AddToClassList("mode-container");
            _container.AddToClassList("mode--hidden");
            _container.AddToClassList("dk-telmode");

            _strip = new VisualElement { name = "DkTelStrip" };
            _strip.AddToClassList("dk-strip");
            _strip.AddToClassList("dk-strip--tel");

            _btnPower = StripButton("DkTelPower", "power", out _powerLabel, ToggleSession);
            _stateLabel = new Label { name = "DkTelState", pickingMode = PickingMode.Ignore };
            _stateLabel.AddToClassList("dk-strip-state");
            _btnPower.Add(_stateLabel);
            _strip.Add(_btnPower);
            _strip.Add(MakeSep());

            _btnSource = StripButton("DkTelSource", "source", out _sourceLabel, () => TogglePopover("source"));
            _btnSource.Add(Kbd("L"));
            _strip.Add(_btnSource);

            _btnAlerts = StripButton("DkTelAlerts", "alert", out _alertsLabel, () => TogglePopover("alerts"));
            _alertsCount = new Label { name = "DkTelAlertsCount", pickingMode = PickingMode.Ignore };
            _alertsCount.AddToClassList("dk-count");
            _btnAlerts.Add(_alertsCount);
            _btnAlerts.Add(Kbd("Shift+A"));
            _strip.Add(_btnAlerts);

            _btnFaults = StripButton("DkTelFaults", "fault", out _faultsLabel, () => TogglePopover("faults"));
            _strip.Add(_btnFaults);

            _btnAnalysis = StripButton("DkTelAnalysis", "analyze", out _analysisLabel, () => { if (_analysisTab) ShowFlightTab(true); else ShowAnalysisTab(); });
            _strip.Add(_btnAnalysis);

            _container.Add(_strip);
            BuildPilotStrip(); // E-6: second pill (Pilotar + the four commands); the popovers added after it open on top of it
            BuildPopovers();
            // after the Studio container: keeps the other modes' order
            bar.Add(_container);
            DesktopMotion.WatchVisible(_container);

            BuildPilotOverlay();
            if (!PCDebugFlags.Has("nofxui")) BuildFaultVisuals(); // PLAN_PC 27.3 / 28.1: the markers pinned on the parts (-pcdbg nofxui: diagnostics)
            BuildSheetTabs();
            BuildEnvOverlay(); // PLAN_PC 28.2 / 28.3: the altitude label and the course HUD pill
            if (!PCDebugFlags.Has("nofxui")) BuildFaultDock(); // PLAN_PC 28.1: the explanation card, a collapsed section of the Flight tab
            UIDetailsSheet.DesktopKeepOpenOnDeselect = () => TelemetryTabActive;
            BuildStatusBarItem();
            OnLanguage(null);
        }

        private Button StripButton(string name, string icon, out Label label, Action click)
        {
            var b = new Button(click) { name = name };
            b.AddToClassList("dk-strip-btn");
            var ic = _chrome.NewIconPublic(icon, 16);
            b.Add(ic);
            label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList("dk-strip-text");
            b.Add(label);
            b.RegisterCallback<PointerEnterEvent>(_ => ic.MarkDirtyRepaint());
            b.RegisterCallback<PointerLeaveEvent>(_ => ic.MarkDirtyRepaint());
            return b;
        }

        private static Label Kbd(string key)
        {
            var l = new Label(key) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("dk-kbd");
            return l;
        }

        private static VisualElement MakeSep()
        {
            var s = new VisualElement { pickingMode = PickingMode.Ignore };
            s.AddToClassList("dk-sep");
            return s;
        }

        private void BuildStatusBarItem()
        {
            VisualElement status = _root.Q<VisualElement>("DkStatus");
            if (status == null) return;
            _statusLink = new Label { name = "DkStatusLink", pickingMode = PickingMode.Ignore };
            _statusLink.AddToClassList("dk-status-text");
            _statusLink.style.display = DisplayStyle.None;
            // before the quality label (which sits right after the thermal legend)
            status.Insert(2, _statusLink);
        }

        // ───────────────────────── mode open / close ─────────────────────────

        public void ToggleMode()
        {
            if (ModeOpen) CloseMode(); else OpenMode();
        }

        public void OpenMode()
        {
            if (_container == null || _ui.IsHeroVisible || _ui.IsOnboardingOpen) return;
            if (!ModeOpen)
            {
                _ui.ShortcutCloseModes();
                _container.RemoveFromClassList("mode--hidden");
                ModeOpen = true;
                // the readings live in the inspector: open it on the Flight tab
                ShowFlightTab(true);
            }
        }

        public void CloseMode()
        {
            if (!ModeOpen) return;
            CloseAllPopovers();
            ModeOpen = false;
            _container.AddToClassList("mode--hidden");
        }

        /// <summary>Esc: closes the open popover first, then the mode itself. True when it consumed the key.</summary>
        public bool HandleBack()
        {
            // Esc while piloting: leave piloting first (hold is sent, the desktop shortcuts come back)
            if (_pilotOn) { SetPilot(false, "Esc"); return true; }
            if (!ModeOpen) return false;
            if (_openPop != null) { CloseAllPopovers(); return true; }
            CloseMode();
            return true;
        }

        // ───────────────────────── session ─────────────────────────

        public void ToggleSession()
        {
            if (DesktopTelemetry.IsRunning) StopSession(); else StartPreferred();
        }

        public void StopSession()
        {
            DesktopTelemetry.Stop();
            _startError = null;
        }

        public void StartPreferred()
        {
            switch (_preferred)
            {
                case TelSource.Sitl: StartSitl(_sitlUrl); break;
                case TelSource.Simulator: StartSimulator(); break;
                default: StartLocalReplay(); break;
            }
        }

        public void StartLocalReplay()
        {
            _preferred = TelSource.LocalReplay;
            PlayerPrefs.SetString(PrefSource, "replay");
            _sitlError = null;
            bool ok = DesktopTelemetry.StartLocalReplay();
            _startError = ok ? null : DesktopTelemetry.LastError;
        }

        /// <summary>The flight simulator (E-5): the kinematic model of the app. Not PX4, not a real drone; accepts the flight commands.</summary>
        public void StartSimulator()
        {
            _preferred = TelSource.Simulator;
            PlayerPrefs.SetString(PrefSource, "sim");
            _sitlError = null;
            DesktopTelemetry.StartSimulator();
            _startError = null;
        }

        /// <summary>PX4 SITL through the local bridge (WebSocketTelemetrySource). A bad URL is reported in the popover; a dead bridge shows as Connecting then Error.</summary>
        public void StartSitl(string url)
        {
            _preferred = TelSource.Sitl;
            PlayerPrefs.SetString(PrefSource, "sitl");
            url = (url ?? string.Empty).Trim();
            WebSocketTelemetrySource src;
            try
            {
                src = new WebSocketTelemetrySource(url, SourceClassification.Sitl);
            }
            catch (Exception e) when (e is ArgumentException || e is UriFormatException)
            {
                _sitlError = T("src.sitl.badurl");
                Debug.LogWarning("[DesktopTelemetryUI] SITL URL rejected: " + url + " (" + e.Message + ")");
                return;
            }

            _sitlError = null;
            _sitlUrl = url;
            PlayerPrefs.SetString(PrefSitlUrl, url);
            DesktopTelemetry.Start(src);
            _startError = null;
        }

        public TelSource ActiveSource
        {
            get
            {
                if (!DesktopTelemetry.IsRunning) return TelSource.Inspector;
                var c = DesktopTelemetry.Client;
                if (DesktopTelemetry.IsOwnReplay) return TelSource.OwnReplay; // an own recording: simulated readings, never a shadow
                if (DesktopTelemetry.IsSimulator) return TelSource.Simulator; // checked first: the simulator rides the wire as mode sitl but is NOT SITL
                if (c.Classification == SourceClassification.Sitl) return TelSource.Sitl;
                return TelSource.LocalReplay;
            }
        }

        // ───────────────────────── texts ─────────────────────────

        private void OnLanguage(string _)
        {
            if (_container == null) return;
            _powerLabel.text = T("power.switch");
            _sourceLabel.text = T("strip.source");
            _alertsLabel.text = T("strip.alerts");
            _faultsLabel.text = T("strip.faults");
            _btnPower.tooltip = T(DesktopTelemetry.IsRunning ? "power.switch.stop" : "power.switch.start");
            _btnSource.tooltip = T("src.title");
            _btnAlerts.tooltip = T("alerts.title");
            _btnFaults.tooltip = T("faults.title");
            _statusChip.tooltip = T("chip.status.tip");
            _sessionChip.tooltip = T("chip.session.tip");
            _lastBadge = null;
            _lastLang = null;
            RelabelPopovers();
            RelabelFlightTab();
            RelabelPilot();
            RelabelFaultExplain();
        }

        private void OnRunningChanged(bool running)
        {
            if (!running) OnPilotSessionEnded();
        }

        /// <summary>The honest badge of what is on now: the session's own text (HonestyLabels) or the Inspector.</summary>
        public static string BadgeText()
        {
            return DesktopTelemetry.IsRunning ? DesktopTelemetry.BadgeText(Lang) : T("src.inspector");
        }

        /// <summary>The detail sentence of what is on now (HonestyLabels), used by tooltips and the source popover.</summary>
        public static string BadgeDetail(TelSource s)
        {
            var labels = HonestyLabels.Load();
            switch (s)
            {
                case TelSource.LocalReplay: return TelemetryStrings.LocalReplayDetail(labels, Lang);
                case TelSource.Sitl: return TelemetryStrings.ForDesktop(labels.ClassificationDetail(SourceClassification.Sitl, Lang), Lang);
                case TelSource.Simulator: return labels.TrySimText("detail", Lang, out string d) ? d : string.Empty;
                case TelSource.OwnReplay: return T("own.detail");
                default: return T("src.inspector.detail");
            }
        }

        private static string LinkText(LinkState s)
        {
            switch (s)
            {
                case LinkState.Live: return T("link.live");
                case LinkState.Connecting: return T("link.connecting");
                case LinkState.Stale: return T("link.stale");
                case LinkState.Error: return T("link.offline");
                default: return T("link.off");
            }
        }

        private static string DotClass(bool running, LinkState s)
        {
            if (!running) return "dk-dot--off";
            switch (s)
            {
                case LinkState.Live: return "dk-dot--live";
                case LinkState.Connecting: return "dk-dot--connecting";
                case LinkState.Stale: return "dk-dot--stale";
                case LinkState.Error: return "dk-dot--error";
                default: return "dk-dot--off";
            }
        }

        // ───────────────────────── per frame ─────────────────────────

        public void Tick()
        {
            if (_container == null) return;

            bool running = DesktopTelemetry.IsRunning;
            LinkState link = DesktopTelemetry.LinkState;
            bool viewer = !_ui.IsHeroVisible && !_ui.IsOnboardingOpen;

            // the status chip is part of the viewer chrome only
            _statusChip.style.display = viewer ? DisplayStyle.Flex : DisplayStyle.None;
            if (!viewer && ModeOpen) CloseMode();

            // chips
            string lang = Lang;
            string badge = BadgeText();
            if (badge != _lastBadge || lang != _lastLang)
            {
                _lastBadge = badge;
                _lastLang = lang;
                _statusText.text = badge;
                _statusChip.tooltip = badge + " · " + T("chip.status.tip");
                _btnPower.tooltip = T(running ? "power.switch.stop" : "power.switch.start");
                _sourceLabel.text = SourceButtonText();
            }

            if (running != _wasRunning || link != _lastLink)
            {
                bool started = running && !_wasRunning;
                _wasRunning = running;
                _lastLink = link;
                foreach (string c in new[] { "dk-dot--off", "dk-dot--live", "dk-dot--connecting", "dk-dot--stale", "dk-dot--error" }) _statusDot.RemoveFromClassList(c);
                _statusDot.AddToClassList(DotClass(running, link));
                _statusChip.EnableInClassList("dk-stchip--on", running);
                DesktopMotion.Flash(_statusChip, "dk-flash", 200);
                _btnPower.EnableInClassList("dk-strip-btn--on", running);
                _btnPower.tooltip = T(running ? "power.switch.stop" : "power.switch.start");
                _sourceLabel.text = SourceButtonText();
                UpdatePulse(running && link == LinkState.Connecting);
                if (started && ModeOpen && !_analysisTab) ShowFlightTab(true); // the Analysis tab stays when a session starts under it
            }

            UIDetailsSheetFlightSync();
            TickAnalysisRecorder();
            UpdateChipVisibility(_sessionChip, running);
            _stateLabel.text = running ? LinkText(link) : T("link.off");
            _stateLabel.EnableInClassList("dk-strip-state--live", running && link == LinkState.Live);
            _stateLabel.EnableInClassList("dk-strip-state--warn", running && (link == LinkState.Connecting || link == LinkState.Stale));
            _stateLabel.EnableInClassList("dk-strip-state--crit", running && link == LinkState.Error);

            // alerts button: count + worst severity (cheap: the client already derived them)
            var client = DesktopTelemetry.Client;
            int count = running && client != null ? client.AlertSummary.count : 0;
            AlertSeverity? worst = running && client != null ? client.AlertSummary.worst : null;
            string ct = count > 0 ? count.ToString() : string.Empty;
            if (_alertsCount.text != ct) _alertsCount.text = ct;
            _alertsCount.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _alertsCount.EnableInClassList("dk-count--crit", worst == AlertSeverity.Critical);
            _alertsCount.EnableInClassList("dk-count--warn", worst == AlertSeverity.Warning);
            _btnAlerts.EnableInClassList("dk-strip-btn--alert", count > 0);

            _btnSource.EnableInClassList("dk-strip-btn--on", _openPop == "source");
            _btnAlerts.EnableInClassList("dk-strip-btn--on", _openPop == "alerts");
            _btnFaults.EnableInClassList("dk-strip-btn--on", _openPop == "faults");

            // status bar: rate and staleness (web: Hz received, "datos atrasados")
            if (_statusLink != null)
            {
                bool show = running && viewer && client != null;
                _statusLink.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (show && Time.unscaledTime >= _nextSlowTick)
                {
                    string txt = T("st.link") + ": " + LinkText(link) + (client.RxHz > 0.5 ? " · " + client.RxHz.ToString("0") + " Hz" : string.Empty);
                    if (_statusLink.text != txt) _statusLink.text = txt;
                }
            }

            if (Time.unscaledTime >= _nextSlowTick) _nextSlowTick = Time.unscaledTime + 0.1f;
            TickFaultVisuals();
            TickEnv();
            TickPopovers();
            TickFlightTab();
            TickAnalysisTab();
            TickPilot();
            ApplyMenuTable(); // PLAN_PC 38: hide what the source does not have (TelemetryMenu), do not dim it
        }

        private static void UpdateChipVisibility(Button chip, bool active)
        {
            if (chip == null) return;
            bool live = chip.ClassListContains("dk-chip--live");
            if (active && !live)
            {
                chip.AddToClassList("dk-chip--live");
                chip.RemoveFromClassList("dk-chip--out");
                chip.style.display = DisplayStyle.Flex;
                DesktopMotion.Enter(chip, 0, "dk-enter--chip");
            }
            else if (!active && live)
            {
                chip.RemoveFromClassList("dk-chip--live");
                chip.AddToClassList("dk-chip--out");
                chip.schedule.Execute(() => { if (!chip.ClassListContains("dk-chip--live")) chip.style.display = DisplayStyle.None; }).StartingIn(DesktopMotion.Reduce ? 1 : 200);
            }
        }

        private void UpdatePulse(bool on)
        {
            if (DesktopMotion.Reduce) on = false;
            if (on)
            {
                if (_pulse == null) _pulse = _statusDot.schedule.Execute(() => _statusDot.ToggleInClassList("dk-dot--dim")).Every(550);
                else _pulse.Resume();
            }
            else
            {
                _pulse?.Pause();
                _statusDot.RemoveFromClassList("dk-dot--dim");
            }
        }

        private string SourceButtonText() => DesktopTelemetry.IsRunning ? T("strip.source") + ": " + ShortSourceName() : T("strip.source");

        private string ShortSourceName()
        {
            switch (ActiveSource)
            {
                case TelSource.LocalReplay: return T("src.replay");
                case TelSource.Sitl: return T("src.sitl");
                case TelSource.Simulator: return HonestyLabels.Load().TrySimText("title", Lang, out string s) ? s : string.Empty;
                case TelSource.OwnReplay: return DesktopTelemetry.BadgeText(Lang) ?? string.Empty;
                default: return T("src.inspector");
            }
        }
    }
}
#endif
