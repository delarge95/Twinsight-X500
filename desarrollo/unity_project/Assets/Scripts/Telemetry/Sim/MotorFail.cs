// Lote 1a, tool 10: «motor cortado» of the flight simulator, a line-by-line port of src/shared/motorfail.ts. A simplified rigid-body dynamics that replaces the
// kinematic part of FlightSim while a motor is cut. Pure and deterministic C# (no UnityEngine, no clock, no randomness), reproduced by
// contracts/golden/motorfail.json. It is NOT PX4 and NOT a real drone: the constants are plausible and UNCALIBRATED, except the hover command (0.62),
// which was measured in a real flight (docs/CALIBRACION_MODELO_FALLAS.md of the web). Everything it produces is shown as "simulado".
//
// Axes: world NED (z down, the ground is z = 0), body FRD. q = [w,x,y,z] body -> world (like the frame). w = [p, q, r] angular rates in the body, rad/s
// (r > 0 = clockwise yaw seen from above).

using System;

namespace WebGL.Telemetry
{
    /// <summary>Constants of the model, each with its source (the same table as MOTORFAIL_PARAMS of the web).</summary>
    public static class MotorFailParams
    {
        /// <summary>kg. Base mass of the Gazebo x500 (PLAN_CONTROL_REAL_Y_ENTORNOS 1.4, px4_bridge.py).</summary>
        public const double Mass = 2.0;
        public const double Gravity = 9.80665;
        /// <summary>N per rotor: k_T * w_max^2 of the Gazebo model.</summary>
        public const double ThrustMax = 8.55;
        /// <summary>N per rotor at hover: m*g/4.</summary>
        public const double ThrustHover = Mass * Gravity / 4;
        /// <summary>Motor command at hover, MEASURED in the real flight (0.62; p10-p90 0.608-0.629). Single source: SimLimits.HoverThrottle reads it.</summary>
        public const double HoverThrottle = 0.62;
        /// <summary>kg*m^2. Plausible inertias for a 2 kg, 500 mm quadcopter (uncalibrated).</summary>
        public const double Ixx = 0.022, Iyy = 0.022, Izz = 0.040;
        /// <summary>N*m*s^2. QUADRATIC yaw damping, tau = -c_q * r * |r|. Chosen so the terminal yaw with compensation, sqrt(2*km*T_max/c_q), is about 15 rad/s.</summary>
        public const double YawDragQuad = 0.0038;
        /// <summary>1/s. Roll and pitch damping (uncalibrated).</summary>
        public const double TiltDamping = 8;
        /// <summary>1/s. Linear drag of the velocity (uncalibrated).</summary>
        public const double LinearDrag = 0.6;
        /// <summary>Degrees. Maximum tilt of the model: the full flip is not modelled.</summary>
        public const double MaxTiltDeg = 80;
        /// <summary>s. With compensation the initial tilt levels out with this lag (the tiltTau of the kinematic model).</summary>
        public const double CompLevelTau = 0.14;
        /// <summary>m. Below this height a cut motor cannot be restored.</summary>
        public const double RestoreMinAltitude = 0.5;
        /// <summary>m/s. From this speed the ground contact is an "impact"; below it a "forced landing".</summary>
        public const double ImpactSpeed = 3;
        /// <summary>Hz. Step of the client (semi-implicit Euler).</summary>
        public const double RateHz = 120;

        /// <summary>The motor of the same spin direction that is also shut down with compensation (1&lt;-&gt;2, 3&lt;-&gt;4); index = motor (1..4).</summary>
        public static int Opposite(int motor) => motor == 1 ? 2 : motor == 2 ? 1 : motor == 3 ? 4 : 3;

        public static RotorGeometry[] Rotors => QuadX.Px4QuadX;
    }

    /// <summary>The vehicle state a cut starts from (radians, ZYX, like SimState).</summary>
    public struct MotorFailSnapshot
    {
        public double[] p, v;
        public double roll, pitch, yaw, yawRate;
    }

    public sealed class MotorFailImpact
    {
        /// <summary>Speed at ground contact, m/s.</summary>
        public double speed;
        public bool hard;
        /// <summary>"critical" for an impact, "warning" for a forced landing.</summary>
        public bool critical;
        public string text;
    }

    public sealed class MotorFailState
    {
        /// <summary>Seconds since the cut.</summary>
        public double t;
        public double[] q = new double[4];
        public double[] w = new double[3];
        public double[] p = new double[3];
        public double[] v = new double[3];
        /// <summary>The cut motor, 1..4.</summary>
        public int motor;
        public bool comp;
        public bool landed;
        public MotorFailImpact impact;
    }

    /// <summary>Back to the kinematic model: hold at this point, keeping the velocity (null when it cannot be done).</summary>
    public sealed class MotorFailRestore
    {
        public double[] p, v;
        public double roll, pitch, yaw, yawRate;
        public SimTarget target;
    }

    public static class MotorFail
    {
        const double Deg = Math.PI / 180;

        static double[] EulerToQuat(double roll, double pitch, double yaw) => FlightSim.EulerToQuat(roll, pitch, yaw);

        static double[] QMul(double[] a, double[] b) => new[]
        {
            a[0] * b[0] - a[1] * b[1] - a[2] * b[2] - a[3] * b[3],
            a[0] * b[1] + a[1] * b[0] + a[2] * b[3] - a[3] * b[2],
            a[0] * b[2] - a[1] * b[3] + a[2] * b[0] + a[3] * b[1],
            a[0] * b[3] + a[1] * b[2] - a[2] * b[1] + a[3] * b[0],
        };

        static double[] QNorm(double[] q)
        {
            double n = Math.Sqrt(q[0] * q[0] + q[1] * q[1] + q[2] * q[2] + q[3] * q[3]);
            return new[] { q[0] / n, q[1] / n, q[2] / n, q[3] / n };
        }

        /// <summary>Rotates a body vector to the world.</summary>
        public static double[] Rotate(double[] q, double[] v)
        {
            double w = q[0], x = q[1], y = q[2], z = q[3];
            double tx = 2 * (y * v[2] - z * v[1]);
            double ty = 2 * (z * v[0] - x * v[2]);
            double tz = 2 * (x * v[1] - y * v[0]);
            return new[] { v[0] + w * tx + (y * tz - z * ty), v[1] + w * ty + (z * tx - x * tz), v[2] + w * tz + (x * ty - y * tx) };
        }

        /// <summary>Tilt (angle between the body z axis and the vertical), rad.</summary>
        public static double Tilt(double[] q)
        {
            double[] b = Rotate(q, new double[] { 0, 0, 1 });
            return Math.Atan2(Math.Sqrt(b[0] * b[0] + b[1] * b[1]), b[2]);
        }

        /// <summary>Takes the tilt to `tilt` (smaller than the current one) turning about the horizontal axis of the tilt.</summary>
        static double[] WithTilt(double[] q, double tilt)
        {
            double[] b = Rotate(q, new double[] { 0, 0, 1 });
            double h = Math.Sqrt(b[0] * b[0] + b[1] * b[1]);
            if (h < 1e-12) return q;
            double d = Math.Atan2(h, b[2]) - tilt;
            double s = Math.Sin(d / 2) / h;
            // ez x b = (-b_y, b_x, 0) increases the tilt: turn -d about that axis (a rotation in world axes).
            return QNorm(QMul(new[] { Math.Cos(d / 2), b[1] * s, -b[0] * s, 0 }, q));
        }

        /// <summary>Thrust of one rotor for a command u: min(T_max, T_h * (u / hoverThrottle)^2).</summary>
        public static double Thrust(double u) => Math.Min(MotorFailParams.ThrustMax, MotorFailParams.ThrustHover * Math.Pow(u / MotorFailParams.HoverThrottle, 2));

        /// <summary>Motor commands u (0..1) of the model: 0 on the cut one; without compensation the other three hold; with it the opposite is shut down too and the two left go to the maximum.</summary>
        public static double[] Command(int motor, bool comp)
        {
            int other = MotorFailParams.Opposite(motor);
            var rotors = MotorFailParams.Rotors;
            var u = new double[4];
            for (int i = 0; i < 4; i++)
            {
                int m = rotors[i].motor;
                if (m == motor) u[i] = 0;
                else if (!comp) u[i] = MotorFailParams.HoverThrottle;
                else u[i] = m == other ? 0 : 1;
            }
            return u;
        }

        /// <summary>Thrusts (N) and torques in the FRD body (N*m): tau_x = -sum(T*py), tau_y = sum(T*px), tau_z = sum(km*T).</summary>
        public static void Torques(int motor, bool comp, out double[] thrust, out double total, out double[] tau)
        {
            double[] u = Command(motor, comp);
            thrust = new double[4];
            double tx = 0, ty = 0, tz = 0;
            total = 0;
            var rotors = MotorFailParams.Rotors;
            for (int i = 0; i < 4; i++)
            {
                thrust[i] = Thrust(u[i]);
                tx -= thrust[i] * rotors[i].py;
                ty += thrust[i] * rotors[i].px;
                tz += thrust[i] * rotors[i].km;
                total += thrust[i];
            }
            tau = new[] { tx, ty, tz };
        }

        /// <summary>Terminal yaw rate (rad/s, signed) of the torque `tauZ` against the quadratic damping: sign(tau) * sqrt(|tau| / c_q).</summary>
        public static double YawTerminal(double tauZ) => Math.Sign(tauZ) * Math.Sqrt(Math.Abs(tauZ) / MotorFailParams.YawDragQuad);

        public static MotorFailState Start(MotorFailSnapshot snap, int motor, bool comp)
        {
            if (motor < 1 || motor > 4) throw new ArgumentException("motor outside 1..4: " + motor);
            return new MotorFailState
            {
                t = 0,
                q = EulerToQuat(snap.roll, snap.pitch, snap.yaw),
                w = new[] { 0, 0, snap.yawRate },
                p = new[] { snap.p[0], snap.p[1], snap.p[2] },
                v = new[] { snap.v[0], snap.v[1], snap.v[2] },
                motor = motor,
                comp = comp,
                landed = false,
                impact = null,
            };
        }

        static string Fmt1(double x) => JsMath.ToFixed(x, 1).Replace('.', ',');

        /// <summary>One semi-implicit Euler step (rates first, then attitude and position with the new ones). Mutates and returns `s`.</summary>
        public static MotorFailState Step(MotorFailState s, double dt)
        {
            if (s.landed || !(dt > 0)) return s;
            Torques(s.motor, s.comp, out _, out double total, out double[] tau);

            // Angular rates in the body.
            s.w[0] += (tau[0] / MotorFailParams.Ixx - MotorFailParams.TiltDamping * s.w[0]) * dt;
            s.w[1] += (tau[1] / MotorFailParams.Iyy - MotorFailParams.TiltDamping * s.w[1]) * dt;
            s.w[2] += ((tau[2] - MotorFailParams.YawDragQuad * s.w[2] * Math.Abs(s.w[2])) / MotorFailParams.Izz) * dt;

            // Attitude: the exact rotation of the step (stable even when the yaw spins very fast).
            double rate = Math.Sqrt(s.w[0] * s.w[0] + s.w[1] * s.w[1] + s.w[2] * s.w[2]);
            if (rate > 1e-12)
            {
                double k = Math.Sin(rate * dt / 2) / rate;
                s.q = QNorm(QMul(s.q, new[] { Math.Cos(rate * dt / 2), s.w[0] * k, s.w[1] * k, s.w[2] * k }));
            }
            double tilt = Tilt(s.q);
            if (s.comp)
            {
                // Didactic compensation: roll and pitch torques are zero; the tilt it carried levels out with CompLevelTau.
                if (tilt > 0) s.q = WithTilt(s.q, tilt * Math.Max(0, 1 - dt / MotorFailParams.CompLevelTau));
            }
            else if (tilt > MotorFailParams.MaxTiltDeg * Deg)
            {
                s.q = WithTilt(s.q, MotorFailParams.MaxTiltDeg * Deg);
                s.w[0] = 0;
                s.w[1] = 0;
            }

            // Translation: a = (0, 0, g) + R(q) * (0, 0, -sum T) / m - c_v * v.
            double[] f = Rotate(s.q, new[] { 0, 0, -total / MotorFailParams.Mass });
            s.v[0] += (f[0] - MotorFailParams.LinearDrag * s.v[0]) * dt;
            s.v[1] += (f[1] - MotorFailParams.LinearDrag * s.v[1]) * dt;
            s.v[2] += (MotorFailParams.Gravity + f[2] - MotorFailParams.LinearDrag * s.v[2]) * dt;
            s.p[0] += s.v[0] * dt;
            s.p[1] += s.v[1] * dt;
            s.p[2] += s.v[2] * dt;
            s.t += dt;

            if (s.p[2] >= 0)
            {
                double speed = Math.Sqrt(s.v[0] * s.v[0] + s.v[1] * s.v[1] + s.v[2] * s.v[2]);
                bool hard = speed >= MotorFailParams.ImpactSpeed;
                s.impact = new MotorFailImpact
                {
                    speed = speed,
                    hard = hard,
                    critical = hard,
                    text = (hard ? "Impacto simulado" : "Aterrizaje forzoso simulado") + " a " + Fmt1(speed) + " m/s",
                };
                s.p[2] = 0;
                s.v = new double[3];
                s.w = new double[3];
                s.landed = true;
            }
            return s;
        }

        /// <summary>Roll, pitch and yaw (ZYX, rad) of the current attitude.</summary>
        public static void Euler(double[] q, out double roll, out double pitch, out double yaw)
        {
            double w = q[0], x = q[1], y = q[2], z = q[3];
            roll = Math.Atan2(2 * (w * x + y * z), 1 - 2 * (x * x + y * y));
            pitch = Math.Asin(Math.Min(1, Math.Max(-1, 2 * (w * y - z * x))));
            yaw = Math.Atan2(2 * (w * z + x * y), 1 - 2 * (y * y + z * z));
        }

        /// <summary>Motor commands u of the frame: 0 on the shut-down motors (and on all of them after the ground contact).</summary>
        public static double[] U(MotorFailState s) => s.landed ? new double[4] : Command(s.motor, s.comp);

        /// <summary>Back to the kinematic model: null if it cannot be done (on the ground or at 0.5 m or less); otherwise hold at the point, keeping the velocity.</summary>
        public static MotorFailRestore Restore(MotorFailState s)
        {
            double alt = -s.p[2];
            if (s.landed || !(alt > MotorFailParams.RestoreMinAltitude)) return null;
            Euler(s.q, out double roll, out double pitch, out double yaw);
            return new MotorFailRestore
            {
                p = new[] { s.p[0], s.p[1], s.p[2] },
                v = new[] { s.v[0], s.v[1], s.v[2] },
                roll = roll, pitch = pitch, yaw = yaw,
                yawRate = s.w[2],
                target = new SimTarget { n = s.p[0], e = s.p[1], alt = alt },
            };
        }
    }
}
