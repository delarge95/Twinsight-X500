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
            // ── P2: battery depletion of the simulator (web dictionaries: simbat.*; the tool button and its tips are neutral labels of the desktop) ──
            d["simbat.title"] = new[] { "Batería del simulador", "Simulator battery" };
            d["simbat.hint"] = new[] { "simulado", "simulated" };
            d["simbat.note"] = new[] { "Simulador: con la batería baja el dron avisa, vuelve solo y, al agotarse, aterriza y se desarma. Es un modelo simplificado; no es PX4 ni un dron real.", "Simulator: with a low battery the drone warns, returns by itself and, when it runs out, lands and disarms. A simplified model; it is not PX4 and not a real drone." };
            d["simbat.ignore"] = new[] { "Ignorar batería", "Ignore battery" };
            d["simbat.params"] = new[] { "Aviso {low} % · crítica {crit} % (retorno automático) · agotada {empty} % (aterrizaje de emergencia y desarme)", "Warning {low} % · critical {crit} % (automatic return) · empty {empty} % (emergency landing and disarm)" };
            d["simbat.ignored"] = new[] { "La batería solo se gasta: no actúa sobre el vuelo", "The battery only drains: it does not act on the flight" };
            d["simbat.state.ok"] = new[] { "Batería sin aviso", "Battery with no warning" };
            d["simbat.state.low"] = new[] { "Batería baja · aviso", "Battery low · warning" };
            d["simbat.state.crit"] = new[] { "Batería crítica · retorno automático", "Battery critical · automatic return" };
            d["simbat.state.empty"] = new[] { "Batería agotada · aterrizaje de emergencia", "Battery empty · emergency landing" };
            d["tools.battery"] = new[] { "Batería", "Battery" };
            d["tools.battery.need"] = new[] { "La batería del simulador es del simulador: elige la fuente Simulador.", "The battery tool belongs to the simulator: choose the Simulator source." };

            // ── P3: the thermal field of the telemetry (web dictionaries: studio.thermalSource, studio.src*, legend.*; the row names are neutral labels of the desktop) ──
            d["studio.thermalSource"] = new[] { "Fuente térmica", "Thermal source" };
            d["studio.srcTelemetry"] = new[] { "Telemetría", "Telemetry" };
            d["studio.srcSim"] = new[] { "Simulación didáctica", "Teaching simulation" };
            d["legend.note"] = new[] { "Escala automática. Isotermas cada 5 °C.", "Automatic scale. Isotherms every 5 °C." };
            d["legend.noData"] = new[] { "Sin telemetría — sin datos térmicos", "No telemetry — no thermal data" };
            d["legend.noDataHint"] = new[] { "El modelo se muestra en gris neutro: no se inventan temperaturas.", "The model is shown in neutral grey: no temperatures are made up." };
            d["legend.noteSim"] = new[] { "Un color por pieza (simulación didáctica, ilustrativo, no FEA).", "One colour per part (didactic simulation, illustrative, not FEA)." };
            d["thermal.src.motor"] = new[] { "Motor {n}", "Motor {n}" };
            d["thermal.src.battery"] = new[] { "Batería", "Battery" };
            d["thermal.src.fc"] = new[] { "Controladora", "Flight controller" };

            // ── P4: «Agotar batería» of PX4 SITL (contract 1.1, battery_off; web dictionaries: cmd.batteryOff, cmd.batteryNote, cmd.ackOkBatteryOff, cmd.restore) ──
            d["cmd.batteryOff"] = new[] { "Agotar batería", "Drain battery" };
            d["cmd.batteryOffConfirm"] = new[] { "¿Agotar batería? Toca otra vez", "Drain battery? Tap again" };
            d["cmd.batteryNote"] = new[] { "Batería (PX4 SITL): la orden se envía a PX4 (MAV_CMD_INJECT_FAILURE) y su simulador de batería la vacía: en la prueba pasó de 70 % a 0 % en unos 6 s; PX4 avisa de batería baja, espera 5 s, aterriza y desarma. «Restaurar motores» también quita esta falla. Es lo que ocurre en la simulación, no una afirmación sobre un dron físico ni sobre una batería real.", "Battery (PX4 SITL): the command is sent to PX4 (MAV_CMD_INJECT_FAILURE) and its battery simulator drains it: in the test it went from 70% to 0% in about 6 s; PX4 warns of low battery, holds for 5 s, lands and disarms. \"Restore motors\" also clears this fault. This is what happens in the simulation, not a claim about a physical drone or a real battery." };
            d["cmd.ackOkBatteryOff"] = new[] { "aceptado por PX4; en SITL la batería simulada se vacía y PX4 aterriza y desarma", "accepted by PX4; in SITL the simulated battery drains and PX4 lands and disarms" };
            d["cmd.ackOk"] = new[] { "aceptado por PX4", "accepted by PX4" };
            d["cmd.restore"] = new[] { "Restaurar motores", "Restore motors" };
        }
    }
}
