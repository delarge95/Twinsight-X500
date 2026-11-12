using System.Collections.Generic;
using UnityEngine;

namespace WebGL.UI.ProceduralIcons
{
    public enum AnimKind
    {
        None,
        Burst,    // strokes move away from the icon centre and come back (settle = small overshoot)
        Pinch,    // strokes move towards the centre and come back
        Slice,    // a scalpel travels along the cut line leaving a cut, the strokes split along it, the two halves slide apart and close again
        Rotate,   // whole icon turns by Deg (360 = one full turn), eased
        Tilt,     // whole icon turns by Deg and comes back
        Pulse,    // scale 1 -> 1 + Amp -> 1
        Shift,    // each stroke moves by its own vector and comes back (static strokes: zero vector)
        Draw,     // strokes are drawn again from 30 % to 100 %
        Stagger,  // strokes grow upwards one after another (bars)
        Float,    // the icon rises Amp units and settles
        Wobble,   // damped swing of Deg degrees
        Fade,     // strokes with index < From fade towards transparent and back (the others stay: x-ray)
        DrawFrom  // strokes with index >= From are erased and drawn again (the others stay)
    }

    /// <summary>One icon's hover animation (a click replays it stronger and a bit faster). All numbers are in 24-grid units / degrees / ms.</summary>
    public sealed class AnimSpec
    {
        public AnimKind Kind;
        public float Amp;
        public float Deg;
        public int Ms = 420;
        public bool Loop;
        public bool Settle;
        public int From;
        public Vector2[] Shifts;
        public string Desc;
    }

    /// <summary>
    /// The single source of truth for the icon micro-animations (documented as a table in PLAN_PC.md; the native web app must reproduce them).
    /// Hover plays the animation once (a gentle loop only where Loop is true), click plays it again at ClickMul x amplitude.
    /// </summary>
    public static class OutlineIconAnim
    {
        public const float ClickMul = 1.8f;
        public const float ClickSpeed = 0.85f;   // click duration = Ms * ClickSpeed

        private static AnimSpec S(AnimKind k, string desc, float amp = 0f, float deg = 0f, int ms = 420, bool loop = false, bool settle = false, params Vector2[] shifts)
        {
            return new AnimSpec { Kind = k, Amp = amp, Deg = deg, Ms = ms, Loop = loop, Settle = settle, Shifts = shifts.Length > 0 ? shifts : null, Desc = desc };
        }

        private static AnimSpec SF(AnimKind k, string desc, int from, float amp = 0f, int ms = 420)
        {
            return new AnimSpec { Kind = k, From = from, Amp = amp, Ms = ms, Desc = desc };
        }

        private static Vector2 V(float x, float y) => new Vector2(x, y);

        public static readonly Dictionary<string, AnimSpec> Specs = new Dictionary<string, AnimSpec>
        {
            { "explode", S(AnimKind.Burst, "Las piezas salen hacia fuera desde el centro y se reasientan (rebote corto)", 3.4f, 0f, 560, false, true) },
            { "cut", S(AnimKind.Slice, "Un bisturí (hoja y mango) recorre la diagonal dejando una línea de corte; las dos mitades se deslizan a lo largo del corte y se cierran de nuevo", 2.3f, 0f, 760) },
            { "inspect", S(AnimKind.Pinch, "Las cuatro esquinas del visor se cierran sobre el punto y vuelven", 1.7f, 0f, 460) },
            { "focus", S(AnimKind.Pinch, "Las marcas de la mira se cierran hacia el centro y vuelven", 1.5f, 0f, 460) },
            { "isolate", S(AnimKind.Burst, "Las esquinas se abren y se cierran de nuevo alrededor de la pieza", 1.4f, 0f, 460) },
            { "analyze", S(AnimKind.Stagger, "Las barras crecen una tras otra", 0.38f, 0f, 560) },
            { "studio", S(AnimKind.Tilt, "El cubo se inclina y vuelve", 0f, 28f, 480) },
            { "solid", SF(AnimKind.DrawFrom, "Las líneas de sombreado de la cara se borran y se dibujan de nuevo (relleno de la cara)", 4, 0f, 700) },
            { "xray", SF(AnimKind.Fade, "Las caras delanteras se vuelven casi transparentes y quedan solo las aristas ocultas discontinuas; luego vuelven", 3, 0f, 700) },
            { "wire", S(AnimKind.Wobble, "La malla oscila como una jaula de alambre", 0f, 14f, 560) },
            { "realistic", S(AnimKind.Tilt, "La esfera gira y el brillo se desplaza", 0f, 40f, 520) },
            { "ghost", S(AnimKind.Float, "El fantasma flota hacia arriba y se asienta", 2.4f, 0f, 560) },
            { "thermal", S(AnimKind.Shift, "El mercurio sube dentro del termómetro", 0f, 0f, 520, false, false, V(0, 0), V(0, -1.8f)) },
            { "pins", S(AnimKind.Float, "El marcador salta y cae con un pequeño rebote", 2.8f, 0f, 520) },
            { "reset", S(AnimKind.Rotate, "Giro completo en sentido antihorario", 0f, -360f, 620) },
            { "home", S(AnimKind.Shift, "El tejado se levanta un instante", 0f, 0f, 420, false, false, V(0, -1.8f), V(0, 0), V(0, 0)) },
            { "power", S(AnimKind.Shift, "La línea de encendido sale hacia arriba y vuelve", 0f, 0f, 420, false, false, V(0, -2f), V(0, 0)) },
            { "settings", S(AnimKind.Shift, "Los deslizadores cruzan en sentido contrario", 0f, 0f, 520, false, false, V(0, 0), V(-4f, 0), V(4f, 0)) },
            { "language", S(AnimKind.Draw, "El globo se dibuja de nuevo", 0f, 0f, 700) },
            { "help", S(AnimKind.Pulse, "Late una vez", 0.14f, 0f, 420) },
            { "info", S(AnimKind.Pulse, "Late una vez", 0.14f, 0f, 420) },
            { "parts", S(AnimKind.Draw, "El árbol se dibuja rama a rama", 0f, 0f, 650) },
            { "search", S(AnimKind.Wobble, "La lupa oscila como buscando", 0f, 16f, 560) },
            { "panel_l", S(AnimKind.Shift, "El divisor se desplaza como un panel que se pliega", 0f, 0f, 420, false, false, V(0, 0), V(-2.4f, 0)) },
            { "panel_r", S(AnimKind.Shift, "El divisor se desplaza como un panel que se pliega", 0f, 0f, 420, false, false, V(0, 0), V(2.4f, 0)) },
            { "close", S(AnimKind.Rotate, "Giro de 90 grados", 0f, 90f, 360) },
            { "back", S(AnimKind.Shift, "La flecha se desplaza hacia atrás", 0f, 0f, 360, false, false, V(-2.2f, 0)) },
            { "chevron", S(AnimKind.Shift, "El chevrón avanza", 0f, 0f, 360, false, false, V(2.2f, 0)) },
            { "env_studio", S(AnimKind.Pulse, "La pantalla late", 0.1f, 0f, 420) },
            { "env_time", S(AnimKind.Rotate, "El sol gira despacio mientras se pasa el cursor (bucle suave)", 0f, 360f, 2600, true) },
            { "env_color", S(AnimKind.Wobble, "La paleta oscila", 0f, 14f, 520) },
            { "filter", S(AnimKind.Wobble, "El embudo se sacude como tamizando", 0f, 12f, 520) },
            { "invert", S(AnimKind.Rotate, "Las flechas dan media vuelta", 0f, 180f, 460) },
            { "angle", S(AnimKind.Wobble, "El ángulo se abre y se cierra", 0f, 10f, 520) },
            { "orbit", S(AnimKind.Rotate, "El anillo orbita una vuelta", 0f, 360f, 900) },
            { "pan", S(AnimKind.Pulse, "Las flechas se dilatan", 0.18f, 0f, 420) },
            { "zoom", S(AnimKind.Pulse, "La lupa se acerca", 0.2f, 0f, 440) },
            { "pointer", S(AnimKind.Shift, "El puntero hace clic (baja y vuelve)", 0f, 0f, 320, false, false, V(1.4f, 1.4f)) },
            { "keyboard", S(AnimKind.Pulse, "La tecla se hunde", 0.08f, 0f, 320) },
            { "check", S(AnimKind.Draw, "La marca se dibuja", 0f, 0f, 420) },
            { "propulsion", S(AnimKind.Rotate, "La hélice gira mientras se pasa el cursor (bucle suave)", 0f, 360f, 900, true) },
            { "avionics", S(AnimKind.Pulse, "El chip late", 0.12f, 0f, 420) },
            { "sensors", S(AnimKind.Pulse, "Las ondas se expanden", 0.16f, 0f, 480) },
            { "battery", S(AnimKind.Pulse, "La batería late", 0.1f, 0f, 420) },
            { "fastener", S(AnimKind.Tilt, "La tuerca da un cuarto de vuelta y vuelve", 0f, 60f, 460) },
            // web telemetry icons (design/icons_new): pasted from design/out/OutlineIconAnimSpecsNew.generated.cs.txt
            { "light", S(AnimKind.Pulse, "La bombilla late una vez", 0.14f, 0f, 420, false, false) },
            { "flight", S(AnimKind.Float, "El avión se eleva y vuelve con un pequeño rebote", 2.4f, 0f, 520, false, false) },
            { "link", S(AnimKind.Shift, "Los dos eslabones se separan un instante y vuelven", 0f, 0f, 420, false, false, V(1.2f, -1.2f), V(-1.2f, 1.2f)) },
            { "source", S(AnimKind.Draw, "Las ondas se dibujan de nuevo desde la antena", 0f, 0f, 520, false, false) },
            { "replay", S(AnimKind.Rotate, "Giro completo en sentido antihorario", 0f, -360f, 620, false, false) },
            { "sitl", S(AnimKind.Pulse, "La pantalla late una vez", 0.1f, 0f, 420, false, false) },
            { "alert", S(AnimKind.Pulse, "Late una vez", 0.14f, 0f, 420, false, false) },
            { "events", S(AnimKind.Draw, "Las líneas se dibujan una tras otra", 0f, 0f, 520, false, false) },
            { "fault", S(AnimKind.Wobble, "El rayo oscila amortiguado", 0f, 10f, 460, false, false) },
            { "takeoff", S(AnimKind.Shift, "La flecha sube y vuelve", 0f, 0f, 420, false, false, V(0f, -2f), V(0f, -2f), V(0f, 0f)) },
            { "land", S(AnimKind.Shift, "La flecha baja y vuelve", 0f, 0f, 420, false, false, V(0f, 2f), V(0f, 2f), V(0f, 0f)) },
            { "rtl", S(AnimKind.Shift, "La flecha se desplaza hacia el punto de partida y vuelve", 0f, 0f, 420, false, false, V(-1.6f, 0f), V(-1.6f, 0f), V(0f, 0f)) },
            { "hold", S(AnimKind.Pulse, "Late una vez", 0.12f, 0f, 420, false, false) },
            { "orbit_cmd", S(AnimKind.Rotate, "El dron da una vuelta alrededor del punto", 0f, 360f, 800, false, false) },
            { "gamepad", S(AnimKind.Tilt, "El mando se inclina y vuelve", 0f, 8f, 420, false, false) },
        };

        public static AnimSpec Get(string shape)
        {
            if (shape == null) return null;
            Specs.TryGetValue(shape, out AnimSpec s);
            return s;
        }

        public static float Ease(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        /// <summary>Per-stroke transform for progress p (0..1): offset, rotation (deg) and scale about the pivot, fraction drawn.</summary>
        public static void Transform(AnimSpec spec, int index, Vector2 centroid, float p, float mul, out Vector2 off, out float rot, out float sc, out float scy, out float draw, out Vector2 pivot, out float alpha)
        {
            off = Vector2.zero; rot = 0f; sc = 1f; scy = 1f; draw = 1f; alpha = 1f; pivot = new Vector2(12f, 12f);
            float e = Mathf.Sin(Mathf.Clamp01(p) * Mathf.PI);
            if (spec.Settle) e *= 1f + 0.28f * Mathf.Sin(p * Mathf.PI * 4f) * (1f - p);
            switch (spec.Kind)
            {
                case AnimKind.Burst:
                case AnimKind.Pinch:
                {
                    Vector2 d = centroid - pivot;
                    float sign = spec.Kind == AnimKind.Pinch ? -1f : 1f;
                    if (d.magnitude < 0.9f) sc = 1f + 0.16f * e * mul;
                    else off = d.normalized * spec.Amp * e * mul * sign;
                    break;
                }
                case AnimKind.Rotate: rot = spec.Deg * Ease(p); break;
                case AnimKind.Tilt: rot = spec.Deg * e * (mul > 1f ? 1.4f : 1f); break;
                case AnimKind.Pulse: sc = 1f + spec.Amp * e * mul; break;
                case AnimKind.Shift:
                    if (spec.Shifts != null) off = spec.Shifts[Mathf.Min(index, spec.Shifts.Length - 1)] * e * mul;
                    break;
                case AnimKind.Draw: draw = Mathf.Lerp(0.3f, 1f, Ease(p)); break;
                case AnimKind.Fade:
                    if (index < spec.From) alpha = Mathf.Clamp(1f - 0.62f * e * mul, 0.08f, 1f);
                    break;
                case AnimKind.DrawFrom:
                    if (index >= spec.From) draw = p < 0.15f ? Mathf.Lerp(1f, 0f, p / 0.15f) : Ease((p - 0.15f) / 0.85f);
                    break;
                case AnimKind.Stagger:
                    scy = 1f + spec.Amp * mul * Mathf.Sin(Mathf.PI * Mathf.Clamp01(p * 1.3f - index * 0.2f));
                    pivot = new Vector2(12f, 19f);
                    break;
                case AnimKind.Float: off = new Vector2(0f, -spec.Amp * e * mul * (1f + 0.2f * Mathf.Sin(p * Mathf.PI * 3f) * (1f - p))); break;
                case AnimKind.Wobble: rot = spec.Deg * mul * Mathf.Sin(p * Mathf.PI * 3f) * (1f - p); break;
            }
        }
    }
}
