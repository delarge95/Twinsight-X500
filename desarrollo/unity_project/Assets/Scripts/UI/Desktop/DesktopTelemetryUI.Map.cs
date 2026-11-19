#if UNITY_STANDALONE
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.Telemetry;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Lote 1b, tool 1 (PC only): the top-down map. A strip button «Mapa» (key M) shows or hides a panel over the lower left corner of the viewer (above the pad
    /// badge): a disc of 220 px (double click: 360 px) with the trail of the flight, the origin «H», the geofence ring (simulator only), the arrow of the heading,
    /// the scale bar and north; on top of it, when they are active, the simulated test course and the wind arrow of the simulator. Two orientations (north up,
    /// heading up), «Encajar» (fit: trail + geofence + course) or «Seguir» (follow the drone); wheel = zoom, drag = pan, Home over the map = fit.
    /// The maths is the pure <see cref="Minimap"/> (golden-tested); the drawing is <see cref="DesktopMiniMap"/>.
    /// Honesty: no base map and no geographic coordinates (the wire carries neither); the origin is «Origen local (EKF)» in the replay and PX4 SITL and «Punto
    /// de despegue» in the simulator; the footer says «Posición: medido» only for the replay of the real flight, «simulado» otherwise. Touching the map sends
    /// no command. Without telemetry (Inspector) the button is dimmed with its reason.
    /// </summary>
    internal sealed partial class DesktopTelemetryUI
    {
        public const string PrefMapOn = "Desk_MapOn";
        public const string PrefMapBig = "Desk_MapBig";
        public const string PrefMapHeadingUp = "Desk_MapHeadingUp";
        private const float MapSmall = 220f, MapLarge = 360f, MapMargin = 22f, MapPeriod = 0.1f;

        private enum MapMode { Fit, Follow, Free }

        private Button _btnMap;
        private Label _mapBtnLabel;
        private VisualElement _mapPanel, _mapDisc;
        private DesktopMiniMap _mapCanvas;
        private Label _mapTitle, _mapReadout, _mapCaption, _mapHonest, _mapH, _mapN, _mapScale, _mapWind, _mapWait;
        private Button _mapOrient, _mapFit, _mapFollow, _mapClose;
        private readonly MapTrail _mapTrail = new MapTrail();
        private readonly List<double> _mapPts = new List<double>(2500);
        private readonly List<double> _mapExtra = new List<double>(64);
        private CourseLayout _mapExtraOf;
        private bool _mapOn, _mapBig, _mapHeadingUp, _mapHover, _mapDrag, _mapDirty = true;
        private MapMode _mapMode = MapMode.Fit;
        private double _mapFollowScale;
        private MapView _mapFree;
        private TelemetryClient _mapClient;
        private HelloMessage _mapHello;
        private TelemetryFrame _mapFrame;
        private Vector2 _mapDragLast;
        private float _mapNext;
        private string _mapLangShown;

        // ── read by the verification harness ──
        public Button MapButton => _btnMap;
        public VisualElement MapPanel => _mapPanel;
        public DesktopMiniMap MapCanvas => _mapCanvas;
        public bool MapOn => _mapOn;
        public bool MapVisible => _mapPanel != null && _mapPanel.style.display != DisplayStyle.None;
        public bool MapBig => _mapBig;
        public bool MapHeadingUp => _mapHeadingUp;
        public bool MapFollowing => _mapMode == MapMode.Follow;
        public bool MapFitted => _mapMode == MapMode.Fit;
        public bool MapHovered => _mapHover && MapVisible;
        public MapTrail MapTrailNow => _mapTrail;
        public string MapReadout => _mapReadout?.text;
        public string MapCaption => _mapCaption?.text;
        public string MapScaleText => _mapScale?.text;
        public string MapWindText => _mapWind != null && _mapWind.style.display != DisplayStyle.None ? _mapWind.text : null;
        public string MapOriginLabel => _mapH?.tooltip;
        public Button MapOrientButton => _mapOrient;
        public Button MapFitButton => _mapFit;
        public Button MapFollowButton => _mapFollow;

        partial void BuildMapTool(VisualElement strip)
        {
            _mapOn = PlayerPrefs.GetInt(PrefMapOn, 0) != 0;
            _mapBig = PlayerPrefs.GetInt(PrefMapBig, 0) != 0;
            _mapHeadingUp = PlayerPrefs.GetInt(PrefMapHeadingUp, 0) != 0;

            _btnMap = StripButton("DkTelMap", "focus", out _mapBtnLabel, () => ToggleMap());
            _btnMap.Add(Kbd("M"));
            strip.Add(_btnMap);

            // the panel lives on the viewer (root), not in the strip: it stays while another mode is open as long as a session runs
            _mapPanel = new VisualElement { name = "DkMap", pickingMode = PickingMode.Position };
            _mapPanel.AddToClassList("dk-map");
            _mapPanel.style.display = DisplayStyle.None;

            var head = new VisualElement { pickingMode = PickingMode.Ignore };
            head.AddToClassList("dk-map-head");
            _mapTitle = Text("dk-map-title", head);
            _mapClose = _chrome.IconButtonPublic("close", "DkMapClose", () => SetMap(false), 14);
            _mapClose.AddToClassList("dk-ibtn--small");
            head.Add(_mapClose);
            _mapPanel.Add(head);
            _mapReadout = Text("dk-map-readout", _mapPanel);
            _mapReadout.name = "DkMapReadout";

            _mapDisc = new VisualElement { name = "DkMapDisc", pickingMode = PickingMode.Ignore };
            _mapDisc.AddToClassList("dk-map-disc");
            _mapCanvas = new DesktopMiniMap { name = "DkMapCanvas", Trail = _mapTrail };
            _mapCanvas.AddToClassList("dk-map-canvas");
            _mapDisc.Add(_mapCanvas);
            _mapH = MapLabel("DkMapH", "dk-map-lbl");
            _mapH.text = "H";
            _mapN = MapLabel("DkMapN", "dk-map-lbl");
            _mapN.AddToClassList("dk-map-lbl--north");
            _mapScale = MapLabel("DkMapScale", "dk-map-lbl");
            _mapWind = MapLabel("DkMapWind", "dk-map-lbl");
            _mapWait = MapLabel("DkMapWait", "dk-map-lbl");
            _mapWait.AddToClassList("dk-map-lbl--wait");
            _mapPanel.Add(_mapDisc);

            var ctl = new VisualElement { name = "DkMapControls" };
            ctl.AddToClassList("dk-map-ctl");
            _mapOrient = ToolButton("DkMapOrient", "dk-b--pill", () => SetMapHeadingUp(!_mapHeadingUp));
            _mapFit = ToolButton("DkMapFit", "dk-b--pill", () => MapFit());
            _mapFollow = ToolButton("DkMapFollow", "dk-b--pill", () => MapFollow(_mapMode != MapMode.Follow));
            foreach (Button b in new[] { _mapOrient, _mapFit, _mapFollow }) { b.AddToClassList("dk-map-btn"); ctl.Add(b); }
            _mapPanel.Add(ctl);

            _mapCaption = Text("dk-map-caption", _mapPanel);
            _mapCaption.name = "DkMapCaption";
            _mapHonest = Text("dk-map-honest", _mapPanel);
            _root.Add(_mapPanel);

            // wheel = zoom, drag = pan, double click = 220 / 360 px. None of it sends a command.
            _mapCanvas.RegisterCallback<WheelEvent>(e => { MapZoom(e.delta.y > 0f ? 1.2 : 1.0 / 1.2); e.StopPropagation(); });
            _mapCanvas.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                if (e.clickCount == 2) { SetMapBig(!_mapBig); e.StopPropagation(); return; }
                _mapDrag = true;
                _mapDragLast = e.position;
                _mapCanvas.CapturePointer(e.pointerId);
                InputManager.InputBlocked = true; // the 3D view stays still while the map is dragged
                e.StopPropagation();
            });
            _mapCanvas.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!_mapDrag) return;
                Vector2 d = (Vector2)e.position - _mapDragLast;
                _mapDragLast = e.position;
                if (d.sqrMagnitude > 0f) MapPan(d.x, d.y);
            });
            _mapCanvas.RegisterCallback<PointerUpEvent>(e => EndMapDrag(e.pointerId));
            _mapCanvas.RegisterCallback<PointerCaptureOutEvent>(_ => { if (_mapDrag) { _mapDrag = false; InputManager.InputBlocked = false; } });
            _mapPanel.RegisterCallback<PointerEnterEvent>(_ => _mapHover = true);
            _mapPanel.RegisterCallback<PointerLeaveEvent>(_ => _mapHover = false);
            ApplyMapSize();
        }

        private Label MapLabel(string name, string cls)
        {
            var l = new Label { name = name, pickingMode = PickingMode.Ignore };
            l.AddToClassList(cls);
            _mapDisc.Add(l);
            return l;
        }

        private void EndMapDrag(int pointerId)
        {
            if (!_mapDrag) return;
            _mapDrag = false;
            if (_mapCanvas.HasPointerCapture(pointerId)) _mapCanvas.ReleasePointer(pointerId);
            InputManager.InputBlocked = false;
        }

        partial void DisposeMap()
        {
            if (_mapDrag) InputManager.InputBlocked = false;
            _mapPanel?.RemoveFromHierarchy();
        }

        partial void RelabelMap()
        {
            if (_btnMap == null) return;
            _mapBtnLabel.text = T("tools.map");
            _mapTitle.text = T("map.title");
            _mapN.text = T("map.north");
            _mapWait.text = T("fl.waiting");
            _mapFit.text = T("map.fit");
            _mapFollow.text = T("map.follow");
            _mapOrient.tooltip = T("map.orient");
            _mapClose.tooltip = T("map.fold");
            _mapHonest.text = T("map.honest");
            _mapPanel.tooltip = string.Empty;
            _mapLangShown = null;
            _mapDirty = true;
        }

        // ───────────────────────── controls ─────────────────────────

        /// <summary>Key M and the strip button. Without telemetry (Inspector) it does nothing: the button is dimmed with its reason.</summary>
        public void ToggleMap()
        {
            if (!DesktopTelemetry.IsRunning) return;
            SetMap(!_mapOn);
        }

        public void SetMap(bool on)
        {
            _mapOn = on;
            PlayerPrefs.SetInt(PrefMapOn, on ? 1 : 0);
            _mapDirty = true;
            _mapNext = 0f;
        }

        public void SetMapBig(bool big)
        {
            _mapBig = big;
            PlayerPrefs.SetInt(PrefMapBig, big ? 1 : 0);
            ApplyMapSize();
        }

        private void ApplyMapSize()
        {
            float s = _mapBig ? MapLarge : MapSmall;
            _mapDisc.style.width = s;
            _mapDisc.style.height = s;
            _mapPanel.style.width = s + 18f;
            _mapPanel.EnableInClassList("dk-map--big", _mapBig);
            _mapDirty = true;
        }

        public void SetMapHeadingUp(bool headingUp)
        {
            _mapHeadingUp = headingUp;
            PlayerPrefs.SetInt(PrefMapHeadingUp, headingUp ? 1 : 0);
            _mapLangShown = null;
            _mapDirty = true;
        }

        /// <summary>«Encajar» and Home over the map: back to the fitted view.</summary>
        public void MapFit()
        {
            _mapMode = MapMode.Fit;
            _mapDirty = true;
        }

        /// <summary>«Seguir»: the drone at the centre, at the scale of the fitted view at the moment it is pressed (the map does not zoom while following).</summary>
        public void MapFollow(bool on)
        {
            if (on)
            {
                _mapFollowScale = FittedMapView().scale;
                _mapMode = MapMode.Follow;
            }
            else _mapMode = MapMode.Fit;
            _mapDirty = true;
        }

        /// <summary>Wheel: `factor` &gt; 1 zooms out (more metres per pixel). While following it changes the scale of the follow; otherwise the view becomes free.</summary>
        public void MapZoom(double factor)
        {
            MapView v = CurrentMapView();
            double scale = Math.Min(60.0, Math.Max(Minimap.MinScale, v.scale * factor));
            if (_mapMode == MapMode.Follow) _mapFollowScale = scale;
            else { _mapFree = new MapView(scale, v.centerN, v.centerE); _mapMode = MapMode.Free; }
            _mapDirty = true;
        }

        /// <summary>Drag: the map moves with the pointer by (dx, dy) px (y down). The view becomes free.</summary>
        public void MapPan(float dx, float dy)
        {
            MapView v = CurrentMapView();
            double yaw = _mapHeadingUp && _mapFrame != null ? Minimap.YawFromQuat(_mapFrame.q) : 0;
            // inverse of Minimap.ToScreen for a displacement: what world step moves a point by (dx, dy) on screen
            double c = Math.Cos(yaw), s = Math.Sin(yaw);
            double dn = _mapHeadingUp ? (-dx * s - dy * c) * v.scale : -dy * v.scale;
            double de = _mapHeadingUp ? (dx * c - dy * s) * v.scale : dx * v.scale;
            _mapFree = new MapView(v.scale, v.centerN - dn, v.centerE - de);
            _mapMode = MapMode.Free;
            _mapDirty = true;
        }

        // ───────────────────────── view ─────────────────────────

        private MapFence MapFenceNow()
        {
            // the ring is the simulator's own fence: PX4's (GF_*) does not reach the client, so nothing is drawn for SITL or the replay
            if (!DesktopTelemetry.IsSimulator) return null;
            FenceConfig f = FenceNow();
            return f != null ? new MapFence(f.radius) : null;
        }

        private CourseLayout MapCourseNow() => _course != null && _run != null ? _run.Layout : null;

        private void BuildMapExtra(CourseLayout L)
        {
            if (ReferenceEquals(L, _mapExtraOf)) return;
            _mapExtraOf = L;
            _mapExtra.Clear();
            if (L == null) return;
            void P(double n, double e) { _mapExtra.Add(n); _mapExtra.Add(e); }
            P(L.PadN, L.PadE);
            foreach (CourseGate g in L.Gates) P(g.N, g.E);
            foreach (CourseZone z in L.Zones) { P(z.N - z.HalfN, z.E - z.HalfE); P(z.N + z.HalfN, z.E + z.HalfE); }
            foreach (CourseBox b in L.Wall) { P(b.N0, b.E0); P(b.N1, b.E1); }
            foreach (CoursePylon py in L.Pylons) P(py.N, py.E);
        }

        /// <summary>
        /// The fitted view (the web's fitMapView): the origin, the trail, the drone and the points of the layers (the course). The geofence joins the fit unless
        /// it is much larger than the rest (then it would shrink a small course to a speck): it is left to run off the edge of the map instead.
        /// </summary>
        private MapView FittedMapView()
        {
            float size = _mapBig ? MapLarge : MapSmall;
            _mapPts.Clear();
            _mapPts.Add(0); _mapPts.Add(0);
            for (int i = 0; i < _mapTrail.Count; i++) { _mapPts.Add(_mapTrail.N(i)); _mapPts.Add(_mapTrail.E(i)); }
            if (_mapFrame != null) { _mapPts.Add(_mapFrame.p[0]); _mapPts.Add(_mapFrame.p[1]); }
            BuildMapExtra(MapCourseNow());
            _mapPts.AddRange(_mapExtra);
            MissionMapPoints(_mapPts); // Lote 1c: the route of the app's mission joins the fit
            MapFence fence = MapFenceNow();
            if (fence == null || _mapExtra.Count == 0) return Minimap.FitView(_mapPts, fence, size, MapMargin);
            MapView content = Minimap.FitView(_mapPts, null, size, MapMargin);
            double reach = content.scale * Math.Max(1, size / 2 - MapMargin);
            return Minimap.FitView(_mapPts, fence.radius > 1.5 * Math.Max(reach, 8) ? null : fence, size, MapMargin);
        }

        private MapView CurrentMapView()
        {
            switch (_mapMode)
            {
                case MapMode.Follow: return _mapFrame != null ? Minimap.FollowView(_mapFrame.p[0], _mapFrame.p[1], _mapFollowScale) : FittedMapView();
                case MapMode.Free: return _mapFree;
                default: return FittedMapView();
            }
        }

        // ───────────────────────── per frame ─────────────────────────

        partial void TickMap(bool viewer)
        {
            if (_btnMap == null) return;
            bool running = DesktopTelemetry.IsRunning;
            TelemetryClient client = DesktopTelemetry.Client;

            // the button: dimmed with its reason in the Inspector (no telemetry)
            _btnMap.SetEnabled(running);
            _btnMap.EnableInClassList("dk-strip-btn--on", running && _mapOn);
            string tip = running ? T("map.title") : T("tools.needTelemetry");
            if (_btnMap.tooltip != tip) _btnMap.tooltip = tip;

            // a new source or a new session (a new hello) starts a new trail and goes back to the fitted view
            if (!ReferenceEquals(client, _mapClient) || !ReferenceEquals(client?.Hello, _mapHello))
            {
                bool newClient = !ReferenceEquals(client, _mapClient);
                _mapClient = client;
                _mapHello = client?.Hello;
                _mapTrail.Clear();
                _mapFrame = null;
                if (newClient) _mapMode = MapMode.Fit;
                _mapDirty = true;
            }

            bool show = running && viewer && _mapOn;
            if ((_mapPanel.style.display != DisplayStyle.None) != show)
            {
                _mapPanel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (!show) { _mapHover = false; if (_mapDrag) { _mapDrag = false; InputManager.InputBlocked = false; } }
                _mapDirty = true;
            }

            // the trail is kept even while the map is hidden, so it is there when it opens (10 Hz; a point every half metre)
            if (!running || Time.unscaledTime < _mapNext) { if (!_mapDirty || !show) return; }
            else
            {
                _mapNext = Time.unscaledTime + MapPeriod;
                TelemetryFrame f = client.Displayed;
                if (f != null)
                {
                    bool moved = _mapTrail.Push(f.p, f.seg);
                    if (moved || !ReferenceEquals(f, _mapFrame)) _mapDirty = true;
                    _mapFrame = f;
                }
            }
            if (!show || !_mapDirty) return;
            _mapDirty = false;
            RefreshMap();
        }

        private void RefreshMap()
        {
            string lang = Lang;
            TelemetryFrame f = _mapFrame;
            DesktopMiniMap m = _mapCanvas;
            m.HasFrame = f != null;
            m.PosN = f != null ? f.p[0] : 0;
            m.PosE = f != null ? f.p[1] : 0;
            m.Yaw = f != null ? Minimap.YawFromQuat(f.q) : 0;
            m.HeadingUp = _mapHeadingUp;
            m.Fence = MapFenceNow();
            m.Course = MapCourseNow();
            m.CourseNext = m.Course != null && _run != null && _run.Reached < _run.Total ? _run.Reached : -1;
            MissionMapLayer(m); // Lote 1c: the route of the app's mission
            m.WindN = 0; m.WindE = 0;
            SimTelemetrySource sim = RunningSimulator;
            if (sim != null && sim.Model != null && sim.Model.armed && !sim.Model.landed)
            {
                FlightSim.WindNow(sim.Model, out double wn, out double we); // the wind only acts in flight
                m.WindN = wn; m.WindE = we;
            }
            m.Target = _trTarget;
            m.View = CurrentMapView();
            m.Compute();
            m.MarkDirtyRepaint();

            // text labels on the disc (centred on their point)
            PlaceMapLabel(_mapH, m.OriginPx + new Vector2(9f, 0f));
            PlaceMapLabel(_mapN, m.NorthPx);
            PlaceMapLabel(_mapScale, m.BarCenterPx + new Vector2(0f, -9f));
            SetText(_mapScale, Minimap.ScaleLabel(m.BarMetres));
            _mapWait.style.display = f == null ? DisplayStyle.Flex : DisplayStyle.None;
            PlaceMapLabel(_mapWait, new Vector2(m.Size / 2f, m.Size / 2f + 16f));
            _mapWind.style.display = m.WindDrawn ? DisplayStyle.Flex : DisplayStyle.None;
            if (m.WindDrawn)
            {
                double speed = Math.Sqrt(m.WindN * m.WindN + m.WindE * m.WindE);
                SetText(_mapWind, speed.ToString(speed < 10 ? "0.0" : "0", CultureInfo.InvariantCulture) + " m/s");
                PlaceMapLabel(_mapWind, m.WindCenterPx + new Vector2(0f, 21f));
            }

            // readout, controls and the honest footer
            if (f != null)
            {
                double dist = Math.Sqrt(f.p[0] * f.p[0] + f.p[1] * f.p[1]);
                double hdg = ((m.Yaw * 180.0 / Math.PI) % 360 + 360) % 360;
                string h = JsMath.ToFixed(hdg, 0);
                if (h == "360") h = "0";
                SetText(_mapReadout, TelemetryStrings.F("map.readout", lang, "dist", JsMath.ToFixed(dist, dist < 10 ? 1 : 0), "hdg", h));
            }
            else SetText(_mapReadout, T("fl.waiting"));

            _mapFit.EnableInClassList("dk-b--on", _mapMode == MapMode.Fit);
            _mapFollow.EnableInClassList("dk-b--on", _mapMode == MapMode.Follow);
            TelSource src = ActiveSource;
            // contract 1.2 (D-10): the replay hello says the position is an EKF2 estimate; without that field the replay keeps «medido» as before
            bool posEstimated = src == TelSource.LocalReplay && FlightOrigins.Pose(DesktopTelemetry.Client?.Hello, SourceClassification.Replay) == ReadingOrigin.Estimated;
            string key = lang + "|" + src + "|" + _mapHeadingUp + "|" + posEstimated;
            if (key != _mapLangShown)
            {
                _mapLangShown = key;
                _mapOrient.text = T(_mapHeadingUp ? "map.headingUp" : "map.northUp");
                // the origin is the take-off point only in the simulator; the position of the replay of the real flight is an EKF2 estimate (hello) or, with an old hello, measured
                string origin = T(src == TelSource.Simulator ? "map.originTakeoff" : "map.originLocal");
                string pos = T(src == TelSource.LocalReplay ? (posEstimated ? "map.posEstimated" : "map.posMeasured") : "map.posSimulated");
                _mapH.tooltip = origin;
                _mapCaption.text = "H · " + origin + " · " + pos;
                _mapCaption.tooltip = T("map.noOrders");
            }
        }

        private static void PlaceMapLabel(Label l, Vector2 centre)
        {
            // every label is a 64 x 14 box with centred text: placing the box places the text
            l.style.left = Mathf.Round(centre.x - 32f);
            l.style.top = Mathf.Round(centre.y - 7f);
        }
    }
}
#endif
