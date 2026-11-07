// DTOs of the telemetry protocol (contracts/telemetry.schema.json, protocolVersion 1.0.0; web: src/shared/protocol.ts).
// Units: NED metres and m/s; quaternion Hamilton [w, x, y, z] rotating body FRD into world NED; u 0..1; rpm; degrees Celsius;
// ts in epoch ms (double: ~1.8e12 does not fit a float). Optional wire fields keep their default and a `Has*` flag.
// Pure C#: no Unity API.

using System;
using System.Collections.Generic;

namespace WebGL.Telemetry
{
    public static class TelemetryProtocol
    {
        /// <summary>Version of the contract this client implements (contracts/telemetry.schema.json "protocolVersion").</summary>
        public const string Version = "1.0.0";
        public const int VersionMajor = 1;

        /// <summary>Servers drop longer client messages without answering (contracts README).</summary>
        public const int MaxClientMessageChars = 512;

        public static readonly string[] Modes = { "replay", "sitl" };
        public static readonly string[] SourceClassifications = { "replay", "sitl", "inspector" };
        public static readonly string[] CommandCapabilities =
            { "inject", "clear", "takeoff", "set_altitude", "land", "fly_to", "yaw", "orbit", "rtl", "hold", "motor_off", "restore", "pilot", "manual" };
        public static readonly string[] CommandNames =
            { "takeoff", "set_altitude", "land", "fly_to", "yaw", "orbit", "rtl", "hold", "motor_off", "restore", "pilot" };
        public static readonly string[] FaultKinds = { "motor_failure", "motor_overheat", "battery_sag", "vibration" };
        public static readonly string[] EventSeverities = { "info", "warning", "critical" };
        public static readonly string[] ProvenanceRpm = { "measured", "estimated", "command" };
        public static readonly string[] ProvenanceTemp = { "measured", "estimated", "unavailable" };
        public static readonly string[] ProvenanceFcTemp = { "measured", "unavailable" };
        public static readonly string[] ProvenanceCurrent = { "measured", "estimated" };
        public static readonly string[] ThermalOrigins = { "measured", "estimated", "simulated" };
        public static readonly string[] AlertComponents = { "motor1", "motor2", "motor3", "motor4", "battery", "fc" };
        public static readonly string[] AlertSeverities = { "warning", "critical" };
    }

    public enum TelemetryMode { Replay, Sitl }

    /// <summary>What the classification badge shows: the two wire modes plus the local Inspector (no telemetry, never on the wire).</summary>
    public enum SourceClassification { Replay, Sitl, Inspector }

    public enum FaultKind { MotorFailure, MotorOverheat, BatterySag, Vibration }

    public enum EventSeverity { Info, Warning, Critical }

    public enum AlertSeverity { Critical = 0, Warning = 1 } // numeric value = sort rank (critical first)

    public enum ThermalOrigin { Measured, Estimated, Simulated }

    public static class WireNames
    {
        public static string Of(TelemetryMode m) => m == TelemetryMode.Sitl ? "sitl" : "replay";
        public static string Of(SourceClassification c) => c == SourceClassification.Sitl ? "sitl" : c == SourceClassification.Inspector ? "inspector" : "replay";
        public static string Of(FaultKind k) => TelemetryProtocol.FaultKinds[(int)k];
        public static string Of(EventSeverity s) => TelemetryProtocol.EventSeverities[(int)s];
        public static string Of(AlertSeverity s) => s == AlertSeverity.Critical ? "critical" : "warning";
        public static string Of(ThermalOrigin o) => TelemetryProtocol.ThermalOrigins[(int)o];

        public static bool TryFaultKind(string s, out FaultKind k)
        {
            int i = Array.IndexOf(TelemetryProtocol.FaultKinds, s);
            k = i < 0 ? FaultKind.MotorFailure : (FaultKind)i;
            return i >= 0;
        }

        public static bool TryMode(string s, out TelemetryMode m)
        {
            m = s == "sitl" ? TelemetryMode.Sitl : TelemetryMode.Replay;
            return s == "sitl" || s == "replay";
        }
    }

    // ------------------------------------------------------------------------------------------------------- frame

    public sealed class BatteryState
    {
        /// <summary>Volts.</summary>
        public double v;
        /// <summary>Amps (origin: Provenance.batteryCurrent; absent = measured).</summary>
        public double i;
        /// <summary>Remaining capacity, fraction 0..1.</summary>
        public double rem;

        public BatteryState Clone() => new BatteryState { v = v, i = i, rem = rem };
    }

    /// <summary>One telemetry sample (TelemetryFrame in the schema). Field names match the wire.</summary>
    public sealed class TelemetryFrame
    {
        /// <summary>Stream time, epoch ms (wall clock of the server). Monotonic within a connection.</summary>
        public double ts;
        /// <summary>Log time (replay) or vehicle boot time (SITL), seconds. Display only.</summary>
        public double lt;
        /// <summary>Segment id; changes on discontinuities (replay loop wrap). Never interpolate across it.</summary>
        public int seg;
        /// <summary>[w, x, y, z] Hamilton, body FRD -> world NED.</summary>
        public double[] q = { 1, 0, 0, 0 };
        /// <summary>Local position NED, m.</summary>
        public double[] p = new double[3];
        /// <summary>Local velocity NED, m/s.</summary>
        public double[] v = new double[3];
        /// <summary>Motor commands 0..1, PX4 motor order 1..4.</summary>
        public double[] u = new double[4];
        /// <summary>Rotor speed, RPM, PX4 motor order 1..4.</summary>
        public double[] rpm = new double[4];
        public BatteryState bat = new BatteryState();
        /// <summary>°C: motor 1..4, battery, flight controller.</summary>
        public double[] temp = new double[6];
        /// <summary>Accelerometer vibration metric, m/s².</summary>
        public double vib;
        public bool armed;
        public bool landed;
        /// <summary>PX4 vehicle_status.nav_state (labels: contracts/labels.json navState).</summary>
        public int nav;

        public TelemetryFrame Clone()
        {
            return new TelemetryFrame
            {
                ts = ts, lt = lt, seg = seg,
                q = (double[])q.Clone(), p = (double[])p.Clone(), v = (double[])v.Clone(),
                u = (double[])u.Clone(), rpm = (double[])rpm.Clone(), bat = bat.Clone(), temp = (double[])temp.Clone(),
                vib = vib, armed = armed, landed = landed, nav = nav,
            };
        }
    }

    public sealed class RotorGeometry
    {
        public int motor;
        /// <summary>Body FRD position, m (+ forward).</summary>
        public double px;
        /// <summary>Body FRD position, m (+ right).</summary>
        public double py;
        /// <summary>Moment coefficient: &gt; 0 CCW seen from above, &lt; 0 CW.</summary>
        public double km;

        public RotorGeometry() { }
        public RotorGeometry(int motor, double px, double py, double km) { this.motor = motor; this.px = px; this.py = py; this.km = km; }
    }

    /// <summary>Which channels are measured and which are model estimates. Strings are the wire enum values.</summary>
    public sealed class Provenance
    {
        public string rpm;          // measured | estimated | command
        public string motorTemp;    // measured | estimated | unavailable
        public string batteryTemp;  // measured | estimated | unavailable
        public string fcTemp;       // measured | unavailable
        public string batteryCurrent; // measured | estimated (optional)
        public bool HasBatteryCurrent;

        /// <summary>Absent batteryCurrent means measured (older servers).</summary>
        public string BatteryCurrentOrDefault => HasBatteryCurrent ? batteryCurrent : "measured";
    }

    public sealed class SourceInfo
    {
        public string label; // free text, Spanish only today (README discrepancy 10)
        public string url; public bool HasUrl;
        public string vehicle; public bool HasVehicle;
        public string px4; public bool HasPx4;
        public double durationS; public bool HasDurationS;
    }

    // ---------------------------------------------------------------------------------------------- server messages

    public enum ServerMessageType { Hello, Frames, Faults, Event, Ack }

    public abstract class ServerMessage
    {
        public abstract ServerMessageType Type { get; }
        /// <summary>Local monotonic time (TelemetryTime.NowMs) when the message arrived; set by the source, not on the wire.</summary>
        public double ReceivedAtMs;
    }

    public sealed class HelloMessage : ServerMessage
    {
        public override ServerMessageType Type => ServerMessageType.Hello;
        public TelemetryMode mode;
        public RotorGeometry[] rotors = Array.Empty<RotorGeometry>();
        public double rateHz;
        public Provenance provenance = new Provenance();
        public int batteryCells;
        public double ambientC;
        public SourceInfo source = new SourceInfo();
        public string[] commands = Array.Empty<string>();
        public double serverTime;
        /// <summary>Future-compatible: `protocolVersion` is not on the wire in 1.0.0 (README discrepancy 14). Tolerant mode reads it if a server adds it.</summary>
        public string protocolVersion; public bool HasProtocolVersion;

        public bool Accepts(string command) => Array.IndexOf(commands, command) >= 0;
    }

    public sealed class FramesMessage : ServerMessage
    {
        public override ServerMessageType Type => ServerMessageType.Frames;
        public TelemetryFrame[] frames = Array.Empty<TelemetryFrame>();
    }

    public sealed class Fault
    {
        /// <summary>'&lt;kind&gt;:&lt;motor&gt;' or '&lt;kind&gt;'.</summary>
        public string id;
        public FaultKind kind;
        /// <summary>1..4 for motor_failure / motor_overheat; 0 when absent.</summary>
        public int motor; public bool HasMotor;
        /// <summary>Stream ms. Active while since &lt;= now &lt; until.</summary>
        public double since;
        public double until;

        public Fault Clone() => new Fault { id = id, kind = kind, motor = motor, HasMotor = HasMotor, since = since, until = until };
    }

    public sealed class FaultsMessage : ServerMessage
    {
        public override ServerMessageType Type => ServerMessageType.Faults;
        public Fault[] faults = Array.Empty<Fault>();
    }

    public sealed class EventMessage : ServerMessage
    {
        public override ServerMessageType Type => ServerMessageType.Event;
        public EventSeverity severity;
        public string text;
        /// <summary>Epoch ms.</summary>
        public double ts;
    }

    public sealed class AckMessage : ServerMessage
    {
        public override ServerMessageType Type => ServerMessageType.Ack;
        /// <summary>Bridge: command name or "?"; worker: the message type (README discrepancy 1).</summary>
        public string command;
        public bool ok;
        public string detail; public bool HasDetail;
    }

    // ------------------------------------------------------------------------------------- derived / other channels

    /// <summary>Derived client-side by Alerts.Derive. id/component/severity are normative; message is informative (Spanish today).</summary>
    public sealed class Alert
    {
        public string id;
        public string component;
        public AlertSeverity severity;
        public string message;
    }

    public struct AlertSummary
    {
        public int count;
        /// <summary>null when count == 0.</summary>
        public AlertSeverity? worst;
    }

    /// <summary>HTTP GET /health of the worker.</summary>
    public sealed class HealthResponse
    {
        public bool ok;
        public int frames;
        public double rateHz;
    }

    /// <summary>JSON header of a TWF1 file. Open object: unknown keys are kept in Extra.</summary>
    public sealed class TwfHeader
    {
        public int version;
        public string[] fields = Array.Empty<string>();
        public double rate_hz;
        public int frame_count;
        public double duration_s; public bool HasDuration;
        public RotorGeometry[] rotors; // null when absent
        public string vehicleHardware, vehiclePx4Version;
        public int vehicleSysAutostart, vehicleBatCells;
        public string sourceLogId, sourceUrl, sourceDataset;
        public Dictionary<string, object> Raw;
    }
}
