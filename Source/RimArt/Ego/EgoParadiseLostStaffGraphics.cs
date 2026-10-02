using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.EgoParadiseLostGraphics;
using T = RimArt.EgoParadiseLostTiming;
using Taper = RimArt.GokuGraphics.Taper;

namespace RimArt
{
    /// <summary>
    /// The staff, the port of staff() in ego-paradise-lost-v2.js (the Lobotomy weapon sprite): held upright in
    /// the right hand, gold tip on the floor, 2.2 lab cells tall. A thin white shaft with a grey outline and a lit
    /// stripe; a snake winding the whole shaft in 3.5 wide loops, its head at the apple; a fan of scale feathers
    /// beside the apple on the outer side, three rows of 4, 6 and 8, darker toward the root; a red apple with a
    /// stem; a gold ring of 10 thorns round the shaft above it; a shadow along the sun. On a shot or a ring the
    /// apple and the halo flash (the flash argument, 1 to 0).
    ///
    /// Upright, so it is a line north on screen for every facing. Only its place changes: at the right hand,
    /// 0.27 cells out facing north or south, 0.22 out and 0.2 ahead facing east or west (the front edge of the
    /// body, clear of the face). A staff north of the pawn's centre (facing north or west) draws under the pawn,
    /// from <see cref="PawnBody.Under"/>; one south of it over the pawn, from <see cref="PawnBody.Over"/>, both against
    /// the pawn's own height (its DrawPos.y).
    /// The fan opens to the east except facing south, where the right hand is on the west.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class EgoParadiseLostStaffGraphics
    {
        private const float Tau = 6.2831855f;
        /// <summary>Points along the snake.</summary>
        private const int Coil = 57;

        /// <summary>
        /// The staff held by a pawn at <paramref name="wielder"/> (its DrawPos, <paramref name="body"/> its y) facing
        /// <paramref name="facing"/>.
        /// </summary>
        public static void Draw(Vector2 wielder, float body, Rot4 facing, float flash, Map map)
        {
            if (!Shown(wielder, map)) return;
            Begin(wielder);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            Draw(wielder, body, facing, flash, sun, strength);
        }

        private static void Draw(Vector2 pos, float body, Rot4 facing, float flash, Vector2 sun, float strength)
        {
            // Ahead and to the right, exact so the side is never decided by a rounding error.
            Vector2 d = facing == Rot4.East ? new Vector2(1f, 0f) : facing == Rot4.North ? new Vector2(0f, 1f) : facing == Rot4.West ? new Vector2(-1f, 0f) : new Vector2(0f, -1f);
            var right = new Vector2(d.y, -d.x);
            bool sideways = facing.IsHorizontal;
            Vector2 g = pos + right * (sideways ? 0.22f : 0.27f) + d * (sideways ? 0.2f : 0.08f);
            bool behind = g.y > pos.y + 0.02f;
            float L = body + (behind ? PawnBody.Under : PawnBody.Over), out1 = right.x >= 0f ? 1f : -1f;
            Vector2 at = Scr(g, 0f), top = Scr(g, T.StaffH);

            Line2(Shd(g, 0f, sun), Shd(g, T.StaffH, sun), 0.07f, Fade(RedInk, strength * 0.5f), solid, ShadowLayer, Taper.End);
            // The gold tip into the floor, then the shaft with its outline and a lit stripe.
            Band(new Vector2(at.x - 0.03f, at.y + 0.07f), new Vector2(at.x + 0.03f, at.y + 0.07f), new Vector2(at.x, at.y - 0.05f), new Vector2(at.x, at.y - 0.05f), Gold, L);
            Pts[0] = at;
            Pts[1] = top;
            W[0] = W[1] = 0.028f;
            Tube(2, 0.014f, ShaftLine, L + 0.0002f);
            Tube(2, 0f, Shaft, L + 0.0004f);
            Tube(2, 0f, Fade(White, 0.7f), L + 0.0006f, 0.2f, 0.8f);

            // The snake: wide loops down the whole shaft, its head at the apple.
            Vector2[] coil = GokuGraphics.Points(Coil);
            for (int i = 0; i < Coil; i++)
            {
                float u = i / (float)(Coil - 1);
                coil[i] = new Vector2(SnakeX(g.x, u), g.y + PawnBody.Ground + Mathf.Lerp(0.12f, 2.0f, u) * T.Lift);
            }
            GokuGraphics.Line(coil, 0.056f, ShaftLine, solid, L + 0.0008f, Taper.None);
            GokuGraphics.Line(coil, 0.036f, Snake, solid, L + 0.001f, Taper.None);
            var head = new Vector2(SnakeX(g.x, 1f) + 0.03f * out1, g.y + PawnBody.Ground + 2.04f * T.Lift);
            Disc(head, L + 0.0012f, 0.048f, 0.034f, 0f, ShaftLine);
            Disc(head, L + 0.0013f, 0.039f, 0.026f, 0f, Snake);
            Disc(new Vector2(head.x + 0.014f * out1, head.y + 0.006f), L + 0.0014f, 0.009f, 0.009f, 0f, Eye);

            Fan(new Vector2(g.x + 0.03f * out1, g.y + PawnBody.Ground + 1.98f * T.Lift), out1, L);

            // The apple and its stem; the halo of thorns round the shaft just above it (a level circle).
            var apple = new Vector2(g.x, g.y + PawnBody.Ground + 2.12f * T.Lift);
            var halo = new Vector2(g.x, g.y + PawnBody.Ground + 2.3f * T.Lift);
            PaperBombGraphics.RingAt(halo, 0.13f, Gold, L + 0.0024f, false, solid);
            for (int i = 0; i < 10; i++)
            {
                Vector2 dd = Turn(i * 36f + 8f), n = Left(dd), b = halo + dd * 0.12f;
                Spike(b, n, 0.018f, halo + dd * 0.2f, Gold, L + 0.0025f + i * 0.00001f);
            }
            Disc(apple, L + 0.003f, 0.088f, 0.082f, 0f, RedInk);
            Disc(apple, L + 0.0032f, 0.075f, 0.07f, 0f, Apple);
            Disc(new Vector2(apple.x - 0.025f, apple.y + 0.025f), L + 0.0034f, 0.025f, 0.02f, 0f, AppleLit);
            Line2(new Vector2(apple.x, apple.y + 0.06f), new Vector2(apple.x + 0.02f, apple.y + 0.11f), 0.018f, Stem, solid, L + 0.0035f, Taper.None);
            if (flash > 0f)
            {
                Sprite(apple, 0.7f * flash, 0.7f * flash, Fade(StarGlow, 0.75f * flash), glow, Overhead + 0.1f);
                Sprite(halo, 0.55f * flash, 0.45f * flash, Fade(GoldLit, 0.6f * flash), glow, Overhead + 0.101f);
            }
        }

        private static float SnakeX(float x, float u) => x + T.SnakeLoop * Mathf.Sin(u * T.SnakeTurns * Tau + 0.6f);

        /// <summary>
        /// The wing on the staff (v2, the Lobotomy sprite): a fan of scale feathers from <paramref name="root"/>
        /// beside the apple on the outer side, about a third of the staff long. Three rows of 4, 6 and 8 feathers,
        /// from 100 degrees down to 15 up, grey at the root to white outside; the outer rows drawn first.
        /// </summary>
        private static void Fan(Vector2 root, float out1, float L)
        {
            for (int r = 2; r >= 0; r--)
            {
                int n = 4 + 2 * r;
                float R = T.WingRows[r], fl = 0.1f + 0.025f * r;
                Color tone = Color.Lerp(WingGrey, Feather, 0.25f + 0.35f * r);
                for (int j = 0; j < n; j++)
                {
                    float a = Mathf.Lerp(-100f, 15f, j / (float)(n - 1)) + 6f * (Rand(r * 13 + j + 900) - 0.5f);
                    var dd = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad) * out1, Mathf.Sin(a * Mathf.Deg2Rad));
                    Vector2 b = root + dd * (R - fl * 0.6f);
                    Pts[0] = b;
                    Pts[1] = b + dd * (fl * 0.5f);
                    Pts[2] = b + dd * fl;
                    for (int k = 0; k < 3; k++) W[k] = 0.05f * Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Pow(k / 2f, 0.55f)));
                    float lay = L + 0.0016f + (2 - r) * 0.0003f + j * 0.00002f;
                    Tube(3, 0.012f, ShaftLine, lay);
                    Tube(3, 0f, tone, lay + 0.00005f);
                }
            }
        }
    }
}
