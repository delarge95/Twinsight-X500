using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace WebGL.UI.ProceduralIcons
{
    /// <summary>
    /// Implementation of IProceduralPainter that translates vector commands into a clean SVG document.
    /// Strictly uses CultureInfo.InvariantCulture to guarantee that floats are formatted with dots ('.')
    /// as decimal separators instead of commas (','), which breaks SVG spec.
    /// </summary>
    public class SvgPainterExporter : IProceduralPainter
    {
        private readonly StringBuilder _svg = new StringBuilder();
        private readonly StringBuilder _currentPath = new StringBuilder();
        private readonly float _width;
        private readonly float _height;

        public Color strokeColor { get; set; } = Color.white;
        public Color fillColor { get; set; } = Color.clear;
        public float lineWidth { get; set; } = 1.5f;

        public SvgPainterExporter(float width, float height)
        {
            _width = width;
            _height = height;
            
            string wStr = Str(width);
            string hStr = Str(height);
            _svg.AppendLine($"<svg width=\"{wStr}\" height=\"{hStr}\" viewBox=\"0 0 {wStr} {hStr}\" xmlns=\"http://www.w3.org/2000/svg\">");
        }

        public void BeginPath()
        {
            _currentPath.Clear();
        }

        public void MoveTo(Vector2 point)
        {
            _currentPath.Append($"M {Str(point.x)} {Str(point.y)} ");
        }

        public void LineTo(Vector2 point)
        {
            _currentPath.Append($"L {Str(point.x)} {Str(point.y)} ");
        }

        public void Arc(Vector2 center, float radius, float startAngle, float endAngle, ArcDirection direction = ArcDirection.Clockwise)
        {
            float angleDiff = Mathf.Abs(endAngle - startAngle);
            
            // Normalize angles to degrees for easy delta comparison if they are in radians
            // Unity angles in Painter2D.Arc are defined in radians, so PI is 180 degrees.
            if (angleDiff >= (2f * Mathf.PI - 0.05f) || angleDiff >= 359f * Mathf.Deg2Rad)
            {
                string strokeHex = GetHexColor(strokeColor);
                string fillHex = fillColor.a > 0 ? GetHexColor(fillColor) : "none";
                
                string cxStr = Str(center.x);
                string cyStr = Str(center.y);
                string rStr = Str(radius);
                string lwStr = Str(lineWidth);
                
                _svg.AppendLine($"  <circle cx=\"{cxStr}\" cy=\"{cyStr}\" r=\"{rStr}\" stroke=\"{strokeHex}\" stroke-width=\"{lwStr}\" fill=\"{fillHex}\" stroke-linecap=\"round\" stroke-linejoin=\"round\" />");
            }
            else
            {
                // In Unity, angles are in radians. 
                // startAngle is absolute start.
                // endAngle is absolute end.
                float startRad = startAngle;
                float endRad = endAngle;

                Vector2 startPoint = new Vector2(
                    center.x + radius * Mathf.Cos(startRad),
                    center.y + radius * Mathf.Sin(startRad)
                );

                Vector2 endPoint = new Vector2(
                    center.x + radius * Mathf.Cos(endRad),
                    center.y + radius * Mathf.Sin(endRad)
                );

                // large-arc-flag: 1 if the arc is greater than 180 degrees (PI radians)
                int largeArcFlag = angleDiff > Mathf.PI ? 1 : 0;
                
                // sweep-flag: 1 for clockwise (matches Unity's default ArcDirection.Clockwise)
                int sweepFlag = direction == ArcDirection.Clockwise ? 1 : 0;

                // Move to start of the arc if the path is empty
                if (_currentPath.Length == 0)
                {
                    _currentPath.Append($"M {Str(startPoint.x)} {Str(startPoint.y)} ");
                }
                else
                {
                    _currentPath.Append($"L {Str(startPoint.x)} {Str(startPoint.y)} ");
                }

                _currentPath.Append($"A {Str(radius)} {Str(radius)} 0 {largeArcFlag} {sweepFlag} {Str(endPoint.x)} {Str(endPoint.y)} ");
            }
        }

        public void ClosePath()
        {
            if (_currentPath.Length > 0)
            {
                _currentPath.Append("Z ");
            }
        }

        public void Stroke()
        {
            if (_currentPath.Length > 0)
            {
                string strokeHex = GetHexColor(strokeColor);
                string opacityAttr = strokeColor.a < 1f ? $" stroke-opacity=\"{Str(strokeColor.a)}\"" : "";
                string lwStr = Str(lineWidth);
                
                _svg.AppendLine($"  <path d=\"{_currentPath.ToString().Trim()}\" stroke=\"{strokeHex}\" stroke-width=\"{lwStr}\" fill=\"none\" stroke-linecap=\"round\" stroke-linejoin=\"round\"{opacityAttr} />");
            }
        }

        public void Fill(FillRule fillRule = FillRule.NonZero)
        {
            if (_currentPath.Length > 0)
            {
                string fillHex = GetHexColor(fillColor);
                string opacityAttr = fillColor.a < 1f ? $" fill-opacity=\"{Str(fillColor.a)}\"" : "";
                string ruleAttr = fillRule == FillRule.OddEven ? " fill-rule=\"evenodd\"" : "";
                
                _svg.AppendLine($"  <path d=\"{_currentPath.ToString().Trim()}\" fill=\"{fillHex}\"{opacityAttr}{ruleAttr} />");
            }
        }

        public string GetSvgString()
        {
            var finalSvg = new StringBuilder();
            finalSvg.Append(_svg.ToString());
            finalSvg.AppendLine("</svg>");
            return finalSvg.ToString();
        }

        /// <summary>
        /// Formats a float strictly using '.' as a decimal point, regardless of OS culture settings.
        /// </summary>
        private static string Str(float val)
        {
            return val.ToString("G", CultureInfo.InvariantCulture);
        }

        private static string GetHexColor(Color color)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }
    }
}
