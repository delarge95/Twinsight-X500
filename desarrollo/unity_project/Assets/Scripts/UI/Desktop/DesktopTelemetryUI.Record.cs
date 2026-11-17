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
    /// Lote 1c, tool 7 (PC only): record an own flight and play it back. A strip button «Grabar» (Shift+R toggles the recording; the button opens the popover) and a red
    /// chip in the top bar with the time while recording (click = stop). The last displayed frame is sampled every 50 ms (<see cref="TwfRecorder"/>, golden-tested
    /// against the web) and the file is a TWF1 (<see cref="TwfWriter"/>) saved in persistentDataPath/twinsight/sessions/. Only the simulator and PX4 SITL can be
    /// recorded (never a replay). The popover «Mis sesiones» lists the saved files (click = play) and opens a file by path; a recording plays as an OWN session
    /// (<see cref="OwnReplayTelemetrySource"/>): «Repetición · sesión propia (simulada / PX4 SITL)», every reading simulated, no commands, never «sombra digital».
    /// </summary>
    internal sealed partial class DesktopTelemetryUI
    {
        private const int RcMaxRows = 8;

        private Button _btnRecord;
        private Label _rcBtnLabel;
        private VisualElement _popRecord, _rcList;
        private Label _rcTitle, _rcState, _rcNote, _rcFolder, _rcMsg, _rcListTitle, _rcEmpty;
        private Button _rcToggle, _rcOpen;
        private TextField _rcPath;
        private Button _recChip;
        private Label _recChipText;
        private TwfRecorder _twr;
        private bool _twOn;
        private string _twKind, _twStarted;
        private RotorGeometry[] _twRotors;
        private int _twCells;
        private TelemetryClient _twClient;
        private float _rcMsgUntil, _rcNextList, _nextChipText;
        private string _rcListSig;
        private string _rcLastSaved;

        // ── read by the verification harness ──
        public Button RecordButton => _btnRecord;
        public Button RecordToggleButton => _rcToggle;
        public VisualElement RecordPopover => _popRecord;
        public Button RecordChip => _recChip ?? (_recChip = MakeRecordChip());
        public bool Recording => _twOn;
        public int RecordedFrames => _twr != null ? _twr.Count : 0;
        public string LastSavedSession => _rcLastSaved;
        public string RecordMessage => _rcMsg != null && _rcMsg.style.display != DisplayStyle.None ? _rcMsg.text : null;
        public TextField RecordPathField => _rcPath;
        public Button RecordOpenButton => _rcOpen;
        public VisualElement RecordList => _rcList;
        public static string SessionsFolder => Path.Combine(Application.persistentDataPath, "twinsight", "sessions");

        private Button MakeRecordChip()
        {
            Button chip = _chrome.MakeChipPublic("replay", "DkChipRecord", () => StopRecording());
            chip.AddToClassList("dk-chip--rec");
            _recChipText = new Label { pickingMode = PickingMode.Ignore };
            _recChipText.AddToClassList("dk-chip-text");
            chip.Add(_recChipText);
            return chip;
        }

        partial void BuildRecordTool(VisualElement strip)
        {
            _btnRecord = StripButton("DkTelRecord", "replay", out _rcBtnLabel, () => TogglePopover("record"));
            _btnRecord.Add(Kbd("Shift+R"));
            strip.Add(_btnRecord);

            _popRecord = NewPopover("DkPopRecord", "dk-pop--w360", out _rcTitle);
            _rcToggle = ToolButton("DkRecToggle", "dk-b--primary", () => ToggleRecording());
            _popRecord.Add(_rcToggle);
            _rcState = Text("dk-note", _popRecord);
            _rcNote = Text("dk-note", _popRecord);
            _rcNote.AddToClassList("dk-note--muted");
            _rcMsg = Text("dk-note", _popRecord);
            _rcMsg.AddToClassList("dk-note--warn");
            _rcMsg.style.display = DisplayStyle.None;

            _rcListTitle = Text("dk-fl-sec", _popRecord);
            _rcList = new VisualElement { name = "DkRecList" };
            _rcList.AddToClassList("dk-rec-list");
            _popRecord.Add(_rcList);
            _rcEmpty = Text("dk-note", _popRecord);
            _rcEmpty.AddToClassList("dk-note--muted");

            var line = new VisualElement();
            line.AddToClassList("dk-sitl-line");
            _rcPath = new TextField { name = "DkRecPath" };
            _rcPath.AddToClassList("dk-sitl-field");
            line.Add(_rcPath);
            _rcOpen = ToolButton("DkRecOpen", "dk-b--ghost", () => OpenSessionPath(_rcPath.value));
            line.Add(_rcOpen);
            _popRecord.Add(line);
            _rcFolder = Text("dk-note", _popRecord);
            _rcFolder.AddToClassList("dk-note--muted");
            RegisterToolPopover("record", _popRecord);
        }

        partial void DisposeRecord()
        {
            if (_twOn) StopRecording();
            _recChip?.RemoveFromHierarchy();
            _popRecord?.RemoveFromHierarchy();
        }

        partial void RelabelRecord()
        {
            if (_btnRecord == null) return;
            _rcBtnLabel.text = T("rec.button");
            _rcTitle.text = T("sessions.title");
            _rcListTitle.text = T("sessions.title");
            _rcEmpty.text = T("sessions.empty");
            _rcOpen.text = T("sessions.open");
            _rcPath.label = null;
            _rcFolder.text = T("sessions.folder");
            _rcNote.text = T("own.noFaults");
            _rcListSig = null;
            RefreshRecordState();
        }

        // ───────────────────────── recording ─────────────────────────

        /// <summary>Only the simulator and PX4 SITL can be recorded, with a link and a hello (the rotors and the cell count come from it).</summary>
        public bool CanRecord
        {
            get
            {
                if (!DesktopTelemetry.IsRunning) return false;
                TelemetryClient c = DesktopTelemetry.Client;
                if (c == null || c.Hello == null) return false;
                return DesktopTelemetry.IsSimulator || c.Classification == SourceClassification.Sitl;
            }
        }

        /// <summary>Shift+R: starts or stops the recording; when the source cannot be recorded it opens the popover that says why.</summary>
        public void ToggleRecording()
        {
            if (_twOn) { StopRecording(); return; }
            if (!CanRecord) { ShowRecordMessage(T(DesktopTelemetry.IsRunning ? "rec.need" : "rec.noLink")); if (_openPop != "record") TogglePopover("record"); return; }
            StartRecording();
        }

        public void StartRecording()
        {
            if (_twOn || !CanRecord) return;
            TelemetryClient c = DesktopTelemetry.Client;
            _twr = new TwfRecorder();
            _twKind = DesktopTelemetry.IsSimulator ? TwfWriter.KindSim : TwfWriter.KindSitl;
            _twRotors = c.Hello.rotors != null && c.Hello.rotors.Length == 4 ? c.Hello.rotors : QuadX.Px4QuadX;
            _twCells = c.Hello.batteryCells > 0 ? c.Hello.batteryCells : 4;
            _twStarted = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
            _twClient = c;
            _twOn = true;
            _nextChipText = 0f;
            HideRecordMessage();
            RefreshRecordState();
        }

        /// <summary>Stops and saves (a recording under 1 s is not saved). Returns the path of the file, or null.</summary>
        public string StopRecording(bool save = true)
        {
            if (!_twOn) return null;
            _twOn = false;
            string path = null;
            if (save)
            {
                if (_twr.Count < TwfWriter.RateHz) ShowRecordMessage(T("rec.tooShort"));
                else
                {
                    try
                    {
                        string version = Application.version ?? "0";
                        byte[] bytes = TwfWriter.Encode(new RecordedMeta(_twKind, "desktop", _twStarted, version), _twRotors, _twCells, _twr.Data, _twr.Count);
                        Directory.CreateDirectory(SessionsFolder);
                        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                        path = Path.Combine(SessionsFolder, "twinsight_" + stamp + "_" + _twKind + ".twf");
                        File.WriteAllBytes(path, bytes);
                        _rcLastSaved = path;
                        string msg = TelemetryStrings.F("rec.desktopSaved", Lang, "name", Path.GetFileName(path));
                        ShowToast(msg, false);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[Record] could not save: " + e.Message);
                        ShowRecordMessage(T("rec.saveError"));
                    }
                }
            }
            _twClient = null;
            _rcListSig = null;
            RefreshRecordState();
            return path;
        }

        private void ShowRecordMessage(string text)
        {
            if (_rcMsg == null) return;
            _rcMsg.text = text;
            _rcMsg.style.display = DisplayStyle.Flex;
            _rcMsgUntil = Time.unscaledTime + 8f;
        }

        private void HideRecordMessage()
        {
            if (_rcMsg != null) _rcMsg.style.display = DisplayStyle.None;
        }

        private static string FmtTime(double s)
        {
            int t = (int)Math.Max(0, Math.Floor(s));
            return (t / 60) + ":" + (t % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        private void RefreshRecordState()
        {
            if (_rcToggle == null) return;
            _rcToggle.text = _twOn ? T("rec.stop") : T("rec.button");
            _rcToggle.EnableInClassList("dk-b--rec", _twOn);
            _rcToggle.SetEnabled(_twOn || CanRecord);
            string state = _twOn ? T("rec.live") + " · " + FmtTime(_twr.Seconds) : (CanRecord ? T("rec.aria") : T(DesktopTelemetry.IsRunning ? "rec.need" : "rec.noLink"));
            if (_rcState.text != state) _rcState.text = state;
        }

        // ───────────────────────── sessions ─────────────────────────

        private static List<FileInfo> SessionFiles()
        {
            var l = new List<FileInfo>();
            try
            {
                if (Directory.Exists(SessionsFolder))
                    foreach (string p in Directory.GetFiles(SessionsFolder, "*.twf")) l.Add(new FileInfo(p));
            }
            catch (Exception e) { Debug.LogWarning("[Record] sessions folder: " + e.Message); }
            l.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            return l;
        }

        private void RefreshSessionList()
        {
            List<FileInfo> files = SessionFiles();
            string sig = Lang + "|" + files.Count;
            foreach (FileInfo f in files) sig += "|" + f.Name + f.Length;
            if (sig == _rcListSig) return;
            _rcListSig = sig;
            _rcList.Clear();
            _rcEmpty.style.display = files.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            for (int i = 0; i < files.Count && i < RcMaxRows; i++)
            {
                FileInfo f = files[i];
                string path = f.FullName;
                bool sitl = f.Name.EndsWith("_sitl.twf", StringComparison.OrdinalIgnoreCase);
                var b = new Button(() => OpenSessionPath(path)) { name = "DkRecRow" + i };
                b.AddToClassList("dk-b");
                b.AddToClassList("dk-b--pill");
                b.AddToClassList("dk-rec-row");
                b.text = Path.GetFileNameWithoutExtension(f.Name) + " · " + T(sitl ? "sessions.kindSitl" : "sessions.kindSim") + " · " + Math.Max(1, (int)(f.Length / 1024)) + " KB";
                b.tooltip = T("sessions.play");
                _rcList.Add(b);
            }
        }

        /// <summary>Plays a saved file as an own session. A bad file says why and changes nothing.</summary>
        public bool OpenSessionPath(string path)
        {
            path = (path ?? string.Empty).Trim().Trim('"');
            byte[] bytes;
            try { bytes = File.ReadAllBytes(path); }
            catch (Exception) { ShowRecordMessage(T("sessions.readError")); return false; }
            OwnReplayTelemetrySource src = OwnReplayTelemetrySource.FromBytes(bytes, Path.GetFileName(path), out TwfWriter.OwnFail fail);
            if (src == null)
            {
                ShowRecordMessage(T("sessions.bad." + (fail == TwfWriter.OwnFail.NotOwn ? "notOwn" : fail == TwfWriter.OwnFail.Empty ? "empty" : fail == TwfWriter.OwnFail.TooLong ? "tooLong" : "format")));
                return false;
            }
            if (_twOn) StopRecording();
            HideRecordMessage();
            DesktopTelemetry.Start(src);
            CloseAllPopovers();
            return true;
        }

        // ───────────────────────── per frame ─────────────────────────

        partial void TickRecord(bool viewer)
        {
            if (_btnRecord == null) return;
            if (_twOn)
            {
                TelemetryClient c = DesktopTelemetry.Client;
                if (!DesktopTelemetry.IsRunning || !ReferenceEquals(c, _twClient)) StopRecording(); // the session ended or changed: what was recorded is kept
                else
                {
                    TelemetryFrame f = c.Displayed;
                    if (f != null) _twr.Record(f, Time.realtimeSinceStartupAsDouble * 1000.0);
                    if (_twr.Full) { ShowRecordMessage(T("rec.max")); StopRecording(); }
                }
            }
            _btnRecord.EnableInClassList("dk-strip-btn--on", _twOn || _openPop == "record");
            _btnRecord.EnableInClassList("dk-strip-btn--rec", _twOn);
            string tip = _twOn ? T("rec.stopAria").Replace("{time}", FmtTime(_twr.Seconds)) : T("sc.tools.record");
            if (_btnRecord.tooltip != tip) _btnRecord.tooltip = tip;
            UpdateChipVisibility(RecordChip, _twOn);
            if (_twOn && Time.unscaledTime >= _nextChipText)
            {
                _nextChipText = Time.unscaledTime + 0.25f;
                string t = FmtTime(_twr.Seconds);
                if (_recChipText.text != t) _recChipText.text = t;
                _recChip.tooltip = T("rec.stopAria").Replace("{time}", t);
            }
            if (_rcMsg.style.display != DisplayStyle.None && Time.unscaledTime > _rcMsgUntil) HideRecordMessage();
        }

        partial void TickRecordPopover()
        {
            RefreshRecordState();
            if (Time.unscaledTime >= _rcNextList) { _rcNextList = Time.unscaledTime + 1f; RefreshSessionList(); }
        }
    }
}
#endif
