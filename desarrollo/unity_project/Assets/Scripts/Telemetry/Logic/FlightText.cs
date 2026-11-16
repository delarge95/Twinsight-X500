// WP-11 texts and origins of the desktop telemetry UI (pure C#: compiled everywhere, tested in EditMode).
//  - ReadingOrigin / FlightOrigins: the origin (medido / estimado / simulado / comando / no disponible) every reading carries. Channels that
//    have a provenance field in `hello` (rpm, motorTemp, batteryTemp, fcTemp, batteryCurrent) use it exactly as the web does
//    (panels.tsx TelemetryPanel); the rest (pose, speeds, attitude, battery voltage and level, vibration) have no field: they are
//    measured on a replay of a real flight and simulated on a simulator source (SITL), and a channel the source calls "measured" is
//    still "simulado" on a simulator. The words come from labels.json (HonestyLabels), never from this file.
//  - AlertText: the approved D-4 alert wording (PROPUESTA_TEXTOS_HONESTIDAD H-15..H-17), ES/EN, with the origin and the
//    "falla sintética" suffix, built from the alert id/component and the displayed frame (the informative message of Alerts.Derive is
//    fixed Spanish and is not used by the desktop UI).
//  - TelemetryStrings: the non-honesty UI strings, ES/EN, copied from the web dictionaries (src/web/i18n) plus the D-6 names.
// No Unity API here.

using System;
using System.Collections.Generic;
using System.Text;

namespace WebGL.Telemetry
{
    public enum ReadingOrigin { Measured, Estimated, Simulated, Command, Unavailable }

    public static class FlightOrigins
    {
        public static ReadingOrigin FromWire(string wire)
        {
            switch (wire)
            {
                case "estimated": return ReadingOrigin.Estimated;
                case "command": return ReadingOrigin.Command;
                case "unavailable": return ReadingOrigin.Unavailable;
                default: return ReadingOrigin.Measured;
            }
        }

        /// <summary>Origin of the channels without a provenance field: real flight data on a replay, simulator data on SITL.</summary>
        public static ReadingOrigin Base(SourceClassification c) => c == SourceClassification.Sitl ? ReadingOrigin.Simulated : ReadingOrigin.Measured;

        /// <summary>A "measured" value coming from a simulator is simulated; the other origins stay as the source declares them.</summary>
        public static ReadingOrigin Honest(ReadingOrigin o, SourceClassification c) => o == ReadingOrigin.Measured && c == SourceClassification.Sitl ? ReadingOrigin.Simulated : o;

        /// <summary>
        /// In the flight simulator (E-5) EVERY reading is simulated, whatever the hello declares for it (estimated, command...): the only
        /// exception is a channel the source says it does not have (unavailable). Other sources keep their own origin.
        /// </summary>
        public static ReadingOrigin ForSession(ReadingOrigin o, bool simulator) => simulator && o != ReadingOrigin.Unavailable ? ReadingOrigin.Simulated : o;

        static ReadingOrigin Of(string wire, SourceClassification c) => Honest(FromWire(wire), c);

        public static ReadingOrigin Rpm(HelloMessage h, SourceClassification c) => h == null ? Base(c) : Of(h.provenance.rpm, c);
        public static ReadingOrigin MotorTemp(HelloMessage h, SourceClassification c) => h == null ? ReadingOrigin.Estimated : Of(h.provenance.motorTemp, c);
        public static ReadingOrigin BatteryTemp(HelloMessage h, SourceClassification c) => h == null ? ReadingOrigin.Estimated : Of(h.provenance.batteryTemp, c);
        public static ReadingOrigin FcTemp(HelloMessage h, SourceClassification c) => h == null ? ReadingOrigin.Unavailable : Of(h.provenance.fcTemp, c);
        public static ReadingOrigin BatteryCurrent(HelloMessage h, SourceClassification c) => h == null ? Base(c) : Of(h.provenance.BatteryCurrentOrDefault, c);

        /// <summary>The origin word (labels.json): "medido" / "estimado" / "simulado" (thermalOrigin), "comando" / "no disponible" (provenance).</summary>
        public static string Text(ReadingOrigin o, string lang, HonestyLabels labels)
        {
            switch (o)
            {
                case ReadingOrigin.Estimated: return labels.ThermalOriginText(ThermalOrigin.Estimated, lang);
                case ReadingOrigin.Simulated: return labels.ThermalOriginText(ThermalOrigin.Simulated, lang);
                case ReadingOrigin.Command: return labels.ProvenanceText("command", lang);
                case ReadingOrigin.Unavailable: return labels.ProvenanceText("unavailable", lang);
                default: return labels.ThermalOriginText(ThermalOrigin.Measured, lang);
            }
        }
    }

    public static class AlertText
    {
        /// <summary>The fault component an active synthetic fault stands for (the same mapping the web uses to isolate the part).</summary>
        public static bool FaultHitsComponent(Fault f, string component)
        {
            switch (f.kind)
            {
                case FaultKind.MotorFailure:
                case FaultKind.MotorOverheat: return component == "motor" + (f.HasMotor && f.motor != 0 ? f.motor : 1);
                case FaultKind.BatterySag: return component == "battery";
                case FaultKind.Vibration: return component == "fc";
                default: return false;
            }
        }

        /// <summary>True when a synthetic fault active at `nowMs` (stream time) acts on the alert's component.</summary>
        public static bool IsSynthetic(Alert a, IReadOnlyList<Fault> faults, double nowMs)
        {
            if (a == null || faults == null) return false;
            for (int i = 0; i < faults.Count; i++)
            {
                var f = faults[i];
                if (f.since <= nowMs && f.until > nowMs && FaultHitsComponent(f, a.component)) return true;
            }
            return false;
        }

        static string RpmAdjective(ReadingOrigin o, string lang)
        {
            bool es = lang != "en";
            switch (o)
            {
                case ReadingOrigin.Estimated: return es ? "estimadas" : "estimated";
                case ReadingOrigin.Simulated: return es ? "simuladas" : "simulated";
                case ReadingOrigin.Command: return es ? "de comando" : "commanded";
                default: return es ? "medidas" : "measured";
            }
        }

        static int MotorOf(Alert a)
        {
            if (a.component != null && a.component.StartsWith("motor", StringComparison.Ordinal) && int.TryParse(a.component.Substring(5), out int m)) return m;
            return 0;
        }

        /// <summary>
        /// The alert sentence in `lang` ("es"/"en"), with its origin and, when `synthetic`, the "falla sintética" suffix (labels.json
        /// faultSynthetic). Values come from the displayed frame; the origin from hello.provenance and the chosen source.
        /// </summary>
        public static string Build(Alert a, TelemetryFrame f, HelloMessage h, SourceClassification c, bool synthetic, string lang, HonestyLabels labels)
        {
            bool es = lang != "en";
            var sb = new StringBuilder(96);
            int m = MotorOf(a);
            string kind = a.id != null && a.id.IndexOf(':') >= 0 ? a.id.Substring(a.id.IndexOf(':') + 1) : string.Empty;
            string unit = " °C";

            if (m >= 1 && m <= 4 && kind == "out")
            {
                double sum = 0;
                for (int j = 0; j < 4; j++) if (j != m - 1) sum += f.rpm[j];
                string o = RpmAdjective(FlightOrigins.Rpm(h, c), lang);
                string rpm = JsMath.Round(f.rpm[m - 1]).ToString(System.Globalization.CultureInfo.InvariantCulture);
                string mean = JsMath.Round(sum / 3).ToString(System.Globalization.CultureInfo.InvariantCulture);
                sb.Append(es ? "Motor " : "Motor ").Append(m).Append(": ");
                if (es) sb.Append("RPM ").Append(o).Append(" muy bajas (").Append(rpm).Append(" frente a ").Append(mean).Append(" de media)");
                else sb.Append(o).Append(" RPM very low (").Append(rpm).Append(" vs. ").Append(mean).Append(" mean)");
            }
            else if (m >= 1 && m <= 4)
            {
                sb.Append("Motor ").Append(m).Append(": ").Append(JsMath.ToFixed(f.temp[m - 1], 0)).Append(unit)
                  .Append(" (").Append(FlightOrigins.Text(FlightOrigins.MotorTemp(h, c), lang, labels)).Append(')');
            }
            else if (a.id == "battery:rem")
            {
                sb.Append(es ? "Batería al " : "Battery at ").Append(JsMath.ToFixed(f.bat.rem * 100, 0)).Append(" % (").Append(JsMath.ToFixed(f.bat.v, 1)).Append(" V)");
                AppendIfNotMeasured(sb, FlightOrigins.Honest(ReadingOrigin.Measured, c), lang, labels);
            }
            else if (a.id == "battery:temp")
            {
                sb.Append(es ? "Batería: " : "Battery: ").Append(JsMath.ToFixed(f.temp[4], 0)).Append(unit)
                  .Append(" (").Append(FlightOrigins.Text(FlightOrigins.BatteryTemp(h, c), lang, labels)).Append(')');
            }
            else if (a.id == "fc:temp")
            {
                sb.Append(es ? "Controladora: " : "Controller: ").Append(JsMath.ToFixed(f.temp[5], 0)).Append(unit);
                AppendIfNotMeasured(sb, FlightOrigins.FcTemp(h, c), lang, labels);
            }
            else if (a.id == "fc:vib")
            {
                sb.Append(es ? "Vibración del acelerómetro: " : "Accelerometer vibration: ").Append(JsMath.ToFixed(f.vib, 1)).Append(" m/s²");
                AppendIfNotMeasured(sb, FlightOrigins.Honest(ReadingOrigin.Measured, c), lang, labels);
            }
            else sb.Append(a.message ?? a.id ?? string.Empty);

            if (synthetic) sb.Append(" · ").Append(SyntheticFault(lang, labels));
            return sb.ToString();
        }

        static void AppendIfNotMeasured(StringBuilder sb, ReadingOrigin o, string lang, HonestyLabels labels)
        {
            if (o != ReadingOrigin.Measured) sb.Append(" (").Append(FlightOrigins.Text(o, lang, labels)).Append(')');
        }

        /// <summary>"falla sintética" / "synthetic fault" from labels.json faultSynthetic ("sintética" / "synthetic").</summary>
        public static string SyntheticFault(string lang, HonestyLabels labels)
        {
            string w = labels.FaultSynthetic(lang);
            return lang == "en" ? w + " fault" : "falla " + w;
        }
    }

    /// <summary>
    /// Non-honesty UI strings of the desktop telemetry UI. Source: the web dictionaries (src/web/i18n/es.ts, en.ts) where a key exists
    /// (named in the comments) and the approved decisions D-1..D-8 (PROPUESTA_TEXTOS_HONESTIDAD §7) otherwise. The honesty labels
    /// (badges, details, origins, "sintética") are NOT here: they come from HonestyLabels.
    /// </summary>
    public static class TelemetryStrings
    {
        // key -> { es, en }
        static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            // names (D-6)
            { "mode4", new[] { "Telemetría", "Telemetry" } },                                                       // shell.mode.flight -> D-6 mode name
            { "mode4.tip", new[] { "Telemetría: encendido, fuente, alertas y fallas", "Telemetry: power, source, alerts and faults" } },
            { "power.switch", new[] { "Encendido", "Power" } },                                                      // D-6 (power.button POWER -> Encendido)
            { "power.switch.start", new[] { "Activar telemetría", "Turn telemetry on" } },                            // tsw.activate
            { "power.switch.stop", new[] { "Apagar telemetría", "Turn telemetry off" } },                            // tsw.turnOff
            { "power.sim", new[] { "Energía (simulada)", "Power (simulated)" } },                                    // D-6 + task wording
            { "power.sim.tip", new[] { "Encender o apagar la energía simulada", "Toggle simulated power" } },
            { "power.sim.note", new[] { "Simulación local de encendido y carga. No es telemetría.", "Local simulation of power and load. It is not telemetry." } },
            { "power.highLoad", new[] { "CARGA ALTA (SIMULADO)", "HIGH LOAD (SIMULATED)" } },                         // D-6 (power.flying VOLANDO -> Carga alta (simulado))
            { "power.chip.off", new[] { "Apagar energía simulada", "Switch off simulated power" } },
            { "tab.part", new[] { "Pieza", "Part" } },
            { "tab.flight", new[] { "Vuelo", "Flight" } },                                                            // D-6 tab name

            // link state (tsw.online, conn.connecting, conn.offline; "datos atrasados": PARIDAD §3.4)
            { "link.live", new[] { "en línea", "online" } },
            { "link.connecting", new[] { "conectando…", "connecting…" } },
            { "link.offline", new[] { "sin conexión", "offline" } },
            { "link.stale", new[] { "datos atrasados", "stale data" } },
            { "link.off", new[] { "apagada", "off" } },

            // strip buttons
            { "strip.source", new[] { "Fuente", "Source" } },
            { "strip.alerts", new[] { "Alertas", "Alerts" } },                                                        // alerts.title
            { "strip.faults", new[] { "Fallas", "Faults" } },
            // 28.2 / 28.3: environment toggles and the simulated test course (honest: simulated, no physics, nothing about real drones)
            { "env.ground", new[] { "Suelo", "Ground" } },
            { "env.ground.tip", new[] { "Suelo del mundo con rejilla de 1, 10 y 100 m, sombra del dron y línea de altitud. Se puede ocultar.", "World ground with a 1, 10 and 100 m grid, the drone's shadow and an altitude line. It can be hidden." } },
            { "course.btn", new[] { "Escenario", "Course" } },
            { "course.label", new[] { "Escenario de prueba simulado", "Simulated test scenario" } },
            { "course.tip", new[] { "Escenario de prueba simulado: aros, conos, un muro con hueco y zonas para volar en el simulador. Sin física: las colisiones son pruebas de volumen simples sobre la posición del dron.", "Simulated test scenario: rings, pylons, a wall with a gap and zones to fly in the simulator. No physics: collisions are simple volume tests on the drone position." } },
            { "course.tip.off", new[] { "Disponible solo con el simulador de vuelo.", "Available only with the flight simulator." } },
            { "course.waiting", new[] { "Despega o pasa el aro 1", "Take off or fly ring 1" } },
            { "course.cp", new[] { "Puntos {k}/{n}", "Points {k}/{n}" } },
            { "course.hits", new[] { "Choques {c}", "Hits {c}" } },
            { "course.reset", new[] { "Reiniciar", "Reset" } },
            { "course.reset.tip", new[] { "Empezar otra vuelta: tiempo, puntos y choques a cero", "Start another lap: time, points and hits back to zero" } },
            { "course.done", new[] { "Vuelta completa en {t} · choques: {c}", "Lap complete in {t} · hits: {c}" } },

            // Lote 1a, tool 9: the geofence editor of the simulator (wording of the web dictionaries src/web/i18n, fence.* and tools.fence*; simulated, local to the simulator)
            { "tools.fence", new[] { "Geocerca", "Geofence" } },
            { "tools.fence.need", new[] { "La geocerca editable es la del simulador: elige la fuente Simulador.", "The editable geofence is the one of the simulator: choose the Simulator source." } },
            { "fence.title", new[] { "Geocerca", "Geofence" } },
            { "fence.origin", new[] { "Geocerca del simulador · simulado", "Simulator geofence · simulated" } },
            { "fence.radius", new[] { "Radio", "Radius" } },
            { "fence.ceiling", new[] { "Techo", "Ceiling" } },
            { "fence.rtl", new[] { "Altura de RTL: {n} m", "RTL altitude: {n} m" } },
            { "fence.reset", new[] { "Restablecer 120/60", "Reset 120/60" } },
            { "fence.statusOk", new[] { "Dentro de la geocerca", "Inside the geofence" } },
            { "fence.statusNear", new[] { "A menos de 5 m del límite", "Less than 5 m from the limit" } },
            { "fence.statusHit", new[] { "En el límite: el simulador lo detiene", "At the limit: the simulator stops it" } },
            { "fence.honest", new[] { "Solo el simulador tiene esta geocerca. La de PX4 (GF_*) no llega por el enlace: en SITL no se dibuja. 120 m de techo es el habitual de la categoría abierta, a título informativo.",
                                      "Only the simulator has this geofence. The fence of PX4 itself (GF_*) does not reach the link: nothing is drawn in SITL. A 120 m ceiling is the usual open-category limit, for information only." } },
            { "fence.flightRule", new[] { "En vuelo no se acepta un radio menor que la distancia actual + 5 m ni un techo menor que la altura + 2 m: el dron nunca se teletransporta.",
                                          "In flight a radius below the current distance + 5 m or a ceiling below the height + 2 m is refused: the drone is never teleported." } },
            { "fence.err.not_finite", new[] { "Valores no válidos.", "Invalid values." } },
            { "fence.err.radius_range", new[] { "El radio debe estar entre {min} y {max} m.", "The radius must be between {min} and {max} m." } },
            { "fence.err.radius_step", new[] { "El radio avanza en pasos de {min} m.", "The radius goes in steps of {min} m." } },
            { "fence.err.ceiling_range", new[] { "El techo debe estar entre {min} y {max} m.", "The ceiling must be between {min} and {max} m." } },
            { "fence.err.ceiling_step", new[] { "El techo avanza en pasos de {min} m.", "The ceiling goes in steps of {min} m." } },
            { "fence.err.rtl_range", new[] { "La altura de RTL no puede ser menor que {min} m.", "The RTL altitude cannot be below {min} m." } },
            { "fence.err.rtl_above_ceiling", new[] { "La altura de RTL no puede superar el techo ({max} m).", "The RTL altitude cannot exceed the ceiling ({max} m)." } },
            { "fence.err.radius_below_position", new[] { "En vuelo el radio debe superar en 5 m la distancia actual: mínimo {min} m. Acércate al origen o aterriza.",
                                                         "In flight the radius must exceed the current distance by 5 m: minimum {min} m. Move towards the origin or land." } },
            { "fence.err.ceiling_below_altitude", new[] { "En vuelo el techo debe superar en 2 m la altura actual: mínimo {min} m. Baja o aterriza.",
                                                          "In flight the ceiling must exceed the current height by 2 m: minimum {min} m. Descend or land." } },

            // Lote 1a, tool 4: the simulated wind (wording of the web dictionaries src/web/i18n, wind.* and tools.wind*; simulated, local to the simulator)
            { "tools.wind", new[] { "Viento", "Wind" } },
            { "tools.wind.need", new[] { "El viento simulado es del simulador: elige la fuente Simulador. En PX4 SITL el viento es el de Gazebo y no se ofrece aquí.",
                                         "The simulated wind belongs to the simulator: pick the Simulator source. In PX4 SITL the wind is Gazebo's and is not offered here." } },
            { "wind.title", new[] { "Viento y turbulencia", "Wind and turbulence" } },
            { "wind.origin", new[] { "Viento del simulador · simulado", "Simulator wind · simulated" } },
            { "wind.speed", new[] { "Velocidad", "Speed" } },
            { "wind.gusts", new[] { "Ráfagas", "Gusts" } },
            { "wind.gustsValue", new[] { "±{n} m/s", "±{n} m/s" } },
            { "wind.dir", new[] { "De dónde viene", "Where it comes from" } },
            { "wind.dirValue", new[] { "{deg}° ({name})", "{deg}° ({name})" } },
            { "wind.seed", new[] { "Semilla {n}", "Seed {n}" } },
            { "wind.seedNew", new[] { "Otra", "Another" } },
            { "wind.seedHint", new[] { "La misma semilla repite las mismas ráfagas.", "The same seed repeats the same gusts." } },
            { "wind.reset", new[] { "Sin viento", "No wind" } },
            { "wind.reading", new[] { "Viento {speed} m/s del {dir} · ráfagas ±{gust} m/s · simulado", "Wind {speed} m/s from the {dir} · gusts ±{gust} m/s · simulated" } },
            { "wind.readingSteady", new[] { "Viento {speed} m/s del {dir} · sin ráfagas · simulado", "Wind {speed} m/s from the {dir} · no gusts · simulated" } },
            { "wind.readingCalm", new[] { "Sin viento · simulado", "No wind · simulated" } },
            { "wind.now", new[] { "Ahora: {speed} m/s del {dir}", "Now: {speed} m/s from the {dir}" } },
            { "wind.honest", new[] { "Modelo sencillo del simulador: el viento empuja al dron, lo inclina contra él y le hace gastar más corriente, solo en vuelo. No es PX4 ni un dron real. En PX4 SITL el viento es el de Gazebo y no se ofrece aquí.",
                                     "Simple simulator model: the wind pushes the drone, makes it lean against it and burn more current, in flight only. It is not PX4 nor a real drone. In PX4 SITL the wind is Gazebo's and is not offered here." } },
            { "wind.dir.0", new[] { "N", "N" } },
            { "wind.dir.1", new[] { "NE", "NE" } },
            { "wind.dir.2", new[] { "E", "E" } },
            { "wind.dir.3", new[] { "SE", "SE" } },
            { "wind.dir.4", new[] { "S", "S" } },
            { "wind.dir.5", new[] { "SO", "SW" } },
            { "wind.dir.6", new[] { "O", "W" } },
            { "wind.dir.7", new[] { "NO", "NW" } },
            { "wind.err.wind_not_finite", new[] { "Valores de viento no válidos.", "The wind values are not valid." } },
            { "wind.err.wind_speed_range", new[] { "La velocidad debe estar entre {min} y {max} m/s.", "The speed must be between {min} and {max} m/s." } },
            { "wind.err.wind_from_range", new[] { "La dirección debe estar entre {min} y {max} grados.", "The direction must be between {min} and {max} degrees." } },
            { "wind.err.wind_gust_range", new[] { "Las ráfagas deben estar entre {min} y {max} m/s.", "The gusts must be between {min} and {max} m/s." } },
            { "wind.err.wind_tau_range", new[] { "El tiempo de ráfaga debe estar entre {min} y {max} s.", "The gust time must be between {min} and {max} s." } },
            { "wind.err.wind_seed", new[] { "La semilla es un entero de 32 bits sin signo.", "The seed is an unsigned 32-bit integer." } },

            // Lote 1a, tool 10: motor cut of the simulator (wording of the web dictionaries src/web/i18n, simfault.* and cmd.ackOkSim). simfault.note is the line «En esta fuente» of the
            // simulator for the motor failure, marked [APROBADO 2026-11-13] in the web; the rest is the web wording (simulated, a simplified model: not PX4, not a real drone)
            { "simfault.title", new[] { "Fallas del simulador", "Simulator faults" } },
            { "simfault.hint", new[] { "simulado", "simulated" } },
            { "simfault.note", new[] { "Simulador: el motor deja de empujar en un modelo simplificado; no es PX4 ni un dron real.",
                                       "Simulator: the motor stops pushing in a simplified model; this is not PX4 or a real drone." } },
            { "simfault.cut", new[] { "Cortar motor {n}", "Cut motor {n}" } },
            { "simfault.cutConfirm", new[] { "¿Cortar motor {n}? Toca otra vez", "Cut motor {n}? Tap again" } },
            { "simfault.restore", new[] { "Restaurar", "Restore" } },
            { "simfault.comp", new[] { "Compensación didáctica", "Didactic compensation" } },
            { "simfault.compNote", new[] { "Apaga también el motor opuesto y deja los otros dos a tope. Es un ejemplo didáctico, no el controlador de PX4.",
                                           "Also shuts down the opposite motor and runs the other two flat out. A teaching example, not the PX4 controller." } },
            { "simfault.status", new[] { "Motor {n} · cortado · simulado", "Motor {n} · cut · simulated" } },
            { "simfault.none", new[] { "Sin fallas activas", "No active faults" } },
            { "simfault.ack.ok", new[] { "aceptado por el simulador", "accepted by the simulator" } },
            { "chip.status.tip", new[] { "Abrir el modo Telemetría", "Open Telemetry mode" } },
            { "chip.session.tip", new[] { "Detener la telemetría", "Stop telemetry" } },

            // source popover
            { "src.title", new[] { "Fuente de datos", "Data source" } },                                              // shell.source
            { "src.inspector", new[] { "Inspector (sin telemetría)", "Inspector (no telemetry)" } },                  // source.inspector (H-24)
            { "src.inspector.detail", new[] { "Sin enlace de vuelo: el encendido y la carga mueven las hélices y la simulación térmica didáctica. Los paneles de vuelo no aplican.",
                                              "No flight link: the power switch and the load drive the propellers and the didactic thermal simulation. Flight panels do not apply." } }, // H-20
            { "src.replay", new[] { "Repetición local", "Local replay" } },
            { "src.sim.soon", new[] { "Próximamente", "Coming soon" } },
            { "src.sim", new[] { "Simulador", "Simulator" } },
            { "src.sim.params", new[] { "Parámetros verosímiles, sin calibrar con el X500 real.", "Plausible parameters, not calibrated against the real X500." } },
            { "src.sitl", new[] { "PX4 SITL", "PX4 SITL" } },                                                         // tsw.sitl
            { "src.sitl.url", new[] { "URL del puente", "Bridge URL" } },
            { "src.sitl.note", new[] { "Necesita PX4 SITL y el puente bridge/px4_bridge.py en marcha en este equipo. Si no están, no habrá datos.",
                                       "It needs PX4 SITL and the bridge/px4_bridge.py bridge running on this machine. Without them there is no data." } },
            { "src.sitl.help", new[] { "No se detecta el puente. Arranca PX4 SITL y bridge/px4_bridge.py en este equipo.",
                                       "The bridge was not found. Start PX4 SITL and bridge/px4_bridge.py on this machine." } },                // tsw.sitlHelp
            { "src.sitl.connect", new[] { "Conectar", "Connect" } },
            { "src.sitl.badurl", new[] { "URL no válida: debe empezar por ws:// o wss://", "Invalid URL: it must start with ws:// or wss://" } },
            { "src.replay.missing", new[] { "No se pudo abrir la repetición local", "The local replay could not be opened" } },

            // alerts popover
            { "alerts.title", new[] { "Alertas", "Alerts" } },
            { "alerts.none", new[] { "Sin alertas. Cuando aparezca una, la pieza afectada pulsa en rojo.", "No alerts. When one appears, the affected part pulses red." } },
            { "alerts.off", new[] { "Activa la telemetría para ver alertas.", "Turn telemetry on to see alerts." } },
            { "alerts.click", new[] { "Clic en una alerta: selecciona y enfoca la pieza.", "Click an alert to select and focus its part." } },
            { "alerts.critical", new[] { "crítica", "critical" } },
            { "alerts.warning", new[] { "aviso", "warning" } },

            // faults popover (fault.*)
            { "faults.title", new[] { "Inyección de fallas", "Fault injection" } },                                   // fault.title
            { "faults.note", new[] { "La falla se superpone a la telemetría grabada durante 30 s y solo la ves tú. La trayectoria no reacciona: es una repetición. El lazo cerrado real es el modo PX4 SITL.",
                                     "The fault is overlaid on the recorded telemetry for 30 s and only you see it. The trajectory does not react: it is a replay. The real closed loop is the PX4 SITL mode." } }, // fault.note adapted: no other viewers
            { "faults.motor", new[] { "Motor", "Motor" } },                                                           // common.motor
            { "faults.motor_failure", new[] { "Cortar motor", "Cut motor" } },
            { "faults.motor_overheat", new[] { "Sobrecalentar motor", "Overheat motor" } },
            { "faults.battery_sag", new[] { "Caída de batería", "Battery sag" } },
            { "faults.vibration", new[] { "Vibración alta", "High vibration" } },
            { "faults.clear", new[] { "Limpiar", "Clear" } },
            { "faults.active", new[] { "Activas", "Active" } },
            { "faults.off", new[] { "Activa la repetición local para inyectar fallas sintéticas.", "Turn the local replay on to inject synthetic faults." } },
            { "faults.sitl", new[] { "En PX4 SITL no hay inyección sintética: las fallas se envían a PX4 con comandos (llegan al escritorio más adelante). En SITL, `motor_off` corta de verdad el motor: el dron simulado cae y hay que reiniciar el SITL.",
                                     "PX4 SITL has no synthetic injection: faults are sent to PX4 with commands (they reach the desktop later). In SITL, `motor_off` really cuts the motor: the simulated drone falls and the SITL must be restarted." } }, // user decision 2026-11-15 (replaces H-13/H-14/D-5)
            { "faults.sim", new[] { "El simulador no inyecta fallas: no es PX4 ni la repetición. Para fallas sintéticas usa la repetición local.",
                                    "The simulator does not inject faults: it is not PX4 nor the replay. Use the local replay for synthetic faults." } },
            { "faults.ack.ok",new[] { "aceptada", "accepted" } },
            { "faults.ack.okMotorOff", new[] { "aceptada por PX4; en SITL el motor se corta y el dron simulado cae (reinicia el SITL)",
                                               "accepted by PX4; in SITL the motor is cut and the simulated drone falls (restart the SITL)" } }, // cmd.ackOkMotorOff
            { "faults.ack.rejected", new[] { "rechazada", "rejected" } },               // cmd.ackRejected

            // flight tab (tel.*, motors.*, tag.*, prov.*)
            { "fl.empty.title", new[] { "Sin telemetría", "No telemetry" } },
            { "fl.empty", new[] { "El inspector muestra el dron en reposo. Enciende una fuente en el modo Telemetría (tecla 4) para ver las lecturas de vuelo, cada una con su origen.",
                                  "The inspector shows the drone at rest. Turn a source on in Telemetry mode (key 4) to see the flight readings, each with its origin." } },
            { "fl.waiting", new[] { "Esperando datos…", "Waiting for data…" } },                                      // telemetry.waiting
            { "fl.sec.state", new[] { "Estado", "State" } },
            { "fl.sec.pos", new[] { "Posición y velocidad", "Position and speed" } },
            { "fl.sec.att", new[] { "Actitud", "Attitude" } },
            { "fl.sec.bat", new[] { "Batería", "Battery" } },                                                         // tel.battery
            { "fl.sec.motors", new[] { "Motores", "Motors" } },                                                       // motors.title
            { "fl.sec.fc", new[] { "Controladora y vibración", "Controller and vibration" } },
            { "fl.mode", new[] { "Modo", "Mode" } },                                                                  // tel.mode
            { "fl.armed", new[] { "armado", "armed" } },
            { "fl.disarmed", new[] { "desarmado", "disarmed" } },
            { "fl.landed", new[] { "en tierra", "landed" } },
            { "fl.time", new[] { "Tiempo del registro", "Log time" } },
            { "fl.altitude", new[] { "Altura", "Altitude" } },
            { "fl.hspeed", new[] { "Vel. horiz.", "Horiz. speed" } },
            { "fl.vspeed", new[] { "Vel. vert.", "Vert. speed" } },
            { "fl.heading", new[] { "Rumbo", "Heading" } },
            { "fl.rollPitch", new[] { "Roll / Pitch", "Roll / Pitch" } },
            { "fl.level", new[] { "Carga", "Charge" } },
            { "fl.voltage", new[] { "Tensión", "Voltage" } },
            { "fl.current", new[] { "Corriente", "Current" } },
            { "fl.batteryTemp", new[] { "Temp. batería", "Battery temp." } },                                         // tel.batteryTemp
            { "fl.fcTemp", new[] { "Temp. controladora", "Controller temp." } },                                      // tel.fcTemp
            { "fl.vibration", new[] { "Vibración", "Vibration" } },                                                   // tel.vibration
            { "fl.rpm", new[] { "RPM", "RPM" } },
            { "fl.temp", new[] { "Temp.", "Temp." } },
            { "fl.front", new[] { "delantero", "front" } },                                                           // rotor.*
            { "fl.rear", new[] { "trasero", "rear" } },
            { "fl.right", new[] { "derecho", "right" } },
            { "fl.left", new[] { "izquierdo", "left" } },
            { "fl.starved", new[] { "Sin datos nuevos desde hace {s} s", "No new data for {s} s" } },                // tel.starved
            { "fl.source", new[] { "Fuente:", "Source:" } },                                                          // prov.source
            { "fl.propnote", new[] { "Las hélices giran en pantalla a escala 1:25 para que se vean a 60 fps.", "Propellers spin on screen at 1:25 scale so they read at 60 fps." } }, // motors.note n=25 (1/0.04)

            // analysis tab (E-4; charts.* of the web dictionaries: title "Análisis", hint "últimos 60 s", altitude, speed, voltage = "Batería", throttle = "Orden a motores", vibration)
            { "tab.analysis", new[] { "Análisis", "Analysis" } },
            { "an.hint", new[] { "últimos 60 s", "last 60 s" } },                                                      // charts.hint
            { "an.empty", new[] { "Las gráficas muestran el último minuto de la sesión: altura, velocidad, batería, orden a motores, RPM y vibración, cada una con su origen. Enciende una fuente en el modo Telemetría (tecla 4).",
                                  "The charts show the last minute of the session: altitude, speed, battery, motor command, RPM and vibration, each with its origin. Turn a source on in Telemetry mode (key 4)." } },
            { "an.collecting", new[] { "Recogiendo muestras…", "Collecting samples…" } },
            { "an.altitude", new[] { "Altura", "Altitude" } },                                                         // charts.altitude
            { "an.speed", new[] { "Velocidad", "Speed" } },                                                            // charts.speed
            { "an.hspeed", new[] { "horiz.", "horiz." } },
            { "an.vspeed", new[] { "vert.", "vert." } },
            { "an.voltage", new[] { "Batería · tensión", "Battery · voltage" } },                                       // charts.voltage "Batería"
            { "an.level", new[] { "Batería · carga", "Battery · charge" } },
            { "an.throttle", new[] { "Orden a motores (media)", "Motor command (mean)" } },                             // charts.throttle
            { "an.rpm", new[] { "RPM por motor", "RPM per motor" } },
            { "an.m1", new[] { "M1", "M1" } },
            { "an.m2", new[] { "M2", "M2" } },
            { "an.m3", new[] { "M3", "M3" } },
            { "an.m4", new[] { "M4", "M4" } },
            { "an.vibration", new[] { "Vibración", "Vibration" } },                                                    // charts.vibration
            { "an.markers", new[] { "Alertas y fallas", "Alerts and faults" } },
            { "an.markers.none", new[] { "sin alertas ni fallas en este minuto", "no alerts or faults in this minute" } },
            { "an.now", new[] { "ahora", "now" } },
            { "an.cursor", new[] { "hace {s} s", "{s} s ago" } },
            { "an.gap", new[] { "sin datos", "no data" } },
            { "an.alert.critical", new[] { "Alerta crítica", "Critical alert" } },
            { "an.alert.warning", new[] { "Aviso", "Warning" } },
            { "an.hover", new[] { "Pasa el cursor por las gráficas para leer cada valor.", "Hover the charts to read each value." } },

            // piloting (E-6): the Pilotar toggle, the four commands, the legend, the toasts (web: pilot.*, cmd.*, pad.* dictionaries)
            { "pilot.toggle", new[] { "Pilotar", "Pilot" } },                                                       // pilot.title
            { "pilot.toggle.tip", new[] { "Pilotar con teclado o mando. Esc suelta el pilotaje y deja el dron en mantener posición.", "Pilot with the keyboard or a pad. Esc releases piloting and leaves the drone holding its position." } },
            { "pilot.toggle.off.tip", new[] { "Disponible con el simulador de vuelo o con PX4 SITL conectado. La repetición es de solo lectura.", "Available with the flight simulator or a connected PX4 SITL. The replay is read-only." } },
            { "cmd.takeoff", new[] { "Despegar", "Take off" } },
            { "cmd.takeoff.tip", new[] { "Armar y despegar a 5 m (T)", "Arm and take off to 5 m (T)" } },       // cmd.takeoff
            { "cmd.land", new[] { "Aterrizar", "Land" } },
            { "cmd.land.tip", new[] { "Aterrizar aquí (L)", "Land here (L)" } },
            { "cmd.hold", new[] { "Mantener", "Hold" } },
            { "cmd.hold.tip", new[] { "Mantener la posición (H)", "Hold the position (H)" } },                  // cmd.hold
            { "cmd.rtl", new[] { "Volver", "Return" } },
            { "cmd.rtl.tip", new[] { "Volver al punto de despegue y aterrizar (R)", "Return to the take-off point and land (R)" } }, // cmd.rtl
            { "pilot.legend.title", new[] { "Pilotando", "Piloting" } },
            { "pilot.legend.move", new[] { "Mover", "Move" } },
            { "pilot.legend.yaw", new[] { "Girar", "Turn" } },
            { "pilot.legend.alt", new[] { "Subir / bajar", "Up / down" } },
            { "pilot.legend.release", new[] { "Soltar", "Release" } },
            { "pilot.legend.pad", new[] { "Mando", "Pad" } },                                                       // pad.title
            { "pilot.legend.pad.on", new[] { "conectado", "connected" } },                                           // pad.connected
            { "pilot.key.space", new[] { "Espacio", "Space" } },                                                    // pilot.space
            { "pilot.key.shift", new[] { "Mayús", "Shift" } },                                                      // pilot.shift
            { "toast.replay", new[] { "La repetición no acepta comandos: es de solo lectura. Usa el simulador de vuelo.", "The replay does not accept commands: it is read-only. Use the flight simulator." } },
            { "toast.nosession", new[] { "Enciende el simulador de vuelo (modo Telemetría, tecla 4) para dar órdenes.", "Turn on the flight simulator (Telemetry mode, key 4) to give orders." } },
            { "toast.nolink", new[] { "El enlace con PX4 SITL no está abierto: no hay a quién enviar la orden.", "The PX4 SITL link is not open: there is nothing to send the command to." } },
            { "toast.deadman", new[] { "Pilotaje suelto: el dron queda en mantener posición.", "Piloting released: the drone holds its position." } },
            { "toast.geofence.target", new[] { "Fuera de la geovalla: el destino se ajusta al límite de {r} m.", "Outside the geofence: the target is clamped to the {r} m limit." } },
            { "toast.geofence.radius", new[] { "Geovalla: el dron llegó al límite de {r} m de radio.", "Geofence: the drone reached the {r} m radius limit." } },
            { "toast.geofence.ceiling", new[] { "Geovalla: el dron llegó al techo de {h} m.", "Geofence: the drone reached the {h} m ceiling." } },
            { "toast.geofence.both", new[] { "Geovalla: el dron está en el límite de {r} m de radio y en el techo de {h} m.", "Geofence: the drone is at the {r} m radius limit and the {h} m ceiling." } },

            // fault and alert explanation card (PLAN_PC 27.3). The texts of the four faults are the APPROVED ones of PLAN_CONTROL_REAL_Y_ENTORNOS 2.4 (D1, 2026-11-13),
            // used as written; the ones marked "(translation)" had only the Spanish wording approved and the English one is a literal translation to be confirmed.
            // A "didactic explanation" is never a measurement; the figures of "what is happening" come from the displayed frame and carry their origin.
            { "fx.title", new[] { "Explicación didáctica", "Didactic explanation" } },
            { "fx.disclaimer", new[] { "Explicación general · no es una medición", "General explanation · not a measurement" } },
            { "fx.h.what", new[] { "Qué pasa", "What is happening" } },
            { "fx.h.means", new[] { "Qué significa", "What it means" } },
            { "fx.h.ctrl", new[] { "Qué haría el controlador", "What the controller would do" } },
            { "fx.h.pilot", new[] { "Qué haría el piloto", "What the pilot would do" } },
            { "fx.h.src", new[] { "En esta fuente", "In this source" } },
            { "fx.what.none", new[] { "Ninguna activa ahora.", "None active right now." } },
            { "fx.what.fault", new[] { "{name} · {s} s restantes de 30 s", "{name} · {s} s left of 30 s" } },
            { "fx.readings", new[] { "Lecturas afectadas", "Affected readings" } },
            { "fx.rd.rpm", new[] { "RPM", "RPM" } },
            { "fx.rd.motorcmd", new[] { "Orden al motor", "Motor command" } },
            { "fx.rd.motortemp", new[] { "Temp. motor", "Motor temp." } },
            { "fx.rd.batv", new[] { "Tensión y carga", "Voltage and charge" } },
            { "fx.rd.battemp", new[] { "Temp. batería", "Battery temp." } },
            { "fx.rd.fctemp", new[] { "Temp. controladora", "Controller temp." } },
            { "fx.rd.vib", new[] { "Vibración", "Vibration" } },
            { "fx.kind.motor_failure", new[] { "Motor cortado", "Motor cut" } },
            { "fx.kind.motor_overheat", new[] { "Motor caliente", "Hot motor" } },
            { "fx.kind.battery_sag", new[] { "Batería", "Battery" } },
            { "fx.kind.vibration", new[] { "Vibración", "Vibration" } },
            { "fx.state.motor_failure", new[] { "Motor {n} cortado", "Motor {n} cut off" } },
            { "fx.state.motor_overheat", new[] { "Motor {n} sobrecalentado", "Motor {n} overheated" } },
            { "fx.state.battery_sag", new[] { "Caída de batería", "Battery sag" } },
            { "fx.state.vibration", new[] { "Vibración alta", "High vibration" } },
            { "fx.pin.motor_failure", new[] { "Motor {n} · detenido", "Motor {n} · stopped" } },
            { "fx.pin.motor_overheat", new[] { "Motor {n} · sobrecalentado", "Motor {n} · overheated" } },
            { "fx.pin.battery_sag", new[] { "Batería · caída", "Battery · sag" } },
            { "fx.pin.vibration", new[] { "Controladora · vibración", "Controller · vibration" } },
            { "fx.pin.alert", new[] { "{part} · alerta crítica", "{part} · critical alert" } },
            // 28.1: the explanation is docked in the inspector and opens only on request (no banner, no toast)
            { "fx.open", new[] { "Explicación", "Explanation" } },
            { "fx.show", new[] { "Mostrar", "Show" } },
            { "fx.collapse", new[] { "Ocultar", "Hide" } },
            { "fx.dock.title", new[] { "Explicación de fallas", "Fault explanation" } },
            { "fx.dock.hint", new[] { "Se abre solo cuando tú lo pides. Elige una falla para leer qué significa.", "It opens only when you ask. Pick a fault to read what it means." } },
            { "fx.pick", new[] { "Qué explicar", "Explain" } },
            // A. motor cut (motor_failure, alert motorN:out)
            { "fx.a.means", new[] { "Un cuadricóptero necesita los cuatro rotores para controlar a la vez la inclinación y el giro. Sin uno pierde un cuarto del empuje y el giro queda desequilibrado.",
                                    "A quadcopter needs all four rotors to control tilt and heading at the same time. Without one it loses a quarter of its thrust and its yaw is left unbalanced." } },
            { "fx.a.ctrl", new[] { "Un controlador estándar, como el de PX4, no está hecho para volar con tres motores: lo habitual es que el dron gire sobre sí mismo y caiga. Hay controladores de investigación que sacrifican el giro para bajar con control; no forman parte de PX4 estándar.",
                                   "A standard controller such as PX4's is not built to fly on three motors: the drone usually spins and falls. Research controllers can give up heading to descend under control; they are not part of standard PX4." } },
            { "fx.a.src.replay", new[] { "Falla sintética: solo cambian las cifras del motor {n} durante 30 s. La trayectoria y la actitud son las del vuelo grabado y no reaccionan; los otros motores tampoco compensan.",
                                         "Synthetic fault: only motor {n}'s figures change for 30 s. Trajectory and attitude are the recorded flight's and do not react; the other motors do not compensate either." } },
            { "fx.a.src.sitl", new[] { "Orden enviada a PX4 (MAV_CMD_INJECT_FAILURE). En el SITL con la configuración del puente, apagar un motor corta de verdad su empuje: el dron simulado se voltea y cae en menos de un segundo, PX4 no lo compensa y hay que reiniciar el SITL después. Es lo que ocurre en la simulación, no una afirmación sobre un dron físico. Las RPM que ves son la orden de PX4 (Gazebo no mide la velocidad del rotor).",
                                       "Command sent to PX4 (MAV_CMD_INJECT_FAILURE). In SITL with the bridge's configuration, turning a motor off really cuts its thrust: the simulated drone rolls over and falls in under a second, PX4 does not compensate, and the SITL must be restarted afterwards. This is what happens in the simulation, not a claim about a physical drone. The RPM shown are PX4's command (Gazebo does not measure rotor speed)." } },
            // B. battery sag / low charge (battery_sag, alert battery:rem)
            { "fx.bat.means", new[] { "Bajo carga, la resistencia interna de la batería hace caer la tensión; si la carga baja del 15 %, queda poco margen para volver.",
                                    "Under load the battery's internal resistance makes its voltage drop; below 15 % charge there is little margin left to return." } },
            { "fx.bat.ctrl", new[] { "PX4 avisa al 15 % y al 7 % (sus valores por defecto) y puede ordenar volver a casa o aterrizar según su configuración.",
                                   "PX4 warns at 15 % and 7 % (its defaults) and can command return or land depending on its settings." } },
            { "fx.bat.src.replay", new[] { "Falla sintética: −2,4 V y −30 % sobre la cifra grabada. El vuelo grabado no vuelve ni aterriza por ello.",
                                         "Synthetic fault: −2.4 V and −30 % on the recorded figure. The recorded flight does not return or land because of it." } },
            { "fx.bat.src.sim", new[] { "El simulador descarga la batería, pero no reacciona a la batería baja: sigue volando hasta que aterrices.",
                                      "The simulator drains the battery but does not react to low charge: it keeps flying until you land." } },
            { "fx.bat.src.sitl", new[] { "Batería simulada por PX4. Su reacción depende de sus parámetros; en SITL puede no llegar a descargarse lo suficiente para que salte el aviso.",
                                       "Battery simulated by PX4. Its reaction depends on its parameters; in SITL it may not drain enough for the warning to trigger." } },      // (translation)
            // C. hot motor (motor_overheat, alert motorN:temp)
            { "fx.c.means", new[] { "Un motor muy caliente pierde par y daña su bobinado; los umbrales que usa la app (65 / 80 °C) son de demostración, no del fabricante.",
                                    "A very hot motor loses torque and damages its windings; the app's thresholds (65 / 80 °C) are demo values, not the manufacturer's." } },
            { "fx.c.ctrl", new[] { "Reducir la carga y aterrizar; PX4 no mide la temperatura del motor por defecto.",
                                   "Reduce load and land; PX4 does not measure motor temperature by default." } },
            { "fx.c.src.replay", new[] { "Falla sintética: +75 °C sobre la temperatura estimada (la temperatura del motor nunca es medida en esta app).",
                                         "Synthetic fault: +75 °C on the estimated temperature (motor temperature is never measured in this app)." } },
            { "fx.c.src.sitl", new[] { "Temperatura estimada por un modelo térmico del puente a partir de la orden de los motores.",
                                       "Temperature estimated by a thermal model of the bridge from the motor command." } },                                                    // (translation)
            // D. high vibration (vibration, alert fc:vib)
            { "fx.d.means", new[] { "La vibración llega al acelerómetro de la controladora y empeora la estimación de altura y posición.",
                                    "Vibration reaches the flight controller's accelerometer and degrades the height and position estimate." } },
            { "fx.d.ctrl", new[] { "Aterrizar y revisar hélices, tornillería y el montaje antivibración.",
                                   "Land and check the propellers, fasteners and the vibration mount." } },
            { "fx.d.src.replay", new[] { "Falla sintética: +28 m/s² sobre la métrica grabada. La estimación del vuelo grabado no cambia.",
                                         "Synthetic fault: +28 m/s² on the recorded metric. The recorded flight's estimate does not change." } },                               // (translation)
            { "fx.d.src.sitl", new[] { "Vibración que informa PX4 (en simulación suele ser casi nula).",
                                       "Vibration reported by PX4 (in simulation it is usually almost zero)." } },                                                              // (translation)
            // any fault in the flight simulator (approved as faults.sim in D-5)
            { "fx.src.sim", new[] { "El simulador no inyecta fallas: no es PX4 ni la repetición.", "The simulator does not inject faults: it is not PX4 nor the replay." } },
            // «En esta fuente · simulador» for the motor failure: the approved line simfault.note (the simulator now cuts a motor in a simplified model)
            { "fx.a.src.sim", new[] { "Simulador: el motor deja de empujar en un modelo simplificado; no es PX4 ni un dron real.",
                                      "Simulator: the motor stops pushing in a simplified model; this is not PX4 or a real drone." } },
            // alerts with NO approved explanation (controller temperature, battery temperature): a neutral factual line, to be approved
            { "fx.n.means", new[] { "Alerta por temperatura alta: la lectura supera el umbral de aviso de la app (valores de demostración, no del fabricante).",
                                    "High temperature alert: the reading is above the app's warning threshold (demo values, not the manufacturer's)." } },
            { "fx.n.ctrl", new[] { "En esta app solo se muestra la lectura; no se envía ninguna orden al dron.", "In this app the reading is only shown; no command is sent to the drone." } },
            { "fx.n.src.replay", new[] { "Cifras de la repetición grabada; una falla sintética solo altera la cifra, durante 30 s.", "Figures of the recorded replay; a synthetic fault only alters the figure, for 30 s." } },
            { "fx.n.src.sitl", new[] { "Lectura que informa PX4 SITL en simulación.", "Reading reported by PX4 SITL in simulation." } },

            // game pad modes (PLAN_PC 27.2): DRON (default) flies the drone and the UI gets nothing; MENÚS (Start) navigates the menus and the drone gets nothing
            { "pad.badge.drone", new[] { "Mando: Dron", "Pad: Drone" } },
            { "pad.badge.menus", new[] { "Mando: Menús", "Pad: Menus" } },
            { "pad.badge.hint", new[] { "Start: cambiar", "Start: switch" } },
            { "pad.badge.tip", new[] { "Start alterna entre pilotar el dron y navegar los menús. Solo mandos XInput (Xbox).", "Start switches between flying the drone and navigating the menus. XInput (Xbox) pads only." } },
            { "pad.toast.connected", new[] { "Mando conectado en modo Dron: no mueve los menús. Start cambia a Menús.", "Pad connected in Drone mode: it does not move the menus. Start switches to Menus." } },
            { "pad.toast.disconnected", new[] { "Mando desconectado.", "Pad disconnected." } },
            { "pad.toast.drone", new[] { "Mando en modo Dron: pilota el dron y no mueve los menús. Start vuelve a los menús.", "Pad in Drone mode: it flies the drone and does not move the menus. Start goes back to the menus." } },
            { "pad.toast.menus", new[] { "Mando en modo Menús: navega los menús y el dron no recibe órdenes del mando. Start vuelve al dron.", "Pad in Menus mode: it navigates the menus and the drone gets no pad commands. Start goes back to the drone." } },
            { "pad.toast.menus.hold", new[] { " Se envió «mantener posición».", " «Hold position» was sent." } },
            { "pad.hint.pilot", new[] { "Activa «Pilotar» para volar con el mando.", "Turn on «Pilot» to fly with the pad." } },
            { "pad.leg.drone", new[] { "Dron", "Drone" } },
            { "pad.leg.menus", new[] { "Menús", "Menus" } },
            { "pad.leg.menus.note", new[] { "el dron no recibe órdenes del mando", "the drone gets no pad commands" } },
            { "pad.leg.sticks", new[] { "Stick izq.: subir y girar · Stick der.: mover", "Left stick: climb and turn · Right stick: move" } },
            { "pad.leg.abxy", new[] { "A despegar · B aterrizar · X mantener · Y volver", "A take off · B land · X hold · Y return" } },
            { "pad.leg.start", new[] { "Start: menús", "Start: menus" } },

            // tutorial step 7 "Telemetría" (E-7; content of PROPUESTA_TEXTOS_HONESTIDAD §3 adapted to the desktop UI; honesty labels inside the
            // hints repeat labels.json titles verbatim and a test checks that they do; D-7: two FIXED readings with different origin)
            { "tut.tel.title", new[] { "Telemetría", "Telemetry" } },
            { "tut.tel.desc", new[] { "Datos de vuelo sobre el modelo, siempre con su origen.", "Flight data on the model, always with its origin." } },
            { "tut.tel.mode", new[] { "Modo Telemetría", "Telemetry mode" } },
            { "tut.tel.mode.key", new[] { "Tecla 4", "Key 4" } },
            { "tut.tel.mode.strip", new[] { "Tira: Encendido, Fuente, Alertas, Fallas", "Strip: Power, Source, Alerts, Faults" } },
            { "tut.tel.src", new[] { "Fuente", "Source" } },
            { "tut.tel.key.l", new[] { "L", "L" } },
            { "tut.tel.src.replay", new[] { "Sombra digital: vuelo real repetido aquí", "Digital shadow: a real flight replayed here" } },
            { "tut.tel.src.sim", new[] { "Simulador de vuelo: no es PX4 ni un dron real", "Flight simulator: not PX4 and not a real drone" } },
            { "tut.tel.src.sitl", new[] { "Gemelo digital SITL: PX4 simulado", "SITL digital twin: simulated PX4" } },
            { "tut.tel.flight", new[] { "Pestaña Vuelo", "Flight tab" } },
            { "tut.tel.flight.origin", new[] { "Origen: medido, estimado o simulado", "Origin: measured, estimated or simulated" } },
            { "tut.tel.key.shifta", new[] { "Shift+A", "Shift+A" } },
            { "tut.tel.flight.faults", new[] { "Fallas de la repetición: sintéticas", "Replay faults: synthetic" } },
            { "tut.tel.pilot", new[] { "Pilotar", "Pilot" } },
            { "tut.tel.pilot.when", new[] { "Simulador o PX4 SITL; repetición: solo lectura", "Simulator or PX4 SITL; replay: read-only" } },
            { "tut.tel.pilot.keys", new[] { "W A S D · Q E · Espacio / Mayús", "W A S D · Q E · Space / Shift" } },
            { "tut.tel.pilot.cmds", new[] { "T L H R: despegar, aterrizar, mantener, volver", "T L H R: take off, land, hold, return" } },
            { "tut.tel.pilot.esc", new[] { "Esc: soltar el pilotaje", "Esc: release piloting" } },
            // touch variant (finger glyphs, no keyboard)
            { "tut.tel.t.mode", new[] { "Toca Telemetría en el conmutador de modos", "Tap Telemetry in the mode switch" } },
            { "tut.tel.t.power", new[] { "Toca Encendido en la tira", "Tap Power in the strip" } },
            { "tut.tel.t.src", new[] { "Toca Fuente", "Tap Source" } },
            { "tut.tel.t.flight", new[] { "Toca la pestaña Vuelo", "Tap the Flight tab" } },
            { "tut.tel.t.alerts", new[] { "Toca Alertas", "Tap Alerts" } },
            { "tut.tel.t.pilot", new[] { "Toca Pilotar", "Tap Pilot" } },
            { "tut.tel.t.cmds", new[] { "Toca Despegar, Aterrizar, Mantener o Volver", "Tap Take off, Land, Hold or Return" } },
            // illustration captions (pc / touch)
            { "tut.tel.cap0", new[] { "4: abre el modo Telemetría", "4: opens Telemetry mode" } },
            { "tut.tel.cap1", new[] { "L: elige la fuente", "L: pick the source" } },
            { "tut.tel.cap2", new[] { "Cada lectura dice su origen", "Each reading states its origin" } },
            { "tut.tel.cap3", new[] { "Pilotar: Esc suelta", "Pilot: Esc releases" } },
            { "tut.tel.cap0.t", new[] { "Toca Telemetría", "Tap Telemetry" } },
            { "tut.tel.cap1.t", new[] { "Toca Fuente", "Tap Source" } },
            { "tut.tel.cap2.t", new[] { "Cada lectura dice su origen", "Each reading states its origin" } },
            { "tut.tel.cap3.t", new[] { "Toca Pilotar", "Tap Pilot" } },
            // drawn labels of the illustration
            { "tut.tel.ill.alt", new[] { "Altura", "Altitude" } },
            { "tut.tel.ill.motor", new[] { "Temp. motores", "Motor temp." } },

            // status bar
            { "st.link", new[] { "Enlace", "Link" } },

            // Lote 1b: top-down map (tool 1), pre-flight checklist (tool 3) and battery health (tool 8). Copied by script from the web dictionaries
            // (src/web/i18n/es.ts and en.ts, commit a8664fa): same keys, same wording. The honest ones are marked [APROBADO 2026-11-13] there.
            { "map.title", new[] { "Mapa cenital", "Top-down map" } },
            { "map.expand", new[] { "Toca para ampliar", "Tap to enlarge" } },
            { "map.fold", new[] { "Plegar el mapa", "Fold the map" } },
            { "map.unfold", new[] { "Mostrar el mapa", "Show the map" } },
            { "map.close", new[] { "Cerrar el mapa ampliado", "Close the enlarged map" } },
            { "map.controls", new[] { "Controles del mapa", "Map controls" } },
            { "map.orient", new[] { "Orientación", "Orientation" } },
            { "map.northUp", new[] { "Norte arriba", "North up" } },
            { "map.headingUp", new[] { "Rumbo arriba", "Heading up" } },
            { "map.fit", new[] { "Encajar", "Fit" } },
            { "map.follow", new[] { "Seguir", "Follow" } },
            { "map.north", new[] { "N", "N" } },
            { "map.originLocal", new[] { "Origen local (EKF)", "Local origin (EKF)" } },
            { "map.originTakeoff", new[] { "Punto de despegue", "Takeoff point" } },
            { "map.posMeasured", new[] { "Posición: medido", "Position: measured" } },
            { "map.posSimulated", new[] { "Posición: simulado", "Position: simulated" } },
            { "map.readout", new[] { "A {dist} m del origen · rumbo {hdg}°", "{dist} m from the origin · heading {hdg}°" } },
            { "map.honest", new[] { "Sin mapa de fondo ni coordenadas geográficas: el enlace no las trae.", "No base map and no geographic coordinates: the link does not carry them." } },
            { "map.noOrders", new[] { "Tocar el mapa no envía órdenes.", "Touching the map sends no commands." } },
            { "map.trailFull", new[] { "El rastro guarda los últimos {n} puntos.", "The trail keeps the last {n} points." } },
            { "battery.title", new[] { "Batería", "Battery" } },
            { "battery.hint", new[] { "salud estimada", "estimated health" } },
            { "battery.v", new[] { "Tensión", "Voltage" } },
            { "battery.i", new[] { "Corriente", "Current" } },
            { "battery.rem", new[] { "Carga restante", "Charge left" } },
            { "battery.r", new[] { "Resistencia interna", "Internal resistance" } },
            { "battery.drop", new[] { "Caída bajo carga", "Sag under load" } },
            { "battery.rest", new[] { "Tensión en reposo", "Rest voltage" } },
            { "battery.time", new[] { "Tiempo restante", "Time left" } },
            { "battery.timeI", new[] { "Por corriente", "By current" } },
            { "battery.perCell", new[] { "por celda", "per cell" } },
            { "battery.calculating", new[] { "calculando… ({n}/{min})", "calculating… ({n}/{min})" } },
            { "battery.timeSlope", new[] { "por la pendiente de la carga", "from the slope of the charge" } },
            { "battery.timeNone", new[] { "solo en vuelo y con la carga bajando", "only in flight and with the charge falling" } },
            { "battery.timeISub", new[] { "con {ah} Ah, solo en el simulador", "with {ah} Ah, simulator only" } },
            { "battery.simData", new[] { "Estimado sobre datos simulados.", "Estimated over simulated data." } },
            { "battery.note", new[] { "La resistencia se estima con los saltos de corriente del vuelo; no es una medida. El tiempo llega hasta la reserva del 15 %.", "The resistance is estimated from the current steps of the flight; it is not a measurement. The time runs to the 15 % reserve." } },
            { "battery.viTitle", new[] { "Tensión frente a corriente", "Voltage against current" } },
            { "battery.viHint", new[] { "último minuto", "last minute" } },
            { "battery.viAria", new[] { "Tensión frente a corriente del último minuto; ahora {v} V y {i} A", "Voltage against current over the last minute; now {v} V and {i} A" } },
            { "battery.viFit", new[] { "Recta V₀ − R̂·I con R̂ = {r} mΩ (estimada).", "Line V₀ − R̂·I with R̂ = {r} mΩ (estimated)." } },
            { "battery.viNoFit", new[] { "Recta: calculando la resistencia…", "Line: calculating the resistance…" } },
            { "pf.title", new[] { "Comprobaciones previas", "Pre-flight checks" } },
            { "pf.ready", new[] { "Listo", "Ready" } },
            { "pf.warn", new[] { "Con avisos", "With warnings" } },
            { "pf.fail", new[] { "No apto", "Not fit" } },
            { "pf.counts", new[] { "Bien {pass} · Avisos {warn} · Fallos {fail}", "Ok {pass} · Warnings {warn} · Failures {fail}" } },
            { "pf.replayNote", new[] { "Evaluación del vuelo grabado en este instante", "Evaluation of the recorded flight at this instant" } },
            { "pf.ownNote", new[] { "Evaluación de la sesión grabada en este instante", "Evaluation of the recorded session at this instant" } },
            { "pf.info", new[] { "Es informativa: no bloquea el despegue. Los valores llevan su origen; lo que marcas tú no es una medida.", "It is informative: it does not block the take-off. Values carry their origin; what you tick is not a measurement." } },
            { "pf.manual", new[] { "Lo marcas tú", "You tick these" } },
            { "pf.status.pass", new[] { "Bien", "Ok" } },
            { "pf.status.warn", new[] { "Aviso", "Warning" } },
            { "pf.status.fail", new[] { "Fallo", "Failure" } },
            { "pf.status.na", new[] { "Sin dato", "No data" } },
            { "pf.item.link", new[] { "Enlace", "Link" } },
            { "pf.item.bat.rem", new[] { "Carga de la batería", "Battery charge" } },
            { "pf.item.bat.cell", new[] { "Tensión por celda", "Voltage per cell" } },
            { "pf.item.temp", new[] { "Temperaturas", "Temperatures" } },
            { "pf.item.vib", new[] { "Vibración", "Vibration" } },
            { "pf.item.armed", new[] { "Estado", "State" } },
            { "pf.item.fence", new[] { "Dentro de la geocerca", "Inside the geofence" } },
            { "pf.item.faults", new[] { "Fallas y alertas", "Faults and alerts" } },
            { "pf.item.gps", new[] { "Fijación GPS", "GPS fix" } },
            { "pf.item.manual.props", new[] { "Hélices apretadas", "Propellers tight" } },
            { "pf.item.manual.radius", new[] { "Radio de seguridad respetado", "Safety radius kept" } },
            { "pf.item.manual.clear", new[] { "Zona despejada", "Area clear" } },
            { "pf.origin.synthetic", new[] { "sintética", "synthetic" } },
            { "pf.origin.command", new[] { "comando", "command" } },
            { "pf.origin.app", new[] { "app", "app" } },
            { "pf.origin.user", new[] { "lo marca el usuario", "ticked by the user" } },
            { "pf.origin.none", new[] { "sin dato", "no data" } },
            { "pf.link.open", new[] { "abierto", "open" } },
            { "pf.link.connecting", new[] { "conectando", "connecting" } },
            { "pf.link.closed", new[] { "cerrado", "closed" } },
            { "pf.link.late", new[] { "datos atrasados", "late data" } },
            { "pf.link.slow", new[] { "llegan pocos datos", "little data arriving" } },
            { "pf.link.measuring", new[] { "midiendo la tasa", "measuring the rate" } },
            { "pf.temp.motor", new[] { "motor {n}", "motor {n}" } },
            { "pf.temp.battery", new[] { "batería", "battery" } },
            { "pf.armed.disarmed", new[] { "Desarmado en tierra", "Disarmed on the ground" } },
            { "pf.armed.armedGround", new[] { "Armado en tierra", "Armed on the ground" } },
            { "pf.armed.disarmedAir", new[] { "Desarmado fuera de tierra", "Disarmed off the ground" } },
            { "pf.armed.flying", new[] { "En vuelo: no es prevuelo", "Flying: not a pre-flight" } },
            { "pf.fence.simOnly", new[] { "Solo existe en el Simulador", "Only the Simulator has one" } },
            { "pf.fence.noFrame", new[] { "Sin posición", "No position" } },
            { "pf.fence.outside", new[] { "Fuera por {m} m", "Outside by {m} m" } },
            { "pf.fence.margin", new[] { "{m} m de margen", "{m} m of margin" } },
            { "pf.faults.none", new[] { "Ninguna", "None" } },
            { "pf.faults.warning", new[] { "Alerta de aviso", "Warning alert" } },
            { "pf.faults.critical", new[] { "Alerta crítica", "Critical alert" } },
            { "pf.faults.faults", new[] { "Falla activa", "Active fault" } },
            { "pf.faults.count", new[] { "activas: {n}", "active: {n}" } },
            { "pf.gps.na", new[] { "El cable no trae GPS", "The cable carries no GPS" } },
            { "pf.takeoff.title", new[] { "Hay comprobaciones en fallo", "Some checks are failing" } },
            { "pf.takeoff.body", new[] { "Fallan: {list}. Las comprobaciones no bloquean el despegue, pero conviene mirarlas antes.", "Failing: {list}. The checks do not block the take-off, but you should look at them first." } },
            { "pf.takeoff.go", new[] { "Despegar igualmente", "Take off anyway" } },
            { "pf.takeoff.cancel", new[] { "Cancelar", "Cancel" } },
            { "tools.map", new[] { "Mapa", "Map" } },
            { "tools.needTelemetry", new[] { "Sin telemetría: activa una fuente para usar las herramientas.", "No telemetry: turn a source on to use the tools." } },
            { "tools.preflight", new[] { "Comprobar", "Check" } },
            { "unit.vPerCell", new[] { "V/celda", "V/cell" } },
            { "sc.tools.map", new[] { "Mapa", "Map" } },
            { "sc.tools.preflight", new[] { "Comprobaciones previas", "Pre-flight checks" } },
        };

        // PX4 nav_state names (labels.json navState carries the raw PX4 names, with no translation): the desktop shows them in the language of the
        // interface and keeps the raw PX4 name as the tooltip. Ids as in src/shared/protocol.ts NAV_STATE_LABEL; an unknown id falls back to the raw label.
        static readonly Dictionary<int, string[]> NavStates = new Dictionary<int, string[]>
        {
            { 0, new[] { "Manual", "Manual" } },
            { 1, new[] { "Altitud", "Altitude" } },
            { 2, new[] { "Posición", "Position" } },
            { 3, new[] { "Misión", "Mission" } },
            { 4, new[] { "Mantener posición", "Hold" } },
            { 5, new[] { "Regreso a casa", "Return" } },
            { 10, new[] { "Acro", "Acro" } },
            { 12, new[] { "Descenso", "Descend" } },
            { 13, new[] { "Terminación de vuelo", "Termination" } },
            { 14, new[] { "Offboard (control externo)", "Offboard" } },
            { 15, new[] { "Estabilizado", "Stabilized" } },
            { 17, new[] { "Despegue", "Takeoff" } },
            { 18, new[] { "Aterrizaje", "Land" } },
            { 19, new[] { "Seguir", "Follow" } },
            { 20, new[] { "Aterrizaje de precisión", "Precision land" } },
            { 21, new[] { "Órbita", "Orbit" } },
        };

        /// <summary>Ids that have a translation (for the completeness test against labels.json navState).</summary>
        public static IEnumerable<int> NavStateIds => NavStates.Keys;

        /// <summary>
        /// The flight mode name in the language of the interface. `raw` is the PX4 name of labels.json (HonestyLabels.NavState), which is the
        /// fallback for an id without translation and what the UI shows as the tooltip.
        /// </summary>
        public static string NavStateText(int nav, string raw, string lang)
        {
            return NavStates.TryGetValue(nav, out var v) ? (lang == "en" ? v[1] : v[0]) : raw;
        }

        // Details the simulator (and the replay) send in the ack, Spanish like the bridge ones; the desktop shows them in the interface language.
        static readonly Dictionary<string, string[]> AckDetails = new Dictionary<string, string[]>
        {
            { "el dron está en tierra: despega primero", new[] { "el dron está en tierra: despega primero", "the drone is on the ground: take off first" } },
            { "ya está en vuelo", new[] { "ya está en vuelo", "it is already flying" } },
            { "punto no válido", new[] { "punto no válido", "invalid point" } },
            { "no disponible en el simulador del navegador", new[] { "no disponible en el simulador (no es PX4: sin inyección de fallas)", "not available in the simulator (it is not PX4: no fault injection)" } },
            { "demasiado rápido, espera un segundo", new[] { "demasiado rápido, espera un segundo", "too fast, wait a second" } },
        };

        /// <summary>An ack detail in the language of the interface (unknown details are shown as the source wrote them, with the web wording adapted to the desktop).</summary>
        public static string AckDetail(string detail, string lang)
        {
            if (string.IsNullOrEmpty(detail)) return detail;
            if (AckDetails.TryGetValue(detail, out var v)) return lang == "en" ? v[1] : v[0];
            return ForDesktop(detail, lang);
        }

        public static string T(string key, string lang)
        {
            if (!Table.TryGetValue(key, out var v)) throw new KeyNotFoundException("TelemetryStrings: missing key " + key);
            return lang == "en" ? v[1] : v[0];
        }

        /// <summary>T with {name} placeholders replaced (name, value pairs).</summary>
        public static string F(string key, string lang, params object[] pairs)
        {
            string s = T(key, lang);
            for (int i = 0; i + 1 < pairs.Length; i += 2) s = s.Replace("{" + pairs[i] + "}", Convert.ToString(pairs[i + 1], System.Globalization.CultureInfo.InvariantCulture));
            return s;
        }

        /// <summary>All keys and both languages (for the EditMode completeness and honesty tests).</summary>
        public static IEnumerable<KeyValuePair<string, string[]>> All => Table;

        /// <summary>
        /// Detail text of the local replay for the DESKTOP: the contract text (labels.json classification.replay.localDetail) says the flight
        /// is replayed "en tu navegador"/"in your browser"; in the .exe it is "en esta aplicación"/"in this app". Only that phrase changes.
        /// </summary>
        public static string LocalReplayDetail(HonestyLabels labels, string lang)
        {
            return labels.TryLocalReplayDetail(lang, out string d) ? ForDesktop(d, lang) : string.Empty;
        }

        /// <summary>
        /// The contract texts are written for the web ("tu navegador", "the browser sends"); in the .exe the same sentences say "esta
        /// aplicación" / "this app". Only those phrases change; nothing else of the approved text is touched.
        /// </summary>
        public static string ForDesktop(string text, string lang)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (lang == "en") return text.Replace("in your browser", "in this app").Replace("the browser sends", "the app sends");
            return text.Replace("en tu navegador", "en esta aplicación").Replace("el navegador envía", "la aplicación envía");
        }
    }
}
