using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.UbwGraphics;
using C = RimArt.UbwCrest;

namespace RimArt
{
    /// <summary>What the camera shows: its centre on the ground and half its width and height, in cells.</summary>
    internal readonly struct UbwView
    {
        public readonly float Cx, Cz, HalfW, HalfH;

        public UbwView(float cx, float cz, float halfW, float halfH)
        {
            Cx = cx; Cz = cz; HalfW = halfW; HalfH = halfH;
        }

        /// <summary>The map camera now. It looks straight down, so its x and z are the middle of the screen and its orthographic size half the screen's height.</summary>
        public static UbwView Current()
        {
            Camera camera = Find.Camera;
            Vector3 at = camera.transform.position;
            float half = camera.orthographicSize;
            return new UbwView(at.x, at.z, half * camera.aspect, half);
        }
    }

    /// <summary>
    /// Everything of the world v4 that hangs on the camera: the backdrop behind the crest, drawn from the
    /// side (the sky with the low sun, clouds lit from below near it, smog and seven gears turning slower the
    /// bigger they are; far mountains, a middle and a near ridge, the plain between them with fourteen rows
    /// of swords shrinking toward the horizon; smoke bands drifting east over the crest and embers rising
    /// past it), and from v3 the faint gear shadows on the map, the camera's haze toward the top of the
    /// screen and the ash and embers in front of everything.
    ///
    /// Every part of the backdrop moves by its own share of the camera's pan (0 the sun, 0.06 the mountains,
    /// 0.45 the nearest row; up and down the horizon by 0.25), so panning north opens more plain and sky.
    /// The ridges and rows are baked once and drawn at a position from the camera each frame; only the
    /// gears, clouds, smoke and embers are rebuilt. The port of drawBackdrop, smokeBands and updraft in
    /// Tools/VfxLab/web/sketches/lib/ubw-crest.js and gearShadows, depthHaze and foreground in
    /// lib/ubw-horizon.js. All of it lies between the crack floor and the map's plates, but for the gear
    /// shadows, the haze and the foreground.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class UbwBackdropGraphics
    {
        internal static readonly Material skyMat = MaterialPool.MatFrom("RimArt/Trace/HorizonSky", ShaderDatabase.Transparent);
        internal static readonly Material groundMat = MaterialPool.MatFrom("RimArt/Trace/HorizonGround", ShaderDatabase.Transparent);
        internal static readonly Material depthMat = MaterialPool.MatFrom("RimArt/Trace/DepthHaze", ShaderDatabase.Transparent);

        // The sketch's colours (lib/ubw-horizon.js, lib/ubw-crest.js).
        internal static readonly Color Glow = new Color(1f, 0.89f, 0.67f), Horizon = new Color(1f, 0.62f, 0.3f), Mid = new Color(0.86f, 0.36f, 0.2f), High = new Color(0.5f, 0.17f, 0.16f);
        internal static readonly Color Top = new Color(0.24f, 0.08f, 0.11f), SunFace = new Color(1f, 0.96f, 0.88f), SunWarm = new Color(1f, 0.75f, 0.45f);
        internal static readonly Color Silhouette = new Color(0.12f, 0.05f, 0.05f), Smog = new Color(0.27f, 0.09f, 0.09f), RimLight = new Color(1f, 0.67f, 0.35f);
        internal static readonly Color CloudDark = new Color(0.36f, 0.13f, 0.14f), CloudWarm = new Color(0.6f, 0.26f, 0.2f), CloudLit = new Color(1f, 0.77f, 0.47f);
        internal static readonly Color RidgeEarth = new Color(0.34f, 0.22f, 0.16f), HazeFar = new Color(0.93f, 0.61f, 0.36f), Steel = new Color(0.6f, 0.58f, 0.6f), Spark = new Color(1f, 0.7f, 0.38f);
        internal static readonly Color Fog = new Color(0.89f, 0.56f, 0.35f), Ash = new Color(0.27f, 0.21f, 0.2f);
        /// <summary>The crack floor (lib/ubw-terrain.js Base) and the backstop's colour (FarEarth toward Haze by 0.45): the cover under the plates.</summary>
        internal static readonly Color Base = new Color(0.07f, 0.045f, 0.035f), Backstop = new Color(0.568f, 0.368f, 0.318f);
        internal const float KA = (float)C.KA, KE = (float)C.KE;
        private const float Step = 0.00005f;

        private static Mesh[] ridgeFill, ridgeRim, ridgeSwords, rows;
        /// <summary>The baked layers, each round its own origin (x from the caster, z from its line): ridge r's fill, rim and swords (null for the mountains), row k's swords.</summary>
        internal static Mesh RidgeFill(int r) { BakeLayers(); return ridgeFill[r]; }
        internal static Mesh RidgeRim(int r) { BakeLayers(); return ridgeRim[r]; }
        internal static Mesh RidgeSwords(int r) { BakeLayers(); return ridgeSwords[r]; }
        internal static Mesh RowSwords(int k) { BakeLayers(); return rows[k]; }
        /// <summary>The gears far first (by haze), as the sketch sorts them.</summary>
        private static readonly int[] gearOrder = Enumerable.Range(0, C.Gears.Length).OrderByDescending(i => C.Gears[i].Haze).ToArray();
        private static readonly Dictionary<long, (Vector2[] v, int[] tri)> shapes = new Dictionary<long, (Vector2[], int[])>();

        internal static Color Mix(Color a, Color b, float t) => new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, 1f);
        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        private static float Hash(int x, int y, int seed) => (float)UbwTerrain.Hash(x, y, seed);
        private static float Wrap(float v, float n) => ((v % n) + n) % n - n / 2f;

        /// <summary>The sky's colour e degrees over the horizon: glow, orange, red, dusk red, the dark top.</summary>
        internal static Color SkyAt(float e)
        {
            if (e < 6f) return Mix(Glow, Horizon, Clamp01(e / 6f));
            if (e < 16f) return Mix(Horizon, Mid, (e - 6f) / 10f);
            if (e < 38f) return Mix(Mid, High, (e - 16f) / 22f);
            return Mix(High, Top, Clamp01((e - 38f) / 40f));
        }

        // ---- the baked layers -----------------------------------------------------------------------------------

        /// <summary>The ridges (fill, rim, swords on the top) and the rows of swords, each round its own origin: x from the caster, z from its line.</summary>
        private static void BakeLayers()
        {
            if (rows != null) return;
            int nr = C.Ridges.Length;
            ridgeFill = new Mesh[nr];
            ridgeRim = new Mesh[nr];
            ridgeSwords = new Mesh[nr];
            for (int r = 0; r < nr; r++)
            {
                C.Ridge R = C.Ridges[r];
                Builder fill = new Builder("UBW backdrop ridge " + r), rim = new Builder("UBW backdrop ridge rim " + r), swords = new Builder("UBW backdrop ridge swords " + r);
                float low = (float)(R.Base - C.RidgeDepth);
                for (double x = -C.Span; x < C.Span; x += .5)
                {
                    float xa = (float)x, xb = (float)(x + .5), t0 = (float)(R.Base + R.H * C.Profile(R, x)), t1 = (float)(R.Base + R.H * C.Profile(R, x + .5));
                    UbwCrestBake.Poly(fill, new[] { new Vector2(xa, low), new Vector2(xa, t0), new Vector2(xb, t1), new Vector2(xb, low) });
                    UbwCrestBake.Poly(rim, new[] { new Vector2(xa, t0), new Vector2(xb, t1), new Vector2(xb, t1 - 0.05f), new Vector2(xa, t0 - 0.05f) });
                }
                if (R.Swords > 0)
                    for (int j = (int)Math.Floor(-C.Span * R.Swords); j < C.Span * R.Swords; j++)
                    {
                        if (UbwTerrain.Hash(j, r, 171) < .4) continue;
                        double x = (j + .7 * UbwTerrain.Hash(j, r, 172)) / R.Swords, top = R.Base + R.H * C.Profile(R, x);
                        double len = R.Tall * (.7 + .6 * UbwTerrain.Hash(j, r, 173)), lean = (UbwTerrain.Hash(j, r, 174) - .5) * .5;
                        UbwCrestBake.Stroke(swords, new Vector2((float)x, (float)(top - .03)), new Vector2((float)(x + Math.Sin(lean) * len), (float)(top + Math.Cos(lean) * len)), (float)Math.Max(.03, R.Tall * .16));
                    }
                ridgeFill[r] = fill.Take("UBW backdrop ridge " + r);
                ridgeRim[r] = rim.Take("UBW backdrop ridge rim " + r);
                ridgeSwords[r] = R.Swords > 0 ? swords.Take("UBW backdrop ridge swords " + r) : null;
            }
            var made = new Mesh[C.RowCount];
            for (int k = 0; k < C.RowCount; k++)
            {
                C.Row R = C.RowOf(k);
                var b = new Builder("UBW backdrop row " + k);
                for (int j = (int)Math.Floor(-C.Span / R.Gap); j < C.Span / R.Gap; j++)
                {
                    if (UbwTerrain.Hash(j, k, 175) < .3) continue;
                    double x = (j + .8 * UbwTerrain.Hash(j, k, 176)) * R.Gap, z = (UbwTerrain.Hash(j, k, 177) - .5) * R.Gap * .3;
                    double len = R.Tall * (.6 + .8 * UbwTerrain.Hash(j, k, 178)), lean = (UbwTerrain.Hash(j, k, 179) - .5) * .6;
                    UbwCrestBake.Stroke(b, new Vector2((float)x, (float)z), new Vector2((float)(x + Math.Sin(lean) * len), (float)(z + Math.Cos(lean) * len)), (float)Math.Max(.025, R.Tall * .14));
                }
                made[k] = b.Take("UBW backdrop row " + k);
            }
            rows = made;
        }

        // ---- gears ------------------------------------------------------------------------------------------------

        /// <summary>A gear of radius 1 as rings and spokes: the toothed rim, then per type its spokes, inner ring and hub. The port of gearShape.</summary>
        internal static (Vector2[] v, int[] tri) GearShape(int type, int teeth)
        {
            long id = type * 1000L + teeth;
            if (shapes.TryGetValue(id, out var made)) return made;
            var v = new List<Vector2>();
            var tri = new List<int>();
            void Ring(float r0, float r1, int n, bool toothed)
            {
                int b = v.Count;
                for (int j = 0; j < n; j++)
                {
                    float a = j / (float)n * Mathf.PI * 2f, outer = toothed ? (j % 4 == 1 || j % 4 == 2 ? 1f : 0.88f) : r1;
                    v.Add(new Vector2(Mathf.Cos(a) * outer, Mathf.Sin(a) * outer));
                    v.Add(new Vector2(Mathf.Cos(a) * r0, Mathf.Sin(a) * r0));
                    int o = b + j * 2, next = b + ((j + 1) % n) * 2;
                    tri.AddRange(new[] { o, next, o + 1, o + 1, next, next + 1 });
                }
            }
            void Spokes(int count, float r0, float r1, float w, float turn = 0f)
            {
                for (int k = 0; k < count; k++)
                {
                    float a = k / (float)count * Mathf.PI * 2f + turn, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                    int b = v.Count;
                    v.Add(new Vector2(cs * r0 - sn * w, sn * r0 + cs * w));
                    v.Add(new Vector2(cs * r1 - sn * w, sn * r1 + cs * w));
                    v.Add(new Vector2(cs * r1 + sn * w, sn * r1 - cs * w));
                    v.Add(new Vector2(cs * r0 + sn * w, sn * r0 - cs * w));
                    tri.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
                }
            }
            if (type == 0) { Ring(0.74f, 1f, teeth * 4, true); Spokes(6, 0.2f, 0.76f, 0.045f); Ring(0.1f, 0.26f, 48, false); }
            else if (type == 1) { Ring(0.8f, 1f, teeth * 4, true); Spokes(4, 0.66f, 0.82f, 0.05f, Mathf.PI / 4f); Ring(0.6f, 0.68f, 64, false); Spokes(4, 0.3f, 0.62f, 0.06f); Ring(0.14f, 0.34f, 48, false); }
            else { Ring(0.7f, 1f, teeth * 4, true); Spokes(3, 0.16f, 0.72f, 0.1f); Ring(0.08f, 0.22f, 40, false); }
            made = (v.ToArray(), tri.ToArray());
            shapes[id] = made;
            return made;
        }

        /// <summary>The gear turned by <paramref name="turn"/> radians, stretched by <paramref name="stretch"/> along <paramref name="along"/> (radians, a shadow along the low sun), then squashed on z (a wheel standing in the sky).</summary>
        private static Mesh GearAt(string key, int type, int teeth, float turn, float squash, float along = 0f, float stretch = 1f)
        {
            var (v, tri) = GearShape(type, teeth);
            Builder b = Scratch(key);
            float ct = Mathf.Cos(turn), st = Mathf.Sin(turn), ca = Mathf.Cos(along), sa = Mathf.Sin(along);
            for (int k = 0; k < v.Length; k++)
            {
                float x = v[k].x * ct - v[k].y * st, z = v[k].x * st + v[k].y * ct;
                if (stretch != 1f)
                {
                    float p = (x * ca + z * sa) * stretch, q = -x * sa + z * ca;
                    x = p * ca - q * sa;
                    z = p * sa + q * ca;
                }
                b.Vertex(x, z * squash, new Vector2(0.5f, 0.5f));
            }
            for (int k = 0; k < tri.Length; k += 3) b.Tri(tri[k], tri[k + 1], tri[k + 2]);
            return b.Bake();
        }

        // ---- the backdrop -----------------------------------------------------------------------------------------

        /// <summary>
        /// Everything behind the crest <paramref name="k"/> for the world round <paramref name="o"/> at
        /// <paramref name="s"/> seconds, seen by <paramref name="view"/>: the sky, the gears, the ridges and the plain
        /// with its rows, the smoke and the embers, and under the plates a cover in the crack floor's colour so the
        /// backdrop never shows through their cracks. <paramref name="edge"/> is the world square's half size;
        /// <paramref name="altitude"/> the lowest of the 41 steps used, over the crack floor.
        /// </summary>
        internal static void Draw(Vector2 o, float s, Vector2 sun, in UbwView view, UbwCrestBake k, Color tint, float edge, float altitude)
        {
            BakeLayers();
            float L(int i) => altitude + i * Step;
            float m = (float)C.Parallax, camX = view.Cx, dz = view.Cz - (o.y + (float)C.CamRef), width = 2f * view.HalfW + 10f, move = (float)C.HorizonMove;
            float Share(float p) => Mathf.Min(0.95f, p * m);
            float XAt(float p) => (1f - Share(p)) * (camX - o.x);
            float LineAt(float off, float p) => o.y + k.North + (float)C.Horizon + off + (1f - Share(p)) * dz;
            float Pv(float f) => move + (1f - move) * f;
            float horizonZ = LineAt(0f, move);
            float SkyX(float a, float p) => o.x + a * KA + XAt(p);
            float SkyZ(float e) => horizonZ + e * KE;
            float cover = o.y + k.North + (float)C.CoverAt;

            // The sky: the plane above it, the gradient, the sun's glow, clouds (bodies, then their lit undersides,
            // in three groups by how near the sun), the sun, the smog.
            Sprite(new Vector2(camX, horizonZ + (float)C.SkyCells + 200f), width + 400f, 400f, Top, solid, L(0));
            Sprite(new Vector2(camX, horizonZ + ((float)C.SkyCells - 1f) / 2f), width, (float)C.SkyCells + 1f, White, skyMat, L(1));
            float sunA = (float)C.SunAzimuth(sun.x, sun.y);
            var sunAt = new Vector2(SkyX(sunA, 0f), SkyZ((float)C.SunUp));
            Sprite(sunAt, 30f * KE * 2f, 30f * KE * 2f, Fade(SunWarm, 0.38f), glow, L(2));
            Builder[] bodies = { Scratch("UBW clouds 0"), Scratch("UBW clouds 1"), Scratch("UBW clouds 2") };
            Builder[] lit = { Scratch("UBW cloud light 0"), Scratch("UBW cloud light 1"), Scratch("UBW cloud light 2") };
            foreach (Puff q in CloudPuffs(s, sunA))
            {
                float x = SkyX(q.A, 0.04f), z = SkyZ(q.E), w = q.R * KE * 2.6f, h = q.R * KE * 2f;
                bodies[q.G].Quad(x, z, w, h, 0f);
                lit[q.G].Quad(x, z - 0.9f * KE - h * 0.12f, w * 0.8f, h * 0.75f, 0f);
            }
            for (int g = 0; g < 3; g++) UbwGraphics.Draw(bodies[g], Vector2.zero, L(3) + g * 0.00001f, Fade(Mix(CloudDark, CloudWarm, g / 2f), 0.5f), soft);
            for (int g = 0; g < 3; g++) UbwGraphics.Draw(lit[g], Vector2.zero, L(4) + g * 0.00001f, Fade(CloudLit, 0.12f + 0.19f * g), glow);
            Sprite(sunAt, 8f * KE * 2f, 8f * KE * 2f, Fade(SunWarm, 0.6f), glow, L(5));
            Sprite(sunAt, 1.6f * KE * 2f, 1.6f * KE * 2f, Fade(SunFace, 0.97f), soft, L(6));
            Builder smog = Scratch("UBW smog");
            foreach (Puff q in SmogBands(s)) smog.Quad(SkyX(q.A, 0.06f), SkyZ(q.E), q.W * KE * 2f, q.H * KE * 2f, 0f);
            UbwGraphics.Draw(smog, Vector2.zero, L(7), Fade(Smog, 0.34f), soft);

            // The gears, the hazier (further) first: a warm rim toward the sun, then the body. Bigger turns slower.
            float toSunX = sunA > 0f ? 1f : sunA < 0f ? -1f : 1f;
            for (int i = 0; i < gearOrder.Length; i++)
            {
                C.SkyGear g = C.Gears[gearOrder[i]];
                float R = (float)g.R * KE, x = SkyX((float)g.A, (float)g.P), z = SkyZ((float)g.E), spin = Math.Sign(g.Spin) * (float)C.Spin * 14f / (float)g.R;
                Mesh gear = GearAt("UBW sky gear " + i, g.Type, g.Teeth, (s * spin + (float)g.A * 5f) * D2R, 0.8f);
                DrawMesh(gear, new Vector2(x + toSunX * R * 0.02f, z + R * 0.012f), L(8 + i * 2), R, R, 0f, Fade(RimLight, 0.5f * (1f - (float)g.Haze)), solid);
                DrawMesh(gear, new Vector2(x, z), L(9 + i * 2), R, R, 0f, Fade(Mix(Silhouette, SkyAt((float)(g.E + g.R * .3)), (float)g.Haze), 0.97f), solid);
            }

            // The ridges and the plain, far to near: mountains, the plain's colour, rows, the middle ridge, rows, the
            // near ridge, rows. The plain runs from the cover line to the horizon.
            void Ridge(int r, int at)
            {
                C.Ridge R = C.Ridges[r];
                var p = new Vector2(o.x + XAt((float)R.P), LineAt(0f, Pv((float)R.P)));
                Color col = Mix(RidgeEarth, HazeFar, (float)R.Haze);
                DrawMesh(ridgeFill[r], p, L(at), 1f, 1f, 0f, col, solid);
                DrawMesh(ridgeRim[r], p, L(at) + 0.00001f, 1f, 1f, 0f, Fade(Mix(col, RimLight, 0.45f), 0.45f), solid);
                if (ridgeSwords[r] != null) DrawMesh(ridgeSwords[r], p, L(at) + 0.00002f, 1f, 1f, 0f, Mix(Mix(Steel, Silhouette, 0.5f), HazeFar, (float)R.Haze * 0.9f), solid);
            }
            void Rows(int k0, int k1, int at)
            {
                for (int q = k0; q < k1; q++)
                {
                    C.Row R = C.RowOf(q);
                    float z = LineAt(-(float)R.D, Pv((float)R.P));
                    if (z + R.Tall < cover) continue;
                    DrawMesh(rows[q], new Vector2(o.x + XAt((float)R.P), z), L(at) + q * 0.000002f, 1f, 1f, 0f, Mix(Mix(Steel, Silhouette, 0.35f), HazeFar, (float)R.Haze), solid);
                }
            }
            Ridge(0, 22);
            if (horizonZ > cover) Sprite(new Vector2(camX, (horizonZ + cover) / 2f), width, horizonZ - cover, tint, groundMat, L(24));
            Rows(0, 5, 25);
            Ridge(1, 26);
            Rows(5, 9, 27);
            Ridge(2, 28);
            Rows(9, C.RowCount, 29);

            // Motion in the drop behind the crest: smoke bands drifting east (far to near), then embers rising from
            // behind the crest into the sky.
            SmokeBands(o, s, view, k, L(30));
            Updraft(o, s, view, k, C.Updraft, L(34));

            // The cover: the crack floor's colour from below the screen to the cover line, over the world's square
            // (the backstop's colour outside it).
            float bottom = Mathf.Min(view.Cz - view.HalfH - 4f, cover - 1f), h2 = cover - bottom, x0 = camX - width / 2f, x1 = camX + width / 2f;
            float w0 = Mathf.Max(x0, o.x - edge), w1 = Mathf.Min(x1, o.x + edge);
            if (w1 > w0) Sprite(new Vector2((w0 + w1) / 2f, bottom + h2 / 2f), w1 - w0, h2, Base, solid, L(40));
            if (w0 > x0) Sprite(new Vector2((x0 + w0) / 2f, bottom + h2 / 2f), w0 - x0, h2, Backstop, solid, L(40));
            if (x1 > w1) Sprite(new Vector2((w1 + x1) / 2f, bottom + h2 / 2f), x1 - w1, h2, Backstop, solid, L(40));
        }

        /// <summary>A thing of the sky in degrees: centre azimuth A and elevation E; R a cloud puff's radius and G how lit it is (0 to 2, by how near the sun); W, H a smog band's half sizes.</summary>
        internal struct Puff
        {
            public float A, E, R, W, H;
            public int G;
        }

        private static readonly List<Puff> puffs = new List<Puff>(), bands = new List<Puff>();

        /// <summary>The clouds' puffs at s: 20 clouds of 8 to 13 puffs drifting east, lit by how near the sun at azimuth sunA they are (lib/ubw-horizon.js cloudPuffs).</summary>
        internal static List<Puff> CloudPuffs(float s, float sunA)
        {
            puffs.Clear();
            for (int i = 0; i < 20; i++)
            {
                float a = (Hash(i, 1, 51) - 0.5f) * 180f + s * (0.35f + 0.7f * Hash(i, 2, 51)), e = 9f + Mathf.Pow(Hash(i, 3, 51), 0.8f) * 58f;
                float near = Clamp01(1f - Mathf.Abs(a - sunA) / 80f) * Clamp01(1f - (e - 8f) / 50f);
                int g = Mathf.Min(2, Mathf.FloorToInt(near * 3f)), n = 8 + Mathf.FloorToInt(Hash(i, 4, 51) * 6f);
                for (int q = 0; q < n; q++)
                    puffs.Add(new Puff { A = a + (Hash(i * 16 + q, 5, 51) - 0.5f) * 18f, E = e + (Hash(i * 16 + q, 6, 51) - 0.35f) * 5f, R = 3.5f + Hash(i * 16 + q, 7, 51) * 5.5f, G = g });
            }
            return puffs;
        }

        /// <summary>The smog's 8 bands at s, drifting east (lib/ubw-horizon.js smogBands).</summary>
        internal static List<Puff> SmogBands(float s)
        {
            bands.Clear();
            for (int i = 0; i < 8; i++)
                bands.Add(new Puff { A = (Hash(i, 1, 53) - 0.5f) * 170f + s * (0.8f + 1.4f * Hash(i, 2, 53)), E = 3f + i * 2.4f + Hash(i, 3, 53) * 2f, W = 28f + 34f * Hash(i, 4, 53), H = 0.8f + 1.2f * Hash(i, 5, 53) });
            return bands;
        }

        /// <summary>A screen quad: centre x, z and size w, h in cells.</summary>
        internal struct Quad4
        {
            public float X, Z, W, H;
        }

        /// <summary>Band j's smoke puffs (bodies and their lit tops) at s: 7 each, 9 to 23 cells wide, drifting east, wrapping round the view.</summary>
        internal static void SmokeQuads(Vector2 o, float s, in UbwView view, UbwCrestBake k, int j, List<Quad4> body, List<Quad4> top)
        {
            body.Clear();
            top.Clear();
            float camX = view.Cx, dz = view.Cz - (o.y + (float)C.CamRef), W = 2f * view.HalfW + 10f + 30f, move = (float)C.HorizonMove;
            float Share(float p) => Mathf.Min(0.95f, p * (float)C.Parallax);
            float p = move + (1f - move) * SmokeP[j], z0 = o.y + k.North + k.MeanTop + SmokeUp[j] + (1f - Share(p)) * dz;
            for (int i = 0; i < 7; i++)
            {
                float w = 9f + 14f * Hash(i, j, 181), h = 0.5f + 0.7f * Hash(i, j, 182);
                // Across, the band's own share of the pan; up and down, the horizon's share added (as the sketch).
                float x = camX + Wrap(Hash(i, j, 183) * W + SmokeV[j] * s - Share(SmokeP[j]) * (camX - o.x), W), z = z0 + (Hash(i, j, 184) - 0.5f) * 0.8f + 0.15f * Mathf.Sin(s * 0.3f + i + j);
                body.Add(new Quad4 { X = x, Z = z, W = w, H = h });
                top.Add(new Quad4 { X = x, Z = z + h * 0.22f, W = w * 0.85f, H = h * 0.45f });
            }
        }

        /// <summary>The smoke bands' parallax, drift east (cells a second) and height over the crest's mean top, far to near.</summary>
        private static readonly float[] SmokeP = { 0.35f, 0.5f, 0.7f }, SmokeV = { 0.3f, 0.6f, 1f }, SmokeUp = { 1.5f, 0.9f, 0.35f };

        internal static Color SmokeColour(int j) => Fade(Smog, (float)C.Smoke * (0.75f + 0.15f * j));
        internal static Color SmokeLight => Fade(CloudLit, (float)C.Smoke * 0.22f);
        internal static Color UpdraftColour(int b) => Fade(Spark, 0.8f * (b + 1) / 4f);

        /// <summary><paramref name="count"/> embers rising from just under the crest's top at s, in four lists by brightness.</summary>
        internal static void UpdraftQuads(Vector2 o, float s, in UbwView view, UbwCrestBake k, int count, List<Quad4>[] buckets)
        {
            foreach (List<Quad4> b in buckets) b.Clear();
            float camX = view.Cx, W = 2f * view.HalfW + 10f + 8f;
            for (int i = 0; i < count; i++)
            {
                float T = 3.5f + 2f * Hash(i, 1, 161), t = s + Hash(i, 2, 161) * T;
                int cycle = Mathf.FloorToInt(t / T);
                float age = t - cycle * T;
                float x = camX + Wrap(Hash(i + cycle * 97, 3, 161) * W - 0.8f * (camX - o.x), W) + 0.25f * Mathf.Sin(s * 0.9f + i) + age * (Hash(i, 4, 161) - 0.3f) * 0.5f;
                float z = o.y + k.TopZ(x - o.x) - 0.35f + age * (0.8f + 0.7f * Hash(i, 5, 161)), a = Clamp01(age / 0.4f) * Clamp01((1f - age / T) / 0.45f);
                float r = (0.08f + 0.14f * Hash(i, 6, 161)) * (1f + 0.3f * Mathf.Sin(s * 9f + i * 3));
                if (a > 0.02f) buckets[Mathf.Min(3, Mathf.FloorToInt(a * 4f))].Add(new Quad4 { X = x, Z = z, W = r * 2f, H = r * 2f });
            }
        }

        private static readonly List<Quad4> smokeBody = new List<Quad4>(), smokeTop = new List<Quad4>();
        private static readonly List<Quad4>[] updraftLists = { new List<Quad4>(), new List<Quad4>(), new List<Quad4>(), new List<Quad4>() };

        /// <summary>Three bands of smoke puffs over the crest's mean top (0.35, 0.9 and 1.5 cells up, far to near), drifting east at 0.3, 0.6 and 1 cells a second, each moving up and down by its own share of the pan.</summary>
        private static void SmokeBands(Vector2 o, float s, in UbwView view, UbwCrestBake k, float altitude)
        {
            for (int j = 0; j < 3; j++)
            {
                SmokeQuads(o, s, view, k, j, smokeBody, smokeTop);
                Builder body = Scratch("UBW smoke " + j), top = Scratch("UBW smoke light " + j);
                foreach (Quad4 q in smokeBody) body.Quad(q.X, q.Z, q.W, q.H, 0f);
                foreach (Quad4 q in smokeTop) top.Quad(q.X, q.Z, q.W, q.H, 0f);
                UbwGraphics.Draw(body, Vector2.zero, altitude + j * Step, SmokeColour(j), soft);
                UbwGraphics.Draw(top, Vector2.zero, altitude + j * Step + 0.00001f, SmokeLight, glow);
            }
        }

        /// <summary><paramref name="count"/> embers rising from just under the crest's top into the sky, 3.5 to 5.5 s each, in four buckets by brightness.</summary>
        private static void Updraft(Vector2 o, float s, in UbwView view, UbwCrestBake k, int count, float altitude)
        {
            if (count <= 0) return;
            UpdraftQuads(o, s, view, k, count, updraftLists);
            for (int b = 0; b < 4; b++)
            {
                Builder bucket = Scratch("UBW updraft " + b);
                foreach (Quad4 q in updraftLists[b]) bucket.Quad(q.X, q.Z, q.W, q.H, 0f);
                UbwGraphics.Draw(bucket, Vector2.zero, altitude + b * 0.00001f, UpdraftColour(b), glow);
            }
        }

        // ---- on the map and in front --------------------------------------------------------------------------------

        /// <summary>Gear shadow i of three at s: its mesh (spoked, stretched 1.3 along the low sun), where it lies and its radius; kept south of the map edge, <paramref name="north"/> cells north of the caster.</summary>
        internal static Mesh GearShadow(int i, Vector2 o, float s, Vector2 sun, int north, out Vector2 at, out float r)
        {
            r = 5f + i * 1.2f;
            at = new Vector2(o.x + Mathf.Sin(s * 0.1f + i * 2.4f) * 10f - 4f + i * 12f, o.y + Mathf.Min(-8f + i * 7f, north - r * 1.4f));
            return GearAt("UBW gear shadow " + i, 0, 14, s * 0.25f + i, 1f, Mathf.Atan2(sun.y, sun.x), 1.3f);
        }

        /// <summary>Three gears' shadows sweeping the map, faint (opacity).</summary>
        internal static void GearShadows(Vector2 o, float s, Vector2 sun, float opacity, int north, float altitude)
        {
            if (opacity <= 0f) return;
            for (int i = 0; i < 3; i++)
            {
                Mesh gear = GearShadow(i, o, s, sun, north, out Vector2 at, out float r);
                DrawMesh(gear, at, altitude + 0.001f + i * 0.0002f, r, r, 0f, Fade(Black, opacity), solid);
            }
        }

        /// <summary>The camera's haze as a screen quad: from the bottom of the screen up to the crest's mean top (<paramref name="top"/> cells north of the caster), and its opacity at the top for <paramref name="most"/>. False when none of it is on screen.</summary>
        internal static bool DepthHazeQuad(Vector2 o, float top, in UbwView view, float most, out Quad4 quad, out float alpha)
        {
            float bottom = view.Cz - view.HalfH, upTo = Mathf.Min(view.Cz + view.HalfH, o.y + top), h = upTo - bottom;
            quad = new Quad4 { X = view.Cx, Z = (upTo + bottom) / 2f, W = 2f * view.HalfW + 4f, H = h };
            alpha = most * Mathf.Pow(Mathf.Max(0f, h) / (2f * view.HalfH), 1.7f);
            return most > 0f && h > 0f;
        }

        /// <summary>The camera's haze: none at the bottom of the screen, up to <paramref name="most"/> at the top, up to the crest's mean top; over the ground and the swords, under the pawns.</summary>
        internal static void DepthHaze(Vector2 o, float top, in UbwView view, float most, float altitude)
        {
            if (!DepthHazeQuad(o, top, view, most, out Quad4 q, out float alpha)) return;
            Sprite(new Vector2(q.X, q.Z), q.W, q.H, Fade(Fog, alpha), depthMat, altitude);
        }

        private static readonly List<Quad4> ashList = new List<Quad4>(), frontEmbers = new List<Quad4>();

        /// <summary>The ash and the embers in front at s: 30 puffs fixed to the screen, moved 1.45 times the camera's pan, drifting up.</summary>
        internal static void ForegroundQuads(Vector2 o, float s, in UbwView view, List<Quad4> ash, List<Quad4> embers)
        {
            ash.Clear();
            embers.Clear();
            float W = 2f * view.HalfW + 8f, H = 2f * view.HalfH + 8f;
            for (int i = 0; i < 30; i++)
            {
                float x = view.Cx + Wrap(Hash(i, 1, 61) * W - 1.45f * (view.Cx - o.x) + s * (Hash(i, 2, 61) - 0.3f) * 1.2f, W);
                float z = view.Cz + Wrap(Hash(i, 3, 61) * H - 1.45f * (view.Cz - o.y) + s * (0.5f + Hash(i, 4, 61) * 1.1f), H);
                float r = (0.22f + 0.4f * Hash(i, 5, 61)) * (1f + 0.15f * Mathf.Sin(s * 2f + i));
                (Hash(i, 6, 61) < 0.45f ? ash : embers).Add(new Quad4 { X = x, Z = z, W = r * 2f, H = r * 2f });
            }
        }

        /// <summary>Ash and embers in front of everything: fixed to the screen, moved 1.45 times the camera's pan (so they read as nearer the camera than the ground), drifting up.</summary>
        internal static void Foreground(Vector2 o, float s, in UbwView view, float alpha)
        {
            if (alpha <= 0f) return;
            ForegroundQuads(o, s, view, ashList, frontEmbers);
            Builder ash = Scratch("UBW ash"), embers = Scratch("UBW front embers");
            foreach (Quad4 q in ashList) ash.Quad(q.X, q.Z, q.W, q.H, 0f);
            foreach (Quad4 q in frontEmbers) embers.Quad(q.X, q.Z, q.W, q.H, 0f);
            UbwGraphics.Draw(ash, Vector2.zero, Overhead + 0.08f, Fade(Ash, 0.3f * alpha), soft);
            UbwGraphics.Draw(embers, Vector2.zero, Overhead + 0.081f, Fade(Spark, 0.34f * alpha), glow);
        }
    }
}
