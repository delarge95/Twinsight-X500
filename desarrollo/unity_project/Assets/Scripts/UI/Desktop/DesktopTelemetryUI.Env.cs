#if UNITY_STANDALONE
using System;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.Telemetry;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PLAN_PC 28.2 / 28.3 (PC only): the environment toggles of the Telemetry strip and the altitude label.
    ///  - «Suelo» (default ON): shows or hides the world ground, the drone's shadow and the altitude cue (TelemetryDroneDriver.ShowGroundReference).
    ///  - «Escenario» (default OFF): the simulated test course (28.3, see DesktopTelemetryUI.Course.cs); only in the flight simulator.
    ///  - the altitude chip beside the thin line from the drone to its ground point (<see cref="DesktopAltitudeLabel"/>).
    /// </summary>
    internal sealed partial class DesktopTelemetryUI
    {
        public const string PrefGround = "Desk_Ground";

        private Button _btnGround;
        private Label _groundLabel;
        private DesktopAltitudeLabel _altLabel;

        public Button GroundButton => _btnGround;
        public DesktopAltitudeLabel AltitudeLabel => _altLabel;
        public bool GroundOn => TelemetryDroneDriver.ShowGroundReference;

        private void BuildEnvButtons(VisualElement strip)
        {
            TelemetryDroneDriver.ShowGroundReference = PlayerPrefs.GetInt(PrefGround, 1) != 0;
            // test aids for the FPS runs: -env-noground starts without the environment, -env-course starts with the course placed (needs -tel-sim)
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-env-noground") >= 0) TelemetryDroneDriver.ShowGroundReference = false;
            strip.Add(MakeSep());
            _btnGround = StripButton("DkTelGround", "env_studio", out _groundLabel, () => SetGround(!TelemetryDroneDriver.ShowGroundReference));
            strip.Add(_btnGround);
            _btnGround.EnableInClassList("dk-strip-btn--on", TelemetryDroneDriver.ShowGroundReference);
            BuildCourseButton(strip);
            BuildFenceButton(strip);
            BuildWindButton(strip);
        }

        // the simulated test course (PLAN_PC 28.3, DesktopTelemetryUI.Course.cs) plugs in here; without it these do nothing
        partial void BuildCourseButton(VisualElement strip);
        partial void BuildCourseOverlay();
        partial void DisposeCourse();
        partial void RelabelCourse();
        partial void TickCourse(bool viewer);
        // Lote 1a: the simulator tools (DesktopTelemetryUI.Fence.cs) plug in here, like the course
        partial void BuildFenceButton(VisualElement strip);
        partial void DisposeFence();
        partial void TickFence(bool viewer);
        partial void BuildWindButton(VisualElement strip);
        partial void DisposeWind();
        partial void TickWind(bool viewer);

        private void BuildEnvOverlay()
        {
            _altLabel = new DesktopAltitudeLabel(_root);
            BuildCourseOverlay();
        }

        private void DisposeEnv()
        {
            _altLabel?.Dispose();
            DisposeCourse();
            DisposeFence();
            DisposeWind();
        }

        /// <summary>Shows or hides the environment (ground, shadow, altitude line and label). Remembered between runs.</summary>
        public void SetGround(bool on)
        {
            TelemetryDroneDriver.ShowGroundReference = on;
            PlayerPrefs.SetInt(PrefGround, on ? 1 : 0);
            _btnGround?.EnableInClassList("dk-strip-btn--on", on);
            var drv = TelemetryDroneDriver.Instance;
            if (drv != null && drv.GroundRef != null) drv.GroundRef.Visible = on;
        }

        private void RelabelEnv()
        {
            if (_btnGround == null) return;
            _groundLabel.text = T("env.ground");
            _btnGround.tooltip = T("env.ground.tip");
            RelabelCourse();
            RelabelFence();
            RelabelWind();
        }

        private void TickEnv()
        {
            bool viewer = !_ui.IsHeroVisible && !_ui.IsOnboardingOpen;
            _altLabel?.Tick(viewer);
            TickCourse(viewer);
            TickFence(viewer);
            TickWind(viewer);
        }
    }
}
#endif
