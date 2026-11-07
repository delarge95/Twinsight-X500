// OPTIONAL host component for WP-10/WP-11: owns a TelemetryClient and pumps it from Update. It is NOT placed in any scene
// and does nothing until Connect() is called (or autoConnect is ticked by whoever adds it). On WebGL it only logs that the
// desktop telemetry link is unavailable (the native web app owns telemetry there).

using UnityEngine;

namespace WebGL.Telemetry
{
    [DisallowMultipleComponent]
    [AddComponentMenu("TwinSight/Telemetry Bootstrap (optional)")]
    public sealed class TelemetryBootstrap : MonoBehaviour
    {
        [Tooltip("Replay = Sombra digital (24/7 worker); Sitl = Gemelo digital SITL (local bridge).")]
        public SourceClassification source = SourceClassification.Sitl;
        [Tooltip("Empty = default (-replay-url / -sitl-url on the command line, else wrangler dev / ws://localhost:8765).")]
        public string url = "";
        [Tooltip("Off by default: nothing connects unless this is ticked or Connect() is called.")]
        public bool autoConnect;

        public TelemetryClient Client { get; private set; }

        void OnEnable()
        {
            if (autoConnect) Connect();
        }

        /// <summary>Creates the client for the configured source and starts it. Returns false where there is no link (WebGL, Inspector).</summary>
        public bool Connect()
        {
            Disconnect();
            if (source == SourceClassification.Inspector) return false; // the Inspector has no telemetry
#if UNITY_STANDALONE || UNITY_EDITOR
            string u = string.IsNullOrEmpty(url) ? (source == SourceClassification.Sitl ? TelemetryEndpoints.Sitl : TelemetryEndpoints.Replay) : url;
            Client = new TelemetryClient(new WebSocketTelemetrySource(u, source));
            Client.Start();
            return true;
#else
            Debug.Log("[Telemetry] the desktop telemetry link is not available on this platform");
            return false;
#endif
        }

        /// <summary>Uses any source (e.g. WP-09's local replay) instead of the WebSocket.</summary>
        public void Use(ITelemetrySource s)
        {
            Disconnect();
            Client = new TelemetryClient(s);
            Client.Start();
        }

        public void Disconnect()
        {
            Client?.Dispose();
            Client = null;
        }

        void Update()
        {
            Client?.Update(Time.unscaledDeltaTime);
        }

        void OnDisable() => Disconnect();
    }
}
