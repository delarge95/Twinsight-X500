using UnityEngine;
using UnityEngine.UIElements;

namespace WebGL.UI.ProceduralIcons
{
    /// <summary>
    /// Abstraction interface for 2D vector drawing commands.
    /// Supports the full signatures including ArcDirection and FillRule.
    /// </summary>
    public interface IProceduralPainter
    {
        Color strokeColor { get; set; }
        Color fillColor { get; set; }
        float lineWidth { get; set; }

        void BeginPath();
        void MoveTo(Vector2 point);
        void LineTo(Vector2 point);
        
        void Arc(Vector2 center, float radius, float startAngle, float endAngle, ArcDirection direction = ArcDirection.Clockwise);
        
        void ClosePath();
        void Stroke();
        void Fill(FillRule fillRule = FillRule.NonZero);
    }
}
