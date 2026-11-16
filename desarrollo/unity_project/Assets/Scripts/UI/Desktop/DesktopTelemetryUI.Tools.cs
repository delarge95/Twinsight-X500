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

        private void RegisterToolPopover(string key, VisualElement pop)
        {
            _toolPops[key] = pop;
            _container.Add(pop);
        }

        private void BuildToolPopovers()
        {
            BuildFencePopover();
            BuildWindPopover();
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
