// Alert rules on every displayed frame (web: src/shared/alerts.ts; golden: alerts.json).
// id, component and severity are normative; message is informative Spanish text (it becomes a translatable code in WP-03,
// README discrepancy 10), so UI code must not key anything on it. Battery thresholds are PX4 defaults; the rest are demo heuristics.
// Pure C#.

using System.Collections.Generic;

namespace WebGL.Telemetry
{
    public sealed class AlertThresholds
    {
        public double batteryLowRem = 0.15;   // PX4 BAT_LOW_THR
        public double batteryCritRem = 0.07;  // PX4 BAT_CRIT_THR
        public double motorTempWarnC = 65;
        public double motorTempCritC = 80;
        public double batteryTempWarnC = 50;
        public double batteryTempCritC = 60;
        public double fcTempWarnC = 70;
        public double vibrationWarn = 20;
        public double vibrationCrit = 30;
        /// <summary>A motor below this fraction of the mean of the others, while flying, counts as failed.</summary>
        public double motorOutRatio = 0.15;

        public static readonly AlertThresholds Default = new AlertThresholds();
    }

    public static class Alerts
    {
        public static List<Alert> Derive(TelemetryFrame f, AlertThresholds th = null)
        {
            th = th ?? AlertThresholds.Default;
            var alerts = new List<Alert>();
            bool flying = f.armed && !f.landed;

            for (int i = 0; i < 4; i++)
            {
                string motor = "motor" + (i + 1);
                double sum = 0;
                for (int j = 0; j < 4; j++) if (j != i) sum += f.rpm[j];
                double mean = sum / 3;
                if (flying && mean > 1000 && f.rpm[i] < th.motorOutRatio * mean)
                {
                    alerts.Add(new Alert
                    {
                        id = motor + ":out", component = motor, severity = AlertSeverity.Critical,
                        message = "Motor " + (i + 1) + ": rotor detenido (" + JsMath.Round(f.rpm[i]) + " RPM frente a " + JsMath.Round(mean) + " de media)",
                    });
                }
                double t = f.temp[i];
                if (t >= th.motorTempWarnC)
                {
                    alerts.Add(new Alert
                    {
                        id = motor + ":temp", component = motor, severity = t >= th.motorTempCritC ? AlertSeverity.Critical : AlertSeverity.Warning,
                        message = "Motor " + (i + 1) + ": " + JsMath.ToFixed(t, 0) + " °C",
                    });
                }
            }

            if (f.bat.rem <= th.batteryLowRem)
            {
                alerts.Add(new Alert
                {
                    id = "battery:rem", component = "battery", severity = f.bat.rem <= th.batteryCritRem ? AlertSeverity.Critical : AlertSeverity.Warning,
                    message = "Batería al " + JsMath.ToFixed(f.bat.rem * 100, 0) + " % (" + JsMath.ToFixed(f.bat.v, 1) + " V)",
                });
            }
            if (f.temp[4] >= th.batteryTempWarnC)
            {
                alerts.Add(new Alert
                {
                    id = "battery:temp", component = "battery", severity = f.temp[4] >= th.batteryTempCritC ? AlertSeverity.Critical : AlertSeverity.Warning,
                    message = "Batería: " + JsMath.ToFixed(f.temp[4], 0) + " °C",
                });
            }
            if (f.temp[5] >= th.fcTempWarnC)
            {
                alerts.Add(new Alert { id = "fc:temp", component = "fc", severity = AlertSeverity.Warning, message = "Controladora: " + JsMath.ToFixed(f.temp[5], 0) + " °C" });
            }
            if (f.vib >= th.vibrationWarn)
            {
                alerts.Add(new Alert
                {
                    id = "fc:vib", component = "fc", severity = f.vib >= th.vibrationCrit ? AlertSeverity.Critical : AlertSeverity.Warning,
                    message = "Vibración del acelerómetro: " + JsMath.ToFixed(f.vib, 1) + " m/s²",
                });
            }

            // Stable sort by severity (critical first), keeping rule order inside a severity (JS Array.sort is stable).
            var sorted = new List<Alert>(alerts.Count);
            foreach (var a in alerts) if (a.severity == AlertSeverity.Critical) sorted.Add(a);
            foreach (var a in alerts) if (a.severity == AlertSeverity.Warning) sorted.Add(a);
            return sorted;
        }

        /// <summary>What the airplane circle shows: count and worst severity.</summary>
        public static AlertSummary Summary(IReadOnlyList<Alert> alerts) => Summary(SeveritiesOf(alerts));

        public static AlertSummary Summary(IReadOnlyList<AlertSeverity> severities)
        {
            if (severities.Count == 0) return new AlertSummary { count = 0, worst = null };
            bool crit = false;
            foreach (var s in severities) if (s == AlertSeverity.Critical) crit = true;
            return new AlertSummary { count = severities.Count, worst = crit ? AlertSeverity.Critical : AlertSeverity.Warning };
        }

        /// <summary>The alert to announce when the set changes: the first (most severe) one not active before; null if none.</summary>
        public static Alert Fresh(ICollection<string> prevIds, IReadOnlyList<Alert> alerts)
        {
            foreach (var a in alerts) if (!prevIds.Contains(a.id)) return a;
            return null;
        }

        static List<AlertSeverity> SeveritiesOf(IReadOnlyList<Alert> alerts)
        {
            var r = new List<AlertSeverity>(alerts.Count);
            foreach (var a in alerts) r.Add(a.severity);
            return r;
        }
    }
}
