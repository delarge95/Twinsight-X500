#if UNITY_STANDALONE
using UnityEngine;
using WebGL.Core.Managers;
using WebGL.Telemetry;

namespace WebGL.Core.Thermal
{
    /// <summary>
    /// MVP P3 (PC only, never WebGL): the quantitative thermal field of the TELEMETRY on the drone, drawn by the shader <c>TwinSight/ThermalFieldPC</c>
    /// (material <c>Resources/PC/Materials/PC_Thermal</c>, hooked in <see cref="ViewModeManager"/> like the other PC view materials). The formula is the one of the web
    /// (src/shared/thermal.ts, golden contracts/golden/thermal.json; the C# <see cref="ThermalField"/> that passes it is reused here for the range and the CPU readout):
    ///   T(p) = T_amb + max_i((T_i - T_amb) * exp(-|p - c_i|^2 / r_i^2)),   6 sources: motor 1..4 (r 0.045 m), battery (0.09 m), flight controller (0.05 m).
    /// Every frame, while the Thermal mode is on and a session runs with the «Telemetría» source selected: the six temperatures of the displayed frame, the ambient of the
    /// hello and the auto-range (<see cref="ThermalField.AutoRange"/> smoothed by <see cref="ThermalField.SmoothRange"/>) go to the material; the source centres are the mesh bounds
    /// of the parts in the root's local space (rest pose, so they follow the drone), radii metres x units per metre. With the «Simulación didáctica» source, or with no session
    /// (the Inspector has no telemetry), nothing changes: the didactic simulation of <see cref="ThermalViewController"/> keeps the Thermal mode. A telemetry session that has not
    /// delivered a frame yet shows a neutral grey, never a scale of made-up values. Honesty: the temperatures are what the session says (measured / estimated / simulated by origin,
    /// see DesktopTelemetryUI.Thermal.cs); this component only draws them.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public sealed class ThermalFieldPC : MonoBehaviour
    {
        public const string PrefSource = "Desk_ThermalSource";
        /// <summary>The heat sources in the order of TelemetryFrame.temp: motor 1..4, battery, flight controller.</summary>
        public static readonly string[] SourceComponents = { "motor1", "motor2", "motor3", "motor4", "battery", "fc" };
        /// <summary>Their spreads, metres (web Drone.tsx).</summary>
        public static readonly double[] RadiiM = { 0.045, 0.045, 0.045, 0.045, 0.09, 0.05 };

        static readonly int RootInvId = Shader.PropertyToID("_TF_RootInv");
        static readonly int SrcId = Shader.PropertyToID("_TF_Src");
        static readonly int TempsId = Shader.PropertyToID("_TF_Temps");
        static readonly int AmbientId = Shader.PropertyToID("_TF_Ambient");
        static readonly int RangeId = Shader.PropertyToID("_TF_Range");
        static readonly int NoDataId = Shader.PropertyToID("_TF_NoData");

        public static ThermalFieldPC Instance { get; private set; }
        /// <summary>The field is what the Thermal mode paints right now (and the legend is the field's).</summary>
        public static bool Active { get; private set; }

        readonly Vector4[] src = new Vector4[6];
        readonly float[] temps = new float[6];
        readonly bool[] haveSource = new bool[6];
        bool sourcesReady;
        bool rangeInit;
        ThermalRange range = ThermalField.FixedRange;
        double ambient = 22;
        bool noData;
        ThermalSourceId chosen = ThermalSourceId.Telemetry;

        /// <summary>The source the person chose (remembered). Whether it applies also depends on the session: see <see cref="EffectiveSource"/>.</summary>
        public static ThermalSourceId Chosen
        {
            get => Instance != null ? Instance.chosen : ThermalSourceId.Telemetry;
            set
            {
                if (Instance == null) return;
                Instance.chosen = value;
                PlayerPrefs.SetString(PrefSource, value == ThermalSourceId.Simulation ? "sim" : "telemetry");
                PlayerPrefs.Save();
            }
        }

        /// <summary>The source in force: the Inspector (no session) can only show the simulation.</summary>
        public static ThermalSourceId EffectiveSource =>
            ThermalField.EffectiveSource(DesktopTelemetry.IsRunning && DesktopTelemetry.Client != null ? DesktopTelemetry.Client.Classification : SourceClassification.Inspector, Chosen);

        // ── read by the legend and by the verification harness ──
        public ThermalRange Range => range;
        public double Ambient => ambient;
        public bool NoData => noData;
        public float TempOf(int i) => temps[i];
        public bool SourceKnown(int i) => haveSource[i];
        public Vector3 SourceCentreLocal(int i) => new Vector3(src[i].x, src[i].y, src[i].z);
        public float SourceRadiusLocal(int i) => src[i].w;
        public Material Material => ViewModeManager.Instance != null ? ViewModeManager.Instance.ThermalFieldMaterial : null;

        /// <summary>The field at a point of the root's local space, from the C# formula the golden thermal.json tests (the shader runs the same one).</summary>
        public double TemperatureAtLocal(Vector3 p)
        {
            var srcs = new System.Collections.Generic.List<HeatSource>(6);
            var ts = new System.Collections.Generic.List<double>(6);
            for (int i = 0; i < 6; i++)
            {
                srcs.Add(new HeatSource(new double[] { src[i].x, src[i].y, src[i].z }, src[i].w));
                ts.Add(temps[i]);
            }
            return ThermalField.TemperatureAt(new double[] { p.x, p.y, p.z }, srcs, ts, ambient);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Instance != null) return;
            var go = new GameObject("_ThermalFieldPC") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<ThermalFieldPC>();
            Instance.chosen = PlayerPrefs.GetString(PrefSource, "telemetry") == "sim" ? ThermalSourceId.Simulation : ThermalSourceId.Telemetry;
        }

        void Update()
        {
            var vm = ViewModeManager.Instance;
            bool thermal = vm != null && vm.CurrentMode == ViewMode.Thermal;
            bool session = DesktopTelemetry.IsRunning && DesktopTelemetry.Client != null;
            bool want = thermal && session && EffectiveSource == ThermalSourceId.Telemetry && vm.ThermalFieldMaterial != null;
            if (want != Active)
            {
                Active = want;
                ViewModeManager.ThermalFieldRequested = want;
                ThermalViewController.FieldActive = want;
                if (vm != null && thermal) vm.ReapplyCurrentMode(); // swap the material of every renderer (the other way round too)
                if (!want) rangeInit = false;
            }
            if (!Active) return;
            Apply(vm.ThermalFieldMaterial);
        }

        bool EnsureSources()
        {
            var drv = TelemetryDroneDriver.Instance;
            if (drv == null || !drv.Engaged || drv.Root == null) return false;
            if (sourcesReady) return true;
            float local = drv.UnitsPerMeter / Mathf.Max(1e-6f, drv.Root.lossyScale.x);
            bool all = true;
            for (int i = 0; i < 6; i++)
            {
                haveSource[i] = drv.TryComponentCentreLocal(SourceComponents[i], out Vector3 c);
                if (!haveSource[i]) { all = false; c = new Vector3(999f, 999f, 999f); }
                src[i] = new Vector4(c.x, c.y, c.z, (float)RadiiM[i] * local);
            }
            sourcesReady = all;
            return true;
        }

        void Apply(Material m)
        {
            var client = DesktopTelemetry.Client;
            var drv = TelemetryDroneDriver.Instance;
            TelemetryFrame f = client.Displayed;
            noData = ThermalField.IsNoData(ThermalSourceId.Telemetry, ThermalField.HasTelemetryData(client.Classification, f != null, f != null)) || !EnsureSources() || drv == null || drv.Root == null;
            m.SetFloat(NoDataId, noData ? 1f : 0f);
            if (noData) return;

            ambient = client.Hello != null && client.Hello.ambientC > -100 ? client.Hello.ambientC : 22;
            var list = new double[6];
            for (int i = 0; i < 6; i++)
            {
                double t = f.temp != null && i < f.temp.Length ? f.temp[i] : double.NaN;
                if (double.IsNaN(t) || double.IsInfinity(t)) t = ambient; // a missing figure draws no heat
                list[i] = t;
                temps[i] = (float)t;
            }
            ThermalRange target = ThermalField.AutoRange(list, ambient);
            range = rangeInit ? ThermalField.SmoothRange(range, target, Time.unscaledDeltaTime) : target;
            rangeInit = true;

            m.SetMatrix(RootInvId, drv.Root.worldToLocalMatrix);
            m.SetVectorArray(SrcId, src);
            m.SetFloatArray(TempsId, temps);
            m.SetFloat(AmbientId, (float)ambient);
            m.SetVector(RangeId, new Vector4((float)range.min, (float)range.max, 0.12f, 0f)); // 0.12 = PALETTE_FLOOR of thermal.ts
        }
    }
}
#endif
