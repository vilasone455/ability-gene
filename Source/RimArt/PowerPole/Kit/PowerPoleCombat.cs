using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>What the pole does to pawns: the blunt hit, the carry along the thrust, the vault's strike.</summary>
    public static class PowerPoleCombat
    {
        /// <summary><paramref name="toward"/> is the way the pole was moving, for the wound's direction.</summary>
        public static void Hit(Pawn victim, Pawn caster, float damage, float armorPenetration, Vector2 toward, int staggerTicks)
        {
            if (damage >= 1f)
            {
                float angle = new Vector3(toward.x, 0f, toward.y).AngleFlat();
                victim.TakeDamage(new DamageInfo(DamageDefOf.Blunt, damage, armorPenetration, angle, caster, null, PowerPoleDefOf.AG_PowerPole,
                    DamageInfo.SourceCategory.ThingOrUnknown, victim));
            }
            if (staggerTicks > 0 && victim.Spawned && !victim.Dead) victim.stances?.stagger?.StaggerFor(staggerTicks);
        }

        /// <summary>
        /// Where a pawn pushed from <paramref name="start"/> along <paramref name="toward"/> ends up.
        /// It is walked one cell at a time and stops at the first cell it cannot stand in, as
        /// VectorPush walks a shove. <paramref name="blocked"/> is true when that happened early.
        /// </summary>
        public static IntVec3 PushDestination(IntVec3 start, Vector2 toward, float cells, Map map, out bool blocked)
        {
            IntVec3 dest = start;
            blocked = false;
            var direction = new Vector3(toward.x, 0f, toward.y);
            for (int i = 1; i <= Mathf.RoundToInt(cells); i++)
            {
                IntVec3 c = (start.ToVector3Shifted() + direction * i).ToIntVec3();
                if (c == dest) continue;
                if (!c.InBounds(map) || !c.Standable(map) || !GenSight.LineOfSight(start, c, map, true))
                {
                    blocked = true;
                    break;
                }
                dest = c;
            }
            return dest;
        }

        /// <summary>Slides the pawn to <paramref name="dest"/> over the time the picture's carry takes.</summary>
        public static void Carry(Pawn victim, IntVec3 dest, Map map)
        {
            PawnFlyer flyer = PawnFlyer.MakeFlyer(PowerPoleDefOf.AG_PowerPoleCarried, victim, dest, null, null);
            if (flyer != null) GenSpawn.Spawn(flyer, dest, map);
        }

        /// <summary>The vault's strike: damage and stun on the target cell, stagger round it.</summary>
        public static void Strike(IntVec3 cell, Map map, Pawn caster, Vector2 toward, CompProperties_PowerPoleStrike props)
        {
            var near = new List<Pawn>();
            foreach (IntVec3 c in GenRadial.RadialCellsAround(cell, props.staggerRadius, true))
            {
                if (!c.InBounds(map)) continue;
                List<Thing> things = c.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                    if (things[i] is Pawn pawn && pawn != caster && !pawn.Dead) near.Add(pawn);
            }
            for (int i = 0; i < near.Count; i++)
            {
                Pawn pawn = near[i];
                if (pawn.Position != cell)
                {
                    pawn.stances?.stagger?.StaggerFor(props.staggerTicks);
                    continue;
                }
                Hit(pawn, caster, props.damage, props.armorPenetration, toward, 0);
                if (pawn.Spawned && !pawn.Dead) pawn.stances?.stunner?.StunFor(props.stunTicks, caster, false, true);
            }
        }

        /// <summary>Holds a pawn where it stands for a moment, then lets it go back to what it was doing.</summary>
        public static void StandFor(Pawn pawn, int ticks)
        {
            if (pawn?.jobs == null || pawn.Downed) return;
            // Its own job def, which the player cannot interrupt: a move order given now waits these few ticks,
            // or the wielder would walk off while the pole is still pinned on the target.
            Job wait = JobMaker.MakeJob(PowerPoleDefOf.AG_PowerPoleRecover);
            wait.expiryInterval = ticks;
            wait.checkOverrideOnExpire = false;
            pawn.jobs.StartJob(wait, JobCondition.InterruptForced, null, true);
        }
    }
}
