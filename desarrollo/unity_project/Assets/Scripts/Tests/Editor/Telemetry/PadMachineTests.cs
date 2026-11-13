// PLAN_PC 27.2: the pad-mode state machine (pure C#). DRON by default; Start toggles MENÚS; in MENÚS the d-pad / left stick move the focus (with
// repeat), A / B / LB / RB / Back become menu actions and the right stick scrolls; in DRON none of that exists and the pad is left to the pilot.
using System.Collections.Generic;
using NUnit.Framework;

namespace WebGL.Telemetry.Tests
{
    public class PadMachineTests
    {
        static PadState S(PadButtons b = PadButtons.None, double lx = 0, double ly = 0, double rx = 0, double ry = 0) =>
            new PadState { Buttons = b, LeftX = lx, LeftY = ly, RightX = rx, RightY = ry };

        static List<PadEvent> Step(PadMachine m, PadState? s, double t)
        {
            var ev = new List<PadEvent>();
            m.Update(s, t, ev);
            return ev;
        }

        [Test]
        public void ANewPad_StartsInDroneMode_AndNothingIsAMenuAction()
        {
            var m = new PadMachine();
            Assert.IsFalse(m.Connected);
            var ev = Step(m, S(), 0);
            Assert.IsTrue(m.Connected);
            Assert.IsTrue(m.JustConnected);
            Assert.AreEqual(PadMode.Drone, m.Mode);
            Assert.IsEmpty(ev);

            // everything a menu would use: nothing is emitted in DRON mode
            ev = Step(m, S(PadButtons.A | PadButtons.B | PadButtons.LB | PadButtons.RB | PadButtons.Back | PadButtons.DpadDown, 1, 1, 0, 1), 0.1);
            Assert.IsEmpty(ev, "in DRON mode the UI receives nothing from the pad");
            Assert.AreEqual(0, m.ScrollY, "the right stick belongs to the drone in DRON mode");
            Assert.IsTrue(m.Pressed(PadButtons.A), "the pilot still sees the face-button edges");
        }

        [Test]
        public void Start_TogglesBetweenDroneAndMenus()
        {
            var m = new PadMachine();
            Step(m, S(), 0);
            Step(m, S(PadButtons.Start), 0.1);
            Assert.AreEqual(PadMode.Menus, m.Mode);
            Assert.IsTrue(m.ModeJustChanged);
            Step(m, S(), 0.2);
            Assert.IsFalse(m.ModeJustChanged);
            Step(m, S(PadButtons.Start), 0.3);
            Assert.AreEqual(PadMode.Drone, m.Mode);
            Assert.IsTrue(m.ModeJustChanged);
        }

        [Test]
        public void HoldingStart_DoesNotFlicker()
        {
            var m = new PadMachine();
            Step(m, S(), 0);
            Step(m, S(PadButtons.Start), 0.1);
            for (int i = 2; i < 30; i++) Step(m, S(PadButtons.Start), 0.1 * i);
            Assert.AreEqual(PadMode.Menus, m.Mode, "an edge toggles once, a held button does not repeat it");
        }

        [Test]
        public void InTheFrameTheModeChanges_NothingElseActs()
        {
            var m = new PadMachine();
            Step(m, S(), 0);
            var ev = Step(m, S(PadButtons.Start | PadButtons.A | PadButtons.B), 0.1);
            Assert.IsEmpty(ev);
            Assert.IsFalse(m.Pressed(PadButtons.A), "a takeoff must not ride along with the mode change");
            Assert.IsTrue(m.Pressed(PadButtons.Start));
        }

        [Test]
        public void Menus_FaceAndShoulderButtons_AreMenuActions()
        {
            var m = new PadMachine();
            Step(m, S(), 0);
            Step(m, S(PadButtons.Start), 0.1);
            Step(m, S(), 0.2);
            var ev = Step(m, S(PadButtons.A), 0.3);
            Assert.AreEqual(PadAction.Submit, ev[0].Action);
            ev = Step(m, S(PadButtons.B), 0.4);
            Assert.AreEqual(PadAction.Cancel, ev[0].Action);
            ev = Step(m, S(PadButtons.LB), 0.5);
            Assert.AreEqual(PadAction.PrevTab, ev[0].Action);
            ev = Step(m, S(PadButtons.RB), 0.6);
            Assert.AreEqual(PadAction.NextTab, ev[0].Action);
            ev = Step(m, S(PadButtons.Back), 0.7);
            Assert.AreEqual(PadAction.Help, ev[0].Action);
        }

        [Test]
        public void Menus_DpadMoves_WithRepeat()
        {
            var m = new PadMachine();
            Step(m, S(), 0);
            Step(m, S(PadButtons.Start), 0.1);
            Step(m, S(), 0.2);
            var ev = Step(m, S(PadButtons.DpadDown), 1.0);
            Assert.AreEqual(1, ev.Count);
            Assert.AreEqual(0, ev[0].Dx);
            Assert.AreEqual(-1, ev[0].Dy, "d-pad down = focus down");
            Assert.IsEmpty(Step(m, S(PadButtons.DpadDown), 1.2), "no repeat before the first delay");
            Assert.AreEqual(1, Step(m, S(PadButtons.DpadDown), 1.45).Count, "first repeat after 0.4 s");
            Assert.IsEmpty(Step(m, S(PadButtons.DpadDown), 1.5));
            Assert.AreEqual(1, Step(m, S(PadButtons.DpadDown), 1.6).Count, "then every 0.12 s");
            Step(m, S(), 1.7);
            ev = Step(m, S(PadButtons.DpadRight), 1.8);
            Assert.AreEqual(1, ev[0].Dx);
        }

        [Test]
        public void Menus_LeftStick_Moves_WithHysteresis_AndTheDominantAxisWins()
        {
            var m = new PadMachine();
            Step(m, S(), 0);
            Step(m, S(PadButtons.Start), 0.1);
            Step(m, S(), 0.2);
            Assert.IsEmpty(Step(m, S(lx: 0.5), 1.0), "below the engage threshold (0.6)");
            var ev = Step(m, S(lx: 0.8, ly: 0.3), 1.1);
            Assert.AreEqual(1, ev.Count);
            Assert.AreEqual(1, ev[0].Dx);
            Assert.IsEmpty(Step(m, S(lx: 0.5), 1.15), "held above the release threshold (0.4): still the same direction, not a new press");
            Step(m, S(lx: 0.3), 1.2);
            ev = Step(m, S(ly: 0.9, lx: 0.2), 1.3);
            Assert.AreEqual(1, ev[0].Dy, "stick up = focus up");
        }

        [Test]
        public void Menus_RightStick_Scrolls_AfterADeadzone()
        {
            var m = new PadMachine();
            Step(m, S(), 0);
            Step(m, S(PadButtons.Start), 0.1);
            Step(m, S(ry: 0.1), 0.2);
            Assert.AreEqual(0, m.ScrollY, "inside the deadzone");
            Step(m, S(ry: 1.0), 0.3);
            Assert.AreEqual(1.0, m.ScrollY, 1e-9);
            Step(m, S(ry: -0.575), 0.4);
            Assert.AreEqual(-0.5, m.ScrollY, 1e-9);
        }

        [Test]
        public void BackToDrone_StopsEveryMenuAction()
        {
            var m = new PadMachine();
            Step(m, S(), 0);
            Step(m, S(PadButtons.Start), 0.1);
            Step(m, S(), 0.2);
            Step(m, S(PadButtons.Start), 0.3);
            Step(m, S(), 0.4);
            Assert.AreEqual(PadMode.Drone, m.Mode);
            Assert.IsEmpty(Step(m, S(PadButtons.A | PadButtons.DpadUp, ly: 1), 0.5));
        }

        [Test]
        public void Disconnect_ResetsToDrone_AndAReconnectIsDroneToo()
        {
            var m = new PadMachine();
            Step(m, S(), 0);
            Step(m, S(PadButtons.Start), 0.1);
            Assert.AreEqual(PadMode.Menus, m.Mode);
            Step(m, null, 0.2);
            Assert.IsFalse(m.Connected);
            Assert.IsTrue(m.JustDisconnected);
            Step(m, S(), 0.3);
            Assert.IsTrue(m.Connected);
            Assert.AreEqual(PadMode.Drone, m.Mode, "a pad that comes back starts in DRON: the safe default");
        }

        [Test]
        public void ButtonsAlreadyHeldWhenThePadAppears_AreNotPresses()
        {
            var m = new PadMachine();
            Step(m, S(PadButtons.A | PadButtons.Start), 0);
            Assert.AreEqual(PadMode.Drone, m.Mode, "a held Start at connection does not toggle");
            Assert.IsFalse(m.Pressed(PadButtons.A), "a held A at connection is not a takeoff");
        }

        [Test]
        public void SetMode_IsTheSameBookkeepingAsStart()
        {
            var m = new PadMachine();
            Step(m, S(), 0);
            m.SetMode(PadMode.Menus);
            Assert.AreEqual(PadMode.Menus, m.Mode);
            Assert.IsTrue(m.ModeJustChanged);
        }

        [Test]
        public void PadSource_UsesTheInjectedProvider()
        {
            try
            {
                PadSource.Provider = () => new PadState { LeftX = 0.25, Buttons = PadButtons.A };
                var s = PadSource.Poll();
                Assert.IsTrue(s.HasValue);
                Assert.AreEqual(0.25, s.Value.LeftX);
                Assert.IsTrue(s.Value.Has(PadButtons.A));
                PadSource.Provider = () => null;
                Assert.IsFalse(PadSource.Poll().HasValue, "an injected 'no pad'");
            }
            finally { PadSource.Provider = null; }
        }
    }
}
