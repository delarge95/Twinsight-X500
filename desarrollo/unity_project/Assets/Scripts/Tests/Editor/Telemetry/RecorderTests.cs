// Lote 1c, tool 7: the own-session recorder (Telemetry/Replay/TwfWriter.cs) against contracts/golden/recording.json, produced by the web's src/shared/recorder.ts:
// 40 frames of the simulator -> the same bytes (base64 and sha256), the file reads back to the same frames, the label of an own session is never «sombra digital».
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;

namespace WebGL.Telemetry.Tests
{
    using JObj = Dictionary<string, object>;
    using JArr = List<object>;

    public class RecorderTests
    {
        static List<TelemetryFrame> GoldenFrames()
        {
            var l = new List<TelemetryFrame>();
            foreach (object o in C.A(C.Golden("recording"), "frames")) l.Add(C.Frame(o));
            return l;
        }

        static RecordedMeta GoldenMeta()
        {
            var m = C.O(C.Golden("recording"), "meta");
            return new RecordedMeta(C.S(m, "kind"), C.S(m, "client"), C.S(m, "started"), C.S(m, "appVersion"));
        }

        static byte[] Record(List<TelemetryFrame> frames, out TwfRecorder rec)
        {
            rec = new TwfRecorder();
            for (int k = 0; k < frames.Count; k++) Assert.IsTrue(rec.Record(frames[k], k * TwfWriter.PeriodMs), "frame " + k);
            return TwfWriter.Encode(GoldenMeta(), QuadX.Px4QuadX, 4, rec.Data, rec.Count);
        }

        [Test]
        public void Golden_Constants_MatchTheWeb()
        {
            var g = C.Golden("recording");
            Assert.AreEqual((int)C.N(g, "rate_hz"), TwfWriter.RateHz);
            Assert.AreEqual((int)C.N(g, "max_frames"), TwfWriter.MaxFrames);
            Assert.AreEqual((int)C.N(g, "max_minutes"), TwfWriter.MaxMinutes);
            Assert.AreEqual(C.N(g, "tolerance"), 0.001);
        }

        [Test]
        public void Golden_HeaderText_AndBytes_AreTheWebs()
        {
            var g = C.Golden("recording");
            byte[] bytes = Record(GoldenFrames(), out TwfRecorder rec);
            Assert.AreEqual((int)C.N(g, "frame_count"), rec.Count);
            Assert.AreEqual((int)C.N(g, "byte_length"), bytes.Length);
            string b64 = Convert.ToBase64String(bytes);
            Assert.AreEqual(C.S(g, "bytes_base64"), b64);
            using (var sha = SHA256.Create())
            {
                string hex = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
                Assert.AreEqual(C.S(g, "sha256"), hex);
            }
            // the float block starts on a multiple of 4
            uint hlen = BitConverter.ToUInt32(bytes, 4);
            Assert.AreEqual((int)C.N(g, "header_bytes"), (int)hlen);
            Assert.AreEqual(0, (8 + hlen) % 4);
        }

        [Test]
        public void RoundTrip_EncodeDecode_GivesTheSameFrames()
        {
            List<TelemetryFrame> frames = GoldenFrames();
            byte[] bytes = Record(frames, out _);
            TwfFile f = TwfFile.Decode(bytes);
            Assert.AreEqual(frames.Count, f.Header.frame_count);
            Assert.AreEqual(TwfWriter.RateHz, f.Header.rate_hz);
            var cols = new FlightColumns(f);
            for (int k = 0; k < frames.Count; k++)
            {
                TelemetryFrame a = frames[k], b = cols.FrameFromRow(f, k, a.ts, 0);
                Assert.AreEqual(k / 20.0, b.lt, 1e-3, "t of row " + k);
                for (int i = 0; i < 3; i++) { Assert.AreEqual(a.p[i], b.p[i], 1e-3); Assert.AreEqual(a.v[i], b.v[i], 1e-3); }
                for (int i = 0; i < 4; i++) { Assert.AreEqual(a.q[i], b.q[i], 1e-3); Assert.AreEqual(a.u[i], b.u[i], 1e-3); Assert.AreEqual(a.rpm[i], b.rpm[i], 1e-3); }
                for (int i = 0; i < 6; i++) Assert.AreEqual(a.temp[i], b.temp[i], 1e-3);
                Assert.AreEqual(a.bat.v, b.bat.v, 1e-3); Assert.AreEqual(a.bat.i, b.bat.i, 1e-3); Assert.AreEqual(a.bat.rem, b.bat.rem, 1e-3);
                Assert.AreEqual(a.vib, b.vib, 1e-3);
                Assert.AreEqual(a.armed, b.armed); Assert.AreEqual(a.landed, b.landed); Assert.AreEqual(a.nav, b.nav);
            }
        }

        [Test]
        public void OwnSession_IsRecognised_AndOnlyOwnSessions()
        {
            byte[] bytes = Convert.FromBase64String(C.S(C.Golden("recording"), "bytes_base64"));
            Assert.AreEqual(TwfWriter.OwnFail.None, TwfWriter.ParseOwn(bytes, out TwfFile file, out string kind));
            Assert.AreEqual("sim", kind);
            Assert.AreEqual(TwfWriter.KindSim, TwfWriter.OwnSessionKind(file.Header));
            // the bundled PX4 flight is not an own session
            byte[] real = System.IO.File.ReadAllBytes(LocalReplayTelemetrySource.BundledPath);
            Assert.AreEqual(TwfWriter.OwnFail.NotOwn, TwfWriter.ParseOwn(real, out _, out string k2));
            Assert.IsNull(k2);
            Assert.AreEqual(TwfWriter.OwnFail.Format, TwfWriter.ParseOwn(new byte[] { 1, 2, 3 }, out _, out _));
        }

        [Test]
        public void Recorder_TakesOneRowPerFiftyMilliseconds_WithoutBursts()
        {
            TelemetryFrame f = GoldenFrames()[0];
            var r = new TwfRecorder();
            Assert.IsTrue(r.Record(f, 1000));      // the first call takes one at once
            Assert.IsFalse(r.Record(f, 1020));
            Assert.IsTrue(r.Record(f, 1050));
            Assert.IsTrue(r.Record(f, 1500));      // a late call: one row, no catching up
            Assert.IsFalse(r.Record(f, 1510));
            Assert.AreEqual(3, r.Count);
            Assert.AreEqual(0.15, r.Seconds, 1e-9);
            Assert.AreEqual(0.1f, r.Data[2 * TwfWriter.Stride], 1e-6);
        }

        [Test]
        public void OwnLabels_NeverSayDigitalShadow()
        {
            var labels = HonestyLabels.Load();
            foreach (string kind in new[] { "sim", "sitl" })
                foreach (string lang in new[] { "es", "en" })
                {
                    Assert.IsTrue(labels.TryOwnReplayText(kind, lang, out string t), kind + " " + lang);
                    string low = t.ToLowerInvariant();
                    Assert.IsFalse(low.Contains("sombra digital") || low.Contains("digital shadow") || low.Contains("gemelo digital"), t);
                }
            Assert.IsTrue(labels.TryOwnReplayText("sim", "es", out string es));
            Assert.IsTrue(es.Contains("sesión propia") && es.Contains("simulada"), es);
        }

        [Test]
        public void Encode_Refuses_MoreThanThirtyMinutes()
        {
            Assert.Throws<ArgumentException>(() => TwfWriter.Encode(GoldenMeta(), QuadX.Px4QuadX, 4, new float[1], TwfWriter.MaxFrames + 1));
        }
    }
}
