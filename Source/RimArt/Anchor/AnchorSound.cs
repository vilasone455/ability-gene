using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimArt
{
    [DefOf]
    public static class AnchorSoundDefOf
    {
        public static SoundDef AG_AnchorClap;
        public static SoundDef AG_AnchorPuff;

        static AnchorSoundDefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(AnchorSoundDefOf)); }
    }

    internal static class AnchorSound
    {
        /// <summary>One palm contact, heard from where the carrier stands.</summary>
        public static void Clap(Pawn carrier)
        {
            if (carrier == null || !carrier.Spawned) return;
            AnchorSoundDefOf.AG_AnchorClap.PlayOneShot(new TargetInfo(carrier.Position, carrier.Map, false));
        }

        /// <summary>The puff at one end of a swap.</summary>
        public static void Puff(Map map, Vector2 ground)
        {
            IntVec3 cell = new IntVec3(Mathf.FloorToInt(ground.x), 0, Mathf.FloorToInt(ground.y));
            if (map == null || !cell.InBounds(map)) return;
            AnchorSoundDefOf.AG_AnchorPuff.PlayOneShot(new TargetInfo(cell, map, false));
        }
    }
}
