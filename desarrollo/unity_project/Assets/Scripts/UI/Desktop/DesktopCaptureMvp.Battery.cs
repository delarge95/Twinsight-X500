#if UNITY_STANDALONE
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.UI.Panels;
using WebGL.Telemetry;

namespace WebGL.UI.Desktop
{
    /// <summary>MVP probes of P2 (battery depletion of the simulator, in the real simulator) and P4 (battery_off of PX4 SITL, against a manual source that plays the bridge).</summary>
    internal sealed partial class DesktopCaptureMvp
    {
        private static SimTelemetrySource Sim => DesktopTelemetry.Source as SimTelemetrySource;
        private static bool Shown(VisualElement e) => e != null && e.resolvedStyle.display != DisplayStyle.None;

        private IEnumerator WaitUntil(System.Func<bool> cond, float timeout)
        {
            float until = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < until && !cond()) yield return null;
        }

        // ───────────────────────── P2: the battery of the simulator acts on the flight ─────────────────────────

        private IEnumerator BatteryChecks()
        {
            Log("── P2: battery depletion of the simulator ──");
            Tel.SetPilot(false, "probe");
            DesktopTelemetry.Client.Send(ClientMessage.Land(), out _);
            yield return WaitUntil(() => Frame != null && Frame.landed, 25f);
            yield return new WaitForSecondsRealtime(1f);
            Tel.CloseAllPopovers();
            Check("the «Batería» button exists in the simulator strip and is enabled", B("DkTelBatteryReact") != null && B("DkTelBatteryReact").enabledSelf);
            Check("by default the battery acts on the flight (the switch is off)", Sim.Model.cfg.battery.react && Tel.BatteryReactOn);

            Click(Tel.BatteryGear); // PLAN_PC 42: the pill is the switch; its gear opens the settings
            yield return new WaitForSecondsRealtime(0.5f);
            Check("the popover opens", Tel.OpenPopover == "batteryreact" && Shown(Tel.BatteryPopover));
            Check("it shows the approved note «no es PX4 ni un dron real»", HelpHas(T("simbat.note")) && T("simbat.note").Contains(AppLanguageManager.IsSpanish ? "no es PX4 ni un dron real" : "not PX4 and not a real drone"));
            Check("it is tagged «simulado»", HelpHas(T("simbat.hint")));
            Check("the thresholds are visible (15 / 7 / 0 %)", Tel.BatteryParamsText == TelemetryStrings.F("simbat.params", Lang, "low", "15", "crit", "7", "empty", "0"));
            Check("the state says the battery has no warning", Tel.BatteryStateText == T("simbat.state.ok"));
            Check("the «Ignorar batería» switch is there and off", Tel.BatteryIgnoreButton.text == T("simbat.ignore") && !Tel.BatteryIgnoreButton.ClassListContains("dk-b--on"));
            yield return Shot("50_p2_popover_bateria", 0.2f);

            Click(Tel.BatteryIgnoreButton);
            yield return new WaitForSecondsRealtime(0.6f);
            Check("«Ignorar batería» on: the model stops reacting and the choice is remembered", !Sim.Model.cfg.battery.react && PlayerPrefs.GetString(SimSettings.PrefBatteryReact) == "0");
            Check("the popover says the battery only drains", Tel.BatteryStateText == T("simbat.ignored"));
            Check("the chip «Quitar …» appears with its tooltip", ChipLive("DkChipBatteryIgnore") && B("DkChipBatteryIgnore").tooltip == Tip("Remove “Ignore battery”", "Quitar «Ignorar batería»"));
            yield return Shot("51_p2_ignorar_con_chip", 0.2f);
            Tel.CloseAllPopovers();
            Click(B("DkChipBatteryIgnore"));
            yield return new WaitForSecondsRealtime(0.6f);
            Check("the chip turns the reaction back on and leaves", Sim.Model.cfg.battery.react && PlayerPrefs.GetString(SimSettings.PrefBatteryReact) == "1" && ChipGone("DkChipBatteryIgnore"));

            // the flight: warning at 15 %, critical at 7 % with the return, 0 %: hold, refusals, landing, disarm
            DesktopTelemetry.Client.Send(ClientMessage.Takeoff(6), out _);
            yield return WaitAlt(6, 20f);
            yield return new WaitForSecondsRealtime(1f);
            Sim.Model.rem = 0.1505;
            yield return WaitUntil(() => Sim.Model.batReact.stage >= 1, 12f);
            Check("warning at 15 %: stage 1 and the drone keeps its mode (no return yet)", Sim.Model.batReact.stage == 1 && Sim.Model.mode != SimMode.Rtl && Sim.Model.mode != SimMode.Land);
            Click(Tel.BatteryGear); // PLAN_PC 42: the pill is the switch; its gear opens the settings
            yield return new WaitForSecondsRealtime(0.6f);
            Check("the popover says «Batería baja · aviso»", Tel.BatteryStateText == T("simbat.state.low"));
            yield return Shot("52_p2_aviso_15", 0.1f);
            Tel.CloseAllPopovers();
            Sim.Model.rem = 0.0705;
            yield return WaitUntil(() => Sim.Model.batReact.stage >= 2, 12f);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("critical at 7 %: the automatic return starts (RTL)", Sim.Model.batReact.stage == 2 && Sim.Model.mode == SimMode.Rtl);
            Click(Tel.BatteryGear); // PLAN_PC 42: the pill is the switch; its gear opens the settings
            yield return new WaitForSecondsRealtime(0.6f);
            Check("the popover says «Batería crítica · retorno automático»", Tel.BatteryStateText == T("simbat.state.crit"));
            yield return Shot("53_p2_critica_7_retorno", 0.1f);
            Tel.CloseAllPopovers();
            Sim.Model.rem = 0;
            yield return WaitUntil(() => Sim.Model.batReact.emergency, 5f);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("0 %: emergency (hold where it is, 5 s) and stage 3", Sim.Model.batReact.stage == 3 && Sim.Model.batReact.emergency && Sim.Model.batReact.holdLeft.HasValue && Sim.Model.mode == SimMode.Hold);
            DesktopTelemetry.Client.Send(ClientMessage.FlyTo(5, 5, 5), out _);
            yield return new WaitForSecondsRealtime(0.4f);
            AckMessage ack = DesktopTelemetry.Client.LastAck;
            Check("during the emergency a manoeuvre is refused with the approved reason", ack != null && !ack.ok && ack.detail == BatteryReact.EmergencyDetail);
            Click(Tel.BatteryGear); // PLAN_PC 42: the pill is the switch; its gear opens the settings
            yield return new WaitForSecondsRealtime(0.6f);
            Check("the popover says «Batería agotada · aterrizaje de emergencia»", Tel.BatteryStateText == T("simbat.state.empty"));
            yield return Shot("54_p2_agotada_emergencia", 0.1f);
            Tel.CloseAllPopovers();
            yield return WaitUntil(() => Sim.Model.mode == SimMode.Land, 9f);
            Check("after the 5 s hold the emergency landing starts", Sim.Model.mode == SimMode.Land);
            yield return WaitUntil(() => Sim.Model.landed, 30f);
            Check("touchdown: still armed for the delayed disarm", Sim.Model.landed && Sim.Model.batReact.disarmIn.HasValue);
            yield return WaitUntil(() => !Sim.Model.armed, 6f);
            Check("then the drone is disarmed", !Sim.Model.armed && !Sim.Model.batReact.emergency);
            yield return Shot("55_p2_desarmado", 0.2f);
            // a new takeoff gives a new battery and a new flight
            DesktopTelemetry.Client.Send(ClientMessage.Takeoff(4), out _);
            yield return new WaitForSecondsRealtime(1f);
            Check("a new takeoff: new battery and a clean state", Sim.Model.rem > 0.95 && Sim.Model.batReact.stage == 0 && !Sim.Model.batReact.emergency);
            DesktopTelemetry.Client.Send(ClientMessage.Land(), out _);
            yield return WaitUntil(() => Frame != null && Frame.landed, 20f);
        }

        // ───────────────────────── P4: «Agotar batería» of PX4 SITL ─────────────────────────

        private IEnumerator SitlBatteryChecks()
        {
            Log("── P4: battery_off of PX4 SITL ──");
            DesktopTelemetry.Stop();
            yield return new WaitForSecondsRealtime(0.5f);
            var src = new ManualTelemetrySource(SourceClassification.Sitl, false);
            Check("a SITL-like source starts", DesktopTelemetry.Start(src));
            var flying = FlightSim.Create();
            FlightSim.Command(flying, ClientMessage.Takeoff(6));
            for (int i = 0; i < 600; i++) FlightSim.Step(flying, 0.01);
            HelloMessage hello = new SimTelemetrySource().BuildHello(ReplaySchedule.WallClockMs());
            hello.commands = new[] { "takeoff", "set_altitude", "land", "fly_to", "yaw", "orbit", "rtl", "hold", "motor_off", "restore", "battery_off", "pilot" };
            src.Push(hello);
            for (int i = 0; i < 12; i++) { src.Push(new FramesMessage { frames = new[] { FlightSim.Frame(flying, ReplaySchedule.WallClockMs()) } }); yield return new WaitForSecondsRealtime(0.1f); }
            Check("the session is SITL and not the simulator", DesktopTelemetry.IsRunning && !DesktopTelemetry.IsSimulator && DesktopTelemetry.Client.Classification == SourceClassification.Sitl);
            Check("the frame says armed", Frame != null && Frame.armed);

            Tel.TogglePopover("faults");
            yield return new WaitForSecondsRealtime(0.6f);
            Check("the Faults popover is open", Tel.OpenPopover == "faults");
            Check("the battery_off controls show because the bridge announced battery_off", Shown(Tel.SitlFaultControls));
            Check("«Agotar batería» is enabled (armed) and says the approved label", Tel.SitlBatteryOffButton.enabledSelf && Tel.SitlBatteryOffButton.text == T("cmd.batteryOff"));
            Check("the note is the approved cmd.batteryNote (PX4's order, «no una afirmación sobre un dron físico»)", Tel.SitlFaultNote == T("cmd.batteryNote") && Tel.SitlFaultNote.Contains("MAV_CMD_INJECT_FAILURE"));
            yield return Shot("60_p4_sitl_agotar_bateria", 0.2f);
            Click(Tel.SitlBatteryOffButton);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("first tap: it only asks «¿Agotar batería? Toca otra vez»; nothing was sent", Tel.SitlBatteryOffArmed && Tel.SitlBatteryOffButton.text == T("cmd.batteryOffConfirm") && src.Sent.IsEmpty);
            yield return Shot("61_p4_sitl_confirmar", 0.1f);
            Click(Tel.SitlBatteryOffButton);
            yield return new WaitForSecondsRealtime(0.3f);
            string sent = null;
            src.Sent.TryDequeue(out sent);
            Check("second tap: battery_off is sent (" + sent + ")", sent != null && sent.Contains("\"battery_off\"") && !Tel.SitlBatteryOffArmed);
            src.Push(new AckMessage { command = "battery_off", ok = true });
            yield return new WaitForSecondsRealtime(0.6f);
            Check("the ack reads «aceptado por PX4; en SITL la batería simulada se vacía y PX4 aterriza y desarma»", Tel.SitlFaultAck == "battery_off: " + T("cmd.ackOkBatteryOff"));
            Check("the explanation topic follows the battery", Tel.ExplainTopic == FaultTopic.BatterySag);
            Tel.CloseAllPopovers();
            Tel.ShowFaultExplanation(FaultTopic.BatterySag, 0, null);
            yield return new WaitForSecondsRealtime(0.8f);
            Check("the card «En esta fuente · SITL» for the battery is cmd.batteryNote", Tel.DockCard.SourceText == T("cmd.batteryNote") && Tel.DockCard.SourceText == FaultExplain.InSource(FaultTopic.BatterySag, ExplainSource.Sitl, 0, Lang));
            yield return Shot("62_p4_sitl_tarjeta_bateria", 0.2f);
            Tel.SetDockOpen(false);

            // a bridge that does not announce battery_off (contract 1.0.0): nothing is offered
            HelloMessage old = new SimTelemetrySource().BuildHello(ReplaySchedule.WallClockMs());
            old.commands = new[] { "takeoff", "land", "motor_off", "restore", "hold" };
            src.Push(old);
            src.Push(new FramesMessage { frames = new[] { FlightSim.Frame(flying, ReplaySchedule.WallClockMs()) } });
            Tel.TogglePopover("faults");
            yield return new WaitForSecondsRealtime(0.8f);
            Check("with a bridge that does not list battery_off the controls are hidden", !Shown(Tel.SitlFaultControls));
            Tel.CloseAllPopovers();
            DesktopTelemetry.Stop();
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }
}
#endif
