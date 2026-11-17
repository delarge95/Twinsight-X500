#if UNITY_STANDALONE
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Telemetry;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// The "Analisis" tab of the inspector (E-4, PLAN_PC 23): the last 60 s of altitude, horizontal / vertical speed, battery voltage and
    /// charge, motor command, the 4 rotor speeds and vibration as compact line charts on a shared time axis, each with its origin tag
    /// (the same words and rules as the "Vuelo" tab), its current value, its min / max of the window and its unit, plus a strip of alert /
    /// fault markers. It lives in the inspector (third tab next to "Pieza" | "Vuelo") because the docked inspector is already outside the
    /// model's canvas (the camera re-frames for it), so the charts cost no extra canvas.
    /// Data: <see cref="AnalysisRecorder"/> samples the displayed frame of ANY session (replay now; simulator / SITL later) at 4 Hz, even
    /// while the tab is closed, so the minute is there when it opens. Drawing: <see cref="AnalysisPlot"/> (Painter2D), repainted only when a
    /// sample arrived or the cursor moved, and never faster than 10 Hz. Nothing is allocated per frame in the drawing path.
    /// </summary>
    internal sealed partial class DesktopTelemetryUI
    {
        private const float AnalysisPeriod = 0.1f;

        private sealed class ChartCell
        {
            public ChartSpec Spec;
            public VisualElement Root;
            public Label Title, Tag, Value, Range;
            public Label[] LineValues;
            public AnalysisPlot Plot;
            public VisualElement Cursor;
            public Series[] Lines;
            public string ShownValue, ShownRange;
        }

        private readonly AnalysisRecorder _rec = new AnalysisRecorder();
        private readonly List<ChartCell> _anCells = new List<ChartCell>();
        private readonly List<KeyValuePair<string, double>> _anSeenFaults = new List<KeyValuePair<string, double>>();
        private TelemetryClient _anClient;

        private Button _tabAnalysis;
        private VisualElement _anPanel;
        private ScrollView _anScroll;
        private bool _analysisTab;
        private float _nextAnalysis;
        private VisualElement _anEmpty, _anBody, _anPlots;
        private Label _anEmptyTitle, _anEmptyText, _anBadge, _anHint, _anWait, _anMarkersTitle, _anReadout;
        private AnalysisMarkerStrip _anStrip;
        private readonly Label[] _anAxis = new Label[5];
        private Button _btnAnalysis;
        private Label _analysisLabel;

        private long _anShownTotal = -1;
        private int _anShownHover = -2;
        private int _anShownMarkers = -1;
        private bool _anForce;
        private float _anHoverX = -1f;      // pointer x in panel coordinates, -1 = not hovering the charts
        private int _anHoverIndex = -1;

        /// <summary>The analysis tab is the one shown in the inspector.</summary>
        public bool AnalysisTabActive => _analysisTab;
        /// <summary>The recorded minute (tests and captures).</summary>
        internal AnalysisRecorder Recorder => _rec;
        /// <summary>Plot repaints requested so far, and how many refreshes asked for them (the 10 Hz cap is checked by the capture probes).</summary>
        public int AnalysisRepaints { get; private set; }
        public int AnalysisRefreshes { get; private set; }
        public int AnalysisHoverIndex => _anHoverIndex;

        // ───────────────────────── build ─────────────────────────

        private void BuildAnalysisTab()
        {
            // the header (source badge, marker strip, time axis, readout) stays put; only the charts scroll under it
            _anPanel = new VisualElement { name = "DkAnalysisPanel" };
            _anPanel.AddToClassList("dk-analysis");
            int at = _sheet.IndexOf(_flightPanel) + 1;
            _sheet.Insert(at, _anPanel);

            // empty state: no session, say so and what to do
            _anEmpty = new VisualElement { name = "DkAnEmpty", pickingMode = PickingMode.Ignore };
            _anEmpty.AddToClassList("dk-fl-empty");
            var icon = _chrome.NewIconPublic("analyze", 24);
            icon.AddToClassList("dk-fl-empty-icon");
            _anEmpty.Add(icon);
            _anEmptyTitle = Text("dk-fl-empty-title", _anEmpty);
            _anEmptyText = Text("dk-note", _anEmpty);
            _anPanel.Add(_anEmpty);

            _anBody = new VisualElement { name = "DkAnBody", pickingMode = PickingMode.Ignore };
            _anBody.AddToClassList("dk-an-body");
            _anBadge = Text("dk-fl-badge", _anBody);
            _anWait = Text("dk-note", _anBody);

            // marker strip
            var markersHead = new VisualElement { pickingMode = PickingMode.Ignore };
            markersHead.AddToClassList("dk-an-head");
            markersHead.AddToClassList("dk-an-head--first");
            _anMarkersTitle = Text("dk-an-title", markersHead);
            _anHint = Text("dk-an-range", markersHead);
            _anBody.Add(markersHead);
            _anStrip = new AnalysisMarkerStrip { name = "DkAnStrip" };
            _anStrip.AddToClassList("dk-an-strip");
            _anStrip.Markers = _rec.Markers;
            _anBody.Add(_anStrip);

            var axis = new VisualElement { pickingMode = PickingMode.Ignore };
            axis.AddToClassList("dk-an-axis");
            for (int i = 0; i < _anAxis.Length; i++) _anAxis[i] = Text("dk-an-axis-t", axis);
            _anBody.Add(axis);

            _anReadout = Text("dk-an-readout", _anBody);

            // charts on one shared horizontal axis (scrolled)
            _anScroll = new ScrollView(ScrollViewMode.Vertical) { name = "DkAnScroll" };
            _anScroll.AddToClassList("dk-an-scroll");
            _anScroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            _anPlots = new VisualElement { name = "DkAnPlots" };
            _anPlots.AddToClassList("dk-an-plots");
            foreach (ChartSpec spec in ChartSpec.All) _anCells.Add(MakeCell(spec));
            foreach (ChartCell c in _anCells) _anPlots.Add(c.Root);
            _anPlots.RegisterCallback<PointerMoveEvent>(OnAnalysisPointer);
            _anPlots.RegisterCallback<PointerEnterEvent>(OnAnalysisPointerEnter);
            _anPlots.RegisterCallback<PointerLeaveEvent>(_ => SetAnalysisHover(-1f));
            _anStrip.RegisterCallback<PointerMoveEvent>(OnAnalysisPointer);
            _anStrip.RegisterCallback<PointerLeaveEvent>(_ => SetAnalysisHover(-1f));
            _anScroll.Add(_anPlots);
            _anBody.Add(_anScroll);
            _anPanel.Add(_anBody);
        }

        private ChartCell MakeCell(ChartSpec spec)
        {
            var c = new ChartCell { Spec = spec };
            c.Root = new VisualElement { name = "DkAn_" + spec.Id, pickingMode = PickingMode.Ignore };
            c.Root.AddToClassList("dk-an-cell");

            var head = new VisualElement { pickingMode = PickingMode.Ignore };
            head.AddToClassList("dk-an-head");
            c.Title = Text("dk-an-title", head);
            c.Tag = Text("dk-tag", head);
            if (spec.Lines.Length == 1) c.Value = Text("dk-an-value", head);
            c.Root.Add(head);

            c.Lines = new Series[spec.Lines.Length];
            for (int i = 0; i < c.Lines.Length; i++) c.Lines[i] = _rec.History[spec.Lines[i]];

            if (spec.Lines.Length > 1)
            {
                var vals = new VisualElement { pickingMode = PickingMode.Ignore };
                vals.AddToClassList("dk-an-vals");
                c.LineValues = new Label[spec.Lines.Length];
                for (int i = 0; i < spec.Lines.Length; i++)
                {
                    c.LineValues[i] = Text("dk-an-lv", vals);
                    c.LineValues[i].style.color = AnalysisColors.Lines[Mathf.Min(i, AnalysisColors.Lines.Length - 1)];
                }
                c.Root.Add(vals);
            }

            c.Plot = new AnalysisPlot { name = "DkAnPlot_" + spec.Id };
            c.Plot.AddToClassList("dk-an-plot");
            c.Plot.Lines = c.Lines;
            c.Plot.LineCount = c.Lines.Length;
            c.Plot.MinSpan = spec.MinSpan;
            c.Plot.Markers = _rec.Markers;
            c.Cursor = new VisualElement { pickingMode = PickingMode.Ignore };
            c.Cursor.AddToClassList("dk-an-cursor");
            c.Cursor.style.display = DisplayStyle.None;
            c.Plot.Add(c.Cursor);
            c.Root.Add(c.Plot);

            c.Range = Text("dk-an-range", c.Root);
            return c;
        }

        private void DisposeAnalysisTab()
        {
            if (_anClient != null) { _anClient.AlertRaised -= OnAnalysisAlert; _anClient = null; }
            _anPanel?.RemoveFromHierarchy();
        }

        // ───────────────────────── texts ─────────────────────────

        private void RelabelAnalysisTab()
        {
            if (_anPanel == null) return;
            ((Label)_tabAnalysis[0]).text = T("tab.analysis");
            _anEmptyTitle.text = T("fl.empty.title");
            _anEmptyText.text = T("an.empty");
            _anHint.text = T("an.hint");
            _anMarkersTitle.text = T("an.markers");
            foreach (ChartCell c in _anCells)
            {
                c.Title.text = T(c.Spec.TitleKey);
                c.ShownValue = null;
                c.ShownRange = null;
            }
            string[] secs = { "−60 s", "−45", "−30", "−15", T("an.now") };
            for (int i = 0; i < _anAxis.Length; i++) _anAxis[i].text = secs[i];
            if (_analysisLabel != null) _analysisLabel.text = T("tab.analysis");
            if (_btnAnalysis != null) _btnAnalysis.tooltip = T("tab.analysis");
            _anForce = true;
            _nextAnalysis = 0f;
        }

        // ───────────────────────── recording (every frame, even with the tab closed) ─────────────────────────

        private void TickAnalysisRecorder()
        {
            TelemetryClient client = DesktopTelemetry.Client;
            if (!ReferenceEquals(client, _anClient))
            {
                if (_anClient != null) _anClient.AlertRaised -= OnAnalysisAlert;
                _anClient = client;
                _rec.Reset();
                _anSeenFaults.Clear();
                _anShownTotal = -1;
                _anForce = true;
                if (client != null) client.AlertRaised += OnAnalysisAlert;
            }

            if (client == null) return;
            TelemetryFrame f = client.Displayed;
            // fresh = data is arriving; otherwise the sample is a gap (no flat line drawn over a stall)
            bool fresh = client.State == LinkState.Live && client.StarvedMs <= TelemetryClient.StaleAfterMs;
            int before = _rec.History.Count;
            long totalBefore = _rec.History.TotalSamples;
            _rec.Update(Time.unscaledTime, f, fresh);
            if (_rec.History.TotalSamples < totalBefore || (before > 0 && _rec.History.Count == 0)) _anForce = true; // restarted (segment change)
            if (f != null) TrackFaults(client, f);
        }

        private void OnAnalysisAlert(Alert a)
        {
            TelemetryClient client = _anClient;
            if (client == null || a == null) return;
            var labels = HonestyLabels.Load();
            double now = client.Displayed != null ? client.Displayed.ts : ReplaySchedule.WallClockMs();
            bool synthetic = DesktopTelemetry.IsLocalReplay && AlertText.IsSynthetic(a, client.Faults, now);
            string es = MarkerAlertText(a, client, synthetic, "es", labels);
            string en = MarkerAlertText(a, client, synthetic, "en", labels);
            _rec.AddAlert(a.id, a.severity, es, en);
            _anForce = true;
        }

        private static string MarkerAlertText(Alert a, TelemetryClient client, bool synthetic, string lang, HonestyLabels labels)
        {
            string head = TelemetryStrings.T(a.severity == AlertSeverity.Critical ? "an.alert.critical" : "an.alert.warning", lang);
            string msg = client.Displayed != null ? AlertText.Build(a, client.Displayed, client.Hello, client.Classification, synthetic, lang, labels) : (a.component ?? a.id);
            return head + " · " + msg;
        }

        /// <summary>A synthetic fault of the local replay that began after the session started becomes a marker with its duration.</summary>
        private void TrackFaults(TelemetryClient client, TelemetryFrame f)
        {
            IReadOnlyList<Fault> list = client.Faults;
            for (int i = 0; i < list.Count; i++)
            {
                Fault ft = list[i];
                if (ft.until <= f.ts) continue; // already over
                bool seen = false;
                for (int k = 0; k < _anSeenFaults.Count; k++)
                    if (_anSeenFaults[k].Value == ft.since && _anSeenFaults[k].Key == ft.id) { seen = true; break; }
                if (seen) continue;
                _anSeenFaults.Add(new KeyValuePair<string, double>(ft.id, ft.since));
                var labels = HonestyLabels.Load();
                double secs = Math.Max(1.0, (ft.until - ft.since) / 1000.0);
                _rec.AddFault(ft.id, secs, MarkerFaultText(ft, secs, "es", labels), MarkerFaultText(ft, secs, "en", labels));
                _anForce = true;
            }
        }

        private static string MarkerFaultText(Fault f, double secs, string lang, HonestyLabels labels)
        {
            string kind = TelemetryStrings.T("faults." + WireNames.Of(f.kind), lang);
            if (f.HasMotor && f.motor != 0) kind += " " + f.motor;
            string synth = AlertText.SyntheticFault(lang, labels);
            if (synth.Length > 0) synth = char.ToUpperInvariant(synth[0]) + synth.Substring(1);
            return synth + " · " + kind + " · " + Math.Round(secs).ToString("0", CultureInfo.InvariantCulture) + " s";
        }

        // ───────────────────────── pointer ─────────────────────────

        /// <summary>Verification aid: puts the cursor on sample `index` (0 = oldest) as a pointer at that x would.</summary>
        public void DebugHoverAtIndex(int index)
        {
            if (_anCells.Count == 0) return;
            AnalysisPlot p = _anCells[0].Plot;
            float x = p.worldBound.xMin + 2.5f + (float)Sparkline.X(index, _rec.History.Count, p.PlotWidth, _rec.History.Capacity);
            SetAnalysisHover(x);
            _nextAnalysis = 0f;
        }

        public void DebugHoverOff() { SetAnalysisHover(-1f); _nextAnalysis = 0f; }

        private void OnAnalysisPointerEnter(PointerEnterEvent e) => SetAnalysisHover(e.position.x);
        private void OnAnalysisPointer(PointerMoveEvent e) => SetAnalysisHover(e.position.x);

        private void SetAnalysisHover(float panelX)
        {
            _anHoverX = panelX;
            UpdateCursorLines();
        }

        /// <summary>The cursor is a thin element inside each plot: moving it costs a layout, not a repaint of the charts.</summary>
        private void UpdateCursorLines()
        {
            AnalysisPlot refPlot = _anCells.Count > 0 ? _anCells[0].Plot : null;
            int count = _rec.History.Count;
            _anHoverIndex = -1;
            if (refPlot != null && _anHoverX >= 0f && count > 0)
            {
                float local = _anHoverX - refPlot.worldBound.xMin - 2.5f;
                _anHoverIndex = Sparkline.IndexAt(local, count, refPlot.PlotWidth, _rec.History.Capacity);
            }

            float x = 0f;
            bool show = _anHoverIndex >= 0 && refPlot != null;
            if (show) x = 2.5f + (float)Sparkline.X(_anHoverIndex, count, refPlot.PlotWidth, _rec.History.Capacity);
            foreach (ChartCell c in _anCells)
            {
                c.Cursor.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (show) c.Cursor.style.left = x;
            }
        }

        // ───────────────────────── refresh (10 Hz while the tab is open) ─────────────────────────

        private void TickAnalysisTab()
        {
            if (_anPanel == null) return;
            bool open = _analysisTab;
            _btnAnalysis?.EnableInClassList("dk-strip-btn--on", open);
            if (!open) return;
            if (Time.unscaledTime < _nextAnalysis) return;
            _nextAnalysis = Time.unscaledTime + AnalysisPeriod;

            bool running = DesktopTelemetry.IsRunning;
            _anEmpty.style.display = running ? DisplayStyle.None : DisplayStyle.Flex;
            _anBody.style.display = running ? DisplayStyle.Flex : DisplayStyle.None;
            if (!running) return;

            TelemetryClient client = DesktopTelemetry.Client;
            string lang = Lang;
            var labels = HonestyLabels.Load();
            SourceClassification cls = client.Classification;
            HelloMessage h = client.Hello;
            FlightHistory hist = _rec.History;

            // the honest source text, exactly as the "Vuelo" tab shows it
            string badge = DesktopTelemetry.BadgeText(lang);
            if (_anBadge.text != badge) _anBadge.text = badge;

            bool noFrame = client.Displayed == null;
            string wait = noFrame ? T("fl.waiting") : hist.Count < 2 ? T("an.collecting") : string.Empty;
            SetText(_anWait, wait);
            _anWait.style.display = wait.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;

            UpdateCursorLines();
            bool repaint = _anForce || hist.TotalSamples != _anShownTotal || _rec.Markers.Count != _anShownMarkers;
            bool changed = repaint || _anHoverIndex != _anShownHover;
            if (!changed) return;
            _anForce = false;
            _anShownTotal = hist.TotalSamples;
            _anShownHover = _anHoverIndex;
            _anShownMarkers = _rec.Markers.Count;

            if (repaint) AnalysisRefreshes++;
            foreach (ChartCell c in _anCells)
            {
                ReadingOrigin o = FlightOrigins.ForSession(c.Spec.RpmOrigin ? FlightOrigins.Rpm(h, cls) : FlightOrigins.Base(cls), DesktopTelemetry.ReadingsSimulated);
                SetTag(c.Tag, o, FlightOrigins.Text(o, lang, labels));

                SparkRange range = Sparkline.Range(c.Lines, c.Lines.Length, c.Spec.MinSpan);
                if (repaint)
                {
                    c.Plot.Range = range;
                    c.Plot.TotalSamples = hist.TotalSamples;
                    c.Plot.MarkDirtyRepaint();
                    AnalysisRepaints++;
                }

                int idx = _anHoverIndex >= 0 ? _anHoverIndex : hist.Count - 1;
                if (c.Spec.Lines.Length == 1)
                {
                    string v = idx >= 0 && idx < c.Lines[0].Count ? Sparkline.Format(c.Lines[0][idx], c.Spec.Digits) + " " + c.Spec.Unit : "—";
                    if (_anHoverIndex < 0 && idx >= 0 && double.IsNaN(c.Lines[0][idx])) v = T("an.gap");
                    SetText(c.Value, v);
                }
                else
                {
                    for (int i = 0; i < c.Lines.Length; i++)
                    {
                        string v = idx >= 0 && idx < c.Lines[i].Count ? Sparkline.Format(c.Lines[i][idx], c.Spec.Digits) : "—";
                        string key = c.Spec.LineKeys != null ? T(c.Spec.LineKeys[i]) : string.Empty;
                        SetText(c.LineValues[i], key + " " + v);
                    }
                }

                string rng = range.Samples == 0 ? "—" : Sparkline.Format(range.Min, c.Spec.Digits) + " – " + Sparkline.Format(range.Max, c.Spec.Digits) + " " + c.Spec.Unit;
                SetText(c.Range, rng);
            }

            // marker strip + readout
            if (repaint)
            {
                _anStrip.TotalSamples = hist.TotalSamples;
                _anStrip.MarkDirtyRepaint();
            }
            SetText(_anReadout, ReadoutText(lang));
        }

        private string ReadoutText(string lang)
        {
            FlightHistory hist = _rec.History;
            if (_anHoverIndex < 0) return hist.Count < 2 ? string.Empty : _rec.Markers.Count == 0 ? T("an.markers.none") + ". " + T("an.hover") : T("an.hover");
            double ago = (hist.Count - 1 - _anHoverIndex) * HistoryConstants.SamplePeriodS;
            string t = _anHoverIndex == hist.Count - 1 ? T("an.now") : TelemetryStrings.F("an.cursor", lang, "s", ago.ToString("0.0", CultureInfo.InvariantCulture));
            // a marker within a sample or two of the cursor (or a fault bar that covers it)
            long sample = hist.TotalSamples - hist.Count + _anHoverIndex;
            for (int m = _rec.Markers.Count - 1; m >= 0; m--)
            {
                Marker mk = _rec.Markers[m];
                bool hit = mk.Kind == MarkerKind.Fault ? sample >= mk.Sample && sample <= mk.Sample + mk.Span : Math.Abs(sample - mk.Sample) <= 1;
                if (hit) return t + " · " + (lang == "en" ? mk.TextEn : mk.Text);
            }
            return t;
        }
    }
}
#endif
