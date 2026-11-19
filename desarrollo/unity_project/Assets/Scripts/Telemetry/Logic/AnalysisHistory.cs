// E-4: the history behind the "Analisis" charts of the desktop telemetry UI. Pure C# (no UnityEngine): compiled everywhere, tested in EditMode.
//
// Port of src/shared/history.ts (web): a fixed-size ring per series, 60 s at 4 Hz (HISTORY_LENGTH 240), `seriesValue`, `pushHistory` (the
// oldest sample drops once full) and `sparkline` (newest sample at the right edge; vertical range = the data range widened to at least
// `minSpan` and centred). The golden of the web (contracts/golden/history.json) is the reference for those parts.
//
// Desktop additions (not in history.ts, tested separately): extra channels (horizontal / vertical speed, battery level, the 4 rotor speeds),
// gaps (NaN samples while the link has no fresh data: a chart must not draw a flat line over a stall), the 4 Hz sampler (the web uses a timer),
// the restart at a segment change (replay loop wrap) and a ring of alert / fault markers anchored to sample numbers so that they scroll with
// the charts. Nothing here knows about UI Toolkit.

using System;
using System.Globalization;
using System.Text;

namespace WebGL.Telemetry
{
    public static class HistoryConstants
    {
        /// <summary>HISTORY_SAMPLE_HZ of the web.</summary>
        public const int SampleHz = 4;
        /// <summary>HISTORY_LENGTH of the web: 60 s at 4 Hz.</summary>
        public const int Length = 240;
        public const double SamplePeriodS = 1.0 / SampleHz;
        /// <summary>Seconds covered by a full ring (newest to oldest sample).</summary>
        public const double WindowS = (Length - 1) * SamplePeriodS;
        public const int MaxMarkers = 64;
    }

    /// <summary>
    /// The plotted values. The first five are the web's SERIES in the same order (altitude, speed, voltage, throttle, vibration);
    /// the others are desktop additions for the charts that ask for them.
    /// </summary>
    public enum Channel
    {
        Altitude, Speed, Voltage, Throttle, Vibration,
        HSpeed, VSpeed, BatteryPct, Rpm1, Rpm2, Rpm3, Rpm4,
    }

    /// <summary>Fixed-capacity ring of doubles, newest last. NaN is a gap.</summary>
    public sealed class Series
    {
        readonly double[] items;
        int start, count;

        public Series(int capacity) { items = new double[Math.Max(1, capacity)]; }
        public int Count => count;
        public int Capacity => items.Length;

        /// <summary>Appends; the oldest sample drops once the ring is full (pushHistory).</summary>
        public void Add(double v)
        {
            if (count < items.Length) items[(start + count++) % items.Length] = v;
            else { items[start] = v; start = (start + 1) % items.Length; }
        }

        /// <summary>i = 0 is the oldest.</summary>
        public double this[int i]
        {
            get
            {
                if (i < 0 || i >= count) throw new ArgumentOutOfRangeException(nameof(i));
                return items[(start + i) % items.Length];
            }
        }

        /// <summary>The newest sample (NaN when empty or a gap).</summary>
        public double Last => count > 0 ? this[count - 1] : double.NaN;

        public void Clear() { start = 0; count = 0; }
    }

    public static class History
    {
        public const int ChannelCount = 12;
        /// <summary>How many channels are the web's SERIES.</summary>
        public const int WebSeriesCount = 5;

        /// <summary>The value each chart plots, from one frame (seriesValue of history.ts for the first five channels).</summary>
        public static double ChannelValue(Channel id, TelemetryFrame f)
        {
            switch (id)
            {
                case Channel.Altitude: return -f.p[2];
                case Channel.Speed: return Math.Sqrt(f.v[0] * f.v[0] + f.v[1] * f.v[1] + f.v[2] * f.v[2]);
                case Channel.Voltage: return f.bat.v;
                case Channel.Throttle: return ((f.u[0] + f.u[1] + f.u[2] + f.u[3]) / 4) * 100;
                case Channel.Vibration: return f.vib;
                case Channel.HSpeed: return Math.Sqrt(f.v[0] * f.v[0] + f.v[1] * f.v[1]);
                case Channel.VSpeed: return -f.v[2];
                case Channel.BatteryPct: return f.bat.rem * 100;
                case Channel.Rpm1: return f.rpm[0];
                case Channel.Rpm2: return f.rpm[1];
                case Channel.Rpm3: return f.rpm[2];
                case Channel.Rpm4: return f.rpm[3];
                default: return double.NaN;
            }
        }
    }

    /// <summary>The rings of every channel, advanced together (pushHistory of the web for all the series at once).</summary>
    public sealed class FlightHistory
    {
        readonly Series[] channels = new Series[History.ChannelCount];

        public FlightHistory(int length = HistoryConstants.Length)
        {
            for (int i = 0; i < channels.Length; i++) channels[i] = new Series(length);
        }

        public int Capacity => channels[0].Capacity;
        public int Count => channels[0].Count;
        /// <summary>Samples (and gaps) pushed since the last Clear; the newest sample has number TotalSamples - 1. Markers are anchored to it.</summary>
        public long TotalSamples { get; private set; }
        public Series this[Channel c] => channels[(int)c];

        public void Push(TelemetryFrame f)
        {
            for (int i = 0; i < channels.Length; i++) channels[i].Add(History.ChannelValue((Channel)i, f));
            TotalSamples++;
        }

        /// <summary>A sample without data (the link went quiet): the charts break the line instead of drawing a flat one.</summary>
        public void PushGap()
        {
            for (int i = 0; i < channels.Length; i++) channels[i].Add(double.NaN);
            TotalSamples++;
        }

        public void Clear()
        {
            for (int i = 0; i < channels.Length; i++) channels[i].Clear();
            TotalSamples = 0;
        }
    }

    public enum MarkerKind { Alert, Fault }

    public struct Marker
    {
        /// <summary>Sample number (FlightHistory.TotalSamples - 1 when it was recorded).</summary>
        public long Sample;
        public MarkerKind Kind;
        public AlertSeverity Severity;
        /// <summary>Samples the marker lasts (faults: their duration); 0 for a tick.</summary>
        public int Span;
        public string Id;
        /// <summary>Text for the readout under the marker strip, Spanish and English (built once, when the event happens).</summary>
        public string Text, TextEn;
    }

    /// <summary>Ring of the last markers. A marker that left the window is simply never drawn.</summary>
    public sealed class MarkerLog
    {
        readonly Marker[] items;
        int start, count;

        public MarkerLog(int capacity = HistoryConstants.MaxMarkers) { items = new Marker[Math.Max(1, capacity)]; }
        public int Count => count;

        public void Add(Marker m)
        {
            if (count < items.Length) items[(start + count++) % items.Length] = m;
            else { items[start] = m; start = (start + 1) % items.Length; }
        }

        /// <summary>i = 0 is the oldest.</summary>
        public Marker this[int i]
        {
            get
            {
                if (i < 0 || i >= count) throw new ArgumentOutOfRangeException(nameof(i));
                return items[(start + i) % items.Length];
            }
        }

        public void Clear() { start = 0; count = 0; Array.Clear(items, 0, items.Length); }
    }

    /// <summary>
    /// Samples the displayed frame at 4 Hz into a <see cref="FlightHistory"/> and keeps the markers. The web does the same with a timer and
    /// restarts the charts when `seg` changes (a replay loop wrap is a discontinuity). Call Update every rendered frame.
    /// </summary>
    public sealed class AnalysisRecorder
    {
        public readonly FlightHistory History = new FlightHistory(HistoryConstants.Length);
        public readonly MarkerLog Markers = new MarkerLog(HistoryConstants.MaxMarkers);

        double nextAt = double.NaN;
        int segment = int.MinValue;
        bool haveSegment;

        /// <summary>
        /// nowS: a monotonic clock in seconds. frame: the displayed frame (null = nothing to sample). fresh: false while the link has no
        /// fresh data (the sample becomes a gap). Returns true when a sample (or gap) was added.
        /// </summary>
        public bool Update(double nowS, TelemetryFrame frame, bool fresh)
        {
            if (frame == null) return false;
            if (haveSegment && frame.seg != segment)
            {
                // a discontinuity: start the charts again instead of drawing a jump
                Reset();
            }
            segment = frame.seg;
            haveSegment = true;

            if (double.IsNaN(nextAt)) nextAt = nowS;
            if (nowS < nextAt) return false;
            nextAt += HistoryConstants.SamplePeriodS;
            // a long hitch (window dragged, breakpoint): do not catch up with a burst of samples, as a timer would not
            if (nextAt < nowS) nextAt = nowS + HistoryConstants.SamplePeriodS;
            if (fresh) History.Push(frame); else History.PushGap();
            return true;
        }

        /// <summary>Forgets the samples and the markers (new session, segment change).</summary>
        public void Reset()
        {
            History.Clear();
            Markers.Clear();
            nextAt = double.NaN;
            haveSegment = false;
            segment = int.MinValue;
        }

        long NewestSample => Math.Max(0, History.TotalSamples - 1);

        /// <summary>A new alert: a tick at the newest sample.</summary>
        public void AddAlert(string id, AlertSeverity severity, string text, string textEn = null)
        {
            Markers.Add(new Marker { Sample = NewestSample, Kind = MarkerKind.Alert, Severity = severity, Span = 0, Id = id, Text = text, TextEn = textEn ?? text });
        }

        /// <summary>A fault that starts now and lasts durationS.</summary>
        public void AddFault(string id, double durationS, string text, string textEn = null)
        {
            int span = (int)Math.Max(1, Math.Round(durationS * HistoryConstants.SampleHz));
            Markers.Add(new Marker { Sample = NewestSample, Kind = MarkerKind.Fault, Severity = AlertSeverity.Critical, Span = span, Id = id, Text = text, TextEn = textEn ?? text });
        }
    }

    /// <summary>One chart of the Analysis tab: what it plots and how (single source for the UI and the tests).</summary>
    public sealed class ChartSpec
    {
        public readonly string Id;
        public readonly string TitleKey;
        public readonly string Unit;
        public readonly int Digits;
        public readonly double MinSpan;
        public readonly Channel[] Lines;
        public readonly string[] LineKeys;
        /// <summary>true = the origin tag of the rotor speeds (hello.provenance.rpm); false = the base origin of the channels without a field.</summary>
        public readonly bool RpmOrigin;

        public ChartSpec(string id, string titleKey, string unit, int digits, double minSpan, Channel[] lines, string[] lineKeys, bool rpmOrigin = false)
        {
            Id = id; TitleKey = titleKey; Unit = unit; Digits = digits; MinSpan = minSpan; Lines = lines; LineKeys = lineKeys; RpmOrigin = rpmOrigin;
        }

        /// <summary>
        /// The origin tag of this chart: the same the Vuelo tab uses for that channel. Rotor speeds: hello.provenance.rpm; altitude: the pose and speeds: the
        /// velocity of contract 1.2 (D-10, absent = the base origin as before); the rest: the base origin of the channels without a field.
        /// </summary>
        public ReadingOrigin Origin(HelloMessage h, SourceClassification c) =>
            RpmOrigin ? FlightOrigins.Rpm(h, c) : Id == "altitude" ? FlightOrigins.Pose(h, c) : Id == "speed" ? FlightOrigins.Velocity(h, c) : FlightOrigins.Base(c);

        /// <summary>
        /// Order and settings of the web (FlightUI.tsx CHARTS: altitude 2, speed 2, voltage 0.6, throttle 20, vibration 2) with the desktop
        /// additions: horizontal / vertical speed on one chart, the battery level, and the 4 rotor speeds.
        /// </summary>
        public static readonly ChartSpec[] All =
        {
            new ChartSpec("altitude", "an.altitude", "m", 1, 2, new[] { Channel.Altitude }, null),
            new ChartSpec("speed", "an.speed", "m/s", 1, 2, new[] { Channel.HSpeed, Channel.VSpeed }, new[] { "an.hspeed", "an.vspeed" }),
            new ChartSpec("voltage", "an.voltage", "V", 2, 0.6, new[] { Channel.Voltage }, null),
            new ChartSpec("level", "an.level", "%", 0, 10, new[] { Channel.BatteryPct }, null),
            new ChartSpec("throttle", "an.throttle", "%", 0, 20, new[] { Channel.Throttle }, null),
            new ChartSpec("rpm", "an.rpm", "rpm", 0, 1000, new[] { Channel.Rpm1, Channel.Rpm2, Channel.Rpm3, Channel.Rpm4 }, new[] { "an.m1", "an.m2", "an.m3", "an.m4" }, true),
            new ChartSpec("vibration", "an.vibration", "m/s²", 1, 2, new[] { Channel.Vibration }, null),
        };
    }

    /// <summary>The range of a chart: the data range of all its lines widened to at least minSpan and centred (sparkline of history.ts).</summary>
    public struct SparkRange
    {
        public double Min, Max;
        /// <summary>Value at the bottom edge and the span: y = height - (v - Lo) / Span * height.</summary>
        public double Lo, Span;
        /// <summary>True when at least one line has two samples in a row (something to draw).</summary>
        public bool Drawable;
        /// <summary>Finite samples found.</summary>
        public int Samples;
    }

    public static class Sparkline
    {
        public static double Step(double width, int capacity) => width / (capacity - 1);

        /// <summary>x of sample i (0 = oldest) of `count` samples: the newest is at `width`, older ones step back.</summary>
        public static double X(int i, int count, double width, int capacity) => width - (count - 1 - i) * Step(width, capacity);

        public static double Y(double v, double lo, double span, double height) => height - ((v - lo) / span) * height;

        /// <summary>Sample nearest to x (0 = oldest), or -1 when there is none within half a step of the data.</summary>
        public static int IndexAt(double x, int count, double width, int capacity)
        {
            if (count <= 0) return -1;
            double step = Step(width, capacity);
            double x0 = X(0, count, width, capacity);
            int i = (int)Math.Round((x - x0) / step, MidpointRounding.AwayFromZero);
            if (i < 0 || i >= count) return -1;
            return i;
        }

        /// <summary>
        /// min/max over every finite sample of the lines, then the range of history.ts: span = max(max - min, minSpan) centred on the middle.
        /// With no finite sample the range is 0..minSpan and Drawable is false.
        /// </summary>
        public static SparkRange Range(Series[] lines, int lineCount, double minSpan)
        {
            double min = double.PositiveInfinity, max = double.NegativeInfinity;
            int samples = 0;
            bool drawable = false;
            for (int l = 0; l < lineCount; l++)
            {
                Series s = lines[l];
                bool prev = false;
                for (int i = 0; i < s.Count; i++)
                {
                    double v = s[i];
                    bool ok = !double.IsNaN(v) && !double.IsInfinity(v);
                    if (ok)
                    {
                        samples++;
                        if (v < min) min = v;
                        if (v > max) max = v;
                        if (prev) drawable = true;
                    }
                    prev = ok;
                }
            }

            var r = new SparkRange { Samples = samples, Drawable = drawable };
            if (samples == 0) { r.Min = 0; r.Max = 0; r.Span = minSpan; r.Lo = -minSpan / 2; return r; }
            r.Min = min; r.Max = max;
            r.Span = Math.Max(max - min, minSpan);
            double mid = (max + min) / 2;
            r.Lo = mid - r.Span / 2;
            return r;
        }

        public static SparkRange Range(Series s, double minSpan) => Range(new[] { s }, 1, minSpan);

        /// <summary>The `d` string of the web sparkline ("M12.3 4.5L..." with one decimal; empty with fewer than two samples). Allocates: tests and tools only.</summary>
        public static string Path(Series s, double width, double height, double minSpan)
        {
            if (s.Count < 2) return string.Empty;
            SparkRange r = Range(s, minSpan);
            var sb = new StringBuilder();
            int cap = s.Capacity;
            for (int i = 0; i < s.Count; i++)
            {
                double x = X(i, s.Count, width, cap);
                double y = Y(s[i], r.Lo, r.Span, height);
                sb.Append(i == 0 ? 'M' : 'L').Append(JsMath.ToFixed(x, 1)).Append(' ').Append(JsMath.ToFixed(y, 1));
            }
            return sb.ToString();
        }

        /// <summary>Formats a value with `digits` decimals (invariant), "—" for a gap.</summary>
        public static string Format(double v, int digits)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "—";
            return v.ToString("F" + digits, CultureInfo.InvariantCulture);
        }
    }
}
