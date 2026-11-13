// E-6: piloting, the pure parts (web: src/web/telemetry/keyboard.ts and gamepad.ts). Held keys and pad sticks become the MANUAL_CONTROL
// stream (x pitch, y roll, z throttle with 500 = hold altitude, r yaw; the ranges of the `manual` client message); the keys T L H R and the
// pad face buttons become the four flight commands. No UnityEngine: unit-tested in EditMode. The Unity side (key reading, the dead-man
// triggers, the toggle, the legend) is in UI/Desktop/DesktopTelemetryUI.Pilot.cs and Telemetry/Sim/XInputPad.cs.
//
// Key map (identical to the web) while piloting is ON:
//   W S / Up Down  forward / back (pitch)      A D / Left Right  left / right (roll)      Q E  yaw left / right
//   Space / Shift  climb / descend             T takeoff   L land   H hold   R return (RTL)
// Mode 2 pad (identical to the web): left stick Y throttle, X yaw; right stick Y pitch, X roll; A takeoff, B land, X hold, Y RTL.

using System;

namespace WebGL.Telemetry
{
    [Flags]
    public enum PilotKeys
    {
        None = 0, Forward = 1, Back = 2, Left = 4, Right = 8, YawLeft = 16, YawRight = 32, Up = 64, Down = 128,
    }

    public enum PilotCommand { Takeoff, Land, Hold, Rtl }

    public static class PilotControls
    {
        /// <summary>Stick deflection of a held key, as a fraction of full scale: a key is all-or-nothing, so full scale would be abrupt.</summary>
        public const double KeyDeflection = 0.7;
        /// <summary>MANUAL_CONTROL ranges: x, y, r are -1000..1000, z is 0..1000 with 500 = hold altitude.</summary>
        public const double AxisMax = 1000;
        public const double ThrottleHold = 500;
        public const double StreamHz = 40;
        public const double TakeoffAltitudeM = 5;
        /// <summary>Altitude of a click-to-fly: the current one, kept between these limits (web: App.tsx flyTo).</summary>
        public const double FlyToMinAltitude = 1, FlyToMaxAltitude = 50;

        public const double Deadzone = 0.08;
        public const double Expo = 0.35;

        static double Clamp(double v, double lo, double hi) => Math.Min(hi, Math.Max(lo, v));

        static int Axis(PilotKeys held, PilotKeys pos, PilotKeys neg) => ((held & pos) != 0 ? 1 : 0) - ((held & neg) != 0 ? 1 : 0);

        /// <summary>Held keys to a stick sample. Opposite keys cancel; no key = sticks centred (hold altitude).</summary>
        public static SimSticks KeysToSticks(PilotKeys held, double deflection = KeyDeflection)
        {
            double d = Clamp(deflection, 0, 1);
            double x = JsMath.Round(Axis(held, PilotKeys.Forward, PilotKeys.Back) * d * AxisMax);
            double y = JsMath.Round(Axis(held, PilotKeys.Right, PilotKeys.Left) * d * AxisMax);
            double r = JsMath.Round(Axis(held, PilotKeys.YawRight, PilotKeys.YawLeft) * d * AxisMax);
            double z = JsMath.Round(ThrottleHold + Axis(held, PilotKeys.Up, PilotKeys.Down) * d * ThrottleHold);
            return new SimSticks(x + 0, y + 0, z, r + 0);
        }

        /// <summary>The wire message of a stick sample.</summary>
        public static ManualMessage ToMessage(SimSticks s) => ClientMessage.Manual(s.x, s.y, s.z, s.r);

        /// <summary>The wire message of a command key / button (takeoff goes to TakeoffAltitudeM, like the web).</summary>
        public static ClientMessage CommandMessage(PilotCommand c)
        {
            switch (c)
            {
                case PilotCommand.Takeoff: return ClientMessage.Takeoff(TakeoffAltitudeM);
                case PilotCommand.Land: return ClientMessage.Land();
                case PilotCommand.Hold: return ClientMessage.Hold();
                default: return ClientMessage.Rtl();
            }
        }

        public static string NameOf(PilotCommand c)
        {
            switch (c)
            {
                case PilotCommand.Takeoff: return "takeoff";
                case PilotCommand.Land: return "land";
                case PilotCommand.Hold: return "hold";
                default: return "rtl";
            }
        }

        // ───────────────────────── gamepad ─────────────────────────

        /// <summary>Deadzone with rescaling (no jump at the edge) followed by a mild expo curve. Input and output in -1..1.</summary>
        public static double ShapeAxis(double v, double deadzone = Deadzone, double expo = Expo)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return 0;
            double a = Math.Abs(Clamp(v, -1, 1));
            if (a <= deadzone) return 0;
            double n = (a - deadzone) / (1 - deadzone);
            return Math.Sign(v) * ((1 - expo) * n + expo * n * n * n);
        }

        /// <summary>
        /// Mode 2 sticks (each in -1..1, Y positive when pushed UP, the XInput convention) to a stick sample:
        /// left Y throttle, left X yaw, right Y pitch (up = forward), right X roll.
        /// </summary>
        public static SimSticks PadToSticks(double leftX, double leftY, double rightX, double rightY)
        {
            double x = JsMath.Round(ShapeAxis(rightY) * AxisMax);
            double y = JsMath.Round(ShapeAxis(rightX) * AxisMax);
            double z = JsMath.Round(ThrottleHold + ShapeAxis(leftY) * ThrottleHold);
            double r = JsMath.Round(ShapeAxis(leftX) * AxisMax);
            return new SimSticks(x + 0, y + 0, Clamp(z, 0, AxisMax), r + 0);
        }

        /// <summary>True when any stick is outside the deadzone (the pad holds the sticks; otherwise the keyboard does).</summary>
        public static bool PadActive(double leftX, double leftY, double rightX, double rightY) =>
            ShapeAxis(leftX) != 0 || ShapeAxis(leftY) != 0 || ShapeAxis(rightX) != 0 || ShapeAxis(rightY) != 0;

        /// <summary>Face buttons: 0 = A takeoff, 1 = B land, 2 = X hold, 3 = Y return (the web's BUTTON indices).</summary>
        public static PilotCommand? ButtonToCommand(int index)
        {
            switch (index)
            {
                case 0: return PilotCommand.Takeoff;
                case 1: return PilotCommand.Land;
                case 2: return PilotCommand.Hold;
                case 3: return PilotCommand.Rtl;
                default: return null;
            }
        }

        // ───────────────────────── click to fly ─────────────────────────

        /// <summary>Altitude of a click-to-fly: the current one between 1 and 50 m (5 m when there is no frame).</summary>
        public static double FlyToAltitude(TelemetryFrame f) => f == null ? TakeoffAltitudeM : Clamp(-f.p[2], FlyToMinAltitude, FlyToMaxAltitude);

        /// <summary>True when the point lies beyond the simulator geofence (its radius): the model clamps the target to the circle.</summary>
        public static bool BeyondGeofence(double north, double east) => Math.Sqrt(north * north + east * east) > SimLimits.Radius;

        /// <summary>The target as the model will fly it (clamped to the geofence radius, same maths as FlightSim fly_to).</summary>
        public static void ClampToGeofence(ref double north, ref double east)
        {
            double d = Math.Sqrt(north * north + east * east);
            if (d > SimLimits.Radius) { double k = SimLimits.Radius / d; north *= k; east *= k; }
        }

        /// <summary>
        /// Which geofence limit a frame is touching (horizontal radius / ceiling), with a 0.5 m margin, for the "geovalla" toast: 0 none,
        /// 1 radius, 2 ceiling, 3 both.
        /// </summary>
        public static int GeofenceTouch(TelemetryFrame f)
        {
            if (f == null || !f.armed || f.landed) return 0;
            int t = 0;
            if (Math.Sqrt(f.p[0] * f.p[0] + f.p[1] * f.p[1]) >= SimLimits.Radius - 0.5) t |= 1;
            if (-f.p[2] >= SimLimits.Ceiling - 0.5) t |= 2;
            return t;
        }
    }
}
