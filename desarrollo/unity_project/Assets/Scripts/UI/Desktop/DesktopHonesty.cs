#if UNITY_STANDALONE
using System.Collections.Generic;
using WebGL.Telemetry;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PC only (PLAN_PC 39). The honesty strings that must stay in the interface tree (as visible text or as the text of an ⓘ) whatever is folded or moved:
    /// the origin of each source (digital shadow / flight simulator "not PX4 and not a real drone" / SITL digital twin) and the "synthetic fault" tag.
    /// Taken from the approved <see cref="HonestyLabels"/> (labels.json), never typed here, except the two fixed fragments of the simulator's detail.
    /// </summary>
    internal static class DesktopHonesty
    {
        public static List<string> Required(string lang)
        {
            var labels = HonestyLabels.Load();
            var list = new List<string>();
            if (labels.TryLocalReplayText(lang, out string local)) list.Add(local);
            if (labels.TrySimText("title", lang, out string sim)) list.Add(sim);
            list.Add(lang == "es" ? "No es PX4" : "Not PX4");
            list.Add(labels.ClassificationTitle(SourceClassification.Sitl, lang));
            list.Add(labels.FaultSynthetic(lang));
            return list;
        }
    }
}
#endif
