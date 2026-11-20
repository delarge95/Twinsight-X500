#if UNITY_STANDALONE
using UnityEngine;
using UnityEngine.UIElements;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PLAN_PC 41 (fix 1): a pill label never leaves its pill. The USS lets the label wrap at spaces and the pill grow; for a single word wider than the pill (a long
    /// translation) the font is reduced in a controlled way (never below <c>minSize</c>) until its longest word fits. The text is never cut.
    /// </summary>
    internal static class DesktopPillFit
    {
        public static void Attach(Label label, float baseSize = 12f, float minSize = 9f)
        {
            if (label == null || label.parent == null) return;
            string lastText = null;
            float lastAvail = -1f;
            VisualElement host = label.parent;

            void Fit()
            {
                IResolvedStyle rs = label.resolvedStyle;
                float avail = host.contentRect.width - rs.marginLeft - rs.marginRight - rs.paddingLeft - rs.paddingRight - 1f;
                if (float.IsNaN(avail) || avail < 12f) return;
                if (label.text == lastText && Mathf.Abs(avail - lastAvail) < 0.5f) return;
                lastText = label.text;
                lastAvail = avail;

                float cur = label.resolvedStyle.fontSize;
                if (cur < 1f) return;
                float widest = 0f;
                foreach (string word in (label.text ?? string.Empty).Split(' ', '\n'))
                {
                    if (word.Length == 0) continue;
                    float w = label.MeasureTextSize(word, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined).x;
                    widest = Mathf.Max(widest, w * baseSize / cur);
                }
                float size = widest > avail ? Mathf.Max(minSize, Mathf.Floor(baseSize * avail / widest * 4f) / 4f) : baseSize;
                label.style.fontSize = Mathf.Approximately(size, baseSize) ? new StyleLength(StyleKeyword.Null) : new StyleLength(size);
            }

            label.RegisterCallback<GeometryChangedEvent>(_ => Fit());
            label.schedule.Execute(Fit).Every(150);
        }
    }
}
#endif
