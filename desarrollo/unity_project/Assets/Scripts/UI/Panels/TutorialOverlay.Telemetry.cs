using System.Collections.Generic;
using WebGL.Telemetry;

namespace WebGL.UI.Panels
{
    /// <summary>
    /// The 7th step of the tutorial, "Telemetría" (E-7, PROPUESTA_TEXTOS_HONESTIDAD §3 adapted to the desktop UI): sits between "Estudio" and
    /// "Listo" (design/onboarding.json finalOrder). It is desktop only (the WebGL tutorial keeps its 6 steps). All its texts are keys of
    /// <see cref="TelemetryStrings"/> (ES/EN); the honesty labels inside them repeat labels.json (an EditMode test checks it). The Mouse variant
    /// of the web wording is not used by the .exe (the PC variant is the pointer one), so Mouse = Pc.
    /// </summary>
    internal sealed partial class TutorialOverlay
    {
        /// <summary>Index of the Telemetry step in the full list (0-based).</summary>
        internal const int TelemetryStepIndex = 5;

        private static Row K(string icon, string nameKey, params string[] hintKeys)
        {
            return new Row { Icon = icon, NameKey = nameKey, HintKeys = hintKeys };
        }

        /// <summary>The steps shown by this build: all of them in the .exe, without the PC-only step in WebGL / mobile.</summary>
        internal static Step[] BuildSteps(bool includePcOnly)
        {
            var list = new List<Step>();
            for (int i = 0; i < Steps.Length; i++)
            {
                if (i == TelemetryStepIndex) list.Add(TelemetryStep);
                list.Add(Steps[i]);
            }

            if (!includePcOnly) list.RemoveAll(s => s.PcOnly);
            return list.ToArray();
        }

        private static Step[] _active;
        private static Step[] Active => _active ?? (_active = BuildSteps(StandaloneBuild));

        private static string Tr(string key) => TelemetryStrings.T(key, Es ? "es" : "en");

        private static IEnumerable<string> HintTexts(Row r)
        {
            if (r.HintKeys == null) return Es ? r.HintsEs : r.HintsEn;
            var l = new List<string>(r.HintKeys.Length);
            foreach (string k in r.HintKeys) l.Add(Tr(k));
            return l;
        }

        private static Step TelemetryStep
        {
            get
            {
                Row[] pc =
                {
                    K("flight", "tut.tel.mode", "tut.tel.mode.key", "tut.tel.mode.strip"),
                    K("source", "tut.tel.src", "tut.tel.key.l", "tut.tel.src.replay", "tut.tel.src.sim", "tut.tel.src.sitl"),
                    K("alert", "tut.tel.flight", "tut.tel.flight.origin", "tut.tel.key.shifta", "tut.tel.flight.faults"),
                    K("gamepad", "tut.tel.pilot", "tut.tel.pilot.when", "tut.tel.pilot.keys", "tut.tel.pilot.cmds", "tut.tel.pilot.esc"),
                };
                Row[] touch =
                {
                    K("flight", "tut.tel.mode", "tut.tel.t.mode", "tut.tel.t.power"),
                    K("source", "tut.tel.src", "tut.tel.t.src", "tut.tel.src.replay", "tut.tel.src.sim", "tut.tel.src.sitl"),
                    K("alert", "tut.tel.flight", "tut.tel.t.flight", "tut.tel.flight.origin", "tut.tel.t.alerts", "tut.tel.flight.faults"),
                    K("gamepad", "tut.tel.pilot", "tut.tel.t.pilot", "tut.tel.t.cmds", "tut.tel.pilot.when"),
                };
                return new Step
                {
                    Icon = "flight", TitleKey = "tut.tel.title", DescKey = "tut.tel.desc", PcOnly = true,
                    Pc = pc, Mouse = pc, Touch = touch,
                };
            }
        }
    }
}
