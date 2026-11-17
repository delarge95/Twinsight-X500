#if UNITY_STANDALONE
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.Telemetry;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Lote 1b, tool 3 (PC only): the pre-flight checklist. A strip button «Comprobar» with a dot in the colour of the verdict opens a popover with one row per
    /// check (glyph, name, state, value, origin) and the three boxes the user ticks (not in a replay, which evaluates the recorded flight at this instant).
    /// The checks are the pure <see cref="Preflight"/> (golden-tested against the web). It is informative: nothing here blocks anything; with a failing check
    /// «Despegar» asks once and «Despegar igualmente» goes on. The boxes live only for the run (nothing is stored).
    /// </summary>
    internal sealed partial class DesktopTelemetryUI
    {
        private const float PfPeriod = 0.2f;
        private static readonly string[] PfRowIds = { "link", "bat.rem", "bat.cell", "temp", "vib", "armed", "fence", "faults", "gps" };

        private sealed class PfRow
        {
            public VisualElement Root;
            public Label Glyph, Name, Status, Value, Sub, Tag;
            public string Id;
        }

        private Button _btnPf;
        private Label _pfBtnLabel;
        private VisualElement _pfBtnDot;
        private VisualElement _popPf, _pfSummary, _pfSummaryDot, _pfManualBox;
        private Label _pfTitle, _pfReplayNote, _pfVerdict, _pfCounts, _pfManualTitle, _pfInfo;
        private readonly PfRow[] _pfRows = new PfRow[PfRowIds.Length];
        private readonly Button[] _pfManual = new Button[3];
        private readonly Label[] _pfManualGlyph = new Label[3], _pfManualText = new Label[3], _pfManualTag = new Label[3];
        private readonly Dictionary<string, bool> _pfTicked = new Dictionary<string, bool>();
        private readonly PreflightInput _pfInput = new PreflightInput();
        private PreflightResult _pfResult;
        private PreflightSource _pfSource;
        private float _pfNext;

        // take-off warning
        private VisualElement _pfAsk;
        private Label _pfAskTitle, _pfAskBody;
        private Button _pfAskGo, _pfAskCancel;
        private List<string> _pfAskIds;

        // ── read by the verification harness ──
        public Button PreflightButton => _btnPf;
        public VisualElement PreflightPopover => _popPf;
        public PreflightResult PreflightNow => _pfResult;
        public string PreflightVerdictText => _pfVerdict?.text;
        public string PreflightCountsText => _pfCounts?.text;
        public string PreflightReplayNote => _pfReplayNote != null && _pfReplayNote.style.display != DisplayStyle.None ? _pfReplayNote.text : null;
        public bool PreflightManualShown => _pfManualBox != null && _pfManualBox.style.display != DisplayStyle.None;
        public Button PreflightManualButton(int i) => _pfManual[i];
        public bool PreflightAskShown => _pfAsk != null && _pfAsk.style.display != DisplayStyle.None;
        public string PreflightAskBody => _pfAskBody?.text;
        public Button PreflightAskGo => _pfAskGo;
        public Button PreflightAskCancel => _pfAskCancel;
        /// <summary>False: «Despegar» never asks (the harnesses that script a flight with a fault on turn it off; the user cannot).</summary>
        public bool PreflightAsks = true;
        public string PreflightRowText(string id)
        {
            foreach (PfRow r in _pfRows) if (r != null && r.Id == id) return r.Glyph.text + " " + r.Name.text + " | " + r.Status.text + " | " + r.Value.text + " | " + r.Sub.text + " | " + r.Tag.text;
            return null;
        }

        partial void BuildPreflightTool(VisualElement strip)
        {
            _btnPf = StripButton("DkTelPreflight", "inspect", out _pfBtnLabel, () => TogglePopover("preflight"));
            _pfBtnDot = new VisualElement { name = "DkPfDot", pickingMode = PickingMode.Ignore };
            _pfBtnDot.AddToClassList("dk-dot");
            _pfBtnDot.AddToClassList("dk-pf-btndot");
            _btnPf.Add(_pfBtnDot);
            strip.Add(_btnPf);

            // the take-off warning lives on the viewer, above the strips
            _pfAsk = new VisualElement { name = "DkPfAsk", pickingMode = PickingMode.Position };
            _pfAsk.AddToClassList("dk-pf-ask");
            _pfAsk.style.display = DisplayStyle.None;
            _pfAskTitle = Text("dk-pf-ask-title", _pfAsk);
            _pfAskBody = Text("dk-note", _pfAsk);
            var row = new VisualElement();
            row.AddToClassList("dk-fault-foot");
            _pfAskGo = ToolButton("DkPfAskGo", "dk-b--danger", () => PreflightAnswer(true));
            _pfAskGo.AddToClassList("dk-fault-footbtn");
            _pfAskCancel = ToolButton("DkPfAskCancel", "dk-b--ghost", () => PreflightAnswer(false));
            _pfAskCancel.AddToClassList("dk-fault-footbtn");
            row.Add(_pfAskGo);
            row.Add(_pfAskCancel);
            _pfAsk.Add(row);
            _root.Add(_pfAsk);
        }

        partial void BuildPreflightPopover()
        {
            _popPf = NewPopover("DkPopPreflight", "dk-pop--w360", out _pfTitle);
            _pfReplayNote = Text("dk-note", _popPf);
            _pfReplayNote.AddToClassList("dk-note--muted");

            _pfSummary = new VisualElement { name = "DkPfSummary", pickingMode = PickingMode.Ignore };
            _pfSummary.AddToClassList("dk-pf-summary");
            _pfSummaryDot = new VisualElement { pickingMode = PickingMode.Ignore };
            _pfSummaryDot.AddToClassList("dk-dot");
            _pfSummary.Add(_pfSummaryDot);
            _pfVerdict = Text("dk-pf-verdict", _pfSummary);
            _pfCounts = Text("dk-pf-counts", _pfSummary);
            _popPf.Add(_pfSummary);

            for (int i = 0; i < PfRowIds.Length; i++)
            {
                var r = new PfRow { Id = PfRowIds[i] };
                r.Root = new VisualElement { name = "DkPfRow_" + PfRowIds[i].Replace('.', '_'), pickingMode = PickingMode.Ignore };
                r.Root.AddToClassList("dk-pf-row");
                r.Glyph = Text("dk-pf-glyph", r.Root);
                var text = new VisualElement { pickingMode = PickingMode.Ignore };
                text.AddToClassList("dk-pf-text");
                r.Name = Text("dk-pf-name", text);
                r.Status = Text("dk-pf-status", text);
                r.Root.Add(text);
                var val = new VisualElement { pickingMode = PickingMode.Ignore };
                val.AddToClassList("dk-pf-val");
                r.Value = Text("dk-pf-value", val);
                r.Sub = Text("dk-pf-sub", val);
                r.Root.Add(val);
                r.Tag = Text("dk-tag", r.Root);
                r.Tag.AddToClassList("dk-pf-tag");
                _popPf.Add(r.Root);
                _pfRows[i] = r;
            }

            _pfManualBox = new VisualElement { name = "DkPfManual" };
            _pfManualBox.AddToClassList("dk-pf-manual");
            _pfManualTitle = Text("dk-field-label", _pfManualBox);
            for (int i = 0; i < 3; i++)
            {
                string id = Preflight.ManualIds[i];
                var b = new Button(() => PreflightToggle(id)) { name = "DkPfManual_" + id.Substring(7) };
                b.AddToClassList("dk-pf-box");
                _pfManualGlyph[i] = Text("dk-pf-check", b);
                _pfManualText[i] = Text("dk-pf-boxtext", b);
                _pfManualTag[i] = Text("dk-tag", b);
                _pfManualBox.Add(b);
                _pfManual[i] = b;
            }
            _popPf.Add(_pfManualBox);
            _pfInfo = Text("dk-note", _popPf);
            _pfInfo.AddToClassList("dk-note--muted");
            RegisterToolPopover("preflight", _popPf);
        }

        partial void DisposePreflight()
        {
            _popPf?.RemoveFromHierarchy();
            _pfAsk?.RemoveFromHierarchy();
        }

        partial void RelabelPreflight()
        {
            if (_btnPf == null || _popPf == null) return;
            _pfBtnLabel.text = T("tools.preflight");
            _pfTitle.text = T("pf.title");
            _pfManualTitle.text = T("pf.manual");
            _pfInfo.text = T("pf.info");
            for (int i = 0; i < 3; i++)
            {
                _pfManualText[i].text = T("pf.item." + Preflight.ManualIds[i]);
                _pfManualTag[i].text = T("pf.origin.user");
            }
            foreach (PfRow r in _pfRows) r.Name.text = T("pf.item." + r.Id);
            _pfAskTitle.text = T("pf.takeoff.title");
            _pfAskGo.text = T("pf.takeoff.go");
            _pfAskCancel.text = T("pf.takeoff.cancel");
            if (_pfAskIds != null) FillPreflightAsk();
            _pfNext = 0f;
        }

        public void PreflightToggle(string id)
        {
            _pfTicked[id] = !(_pfTicked.TryGetValue(id, out bool on) && on);
            _pfNext = 0f;
        }

        /// <summary>Evaluates the checklist now (also used by the harness).</summary>
        public PreflightResult EvaluatePreflight()
        {
            TelemetryClient client = DesktopTelemetry.Client;
            if (client == null) { _pfResult = null; return null; }
            bool sim = DesktopTelemetry.IsSimulator;
            _pfSource = sim ? PreflightSource.Sim : client.Classification == SourceClassification.Replay ? PreflightSource.Replay : PreflightSource.Sitl;
            PreflightInput inp = _pfInput;
            inp.hello = client.Hello;
            inp.frame = client.Displayed;
            LinkState ls = client.State;
            inp.link = ls == LinkState.Live || ls == LinkState.Stale ? PreflightLink.Open : ls == LinkState.Connecting ? PreflightLink.Connecting : PreflightLink.Closed;
            inp.rxHz = client.RxHz;
            inp.starvedS = client.StarvedMs / 1000.0;
            int crit = 0;
            IReadOnlyList<Alert> alerts = client.Alerts;
            for (int i = 0; i < alerts.Count; i++) if (alerts[i].severity == AlertSeverity.Critical) crit++;
            inp.alerts = alerts.Count;
            inp.criticalAlerts = crit;
            inp.faults = client.Faults.Count;
            inp.fence = sim ? FenceNow() : null;
            inp.manual = _pfTicked;
            inp.source = _pfSource;
            inp.own = DesktopTelemetry.IsOwnReplay;
            _pfResult = Preflight.Evaluate(inp);
            return _pfResult;
        }

        partial void TickPreflight(bool viewer)
        {
            if (_btnPf == null) return;
            bool running = DesktopTelemetry.IsRunning;
            _btnPf.SetEnabled(running);
            _btnPf.EnableInClassList("dk-strip-btn--on", _openPop == "preflight");
            string tip = running ? T("pf.title") : T("tools.needTelemetry");
            if (_btnPf.tooltip != tip) _btnPf.tooltip = tip;
            if (!running)
            {
                _pfResult = null;
                _pfBtnDot.style.display = DisplayStyle.None;
                if (PreflightAskShown) PreflightAnswer(false);
                return;
            }
            if (Time.unscaledTime < _pfNext) return;
            _pfNext = Time.unscaledTime + PfPeriod;
            PreflightResult r = EvaluatePreflight();
            _pfBtnDot.style.display = DisplayStyle.Flex;
            SetVerdictDot(_pfBtnDot, r.summary.verdict);
            if (PreflightAskShown && !DesktopTelemetry.AcceptsCommands) PreflightAnswer(false);
        }

        private static void SetVerdictDot(VisualElement dot, PreflightVerdict v)
        {
            dot.EnableInClassList("dk-dot--live", v == PreflightVerdict.Ready);
            dot.EnableInClassList("dk-dot--warn", v == PreflightVerdict.Warn);
            dot.EnableInClassList("dk-dot--crit", v == PreflightVerdict.Fail);
        }

        partial void TickPreflightPopover()
        {
            PreflightResult r = _pfResult ?? EvaluatePreflight();
            if (r == null) return;
            bool replay = _pfSource == PreflightSource.Replay;
            _pfReplayNote.style.display = replay ? DisplayStyle.Flex : DisplayStyle.None;
            if (replay) SetText(_pfReplayNote, T("pf.replayNote"));
            SetVerdictDot(_pfSummaryDot, r.summary.verdict);
            SetText(_pfVerdict, T("pf." + Preflight.Name(r.summary.verdict)));
            _pfSummary.EnableInClassList("dk-pf-summary--warn", r.summary.verdict == PreflightVerdict.Warn);
            _pfSummary.EnableInClassList("dk-pf-summary--fail", r.summary.verdict == PreflightVerdict.Fail);
            SetText(_pfCounts, TelemetryStrings.F("pf.counts", Lang, "pass", r.summary.pass, "warn", r.summary.warn, "fail", r.summary.fail));

            foreach (PfRow row in _pfRows)
            {
                PreflightItem it = r.Find(row.Id);
                if (it == null) { row.Root.style.display = DisplayStyle.None; continue; }
                row.Root.style.display = DisplayStyle.Flex;
                SetText(row.Glyph, it.status == PreflightStatus.Pass ? "✓" : it.status == PreflightStatus.Warn ? "!" : it.status == PreflightStatus.Fail ? "×" : "–");
                row.Root.EnableInClassList("dk-pf-row--pass", it.status == PreflightStatus.Pass);
                row.Root.EnableInClassList("dk-pf-row--warn", it.status == PreflightStatus.Warn);
                row.Root.EnableInClassList("dk-pf-row--fail", it.status == PreflightStatus.Fail);
                SetText(row.Status, T("pf.status." + Preflight.Name(it.status)));
                DescribePreflight(it, out string value, out string sub);
                SetText(row.Value, value);
                SetText(row.Sub, sub ?? string.Empty);
                row.Sub.style.display = string.IsNullOrEmpty(sub) ? DisplayStyle.None : DisplayStyle.Flex;
                SetText(row.Tag, PreflightOriginText(it.origin));
                row.Tag.EnableInClassList("dk-tag--measured", it.origin == PreflightOrigin.Measured);
                row.Tag.EnableInClassList("dk-tag--estimated", it.origin == PreflightOrigin.Estimated);
                row.Tag.EnableInClassList("dk-tag--simulated", it.origin == PreflightOrigin.Simulated);
            }

            _pfManualBox.style.display = replay ? DisplayStyle.None : DisplayStyle.Flex;
            for (int i = 0; i < 3; i++)
            {
                bool on = _pfTicked.TryGetValue(Preflight.ManualIds[i], out bool t) && t;
                SetText(_pfManualGlyph[i], on ? "✓" : string.Empty);
                _pfManual[i].EnableInClassList("dk-pf-box--on", on);
            }
        }

        private string PreflightOriginText(PreflightOrigin o)
        {
            var labels = HonestyLabels.Load();
            switch (o)
            {
                case PreflightOrigin.Measured: return FlightOrigins.Text(ReadingOrigin.Measured, Lang, labels);
                case PreflightOrigin.Estimated: return FlightOrigins.Text(ReadingOrigin.Estimated, Lang, labels);
                case PreflightOrigin.Simulated: return FlightOrigins.Text(ReadingOrigin.Simulated, Lang, labels);
                default: return T("pf.origin." + Preflight.Name(o));
            }
        }

        /// <summary>The text of a row: the value and the line under it (same rules as the web's describe()).</summary>
        private void DescribePreflight(PreflightItem i, out string value, out string sub)
        {
            const string dash = "—";
            CultureInfo inv = CultureInfo.InvariantCulture;
            double? v = i.value;
            sub = null;
            switch (i.id)
            {
                case "link":
                    value = v.HasValue && v.Value > 0 ? v.Value.ToString("0", inv) + " Hz" : dash;
                    sub = T("pf.link." + (i.detail ?? "open"));
                    break;
                case "bat.rem":
                    value = v.HasValue ? v.Value.ToString("0", inv) + " %" : dash;
                    break;
                case "bat.cell":
                    value = v.HasValue ? v.Value.ToString("0.00", inv) + " V" : dash;
                    sub = T("unit.vPerCell");
                    break;
                case "temp":
                    value = v.HasValue ? v.Value.ToString("0", inv) + " °C" : dash;
                    if (v.HasValue) sub = i.detail == "battery" ? T("pf.temp.battery") : TelemetryStrings.F("pf.temp.motor", Lang, "n", (i.detail ?? "motor1").Substring(5));
                    break;
                case "vib":
                    value = v.HasValue ? v.Value.ToString("0.0", inv) + " m/s²" : dash;
                    break;
                case "armed":
                    value = i.detail != null ? T("pf.armed." + i.detail) : dash;
                    break;
                case "fence":
                    if (i.status == PreflightStatus.Na) { value = dash; sub = T(_pfSource == PreflightSource.Sim ? "pf.fence.noFrame" : "pf.fence.simOnly"); }
                    else value = !v.HasValue ? dash : v.Value < 0 ? TelemetryStrings.F("pf.fence.outside", Lang, "m", Math.Abs(v.Value).ToString("0", inv)) : TelemetryStrings.F("pf.fence.margin", Lang, "m", v.Value.ToString("0", inv));
                    break;
                case "faults":
                    value = i.detail != null ? T("pf.faults." + i.detail) : dash;
                    if (v.HasValue && v.Value != 0) sub = TelemetryStrings.F("pf.faults.count", Lang, "n", v.Value.ToString("0", inv));
                    break;
                case "gps":
                    value = dash;
                    sub = T("pf.gps.na");
                    break;
                default:
                    value = dash;
                    break;
            }
        }

        // ───────────────────────── «Despegar» with a failing check ─────────────────────────

        /// <summary>Called by SendPilotCommand before a take-off: with a failing check it shows the warning and holds the command (true).</summary>
        private bool PreflightHoldsTakeoff()
        {
            if (!PreflightAsks || _pfAsk == null) return false;
            PreflightResult r = EvaluatePreflight();
            if (!Preflight.TakeoffAsks(r)) return false;
            _pfAskIds = Preflight.FailingIds(r);
            FillPreflightAsk();
            _pfAsk.style.display = DisplayStyle.Flex;
            _pfAsk.BringToFront();
            Debug.Log("[Preflight] take-off asks: " + string.Join(", ", _pfAskIds));
            return true;
        }

        private void FillPreflightAsk()
        {
            var names = new List<string>(_pfAskIds.Count);
            foreach (string id in _pfAskIds) names.Add(T("pf.item." + id));
            _pfAskBody.text = TelemetryStrings.F("pf.takeoff.body", Lang, "list", string.Join(", ", names));
        }

        /// <summary>«Despegar igualmente» (true: the take-off is sent) or «Cancelar» (false: things stay as they were).</summary>
        public void PreflightAnswer(bool go)
        {
            if (_pfAsk == null) return;
            _pfAsk.style.display = DisplayStyle.None;
            _pfAskIds = null;
            if (go) SendPilotCommand(PilotCommand.Takeoff, "preflight");
        }
    }
}
#endif
