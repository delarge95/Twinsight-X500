#if UNITY_STANDALONE
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Content;
using WebGL.Core.Managers;
using WebGL.Telemetry;
using WebGL.UI.Panels;
using WebGL.UI.ProceduralIcons;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PC only (phase 4). Desktop chrome of the approved mockup (docs/unity_pc/maqueta_desktop):
    ///  - slim top bar (44 px): home, brand, selection crumb, segmented mode switch (1/2/3), pins / isolate / reset,
    ///    left and right rail toggles, ES/EN, settings (CONFIG inside the viewer) and shortcuts;
    ///  - 40 px rails (parts / inspector) that fold to icons; the docked panels replace them;
    ///  - contextual 36 px tool strip under the bar (the old mode cards restyled in DesktopUI.uss) and the old
    ///    sub-panels as small popovers (CUT, FILTER, EXPLODE, POWER, RENDER, ENVIRONMENT);
    ///  - 26 px status bar with mouse hints, the horizontal thermal legend, quality and a shortcuts button.
    /// All the logic stays in the existing panels: this class forwards clicks to their buttons (NavigationSubmit)
    /// and mirrors their state. Nothing here exists in WebGL.
    /// </summary>
    internal sealed class DesktopChrome
    {
        public const float TopHeight = 44f;
        public const float StatusHeight = 26f;
        public const float RailWidth = 40f;
        public const float AutoOpenMinLogicalWidth = 1600f;
        public const float NarrowBarLogicalWidth = 1600f;
        /// <summary>Logical widths below which the top bar gives up one more thing (levels 1..6, see UpdateBarLevel).</summary>
        private static readonly float[] BarLevelBelow = { 1600f, 1500f, 1380f, 1320f, 1260f, 1200f };

        private readonly VisualElement _root;
        private readonly DesktopUIController _ctrl;
        private readonly DesktopPartTree _tree;
        private readonly DesktopSidePanel _side;
        private readonly UIManager _ui;

        private VisualElement _top;
        private VisualElement _railL;
        private VisualElement _railR;
        private VisualElement _status;
        private VisualElement _settings;
        private VisualElement _settingsBody;
        private VisualElement _cfgOriginalParent;
        private int _cfgOriginalIndex;

        private readonly Button[] _modeSeg = new Button[4];
        private readonly Label[] _modeText = new Label[4];
        private DesktopTelemetryUI _tel;
        private readonly Button[] _modeSrc = new Button[3];
        private Button _chipFilter, _chipExplode, _chipCut, _chipPower;
        private DesktopActiveEffects _fx; // MVP P1: the registry of active-effect chips of the top bar

        /// <summary>The registry of active effects (one chip per toggleable tool that is on).</summary>
        internal DesktopActiveEffects Effects => _fx;
        private SlidingPill _segPill, _langPill;
        private Label _sheetTitle;
        private string _lastCrumb, _lastTitle;
        private bool _heroWas;
        private Label _crumb;
        private Button _btnPins, _btnIso, _btnTree, _btnInspector, _btnGear, _btnHelp, _btnLangEn, _btnLangEs;
        private Button _railTree, _railSearch, _railInspector;
        private Label _hint, _quality, _helpLabel;
        private VisualElement _tlg, _tlgBar;
        private Label _tlgMin, _tlgMax;
        private Button _studioRender, _studioEnv;
        private VisualElement _studioStrip, _studioPanel, _studioContainer;
        private Label _studioRenderLabel;
        private DesktopVectorIcon _renderIcon;
        private VisualElement _toolsGrid, _analyzeGrid;
        private readonly List<DesktopVectorIcon> _icons = new List<DesktopVectorIcon>();

        private Transform _lastSelection;
        private bool _studioWasVisible;
        private string _studioSection = "render";
        private bool _settingsOpen;
        private bool _sheetWasOpen;

        private static readonly ViewMode[] CycleOrder =
        {
            ViewMode.Realistic, ViewMode.XRay, ViewMode.SolidColor, ViewMode.Thermal, ViewMode.Wireframe, ViewMode.Ghosted
        };

        public bool IsSettingsOpen => _settingsOpen;
        internal DesktopTelemetryUI Telemetry => _tel;

        public DesktopChrome(VisualElement root, DesktopUIController ctrl, DesktopPartTree tree, DesktopSidePanel side)
        {
            _root = root;
            _ctrl = ctrl;
            _tree = tree;
            _side = side;
            _ui = UIManager.Instance;

            _root.AddToClassList("chrome2");
            BuildTop();
            BuildRails();
            BuildStatus();
            BuildSettings();
            PrepareModePanels();
            BuildSheetFooter();
            _tel.BuildMode();
            AppLanguageManager.LanguageChanged += OnLanguage;
            OnLanguage(null);
        }

        public void Dispose()
        {
            AppLanguageManager.LanguageChanged -= OnLanguage;
            CloseSettings();
            _top?.RemoveFromHierarchy();
            _tel?.Dispose();
            _railL?.RemoveFromHierarchy();
            _railR?.RemoveFromHierarchy();
            _status?.RemoveFromHierarchy();
            _settings?.RemoveFromHierarchy();
            _root.RemoveFromClassList("chrome2");
        }

        private static string T(string en, string es) => AppLanguageManager.IsSpanish ? es : en;

        // ───────────────────────── helpers ─────────────────────────

        private static void Fire(Button b)
        {
            if (b == null) return;
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
        }

        private Button IconButton(string shape, string name, Action onClick, float icon = 16f)
        {
            var b = new Button(onClick) { name = name };
            b.AddToClassList("dk-ibtn");
            var ic = new DesktopVectorIcon(shape, icon);
            _icons.Add(ic);
            b.Add(ic);
            b.RegisterCallback<PointerEnterEvent>(_ => ic.MarkDirtyRepaint());
            b.RegisterCallback<PointerLeaveEvent>(_ => ic.MarkDirtyRepaint());
            return b;
        }

        /// <summary>Active-effect chip: appears next to the pins button while a filter / exploded view / cut is on; click turns it off.</summary>
        private Button MakeChip(string shape, string name, Action off)
        {
            Button chip = IconButton(shape, name, off);
            chip.AddToClassList("dk-chip");
            chip.style.display = DisplayStyle.None;
            return chip;
        }

        private static void UpdateChip(Button chip, bool active)
        {
            if (chip == null) return;
            bool live = chip.ClassListContains("dk-chip--live");
            if (active && !live)
            {
                chip.AddToClassList("dk-chip--live");
                chip.RemoveFromClassList("dk-chip--out");
                chip.style.display = DisplayStyle.Flex;
                DesktopMotion.Enter(chip, 0, "dk-enter--chip");
            }
            else if (!active && live)
            {
                chip.RemoveFromClassList("dk-chip--live");
                chip.AddToClassList("dk-chip--out");
                chip.schedule.Execute(() => { if (!chip.ClassListContains("dk-chip--live")) chip.style.display = DisplayStyle.None; }).StartingIn(DesktopMotion.Reduce ? 1 : 200);
            }
        }

        /// <summary>
        /// MVP P1: every active effect of the top bar, in the order it shows. Existing chips keep their element names (DkChipFilter...); the tool chips are new. The tooltip of a
        /// chip says what a click does: «Quitar …» (the tool goes back to how it was). Mission, training and piloting are not removable things but things that stop: «Detener …».
        /// </summary>
        private void RegisterActiveEffects()
        {
            _fx = new DesktopActiveEffects(T);
            _fx.Add("filter", _chipFilter, () => _ui.FilterActive, "Quitar filtro", "Remove filter", false);
            _fx.Add("explode", _chipExplode, () => ExplodedViewManager.Instance != null && ExplodedViewManager.Instance.GetExplosionFactor() > 0.001f, "Quitar vista explosiva", "Remove exploded view", false);
            _fx.Add("cut", _chipCut, () => CrossSectionManager.Instance != null && CrossSectionManager.Instance.IsEnabled, "Quitar corte", "Remove cut", false);
            _fx.Add("power", _chipPower, () => DroneStateController.Instance != null && DroneStateController.Instance.IsOn, "Apagar energía simulada", "Switch off simulated power", false);

            // render mode other than the realistic one (the V key, the Studio mode)
            AddToolChip("view", "xray", "DkChipView",
                () => ViewModeManager.Instance != null && ViewModeManager.Instance.CurrentMode != ViewMode.Realistic,
                () => ViewModeManager.Instance?.SetViewMode(ViewMode.Realistic), "Volver a la vista realista", "Back to the realistic view", false);
            // the tools of the telemetry mode (state read from DesktopTelemetryUI)
            AddToolChip("pilot", "gamepad", "DkChipPilot", () => _tel.Piloting, () => _tel.SetPilot(false, "chip"), "Dejar de pilotar", "Stop piloting");
            AddToolChip("wind", "wind", "DkChipWind", () => _tel.IsRunning && DesktopTelemetry.IsSimulator && (_tel.WindRemembered.speed > 0 || _tel.WindRemembered.gustSigma > 0),
                () => { var w = _tel.WindRemembered.Clone(); w.speed = 0; w.gustSigma = 0; _tel.ApplyWind(w); }, "Quitar viento", "Remove wind");
            AddToolChip("fence", "fence", "DkChipFence", () => _tel.IsRunning && DesktopTelemetry.IsSimulator && !FenceIsDefault(_tel.FenceRemembered),
                () => _tel.ApplyFence(Geofence.Default()), "Quitar geocerca personalizada", "Remove custom geofence");
            AddToolChip("batteryignore", "battery", "DkChipBatteryIgnore", () => _tel.IsRunning && DesktopTelemetry.IsSimulator && !_tel.BatteryReactOn,
                () => _tel.ApplyBatteryReact(true), "Quitar «Ignorar batería»", "Remove “Ignore battery”");
            AddToolChip("course", "studio", "DkChipCourse", () => _tel.CourseOn, () => _tel.SetCourse(false), "Quitar circuito", "Remove circuit");
            AddToolChip("map", "focus", "DkChipMap", () => _tel.MapOn, () => _tel.SetMap(false), "Quitar mapa", "Remove map");
            AddToolChip("mission", "pins", "DkChipMission", () => _tel.MissionBusy, () => _tel.MissionDo(MissionControl.Abort), "Detener misión", "Stop mission");
            AddToolChip("training", "check", "DkChipTraining", () => _tel.TrainingActive, () => _tel.QuitTraining(), "Salir del entrenamiento", "Leave training");
            AddToolChip("ground", "env_studio", "DkChipGround", () => _tel.IsRunning && !_tel.GroundOn, () => _tel.SetGround(true), "Mostrar suelo", "Show ground");
        }

        private void AddToolChip(string id, string icon, string name, Func<bool> isActive, Action off, string tipEs, string tipEn, bool tool = true)
        {
            Button chip = MakeChip(icon, name, off);
            _top.Add(chip);
            _fx.Add(id, chip, isActive, tipEs, tipEn, tool);
        }

        private static bool FenceIsDefault(FenceConfig f)
        {
            FenceConfig d = Geofence.Default();
            return f == null || (f.radius == d.radius && f.ceiling == d.ceiling && f.rtlAltitude == d.rtlAltitude);
        }

        internal Button MakeChipPublic(string shape, string name, Action off) => MakeChip(shape, name, off);
        internal DesktopVectorIcon NewIconPublic(string shape, float size) => NewIcon(shape, size);
        internal Button IconButtonPublic(string shape, string name, Action onClick, float icon = 16f) => IconButton(shape, name, onClick, icon);

        private DesktopVectorIcon NewIcon(string shape, float size)
        {
            var ic = new DesktopVectorIcon(shape, size);
            _icons.Add(ic);
            return ic;
        }

        /// <summary>Every icon of the PC UI comes from the one set: the procedural / text glyphs of the shared UXML are replaced here.</summary>
        private void DecorateDesktopControls()
        {
            // The shared UXML already carries the OutlineIcon of every card, close and back button: PC only retints / resizes them by USS.
            foreach (string n in new[] { "CrossSectionBackBtn", "FilterBackBtn" })
            {
                // popovers close (not go back): same X as the other panels
                _root.Q<Button>(n)?.Q<OutlineIcon>()?.SetShape("close");
            }

            // hero (PC): button system; icon + text centred as one group
            foreach (string n in new[] { "HeroInfoBtn", "SubmenuBackBtn_Devices", "SubmenuBackBtn_About", "SubmenuBackBtn_Config", "SubmenuBackBtn_Exit" })
            {
                Button b = _root.Q<Button>(n);
                if (b == null) continue;
                b.AddToClassList("dk-b");
                b.AddToClassList("dk-b--icon");
                if (n != "HeroInfoBtn") b.AddToClassList("dk-b--hero-back");
            }

            StyleHeroButton("HeroExploreBtn", "dk-b--primary", "inspect");
            StyleHeroButton("HeroDeviceBtn", "dk-b--secondary", "studio");
            StyleHeroButton("HeroConfigBtn", "dk-b--secondary", "settings");
            StyleHeroButton("HeroExitBtn", "dk-b--secondary", "power");
            StyleHeroButton("HeroHelpBtn", "dk-b--secondary", "help");
            StyleHeroButton("HeroGithubBtn", "dk-b--ghost", null);
            StyleHeroButton("HeroPerfCaptureBtn", "dk-b--ghost", null);
            StyleHeroButton("ExitConfirmBtn", "dk-b--primary", null);
            StyleHeroButton("ExitCancelBtn", "dk-b--secondary", null);
        }

        private readonly List<KeyValuePair<Button, VisualElement>> _heroLeads = new List<KeyValuePair<Button, VisualElement>>();

        private void StyleHeroButton(string name, string variant, string icon)
        {
            Button b = _root.Q<Button>(name);
            if (b == null) return;
            b.AddToClassList("dk-b");
            b.AddToClassList("dk-b--lg");
            b.AddToClassList(variant);
            if (icon != null)
            {
                var ic = NewIcon(icon, 20);
                ic.AddToClassList("dk-b-lead");
                b.AddToClassList("dk-b--lead");
                b.style.paddingLeft = 50; b.style.paddingRight = 22;
                b.Add(ic);
                _heroLeads.Add(new KeyValuePair<Button, VisualElement>(b, ic));
            }
        }

        /// <summary>Icon + text as one centred group: the text keeps its centre shifted by half (icon + gap) and the icon sits just left of it.</summary>
        private void CentreHeroLeads()
        {
            foreach (var kv in _heroLeads)
            {
                Button b = kv.Key;
                if (b.resolvedStyle.display == DisplayStyle.None || b.layout.width < 10f) continue;
                float tw = b.MeasureTextSize(b.text ?? string.Empty, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined).x;
                float total = 20f + 8f + tw;
                kv.Value.style.left = Mathf.Round((b.layout.width - total) * 0.5f);
            }
        }

        private static Label Kbd(string key)
        {
            var l = new Label(key) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("dk-kbd");
            return l;
        }

        private static VisualElement Sep()
        {
            var s = new VisualElement { pickingMode = PickingMode.Ignore };
            s.AddToClassList("dk-sep");
            return s;
        }

        private static void Grow(VisualElement parent)
        {
            var g = new VisualElement { pickingMode = PickingMode.Ignore };
            g.AddToClassList("dk-grow");
            parent.Add(g);
        }

        private void ToggleInspector()
        {
            if (_side.IsOpen)
            {
                UIDetailsSheet.Instance?.SetSheetState(false);
            }
            else if (SelectionManager.Instance != null && SelectionManager.Instance.CurrentSelection != null)
            {
                UIDetailsSheet.Instance?.ShowInfo();
            }
        }

        public void ToggleInspectorPublic() => ToggleInspector();
        public void ToggleTreePublic() => _tree.SetOpen(!_tree.IsOpen);

        // ───────────────────────── top bar ─────────────────────────

        private void BuildTop()
        {
            _top = new VisualElement { name = "DkTopBar" };
            _top.AddToClassList("dk-top");

            var home = IconButton("home", "DkHome", () => Fire(_root.Q<Button>("HomeBtn")));
            home.tooltip = T("Return to menu", "Volver al menú");
            _top.Add(home);

            var brand = new Label("TwinSight") { pickingMode = PickingMode.Ignore };
            brand.AddToClassList("dk-brand");
            brand.AddToClassList("dk-brand-word");
            var x500 = new Label("X500") { pickingMode = PickingMode.Ignore };
            x500.AddToClassList("dk-brand");
            x500.AddToClassList("dk-brand--accent");
            _top.Add(brand);
            _top.Add(x500);

            _crumb = new Label { pickingMode = PickingMode.Ignore };
            _crumb.AddToClassList("dk-crumb");
            _top.Add(_crumb);
            Grow(_top);

            _tel = new DesktopTelemetryUI(_root, this, _ui);
            var seg = new VisualElement();
            seg.AddToClassList("dk-seg");
            string[] keys = { "1", "2", "3", "4" };
            for (int i = 0; i < 4; i++)
            {
                int mode = i + 1;
                // mode 4 (Telemetry, WP-11) is not one of the three UIModeController modes: it lives in DesktopTelemetryUI
                var b = new Button(() => { if (mode == 4) _tel.ToggleMode(); else _ui.ShortcutToggleMode(mode); }) { name = "DkMode" + mode };
                b.AddToClassList("dk-seg-btn");
                var segIcon = new DesktopVectorIcon(mode == 1 ? "inspect" : mode == 2 ? "analyze" : mode == 3 ? "studio" : "flight", 16);
                _icons.Add(segIcon);
                b.Add(segIcon);
                var t = new Label { pickingMode = PickingMode.Ignore };
                t.AddToClassList("dk-seg-text");
                b.Add(t);
                b.Add(Kbd(keys[i]));
                seg.Add(b);
                _modeSeg[i] = b;
                _modeText[i] = t;
            }

            _segPill = new SlidingPill(seg, "dk-seg-pill");
            _modeSrc[0] = _root.Q<Button>("ModeToolsBtn");
            _modeSrc[1] = _root.Q<Button>("ModeAnalyzeBtn");
            _modeSrc[2] = _root.Q<Button>("ModeStudioBtn");
            _top.Add(seg);
            Grow(_top);

            // Top bar zones (keep them when adding things): [home, brand, crumb] [modes 1/2/3/4] [telemetry status chip: DkTopSlotFuture] [view actions: pins + active-effect chips, isolate, reset] [docks] [language, settings, help].
            // DkTopSlotFuture holds the telemetry status chip (WP-11): link dot + honest badge text, click opens mode 4.
            var futureSlot = new VisualElement { name = "DkTopSlotFuture", pickingMode = PickingMode.Ignore };
            futureSlot.AddToClassList("dk-top-slot");
            futureSlot.Add(_tel.StatusChip);
            _top.Add(futureSlot);

            _btnPins = IconButton("pin", "DkPins", () => _ui.ShortcutTogglePins());
            _btnIso = IconButton("iso", "DkIso", () => _ui.ShortcutToggleIsolation());
            _btnPins.AddToClassList("dk-opt-l5"); // given up at level 5 of the top bar (their keys H / I remain)
            _btnIso.AddToClassList("dk-opt-l5");
            var reset = IconButton("reset", "DkReset", () => _ui.ShortcutResetView());
            _top.Add(_btnPins);
            _chipFilter = MakeChip("filter", "DkChipFilter", () => _ui.ShortcutClearFilter());
            _chipExplode = MakeChip("explode", "DkChipExplode", () => _ui.ShortcutClearExplode());
            _chipCut = MakeChip("cut", "DkChipCut", () => CrossSectionManager.Instance?.DisableCrossSection());
            _top.Add(_chipFilter);
            _top.Add(_chipExplode);
            _top.Add(_chipCut);
            // drone power chip (provisional home of the power-off control; see PLAN_PC.md section 19b): visible while the drone is on, click powers it off
            _chipPower = MakeChip("power", "DkChipPower", () => DroneStateController.Instance?.TurnOff());
            _top.Add(_chipPower);
            RegisterActiveEffects(); // MVP P1: the same registry holds the four chips above and one chip per toggleable tool of the telemetry mode
            // second chip: an active telemetry session (same chip style; click = stop the session)
            _top.Add(_tel.SessionChip);
            _top.Add(_btnIso);
            _top.Add(reset);
            _top.Add(Sep());
            _btnTree = IconButton("sidel", "DkTree", () => _tree.SetOpen(!_tree.IsOpen));
            _btnInspector = IconButton("sider", "DkInspector", ToggleInspector);
            _top.Add(_btnTree);
            _top.Add(_btnInspector);
            _top.Add(Sep());

            var lang = new VisualElement();
            lang.AddToClassList("dk-lang");
            var langIcon = new DesktopVectorIcon("language", 16);
            langIcon.AddToClassList("dk-lang-icon");
            _icons.Add(langIcon);
            lang.Add(langIcon);
            _btnLangEn = new Button(() => AppLanguageManager.SetLanguage("en")) { text = "EN" };
            _btnLangEs = new Button(() => AppLanguageManager.SetLanguage("es")) { text = "ES" };
            _btnLangEn.AddToClassList("dk-lang-btn");
            _btnLangEs.AddToClassList("dk-lang-btn");
            _langPill = new SlidingPill(lang, "dk-lang-pill");
            lang.Add(_btnLangEn);
            lang.Add(_btnLangEs);
            _top.Add(lang);

            _btnGear = IconButton("gear", "DkSettings", ToggleSettings);
            _btnHelp = IconButton("help", "DkHelp", () => _ctrl.Help?.Toggle());
            _top.Add(_btnGear);
            _top.Add(_btnHelp);
            _root.Add(_top);
        }

        // ───────────────────────── rails ─────────────────────────

        private void BuildRails()
        {
            _railL = new VisualElement { name = "DkRailL" };
            _railL.AddToClassList("dk-rail");
            _railL.AddToClassList("dk-rail--left");
            _railTree = IconButton("tree", "DkRailTree", () => _tree.SetOpen(true), 18);
            _railSearch = IconButton("search", "DkRailSearch", () => _tree.Open(true), 18);
            _railL.Add(_railTree);
            _railL.Add(_railSearch);
            _root.Add(_railL);

            _railR = new VisualElement { name = "DkRailR" };
            _railR.AddToClassList("dk-rail");
            _railR.AddToClassList("dk-rail--right");
            _railInspector = IconButton("sider", "DkRailInspector", ToggleInspector, 18);
            _railR.Add(_railInspector);
            _root.Add(_railR);
        }

        // ───────────────────────── status bar ─────────────────────────

        private void BuildStatus()
        {
            _status = new VisualElement { name = "DkStatus" };
            _status.AddToClassList("dk-status");
            _hint = new Label { pickingMode = PickingMode.Ignore };
            _hint.AddToClassList("dk-status-text");
            _status.Add(_hint);
            Grow(_status);

            _tlg = new VisualElement { pickingMode = PickingMode.Ignore };
            _tlg.AddToClassList("dk-tlg");
            _tlgMin = new Label("20") { pickingMode = PickingMode.Ignore };
            _tlgMax = new Label("100") { pickingMode = PickingMode.Ignore };
            _tlgMin.AddToClassList("dk-status-text");
            _tlgMax.AddToClassList("dk-status-text");
            _tlgBar = new VisualElement { pickingMode = PickingMode.Ignore };
            _tlgBar.AddToClassList("dk-tlg-bar");
            _tlgBar.style.backgroundImage = new StyleBackground(MakeGradient());
            _tlg.Add(_tlgMin);
            _tlg.Add(_tlgBar);
            _tlg.Add(_tlgMax);
            _tlg.style.display = DisplayStyle.None;
            _status.Add(_tlg);

            _quality = new Label { pickingMode = PickingMode.Ignore };
            _quality.AddToClassList("dk-status-text");
            _status.Add(_quality);

            var help = new Button(() => _ctrl.Help?.Toggle()) { name = "DkStatusHelp" };
            help.AddToClassList("dk-status-btn");
            help.Add(Kbd("?"));
            _helpLabel = new Label { pickingMode = PickingMode.Ignore };
            _helpLabel.AddToClassList("dk-status-text");
            help.Add(_helpLabel);
            _status.Add(help);
            _root.Add(_status);
        }

        private static Texture2D MakeGradient()
        {
            var tex = new Texture2D(128, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color[] stops =
            {
                new Color(0.16f, 0.29f, 0.84f), new Color(0.13f, 0.72f, 0.81f), new Color(0.48f, 0.85f, 0.34f),
                new Color(0.96f, 0.85f, 0.04f), new Color(1.0f, 0.42f, 0.17f), new Color(0.84f, 0.14f, 0.49f)
            };
            for (int x = 0; x < 128; x++)
            {
                float f = x / 127f * (stops.Length - 1);
                int i = Mathf.Min((int)f, stops.Length - 2);
                tex.SetPixel(x, 0, Color.Lerp(stops[i], stops[i + 1], f - i));
            }

            tex.Apply();
            return tex;
        }

        // ───────────────────────── settings (CONFIG inside the viewer) ─────────────────────────

        private void BuildSettings()
        {
            _settings = new VisualElement { name = "DkSettings" };
            _settings.AddToClassList("dk-settings");
            _settings.AddToClassList("dk-settings--hidden");
            DesktopMotion.WatchVisible(_settings);

            var head = new VisualElement();
            head.AddToClassList("dk-pop-head");
            var title = new Label { name = "DkSettingsTitle", pickingMode = PickingMode.Ignore };
            title.AddToClassList("dk-pop-title");
            head.Add(title);
            var close = IconButton("close", "DkSettingsClose", CloseSettings, 16);
            close.AddToClassList("dk-ibtn--small");
            head.Add(close);
            _settings.Add(head);

            _settingsBody = new VisualElement();
            _settingsBody.AddToClassList("dk-settings-body");
            _settings.Add(_settingsBody);
            _root.Add(_settings);
        }

        private void ToggleSettings()
        {
            if (_settingsOpen) CloseSettings(); else OpenSettings();
        }

        public void OpenSettings()
        {
            if (_settingsOpen || _ui.IsHeroVisible) return;

            VisualElement cfg = _root.Q<VisualElement>("HeroSubmenu_Config");
            if (cfg == null) return;

            _cfgOriginalParent = cfg.parent;
            _cfgOriginalIndex = _cfgOriginalParent != null ? _cfgOriginalParent.IndexOf(cfg) : 0;
            _settingsBody.Add(cfg);
            cfg.AddToClassList("hero-submenu--active");
            cfg.AddToClassList("dk-config-embedded");
            _settings.RemoveFromClassList("dk-settings--hidden");
            _settings.BringToFront();
            _settingsOpen = true;
            _btnGear.AddToClassList("dk-ibtn--on");
        }

        public void CloseSettings()
        {
            if (!_settingsOpen) return;

            VisualElement cfg = _root.Q<VisualElement>("HeroSubmenu_Config");
            if (cfg != null)
            {
                cfg.RemoveFromClassList("dk-config-embedded");
                cfg.RemoveFromClassList("hero-submenu--active");
                if (_cfgOriginalParent != null) _cfgOriginalParent.Insert(Mathf.Min(_cfgOriginalIndex, _cfgOriginalParent.childCount), cfg);
            }

            _settings.AddToClassList("dk-settings--hidden");
            _settingsOpen = false;
            _btnGear?.RemoveFromClassList("dk-ibtn--on");
        }

        // ───────────────────────── mode panels as strip + popovers ─────────────────────────

        private void PrepareModePanels()
        {
            _toolsGrid = _root.Q<VisualElement>("ToolsCardGrid");
            _analyzeGrid = _root.Q<VisualElement>("AnalyzeCardGrid");
            _studioContainer = _root.Q<VisualElement>("StudioModeContainer");
            _studioPanel = _root.Q<VisualElement>("StudioPanel");
            _toolsGrid?.AddToClassList("dk-strip");
            _analyzeGrid?.AddToClassList("dk-strip");

            foreach (string n in new[] { "CrossSectionPanel", "FilterSubPanel", "ExplodeInlineSlider", "ToolPowerLoadPanel" })
            {
                VisualElement pop = _root.Q<VisualElement>(n);
                pop?.AddToClassList("dk-pop");
                DesktopMotion.WatchVisible(pop);
            }

            DesktopMotion.WatchVisible(_root.Q<VisualElement>("StudioPanel"));
            DesktopMotion.WatchVisible(_root.Q<VisualElement>("DesktopPartTree"));

            ClearInlineStyles(_root.Q<VisualElement>("ToolPowerLoadPanel"));
            ClearInlineStyles(_root.Q<VisualElement>("ToolPowerStateLabel"));
            ClearInlineStyles(_root.Q<VisualElement>("ToolPowerLoadValue"));
            ClearInlineStyles(_root.Q<VisualElement>("ToolPowerLoadSlider"));

            DecorateDesktopControls();

            // Render modes hidden in the shared UXML (Realistic, Wireframe, Ghosted) are exposed on PC.
            foreach (string n in new[] { "ShaderMode_Realistic", "ShaderMode_Wireframe", "ShaderMode_Ghosted" })
            {
                var b = _root.Q<Button>(n);
                if (b != null) b.style.display = DisplayStyle.Flex;
            }

            if (_studioContainer != null && _studioPanel != null)
            {
                _studioStrip = new VisualElement { name = "DkStudioStrip" };
                _studioStrip.AddToClassList("dk-strip");
                _studioStrip.AddToClassList("dk-strip--studio");
                _studioRender = new Button(() => SetStudioSection(_studioSection == "render" ? "none" : "render")) { name = "DkStudioRender" };
                _studioRender.AddToClassList("dk-strip-btn");
                _renderIcon = new DesktopVectorIcon("realistic", 16) { name = "DkRenderIcon" };
                _icons.Add(_renderIcon);
                _studioRender.Add(_renderIcon);
                _studioRenderLabel = new Label { pickingMode = PickingMode.Ignore };
                _studioRenderLabel.AddToClassList("dk-strip-text");
                _studioRender.Add(_studioRenderLabel);
                _studioRender.Add(Kbd("V"));
                _studioEnv = new Button(() => SetStudioSection(_studioSection == "env" ? "none" : "env")) { name = "DkStudioEnv" };
                _studioEnv.AddToClassList("dk-strip-btn");
                var envIcon = new DesktopVectorIcon("env_time", 16);
                _icons.Add(envIcon);
                _studioEnv.Add(envIcon);
                var envLabel = new Label { name = "DkEnvLabel", pickingMode = PickingMode.Ignore };
                envLabel.AddToClassList("dk-strip-text");
                _studioEnv.Add(envLabel);
                _studioStrip.Add(_studioRender);
                _studioStrip.Add(_studioEnv);
                _studioContainer.Insert(0, _studioStrip);
                _studioPanel.AddToClassList("dk-pop");
                _studioPanel.AddToClassList("dk-pop--studio");
                foreach (VisualElement ch in _studioPanel.Children()) { if (ch.ClassListContains("submenu-title")) { ch.name = "DkTitleRender"; break; } }
                SetStudioSection("render");
            }

            // Tool strip tags: icons are the existing procedural ones; keep them small via USS.
        }

        private static void ClearInlineStyles(VisualElement e)
        {
            if (e == null) return;
            e.style.width = StyleKeyword.Null;
            e.style.minHeight = StyleKeyword.Null;
            e.style.alignSelf = StyleKeyword.Null;
            e.style.marginTop = StyleKeyword.Null;
            e.style.paddingTop = StyleKeyword.Null;
            e.style.paddingLeft = StyleKeyword.Null;
            e.style.paddingRight = StyleKeyword.Null;
            e.style.paddingBottom = StyleKeyword.Null;
            e.style.borderTopWidth = StyleKeyword.Null;
            e.style.borderBottomWidth = StyleKeyword.Null;
            e.style.borderLeftWidth = StyleKeyword.Null;
            e.style.borderRightWidth = StyleKeyword.Null;
            e.style.borderTopLeftRadius = StyleKeyword.Null;
            e.style.borderTopRightRadius = StyleKeyword.Null;
            e.style.borderBottomLeftRadius = StyleKeyword.Null;
            e.style.borderBottomRightRadius = StyleKeyword.Null;
            e.style.backgroundColor = StyleKeyword.Null;
            e.style.fontSize = StyleKeyword.Null;
            e.style.color = StyleKeyword.Null;
            e.style.unityFontStyleAndWeight = StyleKeyword.Null;
            e.style.letterSpacing = StyleKeyword.Null;
            e.style.flexGrow = StyleKeyword.Null;
            e.style.marginBottom = StyleKeyword.Null;
            e.style.flexDirection = StyleKeyword.Null;
            e.style.justifyContent = StyleKeyword.Null;
            e.style.alignItems = StyleKeyword.Null;
        }

        private void SetStudioSection(string section)
        {
            _studioSection = section;
            if (_studioPanel == null) return;
            _studioPanel.EnableInClassList("dk-studio--render", section == "render");
            _studioPanel.EnableInClassList("dk-studio--env", section == "env");
            _studioPanel.EnableInClassList("dk-studio--none", section == "none");
            _studioRender?.EnableInClassList("dk-strip-btn--on", section == "render");
            _studioEnv?.EnableInClassList("dk-strip-btn--on", section == "env");
        }

        private void BuildSheetFooter()
        {
            VisualElement sheet = _root.Q<VisualElement>("BottomSheet");
            if (sheet == null) return;

            var footer = new VisualElement { name = "DkSheetFooter" };
            footer.AddToClassList("dk-sheet-footer");
            var focus = new Button(() => _ui.ShortcutFocusSelection()) { name = "DkFocus" };
            focus.AddToClassList("dk-btn");
            focus.Add(NewIcon("focus", 16));
            var fl = new Label { name = "DkFocusLabel", pickingMode = PickingMode.Ignore };
            fl.AddToClassList("dk-btn-text");
            focus.Add(fl);
            focus.Add(Kbd("F"));
            var iso = new Button(() => _ui.ShortcutToggleIsolation()) { name = "DkIsolate" };
            iso.AddToClassList("dk-btn");
            iso.Add(NewIcon("isolate", 16));
            var il = new Label { name = "DkIsolateLabel", pickingMode = PickingMode.Ignore };
            il.AddToClassList("dk-btn-text");
            iso.Add(il);
            iso.Add(Kbd("I"));
            footer.Add(focus);
            footer.Add(iso);
            sheet.Add(footer);
        }

        // ───────────────────────── shortcuts used by the controller ─────────────────────────

        /// <summary>C / G: open ANALYZE (if needed) and press the CUT / FILTER tool.</summary>
        public void OpenAnalyzeTool(string buttonName)
        {
            if (_ui.IsHeroVisible || _ui.IsOnboardingOpen) return;

            Button src = _root.Q<Button>(buttonName);
            bool analyzeActive = _modeSrc[1] != null && _modeSrc[1].ClassListContains("mode-btn--active");
            if (!analyzeActive)
            {
                _ui.ShortcutToggleMode(2);
                _root.schedule.Execute(() => Fire(src)).StartingIn(80);
            }
            else
            {
                Fire(src);
            }
        }

        /// <summary>V: next render mode (Realistic, X-Ray, Solid, Thermal, Wireframe, Ghosted).</summary>
        public void CycleViewMode()
        {
            if (_ui.IsHeroVisible || _ui.IsOnboardingOpen || ViewModeManager.Instance == null) return;

            ViewMode cur = ViewModeManager.Instance.CurrentMode;
            int idx = Array.IndexOf(CycleOrder, cur);
            ViewMode next = CycleOrder[(idx + 1 + CycleOrder.Length) % CycleOrder.Length];
            ViewModeManager.Instance.SetViewMode(next);
        }

        /// <summary>The mode switch state: 1 Inspect, 2 Analyze, 3 Studio, 4 Telemetry, 0 none (PLAN_PC 27.2: the pad's LB / RB step through them).</summary>
        public int ActiveModeIndex()
        {
            for (int i = 0; i < 4; i++) if (_modeSeg[i] != null && _modeSeg[i].ClassListContains("dk-seg-btn--on")) return i + 1;
            return 0;
        }

        /// <summary>LB / RB in the pad's MENÚS mode: the previous / next of the four modes (wraps around), exactly the keys 1..4.</summary>
        public void PadStepMode(int delta)
        {
            if (_ui.IsHeroVisible || _ui.IsOnboardingOpen) return;
            int cur = ActiveModeIndex();
            int next = cur == 0 ? (delta > 0 ? 1 : 4) : ((cur - 1 + delta + 4) % 4) + 1;
            Debug.Log("[Pad] mode " + cur + " -> " + next);
            if (next == 4) _tel.OpenMode(); else _ui.ShortcutToggleMode(next);
        }

        // ───────────────────────── per frame ─────────────────────────

        private const float MinSpacer = 12f;
        private int _barLevel;
        private int _barExtra;
        private float _barLastWidth = -1f;
        private int _barShown, _barKey, _barKeyPrev = -1, _barWait;

        /// <summary>How much the top bar has given up (0 = nothing, 6 = everything optional). Read by the probes.</summary>
        public int BarLevel => _barLevel;

        /// <summary>True when two items of the top bar overlap or the last one leaves the bar (the layout of the previous frame).</summary>
        public bool TopBarOverflows()
        {
            if (_top == null || _top.resolvedStyle.display == DisplayStyle.None) return false;
            Rect bar = _top.worldBound;
            if (bar.width < 10f) return false;
            float prevRight = float.NegativeInfinity;
            bool over = false;
            int shown = 0;
            for (int ci = 0; ci < _top.childCount; ci++) // F-6: Children() boxes an enumerator every frame
            {
                VisualElement c = _top[ci];
                if (c.resolvedStyle.display == DisplayStyle.None) continue;
                Rect r = c.worldBound;
                shown++; // counted by display, not by width: a squeezed item must not look like one that came or went
                // the two spacers around the mode switch must keep some air: with none left the bar is cramped even if nothing overlaps
                if (c.resolvedStyle.flexGrow > 0f && r.width < MinSpacer) over = true;
                if (r.width < 1f) continue;
                if (r.xMin < prevRight - 0.5f) over = true;
                // an item squeezed below its content (the mode switch) paints its children outside its own box: measure them too
                float right = r.xMax;
                for (int gi = 0; gi < c.childCount; gi++)
                {
                    VisualElement g = c[gi];
                    if (g.resolvedStyle.display == DisplayStyle.None || g.resolvedStyle.position == Position.Absolute) continue;
                    Rect gr = g.worldBound;
                    if (gr.width < 1f) continue;
                    right = Mathf.Max(right, gr.xMax);
                }

                prevRight = Mathf.Max(prevRight, right);
            }

            _barShown = shown;
            return over || prevRight > bar.xMax + 0.5f;
        }

        /// <summary>
        /// The top bar degrades from the least important item to the most: (1) the "select a part" placeholder, (2) the word "TwinSight"
        /// (the "X500" mark stays), (3) the status chip gets a shorter box (its text truncates, the dot and the start of the honest label
        /// stay), (4) the globe of the language switch, (5) the pins and isolate buttons (their keys H / I remain) and tighter mode buttons,
        /// (6) the key caps of the mode switch. The mode switch with its keys is therefore the last thing to go. The level comes from the
        /// logical width; if the layout still overflows (UI size 150 %, a long part name, several chips) the bar gives up one more level
        /// per frame until nothing overlaps, and keeps that until the width changes.
        /// </summary>
        private static string[] _barLevelClasses;

        private void UpdateBarLevel(float logicalWidth)
        {
            if (Mathf.Abs(logicalWidth - _barLastWidth) > 0.5f) { _barLastWidth = logicalWidth; _barExtra = 0; }
            int level = 0;
            while (level < BarLevelBelow.Length && logicalWidth < BarLevelBelow[level]) level++;
            bool over = TopBarOverflows();
            if (_barKey != _barKeyPrev) { _barKeyPrev = _barKey; _barExtra = 0; _barWait = 4; } // a chip came or went, or a part was (de)selected: measure again
            else if (_barWait > 0) _barWait--; // the styles and the layout trail the class change by a few frames: judge the new level only after they caught up
            else if (over && level + _barExtra < BarLevelBelow.Length) { _barExtra++; _barWait = 4; }
            _barLevel = Mathf.Min(BarLevelBelow.Length, level + _barExtra);
            _root.EnableInClassList("dk-narrowbar", _barLevel >= 1);
            if (_barLevelClasses == null)
            {
                _barLevelClasses = new string[BarLevelBelow.Length + 1];
                for (int i = 1; i <= BarLevelBelow.Length; i++) _barLevelClasses[i] = "dk-bar-l" + i;
            }

            for (int i = 1; i <= BarLevelBelow.Length; i++) _root.EnableInClassList(_barLevelClasses[i], _barLevel >= i); // F-6: no string per frame
        }

        /// <summary>Verification harness: the sliding pills and the item each one belongs under.</summary>
        internal SlidingPill SegPill => _segPill;
        internal SlidingPill LangPill => _langPill;
        internal Button ActiveModeSeg { get { for (int i = 3; i >= 0; i--) if (_modeSeg[i] != null && _modeSeg[i].ClassListContains("dk-seg-btn--on")) return _modeSeg[i]; return null; } }
        internal Button ActiveLangButton => AppLanguageManager.IsSpanish ? _btnLangEs : _btnLangEn;

        /// <summary>Puts a chip that is only needed sometimes (the red recording chip) into the top bar the first time it is needed, right after the session chip: while it is not there it costs the bar nothing.</summary>
        internal void AttachTopChip(VisualElement chip)
        {
            if (chip == null || chip.parent != null || _top == null) return;
            int at = _tel != null && _tel.SessionChip != null ? _top.IndexOf(_tel.SessionChip) : -1;
            if (at >= 0) _top.Insert(at + 1, chip); else _top.Add(chip);
        }

        public void Tick(float logicalWidth)
        {
            // segmented mode switch mirrors the (hidden) capsule buttons; the pill slides under the active one
            Button activeSeg = null;
            bool anyOfThree = false;
            for (int i = 0; i < 3; i++)
            {
                bool on = _modeSrc[i] != null && _modeSrc[i].ClassListContains("mode-btn--active");
                _modeSeg[i].EnableInClassList("dk-seg-btn--on", on);
                if (on) { activeSeg = _modeSeg[i]; anyOfThree = true; }
            }

            // mode 4 (Telemetry): exclusive with the other three
            if (anyOfThree && _tel.ModeOpen) _tel.CloseMode();
            _modeSeg[3].EnableInClassList("dk-seg-btn--on", _tel.ModeOpen);
            if (_tel.ModeOpen) activeSeg = _modeSeg[3];

            _segPill?.Update(activeSeg);
            _langPill?.Update(AppLanguageManager.IsSpanish ? _btnLangEs : _btnLangEn);
            DesktopMotion.TickWatched();
            TickFeedback();

            // crumb: the existing header label already has the localized part name / "select a part"
            Label ctx = _qCtx ?? (_qCtx = _root.Q<Label>("TopContextLabel"));
            bool hasSel = SelectionManager.Instance != null && SelectionManager.Instance.CurrentSelection != null;
            string text = ctx != null ? ctx.text : string.Empty;
            if (_crumb.text != text) _crumb.text = text;
            _crumb.EnableInClassList("dk-crumb--has", hasSel);
            // narrow windows: the "select a part" placeholder gives its room to the telemetry chips; a real part name stays (truncated)
            _crumb.style.display = string.IsNullOrEmpty(text) || (_barLevel >= 1 && !hasSel) ? DisplayStyle.None : DisplayStyle.Flex;

            bool treeOpen = _tree.IsOpen;
            bool sheetOpen = _side.IsOpen;
            if (sheetOpen && !_sheetWasOpen)
            {
                Foldout ident = _root.Q<Foldout>("FoldoutIdentification");
                if (ident != null) ident.value = true;
            }

            _sheetWasOpen = sheetOpen;
            SetOn(_btnTree, treeOpen);
            SetOn(_btnInspector, sheetOpen);
            // pins: the real state is the hotspot manager (the card class can drift after Home / re-entry and used to invert the button)
            bool pinsVisible = HotspotManager.Instance != null && HotspotManager.Instance.IsVisible;
            SetOn(_btnPins, pinsVisible);
            (_qHotspotBtn ?? (_qHotspotBtn = _root.Q<Button>("ToolHotspotBtn")))?.EnableInClassList("submenu-card--active", pinsVisible);
            _tel.Tick();
            _fx.Tick(); // MVP P1: filter, explode, cut, power and every tool chip, read from their owners
            RelabelPowerCard();
            // narrow windows (1280x720 = 1391 logical px): the mode keys, a long part name and the status chip give way so the bar never overlaps
            // what the bar shows, as a key: its change (not the layout, which the levels themselves change) restarts the overflow guard
            _barKey = (_ui.FilterActive ? 1 : 0) | (ExplodedViewManager.Instance != null && ExplodedViewManager.Instance.GetExplosionFactor() > 0.001f ? 2 : 0)
                      | (CrossSectionManager.Instance != null && CrossSectionManager.Instance.IsEnabled ? 4 : 0) | (DroneStateController.Instance != null && DroneStateController.Instance.IsOn ? 8 : 0)
                      | (_tel.IsRunning ? 16 : 0) | (hasSel ? 32 : 0) | (_fx.Mask << 6);
            UpdateBarLevel(logicalWidth);
            SetOn(_btnIso, (_qIsolateBtn ?? (_qIsolateBtn = _root.Q<Button>("ToolIsolateBtn")))?.ClassListContains("submenu-card--active") == true);
            SetOn(_btnGear, _settingsOpen);
            _btnLangEn.EnableInClassList("dk-lang-btn--on", !AppLanguageManager.IsSpanish);
            _btnLangEs.EnableInClassList("dk-lang-btn--on", AppLanguageManager.IsSpanish);

            _railL.style.display = treeOpen ? DisplayStyle.None : DisplayStyle.Flex;
            _railR.style.display = sheetOpen ? DisplayStyle.None : DisplayStyle.Flex;

            bool viewer = !_ui.IsHeroVisible && !_ui.IsOnboardingOpen;
            _top.style.display = viewer ? DisplayStyle.Flex : DisplayStyle.None;
            _status.style.display = viewer ? DisplayStyle.Flex : DisplayStyle.None;
            if (!viewer)
            {
                _railL.style.display = DisplayStyle.None;
                _railR.style.display = DisplayStyle.None;
                if (_settingsOpen) CloseSettings();
            }

            // thermal legend, horizontal, from the original (hidden) legend
            VisualElement legend = _qLegend ?? (_qLegend = _root.Q<VisualElement>("ThermalLegendContainer"));
            bool thermal = legend != null && !legend.ClassListContains("thermal-legend--hidden");
            _tlg.style.display = thermal ? DisplayStyle.Flex : DisplayStyle.None;
            if (thermal)
            {
                _tlgMin.text = (_qTMin ?? (_qTMin = _root.Q<Label>("ThermalMinTemp")))?.text ?? string.Empty;
                _tlgMax.text = (_qTMax ?? (_qTMax = _root.Q<Label>("ThermalMaxTemp")))?.text ?? string.Empty;
            }

            // PLAN_PC F-6: these labels were rebuilt (string concat + enum boxing) every frame; rebuild only when the value or the language changes
            var tierNow = RenderQualityController.ResolvedTier;
            bool esNow = AppLanguageManager.IsSpanish;
            if (!_qualityInit || !tierNow.Equals(_qualityTier) || esNow != _qualityEs)
            {
                _qualityInit = true; _qualityTier = tierNow; _qualityEs = esNow;
                _quality.text = T("Quality ", "Calidad ") + tierNow;
            }

            // Studio strip: current render mode on the button, default section when the mode opens
            if (_studioContainer != null)
            {
                bool studioVisible = !_studioContainer.ClassListContains("mode--hidden");
                if (studioVisible && !_studioWasVisible) SetStudioSection("render");
                _studioWasVisible = studioVisible;
                if (_studioRenderLabel != null && ViewModeManager.Instance != null)
                {
                    var modeNow = ViewModeManager.Instance.CurrentMode;
                    if (!_studioInit || !modeNow.Equals(_studioMode) || esNow != _studioEs)
                    {
                        _studioInit = true; _studioMode = modeNow; _studioEs = esNow;
                        _studioRenderLabel.text = T("Render: ", "Render: ") + ModeName(modeNow);
                        _renderIcon?.SetShape(ModeIcon(modeNow));
                    }
                }
            }

            // Auto-open the inspector on a NEW selection when the window is wide enough; it closes on deselect (side panel).
            Transform sel = SelectionManager.Instance != null ? SelectionManager.Instance.CurrentSelection : null;
            if (sel != _lastSelection)
            {
                _lastSelection = sel;
                if (sel != null && viewer && logicalWidth >= AutoOpenMinLogicalWidth && !_side.IsOpen)
                {
                    UIDetailsSheet.Instance?.ShowInfo();
                }
            }

            // Status hint changes with the context
            _hint.text = T("Drag background: orbit · Shift+right drag: pan · Wheel: zoom to cursor · Double click: focus",
                           "Arrastrar fondo: orbitar · Shift+clic der.: desplazar · Rueda: zoom al cursor · Doble clic: enfocar");
            _helpLabel.text = T("Shortcuts", "Atajos");

            CentreHeroLeads();
            foreach (DesktopVectorIcon ic in _icons) ic.Refresh();
        }

        private Label _powerCardLabel, _powerNote;

        // PLAN_PC F-6: per-frame UQuery results and derived strings, cached (the elements live as long as the document)
        private Label _qCtx, _qTMin, _qTMax;
        private Button _qHotspotBtn, _qIsolateBtn, _qPowerBtn;
        private VisualElement _qLegend;
        private bool _qualityInit, _qualityEs, _studioInit, _studioEs;
        private QualityTier _qualityTier;
        private ViewMode _studioMode;

        /// <summary>
        /// D-6 / WP-11: the Inspector's power card is the MANUAL SIMULATION ("Energia (simulada)"), not the telemetry session (the session
        /// switch "Encendido" is in mode 4). The simulation popover says so; its FLYING state reads "Carga alta (simulado)" (InspectModeHandler).
        /// </summary>
        private void RelabelPowerCard()
        {
            if (_powerCardLabel == null)
            {
                _powerCardLabel = _root.Q<Button>("ToolPowerBtn")?.Q<Label>(className: "submenu-label");
                VisualElement panel = _root.Q<VisualElement>("ToolPowerLoadPanel");
                if (panel != null && _powerNote == null)
                {
                    _powerNote = new Label { name = "DkPowerNote", pickingMode = PickingMode.Ignore };
                    _powerNote.AddToClassList("dk-note");
                    _powerNote.AddToClassList("dk-note--muted");
                    panel.Add(_powerNote);
                }
            }

            string lang = AppLanguageManager.IsSpanish ? "es" : "en";
            string t = TelemetryStrings.T("power.sim", lang);
            if (_powerCardLabel != null && _powerCardLabel.text != t) _powerCardLabel.text = t;
            Button powerBtn = _qPowerBtn ?? (_qPowerBtn = _root.Q<Button>("ToolPowerBtn"));
            string tip = TelemetryStrings.T("power.sim.tip", lang);
            if (powerBtn != null && powerBtn.tooltip != tip) powerBtn.tooltip = tip;
            string n = TelemetryStrings.T("power.sim.note", lang);
            if (_powerNote != null && _powerNote.text != n) _powerNote.text = n;
        }

        /// <summary>Selection feedback (part pill, inspector title) and the hero entrance.</summary>
        private void TickFeedback()
        {
            string crumb = _crumb.text;
            if (crumb != _lastCrumb)
            {
                if (_lastCrumb != null) DesktopMotion.Flash(_crumb, "dk-flash", 200);
                _lastCrumb = crumb;
            }

            if (_sheetTitle == null) _sheetTitle = _root.Q<Label>("SheetTitle");
            if (_sheetTitle != null && _sheetTitle.text != _lastTitle)
            {
                if (_lastTitle != null) DesktopMotion.Enter(_sheetTitle, 0, "dk-enter--title");
                _lastTitle = _sheetTitle.text;
            }

            bool hero = _ui.IsHeroVisible;
            if (hero && !_heroWas)
            {
                string[] names = { "HeroTagline", "HeroTitleContainer", "HeroSubtitle", "HeroExploreBtn", "HeroDeviceBtn", "HeroConfigBtn", "HeroExitBtn", "HeroCornerActions" };
                for (int i = 0; i < names.Length; i++)
                {
                    VisualElement e = _root.Q<VisualElement>(names[i]);
                    if (e == null) continue;
                    e.AddToClassList("dk-anim-in");
                    DesktopMotion.Enter(e, 70 * i);
                    VisualElement captured = e;
                    e.schedule.Execute(() => captured.RemoveFromClassList("dk-anim-in")).StartingIn(70 * i + 900);
                }
            }

            _heroWas = hero;
        }

        private static void SetOn(Button b, bool on)
        {
            if (b != null) b.EnableInClassList("dk-ibtn--on", on);
        }

        private static string ModeIcon(ViewMode m)
        {
            switch (m)
            {
                case ViewMode.XRay: return "xray";
                case ViewMode.SolidColor: return "solid";
                case ViewMode.Thermal: return "thermal";
                case ViewMode.Wireframe: return "wire";
                case ViewMode.Ghosted: return "ghost";
                case ViewMode.Blueprint: return "studio";
                default: return "realistic";
            }
        }

        private static string ModeName(ViewMode m)
        {
            switch (m)
            {
                case ViewMode.XRay: return AppLanguageManager.IsSpanish ? "Rayos X" : "X-Ray";
                case ViewMode.SolidColor: return AppLanguageManager.IsSpanish ? "Sólido" : "Solid";
                case ViewMode.Thermal: return AppLanguageManager.IsSpanish ? "Térmico" : "Thermal";
                case ViewMode.Wireframe: return AppLanguageManager.IsSpanish ? "Alambre" : "Wire";
                case ViewMode.Ghosted: return AppLanguageManager.IsSpanish ? "Fantasma" : "Ghost";
                case ViewMode.Blueprint: return AppLanguageManager.IsSpanish ? "Plano" : "Blueprint";
                default: return AppLanguageManager.IsSpanish ? "Realista" : "Realistic";
            }
        }

        private void OnLanguage(string _)
        {
            _modeText[0].text = T("Inspect", "Inspeccionar");
            _modeText[1].text = T("Analyze", "Analizar");
            _modeText[2].text = T("Studio", "Estudio");
            _modeText[3].text = T("Telemetry", "Telemetría");
            _root.Q<Label>("DkSettingsTitle").text = T("Settings", "Ajustes");
            var envLabel = _root.Q<Label>("DkEnvLabel");
            if (envLabel != null) envLabel.text = T("Environment", "Entorno");
            var fl = _root.Q<Label>("DkFocusLabel");
            if (fl != null) fl.text = T("Focus", "Enfocar");
            var il = _root.Q<Label>("DkIsolateLabel");
            if (il != null) il.text = T("Isolate", "Aislar");
            _btnPins.tooltip = T("Pins", "Puntos");
            _fx?.Relabel();
            _btnIso.tooltip = T("Isolate selected part", "Aislar la pieza seleccionada");
            _root.Q<Button>("DkReset").tooltip = T("Reset view", "Restablecer vista");
            _btnTree.tooltip = T("Parts tree", "Árbol de piezas");
            _btnInspector.tooltip = T("Part inspector", "Inspector de pieza");
            _btnGear.tooltip = T("Settings", "Ajustes");
            _btnHelp.tooltip = T("Shortcuts", "Atajos");
            _railTree.tooltip = T("Parts tree", "Árbol de piezas");
            _railSearch.tooltip = T("Search part", "Buscar pieza");
            _railInspector.tooltip = T("Part inspector", "Inspector de pieza");
            _modeSeg[0].tooltip = T("Inspect: pins, isolate, simulated power", "Inspeccionar: puntos, aislar, energía simulada");
            _modeSeg[3].tooltip = T("Telemetry: power, source, alerts and faults", "Telemetría: encendido, fuente, alertas y fallas");
            _modeSeg[1].tooltip = T("Analyze: cut, explode, filter", "Analizar: corte, explosión, filtro");
            _modeSeg[2].tooltip = T("Studio: render and environment", "Estudio: render y entorno");
        }
    }
}
#endif
