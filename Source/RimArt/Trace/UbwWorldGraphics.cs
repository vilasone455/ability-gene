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
    /// wall of fire, the traces) is at MoteOverhead on any map.
    /// </summary>
    internal readonly struct UbwLayers
    {
        public readonly float Back, Floor, Patch, Marks, Hill, Glow, GearShadow, FieldShadow, Blades, Haze;
        /// <summary>The step between the blades' meshes when the field needs more than one.</summary>
        public readonly float BladeStep;
        /// <summary>The world v2's crack floor and its sky, both under the plates (which are at Floor), and the plates' cast shadows between the gear shadows and the swords'.</summary>
        public readonly float Base, Sky, TerrainShadow;

        public UbwLayers(float back, float floor, float patch, float marks, float hill, float glow, float gearShadow, float fieldShadow, float blades, float haze, float bladeStep,
            float baseFloor, float sky, float terrainShadow)
        {
            Back = back; Floor = floor; Patch = patch; Marks = marks; Hill = hill; Glow = glow; GearShadow = gearShadow;
            FieldShadow = fieldShadow; Blades = blades; Haze = haze; BladeStep = bladeStep;
            Base = baseFloor; Sky = sky; TerrainShadow = terrainShadow;
        }

        /// <summary>
        /// The world's own map: the sketch's layers. The ground over the terrain (its earth terrain is only
        /// a fallback), the marks at Filth as a planted blade's are, the shadows at Shadows, the blades at
        /// Building as a planted blade (a Building) is, the haze 0.6 over that: over the swords, under pawns.
        /// </summary>
        public static readonly UbwLayers Pocket = new UbwLayers(
            Terrain + 0.001f, Terrain + 0.005f, Terrain + 0.015f, VfxDraw.Floor + 0.002f, VfxDraw.Floor + 0.01f,
            VfxDraw.Floor + 0.0105f, Shadows, Shadows + 0.002f, UbwGraphics.Building, UbwGraphics.Building + 0.6f, 0.0005f,
            Terrain + 0.002f, Terrain + 0.003f, Shadows + 0.0015f);

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
            AltitudeLayer.ItemImportant.AltitudeFor() + 0.101f, AltitudeLayer.ItemImportant.AltitudeFor() + 0.102f, AltitudeLayer.ItemImportant.AltitudeFor() + 0.1445f);
    }

    /// <summary>
    /// The standing field baked: every sword's shadow, ground marks and blade in three kinds of mesh from
    /// the one atlas, in draw order, north first. The port of field, bladeInto, marksInto and lipInto in
    /// Tools/VfxLab/web/sketches/lib/ubw-pocket.js. Swords past the map edge get no lips or cracks. Built
    /// once per field and sun; a blades mesh that fills up hands over to the next, drawn a step higher.
    /// </summary>
    internal sealed class UbwFieldBake
    {
        public List<UbwSword> Swords;
        public UbwWeaponSet Set;
        public Mesh Shadows, Marks;
        public readonly List<Mesh> Blades = new List<Mesh>();
        public int Vertices;
        private const int MostVertices = 60000;

        public static UbwFieldBake Build(List<UbwSword> swords, Vector2 sun, UbwWeaponSet set)
        {
            var bake = new UbwFieldBake { Swords = swords, Set = set };
            var shadows = new Builder("UBW field shadows");
            var marks = new Builder("UBW field marks");
            var blades = new Builder("UBW field blades");
            var sunXZ = new UbwXZ(sun.x, sun.y);
            foreach (UbwSword sw in swords)
            {
                if (blades.Count + 200 > MostVertices)
                {
                    bake.Blades.Add(blades.Take("UBW field blades " + bake.Blades.Count));
                    bake.Vertices += blades.Count;
                    blades.Clear();
                }
                int row = sw.W.Row;
                MarksInto(marks, sw.Cut, sw.Seed, sw.Far ? 0 : 4);
                if (!sw.Far) LipInto(blades, sw.Cut, -1, 0.026, sw.Seed, sunXZ);
                BladeInto(shadows, blades, sw.Pose, sunXZ, row);
                if (!sw.Far) LipInto(blades, sw.Cut, 1, 0.032, sw.Seed + 3, sunXZ);
            }
            if (blades.Count > 0) bake.Blades.Add(blades.Take("UBW field blades " + bake.Blades.Count));
            bake.Shadows = shadows.Take("UBW field shadows");
            bake.Marks = marks.Take("UBW field marks");
            bake.Vertices += shadows.Count + marks.Count + blades.Count;
            return bake;
        }

        /// <summary>Part of blade b's picture, a polygon in its own uv, from cell r of the atlas, projected on screen or along the sun.</summary>
        private static void PolyInto(Builder b, List<UbwUV> poly, UbwPose pose, UbwXZ? sun, in Cell r, UbwV3? shift = null)
        {
            if (poly.Count < 3) return;
            var pts = new List<Vector2>(poly.Count);
            var uvs = new List<Vector2>(poly.Count);
            for (int i = 0; i < poly.Count; i++)
            {
                UbwV3 p = UbwBlade.At3(pose, poly[i]);
                if (shift.HasValue) p = UbwV3.Plus(p, shift.Value);
                UbwXZ q = sun.HasValue ? UbwBlade.AlongSun(p, sun.Value) : UbwBlade.OnScreen(p);
                pts.Add(new Vector2((float)q.X, (float)q.Z));
                uvs.Add(r.At(poly[i].U, poly[i].V));
            }
            b.Poly(pts, uvs);
        }

        /// <summary>A sword standing at rest: its shadow, both edges, the face lit one of three ways, the two dark bands low on the blade.</summary>
        private static void BladeInto(Builder shadows, Builder blades, UbwPose pose, UbwXZ sun, int row)
        {
            List<UbwUV> above = UbwBlade.Clip(UbwBlade.Square, UbwBlade.HigherThan(pose, 0));
            PolyInto(shadows, above, pose, sun, new Cell(FaceCol[2], row));
            for (int i = 0; i < 2; i++)
            {
                double f = i == 0 ? 1 : 0.5;
                PolyInto(blades, above, pose, null, new Cell(i == 0 ? Edge0 : Edge1, row), new UbwV3(-pose.N.X * 0.03 * f, -pose.N.Y * 0.03 * f, -pose.N.Z * 0.03 * f));
            }
            double lit = 0.8 + 0.2 * Math.Max(0, UbwV3.Dot(pose.N, UbwV3.Unit(new UbwV3(-sun.X, 1, -sun.Z))));
            int face = 0;
            for (int k = 1; k < FaceLit.Length; k++)
                if (Math.Abs(FaceLit[k] - lit) < Math.Abs(FaceLit[face] - lit)) face = k;
            PolyInto(blades, above, pose, null, new Cell(FaceCol[face], row));
            PolyInto(blades, UbwBlade.Clip(above, UbwBlade.LowerThan(pose, 0.2)), pose, null, new Cell(LowBand, row));
            PolyInto(blades, UbwBlade.Clip(above, UbwBlade.LowerThan(pose, 0.08)), pose, null, new Cell(LowerBand, row));
        }

        private static Vector2 Pt(in UbwCut cut, double along, double outward) =>
            new Vector2((float)(cut.X + cut.D.X * along + cut.F.X * outward), (float)(cut.Z + cut.D.Z * along + cut.F.Z * outward));

        /// <summary>The mark a blade leaves where it goes in, flat on the floor: the contact shadow, cracks, the slit.</summary>
        private static void MarksInto(Builder marks, in UbwCut cut, int seed, int cracks)
        {
            double half = cut.Half;
            float rot = (float)(-Math.Atan2(cut.D.Z, cut.D.X) / UbwBlade.D2R);
            Vector2 contact = Pt(cut, 0, 0.015);
            marks.Quad(contact.x, contact.y, (float)(half * 2 + 0.35), 0.2f, rot, new Cell(SwContact, SwatchRow));
            for (int i = 0; i < cracks; i++)
            {
                bool end = i < 2;
                double side = i % 2 == 1 ? 1 : -1;
                Vector2 start = end ? Pt(cut, side * half * 0.9, 0) : Pt(cut, (Rand(seed * 7 + i) - 0.5) * half * 1.4, 0);
                double ang = end ? Math.Atan2(cut.D.Z * side, cut.D.X * side) + (Rand(seed * 3 + i) - 0.5) * 0.6
                    : Math.Atan2(cut.F.Z * side, cut.F.X * side) + (Rand(seed * 5 + i) - 0.5) * 1.7;
                double len = 0.1 + Rand(seed * 11 + i) * 0.17;
                var pts = new List<Vector2> { start };
                for (int j = 1; j <= 4; j++)
                {
                    double a = ang + (Rand(seed * 13 + i * 5 + j) - 0.5) * 1.1;
                    Vector2 q = pts[j - 1];
                    pts.Add(new Vector2(q.x + (float)(Math.Cos(a) * len / 4), q.y + (float)(Math.Sin(a) * len / 4)));
                }
                marks.Line(pts, 0.024f, Swatch(SwCrack), 1);
            }
            marks.Line(new List<Vector2> { Pt(cut, -half - 0.035, 0), Pt(cut, 0, 0), Pt(cut, half + 0.035, 0) }, 0.055f, Swatch(SwHole), 2);
        }

        /// <summary>A lip of earth pushed up along the slit (side 1 faces the camera), into the blades so the back lip goes under its own blade and the front lip over its foot.</summary>
        private static void LipInto(Builder blades, in UbwCut cut, int side, double reach, int seed, UbwXZ sun)
        {
            double half = cut.Half, fx = cut.F.X * side, fz = cut.F.Z * side;
            const int n = 9;
            var inner = new Vector2[n];
            var crest = new Vector2[n];
            var outer = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)(n - 1), along = -half - 0.045 + (half * 2 + 0.09) * t, bulge = Math.Pow(Math.Sin(t * Math.PI), 0.6);
                double outward = 0.01 + reach * bulge * (0.7 + 0.6 * Rand(seed * 11 + i)), back = (side > 0 ? 0.024 : 0.012) * bulge;
                double bx = cut.X + cut.D.X * along, bz = cut.Z + cut.D.Z * along;
                inner[i] = new Vector2((float)(bx - fx * back), (float)(bz - fz * back));
                crest[i] = new Vector2((float)(bx + fx * outward * 0.3), (float)(bz + fz * outward * 0.3));
                outer[i] = new Vector2((float)(bx + fx * outward), (float)(bz + fz * outward));
            }
            bool sunward = -(fx * sun.X + fz * sun.Z) > 0;
            blades.Strip(inner, outer, Swatch(sunward ? SwDirtMid : SwDirtDark));
            blades.Strip(inner, crest, Swatch(sunward ? SwDirtLit : SwDirtMid));
        }

        /// <summary>The baked field with the caster at <paramref name="o"/>: shadows, ground marks, blades. tint colours the marks and blades.</summary>
        public void Draw(Vector2 o, in UbwLayers layers, float strength, Color tint)
        {
            DrawMesh(Shadows, o, layers.FieldShadow, 1f, 1f, 0f, Fade(Black, 0.42f * strength / 0.32f), Set.Atlas);
            DrawMesh(Marks, o, layers.Marks, 1f, 1f, 0f, tint, Set.Atlas);
            for (int k = 0; k < Blades.Count; k++) DrawMesh(Blades[k], o, layers.Blades + k * layers.BladeStep, 1f, 1f, 0f, tint, Set.Atlas);
        }
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

        /// <summary>The field for these landing spots under this sun, baked; the last three are kept. With <paramref name="ground"/> (the world v2) the swords stand on its plates.</summary>
        public static UbwFieldBake BakeFor(IList<UbwXZ> keep, Vector2 sun, UbwTerrain ground = null)
        {
            UbwWeaponSet set = Set;
            string key = keep.Count + ":";
            for (int i = 0; i < keep.Count; i++) key += keep[i].X.ToString("0.###") + "," + keep[i].Z.ToString("0.###") + ";";
            key += sun.x.ToString("0.000") + "," + sun.y.ToString("0.000") + "|" + set.Weapons.Length + (ground != null ? "|v2 " + ground.Seed : "");
            for (int i = 0; i < bakes.Count; i++) if (bakes[i].key == key) return bakes[i].bake;
            UbwFieldBake bake = UbwFieldBake.Build(UbwField.Make(UbwField.Look, keep, set.Weapons, ground != null ? ground.HeightAt : (Func<double, double, double>)null), sun, set);
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

        private static Vector2 ToSun(Vector2 sun)
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

        /// <summary>Embers drifting up (north) and with the wind (east) over the whole view, looping, three brightnesses.</summary>
        private static void Embers(string key, Vector2 c, float s, int count, float alpha)
        {
            if (alpha <= 0f) return;
            Builder[] lists = { Scratch(key + " 0"), Scratch(key + " 1"), Scratch(key + " 2") };
            const float spanX = 48f, spanZ = 34f;
            for (int i = 0; i < count; i++)
            {
                float x = Wrap((Rand(i * 3 + 2) - 0.5f) * spanX + s * (0.25f + 0.3f * Rand(i * 11)) + Mathf.Sin(s * 1.3f + i) * 0.15f, spanX);
                float z = Wrap((Rand(i * 5 + 4) - 0.5f) * spanZ + s * (0.35f + 0.5f * Rand(i * 7 + 1)), spanZ);
                float f = 0.5f + 0.5f * Mathf.Sin(s * (3f + Rand(i) * 4f) + i * 1.7f), size = 0.05f + Rand(i * 13) * 0.06f;
                lists[Mathf.Min(2, Mathf.FloorToInt(f * 3f))].Quad(x, z, size, size, 0f);
            }
            for (int k = 0; k < 3; k++) UbwGraphics.Draw(lists[k], c, Overhead + 0.03f, Fade(Ember, (0.3f + 0.3f * k) * alpha), glow);
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

        /// <summary>The preview: the world drawn over the map's ground, centred 2 cells north of the chosen cell as the sketch is, with the sketch's landing spots and the map's sun made low. <paramref name="depth"/>: the world v2, on the plate ground with the sky (seed 1, the sketch's).</summary>
        public static void DrawPreview(Vector3 centre, float seconds, Map map, bool depth = false)
        {
            var o = new Vector2(centre.x, centre.z + SceneNorth);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            sun *= T.DuskShadow;
            UbwTerrain ground = depth ? UbwTerrainGraphics.For(1) : null;
            UbwFieldBake bake = BakeFor(PreviewKeep(), sun, ground);
            Draw(o, bake, seconds, T.CloseAt, UbwLayers.Preview, sun, strength, map, ground != null ? UbwTerrainGraphics.BakeFor(ground, sun) : null);
        }

        /// <summary>
        /// The whole inside round <paramref name="o"/> at <paramref name="s"/> seconds. <paramref name="closeAt"/>
        /// is when the white began closing in (the preview's hold end; the real world's Close), or infinity
        /// while the world stands. <paramref name="sun"/> is the shadow vector per cell of height, already
        /// made low, and <paramref name="strength"/> how dark shadows are.
        /// </summary>
        /// <param name="terrain">The world v2's ground baked, or null for the flat world: with it the plates stand in for the earth tiles, the hill's shade is deeper, the gears hang in a sky north of the ridge and cast their shadows from there, and the haze stops at the ridge.</param>
        public static void Draw(Vector2 o, UbwFieldBake bake, float s, float closeAt, in UbwLayers layers, Vector2 sun, float strength, Map map, UbwTerrainBake terrain = null)
        {
            if (s < 0f || s >= closeAt + T.Close + 0.35f || !Shown(o, map)) return;
            Begin(o);
            float reach = T.Reach(UbwField.Look.Beyond), beyond = (float)UbwField.Look.Beyond, hillR = (float)UbwField.Look.Hill;
            float twilight = (float)UbwField.Twilight;
            Color tint = Color.Lerp(White, Tint, twilight);

            // The ground, the light and the air; the field.
            Backstop(o, layers.Back);
            if (terrain != null)
            {
                UbwTerrainGraphics.TerrainBase(o, terrain.Terrain, layers.Base);
                terrain.Draw(o, layers.Floor, layers.TerrainShadow, tint, strength);
            }
            else FloorTiles(o, T.MapHalf + beyond + 8f, 8f, tint, layers.Floor);
            Patches(o, T.MapHalf + beyond, 40, layers.Patch);
            SunGlow(o, sun, twilight, layers.Glow);
            Hill(o, hillR, sun, 1f, layers.Hill, terrain != null ? 0.5f : 0.36f);
            if (terrain == null) SkyGears("UBW gears", o, s, sun, (float)UbwField.GearShadow, layers.GearShadow);
            bake.Draw(o, layers, strength, tint);
            if (terrain != null)
            {
                UbwTerrainGraphics.HazeToRidge(terrain.Terrain, o, 1f, layers.Haze);
                UbwTerrainGraphics.Sky("UBW sky", terrain.Terrain, o, s, sun, (float)UbwField.GearShadow, layers.Sky, layers.GearShadow);
            }
            else HazeBands(o, 1f, layers.Haze);
            Embers("UBW embers", o, s, 140, 1f);

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
