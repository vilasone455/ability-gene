using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>What Accelerator's abilities share: def lookups, the pawn's arm colours for the drawn arm, and the bullets his vectors can hold.</summary>
    public static class AcceleratorKit
    {
        /// <summary>An ability def's comp properties of one kind (AbilityDef has no GetCompProperties).</summary>
        public static T Props<T>(AbilityDef def) where T : AbilityCompProperties
        {
            if (def?.comps == null) return null;
            for (int i = 0; i < def.comps.Count; i++)
                if (def.comps[i] is T props) return props;
            return null;
        }

        /// <summary>
        /// The colour of what covers <paramref name="pawn"/>'s arms, for the drawn arm: the outermost worn piece
        /// that covers the shoulders or arms, else bare skin.
        /// </summary>
        public static Color Sleeve(Pawn pawn)
        {
            if (pawn?.apparel != null)
            {
                Apparel best = null;
                foreach (Apparel worn in pawn.apparel.WornApparel)
                {
                    var groups = worn.def.apparel?.bodyPartGroups;
                    if (groups == null || !groups.Any(g => g.defName == "Arms" || g.defName == "Shoulders")) continue;
                    if (best == null || worn.def.apparel.LastLayer.drawOrder > best.def.apparel.LastLayer.drawOrder) best = worn;
                }
                if (best != null) return best.DrawColor;
            }
            return Skin(pawn);
        }

        public static Color Skin(Pawn pawn) => pawn?.story?.SkinColor ?? AcceleratorGraphics.Skin;

        /// <summary>
        /// The colour of what covers <paramref name="pawn"/>'s legs, for the drawn leg of Vector Flick: the
        /// outermost worn piece that covers the legs, else bare skin.
        /// </summary>
        public static Color Pants(Pawn pawn)
        {
            if (pawn?.apparel != null)
            {
                Apparel best = null;
                foreach (Apparel worn in pawn.apparel.WornApparel)
                {
                    var groups = worn.def.apparel?.bodyPartGroups;
                    if (groups == null || !groups.Contains(BodyPartGroupDefOf.Legs)) continue;
                    if (best == null || worn.def.apparel.LastLayer.drawOrder > best.def.apparel.LastLayer.drawOrder) best = worn;
                }
                if (best != null) return best.DrawColor;
            }
            return Skin(pawn);
        }

        /// <summary>
        /// Whether his vectors can take hold of <paramref name="round"/>: the rule vector manipulation and
        /// Plasma's channel share. Bullets only (which takes in arrows and this mod's loosed blades); mortar
        /// shells (flyOverhead) and anything that explodes are refused, and so are rounds already held by a
        /// phase barrier or frozen in a stasis field. Combat Extended's rounds pass on the same def tests.
        /// </summary>
        public static bool Holdable(Thing round)
        {
            if (round == null || round.Destroyed || !round.Spawned) return false;
            RoundBackend backend = Rounds.For(round);
            if (backend == null) return false;
            if (backend == Rounds.Vanilla && !(round is Bullet)) return false;
            ProjectileProperties props = round.def.projectile;
            if (props == null || props.flyOverhead || props.explosionRadius > 0f) return false;
            if (RecursionRegistry.CapturedCount > 0 && RecursionRegistry.TryGetCapture(round, out HalvingProjectile _)) return false;
            if (TimeBubbleRegistry.ActiveCount > 0 && TimeBubbleRegistry.IsFrozen(round)) return false;
            return true;
        }
    }

    /// <summary>
    /// On Accelerator's hero form: remembers the last melee hit he took, for vector shove's force returned
    /// (the attacker, the tick and the hit's damage before armour). Read-only: nothing about the hit changes.
    /// </summary>
    public class HediffCompProperties_ForceReturn : HediffCompProperties
    {
        public HediffCompProperties_ForceReturn()
        {
            compClass = typeof(HediffComp_ForceReturn);
        }
    }

    public class HediffComp_ForceReturn : HediffComp
    {
        public Pawn attacker;
        public int tick = -1;
        public float damage;

        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);
            if (!(dinfo.Instigator is Pawn by) || by == Pawn || dinfo.Def == null || dinfo.Def.isRanged || dinfo.Def.isExplosive) return;
            if (dinfo.Amount <= 0f || !by.Spawned || by.Map != Pawn.MapHeld || by.Position.DistanceTo(Pawn.PositionHeld) > 1.9f) return;
            attacker = by;
            tick = Find.TickManager.TicksGame;
            damage = dinfo.Amount;
            Pawn.MapHeld?.GetComponent<MapComponent_Shoves>()?.MeleeHit(Pawn, by);
        }

        /// <summary>The damage to return when <paramref name="target"/> hit him within <paramref name="window"/> ticks, else 0.</summary>
        public float Returned(Pawn target, int window)
        {
            if (target == null || target != attacker || tick < 0) return 0f;
            return Find.TickManager.TicksGame - tick <= window ? damage : 0f;
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref attacker, "attacker");
            Scribe_Values.Look(ref tick, "tick", -1);
            Scribe_Values.Look(ref damage, "damage");
        }
    }
}
