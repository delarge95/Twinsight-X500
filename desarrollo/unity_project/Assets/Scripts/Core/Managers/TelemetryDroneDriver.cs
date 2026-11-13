#if UNITY_STANDALONE
using System;
using System.Collections.Generic;
using UnityEngine;
using WebGL.Core.Content;
using WebGL.Telemetry;

namespace WebGL.Core.Managers
{
    /// <summary>
    /// WP-10 (PC only): drives the imported drone from the running telemetry session (<see cref="DesktopTelemetry"/>) and does nothing
    /// otherwise. While a session runs:
    ///  - POSE: the root takes the displayed frame (client.Displayed, already ground-corrected and interpolated). Position is NED -> Unity
    ///    (E, -D, N) scaled by the model's units per metre and added to the rest pose; attitude is q_body * ModelToBody about the middle
    ///    of the motors (see <see cref="DronePose"/>, the one place that states the model orientation).
    ///  - PROPELLERS: each rotor turns with Interp.AdvancePhase (rpm of its motor, spin sign from hello.rotors, the web's 0.04 visual
    ///    scale). Parts are matched to motors by their rest position, never by name.
    ///  - ALERTS: the part of every active alert (motor1..4, battery, fc) pulses red through the existing highlight shader properties.
    ///    The alerts come from the replay/SITL frames, not from the real drone.
    /// Interaction rules (documented in PLAN_PC §21):
    ///  - manual power simulation vs telemetry: telemetry wins the MOTION (DroneStateController.ExternalMotion): propellers and root
    ///    pose come from the frames. The simulated state machine still runs (state, lights, sound, thermal load) but moves nothing; on
    ///    stop the driver restores the rest pose and propeller angles and the simulation resumes where its state says.
    ///  - camera: keeps framing the drone (OrbitCameraController.SetTelemetryFollow): orbit, zoom, pan, Reset and the side-panel shift
    ///    work as at rest. Explode / isolation / filter / cut states are local to the parts or follow the root (the cut plane centres
    ///    on the root every frame), so they are untouched.
    ///  - Reset restores the rest pose when the session stops, not while it runs (the drone is where the flight is).
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class TelemetryDroneDriver : MonoBehaviour
    {
        const string RootName = "x500v2_Drone";
        static readonly string[] MotorPartNames = { "x500v2_motor_FR", "x500v2_motor_FL", "x500v2_motor_BR", "x500v2_motor_BL" };
        static readonly string[] PropPartNames = { "x500v2_prop_FR", "x500v2_prop_FL", "x500v2_prop_BR", "x500v2_prop_BL" };
        // The model has no battery body (x500v2_battery_PROXY_low is disabled): the pad and the mounting plate stand for it.
        static readonly string[] BatteryParts = { "x500v2_blend_battery_pad", "x500v2_blend_battery_mounting_plat" };
        static readonly string[] FcParts = { "x500v2_pixhawk6c", "x500v2_blend_pcb_pixhawk6c_f1", "x500v2_blend_mianke_pixhawk6c_lv_c1" };

        // Alert accent: the design token state.crit (#ff4d4d), drawn by the _AlertColor / _AlertIntensity properties of DroneLitPC (own
        // path: it is not mixed with the celeste of the selection/hover highlight, which keeps its colours). Materials without those
        // properties (the WebGL shader, view modes) fall back to the shared highlight properties with the legacy red.
        static readonly Color AlertColor = new Color32(255, 77, 77, 255);
        static readonly Color AlertColorFallback = new Color(1f, 0.16f, 0.12f, 1f);
        static readonly int AlertColorId = Shader.PropertyToID("_AlertColor");
        static readonly int AlertIntensityId = Shader.PropertyToID("_AlertIntensity");
        MaterialPropertyBlock alertBlock;

        // ── state read by the verification harness ──
        public static TelemetryDroneDriver Instance { get; private set; }
        public bool Engaged { get; private set; }
        public Transform Root => root;
        public DronePose.Rest RestPose => rest;
        public IReadOnlyCollection<string> HighlightedComponents => highlighted.Keys;
        public double[] PropPhases => phase;
        /// <summary>Faint 1 m ground grid under the flight (the studio has no world floor). On by default; WP-11 may expose it.</summary>
        public static bool ShowGroundReference = true;
        public Transform MotorPart(int motor) => motor >= 1 && motor <= 4 ? motorPart[motor] : null;
        public int LastMotorOfPart(string partName) => motorOfPart.TryGetValue(partName, out int m) ? m : 0;

        /// <summary>
        /// The part an alert component (motor1..4, battery, fc) names, for the alerts list click (WP-11): select / focus it. Null while no
        /// session has engaged the model. The battery has no body in the model: its pad stands for it (same as the red pulse).
        /// </summary>
        public Transform PartFor(string component)
        {
            if (root == null || string.IsNullOrEmpty(component)) return null;
            if (component.StartsWith("motor", StringComparison.Ordinal) && int.TryParse(component.Substring(5), out int m) && m >= 1 && m <= 4) return motorPart[m];
            if (component == "battery") return FindDeep(root, BatteryParts[0]);
            if (component == "fc") return FindDeep(root, FcParts[0]);
            return null;
        }

        Transform root;
        DronePose.Rest rest;
        DroneStateController drone;
        OrbitCameraController orbit;
        HelloMessage mappedHello;
        TelemetryGroundReference ground;

        // motor number (1..4) -> parts
        readonly Transform[] motorPart = new Transform[5];
        readonly Transform[] propPart = new Transform[5];
        readonly Quaternion[] propRestLocal = new Quaternion[5];
        readonly Vector3[] propAxisLocal = new Vector3[5];
        readonly int[] spin = { 1, 1, -1, -1, 1 };
        readonly double[] phase = new double[5];
        readonly Dictionary<string, int> motorOfPart = new Dictionary<string, int>();
        // World positions of the parts at REST, captured once per engagement (the model moves with the flight: never read them later).
        readonly Dictionary<string, Vector3> restPosByName = new Dictionary<string, Vector3>();
        readonly Dictionary<string, (Quaternion rotation, Vector3 axis)> propRestByName = new Dictionary<string, (Quaternion rotation, Vector3 axis)>();

        // alert highlight bookkeeping: component -> highlighted transforms (so they can be released)
        readonly Dictionary<string, List<HighlightSystem>> highlighted = new Dictionary<string, List<HighlightSystem>>();
        readonly HashSet<string> wanted = new HashSet<string>();
        readonly List<string> release = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Instance != null) return;
            var go = new GameObject("_TelemetryDroneDriver") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<TelemetryDroneDriver>();
        }

        void Update()
        {
            var client = DesktopTelemetry.Client;
            if (client == null)
            {
                if (Engaged) Disengage();
                return;
            }

            // No frame yet (a source that just started or was switched, or a link that never opened): the drone is at rest, never frozen
            // in the last pose of the previous session (WP-11: switching from the replay to SITL while it connects).
            var f = client.Displayed;
            if (f == null)
            {
                if (Engaged) Disengage();
                return;
            }

            if (!Engaged && !TryEngage()) return;
            if (client.Hello != null && !ReferenceEquals(client.Hello, mappedHello)) BuildMapping(client.Hello);
            if (root == null) return;

            ApplyPose(f);
            if (ground != null) ground.Follow(DronePose.BodyPosition(rest, f.p));
            ApplyPropellers(client, f, Time.unscaledDeltaTime);
            ApplyAlerts(client);
        }

        void OnDisable()
        {
            if (Engaged) Disengage();
        }

        // ──────────────────────────────────────────────── engage / disengage

        bool TryEngage()
        {
            var rootGo = GameObject.Find(RootName);
            if (rootGo == null) return false;
            root = rootGo.transform;
            drone = DroneStateController.Instance;
            orbit = OrbitCameraController.Instance;

            var motors = new List<Vector3>();
            foreach (string n in MotorPartNames)
            {
                var t = FindDeep(root, n);
                if (t == null) { Debug.LogWarning("[TelemetryDrone] missing " + n + ": pose not applied"); root = null; return false; }
                motors.Add(t.position);
            }

            restPosByName.Clear();
            for (int i = 0; i < 4; i++) restPosByName[MotorPartNames[i]] = motors[i];
            foreach (string n in PropPartNames) { var tp = FindDeep(root, n); if (tp != null) restPosByName[n] = tp.position; }

            var geometry = QuadX.Px4QuadX;
            rest = new DronePose.Rest { RootPosition = root.position, RootRotation = root.rotation };
            rest.UnitsPerMeter = DronePose.UnitsPerMeter(motors.ToArray(), geometry, out Vector3 centre);
            rest.BodyOrigin = centre;

            // Propellers: rest rotation and spin axis (world up at rest, in the part's local space), captured once per engagement.
            propRestByName.Clear();
            foreach (string n in PropPartNames)
            {
                var t = FindDeep(root, n);
                if (t != null) propRestByName[n] = (t.localRotation, t.InverseTransformDirection(Vector3.up).normalized);
            }

            if (ShowGroundReference)
            {
                // Ground = underside of the landing gear at rest (altitude 0 of the flight).
                float feet = float.PositiveInfinity;
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                    if (r.enabled && r.gameObject.activeInHierarchy) feet = Mathf.Min(feet, r.bounds.min.y);
                if (!float.IsInfinity(feet)) ground = TelemetryGroundReference.Create(rest.UnitsPerMeter, feet);
            }

            mappedHello = null;
            BuildMapping(null);
            if (drone != null) drone.ExternalMotion = true;
            if (orbit != null) orbit.SetTelemetryFollow(true, root, rest.RootRotation);
            Engaged = true;
            Debug.Log("[TelemetryDrone] engaged: model units per metre = " + rest.UnitsPerMeter.ToString("F2") + ", body origin = " + rest.BodyOrigin.ToString("F3") +
                      ", model nose = -Z (correction = Ry(180))");
            return true;
        }

        void Disengage()
        {
            ReleaseAllAlerts();
            if (ground != null) { Destroy(ground.gameObject); ground = null; }
            // Rest pose and propeller angles.
            if (root != null)
            {
                root.SetPositionAndRotation(rest.RootPosition, rest.RootRotation);
                for (int m = 1; m <= 4; m++) if (propPart[m] != null) propPart[m].localRotation = propRestLocal[m];
            }
            if (drone != null) drone.ExternalMotion = false;
            if (orbit != null) orbit.SetTelemetryFollow(false);
            Engaged = false;
            Debug.Log("[TelemetryDrone] disengaged: rest pose restored");
        }

        /// <summary>Matches parts to motors (rest position vs hello.rotors) and reads spin signs. Safe to call again on a new hello.</summary>
        void BuildMapping(HelloMessage hello)
        {
            mappedHello = hello;
            var rotors = hello != null && hello.rotors != null && hello.rotors.Length == 4 ? hello.rotors : QuadX.Px4QuadX;
            if (hello != null)
            {
                // The geometry of the hello decides the scale too (same model, different rotor spacing -> different scale); REST positions only.
                var motors = new List<Vector3>();
                foreach (string n in MotorPartNames) if (restPosByName.TryGetValue(n, out var rp)) motors.Add(rp);
                if (motors.Count == 4)
                {
                    float k = DronePose.UnitsPerMeter(motors.ToArray(), rotors, out _);
                    if (k > 0f && !float.IsNaN(k)) rest.UnitsPerMeter = k;
                }
            }

            motorOfPart.Clear();
            Array.Clear(motorPart, 0, motorPart.Length);
            Array.Clear(propPart, 0, propPart.Length);
            AssignParts(MotorPartNames, rotors, motorPart);
            AssignParts(PropPartNames, rotors, propPart);
            for (int m = 1; m <= 4; m++)
            {
                if (propPart[m] == null || !propRestByName.TryGetValue(propPart[m].name, out var pr)) continue;
                propRestLocal[m] = pr.rotation;
                propAxisLocal[m] = pr.axis;
            }
            foreach (var r in rotors) if (r.motor >= 1 && r.motor <= 4) spin[r.motor] = QuadX.SpinSign(r);
        }

        void AssignParts(string[] names, RotorGeometry[] rotors, Transform[] byMotor)
        {
            var parts = new List<Transform>();
            foreach (string n in names) { var t = FindDeep(root, n); if (t != null) parts.Add(t); }
            if (parts.Count != 4) return;
            var pos = new Vector3[4];
            for (int i = 0; i < 4; i++) pos[i] = restPosByName.TryGetValue(parts[i].name, out var rp) ? rp : parts[i].position;
            int[] motorOf;
            try { motorOf = DronePose.MotorOfParts(pos, rest.BodyOrigin, rotors); }
            catch (Exception e) { Debug.LogWarning("[TelemetryDrone] part to motor match failed: " + e.Message); return; }
            for (int i = 0; i < 4; i++)
            {
                int m = motorOf[i];
                if (m >= 1 && m <= 4) { byMotor[m] = parts[i]; motorOfPart[parts[i].name] = m; }
            }
        }

        // ──────────────────────────────────────────────── per frame

        void ApplyPose(TelemetryFrame f)
        {
            DronePose.Root(rest, f.p, f.q, out Vector3 pos, out Quaternion rot);
            root.SetPositionAndRotation(pos, rot);
        }

        void ApplyPropellers(TelemetryClient client, TelemetryFrame f, float dt)
        {
            for (int m = 1; m <= 4; m++)
            {
                var p = propPart[m];
                if (p == null) continue;
                phase[m] = Interp.AdvancePhase(phase[m], f.rpm[m - 1], spin[m], dt, DronePose.PropVisualScale);
                float deg = DronePose.PropAngleDegrees(phase[m], 1); // spin sign already folded into the phase direction
                p.localRotation = propRestLocal[m] * Quaternion.AngleAxis(deg, propAxisLocal[m]);
            }
        }

        void ApplyAlerts(TelemetryClient client)
        {
            wanted.Clear();
            float t = Time.unscaledTime;
            var list = client.Alerts;
            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                if (string.IsNullOrEmpty(a.component)) continue;
                wanted.Add(a.component);
                bool critical = a.severity == AlertSeverity.Critical;
                float hz = critical ? 1.8f : 1.0f;
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * hz * Mathf.PI * 2f);
                float intensity = Mathf.Lerp(critical ? 0.65f : 0.40f, 1f, pulse);
                if (!highlighted.TryGetValue(a.component, out var systems))
                {
                    systems = ResolveSystems(a.component);
                    highlighted[a.component] = systems;
                    Debug.Log("[TelemetryDrone] alert highlight on " + a.component + " (" + a.id + ", " + a.severity + "): " + systems.Count + " part(s)");
                }
                foreach (var hs in systems)
                {
                    if (hs == null) continue;
                    SetAlert(hs, intensity);
                }
            }

            release.Clear();
            foreach (var kv in highlighted) if (!wanted.Contains(kv.Key)) release.Add(kv.Key);
            foreach (string c in release) ReleaseAlert(c);
        }

        List<HighlightSystem> ResolveSystems(string component)
        {
            var result = new List<HighlightSystem>();
            var names = new List<string>();
            if (component.StartsWith("motor", StringComparison.Ordinal) && int.TryParse(component.Substring(5), out int m) && m >= 1 && m <= 4)
            {
                if (motorPart[m] != null) names.Add(motorPart[m].name);
            }
            else if (component == "battery") names.AddRange(BatteryParts);
            else if (component == "fc") names.AddRange(FcParts);

            foreach (string n in names)
            {
                var t = FindDeep(root, n);
                if (t == null) continue;
                var hs = t.GetComponent<HighlightSystem>();
                if (hs == null) hs = t.gameObject.AddComponent<HighlightSystem>(); // its Awake adds the MaterialController
                result.Add(hs);
            }
            return result;
        }

        /// <summary>Paints (intensity &gt; 0) or clears (0) the alert accent of a part. Reads the renderer's property block first, so a selection or hover on the same part keeps its own values.</summary>
        void SetAlert(HighlightSystem hs, float intensity)
        {
            var mc = hs.GetComponent<MaterialController>();
            Renderer[] rs = mc != null && mc.Renderers != null && mc.Renderers.Length > 0 ? mc.Renderers : hs.GetComponentsInChildren<Renderer>(true);
            if (alertBlock == null) alertBlock = new MaterialPropertyBlock();
            bool fallback = false;
            for (int i = 0; i < rs.Length; i++)
            {
                Renderer r = rs[i];
                if (r == null) continue;
                Material m = r.sharedMaterial;
                if (m == null || !m.HasProperty(AlertIntensityId)) { fallback = true; continue; }
                r.GetPropertyBlock(alertBlock);
                alertBlock.SetColor(AlertColorId, AlertColor);
                alertBlock.SetFloat(AlertIntensityId, intensity);
                r.SetPropertyBlock(alertBlock);
            }
            if (fallback && intensity > 0f && mc != null) mc.SetColor(AlertColorFallback, intensity);
        }

        void ReleaseAlert(string component)
        {
            if (highlighted.TryGetValue(component, out var systems))
                foreach (var hs in systems)
                {
                    if (hs == null) continue;
                    SetAlert(hs, 0f);
                    hs.RefreshVisualTargets(); // back to selection / hover / rest
                }
            highlighted.Remove(component);
        }

        void ReleaseAllAlerts()
        {
            release.Clear();
            foreach (var k in highlighted.Keys) release.Add(k);
            foreach (string c in release) ReleaseAlert(c);
        }

        static Transform FindDeep(Transform parent, string name)
        {
            if (parent == null) return null;
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var r = FindDeep(parent.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
#endif
