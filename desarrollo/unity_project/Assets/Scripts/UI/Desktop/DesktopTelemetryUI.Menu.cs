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
                default: return null;
            }
        }

        /// <summary>Verificacion: el control esta a la vista (existe en la fuente y su pastilla tampoco esta oculta).</summary>
        public bool ToolShown(ToolId id)
        {
            Button b = StripButtonOf(id);
            if (b == null) return false;
            if (b.style.display == DisplayStyle.None) return false;
            VisualElement strip = id <= ToolId.Game ? _pilotStrip : null;
            return strip == null || strip.style.display != DisplayStyle.None;
        }

        private static void SetShown(VisualElement e, bool shown)
        {
            if (e == null) return;
            e.style.display = shown ? new StyleEnum<DisplayStyle>(StyleKeyword.Null) : new StyleEnum<DisplayStyle>(DisplayStyle.None);
        }

        private static readonly ToolId[] Drawn =
        {
            ToolId.Pilot, ToolId.Takeoff, ToolId.Land, ToolId.Hold, ToolId.Rtl, ToolId.Follow, ToolId.Game,
            ToolId.Ground, ToolId.Course, ToolId.Fence, ToolId.Wind, ToolId.Battery,
            ToolId.Map, ToolId.Preflight, ToolId.Mission, ToolId.Sessions, ToolId.Train, ToolId.Record,
        };

        /// <summary>Cada fotograma (solo hace algo cuando cambia la fuente o el circuito): muestra u oculta los controles segun la tabla.</summary>
        private void ApplyMenuTable()
        {
            if (_btnPilot == null) return;
            MenuSurface s = CurrentSurface;
            int key = (int)s * 4 + (_courseOn ? 1 : 0);
            if (key == _menuKey) return;
            _menuKey = key;
            var shown = new HashSet<ToolId>(TelemetryMenu.ToolsFor(s));
            foreach (ToolId id in Drawn)
            {
                bool on = shown.Contains(id);
                if (id == ToolId.Course && _courseOn) on = true; // un circuito colocado se puede quitar aunque la fuente ya no sea el simulador
                Button b = StripButtonOf(id);
                SetShown(b, on);
                // the buttons that have no enabled state of their own: a hidden one is also disabled (the others do it in their Tick)
                if (b != null && (id >= ToolId.Takeoff && id <= ToolId.Game)) b.SetEnabled(on);
            }
            // la pastilla de pilotaje entera: sin ordenes no queda nada que mostrar (ni separadores)
            SetShown(_pilotStrip, shown.Contains(ToolId.Pilot));
        }
    }
}
#endif
