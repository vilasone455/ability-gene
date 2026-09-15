using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Draws orbs whose outline changes shape. Built on the black hole's drawing approach
    /// (Source/RimArt/Gravity/GravityGraphics.cs): one white pixel as the only texture, tinted
    /// per draw through a MaterialPropertyBlock, and meshes generated in C#. Nothing here reads
    /// a PNG, so a new form costs four numbers rather than a sprite sheet.
    ///
    /// The difference from the black hole is that its meshes are static and radially symmetric.
    /// These are rebuilt while a transformation is running, and are only symmetric when the orb
    /// happens to be a ball.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SixPathsGraphics
    {
        private const int Segments = 64;
        // Rim width in orb radii, so 0.025 cells drawn. A black orb on a dark map has no
        // silhouette without it; this is the one part of the effect that is not optional. It is
        // held narrow because the staff is only 0.20 radii across and a wider rim closes it up.
        private const float RimWidth = 0.06f;

        private static readonly Material solid =
            new Material(ShaderDatabase.Transparent) { mainTexture = BaseContent.WhiteTex };
        private static readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private static readonly Mesh shadow = Disc();
        private static readonly MorphMesh[] pool = NewPool(SixPathsTiming.Orbs + SixPathsShapes.Cycle.Length);

        private static readonly Color Body = new Color(0.020f, 0.015f, 0.032f);
        private static readonly Color Rim = new Color(0.52f, 0.36f, 0.86f);
        private static readonly Color Sheen = new Color(0.72f, 0.62f, 0.95f);

        /// <summary>
        /// One ring of orbs around a point, all holding and changing form together.
        /// <paramref name="seconds"/> drives everything, so a caller that stops advancing it
        /// freezes the whole effect mid-change.
        /// </summary>
        public static void DrawRing(Vector3 centre, float seconds, float fade, Map map)
        {
            OrbShape shape = SixPathsTiming.ShapeAt(seconds);
            float surge = SixPathsTiming.Surge(seconds);
            float altitude = AltitudeLayer.MoteOverhead.AltitudeFor();

            for (int i = 0; i < SixPathsTiming.Orbs; i++)
            {
                float angle = (seconds / SixPathsTiming.OrbitSeconds + i / (float)SixPathsTiming.Orbs) * Mathf.PI * 2f;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle) * SixPathsTiming.OrbitDepth)
                    * SixPathsTiming.OrbitRadius;
                Vector3 position = centre + offset;
                if (!position.ToIntVec3().InBounds(map) || position.ToIntVec3().Fogged(map)) continue;

                // Orbs on the far half of the ring sit smaller and lower in the draw order. The
                // ring is flat in map space, so without this the near and far halves are
                // indistinguishable and the orbit reads as a flat circle rather than a ring.
                float front = 0.5f - 0.5f * Mathf.Sin(angle);
                float depth = Mathf.Lerp(0.84f, 1.0f, front);
                // Euler-y sends local +x to (cos a, 0, -sin a), so the long axis lies along the
                // orbit tangent at this angle.
                float facing = -(angle * Mathf.Rad2Deg + 90f);

                DrawOrb(pool[i], position.WithY(altitude + front * 0.01f), shape,
                    0.42f * depth, facing, fade, surge, true);
            }
        }

        /// <summary>Every form at rest, side by side, for judging the silhouettes on their own.</summary>
        public static void DrawSheet(Vector3 centre, Map map)
        {
            float altitude = AltitudeLayer.MoteOverhead.AltitudeFor();
            for (int i = 0; i < SixPathsShapes.Cycle.Length; i++)
            {
                Vector3 position = centre + new Vector3((i - 1.5f) * 2.4f, 0f, 0f);
                if (!position.ToIntVec3().InBounds(map) || position.ToIntVec3().Fogged(map)) continue;
                DrawOrb(pool[SixPathsTiming.Orbs + i], position.WithY(altitude),
                    SixPathsShapes.Cycle[i], 0.42f, 0f, 1f, 0f, false);
            }
        }

        private static void DrawOrb(MorphMesh mesh, Vector3 position, in OrbShape shape, float size,
            float facing, float fade, float surge, bool grounded)
        {
            mesh.Rebuild(shape);
            // A slight swell at mid-change. The orb is black and its interior never moves, so
            // without this the outline is the only thing announcing the transformation.
            float scale = size * (1f + 0.12f * surge);

            if (grounded)
                DrawMesh(shadow, position.WithY(AltitudeLayer.MoteLow.AltitudeFor()),
                    scale * 2.6f, scale * 1.5f, 0f, new Color(0.02f, 0.01f, 0.05f, 0.26f * fade));

            // Rim first, body over it: the body hides the inner edge of the band, so the lit
            // line always sits outside the silhouette rather than cutting into it.
            Color rim = Rim;
            rim.a = fade * (0.85f + 0.15f * surge);
            DrawMesh(mesh.rim, position, scale, scale, facing,
                Color.Lerp(rim, new Color(0.92f, 0.86f, 1f, rim.a), surge * 0.7f));
            DrawMesh(mesh.body, position.WithY(position.y + 0.004f), scale, scale, facing,
                new Color(Body.r, Body.g, Body.b, fade));
            // One fixed highlight on the upper left, the only cue that the shape is a solid.
            Vector3 sheen = position.WithY(position.y + 0.008f) + new Vector3(-0.22f * scale, 0f, 0.26f * scale);
            DrawMesh(shadow, sheen, scale * 0.34f, scale * 0.26f, 0f,
                new Color(Sheen.r, Sheen.g, Sheen.b, fade * 0.18f));
        }

        private static void DrawMesh(Mesh mesh, Vector3 position, float width, float depth,
            float rotation, Color colour)
        {
            properties.SetColor(ShaderPropertyIDs.Color, colour);
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(position, Quaternion.Euler(0f, rotation, 0f),
                new Vector3(width, 1f, depth)), solid, 0, null, 0, properties);
        }

        private static MorphMesh[] NewPool(int count)
        {
            var meshes = new MorphMesh[count];
            for (int i = 0; i < count; i++) meshes[i] = new MorphMesh(i);
            return meshes;
        }

        /// <summary>A flat unit ellipse, for the ground shadow and the highlight.</summary>
        private static Mesh Disc()
        {
            var vertices = new Vector3[Segments + 1];
            var indices = new int[Segments * 3];
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                indices[i * 3] = 0;
                indices[i * 3 + 1] = (i + 1) % Segments + 1;
                indices[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = "Six Paths disc", vertices = vertices, triangles = indices };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// One orb's body and rim. Topology is fixed at construction and only vertex positions
        /// move, so a transformation writes two float arrays and never allocates a mesh.
        /// </summary>
        private sealed class MorphMesh
        {
            public readonly Mesh body, rim;
            private readonly Vector3[] bodyVertices = new Vector3[Segments + 1];
            private readonly Vector3[] rimVertices = new Vector3[(Segments + 1) * 2];
            private readonly Vector2[] outline = new Vector2[Segments];
            private OrbShape last;
            private bool built;

            public MorphMesh(int index)
            {
                var bodyIndices = new int[Segments * 3];
                for (int i = 0; i < Segments; i++)
                {
                    bodyIndices[i * 3] = 0;
                    bodyIndices[i * 3 + 1] = (i + 1) % Segments + 1;
                    bodyIndices[i * 3 + 2] = i + 1;
                }
                var rimIndices = new int[Segments * 6];
                for (int i = 0; i < Segments; i++)
                {
                    int v = i * 2, j = i * 6;
                    rimIndices[j] = v; rimIndices[j + 1] = v + 2; rimIndices[j + 2] = v + 1;
                    rimIndices[j + 3] = v + 1; rimIndices[j + 4] = v + 2; rimIndices[j + 5] = v + 3;
                }
                body = new Mesh { name = "Six Paths orb " + index };
                rim = new Mesh { name = "Six Paths rim " + index };
                Rebuild(SixPathsShapes.Orb);
                body.triangles = bodyIndices;
                rim.triangles = rimIndices;
            }

            public void Rebuild(in OrbShape shape)
            {
                // Held forms are by far the common case: the ring spends 1.40 s on each form and
                // 0.26 s changing, so most frames reuse the vertices already uploaded.
                if (built && Same(shape, last)) return;
                last = shape;
                built = true;

                for (int i = 0; i < Segments; i++)
                    outline[i] = SixPathsShapes.Outline(shape, i / (float)Segments);

                bodyVertices[0] = Vector3.zero;
                for (int i = 0; i < Segments; i++)
                {
                    Vector2 point = outline[i];
                    bodyVertices[i + 1] = new Vector3(point.x, 0f, point.y);

                    // Constant-width offset along the outward normal, not a scale of the whole
                    // shape: scaling a 2.35 x 0.17 bar would give it a rim 14 times thicker along
                    // its length than across it.
                    Vector2 tangent = outline[(i + 1) % Segments] - outline[(i + Segments - 1) % Segments];
                    Vector2 normal = new Vector2(tangent.y, -tangent.x);
                    float length = normal.magnitude;
                    normal = length > 0.0001f ? normal / length : point.normalized;
                    Vector2 outer = point + normal * RimWidth;
                    rimVertices[i * 2] = new Vector3(point.x, 0f, point.y);
                    rimVertices[i * 2 + 1] = new Vector3(outer.x, 0f, outer.y);
                }
                rimVertices[Segments * 2] = rimVertices[0];
                rimVertices[Segments * 2 + 1] = rimVertices[1];

                body.vertices = bodyVertices;
                rim.vertices = rimVertices;
                body.RecalculateBounds();
                rim.RecalculateBounds();
            }

            private static bool Same(in OrbShape a, in OrbShape b) =>
                a.halfX == b.halfX && a.halfY == b.halfY && a.corner == b.corner && a.taper == b.taper;
        }
    }
}
