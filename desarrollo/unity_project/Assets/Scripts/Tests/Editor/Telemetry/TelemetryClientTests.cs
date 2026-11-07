// TelemetryClient behaviour with an in-memory source, and the real ClientWebSocket against a loopback server (Editor Mono).
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WebGL.Telemetry.Diagnostics;

namespace WebGL.Telemetry.Tests
{
    public class TelemetryClientTests
    {
        static TelemetryClient NewClient(ManualTelemetrySource s) => new TelemetryClient(s) { Log = null };

        [Test]
        public void Replay_Session_Interpolates_250ms_Behind_And_Goes_Stale()
        {
            var src = new ManualTelemetrySource(SourceClassification.Replay);
            var c = NewClient(src);
            Assert.AreEqual(LinkState.Disconnected, c.State);
            src.Start();
            c.Update(0, 0);
            Assert.AreEqual(LinkState.Live, c.State, "open, nothing late yet");
            Assert.IsTrue(src.PushJson(C.Text("fixtures/hello.replay.json"), out _));
            var frames = C.LoopwrapFrames();
            // Frames arrive at local 1000 ms with server ts frames[1].ts.
            src.Push(new FramesMessage { frames = new[] { frames[0], frames[1] }, ReceivedAtMs = 1000 });
            c.Update(1000, 0.016);
            Assert.IsNotNull(c.Hello);
            Assert.AreEqual(SourceClassification.Replay, c.Classification);
            // offset = 1000 - ts1; render time at local 1000+225 = ts1 - 25 -> between f0 (ts1-50) and f1: alpha 0.5.
            c.Update(1225, 0.016);
            Assert.IsNotNull(c.Displayed);
            Assert.AreEqual((frames[0].ts + frames[1].ts) / 2, c.Displayed.ts, 1e-6);
            Assert.AreEqual(LinkState.Live, c.State);
            c.Update(1000 + TelemetryClient.StaleAfterMs + 1, 0.016);
            Assert.AreEqual(LinkState.Stale, c.State, "no frame for more than 500 ms");
            // A new hello is a new session.
            Assert.IsTrue(src.PushJson(C.Text("fixtures/hello.replay.json"), out _));
            c.Update(1600, 0.016);
            Assert.AreEqual(0, c.Buffer.Size);
            Assert.IsNull(c.Displayed);
            Assert.IsFalse(c.Clock.Synced);
        }

        [Test]
        public void Sitl_Session_Ground_Alerts_Acks_Events_Faults()
        {
            var src = new ManualTelemetrySource(SourceClassification.Sitl, isLocal: false);
            var c = NewClient(src);
            src.Start();
            Assert.IsTrue(src.PushJson(C.Text("fixtures/hello.sitl.json"), out _));
            Assert.IsTrue(src.PushJson(C.Text("fixtures/frames.sitl.landed.json"), out _));
            c.Update(10, 0.016);
            Assert.IsNotNull(c.Displayed);
            Assert.AreEqual(0, c.Displayed.p[2], 1e-12, "landed: ground reference pins the drawn height to 0");

            // Motor 3 stopped while flying -> fresh critical alert once.
            var f = C.Frame(C.O(C.A(C.Golden("alerts"), "cases")[1])["frame"]); // "motor 3 stopped while flying"
            f.ts = 1792195340000; f.seg = 0;
            src.Push(new FramesMessage { frames = new[] { f } });
            Alert raised = null;
            c.AlertRaised += a => raised = a;
            c.Update(20, 1.0); // SITL smoothing: large dt -> at the target
            Assert.IsNotNull(raised);
            Assert.AreEqual("motor3:out", raised.id);
            Assert.AreEqual(1, c.AlertSummary.count);
            Assert.AreEqual(AlertSeverity.Critical, c.AlertSummary.worst);
            c.Update(30, 0.016);
            Assert.IsNull(c.FreshAlert, "announced once");

            Assert.IsTrue(src.PushJson(C.Text("fixtures/ack.sitl.failed.json"), out _));
            Assert.IsTrue(src.PushJson(C.Text("fixtures/event.warning.json"), out _));
            Assert.IsTrue(src.PushJson(C.Text("fixtures/faults.active.json"), out _));
            c.Update(40, 0.016);
            Assert.IsNotNull(c.LastAck);
            Assert.AreEqual(2, c.Faults.Count);
            Assert.AreEqual(2, c.Events.Count, "negative ack with detail becomes a warning event, plus the event itself");

            Assert.IsTrue(c.Send(ClientMessage.Takeoff(5), out _));
            Assert.IsFalse(c.Send(ClientMessage.Clear(), out var why), "SITL hello does not accept clear: " + why);
            Assert.IsFalse(c.Send(ClientMessage.MotorOff(7), out _), "invalid motor");
            Assert.AreEqual(1, src.Sent.Count);
        }

        [Test]
        public void Reconnect_States_Map_To_Chip()
        {
            var src = new ManualTelemetrySource(SourceClassification.Replay, isLocal: false);
            var c = NewClient(src);
            src.SetState(TransportState.Connecting, 0, false); c.Update(0, 0);
            Assert.AreEqual(LinkState.Connecting, c.State);
            src.SetState(TransportState.Connecting, 3, false); c.Update(1, 0);
            Assert.AreEqual(LinkState.Connecting, c.State, "3 quiet failures");
            src.SetState(TransportState.Closed, 4, false); c.Update(2, 0);
            Assert.AreEqual(LinkState.Error, c.State);
            Assert.AreEqual(4, c.Failures);
            src.SetState(TransportState.Idle); c.Update(3, 0);
            Assert.AreEqual(LinkState.Disconnected, c.State);
        }

        [Test]
        public void RingBuffer_Keeps_Newest()
        {
            var r = new RingBuffer<int>(3);
            for (int i = 1; i <= 5; i++) r.Add(i);
            Assert.AreEqual(3, r.Count);
            CollectionAssert.AreEqual(new[] { 3, 4, 5 }, Enumerable.Range(0, r.Count).Select(i => r[i]).ToArray());
            CollectionAssert.AreEqual(new[] { 5, 4 }, r.NewestFirst(2).ToArray());
        }

        /// <summary>Real System.Net.WebSockets.ClientWebSocket (Editor Mono) against LoopbackWebSocketServer on 127.0.0.1.</summary>
        [Test, Timeout(60000)]
        public void ClientWebSocket_Loopback_EndToEnd()
        {
            var r = TelemetryLoopbackCheck.Run();
            Debug.Log("[contract] loopback ClientWebSocket (Editor):\n" + string.Join("\n", r.Lines));
            Cases.Summary["loopback.editor"] = r.Pass + "/" + (r.Pass + r.Fail);
            Assert.IsTrue(r.Ok, string.Join("\n", r.Lines));
        }
    }
}
