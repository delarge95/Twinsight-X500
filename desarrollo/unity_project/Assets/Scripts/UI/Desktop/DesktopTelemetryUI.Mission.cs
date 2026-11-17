#if UNITY_STANDALONE
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.Telemetry;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Lote 1c, tool 2 (PC only): the mission planner. A mission of the APP, not a PX4 mission: nothing new travels on the wire; the runner (the pure
    /// <see cref="Mission"/>, golden-tested against the web) flies it with the commands that already exist (takeoff, fly_to, hold, land, rtl) and the speed of a
    /// leg comes from a destination that moves along it. Strip button «Misión» (Shift+M) and a popover: «Añadir en el suelo» (a click on the 3D ground adds a
    /// waypoint with the altitude and speed of the last one), the list (click = select; three sliders edit the selected one; Supr deletes it), the final action,
    /// the problems against the limits (the geofence of the simulator, or the clamps of the PX4 SITL bridge), export / import and Ejecutar / Pausar / Reanudar /
    /// Abortar / Volver (RTL). The route is drawn in the scene (<see cref="TelemetryMissionPath"/>) and on the map.
    /// The runner's clock is the vehicle's own time (frame.lt). Dead-man: losing the window focus, touching the sticks or sending a command by hand pauses it
    /// (hold). The document is remembered in PlayerPrefs `Desk_Mission` (the same JSON as the web's `twinsight.mission.v1`) and exported to
    /// persistentDataPath/twinsight/missions/. The replay takes no commands: the button explains it.
    /// </summary>
    internal sealed partial class DesktopTelemetryUI
    {
        public const string PrefMission = "Desk_Mission";
        private const int MsMaxFiles = 6;

        private sealed class MsRow
        {
            public Button Root, Delete;
            public Label Index, Text;
        }

        private Button _btnMission;
        private Label _msBtnLabel;
        private VisualElement _popMission, _msList, _msEdit, _msFinishRow, _msFilesBox, _msFoot;
        private Label _msTitle, _msOrigin, _msStatus, _msProgress, _msLocked, _msEmpty, _msFinishLabel, _msIssues, _msFileNote, _msSpeedNote, _msRunReason;
        private Button _msAdd, _msExport, _msImport, _msClear, _msGo, _msAbort, _msRtl;
        private readonly Button[] _msFinish = new Button[3];
        private static readonly string[] MsFinishIds = { "hold", "land", "rtl" };
        private static readonly string[] MsFinishKeys = { "mission.finishHold", "mission.finishLand", "mission.finishRtl" };
        private Slider _msAlt, _msSpeed, _msHold;
        private Label _msAltLabel, _msSpeedLabel, _msHoldLabel;
        private readonly List<MsRow> _msRows = new List<MsRow>();
        private readonly List<Button> _msFiles = new List<Button>();
        private MissionDoc _mission;
        private MissionRun _msRun;
        private AckMessage _msSeenAck;
        private TelemetryClient _msClient;
        private TelemetryMissionPath _msPath;
        private readonly List<double> _msMapPts = new List<double>(32);
        private double _msLastT;
        private int _msSelected = -1, _msPathSig = int.MinValue;
        private bool _msAdding, _msImportOpen, _msUiDirty = true;
        private string _msFileMessage;
        private MissionPhase _msPhaseShown = MissionPhase.Idle;
        private float _msNextUi;

        // ── read by the verification harness ──
        public Button MissionButton => _btnMission;
        public VisualElement MissionPopover => _popMission;
        public MissionDoc MissionNow => _mission;
        public MissionRun MissionRunNow => _msRun;
        public bool MissionAdding => _msAdding;
        public bool MissionBusy => _msRun != null && (Mission.IsActive(_msRun) || _msRun.phase == MissionPhase.Paused);
        public int MissionSelected => _msSelected;
        public TelemetryMissionPath MissionPath => _msPath;
        public string MissionStatusText => _msStatus?.text;
        public string MissionProgressText => _msProgress != null && _msProgress.style.display != DisplayStyle.None ? _msProgress.text : null;
        public string MissionIssuesText => _msIssues != null && _msIssues.style.display != DisplayStyle.None ? _msIssues.text : null;
        public string MissionRunReason => _msRunReason != null && _msRunReason.style.display != DisplayStyle.None ? _msRunReason.text : null;
        public string MissionOriginText => _msOrigin?.text;
        public string MissionFileMessage => _msFileMessage;
        public Button MissionRunButton => _msGo;
        public Button MissionAbortButton => _msAbort;
        public Button MissionAddButton => _msAdd;
        public int MissionRowsShown { get { int n = 0; foreach (MsRow r in _msRows) if (r.Root.style.display != DisplayStyle.None) n++; return n; } }
        public static string MissionFolder => Path.Combine(Application.persistentDataPath, "twinsight", "missions");

        partial void BuildMissionTool(VisualElement strip)
        {
            _mission = Mission.Parse(PlayerPrefs.GetString(PrefMission, string.Empty)) ?? new MissionDoc();
            _msRun = Mission.CreateRun(_mission);
            _btnMission = StripButton("DkTelMission", "pins", out _msBtnLabel, () => TogglePopover("mission"));
            _btnMission.Add(Kbd("Shift+M"));
            strip.Add(_btnMission);
        }

        partial void BuildMissionPopover()
        {
            _popMission = NewPopover("DkPopMission", "dk-pop--w360", out _msTitle);
            _msOrigin = Text("dk-note", _popMission);
            _msOrigin.AddToClassList("dk-note--muted");
            var st = new VisualElement { pickingMode = PickingMode.Ignore };
            st.AddToClassList("dk-ms-status");
            _msStatus = Text("dk-ms-phase", st);
            _msProgress = Text("dk-ms-progress", st);
            _popMission.Add(st);

            _msAdd = ToolButton("DkMsAdd", "dk-b--pill", () => SetMissionAdding(!_msAdding));
            _msAdd.AddToClassList("dk-ms-add");
            _popMission.Add(_msAdd);
            _msLocked = Text("dk-note", _popMission);
            _msLocked.AddToClassList("dk-note--muted");
            _msEmpty = Text("dk-note", _popMission);
            _msEmpty.AddToClassList("dk-note--muted");

            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "DkMsList" };
            scroll.AddToClassList("dk-ms-list");
            _msList = scroll;
            _popMission.Add(scroll);

            _msEdit = new VisualElement { name = "DkMsEdit" };
            _msAlt = ToolSlider(_msEdit, "DkMsAlt", 1f, 60f, v => EditMissionSelected(w => w.alt = Mathf.Round(v * 2f) / 2f), out _msAltLabel);
            _msSpeed = ToolSlider(_msEdit, "DkMsSpeed", (float)Mission.SpeedMin, (float)Mission.SpeedMax, v => EditMissionSelected(w => w.speed = Mathf.Round(v * 2f) / 2f), out _msSpeedLabel);
            _msHold = ToolSlider(_msEdit, "DkMsHold", 0f, (float)Mission.HoldMax, v => EditMissionSelected(w => { double h = Mathf.Round(v); w.holdS = h > 0 ? h : (double?)null; }), out _msHoldLabel);
            _popMission.Add(_msEdit);

            _msFinishRow = new VisualElement();
            _msFinishRow.AddToClassList("dk-ms-finish");
            _msFinishLabel = Text("dk-field-label", _msFinishRow);
            for (int i = 0; i < 3; i++)
            {
                string id = MsFinishIds[i];
                _msFinish[i] = ToolButton("DkMsFinish_" + id, "dk-b--pill", () => { if (!MissionBusy) { MissionDoc m = _mission.Clone(); m.finish = id; SetMission(m); } });
                _msFinish[i].AddToClassList("dk-ms-seg");
                _msFinishRow.Add(_msFinish[i]);
            }
            _popMission.Add(_msFinishRow);

            _msIssues = Text("dk-note", _popMission);
            _msIssues.AddToClassList("dk-note--warn");

            var files = new VisualElement();
            files.AddToClassList("dk-ms-files");
            _msExport = ToolButton("DkMsExport", "dk-b--ghost", () => MissionExport());
            _msImport = ToolButton("DkMsImport", "dk-b--ghost", () => { _msImportOpen = !_msImportOpen; _msFileMessage = null; _msUiDirty = true; });
            _msClear = ToolButton("DkMsClear", "dk-b--ghost", () => { if (!MissionBusy) { MissionDoc m = _mission.Clone(); m.waypoints.Clear(); SetMission(m); } });
            foreach (Button b in new[] { _msExport, _msImport, _msClear }) { b.AddToClassList("dk-fault-footbtn"); files.Add(b); }
            _popMission.Add(files);
            _msFilesBox = new VisualElement { name = "DkMsFiles" };
            _msFilesBox.AddToClassList("dk-ms-filelist");
            _popMission.Add(_msFilesBox);
            _msFileNote = Text("dk-note", _popMission);
            _msFileNote.AddToClassList("dk-note--muted");
            _msSpeedNote = Text("dk-note", _popMission);
            _msSpeedNote.AddToClassList("dk-note--muted");

            _msFoot = new VisualElement { name = "DkMsFoot" };
            _msFoot.AddToClassList("dk-ms-foot");
            _msGo = ToolButton("DkMsRun", "dk-b--primary", () =>
            {
                if (_msRun.phase == MissionPhase.Paused) MissionDo(MissionControl.Resume);
                else if (MissionBusy) MissionDo(MissionControl.Pause);
                else MissionDo(MissionControl.Start);
            });
            _msAbort = ToolButton("DkMsAbort", "dk-b--danger", () => MissionDo(MissionControl.Abort));
            _msRtl = ToolButton("DkMsRtl", "dk-b--ghost", () => MissionDo(MissionControl.AbortRtl));
            foreach (Button b in new[] { _msGo, _msAbort, _msRtl }) { b.AddToClassList("dk-fault-footbtn"); _msFoot.Add(b); }
            _popMission.Add(_msFoot);
            _msRunReason = Text("dk-note", _popMission);
            _msRunReason.AddToClassList("dk-note--muted");
            RegisterToolPopover("mission", _popMission);
        }

        partial void DisposeMission()
        {
            DestroyMissionPath();
            _popMission?.RemoveFromHierarchy();
        }

        private void DestroyMissionPath()
        {
            if (_msPath != null) { UnityEngine.Object.Destroy(_msPath.gameObject); _msPath = null; }
            _msPathSig = int.MinValue;
        }

        partial void RelabelMission()
        {
            if (_btnMission == null || _popMission == null) return;
            _msBtnLabel.text = T("tools.mission");
            _msTitle.text = T("mission.title");
            _msLocked.text = T("mission.editLocked");
            _msEmpty.text = T("mission.empty");
            _msFinishLabel.text = T("mission.finish");
            for (int i = 0; i < 3; i++) _msFinish[i].text = T(MsFinishKeys[i]);
            _msExport.text = T("mission.export");
            _msImport.text = T("mission.import");
            _msClear.text = T("mission.clear");
            _msSpeedNote.text = T("mission.speedNote");
            _msAbort.text = T("mission.abort");
            _msRtl.text = T("mission.rtl");
            _msUiDirty = true;
        }

        // ───────────────────────── the document ─────────────────────────

        private MissionLimits MissionLimitsNow()
        {
            if (!DesktopTelemetry.IsSimulator) return Mission.SitlLimits;
            FenceConfig f = FenceNow();
            return Mission.SimLimitsOf(f.radius, f.ceiling);
        }

        /// <summary>Replaces the document (not while the mission flies or is paused), remembers it and goes back to «Lista para ejecutar».</summary>
        public bool SetMission(MissionDoc m)
        {
            if (m == null || MissionBusy) return false;
            _mission = m;
            _msRun = Mission.CreateRun(m);
            if (_msSelected >= m.waypoints.Count) _msSelected = m.waypoints.Count - 1;
            PlayerPrefs.SetString(PrefMission, Mission.ToJson(m));
            _msUiDirty = true;
            _mapDirty = true;
            return true;
        }

        public void SetMissionAdding(bool adding)
        {
            _msAdding = adding && !MissionBusy;
            if (_msAdding && !TelemetryDroneDriver.ShowGroundReference) SetGround(true); // the click needs the ground
            _msUiDirty = true;
        }

        /// <summary>A ground point (local NED) while adding: a new waypoint with the altitude and speed of the last one, rounded to 0.1 m.</summary>
        public bool MissionAddPoint(double north, double east)
        {
            if (MissionBusy || _mission.waypoints.Count >= Mission.MaxWaypoints) return false;
            MissionDoc m = _mission.Clone();
            Waypoint last = m.waypoints.Count > 0 ? m.waypoints[m.waypoints.Count - 1] : null;
            m.waypoints.Add(new Waypoint(Math.Round(north * 10) / 10, Math.Round(east * 10) / 10, last != null ? last.alt : 5, last != null ? last.speed : 3));
            _msSelected = m.waypoints.Count - 1;
            return SetMission(m);
        }

        /// <summary>The ground click while «Añadir en el suelo» is on (called first by the ground-click hook). True = consumed.</summary>
        private bool MissionGroundClick(Vector2 screen)
        {
            if (!_msAdding) return false;
            var drv = TelemetryDroneDriver.Instance;
            if (drv == null || !drv.TryGroundPoint(screen, out double n, out double e, out Vector3 _)) return true; // adding: the click never flies the drone
            MissionAddPoint(n, e);
            return true;
        }

        public void MissionSelect(int k)
        {
            _msSelected = k >= 0 && k < _mission.waypoints.Count ? k : -1;
            _msUiDirty = true;
        }

        public bool MissionDeleteSelected()
        {
            if (MissionBusy || _msSelected < 0 || _msSelected >= _mission.waypoints.Count) return false;
            MissionDoc m = _mission.Clone();
            m.waypoints.RemoveAt(_msSelected);
            return SetMission(m);
        }

        private void EditMissionSelected(Action<Waypoint> edit)
        {
            if (MissionBusy || _msSelected < 0 || _msSelected >= _mission.waypoints.Count) return;
            MissionDoc m = _mission.Clone();
            edit(m.waypoints[_msSelected]);
            SetMission(m);
        }

        /// <summary>Writes the document to persistentDataPath/twinsight/missions/&lt;id&gt;.json (the same JSON the web exports). Returns the path, or null.</summary>
        public string MissionExport()
        {
            if (_mission.waypoints.Count == 0) return null;
            try
            {
                Directory.CreateDirectory(MissionFolder);
                string id = string.IsNullOrEmpty(_mission.id) ? "mision" : _mission.id;
                foreach (char c in Path.GetInvalidFileNameChars()) id = id.Replace(c, '_');
                string path = Path.Combine(MissionFolder, id + ".json");
                File.WriteAllText(path, Mission.ToJson(_mission) + "\n");
                _msFileMessage = TelemetryStrings.F("mission.d.exported", Lang, "path", path);
                _msUiDirty = true;
                return path;
            }
            catch (Exception ex)
            {
                _msFileMessage = TelemetryStrings.F("mission.d.exportFailed", Lang, "why", ex.Message);
                _msUiDirty = true;
                return null;
            }
        }

        /// <summary>Reads a mission file. Anything that is not a version-1 mission is refused with «El archivo no es una misión válida.».</summary>
        public bool MissionImport(string path)
        {
            if (MissionBusy) return false;
            MissionDoc m = null;
            try { m = Mission.Parse(File.ReadAllText(path)); } catch { m = null; }
            _msFileMessage = m == null ? T("mission.importBad") : null;
            _msUiDirty = true;
            if (m == null) return false;
            _msImportOpen = false;
            _msSelected = -1;
            return SetMission(m);
        }

        // ───────────────────────── the runner ─────────────────────────

        private double MissionClock()
        {
            TelemetryFrame f = DesktopTelemetry.Client?.Displayed;
            if (f != null && !double.IsNaN(f.lt) && !double.IsInfinity(f.lt)) _msLastT = f.lt;
            return _msLastT;
        }

        /// <summary>The buttons of the popover (and the dead-man). A start needs a link that takes commands and a mission without problems.</summary>
        public bool MissionDo(MissionControl action, string reason = Mission.ReasonUser)
        {
            TelemetryClient client = DesktopTelemetry.Client;
            if ((action == MissionControl.Start || action == MissionControl.Resume) && (client == null || !DesktopTelemetry.AcceptsCommands)) return false;
            if (action == MissionControl.Start && Mission.Validate(_mission, MissionLimitsNow()).Count > 0) return false;
            MissionPhase before = _msRun.phase;
            ApplyMission(Mission.Control(_msRun, action, MissionFrame.Of(client?.Displayed), MissionClock(), reason), client);
            if ((action == MissionControl.Start || action == MissionControl.Resume) && Mission.IsActive(_msRun))
            {
                _msAdding = false;
                _pilotAuto = true; // the mission flies: no neutral stick stream over it until the pilot touches a stick (which pauses it)
            }
            Debug.Log("[Mission] " + action + ": " + Mission.Name(before) + " -> " + Mission.Name(_msRun.phase) + (_msRun.reason != null ? " (" + _msRun.reason + ")" : string.Empty));
            return _msRun.phase != before || action == MissionControl.Start;
        }

        private void ApplyMission(MissionStepResult r, TelemetryClient client)
        {
            _msRun = r.run;
            if (client != null)
                foreach (CommandMessage c in r.send)
                {
                    bool ok = client.Send(c, out string why);
                    if (!ok) Debug.Log("[Mission] " + c.name + " not sent: " + why);
                }
            if (_msRun.phase != _msPhaseShown)
            {
                // the popover may be closed: a toast says what happened to the mission
                if (_msRun.phase == MissionPhase.Done) ShowToast(T("mission.phaseDone"), false);
                else if (_msRun.phase == MissionPhase.Paused || _msRun.phase == MissionPhase.Failed) ShowToast(MissionPhaseText(), true);
                _msPhaseShown = _msRun.phase;
                _msUiDirty = true;
                _mapDirty = true;
            }
        }

        /// <summary>A command sent by hand (button, key, pad, a click on the ground) while the mission flies: the mission pauses first.</summary>
        private void MissionOnManualCommand()
        {
            if (_msRun != null && Mission.IsActive(_msRun)) MissionDo(MissionControl.Pause);
        }

        private string MissionReasonText(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return string.Empty;
            if (reason == Mission.ReasonTimeout) return T("mission.d.reason.timeout");
            if (reason == Mission.ReasonLanded) return T("mission.d.reason.landed");
            if (reason == Mission.ReasonFocus) return T("mission.d.reason.focus");
            if (reason == Mission.ReasonUser) return T("mission.d.reason.user");
            return TelemetryStrings.AckDetail(reason, Lang);
        }

        private string MissionPhaseText()
        {
            switch (_msRun.phase)
            {
                case MissionPhase.Takeoff: return T("mission.phaseTakeoff");
                case MissionPhase.Leg: case MissionPhase.Finish: return T("mission.phaseLeg");
                case MissionPhase.Hold: return T("mission.phaseHold");
                case MissionPhase.Done: return T("mission.phaseDone");
                case MissionPhase.Paused: return TelemetryStrings.F("mission.phasePaused", Lang, "reason", MissionReasonText(_msRun.reason));
                case MissionPhase.Failed: return TelemetryStrings.F("mission.phaseFailed", Lang, "reason", MissionReasonText(_msRun.reason));
                default: return T("mission.phaseIdle");
            }
        }

        partial void TickMission(bool viewer)
        {
            if (_btnMission == null) return;
            TelemetryClient client = DesktopTelemetry.Client;
            bool running = client != null;

            // a new source: the old run is dropped (its link is gone), the document stays
            if (!ReferenceEquals(client, _msClient))
            {
                _msClient = client;
                _msSeenAck = client?.LastAck;
                _msRun = Mission.CreateRun(_mission);
                _msPhaseShown = MissionPhase.Idle;
                _msAdding = false;
                _msLastT = 0;
                _msUiDirty = true;
            }

            _btnMission.SetEnabled(running);
            _btnMission.EnableInClassList("dk-strip-btn--on", _openPop == "mission" || MissionBusy);
            string tip = !running ? T("tools.needTelemetry") : !DesktopTelemetry.AcceptsCommands && client.Classification == SourceClassification.Replay ? T("tools.missionReplay") : T("mission.title");
            if (_btnMission.tooltip != tip) _btnMission.tooltip = tip;

            if (running)
            {
                if (Mission.IsActive(_msRun))
                {
                    // dead-man: the window lost the focus, the link stopped taking commands, or the pilot took the sticks
                    if (!Application.isFocused && !IgnoreFocusLoss) MissionDo(MissionControl.Pause, Mission.ReasonFocus);
                    else if (!DesktopTelemetry.AcceptsCommands) MissionDo(MissionControl.Pause, Mission.ReasonUser);
                    else if (_pilotOn && !_pilotAuto) MissionDo(MissionControl.Pause, Mission.ReasonUser);
                }
                AckMessage ack = client.LastAck;
                if (ack != null && !ReferenceEquals(ack, _msSeenAck))
                {
                    _msSeenAck = ack;
                    ApplyMission(Mission.Ack(_msRun, ack.command, ack.ok, ack.HasDetail ? ack.detail : null, MissionClock()), client);
                }
                ApplyMission(Mission.Step(_msRun, MissionFrame.Of(client.Displayed), MissionClock()), client);
            }

            // Supr deletes the selected waypoint while the popover is open
            if (_openPop == "mission" && !DesktopKeyboardGate.Typing && DesktopKeyboardGate.Down(KeyCode.Delete)) MissionDeleteSelected();

            TickMissionPath(running && viewer);
        }

        private void TickMissionPath(bool show)
        {
            var drv = TelemetryDroneDriver.Instance;
            bool can = show && _mission.waypoints.Count > 0 && drv != null && drv.Engaged && drv.GroundRef != null && !drv.GroundRef.IsLegacy;
            if (!can) { if (_msPath != null) DestroyMissionPath(); return; }
            if (_msPath == null)
            {
                _msPath = TelemetryMissionPath.Create(drv.FlightOrigin, drv.GroundRef.GroundY, drv.UnitsPerMeter);
                if (_msPath == null) return;
                _msPathSig = int.MinValue;
            }
            int cur = MissionBusy ? _msRun.i : -1;
            int sig = cur * 31 + _mission.waypoints.Count;
            foreach (Waypoint w in _mission.waypoints) sig = unchecked(sig * 397 ^ w.n.GetHashCode() ^ (w.e.GetHashCode() * 3) ^ (w.alt.GetHashCode() * 7));
            if (sig == _msPathSig) return;
            _msPathSig = sig;
            _msPath.Set(_mission.waypoints, cur);
        }

        // ───────────────────────── the map layer ─────────────────────────

        /// <summary>The route on the map (called by the map tool): north, east pairs and the waypoint being flown to.</summary>
        private void MissionMapLayer(DesktopMiniMap m)
        {
            _msMapPts.Clear();
            if (_mission != null) foreach (Waypoint w in _mission.waypoints) { _msMapPts.Add(w.n); _msMapPts.Add(w.e); }
            m.MissionPts = _msMapPts;
            m.MissionCur = MissionBusy ? _msRun.i : -1;
        }

        private void MissionMapPoints(List<double> into)
        {
            if (_mission == null) return;
            foreach (Waypoint w in _mission.waypoints) { into.Add(w.n); into.Add(w.e); }
        }

        // ───────────────────────── the popover ─────────────────────────

        partial void TickMissionPopover()
        {
            if (!_msUiDirty && Time.unscaledTime < _msNextUi) return;
            _msNextUi = Time.unscaledTime + 0.25f;
            _msUiDirty = false;
            CultureInfo inv = CultureInfo.InvariantCulture;
            TelemetryClient client = DesktopTelemetry.Client;
            bool busy = MissionBusy;
            bool canRun = client != null && DesktopTelemetry.AcceptsCommands;
            bool sitl = client != null && !DesktopTelemetry.IsSimulator && client.Classification != SourceClassification.Replay;
            MissionLimits limits = MissionLimitsNow();
            List<MissionIssue> issues = Mission.Validate(_mission, limits);
            int count = _mission.waypoints.Count;

            SetText(_msOrigin, T("mission.honest") + " · " + T(sitl ? "mission.originSitl" : "mission.originSim"));
            SetText(_msStatus, MissionPhaseText());
            _msStatus.EnableInClassList("dk-ms-phase--warn", _msRun.phase == MissionPhase.Paused || _msRun.phase == MissionPhase.Failed);
            bool prog = count > 0 && _msRun.phase != MissionPhase.Idle;
            _msProgress.style.display = prog ? DisplayStyle.Flex : DisplayStyle.None;
            if (prog)
            {
                MissionProgress p = Mission.Progress(_msRun, MissionFrame.Of(client?.Displayed));
                SetText(_msProgress, TelemetryStrings.F("mission.progress", Lang, "leg", p.leg, "legs", p.legs, "m", Math.Round(p.metres).ToString("0", inv), "s", Math.Round(p.seconds).ToString("0", inv)));
            }

            SetText(_msAdd, T(_msAdding ? "mission.adding" : "mission.add"));
            _msAdd.EnableInClassList("dk-b--on", _msAdding);
            _msAdd.SetEnabled(!busy);
            _msLocked.style.display = busy ? DisplayStyle.Flex : DisplayStyle.None;
            _msEmpty.style.display = count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _msList.style.display = count == 0 ? DisplayStyle.None : DisplayStyle.Flex;

            while (_msRows.Count < count)
            {
                int k = _msRows.Count;
                var row = new MsRow();
                row.Root = new Button(() => MissionSelect(_msSelected == k ? -1 : k)) { name = "DkMsRow" + k };
                row.Root.AddToClassList("dk-ms-row");
                row.Index = Text("dk-ms-n", row.Root);
                row.Text = Text("dk-ms-rowtext", row.Root);
                row.Delete = new Button(() => { if (!MissionBusy) { _msSelected = k; MissionDeleteSelected(); } }) { name = "DkMsDel" + k, text = "×" };
                row.Delete.AddToClassList("dk-ms-del");
                row.Root.Add(row.Delete);
                _msList.Add(row.Root);
                _msRows.Add(row);
            }
            for (int k = 0; k < _msRows.Count; k++)
            {
                MsRow row = _msRows[k];
                row.Root.style.display = k < count ? DisplayStyle.Flex : DisplayStyle.None;
                if (k >= count) continue;
                Waypoint w = _mission.waypoints[k];
                SetText(row.Index, (k + 1).ToString(inv));
                SetText(row.Text, w.n.ToString("0.0", inv) + " N · " + w.e.ToString("0.0", inv) + " E · " + w.alt.ToString("0.#", inv) + " m · " + w.speed.ToString("0.#", inv) + " m/s" + (w.holdS.HasValue ? " · " + w.holdS.Value.ToString("0", inv) + " s" : string.Empty));
                bool bad = false;
                foreach (MissionIssue x in issues) if (x.index == k) bad = true;
                row.Root.EnableInClassList("dk-ms-row--sel", k == _msSelected);
                row.Root.EnableInClassList("dk-ms-row--cur", busy && k == _msRun.i);
                row.Root.EnableInClassList("dk-ms-row--bad", bad);
                row.Delete.tooltip = TelemetryStrings.F("mission.delete", Lang, "n", k + 1);
                row.Delete.SetEnabled(!busy);
            }

            bool edit = !busy && _msSelected >= 0 && _msSelected < count;
            _msEdit.style.display = edit ? DisplayStyle.Flex : DisplayStyle.None;
            if (edit)
            {
                Waypoint w = _mission.waypoints[_msSelected];
                _msAlt.lowValue = (float)limits.minAlt;
                _msAlt.highValue = (float)limits.maxAlt;
                _msAlt.SetValueWithoutNotify((float)w.alt);
                _msSpeed.SetValueWithoutNotify((float)w.speed);
                _msHold.SetValueWithoutNotify((float)(w.holdS ?? 0));
                string n = " · " + (_msSelected + 1).ToString(inv) + " · ";
                SetText(_msAltLabel, T("mission.alt") + n + w.alt.ToString("0.#", inv));
                SetText(_msSpeedLabel, T("mission.speed") + n + w.speed.ToString("0.#", inv));
                SetText(_msHoldLabel, T("mission.hold") + n + (w.holdS ?? 0).ToString("0", inv));
            }

            for (int i = 0; i < 3; i++)
            {
                _msFinish[i].EnableInClassList("dk-b--on", _mission.finish == MsFinishIds[i]);
                _msFinish[i].SetEnabled(!busy);
            }

            bool showIssues = issues.Count > 0 && count > 0;
            _msIssues.style.display = showIssues ? DisplayStyle.Flex : DisplayStyle.None;
            if (showIssues)
            {
                var lines = new List<string>(3);
                for (int k = 0; k < issues.Count && k < 3; k++) lines.Add(MissionIssueText(issues[k], limits));
                SetText(_msIssues, string.Join("\n", lines));
            }

            _msExport.SetEnabled(count > 0);
            _msImport.SetEnabled(!busy);
            _msImport.EnableInClassList("dk-b--on", _msImportOpen);
            _msClear.SetEnabled(!busy && count > 0);
            RefreshMissionFiles(busy);
            string note = _msFileMessage ?? (_msImportOpen ? TelemetryStrings.F("mission.d.folder", Lang, "path", MissionFolder) : null);
            _msFileNote.style.display = note != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (note != null) SetText(_msFileNote, note);

            string runReason = !canRun ? T(client != null && client.Classification == SourceClassification.Replay ? "tools.missionReplay" : "mission.needLink") : issues.Count > 0 ? MissionIssueText(issues[0], limits) : null;
            bool paused = _msRun.phase == MissionPhase.Paused;
            SetText(_msGo, T(paused ? "mission.resume" : busy ? "mission.pause" : "mission.run"));
            _msGo.SetEnabled(paused ? canRun : busy || runReason == null);
            _msAbort.SetEnabled(busy);
            _msRtl.style.display = busy ? DisplayStyle.Flex : DisplayStyle.None;
            bool showReason = runReason != null && !busy;
            _msRunReason.style.display = showReason ? DisplayStyle.Flex : DisplayStyle.None;
            if (showReason) SetText(_msRunReason, runReason);
        }

        private static void SetText(Button b, string text) { if (b.text != text) b.text = text; }

        private string MissionIssueText(MissionIssue x, MissionLimits limits)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            return TelemetryStrings.F("mission.err." + x.code, Lang, "n", (x.index ?? 0) + 1, "min", limits.minAlt.ToString("0.#", inv), "max", (x.code == "radius" ? limits.radius : limits.maxAlt).ToString("0.#", inv));
        }

        private void RefreshMissionFiles(bool busy)
        {
            bool show = _msImportOpen && !busy;
            _msFilesBox.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;
            string[] found = Array.Empty<string>();
            try { if (Directory.Exists(MissionFolder)) found = Directory.GetFiles(MissionFolder, "*.json"); } catch { /* the folder cannot be read: nothing to list */ }
            Array.Sort(found, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
            int n = Math.Min(MsMaxFiles, found.Length);
            while (_msFiles.Count < n)
            {
                var b = ToolButton("DkMsFile" + _msFiles.Count, "dk-b--ghost", null);
                b.AddToClassList("dk-ms-file");
                _msFilesBox.Add(b);
                _msFiles.Add(b);
            }
            for (int i = 0; i < _msFiles.Count; i++)
            {
                Button b = _msFiles[i];
                b.style.display = i < n ? DisplayStyle.Flex : DisplayStyle.None;
                if (i >= n) continue;
                string path = found[i];
                SetText(b, Path.GetFileName(path));
                b.clickable = new Clickable(() => MissionImport(path));
            }
            if (n == 0 && _msFileMessage == null) _msFileMessage = TelemetryStrings.F("mission.d.noFiles", Lang, "path", MissionFolder);
        }
    }
}
#endif
