using RimWorld;
using UnityEngine;
using Verse;

namespace AbilityGenes
{
    /// <summary>
    /// Shared "seize a body's momentum and spend it" move, used by vector shove and by the
    /// momentum bank's release.
    ///
    /// The pawn is walked outward one cell at a time and stops at the first cell it cannot
    /// stand in or cannot see from where it started, so a shove into a wall moves them up to
    /// the wall rather than through it - and being stopped early is what makes the slam hurt.
    /// </summary>
    public static class VectorPush
    {
        public static void Shove(Pawn victim, IntVec3 origin, float cells, int stunTicks,
                                 float damagePerCell, float slamDamage, Thing instigator,
                                 bool scaleByBodySize = true)
        {
            if (victim == null || !victim.Spawned || victim.Dead) return;

            Map map = victim.Map;
            IntVec3 start = victim.Position;

            Vector3 dir = (start - origin).ToVector3();
            // Standing exactly on the origin: no vector to reverse, so pick one.
            if (dir.sqrMagnitude < 0.01f) dir = Rand.InsideUnitCircleVec3;
            dir = dir.normalized;

            if (scaleByBodySize) cells /= Mathf.Max(1f, victim.BodySize);

            IntVec3 dest = start;
            bool blocked = false;
            int max = Mathf.RoundToInt(cells);
            for (int i = 1; i <= max; i++)
            {
                IntVec3 c = (start.ToVector3Shifted() + dir * i).ToIntVec3();
                if (c == dest) continue;
                if (!c.InBounds(map) || !c.Standable(map) || !GenSight.LineOfSight(start, c, map, true))
                {
                    blocked = true;
                    break;
                }
                dest = c;
            }

            float travelled = (dest - start).LengthHorizontal;
            if (dest != start)
            {
                victim.Position = dest;
                victim.Notify_Teleported(true, true);
                FleckMaker.ThrowDustPuffThick(dest.ToVector3Shifted(), map, 2f, Color.white);
            }

            if (stunTicks > 0 && victim.stances != null && victim.stances.stunner != null)
            {
                victim.stances.stunner.StunFor(stunTicks, instigator, false, true);
            }

            float damage = travelled * damagePerCell + (blocked ? slamDamage : 0f);
            if (damage >= 1f)
            {
                victim.TakeDamage(new DamageInfo(
                    DamageDefOf.Blunt, damage, 0f, dir.AngleFlat(), instigator, null, null,
                    DamageInfo.SourceCategory.ThingOrUnknown, victim));
            }
        }
    }
}
