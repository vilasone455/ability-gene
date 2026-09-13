using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Spikes on one cell, for <see cref="MakibishiDefaults.LifetimeTicks"/>.
    ///
    /// Pathing: any thing whose class implements <see cref="IPathFindCostProvider"/> is listed in
    /// ThingRequestGroup.CostProvider, and PathGridDoorsBlockedJob adds its PathFindCostFor(pawn) to
    /// the cell for that pawn's path. It does not have to be an edifice, so these share a cell with
    /// a door. Pawns go around when the detour is cheaper and walk through when it is not.
    ///
    /// Stepping: the same touching-pawns list as Building_Trap. Each time a pawn enters the cell it
    /// rolls <see cref="MakibishiDefaults.TriggerChance"/> once; standing on the cell does not roll
    /// again.
    /// </summary>
    public class Makibishi : Building, IPathFindCostProvider
    {
        private int expireTick;
        private Pawn thrower;
        private List<Pawn> touchingPawns = new List<Pawn>();

        /// <summary>Starts or restarts the 30 s timer. The latest thrower is blamed for wounds.</summary>
        public void Arm(Pawn by)
        {
            expireTick = Find.TickManager.TicksGame + MakibishiDefaults.LifetimeTicks;
            if (by != null) thrower = by;
        }

        /// <summary>Flying pawns and mechanoids are not hurt, and so do not path around it.</summary>
        public static bool Affects(Pawn pawn)
        {
            return pawn != null && !pawn.Flying && !pawn.RaceProps.IsMechanoid;
        }

        public CellRect GetOccupiedRect() => this.OccupiedRect();

        public ushort PathFindCostFor(Pawn p) => Affects(p) ? MakibishiDefaults.PathFindCost : (ushort)0;

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned) return;

            if (Find.TickManager.TicksGame >= expireTick)
            {
                Destroy(DestroyMode.Vanish);
                return;
            }

            List<Thing> things = Map.thingGrid.ThingsListAt(Position);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] is Pawn pawn && !touchingPawns.Contains(pawn))
                {
                    touchingPawns.Add(pawn);
                    Step(pawn);
                }
            }
            for (int i = touchingPawns.Count - 1; i >= 0; i--)
            {
                Pawn pawn = touchingPawns[i];
                if (pawn == null || !pawn.Spawned || pawn.Position != Position) touchingPawns.RemoveAt(i);
            }
        }

        private void Step(Pawn pawn)
        {
            if (!Affects(pawn) || pawn.Dead) return;
            if (!Rand.Chance(MakibishiDefaults.TriggerChance)) return;
            Wound(pawn, thrower);
        }

        /// <summary>One spike: a stab to a foot, and the movement penalty reset to full.</summary>
        public static void Wound(Pawn pawn, Pawn instigator)
        {
            BodyPartRecord part = FootOf(pawn);
            if (part != null)
            {
                var dinfo = new DamageInfo(DamageDefOf.Stab, MakibishiDefaults.WoundDamage,
                    MakibishiDefaults.WoundArmorPenetration, -1f, instigator, part);
                dinfo.SetAllowDamagePropagation(false);
                pawn.TakeDamage(dinfo);
            }
            if (pawn.Dead || pawn.health == null) return;

            // Replaced rather than topped up, which restarts its 15 s timer at the full penalty.
            Hediff old = pawn.health.hediffSet.GetFirstHediffOfDef(MakibishiDefOf.AG_PuncturedFoot);
            if (old != null) pawn.health.RemoveHediff(old);
            pawn.health.AddHediff(MakibishiDefOf.AG_PuncturedFoot);

            if (pawn.Spawned)
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Stepped on makibishi", new Color(0.9f, 0.55f, 0.45f));
        }

        /// <summary>
        /// The part a spike goes into. In order: a foot (group Feet, not a toe), a paw or hoof
        /// (tag MovingLimbSegment, outside), then any leg (tag MovingLimbCore). Null for a body with
        /// none of these, which still gets the movement penalty.
        /// </summary>
        public static BodyPartRecord FootOf(Pawn pawn)
        {
            HediffSet hediffs = pawn.health?.hediffSet;
            if (hediffs == null) return null;
            List<BodyPartRecord> parts = hediffs.GetNotMissingParts().ToList();

            BodyPartRecord foot = parts.Where(p => p.groups.Contains(MakibishiDefOf.Feet)
                                                   && (p.parent == null || !p.parent.groups.Contains(MakibishiDefOf.Feet)))
                                       .RandomElementWithFallback();
            if (foot != null) return foot;

            BodyPartRecord paw = parts.Where(p => p.depth == BodyPartDepth.Outside
                                                  && p.def.tags.Contains(BodyPartTagDefOf.MovingLimbSegment))
                                      .RandomElementWithFallback();
            if (paw != null) return paw;

            return parts.Where(p => p.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore)).RandomElementWithFallback();
        }

        public override string GetInspectString()
        {
            string basic = base.GetInspectString();
            int left = Mathf.Max(0, expireTick - Find.TickManager.TicksGame);
            string mine = "Gone in " + left.ToStringSecondsFromTicks() + ".";
            return string.IsNullOrEmpty(basic) ? mine : basic + "\n" + mine;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref expireTick, "expireTick", 0);
            Scribe_References.Look(ref thrower, "thrower");
            Scribe_Collections.Look(ref touchingPawns, "touchingPawns", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (touchingPawns == null) touchingPawns = new List<Pawn>();
                touchingPawns.RemoveAll(p => p == null);
            }
        }
    }
}
