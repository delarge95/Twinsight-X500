// Helpers for the contract tests: load the copied contract (Contracts~, see SOURCE.md), typed access to parsed JSON and
// per-golden case counting (each test logs "[contract] <golden>.<section>: pass/total").
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WebGL.Telemetry.Json;

namespace WebGL.Telemetry.Tests
{
    using JObj = Dictionary<string, object>;
    using JArr = List<object>;

    static class C
    {
        public static string Root => Path.Combine(Application.dataPath, "Scripts", "Tests", "Editor", "Telemetry", "Contracts~");

        public static string Text(string rel) => File.ReadAllText(Path.Combine(Root, rel.Replace('/', Path.DirectorySeparatorChar)));
        public static object Load(string rel) => TelemetryJson.Parse(Text(rel));
        public static JObj Golden(string name) => (JObj)Load("golden/" + name + ".json");

        public static JObj O(object o) => (JObj)o;
        public static JArr A(object o) => (JArr)o;
        public static JObj O(JObj o, string k) => (JObj)o[k];
        public static JArr A(JObj o, string k) => (JArr)o[k];
        public static double N(object o) => (double)o;
        public static double N(JObj o, string k) => (double)o[k];
        public static int I(JObj o, string k) => (int)(double)o[k];
        public static string S(JObj o, string k) => (string)o[k];
        public static bool B(JObj o, string k) => (bool)o[k];
        public static bool IsNull(JObj o, string k) => !o.ContainsKey(k) || o[k] == null;
        public static double[] V(object o) => ((JArr)o).Select(x => (double)x).ToArray();
        public static double[] V(JObj o, string k) => V(o[k]);

        public static TelemetryFrame Frame(object raw) => ProtocolParser.Frame(raw, ParseOptions.StrictSchema);

        /// <summary>Contract frames of interpolation.json (fixtures/frames.replay.loopwrap.json).</summary>
        public static TelemetryFrame[] LoopwrapFrames()
        {
            var m = (FramesMessage)ProtocolParser.ParseServer(Load("fixtures/frames.replay.loopwrap.json"), ParseOptions.StrictSchema);
            return m.frames;
        }

        public static Provenance Provenance(object raw)
        {
            if (raw == null) return null;
            var o = (JObj)raw;
            return new Provenance
            {
                rpm = o["rpm"] as string, motorTemp = o["motorTemp"] as string, batteryTemp = o["batteryTemp"] as string, fcTemp = o["fcTemp"] as string,
                batteryCurrent = o.TryGetValue("batteryCurrent", out var bc) ? bc as string : null, HasBatteryCurrent = o.ContainsKey("batteryCurrent"),
                // contract 1.2 (golden/thermal.json telemetry_origins now carries them)
                pose = o.TryGetValue("pose", out var po) ? po as string : null, HasPose = o.ContainsKey("pose"),
                velocity = o.TryGetValue("velocity", out var ve) ? ve as string : null, HasVelocity = o.ContainsKey("velocity"),
                gps = o.TryGetValue("gps", out var gp) ? gp as string : null, HasGps = o.ContainsKey("gps"),
                esc = o.TryGetValue("esc", out var es) ? es as string : null, HasEsc = o.ContainsKey("esc"),
            };
        }

        public static SourceClassification Cls(string s) => s == "sitl" ? SourceClassification.Sitl : s == "inspector" ? SourceClassification.Inspector : SourceClassification.Replay;
    }

    /// <summary>Collects mismatches of one golden section and asserts once with every failure listed.</summary>
    sealed class Cases
    {
        readonly string name;
        readonly double tol;
        readonly List<string> fails = new List<string>();
        int total;
        bool currentFailed;

        public static readonly SortedDictionary<string, string> Summary = new SortedDictionary<string, string>();

        public Cases(string name, double tolerance = 0) { this.name = name; tol = tolerance; }

        /// <summary>Starts a case; returns this for chaining.</summary>
        public Cases Case()
        {
            total++;
            currentFailed = false;
            return this;
        }

        void Fail(string what)
        {
            if (!currentFailed) currentFailed = true;
            fails.Add("case " + total + ": " + what);
        }

        public void True(bool ok, string what) { if (!ok) Fail(what); }
        public void Eq<T>(T expected, T actual, string what) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) Fail(what + ": expected " + expected + ", got " + actual); }

        public void Near(double expected, double actual, string what, double? tolerance = null)
        {
            double t = tolerance ?? tol;
            if (double.IsNaN(expected) && double.IsNaN(actual)) return;
            if (!(Math.Abs(expected - actual) <= t)) Fail(what + ": expected " + expected.ToString("R") + ", got " + actual.ToString("R") + " (tol " + t + ")");
        }

        public void Near(double[] expected, double[] actual, string what, double? tolerance = null)
        {
            if (expected.Length != actual.Length) { Fail(what + ": length " + expected.Length + " vs " + actual.Length); return; }
            for (int i = 0; i < expected.Length; i++) Near(expected[i], actual[i], what + "[" + i + "]", tolerance);
        }

        public void Frame(object expectedRaw, TelemetryFrame a, string what, double? tolerance = null)
        {
            var e = (Dictionary<string, object>)expectedRaw;
            Near(C.N(e, "ts"), a.ts, what + ".ts", tolerance);
            Near(C.N(e, "lt"), a.lt, what + ".lt", tolerance);
            Eq(C.I(e, "seg"), a.seg, what + ".seg");
            foreach (var k in new[] { "q", "p", "v", "u", "rpm", "temp" })
            {
                var arr = (double[])typeof(TelemetryFrame).GetField(k).GetValue(a);
                Near(C.V(e, k), arr, what + "." + k, tolerance);
            }
            var bat = C.O(e, "bat");
            Near(C.N(bat, "v"), a.bat.v, what + ".bat.v", tolerance);
            Near(C.N(bat, "i"), a.bat.i, what + ".bat.i", tolerance);
            Near(C.N(bat, "rem"), a.bat.rem, what + ".bat.rem", tolerance);
            Near(C.N(e, "vib"), a.vib, what + ".vib", tolerance);
            Eq(C.B(e, "armed"), a.armed, what + ".armed");
            Eq(C.B(e, "landed"), a.landed, what + ".landed");
            Eq(C.I(e, "nav"), a.nav, what + ".nav");
        }

        public void Done()
        {
            int failedCases = fails.Select(f => f.Substring(0, f.IndexOf(':'))).Distinct().Count();
            string line = (total - failedCases) + "/" + total;
            Summary[name] = line;
            Debug.Log("[contract] " + name + ": " + line + " cases pass");
            Assert.That(total, Is.GreaterThan(0), name + ": no cases");
            if (fails.Count > 0) Assert.Fail(name + ": " + line + "\n" + string.Join("\n", fails.Take(40)));
        }
    }
}
