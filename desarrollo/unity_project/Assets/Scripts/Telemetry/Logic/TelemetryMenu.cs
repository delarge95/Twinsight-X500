// UX de escritorio, paso 1 (PLAN_UX_ESCRITORIO / PLAN_PC 38): "oculta lo que no aplica, no lo atenues". UNA tabla por fuente decide que herramientas y que tarjetas
// existen; la interfaz solo pregunta. Equivalente de src/shared/surface.ts + src/shared/telemetrymenu.ts de la web (rama web/telemetria-control), adaptado a lo que
// el escritorio tiene de verdad. Logica pura (sin UnityEngine, sin interfaz); las pruebas estan en TelemetryMenuTests. Ningun rotulo de honestidad depende de esta tabla.
//
// Diferencias con la web (decididas con el usuario el 2026-11-20 o por la regla "la tabla dice lo que el escritorio hace"):
//  - «Vuelos» (flights) solo tiene «Mis sesiones» y el vuelo de muestra: el escritorio no lee .ulg ni compara vuelos.
//  - PX4 SITL tiene TRES secciones (datos, mando, herramientas): no hay tarjeta «PX4» (parametros, failsafe, calibracion) en el escritorio (sin Pixhawk/HITL).
//  - Escenario (course) solo en el simulador: en el escritorio el circuito solo corre en el simulador (la web tambien lo ofrece en SITL).
//  - Bateria (la reaccion a bateria baja) es del simulador; en la web va dentro de las fallas del simulador, aqui es un boton propio.
//  - Los cuatro comandos, Pilotar y las dos camaras (Seguir/Libre, Modo juego) son del mismo grupo «Mando»: existen donde la fuente acepta ordenes (simulador y SITL).
//  - «Grabar» (Sessions) abre «Mis sesiones»: existe en todas las fuentes (con el Inspector tambien: es como se abre una sesion propia); Record (el interruptor de
//    grabar dentro de ese popover) solo existe en el simulador y en PX4 SITL.
using System;
using System.Collections.Generic;

namespace WebGL.Telemetry
{
    /// <summary>Lo que la pestana de telemetria puede estar mostrando. <c>Own</c> es una sesion que esta app grabo (el cliente de repeticion la reproduce y no admite ordenes).</summary>
    public enum MenuSurface { Inspector, Replay, Own, Sim, Sitl }

    /// <summary>Las tres fuentes que se pueden elegir (una sesion propia cuenta como la repeticion).</summary>
    public enum PickSource { Replay, Sim, Sitl }

    /// <summary>Los controles de las pastillas de la pestana 4, en el orden en que se dibujan (pastilla de pilotaje, de entorno y de herramientas) mas el interruptor de grabar.</summary>
    public enum ToolId
    {
        // pastilla 2: «Mando»
        Pilot, Takeoff, Land, Hold, Rtl, Follow, Game,
        // pastilla 3: entorno
        Ground, Course, Fence, Wind, Battery,
        // pastilla 4: herramientas
        Map, Preflight, Mission, Sessions, Train,
        // fila 1 (PLAN_PC 42): pastillas pequenas que abren un panel lateral
        Alerts, Faults, Analysis,
        // dentro del popover de «Grabar»
        Record,
    }

    /// <summary>Las cuatro fuentes que ofrece la fila 1 (PLAN_PC 42): repeticion local, simulador, PX4 SITL y «Mis sesiones» (una sesion propia).</summary>
    public enum SourcePill { Replay, Sim, Sitl, Own }

    /// <summary>Las secciones plegables de la fila 1 (PLAN_PC 42): una abierta a la vez.</summary>
    public enum TSection { Cmd, Scene, Tools }

    /// <summary>Lo que dibujan las filas de arriba (ver <see cref="TelemetryMenu.ViewFor"/>).</summary>
    public sealed class StripView
    {
        public List<SourcePill> Pills;
        public List<TSection> Sections;
        public List<ToolId> Tools;
        public TSection? Open;
        public bool More;
    }

    /// <summary>Las secciones de la telemetria, en el orden en que se dibujan: ver (datos), actuar (mando / vuelos) y preparar (herramientas).</summary>
    public enum TGroup { Data, Cmd, Flights, Tools }

    /// <summary>Las tarjetas (paneles) de la telemetria del escritorio.</summary>
    public enum TCard { Switch, Sessions, Sample, Tools, Telemetry, Battery, Charts, Provenance, Alerts, Pilot, Commands, Faults, SimFaults, Explain }

    public static class TelemetryMenu
    {
        public static readonly MenuSurface[] Surfaces = { MenuSurface.Inspector, MenuSurface.Replay, MenuSurface.Own, MenuSurface.Sim, MenuSurface.Sitl };
        public static readonly ToolId[] ToolIds = (ToolId[])Enum.GetValues(typeof(ToolId));
        public static readonly TGroup[] TGroups = { TGroup.Data, TGroup.Cmd, TGroup.Flights, TGroup.Tools };
        public static readonly TCard[] TCards = (TCard[])Enum.GetValues(typeof(TCard));
        public static readonly PickSource[] PickSources = { PickSource.Replay, PickSource.Sim, PickSource.Sitl };

        private static readonly MenuSurface[] AllSurfaces = Surfaces;
        private static readonly MenuSurface[] Running = { MenuSurface.Replay, MenuSurface.Own, MenuSurface.Sim, MenuSurface.Sitl };
        private static readonly MenuSurface[] Commands = { MenuSurface.Sim, MenuSurface.Sitl };
        private static readonly MenuSurface[] SimOnly = { MenuSurface.Sim };
        private static readonly MenuSurface[] Flown = { MenuSurface.Replay, MenuSurface.Own };

        /// <summary>Donde existe cada control. Nada mas se dibuja: un boton que no aplica no se atenua, no esta.</summary>
        private static readonly Dictionary<ToolId, MenuSurface[]> ToolSurfaces = new Dictionary<ToolId, MenuSurface[]>
        {
            { ToolId.Pilot, Commands },
            { ToolId.Takeoff, Commands },
            { ToolId.Land, Commands },
            { ToolId.Hold, Commands },   // «Mantener» (H) y «Volver» (R) se quedan a la vista por seguridad donde hay mando
            { ToolId.Rtl, Commands },
            { ToolId.Follow, Commands }, // «Seguir / Libre»: la camara de persecucion solo actua pilotando
            { ToolId.Game, Commands },   // «Modo juego»
            { ToolId.Ground, Running },      // el suelo refleja el vuelo: con la telemetria apagada no hay nada que reflejar (PLAN_PC 42)
            { ToolId.Course, SimOnly },      // el circuito de pruebas simulado
            { ToolId.Fence, SimOnly },       // la geocerca editable es la del simulador
            { ToolId.Wind, SimOnly },        // viento simulado; en PX4 SITL el viento es de Gazebo
            { ToolId.Battery, SimOnly },     // la reaccion a la bateria baja del simulador
            { ToolId.Map, Running },         // necesita un vuelo que dibujar
            { ToolId.Preflight, Running },
            { ToolId.Mission, Commands },    // da ordenes
            { ToolId.Sessions, Commands },   // «Grabar»: solo donde se puede grabar; «Mis sesiones» es una fuente (SourcePill.Own), no esta herramienta
            { ToolId.Alerts, Running },      // la repeticion de muestra SI produce alertas del propio log (motor4:out, 4 de 9492 marcos; PLAN_PC 42)
            { ToolId.Faults, Commands },     // solo donde el dron reacciona de verdad: corte de motor del simulador, «Agotar bateria» de PX4 SITL
            { ToolId.Analysis, Running },
            { ToolId.Train, SimOnly },       // aprender a volar necesita el simulador
            { ToolId.Record, Commands },     // grabar necesita una fuente viva (su estado de enlace es un aviso dinamico, no parte de la tabla)
        };

        public static bool Has(ToolId id, MenuSurface s) => Array.IndexOf(ToolSurfaces[id], s) >= 0;

        // ───────────────────────── PLAN_PC 42: fuentes como pastillas, secciones plegables y modo compacto al pilotar ─────────────────────────

        /// <summary>Las cuatro pastillas de fuente de la fila 1. Con la telemetria apagada salen las cuatro; encendida, solo la activa (pulsarla la apaga).</summary>
        public static readonly SourcePill[] SourcePills = { SourcePill.Replay, SourcePill.Sim, SourcePill.Sitl, SourcePill.Own };

        public static SourcePill? PillOf(MenuSurface s)
        {
            switch (s)
            {
                case MenuSurface.Replay: return SourcePill.Replay;
                case MenuSurface.Own: return SourcePill.Own;
                case MenuSurface.Sim: return SourcePill.Sim;
                case MenuSurface.Sitl: return SourcePill.Sitl;
                default: return null;
            }
        }

        public static List<SourcePill> SourcePillsFor(MenuSurface s)
        {
            SourcePill? on = PillOf(s);
            return on.HasValue ? new List<SourcePill> { on.Value } : new List<SourcePill>(SourcePills);
        }

        /// <summary>Pulsar una pastilla de fuente: la activa apaga la telemetria (null); otra la enciende.</summary>
        public static SourcePill? PressPill(MenuSurface s, SourcePill p) => PillOf(s) == p ? (SourcePill?)null : p;

        /// <summary>La seccion plegable de cada herramienta de pastilla; null = va directa en la fila 1 (Alertas, Fallas, Analisis) o dentro de un panel (Grabar).</summary>
        public static TSection? SectionOf(ToolId id)
        {
            if (id <= ToolId.Game) return TSection.Cmd;
            if (id <= ToolId.Battery) return TSection.Scene;
            if (id <= ToolId.Train) return TSection.Tools;
            return null;
        }

        public static readonly TSection[] Sections = { TSection.Cmd, TSection.Scene, TSection.Tools };

        /// <summary>Las secciones con algo que mostrar en la fuente (ninguna con la telemetria apagada).</summary>
        public static List<TSection> SectionsFor(MenuSurface s)
        {
            var l = new List<TSection>();
            foreach (TSection sec in Sections)
                foreach (ToolId id in ToolIds)
                    if (SectionOf(id) == sec && Has(id, s)) { l.Add(sec); break; }
            return l;
        }

        /// <summary>La seccion que se abre al elegir la fuente: «Mando» donde hay ordenes; si no, ninguna (menos ruido).</summary>
        public static TSection? DefaultSection(MenuSurface s) => Has(ToolId.Pilot, s) ? (TSection?)TSection.Cmd : null;

        public static TSection? ToggleSection(TSection? cur, TSection next) => cur == next ? (TSection?)null : next;

        /// <summary>
        /// Lo que se ve en las filas de arriba. Fila 1: pastillas de fuente, secciones y Alertas / Fallas / Analisis. Fila 2: la seccion abierta.
        /// Pilotando (modo compacto): fila 1 = la fuente, Alertas (aviso dinamico) y «Mas»; fila 2 = «Mando» siempre (Mantener y Volver a la vista);
        /// «Mas» devuelve las secciones y, si se abre otra, sale como fila 3.
        /// </summary>
        public static StripView ViewFor(MenuSurface s, TSection? open, bool piloting, bool more)
        {
            var v = new StripView { Pills = SourcePillsFor(s), Sections = new List<TSection>(), Tools = new List<ToolId>() };
            if (s == MenuSurface.Inspector) return v;
            open = open.HasValue && SectionsFor(s).Contains(open.Value) ? open : null;
            bool full = !piloting || more;
            v.More = piloting;
            if (full) v.Sections = SectionsFor(s);
            foreach (ToolId id in ToolIds)
            {
                if (id == ToolId.Record || !Has(id, s)) continue;
                TSection? sec = SectionOf(id);
                bool show;
                if (sec == null) show = id == ToolId.Alerts || full;
                else if (piloting && sec == TSection.Cmd) show = true;
                else show = full && open == sec;
                if (show) v.Tools.Add(id);
            }
            v.Open = open;
            return v;
        }

        /// <summary>Los controles que existen en <paramref name="s"/>, en el orden de dibujo.</summary>
        public static List<ToolId> ToolsFor(MenuSurface s)
        {
            var l = new List<ToolId>();
            foreach (ToolId id in ToolIds) if (Has(id, s)) l.Add(id);
            return l;
        }

        /// <summary>Los controles que NO existen en <paramref name="s"/>.</summary>
        public static List<ToolId> ToolsMissing(MenuSurface s)
        {
            var l = new List<ToolId>();
            foreach (ToolId id in ToolIds) if (!Has(id, s)) l.Add(id);
            return l;
        }

        /// <summary>Los botones de las pastillas (sin el interruptor de grabar, que vive dentro de un popover).</summary>
        public static List<ToolId> StripToolsFor(MenuSurface s)
        {
            var l = ToolsFor(s);
            l.Remove(ToolId.Record);
            return l;
        }

        // ───────────────────────── fuente elegible ─────────────────────────

        public static PickSource? PickOf(MenuSurface s)
        {
            switch (s)
            {
                case MenuSurface.Inspector: return null;
                case MenuSurface.Own: return PickSource.Replay;
                case MenuSurface.Replay: return PickSource.Replay;
                case MenuSurface.Sim: return PickSource.Sim;
                default: return PickSource.Sitl;
            }
        }

        /// <summary>La tarjeta de fuente que se ve elegida: ninguna con la telemetria apagada (el Inspector).</summary>
        public static PickSource? ChosenSource(MenuSurface s) => PickOf(s);

        /// <summary>Las fuentes que alguien puede elegir para tener un control que aqui falta, nunca la actual (la linea «Disponible en: ...»).</summary>
        public static List<PickSource> AvailableIn(MenuSurface s)
        {
            PickSource? here = PickOf(s);
            var found = new HashSet<PickSource>();
            foreach (ToolId id in ToolsMissing(s))
                foreach (MenuSurface w in ToolSurfaces[id])
                {
                    PickSource? p = PickOf(w);
                    if (p.HasValue) found.Add(p.Value);
                }
            var l = new List<PickSource>();
            foreach (PickSource p in PickSources) if (found.Contains(p) && p != here) l.Add(p);
            return l;
        }

        // ───────────────────────── secciones y tarjetas ─────────────────────────

        private static readonly Dictionary<TGroup, MenuSurface[]> GroupSurfaces = new Dictionary<TGroup, MenuSurface[]>
        {
            { TGroup.Data, Running },
            { TGroup.Cmd, Commands },
            { TGroup.Flights, Flown },
            { TGroup.Tools, Running },
        };

        /// <summary>Las secciones de una fuente: tres en cada una (PX4 SITL tambien, sin la cuarta «PX4»), ninguna con la telemetria apagada.</summary>
        public static List<TGroup> GroupsFor(MenuSurface s)
        {
            var l = new List<TGroup>();
            foreach (TGroup g in TGroups) if (Array.IndexOf(GroupSurfaces[g], s) >= 0) l.Add(g);
            return l;
        }

        private struct Place
        {
            public TGroup[] Groups;
            public MenuSurface[] Only; // null = en toda fuente con telemetria
            public Place(MenuSurface[] only, params TGroup[] groups) { Groups = groups; Only = only; }
        }

        /// <summary>Cada tarjeta: las secciones que la muestran y, si no existe en todas las fuentes, donde existe.</summary>
        private static readonly Dictionary<TCard, Place> CardPlace = new Dictionary<TCard, Place>
        {
            { TCard.Switch, new Place(null, TGroup.Data) },                                  // la tarjeta de fuente y su enlace
            { TCard.Sessions, new Place(Flown, TGroup.Flights) },                            // «Mis sesiones»
            { TCard.Sample, new Place(Flown, TGroup.Flights) },                              // el vuelo de muestra
            { TCard.Tools, new Place(null, TGroup.Tools) },                                  // las pastillas de entorno y herramientas
            { TCard.Telemetry, new Place(null, TGroup.Data, TGroup.Cmd) },                   // las lecturas clave se quedan a la vista mientras se manda
            { TCard.Battery, new Place(null, TGroup.Data) },
            { TCard.Charts, new Place(null, TGroup.Data) },
            { TCard.Provenance, new Place(null, TGroup.Data) },
            { TCard.Alerts, new Place(null, TGroup.Data) },
            { TCard.Pilot, new Place(Commands, TGroup.Cmd) },
            { TCard.Commands, new Place(Commands, TGroup.Cmd) },
            { TCard.Faults, new Place(new[] { MenuSurface.Replay }, TGroup.Tools) },         // la inyeccion sintetica de fallas de la repeticion
            { TCard.SimFaults, new Place(SimOnly, TGroup.Cmd) },
            { TCard.Explain, new Place(Commands, TGroup.Cmd) },
        };

        public static IReadOnlyList<TGroup> GroupsOf(TCard c) => CardPlace[c].Groups;

        /// <summary>Las fuentes donde existe la tarjeta (con telemetria: el Inspector no tiene secciones).</summary>
        public static List<MenuSurface> SurfacesOf(TCard c)
        {
            var l = new List<MenuSurface>();
            foreach (MenuSurface s in Running) if (CardPlace[c].Only == null || Array.IndexOf(CardPlace[c].Only, s) >= 0) l.Add(s);
            return l;
        }

        /// <summary>Las tarjetas que una seccion muestra en una fuente.</summary>
        public static List<TCard> CardsOf(TGroup g, MenuSurface s)
        {
            var l = new List<TCard>();
            foreach (TCard c in TCards)
            {
                Place p = CardPlace[c];
                if (Array.IndexOf(p.Groups, g) >= 0 && (p.Only == null ? s != MenuSurface.Inspector : Array.IndexOf(p.Only, s) >= 0)) l.Add(c);
            }
            return l;
        }

        /// <summary>Pulsar una tarjeta de seccion: la abierta se cierra, otra se abre. Una seccion que la fuente no tiene se lee como ninguna.</summary>
        public static TGroup? Toggle(TGroup? cur, TGroup next) => cur == next ? (TGroup?)null : next;
        public static TGroup? GroupOn(TGroup? g, MenuSurface s) => g.HasValue && Array.IndexOf(GroupSurfaces[g.Value], s) >= 0 ? g : null;
    }
}
