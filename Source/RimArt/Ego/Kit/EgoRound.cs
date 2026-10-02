using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// A hit by an E.G.O. gun whose round is never spawned (Magic Bullet's line, Solemn Lament's black shot): the round
    /// def's damage def and armour penetration, logged as a ranged impact the way a bullet's is.
    /// </summary>
    public static class EgoRound
    {
        /// <summary>
        /// <paramref name="amount"/> damage to <paramref name="victim"/> from <paramref name="shooter"/> with
        /// <paramref name="weapon"/>, flying along <paramref name="dir"/> (a ground direction).
        /// </summary>
        public static void Hit(Pawn shooter, ThingWithComps weapon, ThingDef round, Thing victim, Thing intended, float amount, Vector2 dir)
        {
            ProjectileProperties p = round.projectile;
            var entry = new BattleLogEntry_RangedImpact(shooter, victim, intended, weapon.def, round, null);
            Find.BattleLog.Add(entry);
            var dinfo = new DamageInfo(p.damageDef, amount, p.GetArmorPenetration(weapon), new Vector3(dir.x, 0f, dir.y).AngleFlat(),
                shooter, null, weapon.def, DamageInfo.SourceCategory.ThingOrUnknown, intended);
            victim.TakeDamage(dinfo).AssociateWithLog(entry);
        }
    }
}
