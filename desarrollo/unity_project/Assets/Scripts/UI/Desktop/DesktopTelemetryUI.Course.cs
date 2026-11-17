#if UNITY_STANDALONE
using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.Telemetry;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PLAN_PC 28.3 (PC only): the «Escenario de prueba simulado» of the flight simulator. A toggle of the strip («Escenario», default OFF) places a
    /// low-poly course in the simulator's world (<see cref="TelemetryTestCourse"/>); <see cref="CourseRun"/> scores one lap from the drone position of
    /// every frame: ordered checkpoints, a lap timer and a collision counter (simple volume tests, the simulator has no physics). One small HUD pill
    /// (caption, time, checkpoints k/n, collisions, «Reiniciar», and the result message at the end). The course exists ONLY while the simulator runs:
    /// with another source (or none) it is destroyed and the toggle is dimmed. It does not touch the simulator and has no colliders.
    /// </summary>
    internal sealed partial class DesktopTelemetryUI
    {
        private Button _btnCourse;
        private Label _courseBtnLabel;
        private VisualElement _coursePill;
        private Label _courseCaption, _courseTime, _courseCp, _courseHits, _courseResult;
        private Button _courseReset;
        private bool _courseOn;
        private TelemetryTestCourse _course;
        private CourseRun _run;
        private float _courseHitUntil;
        private float _courseNextText;
        private string _courseLang;
        private CourseRecordSet _courseRecords;
        private string _courseRecordKey; // what the result line says about the records: "clean" | "pen" | "both" | null (computed once per lap, text per language)
        private double _courseRecordPen;
        public CourseRecordSet CourseRecordsNow { get { EnsureCourseRecords(); return _courseRecords; } }
        public static string CourseRecordsFile => System.IO.Path.Combine(Application.persistentDataPath, "twinsight", "records.json");

        private void EnsureCourseRecords()
        {
            if (_courseRecords != null) return;
            try { _courseRecords = CourseRecords.Parse(System.IO.File.Exists(CourseRecordsFile) ? System.IO.File.ReadAllText(CourseRecordsFile) : null); }
            catch (Exception e) { Debug.LogWarning("[TestCourse] records not read: " + e.Message); _courseRecords = CourseRecords.Empty(); }
        }

        /// <summary>A finished lap: scores it (time + 2 s per collision [APROBADO 2026-11-13]), updates the records of this course and keeps them (the same JSON as the web's).</summary>
        private void RecordCourseResult()
        {
            EnsureCourseRecords();
            CourseResult res = CourseRecords.Score(_run);
            double nowMs = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            CourseRecordUpdate u = CourseRecords.Update(_courseRecords, _run.Layout.Id, res, nowMs);
            _courseRecords = u.Records;
            _courseRecordPen = res.PenalisedS;
            _courseRecordKey = u.NewBestClean && u.NewBestPenalised ? "both" : u.NewBestClean ? "clean" : u.NewBestPenalised ? "pen" : "none";
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(CourseRecordsFile));
                System.IO.File.WriteAllText(CourseRecordsFile, CourseRecords.ToJson(_courseRecords));
            }
            catch (Exception e) { Debug.LogWarning("[TestCourse] records not saved: " + e.Message); }
        }

        /// <summary>The second line of the result: the time with the penalty and, when it is one, the record it beats.</summary>
        private string CourseRecordLine(string lang)
        {
            if (_courseRecordKey == null) return string.Empty;
            string s = TelemetryStrings.T("course.penalised", lang) + " " + FormatLap(_courseRecordPen);
            if (_courseRecordKey == "clean" || _courseRecordKey == "both") s += " · " + TelemetryStrings.T("course.newClean", lang);
            if (_courseRecordKey == "pen" || _courseRecordKey == "both") s += " · " + TelemetryStrings.T("course.newPenalised", lang);
            CourseRecord r = _courseRecords?.Of(_run.Layout.Id);
            if (r != null && r.BestClean.HasValue) s += "\n" + TelemetryStrings.T("course.bestClean", lang) + " " + FormatLap(r.BestClean.Value);
            if (r != null && r.BestPenalised.HasValue && _courseRecordKey != "pen" && _courseRecordKey != "both") s += "\n" + TelemetryStrings.T("course.bestPenalised", lang) + " " + FormatLap(r.BestPenalised.Value);
            return s;
        }

        // ── read by the verification harness ──
        public bool CourseOn => _courseOn;
        public Button CourseButton => _btnCourse;
        public CourseRun CourseRunNow => _run;
        public TelemetryTestCourse CourseScene => _course;
        public bool CourseActive => _course != null;
        public bool CoursePillVisible => _coursePill != null && _coursePill.style.display != DisplayStyle.None;
        public VisualElement CoursePill => _coursePill;
        public string CourseHud => _courseTime != null ? _courseTime.text + " | " + _courseCp.text + " | " + _courseHits.text : null;
        public string CourseCaption => _courseCaption?.text;
        public string CourseResultText => _courseResult != null && _courseResult.style.display != DisplayStyle.None ? _courseResult.text : null;
        public bool CourseHitFlashing => _coursePill != null && _coursePill.ClassListContains("dk-course--hit");
        public Button CourseResetButton => _courseReset;

        partial void BuildCourseButton(VisualElement strip)
        {
            _btnCourse = StripButton("DkTelCourse", "studio", out _courseBtnLabel, () => SetCourse(!_courseOn));
            strip.Add(_btnCourse);
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-env-course") >= 0) SetCourse(true); // test aid for the FPS runs (needs -tel-sim)
        }

        partial void BuildCourseOverlay()
        {
            _coursePill = new VisualElement { name = "DkCoursePill", pickingMode = PickingMode.Position };
            _coursePill.AddToClassList("dk-course");
            _coursePill.style.display = DisplayStyle.None;
            _courseCaption = new Label { name = "DkCourseCaption", pickingMode = PickingMode.Ignore };
            _courseCaption.AddToClassList("dk-course-caption");
            _coursePill.Add(_courseCaption);

            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("dk-course-row");
            _courseTime = new Label { name = "DkCourseTime", pickingMode = PickingMode.Ignore };
            _courseTime.AddToClassList("dk-course-time");
            row.Add(_courseTime);
            _courseCp = new Label { name = "DkCourseCp", pickingMode = PickingMode.Ignore };
            _courseCp.AddToClassList("dk-course-stat");
            row.Add(_courseCp);
            _courseHits = new Label { name = "DkCourseHits", pickingMode = PickingMode.Ignore };
            _courseHits.AddToClassList("dk-course-stat");
            row.Add(_courseHits);
            _courseReset = new Button(() => ResetCourse()) { name = "DkCourseReset" };
            _courseReset.AddToClassList("dk-b");
            _courseReset.AddToClassList("dk-b--pill");
            _courseReset.AddToClassList("dk-course-resetbtn");
            row.Add(_courseReset);
            _coursePill.Add(row);

            _courseResult = new Label { name = "DkCourseResult", pickingMode = PickingMode.Ignore };
            _courseResult.AddToClassList("dk-course-result");
            _courseResult.style.display = DisplayStyle.None;
            _coursePill.Add(_courseResult);

            // a click on the pill never reaches the 3D scene (same guard as the shortcuts pill)
            _coursePill.RegisterCallback<PointerDownEvent>(evt => { if (evt.button == 0) InputManager.InputBlocked = true; evt.StopPropagation(); });
            _coursePill.RegisterCallback<PointerUpEvent>(evt => { if (evt.button == 0) InputManager.InputBlocked = false; evt.StopPropagation(); });
            // under the two strips of the Telemetry mode (same column, centred): it never covers the canvas beyond its own 40 px and it goes with the mode
            _container.Add(_coursePill);
        }

        partial void DisposeCourse()
        {
            DestroyCourse();
            _coursePill?.RemoveFromHierarchy();
        }

        partial void RelabelCourse()
        {
            if (_btnCourse == null) return;
            _courseBtnLabel.text = T("course.btn");
            _courseCaption.text = T("course.label");
            _courseReset.text = T("course.reset");
            _courseReset.tooltip = T("course.reset.tip");
            _coursePill.tooltip = T("course.tip");
            _courseLang = null; // the HUD texts are rebuilt on the next tick
        }

        /// <summary>Shows or hides the course. It only exists while the simulator is the source; the lap starts fresh each time.</summary>
        public void SetCourse(bool on)
        {
            _courseOn = on;
            _btnCourse?.EnableInClassList("dk-strip-btn--on", on);
            if (!on) DestroyCourse();
            else { _run?.Reset(); _courseRecordKey = null; }
        }

        /// <summary>Starts a new lap (timer, checkpoints and collisions back to zero; the course stays where it is).</summary>
        public void ResetCourse()
        {
            _run?.Reset();
            _courseRecordKey = null;
            _course?.SetProgress(0, false);
            _courseResult.style.display = DisplayStyle.None;
            _coursePill.RemoveFromClassList("dk-course--done");
            _courseNextText = 0f;
            Debug.Log("[TestCourse] reset");
        }

        private void DestroyCourse()
        {
            if (_course != null) { UnityEngine.Object.Destroy(_course.gameObject); _course = null; }
            _run = null;
            if (_coursePill != null) _coursePill.style.display = DisplayStyle.None;
        }

        partial void TickCourse(bool viewer)
        {
            if (_btnCourse == null) return;
            bool sim = DesktopTelemetry.IsRunning && DesktopTelemetry.IsSimulator;
            bool canUse = sim || _courseOn;
            _btnCourse.SetEnabled(canUse);
            string tip = T(sim ? "course.tip" : "course.tip.off");
            if (_btnCourse.tooltip != tip) _btnCourse.tooltip = tip;

            var drv = TelemetryDroneDriver.Instance;
            var client = DesktopTelemetry.Client;
            TelemetryFrame f = client != null ? client.Displayed : null;
            bool active = _courseOn && sim && f != null && drv != null && drv.Engaged && drv.GroundRef != null && !drv.GroundRef.IsLegacy;
            if (!active)
            {
                if (_course != null || (_coursePill != null && _coursePill.style.display != DisplayStyle.None)) DestroyCourse();
                return;
            }

            if (_course == null)
            {
                var layout = CourseLayout.Default();
                _course = TelemetryTestCourse.Create(layout, drv.FlightOrigin, drv.GroundRef.GroundY, drv.UnitsPerMeter);
                if (_course == null) return;
                _run = new CourseRun(layout);
                _courseResult.style.display = DisplayStyle.None;
                _coursePill.RemoveFromClassList("dk-course--done");
                _courseNextText = 0f;
                Debug.Log("[TestCourse] placed: " + layout.Gates.Count + " rings, " + layout.Zones.Count + " zones, " + layout.Pylons.Count + " pylons, wall with a gap, " + layout.Checkpoints.Count + " ordered checkpoints; farthest element " + layout.FarthestFromOrigin().ToString("F0", CultureInfo.InvariantCulture) + " m from the take-off point");
            }

            _run.Step(Time.unscaledDeltaTime, f.p[0], f.p[1], -f.p[2]);
            float now = Time.unscaledTime;
            for (int i = 0; i < _run.Events.Count; i++)
            {
                CourseEvent ev = _run.Events[i];
                switch (ev.Kind)
                {
                    case CourseEventKind.Started: Debug.Log("[TestCourse] lap started"); break;
                    case CourseEventKind.Checkpoint: Debug.Log("[TestCourse] checkpoint " + ev.Number + "/" + _run.Total + " at " + ev.Time.ToString("F1", CultureInfo.InvariantCulture) + " s"); break;
                    case CourseEventKind.Collision:
                        _course.Flash(ev.Obstacle);
                        _courseHitUntil = now + 0.55f;
                        _courseNextText = 0f;
                        Debug.Log("[TestCourse] collision with " + ev.Obstacle + " (" + _run.Collisions + ") at " + ev.Time.ToString("F1", CultureInfo.InvariantCulture) + " s");
                        break;
                    case CourseEventKind.Finished:
                        RecordCourseResult();
                        Debug.Log("[TestCourse] lap finished in " + ev.Time.ToString("F1", CultureInfo.InvariantCulture) + " s with " + _run.Collisions + " collision(s)");
                        break;
                }
            }
            _run.Events.Clear();
            _course.SetProgress(_run.Reached, _run.State == CourseState.Finished);

            bool show = viewer && _openPop == null;
            if (show != CoursePillVisible) _coursePill.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            bool hit = now < _courseHitUntil;
            if (hit != _coursePill.ClassListContains("dk-course--hit")) _coursePill.EnableInClassList("dk-course--hit", hit);

            string lang = Lang;
            if (now < _courseNextText && lang == _courseLang) return;
            _courseNextText = now + 0.1f;
            _courseLang = lang;
            _courseTime.text = FormatLap(_run.State == CourseState.Finished ? _run.FinishTime : _run.Elapsed);
            _courseCp.text = _run.State == CourseState.Waiting
                ? T("course.waiting")
                : TelemetryStrings.F("course.cp", lang, "k", _run.Reached.ToString(CultureInfo.InvariantCulture), "n", _run.Total.ToString(CultureInfo.InvariantCulture));
            _courseHits.text = TelemetryStrings.F("course.hits", lang, "c", _run.Collisions.ToString(CultureInfo.InvariantCulture));
            bool done = _run.State == CourseState.Finished;
            _coursePill.EnableInClassList("dk-course--done", done);
            _courseResult.style.display = done ? DisplayStyle.Flex : DisplayStyle.None;
            if (done) _courseResult.text = TelemetryStrings.F("course.done", lang, "t", FormatLap(_run.FinishTime), "c", _run.Collisions.ToString(CultureInfo.InvariantCulture)) + (_courseRecordKey != null ? "\n" + CourseRecordLine(lang) : string.Empty);
        }

        /// <summary>m:ss.d with a dot (locale independent).</summary>
        public static string FormatLap(double seconds)
        {
            int tenths = (int)Math.Floor(Math.Max(0, seconds) * 10.0 + 1e-6);
            return (tenths / 600).ToString(CultureInfo.InvariantCulture) + ":" + ((tenths / 10) % 60).ToString("00", CultureInfo.InvariantCulture) + "." + (tenths % 10).ToString(CultureInfo.InvariantCulture);
        }
    }
}
#endif
