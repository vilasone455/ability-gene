using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using T = RimArt.EgoMimicryTiming;

namespace RimArt
{
    /// <summary>Where a Mimicry swing comes from: the wielder's own melee attack, the corroded hunt, or Overclock.</summary>
    public enum EgoMimicrySource { Ordinary, Corroded, Overclock }

    /// <summary>
    /// Mimicry's rules apart from the swing's timing (docs/ego-weapons.md, Weapon 3): melee reach, the corroded lunge's
    /// landing cell, who is under the grown blade, what a landed hit does (the lifesteal and the arm's growth), and the arm
    /// losing a stage when the wielder is hit. The swing itself is <see cref="EgoMimicryCast"/> and <see cref="Verb_EgoMimicry"/>.
    /// </summary>
    public static class EgoMimicry
    {
        /// <summary>Ordinary swings that rolled Corrosion since the count was last set, for the tests' once-per-swing checks.</summary>
        internal static int rollsForTests;

        /// <summary>Whether the wielder is corroded or overclocking with this sword: the arm and its damage count only then.</summary>
        public static bool Special(Pawn wielder, CompEgoMimicry sword) =>
            sword != null && wielder != null && ((wielder.MentalState is MentalState_EgoCorroded state && state.weapon == sword.parent)
                || (wielder.jobs?.curDriver is JobDriver_EgoOverclock overclock && overclock.Weapon == sword));

        /// <summary>The source of a swing the wielder starts now with this sword.</summary>
        public static EgoMimicrySource SourceOf(Pawn wielder, CompEgoMimicry sword)
        {
            if (wielder.MentalState is MentalState_EgoCorroded state && state.weapon == sword.parent) return EgoMimicrySource.Corroded;
            if (wielder.jobs?.curDriver is JobDriver_EgoOverclock overclock && overclock.Weapon == sword) return EgoMimicrySource.Overclock;
            return EgoMimicrySource.Ordinary;
        }

        /// <summary>
        /// Whether a melee swing from <paramref name="from"/> reaches <paramref name="target"/>: next to it, diagonals included
        /// unless a wall closes the corner (Core's touch rule for melee, ReachabilityImmediate).
        /// </summary>
        public static bool Reaches(Pawn wielder, IntVec3 from, Thing target) =>
            target != null && target.Spawned && wielder.Spawned && target.Map == wielder.Map
            && ReachabilityImmediate.CanReachImmediate(from, target, wielder.Map, PathEndMode.Touch, wielder);

        public static bool Reaches(Pawn wielder, Thing target) => Reaches(wielder, wielder.Position, target);

        /// <summary>
        /// The corroded lunge's landing cell: within <paramref name="cells"/> of the wielder, standable, nobody on it, reached
        /// on a clear straight line (<see cref="LineClear"/>), and close enough to swing at <paramref name="target"/> from.
        /// The shortest lunge wins, then the cell nearest the target. False when there is none: the walk goes on.
        /// </summary>
        public static bool Landing(Pawn wielder, Thing target, float cells, out IntVec3 landing)
        {
            landing = IntVec3.Invalid;
            Map map = wielder.Map;
            IntVec3 from = wielder.Position;
            float best = float.MaxValue;
            foreach (IntVec3 c in GenRadial.RadialCellsAround(from, cells, useCenter: false))
            {
                if (!c.InBounds(map) || !c.Standable(map) || !Reaches(wielder, c, target)) continue;
                Pawn standing = c.GetFirstPawn(map);
                if (standing != null && standing != wielder) continue;
                if (!LineClear(map, from, c)) continue;
                float score = (c - from).LengthHorizontalSquared + 0.01f * (c - target.Position).LengthHorizontalSquared;
                if (score < best)
                {
                    best = score;
                    landing = c;
                }
            }
            return landing.IsValid;
        }

        /// <summary>
        /// Nothing in the way of a straight dash from one cell's centre to the other's: every cell it crosses is walkable with
        /// no closed door, and a diagonal step needs both corner cells open, as Core's pathing does, so the lunge never cuts
        /// past a wall's corner. A line that runs exactly through a grid corner (a dash of one cell diagonally) touches all
        /// four cells round it; sampling alone would pass on one side of the corner and miss the other.
        /// </summary>
        public static bool LineClear(Map map, IntVec3 from, IntVec3 to)
        {
            Vector3 a = from.ToVector3Shifted(), b = to.ToVector3Shifted();
            float length = (b - a).magnitude;
            for (int x = Mathf.CeilToInt(Mathf.Min(a.x, b.x)); x <= Mathf.FloorToInt(Mathf.Max(a.x, b.x)); x++)
                for (int z = Mathf.CeilToInt(Mathf.Min(a.z, b.z)); z <= Mathf.FloorToInt(Mathf.Max(a.z, b.z)); z++)
                {
                    float cross = (b.x - a.x) * (z - a.z) - (b.z - a.z) * (x - a.x);
                    if (Mathf.Abs(cross) > 1e-3f * length) continue;
                    if (!Open(map, new IntVec3(x - 1, 0, z - 1)) || !Open(map, new IntVec3(x, 0, z - 1))
                        || !Open(map, new IntVec3(x - 1, 0, z)) || !Open(map, new IntVec3(x, 0, z))) return false;
                }
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / 0.25f));
            IntVec3 last = from;
            for (int i = 1; i <= steps; i++)
            {
                IntVec3 c = Vector3.Lerp(a, b, i / (float)steps).ToIntVec3();
                if (c == last) continue;
                if (!Open(map, c)) return false;
                if (c.x != last.x && c.z != last.z && (!Open(map, new IntVec3(c.x, 0, last.z)) || !Open(map, new IntVec3(last.x, 0, c.z)))) return false;
                last = c;
            }
            return true;
        }

        private static bool Open(Map map, IntVec3 c) => c.InBounds(map) && c.Walkable(map) && !(c.GetDoor(map) is Building_Door door && !door.Open);

        /// <summary>
        /// The grown blade's strip on the floor for a wielder at <paramref name="pos"/> (its cell's centre) aiming
        /// <paramref name="aim"/>: the picture's footprint at the swing's size, read as map coordinates.
        /// </summary>
        public static void Strip(Vector2 pos, float aim, float sign, CompProperties_EgoMimicry p, out Vector2 from, out Vector2 to) =>
            T.SlamFootprint(pos, aim, p.growScale, out from, out to, sign);

        /// <summary>
        /// Whether the strip from <paramref name="a"/> to <paramref name="b"/>, <paramref name="halfWidth"/> to each side, touches
        /// the square of <paramref name="cell"/>: some point of the strip's middle line within halfWidth of the square, tried
        /// every 0.05 cells.
        /// </summary>
        public static bool CellUnder(IntVec3 cell, Vector2 a, Vector2 b, float halfWidth)
        {
            float x0 = cell.x, x1 = cell.x + 1f, z0 = cell.z, z1 = cell.z + 1f, w2 = halfWidth * halfWidth;
            int n = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / 0.05f));
            for (int i = 0; i <= n; i++)
            {
                Vector2 q = Vector2.Lerp(a, b, i / (float)n);
                float dx = Mathf.Max(x0 - q.x, 0f, q.x - x1), dz = Mathf.Max(z0 - q.y, 0f, q.y - z1);
                if (dx * dx + dz * dz <= w2) return true;
            }
            return false;
        }

        /// <summary>Whether the grown blade strikes <paramref name="pawn"/> under it: the pawn the swing was aimed at, and any other hostile that is standing. Never the wielder.</summary>
        public static bool Strikes(Pawn wielder, Thing target, Pawn pawn) =>
            pawn != wielder && !pawn.Dead && (pawn == target || (!pawn.Downed && pawn.HostileTo(wielder)));

        /// <summary>
        /// Every pawn the grown blade strikes, into <paramref name="into"/>, each once: a pawn <see cref="Strikes"/> takes with
        /// a cell under the strip (<see cref="CellUnder"/>) that the wielder can see from its cell, so a wall or a closed door
        /// between keeps it out. A copy, so the slam can kill from it.
        /// </summary>
        public static List<Pawn> Under(Pawn wielder, Thing target, Vector2 from, Vector2 to, float halfWidth, List<Pawn> into)
        {
            into.Clear();
            Map map = wielder.Map;
            CellRect area = CellRect.FromLimits(Mathf.FloorToInt(Mathf.Min(from.x, to.x) - halfWidth), Mathf.FloorToInt(Mathf.Min(from.y, to.y) - halfWidth),
                Mathf.FloorToInt(Mathf.Max(from.x, to.x) + halfWidth), Mathf.FloorToInt(Mathf.Max(from.y, to.y) + halfWidth)).ClipInsideMap(map);
            foreach (IntVec3 c in area)
            {
                if (!CellUnder(c, from, to, halfWidth)) continue;
                if (c != wielder.Position && !GenSight.LineOfSight(wielder.Position, c, map, skipFirstCell: true)) continue;
                List<Thing> things = c.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                    if (things[i] is Pawn pawn && !into.Contains(pawn) && Strikes(wielder, target, pawn)) into.Add(pawn);
            }
            return into;
        }

        /// <summary>
        /// What a hit that dealt <paramref name="dealt"/> to <paramref name="victim"/> does for the wielder: the lifesteal
        /// (healFraction of it, off injuries that are not scars, from flesh pawns only: never a building or a mechanoid), and
        /// while corroded or overclocking one arm stage more. A miss or a hit armour turned to nothing (0 dealt) does neither.
        /// </summary>
        public static void Landed(Pawn wielder, CompEgoMimicry sword, Thing victim, float dealt)
        {
            if (dealt <= 0f || wielder == null || wielder.Dead) return;
            GameComponent_EgoMimicry game = GameComponent_EgoMimicry.Instance;
            if (victim is Pawn pawn && pawn.RaceProps != null && pawn.RaceProps.IsFlesh)
            {
                InjuryHeal.Heal(wielder, dealt * sword.Props.healFraction);
                game?.Fed(wielder);
            }
            if (Special(wielder, sword) && sword.Grow()) game?.StageChanged(wielder);
        }

        /// <summary>
        /// One damaging hit on a corroded or overclocking wielder (from Pawn.PostApplyDamage): the arm loses a stage. Damage the
        /// wielder deals itself does not count, nor a hit that dealt nothing.
        /// </summary>
        public static void HitTaken(Pawn pawn, DamageInfo dinfo, float dealt)
        {
            if (dealt <= 0f || dinfo.Instigator == pawn) return;
            CompEgoMimicry sword = CompEgoMimicry.HeldBy(pawn);
            if (sword == null || !Special(pawn, sword) || !sword.Shrink()) return;
            GameComponent_EgoMimicry.Instance?.Torn(pawn, dinfo.Instigator);
        }

        /// <summary>Corrosion's roll for one ordinary swing that hit or missed: once per swing, never per pawn under a grown blade.</summary>
        public static void Roll(Pawn wielder, CompEgoMimicry sword)
        {
            rollsForTests++;
            EgoCorrosion.Roll(wielder, sword);
        }
    }
}
