#if UNITY_STANDALONE
using System;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PC only. The looping illustration of each tutorial step, drawn with Painter2D (no video, no assets): a wireframe drone and a
    /// cursor / mouse / key glyph that act out the control the step explains. Every step has 3-4 phases of 2.6 s that match the rows of the
    /// step (the overlay highlights the row of the current phase). Painter2D draws the shapes; the key caps and the caption are small
    /// labels placed over it. It only repaints while the tutorial is open and not at all with "Reduce animations" (one still frame).
    /// </summary>
    internal sealed class TutorialAnim : VisualElement
    {
        private const float W = 210f, H = 230f, PhaseSeconds = 2.6f;
        private static readonly int[] PhaseCount = { 3, 3, 4, 3, 3, 3 };

        private static readonly Color C2 = new Color(0.749f, 0.918f, 1f, 1f);
        private static readonly Color Cel = new Color(0.275f, 0.686f, 1f, 1f);
        private static readonly Color Bg = new Color(0.055f, 0.067f, 0.086f, 1f);
        private static readonly Color Mute = new Color(1f, 1f, 1f, 0.38f);
        private static readonly Color[] HeatStops =
        {
            new Color(0.16f, 0.29f, 0.84f), new Color(0.13f, 0.72f, 0.81f), new Color(0.48f, 0.85f, 0.34f),
            new Color(0.96f, 0.85f, 0.04f), new Color(1f, 0.42f, 0.17f), new Color(0.84f, 0.14f, 0.49f)
        };

        public Action<int> PhaseChanged;

        private int _step;
        private int _phase = -1;
        private float _t0;
        private bool _play;
        private float _k = 1f, _ox, _oy;
        private readonly Label _cap;
        private readonly Label[] _keys = new Label[3];
        private readonly IVisualElementScheduledItem _tick;

        // per-draw state
        private float _t;
        private int _ph;
        private float _time;

        public TutorialAnim()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("dk-tut-anim");
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => { UpdateGeo(); PlaceTags(); });

            _cap = new Label { pickingMode = PickingMode.Ignore };
            _cap.AddToClassList("dk-tut-anim-cap");
            Add(_cap);
            for (int i = 0; i < _keys.Length; i++)
            {
                _keys[i] = new Label { pickingMode = PickingMode.Ignore };
                _keys[i].AddToClassList("dk-tut-anim-key");
                _keys[i].style.display = DisplayStyle.None;
                Add(_keys[i]);
            }

            _tick = schedule.Execute(Frame).Every(16);
            _tick.Pause();
            DesktopMotion.Changed += () => MarkDirtyRepaint();
        }

        public void SetStep(int step)
        {
            _step = Mathf.Clamp(step, 0, PhaseCount.Length - 1);
            _t0 = Time.unscaledTime;
            _phase = -1;
            MarkDirtyRepaint();
            Frame();
        }

        public void Play(bool on)
        {
            _play = on;
            if (on) { _t0 = Time.unscaledTime; _tick.Resume(); }
            else _tick.Pause();
            MarkDirtyRepaint();
        }

        private static bool Es => AppLanguageManager.IsSpanish;

        private void Frame()
        {
            float time = DesktopMotion.Reduce ? 0.55f * PhaseSeconds : Time.unscaledTime - _t0;
            int count = PhaseCount[_step];
            int ph = DesktopMotion.Reduce ? 0 : Mathf.FloorToInt(time / PhaseSeconds) % count;
            if (ph != _phase)
            {
                _phase = ph;
                PlaceTags();
                PhaseChanged?.Invoke(ph);
                for (int i = 0; i < _keys.Length; i++)
                {
                    if (_keys[i].style.display == DisplayStyle.Flex) DesktopMotion.Flash(_keys[i], "dk-press-sim", 360);
                }
            }

            if (!DesktopMotion.Reduce) MarkDirtyRepaint();
        }

        private void UpdateGeo()
        {
            Rect r = contentRect;
            if (r.width < 4f || r.height < 4f) return;
            _k = Mathf.Min(r.width / W, r.height / H);
            _ox = r.x + (r.width - W * _k) * 0.5f;
            _oy = r.y + (r.height - H * _k) * 0.5f;
        }

        // ───────────────────────── labels (key caps, caption) ─────────────────────────

        private void Key(int i, string text, float x, float y)
        {
            Label l = _keys[i];
            if (string.IsNullOrEmpty(text)) { l.style.display = DisplayStyle.None; return; }
            l.style.display = DisplayStyle.Flex;
            l.text = text;
            l.style.left = _ox + x * _k;
            l.style.top = _oy + y * _k;
        }

        private void PlaceTags()
        {
            int s = _step, ph = Mathf.Max(0, _phase);
            string cap = "";
            for (int i = 0; i < _keys.Length; i++) _keys[i].style.display = DisplayStyle.None;
            switch (s)
            {
                case 0:
                    if (ph == 0) { cap = Es ? "Arrastra el fondo para orbitar" : "Drag the background to orbit"; }
                    else if (ph == 1) { cap = Es ? "Shift + clic derecho: desplazar" : "Shift + right drag: pan"; Key(0, "Shift", 8, 190); }
                    else { cap = Es ? "Rueda: zoom hacia el cursor" : "Wheel: zoom to the cursor"; }
                    break;
                case 1:
                    if (ph == 0) cap = Es ? "Clic: seleccionar la pieza" : "Click: select the part";
                    else if (ph == 1) { cap = Es ? "Doble clic: enfocar" : "Double click: focus"; Key(0, "F", 8, 190); }
                    else { cap = Es ? "Clic en el fondo: soltar" : "Click the background: deselect"; Key(0, "Esc", 8, 190); }
                    break;
                case 2:
                    Key(0, "1", 30, 17); Key(1, "2", 90, 17); Key(2, "3", 150, 17);
                    cap = ph == 0 ? (Es ? "1 Inspeccionar: puntos" : "1 Inspect: pins")
                        : ph == 1 ? (Es ? "2 Analizar: corte" : "2 Analyze: cut")
                        : ph == 2 ? (Es ? "3 Estudio: render" : "3 Studio: render")
                        : (Es ? "I: aislar la pieza" : "I: isolate the part");
                    if (ph == 3) { Key(0, "", 0, 0); Key(1, "", 0, 0); Key(2, "", 0, 0); Key(0, "I", 8, 190); }
                    break;
                case 3:
                    cap = ph == 0 ? (Es ? "C: corte transversal" : "C: cross-section")
                        : ph == 1 ? (Es ? "E: vista explosionada" : "E: exploded view")
                        : (Es ? "G: filtrar por categoría" : "G: filter by category");
                    Key(0, ph == 0 ? "C" : ph == 1 ? "E" : "G", 8, 190);
                    break;
                case 4:
                    cap = ph == 0 ? (Es ? "V: cambia el modo de render" : "V: cycle the render mode")
                        : ph == 1 ? (Es ? "Entorno: estudio, tiempo, color" : "Environment: studio, time, color")
                        : (Es ? "Térmico: la leyenda aparece" : "Thermal: the legend appears");
                    if (ph == 0) Key(0, "V", 8, 190);
                    break;
                default:
                    cap = ph == 0 ? (Es ? "T: árbol de piezas" : "T: parts tree")
                        : ph == 1 ? (Es ? "Ajustes: pantalla y calidad" : "Settings: display and quality")
                        : (Es ? "?: todos los atajos" : "?: every shortcut");
                    if (ph == 0) Key(0, "T", 8, 190);
                    if (ph == 2) Key(0, "?", 8, 190);
                    break;
            }

            _cap.text = cap;
        }

        // ───────────────────────── drawing helpers ─────────────────────────

        private static float E(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        private static float Seg(float t, float a, float b) => E((t - a) / (b - a));
        private Vector2 Pt(float x, float y) => new Vector2(_ox + x * _k, _oy + y * _k);

        private static Color Heat(float f)
        {
            f = Mathf.Clamp01(f) * (HeatStops.Length - 1);
            int i = Mathf.Min((int)f, HeatStops.Length - 2);
            return Color.Lerp(HeatStops[i], HeatStops[i + 1], f - i);
        }

        private static Color A(Color c, float a) { c.a *= a; return c; }

        private void Line(Painter2D p, Vector2 a, Vector2 b, Color c, float w = 1.5f)
        {
            p.strokeColor = c; p.lineWidth = w * Mathf.Max(0.8f, _k);
            p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke();
        }

        private void Dash(Painter2D p, Vector2 a, Vector2 b, Color c)
        {
            float len = Vector2.Distance(a, b);
            float on = 4f * _k, off = 3f * _k;
            p.strokeColor = c; p.lineWidth = 1.4f * Mathf.Max(0.8f, _k);
            for (float d = 0f; d < len; d += on + off)
            {
                p.BeginPath();
                p.MoveTo(Vector2.Lerp(a, b, d / len));
                p.LineTo(Vector2.Lerp(a, b, Mathf.Min(len, d + on) / len));
                p.Stroke();
            }
        }

        private void Poly(Painter2D p, Vector2[] pts, int n, Color stroke, Color fill, bool closed = true, bool dashed = false)
        {
            if (fill.a > 0.001f)
            {
                p.fillColor = fill; p.BeginPath(); p.MoveTo(pts[0]);
                for (int i = 1; i < n; i++) p.LineTo(pts[i]);
                p.ClosePath(); p.Fill();
            }

            if (dashed)
            {
                for (int i = 0; i < n - 1; i++) Dash(p, pts[i], pts[i + 1], stroke);
                if (closed) Dash(p, pts[n - 1], pts[0], stroke);
                return;
            }

            p.strokeColor = stroke; p.lineWidth = 1.5f * Mathf.Max(0.8f, _k);
            p.BeginPath(); p.MoveTo(pts[0]);
            for (int i = 1; i < n; i++) p.LineTo(pts[i]);
            if (closed) p.ClosePath();
            p.Stroke();
        }

        private void Circle(Painter2D p, Vector2 c, float r, Color stroke, Color fill, float w = 1.5f)
        {
            if (fill.a > 0.001f) { p.fillColor = fill; p.BeginPath(); p.Arc(c, r * _k, Angle.Degrees(0f), Angle.Degrees(360f)); p.Fill(); }
            if (stroke.a > 0.001f)
            {
                p.strokeColor = stroke; p.lineWidth = w * Mathf.Max(0.8f, _k);
                p.BeginPath(); p.Arc(c, r * _k, Angle.Degrees(0f), Angle.Degrees(360f)); p.Stroke();
            }
        }

        private void RoundRect(Painter2D p, float x, float y, float w, float h, float r, Color stroke, Color fill)
        {
            r = Mathf.Min(r, Mathf.Min(w, h) * 0.5f);
            p.BeginPath();
            p.MoveTo(Pt(x + r, y));
            p.LineTo(Pt(x + w - r, y)); p.Arc(Pt(x + w - r, y + r), r * _k, Angle.Degrees(-90f), Angle.Degrees(0f));
            p.LineTo(Pt(x + w, y + h - r)); p.Arc(Pt(x + w - r, y + h - r), r * _k, Angle.Degrees(0f), Angle.Degrees(90f));
            p.LineTo(Pt(x + r, y + h)); p.Arc(Pt(x + r, y + h - r), r * _k, Angle.Degrees(90f), Angle.Degrees(180f));
            p.LineTo(Pt(x, y + r)); p.Arc(Pt(x + r, y + r), r * _k, Angle.Degrees(180f), Angle.Degrees(270f));
            p.ClosePath();
            if (fill.a > 0.001f) { p.fillColor = fill; p.Fill(); }
            if (stroke.a > 0.001f) { p.strokeColor = stroke; p.lineWidth = 1.5f * Mathf.Max(0.8f, _k); p.Stroke(); }
        }

        private void Ripple(Painter2D p, Vector2 c, float t)
        {
            if (t <= 0f || t >= 1f) return;
            Circle(p, c, 4f + 24f * E(t), A(C2, 1f - t), Color.clear, 2f);
        }

        private void Cursor(Painter2D p, Vector2 at, float press)
        {
            float s = _k * (1f - 0.12f * press);
            Vector2[] pts =
            {
                at, at + new Vector2(0f, 17f) * s, at + new Vector2(4.5f, 13.2f) * s, at + new Vector2(8f, 20.5f) * s,
                at + new Vector2(11f, 19f) * s, at + new Vector2(7.6f, 12f) * s, at + new Vector2(13f, 12f) * s
            };
            p.fillColor = new Color(1f, 1f, 1f, 0.96f);
            p.BeginPath(); p.MoveTo(pts[0]);
            for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]);
            p.ClosePath(); p.Fill();
            p.strokeColor = Bg; p.lineWidth = 1.2f; p.lineJoin = LineJoin.Round;
            p.BeginPath(); p.MoveTo(pts[0]);
            for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]);
            p.ClosePath(); p.Stroke();
        }

        /// <summary>Mouse glyph: button 0 = left, 1 = right, 2 = wheel; pressed fills it celeste.</summary>
        private void Mouse(Painter2D p, float x, float y, int button, float press, float wheelOffset = 0f)
        {
            RoundRect(p, x, y, 22f, 34f, 10f, A(C2, 0.9f), new Color(0.09f, 0.11f, 0.14f, 1f));
            Line(p, Pt(x + 11f, y), Pt(x + 11f, y + 13f), A(C2, 0.5f), 1.2f);
            Line(p, Pt(x, y + 13f), Pt(x + 22f, y + 13f), A(C2, 0.5f), 1.2f);
            if (button == 0 && press > 0f) FillQuad(p, x + 1f, y + 1f, 9.5f, 11.5f, A(Cel, press));
            if (button == 1 && press > 0f) FillQuad(p, x + 11.5f, y + 1f, 9.5f, 11.5f, A(Cel, press));
            RoundRect(p, x + 9.2f, y + 3.5f + wheelOffset, 3.6f, 6f, 1.8f, A(C2, 0.9f), button == 2 ? A(Cel, press) : Color.clear);
        }

        private void FillQuad(Painter2D p, float x, float y, float w, float h, Color c)
        {
            p.fillColor = c;
            p.BeginPath(); p.MoveTo(Pt(x, y)); p.LineTo(Pt(x + w, y)); p.LineTo(Pt(x + w, y + h)); p.LineTo(Pt(x, y + h)); p.ClosePath(); p.Fill();
        }

        // the drone: groups 0 body, 1 arms, 2 props (3 motors share the arm colour). style 0 outline, 1 solid, 2 x-ray, 3 thermal.
        private Vector2 DronePt(float cx, float cy, float sc, float yaw, float x, float z, float y)
        {
            float cs = Mathf.Cos(yaw), sn = Mathf.Sin(yaw);
            float xr = x * cs - z * sn, zr = x * sn + z * cs;
            return Pt(cx + xr * sc, cy + zr * sc * 0.42f + y * sc * 0.9f);
        }

        private static readonly Vector2[] Motors = { new Vector2(-40f, -28f), new Vector2(40f, -28f), new Vector2(40f, 28f), new Vector2(-40f, 28f) };

        private Vector2 MotorScreen(float cx, float cy, float sc, float yaw, int i, float expl = 0f)
        {
            Vector2 m = Motors[i];
            return DronePt(cx, cy, sc, yaw, m.x * (1f + 0.35f * expl), m.y * (1f + 0.35f * expl), -2f);
        }

        private readonly Vector2[] _tmp = new Vector2[24];

        private void Drone(Painter2D p, float cx, float cy, float sc, float yaw, float alpha, float expl, int style, int hot, float heat = 0.5f)
        {
            Color baseC = A(C2, 0.92f * alpha);
            Color Gc(int g, float heatShift)
            {
                float ga = hot < 0 || hot == g ? 1f : 0.16f;
                if (style == 3) return A(Heat(g == 0 ? 0.85f * heatShift : g == 1 ? 0.55f * heatShift : g == 2 ? 0.18f + 0.1f * heatShift : 0.95f * heatShift), alpha * ga);
                if (style == 1) return A(new Color(0.78f, 0.82f, 0.88f), 0.95f * alpha * ga);
                return A(style == 2 ? Cel : C2, 0.92f * alpha * ga);
            }

            bool dashed = style == 2;
            float lift = expl * 14f;
            // arms (group 1)
            for (int i = 0; i < 4; i++)
            {
                Vector2 m = Motors[i];
                float sx = Mathf.Sign(m.x), sz = Mathf.Sign(m.y);
                Vector2 a = DronePt(cx, cy, sc, yaw, sx * 13f, sz * 9f, -3f);
                Vector2 b = DronePt(cx, cy, sc, yaw, m.x * (1f + 0.35f * expl), m.y * (1f + 0.35f * expl), -2f);
                Color ac = Gc(1, heat);
                if (dashed) Dash(p, a, b, ac); else Line(p, a, b, ac, style == 1 ? 3.2f : 1.6f);
            }

            // props (group 2) and motors
            for (int i = 0; i < 4; i++)
            {
                Vector2 m = Motors[i] * (1f + 0.35f * expl);
                for (int j = 0; j < 20; j++)
                {
                    float a = j / 20f * Mathf.PI * 2f;
                    _tmp[j] = DronePt(cx, cy, sc, yaw, m.x + Mathf.Cos(a) * 15f, m.y + Mathf.Sin(a) * 15f, -7f - lift * 0.5f);
                }

                Color pc = Gc(2, heat);
                Poly(p, _tmp, 20, pc, style == 1 ? A(pc, 0.25f) : Color.clear, true, dashed);
                Vector2 mc = DronePt(cx, cy, sc, yaw, m.x, m.y, -3f - lift * 0.3f);
                Circle(p, mc, 3f * Mathf.Max(0.8f, sc * 0.7f), Gc(1, heat), style == 1 ? Gc(1, heat) : Color.clear, 1.4f);
            }

            // body (group 0): top and bottom faces + verticals
            float top = -9f - lift, bot = 0f;
            Vector2[] t4 = { DronePt(cx, cy, sc, yaw, -13f, -9f, top), DronePt(cx, cy, sc, yaw, 13f, -9f, top), DronePt(cx, cy, sc, yaw, 13f, 9f, top), DronePt(cx, cy, sc, yaw, -13f, 9f, top) };
            Vector2[] b4 = { DronePt(cx, cy, sc, yaw, -13f, -9f, bot), DronePt(cx, cy, sc, yaw, 13f, -9f, bot), DronePt(cx, cy, sc, yaw, 13f, 9f, bot), DronePt(cx, cy, sc, yaw, -13f, 9f, bot) };
            Color bc = Gc(0, heat);
            Poly(p, b4, 4, bc, style == 1 ? A(bc, 0.5f) : Color.clear, true, dashed);
            for (int i = 0; i < 4; i++) { if (dashed) Dash(p, b4[i], t4[i], bc); else Line(p, b4[i], t4[i], bc, 1.3f); }
            Poly(p, t4, 4, bc, style == 1 ? A(bc, 0.85f) : A(bc, style == 3 ? 0.55f : 0.10f), true, dashed);
        }

        // ───────────────────────── frame ─────────────────────────

        private void Draw(MeshGenerationContext ctx)
        {
            if (contentRect.width < 4f) return;
            UpdateGeo();
            Painter2D p = ctx.painter2D;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            _time = DesktopMotion.Reduce ? 0.55f * PhaseSeconds : Time.unscaledTime - _t0;
            int count = PhaseCount[_step];
            _ph = DesktopMotion.Reduce ? 0 : Mathf.FloorToInt(_time / PhaseSeconds) % count;
            _t = DesktopMotion.Reduce ? 0.55f : (_time % PhaseSeconds) / PhaseSeconds;
            switch (_step)
            {
                case 0: Navigate(p); break;
                case 1: Select(p); break;
                case 2: Modes(p); break;
                case 3: Analyze(p); break;
                case 4: Studio(p); break;
                default: Ready(p); break;
            }
        }

        private float Fade() => Mathf.Min(Seg(_t, 0f, 0.08f), 1f - Seg(_t, 0.92f, 1f));

        // 1 Navegar: orbit / pan / zoom
        private void Navigate(Painter2D p)
        {
            float f = Fade();
            if (_ph == 0)
            {
                float e = E(_t);
                Drone(p, 105f, 100f, 1.5f, Mathf.Lerp(-0.9f, 0.9f, e), f, 0f, 0, -1);
                Vector2 c = Pt(Mathf.Lerp(52f, 158f, e), 168f - 20f * Mathf.Sin(e * Mathf.PI));
                for (int i = 0; i <= 14; i++)
                {
                    float u = i / 14f * e;
                    Circle(p, Pt(Mathf.Lerp(52f, 158f, u), 168f - 20f * Mathf.Sin(u * Mathf.PI)), 1.1f, Color.clear, A(C2, 0.5f * f));
                }

                Cursor(p, c, 1f);
                Mouse(p, 176f, 176f, 0, 1f);
            }
            else if (_ph == 1)
            {
                float e = E(_t);
                float cx = Mathf.Lerp(76f, 134f, e);
                Drone(p, cx, 100f, 1.5f, 0.45f, f, 0f, 0, -1);
                Cursor(p, Pt(cx + 6f, 130f), 1f);
                Mouse(p, 176f, 176f, 1, 1f);
                Line(p, Pt(60f, 160f), Pt(150f, 160f), A(C2, 0.25f * f), 1.2f);
            }
            else
            {
                float e = E(_t);
                Vector2 cur = new Vector2(150f, 78f);
                float sc = Mathf.Lerp(0.9f, 1.9f, e);
                Vector2 c0 = new Vector2(105f, 112f);
                Vector2 c = cur + (c0 - cur) * (sc / 0.9f);
                Drone(p, c.x, c.y, sc, 0.45f, f, 0f, 0, -1);
                Cursor(p, Pt(cur.x, cur.y), 0f);
                Mouse(p, 176f, 176f, 2, 1f, Mathf.Sin(_t * Mathf.PI * 6f) * 2f);
                Poly(p, new[] { Pt(166f, 168f), Pt(172f, 160f), Pt(178f, 168f) }, 3, A(C2, 0.8f * f), Color.clear, false);
            }
        }

        // 2 Seleccionar: click, double click focus, deselect
        private void Select(Painter2D p)
        {
            Vector2 m0 = MotorScreen(105f, 100f, 1.5f, 0.35f, 0);
            if (_ph == 0)
            {
                float f = Fade();
                Drone(p, 105f, 100f, 1.5f, 0.35f, 1f, 0f, 0, -1);
                float mv = Seg(_t, 0f, 0.5f);
                Vector2 cur = Vector2.Lerp(Pt(170f, 40f), m0, mv);
                float click = Seg(_t, 0.5f, 0.56f);
                Ripple(p, m0, (_t - 0.5f) / 0.45f);
                Circle(p, m0, 6f + 11f * Seg(_t, 0.52f, 0.7f), A(C2, Seg(_t, 0.52f, 0.62f) * f), A(C2, 0.12f * Seg(_t, 0.55f, 0.8f)), 2.4f);
                Cursor(p, cur, click);
            }
            else if (_ph == 1)
            {
                float focus = Seg(_t, 0.5f, 0.95f);
                float sc = Mathf.Lerp(1.5f, 2.5f, focus);
                Vector2 off = (m0 - Pt(105f, 100f)) / _k * (sc / 1.5f);
                Vector2 c = Vector2.Lerp(new Vector2(105f, 100f), new Vector2(105f, 112f) - off, focus);
                Drone(p, c.x, c.y, sc, 0.35f, 1f, 0f, 0, -1);
                Vector2 m = MotorScreen(c.x, c.y, sc, 0.35f, 0);
                Circle(p, m, 17f * sc / 1.5f * 0.8f, A(C2, 1f), A(C2, 0.12f), 2.4f);
                Ripple(p, m0, (_t - 0.08f) / 0.3f);
                Ripple(p, m0, (_t - 0.26f) / 0.3f);
                if (_t < 0.5f) Cursor(p, m0, (_t > 0.08f && _t < 0.14f) || (_t > 0.26f && _t < 0.32f) ? 1f : 0f);
            }
            else
            {
                Drone(p, 105f, 100f, 1.5f, 0.35f, 1f, 0f, 0, -1);
                float ring = 1f - Seg(_t, 0.5f, 0.85f);
                Circle(p, m0, 17f, A(C2, ring), A(C2, 0.12f * ring), 2.4f);
                Vector2 cur = Vector2.Lerp(m0 + new Vector2(10f, 10f), Pt(176f, 176f), Seg(_t, 0f, 0.4f));
                Ripple(p, Pt(176f, 176f), (_t - 0.42f) / 0.45f);
                Cursor(p, cur, Seg(_t, 0.4f, 0.46f));
            }
        }

        private void Chip(Painter2D p, float x, bool on, float glow)
        {
            RoundRect(p, x, 14f, 54f, 30f, 15f, A(on ? Cel : C2, on ? 0.9f : 0.35f), on ? A(Cel, 0.16f + 0.1f * glow) : new Color(0.09f, 0.11f, 0.14f, 1f));
        }

        // 3 Modos y aislar: 1 / 2 / 3 / I
        private void Modes(Painter2D p)
        {
            float g = Mathf.Sin(Mathf.Clamp01(_t) * Mathf.PI);
            for (int i = 0; i < 3; i++) Chip(p, 11f + 60f * i, _ph == i, g);
            float f = Fade();
            if (_ph == 0)
            {
                Drone(p, 105f, 150f, 1.15f, 0.4f, 1f, 0f, 0, -1);
                for (int i = 0; i < 4; i++)
                {
                    Vector2 m = MotorScreen(105f, 150f, 1.15f, 0.4f, i);
                    float pulse = (_t * 2f + i * 0.2f) % 1f;
                    Circle(p, m, 4f, A(C2, f), A(C2, 0.5f * f), 1.5f);
                    Circle(p, m, 4f + 9f * pulse, A(C2, (1f - pulse) * 0.6f * f), Color.clear, 1.2f);
                }
            }
            else if (_ph == 1)
            {
                Drone(p, 105f, 150f, 1.15f, 0.4f, 1f, 0f, 0, -1);
                float y = Mathf.Lerp(110f, 186f, E(_t));
                p.fillColor = Bg;
                p.BeginPath(); p.MoveTo(Pt(10f, 60f)); p.LineTo(Pt(200f, 60f)); p.LineTo(Pt(200f, y - 6f)); p.LineTo(Pt(10f, y)); p.ClosePath(); p.Fill();
                Line(p, Pt(10f, y), Pt(200f, y - 6f), Cel, 2f);
                for (int i = 0; i < 9; i++) Line(p, Pt(22f + i * 20f, y + 2f - i * 0.7f), Pt(14f + i * 20f, y + 9f - i * 0.7f), A(Cel, 0.4f), 1.2f);
            }
            else if (_ph == 2)
            {
                float blend = E((_t - 0.3f) / 0.4f);
                Drone(p, 105f, 150f, 1.15f, 0.4f, 1f - blend, 0f, 1, -1);
                Drone(p, 105f, 150f, 1.15f, 0.4f, blend, 0f, 3, -1, 0.6f + 0.2f * Mathf.Sin(_time * 3f));
            }
            else
            {
                float iso = E(_t / 0.4f);
                int hot = 0;
                Drone(p, 105f, 150f, 1.15f, 0.4f, 1f, 0f, 0, iso > 0.01f ? hot : -1);
                Circle(p, DronePt(105f, 150f, 1.15f, 0.4f, 0f, 0f, -9f), 17f, A(C2, iso), A(C2, 0.08f * iso), 2f);
                Cursor(p, Pt(122f, 150f), 0f);
            }
        }

        // 4 Analizar: cut / explode / filter
        private void Analyze(Painter2D p)
        {
            float f = Fade();
            if (_ph == 0)
            {
                Drone(p, 105f, 118f, 1.35f, 0.4f, 1f, 0f, 0, -1);
                float e = E(_t);
                float y = Mathf.Lerp(62f, 160f, e);
                p.fillColor = Bg;
                p.BeginPath(); p.MoveTo(Pt(8f, 50f)); p.LineTo(Pt(202f, 50f)); p.LineTo(Pt(202f, y - 8f)); p.LineTo(Pt(8f, y)); p.ClosePath(); p.Fill();
                Poly(p, new[] { Pt(8f, y), Pt(202f, y - 8f), Pt(190f, y + 22f), Pt(20f, y + 30f) }, 4, A(Cel, 0.9f * f), A(Cel, 0.12f * f));
            }
            else if (_ph == 1)
            {
                float e = _t < 0.5f ? E(_t * 2f) : 1f - E((_t - 0.5f) * 2f);
                Drone(p, 105f, 125f, 1.25f, 0.4f, 1f, e, 0, -1);
                Line(p, Pt(36f, 202f), Pt(174f, 202f), A(C2, 0.35f), 1.5f);
                Circle(p, Pt(36f + 138f * e, 202f), 5f, A(C2, 0.9f), A(Cel, 0.9f), 1.5f);
            }
            else
            {
                int hot = _t < 0.33f ? 2 : _t < 0.66f ? 1 : 0;
                Drone(p, 105f, 125f, 1.25f, 0.4f, 1f, 0f, 0, hot);
                Poly(p, new[] { Pt(14f, 22f), Pt(48f, 22f), Pt(36f, 38f), Pt(36f, 52f), Pt(26f, 46f), Pt(26f, 38f) }, 6, A(Cel, 0.95f), A(Cel, 0.16f));
            }
        }

        // 5 Estudio: render modes / environment / thermal legend
        private void Studio(Painter2D p)
        {
            if (_ph == 0)
            {
                int style = _t < 0.25f ? 0 : _t < 0.5f ? 1 : _t < 0.75f ? 2 : 3;
                Drone(p, 105f, 112f, 1.35f, 0.4f, 1f, 0f, style, -1, 0.7f);
                for (int i = 0; i < 4; i++) Circle(p, Pt(66f + i * 26f, 190f), 4f, A(C2, i == style ? 1f : 0.35f), i == style ? A(Cel, 0.9f) : Color.clear, 1.4f);
            }
            else if (_ph == 1)
            {
                float u = _t * 3f;
                Color a = u < 1f ? Bg : u < 2f ? new Color(0.78f, 0.81f, 0.86f) : new Color(0.04f, 0.1f, 0.24f);
                Color b = u < 1f ? new Color(0.78f, 0.81f, 0.86f) : u < 2f ? new Color(0.04f, 0.1f, 0.24f) : Bg;
                Color bgc = Color.Lerp(a, b, E(u - Mathf.Floor(u)));
                FillQuad(p, 6f, 6f, 198f, 170f, A(bgc, 0.9f));
                float ang = Mathf.Lerp(200f, 340f, E(_t));
                Circle(p, Pt(105f + Mathf.Cos(ang * Mathf.Deg2Rad) * 80f, 70f + Mathf.Sin(ang * Mathf.Deg2Rad) * 40f), 7f, A(new Color(1f, 0.9f, 0.5f), 0.95f), A(new Color(1f, 0.9f, 0.5f), 0.4f), 1.5f);
                bool light = u >= 1f && u < 2f;
                Drone(p, 105f, 120f, 1.3f, 0.4f, 1f, 0f, 0, -1);
                if (light) Drone(p, 105f, 120f, 1.3f, 0.4f, 0.0f, 0f, 0, -1);
                int idx = Mathf.Min(2, (int)u);
                for (int i = 0; i < 3; i++) Circle(p, Pt(84f + i * 21f, 196f), 4f, A(C2, i == idx ? 1f : 0.35f), i == idx ? A(Cel, 0.9f) : Color.clear, 1.4f);
            }
            else
            {
                Drone(p, 105f, 105f, 1.35f, 0.4f, 1f, 0f, 3, -1, 0.55f + 0.35f * Mathf.Sin(_time * 2.4f));
                for (int i = 0; i < 40; i++) FillQuad(p, 30f + i * 4.2f, 176f, 4.4f, 9f, Heat(i / 39f));
                Line(p, Pt(30f, 192f), Pt(198f, 192f), A(C2, 0.3f), 1.2f);
            }
        }

        // 6 Listo: tree / settings / shortcuts
        private void Ready(Painter2D p)
        {
            if (_ph == 0)
            {
                float slide = E(_t / 0.25f);
                float x = Mathf.Lerp(-90f, 8f, slide);
                RoundRect(p, x, 12f, 90f, 200f, 8f, A(C2, 0.45f), new Color(0.09f, 0.11f, 0.14f, 1f));
                for (int i = 0; i < 7; i++)
                {
                    float rowX = x + 10f + (i == 1 || i == 2 ? 12f : 0f);
                    float expand = i == 0 ? E((_t - 0.3f) / 0.2f) : 1f;
                    float y = 28f + i * 24f;
                    bool hot = i == 2 && _t > 0.6f;
                    if (hot) RoundRect(p, x + 4f, y - 9f, 82f, 18f, 6f, A(Cel, 0.6f), A(Cel, 0.16f));
                    if (i == 0 || i == 3)
                    {
                        float r = i == 0 ? expand : 0f;
                        Vector2 c = Pt(rowX + 2f, y);
                        Poly(p, new[] { c + new Vector2(-2f, -3.5f) * _k, c + new Vector2(2.5f, 0f) * _k, c + new Vector2(-2f, 3.5f) * _k }, 3, A(C2, 0.8f), Color.clear, false);
                    }

                    Line(p, Pt(rowX + 10f, y), Pt(rowX + 10f + 40f - (i % 3) * 8f, y), A(C2, hot ? 0.95f : 0.45f), 2f);
                }

                Drone(p, 152f, 112f, 0.75f, 0.4f, 0.8f, 0f, 0, -1);
            }
            else if (_ph == 1)
            {
                RoundRect(p, 18f, 28f, 174f, 150f, 12f, A(C2, 0.45f), new Color(0.09f, 0.11f, 0.14f, 1f));
                for (int i = 0; i < 3; i++)
                {
                    float y = 62f + i * 36f;
                    float v = 0.5f + 0.4f * Mathf.Sin(_time * 1.6f + i * 1.7f);
                    Line(p, Pt(40f, y), Pt(170f, y), A(C2, 0.3f), 2f);
                    Line(p, Pt(40f, y), Pt(40f + 130f * v, y), A(Cel, 0.9f), 2.4f);
                    Circle(p, Pt(40f + 130f * v, y), 5.5f, A(C2, 1f), A(Bg, 1f), 1.6f);
                }
            }
            else
            {
                Drone(p, 105f, 125f, 1.1f, 0.4f, 0.35f, 0f, 0, -1);
                float s = E(_t / 0.2f);
                RoundRect(p, 16f, 22f + 10f * (1f - s), 178f, 140f, 10f, A(C2, 0.5f * s), A(new Color(0.09f, 0.11f, 0.14f, 1f), s));
                for (int i = 0; i < 4; i++)
                {
                    float y = 44f + i * 28f + 10f * (1f - s);
                    RoundRect(p, 28f, y, 26f, 16f, 5f, A(Cel, 0.8f * s), A(Cel, 0.14f * s));
                    Line(p, Pt(66f, y + 8f), Pt(66f + 90f - i * 14f, y + 8f), A(C2, 0.5f * s), 2f);
                }
            }
        }
    }
}
#endif
