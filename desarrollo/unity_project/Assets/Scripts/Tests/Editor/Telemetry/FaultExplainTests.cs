// PLAN_PC 27.3: the fault / alert explanation card. The wording is the APPROVED one of PLAN_CONTROL_REAL_Y_ENTORNOS 2.4 (D1); these tests pin the
// pieces that must not drift: every topic has its four blocks in ES and EN for every source, "En esta fuente" tells the truth of each source,
// nothing is called a measurement, and an alert without an approved text is neutral.
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace WebGL.Telemetry.Tests
{
    public class FaultExplainTests
    {
        static readonly FaultTopic[] Topics = { FaultTopic.MotorFailure, FaultTopic.MotorOverheat, FaultTopic.BatterySag, FaultTopic.Vibration, FaultTopic.Neutral };
        static readonly ExplainSource[] Sources = { ExplainSource.LocalReplay, ExplainSource.Simulator, ExplainSource.Sitl };
        static readonly string[] Langs = { "es", "en" };

        [Test]
        public void EveryTopic_HasMeansAndController_InBothLanguages()
        {
            foreach (var t in Topics)
                foreach (var l in Langs)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(FaultExplain.Means(t, l)), t + " means " + l);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(FaultExplain.Controller(t, l)), t + " controller " + l);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(FaultExplain.ControllerHeading(t, l)), t + " heading " + l);
                }
        }

        [Test]
        public void EveryTopicAndSource_HasAnInSourceText_InBothLanguages()
        {
            foreach (var t in Topics)
                foreach (var s in Sources)
                    foreach (var l in Langs)
                        Assert.IsFalse(string.IsNullOrWhiteSpace(FaultExplain.InSource(t, s, 3, l)), t + " " + s + " " + l);
            Assert.IsNull(FaultExplain.InSource(FaultTopic.MotorFailure, ExplainSource.None, 1, "es"), "no session: no block");
        }

        [Test]
        public void ReplayText_SaysTheTrajectoryDoesNotReact_AndTheOtherMotorsDoNotCompensate()
        {
            string es = FaultExplain.InSource(FaultTopic.MotorFailure, ExplainSource.LocalReplay, 3, "es");
            StringAssert.Contains("Falla sintética", es);
            StringAssert.Contains("motor 3", es);
            StringAssert.Contains("30 s", es);
            StringAssert.Contains("no reaccionan", es);
            StringAssert.Contains("tampoco compensan", es);
            string en = FaultExplain.InSource(FaultTopic.MotorFailure, ExplainSource.LocalReplay, 3, "en");
            StringAssert.Contains("Synthetic fault", en);
            StringAssert.Contains("do not react", en);
            StringAssert.Contains("do not compensate", en);
        }

        [Test]
        public void SimulatorText_SaysItDoesNotInjectFaults_ExceptTheBatteryWhichHasItsOwnApprovedText()
        {
            foreach (var t in new[] { FaultTopic.MotorFailure, FaultTopic.MotorOverheat, FaultTopic.Vibration, FaultTopic.Neutral })
            {
                StringAssert.Contains("no inyecta fallas", FaultExplain.InSource(t, ExplainSource.Simulator, 1, "es"));
                StringAssert.Contains("does not inject faults", FaultExplain.InSource(t, ExplainSource.Simulator, 1, "en"));
            }
            StringAssert.Contains("no reacciona a la batería baja", FaultExplain.InSource(FaultTopic.BatterySag, ExplainSource.Simulator, 1, "es"));
            StringAssert.Contains("does not react to low charge", FaultExplain.InSource(FaultTopic.BatterySag, ExplainSource.Simulator, 1, "en"));
        }

        [Test]
        public void SitlMotorText_SaysTheMotorIsReallyCutInSitl_AndTheRpmAreThePx4Command()
        {
            string es = FaultExplain.InSource(FaultTopic.MotorFailure, ExplainSource.Sitl, 2, "es");
            StringAssert.Contains("MAV_CMD_INJECT_FAILURE", es);
            StringAssert.Contains("corta de verdad su empuje", es);
            StringAssert.Contains("hay que reiniciar el SITL", es);
            StringAssert.Contains("no una afirmación sobre un dron físico", es);
            StringAssert.DoesNotContain("puede no detener", es);
            StringAssert.Contains("son la orden de PX4", es);
            string en = FaultExplain.InSource(FaultTopic.MotorFailure, ExplainSource.Sitl, 2, "en");
            StringAssert.Contains("really cuts its thrust", en);
            StringAssert.Contains("not a claim about a physical drone", en);
            StringAssert.Contains("PX4's command", en);
        }

        [Test]
        public void ControllerText_SaysAStandardControllerCannotFlyOnThreeMotors()
        {
            StringAssert.Contains("no está hecho para volar con tres motores", FaultExplain.Controller(FaultTopic.MotorFailure, "es"));
            StringAssert.Contains("not part of standard PX4", FaultExplain.Controller(FaultTopic.MotorFailure, "en"));
            Assert.AreEqual("Qué haría el controlador", FaultExplain.ControllerHeading(FaultTopic.MotorFailure, "es"));
            Assert.AreEqual("Qué haría el piloto", FaultExplain.ControllerHeading(FaultTopic.MotorOverheat, "es"), "heat and vibration: the approved text is about the pilot");
        }

        [Test]
        public void TheDidacticLabels_AreTheApprovedOnes_AndNoneIsAMeasurement()
        {
            Assert.AreEqual("Explicación didáctica", TelemetryStrings.T("fx.title", "es"));
            Assert.AreEqual("Explicación general · no es una medición", TelemetryStrings.T("fx.disclaimer", "es"));
            Assert.AreEqual("General explanation · not a measurement", TelemetryStrings.T("fx.disclaimer", "en"));
        }

        [Test]
        public void ApprovedTextsCarryNoApprovalMarkers_AndNoUnqualifiedTwin()
        {
            var ban = new Regex(@"\[APROBAR\]|\[NO VERIFICADO\]|gemelo(\s+digital)?|digital\s+twin", RegexOptions.IgnoreCase);
            foreach (var kv in TelemetryStrings.All)
            {
                if (!kv.Key.StartsWith("fx.")) continue;
                foreach (string v in kv.Value) Assert.IsFalse(ban.IsMatch(v), kv.Key + ": " + v);
            }
        }

        [Test]
        public void TopicOfAlert_FollowsTheAlertIds()
        {
            Assert.AreEqual(FaultTopic.MotorFailure, FaultExplain.TopicOfAlert("motor3:out"));
            Assert.AreEqual(FaultTopic.MotorOverheat, FaultExplain.TopicOfAlert("motor2:temp"));
            Assert.AreEqual(FaultTopic.BatterySag, FaultExplain.TopicOfAlert("battery:rem"));
            Assert.AreEqual(FaultTopic.Vibration, FaultExplain.TopicOfAlert("fc:vib"));
            Assert.AreEqual(FaultTopic.Neutral, FaultExplain.TopicOfAlert("fc:temp"), "controller temperature: no approved text");
            Assert.AreEqual(FaultTopic.Neutral, FaultExplain.TopicOfAlert("battery:temp"), "battery temperature: no approved text");
            Assert.AreEqual(FaultTopic.Neutral, FaultExplain.TopicOfAlert(null));
            Assert.IsFalse(FaultExplain.IsApproved(FaultTopic.Neutral));
            Assert.IsTrue(FaultExplain.IsApproved(FaultTopic.MotorFailure));
        }

        [Test]
        public void EveryFaultKind_MapsToATopic_AndBack()
        {
            foreach (FaultKind k in new[] { FaultKind.MotorFailure, FaultKind.MotorOverheat, FaultKind.BatterySag, FaultKind.Vibration })
                Assert.AreEqual(k, FaultExplain.KindOf(FaultExplain.TopicOf(k)));
        }

        [Test]
        public void StatesAndPins_NameTheMotor()
        {
            Assert.AreEqual("Motor 3 cortado", FaultExplain.StateName(FaultKind.MotorFailure, 3, "es"));
            Assert.AreEqual("Motor 3 cut off", FaultExplain.StateName(FaultKind.MotorFailure, 3, "en"));
            Assert.AreEqual("Motor 3 · detenido", FaultExplain.PinName(FaultKind.MotorFailure, 3, "es"));
            Assert.AreEqual("Caída de batería", FaultExplain.StateName(FaultKind.BatterySag, 0, "es"));
        }

        [Test]
        public void Countdown_IsTheSecondsLeftOfThe30sFault_NeverNegative()
        {
            var f = new Fault { id = "motor_failure:3", kind = FaultKind.MotorFailure, motor = 3, HasMotor = true, since = 1000, until = 31000 };
            Assert.AreEqual(30, FaultExplain.SecondsLeft(f, 1000));
            Assert.AreEqual(24, FaultExplain.SecondsLeft(f, 7000));
            Assert.AreEqual(0, FaultExplain.SecondsLeft(f, 99000));
            StringAssert.Contains("24 s", FaultExplain.FaultLine(f, 7000, "es"));
            StringAssert.Contains("Motor 3 cortado", FaultExplain.FaultLine(f, 7000, "es"));
        }

        [Test]
        public void AffectedReadings_CarryTheirOrigin_AndASimulatorIsAlwaysSimulated()
        {
            var h = new HelloMessage { mode = TelemetryMode.Replay, batteryCells = 4 };
            h.provenance = new Provenance { rpm = "estimated", motorTemp = "estimated", batteryTemp = "estimated", fcTemp = "measured" };
            var rep = FaultExplain.Affected(FaultTopic.MotorFailure, "motor3:out", h, SourceClassification.Replay, false);
            Assert.AreEqual("fx.rd.rpm", rep[0].LabelKey);
            Assert.AreEqual(ReadingOrigin.Estimated, rep[0].Origin);
            Assert.AreEqual(ReadingOrigin.Command, rep[1].Origin);
            var sim = FaultExplain.Affected(FaultTopic.MotorFailure, "motor3:out", h, SourceClassification.Sitl, true);
            Assert.AreEqual(ReadingOrigin.Simulated, sim[0].Origin, "in the flight simulator every reading is simulated");
            var bat = FaultExplain.Affected(FaultTopic.BatterySag, "battery:rem", h, SourceClassification.Replay, false);
            Assert.AreEqual(ReadingOrigin.Measured, bat[0].Origin);
            var fc = FaultExplain.Affected(FaultTopic.Neutral, "fc:temp", h, SourceClassification.Replay, false);
            Assert.AreEqual("fx.rd.fctemp", fc[0].LabelKey);
            Assert.AreEqual(ReadingOrigin.Measured, fc[0].Origin);
        }
    }
}
