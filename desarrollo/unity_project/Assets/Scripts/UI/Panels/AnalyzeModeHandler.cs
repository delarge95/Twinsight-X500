using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using WebGL.Core.Content;
using WebGL.Core.Managers;

namespace WebGL.UI.Panels
{
    /// <summary>
    /// Handles the Analyze mode UI: Cross-section, Explode, and Filter cards
    /// with two-level navigation (card grid → sub-panel → back).
    /// </summary>
    public class AnalyzeModeHandler : BaseModeHandler
    {
        private readonly VisualElement _cardGrid;
        private readonly Button _crossSectionBtn;
        private readonly Button _explodeBtn;
        private readonly Button _filterBtn;
        private readonly VisualElement _crossSectionPanel;
        private readonly VisualElement _explodeInlineSlider;
        private readonly VisualElement _filterSubPanel;

        private enum SubLevel { CardGrid, SubPanel }
        private static readonly string[] DefaultCategories =
        {
            "SkeletonAirframe",
            "PropulsionSystem",
            "Avionics",
            "SensorsComms",
            "PowerDistribution",
            "Fasteners"
        };

        private SubLevel _level = SubLevel.CardGrid;
        private string _activePanel; // "cross-section" | "explode" | "filter" | null
        private bool _isExploded;
        private float _lastExplodeValue = 0.5f; // remember last non-zero slider value (default 50%)
        private readonly List<string> _activeCategories = new(DefaultCategories);

        /// <summary>True when a sub-panel is open (for back-navigation detection).</summary>
        public bool IsSubPanelOpen => _level == SubLevel.SubPanel;

        public event Action OnExplodeToggleRequested;

        public AnalyzeModeHandler(VisualElement root, VisualElement container) : base(root, container)
        {
            if (container == null) return;

            _cardGrid = container.Q<VisualElement>("AnalyzeCardGrid");
            _crossSectionBtn = container.Q<Button>("AnalyzeCrossSectionBtn");
            _explodeBtn = container.Q<Button>("AnalyzeExplodeBtn");
            _filterBtn = container.Q<Button>("AnalyzeFilterBtn");
            _crossSectionPanel = container.Q<VisualElement>("CrossSectionPanel");
            _explodeInlineSlider = container.Q<VisualElement>("ExplodeInlineSlider");
            _filterSubPanel = container.Q<VisualElement>("FilterSubPanel");

            BindCards();
        }

        private void BindCards()
        {
            if (_crossSectionBtn != null)
            {
                Action onCross = () => DelayAction(PressCutCard);
                _crossSectionBtn.clicked += onCross;
                AddCleanup(() => _crossSectionBtn.clicked -= onCross);
            }

            if (_explodeBtn != null)
            {
                Action onExplode = () => DelayAction(ToggleExplodeInline);
                _explodeBtn.clicked += onExplode;
                AddCleanup(() => _explodeBtn.clicked -= onExplode);
            }

            if (_filterBtn != null)
            {
                Action onFilter = () => DelayAction(PressFilterCard);
                _filterBtn.clicked += onFilter;
                AddCleanup(() => _filterBtn.clicked -= onFilter);
            }

            // AUX-U10: explicit "back" button in the CUT and FILTER sub-panels.
            foreach (string backName in new[] { "CrossSectionBackBtn", "FilterBackBtn" })
            {
                Button back = Container?.Q<Button>(backName);
                if (back == null) continue;

                Action onBack = () => NavigateToCardGrid();
                back.clicked += onBack;
                AddCleanup(() => back.clicked -= onBack);
            }
        }

        public override void Activate()
        {
            _level = SubLevel.CardGrid;
            _activePanel = null;
            ShowLevel();
            ApplyCategoryFilters();
            UpdateCategoryButtonStates();
            WatchCutState();
            RefreshActiveStates();

            // Restore inline slider if explode is active
            if (_isExploded)
            {
                _explodeInlineSlider?.RemoveFromClassList("submenu--hidden");
                _explodeBtn?.EnableInClassList("submenu-card--active", true);
            }
        }

        public override void Deactivate()
        {
            CloseAllSubPanels();
            _explodeInlineSlider?.AddToClassList("submenu--hidden");
        }

        /// <summary>Navigate from card grid to a sub-panel.</summary>
        public void NavigateToSubPanel(string panelId)
        {
            _level = SubLevel.SubPanel;
            _activePanel = panelId;
            ShowLevel();

            // Hide inline explode slider when navigating to a sub-panel
            _explodeInlineSlider?.AddToClassList("submenu--hidden");

            _crossSectionBtn?.EnableInClassList("submenu-card--active", panelId == "cross-section" || CutOnPc());
            _explodeBtn?.EnableInClassList("submenu-card--active", _isExploded);
            _filterBtn?.EnableInClassList("submenu-card--active", panelId == "filter" || FilterOn());

            if (panelId == "cross-section")
                CrossSectionManager.Instance?.EnableCrossSection();
        }

        /// <summary>Navigate from sub-panel back to card grid.</summary>
        public void NavigateToCardGrid()
        {
            // Cross-section is NOT disabled on back-navigation.
            // It persists until all axes are deselected or Reset View.

            _level = SubLevel.CardGrid;
            _activePanel = null;
            ShowLevel();

            RefreshActiveStates();

            // Restore inline slider if explode is active
            if (_isExploded)
                _explodeInlineSlider?.RemoveFromClassList("submenu--hidden");
        }

        private void ShowLevel()
        {
            bool showGrid = _level == SubLevel.CardGrid;
            _cardGrid?.EnableInClassList("submenu--hidden", !showGrid);

            _crossSectionPanel?.EnableInClassList("submenu--hidden", !(
                !showGrid && _activePanel == "cross-section"));
            _filterSubPanel?.EnableInClassList("submenu--hidden", !(
                !showGrid && _activePanel == "filter"));
        }

        private void CloseAllSubPanels()
        {
            // NOTE: Cross-section is intentionally NOT disabled here.
            // It persists across mode switches / part selections.
            // Only disabled via: all axes deselected, or Reset View.

            _level = SubLevel.CardGrid;
            _activePanel = null;
            _cardGrid?.RemoveFromClassList("submenu--hidden");
            _crossSectionPanel?.AddToClassList("submenu--hidden");
            _filterSubPanel?.AddToClassList("submenu--hidden");

            // Keep cross-section card highlighted if cross-section is active
            RefreshActiveStates();
        }


        // ── Card toggles (PC: pressing an active card again turns its effect off; PLAN_PC 41) ──

        /// <summary>PC only: the cut card stays lit while another panel is open (WebGL lights it only with its own panel).</summary>
        private static bool CutOnPc()
        {
#if UNITY_STANDALONE
            return CutOn();
#else
            return false;
#endif
        }

        private static bool CutOn() => CrossSectionManager.Instance != null && CrossSectionManager.Instance.IsEnabled;

        /// <summary>The category filter is narrowed (PC only: WebGL keeps its card behaviour, the Filter card is lit only while its panel is open).</summary>
        private bool FilterOn()
        {
#if UNITY_STANDALONE
            return IsCategoryFilterActive;
#else
            return false;
#endif
        }

        /// <summary>Lights the three cards from the real state: cut on, exploded, filter narrowed (or its panel open).</summary>
        private void RefreshActiveStates()
        {
            bool cutPanel = _level == SubLevel.SubPanel && _activePanel == "cross-section";
            bool filterPanel = _level == SubLevel.SubPanel && _activePanel == "filter";
            _crossSectionBtn?.EnableInClassList("submenu-card--active", cutPanel || CutOn());
            _explodeBtn?.EnableInClassList("submenu-card--active", _isExploded);
            _filterBtn?.EnableInClassList("submenu-card--active", filterPanel || FilterOn());
        }

        public void PressCutCard()
        {
#if UNITY_STANDALONE
            bool panelOpen = _level == SubLevel.SubPanel && _activePanel == "cross-section";
            switch (AnalyzeCardToggle.ForCard(CutOn(), panelOpen && CutOn())) // the cut off: pressing always switches it on (a stale open panel does not count)
            {
                case AnalyzeCardToggle.Step.TurnOff:
                    CrossSectionManager.Instance?.DisableCrossSection();
                    NavigateToCardGrid();
                    return;
                case AnalyzeCardToggle.Step.ClosePanel:
                    NavigateToCardGrid();
                    return;
            }
#endif
            NavigateToSubPanel("cross-section");
        }

        public void PressFilterCard()
        {
#if UNITY_STANDALONE
            bool panelOpen = _level == SubLevel.SubPanel && _activePanel == "filter";
            switch (AnalyzeCardToggle.ForCard(FilterOn(), panelOpen))
            {
                case AnalyzeCardToggle.Step.TurnOff:
                    ClearCategoryFilters();
                    NavigateToCardGrid();
                    return;
                case AnalyzeCardToggle.Step.ClosePanel:
                    NavigateToCardGrid();
                    return;
            }
#endif
            NavigateToSubPanel("filter");
        }

        private bool _watchingCut;

        /// <summary>The top-bar chip, Reset or the axis buttons can switch the cut off: the card follows the real state.</summary>
        private void WatchCutState()
        {
#if UNITY_STANDALONE
            CrossSectionManager mgr = CrossSectionManager.Instance;
            if (mgr == null || _watchingCut) return;
            _watchingCut = true;
            Action<bool> onToggled = _ => RefreshActiveStates();
            mgr.OnCrossSectionToggled += onToggled;
            AddCleanup(() => { if (mgr != null) mgr.OnCrossSectionToggled -= onToggled; });
#endif
        }

        // ── Explode State Sync ──

        public void SetExplodeState(bool exploded)
        {
            _isExploded = exploded;
            _explodeBtn?.EnableInClassList("submenu-card--active", exploded);
            _explodeInlineSlider?.EnableInClassList("submenu--hidden", !exploded);
        }

        public bool IsExploded => _isExploded;

#if UNITY_STANDALONE
        /// <summary>PC top-bar chips: turn the category filter off without opening the Analyze menu.</summary>
        /// <summary>True when the user narrowed the categories (the full default set is "no filter"; the manager reports it as active).</summary>
        public bool IsCategoryFilterActive => _activeCategories.Count != DefaultCategories.Length;

        public void ClearCategoryFilters()
        {
            ResetToDefaultCategories();
            ApplyCategoryFilters();
            UpdateCategoryButtonStates();
        }

        /// <summary>PC top-bar chips: reassemble the exploded view without opening the Analyze menu.</summary>
        public void ClearExplode()
        {
            if (_isExploded) ToggleExplodePanel();
        }
#endif

        // ── Category Filters ──

        public void SetCategoryFilter(string category, Button clickedBtn, bool exclusiveMode = false)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return;
            }

            if (exclusiveMode)
            {
                if (IsOnlyCategoryActive(category))
                {
                    ResetToDefaultCategories();
                }
                else
                {
                    _activeCategories.Clear();
                    _activeCategories.Add(category);
                }
            }
            else
            {
                if (_activeCategories.Contains(category))
                {
                    _activeCategories.Remove(category);
                }
                else
                {
                    _activeCategories.Add(category);
                }

                if (_activeCategories.Count == 0)
                {
                    // Keep a visible default when user toggles the last active chip off.
                    ResetToDefaultCategories();
                }
            }

            ApplyCategoryFilters();
            UpdateCategoryButtonStates();
        }

        private bool IsOnlyCategoryActive(string category)
        {
            return _activeCategories.Count == 1 &&
                   string.Equals(_activeCategories[0], category, StringComparison.OrdinalIgnoreCase);
        }

        private void ResetToDefaultCategories()
        {
            _activeCategories.Clear();
            _activeCategories.AddRange(DefaultCategories);
        }

        private void ApplyCategoryFilters()
        {
            ExplodedViewManager.Instance?.SetCategoryFilters(_activeCategories);
        }

        private void UpdateCategoryButtonStates()
        {
            if (_filterSubPanel == null) return;

            void UpdateBtn(string btnName, string catName)
            {
                var btn = _filterSubPanel.Q<Button>(btnName);
                btn?.EnableInClassList("submenu-card--active", _activeCategories.Contains(catName));
            }

            UpdateBtn("CatBtn_Structure", "SkeletonAirframe");
            UpdateBtn("CatBtn_Propulsion", "PropulsionSystem");
            UpdateBtn("CatBtn_Avionics", "Avionics");
            UpdateBtn("CatBtn_Sensors", "SensorsComms");
            UpdateBtn("CatBtn_Power", "PowerDistribution");
            UpdateBtn("CatBtn_Payload", "Fasteners");
#if UNITY_STANDALONE
            RefreshActiveStates();
#endif
        }

        // ── Legacy API ──

        public void ToggleCrossSectionPanel()
        {
            if (_level == SubLevel.SubPanel && _activePanel == "cross-section")
                NavigateToCardGrid();
            else
                NavigateToSubPanel("cross-section");
        }

        public void ToggleCategoryMenu()
        {
            if (_level == SubLevel.SubPanel && _activePanel == "filter")
                NavigateToCardGrid();
            else
                NavigateToSubPanel("filter");
        }

        public void ToggleExplodePanel() => ToggleExplodeInline();

        /// <summary>Toggle the inline explode slider below the card grid.</summary>
        private void ToggleExplodeInline()
        {
            // If a sub-panel is open, go back to card grid first
            if (_level == SubLevel.SubPanel)
                NavigateToCardGrid();

            bool sliderVisible = _explodeInlineSlider != null
                && !_explodeInlineSlider.ClassListContains("submenu--hidden");

            if (sliderVisible)
            {
                // Save current value before hiding (only if non-zero)
                var slider = _explodeInlineSlider?.Q<UnityEngine.UIElements.Slider>("ExplosionSlider");
                if (slider != null && slider.value > 0.001f)
                    _lastExplodeValue = slider.value;

                // Hide slider and reset explosion
                _explodeInlineSlider?.AddToClassList("submenu--hidden");
                _explodeBtn?.RemoveFromClassList("submenu-card--active");
                _isExploded = false;

                // Reset slider value to 0 so ExplodedViewManager animates back
                if (slider != null) slider.value = 0f;

                OnExplodeToggleRequested?.Invoke();
            }
            else
            {
                // Show slider — restore last value (or default 50%)
                _explodeInlineSlider?.RemoveFromClassList("submenu--hidden");
                _explodeBtn?.EnableInClassList("submenu-card--active", true);
                _isExploded = true;

                var slider = _explodeInlineSlider?.Q<UnityEngine.UIElements.Slider>("ExplosionSlider");
                if (slider != null)
                    slider.value = _lastExplodeValue;

                OnExplodeToggleRequested?.Invoke();
            }
        }

        public override void Dispose()
        {
            OnExplodeToggleRequested = null;
            base.Dispose();
        }
    }
}
