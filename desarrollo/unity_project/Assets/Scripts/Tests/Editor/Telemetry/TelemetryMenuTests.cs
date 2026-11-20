// UX de escritorio, paso 1: la tabla por fuente (TelemetryMenu). Port de tests/ts/surface.test.ts y de la parte de tablas de ux4.test.ts de la web, con las
// decisiones del escritorio (ver el encabezado de TelemetryMenu.cs): «Vuelos» solo con Mis sesiones y el vuelo de muestra, SITL con tres secciones.
using System.Linq;
using NUnit.Framework;

namespace WebGL.Telemetry.Tests
{
    public class TelemetryMenuTests
    {
        static readonly MenuSurface[] All = TelemetryMenu.Surfaces;

        [Test]
        public void Inspector_Has_Only_The_Ground_And_The_Sessions_Hub()
        {
            CollectionAssert.AreEqual(new[] { ToolId.Ground, ToolId.Sessions }, TelemetryMenu.ToolsFor(MenuSurface.Inspector));
        }

        [Test]
        public void Simulator_Has_Every_Tool()
        {
            CollectionAssert.AreEqual(TelemetryMenu.ToolIds, TelemetryMenu.ToolsFor(MenuSurface.Sim));
            Assert.IsEmpty(TelemetryMenu.ToolsMissing(MenuSurface.Sim));
        }

        [Test]
        public void Sitl_Has_Commands_And_Mission_But_No_Fence_Wind_Battery_Course_Or_Training()
        {
            CollectionAssert.AreEqual(new[]
            {
                ToolId.Pilot, ToolId.Takeoff, ToolId.Land, ToolId.Hold, ToolId.Rtl, ToolId.Follow, ToolId.Game,
                ToolId.Ground, ToolId.Map, ToolId.Preflight, ToolId.Mission, ToolId.Sessions, ToolId.Record,
            }, TelemetryMenu.ToolsFor(MenuSurface.Sitl));
        }

        [Test]
        public void Replay_And_Own_Take_No_Orders_So_Only_Map_Preflight_Ground_And_Sessions()
        {
            var expected = new[] { ToolId.Ground, ToolId.Map, ToolId.Preflight, ToolId.Sessions };
            CollectionAssert.AreEquivalent(expected, TelemetryMenu.ToolsFor(MenuSurface.Replay));
            CollectionAssert.AreEquivalent(expected, TelemetryMenu.ToolsFor(MenuSurface.Own));
        }

        [Test]
        public void Present_Plus_Missing_Is_Every_Tool_On_Every_Surface()
        {
            foreach (var s in All)
                CollectionAssert.AreEquivalent(TelemetryMenu.ToolIds, TelemetryMenu.ToolsFor(s).Concat(TelemetryMenu.ToolsMissing(s)).ToList(), s.ToString());
        }

        [Test]
        public void Every_Tool_Exists_Somewhere_And_The_Drawing_Order_Is_The_Enum_Order()
        {
            foreach (var id in TelemetryMenu.ToolIds) Assert.IsTrue(All.Any(s => TelemetryMenu.Has(id, s)), id.ToString());
            foreach (var s in All)
            {
                var idx = TelemetryMenu.ToolsFor(s).Select(t => (int)t).ToList();
                CollectionAssert.AreEqual(idx.OrderBy(i => i).ToList(), idx, s.ToString());
            }
        }

        [Test]
        public void Tools_That_Give_Orders_Exist_Exactly_Where_The_Source_Takes_Commands()
        {
            foreach (var s in All)
            {
                bool commands = s == MenuSurface.Sim || s == MenuSurface.Sitl;
                foreach (var id in new[] { ToolId.Pilot, ToolId.Takeoff, ToolId.Land, ToolId.Hold, ToolId.Rtl, ToolId.Mission })
                    Assert.AreEqual(commands, TelemetryMenu.Has(id, s), id + " on " + s);
            }
        }

        [Test]
        public void Hold_And_Rtl_Stay_Visible_Wherever_There_Are_Commands()
        {
            // «Mantener» (H) y «Volver» (R) no pasan a «Avanzado»: por seguridad se ven siempre que la fuente da ordenes
            foreach (var s in new[] { MenuSurface.Sim, MenuSurface.Sitl })
            {
                Assert.IsTrue(TelemetryMenu.Has(ToolId.Hold, s));
                Assert.IsTrue(TelemetryMenu.Has(ToolId.Rtl, s));
            }
        }

        [Test]
        public void The_Simulator_Owns_The_Fence_Wind_Battery_Course_And_Training()
        {
            foreach (var id in new[] { ToolId.Fence, ToolId.Wind, ToolId.Battery, ToolId.Course, ToolId.Train })
                CollectionAssert.AreEqual(new[] { MenuSurface.Sim }, All.Where(s => TelemetryMenu.Has(id, s)).ToList(), id.ToString());
        }

        [Test]
        public void The_Record_Switch_Needs_A_Live_Source_That_Can_Be_Recorded()
        {
            CollectionAssert.AreEqual(new[] { MenuSurface.Sim, MenuSurface.Sitl }, All.Where(s => TelemetryMenu.Has(ToolId.Record, s)).ToList());
            CollectionAssert.DoesNotContain(TelemetryMenu.StripToolsFor(MenuSurface.Sim), ToolId.Record); // lives inside the popover, not in a pill
        }

        [Test]
        public void AvailableIn_Names_The_Sources_That_Have_What_Is_Missing_Never_The_Current_One()
        {
            CollectionAssert.AreEqual(new[] { PickSource.Replay, PickSource.Sim, PickSource.Sitl }, TelemetryMenu.AvailableIn(MenuSurface.Inspector));
            CollectionAssert.AreEqual(new[] { PickSource.Sim, PickSource.Sitl }, TelemetryMenu.AvailableIn(MenuSurface.Replay));
            CollectionAssert.AreEqual(new[] { PickSource.Sim, PickSource.Sitl }, TelemetryMenu.AvailableIn(MenuSurface.Own));
            CollectionAssert.AreEqual(new[] { PickSource.Sim }, TelemetryMenu.AvailableIn(MenuSurface.Sitl));
            Assert.IsEmpty(TelemetryMenu.AvailableIn(MenuSurface.Sim));
        }

        [Test]
        public void An_Own_Session_Counts_As_The_Replay_In_The_Source_Selector()
        {
            Assert.AreEqual(PickSource.Replay, TelemetryMenu.PickOf(MenuSurface.Own));
            Assert.AreEqual(PickSource.Replay, TelemetryMenu.ChosenSource(MenuSurface.Replay));
            Assert.IsNull(TelemetryMenu.ChosenSource(MenuSurface.Inspector));
            Assert.AreEqual(3, TelemetryMenu.PickSources.Length);
        }

        [Test]
        public void Sections_Are_Three_On_Every_Source_Sitl_Included_And_None_With_The_Telemetry_Off()
        {
            CollectionAssert.AreEqual(new[] { TGroup.Data, TGroup.Flights, TGroup.Tools }, TelemetryMenu.GroupsFor(MenuSurface.Replay));
            CollectionAssert.AreEqual(new[] { TGroup.Data, TGroup.Flights, TGroup.Tools }, TelemetryMenu.GroupsFor(MenuSurface.Own));
            CollectionAssert.AreEqual(new[] { TGroup.Data, TGroup.Cmd, TGroup.Tools }, TelemetryMenu.GroupsFor(MenuSurface.Sim));
            CollectionAssert.AreEqual(new[] { TGroup.Data, TGroup.Cmd, TGroup.Tools }, TelemetryMenu.GroupsFor(MenuSurface.Sitl)); // sin tarjeta «PX4»
            Assert.IsEmpty(TelemetryMenu.GroupsFor(MenuSurface.Inspector));
        }

        [Test]
        public void Flights_Has_Only_My_Sessions_And_The_Sample_Flight_And_Only_On_A_Replayed_Flight()
        {
            CollectionAssert.AreEqual(new[] { TCard.Sessions, TCard.Sample }, TelemetryMenu.CardsOf(TGroup.Flights, MenuSurface.Replay));
            CollectionAssert.AreEqual(new[] { TCard.Sessions, TCard.Sample }, TelemetryMenu.CardsOf(TGroup.Flights, MenuSurface.Own));
            foreach (var s in new[] { MenuSurface.Sim, MenuSurface.Sitl, MenuSurface.Inspector })
                Assert.IsEmpty(TelemetryMenu.CardsOf(TGroup.Flights, s), s.ToString());
        }

        [Test]
        public void Every_Card_Is_In_A_Section_Of_Every_Source_Where_It_Exists()
        {
            foreach (var c in TelemetryMenu.TCards)
            {
                Assert.IsNotEmpty(TelemetryMenu.GroupsOf(c), c.ToString());
                foreach (var s in TelemetryMenu.SurfacesOf(c))
                    Assert.IsTrue(TelemetryMenu.GroupsOf(c).Any(g => TelemetryMenu.GroupsFor(s).Contains(g)), c + " on " + s);
            }
        }

        [Test]
        public void Nothing_Is_Lost_Every_Card_Shows_On_Every_Source_Where_It_Exists()
        {
            foreach (var c in TelemetryMenu.TCards)
                foreach (var s in TelemetryMenu.SurfacesOf(c))
                    Assert.IsTrue(TelemetryMenu.GroupsFor(s).Any(g => TelemetryMenu.CardsOf(g, s).Contains(c)), c + " on " + s);
        }

        [Test]
        public void What_Goes_Where_Data_Control_Flights_Tools()
        {
            CollectionAssert.AreEqual(new[] { TCard.Switch, TCard.Telemetry, TCard.Battery, TCard.Charts, TCard.Provenance, TCard.Alerts }, TelemetryMenu.CardsOf(TGroup.Data, MenuSurface.Sim));
            CollectionAssert.AreEqual(new[] { TCard.Telemetry, TCard.Pilot, TCard.Commands, TCard.SimFaults, TCard.Explain }, TelemetryMenu.CardsOf(TGroup.Cmd, MenuSurface.Sim));
            CollectionAssert.AreEqual(new[] { TCard.Telemetry, TCard.Pilot, TCard.Commands, TCard.Explain }, TelemetryMenu.CardsOf(TGroup.Cmd, MenuSurface.Sitl));
            CollectionAssert.AreEqual(new[] { TCard.Tools, TCard.Faults }, TelemetryMenu.CardsOf(TGroup.Tools, MenuSurface.Replay));
            CollectionAssert.AreEqual(new[] { TCard.Tools }, TelemetryMenu.CardsOf(TGroup.Tools, MenuSurface.Sitl));
        }

        [Test]
        public void No_Section_Shows_More_Than_Eight_Cards_And_A_Source_Never_Has_More_Than_Three_Sections()
        {
            foreach (var s in All)
            {
                Assert.LessOrEqual(TelemetryMenu.GroupsFor(s).Count, 3, s.ToString());
                foreach (var g in TelemetryMenu.GroupsFor(s)) Assert.LessOrEqual(TelemetryMenu.CardsOf(g, s).Count, 8, g + " on " + s);
            }
        }

        [Test]
        public void Toggle_Closes_The_Open_Section_And_A_Section_The_Source_Lacks_Reads_As_None()
        {
            Assert.IsNull(TelemetryMenu.Toggle(TGroup.Data, TGroup.Data));
            Assert.AreEqual(TGroup.Tools, TelemetryMenu.Toggle(TGroup.Data, TGroup.Tools));
            Assert.AreEqual(TGroup.Data, TelemetryMenu.Toggle(null, TGroup.Data));
            Assert.IsNull(TelemetryMenu.GroupOn(TGroup.Cmd, MenuSurface.Replay));
            Assert.IsNull(TelemetryMenu.GroupOn(TGroup.Flights, MenuSurface.Sim));
            Assert.AreEqual(TGroup.Cmd, TelemetryMenu.GroupOn(TGroup.Cmd, MenuSurface.Sitl));
        }
    }
}
