// Minimal JSON reader/writer for the telemetry protocol (contracts/telemetry.schema.json).
//
// Why not JsonUtility: it cannot tell a missing field from its default value, cannot read null, nested arrays or
// heterogeneous values, and the contract needs all of that (required fields, optional `has*` fields, unknown-field
// tolerance, strict rejection of the invalid fixtures). Newtonsoft would be a package download (not allowed).
// Values: Dictionary<string, object> (object), List<object> (array), double (number), string, bool, null.
// Pure C#: no Unity API, safe on any thread and on every platform (WebGL included).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WebGL.Telemetry.Json
{
    public sealed class JsonParseException : Exception
    {
        public JsonParseException(string message) : base(message) { }
    }

    public static class TelemetryJson
    {
        /// <summary>Hard limit on nesting, protects the receive thread against hostile input.</summary>
        public const int MaxDepth = 64;

        public static object Parse(string text)
        {
            if (text == null) throw new JsonParseException("null input");
            var p = new Reader(text);
            p.SkipWs();
            object v = p.ReadValue(0);
            p.SkipWs();
            if (!p.AtEnd) throw new JsonParseException("trailing characters at " + p.Pos);
            return v;
        }

        public static bool TryParse(string text, out object value, out string error)
        {
            try { value = Parse(text); error = null; return true; }
            catch (JsonParseException e) { value = null; error = e.Message; return false; }
        }

        sealed class Reader
        {
            readonly string s;
            int i;
            public Reader(string s) { this.s = s; }
            public int Pos => i;
            public bool AtEnd => i >= s.Length;

            public void SkipWs()
            {
                while (i < s.Length)
                {
                    char c = s[i];
                    if (c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '﻿') i++;
                    else break;
                }
            }

            public object ReadValue(int depth)
            {
                if (depth > MaxDepth) throw new JsonParseException("nesting too deep");
                SkipWs();
                if (AtEnd) throw new JsonParseException("unexpected end");
                char c = s[i];
                switch (c)
                {
                    case '{': return ReadObject(depth);
                    case '[': return ReadArray(depth);
                    case '"': return ReadString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                        throw new JsonParseException("unexpected '" + c + "' at " + i);
                }
            }

            void Expect(string word)
            {
                if (i + word.Length > s.Length || string.CompareOrdinal(s, i, word, 0, word.Length) != 0)
                    throw new JsonParseException("bad literal at " + i);
                i += word.Length;
            }

            Dictionary<string, object> ReadObject(int depth)
            {
                var d = new Dictionary<string, object>(StringComparer.Ordinal);
                i++; // {
                SkipWs();
                if (i < s.Length && s[i] == '}') { i++; return d; }
                while (true)
                {
                    SkipWs();
                    if (AtEnd || s[i] != '"') throw new JsonParseException("expected key at " + i);
                    string key = ReadString();
                    SkipWs();
                    if (AtEnd || s[i] != ':') throw new JsonParseException("expected ':' at " + i);
                    i++;
                    d[key] = ReadValue(depth + 1); // a duplicate key keeps the last value (as JSON.parse)
                    SkipWs();
                    if (AtEnd) throw new JsonParseException("unterminated object");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw new JsonParseException("expected ',' or '}' at " + i);
                }
            }

            List<object> ReadArray(int depth)
            {
                var l = new List<object>();
                i++; // [
                SkipWs();
                if (i < s.Length && s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(ReadValue(depth + 1));
                    SkipWs();
                    if (AtEnd) throw new JsonParseException("unterminated array");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return l; }
                    throw new JsonParseException("expected ',' or ']' at " + i);
                }
            }

            string ReadString()
            {
                i++; // opening quote
                StringBuilder sb = null;
                int start = i;
                while (true)
                {
                    if (AtEnd) throw new JsonParseException("unterminated string");
                    char c = s[i];
                    if (c == '"')
                    {
                        string tail = s.Substring(start, i - start);
                        i++;
                        return sb == null ? tail : sb.Append(tail).ToString();
                    }
                    if (c < 0x20) throw new JsonParseException("control character in string at " + i);
                    if (c != '\\') { i++; continue; }
                    sb = sb ?? new StringBuilder();
                    sb.Append(s, start, i - start);
                    i++;
                    if (AtEnd) throw new JsonParseException("bad escape");
                    char e = s[i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw new JsonParseException("bad \\u escape");
                            if (!int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                                throw new JsonParseException("bad \\u escape");
                            sb.Append((char)code);
                            i += 4;
                            break;
                        default: throw new JsonParseException("bad escape '\\" + e + "'");
                    }
                    start = i;
                }
            }

            double ReadNumber()
            {
                int start = i;
                if (s[i] == '-') i++;
                while (i < s.Length)
                {
                    char c = s[i];
                    if ((c >= '0' && c <= '9') || c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-') i++;
                    else break;
                }
                string tok = s.Substring(start, i - start);
                if (!double.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) || double.IsInfinity(v))
                    throw new JsonParseException("bad number '" + tok + "'");
                return v;
            }
        }

        // ---------------------------------------------------------------------------------------------------- writer

        /// <summary>Number text as JSON.stringify writes it for the values the protocol uses (integers without ".0").</summary>
        public static string Num(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "null";
            if (v == Math.Floor(v) && Math.Abs(v) < 1e15) return ((long)v).ToString(CultureInfo.InvariantCulture);
            return v.ToString("R", CultureInfo.InvariantCulture);
        }

        public static string Str(string v)
        {
            if (v == null) return "null";
            var sb = new StringBuilder(v.Length + 2);
            sb.Append('"');
            foreach (char c in v)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        /// <summary>Serializes a value tree (Dictionary / IEnumerable / primitives) to JSON; objects keep insertion order.</summary>
        public static string Write(object v)
        {
            var sb = new StringBuilder();
            WriteTo(sb, v);
            return sb.ToString();
        }

        static void WriteTo(StringBuilder sb, object v)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case string str: sb.Append(Str(str)); break;
                case double d: sb.Append(Num(d)); break;
                case float f: sb.Append(Num(f)); break;
                case int n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
                case long n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
                case IEnumerable<KeyValuePair<string, object>> o:
                    sb.Append('{');
                    bool first = true;
                    foreach (var kv in o)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        sb.Append(Str(kv.Key)).Append(':');
                        WriteTo(sb, kv.Value);
                    }
                    sb.Append('}');
                    break;
                case System.Collections.IEnumerable e:
                    sb.Append('[');
                    bool f0 = true;
                    foreach (var x in e)
                    {
                        if (!f0) sb.Append(',');
                        f0 = false;
                        WriteTo(sb, x);
                    }
                    sb.Append(']');
                    break;
                default: throw new ArgumentException("cannot serialize " + v.GetType());
            }
        }
    }
}
