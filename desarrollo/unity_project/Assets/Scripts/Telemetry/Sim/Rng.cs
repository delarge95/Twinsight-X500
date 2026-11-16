// Lote 1a, tool 4: deterministic random numbers of the simulator, a port of src/shared/rng.ts. mulberry32 is 32-bit integer arithmetic (`uint` with unchecked
// wrap-around here; Math.imul and `| 0` in JS), so the same seed gives the same sequence on the web and on the desktop. No clock, no System.Random.
// Reproduced by contracts/golden/wind.json (the ten uniforms of seed 42 exactly, the ten normals to 1e-12).
// Pure C#, no UnityEngine.

using System;

namespace WebGL.Telemetry
{
    /// <summary>State of a mulberry32 generator: one 32-bit word (the JS port keeps it as a signed integer; the bits are the same).</summary>
    public sealed class Rng
    {
        public uint state;

        /// <summary>Starts a generator from a seed (any integer; only its 32 low bits count).</summary>
        public Rng(double seed) { state = unchecked((uint)(long)seed); }

        /// <summary>The next number of mulberry32, uniform in [0, 1) (a 32-bit integer divided by 2^32). Advances the state.</summary>
        public double Next()
        {
            unchecked
            {
                state += 0x6d2b79f5u;
                uint t = (state ^ (state >> 15)) * (1u | state);
                t = (t + (t ^ (t >> 7)) * (61u | t)) ^ t;
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        /// <summary>
        /// A standard normal number by Box-Muller. It spends exactly two uniforms per normal (u1 first, u2 second) and keeps the cosine branch only, so the
        /// sequence does not depend on any cached second value. u1 = 1 - r is in (0, 1]: log never sees zero.
        /// </summary>
        public double Normal()
        {
            double u1 = 1 - Next();
            double u2 = Next();
            return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        }
    }
}
