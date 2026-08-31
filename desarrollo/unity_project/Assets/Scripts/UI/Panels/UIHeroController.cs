using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.Core.Utils;

namespace WebGL.UI.Panels
{
    /// <summary>
    /// Manages the Hero/Landing screen: main menu, submenus (Devices, About, Exit),
    /// and transitions into/out of the 3D viewer.
    /// Extracted from UIManager (Phase 3 Step 2: God Class Dismantling).
    /// </summary>
    public class UIHeroController
    {
        private const string RepositoryUrl = "https://github.com/delarge95/WebGL-Thesis-Proposal";

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void X500V2ExitToLanding();
#endif

        public enum SubmenuType { Devices, About, Config, Exit }

        // ── Elements ──
        private readonly VisualElement _heroContainer;
        private readonly VisualElement _heroMain;
        private readonly VisualElement _submenuDevices;
        private readonly VisualElement _submenuAbout;
        private readonly VisualElement _submenuConfig;
        private readonly VisualElement _submenuExit;
        private readonly VisualElement _root;
        private readonly Button _languageBtn;
        private readonly Label _languageEnLabel;
        private readonly Label _languageEsLabel;
        private readonly Label _heroTitleCyan;
        private readonly Label _heroTitleRed;
        private readonly Label _heroTitleMain;
        private readonly UIQualityConfigPanel _configPanel;
        private IVisualElementScheduledItem _glitchScheduler;

        // ── State ──
        public bool HeroDismissed { get; private set; } = false;
        public bool HasOpenSubmenu =>
            (_submenuDevices != null && _submenuDevices.ClassListContains("hero-submenu--active")) ||
            (_submenuAbout != null && _submenuAbout.ClassListContains("hero-submenu--active")) ||
            (_submenuConfig != null && _submenuConfig.ClassListContains("hero-submenu--active")) ||
            (_submenuExit != null && _submenuExit.ClassListContains("hero-submenu--active"));

        // ── Cleanup ──
        private readonly List<System.Action> _cleanupActions = new List<System.Action>();

        // ── Callbacks that UIManager can hook into ──
        public event System.Action OnHeroDismissed;
        public event System.Action OnHeroReturned;
        public event System.Action OnHelpRequested;

        public UIHeroController(VisualElement root)
        {
            _root = root;
            _heroContainer = root.Q<VisualElement>("HeroContainer");
            _heroMain = root.Q<VisualElement>("HeroMain");
            _submenuDevices = root.Q<VisualElement>("HeroSubmenu_Devices");
            _submenuAbout = root.Q<VisualElement>("HeroSubmenu_About");
            _submenuConfig = root.Q<VisualElement>("HeroSubmenu_Config");
            _submenuExit = root.Q<VisualElement>("HeroSubmenu_Exit");
            _languageBtn = root.Q<Button>("HeroLanguageBtn");
            _languageEnLabel = root.Q<Label>("HeroLangEnLabel");
            _languageEsLabel = root.Q<Label>("HeroLangEsLabel");
            _heroTitleCyan = root.Q<Label>("HeroTitleGlitchCyan");
            _heroTitleRed = root.Q<Label>("HeroTitleGlitchRed");
            _heroTitleMain = root.Q<Label>("HeroTitleMain");
            _configPanel = new UIQualityConfigPanel(root);

            AppLanguageManager.LanguageChanged += OnLanguageChanged;
            AddCleanup(() => AppLanguageManager.LanguageChanged -= OnLanguageChanged);
            BindButtons();
            UpdateLanguageVisuals();
            StartGlitchAnimation();
        }

        private void AddCleanup(System.Action action)
        {
            if (action != null) _cleanupActions.Add(action);
        }

        public void Dispose()
        {
            _glitchScheduler?.Pause();
            OnHeroDismissed = null;
            OnHeroReturned = null;
            OnHelpRequested = null;
            _configPanel?.Dispose();
            foreach (var action in _cleanupActions) action?.Invoke();
            _cleanupActions.Clear();
        }

        // ═══════════════════════════════════════════════════════
        //  Public API
        // ═══════════════════════════════════════════════════════

        /// <summary>Hide the hero screen and notify listeners.</summary>
        public void DismissHero()
        {
            HeroDismissed = true;
            if (_heroContainer != null)
            {
                _heroContainer.AddToClassList("hero--hidden");
                _heroContainer.pickingMode = PickingMode.Ignore;
                _heroContainer.style.display = DisplayStyle.None;
            }
            OnHeroDismissed?.Invoke();
        }

        /// <summary>Return to the hero screen from 3D viewer.</summary>
        public void ReturnToHero()
        {
            HeroDismissed = false;

            if (_heroContainer != null)
            {
                _heroContainer.RemoveFromClassList("hero--hidden");
                _heroContainer.pickingMode = PickingMode.Position;
                _heroContainer.style.display = DisplayStyle.Flex;
                CloseHeroSubmenu();
                ResetGlitchRestingState();
            }
            OnHeroReturned?.Invoke();
        }

        private void StartGlitchAnimation()
        {
            if (_heroContainer == null) return;

            float nextGlitchTime = Time.time + Random.Range(1.8f, 3.8f);
            bool inGlitch = false;
            int glitchSteps = 0;

            _glitchScheduler = _heroContainer.schedule.Execute(() =>
            {
                if (HeroDismissed) return;

                float now = Time.time;
                if (!inGlitch && now >= nextGlitchTime)
                {
                    inGlitch = true;
                    glitchSteps = Random.Range(2, 5);
                }

                if (inGlitch)
                {
                    float shiftX = Random.Range(3f, 7f) * (Random.value > 0.5f ? 1f : -1f);
                    float shiftY = Random.Range(-1.5f, 1.5f);

                    if (_heroTitleCyan != null)
                    {
                        _heroTitleCyan.style.translate = new Translate(-shiftX, shiftY, 0);
                        _heroTitleCyan.style.opacity = Random.Range(0.65f, 0.95f);
                    }

                    if (_heroTitleRed != null)
                    {
                        _heroTitleRed.style.translate = new Translate(shiftX, -shiftY, 0);
                        _heroTitleRed.style.opacity = Random.Range(0.65f, 0.95f);
                    }

                    if (_heroTitleMain != null)
                    {
                        _heroTitleMain.style.translate = new Translate(Random.Range(-1f, 1f), 0, 0);
                    }

                    glitchSteps--;
                    if (glitchSteps <= 0)
                    {
                        inGlitch = false;
                        nextGlitchTime = now + Random.Range(1.8f, 4.2f);
                        ResetGlitchRestingState();
                    }
                }
            }).Every(45);
        }

        private void ResetGlitchRestingState()
        {
            if (_heroTitleCyan != null)
            {
                _heroTitleCyan.style.translate = new Translate(-2f, 0, 0);
                _heroTitleCyan.style.opacity = 0.85f;
            }
            if (_heroTitleRed != null)
            {
                _heroTitleRed.style.translate = new Translate(2f, 0, 0);
                _heroTitleRed.style.opacity = 0.85f;
            }
            if (_heroTitleMain != null)
            {
                _heroTitleMain.style.translate = new Translate(0, 0, 0);
            }
        }

        public void OpenHeroSubmenu(SubmenuType type)
        {
            if (_heroMain != null) _heroMain.style.display = DisplayStyle.None;

            if (_submenuDevices != null) _submenuDevices.RemoveFromClassList("hero-submenu--active");
            if (_submenuAbout != null) _submenuAbout.RemoveFromClassList("hero-submenu--active");
            if (_submenuConfig != null) _submenuConfig.RemoveFromClassList("hero-submenu--active");
            if (_submenuExit != null) _submenuExit.RemoveFromClassList("hero-submenu--active");

            switch (type)
            {
                case SubmenuType.Devices:
                    if (_submenuDevices != null) _submenuDevices.AddToClassList("hero-submenu--active");
                    break;
                case SubmenuType.About:
                    if (_submenuAbout != null) _submenuAbout.AddToClassList("hero-submenu--active");
                    break;
                case SubmenuType.Config:
                    if (_submenuConfig != null) _submenuConfig.AddToClassList("hero-submenu--active");
                    break;
                case SubmenuType.Exit:
                    if (_submenuExit != null) _submenuExit.AddToClassList("hero-submenu--active");
                    break;
            }
        }

        public void CloseHeroSubmenu()
        {
            if (_submenuDevices != null) _submenuDevices.RemoveFromClassList("hero-submenu--active");
            if (_submenuAbout != null) _submenuAbout.RemoveFromClassList("hero-submenu--active");
            if (_submenuConfig != null) _submenuConfig.RemoveFromClassList("hero-submenu--active");
            if (_submenuExit != null) _submenuExit.RemoveFromClassList("hero-submenu--active");
            if (_heroMain != null) _heroMain.style.display = DisplayStyle.Flex;
        }

        public bool HandleBackNavigation()
        {
            if (HeroDismissed)
            {
                return false;
            }

            if (HasOpenSubmenu)
            {
                CloseHeroSubmenu();
                return true;
            }

            return false;
        }

        public bool RequestExitConfirmation()
        {
            if (HeroDismissed)
            {
                return false;
            }

            OpenHeroSubmenu(SubmenuType.Exit);
            return true;
        }

        // ═══════════════════════════════════════════════════════
        //  Button Bindings (with cleanup)
        // ═══════════════════════════════════════════════════════

        private void BindButtons()
        {
            var heroExploreBtn = _root.Q<Button>("HeroExploreBtn");
            var heroDeviceBtn = _root.Q<Button>("HeroDeviceBtn");
            var heroConfigBtn = _root.Q<Button>("HeroConfigBtn");
            var heroInfoBtn = _root.Q<Button>("HeroInfoBtn");
            var heroExitBtn = _root.Q<Button>("HeroExitBtn");
            var heroLanguageBtn = _root.Q<Button>("HeroLanguageBtn");

            // Explore
            if (heroExploreBtn != null)
            {
                System.Action onExplore = () =>
                {
                    DismissHero();
                    if (AppStateMachine.Instance != null) AppStateMachine.Instance.EnterExploration();
                };
                heroExploreBtn.clicked += onExplore;
                AddCleanup(() => heroExploreBtn.clicked -= onExplore);
            }

            // Submenu openers
            if (heroDeviceBtn != null) { heroDeviceBtn.clicked += () => OpenHeroSubmenu(SubmenuType.Devices); AddCleanup(() => heroDeviceBtn.clicked -= () => OpenHeroSubmenu(SubmenuType.Devices)); }
            if (heroInfoBtn != null) { heroInfoBtn.clicked += () => OpenHeroSubmenu(SubmenuType.About); AddCleanup(() => heroInfoBtn.clicked -= () => OpenHeroSubmenu(SubmenuType.About)); }
            if (heroConfigBtn != null) { heroConfigBtn.clicked += () => OpenHeroSubmenu(SubmenuType.Config); AddCleanup(() => heroConfigBtn.clicked -= () => OpenHeroSubmenu(SubmenuType.Config)); }
            if (heroExitBtn != null) { heroExitBtn.clicked += () => OpenHeroSubmenu(SubmenuType.Exit); AddCleanup(() => heroExitBtn.clicked -= () => OpenHeroSubmenu(SubmenuType.Exit)); }
            if (heroLanguageBtn != null)
            {
                System.Action onLanguageToggle = () =>
                {
                    AppLanguageManager.ToggleLanguage();
                };
                heroLanguageBtn.clicked += onLanguageToggle;
                AddCleanup(() => heroLanguageBtn.clicked -= onLanguageToggle);
            }

            // Submenu back buttons
            var backDev = _root.Q<Button>("SubmenuBackBtn_Devices");
            var backAbt = _root.Q<Button>("SubmenuBackBtn_About");
            var backCfg = _root.Q<Button>("SubmenuBackBtn_Config");
            var backExt = _root.Q<Button>("SubmenuBackBtn_Exit");
            if (backDev != null) { backDev.clicked += CloseHeroSubmenu; AddCleanup(() => backDev.clicked -= CloseHeroSubmenu); }
            if (backAbt != null) { backAbt.clicked += CloseHeroSubmenu; AddCleanup(() => backAbt.clicked -= CloseHeroSubmenu); }
            if (backCfg != null) { backCfg.clicked += CloseHeroSubmenu; AddCleanup(() => backCfg.clicked -= CloseHeroSubmenu); }
            if (backExt != null) { backExt.clicked += CloseHeroSubmenu; AddCleanup(() => backExt.clicked -= CloseHeroSubmenu); }

            // About actions
            var helpBtn = _root.Q<Button>("HeroHelpBtn");
            if (helpBtn != null)
            {
                System.Action onHelp = () =>
                {
                    DismissHero();
                    OnHelpRequested?.Invoke();
                };
                helpBtn.clicked += onHelp;
                AddCleanup(() => helpBtn.clicked -= onHelp);
            }

            var perfBtn = _root.Q<Button>("HeroPerfCaptureBtn");
            if (perfBtn != null)
            {
                perfBtn.clicked += TogglePerformanceCapture;
                AddCleanup(() => perfBtn.clicked -= TogglePerformanceCapture);
            }

            var githubBtn = _root.Q<Button>("HeroGithubBtn");
            if (githubBtn != null)
            {
                System.Action onGithub = () => Application.OpenURL(RepositoryUrl);
                githubBtn.clicked += onGithub;
                AddCleanup(() => githubBtn.clicked -= onGithub);
            }

            // Exit actions
            var exitConfirm = _root.Q<Button>("ExitConfirmBtn");
            var exitCancel = _root.Q<Button>("ExitCancelBtn");
            if (exitConfirm != null) { exitConfirm.clicked += RequestExitToLanding; AddCleanup(() => exitConfirm.clicked -= RequestExitToLanding); }
            if (exitCancel != null) { exitCancel.clicked += CloseHeroSubmenu; AddCleanup(() => exitCancel.clicked -= CloseHeroSubmenu); }

            // Home button (return to hero)
            var homeBtn = _root.Q<Button>("HomeBtn");
            if (homeBtn != null) { homeBtn.clicked += ReturnToHero; AddCleanup(() => homeBtn.clicked -= ReturnToHero); }
        }

        private static void TogglePerformanceCapture()
        {
            WebGLProfiler profiler = UnityEngine.Object.FindAnyObjectByType<WebGLProfiler>();
            if (profiler == null)
            {
                var host = new GameObject("_WebGLProfiler");
                profiler = host.AddComponent<WebGLProfiler>();
                UnityEngine.Object.DontDestroyOnLoad(host);
            }

            profiler.ToggleCapturePanel();
        }

        private void RequestExitToLanding()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            X500V2ExitToLanding();
#else
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
#endif
        }

        private void OnLanguageChanged(string languageCode)
        {
            UpdateLanguageVisuals();
        }

        private void UpdateLanguageVisuals()
        {
            AppLanguageManager.ApplyStaticText(_root);
            bool spanish = AppLanguageManager.IsSpanish;
            if (_languageBtn != null)
            {
                _languageBtn.text = string.Empty;
                _languageBtn.EnableInClassList("hero-language-switch--es", spanish);
                _languageBtn.EnableInClassList("hero-language-switch--en", !spanish);
                _languageBtn.tooltip = spanish ? "Switch to English" : "Cambiar a español";
            }

            _languageEnLabel?.EnableInClassList("hero-language-option--active", !spanish);
            _languageEsLabel?.EnableInClassList("hero-language-option--active", spanish);

            var tagline = _root.Q<Label>("HeroTagline");
            if (tagline != null)
            {
                tagline.text = spanish ? "GEMELO DIGITAL INTERACTIVO · HOLYBRO X500 V2" : "INTERACTIVE DIGITAL TWIN · HOLYBRO X500 V2";
            }

            var subtitle = _root.Q<Label>("HeroSubtitle");
            if (subtitle != null)
            {
                subtitle.text = spanish ? "Visor WebGL visual-semántico" : "Visual-semantic WebGL viewer";
            }

            var exploreBtn = _root.Q<Button>("HeroExploreBtn");
            if (exploreBtn != null)
            {
                exploreBtn.text = spanish ? "ABRIR VISOR" : "OPEN VIEWER";
            }
        }
    }
}
