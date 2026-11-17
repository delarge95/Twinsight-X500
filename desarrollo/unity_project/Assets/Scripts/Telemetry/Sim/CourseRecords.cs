// Tool 5 of Lote 1 (the test course): the scoring, the records and the ghost of the best attempt. Line-by-line port of the second half of the web's
// src/shared/course.ts (score, updateRecords, parseRecords, pushGhost, ghostPose, parseGhost). Pure and deterministic: no UnityEngine, no clock (the
// time of an attempt comes in as an argument), so it compiles for WebGL too. Tested against contracts/golden/course.json (CourseRecordsTests).
// The records JSON is the web's own (localStorage `twinsight.course.records.v1`); the desktop keeps it in persistentDataPath/twinsight/records.json.
using System;
using System.Collections.Generic;
using System.Text;
using WebGL.Telemetry.Json;

namespace WebGL.Telemetry
{
    /// <summary>The time of a lap and the same plus the penalty per collision. Before the finish it is the running time.</summary>
    public struct CourseResult
    {
        public double TimeS, PenalisedS;
        public int Collisions;
    }

    /// <summary>One finished attempt. `At` = ms since the epoch (supplied by the caller).</summary>
    public sealed class CourseAttempt
    {
        public double At, TimeS, PenalisedS;
        public int Collisions;
    }

    /// <summary>The records of one course.</summary>
    public sealed class CourseRecord
    {
        /// <summary>Best time of an attempt with 0 collisions (null = none yet).</summary>
        public double? BestClean;
        /// <summary>Best time with the penalty added, any number of collisions (null = none yet).</summary>
        public double? BestPenalised;
        /// <summary>The last attempts, newest first (at most <see cref="CourseRecords.MaxAttempts"/>).</summary>
        public readonly List<CourseAttempt> Attempts = new List<CourseAttempt>();
    }

    /// <summary>The records of every course, by course id (insertion order is kept, as in the web's object).</summary>
    public sealed class CourseRecordSet
    {
        public const int Version = 1;
        public readonly Dictionary<string, CourseRecord> Courses = new Dictionary<string, CourseRecord>(StringComparer.Ordinal);
        public CourseRecord Of(string courseId) => courseId != null && Courses.TryGetValue(courseId, out CourseRecord r) ? r : null;
    }

    public struct CourseRecordUpdate
    {
        public CourseRecordSet Records;
        /// <summary>This attempt is the new best clean time / best penalised time (the first attempt counts).</summary>
        public bool NewBestClean, NewBestPenalised;
    }

    public static class CourseRecords
    {
        /// <summary>[APROBADO 2026-11-13] Seconds added to the time for every collision (the "time with penalty").</summary>
        public const double PenaltyS = 2;
        /// <summary>Attempts kept per course.</summary>
        public const int MaxAttempts = 10;
        /// <summary>The ghost trace is sampled at this rate, Hz.</summary>
        public const int GhostHz = 10;
        /// <summary>Longest ghost trace accepted from storage (one hour at 10 Hz).</summary>
        public const int GhostMaxSamples = 36000;

        // ── scoring ──

        public static CourseResult Score(CourseRun run) => Score(run.State == CourseState.Finished, run.FinishTime, run.Elapsed, run.Collisions);

        /// <summary>[APROBADO 2026-11-13] The time of the lap and the same plus PenaltyS per collision. Before the finish it is the running time.</summary>
        public static CourseResult Score(bool finished, double finishTime, double elapsed, int collisions)
        {
            double timeS = finished ? finishTime : elapsed;
            return new CourseResult { TimeS = timeS, PenalisedS = timeS + PenaltyS * collisions, Collisions = collisions };
        }

        // ── records ──

        public static CourseRecordSet Empty() => new CourseRecordSet();

        /// <summary>Adds a finished attempt of `courseId` made at `now` (ms since the epoch, supplied by the caller). The input is not modified.</summary>
        public static CourseRecordUpdate Update(CourseRecordSet records, string courseId, CourseResult result, double now)
        {
            CourseRecord old = records.Of(courseId) ?? new CourseRecord();
            bool newBestClean = result.Collisions == 0 && (!old.BestClean.HasValue || result.TimeS < old.BestClean.Value);
            bool newBestPenalised = !old.BestPenalised.HasValue || result.PenalisedS < old.BestPenalised.Value;
            var rec = new CourseRecord
            {
                BestClean = newBestClean ? result.TimeS : old.BestClean,
                BestPenalised = newBestPenalised ? result.PenalisedS : old.BestPenalised,
            };
            rec.Attempts.Add(new CourseAttempt { At = now, TimeS = result.TimeS, PenalisedS = result.PenalisedS, Collisions = result.Collisions });
            for (int i = 0; i < old.Attempts.Count && rec.Attempts.Count < MaxAttempts; i++) rec.Attempts.Add(old.Attempts[i]);
            var next = new CourseRecordSet();
            foreach (var kv in records.Courses) next.Courses[kv.Key] = kv.Value;
            next.Courses[courseId] = rec; // an existing id keeps its place (as `{ ...courses, [id]: rec }`)
            return new CourseRecordUpdate { Records = next, NewBestClean = newBestClean, NewBestPenalised = newBestPenalised };
        }

        static bool Fin(double x) => !double.IsNaN(x) && !double.IsInfinity(x);

        /// <summary>null, or a finite number &gt;= 0.</summary>
        static bool NumOrNull(Dictionary<string, object> o, string key, out double? v)
        {
            v = null;
            if (!o.TryGetValue(key, out object x)) return false; // missing (undefined) is not null
            if (x == null) return true;
            if (!(x is double d) || !Fin(d) || d < 0) return false;
            v = d;
            return true;
        }

        static CourseAttempt AttemptOf(object raw)
        {
            var o = raw as Dictionary<string, object>;
            if (o == null) return null;
            double[] v = new double[4];
            string[] keys = { "at", "timeS", "penalisedS", "collisions" };
            for (int i = 0; i < 4; i++)
            {
                if (!o.TryGetValue(keys[i], out object x) || !(x is double d) || !Fin(d)) return null;
                v[i] = d;
            }
            if (v[3] < 0 || v[3] != Math.Floor(v[3]) || v[3] > int.MaxValue) return null;
            return new CourseAttempt { At = v[0], TimeS = v[1], PenalisedS = v[2], Collisions = (int)v[3] };
        }

        /// <summary>The records from their stored text (the web's `twinsight.course.records.v1`, the desktop's records.json). Anything invalid gives empty records (nothing is half-read).</summary>
        public static CourseRecordSet Parse(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return Empty();
            object root;
            try { root = TelemetryJson.Parse(raw); } catch { return Empty(); }
            var d = root as Dictionary<string, object>;
            if (d == null || !d.TryGetValue("version", out object ver) || !(ver is double) || (double)ver != 1) return Empty();
            if (!d.TryGetValue("courses", out object co) || !(co is Dictionary<string, object> courses)) return Empty();
            var set = new CourseRecordSet();
            foreach (var kv in courses)
            {
                var r = kv.Value as Dictionary<string, object>;
                if (r == null || !NumOrNull(r, "bestClean", out double? bestClean) || !NumOrNull(r, "bestPenalised", out double? bestPenalised)) return Empty();
                if (!r.TryGetValue("attempts", out object ao) || !(ao is List<object> attempts)) return Empty();
                var rec = new CourseRecord { BestClean = bestClean, BestPenalised = bestPenalised };
                foreach (object a in attempts)
                {
                    CourseAttempt at = AttemptOf(a);
                    if (at == null) return Empty();
                    if (rec.Attempts.Count < MaxAttempts) rec.Attempts.Add(at);
                }
                set.Courses[kv.Key] = rec;
            }
            return set;
        }

        /// <summary>The records as JSON, byte for byte what the web's JSON.stringify writes (keys in the same order, null for no record).</summary>
        public static string ToJson(CourseRecordSet records)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"version\":1,\"courses\":{");
            bool first = true;
            foreach (var kv in records.Courses)
            {
                if (!first) sb.Append(',');
                first = false;
                CourseRecord r = kv.Value;
                sb.Append(TelemetryJson.Str(kv.Key)).Append(":{\"bestClean\":").Append(Opt(r.BestClean)).Append(",\"bestPenalised\":").Append(Opt(r.BestPenalised)).Append(",\"attempts\":[");
                for (int i = 0; i < r.Attempts.Count; i++)
                {
                    CourseAttempt a = r.Attempts[i];
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"at\":").Append(TelemetryJson.Num(a.At)).Append(",\"timeS\":").Append(TelemetryJson.Num(a.TimeS))
                      .Append(",\"penalisedS\":").Append(TelemetryJson.Num(a.PenalisedS)).Append(",\"collisions\":").Append(a.Collisions.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('}');
                }
                sb.Append("]}");
            }
            sb.Append("}}");
            return sb.ToString();
        }

        static string Opt(double? v) => v.HasValue ? TelemetryJson.Num(v.Value) : "null";

        // ── the ghost: the trace of the best attempt, 10 Hz ──

        /// <summary>Appends a sample [n, e, alt, yaw] (yaw in rad, clockwise from north) when the lap time `t` has reached the next 0.1 s slot; false when it did not.</summary>
        public static bool PushGhost(List<double[]> trace, double t, double n, double e, double alt, double yaw)
        {
            if (t < trace.Count / (double)GhostHz) return false;
            trace.Add(new[] { n, e, alt, yaw });
            return true;
        }

        static double WrapPi(double a) => Math.Atan2(Math.Sin(a), Math.Cos(a));

        /// <summary>The ghost's pose [n, e, alt, yaw] at lap time `t`: linear between the two samples, the shortest way round for the yaw, held at the last sample after the end. null for an empty trace.</summary>
        public static double[] GhostPose(IReadOnlyList<double[]> trace, double t)
        {
            if (trace == null || trace.Count == 0) return null;
            double x = Math.Max(0, t) * GhostHz;
            double kf = Math.Floor(x);
            if (kf >= trace.Count - 1) return (double[])trace[trace.Count - 1].Clone();
            int k = (int)kf;
            double f = x - k;
            double[] a = trace[k], b = trace[k + 1];
            return new[] { a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f, a[2] + (b[2] - a[2]) * f, WrapPi(a[3] + WrapPi(b[3] - a[3]) * f) };
        }

        /// <summary>The trace from its JSON: anything that is not a non-empty array (at most GhostMaxSamples) of finite 4-tuples gives null.</summary>
        public static List<double[]> ParseGhost(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            object root;
            try { root = TelemetryJson.Parse(raw); } catch { return null; }
            if (!(root is List<object> l) || l.Count == 0 || l.Count > GhostMaxSamples) return null;
            var trace = new List<double[]>(l.Count);
            foreach (object s in l)
            {
                if (!(s is List<object> v) || v.Count != 4) return null;
                var p = new double[4];
                for (int i = 0; i < 4; i++)
                {
                    if (!(v[i] is double d) || !Fin(d)) return null;
                    p[i] = d;
                }
                trace.Add(p);
            }
            return trace;
        }

        /// <summary>The value kept for the ghost: every number rounded to centimetres / centiradians (as the web: Math.round(x * 100) / 100).</summary>
        public static double RoundGhost(double x) => Math.Floor(x * 100 + 0.5) / 100;

        /// <summary>The trace as JSON ([[n,e,alt,yaw],...]), rounded as the web stores it.</summary>
        public static string GhostToJson(IReadOnlyList<double[]> trace)
        {
            var sb = new StringBuilder(trace.Count * 28 + 2);
            sb.Append('[');
            for (int i = 0; i < trace.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('[');
                for (int j = 0; j < 4; j++)
                {
                    if (j > 0) sb.Append(',');
                    sb.Append(TelemetryJson.Num(RoundGhost(trace[i][j])));
                }
                sb.Append(']');
            }
            return sb.Append(']').ToString();
        }
    }
}
