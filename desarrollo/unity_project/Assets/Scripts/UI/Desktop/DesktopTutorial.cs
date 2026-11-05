#if UNITY_STANDALONE
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PC only. The 6-step tutorial of the approved sheet (docs/unity_pc/maqueta_iconos): a centred card with a progress bar,
    /// one icon per step, three or four actions with their key caps, Atras / Siguiente / Omitir and the keys Left, Right and Esc
    /// (Esc is handled by UIManager through OnboardingController). It replaces the 15-step full-screen tutorial only in the
    /// Standalone build; the small PC / mobile switch keeps working (it swaps the key caps for touch gestures).
    /// </summary>
    internal sealed class DesktopTutorial
    {
        private struct Row
        {
            public string Icon;
            public string NameEs, NameEn;
            public string[] PcEs, PcEn, TouchEs, TouchEn;
        }

        private struct Step
        {
            public string Icon;
            public string TitleEs, TitleEn, DescEs, DescEn;
            public Row[] Rows;
        }

        private static Row R(string icon, string es, string en, string[] pcEs, string[] pcEn, string[] touchEs, string[] touchEn)
        {
            return new Row { Icon = icon, NameEs = es, NameEn = en, PcEs = pcEs, PcEn = pcEn ?? pcEs, TouchEs = touchEs, TouchEn = touchEn ?? touchEs };
        }

        private static string[] A(params string[] a) => a;

        private static readonly Step[] Steps =
        {
            new Step
            {
                Icon = "orbit", TitleEs = "Navegar", TitleEn = "Navigate",
                DescEs = "Mueve la cámara alrededor del dron.", DescEn = "Move the camera around the drone.",
                Rows = new[]
                {
                    R("orbit", "Orbitar", "Orbit", A("Arrastra el fondo", "Botón derecho"), A("Drag the background", "Right button"), A("Un dedo"), A("One finger")),
                    R("pan", "Desplazar", "Pan", A("Shift + clic derecho", "Botón central"), A("Shift + right drag", "Middle button"), A("Dos dedos"), A("Two fingers")),
                    R("focus", "Acercar al cursor", "Zoom to cursor", A("Rueda del ratón"), A("Mouse wheel"), A("Pellizco"), A("Pinch")),
                }
            },
            new Step
            {
                Icon = "pins", TitleEs = "Seleccionar", TitleEn = "Select",
                DescEs = "Elige una pieza para ver su ficha.", DescEn = "Pick a part to see its sheet.",
                Rows = new[]
                {
                    R("pins", "Seleccionar", "Select", A("Clic en la pieza"), A("Click the part"), A("Toca la pieza"), A("Tap the part")),
                    R("focus", "Enfocar", "Focus", A("Doble clic", "F"), A("Double click", "F"), A("Doble toque"), A("Double tap")),
                    R("close", "Soltar", "Deselect", A("Clic en el fondo", "Esc"), A("Click the background", "Esc"), A("Toca el fondo"), A("Tap the background")),
                }
            },
            new Step
            {
                Icon = "inspect", TitleEs = "Modos y aislar", TitleEn = "Modes and isolate",
                DescEs = "Tres modos con una tecla y aislar la pieza elegida.", DescEn = "Three modes on one key, and isolate the chosen part.",
                Rows = new[]
                {
                    R("inspect", "Inspeccionar", "Inspect", A("1"), null, A("Barra superior"), A("Top bar")),
                    R("analyze", "Analizar", "Analyze", A("2"), null, A("Barra superior"), A("Top bar")),
                    R("studio", "Estudio", "Studio", A("3"), null, A("Barra superior"), A("Top bar")),
                    R("isolate", "Aislar", "Isolate", A("I"), null, A("Botón Aislar"), A("Isolate button")),
                }
            },
            new Step
            {
                Icon = "analyze", TitleEs = "Analizar", TitleEn = "Analyze",
                DescEs = "Corta, explota o filtra el modelo.", DescEn = "Cut, explode or filter the model.",
                Rows = new[]
                {
                    R("cut", "Corte", "Cut", A("C"), null, A("Herramienta Corte"), A("Cut tool")),
                    R("explode", "Explotar", "Explode", A("E"), null, A("Herramienta Explotar"), A("Explode tool")),
                    R("filter", "Filtrar", "Filter", A("G"), null, A("Herramienta Filtrar"), A("Filter tool")),
                }
            },
            new Step
            {
                Icon = "studio", TitleEs = "Estudio", TitleEn = "Studio",
                DescEs = "Cambia el render y el entorno.", DescEn = "Change the render and the environment.",
                Rows = new[]
                {
                    R("xray", "Render", "Render", A("V cicla los modos"), A("V cycles modes"), A("Botón Render"), A("Render button")),
                    R("env_time", "Entorno", "Environment", A("Estudio", "Tiempo", "Color"), A("Studio", "Time", "Color"), A("Estudio", "Tiempo", "Color"), A("Studio", "Time", "Color")),
                    R("thermal", "Térmico", "Thermal", A("Leyenda en la barra"), A("Legend in the bar"), A("Leyenda en la barra"), A("Legend in the bar")),
                }
            },
            new Step
            {
                Icon = "help", TitleEs = "Listo", TitleEn = "Ready",
                DescEs = "Todo está a mano. Puedes repetir esto desde Ayuda.", DescEn = "Everything is at hand. You can repeat this from Help.",
                Rows = new[]
                {
                    R("parts", "Árbol de piezas", "Parts tree", A("T", "Ctrl+F"), null, A("Botón Piezas"), A("Parts button")),
                    R("settings", "Ajustes", "Settings", A("Barra superior"), A("Top bar"), A("Barra superior"), A("Top bar")),
                    R("help", "Atajos", "Shortcuts", A("?", "F1"), null, A("Barra superior"), A("Top bar")),
                }
            },
        };

        private readonly VisualElement _overlay;
        private readonly Action _onClose;
        private readonly Label _counter;
        private readonly VisualElement[] _segs = new VisualElement[6];
        private readonly Button _skip;
        private readonly Button _pcBtn, _touchBtn;
        private readonly DesktopVectorIcon _bigIcon;
        private readonly Label _title, _desc, _prevLabel, _nextLabel, _skipLabel;
        private readonly VisualElement _rows;
        private readonly Button _prev, _next;
        private readonly Label _prevKey, _nextKey;
        private int _step;
        private bool _touch;

        public bool IsOpen { get; private set; }
        public int CurrentStep => _step;

        private static bool Es => AppLanguageManager.IsSpanish;

        public DesktopTutorial(VisualElement parent, VisualElement after, Action onClose, bool startTouch)
        {
            _onClose = onClose;
            _touch = startTouch;

            _overlay = new VisualElement { name = "DkTutorial", pickingMode = PickingMode.Position };
            _overlay.AddToClassList("dk-tut");
            _overlay.AddToClassList("dk-tut--hidden");
            _overlay.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            _overlay.RegisterCallback<WheelEvent>(evt => evt.StopPropagation());

            var card = new VisualElement { name = "DkTutorialCard" };
            card.AddToClassList("dk-tut-card");
            _overlay.Add(card);

            // top: counter, progress, PC / mobile switch, skip
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
            _pcBtn = new Button(() => SetTouch(false)) { text = "PC", name = "DkTutPc" };
            _touchBtn = new Button(() => SetTouch(true)) { text = Es ? "Móvil" : "Mobile", name = "DkTutTouch" };
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
            _skip.Add(MakeKbd("Esc"));
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
            _bigIcon = new DesktopVectorIcon("orbit", 40);
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
            _prev.Add(new DesktopVectorIcon("back", 16));
            _prevLabel = new Label { pickingMode = PickingMode.Ignore };
            _prevLabel.AddToClassList("dk-b-text");
            _prev.Add(_prevLabel);
            _prevKey = MakeKbd("←");
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
            _nextKey = MakeKbd("→");
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
            IsOpen = true;
            Debug.Log("[DesktopTutorial] open");
            Render();
            _overlay.RemoveFromClassList("dk-tut--hidden");
            _overlay.BringToFront();
        }

        public void Hide()
        {
            if (IsOpen) Debug.Log("[DesktopTutorial] closed at step " + (_step + 1));
            IsOpen = false;
            _overlay.AddToClassList("dk-tut--hidden");
        }

        /// <summary>Test hook: jump to a step (0-based).</summary>
        public void GoTo(int step)
        {
            _step = Mathf.Clamp(step, 0, Steps.Length - 1);
            Render();
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
            if (IsOpen) Debug.Log("[DesktopTutorial] step " + (_step + 1) + "/" + Steps.Length + (_touch ? " touch" : " pc"));
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
            _touchBtn.text = Es ? "Móvil" : "Mobile";
            _pcBtn.EnableInClassList("dk-tut-switch-btn--on", !_touch);
            _touchBtn.EnableInClassList("dk-tut-switch-btn--on", _touch);
            _prev.SetEnabled(_step > 0);
            _prevKey.style.display = _touch ? DisplayStyle.None : DisplayStyle.Flex;
            _nextKey.style.display = _touch ? DisplayStyle.None : DisplayStyle.Flex;
            _skip.Q<Label>(className: "dk-kbd").style.display = _touch ? DisplayStyle.None : DisplayStyle.Flex;

            _rows.Clear();
            foreach (Row r in s.Rows)
            {
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.AddToClassList("dk-tut-row");
                var bubble = new VisualElement { pickingMode = PickingMode.Ignore };
                bubble.AddToClassList("dk-tut-bubble");
                bubble.Add(new DesktopVectorIcon(r.Icon, 20));
                row.Add(bubble);
                var name = new Label(Es ? r.NameEs : r.NameEn) { pickingMode = PickingMode.Ignore };
                name.AddToClassList("dk-tut-rowname");
                row.Add(name);
                var keys = new VisualElement { pickingMode = PickingMode.Ignore };
                keys.AddToClassList("dk-tut-keys");
                string[] hints = _touch ? (Es ? r.TouchEs : r.TouchEn) : (Es ? r.PcEs : r.PcEn);
                foreach (string h in hints)
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
#endif
