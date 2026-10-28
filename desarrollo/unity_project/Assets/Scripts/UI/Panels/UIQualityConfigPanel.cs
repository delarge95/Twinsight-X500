using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.Core.Utils;

namespace WebGL.UI.Panels
{
    /// <summary>
    /// Panel de configuracion de calidad del hero menu:
    /// AUTO actua como expansor; al pulsarlo se despliegan las 4 opciones
    /// restantes en una fila junto a la configuracion avanzada.
    /// </summary>
    public class UIQualityConfigPanel
    {
        private readonly VisualElement _root;
        private readonly VisualElement _tierRow;
        private readonly VisualElement _advancedSection;
        private readonly Button _autoButton;
        private readonly Slider _renderScaleSlider;
        private readonly Slider _shadowDistanceSlider;
        private readonly Button _shadowsToggle;
        private readonly Button _msaaBtn;
        private readonly Button _textureBtn;
        private readonly Button _softboxesBtn;
        private readonly Button _compatBtn;
        private readonly Button[] _tierButtons;
        private readonly QualityTier[] _tierValues =
        {
            QualityTier.Low,
            QualityTier.Medium,
            QualityTier.High,
            QualityTier.Ultra
        };

        private static readonly int[] MsaaCycle = { 0, 2, 4 };
        private static readonly string[] MsaaLabels = { "OFF", "2X", "4X" };
        private static readonly int[] TextureCycle = { 0, 1, 2 };
        private static readonly string[] TextureLabels = { "FULL", "HALF", "QUARTER" };

        private bool _expanded;

        public UIQualityConfigPanel(VisualElement root)
        {
            _root = root;
            _tierRow = root.Q<VisualElement>("ConfigTierRow");
            _advancedSection = root.Q<VisualElement>("ConfigAdvancedSection");
            _autoButton = root.Q<Button>("QualityBtn_Auto");
            _renderScaleSlider = root.Q<Slider>("ConfigRenderScale");
            _shadowDistanceSlider = root.Q<Slider>("ConfigShadowDistance");
            _shadowsToggle = root.Q<Button>("ConfigShadowsToggle");
            _msaaBtn = root.Q<Button>("ConfigMsaaBtn");
            _textureBtn = root.Q<Button>("ConfigTextureBtn");
            _softboxesBtn = root.Q<Button>("ConfigSoftboxesBtn");
            _compatBtn = root.Q<Button>("ConfigCompatBtn");

            _tierButtons = new[]
            {
                root.Q<Button>("QualityBtn_Low"),
                root.Q<Button>("QualityBtn_Medium"),
                root.Q<Button>("QualityBtn_High"),
                root.Q<Button>("QualityBtn_Ultra")
            };

            _expanded = RenderQualityController.SavedTier != QualityTier.Auto;

            RenderQualityController.OnTierChanged += OnTierChanged;
            Bind();
            RefreshTierCards();
            RefreshExpansion();
            RefreshAdvancedControls();
        }

        public void Dispose()
        {
            RenderQualityController.OnTierChanged -= OnTierChanged;
        }

        private void Bind()
        {
            if (_autoButton != null)
            {
                System.Action onAuto = () =>
                {
                    RenderQualityController.SetTier(QualityTier.Auto);
                    _expanded = !_expanded;
                    RefreshExpansion();
                    RefreshTierCards();
                };
                _autoButton.clicked += onAuto;
            }

            for (int i = 0; i < _tierButtons.Length; i++)
            {
                QualityTier tier = _tierValues[i];
                Button button = _tierButtons[i];
                if (button == null)
                {
                    continue;
                }

                System.Action onClick = () =>
                {
                    _expanded = true;
                    RenderQualityController.SetTier(tier);
                    RefreshExpansion();
                    RefreshTierCards();
                };
                button.clicked += onClick;
            }

            if (_renderScaleSlider != null)
            {
                EventCallback<ChangeEvent<float>> onScale = evt => RenderQualityController.SetAdvancedRenderScale(evt.newValue);
                _renderScaleSlider.RegisterValueChangedCallback(onScale);
            }

            if (_shadowDistanceSlider != null)
            {
                EventCallback<ChangeEvent<float>> onDistance = evt => RenderQualityController.SetAdvancedShadowDistance(evt.newValue);
                _shadowDistanceSlider.RegisterValueChangedCallback(onDistance);
            }

            if (_shadowsToggle != null)
            {
                System.Action onShadows = () =>
                {
                    bool current = RenderQualityController.GetAdvancedShadows(ResolvedTierHasShadows);
                    RenderQualityController.SetAdvancedShadows(!current);
                    RefreshAdvancedControls();
                };
                _shadowsToggle.clicked += onShadows;
            }

            if (_msaaBtn != null)
            {
                System.Action onMsaa = () =>
                {
                    int current = RenderQualityController.GetAdvancedMsaa(TierMsaaDefault);
                    int index = System.Array.IndexOf(MsaaCycle, current);
                    int next = MsaaCycle[(index + 1 + MsaaCycle.Length) % MsaaCycle.Length];
                    RenderQualityController.SetAdvancedMsaa(next);
                    RefreshAdvancedControls();
                };
                _msaaBtn.clicked += onMsaa;
            }

            if (_textureBtn != null)
            {
                System.Action onTexture = () =>
                {
                    int current = RenderQualityController.GetAdvancedTextureLimit(TierTextureLimitDefault);
                    int index = System.Array.IndexOf(TextureCycle, current);
                    int next = TextureCycle[(index + 1 + TextureCycle.Length) % TextureCycle.Length];
                    RenderQualityController.SetAdvancedTextureLimit(next);
                    RefreshAdvancedControls();
                };
                _textureBtn.clicked += onTexture;
            }

            if (_softboxesBtn != null)
            {
                System.Action onSoftboxes = () =>
                {
                    bool next = !RenderQualityController.SoftboxesEnabled;
                    RenderQualityController.SetAdvancedSoftboxes(next);
                    RefreshAdvancedControls();
                    if (ServiceLocator.TryGet<EnvironmentController>(out var env))
                    {
                        env.RefreshLightingForQualityChange();
                    }
                };
                _softboxesBtn.clicked += onSoftboxes;
            }

            if (_compatBtn != null)
            {
                System.Action onCompat = () =>
                {
                    RenderQualityController.SetCompatibilityLighting(!RenderQualityController.CompatibilityLightingEnabled);
                    RefreshAdvancedControls();
                };
                _compatBtn.clicked += onCompat;
            }
        }

        private void OnTierChanged(QualityTier tier)
        {
            RefreshTierCards();
            RefreshAdvancedControls();
        }

        private void RefreshExpansion()
        {
            if (_tierRow != null)
            {
                _tierRow.style.display = _expanded ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_advancedSection != null)
            {
                _advancedSection.style.display = _expanded ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void RefreshTierCards()
        {
            QualityTier saved = RenderQualityController.SavedTier;
            if (_autoButton != null)
            {
                _autoButton.EnableInClassList("config-tier-card--active", saved == QualityTier.Auto);
            }

            for (int i = 0; i < _tierButtons.Length; i++)
            {
                if (_tierButtons[i] != null)
                {
                    _tierButtons[i].EnableInClassList("config-tier-card--active", _tierValues[i] == saved);
                }
            }
        }

        private bool ResolvedTierHasShadows => RenderQualityController.GetTierShadowsDefault(RenderQualityController.ResolvedTier);
        private int TierMsaaDefault => RenderQualityController.GetTierMsaaDefault(RenderQualityController.ResolvedTier);
        private int TierTextureLimitDefault => RenderQualityController.GetTierTextureLimitDefault(RenderQualityController.ResolvedTier);
        private float TierRenderScaleDefault => RenderQualityController.GetTierRenderScaleDefault(RenderQualityController.ResolvedTier);
        private float TierShadowDistanceDefault => RenderQualityController.GetTierShadowDistanceDefault(RenderQualityController.ResolvedTier);

        private void RefreshAdvancedControls()
        {
            if (_renderScaleSlider != null)
            {
                _renderScaleSlider.SetValueWithoutNotify(RenderQualityController.GetAdvancedRenderScale(TierRenderScaleDefault));
            }

            if (_shadowDistanceSlider != null)
            {
                _shadowDistanceSlider.SetValueWithoutNotify(RenderQualityController.GetAdvancedShadowDistance(TierShadowDistanceDefault));
            }

            if (_shadowsToggle != null)
            {
                _shadowsToggle.text = RenderQualityController.GetAdvancedShadows(ResolvedTierHasShadows) ? "ON" : "OFF";
            }

            if (_msaaBtn != null)
            {
                int msaa = RenderQualityController.GetAdvancedMsaa(TierMsaaDefault);
                int index = System.Math.Max(0, System.Array.IndexOf(MsaaCycle, msaa));
                _msaaBtn.text = MsaaLabels[index];
            }

            if (_textureBtn != null)
            {
                int limit = RenderQualityController.GetAdvancedTextureLimit(TierTextureLimitDefault);
                int index = System.Math.Max(0, System.Array.IndexOf(TextureCycle, limit));
                _textureBtn.text = TextureLabels[index];
            }

            if (_softboxesBtn != null)
            {
                _softboxesBtn.text = RenderQualityController.SoftboxesEnabled ? "ON" : "OFF";
            }

            if (_compatBtn != null)
            {
                _compatBtn.text = RenderQualityController.CompatibilityLightingEnabled ? "ON" : "OFF";
            }
        }
    }
}
