// Minimal RFC 6455 server on 127.0.0.1 for verifying ClientWebSocket (EditMode tests and the player self-check).
// Not a production server: text frames only, one thread per client, no extensions. Behaviour:
//   - on connect it sends the scripted Greeting messages (e.g. hello + frames);
//   - every text message received is recorded and, when it is a valid ClientMessage, answered with ack{command, ok:true}
//     (echoing the command name like the SITL bridge);
//   - Broadcast() pushes text to every client; DropClients() cuts the TCP connections abruptly (simulated outage).
#if UNITY_STANDALONE || UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace WebGL.Telemetry.Diagnostics
{
    public sealed class LoopbackWebSocketServer : IDisposable
    {
        const string Guid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        readonly TcpListener listener;
        readonly Thread acceptThread;
        readonly List<Client> clients = new List<Client>();
        volatile bool running = true;

        public readonly List<string> Greeting = new List<string>();
        public readonly ConcurrentQueue<string> Received = new ConcurrentQueue<string>();
        public int Connections;
        public int Port { get; }
        public string Url => "ws://127.0.0.1:" + Port + "/ws";
        /// <summary>When false, accepted sockets are closed immediately (simulates a server that is down).</summary>
        public volatile bool Accepting = true;

        sealed class Client
        {
            public TcpClient Tcp;
            public NetworkStream Stream;
            public readonly object WriteLock = new object();
        }

        public LoopbackWebSocketServer(params string[] greeting)
        {
            Greeting.AddRange(greeting);
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "LoopbackWs.accept" };
            acceptThread.Start();
        }

        void AcceptLoop()
        {
            while (running)
            {
                TcpClient tcp;
                try { tcp = listener.AcceptTcpClient(); }
                catch { return; }
                if (!Accepting) { try { tcp.Close(); } catch { } continue; }
                var t = new Thread(() => Serve(tcp)) { IsBackground = true, Name = "LoopbackWs.client" };
                t.Start();
            }
        }

        void Serve(TcpClient tcp)
        {
            var c = new Client { Tcp = tcp, Stream = tcp.GetStream() };
            try
            {
                if (!Handshake(c.Stream)) { tcp.Close(); return; }
                lock (clients) clients.Add(c);
                Interlocked.Increment(ref Connections);
                foreach (var g in Greeting) SendText(c, g);
                while (running)
                {
                    if (!ReadFrame(c.Stream, out int opcode, out byte[] payload)) break;
                    if (opcode == 8) { SendFrame(c, 8, payload); break; }          // close
                    if (opcode == 9) { SendFrame(c, 10, payload); continue; }      // ping -> pong
                    if (opcode != 1) continue;
                    string text = Encoding.UTF8.GetString(payload);
                    Received.Enqueue(text);
                    if (ProtocolParser.TryParseClient(text, ParseOptions.Tolerant, out var msg, out _))
                    {
                        string name = msg is CommandMessage cm ? cm.name : msg.Type.ToString().ToLowerInvariant();
                        SendText(c, "{\"type\":\"ack\",\"command\":" + Json.TelemetryJson.Str(name) + ",\"ok\":true}");
                    }
                    else SendText(c, "{\"type\":\"ack\",\"command\":\"?\",\"ok\":false,\"detail\":\"comando no válido\"}");
                }
            }
            catch { /* client gone */ }
            finally
            {
                lock (clients) clients.Remove(c);
                try { tcp.Close(); } catch { }
            }
        }

        /// <summary>Last handshake request received and the accept value sent (diagnostics).</summary>
        public static string LastRequest, LastAccept;

        /// <summary>RFC 6455 4.2.2: base64(SHA1(key + GUID)).</summary>
        public static string ComputeAccept(string key)
        {
            using (var sha = SHA1.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key + Guid)));
        }

        static bool Handshake(NetworkStream s)
        {
            var sb = new StringBuilder();
            var one = new byte[1];
            while (!sb.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
            {
                if (s.Read(one, 0, 1) != 1) return false;
                sb.Append((char)one[0]);
                if (sb.Length > 8192) return false;
            }
            string key = null;
            foreach (var line in sb.ToString().Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                int colon = line.IndexOf(':');
                if (colon > 0 && line.Substring(0, colon).Trim().Equals("Sec-WebSocket-Key", StringComparison.OrdinalIgnoreCase))
                    key = line.Substring(colon + 1).Trim();
            }
            if (key == null) return false;
            string accept = ComputeAccept(key);
            LastRequest = sb.ToString();
            LastAccept = accept;
            var resp = Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n");
            s.Write(resp, 0, resp.Length);
            return true;
        }

        static bool ReadExactly(Stream s, byte[] b, int n)
        {
            int got = 0;
            while (got < n)
            {
                int r = s.Read(b, got, n - got);
                if (r <= 0) return false;
                got += r;
            }
            return true;
        }

        static bool ReadFrame(Stream s, out int opcode, out byte[] payload)
        {
            opcode = 0;
            payload = null;
            var payloadAll = new MemoryStream();
            while (true)
            {
                var h = new byte[2];
                if (!ReadExactly(s, h, 2)) return false;
                bool fin = (h[0] & 0x80) != 0;
                int op = h[0] & 0x0F;
                bool masked = (h[1] & 0x80) != 0;
                long len = h[1] & 0x7F;
                if (len == 126) { var e = new byte[2]; if (!ReadExactly(s, e, 2)) return false; len = (e[0] << 8) | e[1]; }
                else if (len == 127) { var e = new byte[8]; if (!ReadExactly(s, e, 8)) return false; len = 0; for (int i = 0; i < 8; i++) len = (len << 8) | e[i]; }
                if (len > 16 * 1024 * 1024) return false;
                var mask = new byte[4];
                if (masked && !ReadExactly(s, mask, 4)) return false;
                var data = new byte[len];
                if (!ReadExactly(s, data, (int)len)) return false;
                if (masked) for (int i = 0; i < data.Length; i++) data[i] ^= mask[i & 3];
                if (op >= 8) { opcode = op; payload = data; return true; } // control frames are never fragmented
                if (op != 0) opcode = op;
                payloadAll.Write(data, 0, data.Length);
                if (fin) { payload = payloadAll.ToArray(); return true; }
            }
        }

        static void SendFrame(Client c, int opcode, byte[] payload)
        {
            var head = new MemoryStream();
            head.WriteByte((byte)(0x80 | opcode));
            if (payload.Length < 126) head.WriteByte((byte)payload.Length);
            else if (payload.Length <= 0xFFFF) { head.WriteByte(126); head.WriteByte((byte)(payload.Length >> 8)); head.WriteByte((byte)payload.Length); }
            else { head.WriteByte(127); for (int i = 7; i >= 0; i--) head.WriteByte((byte)((long)payload.Length >> (8 * i))); }
            lock (c.WriteLock)
            {
                var hb = head.ToArray();
                c.Stream.Write(hb, 0, hb.Length);
                c.Stream.Write(payload, 0, payload.Length);
                c.Stream.Flush();
            }
        }

        static void SendText(Client c, string text) => SendFrame(c, 1, Encoding.UTF8.GetBytes(text));

        public void Broadcast(string text)
        {
            Client[] list;
            lock (clients) list = clients.ToArray();
            foreach (var c in list)
            {
                try { SendText(c, text); } catch { }
            }
        }

        /// <summary>Abruptly closes every client TCP connection (no close frame).</summary>
        public void DropClients()
        {
            Client[] list;
            lock (clients) { list = clients.ToArray(); clients.Clear(); }
            foreach (var c in list)
            {
                try { c.Tcp.Client.LingerState = new LingerOption(true, 0); c.Tcp.Close(); } catch { }
            }
        }

        public int ClientCount { get { lock (clients) return clients.Count; } }

        public void Dispose()
        {
            running = false;
            try { listener.Stop(); } catch { }
            DropClients();
        }
    }
}
#endif
