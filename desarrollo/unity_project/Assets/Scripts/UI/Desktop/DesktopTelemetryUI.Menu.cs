#if UNITY_STANDALONE
using System.Collections.Generic;
using UnityEngine.UIElements;
using WebGL.Telemetry;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// UX de escritorio, paso 2 (PLAN_PC 38): «oculta lo que no aplica, no lo atenues». La tabla por fuente (<see cref="TelemetryMenu"/>) decide que botones de las
    /// pastillas de la pestana 4 existen; aqui solo se aplica. Un boton que la fuente no tiene queda con display none (y sigue deshabilitado, no recibe el foco del
    /// mando ni el clic); un boton que SI existe pero hoy no puede actuar (p. ej. «Pilotar» con el SITL conectando) sigue atenuado: eso es estado, no fuente.
    /// </summary>
    internal sealed partial class DesktopTelemetryUI
    {
        private int _menuKey = -1;

        /// <summary>La fuente que esta mostrando la telemetria, en los terminos de la tabla.</summary>
        public MenuSurface CurrentSurface
        {
            get
            {
                switch (ActiveSource)
                {
                    case TelSource.LocalReplay: return MenuSurface.Replay;
                    case TelSource.OwnReplay: return MenuSurface.Own;
                    case TelSource.Simulator: return MenuSurface.Sim;
                    case TelSource.Sitl: return MenuSurface.Sitl;
                    default: return MenuSurface.Inspector;
                }
            }
        }

        /// <summary>El boton de la pastilla (y su control) que corresponde a un <see cref="ToolId"/>, o null.</summary>
        public Button StripButtonOf(ToolId id)
        {
            switch (id)
            {
                case ToolId.Pilot: return _btnPilot;
                case ToolId.Takeoff: return _btnCmd[0];
                case ToolId.Land: return _btnCmd[1];
                case ToolId.Hold: return _btnCmd[2];
                case ToolId.Rtl: return _btnCmd[3];
                case ToolId.Follow: return _btnCam;
                case ToolId.Game: return _btnGame;
                case ToolId.Ground: return _btnGround;
                case ToolId.Course: return _btnCourse;
                case ToolId.Fence: return _btnFence;
                case ToolId.Wind: return _btnWind;
                case ToolId.Battery: return _btnBattery;
                case ToolId.Map: return _btnMap;
                case ToolId.Preflight: return _btnPf;
                case ToolId.Mission: return _btnMission;
                case ToolId.Sessions: return _btnRecord;
                case ToolId.Train: return _btnTraining;
                case ToolId.Record: return _rcToggle;
                case ToolId.Alerts: return _btnAlerts;
                case ToolId.Faults: return _btnFaults;
                case ToolId.Analysis: return _btnAnalysis;
                default: return null;
            }
        }

        /// <summary>Verificacion: el control esta a la vista (existe en la fuente, su seccion esta abierta y su fila tampoco esta oculta).</summary>
        public bool ToolShown(ToolId id)
        {
            Button b = StripButtonOf(id);
            if (b == null) return false;
            for (VisualElement e = b; e != null && e != _container; e = e.parent)
                if (e.style.display == DisplayStyle.None) return false;
            return true;
        }

        /// <summary>La fila (pastilla) de cada seccion.</summary>
        private VisualElement RowOf(TSection s) => s == TSection.Cmd ? _pilotStrip : s == TSection.Scene ? _envStrip : _toolsStrip;

        private static void SetShown(VisualElement e, bool shown)
        {
            if (e == null) return;
            e.style.display = shown ? new StyleEnum<DisplayStyle>(StyleKeyword.Null) : new StyleEnum<DisplayStyle>(DisplayStyle.None);
        }

        private static readonly ToolId[] Drawn =
        {
            ToolId.Pilot, ToolId.Takeoff, ToolId.Land, ToolId.Hold, ToolId.Rtl, ToolId.Follow, ToolId.Game,
            ToolId.Ground, ToolId.Course, ToolId.Fence, ToolId.Wind, ToolId.Battery,
            ToolId.Map, ToolId.Preflight, ToolId.Mission, ToolId.Sessions, ToolId.Train,
            ToolId.Alerts, ToolId.Faults, ToolId.Analysis, ToolId.Record,
        };

        private VisualElement _availRow;
        private Label _availText;
        private Button _availInfo;

        public VisualElement AvailRow => _availRow;
        public string AvailText => _availText?.text;
        public string AvailInfoText => _availInfo?.tooltip;

        /// <summary>UX step 5: the line «Disponible en: ...» with its ⓘ (the web's tools.availableIn): where the hole left by the hidden controls can be filled.</summary>
        private void BuildAvailRow()
        {
            _availRow = new VisualElement { name = "DkAvailRow", pickingMode = PickingMode.Ignore };
            _availRow.AddToClassList("dk-avail");
            _availText = new Label { pickingMode = PickingMode.Ignore };
            _availText.AddToClassList("dk-avail-text");
            _availRow.Add(_availText);
            _availInfo = DesktopInfo.Create("DkInfoAvail");
            _availRow.Add(_availInfo);
            _availRow.style.display = DisplayStyle.None;
            _container.Add(_availRow);
        }

        private void RefreshAvailRow(MenuSurface s)
        {
            if (_availRow == null) return;
            List<PickSource> where = TelemetryMenu.AvailableIn(s);
            if (where.Count == 0) { _availRow.style.display = DisplayStyle.None; return; }
            var names = new List<string>();
            foreach (PickSource p in where) names.Add(T(p == PickSource.Replay ? "src.replay" : p == PickSource.Sim ? "src.sim" : "src.sitl"));
            _availText.text = TelemetryStrings.F("ux.availableIn", Lang, "sources", string.Join(", ", names));
            var tools = new List<string>();
            foreach (ToolId id in TelemetryMenu.ToolsMissing(s))
            {
                if (id == ToolId.Record) continue;
                string label = StripButtonOf(id)?.Q<Label>()?.text;
                if (!string.IsNullOrEmpty(label)) tools.Add(label);
            }
            DesktopInfo.SetText(_availInfo, string.Join(", ", tools));
            _availRow.style.display = DisplayStyle.Flex;
        }

        private bool _lastPiloting;

        /// <summary>
        /// Cada fotograma (solo hace algo cuando cambia la fuente, la seccion abierta, el pilotaje, «Mas», el circuito o el idioma): muestra u oculta las pastillas
        /// segun <see cref="TelemetryMenu.ViewFor"/> (PLAN_PC 42). Al cambiar de fuente se abre su seccion por defecto («Mando» donde hay ordenes) y se cierran los
        /// paneles que la nueva fuente no tiene.
        /// </summary>
        private void ApplyMenuTable()
        {
            if (_btnPilot == null) return;
            MenuSurface s = CurrentSurface;
            if (_lastSurface != s)
            {
                _lastSurface = s;
                _openSection = TelemetryMenu.DefaultSection(s);
                _moreOpen = false;
                string keep = _openPop;
                bool keepIt = keep == "source" || keep == "record" && (s == MenuSurface.Inspector || s == MenuSurface.Own || TelemetryMenu.Has(ToolId.Sessions, s))
                    || keep == "alerts" && TelemetryMenu.Has(ToolId.Alerts, s) || keep == "faults" && TelemetryMenu.Has(ToolId.Faults, s);
                if (keep != null && !keepIt) CloseAllPopovers();
            }
            if (_pilotOn != _lastPiloting) { _lastPiloting = _pilotOn; _moreOpen = false; }
            int key = (int)s * 4096 + (_openSection.HasValue ? 1 + (int)_openSection.Value : 0) * 256 + (_pilotOn ? 128 : 0) + (_moreOpen ? 64 : 0) + (_courseOn ? 1 : 0) + (Lang == "es" ? 0 : 2);
            if (key == _menuKey) return;
            _menuKey = key;
            StripView view = _view = TelemetryMenu.ViewFor(s, _openSection, _pilotOn, _moreOpen);
            var shown = new HashSet<ToolId>(view.Tools);
            foreach (ToolId id in Drawn)
            {
                bool exists = TelemetryMenu.Has(id, s);
                bool on = id == ToolId.Record ? exists : shown.Contains(id);
                // un circuito colocado se puede quitar aunque la fuente ya no sea el simulador (con su seccion abierta)
                if (id == ToolId.Course && _courseOn && s != MenuSurface.Inspector && (view.Open == TSection.Scene || shown.Contains(ToolId.Ground))) on = true;
                Button b = StripButtonOf(id);
                SetShown(b, on);
                // the buttons that have no enabled state of their own: one the source lacks is also disabled (the others do it in their Tick)
                if (b != null && (id >= ToolId.Takeoff && id <= ToolId.Game || id >= ToolId.Alerts && id <= ToolId.Analysis || id == ToolId.Ground || id == ToolId.Sessions)) b.SetEnabled(exists);
            }
            // una fila por seccion: se ve si tiene alguna pastilla a la vista
            foreach (TSection sec in TelemetryMenu.Sections)
            {
                bool any = false;
                foreach (ToolId id in view.Tools) if (TelemetryMenu.SectionOf(id) == sec) { any = true; break; }
                if (sec == TSection.Scene && _courseOn && view.Open == TSection.Scene) any = true;
                SetShown(RowOf(sec), any);
            }
            foreach (KeyValuePair<SourcePill, Button> kv in _srcPills) SetShown(kv.Value, view.Pills.Contains(kv.Key));
            SetShown(_srcGear, s != MenuSurface.Inspector);
            foreach (KeyValuePair<TSection, Button> kv in _secBtns)
            {
                SetShown(kv.Value, view.Sections.Contains(kv.Key));
                kv.Value.EnableInClassList("dk-strip-btn--on", view.Open == kv.Key);
                kv.Value.EnableInClassList("dk-secbtn--open", view.Open == kv.Key);
            }
            SetShown(_moreBtn, view.More);
            _moreBtn.EnableInClassList("dk-strip-btn--on", _moreOpen);
            _moreBtn.EnableInClassList("dk-secbtn--open", _moreOpen);
            SetShown(_sepData, s != MenuSurface.Inspector);
            SetShown(_sepSrc, s != MenuSurface.Inspector);
            if (s == MenuSurface.Inspector || _pilotOn) SetShown(_availRow, false); else RefreshAvailRow(s);
            SetShown(_camFold, shown.Contains(ToolId.Follow) || shown.Contains(ToolId.Game)); // the «Avanzado» fold of the commands (camera, game mode)
        }
    }
}
#endif
