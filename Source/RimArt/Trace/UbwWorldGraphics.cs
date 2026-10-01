using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.UbwGraphics;
using T = RimArt.UbwWorldTiming;

namespace RimArt
{
    /// <summary>
    /// The heights the world is drawn at. Only the order matters: the backstop, the ground, the dust
    /// patches, the ground marks of the swords, the hill's light, the sun's glow, the gear shadows, the
    /// swords' shadows, the blades, the haze past the map edge. What is overhead (embers, the white, the
    /// wall of fire, the traces) is at MoteOverhead on any map. The world v4's crest stands just over the
    /// plates (Floor + 0.0002 to + 0.0007) and its shadows just over the gear shadows.
    /// </summary>
    internal readonly struct UbwLayers
    {
        public readonly float Back, Floor, Patch, Marks, Hill, Glow, GearShadow, FieldShadow, Blades, Haze;
        /// <summary>The step between the blades' meshes when the field needs more than one.</summary>
        public readonly float BladeStep;
        /// <summary>The world v4's crack floor and the backdrop over it (41 steps of 0.00005), both under the plates (which are at Floor), and the plates' cast shadows between the gear shadows and the swords'.</summary>
        public readonly float Base, Backdrop, TerrainShadow;

        public UbwLayers(float back, float floor, float patch, float marks, float hill, float glow, float gearShadow, float fieldShadow, float blades, float haze, float bladeStep,
            float baseFloor, float backdrop, float terrainShadow)
        {
            Back = back; Floor = floor; Patch = patch; Marks = marks; Hill = hill; Glow = glow; GearShadow = gearShadow;
            FieldShadow = fieldShadow; Blades = blades; Haze = haze; BladeStep = bladeStep;
            Base = baseFloor; Backdrop = backdrop; TerrainShadow = terrainShadow;
        }

        /// <summary>
        /// The world's own map: the sketch's layers. The ground over the terrain (its earth terrain is only
        /// a fallback), the marks at Filth as a planted blade's are, the shadows at Shadows, the blades at
        /// Building as a planted blade (a Building) is, the haze 0.6 over that: over the swords, under pawns.
        /// </summary>
        public static readonly UbwLayers Pocket = new UbwLayers(
            Terrain + 0.001f, Terrain + 0.005f, Terrain + 0.015f, VfxDraw.Floor + 0.002f, VfxDraw.Floor + 0.01f,
            VfxDraw.Floor + 0.0105f, Shadows, Shadows + 0.002f, UbwGraphics.Building, UbwGraphics.Building + 0.6f, 0.0005f,
            Terrain + 0.002f, Terrain + 0.0022f, Shadows + 0.0015f);

        /// <summary>
        /// A home map, for the preview: packed between ItemImportant + 0.1 and + 0.27, as the castle's and
        /// Kamui's previews are, so the ground covers the map's ground, plants, buildings and items and pawns
        /// stay on top.
        /// </summary>
        public static readonly UbwLayers Preview = new UbwLayers(
            AltitudeLayer.ItemImportant.AltitudeFor() + 0.1f, AltitudeLayer.ItemImportant.AltitudeFor() + 0.105f, AltitudeLayer.ItemImportant.AltitudeFor() + 0.11f,
            AltitudeLayer.ItemImportant.AltitudeFor() + 0.12f, AltitudeLayer.ItemImportant.AltitudeFor() + 0.13f, AltitudeLayer.ItemImportant.AltitudeFor() + 0.133f,
            AltitudeLayer.ItemImportant.AltitudeFor() + 0.14f, AltitudeLayer.ItemImportant.AltitudeFor() + 0.145f, AltitudeLayer.ItemImportant.AltitudeFor() + 0.15f,
            AltitudeLayer.ItemImportant.AltitudeFor() + 0.26f, 0.0005f,
            AltitudeLayer.ItemImportant.AltitudeFor() + 0.101f, AltitudeLayer.ItemImportant.AltitudeFor() + 0.1012f, AltitudeLayer.ItemImportant.AltitudeFor() + 0.1445f);
    }

    /// <summary>
    /// Draws the inside of Unlimited Blade Works round the cell the caster lands on: the white everyone
    /// arrives in, the wall of fire running out from the caster past the map edge, and behind it the
    /// world (red-brown cracked earth, dust patches, the low sun's warm glow, the hill of swords under the
    /// caster shown by its light, the shadows of six gears turning overhead, the field of swords standing
    /// like grave markers, the haze past the map edge, embers drifting up and east); each sword near the
    /// caster flashing its wire outline as the fire passes with a bright line running up it; the world
    /// standing; the white closing in from the edge behind a wall of fire, each sword near the caster
    /// flashing into light just before it goes.
    ///
    /// The port of Tools/VfxLab/web/sketches/trace-ubw-world.js and the world of lib/ubw-pocket.js. The
    /// sketch's stand-ins are not ported: the pawns, and the dashed map edge. Everything is flat on the
    /// floor, a level circle or a standing sword, so it looks the same from every side.
    ///
    /// The world v4 (trace-ubw-world-v4.js, the one the ability opens) stands on the plate ground, ends it
    /// with a sword crest past the map's north edge and hangs a sky with gears behind it
    /// (<see cref="UbwCrestBake"/>, <see cref="UbwBackdropGraphics"/>). The sketch starts on the standing
    /// world as the white fades, because its fire moves into the reveal shot, which is not built; until it
    /// is, the fire still runs out here as in v1.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class UbwWorldGraphics
    {
        /// <summary>The lab camera sits about 2 cells north of the chosen cell, so the sketch centres the world there; the preview does the same.</summary>
        public const float SceneNorth = 2f;
        private static readonly Color Far = Color.Lerp(FarEarth, Haze, 0.45f);
        private static readonly Mesh[] hazeBands = MakeHazeBands();
        private static readonly Dictionary<string, Mesh> floors = new Dictionary<string, Mesh>();
        private static readonly List<(string key, UbwFieldBake bake)> bakes = new List<(string, UbwFieldBake)>();

        private static Mesh[] MakeHazeBands()
        {
            var bands = new Mesh[8];
            for (int k = 0; k < 8; k++) bands[k] = Band((T.MapHalf + 2f + k * 2.4f) / 240f, 1f, 96, "UBW haze " + k);
            return bands;
        }

        /// <summary>The preview's stand-in landing spots: the caster in the middle and the sketch's three.</summary>
        public static UbwXZ[] PreviewKeep()
        {
            var keep = new UbwXZ[T.Landed.Length + 1];
            keep[0] = new UbwXZ(0, 0);
            for (int i = 0; i < T.Landed.Length; i++) keep[i + 1] = new UbwXZ(T.Landed[i].x, T.Landed[i].y);
            return keep;
        }

        /// <summary>The field for these landing spots under this sun, baked; the last three are kept. With <paramref name="ground"/> (the world v4) the swords stand on its plates, end at the map's north edge and cluster.</summary>
        public static UbwFieldBake BakeFor(IList<UbwXZ> keep, Vector2 sun, UbwTerrain ground = null)
        {
            UbwWeaponSet set = Set;
            string key = keep.Count + ":";
            for (int i = 0; i < keep.Count; i++) key += keep[i].X.ToString("0.###") + "," + keep[i].Z.ToString("0.###") + ";";
            key += sun.x.ToString("0.000") + "," + sun.y.ToString("0.000") + "|" + set.Weapons.Length + (ground != null ? "|v4 " + ground.Seed : "");
            for (int i = 0; i < bakes.Count; i++) if (bakes[i].key == key) return bakes[i].bake;
            UbwFieldSettings look = ground != null ? UbwField.CrestLook : UbwField.Look;
            UbwFieldBake bake = UbwFieldBake.Build(UbwField.Make(look, keep, set.Weapons, ground != null ? ground.HeightAt : (Func<double, double, double>)null), sun, set);
            bakes.Add((key, bake));
            if (bakes.Count > 3) bakes.RemoveAt(0);
            return bake;
        }

        // ---- the ground and the air ------------------------------------------------------------------------------------

        /// <summary>What the far zoom-out sees past the drawn ground.</summary>
        private static void Backstop(Vector2 c, float altitude) => Sprite(c, 500f, 500f, Far, solid, altitude);

        /// <summary>The ground from half cells west to half east (and south to north) of c, in tiles of tile cells with the whole texture on each, one draw.</summary>
        private static void FloorTiles(Vector2 c, float half, float tile, Color tint, float altitude)
        {
            string id = half + "," + tile;
            if (!floors.TryGetValue(id, out Mesh m))
            {
                var b = new Builder("UBW floor " + id);
                for (float z = -half; z < half - 1e-6f; z += tile)
                    for (float x = -half; x < half - 1e-6f; x += tile)
                    {
                        int n = b.Count;
                        b.Vertex(x, z, new Vector2(0f, 0f));
                        b.Vertex(x, z + tile, new Vector2(0f, 1f));
                        b.Vertex(x + tile, z + tile, new Vector2(1f, 1f));
                        b.Vertex(x + tile, z, new Vector2(1f, 0f));
                        b.Tri(n, n + 1, n + 2);
                        b.Tri(n, n + 2, n + 3);
                    }
                m = b.Take("UBW floor " + id);
                floors[id] = m;
            }
            DrawMesh(m, c, altitude, 1f, 1f, 0f, tint, earth);
        }

        /// <summary>Broad dust patches, darker and warmer, over the tiles so the repeat does not show: count of them in a square half cells across.</summary>
        private static void Patches(Vector2 c, float half, int count, float altitude)
        {
            Builder dark = Scratch("UBW patches dark"), warm = Scratch("UBW patches warm");
            for (int i = 0; i < count; i++)
            {
                float x = (Rand(i * 17 + 2) - 0.5f) * 2f * half, z = (Rand(i * 19 + 4) - 0.5f) * 2f * half, size = 5f + Rand(i * 23) * 9f;
                (i % 3 != 0 ? dark : warm).Quad(x, z, size, size * (0.6f + 0.3f * Rand(i * 29)), Rand(i * 31) * 180f);
            }
            UbwGraphics.Draw(dark, c, altitude, Fade(DuskDark, 0.18f), soft);
            UbwGraphics.Draw(warm, c, altitude + 0.001f, Fade(Sunset, 0.08f), soft);
        }

        internal static Vector2 ToSun(Vector2 sun)
        {
            float d = sun.magnitude;
            if (d == 0f) d = 1f;
            return new Vector2(-sun.x / d, -sun.y / d);
        }

        /// <summary>The low sun's side of the sky warms the ground on that side.</summary>
        private static void SunGlow(Vector2 c, Vector2 sun, float alpha, float altitude)
        {
            if (alpha <= 0f) return;
            Vector2 to = ToSun(sun);
            Sprite(c + to * 24f, 80f, 80f, Fade(Twilight, 0.14f * alpha), glow, altitude);
        }

        /// <summary>The hill of swords under the caster, shown by its light: lit toward the sun, shaded away from it.</summary>
        private static void Hill(Vector2 c, float radius, Vector2 sun, float alpha, float altitude, float dark = 0.36f)
        {
            if (radius <= 0f || alpha <= 0f) return;
            Vector2 to = ToSun(sun);
            Sprite(c + to * radius * 0.25f, radius * 2.3f, radius * 2.3f, Fade(Sunset, 0.2f * alpha), glow, altitude + 0.0001f);
            Sprite(c - to * radius * 0.45f, radius * 2.1f, radius * 1.9f, Fade(DuskDark, dark * alpha), soft, altitude);
        }

        /// <summary>Haze past the map edge, thicker the further out, over the swords and under the pawns.</summary>
        private static void HazeBands(Vector2 c, float alpha, float altitude)
        {
            if (alpha <= 0f) return;
            for (int k = 0; k < hazeBands.Length; k++) DrawMesh(hazeBands[k], c, altitude + k * 0.0001f, 240f, 240f, 0f, Fade(Haze, 0.1f * alpha), solid);
        }

        private static float Wrap(float v, float span) => ((v + span / 2f) % span + span) % span - span / 2f;

        /// <summary>An ember of <see cref="EmberAt"/>: where it is from the caster, its size, its index and which of three brightnesses.</summary>
        internal struct Ember1
        {
            public float X, Z, Size;
            public int I, Bright;
        }

        /// <summary>Ember i of the embers drifting up (north) and with the wind (east) over the whole view, looping (lib/ubw-pocket.js emberLists).</summary>
        internal static Ember1 EmberAt(int i, float s)
        {
            const float spanX = 48f, spanZ = 34f;
            float x = Wrap((Rand(i * 3 + 2) - 0.5f) * spanX + s * (0.25f + 0.3f * Rand(i * 11)) + Mathf.Sin(s * 1.3f + i) * 0.15f, spanX);
            float z = Wrap((Rand(i * 5 + 4) - 0.5f) * spanZ + s * (0.35f + 0.5f * Rand(i * 7 + 1)), spanZ);
            float f = 0.5f + 0.5f * Mathf.Sin(s * (3f + Rand(i) * 4f) + i * 1.7f);
            return new Ember1 { X = x, Z = z, Size = 0.05f + Rand(i * 13) * 0.06f, I = i, Bright = Mathf.Min(2, Mathf.FloorToInt(f * 3f)) };
        }

        internal static Color EmberColour(int bright, float alpha) => Fade(Ember, (0.3f + 0.3f * bright) * alpha);

        /// <summary>Embers drifting up (north) and with the wind (east) over the whole view, looping, three brightnesses.</summary>
        private static void Embers(string key, Vector2 c, float s, int count, float alpha)
        {
            if (alpha <= 0f) return;
            Builder[] lists = { Scratch(key + " 0"), Scratch(key + " 1"), Scratch(key + " 2") };
            for (int i = 0; i < count; i++)
            {
                Ember1 e = EmberAt(i, s);
                lists[e.Bright].Quad(e.X, e.Z, e.Size, e.Size, 0f);
            }
            for (int k = 0; k < 3; k++) UbwGraphics.Draw(lists[k], c, Overhead + 0.03f, EmberColour(k, alpha), glow);
        }

        // ---- the trace over a sword -------------------------------------------------------------------------------------

        /// <summary>One sword's trace drawn over its baked steel: the wire outline at wireAlpha and, if scan >= 0, the bright line that runs up it (a scan of 0..1 of its height).</summary>
        private static void TraceOver(UbwSword sw, UbwWeaponSet set, Vector2 o, float wireAlpha, float scan, float altitude)
        {
            UbwPose b = sw.Pose;
            List<UbwUV> above = UbwBlade.Clip(UbwBlade.Square, UbwBlade.HigherThan(b, 0));
            int row = sw.W.Row;
            if (wireAlpha > 0f)
            {
                Builder wire = Scratch("UBW wire " + sw.Seed);
                TexPoly(wire, UbwBlade.Clip(above, UbwBlade.LowerThan(b, 9)), b);
                UbwGraphics.Draw(wire, o, altitude + 0.0018f, Fade(Trace, wireAlpha), set.Wire[row]);
            }
            if (scan >= 0f)
            {
                double at = sw.Top * scan;
                Builder mask = Scratch("UBW scan " + sw.Seed);
                TexPoly(mask, UbwBlade.Clip(UbwBlade.Clip(above, UbwBlade.LowerThan(b, at + 0.015)), UbwBlade.HigherThan(b, at - 0.07)), b);
                UbwGraphics.Draw(mask, o, altitude + 0.002f, Fade(TraceHot, 0.85f), set.Mask[row]);
            }
        }

        /// <summary>Part of a blade's texture (a polygon in uv) as it is drawn on screen, with the whole texture's uv.</summary>
        private static void TexPoly(Builder into, List<UbwUV> poly, UbwPose b)
        {
            if (poly.Count < 3) return;
            var pts = new List<Vector2>(poly.Count);
            var uvs = new List<Vector2>(poly.Count);
            for (int i = 0; i < poly.Count; i++)
            {
                UbwXZ q = UbwBlade.OnScreen(UbwBlade.At3(b, poly[i]));
                pts.Add(new Vector2((float)q.X, (float)q.Z));
                uvs.Add(new Vector2((float)poly[i].U, (float)poly[i].V));
            }
            into.Poly(pts, uvs);
        }

        // ---- the world -------------------------------------------------------------------------------------------------------

        /// <summary>The preview: the world drawn over the map's ground, centred 2 cells north of the chosen cell as the sketch is, with the sketch's landing spots and the map's sun made low. <paramref name="crest"/>: the world v4, on the plate ground with the crest and the backdrop (seed 1, the sketch's).</summary>
        public static void DrawPreview(Vector3 centre, float seconds, Map map, bool crest = false)
        {
            var o = new Vector2(centre.x, centre.z + SceneNorth);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            sun = crest ? UbwCrestWorld.LowSun(sun) : sun * T.DuskShadow;
            UbwCrestWorld world = crest ? UbwCrestWorld.For(sun) : null;
            UbwFieldBake bake = BakeFor(PreviewKeep(), sun, world?.Terrain.Terrain);
            Draw(o, bake, seconds, T.CloseAt, UbwLayers.Preview, sun, strength, map, world);
        }

        /// <summary>
        /// The whole inside round <paramref name="o"/> at <paramref name="s"/> seconds. <paramref name="closeAt"/>
        /// is when the white began closing in (the preview's hold end; the real world's Close), or infinity
        /// while the world stands. <paramref name="sun"/> is the shadow vector per cell of height, already
        /// made low, and <paramref name="strength"/> how dark shadows are.
        /// </summary>
        /// <param name="crest">The world v4 baked, or null for the flat world v1: with it the plates stand in for the earth tiles and dust patches, the crest ends the ground past the north edge with the backdrop behind it, the hill's shade is deeper, three faint gear shadows sweep the map, the camera's haze stands in for the haze bands, and ash and embers drift in front.</param>
        public static void Draw(Vector2 o, UbwFieldBake bake, float s, float closeAt, in UbwLayers layers, Vector2 sun, float strength, Map map, UbwCrestWorld crest = null)
        {
            if (s < 0f || s >= closeAt + T.Close + 0.35f || !Shown(o, map)) return;
            Begin(o);
            float reach = T.Reach(UbwField.Look.Beyond), beyond = (float)UbwField.Look.Beyond, hillR = (float)UbwField.Look.Hill;
            float twilight = (float)UbwField.Twilight;
            Color tint = Color.Lerp(White, Tint, twilight);

            // The ground, the light and the air; the field. v4: behind the crest the backdrop, then the plates, the
            // crest, the light and the swords, the camera's haze, the air.
            Backstop(o, layers.Back);
            if (crest != null)
            {
                UbwView view = UbwView.Current();
                UbwTerrain ground = crest.Terrain.Terrain;
                UbwTerrainGraphics.TerrainBase(o, ground, layers.Base);
                UbwBackdropGraphics.Draw(o, s, sun, view, crest.Crest, tint, (float)ground.EdgeAt, layers.Backdrop);
                crest.Terrain.Draw(o, layers.Floor, layers.TerrainShadow, tint, strength);
                crest.Crest.Draw(o, layers, strength, tint);
                UbwBackdropGraphics.GearShadows(o, s, sun, (float)UbwCrest.GearShadows, crest.Crest.North, layers.GearShadow);
                SunGlow(o, sun, twilight, layers.Glow);
                Hill(o, hillR, sun, 1f, layers.Hill, 0.5f);
                bake.Draw(o, layers, strength, tint);
                UbwBackdropGraphics.DepthHaze(o, crest.Crest.North + crest.Crest.MeanTop, view, (float)UbwCrest.Haze, layers.Haze - 0.001f);
                Embers("UBW embers", o, s, 140, 1f);
                UbwBackdropGraphics.Foreground(o, s, view, 1f);
            }
            else
            {
                FloorTiles(o, T.MapHalf + beyond + 8f, 8f, tint, layers.Floor);
                Patches(o, T.MapHalf + beyond, 40, layers.Patch);
                SunGlow(o, sun, twilight, layers.Glow);
                Hill(o, hillR, sun, 1f, layers.Hill);
                SkyGears("UBW gears", o, s, sun, (float)UbwField.GearShadow, layers.GearShadow);
                bake.Draw(o, layers, strength, tint);
                HazeBands(o, 1f, layers.Haze);
                Embers("UBW embers", o, s, 140, 1f);
            }

            // The fire going out traces each sword near the caster as it passes.
            float traceAlt = layers.Haze + 0.05f;
            if (s < T.Swept)
                foreach (UbwSword sw in bake.Swords)
                {
                    if (sw.D > T.Near) continue;
                    float age = s - T.Passes((float)sw.D, reach);
                    if (age >= 0f && age < T.TraceFor)
                        TraceOver(sw, bake.Set, o, 0.9f * (1f - Smooth(age / T.TraceFor)), age < T.ScanFor ? Smooth(age / T.ScanFor) : -1f, traceAlt);
                }
            // Closing, each sword near the caster flashes into light just before the white takes it.
            if (s >= closeAt)
            {
                Builder sparks = Scratch("UBW sparks");
                foreach (UbwSword sw in bake.Swords)
                {
                    if (sw.D > T.Near) continue;
                    float lead = T.Covers((float)sw.D, closeAt, reach) - s;
                    if (lead <= 0f || lead > T.BreakFor) continue;
                    float u = 1f - lead / T.BreakFor;
                    UbwXZ mid = UbwBlade.OnScreen(UbwV3.Plus(sw.Pose.Tip, sw.Pose.A, sw.Pose.L * 0.6));
                    TraceOver(sw, bake.Set, o, 0.95f * Smooth(u), -1f, traceAlt);
                    for (int i = 0; i < 3; i++)
                    {
                        float a = Rand(sw.Seed + i * 3) * Mathf.PI * 2f;
                        sparks.Quad((float)mid.X + Mathf.Cos(a) * u * 0.45f, (float)mid.Z + Mathf.Sin(a) * u * 0.35f + u * 0.15f, 0.08f, 0.08f, 0f);
                    }
                }
                UbwGraphics.Draw(sparks, o, Overhead + 0.02f, Fade(TraceHot, 0.9f), glow);
            }

            // Outside the world: white, with the wall of fire at its edge.
            float r = s < T.Swept ? T.OutAt(s, reach) : s >= closeAt ? T.InAt(s, closeAt, reach) : float.PositiveInfinity;
            if (!float.IsPositiveInfinity(r))
            {
                Cover("UBW cover", o, r, 1f, Overhead + 0.06f);
                FireWall("UBW wall", o, r, s, T.WallHeight, Overhead + 0.065f);
            }
        }
    }
}
