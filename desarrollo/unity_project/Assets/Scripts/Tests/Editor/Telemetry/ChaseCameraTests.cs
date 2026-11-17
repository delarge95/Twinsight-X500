// MVP P0 (PLAN_PC 32): the maths of the «Seguir» chase camera (pure C#): shortest-way angles, the lag that follows the heading, the 1.5 s recentre of the user's
// orbit, framerate independence, the heading of a tumbling drone and the pad mapping (LB + right stick, D-pad, R3).
using NUnit.Framework;

namespace WebGL.Telemetry.Tests
{
    public class ChaseCameraTests
    {
        const float Dt60 = 1f / 60f;

        static void Run(ChaseCamera c, float seconds, float bodyYaw, float dt = Dt60, bool reduce = false)
        {
            int n = (int)(seconds / dt + 0.5f);
            for (int i = 0; i < n; i++) c.Step(dt, bodyYaw, true, reduce);
        }

        [Test]
        public void DeltaAngle_TakesTheShortestWayAcrossTheSeam()
        {
            Assert.AreEqual(20f, ChaseCamera.DeltaAngle(350f, 10f), 1e-4);
            Assert.AreEqual(-20f, ChaseCamera.DeltaAngle(10f, 350f), 1e-4);
            Assert.AreEqual(0f, ChaseCamera.DeltaAngle(90f, 90f), 1e-4);
            Assert.AreEqual(-170f, ChaseCamera.DeltaAngle(-170f + 340f, -170f + 340f - 170f), 1e-3);
            Assert.AreEqual(180f, System.Math.Abs(ChaseCamera.DeltaAngle(0f, 180f)), 1e-4);
            Assert.AreEqual(-90f, ChaseCamera.Wrap180(270f), 1e-4);
        }

        [Test]
        public void Heading_OfTheNoseVector()
        {
            Assert.IsTrue(ChaseCamera.TryHeading(0f, 1f, out float north)); Assert.AreEqual(0f, north, 1e-4);   // +Z
            Assert.IsTrue(ChaseCamera.TryHeading(1f, 0f, out float east)); Assert.AreEqual(90f, east, 1e-4);    // +X
            Assert.IsTrue(ChaseCamera.TryHeading(0f, -1f, out float back)); Assert.AreEqual(180f, System.Math.Abs(back), 1e-3);
            Assert.IsTrue(ChaseCamera.TryHeading(-1f, 0f, out float west)); Assert.AreEqual(-90f, west, 1e-4);
        }

        [Test]
        public void Heading_IsUndefinedForADroneNearlyVertical()
        {
            Assert.IsFalse(ChaseCamera.TryHeading(0.05f, 0.05f, out _), "a nose pointing up or down has no heading: the chase keeps the last one");
            Assert.IsFalse(ChaseCamera.TryHeading(float.NaN, 1f, out _));
            var c = new ChaseCamera();
            c.Prime(40f);
            for (int i = 0; i < 120; i++) c.Step(Dt60, 123f, false, false);
            Assert.AreEqual(40f, c.SmoothedYaw, 1e-4, "without a heading the camera does not move");
        }

        [Test]
        public void Prime_PutsTheCameraBehindTheDroneAtOnce()
        {
            var c = new ChaseCamera();
            c.Prime(135f);
            Assert.AreEqual(135f, c.Yaw, 1e-4);
            Assert.AreEqual(ChaseCamera.BasePitch, c.Pitch, 1e-4);
            Assert.IsFalse(c.HasOffset);
        }

        [Test]
        public void Lag_FollowsAHeadingChangeSmoothlyWithoutOvershoot()
        {
            var c = new ChaseCamera();
            c.Prime(0f);
            float prev = c.Yaw;
            for (int i = 0; i < 240; i++)
            {
                c.Step(Dt60, 90f, true, false);
                Assert.GreaterOrEqual(c.Yaw, prev - 1e-4f, "monotonic");
                Assert.LessOrEqual(c.Yaw, 90f + 1e-3f, "never overshoots the heading");
                float step = c.Yaw - prev;
                Assert.Less(step, 90f * (1f - (float)System.Math.Exp(-Dt60 / ChaseCamera.YawTau)) + 0.01f, "the step of one frame is the lag's, never a jump");
                prev = c.Yaw;
            }
            Assert.AreEqual(90f, c.Yaw, 0.1f, "after 4 s it has caught up");
        }

        [Test]
        public void Lag_TurnsTheShortWayAcrossTheSeam()
        {
            var c = new ChaseCamera();
            c.Prime(170f);
            Run(c, 0.3f, -170f); // 20 degrees away through 180, not 340 the other way
            float d = System.Math.Abs(ChaseCamera.DeltaAngle(c.Yaw, -170f));
            Assert.Less(d, 20f, "it moved toward the heading by the short way");
            Assert.Greater(System.Math.Abs(ChaseCamera.DeltaAngle(170f, c.Yaw)), 1f, "and it did move");
        }

        [Test]
        public void SteadyTurn_LagIsRateTimesTau_AndTheCameraStaysBehind()
        {
            // a drone turning at 90 deg/s: first-order lag settles at rate * tau behind the heading (here 31.5 degrees): bounded, never a loss of the drone
            var c = new ChaseCamera();
            float yaw = 0f;
            c.Prime(yaw);
            for (int i = 0; i < 600; i++) { yaw += 90f * Dt60; c.Step(Dt60, ChaseCamera.Wrap180(yaw), true, false); }
            float lag = System.Math.Abs(ChaseCamera.DeltaAngle(c.Yaw, yaw));
            Assert.AreEqual(90f * ChaseCamera.YawTau, lag, 2f);
            Assert.Less(lag, 60f);
        }

        [Test]
        public void Lag_IsFramerateIndependent()
        {
            var a = new ChaseCamera(); a.Prime(0f);
            var b = new ChaseCamera(); b.Prime(0f);
            Run(a, 0.5f, 100f, 1f / 30f);
            Run(b, 0.5f, 100f, 1f / 144f);
            Assert.AreEqual(a.Yaw, b.Yaw, 0.2f, "30 fps and 144 fps follow the same curve");
        }

        [Test]
        public void ReducedMotion_TakesTheHeadingAtOnce()
        {
            var c = new ChaseCamera(); c.Prime(0f);
            c.Step(Dt60, 80f, true, true);
            Assert.AreEqual(80f, c.Yaw, 1e-3);
        }

        [Test]
        public void UserOrbit_IsAnOffsetThatStaysWhileInputContinues()
        {
            var c = new ChaseCamera(); c.Prime(0f);
            for (int i = 0; i < 180; i++) { c.AddInput(1f, 0.2f); c.Step(Dt60, 0f, true, false); }
            Assert.AreEqual(180f, System.Math.Abs(c.UserYaw), 0.05f, "180 frames of 1 degree: half a turn, kept while the input goes on (wrapped to -180..180)");
            Assert.AreEqual(36f, c.UserPitch, 0.01f);
        }

        [Test]
        public void UserOrbit_RecentresOnlyAfterTheDelay_AndThenGlidesToZero()
        {
            var c = new ChaseCamera(); c.Prime(0f);
            c.AddInput(40f, 15f);
            Assert.AreEqual(40f, c.UserYaw, 1e-4);
            Run(c, ChaseCamera.RecenterDelay - 0.1f, 0f);
            Assert.AreEqual(40f, c.UserYaw, 1e-4, "within the 1.5 s nothing moves");
            Assert.AreEqual(15f, c.UserPitch, 1e-4);
            Run(c, 0.3f, 0f);
            Assert.Less(c.UserYaw, 40f, "after 1.5 s it starts gliding back");
            Assert.Greater(c.UserYaw, 0f, "smoothly, not at once");
            Run(c, 4f, 0f);
            Assert.AreEqual(0f, c.UserYaw, 1e-4);
            Assert.AreEqual(0f, c.UserPitch, 1e-4);
            Assert.IsFalse(c.HasOffset);
        }

        [Test]
        public void UserOrbit_NewInputRestartsTheTimer()
        {
            var c = new ChaseCamera(); c.Prime(0f);
            c.AddInput(30f, 0f);
            Run(c, 1.2f, 0f);
            c.AddInput(5f, 0f);
            Run(c, 1.2f, 0f);
            Assert.AreEqual(35f, c.UserYaw, 1e-3, "1.2 s since the last input: still holding");
            Assert.AreEqual(0f, c.IdleFor - 1.2f, 0.05f);
        }

        [Test]
        public void UserOrbit_ZeroInputDoesNotHoldTheCameraAway()
        {
            var c = new ChaseCamera(); c.Prime(0f);
            c.AddInput(30f, 0f);
            for (int i = 0; i < 200; i++) { c.AddInput(0f, 0f); c.Step(Dt60, 0f, true, false); }
            Assert.Less(System.Math.Abs(c.UserYaw), 30f, "a stick at rest (zero input every frame) lets the 1.5 s run");
        }

        [Test]
        public void UserOrbit_TakesTheShortWayBack()
        {
            var c = new ChaseCamera(); c.Prime(0f);
            c.AddInput(170f, 0f);
            c.AddInput(30f, 0f); // 200 = -160
            Assert.AreEqual(-160f, c.UserYaw, 1e-3);
            Run(c, ChaseCamera.RecenterDelay + 0.3f, 0f);
            Assert.Greater(c.UserYaw, -160f, "it recentres by the short way (towards 0 through -80)");
        }

        [Test]
        public void Pitch_IsBoundedSoTheViewNeverFlips()
        {
            var c = new ChaseCamera(); c.Prime(0f);
            for (int i = 0; i < 1000; i++) c.AddInput(0f, 5f);
            Assert.LessOrEqual(c.Pitch, ChaseCamera.MaxPitch);
            Assert.AreEqual(ChaseCamera.MaxUserPitch, c.UserPitch, 1e-4);
            for (int i = 0; i < 1000; i++) c.AddInput(0f, -5f);
            Assert.GreaterOrEqual(c.Pitch, ChaseCamera.MinPitch);
            Assert.AreEqual(ChaseCamera.MinUserPitch, c.UserPitch, 1e-4);
        }

        [Test]
        public void ClearUser_DropsTheOffsetForReset()
        {
            var c = new ChaseCamera(); c.Prime(10f);
            c.AddInput(60f, 20f);
            c.ClearUser();
            Assert.IsFalse(c.HasOffset);
            Assert.AreEqual(10f, c.Yaw, 1e-4);
        }

        [Test]
        public void NaNInput_IsIgnored()
        {
            var c = new ChaseCamera(); c.Prime(0f);
            c.AddInput(float.NaN, 1f);
            c.AddInput(1f, float.NaN);
            Assert.IsFalse(c.HasOffset);
            c.Step(float.NaN, 0f, true, false); // a broken dt is a zero step
            Assert.AreEqual(0f, c.Yaw, 1e-4);
        }

        // ───────────────────────── pad mapping ─────────────────────────

        [Test]
        public void Pad_RightStickOnlyOrbitsWithLbHeld()
        {
            ChaseCamera.PadOrbit(false, 1.0, 1.0, false, false, false, false, 0.1f, out float y0, out float p0);
            Assert.AreEqual(0f, y0); Assert.AreEqual(0f, p0);
            ChaseCamera.PadOrbit(true, 1.0, 0.0, false, false, false, false, 0.1f, out float y1, out float p1);
            Assert.Greater(y1, 0f, "stick right turns the view right");
            Assert.AreEqual(ChaseCamera.PadYawRate * 0.1f, y1, 0.01f);
            Assert.AreEqual(0f, p1);
        }

        [Test]
        public void Pad_StickUpLooksUp()
        {
            ChaseCamera.PadOrbit(true, 0.0, 1.0, false, false, false, false, 0.1f, out _, out float p);
            Assert.Less(p, 0f, "stick up lowers the camera pitch (looks up), like the mouse");
        }

        [Test]
        public void Pad_DeadzoneIsRespected()
        {
            ChaseCamera.PadOrbit(true, 0.05, -0.05, false, false, false, false, 0.1f, out float y, out float p);
            Assert.AreEqual(0f, y); Assert.AreEqual(0f, p);
        }

        [Test]
        public void Pad_DpadOrbitsWithoutLb_AndOppositesCancel()
        {
            ChaseCamera.PadOrbit(false, 0, 0, false, true, false, false, 0.1f, out float y, out float p);
            Assert.AreEqual(ChaseCamera.DpadRate * 0.1f, y, 1e-4); Assert.AreEqual(0f, p);
            ChaseCamera.PadOrbit(false, 0, 0, true, true, false, false, 0.1f, out y, out p);
            Assert.AreEqual(0f, y, 1e-5);
            ChaseCamera.PadOrbit(false, 0, 0, false, false, true, false, 0.1f, out y, out p);
            Assert.Less(p, 0f);
            ChaseCamera.PadOrbit(false, 0, 0, false, false, false, true, 0.1f, out y, out p);
            Assert.Greater(p, 0f);
        }

        [Test]
        public void Pad_NoTimeNoMotion()
        {
            ChaseCamera.PadOrbit(true, 1, 1, true, true, true, true, 0f, out float y, out float p);
            Assert.AreEqual(0f, y); Assert.AreEqual(0f, p);
        }

        [Test]
        public void Pad_WithLbTheDroneGetsNeitherPitchNorRoll()
        {
            // the pilot zeroes the right stick while LB is held (DesktopTelemetryUI.ReadPilotInput): the sticks of the drone stay neutral
            SimSticks s = PilotControls.PadToSticks(0, 0, 0, 0);
            Assert.AreEqual(0, s.x); Assert.AreEqual(0, s.y); Assert.AreEqual(PilotControls.ThrottleHold, s.z); Assert.AreEqual(0, s.r);
            SimSticks free = PilotControls.PadToSticks(0, 0, 0.8, 0.8);
            Assert.AreNotEqual(0, free.x, "without LB the right stick flies (mode 2)");
        }

        [Test]
        public void PadButtons_HaveTheStickClicks()
        {
            Assert.AreNotEqual(PadButtons.None, PadButtons.RightStick);
            Assert.AreNotEqual(PadButtons.RightStick, PadButtons.LeftStick);
            var m = new PadMachine();
            var ev = new System.Collections.Generic.List<PadEvent>();
            m.Update(new PadState(), 0, ev);
            m.Update(new PadState { Buttons = PadButtons.RightStick }, 0.1, ev);
            Assert.IsTrue(m.Pressed(PadButtons.RightStick), "R3 is an edge in DRON mode (it switches the camera mode)");
            Assert.IsEmpty(ev);
        }
    }
}
