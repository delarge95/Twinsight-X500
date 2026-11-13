// E-6: game controller on Windows through XInput (Xbox-compatible pads), read with a plain P/Invoke of xinput1_4.dll (part of Windows 8+).
// Why not Input.GetAxis: the old Input Manager only reads the right stick through axes DEFINED in ProjectSettings/InputManager.asset, a file
// shared with the WebGL build; XInput needs no project setting, no package and no download, and the file is only compiled for the Windows
// player (UNITY_STANDALONE_WIN), so WebGL is untouched. Mapping of the sticks and buttons: PilotControls (mode 2, A/B/X/Y = takeoff/land/hold/RTL).
// Honest limit: only XInput devices (Xbox, and pads in XInput mode); DualShock / DualSense need Steam Input or a wrapper. A missing DLL or
// device simply reads as "no pad".

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
        const ushort ButtonA = 0x1000, ButtonB = 0x2000, ButtonX = 0x4000, ButtonY = 0x8000;
        static bool broken;

        /// <summary>One poll of a connected pad: sticks in -1..1 (Y positive UP) and the four face buttons.</summary>
        public struct Reading
        {
            public int Index;
            public double LeftX, LeftY, RightX, RightY;
            public bool A, B, X, Y;

            public bool Button(int i) => i == 0 ? A : i == 1 ? B : i == 2 ? X : i == 3 && Y;
        }

        static double Norm(short v) => Math.Max(-1.0, v / 32767.0);

        /// <summary>The first connected pad, or false when there is none (or XInput is not available).</summary>
        public static bool TryRead(out Reading r)
        {
            r = default;
            if (broken) return false;
            try
            {
                for (uint i = 0; i < 4; i++)
                {
                    if (XInputGetState(i, out XState s) != Success) continue;
                    var g = s.Gamepad;
                    r = new Reading
                    {
                        Index = (int)i,
                        LeftX = Norm(g.sThumbLX), LeftY = Norm(g.sThumbLY), RightX = Norm(g.sThumbRX), RightY = Norm(g.sThumbRY),
                        A = (g.wButtons & ButtonA) != 0, B = (g.wButtons & ButtonB) != 0, X = (g.wButtons & ButtonX) != 0, Y = (g.wButtons & ButtonY) != 0,
                    };
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
    }
}
#endif
