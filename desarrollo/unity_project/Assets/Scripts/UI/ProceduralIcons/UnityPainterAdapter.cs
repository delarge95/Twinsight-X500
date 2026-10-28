using UnityEngine;
using UnityEngine.UIElements;

namespace WebGL.UI.ProceduralIcons
{
    /// <summary>
    /// Wrapper adapter to redirect drawing commands to the real UnityEngine.UIElements.Painter2D
    /// </summary>
    public class UnityPainterAdapter : IProceduralPainter
    {
        private readonly Painter2D _painter;

        public UnityPainterAdapter(Painter2D painter)
        {
            _painter = painter;
        }

        public Color strokeColor
        {
            get => _painter.strokeColor;
            set => _painter.strokeColor = value;
        }

        public Color fillColor
        {
            get => _painter.fillColor;
            set => _painter.fillColor = value;
        }

        public float lineWidth
        {
            get => _painter.lineWidth;
            set => _painter.lineWidth = value;
        }

        public void BeginPath() => _painter.BeginPath();
        public void MoveTo(Vector2 point) => _painter.MoveTo(point);
        public void LineTo(Vector2 point) => _painter.LineTo(point);
        public void Arc(Vector2 center, float radius, float startAngle, float endAngle, ArcDirection direction = ArcDirection.Clockwise) => _painter.Arc(center, radius, startAngle, endAngle, direction);
        public void ClosePath() => _painter.ClosePath();
        public void Stroke() => _painter.Stroke();
        public void Fill(FillRule fillRule = FillRule.NonZero) => _painter.Fill(fillRule);
    }
}
