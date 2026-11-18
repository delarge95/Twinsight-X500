#if UNITY_STANDALONE
using System.Collections;
using UnityEngine;
using WebGL.Core.Managers;
using WebGL.Core.Thermal;
using WebGL.Telemetry;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>MVP probe of P3: the thermal field of the telemetry on the drone (shader, hook, source selector, legend with origins, no-data grey) in the real simulator.</summary>
    internal sealed partial class DesktopCaptureMvp
    {
        private Vector2 ScreenOf(Vector3 world)
        {
            Vector3 s = Camera.main.WorldToScreenPoint(world);
            return new Vector2(s.x, s.y);
        }

        /// <summary>Grabs the screen once and reads the colour at the given screen points (origin bottom-left, as the texture).</summary>
        private IEnumerator SampleColours(Vector2[] pts, Color[] outCols, string shotName)
        {
            Texture2D tex = null;
            yield return DesktopScreenGrab.Grab(t => tex = t);
            if (tex == null) yield break;
            float luma = DesktopScreenGrab.LastLuma;
            if (luma < 3f) { _blackShots++; Log("BLACK " + _tag + "_" + shotName + " (luma " + F1(luma) + ")"); }
            for (int i = 0; i < pts.Length; i++)
            {
                int x = Mathf.Clamp(Mathf.RoundToInt(pts[i].x / Screen.width * tex.width), 0, tex.width - 1);
                int y = Mathf.Clamp(Mathf.RoundToInt(pts[i].y / Screen.height * tex.height), 0, tex.height - 1);
                outCols[i] = tex.GetPixel(x, y);
            }
            if (shotName != null)
            {
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(OutputDirectory, _tag + "_" + shotName + ".png"), ImageConversion.EncodeToPNG(tex));
                Log("shot " + _tag + "_" + shotName + " " + Screen.width + "x" + Screen.height + " via " + DesktopScreenGrab.LastMethod + " luma " + F1(luma));
            }
            Destroy(tex);
        }

        private IEnumerator ThermalChecks()
        {
            Log("── P3: thermal field of the telemetry ──");
            Tel.CloseAllPopovers();
            Check("the Thermal mode is not on yet: no field", !ThermalFieldPC.Active);
            DesktopTelemetry.Client.Send(ClientMessage.Takeoff(6), out _);
            yield return WaitAlt(6, 20f);
            Cam.ResetView();
            yield return new WaitForSecondsRealtime(1.5f);
            Check("the PC thermal material exists in Resources and its shader is supported", ViewModeManager.Instance.ThermalFieldMaterial != null && ViewModeManager.Instance.ThermalFieldMaterial.shader.isSupported);
            ViewModeManager.Instance.SetViewMode(ViewMode.Thermal);
            yield return new WaitForSecondsRealtime(1.5f);
            ThermalFieldPC fld = ThermalFieldPC.Instance;
            Check("Thermal mode + a session + the Telemetría source: the field is active and the didactic controller stands down", ThermalFieldPC.Active && ViewModeManager.ThermalFieldRequested && ThermalViewController.FieldActive);
            Check("the card of the source and the legend show", Tel.ThermalCardShown && Tel.ThermalLegendShown && Tel.ThermalTelemetryButton.text == T("studio.srcTelemetry") && Tel.ThermalSimButton.text == T("studio.srcSim"));
            Check("the Telemetría button is the lit one", Tel.ThermalTelemetryButton.ClassListContains("dk-b--on") && !Tel.ThermalSimButton.ClassListContains("dk-b--on"));
            Check("the six sources have a centre (motors, battery pad, flight controller)", fld.SourceKnown(0) && fld.SourceKnown(1) && fld.SourceKnown(2) && fld.SourceKnown(3) && fld.SourceKnown(4) && fld.SourceKnown(5));
            Check("the radii are the web's (0.045 / 0.09 / 0.05 m) times the units per metre", Mathf.Abs(fld.SourceRadiusLocal(0) / fld.SourceRadiusLocal(4) - 0.5f) < 0.01f && Mathf.Abs(fld.SourceRadiusLocal(5) / fld.SourceRadiusLocal(4) - 0.05f / 0.09f) < 0.01f);
            string tagSim = FlightOrigins.Text(ReadingOrigin.Simulated, Lang, HonestyLabels.Load());
            bool allSim = true;
            for (int i = 0; i < 5; i++) allSim &= Tel.ThermalRowText(i) != null && Tel.ThermalRowText(i).EndsWith("| " + tagSim);
            Log("rows: " + Tel.ThermalRowText(0) + " ; " + Tel.ThermalRowText(4) + " ; " + Tel.ThermalRowText(5));
            Check("every source row says its origin: «simulado» over the simulator (no figure without its tag)", allSim);
            Check("the flight controller (not carried by the simulator) says «no disponible» and prints no figure", Tel.ThermalRowText(5) != null && Tel.ThermalRowText(5).Contains("| —"));
            Check("the range is at least 15 C wide (auto-range) and the legend shows it (" + Tel.ThermalMinText + " .. " + Tel.ThermalMaxText + ")", fld.Range.max - fld.Range.min >= 15 - 1e-6 && Tel.ThermalMinText.EndsWith("°C") && Tel.ThermalMaxText.EndsWith("°C"));

            // a hot motor 1 (probe hook on the model): the range follows, the field peaks at its centre and the pixels agree
            Sim.Model.tempMotor[0] = 62;
            yield return new WaitForSecondsRealtime(5f);
            Check("the range follows the hot motor (max " + F1(fld.Range.max) + " C)", fld.Range.max > 60);
            double tHot = fld.TemperatureAtLocal(fld.SourceCentreLocal(0));
            double tFar = fld.TemperatureAtLocal(fld.SourceCentreLocal(0) + new Vector3(5f, 5f, 5f) * fld.SourceRadiusLocal(0));
            Log("field at motor 1 centre " + F1(tHot) + " C, five radii away " + F1(tFar) + " C (ambient " + F1(fld.Ambient) + ")");
            Check("the formula (the one the golden thermal.json tests, shared with the shader): the motor centre is its temperature, far away it is the ambient", Mathf.Abs((float)tHot - fld.TempOf(0)) < 0.6f && Mathf.Abs((float)tFar - (float)fld.Ambient) < 0.01f);
            Transform m1 = TelemetryDroneDriver.Instance.MotorPart(1), m3 = TelemetryDroneDriver.Instance.MotorPart(3);
            var pts = new[] { ScreenOf(m1.position), ScreenOf(m3.position) };
            var cols = new Color[2];
            yield return SampleColours(pts, cols, "70_p3_termico_telemetria");
            Log("pixel at the hot motor " + cols[0] + ", at motor 3 " + cols[1]);
            Check("the hot motor is drawn warmer and brighter than a cool one (inferno: red channel)", cols[0].r > cols[1].r + 0.15f);
            Check("it is not black", cols[0].maxColorComponent > 0.2f);

            // the selector
            Click(Tel.ThermalSimButton);
            yield return new WaitForSecondsRealtime(1.2f);
            Check("«Simulación didáctica»: the field stands down and the didactic controller takes the mode back", !ThermalFieldPC.Active && !ViewModeManager.ThermalFieldRequested && !ThermalViewController.FieldActive);
            Check("its card says the didactic note «Un color por pieza … no FEA»", Tel.ThermalSimNoteText == T("legend.noteSim") && !Tel.ThermalLegendShown);
            Check("the choice is remembered", PlayerPrefs.GetString(ThermalFieldPC.PrefSource) == "sim");
            yield return Shot("71_p3_simulacion_didactica", 0.3f);
            Click(Tel.ThermalTelemetryButton);
            yield return new WaitForSecondsRealtime(1.2f);
            Check("back to «Telemetría»: the field again", ThermalFieldPC.Active && Tel.ThermalLegendShown && PlayerPrefs.GetString(ThermalFieldPC.PrefSource) == "telemetry");

            // no session: the Inspector has no telemetry -> only the didactic simulation, no card
            Sim.Model.tempMotor[0] = 30;
            DesktopTelemetry.Client.Send(ClientMessage.Land(), out _);
            yield return WaitUntil(() => Frame != null && Frame.landed, 25f);
            DesktopTelemetry.Stop();
            yield return new WaitForSecondsRealtime(1.2f);
            Check("without a session the field is off and the card hidden (Inspector: only the didactic simulation)", !ThermalFieldPC.Active && !Tel.ThermalCardShown);
            yield return Shot("72_p3_sin_sesion_simulacion", 0.3f);

            // a telemetry session that has no data yet: a neutral grey, never a scale
            Tel.StartSitl("ws://127.0.0.1:9");
            yield return new WaitForSecondsRealtime(2.0f);
            Check("a session without frames: the field is active but says no data", ThermalFieldPC.Active && fld.NoData && Tel.ThermalNoDataText == T("legend.noData"));
            yield return Shot("73_p3_sin_datos", 0.3f);
            DesktopTelemetry.Stop();
            ViewModeManager.Instance.SetViewMode(ViewMode.Realistic);
            yield return new WaitForSecondsRealtime(1.0f);
            Check("Realistic again: the field is off", !ThermalFieldPC.Active && !ViewModeManager.ThermalFieldRequested);
            DesktopTelemetry.StartSimulator();
            yield return new WaitForSecondsRealtime(2.5f);
        }
    }
}
#endif
