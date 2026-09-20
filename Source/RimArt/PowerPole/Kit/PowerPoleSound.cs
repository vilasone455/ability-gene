using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimArt
{
    [DefOf]
    public static class PowerPoleSoundDefOf
    {
        public static SoundDef AG_PowerPoleExtend;
        public static SoundDef AG_PowerPoleSwing;
        public static SoundDef AG_PowerPoleRetract;
        public static SoundDef AG_PowerPoleHit;
        public static SoundDef AG_PowerPoleWall;
        public static SoundDef AG_PowerPoleSlam;
        public static SoundDef AG_PowerPoleVault;

        static PowerPoleSoundDefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(PowerPoleSoundDefOf)); }
    }

    internal static class PowerPoleSound
    {
        /// <summary>Heard from a place on the map, given as the picture gives it: x east, y north.</summary>
        public static void Play(SoundDef sound, Map map, Vector2 ground)
        {
            var cell = new IntVec3(Mathf.FloorToInt(ground.x), 0, Mathf.FloorToInt(ground.y));
            if (sound == null || map == null || !cell.InBounds(map)) return;
            sound.PlayOneShot(new TargetInfo(cell, map, false));
        }
    }
}
