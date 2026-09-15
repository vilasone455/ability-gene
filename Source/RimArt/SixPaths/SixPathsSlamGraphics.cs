using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Draws the slam: the gather, the block, and the floor it lands on. Same drawing approach as
    /// the rest of this branch -- one white pixel as the only texture, tinted per draw, and every
    /// mesh generated in C# (SixPathsGraphics.cs:9, GravityGraphics.cs:9). No PNG is read.
    ///
    /// The block is the one thing here that is not a flat sprite. Three of its six faces point at
    /// the camera at any moment; each is drawn as its own lit plane, and the twelve edges are
    /// drawn as thin quads over the top. The faces give it planes and the edges give it corners,
    /// and between them a black box on dark terrain is a box rather than a hole in the map.
    ///
    /// It stands, and it never rotates: what falls is what formed, and it is still standing when
    /// it lands. See SixPathsSlab.cs for why a block that tips flat stops being a solid.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SixPathsSlamGraphics
    {
        private static readonly Material solid =
            new Material(ShaderDatabase.Transparent) { mainTexture = BaseContent.WhiteTex };
        private static readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private static readonly Mesh disc = Disc(), ring = Band(0.90f);
        // One block per copy drawn in a frame, never one reused. Graphics.DrawMesh reads a mesh
        // when the frame renders rather than when it is called, so the block and its two motion
        // ghosts writing into one set of vertices would all come out at the last pose written.
        private static readonly Block[] blocks = { new Block(), new Block(), new Block() };

        // The orb's own colours, because the block is the orbs: same near-black body, same violet
        // rim. Faces read as planes by their value alone, so the lit face is only a sixth as
        // bright as the rim rather than a different colour.
        private static readonly Color Body = new Color(0.020f, 0.015f, 0.032f);
        private static readonly Color Plane = new Color(0.160f, 0.130f, 0.260f);
        private static readonly Color Rim = new Color(0.52f, 0.36f, 0.86f);
        private static readonly Color Flash = new Color(0.86f, 0.80f, 1f);

        private const int Dust = 26;
        private const float EdgeWidth = 0.075f;

        /// <summary>
        /// The whole sequence, driven by <paramref name="seconds"/> alone: a caller that stops
        /// advancing it freezes the block wherever it is, mid-fall included.
        /// </summary>
        public static void Draw(Vector3 ground, float seconds, Map map)
        {
            if (!ground.ToIntVec3().InBounds(map) || ground.ToIntVec3().Fogged(map)) return;
            DrawGather(ground, seconds, map);
            DrawBlock(ground, seconds);
            DrawFloor(ground, seconds, map);
        }

        private static void DrawGather(Vector3 ground, float seconds, Map map)
        {
            for (int i = 0; i < SixPathsTiming.Orbs; i++)
            {
                GatherStep step = SixPathsSlamTiming.Orb(i, SixPathsTiming.Orbs, seconds);
                if (step.alpha <= 0f) continue;
                Vector3 position = ground + new Vector3(Mathf.Cos(step.angle), 0f,
                    Mathf.Sin(step.angle) * SixPathsTiming.OrbitDepth) * step.radius;
                if (!position.ToIntVec3().InBounds(map)) continue;
                SixPathsGraphics.DrawAloft(i, position, step.height, SixPathsShapes.Orb,
                    step.size, -(step.angle * Mathf.Rad2Deg + 90f), step.alpha);
            }

            float flash = SixPathsSlamTiming.FuseFlash(seconds);
            if (flash <= 0.001f) return;
            // At the fuse the six orbs are already at the apex, so the flash belongs up there
            // with them rather than on the cell below.
            Vector3 sky = SixPathsHeight.Above(ground, SixPathsSlamTiming.Apex)
                .WithY(AltitudeLayer.MoteOverhead.AltitudeFor() + 0.05f);
            DrawMesh(disc, sky, 3.4f * flash, 3.4f * flash, 0f,
                new Color(Flash.r, Flash.g, Flash.b, flash * 0.75f));
        }

        private static void DrawBlock(Vector3 ground, float seconds)
        {
            SlabPose pose = SixPathsSlamTiming.Slab(seconds);
            if (pose.alpha <= 0.001f || pose.scale <= 0.001f) return;
            float altitude = AltitudeLayer.MoteOverhead.AltitudeFor();

            // Motion ghosts, and only while it is falling. Six cells in a fifth of a second is
            // most of half a cell between frames; without a couple of copies left behind the
            // block reads as teleporting rather than dropping.
            if (seconds > SixPathsSlamTiming.FallAt && seconds < SixPathsSlamTiming.LandAt)
                for (int i = 2; i >= 1; i--)
                {
                    SlabPose ghost = SixPathsSlamTiming.Slab(seconds - i * 0.045f);
                    if (ghost.height <= pose.height) continue;
                    DrawSolid(i, ground, ghost, pose.alpha * (0.22f - i * 0.06f),
                        altitude - 0.01f * i, false);
                }

            DrawShadow(ground, pose);
            DrawSolid(0, ground, pose, pose.alpha, altitude, true);
        }

        private static void DrawShadow(Vector3 ground, in SlabPose pose)
        {
            blocks[0].RebuildShadow(pose.yaw, SixPathsSlamTiming.DrawScale(pose));
            // Measured from the underside, not the centre: the block is six cells tall, so its
            // centre is three cells up even when it is standing on the floor.
            float clearance = SixPathsSlamTiming.Clearance(pose);
            float size = SixPathsHeight.ShadowSize(clearance);
            DrawMesh(blocks[0].shadow, ground.WithY(AltitudeLayer.MoteLow.AltitudeFor()), size, size, 0f,
                new Color(0.02f, 0.01f, 0.05f, SixPathsHeight.ShadowAlpha(clearance) * pose.alpha));
        }

        private static void DrawSolid(int slot, Vector3 ground, in SlabPose pose, float alpha,
            float altitude, bool edges)
        {
            Block block = blocks[slot];
            block.Rebuild(pose.yaw, pose.height, SixPathsSlamTiming.DrawScale(pose));

            Vector3 at = ground.WithY(altitude);
            for (int face = 0; face < SixPathsSlab.FaceCount; face++)
            {
                if (!block.visible[face]) continue;
                // Squared, so the planes separate at the dark end where all of this lives: the
                // difference between a face at 0.3 and one at 0.7 has to survive being drawn in
                // near-black on dark terrain.
                float light = SixPathsSlab.Lambert(face, pose.yaw);
                Color colour = Color.Lerp(Body, Plane, light * light);
                colour.a = alpha;
                DrawMesh(block.faces[face], at, 1f, 1f, 0f, colour);
            }
            if (!edges) return;
            DrawMesh(block.edges, at.WithY(altitude + 0.004f), 1f, 1f, 0f,
                new Color(Rim.r, Rim.g, Rim.b, alpha * 0.9f));
        }

        private static void DrawFloor(Vector3 ground, float seconds, Map map)
        {
            float altitude = AltitudeLayer.MoteOverhead.AltitudeFor();

            float flash = SixPathsSlamTiming.ImpactFlash(seconds);
            if (flash > 0.001f)
            {
                float size = Mathf.Lerp(3f, 7f, 1f - flash);
                DrawMesh(disc, ground.WithY(altitude - 0.02f), size, size, 0f,
                    new Color(Flash.r, Flash.g, Flash.b, flash * 0.55f));
            }

            float ringAlpha = SixPathsSlamTiming.RingAlpha(seconds);
            if (ringAlpha > 0.001f)
            {
                float radius = SixPathsSlamTiming.RingRadius(seconds);
                DrawMesh(ring, ground.WithY(altitude + 0.03f), radius, radius, 0f,
                    new Color(Rim.r, Rim.g, Rim.b, ringAlpha));
            }

            float dust = SixPathsSlamTiming.DustAlpha(seconds);
            if (dust <= 0.001f) return;
            // Deterministic, like the well's debris: visual dust must not consume gameplay RNG.
            for (int i = 0; i < Dust; i++)
            {
                float angle = i * 2.39996f;
                float radius = SixPathsSlamTiming.DustRadius(i, seconds);
                Vector3 position = ground + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle) * 0.7f) * radius;
                if (!position.ToIntVec3().InBounds(map) || position.ToIntVec3().Fogged(map)) continue;
                DrawMesh(MeshPool.plane10, position.WithY(altitude + 0.02f),
                    0.06f + i % 3 * 0.02f, 0.045f, angle * Mathf.Rad2Deg,
                    new Color(0.60f, 0.57f, 0.66f, dust * (0.5f + i % 4 * 0.1f)));
            }
        }

        private static void DrawMesh(Mesh mesh, Vector3 position, float width, float depth,
            float rotation, Color colour)
        {
            properties.SetColor(ShaderPropertyIDs.Color, colour);
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(position, Quaternion.Euler(0f, rotation, 0f),
                new Vector3(width, 1f, depth)), solid, 0, null, 0, properties);
        }

        private static Mesh Disc()
        {
            const int segments = 64;
            var vertices = new Vector3[segments + 1];
            var indices = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                indices[i * 3] = 0;
                indices[i * 3 + 1] = (i + 1) % segments + 1;
                indices[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = "Six Paths slam disc", vertices = vertices, triangles = indices };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A unit-radius band, for the shock ring on the floor.</summary>
        private static Mesh Band(float inner)
        {
            const int segments = 64;
            var vertices = new Vector3[(segments + 1) * 2];
            var indices = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = direction * inner;
                vertices[i * 2 + 1] = direction;
                if (i == segments) continue;
                int v = i * 2, j = i * 6;
                indices[j] = v; indices[j + 1] = v + 2; indices[j + 2] = v + 1;
                indices[j + 3] = v + 1; indices[j + 4] = v + 2; indices[j + 5] = v + 3;
            }
            var mesh = new Mesh { name = "Six Paths slam ring", vertices = vertices, triangles = indices };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// One block's six faces, its twelve edges and its shadow. Topology is fixed at
        /// construction and only vertex positions move, so a frame of the fall writes eight
        /// arrays and allocates nothing.
        ///
        /// Faces are separate meshes rather than one because each is drawn in its own colour and
        /// the shader takes colour per draw. There are never more than three of them: a box is
        /// convex, so the faces turned toward the camera cannot overlap each other and need
        /// neither depth sorting nor separate altitudes.
        /// </summary>
        private sealed class Block
        {
            public readonly Mesh[] faces = new Mesh[SixPathsSlab.FaceCount];
            public readonly Mesh edges, shadow;
            public readonly bool[] visible = new bool[SixPathsSlab.FaceCount];

            private readonly Vector2[] corners = new Vector2[SixPathsSlab.CornerCount];
            private readonly Vector2[] ground = new Vector2[SixPathsSlab.CornerCount];
            private readonly Vector3[][] faceVertices = new Vector3[SixPathsSlab.FaceCount][];
            private readonly Vector3[] edgeVertices = new Vector3[SixPathsSlab.EdgeCount * 4];
            private readonly Vector3[] shadowVertices = new Vector3[4];

            public Block()
            {
                // Two triangles per quad, wound the way the disc above is wound: a face that
                // survives the visibility test is clockwise on screen, which is the front side.
                var quad = new[] { 0, 1, 2, 0, 2, 3 };
                for (int face = 0; face < SixPathsSlab.FaceCount; face++)
                {
                    faceVertices[face] = new Vector3[4];
                    faces[face] = new Mesh { name = "Six Paths block face " + face, vertices = faceVertices[face] };
                    faces[face].triangles = quad;
                }

                var edgeIndices = new int[SixPathsSlab.EdgeCount * 6];
                for (int edge = 0; edge < SixPathsSlab.EdgeCount; edge++)
                    for (int i = 0; i < 6; i++)
                        edgeIndices[edge * 6 + i] = edge * 4 + quad[i];
                edges = new Mesh { name = "Six Paths block edges", vertices = edgeVertices };
                edges.triangles = edgeIndices;

                // The shadow is the underside seen from above, so its winding is the face's
                // reversed -- the bottom face is by definition turned away from the camera.
                shadow = new Mesh { name = "Six Paths block shadow", vertices = shadowVertices };
                shadow.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            }

            public void Rebuild(float yaw, float height, float scale)
            {
                SixPathsSlab.Project(corners, yaw, height, scale);

                for (int face = 0; face < SixPathsSlab.FaceCount; face++)
                {
                    visible[face] = SixPathsSlab.Visible(corners, face);
                    if (!visible[face]) continue;
                    int[] index = SixPathsSlab.FaceCorners[face];
                    for (int i = 0; i < 4; i++)
                    {
                        Vector2 point = corners[index[i]];
                        faceVertices[face][i] = new Vector3(point.x, 0f, point.y);
                    }
                    faces[face].vertices = faceVertices[face];
                    faces[face].RecalculateBounds();
                }

                for (int edge = 0; edge < SixPathsSlab.EdgeCount; edge++)
                {
                    int v = edge * 4;
                    Vector2 a = corners[SixPathsSlab.EdgeCorners[edge][0]];
                    Vector2 b = corners[SixPathsSlab.EdgeCorners[edge][1]];
                    Vector2 along = b - a;
                    // A hidden edge collapses onto a point instead of being skipped: all twelve
                    // are one mesh and one draw, and a zero-area quad rasterises nothing.
                    if (!SixPathsSlab.EdgeVisible(corners, edge) || along.sqrMagnitude < 1e-8f)
                    {
                        var point = new Vector3(a.x, 0f, a.y);
                        for (int i = 0; i < 4; i++) edgeVertices[v + i] = point;
                        continue;
                    }
                    along /= along.magnitude;
                    var side = new Vector2(-along.y, along.x) * EdgeWidth;
                    edgeVertices[v] = new Vector3(a.x + side.x, 0f, a.y + side.y);
                    edgeVertices[v + 1] = new Vector3(b.x + side.x, 0f, b.y + side.y);
                    edgeVertices[v + 2] = new Vector3(b.x - side.x, 0f, b.y - side.y);
                    edgeVertices[v + 3] = new Vector3(a.x - side.x, 0f, a.y - side.y);
                }
                edges.vertices = edgeVertices;
                edges.RecalculateBounds();
            }

            public void RebuildShadow(float yaw, float scale)
            {
                SixPathsSlab.Footprint(ground, yaw, scale);
                int[] index = SixPathsSlab.FaceCorners[3];
                for (int i = 0; i < 4; i++)
                {
                    Vector2 point = ground[index[i]];
                    shadowVertices[i] = new Vector3(point.x, 0f, point.y);
                }
                shadow.vertices = shadowVertices;
                shadow.RecalculateBounds();
            }
        }
    }
}
