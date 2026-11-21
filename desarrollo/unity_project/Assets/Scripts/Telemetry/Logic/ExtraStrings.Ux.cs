// Texts of the desktop UX cut (PLAN_PC 39): the «Avanzado» fold and the «Disponible en» line. APPROVED 2026-11-20 (the web has both: common.advanced and
// tools.availableIn in src/web/i18n; approved for the desktop too). Same layout as the other tables: key -> { es, en }.
using System.Collections.Generic;

namespace WebGL.Telemetry
{
    public static partial class ExtraStrings
    {
        static partial void AddUx(Dictionary<string, string[]> d)
        {
            d["ux.advanced"] = new[] { "Avanzado", "Advanced" }; // approved 2026-11-20 (web: common.advanced)
            d["ux.availableIn"] = new[] { "Disponible en: {sources}", "Available in: {sources}" }; // approved 2026-11-20 (web: tools.availableIn)
            AddUx42(d);
        }

        // PLAN_PC 42 (rama pc-fix-2): PENDIENTES de aprobacion. Etiquetas cortas; lo largo va al tooltip / ⓘ.
        static void AddUx42(Dictionary<string, string[]> d)
        {
            d["ux.sec.cmd"] = new[] { "Mando", "Controls" };
            d["ux.sec.scene"] = new[] { "Escena", "Scene" };
            d["ux.sec.tools"] = new[] { "Herramientas", "Tools" };
            d["ux.sec.tip"] = new[] { "Mostrar u ocultar este grupo", "Show or hide this group" };
            d["ux.more"] = new[] { "Más", "More" };
            d["ux.more.tip"] = new[] { "Resto del menú (pilotando se muestra solo lo esencial)", "Rest of the menu (only the essentials while piloting)" };
            d["ux.src.on.tip"] = new[] { "Pulsa otra vez para apagar la telemetría", "Press again to turn the telemetry off" };
            d["ux.win.tip"] = new[] { "Arrastra para mover · doble clic: a su sitio", "Drag to move · double-click: back to place" };
            d["ux.win.reset"] = new[] { "Restablecer ventanas", "Reset windows" };
        }
    }
}
