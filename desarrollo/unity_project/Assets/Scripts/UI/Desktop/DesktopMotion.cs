#if UNITY_STANDALONE
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.UI.ProceduralIcons;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PC only. One place for the desktop micro-interactions: the global speed constant, the "Reduce animations" switch
    /// (CONFIG, default off, saved) and the small helpers that USS transitions cannot express (enter animations after a
    /// display:none, press flashes, sliding pill indicators). The durations of the USS transitions (120-200 ms, ease-out)
    /// are declared in DesktopUI.uss; <c>.dk-reduce-motion</c> on the root zeroes them.
    /// </summary>
    internal static class DesktopMotion
    {
        /// <summary>Speed of the code-driven animations: 1 = nominal, 2 = twice as fast. (USS transition times live in DesktopUI.uss.)</summary>
        public const float Speed = 1f;

        private const string PrefKey = "Desk_ReduceMotion";
        private static int _reduce = -1;

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void FastIconPolling()
        {
            OutlineIcon.PollMs = 16;
            OutlineIcon.AnimateHover = !Reduce;
            Changed += () => OutlineIcon.AnimateHover = !Reduce;
        }

        public static bool Reduce
        {
            get
            {
                if (_reduce < 0) _reduce = PlayerPrefs.GetInt(PrefKey, 0) != 0 ? 1 : 0;
                return _reduce == 1;
            }
            set
            {
                if (Reduce == value) return;
                _reduce = value ? 1 : 0;
                PlayerPrefs.SetInt(PrefKey, _reduce);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>-desk-reduce on the command line forces the switch (tests).</summary>
        public static void ApplyCommandLine()
        {
            foreach (string a in Environment.GetCommandLineArgs())
            {
                if (a.Equals("-desk-reduce", StringComparison.OrdinalIgnoreCase)) _reduce = 1;
                if (a.Equals("-desk-noreduce", StringComparison.OrdinalIgnoreCase)) _reduce = 0;
            }
        }

        public static int Ms(float ms) => Mathf.Max(1, Mathf.RoundToInt(ms / Speed));

        /// <summary>
        /// Enter animation: the element starts in its ".dk-enter" state (no transition) and, a frame later, transitions to its
        /// normal state. Works after display:none -> flex, where a plain USS transition has nothing to start from.
        /// </summary>
        public static void Enter(VisualElement e, int delayMs = 0, string extra = null)
        {
            if (e == null || Reduce) return;
            e.AddToClassList("dk-enter");
            e.AddToClassList("dk-notrans");
            if (extra != null) e.AddToClassList(extra);
            long d = Ms(delayMs);
            e.schedule.Execute(() => e.RemoveFromClassList("dk-notrans")).StartingIn(d + 20);
            e.schedule.Execute(() => { e.RemoveFromClassList("dk-enter"); if (extra != null) e.RemoveFromClassList(extra); }).StartingIn(d + 60);
        }

        /// <summary>A quick scale + celeste flash (selection changes, key presses mirrored on screen).</summary>
        public static void Flash(VisualElement e, string cls = "dk-flash", int holdMs = 140)
        {
            if (e == null || Reduce) return;
            e.AddToClassList(cls);
            e.schedule.Execute(() => e.RemoveFromClassList(cls)).StartingIn(holdMs);
        }

        public static bool IsShown(VisualElement e)
        {
            for (VisualElement v = e; v != null; v = v.parent)
            {
                if (v.resolvedStyle.display == DisplayStyle.None) return false;
            }

            return true;
        }

        // popovers: run Enter each time they become visible
        private sealed class Watch { public VisualElement E; public bool Was; }
        private static readonly List<Watch> Watched = new List<Watch>();

        public static void WatchVisible(VisualElement e)
        {
            if (e != null) Watched.Add(new Watch { E = e, Was = false });
        }

        public static void TickWatched()
        {
            for (int i = 0; i < Watched.Count; i++)
            {
                Watch w = Watched[i];
                bool now = IsShown(w.E);
                if (now && !w.Was) Enter(w.E);
                w.Was = now;
            }
        }
    }

    /// <summary>A highlight that slides under the active button of a segmented control (mode 1/2/3, ES/EN).</summary>
    internal sealed class SlidingPill
    {
        private readonly VisualElement _pill;
        private VisualElement _target;
        private bool _placed;
        private float _lastX, _lastW;

        public SlidingPill(VisualElement container, string cls)
        {
            _pill = new VisualElement { pickingMode = PickingMode.Ignore };
            _pill.AddToClassList("dk-pill");
            _pill.AddToClassList(cls);
            _pill.AddToClassList("dk-notrans");
            container.Insert(0, _pill);
        }

        public void Update(VisualElement active)
        {
            if (active == null)
            {
                _pill.style.opacity = 0f;
                _target = null;
                return;
            }

            Rect r = active.layout;
            if (float.IsNaN(r.width) || r.width < 4f) return;

            if (_target == active && Mathf.Approximately(r.x, _lastX) && Mathf.Approximately(r.width, _lastW)) return;
            _lastX = r.x;
            _lastW = r.width;
            _pill.style.opacity = 1f;
            _pill.style.top = r.y;
            _pill.style.height = r.height;
            _pill.style.left = r.x;
            _pill.style.width = r.width;
            if (!_placed)
            {
                _placed = true;
                _pill.schedule.Execute(() => _pill.RemoveFromClassList("dk-notrans")).StartingIn(50);
            }

            _target = active;
        }
    }
}
#endif
