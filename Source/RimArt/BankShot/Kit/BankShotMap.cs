using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The ricochet rule (BankShotPath) on a real map. The rule works in cells whose centres are
    /// whole numbers; a map cell's centre is its index plus 0.5, so points are shifted by 0.5 on
    /// the way in and out.
    ///
    /// What the rule calls a wall is a cell whose edifice has Full fillage, the same test vanilla
    /// projectiles use to stop at a thing: walls, natural rock, closed doors, and any other
    /// building that fills its cell. An open door, filth, plants, sandbags and other partial cover
    /// are not walls. The rule's pawn is a standing pawn (not downed, not lying), because the
    /// bullet flies at chest height. The map's edge ends the flight.
    /// </summary>
    public static class BankShotMap
    {
        public static bool IsWall(Thing thing) =>
            thing != null && thing.def.category == ThingCategory.Building && thing.def.Fillage == FillCategory.Full
            && !(thing is Building_Door door && door.Open);

        public static bool Wall(Map map, IntVec3 cell) => cell.InBounds(map) && IsWall(cell.GetEdifice(map));

        /// <summary>The first standing pawn in the cell other than <paramref name="except"/>, or null.</summary>
        public static Pawn StandingPawn(Map map, IntVec3 cell, Pawn except)
        {
            if (!cell.InBounds(map)) return null;
            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
                if (things[i] is Pawn pawn && pawn != except && !pawn.Dead && !pawn.Downed && pawn.GetPosture() == PawnPosture.Standing)
                    return pawn;
            return null;
        }

        /// <summary>
        /// The flight from the map point <paramref name="start"/> along <paramref name="aim"/> degrees.
        /// <paramref name="pawns"/> false flies through pawns (the rebound's miss).
        /// </summary>
        public static BankShotPath Trace(Map map, Vector2 start, float aim, int maxBounces, float range, Pawn except, bool pawns = true)
        {
            return BankShotPath.Trace(
                (x, z) => Wall(map, new IntVec3(x, 0, z)),
                pawns ? (x, z) => StandingPawn(map, new IntVec3(x, 0, z), except) != null : (System.Func<int, int, bool>)null,
                start.x - 0.5, start.y - 0.5, aim, maxBounces, range,
                (x, z) => !new IntVec3(x, 0, z).InBounds(map));
        }

        /// <summary>The map point of a rule point.</summary>
        public static Vector2 ToMap(double x, double z) => new Vector2((float)x + 0.5f, (float)z + 0.5f);

        /// <summary>
        /// A charged shot from <paramref name="caster"/> toward <paramref name="target"/>: the aim is
        /// from the caster's cell centre to the target cell's, and the bullet leaves from the muzzle,
        /// <see cref="BankShotTiming.MuzzleAlong"/> along it.
        /// </summary>
        public static BankShotShot Shot(Pawn caster, IntVec3 target, float charge, CompProperties_BankShotCharge props)
        {
            Vector3 c = caster.Position.ToVector3Shifted();
            var feet = new Vector2(c.x, c.z);
            Vector2 toward = new Vector2(target.x - caster.Position.x, target.z - caster.Position.z);
            float aim = toward.sqrMagnitude < 0.01f ? 0f : Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg;
            Vector2 muzzle = feet + VfxDraw.Turn(aim) * BankShotTiming.MuzzleAlong;
            return new BankShotShot
            {
                Path = Trace(caster.Map, muzzle, aim, props.maxBounces, props.range, caster),
                Offset = new Vector2(0.5f, 0.5f),
                Caster = feet,
                Aim = aim,
                Charge = charge,
                Speed = Mathf.Max(1f, props.speed),
                MaxBounces = props.maxBounces,
            };
        }

        /// <summary>The pawn a charged shot stops at, or null when it embeds or is spent.</summary>
        public static Pawn Victim(Map map, BankShotPath path, Pawn caster) =>
            path.End == BankShotEnd.Hit ? StandingPawn(map, new IntVec3(path.HitCellX, 0, path.HitCellZ), caster) : null;

        /// <summary>18 + 6 per bounce with the default fields.</summary>
        public static float Damage(CompProperties_BankShotCharge props, int bounces) => props.baseDamage + props.damagePerBounce * bounces;
    }
}
