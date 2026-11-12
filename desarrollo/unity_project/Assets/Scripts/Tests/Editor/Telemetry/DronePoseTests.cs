// WP-10: pose of the imported model (DronePose). EditMode, no scene.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace WebGL.Telemetry.Tests
{
    using JObj = Dictionary<string, object>;

    public class DronePoseTests
    {
        static readonly Vector3 Nose = new Vector3(0, 0, -1), Right = new Vector3(-1, 0, 0), Up = new Vector3(0, 1, 0);

        [Test]
        public void ModelToBody_Maps_Model_Nose_Right_Up_To_Body_Forward_Right_Up()
        {
            Assert.Less((DronePose.ModelToBody * Nose - Vector3.forward).magnitude, 1e-5f);
            Assert.Less((DronePose.ModelToBody * Right - Vector3.right).magnitude, 1e-5f);
            Assert.Less((DronePose.ModelToBody * Up - Vector3.up).magnitude, 1e-5f);
        }

        [Test]
        public void Model_Axes_Follow_The_Golden_Body_Axes_For_Every_Attitude()
        {
            var g = C.Golden("frames");
            int n = 0;
            foreach (JObj a in C.A(g, "attitude"))
            {
                var q = C.V(a, "q_px4_wxyz");
                var ax = C.O(a, "body_axes_in_unity_world");
                var rel = DronePose.RelativeRotation(q);
                AssertV(C.V(ax, "forward"), rel * Nose, "nose = body forward " + C.S(a, "name"));
                AssertV(C.V(ax, "right"), rel * Right, "model right = body right " + C.S(a, "name"));
                AssertV(C.V(ax, "down"), -(rel * Up), "model up = - body down " + C.S(a, "name"));
                n++;
            }
            Assert.AreEqual(8, n);
        }

        [Test]
        public void Climb_Goes_Up_North_Is_PlusZ_Left_Roll_Banks_Left()
        {
            var rest = new DronePose.Rest { RootPosition = Vector3.zero, RootRotation = Quaternion.identity, BodyOrigin = new Vector3(0, 1, 0), UnitsPerMeter = 16f };
            // 10 m climb (NED z = -10) and 5 m north: up and +Z.
            Vector3 p = DronePose.BodyPosition(rest, new[] { 5.0, 0, -10 });
            Assert.AreEqual(1 + 160f, p.y, 1e-3);
            Assert.AreEqual(80f, p.z, 1e-3);
            Assert.AreEqual(0f, p.x, 1e-3);
            // East = +X.
            Assert.AreEqual(32f, DronePose.BodyPosition(rest, new[] { 0.0, 2, 0 }).x, 1e-3);
            // Roll LEFT 30 deg (PX4 roll is positive right-side down): negative roll, the left wing goes down: body-left axis tips below the horizon.
            var qLeft = RollQuat(-30);
            var rel = DronePose.RelativeRotation(qLeft);
            Vector3 leftWing = rel * -Right; // model left = +X at rest
            Assert.Less(leftWing.y, -0.45f, "left wing down");
            Vector3 rightWing = rel * Right;
            Assert.Greater(rightWing.y, 0.45f, "right wing up");
            // Nose up (positive pitch) raises the nose.
            Assert.Greater((DronePose.RelativeRotation(PitchQuat(20)) * Nose).y, 0.3f);
            // Yaw 90 (east): nose points +X.
            AssertV(new double[] { 1, 0, 0 }, DronePose.RelativeRotation(YawQuat(90)) * Nose, "yaw 90 nose east");
        }

        [Test]
        public void Root_Rotates_The_Whole_Model_About_The_Body_Origin()
        {
            var restRot = Quaternion.Euler(270, 90, 0);
            var rest = new DronePose.Rest { RootPosition = new Vector3(0, 0, 0), RootRotation = restRot, BodyOrigin = new Vector3(0, 1.088f, 0), UnitsPerMeter = 16.1f };
            Vector3 partRest = new Vector3(2.9f, 1.088f + 0.4f, -2.9f);
            Vector3 local = Quaternion.Inverse(restRot) * (partRest - rest.RootPosition);
            double[] p = { 3, -2, -4 }, q = { 0.9, 0.1, -0.2, 0.3 };
            DronePose.Root(rest, p, q, out Vector3 pos, out Quaternion rot);
            Vector3 world = pos + rot * local;
            Vector3 expected = DronePose.BodyPosition(rest, p) + DronePose.RelativeRotation(q) * (partRest - rest.BodyOrigin);
            Assert.Less((world - expected).magnitude, 2e-3f, "part follows body origin + relative rotation");
            // The body origin itself lands exactly on the frame position.
            Vector3 originLocal = Quaternion.Inverse(restRot) * (rest.BodyOrigin - rest.RootPosition);
            Assert.Less((pos + rot * originLocal - DronePose.BodyPosition(rest, p)).magnitude, 2e-3f);
        }

        [Test]
        public void UnitsPerMeter_And_Motor_Assignment_From_The_Real_Layout()
        {
            // Rest world positions of x500v2_motor_FR/FL/BR/BL measured in the Editor (names are Blender labels, not body positions).
            var names = new[] { "FR", "FL", "BR", "BL" };
            var pos = new[] { new Vector3(2.897f, 1.088f, -2.917f), new Vector3(-2.911f, 1.088f, -2.917f), new Vector3(2.897f, 1.088f, 2.891f), new Vector3(-2.911f, 1.088f, 2.891f) };
            float k = DronePose.UnitsPerMeter(pos, QuadX.Px4QuadX, out Vector3 centre);
            Assert.AreEqual(16.1f, k, 0.2f);
            Assert.AreEqual(0f, centre.x, 0.02f);
            Assert.AreEqual(0f, centre.z, 0.02f);
            int[] motor = DronePose.MotorOfParts(pos, centre, QuadX.Px4QuadX);
            Assert.AreEqual(4, motor.Distinct().Count(), "one motor per part");
            // Nose is -Z and right is -X: FR (+X, -Z) is physically front-LEFT = motor 3 (px +0.18 forward, py -0.18 left);
            // FL (-X, -Z) is front-RIGHT = motor 1; BR (+X, +Z) is rear-LEFT = motor 2 (px -0.18, py -0.18); BL (-X, +Z) is rear-RIGHT = motor 4.
            Assert.AreEqual(3, motor[Array.IndexOf(names, "FR")]);
            Assert.AreEqual(1, motor[Array.IndexOf(names, "FL")]);
            Assert.AreEqual(2, motor[Array.IndexOf(names, "BR")]);
            Assert.AreEqual(4, motor[Array.IndexOf(names, "BL")]);
        }

        [Test]
        public void Propeller_Angle_Sign_Follows_Spin_Direction()
        {
            // spin +1 = CCW from above: AdvancePhase increases the phase, a Unity rotation about +Y is clockwise from above, so the angle goes negative.
            double ph = Interp.AdvancePhase(0, 6000, 1, 1.0 / 60, DronePose.PropVisualScale);
            Assert.Greater(ph, 0);
            Assert.Less(DronePose.PropAngleDegrees(ph, 1), 0f);
            double ph2 = Interp.AdvancePhase(0, 6000, -1, 1.0 / 60, DronePose.PropVisualScale);
            Assert.Less(ph2, 0);
            Assert.Greater(DronePose.PropAngleDegrees(ph2, 1), 0f);
            // 6500 rpm * 0.04 = 260 rpm: about 26 degrees per frame at 60 fps (visible, not a blur).
            Assert.AreEqual(26.0, Math.Abs(DronePose.PropAngleDegrees(Interp.AdvancePhase(0, 6500, 1, 1.0 / 60, DronePose.PropVisualScale), 1)), 0.2);
        }

        static void AssertV(double[] want, Vector3 got, string what)
        {
            Assert.AreEqual(want[0], got.x, 2e-5, what + " x");
            Assert.AreEqual(want[1], got.y, 2e-5, what + " y");
            Assert.AreEqual(want[2], got.z, 2e-5, what + " z");
        }

        // PX4 attitude quaternions [w, x, y, z] from yaw/pitch/roll (ZYX, degrees).
        static double[] Euler(double yaw, double pitch, double roll)
        {
            double r = Math.PI / 180;
            double cy = Math.Cos(yaw * r / 2), sy = Math.Sin(yaw * r / 2), cp = Math.Cos(pitch * r / 2), sp = Math.Sin(pitch * r / 2), cr = Math.Cos(roll * r / 2), sr = Math.Sin(roll * r / 2);
            return new[] { cr * cp * cy + sr * sp * sy, sr * cp * cy - cr * sp * sy, cr * sp * cy + sr * cp * sy, cr * cp * sy - sr * sp * cy };
        }
        static double[] RollQuat(double d) => Euler(0, 0, d);
        static double[] PitchQuat(double d) => Euler(0, d, 0);
        static double[] YawQuat(double d) => Euler(d, 0, 0);
    }
}
