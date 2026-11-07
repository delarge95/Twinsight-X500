// Remote source: System.Net.WebSockets.ClientWebSocket on a background task (desktop only).
// The receive loop parses on the background thread (tolerant parser) and posts typed messages; TelemetryClient.Update drains
// them on the main thread. Reconnection: ReconnectPolicy (0.5 s doubling to 15 s, 3 quiet failures), connect timeout 5 s.
// Every await uses ConfigureAwait(false): nothing runs on Unity's SynchronizationContext, so the loop also works while the
// main thread is blocked (EditMode tests) and never touches Unity APIs.
// Compiled only for Standalone and the Editor: the WebGL player has no sockets (the web app owns telemetry there).
#if UNITY_STANDALONE || UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebGL.Telemetry
{
    public sealed class WebSocketTelemetrySource : QueuedTelemetrySource
    {
        public const int ConnectTimeoutMs = 5000;
        public const int MaxMessageBytes = 4 * 1024 * 1024;

        readonly Uri uri;
        readonly SourceClassification classification;
        CancellationTokenSource cts;
        Task loop;
        volatile ClientWebSocket current;
        readonly ConcurrentQueue<string> outbox = new ConcurrentQueue<string>();
        readonly SemaphoreSlim outboxSignal = new SemaphoreSlim(0);
        int failures;
        bool everOpened;
        int rejectedLogged;

        /// <summary>Parsed-but-rejected server messages (schema violations), for diagnostics.</summary>
        public int Rejected;
        /// <summary>Messages parsed and posted.</summary>
        public int Received;

        public WebSocketTelemetrySource(string url, SourceClassification classification)
        {
            if (classification == SourceClassification.Inspector) throw new ArgumentException("the Inspector has no telemetry link");
            uri = new Uri(url);
            if (uri.Scheme != "ws" && uri.Scheme != "wss") throw new ArgumentException("telemetry URL must be ws:// or wss://: " + url);
            this.classification = classification;
        }

        public override SourceClassification Classification => classification;
        public override bool IsLocal => false;
        public override string Endpoint => uri.ToString();

        public override void Start()
        {
            if (loop != null && !loop.IsCompleted) return;
            cts = new CancellationTokenSource();
            var token = cts.Token;
            failures = 0;
            loop = Task.Run(() => RunAsync(token));
        }

        public override void Stop()
        {
            var c = cts;
            cts = null;
            if (c == null) return;
            c.Cancel();
            var ws = current;
            try { ws?.Abort(); } catch { /* already closed */ }
            PostState(TransportState.Idle, failures, everOpened);
        }

        public override bool Send(ClientMessage msg, out string reason)
        {
            reason = msg.Validate();
            if (reason != null) return false;
            string json = msg.ToJson();
            if (json.Length > TelemetryProtocol.MaxClientMessageChars) { reason = "message longer than 512 characters"; return false; }
            var ws = current;
            if (ws == null || ws.State != WebSocketState.Open) { reason = "link not open"; return false; }
            outbox.Enqueue(json);
            outboxSignal.Release();
            return true;
        }

        async Task RunAsync(CancellationToken stop)
        {
            while (!stop.IsCancellationRequested)
            {
                // A retry keeps the state it already shows ("connecting" while quiet, "closed" in a real outage): no flicker per attempt.
                if (State != TransportState.Closed || failures == 0) PostState(TransportState.Connecting, failures, everOpened);
                string error = null;
                using (var ws = new ClientWebSocket())
                {
                    try
                    {
                        ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                        using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(stop))
                        {
                            connectCts.CancelAfter(ConnectTimeoutMs);
                            await ws.ConnectAsync(uri, connectCts.Token).ConfigureAwait(false);
                        }
                        failures = 0;
                        everOpened = true;
                        current = ws;
                        while (outbox.TryDequeue(out _)) { } // never send stale commands from a previous link
                        PostState(TransportState.Open, 0, true);
                        using (var linkCts = CancellationTokenSource.CreateLinkedTokenSource(stop))
                        {
                            var sender = SendLoopAsync(ws, linkCts.Token);
                            error = await ReceiveLoopAsync(ws, stop).ConfigureAwait(false);
                            linkCts.Cancel();
                            try { await sender.ConfigureAwait(false); } catch (OperationCanceledException) { }
                        }
                    }
                    catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
                    catch (OperationCanceledException) { error = "connect timeout (" + ConnectTimeoutMs + " ms)"; }
                    catch (Exception e) { error = Describe(e); }
                    finally { current = null; }
                }
                if (stop.IsCancellationRequested) break;
                failures++;
                PostState(ReconnectPolicy.IsQuietFailure(failures, everOpened) ? TransportState.Connecting : TransportState.Closed, failures, everOpened, error);
                try { await Task.Delay(ReconnectPolicy.RetryDelayMs(failures), stop).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }

        /// <summary>Returns the reason the link ended.</summary>
        async Task<string> ReceiveLoopAsync(ClientWebSocket ws, CancellationToken stop)
        {
            var buf = new byte[16 * 1024];
            var acc = new MemoryStream();
            while (ws.State == WebSocketState.Open && !stop.IsCancellationRequested)
            {
                WebSocketReceiveResult r;
                try { r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), stop).ConfigureAwait(false); }
                catch (OperationCanceledException) { return "stopped"; }
                catch (Exception e) { return Describe(e); }
                if (r.MessageType == WebSocketMessageType.Close)
                {
                    try { await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).ConfigureAwait(false); } catch { }
                    return "closed by server (" + r.CloseStatus + ")";
                }
                acc.Write(buf, 0, r.Count);
                if (acc.Length > MaxMessageBytes)
                {
                    try { ws.Abort(); } catch { }
                    return "message larger than " + MaxMessageBytes + " bytes";
                }
                if (!r.EndOfMessage) continue;
                if (r.MessageType == WebSocketMessageType.Text)
                {
                    string text = Encoding.UTF8.GetString(acc.GetBuffer(), 0, (int)acc.Length);
                    if (ProtocolParser.TryParseServer(text, out var msg, out var err))
                    {
                        Interlocked.Increment(ref Received);
                        PostMessage(msg);
                    }
                    else
                    {
                        Interlocked.Increment(ref Rejected);
                        if (rejectedLogged++ < 5 || rejectedLogged % 100 == 0) PostLog(1, "ignored server message: " + err);
                    }
                }
                acc.SetLength(0);
            }
            return "socket " + ws.State;
        }

        async Task SendLoopAsync(ClientWebSocket ws, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await outboxSignal.WaitAsync(token).ConfigureAwait(false);
                if (!outbox.TryDequeue(out var json)) continue;
                if (ws.State != WebSocketState.Open) continue;
                var bytes = Encoding.UTF8.GetBytes(json);
                try { await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
                catch (Exception e) { PostLog(1, "send failed: " + Describe(e)); }
            }
        }

        static string Describe(Exception e)
        {
            var inner = e;
            while (inner.InnerException != null) inner = inner.InnerException;
            return inner == e ? e.GetType().Name + ": " + e.Message : e.GetType().Name + ": " + e.Message + " <- " + inner.GetType().Name + ": " + inner.Message;
        }

        public override void Dispose()
        {
            Stop();
            base.Dispose();
        }
    }
}
#endif
