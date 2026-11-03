#if UNITY_STANDALONE
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PC only (U2). Full-screen help card with the keyboard shortcuts and the mouse controls.
    /// Opened with ? or F1, closed with ? / F1 / Esc or a click. Pills + celeste, like the rest of the UI.
    /// </summary>
    internal sealed class DesktopHelpOverlay
    {
        private readonly VisualElement _root;
        private readonly VisualElement _overlay;
        private readonly VisualElement _card;

        public bool IsOpen { get; private set; }

        public DesktopHelpOverlay(VisualElement root)
        {
            _root = root;

            _overlay = new VisualElement { name = "DesktopHelpOverlay", pickingMode = PickingMode.Position };
            _overlay.AddToClassList("desktop-help");
            _overlay.AddToClassList("desktop-help--hidden");
            _overlay.RegisterCallback<PointerDownEvent>(evt =>
            {
                // Clicking anywhere closes it (and never reaches the 3D scene).
                Close();
                evt.StopPropagation();
            });

            _card = new VisualElement { pickingMode = PickingMode.Ignore };
            _card.AddToClassList("desktop-help-card");
            _overlay.Add(_card);
            root.Add(_overlay);

            AppLanguageManager.LanguageChanged += OnLanguageChanged;
            Rebuild();
        }

        public void Dispose()
        {
            AppLanguageManager.LanguageChanged -= OnLanguageChanged;
            _overlay.RemoveFromHierarchy();
        }

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        public void Open()
        {
            if (IsOpen) return;

            IsOpen = true;
            Rebuild();
            _overlay.RemoveFromClassList("desktop-help--hidden");
            _overlay.schedule.Execute(() => _overlay.AddToClassList("desktop-help--visible")).StartingIn(0);
            _overlay.BringToFront();
        }

        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            _overlay.RemoveFromClassList("desktop-help--visible");
            _overlay.AddToClassList("desktop-help--hidden");
        }

        private void OnLanguageChanged(string _)
        {
            Rebuild();
        }

        private static string T(string en, string es) => AppLanguageManager.IsSpanish ? es : en;

        private void Rebuild()
        {
            _card.Clear();

            var title = new Label(T("KEYBOARD SHORTCUTS", "ATAJOS DE TECLADO")) { pickingMode = PickingMode.Ignore };
            title.AddToClassList("desktop-help-title");
            _card.Add(title);

            var subtitle = new Label(T("TwinSight X500 · desktop viewer", "TwinSight X500 · visor de escritorio")) { pickingMode = PickingMode.Ignore };
            subtitle.AddToClassList("desktop-help-subtitle");
            _card.Add(subtitle);

            var columns = new VisualElement { pickingMode = PickingMode.Ignore };
            columns.AddToClassList("desktop-help-columns");
            _card.Add(columns);

            // ── Column 1: modes and tools ──
            VisualElement left = NewColumn(columns);
            left.AddToClassList("desktop-help-column--narrow");
            AddSection(left, T("MODES", "MODOS"));
            AddRow(left, new[] { "1" }, T("Inspect (pins, isolate, power)", "Inspeccionar (puntos, aislar, energía)"));
            AddRow(left, new[] { "2" }, T("Analyze (cut, explode, filter)", "Analizar (corte, explosión, filtro)"));
            AddRow(left, new[] { "3" }, T("Studio (render modes, light)", "Estudio (modos de render, luz)"));
            AddSection(left, T("TOOLS", "HERRAMIENTAS"));
            AddRow(left, new[] { "H" }, T("Show / hide pins", "Mostrar / ocultar puntos"));
            AddRow(left, new[] { "I" }, T("Isolate selected part", "Aislar la pieza seleccionada"));
            AddRow(left, new[] { "E" }, T("Explode / assemble", "Explosionar / ensamblar"));
            AddSection(left, T("WINDOW", "VENTANA"));
            AddRow(left, new[] { "F11", "Alt+Enter" }, T("Fullscreen", "Pantalla completa"));
            AddRow(left, new[] { "?", "F1" }, T("This help", "Esta ayuda"));

            // ── Column 2: view, window, mouse ──
            VisualElement right = NewColumn(columns);
            AddSection(right, T("VIEW", "VISTA"));
            AddRow(right, new[] { "F" }, T("Focus the selected part", "Enfocar la pieza seleccionada"));
            AddRow(right, new[] { "Home", "R" }, T("Reset the view", "Restablecer la vista"));
            AddRow(right, new[] { "Esc" }, T("Back / deselect", "Atrás / deseleccionar"));
            AddSection(right, T("MOUSE", "RATÓN"));
            AddRow(right, new[] { T("Click", "Clic") }, T("Select a part", "Seleccionar una pieza"));
            AddRow(right, new[] { T("Double click", "Doble clic") }, T("Select and focus", "Seleccionar y enfocar"));
            AddRow(right, new[] { T("Drag", "Arrastrar") }, T("Orbit (on the background)", "Orbitar (sobre el fondo)"));
            AddRow(right, new[] { T("Right drag", "Der. arrastrar") }, T("Orbit", "Orbitar"));
            AddRow(right, new[] { T("Middle drag", "Central arrastrar"), "Shift + " + T("Right drag", "Der.") }, T("Pan", "Desplazar"));
            AddRow(right, new[] { T("Wheel", "Rueda") }, T("Zoom toward the cursor", "Zoom hacia el cursor"));

            var footer = new VisualElement { pickingMode = PickingMode.Ignore };
            footer.AddToClassList("desktop-help-footer");
            footer.Add(MakeCap("?", true));
            var f1 = new Label(T("or", "o")) { pickingMode = PickingMode.Ignore };
            f1.AddToClassList("desktop-help-footer-text");
            footer.Add(f1);
            footer.Add(MakeCap("Esc", true));
            var f2 = new Label(T("or click anywhere to close", "o haz clic en cualquier sitio para cerrar")) { pickingMode = PickingMode.Ignore };
            f2.AddToClassList("desktop-help-footer-text");
            footer.Add(f2);
            _card.Add(footer);
        }

        private static VisualElement NewColumn(VisualElement parent)
        {
            var col = new VisualElement { pickingMode = PickingMode.Ignore };
            col.AddToClassList("desktop-help-column");
            parent.Add(col);
            return col;
        }

        private static void AddSection(VisualElement column, string text)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("desktop-help-section");
            column.Add(l);
        }

        private static void AddRow(VisualElement column, string[] keys, string description)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("desktop-help-row");

            var keysBox = new VisualElement { pickingMode = PickingMode.Ignore };
            keysBox.AddToClassList("desktop-help-keys");
            for (int i = 0; i < keys.Length; i++)
            {
                if (i > 0)
                {
                    var sep = new Label("/") { pickingMode = PickingMode.Ignore };
                    sep.AddToClassList("desktop-keycap-sep");
                    keysBox.Add(sep);
                }

                keysBox.Add(MakeCap(keys[i], i == 0));
            }

            row.Add(keysBox);

            var desc = new Label(description) { pickingMode = PickingMode.Ignore };
            desc.AddToClassList("desktop-help-desc");
            row.Add(desc);
            column.Add(row);
        }

        internal static Label MakeCap(string text, bool first)
        {
            var cap = new Label(text) { pickingMode = PickingMode.Ignore };
            cap.AddToClassList("desktop-keycap");
            if (first) cap.AddToClassList("desktop-keycap--first");
            return cap;
        }
    }
}
#endif
