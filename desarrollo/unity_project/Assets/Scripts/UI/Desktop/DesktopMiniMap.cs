#if UNITY_STANDALONE
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Telemetry;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Lote 1b, tool 1 (PC only): the drawing of the top-down map, with Painter2D. It draws from data it is GIVEN (the trail, the pose, the view, the geofence of
    /// the simulator, the test course and the wind) and all the maths is the pure <see cref="Minimap"/> (golden-tested). The look imitates the web's map: sober
    /// and monochrome, thin strokes, the accent only for the trail (design/tokens.json literals, the same as DesktopUI.uss). No base map and no geographic
    /// coordinates: the wire carries neither. The owner calls <see cref="Compute"/> (which also gives the positions of the text labels) and repaints only when
    /// something changed.
    /// </summary>
    internal sealed class DesktopMiniMap : VisualElement
    {
        // ── colours (tokens) ──
        static readonly Color Ring = new Color(1f, 1f, 1f, 0.055f);
        static readonly Color FenceLine = new Color(1f, 1f, 1f, 0.22f);
        static readonly Color FenceAlertLine = new Color(245 / 255f, 185 / 255f, 66 / 255f, 0.8f);     // state.warn
        static readonly Color Accent = new Color32(70, 175, 255, 255);                                  // color.accent #46afff
        static readonly Color Highlight = new Color32(191, 234, 255, 255);                              // color.highlight
        static readonly Color Muted = new Color32(139, 149, 163, 255);                                  // text.muted
        static readonly Color Secondary = new Color32(170, 180, 193, 255);                              // text.secondary
        static readonly Color TextDefault = new Color32(232, 237, 243, 255);                            // text.default

        // ── what to draw (set by the owner) ──
        public MapTrail Trail;
        public bool HasFrame;
        public double PosN, PosE, Yaw;
        public bool HeadingUp;
        public MapView View = new MapView(Minimap.MinScale, 0, 0);
        /// <summary>The geofence ring (simulator only), or null.</summary>
        public MapFence Fence;
        /// <summary>The test course when it is placed, or null; <see cref="CourseNext"/> = index of the next checkpoint (-1 = none).</summary>
        public CourseLayout Course;
        public int CourseNext = -1;
        /// <summary>Wind in force (NED, m/s); drawn only above 0.05 m/s.</summary>
        public double WindN, WindE;
        public float BarMaxPx = 80f;

        // ── computed (labels of the owner and the verification probe) ──
        public float Size { get; private set; }
        public Vector2 OriginPx { get; private set; }
        public Vector2 MePx { get; private set; }
        public Vector2 NorthPx { get; private set; }
        public Vector2 BarCenterPx { get; private set; }
        public Vector2 WindCenterPx { get; private set; }
        public double BarMetres { get; private set; }
        public double BarPx { get; private set; }
        /// <summary>Rad clockwise from the top of the screen.</summary>
        public double ArrowAngle { get; private set; }
        public double NorthAngle { get; private set; }
        public bool FenceAlert { get; private set; }
        public float FenceRadiusPx { get; private set; }
        public bool WindDrawn { get; private set; }
        public Vector2 WindDirPx { get; private set; }
        public float WindLengthPx { get; private set; }
        public int TrailPointsDrawn { get; private set; }
        public bool FenceDrawn => Fence != null;
        public bool CourseDrawn => Course != null;
        public int Repaints { get; private set; }

        public DesktopMiniMap()
        {
            pickingMode = PickingMode.Position;
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => { Compute(); MarkDirtyRepaint(); });
        }

        private Vector2 ToXY(double n, double e)
        {
            Minimap.ToScreen(n, e, View, HeadingUp, Yaw, out double x, out double y);
            float c = Size / 2f;
            return new Vector2(c + (float)x, c + (float)y);
        }

        /// <summary>World point to the pixel of this element (after <see cref="Compute"/>).</summary>
        public Vector2 WorldToPx(double n, double e) => ToXY(n, e);

        /// <summary>Everything the labels and the drawing share. Cheap: no allocation.</summary>
        public void Compute()
        {
            float size = contentRect.width;
            if (float.IsNaN(size) || size < 8f) size = resolvedStyle.width;
            if (float.IsNaN(size) || size < 8f) return;
            Size = size;
            float c = size / 2f;
            Minimap.ViewAngles(HeadingUp, Yaw, out double arrow, out double north);
            ArrowAngle = arrow;
            NorthAngle = north;
            OriginPx = ToXY(0, 0);
            MePx = ToXY(PosN, PosE);
            float rim = c - 15f;
            NorthPx = new Vector2(c + rim * (float)Math.Sin(north), c - rim * (float)Math.Cos(north));
            Minimap.ScaleBar(View.scale, BarMaxPx, out double metres, out double px);
            BarMetres = metres;
            BarPx = px;
            BarCenterPx = new Vector2(c, size - 20f);
            FenceRadiusPx = Fence != null ? (float)(Fence.radius / View.scale) : 0f;
            // amber only as an alert: the vehicle is within 5 m of the ring (the 3D scene uses the same margin)
            FenceAlert = Fence != null && HasFrame && Math.Sqrt((PosN - Fence.n) * (PosN - Fence.n) + (PosE - Fence.e) * (PosE - Fence.e)) > Fence.radius - Geofence.NearM;

            double speed = Math.Sqrt(WindN * WindN + WindE * WindE);
            WindDrawn = speed >= 0.05;
            if (WindDrawn)
            {
                // screen direction of the vector, whatever the orientation of the map
                Vector2 o = ToXY(0, 0), q = ToXY(WindN / speed, WindE / speed);
                Vector2 d = q - o;
                float len = d.magnitude;
                WindDirPx = len > 1e-6f ? d / len : new Vector2(0f, -1f);
                WindLengthPx = 14f + (float)Math.Min(12, speed) * 2f;
                WindCenterPx = new Vector2(size * 0.8f, size * 0.72f);
            }
        }

        private static Color A(Color c, float a) { c.a = a; return c; }

        private void Draw(MeshGenerationContext ctx)
        {
            Compute();
            float size = Size;
            if (size < 8f) return;
            Repaints++;
            Painter2D p = ctx.painter2D;
            float c = size / 2f;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;

            // faint reference: a ring at half the radius and the cross
            p.lineWidth = 1f;
            p.strokeColor = Ring;
            p.BeginPath(); p.Arc(new Vector2(c, c), c * 0.5f, 0f, 360f); p.Stroke();
            p.BeginPath(); p.MoveTo(new Vector2(c, 0f)); p.LineTo(new Vector2(c, size)); p.Stroke();
            p.BeginPath(); p.MoveTo(new Vector2(0f, c)); p.LineTo(new Vector2(size, c)); p.Stroke();

            // geofence of the simulator
            if (Fence != null && FenceRadiusPx > 0.5f && FenceRadiusPx < 20000f)
            {
                p.strokeColor = FenceAlert ? FenceAlertLine : FenceLine;
                p.BeginPath(); p.Arc(ToXY(Fence.n, Fence.e), FenceRadiusPx, 0f, 360f); p.Stroke();
            }

            if (Course != null) DrawCourse(p);
            DrawTrail(p);

            // origin: a small circle (the «H» is a label of the owner)
            p.lineWidth = 1f;
            p.strokeColor = Muted;
            p.BeginPath(); p.Arc(OriginPx, 3f, 0f, 360f); p.Stroke();

            // the vehicle: an arrow that points where the nose points
            if (HasFrame)
            {
                float ca = (float)Math.Cos(ArrowAngle), sa = (float)Math.Sin(ArrowAngle);
                Vector2 R(float x, float y) => new Vector2(MePx.x + x * ca - y * sa, MePx.y + x * sa + y * ca);
                p.fillColor = Highlight;
                p.BeginPath();
                p.MoveTo(R(0f, -7.5f)); p.LineTo(R(5f, 6f)); p.LineTo(R(0f, 3f)); p.LineTo(R(-5f, 6f)); p.ClosePath();
                p.Fill();
            }

            // scale bar
            if (BarPx > 1)
            {
                float half = (float)BarPx / 2f, y = BarCenterPx.y;
                p.lineWidth = 1f;
                p.strokeColor = Muted;
                p.BeginPath(); p.MoveTo(new Vector2(c - half, y)); p.LineTo(new Vector2(c + half, y)); p.Stroke();
                p.BeginPath(); p.MoveTo(new Vector2(c - half, y - 2f)); p.LineTo(new Vector2(c - half, y + 2f)); p.Stroke();
                p.BeginPath(); p.MoveTo(new Vector2(c + half, y - 2f)); p.LineTo(new Vector2(c + half, y + 2f)); p.Stroke();
            }

            // north: a tick on the rim (the «N» is a label of the owner)
            {
                float sn = (float)Math.Sin(NorthAngle), cn = (float)Math.Cos(NorthAngle);
                p.lineWidth = 1f;
                p.strokeColor = Muted;
                p.BeginPath();
                p.MoveTo(new Vector2(c + (c - 1f) * sn, c - (c - 1f) * cn));
                p.LineTo(new Vector2(c + (c - 7f) * sn, c - (c - 7f) * cn));
                p.Stroke();
            }

            // wind of the simulator: a small arrow that points where the wind blows to (length grows with the speed)
            if (WindDrawn)
            {
                Vector2 u = WindDirPx;
                Vector2 tail = WindCenterPx - u * (WindLengthPx / 2f), tip = WindCenterPx + u * (WindLengthPx / 2f);
                const float h = 3.4f;
                Vector2 left = new Vector2(tip.x - u.x * h * 1.6f - u.y * h, tip.y - u.y * h * 1.6f + u.x * h);
                Vector2 right = new Vector2(tip.x - u.x * h * 1.6f + u.y * h, tip.y - u.y * h * 1.6f - u.x * h);
                p.lineWidth = 1.2f;
                p.strokeColor = Secondary;
                p.BeginPath(); p.MoveTo(tail); p.LineTo(tip); p.Stroke();
                p.fillColor = Secondary;
                p.BeginPath(); p.MoveTo(tip); p.LineTo(left); p.LineTo(right); p.ClosePath(); p.Fill();
            }
        }

        /// <summary>The trail as a few strokes that fade in towards the present (oldest faintest); neighbours share a point so the line stays continuous.</summary>
        private void DrawTrail(Painter2D p)
        {
            int count = Trail != null ? Trail.Count : 0;
            TrailPointsDrawn = count;
            if (count < 2) return;
            int n = Math.Min(8, count - 1);
            p.lineWidth = 1.5f;
            for (int k = 0; k < n; k++)
            {
                int a = (int)Math.Floor((count - 1) * (double)k / n);
                int b = (int)Math.Floor((count - 1) * (double)(k + 1) / n);
                p.strokeColor = A(Accent, 0.14f + 0.76f * (k + 1) / n);
                p.BeginPath();
                p.MoveTo(ToXY(Trail.N(a), Trail.E(a)));
                for (int i = a + 1; i <= b; i++) p.LineTo(ToXY(Trail.N(i), Trail.E(i)));
                p.Stroke();
            }
        }

        /// <summary>The simulated test course seen from above: wall, zones, the bar of each ring, pylons and the pad. The next checkpoint takes the accent.</summary>
        private void DrawCourse(Painter2D p)
        {
            CourseLayout L = Course;
            bool hasNext = CourseNext >= 0 && CourseNext < L.Checkpoints.Count;
            Checkpoint next = hasNext ? L.Checkpoints[CourseNext] : default(Checkpoint);

            void Rect(double n0, double n1, double e0, double e1)
            {
                p.BeginPath();
                p.MoveTo(ToXY(n0, e0)); p.LineTo(ToXY(n0, e1)); p.LineTo(ToXY(n1, e1)); p.LineTo(ToXY(n1, e0)); p.ClosePath();
                p.Stroke();
            }

            p.lineWidth = 1.5f;
            p.strokeColor = A(Secondary, 0.7f);
            foreach (CourseBox b in L.Wall) Rect(b.N0, b.N1, b.E0, b.E1);

            for (int i = 0; i < L.Zones.Count; i++)
            {
                CourseZone z = L.Zones[i];
                bool isNext = hasNext && next.Kind == CheckpointKind.Zone && next.Index == i;
                p.lineWidth = 1f;
                p.strokeColor = isNext ? A(Accent, 0.9f) : A(Secondary, 0.35f);
                Rect(z.N - z.HalfN, z.N + z.HalfN, z.E - z.HalfE, z.E + z.HalfE);
            }

            for (int i = 0; i < L.Gates.Count; i++)
            {
                CourseGate g = L.Gates[i];
                bool isNext = hasNext && next.Kind == CheckpointKind.Gate && next.Index == i;
                double az = g.AzimuthDeg * Math.PI / 180.0;
                // the bar across the ring: perpendicular to its heading (north = cos, east = sin)
                Vector2 a = ToXY(g.N + g.Radius * Math.Sin(az), g.E - g.Radius * Math.Cos(az));
                Vector2 b = ToXY(g.N - g.Radius * Math.Sin(az), g.E + g.Radius * Math.Cos(az));
                p.lineWidth = isNext ? 2f : 1.5f;
                p.strokeColor = isNext ? Accent : A(TextDefault, 0.6f);
                p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke();
            }

            p.fillColor = A(Secondary, 0.6f);
            foreach (CoursePylon py in L.Pylons)
            {
                p.BeginPath(); p.Arc(ToXY(py.N, py.E), 1.8f, 0f, 360f); p.Fill();
            }

            p.lineWidth = 1f;
            p.strokeColor = A(Muted, 0.6f);
            p.BeginPath(); p.Arc(ToXY(L.PadN, L.PadE), Mathf.Max(3f, (float)(L.PadRadius / View.scale)), 0f, 360f); p.Stroke();
        }
    }
}
#endif
