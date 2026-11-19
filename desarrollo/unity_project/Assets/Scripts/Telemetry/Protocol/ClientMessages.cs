// Client -> server messages (ClientMessage in contracts/telemetry.schema.json). Build them with the factory methods,
// serialize with ToJson(). Validate() applies the schema rules (ranges, required fields) before sending.
// Pure C#.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WebGL.Telemetry.Json;

namespace WebGL.Telemetry
{
    public enum ClientMessageType { Inject, Clear, Command, Manual }

    public abstract class ClientMessage
    {
        public abstract ClientMessageType Type { get; }
        public abstract string ToJson();

        /// <summary>Schema rules; null when valid, else the reason.</summary>
        public abstract string Validate();

        /// <summary>Longest JSON the servers accept for this message (512; contract 1.2: 4096 for mission_set).</summary>
        public virtual int MaxChars => TelemetryProtocol.MaxClientMessageChars;

        // Factories ------------------------------------------------------------------------------------------------
        public static InjectMessage Inject(FaultKind kind, int? motor = null) => new InjectMessage { kind = kind, motor = motor };
        public static ClearMessage Clear() => new ClearMessage();
        public static CommandMessage Takeoff(double altitude) => new CommandMessage { name = "takeoff", altitude = altitude };
        public static CommandMessage SetAltitude(double altitude) => new CommandMessage { name = "set_altitude", altitude = altitude };
        public static CommandMessage Land() => new CommandMessage { name = "land" };
        public static CommandMessage FlyTo(double north, double east, double altitude) => new CommandMessage { name = "fly_to", north = north, east = east, altitude = altitude };
        public static CommandMessage Yaw(double degrees) => new CommandMessage { name = "yaw", degrees = degrees };
        public static CommandMessage Orbit(double radius) => new CommandMessage { name = "orbit", radius = radius };
        public static CommandMessage Rtl() => new CommandMessage { name = "rtl" };
        public static CommandMessage Hold() => new CommandMessage { name = "hold" };
        public static CommandMessage BatteryOff() => new CommandMessage { name = "battery_off" };
        public static CommandMessage MotorOff(int motor) => new CommandMessage { name = "motor_off", motor = motor };
        public static CommandMessage Restore(int? motor = null) => new CommandMessage { name = "restore", motor = motor };
        public static CommandMessage Pilot() => new CommandMessage { name = "pilot" };
        // Contract 1.2 (data support only: no screen offers these yet). Offer them only when `hello.commands` lists them (TelemetryClient.Send checks it).
        public static CommandMessage ParamList() => new CommandMessage { name = "param_list" };
        public static CommandMessage ParamGet(string id) => new CommandMessage { name = "param_get", id = id };
        public static CommandMessage ParamSet(string id, double value) => new CommandMessage { name = "param_set", id = id, value = value };
        public static CommandMessage MissionSet(MissionDoc mission) => new CommandMessage { name = "mission_set", mission = mission };
        public static CommandMessage MissionStart() => new CommandMessage { name = "mission_start" };
        public static CommandMessage MissionClear() => new CommandMessage { name = "mission_clear" };
        public static ManualMessage Manual(double x, double y, double z, double r) => new ManualMessage { x = x, y = y, z = z, r = r };

        protected static string N(double v) => TelemetryJson.Num(v);
    }

    public sealed class InjectMessage : ClientMessage
    {
        public override ClientMessageType Type => ClientMessageType.Inject;
        public FaultKind kind;
        public int? motor;

        public override string ToJson()
        {
            var sb = new StringBuilder("{\"type\":\"inject\",\"kind\":").Append(TelemetryJson.Str(WireNames.Of(kind)));
            if (motor.HasValue) sb.Append(",\"motor\":").Append(motor.Value.ToString(CultureInfo.InvariantCulture));
            return sb.Append('}').ToString();
        }

        public override string Validate()
        {
            bool needsMotor = kind == FaultKind.MotorFailure || kind == FaultKind.MotorOverheat;
            if (needsMotor && !motor.HasValue) return "motor required for " + WireNames.Of(kind);
            if (motor.HasValue && (motor < 1 || motor > 4)) return "motor out of 1..4";
            return null;
        }
    }

    public sealed class ClearMessage : ClientMessage
    {
        public override ClientMessageType Type => ClientMessageType.Clear;
        public override string ToJson() => "{\"type\":\"clear\"}";
        public override string Validate() => null;
    }

    public sealed class CommandMessage : ClientMessage
    {
        public override ClientMessageType Type => ClientMessageType.Command;
        public string name;
        public double? altitude, north, east, degrees, radius;
        public int? motor;
        /// <summary>Contract 1.2: PX4 parameter name (param_get, param_set), its new value (param_set) and the mission document (mission_set).</summary>
        public string id;
        public double? value;
        public MissionDoc mission;
        /// <summary>Contract 1.1, optional: sender sequence number (non-negative integer, increasing per connection; one counter shared with manual) and sender clock (epoch ms).</summary>
        public long? seq; public double? ts;

        public override int MaxChars => name == "mission_set" ? TelemetryProtocol.MaxMissionSetChars : TelemetryProtocol.MaxClientMessageChars;

        static readonly System.Text.RegularExpressions.Regex ParamId = new System.Text.RegularExpressions.Regex("^[A-Z][A-Z0-9_]{0,15}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        public override string ToJson()
        {
            var sb = new StringBuilder("{\"type\":\"command\",\"name\":").Append(TelemetryJson.Str(name));
            if (id != null) sb.Append(",\"id\":").Append(TelemetryJson.Str(id));
            if (value.HasValue) sb.Append(",\"value\":").Append(N(value.Value));
            if (mission != null) sb.Append(",\"mission\":").Append(Mission.ToJson(mission));
            if (north.HasValue) sb.Append(",\"north\":").Append(N(north.Value));
            if (east.HasValue) sb.Append(",\"east\":").Append(N(east.Value));
            if (altitude.HasValue) sb.Append(",\"altitude\":").Append(N(altitude.Value));
            if (degrees.HasValue) sb.Append(",\"degrees\":").Append(N(degrees.Value));
            if (radius.HasValue) sb.Append(",\"radius\":").Append(N(radius.Value));
            if (motor.HasValue) sb.Append(",\"motor\":").Append(motor.Value.ToString(CultureInfo.InvariantCulture));
            if (seq.HasValue) sb.Append(",\"seq\":").Append(seq.Value.ToString(CultureInfo.InvariantCulture));
            if (ts.HasValue) sb.Append(",\"ts\":").Append(N(ts.Value));
            return sb.Append('}').ToString();
        }

        /// <summary>Field names each command carries (schema CommandXxx definitions); "?" marks optional.</summary>
        public static string[] FieldsOf(string commandName)
        {
            switch (commandName)
            {
                case "takeoff":
                case "set_altitude": return new[] { "altitude" };
                case "fly_to": return new[] { "north", "east", "altitude" };
                case "yaw": return new[] { "degrees" };
                case "orbit": return new[] { "radius" };
                case "motor_off": return new[] { "motor" };
                case "restore": return new[] { "motor?" };
                case "param_get": return new[] { "id" };
                case "param_set": return new[] { "id", "value" };
                case "mission_set": return new[] { "mission" };
                case "land": case "rtl": case "hold": case "pilot": case "battery_off":
                case "param_list": case "mission_start": case "mission_clear": return Array.Empty<string>();
                default: return null;
            }
        }

        public override string Validate()
        {
            var fields = FieldsOf(name);
            if (fields == null) return "unknown command '" + name + "'";
            var present = new Dictionary<string, bool>
            {
                { "altitude", altitude.HasValue }, { "north", north.HasValue }, { "east", east.HasValue },
                { "degrees", degrees.HasValue }, { "radius", radius.HasValue }, { "motor", motor.HasValue },
                { "id", id != null }, { "value", value.HasValue }, { "mission", mission != null },
            };
            var allowed = new HashSet<string>();
            foreach (var f in fields)
            {
                bool optional = f.EndsWith("?", StringComparison.Ordinal);
                string key = optional ? f.Substring(0, f.Length - 1) : f;
                allowed.Add(key);
                if (!optional && !present[key]) return name + ": missing " + key;
            }
            foreach (var kv in present)
                if (kv.Value && !allowed.Contains(kv.Key)) return name + ": unexpected " + kv.Key;
            if (seq.HasValue && seq.Value < 0) return name + ": seq must be a non-negative integer";
            if (ts.HasValue && ts.Value < 0) return name + ": ts must be non-negative";
            if (name == "motor_off" && (motor < 1 || motor > 4)) return "motor_off: motor out of 1..4";
            if (name == "restore" && motor.HasValue && (motor < 0 || motor > 4)) return "restore: motor out of 0..4";
            if (id != null && !ParamId.IsMatch(id)) return name + ": id is not a PX4 parameter name";
            if (value.HasValue && (double.IsNaN(value.Value) || double.IsInfinity(value.Value))) return name + ": value must be finite";
            if (mission != null)
            {
                if (mission.waypoints == null || mission.waypoints.Count < 1 || mission.waypoints.Count > Mission.MaxWaypoints) return "mission_set: a mission has 1.." + Mission.MaxWaypoints + " waypoints";
                if (Array.IndexOf(TelemetryProtocol.MissionFinishes, mission.finish) < 0) return "mission_set: finish must be hold, land or rtl";
                foreach (Waypoint w in mission.waypoints)
                {
                    if (!(w.speed > 0) || double.IsInfinity(w.speed) || double.IsNaN(w.n) || double.IsNaN(w.e) || double.IsNaN(w.alt) || double.IsInfinity(w.n) || double.IsInfinity(w.e) || double.IsInfinity(w.alt))
                        return "mission_set: a waypoint has invalid values";
                    if (w.holdS.HasValue && (w.holdS.Value < 0 || double.IsNaN(w.holdS.Value) || double.IsInfinity(w.holdS.Value))) return "mission_set: holdS must be >= 0";
                }
            }
            return null;
        }
    }

    public sealed class ManualMessage : ClientMessage
    {
        public override ClientMessageType Type => ClientMessageType.Manual;
        /// <summary>Pitch, forward +, -1000..1000.</summary>
        public double x;
        /// <summary>Roll, right +, -1000..1000.</summary>
        public double y;
        /// <summary>Throttle 0..1000 (500 = hold altitude in Position mode).</summary>
        public double z;
        /// <summary>Yaw, clockwise +, -1000..1000.</summary>
        public double r;
        /// <summary>Contract 1.1, optional (see CommandMessage).</summary>
        public long? seq; public double? ts;

        public override string ToJson() =>
            "{\"type\":\"manual\",\"x\":" + N(x) + ",\"y\":" + N(y) + ",\"z\":" + N(z) + ",\"r\":" + N(r)
            + (seq.HasValue ? ",\"seq\":" + seq.Value.ToString(CultureInfo.InvariantCulture) : string.Empty) + (ts.HasValue ? ",\"ts\":" + N(ts.Value) : string.Empty) + "}";

        public override string Validate()
        {
            if (seq.HasValue && seq.Value < 0) return "manual: seq must be a non-negative integer";
            if (ts.HasValue && ts.Value < 0) return "manual: ts must be non-negative";
            if (Math.Abs(x) > 1000 || Math.Abs(y) > 1000 || Math.Abs(r) > 1000) return "manual axis out of -1000..1000";
            if (z < 0 || z > 1000) return "manual throttle out of 0..1000";
            return null;
        }
    }
}
