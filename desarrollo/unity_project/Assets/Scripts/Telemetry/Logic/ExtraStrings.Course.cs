// Strings of the course records and ghost (Lote 1c, tool 5), copied from the web dictionaries (src/web/i18n/es.ts, en.ts): same keys, same wording.
// Keys the desktop already has in TelemetryStrings.Table win over these.
using System.Collections.Generic;

namespace WebGL.Telemetry
{
    public static partial class ExtraStrings
    {
        static partial void AddCourse(Dictionary<string, string[]> d)
        {
            d["course.title"] = new[] { "Circuito de pruebas", "Test course" };
            d["course.honest"] = new[] { "Escenario de prueba simulado · los choques son una prueba de volumen simple, no física", "Simulated test scenario · collisions are a simple volume test, not physics" };
            d["course.sitlNote"] = new[] { "Obstáculos solo en la app: Gazebo no los tiene", "Obstacles only in the app: Gazebo does not have them" };
            d["course.hudTag"] = new[] { "Escenario simulado", "Simulated scenario" };
            d["course.hudWaiting"] = new[] { "despega desde la H o cruza el primer aro", "take off from the H or fly through the first ring" };
            d["course.hudNext"] = new[] { "siguiente: {what}", "next: {what}" };
            d["course.hudDone"] = new[] { "vuelta terminada", "lap finished" };
            d["course.hudTime"] = new[] { "Tiempo", "Time" };
            d["course.hudPoints"] = new[] { "Puntos", "Points" };
            d["course.hudHits"] = new[] { "Choques", "Hits" };
            d["course.restart"] = new[] { "Reiniciar", "Restart" };
            d["course.nextGate"] = new[] { "aro {n}", "ring {n}" };
            d["course.nextZone"] = new[] { "zona {n}", "zone {n}" };
            d["course.nextFinish"] = new[] { "la meta (H)", "the finish (H)" };
            d["course.statusWaiting"] = new[] { "Esperando el despegue: despega desde la H (o cruza el primer aro) y el cronómetro arranca.", "Waiting for the take-off: take off from the H (or fly through the first ring) and the timer starts." };
            d["course.statusRunning"] = new[] { "En marcha · punto {k} de {n}", "Running · point {k} of {n}" };
            d["course.statusDone"] = new[] { "Vuelta terminada", "Lap finished" };
            d["course.resultTitle"] = new[] { "Resultado", "Result" };
            d["course.time"] = new[] { "Tiempo", "Time" };
            d["course.collisions"] = new[] { "Choques", "Hits" };
            d["course.penalised"] = new[] { "Con penalización", "With penalty" };
            d["course.penaltyNote"] = new[] { "Penalización: {s} s por choque.", "Penalty: {s} s per hit." };
            d["course.newClean"] = new[] { "Récord limpio", "Clean record" };
            d["course.newPenalised"] = new[] { "Mejor con penalización", "Best with penalty" };
            d["course.records"] = new[] { "Récords", "Records" };
            d["course.bestClean"] = new[] { "Mejor tiempo limpio (0 choques)", "Best clean time (0 hits)" };
            d["course.bestPenalised"] = new[] { "Mejor con penalización", "Best with penalty" };
            d["course.attempts"] = new[] { "Últimos intentos", "Last attempts" };
            d["course.attemptHits"] = new[] { "{n} choques", "{n} hits" };
            d["course.ghostNote"] = new[] { "Fantasma: tu mejor intento ({time} s con penalización) vuela translúcido durante el siguiente.", "Ghost: your best attempt ({time} s with penalty) flies translucent during the next one." };
            d["course.ghostNone"] = new[] { "Aún no hay fantasma: termina una vuelta para guardarlo.", "No ghost yet: finish a lap to keep it." };
            d["course.remove"] = new[] { "Quitar el circuito", "Remove the course" };
        }
    }
}
