// PLAN_PC 27.2: the game pad has TWO modes and only one is active at a time.
//   DRON  (default): the pad flies the drone (PilotControls: sticks, A takeoff, B land, X hold, Y return) and the UI receives NOTHING from it.
//   MENÚS (Start toggles): the pad navigates the menus (d-pad / left stick = focus, A = activate, B = back, LB / RB = previous / next mode or tab,
//          right stick = scroll, Back = help) and the drone receives NO pad command.
// Pure C# (no UnityEngine): the state machine, the stick-to-direction repeat and the pad source seam are compiled on every platform and
// tested in EditMode. XInput itself (Windows only) is XInputPad.cs; the UI part is UI/Desktop/DesktopPad.cs.
// Honest limit: only XInput pads exist for the app. A pad that is not XInput is not read (and so cannot change the mode).

using System;
using System.Collections.Generic;

namespace WebGL.Telemetry
{
    [Flags]
    public enum PadButtons
    {
        None = 0,
        A = 1 << 0, B = 1 << 1, X = 1 << 2, Y = 1 << 3,
        LB = 1 << 4, RB = 1 << 5, Start = 1 << 6, Back = 1 << 7,
        DpadUp = 1 << 8, DpadDown = 1 << 9, DpadLeft = 1 << 10, DpadRight = 1 << 11,
    }

    /// <summary>One poll of a connected pad: sticks in -1..1 (Y positive UP, the XInput convention) and the buttons.</summary>
    public struct PadState
    {
        public double LeftX, LeftY, RightX, RightY;
        public PadButtons Buttons;

        public bool Has(PadButtons b) => (Buttons & b) == b;
        public static PadState Neutral => default;
    }

    public enum PadMode { Drone, Menus }

    public enum PadAction { Move, Submit, Cancel, PrevTab, NextTab, Help }

    public struct PadEvent
    {
        public PadAction Action;
        /// <summary>Move only: Dx +1 right / -1 left, Dy +1 up / -1 down (one of them is 0).</summary>
        public int Dx, Dy;
        public override string ToString() => Action == PadAction.Move ? "Move(" + Dx + "," + Dy + ")" : Action.ToString();
    }

    /// <summary>The pad-mode state machine. Feed it one <see cref="PadState"/> (or null = no pad) per frame.</summary>
    public sealed class PadMachine
    {
        public const double StickEngage = 0.60, StickRelease = 0.40;
        public const double RepeatFirst = 0.40, RepeatNext = 0.12;
        public const double ScrollDeadzone = 0.15;

        public PadMode Mode { get; private set; } = PadMode.Drone;
        public bool Connected { get; private set; }
        public PadState State { get; private set; }
        /// <summary>Buttons that went down this frame (edges). Only the Start edge is reported in the frame the mode changes.</summary>
        public PadButtons Edges { get; private set; }
        public bool ModeJustChanged { get; private set; }
        public bool JustConnected { get; private set; }
        public bool JustDisconnected { get; private set; }
        /// <summary>Right stick Y after the deadzone, -1..1 (positive = up). 0 in Drone mode: the right stick belongs to the drone there.</summary>
        public double ScrollY { get; private set; }

        PadButtons prev;
        int lastDx, lastDy;
        double nextRepeat;

        public bool Pressed(PadButtons b) => (Edges & b) != 0;

        /// <summary>Back to DRON with no pad (a new pad always starts in DRON: the safe default).</summary>
        public void Reset()
        {
            Mode = PadMode.Drone;
            Connected = false;
            State = default;
            Edges = PadButtons.None;
            prev = PadButtons.None;
            lastDx = lastDy = 0;
            ScrollY = 0;
        }

        /// <summary>Sets the mode explicitly (harness / tests); the same bookkeeping as the Start button.</summary>
        public void SetMode(PadMode m)
        {
            if (m == Mode) return;
            Mode = m;
            ModeJustChanged = true;
            lastDx = lastDy = 0;
            ScrollY = 0;
        }

        public void Update(PadState? state, double now, List<PadEvent> events)
        {
            ModeJustChanged = false;
            JustConnected = false;
            JustDisconnected = false;
            Edges = PadButtons.None;

            if (!state.HasValue)
            {
                if (Connected) { Reset(); JustDisconnected = true; }
                return;
            }

            PadState s = state.Value;
            if (!Connected)
            {
                Connected = true;
                Mode = PadMode.Drone;
                JustConnected = true;
                prev = s.Buttons; // buttons already held when the pad appears are not presses
            }

            State = s;
            PadButtons cur = s.Buttons;
            PadButtons edges = cur & ~prev;
            prev = cur;

            if ((edges & PadButtons.Start) != 0)
            {
                Mode = Mode == PadMode.Drone ? PadMode.Menus : PadMode.Drone;
                ModeJustChanged = true;
                lastDx = lastDy = 0;
                ScrollY = 0;
                Edges = PadButtons.Start; // nothing else acts in the frame the mode changes
                return;
            }

            Edges = edges;
            if (Mode != PadMode.Menus) { ScrollY = 0; lastDx = lastDy = 0; return; }

            if (events != null)
            {
                if ((edges & PadButtons.A) != 0) events.Add(new PadEvent { Action = PadAction.Submit });
                if ((edges & PadButtons.B) != 0) events.Add(new PadEvent { Action = PadAction.Cancel });
                if ((edges & PadButtons.LB) != 0) events.Add(new PadEvent { Action = PadAction.PrevTab });
                if ((edges & PadButtons.RB) != 0) events.Add(new PadEvent { Action = PadAction.NextTab });
                if ((edges & PadButtons.Back) != 0) events.Add(new PadEvent { Action = PadAction.Help });
            }

            Direction(s, out int dx, out int dy);
            if (dx != lastDx || dy != lastDy)
            {
                lastDx = dx;
                lastDy = dy;
                if ((dx != 0 || dy != 0) && events != null) { events.Add(new PadEvent { Action = PadAction.Move, Dx = dx, Dy = dy }); nextRepeat = now + RepeatFirst; }
            }
            else if ((dx != 0 || dy != 0) && now >= nextRepeat)
            {
                if (events != null) events.Add(new PadEvent { Action = PadAction.Move, Dx = dx, Dy = dy });
                nextRepeat = now + RepeatNext;
            }

            double r = Math.Abs(s.RightY);
            ScrollY = r <= ScrollDeadzone ? 0 : Math.Sign(s.RightY) * (r - ScrollDeadzone) / (1 - ScrollDeadzone);
        }

        // d-pad wins over the stick; the stick needs StickEngage to start and falls below StickRelease to stop (no flicker around the threshold)
        void Direction(PadState s, out int dx, out int dy)
        {
            dx = dy = 0;
            if (s.Has(PadButtons.DpadUp)) dy = 1;
            else if (s.Has(PadButtons.DpadDown)) dy = -1;
            else if (s.Has(PadButtons.DpadLeft)) dx = -1;
            else if (s.Has(PadButtons.DpadRight)) dx = 1;
            if (dx != 0 || dy != 0) return;

            double ax = Math.Abs(s.LeftX), ay = Math.Abs(s.LeftY);
            double threshold = lastDx != 0 || lastDy != 0 ? StickRelease : StickEngage;
            if (Math.Max(ax, ay) < threshold) return;
            if (ax > ay) dx = s.LeftX > 0 ? 1 : -1;
            else dy = s.LeftY > 0 ? 1 : -1;
        }
    }

    /// <summary>Where the pad state comes from: XInput on Windows, or an injected function (the in-player harness: nobody can plug a pad into a probe).</summary>
    public static class PadSource
    {
        /// <summary>Replaces the real pad (null = the real one). Returns null for "no pad connected".</summary>
        public static Func<PadState?> Provider;

        public static bool IsInjected => Provider != null;

        public static PadState? Poll()
        {
            if (Provider != null) return Provider();
#if UNITY_STANDALONE_WIN
            return XInputPad.TryReadState(out PadState s) ? s : (PadState?)null;
#else
            return null;
#endif
        }
    }
}
