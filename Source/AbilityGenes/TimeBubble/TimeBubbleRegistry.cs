using System.Collections.Generic;
using Verse;

namespace AbilityGenes
{
    /// <summary>
    /// Flat list of every active bubble across all maps, kept purely so the
    /// <see cref="Verse.Thing.DoTick"/> prefix can answer "is this frozen?" without
    /// walking map components. DoTick runs for every ticking thing every tick, so the
    /// zero-bubble case must cost nothing more than one static field read.
    /// </summary>
    public static class TimeBubbleRegistry
    {
        private static readonly List<TimeBubble> active = new List<TimeBubble>();

        public static int ActiveCount => active.Count;

        public static void Register(TimeBubble bubble)
        {
            if (!active.Contains(bubble)) active.Add(bubble);
        }

        public static void Deregister(TimeBubble bubble)
        {
            active.Remove(bubble);
        }

        public static void DeregisterMap(Map map)
        {
            active.RemoveAll(b => b.Map == map);
        }

        /// <summary>Hot path. Only ever called when at least one bubble exists.</summary>
        public static bool IsFrozen(Thing thing)
        {
            if (!thing.Spawned) return false;
            Map map = thing.Map;
            if (map == null) return false;
            IntVec3 pos = thing.Position;
            for (int i = 0; i < active.Count; i++)
            {
                TimeBubble b = active[i];
                if (b.Map == map && b.Contains(pos)) return true;
            }
            return false;
        }

        /// <summary>As IsFrozen, but only for bubbles that also suspend damage.</summary>
        public static bool IsProtected(Thing thing)
        {
            if (!thing.Spawned) return false;
            Map map = thing.Map;
            if (map == null) return false;
            IntVec3 pos = thing.Position;
            for (int i = 0; i < active.Count; i++)
            {
                TimeBubble b = active[i];
                if (b.invulnerable && b.Map == map && b.Contains(pos)) return true;
            }
            return false;
        }
    }
}
