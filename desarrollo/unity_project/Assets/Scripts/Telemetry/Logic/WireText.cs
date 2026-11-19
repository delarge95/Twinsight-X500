// Contract 1.2 (docs/CONTRATO_1_2.md section 8, web: src/shared/wiretext.ts): texts that travel on the wire with a translatable code.
// The client looks the code up in its own dictionary (`wire.ack.<code>`, `wire.source.<code>`) and falls back to the text of the server
// (`detail`, `source.label`), which stays in Spanish. Old servers send no code: the fallback is exactly what 1.1 showed. PX4's own
// STATUSTEXT (`params.px4`) is never translated. The dictionary of the desktop has NO `wire.*` keys yet (the proposed texts of the document
// are not approved), so today every call returns the fallback. Pure C#: no Unity API.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WebGL.Telemetry
{
    public static class WireText
    {
        /// <summary>{name} placeholders replaced from `p` (a name that is not in `p` stays as written), as the web's `fill`.</summary>
        public static string Fill(string raw, IReadOnlyDictionary<string, object> p)
        {
            if (p == null || string.IsNullOrEmpty(raw)) return raw;
            var sb = new StringBuilder(raw.Length + 16);
            int i = 0;
            while (i < raw.Length)
            {
                int open = raw.IndexOf('{', i);
                if (open < 0) { sb.Append(raw, i, raw.Length - i); break; }
                int close = raw.IndexOf('}', open + 1);
                if (close < 0) { sb.Append(raw, i, raw.Length - i); break; }
                string name = raw.Substring(open + 1, close - open - 1);
                bool word = name.Length > 0;
                foreach (char c in name) if (!(char.IsLetterOrDigit(c) || c == '_')) { word = false; break; }
                sb.Append(raw, i, open - i);
                if (word && p.TryGetValue(name, out object v)) sb.Append(Format(v));
                else sb.Append(raw, open, close - open + 1);
                i = close + 1;
            }
            return sb.ToString();
        }

        static string Format(object v)
        {
            if (v is double d) return d.ToString("R", CultureInfo.InvariantCulture);
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        /// <summary>Text of a translated code, or null when the code is absent or the dictionary does not know it (`lookup` returns null for an unknown key).</summary>
        public static string Text(string kind, string code, IReadOnlyDictionary<string, object> p, Func<string, string> lookup)
        {
            if (string.IsNullOrEmpty(code) || lookup == null) return null;
            string raw = lookup("wire." + kind + "." + code);
            return raw == null ? null : Fill(raw, p);
        }

        /// <summary>What to show for an ack: the translation of `code`, else `detail`, else null (the caller shows its generic text).</summary>
        public static string Ack(AckMessage ack, Func<string, string> lookup) => Text("ack", ack.HasCode ? ack.code : null, ack.codeParams, lookup) ?? (ack.HasDetail ? ack.detail : null);

        /// <summary>What to show for the source line: the translation of `source.code`, else `source.label`.</summary>
        public static string Source(HelloMessage hello, Func<string, string> lookup) => Text("source", hello.source.HasCode ? hello.source.code : null, null, lookup) ?? hello.source.label;

        /// <summary>The lookup of the desktop dictionary in `lang` (null when the key is not there).</summary>
        public static Func<string, string> Lookup(string lang) => key => TelemetryStrings.TryT(key, lang, out string t) ? t : null;

        /// <summary>Ack text for the interface: the translation by code, else the detail in the language of the interface (TelemetryStrings.AckDetail), else null.</summary>
        public static string AckForUi(AckMessage ack, string lang)
        {
            string byCode = Text("ack", ack.HasCode ? ack.code : null, ack.codeParams, Lookup(lang));
            if (byCode != null) return byCode;
            return ack.HasDetail ? TelemetryStrings.AckDetail(ack.detail, lang) : null;
        }

        /// <summary>Source line for the interface: translation by `source.code` or the server label.</summary>
        public static string SourceForUi(HelloMessage hello, string lang) => Source(hello, Lookup(lang));
    }
}
