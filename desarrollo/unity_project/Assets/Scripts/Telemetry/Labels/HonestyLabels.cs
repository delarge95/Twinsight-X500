// Honesty labels (contracts/labels.json, copied verbatim to Resources/TelemetryLabels.json). Never write these texts by hand in C#:
//  - the 24/7 replay is a "Sombra digital" (digital shadow), never "en vivo"/live and never a digital twin;
//  - only the closed-loop PX4 SITL is a "Gemelo digital SITL";
//  - the Inspector has no telemetry;
//  - every reading carries its origin (provenance: medida / estimada / comando / no disponible; thermal: medido / estimado / simulado);
//  - synthetic faults are labelled "sintética".
// The badge comes from the source the user chose, not from hello.mode (README discrepancy 11: the Inspector hello says "replay").
// Pure C# except Load() (Resources).

using System;
using System.Collections.Generic;
using WebGL.Telemetry.Json;

namespace WebGL.Telemetry
{
    public sealed class HonestyLabels
    {
        public const string ResourceName = "TelemetryLabels";

        public string ProtocolVersion { get; private set; }
        readonly Dictionary<string, object> root;

        HonestyLabels(Dictionary<string, object> root)
        {
            this.root = root;
            ProtocolVersion = root.TryGetValue("protocolVersion", out var v) ? v as string : null;
        }

        public static HonestyLabels Parse(string json)
        {
            var root = TelemetryJson.Parse(json) as Dictionary<string, object> ?? throw new FormatException("labels.json: not an object");
            var l = new HonestyLabels(root);
            l.Check();
            return l;
        }

        static HonestyLabels cached;

        /// <summary>Loads Resources/TelemetryLabels.json (main thread). Cached.</summary>
        public static HonestyLabels Load()
        {
            if (cached != null) return cached;
            var ta = UnityEngine.Resources.Load<UnityEngine.TextAsset>(ResourceName);
            if (ta == null) throw new InvalidOperationException("Resources/" + ResourceName + ".json missing");
            cached = Parse(ta.text);
            return cached;
        }

        // ------------------------------------------------------------------------------------------------- texts

        public string ClassificationTitle(SourceClassification c, string lang) => Text(lang, "classification", WireNames.Of(c), "title");
        public string ClassificationDetail(SourceClassification c, string lang) => Text(lang, "classification", WireNames.Of(c), "detail");
        public string ClassificationSource(SourceClassification c, string lang) => Text(lang, "classification", WireNames.Of(c), "source");

        /// <summary>Provenance value text ("measured" -> "medida"). wire: measured | estimated | command | unavailable.</summary>
        public string ProvenanceText(string wire, string lang) => Text(lang, "provenance", wire);

        /// <summary>Channel name text: motorTemp | batteryTemp | fcTemp.</summary>
        public string ProvenanceChannelText(string channel, string lang) => Text(lang, "provenanceChannels", channel);

        public string ThermalOriginText(ThermalOrigin o, string lang) => Text(lang, "thermalOrigin", WireNames.Of(o));

        public string FaultSynthetic(string lang) => Text(lang, "faultSynthetic");

        /// <summary>About lines 1..3 (line 3: "not a twin of a physical drone").</summary>
        public string About(int line, string lang) => Text(lang, "about", "line" + line);

        /// <summary>PX4 nav_state label; unknown states fall back to the number (labels.json navState has no translations).</summary>
        public string NavState(int nav)
        {
            var d = Node("navState") as Dictionary<string, object>;
            return d != null && d.TryGetValue(nav.ToString(System.Globalization.CultureInfo.InvariantCulture), out var v) && v is string s ? s : "nav " + nav;
        }

        /// <summary>
        /// Text for the LOCAL replay (P-3, user-approved): "Sombra digital · reproducción local" / "Digital shadow · local replay".
        /// Comes from `classification.replay.local` of labels.json (contract commit 8e47e15); callers must not hand-write it and the
        /// local replay is never labelled "en vivo".
        /// </summary>
        public bool TryLocalReplayText(string lang, out string text)
        {
            text = TryText(lang, "classification", "replay", "local");
            return text != null;
        }

        // ------------------------------------------------------------------------------------------------- lookup

        object Node(params string[] path)
        {
            object cur = root;
            foreach (var k in path)
            {
                if (!(cur is Dictionary<string, object> d) || !d.TryGetValue(k, out cur)) return null;
            }
            return cur;
        }

        string TryText(string lang, params string[] path)
        {
            if (!(Node(path) is Dictionary<string, object> d)) return null;
            string l = lang == "en" ? "en" : "es";
            if (d.TryGetValue(l, out var v) && v is string s) return s;
            return d.TryGetValue("es", out var e) ? e as string : null;
        }

        string Text(string lang, params string[] path) =>
            TryText(lang, path) ?? throw new KeyNotFoundException("labels.json: missing " + string.Join(".", path));

        /// <summary>Required keys and the honesty invariants of the texts (fails loudly on a broken copy).</summary>
        void Check()
        {
            foreach (var lang in new[] { "es", "en" })
            {
                foreach (SourceClassification c in Enum.GetValues(typeof(SourceClassification)))
                {
                    ClassificationTitle(c, lang); ClassificationDetail(c, lang); ClassificationSource(c, lang);
                }
                foreach (var p in new[] { "measured", "estimated", "command", "unavailable" }) ProvenanceText(p, lang);
                foreach (var ch in new[] { "motorTemp", "batteryTemp", "fcTemp" }) ProvenanceChannelText(ch, lang);
                foreach (ThermalOrigin o in Enum.GetValues(typeof(ThermalOrigin))) ThermalOriginText(o, lang);
                if (!TryLocalReplayText(lang, out _)) throw new KeyNotFoundException("labels.json: missing classification.replay.local");
                FaultSynthetic(lang);
                for (int i = 1; i <= 3; i++) About(i, lang);
            }
        }
    }
}
