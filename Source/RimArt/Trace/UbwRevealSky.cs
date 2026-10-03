using System;
using System.Collections.Generic;
using UnityEngine;
using static RimArt.VfxMath;
using B = RimArt.UbwBackdropGraphics;
using C = RimArt.UbwCrest;

namespace RimArt
{
    /// <summary>
    /// Everything the world v4 works out before it draws, for the reveal shot: the caster's plate ground and
    /// crest under the low sun, the field of swords round the landing spots, and the game view the shot blends
    /// into (36 cells tall, 7 north of the caster). All of it relative to the caster; the shot draws it at the
    /// caster's place. The port of world() in Tools/VfxLab/web/sketches/lib/ubw-reveal.js.
    /// </summary>
    internal sealed class UbwRevealScene
    {
        public readonly int North = C.North;
        public Vector2 Sun;
        public float Strength = 0.32f;
        public Color Tint;
        /// <summary>The game view the shot blends into, from the caster.</summary>
        public UbwView View;
        public UbwTerrain T;
        public UbwTerrainBake Ground;
        public UbwCrestBake K;
        public List<UbwSword> Swords;
        public UbwWeaponSet Set;
        /// <summary>The landing spots, from the caster: no sword stands over one; the shot's figures stand there.</summary>
        public UbwXZ[] Keep;
        public float Dz, CamX, HorizonZ, Cover, SunA;
        /// <summary>The caster's eye: the sky's dome, sprites and gears stand round it.</summary>
        public Vector3 Eye = new Vector3(0f, UbwRevealSky.EyeHeight, 0f);
        /// <summary>What this scene has built (the 3D meshes), made once.</summary>
        public readonly Dictionary<string, object> Made = new Dictionary<string, object>();

        public float Share(float p) => Mathf.Min(0.95f, p * (float)C.Parallax);
        public float SkyX(float a, float p) => a * B.KA + (1f - Share(p)) * CamX;
        public float SkyZ(float e) => HorizonZ + e * B.KE;

        private static readonly List<(string key, UbwRevealScene scene)> scenes = new List<(string, UbwRevealScene)>();

        /// <summary>The scene for these landing spots, this low sun (the shadow vector per cell of height) and this screen shape; the last two are kept.</summary>
        public static UbwRevealScene For(IList<UbwXZ> keep, Vector2 sun, float aspect)
        {
            string key = sun.x.ToString("0.000") + "," + sun.y.ToString("0.000") + "|" + aspect.ToString("0.000") + "|";
            for (int i = 0; i < keep.Count; i++) key += keep[i].X.ToString("0.###") + "," + keep[i].Z.ToString("0.###") + ";";
            for (int i = 0; i < scenes.Count; i++) if (scenes[i].key == key) return scenes[i].scene;
            UbwCrestWorld world = UbwCrestWorld.For(sun);
            float half = UbwRevealTiming.CellsTall / 2f;
            var W = new UbwRevealScene
            {
                Sun = sun, Tint = Color.Lerp(UbwGraphics.White, UbwGraphics.Tint, (float)UbwField.Twilight),
                View = new UbwView(0f, UbwRevealTiming.GameNorth, half * aspect, half),
                T = world.Terrain.Terrain, Ground = world.Terrain, K = world.Crest, Set = UbwGraphics.Set, Keep = new UbwXZ[keep.Count],
            };
            for (int i = 0; i < keep.Count; i++) W.Keep[i] = keep[i];
            W.Swords = UbwField.Make(UbwField.CrestLook, W.Keep, W.Set.Weapons, W.T.HeightAt);
            W.CamX = W.View.Cx;
            W.Dz = W.View.Cz - (float)C.CamRef;
            W.HorizonZ = W.North + (float)C.Horizon + (1f - W.Share((float)C.HorizonMove)) * W.Dz;
            W.Cover = W.North + (float)C.CoverAt;
            W.SunA = (float)C.SunAzimuth(sun.x, sun.y);
            scenes.Add((key, W));
            if (scenes.Count > 2) scenes.RemoveAt(0);
            return W;
        }

        public T Get<T>(string key, Func<T> make) where T : class
        {
            if (Made.TryGetValue(key, out object m)) return (T)m;
            T made = make();
            Made[key] = made;
            return made;
        }
    }

    /// <summary>
    /// The sky, the plain and the backdrop of the world v4 in 3D, for the reveal shot (lib/ubw-reveal.js drawSky,
    /// plain; lib/ubw-crest.js backdrop3):
    ///   - the sky: a dome 5000 cells out with the gradient by elevation; the sun, clouds and smog as sprites facing
    ///     the caster's eye 4600 cells out; the seven gears as wheels 3000 cells out facing the eye, their lower
    ///     parts behind the ground. In 3D a thing of the sky is r degrees across; in the game view it keeps v4's shape;
    ///   - the plain: a grid from 70 cells south of the edge to 4000 north of it, hazing over hundreds of cells,
    ///     with a second copy in v4's own band of plain faded in with the blend;
    ///   - the backdrop stood up: an eye 9 cells over the caster sees each of v4's side-view parts where v4 draws it,
    ///     so the near ridge is a wall 81 cells north, the middle one 172, the mountains 516, and the rows of swords
    ///     stand on the plain from 27 to 480 cells north, each in rings that rise as the fire gets there.
    /// Every vertex keeps v4's drawing as its game position. The shot always looks north.
    /// </summary>
    internal static class UbwRevealSky
    {
        public const float EyeHeight = 1.7f, DomeR = 5000f, SpriteR = 4600f, GearR = 3000f, PlainFar = 4000f, PlainSouth = -70f;
        /// <summary>The backdrop's eye stands BackEye cells over the caster; rows stand no further than RowFar of the mountains' distance (near the horizon they would go past them); the plain hazes over HazeAt cells.</summary>
        public const float BackEye = 9f, RowFar = 0.93f, HazeAt = 420f;
        private const float D2R = Mathf.Deg2Rad;

        // ---- the sky -----------------------------------------------------------------------------------------------

        private static Vector3 Dir(float a, float e) => new Vector3(Mathf.Sin(a * D2R) * Mathf.Cos(e * D2R), Mathf.Sin(e * D2R), Mathf.Cos(a * D2R) * Mathf.Cos(e * D2R));

        /// <summary>A point dist cells from the eye toward azimuth a, elevation e, moved dr across and du up on the plane facing the eye.</summary>
        private static Vector3 SkyPoint(UbwRevealScene W, float a, float e, float dist, float dr = 0f, float du = 0f)
        {
            float A = a * D2R, E = e * D2R;
            var right = new Vector3(Mathf.Cos(A), 0f, -Mathf.Sin(A));
            var up = new Vector3(-Mathf.Sin(A) * Mathf.Sin(E), Mathf.Cos(E), -Mathf.Cos(A) * Mathf.Sin(E));
            return W.Eye + Dir(a, e) * dist + right * dr + up * du;
        }

        /// <summary>A sprite of the sky: a, e and its size w, h in degrees in 3D; its game quad gx, gz, gw, gh.</summary>
        private struct SkySprite
        {
            public float A, E, W, H, Gx, Gz, Gw, Gh;
        }

        private static readonly UbwBuilder3 scratch = new UbwBuilder3();

        private static UbwMesh3 Sprites(UbwRevealScene W, string key, List<SkySprite> items, float dist)
        {
            scratch.Clear();
            foreach (SkySprite q in items)
            {
                float hw = dist * Mathf.Tan(q.W / 2f * D2R), hh = dist * Mathf.Tan(q.H / 2f * D2R);
                int b = scratch.Count;
                for (int k = 0; k < 4; k++)
                {
                    float x = k < 2 ? -0.5f : 0.5f, z = k == 1 || k == 2 ? 0.5f : -0.5f;
                    scratch.Vertex(SkyPoint(W, q.A, q.E, dist, x * 2f * hw, z * 2f * hh), new Vector2(q.Gx + x * q.Gw, q.Gz + z * q.Gh), new Vector2(x + 0.5f, z + 0.5f));
                }
                scratch.Triangle(b, b + 1, b + 2);
                scratch.Triangle(b, b + 2, b + 3);
            }
            return scratch.Into(Live(W, key));
        }

        /// <summary>A mesh of this scene rebuilt each frame (the sky's moving parts), kept by key.</summary>
        internal static UbwMesh3 Live(UbwRevealScene W, string key) => W.Get(key, () => new UbwMesh3(key));

        /// <summary>The dome: v4's gradient by elevation, 120 degrees either side of north, from 8 below the horizon to the top.</summary>
        private static UbwMesh3 Dome(UbwRevealScene W) => W.Get("dome", () =>
        {
            var b = new UbwBuilder3();
            float[] E = { -8, -4, -2, -1, 0, 1, 2, 3, 4, 6, 8, 10, 13, 16, 20, 25, 30, 36, 44, 54, 66, 80, 89 };
            const int cols = 41;
            for (int j = 0; j < E.Length; j++)
                for (int i = 0; i < cols; i++)
                {
                    float a = -120f + 240f * i / (cols - 1);
                    b.Vertex(SkyPoint(W, a, E[j], DomeR), new Vector2(a * B.KA, W.SkyZ(E[j])), new Vector2(0.5f, Mathf.Clamp((E[j] * B.KE + 1f) / ((float)C.SkyCells + 1f), 0.004f, 0.996f)));
                    if (i == 0 || j == 0) continue;
                    int n = j * cols + i;
                    b.Triangle(n - cols - 1, n - 1, n);
                    b.Triangle(n - cols - 1, n, n - cols);
                }
            return b.Take("UBW reveal dome");
        });

        /// <summary>A gear of the sky turned by <paramref name="turn"/>: its 3D wheel (radius r degrees, facing the eye) and its game drawing (v4's, squashed 0.8), moved by (dr, du) in 3D and (dgx, dgz) on the game screen: the rim light.</summary>
        private static UbwMesh3 Gear(UbwRevealScene W, string key, in C.SkyGear g, float turn, float dr, float du, float dgx, float dgz)
        {
            var (v, tri) = B.GearShape(g.Type, g.Teeth);
            float ct = Mathf.Cos(turn), st = Mathf.Sin(turn), R3 = GearR * Mathf.Tan((float)g.R * D2R), R = (float)g.R * B.KE;
            float gx = W.SkyX((float)g.A, (float)g.P) + dgx, gz = W.SkyZ((float)g.E) + dgz;
            scratch.Clear();
            for (int k = 0; k < v.Length; k++)
            {
                float x = v[k].x * ct - v[k].y * st, z = v[k].x * st + v[k].y * ct;
                scratch.Vertex(SkyPoint(W, (float)g.A, (float)g.E, GearR, x * R3 + dr, z * R3 + du), new Vector2(gx + x * R, gz + z * R * 0.8f), new Vector2(0.5f, 0.5f));
            }
            for (int k = 0; k < tri.Length; k += 3) scratch.Triangle(tri[k], tri[k + 1], tri[k + 2]);
            return scratch.Into(Live(W, key));
        }

        private static readonly List<SkySprite>[] bodies = { new List<SkySprite>(), new List<SkySprite>(), new List<SkySprite>() }, lit = { new List<SkySprite>(), new List<SkySprite>(), new List<SkySprite>() };
        private static readonly List<SkySprite> one = new List<SkySprite>(), smog = new List<SkySprite>();
        private static int[] gearOrder;

        /// <summary>The sky at world time s, in v4's order: dome, the sun's wide glow, cloud bodies, their lit undersides, the sun's glow and face, the smog, the gears (hazier first; a warm rim toward the sun, then the body).</summary>
        internal static void Sky(UbwShot shot, UbwRevealScene W, Vector3 at, float s)
        {
            shot.Draw(Dome(W), UbwGraphics.White, B.skyMat, at, UbwDepth.NoWrite);
            float sunGx = W.SkyX(W.SunA, 0f), sunGz = W.SkyZ((float)C.SunUp);
            SkySprite SunAt(float r, float w) => new SkySprite { A = W.SunA, E = (float)C.SunUp, W = 2f * r, H = 2f * r, Gx = sunGx, Gz = sunGz, Gw = w, Gh = w };
            void Single(string key, SkySprite q, float dist, Color colour, Material mat, UbwDepth depth)
            {
                one.Clear();
                one.Add(q);
                shot.Draw(Sprites(W, key, one, dist), colour, mat, at, depth);
            }
            Single("sun glow", SunAt(30f, 30f * B.KE * 2f), SpriteR, VfxDraw.Fade(B.SunWarm, 0.38f), VfxDraw.glow, UbwDepth.NoWrite);
            foreach (List<SkySprite> l in bodies) l.Clear();
            foreach (List<SkySprite> l in lit) l.Clear();
            foreach (B.Puff q in B.CloudPuffs(s, W.SunA))
            {
                float gx = W.SkyX(q.A, 0.04f), gz = W.SkyZ(q.E), gw = q.R * B.KE * 2.6f, gh = q.R * B.KE * 2f;
                bodies[q.G].Add(new SkySprite { A = q.A, E = q.E, W = 2f * q.R, H = 2f * q.R, Gx = gx, Gz = gz, Gw = gw, Gh = gh });
                lit[q.G].Add(new SkySprite { A = q.A, E = q.E - 0.9f - 0.24f * q.R, W = 1.6f * q.R, H = 1.5f * q.R, Gx = gx, Gz = gz - 0.9f * B.KE - gh * 0.12f, Gw = gw * 0.8f, Gh = gh * 0.75f });
            }
            for (int g = 0; g < 3; g++) shot.Draw(Sprites(W, "clouds " + g, bodies[g], SpriteR - 10f), VfxDraw.Fade(B.Mix(B.CloudDark, B.CloudWarm, g / 2f), 0.5f), VfxDraw.soft, at, UbwDepth.NoWrite);
            for (int g = 0; g < 3; g++) shot.Draw(Sprites(W, "cloud light " + g, lit[g], SpriteR - 20f), VfxDraw.Fade(B.CloudLit, 0.12f + 0.19f * g), VfxDraw.glow, at, UbwDepth.NoWrite);
            Single("sun", SunAt(8f, 8f * B.KE * 2f), SpriteR - 30f, VfxDraw.Fade(B.SunWarm, 0.6f), VfxDraw.glow, UbwDepth.NoWrite);
            Single("sun face", SunAt(1.6f, 1.6f * B.KE * 2f), SpriteR - 31f, VfxDraw.Fade(B.SunFace, 0.97f), VfxDraw.soft, UbwDepth.NoWrite);
            smog.Clear();
            foreach (B.Puff q in B.SmogBands(s))
                smog.Add(new SkySprite { A = q.A, E = q.E, W = 2f * q.W, H = 2f * q.H, Gx = W.SkyX(q.A, 0.06f), Gz = W.SkyZ(q.E), Gw = q.W * B.KE * 2f, Gh = q.H * B.KE * 2f });
            shot.Draw(Sprites(W, "smog", smog, SpriteR - 40f), VfxDraw.Fade(B.Smog, 0.34f), VfxDraw.soft, at, UbwDepth.NoWrite);

            if (gearOrder == null)
            {
                gearOrder = new int[C.Gears.Length];
                for (int i = 0; i < gearOrder.Length; i++) gearOrder[i] = i;
                Array.Sort(gearOrder, (p, q) => C.Gears[q].Haze.CompareTo(C.Gears[p].Haze));
            }
            float toSunX = W.SunA > 0f ? 1f : W.SunA < 0f ? -1f : 1f;
            for (int i = 0; i < gearOrder.Length; i++)
            {
                C.SkyGear g = C.Gears[gearOrder[i]];
                float turn = (s * Math.Sign(g.Spin) * (float)C.Spin * 14f / (float)g.R + (float)g.A * 5f) * D2R, R3 = GearR * Mathf.Tan((float)g.R * D2R), R = (float)g.R * B.KE;
                shot.Draw(Gear(W, "gear rim " + i, g, turn, toSunX * R3 * 0.02f, R3 * 0.012f, toSunX * R * 0.02f, R * 0.012f), VfxDraw.Fade(B.RimLight, 0.5f * (1f - (float)g.Haze)), VfxDraw.solid, at, UbwDepth.NoWrite);
                shot.Draw(Gear(W, "gear " + i, g, turn, 0f, 0f, 0f, 0f), VfxDraw.Fade(B.Mix(B.Silhouette, B.SkyAt((float)(g.E + g.R * .3)), (float)g.Haze), 0.97f), VfxDraw.solid, at);
            }
        }

        // ---- the plain and the backdrop ------------------------------------------------------------------------------

        public static float HazeOf(float d) => Mathf.Sqrt(Mathf.Clamp01(d / HazeAt));
        /// <summary>The distance where the backdrop's eye sees the ground `off` screen cells below the horizon, and the other way round.</summary>
        public static float BackDistance(float off) => BackEye / Mathf.Tan(Mathf.Max(0.02f, -off / B.KE) * D2R);
        public static float BackOffset(float d) => -B.KE * Mathf.Atan(BackEye / d) / D2R;

        /// <summary>
        /// The plain from PlainSouth to PlainFar cells past the edge, level at 0. 3D (<paramref name="game"/> false): the
        /// texture's row whose colour is the haze at that distance. game: v4's band of plain from the cover line to the
        /// horizon; a point is where the backdrop's eye sees it, no lower than the cover line.
        /// </summary>
        private static UbwMesh3 Plain(UbwRevealScene W, bool game) => W.Get("plain " + game, () =>
        {
            var rows = new List<float> { PlainSouth, -40f, -20f, -10f, -5f, -2f, 0f };
            for (float d = 0.25f; d < PlainFar; d *= d < 2f ? 2f : 1.32f) rows.Add(d);
            rows.Add(PlainFar);
            const int cols = 49;
            float width = 2f * W.View.HalfW + 10f;
            var b = new UbwBuilder3();
            for (int j = 0; j < rows.Count; j++)
            {
                float d = rows[j], X = 80f + 1.5f * (W.North + Mathf.Max(0f, d) + 25f), z = W.North + d;
                // (the far end on the horizon itself, or the sky shows through under it)
                float gz = d <= (float)C.CoverAt ? z : d >= PlainFar ? W.HorizonZ : Mathf.Max(W.Cover, W.HorizonZ + BackOffset(W.North + d));
                float hazeRow = Mathf.Min(0.05f, d * 0.1f) + 0.95f * Mathf.Pow(HazeOf(d) * 0.85f, 1f / 2.6f);
                for (int i = 0; i < cols; i++)
                {
                    float x = -X + 2f * X * i / (cols - 1);
                    float v = d <= 0f ? 0.002f : Mathf.Clamp(game ? (gz - W.Cover) / (W.HorizonZ - W.Cover) : hazeRow, 0.002f, 0.998f);
                    b.Vertex(new Vector3(x, 0f, z), new Vector2(x, gz), new Vector2(Mathf.Clamp((x - W.CamX) / width + 0.5f, 0.002f, 0.998f), v));
                    if (i == 0 || j == 0) continue;
                    int n = j * cols + i;
                    b.Triangle(n - cols - 1, n - 1, n);
                    b.Triangle(n - cols - 1, n, n - cols);
                }
            }
            return b.Take("UBW reveal plain " + game);
        });

        /// <summary>The plain, then its game copy faded in with the blend.</summary>
        internal static void PlainDraw(UbwShot shot, UbwRevealScene W, Vector3 at, float blend)
        {
            shot.Draw(Plain(W, false), W.Tint, B.groundMat, at, UbwDepth.NoWrite);
            if (blend > 0f) shot.Draw(Plain(W, true), VfxDraw.Fade(W.Tint, blend), B.groundMat, at, UbwDepth.NoWrite);
        }

        /// <summary>A layer of the backdrop in 3D: a ridge (fill, rim, swords by ring) or a row of swords (by ring) at distance D.</summary>
        internal sealed class BackLayer
        {
            public UbwMesh3 Fill, Rim;
            public readonly List<(int ring, UbwMesh3 mesh)> Rings = new List<(int, UbwMesh3)>();
            public Color Colour, RimColour, SwordColour;
            public float D;
        }

        /// <summary>
        /// v4's backdrop stood up, far to near: the mountains, rows 0-4, the middle ridge, rows 5-8, the near ridge, rows
        /// 9-13. A vertex `off` screen cells from the horizon is off / KE degrees up for the backdrop's eye, x screen
        /// cells across is x / KA degrees across; a ridge is a wall where the eye sees its base, a row's swords stand
        /// where it sees their feet, no further than RowFar of the mountains. The game positions are v4's drawing for
        /// the shot's game view.
        /// </summary>
        internal static List<BackLayer> Backdrop(UbwRevealScene W) => W.Get("backdrop", () =>
        {
            float move = (float)C.HorizonMove, far = RowFar * BackDistance((float)C.Ridges[0].Base);
            float XAt(float p) => (1f - W.Share(p)) * W.CamX;
            float LineAt(float off, float p) => W.North + (float)C.Horizon + off + (1f - W.Share(p)) * W.Dz;
            float Pv(float f) => move + (1f - move) * f;
            float horizonZ = LineAt(0f, move);
            float TanE(float off) => Mathf.Tan(off / B.KE * D2R);
            // Vertex i of a layer mesh drawn from a line `line` screen cells from the horizon: its game point at (gx, gz)
            // from it, no lower than floor; its 3D place on the wall of its foot (off, d), d x behind back.
            void Vertex(UbwBuilder3 into, Vector3[] v, Vector2[] uv, int i, float line, float gx, float gz, float off, float d, float behind, float floor)
            {
                float xl = v[i].x, zl = v[i].z;
                into.Vertex(new Vector3(xl * d * D2R / B.KA, Mathf.Max(0f, d * (TanE(line + zl) - TanE(off))), d * behind), new Vector2(gx + xl, Mathf.Max(floor, gz + zl)),
                    uv != null && uv.Length == v.Length ? uv[i] : new Vector2(0.5f, 0.5f));
            }
            UbwMesh3 Whole(string name, Mesh m, float gx, float gz, float off, float d, float floor)
            {
                Vector3[] v = m.vertices;
                var b = new UbwBuilder3();
                for (int i = 0; i < v.Length; i++) Vertex(b, v, m.uv, i, 0f, gx, gz, off, d, 1f, floor);
                b.Tri.AddRange(m.triangles);
                return b.Take(name);
            }
            // A mesh of strokes (4 corners, 2 triangles each) by ring, each stroke on the wall of its own foot.
            void Strokes(BackLayer L, string name, Mesh m, float line, float gx, float gz, Func<Vector3[], int, (float off, float d, float behind)> footOf)
            {
                Vector3[] v = m.vertices;
                int[] tri = m.triangles;
                var by = new SortedDictionary<int, UbwBuilder3>();
                for (int g = 0; g < v.Length / 4; g++)
                {
                    var (off, d, behind) = footOf(v, g);
                    int ring = UbwRevealTiming.RingOf(Mathf.Sqrt(Sq(v[4 * g].x * d * D2R / B.KA) + d * d));
                    if (!by.TryGetValue(ring, out UbwBuilder3 b)) by[ring] = b = new UbwBuilder3();
                    int start = b.Count;
                    for (int k = 0; k < 4; k++) Vertex(b, v, null, 4 * g + k, line, gx, gz, off, d, behind, float.NegativeInfinity);
                    for (int t = 6 * g; t < 6 * g + 6; t++) b.Tri.Add(tri[t] - 4 * g + start);
                }
                foreach (KeyValuePair<int, UbwBuilder3> kv in by) L.Rings.Add((kv.Key, kv.Value.Take(name + " " + kv.Key)));
            }
            var layers = new List<BackLayer>();
            void Ridge(int r)
            {
                C.Ridge R = C.Ridges[r];
                float off = (float)R.Base, d = BackDistance(off), gx = XAt((float)R.P), gz = LineAt(0f, Pv((float)R.P));
                Color col = B.Mix(B.RidgeEarth, B.HazeFar, (float)R.Haze);
                float floor = r != 0 ? float.NegativeInfinity : horizonZ;
                var L = new BackLayer
                {
                    Fill = Whole("UBW reveal ridge " + r, B.RidgeFill(r), gx, gz, off, d, floor), Rim = Whole("UBW reveal ridge rim " + r, B.RidgeRim(r), gx, gz, off, d, floor),
                    Colour = col, RimColour = VfxDraw.Fade(B.Mix(col, B.RimLight, 0.45f), 0.45f), SwordColour = B.Mix(B.Mix(B.Steel, B.Silhouette, 0.5f), B.HazeFar, (float)R.Haze * 0.9f), D = d,
                };
                if (B.RidgeSwords(r) != null) Strokes(L, "UBW reveal ridge swords " + r, B.RidgeSwords(r), 0f, gx, gz, (v, g) => (off, d, 1.01f));
                layers.Add(L);
            }
            void Rows(int k0, int k1)
            {
                for (int q = k0; q < k1; q++)
                {
                    C.Row R = C.RowOf(q);
                    float rd = (float)R.D;
                    var L = new BackLayer { Colour = B.Mix(B.Mix(B.Steel, B.Silhouette, 0.35f), B.HazeFar, (float)R.Haze), D = Mathf.Min(far, BackDistance(-rd)) };
                    Strokes(L, "UBW reveal row " + q, B.RowSwords(q), -rd, XAt((float)R.P), LineAt(-rd, Pv((float)R.P)), (v, g) =>
                    {
                        float low = float.PositiveInfinity;
                        for (int k = 0; k < 4; k++) low = Mathf.Min(low, v[4 * g + k].z);
                        return (low - rd, Mathf.Min(far, BackDistance(low - rd)), 1f);
                    });
                    layers.Add(L);
                }
            }
            Ridge(0); Rows(0, 5); Ridge(1); Rows(5, 9); Ridge(2); Rows(9, C.RowCount);
            return layers;
        });

        private static float Sq(float v) => v * v;

        /// <summary>
        /// The backdrop at blend b with the fire run out to r: a ridge's swords before its fill (the wall hides their
        /// feet), the fill, its rim; a row's swords against the low sun, dark going to haze with distance, toward v4's
        /// colour with the blend. A ring stands once the fire has reached it, rising out of the ground.
        /// </summary>
        internal static void BackdropDraw(UbwShot shot, UbwRevealScene W, Vector3 at, float r, float blend)
        {
            foreach (BackLayer L in Backdrop(W))
            {
                if (L.Fill != null)
                {
                    foreach (var (ring, mesh) in L.Rings) shot.Draw(mesh, L.SwordColour, VfxDraw.solid, at, UbwDepth.NoWrite, UbwRevealTiming.RiseOf(ring, r));
                    shot.Draw(L.Fill, L.Colour, VfxDraw.solid, at);
                    shot.Draw(L.Rim, L.RimColour, VfxDraw.solid, at, UbwDepth.Over);
                    continue;
                }
                Color colour = Color.Lerp(B.Mix(B.Silhouette, B.HazeFar, HazeOf(L.D) * 0.85f), L.Colour, blend);
                foreach (var (ring, mesh) in L.Rings) shot.Draw(mesh, colour, VfxDraw.solid, at, UbwDepth.NoWrite, UbwRevealTiming.RiseOf(ring, r));
            }
        }
    }
}
