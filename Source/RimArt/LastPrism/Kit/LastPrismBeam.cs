using System;
using UnityEngine;
using Verse;
using T = RimArt.LastPrismTiming;

namespace RimArt
{
    /// <summary>
    /// The beams on a real map, for the rules and the picture alike: where a beam stops, and the six fan beams of one
    /// tick. Ground points are (x, z) in map units, a cell spanning x..x+1; angles are radians, 0 east, pi/2 north.
    /// </summary>
    public static class LastPrismBeam
    {
        /// <summary>
        /// Cells a beam from <paramref name="from"/> along <paramref name="radians"/> runs before the first cell that blocks
        /// sight (a wall, a closed door, rock) or the map edge, at most <paramref name="max"/>. Walks the cells the line
        /// crosses (Amanatides and Woo), so a beam never skips a wall corner. A beam whose start is inside a wall (the
        /// prism's tip pushed into the wall in front) runs 0.
        /// </summary>
        public static float Reach(Map map, Vector2 from, float radians, float max)
        {
            if (map == null) return max;
            float dx = Mathf.Cos(radians), dz = Mathf.Sin(radians);
            int x = Mathf.FloorToInt(from.x), z = Mathf.FloorToInt(from.y);
            int stepX = dx > 0f ? 1 : -1, stepZ = dz > 0f ? 1 : -1;
            float ax = Mathf.Abs(dx), az = Mathf.Abs(dz);
            float nextX = ax < 1e-6f ? float.PositiveInfinity : (dx > 0f ? x + 1 - from.x : from.x - x) / ax;
            float nextZ = az < 1e-6f ? float.PositiveInfinity : (dz > 0f ? z + 1 - from.y : from.y - z) / az;
            float deltaX = ax < 1e-6f ? float.PositiveInfinity : 1f / ax, deltaZ = az < 1e-6f ? float.PositiveInfinity : 1f / az;
            float t = 0f;
            while (t < max)
            {
                var c = new IntVec3(x, 0, z);
                if (!c.InBounds(map) || !c.CanBeSeenOver(map)) return t;
                if (nextX < nextZ)
                {
                    t = nextX;
                    nextX += deltaX;
                    x += stepX;
                }
                else
                {
                    t = nextZ;
                    nextZ += deltaZ;
                    z += stepZ;
                }
            }
            return max;
        }

        /// <summary>The prism's tip on the ground, where every beam starts.</summary>
        public static Vector2 Tip(Vector2 wielder, float aim) => T.Tip(wielder, new Vector2(Mathf.Cos(aim), Mathf.Sin(aim)));

        public static Vector2 Ground(Vector3 v) => new Vector2(v.x, v.z);

        /// <summary>The six fan beams at one moment: each beam's start, angle and length (to a wall or the range).</summary>
        public sealed class Fan
        {
            public readonly Vector2[] start = new Vector2[T.Beams];
            public readonly double[] angle = new double[T.Beams];
            public readonly double[] length = new double[T.Beams];

            /// <summary>
            /// The fan at <paramref name="s"/> s on the cast's clock, the channel having started at <paramref name="channelAt"/>:
            /// the picture's own beams (<see cref="LastPrismTiming.FanBeam"/>), stopped by the map's walls.
            /// </summary>
            public void Set(Map map, Vector2 tip, float aim, double s, double channelAt, CompProperties_LastPrismFire props)
            {
                var side = new Vector2(-Mathf.Sin(aim), Mathf.Cos(aim));
                for (int i = 0; i < T.Beams; i++)
                {
                    T.FanBeam(i, s, channelAt, props.joinSeconds, props.fanDegrees, out double turn, out double shift);
                    start[i] = tip + side * (float)shift;
                    angle[i] = aim + turn;
                    length[i] = Reach(map, start[i], (float)angle[i], props.Range);
                }
            }

            /// <summary>Some fan beam passes within <paramref name="reach"/> of the ground point <paramref name="at"/>.</summary>
            public bool Crosses(Vector2 at, double reach)
            {
                for (int i = 0; i < T.Beams; i++)
                    if (T.OnLine(at.x, at.y, start[i].x, start[i].y, angle[i], length[i], reach)) return true;
                return false;
            }
        }

        /// <summary>The ground point <paramref name="target"/> stands for: a pawn's DrawPos, or a cell's centre.</summary>
        public static Vector2 Point(LocalTargetInfo target) =>
            target.HasThing && target.Thing.Spawned ? Ground(target.Thing.DrawPos) : new Vector2(target.Cell.x + 0.5f, target.Cell.z + 0.5f);

        /// <summary>The angle from <paramref name="a"/> to <paramref name="b"/>, or <paramref name="fallback"/> when they meet.</summary>
        public static float Toward(Vector2 a, Vector2 b, float fallback)
        {
            Vector2 d = b - a;
            return d.sqrMagnitude < 1e-6f ? fallback : Mathf.Atan2(d.y, d.x);
        }

        /// <summary>An angle in radians turned toward <paramref name="goal"/> by at most <paramref name="step"/>.</summary>
        public static float TurnToward(float aim, float goal, float step) =>
            aim + Mathf.Clamp((float)T.Wrap(goal - aim), -step, step);

        /// <summary>The cell facing for an angle in radians (Core's AngleFlat counts from north, clockwise).</summary>
        public static Rot4 Facing(float aim) => Rot4.FromAngleFlat(90f - aim * Mathf.Rad2Deg);

        /// <summary>Core's AngleFlat of a beam travelling along <paramref name="aim"/>: the damage's direction.</summary>
        public static float AngleFlat(float aim) => (float)(((90.0 - aim * (180.0 / Math.PI)) % 360.0 + 360.0) % 360.0);
    }
}
