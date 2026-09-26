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
    /// The world v2's ground baked: every plate in painter's order (north first) into one mesh from the
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
    /// The world v2's ground and sky drawn: the crack floor under the plates, and north of the ridge the
    /// sky (the twilight gradient, a warm band along the ridge, the low sun, nine near-black gears hanging
    /// in it with their shadows cast from their bodies along the sun, smog streaks), and the haze past the
    /// map cut off at the ridge so the sky keeps its colour. The port of terrainBase in lib/ubw-terrain.js
    /// and sky, stripsOf and hazeToRidge in lib/ubw-sky.js. The sky is drawn under the plates (by layer),
    /// so the far ridge's plates stand in front of it and three gears hang partly behind the ridge.
    /// Level shapes only, no facing.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class UbwTerrainGraphics
    {
        internal static readonly Material atlas = MaterialPool.MatFrom("RimArt/Trace/TerrainAtlas", ShaderDatabase.Transparent);
        internal static readonly Material skyMat = MaterialPool.MatFrom("RimArt/Trace/Sky", ShaderDatabase.Transparent);
        internal static readonly Color Base = new Color(0.07f, 0.045f, 0.035f), Mid = new Color(0.86f, 0.36f, 0.2f), Top = new Color(0.28f, 0.1f, 0.13f);
        internal static readonly Color SunFace = new Color(1f, 0.75f, 0.45f), Silhouette = new Color(0.12f, 0.05f, 0.05f), Smog = new Color(0.3f, 0.1f, 0.1f);
        public const float SkyH = 40f, RidgeGlow = 7f, SunUp = 5f, SunOut = 34f, HazeFar = 240f;
        public const int HazeRays = 360;
        private static readonly Mesh sunDisc = Disc(48, "UBW sun");

        /// <summary>The gears in the sky: x cells from the caster, up cells above the world's edge (negative: the lower part hangs behind the ridge), radius, cells above the ground (which sets where the shadow falls), degrees a second, teeth, the squash of a standing wheel seen from above and in front, haze toward the sky for the far ones.</summary>
        internal static readonly float[] GearX = { -36f, -15f, 3f, 17f, 33f, 48f, 30f, 52f, -50f }, GearUp = { 5f, -3f, 3.5f, -2f, 2.5f, -2.5f, 13f, 9.5f, 11f },
            GearR = { 6f, 9.5f, 5f, 7.5f, 5.5f, 8.5f, 4.5f, 6f, 4f }, GearH = { 30f, 36f, 30f, 32f, 28f, 44f, 46f, 50f, 40f },
            GearSpin = { 3.5f, -2.2f, 5f, -3.5f, 6f, 2.5f, -6f, -3f, 4f }, GearSquash = { 0.62f, 0.7f, 0.6f, 0.66f, 0.58f, 0.72f, 0.6f, 0.64f, 0.64f },
            GearHaze = { 0.3f, 0.05f, 0.35f, 0.1f, 0.25f, 0.08f, 0.45f, 0.4f, 0.5f };
        internal static readonly int[] GearTeeth = { 12, 14, 10, 12, 10, 14, 10, 12, 12 };

        private sealed class SkyMeshes
        {
            public Mesh Sky, Band;
            public Mesh[] Haze;
        }

        private static readonly Dictionary<UbwTerrain, SkyMeshes> skies = new Dictionary<UbwTerrain, SkyMeshes>();
        private static readonly Dictionary<int, UbwTerrain> terrains = new Dictionary<int, UbwTerrain>();
        private static readonly List<(UbwTerrain terrain, string sun, UbwTerrainBake bake)> bakes = new List<(UbwTerrain, string, UbwTerrainBake)>();

        /// <summary>The ground of the sketch's rules for a seed, made once.</summary>
        internal static UbwTerrain For(int seed)
        {
            if (!terrains.TryGetValue(seed, out UbwTerrain T))
            {
                T = UbwTerrain.Make(UbwGround.Default, seed);
                terrains[seed] = T;
            }
            return T;
        }

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

        /// <summary>Two strips along the ridge (the sky gradient from the ridge to SkyH above the world's edge, the warm band just above the ridge) and the haze rings cut at the ridge, made once per ground.</summary>
        private static SkyMeshes MeshesOf(UbwTerrain T)
        {
            if (skies.TryGetValue(T, out SkyMeshes made)) return made;
            float[] zs = T.RidgeSamples();
            float x0 = (float)T.RidgeX0, edge = (float)T.EdgeAt, top = edge + SkyH, step = (float)UbwTerrain.RidgeStep;
            var sky = new Builder("UBW sky " + T.Seed);
            var band = new Builder("UBW sky band " + T.Seed);
            var pts = new List<Vector2>(4);
            var uvs = new List<Vector2>(4);
            void Quad(Builder into, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                pts.Clear(); uvs.Clear();
                pts.Add(a); pts.Add(b); pts.Add(c); pts.Add(d);
                uvs.Add(ua); uvs.Add(ub); uvs.Add(uc); uvs.Add(ud);
                into.Poly(pts, uvs);
            }
            for (int i = 1; i < zs.Length; i++)
            {
                float xa = x0 + (i - 1) * step, xb = x0 + i * step, za = zs[i - 1], zb = zs[i];
                float va = Mathf.Clamp01((za - edge) / SkyH), vb = Mathf.Clamp01((zb - edge) / SkyH);
                Quad(sky, new Vector2(xa, za), new Vector2(xb, zb), new Vector2(xb, top), new Vector2(xa, top), new Vector2(0.5f, va), new Vector2(0.5f, vb), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                Quad(band, new Vector2(xa, za), new Vector2(xb, zb), new Vector2(xb, zb + RidgeGlow), new Vector2(xa, za + RidgeGlow), new Vector2(0.98f, 0.5f), new Vector2(0.98f, 0.5f), new Vector2(0.02f, 0.5f), new Vector2(0.02f, 0.5f));
            }
            made = new SkyMeshes { Sky = sky.Take("UBW sky " + T.Seed), Band = band.Take("UBW sky band " + T.Seed), Haze = new Mesh[8] };
            for (int k = 0; k < 8; k++)
            {
                float r0 = UbwWorldTiming.MapHalf + 2f + k * 2.4f;
                var haze = new Builder("UBW sky haze " + T.Seed + " " + k);
                for (int j = 0; j <= HazeRays; j++)
                {
                    float a = j / (float)HazeRays * Mathf.PI * 2f, ca = Mathf.Cos(a), sa = Mathf.Sin(a), t = 1f;
                    if (sa * HazeFar > edge)
                    {
                        t = edge / (sa * HazeFar);
                        t = Mathf.Max(t, (float)T.RidgeAt(ca * HazeFar * t) / (sa * HazeFar));
                    }
                    haze.Vertex(ca * r0, sa * r0, new Vector2(0.5f, 0.5f));
                    haze.Vertex(ca * HazeFar * t, sa * HazeFar * t, new Vector2(0.5f, 0.5f));
                    if (j == 0) continue;
                    int b = j * 2;
                    haze.Tri(b - 2, b, b - 1);
                    haze.Tri(b - 1, b, b + 1);
                }
                made.Haze[k] = haze.Take("UBW sky haze " + T.Seed + " " + k);
            }
            skies[T] = made;
            return made;
        }

        /// <summary>The haze past the map as eight rings, each ray ending where it meets the ridge, so the ground north of the map is hazed and the sky above the ridge is not.</summary>
        internal static void HazeToRidge(UbwTerrain T, Vector2 o, float alpha, float altitude)
        {
            if (alpha <= 0f) return;
            SkyMeshes m = MeshesOf(T);
            for (int k = 0; k < m.Haze.Length; k++) DrawMesh(m.Haze[k], o, altitude + k * 0.0001f, 1f, 1f, 0f, Fade(Haze, 0.1f * alpha), solid);
        }

        /// <summary>A gear turned by <paramref name="turn"/> radians and squashed on z: its body, a standing wheel seen from above and in front.</summary>
        private static Builder BodyMesh(string key, int teeth, float turn, float squash)
        {
            var (v, tri) = GearShape(teeth);
            Builder b = Scratch(key);
            float ct = Mathf.Cos(turn), st = Mathf.Sin(turn);
            for (int k = 0; k < v.Length; k++) b.Vertex(v[k].x * ct - v[k].y * st, (v[k].x * st + v[k].y * ct) * squash, new Vector2(0.5f, 0.5f));
            for (int k = 0; k < tri.Length; k += 3) b.Tri(tri[k], tri[k + 1], tri[k + 2]);
            return b;
        }

        /// <summary>
        /// The whole sky round <paramref name="o"/> at <paramref name="s"/>: strip, top, ridge band, sun, gears with their
        /// shadows (opacity as the world's gear shadows), smog. <paramref name="skyAltitude"/> is under the plates;
        /// the gear shadows go at <paramref name="shadowAltitude"/> + 0.001 + 0.0002 a gear, as the v1 gear shadows do.
        /// </summary>
        internal static void Sky(string key, UbwTerrain T, Vector2 o, float s, Vector2 sun, float gearOpacity, float skyAltitude, float shadowAltitude)
        {
            SkyMeshes m = MeshesOf(T);
            float edge = (float)T.EdgeAt;
            float L(int k) => skyAltitude + k * 0.0001f;
            Vector2 to = -sun.normalized;
            Sprite(new Vector2(o.x, o.y + edge + SkyH + 200f), 800f, 400f, Top, solid, L(0));
            DrawMesh(m.Sky, o, L(1), 1f, 1f, 0f, White, skyMat);
            DrawMesh(m.Band, o, L(2), 1f, 1f, 0f, Fade(Sunset, 0.45f), fadeMat);
            // The sun, low over the world's edge on its side of the sky.
            var sunAt = new Vector2(o.x + to.x * SunOut, o.y + edge + SunUp);
            Sprite(sunAt, 70f, 70f, Fade(Sunset, 0.2f), glow, L(3));
            Sprite(sunAt, 24f, 24f, Fade(SunFace, 0.55f), glow, L(4));
            DrawMesh(sunDisc, sunAt, L(5), 2.4f, 2.4f, 0f, Fade(WhiteHot, 0.95f), solid);
            // Smog: long thin streaks drifting east, darker with height.
            for (int k = 0; k < 7; k++)
            {
                float x = ((Rand(k * 7 + 1) - 0.5f) * 120f + s * (0.2f + 0.2f * Rand(k * 3)) + 60f) % 120f - 60f, up = 6f + k * 3.2f + Rand(k * 5) * 2f;
                Sprite(new Vector2(o.x + x, o.y + edge + up), 18f + Rand(k * 11) * 18f, 1.2f + Rand(k * 13) * 1.6f, Fade(Smog, 0.22f + 0.04f * k), soft, L(20 + k), (Rand(k * 17) - 0.5f) * 6f);
            }
            // The gears: bodies in the sky (the far ones hazed toward it), shadows on the ground.
            float along = Mathf.Atan2(sun.y, sun.x);
            for (int i = 0; i < GearX.Length; i++)
            {
                float turn = s * GearSpin[i] * D2R + i * 0.7f, dx = Mathf.Sin(s * 0.21f + i * 2f) * 0.6f, dz = Mathf.Cos(s * 0.17f + i) * 0.4f;
                var body = new Vector2(o.x + GearX[i] + dx, o.y + edge + GearUp[i] + dz);
                DrawMesh(BodyMesh(key + " body " + i, GearTeeth[i], turn, GearSquash[i]).Bake(), body, L(8 + i), GearR[i], GearR[i], 0f,
                    Fade(Color.Lerp(Silhouette, Mid, GearHaze[i] * 0.6f), 0.95f), solid);
                if (gearOpacity <= 0f) continue;
                Mesh shadow = SpunGear(key + " shadow " + i, GearTeeth[i], turn, along, 1.25f).Bake();
                var at = new Vector2(body.x + sun.x * GearH[i], body.y - Lift * GearH[i] + sun.y * GearH[i]);
                DrawMesh(shadow, at, shadowAltitude + 0.001f + i * 0.0002f, GearR[i] * 1.05f, GearR[i] * 1.05f, 0f, Fade(Black, gearOpacity * 0.35f), solid);
                DrawMesh(shadow, at, shadowAltitude + 0.0011f + i * 0.0002f, GearR[i], GearR[i], 0f, Fade(Black, gearOpacity), solid);
            }
        }
    }
}
