using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// A thrown kunai. Damage, armour and the battle log entry are vanilla <see cref="Bullet"/>.
    ///
    /// After impact the kunai goes one of three ways:
    ///   breaks   a hit on a pawn or building, <see cref="KunaiDefaults.BreakChanceOnHit"/>
    ///   sticks   a hit on a living pawn that made an injury: <see cref="KunaiEmbedding.TryEmbed"/>
    ///   plants   a miss that comes down on open ground: it stands in the ground at the angle it
    ///            flew (<see cref="KunaiEmbedding.PlantKunai"/>)
    ///   drops    everything else - buildings, shields, armour stopping it, a killing hit, a pawn
    ///            already holding <see cref="KunaiDefaults.MaxEmbeddedPerPawn"/> kunai, a miss into
    ///            water
    /// A kunai that flies off the map edge is destroyed by Projectile without calling Impact, and
    /// is lost. Minato's (AG_KunaiProjectileMinato) lands, sticks or drops sealed (<see cref="KunaiSeal"/>).
    /// </summary>
    public class Projectile_Kunai : Bullet
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            IntVec3 cell = Position;
            Vector3 landed = ExactPosition;
            float angle = (destination - origin).AngleFlat();
            bool sealedByMinato = def == KunaiDefOf.AG_KunaiProjectileMinato;

            // The injury the hit makes is found by comparing hediffs before and after: Bullet.Impact
            // does not return the damage result.
            Pawn pawn = hitThing as Pawn;
            HashSet<Hediff> before = pawn?.health?.hediffSet != null
                ? new HashSet<Hediff>(pawn.health.hediffSet.hediffs)
                : null;

            base.Impact(hitThing, blockedByShield);

            if (map == null || !cell.InBounds(map)) return;
            if (hitThing != null && !blockedByShield && Rand.Chance(KunaiDefaults.BreakChanceOnHit)) return;
            if (!blockedByShield && KunaiEmbedding.TryEmbed(pawn, before, sealedByMinato)) return;
            if (hitThing == null && !blockedByShield && KunaiEmbedding.PlantKunai(landed, angle, map, sealedByMinato)) return;

            // Near rather than Direct: a kunai that hit a wall stopped in the wall's cell, and one
            // that killed its target drops beside the body.
            KunaiEmbedding.DropKunai(pawn != null && pawn.Dead ? pawn.PositionHeld : cell, map, sealedByMinato);
        }
    }
}
