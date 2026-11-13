#if UNITY_STANDALONE
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Verification aid (PLAN_PC 28.4): the capture path of the in-player probes. CAUSE of the black PNGs of the earlier rounds (reproduced): when the
    /// desktop session does not present (a disconnected or locked session, a monitor that is off, or a window the OS cannot show: the player log says
    /// «Failed to find a valid fullscreen resolution for exclusiveFullscreen 1920x1080» and falls back to a window taller than the work area) the back
    /// buffer is never filled and Texture2D.ReadPixels of the screen returns zeros: luma 0.0 at 1080p, 720p and in every retry. The game itself keeps
    /// rendering (frames, flights and the UI run normally), so the fix is not to read the back buffer: <see cref="Grab"/> first tries the back buffer
    /// (the true pixels) and, when it is black, renders the camera and the UI panel into two RenderTextures and composites them on the CPU
    /// (UI Toolkit draws premultiplied: out = ui + scene * (1 - ui.a)). The result is the same frame, independent of the desktop.
    /// </summary>
    internal static class DesktopScreenGrab
    {
        /// <summary>How the last grab was made: "backbuffer" or "offscreen".</summary>
        public static string LastMethod { get; private set; } = "none";
        public static float LastLuma { get; private set; }

        public static float MeanLuma(Texture2D t)
        {
            Color32[] px = t.GetPixels32();
            double sum = 0;
            int n = 0;
            for (int i = 0; i < px.Length; i += 37) { sum += 0.2126 * px[i].r + 0.7152 * px[i].g + 0.0722 * px[i].b; n++; }
            return n > 0 ? (float)(sum / n) : 0f;
        }

        /// <summary>Call from a coroutine after the frame is ready (it waits for the end of the frame itself). `done` receives a texture the caller owns.</summary>
        public static IEnumerator Grab(Action<Texture2D> done)
        {
            yield return new WaitForEndOfFrame();
            var t = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            t.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            t.Apply();
            float luma = MeanLuma(t);
            if (luma >= 3f)
            {
                LastMethod = "backbuffer";
                LastLuma = luma;
                done(t);
                yield break;
            }

            UnityEngine.Object.Destroy(t);
            Texture2D off = null;
            yield return Offscreen(r => off = r);
            LastMethod = "offscreen";
            LastLuma = off != null ? MeanLuma(off) : 0f;
            done(off);
        }

        private static IEnumerator Offscreen(Action<Texture2D> done)
        {
            int w = Screen.width, h = Screen.height;
            Camera cam = Camera.main;
            UIDocument doc = UnityEngine.Object.FindFirstObjectByType<UIDocument>();
            PanelSettings ps = doc != null ? doc.panelSettings : null;
            if (cam == null || ps == null) { done(new Texture2D(w, h, TextureFormat.RGB24, false)); yield break; }

            var rtScene = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "GrabScene" };
            var rtUi = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "GrabUI" };
            RenderTexture prevCam = cam.targetTexture;
            RenderTexture prevPanel = ps.targetTexture;
            bool prevClear = ps.clearColor;
            Color prevClearColor = ps.colorClearValue;
            cam.targetTexture = rtScene;
            ps.targetTexture = rtUi;
            ps.clearColor = true;
            ps.colorClearValue = new Color(0, 0, 0, 0);
            yield return null;
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return new WaitForEndOfFrame();

            Texture2D scene = Read(rtScene), ui = Read(rtUi);
            cam.targetTexture = prevCam;
            ps.targetTexture = prevPanel;
            ps.clearColor = prevClear;
            ps.colorClearValue = prevClearColor;
            rtScene.Release(); rtUi.Release();
            UnityEngine.Object.Destroy(rtScene); UnityEngine.Object.Destroy(rtUi);

            Color32[] s = scene.GetPixels32(), u = ui.GetPixels32();
            var o = new Color32[s.Length];
            for (int i = 0; i < s.Length; i++)
            {
                float inv = 1f - u[i].a / 255f;
                o[i] = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(u[i].r + s[i].r * inv), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(u[i].g + s[i].g * inv), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(u[i].b + s[i].b * inv), 0, 255), 255);
            }
            UnityEngine.Object.Destroy(scene); UnityEngine.Object.Destroy(ui);
            var result = new Texture2D(w, h, TextureFormat.RGB24, false);
            result.SetPixels32(o);
            result.Apply();
            // the panel gets its screen target back: give it a few frames before anything else (a click right after the grab was lost otherwise)
            for (int i = 0; i < 4; i++) yield return null;
            done(result);
        }

        private static Texture2D Read(RenderTexture rt)
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            return t;
        }
    }
}
#endif
