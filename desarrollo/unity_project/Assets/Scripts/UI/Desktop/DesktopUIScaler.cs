#if UNITY_STANDALONE
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PC only (U4). Replaces the "Scale With Screen Size / match 0.5" behaviour of the shared
    /// PanelSettings (which shrank the UI to 0.67 at 1280x720, leaving 32 px buttons) by a constant
    /// pixel scale derived from the window height:
    ///   scale = clamp(height / 1080, 0.92, 2.0) * "UI size" (80-150 %, AccessibilityManager).
    /// 0.92 is the floor that keeps the smallest control (48 px) at 44 px or more; below ~900 logical px
    /// of height the <c>ui-compact</c> class shrinks cards and the capsule so the Studio panel still fits.
    /// The PanelSettings asset is modified in memory only and restored on exit (no asset diff).
    /// </summary>
    internal sealed class DesktopUIScaler
    {
        public const float FloorScale = 0.92f;
        public const float MaxScale = 2.0f;
        private const float CompactBelowLogicalHeight = 900f;

        private readonly VisualElement _root;
        private readonly PanelSettings _panelSettings;

        private readonly PanelScaleMode _origMode;
        private readonly float _origScale;
        private readonly Vector2Int _origReference;
        private readonly PanelScreenMatchMode _origMatchMode;
        private readonly float _origMatch;

        private int _lastW = -1;
        private int _lastH = -1;
        private float _lastUser = -1f;

        public float CurrentScale { get; private set; } = 1f;
        public Vector2 LogicalSize { get; private set; }

        public DesktopUIScaler(UIDocument document)
        {
            _root = document.rootVisualElement;
            _panelSettings = document.panelSettings;
            if (_panelSettings != null)
            {
                _origMode = _panelSettings.scaleMode;
                _origScale = _panelSettings.scale;
                _origReference = _panelSettings.referenceResolution;
                _origMatchMode = _panelSettings.screenMatchMode;
                _origMatch = _panelSettings.match;
            }
        }

        public static float UserScale
        {
            get
            {
                AccessibilityManager a = AccessibilityManager.Instance;
                return a != null ? Mathf.Clamp(a.UIScale, 0.8f, 1.5f) : 1f;
            }
        }

        public void Tick()
        {
            if (_panelSettings == null) return;

            float user = UserScale;
            if (Screen.width == _lastW && Screen.height == _lastH && Mathf.Approximately(user, _lastUser))
            {
                return;
            }

            _lastW = Screen.width;
            _lastH = Screen.height;
            _lastUser = user;
            Apply(user);
        }

        private void Apply(float user)
        {
            float height = Mathf.Max(1, Screen.height);
            float scale = Mathf.Clamp(height / 1080f * user, FloorScale, MaxScale);

            _panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            _panelSettings.scale = scale;

            CurrentScale = scale;
            LogicalSize = new Vector2(Screen.width / scale, Screen.height / scale);

            _root?.EnableInClassList("ui-compact", LogicalSize.y < CompactBelowLogicalHeight);
        }

        public void Restore()
        {
            if (_panelSettings == null) return;

            _panelSettings.scaleMode = _origMode;
            _panelSettings.scale = _origScale;
            _panelSettings.referenceResolution = _origReference;
            _panelSettings.screenMatchMode = _origMatchMode;
            _panelSettings.match = _origMatch;
            _root?.RemoveFromClassList("ui-compact");
        }
    }
}
#endif
