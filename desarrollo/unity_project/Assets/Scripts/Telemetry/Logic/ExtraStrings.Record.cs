// Strings of the own-session recorder (Lote 1c, tool 7), copied from the web dictionaries (src/web/i18n/es.ts, en.ts): same keys, same wording. The own.* ones
// are [APROBADO 2026-11-13] there; rec.* and sessions.* carry the "provisional wording" note of that block. Desktop-only additions are at the end.
using System.Collections.Generic;

namespace WebGL.Telemetry
{
    public static partial class ExtraStrings
    {
        static partial void AddRecord(Dictionary<string, string[]> d)
        {
            d["sc.tools.record"] = new[] { "Grabar o parar la sesión", "Start or stop the recording" };
            d["own.detail"] = new[] { "Repetición de un vuelo que grabaste en esta app. Todas las lecturas son simuladas; no acepta órdenes ni fallas.", "Replay of a flight you recorded in this app. Every reading is simulated; it takes no commands and no faults." };
            d["own.readings"] = new[] { "Todas las lecturas: simulado", "All readings: simulated" };
            d["own.noFaults"] = new[] { "La sesión propia no acepta órdenes ni fallas sintéticas: solo se reproduce.", "An own session takes no commands and no synthetic faults: it only plays back." };
            d["rec.button"] = new[] { "Grabar", "Record" };
            d["rec.stop"] = new[] { "Parar", "Stop" };
            d["rec.aria"] = new[] { "Grabar el vuelo. Se guarda como archivo .twf", "Record the flight. It is saved as a .twf file" };
            d["rec.stopAria"] = new[] { "Parar la grabación: {time}", "Stop recording: {time}" };
            d["rec.need"] = new[] { "Solo se graban el Simulador y PX4 SITL.", "Only the Simulator and PX4 SITL can be recorded." };
            d["rec.noLink"] = new[] { "Sin enlace: conecta la fuente para grabar.", "No link: connect the source to record." };
            d["rec.live"] = new[] { "Grabando", "Recording" };
            d["rec.doneTitle"] = new[] { "Sesión grabada", "Recorded session" };
            d["rec.summary"] = new[] { "{time} · {frames} fotogramas · {size} KB", "{time} · {frames} frames · {size} KB" };
            d["rec.download"] = new[] { "Descargar .twf", "Download .twf" };
            d["rec.save"] = new[] { "Guardar en la app", "Save in the app" };
            d["rec.discard"] = new[] { "Descartar", "Discard" };
            d["rec.saved"] = new[] { "Guardada en la app.", "Saved in the app." };
            d["rec.max"] = new[] { "La grabación llegó al máximo de 30 min y se detuvo.", "The recording reached the 30 min maximum and stopped." };
            d["rec.tooShort"] = new[] { "La grabación es demasiado corta para guardarla (menos de 1 s).", "The recording is too short to save (under 1 s)." };
            d["rec.noStore"] = new[] { "Este navegador no permite guardar en la app: descarga el archivo.", "This browser cannot save in the app: download the file." };
            d["rec.storeFull"] = new[] { "Ya hay 5 sesiones guardadas: borra una para guardar esta.", "There are already 5 saved sessions: delete one to save this." };
            d["rec.saveError"] = new[] { "No se pudo guardar en la app: descarga el archivo.", "Could not save in the app: download the file." };
            d["sessions.title"] = new[] { "Mis sesiones", "My sessions" };
            d["sessions.empty"] = new[] { "Aún no hay sesiones guardadas. Graba un vuelo del Simulador o de PX4 SITL.", "No saved sessions yet. Record a flight of the Simulator or PX4 SITL." };
            d["sessions.open"] = new[] { "Abrir archivo…", "Open file…" };
            d["sessions.play"] = new[] { "Reproducir", "Play" };
            d["sessions.delete"] = new[] { "Borrar la sesión {name}", "Delete the session {name}" };
            d["sessions.kindSim"] = new[] { "simulador", "simulator" };
            d["sessions.kindSitl"] = new[] { "PX4 SITL", "PX4 SITL" };
            d["sessions.noStore"] = new[] { "Guardar en la app no está disponible en este navegador. Usa «Abrir archivo…».", "Saving in the app is not available in this browser. Use \"Open file…\"." };
            d["sessions.readError"] = new[] { "No se pudo leer el archivo.", "Could not read the file." };
            d["sessions.bad.format"] = new[] { "El archivo no es una sesión TWF válida.", "The file is not a valid TWF session." };
            d["sessions.bad.notOwn"] = new[] { "Es un archivo TWF, pero no una sesión grabada con TwinSight.", "It is a TWF file, but not a session recorded with TwinSight." };
            d["sessions.bad.empty"] = new[] { "La sesión no tiene fotogramas suficientes.", "The session does not have enough frames." };
            d["sessions.bad.tooLong"] = new[] { "La sesión supera los 30 min permitidos.", "The session is longer than the 30 min allowed." };
            d["sessions.folder"] = new[] { "Las sesiones se guardan en la carpeta de datos de la app.", "Sessions are saved in the app data folder." };
            d["sessions.path"] = new[] { "Ruta de un archivo .twf", "Path of a .twf file" };
            d["rec.desktopSaved"] = new[] { "Guardada: {name}", "Saved: {name}" };
        }
    }
}
