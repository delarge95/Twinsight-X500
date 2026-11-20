#if UNITY_STANDALONE
using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.Telemetry;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Lote 1a, tool 9 (PC only): the geofence editor of the FLIGHT SIMULATOR. A strip button «Geocerca» (Shift+G) opens a popover with two sliders (radius 20..200 m in
    /// steps of 5, ceiling 5..120 m in steps of 1), «Restablecer 120/60», the status of the vehicle against the fence and the reason when a change is refused (in
    /// flight the radius must clear the current distance by 5 m and the ceiling the height by 2 m: the drone is never teleported). The ring, the translucent
    /// wall and the lid are drawn in the 3D scene while the simulator runs (<see cref="TelemetrySimFence"/>). Persisted in PlayerPrefs Desk_SimFence.
    /// Honesty: the fence is local state of the simulator («Geocerca del simulador · simulado»); PX4's own fence (GF_*) does not reach the client, so with SITL
    /// or the replay the button is dimmed and nothing is drawn. The logic is the pure <see cref="Geofence"/> / <see cref="FlightSim.SetConfig"/> (golden-tested).
    /// </summary>
    internal sealed partial class DesktopTelemetryUI
    {
        private Button _btnFence;
        private Label _fenceBtnLabel;
        private VisualElement _popFence;
        private Label _fcTitle, _fcRadiusLabel, _fcCeilLabel, _fcRtl, _fcStatus, _fcRefused, _fcFlightRule, _fcHonest, _fcTag;
        private Slider _fcRadius, _fcCeil;
        private Button _fcReset;
        private FenceConfig _fence;
        private TelemetrySimFence _fenceScene;
        private string _fenceRefusedCode;
        private double _fenceRefusedMin, _fenceRefusedMax;
        private float _fenceRefusedUntil;

        // ── read by the verification harness ──
        public Button FenceButton => _btnFence;
        public VisualElement FencePopover => _popFence;
        public FenceConfig FenceRemembered => _fence;
        public TelemetrySimFence FenceScene => _fenceScene;
        public Slider FenceRadiusSlider => _fcRadius;
        public Slider FenceCeilingSlider => _fcCeil;
        public Button FenceResetButton => _fcReset;
        public string FenceRefusal => _fcRefused != null && _fcRefused.style.display != DisplayStyle.None ? _fcRefused.text : null;
        public string FenceStatusText => _fcStatus?.text;
        public string FenceRefusalCode => _fenceRefusedCode;

        partial void BuildFenceButton(VisualElement strip)
        {
            _fence = SimSettings.LoadFence();
            _btnFence = StripButton("DkTelFence", "fence", out _fenceBtnLabel, () => TogglePopover("fence"));
            _btnFence.Add(Kbd("Shift+G"));
            strip.Add(_btnFence);
        }

        partial void BuildFencePopover()
        {
            _popFence = NewPopover("DkPopFence", "dk-pop--w280", out _fcTitle);
            var tagRow = new VisualElement();
            tagRow.AddToClassList("dk-fault-tagrow");
            _fcTag = Text("dk-tag", tagRow);
            _fcTag.AddToClassList("dk-tag--simulated");
            _popFence.Add(tagRow);

            _fcRadius = ToolSlider(_popFence, "DkFenceRadius", (float)Geofence.RadiusMin, (float)Geofence.RadiusMax, v => ApplyFence(new FenceConfig(Mathf.Round(v / 5f) * 5f, _fence.ceiling, _fence.rtlAltitude)), out _fcRadiusLabel);
            _fcCeil = ToolSlider(_popFence, "DkFenceCeiling", (float)Geofence.CeilingMin, (float)Geofence.CeilingMax, v =>
            {
                double c = Mathf.Round(v);
                // the RTL altitude never exceeds the ceiling: it follows it down and comes back to its default when there is room
                ApplyFence(new FenceConfig(_fence.radius, c, Math.Min(Geofence.Default().rtlAltitude, c)));
            }, out _fcCeilLabel);
            _fcRtl = Text("dk-note", _popFence);
            _fcStatus = Text("dk-note", _popFence);
            _fcRefused = Text("dk-note", _popFence);
            _fcRefused.AddToClassList("dk-note--warn");
            _fcRefused.style.display = DisplayStyle.None;

            var foot = new VisualElement();
            foot.AddToClassList("dk-fault-foot");
            _fcReset = ToolButton("DkFenceReset", "dk-b--ghost", () => ApplyFence(Geofence.Default()));
            _fcReset.AddToClassList("dk-fault-footbtn");
            foot.Add(_fcReset);
            _popFence.Add(foot);
            _fcFlightRule = Text("dk-note", _popFence);
            _fcFlightRule.AddToClassList("dk-note--muted");
            _fcHonest = Text("dk-note", _popFence);
            _fcHonest.AddToClassList("dk-note--muted");
            // UX step 5: the two long notes (flight rule, what is and is not modelled) go behind an ⓘ next to the origin tag («simulado», which stays visible)
            DesktopInfo.Beside(_fcTag, false, "DkInfoFence", _fcFlightRule, _fcHonest);
            RegisterToolPopover("fence", _popFence);
            RefreshFenceSliders();
        }

        partial void DisposeFence()
        {
            DestroyFenceScene();
            _popFence?.RemoveFromHierarchy();
        }

        partial void RelabelFence()
        {
            if (_btnFence == null || _popFence == null) return;
            _fenceBtnLabel.text = T("tools.fence");
            _fcTitle.text = T("fence.title");
            _fcTag.text = T("fence.origin");
            _fcReset.text = T("fence.reset");
            _fcFlightRule.text = T("fence.flightRule");
            _fcHonest.text = T("fence.honest");
            RefreshFenceSliders();
        }

        /// <summary>The fence the simulator has now (what is applied), or the remembered one while no simulator runs.</summary>
        private FenceConfig FenceNow()
        {
            var src = RunningSimulator;
            return src != null ? src.Model.cfg : _fence;
        }

        private void RefreshFenceSliders()
        {
            if (_fcRadius == null) return;
            FenceConfig f = FenceNow();
            _fcRadius.SetValueWithoutNotify((float)f.radius);
            _fcCeil.SetValueWithoutNotify((float)f.ceiling);
            _fcRadiusLabel.text = T("fence.radius") + " · " + f.radius.ToString("0", CultureInfo.InvariantCulture) + " m";
            _fcCeilLabel.text = T("fence.ceiling") + " · " + f.ceiling.ToString("0", CultureInfo.InvariantCulture) + " m";
            _fcRtl.text = TelemetryStrings.F("fence.rtl", Lang, "n", f.rtlAltitude.ToString("0.#", CultureInfo.InvariantCulture));
            _fcReset?.SetEnabled(!(f.radius == 120 && f.ceiling == 60 && f.rtlAltitude == 10));
        }

        /// <summary>
        /// Applies a configuration to the running simulator (a refusal leaves everything as it was and says why) and remembers what was accepted. With no simulator
        /// running only the ranges apply and the value is remembered for the next flight.
        /// </summary>
        public ConfigCheck ApplyFence(FenceConfig next)
        {
            var src = RunningSimulator;
            ConfigCheck r = src != null
                ? src.SetConfig(new SimConfigPatch { radius = next.radius, ceiling = next.ceiling, rtlAltitude = next.rtlAltitude })
                : Geofence.Validate(next, new double[3], false, true);
            if (r.ok)
            {
                _fence = next.CloneFence();
                SimSettings.SaveFence(_fence);
                _fenceRefusedCode = null;
                _fcRefused.style.display = DisplayStyle.None;
            }
            else
            {
                _fenceRefusedCode = r.code;
                _fenceRefusedMin = r.min ?? 0;
                _fenceRefusedMax = r.max ?? 0;
                _fenceRefusedUntil = Time.unscaledTime + 8f; // a refusal is about the moment it happened: it goes away by itself
                ShowFenceRefusal();
            }
            RefreshFenceSliders(); // a refused slider snaps back to what the model has
            return r;
        }

        private void ShowFenceRefusal()
        {
            if (_fenceRefusedCode == null) { _fcRefused.style.display = DisplayStyle.None; return; }
            _fcRefused.text = TelemetryStrings.F("fence.err." + _fenceRefusedCode, Lang,
                "min", _fenceRefusedMin.ToString("0.#", CultureInfo.InvariantCulture), "max", _fenceRefusedMax.ToString("0.#", CultureInfo.InvariantCulture));
            _fcRefused.style.display = DisplayStyle.Flex;
        }

        /// <summary>Shift+G (DesktopUIController): opens or closes the popover. Dimmed (and a no-op) when the simulator is not the source.</summary>
        public void ToggleFencePopover()
        {
            if (RunningSimulator == null) return;
            TogglePopover("fence");
        }

        partial void TickFencePopover()
        {
            if (_fenceRefusedCode != null && Time.unscaledTime > _fenceRefusedUntil) { _fenceRefusedCode = null; _fcRefused.style.display = DisplayStyle.None; }
            else if (_fenceRefusedCode != null) ShowFenceRefusal();
            RefreshFenceSliders();
            var f = DesktopTelemetry.Client?.Displayed;
            FenceConfig cfg = FenceNow();
            FenceStatus st = f != null ? Geofence.Status(cfg.radius, cfg.ceiling, f.p) : FenceStatus.Ok;
            _fcStatus.text = T(st == FenceStatus.Hit ? "fence.statusHit" : st == FenceStatus.Near ? "fence.statusNear" : "fence.statusOk");
            _fcStatus.EnableInClassList("dk-note--warn", st != FenceStatus.Ok);
        }

        private void DestroyFenceScene()
        {
            if (_fenceScene != null) { UnityEngine.Object.Destroy(_fenceScene.gameObject); _fenceScene = null; }
        }

        partial void TickFence(bool viewer)
        {
            if (_btnFence == null) return;
            bool sim = DesktopTelemetry.IsRunning && DesktopTelemetry.IsSimulator;
            _btnFence.SetEnabled(sim);
            _btnFence.EnableInClassList("dk-strip-btn--on", _openPop == "fence");
            string tip = sim ? T("fence.origin") : T("tools.fence.need");
            if (_btnFence.tooltip != tip) _btnFence.tooltip = tip;
            if (!sim && _openPop == "fence") CloseAllPopovers();

            var drv = TelemetryDroneDriver.Instance;
            var client = DesktopTelemetry.Client;
            TelemetryFrame f = client != null ? client.Displayed : null;
            bool active = sim && f != null && drv != null && drv.Engaged && drv.GroundRef != null && !drv.GroundRef.IsLegacy;
            if (!active) { if (_fenceScene != null) DestroyFenceScene(); return; }
            if (_fenceScene == null)
            {
                FenceConfig c0 = FenceNow();
                _fenceScene = TelemetrySimFence.Create(drv.FlightOrigin, drv.GroundRef.GroundY, drv.UnitsPerMeter, c0.radius, c0.ceiling);
                if (_fenceScene == null) return;
            }
            FenceConfig c = FenceNow();
            _fenceScene.SetSize(c.radius, c.ceiling);
            _fenceScene.SetPosition(f.p);
        }
    }
}
#endif
