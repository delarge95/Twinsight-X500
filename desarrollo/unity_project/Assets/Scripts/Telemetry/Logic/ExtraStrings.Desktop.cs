// Texts of the Lote 1c tools that only the desktop needs: the web downloads / uploads files through the browser, the desktop writes them to a folder and
// lists what is there; and the reasons of the mission runner (Spanish constants in the shared logic) in both languages. Written by hand, not generated.
using System.Collections.Generic;

namespace WebGL.Telemetry
{
    public static partial class ExtraStrings
    {
        static partial void AddDesktop(Dictionary<string, string[]> d)
        {
            // mission planner (tool 2)
            d["mission.d.exported"] = new[] { "Exportada a {path}", "Exported to {path}" };
            d["mission.d.exportFailed"] = new[] { "No se pudo exportar: {why}", "Could not export: {why}" };
            d["mission.d.folder"] = new[] { "Elige un archivo de {path}", "Pick a file from {path}" };
            d["mission.d.noFiles"] = new[] { "No hay misiones en {path}", "No missions in {path}" };
            d["mission.d.reason.timeout"] = new[] { "no llegó", "it did not arrive" };
            d["mission.d.reason.landed"] = new[] { "el dron aterrizó", "the drone landed" };
            d["mission.d.reason.focus"] = new[] { "se perdió el foco", "the window lost the focus" };
            d["mission.d.reason.user"] = new[] { "por el usuario", "by the user" };
        }
    }
}
