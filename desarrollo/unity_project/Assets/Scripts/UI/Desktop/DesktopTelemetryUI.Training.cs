#if UNITY_STANDALONE
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.Telemetry;
using WebGL.Telemetry.Json;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Lote 1c, tool 6 (PC only): guided training on the SIMULATOR. A strip button «Entrenar» (Shift+T) opens a popover with the lessons (their progress and best time);
    /// while one is in progress a compact card sits at the top left of the viewer (objective, progress bar, «Pista» after 20 s without progress, «Reintentar», «Salir»)
    /// and the objective is marked on the map. The steps are checked ONLY against the telemetry frames on screen (the pure <see cref="Training"/>, golden-tested
    /// against the web), never against the input used, so the lesson can be flown with commands, keys or the pad. Lessons: the bundled missions.json (a copy of the web's
    /// contracts/training/missions.json) with their texts from design_training.json (a copy of the web's design/training.json). Progress is remembered in
    /// persistentDataPath/twinsight/training.json (the same JSON as the web's `twinsight.training.v1`). Honesty: «Entrenamiento en el simulador · simulado · no sustituye
    /// la formación oficial de piloto» is always in the popover and on the card. Nothing travels on the wire that did not exist: the pupil flies with the existing controls.
    /// </summary>
    internal sealed partial class DesktopTelemetryUI
    {
        private sealed class LessonText
        {
            public string[] Title, Summary;
            public readonly Dictionary<string, string[][]> Steps = new Dictionary<string, string[][]>(); // step id -> { title, objective, hint } each { es, en }
        }

        private Button _btnTraining;
        private Label _trBtnLabel;
        private VisualElement _popTraining, _trList, _trCard, _trBarFill, _trActions, _trDoneActions;
        private Label _trTitle, _trDisclaimer, _trNeed, _trCardTitle, _trCardStep, _trCardObjective, _trCardHint, _trCardNote, _trCardFoot, _trDoneTitle, _trDoneTime;
        private Button _trHintBtn, _trRetryBtn, _trQuitBtn, _trNextBtn, _trToListBtn;
        private List<Lesson> _trLessons;
        private readonly Dictionary<string, LessonText> _trTexts = new Dictionary<string, LessonText>();
        private Dictionary<string, LessonRecord> _trProgress = new Dictionary<string, LessonRecord>();
        private LessonRun _trRun;
        private Lesson _trLesson;
        private bool _trHintOpen, _trLoaded;
        private string _trListSig, _trOutcomeText;
        private float _trNextTick, _trLastTick;
        private string _trLoadError;
        private TargetMark _trTarget;
        /// <summary>Seconds without progress after which a hint is raised; the probes shorten it through <see cref="TrainingHintSeconds"/> only to look at the card quickly.</summary>
        private double _trHintAfter = Training.HintAfterS;

        // ── read by the verification harness ──
        public Button TrainingButton => _btnTraining;
        public VisualElement TrainingPopover => _popTraining;
        public VisualElement TrainingCard => _trCard;
        public VisualElement TrainingList => _trList;
        public bool TrainingActive => _trRun != null;
        public LessonRun TrainingRun => _trRun;
        public Lesson TrainingLesson => _trLesson;
        public IReadOnlyList<Lesson> TrainingLessons { get { EnsureTrainingLoaded(); return _trLessons; } }
        public Dictionary<string, LessonRecord> TrainingProgressNow => _trProgress;
        public string TrainingCardStep => _trCardStep?.text;
        public string TrainingCardObjective => _trCardObjective?.text;
        public string TrainingHintText => _trCardHint != null && _trCardHint.style.display != DisplayStyle.None ? _trCardHint.text : null;
        public Button TrainingHintButton => _trHintBtn;
        public Button TrainingRetryButton => _trRetryBtn;
        public Button TrainingQuitButton => _trQuitBtn;
        public string TrainingDisclaimerText => _trDisclaimer?.text;
        public static string TrainingFile => Path.Combine(Application.persistentDataPath, "twinsight", "training.json");
        /// <summary>Test seam: the seconds without progress before the hint is raised (the golden value is 20).</summary>
        public double TrainingHintSeconds { get => _trHintAfter; set => _trHintAfter = value; }
        public TargetMark TrainingTargetMark => _trTarget;

        // ───────────────────────── data ─────────────────────────

        private static string TrainingDataPath(string file) => Path.Combine(Application.streamingAssetsPath, "Telemetry", "training", file);

        private void EnsureTrainingLoaded()
        {
            if (_trLoaded) return;
            _trLoaded = true;
            try
            {
                _trLessons = Training.ParseLessons(File.ReadAllText(TrainingDataPath("missions.json")));
                if (_trLessons == null) throw new InvalidDataException("missions.json is not a version 1 lesson file");
                var root = (Dictionary<string, object>)TelemetryJson.Parse(File.ReadAllText(TrainingDataPath("design_training.json")));
                foreach (object lo in (List<object>)root["lessons"])
                {
                    var l = (Dictionary<string, object>)lo;
                    var lt = new LessonText { Title = Pair(l["title"]), Summary = Pair(l["summary"]) };
                    foreach (object so in (List<object>)l["steps"])
                    {
                        var s = (Dictionary<string, object>)so;
                        lt.Steps[(string)s["id"]] = new[] { Pair(s["title"]), Pair(s["objective"]), Pair(s["hint"]) };
                    }
                    _trTexts[(string)l["id"]] = lt;
                }
                _trProgress = Training.ParseProgress(File.Exists(TrainingFile) ? File.ReadAllText(TrainingFile) : null);
            }
            catch (Exception e)
            {
                _trLoadError = e.GetType().Name + ": " + e.Message;
                Debug.LogWarning("[Training] could not load the lessons: " + _trLoadError);
                _trLessons = null;
            }
        }

        private static string[] Pair(object o)
        {
            var d = (Dictionary<string, object>)o;
            return new[] { (string)d["es"], (string)d["en"] };
        }

        private static string Pick(string[] pair) => pair == null ? string.Empty : (Lang == "en" ? pair[1] : pair[0]);

        private string LessonTitle(Lesson l) => _trTexts.TryGetValue(l.id, out LessonText t) ? Pick(t.Title) : l.id;
        private string LessonSummary(Lesson l) => _trTexts.TryGetValue(l.id, out LessonText t) ? Pick(t.Summary) : string.Empty;

        private string StepText(Lesson l, int stepIndex, int which)
        {
            string id = l.steps[Math.Min(stepIndex, l.steps.Count - 1)].id;
            return _trTexts.TryGetValue(l.id, out LessonText t) && t.Steps.TryGetValue(id, out string[][] s) ? Pick(s[which]) : id;
        }

        private void SaveTrainingProgress()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(TrainingFile));
                File.WriteAllText(TrainingFile, Training.ProgressToJson(_trProgress));
            }
            catch (Exception e) { Debug.LogWarning("[Training] progress not saved: " + e.Message); }
        }

        // ───────────────────────── build ─────────────────────────

        partial void BuildTrainingTool(VisualElement strip)
        {
            _btnTraining = StripButton("DkTelTraining", "check", out _trBtnLabel, () => ToggleTrainingPopover());
            _btnTraining.Add(Kbd("Shift+T"));
            strip.Add(_btnTraining);
        }

        partial void BuildTrainingPopover()
        {
            _popTraining = NewPopover("DkPopTraining", "dk-pop--w360", out _trTitle);
            var tagRow = new VisualElement();
            tagRow.AddToClassList("dk-fault-tagrow");
            _trDisclaimer = Text("dk-tag", tagRow);
            _trDisclaimer.AddToClassList("dk-tag--simulated");
            _trDisclaimer.style.whiteSpace = WhiteSpace.Normal;
            _popTraining.Add(tagRow);
            _trNeed = Text("dk-note", _popTraining);
            _trNeed.AddToClassList("dk-note--warn");
            _trList = new VisualElement { name = "DkTrainingList" };
            _trList.AddToClassList("dk-tr-list");
            _popTraining.Add(_trList);
            RegisterToolPopover("training", _popTraining);

            // the card on the viewer (top left), only while a lesson is in progress
            _trCard = new VisualElement { name = "DkTrainingCard", pickingMode = PickingMode.Position };
            _trCard.AddToClassList("dk-tr-card");
            _trCard.style.display = DisplayStyle.None;
            _trCardTitle = Text("dk-tr-title", _trCard);
            _trCardStep = Text("dk-tr-step", _trCard);
            _trCardObjective = Text("dk-tr-objective", _trCard);
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("dk-tr-bar");
            _trBarFill = new VisualElement { pickingMode = PickingMode.Ignore };
            _trBarFill.AddToClassList("dk-tr-barfill");
            bar.Add(_trBarFill);
            _trCard.Add(bar);
            _trCardHint = Text("dk-tr-hint", _trCard);
            _trCardHint.style.display = DisplayStyle.None;
            _trCardNote = Text("dk-tr-note", _trCard);
            _trActions = new VisualElement();
            _trActions.AddToClassList("dk-tr-actions");
            _trHintBtn = ToolButton("DkTrHint", "dk-b--pill", () => { _trHintOpen = !_trHintOpen; RefreshTrainingCard(); });
            _trRetryBtn = ToolButton("DkTrRetry", "dk-b--pill", () => RetryTrainingStep());
            _trQuitBtn = ToolButton("DkTrQuit", "dk-b--pill", () => QuitTraining());
            foreach (Button b in new[] { _trHintBtn, _trRetryBtn, _trQuitBtn }) { b.AddToClassList("dk-tr-btn"); _trActions.Add(b); }
            _trCard.Add(_trActions);
            _trDoneTitle = Text("dk-tr-donetitle", _trCard);
            _trDoneTime = Text("dk-tr-step", _trCard);
            _trDoneActions = new VisualElement();
            _trDoneActions.AddToClassList("dk-tr-actions");
            _trNextBtn = ToolButton("DkTrNext", "dk-b--pill", () => StartNextTrainingLesson());
            _trToListBtn = ToolButton("DkTrList", "dk-b--pill", () => { QuitTraining(); TogglePopover("training"); });
            foreach (Button b in new[] { _trNextBtn, _trToListBtn }) { b.AddToClassList("dk-tr-btn"); _trDoneActions.Add(b); }
            _trCard.Add(_trDoneActions);
            _trCardFoot = Text("dk-tr-note", _trCard);
            _root.Add(_trCard);
        }

        partial void DisposeTraining()
        {
            _trCard?.RemoveFromHierarchy();
            _popTraining?.RemoveFromHierarchy();
        }

        partial void RelabelTraining()
        {
            if (_btnTraining == null || _popTraining == null) return;
            _trBtnLabel.text = T("tools.training");
            _trTitle.text = T("training.title");
            _trDisclaimer.text = T("training.disclaimer");
            _trNeed.text = T("training.need");
            _trHintBtn.text = T("training.hint");
            _trRetryBtn.text = T("training.retry");
            _trRetryBtn.tooltip = T("training.retryTitle");
            _trQuitBtn.text = T("training.quit");
            _trQuitBtn.tooltip = T("training.quitTitle");
            _trNextBtn.text = T("training.next");
            _trToListBtn.text = T("training.toList");
            _trCardFoot.text = T("training.disclaimer");
            _trListSig = null;
            RefreshTrainingCard();
        }

        // ───────────────────────── controls ─────────────────────────

        /// <summary>Training belongs to the simulator (the frames are simulated).</summary>
        public bool CanTrain => DesktopTelemetry.IsRunning && DesktopTelemetry.IsSimulator && DesktopTelemetry.Client?.Hello != null;

        /// <summary>Shift+T and the strip button.</summary>
        public void ToggleTrainingPopover() => TogglePopover("training");

        public bool StartTraining(string lessonId, bool resume = true)
        {
            EnsureTrainingLoaded();
            if (_trLessons == null || !CanTrain) return false;
            Lesson lesson = _trLessons.Find(x => x.id == lessonId);
            if (lesson == null) return false;
            _trProgress.TryGetValue(lesson.id, out LessonRecord rec);
            int from = resume ? Training.ResumeFrom(lesson, rec) : 0;
            _trLesson = lesson;
            _trRun = Training.StartLesson(lesson, from);
            _trHintOpen = false;
            _trOutcomeText = null;
            _trLastTick = Time.unscaledTime;
            _trNextTick = 0f;
            _trListSig = null;
            OnTrainingStepStarted();
            CloseAllPopovers();
            RefreshTrainingCard();
            return true;
        }

        public void QuitTraining()
        {
            if (_trRun == null) return;
            _trRun = null;
            _trLesson = null;
            _trTarget = null;
            _trCard.style.display = DisplayStyle.None;
            _mapDirty = true;
            _trListSig = null;
        }

        public void RetryTrainingStep()
        {
            if (_trRun == null || _trRun.done) return;
            _trRun = Training.RetryStep(_trRun);
            _trHintOpen = false;
            RefreshTrainingCard();
        }

        private void StartNextTrainingLesson()
        {
            if (_trLessons == null || _trLesson == null) return;
            int i = _trLessons.IndexOf(_trLesson);
            Lesson next = i >= 0 && i + 1 < _trLessons.Count ? _trLessons[i + 1] : _trLessons[0];
            StartTraining(next.id, false);
        }

        private void OnTrainingStepStarted()
        {
            if (_trRun == null || _trRun.done) return;
            // the course lesson delegates in the circuit: it is placed in the scene and on the map
            if (_trLesson.steps[_trRun.step].type == StepType.Course && !CourseActive) SetCourse(true);
        }

        // ───────────────────────── per frame ─────────────────────────

        partial void TickTraining(bool viewer)
        {
            if (_btnTraining == null) return;
            bool running = DesktopTelemetry.IsRunning;
            bool can = CanTrain;
            _btnTraining.SetEnabled(running && TelemetryMenu.Has(ToolId.Train, CurrentSurface)); // hidden (not dimmed) outside the simulator: ApplyMenuTable
            _btnTraining.EnableInClassList("dk-strip-btn--on", _openPop == "training" || _trRun != null);
            string tip = !running ? T("tools.needTelemetry") : !can ? T("training.need") : T("training.title");
            if (_btnTraining.tooltip != tip) _btnTraining.tooltip = tip;
            if (_trRun == null) return;
            if (!can || !viewer) { if (!can) QuitTraining(); return; }
            if (Time.unscaledTime < _trNextTick) return;
            _trNextTick = Time.unscaledTime + 0.05f;
            double dt = Math.Min(0.5, Time.unscaledTime - _trLastTick);
            _trLastTick = Time.unscaledTime;
            TelemetryFrame f = DesktopTelemetry.Client.Displayed;
            if (f == null || _trRun.done) { RefreshTrainingCard(); return; }
            var events = new List<LessonEvent>();
            int stepBefore = _trRun.step;
            _trRun = Training.LessonStep(_trLesson, _trRun, Training.FrameOf(f), dt, events, id => CourseLayout.Default());
            // the hint threshold of this desktop (the probes shorten it): the run raises it at 20 s; a shorter one is read from the context
            if (_trHintAfter < Training.HintAfterS && _trRun.hint == null && !_trRun.done && _trRun.ctx.idleS >= _trHintAfter) _trRun.hint = _trLesson.steps[_trRun.step].id;
            foreach (LessonEvent ev in events)
            {
                if (ev.kind == "done")
                {
                    _trProgress.TryGetValue(_trLesson.id, out LessonRecord old);
                    _trProgress = Training.RecordRun(_trProgress, _trRun);
                    bool newBest = _trRun.fromStart && (old == null || !old.bestS.HasValue || ev.timeS < old.bestS.Value);
                    _trOutcomeText = TelemetryStrings.F("training.doneTime", Lang, "time", FmtClock(ev.timeS)) + (newBest ? " · " + T("training.newBest") : string.Empty);
                    ShowToast(T("training.doneTitle"), false);
                    SaveTrainingProgress();
                    _trListSig = null;
                }
            }
            if (_trRun.step != stepBefore && !_trRun.done)
            {
                _trProgress = Training.RecordRun(_trProgress, _trRun);
                SaveTrainingProgress();
                _trHintOpen = false;
                OnTrainingStepStarted();
            }
            RefreshTrainingCard();
        }

        private static string FmtClock(double s)
        {
            int t = (int)Math.Max(0, Math.Round(s));
            return (t / 60) + ":" + (t % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        private void RefreshTrainingCard()
        {
            if (_trCard == null) return;
            bool on = _trRun != null;
            _trCard.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (!on) return;
            bool done = _trRun.done;
            int stepIndex = Math.Min(_trRun.step, _trLesson.steps.Count - 1);
            _trCardTitle.text = LessonTitle(_trLesson);
            _trCardStep.text = done ? string.Empty : TelemetryStrings.F("training.stepOf", Lang, "k", _trRun.step + 1, "n", _trRun.total) + " · " + StepText(_trLesson, stepIndex, 0);
            _trCardObjective.text = done ? string.Empty : StepText(_trLesson, stepIndex, 1);
            _trCardStep.style.display = done ? DisplayStyle.None : DisplayStyle.Flex;
            _trCardObjective.style.display = done ? DisplayStyle.None : DisplayStyle.Flex;
            _trBarFill.style.width = Length.Percent((float)(Training.LessonFraction(_trRun) * 100.0));
            bool hintRaised = !done && _trRun.hint != null;
            _trHintBtn.style.display = hintRaised ? DisplayStyle.Flex : DisplayStyle.None;
            _trHintBtn.EnableInClassList("dk-b--on", _trHintOpen);
            _trCardHint.style.display = hintRaised && _trHintOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (hintRaised) _trCardHint.text = StepText(_trLesson, stepIndex, 2);
            _trActions.style.display = done ? DisplayStyle.None : DisplayStyle.Flex;
            _trDoneTitle.style.display = done ? DisplayStyle.Flex : DisplayStyle.None;
            _trDoneTime.style.display = done ? DisplayStyle.Flex : DisplayStyle.None;
            _trDoneActions.style.display = done ? DisplayStyle.Flex : DisplayStyle.None;
            _trDoneTitle.text = T("training.doneTitle");
            _trDoneTime.text = _trOutcomeText ?? string.Empty;
            bool course = !done && _trLesson.steps[stepIndex].type == StepType.Course;
            _trCardNote.style.display = course ? DisplayStyle.Flex : DisplayStyle.None;
            _trCardNote.text = course ? T("training.courseNote") : string.Empty;
            _trTarget = done ? null : Training.StepTarget(_trLesson.steps[stepIndex], _trRun.ctx);
            _mapDirty = true;
        }

        partial void TickTrainingPopover()
        {
            EnsureTrainingLoaded();
            bool can = CanTrain;
            _trNeed.style.display = can ? DisplayStyle.None : DisplayStyle.Flex;
            if (_trLessons == null)
            {
                _trNeed.style.display = DisplayStyle.Flex;
                _trNeed.text = _trLoadError ?? string.Empty;
                return;
            }
            string sig = Lang + "|" + can + "|" + (_trRun != null ? _trRun.lessonId + _trRun.step : "-");
            foreach (var kv in _trProgress) sig += "|" + kv.Key + kv.Value.step + kv.Value.done + kv.Value.bestS;
            if (sig == _trListSig) return;
            _trListSig = sig;
            _trList.Clear();
            foreach (Lesson lesson in _trLessons)
            {
                Lesson l = lesson;
                _trProgress.TryGetValue(l.id, out LessonRecord rec);
                var row = new VisualElement { name = "DkTrRow_" + l.id };
                row.AddToClassList("dk-tr-row");
                var texts = new VisualElement { pickingMode = PickingMode.Ignore };
                texts.AddToClassList("dk-tr-rowtext");
                Text("dk-tr-rowtitle", texts).text = LessonTitle(l);
                Text("dk-tr-rowsum", texts).text = LessonSummary(l);
                string status = rec == null || (!rec.done && rec.step == 0) ? T("training.rowNew")
                    : rec.done ? (rec.bestS.HasValue ? TelemetryStrings.F("training.rowDoneBest", Lang, "time", FmtClock(rec.bestS.Value)) : T("training.rowDone"))
                    : TelemetryStrings.F("training.rowStep", Lang, "k", rec.step + 1, "n", l.steps.Count);
                Text("dk-tr-rowstatus", texts).text = status;
                row.Add(texts);
                string label = rec == null || (!rec.done && rec.step == 0) ? T("training.start") : rec.done ? T("training.again") : T("training.resume");
                var go = ToolButton("DkTrGo_" + l.id, "dk-b--pill", () => StartTraining(l.id, true));
                go.text = label;
                go.AddToClassList("dk-tr-go");
                go.SetEnabled(can);
                row.Add(go);
                _trList.Add(row);
            }
        }
    }
}
#endif
