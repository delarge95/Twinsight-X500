// TWF1 reader (web: src/shared/twf.ts, written by tools/ulog/twf.py). Layout: "TWF1", u32 LE header length, UTF-8 JSON header,
// then frame_count x len(fields) little-endian float32, row-major. Golden: replay.json (mini_twf, frame_from_row).
// Skeleton for WP-09 (local replay); pure C#.

using System;
using System.Collections.Generic;
using System.Text;
using WebGL.Telemetry.Json;

namespace WebGL.Telemetry
{
    public sealed class TwfFile
    {
        public TwfHeader Header;
        public float[] Rows;
        public int Stride;
        readonly Dictionary<string, int> index = new Dictionary<string, int>(StringComparer.Ordinal);

        public static TwfFile Decode(byte[] buf)
        {
            if (buf == null || buf.Length < 8 || buf[0] != 'T' || buf[1] != 'W' || buf[2] != 'F' || buf[3] != '1')
                throw new FormatException("not a TWF1 file");
            uint hlen = BitConverter.ToUInt32(LittleEndian(buf, 4), 0);
            if (8 + (long)hlen > buf.Length) throw new FormatException("header length past the end");
            string json = Encoding.UTF8.GetString(buf, 8, (int)hlen);
            var header = ProtocolParser.ParseTwfHeader(TelemetryJson.Parse(json));
            int offset = 8 + (int)hlen;
            if (offset % 4 != 0) throw new FormatException("float block is not 4-byte aligned");
            int count = (buf.Length - offset) / 4;
            if ((buf.Length - offset) % 4 != 0 || count != (long)header.frame_count * header.fields.Length)
                throw new FormatException("float block size does not match header");
            var rows = new float[count];
            if (BitConverter.IsLittleEndian) Buffer.BlockCopy(buf, offset, rows, 0, count * 4);
            else for (int i = 0; i < count; i++) rows[i] = BitConverter.ToSingle(LittleEndian(buf, offset + i * 4), 0);
            var f = new TwfFile { Header = header, Rows = rows, Stride = header.fields.Length };
            for (int i = 0; i < header.fields.Length; i++) f.index[header.fields[i]] = i;
            return f;
        }

        static byte[] LittleEndian(byte[] b, int at)
        {
            var r = new[] { b[at], b[at + 1], b[at + 2], b[at + 3] };
            if (!BitConverter.IsLittleEndian) Array.Reverse(r);
            return r;
        }

        public int Col(string name)
        {
            if (!index.TryGetValue(name, out int i)) throw new KeyNotFoundException("TWF field missing: " + name);
            return i;
        }

        /// <summary>Value as a double (the float32 widened, as a JS Float32Array read).</summary>
        public double At(int row, int col) => Rows[row * Stride + col];
    }

    /// <summary>Column indices of the flight file (tools/ulog/preprocess.py FIELDS).</summary>
    public sealed class FlightColumns
    {
        public int t, vbat, ibat, rem, vib, armed, landed, nav;
        public int[] q, p, v, u, rpm, temp;

        public FlightColumns(TwfFile f)
        {
            t = f.Col("t");
            q = new[] { f.Col("qw"), f.Col("qx"), f.Col("qy"), f.Col("qz") };
            p = new[] { f.Col("x"), f.Col("y"), f.Col("z") };
            v = new[] { f.Col("vx"), f.Col("vy"), f.Col("vz") };
            u = new[] { f.Col("u1"), f.Col("u2"), f.Col("u3"), f.Col("u4") };
            rpm = new[] { f.Col("rpm1"), f.Col("rpm2"), f.Col("rpm3"), f.Col("rpm4") };
            vbat = f.Col("vbat");
            ibat = f.Col("ibat");
            rem = f.Col("rem");
            temp = new[] { f.Col("temp_m1"), f.Col("temp_m2"), f.Col("temp_m3"), f.Col("temp_m4"), f.Col("temp_bat"), f.Col("temp_fc") };
            vib = f.Col("vib");
            armed = f.Col("armed");
            landed = f.Col("landed");
            nav = f.Col("nav");
        }

        static double R3(double x) => JsMath.Round(x * 1000) / 1000;

        /// <summary>Wire frame from row k, exactly as the worker builds it (3-decimal rounding; rpm/nav integers; flags &gt; 0.5).</summary>
        public TelemetryFrame FrameFromRow(TwfFile f, int k, double ts, int seg)
        {
            double[] Pick(int[] idx)
            {
                var r = new double[idx.Length];
                for (int i = 0; i < idx.Length; i++) r[i] = R3(f.At(k, idx[i]));
                return r;
            }
            var rpmV = new double[4];
            for (int i = 0; i < 4; i++) rpmV[i] = JsMath.Round(f.At(k, rpm[i]));
            return new TelemetryFrame
            {
                ts = ts,
                lt = R3(f.At(k, t)),
                seg = seg,
                q = Pick(q), p = Pick(p), v = Pick(v), u = Pick(u),
                rpm = rpmV,
                bat = new BatteryState { v = R3(f.At(k, vbat)), i = R3(f.At(k, ibat)), rem = R3(f.At(k, rem)) },
                temp = Pick(temp),
                vib = R3(f.At(k, vib)),
                armed = f.At(k, armed) > 0.5,
                landed = f.At(k, landed) > 0.5,
                nav = (int)JsMath.Round(f.At(k, nav)),
            };
        }
    }
}
