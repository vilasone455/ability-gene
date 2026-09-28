using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace RimArt
{
    /// <summary>One plate of ground: its outline and centre in world cells, the cells whose centres it covers.</summary>
    public sealed class ChibakuPlate
    {
        public int index;
        public Vector2 centre;
        public Vector2[] outline;
        /// <summary>Distance of the centre from the middle, as a share of the radius (0 middle, 1 rim).</summary>
        public float along;
        /// <summary>Stays in the ground: a covered cell has a building on it (a wall, a conduit, a frame) or a roof over it.</summary>
        public bool anchored;
        public readonly List<IntVec3> cells = new List<IntVec3>();
        internal Mesh face, edge, hole;
    }

    /// <summary>
    /// The ground under a Chibaku Tensei circle, taken up as plates (test stage: the plates lift 1 cell and
    /// land again; there is no ball yet).
    ///
    /// The picture: when it is made, the map's own ground meshes under the circle (terrain, the blended
    /// terrain edges, scatter, sand, snow) are drawn once into a render texture by a view straight down onto
    /// the square round the circle, the way the game's camera sees them. The game's terrain meshes carry no
    /// UVs (the terrain shader textures by world position), so a moving copy of a terrain mesh would slide
    /// over its texture; the captured picture is fixed, and each plate's mesh samples the part it covered.
    /// A plate therefore shows whatever ground was there: soil, sand, a floor, blended edges.
    ///
    /// Meshes are built once. A frame costs a matrix per draw: up to five draws per lifted plate (the hole it
    /// left, its shadow, a dark south side for its thickness, an edge, the face).
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class ChibakuGround : IDisposable
    {
        public const int PixelsPerCell = 64;
        public const float Thickness = .16f;
        /// <summary>The test's timeline, seconds: rest, then each plate lifts over Rise to 1 cell, then lands.</summary>
        public const float RestUntil = .6f, Rise = .45f, LiftTo = 1f, LowerFrom = 3.4f, End = 5.6f;

        private static readonly Type[] GroundLayers =
            { typeof(SectionLayer_Terrain), typeof(SectionLayer_TerrainEdges), typeof(SectionLayer_TerrainScatter), typeof(SectionLayer_Sand), typeof(SectionLayer_Snow) };
        private static readonly Vector2 ShadowPerCell = new Vector2(-.45f, -.32f);
        private static readonly Color ShadowColour = new Color(0f, 0f, 0f, .35f), SideColour = new Color(.13f, .09f, .06f),
            EdgeColour = new Color(.07f, .05f, .035f), HoleRim = new Color(.24f, .18f, .12f), HoleDeep = new Color(.11f, .08f, .05f);
        private static readonly Material solid = new Material(ShaderDatabase.Transparent) { mainTexture = BaseContent.WhiteTex, name = "RimArt Chibaku solid" };
        private static readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();

        public readonly Map map;
        public readonly IntVec3 cell;
        public readonly float radius;
        /// <summary>The captured square: south-west corner and side, in world cells.</summary>
        public readonly float x0, z0, span;
        public RenderTexture texture;
        /// <summary>North first.</summary>
        public readonly List<ChibakuPlate> plates = new List<ChibakuPlate>();
        private Material faceMaterial;
        /// <summary>The captured picture as a solid (cutout) material, for anything that shows the ground.</summary>
        public Material FaceMaterial => faceMaterial;
        private Texture2D readBack;

        private ChibakuGround(Map map, IntVec3 cell, float radius)
        {
            this.map = map;
            this.cell = cell;
            this.radius = radius;
            Vector3 middle = cell.ToVector3Shifted();
            span = radius * 2f + 2f;
            x0 = middle.x - span / 2f;
            z0 = middle.z - span / 2f;
        }

        /// <summary>The ground round <paramref name="cell"/> captured and cut into plates.</summary>
        public static ChibakuGround Capture(Map map, IntVec3 cell, float radius, float plateSize = 1f, int seed = 0)
        {
            var ground = new ChibakuGround(map, cell, radius);
            ground.Photograph();
            ground.Cut(plateSize, seed);
            return ground;
        }

        /// <summary>Where a world point (x, z) sits in the captured picture.</summary>
        public Vector2 UV(Vector2 world) => new Vector2((world.x - x0) / span, (world.y - z0) / span);

        private void Photograph()
        {
            int pixels = Mathf.Clamp(Mathf.CeilToInt(span * PixelsPerCell), 128, 2048);
            texture = new RenderTexture(pixels, pixels, 0, RenderTextureFormat.ARGB32)
                { name = "RimArt Chibaku ground", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            texture.Create();

            // The sections under the square, their ground layers rebuilt now so a change made this tick shows.
            var sections = new HashSet<Section>();
            CellRect square = CellRect.FromLimits(Mathf.FloorToInt(x0), Mathf.FloorToInt(z0), Mathf.CeilToInt(x0 + span), Mathf.CeilToInt(z0 + span)).ClipInsideMap(map);
            foreach (IntVec3 c in square) sections.Add(map.mapDrawer.SectionAt(c));
            var draws = new List<(int queue, int layer, LayerSubMesh sub)>();
            foreach (Section section in sections)
                for (int l = 0; l < GroundLayers.Length; l++)
                {
                    SectionLayer layer = section.GetLayer(GroundLayers[l]);
                    if (layer == null) continue;
                    section.RegenerateSingleLayer(layer);
                    foreach (LayerSubMesh sub in layer.subMeshes)
                        if (sub.finalized && !sub.disabled && sub.mesh != null && sub.material != null && sub.material != MatBases.ShadowMask)
                            draws.Add((sub.material.renderQueue, l, sub));
                }

            // A view straight down, as Unity's own camera would be for this square: x east, z north, and
            // altitude toward the camera (Unity's view space looks down -z, so the matrix mirrors, as a
            // camera's worldToCameraMatrix does).
            var view = new Matrix4x4();
            view.SetRow(0, new Vector4(1f, 0f, 0f, 0f));
            view.SetRow(1, new Vector4(0f, 0f, 1f, 0f));
            view.SetRow(2, new Vector4(0f, 1f, 0f, -100f));
            view.SetRow(3, new Vector4(0f, 0f, 0f, 1f));
            Matrix4x4 projection = Matrix4x4.Ortho(x0, x0 + span, z0, z0 + span, .1f, 200f);
            var commands = new CommandBuffer { name = "RimArt Chibaku ground capture" };
            commands.SetRenderTarget(texture);
            commands.ClearRenderTarget(true, true, new Color(.3f, .25f, .2f, 1f));
            commands.SetViewProjectionMatrices(view, projection);
            foreach (var d in draws.OrderBy(d => d.queue).ThenBy(d => d.layer))
                commands.DrawMesh(d.sub.mesh, Matrix4x4.identity, d.sub.material);
            PrintPlants(commands, square);
            Graphics.ExecuteCommandBuffer(commands);
            commands.Release();

            // Cutout keeps the face solid: the clear is opaque, so every pixel ends well above the cut.
            faceMaterial = new Material(ShaderDatabase.Cutout) { mainTexture = texture, name = "RimArt Chibaku ground" };
        }

        /// <summary>
        /// Small plants inside the circle printed over the ground, the way <c>Plant.Print</c> lays them out (one
        /// quad per mesh the plant's growth shows, a single-mesh plant's foot on its cell's south edge), north
        /// first so nearer ones overlap. They go up printed on their plates and show on the ball; the plants
        /// themselves are removed when their plate tears free. Trees are left out: they fly as their own piece.
        /// </summary>
        private void PrintPlants(CommandBuffer commands, CellRect square)
        {
            Vector3 middle = cell.ToVector3Shifted();
            var quads = new List<(float z, Matrix4x4 at, Material material)>();
            foreach (IntVec3 c in square)
            {
                if ((c.ToVector3Shifted() - middle).MagnitudeHorizontal() > radius + 1f) continue;
                foreach (Thing thing in c.GetThingList(map))
                {
                    if (!(thing is Plant plant) || plant.def.plant.IsTree || plant.def.graphicData == null) continue;
                    Material material = plant.Graphic?.MatSingleFor(plant);
                    if (material == null) continue;
                    float visual = plant.def.plant.visualSizeRange.LerpThroughRange(plant.Growth), size = plant.def.graphicData.drawSize.x * visual;
                    int max = Mathf.Max(1, plant.def.plant.maxMeshCount), side = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(max)));
                    int meshes = Mathf.Clamp(Mathf.CeilToInt(plant.Growth * max), 1, max);
                    for (int k = 0; k < meshes; k++)
                    {
                        float jx = (float)ChibakuCut.Rand(plant.thingIDNumber * 31 + k * 2) - .5f, jz = (float)ChibakuCut.Rand(plant.thingIDNumber * 31 + k * 2 + 1) - .5f;
                        Vector3 at;
                        if (max == 1)
                        {
                            at = c.ToVector3Shifted() + new Vector3(jx, 0f, jz) * .1f;
                            at.z = Mathf.Max(at.z, c.z + visual / 2f);
                        }
                        else
                        {
                            float step = 1f / side;
                            at = new Vector3(c.x + step * (k / side + .5f), 0f, c.z + step * (k % side + .5f)) + new Vector3(jx, 0f, jz) * step * .6f;
                        }
                        quads.Add((at.z, Matrix4x4.TRS(new Vector3(at.x, 5f, at.z), Quaternion.identity, new Vector3(size, 1f, size)), material));
                    }
                }
            }
            foreach (var q in quads.OrderByDescending(q => q.z)) commands.DrawMesh(MeshPool.plane10, q.at, q.material);
        }

        private void Cut(float plateSize, int seed)
        {
            Vector3 middle = cell.ToVector3Shifted();
            var centre0 = new Vector2(middle.x, middle.z);
            foreach (Vector2[] local in ChibakuCut.Plates(radius, plateSize, seed))
            {
                var plate = new ChibakuPlate { index = plates.Count, outline = local.Select(q => q + centre0).ToArray() };
                plate.centre = plate.outline.Aggregate(Vector2.zero, (s, q) => s + q) / plate.outline.Length;
                plate.along = Mathf.Min(1f, (plate.centre - centre0).magnitude / radius);
                float minX = plate.outline.Min(q => q.x), maxX = plate.outline.Max(q => q.x), minZ = plate.outline.Min(q => q.y), maxZ = plate.outline.Max(q => q.y);
                for (int x = Mathf.FloorToInt(minX); x <= Mathf.FloorToInt(maxX); x++)
                    for (int z = Mathf.FloorToInt(minZ); z <= Mathf.FloorToInt(maxZ); z++)
                        if (ChibakuCut.Contains(plate.outline, new Vector2(x + .5f, z + .5f))) plate.cells.Add(new IntVec3(x, 0, z));
                plate.anchored = plate.cells.Any(c => !c.InBounds(map) || c.Roofed(map) || c.GetThingList(map).Any(t => t.def.category == ThingCategory.Building));
                plate.face = Fan(plate, 0f, true);
                plate.edge = Fan(plate, .022f, false);
                plate.hole = Fan(plate, -.045f, false);
                plates.Add(plate);
            }
            plates.Sort((a, b) => b.centre.y.CompareTo(a.centre.y));     // north first, so nearer plates draw over
            for (int i = 0; i < plates.Count; i++) plates[i].index = i;
        }

        /// <summary>
        /// A fan round the plate's centre, its outline moved <paramref name="grow"/> cells out (in, below 0),
        /// wound clockwise seen from above like the game's own meshes; with UVs into the captured picture.
        /// </summary>
        private Mesh Fan(ChibakuPlate plate, float grow, bool uvs)
        {
            int n = plate.outline.Length;
            var vertices = new Vector3[n + 1];
            var uv = new Vector2[n + 1];
            var triangles = new int[n * 3];
            uv[0] = uvs ? UV(plate.centre) : Vector2.zero;
            for (int i = 0; i < n; i++)
            {
                Vector2 d = plate.outline[i] - plate.centre;
                float length = d.magnitude;
                if (length > 1e-4f) d *= Mathf.Max(.1f, 1f + grow / length);
                vertices[i + 1] = new Vector3(d.x, 0f, d.y);
                uv[i + 1] = uvs ? UV(plate.centre + d) : Vector2.zero;
            }
            bool counterClockwise = ChibakuCut.SignedArea(plate.outline) > 0f;
            for (int i = 0; i < n; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % n;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = counterClockwise ? b : a;
                triangles[i * 3 + 2] = counterClockwise ? a : b;
            }
            var mesh = new Mesh { name = "RimArt Chibaku plate", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The test's height of a plate at <paramref name="seconds"/>: inner plates lift first, outer ones land first.</summary>
        public static float TestHeight(ChibakuPlate plate, float seconds)
        {
            if (plate.anchored) return 0f;
            float lift = RestUntil + plate.along + .25f * (float)ChibakuCut.Rand(plate.index * 3 + 1);
            float lower = LowerFrom + (1f - plate.along) + .25f * (float)ChibakuCut.Rand(plate.index * 3 + 2);
            return LiftTo * Smooth((seconds - lift) / Rise) * (1f - Smooth((seconds - lower) / Rise));
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        /// <summary>
        /// Draws the plates at <paramref name="seconds"/> of the test timeline. A plate at rest is drawn in
        /// place (so it can be compared with the ground under it), except an anchored one, which would cover
        /// its building; a lifted one leaves its hole and draws its
        /// shadow, side, edge and face, north first so nearer plates overlap.
        /// </summary>
        public void Draw(float seconds)
        {
            if (texture == null) return;
            float top = AltitudeLayer.MoteOverhead.AltitudeFor(), floor = AltitudeLayer.Filth.AltitudeFor() + .01f, shadows = AltitudeLayer.Shadows.AltitudeFor();
            for (int k = 0; k < plates.Count; k++)
            {
                ChibakuPlate plate = plates[k];
                float h = TestHeight(plate, seconds), altitude = top + k * .0003f;
                if (h <= 0f)
                {
                    if (!plate.anchored) DrawFace(plate, new Vector3(plate.centre.x, altitude, plate.centre.y));
                    continue;
                }
                Solid(plate.hole, new Vector3(plate.centre.x, floor, plate.centre.y), Color.Lerp(HoleRim, HoleDeep, 1f - plate.along));
                Solid(plate.face, new Vector3(plate.centre.x + ShadowPerCell.x * h, shadows, plate.centre.y + ShadowPerCell.y * h), ShadowColour);
                Solid(plate.face, new Vector3(plate.centre.x, altitude, plate.centre.y + (h - Thickness) * SixPathsHeight.Lift), SideColour);
                Solid(plate.edge, new Vector3(plate.centre.x, altitude + .0001f, plate.centre.y + h * SixPathsHeight.Lift), EdgeColour);
                DrawFace(plate, new Vector3(plate.centre.x, altitude + .0002f, plate.centre.y + h * SixPathsHeight.Lift));
            }
        }

        private void DrawFace(ChibakuPlate plate, Vector3 at)
        {
            properties.SetColor(ShaderPropertyIDs.Color, Color.white);
            Graphics.DrawMesh(plate.face, Matrix4x4.TRS(at, Quaternion.identity, Vector3.one), faceMaterial, 0, null, 0, properties);
        }

        private static void Solid(Mesh mesh, Vector3 at, Color colour)
        {
            properties.SetColor(ShaderPropertyIDs.Color, colour);
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(at, Quaternion.identity, Vector3.one), solid, 0, null, 0, properties);
        }

        /// <summary>
        /// The captured colour over a cell, averaged over 5 x 5 points in its middle, read through the same UVs
        /// the plates use (for tests; reads the picture back once).
        /// </summary>
        public Color SampleCell(IntVec3 c)
        {
            if (readBack == null)
            {
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = texture;
                readBack = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                readBack.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                readBack.Apply();
                RenderTexture.active = previous;
            }
            Color sum = Color.clear;
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < 5; j++)
                {
                    Vector2 uv = UV(new Vector2(c.x + .3f + i * .1f, c.z + .3f + j * .1f));
                    sum += readBack.GetPixelBilinear(uv.x, uv.y);
                }
            return sum / 25f;
        }

        public void Dispose()
        {
            foreach (ChibakuPlate plate in plates)
            {
                UnityEngine.Object.Destroy(plate.face);
                UnityEngine.Object.Destroy(plate.edge);
                UnityEngine.Object.Destroy(plate.hole);
            }
            plates.Clear();
            if (faceMaterial != null) UnityEngine.Object.Destroy(faceMaterial);
            if (readBack != null) UnityEngine.Object.Destroy(readBack);
            if (texture != null)
            {
                texture.Release();
                UnityEngine.Object.Destroy(texture);
            }
            faceMaterial = null;
            readBack = null;
            texture = null;
        }
    }
}
