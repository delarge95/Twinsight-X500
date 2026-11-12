// WP-09: small desktop-only facade over TelemetryClient for the app (WP-10 drives the scene from it, WP-11 will call it from the UI).
//   DesktopTelemetry.Start(source) / StartLocalReplay() / Stop()   one session at a time; Start replaces the running one.
//   DesktopTelemetry.Client (Displayed, Alerts, Hello, Events, State...), IsRunning, LinkState, events, honest label.
// Not wired to any UI and NOT active by default: it only starts by itself with the command-line flag -tel-replay (testing), and
// -tel-replay-at <seconds> starts the loop at that second of the flight (test aid, shifts the epoch; the default is the shared one).
// Desktop only (#if UNITY_STANDALONE): WebGL keeps its web-only behaviour and compiles without this file.

#if UNITY_STANDALONE
using System;
using UnityEngine;

namespace WebGL.Telemetry
{
    public static class DesktopTelemetry
    {
        public const string ReplayFlag = "-tel-replay";
        public const string ReplayAtFlag = "-tel-replay-at";

        /// <summary>Pumps the client once per frame (before the scene components that read it).</summary>
        [DefaultExecutionOrder(-100)]
        sealed class Pump : MonoBehaviour
        {
            void Update() => Client?.Update(Time.unscaledDeltaTime);
            void OnApplicationQuit() => Stop();
        }

        static GameObject pumpObject;
        static LinkState lastLink = LinkState.Disconnected;

        /// <summary>The running client, or null when stopped.</summary>
        public static TelemetryClient Client { get; private set; }
        public static ITelemetrySource Source => Client?.Source;
        public static bool IsRunning => Client != null;
        public static LinkState LinkState => Client?.State ?? LinkState.Disconnected;
        /// <summary>Why the last Start failed (null when it did not).</summary>
        public static string LastError { get; private set; }
        public static bool IsLocalReplay => Client?.Source != null && Client.Source.IsLocal && Client.Source.Classification == SourceClassification.Replay;

        /// <summary>Raised on the main thread after a session starts (true) or stops (false).</summary>
        public static event Action<bool> RunningChanged;
        /// <summary>Raised on the main thread when the link state of the running session changes.</summary>
        public static event Action<LinkState> LinkStateChanged;

        public static bool Start(ITelemetrySource source)
        {
            if (source == null) { LastError = "no source"; return false; }
            Stop();
            LastError = null;
            EnsurePump();
            Client = new TelemetryClient(source);
            Client.StateChanged += OnState;
            lastLink = Client.State;
            Client.Start();
            RunningChanged?.Invoke(true);
            return true;
        }

        /// <summary>Starts the bundled local replay ("Sombra digital · reproducción local"). False (with LastError) when the TWF is missing.</summary>
        public static bool StartLocalReplay(double startAtSeconds = 0)
        {
            var src = LocalReplayTelemetrySource.LoadBundled(out string error);
            if (src == null) { Stop(); LastError = error; Debug.LogWarning("[Telemetry] local replay unavailable: " + error); return false; }
            if (startAtSeconds > 0)
            {
                // Test aid: shift the clock so that "now" maps to second `startAtSeconds` of the flight (the schedule itself is unchanged).
                double shift = startAtSeconds * 1000 - JsMath.Mod(ReplaySchedule.WallClockMs() - ReplaySchedule.ReplayEpochMs, src.Schedule.PeriodMs * src.Schedule.FrameCount);
                src.WallNowMs = () => ReplaySchedule.WallClockMs() + shift;
            }
            return Start(src);
        }

        public static void Stop()
        {
            var c = Client;
            if (c == null) return;
            Client = null;
            c.StateChanged -= OnState;
            c.Dispose();
            lastLink = LinkState.Disconnected;
            RunningChanged?.Invoke(false);
        }

        static void OnState(LinkState s)
        {
            if (s == lastLink) return;
            lastLink = s;
            LinkStateChanged?.Invoke(s);
        }

        /// <summary>
        /// The honest badge text of the running session: the local replay is exactly the approved "Sombra digital · reproducción local"
        /// (labels.json classification.replay.local), a remote replay is "Sombra digital", SITL is "Gemelo digital SITL". Never "en vivo".
        /// Null when stopped.
        /// </summary>
        public static string BadgeText(string lang = "es")
        {
            var c = Client;
            if (c == null) return null;
            var labels = HonestyLabels.Load();
            if (c.Source != null && c.Source.IsLocal && c.Classification == SourceClassification.Replay && labels.TryLocalReplayText(lang, out string t)) return t;
            return labels.ClassificationTitle(c.Classification, lang);
        }

        static void EnsurePump()
        {
            if (pumpObject != null) return;
            pumpObject = new GameObject("_DesktopTelemetry") { hideFlags = HideFlags.HideInHierarchy };
            UnityEngine.Object.DontDestroyOnLoad(pumpObject);
            pumpObject.AddComponent<Pump>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStartFromCommandLine()
        {
            bool replay = false;
            double at = 0;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], ReplayFlag, StringComparison.OrdinalIgnoreCase)) replay = true;
                else if (string.Equals(args[i], ReplayAtFlag, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    double.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out at);
            }
            if (!replay) return;
            bool ok = StartLocalReplay(at);
            Debug.Log("[Telemetry] " + ReplayFlag + ": local replay " + (ok ? "started (" + BadgeText("es") + ")" : "NOT started: " + LastError));
        }
    }
}
#endif
