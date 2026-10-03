using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Game time in seconds since a tick, for drawing. The game ticks 60 times a second at speed 1, and a
    /// picture drawn on whole ticks steps at that rate, so this adds the share of the next tick that has
    /// gone by in real time at the current speed. The share is 0 while paused, so a paused picture holds
    /// still, and at most 0.99, so it never reaches the next tick's value. The rules never read this; they
    /// count whole ticks.
    /// </summary>
    internal static class PictureClock
    {
        // One clock for every caller: the first call that sees a new tick notes the real time, and every
        // picture drawn in that tick measures its share from it.
        private static int lastTick = -1;
        private static float lastReal;

        /// <summary>(TicksGame - tick + share) / 60.</summary>
        public static float Since(int tick)
        {
            TickManager ticks = Find.TickManager;
            int now = ticks.TicksGame;
            if (now != lastTick)
            {
                lastTick = now;
                lastReal = Time.realtimeSinceStartup;
            }
            float share = ticks.Paused ? 0f : Mathf.Clamp((Time.realtimeSinceStartup - lastReal) * 60f * ticks.TickRateMultiplier, 0f, 0.99f);
            return (now - tick + share) / 60f;
        }
    }
}
