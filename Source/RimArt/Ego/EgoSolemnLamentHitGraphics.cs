using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using G = RimArt.EgoSolemnLamentGraphics;
using T = RimArt.EgoSolemnLamentTiming;

namespace RimArt
{
    /// <summary>
    /// Solemn Lament's hits, as the Limbus frames draw them, on the target's chest: the white hit (a white
    /// splat with grey cracks breaking into see-through shards, no blood: the white shot does no body damage)
    /// and the black hit (an ink splat with red inside it, black slashes, droplets, chunks breaking off it,
    /// blood thrown along the shot and a floor spatter that stays). Also the ink splat meshes and the sliver
    /// the muzzles use.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class EgoSolemnLamentHitGraphics
    {
        /// <summary>
        /// A black ink splat, unit radius: 64 points round the centre, a ragged edge of small teeth and, about one
        /// point in five, a long sharp spike, so it reads as a splash and not a disc. Three variants; each shot
        /// picks one and turns it.
        /// </summary>
        internal static readonly Mesh[] Splats = { SplatMesh(1), SplatMesh(2), SplatMesh(3) };
        /// <summary>A sliver's four corners round its centre, as shares of its size: long and pointed along its turn, narrow across.</summary>
        private static readonly float[] SliverShape = { 1.6f, 0.45f, 0.8f, 0.4f };
        private const float Tau = 6.2831855f;

        private static Mesh SplatMesh(int seed)
        {
            const int N = 64;
            var vertices = new Vector3[N + 1];
            var triangles = new int[N * 3];
            for (int i = 0; i < N; i++)
            {
                float t = i / (float)N * Tau, r = (0.8f + 0.2f * Rand(seed * 71 + i * 3)) * (i % 2 == 1 ? 0.88f : 1f);
                if (Rand(seed * 131 + i) > 0.8f) r += 0.35f + 0.55f * Rand(seed * 53 + i);
                vertices[i + 1] = new Vector3(Mathf.Cos(t) * r, 0f, Mathf.Sin(t) * r);
                // The points go anticlockwise, so each fan triangle is written centre, next, this: clockwise on screen.
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = 1 + (i + 1) % N;
                triangles[i * 3 + 2] = 1 + i;
            }
            var mesh = new Mesh { name = "Solemn Lament splat " + seed, vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A jagged ink sliver round <paramref name="q"/>: four corners, long and pointed along <paramref name="rot"/> (radians), narrow across.</summary>
        internal static void Chunk(Vector2 q, float rot, float size, int seed, Color colour, float altitude)
        {
            Sides(2, out Vector2[] a, out Vector2[] b);
            a[0] = SliverCorner(q, rot, size, seed, 0);
            a[1] = SliverCorner(q, rot, size, seed, 1);
            b[0] = SliverCorner(q, rot, size, seed, 3);
            b[1] = SliverCorner(q, rot, size, seed, 2);
            Strip(a, b, colour, solid, altitude);
        }

        private static Vector2 SliverCorner(Vector2 q, float rot, float size, int seed, int k)
        {
            float t = rot + k * Mathf.PI / 2f + (Rand(seed * 5 + k) - 0.5f) * 0.5f, r = size * SliverShape[k] * (0.75f + 0.5f * Rand(seed * 7 + k));
            return q + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * r;
        }

        /// <summary>
        /// The white hit on chest <paramref name="c"/>, the black hit's shape in white (Skill 1 2.95-3.0 s): a solid
        /// white splat out to 0.38 cells (spikes to 0.75) in 0.04 s in a white bloom, eight thin grey cracks from
        /// its middle; from 0.05 s it shrinks away, opaque, gone by 0.25 s, and about fifteen see-through shards of
        /// uneven width and length (a third pointed), narrow at the middle and wide at the tip, fly out to about
        /// 1.2 cells and thin out by 0.35 s; six white sparks. Drawn over the black hit, so a white hit on fresh
        /// ink still shows.
        /// </summary>
        public static void WhiteHit(Vector2 c, float age, int seed, float shift)
        {
            if (age < 0f || age > T.WhiteHit) return;
            float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(age / 0.04f), 3f), R = 0.38f * grow * (1f - Mathf.Pow(Mathf.Clamp01((age - 0.05f) / 0.2f), 1.5f));
            float turn = Rand(seed + 900) * 360f;
            if (age < 0.2f) Sprite(c, 1.3f * grow, 1.2f * grow, Fade(G.White, 0.6f * (1f - age / 0.2f)), glow, Overhead + 0.093f + shift);
            for (int i = 0; i < 18; i++)
            {
                float u = Mathf.Clamp01((age - 0.03f) / (T.WhiteHit - 0.03f));
                if (age < 0.03f || u >= 1f || Rand(seed * 11 + i + 905) > 0.82f) continue;
                float t = i / 18f * Tau + (Rand(seed * 13 + i + 910) - 0.5f) * 0.5f, r0 = 0.12f + 0.9f * Mathf.Sqrt(u) * (0.6f + 0.4f * Rand(seed * 17 + i + 920));
                float len = (0.2f + 0.5f * Rand(seed * 19 + i + 930)) * (0.6f + 0.4f * u), wl = 0.03f + 0.14f * Rand(seed * 23 + i + 940), wr = 0.03f + 0.14f * Rand(seed * 29 + i + 950);
                var way = new Vector2(Mathf.Cos(t), Mathf.Sin(t));
                var normal = new Vector2(-way.y, way.x);
                bool pointed = Rand(seed * 41 + i + 955) > 0.65f;
                float tip = r0 + len * (pointed ? 1.25f : 1f);
                Sides(2, out Vector2[] a, out Vector2[] b);
                a[0] = c + way * r0 + normal * 0.02f;
                a[1] = pointed ? c + way * tip : c + way * (r0 + len) + normal * wl;
                b[0] = c + way * r0 - normal * 0.02f;
                b[1] = pointed ? c + way * tip : c + way * (r0 + len * (0.8f + 0.3f * Rand(seed * 43 + i + 958))) - normal * wr;
                Strip(a, b, Fade(G.Pale, 0.6f * Mathf.Pow(1f - u, 1.2f)), solid, Overhead + 0.0935f + shift);
            }
            if (R > 0.01f)
            {
                DrawMesh(Splats[(seed + 1) % 3], c, Overhead + 0.094f + shift, R, R, turn, G.White, solid);
                for (int i = 0; i < 8; i++)
                {
                    float t = Rand(seed * 31 + i + 960) * Tau, l = R * (0.4f + 0.4f * Rand(seed * 37 + i + 970));
                    Streak(c, c + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * l, 0.02f, Fade(G.Ash, 0.7f), solid, Overhead + 0.0945f + shift, 3);
                }
            }
            for (int i = 0; i < 6; i++)
            {
                float t = Rand(i + 120 + seed) * Tau, v = 1.5f + 2f * Rand(i + 130 + seed), f = 1f - age / T.WhiteHit;
                Sprite(c + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * (v * age), 0.06f, 0.06f, Fade(G.White, f), glow, Overhead + 0.0948f + shift);
            }
        }

        /// <summary>
        /// The black hit on chest <paramref name="c"/>: a solid black ink splat out to 0.36 cells (spikes to 0.7) in
        /// 0.06 s, a shade lighter in the middle; red inside it (a glow and seven thin streaks) for 0.16 s; three
        /// thin black slashes along the shot through the target for 0.15 s; ten droplets flung out. From 0.1 s the
        /// ink breaks up: the splat shrinks away (opaque, it does not fade) and fourteen slivers come off its edge,
        /// fly 1.2 to 3.4 cells/s, turn and shrink, all gone by 0.4 s. Blood is thrown on along the shot
        /// <paramref name="dir"/>, and a floor spatter by the target's feet (<paramref name="pos"/> is its point)
        /// stays.
        /// </summary>
        public static void BlackHit(Vector2 c, Vector2 pos, Vector2 dir, float age, int seed, float shift)
        {
            if (age < 0f) return;
            float aim = Mathf.Atan2(dir.y, dir.x);
            if (age < T.InkHit)
            {
                float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(age / 0.06f), 3f), brk = Mathf.Clamp01((age - 0.1f) / (T.InkHit - 0.1f));
                float R = 0.36f * grow * (1f - Mathf.Pow(brk, 1.5f));
                if (R > 0.01f)
                {
                    DrawMesh(Splats[seed % 3], c, Overhead + 0.09f + shift, R, R, Rand(seed + 300) * 360f, Fade(G.Ink, 0.95f), solid);
                    Sprite(c, R * 1.1f, R, Fade(G.Soot, 0.4f), soft, Overhead + 0.0902f + shift);
                }
                if (age < 0.16f)
                {
                    float f = 1f - age / 0.16f;
                    Sprite(c, 0.3f * grow, 0.28f * grow, Fade(G.Red, 0.6f * f), glow, Overhead + 0.0905f + shift);
                    for (int i = 0; i < 7; i++)
                    {
                        float t = Rand(seed * 7 + i + 310) * Tau, l = (0.1f + 0.2f * Rand(seed * 7 + i + 320)) * grow;
                        Streak(c, c + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * l, 0.035f, Fade(G.Red, 0.95f * f), solid, Overhead + 0.0906f + shift, 3);
                    }
                }
                for (int i = 0; i < 14; i++)
                {
                    float a = age - 0.08f - Rand(seed * 11 + i + 330) * 0.06f;
                    if (a < 0f) continue;
                    float u = a / (T.InkHit - 0.08f);
                    if (u >= 1f) continue;
                    float t = i / 14f * Tau + Rand(seed * 13 + i + 340) * 0.4f, v = 1.2f + 2.2f * Rand(seed * 17 + i + 350), d0 = 0.28f + v * a;
                    var q = new Vector2(c.x + Mathf.Cos(t) * d0, c.y + Mathf.Sin(t) * d0 * 0.9f - 1.5f * a * a);
                    Chunk(q, t + a * 3f * (Rand(seed + i + 370) - 0.5f), (0.06f + 0.08f * Rand(seed * 19 + i + 360)) * (1f - u), seed * 23 + i, Fade(G.Ink, 0.95f),
                        Overhead + 0.091f + shift);
                }
                for (int i = 0; i < 10; i++)
                {
                    float u = age / (0.3f + 0.1f * Rand(seed + i + 380));
                    if (u >= 1f) continue;
                    float t = Rand(seed * 29 + i + 390) * Tau, v = 2.5f + 3f * Rand(seed * 31 + i + 400), r = 0.035f * (1f - u * u);
                    DrawMesh(disc, new Vector2(c.x + Mathf.Cos(t) * v * age, c.y + Mathf.Sin(t) * v * age * 0.9f - 3f * age * age), Overhead + 0.0915f + shift, r, r, 0f, G.Ink, solid);
                }
                if (age < 0.15f)
                {
                    float f = 1f - age / 0.15f;
                    for (int i = 0; i < 3; i++)
                    {
                        float off = (Rand(seed * 37 + i + 410) - 0.5f) * 0.5f, a0 = -0.5f + age * 6f + Rand(i + 420) * 0.3f, len = 0.6f + 0.5f * Rand(i + 430);
                        var from = new Vector2(c.x + dir.x * a0 - dir.y * off, c.y + dir.y * a0 + dir.x * off);
                        Streak(from, from + dir * len, 0.03f, Fade(G.Ink, 0.9f * f), solid, Overhead + 0.0918f + shift, 4);
                    }
                }
            }
            for (int i = 0; i < 6; i++)
            {
                float life = 0.22f + Rand(i + 240) * 0.1f, u = age / life;
                if (u > 1f) continue;
                float a = aim + (Rand(i + 250) - 0.5f), v = 1.8f + Rand(i + 260) * 2.5f;
                Sprite(new Vector2(c.x + Mathf.Cos(a) * v * age, c.y + Mathf.Sin(a) * v * age - 5f * age * age), 0.12f, 0.09f, Fade(G.Blood, 1f - u * u), soft,
                    Overhead + 0.089f + shift);
            }
            float g = Mathf.Clamp01(age / 0.25f);
            Vector2 spatter = new Vector2(pos.x + (Rand(seed + 270) - 0.5f) * 0.3f, pos.y + PawnBody.Ground) + dir * (0.3f + Rand(seed + 280) * 0.3f);
            Sprite(spatter, 0.42f * g, 0.26f * g, Fade(G.Blood, 0.7f * g), soft, Floor + 0.02f + shift, aim * Mathf.Rad2Deg + (Rand(seed + 290) - 0.5f) * 40f);
        }
    }
}
