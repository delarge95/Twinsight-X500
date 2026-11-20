#if UNITY_STANDALONE
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.Telemetry;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PC only (PLAN_PC 27.2). The game pad and the UI.
    ///
    /// THE PROBLEM. The scene's EventSystem runs the legacy StandaloneInputModule, which turns the Input Manager axes "Horizontal" / "Vertical" /
    /// "Submit" / "Cancel" into UI Toolkit NavigationMove / NavigationSubmit / NavigationCancel events. A joystick is bound to those axes (stick,
    /// button 0 = A, button 1 = B), so with a pad connected the menus moved while the user flew the drone.
    ///
    /// THE FIX (no change to InputManager.asset, so WebGL is untouched; keyboard arrows, Enter, Esc, Tab and typing are unaffected):
    ///  1. A trickle-down filter on the panel's visual tree stops EVERY navigation event that does not come from the keyboard while an XInput pad
    ///     is connected: no keyboard navigation key (arrows, WASD, Enter, Space, Esc) is down in that frame and the event is not one of ours.
    ///     (The device type of a navigation event is internal in Unity 6, so the keyboard is recognised by its keys, which the legacy module reads in the same
    ///     frame.) A pad therefore never moves the focus, submits or cancels by itself, in either mode.
    ///  2. The pad is read ONCE per frame here (XInput, <see cref="PadSource"/>) into the pure <see cref="PadMachine"/>:
    ///     DRON mode (default): the pilot (DesktopTelemetryUI.Pilot) reads the pad; this class does nothing to the UI.
    ///     MENÚS mode (Start toggles): this class turns the pad into navigation events marked with <see cref="OwnMark"/> (so the filter lets them pass),
    ///     scrolls with the right stick and the pilot ignores the pad (and `hold` is sent when it was piloting).
    ///  3. Visible state: a badge while a pad is connected («Mando: Dron» / «Mando: Menús» + «Start: cambiar»), a toast on every change, the focus ring
    ///     only in MENÚS mode (first focus on a sensible control) and the pad legend of the help overlay.
    /// The window must have the OS focus for the pad to act (an unfocused window reads a neutral pad).
    /// </summary>
    internal sealed class DesktopPad
    {
        public static DesktopPad Instance { get; private set; }

        private readonly VisualElement _root;
        private readonly DesktopUIController _ctrl;
        private readonly List<PadEvent> _events = new List<PadEvent>();
        private VisualElement _badge;
        private DesktopVectorIcon _icon;
        private Label _modeLabel, _hintLabel, _camLabel;
        private string _lastBadge, _lastLang;
        private bool _navHooked;
        private bool _ringAdded;
        private float _nextHint = -10f;

        public readonly PadMachine Machine = new PadMachine();
        public PadMode Mode => Machine.Mode;
        public bool Connected => Machine.Connected;
        public PadState State => Machine.State;
        public bool Pressed(PadButtons b) => Machine.Pressed(b);
        public PadButtons Edges => Machine.Edges;
        public VisualElement Badge => _badge;
        public bool MenusMode => Machine.Connected && Machine.Mode == PadMode.Menus;
        /// <summary>Pad navigation events stopped by the filter / dispatched in MENÚS mode (verification harness).</summary>
        public int BlockedNavEvents { get; private set; }
        public int DispatchedNavEvents { get; private set; }
        public PadEvent? LastEvent { get; private set; }

        /// <summary>The modifier bits that mark an event as dispatched by this class (a keyboard or pad event of the Input Manager never carries both).</summary>
        internal const EventModifiers OwnMark = EventModifiers.Numeric | EventModifiers.FunctionKey;

        private static string Lang => AppLanguageManager.IsSpanish ? "es" : "en";
        private static string T(string key) => TelemetryStrings.T(key, Lang);
        private DesktopTelemetryUI Tel => _ctrl.Chrome != null ? _ctrl.Chrome.Telemetry : null;

        public DesktopPad(VisualElement root, DesktopUIController ctrl)
        {
            _root = root;
            _ctrl = ctrl;
            Instance = this;
            BuildBadge();
            AppLanguageManager.LanguageChanged += _ => _lastBadge = null;
        }

        public void Dispose()
        {
            _badge?.RemoveFromHierarchy();
            if (Instance == this) Instance = null;
        }

        // ───────────────────────── badge ─────────────────────────

        private void BuildBadge()
        {
            _badge = new VisualElement { name = "DkPadBadge", pickingMode = PickingMode.Ignore };
            _badge.AddToClassList("dk-pad-badge");
            _badge.style.display = DisplayStyle.None;
            _icon = new DesktopVectorIcon("gamepad", 16) { pickingMode = PickingMode.Ignore };
            _icon.AddToClassList("dk-pad-icon");
            _badge.Add(_icon);
            _modeLabel = new Label { name = "DkPadMode", pickingMode = PickingMode.Ignore };
            _modeLabel.AddToClassList("dk-pad-mode");
            _badge.Add(_modeLabel);
            _hintLabel = new Label { name = "DkPadHint", pickingMode = PickingMode.Ignore };
            _hintLabel.AddToClassList("dk-pad-hint");
            _badge.Add(_hintLabel);
            // MVP P0: the camera mapping of the pad, shown in DRON mode (LB held + right stick orbits the camera, R3 switches «Seguir» / «Libre»)
            _camLabel = new Label { name = "DkPadCam", pickingMode = PickingMode.Ignore };
            _camLabel.AddToClassList("dk-pad-hint");
            _badge.Add(_camLabel);
            _root.Add(_badge);
        }

        private void UpdateBadge()
        {
            bool show = Machine.Connected;
            bool shown = _badge.style.display != DisplayStyle.None;
            if (shown != show) _badge.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) { _lastBadge = null; return; }

            string key = Machine.Mode == PadMode.Menus ? "pad.badge.menus" : "pad.badge.drone";
            string sig = key + Lang;
            if (sig == _lastBadge) return;
            _lastBadge = sig;
            _modeLabel.text = T(key);
            _hintLabel.text = T("pad.badge.hint");
            _camLabel.text = T("pad.badge.cam");
            _camLabel.style.display = Machine.Mode == PadMode.Drone ? DisplayStyle.Flex : DisplayStyle.None;
            _badge.tooltip = T("pad.badge.tip");
            _badge.EnableInClassList("dk-pad-badge--menus", Machine.Mode == PadMode.Menus);
            _badge.EnableInClassList("dk-pad-badge--drone", Machine.Mode == PadMode.Drone);
            _icon.MarkDirtyRepaint();
        }

        // ───────────────────────── the navigation filter ─────────────────────────

        private void HookNavigation()
        {
            if (_navHooked || _root.panel == null) return;
            _navHooked = true;
            VisualElement tree = _root.panel.visualTree;
            tree.RegisterCallback<NavigationMoveEvent>(e => Filter(e, e.modifiers, 0), TrickleDown.TrickleDown);
            tree.RegisterCallback<NavigationSubmitEvent>(e => Filter(e, e.modifiers, 1), TrickleDown.TrickleDown);
            tree.RegisterCallback<NavigationCancelEvent>(e => Filter(e, e.modifiers, 2), TrickleDown.TrickleDown);
        }

        private static readonly KeyCode[] MoveKeys =
        {
            KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.A, KeyCode.D, KeyCode.W, KeyCode.S,
        };
        private static readonly KeyCode[] SubmitKeys = { KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Space };
        private static readonly KeyCode[] CancelKeys = { KeyCode.Escape };

        private static bool AnyKey(KeyCode[] keys)
        {
            foreach (KeyCode k in keys) if (DesktopKeyboardGate.KeyHeld(k) || DesktopKeyboardGate.KeyPressed(k)) return true;
            return false;
        }

        /// <summary>
        /// Navigation that comes from the pad (not from the keyboard) while an XInput pad is connected: no keyboard navigation key of that kind is down
        /// and the event is not one dispatched by this class (OwnMark). kind: 0 move, 1 submit, 2 cancel.
        /// </summary>
        internal bool IsPadNavigation(EventModifiers modifiers, int kind)
        {
            if (!Machine.Connected) return false;
            if ((modifiers & OwnMark) == OwnMark) return false;
            KeyCode[] keys = kind == 0 ? MoveKeys : kind == 1 ? SubmitKeys : CancelKeys;
            return !AnyKey(keys);
        }

        private void Filter(EventBase evt, EventModifiers modifiers, int kind)
        {
            if (!IsPadNavigation(modifiers, kind)) return;
            BlockedNavEvents++;
            evt.StopImmediatePropagation();
            evt.PreventDefault();
        }

        // ───────────────────────── per frame ─────────────────────────

        public void Tick()
        {
            HookNavigation();

            bool focused = Application.isFocused || DesktopTelemetryUI.IgnoreFocusLoss;
            PadState? raw = PadSource.Poll();
            PadState? state = raw.HasValue && !focused ? PadState.Neutral : raw;

            _events.Clear();
            Machine.Update(state, Time.unscaledTimeAsDouble, _events);

            if (Machine.JustConnected)
            {
                Debug.Log("[Pad] connected: DRON mode (the pad flies the drone, the UI gets nothing; Start = menus)");
                Tel?.ShowToast(T("pad.toast.connected"), false, 5f);
            }
            if (Machine.JustDisconnected)
            {
                Debug.Log("[Pad] disconnected");
                LeaveMenusUi();
                Tel?.ShowToast(T("pad.toast.disconnected"), true);
            }
            if (Machine.ModeJustChanged && Machine.Connected) OnModeChanged();

            if (Machine.Connected && Machine.Mode == PadMode.Menus)
            {
                foreach (PadEvent e in _events) Dispatch(e);
                ScrollWithRightStick();
            }

            UpdateBadge();
        }

        /// <summary>Harness / tests: act as if Start had been pressed.</summary>
        public void ForceMode(PadMode m)
        {
            if (!Machine.Connected) return;
            Machine.SetMode(m);
            OnModeChanged();
        }

        private void OnModeChanged()
        {
            bool menus = Machine.Mode == PadMode.Menus;
            Debug.Log("[Pad] mode -> " + (menus ? "MENUS (the pad navigates the menus; the drone gets no pad command)" : "DRON (the pad flies the drone; the UI gets nothing)"));
            DesktopTelemetryUI tel = Tel;
            bool held = false;
            if (menus)
            {
                if (tel != null) held = tel.OnPadMenus();
                EnterMenusUi();
            }
            else
            {
                LeaveMenusUi();
                tel?.OnPadDrone();
            }

            string msg = T(menus ? "pad.toast.menus" : "pad.toast.drone") + (menus && held ? T("pad.toast.menus.hold") : string.Empty);
            tel?.ShowToast(msg, menus, 4.5f);
            DesktopMotion.Flash(_badge, "dk-flash", 200);
            _lastBadge = null;
        }

        // ───────────────────────── MENÚS: focus ring and first focus ─────────────────────────

        private void EnterMenusUi()
        {
            if (!_root.ClassListContains("dk-keynav")) { _root.AddToClassList("dk-keynav"); _ringAdded = true; }
            _root.AddToClassList("dk-padnav");
            PickFirstFocus();
        }

        private void LeaveMenusUi()
        {
            _root.RemoveFromClassList("dk-padnav");
            if (_ringAdded) { _root.RemoveFromClassList("dk-keynav"); _ringAdded = false; }
            var fe = _root.panel?.focusController?.focusedElement as VisualElement;
            if (fe != null && DesktopKeyboardGate.TextFieldOf(fe) == null) fe.Blur();
        }

        /// <summary>The first control that gets the focus when MENÚS starts: the open popover, the parts tree, the open mode strip, else the mode switch.</summary>
        public Button PickFirstFocus()
        {
            Button target = null;
            DesktopTelemetryUI tel = Tel;
            if (_ctrl.IsHelpOpen) return null;
            if (tel != null && tel.OpenPopover != null)
            {
                VisualElement pop = _root.Q<VisualElement>(tel.OpenPopover == "source" ? "DkPopSource" : tel.OpenPopover == "alerts" ? "DkPopAlerts" : "DkPopFaults");
                target = FirstShownButton(pop);
            }
            if (target == null && tel != null && tel.ModeOpen)
            {
                // «Encendido» is the first focus of the Telemetry strip; if it is ever hidden, the first shown button of the mode container takes it
                target = _root.Q<Button>("DkTelPower");
                if (target == null || target.resolvedStyle.display == DisplayStyle.None) target = FirstShownButton(_root.Q<VisualElement>("DkTelemetry"));
            }
            if (target == null && UIManager.Instance != null && UIManager.Instance.IsHeroVisible) target = _root.Q<Button>("HeroExploreBtn");
            if (target == null) target = _root.Q<Button>(className: "dk-seg-btn--on") ?? _root.Q<Button>("DkMode1");
            if (target == null) target = _root.Q<Button>("DesktopHintBtn");
            target?.Focus();
            return target;
        }

        private static Button FirstShownButton(VisualElement parent)
        {
            if (parent == null) return null;
            foreach (Button b in parent.Query<Button>().ToList())
            {
                if (b.enabledSelf && b.resolvedStyle.display != DisplayStyle.None && b.focusable) return b;
            }
            return null;
        }

        // ───────────────────────── MENÚS: events ─────────────────────────

        private void Dispatch(PadEvent e)
        {
            LastEvent = e;
            var focused = _root.panel?.focusController?.focusedElement as VisualElement;
            switch (e.Action)
            {
                case PadAction.Move:
                    if (focused == null) { PickFirstFocus(); break; }
                    SendMove(focused, e.Dx, e.Dy);
                    break;
                case PadAction.Submit:
                    if (focused == null) { PickFirstFocus(); break; }
                    using (var submit = NavigationSubmitEvent.GetPooled(OwnMark)) { submit.target = focused; focused.SendEvent(submit); }
                    DispatchedNavEvents++;
                    break;
                case PadAction.Cancel:
                    UIManager.Instance?.ShortcutBack();
                    DispatchedNavEvents++;
                    break;
                case PadAction.PrevTab:
                case PadAction.NextTab:
                    int step = e.Action == PadAction.NextTab ? 1 : -1;
                    if (_ctrl.IsHelpOpen) _ctrl.Help?.StepTab(step);
                    else { _ctrl.Chrome?.PadStepMode(step); _root.schedule.Execute(() => PickFirstFocus()).StartingIn(120); }
                    DispatchedNavEvents++;
                    break;
                case PadAction.Help:
                    _ctrl.Help?.TogglePad();
                    DispatchedNavEvents++;
                    break;
            }
        }

        private void SendMove(VisualElement focused, int dx, int dy)
        {
            NavigationMoveEvent.Direction dir = dx > 0 ? NavigationMoveEvent.Direction.Right : dx < 0 ? NavigationMoveEvent.Direction.Left
                : dy > 0 ? NavigationMoveEvent.Direction.Up : NavigationMoveEvent.Direction.Down;
            using (var move = NavigationMoveEvent.GetPooled(dir, OwnMark)) { move.target = focused; focused.SendEvent(move); }
            DispatchedNavEvents++;
        }

        // ───────────────────────── MENÚS: right stick scrolls ─────────────────────────

        private void ScrollWithRightStick()
        {
            double v = Machine.ScrollY;
            if (v == 0) return;
            ScrollView sv = FindScrollTarget();
            if (sv == null) return;
            Vector2 off = sv.scrollOffset;
            off.y = Mathf.Max(0f, off.y - (float)v * 900f * Time.unscaledDeltaTime);
            sv.scrollOffset = off;
        }

        private ScrollView FindScrollTarget()
        {
            var focused = _root.panel?.focusController?.focusedElement as VisualElement;
            for (VisualElement e = focused; e != null; e = e.parent) if (e is ScrollView own && Scrollable(own)) return own;
            // no focus inside a scroll view: the TOPMOST scrollable one (the last in the tree: popovers and overlays are added after the side panels)
            ScrollView top = null;
            foreach (ScrollView sv in _root.Query<ScrollView>().ToList()) if (Scrollable(sv)) top = sv;
            return top;
        }

        private static bool Scrollable(ScrollView sv)
        {
            for (VisualElement p = sv; p != null; p = p.parent) if (p.resolvedStyle.display == DisplayStyle.None) return false;
            return sv.layout.height > 1f && sv.contentContainer.layout.height > sv.layout.height + 1f;
        }
    }
}
#endif
