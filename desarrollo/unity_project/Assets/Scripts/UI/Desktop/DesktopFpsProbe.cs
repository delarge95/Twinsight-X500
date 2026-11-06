#if UNITY_STANDALONE
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Test aid (-desk-fps): logs average frame time and managed allocation per frame every 5 s while it walks through
    /// hero -> viewer at idle -> tutorial open (animated illustration), to prove the animations are cheap and allocation-free at idle.
    /// </summary>
    internal sealed class DesktopFpsProbe : MonoBehaviour
    {
        private float _acc;
        private int _frames;
        private long _bytes;
        private long _last = -1;
        private float _maxDt;
        private string _phase = "hero";

        private void Start() { StartCoroutine(Walk()); }

        private IEnumerator Walk()
        {
            PlayerPrefs.SetInt("Onboarding_Seen", 1);
            yield return new WaitForSecondsRealtime(20f);
            _phase = "viewer idle";
            UIManager.Instance.PrepareViewerForDeepLink();
            yield return new WaitForSecondsRealtime(30f);
            _phase = "tutorial animado";
            FieldInfo f = typeof(UIManager).GetField("_onboardingController", BindingFlags.NonPublic | BindingFlags.Instance);
            object onboarding = f != null ? f.GetValue(UIManager.Instance) : null;
            if (onboarding != null) onboarding.GetType().GetMethod("Show").Invoke(onboarding, null);
            yield return new WaitForSecondsRealtime(20f);
            Application.Quit();
        }

        private void Update()
        {
            long now = GC.GetTotalMemory(false);
            if (_last >= 0 && now > _last) _bytes += now - _last;
            _last = now;
            float dt = Time.unscaledDeltaTime;
            _acc += dt;
            _frames++;
            if (dt > _maxDt) _maxDt = dt;
            if (_acc >= 5f)
            {
                Debug.Log("[DesktopFps] " + _phase + ": " + (_frames / _acc).ToString("F1") + " fps, " + (1000f * _acc / _frames).ToString("F2") + " ms avg, "
                          + (1000f * _maxDt).ToString("F1") + " ms max, " + (_bytes / (float)_frames).ToString("F0") + " B/frame managed alloc (GC.GetTotalMemory deltas), gc0=" + GC.CollectionCount(0) + ", reduce, reduce=" + DesktopMotion.Reduce);
                _acc = 0f; _frames = 0; _bytes = 0; _maxDt = 0f;
                _last = GC.GetTotalMemory(false);
            }
        }
    }
}
#endif
