#if UNITY_STANDALONE
using UnityEngine;
using UnityEngine.UIElements;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PC only (phase 4). Small stroked vector icons drawn with Painter2D on a 24x24 grid, tinted with the
    /// element's USS <c>color</c> so hover/active states of the chrome recolour them (celeste accent).
    /// </summary>
    internal sealed class DesktopVectorIcon : VisualElement
    {
        private readonly string _shape;

        public DesktopVectorIcon(string shape, float size = 16f)
        {
            _shape = shape;
            pickingMode = PickingMode.Ignore;
            AddToClassList("dk-icon");
            style.width = size;
            style.height = size;
            style.minWidth = size;
            style.flexShrink = 0;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext ctx)
        {
            Rect r = contentRect;
            if (r.width < 2f || r.height < 2f) return;

            float k = Mathf.Min(r.width, r.height) / 24f;
            Vector2 o = new Vector2(r.x + (r.width - 24f * k) * 0.5f, r.y + (r.height - 24f * k) * 0.5f);
            Vector2 P(float x, float y) => o + new Vector2(x, y) * k;

            Painter2D p = ctx.painter2D;
            p.strokeColor = resolvedStyle.color;
            p.lineWidth = Mathf.Max(1.2f, 1.7f * k);
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;

            void Line(params float[] c)
            {
                p.BeginPath();
                p.MoveTo(P(c[0], c[1]));
                for (int i = 2; i + 1 < c.Length; i += 2) p.LineTo(P(c[i], c[i + 1]));
                p.Stroke();
            }

            void Circle(float cx, float cy, float rad)
            {
                p.BeginPath();
                p.Arc(P(cx, cy), rad * k, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Stroke();
            }

            switch (_shape)
            {
                case "home": Line(3, 11, 12, 4, 21, 11); Line(5, 10, 5, 20, 19, 20, 19, 10); break;
                case "pin": Circle(12, 10, 5); Line(12, 15, 12, 21); Circle(12, 10, 1.6f); break;
                case "iso": Line(4, 9, 4, 5, 8, 5); Line(20, 9, 20, 5, 16, 5); Line(4, 15, 4, 19, 8, 19); Line(20, 15, 20, 19, 16, 19); Circle(12, 12, 1.6f); break;
                case "bolt": Line(13, 3, 5, 14, 11, 14, 10, 21, 18, 10, 12, 10, 13, 3); break;
                case "cut": Line(3, 15, 21, 9); Line(5, 8, 9, 15); Line(15, 6, 19, 13); break;
                case "expl": Line(12, 3, 12, 8); Line(12, 16, 12, 21); Line(3, 12, 8, 12); Line(16, 12, 21, 12); Line(9, 9, 15, 9, 15, 15, 9, 15, 9, 9); break;
                case "filt": Line(4, 5, 20, 5, 14, 13, 14, 19, 10, 17, 10, 13, 4, 5); break;
                case "reset":
                    p.BeginPath(); p.Arc(P(12, 12), 8f * k, Angle.Degrees(-40f), Angle.Degrees(290f)); p.Stroke();
                    Line(20, 4, 20, 9, 15, 9);
                    break;
                case "tree": Line(5, 6, 19, 6); Line(9, 11, 19, 11); Line(9, 16, 19, 16); Line(13, 21, 19, 21); Line(5, 11, 5, 16, 9, 16); break;
                case "search": Circle(11, 11, 6); Line(16, 16, 20, 20); break;
                case "sidel": Line(4, 5, 20, 5, 20, 19, 4, 19, 4, 5); Line(9, 5, 9, 19); break;
                case "sider": Line(4, 5, 20, 5, 20, 19, 4, 19, 4, 5); Line(15, 5, 15, 19); break;
                case "gear":
                    Circle(12, 12, 3.2f);
                    Line(12, 3, 12, 6); Line(12, 18, 12, 21); Line(3, 12, 6, 12); Line(18, 12, 21, 12);
                    Line(5.6f, 5.6f, 7.7f, 7.7f); Line(16.3f, 16.3f, 18.4f, 18.4f); Line(18.4f, 5.6f, 16.3f, 7.7f); Line(7.7f, 16.3f, 5.6f, 18.4f);
                    break;
                case "close": Line(6, 6, 18, 18); Line(18, 6, 6, 18); break;
                case "render": Line(12, 3, 20, 7.5f, 20, 16.5f, 12, 21, 4, 16.5f, 4, 7.5f, 12, 3); Line(12, 12, 20, 7.5f); Line(12, 12, 12, 21); Line(12, 12, 4, 7.5f); break;
                case "env": Circle(12, 12, 4); Line(12, 2, 12, 4); Line(12, 20, 12, 22); Line(2, 12, 4, 12); Line(20, 12, 22, 12); break;
                case "swap": Line(4, 9, 18, 9, 15, 6); Line(20, 15, 6, 15, 9, 18); break;
                case "focus": Circle(12, 12, 3); Line(12, 3, 12, 6); Line(12, 18, 12, 21); Line(3, 12, 6, 12); Line(18, 12, 21, 12); break;
                case "chev": Line(9, 6, 15, 12, 9, 18); break;
                case "help": Circle(12, 12, 9); Line(9.5f, 9.5f, 10, 8, 12, 7.5f, 14, 8.2f, 14.5f, 10, 12, 12, 12, 13.5f); Line(12, 16.6f, 12, 16.8f); break;
            }
        }
    }
}
#endif
