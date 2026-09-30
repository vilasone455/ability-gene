using System.Collections.Generic;
using UnityEngine;

namespace RimArt
{
    /// <summary>One line of the circuit: its corners, when it starts (cells of travel after the chest lights) and its length.</summary>
    public sealed class TraceCircuitRun
    {
        public readonly float From, Length;
        public readonly Vector2[] Points;

        public TraceCircuitRun(float from, params Vector2[] points)
        {
            From = from;
            Points = points;
            for (int i = 1; i < points.Length; i++) Length += (points[i] - points[i - 1]).magnitude;
        }
    }

    /// <summary>
    /// The magic circuit on Shirou's body (lib/trace.js Circuit): drawn while Reinforcement is cast and when it
    /// ends, and its run 0 is Trace On's arm line. Square traces along the edges of the body, in cells from the
    /// point a pawn is drawn at (x toward the weapon hand, y up the screen), laid out on the lab's stand-in. They
    /// are uneven from side to side and have no line down the middle: a spine with limbs off it read as a skeleton.
    /// Run 0 ends at the weapon hand. Each run starts <see cref="TraceCircuitRun.From"/> cells of travel after the
    /// chest lights, so a branch lights when the line reaches it.
    ///
    /// In game the body faces a way (the stand-in does not): east and west draw the table <see cref="SideWidth"/>
    /// as wide, because a body seen from the side is narrower than from the front, and run 0 is made again by
    /// <see cref="ArmTo"/> to end at the real weapon's grip.
    /// </summary>
    public static class TraceCircuit
    {
        public static readonly TraceCircuitRun[] Runs =
        {
            new TraceCircuitRun(0f, V(0f, 0.34f), V(0.1f, 0.34f), V(0.1f, 0.41f), V(0.18f, 0.41f), V(0.18f, 0.28f), V(0.24f, 0.2f)),
            new TraceCircuitRun(0.38f, V(0.18f, 0.28f), V(0.18f, 0.1f), V(0.12f, 0.1f), V(0.12f, -0.1f)),
            new TraceCircuitRun(0f, V(0f, 0.34f), V(-0.1f, 0.34f), V(-0.1f, 0.22f), V(-0.19f, 0.22f), V(-0.19f, 0.04f), V(-0.1f, 0.04f), V(-0.1f, -0.1f)),
            new TraceCircuitRun(0.31f, V(0.18f, 0.35f), V(0.23f, 0.35f)),
            new TraceCircuitRun(0.4f, V(-0.19f, 0.13f), V(-0.14f, 0.13f)),
            new TraceCircuitRun(0.72f, V(0.12f, 0f), V(0.07f, 0f)),
        };

        /// <summary>Travel that lights all of it.</summary>
        public static readonly float Travel;

        /// <summary>How wide the table is drawn on a body seen from the side (facing east or west), in game.</summary>
        public const float SideWidth = 0.7f;

        /// <summary>The weapon hand of the table's run 0, where Trace On's flash shows in the lab: at the stand-in's hand height 0.3.</summary>
        public static readonly Vector2 Hand = V(0.24f, 0.2f);

        static TraceCircuit()
        {
            foreach (TraceCircuitRun run in Runs) Travel = Mathf.Max(Travel, run.From + run.Length);
        }

        private static Vector2 V(float x, float y) => new Vector2(x, y);

        /// <summary>The first <paramref name="length"/> cells of a line of corners.</summary>
        public static List<Vector2> Partial(IList<Vector2> pts, float length)
        {
            var result = new List<Vector2>(pts.Count) { pts[0] };
            float left = length;
            for (int i = 1; i < pts.Count && left > 0f; i++)
            {
                Vector2 a = pts[i - 1], b = pts[i];
                float d = (b - a).magnitude;
                if (d <= left)
                {
                    result.Add(b);
                    left -= d;
                    continue;
                }
                result.Add(a + (b - a) * (left / d));
                left = 0f;
            }
            return result;
        }

        /// <summary>
        /// Run 0 carried on to <paramref name="hand"/> (table coordinates): the table's line, then straight to the
        /// real weapon's grip, so the branches that leave run 0 stay on it. A hand within 0.02 of the table's adds nothing.
        /// </summary>
        public static TraceCircuitRun ArmTo(Vector2 hand)
        {
            Vector2[] table = Runs[0].Points;
            if ((hand - table[table.Length - 1]).magnitude < 0.02f) return Runs[0];
            var points = new Vector2[table.Length + 1];
            table.CopyTo(points, 0);
            points[table.Length] = hand;
            return new TraceCircuitRun(0f, points);
        }
    }
}
