// Contract 1.2 (docs/CONTRATO_1_2.md section 5, web: src/shared/px4params.ts): the PX4 parameters a client may read or write through the bridge.
// DATA SUPPORT ONLY: no screen offers parameters yet (the parameter editor is Lote 2). A client that offers parameters must not offer what
// CheckRead / CheckSet refuse; the bridge and the relay check the same rules again (defence in depth). The whitelist below is a copy of
// contracts/px4_params.json (a test compares them); the cases of contracts/golden/params.json run against these two functions.
// Names, types and ranges are of PX4 v1.17 and are NOT all verified against a running PX4 (CONTRATO_1_2.md section 11).
// Pure C#: no Unity API.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace WebGL.Telemetry
{
    /// <summary>Relay roles, weakest first. A local bridge client counts as Admin.</summary>
    public enum ParamRole { Viewer = 0, Pilot = 1, Admin = 2 }

    public sealed class ParamRule
    {
        public string id;
        /// <summary>int32 | real32.</summary>
        public string type;
        /// <summary>Weakest role that may read it.</summary>
        public ParamRole read;
        /// <summary>Weakest role that may write it; null = read only.</summary>
        public ParamRole? write;
        public double? min, max;
        /// <summary>Allowed values (int32 enums); when present, min / max are not used.</summary>
        public double[] values;
        public string unit;
        /// <summary>Only in the relay profile 'sitl' (failure injection).</summary>
        public bool sitlOnly;
        public string note;
    }

    public sealed class ParamCheck
    {
        public bool ok;
        public ParamRule rule;
        /// <summary>param.notAllowed | param.outOfRange | param.badValue | param.armed | relay.role | relay.profile (null when ok).</summary>
        public string code;
        /// <summary>Values for the translation of `code` (id, min, max, values), as the web builds them.</summary>
        public Dictionary<string, object> codeParams;
    }

    public static class Px4Params
    {
        public const string IdPattern = "^[A-Z][A-Z0-9_]{0,15}$";
        /// <summary>Relative tolerance when comparing a written real32 with the value PX4 reads back.</summary>
        public const double ReadbackRelTol = 1e-4;

        static readonly Regex IdRegex = new Regex(IdPattern, RegexOptions.CultureInvariant);

        /// <summary>Never written by a client, whatever the whitelist says (calibrations, circuit breakers, outputs, arming checks, estimator, links, battery set-up, simulator, RC).</summary>
        public static readonly string[] NeverWrite =
        {
            "^CAL_", "^SENS_", "^CBRK_", "^PWM_", "^DSHOT_", "^MOT_", "^CA_ROTOR", "^CA_SV", "^CA_AIRFRAME$", "^COM_ARM_", "^COM_PREARM",
            "^SYS_AUTOSTART$", "^SYS_AUTOCONFIG$", "^SYS_HAS_", "^SYS_FAILURE_EN$", "^EKF2_", "^MAV_", "^BAT[0-9]?_", "^SIM_", "^RC[0-9]*_",
            "^COM_RC_IN_MODE$",
        };

        static readonly Regex[] Never = BuildNever();
        static Regex[] BuildNever()
        {
            var r = new Regex[NeverWrite.Length];
            for (int i = 0; i < r.Length; i++) r[i] = new Regex(NeverWrite[i], RegexOptions.CultureInvariant);
            return r;
        }

        static ParamRule R(string id, string type, ParamRole? write, double? min = null, double? max = null, double[] values = null, string unit = null, bool sitlOnly = false, string note = "") =>
            new ParamRule { id = id, type = type, read = ParamRole.Pilot, write = write, min = min, max = max, values = values, unit = unit, sitlOnly = sitlOnly, note = note };

        public static readonly ParamRule[] Rules =
        {
            R("GF_ACTION", "int32", ParamRole.Admin, values: new double[] { 1, 2, 3, 5 }, note: "Geofence breach action: 1 warning, 2 hold, 3 return, 5 land. 0 (none) and 4 (terminate) are excluded."),
            R("GF_MAX_HOR_DIST", "real32", ParamRole.Admin, 10, 500, unit: "m", note: "Geofence horizontal radius from home. 0 (disabled) is excluded."),
            R("GF_MAX_VER_DIST", "real32", ParamRole.Admin, 10, 120, unit: "m", note: "Geofence ceiling above home."),
            R("COM_LOW_BAT_ACT", "int32", ParamRole.Admin, values: new double[] { 2, 3 }, note: "Low battery action: 2 land, 3 return at critical then land at emergency. 0 (warning only) is excluded. Values change between PX4 versions: verify in v1.17."),
            R("COM_DL_LOSS_T", "int32", ParamRole.Admin, 5, 60, unit: "s", note: "Data link loss timeout."),
            R("NAV_DLL_ACT", "int32", ParamRole.Admin, values: new double[] { 1, 2, 3 }, note: "Data link loss action: 1 hold, 2 return, 3 land. 0 (disabled), 5 (terminate) and 6 (disarm) are excluded."),
            R("COM_RC_LOSS_T", "real32", ParamRole.Admin, 0.5, 5, unit: "s", note: "Manual control loss timeout (MANUAL_CONTROL from the bridge counts as RC)."),
            R("NAV_RCL_ACT", "int32", ParamRole.Admin, values: new double[] { 1, 2, 3 }, note: "Manual control loss action: 1 hold, 2 return, 3 land. The bridge sets 1 when piloting starts."),
            R("COM_ACT_FAIL_ACT", "int32", ParamRole.Admin, values: new double[] { 0, 1, 2, 3 }, note: "Actuator failure action: 0 disabled, 1 hold, 2 land, 3 return. 4 (terminate) is excluded. Verify the values in v1.17."),
            R("CA_FAILURE_MODE", "int32", ParamRole.Admin, values: new double[] { 0, 1 }, sitlOnly: true, note: "Control allocator obeys the failure injector (1). The bridge sets 1 on motor_off (docs/INVESTIGACION_FALLAS_PX4.md)."),
            R("RTL_RETURN_ALT", "real32", ParamRole.Admin, 5, 50, unit: "m", note: "Return altitude above home."),
            R("MIS_TAKEOFF_ALT", "real32", ParamRole.Pilot, 1, 50, unit: "m", note: "Takeoff altitude; the bridge also writes it on every takeoff (1..50 m clamp)."),
            R("MPC_XY_VEL_MAX", "real32", ParamRole.Pilot, 1, 12, unit: "m/s", note: "Maximum horizontal speed."),
            // read only
            R("SYS_FAILURE_EN", "int32", null, note: "Failure injection enabled (read at boot). Written by the bridge only, never by a client."),
            R("COM_RC_IN_MODE", "int32", null, note: "Manual control source; managed by the bridge (pilot)."),
            R("COM_RCL_EXCEPT", "int32", null, note: "Modes that ignore manual control loss."),
            R("BAT_LOW_THR", "real32", null, note: "Low battery threshold (fraction)."),
            R("BAT_CRIT_THR", "real32", null, note: "Critical battery threshold (fraction)."),
            R("BAT_EMERGEN_THR", "real32", null, note: "Emergency battery threshold (fraction)."),
            R("SYS_AUTOSTART", "int32", null, note: "Airframe id."),
        };

        static readonly Dictionary<string, ParamRule> ById = BuildIndex();
        static Dictionary<string, ParamRule> BuildIndex()
        {
            var d = new Dictionary<string, ParamRule>(StringComparer.Ordinal);
            foreach (ParamRule r in Rules) d[r.id] = r;
            return d;
        }

        public static ParamRule Rule(string id) => id != null && ById.TryGetValue(id, out ParamRule r) ? r : null;

        public static bool NeverWritten(string id)
        {
            foreach (Regex rx in Never) if (rx.IsMatch(id ?? string.Empty)) return true;
            return false;
        }

        public static bool ValidId(string id) => id != null && IdRegex.IsMatch(id);

        /// <summary>May `role` read `id`?</summary>
        public static ParamCheck CheckRead(string id, ParamRole role)
        {
            ParamRule rule = Rule(id);
            if (rule == null) return Fail("param.notAllowed", id);
            if (role < rule.read) return Fail("relay.role", id);
            return new ParamCheck { ok = true, rule = rule };
        }

        /// <summary>
        /// May this value be written? Order: whitelist and never-write, role, profile, value, armed. `sitlProfile` is the relay profile (true = 'sitl');
        /// `armed` null = no telemetry yet (treated as armed: nothing is written without a frame).
        /// </summary>
        public static ParamCheck CheckSet(string id, double value, ParamRole role, bool sitlProfile, bool? armed)
        {
            ParamRule rule = Rule(id);
            if (rule == null || !rule.write.HasValue || NeverWritten(id)) return Fail("param.notAllowed", id);
            if (role < rule.write.Value) return Fail("relay.role", id);
            if (rule.sitlOnly && !sitlProfile) return Fail("relay.profile", id);
            if (double.IsNaN(value) || double.IsInfinity(value) || (rule.type == "int32" && value != Math.Floor(value))) return Fail("param.badValue", id);
            if (rule.values != null)
            {
                if (Array.IndexOf(rule.values, value) < 0) return Fail("param.badValue", id, ("values", string.Join(",", Array.ConvertAll(rule.values, v => v.ToString("R", CultureInfo.InvariantCulture)))));
            }
            else if ((rule.min.HasValue && value < rule.min.Value) || (rule.max.HasValue && value > rule.max.Value))
            {
                return Fail("param.outOfRange", id, ("min", rule.min.HasValue ? (object)rule.min.Value : string.Empty), ("max", rule.max.HasValue ? (object)rule.max.Value : string.Empty));
            }
            if (armed != false) return Fail("param.armed", id);
            return new ParamCheck { ok = true, rule = rule };
        }

        /// <summary>Whether a value PX4 read back counts as the one written: an integer exactly, a real within ReadbackRelTol · max(1, |written|) (the bridge's rule).</summary>
        public static bool ReadbackMatches(ParamRule rule, double written, double readBack)
        {
            if (rule.type == "int32") return (long)written == (long)readBack;
            return Math.Abs(written - readBack) <= ReadbackRelTol * Math.Max(1.0, Math.Abs(written));
        }

        static ParamCheck Fail(string code, string id, params (string, object)[] extra)
        {
            var p = new Dictionary<string, object> { { "id", id } };
            foreach (var kv in extra) p[kv.Item1] = kv.Item2;
            return new ParamCheck { ok = false, code = code, codeParams = p };
        }
    }
}
