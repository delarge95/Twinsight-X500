// Lote 1a (PC only): what the app remembers of the simulator's local configuration between runs. Geofence: Desk_SimFence ({ radius, ceiling, rtlAltitude });
// wind: Desk_SimWind (added with tool 4). The JSON is the one the web keeps under twinsight.sim.fence.v1 / twinsight.sim.wind.v1 and anything invalid falls
// back to the defaults (Geofence.Parse). Preferences only: the configuration itself is local state of the model (SimState.cfg) and never travels on the wire.

#if UNITY_STANDALONE
using UnityEngine;

namespace WebGL.Telemetry
{
    public static class SimSettings
    {
        public const string PrefFence = "Desk_SimFence";
        public const string PrefWind = "Desk_SimWind";
        /// <summary>P2: the switch «Ignorar batería» ("0" = the battery does not act on the flight; anything else or nothing = it acts). The web keeps it under twinsight.sim.battery.react.v1.</summary>
        public const string PrefBatteryReact = "Desk_SimBatteryReact";

        public static FenceConfig LoadFence() => Geofence.Parse(PlayerPrefs.GetString(PrefFence, string.Empty));

        public static void SaveFence(FenceConfig c)
        {
            PlayerPrefs.SetString(PrefFence, Geofence.ToJson(c));
            PlayerPrefs.Save();
        }

        public static WindConfig LoadWind() => Wind.Parse(PlayerPrefs.GetString(PrefWind, string.Empty));

        public static void SaveWind(WindConfig w)
        {
            PlayerPrefs.SetString(PrefWind, Wind.ToJson(w));
            PlayerPrefs.Save();
        }

        public static bool LoadBatteryReact() => BatteryReact.ParseReact(PlayerPrefs.HasKey(PrefBatteryReact) ? PlayerPrefs.GetString(PrefBatteryReact) : null);

        public static void SaveBatteryReact(bool react)
        {
            PlayerPrefs.SetString(PrefBatteryReact, react ? "1" : "0");
            PlayerPrefs.Save();
        }

        /// <summary>The configuration a fresh simulator starts with: what was stored (defaults when nothing was).</summary>
        public static SimConfig Load()
        {
            FenceConfig f = LoadFence();
            return new SimConfig { radius = f.radius, ceiling = f.ceiling, rtlAltitude = f.rtlAltitude, wind = LoadWind(), battery = new BatteryReactConfig { react = LoadBatteryReact() } };
        }
    }
}
#endif
