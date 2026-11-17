// Texts of the MVP priorities (PLAN_PC 32): camera while piloting (P0), active-effect chips (P1), thermal field (P3), battery (P2).
// Neutral interface labels written by hand (modes, shortcuts, tooltips); nothing here is a claim about the drone. Same layout as the other tables: key -> { es, en }.
using System.Collections.Generic;

namespace WebGL.Telemetry
{
    public static partial class ExtraStrings
    {
        static partial void AddMvp(Dictionary<string, string[]> d)
        {
            // ── P0: camera while piloting ──
            d["cam.follow"] = new[] { "Seguir", "Follow" };
            d["cam.free"] = new[] { "Libre", "Free" };
            d["cam.follow.tip"] = new[] { "Cámara «Seguir»: va detrás del dron y gira con él. Clic para pasar a «Libre» (B).", "«Follow» camera: behind the drone, turning with it. Click for «Free» (B)." };
            d["cam.free.tip"] = new[] { "Cámara «Libre»: la órbita de siempre. Clic para pasar a «Seguir» (B).", "«Free» camera: the usual orbit. Click for «Follow» (B)." };
            d["cam.toast.follow"] = new[] { "Cámara: Seguir", "Camera: Follow" };
            d["cam.toast.free"] = new[] { "Cámara: Libre", "Camera: Free" };
            d["cam.game"] = new[] { "Modo juego", "Game mode" };
            d["cam.game.tip"] = new[] { "Captura el cursor: el ratón orbita la cámara. Esc o X lo liberan.", "Captures the cursor: the mouse orbits the camera. Esc or X release it." };
            d["cam.game.off.tip"] = new[] { "El modo juego solo está disponible mientras pilotas.", "Game mode is only available while piloting." };
            d["cam.game.badge"] = new[] { "Modo juego · Esc o X para salir", "Game mode · Esc or X to exit" };
            d["cam.game.refused"] = new[] { "El modo juego necesita el pilotaje activado.", "Game mode needs piloting on." };
            d["pilot.legend.cam"] = new[] { "Cámara", "Camera" };
            d["pilot.legend.cam.mode"] = new[] { "Seguir / Libre", "Follow / Free" };
            d["pilot.legend.cam.game"] = new[] { "Modo juego (el ratón orbita)", "Game mode (the mouse orbits)" };
            d["pilot.legend.cam.pad"] = new[] { "Cámara", "Camera" };
            d["pilot.legend.cam.padmode"] = new[] { "Seguir / Libre", "Follow / Free" };
            d["pilot.legend.cam.dpad"] = new[] { "Orbitar", "Orbit" };
            d["pad.badge.cam"] = new[] { "LB + stick der.: cámara", "LB + right stick: camera" };
        }
    }
}
