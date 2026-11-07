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
            public Vector2 Centroid;
        }

        private static readonly Dictionary<string, List<Stroke>> Cache = new Dictionary<string, List<Stroke>>();

        // names used by older call sites
        private static readonly Dictionary<string, string> Alias = new Dictionary<string, string>
        {
            { "pin", "pins" }, { "iso", "isolate" }, { "tree", "parts" }, { "sidel", "panel_l" }, { "sider", "panel_r" },
            { "gear", "settings" }, { "render", "studio" }, { "env", "env_time" }, { "chev", "chevron" }, { "expl", "explode" },
            { "filt", "filter" }, { "swap", "invert" }
        };

        /// <summary>How often the tint is polled (ms). The PC build lowers it so USS colour transitions of the icons look smooth.</summary>
        public static int PollMs = 120;

        /// <summary>The PC build turns the hover / click micro-animations on (OutlineIconAnim.Specs); WebGL leaves them off.</summary>
        public static bool AnimateHover;

        /// <summary>Verification aid: when >= 0 the icon is drawn frozen at this animation phase (0..1), as a click when DebugClick.</summary>
        public float DebugPhase = -1f;
        public bool DebugClick;

        private string _shape;
        private Button _host;
        private float _animStart = -1f;
        private bool _animClick, _hovered;

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
            schedule.Execute(Refresh).Every(PollMs);
            RegisterCallback<AttachToPanelEvent>(OnAttachAnim);
            RegisterCallback<DetachFromPanelEvent>(OnDetachAnim);
        }

        private void OnAttachAnim(AttachToPanelEvent e)
        {
            OnDetachAnim(null);
            if (!AnimateHover) return;
            _host = this.GetFirstAncestorOfType<Button>();
            if (_host == null) return;
            _host.RegisterCallback<PointerEnterEvent>(OnHostEnter, TrickleDown.TrickleDown);
            _host.RegisterCallback<PointerLeaveEvent>(OnHostLeave, TrickleDown.TrickleDown);
            _host.RegisterCallback<PointerDownEvent>(OnHostDown, TrickleDown.TrickleDown);
        }

        private void OnDetachAnim(DetachFromPanelEvent e)
        {
            if (_host == null) return;
            _host.UnregisterCallback<PointerEnterEvent>(OnHostEnter, TrickleDown.TrickleDown);
            _host.UnregisterCallback<PointerLeaveEvent>(OnHostLeave, TrickleDown.TrickleDown);
            _host.UnregisterCallback<PointerDownEvent>(OnHostDown, TrickleDown.TrickleDown);
            _host = null;
        }

        private void OnHostEnter(PointerEnterEvent e) { _hovered = true; StartAnim(false); }
        private void OnHostLeave(PointerLeaveEvent e) { _hovered = false; }
        private void OnHostDown(PointerDownEvent e) { if (e.button == 0) StartAnim(true); }

        /// <summary>Plays the hover (or click) animation now (tests and the tutorial).</summary>
        public void PlayAnim(bool click) => StartAnim(click);

        private void StartAnim(bool click)
        {
            if (!AnimateHover || OutlineIconAnim.Get(_shape) == null) return;
            _animStart = Time.unscaledTime;
            _animClick = click;
            MarkDirtyRepaint();
        }

        private Color _lastColor;

        /// <summary>Repaints only when the tint changed.</summary>
        public void Refresh()
        {
            Color c = resolvedStyle.color;
            if (c != _lastColor) { _lastColor = c; MarkDirtyRepaint(); }
            if (_animStart >= 0f) MarkDirtyRepaint();
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

            AnimSpec spec = _animStart >= 0f && AnimateHover ? OutlineIconAnim.Get(_shape) : null;
            float ap = 0f, mul = 1f;
            if (DebugPhase >= 0f && AnimateHover)
            {
                spec = OutlineIconAnim.Get(_shape);
                ap = Mathf.Clamp01(DebugPhase);
                mul = DebugClick ? OutlineIconAnim.ClickMul : 1f;
            }
            else if (spec != null)
            {
                float dur = Mathf.Max(0.05f, spec.Ms * 0.001f * (_animClick ? OutlineIconAnim.ClickSpeed : 1f));
                float t = (Time.unscaledTime - _animStart) / dur;
                if (spec.Loop && _hovered && !_animClick) ap = t % 1f;
                else if (t >= 1f) { _animStart = -1f; spec = null; }
                else ap = t;
                mul = _animClick ? OutlineIconAnim.ClickMul : 1f;
            }

            if (spec != null && spec.Kind == AnimKind.Slice && strokes.Count > 1)
            {
                DrawSlice(p, strokes, spec, o, k, ap, mul, Mathf.Max(1f, Mathf.Min(r.width, r.height) / 24f));
                return;
            }

            Color baseColor = resolvedStyle.color;
            int index = 0;
            foreach (Stroke s0 in strokes)
            {
                Stroke s = s0;
                if (s.Points.Length < 2) { index++; continue; }
                float alpha = 1f;
                if (spec != null)
                {
                    OutlineIconAnim.Transform(spec, index, s.Centroid, ap, mul, out Vector2 off, out float rot, out float scx, out float scy, out float draw, out Vector2 pivot, out alpha);
                    var np = new Vector2[s.Points.Length];
                    float cr = Mathf.Cos(rot * Mathf.Deg2Rad), sr = Mathf.Sin(rot * Mathf.Deg2Rad);
                    for (int i = 0; i < np.Length; i++)
                    {
                        Vector2 d = s.Points[i] - pivot;
                        d = new Vector2(d.x * scx, d.y * scx * scy);
                        np[i] = pivot + new Vector2(d.x * cr - d.y * sr, d.x * sr + d.y * cr) + off;
                    }

                    s.Points = np;
                    if (draw < 0.999f)
                    {
                        if (draw <= 0.01f) { index++; continue; }
                        s.Points = ClipLength(np, s.Closed, draw);
                        s.Closed = false;
                    }
                }

                index++;
                Color sc = baseColor; sc.a *= alpha;
                p.strokeColor = sc;
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

        /// <summary>Keeps the first <paramref name="frac"/> of the polyline's length (a closed one is first opened).</summary>
        private static Vector2[] ClipLength(Vector2[] pts, bool closed, float frac)
        {
            var list = new List<Vector2>(pts);
            if (closed) list.Add(pts[0]);
            float total = 0f;
            for (int i = 1; i < list.Count; i++) total += Vector2.Distance(list[i - 1], list[i]);
            float target = total * Mathf.Clamp01(frac);
            var res = new List<Vector2> { list[0] };
            float acc = 0f;
            for (int i = 1; i < list.Count; i++)
            {
                float seg = Vector2.Distance(list[i - 1], list[i]);
                if (acc + seg >= target)
                {
                    float t = seg > 1e-5f ? (target - acc) / seg : 0f;
                    res.Add(Vector2.Lerp(list[i - 1], list[i], t));
                    return res.ToArray();
                }

                acc += seg;
                res.Add(list[i]);
            }

            return res.ToArray();
        }

        private static int SideOf(Vector2 q, Vector2 c0, Vector2 nrm) => Vector2.Dot(q - c0, nrm) >= 0f ? 1 : -1;

        /// <summary>
        /// Cut icon: the LAST stroke is the cut line. A scalpel (blade + handle) travels along it leaving a visible cut; every other stroke is
        /// split along the line and the two halves slide apart (a little along the cut, a little away from it) behind the blade, then close again.
        /// </summary>
        private void DrawSlice(Painter2D p, List<Stroke> strokes, AnimSpec spec, Vector2 o, float k, float ap, float mul, float lwScale)
        {
            Stroke line = strokes[strokes.Count - 1];
            Vector2 la = line.Points[0], lb = line.Points[line.Points.Length - 1];
            Vector2 c0 = (la + lb) * 0.5f;
            Vector2 dir = (lb - la).normalized;
            Vector2 nrm = new Vector2(-dir.y, dir.x);
            float half = (lb - la).magnitude * 0.5f;
            const float sweepEnd = 0.62f;
            float sp = Mathf.Clamp01(ap / sweepEnd);
            float tip = Mathf.Lerp(-half - 1.5f, half + 5f, OutlineIconAnim.Ease(sp));
            float gap = spec.Amp * mul * (1f - OutlineIconAnim.Ease((ap - sweepEnd) / (1f - sweepEnd)));
            Color col = resolvedStyle.color;
            p.strokeColor = col;

            for (int si = 0; si < strokes.Count - 1; si++)
            {
                Stroke s = strokes[si];
                var pts = new List<Vector2>(s.Points);
                if (s.Closed) pts.Add(s.Points[0]);
                var pieces = new List<List<Vector2>>();
                var sides = new List<int>();
                var piece = new List<Vector2> { pts[0] };
                int cur = SideOf(pts[0], c0, nrm);
                for (int j = 1; j < pts.Count; j++)
                {
                    Vector2 a = pts[j - 1], b = pts[j];
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 1.5f));
                    for (int st = 1; st <= steps; st++)
                    {
                        Vector2 q = Vector2.Lerp(a, b, st / (float)steps);
                        int qs = SideOf(q, c0, nrm);
                        if (qs != cur)
                        {
                            Vector2 prev = piece[piece.Count - 1];
                            float sprev = Vector2.Dot(prev - c0, nrm), sq = Vector2.Dot(q - c0, nrm);
                            float tt = Mathf.Abs(sprev - sq) > 1e-6f ? Mathf.Clamp01(sprev / (sprev - sq)) : 0.5f;
                            Vector2 x = Vector2.Lerp(prev, q, tt);
                            piece.Add(x);
                            pieces.Add(piece);
                            sides.Add(cur);
                            piece = new List<Vector2> { x };
                            cur = qs;
                        }

                        piece.Add(q);
                    }
                }

                pieces.Add(piece);
                sides.Add(cur);
                if (s.Closed && pieces.Count > 1 && sides[0] == sides[sides.Count - 1])
                {
                    List<Vector2> last = pieces[pieces.Count - 1];
                    for (int j = 1; j < pieces[0].Count; j++) last.Add(pieces[0][j]);
                    pieces.RemoveAt(0);
                    sides.RemoveAt(0);
                }

                for (int pi = 0; pi < pieces.Count; pi++)
                {
                    List<Vector2> pc = pieces[pi];
                    if (pc.Count < 2) continue;
                    float sd = sides[pi];
                    p.BeginPath();
                    for (int j = 0; j < pc.Count; j++)
                    {
                        float proj = Vector2.Dot(pc[j] - c0, dir);
                        float open = OutlineIconAnim.Ease((tip - proj + 0.6f) / 2.4f);
                        Vector2 pt = pc[j] + sd * gap * open * (nrm * 0.55f + dir * 0.9f);
                        if (j == 0) p.MoveTo(o + pt * k); else p.LineTo(o + pt * k);
                    }

                    if (pieces.Count == 1 && s.Closed) p.ClosePath();
                    p.Stroke();
                }
            }

            // the cut line, drawn behind the blade
            float lineEnd = Mathf.Min(tip, half);
            if (lineEnd > -half)
            {
                p.BeginPath();
                p.MoveTo(o + la * k);
                p.LineTo(o + (c0 + dir * lineEnd) * k);
                p.Stroke();
            }

            // the scalpel: the tip leads along the line, the blade edge rides on the cut, the handle trails up and back
            if (sp > 0.001f && sp < 0.999f)
            {
                float a = Mathf.Clamp01(sp * 7f) * Mathf.Clamp01((1f - sp) * 4f);
                float sc = 1f + 0.12f * (mul - 1f);
                Color bc = col; bc.a *= a;
                p.strokeColor = bc;
                p.lineWidth = 1.3f * lwScale;
                Vector2 tipPoint = c0 + dir * tip;

                // tool frame: tx runs back along the scalpel axis from the tip (up and back, tilted 18 deg from the normal of the cut),
                // ty is across it, positive on the cutting-edge side (towards the travel direction)
                const float alpha = 18f * Mathf.Deg2Rad;
                Vector2 wUp = -nrm;
                Vector2 axis = Mathf.Cos(alpha) * wUp - Mathf.Sin(alpha) * dir;
                Vector2 edge = Mathf.Cos(alpha) * dir + Mathf.Sin(alpha) * wUp;
                Vector2 U(float tx, float ty) => o + (tipPoint + (axis * tx + edge * ty) * sc) * k;
                p.BeginPath();                  // blade: pointed tip, convex belly on the cutting edge, straight spine
                p.MoveTo(U(0f, 0f));
                p.LineTo(U(2.2f, 1.6f));
                p.LineTo(U(5.4f, 1.5f));
                p.LineTo(U(5.4f, -1.2f));
                p.ClosePath();
                p.Stroke();
                p.BeginPath();                  // handle
                p.MoveTo(U(5.4f, 0.2f));
                p.LineTo(U(13.5f, 0.2f));
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
                if (pts != null && pts.Count > 1) { Vector2 c = Vector2.zero; foreach (Vector2 q in pts) c += q; c /= pts.Count; output.Add(new Stroke { Points = pts.ToArray(), Closed = closed, Dashed = dashed, Centroid = c }); }
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
