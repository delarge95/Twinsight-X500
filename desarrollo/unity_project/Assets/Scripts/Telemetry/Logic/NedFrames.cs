// Coordinate frames (web: src/shared/frames.ts; golden: contracts/golden/frames.json).
//
//   PX4 world: NED (x North, y East, z Down)           PX4 body: FRD (x Forward, y Right, z Down)
//   Unity world: left-handed, Y up. Mapping used here: x = East, y = Up, z = North  ->  unity = (E, -D, N).
//   Unity body: x = Right, y = Up, z = Forward          ->  body_unity = (R, -D, F).
//
// Both maps are the same permutation P(a, b, c) = (b, -c, a) with det(P) = -1 (a reflection, which is exactly what turns the
// right-handed NED/FRD pair into Unity's left-handed convention). A PX4 attitude R (FRD -> NED) therefore becomes the Unity
// rotation R_u = P R P^-1, a proper rotation whose quaternion is
//      q_unity = (w, -P(x, y, z)) = (x: -y, y: z, z: -x, w: w)      for q_px4 = [w, x, y, z]
// (conjugating by a reflection reverses the angle). The golden file is checked through body_axes_in_unity_world, which does
// not depend on how the imported model is oriented. A model whose nose is not +Z needs an extra fixed model->body rotation
// applied on the right (WP-10): world = q_unity * modelToBody.
//
// The Three.js ("render") variants reproduce the web exactly so the other golden fields can be compared too.
// Math in double; ToUnity* helpers give UnityEngine types for the scene.

using System;
using UnityEngine;

namespace WebGL.Telemetry
{
    /// <summary>Double-precision quaternion (x, y, z, w) for the pure logic and the golden comparisons.</summary>
    public struct DQuat
    {
        public double x, y, z, w;
        public DQuat(double x, double y, double z, double w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static DQuat Identity => new DQuat(0, 0, 0, 1);

        public static DQuat operator *(DQuat a, DQuat b) => new DQuat(
            a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
            a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
            a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
            a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);

        public double Norm => Math.Sqrt(x * x + y * y + z * z + w * w);

        public DQuat Normalized()
        {
            double n = Norm;
            return n > 0 ? new DQuat(x / n, y / n, z / n, w / n) : Identity;
        }

        public static double Dot(DQuat a, DQuat b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;

        /// <summary>Rotates a vector (q v q*), valid in either handedness as long as q and v use the same one.</summary>
        public double[] Rotate(double vx, double vy, double vz)
        {
            // t = 2 * cross(q.xyz, v); v' = v + w t + cross(q.xyz, t)
            double tx = 2 * (y * vz - z * vy);
            double ty = 2 * (z * vx - x * vz);
            double tz = 2 * (x * vy - y * vx);
            return new[]
            {
                vx + w * tx + (y * tz - z * ty),
                vy + w * ty + (z * tx - x * tz),
                vz + w * tz + (x * ty - y * tx),
            };
        }

        public Quaternion ToUnity() => new Quaternion((float)x, (float)y, (float)z, (float)w);
    }

    public static class NedFrames
    {
        // ------------------------------------------------------------------------------------------------- Unity

        /// <summary>NED vector -> Unity world (x = East, y = Up, z = North).</summary>
        public static double[] NedToUnity(double n, double e, double d) => new[] { e, -d, n };
        public static double[] NedToUnity(double[] ned) => NedToUnity(ned[0], ned[1], ned[2]);
        public static Vector3 NedToUnityVector(double[] ned) => new Vector3((float)ned[1], (float)-ned[2], (float)ned[0]);

        /// <summary>Unity world -> NED (inverse of NedToUnity).</summary>
        public static double[] UnityToNed(double x, double y, double z) => new[] { z, x, -y };

        /// <summary>Body FRD vector -> Unity body (x = Right, y = Up, z = Forward).</summary>
        public static double[] FrdToUnityBody(double f, double r, double d) => new[] { r, -d, f };

        /// <summary>PX4 attitude [w, x, y, z] (FRD -> NED) -> Unity world rotation of the body (Unity body axes, see header).</summary>
        public static DQuat Px4AttitudeToUnity(double[] q)
        {
            var n = new DQuat(q[1], q[2], q[3], q[0]).Normalized();
            return new DQuat(-n.y, n.z, -n.x, n.w);
        }

        public static Quaternion Px4AttitudeToUnityQuaternion(double[] q) => Px4AttitudeToUnity(q).ToUnity();

        /// <summary>Forward, right and down body axes expressed in the Unity world (compare with golden body_axes_in_unity_world).</summary>
        public static void BodyAxesInUnity(double[] qPx4, out double[] forward, out double[] right, out double[] down)
        {
            var q = Px4AttitudeToUnity(qPx4);
            forward = q.Rotate(0, 0, 1);
            right = q.Rotate(1, 0, 0);
            down = q.Rotate(0, -1, 0);
        }

        // --------------------------------------------------------------------------------- Three.js (web parity)

        /// <summary>NED -> Three.js world (x = East, y = Up, z = South).</summary>
        public static double[] NedToRender(double[] ned) => new[] { ned[1], -ned[2], -ned[0] };

        // Quaternions of the web's NED_TO_THREE and MODEL_TO_FRD matrices (setFromRotationMatrix). Both matrices have det = +1.
        static readonly DQuat QW = FromRotationMatrix(0, 1, 0, 0, 0, -1, -1, 0, 0);
        static readonly DQuat QB = FromRotationMatrix(-1, 0, 0, 0, 0, -1, 0, -1, 0);

        /// <summary>PX4 attitude -> the GLB model's quaternion in the Three.js world (web px4AttitudeToThree).</summary>
        public static DQuat Px4AttitudeToRender(double[] q)
        {
            var t = new DQuat(q[1], q[2], q[3], q[0]).Normalized();
            return QW * t * QB;
        }

        /// <summary>Body axes of the PX4 attitude in the Three.js world (forward/right/down of FRD mapped through NED_TO_THREE).</summary>
        public static void BodyAxesInRender(double[] qPx4, out double[] forward, out double[] right, out double[] down)
        {
            var t = new DQuat(qPx4[1], qPx4[2], qPx4[3], qPx4[0]).Normalized();
            forward = NedToRender(t.Rotate(1, 0, 0));
            right = NedToRender(t.Rotate(0, 1, 0));
            down = NedToRender(t.Rotate(0, 0, 1));
        }

        /// <summary>Body FRD point -> GLB model coordinates (web frdToModel; MODEL_TO_FRD is its own inverse).</summary>
        public static double[] FrdToModel(double x, double y, double z) => new[] { -x, -z, -y };

        /// <summary>Euler angles in degrees (aerospace ZYX) for the HUD; yaw in [0, 360).</summary>
        public static void Px4Euler(double[] q, out double roll, out double pitch, out double yaw)
        {
            double w = q[0], x = q[1], y = q[2], z = q[3];
            const double deg = 180.0 / Math.PI;
            roll = Math.Atan2(2 * (w * x + y * z), 1 - 2 * (x * x + y * y)) * deg;
            pitch = Math.Asin(Math.Max(-1, Math.Min(1, 2 * (w * y - z * x)))) * deg;
            yaw = Math.Atan2(2 * (w * z + x * y), 1 - 2 * (y * y + z * z)) * deg;
            yaw = JsMath.Mod(yaw + 360, 360);
        }

        /// <summary>three.js Quaternion.setFromRotationMatrix (row-major 3x3).</summary>
        static DQuat FromRotationMatrix(double m11, double m12, double m13, double m21, double m22, double m23, double m31, double m32, double m33)
        {
            double trace = m11 + m22 + m33;
            if (trace > 0)
            {
                double s = 0.5 / Math.Sqrt(trace + 1.0);
                return new DQuat((m32 - m23) * s, (m13 - m31) * s, (m21 - m12) * s, 0.25 / s);
            }
            if (m11 > m22 && m11 > m33)
            {
                double s = 2.0 * Math.Sqrt(1.0 + m11 - m22 - m33);
                return new DQuat(0.25 * s, (m12 + m21) / s, (m13 + m31) / s, (m32 - m23) / s);
            }
            if (m22 > m33)
            {
                double s = 2.0 * Math.Sqrt(1.0 + m22 - m11 - m33);
                return new DQuat((m12 + m21) / s, 0.25 * s, (m23 + m32) / s, (m13 - m31) / s);
            }
            {
                double s = 2.0 * Math.Sqrt(1.0 + m33 - m11 - m22);
                return new DQuat((m13 + m31) / s, (m23 + m32) / s, 0.25 * s, (m21 - m12) / s);
            }
        }
    }

    /// <summary>JavaScript number semantics the web relies on.</summary>
    public static class JsMath
    {
        /// <summary>Math.round: half rounds toward +infinity (C# Math.Round is banker's rounding).</summary>
        public static double Round(double x) => Math.Floor(x + 0.5);

        /// <summary>The % operator of JS (sign of the dividend), same as C# % on doubles.</summary>
        public static double Mod(double a, double b) => a % b;

        /// <summary>Number.prototype.toFixed for the informative texts (invariant culture, half away from zero on the decimal value).</summary>
        public static string ToFixed(double x, int digits)
        {
            decimal d = (decimal)x;
            return Math.Round(d, digits, MidpointRounding.AwayFromZero).ToString("F" + digits, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
