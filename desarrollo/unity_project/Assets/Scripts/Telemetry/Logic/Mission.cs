// Lote 1c, tool 2: the mission planner. Line-by-line port of the web's src/shared/mission.ts: the mission document, its validation and the runner that flies it
// with the commands that already exist (takeoff, fly_to, hold, land, rtl). Nothing new travels on the wire: the CLIENT sends the sequence, and the speed per leg
// comes from a "rabbit" destination that moves along the leg at the requested speed (fly_to has no speed). Pure and deterministic: no UnityEngine, no clock, no
// randomness; the time (s) comes in as an argument. Tested against contracts/golden/mission.json (MissionTests).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WebGL.Telemetry.Json;

namespace WebGL.Telemetry
{
    public sealed class Waypoint
    {
        /// <summary>Local NED north / east of the takeoff point (EKF origin), m.</summary>
        public double n, e;
        /// <summary>Height above the origin, m (positive up).</summary>
        public double alt;
        /// <summary>Requested horizontal speed of the leg that ENDS here, m/s.</summary>
        public double speed;
        /// <summary>Wait at the waypoint, s (null = none).</summary>
        public double? holdS;

        public Waypoint() { }
        public Waypoint(double n, double e, double alt = 5, double speed = 3, double? holdS = null) { this.n = n; this.e = e; this.alt = alt; this.speed = speed; this.holdS = holdS; }
        public Waypoint Clone() => new Waypoint(n, e, alt, speed, holdS);
    }

    /// <summary>The mission document: the same JSON in both clients (export / import; the web keeps it in localStorage `twinsight.mission.v1`).</summary>
    public sealed class MissionDoc
    {
        public const int Version = 1;
        public string id = "mision-1";
        public string name = "Misión";
        public List<Waypoint> waypoints = new List<Waypoint>();
        /// <summary>hold | land | rtl (anything else fails the validation).</summary>
        public string finish = "hold";

        public MissionDoc Clone()
        {
            var m = new MissionDoc { id = id, name = name, finish = finish };
            foreach (Waypoint w in waypoints) m.waypoints.Add(w.Clone());
            return m;
        }
    }

    /// <summary>Where the waypoints may be. Simulator: its geofence (cfg.radius / cfg.ceiling). SITL: the clamps of the bridge (200 m, 1..50 m).</summary>
    public struct MissionLimits
    {
        public double radius, minAlt, maxAlt;
        public MissionLimits(double radius, double minAlt, double maxAlt) { this.radius = radius; this.minAlt = minAlt; this.maxAlt = maxAlt; }
    }

    public sealed class MissionIssue
    {
        /// <summary>count | not_finite | radius | altitude | speed | hold | finish</summary>
        public string code;
        /// <summary>Waypoint index (0-based); null for the whole mission.</summary>
        public int? index;
        public string detail;
    }

    public enum MissionPhase { Idle, Takeoff, Leg, Hold, Finish, Done, Paused, Failed }
    public enum MissionControl { Start, Pause, Resume, Abort, AbortRtl }

    /// <summary>What the runner reads from a frame.</summary>
    public struct MissionFrame
    {
        public double n, e, d;
        public bool landed, armed;
        public MissionFrame(double n, double e, double d, bool landed, bool armed) { this.n = n; this.e = e; this.d = d; this.landed = landed; this.armed = armed; }
        public static MissionFrame? Of(TelemetryFrame f) => f == null ? (MissionFrame?)null : new MissionFrame(f.p[0], f.p[1], f.p[2], f.landed, f.armed);
        public double Alt => -d;
    }

    public sealed class MissionRun
    {
        public MissionDoc mission;
        public MissionPhase phase = MissionPhase.Idle;
        /// <summary>Waypoint the vehicle is flying to (leg i ends at waypoint i) or waiting at.</summary>
        public int i;
        /// <summary>Start of the current phase (s); for a leg, of the rabbit.</summary>
        public double t0;
        /// <summary>Start of the current leg: north, east (m).</summary>
        public double startN, startE;
        /// <summary>When the last rabbit was sent (s); -Infinity = never.</summary>
        public double lastSend = double.NegativeInfinity;
        /// <summary>Deadline of the current takeoff / leg (s, absolute).</summary>
        public double deadline = double.PositiveInfinity;
        /// <summary>Remaining wait when a hold was paused (s).</summary>
        public double holdLeft;
        /// <summary>Phase to resume when paused (Takeoff, Leg or Hold), or null.</summary>
        public MissionPhase? resume;
        /// <summary>Why it paused or failed (Spanish, as the web shows it; the interface translates the known ones).</summary>
        public string reason;

        public MissionRun Copy() => (MissionRun)MemberwiseClone();
    }

    public sealed class MissionStepResult
    {
        public MissionRun run;
        public readonly List<CommandMessage> send = new List<CommandMessage>(1);
        public MissionStepResult(MissionRun run, CommandMessage c = null) { this.run = run; if (c != null) send.Add(c); }
    }

    public struct MissionProgress
    {
        /// <summary>1-based leg number and the total.</summary>
        public int leg, legs;
        /// <summary>Horizontal distance left to fly, current leg plus the rest, m.</summary>
        public double metres;
        /// <summary>Estimate of the time left: distances / speeds plus the waits, s.</summary>
        public double seconds;
    }

    public static class Mission
    {
        public const int MaxWaypoints = 50;
        public const double SpeedMin = 0.5;
        /// <summary>SimLimits.MaxSpeed</summary>
        public const double SpeedMax = 6;
        public const double HoldMax = 120;
        /// <summary>Arrival: horizontal distance under this, m ...</summary>
        public const double ArriveH = 1.0;
        /// <summary>... and altitude error under this, m.</summary>
        public const double ArriveV = 0.5;
        /// <summary>The rabbit moves at most this often, s (2 Hz).</summary>
        public const double RabbitPeriod = 0.5;
        /// <summary>Lead of the rabbit over the ideal point, as v / LeadDiv (the simulator flies at min(maxSpeed, d · 1.1) toward its target).</summary>
        public const double LeadDiv = 1.1;
        /// <summary>Deadline of a leg: LegFactor · L / v + LegSlack, s.</summary>
        public const double LegFactor = 2, LegSlack = 20;
        /// <summary>Deadline of the takeoff: alt / TakeoffRate + LegSlack, s.</summary>
        public const double TakeoffRate = 1;

        public static readonly MissionLimits SitlLimits = new MissionLimits(200, 1, 50);
        public static MissionLimits SimLimitsOf(double radius, double ceiling) => new MissionLimits(radius, 1, ceiling);

        public const string ReasonTimeout = "no llegó";
        public const string ReasonLanded = "el dron aterrizó";
        public const string ReasonFocus = "se perdió el foco";
        public const string ReasonUser = "por el usuario";

        static bool Fin(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        static double Hyp(double a, double b) => Math.Sqrt(a * a + b * b);
        static string Num(double x) => x.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>Every problem of the mission against the limits (empty = it can fly). Order: mission issues first, then per waypoint, field by field.</summary>
        public static List<MissionIssue> Validate(MissionDoc m, MissionLimits limits)
        {
            var o = new List<MissionIssue>();
            int count = m.waypoints.Count;
            if (count < 1 || count > MaxWaypoints) o.Add(new MissionIssue { code = "count", detail = "la misión necesita de 1 a " + MaxWaypoints + " waypoints (tiene " + count + ")" });
            if (m.finish != "hold" && m.finish != "land" && m.finish != "rtl") o.Add(new MissionIssue { code = "finish", detail = "acción final no válida" });
            for (int index = 0; index < count; index++)
            {
                Waypoint w = m.waypoints[index];
                int n = index + 1;
                if (!Fin(w.n) || !Fin(w.e) || !Fin(w.alt) || !Fin(w.speed) || (w.holdS.HasValue && !Fin(w.holdS.Value)))
                {
                    o.Add(new MissionIssue { code = "not_finite", index = index, detail = "waypoint " + n + ": valores no válidos" });
                    continue;
                }
                if (Hyp(w.n, w.e) > limits.radius + 1e-9) o.Add(new MissionIssue { code = "radius", index = index, detail = "waypoint " + n + ": fuera de la geocerca (" + Num(limits.radius) + " m del origen)" });
                if (w.alt < limits.minAlt - 1e-9 || w.alt > limits.maxAlt + 1e-9) o.Add(new MissionIssue { code = "altitude", index = index, detail = "waypoint " + n + ": la altura debe estar entre " + Num(limits.minAlt) + " y " + Num(limits.maxAlt) + " m" });
                if (w.speed < SpeedMin - 1e-9 || w.speed > SpeedMax + 1e-9) o.Add(new MissionIssue { code = "speed", index = index, detail = "waypoint " + n + ": la velocidad debe estar entre " + Num(SpeedMin) + " y " + Num(SpeedMax) + " m/s" });
                double h = w.holdS ?? 0;
                if (h < 0 || h > HoldMax + 1e-9) o.Add(new MissionIssue { code = "hold", index = index, detail = "waypoint " + n + ": la espera debe estar entre 0 y " + Num(HoldMax) + " s" });
            }
            return o;
        }

        /// <summary>Reads a mission document (import, storage). Anything that is not a version-1 mission gives null; the fields are copied, not trusted.</summary>
        public static MissionDoc Parse(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            object v;
            try { v = TelemetryJson.Parse(raw); } catch { return null; }
            var o = v as Dictionary<string, object>;
            if (o == null) return null;
            if (!o.TryGetValue("version", out object ver) || !(ver is double) || (double)ver != 1) return null;
            if (!o.TryGetValue("waypoints", out object wo) || !(wo is List<object> list) || list.Count > MaxWaypoints) return null;
            string finish;
            if (!o.TryGetValue("finish", out object fo) || fo == null && !o.ContainsKey("finish")) finish = "hold";
            else if (fo is string fs && (fs == "land" || fs == "rtl" || fs == "hold")) finish = fs;
            else return null;
            var m = new MissionDoc { finish = finish };
            foreach (object item in list)
            {
                var r = item as Dictionary<string, object>;
                if (r == null) return null;
                if (!NumOf(r, "n", out double n) || !NumOf(r, "e", out double e) || !NumOf(r, "alt", out double alt) || !NumOf(r, "speed", out double speed)) return null;
                var wp = new Waypoint(n, e, alt, speed);
                if (r.ContainsKey("holdS"))
                {
                    if (!NumOf(r, "holdS", out double h)) return null;
                    if (h > 0) wp.holdS = h;
                }
                m.waypoints.Add(wp);
            }
            m.id = o.TryGetValue("id", out object id) && id is string ids ? ids : "mision-1";
            m.name = o.TryGetValue("name", out object nm) && nm is string nms ? nms : "Misión";
            return m;
        }

        static bool NumOf(Dictionary<string, object> r, string k, out double x)
        {
            x = 0;
            if (!r.TryGetValue(k, out object v) || !(v is double)) return false;
            x = (double)v;
            return Fin(x);
        }

        /// <summary>The document as JSON (same keys and order as the web's JSON.stringify of a Mission).</summary>
        public static string ToJson(MissionDoc m)
        {
            var sb = new StringBuilder(64 + m.waypoints.Count * 48);
            sb.Append("{\"version\":1,\"id\":").Append(Quote(m.id)).Append(",\"name\":").Append(Quote(m.name)).Append(",\"waypoints\":[");
            for (int k = 0; k < m.waypoints.Count; k++)
            {
                Waypoint w = m.waypoints[k];
                if (k > 0) sb.Append(',');
                sb.Append("{\"n\":").Append(Num(w.n)).Append(",\"e\":").Append(Num(w.e)).Append(",\"alt\":").Append(Num(w.alt)).Append(",\"speed\":").Append(Num(w.speed));
                if (w.holdS.HasValue && w.holdS.Value > 0) sb.Append(",\"holdS\":").Append(Num(w.holdS.Value));
                sb.Append('}');
            }
            sb.Append("],\"finish\":").Append(Quote(m.finish)).Append('}');
            return sb.ToString();
        }

        static string Quote(string s)
        {
            var sb = new StringBuilder((s ?? string.Empty).Length + 2);
            sb.Append('"');
            foreach (char c in s ?? string.Empty)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\t') sb.Append("\\t");
                else if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else sb.Append(c);
            }
            sb.Append('"');
            return sb.ToString();
        }

        // ---------------------------------------------------------------- runner

        public static MissionRun CreateRun(MissionDoc mission) => new MissionRun { mission = mission };

        /// <summary>The runner is flying the mission (it sends commands and reacts to acks).</summary>
        public static bool IsActive(MissionRun r) => r.phase == MissionPhase.Takeoff || r.phase == MissionPhase.Leg || r.phase == MissionPhase.Hold;

        static CommandMessage Rabbit(MissionRun r, double now)
        {
            Waypoint w = r.mission.waypoints[r.i];
            double dn = w.n - r.startN, de = w.e - r.startE;
            double L = Hyp(dn, de);
            double s = Math.Min(L, w.speed * Math.Max(0, now - r.t0) + w.speed / LeadDiv);
            if (L < 1e-9 || s >= L) return ClientMessage.FlyTo(w.n, w.e, w.alt);
            return ClientMessage.FlyTo(r.startN + (dn / L) * s, r.startE + (de / L) * s, w.alt);
        }

        static MissionStepResult BeginLeg(MissionRun r, int i, MissionFrame f, double now)
        {
            Waypoint w = r.mission.waypoints[i];
            double L = Hyp(w.n - f.n, w.e - f.e);
            MissionRun next = r.Copy();
            next.phase = MissionPhase.Leg; next.i = i; next.t0 = now; next.startN = f.n; next.startE = f.e; next.lastSend = now; next.holdLeft = 0; next.resume = null; next.reason = null;
            next.deadline = now + (LegFactor * L) / w.speed + LegSlack;
            return new MissionStepResult(next, Rabbit(next, now));
        }

        static MissionStepResult BeginTakeoff(MissionRun r, double now)
        {
            double a = r.mission.waypoints[0].alt;
            MissionRun next = r.Copy();
            next.phase = MissionPhase.Takeoff; next.i = 0; next.t0 = now; next.lastSend = now; next.holdLeft = 0; next.resume = null; next.reason = null;
            next.deadline = now + a / TakeoffRate + LegSlack;
            return new MissionStepResult(next, ClientMessage.Takeoff(a));
        }

        static MissionStepResult AfterWaypoint(MissionRun r, MissionFrame f, double now)
        {
            if (r.i + 1 < r.mission.waypoints.Count) return BeginLeg(r, r.i + 1, f, now);
            // finish: the final action goes out once and the run is done (the transient phase Finish is never left standing)
            MissionRun next = r.Copy();
            next.phase = MissionPhase.Done; next.t0 = now; next.deadline = double.PositiveInfinity; next.resume = null;
            return new MissionStepResult(next, new CommandMessage { name = r.mission.finish });
        }

        static MissionStepResult Fail(MissionRun r, string reason, double now)
        {
            MissionRun next = r.Copy();
            next.phase = MissionPhase.Failed; next.t0 = now; next.deadline = double.PositiveInfinity; next.resume = null; next.reason = reason;
            return new MissionStepResult(next, ClientMessage.Hold());
        }

        /// <summary>
        /// One tick of the runner. Call it as often as frames arrive (it limits the rabbit to 2 Hz itself). It does nothing outside takeoff / leg / hold or
        /// without a frame.
        /// </summary>
        public static MissionStepResult Step(MissionRun r, MissionFrame? frame, double now)
        {
            if (!frame.HasValue || !IsActive(r)) return new MissionStepResult(r);
            MissionFrame f = frame.Value;
            if (r.phase == MissionPhase.Takeoff)
            {
                double target = r.mission.waypoints[0].alt;
                if (!f.landed && Math.Abs(f.Alt - target) < ArriveV) return BeginLeg(r, 0, f, now);
                if (now > r.deadline) return Fail(r, ReasonTimeout, now);
                return new MissionStepResult(r);
            }
            if (f.landed) return Fail(r, ReasonLanded, now);
            Waypoint w = r.mission.waypoints[r.i];
            if (r.phase == MissionPhase.Hold)
            {
                if (now - r.t0 >= r.holdLeft - 1e-9) return AfterWaypoint(r, f, now);
                return new MissionStepResult(r);
            }
            // leg
            if (Hyp(w.n - f.n, w.e - f.e) < ArriveH && Math.Abs(f.Alt - w.alt) < ArriveV)
            {
                double h = w.holdS ?? 0;
                // the wait is flown on the waypoint itself: one exact fly_to, whatever the last rabbit was
                if (h > 0)
                {
                    MissionRun hold = r.Copy();
                    hold.phase = MissionPhase.Hold; hold.t0 = now; hold.holdLeft = h; hold.deadline = double.PositiveInfinity; hold.lastSend = now;
                    return new MissionStepResult(hold, ClientMessage.FlyTo(w.n, w.e, w.alt));
                }
                return AfterWaypoint(r, f, now);
            }
            if (now > r.deadline) return Fail(r, ReasonTimeout, now);
            if (now - r.lastSend >= RabbitPeriod - 1e-9)
            {
                MissionRun next = r.Copy();
                next.lastSend = now;
                return new MissionStepResult(next, Rabbit(next, now));
            }
            return new MissionStepResult(r);
        }

        /// <summary>
        /// The buttons. Start: from idle / done / failed (takeoff when on the ground, else straight to leg 1). Pause: hold, remembering the phase (a paused
        /// hold keeps its remaining wait). Resume: the current leg again from where the vehicle is (new start, new deadline), or a new takeoff if it is on
        /// the ground. Abort / AbortRtl: hold / rtl and back to idle.
        /// </summary>
        public static MissionStepResult Control(MissionRun r, MissionControl action, MissionFrame? frame, double now, string reason = ReasonUser)
        {
            switch (action)
            {
                case MissionControl.Start:
                    if (!frame.HasValue || r.mission.waypoints.Count == 0 || IsActive(r) || r.phase == MissionPhase.Paused) return new MissionStepResult(r);
                    return frame.Value.landed || !frame.Value.armed ? BeginTakeoff(r, now) : BeginLeg(r, 0, frame.Value, now);
                case MissionControl.Pause:
                {
                    if (!IsActive(r)) return new MissionStepResult(r);
                    MissionRun p = r.Copy();
                    p.holdLeft = r.phase == MissionPhase.Hold ? Math.Max(0, r.holdLeft - (now - r.t0)) : 0;
                    p.resume = r.phase; p.phase = MissionPhase.Paused; p.t0 = now; p.deadline = double.PositiveInfinity; p.reason = reason;
                    return new MissionStepResult(p, ClientMessage.Hold());
                }
                case MissionControl.Resume:
                {
                    if (r.phase != MissionPhase.Paused || !frame.HasValue) return new MissionStepResult(r);
                    MissionFrame f = frame.Value;
                    if (f.landed || !f.armed) { MissionRun g = r.Copy(); g.i = 0; return BeginTakeoff(g, now); }
                    if (r.resume == MissionPhase.Hold)
                    {
                        MissionRun h = r.Copy();
                        h.phase = MissionPhase.Hold; h.t0 = now; h.resume = null; h.reason = null;
                        return new MissionStepResult(h);
                    }
                    return BeginLeg(r, r.resume == MissionPhase.Takeoff ? 0 : r.i, f, now);
                }
                default:
                    if (r.phase == MissionPhase.Idle) return new MissionStepResult(r);
                    return new MissionStepResult(CreateRun(r.mission), action == MissionControl.AbortRtl ? ClientMessage.Rtl() : ClientMessage.Hold());
            }
        }

        static bool IsMissionCommand(string c) => c == "takeoff" || c == "fly_to" || c == "hold" || c == "land" || c == "rtl";

        /// <summary>A negative ack of a command the mission sends pauses the active run with the ack's text (and a hold).</summary>
        public static MissionStepResult Ack(MissionRun r, string command, bool ok, string detail, double now)
        {
            if (ok || !IsActive(r) || !IsMissionCommand(command)) return new MissionStepResult(r);
            return Control(r, MissionControl.Pause, null, now, !string.IsNullOrEmpty(detail) ? detail : command + " rechazado");
        }

        public static MissionProgress Progress(MissionRun r, MissionFrame? f)
        {
            List<Waypoint> wps = r.mission.waypoints;
            int legs = wps.Count;
            if (legs == 0) return new MissionProgress();
            if (r.phase == MissionPhase.Done) return new MissionProgress { leg = legs, legs = legs };
            int from = r.phase == MissionPhase.Idle ? 0 : Math.Min(r.i, legs - 1);
            double pn = f.HasValue ? f.Value.n : r.startN, pe = f.HasValue ? f.Value.e : r.startE;
            double metres = 0, seconds = 0;
            for (int k = from; k < legs; k++)
            {
                double an = k == from ? pn : wps[k - 1].n, ae = k == from ? pe : wps[k - 1].e;
                bool inHold = k == from && (r.phase == MissionPhase.Hold || (r.phase == MissionPhase.Paused && r.resume == MissionPhase.Hold));
                double d = inHold ? 0 : Hyp(wps[k].n - an, wps[k].e - ae);
                metres += d;
                seconds += d / wps[k].speed + (inHold ? r.holdLeft : (wps[k].holdS ?? 0));
            }
            return new MissionProgress { leg = from + 1, legs = legs, metres = metres, seconds = seconds };
        }

        public static string Name(MissionPhase p)
        {
            switch (p)
            {
                case MissionPhase.Takeoff: return "takeoff";
                case MissionPhase.Leg: return "leg";
                case MissionPhase.Hold: return "hold";
                case MissionPhase.Finish: return "finish";
                case MissionPhase.Done: return "done";
                case MissionPhase.Paused: return "paused";
                case MissionPhase.Failed: return "failed";
                default: return "idle";
            }
        }
    }
}
