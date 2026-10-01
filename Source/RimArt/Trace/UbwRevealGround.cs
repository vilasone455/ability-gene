using System;
using System.Collections.Generic;
using UnityEngine;
using static RimArt.UbwGraphics;
using B = RimArt.UbwBackdropGraphics;
using C = RimArt.UbwCrest;
using R = RimArt.UbwRevealTiming;

namespace RimArt
{
    /// <summary>
    /// Something standing in the reveal shot among the swords: a pawn (a portrait in game, the lab's stand-in discs in
    /// the preview), at X, Z from the caster. Its parts are drawn in order, after the swords north of it.
    /// </summary>
    internal sealed class UbwRevealFigure
    {
        public float X, Z;
        public readonly List<(UbwMesh3 mesh, Color colour, Material material, UbwDepth depth)> Parts = new List<(UbwMesh3, Color, Material, UbwDepth)>();
    }

    /// <summary>
    /// The map side of the world v4 in 3D, for the reveal shot (lib/ubw-reveal.js crackFloor, ground3, field3,
    /// drawPawns; lib/ubw-crest.js crestOf with rings):
    ///   - the crack floor 1 cell under the level plates, from the world's south edge to 2 cells past the map's
    ///     north edge, under the crest's foot; the map's plates with their faces down to it (v4's bake);
    ///   - the crest standing on the ground past the edge, with a flat top 2 cells deep and a back slope that only
    ///     the 3D camera sees (in the game view they lie on the top line), its swords on upright planes in rings;
    ///   - the field's swords, north first so that nothing needs a depth test, each ring rising out of the ground
    ///     once the fire is past it; their marks and shadows lying on the plates;
    ///   - the figures standing among them.
    /// </summary>
    internal static class UbwRevealGround
    {
        /// <summary>The crack floor runs FloorPast cells past the edge; the crest's flat top is Plateau deep and its back slope BackSlope x its height long; the thicket stands BackDepth (0.5 + 0 to 0.9) behind the top.</summary>
        public const float FloorPast = 2f, Plateau = 2f, BackSlope = 1.2f, BackDepth0 = 0.5f, BackDepth1 = 0.9f;
        private static readonly Color PlateauColour = new Color(0.382f, 0.237f, 0.1565f);

        /// <summary>The crack floor: its game position is v4's, flat, and no further north than the cover line.</summary>
        internal static UbwMesh3 Floor(UbwRevealScene W) => W.Get("floor", () =>
        {
            float e = (float)W.T.EdgeAt, reach = W.North + FloorPast, y = (float)W.T.Bottom;
            const int n = 20, m = 12;
            var b = new UbwBuilder3();
            for (int j = 0; j <= m; j++)
                for (int i = 0; i <= n; i++)
                {
                    float x = -e + 2f * e * i / n, z = -e + (e + reach) * j / m;
                    b.Vertex(new Vector3(x, y, z), new Vector2(x, Mathf.Min(z, W.Cover)), new Vector2(0.5f, 0.5f));
                    if (i == 0 || j == 0) continue;
                    int k = j * (n + 1) + i;
                    b.Triangle(k - n - 2, k - 1, k);
                    b.Triangle(k - n - 2, k, k - n - 1);
                }
            return b.Take("UBW reveal floor");
        });

        // ---- the crest -------------------------------------------------------------------------------------------

        internal sealed class Crest3
        {
            public UbwMesh3 Face, Rim, Plateau;
            /// <summary>By ring: the thicket's [rim copy, body] and the top row's [rim copy, body].</summary>
            public readonly List<(int ring, UbwMesh3[] back, UbwMesh3[] top)> Rings = new List<(int, UbwMesh3[], UbwMesh3[])>();
        }

        internal static Crest3 Crest(UbwRevealScene W) => W.Get("crest", () =>
        {
            double H = C.Height, perCell = C.SwordsPerCell, E = W.North;
            double FootZ(double x) => E + C.FootOf(x);
            double GroundZ(double x) => E + C.GroundOf(x);
            double TopZ(double x) => E + C.TopOf(x, H);
            Vector3 S3(double x, double z, double y, double wz) => new Vector3((float)x, (float)y, (float)wz);
            Vector2 G(double x, double z) => new Vector2((float)x, (float)z);
            UbwBuilder3 face = new UbwBuilder3(), rim = new UbwBuilder3(), plateau = new UbwBuilder3();
            int n = (int)Math.Round(2 * C.Span / C.Step), chunk = (int)Math.Round(C.Chunk / C.Step);
            for (int i = 0; i < n; i++)
            {
                double a = -C.Span + i * C.Step, b = a + C.Step;
                double f0 = FootZ(a), f1 = FootZ(b), t0 = TopZ(a), t1 = TopZ(b), g0 = GroundZ(a), g1 = GroundZ(b), h0 = C.HeightOf(a, H), h1 = C.HeightOf(b, H);
                float u0 = (float)(i % chunk * C.Step / C.Chunk), u1 = u0 + (float)(C.Step / C.Chunk);
                face.Poly(new[] { S3(a, f0, 0, f0), S3(a, t0, h0, g0), S3(b, t1, h1, g1), S3(b, f1, 0, f1) }, new[] { G(a, f0), G(a, t0), G(b, t1), G(b, f1) },
                    new[] { new Vector2(u0, 0.01f), new Vector2(u0, 0.99f), new Vector2(u1, 0.99f), new Vector2(u1, 0.01f) });
                // On the face, the point RimW below the top on the screen.
                Vector3 Under(double x, double f, double t, double g, double h) { double u = 1 - C.RimW / (t - f); return S3(x, t - C.RimW, u * h, f + u * (g - f)); }
                rim.Poly(new[] { S3(a, t0, h0, g0), S3(b, t1, h1, g1), Under(b, f1, t1, g1, h1), Under(a, f0, t0, g0, h0) }, new[] { G(a, t0), G(b, t1), G(b, t1 - C.RimW), G(a, t0 - C.RimW) });
                // The flat top and the back slope, all on the top line in the game view.
                int start = plateau.Count;
                plateau.Vertex(S3(a, t0, h0, g0), G(a, t0), new Vector2(0.5f, 0.5f));
                plateau.Vertex(S3(a, t0, h0, g0 + Plateau), G(a, t0), new Vector2(0.5f, 0.5f));
                plateau.Vertex(S3(a, t0, 0, g0 + Plateau + h0 * BackSlope), G(a, t0), new Vector2(0.5f, 0.5f));
                plateau.Vertex(S3(b, t1, h1, g1), G(b, t1), new Vector2(0.5f, 0.5f));
                plateau.Vertex(S3(b, t1, h1, g1 + Plateau), G(b, t1), new Vector2(0.5f, 0.5f));
                plateau.Vertex(S3(b, t1, 0, g1 + Plateau + h1 * BackSlope), G(b, t1), new Vector2(0.5f, 0.5f));
                plateau.Triangle(start, start + 1, start + 4); plateau.Triangle(start, start + 4, start + 3);
                plateau.Triangle(start + 1, start + 2, start + 5); plateau.Triangle(start + 1, start + 5, start + 4);
            }
            var rings = new SortedDictionary<int, UbwBuilder3[]>();
            double toSun = Math.Sign(-W.Sun.x);
            if (toSun == 0) toSun = 1;
            void Put(int row, double x, double z, double len, C.Weapon q, double zp)
            {
                Builder rimCopy = Scratch("UBW reveal crest rim copy"), body = Scratch("UBW reveal crest sword");
                UbwCrestBake.WeaponInto(rimCopy, x + toSun * .03, z + .018, len, q.Lean, q.Type, .035);
                UbwCrestBake.WeaponInto(body, x, z, len, q.Lean, q.Type);
                int ring = R.RingOf((float)Math.Sqrt(x * x + zp * zp));
                if (!rings.TryGetValue(ring, out UbwBuilder3[] pair)) rings[ring] = pair = new[] { new UbwBuilder3(), new UbwBuilder3(), new UbwBuilder3(), new UbwBuilder3() };
                pair[row * 2].OnPlane(rimCopy, (float)zp);
                pair[row * 2 + 1].OnPlane(body, (float)zp);
            }
            void Place(double density, int seed, Action<double, int> fn)
            {
                double gap = 1 / density;
                for (int j = (int)Math.Floor(-C.Span / gap); j <= C.Span / gap; j++)
                    if (UbwTerrain.Hash(j, 1, seed) < .85) fn((j + .8 * UbwTerrain.Hash(j, 2, seed)) * gap, j);
            }
            Place(perCell * .8, 151, (x, j) =>
            {
                C.Weapon q = C.WeaponOf(j, 151);
                Put(0, x, TopZ(x) - .2 - .6 * UbwTerrain.Hash(j, 7, 151), q.Tall * C.Lift * q.Short * C.SwordScale * 1.1, q, GroundZ(x) + BackDepth0 + BackDepth1 * UbwTerrain.Hash(j, 10, 151));
            });
            Place(perCell, 153, (x, j) =>
            {
                C.Weapon q = C.WeaponOf(j, 153);
                Put(1, x, TopZ(x) - .05, q.Tall * C.SwordScale * C.Lift * q.Short, q, GroundZ(x));
            });
            var k = new Crest3 { Face = face.Take("UBW reveal crest face"), Rim = rim.Take("UBW reveal crest rim"), Plateau = plateau.Take("UBW reveal crest plateau") };
            foreach (KeyValuePair<int, UbwBuilder3[]> kv in rings)
            {
                int ring = kv.Key;
                UbwBuilder3[] p = kv.Value;
                k.Rings.Add((ring, new[] { p[0].Take("UBW reveal crest back rim " + ring), p[1].Take("UBW reveal crest back " + ring) },
                    new[] { p[2].Take("UBW reveal crest top rim " + ring), p[3].Take("UBW reveal crest top " + ring) }));
            }
            return k;
        });

        /// <summary>The crest, as v4 draws it: the flat top, the thicket, the face, the rim, the swords on the top (each ring once the fire has reached it).</summary>
        internal static void CrestDraw(UbwShot shot, UbwRevealScene W, Vector3 at, float fire)
        {
            Crest3 k = Crest(W);
            shot.Draw(k.Plateau, PlateauColour, VfxDraw.solid, at);
            foreach (var (ring, back, _) in k.Rings)
            {
                float up = R.RiseOf(ring, fire);
                shot.Draw(back[0], VfxDraw.Fade(UbwCrestBake.RimLight, 0.35f), VfxDraw.solid, at, UbwDepth.NoWrite, up);
                shot.Draw(back[1], UbwCrestBake.BackBody, VfxDraw.solid, at, UbwDepth.Write, up);
            }
            shot.Draw(k.Face, W.Tint, UbwCrestBake.faceMat, at);
            shot.Draw(k.Rim, VfxDraw.Fade(UbwCrestBake.RimLight, 0.45f), VfxDraw.solid, at, UbwDepth.Over);
            foreach (var (ring, _, top) in k.Rings)
            {
                float up = R.RiseOf(ring, fire);
                shot.Draw(top[0], VfxDraw.Fade(UbwCrestBake.RimLight, 0.6f), VfxDraw.solid, at, UbwDepth.NoWrite, up);
                shot.Draw(top[1], UbwCrestBake.Silhouette, VfxDraw.solid, at, UbwDepth.Write, up);
            }
        }

        // ---- the field ---------------------------------------------------------------------------------------------

        /// <summary>
        /// The field in 3D: the swords' blades north first, cut into a part per figure (the swords whose screen foot is
        /// north of the figure come before it), each part with the ring of every vertex so it can rise ring by ring;
        /// the marks and shadows by ring.
        /// </summary>
        internal sealed class Field3
        {
            public readonly List<(float z, UbwMesh3 baked, int[] vertexRing, UbwMesh3 live)> Parts = new List<(float, UbwMesh3, int[], UbwMesh3)>();
            public readonly List<(int ring, UbwMesh3 marks, UbwMesh3 shadows)> Rings = new List<(int, UbwMesh3, UbwMesh3)>();
            public int MaxRing;
        }

        internal static Field3 Field(UbwRevealScene W, IList<float> cutsNorthFirst) => W.Get("field " + string.Join(",", cutsNorthFirst), () =>
        {
            var f = new Field3();
            var ground = new SortedDictionary<int, (UbwBuilder3 marks, UbwBuilder3 shadows)>();
            var blades = new UbwBuilder3();
            var vertexRing = new List<int>();
            int cut = 0;
            void Close(float z)
            {
                f.Parts.Add((z, blades.Take("UBW reveal blades " + f.Parts.Count), vertexRing.ToArray(), new UbwMesh3("UBW reveal blades live " + f.Parts.Count)));
                blades.Clear();
                vertexRing.Clear();
            }
            foreach (UbwSword sw in W.Swords)
            {
                float foot = (float)(sw.Z + sw.Lift);
                while (cut < cutsNorthFirst.Count && foot <= cutsNorthFirst[cut]) Close(cutsNorthFirst[cut++]);
                int ring = R.RingOf((float)Math.Sqrt(sw.X * sw.X + sw.Z * sw.Z));
                f.MaxRing = Mathf.Max(f.MaxRing, ring);
                if (!ground.TryGetValue(ring, out var g)) ground[ring] = g = (new UbwBuilder3(), new UbwBuilder3());
                int before = blades.Count;
                UbwFieldBake.SwordInto3(sw, W.Sun, blades, g.marks, g.shadows);
                for (int i = before; i < blades.Count; i++) vertexRing.Add(ring);
            }
            while (cut < cutsNorthFirst.Count) Close(cutsNorthFirst[cut++]);
            Close(float.NegativeInfinity);
            foreach (var kv in ground) f.Rings.Add((kv.Key, kv.Value.marks.Take("UBW reveal marks " + kv.Key), kv.Value.shadows.Take("UBW reveal sword shadows " + kv.Key)));
            return f;
        });

        /// <summary>A part of the field's blades with each ring risen so far (up 0: gone, collapsed to a point), or the baked part once all have risen.</summary>
        internal static UbwMesh3 Risen((float z, UbwMesh3 baked, int[] vertexRing, UbwMesh3 live) part, float fire)
        {
            if (float.IsPositiveInfinity(fire)) return part.baked;
            Vector3[] from = part.baked.Xyz;
            UbwMesh3 live = part.live;
            if (live.Xyz == null || live.Xyz.Length != from.Length)
            {
                live.Xyz = new Vector3[from.Length];
                live.Game = part.baked.Game;
                live.Uv = part.baked.Uv;
                live.Tri = part.baked.Tri;
            }
            bool all = true;
            int lastRing = int.MinValue;
            float up = 1f;
            for (int i = 0; i < from.Length; i++)
            {
                int ring = part.vertexRing[i];
                if (ring != lastRing) { up = R.RiseOf(ring, fire); lastRing = ring; }
                if (up < 1f) all = false;
                live.Xyz[i] = up > 0f ? new Vector3(from[i].x, from[i].y * up, from[i].z) : Vector3.zero;
            }
            if (all) return part.baked;
            live.Version++;
            return live;
        }

        /// <summary>The field's marks and shadows by ring, faded in as each ring rises; then the blades north first with the figures among them.</summary>
        internal static void FieldDraw(UbwShot shot, UbwRevealScene W, Vector3 at, float fire, List<UbwRevealFigure> figures)
        {
            var cuts = new List<float>();
            figures.Sort((p, q) => q.Z.CompareTo(p.Z));
            foreach (UbwRevealFigure fig in figures) cuts.Add(fig.Z);
            Field3 f = Field(W, cuts);
            foreach (var (ring, marks, shadows) in f.Rings)
            {
                float up = R.RiseOf(ring, fire);
                if (up <= 0f) continue;
                shot.Draw(shadows, VfxDraw.Fade(Black, 0.42f * W.Strength / 0.32f * up), W.Set.Atlas, at, UbwDepth.Over);
                shot.Draw(marks, VfxDraw.Fade(W.Tint, up), W.Set.Atlas, at, UbwDepth.Over);
            }
            for (int p = 0; p < f.Parts.Count; p++)
            {
                shot.Draw(Risen(f.Parts[p], fire), W.Tint, W.Set.Atlas, at);
                if (p < figures.Count)
                    foreach (var (mesh, colour, material, depth) in figures[p].Parts) shot.Draw(mesh, colour, material, at, depth);
            }
        }

        // ---- on the ground -----------------------------------------------------------------------------------------

        /// <summary>A flat quad on the ground (a sprite of the map camera), at x, z from the caster, w by h, its game position where it lies.</summary>
        internal static void GroundQuad(UbwBuilder3 into, float x, float z, float w, float h)
        {
            int b = into.Count;
            for (int k = 0; k < 4; k++)
            {
                float cx = k < 2 ? -0.5f : 0.5f, cz = k == 1 || k == 2 ? 0.5f : -0.5f;
                into.Vertex(new Vector3(x + cx * w, 0f, z + cz * h), new Vector2(x + cx * w, z + cz * h), new Vector2(cx + 0.5f, cz + 0.5f));
            }
            into.Triangle(b, b + 1, b + 2);
            into.Triangle(b, b + 2, b + 3);
        }

        private static UbwMesh3 Quad(UbwRevealScene W, string key, float x, float z, float w, float h) => W.Get("quad " + key, () =>
        {
            var b = new UbwBuilder3();
            GroundQuad(b, x, z, w, h);
            return b.Take("UBW reveal " + key);
        });

        /// <summary>The light on the ground, lying on the plates: the crest's shadow and its swords' (once the ring at the edge has risen), the faint gear shadows, the sun's warm glow, the hill's light and shade.</summary>
        internal static void Flats(UbwShot shot, UbwRevealScene W, Vector3 at, float s, float fire)
        {
            float rise = R.RiseOf(R.RingOf(W.North + 1), fire);
            UbwMesh3 shade = W.Get("crest shade", () => { var b = new UbwBuilder3(); b.Ground(W.K.Shade, Vector2.zero, 0f); return b.Take("UBW reveal crest shade"); });
            UbwMesh3 bladeShade = W.Get("crest blade shade", () => { var b = new UbwBuilder3(); b.Ground(W.K.BladeShade, Vector2.zero, 0f); return b.Take("UBW reveal crest blade shade"); });
            shot.Draw(shade, VfxDraw.Fade(Black, 0.4f * W.Strength * rise / 0.32f), VfxDraw.solid, at, UbwDepth.Over);
            shot.Draw(bladeShade, VfxDraw.Fade(Black, 0.22f * W.Strength * rise / 0.32f), VfxDraw.solid, at, UbwDepth.Over);
            for (int i = 0; i < 3; i++)
            {
                Mesh gear = B.GearShadow(i, Vector2.zero, s, W.Sun, W.North, out Vector2 to, out float r);
                var b = new UbwBuilder3();
                Vector3[] v = gear.vertices;
                for (int k = 0; k < v.Length; k++) b.Vertex(new Vector3(to.x + v[k].x * r, 0f, to.y + v[k].z * r), new Vector2(to.x + v[k].x * r, to.y + v[k].z * r), new Vector2(0.5f, 0.5f));
                b.Tri.AddRange(gear.triangles);
                shot.Draw(b.Into(UbwRevealSky.Live(W, "gear shadow " + i)), VfxDraw.Fade(Black, (float)C.GearShadows), VfxDraw.solid, at, UbwDepth.Over);
            }
            Vector2 sunward = UbwWorldGraphics.ToSun(W.Sun);
            float hill = (float)UbwField.Look.Hill, twilight = (float)UbwField.Twilight;
            shot.Draw(Quad(W, "sun glow", sunward.x * 24f, sunward.y * 24f, 80f, 80f), VfxDraw.Fade(Twilight, 0.14f * twilight), VfxDraw.glow, at, UbwDepth.Over);
            shot.Draw(Quad(W, "hill light", sunward.x * hill * 0.25f, sunward.y * hill * 0.25f, hill * 2.3f, hill * 2.3f), VfxDraw.Fade(Sunset, 0.2f), VfxDraw.glow, at, UbwDepth.Over);
            shot.Draw(Quad(W, "hill shade", -sunward.x * hill * 0.45f, -sunward.y * hill * 0.45f, hill * 2.1f, hill * 1.9f), VfxDraw.Fade(DuskDark, 0.5f), VfxDraw.soft, at, UbwDepth.Over);
        }

        // ---- the stand-ins ---------------------------------------------------------------------------------------------

        /// <summary>
        /// The preview's figures: the lab's stand-in discs (lib/goku.js pawn) for the caster and the three landed pawns,
        /// standing up (height = drawn z / Lift), each a little nearer the camera than the one before, with a shadow blob
        /// lying on the plate. Game positions: the discs as the map camera would draw them.
        /// </summary>
        internal static List<UbwRevealFigure> StandIns(UbwRevealScene W) => W.Get("stand-ins", () =>
        {
            var list = new List<UbwRevealFigure>();
            Color warm(Color c) => Color.Lerp(c, Twilight, 0.3f * (float)UbwField.Twilight);
            Color[] colours = { new Color(0.55f, 0.3f, 0.24f), PreviewAlly, PreviewEnemy, PreviewEnemy };
            for (int p = 0; p < W.Keep.Length && p < colours.Length; p++)
            {
                float px = (float)W.Keep[p].X, pz = (float)W.Keep[p].Z, h = (float)W.T.HeightAt(px, pz);
                var fig = new UbwRevealFigure { X = px, Z = pz };
                var shadow = new UbwBuilder3();
                float sx = px + W.Sun.x * 0.45f, sz = pz + W.Sun.y * 0.45f;
                int b0 = shadow.Count;
                for (int k = 0; k < 4; k++)
                {
                    float cx = k < 2 ? -0.5f : 0.5f, cz = k == 1 || k == 2 ? 0.5f : -0.5f;
                    shadow.Vertex(new Vector3(sx + cx * 0.85f, h, sz + cz * 0.4f), new Vector2(sx + cx * 0.85f, sz + cz * 0.4f), new Vector2(cx + 0.5f, cz + 0.5f));
                }
                shadow.Triangle(b0, b0 + 1, b0 + 2);
                shadow.Triangle(b0, b0 + 2, b0 + 3);
                fig.Parts.Add((shadow.Take("UBW reveal pawn shadow " + p), VfxDraw.Fade(VfxDraw.Ink, W.Strength), VfxDraw.soft, UbwDepth.Over));
                // [z, rx, rz, colour] of the body, the head and (the caster) the hair.
                var discs = new List<(float cz, float rx, float rz, Color colour)> { (0.18f, 0.22f, 0.32f, warm(colours[p])), (0.58f, 0.16f, 0.17f, warm(Skin)) };
                if (p == 0) discs.Add((0.69f, 0.19f, 0.1f, Hair));
                for (int k = 0; k < discs.Count; k++)
                {
                    var (cz, rx, rz, colour) = discs[k];
                    var d = new UbwBuilder3();
                    float zk = pz - 0.01f * (k + 1);
                    d.Vertex(new Vector3(px, h + cz / Lift, zk), new Vector2(px, pz + cz), new Vector2(0.5f, 0.5f));
                    const int n = 40;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)n * Mathf.PI * 2f, x = Mathf.Cos(t), z = Mathf.Sin(t);
                        d.Vertex(new Vector3(px + rx * x, h + (cz + rz * z) / Lift, zk), new Vector2(px + rx * x, pz + cz + rz * z), new Vector2(0.5f, 0.5f));
                    }
                    for (int i = 0; i < n; i++) d.Triangle(0, 1 + i, 1 + (i + 1) % n);
                    fig.Parts.Add((d.Take("UBW reveal pawn " + p + " " + k), colour, VfxDraw.solid, UbwDepth.Write));
                }
                list.Add(fig);
            }
            return list;
        });

        /// <summary>The stand-ins' colours (lib/goku.js, lib/flying-thunder-god.js): an ally's and an enemy's clothes, skin, the caster's hair.</summary>
        private static readonly Color PreviewAlly = new Color(0.39f, 0.58f, 0.65f), PreviewEnemy = new Color(0.55f, 0.38f, 0.27f), Skin = new Color(0.83f, 0.7f, 0.54f), Hair = new Color(0.06f, 0.05f, 0.05f);
    }
}
