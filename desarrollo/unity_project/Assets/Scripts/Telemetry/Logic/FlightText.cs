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
            { "chip.status.tip", new[] { "Abrir el modo Telemetría", "Open Telemetry mode" } },
            { "chip.session.tip", new[] { "Detener la telemetría", "Stop telemetry" } },

            // source popover
            { "src.title", new[] { "Fuente de datos", "Data source" } },                                              // shell.source
            { "src.inspector", new[] { "Inspector (sin telemetría)", "Inspector (no telemetry)" } },                  // source.inspector (H-24)
            { "src.inspector.detail", new[] { "Sin enlace de vuelo: el encendido y la carga mueven las hélices y la simulación térmica didáctica. Los paneles de vuelo no aplican.",
                                              "No flight link: the power switch and the load drive the propellers and the didactic thermal simulation. Flight panels do not apply." } }, // H-20
            { "src.replay", new[] { "Repetición local", "Local replay" } },
            { "src.sim.soon", new[] { "Próximamente", "Coming soon" } },
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
            { "faults.sitl", new[] { "En PX4 SITL no hay inyección sintética: las fallas se envían a PX4 con comandos (llegan al escritorio más adelante). PX4 acepta motor_off, pero el simulador Gazebo puede no aplicarlo.",
                                     "PX4 SITL has no synthetic injection: faults are sent to PX4 with commands (they reach the desktop later). PX4 accepts motor_off, but the Gazebo simulator may not apply it." } }, // H-13/H-14 (approved D-5)
            { "faults.ack.ok", new[] { "aceptada", "accepted" } },
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

            // status bar
            { "st.link", new[] { "Enlace", "Link" } },
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
