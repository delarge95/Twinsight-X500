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
        public void Inspector_Has_No_Tool_Only_The_Four_Source_Pills()
        {
            // PLAN_PC 42 (A): con la telemetria apagada solo se ofrecen las cuatro fuentes; ni Suelo (no hay nada que reflejar) ni Grabar ni Alertas
            Assert.IsEmpty(TelemetryMenu.ToolsFor(MenuSurface.Inspector));
            var v = TelemetryMenu.ViewFor(MenuSurface.Inspector, TSection.Cmd, false, false);
            CollectionAssert.AreEqual(new[] { SourcePill.Replay, SourcePill.Sim, SourcePill.Sitl, SourcePill.Own }, v.Pills);
            Assert.IsEmpty(v.Tools);
            Assert.IsEmpty(v.Sections);
            Assert.IsFalse(v.More);
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
                ToolId.Ground, ToolId.Map, ToolId.Preflight, ToolId.Mission, ToolId.Sessions,
                ToolId.Alerts, ToolId.Faults, ToolId.Analysis, ToolId.Record,
            }, TelemetryMenu.ToolsFor(MenuSurface.Sitl));
        }

        [Test]
        public void Replay_And_Own_Take_No_Orders_No_Faults_No_Record_But_Alerts_From_The_Log()
        {
            // PLAN_PC 42 (B): sin Fallas (en la repeticion el dron no reacciona), sin Grabar; Alertas si (el vuelo de muestra produce motor4:out)
            var expected = new[] { ToolId.Ground, ToolId.Map, ToolId.Preflight, ToolId.Alerts, ToolId.Analysis };
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
        // ───────────────────────── PLAN_PC 42 ─────────────────────────

        [Test]
        public void Running_Shows_Only_The_Active_Source_Pill_And_Pressing_It_Again_Turns_Off()
        {
            Assert.AreEqual(SourcePill.Sim, TelemetryMenu.SourcePillsFor(MenuSurface.Sim).Single());
            Assert.AreEqual(SourcePill.Own, TelemetryMenu.SourcePillsFor(MenuSurface.Own).Single());
            Assert.AreEqual(SourcePill.Sitl, TelemetryMenu.SourcePillsFor(MenuSurface.Sitl).Single());
            Assert.AreEqual(SourcePill.Replay, TelemetryMenu.SourcePillsFor(MenuSurface.Replay).Single());
            Assert.IsNull(TelemetryMenu.PressPill(MenuSurface.Sim, SourcePill.Sim));
            Assert.IsNull(TelemetryMenu.PressPill(MenuSurface.Own, SourcePill.Own));
            Assert.AreEqual(SourcePill.Sitl, TelemetryMenu.PressPill(MenuSurface.Inspector, SourcePill.Sitl));
        }

        [Test]
        public void Faults_Only_Where_The_Drone_Reacts_And_Record_Only_Where_It_Can_Be_Recorded()
        {
            CollectionAssert.AreEqual(new[] { MenuSurface.Sim, MenuSurface.Sitl }, All.Where(s => TelemetryMenu.Has(ToolId.Faults, s)).ToList());
            CollectionAssert.AreEqual(new[] { MenuSurface.Sim, MenuSurface.Sitl }, All.Where(s => TelemetryMenu.Has(ToolId.Sessions, s)).ToList());
            CollectionAssert.AreEqual(new[] { MenuSurface.Replay, MenuSurface.Own, MenuSurface.Sim, MenuSurface.Sitl }, All.Where(s => TelemetryMenu.Has(ToolId.Alerts, s)).ToList());
            Assert.IsFalse(TelemetryMenu.Has(ToolId.Ground, MenuSurface.Inspector));
        }

        [Test]
        public void Sections_Group_The_Pills_And_Only_Those_With_Something_Appear()
        {
            CollectionAssert.AreEqual(new[] { TSection.Cmd, TSection.Scene, TSection.Tools }, TelemetryMenu.SectionsFor(MenuSurface.Sim));
            CollectionAssert.AreEqual(new[] { TSection.Cmd, TSection.Scene, TSection.Tools }, TelemetryMenu.SectionsFor(MenuSurface.Sitl));
            CollectionAssert.AreEqual(new[] { TSection.Scene, TSection.Tools }, TelemetryMenu.SectionsFor(MenuSurface.Replay));
            Assert.IsEmpty(TelemetryMenu.SectionsFor(MenuSurface.Inspector));
            Assert.AreEqual(TSection.Cmd, TelemetryMenu.DefaultSection(MenuSurface.Sim));
            Assert.IsNull(TelemetryMenu.DefaultSection(MenuSurface.Replay));
            Assert.IsNull(TelemetryMenu.ToggleSection(TSection.Scene, TSection.Scene));
            Assert.AreEqual(TSection.Tools, TelemetryMenu.ToggleSection(TSection.Scene, TSection.Tools));
            foreach (var id in TelemetryMenu.ToolIds)
            {
                bool row1 = id == ToolId.Alerts || id == ToolId.Faults || id == ToolId.Analysis || id == ToolId.Record;
                Assert.AreEqual(row1, TelemetryMenu.SectionOf(id) == null, id.ToString());
            }
        }

        [Test]
        public void One_Section_Open_At_A_Time_Shows_Only_Its_Pills_Plus_The_Row1_Pills()
        {
            var v = TelemetryMenu.ViewFor(MenuSurface.Sim, TSection.Scene, false, false);
            CollectionAssert.AreEqual(new[] { ToolId.Ground, ToolId.Course, ToolId.Fence, ToolId.Wind, ToolId.Battery, ToolId.Alerts, ToolId.Faults, ToolId.Analysis }, v.Tools);
            CollectionAssert.AreEqual(new[] { SourcePill.Sim }, v.Pills);
            Assert.AreEqual(3, v.Sections.Count);
            var closed = TelemetryMenu.ViewFor(MenuSurface.Replay, null, false, false);
            CollectionAssert.AreEqual(new[] { ToolId.Alerts, ToolId.Analysis }, closed.Tools);
            // a section the source lacks reads as none
            Assert.IsNull(TelemetryMenu.ViewFor(MenuSurface.Replay, TSection.Cmd, false, false).Open);
        }

        [Test]
        public void Piloting_Is_Compact_With_Hold_And_Return_Always_Visible()
        {
            foreach (var s in new[] { MenuSurface.Sim, MenuSurface.Sitl })
                foreach (TSection? open in new TSection?[] { null, TSection.Cmd, TSection.Scene, TSection.Tools })
                    foreach (bool more in new[] { false, true })
                    {
                        var v = TelemetryMenu.ViewFor(s, open, true, more);
                        Assert.IsTrue(v.Tools.Contains(ToolId.Hold) && v.Tools.Contains(ToolId.Rtl) && v.Tools.Contains(ToolId.Takeoff) && v.Tools.Contains(ToolId.Land), s + " " + open + " " + more);
                        Assert.IsTrue(v.Tools.Contains(ToolId.Alerts), "alerts are a dynamic warning: always visible");
                        Assert.IsTrue(v.More);
                        if (!more)
                        {
                            Assert.IsEmpty(v.Sections);
                            Assert.IsFalse(v.Tools.Contains(ToolId.Faults) || v.Tools.Contains(ToolId.Analysis) || v.Tools.Contains(ToolId.Ground) || v.Tools.Contains(ToolId.Map));
                        }
                    }
            var compact = TelemetryMenu.ViewFor(MenuSurface.Sim, TSection.Tools, true, false);
            Assert.AreEqual(8, compact.Tools.Count); // 7 de Mando (2 dentro de «Avanzado») + Alertas
            var expanded = TelemetryMenu.ViewFor(MenuSurface.Sim, TSection.Tools, true, true);
            CollectionAssert.IsSubsetOf(new[] { ToolId.Map, ToolId.Mission, ToolId.Hold, ToolId.Rtl }, expanded.Tools);
        }

        [Test]
        public void Nothing_Is_Lost_Every_Tool_Of_A_Source_Is_Reachable_From_Some_Section_State()
        {
            foreach (var s in All)
                foreach (var id in TelemetryMenu.ToolsFor(s))
                {
                    if (id == ToolId.Record) continue; // dentro del panel «Grabar»
                    bool reach = false;
                    foreach (TSection? open in new TSection?[] { null, TSection.Cmd, TSection.Scene, TSection.Tools })
                        reach |= TelemetryMenu.ViewFor(s, open, false, false).Tools.Contains(id) || TelemetryMenu.ViewFor(s, open, true, true).Tools.Contains(id);
                    Assert.IsTrue(reach, id + " on " + s);
                }
        }
    }
}
