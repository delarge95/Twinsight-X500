#if UNITY_STANDALONE
using System;
using System.Collections.Generic;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;
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

        // F-6: asignacion gestionada por fotograma (contador "GC Allocated In Frame", disponible tambien en builds
        // de release) y tiempos de CPU/GPU del fotograma (FrameTimingManager). Con -perf-markers (pensado para una
        // build de desarrollo) se suman ademas TODOS los marcadores del hilo principal y se listan los mas caros.
        private ProfilerRecorder _gcAlloc;
        private long _gcBytes, _heapBytes, _lastHeap = -1;
        private readonly FrameTiming[] _ft = new FrameTiming[1];
        private double _ftCpu, _ftMain, _ftPresent, _ftRender, _ftGpu;
        private int _ftN;
        private bool _markers;
        private sealed class Rec { public string Name; public ProfilerRecorder R; public double Sum; }
        private readonly List<Rec> _recs = new List<Rec>(4096);
        private readonly HashSet<string> _recNames = new HashSet<string>();

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
                if (string.Equals(a, "-perf-markers", StringComparison.OrdinalIgnoreCase)) pl._markers = true;
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

        private void OnEnable()
        {
            _gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        }

        private void OnDisable()
        {
            _gcAlloc.Dispose();
            for (int i = 0; i < _recs.Count; i++) _recs[i].R.Dispose();
            _recs.Clear();
        }

        /// <summary>Anade un grabador por cada marcador de tiempo que exista ya (los marcadores se crean al usarse).</summary>
        private void CollectMarkers()
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (ProfilerRecorderHandle h in handles)
            {
                ProfilerRecorderDescription d = ProfilerRecorderHandle.GetDescription(h);
                if (d.UnitType != ProfilerMarkerDataUnit.TimeNanoseconds || !WantMarker(d.Name) || !_recNames.Add(d.Name)) continue;
                var r = new ProfilerRecorder(h, 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
                r.Start();
                _recs.Add(new Rec { Name = d.Name, R = r });
            }
        }

        // Solo los marcadores utiles para repartir el fotograma (grabarlos todos frena el jugador).
        private static readonly string[] MarkerPrefixes =
        {
            "PlayerLoop", "Initialization.", "EarlyUpdate.", "FixedUpdate.", "PreUpdate.", "Update.", "PreLateUpdate.", "PostLateUpdate.",
            "UIElements.", "MainPanelSettings.", "UIR.", "Gfx.", "Camera.", "Inl_", "RenderPipeline", "Universal", "Physics.", "GC.",
            "BehaviourUpdate", "LateBehaviourUpdate", "CoroutinesDelayedCalls", "GenerateVisualContent", "GenerateTextMesh", "ATGTextJob.",
            "VectorAPI", "WaitForTargetFPS", "Semaphore.WaitForSignal", "Culling", "Shadows.", "Render", "Draw", "Animators.", "Director.",
        };

        private static bool WantMarker(string name)
        {
            if (name.IndexOf("Update()", StringComparison.Ordinal) >= 0 || name.IndexOf("[Coroutine", StringComparison.Ordinal) >= 0) return true;
            for (int i = 0; i < MarkerPrefixes.Length; i++) if (name.StartsWith(MarkerPrefixes[i], StringComparison.Ordinal)) return true;
            return false;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;
            // El contador "GC Allocated In Frame" solo da valores en builds de desarrollo; en release se usa el crecimiento del monton
            // (GC.GetTotalMemory, igual que -desk-fps), que no asigna.
            long heapNow = GC.GetTotalMemory(false);
            if (_lastHeap >= 0 && heapNow > _lastHeap) _heapBytes += heapNow - _lastHeap;
            _lastHeap = heapNow;
            if (_gcAlloc.Valid) _gcBytes += _gcAlloc.LastValue;
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _ft) > 0)
            {
                _ftCpu += _ft[0].cpuFrameTime; _ftMain += _ft[0].cpuMainThreadFrameTime; _ftPresent += _ft[0].cpuMainThreadPresentWaitTime;
                _ftRender += _ft[0].cpuRenderThreadFrameTime; _ftGpu += _ft[0].gpuFrameTime; _ftN++;
            }
            if (_markers)
            {
                if (_recs.Count == 0 && Time.realtimeSinceStartup > 14f) CollectMarkers();
                for (int i = 0; i < _recs.Count; i++) _recs[i].Sum += _recs[i].R.LastValue;
            }
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
            int fn = Mathf.Max(1, _ftN);
            Debug.Log($"[PerfLogger] ventana {_windowIndex}: asignacion gestionada={(_heapBytes / (double)n):F0} B/fotograma por crecimiento del monton ({(_heapBytes / 1048576.0 / _windowElapsed):F2} MB/s), contador={(_gcBytes / (double)n):F0} B/fotograma | ms cpu={_ftCpu / fn:F2} hiloPrincipal={_ftMain / fn:F2} esperaPresent={_ftPresent / fn:F2} hiloRender={_ftRender / fn:F2} gpu={_ftGpu / fn:F2} (muestras {_ftN})");
            _gcBytes = 0; _heapBytes = 0; _ftCpu = _ftMain = _ftPresent = _ftRender = _ftGpu = 0; _ftN = 0;
            if (_markers)
            {
                _recs.Sort((a, b) => b.Sum.CompareTo(a.Sum));
                var sb = new System.Text.StringBuilder("[PerfLogger] marcadores (ms/fotograma, hilo principal): ");
                for (int i = 0; i < _recs.Count && i < 60; i++) sb.Append(_recs[i].Name).Append('=').Append((_recs[i].Sum / n / 1e6).ToString("F2")).Append("; ");
                Debug.Log(sb.ToString());
                for (int i = 0; i < _recs.Count; i++) _recs[i].Sum = 0;
                CollectMarkers();
            }
            _frameTimes.Clear();
            _windowElapsed = 0f;
        }
    }
}
#endif
