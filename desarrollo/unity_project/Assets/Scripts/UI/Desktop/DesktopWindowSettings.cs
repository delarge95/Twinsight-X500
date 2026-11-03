#if UNITY_STANDALONE
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PC only (U5): window mode, windowed size, resolution list, vsync and the minimum window size.
    /// Preferences live in PlayerPrefs (Desk_*). Command-line arguments always win
    /// (-screen-width / -screen-height / -screen-fullscreen / -vsync); <c>-desk-nofit</c> disables the
    /// "fit the window inside the monitor work area" step (used to capture 2560x1440 on a 1080p monitor).
    /// </summary>
    public static class DesktopWindowSettings
    {
        /// <summary>Smallest window the layout is designed for (the compact layout kicks in below ~900 logical px).</summary>
        public const int MinWidth = 1280;
        public const int MinHeight = 720;

        private const string KeyFullscreen = "Desk_Fullscreen";
        private const string KeyWinW = "Desk_WinW";
        private const string KeyWinH = "Desk_WinH";
        private const string KeyVsync = "Desk_VSync";

        // Windows adds a title bar + borders around the client area.
        private const int FrameExtraW = 16;
        private const int FrameExtraH = 48;

        private static readonly Vector2Int[] StandardSizes =
        {
            new Vector2Int(1280, 720), new Vector2Int(1366, 768), new Vector2Int(1600, 900),
            new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(3840, 2160)
        };

        public static event Action Changed;

        public static bool CliForcedWindow { get; private set; }
        public static bool CliForcedVsync { get; private set; }
        public static bool NoFit { get; private set; }

        private static int _lastWindowedW;
        private static int _lastWindowedH;
        private static float _belowMinSince = -1f;

        public static bool IsFullscreen => Screen.fullScreenMode != FullScreenMode.Windowed;

        public static bool VSync
        {
            get => QualitySettings.vSyncCount > 0;
            set
            {
                QualitySettings.vSyncCount = value ? 1 : 0;
                PlayerPrefs.SetInt(KeyVsync, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        // ───────────────────────────── startup ─────────────────────────────

        public static void ReadCommandLine()
        {
            foreach (string a in Environment.GetCommandLineArgs())
            {
                if (a.Equals("-screen-width", StringComparison.OrdinalIgnoreCase)
                    || a.Equals("-screen-height", StringComparison.OrdinalIgnoreCase)
                    || a.Equals("-screen-fullscreen", StringComparison.OrdinalIgnoreCase))
                {
                    CliForcedWindow = true;
                }
                else if (a.Equals("-vsync", StringComparison.OrdinalIgnoreCase))
                {
                    CliForcedVsync = true;
                }
                else if (a.Equals("-desk-nofit", StringComparison.OrdinalIgnoreCase))
                {
                    NoFit = true;
                }
            }
        }

        /// <summary>Applies the saved window mode / size / vsync and fits the window inside the monitor.</summary>
        public static IEnumerator Startup()
        {
            ReadCommandLine();

            // Let Unity finish creating the window first.
            yield return null;
            yield return null;

            if (Application.isEditor)
            {
                yield break;
            }

            if (!CliForcedWindow && PlayerPrefs.HasKey(KeyFullscreen))
            {
                bool wantFull = PlayerPrefs.GetInt(KeyFullscreen, 0) == 1;
                if (wantFull && !IsFullscreen)
                {
                    EnterFullscreen();
                    yield return null;
                }
                else if (!wantFull && IsFullscreen)
                {
                    LeaveFullscreen();
                    yield return null;
                    yield return null;
                }
                else if (!wantFull)
                {
                    int w = PlayerPrefs.GetInt(KeyWinW, 0);
                    int h = PlayerPrefs.GetInt(KeyWinH, 0);
                    if (w > 0 && h > 0)
                    {
                        Screen.SetResolution(ClampWindowSize(new Vector2Int(w, h)).x, ClampWindowSize(new Vector2Int(w, h)).y, FullScreenMode.Windowed);
                        yield return null;
                    }
                }
            }

            if (!CliForcedVsync && PlayerPrefs.HasKey(KeyVsync))
            {
                QualitySettings.vSyncCount = PlayerPrefs.GetInt(KeyVsync, 1) == 1 ? 1 : 0;
            }

            if (!IsFullscreen)
            {
                yield return FitWindowToWorkArea();
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// A window whose client area is as big as the monitor (e.g. 1920x1080 on a 1080p screen) ends up with
        /// its bottom edge under the taskbar, which cut the bottom of the UI (the mode capsule). Shrink the client
        /// area to the work area and centre the window.
        /// </summary>
        public static IEnumerator FitWindowToWorkArea()
        {
            if (NoFit || IsFullscreen || Application.isEditor)
            {
                yield break;
            }

            Vector2Int target = ClampToWorkArea(new Vector2Int(Screen.width, Screen.height));
            if (target.x != Screen.width || target.y != Screen.height)
            {
                Screen.SetResolution(target.x, target.y, FullScreenMode.Windowed);
                yield return null;
                yield return null;
            }

            CenterWindow();
        }

        // ───────────────────────────── window mode ─────────────────────────────

        public static void ToggleFullscreen()
        {
            SetFullscreen(!IsFullscreen);
        }

        public static void SetFullscreen(bool full)
        {
            if (Application.isEditor || full == IsFullscreen)
            {
                return;
            }

            if (full) EnterFullscreen(); else LeaveFullscreen();

            PlayerPrefs.SetInt(KeyFullscreen, full ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static void EnterFullscreen()
        {
            _lastWindowedW = Screen.width;
            _lastWindowedH = Screen.height;
            if (_lastWindowedW > 0)
            {
                PlayerPrefs.SetInt(KeyWinW, _lastWindowedW);
                PlayerPrefs.SetInt(KeyWinH, _lastWindowedH);
            }

            DisplayInfo di = SafeDisplayInfo();
            int w = di.width > 0 ? di.width : Screen.currentResolution.width;
            int h = di.height > 0 ? di.height : Screen.currentResolution.height;
            Screen.SetResolution(w, h, FullScreenMode.FullScreenWindow);
        }

        private static void LeaveFullscreen()
        {
            int w = _lastWindowedW > 0 ? _lastWindowedW : PlayerPrefs.GetInt(KeyWinW, 0);
            int h = _lastWindowedH > 0 ? _lastWindowedH : PlayerPrefs.GetInt(KeyWinH, 0);
            if (w <= 0 || h <= 0)
            {
                w = 1600;
                h = 900;
            }

            Vector2Int size = ClampWindowSize(new Vector2Int(w, h));
            Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
        }

        // ───────────────────────────── resolution ─────────────────────────────

        /// <summary>Resolutions offered for the windowed client area (never larger than the monitor).</summary>
        public static List<Vector2Int> GetResolutionOptions()
        {
            DisplayInfo di = SafeDisplayInfo();
            int maxW = di.width > 0 ? di.width : Screen.currentResolution.width;
            int maxH = di.height > 0 ? di.height : Screen.currentResolution.height;
            if (maxW <= 0) maxW = 3840;
            if (maxH <= 0) maxH = 2160;

            var set = new HashSet<Vector2Int>();
            foreach (Vector2Int s in StandardSizes)
            {
                if (s.x <= maxW && s.y <= maxH) set.Add(s);
            }

            foreach (Resolution r in Screen.resolutions)
            {
                if (r.width >= MinWidth && r.height >= MinHeight && r.width <= maxW && r.height <= maxH)
                {
                    set.Add(new Vector2Int(r.width, r.height));
                }
            }

            if (!IsFullscreen)
            {
                set.Add(new Vector2Int(Screen.width, Screen.height));
            }

            var list = new List<Vector2Int>(set);
            list.Sort((a, b) => (a.x * a.y).CompareTo(b.x * b.y));
            return list;
        }

        public static void SetWindowedSize(Vector2Int size)
        {
            if (Application.isEditor)
            {
                return;
            }

            size = ClampWindowSize(size);
            _lastWindowedW = size.x;
            _lastWindowedH = size.y;
            PlayerPrefs.SetInt(KeyWinW, size.x);
            PlayerPrefs.SetInt(KeyWinH, size.y);
            PlayerPrefs.SetInt(KeyFullscreen, 0);
            PlayerPrefs.Save();
            Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
            Changed?.Invoke();
        }

        /// <summary>Resizable window with a floor: grows back to the minimum after being dragged smaller.</summary>
        public static void TickMinimumSize()
        {
            if (Application.isEditor || IsFullscreen || NoFit)
            {
                return;
            }

            Vector2Int min = EffectiveMinimum();
            bool below = Screen.width < min.x || Screen.height < min.y;
            if (!below)
            {
                _belowMinSince = -1f;
                return;
            }

            // Wait until the user stops dragging the border (no mouse button held) before snapping back.
            if (Input.GetMouseButton(0))
            {
                _belowMinSince = -1f;
                return;
            }

            if (_belowMinSince < 0f)
            {
                _belowMinSince = Time.unscaledTime;
                return;
            }

            if (Time.unscaledTime - _belowMinSince > 0.35f)
            {
                _belowMinSince = -1f;
                Screen.SetResolution(Mathf.Max(Screen.width, min.x), Mathf.Max(Screen.height, min.y), FullScreenMode.Windowed);
                Changed?.Invoke();
            }
        }

        // ───────────────────────────── helpers ─────────────────────────────

        private static Vector2Int EffectiveMinimum()
        {
            Vector2Int max = MaxClientSize();
            return new Vector2Int(Mathf.Min(MinWidth, max.x), Mathf.Min(MinHeight, max.y));
        }

        private static Vector2Int MaxClientSize()
        {
            DisplayInfo di = SafeDisplayInfo();
            RectInt wa = di.workArea;
            if (wa.width <= 0 || wa.height <= 0)
            {
                return new Vector2Int(int.MaxValue / 2, int.MaxValue / 2);
            }

            return new Vector2Int(wa.width - FrameExtraW, wa.height - FrameExtraH);
        }

        private static Vector2Int ClampToWorkArea(Vector2Int size)
        {
            if (NoFit)
            {
                return size;
            }

            Vector2Int max = MaxClientSize();
            return new Vector2Int(Mathf.Min(size.x, max.x), Mathf.Min(size.y, max.y));
        }

        private static Vector2Int ClampWindowSize(Vector2Int size)
        {
            Vector2Int min = EffectiveMinimum();
            size = new Vector2Int(Mathf.Max(size.x, min.x), Mathf.Max(size.y, min.y));
            return ClampToWorkArea(size);
        }

        private static DisplayInfo SafeDisplayInfo()
        {
            try
            {
                return Screen.mainWindowDisplayInfo;
            }
            catch (Exception)
            {
                return default;
            }
        }

        private static void CenterWindow()
        {
            try
            {
                DisplayInfo di = Screen.mainWindowDisplayInfo;
                RectInt wa = di.workArea;
                if (wa.width <= 0 || wa.height <= 0)
                {
                    return;
                }

                int outerW = Screen.width + FrameExtraW;
                int outerH = Screen.height + FrameExtraH;
                var pos = new Vector2Int(
                    wa.x + Mathf.Max(0, (wa.width - outerW) / 2),
                    wa.y + Mathf.Max(0, (wa.height - outerH) / 2));
                Screen.MoveMainWindowTo(di, pos);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DesktopWindow] Could not centre the window: " + e.Message);
            }
        }
    }
}
#endif
