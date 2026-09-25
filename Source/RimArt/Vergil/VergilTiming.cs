using System.Collections.Generic;
using UnityEngine;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>One convex piece a disc broke into: its points are relative to its own centre, counter-clockwise.</summary>
    public struct VergilPiece
    {
        public Vector2 Centre;
        public float Area;
        public Vector2[] Points;
    }

    /// <summary>A straight line across a disc: a point on it, its direction, and its two ends.</summary>
    public struct VergilChord
    {
        public Vector2 Q, D, A, B;
    }

    /// <summary>
    /// The numbers and pure geometry shared by the Vergil effects: where a cut crosses the ball, and
    /// how a disc breaks along its last few cuts. Seconds and points in, geometry out, no drawing and
    /// no map. The port of the non-drawing half of Tools/VfxLab/web/sketches/lib/vergil.js; its
    /// numbers are that file's.
    /// </summary>
    public static class VergilTiming
    {
        /// <summary>A piece smaller than this is dropped, as the sketch drops it.</summary>
        private const float SmallestPiece = 0.02f;

        public static float Clamp(float t) => Mathf.Clamp01(t);

        /// <summary>
        /// The two ends of the chord a line through <paramref name="q"/> along <paramref name="d"/>
        /// makes across a disc of <paramref name="radius"/> centred on the origin.
        /// </summary>
        public static void Chord(Vector2 q, Vector2 d, float radius, out Vector2 a, out Vector2 b)
        {
            float bHalf = q.x * d.x + q.y * d.y, c = q.x * q.x + q.y * q.y - radius * radius;
            float root = Mathf.Sqrt(Mathf.Max(0f, bHalf * bHalf - c));
            a = q + d * (-bHalf - root);
            b = q + d * (-bHalf + root);
        }

        /// <summary>
        /// The convex pieces left when a disc of <paramref name="radius"/> is cut by the straight
        /// <paramref name="lines"/>, each a point and a unit direction relative to the centre.
        /// </summary>
        public static List<VergilPiece> Shatter(float radius, IList<VergilChord> lines, int sides = 40)
        {
            var pieces = new List<List<Vector2>> { new List<Vector2>(sides) };
            for (int i = 0; i < sides; i++)
            {
                float turn = i / (float)sides * Mathf.PI * 2f;
                pieces[0].Add(new Vector2(Mathf.Cos(turn) * radius, Mathf.Sin(turn) * radius));
            }

            foreach (VergilChord line in lines)
            {
                var next = new List<List<Vector2>>();
                foreach (List<Vector2> poly in pieces)
                {
                    int n = poly.Count;
                    var side = new float[n];
                    for (int i = 0; i < n; i++) side[i] = (poly[i].x - line.Q.x) * -line.D.y + (poly[i].y - line.Q.y) * line.D.x;
                    var left = new List<Vector2>();
                    var right = new List<Vector2>();
                    for (int i = 0; i < n; i++)
                    {
                        Vector2 v = poly[i], w = poly[(i + 1) % n];
                        float sv = side[i], sw = side[(i + 1) % n];
                        (sv >= 0f ? left : right).Add(v);
                        if ((sv >= 0f) != (sw >= 0f))
                        {
                            Vector2 mid = v + (w - v) * (sv / (sv - sw));
                            left.Add(mid);
                            right.Add(mid);
                        }
                    }
                    if (left.Count >= 3) next.Add(left);
                    if (right.Count >= 3) next.Add(right);
                }
                pieces = next;
            }

            var made = new List<VergilPiece>(pieces.Count);
            foreach (List<Vector2> poly in pieces)
            {
                float area = 0f, cx = 0f, cz = 0f;
                int n = poly.Count;
                for (int i = 0; i < n; i++)
                {
                    Vector2 v = poly[i], w = poly[(i + 1) % n];
                    float k = v.x * w.y - w.x * v.y;
                    area += k;
                    cx += (v.x + w.x) * k;
                    cz += (v.y + w.y) * k;
                }
                area /= 2f;
                Vector2 centre = Mathf.Abs(area) < 1e-6f ? poly[0] : new Vector2(cx / (6f * area), cz / (6f * area));
                if (Mathf.Abs(area) <= SmallestPiece) continue;
                var points = new Vector2[n];
                for (int i = 0; i < n; i++) points[i] = poly[i] - centre;
                made.Add(new VergilPiece { Centre = centre, Area = Mathf.Abs(area), Points = points });
            }
            return made;
        }
    }
}
