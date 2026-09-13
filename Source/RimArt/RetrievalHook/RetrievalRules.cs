using System;
using System.Collections.Generic;

namespace RimArt
{
    /// <summary>What the game knows about a pawn target, reduced to the questions the rules ask.</summary>
    public struct HookPawnFacts
    {
        public bool IsCaster;
        public bool Spawned;
        public bool Dead;
        public bool Downed;
        public bool Humanlike;
        public bool Prisoner;
        public bool ColonyMember;
        public bool AlliedFaction;
        public bool AlreadyRetrieved;
        public bool Reservable;
    }

    public enum HookItemKind { Other, Weapon, Apparel, Medicine, Food, Resource, Minified }

    /// <summary>What the game knows about an item target.</summary>
    public struct HookItemFacts
    {
        public bool Spawned;
        public bool IsItem;
        public bool Haulable;
        public bool Corpse;
        public bool Chunk;
        public HookItemKind Kind;
        public int StackCount;
        public float MassPerUnit;
        public bool AlreadyRetrieved;
        public bool Reservable;
    }

    /// <summary>What the game knows about one injury, for choosing which one the pull worsens.</summary>
    public struct HookWoundFacts
    {
        public bool Permanent;
        public bool PartPresent;
        public bool External;
        public bool Organic;
        public bool Tended;
        public bool CanBleed;
    }

    /// <summary>
    /// The retrieval hook's rules with no game types in them. The game-facing code in
    /// <see cref="RetrievalTargets"/> and <see cref="RetrievalWounds"/> fills in the facts and
    /// acts on the answers.
    /// </summary>
    public static class RetrievalRules
    {
        /// <summary>Why this pawn cannot be pulled, or null when it can.</summary>
        public static string PawnRefusal(HookPawnFacts f)
        {
            if (f.IsCaster) return "Cannot target yourself.";
            if (!f.Spawned) return "Target is being carried or is inside something.";
            if (f.Dead) return "Target is dead.";
            if (!f.Humanlike) return "Only people can be retrieved, not animals or mechanoids.";
            if (f.Prisoner) return "Prisoners cannot be retrieved.";
            if (!f.ColonyMember && !f.AlliedFaction) return "Only colonists, slaves and allies can be retrieved.";
            if (!f.Downed) return "Only downed people can be retrieved.";
            if (f.AlreadyRetrieved) return "Target is already being retrieved.";
            if (!f.Reservable) return "Someone else is already handling this person.";
            return null;
        }

        /// <summary>Why this item cannot be pulled, or null when it can. <paramref name="take"/> is how many units the pull moves.</summary>
        public static string ItemRefusal(HookItemFacts f, out int take)
        {
            take = 0;
            if (!f.Spawned) return "Target is inside something.";
            if (f.Corpse) return "Corpses cannot be retrieved.";
            if (!f.IsItem) return "Only items and downed people can be retrieved.";
            if (f.Chunk) return "Chunks cannot be retrieved.";
            if (!f.Haulable) return "This cannot be hauled.";
            if (f.Kind == HookItemKind.Other)
                return "Only weapons, apparel, medicine, food, resources and minified buildings can be retrieved.";
            take = ItemTakeCount(f.StackCount, f.MassPerUnit, RetrievalHookDefaults.ItemCapacityKg);
            if (take <= 0)
                return "Too heavy: " + f.MassPerUnit.ToString("0.##") + " kg per unit, limit "
                       + RetrievalHookDefaults.ItemCapacityKg.ToString("0") + " kg.";
            if (f.AlreadyRetrieved) return "Target is already being retrieved.";
            if (!f.Reservable) return "Someone else has reserved this.";
            return null;
        }

        /// <summary>
        /// Units of a stack that fit in the capacity. Zero when a single unit is too heavy. A
        /// massless item comes whole. The small epsilon keeps 40 units at 0.5 kg exactly at 20 kg
        /// from rounding down to 39.
        /// </summary>
        public static int ItemTakeCount(int stackCount, float massPerUnit, float capacityKg)
        {
            if (stackCount <= 0) return 0;
            if (massPerUnit <= 0f) return stackCount;
            double fit = Math.Floor(capacityKg / (double)massPerUnit + 1e-4);
            if (fit <= 0) return 0;
            return fit >= stackCount ? stackCount : (int)fit;
        }

        /// <summary>
        /// The two stacks a split leaves: what stays on the ground and what the net carries.
        /// The sum always equals the original count.
        /// </summary>
        public static void SplitCounts(int stackCount, int take, out int remaining, out int moved)
        {
            moved = Math.Max(0, Math.Min(take, stackCount));
            remaining = stackCount - moved;
        }

        public static bool WoundEligible(HookWoundFacts w)
        {
            return !w.Permanent && w.PartPresent && w.External && w.Organic;
        }

        public static bool WoundPreferred(HookWoundFacts w)
        {
            return WoundEligible(w) && w.Tended && w.CanBleed;
        }

        /// <summary>
        /// Index of the wound the pull worsens, or -1 when none is eligible. Tended wounds that can
        /// bleed are chosen first; otherwise any eligible wound. <paramref name="randomBelow"/>
        /// returns a value in [0, n).
        /// </summary>
        public static int PickWound(IList<HookWoundFacts> wounds, Func<int, int> randomBelow)
        {
            List<int> preferred = new List<int>();
            List<int> eligible = new List<int>();
            for (int i = 0; i < wounds.Count; i++)
            {
                if (!WoundEligible(wounds[i])) continue;
                eligible.Add(i);
                if (WoundPreferred(wounds[i])) preferred.Add(i);
            }

            List<int> pool = preferred.Count > 0 ? preferred : eligible;
            if (pool.Count == 0) return -1;
            int pick = randomBelow(pool.Count);
            if (pick < 0 || pick >= pool.Count) pick = 0;
            return pool[pick];
        }

        /// <summary>
        /// The largest increase, starting at <paramref name="max"/> and reduced one
        /// <paramref name="step"/> at a time, for which <paramref name="safe"/> holds. Zero when no
        /// positive increase is safe. Counted in whole steps so 1.00 - 0.01 * n does not drift.
        /// </summary>
        public static float CappedIncrease(float max, float step, Func<float, bool> safe)
        {
            int steps = (int)Math.Round(max / step);
            for (int n = steps; n > 0; n--)
            {
                float amount = (float)(n * (double)step);
                if (safe(amount)) return amount;
            }
            return 0f;
        }

        /// <summary>Cells on a straight line from a to b, both ends included.</summary>
        public static List<(int x, int z)> Line(int ax, int az, int bx, int bz)
        {
            List<(int x, int z)> cells = new List<(int x, int z)>();
            int dx = Math.Abs(bx - ax), dz = Math.Abs(bz - az);
            int sx = ax < bx ? 1 : -1, sz = az < bz ? 1 : -1;
            int err = dx - dz;
            int x = ax, z = az;
            while (true)
            {
                cells.Add((x, z));
                if (x == bx && z == bz) break;
                int e2 = 2 * err;
                if (e2 > -dz) { err -= dz; x += sx; }
                if (e2 < dx) { err += dx; z += sz; }
            }
            return cells;
        }

        /// <summary>
        /// The drag path: the line from target to wearer with the wearer's own cell removed, so
        /// the target stops beside them. The first entry is the target's cell.
        /// </summary>
        public static List<(int x, int z)> DragPath(int targetX, int targetZ, int casterX, int casterZ)
        {
            List<(int x, int z)> line = Line(targetX, targetZ, casterX, casterZ);
            if (line.Count > 1) line.RemoveAt(line.Count - 1);
            return line;
        }
    }

    public enum DragEvent { None, StepStarted, Arrived, Blocked }

    /// <summary>
    /// Progress of a target along its drag path, one cell at a time.
    ///
    /// A step starts only after its cell is checked, and the target occupies the new cell for the
    /// whole step. <see cref="penaltyApplied"/> is set on the same call that reports the first
    /// step, so a save made at any point cannot apply the wound penalty twice.
    /// </summary>
    public sealed class DragProgress
    {
        /// <summary>Index into the path of the cell the target occupies.</summary>
        public int index;

        /// <summary>Ticks into the current step, or -1 when not between cells.</summary>
        public int stepTicks = -1;

        public bool penaltyApplied;

        public DragEvent Tick(int pathCount, Func<int, bool> cellOpen, out bool firstMove)
        {
            firstMove = false;
            if (stepTicks >= 0)
            {
                stepTicks++;
                if (stepTicks < RetrievalHookDefaults.DragTicksPerCell) return DragEvent.None;
                stepTicks = -1;
            }

            if (index >= pathCount - 1) return DragEvent.Arrived;
            if (!cellOpen(index + 1)) return DragEvent.Blocked;

            index++;
            stepTicks = 0;
            firstMove = !penaltyApplied;
            penaltyApplied = true;
            return DragEvent.StepStarted;
        }

        /// <summary>0 at the previous cell, 1 at the current one.</summary>
        public float StepFraction =>
            stepTicks < 0 ? 1f : stepTicks / (float)RetrievalHookDefaults.DragTicksPerCell;
    }
}
