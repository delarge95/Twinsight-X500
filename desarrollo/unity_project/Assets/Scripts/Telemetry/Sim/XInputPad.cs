// E-6: game controller on Windows through XInput (Xbox-compatible pads), read with a plain P/Invoke of xinput1_4.dll (part of Windows 8+).
// Why not Input.GetAxis: the old Input Manager only reads the right stick through axes DEFINED in ProjectSettings/InputManager.asset, a file
// shared with the WebGL build; XInput needs no project setting, no package and no download, and the file is only compiled for the Windows
// player (UNITY_STANDALONE_WIN), so WebGL is untouched. Mapping of the sticks and buttons: PilotControls (mode 2, A/B/X/Y = takeoff/land/hold/RTL)
// and PadControls.cs (DRON / MENÚS modes, Start toggles; d-pad, shoulders and Back are read here too, PLAN_PC 27.2).
// Honest limit: only XInput devices (Xbox, and pads in XInput mode); DualShock / DualSense need Steam Input or a wrapper. A missing DLL or
// device simply reads as "no pad".
// Cost: XInputGetState on an EMPTY slot is slow on some systems (it probes the bus), so with no pad connected the four slots are scanned once a
// second; once a pad answers, only its slot is polled every frame.

#if UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;

namespace WebGL.Telemetry
{
    public static class XInputPad
    {
        [StructLayout(LayoutKind.Sequential)]
        struct XGamepad
        {
            public ushort wButtons;
            public byte bLeftTrigger, bRightTrigger;
            public short sThumbLX, sThumbLY, sThumbRX, sThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct XState
        {
            public uint dwPacketNumber;
            public XGamepad Gamepad;
        }

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        static extern uint XInputGetState(uint userIndex, out XState state);

        const uint Success = 0;
        const ushort DpadUp = 0x0001, DpadDown = 0x0002, DpadLeft = 0x0004, DpadRight = 0x0008, StartBtn = 0x0010, BackBtn = 0x0020;
        const ushort LbBtn = 0x0100, RbBtn = 0x0200;
        const ushort ButtonA = 0x1000, ButtonB = 0x2000, ButtonX = 0x4000, ButtonY = 0x8000;
        static bool broken;
        static int slot = -1;           // the slot that answered last
        static long nextScanTicks;       // no pad: next time the four slots are probed

        /// <summary>One poll of a connected pad: sticks in -1..1 (Y positive UP) and the four face buttons.</summary>
        public struct Reading
        {
            public int Index;
            public double LeftX, LeftY, RightX, RightY;
            public bool A, B, X, Y;
            public PadButtons Buttons;

            public bool Button(int i) => i == 0 ? A : i == 1 ? B : i == 2 ? X : i == 3 && Y;
        }

        static long NowMs() => System.Diagnostics.Stopwatch.GetTimestamp() * 1000L / System.Diagnostics.Stopwatch.Frequency;

        static double Norm(short v) => Math.Max(-1.0, v / 32767.0);

        static Reading Convert(uint index, XGamepad g)
        {
            PadButtons b = PadButtons.None;
            if ((g.wButtons & ButtonA) != 0) b |= PadButtons.A;
            if ((g.wButtons & ButtonB) != 0) b |= PadButtons.B;
            if ((g.wButtons & ButtonX) != 0) b |= PadButtons.X;
            if ((g.wButtons & ButtonY) != 0) b |= PadButtons.Y;
            if ((g.wButtons & LbBtn) != 0) b |= PadButtons.LB;
            if ((g.wButtons & RbBtn) != 0) b |= PadButtons.RB;
            if ((g.wButtons & StartBtn) != 0) b |= PadButtons.Start;
            if ((g.wButtons & BackBtn) != 0) b |= PadButtons.Back;
            if ((g.wButtons & DpadUp) != 0) b |= PadButtons.DpadUp;
            if ((g.wButtons & DpadDown) != 0) b |= PadButtons.DpadDown;
            if ((g.wButtons & DpadLeft) != 0) b |= PadButtons.DpadLeft;
            if ((g.wButtons & DpadRight) != 0) b |= PadButtons.DpadRight;
            return new Reading
            {
                Index = (int)index,
                LeftX = Norm(g.sThumbLX), LeftY = Norm(g.sThumbLY), RightX = Norm(g.sThumbRX), RightY = Norm(g.sThumbRY),
                A = (g.wButtons & ButtonA) != 0, B = (g.wButtons & ButtonB) != 0, X = (g.wButtons & ButtonX) != 0, Y = (g.wButtons & ButtonY) != 0,
                Buttons = b,
            };
        }

        /// <summary>The connected pad, or false when there is none (or XInput is not available).</summary>
        public static bool TryRead(out Reading r)
        {
            r = default;
            if (broken) return false;
            try
            {
                if (slot >= 0)
                {
                    if (XInputGetState((uint)slot, out XState cur) == Success) { r = Convert((uint)slot, cur.Gamepad); return true; }
                    slot = -1; // unplugged: rescan (throttled)
                    nextScanTicks = NowMs() + 1000;
                    return false;
                }

                if (NowMs() < nextScanTicks) return false;
                nextScanTicks = NowMs() + 1000;
                for (uint i = 0; i < 4; i++)
                {
                    if (XInputGetState(i, out XState s) != Success) continue;
                    slot = (int)i;
                    r = Convert(i, s.Gamepad);
                    return true;
                }
                return false;
            }
            catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException)
            {
                broken = true; // no XInput on this machine: stop polling
                return false;
            }
        }

        /// <summary>The same poll as a <see cref="PadState"/> (PadControls).</summary>
        public static bool TryReadState(out PadState state)
        {
            state = default;
            if (!TryRead(out Reading r)) return false;
            state = new PadState { LeftX = r.LeftX, LeftY = r.LeftY, RightX = r.RightX, RightY = r.RightY, Buttons = r.Buttons };
            return true;
        }
    }
}
#endif
