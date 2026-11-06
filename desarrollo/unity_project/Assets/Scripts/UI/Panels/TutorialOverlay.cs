using System;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.UI.ProceduralIcons;

namespace WebGL.UI.Panels
{
    /// <summary>
    /// The 6-step tutorial of the approved sheet (docs/unity_pc/maqueta_iconos), shared by PC, WebGL and mobile.
    /// Two layouts: a centred card with progress, key caps and Left / Right / Esc (mouse) and a bottom sheet with gestures
    /// (touch devices or a narrow window). Content: 1 Navegar, 2 Seleccionar, 3 Modos y aislar, 4 Analizar, 5 Estudio, 6 Listo.
    /// Hints per input: PC build (keys), WebGL with a mouse (buttons and mouse), touch (gestures). The small switch of the card
    /// swaps the hints between mouse and touch. Created and driven by OnboardingController (Esc is handled by UIManager).
    /// </summary>
    internal sealed class TutorialOverlay
    {
        private struct Row
        {
            public string Icon, NameEs, NameEn;
            public string[] HintsEs, HintsEn;
        }

        private struct Step
        {
            public string Icon, TitleEs, TitleEn, DescEs, DescEn;
            public Row[] Pc, Mouse, Touch;
        }

        private static Row R(string icon, string es, string en, string hintsEs, string hintsEn = null)
        {
            return new Row
            {
                Icon = icon, NameEs = es, NameEn = en,
                HintsEs = hintsEs.Split('|'), HintsEn = (hintsEn ?? hintsEs).Split('|')
            };
        }

        private static readonly Step[] Steps =
        {
            new Step
            {
                Icon = "orbit", TitleEs = "Navegar", TitleEn = "Navigate",
                DescEs = "Mueve la cámara alrededor del dron.", DescEn = "Move the camera around the drone.",
                Pc = new[]
                {
                    R("orbit", "Orbitar", "Orbit", "Arrastra el fondo|Botón derecho", "Drag the background|Right button"),
                    R("pan", "Desplazar", "Pan", "Shift + clic derecho|Botón central", "Shift + right drag|Middle button"),
                    R("focus", "Acercar al cursor", "Zoom to cursor", "Rueda del ratón", "Mouse wheel"),
                },
                Mouse = new[]
                {
                    R("orbit", "Orbitar", "Orbit", "Botón derecho + arrastrar", "Right button + drag"),
                    R("pan", "Desplazar", "Pan", "Botón central + arrastrar", "Middle button + drag"),
                    R("focus", "Acercar", "Zoom", "Rueda del ratón", "Mouse wheel"),
                },
                Touch = new[]
                {
                    R("orbit", "Orbitar", "Orbit", "Un dedo", "One finger"),
                    R("pan", "Desplazar", "Pan", "Dos dedos", "Two fingers"),
                    R("focus", "Acercar", "Zoom", "Pellizco", "Pinch"),
                },
            },
            new Step
            {
                Icon = "pins", TitleEs = "Seleccionar", TitleEn = "Select",
                DescEs = "Elige una pieza para ver su ficha.", DescEn = "Pick a part to see its sheet.",
                Pc = new[]
                {
                    R("pins", "Seleccionar", "Select", "Clic en la pieza", "Click the part"),
                    R("focus", "Enfocar", "Focus", "Doble clic|F", "Double click|F"),
                    R("close", "Soltar", "Deselect", "Clic en el fondo|Esc", "Click the background|Esc"),
                },
                Mouse = new[]
                {
                    R("pins", "Seleccionar", "Select", "Clic en la pieza", "Click the part"),
                    R("isolate", "Aislar", "Isolate", "Doble clic en la pieza", "Double click the part"),
                    R("close", "Soltar", "Deselect", "Clic en el fondo", "Click the background"),
                },
                Touch = new[]
                {
                    R("pins", "Seleccionar", "Select", "Toca la pieza", "Tap the part"),
                    R("isolate", "Aislar", "Isolate", "Doble toque", "Double tap"),
                    R("close", "Soltar", "Deselect", "Toca el fondo", "Tap the background"),
                },
            },
            new Step
            {
                Icon = "inspect", TitleEs = "Modos y aislar", TitleEn = "Modes and isolate",
                DescEs = "Tres modos y aislar la pieza elegida.", DescEn = "Three modes, and isolate the chosen part.",
                Pc = new[]
                {
                    R("inspect", "Inspeccionar", "Inspect", "1"),
                    R("analyze", "Analizar", "Analyze", "2"),
                    R("studio", "Estudio", "Studio", "3"),
                    R("isolate", "Aislar", "Isolate", "I"),
                },
                Mouse = new[]
                {
                    R("inspect", "Inspeccionar", "Inspect", "Cápsula inferior", "Bottom bar"),
                    R("analyze", "Analizar", "Analyze", "Cápsula inferior", "Bottom bar"),
                    R("studio", "Estudio", "Studio", "Cápsula inferior", "Bottom bar"),
                    R("isolate", "Aislar", "Isolate", "Botón Aislar", "Isolate button"),
                },
                Touch = new[]
                {
                    R("inspect", "Inspeccionar", "Inspect", "Cápsula inferior", "Bottom bar"),
                    R("analyze", "Analizar", "Analyze", "Cápsula inferior", "Bottom bar"),
                    R("studio", "Estudio", "Studio", "Cápsula inferior", "Bottom bar"),
                    R("isolate", "Aislar", "Isolate", "Botón Aislar", "Isolate button"),
                },
            },
            new Step
            {
                Icon = "analyze", TitleEs = "Analizar", TitleEn = "Analyze",
                DescEs = "Corta, explota o filtra el modelo.", DescEn = "Cut, explode or filter the model.",
                Pc = new[]
                {
                    R("cut", "Corte", "Cut", "C"),
                    R("explode", "Explotar", "Explode", "E"),
                    R("filter", "Filtrar", "Filter", "G"),
                },
                Mouse = new[]
                {
                    R("cut", "Corte", "Cut", "Tarjeta Corte", "Cut card"),
                    R("explode", "Explotar", "Explode", "Tarjeta Explotar", "Explode card"),
                    R("filter", "Filtrar", "Filter", "Tarjeta Filtrar", "Filter card"),
                },
                Touch = new[]
                {
                    R("cut", "Corte", "Cut", "Tarjeta Corte", "Cut card"),
                    R("explode", "Explotar", "Explode", "Tarjeta Explotar", "Explode card"),
                    R("filter", "Filtrar", "Filter", "Tarjeta Filtrar", "Filter card"),
                },
            },
            new Step
            {
                Icon = "studio", TitleEs = "Estudio", TitleEn = "Studio",
                DescEs = "Cambia el render y el entorno.", DescEn = "Change the render and the environment.",
                Pc = new[]
                {
                    R("xray", "Render", "Render", "V cicla los modos", "V cycles modes"),
                    R("env_time", "Entorno", "Environment", "Estudio|Tiempo|Color", "Studio|Time|Color"),
                    R("thermal", "Térmico", "Thermal", "Leyenda en la barra", "Legend in the bar"),
                },
                Mouse = new[]
                {
                    R("xray", "Render", "Render", "Tarjetas de modo", "Mode cards"),
                    R("env_time", "Entorno", "Environment", "Estudio|Tiempo|Color", "Studio|Time|Color"),
                    R("thermal", "Térmico", "Thermal", "Leyenda a la derecha", "Legend on the right"),
                },
                Touch = new[]
                {
                    R("xray", "Render", "Render", "Tarjetas de modo", "Mode cards"),
                    R("env_time", "Entorno", "Environment", "Estudio|Tiempo|Color", "Studio|Time|Color"),
                    R("thermal", "Térmico", "Thermal", "Leyenda a la derecha", "Legend on the right"),
                },
            },
            new Step
            {
                Icon = "help", TitleEs = "Listo", TitleEn = "Ready",
                DescEs = "Todo está a mano. Puedes repetir esto desde Ayuda.", DescEn = "Everything is at hand. You can repeat this from Help.",
                Pc = new[]
                {
                    R("parts", "Árbol de piezas", "Parts tree", "T|Ctrl+F"),
                    R("settings", "Ajustes", "Settings", "Barra superior", "Top bar"),
                    R("help", "Atajos", "Shortcuts", "?|F1"),
                },
                Mouse = new[]
                {
                    R("home", "Inicio", "Home", "Botón superior izquierdo", "Top left button"),
                    R("reset", "Restablecer vista", "Reset view", "Botón superior derecho", "Top right button"),
                    R("help", "Repetir el tutorial", "Repeat the tutorial", "Inicio › Acerca de", "Home › About"),
                },
                Touch = new[]
                {
                    R("home", "Inicio", "Home", "Botón superior izquierdo", "Top left button"),
                    R("reset", "Restablecer vista", "Reset view", "Botón superior derecho", "Top right button"),
                    R("help", "Repetir el tutorial", "Repeat the tutorial", "Inicio › Acerca de", "Home › About"),
                },
            },
        };

        private readonly VisualElement _overlay;
        private readonly Action _onClose;
        private readonly Label _counter;
        private readonly VisualElement[] _segs = new VisualElement[6];
        private readonly Button _skip;
        private readonly Button _pcBtn, _touchBtn;
        private readonly OutlineIcon _bigIcon;
        private readonly Label _title, _desc, _prevLabel, _nextLabel, _skipLabel;
        private readonly VisualElement _rows;
        private readonly Button _prev, _next;
        private readonly Label _prevKey, _nextKey, _skipKey;
        private readonly bool _touchDevice;
        private int _step;
        private bool _touch;

        public bool IsOpen { get; private set; }
        public int CurrentStep => _step;
        public bool IsSheet => _overlay.ClassListContains("dk-tut--sheet");

        private static bool Es => AppLanguageManager.IsSpanish;

        /// <summary>Hints for a mouse user: the PC build lists keyboard shortcuts, WebGL lists the on-screen buttons.</summary>
        private static bool StandaloneBuild
        {
            get
            {
#if UNITY_STANDALONE
                return true;
#else
                return false;
#endif
            }
        }

        public TutorialOverlay(VisualElement parent, VisualElement after, Action onClose, bool touchDevice)
        {
            _onClose = onClose;
            _touchDevice = touchDevice;
            _touch = touchDevice;

            _overlay = new VisualElement { name = "DkTutorial", pickingMode = PickingMode.Position };
            _overlay.AddToClassList("dk-tut");
            _overlay.AddToClassList("dk-tut--hidden");
            _overlay.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            _overlay.RegisterCallback<WheelEvent>(evt => evt.StopPropagation());
            _overlay.RegisterCallback<GeometryChangedEvent>(_ => UpdateLayoutVariant());

            var card = new VisualElement { name = "DkTutorialCard" };
            card.AddToClassList("dk-tut-card");
            _overlay.Add(card);

            // top: counter, progress, mouse / touch switch, skip
            var top = new VisualElement();
            top.AddToClassList("dk-tut-top");
            _counter = new Label { pickingMode = PickingMode.Ignore };
            _counter.AddToClassList("dk-tut-counter");
            top.Add(_counter);
            var progress = new VisualElement { pickingMode = PickingMode.Ignore };
            progress.AddToClassList("dk-tut-progress");
            for (int i = 0; i < _segs.Length; i++)
            {
                _segs[i] = new VisualElement { pickingMode = PickingMode.Ignore };
                _segs[i].AddToClassList("dk-tut-seg");
                progress.Add(_segs[i]);
            }

            top.Add(progress);

            var sw = new VisualElement { name = "DkTutorialSwitch" };
            sw.AddToClassList("dk-tut-switch");
            _pcBtn = new Button(() => SetTouch(false)) { name = "DkTutPc" };
            _touchBtn = new Button(() => SetTouch(true)) { name = "DkTutTouch" };
            _pcBtn.AddToClassList("dk-tut-switch-btn");
            _touchBtn.AddToClassList("dk-tut-switch-btn");
            sw.Add(_pcBtn);
            sw.Add(_touchBtn);
            top.Add(sw);

            _skip = new Button(() => _onClose?.Invoke()) { name = "DkTutSkip" };
            _skip.AddToClassList("dk-b");
            _skip.AddToClassList("dk-b--ghost");
            _skipLabel = new Label { pickingMode = PickingMode.Ignore };
            _skipLabel.AddToClassList("dk-b-text");
            _skip.Add(_skipLabel);
            _skipKey = MakeKbd("Esc");
            _skip.Add(_skipKey);
            top.Add(_skip);
            card.Add(top);

            // body: art + text
            var body = new VisualElement();
            body.AddToClassList("dk-tut-body");
            card.Add(body);

            var art = new VisualElement { pickingMode = PickingMode.Ignore };
            art.AddToClassList("dk-tut-art");
            var orbit = new VisualElement { pickingMode = PickingMode.Ignore };
            orbit.AddToClassList("dk-tut-orbit");
            art.Add(orbit);
            var ring = new VisualElement { pickingMode = PickingMode.Ignore };
            ring.AddToClassList("dk-tut-ring");
            _bigIcon = new OutlineIcon { Shape = "orbit" };
            _bigIcon.AddToClassList("dk-tut-bigicon");
            ring.Add(_bigIcon);
            art.Add(ring);
            body.Add(art);

            var text = new VisualElement { pickingMode = PickingMode.Ignore };
            text.AddToClassList("dk-tut-text");
            _title = new Label { pickingMode = PickingMode.Ignore };
            _title.AddToClassList("dk-tut-title");
            _desc = new Label { pickingMode = PickingMode.Ignore };
            _desc.AddToClassList("dk-tut-desc");
            _rows = new VisualElement { pickingMode = PickingMode.Ignore };
            _rows.AddToClassList("dk-tut-rows");
            text.Add(_title);
            text.Add(_desc);
            text.Add(_rows);
            body.Add(text);

            // foot
            var foot = new VisualElement();
            foot.AddToClassList("dk-tut-foot");
            _prev = new Button(Prev) { name = "DkTutPrev" };
            _prev.AddToClassList("dk-b");
            _prev.AddToClassList("dk-b--secondary");
            var backIcon = new OutlineIcon { Shape = "back" };
            backIcon.style.width = 18;
            backIcon.style.height = 18;
            _prev.Add(backIcon);
            _prevLabel = new Label { pickingMode = PickingMode.Ignore };
            _prevLabel.AddToClassList("dk-b-text");
            _prev.Add(_prevLabel);
            _prevKey = MakeKbd(StandaloneBuild ? "←" : "<");
            _prev.Add(_prevKey);
            foot.Add(_prev);
            var grow = new VisualElement { pickingMode = PickingMode.Ignore };
            grow.AddToClassList("dk-grow");
            foot.Add(grow);
            _next = new Button(Next) { name = "DkTutNext" };
            _next.AddToClassList("dk-b");
            _next.AddToClassList("dk-b--primary");
            _nextLabel = new Label { pickingMode = PickingMode.Ignore };
            _nextLabel.AddToClassList("dk-b-text");
            _next.Add(_nextLabel);
            _nextKey = MakeKbd(StandaloneBuild ? "→" : ">");
            _nextKey.AddToClassList("dk-kbd--on-primary");
            _next.Add(_nextKey);
            foot.Add(_next);
            card.Add(foot);

            if (after != null && after.parent == parent) parent.Insert(parent.IndexOf(after) + 1, _overlay);
            else parent.Add(_overlay);

            AppLanguageManager.LanguageChanged += OnLanguage;
            _overlay.schedule.Execute(Tick).Every(16);
            Render();
        }

        public void Dispose()
        {
            AppLanguageManager.LanguageChanged -= OnLanguage;
            _overlay.RemoveFromHierarchy();
        }

        public void Show()
        {
            _step = 0;
            _touch = _touchDevice;
            IsOpen = true;
            Debug.Log("[Tutorial] open");
            UpdateLayoutVariant();
            Render();
            _overlay.RemoveFromClassList("dk-tut--hidden");
            _overlay.BringToFront();
        }

        public void Hide()
        {
            if (IsOpen) Debug.Log("[Tutorial] closed at step " + (_step + 1));
            IsOpen = false;
            _overlay.AddToClassList("dk-tut--hidden");
        }

        /// <summary>Test hook: jump to a step (0-based).</summary>
        public void GoTo(int step)
        {
            _step = Mathf.Clamp(step, 0, Steps.Length - 1);
            Render();
        }

        private void UpdateLayoutVariant()
        {
            float w = _overlay.resolvedStyle.width;
            bool narrow = !float.IsNaN(w) && w > 0f && w < 820f;
            bool sheet = _touchDevice || narrow;
            _overlay.EnableInClassList("dk-tut--sheet", sheet);
        }

        private static Label MakeKbd(string key)
        {
            var l = new Label(key) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("dk-kbd");
            return l;
        }

        private void OnLanguage(string _) => Render();

        private void SetTouch(bool touch)
        {
            _touch = touch;
            Render();
        }

        private void Prev()
        {
            if (_step <= 0) return;
            _step--;
            Render();
        }

        private void Next()
        {
            if (_step < Steps.Length - 1)
            {
                _step++;
                Render();
            }
            else
            {
                _onClose?.Invoke();
            }
        }

        private void Tick()
        {
            if (!IsOpen) return;
            if (Input.GetKeyDown(KeyCode.RightArrow)) Next();
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) Prev();
        }

        private void Render()
        {
            Step s = Steps[_step];
            if (IsOpen) Debug.Log("[Tutorial] step " + (_step + 1) + "/" + Steps.Length + (_touch ? " touch" : " mouse") + (IsSheet ? " sheet" : " card"));
            _counter.text = (_step + 1).ToString("00") + " / " + Steps.Length.ToString("00");
            for (int i = 0; i < _segs.Length; i++)
            {
                _segs[i].EnableInClassList("dk-tut-seg--cur", i == _step);
                _segs[i].EnableInClassList("dk-tut-seg--done", i < _step);
            }

            _bigIcon.SetShape(s.Icon);
            _title.text = Es ? s.TitleEs : s.TitleEn;
            _desc.text = Es ? s.DescEs : s.DescEn;
            _skipLabel.text = Es ? "Omitir" : "Skip";
            _prevLabel.text = Es ? "Atrás" : "Back";
            _nextLabel.text = _step == Steps.Length - 1 ? (Es ? "Empezar" : "Start") : (Es ? "Siguiente" : "Next");
            _pcBtn.text = StandaloneBuild ? "PC" : (Es ? "Ratón" : "Mouse");
            _touchBtn.text = Es ? "Táctil" : "Touch";
            _pcBtn.EnableInClassList("dk-tut-switch-btn--on", !_touch);
            _touchBtn.EnableInClassList("dk-tut-switch-btn--on", _touch);
            _prev.SetEnabled(_step > 0);
            _overlay.EnableInClassList("dk-tut--touch", _touch);

            Row[] rows = _touch ? s.Touch : (StandaloneBuild ? s.Pc : s.Mouse);
            _rows.Clear();
            foreach (Row r in rows)
            {
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.AddToClassList("dk-tut-row");
                var bubble = new VisualElement { pickingMode = PickingMode.Ignore };
                bubble.AddToClassList("dk-tut-bubble");
                bubble.Add(new OutlineIcon { Shape = r.Icon });
                row.Add(bubble);
                var name = new Label(Es ? r.NameEs : r.NameEn) { pickingMode = PickingMode.Ignore };
                name.AddToClassList("dk-tut-rowname");
                row.Add(name);
                var keys = new VisualElement { pickingMode = PickingMode.Ignore };
                keys.AddToClassList("dk-tut-keys");
                foreach (string h in Es ? r.HintsEs : r.HintsEn)
                {
                    var cap = new Label(h) { pickingMode = PickingMode.Ignore };
                    cap.AddToClassList("dk-tut-cap");
                    keys.Add(cap);
                }

                row.Add(keys);
                _rows.Add(row);
            }
        }
    }
}
