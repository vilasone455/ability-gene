using System;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The kit's light rule on a real map: the level at a cell is the higher of sky light (times
    /// <see cref="ShadowPlexusExtension.skyShare"/>, nothing under a roof) and lamp or fire light.
    /// The game's glow grid gives lamp light as 0.5 at most unless the cell is saturated, where it
    /// gives 1, so daylight is half reach and a cell next to a lamp or a campfire is full reach.
    /// Under <see cref="ShadowPlexusExtension.darkBelow"/> a cell is dark. The sketches' pure model
    /// of the same rule is <see cref="ShadowPlexusTiming"/>.
    /// </summary>
    public static class ShadowLight
    {
        /// <summary>Game tests set this so a scenario does not depend on the hour: the level at a cell.</summary>
        internal static Func<IntVec3, float> levelForTests;

        public static float Level(Map map, IntVec3 c)
        {
            if (levelForTests != null) return levelForTests(c);
            if (map == null || !c.InBounds(map)) return 0f;
            float sky = map.roofGrid.Roofed(c) ? 0f : map.skyManager.CurSkyGlow * ShadowPlexusExtension.Get.skyShare;
            float lamps = map.glowGrid.GroundGlowAt(c, ignoreCavePlants: false, ignoreSky: true);
            return Mathf.Max(sky, lamps);
        }

        public static bool Dark(Map map, IntVec3 c) => Level(map, c) < ShadowPlexusExtension.Get.darkBelow;

        public static bool Smoky(Map map, IntVec3 c) =>
            map != null && c.InBounds(map) && map.gasGrid.DensityPercentAt(c, GasType.BlindSmoke) >= ShadowPlexusExtension.Get.smokeDensity;

        /// <summary>An ability's reach from a cell: its full reach times the light level, and nothing in the dark.</summary>
        public static float Reach(float fullReach, Map map, IntVec3 c)
        {
            float level = Level(map, c);
            return level < ShadowPlexusExtension.Get.darkBelow ? 0f : fullReach * level;
        }
    }
}
