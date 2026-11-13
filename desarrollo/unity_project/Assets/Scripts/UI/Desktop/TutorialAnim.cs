#if UNITY_STANDALONE
using System;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PC only. The looping illustration of each tutorial step, drawn with Painter2D (no video, no assets): a wireframe drone and a
    /// cursor / mouse / key glyph (or finger glyphs in the Touch variant) that act out the control the step explains. Every step has 3-4
    /// phases of 2.6 s that match the rows of the step: they play in sequence, or only one of them (looping) when the user selects its row
    /// (<see cref="LockedPhase"/>). Painter2D draws the shapes; key caps, caption and the part label are small labels over it. It only
    /// repaints while the tutorial is open and not at all with "Reduce animations" (one still frame).
    /// Tokens: PhaseSeconds, the 210x230 design box, the colours below and DesktopMotion.Speed are the only constants to port.
    /// </summary>
    internal sealed partial class TutorialAnim : VisualElement
    {
        private const float W = 210f, H = 230f, PhaseSeconds = 2.6f;
        private static readonly int[] PhaseCount = { 3, 3, 4, 3, 3, 3 };

        private static readonly Color C2 = new Color(0.749f, 0.918f, 1f, 1f);
        private static readonly Color Cel = new Color(0.275f, 0.686f, 1f, 1f);
        private static readonly Color Bg = new Color(0.055f, 0.067f, 0.086f, 1f);
        private static readonly Color[] HeatStops =
        {
            new Color(0.16f, 0.29f, 0.84f), new Color(0.13f, 0.72f, 0.81f), new Color(0.48f, 0.85f, 0.34f),
            new Color(0.96f, 0.85f, 0.04f), new Color(1f, 0.42f, 0.17f), new Color(0.84f, 0.14f, 0.49f)
        };

        public Action<int> PhaseChanged;

        /// <summary>Touch variant: finger glyphs and gestures (drag, two-finger pan, pinch, tap, double tap) instead of cursor, mouse and keys.</summary>
        public bool Touch;

        /// <summary>-1 = the phases play in sequence; otherwise only that phase loops.</summary>
        public int LockedPhase = -1;

        public int CurrentPhase => Mathf.Max(0, _phase);

        private int _step;
        private int _phase = -1;
        private float _t0;
        private float _k = 1f, _ox, _oy;
        private readonly Label _cap;
        private readonly Label _pop;
        private readonly Label[] _keys = new Label[3];
        private readonly IVisualElementScheduledItem _tick;
        private int _popState;
        private Color? _tint;

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
            _pop = new Label { pickingMode = PickingMode.Ignore };
            _pop.AddToClassList("dk-tut-anim-pop");
            _pop.style.display = DisplayStyle.None;
            Add(_pop);
            for (int i = 0; i < _keys.Length; i++)
            {
                _keys[i] = new Label { pickingMode = PickingMode.Ignore };
                _keys[i].AddToClassList("dk-tut-anim-key");
                _keys[i].style.display = DisplayStyle.None;
                Add(_keys[i]);
            }

            InitTel();
            _tick = schedule.Execute(Frame).Every(16);
            _tick.Pause();
            DesktopMotion.Changed += () => MarkDirtyRepaint();
        }

        public void SetStep(int step)
        {
            _step = Mathf.Clamp(step, 0, PhaseCount.Length);   // 0-4 as before, 5 = Telemetría (E-7), 6 = Listo
            _t0 = Time.unscaledTime;
            _phase = -1;
            _popState = 0;
            _pop.style.display = DisplayStyle.None;   // the part label of the Inspect scene must not survive a step change
            MarkDirtyRepaint();
            Frame();
        }

        public void Play(bool on)
        {
            if (on) { _t0 = Time.unscaledTime; _tick.Resume(); }
            else _tick.Pause();
            MarkDirtyRepaint();
        }

        /// <summary>Lock (or unlock with -1) one phase; the clock restarts so the chosen action starts from its beginning.</summary>
        public void Lock(int phase)
        {
            LockedPhase = phase;
            _t0 = Time.unscaledTime;
            _phase = -1;
            _popState = 0;
            _pop.style.display = DisplayStyle.None;
            Frame();
        }

        public void Refresh() { _phase = -1; PlaceTags(); Frame(); }

        private static bool Es => AppLanguageManager.IsSpanish;

        private int PhaseNow(float time)
        {
            if (DesktopMotion.Reduce) return Mathf.Max(0, LockedPhase);
            if (LockedPhase >= 0) return Mathf.Min(LockedPhase, PhasesOf(_step) - 1);
            return Mathf.FloorToInt(time / PhaseSeconds) % PhasesOf(_step);
        }

        private void Frame()
        {
            float time = DesktopMotion.Reduce ? 0.55f * PhaseSeconds : Time.unscaledTime - _t0;
            int ph = PhaseNow(time);
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

            UpdatePop(time);
            UpdateTel(time);
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

        // ───────────────────────── labels (key caps, caption, part label) ─────────────────────────

        private void Key(int i, string text, float x, float y)
        {
            Label l = _keys[i];
            if (string.IsNullOrEmpty(text) || Touch) { l.style.display = DisplayStyle.None; return; }
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
            HideTel();
            if (s == TelStep) { _cap.text = PlaceTelTags(ph); return; }
            switch (s)
            {
                case 0:
                    if (ph == 0) cap = Touch ? (Es ? "Un dedo: orbitar" : "One finger: orbit") : (Es ? "Arrastra el fondo para orbitar" : "Drag the background to orbit");
                    else if (ph == 1) { cap = Touch ? (Es ? "Dos dedos: desplazar" : "Two fingers: pan") : (Es ? "Shift + clic derecho: desplazar" : "Shift + right drag: pan"); Key(0, "Shift", 8, 190); }
                    else cap = Touch ? (Es ? "Pellizco: zoom" : "Pinch: zoom") : (Es ? "Rueda: zoom hacia el cursor" : "Wheel: zoom to the cursor");
                    break;
                case 1:
                    if (ph == 0) cap = Touch ? (Es ? "Toca la pieza" : "Tap the part") : (Es ? "Clic: seleccionar la pieza" : "Click: select the part");
                    else if (ph == 1) { cap = Touch ? (Es ? "Doble toque: enfocar" : "Double tap: focus") : (Es ? "Doble clic: enfocar" : "Double click: focus"); Key(0, "F", 8, 190); }
                    else { cap = Touch ? (Es ? "Toca el fondo: soltar" : "Tap the background: deselect") : (Es ? "Clic en el fondo: soltar" : "Click the background: deselect"); Key(0, "Esc", 8, 190); }
                    break;
                case 2:
                    Key(0, "1", 30, 17); Key(1, "2", 90, 17); Key(2, "3", 150, 17);
                    cap = ph == 0 ? (Es ? "1 Inspeccionar: ver y nombrar piezas" : "1 Inspect: see and name parts")
                        : ph == 1 ? (Es ? "2 Analizar: corte" : "2 Analyze: cut")
                        : ph == 2 ? (Es ? "3 Estudio: render" : "3 Studio: render")
                        : (Es ? "I: aislar la pieza" : "I: isolate the part");
                    if (ph == 3) { Key(0, "", 0, 0); Key(1, "", 0, 0); Key(2, "", 0, 0); Key(0, "I", 8, 190); }
                    if (Touch) cap = ph == 0 ? (Es ? "Toca Inspeccionar" : "Tap Inspect") : ph == 1 ? (Es ? "Toca Analizar" : "Tap Analyze") : ph == 2 ? (Es ? "Toca Estudio" : "Tap Studio") : (Es ? "Toca Aislar" : "Tap Isolate");
                    break;
                case 3:
                    cap = ph == 0 ? (Es ? "C: corte transversal" : "C: cross-section")
                        : ph == 1 ? (Es ? "E: vista explosionada" : "E: exploded view")
                        : (Es ? "G: filtrar por categoría" : "G: filter by category");
                    Key(0, ph == 0 ? "C" : ph == 1 ? "E" : "G", 8, 190);
                    if (Touch) cap = ph == 0 ? (Es ? "Toca Corte" : "Tap Cut") : ph == 1 ? (Es ? "Toca Explotar" : "Tap Explode") : (Es ? "Toca Filtrar" : "Tap Filter");
                    break;
                case 4:
                    cap = ph == 0 ? (Es ? "V: cambia el modo de render" : "V: cycle the render mode")
                        : ph == 1 ? (Es ? "Tiempo: día, noche, atardecer" : "Time: day, night, sunset")
                        : (Es ? "Térmico: la leyenda aparece" : "Thermal: the legend appears");
                    if (ph == 0) Key(0, "V", 8, 190);
                    if (Touch && ph == 0) cap = Es ? "Toca una tarjeta de render" : "Tap a render card";
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

        // the "Inspect" scene shows a part label: its state is derived from the clock so the label never lags the drawing
        private void InspectTimeline(float t, out int state, out float sheet)
        {
            sheet = Seg(t, 0.68f, 0.9f);
            state = t > 0.27f && t < 0.5f ? 1 : t >= 0.5f && t < 0.66f ? 2 : 0;
        }

        private void UpdatePop(float time)
        {
            int state = 0;
            if (_step == 2 && _phase == 0)
            {
                float t = DesktopMotion.Reduce ? 0.55f : (time % PhaseSeconds) / PhaseSeconds;
                InspectTimeline(t, out state, out _);
            }

            if (state == _popState) return;
            _popState = state;
            if (state == 0) { _pop.style.display = DisplayStyle.None; return; }
            _pop.style.display = DisplayStyle.Flex;
            _pop.text = state == 1 ? (Es ? "Motor delantero" : "Front motor") : (Es ? "Controlador de vuelo" : "Flight controller");
            Vector2 at = state == 1 ? new Vector2(84f, 112f) : new Vector2(30f, 108f);
            _pop.style.left = _ox + at.x * _k;
            _pop.style.top = _oy + at.y * _k;
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
            if (len < 0.5f) return;
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

        /// <summary>Touch glyph: a finger whose tip is at 'at'; pressing shrinks it and shows a contact ring.</summary>
        private void Finger(Painter2D p, Vector2 at, float press)
        {
            float s = _k * (1f - 0.1f * press);
            Vector2 tip = at + new Vector2(0f, 5.5f * s);
            if (press > 0.01f) Circle(p, at, 9f + 3f * press, A(C2, 0.6f * press), A(Cel, 0.25f * press), 1.6f);
            p.fillColor = new Color(1f, 1f, 1f, 0.9f);
            p.BeginPath();
            p.MoveTo(tip + new Vector2(-5.5f * s, 22f * s));
            p.LineTo(tip + new Vector2(-5.5f * s, 0f));
            p.Arc(tip, 5.5f * s, Angle.Degrees(180f), Angle.Degrees(360f));
            p.LineTo(tip + new Vector2(5.5f * s, 22f * s));
            p.ClosePath();
            p.Fill();
            p.strokeColor = Bg; p.lineWidth = 1.1f;
            p.BeginPath();
            p.MoveTo(tip + new Vector2(-5.5f * s, 22f * s));
            p.LineTo(tip + new Vector2(-5.5f * s, 0f));
            p.Arc(tip, 5.5f * s, Angle.Degrees(180f), Angle.Degrees(360f));
            p.LineTo(tip + new Vector2(5.5f * s, 22f * s));
            p.Stroke();
        }

        private void Ptr(Painter2D p, Vector2 at, float press)
        {
            if (Touch) Finger(p, at, press); else Cursor(p, at, press);
        }

        /// <summary>Mouse glyph: button 0 = left, 1 = right, 2 = wheel; pressed fills it celeste.</summary>
        private void Mouse(Painter2D p, float x, float y, int button, float press, float wheelOffset = 0f)
        {
            if (Touch) return;
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
            Color Gc(int g, float heatShift)
            {
                float ga = hot < 0 || hot == g ? 1f : 0.16f;
                if (style == 3) return A(Heat(g == 0 ? 0.85f * heatShift : g == 1 ? 0.55f * heatShift : g == 2 ? 0.18f + 0.1f * heatShift : 0.95f * heatShift), alpha * ga);
                if (style == 1) return A(new Color(0.78f, 0.82f, 0.88f), 0.95f * alpha * ga);
                return A(style == 2 ? Cel : (_tint ?? C2), 0.92f * alpha * ga);
            }

            bool dashed = style == 2;
            float lift = expl * 14f;
            for (int i = 0; i < 4; i++)
            {
                Vector2 m = Motors[i];
                float sx = Mathf.Sign(m.x), sz = Mathf.Sign(m.y);
                Vector2 a = DronePt(cx, cy, sc, yaw, sx * 13f, sz * 9f, -3f);
                Vector2 b = DronePt(cx, cy, sc, yaw, m.x * (1f + 0.35f * expl), m.y * (1f + 0.35f * expl), -2f);
                Color ac = Gc(1, heat);
                if (dashed) Dash(p, a, b, ac); else Line(p, a, b, ac, style == 1 ? 3.2f : 1.6f);
            }

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

            float top = -9f - lift, bot = 0f;
            Vector2[] t4 = { DronePt(cx, cy, sc, yaw, -13f, -9f, top), DronePt(cx, cy, sc, yaw, 13f, -9f, top), DronePt(cx, cy, sc, yaw, 13f, 9f, top), DronePt(cx, cy, sc, yaw, -13f, 9f, top) };
            Vector2[] b4 = { DronePt(cx, cy, sc, yaw, -13f, -9f, bot), DronePt(cx, cy, sc, yaw, 13f, -9f, bot), DronePt(cx, cy, sc, yaw, 13f, 9f, bot), DronePt(cx, cy, sc, yaw, -13f, 9f, bot) };
            Color bc = Gc(0, heat);
            Poly(p, b4, 4, bc, style == 1 ? A(bc, 0.5f) : Color.clear, true, dashed);
            for (int i = 0; i < 4; i++) { if (dashed) Dash(p, b4[i], t4[i], bc); else Line(p, b4[i], t4[i], bc, 1.3f); }
            Poly(p, t4, 4, bc, style == 1 ? A(bc, 0.85f) : A(bc, style == 3 ? 0.55f : 0.10f), true, dashed);
        }

        /// <summary>Exploded view: every part moves radially away from the centre with visible gaps (plates, battery, arms, motors, props).</summary>
        private void ExplodedDrone(Painter2D p, float cx, float cy, float sc, float yaw, float x)
        {
            Color c = A(C2, 0.95f);
            Color soft = A(C2, 0.45f);
            // bottom plate
            Vector2[] q = new Vector2[4];
            float yb = 10f * x;
            q[0] = DronePt(cx, cy, sc, yaw, -13f, -9f, yb); q[1] = DronePt(cx, cy, sc, yaw, 13f, -9f, yb); q[2] = DronePt(cx, cy, sc, yaw, 13f, 9f, yb); q[3] = DronePt(cx, cy, sc, yaw, -13f, 9f, yb);
            Poly(p, q, 4, c, A(C2, 0.06f));
            // battery (box below the plate)
            float by = 4f + 34f * x;
            Vector2[] bt = { DronePt(cx, cy, sc, yaw, -9f, -6f, by), DronePt(cx, cy, sc, yaw, 9f, -6f, by), DronePt(cx, cy, sc, yaw, 9f, 6f, by), DronePt(cx, cy, sc, yaw, -9f, 6f, by) };
            Vector2[] bb = { DronePt(cx, cy, sc, yaw, -9f, -6f, by + 8f), DronePt(cx, cy, sc, yaw, 9f, -6f, by + 8f), DronePt(cx, cy, sc, yaw, 9f, 6f, by + 8f), DronePt(cx, cy, sc, yaw, -9f, 6f, by + 8f) };
            Poly(p, bb, 4, soft, Color.clear);
            for (int i = 0; i < 4; i++) Line(p, bt[i], bb[i], soft, 1.2f);
            Poly(p, bt, 4, c, A(C2, 0.1f));
            // top plate
            float yt = -9f - 30f * x;
            Vector2[] tp = { DronePt(cx, cy, sc, yaw, -13f, -9f, yt), DronePt(cx, cy, sc, yaw, 13f, -9f, yt), DronePt(cx, cy, sc, yaw, 13f, 9f, yt), DronePt(cx, cy, sc, yaw, -13f, 9f, yt) };
            Poly(p, tp, 4, c, A(C2, 0.12f));
            // arms, motors, props: radial
            for (int i = 0; i < 4; i++)
            {
                Vector2 m = Motors[i];
                float sx = Mathf.Sign(m.x), sz = Mathf.Sign(m.y);
                Vector2 dir = new Vector2(sx * 0.82f, sz * 0.58f);
                Vector2 a0 = new Vector2(sx * 13f, sz * 9f) + dir * (18f * x);
                Vector2 a1 = new Vector2(m.x, m.y) * 0.78f + dir * (22f * x);
                Line(p, DronePt(cx, cy, sc, yaw, a0.x, a0.y, -3f), DronePt(cx, cy, sc, yaw, a1.x, a1.y, -3f), c, 2.4f);
                Vector2 mm = m + dir * (36f * x);
                Vector2 mc = DronePt(cx, cy, sc, yaw, mm.x, mm.y, -3f - 6f * x);
                Circle(p, mc, 3.4f, c, A(C2, 0.5f), 1.4f);
                Vector2 pm = m + dir * (50f * x);
                for (int j = 0; j < 20; j++)
                {
                    float a = j / 20f * Mathf.PI * 2f;
                    _tmp[j] = DronePt(cx, cy, sc, yaw, pm.x + Mathf.Cos(a) * 15f, pm.y + Mathf.Sin(a) * 15f, -7f - 16f * x);
                }

                Poly(p, _tmp, 20, c, A(C2, 0.08f));
                if (x > 0.25f) Dash(p, DronePt(cx, cy, sc, yaw, a1.x, a1.y, -3f), mc, A(C2, 0.45f * x));
            }
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
            _ph = PhaseNow(_time);
            _t = DesktopMotion.Reduce ? 0.55f : (_time % PhaseSeconds) / PhaseSeconds;
            _tint = null;
            switch (_step)
            {
                case 0: Navigate(p); break;
                case 1: Select(p); break;
                case 2: Modes(p); break;
                case 3: Analyze(p); break;
                case 4: Studio(p); break;
                case TelStep: Telemetry(p); break;
                default: Ready(p); break;
            }
        }

        private float Fade() => Mathf.Min(Seg(_t, 0f, 0.08f), 1f - Seg(_t, 0.92f, 1f));

        // 1 Navegar: orbit / pan / zoom (the drone stays centred in the frame while zooming)
        private void Navigate(Painter2D p)
        {
            float f = Fade();
            float e = E(_t);
            if (_ph == 0)
            {
                Drone(p, 105f, 100f, 1.5f, Mathf.Lerp(-0.9f, 0.9f, e), f, 0f, 0, -1);
                Vector2 c = Pt(Mathf.Lerp(52f, 158f, e), 168f - 20f * Mathf.Sin(e * Mathf.PI));
                for (int i = 0; i <= 14; i++)
                {
                    float u = i / 14f * e;
                    Circle(p, Pt(Mathf.Lerp(52f, 158f, u), 168f - 20f * Mathf.Sin(u * Mathf.PI)), 1.1f, Color.clear, A(C2, 0.5f * f));
                }

                Ptr(p, c, 1f);
                Mouse(p, 176f, 176f, 0, 1f);
            }
            else if (_ph == 1)
            {
                float cx = Mathf.Lerp(76f, 134f, e);
                Drone(p, cx, 100f, 1.5f, 0.45f, f, 0f, 0, -1);
                if (Touch)
                {
                    Finger(p, Pt(cx - 12f, 126f), 1f);
                    Finger(p, Pt(cx + 14f, 132f), 1f);
                }
                else
                {
                    Cursor(p, Pt(cx + 6f, 130f), 1f);
                    Mouse(p, 176f, 176f, 1, 1f);
                }

                Line(p, Pt(60f, 168f), Pt(150f, 168f), A(C2, 0.25f * f), 1.2f);
            }
            else
            {
                float sc = Mathf.Lerp(0.9f, 1.9f, e);
                Drone(p, 105f, 108f, sc, 0.45f, f, 0f, 0, -1);
                if (Touch)
                {
                    float gap = Mathf.Lerp(12f, 48f, e);
                    Finger(p, Pt(105f - gap, 150f + gap * 0.25f), 1f);
                    Finger(p, Pt(105f + gap, 150f - gap * 0.25f), 1f);
                    Line(p, Pt(105f - gap, 150f + gap * 0.25f), Pt(105f + gap, 150f - gap * 0.25f), A(C2, 0.3f * f), 1.2f);
                }
                else
                {
                    Cursor(p, Pt(105f, 108f), 0f);
                    Mouse(p, 176f, 176f, 2, 1f, Mathf.Sin(_t * Mathf.PI * 6f) * 2f);
                    Poly(p, new[] { Pt(166f, 168f), Pt(172f, 160f), Pt(178f, 168f) }, 3, A(C2, 0.8f * f), Color.clear, false);
                }
            }
        }

        // 2 Seleccionar: click / tap, double click / double tap focus, deselect
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
                Ptr(p, cur, click);
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
                if (_t < 0.5f) Ptr(p, m0, (_t > 0.08f && _t < 0.14f) || (_t > 0.26f && _t < 0.32f) ? 1f : 0f);
            }
            else
            {
                Drone(p, 105f, 100f, 1.5f, 0.35f, 1f, 0f, 0, -1);
                float ring = 1f - Seg(_t, 0.5f, 0.85f);
                Circle(p, m0, 17f, A(C2, ring), A(C2, 0.12f * ring), 2.4f);
                Vector2 cur = Vector2.Lerp(m0 + new Vector2(10f, 10f), Pt(176f, 176f), Seg(_t, 0f, 0.4f));
                Ripple(p, Pt(176f, 176f), (_t - 0.42f) / 0.45f);
                Ptr(p, cur, Seg(_t, 0.4f, 0.46f));
            }
        }

        private void Chip(Painter2D p, float x, bool on, float glow)
        {
            RoundRect(p, x, 14f, 54f, 30f, 15f, A(on ? Cel : C2, on ? 0.9f : 0.35f), on ? A(Cel, 0.16f + 0.1f * glow) : new Color(0.09f, 0.11f, 0.14f, 1f));
        }

        private void Lens(Painter2D p, Vector2 at, float a)
        {
            Circle(p, at, 12f, A(C2, a), A(new Color(0.75f, 0.92f, 1f), 0.1f * a), 2f);
            Line(p, at + new Vector2(8.5f, 8.5f) * _k, at + new Vector2(19f, 19f) * _k, A(C2, a), 2.6f);
            p.strokeColor = A(C2, 0.5f * a); p.lineWidth = 1.2f * _k;
            p.BeginPath(); p.Arc(at, 7.5f * _k, Angle.Degrees(200f), Angle.Degrees(260f)); p.Stroke();
        }

        // the cut: the plane sweeps in, the section stays visible, then the plane retreats and the drone is whole again
        private void CutScene(Painter2D p, float cx, float cy, float sc)
        {
            Drone(p, cx, cy, sc, 0.4f, 1f, 0f, 0, -1);
            float amount = _t < 0.42f ? E(_t / 0.42f) : _t < 0.68f ? 1f : 1f - E((_t - 0.68f) / 0.3f);
            if (amount < 0.01f) return;
            float y = Mathf.Lerp(48f, cy + 8f, amount);
            p.fillColor = A(Bg, 1f);
            p.BeginPath(); p.MoveTo(Pt(8f, 40f)); p.LineTo(Pt(202f, 40f)); p.LineTo(Pt(202f, y - 8f)); p.LineTo(Pt(8f, y)); p.ClosePath(); p.Fill();
            Poly(p, new[] { Pt(8f, y), Pt(202f, y - 8f), Pt(190f, y + 22f), Pt(20f, y + 30f) }, 4, A(Cel, 0.95f), A(Cel, 0.14f));
            // hatched section across the drone
            for (int i = 0; i < 9; i++) Line(p, Pt(34f + i * 18f, y + 24f - i * 0.7f), Pt(26f + i * 18f, y + 8f - i * 0.7f), A(Cel, 0.35f * amount), 1.1f);
        }

        // 3 Modos y aislar: 1 inspect / 2 cut / 3 render / I isolate
        private void Modes(Painter2D p)
        {
            float g = Mathf.Sin(Mathf.Clamp01(_t) * Mathf.PI);
            for (int i = 0; i < 3; i++) Chip(p, 11f + 60f * i, _ph == i, g);
            float f = Fade();
            if (_ph == 0)
            {
                // Inspect: a lens scans the drone, parts get the celeste ring and a name label, then the info sheet opens
                InspectTimeline(_t, out _, out float sheet);
                float cx = 105f - 30f * sheet, cy = 150f;
                Drone(p, cx, cy, 1.15f, 0.4f, 1f, 0f, 0, -1);
                Vector2 mot = MotorScreen(cx, cy, 1.15f, 0.4f, 1);
                Vector2 body = DronePt(cx, cy, 1.15f, 0.4f, 0f, 0f, -9f);
                Vector2 l0 = Pt(22f, 118f);
                Vector2 pos = _t < 0.3f ? Vector2.Lerp(l0, mot + new Vector2(-6f, -6f) * _k, E(_t / 0.3f))
                    : _t < 0.55f ? Vector2.Lerp(mot + new Vector2(-6f, -6f) * _k, body + new Vector2(-6f, -6f) * _k, E((_t - 0.3f) / 0.25f))
                    : body + new Vector2(-6f, -6f) * _k;
                float ring1 = Seg(_t, 0.26f, 0.34f) * (1f - Seg(_t, 0.46f, 0.54f));
                float ring2 = Seg(_t, 0.5f, 0.58f);
                Circle(p, mot, 11f, A(C2, ring1), A(C2, 0.12f * ring1), 2.4f);
                Circle(p, body, 13f, A(C2, ring2), A(C2, 0.12f * ring2), 2.4f);
                Lens(p, pos, 1f - Seg(_t, 0.78f, 0.9f));
                // info sheet sliding in from the right
                float sx = Mathf.Lerp(214f, 112f, sheet);
                if (sheet > 0.01f)
                {
                    RoundRect(p, sx, 60f, 92f, 120f, 8f, A(C2, 0.6f * sheet), new Color(0.09f, 0.11f, 0.14f, 1f));
                    Line(p, Pt(sx + 10f, 78f), Pt(sx + 62f, 78f), A(C2, sheet), 3f);
                    for (int i = 0; i < 4; i++)
                    {
                        Line(p, Pt(sx + 10f, 100f + i * 18f), Pt(sx + 30f, 100f + i * 18f), A(C2, 0.4f * sheet), 2f);
                        Line(p, Pt(sx + 38f, 100f + i * 18f), Pt(sx + 78f - i * 6f, 100f + i * 18f), A(C2, 0.75f * sheet), 2f);
                    }
                }
            }
            else if (_ph == 1)
            {
                CutScene(p, 105f, 150f, 1.15f);
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
                Drone(p, 105f, 150f, 1.15f, 0.4f, 1f, 0f, 0, iso > 0.01f ? 0 : -1);
                Circle(p, DronePt(105f, 150f, 1.15f, 0.4f, 0f, 0f, -9f), 17f, A(C2, iso), A(C2, 0.08f * iso), 2f);
                Ptr(p, Pt(122f, 150f), 0f);
            }

            if (_ph < 3 && Touch) Ptr(p, Pt(38f + 60f * _ph, 60f), Mathf.Sin(_t * Mathf.PI) > 0.8f ? 1f : 0f);
        }

        // 4 Analizar: cut / explode / filter
        private void Analyze(Painter2D p)
        {
            if (_ph == 0)
            {
                CutScene(p, 105f, 118f, 1.35f);
            }
            else if (_ph == 1)
            {
                // assemble -> explode (parts leave radially) -> hold -> reassemble
                float x = _t < 0.38f ? E(_t / 0.38f) : _t < 0.62f ? 1f : 1f - E((_t - 0.62f) / 0.34f);
                ExplodedDrone(p, 105f, 118f, 1.0f + 0.12f * (1f - x), 0.5f, x);
                Line(p, Pt(36f, 206f), Pt(174f, 206f), A(C2, 0.35f), 1.5f);
                Circle(p, Pt(36f + 138f * x, 206f), 5f, A(C2, 0.9f), A(Cel, 0.9f), 1.5f);
            }
            else
            {
                int hot = _t < 0.33f ? 2 : _t < 0.66f ? 1 : 0;
                Drone(p, 105f, 125f, 1.25f, 0.4f, 1f, 0f, 0, hot);
                Poly(p, new[] { Pt(14f, 22f), Pt(48f, 22f), Pt(36f, 38f), Pt(36f, 52f), Pt(26f, 46f), Pt(26f, 38f) }, 6, A(Cel, 0.95f), A(Cel, 0.16f));
            }
        }

        // sky colours: 0 day, 1 night, 2 sunset (the same cycle as the TIME card)
        private static readonly Color[] SkyTop = { new Color(0.55f, 0.76f, 0.95f), new Color(0.02f, 0.03f, 0.10f), new Color(0.42f, 0.16f, 0.46f) };
        private static readonly Color[] SkyBot = { new Color(0.86f, 0.93f, 0.99f), new Color(0.07f, 0.09f, 0.22f), new Color(1f, 0.58f, 0.22f) };
        private static readonly Vector2[] Stars = { new Vector2(30, 24), new Vector2(62, 54), new Vector2(98, 20), new Vector2(150, 44), new Vector2(176, 22), new Vector2(120, 70), new Vector2(44, 88), new Vector2(188, 80) };

        private void Sky(Painter2D p)
        {
            float u = Mathf.Clamp01(_t) * 2.999f * 1f;
            int seg = Mathf.Min(2, (int)(u));
            float frac = u - seg;
            float bl = Seg(frac, 0.66f, 1f);
            int nx = (seg + 1) % 3;
            Color top = Color.Lerp(SkyTop[seg], SkyTop[nx], bl), bot = Color.Lerp(SkyBot[seg], SkyBot[nx], bl);
            const int bands = 14;
            for (int i = 0; i < bands; i++) FillQuad(p, 6f, 6f + i * (170f / bands), 198f, 170f / bands + 0.6f, Color.Lerp(top, bot, i / (bands - 1f)));
            float[] w = new float[3];
            w[seg] += 1f - bl; w[nx] += bl;
            float sun = w[0] + w[2], moon = w[1];
            if (sun > 0.01f)
            {
                float y = (w[0] * 38f + w[2] * 128f) / sun;
                Color sc = Color.Lerp(new Color(1f, 0.93f, 0.55f), new Color(1f, 0.55f, 0.18f), w[2] / sun);
                Circle(p, Pt(150f, y), 9f + 3f * (w[2] / sun), A(sc, 0.95f * sun), A(sc, 0.95f * sun), 1f);
                Circle(p, Pt(150f, y), 16f + 4f * (w[2] / sun), A(sc, 0.25f * sun), Color.clear, 6f);
            }

            if (moon > 0.01f)
            {
                Circle(p, Pt(58f, 42f), 10f, A(new Color(0.92f, 0.94f, 1f), moon), A(new Color(0.92f, 0.94f, 1f), moon), 1f);
                Circle(p, Pt(63f, 38f), 9f, Color.clear, A(Color.Lerp(top, SkyTop[1], moon), 1f), 1f);
                for (int i = 0; i < Stars.Length; i++) Circle(p, Pt(Stars[i].x, Stars[i].y), 1.3f, Color.clear, A(Color.white, 0.8f * moon * (0.6f + 0.4f * Mathf.Sin(_time * 3f + i))), 1f);
            }

            // the drone, dark on the light sky and light on the dark ones
            float dark = w[0] * 0.9f;
            _tint = Color.Lerp(C2, new Color(0.08f, 0.12f, 0.2f), dark);
            Drone(p, 105f, 128f, 1.2f, 0.4f, 1f, 0f, 0, -1);
            _tint = null;
            for (int i = 0; i < 3; i++) Circle(p, Pt(84f + i * 21f, 196f), 4f, A(C2, w[i] > 0.5f ? 1f : 0.35f), w[i] > 0.5f ? A(Cel, 0.9f) : Color.clear, 1.4f);
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
                Sky(p);
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
                        Vector2 c = Pt(rowX + 2f, y);
                        Poly(p, new[] { c + new Vector2(-2f, -3.5f) * _k, c + new Vector2(2.5f, 0f) * _k, c + new Vector2(-2f, 3.5f) * _k }, 3, A(C2, 0.8f * (i == 0 ? expand : 1f)), Color.clear, false);
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
