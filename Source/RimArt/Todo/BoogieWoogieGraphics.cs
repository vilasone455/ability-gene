using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.TodoGraphics;
using B = RimArt.BoogieWoogie;
using Taper = RimArt.GokuGraphics.Taper;

namespace RimArt
{
    /// <summary>
    /// Draws the Boogie Woogie clap, the port of the lab's todo-boogie-woogie.js: the burst of teal
    /// dashes out of the palms at each contact, the two ink frames on white at both ends when they
    /// swap, and the teal crescents and flecks round whoever arrived. The stand-in pawns and Todo's
    /// clip are not drawn here; the white covers the real pawn for the two frames.
    ///
    /// The burst, crescents and flecks are level rings and the ink figures stand upright on screen
    /// like the pawn they cover, so there is one drawing for every facing. Every routine takes an
    /// age and keeps no state.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class BoogieWoogieGraphics
    {
        private static readonly Vector2[] jagged = new Vector2[5], arc = new Vector2[9];

        /// <summary>
        /// "Pan": 16 dashes and 6 drops burst out of the palms in a level ring, <paramref name="age"/>
        /// seconds after a contact. <paramref name="scale"/> is 1 for the swap's clap and 0.6 for a
        /// Double Clap's first.
        /// </summary>
        internal static void PanBurst(Vector2 palms, float age, float scale)
        {
            if (age < 0f || age >= B.Life) return;
            Begin(palms);
            for (int i = 0; i < B.Dashes + B.Drops; i++)
            {
                float late = i % 5 < 2 ? B.SecondWave : 0f, a = age - late;
                if (a < 0f) continue;
                float u = Mathf.Clamp01(a / (B.Life - late)), fly = 1f - Mathf.Pow(1f - Mathf.Clamp01(u / 0.45f), 3f);
                float f = 1f - Smooth((u - 0.55f) / 0.45f);
                bool dash = i < B.Dashes;
                int k = dash ? i : i - B.Dashes, n = dash ? B.Dashes : B.Drops;
                float ang = (k + Rand(i + 3) * 0.6f) / n * Mathf.PI * 2f + B.Curl * fly;
                float reach = B.Reach * scale * (0.6f + 0.4f * Rand(i + 5)), r = Mathf.Lerp(B.BurstStart * scale, reach, fly) + 0.08f * scale * u;
                var at = new Vector2(palms.x + Mathf.Cos(ang) * r, palms.y + Mathf.Sin(ang) * r);
                if (!dash)
                {
                    float size = 0.065f * scale * (1f - 0.5f * u);
                    DrawMesh(disc, at, Overhead + 0.132f, size, size, 0f, Fade(Outline, 0.85f * f), solid);
                    DrawMesh(disc, at, Overhead + 0.133f, size * 0.6f, size * 0.6f, 0f, Fade(Teal, f), whiteGlow);
                    continue;
                }
                // Each dash turns from pointing out toward pointing round as it flies, so the burst swirls.
                float dir = ang + B.Turn * fly, len = (0.2f + 0.2f * Rand(i + 7)) * scale * (1f - 0.5f * Smooth(u));
                var b = new Vector2(at.x + Mathf.Cos(dir) * len, at.y + Mathf.Sin(dir) * len);
                Vector2[] pts = Dash(at, b);
                Line(pts, 0.14f * scale, Fade(Outline, 0.85f * f), null, Overhead + 0.13f, Taper.Both);
                Line(pts, 0.075f * scale, Fade(Teal, f), whiteGlow, Overhead + 0.131f, Taper.Both);
            }
            float ring = Mathf.Clamp01(age / (B.Life * 0.6f));
            if (ring < 1f)
                PaperBombGraphics.RingAt(palms, Mathf.Lerp(0.2f, B.RingReach, 1f - (1f - ring) * (1f - ring)) * scale,
                    Fade(TealPale, 0.25f * (1f - Smooth(ring))), Overhead + 0.125f, false, whiteGlow);
        }

        /// <summary>
        /// S1's swap at one end (<paramref name="pos"/> is its ground), <paramref name="age"/> seconds
        /// after it: two frames of white with the fighter as black ink shards, who stood there in the
        /// first and who stands there now in the second, then the white fades.
        /// </summary>
        internal static void InkFrame(Vector2 pos, float age, BoogieEnd before, BoogieEnd after)
        {
            if (age < 0f || age >= B.Ink + B.InkFade) return;
            Begin(pos);
            float white = age < B.Ink ? 1f : 1f - Smooth((age - B.Ink) / B.InkFade);
            var c = new Vector2(pos.x, pos.y + PawnFit.Y(0.3f));
            Sprite(c, 3.2f, 3.2f, Fade(White, 0.5f * white), glow, Overhead + 0.079f);
            Sprite(c, 2.6f, 2.6f, Fade(White, 0.9f * white), soft, Overhead + 0.08f);
            Sprite(c, 1.5f, 1.5f, Fade(White, white), soft, Overhead + 0.081f);
            // Hides the pawn under it. The sketch's 1.1 x 1.65 covers its small stand-in; a real pawn is
            // drawn larger (feet to hair about -0.5 to +0.9 cells), so the game's cover is wider, taller
            // and three deep, or the legs show under the ink figure.
            for (int k = 0; k < 3; k++) Sprite(new Vector2(pos.x, pos.y + 0.2f), 1.6f, 2.3f, Fade(White, white), soft, Overhead + 0.082f + k * 0.0005f);
            if (age >= B.Ink) return;
            int frame = age < B.Ink / 2f ? 0 : 1;
            BoogieEnd who = frame == 1 ? after : before;
            if (who == BoogieEnd.None) return;
            int seed = frame * 131 + Mathf.FloorToInt(pos.x * 7f + pos.y * 3f + 0.5f) * 17;
            if (who == BoogieEnd.Stone)
            {
                for (int i = 0; i < 4; i++)
                {
                    float ang = i * 0.8f + Rand(seed + i) * 0.6f, l = 0.12f + 0.08f * Rand(seed + i + 5);
                    var d = new Vector2(Mathf.Cos(ang) * l, Mathf.Sin(ang) * l);
                    Jag(seed, i, pos - d, pos + d, 0.07f);
                }
                return;
            }
            // The body as tall jagged strokes, tallest in the middle where the head is; shards flare off it.
            // In game the figure is a real pawn's size and height (PawnFit).
            float k1 = PawnFit.Body;
            for (int i = 0; i < B.InkStrokes; i++)
            {
                float v = i / (float)(B.InkStrokes - 1) - 0.5f, x = pos.x + (v * 0.5f + (Rand(seed + i) - 0.5f) * 0.05f) * k1;
                float top = pos.y + PawnFit.Y(0.8f - Mathf.Abs(v) * 0.6f + (Rand(seed + i + 20) - 0.5f) * 0.1f), bottom = pos.y + PawnFit.Y(-0.08f - Rand(seed + i + 40) * 0.1f);
                Jag(seed, i, new Vector2(x, bottom), new Vector2(x + (Rand(seed + i + 50) - 0.5f) * 0.06f * k1, top), (0.06f + 0.05f * Rand(seed + i + 60)) * k1);
            }
            for (int i = 0; i < B.InkShards; i++)
            {
                float side = i % 2 == 1 ? 1f : -1f, h = 0.1f + 0.5f * Rand(seed + i + 70);
                Vector2 from = PawnFit.At(pos, side * 0.18f, h);
                Jag(seed, B.InkStrokes + i, from,
                    new Vector2(from.x + side * (0.15f + 0.18f * Rand(seed + i + 80)) * k1, from.y + (0.08f + 0.2f * Rand(seed + i + 90)) * k1), 0.05f);
            }
        }

        private static void Jag(int seed, int id, Vector2 from, Vector2 to, float width)
        {
            for (int j = 0; j <= 4; j++)
            {
                float v = j / 4f, side = j % 2 == 1 ? 1f : -1f, off = j > 0 && j < 4 ? side * (0.02f + 0.04f * Rand(seed + id * 9 + j)) : 0f;
                jagged[j] = new Vector2(Mathf.Lerp(from.x, to.x, v) + off, Mathf.Lerp(from.y, to.y, v));
            }
            Line(jagged, width, Outline, null, Overhead + 0.09f + id * 0.0002f, Taper.Both);
        }

        /// <summary>
        /// S2's arrival round whoever arrived at <paramref name="pos"/>, <paramref name="age"/> seconds
        /// after the swap: teal crescents swirl round them, then break into flecks that drift out and hang.
        /// </summary>
        internal static void Arrival(Vector2 pos, float age)
        {
            // Round a real pawn in game: centred on its body and sized to it (PawnFit).
            var c = new Vector2(pos.x, pos.y + PawnFit.Y(0.35f));
            Begin(c);
            float sw = age - B.SwooshFrom;
            if (sw >= 0f && sw < B.SwooshLife)
            {
                float u = sw / B.SwooshLife, f = 1f - Smooth((u - 0.5f) / 0.5f), r = (0.3f + 0.35f * Smooth(u)) * PawnFit.Body, span = 1.9f * (1f - 0.4f * u);
                for (int k = 0; k < 3; k++)
                {
                    float start = k * 2.094f + Rand(k + 80) * 0.6f + u * 5.5f;
                    for (int j = 0; j <= 8; j++)
                    {
                        float a = start + span * j / 8f;
                        arc[j] = new Vector2(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r * 0.85f);
                    }
                    Line(arc, 0.13f, Fade(Outline, 0.8f * f), null, Overhead + 0.1f, Taper.Both);
                    Line(arc, 0.07f, Fade(Teal, f), whiteGlow, Overhead + 0.101f, Taper.Both);
                }
            }
            float fa = age - B.FleckFrom;
            if (fa < 0f || fa >= B.Fleck) return;
            float fu = fa / B.Fleck, fly = 1f - Mathf.Pow(1f - Mathf.Clamp01(fu / 0.35f), 3f), ff = 1f - Smooth((fu - 0.5f) / 0.5f);
            for (int i = 0; i < B.Flecks; i++)
            {
                float ang = (i + Rand(i + 60) * 0.7f) / B.Flecks * Mathf.PI * 2f, r = (Mathf.Lerp(0.35f, 0.7f + 0.5f * Rand(i + 61), fly) + 0.1f * fu) * PawnFit.Body;
                float rot = ang + (Rand(i + 63) - 0.5f) * 2f + fu * 3f * (i % 2 == 1 ? 1f : -1f), size = (0.08f + 0.08f * Rand(i + 64)) * (1f - 0.4f * fu);
                var q = new Vector2(c.x + Mathf.Cos(ang) * r, c.y + Mathf.Sin(ang) * r * 0.8f + 0.12f * fu);
                var half = new Vector2(Mathf.Cos(rot) * size / 2f, Mathf.Sin(rot) * size / 2f);
                Vector2[] pts = Dash(q - half, q + half);
                Line(pts, 0.08f, Fade(Outline, 0.8f * ff), null, Overhead + 0.07f, Taper.Both);
                Line(pts, 0.04f, Fade(Teal, ff), whiteGlow, Overhead + 0.071f, Taper.Both);
            }
        }

        /// <summary>
        /// One whole clap, <paramref name="seconds"/> after its warmup began: the bursts at the palms
        /// and, from the swap, the ink frames at both ends and the arrivals. <paramref name="palmsGround"/>
        /// is where Todo stood when he clapped.
        /// </summary>
        internal static void Draw(Vector2 palmsGround, float seconds, float warmup, bool twice,
            Vector2 end0, BoogieEnd before0, BoogieEnd after0, Vector2 end1, BoogieEnd before1, BoogieEnd after1, Map map)
        {
            if (seconds < 0f || seconds >= B.Duration(warmup)) return;
            var palms = new Vector2(palmsGround.x, palmsGround.y + B.PalmsNorth(seconds, warmup, twice));
            if (Shown(palmsGround, map))
            {
                if (twice) PanBurst(palms, seconds - B.FirstContactAt(warmup), B.FirstScale);
                PanBurst(palms, seconds - warmup, 1f);
            }
            float age = seconds - warmup;
            if (age < 0f) return;
            if (Shown(end0, map))
            {
                InkFrame(end0, age, before0, after0);
                if (after0 == BoogieEnd.Pawn) Arrival(end0, age);
            }
            if (Shown(end1, map))
            {
                InkFrame(end1, age, before1, after1);
                if (after1 == BoogieEnd.Pawn) Arrival(end1, age);
            }
        }
    }
}
