// MVP P0 (camera while piloting). Pure maths of the «Seguir» chase camera: no scene access, unit-tested in EditMode (ChaseCameraTests).
//
// Model: the camera orbits the drone (the focus point is always the drone, so the drone can not leave the frame). Its yaw is
//   yaw = SmoothedBodyYaw + UserYaw        pitch = BasePitch + UserPitch
// where SmoothedBodyYaw follows the heading of the drone with a first-order lag (smooth, never abrupt, shortest way round) and the user offsets come
// from the pad / mouse. When the user stops giving input for RecenterDelay (1.5 s) the offsets glide back to 0: the camera returns behind the drone.
// Angles are degrees; yaw is the Unity convention used by OrbitCameraController (0 = looking along +Z, positive turns towards +X).

using System;

namespace WebGL.Telemetry
{
    /// <summary>«Seguir» = chase camera behind the drone (default when piloting starts); «Libre» = the free orbit of the viewer.</summary>
    public enum CameraMode { Follow, Free }

    public sealed class ChaseCamera
    {
        /// <summary>Pitch of the camera above the horizon of the drone when chasing (degrees, looking down).</summary>
        public const float BasePitch = 18f;
        /// <summary>Seconds without user input before the camera glides back behind the drone.</summary>
        public const float RecenterDelay = 1.5f;
        /// <summary>Time constant of that glide.</summary>
        public const float RecenterTau = 0.45f;
        /// <summary>Time constant of the lag with which the camera follows the heading of the drone.</summary>
        public const float YawTau = 0.35f;
        /// <summary>User offset limits: pitch is bounded so the view never flips over the drone; the camera pitch itself is clamped to <see cref="MinPitch"/>..<see cref="MaxPitch"/>.</summary>
        public const float MinUserPitch = -28f, MaxUserPitch = 60f;
        public const float MinPitch = -8f, MaxPitch = 80f;
        /// <summary>Below this horizontal part of the nose vector (a drone pitched or rolled to the vertical) the heading is undefined and the last one is kept.</summary>
        public const float MinHorizontal = 0.25f;

        /// <summary>Pad: right-stick rates at full deflection (deg/s), and the D-pad rate. The stick goes through PilotControls.ShapeAxis (deadzone and expo).</summary>
        public const float PadYawRate = 150f, PadPitchRate = 85f, DpadRate = 95f;

        public float SmoothedYaw { get; private set; }
        public float UserYaw { get; private set; }
        public float UserPitch { get; private set; }
        public float IdleFor { get; private set; }
        public bool Primed { get; private set; }

        /// <summary>The camera yaw to aim at (not wrapped: callers take the shortest way from their own angle).</summary>
        public float Yaw => SmoothedYaw + UserYaw;
        public float Pitch => Clamp(BasePitch + UserPitch, MinPitch, MaxPitch);
        /// <summary>True while a user offset is being held (an orbit is in progress or waiting to recentre).</summary>
        public bool HasOffset => Math.Abs(UserYaw) > 0.05f || Math.Abs(UserPitch) > 0.05f;

        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        /// <summary>Signed shortest angular distance from <paramref name="from"/> to <paramref name="to"/>, in -180..180.</summary>
        public static float DeltaAngle(float from, float to)
        {
            float d = (to - from) % 360f;
            if (d > 180f) d -= 360f;
            else if (d < -180f) d += 360f;
            return d;
        }

        public static float Wrap180(float a) => DeltaAngle(0f, a);

        /// <summary>Heading (yaw) of a horizontal direction (x east, z north in Unity). False when the horizontal part is too small to define it.</summary>
        public static bool TryHeading(float fx, float fz, out float yawDeg)
        {
            yawDeg = 0f;
            if (float.IsNaN(fx) || float.IsNaN(fz) || (float)Math.Sqrt(fx * fx + fz * fz) < MinHorizontal) return false;
            yawDeg = (float)(Math.Atan2(fx, fz) * 180.0 / Math.PI);
            return true;
        }

        /// <summary>Starts (or restarts) the chase on a heading: no user offset, no lag.</summary>
        public void Prime(float bodyYaw)
        {
            SmoothedYaw = Wrap180(bodyYaw);
            UserYaw = 0f;
            UserPitch = 0f;
            IdleFor = 0f;
            Primed = true;
        }

        public void Unprime() { Primed = false; UserYaw = 0f; UserPitch = 0f; IdleFor = 0f; }

        /// <summary>Drops the user offset at once (Reset view): the camera glides back behind the drone through the normal smoothing of the viewer.</summary>
        public void ClearUser() { UserYaw = 0f; UserPitch = 0f; IdleFor = 0f; }

        /// <summary>User orbit input (degrees): restarts the 1.5 s timer. Zero input does not count as input.</summary>
        public void AddInput(float dYaw, float dPitch)
        {
            if (dYaw == 0f && dPitch == 0f) return;
            if (float.IsNaN(dYaw) || float.IsNaN(dPitch)) return;
            UserYaw = Wrap180(UserYaw + dYaw);
            UserPitch = Clamp(UserPitch + dPitch, MinUserPitch, MaxUserPitch);
            IdleFor = 0f;
        }

        /// <summary>
        /// Advances the chase by <paramref name="dt"/> seconds. <paramref name="bodyYaw"/> is the current heading of the drone;
        /// <paramref name="hasBodyYaw"/> false keeps the previous one (a drone tumbling has no heading). <paramref name="reduceMotion"/> (the app's «Reducir
        /// animaciones») removes the lag: the camera takes the heading at once and recentres without gliding.
        /// </summary>
        public void Step(float dt, float bodyYaw, bool hasBodyYaw, bool reduceMotion)
        {
            if (dt < 0f || float.IsNaN(dt)) dt = 0f;
            if (!Primed) Prime(hasBodyYaw ? bodyYaw : 0f);

            if (hasBodyYaw)
            {
                float k = reduceMotion ? 1f : 1f - (float)Math.Exp(-dt / YawTau);
                SmoothedYaw = Wrap180(SmoothedYaw + DeltaAngle(SmoothedYaw, bodyYaw) * k);
            }

            IdleFor += dt;
            if (IdleFor >= RecenterDelay && (UserYaw != 0f || UserPitch != 0f))
            {
                float k = reduceMotion ? 1f : 1f - (float)Math.Exp(-dt / RecenterTau);
                UserYaw -= UserYaw * k;
                UserPitch -= UserPitch * k;
                if (Math.Abs(UserYaw) < 0.05f) UserYaw = 0f;
                if (Math.Abs(UserPitch) < 0.05f) UserPitch = 0f;
            }
        }

        // ───────────────────────── pad mapping (the final one; documented in the help, the legend and PLAN_PC §32) ─────────────────────────

        /// <summary>
        /// Gamepad camera input of one frame, DRON mode. The camera belongs to the pad only while LB is held (the right stick flies the drone in mode 2 otherwise):
        /// LB + right stick orbits (stick right = look right, stick up = look up) and the D-pad orbits in fixed steps (always, it flies nothing).
        /// </summary>
        public static void PadOrbit(bool lbHeld, double rightX, double rightY, bool dLeft, bool dRight, bool dUp, bool dDown, float dt, out float dYaw, out float dPitch)
        {
            dYaw = 0f;
            dPitch = 0f;
            if (dt <= 0f) return;
            if (lbHeld)
            {
                dYaw += (float)(PilotControls.ShapeAxis(rightX) * PadYawRate) * dt;
                dPitch -= (float)(PilotControls.ShapeAxis(rightY) * PadPitchRate) * dt; // stick up (+Y) = look up = the camera pitch decreases
            }
            if (dRight) dYaw += DpadRate * dt;
            if (dLeft) dYaw -= DpadRate * dt;
            if (dUp) dPitch -= DpadRate * 0.6f * dt;
            if (dDown) dPitch += DpadRate * 0.6f * dt;
        }
    }
}
