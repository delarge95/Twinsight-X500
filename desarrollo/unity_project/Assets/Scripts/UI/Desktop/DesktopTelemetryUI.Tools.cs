#if UNITY_STANDALONE
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.Telemetry;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Lote 1a (PC only): shared plumbing of the simulator tools that live in popovers of the Telemetry strip (geofence: DesktopTelemetryUI.Fence.cs, wind:
    /// DesktopTelemetryUI.Wind.cs). A tool registers its popover here so that the strip's one-open-at-a-time rule (TogglePopover / CloseAllPopovers / Esc)
    /// covers it, and borrows the slider row. Every value these tools show is simulated and belongs to the simulator source only.
    /// </summary>
    internal sealed partial class DesktopTelemetryUI
    {
        private readonly Dictionary<string, VisualElement> _toolPops = new Dictionary<string, VisualElement>();

        partial void BuildFencePopover();
        partial void BuildWindPopover();
        partial void RelabelWind();
        partial void TickWindPopover();
        partial void RelabelFence();
        partial void TickFencePopover();
        // ── Lote 1b / 1c: one partial per tool (its own file DesktopTelemetryUI.<Tool>.cs). A tool that has nothing to do for a hook just does not implement it. ──
        partial void BuildMapTool(VisualElement strip);
        partial void DisposeMap();
        partial void RelabelMap();
        partial void TickMap(bool viewer);
        partial void BuildPreflightTool(VisualElement strip);
        partial void DisposePreflight();
        partial void RelabelPreflight();
        partial void TickPreflight(bool viewer);
        partial void BuildMissionTool(VisualElement strip);
        partial void DisposeMission();
        partial void RelabelMission();
        partial void TickMission(bool viewer);
        partial void BuildRecordTool(VisualElement strip);
        partial void DisposeRecord();
        partial void RelabelRecord();
        partial void TickRecord(bool viewer);
        partial void BuildTrainingTool(VisualElement strip);
        partial void DisposeTraining();
        partial void RelabelTraining();
        partial void TickTraining(bool viewer);
        partial void TickRecordPopover();
        partial void BuildPreflightPopover();
        partial void TickPreflightPopover();
        partial void BuildMissionPopover();
        partial void TickMissionPopover();
        partial void BuildTrainingPopover();
        partial void TickTrainingPopover();

        private void RegisterToolPopover(string key, VisualElement pop)
        {
            _toolPops[key] = pop;
            _container.Add(pop);
        }

        private void BuildToolPopovers()
        {
            BuildFencePopover();
            BuildWindPopover();
            BuildPreflightPopover();
            BuildMissionPopover();
            BuildTrainingPopover();
        }

        /// <summary>The third pill of the strip (Lote 1b / 1c): «Mapa», «Comprobar», «Misión», «Grabar», «Entrenar». The second pill has no room left.</summary>
        private VisualElement _toolsStrip;

        private void BuildToolsStrip()
        {
            _toolsStrip = new VisualElement { name = "DkTelToolsStrip" };
            _toolsStrip.AddToClassList("dk-strip");
            _toolsStrip.AddToClassList("dk-strip--tel");
            _toolsStrip.AddToClassList("dk-strip--pilot");
            BuildMapTool(_toolsStrip);
            BuildPreflightTool(_toolsStrip);
            BuildMissionTool(_toolsStrip);
            BuildRecordTool(_toolsStrip);
            BuildTrainingTool(_toolsStrip);
            _container.Add(_toolsStrip);
        }

        private void DisposeTools()
        {
            DisposeMap(); DisposePreflight(); DisposeMission(); DisposeRecord(); DisposeTraining();
            _toolsStrip?.RemoveFromHierarchy();
        }

        private void RelabelToolsLote1()
        {
            RelabelMap(); RelabelPreflight(); RelabelMission(); RelabelRecord(); RelabelTraining();
        }

        private void TickToolsLote1(bool viewer)
        {
            TickBatterySampler();
            TickMap(viewer); TickPreflight(viewer); TickMission(viewer); TickRecord(viewer); TickTraining(viewer);
        }

        private void HideToolPopovers()
        {
            foreach (var kv in _toolPops) kv.Value.style.display = DisplayStyle.None;
        }

        private void RelabelTools()
        {
            RelabelFence();
            RelabelWind();
        }

        private void TickToolPopover(string which)
        {
            if (which == "fence") TickFencePopover();
            else if (which == "wind") TickWindPopover();
            else if (which == "record") TickRecordPopover();
            else if (which == "preflight") TickPreflightPopover();
            else if (which == "mission") TickMissionPopover();
            else if (which == "training") TickTrainingPopover();
        }

        /// <summary>The simulator source when it is the running one (not PX4 SITL, not a replay), else null.</summary>
        private static SimTelemetrySource RunningSimulator => DesktopTelemetry.IsSimulator ? DesktopTelemetry.Source as SimTelemetrySource : null;

        /// <summary>A slider row in the style of the popovers' sliders (.env-slider-group); the 3D view stays responsive while it is dragged (same guard as the other sliders).</summary>
        private static Slider ToolSlider(VisualElement parent, string name, float min, float max, Action<float> changed, out Label label)
        {
            var group = new VisualElement();
            group.AddToClassList("env-slider-group");
            label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList("env-slider-label");
            var slider = new Slider(min, max) { name = name };
            slider.AddToClassList("glass-slider");
            slider.AddToClassList("env-slider");
            group.Add(label);
            group.Add(slider);
            parent.Add(group);
            slider.RegisterValueChangedCallback(e => changed(e.newValue));
            slider.RegisterCallback<PointerDownEvent>(evt => { if (evt.button == 0) InputManager.InputBlocked = true; evt.StopPropagation(); });
            slider.RegisterCallback<PointerUpEvent>(evt => { if (evt.button == 0) InputManager.InputBlocked = false; });
            return slider;
        }

        /// <summary>A button of a tool popover with the input guard of every other button.</summary>
        private static Button ToolButton(string name, string cls, Action click)
        {
            var b = new Button(click) { name = name };
            b.AddToClassList("dk-b");
            b.AddToClassList(cls);
            return b;
        }
    }
}
#endif
