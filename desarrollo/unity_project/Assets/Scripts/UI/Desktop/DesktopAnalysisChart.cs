#if UNITY_STANDALONE
using System;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Telemetry;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PC only (E-4, PLAN_PC 23). The drawing of the "Analisis" tab, with Painter2D (UI Toolkit). Both elements draw from data they are
    /// GIVEN (the rings of <see cref="FlightHistory"/> and the <see cref="MarkerLog"/>), allocate nothing per redraw (no lists, no strings,
    /// no closures: the points go straight to the painter) and are only repainted by their owner when something changed (at most 10 Hz).
    /// Colours are the tokens of design/tokens.json (the same literals as DesktopUI.uss).
    /// </summary>
    internal static class AnalysisColors
    {
        public static readonly Color Grid = new Color(1f, 1f, 1f, 0.09f);        // line.default
        public static readonly Color GridStrong = new Color(1f, 1f, 1f, 0.16f);  // line.strong
        public static readonly Color Accent = new Color32(70, 175, 255, 255);    // color.accent
        public static readonly Color Highlight = new Color32(191, 234, 255, 255); // color.highlight
        public static readonly Color Secondary = new Color32(170, 180, 193, 255); // text.secondary
        public static readonly Color Muted = new Color32(139, 149, 163, 255);    // text.muted
        public static readonly Color Brand = new Color32(255, 138, 42, 255);     // brand
        public static readonly Color Warn = new Color32(245, 185, 66, 255);      // state.warn
        public static readonly Color Crit = new Color32(255, 77, 77, 255);       // state.crit

        /// <summary>Line colours of the lines of a chart, in order (single line: accent; speed: accent + highlight; motors 1..4).</summary>
        public static readonly Color[] Lines = { Accent, Highlight, Secondary, Brand };

        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }
    }

    /// <summary>One chart: faint grid, the lines of its <see cref="ChartSpec"/> (a gap breaks the line), a dot on the newest sample, faint ticks at the alert / fault markers.</summary>
    internal sealed class AnalysisPlot : VisualElement
    {
        private const float Inset = 2.5f;   // keeps the dot and the stroke of the newest sample inside the element

        public Series[] Lines;
        public int LineCount;
        public double MinSpan = 1;
        public MarkerLog Markers;
        public long TotalSamples;
        public int Capacity = HistoryConstants.Length;
        public SparkRange Range;

        public AnalysisPlot()
        {
            pickingMode = PickingMode.Position;
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        /// <summary>Horizontal extent the samples use (the owner maps the pointer with it).</summary>
        public float PlotWidth => Mathf.Max(1f, contentRect.width - 2f * Inset);

        private void Draw(MeshGenerationContext ctx)
        {
            Rect r = contentRect;
            if (r.width < 8f || r.height < 8f || Lines == null) return;
            Painter2D p = ctx.painter2D;
            float w = r.width - 2f * Inset;
            float top = Inset, h = r.height - 2f * Inset;
            float bottom = top + h;

            // grid: top / middle / bottom
            p.lineWidth = 1f;
            p.strokeColor = AnalysisColors.Grid;
            for (int g = 0; g < 3; g++)
            {
                float y = top + h * 0.5f * g;
                p.BeginPath(); p.MoveTo(new Vector2(Inset, y)); p.LineTo(new Vector2(Inset + w, y)); p.Stroke();
            }

            // markers: faint vertical ticks (alerts) and the span of a fault
            if (Markers != null)
            {
                float step = (float)Sparkline.Step(w, Capacity);
                for (int m = 0; m < Markers.Count; m++)
                {
                    Marker mk = Markers[m];
                    long age = TotalSamples - 1 - mk.Sample;
                    if (age < 0 || age >= Capacity) continue;
                    float x = Inset + w - age * step;
                    Color c = mk.Kind == MarkerKind.Fault || mk.Severity == AlertSeverity.Critical ? AnalysisColors.Crit : AnalysisColors.Warn;
                    p.strokeColor = AnalysisColors.WithAlpha(c, 0.35f);
                    p.BeginPath(); p.MoveTo(new Vector2(x, top)); p.LineTo(new Vector2(x, bottom)); p.Stroke();
                }
            }

            // the lines
            p.lineWidth = 1.5f;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;
            for (int l = 0; l < LineCount; l++)
            {
                Series s = Lines[l];
                int n = s.Count;
                if (n == 0) continue;
                p.strokeColor = AnalysisColors.Lines[Mathf.Min(l, AnalysisColors.Lines.Length - 1)];
                p.BeginPath();
                bool pen = false;
                float lastX = 0, lastY = 0;
                bool haveLast = false;
                for (int i = 0; i < n; i++)
                {
                    double v = s[i];
                    if (double.IsNaN(v) || double.IsInfinity(v)) { pen = false; haveLast = false; continue; }
                    float x = Inset + (float)Sparkline.X(i, n, w, Capacity);
                    float y = top + (float)Sparkline.Y(v, Range.Lo, Range.Span, h);
                    if (!pen) { p.MoveTo(new Vector2(x, y)); pen = true; } else p.LineTo(new Vector2(x, y));
                    lastX = x; lastY = y; haveLast = true;
                }
                p.Stroke();
                if (haveLast && !double.IsNaN(s.Last))
                {
                    p.fillColor = p.strokeColor;
                    p.BeginPath();
                    p.Arc(new Vector2(lastX, lastY), 2.2f, 0f, 360f);
                    p.Fill();
                }
            }
        }
    }

    /// <summary>The alert / fault marker strip on the shared time axis: a tick with a dot per alert (amber warning, red critical) and a bar per synthetic fault.</summary>
    internal sealed class AnalysisMarkerStrip : VisualElement
    {
        private const float Inset = 2.5f;

        public MarkerLog Markers;
        public long TotalSamples;
        public int Capacity = HistoryConstants.Length;

        public AnalysisMarkerStrip()
        {
            pickingMode = PickingMode.Position;
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        private void Draw(MeshGenerationContext ctx)
        {
            Rect r = contentRect;
            if (r.width < 8f || r.height < 6f) return;
            Painter2D p = ctx.painter2D;
            float w = r.width - 2f * Inset;
            float baseY = r.height - 3f;

            p.lineWidth = 1f;
            p.strokeColor = AnalysisColors.GridStrong;
            p.BeginPath(); p.MoveTo(new Vector2(Inset, baseY)); p.LineTo(new Vector2(Inset + w, baseY)); p.Stroke();
            if (Markers == null) return;

            float step = (float)Sparkline.Step(w, Capacity);
            // faults first (bars under the ticks)
            for (int m = 0; m < Markers.Count; m++)
            {
                Marker mk = Markers[m];
                if (mk.Kind != MarkerKind.Fault) continue;
                long age = TotalSamples - 1 - mk.Sample;
                if (age < 0 || age >= Capacity + mk.Span) continue;
                float x0 = Inset + w - age * step;
                float x1 = Mathf.Min(Inset + w, x0 + mk.Span * step);
                x0 = Mathf.Max(Inset, x0);
                if (x1 <= x0) continue;
                p.fillColor = AnalysisColors.WithAlpha(AnalysisColors.Crit, 0.30f);
                p.strokeColor = AnalysisColors.WithAlpha(AnalysisColors.Crit, 0.85f);
                p.lineWidth = 1f;
                p.BeginPath();
                p.MoveTo(new Vector2(x0, baseY - 5f)); p.LineTo(new Vector2(x1, baseY - 5f)); p.LineTo(new Vector2(x1, baseY)); p.LineTo(new Vector2(x0, baseY)); p.ClosePath();
                p.Fill();
                p.Stroke();
            }

            for (int m = 0; m < Markers.Count; m++)
            {
                Marker mk = Markers[m];
                if (mk.Kind != MarkerKind.Alert) continue;
                long age = TotalSamples - 1 - mk.Sample;
                if (age < 0 || age >= Capacity) continue;
                float x = Inset + w - age * step;
                Color c = mk.Severity == AlertSeverity.Critical ? AnalysisColors.Crit : AnalysisColors.Warn;
                p.strokeColor = c;
                p.lineWidth = 1.5f;
                p.BeginPath(); p.MoveTo(new Vector2(x, baseY)); p.LineTo(new Vector2(x, 4f)); p.Stroke();
                p.fillColor = c;
                p.BeginPath(); p.Arc(new Vector2(x, 4f), 2.6f, 0f, 360f); p.Fill();
            }
        }
    }
}
#endif
