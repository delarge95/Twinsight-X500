// Telemetry thermal field, pure part (web: src/shared/thermal.ts + thermalfield.ts; golden: thermal.json). The shader and the
// legend are WP-12; this is the CPU formula, the auto-range and the honesty rule: a temperature is never formatted without
// its origin (FormatReadout throws), as the web does (D-14).
//   T(p) = ambient + max_i((T_i - ambient) * exp(-|p - c_i|^2 / r_i^2))
// Pure C#.

using System;
using System.Collections.Generic;

namespace WebGL.Telemetry
{
    public struct HeatSource
    {
        public double[] pos;
        public double radius;
        public HeatSource(double[] pos, double radius) { this.pos = pos; this.radius = radius; }
    }

    public struct ThermalRange
    {
        public double min, max;
        public ThermalRange(double min, double max) { this.min = min; this.max = max; }
    }

    public enum ThermalSourceId { Telemetry, Simulation }

    public static class ThermalField
    {
        public const double RangePadLow = 3;
        public const double RangePadHigh = 5;
        public const double RangeMinSpan = 15;
        public const double RangeTauS = 1.5;
        /// <summary>Unity's fixed legend (ThermalViewController): 20-100 °C.</summary>
        public static readonly ThermalRange FixedRange = new ThermalRange(20, 100);

        public static double TemperatureAt(double[] p, IReadOnlyList<HeatSource> sources, IReadOnlyList<double> temps, double ambient)
        {
            double best = 0;
            for (int i = 0; i < sources.Count; i++)
            {
                var s = sources[i];
                double dx = p[0] - s.pos[0], dy = p[1] - s.pos[1], dz = p[2] - s.pos[2];
                double g = Math.Exp(-(dx * dx + dy * dy + dz * dz) / (s.radius * s.radius));
                best = Math.Max(best, (temps[i] - ambient) * g);
            }
            return ambient + best;
        }

        public static ThermalRange AutoRange(IReadOnlyList<double> temps, double ambient)
        {
            double lo = ambient, hi = ambient;
            foreach (var t in temps)
            {
                if (double.IsNaN(t) || double.IsInfinity(t)) continue;
                lo = Math.Min(lo, t);
                hi = Math.Max(hi, t);
            }
            double min = Math.Floor(lo - RangePadLow);
            return new ThermalRange(min, Math.Max(hi + RangePadHigh, min + RangeMinSpan));
        }

        public static ThermalRange SmoothRange(ThermalRange current, ThermalRange target, double dt, double tau = RangeTauS)
        {
            double k = 1 - Math.Exp(-Math.Max(0, dt) / tau);
            return new ThermalRange(current.min + (target.min - current.min) * k, current.max + (target.max - current.max) * k);
        }

        /// <summary>Origins of the telemetry field: FC measured when the link says so; motors/battery are model estimates.</summary>
        public static List<ThermalOrigin> TelemetryOrigins(Provenance p)
        {
            var r = new List<ThermalOrigin>();
            if (p == null) { r.Add(ThermalOrigin.Estimated); return r; }
            bool measured = false, estimated = false;
            foreach (var v in new[] { p.motorTemp, p.batteryTemp, p.fcTemp })
            {
                if (v == "measured") measured = true;
                else if (v == "estimated") estimated = true;
            }
            if (measured) r.Add(ThermalOrigin.Measured);
            if (estimated) r.Add(ThermalOrigin.Estimated);
            return r;
        }

        /// <summary>The Inspector has no telemetry, so it can only show the simulation.</summary>
        public static ThermalSourceId EffectiveSource(SourceClassification app, ThermalSourceId chosen) =>
            app == SourceClassification.Inspector ? ThermalSourceId.Simulation : chosen;

        /// <summary>The flight link is up and delivered a frame (web hasTelemetryData; connection "open").</summary>
        public static bool HasTelemetryData(SourceClassification app, bool linkOpen, bool hasFrame) =>
            app != SourceClassification.Inspector && linkOpen && hasFrame;

        /// <summary>Telemetry source without data: show "sin datos", never a colour scale of made-up values (QA-23).</summary>
        public static bool IsNoData(ThermalSourceId source, bool hasTelemetry) => source == ThermalSourceId.Telemetry && !hasTelemetry;

        /// <summary>The only way to turn a temperature into text: "42.3 °C · estimado". Throws without an origin.</summary>
        public static string FormatReadout(double tempC, IReadOnlyList<ThermalOrigin> origins, HonestyLabels labels, string lang)
        {
            if (origins == null || origins.Count == 0) throw new InvalidOperationException("a temperature must carry its origin (D-14)");
            var parts = new string[origins.Count];
            for (int i = 0; i < origins.Count; i++) parts[i] = labels.ThermalOriginText(origins[i], lang);
            return JsMath.ToFixed(tempC, 1) + " °C · " + string.Join(" / ", parts);
        }
    }
}
