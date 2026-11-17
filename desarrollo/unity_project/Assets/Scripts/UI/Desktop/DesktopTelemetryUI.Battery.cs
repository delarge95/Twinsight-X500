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
    /// Lote 1b, tool 8 (PC only): the estimated health of the battery, in the «Batería» section of the «Vuelo» tab under the readings that were already there
    /// (level, voltage, current, temperature). Rows: internal resistance (and per cell), sag under load, rest voltage, time left by the slope of the charge
    /// and, only in the simulator (the one source whose capacity is known), time left by the current; then the chart «Tensión frente a corriente» (the cloud of
    /// the last minute and the line V₀ − R̂·I). The maths is the pure <see cref="BatteryHealth"/> (golden-tested against the web); here only the 4 Hz sampler
    /// and the drawing. Honesty: every row here is «estimado» (it is not a measurement), and over simulated readings the section says so.
    /// </summary>
    internal sealed partial class DesktopTelemetryUI
    {
        private const float ViW = 240f, ViH = 110f;

        private readonly BatteryEstimator _bat = new BatteryEstimator();
        private TelemetryClient _batClient;
        private int _batSeg = int.MinValue;
        private float _batNext;
        private Reading _rBatR, _rBatDrop, _rBatRest, _rBatTime, _rBatTimeI;
        private Label _batSubR, _batSubRest, _batSubTime, _batSubTimeI, _batSimData, _batNote, _batViTitle, _batViAxes, _batViFit;
        private VisualElement _batViBox;
        private DkViChart _batVi;
        private readonly ViCloud _batCloud = new ViCloud();
        private BatteryReport _batReport;

        // ── read by the verification harness ──
        public BatteryEstimator BatteryState => _bat;
        public BatteryReport BatteryReportNow => _batReport;
        public string BatteryRowText(string id)
        {
            Reading r = id == "r" ? _rBatR : id == "drop" ? _rBatDrop : id == "rest" ? _rBatRest : id == "time" ? _rBatTime : id == "timeI" ? _rBatTimeI : null;
            if (r == null || r.Root.style.display == DisplayStyle.None) return null;
            return r.Name.text + " | " + r.Value.text + " | " + r.Tag.text;
        }
        public string BatterySimDataNote => _batSimData != null && _batSimData.style.display != DisplayStyle.None ? _batSimData.text : null;
        public string BatteryFitText => _batViFit?.text;
        public bool BatteryChartShown => _batViBox != null && _batViBox.style.display != DisplayStyle.None;
        public DkViChart BatteryChart => _batVi;

        private void BuildBatteryRows(VisualElement parent)
        {
            _batSimData = Text("dk-note", parent);
            _batSimData.AddToClassList("dk-note--muted");
            _rBatR = AddReading(parent, "battery.r");
            _batSubR = BatSub(parent);
            _rBatDrop = AddReading(parent, "battery.drop");
            _rBatRest = AddReading(parent, "battery.rest");
            _batSubRest = BatSub(parent);
            _rBatTime = AddReading(parent, "battery.time");
            _batSubTime = BatSub(parent);
            _rBatTimeI = AddReading(parent, "battery.timeI");
            _batSubTimeI = BatSub(parent);
            foreach (Reading r in new[] { _rBatR, _rBatDrop, _rBatRest, _rBatTime, _rBatTimeI }) r.Root.AddToClassList("dk-reading--derived");

            _batViBox = new VisualElement { name = "DkBatVi", pickingMode = PickingMode.Ignore };
            _batViBox.AddToClassList("dk-bat-vi");
            _batViTitle = Text("dk-bat-vi-title", _batViBox);
            _batVi = new DkViChart { name = "DkBatViChart", Cloud = _batCloud };
            _batVi.style.width = ViW;
            _batVi.style.height = ViH;
            _batViBox.Add(_batVi);
            _batViAxes = Text("dk-bat-sub", _batViBox);
            _batViFit = Text("dk-note", _batViBox);
            _batViFit.AddToClassList("dk-note--muted");
            parent.Add(_batViBox);

            _batNote = Text("dk-note", parent);
            _batNote.AddToClassList("dk-note--muted");
        }

        private static Label BatSub(VisualElement parent)
        {
            Label l = Text("dk-bat-sub", parent);
            l.style.display = DisplayStyle.None;
            return l;
        }

        private void RelabelBattery()
        {
            if (_rBatR == null) return;
            foreach (Reading r in new[] { _rBatR, _rBatDrop, _rBatRest, _rBatTime, _rBatTimeI }) r.Name.text = T(r.Key);
            _batSimData.text = T("battery.simData");
            _batNote.text = T("battery.note");
            _batViTitle.text = T("battery.viTitle") + " · " + T("battery.viHint");
        }

        /// <summary>
        /// Samples the displayed frame at 4 Hz, whatever is on screen. A new session starts a new state, and so does a discontinuity: a replay loop wrap (new
        /// segment) or the clock going back (the simulator restarted).
        /// </summary>
        private void TickBatterySampler()
        {
            TelemetryClient client = DesktopTelemetry.Client;
            if (!ReferenceEquals(client, _batClient)) { _batClient = client; _bat.Clear(); _batSeg = int.MinValue; _batNext = 0f; }
            if (client == null || Time.unscaledTime < _batNext) return;
            _batNext = Time.unscaledTime + 1f / BatteryHealth.SampleHz;
            TelemetryFrame f = client.Displayed;
            if (f == null) return;
            bool restart = _batSeg != int.MinValue && _batSeg != f.seg;
            _batSeg = f.seg;
            if (restart || (_bat.HasLast && f.lt < _bat.Last.t - 1)) _bat.Clear();
            BatteryHealth.Push(_bat, new BatterySample(f.lt, f.bat.v, f.bat.i, f.bat.rem));
        }

        private static string BatClock(double s)
        {
            if (s >= 3600) return "> 1 h";
            return ((int)Math.Floor(s / 60)).ToString(CultureInfo.InvariantCulture) + ":" + ((int)Math.Floor(s % 60)).ToString("00", CultureInfo.InvariantCulture);
        }

        private static void BatSubSet(Label l, string text)
        {
            l.style.display = text == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (text != null) SetText(l, text);
        }

        private void TickBatteryRows(TelemetryClient client, TelemetryFrame f, HelloMessage h, int cells, string lang, HonestyLabels labels)
        {
            if (_rBatR == null) return;
            CultureInfo inv = CultureInfo.InvariantCulture;
            bool sim = DesktopTelemetry.IsSimulator;
            BatterySource src = sim ? BatterySource.Sim : client.Classification == SourceClassification.Replay ? BatterySource.Replay : BatterySource.Sitl;
            bool flying = f.armed && !f.landed;
            BatteryReport r = BatteryHealth.Health(_bat, cells, flying, sim ? BatteryHealth.SimCapacityAh : (double?)null);
            _batReport = r;
            BatteryOrigins o = BatteryHealth.Origins(src, h != null && h.provenance.HasBatteryCurrent ? h.provenance.batteryCurrent : null, DesktopTelemetry.IsOwnReplay);
            string tag = FlightOrigins.Text(o.derived, lang, labels);
            string calc = TelemetryStrings.F("battery.calculating", lang, "n", r.quality, "min", BatteryHealth.MinAccepted);
            string perCell = T("unit.vPerCell");

            _batSimData.style.display = o.onSimData ? DisplayStyle.Flex : DisplayStyle.None;
            Set(_rBatR, r.rHat.HasValue ? (r.rHat.Value * 1000).ToString("0.0", inv) + " mΩ" : calc, o.derived, tag);
            BatSubSet(_batSubR, r.rCell.HasValue ? (r.rCell.Value * 1000).ToString("0.0", inv) + " mΩ " + T("battery.perCell") : null);
            Set(_rBatDrop, r.dropV.HasValue ? r.dropV.Value.ToString("0.00", inv) + " V" : calc, o.derived, tag);
            Set(_rBatRest, r.restV.HasValue ? r.restV.Value.ToString("0.00", inv) + " V" : calc, o.derived, tag);
            BatSubSet(_batSubRest, r.restCell.HasValue ? r.restCell.Value.ToString("0.00", inv) + " " + perCell : null);
            Set(_rBatTime, r.bySlopeS.HasValue ? BatClock(r.bySlopeS.Value) : "—", o.derived, tag);
            BatSubSet(_batSubTime, T(r.bySlopeS.HasValue ? "battery.timeSlope" : "battery.timeNone"));
            _rBatTimeI.Root.style.display = sim ? DisplayStyle.Flex : DisplayStyle.None;
            if (sim)
            {
                Set(_rBatTimeI, r.byCurrentS.HasValue ? BatClock(r.byCurrentS.Value) : "—", o.derived, tag);
                BatSubSet(_batSubTimeI, TelemetryStrings.F("battery.timeISub", lang, "ah", BatteryHealth.SimCapacityAh));
            }
            else BatSubSet(_batSubTimeI, null);

            // voltage against current: the cloud of the ring and the fitted line through the newest sample
            bool chart = _bat.samples.Count >= 2;
            _batViBox.style.display = chart ? DisplayStyle.Flex : DisplayStyle.None;
            if (!chart) return;
            BatteryHealth.Cloud(_bat.samples, r.rHat, ViW, ViH, _batCloud);
            _batVi.MarkDirtyRepaint();
            SetText(_batViAxes, _batCloud.iMin.ToString("0", inv) + " – " + _batCloud.iMax.ToString("0", inv) + " A   ·   " + _batCloud.vMin.ToString("0.0", inv) + " – " + _batCloud.vMax.ToString("0.0", inv) + " V");
            SetText(_batViFit, r.rHat.HasValue ? TelemetryStrings.F("battery.viFit", lang, "r", (r.rHat.Value * 1000).ToString("0.0", inv)) : T("battery.viNoFit"));
        }
    }

    /// <summary>The cloud «tensión frente a corriente»: one dot per sample of the last minute (the newest larger, in the accent) and the fitted line.</summary>
    internal sealed class DkViChart : VisualElement
    {
        public ViCloud Cloud;
        static readonly Color Dot = new Color(0.72f, 0.75f, 0.8f, 0.55f);
        static readonly Color Now = new Color(70f / 255f, 175f / 255f, 1f, 1f);
        static readonly Color Fit = new Color(245f / 255f, 185f / 255f, 66f / 255f, 0.9f);
        /// <summary>Dots drawn in the last repaint (read by the harness).</summary>
        public int Drawn { get; private set; }
        public bool LineDrawn { get; private set; }

        public DkViChart()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("dk-bat-vi-chart");
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext ctx)
        {
            Drawn = 0;
            LineDrawn = false;
            ViCloud c = Cloud;
            if (c == null || c.count == 0) return;
            Painter2D p = ctx.painter2D;
            if (c.hasLine)
            {
                p.strokeColor = Fit;
                p.lineWidth = 1.5f;
                p.BeginPath();
                p.MoveTo(new Vector2((float)c.lineX0, (float)c.lineY0));
                p.LineTo(new Vector2((float)c.lineX1, (float)c.lineY1));
                p.Stroke();
                LineDrawn = true;
            }
            for (int k = 0; k < c.count; k++)
            {
                bool last = k == c.count - 1;
                p.fillColor = last ? Now : Dot;
                p.BeginPath();
                p.Arc(new Vector2((float)c.x[k], (float)c.y[k]), last ? 3f : 1.6f, 0f, 360f);
                p.Fill();
                Drawn++;
            }
        }
    }
}
#endif
