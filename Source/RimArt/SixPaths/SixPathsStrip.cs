using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// A ribbon between two lines of points. Topology is fixed at construction and only vertex
    /// positions move. Every triangle has its own three vertices and is written clockwise on
    /// screen whichever way the ribbon runs: a petal on the west of the flower is the mirror of
    /// one on the east, and a bud's petal curls back over itself, so no one winding for the
    /// whole strip would face the camera everywhere.
    /// </summary>
    internal sealed class SixPathsStrip
    {
        public readonly Mesh mesh;
        private readonly Vector3[] vertices;
        private readonly Vector2[] sideA, sideB;

        public SixPathsStrip(string name, int points)
        {
            vertices = new Vector3[(points - 1) * 6];
            sideA = new Vector2[points];
            sideB = new Vector2[points];
            var indices = new int[vertices.Length];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            mesh = new Mesh { name = name, vertices = vertices };
            mesh.triangles = indices;
        }

        public void Between(Vector2[] a, Vector2[] b)
        {
            for (int i = 0; i + 1 < a.Length; i++)
            {
                Triangle(i * 6, a[i], a[i + 1], b[i]);
                Triangle(i * 6 + 3, b[i], a[i + 1], b[i + 1]);
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        /// <summary>A line through <paramref name="points"/>, <paramref name="width"/> across at its middle and nothing at either end.</summary>
        public void Line(Vector2[] points, float width)
        {
            int last = points.Length - 1;
            for (int i = 0; i <= last; i++)
            {
                Vector2 along = points[Mathf.Min(last, i + 1)] - points[Mathf.Max(0, i - 1)];
                float length = along.magnitude;
                Vector2 side = new Vector2(-along.y, along.x)
                    * (Mathf.Sin(i / (float)last * Mathf.PI) * width * 0.5f / (length > 1e-5f ? length : 1f));
                sideA[i] = points[i] + side;
                sideB[i] = points[i] - side;
            }
            Between(sideA, sideB);
        }

        private void Triangle(int v, Vector2 p, Vector2 q, Vector2 r)
        {
            // Positive is counter-clockwise with north up, which is the back side.
            bool back = (q.x - p.x) * (r.y - p.y) - (q.y - p.y) * (r.x - p.x) > 0f;
            vertices[v] = new Vector3(p.x, 0f, p.y);
            vertices[v + 1] = back ? new Vector3(r.x, 0f, r.y) : new Vector3(q.x, 0f, q.y);
            vertices[v + 2] = back ? new Vector3(q.x, 0f, q.y) : new Vector3(r.x, 0f, r.y);
        }
    }
}
