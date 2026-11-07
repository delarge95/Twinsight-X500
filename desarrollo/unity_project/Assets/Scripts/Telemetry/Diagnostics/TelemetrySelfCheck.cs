// Player-only hook: `TwinSight.exe -tel-selfcheck <file.txt>` runs TelemetryLoopbackCheck inside the built player (Mono) and
// writes PASS/FAIL lines, then quits. Optional `-tel-probe <ws-url> [-tel-probe-ms N]` connects to a real server (wrangler dev,
// the SITL bridge) and logs what arrives. Without these flags it does nothing (no object is created, nothing in the scene).
#if UNITY_STANDALONE && !UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace WebGL.Telemetry.Diagnostics
{
    static class TelemetrySelfCheck
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            string outFile = TelemetryEndpoints.CommandLine("-tel-selfcheck");
            string probe = TelemetryEndpoints.CommandLine("-tel-probe");
            if (outFile == null && probe == null) return;
            var sb = new StringBuilder();
            sb.AppendLine("[TelemetrySelfCheck] " + DateTime.Now.ToString("s") + " unity " + Application.unityVersion + " platform " + Application.platform);
            bool ok = true;
            try
            {
                if (outFile != null)
                {
                    var r = TelemetryLoopbackCheck.Run();
                    foreach (var l in r.Lines) sb.AppendLine(l);
                    sb.AppendLine("RESULT " + (r.Ok ? "PASS" : "FAIL") + " " + r.Pass + "/" + (r.Pass + r.Fail));
                    ok = r.Ok;
                }
                if (probe != null) Probe(probe, sb);
            }
            catch (Exception e)
            {
                sb.AppendLine("FAIL exception " + e);
                ok = false;
            }
            Debug.Log(sb.ToString());
            if (outFile != null)
            {
                try { File.WriteAllText(outFile, sb.ToString()); } catch (Exception e) { Debug.LogError("[TelemetrySelfCheck] cannot write " + outFile + ": " + e.Message); }
            }
            Application.Quit(ok ? 0 : 1);
        }

        static void Probe(string url, StringBuilder sb)
        {
            int ms = int.TryParse(TelemetryEndpoints.CommandLine("-tel-probe-ms"), out int v) ? v : 5000;
            var cls = url.Contains(":8765") ? SourceClassification.Sitl : SourceClassification.Replay;
            using (var client = new TelemetryClient(new WebSocketTelemetrySource(url, cls)) { Log = (l, t) => sb.AppendLine("log[" + l + "] " + t) })
            {
                client.Start();
                var t0 = TelemetryTime.NowMs;
                double last = t0;
                while (TelemetryTime.NowMs - t0 < ms)
                {
                    double now = TelemetryTime.NowMs;
                    client.Update(now, (now - last) / 1000.0);
                    last = now;
                    Thread.Sleep(15);
                }
                sb.AppendLine("PROBE " + url + " state=" + client.State + " hello=" + (client.Hello != null ? WireNames.Of(client.Hello.mode) : "none") +
                              " frames=" + client.History.Count + " rxHz=" + client.RxHz.ToString("0.0") + " alerts=" + client.Alerts.Count + " error=" + client.LastError);
                client.Stop();
            }
        }
    }
}
#endif
