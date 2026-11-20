// Texts of the desktop UX cut (PLAN_PC 39): the «Avanzado» fold and the «Disponible en» line. NEW, PENDING APPROVAL (the web has both: common.advanced and
// tools.availableIn in src/web/i18n; neither was approved for the desktop yet). Same layout as the other tables: key -> { es, en }.
using System.Collections.Generic;

namespace WebGL.Telemetry
{
    public static partial class ExtraStrings
    {
        static partial void AddUx(Dictionary<string, string[]> d)
        {
            d["ux.advanced"] = new[] { "Avanzado", "Advanced" }; // PENDING APPROVAL (web: common.advanced)
            d["ux.availableIn"] = new[] { "Disponible en: {sources}", "Available in: {sources}" }; // PENDING APPROVAL (web: tools.availableIn)
        }
    }
}
