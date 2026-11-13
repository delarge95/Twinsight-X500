// E-7: the 7-step desktop tutorial. The 7th "Telemetría" step (between "Estudio" and "Listo", design/onboarding.json finalOrder) is desktop only:
//  - 7 steps in the .exe, 6 in WebGL / mobile (the PC-only step is filtered out);
//  - every row of every step and variant references an existing icon (OutlineIconData) and has its texts in ES and EN
//    (steps 1-6: the inline pairs; step 7: keys of TelemetryStrings, all present in both languages);
//  - the honesty words inside the hints repeat labels.json (Sombra digital, Simulador de vuelo, Gemelo digital SITL, medido/estimado/simulado, sintética).
using System.Collections.Generic;
using NUnit.Framework;
using WebGL.UI.Panels;
using WebGL.UI.ProceduralIcons;

namespace WebGL.Telemetry.Tests
{
    public class TutorialStepsTests
    {
        static HonestyLabels Labels() => HonestyLabels.Parse(C.Text("labels.json"));

        static readonly HashSet<string> Keys = BuildKeys();

        static HashSet<string> BuildKeys()
        {
            var h = new HashSet<string>();
            foreach (var kv in TelemetryStrings.All)
            {
                Assert.AreEqual(2, kv.Value.Length, kv.Key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(kv.Value[0]), kv.Key + " es");
                Assert.IsFalse(string.IsNullOrWhiteSpace(kv.Value[1]), kv.Key + " en");
                h.Add(kv.Key);
            }

            return h;
        }

        static IEnumerable<(string variant, TutorialOverlay.Row[] rows)> Variants(TutorialOverlay.Step s)
        {
            yield return ("pc", s.Pc);
            yield return ("mouse", s.Mouse);
            yield return ("touch", s.Touch);
        }

        [Test]
        public void Tutorial_HasSevenSteps_InTheDesktopBuild_AndSixInWebGL()
        {
            var all = TutorialOverlay.BuildSteps(true);
            Assert.AreEqual(7, all.Length, "7 steps in the .exe");
            Assert.AreEqual("flight", all[TutorialOverlay.TelemetryStepIndex].Icon);
            Assert.AreEqual("tut.tel.title", all[TutorialOverlay.TelemetryStepIndex].TitleKey);
            Assert.IsTrue(all[TutorialOverlay.TelemetryStepIndex].PcOnly);
            Assert.AreEqual("help", all[6].Icon, "'Listo' stays the last step");
            Assert.AreEqual("studio", all[4].Icon);
            Assert.AreEqual(6, TutorialOverlay.BuildSteps(false).Length, "WebGL / mobile keep the 6 steps");
        }

        [Test]
        public void EveryRow_ReferencesAnExistingIcon_AndHasEsAndEnTexts()
        {
            var bad = new List<string>();
            int rows = 0, keyed = 0;
            var all = TutorialOverlay.BuildSteps(true);
            for (int i = 0; i < all.Length; i++)
            {
                var s = all[i];
                if (!OutlineIconData.Paths.ContainsKey(s.Icon)) bad.Add("step " + (i + 1) + " icon " + s.Icon);
                bool stepKeys = s.TitleKey != null;
                if (stepKeys ? !(Keys.Contains(s.TitleKey) && Keys.Contains(s.DescKey)) : string.IsNullOrEmpty(s.TitleEs) || string.IsNullOrEmpty(s.TitleEn) || string.IsNullOrEmpty(s.DescEs) || string.IsNullOrEmpty(s.DescEn))
                    bad.Add("step " + (i + 1) + " title/desc");
                foreach (var (variant, rs) in Variants(s))
                {
                    if (rs == null || rs.Length < 3) { bad.Add("step " + (i + 1) + "/" + variant + ": fewer than 3 rows"); continue; }
                    foreach (var r in rs)
                    {
                        rows++;
                        string where = "step " + (i + 1) + "/" + variant + "/" + r.Icon;
                        if (!OutlineIconData.Paths.ContainsKey(r.Icon)) bad.Add(where + ": unknown icon");
                        if (r.NameKey != null)
                        {
                            keyed++;
                            if (!Keys.Contains(r.NameKey)) bad.Add(where + ": missing key " + r.NameKey);
                            if (r.HintKeys == null || r.HintKeys.Length == 0) bad.Add(where + ": no hint keys");
                            else foreach (string k in r.HintKeys) if (!Keys.Contains(k)) bad.Add(where + ": missing hint key " + k);
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(r.NameEs) || string.IsNullOrEmpty(r.NameEn)) bad.Add(where + ": name es/en");
                            if (r.HintsEs == null || r.HintsEn == null || r.HintsEs.Length == 0 || r.HintsEs.Length != r.HintsEn.Length) bad.Add(where + ": hints es/en");
                        }
                    }
                }
            }

            Assert.IsEmpty(bad);
            Assert.Greater(keyed, 0);
            Assert.Greater(rows, 40);
        }

        [Test]
        public void TelemetryStep_EveryRowUsesDictionaryKeys_FourRowsPerVariant()
        {
            var s = TutorialOverlay.BuildSteps(true)[TutorialOverlay.TelemetryStepIndex];
            foreach (var (variant, rs) in Variants(s))
            {
                Assert.AreEqual(4, rs.Length, variant + ": one row per animation phase");
                foreach (var r in rs) Assert.IsNotNull(r.NameKey, variant + "/" + r.Icon + " must be a dictionary key");
            }

            // captions of the four phases, pc and touch
            for (int ph = 0; ph < 4; ph++)
            {
                Assert.IsTrue(Keys.Contains("tut.tel.cap" + ph), "caption " + ph);
                Assert.IsTrue(Keys.Contains("tut.tel.cap" + ph + ".t"), "touch caption " + ph);
            }

            // the PC rows carry the keys of the step (4, L, Shift+A) and the piloting ones
            var pcHints = new List<string>();
            foreach (var r in s.Pc) pcHints.AddRange(r.HintKeys);
            foreach (string k in new[] { "tut.tel.mode.key", "tut.tel.key.l", "tut.tel.key.shifta", "tut.tel.pilot.keys", "tut.tel.pilot.cmds", "tut.tel.pilot.esc" })
                CollectionAssert.Contains(pcHints, k);
        }

        [Test]
        public void TelemetryStep_HonestyWords_RepeatLabelsJson()
        {
            var l = Labels();
            foreach (string lang in new[] { "es", "en" })
            {
                int i = lang == "es" ? 0 : 1;
                StringAssert.StartsWith(l.ClassificationTitle(SourceClassification.Replay, lang), TelemetryStrings.T("tut.tel.src.replay", lang));
                StringAssert.StartsWith(l.ClassificationTitle(SourceClassification.Sitl, lang), TelemetryStrings.T("tut.tel.src.sitl", lang));
                Assert.IsTrue(l.TrySimText("title", lang, out string sim));
                StringAssert.StartsWith(sim, TelemetryStrings.T("tut.tel.src.sim", lang));
                string origin = TelemetryStrings.T("tut.tel.flight.origin", lang);
                foreach (ThermalOrigin o in new[] { ThermalOrigin.Measured, ThermalOrigin.Estimated, ThermalOrigin.Simulated })
                    StringAssert.Contains(l.ThermalOriginText(o, lang), origin);
                StringAssert.Contains(l.FaultSynthetic(lang), TelemetryStrings.T("tut.tel.flight.faults", lang));
                // the replay is never "live" and the simulator is never called PX4 / a shadow
                foreach (string k in new[] { "tut.tel.src.replay", "tut.tel.src.sim", "tut.tel.src.sitl", "tut.tel.desc", "tut.tel.flight.origin" })
                    StringAssert.DoesNotContain(lang == "es" ? "en vivo" : "live", TelemetryStrings.T(k, lang).ToLowerInvariant(), k);
                Assert.AreNotEqual(string.Empty, TelemetryStrings.T("tut.tel.title", lang));
                Assert.IsTrue(i == 0 || i == 1);
            }
        }
    }
}
