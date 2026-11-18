// P2 of the MVP: what the simulator does when its battery runs down. Line-by-line port of src/shared/batteryfail.ts (contracts/golden/batteryreact.json).
// Pure C# (no UnityEngine). The stepping itself lives in FlightSim.StepBatteryReact (it needs the model's RTL and land modes).
//
// SIMULATED: a simplified imitation of what PX4 SITL did with a failed battery (warning, a hold of about 5 s, emergency landing, disarm about 2 s after
// the landing). This is NOT PX4 and not a real drone; every text says "simulado".
//
//   rem <= lowRem  (15 %)  warning event.
//   rem <= critRem (7 %)   critical event and automatic return (RTL of the model).
//   rem <= emptyRem (0 %)  critical event, a hold of EmergencyHoldS where the vehicle is, then an emergency landing; at touchdown the vehicle stays
//                          armed DisarmDelayS more seconds and is disarmed.
// With `react` off none of it happens (the battery drains and the vehicle keeps flying).

using System;
using System.Globalization;

namespace WebGL.Telemetry
{
    /// <summary>Battery reaction of the simulator. Off = "Ignorar bateria".</summary>
    public sealed class BatteryReactConfig
    {
        /// <summary>The battery acts on the flight (on by default).</summary>
        public bool react = true;
        /// <summary>State of charge (0..1) at or under which the warning is raised.</summary>
        public double lowRem = 0.15;
        /// <summary>... at or under which the critical event and the automatic return happen.</summary>
        public double critRem = 0.07;
        /// <summary>... at or under which the vehicle is considered empty: hold, emergency landing, disarm.</summary>
        public double emptyRem = 0;

        public BatteryReactConfig Clone() => new BatteryReactConfig { react = react, lowRem = lowRem, critRem = critRem, emptyRem = emptyRem };
    }

    /// <summary>Any members of the battery reaction (null = leave as is).</summary>
    public sealed class BatteryPatch
    {
        public bool? react;
        public double? lowRem, critRem, emptyRem;
    }

    /// <summary>What the reaction remembers between steps (SimState.batReact).</summary>
    public sealed class BatteryReactState
    {
        /// <summary>Highest stage already announced in this flight: 0 none, 1 warning, 2 critical, 3 empty. Each stage is raised once.</summary>
        public int stage;
        /// <summary>The emergency is in progress (empty reached while flying): the manoeuvre commands are refused until it ends.</summary>
        public bool emergency;
        /// <summary>Seconds of the hold left before the emergency landing; null when not holding.</summary>
        public double? holdLeft;
        /// <summary>Seconds left before the disarm after the touchdown of an emergency; null when not waiting.</summary>
        public double? disarmIn;
    }

    public static class BatteryReact
    {
        public const double EmptyMax = 0.05, CritMax = 0.25, LowMax = 0.5;
        /// <summary>Hold where the vehicle is before the emergency landing starts, s.</summary>
        public const double EmergencyHoldS = 5;
        /// <summary>Armed time after touchdown before the disarm, s.</summary>
        public const double DisarmDelayS = 2;

        /// <summary>Commands still accepted during the emergency.</summary>
        public static readonly string[] EmergencyAllowed = { "hold", "land", "motor_off", "restore" };
        public const string EmergencyDetail = "batería agotada: el simulador está en aterrizaje de emergencia";

        /// <summary>What the switch keeps in the app (Desk_SimBatteryReact): "0" = off, anything else = on.</summary>
        public const string StorageKey = "Desk_SimBatteryReact";

        public static BatteryReactConfig Default() => new BatteryReactConfig();
        public static BatteryReactState CreateState() => new BatteryReactState();

        static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        /// <summary>JS String(Math.round(x * 100)).replace('.', ',').</summary>
        static string Pct(double x) => JsMath.Round(x * 100).ToString("R", CultureInfo.InvariantCulture).Replace('.', ',');

        /// <summary>The ranges and the order of the thresholds. The first thing wrong is returned, with its Spanish reason.</summary>
        public static ConfigCheck Validate(BatteryReactConfig c)
        {
            if (!Finite(c.emptyRem) || c.emptyRem < 0 || c.emptyRem > EmptyMax)
                return ConfigCheck.No("batteryEmpty", "el umbral de batería agotada debe estar entre 0 y " + Pct(EmptyMax) + " %", 0, EmptyMax);
            if (!Finite(c.critRem) || c.critRem <= c.emptyRem || c.critRem > CritMax)
                return ConfigCheck.No("batteryCrit", "el umbral crítico debe ser mayor que el de agotada y de hasta " + Pct(CritMax) + " %", c.emptyRem, CritMax);
            if (!Finite(c.lowRem) || c.lowRem <= c.critRem || c.lowRem > LowMax)
                return ConfigCheck.No("batteryLow", "el umbral de aviso debe ser mayor que el crítico y de hasta " + Pct(LowMax) + " %", c.critRem, LowMax);
            return ConfigCheck.Ok;
        }

        /// <summary>"0" turns the reaction off, anything else (or nothing) leaves it on.</summary>
        public static bool ParseReact(string raw) => raw != "0";

        /// <summary>Event texts (Spanish, like the rest of the simulator's events; all of them say "simulado"). `rem` is the state of charge 0..1.</summary>
        public static class Text
        {
            public static string Low(double rem) => "Batería baja: " + Pct(rem) + " % · aviso (simulado)";
            public static string Crit(double rem) => "Batería crítica: " + Pct(rem) + " % · retorno automático (simulado)";
            public static string CritNoAction(double rem) => "Batería crítica: " + Pct(rem) + " % (simulado)";
            public static string Empty(double hold) => "Batería agotada · espera de " + hold.ToString("R", CultureInfo.InvariantCulture) + " s y aterrizaje de emergencia (simulado)";
            public const string EmptyLanding = "Batería agotada · aterrizaje de emergencia (simulado)";
            public const string EmptyNoAction = "Batería agotada (simulado)";
            public const string Landing = "Aterrizaje de emergencia (simulado)";
            public const string Disarmed = "Desarmado tras el aterrizaje de emergencia (simulado)";
        }
    }
}
