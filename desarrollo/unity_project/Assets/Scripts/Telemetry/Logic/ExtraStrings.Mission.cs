// GENERATED from the web dictionaries (twinsight-web-twin src/web/i18n/es.ts and en.ts): the texts of the mission tool, verbatim. Do not edit by hand.
using System.Collections.Generic;

namespace WebGL.Telemetry
{
    public static partial class ExtraStrings
    {
        static partial void AddMission(Dictionary<string, string[]> d)
        {
            d["tools.mission"] = new[] { "Misión", "Mission" };
            d["tools.missionReplay"] = new[] { "La repetición no acepta comandos", "The replay does not accept commands" };
            d["mission.title"] = new[] { "Misión", "Mission" };
            d["mission.honest"] = new[] { "Misión de la app (no es una misión PX4)", "App mission (not a PX4 mission)" };
            d["mission.originSim"] = new[] { "simulado", "simulated" };
            d["mission.originSitl"] = new[] { "Gemelo digital SITL · órdenes fly_to de la app", "SITL digital twin · fly_to commands from the app" };
            d["mission.speedNote"] = new[] { "Velocidad aproximada (el destino avanza a la velocidad pedida).", "Approximate speed (the destination moves at the requested speed)." };
            d["mission.add"] = new[] { "Añadir en el suelo", "Add on the ground" };
            d["mission.adding"] = new[] { "Toca el suelo 3D para añadir · pulsa para terminar", "Tap the 3D ground to add · press to finish" };
            d["mission.empty"] = new[] { "Sin waypoints: pulsa «Añadir en el suelo» y toca el suelo 3D.", "No waypoints: press \"Add on the ground\" and tap the 3D ground." };
            d["mission.alt"] = new[] { "Altura (m)", "Altitude (m)" };
            d["mission.speed"] = new[] { "Velocidad (m/s)", "Speed (m/s)" };
            d["mission.hold"] = new[] { "Espera (s)", "Wait (s)" };
            d["mission.delete"] = new[] { "Borrar el waypoint {n}", "Delete waypoint {n}" };
            d["mission.finish"] = new[] { "Al terminar", "When done" };
            d["mission.finishHold"] = new[] { "Mantener", "Hold" };
            d["mission.finishLand"] = new[] { "Aterrizar", "Land" };
            d["mission.finishRtl"] = new[] { "Volver (RTL)", "Return (RTL)" };
            d["mission.run"] = new[] { "Ejecutar", "Run" };
            d["mission.pause"] = new[] { "Pausar", "Pause" };
            d["mission.resume"] = new[] { "Reanudar", "Resume" };
            d["mission.abort"] = new[] { "Abortar", "Abort" };
            d["mission.rtl"] = new[] { "Volver (RTL)", "Return (RTL)" };
            d["mission.progress"] = new[] { "Tramo {leg}/{legs} · {m} m · {s} s", "Leg {leg}/{legs} · {m} m · {s} s" };
            d["mission.phaseIdle"] = new[] { "Lista para ejecutar", "Ready to run" };
            d["mission.phaseTakeoff"] = new[] { "Despegando", "Taking off" };
            d["mission.phaseLeg"] = new[] { "En vuelo", "Flying" };
            d["mission.phaseHold"] = new[] { "Esperando en el waypoint", "Waiting at the waypoint" };
            d["mission.phaseDone"] = new[] { "Misión terminada", "Mission complete" };
            d["mission.phasePaused"] = new[] { "En pausa: {reason}", "Paused: {reason}" };
            d["mission.phaseFailed"] = new[] { "Fallida: {reason}", "Failed: {reason}" };
            d["mission.export"] = new[] { "Exportar JSON", "Export JSON" };
            d["mission.import"] = new[] { "Importar JSON", "Import JSON" };
            d["mission.clear"] = new[] { "Vaciar", "Clear" };
            d["mission.importBad"] = new[] { "El archivo no es una misión válida.", "The file is not a valid mission." };
            d["mission.needLink"] = new[] { "Sin enlace: conecta la fuente para ejecutar.", "No link: connect the source to run." };
            d["mission.editLocked"] = new[] { "La misión no se edita mientras vuela: pausa o aborta.", "The mission cannot be edited while it flies: pause or abort." };
            d["mission.pathAria"] = new[] { "Ruta de la misión con {n} waypoints", "Mission route with {n} waypoints" };
            d["mission.err.count"] = new[] { "La misión necesita de 1 a 50 waypoints.", "The mission needs 1 to 50 waypoints." };
            d["mission.err.not_finite"] = new[] { "Waypoint {n}: valores no válidos.", "Waypoint {n}: invalid values." };
            d["mission.err.radius"] = new[] { "Waypoint {n}: fuera del límite de {max} m del origen.", "Waypoint {n}: beyond the {max} m limit from the origin." };
            d["mission.err.altitude"] = new[] { "Waypoint {n}: la altura debe estar entre {min} y {max} m.", "Waypoint {n}: the altitude must be between {min} and {max} m." };
            d["mission.err.speed"] = new[] { "Waypoint {n}: la velocidad debe estar entre 0,5 y 6 m/s.", "Waypoint {n}: the speed must be between 0.5 and 6 m/s." };
            d["mission.err.hold"] = new[] { "Waypoint {n}: la espera debe estar entre 0 y 120 s.", "Waypoint {n}: the wait must be between 0 and 120 s." };
            d["mission.err.finish"] = new[] { "Acción final no válida.", "Invalid final action." };
        }
    }
}
