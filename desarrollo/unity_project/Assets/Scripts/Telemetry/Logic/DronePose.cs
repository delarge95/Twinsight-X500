// WP-10: pose of the imported drone model from a telemetry frame. Pure Unity math (no scene access), unit-tested in EditMode.
//
// ORIENTATION OF THE IMPORTED MODEL (x500v2_Drone, the only place this is stated):
//   At rest the model stands with +Y up and its NOSE pointing to world -Z. Evidence (rest world positions, Editor, 2026-11-06):
//     - GPS mast (x500v2_gps_m10, bracket gpsv5_zhijia_tuopan) at z = +0.9 .. +1.6 -> the rear of an X500 V2 is +Z, the nose is -Z;
//     - the four motors are at (+-2.9, +-2.9) and the motor names FR/FL/BR/BL agree with front = -Z;
//     - the telemetry radio is at x = +0.89, the LEFT of a body whose nose is -Z (Unity is left-handed: right = up x forward = -X);
//       in the web GLB (nose -X, right -Z) the radio is also on the left (z = +0.054): the Unity import is NOT mirrored.
//   The names say "FR" for the motor at +X, which is physically the front-LEFT one: never trust the names, assign parts by position.
//   So the fixed model->body rotation is a half turn about Y (nose -Z -> body forward +Z, model right -X -> body right +X).
//
//   world rotation of the root = q_body * ModelToBody * restRootRotation      (q_body = NedFrames.Px4AttitudeToUnity)
//   body origin (pivot of the attitude) = middle of the four motors, in the rotor plane;
//   scale: the model is ~16 units per metre (motors at +-2.9 for CA_ROTOR_PX/PY = +-0.18 m), measured from the motors at run time.

using UnityEngine;

namespace WebGL.Telemetry
{
    public static class DronePose
    {
        /// <summary>Half turn about Y: the model nose is -Z, the body forward is +Z. The single orientation correction of the model.</summary>
        public static readonly Quaternion ModelToBody = Quaternion.Euler(0f, 180f, 0f);

        /// <summary>Propeller phase scale of the web (PROP_VISUAL_SCALE): 6500 rpm turns at 260 rpm so the blades stay readable.</summary>
        public const double PropVisualScale = 0.04;

        /// <summary>The model at rest (before telemetry): root transform and the body origin, all in world space.</summary>
        public struct Rest
        {
            public Vector3 RootPosition;
            public Quaternion RootRotation;
            public Vector3 BodyOrigin;
            /// <summary>Model units per metre.</summary>
            public float UnitsPerMeter;
        }

        /// <summary>Position of the body origin in the Unity world for a NED position p (metres), ground-corrected by the client.</summary>
        public static Vector3 BodyPosition(in Rest rest, double[] pNed) => rest.BodyOrigin + NedFrames.NedToUnityVector(pNed) * rest.UnitsPerMeter;

        /// <summary>Rotation to apply to the rest model (relative): q_body * ModelToBody.</summary>
        public static Quaternion RelativeRotation(double[] qPx4) => NedFrames.Px4AttitudeToUnityQuaternion(qPx4) * ModelToBody;

        /// <summary>World position and rotation of the model ROOT so that the whole model rotates about the body origin and sits at p.</summary>
        public static void Root(in Rest rest, double[] pNed, double[] qPx4, out Vector3 position, out Quaternion rotation)
        {
            Quaternion rel = RelativeRotation(qPx4);
            rotation = rel * rest.RootRotation;
            position = BodyPosition(rest, pNed) + rel * (rest.RootPosition - rest.BodyOrigin);
        }

        /// <summary>
        /// Model units per metre from the rest world positions of the four motors and the rotor geometry (mean distance to the centre,
        /// in the rotor plane). Rotor geometry is FRD metres; motor positions are Unity world.
        /// </summary>
        public static float UnitsPerMeter(Vector3[] motorsWorld, RotorGeometry[] rotors, out Vector3 centre)
        {
            centre = Vector3.zero;
            foreach (var m in motorsWorld) centre += m;
            centre /= Mathf.Max(1, motorsWorld.Length);
            double dModel = 0;
            foreach (var m in motorsWorld) dModel += Mathf.Sqrt((m.x - centre.x) * (m.x - centre.x) + (m.z - centre.z) * (m.z - centre.z));
            dModel /= Mathf.Max(1, motorsWorld.Length);
            double dMeters = 0;
            foreach (var r in rotors) dMeters += System.Math.Sqrt(r.px * r.px + r.py * r.py);
            dMeters /= System.Math.Max(1, rotors.Length);
            return dMeters > 1e-6 ? (float)(dModel / dMeters) : 1f;
        }

        /// <summary>
        /// Motor number (1..4) of each part from its REST world position: positions go to body coordinates (x right, z forward) through
        /// ModelToBody about the body origin and are matched to the nearest rotor of the hello (QuadX.AssignToRotorsUnity).
        /// </summary>
        public static int[] MotorOfParts(Vector3[] partsWorld, Vector3 bodyOrigin, RotorGeometry[] rotors)
        {
            var xz = new System.Collections.Generic.List<double[]>();
            foreach (var p in partsWorld)
            {
                Vector3 b = ModelToBody * (p - bodyOrigin);
                xz.Add(new double[] { b.x, b.z });
            }
            return QuadX.AssignToRotorsUnity(xz, rotors);
        }

        /// <summary>Propeller turn in degrees about an UP-pointing axis. Spin +1 = counter-clockwise seen from above; Unity rotations about +Y are clockwise from above.</summary>
        public static float PropAngleDegrees(double phaseRad, int spin) => (float)(-spin * phaseRad * 180.0 / System.Math.PI);
    }
}
