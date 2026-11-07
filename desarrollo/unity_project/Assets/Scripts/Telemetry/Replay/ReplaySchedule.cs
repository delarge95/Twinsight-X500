// 24/7 replay schedule (web: src/shared/replay.ts; golden: replay.json). Playback position is a pure function of wall-clock time:
// global frame g plays at epoch + g * 1000 / rate; loop = floor(g / N) (= seg); row k = g mod N. No pause, no seek (P-3).
// Skeleton for WP-09: the local replay source will call FramesBetween on a timer with the bundled TWF and apply the
// synthetic faults (Faults.Apply). Pure C#.

using System;
using System.Collections.Generic;

namespace WebGL.Telemetry
{
    public sealed class ReplaySchedule
    {
        /// <summary>Date.UTC(2026, 9, 17) = 2026-10-17T00:00:00Z.</summary>
        public const double ReplayEpochMs = 1792195200000;

        public readonly int FrameCount;
        public readonly double Rate;
        public readonly double PeriodMs;
        public readonly double EpochMs;
        readonly TwfFile file;
        readonly FlightColumns cols;

        public ReplaySchedule(TwfFile file, double epochMs = ReplayEpochMs)
        {
            this.file = file;
            EpochMs = epochMs;
            FrameCount = file.Header.frame_count;
            Rate = file.Header.rate_hz;
            PeriodMs = 1000 / Rate;
            cols = new FlightColumns(file);
        }

        /// <summary>Schedule arithmetic without a file (for tests and for estimating times before the TWF is loaded).</summary>
        public static double GlobalFrameAt(double nowMs, double epochMs, double periodMs) => Math.Floor((nowMs - epochMs) / periodMs);

        public double GlobalFrameAt(double nowMs) => GlobalFrameAt(nowMs, EpochMs, PeriodMs);

        public double PlayTime(double g) => EpochMs + g * PeriodMs;

        public TelemetryFrame Frame(double g)
        {
            double loop = Math.Floor(g / FrameCount);
            int k = (int)(g - loop * FrameCount);
            return cols.FrameFromRow(file, k, JsMath.Round(PlayTime(g)), (int)loop);
        }

        /// <summary>Frames with play time in (fromMs, toMs], capped to the newest `max`.</summary>
        public List<TelemetryFrame> FramesBetween(double fromMs, double toMs, int max = 40)
        {
            double last = GlobalFrameAt(toMs);
            double first = Math.Max(GlobalFrameAt(fromMs) + 1, last - max + 1);
            var r = new List<TelemetryFrame>();
            for (double g = first; g <= last; g++) r.Add(Frame(g));
            return r;
        }

        /// <summary>Unix epoch ms of the current wall clock (what the worker uses as "now").</summary>
        public static double WallClockMs() => (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
    }
}
