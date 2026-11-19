// Network frames (10-50 Hz) -> smooth display (web: src/shared/interpolation.ts + ground.ts; golden: interpolation.json, ground.json).
//
// Replay: timestamped buffer + fixed render delay (PlaybackClock, 250 ms). LERP for vectors/scalars, SLERP (shortest arc) for the
//   attitude; never across a segment change; discrete fields (armed, landed, nav, seg) are held from the older sample.
// SITL: no delay; exponential approach to the newest sample (SmoothToward, k = 18 1/s).
// Pure C#, all in double (ts ~ 1.8e12 ms).

using System;
using System.Collections.Generic;

namespace WebGL.Telemetry
{
    public static class Interp
    {
        public static double Lerp(double a, double b, double t) => a + (b - a) * t;

        static double[] LerpArr(double[] a, double[] b, double t)
        {
            var r = new double[a.Length];
            for (int i = 0; i < a.Length; i++) r[i] = Lerp(a[i], b[i], t);
            return r;
        }

        /// <summary>SLERP of PX4 quaternions [w, x, y, z]; shortest arc; linear branch when dot &gt; 0.9995; result normalized.</summary>
        public static double[] SlerpQuat(double[] a, double[] b, double t)
        {
            double bw = b[0], bx = b[1], by = b[2], bz = b[3];
            double dot = a[0] * bw + a[1] * bx + a[2] * by + a[3] * bz;
            if (dot < 0)
            {
                bw = -bw; bx = -bx; by = -by; bz = -bz;
                dot = -dot;
            }
            double s0, s1;
            if (dot > 0.9995)
            {
                s0 = 1 - t;
                s1 = t;
            }
            else
            {
                double theta = Math.Acos(Math.Min(1, dot));
                double sin = Math.Sin(theta);
                s0 = Math.Sin((1 - t) * theta) / sin;
                s1 = Math.Sin(t * theta) / sin;
            }
            double w = s0 * a[0] + s1 * bw, x = s0 * a[1] + s1 * bx, y = s0 * a[2] + s1 * by, z = s0 * a[3] + s1 * bz;
            double n = Math.Sqrt(w * w + x * x + y * y + z * z);
            return new[] { w / n, x / n, y / n, z / n };
        }

        public static TelemetryFrame InterpolateFrame(TelemetryFrame a, TelemetryFrame b, double t)
        {
            return new TelemetryFrame
            {
                ts = Lerp(a.ts, b.ts, t),
                lt = Lerp(a.lt, b.lt, t),
                seg = a.seg,
                q = SlerpQuat(a.q, b.q, t),
                p = LerpArr(a.p, b.p, t),
                v = LerpArr(a.v, b.v, t),
                u = LerpArr(a.u, b.u, t),
                rpm = LerpArr(a.rpm, b.rpm, t),
                bat = new BatteryState { v = Lerp(a.bat.v, b.bat.v, t), i = Lerp(a.bat.i, b.bat.i, t), rem = Lerp(a.bat.rem, b.bat.rem, t) },
                temp = LerpArr(a.temp, b.temp, t),
                vib = Lerp(a.vib, b.vib, t),
                // Discrete state does not blend: hold the older sample.
                armed = a.armed,
                landed = a.landed,
                nav = a.nav,
                // Contract 1.2 optional state (GPS fix, ESC readings): also held from the older sample, never blended.
                gps = a.gps,
                esc = a.esc,
            };
        }

        /// <summary>Frame-rate independent smoothing factor for x += (target - x) * alpha.</summary>
        public static double ExpAlpha(double k, double dtSeconds) => 1 - Math.Exp(-k * Math.Max(0, dtSeconds));

        /// <summary>SITL: next displayed frame from the last displayed one, the newest target and dt (s).</summary>
        public static TelemetryFrame SmoothToward(TelemetryFrame current, TelemetryFrame target, double k, double dt)
        {
            if (current == null || current.seg != target.seg) return target;
            var f = InterpolateFrame(current, target, ExpAlpha(k, dt));
            f.armed = target.armed;
            f.landed = target.landed;
            f.nav = target.nav;
            f.gps = target.gps;
            f.esc = target.esc;
            f.ts = target.ts;
            return f;
        }

        /// <summary>Propeller phase integration (rad): integrate angular speed locally, wrap with the JS % semantics.</summary>
        public static double AdvancePhase(double phase, double rpm, int spin, double dt, double visualScale = 1)
        {
            double omega = rpm / 60 * 2 * Math.PI * visualScale;
            double next = phase + spin * omega * dt;
            return JsMath.Mod(next, 2 * Math.PI);
        }
    }

    public struct SampleResult
    {
        public TelemetryFrame frame;
        /// <summary>ms the render time is past the newest sample (0 inside the buffer).</summary>
        public double starvedMs;
        /// <summary>True when two samples were blended.</summary>
        public bool interpolated;
    }

    /// <summary>Timestamp-ordered buffer of frames (web InterpolationBuffer). Main thread only.</summary>
    public sealed class InterpolationBuffer
    {
        readonly List<TelemetryFrame> items = new List<TelemetryFrame>();
        readonly int capacity;

        public InterpolationBuffer(int capacity = 512) { this.capacity = capacity; }

        public int Size => items.Count;
        public TelemetryFrame Newest => items.Count > 0 ? items[items.Count - 1] : null;
        public TelemetryFrame Oldest => items.Count > 0 ? items[0] : null;

        public void Clear() => items.Clear();

        /// <summary>Inserts keeping ts order; a duplicate ts replaces the old sample. Out-of-order frames are fine.</summary>
        public void Push(TelemetryFrame f)
        {
            if (items.Count == 0 || f.ts > items[items.Count - 1].ts) items.Add(f);
            else
            {
                int i = LowerBound(f.ts);
                if (i < items.Count && items[i].ts == f.ts) items[i] = f;
                else items.Insert(i, f);
            }
            if (items.Count > capacity) items.RemoveRange(0, items.Count - capacity);
        }

        /// <summary>Drops samples older than ts but keeps one before it so the next Sample() can blend.</summary>
        public void Prune(double ts)
        {
            int i = LowerBound(ts) - 1;
            if (i > 0) items.RemoveRange(0, i);
        }

        /// <summary>State at server time t; null when empty.</summary>
        public SampleResult? Sample(double t)
        {
            if (items.Count == 0) return null;
            var first = items[0];
            var last = items[items.Count - 1];
            if (t <= first.ts) return new SampleResult { frame = first, starvedMs = 0, interpolated = false };
            if (t >= last.ts) return new SampleResult { frame = last, starvedMs = t - last.ts, interpolated = false };
            int j = LowerBound(t); // items[j].ts >= t > items[j-1].ts
            var a = items[j - 1];
            var b = items[j];
            if (a.seg != b.seg) return new SampleResult { frame = a, starvedMs = 0, interpolated = false };
            double alpha = (t - a.ts) / (b.ts - a.ts);
            return new SampleResult { frame = Interp.InterpolateFrame(a, b, alpha), starvedMs = 0, interpolated = true };
        }

        int LowerBound(double ts)
        {
            int lo = 0, hi = items.Count;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (items[mid].ts < ts) lo = mid + 1;
                else hi = mid;
            }
            return lo;
        }
    }

    /// <summary>
    /// Maps server stream time onto the local clock (web PlaybackClock). offset = local - server, tracked as the lowest
    /// observed value and allowed to creep up slowly (drift / route changes).
    /// </summary>
    public sealed class PlaybackClock
    {
        double? offset;
        public double DelayMs;
        readonly double creep;

        public PlaybackClock(double delayMs = 250, double creep = 0.002) { DelayMs = delayMs; this.creep = creep; }

        public void Observe(double serverTs, double localNow)
        {
            double sample = localNow - serverTs;
            if (!offset.HasValue || sample < offset.Value) offset = sample;
            else offset = offset.Value + (sample - offset.Value) * creep;
        }

        public void Reset() => offset = null;
        public bool Synced => offset.HasValue;

        /// <summary>Server time to render at local time localNow; NaN while not synced.</summary>
        public double RenderTime(double localNow) => offset.HasValue ? localNow - offset.Value - DelayMs : double.NaN;
    }

    /// <summary>
    /// Ground reference (web ground.ts): while landed the EKF height wanders; track it as a bias (low-pass, tau 2 s), draw z = 0,
    /// freeze the bias in flight, reset on a segment change.
    /// </summary>
    public sealed class GroundReference
    {
        public const double TauMs = 2000;
        double? bias;
        double lastTs;
        int? seg;

        public void Reset()
        {
            bias = null;
            seg = null;
        }

        /// <summary>Returns a copy of f with the drawn height in p[2].</summary>
        public TelemetryFrame Apply(TelemetryFrame f)
        {
            if (seg.HasValue && f.seg != seg.Value) Reset();
            seg = f.seg;
            double z;
            if (f.landed)
            {
                if (!bias.HasValue) bias = f.p[2];
                else bias = bias.Value + (f.p[2] - bias.Value) * (1 - Math.Exp(-Math.Max(0, f.ts - lastTs) / TauMs));
                z = 0;
            }
            else
            {
                z = f.p[2] - (bias ?? 0); // no landed sample yet (joined mid-flight): raw height
            }
            lastTs = f.ts;
            var o = f.Clone();
            o.p = new[] { f.p[0], f.p[1], z };
            return o;
        }
    }

    /// <summary>Reconnection policy (web reconnect.ts): 0.5 s doubling up to 15 s; 3 quiet failures before "offline".</summary>
    public static class ReconnectPolicy
    {
        public const int RetryBaseMs = 500;
        public const int RetryMaxMs = 15000;
        public const int QuietFailures = 3;
        public const int LogEvery = 10;

        public static int RetryDelayMs(int failures) => RetryDelayMs((double)failures);

        /// <summary>Web retryDelayMs(failures): floor, at least 1, 500 * 2^(n-1) capped at 15 s.</summary>
        public static int RetryDelayMs(double failures)
        {
            double n = Math.Max(1, Math.Floor(failures));
            double d = RetryBaseMs * Math.Pow(2, n - 1);
            return d >= RetryMaxMs ? RetryMaxMs : (int)d;
        }

        /// <summary>true = still "connecting" (quiet), false = "closed" (offline, shown and logged).</summary>
        public static bool IsQuietFailure(int failures, bool everOpened) => !everOpened && failures <= QuietFailures;

        /// <summary>Web linkStateAfterFailure: "connecting" or "closed".</summary>
        public static string LinkStateAfterFailure(int failures, bool everOpened) => IsQuietFailure(failures, everOpened) ? "connecting" : "closed";

        public static bool ShouldLogFailure(int failures, bool everOpened)
        {
            if (everOpened) return failures == 1 || (failures > 1 && (failures - 1) % LogEvery == 0);
            if (failures <= QuietFailures) return false;
            int loud = failures - QuietFailures;
            return loud == 1 || loud % LogEvery == 1;
        }
    }
}
