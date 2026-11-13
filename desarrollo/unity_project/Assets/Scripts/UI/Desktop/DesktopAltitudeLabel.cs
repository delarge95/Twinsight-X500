#if UNITY_STANDALONE
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PLAN_PC 28.2 (PC only): the small altitude label next to the thin vertical line that joins the drone with its ground point (the line itself is
    /// drawn by <see cref="TelemetryGroundReference"/>). One chip with the altitude in metres, pinned to the middle of the line every frame like the
    /// fault markers; it never takes the pointer and is hidden when the line is (environment off, drone on the ground, no session, hero / onboarding).
    /// Design tokens only (accent border, highlight text, surface background), no new colours.
    /// </summary>
    internal sealed class DesktopAltitudeLabel
    {
        private readonly VisualElement _root;
        private readonly Label _chip;
        private string _last;

        public bool Visible => _chip.style.display != DisplayStyle.None;
        public string Text => _chip.text;
        public Vector2 PanelPosition => new Vector2(_chip.layout.x, _chip.layout.y);

        public DesktopAltitudeLabel(VisualElement root)
        {
            _root = root;
            _chip = new Label { name = "DkAltChip", pickingMode = PickingMode.Ignore };
            _chip.AddToClassList("dk-alt-chip");
            _chip.style.display = DisplayStyle.None;
            root.Add(_chip);
        }

        public void Dispose() { _chip.RemoveFromHierarchy(); }

        public void Tick(bool viewer)
        {
            var drv = TelemetryDroneDriver.Instance;
            var ground = drv != null ? drv.GroundRef : null;
            var cam = Camera.main;
            if (!viewer || ground == null || !ground.CueVisible || cam == null || _root.panel == null) { Hide(); return; }

            // both ends of the line in panel space; the label goes on the middle of the part of the line that is inside the free canvas
            // (below the top bar, clear of the rails and the status bar), so a long line (high altitude, close camera) never hides it off screen
            Vector3 top = ground.CueTop, foot = ground.CueFoot;
            if (cam.WorldToViewportPoint(top).z <= 0.01f) { Hide(); return; }
            Vector2 a = RuntimePanelUtils.CameraTransformWorldToPanel(_root.panel, top, cam);
            Vector3 footVp = cam.WorldToViewportPoint(foot);
            Vector2 b = footVp.z > 0.01f ? RuntimePanelUtils.CameraTransformWorldToPanel(_root.panel, foot, cam) : a;
            Rect rw = _root.worldBound;
            var view = new Rect(rw.xMin + 56f, rw.yMin + 60f, Mathf.Max(10f, rw.width - 112f), Mathf.Max(10f, rw.height - 60f - 48f));
            if (!ClipSegment(a, b, view, out float t0, out float t1)) { Hide(); return; }
            Vector2 p = Vector2.Lerp(a, b, (t0 + t1) * 0.5f);
            _chip.style.left = p.x + 8f;
            _chip.style.top = p.y - 9f;
            string text = ground.AltitudeMeters.ToString("0.0", CultureInfo.InvariantCulture) + " m";
            if (text != _last) { _last = text; _chip.text = text; }
            if (_chip.style.display == DisplayStyle.None) _chip.style.display = DisplayStyle.Flex;
        }

        /// <summary>Liang-Barsky: the parameter range [t0, t1] of the segment a-b that lies inside the rectangle.</summary>
        private static bool ClipSegment(Vector2 a, Vector2 b, Rect r, out float t0, out float t1)
        {
            t0 = 0f; t1 = 1f;
            float dx = b.x - a.x, dy = b.y - a.y;
            return Clip(-dx, a.x - r.xMin, ref t0, ref t1) && Clip(dx, r.xMax - a.x, ref t0, ref t1) && Clip(-dy, a.y - r.yMin, ref t0, ref t1) && Clip(dy, r.yMax - a.y, ref t0, ref t1);
        }

        private static bool Clip(float p, float q, ref float t0, ref float t1)
        {
            if (Mathf.Abs(p) < 1e-6f) return q >= 0f;
            float t = q / p;
            if (p < 0f) { if (t > t1) return false; if (t > t0) t0 = t; }
            else { if (t < t0) return false; if (t < t1) t1 = t; }
            return true;
        }

        private void Hide()
        {
            if (_chip.style.display != DisplayStyle.None) _chip.style.display = DisplayStyle.None;
        }
    }
}
#endif
