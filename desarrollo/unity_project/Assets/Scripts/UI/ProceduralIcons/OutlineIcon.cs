using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace WebGL.UI.ProceduralIcons
{
    /// <summary>
    /// The single icon set of the approved sheet (docs/unity_pc/maqueta_iconos), shared by PC, WebGL and mobile: SVG path data on a 24 grid
    /// (<see cref="OutlineIconData"/>) flattened to polylines and stroked with Painter2D: 1.5 px stroke (scaled above 24 px),
    /// round caps and joins, no fill, no animation. Tinted with the USS <c>color</c> (rest / active / light background), no hover dependence.
    /// Use it in UXML as <c>&lt;pi:OutlineIcon shape="pins" /&gt;</c>.
    /// </summary>
    [UxmlElement]
    public partial class OutlineIcon : VisualElement
    {
        private struct Stroke
        {
            public Vector2[] Points;
            public bool Closed;
            public bool Dashed;
        }

        private static readonly Dictionary<string, List<Stroke>> Cache = new Dictionary<string, List<Stroke>>();

        // names used by older call sites
        private static readonly Dictionary<string, string> Alias = new Dictionary<string, string>
        {
            { "pin", "pins" }, { "iso", "isolate" }, { "tree", "parts" }, { "sidel", "panel_l" }, { "sider", "panel_r" },
            { "gear", "settings" }, { "render", "studio" }, { "env", "env_time" }, { "chev", "chevron" }, { "expl", "explode" },
            { "filt", "filter" }, { "swap", "invert" }
        };

        private string _shape;

        [UxmlAttribute("shape")]
        public string Shape
        {
            get => _shape;
            set { if (_shape == value) return; _shape = value; MarkDirtyRepaint(); }
        }

        public OutlineIcon()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("outline-icon");
            AddToClassList("dk-icon");
            style.flexShrink = 0;
            generateVisualContent += Draw;
            // repaint when the USS tint changes (active state, light background): cheap poll, no hover dependence
            schedule.Execute(Refresh).Every(120);
        }

        private Color _lastColor;

        /// <summary>Repaints only when the tint changed.</summary>
        public void Refresh()
        {
            Color c = resolvedStyle.color;
            if (c != _lastColor) { _lastColor = c; MarkDirtyRepaint(); }
        }

        public void SetShape(string shape) => Shape = shape;

        /// <summary>Replaces Unity's default Foldout triangle of every sheet foldout with the set's chevron (rotates when open).</summary>
        public static void ReplaceFoldoutArrows(VisualElement root)
        {
            if (root == null) return;
            root.Query<Foldout>().ForEach(f =>
            {
                if (f.ClassListContains("foldout-chev-done")) return;
                VisualElement input = f.Q<VisualElement>(className: "unity-toggle__input");
                if (input == null) return;
                f.AddToClassList("foldout-chev-done");
                var chev = new OutlineIcon { Shape = "chevron" };
                chev.AddToClassList("foldout-chev");
                chev.EnableInClassList("foldout-chev--open", f.value);
                input.Insert(0, chev);
                f.RegisterValueChangedCallback(e => chev.EnableInClassList("foldout-chev--open", e.newValue));
            });
        }

        private static List<Stroke> Get(string shape)
        {
            if (shape == null) return null;
            if (Alias.TryGetValue(shape, out string real)) shape = real;
            if (Cache.TryGetValue(shape, out List<Stroke> cached)) return cached;

            List<Stroke> list = null;
            if (OutlineIconData.Paths.TryGetValue(shape, out string[] paths))
            {
                list = new List<Stroke>();
                foreach (string p in paths)
                {
                    bool dashed = p.Length > 0 && p[0] == '~';
                    Parse(dashed ? p.Substring(1) : p, dashed, list);
                }
            }

            Cache[shape] = list;
            return list;
        }

        private void Draw(MeshGenerationContext ctx)
        {
            Rect r = contentRect;
            if (r.width < 2f || r.height < 2f) return;

            List<Stroke> strokes = Get(_shape);
            if (strokes == null) return;

            float k = Mathf.Min(r.width, r.height) / 24f;
            Vector2 o = new Vector2(r.x + (r.width - 24f * k) * 0.5f, r.y + (r.height - 24f * k) * 0.5f);

            Painter2D p = ctx.painter2D;
            p.strokeColor = resolvedStyle.color;
            p.lineWidth = 1.5f * Mathf.Max(1f, Mathf.Min(r.width, r.height) / 24f);
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;

            foreach (Stroke s in strokes)
            {
                if (s.Points.Length < 2) continue;
                if (s.Dashed)
                {
                    DrawDashed(p, s, o, k);
                    continue;
                }

                p.BeginPath();
                p.MoveTo(o + s.Points[0] * k);
                for (int i = 1; i < s.Points.Length; i++) p.LineTo(o + s.Points[i] * k);
                if (s.Closed) p.ClosePath();
                p.Stroke();
            }
        }

        private static void DrawDashed(Painter2D p, Stroke s, Vector2 o, float k)
        {
            const float on = 2f, off = 2.4f;
            float phase = 0f;
            bool drawing = true;
            Vector2 prev = s.Points[0];
            bool open = false;
            int n = s.Points.Length + (s.Closed ? 1 : 0);
            for (int i = 1; i < n; i++)
            {
                Vector2 cur = s.Points[i % s.Points.Length];
                float len = Vector2.Distance(prev, cur);
                float pos = 0f;
                while (pos < len)
                {
                    float limit = drawing ? on : off;
                    float step = Mathf.Min(limit - phase, len - pos);
                    Vector2 a = Vector2.Lerp(prev, cur, pos / len);
                    Vector2 b = Vector2.Lerp(prev, cur, (pos + step) / len);
                    if (drawing)
                    {
                        if (!open) { p.BeginPath(); p.MoveTo(o + a * k); open = true; }
                        p.LineTo(o + b * k);
                    }

                    pos += step;
                    phase += step;
                    if (phase >= limit - 1e-4f)
                    {
                        if (drawing && open) { p.Stroke(); open = false; }
                        drawing = !drawing;
                        phase = 0f;
                    }
                }

                prev = cur;
            }

            if (open) p.Stroke();
        }

        // ───────────────────────── SVG path parsing (M L H V C S A Z, absolute and relative) ─────────────────────────

        private static void Parse(string d, bool dashed, List<Stroke> output)
        {
            int i = 0;
            char cmd = ' ';
            Vector2 cur = Vector2.zero, start = Vector2.zero, lastCtrl = Vector2.zero;
            char prevCmd = ' ';
            List<Vector2> pts = null;

            void Flush(bool closed)
            {
                if (pts != null && pts.Count > 1) output.Add(new Stroke { Points = pts.ToArray(), Closed = closed, Dashed = dashed });
                pts = null;
            }

            while (true)
            {
                SkipSep(d, ref i);
                if (i >= d.Length) break;
                if (char.IsLetter(d[i])) { cmd = d[i]; i++; }
                char up = char.ToUpperInvariant(cmd);
                bool rel = cmd != up;

                if (up == 'Z')
                {
                    Flush(true);
                    cur = start;
                    prevCmd = 'Z';
                    continue;
                }

                if (up == 'M')
                {
                    Flush(false);
                    float x = Num(d, ref i), y = Num(d, ref i);
                    cur = rel ? cur + new Vector2(x, y) : new Vector2(x, y);
                    start = cur;
                    pts = new List<Vector2> { cur };
                    cmd = rel ? 'l' : 'L';
                    prevCmd = 'M';
                    continue;
                }

                if (pts == null) pts = new List<Vector2> { cur };

                switch (up)
                {
                    case 'L':
                    {
                        float x = Num(d, ref i), y = Num(d, ref i);
                        cur = rel ? cur + new Vector2(x, y) : new Vector2(x, y);
                        pts.Add(cur);
                        break;
                    }
                    case 'H':
                    {
                        float x = Num(d, ref i);
                        cur = new Vector2(rel ? cur.x + x : x, cur.y);
                        pts.Add(cur);
                        break;
                    }
                    case 'V':
                    {
                        float y = Num(d, ref i);
                        cur = new Vector2(cur.x, rel ? cur.y + y : y);
                        pts.Add(cur);
                        break;
                    }
                    case 'C':
                    case 'S':
                    {
                        Vector2 c1;
                        if (up == 'C')
                        {
                            float x1 = Num(d, ref i), y1 = Num(d, ref i);
                            c1 = rel ? cur + new Vector2(x1, y1) : new Vector2(x1, y1);
                        }
                        else
                        {
                            c1 = (prevCmd == 'C' || prevCmd == 'S') ? 2f * cur - lastCtrl : cur;
                        }

                        float x2 = Num(d, ref i), y2 = Num(d, ref i), x = Num(d, ref i), y = Num(d, ref i);
                        Vector2 c2 = rel ? cur + new Vector2(x2, y2) : new Vector2(x2, y2);
                        Vector2 e = rel ? cur + new Vector2(x, y) : new Vector2(x, y);
                        for (int s = 1; s <= 14; s++)
                        {
                            float t = s / 14f, u = 1f - t;
                            pts.Add(u * u * u * cur + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * e);
                        }

                        lastCtrl = c2;
                        cur = e;
                        break;
                    }
                    case 'A':
                    {
                        float rx = Num(d, ref i), ry = Num(d, ref i), rot = Num(d, ref i);
                        float fa = Num(d, ref i), fs = Num(d, ref i), x = Num(d, ref i), y = Num(d, ref i);
                        Vector2 e = rel ? cur + new Vector2(x, y) : new Vector2(x, y);
                        Arc(pts, cur, e, rx, ry, rot, fa != 0f, fs != 0f);
                        cur = e;
                        break;
                    }
                    default:
                        Flush(false);
                        return;
                }

                prevCmd = up;
            }

            Flush(false);
        }

        private static void SkipSep(string d, ref int i)
        {
            while (i < d.Length && (d[i] == ' ' || d[i] == ',' || d[i] == '\n' || d[i] == '\t')) i++;
        }

        private static float Num(string d, ref int i)
        {
            SkipSep(d, ref i);
            int s = i;
            if (i < d.Length && (d[i] == '-' || d[i] == '+')) i++;
            bool dot = false;
            while (i < d.Length)
            {
                char c = d[i];
                if (c >= '0' && c <= '9') { i++; continue; }
                if (c == '.' && !dot) { dot = true; i++; continue; }
                break;
            }

            return float.Parse(d.Substring(s, i - s), CultureInfo.InvariantCulture);
        }

        // SVG endpoint arc -> centre parametrisation, flattened (W3C implementation notes F.6.5).
        private static void Arc(List<Vector2> pts, Vector2 p0, Vector2 p1, float rx, float ry, float rotDeg, bool large, bool sweep)
        {
            if (rx == 0f || ry == 0f || p0 == p1) { pts.Add(p1); return; }

            rx = Mathf.Abs(rx);
            ry = Mathf.Abs(ry);
            float phi = rotDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(phi), sin = Mathf.Sin(phi);
            Vector2 dp = (p0 - p1) * 0.5f;
            float x1 = cos * dp.x + sin * dp.y;
            float y1 = -sin * dp.x + cos * dp.y;
            float lambda = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);
            if (lambda > 1f) { float sq = Mathf.Sqrt(lambda); rx *= sq; ry *= sq; }

            float num = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
            float den = rx * rx * y1 * y1 + ry * ry * x1 * x1;
            float co = Mathf.Sqrt(Mathf.Max(0f, num / den));
            if (large == sweep) co = -co;
            float cxp = co * rx * y1 / ry;
            float cyp = -co * ry * x1 / rx;
            Vector2 c = new Vector2(cos * cxp - sin * cyp + (p0.x + p1.x) * 0.5f, sin * cxp + cos * cyp + (p0.y + p1.y) * 0.5f);

            float Ang(float ux, float uy, float vx, float vy) => Mathf.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);

            float th1 = Ang(1f, 0f, (x1 - cxp) / rx, (y1 - cyp) / ry);
            float dth = Ang((x1 - cxp) / rx, (y1 - cyp) / ry, (-x1 - cxp) / rx, (-y1 - cyp) / ry);
            if (!sweep && dth > 0f) dth -= 2f * Mathf.PI;
            else if (sweep && dth < 0f) dth += 2f * Mathf.PI;

            int steps = Mathf.Max(4, Mathf.CeilToInt(Mathf.Abs(dth) / (Mathf.PI / 18f)));
            for (int s = 1; s <= steps; s++)
            {
                float a = th1 + dth * s / steps;
                float ex = rx * Mathf.Cos(a), ey = ry * Mathf.Sin(a);
                pts.Add(new Vector2(cos * ex - sin * ey + c.x, sin * ex + cos * ey + c.y));
            }

            pts[pts.Count - 1] = p1;
        }
    }
}
