#if UNITY_STANDALONE
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Telemetry;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Scene "Vuelo" of the tutorial (E-7, step 6 of 7 "Telemetría"; PROPUESTA_TEXTOS_HONESTIDAD §3.4 and decision D-7). Four phases of 2.6 s that match
    /// the rows of the step: 0 key 4 opens the Telemetry mode (mode chip lights, the Encendido / Fuente / Alertas / Fallas strip rises, the
    /// propellers start), 1 source selector (L; the chip names the source: replay, simulator, SITL), 2 the "Vuelo" tab with TWO FIXED readings of
    /// different origin (altitude "medido", motor temperature "estimado": nothing turns an estimated reading into a measured one) and 3 the
    /// "Pilotar" toggle with its keys. Every word that states an origin or a source comes from labels.json (HonestyLabels / FlightOrigins);
    /// the other texts are keys of TelemetryStrings. With "Reducir animaciones" it is one still frame (55 % of the phase).
    /// </summary>
    internal sealed partial class TutorialAnim
    {
        private const int TelStep = 5;
        private static int PhasesOf(int step) => step == TelStep ? 4 : PhaseCount[step > TelStep ? step - 1 : step];

        private static readonly Color Ok = new Color(0.247f, 0.749f, 0.541f, 1f);
        private static readonly Color Warn = new Color(0.961f, 0.725f, 0.259f, 1f);
        private static readonly Color PanelBg = new Color(0.09f, 0.11f, 0.14f, 1f);

        private readonly Label[] _tel = new Label[10];
        private HonestyLabels _honesty;
        private bool _honestyTried;
        private int _telWhich = -1;

        private void InitTel()
        {
            for (int i = 0; i < _tel.Length; i++)
            {
                _tel[i] = new Label { pickingMode = PickingMode.Ignore };
                _tel[i].AddToClassList("dk-tut-tel");
                _tel[i].style.display = DisplayStyle.None;
                Add(_tel[i]);
            }
        }

        private HonestyLabels Honesty
        {
            get
            {
                if (_honesty == null && !_honestyTried)
                {
                    _honestyTried = true;
                    try { _honesty = HonestyLabels.Load(); }
                    catch (System.Exception e) { Debug.LogWarning("[TutorialAnim] honesty labels unavailable: " + e.Message); }
                }

                return _honesty;
            }
        }

        private static string Lang => Es ? "es" : "en";
        private static string Tx(string key) => TelemetryStrings.T(key, Lang);

        private void HideTel()
        {
            _telWhich = -1;
            for (int i = 0; i < _tel.Length; i++) _tel[i].style.display = DisplayStyle.None;
        }

        // role: null = plain light, "dim", or an origin tag class
        private void TelLabel(int i, string text, float x, float y, string role = null)
        {
            Label l = _tel[i];
            if (string.IsNullOrEmpty(text)) { l.style.display = DisplayStyle.None; return; }
            l.style.display = DisplayStyle.Flex;
            l.text = text;
            l.style.left = _ox + x * _k;
            l.style.top = _oy + y * _k - 9f;
            l.style.opacity = 1f;
            l.EnableInClassList("dk-tut-tel--dim", role == "dim");
            l.EnableInClassList("dk-tut-tel--pop", role == "pop");
            l.EnableInClassList("dk-tag", role == "measured" || role == "estimated");
            l.EnableInClassList("dk-tag--measured", role == "measured");
            l.EnableInClassList("dk-tag--estimated", role == "estimated");
        }

        private string SourceTitle(int which)
        {
            HonestyLabels h = Honesty;
            if (h == null) return null;
            string text;
            switch (which)
            {
                case 0: return h.ClassificationTitle(SourceClassification.Replay, Lang);
                case 1: return h.TrySimText("title", Lang, out text) ? text : null;
                default: return h.ClassificationTitle(SourceClassification.Sitl, Lang);
            }
        }

        private string Origin(ReadingOrigin o)
        {
            HonestyLabels h = Honesty;
            return h == null ? null : FlightOrigins.Text(o, Lang, h);
        }

        // ───────────────────────── labels per phase (called when the phase / language / geometry changes) ─────────────────────────

        private string PlaceTelTags(int ph)
        {
            bool t = Touch;
            switch (ph)
            {
                case 0:
                    TelLabel(1, "1", 27f, 24f, "dim"); TelLabel(2, "2", 77f, 24f, "dim"); TelLabel(3, "3", 127f, 24f, "dim");
                    Key(0, "4", 8, 176);
                    break;
                case 1:
                    TelLabel(0, SourceTitle(0), 90f, 21f);
                    TelLabel(1, Tx("src.replay"), 46f, 95f, "dim"); TelLabel(2, Tx("src.sim"), 46f, 112f, "dim"); TelLabel(3, Tx("src.sitl"), 46f, 129f, "dim");
                    Key(0, "L", 8, 176);
                    break;
                case 2:
                    TelLabel(0, SourceTitle(0), 90f, 21f);
                    TelLabel(1, Tx("tab.part"), 111f, 54f, "dim"); TelLabel(2, Tx("tab.flight"), 157f, 54f);
                    TelLabel(3, Tx("tut.tel.ill.alt"), 106f, 74f, "dim");
                    TelLabel(4, Origin(ReadingOrigin.Measured), 106f, 90f, "measured");
                    TelLabel(5, Tx("tut.tel.ill.motor"), 106f, 106f, "dim");
                    TelLabel(6, Origin(ReadingOrigin.Estimated), 106f, 122f, "estimated");
                    break;
                default:
                    TelLabel(0, SourceTitle(1), 90f, 21f);
                    TelLabel(1, Tx("pilot.legend.title"), 10f, 46f, "pop");
                    Key(0, "WASD", 8, 176); Key(1, "Esc", 56, 176);
                    break;
            }

            return Tx((t ? "tut.tel.cap" + ph + ".t" : "tut.tel.cap" + ph));
        }

        private void UpdateTel(float time)
        {
            if (_step != TelStep) return;
            float t = DesktopMotion.Reduce ? 0.55f : (time % PhaseSeconds) / PhaseSeconds;
            if (_phase == 1)
            {
                int which = t < 0.34f ? 0 : t < 0.67f ? 1 : 2;
                if (which != _telWhich)
                {
                    _telWhich = which;
                    TelLabel(0, SourceTitle(which), 90f, 21f);
                    for (int i = 0; i < 3; i++) _tel[1 + i].EnableInClassList("dk-tut-tel--dim", i != which);
                }
            }
            else if (_phase == 2)
            {
                // the two readings are fixed: they only fade in, one after the other. No label turns into another.
                _tel[3].style.opacity = _tel[4].style.opacity = Seg(t, 0.08f, 0.2f);
                _tel[5].style.opacity = _tel[6].style.opacity = Seg(t, 0.3f, 0.42f);
            }
            else if (_phase == 3)
            {
                _tel[1].style.opacity = Seg(t, 0.2f, 0.3f);
            }
        }

        // ───────────────────────── drawing ─────────────────────────

        private void Spin(Painter2D p, float cx, float cy, float sc, float yaw, float alpha)
        {
            float ang = DesktopMotion.Reduce ? 0.7f : _time * 16f;
            for (int i = 0; i < 4; i++)
            {
                Vector2 m = Motors[i];
                float a = ang + i * 0.9f;
                Vector2 e0 = DronePt(cx, cy, sc, yaw, m.x + Mathf.Cos(a) * 15f, m.y + Mathf.Sin(a) * 15f, -7f);
                Vector2 e1 = DronePt(cx, cy, sc, yaw, m.x - Mathf.Cos(a) * 15f, m.y - Mathf.Sin(a) * 15f, -7f);
                Line(p, e0, e1, A(Cel, 0.9f * alpha), 1.6f);
            }
        }

        private void MiniQuad(Painter2D p, Vector2 c, float r, Color col)
        {
            float d = r * 0.62f * _k;
            Line(p, c + new Vector2(-d, -d), c + new Vector2(d, d), col, 1.3f);
            Line(p, c + new Vector2(-d, d), c + new Vector2(d, -d), col, 1.3f);
            for (int i = 0; i < 4; i++) Circle(p, c + new Vector2(i % 2 == 0 ? -d : d, i < 2 ? -d : d), 2.1f, col, Color.clear, 1.2f);
        }

        // the strip of the Telemetry mode: Encendido (power dot) | Fuente | Alertas | Fallas
        private void TelStrip(Painter2D p, float rise, bool powerOn, int hot)
        {
            if (rise < 0.01f) return;
            float y0 = 148f + 26f * (1f - rise);
            RoundRect(p, 30f, y0, 150f, 24f, 12f, A(C2, 0.5f * rise), A(PanelBg, rise));
            for (int i = 1; i < 4; i++) Line(p, Pt(30f + 37.5f * i, y0 + 5f), Pt(30f + 37.5f * i, y0 + 19f), A(C2, 0.2f * rise), 1f);
            if (hot >= 0) RoundRect(p, 33f + 37.5f * hot, y0 + 2.5f, 31.5f, 19f, 9.5f, A(Cel, 0.9f * rise), A(Cel, 0.16f * rise));
            float cy = y0 + 12f;
            Circle(p, Pt(48.75f, cy), 4.5f, A(powerOn ? Ok : C2, rise * (powerOn ? 1f : 0.6f)), powerOn ? A(Ok, rise) : Color.clear, 1.5f);
            for (int i = 0; i < 3; i++) Line(p, Pt(80f, cy - 4f + i * 4f), Pt(88f + (i == 1 ? 4f : 0f), cy - 4f + i * 4f), A(C2, 0.75f * rise), 1.4f);
            Poly(p, new[] { Pt(123f, cy - 5f), Pt(129f, cy + 4f), Pt(117f, cy + 4f) }, 3, A(C2, 0.75f * rise), Color.clear);
            Vector2[] z = { Pt(155f, cy - 5f), Pt(151f, cy), Pt(157f, cy), Pt(153f, cy + 5f) };
            for (int i = 0; i < 3; i++) Line(p, z[i], z[i + 1], A(C2, 0.75f * rise), 1.4f);
        }

        private void Chip(Painter2D p, float alpha)
        {
            RoundRect(p, 66f, 9f, 138f, 24f, 12f, A(C2, 0.45f * alpha), A(PanelBg, alpha));
            Circle(p, Pt(79f, 21f), 3.4f, Color.clear, A(Ok, alpha), 1f);
        }

        private void Telemetry(Painter2D p)
        {
            float f = Fade();
            if (_ph == 0)
            {
                bool lit = _t > 0.22f;
                float glow = Mathf.Sin(Mathf.Clamp01(_t) * Mathf.PI);
                for (int i = 0; i < 4; i++)
                {
                    bool on = i == 3 && lit;
                    RoundRect(p, 12f + 50f * i, 11f, 42f, 26f, 13f, A(on ? Cel : C2, on ? 0.95f : 0.35f), on ? A(Cel, 0.2f + 0.1f * glow) : PanelBg);
                }

                MiniQuad(p, Pt(183f, 24f), 7f, A(lit ? C2 : C2, lit ? 1f : 0.55f));
                float rise = Seg(_t, 0.36f, 0.62f);
                Drone(p, 105f, 100f, 1.3f, 0.35f, f, 0f, 0, -1);
                if (_t > 0.5f) Spin(p, 105f, 100f, 1.3f, 0.35f, f * Seg(_t, 0.5f, 0.7f));
                TelStrip(p, rise, _t > 0.62f, -1);
                if (Touch) Finger(p, Pt(183f, 36f), Mathf.Sin(Mathf.Clamp01((_t - 0.1f) / 0.22f) * Mathf.PI) > 0.7f ? 1f : 0f);
            }
            else if (_ph == 1)
            {
                int which = _t < 0.34f ? 0 : _t < 0.67f ? 1 : 2;
                Chip(p, 1f);
                Drone(p, 105f, 56f, 1.05f, 0.35f, f, 0f, 0, -1);
                Spin(p, 105f, 56f, 1.05f, 0.35f, f);
                RoundRect(p, 22f, 84f, 166f, 56f, 8f, A(C2, 0.5f), PanelBg);
                for (int i = 0; i < 3; i++)
                {
                    float yc = 95f + 17f * i;
                    bool on = i == which;
                    if (on) RoundRect(p, 26f, yc - 8f, 158f, 16f, 6f, A(Cel, 0.6f), A(Cel, 0.16f));
                    Circle(p, Pt(36f, yc), 3.6f, A(on ? Cel : C2, on ? 1f : 0.5f), on ? A(Cel, 0.9f) : Color.clear, 1.4f);
                }

                TelStrip(p, 1f, true, 1);
                float yPtr = 95f + 17f * which;
                Ptr(p, Vector2.Lerp(Pt(150f, yPtr + 4f), Pt(150f, yPtr), 1f), Mathf.Sin(Mathf.Repeat(_t * 3f, 1f) * Mathf.PI) > 0.85f ? 1f : 0f);
            }
            else if (_ph == 2)
            {
                Chip(p, 1f);
                Drone(p, 52f, 92f, 0.85f, 0.35f, f, 0f, 0, -1);
                Spin(p, 52f, 92f, 0.85f, 0.35f, f);
                RoundRect(p, 98f, 40f, 106f, 98f, 8f, A(C2, 0.5f), PanelBg);
                RoundRect(p, 104f, 46f, 44f, 16f, 8f, A(C2, 0.3f), Color.clear);
                RoundRect(p, 150f, 46f, 50f, 16f, 8f, A(Cel, 0.9f), A(Cel, 0.16f));
                // two fixed readings: a value bar each; the origin tags are labels that fade in (never swap)
                float a1 = Seg(_t, 0.08f, 0.2f), a2 = Seg(_t, 0.3f, 0.42f);
                Line(p, Pt(176f, 90f), Pt(198f, 90f), A(C2, 0.9f * a1), 3f);
                Line(p, Pt(106f, 98f), Pt(196f, 98f), A(C2, 0.15f), 1f);
                Line(p, Pt(176f, 122f), Pt(198f, 122f), A(C2, 0.9f * a2), 3f);
                TelStrip(p, 1f, true, -1);
            }
            else
            {
                Chip(p, 1f);
                float on = Seg(_t, 0.12f, 0.24f);
                float climb = Seg(_t, 0.26f, 0.45f), side = Seg(_t, 0.45f, 0.65f), back = Seg(_t, 0.85f, 1f);
                float cx = 105f + 34f * side * (1f - back), cy = 80f - 14f * climb * (1f - back);
                float yaw = 0.35f + 0.5f * side * (1f - back);
                Drone(p, cx, cy, 1.1f, yaw, f, 0f, 0, -1);
                Spin(p, cx, cy, 1.1f, yaw, f);
                // pill under the strip: the Pilotar toggle and the four orders
                RoundRect(p, 20f, 114f, 170f, 26f, 13f, A(C2, 0.5f), PanelBg);
                RoundRect(p, 28f, 120f, 28f, 14f, 7f, A(on > 0.5f ? Cel : C2, 0.8f), on > 0.5f ? A(Cel, 0.3f) : Color.clear);
                Circle(p, Pt(Mathf.Lerp(35f, 49f, on), 127f), 4.4f, A(C2, 1f), A(on > 0.5f ? C2 : Bg, 1f), 1.2f);
                float hold = _t > 0.66f && _t < 0.86f ? 1f : 0f;
                for (int i = 0; i < 4; i++)
                {
                    Vector2 c = Pt(80f + 30f * i, 127f);
                    bool hot = i == 2 && hold > 0f;
                    Circle(p, c, 8.5f, A(hot ? Cel : C2, hot ? 1f : 0.4f), hot ? A(Cel, 0.25f) : Color.clear, 1.4f);
                    float g = 3.2f * _k;
                    Color gc = A(C2, 0.85f);
                    if (i == 0) Poly(p, new[] { c + new Vector2(0f, -g), c + new Vector2(g, g), c + new Vector2(-g, g) }, 3, gc, Color.clear);
                    else if (i == 1) Poly(p, new[] { c + new Vector2(0f, g), c + new Vector2(g, -g), c + new Vector2(-g, -g) }, 3, gc, Color.clear);
                    else if (i == 2) { Line(p, c + new Vector2(-g * 0.5f, -g), c + new Vector2(-g * 0.5f, g), gc, 1.6f); Line(p, c + new Vector2(g * 0.5f, -g), c + new Vector2(g * 0.5f, g), gc, 1.6f); }
                    else Circle(p, Pt(80f + 90f, 127f), 3.4f, gc, Color.clear, 1.4f);
                }

                TelStrip(p, 1f, true, -1);
                if (_t < 0.24f) Ptr(p, Vector2.Lerp(Pt(160f, 102f), Pt(42f, 130f), Seg(_t, 0f, 0.1f)), Seg(_t, 0.1f, 0.14f) * (1f - Seg(_t, 0.18f, 0.24f)));
            }
        }
    }
}
#endif
