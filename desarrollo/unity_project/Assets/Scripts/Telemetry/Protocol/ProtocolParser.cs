// Parser + validator of the telemetry protocol. Two steps as the contract README asks: read `type`, then the concrete DTO.
//
// Tolerant (default, what the live client uses): unknown fields are IGNORED (forward compatibility, README "Cómo funciona" 1);
// everything else the schema requires is checked (required fields, enums, tuple lengths, ranges, integers) and a message
// that breaks it is rejected with a reason. Strict (tests / diagnostics): additionally rejects unknown fields, i.e. the
// schema's `additionalProperties: false`, so every fixture in contracts/fixtures/invalid is rejected exactly as the schema does.
// Pure C#: runs on the receive thread.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using WebGL.Telemetry.Json;

namespace WebGL.Telemetry
{
    public sealed class ProtocolException : Exception
    {
        public ProtocolException(string message) : base(message) { }
    }

    public struct ParseOptions
    {
        /// <summary>Reject unknown fields (schema additionalProperties:false). Off for the live client.</summary>
        public bool Strict;
        public static ParseOptions Tolerant => new ParseOptions { Strict = false };
        public static ParseOptions StrictSchema => new ParseOptions { Strict = true };
    }

    public static class ProtocolParser
    {
        // ------------------------------------------------------------------------------------------------- entry points

        public static bool TryParseServer(string json, out ServerMessage msg, out string error) => TryParseServer(json, ParseOptions.Tolerant, out msg, out error);

        public static bool TryParseServer(string json, ParseOptions opt, out ServerMessage msg, out string error)
        {
            try { msg = ParseServer(TelemetryJson.Parse(json), opt); error = null; return true; }
            catch (Exception e) when (e is ProtocolException || e is JsonParseException) { msg = null; error = e.Message; return false; }
        }

        public static bool TryParseClient(string json, ParseOptions opt, out ClientMessage msg, out string error)
        {
            try { msg = ParseClient(TelemetryJson.Parse(json), opt); error = null; return true; }
            catch (Exception e) when (e is ProtocolException || e is JsonParseException) { msg = null; error = e.Message; return false; }
        }

        public static ServerMessage ParseServer(object root, ParseOptions opt)
        {
            var o = new Obj(root, "$", opt);
            string type = o.Str("type");
            switch (type)
            {
                case "hello": return Hello(o);
                case "frames": return Frames(o);
                case "faults": return Faults(o);
                case "event": return Event(o);
                case "ack": return Ack(o);
                default: throw new ProtocolException("unknown server message type '" + type + "'");
            }
        }

        public static ClientMessage ParseClient(object root, ParseOptions opt)
        {
            var o = new Obj(root, "$", opt);
            string type = o.Str("type");
            switch (type)
            {
                case "inject":
                {
                    o.Allow("type", "kind", "motor");
                    var m = new InjectMessage { kind = FaultKindOf(o.Str("kind"), o.Path + ".kind") };
                    if (o.Has("motor")) m.motor = o.Int("motor", 1, 4);
                    string err = m.Validate();
                    if (err != null) throw new ProtocolException(err);
                    return m;
                }
                case "clear":
                    o.Allow("type");
                    return new ClearMessage();
                case "command":
                {
                    string name = o.Str("name");
                    var fields = CommandMessage.FieldsOf(name);
                    if (fields == null) throw new ProtocolException("unknown command '" + name + "'");
                    var allowed = new List<string> { "type", "name" };
                    foreach (var f in fields) allowed.Add(f.TrimEnd('?'));
                    o.Allow(allowed.ToArray());
                    var m = new CommandMessage { name = name };
                    if (o.Has("altitude")) m.altitude = o.Num("altitude");
                    if (o.Has("north")) m.north = o.Num("north");
                    if (o.Has("east")) m.east = o.Num("east");
                    if (o.Has("degrees")) m.degrees = o.Num("degrees");
                    if (o.Has("radius")) m.radius = o.Num("radius");
                    if (o.Has("motor")) m.motor = o.Int("motor", name == "restore" ? 0 : 1, 4);
                    string err = m.Validate();
                    if (err != null) throw new ProtocolException(err);
                    return m;
                }
                case "manual":
                {
                    o.Allow("type", "x", "y", "z", "r");
                    var m = new ManualMessage { x = o.Num("x", -1000, 1000), y = o.Num("y", -1000, 1000), z = o.Num("z", 0, 1000), r = o.Num("r", -1000, 1000) };
                    return m;
                }
                default: throw new ProtocolException("unknown client message type '" + type + "'");
            }
        }

        // ------------------------------------------------------------------------------------------------- messages

        static HelloMessage Hello(Obj o)
        {
            o.Allow("type", "mode", "rotors", "rateHz", "provenance", "batteryCells", "ambientC", "source", "commands", "serverTime");
            var h = new HelloMessage();
            if (!WireNames.TryMode(o.Str("mode"), out h.mode)) throw new ProtocolException(o.Path + ".mode: unknown mode '" + o.Str("mode") + "'");
            var rotors = o.Arr("rotors");
            h.rotors = new RotorGeometry[rotors.Count];
            for (int i = 0; i < rotors.Count; i++) h.rotors[i] = Rotor(o.Child(rotors[i], ".rotors[" + i + "]"));
            h.rateHz = o.Num("rateHz", 0, double.MaxValue);
            h.provenance = ProvenanceOf(o.Sub("provenance"));
            h.batteryCells = o.Int("batteryCells", 1, int.MaxValue);
            h.ambientC = o.Num("ambientC");
            var s = o.Sub("source");
            s.Allow("label", "url", "vehicle", "px4", "durationS");
            h.source = new SourceInfo { label = s.Str("label") };
            if (s.Has("url")) { h.source.url = s.Str("url"); h.source.HasUrl = true; }
            if (s.Has("vehicle")) { h.source.vehicle = s.Str("vehicle"); h.source.HasVehicle = true; }
            if (s.Has("px4")) { h.source.px4 = s.Str("px4"); h.source.HasPx4 = true; }
            if (s.Has("durationS")) { h.source.durationS = s.Num("durationS"); h.source.HasDurationS = true; }
            var cmds = o.Arr("commands");
            h.commands = new string[cmds.Count];
            for (int i = 0; i < cmds.Count; i++)
            {
                if (!(cmds[i] is string c)) throw new ProtocolException(o.Path + ".commands[" + i + "]: not a string");
                if (Array.IndexOf(TelemetryProtocol.CommandCapabilities, c) < 0) throw new ProtocolException(o.Path + ".commands[" + i + "]: unknown capability '" + c + "'");
                h.commands[i] = c;
            }
            h.serverTime = o.Num("serverTime");
            // Not in protocol 1.0.0; read when a future server adds it (README discrepancy 14). Strict mode rejects it above.
            if (o.Has("protocolVersion") && o.Raw("protocolVersion") is string pv) { h.protocolVersion = pv; h.HasProtocolVersion = true; }
            return h;
        }

        static FramesMessage Frames(Obj o)
        {
            o.Allow("type", "frames");
            var arr = o.Arr("frames");
            var m = new FramesMessage { frames = new TelemetryFrame[arr.Count] };
            for (int i = 0; i < arr.Count; i++) m.frames[i] = Frame(o.Child(arr[i], ".frames[" + i + "]"));
            return m;
        }

        public static TelemetryFrame Frame(object raw, ParseOptions opt) => Frame(new Obj(raw, "$", opt));

        static TelemetryFrame Frame(Obj o)
        {
            o.Allow("ts", "lt", "seg", "q", "p", "v", "u", "rpm", "bat", "temp", "vib", "armed", "landed", "nav");
            var f = new TelemetryFrame
            {
                ts = o.Num("ts"),
                lt = o.Num("lt"),
                seg = o.Int("seg", 0, int.MaxValue),
                q = o.Tuple("q", 4),
                p = o.Tuple("p", 3),
                v = o.Tuple("v", 3),
                u = o.Tuple("u", 4, 0, 1),
                rpm = o.Tuple("rpm", 4),
                temp = o.Tuple("temp", 6),
                vib = o.Num("vib"),
                armed = o.Bool("armed"),
                landed = o.Bool("landed"),
                nav = o.Int("nav", int.MinValue, int.MaxValue),
            };
            var b = o.Sub("bat");
            b.Allow("v", "i", "rem");
            f.bat = new BatteryState { v = b.Num("v"), i = b.Num("i"), rem = b.Num("rem", 0, 1) };
            return f;
        }

        static FaultsMessage Faults(Obj o)
        {
            o.Allow("type", "faults");
            var arr = o.Arr("faults");
            var m = new FaultsMessage { faults = new Fault[arr.Count] };
            for (int i = 0; i < arr.Count; i++) m.faults[i] = FaultOf(o.Child(arr[i], ".faults[" + i + "]"));
            return m;
        }

        static Fault FaultOf(Obj o)
        {
            o.Allow("id", "kind", "motor", "since", "until");
            var f = new Fault { id = o.Str("id"), kind = FaultKindOf(o.Str("kind"), o.Path + ".kind"), since = o.Num("since"), until = o.Num("until") };
            if (o.Has("motor")) { f.motor = o.Int("motor", 1, 4); f.HasMotor = true; }
            return f;
        }

        static EventMessage Event(Obj o)
        {
            o.Allow("type", "severity", "text", "ts");
            string sev = o.Str("severity");
            int si = Array.IndexOf(TelemetryProtocol.EventSeverities, sev);
            if (si < 0) throw new ProtocolException(o.Path + ".severity: unknown '" + sev + "'");
            return new EventMessage { severity = (EventSeverity)si, text = o.Str("text"), ts = o.Num("ts") };
        }

        static AckMessage Ack(Obj o)
        {
            o.Allow("type", "command", "ok", "detail");
            var a = new AckMessage { command = o.Str("command"), ok = o.Bool("ok") };
            if (o.Has("detail")) { a.detail = o.Str("detail"); a.HasDetail = true; }
            return a;
        }

        // --------------------------------------------------------------------------------------------- shared defs

        static RotorGeometry Rotor(Obj o)
        {
            o.Allow("motor", "px", "py", "km");
            return new RotorGeometry(o.Int("motor", 1, 4), o.Num("px"), o.Num("py"), o.Num("km"));
        }

        static Provenance ProvenanceOf(Obj o)
        {
            o.Allow("rpm", "motorTemp", "batteryTemp", "fcTemp", "batteryCurrent");
            var p = new Provenance
            {
                rpm = o.Enum("rpm", TelemetryProtocol.ProvenanceRpm),
                motorTemp = o.Enum("motorTemp", TelemetryProtocol.ProvenanceTemp),
                batteryTemp = o.Enum("batteryTemp", TelemetryProtocol.ProvenanceTemp),
                fcTemp = o.Enum("fcTemp", TelemetryProtocol.ProvenanceFcTemp),
            };
            if (o.Has("batteryCurrent")) { p.batteryCurrent = o.Enum("batteryCurrent", TelemetryProtocol.ProvenanceCurrent); p.HasBatteryCurrent = true; }
            return p;
        }

        static FaultKind FaultKindOf(string s, string path)
        {
            if (!WireNames.TryFaultKind(s, out var k)) throw new ProtocolException(path + ": unknown fault kind '" + s + "'");
            return k;
        }

        // ------------------------------------------------------------------------------------------ derived types

        static readonly Regex AlertId = new Regex("^(motor[1-4]:(out|temp)|battery:(rem|temp)|fc:(temp|vib))$", RegexOptions.CultureInvariant);

        public static Alert ParseAlert(object raw, ParseOptions opt)
        {
            var o = new Obj(raw, "$", opt);
            o.Allow("id", "component", "severity", "message");
            string id = o.Str("id");
            if (!AlertId.IsMatch(id)) throw new ProtocolException("$.id: bad alert id '" + id + "'");
            string sev = o.Enum("severity", TelemetryProtocol.AlertSeverities);
            return new Alert
            {
                id = id,
                component = o.Enum("component", TelemetryProtocol.AlertComponents),
                severity = sev == "critical" ? AlertSeverity.Critical : AlertSeverity.Warning,
                message = o.Str("message"),
            };
        }

        public static AlertSummary ParseAlertSummary(object raw, ParseOptions opt)
        {
            var o = new Obj(raw, "$", opt);
            o.Allow("count", "worst");
            var s = new AlertSummary { count = o.Int("count", 0, int.MaxValue) };
            object w = o.Required("worst");
            if (w == null) s.worst = null;
            else if (w is string ws && Array.IndexOf(TelemetryProtocol.AlertSeverities, ws) >= 0) s.worst = ws == "critical" ? AlertSeverity.Critical : AlertSeverity.Warning;
            else throw new ProtocolException("$.worst: must be warning, critical or null");
            return s;
        }

        public static HealthResponse ParseHealth(object raw, ParseOptions opt)
        {
            var o = new Obj(raw, "$", opt);
            o.Allow("ok", "frames", "rateHz");
            if (!o.Bool("ok")) throw new ProtocolException("$.ok: must be true");
            double rate = o.Num("rateHz");
            if (!(rate > 0)) throw new ProtocolException("$.rateHz: must be > 0");
            return new HealthResponse { ok = true, frames = o.Int("frames", 1, int.MaxValue), rateHz = rate };
        }

        /// <summary>TWF1 header. Open object (unknown keys allowed even in strict mode, as in the schema).</summary>
        public static TwfHeader ParseTwfHeader(object raw)
        {
            var o = new Obj(raw, "$", ParseOptions.Tolerant);
            var h = new TwfHeader { Raw = o.Dict };
            h.version = o.Int("version", 1, 1);
            var f = o.Arr("fields");
            if (f.Count < 1) throw new ProtocolException("$.fields: empty");
            h.fields = new string[f.Count];
            for (int i = 0; i < f.Count; i++) h.fields[i] = f[i] as string ?? throw new ProtocolException("$.fields[" + i + "]: not a string");
            h.rate_hz = o.Num("rate_hz");
            if (!(h.rate_hz > 0)) throw new ProtocolException("$.rate_hz: must be > 0");
            h.frame_count = o.Int("frame_count", 1, int.MaxValue);
            if (o.Has("duration_s")) { h.duration_s = o.Num("duration_s"); h.HasDuration = true; }
            if (o.Has("rotors"))
            {
                var r = o.Arr("rotors");
                h.rotors = new RotorGeometry[r.Count];
                for (int i = 0; i < r.Count; i++) h.rotors[i] = Rotor(o.Child(r[i], ".rotors[" + i + "]"));
            }
            if (o.Has("vehicle"))
            {
                var v = o.Sub("vehicle");
                if (v.Has("hardware")) h.vehicleHardware = v.Str("hardware");
                if (v.Has("px4_version")) h.vehiclePx4Version = v.Str("px4_version");
                if (v.Has("sys_autostart")) h.vehicleSysAutostart = v.Int("sys_autostart", int.MinValue, int.MaxValue);
                if (v.Has("bat_cells")) h.vehicleBatCells = v.Int("bat_cells", int.MinValue, int.MaxValue);
            }
            if (o.Has("source"))
            {
                var s = o.Sub("source");
                if (s.Has("log_id")) h.sourceLogId = s.Str("log_id");
                if (s.Has("url")) h.sourceUrl = s.Str("url");
                if (s.Has("dataset")) h.sourceDataset = s.Str("dataset");
            }
            return h;
        }

        // ----------------------------------------------------------------------------------------------- object view

        /// <summary>Typed accessors over a parsed JSON object, with JSON-path errors.</summary>
        sealed class Obj
        {
            public readonly Dictionary<string, object> Dict;
            public readonly string Path;
            readonly ParseOptions opt;

            public Obj(object raw, string path, ParseOptions opt)
            {
                Dict = raw as Dictionary<string, object> ?? throw new ProtocolException(path + ": not an object");
                Path = path;
                this.opt = opt;
            }

            public Obj Child(object raw, string suffix) => new Obj(raw, Path + suffix, opt);
            public Obj Sub(string key) => new Obj(Required(key), Path + "." + key, opt);

            public bool Has(string key) => Dict.ContainsKey(key);
            public object Raw(string key) => Dict.TryGetValue(key, out var v) ? v : null;

            /// <summary>Strict mode: every key must be in the list (schema additionalProperties:false). Tolerant: no-op.</summary>
            public void Allow(params string[] keys)
            {
                if (!opt.Strict) return;
                foreach (var k in Dict.Keys)
                    if (Array.IndexOf(keys, k) < 0) throw new ProtocolException(Path + ": unknown field '" + k + "'");
            }

            public object Required(string key)
            {
                if (!Dict.TryGetValue(key, out var v)) throw new ProtocolException(Path + ": missing '" + key + "'");
                return v;
            }

            public string Str(string key) => Required(key) as string ?? throw new ProtocolException(Path + "." + key + ": not a string");

            public bool Bool(string key) => Required(key) is bool b ? b : throw new ProtocolException(Path + "." + key + ": not a boolean");

            public double Num(string key) => Num(key, double.NegativeInfinity, double.PositiveInfinity);

            public double Num(string key, double min, double max)
            {
                if (!(Required(key) is double d)) throw new ProtocolException(Path + "." + key + ": not a number");
                if (d < min || d > max) throw new ProtocolException(Path + "." + key + ": " + d.ToString(CultureInfo.InvariantCulture) + " out of range");
                return d;
            }

            public int Int(string key, int min, int max)
            {
                double d = Num(key);
                if (d != Math.Floor(d)) throw new ProtocolException(Path + "." + key + ": not an integer");
                if (d < min || d > max) throw new ProtocolException(Path + "." + key + ": " + d.ToString(CultureInfo.InvariantCulture) + " out of range");
                return (int)d;
            }

            public string Enum(string key, string[] values)
            {
                string s = Str(key);
                if (Array.IndexOf(values, s) < 0) throw new ProtocolException(Path + "." + key + ": unknown value '" + s + "'");
                return s;
            }

            public List<object> Arr(string key) => Required(key) as List<object> ?? throw new ProtocolException(Path + "." + key + ": not an array");

            public double[] Tuple(string key, int n, double min = double.NegativeInfinity, double max = double.PositiveInfinity)
            {
                var a = Arr(key);
                if (a.Count != n) throw new ProtocolException(Path + "." + key + ": expected " + n + " items, got " + a.Count);
                var r = new double[n];
                for (int i = 0; i < n; i++)
                {
                    if (!(a[i] is double d)) throw new ProtocolException(Path + "." + key + "[" + i + "]: not a number");
                    if (d < min || d > max) throw new ProtocolException(Path + "." + key + "[" + i + "]: out of range");
                    r[i] = d;
                }
                return r;
            }
        }
    }
}
