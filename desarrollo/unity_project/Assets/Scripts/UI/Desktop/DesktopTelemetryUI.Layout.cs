#if UNITY_STANDALONE
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Telemetry;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PLAN_PC 42: the rows on top of the Telemetry tab.
    ///  - Row 1: the four source pills (Repetición local, Simulador, PX4 SITL, Mis sesiones) with the telemetry off; with it on, only the active one (blue, with its
    ///    link state; pressing it again turns the telemetry off) and a gear that opens the «Fuente» window (URL of the bridge, origin badges). Then the folding
    ///    sections the source has (Mando, Escena, Herramientas: one open at a time) and the small pills Alertas · Fallas · Análisis where they apply.
    ///  - Row 2: the open section.
    ///  - Piloting: compact. Row 1 keeps the source, Alertas (a dynamic warning) and «Más»; row 2 is always «Mando» (Mantener H and Volver R in sight).
    ///  - Every panel opens as a side window (<see cref="DesktopWindow"/>): beside the tree or the inspector, movable, remembered.
    /// What exists is decided by the pure table (<see cref="TelemetryMenu.ViewFor"/>); this file only draws it.
    /// </summary>
    internal sealed partial class DesktopTelemetryUI
    {
        private readonly Dictionary<SourcePill, Button> _srcPills = new Dictionary<SourcePill, Button>();
        private readonly Dictionary<SourcePill, Label> _srcPillLabels = new Dictionary<SourcePill, Label>();
        private readonly Dictionary<SourcePill, Label> _srcPillStates = new Dictionary<SourcePill, Label>();
        private readonly Dictionary<TSection, Button> _secBtns = new Dictionary<TSection, Button>();
        private readonly Dictionary<TSection, Label> _secLabels = new Dictionary<TSection, Label>();
        private readonly Dictionary<string, DesktopWindowDrag> _wins = new Dictionary<string, DesktopWindowDrag>();
        private readonly Dictionary<SourcePill, string> _srcPillTips = new Dictionary<SourcePill, string>();
        private Button _srcGear, _moreBtn;
        private Label _moreLabel;
        private VisualElement _winLayer, _sepData, _sepSrc;
        private TSection? _openSection;
        private bool _moreOpen;
        private MenuSurface? _lastSurface;
        private StripView _view;

        // ── read by the verification harness ──
        public Button SourcePillButton(SourcePill p) => _srcPills.TryGetValue(p, out Button b) ? b : null;
        public Button SectionButton(TSection s) => _secBtns.TryGetValue(s, out Button b) ? b : null;
        public Button MoreButton => _moreBtn;
        public Button SourceGear => _srcGear;
        public TSection? OpenSection => _openSection;
        public bool MoreOpen => _moreOpen;
        public StripView CurrentView => _view;
        /// <summary>Verification and keyboard: opens a section (null = none) / the «Más» of the compact mode, like a click on its pill.</summary>
        public void SetOpenSection(TSection? s) { _openSection = s; _menuKey = -1; ApplyMenuTable(); }
        public void SetMoreOpen(bool on) { _moreOpen = on; _menuKey = -1; ApplyMenuTable(); }
        public DesktopWindowDrag WindowOf(string key) => _wins.TryGetValue(key, out DesktopWindowDrag w) ? w : null;
        public VisualElement WindowLayer => _winLayer;
        public VisualElement OpenPopoverElement => _openPop == null ? null : _openPop == "source" ? _popSource : _openPop == "alerts" ? _popAlerts : _openPop == "faults" ? _popFaults : _toolPops.TryGetValue(_openPop, out VisualElement p) ? p : null;

        // ───────────────────────── row 1 ─────────────────────────

        private void BuildSourcePills()
        {
            AddPill(SourcePill.Replay, "DkSrcPillReplay", "replay");
            AddPill(SourcePill.Sim, "DkSrcPillSim", "flight");
            AddPill(SourcePill.Sitl, "DkSrcPillSitl", "sitl");
            AddPill(SourcePill.Own, "DkSrcPillOwn", "pins");
            _srcGear = _chrome.IconButtonPublic("gear", "DkSrcGear", () => TogglePopover("source"), 14);
            _srcGear.AddToClassList("dk-strip-gear");
            _strip.Add(_srcGear);
            _sepSrc = MakeSep();
            _strip.Add(_sepSrc);
            AddSection(TSection.Cmd, "DkSecCmd", "gamepad");
            AddSection(TSection.Scene, "DkSecScene", "env_studio");
            AddSection(TSection.Tools, "DkSecTools", "pins");
            _moreBtn = StripButton("DkTelMore", "chevron", out _moreLabel, () => { _moreOpen = !_moreOpen; _menuKey = -1; });
            _strip.Add(_moreBtn);
            _sepData = MakeSep();
            _strip.Add(_sepData);
        }

        private void AddPill(SourcePill p, string name, string icon)
        {
            Button b = StripButton(name, icon, out Label l, () => PressSourcePill(p));
            b.AddToClassList("dk-srcpill");
            var st = new Label { pickingMode = PickingMode.Ignore };
            st.AddToClassList("dk-strip-state");
            b.Add(st);
            _srcPills[p] = b;
            _srcPillLabels[p] = l;
            _srcPillStates[p] = st;
            _strip.Add(b);
        }

        private void AddSection(TSection s, string name, string icon)
        {
            Button b = StripButton(name, icon, out Label l, () => { _openSection = TelemetryMenu.ToggleSection(_openSection, s); _menuKey = -1; });
            b.AddToClassList("dk-secbtn");
            var chev = _chrome.NewIconPublic("chevron", 12);
            chev.AddToClassList("dk-sec-chev");
            b.Add(chev);
            _secBtns[s] = b;
            _secLabels[s] = l;
            _strip.Add(b);
        }

        /// <summary>A source pill: the active one turns the telemetry off; another one starts its source (SITL with the remembered URL, «Mis sesiones» opens the list).</summary>
        public void PressSourcePill(SourcePill p)
        {
            SourcePill? next = TelemetryMenu.PressPill(CurrentSurface, p);
            if (next == null) { StopSession(); CloseAllPopovers(); return; }
            switch (next.Value)
            {
                case SourcePill.Replay: StartLocalReplay(); break;
                case SourcePill.Sim: StartSimulator(); break;
                case SourcePill.Sitl:
                    _sitlBlockOpen = true;
                    StartSitl(_sitlUrl);
                    TogglePopoverOpen("source"); // the URL, the link state and any error stay in sight
                    break;
                case SourcePill.Own:
                    TogglePopoverOpen("record"); // the list of own sessions: a click there starts one
                    break;
            }
        }

        private void TogglePopoverOpen(string which) { if (_openPop != which) TogglePopover(which); }

        // ───────────────────────── gear of the switches (Geocerca, Viento, Batería) ─────────────────────────

        private Button AddGear(VisualElement strip, string name, string pop)
        {
            Button g = _chrome.IconButtonPublic("gear", name, () => TogglePopover(pop), 14);
            g.AddToClassList("dk-strip-gear");
            strip.Add(g);
            return g;
        }

        private static void ShowGear(Button gear, Button pill, bool on, bool open)
        {
            if (gear == null) return;
            bool shown = on && pill != null && pill.style.display != DisplayStyle.None;
            gear.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            gear.EnableInClassList("dk-ibtn--on", open);
        }

        // ───────────────────────── side windows ─────────────────────────

        private void BuildWindows()
        {
            _winLayer = new VisualElement { name = "DkTelWindows", pickingMode = PickingMode.Ignore };
            _winLayer.AddToClassList("dk-winlayer");
            _root.Add(_winLayer);
            AddWindow("source", _popSource, false);
            AddWindow("alerts", _popAlerts, true);
            AddWindow("faults", _popFaults, true);
            foreach (KeyValuePair<string, VisualElement> kv in _toolPops) AddWindow(kv.Key, kv.Value, false);
        }

        private void AddWindow(string id, VisualElement pop, bool right)
        {
            if (pop == null) return;
            _winLayer.Add(pop);
            var w = new DesktopWindowDrag(pop, _winLayer, id, right, () => _chrome.LeftOccupied, () => _chrome.RightOccupied, WindowTop);
            _wins[id] = w;
            pop.RegisterCallback<GeometryChangedEvent>(e =>
            {
                if (pop.style.display == DisplayStyle.None) return;
                if (Mathf.Abs(e.oldRect.width - e.newRect.width) > 0.5f || Mathf.Abs(e.oldRect.height - e.newRect.height) > 0.5f) w.AutoPlace();
            });
        }

        /// <summary>Below the rows of the tab (they are centred on top): a side window never covers them.</summary>
        private float WindowTop()
        {
            float y = 52f;
            if (_container != null)
                foreach (VisualElement row in _container.Children())
                    if (row.resolvedStyle.display != DisplayStyle.None && !float.IsNaN(row.worldBound.yMax) && row.worldBound.height > 1f) y = Mathf.Max(y, row.worldBound.yMax + DesktopWindow.Gap);
            return y;
        }

        private void PlaceWindow(string which)
        {
            if (which != null && _wins.TryGetValue(which, out DesktopWindowDrag w)) w.Place();
        }

        // ───────────────────────── texts ─────────────────────────

        private void RelabelLayout()
        {
            if (_srcPills.Count == 0) return;
            _srcPillLabels[SourcePill.Replay].text = T("src.replay");
            _srcPillLabels[SourcePill.Sim].text = T("src.sim");
            _srcPillLabels[SourcePill.Sitl].text = T("src.sitl");
            _srcPillLabels[SourcePill.Own].text = T("sessions.title");
            var labels = HonestyLabels.Load();
            string lang = Lang;
            // the origin badge of each source rides on its pill (the long detail stays behind the ⓘ of the «Fuente» window)
            _srcPillTips[SourcePill.Replay] = labels.TryLocalReplayText(lang, out string local) ? local : T("src.replay");
            _srcPillTips[SourcePill.Sim] = labels.TrySimText("title", lang, out string sim) ? sim : T("src.sim");
            _srcPillTips[SourcePill.Sitl] = labels.ClassificationTitle(SourceClassification.Sitl, lang);
            _srcPillTips[SourcePill.Own] = T("own.detail");
            foreach (KeyValuePair<SourcePill, string> kv in _srcPillTips) _srcPills[kv.Key].tooltip = kv.Value;
            _srcGear.tooltip = T("src.title");
            _secLabels[TSection.Cmd].text = T("ux.sec.cmd");
            _secLabels[TSection.Scene].text = T("ux.sec.scene");
            _secLabels[TSection.Tools].text = T("ux.sec.tools");
            foreach (Button b in _secBtns.Values) b.tooltip = T("ux.sec.tip");
            _moreLabel.text = T("ux.more");
            _moreBtn.tooltip = T("ux.more.tip");
            foreach (DesktopWindowDrag w in _wins.Values)
            {
                VisualElement head = w.Window.Q<VisualElement>(className: "dk-win-head");
                if (head != null) head.tooltip = T("ux.win.tip");
            }
            _menuKey = -1;
        }

        /// <summary>Per frame: the state of the active pill and its tooltip «press again to turn off».</summary>
        private void TickSourcePills(bool running, LinkState link)
        {
            if (_srcPills.Count == 0) return;
            SourcePill? on = TelemetryMenu.PillOf(CurrentSurface);
            foreach (KeyValuePair<SourcePill, Button> kv in _srcPills)
            {
                bool active = on == kv.Key;
                kv.Value.EnableInClassList("dk-strip-btn--on", active || (kv.Key == SourcePill.Own && !running && _openPop == "record"));
                string tip = _srcPillTips.TryGetValue(kv.Key, out string bt) ? (active ? bt + " · " + T("ux.src.on.tip") : bt) : string.Empty;
                if (kv.Value.tooltip != tip) kv.Value.tooltip = tip;
                Label st = _srcPillStates[kv.Key];
                string txt = active ? LinkText(link) : string.Empty;
                if (st.text != txt) st.text = txt;
                st.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
                st.EnableInClassList("dk-strip-state--live", active && link == LinkState.Live);
                st.EnableInClassList("dk-strip-state--warn", active && (link == LinkState.Connecting || link == LinkState.Stale));
                st.EnableInClassList("dk-strip-state--crit", active && link == LinkState.Error);
            }
            _srcGear.EnableInClassList("dk-ibtn--on", _openPop == "source");
        }
    }
}
#endif
