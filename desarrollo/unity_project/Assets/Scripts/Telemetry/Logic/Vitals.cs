// Readings and Quad-X bookkeeping (web: src/shared/vitals.ts + quadx.ts; golden: vitals.json, quadx.json).
// Geometry always comes from hello.rotors (README discrepancy 12: replay 0.18 m, SITL 0.174 m); PX4_QUAD_X is only the fallback.
// Pure C#.

using System;
using System.Collections.Generic;

namespace WebGL.Telemetry
{
    public struct CrossSlot
    {
        public int motor;
        /// <summary>0 = front, 1 = rear.</summary>
        public int row;
        /// <summary>0 = left, 1 = right (seen from above, nose up).</summary>
        public int col;
    }

    public enum BatteryClass { Ok, Low, Critical }

    public static class Vitals
    {
        /// <summary>Corner of the Quad-X cross of each rotor from its FRD position.</summary>
        public static CrossSlot[] CrossLayout(IReadOnlyList<RotorGeometry> rotors)
        {
            var r = new CrossSlot[rotors.Count];
            for (int i = 0; i < rotors.Count; i++)
                r[i] = new CrossSlot { motor = rotors[i].motor, row = rotors[i].px >= 0 ? 0 : 1, col = rotors[i].py >= 0 ? 1 : 0 };
            return r;
        }

        /// <summary>Same thresholds as the alerts, so the bar and the alert never disagree.</summary>
        public static BatteryClass ClassOf(double rem, AlertThresholds th = null)
        {
            th = th ?? AlertThresholds.Default;
            if (rem <= th.batteryCritRem) return BatteryClass.Critical;
            if (rem <= th.batteryLowRem) return BatteryClass.Low;
            return BatteryClass.Ok;
        }

        public static string WireName(BatteryClass c) => c == BatteryClass.Critical ? "critical" : c == BatteryClass.Low ? "low" : "ok";

        /// <summary>Fill of the battery bar 0..1; NaN/infinite -> 0.</summary>
        public static double BatteryFill(double rem) => double.IsNaN(rem) || double.IsInfinity(rem) ? 0 : Math.Min(1, Math.Max(0, rem));
    }

    public static class QuadX
    {
        /// <summary>Default geometry (PX4 airframe 4019 / gz_x500) when a source sends none.</summary>
        public static readonly RotorGeometry[] Px4QuadX =
        {
            new RotorGeometry(1, 0.18, 0.18, 0.05),
            new RotorGeometry(2, -0.18, -0.18, 0.05),
            new RotorGeometry(3, 0.18, -0.18, -0.05),
            new RotorGeometry(4, -0.18, 0.18, -0.05),
        };

        /// <summary>+1 = CCW seen from above (km &gt;= 0), -1 = CW.</summary>
        public static int SpinSign(RotorGeometry r) => r.km >= 0 ? 1 : -1;

        /// <summary>Spanish position label (informative; the UI text comes from the shared dictionary).</summary>
        public static string RotorLabelEs(RotorGeometry r) => (r.px >= 0 ? "delantero" : "trasero") + " " + (r.py >= 0 ? "derecho" : "izquierdo");

        /// <summary>
        /// Web assignToRotors: GLB-model {x, z} of each part -> nearest PX4 motor (positions via FrdToModel). For Unity model
        /// coordinates use AssignToRotorsUnity. Throws when a one-to-one set is not one-to-one (frame mapping wrong).
        /// </summary>
        public static int[] AssignToRotors(IReadOnlyList<double[]> partsXZ, IReadOnlyList<RotorGeometry> rotors)
        {
            var targets = new List<(int motor, double x, double z)>();
            foreach (var r in rotors)
            {
                var m = NedFrames.FrdToModel(r.px, r.py, 0);
                targets.Add((r.motor, m[0], m[2]));
            }
            return Nearest(partsXZ, targets, rotors.Count);
        }

        /// <summary>
        /// Same as AssignToRotors for parts in Unity body coordinates (x = right, z = forward), the convention of NedFrames.
        /// Not covered by a golden (the web has no Unity model): WP-10 must check it against the real prefab.
        /// </summary>
        public static int[] AssignToRotorsUnity(IReadOnlyList<double[]> partsXZ, IReadOnlyList<RotorGeometry> rotors)
        {
            var targets = new List<(int motor, double x, double z)>();
            foreach (var r in rotors)
            {
                var b = NedFrames.FrdToUnityBody(r.px, r.py, 0);
                targets.Add((r.motor, b[0], b[2]));
            }
            return Nearest(partsXZ, targets, rotors.Count);
        }

        static int[] Nearest(IReadOnlyList<double[]> parts, List<(int motor, double x, double z)> targets, int rotorCount)
        {
            var result = new int[parts.Count];
            for (int p = 0; p < parts.Count; p++)
            {
                var best = targets[0];
                double bestD = double.PositiveInfinity;
                foreach (var t in targets)
                {
                    double dx = parts[p][0] - t.x, dz = parts[p][1] - t.z;
                    double d = dx * dx + dz * dz;
                    if (d < bestD) { bestD = d; best = t; }
                }
                result[p] = best.motor;
            }
            if (parts.Count == rotorCount && new HashSet<int>(result).Count != result.Length)
                throw new InvalidOperationException("rotor assignment is not one-to-one: " + string.Join(",", result));
            return result;
        }
    }
}
