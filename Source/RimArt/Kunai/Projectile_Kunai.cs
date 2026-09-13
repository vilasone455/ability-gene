using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// A thrown kunai. Damage, armour and the battle log entry are vanilla <see cref="Bullet"/>.
    ///
    /// After impact it drops one kunai item where it stopped, so the belt can be reloaded from
    /// it. A kunai that hits a pawn or building breaks instead with
    /// <see cref="KunaiDefaults.BreakChanceOnHit"/>. One that flies off the map edge is
    /// destroyed by Projectile without calling Impact, and is lost.
    /// </summary>
    public class Projectile_Kunai : Bullet
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            IntVec3 cell = Position;

            base.Impact(hitThing, blockedByShield);

            if (map == null || !cell.InBounds(map)) return;
            if (hitThing != null && !blockedByShield && Rand.Chance(KunaiDefaults.BreakChanceOnHit)) return;

            // Near rather than Direct: a kunai that hit a wall stopped in the wall's cell.
            Thing kunai = ThingMaker.MakeThing(KunaiDefOf.AG_Kunai);
            GenPlace.TryPlaceThing(kunai, cell, map, ThingPlaceMode.Near);
        }
    }
}
