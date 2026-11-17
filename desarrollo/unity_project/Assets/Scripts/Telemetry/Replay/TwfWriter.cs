// Lote 1c, tool 7: record an own flight (flight simulator or PX4 SITL) as a TWF1 file and read it back. A port of the web's src/shared/recorder.ts. Pure C# (no
// UnityEngine). Same TWF1 as Twf.cs reads: "TWF1", u32 LE header length, UTF-8 JSON header padded with spaces so that the float block starts on a multiple of 4,
// then frame_count x fields float32 LE. The JSON header is written key by key in the order of JSON.stringify of the web's recordingHeader, so the same frames give
// the same bytes (golden recording.json, sha256 of the base64). The wire protocol does not change: TwfHeader is an open object.

using System;
using System.Collections.Generic;
using System.Text;
using WebGL.Telemetry.Json;

namespace WebGL.Telemetry
{
    /// <summary>Who recorded the file: kind "sim" | "sitl", client "web" | "desktop", ISO-8601 start, app version.</summary>
    public sealed class RecordedMeta
    {
        public string kind, client, started, appVersion;
        public RecordedMeta(string kind, string client, string started, string appVersion) { this.kind = kind; this.client = client; this.started = started; this.appVersion = appVersion; }
    }

    public static class TwfWriter
    {
        /// <summary>The columns of a flight file, in the order tools/ulog/preprocess.py writes them.</summary>
        public static readonly string[] Fields =
        {
            "t", "qw", "qx", "qy", "qz", "x", "y", "z", "vx", "vy", "vz", "u1", "u2", "u3", "u4", "rpm1", "rpm2", "rpm3", "rpm4",
            "vbat", "ibat", "rem", "temp_m1", "temp_m2", "temp_m3", "temp_m4", "temp_bat", "temp_fc", "vib", "armed", "landed", "nav",
        };
        public const int Stride = 32;
        public const int RateHz = 20;
        /// <summary>The last displayed frame is sampled every 50 ms.</summary>
        public const double PeriodMs = 1000.0 / RateHz;
        public const int MaxMinutes = 30;
        /// <summary>30 min at 20 Hz = 36 000 frames, about 4.6 MB.</summary>
        public const int MaxFrames = MaxMinutes * 60 * RateHz;
        public const string OwnDataset = "twinsight-own-session";
        public const string KindSim = "sim", KindSitl = "sitl";

        /// <summary>One TWF1 row of a wire frame; `t` is the row time in seconds since the start of the recording.</summary>
        public static void RowFromFrame(TelemetryFrame f, double t, float[] dst, int offset)
        {
            int i = offset;
            dst[i++] = (float)t;
            for (int k = 0; k < 4; k++) dst[i++] = (float)f.q[k];
            for (int k = 0; k < 3; k++) dst[i++] = (float)f.p[k];
            for (int k = 0; k < 3; k++) dst[i++] = (float)f.v[k];
            for (int k = 0; k < 4; k++) dst[i++] = (float)f.u[k];
            for (int k = 0; k < 4; k++) dst[i++] = (float)f.rpm[k];
            dst[i++] = (float)f.bat.v; dst[i++] = (float)f.bat.i; dst[i++] = (float)f.bat.rem;
            for (int k = 0; k < 6; k++) dst[i++] = (float)f.temp[k];
            dst[i++] = (float)f.vib;
            dst[i++] = f.armed ? 1f : 0f;
            dst[i++] = f.landed ? 1f : 0f;
            dst[i] = f.nav;
        }

        static string Num(double v) => TelemetryJson.Num(v);

        /// <summary>
        /// The JSON header text, key order and number formatting as the web's JSON.stringify of recordingHeader (+ frame_count): version, fields, rate_hz, frame_count,
        /// duration_s, frames, rotors, vehicle, source, recorded.
        /// </summary>
        public static string HeaderJson(RecordedMeta meta, IReadOnlyList<RotorGeometry> rotors, int batCells, float[] rows, int frames)
        {
            double duration = frames > 0 ? rows[(frames - 1) * Stride] : 0;
            duration = Math.Floor(duration * 1000 + 0.5) / 1000;
            var sb = new StringBuilder(768);
            sb.Append("{\"version\":1,\"fields\":[");
            for (int i = 0; i < Fields.Length; i++) { if (i > 0) sb.Append(','); sb.Append('"').Append(Fields[i]).Append('"'); }
            sb.Append("],\"rate_hz\":").Append(RateHz).Append(",\"frame_count\":").Append(frames).Append(",\"duration_s\":").Append(Num(duration));
            sb.Append(",\"frames\":{\"attitude\":\"FRD body -> NED (Hamilton, w first)\",\"position\":\"local NED metres\"},\"rotors\":[");
            for (int i = 0; i < rotors.Count; i++)
            {
                if (i > 0) sb.Append(',');
                RotorGeometry r = rotors[i];
                sb.Append("{\"motor\":").Append(r.motor).Append(",\"px\":").Append(Num(r.px)).Append(",\"py\":").Append(Num(r.py)).Append(",\"km\":").Append(Num(r.km)).Append('}');
            }
            sb.Append("],\"vehicle\":{\"bat_cells\":").Append(batCells).Append("},\"source\":{\"dataset\":\"").Append(OwnDataset).Append("\"},\"recorded\":{\"kind\":")
              .Append(TelemetryJson.Str(meta.kind)).Append(",\"client\":").Append(TelemetryJson.Str(meta.client)).Append(",\"started\":").Append(TelemetryJson.Str(meta.started))
              .Append(",\"appVersion\":").Append(TelemetryJson.Str(meta.appVersion)).Append("}}");
            return sb.ToString();
        }

        /// <summary>The file bytes: `frames` rows of <see cref="Stride"/> floats from `rows`. Little-endian whatever the platform.</summary>
        public static byte[] Encode(RecordedMeta meta, IReadOnlyList<RotorGeometry> rotors, int batCells, float[] rows, int frames)
        {
            if (frames < 0 || (long)frames * Stride > rows.Length) throw new ArgumentException("rows do not match the frame count");
            if (frames > MaxFrames) throw new ArgumentException("a session holds at most " + MaxFrames + " frames (" + MaxMinutes + " min)");
            byte[] raw = Encoding.UTF8.GetBytes(HeaderJson(meta, rotors, batCells, rows, frames));
            int pad = (4 - ((8 + raw.Length) % 4)) % 4;
            int hlen = raw.Length + pad;
            var buf = new byte[8 + hlen + frames * Stride * 4];
            buf[0] = (byte)'T'; buf[1] = (byte)'W'; buf[2] = (byte)'F'; buf[3] = (byte)'1';
            buf[4] = (byte)(hlen & 0xFF); buf[5] = (byte)((hlen >> 8) & 0xFF); buf[6] = (byte)((hlen >> 16) & 0xFF); buf[7] = (byte)((hlen >> 24) & 0xFF);
            Buffer.BlockCopy(raw, 0, buf, 8, raw.Length);
            for (int i = 0; i < pad; i++) buf[8 + raw.Length + i] = 0x20;
            int o = 8 + hlen;
            for (int i = 0; i < frames * Stride; i++)
            {
                byte[] b = BitConverter.GetBytes(rows[i]);
                if (!BitConverter.IsLittleEndian) Array.Reverse(b);
                Buffer.BlockCopy(b, 0, buf, o + i * 4, 4);
            }
            return buf;
        }

        /// <summary>"sim" or "sitl" for a file recorded by TwinSight (the web or the desktop app); null for any other TWF1 (a PX4 log, a stranger's file).</summary>
        public static string OwnSessionKind(TwfHeader header)
        {
            if (header == null || header.sourceDataset != OwnDataset || header.Raw == null) return null;
            if (!header.Raw.TryGetValue("recorded", out object rec) || !(rec is Dictionary<string, object> d)) return null;
            if (!d.TryGetValue("kind", out object k) || !(k is string s)) return null;
            return s == KindSim || s == KindSitl ? s : null;
        }

        public enum OwnFail { None, Format, NotOwn, Empty, TooLong }

        /// <summary>Opens a TWF1 as an own session: it must decode, hold the flight columns and say it was recorded by TwinSight. Never throws.</summary>
        public static OwnFail ParseOwn(byte[] bytes, out TwfFile file, out string kind)
        {
            file = null; kind = null;
            try
            {
                file = TwfFile.Decode(bytes);
                new FlightColumns(file); // throws when a column is missing
                TwfHeader h = file.Header;
                if (double.IsNaN(h.rate_hz) || h.rate_hz <= 0) return OwnFail.Format;
            }
            catch (Exception) { file = null; return OwnFail.Format; }
            kind = OwnSessionKind(file.Header);
            if (kind == null) return OwnFail.NotOwn;
            if (file.Header.frame_count < 2) return OwnFail.Empty;
            if (file.Header.frame_count > MaxFrames) return OwnFail.TooLong;
            return OwnFail.None;
        }
    }

    /// <summary>A recording in progress (web: createRecorder / recordFrame). `data` grows by doubling; `Count` rows are valid.</summary>
    public sealed class TwfRecorder
    {
        public float[] Data = new float[TwfWriter.Stride * 256];
        public int Count;
        double? nextMs;

        public bool Full => Count >= TwfWriter.MaxFrames;
        public double Seconds => Count / (double)TwfWriter.RateHz;

        /// <summary>
        /// Offers the frame on screen at `nowMs` (any monotonic clock). One row is taken per 50 ms whatever the render rate (the last frame shown is held when no new
        /// one arrived): the first call takes one at once and a late call does not make up for the lost time (no burst). `t` is the row time on the 20 Hz grid, so the
        /// file plays back at the speed it was flown. True when a row was added. Stops at MaxFrames (30 min).
        /// </summary>
        public bool Record(TelemetryFrame f, double nowMs)
        {
            if (Full) return false;
            if (nowMs < (nextMs ?? double.NegativeInfinity)) return false;
            if ((Count + 1) * TwfWriter.Stride > Data.Length) Array.Resize(ref Data, Math.Min(TwfWriter.MaxFrames * TwfWriter.Stride, Data.Length * 2));
            TwfWriter.RowFromFrame(f, Count / (double)TwfWriter.RateHz, Data, Count * TwfWriter.Stride);
            Count++;
            double next = (nextMs ?? nowMs) + TwfWriter.PeriodMs;
            nextMs = next <= nowMs ? nowMs + TwfWriter.PeriodMs : next;
            return true;
        }
    }
}
