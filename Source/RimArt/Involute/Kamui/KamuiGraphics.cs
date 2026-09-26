using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;

namespace RimArt
{
    /// <summary>The heights Kamui's dimension is drawn at. Only the order matters: back, field, fade.</summary>
    internal readonly struct KamuiLayers
    {
        public readonly float Back, Field, Fade;

        public KamuiLayers(float back, float field, float fade)
        {
            Back = back; Field = field; Fade = fade;
        }

        /// <summary>
        /// The dimension's own map. Its void terrain is opaque and the mod draws the whole field over the
        /// terrain, under TerrainScatter: the back, the baked blocks, then the dark past the map edge.
        /// </summary>
        public static readonly KamuiLayers Pocket = new KamuiLayers(
            AltitudeLayer.Terrain.AltitudeFor() + 0.005f, AltitudeLayer.Terrain.AltitudeFor() + 0.01f,
            AltitudeLayer.Terrain.AltitudeFor() + 0.2f);

        /// <summary>
        /// A home map, for the preview: packed between ItemImportant + 0.1 and + 0.2, as the castle's
        /// preview is, so the back covers the ground, plants, buildings and items and pawns stay on top.
        /// </summary>
        public static readonly KamuiLayers Preview = new KamuiLayers(
            AltitudeLayer.ItemImportant.AltitudeFor() + 0.1f, AltitudeLayer.ItemImportant.AltitudeFor() + 0.12f,
            AltitudeLayer.ItemImportant.AltitudeFor() + 0.2f);
    }

    /// <summary>
    /// Draws Kamui's dimension: the port of bakeField, drawVoid and drawEdgeFade in
    /// Tools/VfxLab/web/sketches/lib/kamui.js, with the same shapes and the same atlas.
    ///
    /// Every block is baked once into meshes in painter's order (north first; lower first on a tie),
    /// textured from one atlas that holds the colours (Textures/RimArt/Kamui/Atlas.png, made by
    /// make_kamui_textures.py), so the field is a draw call or two a frame. Per block: its south face (the
    /// face colour fading to void over <see cref="FaceFade"/> cells, then solid void, because every block
    /// is a pillar out of the abyss), two corner lines on the face, its top (walkable tops get the patch
    /// tile over them) and four edge lines. Blocks never overlap on the ground, so that order is the whole
    /// cover rule; a mesh that fills up hands over to the next, drawn a step higher.
    ///
    /// Every quad keeps the sketch's corner order (x0,z0), (x0,z1), (x1,z1), (x1,z0): clockwise on
    /// screen, so it survives the game's backface culling.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class KamuiGraphics
    {
        public const string Fight = "fight", Still = "still";

        // The void of each palette; everything else is in the atlas.
        internal static readonly Color VoidFight = new Color(10f / 255f, 16f / 255f, 22f / 255f);
        internal static readonly Color VoidStill = new Color(7f / 255f, 13f / 255f, 20f / 255f);
        private static readonly Material AtlasFight = MaterialPool.MatFrom("RimArt/Kamui/Atlas", ShaderDatabase.Transparent);
        private static readonly Material AtlasStill = MaterialPool.MatFrom("RimArt/Kamui/AtlasStill", ShaderDatabase.Transparent);
        private static readonly Material FadeMat = MaterialPool.MatFrom("RimArt/Kamui/Fade", ShaderDatabase.Transparent);

        public const float Lift = 0.60f, FaceFade = 2f, FaceTail = 14f, EdgeW = 0.06f;
        public const int PatchTile = 6;
        private const int AtlasN = 8, CellPx = 64, SidePx = AtlasN * CellPx, Pad = 5;
        /// <summary>A block is at most 4 + 8 quads and a 14 x 10 top 6 patch quads: kept well under 65,535 vertices a mesh.</summary>
        private const int MostVertices = 60000;
        private const float MeshStep = 0.0005f;

        internal static Color VoidOf(string palette) => palette == Still ? VoidStill : VoidFight;
        internal static Material AtlasOf(string palette) => palette == Still ? AtlasStill : AtlasFight;

        /// <summary>An atlas cell's uv rectangle, v up (v 1 is the top row of the image), inside its border.</summary>
        internal readonly struct Cell
        {
            public readonly float U0, U1, VTop, VBot, Uc, Vc;

            public Cell(int col, int row)
            {
                U0 = (col * CellPx + Pad) / (float)SidePx;
                U1 = ((col + 1) * CellPx - Pad) / (float)SidePx;
                VTop = 1f - (row * CellPx + Pad) / (float)SidePx;
                VBot = 1f - ((row + 1) * CellPx - Pad) / (float)SidePx;
                Uc = (U0 + U1) / 2f;
                Vc = (VTop + VBot) / 2f;
            }
        }

        /// <summary>The atlas column of a level: 0 walkable, 1 above it, 2-7 one to six cells down.</summary>
        private static int ColOfLevel(int level) => level == 0 ? 0 : level > 0 ? 1 : 1 + Math.Min(6, -level);

        private enum Uv { Solid, Down, Part }

        /// <summary>The field of one layout, baked. Built once per map; the layout never changes.</summary>
        internal sealed class KamuiField
        {
            public readonly List<Mesh> Meshes = new List<Mesh>();
            public int Quads;

            private readonly List<List<Vector3>> vertices = new List<List<Vector3>> { new List<Vector3>() };
            private readonly List<List<Vector2>> uvs = new List<List<Vector2>> { new List<Vector2>() };
            private readonly List<List<int>> triangles = new List<List<int>> { new List<int>() };

            public static KamuiField Build(KamuiLayout layout, bool lower = true, bool outside = true)
            {
                var field = new KamuiField();
                var voidCell = new Cell(0, 5);
                var patchCell = new Cell(1, 5);
                foreach (KamuiBlock b in layout.PaintOrder(lower, outside))
                {
                    int lc = ColOfLevel(b.Level);
                    float lift = b.Level * Lift;
                    float x0 = b.X, x1 = b.X + b.W, z0 = b.Z + lift, z1 = b.Z + b.H + lift;
                    float fade = FaceFade + Math.Max(0f, lift);                   // a tall block's face darkens over its height too
                    int patches = b.Level == 0 ? ((b.W + PatchTile - 1) / PatchTile) * ((b.H + PatchTile - 1) / PatchTile) : 0;
                    field.Room(4 * (9 + patches));
                    field.Quad(x0, z0 - fade - FaceTail, x1, z0 - fade, Uv.Solid, voidCell);
                    field.Quad(x0, z0 - fade, x1, z0, Uv.Down, new Cell(lc, 2));
                    field.Quad(x0, z0 - fade, x0 + EdgeW, z0, Uv.Down, new Cell(lc, 4));
                    field.Quad(x1 - EdgeW, z0 - fade, x1, z0, Uv.Down, new Cell(lc, 4));
                    Cell top = b.Level == 0 ? new Cell(b.Shade, 0) : b.Level > 0 ? new Cell(4, 0) : new Cell(Math.Min(6, -b.Level) - 1, 1);
                    field.Quad(x0, z0, x1, z1, Uv.Part, top);
                    if (b.Level == 0)
                    {
                        // Whole tiles from the top's north-west corner; the last row and column are cut
                        // to the top and show the matching part of their tile.
                        for (int tz = 0; tz < b.H; tz += PatchTile)
                            for (int tx = 0; tx < b.W; tx += PatchTile)
                            {
                                int w = Math.Min(PatchTile, b.W - tx), h = Math.Min(PatchTile, b.H - tz);
                                field.Quad(x0 + tx, z1 - tz - h, x0 + tx + w, z1 - tz, Uv.Part, patchCell,
                                    0f, w / (float)PatchTile, 1f - h / (float)PatchTile, 1f);
                            }
                    }
                    var edge = new Cell(lc, 3);
                    field.Quad(x0, z0, x1, z0 + EdgeW, Uv.Solid, edge);
                    field.Quad(x0, z1 - EdgeW, x1, z1, Uv.Solid, edge);
                    field.Quad(x0, z0, x0 + EdgeW, z1, Uv.Solid, edge);
                    field.Quad(x1 - EdgeW, z0, x1, z1, Uv.Solid, edge);
                }
                field.Bake("Kamui field " + layout.Seed);
                return field;
            }

            /// <summary>Starts a new mesh when the next block would not fit, so no block is split between two.</summary>
            private void Room(int more)
            {
                if (vertices[vertices.Count - 1].Count + more <= MostVertices) return;
                vertices.Add(new List<Vector3>());
                uvs.Add(new List<Vector2>());
                triangles.Add(new List<int>());
            }

            private void Quad(float x0, float z0, float x1, float z1, Uv mode, Cell c,
                float fu0 = 0f, float fu1 = 1f, float fv0 = 0f, float fv1 = 1f)
            {
                if (x1 <= x0 || z1 <= z0) return;
                List<Vector3> v = vertices[vertices.Count - 1];
                List<Vector2> uv = uvs[uvs.Count - 1];
                List<int> t = triangles[triangles.Count - 1];
                int n = v.Count;
                v.Add(new Vector3(x0, 0f, z0));
                v.Add(new Vector3(x0, 0f, z1));
                v.Add(new Vector3(x1, 0f, z1));
                v.Add(new Vector3(x1, 0f, z0));
                switch (mode)
                {
                    case Uv.Solid:
                        for (int i = 0; i < 4; i++) uv.Add(new Vector2(c.Uc, c.Vc));
                        break;
                    case Uv.Down:
                        uv.Add(new Vector2(c.Uc, c.VBot)); uv.Add(new Vector2(c.Uc, c.VTop));
                        uv.Add(new Vector2(c.Uc, c.VTop)); uv.Add(new Vector2(c.Uc, c.VBot));
                        break;
                    default:
                        float ua = Mathf.Lerp(c.U0, c.U1, fu0), ub = Mathf.Lerp(c.U0, c.U1, fu1);
                        float va = Mathf.Lerp(c.VBot, c.VTop, fv0), vb = Mathf.Lerp(c.VBot, c.VTop, fv1);
                        uv.Add(new Vector2(ua, va)); uv.Add(new Vector2(ua, vb));
                        uv.Add(new Vector2(ub, vb)); uv.Add(new Vector2(ub, va));
                        break;
                }
                t.Add(n); t.Add(n + 1); t.Add(n + 2); t.Add(n); t.Add(n + 2); t.Add(n + 3);
                Quads++;
            }

            private void Bake(string name)
            {
                for (int i = 0; i < vertices.Count; i++)
                {
                    if (vertices[i].Count == 0) continue;
                    var mesh = new Mesh
                    {
                        name = name + (i > 0 ? " " + i : ""),
                        vertices = vertices[i].ToArray(),
                        uv = uvs[i].ToArray(),
                        triangles = triangles[i].ToArray(),
                    };
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                    Meshes.Add(mesh);
                }
            }

            /// <summary>The baked blocks with the map's corner at <paramref name="corner"/>, each mesh a step over the last.</summary>
            public void Draw(Vector2 corner, in KamuiLayers layers, string palette)
            {
                Material atlas = AtlasOf(palette);
                for (int i = 0; i < Meshes.Count; i++)
                    DrawMesh(Meshes[i], corner, layers.Field + i * MeshStep, 1f, 1f, 0f, Color.white, atlas);
            }
        }

        /// <summary>The void under everything: one plane past the band on every side.</summary>
        internal static void DrawVoid(Vector2 corner, int size, float altitude, string palette)
        {
            float reach = size + 2 * KamuiLayout.Band + 80;
            DrawMesh(MeshPool.plane10, new Vector2(corner.x + size / 2f, corner.y + size / 2f), altitude, reach, reach, 0f, VoidOf(palette), solid);
        }

        /// <summary>
        /// The blocks past the edge go dark toward the band's outer side: on each side a strip whose alpha
        /// rises away from the map over 90 % of the band, then solid void to 12 cells past the band, which
        /// also covers tall tops lifted past it and low tops dropped past it. East and west strips run the
        /// full height and cover the corners.
        /// </summary>
        internal static void DrawEdgeFade(Vector2 corner, int size, float altitude, string palette)
        {
            float band = KamuiLayout.Band, n = size, ramp = band * 0.9f, solidW = band - ramp + 12f;
            float full = n + 2f * (ramp + solidW);
            Color c = VoidOf(palette);
            var mid = new Vector2(corner.x + n / 2f, corner.y + n / 2f);
            DrawMesh(MeshPool.plane10, new Vector2(corner.x + n + ramp / 2f, mid.y), altitude, ramp, full, 0f, c, FadeMat);
            DrawMesh(MeshPool.plane10, new Vector2(corner.x - ramp / 2f, mid.y), altitude, ramp, full, 180f, c, FadeMat);
            DrawMesh(MeshPool.plane10, new Vector2(corner.x + n + ramp + solidW / 2f, mid.y), altitude, solidW, full, 0f, c, solid);
            DrawMesh(MeshPool.plane10, new Vector2(corner.x - ramp - solidW / 2f, mid.y), altitude, solidW, full, 0f, c, solid);
            DrawMesh(MeshPool.plane10, new Vector2(mid.x, corner.y + n + ramp / 2f), altitude, ramp, n, -90f, c, FadeMat);
            DrawMesh(MeshPool.plane10, new Vector2(mid.x, corner.y - ramp / 2f), altitude, ramp, n, 90f, c, FadeMat);
            DrawMesh(MeshPool.plane10, new Vector2(mid.x, corner.y + n + ramp + solidW / 2f), altitude, n, solidW, 0f, c, solid);
            DrawMesh(MeshPool.plane10, new Vector2(mid.x, corner.y - ramp - solidW / 2f), altitude, n, solidW, 0f, c, solid);
        }

        /// <summary>The whole dimension with the map's corner at <paramref name="corner"/>: void, field, the dark past the edge.</summary>
        internal static void Draw(KamuiLayout layout, KamuiField field, Vector2 corner, in KamuiLayers layers, string palette, bool outside = true)
        {
            DrawVoid(corner, layout.Size, layers.Back, palette);
            field.Draw(corner, layers, palette);
            if (outside) DrawEdgeFade(corner, layout.Size, layers.Fade, palette);
        }

        // One layout and field per (seed, size) for the previews; the pocket map keeps its own.
        private static readonly Dictionary<(int, int), (KamuiLayout layout, KamuiField field)> cache = new Dictionary<(int, int), (KamuiLayout, KamuiField)>();

        internal static (KamuiLayout layout, KamuiField field) For(int seed, int size)
        {
            if (!cache.TryGetValue((seed, size), out var entry))
            {
                KamuiLayout layout = KamuiLayout.Generate(seed, size);
                entry = (layout, KamuiField.Build(layout));
                cache[(seed, size)] = entry;
            }
            return entry;
        }
    }
}
