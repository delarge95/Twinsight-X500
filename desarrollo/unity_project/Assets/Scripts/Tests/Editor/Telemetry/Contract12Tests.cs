// Contract 1.2 (docs/CONTRATO_1_2.md, web branch web/telemetria-control): the desktop client reads and writes the new fields and messages (GPS, ESC, pose and
// velocity provenance, translatable codes, params, mission status, param_* / mission_* commands) against the fixtures and goldens copied from the web
// (Contracts~/, see SOURCE.md), keeps reading 1.0 / 1.1 messages, and ignores hello.world / hello.streams. Data support only: no screen offers parameters or PX4 missions.
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WebGL.Telemetry.Json;

namespace WebGL.Telemetry.Tests
{
    using JObj = Dictionary<string, object>;

    public class Contract12Tests
    {
        static T Server<T>(string fixture, ParseOptions opt) where T : ServerMessage => (T)ProtocolParser.ParseServer(C.Load("fixtures/" + fixture + ".json"), opt);
        static ClientMessage Client(string fixture) => ProtocolParser.ParseClient(C.Load("fixtures/" + fixture + ".json"), ParseOptions.StrictSchema);

        static string Canon(string json) => TelemetryJson.Write(CanonOf(TelemetryJson.Parse(json)));
        static object CanonOf(object v)
        {
            if (v is JObj o) return o.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => new KeyValuePair<string, object>(k.Key, CanonOf(k.Value))).ToList();
            if (v is List<object> l) return l.Select(CanonOf).ToList();
            return v;
        }

        // ------------------------------------------------------------------------------------------------ U-1, U-3

        [Test]
        public void Version_Is_1_2_0_And_Hello_Fixtures_Are_Read()
        {
            Assert.AreEqual("1.2.0", TelemetryProtocol.Version);
            var sitl = Server<HelloMessage>("hello.sitl", ParseOptions.StrictSchema);
            Assert.IsTrue(sitl.HasProtocolVersion);
            Assert.AreEqual("1.2.0", sitl.protocolVersion);
            var c = new TelemetryClient(new ManualTelemetrySource(SourceClassification.Sitl)) { Log = null };
            Assert.IsNull(c.ProtocolWarning);
            // a 1.2.0 hello is not "a newer major": no warning
            var src = new ManualTelemetrySource(SourceClassification.Sitl);
            c = new TelemetryClient(src) { Log = null };
            src.Start();
            Assert.IsTrue(src.PushJson(C.Text("fixtures/hello.sitl.json"), out _));
            c.Update(0, 0.016);
            Assert.IsNull(c.ProtocolWarning, "protocolVersion 1.2.0 is accepted without a warning");
            Assert.IsNotNull(c.Hello);
        }

        [Test]
        public void Provenance_Pose_Velocity_Gps_Esc()
        {
            var r = Server<HelloMessage>("hello.replay", ParseOptions.StrictSchema).provenance;
            Assert.IsTrue(r.HasPose && r.HasVelocity && r.HasGps);
            Assert.AreEqual("estimated", r.pose);
            Assert.AreEqual("estimated", r.velocity);
            Assert.AreEqual("unavailable", r.gps);
            Assert.IsFalse(r.HasEsc);
            var s = Server<HelloMessage>("hello.sitl", ParseOptions.StrictSchema).provenance;
            Assert.AreEqual("estimated", s.pose);
            Assert.AreEqual("estimated", s.velocity);
            Assert.AreEqual("measured", s.gps);
            // the frame with MEASURED ESC data says so
            var esc = Server<FramesMessage>("frames.esc", ParseOptions.StrictSchema).frames[0];
            Assert.IsNotNull(esc.esc);
            CollectionAssert.AreEqual(esc.rpm, esc.esc.rpm, "the frame's rpm is the same measured speed");
        }

        [Test]
        public void FlightOrigins_Pose_And_Velocity_Follow_The_Hello_And_Fall_Back_To_The_Old_Rule()
        {
            var replay = Server<HelloMessage>("hello.replay", ParseOptions.StrictSchema);
            var sitl = Server<HelloMessage>("hello.sitl", ParseOptions.StrictSchema);
            var inspector = Server<HelloMessage>("hello.inspector", ParseOptions.StrictSchema);
            // D-10: the replay and PX4 SITL declare an EKF2 estimate -> «estimado»
            Assert.AreEqual(ReadingOrigin.Estimated, FlightOrigins.Pose(replay, SourceClassification.Replay));
            Assert.AreEqual(ReadingOrigin.Estimated, FlightOrigins.Velocity(replay, SourceClassification.Replay));
            Assert.AreEqual(ReadingOrigin.Estimated, FlightOrigins.Pose(sitl, SourceClassification.Sitl));
            Assert.AreEqual(ReadingOrigin.Estimated, FlightOrigins.Velocity(sitl, SourceClassification.Sitl));
            // a hello without the fields (1.0 / 1.1 server, simulator, Inspector, no hello yet): what the desktop showed before
            Assert.AreEqual(ReadingOrigin.Measured, FlightOrigins.Pose(inspector, SourceClassification.Replay));
            Assert.AreEqual(ReadingOrigin.Simulated, FlightOrigins.Pose(inspector, SourceClassification.Sitl));
            Assert.AreEqual(ReadingOrigin.Measured, FlightOrigins.Velocity(null, SourceClassification.Replay));
            Assert.AreEqual(ReadingOrigin.Simulated, FlightOrigins.Velocity(null, SourceClassification.Sitl));
            // the other channels keep their rule (battery and vibration stay «medido» on the replay: D-10 / README)
            Assert.AreEqual(ReadingOrigin.Measured, FlightOrigins.Base(SourceClassification.Replay));
            // the flight simulator still says simulated for every reading
            Assert.AreEqual(ReadingOrigin.Simulated, FlightOrigins.ForSession(FlightOrigins.Pose(replay, SourceClassification.Replay), true));
            // the analysis chart tags use the same words as the Vuelo tab
            var altitude = ChartSpec.All.First(x => x.Id == "altitude");
            var speed = ChartSpec.All.First(x => x.Id == "speed");
            var level = ChartSpec.All.First(x => x.Id == "level");
            Assert.AreEqual(ReadingOrigin.Estimated, altitude.Origin(replay, SourceClassification.Replay));
            Assert.AreEqual(ReadingOrigin.Estimated, speed.Origin(replay, SourceClassification.Replay));
            Assert.AreEqual(ReadingOrigin.Measured, level.Origin(replay, SourceClassification.Replay));
            Assert.AreEqual(ReadingOrigin.Measured, altitude.Origin(inspector, SourceClassification.Replay));
        }

        [Test]
        public void MapCaption_Text_Exists_Only_As_A_Proposed_Neutral_Text()
        {
            Assert.IsTrue(TelemetryStrings.TryT("map.posEstimated", "es", out string es));
            Assert.AreEqual("Posición: estimado", es);
            Assert.IsTrue(TelemetryStrings.TryT("map.posEstimated", "en", out string en));
            Assert.AreEqual("Position: estimated", en);
            Assert.IsTrue(TelemetryStrings.TryT("map.posMeasured", "es", out _), "the old text stays for a hello without provenance.pose");
        }

        // ------------------------------------------------------------------------------------------------ U-2

        [Test]
        public void Frame_Gps_And_Esc_Are_Read_With_Their_Absence_Marks()
        {
            var g = Server<FramesMessage>("frames.sitl.gps", ParseOptions.StrictSchema).frames[0].gps;
            Assert.IsNotNull(g);
            Assert.AreEqual(3, g.fix);
            Assert.IsTrue(g.HasSats && g.HasHdop);
            Assert.AreEqual(10, g.sats);
            Assert.AreEqual(0.8, g.hdop, 1e-12);

            var e = Server<FramesMessage>("frames.esc", ParseOptions.StrictSchema).frames[0].esc;
            CollectionAssert.AreEqual(new[] { 6012.0, 6100.0, 5980.0, 6044.0 }, e.rpm);
            CollectionAssert.AreEqual(new[] { 15.8, 15.8, 15.8, 15.8 }, e.v);
            CollectionAssert.AreEqual(new[] { 4.1, 4.3, 4.0, 4.2 }, e.i);

            // frames without them: null = "sin dato" (never a 0)
            var plain = Server<FramesMessage>("frames.sitl", ParseOptions.StrictSchema).frames[0];
            Assert.IsNull(plain.gps);
            Assert.IsNull(plain.esc);

            // sats and hdop optional, esc v and i optional
            string json = "{\"type\":\"frames\",\"frames\":[" + FrameWith(", \"gps\": {\"fix\": 0}, \"esc\": {\"rpm\": [1,2,3,4]}") + "]}";
            Assert.IsTrue(ProtocolParser.TryParseServer(json, ParseOptions.StrictSchema, out var m, out string err), err);
            var f = ((FramesMessage)m).frames[0];
            Assert.AreEqual(0, f.gps.fix);
            Assert.IsFalse(f.gps.HasSats);
            Assert.IsFalse(f.gps.HasHdop);
            Assert.IsNull(f.esc.v);
            Assert.IsNull(f.esc.i);
        }

        static string FrameWith(string extra) =>
            "{\"ts\":1792195330000,\"lt\":1,\"seg\":0,\"q\":[1,0,0,0],\"p\":[0,0,0],\"v\":[0,0,0],\"u\":[0,0,0,0],\"rpm\":[0,0,0,0],\"bat\":{\"v\":16,\"i\":0,\"rem\":1},\"temp\":[20,20,20,20,20,20],\"vib\":0,\"armed\":false,\"landed\":true,\"nav\":0" + extra + "}";

        [Test]
        public void Gps_And_Esc_Out_Of_Range_Are_Rejected_Even_By_The_Tolerant_Reader()
        {
            foreach (string extra in new[]
            {
                ", \"gps\": {\"fix\": 9}", ", \"gps\": {\"fix\": -1}", ", \"gps\": {\"fix\": 3, \"sats\": 255}", ", \"gps\": {\"fix\": 3, \"hdop\": -0.1}", ", \"gps\": {}",
                ", \"esc\": {\"rpm\": [1,2,3]}", ", \"esc\": {\"rpm\": [1,2,3,-4]}", ", \"esc\": {\"rpm\": [1,2,3,4], \"v\": [1,2]}",
            })
            {
                string json = "{\"type\":\"frames\",\"frames\":[" + FrameWith(extra) + "]}";
                Assert.IsFalse(ProtocolParser.TryParseServer(json, ParseOptions.Tolerant, out _, out _), extra);
            }
        }

        [Test]
        public void Gps_And_Esc_Survive_Clone_And_Interpolation()
        {
            var f = Server<FramesMessage>("frames.sitl.gps", ParseOptions.StrictSchema).frames[0];
            Assert.AreSame(f.gps, f.Clone().gps);
            var a = f.Clone();
            var b = f.Clone();
            b.ts = a.ts + 100;
            a.esc = Server<FramesMessage>("frames.esc", ParseOptions.StrictSchema).frames[0].esc;
            var mid = Interp.InterpolateFrame(a, b, 0.5);
            Assert.AreSame(a.gps, mid.gps, "discrete state is held from the older sample");
            Assert.AreSame(a.esc, mid.esc);
            var smooth = Interp.SmoothToward(a, b, 18, 0.016);
            Assert.AreSame(b.gps, smooth.gps, "SITL smoothing takes it from the newest sample");
            Assert.IsNull(smooth.esc);
        }

        // ------------------------------------------------------------------------------------------------ U-4

        [Test]
        public void Hello_Source_Code_World_And_Streams()
        {
            var cs = new Cases("hello.source.code");
            foreach (var (fixture, code) in new[] { ("hello.replay", "replay.flightReview"), ("hello.sitl", "sitl.gz"), ("hello.inspector", "inspector"), ("hello.world", "sitl.gz") })
            {
                cs.Case();
                var h = Server<HelloMessage>(fixture, ParseOptions.StrictSchema);
                cs.True(h.source.HasCode && h.source.code == code, fixture + " source.code");
            }
            // world and streams: tolerated and ignored (the flags only say they were there)
            var w = Server<HelloMessage>("hello.world", ParseOptions.StrictSchema);
            cs.Case();
            cs.True(w.HasWorld && w.HasStreams, "hello.world flags");
            var tolerant = Server<HelloMessage>("hello.world", ParseOptions.Tolerant);
            cs.True(tolerant.HasWorld && tolerant.HasStreams && tolerant.mode == TelemetryMode.Sitl && tolerant.Accepts("mission_set"), "tolerant read keeps the rest of the hello");
            // strict rejects a bad world (frame ENU), the tolerant live reader ignores it
            string bad = C.Text("fixtures/invalid/hello.world_bad_frame.json");
            cs.Case();
            cs.True(!ProtocolParser.TryParseServer(bad, ParseOptions.StrictSchema, out _, out _), "strict: world.frame must be NED");
            cs.True(ProtocolParser.TryParseServer(bad, ParseOptions.Tolerant, out _, out _), "tolerant: world is ignored");
            // the codes are a pattern: a free-text code is rejected
            cs.Case();
            string badCode = C.Text("fixtures/hello.sitl.json").Replace("\"code\": \"sitl.gz\"", "\"code\": \"PX4 SITL\"");
            cs.True(!ProtocolParser.TryParseServer(badCode, ParseOptions.Tolerant, out _, out _), "source.code pattern");
            cs.Done();
        }

        /// <summary>The 1.2 hello as a 1.0 / 1.1 server would have sent it: without protocolVersion, provenance.pose / velocity / gps / esc, source.code and the new commands.</summary>
        static string As10(string helloJson)
        {
            var h = (JObj)TelemetryJson.Parse(helloJson);
            h.Remove("protocolVersion");
            var prov = (JObj)h["provenance"];
            foreach (string k in new[] { "pose", "velocity", "gps", "esc" }) prov.Remove(k);
            ((JObj)h["source"]).Remove("code");
            string[] added = { "param_list", "param_get", "param_set", "mission_set", "mission_start", "mission_clear" };
            h["commands"] = ((List<object>)h["commands"]).Where(c => !added.Contains((string)c)).ToList();
            return TelemetryJson.Write(h);
        }

        [Test]
        public void Old_Hellos_Without_1_2_Fields_Still_Read()
        {
            // a 1.0.0 hello (no protocolVersion, no pose/velocity/gps/code, none of the new commands) made from the 1.2 fixture
            string hello10 = As10(C.Text("fixtures/hello.sitl.json"));
            Assert.IsTrue(ProtocolParser.TryParseServer(hello10, ParseOptions.StrictSchema, out var m, out string err), err);
            var h = (HelloMessage)m;
            Assert.IsFalse(h.provenance.HasPose || h.provenance.HasVelocity || h.provenance.HasGps || h.provenance.HasEsc);
            Assert.IsFalse(h.source.HasCode);
            Assert.IsFalse(h.HasProtocolVersion);
            Assert.IsFalse(h.Accepts("param_list"));
            Assert.AreEqual(ReadingOrigin.Simulated, FlightOrigins.Pose(h, SourceClassification.Sitl), "no provenance.pose: the old rule");
            // a 1.0 / 1.1 ack and frame are unchanged
            Assert.IsTrue(ProtocolParser.TryParseServer("{\"type\":\"ack\",\"command\":\"takeoff\",\"ok\":false,\"detail\":\"MAV_RESULT 4 (FAILED)\"}", ParseOptions.StrictSchema, out var a, out err), err);
            Assert.IsFalse(((AckMessage)a).HasCode);
            Assert.IsNull(((AckMessage)a).codeParams);
        }

        // ------------------------------------------------------------------------------------------------ U-5

        [Test]
        public void Ack_Codes_Params_And_Fallback_To_Detail()
        {
            var a = Server<AckMessage>("ack.sitl.mission_uploaded", ParseOptions.StrictSchema);
            Assert.IsTrue(a.HasCode);
            Assert.AreEqual("mission.uploaded", a.code);
            Assert.AreEqual(8.0, a.codeParams["items"]);
            Assert.AreEqual(3.0, a.codeParams["legs"]);
            var armed = Server<AckMessage>("ack.sitl.param_armed", ParseOptions.StrictSchema);
            Assert.AreEqual("param.armed", armed.code);
            Assert.AreEqual("MPC_XY_VEL_MAX", armed.codeParams["id"]);
            var stale = Server<AckMessage>("ack.sitl.stale", ParseOptions.StrictSchema);
            Assert.AreEqual("stale", stale.code);
            Assert.AreEqual(41L, stale.seq);
            var relay = Server<AckMessage>("ack.relay.no_lease", ParseOptions.StrictSchema);
            Assert.AreEqual("relay.noLease", relay.code);
            var legacy = Server<AckMessage>("ack.legacy.no_code", ParseOptions.StrictSchema);
            Assert.IsFalse(legacy.HasCode);
            Assert.AreEqual("MAV_RESULT 4 (FAILED)", legacy.detail);

            // the rule: no translation in the dictionary -> `detail` (what 1.1 showed); with a dictionary that knows the code -> the translation with its params
            Assert.AreEqual("MAV_RESULT 4 (FAILED)", WireText.Ack(legacy, key => "never used"));
            Assert.AreEqual(armed.detail, WireText.Ack(armed, key => null));
            Assert.AreEqual(armed.detail, WireText.Ack(armed, null));
            string text = WireText.Ack(a, key => key == "wire.ack.mission.uploaded" ? "Mission: {legs} waypoints ({items}) {missing}" : null);
            Assert.AreEqual("Mission: 3 waypoints (8) {missing}", text);
            Assert.AreEqual(legacy.detail, WireText.Ack(legacy, key => "ignored: it has no code"));
            Assert.IsNull(WireText.Ack(new AckMessage { command = "x", ok = false }, key => null), "no code and no detail: the caller shows its generic text");
            // the desktop dictionary has no wire.* keys yet (nothing is approved): the interface shows the detail exactly as before
            foreach (var kv in TelemetryStrings.All) Assert.IsFalse(kv.Key.StartsWith("wire.", StringComparison.Ordinal), "no unapproved wire.* text is in the dictionary: " + kv.Key);
            Assert.AreEqual(TelemetryStrings.AckDetail(armed.detail, "es"), WireText.AckForUi(armed, "es"));
            Assert.AreEqual(TelemetryStrings.AckDetail(armed.detail, "en"), WireText.AckForUi(armed, "en"));
            Assert.IsNull(WireText.AckForUi(new AckMessage { command = "x", ok = false, code = "stale", HasCode = true }, "es"));
        }

        [Test]
        public void Source_Text_By_Code_With_The_Label_As_Fallback()
        {
            var h = Server<HelloMessage>("hello.replay", ParseOptions.StrictSchema);
            Assert.AreEqual(h.source.label, WireText.Source(h, key => null));
            Assert.AreEqual("T", WireText.Source(h, key => key == "wire.source.replay.flightReview" ? "T" : null));
            Assert.AreEqual(h.source.label, WireText.SourceForUi(h, "es"), "no wire.source.* key: the server label, as in 1.1");
            var noCode = new HelloMessage { source = new SourceInfo { label = "L" } };
            Assert.AreEqual("L", WireText.Source(noCode, key => "ignored"));
        }

        [Test]
        public void Ack_Params_Must_Be_Text_Or_Number()
        {
            Assert.IsFalse(ProtocolParser.TryParseServer("{\"type\":\"ack\",\"command\":\"a\",\"ok\":true,\"code\":\"x\",\"params\":{\"n\":true}}", ParseOptions.Tolerant, out _, out _));
            Assert.IsFalse(ProtocolParser.TryParseServer("{\"type\":\"ack\",\"command\":\"a\",\"ok\":true,\"code\":\"Bad Code\"}", ParseOptions.Tolerant, out _, out _));
            Assert.IsTrue(ProtocolParser.TryParseServer("{\"type\":\"ack\",\"command\":\"a\",\"ok\":true,\"code\":\"param.set\",\"params\":{\"id\":\"GF_ACTION\",\"value\":3}}", ParseOptions.Tolerant, out var m, out _));
            Assert.AreEqual("GF_ACTION", ((AckMessage)m).codeParams["id"]);
        }

        // ------------------------------------------------------------------------------------------------ U-6

        [Test]
        public void Params_And_Mission_Messages_Are_Parsed_And_Stored_By_The_Client()
        {
            var list = Server<ParamsMessage>("params.list", ParseOptions.StrictSchema);
            Assert.AreEqual(4, list.values.Length);
            Assert.AreEqual("GF_ACTION", list.values[0].id);
            Assert.AreEqual(1.0, list.values[0].value);
            Assert.AreEqual("int32", list.values[0].type);
            Assert.AreEqual(2.5, list.values[3].value);
            Assert.AreEqual("real32", list.values[3].type);
            Assert.AreEqual(1792195330400.0, list.ts);

            var ready = Server<MissionStatusMessage>("mission.status.ready", ParseOptions.StrictSchema);
            Assert.AreEqual(MissionState.Ready, ready.state);
            Assert.AreEqual("mision-demo", ready.id);
            Assert.AreEqual(3, ready.legs);
            Assert.IsFalse(ready.HasLeg, "no leg before the mission starts");
            Assert.IsTrue(ready.HasItem && ready.HasItems);
            var active = Server<MissionStatusMessage>("mission.status.active", ParseOptions.StrictSchema);
            Assert.AreEqual(MissionState.Active, active.state);
            Assert.IsTrue(active.HasLeg);
            Assert.AreEqual(2, active.leg);
            Assert.AreEqual(4, active.item);

            var src = new ManualTelemetrySource(SourceClassification.Sitl);
            var c = new TelemetryClient(src) { Log = null };
            src.Start();
            ParamsMessage seenParams = null; MissionStatusMessage seenMission = null;
            c.ParamsReceived += p => seenParams = p;
            c.MissionStatusReceived += m => seenMission = m;
            Assert.IsTrue(src.PushJson(C.Text("fixtures/hello.sitl.json"), out _));
            Assert.IsTrue(src.PushJson(C.Text("fixtures/params.list.json"), out _));
            Assert.IsTrue(src.PushJson(C.Text("fixtures/params.set.json"), out _));
            Assert.IsTrue(src.PushJson(C.Text("fixtures/mission.status.ready.json"), out _));
            Assert.IsTrue(src.PushJson(C.Text("fixtures/mission.status.active.json"), out _));
            c.Update(0, 0.016);
            Assert.AreEqual(5, c.Params.Count, "four listed + MPC_XY_VEL_MAX from the set read-back");
            Assert.AreEqual(8.0, c.Params["MPC_XY_VEL_MAX"].value);
            Assert.AreEqual(0.0, c.Params["GF_MAX_HOR_DIST"].value);
            Assert.IsNotNull(seenParams);
            Assert.AreEqual(MissionState.Active, c.MissionStatus.state);
            Assert.AreSame(c.MissionStatus, seenMission);
            // a later value replaces the earlier one
            Assert.IsTrue(src.PushJson("{\"type\":\"params\",\"ts\":1,\"params\":[{\"id\":\"GF_ACTION\",\"value\":3,\"type\":\"int32\"}]}", out _));
            c.Update(0.1, 0.016);
            Assert.AreEqual(3.0, c.Params["GF_ACTION"].value);
            // a new hello is a new session: both are forgotten
            Assert.IsTrue(src.PushJson(C.Text("fixtures/hello.sitl.json"), out _));
            c.Update(0.2, 0.016);
            Assert.AreEqual(0, c.Params.Count);
            Assert.IsNull(c.MissionStatus);
        }

        [Test]
        public void Unknown_Message_Types_Are_Still_Dropped_Without_Breaking_The_Session()
        {
            Assert.IsFalse(ProtocolParser.TryParseServer(C.Text("fixtures/invalid/message.unknown_type.json"), out _, out string err));
            StringAssert.Contains("unknown", err);
            var src = new ManualTelemetrySource(SourceClassification.Replay);
            var c = new TelemetryClient(src) { Log = null };
            src.Start();
            Assert.IsTrue(src.PushJson(C.Text("fixtures/hello.replay.json"), out _));
            Assert.IsFalse(src.PushJson("{\"type\":\"telemetry2\",\"x\":1}", out _), "a type of the future is rejected by the parser and never reaches the client");
            c.Update(0, 0.016);
            Assert.IsNotNull(c.Hello);
        }

        // ------------------------------------------------------------------------------------------------ U-7

        [Test]
        public void Param_And_Mission_Commands_Match_The_Client_Fixtures()
        {
            var cs = new Cases("fixtures client param/mission");
            void Same(string fixture, ClientMessage made)
            {
                cs.Case();
                cs.Eq(Canon(C.Text("fixtures/" + fixture + ".json")), Canon(made.ToJson()), fixture);
                cs.True(made.Validate() == null, fixture + " validates: " + made.Validate());
            }
            Same("client.command.param_list", ClientMessage.ParamList());
            Same("client.command.param_get", ClientMessage.ParamGet("GF_MAX_HOR_DIST"));
            Same("client.command.param_set", ClientMessage.ParamSet("MPC_XY_VEL_MAX", 8));
            Same("client.command.mission_start", ClientMessage.MissionStart());
            Same("client.command.mission_clear", ClientMessage.MissionClear());
            var doc = new MissionDoc { id = "mision-demo", name = "Vuelta corta", finish = "land" };
            doc.waypoints.Add(new Waypoint(10, 0, 8, 3));
            doc.waypoints.Add(new Waypoint(10, 15, 8, 3, 5));
            doc.waypoints.Add(new Waypoint(0, 15, 10, 2));
            Same("client.command.mission_set", ClientMessage.MissionSet(doc));
            cs.Done();

            // the parsed fixture gives back the mission document
            var parsed = (CommandMessage)Client("client.command.mission_set");
            Assert.AreEqual("mission_set", parsed.name);
            Assert.AreEqual(3, parsed.mission.waypoints.Count);
            Assert.AreEqual(5.0, parsed.mission.waypoints[1].holdS);
            Assert.IsNull(parsed.mission.waypoints[0].holdS);
            Assert.AreEqual("land", parsed.mission.finish);
        }

        [Test]
        public void Invalid_Param_And_Mission_Commands_Are_Refused_On_Both_Sides()
        {
            foreach (string bad in new[] { "client.param_get_bad_id", "client.param_set_without_value", "client.mission_set_empty" })
            {
                string json = C.Text("fixtures/invalid/" + bad + ".json");
                Assert.IsFalse(ProtocolParser.TryParseClient(json, ParseOptions.Tolerant, out _, out _), bad);
            }
            Assert.IsNotNull(ClientMessage.ParamGet("gf_action").Validate());
            Assert.IsNotNull(ClientMessage.ParamGet("WAY_TOO_LONG_PARAM_NAME").Validate(), "17 characters");
            Assert.IsNull(ClientMessage.ParamGet("A").Validate());
            Assert.IsNotNull(new CommandMessage { name = "param_set", id = "GF_ACTION" }.Validate(), "value is required");
            Assert.IsNotNull(new CommandMessage { name = "param_get" }.Validate(), "id is required");
            Assert.IsNotNull(ClientMessage.ParamSet("GF_ACTION", double.NaN).Validate());
            Assert.IsNotNull(ClientMessage.ParamSet("GF_ACTION", double.PositiveInfinity).Validate());
            Assert.IsNotNull(new CommandMessage { name = "mission_set" }.Validate(), "mission is required");
            Assert.IsNotNull(ClientMessage.MissionSet(new MissionDoc { finish = "land" }).Validate(), "0 waypoints");
            var tooMany = new MissionDoc { finish = "hold" };
            for (int i = 0; i < 51; i++) tooMany.waypoints.Add(new Waypoint(i, 0, 5, 3));
            Assert.IsNotNull(ClientMessage.MissionSet(tooMany).Validate(), "51 waypoints");
            var noSpeed = new MissionDoc { finish = "hold" };
            noSpeed.waypoints.Add(new Waypoint(1, 1, 5, 0));
            Assert.IsNotNull(ClientMessage.MissionSet(noSpeed).Validate(), "speed must be > 0");
            Assert.IsNotNull(ClientMessage.MissionSet(new MissionDoc { finish = "teleport", waypoints = { new Waypoint(1, 1, 5, 3) } }).Validate(), "finish");
            // a field of another command is refused
            Assert.IsNotNull(new CommandMessage { name = "land", id = "GF_ACTION" }.Validate());
            Assert.IsNotNull(new CommandMessage { name = "param_list", value = 1 }.Validate());
        }

        [Test]
        public void Mission_Set_Is_The_Only_Message_Allowed_Up_To_4096_Characters()
        {
            Assert.AreEqual(512, ClientMessage.Land().MaxChars);
            Assert.AreEqual(512, ClientMessage.ParamSet("GF_ACTION", 3).MaxChars);
            Assert.AreEqual(512, ClientMessage.Manual(0, 0, 500, 0).MaxChars);
            Assert.AreEqual(4096, ClientMessage.MissionSet(new MissionDoc()).MaxChars);
            Assert.AreEqual(4096, TelemetryProtocol.MaxMissionSetChars);

            var doc = new MissionDoc { id = "mision-grande", name = "Cincuenta waypoints", finish = "rtl" };
            for (int i = 0; i < 50; i++) doc.waypoints.Add(new Waypoint(10 + i * 3.25, -20 + i * 1.5, 8 + (i % 5), 3, i % 3 == 0 ? 5 : (double?)null));
            var m = ClientMessage.MissionSet(doc);
            Assert.IsNull(m.Validate());
            string json = m.ToJson();
            Assert.Greater(json.Length, TelemetryProtocol.MaxClientMessageChars, "a 50-waypoint mission does not fit 512");
            Assert.LessOrEqual(json.Length, TelemetryProtocol.MaxMissionSetChars, "but fits 4096 (about 3 KB)");
            // it reads back identical
            var back = (CommandMessage)ProtocolParser.ParseClient(TelemetryJson.Parse(json), ParseOptions.StrictSchema);
            Assert.AreEqual(50, back.mission.waypoints.Count);
            Assert.AreEqual(json, back.ToJson());
            // a fake socket source (the network one applies the same MaxChars) accepts it
            var src = new ManualTelemetrySource(SourceClassification.Sitl);
            src.Start();
            Assert.IsTrue(src.Send(m, out string reason), reason);
        }

        [Test]
        public void Client_Offers_Param_And_Mission_Commands_Only_When_The_Hello_Announces_Them()
        {
            // hello 1.1 style: no param_* / mission_*
            var src = new ManualTelemetrySource(SourceClassification.Sitl);
            var c = new TelemetryClient(src) { Log = null };
            src.Start();
            string h11 = As10(C.Text("fixtures/hello.sitl.json"));
            Assert.IsTrue(src.PushJson(h11, out string err), err);
            c.Update(0, 0.016);
            Assert.IsFalse(c.Hello.Accepts("param_list"));
            Assert.IsFalse(c.Send(ClientMessage.ParamList(), out string why));
            StringAssert.Contains("param_list", why);
            Assert.IsFalse(c.Send(ClientMessage.MissionStart(), out _));
            Assert.IsTrue(c.Send(ClientMessage.Land(), out _), "the old commands still go");

            // hello 1.2: announced
            Assert.IsTrue(src.PushJson(C.Text("fixtures/hello.sitl.json"), out _));
            c.Update(0.1, 0.016);
            Assert.IsTrue(c.Send(ClientMessage.ParamList(), out why), why);
            Assert.IsTrue(c.Send(ClientMessage.ParamSet("MPC_XY_VEL_MAX", 8), out why), why);
            Assert.IsTrue(c.Send(ClientMessage.MissionClear(), out why), why);
            Assert.IsTrue(src.Sent.Any(s => s.Contains("\"param_set\"")));

            // the replay does not announce them either
            var replaySrc = new ManualTelemetrySource(SourceClassification.Replay);
            var rc = new TelemetryClient(replaySrc) { Log = null };
            replaySrc.Start();
            Assert.IsTrue(replaySrc.PushJson(C.Text("fixtures/hello.replay.json"), out _));
            rc.Update(0, 0.016);
            Assert.IsFalse(rc.Send(ClientMessage.ParamSet("GF_ACTION", 3), out _));
            Assert.IsFalse(rc.Send(ClientMessage.MissionSet(new MissionDoc { waypoints = { new Waypoint(1, 1, 5, 3) } }), out _));
        }

        // ------------------------------------------------------------------------------------------------ U-8

        [Test]
        public void Px4Params_Whitelist_Is_The_Contract_File()
        {
            var file = (JObj)C.Load("px4_params.json");
            CollectionAssert.AreEqual(C.A(file, "never_write").Cast<string>().ToArray(), Px4Params.NeverWrite, "never_write");
            Assert.AreEqual(C.S(file, "id_pattern"), Px4Params.IdPattern);
            Assert.AreEqual(C.N(file, "readback_rel_tol"), Px4Params.ReadbackRelTol);
            var rules = C.A(file, "rules").Cast<JObj>().ToList();
            Assert.AreEqual(rules.Count, Px4Params.Rules.Length);
            for (int i = 0; i < rules.Count; i++)
            {
                JObj e = rules[i];
                ParamRule r = Px4Params.Rules[i];
                Assert.AreEqual(C.S(e, "id"), r.id);
                string id = r.id;
                Assert.AreEqual(C.S(e, "type"), r.type, id);
                Assert.AreEqual(RoleOf(C.S(e, "read")), r.read, id);
                Assert.AreEqual(e["write"] == null ? (ParamRole?)null : RoleOf(C.S(e, "write")), r.write, id);
                Assert.AreEqual(e.ContainsKey("min") ? C.N(e, "min") : (double?)null, r.min, id);
                Assert.AreEqual(e.ContainsKey("max") ? C.N(e, "max") : (double?)null, r.max, id);
                if (e.ContainsKey("values")) CollectionAssert.AreEqual(C.V(e, "values"), r.values, id); else Assert.IsNull(r.values, id);
                Assert.AreEqual(e.ContainsKey("unit") ? C.S(e, "unit") : null, r.unit, id);
                Assert.AreEqual(e.ContainsKey("sitlOnly") && C.B(e, "sitlOnly"), r.sitlOnly, id);
                Assert.AreEqual(C.S(e, "note"), r.note, id);
            }
        }

        static ParamRole RoleOf(string s) => s == "admin" ? ParamRole.Admin : s == "pilot" ? ParamRole.Pilot : ParamRole.Viewer;

        static void SameCheck(Cases cs, JObj expected, ParamCheck got, string what)
        {
            cs.Eq(C.B(expected, "ok"), got.ok, what + ".ok");
            if (C.B(expected, "ok"))
            {
                JObj r = C.O(expected, "rule");
                cs.True(got.rule != null && got.rule.id == C.S(r, "id") && got.rule.type == C.S(r, "type") && got.rule.note == C.S(r, "note"), what + ".rule");
                return;
            }
            cs.Eq(C.S(expected, "code"), got.code, what + ".code");
            JObj want = C.O(expected, "params");
            cs.Eq(want.Count, got.codeParams.Count, what + ".params count");
            foreach (var kv in want)
            {
                if (!got.codeParams.TryGetValue(kv.Key, out object v)) { cs.True(false, what + ".params." + kv.Key + " missing"); continue; }
                cs.True(kv.Value is double dw ? v is double dv && dv == dw : Equals(kv.Value, v), what + ".params." + kv.Key + ": expected " + kv.Value + ", got " + v);
            }
        }

        [Test]
        public void Px4Params_Golden_Read_Set_And_NeverWrite()
        {
            JObj g = C.Golden("params");
            // the list of rule names is the whitelist
            CollectionAssert.AreEqual(C.A(g, "rules").Cast<string>().ToArray(), Px4Params.Rules.Select(r => r.id).ToArray());

            var read = new Cases("params.read");
            foreach (JObj c in C.A(g, "read"))
            {
                read.Case();
                SameCheck(read, C.O(c, "out"), Px4Params.CheckRead(C.S(c, "id"), RoleOf(C.S(c, "role"))), C.S(c, "id") + "/" + C.S(c, "role"));
            }
            read.Done();

            var set = new Cases("params.set");
            foreach (JObj c in C.A(g, "set"))
            {
                set.Case();
                bool? armed = c["armed"] == null ? (bool?)null : C.B(c, "armed");
                var got = Px4Params.CheckSet(C.S(c, "id"), C.N(c, "value"), RoleOf(C.S(c, "role")), C.S(c, "profile") == "sitl", armed);
                SameCheck(set, C.O(c, "out"), got, C.S(c, "id") + "=" + C.N(c, "value") + "/" + C.S(c, "role") + "/" + C.S(c, "profile") + "/" + (armed.HasValue ? armed.Value.ToString() : "null"));
            }
            set.Done();

            var never = new Cases("params.never_write");
            foreach (JObj c in C.A(g, "never_write"))
            {
                never.Case();
                never.Eq(C.B(c, "out"), Px4Params.NeverWritten(C.S(c, "id")), C.S(c, "id"));
            }
            never.Done();
        }

        [Test]
        public void Px4Params_Never_Written_Even_If_Added_To_The_Whitelist_And_Readback()
        {
            // a calibration or an EKF2 parameter is refused by name whatever the whitelist says
            Assert.IsTrue(Px4Params.NeverWritten("CAL_GYRO0_ID"));
            Assert.IsTrue(Px4Params.NeverWritten("EKF2_GPS_CTRL"));
            Assert.IsFalse(Px4Params.NeverWritten("MPC_XY_VEL_MAX"));
            Assert.IsFalse(Px4Params.CheckSet("EKF2_GPS_CTRL", 1, ParamRole.Admin, true, false).ok);
            Assert.IsFalse(Px4Params.CheckSet("SYS_AUTOSTART", 4019, ParamRole.Admin, true, false).ok);
            // armed null (no telemetry) counts as armed
            Assert.AreEqual("param.armed", Px4Params.CheckSet("MPC_XY_VEL_MAX", 8, ParamRole.Pilot, true, null).code);
            Assert.IsTrue(Px4Params.CheckSet("MPC_XY_VEL_MAX", 8, ParamRole.Pilot, true, false).ok);
            // the bridge's read-back rule
            var real = Px4Params.Rule("MPC_XY_VEL_MAX");
            var integer = Px4Params.Rule("GF_ACTION");
            Assert.IsTrue(Px4Params.ReadbackMatches(real, 8, 8.0005));
            Assert.IsFalse(Px4Params.ReadbackMatches(real, 8, 8.01));
            Assert.IsTrue(Px4Params.ReadbackMatches(integer, 3, 3));
            Assert.IsFalse(Px4Params.ReadbackMatches(integer, 3, 2));
            Assert.IsTrue(Px4Params.ValidId("GF_ACTION"));
            Assert.IsFalse(Px4Params.ValidId("gf_action"));
        }

        // ------------------------------------------------------------------------------------------------ U-9, U-10

        [Test]
        public void Thermal_Golden_Origins_Carry_The_New_Provenance_Fields_And_Keep_Their_Outputs()
        {
            JObj g = C.Golden("thermal");
            int withPose = 0;
            foreach (JObj s in C.A(g, "telemetry_origins"))
            {
                var p = C.Provenance(s["provenance"]);
                if (p != null && p.HasPose) { withPose++; Assert.IsTrue(p.HasVelocity && p.HasGps); }
                var got = ThermalField.TelemetryOrigins(p).Select(o => WireNames.Of(o)).ToList();
                CollectionAssert.AreEqual(C.A(s, "out").Cast<string>().ToList(), got, "pose, velocity and gps do not change the thermal origins");
            }
            Assert.Greater(withPose, 0, "the regenerated golden has provenances with pose / velocity / gps");
        }

        [Test]
        public void Labels_Only_Change_The_Protocol_Version()
        {
            var l = HonestyLabels.Load();
            Assert.AreEqual("1.2.0", l.ProtocolVersion);
            // no 1.2 text was added to labels.json (CONTRATO_1_2.md section 9: all proposed, none approved)
            Assert.AreEqual(C.Text("labels.json"), System.IO.File.ReadAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath, "Scripts", "Telemetry", "Resources", "TelemetryLabels.json")));
        }
    }
}
