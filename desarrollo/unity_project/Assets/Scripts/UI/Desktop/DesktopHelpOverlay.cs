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
        /// <summary>0 = keyboard and mouse, 1 = game pad (PLAN_PC 27.2). The tab strip is clickable (and LB / RB switch it from the pad).</summary>
        public int Tab { get; private set; }

        /// <summary>E-6: true while piloting is ON (set by DesktopUIController). The overlay then shows the piloting mapping as ACTIVE and marks the desktop shortcuts it pauses.</summary>
        public System.Func<bool> PilotActive;

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
            Tab = 0;
            OpenInternal();
        }

        /// <summary>Opens (or closes, when it already shows the pad tab) the help on the game pad tab.</summary>
        public void TogglePad()
        {
            if (IsOpen && Tab == 1) { Close(); return; }
            OpenPadTab();
        }

        public void OpenPadTab()
        {
            Tab = 1;
            if (IsOpen) Rebuild(); else OpenInternal();
        }

        public void StepTab(int delta)
        {
            if (!IsOpen) return;
            Tab = ((Tab + delta) % 2 + 2) % 2;
            Rebuild();
        }

        private void OpenInternal()
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

            var head = new VisualElement { pickingMode = PickingMode.Ignore };
            head.AddToClassList("desktop-help-head");
            head.Add(new DesktopVectorIcon("help", 20));
            var title = new Label(T("KEYBOARD SHORTCUTS", "ATAJOS DE TECLADO")) { pickingMode = PickingMode.Ignore };
            title.AddToClassList("desktop-help-title");
            head.Add(title);
            var closeIcon = new VisualElement { pickingMode = PickingMode.Ignore };
            closeIcon.AddToClassList("desktop-help-close");
            closeIcon.Add(new DesktopVectorIcon("close", 16));
            head.Add(closeIcon);
            _card.Add(head);

            var subtitle = new Label(T("TwinSight X500 · desktop viewer", "TwinSight X500 · visor de escritorio")) { pickingMode = PickingMode.Ignore };
            subtitle.AddToClassList("desktop-help-subtitle");
            _card.Add(subtitle);
            _card.Add(BuildTabs());

            var columns = new VisualElement { pickingMode = PickingMode.Ignore };
            columns.AddToClassList("desktop-help-columns");
            _card.Add(columns);

            _card.AddToClassList("desktop-help-card--wide");
            if (Tab == 1)
            {
                BuildPadColumns(columns);
            }
            else
            {
            // ── Column 1: modes and tools ──
            VisualElement left = NewColumn(columns);
            left.AddToClassList("desktop-help-column--narrow");
            AddSection(left, T("MODES", "MODOS"));
            AddRow(left, new[] { "1" }, T("Inspect (pins, isolate, simulated power)", "Inspeccionar (puntos, aislar, energía simulada)"));
            AddRow(left, new[] { "2" }, T("Analyze (cut, explode, filter)", "Analizar (corte, explosión, filtro)"));
            AddRow(left, new[] { "3" }, T("Studio (render modes, light)", "Estudio (modos de render, luz)"));
            AddRow(left, new[] { "4" }, T("Telemetry (power, source, alerts, faults)", "Telemetría (encendido, fuente, alertas, fallas)"));
            AddSection(left, T("TOOLS", "HERRAMIENTAS"));
            AddRow(left, new[] { "H" }, T("Show / hide pins", "Mostrar / ocultar puntos"));
            AddRow(left, new[] { "I" }, T("Isolate selected part", "Aislar la pieza seleccionada"));
            AddRow(left, new[] { "E" }, T("Explode / assemble", "Explosionar / ensamblar"));
            AddRow(left, new[] { "C" }, T("Cross-section cut", "Corte transversal"));
            AddRow(left, new[] { "G" }, T("Filter by category", "Filtrar por categoría"));
            AddRow(left, new[] { "V" }, T("Cycle render modes", "Ciclar modos de render"));
            AddSection(left, T("TELEMETRY", "TELEMETRÍA"));
            AddRow(left, new[] { "L" }, T("Data source", "Fuente de datos"));
            AddRow(left, new[] { "Shift+A" }, T("Alerts", "Alertas"));
            AddRow(left, new[] { "Shift+G" }, T("Geofence (simulator)", "Geocerca (simulador)"));
            AddSection(left, T("WINDOW", "VENTANA"));
            AddRow(left, new[] { "F11", "Alt+Enter" }, T("Fullscreen", "Pantalla completa"));
            AddRow(left, new[] { "?", "F1" }, T("This help", "Esta ayuda"));

            // ── Column 2: view, window, mouse ──
            VisualElement right = NewColumn(columns);
            AddSection(right, T("VIEW", "VISTA"));
            AddRow(right, new[] { "F" }, T("Focus the selected part", "Enfocar la pieza seleccionada"));
            AddRow(right, new[] { "Home", "R" }, T("Reset the view", "Restablecer la vista"));
            AddRow(right, new[] { "Esc" }, T("Back / deselect", "Atrás / deseleccionar"));
            AddRow(right, new[] { "T", "P" }, T("Fold parts tree / inspector (also [ ])", "Plegar piezas / inspector (también [ ])"));
            AddRow(right, new[] { "Ctrl+F", "/" }, T("Search a part", "Buscar una pieza"));
            AddSection(right, T("MOUSE", "RATÓN"));
            AddRow(right, new[] { T("Click", "Clic") }, T("Select a part", "Seleccionar una pieza"));
            AddRow(right, new[] { T("Double click", "Doble clic") }, T("Select and focus", "Seleccionar y enfocar"));
            AddRow(right, new[] { T("Drag", "Arrastrar") }, T("Orbit (on the background)", "Orbitar (sobre el fondo)"));
            AddRow(right, new[] { T("Right drag", "Der. arrastrar") }, T("Orbit", "Orbitar"));
            AddRow(right, new[] { T("Middle drag", "Central arrastrar"), "Shift + " + T("Right drag", "Der.") }, T("Pan", "Desplazar"));
            AddRow(right, new[] { T("Wheel", "Rueda") }, T("Zoom toward the cursor", "Zoom hacia el cursor"));

            // E-6: the piloting mapping gets its own third column (the card widens): it is a temporary mapping, shown ACTIVE while piloting is on
            _card.AddToClassList("desktop-help-card--wide");
            VisualElement pilotColumn = NewColumn(columns);
            pilotColumn.AddToClassList("desktop-help-column--pilot");
            AddPilotRows(pilotColumn);
            }

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
            MarkPausedRows();
        }

        // ───────────────────────── tabs and the game pad tab (PLAN_PC 27.2) ─────────────────────────

        private VisualElement BuildTabs()
        {
            var strip = new VisualElement { name = "DesktopHelpTabs" };
            strip.AddToClassList("desktop-help-tabs");
            string[] names = { T("Keyboard and mouse", "Teclado y ratón"), T("Game pad", "Mando") };
            for (int i = 0; i < 2; i++)
            {
                int idx = i;
                var b = new Button(() => { Tab = idx; Rebuild(); }) { text = names[i], name = "DesktopHelpTab" + i };
                b.AddToClassList("desktop-help-tab");
                b.EnableInClassList("desktop-help-tab--on", Tab == i);
                b.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation()); // the overlay closes on a click anywhere: not on its own tabs
                strip.Add(b);
            }
            return strip;
        }

        private void BuildPadColumns(VisualElement columns)
        {
            VisualElement drone = NewColumn(columns);
            AddSection(drone, T("DRONE MODE (default)", "MODO DRON (predeterminado)"));
            AddPadRow(drone, new[] { "LS" }, T("Climb / descend (up-down) and turn (left-right)", "Subir / bajar (arriba-abajo) y girar (izq.-der.)"));
            AddPadRow(drone, new[] { "RS" }, T("Move: forward, back, left, right", "Mover: adelante, atrás, izquierda, derecha"));
            AddPadRow(drone, new[] { "A", "B", "X", "Y" }, T("Take off · land · hold · return", "Despegar · aterrizar · mantener · volver"));
            AddPadRow(drone, new[] { "Start" }, T("Switch to MENUS mode", "Cambiar al modo MENÚS"));
            AddNote(drone, T("The pad flies the drone only with «Pilot» on (flight simulator or PX4 SITL; a replay is read-only). In this mode the menus receive nothing from the pad.",
                             "El mando pilota solo con «Pilotar» activado (simulador de vuelo o PX4 SITL; la repetición es de solo lectura). En este modo los menús no reciben nada del mando."));

            VisualElement menus = NewColumn(columns);
            AddSection(menus, T("MENUS MODE (Start)", "MODO MENÚS (Start)"));
            AddPadRow(menus, new[] { "D-pad", "LS" }, T("Move the focus between controls", "Mover el foco entre controles"));
            AddPadRow(menus, new[] { "A" }, T("Activate the focused control", "Activar el control enfocado"));
            AddPadRow(menus, new[] { "B" }, T("Back / close (like Esc)", "Atrás / cerrar (como Esc)"));
            AddPadRow(menus, new[] { "LB", "RB" }, T("Previous / next mode (or help tab)", "Modo anterior / siguiente (o pestaña de ayuda)"));
            AddPadRow(menus, new[] { "RS" }, T("Scroll panels and the parts tree", "Desplazar paneles y el árbol de piezas"));
            AddPadRow(menus, new[] { "Back" }, T("This help", "Esta ayuda"));
            AddPadRow(menus, new[] { "Start" }, T("Back to DRONE mode", "Volver al modo DRON"));
            AddNote(menus, T("In this mode the drone gets no pad command: «hold» is sent when you enter it while piloting. The keyboard still works as always.",
                             "En este modo el dron no recibe órdenes del mando: al entrar pilotando se envía «mantener». El teclado sigue funcionando como siempre."));

            var foot = new Label(T("XInput (Xbox) pads only. The window needs the focus for the pad to act. The badge «Pad: Drone / Menus» shows the mode.",
                                   "Solo mandos XInput (Xbox). La ventana necesita el foco para que actúe el mando. La insignia «Mando: Dron / Menús» muestra el modo.")) { pickingMode = PickingMode.Ignore };
            foot.AddToClassList("desktop-help-desc");
            foot.AddToClassList("desktop-help-note");
            foot.AddToClassList("desktop-help-padfoot");
            columns.parent.Add(foot);
        }

        private static void AddPadRow(VisualElement column, string[] keys, string description)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("desktop-help-row--pilot");
            var caps = new VisualElement { pickingMode = PickingMode.Ignore };
            caps.AddToClassList("desktop-help-pilot-caps");
            for (int i = 0; i < keys.Length; i++) caps.Add(MakeCap(keys[i], i == 0));
            row.Add(caps);
            var desc = new Label(description) { pickingMode = PickingMode.Ignore };
            desc.AddToClassList("desktop-help-desc");
            desc.AddToClassList("desktop-help-pilot-desc");
            row.Add(desc);
            column.Add(row);
        }

        private static void AddNote(VisualElement column, string text)
        {
            var note = new Label(text) { pickingMode = PickingMode.Ignore };
            note.AddToClassList("desktop-help-desc");
            note.AddToClassList("desktop-help-note");
            column.Add(note);
        }

        // E-6: the piloting keys. They are active ONLY while piloting is on (Telemetry mode, "Pilot" toggle); then T L H R E and Shift+A stop being
        // desktop shortcuts. The rows are built here (not in the table above) because they are a temporary mapping, not a global shortcut.
        private static readonly string[] PausedKeys = { "H", "E", "L", "R", "T", "Shift+A" };

        private bool Piloting => PilotActive != null && PilotActive();

        private void AddPilotRows(VisualElement column)
        {
            AddSection(column, Piloting ? T("PILOTING · ON", "PILOTAJE · ACTIVO") : T("PILOTING (when on)", "PILOTAJE (al activarlo)"));
            AddPilotRow(column, new[] { "W", "A", "S", "D" }, T("Fly: forward, left, back, right (also arrows)", "Volar: avanzar, izquierda, atrás, derecha (también flechas)"));
            AddPilotRow(column, new[] { "Q", "E" }, T("Turn left / right", "Girar a izquierda / derecha"));
            AddPilotRow(column, new[] { "Space", "Shift" }, T("Climb / descend", "Subir / bajar"));
            AddPilotRow(column, new[] { "T", "L", "H", "R" }, T("Take off · land · hold · return", "Despegar · aterrizar · mantener · volver"));
            AddPilotRow(column, new[] { "Esc" }, T("Leave piloting (hold)", "Soltar el pilotaje (mantener)"));
            AddPilotRow(column, new[] { T("Click", "Clic") }, T("On the ground grid: fly there", "En la rejilla del suelo: volar allí"));
            var note = new Label(Piloting
                ? T("Paused while piloting: H E L R T and Shift+A. Esc gives them back.", "En pausa mientras pilotas: H E L R T y Shift+A. Esc los devuelve.")
                : T("While on, H E L R T and Shift+A are paused as shortcuts.", "Al activarlo, H E L R T y Shift+A dejan de ser atajos.")) { pickingMode = PickingMode.Ignore };
            note.AddToClassList("desktop-help-desc");
            note.AddToClassList("desktop-help-note");
            column.Add(note);
        }

        private void AddPilotRow(VisualElement column, string[] keys, string description)
        {
            // two lines (the column is narrow): the key caps, then what they do. No "/" separators.
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("desktop-help-row--pilot");
            var caps = new VisualElement { pickingMode = PickingMode.Ignore };
            caps.AddToClassList("desktop-help-pilot-caps");
            for (int i = 0; i < keys.Length; i++) caps.Add(MakeCap(keys[i], i == 0));
            row.Add(caps);
            var desc = new Label(description) { pickingMode = PickingMode.Ignore };
            desc.AddToClassList("desktop-help-desc");
            desc.AddToClassList("desktop-help-pilot-desc");
            row.Add(desc);
            column.Add(row);
        }

        /// <summary>While piloting, the desktop shortcuts that share a key with the pilot are shown as paused (dimmed, struck by the class).</summary>
        private void MarkPausedRows()
        {
            if (!Piloting) return;
            foreach (VisualElement row in _card.Query<VisualElement>(className: "desktop-help-row").ToList())
            {
                if (row.ClassListContains("desktop-help-row--pilot")) continue;
                var caps = row.Query<Label>(className: "desktop-keycap").ToList();
                bool pausedRow = false;
                foreach (Label cap in caps)
                {
                    if (System.Array.IndexOf(PausedKeys, cap.text) >= 0) { cap.AddToClassList("desktop-keycap--paused"); pausedRow = true; }
                }
                if (pausedRow) row.AddToClassList("desktop-help-row--paused");
            }
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
