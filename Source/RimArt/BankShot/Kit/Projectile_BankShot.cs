using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>On the Bank Shot pistol's bullet def: what its one rebound may do.</summary>
    public class BankShotReboundExtension : DefModExtension
    {
        /// <summary>Cells the rebound flies at most.</summary>
        public float reboundRange = 12f;
        /// <summary>Chance the rebound hits the first standing pawn on its line (the shooter excepted); otherwise it flies past.</summary>
        public float reboundHitChance = 0.5f;
    }

    /// <summary>
    /// The Bank Shot pistol's normal bullet. Aiming, the hit roll, damage and the battle log are
    /// vanilla <see cref="Bullet"/>. The one addition: a bullet that meets a wall (BankShotMap.IsWall,
    /// which is how a missed shot usually ends) does not stop there once. It mirrors off the face it
    /// met, found with the ricochet rule (BankShotPath) walked up to the wall along the bullet's own
    /// line, and a new bullet of the same def flies the mirrored line for up to reboundRange cells.
    /// The rule is walked along that line too: the first standing pawn on it is hit with
    /// reboundHitChance; otherwise the rebound flies past everyone to the next wall or its range end,
    /// and stops there as an ordinary bullet. The rebound never bounces again and cannot hit its
    /// shooter.
    /// </summary>
    public class Projectile_BankShot : Bullet
    {
        private static readonly BankShotReboundExtension Defaults = new BankShotReboundExtension();
        private bool rebounded;

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            if (!rebounded && !blockedByShield && Spawned && BankShotMap.IsWall(hitThing) && TryRebound(hitThing)) return;
            base.Impact(hitThing, blockedByShield);
        }

        private bool TryRebound(Thing wall)
        {
            Map map = Map;
            BankShotReboundExtension ext = def.GetModExtension<BankShotReboundExtension>() ?? Defaults;
            Vector2 from = new Vector2(origin.x, origin.z), flight = new Vector2(destination.x, destination.z) - from;
            if (flight.sqrMagnitude < 0.01f) return false;
            Vector2 dir = flight.normalized;
            float aim = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            // The face: walk the rule toward this wall alone, from a little before it on the bullet's line.
            CellRect rect = wall.OccupiedRect();
            Vector3 middle = rect.CenterVector3;
            float size = Mathf.Max(rect.Width, rect.Height);
            float before = Mathf.Max(0f, Vector2.Dot(new Vector2(middle.x, middle.z) - from, dir) - 1.5f - size / 2f);
            Vector2 start = from + dir * before;
            BankShotPath meet = BankShotPath.Trace((x, z) => rect.Contains(new IntVec3(x, 0, z)), null, start.x - 0.5, start.y - 0.5, aim, 0, 3.0 + size);
            if (meet.End != BankShotEnd.Embed) return false;

            Vector2 contact = BankShotMap.ToMap(meet.EndPoint.X, meet.EndPoint.Z), normal = new Vector2(meet.Embed.NormalX, meet.Embed.NormalZ);
            Vector2 away = normal.x != 0f ? new Vector2(-dir.x, dir.y) : new Vector2(dir.x, -dir.y);
            float reboundAim = Mathf.Atan2(away.y, away.x) * Mathf.Rad2Deg;
            Vector2 leave = contact + normal * 0.05f;
            var leaveCell = new IntVec3(Mathf.FloorToInt(leave.x), 0, Mathf.FloorToInt(leave.y));
            if (!leaveCell.InBounds(map) || BankShotMap.Wall(map, leaveCell)) return false;

            Pawn shooter = launcher as Pawn;
            BankShotPath line = BankShotMap.Trace(map, leave, reboundAim, 0, ext.reboundRange, shooter);
            Pawn victim = BankShotMap.Victim(map, line, shooter);
            LocalTargetInfo target;
            ProjectileHitFlags flags;
            if (victim != null && Rand.Chance(ext.reboundHitChance))
            {
                target = victim;
                flags = ProjectileHitFlags.IntendedTarget;
            }
            else
            {
                if (victim != null) line = BankShotMap.Trace(map, leave, reboundAim, 0, ext.reboundRange, shooter, false);
                Vector2 end = BankShotMap.ToMap(line.EndPoint.X, line.EndPoint.Z);
                target = line.End == BankShotEnd.Embed ? new IntVec3(line.Embed.CellX, 0, line.Embed.CellZ)
                    : new IntVec3(Mathf.FloorToInt(end.x), 0, Mathf.FloorToInt(end.y));
                if (!target.Cell.InBounds(map)) return false;
                flags = ProjectileHitFlags.NonTargetWorld;
            }

            FleckMaker.ThrowMicroSparks(new Vector3(contact.x, 0f, contact.y), map);
            Building hit = wall as Building;
            PowerPoleSound.Play(hit?.Stuff?.stuffProps?.soundImpactBullet ?? wall.def.soundImpactDefault, map, contact);

            var next = (Projectile_BankShot)GenSpawn.Spawn(def, leaveCell, map);
            next.rebounded = true;
            next.Launch(launcher, new Vector3(leave.x, def.Altitude, leave.y), target, target, flags, preventFriendlyFire, equipment, targetCoverDef);
            Destroy();
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref rebounded, "bankShotRebounded");
        }
    }
}
