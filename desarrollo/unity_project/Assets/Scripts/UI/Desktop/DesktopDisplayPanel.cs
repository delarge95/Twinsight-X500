#if UNITY_STANDALONE
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PC only (U4 + U5). Adds a DISPLAY section at the end of the CONFIG submenu (the shared UXML
    /// is not touched): window mode, resolution, vsync and "UI size" (80-150 %).
    /// </summary>
    internal sealed class DesktopDisplayPanel
    {
        private readonly Button _modeBtn;
        private readonly Button _resolutionBtn;
        private readonly Button _vsyncBtn;
        private readonly Slider _uiSizeSlider;
        private readonly Label _uiSizeLabel;
        private readonly VisualElement _section;
        private readonly Label _sectionTitle;
        private readonly Label _modeLabel;
        private readonly Label _resolutionLabel;
        private readonly Label _vsyncLabel;
        private readonly Button _motionBtn;
        private readonly Label _motionLabel;

        private List<Vector2Int> _options = new List<Vector2Int>();

        public DesktopDisplayPanel(VisualElement root)
        {
            VisualElement config = root.Q<VisualElement>("HeroSubmenu_Config");
            ScrollView scroll = config?.Q<ScrollView>();
            VisualElement parent = scroll != null ? scroll.contentContainer : config;
            if (parent == null)
            {
                return;
            }

            _sectionTitle = new Label("DISPLAY");
            _sectionTitle.AddToClassList("config-section-label");
            parent.Add(_sectionTitle);

            _section = new VisualElement { name = "DesktopDisplaySection" };
            _section.AddToClassList("desktop-display-section");
            parent.Add(_section);

            // Window mode
            VisualElement modeRow = NewRow(out _modeLabel, "WINDOW MODE", out _modeBtn, "DesktopWindowModeBtn");
            _section.Add(modeRow);
            _modeBtn.clicked += () => DesktopWindowSettings.ToggleFullscreen();

            // Resolution
            VisualElement resRow = NewRow(out _resolutionLabel, "RESOLUTION", out _resolutionBtn, "DesktopResolutionBtn");
            _section.Add(resRow);
            _resolutionBtn.clicked += CycleResolution;

            // VSync
            VisualElement vsyncRow = NewRow(out _vsyncLabel, "VSYNC", out _vsyncBtn, "DesktopVSyncBtn");
            _section.Add(vsyncRow);
            _vsyncBtn.clicked += () =>
            {
                DesktopWindowSettings.VSync = !DesktopWindowSettings.VSync;
                Refresh();
            };

            // Reduce animations (saved): zeroes every UI transition and shows the tutorial illustrations still
            VisualElement motionRow = NewRow(out _motionLabel, "REDUCE ANIMATIONS", out _motionBtn, "DesktopReduceMotionBtn");
            _section.Add(motionRow);
            _motionBtn.clicked += () => { DesktopMotion.Reduce = !DesktopMotion.Reduce; Refresh(); };

            // UI size (80-150 %) -> AccessibilityManager
            var sliderGroup = new VisualElement();
            sliderGroup.AddToClassList("env-slider-group");
            _uiSizeLabel = new Label("UI SIZE");
            _uiSizeLabel.AddToClassList("env-slider-label");
            _uiSizeSlider = new Slider(80f, 150f) { name = "DesktopUiSizeSlider" };
            _uiSizeSlider.AddToClassList("glass-slider");
            _uiSizeSlider.AddToClassList("env-slider");
            sliderGroup.Add(_uiSizeLabel);
            sliderGroup.Add(_uiSizeSlider);
            _section.Add(sliderGroup);

            _uiSizeSlider.RegisterValueChangedCallback(evt =>
            {
                AccessibilityManager a = AccessibilityManager.Instance;
                if (a != null)
                {
                    a.SetUIScale(Mathf.Round(evt.newValue) / 100f);
                }

                UpdateUiSizeLabel(evt.newValue);
            });

            // Keep the 3D view responsive while dragging the slider (same guard as the other sliders).
            _uiSizeSlider.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0) InputManager.InputBlocked = true;
                evt.StopPropagation();
            });
            _uiSizeSlider.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.button == 0) InputManager.InputBlocked = false;
            });

            // Same input guard as every other button.
            foreach (Button b in new[] { _modeBtn, _resolutionBtn, _vsyncBtn, _motionBtn })
            {
                b.RegisterCallback<PointerDownEvent>(evt => { if (evt.button == 0) InputManager.InputBlocked = true; evt.StopPropagation(); });
                b.RegisterCallback<PointerUpEvent>(evt => { if (evt.button == 0) InputManager.InputBlocked = false; evt.StopPropagation(); });
            }

            DesktopWindowSettings.Changed += Refresh;
            AppLanguageManager.LanguageChanged += OnLanguageChanged;
            Refresh();
        }

        public void Dispose()
        {
            DesktopWindowSettings.Changed -= Refresh;
            AppLanguageManager.LanguageChanged -= OnLanguageChanged;
        }

        private static VisualElement NewRow(out Label label, string text, out Button button, string buttonName)
        {
            var row = new VisualElement();
            row.AddToClassList("config-row");
            label = new Label(text);
            label.AddToClassList("config-row-label");
            button = new Button { name = buttonName };
            button.AddToClassList("config-cycle-btn");
            row.Add(label);
            row.Add(button);
            return row;
        }

        private void OnLanguageChanged(string _)
        {
            Refresh();
        }

        private void CycleResolution()
        {
            if (DesktopWindowSettings.IsFullscreen)
            {
                return;
            }

            _options = DesktopWindowSettings.GetResolutionOptions();
            if (_options.Count == 0)
            {
                return;
            }

            int index = _options.FindIndex(s => s.x == Screen.width && s.y == Screen.height);
            Vector2Int next = _options[(index + 1) % _options.Count];
            DesktopWindowSettings.SetWindowedSize(next);
        }

        private static string T(string s) => AppLanguageManager.TranslateStatic(s);

        public void Refresh()
        {
            if (_section == null)
            {
                return;
            }

            _sectionTitle.text = T("DISPLAY");
            _modeLabel.text = T("WINDOW MODE");
            _resolutionLabel.text = T("RESOLUTION");
            _vsyncLabel.text = "VSYNC";

            bool full = DesktopWindowSettings.IsFullscreen;
            _modeBtn.text = T(full ? "FULLSCREEN" : "WINDOWED");
            _resolutionBtn.text = Screen.width + " x " + Screen.height;
            _resolutionBtn.SetEnabled(!full);
            _vsyncBtn.text = DesktopWindowSettings.VSync ? "ON" : "OFF";
            _motionLabel.text = AppLanguageManager.IsSpanish ? "REDUCIR ANIMACIONES" : "REDUCE ANIMATIONS";
            _motionBtn.text = DesktopMotion.Reduce ? "ON" : "OFF";

            AccessibilityManager a = AccessibilityManager.Instance;
            float percent = Mathf.Round((a != null ? a.UIScale : 1f) * 100f);
            _uiSizeSlider.SetValueWithoutNotify(percent);
            UpdateUiSizeLabel(percent);
        }

        private void UpdateUiSizeLabel(float percent)
        {
            _uiSizeLabel.text = T("UI SIZE") + "  " + Mathf.Round(percent) + " %";
        }
    }
}
#endif
