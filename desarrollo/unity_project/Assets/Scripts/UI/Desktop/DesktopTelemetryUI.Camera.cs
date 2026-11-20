#if UNITY_STANDALONE
using System;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.Telemetry;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PC only (MVP P0, PLAN_PC 32). The camera while piloting:
    ///  - «Seguir» (default): the camera goes behind the drone and turns with it (OrbitCameraController chase, maths in <see cref="ChaseCamera"/>); «Libre» is the
    ///    usual orbit. A button of the piloting strip and the key B switch; the mode is remembered (PlayerPrefs Desk_CamMode).
    ///  - Pad (DRON mode): LB + right stick orbits the camera (the right stick flies the drone otherwise), the D-pad orbits in steps, R3 switches Seguir / Libre;
    ///    the orbit glides back behind the drone 1.5 s after the last input.
    ///  - «Modo juego»: the cursor is locked, the mouse orbits (<see cref="DesktopGameMode"/>); Esc or the key X release it; a badge and a crosshair show it.
    /// The chase only exists while piloting is ON; leaving piloting releases the cursor and gives the free orbit back where it is.
    /// </summary>
    internal sealed partial class DesktopTelemetryUI
    {
        private const string PrefCamMode = "Desk_CamMode";
        private Button _btnCam, _btnGame;
        private Label _camLabel, _gameLabel;
        private DesktopVectorIcon _camIcon;
        private VisualElement _gameShield, _gameBadge, _gameCross;
        private Label _gameBadgeText;
        private bool _camLegendBuilt;
        private CameraMode _camMode = LoadCamMode();

        /// <summary>The camera mode the pilot asked for (remembered between runs).</summary>
        public CameraMode CamMode => _camMode;
        public Button CameraButton => _btnCam;
        public Button GameModeButton => _btnGame;
        public bool GameModeOn => DesktopGameMode.Active;
        public VisualElement GameBadge => _gameBadge;
        public VisualElement GameShield => _gameShield;
        /// <summary>Verification: the pad camera input of the last frame (degrees).</summary>
        public Vector2 LastPadOrbit { get; private set; }

        private static CameraMode LoadCamMode()
        {
            try { return PlayerPrefs.GetString(PrefCamMode, "follow") == "free" ? CameraMode.Free : CameraMode.Follow; } catch (Exception) { return CameraMode.Follow; }
        }

        // ───────────────────────── build ─────────────────────────

        /// <summary>Two more buttons at the end of the piloting pill: camera mode and game mode.</summary>
        private DesktopFold _camFold;

        private void BuildCameraButtons()
        {
            _pilotStrip.Add(MakeSep());
            _btnCam = StripButton("DkCamMode", "camera", out _camLabel, () => ToggleCameraMode("button"));
            _btnCam.Add(Kbd("B"));
            // UX step 5: the camera («Seguir / Libre», B) and «Modo juego» (X) go in the «Avanzado» fold of the commands (closed, inline in the pill). Takeoff, Land, Hold (H)
            // and Return (R) stay visible: a safety command is never hidden. (Turn and Orbit have no button on the desktop: they are keys, listed in the pilot legend.)
            _camFold = new DesktopFold("commands", "DkCmdFold");
            _camFold.AddToClassList("dk-fold--inline");
            _camFold.Body.Add(_btnCam);
            _btnGame = StripButton("DkGameMode", "mouse", out _gameLabel, () => ToggleGameMode("button"));
            _btnGame.Add(Kbd("X"));
            _camFold.Body.Add(_btnGame);
            _pilotStrip.Add(_camFold);
        }

        private void BuildGameOverlay()
        {
            // the shield: while the cursor is locked in the middle of the screen no UI element under it may receive a press (a full-screen element that takes the pointer)
            _gameShield = new VisualElement { name = "DkGameShield", pickingMode = PickingMode.Position };
            _gameShield.AddToClassList("dk-game-shield");
            _gameShield.style.display = DisplayStyle.None;
            _gameShield.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            _gameShield.RegisterCallback<PointerUpEvent>(e => e.StopPropagation());
            _gameShield.RegisterCallback<PointerMoveEvent>(e => e.StopPropagation());
            _gameShield.RegisterCallback<WheelEvent>(e => e.StopPropagation());

            _gameCross = new VisualElement { name = "DkGameCross", pickingMode = PickingMode.Ignore };
            _gameCross.AddToClassList("dk-game-cross");
            _gameShield.Add(_gameCross);

            _gameBadge = new VisualElement { name = "DkGameBadge", pickingMode = PickingMode.Ignore };
            _gameBadge.AddToClassList("dk-game-badge");
            var ic = new DesktopVectorIcon("mouse", 16) { pickingMode = PickingMode.Ignore };
            ic.AddToClassList("dk-game-icon");
            _gameBadge.Add(ic);
            _gameBadgeText = new Label { pickingMode = PickingMode.Ignore };
            _gameBadgeText.AddToClassList("dk-game-text");
            _gameBadge.Add(_gameBadgeText);
            _gameShield.Add(_gameBadge);

            _root.Add(_gameShield);
            DesktopGameMode.Changed += OnGameModeChanged;
        }

        private void OnGameModeChanged(bool on, string why)
        {
            if (_gameShield == null) return;
            _gameShield.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (on) _gameShield.BringToFront();
            _btnGame?.EnableInClassList("dk-strip-btn--on", on);
            if (on) _gameBadge.AddToClassList("dk-game-badge--on"); else _gameBadge.RemoveFromClassList("dk-game-badge--on");
        }

        // ───────────────────────── texts ─────────────────────────

        private void RelabelCamera()
        {
            if (_btnCam == null) return;
            bool follow = _camMode == CameraMode.Follow;
            _camFold?.SetTitle(T("ux.advanced"));
            _camLabel.text = T(follow ? "cam.follow" : "cam.free");
            _btnCam.tooltip = T(follow ? "cam.follow.tip" : "cam.free.tip");
            _gameLabel.text = T("cam.game");
            _btnGame.tooltip = T(_pilotOn ? "cam.game.tip" : "cam.game.off.tip");
            if (_gameBadgeText != null) _gameBadgeText.text = T("cam.game.badge");
            _btnCam.EnableInClassList("dk-strip-btn--on", follow && _pilotOn);
            _btnGame.EnableInClassList("dk-strip-btn--dim", !_pilotOn);
        }

        /// <summary>The camera rows of the piloting legend (keyboard and mouse; the pad rows are added with the pad box).</summary>
        private VisualElement CameraLegendRow()
        {
            return LegendRow(new[] { "B" }, T("pilot.legend.cam.mode"), new[] { "X" }, T("pilot.legend.cam.game"));
        }

        private VisualElement CameraPadLegendRow()
        {
            return PadLegendRow(new[] { "LB", "RS" }, T("pilot.legend.cam.pad"), new[] { "R3" }, T("pilot.legend.cam.padmode"), new[] { "D-pad" }, T("pilot.legend.cam.dpad"));
        }

        // ───────────────────────── actions ─────────────────────────

        /// <summary>Applies the remembered mode to the viewer: «Seguir» chases the drone while piloting, «Libre» is the plain orbit.</summary>
        private void ApplyCameraMode()
        {
            OrbitCameraController cam = OrbitCameraController.Instance;
            if (cam != null) cam.DesktopSetChase(_pilotOn && _camMode == CameraMode.Follow);
            RelabelCamera();
        }

        public void SetCameraMode(CameraMode mode, string why, bool toast = true)
        {
            if (mode == _camMode) { ApplyCameraMode(); return; }
            _camMode = mode;
            try { PlayerPrefs.SetString(PrefCamMode, mode == CameraMode.Free ? "free" : "follow"); } catch (Exception) { }
            ApplyCameraMode();
            Debug.Log("[Camera] mode " + mode + " (" + why + ")");
            if (toast && _pilotOn) ShowToast(T(mode == CameraMode.Follow ? "cam.toast.follow" : "cam.toast.free"), false, 1.6f);
        }

        public void ToggleCameraMode(string why)
        {
            SetCameraMode(_camMode == CameraMode.Follow ? CameraMode.Free : CameraMode.Follow, why);
            _btnCam?.Blur();
        }

        /// <summary>Enters or leaves «Modo juego». Entering needs piloting ON, no text field and no help overlay.</summary>
        public bool SetGameMode(bool on, string why)
        {
            if (on == DesktopGameMode.Active) return on;
            if (!on) { DesktopGameMode.Set(false, why); return false; }
            bool help = DesktopUIController.Instance != null && DesktopUIController.Instance.IsHelpOpen;
            if (!_pilotOn) { ShowToast(T("cam.game.refused"), true); return false; }
            if (help || DesktopKeyboardGate.Typing) return false;
            BlurFocus();
            DesktopGameMode.Set(true, why);
            return true;
        }

        public void ToggleGameMode(string why)
        {
            SetGameMode(!DesktopGameMode.Active, why);
            _btnGame?.Blur();
        }

        /// <summary>Called when piloting changes: the chase follows piloting, and the cursor is never left captured.</summary>
        private void OnPilotChangedForCamera(bool on)
        {
            if (!on) DesktopGameMode.Release("piloting ended");
            ApplyCameraMode();
        }

        // ───────────────────────── per frame ─────────────────────────

        /// <summary>Keyboard part (inside the key reader of the pilot: it only runs while piloting and nothing owns the keyboard).</summary>
        private void ReadCameraKeys()
        {
            if (DesktopKeyboardGate.Down(KeyCode.B)) ToggleCameraMode("key B");
            if (DesktopKeyboardGate.Down(KeyCode.X)) ToggleGameMode("key X");
        }

        /// <summary>Pad part: LB + right stick and the D-pad orbit, R3 switches the mode. DRON mode only (the caller checks it).</summary>
        private void ReadCameraPad(DesktopPad pad, PadState s)
        {
            if (pad.Pressed(PadButtons.RightStick)) ToggleCameraMode("pad R3");
            bool lb = s.Has(PadButtons.LB);
            ChaseCamera.PadOrbit(lb, s.RightX, s.RightY, s.Has(PadButtons.DpadLeft), s.Has(PadButtons.DpadRight), s.Has(PadButtons.DpadUp), s.Has(PadButtons.DpadDown),
                Time.unscaledDeltaTime, out float dYaw, out float dPitch);
            LastPadOrbit = new Vector2(dYaw, dPitch);
            if (dYaw != 0f || dPitch != 0f) OrbitCameraController.Instance?.DesktopOrbitBy(dYaw, dPitch);
        }

        /// <summary>Every frame (also with piloting off): the cursor must never stay captured when its conditions are gone.</summary>
        private void TickCamera()
        {
            if (!DesktopGameMode.Active) return;
            bool help = DesktopUIController.Instance != null && DesktopUIController.Instance.IsHelpOpen;
            if (!_pilotOn) DesktopGameMode.Release("piloting off");
            else if (help) DesktopGameMode.Release("help opened");
            else if (DesktopKeyboardGate.Typing) DesktopGameMode.Release("a text field took the keyboard");
            else if (!Application.isFocused && !IgnoreFocusLoss) DesktopGameMode.Release("the window lost focus");
        }
    }
}
#endif
