using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Magic Bullet's rules with no clock (docs/ego-weapons.md, Weapon 1): whom the seventh goes to, what a line crosses,
    /// and the damage it does. <see cref="EgoMagicBulletCast"/> calls them when a shot goes off.
    /// </summary>
    public static class EgoMagicBullet
    {
        /// <summary>
        /// The seventh's target, Der Freischütz's bride. In order: the one living pawn on the map the shooter has the highest
        /// opinion of, above 0; on a tie at the top, or nobody above 0, a bonded animal on the map (the nearest); on a tie
        /// with no bonded animal, the nearest of the tied; nobody at all, the shooter.
        /// </summary>
        public static Pawn Beloved(Pawn shooter)
        {
            if (shooter?.Map == null) return shooter;
            IReadOnlyList<Pawn> pawns = shooter.Map.mapPawns.AllPawnsSpawned;
            int best = 0;
            Pawn top = null, bond = null;
            bool tie = false;
            if (shooter.relations != null)
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn p = pawns[i];
                    if (p == shooter || p.Dead) continue;
                    if (p.RaceProps.Humanlike)
                    {
                        int opinion = shooter.relations.OpinionOf(p);
                        if (opinion <= 0 || opinion < best) continue;
                        if (opinion == best)
                        {
                            tie = true;
                            if (Nearer(shooter, p, top)) top = p;
                            continue;
                        }
                        best = opinion;
                        top = p;
                        tie = false;
                    }
                    else if (shooter.relations.DirectRelationExists(PawnRelationDefOf.Bond, p) && Nearer(shooter, p, bond)) bond = p;
                }
            if (top != null && !tie) return top;
            return bond ?? top ?? shooter;
        }

        private static bool Nearer(Pawn from, Pawn a, Pawn b) =>
            b == null || (a.Position - from.Position).LengthHorizontalSquared < (b.Position - from.Position).LengthHorizontalSquared;

        /// <summary>The ground point at the centre of a cell, (x, z).</summary>
        public static Vector2 Centre(IntVec3 cell) => new Vector2(cell.x + 0.5f, cell.z + 0.5f);

        /// <summary>
        /// Whether the line from <paramref name="from"/> along the unit <paramref name="dir"/> passes through
        /// <paramref name="cell"/> within <paramref name="length"/> cells, and how far along it the cell's centre lies. A line
        /// passes through a square when its distance from the centre is under the square's half-width across the line,
        /// 0.5 (|dx| + |dz|); a line that only grazes a corner does not count. The cell the line starts in never counts.
        /// </summary>
        public static bool Crosses(Vector2 from, Vector2 dir, float length, IntVec3 cell, out float along)
        {
            Vector2 c = Centre(cell) - from;
            along = c.x * dir.x + c.y * dir.y;
            float across = Mathf.Abs(c.y * dir.x - c.x * dir.y), half = 0.5f * (Mathf.Abs(dir.x) + Mathf.Abs(dir.y));
            return along > 0.5f * half && along <= length + 0.01f && across < half - 0.01f;
        }

        /// <summary>
        /// Every spawned living pawn the line crosses, nearest first, downed ones included, the shooter never. With
        /// <paramref name="hostilesOnly"/> (Overclock) only pawns hostile to the shooter; the line passes through the rest.
        /// </summary>
        public static List<Pawn> Crossed(Pawn shooter, Vector2 from, Vector2 dir, float length, bool hostilesOnly, List<float> alongs = null)
        {
            var hit = new List<Pawn>();
            alongs?.Clear();
            IReadOnlyList<Pawn> pawns = shooter.Map.mapPawns.AllPawnsSpawned;
            var order = new List<float>();
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p == shooter || p.Dead || (hostilesOnly && !p.HostileTo(shooter))) continue;
                if (!Crosses(from, dir, length, p.Position, out float along)) continue;
                int at = order.Count;
                while (at > 0 && order[at - 1] > along) at--;
                order.Insert(at, along);
                hit.Insert(at, p);
            }
            alongs?.AddRange(order);
            return hit;
        }

        /// <summary>
        /// The filled cells (walls, rock, anything whose edifice fills its cell) the line passes, nearest first, and how far
        /// along each: for the picture's punched holes. Nothing stops the line.
        /// </summary>
        public static void Walls(Map map, Vector2 from, Vector2 dir, float length, List<IntVec3> cells, List<float> alongs)
        {
            cells.Clear();
            alongs.Clear();
            IntVec3 last = IntVec3.Invalid;
            for (float t = 0.5f; t <= length; t += 0.2f)
            {
                Vector2 q = from + dir * t;
                var cell = new IntVec3(Mathf.FloorToInt(q.x), 0, Mathf.FloorToInt(q.y));
                if (cell == last) continue;
                last = cell;
                if (!cell.InBounds(map)) break;
                if (cell.Filled(map) && Crosses(from, dir, length, cell, out float along))
                {
                    cells.Add(cell);
                    alongs.Add(along);
                }
            }
        }

        /// <summary>
        /// One pawn hit by the line, or the target that is not a pawn: the round's damage def and armour penetration,
        /// <paramref name="amount"/> damage, from the shooter with the weapon, logged as a ranged impact the way a bullet's
        /// is. Pawns behind it are hit as well.
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
