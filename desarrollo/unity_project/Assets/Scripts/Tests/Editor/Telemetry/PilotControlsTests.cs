// E-6: the pure parts of piloting (Telemetry/Sim/PilotControls.cs), mirroring the keyboard and gamepad tests of the web
// (tests/ts/flightsim.test.ts "keyboard piloting", src/web/telemetry/gamepad.ts).
using System;
using NUnit.Framework;

namespace WebGL.Telemetry.Tests
{
    public class PilotControlsTests
    {
        const int Full = 700; // KeyDeflection * AxisMax

        [Test]
        public void NoKey_IsCentredSticks_HoldAltitude()
        {
            SimSticks s = PilotControls.KeysToSticks(PilotKeys.None);
            Assert.AreEqual(0, s.x); Assert.AreEqual(0, s.y); Assert.AreEqual(500, s.z); Assert.AreEqual(0, s.r);
            Assert.IsFalse(1 / s.x < 0, "no negative zero");
        }

        [TestCase(PilotKeys.Forward, 700.0, 0.0, 500.0, 0.0)]
        [TestCase(PilotKeys.Back, -700.0, 0.0, 500.0, 0.0)]
        [TestCase(PilotKeys.Right, 0.0, 700.0, 500.0, 0.0)]
        [TestCase(PilotKeys.Left, 0.0, -700.0, 500.0, 0.0)]
        [TestCase(PilotKeys.YawRight, 0.0, 0.0, 500.0, 700.0)]
        [TestCase(PilotKeys.YawLeft, 0.0, 0.0, 500.0, -700.0)]
        [TestCase(PilotKeys.Up, 0.0, 0.0, 850.0, 0.0)]
        [TestCase(PilotKeys.Down, 0.0, 0.0, 150.0, 0.0)]
        public void OneKey_DeflectsOneAxis(PilotKeys k, double x, double y, double z, double r)
        {
            SimSticks s = PilotControls.KeysToSticks(k);
            Assert.AreEqual(x, s.x); Assert.AreEqual(y, s.y); Assert.AreEqual(z, s.z); Assert.AreEqual(r, s.r);
        }

        [Test]
        public void OppositeKeys_Cancel_AndFullDeflectionIsInRange()
        {
            SimSticks c = PilotControls.KeysToSticks(PilotKeys.Forward | PilotKeys.Back | PilotKeys.Left | PilotKeys.Right | PilotKeys.Up | PilotKeys.Down);
            Assert.AreEqual(0, c.x); Assert.AreEqual(0, c.y); Assert.AreEqual(500, c.z);
            SimSticks m = PilotControls.KeysToSticks(PilotKeys.Forward | PilotKeys.Right | PilotKeys.YawRight | PilotKeys.Up, 5);
            Assert.AreEqual(1000, m.x); Assert.AreEqual(1000, m.y); Assert.AreEqual(1000, m.z); Assert.AreEqual(1000, m.r);
            Assert.IsNull(PilotControls.ToMessage(m).Validate(), "the sample is valid on the wire");
            Assert.IsNull(PilotControls.ToMessage(PilotControls.KeysToSticks(PilotKeys.Down | PilotKeys.Back)).Validate());
        }

        [Test]
        public void CommandKeys_AreTheFourCommands()
        {
            Assert.AreEqual("{\"type\":\"command\",\"name\":\"takeoff\",\"altitude\":5}", PilotControls.CommandMessage(PilotCommand.Takeoff).ToJson());
            Assert.AreEqual("{\"type\":\"command\",\"name\":\"land\"}", PilotControls.CommandMessage(PilotCommand.Land).ToJson());
            Assert.AreEqual("{\"type\":\"command\",\"name\":\"hold\"}", PilotControls.CommandMessage(PilotCommand.Hold).ToJson());
            Assert.AreEqual("{\"type\":\"command\",\"name\":\"rtl\"}", PilotControls.CommandMessage(PilotCommand.Rtl).ToJson());
            foreach (PilotCommand c in Enum.GetValues(typeof(PilotCommand))) Assert.IsNull(PilotControls.CommandMessage(c).Validate());
        }

        [Test]
        public void Pad_Mode2_SticksAndButtons()
        {
            Assert.AreEqual(0, PilotControls.ShapeAxis(0.05));
            Assert.AreEqual(1, PilotControls.ShapeAxis(1), 1e-12);
            Assert.AreEqual(-1, PilotControls.ShapeAxis(-1), 1e-12);
            Assert.AreEqual(0, PilotControls.ShapeAxis(double.NaN));
            SimSticks centred = PilotControls.PadToSticks(0, 0, 0, 0);
            Assert.AreEqual(0, centred.x); Assert.AreEqual(500, centred.z);
            SimSticks s = PilotControls.PadToSticks(1, 1, 1, 1); // left stick up-right, right stick up-right
            Assert.AreEqual(1000, s.x, "right stick up = forward");
            Assert.AreEqual(1000, s.y, "right stick right = roll right");
            Assert.AreEqual(1000, s.z, "left stick up = climb");
            Assert.AreEqual(1000, s.r, "left stick right = yaw right");
            SimSticks d = PilotControls.PadToSticks(0, -1, 0, -1);
            Assert.AreEqual(0, d.z, "left stick down = full descent");
            Assert.AreEqual(-1000, d.x);
            Assert.IsFalse(PilotControls.PadActive(0.05, 0, 0, 0.07));
            Assert.IsTrue(PilotControls.PadActive(0, 0.5, 0, 0));
            Assert.AreEqual(PilotCommand.Takeoff, PilotControls.ButtonToCommand(0));
            Assert.AreEqual(PilotCommand.Land, PilotControls.ButtonToCommand(1));
            Assert.AreEqual(PilotCommand.Hold, PilotControls.ButtonToCommand(2));
            Assert.AreEqual(PilotCommand.Rtl, PilotControls.ButtonToCommand(3));
            Assert.IsNull(PilotControls.ButtonToCommand(4));
        }

        [Test]
        public void FlyTo_Altitude_AndGeofence()
        {
            Assert.AreEqual(5, PilotControls.FlyToAltitude(null));
            Assert.AreEqual(12.5, PilotControls.FlyToAltitude(new TelemetryFrame { p = new[] { 0.0, 0.0, -12.5 } }), 1e-12);
            Assert.AreEqual(50, PilotControls.FlyToAltitude(new TelemetryFrame { p = new[] { 0.0, 0.0, -58.0 } }));
            Assert.AreEqual(1, PilotControls.FlyToAltitude(new TelemetryFrame { p = new[] { 0.0, 0.0, 0.2 } }));
            Assert.IsFalse(PilotControls.BeyondGeofence(60, 60));
            Assert.IsTrue(PilotControls.BeyondGeofence(100, 100));
            double n = 300, e = 400;
            PilotControls.ClampToGeofence(ref n, ref e);
            Assert.AreEqual(72, n, 1e-9); Assert.AreEqual(96, e, 1e-9);
            double n2 = 3, e2 = 4;
            PilotControls.ClampToGeofence(ref n2, ref e2);
            Assert.AreEqual(3, n2); Assert.AreEqual(4, e2);
        }

        [Test]
        public void GeofenceTouch_FlagsRadiusAndCeiling_OnlyInFlight()
        {
            Assert.AreEqual(0, PilotControls.GeofenceTouch(null));
            var f = new TelemetryFrame { armed = true, landed = false, p = new[] { 119.8, 0.0, -10.0 } };
            Assert.AreEqual(1, PilotControls.GeofenceTouch(f));
            f.p = new[] { 10.0, 0.0, -59.8 };
            Assert.AreEqual(2, PilotControls.GeofenceTouch(f));
            f.p = new[] { 120.0, 0.0, -60.0 };
            Assert.AreEqual(3, PilotControls.GeofenceTouch(f));
            f.p = new[] { 10.0, 0.0, -10.0 };
            Assert.AreEqual(0, PilotControls.GeofenceTouch(f));
            f.landed = true; f.p = new[] { 120.0, 0.0, 0.0 };
            Assert.AreEqual(0, PilotControls.GeofenceTouch(f));
        }

        [Test]
        public void Piloting_Replay_ReadsOnly_NoCommandsAccepted()
        {
            // the replay hello lists only inject / clear: the client refuses every flight command with a reason, so the UI can say why
            var src = new ManualTelemetrySource(SourceClassification.Replay);
            var client = new TelemetryClient(src) { Log = null };
            client.Start();
            src.Push(new HelloMessage { mode = TelemetryMode.Replay, commands = new[] { "inject", "clear" } });
            client.Update(1000, 0.016);
            Assert.IsFalse(client.Send(PilotControls.CommandMessage(PilotCommand.Takeoff), out string reason));
            StringAssert.Contains("does not accept takeoff", reason);
        }

        [Test]
        public void AckDetails_AreTranslated_AndTheSimulatorOnesAreKnown()
        {
            Assert.AreEqual("the drone is on the ground: take off first", TelemetryStrings.AckDetail("el dron está en tierra: despega primero", "en"));
            Assert.AreEqual("el dron está en tierra: despega primero", TelemetryStrings.AckDetail("el dron está en tierra: despega primero", "es"));
            StringAssert.DoesNotContain("navegador", TelemetryStrings.AckDetail(FlightSim.NotAvailable, "es"));
            StringAssert.DoesNotContain("browser", TelemetryStrings.AckDetail(FlightSim.NotAvailable, "en"));
            Assert.AreEqual("otra cosa", TelemetryStrings.AckDetail("otra cosa", "es"));
        }
    }
}
