// Lote 1a: the LOCAL configuration of the flight simulator (SimState.cfg of the web, src/shared/flightsim.ts). It never travels on the wire: the simulator
// runs inside the app. With the defaults the model behaves exactly as it always did (contracts/golden/flightsim.json). Each tool of the lote adds its own
// members next to the geofence: wind (tool 4) and the didactic compensation of a cut motor (tool 10).
// Pure C#, no UnityEngine.

namespace WebGL.Telemetry
{
    public sealed class SimConfig : FenceConfig
    {
        /// <summary>Simulated wind (tool 4 of Lote 1a). All zero by default = no wind.</summary>
        public WindConfig wind = new WindConfig();
        /// <summary>Didactic compensation of a cut motor (tool 10 of Lote 1a, MotorFail.cs). Off by default. NOT the controller of PX4.</summary>
        public bool faultComp;
        /// <summary>What the battery does to the flight (P2 of the MVP, BatteryReact.cs): warning, return, emergency landing. On by default.</summary>
        public BatteryReactConfig battery = new BatteryReactConfig();
        /// <summary>PLAN_PC 42: the geofence acts (default) or not. Off = the model only keeps the widest envelope of the ranges (Geofence.RadiusMax / CeilingMax).</summary>
        public bool fenceOn = true;

        public SimConfig() { }

        /// <summary>Radius and ceiling in force: the fence when it is on, the widest envelope of the simulator when it is off.</summary>
        public double FenceRadius => fenceOn ? radius : Geofence.RadiusMax;
        public double FenceCeiling => fenceOn ? ceiling : Geofence.CeilingMax;

        /// <summary>A copy with every member present, nothing shared with the original.</summary>
        public SimConfig Clone() => new SimConfig { radius = radius, ceiling = ceiling, rtlAltitude = rtlAltitude, wind = wind.Clone(), faultComp = faultComp, battery = battery.Clone(), fenceOn = fenceOn };
    }

    /// <summary>What <see cref="FlightSim.SetConfig"/> takes: any members of the configuration (null = leave as is).</summary>
    public sealed class SimConfigPatch
    {
        public double? radius, ceiling, rtlAltitude;
        public WindPatch wind;
        public bool? faultComp;
        public BatteryPatch battery;
        public bool? fenceOn;
    }
}
