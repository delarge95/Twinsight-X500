// PLAN_PC 27.3: the content of the fault / alert explanation card (pure C#, compiled everywhere, tested in EditMode).
// Four blocks: "Qué pasa" (the data: an alert sentence with its origin, or the synthetic fault with its countdown), "Qué significa" and "Qué haría el
// controlador / el piloto" (general explanations), and "En esta fuente" (what THIS source really does with the fault: it depends on the active
// source and is the sensitive part). The texts are the approved ones of PLAN_CONTROL_REAL_Y_ENTORNOS 2.4 (D1) kept in TelemetryStrings ("fx.*");
// an alert with no approved text (controller temperature, battery temperature) gets a neutral factual line.
// Honesty: the explanation is didactic, never a measurement; the figures and their origins come from the displayed frame and hello.provenance.

using System;
using System.Collections.Generic;

namespace WebGL.Telemetry
{
    /// <summary>Which source the explanation talks about (the card says what THIS source does with the fault).</summary>
    public enum ExplainSource { None, LocalReplay, Simulator, Sitl }

    /// <summary>The approved explanations: one per kind of fault. Neutral = an alert without an approved text.</summary>
    public enum FaultTopic { MotorFailure, MotorOverheat, BatterySag, Vibration, Neutral }

    public struct AffectedReading
    {
        public string LabelKey;
        public ReadingOrigin Origin;
    }

    public static class FaultExplain
    {
        public static FaultTopic TopicOf(FaultKind k)
        {
            switch (k)
            {
                case FaultKind.MotorFailure: return FaultTopic.MotorFailure;
                case FaultKind.MotorOverheat: return FaultTopic.MotorOverheat;
                case FaultKind.BatterySag: return FaultTopic.BatterySag;
                default: return FaultTopic.Vibration;
            }
        }

        public static FaultKind KindOf(FaultTopic t)
        {
            switch (t)
            {
                case FaultTopic.MotorOverheat: return FaultKind.MotorOverheat;
                case FaultTopic.BatterySag: return FaultKind.BatterySag;
                case FaultTopic.Vibration: return FaultKind.Vibration;
                default: return FaultKind.MotorFailure;
            }
        }

        /// <summary>The explanation that goes with an alert id (motorN:out, motorN:temp, battery:rem, fc:vib; anything else is neutral).</summary>
        public static FaultTopic TopicOfAlert(string alertId)
        {
            if (string.IsNullOrEmpty(alertId)) return FaultTopic.Neutral;
            if (alertId.EndsWith(":out", StringComparison.Ordinal)) return FaultTopic.MotorFailure;
            if (alertId.StartsWith("motor", StringComparison.Ordinal) && alertId.EndsWith(":temp", StringComparison.Ordinal)) return FaultTopic.MotorOverheat;
            if (alertId == "battery:rem") return FaultTopic.BatterySag;
            if (alertId == "fc:vib") return FaultTopic.Vibration;
            return FaultTopic.Neutral;
        }

        /// <summary>True when the topic has the approved explanation of PLAN_CONTROL_REAL_Y_ENTORNOS 2.4 (Neutral has not).</summary>
        public static bool IsApproved(FaultTopic t) => t != FaultTopic.Neutral;

        static string Key(FaultTopic t)
        {
            switch (t)
            {
                case FaultTopic.MotorFailure: return "a";
                case FaultTopic.MotorOverheat: return "c";
                case FaultTopic.BatterySag: return "bat";
                case FaultTopic.Vibration: return "d";
                default: return "n";
            }
        }

        static string Motor(int m) => (m >= 1 && m <= 4 ? m : 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

        // ───────────────────────── names ─────────────────────────

        /// <summary>"Motor 3 cortado" / "Caída de batería": the state of a synthetic fault.</summary>
        public static string StateName(FaultKind k, int motor, string lang) =>
            TelemetryStrings.F("fx.state." + WireNames.Of(k), lang, "n", Motor(motor));

        /// <summary>The label of the marker pinned on the part: "Motor 3 · detenido".</summary>
        public static string PinName(FaultKind k, int motor, string lang) =>
            TelemetryStrings.F("fx.pin." + WireNames.Of(k), lang, "n", Motor(motor));

        public static string KindTab(FaultTopic t, string lang) => TelemetryStrings.T("fx.kind." + WireNames.Of(KindOf(t)), lang);

        // ───────────────────────── the blocks ─────────────────────────

        public static string Means(FaultTopic t, string lang) => TelemetryStrings.T("fx." + Key(t) + ".means", lang);

        /// <summary>The block title: the controller for the motor and the battery, the pilot for heat and vibration (as in the approved texts).</summary>
        public static string ControllerHeading(FaultTopic t, string lang) =>
            TelemetryStrings.T(t == FaultTopic.MotorOverheat || t == FaultTopic.Vibration ? "fx.h.pilot" : "fx.h.ctrl", lang);

        public static string Controller(FaultTopic t, string lang) => TelemetryStrings.T("fx." + Key(t) + ".ctrl", lang);

        /// <summary>The controller block for the active source: null = the block is not shown. The simulator's battery is not PX4: its own behaviour is the approved `simbat.note`
        /// (shown in «En esta fuente»), so PX4's 15 % / 7 % line is left out (web: controllerBySource { sim: null }).</summary>
        public static string Controller(FaultTopic t, ExplainSource s, string lang) =>
            t == FaultTopic.BatterySag && s == ExplainSource.Simulator ? null : Controller(t, lang);

        /// <summary>"En esta fuente": what the ACTIVE source does with this fault. No session = null (the block is not shown).</summary>
        public static string InSource(FaultTopic t, ExplainSource s, int motor, string lang)
        {
            switch (s)
            {
                case ExplainSource.LocalReplay: return TelemetryStrings.F("fx." + Key(t) + ".src.replay", lang, "n", Motor(motor));
                case ExplainSource.Sitl:
                    // battery in SITL: the approved and verified text of 2026-11-17 (cmd.batteryNote) replaces the old line of section 2.4 B
                    if (t == FaultTopic.BatterySag) return TelemetryStrings.T("cmd.batteryNote", lang);
                    return TelemetryStrings.F("fx." + Key(t) + ".src.sitl", lang, "n", Motor(motor));
                case ExplainSource.Simulator:
                    // the battery (P2: the approved simbat.note, it warns, returns and lands) and (Lote 1a) the motor failure have their own simulator text; every other fault: the simulator does not inject faults
                    if (t == FaultTopic.BatterySag) return TelemetryStrings.T("simbat.note", lang);
                    if (t == FaultTopic.MotorFailure) return TelemetryStrings.T("fx.a.src.sim", lang);
                    return TelemetryStrings.T("fx.src.sim", lang);
                default: return null;
            }
        }

        /// <summary>The seconds of a synthetic fault left at `nowMs` (stream time); never negative.</summary>
        public static int SecondsLeft(Fault f, double nowMs) => Math.Max(0, (int)Math.Ceiling((f.until - nowMs) / 1000.0));

        /// <summary>"Motor 3 cortado · 24 s restantes de 30 s".</summary>
        public static string FaultLine(Fault f, double nowMs, string lang) =>
            TelemetryStrings.F("fx.what.fault", lang, "name", StateName(f.kind, f.motor, lang), "s", SecondsLeft(f, nowMs));

        // ───────────────────────── the readings the fault / alert touches, each with its origin ─────────────────────────

        /// <summary>The readings that the topic changes (or that the alert reports) and where each one comes from (hello.provenance; a simulator: simulated).</summary>
        public static List<AffectedReading> Affected(FaultTopic t, string alertId, HelloMessage h, SourceClassification c, bool simulator)
        {
            var r = new List<AffectedReading>();
            void Add(string key, ReadingOrigin o) => r.Add(new AffectedReading { LabelKey = key, Origin = FlightOrigins.ForSession(o, simulator) });
            switch (t)
            {
                case FaultTopic.MotorFailure:
                    Add("fx.rd.rpm", FlightOrigins.Rpm(h, c));
                    Add("fx.rd.motorcmd", FlightOrigins.Honest(ReadingOrigin.Command, c));
                    break;
                case FaultTopic.MotorOverheat:
                    Add("fx.rd.motortemp", FlightOrigins.MotorTemp(h, c));
                    break;
                case FaultTopic.BatterySag:
                    Add("fx.rd.batv", FlightOrigins.Honest(ReadingOrigin.Measured, c));
                    Add("fx.rd.battemp", FlightOrigins.BatteryTemp(h, c));
                    break;
                case FaultTopic.Vibration:
                    Add("fx.rd.vib", FlightOrigins.Honest(ReadingOrigin.Measured, c));
                    break;
                default:
                    if (alertId == "fc:temp") Add("fx.rd.fctemp", FlightOrigins.FcTemp(h, c));
                    else Add("fx.rd.battemp", FlightOrigins.BatteryTemp(h, c));
                    break;
            }
            return r;
        }
    }
}
