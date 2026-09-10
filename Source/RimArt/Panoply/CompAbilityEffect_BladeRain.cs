using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public class CompProperties_AbilityBladeRain : CompProperties_AbilityEffect
    {
        public float radius = 6.9f;
        public int bladeCount = 14;
        public int impactDamage = 12;

        public CompProperties_AbilityBladeRain()
        {
            compClass = typeof(CompAbilityEffect_BladeRain);
        }
    }

    /// <summary>
    /// The rain, and the only ability of the three that makes anything.
    ///
    /// What it does is not damage - the damage on landing is small and half the blades will hit
    /// nothing at all. What it does is *put objects on the ground in a shape*, and every other
    /// thing this gene can do reads that shape afterwards. Rain onto a corridor and loose is a
    /// crossfire; rain onto the wrong side of a wall and loose is a wall being stabbed.
    ///
    /// The blades are staggered on the way down rather than landing together, which costs one
    /// integer and buys the whole read: a player watching shadows grow across a blob has time to
    /// see where it is going while their own pawns can still walk out of it.
    /// </summary>
    public class CompAbilityEffect_BladeRain : CompAbilityEffect
    {
        public new CompProperties_AbilityBladeRain Props => (CompProperties_AbilityBladeRain)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Map map = caster?.Map;
            if (map == null) return;

            List<IntVec3> candidates = new List<IntVec3>();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(target.Cell, Props.radius, true))
            {
                if (!cell.InBounds(map)) continue;
                if (cell.Fogged(map)) continue;
                if (!cell.Standable(map)) continue;
                candidates.Add(cell);
            }
            if (candidates.Count == 0) return;

            candidates.Shuffle();

            int blades = Mathf.Min(Props.bladeCount, candidates.Count);
            for (int i = 0; i < blades; i++)
            {
                Thing spawned = GenSpawn.Spawn(PanoplyDefOf.AG_PanoplyBladeFalling, candidates[i], map);
                FallingBlade falling = spawned as FallingBlade;
                if (falling == null) continue;

                falling.Configure(caster, i * PanoplyDefaults.RainStaggerTicks, Props.impactDamage);
            }

            Messages.Message("AG_PanoplyRained".Translate(caster.LabelShortCap, blades),
                caster, MessageTypeDefOf.NeutralEvent, false);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            GenDraw.DrawRadiusRing(target.Cell, Props.radius);
        }
    }
}
