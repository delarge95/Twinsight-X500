// Synthetic faults layered over the REPLAYED telemetry (web: src/shared/faults.ts; golden: faults.json). The recorded
// trajectory does not react: it is a replay and the UI must label the faults as synthetic (labels.json faultSynthetic).
// In SITL faults are real (motor_off) and are never simulated here. WP-09 uses this for the local replay.
// Pure C#.

using System;
using System.Collections.Generic;

namespace WebGL.Telemetry
{
    public static class Faults
    {
        public const double DurationMs = 30000;
        public const int MaxActive = 3;

        static bool NeedsMotor(FaultKind k) => k == FaultKind.MotorFailure || k == FaultKind.MotorOverheat;

        /// <summary>Web validateFault. kind/motor are raw values (string / double / null) as they come off the wire.</summary>
        public static bool Validate(object kind, object motor, out FaultKind k, out int? m)
        {
            m = null;
            k = FaultKind.MotorFailure;
            if (!(kind is string s) || !WireNames.TryFaultKind(s, out k)) return false;
            if (!NeedsMotor(k)) return true;
            if (!(motor is double d) || d != Math.Floor(d) || d < 1 || d > 4)
            {
                if (motor is int i && i >= 1 && i <= 4) { m = i; return true; }
                return false;
            }
            m = (int)d;
            return true;
        }

        /// <summary>Adds a fault, replacing one with the same id and dropping expired ones; keeps the newest MaxActive.</summary>
        public static List<Fault> Add(IReadOnlyList<Fault> faults, FaultKind kind, int? motor, double now)
        {
            string id = motor.HasValue && motor.Value != 0 ? WireNames.Of(kind) + ":" + motor.Value : WireNames.Of(kind);
            var next = new List<Fault>();
            foreach (var f in faults) if (f.id != id && f.until > now) next.Add(f);
            next.Add(new Fault
            {
                id = id, kind = kind, motor = motor ?? 0, HasMotor = motor.HasValue && motor.Value != 0,
                since = now, until = now + DurationMs,
            });
            if (next.Count > MaxActive) next.RemoveRange(0, next.Count - MaxActive);
            return next;
        }

        public static List<Fault> Active(IReadOnlyList<Fault> faults, double now)
        {
            var r = new List<Fault>();
            foreach (var f in faults) if (f.since <= now && f.until > now) r.Add(f);
            return r;
        }

        /// <summary>First-order ramp 0 -> 1 with time constant tau (s).</summary>
        static double Ramp(double elapsedMs, double tauS) => 1 - Math.Exp(-Math.Max(0, elapsedMs) / 1000 / tauS);

        /// <summary>Applies the active faults at `now` (default frame.ts). Returns the same instance when none is active.</summary>
        public static TelemetryFrame Apply(TelemetryFrame frame, IReadOnlyList<Fault> faults, double? nowMs = null)
        {
            double now = nowMs ?? frame.ts;
            var live = Active(faults, now);
            if (live.Count == 0) return frame;
            var o = frame.Clone();
            foreach (var f in live)
            {
                double r = Ramp(now - f.since, 1.5);
                int i = (f.HasMotor && f.motor != 0 ? f.motor : 1) - 1;
                switch (f.kind)
                {
                    case FaultKind.MotorFailure:
                        // ESC cut: aerodynamic drag stops the rotor in about a second.
                        o.rpm[i] = JsMath.Round(o.rpm[i] * (1 - Ramp(now - f.since, 0.8)));
                        o.u[i] = 0;
                        break;
                    case FaultKind.MotorOverheat:
                        o.temp[i] += 75 * Ramp(now - f.since, 6);
                        break;
                    case FaultKind.BatterySag:
                        o.bat.v -= 2.4 * r;
                        o.bat.rem = Math.Max(0, o.bat.rem - 0.3 * r);
                        o.temp[4] += 30 * Ramp(now - f.since, 8);
                        break;
                    case FaultKind.Vibration:
                        o.vib += 28 * r;
                        break;
                }
            }
            return o;
        }
    }
}
