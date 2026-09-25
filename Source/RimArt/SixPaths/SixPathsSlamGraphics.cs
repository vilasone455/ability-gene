using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Draws the slam: the gather, the block, its shadows, and what the landing does to the floor.
    /// Meshes are generated in C# as elsewhere in this kit (SixPathsGraphics.cs). Two soft
    /// textures are read, Textures/RimArt/SixPaths/SoftDisc.png and Puff.png, made by
    /// make_six_paths_textures.py from the VFX lab's formulas.
    ///
    /// The block's three visible faces are flat colours; the solid is carried by a bright
    /// outline on the silhouette, dimmer lines on the folds inside it, and two shadows: one cast
    /// along the game's sun and one straight below that tightens as the block comes down.
    ///
    /// It never rotates: what falls is what formed, and it is still standing when it sinks away.
    /// See SixPathsSlab.cs for why a block that tips flat stops being a solid.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SixPathsSlamGraphics
    {
        /// <summary>
        /// Cells of shadow per cell of height, per unit of the game's shadow vector. The vector runs
        /// from 1.5 long at noon to 16.6 at dawn and dusk; the game stretches its own shadows by it
        /// inside a shader, so this factor is picked rather than read: 7.5, mid-morning, casts the
        /// 0.55 cells per cell the lab sketch was tuned with.
        /// </summary>
        public const float SunScale = 0.55f / 7.5f;

        private static readonly Material solid =
            new Material(ShaderDatabase.Transparent) { mainTexture = BaseContent.WhiteTex };
        private static readonly Material glow =
            new Material(ShaderDatabase.MoteGlow) { mainTexture = BaseContent.WhiteTex };
        private static readonly Material soft = MaterialPool.MatFrom("RimArt/SixPaths/SoftDisc", ShaderDatabase.Transparent);
        private static readonly Material softGlow = MaterialPool.MatFrom("RimArt/SixPaths/SoftDisc", ShaderDatabase.MoteGlow);
        private static readonly Material puff = MaterialPool.MatFrom("RimArt/SixPaths/Puff", ShaderDatabase.Transparent);
        private static readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private static readonly Mesh ring = VfxDraw.Ring(0.90f, "Six Paths slam ring");

        // One mesh per thing drawn in a frame, never one reused. Graphics.DrawMesh reads a mesh
        // when the frame renders rather than when it is called, so the block and its two motion
        // ghosts writing into one set of vertices would all come out at the last pose written.
        private static readonly Block[] blocks = { new Block(), new Block(), new Block() };
        private static readonly SoftShadow sunShadow = new SoftShadow("sun"), contactShadow = new SoftShadow("contact");
        private static readonly Quads cracks = new Quads("Six Paths slam cracks", SixPathsSlamTiming.Cracks * 4);
        private static readonly Vector2[] scratch = new Vector2[SixPathsSlab.CornerCount];
        private static float crackGrowth = -1f;

        // The orb's own colours, because the block is the orbs: same near-black body, same violet.
        private static readonly Color Body = new Color(0.020f, 0.015f, 0.032f);
        private static readonly Color Tint = new Color(0.62f, 0.52f, 0.95f);
        private static readonly Color Rim = new Color(0.52f, 0.36f, 0.86f);
        private static readonly Color Flash = new Color(0.86f, 0.80f, 1f);
        private static readonly Color Dirt = new Color(0.36f, 0.28f, 0.20f);

        /// <summary>
        /// The whole sequence, driven by <paramref name="seconds"/> alone: a caller that stops
        /// advancing it freezes the block wherever it is, mid-fall included.
        /// </summary>
        public static void Draw(Vector3 ground, float seconds, Map map)
        {
            if (!ground.ToIntVec3().InBounds(map) || ground.ToIntVec3().Fogged(map)) return;
            Vector2 sun = GenCelestial.GetLightSourceInfo(map, GenCelestial.LightType.Shadow).vector * SunScale;
            // The game's own fade: shadows go at dawn and dusk, when the light is changing over.
            float daylight = GenCelestial.CurShadowStrength(map);
            DrawGather(ground, seconds, map, sun, daylight);
            DrawBlock(ground, seconds, sun, daylight);
            DrawImpact(ground, seconds, map, sun, daylight);
        }

        private static void DrawGather(Vector3 ground, float seconds, Map map, Vector2 sun, float daylight)
        {
            float shadows = AltitudeLayer.Shadows.AltitudeFor();
            for (int i = 0; i < SixPathsTiming.Orbs; i++)
            {
                GatherStep step = SixPathsSlamTiming.Orb(i, SixPathsTiming.Orbs, seconds);
                if (step.alpha <= 0f) continue;
                Vector3 position = ground + new Vector3(Mathf.Cos(step.angle), 0f,
                    Mathf.Sin(step.angle) * SixPathsTiming.OrbitDepth) * step.radius;
                if (!position.ToIntVec3().InBounds(map)) continue;

                Vector3 shadow = position + new Vector3(sun.x, 0f, sun.y) * step.height;
                float strength = SixPathsSlamLook.SunShadow * 0.55f * daylight
                    * Mathf.Lerp(1f, 0.4f, step.height / SixPathsHeight.Ceiling);
                DrawMesh(MeshPool.plane10, shadow.WithY(shadows), step.size * 2.2f, step.size * 1.6f, 0f,
                    new Color(0f, 0f, 0f, strength * step.alpha), soft);
                SixPathsGraphics.DrawAloft(i, position, step.height, SixPathsShapes.Orb,
                    step.size, -(step.angle * Mathf.Rad2Deg + 90f), step.alpha);
            }

            float flash = SixPathsSlamTiming.FuseFlash(seconds);
            if (flash <= 0.001f) return;
            // At the fuse the six orbs are at the apex, so the flash belongs up there with them.
            Vector3 sky = SixPathsHeight.Above(ground, SixPathsSlamTiming.Apex)
                .WithY(AltitudeLayer.MoteOverhead.AltitudeFor() + 0.05f);
            DrawMesh(MeshPool.plane10, sky, 6.4f * flash, 6.4f * flash, 0f,
                new Color(Flash.r, Flash.g, Flash.b, flash * 0.55f), softGlow);
        }

        private static void DrawBlock(Vector3 ground, float seconds, Vector2 sun, float daylight)
        {
            SlabPose pose = SixPathsSlamTiming.Slab(seconds);
            if (pose.alpha <= 0.001f || pose.scale <= 0.001f) return;
            float altitude = AltitudeLayer.MoteOverhead.AltitudeFor();
            float scale = SixPathsSlamTiming.DrawScale(pose), clearance = SixPathsSlamTiming.Clearance(pose);

            DrawShadows(ground, pose, scale, clearance, sun, daylight);

            // Motion ghosts, faces only, and only while it is falling: most of a cell between
            // frames reads as teleporting without a couple of copies left behind.
            if (seconds > SixPathsSlamTiming.FallAt && seconds < SixPathsSlamTiming.LandAt)
                for (int i = 2; i >= 1; i--)
                {
                    SlabPose ghost = SixPathsSlamTiming.Slab(seconds - i * 0.045f);
                    if (ghost.height <= pose.height) continue;
                    blocks[i].Rebuild(ghost.yaw, ghost.height, SixPathsSlamTiming.DrawScale(ghost));
                    DrawFaces(blocks[i], ground.WithY(altitude - 0.01f * i), 0.22f - i * 0.06f);
                }

            Block block = blocks[0];
            block.Rebuild(pose.yaw, pose.height, scale);
            block.RebuildEdges(SixPathsSlamTiming.SeamWeight(seconds));
            Vector3 at = ground.WithY(altitude);
            DrawFaces(block, at, pose.alpha);
            DrawMesh(block.halo.mesh, at.WithY(altitude - 0.002f), 1f, 1f, 0f,
                new Color(Rim.r, Rim.g, Rim.b, SixPathsSlamLook.GlowAlpha * pose.alpha), glow);
            DrawMesh(block.inner.mesh, at.WithY(altitude + 0.004f), 1f, 1f, 0f,
                new Color(Rim.r, Rim.g, Rim.b, SixPathsSlamLook.Outline * SixPathsSlamLook.Inner * pose.alpha), solid);
            Color outline = Color.Lerp(Rim, Flash, 0.25f);
            outline.a = SixPathsSlamLook.Outline * pose.alpha;
            DrawMesh(block.outline.mesh, at.WithY(altitude + 0.005f), 1f, 1f, 0f, outline, solid);
        }

        private static void DrawFaces(Block block, Vector3 at, float alpha)
        {
            for (int face = 0; face < SixPathsSlab.FaceCount; face++)
            {
                if (!block.visible[face]) continue;
                Color colour = Color.Lerp(Body, Tint, SixPathsSlamLook.FaceValue(face, block.front));
                colour.a = alpha;
                DrawMesh(block.faces[face], at, 1f, 1f, 0f, colour, solid);
            }
        }

        private static void DrawShadows(Vector3 ground, in SlabPose pose, float scale, float clearance,
            Vector2 sun, float daylight)
        {
            float altitude = AltitudeLayer.Shadows.AltitudeFor();

            // Every corner pushed along the sun by its own height, wrapped in a hull.
            SixPathsSlab.Cast(scratch, pose.yaw, pose.height, scale, sun);
            sunShadow.Rebuild(scratch, SixPathsSlamLook.SunSoftness(clearance));
            DrawMesh(sunShadow.mesh, ground.WithY(altitude), 1f, 1f, 0f, new Color(0f, 0f, 0f,
                SixPathsSlamLook.SunStrength(clearance) * daylight * pose.alpha / SoftShadow.Layers), solid);

            SixPathsSlab.Footprint(scratch, pose.yaw, scale);
            float spread = SixPathsSlamLook.ContactSpread(clearance);
            for (int i = 0; i < scratch.Length; i++) scratch[i] = scratch[i] * spread;
            contactShadow.Rebuild(scratch, SixPathsSlamLook.ContactSoftness(clearance));
            DrawMesh(contactShadow.mesh, ground.WithY(altitude + 0.003f), 1f, 1f, 0f, new Color(0f, 0f, 0f,
                SixPathsSlamLook.ContactStrength(clearance) * pose.alpha / SoftShadow.Layers), solid);
        }

        private static void DrawImpact(Vector3 ground, float seconds, Map map, Vector2 sun, float daylight)
        {
            if (seconds < SixPathsSlamTiming.LandAt) return;
            float front = AltitudeLayer.MoteOverhead.AltitudeFor();
            // Dust and debris north of the base are behind the block, so they go under it.
            float behind = AltitudeLayer.MoteOverheadLow.AltitudeFor();
            float low = AltitudeLayer.MoteLow.AltitudeFor();
            float shadows = AltitudeLayer.Shadows.AltitudeFor();

            float marks = SixPathsSlamTiming.MarksAlpha(seconds);
            if (marks > 0.001f)
            {
                float growth = SixPathsSlamTiming.CrackGrowth(seconds);
                if (growth != crackGrowth)
                {
                    crackGrowth = growth;
                    cracks.Clear();
                    for (int crack = 0; crack < SixPathsSlamTiming.Cracks; crack++)
                        for (int step = 0; step < 4; step++)
                        {
                            SixPathsSlamTiming.CrackSegment(crack, step, growth, out Vector2 from, out Vector2 to);
                            cracks.Add(from, to, SixPathsSlamTiming.CrackWidth(step));
                        }
                    cracks.Commit();
                }
                DrawMesh(cracks.mesh, ground.WithY(AltitudeLayer.Filth.AltitudeFor()), 1f, 1f, 0f,
                    new Color(0.12f, 0.08f, 0.05f, 0.6f * marks), solid);
            }

            float flash = SixPathsSlamTiming.ImpactFlash(seconds);
            if (flash > 0.001f)
            {
                float size = 2f * SixPathsSlamTiming.ImpactFlashRadius(seconds);
                DrawMesh(MeshPool.plane10, ground.WithY(low + 0.01f), size, size * 0.8f, 0f,
                    new Color(Flash.r, Flash.g, Flash.b, flash), softGlow);
            }

            float ringAlpha = SixPathsSlamTiming.RingAlpha(seconds);
            if (ringAlpha > 0.001f)
            {
                float radius = SixPathsSlamTiming.RingRadius(seconds);
                DrawMesh(ring, ground.WithY(low + 0.02f), radius, radius * 0.8f, 0f,
                    new Color(Rim.r, Rim.g, Rim.b, ringAlpha), solid);
            }

            for (int i = 0; i < SixPathsSlamTiming.Puffs; i++)
            {
                ImpactParticle p = SixPathsSlamTiming.Puff(i, seconds);
                if (p.alpha <= 0.001f || !Visible(ground, p, map)) continue;
                DrawMesh(MeshPool.plane10, Lifted(ground, p).WithY((p.z > 0f ? behind : front + 0.02f) + i * 0.0001f),
                    p.size, p.size * 0.85f, p.rotation, new Color(0.66f, 0.58f, 0.48f, p.alpha), puff);
            }

            for (int i = 0; i < SixPathsSlamTiming.Debris; i++)
            {
                ImpactParticle p = SixPathsSlamTiming.Chunk(i, seconds);
                if (p.alpha <= 0.001f || !Visible(ground, p, map)) continue;
                if (p.height > 0.02f)
                    DrawMesh(MeshPool.plane10,
                        (ground + new Vector3(p.x + sun.x * p.height, 0f, p.z + sun.y * p.height)).WithY(shadows),
                        p.size * 1.4f, p.size, 0f, new Color(0f, 0f, 0f, 0.3f * p.alpha * daylight), soft);
                DrawMesh(MeshPool.plane10, Lifted(ground, p).WithY(p.z > 0f && p.height < 1f ? behind : front + 0.03f),
                    p.size, p.size * 0.8f, p.rotation, new Color(Dirt.r, Dirt.g, Dirt.b, p.alpha), solid);
            }

            for (int i = 0; i < SixPathsSlamTiming.SkirtPuffs; i++)
            {
                ImpactParticle p = SixPathsSlamTiming.SkirtPuff(i, seconds);
                if (p.alpha <= 0.001f || !Visible(ground, p, map)) continue;
                DrawMesh(MeshPool.plane10, Lifted(ground, p).WithY(p.z > 0f ? behind : front + 0.02f),
                    1.2f, 1f, p.rotation, new Color(0.62f, 0.55f, 0.46f, p.alpha), puff);
            }
        }

        private static Vector3 Lifted(Vector3 ground, in ImpactParticle p) =>
            ground + new Vector3(p.x, 0f, p.z + p.height * SixPathsHeight.Lift);

        private static bool Visible(Vector3 ground, in ImpactParticle p, Map map)
        {
            IntVec3 cell = (ground + new Vector3(p.x, 0f, p.z)).ToIntVec3();
            return cell.InBounds(map) && !cell.Fogged(map);
        }

        private static void DrawMesh(Mesh mesh, Vector3 position, float width, float depth,
            float rotation, Color colour, Material material)
        {
            properties.SetColor(ShaderPropertyIDs.Color, colour);
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(position, Quaternion.Euler(0f, rotation, 0f),
                new Vector3(width, 1f, depth)), material, 0, null, 0, properties);
        }

        /// <summary>A unit-radius band, for the shock ring on the floor.</summary>

        /// <summary>
        /// Thin quads along line segments, all in one mesh and one draw. Capacity is fixed at
        /// construction and unused quads collapse onto the origin, where a zero-area quad
        /// rasterises nothing, so a rebuild writes one array and allocates nothing.
        /// </summary>
        private sealed class Quads
        {
            public readonly Mesh mesh;
            private readonly Vector3[] vertices;
            private int count;

            public Quads(string name, int capacity)
            {
                vertices = new Vector3[capacity * 4];
                var indices = new int[capacity * 6];
                for (int q = 0; q < capacity; q++)
                {
                    int v = q * 4, j = q * 6;
                    indices[j] = v; indices[j + 1] = v + 1; indices[j + 2] = v + 2;
                    indices[j + 3] = v; indices[j + 4] = v + 2; indices[j + 5] = v + 3;
                }
                mesh = new Mesh { name = name, vertices = vertices };
                mesh.triangles = indices;
            }

            public void Clear() => count = 0;

            /// <summary>
            /// A quad <paramref name="width"/> × <paramref name="weight"/> across, running past
            /// both ends by half the full width so that lines meeting at a corner close it.
            /// </summary>
            public void Add(Vector2 a, Vector2 b, float width, float weight = 1f)
            {
                Vector2 along = b - a;
                float length = along.magnitude;
                if (length < 1e-5f || count * 4 >= vertices.Length) return;
                along /= length;
                Vector2 side = new Vector2(-along.y, along.x) * (width * 0.5f * weight);
                Vector2 end = along * (width * 0.5f);
                int v = count++ * 4;
                // Clockwise on screen, the same winding as the block's faces.
                vertices[v] = Flat(a - end + side);
                vertices[v + 1] = Flat(b + end + side);
                vertices[v + 2] = Flat(b + end - side);
                vertices[v + 3] = Flat(a - end - side);
            }

            public void Commit()
            {
                for (int v = count * 4; v < vertices.Length; v++) vertices[v] = Vector3.zero;
                mesh.vertices = vertices;
                mesh.RecalculateBounds();
            }
        }

        private static Vector3 Flat(Vector2 point) => new Vector3(point.x, 0f, point.y);

        /// <summary>
        /// A convex shadow drawn as three copies of its hull, one shrunk and one grown by half the
        /// softness, each at a third of the strength: the overlap is dark and the edge falls off
        /// in steps. Eight hull slots per copy, because the hull of eight corners has at most eight
        /// points; slots past the hull repeat its last point and make zero-area triangles.
        /// </summary>
        private sealed class SoftShadow
        {
            public const int Layers = 3;
            private const int Slots = SixPathsSlab.CornerCount;
            public readonly Mesh mesh;
            private readonly Vector3[] vertices = new Vector3[Layers * (Slots + 1)];
            private readonly Vector2[] points = new Vector2[Slots];
            private readonly Vector2[] hull = new Vector2[Slots * 2];

            public SoftShadow(string name)
            {
                var indices = new int[Layers * Slots * 3];
                for (int layer = 0; layer < Layers; layer++)
                    for (int i = 0; i < Slots; i++)
                    {
                        int centre = layer * (Slots + 1), j = (layer * Slots + i) * 3;
                        // Centre, next, current over a counter-clockwise hull: clockwise on
                        // screen, the winding the kit's discs use.
                        indices[j] = centre; indices[j + 1] = centre + 1 + (i + 1) % Slots; indices[j + 2] = centre + 1 + i;
                    }
                mesh = new Mesh { name = "Six Paths slam " + name + " shadow", vertices = vertices };
                mesh.triangles = indices;
            }

            public void Rebuild(Vector2[] corners, float softness)
            {
                for (int i = 0; i < Slots; i++) points[i] = corners[i];
                int count = SixPathsSlab.Hull(points, Slots, hull);
                if (count < 3)
                {
                    for (int v = 0; v < vertices.Length; v++) vertices[v] = Vector3.zero;
                }
                else
                {
                    Vector2 centre = Vector2.zero;
                    for (int i = 0; i < count; i++) centre += hull[i];
                    centre /= count;
                    for (int layer = 0; layer < Layers; layer++)
                    {
                        float grow = softness * (layer * 0.5f - 0.5f);
                        int first = layer * (Slots + 1);
                        vertices[first] = Flat(centre);
                        for (int i = 0; i < Slots; i++)
                        {
                            Vector2 point = hull[Mathf.Min(i, count - 1)], outward = point - centre;
                            float length = outward.magnitude;
                            vertices[first + 1 + i] = Flat(point + outward * (grow / (length > 1e-5f ? length : 1f)));
                        }
                    }
                }
                mesh.vertices = vertices;
                mesh.RecalculateBounds();
            }
        }

        /// <summary>
        /// One block's six faces and, for the one drawn with edges, its outline, inner lines and
        /// glow. Topology is fixed at construction and only vertex positions move.
        ///
        /// Faces are separate meshes because each is drawn in its own colour and the shader takes
        /// colour per draw. A box is convex, so the faces turned toward the camera cannot overlap
        /// each other and need neither depth sorting nor separate altitudes.
        /// </summary>
        private sealed class Block
        {
            public readonly Mesh[] faces = new Mesh[SixPathsSlab.FaceCount];
            public readonly bool[] visible = new bool[SixPathsSlab.FaceCount];
            public int front;
            public readonly Quads outline = new Quads("Six Paths block outline", SixPathsSlab.EdgeCount);
            public readonly Quads halo = new Quads("Six Paths block glow", SixPathsSlab.EdgeCount);
            // Inner edges, plus five seams across each of at most two visible upright faces.
            public readonly Quads inner = new Quads("Six Paths block inner", SixPathsSlab.EdgeCount + 10);

            private readonly Vector2[] corners = new Vector2[SixPathsSlab.CornerCount];
            private readonly Vector3[][] faceVertices = new Vector3[SixPathsSlab.FaceCount][];

            public Block()
            {
                // Two triangles per quad: a face that survives the visibility test is clockwise
                // on screen, which is the front side.
                var quad = new[] { 0, 1, 2, 0, 2, 3 };
                for (int face = 0; face < SixPathsSlab.FaceCount; face++)
                {
                    faceVertices[face] = new Vector3[4];
                    faces[face] = new Mesh { name = "Six Paths block face " + face, vertices = faceVertices[face] };
                    faces[face].triangles = quad;
                }
            }

            public void Rebuild(float yaw, float height, float scale)
            {
                SixPathsSlab.Project(corners, yaw, height, scale);
                front = SixPathsSlab.Front(corners, yaw);
                for (int face = 0; face < SixPathsSlab.FaceCount; face++)
                {
                    visible[face] = SixPathsSlab.Visible(corners, face);
                    if (!visible[face]) continue;
                    int[] index = SixPathsSlab.FaceCorners[face];
                    for (int i = 0; i < 4; i++) faceVertices[face][i] = Flat(corners[index[i]]);
                    faces[face].vertices = faceVertices[face];
                    faces[face].RecalculateBounds();
                }
            }

            /// <summary>After <see cref="Rebuild"/>, from the same corners.</summary>
            public void RebuildEdges(float seamWeight)
            {
                outline.Clear(); halo.Clear(); inner.Clear();
                for (int edge = 0; edge < SixPathsSlab.EdgeCount; edge++)
                {
                    int shown = SixPathsSlab.SidesShown(corners, edge);
                    if (shown == 0) continue;
                    Vector2 a = corners[SixPathsSlab.EdgeCorners[edge][0]], b = corners[SixPathsSlab.EdgeCorners[edge][1]];
                    if (shown == 2)
                    {
                        inner.Add(a, b, SixPathsSlamLook.EdgeWidth);
                        continue;
                    }
                    outline.Add(a, b, SixPathsSlamLook.EdgeWidth);
                    halo.Add(a, b, SixPathsSlamLook.GlowWidth);
                }

                if (SixPathsSlamLook.Seams)
                    for (int face = 0; face < SixPathsSlab.FaceCount; face++)
                    {
                        if (SixPathsSlab.FaceNormals[face].y != 0f || !visible[face]) continue;
                        // The face's two bottom corners, in its own winding order.
                        int[] index = SixPathsSlab.FaceCorners[face];
                        int bottomA = -1, bottomB = -1;
                        for (int i = 0; i < 4; i++)
                            if ((index[i] & 2) == 0)
                            {
                                if (bottomA < 0) bottomA = index[i]; else bottomB = index[i];
                            }
                        Vector2 a = corners[bottomA], b = corners[bottomB];
                        // A corner's top minus its bottom is the drawn height of that upright edge.
                        float tall = corners[bottomA | 2].y - a.y;
                        for (int k = 1; k < 6; k++)
                        {
                            var lift = new Vector2(0f, tall * k / 6f);
                            inner.Add(a + lift, b + lift, SixPathsSlamLook.EdgeWidth * 0.6f, seamWeight);
                        }
                    }

                outline.Commit(); halo.Commit(); inner.Commit();
            }
        }
    }
}
