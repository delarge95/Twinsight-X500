// WP-11 / E-3: origins and texts of the desktop telemetry UI (FlightText.cs) and the honesty scan of the whole UI text.
//  - FlightOrigins: the provenance of each reading as the web assigns it; a "measured" channel of SITL is "simulado".
//  - AlertText: the approved D-4 alert wording with origin and "falla sintética".
//  - TelemetryStrings: every key in ES and EN, no unqualified "gemelo digital" / "digital twin".
//  - Scan (E-3): no unqualified "gemelo digital" / "digital twin" in any user-visible string of the project (code string literals, UXML,
//    USS content, JSON resources). Allowed forms: "Gemelo digital SITL", "SITL digital twin", "no es un gemelo digital del dron físico", ...
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace WebGL.Telemetry.Tests
{
    public class FlightTextTests
    {
        static HonestyLabels Labels() => HonestyLabels.Parse(C.Text("labels.json"));

        static HelloMessage Hello(string rpm = "estimated", string motorTemp = "estimated", string batteryTemp = "estimated", string fcTemp = "measured", string current = null)
        {
            var h = new HelloMessage { mode = TelemetryMode.Replay, batteryCells = 4 };
            h.provenance = new Provenance { rpm = rpm, motorTemp = motorTemp, batteryTemp = batteryTemp, fcTemp = fcTemp };
            if (current != null) { h.provenance.batteryCurrent = current; h.provenance.HasBatteryCurrent = true; }
            return h;
        }

        static TelemetryFrame Flying()
        {
            var f = new TelemetryFrame { armed = true, landed = false, vib = 5 };
            f.rpm = new double[] { 5400, 5400, 40, 5400 };
            f.temp = new double[] { 40, 41, 42, 43, 30, 35 };
            f.bat = new BatteryState { v = 15.1, i = 12, rem = 0.8 };
            return f;
        }

        // ───────────────────────────── origins ─────────────────────────────

        [Test]
        public void Origins_FollowHelloProvenance_AsTheWebAssignsThem()
        {
            var h = Hello(rpm: "estimated", motorTemp: "estimated", batteryTemp: "measured", fcTemp: "unavailable", current: "estimated");
            var r = SourceClassification.Replay;
            Assert.AreEqual(ReadingOrigin.Estimated, FlightOrigins.Rpm(h, r));
            Assert.AreEqual(ReadingOrigin.Estimated, FlightOrigins.MotorTemp(h, r));
            Assert.AreEqual(ReadingOrigin.Measured, FlightOrigins.BatteryTemp(h, r));
            Assert.AreEqual(ReadingOrigin.Unavailable, FlightOrigins.FcTemp(h, r));
            Assert.AreEqual(ReadingOrigin.Estimated, FlightOrigins.BatteryCurrent(h, r));
            Assert.AreEqual(ReadingOrigin.Command, FlightOrigins.Rpm(Hello(rpm: "command"), r));
        }

        [Test]
        public void Origins_BatteryCurrentAbsent_IsMeasured()
        {
            Assert.AreEqual(ReadingOrigin.Measured, FlightOrigins.BatteryCurrent(Hello(), SourceClassification.Replay));
        }

        [Test]
        public void Origins_MeasuredOnSitl_IsSimulated_ButEstimatedStaysEstimated()
        {
            var h = Hello(rpm: "measured", motorTemp: "estimated", batteryTemp: "measured", fcTemp: "measured");
            var s = SourceClassification.Sitl;
            Assert.AreEqual(ReadingOrigin.Simulated, FlightOrigins.Rpm(h, s));
            Assert.AreEqual(ReadingOrigin.Estimated, FlightOrigins.MotorTemp(h, s));
            Assert.AreEqual(ReadingOrigin.Simulated, FlightOrigins.BatteryTemp(h, s));
            Assert.AreEqual(ReadingOrigin.Simulated, FlightOrigins.FcTemp(h, s));
            Assert.AreEqual(ReadingOrigin.Simulated, FlightOrigins.Base(s));
            Assert.AreEqual(ReadingOrigin.Measured, FlightOrigins.Base(SourceClassification.Replay));
        }

        [Test]
        public void OriginWords_ComeFromLabelsJson()
        {
            var l = Labels();
            Assert.AreEqual("medido", FlightOrigins.Text(ReadingOrigin.Measured, "es", l));
            Assert.AreEqual("estimado", FlightOrigins.Text(ReadingOrigin.Estimated, "es", l));
            Assert.AreEqual("simulado", FlightOrigins.Text(ReadingOrigin.Simulated, "es", l));
            Assert.AreEqual("comando", FlightOrigins.Text(ReadingOrigin.Command, "es", l));
            Assert.AreEqual("no disponible", FlightOrigins.Text(ReadingOrigin.Unavailable, "es", l));
            Assert.AreEqual("estimated", FlightOrigins.Text(ReadingOrigin.Estimated, "en", l));
            Assert.AreEqual("simulated", FlightOrigins.Text(ReadingOrigin.Simulated, "en", l));
        }

        // ───────────────────────────── alert text ─────────────────────────────

        [Test]
        public void AlertText_MotorOut_SaysEstimatedRpm_AndSynthetic()
        {
            var f = Flying();
            var a = Alerts.Derive(f).Find(x => x.id == "motor3:out");
            Assert.NotNull(a);
            var l = Labels();
            string es = AlertText.Build(a, f, Hello(), SourceClassification.Replay, true, "es", l);
            Assert.AreEqual("Motor 3: RPM estimadas muy bajas (40 frente a 5400 de media) · falla sintética", es);
            string en = AlertText.Build(a, f, Hello(), SourceClassification.Replay, false, "en", l);
            Assert.AreEqual("Motor 3: estimated RPM very low (40 vs. 5400 mean)", en);
        }

        [Test]
        public void AlertText_Temperatures_CarryTheirOrigin()
        {
            var f = Flying();
            f.temp[0] = 72; f.temp[4] = 55; f.temp[5] = 71;
            var l = Labels();
            var list = Alerts.Derive(f);
            var h = Hello(batteryTemp: "measured");
            Assert.AreEqual("Motor 1: 72 °C (estimado)", AlertText.Build(list.Find(x => x.id == "motor1:temp"), f, h, SourceClassification.Replay, false, "es", l));
            Assert.AreEqual("Batería: 55 °C (medido)", AlertText.Build(list.Find(x => x.id == "battery:temp"), f, h, SourceClassification.Replay, false, "es", l));
            Assert.AreEqual("Battery: 55 °C (measured)", AlertText.Build(list.Find(x => x.id == "battery:temp"), f, h, SourceClassification.Replay, false, "en", l));
            // fc temperature and vibration keep the plain text on a replay, and say "simulado" on SITL
            Assert.AreEqual("Controladora: 71 °C", AlertText.Build(list.Find(x => x.id == "fc:temp"), f, h, SourceClassification.Replay, false, "es", l));
            Assert.AreEqual("Controladora: 71 °C (simulado)", AlertText.Build(list.Find(x => x.id == "fc:temp"), f, h, SourceClassification.Sitl, false, "es", l));
        }

        [Test]
        public void AlertText_Battery_SitlIsSimulated()
        {
            var f = Flying();
            f.bat.rem = 0.12; f.bat.v = 14.1;
            var a = Alerts.Derive(f).Find(x => x.id == "battery:rem");
            var l = Labels();
            Assert.AreEqual("Batería al 12 % (14.1 V)", AlertText.Build(a, f, Hello(), SourceClassification.Replay, false, "es", l));
            Assert.AreEqual("Batería al 12 % (14.1 V) (simulado)", AlertText.Build(a, f, Hello(), SourceClassification.Sitl, false, "es", l));
            Assert.AreEqual("Battery at 12 % (14.1 V) (simulated)", AlertText.Build(a, f, Hello(), SourceClassification.Sitl, false, "en", l));
        }

        [Test]
        public void AlertText_SyntheticFaultMapping()
        {
            var l = Labels();
            Assert.AreEqual("falla sintética", AlertText.SyntheticFault("es", l));
            Assert.AreEqual("synthetic fault", AlertText.SyntheticFault("en", l));
            var faults = new List<Fault>
            {
                new Fault { id = "motor_failure:3", kind = FaultKind.MotorFailure, motor = 3, HasMotor = true, since = 0, until = 30000 },
                new Fault { id = "vibration", kind = FaultKind.Vibration, since = 0, until = 100 },
            };
            Assert.IsTrue(AlertText.IsSynthetic(new Alert { id = "motor3:out", component = "motor3" }, faults, 1000));
            Assert.IsFalse(AlertText.IsSynthetic(new Alert { id = "motor1:out", component = "motor1" }, faults, 1000));
            Assert.IsFalse(AlertText.IsSynthetic(new Alert { id = "fc:vib", component = "fc" }, faults, 1000), "the vibration fault expired");
            Assert.IsTrue(AlertText.IsSynthetic(new Alert { id = "fc:vib", component = "fc" }, faults, 50));
        }

        // ───────────────────────────── strings ─────────────────────────────

        [Test]
        public void Strings_EveryKeyHasBothLanguages_AndTheD6Names()
        {
            int n = 0;
            foreach (var kv in TelemetryStrings.All)
            {
                n++;
                Assert.AreEqual(2, kv.Value.Length, kv.Key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(kv.Value[0]), kv.Key + " es");
                Assert.IsFalse(string.IsNullOrWhiteSpace(kv.Value[1]), kv.Key + " en");
            }
            Assert.Greater(n, 80);
            Assert.AreEqual("Telemetría", TelemetryStrings.T("mode4", "es"));
            Assert.AreEqual("Vuelo", TelemetryStrings.T("tab.flight", "es"));
            Assert.AreEqual("Encendido", TelemetryStrings.T("power.switch", "es"));
            Assert.AreEqual("Energía (simulada)", TelemetryStrings.T("power.sim", "es"));
            Assert.AreEqual("CARGA ALTA (SIMULADO)", TelemetryStrings.T("power.highLoad", "es"));
            Assert.AreEqual("HIGH LOAD (SIMULATED)", TelemetryStrings.T("power.highLoad", "en"));
            Assert.AreEqual("Próximamente", TelemetryStrings.T("src.sim.soon", "es"));
            Assert.AreEqual("Inspector (sin telemetría)", TelemetryStrings.T("src.inspector", "es"));
        }

        [Test]
        public void Strings_NoPowerWordLeftInSpanish_AndNoFlyingForTheInspector()
        {
            foreach (var kv in TelemetryStrings.All)
            {
                Assert.IsFalse(Regex.IsMatch(kv.Value[0], @"\bPOWER\b"), kv.Key + ": 'POWER' must read Energía / Encendido in Spanish (D-6)");
                Assert.IsFalse(Regex.IsMatch(kv.Value[0] + kv.Value[1], @"\b(VOLANDO|FLYING)\b"), kv.Key + ": 'VOLANDO'/'FLYING' is 'Carga alta (simulado)' in the Inspector (D-6)");
            }
        }

        [Test]
        public void LocalReplayDetail_SaysThisApp_NotTheBrowser()
        {
            var l = Labels();
            string es = TelemetryStrings.LocalReplayDetail(l, "es");
            string en = TelemetryStrings.LocalReplayDetail(l, "en");
            StringAssert.Contains("esta aplicación", es);
            StringAssert.DoesNotContain("navegador", es);
            StringAssert.Contains("this app", en);
            StringAssert.DoesNotContain("browser", en);
            StringAssert.Contains("sintéticas", es);
        }

        [Test]
        public void ForDesktop_OnlyReplacesTheBrowserPhrases_OfTheSitlDetail()
        {
            var l = Labels();
            string es = TelemetryStrings.ForDesktop(l.ClassificationDetail(SourceClassification.Sitl, "es"), "es");
            string en = TelemetryStrings.ForDesktop(l.ClassificationDetail(SourceClassification.Sitl, "en"), "en");
            StringAssert.Contains("la aplicación envía comandos MAVLink", es);
            StringAssert.DoesNotContain("navegador", es);
            StringAssert.Contains("the app sends MAVLink commands", en);
            StringAssert.DoesNotContain("browser", en);
            // untouched where there is no web phrase
            string insp = l.ClassificationTitle(SourceClassification.Inspector, "es");
            Assert.AreEqual(insp, TelemetryStrings.ForDesktop(insp, "es"));
        }

        // ───────────────────────────── honesty scan (E-3) ─────────────────────────────

        static readonly Regex Banned = new Regex(@"gemelo(\s+digital)?|digital\s+twin", RegexOptions.IgnoreCase);

        // Allowed forms: qualified as SITL, or the explicit denial that this is a twin of the physical drone.
        static readonly Regex[] Allowed =
        {
            new Regex(@"gemelo\s+digital\s+SITL", RegexOptions.IgnoreCase),
            new Regex(@"SITL\s+digital\s+twin", RegexOptions.IgnoreCase),
            new Regex(@"no\s+es\s+un\s+gemelo(\s+digital)?(\s+del?\s+un?\s+dron\s+f[ií]sico)?", RegexOptions.IgnoreCase),
            new Regex(@"not\s+a\s+(digital\s+)?twin(\s+of\s+a\s+physical\s+drone)?", RegexOptions.IgnoreCase),
        };

        public static List<string> Offenders(string text)
        {
            string clean = text;
            foreach (var a in Allowed) clean = a.Replace(clean, " ");
            var r = new List<string>();
            foreach (Match m in Banned.Matches(clean))
            {
                int s = Math.Max(0, m.Index - 30);
                r.Add(clean.Substring(s, Math.Min(clean.Length - s, 80)).Replace('\n', ' '));
            }
            return r;
        }

        [Test]
        public void Scan_FindsUnqualifiedTwin_AndAcceptsTheAllowedForms()
        {
            Assert.IsNotEmpty(Offenders("Gemelo digital web de un dron"));
            Assert.IsNotEmpty(Offenders("A digital twin of the X500"));
            Assert.IsNotEmpty(Offenders("el gemelo del dron"));
            Assert.IsEmpty(Offenders("Gemelo digital SITL"));
            Assert.IsEmpty(Offenders("SITL digital twin"));
            Assert.IsEmpty(Offenders("No es un gemelo de un dron físico"));
            Assert.IsEmpty(Offenders("no es un gemelo digital del dron físico"));
            Assert.IsEmpty(Offenders("It is not a twin of a physical drone"));
            Assert.IsEmpty(Offenders("Sombra digital · reproducción local"));
        }

        static string Assets => Application.dataPath;

        static IEnumerable<string> Files(string pattern)
        {
            foreach (string f in Directory.GetFiles(Assets, pattern, SearchOption.AllDirectories))
            {
                string n = f.Replace('\\', '/');
                if (n.Contains("/Tests/") || n.Contains("/Contracts~/") || n.Contains("/Screenshots")) continue;
                yield return n;
            }
        }

        static readonly Regex StringLiteral = new Regex("@?\"(?:[^\"\\\\\\n]|\\\\.)*\"");

        [Test]
        public void Scan_CodeStringLiterals_HaveNoUnqualifiedDigitalTwin()
        {
            var bad = new List<string>();
            foreach (string f in Files("*.cs"))
            {
                int line = 0;
                foreach (string raw in File.ReadAllLines(f))
                {
                    line++;
                    string t = raw.TrimStart();
                    if (t.StartsWith("//") || t.StartsWith("*") || t.StartsWith("/*")) continue; // comments are not user-visible
                    foreach (Match m in StringLiteral.Matches(raw))
                        foreach (string o in Offenders(m.Value)) bad.Add(f.Substring(f.IndexOf("Assets/", StringComparison.Ordinal)) + ":" + line + " -> " + o);
                }
            }
            Assert.IsEmpty(bad, "Unqualified 'gemelo digital' / 'digital twin' in code strings:\n" + string.Join("\n", bad));
        }

        [Test]
        public void Scan_UxmlAndUss_HaveNoUnqualifiedDigitalTwin()
        {
            var bad = new List<string>();
            foreach (string f in Files("*.uxml"))
            {
                string text = Regex.Replace(File.ReadAllText(f), @"<!--.*?-->", " ", RegexOptions.Singleline); // XML comments are not user-visible
                foreach (string o in Offenders(text)) bad.Add(f.Substring(f.IndexOf("Assets/", StringComparison.Ordinal)) + " -> " + o);
            }
            foreach (string f in Files("*.uss"))
            {
                string text = Regex.Replace(File.ReadAllText(f), @"/\*.*?\*/", " ", RegexOptions.Singleline);
                foreach (string o in Offenders(text)) bad.Add(f.Substring(f.IndexOf("Assets/", StringComparison.Ordinal)) + " -> " + o);
            }
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void Scan_JsonResources_HaveNoUnqualifiedDigitalTwin_AndTheLabelsKeepTheQualifiedForms()
        {
            var bad = new List<string>();
            foreach (string f in Files("*.json"))
            {
                if (!f.Contains("/Resources/") && !f.Contains("/StreamingAssets/")) continue;
                foreach (string o in Offenders(File.ReadAllText(f))) bad.Add(f.Substring(f.IndexOf("Assets/", StringComparison.Ordinal)) + " -> " + o);
            }
            Assert.IsEmpty(bad, string.Join("\n", bad));

            var l = Labels();
            Assert.AreEqual("Gemelo digital SITL", l.ClassificationTitle(SourceClassification.Sitl, "es"));
            Assert.AreEqual("SITL digital twin", l.ClassificationTitle(SourceClassification.Sitl, "en"));
            StringAssert.Contains("No es un gemelo de un dron físico", l.About(3, "es"));
        }

        [Test]
        public void Scan_TelemetryStrings_HaveNoUnqualifiedDigitalTwin()
        {
            var bad = new List<string>();
            foreach (var kv in TelemetryStrings.All)
                foreach (string v in kv.Value)
                    foreach (string o in Offenders(v)) bad.Add(kv.Key + " -> " + o);
            Assert.IsEmpty(bad);
        }

        [Test]
        public void Scan_UnityWebGlSourceText_IsTheKnownLegacyOnly()
        {
            // D-6 in the shared dictionary: the Inspector state FLYING reads HIGH LOAD (SIMULATED) on PC (WebGL keeps its legacy text, inert).
            string dict = File.ReadAllText(Path.Combine(Assets, "Scripts", "UI", "Panels", "AppLanguageManager.cs"));
            StringAssert.Contains("{ \"HIGH LOAD (SIMULATED)\", \"CARGA ALTA (SIMULADO)\" }", dict);
            StringAssert.Contains("{ \"POWER\", \"ENERG\\u00cdA\" }", dict);
            string insp = File.ReadAllText(Path.Combine(Assets, "Scripts", "UI", "Panels", "InspectModeHandler.cs"));
            StringAssert.Contains("DroneState.Flying => \"HIGH LOAD (SIMULATED)\"", insp);
            // hero tagline of D-2 in the shared UXML and in the code that rewrites it
            string uxml = File.ReadAllText(Path.Combine(Assets, "Scripts", "UI", "Layouts", "MainLayout.uxml"));
            StringAssert.Contains("VISOR 3D INTERACTIVO · HOLYBRO X500 V2", uxml);
        }
    }
}
