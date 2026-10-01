using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.UbwGraphics;

namespace RimArt
{
    /// <summary>
    /// The plate ground baked: every plate in painter's order (north first) into one mesh from the
    /// terrain atlas (per plate the faces down to the bottom of the world, the top as its own window of one
    /// earth tile in its shade, the hairline cracks on the map's plates, the lit lip along a crest that
    /// stands above its neighbour) and the shadows a raised edge casts along the sun into another. The
    /// port of bakeTerrain in Tools/VfxLab/web/sketches/lib/ubw-terrain.js. Built once per ground and sun.
    /// </summary>
    internal sealed class UbwTerrainBake
    {
        public UbwTerrain Terrain;
        public Mesh Ground, Shadows;
        public int Vertices;

        /// <summary>The face gradient runs GradLen screen cells down from the crest, then the face is solid to the bottom. A plate takes a window of Tile cells of an earth tile (or its own size if bigger).</summary>
        public const float GradLen = 3f, LipW = 0.06f, LipMin = 0.1f, Tile = 6f, HairW = 0.028f;
        public const int Hairlines = 2;
        // The atlas (make_trace_textures.py): the earth tile of TilePx in four shades in a 2 x 2 block at
        // the top left, the face gradient in a column right of it, swatches of SwatchPx along the bottom:
        // 0 the lit lip, 1 the crest, 2 the solid foot, 3 the crack floor, 4 a hairline crack.
        private const float Side = 1024f, TilePx = 384f, Pad = 4f, GradX0 = 800f, GradX1 = 832f, GradH = 768f, SwatchY = 900f, SwatchPx = 64f;
        private const float TileUV = TilePx / Side;
        private static readonly Vector2 GradTop = new Vector2((GradX0 + GradX1) / 2f / Side, 1f - (Pad + 1f) / Side), GradBot = new Vector2((GradX0 + GradX1) / 2f / Side, 1f - (GradH - Pad) / Side);
        private static readonly Vector2 SwLip = SwatchOf(0), SwFoot = SwatchOf(2), SwHair = SwatchOf(4), Flat = new Vector2(0.5f, 0.5f);

        private static Vector2 SwatchOf(int k) => new Vector2((k + 0.5f) * SwatchPx / Side, 1f - (SwatchY + SwatchPx / 2f) / Side);

        public static UbwTerrainBake Build(UbwTerrain T, Vector2 sun)
        {
            var bake = new UbwTerrainBake { Terrain = T };
            var ground = new Builder("UBW terrain " + T.Seed);
            var shadows = new Builder("UBW terrain shadows " + T.Seed);
            var pts = new List<Vector2>(4);
            var uvs = new List<Vector2>(4);
            void Quad(Builder into, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                pts.Clear(); uvs.Clear();
                pts.Add(a); pts.Add(b); pts.Add(c); pts.Add(d);
                uvs.Add(ua); uvs.Add(ub); uvs.Add(uc); uvs.Add(ud);
                into.Poly(pts, uvs);
            }
            foreach (int i in T.PaintOrder())
            {
                UbwPlate p = T.Plates[i];
                float lift = (float)(p.H * UbwTerrain.Lift), faceScreen = (float)((p.H - T.Bottom) * UbwTerrain.Lift), G = Mathf.Min(faceScreen, GradLen);
                int n = p.Poly.Count;
                var ox = new float[n];
                var oz = new float[n];
                for (int k = 0; k < n; k++)
                {
                    UbwPlateVertex a = p.Poly[k], b = p.Poly[(k + 1) % n];
                    double dx = b.X - a.X, dz = b.Z - a.Z, L = Math.Sqrt(dx * dx + dz * dz);
                    if (L == 0) L = 1;
                    ox[k] = (float)(dz / L * p.Sgn);                                 // outward normal
                    oz[k] = (float)(-dx / L * p.Sgn);
                }
                for (int k = 0; k < n; k++)
                {
                    UbwPlateVertex a = p.Poly[k], b = p.Poly[(k + 1) % n];
                    float ax = (float)a.X, az = (float)a.Z, bx = (float)b.X, bz = (float)b.Z;
                    if (oz[k] < -0.02f)
                    {
                        float za = az + lift, zb = bz + lift;
                        Quad(ground, new Vector2(ax, za), new Vector2(bx, zb), new Vector2(bx, zb - G), new Vector2(ax, za - G), GradTop, GradTop, GradBot, GradBot);
                        if (faceScreen > G)
                            Quad(ground, new Vector2(ax, za - G), new Vector2(bx, zb - G), new Vector2(bx, zb - faceScreen), new Vector2(ax, za - faceScreen), SwFoot, SwFoot, SwFoot, SwFoot);
                    }
                    UbwPlate nb = a.Nb >= 0 ? T.Plates[a.Nb] : null;
                    if (nb == null) continue;
                    float hd = (float)(p.H - nb.H);
                    if (hd > 0.04f && ox[k] * sun.x + oz[k] * sun.y > 0f)
                    {
                        float lz = (float)(nb.H * UbwTerrain.Lift);
                        Quad(shadows, new Vector2(ax, az + lz), new Vector2(bx, bz + lz), new Vector2(bx + sun.x * hd, bz + lz + sun.y * hd), new Vector2(ax + sun.x * hd, az + lz + sun.y * hd), Flat, Flat, Flat, Flat);
                    }
                }
                // The top: a window of this plate's shade of the earth tile.
                float tu0 = (p.Shade % 2) * TileUV, tv0 = 1f - (p.Shade / 2 + 1) * TileUV;
                float extent = (float)Math.Max(p.MaxX - p.MinX, p.MaxZ - p.MinZ), tile = Mathf.Max(Tile, extent), span = extent / tile * TileUV;
                float room = Mathf.Max(0f, TileUV - span - 0.012f);
                float u0 = tu0 + 0.006f + (float)UbwTerrain.Hash(i, 5, T.Seed) * room, v0 = tv0 + 0.006f + (float)UbwTerrain.Hash(i, 6, T.Seed) * room;
                pts.Clear(); uvs.Clear();
                foreach (UbwPlateVertex q in p.Poly)
                {
                    pts.Add(new Vector2((float)q.X, (float)q.Z + lift));
                    uvs.Add(new Vector2(u0 + (float)(q.X - p.MinX) / tile * TileUV, v0 + (float)(q.Z - p.MinZ) / tile * TileUV));
                }
                ground.Poly(pts, uvs);
                // Hairline cracks on the map's plates: short wandering lines from near the middle.
                if (p.Tier == 0 && Math.Max(Math.Abs(T.Seeds[i].X), Math.Abs(T.Seeds[i].Z)) <= UbwField.MapHalf + 3)
                {
                    float cx = (float)((p.MinX + p.MaxX) / 2), cz = (float)((p.MinZ + p.MaxZ) / 2);
                    for (int c = 0; c < Hairlines; c++)
                    {
                        int s0 = i * 31 + c * 7;
                        double ang = UbwTerrain.Hash(s0, 1, T.Seed) * Math.PI * 2;
                        var line = new List<Vector2> { new Vector2(cx + (float)(UbwTerrain.Hash(s0, 2, T.Seed) - .5) * extent * 0.3f, cz + lift + (float)(UbwTerrain.Hash(s0, 3, T.Seed) - .5) * extent * 0.3f) };
                        for (int j = 1; j <= 4; j++)
                        {
                            ang += (UbwTerrain.Hash(s0, 3 + j, T.Seed) - .5) * 1.3;
                            float step = 0.18f + (float)UbwTerrain.Hash(s0, 9 + j, T.Seed) * 0.28f;
                            Vector2 q = line[j - 1];
                            line.Add(new Vector2(q.x + (float)Math.Cos(ang) * step, q.y + (float)Math.Sin(ang) * step));
                        }
                        for (int j = 1; j < line.Count; j++)
                        {
                            Vector2 a = line[j - 1], b = line[j];
                            float dx = b.x - a.x, dz = b.y - a.y, L = Mathf.Sqrt(dx * dx + dz * dz);
                            if (L == 0f) L = 1f;
                            float nx = -dz / L * HairW / 2f, nz = dx / L * HairW / 2f;
                            Quad(ground, new Vector2(a.x + nx, a.y + nz), new Vector2(b.x + nx, b.y + nz), new Vector2(b.x - nx, b.y - nz), new Vector2(a.x - nx, a.y - nz), SwHair, SwHair, SwHair, SwHair);
                        }
                    }
                }
                // The lit lip along a crest that stands above the plate across that edge.
                for (int k = 0; k < n; k++)
                {
                    UbwPlateVertex a = p.Poly[k], b = p.Poly[(k + 1) % n];
                    UbwPlate nb = a.Nb >= 0 ? T.Plates[a.Nb] : null;
                    if (oz[k] >= -0.02f || nb == null || p.H - nb.H < LipMin) continue;
                    float w = LipW * (p.Tier != 0 ? 1.5f : 1f), ix = -ox[k] * w, iz = -oz[k] * w;
                    float ax = (float)a.X, az = (float)a.Z + lift, bx = (float)b.X, bz = (float)b.Z + lift;
                    Quad(ground, new Vector2(ax, az), new Vector2(bx, bz), new Vector2(bx + ix, bz + iz), new Vector2(ax + ix, az + iz), SwLip, SwLip, SwLip, SwLip);
                }
            }
            bake.Ground = ground.Take("UBW terrain " + T.Seed);
            bake.Shadows = shadows.Take("UBW terrain shadows " + T.Seed);
            bake.Vertices = ground.Count + shadows.Count;
            return bake;
        }

        /// <summary>The plates round <paramref name="o"/> in the world's light, and their cast shadows.</summary>
        public void Draw(Vector2 o, float plateAltitude, float shadowAltitude, Color tint, float strength)
        {
            DrawMesh(Ground, o, plateAltitude, 1f, 1f, 0f, tint, UbwTerrainGraphics.atlas);
            DrawMesh(Shadows, o, shadowAltitude, 1f, 1f, 0f, Fade(Black, 0.4f * strength / 0.32f), solid);
        }
    }

    /// <summary>
    /// The world v4's ground: the plates made once (seed 1, the sketch's), baked per sun, and the crack floor
    /// under them. The port of terrainBase in lib/ubw-terrain.js. Level shapes only, no facing.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class UbwTerrainGraphics
    {
        internal static readonly Material atlas = MaterialPool.MatFrom("RimArt/Trace/TerrainAtlas", ShaderDatabase.Transparent);
        internal static readonly Color Base = new Color(0.07f, 0.045f, 0.035f);

        private static UbwTerrain ground;
        private static readonly List<(UbwTerrain terrain, string sun, UbwTerrainBake bake)> bakes = new List<(UbwTerrain, string, UbwTerrainBake)>();

        /// <summary>The world v4's ground (<see cref="UbwGround.Crest"/>, seed 1), made once.</summary>
        internal static UbwTerrain Ground() => ground ?? (ground = UbwTerrain.Make(UbwGround.Crest(UbwCrest.North), 1));

        /// <summary>The ground baked under this sun; the last three are kept.</summary>
        internal static UbwTerrainBake BakeFor(UbwTerrain T, Vector2 sun)
        {
            string key = sun.x.ToString("0.000") + "," + sun.y.ToString("0.000");
            for (int i = 0; i < bakes.Count; i++) if (bakes[i].terrain == T && bakes[i].sun == key) return bakes[i].bake;
            UbwTerrainBake bake = UbwTerrainBake.Build(T, sun);
            bakes.Add((T, key, bake));
            if (bakes.Count > 3) bakes.RemoveAt(0);
            return bake;
        }

        /// <summary>The crack floor under the plates, over the world's square only: past its edge the far haze colour (the backstop) shows.</summary>
        internal static void TerrainBase(Vector2 o, UbwTerrain T, float altitude) =>
            Sprite(o, 2f * (float)T.EdgeAt, 2f * (float)T.EdgeAt, Base, solid, altitude);
    }
}
