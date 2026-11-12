// Every fixture of contracts/fixtures/manifest.json: valid ones parse (strict = schema), invalid ones are rejected (strict),
// the tolerant reader ignores unknown fields; client messages round-trip; C# enums match the schema; labels are verbatim.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WebGL.Telemetry.Json;

namespace WebGL.Telemetry.Tests
{
    using JObj = Dictionary<string, object>;

    public class ProtocolFixtureTests
    {
        static object ParseAs(string def, object raw, ParseOptions opt)
        {
            switch (def)
            {
                case "HelloMessage":
                case "FramesMessage":
                case "FaultsMessage":
                case "EventMessage":
                case "AckMessage":
                {
                    var m = ProtocolParser.ParseServer(raw, opt);
                    string want = def.Replace("Message", "");
                    if (m.Type.ToString() != want) throw new ProtocolException("parsed as " + m.Type + ", manifest says " + def);
                    return m;
                }
                case "ServerMessage": return ProtocolParser.ParseServer(raw, opt);
                case "ClientMessage": return ProtocolParser.ParseClient(raw, opt);
                case "Alert": return ProtocolParser.ParseAlert(raw, opt);
                case "AlertSummary": return ProtocolParser.ParseAlertSummary(raw, opt);
                case "HealthResponse": return ProtocolParser.ParseHealth(raw, opt);
                case "TwfHeader": return ProtocolParser.ParseTwfHeader(raw);
                default: throw new ArgumentException("unknown def " + def);
            }
        }

        [Test]
        public void Manifest_Valid_And_Invalid_Fixtures()
        {
            var man = (JObj)C.Load("fixtures/manifest.json");
            var cs = new Cases("fixtures.manifest (strict = schema)");
            int valid = 0, invalid = 0;
            foreach (JObj e in C.A(man, "entries"))
            {
                cs.Case();
                string file = C.S(e, "file"), def = C.S(e, "def");
                bool each = e.ContainsKey("each") && C.B(e, "each");
                bool shouldPass = C.B(e, "valid");
                var raw = C.Load(file);
                var items = each ? C.A(raw) : new List<object> { raw };
                string err = null;
                foreach (var it in items)
                {
                    try { ParseAs(def, it, ParseOptions.StrictSchema); }
                    catch (Exception ex) when (ex is ProtocolException || ex is JsonParseException) { err = ex.Message; }
                }
                if (shouldPass) { valid++; cs.True(err == null, file + " must be accepted: " + err); }
                else { invalid++; cs.True(err != null, file + " must be rejected"); if (err != null) Debug.Log("[contract] rejected " + file + ": " + err); }
            }
            Debug.Log("[contract] fixtures: " + valid + " valid, " + invalid + " invalid in the manifest");
            Assert.That(invalid, Is.EqualTo(Directory.GetFiles(Path.Combine(C.Root, "fixtures", "invalid"), "*.json").Length), "every invalid fixture is in the manifest");
            cs.Done();
        }

        /// <summary>The live reader is tolerant: unknown fields are ignored (README rule 1); everything else is still rejected.</summary>
        [Test]
        public void Tolerant_Reader_Ignores_Only_Unknown_Fields()
        {
            var cs = new Cases("fixtures.invalid (tolerant reader)");
            foreach (var path in Directory.GetFiles(Path.Combine(C.Root, "fixtures", "invalid"), "*.json"))
            {
                cs.Case();
                string name = Path.GetFileName(path);
                string json = File.ReadAllText(path);
                bool server = ProtocolParser.TryParseServer(json, ParseOptions.Tolerant, out var sm, out var e1);
                bool client = ProtocolParser.TryParseClient(json, ParseOptions.Tolerant, out var cm, out var e2);
                bool accepted = server || client;
                if (name == "frames.extra_field.json")
                {
                    cs.True(server && sm is FramesMessage fm && fm.frames.Length == 1 && fm.frames[0].nav == 4, name + ": tolerant reader keeps the frame and ignores 'gps'");
                }
                else cs.True(!accepted, name + " must be rejected even by the tolerant reader");
            }
            // Forward compatibility: an unknown field in a valid hello is ignored, protocolVersion is read when present.
            cs.Case();
            var hello = C.Text("fixtures/hello.sitl.json").TrimEnd().TrimEnd('}') + ",\"protocolVersion\":\"2.0.0\",\"futureField\":[1,2]}";
            bool ok = ProtocolParser.TryParseServer(hello, out var hm, out var err);
            cs.True(ok && ((HelloMessage)hm).HasProtocolVersion && ((HelloMessage)hm).protocolVersion == "2.0.0", "future hello accepted, version read: " + err);
            cs.True(!ProtocolParser.TryParseServer(hello, ParseOptions.StrictSchema, out _, out _), "strict rejects the same hello");
            cs.Done();
        }

        [Test]
        public void Server_Fixtures_Field_Values()
        {
            var h = (HelloMessage)ProtocolParser.ParseServer(C.Load("fixtures/hello.replay.json"), ParseOptions.StrictSchema);
            Assert.AreEqual(TelemetryMode.Replay, h.mode);
            Assert.AreEqual(4, h.rotors.Length);
            Assert.AreEqual(0.18, h.rotors[0].px, 1e-12);
            Assert.AreEqual("measured", h.provenance.fcTemp);
            Assert.IsTrue(h.provenance.HasBatteryCurrent);
            Assert.AreEqual(474.55, h.source.durationS, 1e-12);
            Assert.AreEqual(1792195320000.0, h.serverTime);
            CollectionAssert.AreEqual(new[] { "inject", "clear" }, h.commands);
            Assert.IsTrue(h.Accepts("inject") && !h.Accepts("takeoff"));

            var hi = (HelloMessage)ProtocolParser.ParseServer(C.Load("fixtures/hello.inspector.json"), ParseOptions.StrictSchema);
            Assert.IsFalse(hi.provenance.HasBatteryCurrent);
            Assert.AreEqual("measured", hi.provenance.BatteryCurrentOrDefault, "absent batteryCurrent = measured");

            var f = (FramesMessage)ProtocolParser.ParseServer(C.Load("fixtures/frames.sitl.json"), ParseOptions.StrictSchema);
            Assert.AreEqual(1792195330000.0, f.frames[0].ts);
            Assert.AreEqual(15.9, f.frames[0].bat.v, 1e-12);
            Assert.AreEqual(4, f.frames[0].nav);

            var fa = (FaultsMessage)ProtocolParser.ParseServer(C.Load("fixtures/faults.active.json"), ParseOptions.StrictSchema);
            Assert.IsTrue(fa.faults[0].HasMotor && fa.faults[0].motor == 2);
            Assert.IsFalse(fa.faults[1].HasMotor);

            var ack = (AckMessage)ProtocolParser.ParseServer(C.Load("fixtures/ack.replay.no_flight_commands.json"), ParseOptions.StrictSchema);
            Assert.IsFalse(ack.ok);
            Assert.AreEqual("command", ack.command, "worker acks with the message type (README discrepancy 1)");
            Assert.IsTrue(ack.HasDetail);

            var ev = (EventMessage)ProtocolParser.ParseServer(C.Load("fixtures/event.critical.json"), ParseOptions.StrictSchema);
            Assert.AreEqual(EventSeverity.Critical, ev.severity);
        }

        [Test]
        public void Client_Messages_RoundTrip()
        {
            var cs = new Cases("fixtures.client round trip");
            foreach (var path in Directory.GetFiles(Path.Combine(C.Root, "fixtures"), "client.*.json"))
            {
                cs.Case();
                var raw = TelemetryJson.Parse(File.ReadAllText(path));
                var m = ProtocolParser.ParseClient(raw, ParseOptions.StrictSchema);
                string json = m.ToJson();
                cs.True(json.Length <= TelemetryProtocol.MaxClientMessageChars, "<= 512 chars");
                cs.Eq(TelemetryJson.Write(Canon(raw)), TelemetryJson.Write(Canon(TelemetryJson.Parse(json))), Path.GetFileName(path));
                cs.True(m.Validate() == null, "validates");
            }
            // Factories produce exactly the fixtures.
            cs.Case();
            cs.Eq(TelemetryJson.Write(Canon(C.Load("fixtures/client.command.fly_to.json"))), TelemetryJson.Write(Canon(TelemetryJson.Parse(ClientMessage.FlyTo(25, -10, 6).ToJson()))), "FlyTo factory");
            cs.Eq(TelemetryJson.Write(Canon(C.Load("fixtures/client.command.restore_all.json"))), TelemetryJson.Write(Canon(TelemetryJson.Parse(ClientMessage.Restore().ToJson()))), "Restore() factory");
            cs.Eq(TelemetryJson.Write(Canon(C.Load("fixtures/client.manual.json"))), TelemetryJson.Write(Canon(TelemetryJson.Parse(ClientMessage.Manual(120, -80, 500, 0).ToJson()))), "Manual factory");
            cs.True(ClientMessage.Inject(FaultKind.MotorFailure).Validate() != null, "inject motor_failure without motor is invalid");
            cs.True(ClientMessage.Manual(0, 0, -5, 0).Validate() != null, "manual z < 0 is invalid");
            cs.True(ClientMessage.MotorOff(0).Validate() != null, "motor_off 0 is invalid");
            cs.Done();
        }

        /// <summary>Sorted-key copy, so two JSON values compare independently of key order.</summary>
        static object Canon(object v)
        {
            if (v is JObj o)
            {
                var r = new SortedDictionary<string, object>(StringComparer.Ordinal);
                foreach (var kv in o) r[kv.Key] = Canon(kv.Value);
                return r.ToList();
            }
            if (v is List<object> l) return l.Select(Canon).ToList();
            return v;
        }

        [Test]
        public void Schema_Enums_And_Version_Match_The_Port()
        {
            var schema = (JObj)C.Load("telemetry.schema.json");
            Assert.AreEqual(TelemetryProtocol.Version, C.S(schema, "protocolVersion"), "protocolVersion");
            var defs = C.O(schema, "$defs");
            string[] Enum(string def) => C.A(C.O(defs, def), "enum").Select(x => (string)x).ToArray();
            CollectionAssert.AreEqual(Enum("Mode"), TelemetryProtocol.Modes);
            CollectionAssert.AreEqual(Enum("SourceClassification"), TelemetryProtocol.SourceClassifications);
            CollectionAssert.AreEqual(Enum("CommandCapability"), TelemetryProtocol.CommandCapabilities);
            CollectionAssert.AreEqual(Enum("FaultKind"), TelemetryProtocol.FaultKinds);
            CollectionAssert.AreEqual(Enum("ProvenanceRpm"), TelemetryProtocol.ProvenanceRpm);
            CollectionAssert.AreEqual(Enum("ProvenanceTemp"), TelemetryProtocol.ProvenanceTemp);
            CollectionAssert.AreEqual(Enum("ProvenanceFcTemp"), TelemetryProtocol.ProvenanceFcTemp);
            CollectionAssert.AreEqual(Enum("ProvenanceCurrent"), TelemetryProtocol.ProvenanceCurrent);
            CollectionAssert.AreEqual(Enum("ThermalOrigin"), TelemetryProtocol.ThermalOrigins);
            var cmdNames = C.A(C.O(defs, "CommandMessage"), "oneOf").Cast<JObj>()
                .Select(r => C.S(r, "$ref").Split('/').Last())
                .Select(n => C.S(C.O(C.O(C.O(defs, n), "properties"), "name"), "const")).ToArray();
            CollectionAssert.AreEqual(cmdNames, TelemetryProtocol.CommandNames);
            var frameReq = C.A(C.O(defs, "TelemetryFrame"), "required").Cast<string>().ToArray();
            CollectionAssert.AreEquivalent(frameReq, typeof(TelemetryFrame).GetFields().Select(f => f.Name).ToArray(), "TelemetryFrame fields = schema required");
            var alertProps = C.O(C.O(defs, "Alert"), "properties");
            CollectionAssert.AreEqual(C.A(C.O(alertProps, "component"), "enum").Cast<string>().ToArray(), TelemetryProtocol.AlertComponents);
        }
    }

    public class HonestyLabelTests
    {
        [Test]
        public void Bundled_Labels_Are_The_Contract_Verbatim()
        {
            string bundled = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts", "Telemetry", "Resources", "TelemetryLabels.json"));
            Assert.AreEqual(C.Text("labels.json"), bundled, "Resources/TelemetryLabels.json must be a byte copy of contracts/labels.json");
            var viaResources = HonestyLabels.Load();
            Assert.AreEqual(TelemetryProtocol.Version, viaResources.ProtocolVersion);
        }

        [Test]
        public void Honesty_Rules()
        {
            var l = HonestyLabels.Parse(C.Text("labels.json"));
            Assert.AreEqual("Sombra digital", l.ClassificationTitle(SourceClassification.Replay, "es"));
            Assert.AreEqual("Gemelo digital SITL", l.ClassificationTitle(SourceClassification.Sitl, "es"));
            Assert.AreEqual("SITL digital twin", l.ClassificationTitle(SourceClassification.Sitl, "en"));
            foreach (var lang in new[] { "es", "en" })
            {
                string replay = (l.ClassificationTitle(SourceClassification.Replay, lang) + " " + l.ClassificationDetail(SourceClassification.Replay, lang)).ToLowerInvariant();
                Assert.IsFalse(replay.Contains("gemelo") || replay.Contains("twin"), "the replay is never a twin (" + lang + ")");
                Assert.IsFalse(replay.Contains("en vivo") || replay.Contains("live"), "the replay is never live (" + lang + ")");
                StringAssert.Contains("SITL", l.ClassificationTitle(SourceClassification.Sitl, lang));
                string insp = l.ClassificationTitle(SourceClassification.Inspector, lang).ToLowerInvariant();
                Assert.IsTrue(insp.Contains("sin telemetría") || insp.Contains("without telemetry"));
            }
            Assert.AreEqual("medida", l.ProvenanceText("measured", "es"));
            Assert.AreEqual("estimado", l.ThermalOriginText(ThermalOrigin.Estimated, "es"));
            Assert.AreEqual("simulado", l.ThermalOriginText(ThermalOrigin.Simulated, "es"));
            Assert.AreEqual("sintética", l.FaultSynthetic("es"));
            StringAssert.Contains("No es un gemelo de un dron físico", l.About(3, "es"));
            Assert.AreEqual("Hold", l.NavState(4));
            Assert.AreEqual("nav 99", l.NavState(99));
            Assert.IsTrue(l.TryLocalReplayText("es", out var localEs), "contract 8e47e15 adds classification.replay.local");
            Assert.AreEqual("Sombra digital · reproducción local", localEs);
            Assert.IsTrue(l.TryLocalReplayText("en", out var localEn));
            Assert.AreEqual("Digital shadow · local replay", localEn);
            // The flight simulator (web source "sim", approved label): its own texts, neither a digital twin nor a shadow.
            Assert.IsTrue(l.TrySimText("title", "es", out var simEs));
            Assert.AreEqual("Simulador de vuelo", simEs);
            Assert.IsTrue(l.TrySimText("title", "en", out var simEn));
            Assert.AreEqual("Flight simulator", simEn);
            Assert.IsTrue(l.TrySimText("detail", "es", out var simDetail));
            StringAssert.Contains("No es PX4 ni un dron real", simDetail);
            Assert.IsTrue(l.TrySimText("source", "es", out var simSource));
            Assert.AreEqual("Simulador (navegador)", simSource);
            StringAssert.DoesNotContain("gemelo", simEs.ToLowerInvariant());
            StringAssert.DoesNotContain("sombra", simEs.ToLowerInvariant());
            Assert.IsFalse(l.TrySimText("nope", "es", out _));
            Assert.IsTrue(l.TryLocalReplayDetail("es", out var localDetail));
            StringAssert.Contains("sin servidor", localDetail);
        }
    }
}
