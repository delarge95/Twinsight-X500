#if UNITY_STANDALONE
using System;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PC only (phase 2A). Created by code after the scene loads (nothing in the scene or in WebGL changes):
    ///  - U2 keyboard shortcuts + help overlay (? / F1) + a small "?" hint pill;
    ///  - U3 floating tooltips with the shortcut key cap;
    ///  - U4 UI scale from the window height (+ "UI size" 80-150 %) and the compact layout;
    ///  - U5 window: resizable with a minimum size, windowed / fullscreen, resolution, vsync.
    ///
    /// | Key             | Action                                       |
    /// | 1 / 2 / 3       | Inspect / Analyze / Studio (toggle)           |
    /// | F               | Focus the selected part (no selection: reset) |
    /// | Home or R       | Reset the view                                |
    /// | H / I / E       | Pins / Isolate / Explode                      |
    /// | Esc             | Back, then deselect (UIManager)               |
    /// | F11, Alt+Enter  | Fullscreen                                    |
    /// | ? or F1         | Help overlay                                  |
    /// </summary>
    public sealed class DesktopUIController : MonoBehaviour
    {
        public static DesktopUIController Instance { get; private set; }

        private UIManager _ui;
        private VisualElement _root;
        private DesktopUIScaler _scaler;
        private DesktopTooltip _tooltip;
        private DesktopHelpOverlay _help;
        private DesktopDisplayPanel _display;
        private DesktopSidePanel _sidePanel;
        private VisualElement _hint;
        private VisualElement _bottomSheet;
        private bool _ready;
        private int _lastScreenW;
        private int _lastScreenH;

        public bool IsHelpOpen => _help != null && _help.IsOpen;
        public float UiScale => _scaler != null ? _scaler.CurrentScale : 1f;
        public Vector2 LogicalSize => _scaler != null ? _scaler.LogicalSize : Vector2.zero;
        public void SetHelpOpen(bool open) { if (_help != null) { if (open) _help.Open(); else _help.Close(); } }
        public void ShowTooltipFor(string elementName)
        {
            VisualElement e = _root?.Q<VisualElement>(elementName);
            if (e != null) _tooltip?.ShowFor(e);
        }
        internal DesktopTooltip Tooltip => _tooltip;
        internal DesktopSidePanel SidePanel => _sidePanel;
        internal DesktopHelpOverlay Help => _help;
        internal DesktopUIScaler Scaler => _scaler;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;

            var go = new GameObject("_DesktopUI");
            DontDestroyOnLoad(go);
            go.AddComponent<DesktopUIController>();
        }

        private void Awake()
        {
            Instance = this;
            DesktopWindowSettings.ReadCommandLine();
        }

        private void Start()
        {
            StartCoroutine(DesktopWindowSettings.Startup());
        }

        private void OnDestroy()
        {
            Shutdown();
            if (Instance == this) Instance = null;
        }

        private void OnApplicationQuit()
        {
            Shutdown();
        }

        private void Shutdown()
        {
            if (!_ready) return;

            _ready = false;
            if (UIManager.OverlayBackHandler != null) UIManager.OverlayBackHandler = null;
            _tooltip?.Dispose();
            _help?.Dispose();
            _display?.Dispose();
            _sidePanel?.Dispose();
            _hint?.RemoveFromHierarchy();
            _scaler?.Restore();
        }

        // ─────────────────────────── init ───────────────────────────

        private void TryInit()
        {
            _ui = UIManager.Instance;
            if (_ui == null || _ui.Root == null || _ui.Root.panel == null)
            {
                return;
            }

            _root = _ui.Root;

            UIDocument document = null;
            foreach (UIDocument d in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                if (d != null && d.rootVisualElement == _root)
                {
                    document = d;
                    break;
                }
            }

            if (document == null)
            {
                return;
            }

            // Style sheet and the class that scopes every desktop-only rule.
            var sheet = Resources.Load<StyleSheet>("UI/DesktopUI");
            if (sheet != null)
            {
                _root.styleSheets.Add(sheet);
            }
            else
            {
                Debug.LogWarning("[DesktopUI] Resources/UI/DesktopUI.uss not found.");
            }

            _root.AddToClassList("desktop-ui");

            // "UI size" lives in the AccessibilityManager (not in the scene): create it on the PC.
            if (AccessibilityManager.Instance == null)
            {
                new GameObject("_Accessibility").AddComponent<AccessibilityManager>();
            }

            _scaler = new DesktopUIScaler(document);
            _scaler.Tick();

            _sidePanel = new DesktopSidePanel(_root);
            _display = new DesktopDisplayPanel(_root);
            _help = new DesktopHelpOverlay(_root);
            BuildHint();
            _tooltip = new DesktopTooltip(_root); // last child: always on top

            UIManager.OverlayBackHandler = () =>
            {
                if (_help != null && _help.IsOpen)
                {
                    Log("Esc -> help closed");
                    _help.Close();
                    return true;
                }

                return false;
            };

            _bottomSheet = _root.Q<VisualElement>("BottomSheet");
            _lastScreenW = Screen.width;
            _lastScreenH = Screen.height;
            _ready = true;

            ParseDemoArgument();
        }

        private void BuildHint()
        {
            var hint = new Button { name = "DesktopHintBtn" };
            hint.AddToClassList("desktop-hint");
            hint.Add(DesktopHelpOverlay.MakeCap("?", true));
            var label = new Label(AppLanguageManager.IsSpanish ? "ATAJOS" : "SHORTCUTS") { pickingMode = PickingMode.Ignore };
            label.AddToClassList("desktop-hint-text");
            hint.Add(label);
            hint.clicked += () => _help?.Toggle();
            hint.RegisterCallback<PointerDownEvent>(evt => { if (evt.button == 0) InputManager.InputBlocked = true; evt.StopPropagation(); });
            hint.RegisterCallback<PointerUpEvent>(evt => { if (evt.button == 0) InputManager.InputBlocked = false; evt.StopPropagation(); });
            AppLanguageManager.LanguageChanged += code => label.text = AppLanguageManager.IsSpanish ? "ATAJOS" : "SHORTCUTS";
            _root.Add(hint);
            _hint = hint;
        }

        // ─────────────────────────── per frame ───────────────────────────

        private void Update()
        {
            if (!_ready)
            {
                TryInit();
                return;
            }

            _scaler.Tick();
            _sidePanel.Tick();
            DesktopWindowSettings.TickMinimumSize();

            if (Screen.width != _lastScreenW || Screen.height != _lastScreenH)
            {
                _lastScreenW = Screen.width;
                _lastScreenH = Screen.height;
                _display?.Refresh();
            }

            UpdateHintVisibility();
            HandleShortcuts();
        }

        private void UpdateHintVisibility()
        {
            if (_hint == null) return;

            bool sheetOpen = _bottomSheet != null && !_bottomSheet.ClassListContains("details-sheet--hidden");
            bool hide = _ui.IsHeroVisible || _ui.IsOnboardingOpen || sheetOpen || IsHelpOpen;
            _hint.EnableInClassList("desktop-hint--hidden", hide);

            // The tutorial is a full card: the viewer UI behind it (capsule, header) only adds noise.
            _root.EnableInClassList("desktop-tutorial-open", _ui.IsOnboardingOpen);
        }

        private bool IsTypingInTextField()
        {
            Focusable focused = _root?.panel?.focusController?.focusedElement;
            for (VisualElement e = focused as VisualElement; e != null; e = e.parent)
            {
                if (e is ITextEdition)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Log(string message)
        {
            Debug.Log("[DesktopUI] shortcut: " + message);
        }

        private void HandleShortcuts()
        {
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);

            // Window: F11 and Alt+Enter work everywhere (Unity's own Alt+Enter is disabled in Player Settings
            // so the state is tracked and saved here).
            if (Input.GetKeyDown(KeyCode.F11)
                || (alt && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))))
            {
                Log("F11/Alt+Enter -> fullscreen toggle"); DesktopWindowSettings.ToggleFullscreen();
                return;
            }

            if (IsTypingInTextField())
            {
                return;
            }

            // Help: "?" (any keyboard layout, read from the typed characters) or F1.
            bool questionTyped = Input.inputString != null && Input.inputString.IndexOf('?') >= 0;
            if (Input.GetKeyDown(KeyCode.F1) || questionTyped)
            {
                Log("? / F1 -> help toggle"); _help.Toggle();
                return;
            }

            if (_help.IsOpen || ctrl || alt)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) { Log("key 1"); _ui.ShortcutToggleMode(1); }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) { Log("key 2"); _ui.ShortcutToggleMode(2); }
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) { Log("key 3"); _ui.ShortcutToggleMode(3); }
            else if (Input.GetKeyDown(KeyCode.F)) { Log("key F"); _ui.ShortcutFocusSelection(); }
            else if (Input.GetKeyDown(KeyCode.Home) || Input.GetKeyDown(KeyCode.R)) { Log("key Home/R"); _ui.ShortcutResetView(); }
            else if (Input.GetKeyDown(KeyCode.H)) { Log("key H"); _ui.ShortcutTogglePins(); }
            else if (Input.GetKeyDown(KeyCode.I)) { Log("key I"); _ui.ShortcutToggleIsolation(); }
            else if (Input.GetKeyDown(KeyCode.E)) { Log("key E"); _ui.ShortcutToggleExplode(); }
        }

        // ─────────────────────────── capture demo hook ───────────────────────────

        private void ParseDemoArgument()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals("-desk-demo2b", StringComparison.OrdinalIgnoreCase))
                {
                    var demo2b = gameObject.AddComponent<DesktopCaptureDemo2B>();
                    demo2b.OutputDirectory = args[i + 1];
                    return;
                }

                if (args[i].Equals("-desk-demo", StringComparison.OrdinalIgnoreCase))
                {
                    var demo = gameObject.AddComponent<DesktopCaptureDemo>();
                    demo.OutputDirectory = args[i + 1];
                    return;
                }
            }
        }
    }
}
#endif
