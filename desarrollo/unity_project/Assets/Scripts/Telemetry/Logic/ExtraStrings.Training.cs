// Strings of the guided training (Lote 1c, tool 6), copied from the web dictionaries (src/web/i18n/es.ts, en.ts): same keys, same wording.
using System.Collections.Generic;

namespace WebGL.Telemetry
{
    public static partial class ExtraStrings
    {
        static partial void AddTraining(Dictionary<string, string[]> d)
        {
            d["training.title"] = new[] { "Entrenamiento", "Training" };
            d["training.disclaimer"] = new[] { "Entrenamiento en el simulador · simulado · no sustituye la formación oficial de piloto", "Simulator training · simulated · does not replace official pilot training" };
            d["training.lessons"] = new[] { "Lecciones", "Lessons" };
            d["training.rowSteps"] = new[] { "{n} pasos", "{n} steps" };
            d["training.rowStepsOne"] = new[] { "1 paso", "1 step" };
            d["training.rowStep"] = new[] { "Paso {k} de {n}", "Step {k} of {n}" };
            d["training.rowNew"] = new[] { "Sin empezar", "Not started" };
            d["training.rowDone"] = new[] { "Completada", "Completed" };
            d["training.rowDoneBest"] = new[] { "Completada · mejor {time}", "Completed · best {time}" };
            d["training.start"] = new[] { "Empezar", "Start" };
            d["training.resume"] = new[] { "Continuar", "Continue" };
            d["training.again"] = new[] { "Repetir", "Repeat" };
            d["training.stepOf"] = new[] { "Paso {k} de {n}", "Step {k} of {n}" };
            d["training.progress"] = new[] { "Progreso de la lección", "Lesson progress" };
            d["training.hint"] = new[] { "Pista", "Hint" };
            d["training.retry"] = new[] { "Reintentar", "Retry" };
            d["training.retryTitle"] = new[] { "Reintentar el paso: el punto de partida pasa a ser donde está el dron ahora", "Retry the step: its starting point becomes where the drone is now" };
            d["training.quit"] = new[] { "Salir", "Exit" };
            d["training.quitTitle"] = new[] { "Salir de la lección: el progreso queda guardado", "Leave the lesson: your progress is kept" };
            d["training.doneTitle"] = new[] { "Lección completada", "Lesson completed" };
            d["training.doneTime"] = new[] { "Tiempo: {time}", "Time: {time}" };
            d["training.newBest"] = new[] { "Mejor tiempo", "Best time" };
            d["training.toList"] = new[] { "Lecciones", "Lessons" };
            d["training.next"] = new[] { "Siguiente lección", "Next lesson" };
            d["training.courseNote"] = new[] { "Esta lección usa el circuito básico: se coloca en la escena y en el mapa.", "This lesson uses the basic course: it is placed in the scene and on the map." };
            // desktop (not in the web): the button of the strip and why it is dimmed
            d["tools.training"] = new[] { "Aprender", "Learn" }; // the web's tools.train
            d["training.need"] = new[] { "El entrenamiento es del simulador: elige la fuente Simulador.", "Training belongs to the simulator: choose the Simulator source." };
        }
    }
}
