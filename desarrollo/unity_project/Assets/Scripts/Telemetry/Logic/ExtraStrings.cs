// Strings of the Lote 1c tools (mission, recorder, training, course records), one partial per tool so that each tool adds its own table in its own file
// (no edits to the big TelemetryStrings.Table). Same layout as there: key -> { es, en }. Looked up by TelemetryStrings.T after its own table.
using System.Collections.Generic;

namespace WebGL.Telemetry
{
    public static partial class ExtraStrings
    {
        static readonly Dictionary<string, string[]> Table = Build();

        static Dictionary<string, string[]> Build()
        {
            var d = new Dictionary<string, string[]>();
            AddMission(d);
            AddRecord(d);
            AddTraining(d);
            AddCourse(d);
            AddDesktop(d);
            return d;
        }

        // implemented (or not) by each tool's own file: Telemetry/Logic/ExtraStrings.<Tool>.cs
        static partial void AddMission(Dictionary<string, string[]> d);
        static partial void AddRecord(Dictionary<string, string[]> d);
        static partial void AddTraining(Dictionary<string, string[]> d);
        static partial void AddCourse(Dictionary<string, string[]> d);
        // texts that only the desktop needs (files on disk instead of a download, reasons in both languages...): ExtraStrings.Desktop.cs
        static partial void AddDesktop(Dictionary<string, string[]> d);

        public static bool TryGet(string key, out string[] v) => Table.TryGetValue(key, out v);

        /// <summary>The extra entries first, then `rest` (for TelemetryStrings.All).</summary>
        public static IEnumerable<KeyValuePair<string, string[]>> Concat(IEnumerable<KeyValuePair<string, string[]>> rest)
        {
            foreach (var kv in Table) yield return kv;
            foreach (var kv in rest) yield return kv;
        }
    }
}
