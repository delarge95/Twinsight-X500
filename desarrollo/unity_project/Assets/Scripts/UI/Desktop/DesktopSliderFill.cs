#if UNITY_STANDALONE
using UnityEngine;
using UnityEngine.UIElements;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PLAN_PC 41 (fix 1): the shared sliders (.glass-slider) have a track but no filled part, and on a light environment the track nearly vanished over the dark popovers.
    /// The track gets a border (USS, <c>.dk-slider-fill</c> / <c>.glass-slider</c> in DesktopUI.uss) and this adds the fill in the accent colour, from the start to the dragger.
    /// The fill follows the value (changed by the user, by code with or without notification, or by a new range) and the layout.
    /// </summary>
    internal static class DesktopSliderFill
    {
        public const string FillClass = "dk-slider-fill";

        public static void AttachAll(VisualElement root)
        {
            if (root == null) return;
            foreach (Slider s in root.Query<Slider>(className: "glass-slider").ToList()) Attach(s);
        }

        public static void Attach(Slider slider)
        {
            if (slider == null || slider.Q(className: FillClass) != null) return;
            VisualElement tracker = slider.Q(className: "unity-base-slider__tracker");
            if (tracker == null || tracker.parent == null) return;
            var fill = new VisualElement { name = "DkSliderFill", pickingMode = PickingMode.Ignore };
            fill.AddToClassList(FillClass);
            tracker.parent.Insert(tracker.parent.IndexOf(tracker) + 1, fill);

            void Refresh()
            {
                float w = tracker.layout.width;
                if (float.IsNaN(w) || w < 1f) { fill.style.width = 0f; return; }
                VisualElement dragger = slider.Q(className: "unity-base-slider__dragger");
                float d = dragger != null && !float.IsNaN(dragger.layout.width) ? dragger.layout.width : 14f;
                float f = Mathf.InverseLerp(slider.lowValue, slider.highValue, slider.value);
                fill.style.width = Mathf.Clamp(f * (w - d) + d * 0.5f, 0f, w);
            }

            slider.RegisterValueChangedCallback(_ => Refresh());
            slider.RegisterCallback<GeometryChangedEvent>(_ => Refresh());
            slider.schedule.Execute(Refresh).Every(120);
            Refresh();
        }
    }
}
#endif
