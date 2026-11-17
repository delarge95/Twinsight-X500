#if UNITY_STANDALONE
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// MVP P1 (PLAN_PC 32): the registry of ACTIVE EFFECTS of the top bar. Every toggleable tool registers once (icon, tooltip, "is it on?", "turn it off"); while it is on its
    /// chip shows in the bar with the tooltip «Quitar …» and one click turns it off, with no menu opened; the chip comes in and goes out animated
    /// (<see cref="DesktopMotion"/>, which respects «Reducir animaciones»). The state is READ every frame from the owner of each tool (the manager, the telemetry UI), never kept here,
    /// so a chip can not drift: a shortcut, Reset, a menu or the chip itself all end up in the same place. The session chip (status) and the red recording chip keep their own
    /// elements (they carry text).
    /// </summary>
    internal sealed class DesktopActiveEffects
    {
        internal sealed class Effect
        {
            public string Id;
            /// <summary>Element name (the probes and the tooltip table find the chip by it).</summary>
            public string Name;
            public Func<bool> IsActive;
            public string TipEs, TipEn;
            public bool Tool;       // a tool of the telemetry mode (smaller chip when the bar is narrow)
            public Button Chip;
            public bool WasActive;
        }

        private readonly List<Effect> _effects = new List<Effect>();
        private readonly Func<string, string, string> _t;

        public DesktopActiveEffects(Func<string, string, string> translate) { _t = translate; }

        public IReadOnlyList<Effect> Effects => _effects;

        /// <summary>Registers an effect: its chip (already built, hidden; click = turn it off), the question "is it on?" and its «Quitar …» tooltip in both languages.</summary>
        public Effect Add(string id, Button chip, Func<bool> isActive, string tipEs, string tipEn, bool tool)
        {
            var e = new Effect { Id = id, Name = chip.name, IsActive = isActive, TipEs = tipEs, TipEn = tipEn, Tool = tool, Chip = chip };
            if (tool) chip.AddToClassList("dk-chip--tool");
            chip.userData = e;
            _effects.Add(e);
            return e;
        }

        public Effect Find(string id)
        {
            foreach (Effect e in _effects) if (e.Id == id) return e;
            return null;
        }

        public Button ChipOf(string id) => Find(id)?.Chip;

        /// <summary>Number of registered effects that are on (not the chips still animating out).</summary>
        public int ActiveCount
        {
            get
            {
                int n = 0;
                foreach (Effect e in _effects) if (e.WasActive) n++;
                return n;
            }
        }

        /// <summary>One bit per effect that is on: the top bar measures its overflow again when it changes.</summary>
        public int Mask
        {
            get
            {
                int m = 0;
                for (int i = 0; i < _effects.Count && i < 30; i++) if (_effects[i].WasActive) m |= 1 << i;
                return m;
            }
        }

        public void Relabel()
        {
            foreach (Effect e in _effects) e.Chip.tooltip = _t(e.TipEn, e.TipEs);
        }

        /// <summary>Every frame: reads each owner's state and animates the chip in or out on a change.</summary>
        public void Tick()
        {
            for (int i = 0; i < _effects.Count; i++)
            {
                Effect e = _effects[i];
                bool on;
                try { on = e.IsActive(); } catch (Exception) { on = false; }
                e.WasActive = on;
                SetChip(e.Chip, on);
            }
        }

        internal static void SetChip(Button chip, bool active)
        {
            if (chip == null) return;
            bool live = chip.ClassListContains("dk-chip--live");
            if (active && !live)
            {
                chip.AddToClassList("dk-chip--live");
                chip.RemoveFromClassList("dk-chip--out");
                chip.style.display = DisplayStyle.Flex;
                DesktopMotion.Enter(chip, 0, "dk-enter--chip");
            }
            else if (!active && live)
            {
                chip.RemoveFromClassList("dk-chip--live");
                chip.AddToClassList("dk-chip--out");
                chip.schedule.Execute(() => { if (!chip.ClassListContains("dk-chip--live")) chip.style.display = DisplayStyle.None; }).StartingIn(DesktopMotion.Reduce ? 1 : 200);
            }
        }
    }
}
#endif
