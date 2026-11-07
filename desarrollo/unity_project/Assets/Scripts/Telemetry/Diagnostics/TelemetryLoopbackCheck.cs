// End-to-end check of the desktop telemetry link against LoopbackWebSocketServer on 127.0.0.1:
// connect -> hello + frames -> Live -> stale after 500 ms without data -> command + ack -> invalid message ignored ->
// server outage (Error, failures counted) -> automatic reconnection with a new hello -> Live -> Stop (Disconnected).
// Blocking (pumps TelemetryClient.Update itself); used by the EditMode test and by the player self-check
// (`TwinSight.exe -tel-selfcheck <file>`, see TelemetrySelfCheck). Desktop/Editor only.
#if UNITY_STANDALONE || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace WebGL.Telemetry.Diagnostics
{
    public static class TelemetryLoopbackCheck
    {
        // Copies of contracts/fixtures/hello.sitl.json (trimmed source) and frames.sitl.json.
        public const string HelloSitl =
            "{\"type\":\"hello\",\"mode\":\"sitl\",\"rotors\":[{\"motor\":1,\"px\":0.174,\"py\":0.174,\"km\":0.05},{\"motor\":2,\"px\":-0.174,\"py\":-0.174,\"km\":0.05}," +
            "{\"motor\":3,\"px\":0.174,\"py\":-0.174,\"km\":-0.05},{\"motor\":4,\"px\":-0.174,\"py\":0.174,\"km\":-0.05}],\"rateHz\":30," +
            "\"provenance\":{\"rpm\":\"command\",\"motorTemp\":\"estimated\",\"batteryTemp\":\"estimated\",\"fcTemp\":\"measured\",\"batteryCurrent\":\"estimated\"}," +
            "\"batteryCells\":4,\"ambientC\":20,\"source\":{\"label\":\"PX4 SITL local (gz_x500) vía pymavlink\"}," +
            "\"commands\":[\"takeoff\",\"set_altitude\",\"land\",\"fly_to\",\"yaw\",\"orbit\",\"rtl\",\"hold\",\"motor_off\",\"restore\",\"pilot\",\"manual\"],\"serverTime\":1792195320000}";

        public static string FrameSitl(double ts, double north) =>
            "{\"type\":\"frames\",\"frames\":[{\"ts\":" + Json.TelemetryJson.Num(ts) + ",\"lt\":312.4,\"seg\":0,\"q\":[0.966,0,0,0.259],\"p\":[" + Json.TelemetryJson.Num(north) +
            ",-0.4,-5.02],\"v\":[0,0.01,-0.02],\"u\":[0.62,0.63,0.61,0.62],\"rpm\":[5921,6016,5825,5921],\"bat\":{\"v\":15.9,\"i\":12.22,\"rem\":0.93}," +
            "\"temp\":[31.2,31.4,30.9,31.1,24.6,25.3],\"vib\":1.192,\"armed\":true,\"landed\":false,\"nav\":4}]}";

        public sealed class Result
        {
            public readonly List<string> Lines = new List<string>();
            public int Pass, Fail;
            public bool Ok => Fail == 0 && Pass > 0;

            public void Check(bool ok, string what)
            {
                if (ok) Pass++; else Fail++;
                Lines.Add((ok ? "PASS " : "FAIL ") + what);
            }

            public void Info(string what) => Lines.Add("INFO " + what);
        }

        public static Result Run(int stepTimeoutMs = 8000)
        {
            var res = new Result();
            res.Info("runtime: " + Environment.Version + " / " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
            using (var server = new LoopbackWebSocketServer(HelloSitl, FrameSitl(1792195330000, 2.1)))
            {
                var source = new WebSocketTelemetrySource(server.Url, SourceClassification.Sitl);
                var client = new TelemetryClient(source) { Log = (l, t) => res.Info("log[" + l + "] " + t) };
                var states = new List<LinkState>();
                client.StateChanged += s => states.Add(s);
                var sw = Stopwatch.StartNew();
                double last = TelemetryTime.NowMs;

                bool Pump(Func<bool> cond, int timeout)
                {
                    var t0 = Stopwatch.StartNew();
                    while (t0.ElapsedMilliseconds < timeout)
                    {
                        double now = TelemetryTime.NowMs;
                        client.Update(now, (now - last) / 1000.0);
                        last = now;
                        if (cond()) return true;
                        Thread.Sleep(10);
                    }
                    return false;
                }

                try
                {
                    client.Start();
                    res.Check(Pump(() => client.Hello != null && client.HasFrame, stepTimeoutMs), "connect, hello and first frame (" + sw.ElapsedMilliseconds + " ms) url=" + server.Url);
                    res.Check(client.Hello != null && client.Hello.mode == TelemetryMode.Sitl && client.Hello.rotors.Length == 4, "hello parsed (mode sitl, 4 rotors)");
                    res.Check(client.State == LinkState.Live, "state Live after data (" + client.State + ")");
                    res.Check(Pump(() => client.State == LinkState.Stale, 2000), "state Stale after " + TelemetryClient.StaleAfterMs + " ms without frames");

                    res.Check(client.Send(ClientMessage.Takeoff(5), out var why), "send takeoff accepted by the client" + (why != null ? " (" + why + ")" : ""));
                    res.Check(Pump(() => client.LastAck != null && client.LastAck.command == "takeoff" && client.LastAck.ok, stepTimeoutMs), "ack{takeoff, ok} received");
                    bool serverGot = false;
                    foreach (var r in server.Received) if (r.Contains("\"takeoff\"") && r.Contains("\"altitude\":5")) serverGot = true;
                    res.Check(serverGot, "server received {\"type\":\"command\",\"name\":\"takeoff\",\"altitude\":5}");
                    res.Check(!client.Send(ClientMessage.Inject(FaultKind.Vibration), out why), "inject refused locally: SITL hello does not list it (" + why + ")");

                    server.Broadcast("{\"type\":\"pong\"}");
                    for (int i = 1; i <= 10; i++) server.Broadcast(FrameSitl(1792195330000 + i * 33, 2.1 + i * 0.1));
                    res.Check(Pump(() => client.History.Count >= 11, stepTimeoutMs), "10 more frames buffered (" + client.History.Count + ")");
                    res.Check(source.Rejected >= 1, "unknown message type ignored without dropping the link (rejected=" + source.Rejected + ")");
                    res.Check(client.State == LinkState.Live, "Live again with fresh frames (" + client.State + ")");

                    server.Accepting = false;
                    server.DropClients();
                    res.Check(Pump(() => client.State == LinkState.Error, stepTimeoutMs), "outage of an established link shows Error (failures=" + client.Failures + ", error=" + client.LastError + ")");
                    Pump(() => client.Failures >= 2, 3000);
                    res.Check(client.Failures >= 2, "retries counted while the server refuses (" + client.Failures + ")");
                    server.Accepting = true;
                    res.Check(Pump(() => server.Connections >= 2 && client.State == LinkState.Live && client.Hello != null, 20000), "automatic reconnection with a new hello -> Live (connections=" + server.Connections + ")");

                    client.Stop();
                    res.Check(Pump(() => client.State == LinkState.Disconnected, 3000), "Stop -> Disconnected");
                    res.Info("states: " + string.Join(" > ", states));
                }
                catch (Exception e)
                {
                    res.Check(false, "exception: " + e);
                }
                finally
                {
                    client.Dispose();
                }
            }
            return res;
        }
    }
}
#endif
