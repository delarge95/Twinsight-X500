#if UNITY_STANDALONE
using System;
using UnityEngine;

namespace WebGL.Core.Managers
{
    /// <summary>
    /// MVP P0, «Modo juego» (PC only): the mouse cursor is locked (<see cref="Cursor.lockState"/>) and the mouse orbits the camera. One place decides it:
    ///  - ON only from piloting (the button / the key X of the piloting strip) and only when nothing owns the keyboard (<see cref="DesktopKeyboardGate"/>);
    ///  - OFF with Esc (first thing Esc does), the same key X, leaving piloting, a stopped source, the window losing focus, the help opening or a text field taking the keyboard;
    ///  - while ON the scene neither hovers, selects nor flies to a click (SelectionManager asks <see cref="Active"/>) and a full-screen shield swallows the UI pointer events
    ///    (the cursor sits in the middle of the screen and could otherwise press whatever is there); the camera reads the mouse movement (OrbitCameraController).
    /// </summary>
    public static class DesktopGameMode
    {
        /// <summary>True while the cursor is captured.</summary>
        public static bool Active { get; private set; }

        /// <summary>Raised when the mode changes (the UI shows / hides its indicator and shield). The string is the reason (logs and probes).</summary>
        public static event Action<bool, string> Changed;

        public static string LastReason { get; private set; }

        /// <summary>Verification seam: the in-player probe has no real mouse; when set, it replaces the mouse movement (pixels per frame) read in game mode.</summary>
        public static Func<Vector2> MouseDeltaOverride;

        /// <summary>The mouse movement of this frame (the old Input Manager "Mouse X" / "Mouse Y" axes, or the seam).</summary>
        public static Vector2 ReadMouseDelta()
        {
            if (MouseDeltaOverride != null) return MouseDeltaOverride();
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        }

        /// <summary>Verification: the lock state asked of the OS, as Unity reports it.</summary>
        public static bool CursorLocked => Cursor.lockState == CursorLockMode.Locked;

        public static void Set(bool on, string why)
        {
            if (on == Active) return;
            Active = on;
            LastReason = why;
            try
            {
                Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !on;
            }
            catch (Exception e) { Debug.LogWarning("[GameMode] cursor lock failed: " + e.Message); }
            Debug.Log("[GameMode] " + (on ? "ON" : "OFF") + " (" + why + "): cursor " + (on ? "locked" : "released"));
            Changed?.Invoke(on, why);
        }

        /// <summary>Releases the cursor if it is captured (every way out goes through here).</summary>
        public static void Release(string why)
        {
            if (Active) Set(false, why);
        }
    }
}
#endif
