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

        /// <summary>Part of blade b's picture, a polygon in its own uv, from cell r of the atlas, projected on screen or along the sun. With <paramref name="three"/> (not for a shadow), also its 3D places, the sword standing <paramref name="h"/> cells up on its plate instead of drawn Lift h north.</summary>
        private static void PolyInto(Builder b, List<UbwUV> poly, UbwPose pose, UbwXZ? sun, in Cell r, UbwV3? shift = null, UbwBuilder3 three = null, float h = 0f)
        {
            if (poly.Count < 3) return;
            var pts = new List<Vector2>(poly.Count);
            var uvs = new List<Vector2>(poly.Count);
            var at3 = three != null ? new List<Vector3>(poly.Count) : null;
            for (int i = 0; i < poly.Count; i++)
            {
                UbwV3 p = UbwBlade.At3(pose, poly[i]);
                if (shift.HasValue) p = UbwV3.Plus(p, shift.Value);
                UbwXZ q = sun.HasValue ? UbwBlade.AlongSun(p, sun.Value) : UbwBlade.OnScreen(p);
                pts.Add(new Vector2((float)q.X, (float)q.Z));
                uvs.Add(r.At(poly[i].U, poly[i].V));
                at3?.Add(new Vector3((float)p.X, (float)p.Y + h, (float)p.Z - h * Lift));
            }
            if (b != null) b.Poly(pts, uvs);
            three?.Poly(at3, pts, uvs);
        }

        /// <summary>
        /// One sword for the reveal shot's camera: its blade (both lips, both edges, the face, the bands) with 3D
        /// places into <paramref name="blades"/>, and its marks and its shadow lying on its plate into
        /// <paramref name="ground"/> and <paramref name="shadow"/> (lib/ubw-reveal.js field3).
        /// </summary>
        internal static void SwordInto3(UbwSword sw, Vector2 sun, UbwBuilder3 blades, UbwBuilder3 ground, UbwBuilder3 shadow)
        {
            var sunXZ = new UbwXZ(sun.x, sun.y);
            float lift = (float)sw.Lift, h = lift / Lift;
            Builder marks = Scratch("UBW reveal marks"), lip = Scratch("UBW reveal lip"), shade = Scratch("UBW reveal shadow");
            MarksInto(marks, sw.Cut, sw.Seed, sw.Far ? 0 : 4);
            ground.Ground(marks, h, lift);
            if (!sw.Far)
            {
                LipInto(lip, sw.Cut, -1, 0.026, sw.Seed, sunXZ);
                blades.Ground(lip, h + 0.01f, lift);
            }
            BladeInto(shade, null, sw.Pose, sunXZ, sw.W.Row, blades, h);
            shadow.Ground(shade, h, lift);
            if (!sw.Far)
            {
                lip = Scratch("UBW reveal lip");
                LipInto(lip, sw.Cut, 1, 0.032, sw.Seed + 3, sunXZ);
                blades.Ground(lip, h + 0.01f, lift);
            }
        }

        /// <summary>A sword standing at rest: its shadow, both edges, the face lit one of three ways, the two dark bands low on the blade.</summary>
        private static void BladeInto(Builder shadows, Builder blades, UbwPose pose, UbwXZ sun, int row, UbwBuilder3 three = null, float h = 0f)
        {
            List<UbwUV> above = UbwBlade.Clip(UbwBlade.Square, UbwBlade.HigherThan(pose, 0));
            PolyInto(shadows, above, pose, sun, new Cell(FaceCol[2], row));
            for (int i = 0; i < 2; i++)
            {
                double f = i == 0 ? 1 : 0.5;
                PolyInto(blades, above, pose, null, new Cell(i == 0 ? Edge0 : Edge1, row), new UbwV3(-pose.N.X * 0.03 * f, -pose.N.Y * 0.03 * f, -pose.N.Z * 0.03 * f), three, h);
            }
            double lit = 0.8 + 0.2 * Math.Max(0, UbwV3.Dot(pose.N, UbwV3.Unit(new UbwV3(-sun.X, 1, -sun.Z))));
            int face = 0;
            for (int k = 1; k < FaceLit.Length; k++)
                if (Math.Abs(FaceLit[k] - lit) < Math.Abs(FaceLit[face] - lit)) face = k;
            PolyInto(blades, above, pose, null, new Cell(FaceCol[face], row), null, three, h);
            PolyInto(blades, UbwBlade.Clip(above, UbwBlade.LowerThan(pose, 0.2)), pose, null, new Cell(LowBand, row), null, three, h);
            PolyInto(blades, UbwBlade.Clip(above, UbwBlade.LowerThan(pose, 0.08)), pose, null, new Cell(LowerBand, row), null, three, h);
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
}
