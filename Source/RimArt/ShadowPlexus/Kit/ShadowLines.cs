using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>Why a shadow line cannot lie where it does, or None.</summary>
    public enum LineBreak
    {
        None,
        /// <summary>A pawn stands on it.</summary>
        Crossed,
        /// <summary>Smoke covers a cell of it.</summary>
        Smoke,
        /// <summary>A cell under it is dark.</summary>
        Dark,
        /// <summary>A wall or a closed door is in the way.</summary>
        Wall,
    }

    /// <summary>
    /// The rule every shadow line follows. A line is the straight run of cells from one end to the
    /// other. It breaks when a pawn stands on a cell of it (not the two ends, and not the pawns the
    /// caller names), when a cell under it is dark, when smoke covers one, and it cannot cross a wall
    /// or a closed door. The light and smoke checks are options because the Double's tie line may
    /// cross dark cells.
    /// </summary>
    public static class ShadowLines
    {
        /// <summary>
        /// Checks the line from <paramref name="a"/> to <paramref name="b"/>. <paramref name="at"/> is
        /// the first cell that fails and <paramref name="by"/> the pawn standing on it, if that is why.
        /// </summary>
        public static LineBreak Check(Map map, IntVec3 a, IntVec3 b, Thing ignore1, Thing ignore2, bool light, bool smoke, out IntVec3 at, out Pawn by)
        {
            at = a;
            by = null;
            if (map == null) return LineBreak.None;
            float minSize = ShadowPlexusExtension.Get.minCrossingBodySize;
            foreach (IntVec3 c in GenSight.PointsOnLineOfSight(a, b))
            {
                LineBreak result = CheckCell(map, c, c == a || c == b, ignore1, ignore2, light, smoke, minSize, out by);
                if (result != LineBreak.None) { at = c; return result; }
            }
            LineBreak end = CheckCell(map, b, true, ignore1, ignore2, light, smoke, minSize, out by);
            if (end != LineBreak.None) at = b;
            return end;
        }

        private static LineBreak CheckCell(Map map, IntVec3 c, bool end, Thing ignore1, Thing ignore2, bool light, bool smoke, float minSize, out Pawn by)
        {
            by = null;
            if (!c.InBounds(map)) return LineBreak.Wall;
            if (light && ShadowLight.Dark(map, c)) return LineBreak.Dark;
            if (smoke && ShadowLight.Smoky(map, c)) return LineBreak.Smoke;
            if (end) return LineBreak.None;
            if (!c.Walkable(map) || (c.GetDoor(map) is Building_Door door && !door.Open)) return LineBreak.Wall;
            System.Collections.Generic.List<Thing> things = c.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                if (!(things[i] is Pawn pawn) || pawn == ignore1 || pawn == ignore2 || pawn.Dead || pawn.Downed) continue;
                if (pawn.BodySize < minSize) continue;
                by = pawn;
                return LineBreak.Crossed;
            }
            return LineBreak.None;
        }

        /// <summary>Where along the line from <paramref name="a"/> to <paramref name="b"/> the cell <paramref name="at"/> is, 0 to 1.</summary>
        public static float Share(IntVec3 a, IntVec3 b, IntVec3 at)
        {
            float length = a.DistanceTo(b);
            return length < 0.01f ? 0.5f : Mathf.Clamp01(a.DistanceTo(at) / length);
        }

        /// <summary>The first cell of the line from <paramref name="from"/> toward <paramref name="to"/> after <paramref name="from"/> itself.</summary>
        public static IntVec3 NextToward(IntVec3 from, IntVec3 to)
        {
            if (from == to) return from;
            foreach (IntVec3 c in GenSight.PointsOnLineOfSight(from, to))
                if (c != from) return c;
            return to;
        }

        /// <summary>One word for a message: what broke the line.</summary>
        public static string Word(LineBreak result, Pawn by)
        {
            switch (result)
            {
                case LineBreak.Crossed: return "AG_ShadowBreakCrossed".Translate(by?.LabelShort ?? "someone");
                case LineBreak.Smoke: return "AG_ShadowBreakSmoke".Translate();
                case LineBreak.Dark: return "AG_ShadowBreakDark".Translate();
                case LineBreak.Wall: return "AG_ShadowBreakWall".Translate();
                default: return "";
            }
        }
    }
}
