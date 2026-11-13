#if UNITY_STANDALONE
using System;
using System.Collections.Generic;
using UnityEngine;

namespace WebGL.Core.Utils
{
    /// <summary>
    /// Registro de rendimiento SOLO para builds de PC (Standalone) y SOLO si la app se
    /// lanza con el argumento <c>-perflog</c>. Cada 10 s escribe en el log del Player
    /// (Player.log o el archivo de <c>-logFile</c>) la media, el minimo y el percentil 1
    /// de los FPS de esa ventana. No hace nada en WebGL ni en el Editor.
    /// </summary>
    public sealed class PerfLogger : MonoBehaviour
    {
        private const float WindowSeconds = 10f;
        private const string Arg = "-perflog";

        private readonly List<float> _frameTimes = new List<float>(2048);
        private float _windowElapsed;
        private int _windowIndex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
#if UNITY_EDITOR
            return;
#else
            bool enabled = false;
            foreach (string a in Environment.GetCommandLineArgs())
            {
                if (string.Equals(a, Arg, StringComparison.OrdinalIgnoreCase)) { enabled = true; break; }
            }
            if (!enabled) return;

            var go = new GameObject("_PerfLogger");
            DontDestroyOnLoad(go);
            var pl = go.AddComponent<PerfLogger>();
            foreach (string a in Environment.GetCommandLineArgs())
            {
                // Fase 3A: -perf-viewer abre el visor a los 8 s para medir con el dron en pantalla.
                if (string.Equals(a, "-perf-viewer", StringComparison.OrdinalIgnoreCase)) pl.StartCoroutine(pl.OpenViewer());
            }
            Debug.Log("[PerfLogger] activo (-perflog). GPU=" + SystemInfo.graphicsDeviceName +
                      " | API=" + SystemInfo.graphicsDeviceType + " | CPU=" + SystemInfo.processorType +
                      " | Res=" + Screen.width + "x" + Screen.height + " | Calidad=" + QualitySettings.names[QualitySettings.GetQualityLevel()] +
                      " | vSync=" + QualitySettings.vSyncCount);
#endif
        }

        private System.Collections.IEnumerator OpenViewer()
        {
            yield return new WaitForSecondsRealtime(8f);
            // Reflection: Core no referencia el ensamblado UI.
            Type t = Type.GetType("WebGL.UI.UIManager, UI");
            object inst = t != null ? UnityEngine.Object.FindFirstObjectByType(t) : null;
            t?.GetMethod("PrepareViewerForDeepLink")?.Invoke(inst, null);
            string shot = null;
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == "-perf-shot") shot = a[i + 1];
            if (shot != null)
            {
                // Captura durante la medicion (confirma que el visor esta abierto).
                yield return new WaitForSecondsRealtime(22f);
                yield return new WaitForEndOfFrame();
                var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                tex.Apply();
                System.IO.File.WriteAllBytes(shot, ImageConversion.EncodeToPNG(tex));
                Destroy(tex);
            }
            Debug.Log("[PerfLogger] visor abierto: tipo=" + (t != null) + " instancia=" + (inst != null));
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;
            _frameTimes.Add(dt);
            _windowElapsed += dt;
            if (_windowElapsed >= WindowSeconds) Flush();
        }

        private void OnApplicationQuit()
        {
            if (_frameTimes.Count > 30) Flush();
        }

        private void Flush()
        {
            int n = _frameTimes.Count;
            if (n == 0) return;
            _frameTimes.Sort(); // ascendente: el final son los frames mas lentos
            float worst = _frameTimes[n - 1];
            float p99 = _frameTimes[Mathf.Min(n - 1, Mathf.CeilToInt(n * 0.99f) - 1)];
            float avgFps = n / _windowElapsed;
            float minFps = 1f / worst;
            float p1Fps = 1f / p99;
            _windowIndex++;
            Debug.Log($"[PerfLogger] ventana {_windowIndex} ({_windowElapsed:F1}s, {n} frames): t={Time.realtimeSinceStartup:F0}s FPS media={avgFps:F1} min={minFps:F1} p1={p1Fps:F1} vsync={QualitySettings.vSyncCount} target={Application.targetFrameRate}");
            _frameTimes.Clear();
            _windowElapsed = 0f;
        }
    }
}
#endif
