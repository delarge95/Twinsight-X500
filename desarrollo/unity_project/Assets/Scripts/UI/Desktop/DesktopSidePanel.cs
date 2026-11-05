#if UNITY_STANDALONE
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PC only (phase 2B, U1). Turns the bottom sheet of the part details into a docked panel on the right
    /// ("layout--desktop" class on the Root; the geometry lives in DesktopUI.uss). This class keeps the
    /// numbers that USS cannot express:
    ///  - panel width = 34 % of the (logical) window width clamped to 380-480 px (the UI scale of the 2A
    ///    already makes those logical px bigger on larger windows);
    ///  - the bottom bar (capsule + mode panels) is centred in the FREE area: its right edge moves left by
    ///    the panel width, so the capsule never sits under the panel and the mode sub-panels (CUT, FILTER,
    ///    RENDER MODE, ENVIRONMENT...) coexist with it without overlapping;
    ///  - the thermal legend moves left of the panel;
    ///  - the camera shifts the view sideways (AUX-U05) so the selected part is centred in the free area.
    /// Nothing here exists in WebGL (the bottom sheet keeps its 55 % behaviour there).
    /// </summary>
    internal sealed class DesktopSidePanel
    {
        public const float MinWidth = 380f;
        public const float MaxWidth = 480f;
        public const float WidthFraction = 0.34f;
        public const float Margin = 0f;   // phase 4: the inspector is docked flush to the edge

        private readonly VisualElement _root;
        private readonly VisualElement _sheet;
        private readonly VisualElement _bar;
        private readonly VisualElement _thermal;

        private bool _open;
        private float _leftShift = -1f;
        internal DesktopPartTree Tree;
        private float _width = -1f;
        private float _lastRootWidth = -1f;

        public bool IsOpen => _open;
        public float PanelWidth => _width;

        /// <summary>Space taken from the right edge by the panel and its two margins, in logical px (0 when closed).</summary>
        public float OccupiedWidth => _open ? _width + 2f * Margin : 0f;

        public DesktopSidePanel(VisualElement root)
        {
            _root = root;
            _sheet = root.Q<VisualElement>("BottomSheet");
            _bar = root.Q<VisualElement>("BottomBar");
            _thermal = root.Q<VisualElement>("ThermalLegendContainer");
            _root.AddToClassList("layout--desktop");
        }

        public void Dispose()
        {
            _root.RemoveFromClassList("layout--desktop");
            _root.RemoveFromClassList("layout--panel-open");
            if (_sheet != null) _sheet.style.width = StyleKeyword.Null;
            if (_bar != null) { _bar.style.right = StyleKeyword.Null; _bar.style.left = StyleKeyword.Null; }
            if (_thermal != null) _thermal.style.right = StyleKeyword.Null;
            OrbitCameraController.Instance?.SetDesktopViewShiftX(0f);
        }

        public void Tick()
        {
            if (_sheet == null)
            {
                return;
            }

            float logicalWidth = _root.worldBound.width;
            if (float.IsNaN(logicalWidth) || logicalWidth < 100f)
            {
                return;
            }

            bool changed = false;
            float width = logicalWidth < 1500f ? 300f : 340f;   // phase 4 (mockup): 340 px, 300 px on small windows
            if (!Mathf.Approximately(width, _width))
            {
                _width = width;
                _sheet.style.width = width;
                changed = true;
            }

            bool open = !_sheet.ClassListContains("details-sheet--hidden");
            if (open != _open)
            {
                _open = open;
                changed = true;
            }

            if (!Mathf.Approximately(logicalWidth, _lastRootWidth))
            {
                _lastRootWidth = logicalWidth;
                changed = true;
            }

            // The camera can appear after the UI: keep it informed every frame (cheap).
            float rightOcc = _open ? _width : DesktopChrome.RailWidth;
            float leftOcc = (Tree != null && Tree.IsOpen) ? DesktopPartTree.Width : DesktopChrome.RailWidth;
            OrbitCameraController.Instance?.SetDesktopViewShiftX((rightOcc - leftOcc) / (2f * logicalWidth));
            if (_bar != null) _bar.style.right = rightOcc;
            float left = leftOcc;
            if (!Mathf.Approximately(left, _leftShift))
            {
                _leftShift = left;
                if (_bar != null)
                {
                    if (left > 0f) _bar.style.left = left; else _bar.style.left = StyleKeyword.Null;
                }
            }

            if (!changed)
            {
                return;
            }

            _root.EnableInClassList("layout--panel-open", _open);

            float occupied = OccupiedWidth;
            if (_bar != null)
            {
                _bar.style.right = rightOcc;
            }

            if (_thermal != null)
            {
                if (_open) _thermal.style.right = occupied + 8f;
                else _thermal.style.right = StyleKeyword.Null;
            }
        }
    }
}
#endif
