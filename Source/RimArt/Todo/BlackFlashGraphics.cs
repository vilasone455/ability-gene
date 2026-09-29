using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.TodoGraphics;
using F = RimArt.BlackFlash;
using Taper = RimArt.GokuGraphics.Taper;

namespace RimArt
{
    /// <summary>
    /// Draws Black Flash, the port of the lab's todo-black-flash.js: black sparks on the fist before
    /// it lands, the negative flash, the dark and the spark, the burst (black core, red ring, pale
    /// shards, red streaks along the punch, black clumps, black bolts with red edges in two waves,
    /// black sparks, red specks), the stun stars and the sparks on Todo in the zone; and the
    /// ordinary punch. The stand-in pawns and the arm are not drawn here (no punch clip yet).
    ///
    /// Everything lies flat at chest height and turns with the aim, so there is one drawing for every
    /// facing. Shapes come from a hash of the strike number or redraw step, never from state.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class BlackFlashGraphics
    {
        internal static readonly Color Ink = new Color(0.02f, 0.015f, 0.02f), Red = new Color(0.95f, 0.08f, 0.1f),
            RedPale = new Color(1f, 0.6f, 0.55f), SparkPale = new Color(1f, 0.86f, 0.9f), Shard = new Color(1f, 0.8f, 0.85f);

        // Ink splats: irregular outlines of 9 corners, a few of them spikes, built once.
        private static readonly Mesh[] blobs = MakeBlobs();
        private static readonly Mesh negativeDisc = MakeDisc(48, "Black Flash negative disc");
        // Scratch points: a bolt, the bolt it was a strike ago, its fork, and small bolts.
        private static readonly Vector2[] cur = new Vector2[64], ghost = new Vector2[64], fork = new Vector2[64], small = new Vector2[64];
        private static readonly Vector2[] pair = new Vector2[2];

        /// <summary>Which negative the burst uses: the sketch's default is the whole view.</summary>
        internal enum Negative { FullScreen, Disc, Off }

        /// <summary>
        /// A lightning bolt from a to b as the manga draws it into <paramref name="pts"/>: straight runs
        /// that turn sharply, each corner thrown to the other side of the line, on one slow bend.
        /// Returns the point count. <paramref name="fine"/> below 0 leaves out the fine crackle.
        /// </summary>
        private static int BoltPoints(Vector2[] pts, Vector2 a, Vector2 b, float jag, int seed, float seg, int fine)
        {
            float dx = b.x - a.x, dz = b.y - a.y, len = Mathf.Sqrt(dx * dx + dz * dz);
            if (len == 0f) len = 1e-6f;
            int n = Mathf.Min(pts.Length - 1, Mathf.Max(3, Mathf.FloorToInt(len / seg + 0.5f)));
            float nx = -dz / len, nz = dx / len, j = jag * Mathf.Min(1f, len / 0.6f);
            float bend = (Rand(seed + 1) - 0.5f) * 2.4f * j, first = Rand(seed + 2) < 0.5f ? 1f : -1f;
            for (int i = 0; i <= n; i++)
            {
                bool inner = i > 0 && i < n;
                float u = (i + (inner ? (Rand(seed + i * 5) - 0.5f) * 0.5f : 0f)) / n;
                float zig = inner ? (i % 2 == 1 ? first : -first) * (0.35f + 0.65f * Rand(seed + i * 7)) * j : 0f;
                float off = zig + bend * Mathf.Sin(u * Mathf.PI) + (fine >= 0 && inner ? (Rand(fine + i * 3) - 0.5f) * 0.4f * j : 0f);
                pts[i] = new Vector2(a.x + dx * u + nx * off, a.y + dz * u + nz * off);
            }
            return n + 1;
        }

        /// <summary>
        /// One black bolt with a red edge through the first <paramref name="count"/> points, tapering
        /// to its far end. core scales the black and rim the red, so a dying bolt thins to a red line;
        /// flare 0 to 1 is a new strike: a wider edge and a pale red thread down the middle.
        /// </summary>
        private static void Bolt(Vector2[] pts, int count, float width, float core, float rim, float altitude, float flare = 0f)
        {
            if (count < 2 || (core <= 0.01f && rim <= 0.01f)) return;
            Line(pts, count, width * (2.2f + 0.8f * flare), Fade(Red, Mathf.Min(1f, 0.8f * rim * (1f + 0.4f * flare))), whiteGlow, altitude, Taper.End);
            if (core > 0.01f) Line(pts, count, width, Fade(Ink, Mathf.Min(1f, core)), null, altitude + 0.0005f, Taper.End);
            if (flare > 0.01f) Line(pts, count, width * 0.3f, Fade(RedPale, flare * rim), whiteGlow, altitude + 0.0007f, Taper.End);
        }

        /// <summary>A burst of <paramref name="count"/> bolts out of c, <paramref name="age"/> seconds after it began. aim in radians.</summary>
        private static void Burst(Vector2 c, float age, float seconds, float aim, float scale, int count, int seed)
        {
            int nF = Mathf.FloorToInt(count * F.ForwardShare + 0.5f), nB = count - nF;
            for (int i = 0; i < count; i++)
            {
                float a = age - Rand(seed + i * 13) * 0.03f;
                if (a < 0f || a > F.Life) continue;
                bool fwd = i < nF;
                int k = fwd ? i : i - nF, n = fwd ? nF : nB;
                float fan = (fwd ? F.ForwardFan : F.BackFan) * Mathf.Deg2Rad, jitter = (Rand(seed + i * 17) - 0.5f) * 0.26f;
                float ang0 = aim + (fwd ? 0f : Mathf.PI) + ((k + 0.5f) / n - 0.5f) * fan + jitter;
                float len0 = F.ReachBolts * scale * (fwd ? 0.55f + 0.45f * Rand(seed + i * 19) : F.BackShare * (0.55f + 0.45f * Rand(seed + i * 19)));
                float dur = F.StrikeMin + (F.StrikeMax - F.StrikeMin) * Rand(seed + i * 47);
                int strike = Mathf.FloorToInt(a / dur);
                float inStrike = a - strike * dur, gone = Smooth((a - F.Life * 0.45f) / (F.Life * 0.55f));
                // Fine crackle on top of the strike's shape, redrawn Boil times a second on this bolt's own clock.
                int fine = seed + i * 7 + Mathf.FloorToInt(seconds * F.Boil + Rand(seed + i * 61)) * 131;

                float curAng = Shape(cur, c, strike, ang0, len0, gone, scale, seed, i, fine, out int curCount, out float curLen);
                int last = curCount - 1;
                float layer = Overhead + 0.03f + i * 0.001f;
                float lead = strike == 0 ? Mathf.Clamp01(a / (curLen / F.LeaderSpeed)) : 1f;
                int to = Mathf.Max(1, Mathf.CeilToInt(lead * last));
                float flare = inStrike < 0.025f ? 1f - inStrike / 0.025f : 0f;
                float w = F.Width * scale * (1f - 0.65f * gone), core = 1f - gone, rim = 1f - gone * gone * gone;
                if (strike > 0 && inStrike < F.GhostLife)
                {
                    float g = 0.4f * (1f - inStrike / F.GhostLife);
                    Shape(ghost, c, strike - 1, ang0, len0, gone, scale, seed, i, fine, out int ghostCount, out _);
                    Bolt(ghost, ghostCount, w * 0.8f, g * core, g * rim, layer - 0.0004f);
                }
                Bolt(cur, to + 1, w, core, rim, layer, flare);
                if (lead < 1f) Sprite(cur[to], 0.28f * scale, 0.28f * scale, Fade(RedPale, 0.9f), glow, layer + 0.0008f);
                // Forward bolts fork once, 35-65 % along, 25-45 deg off, a third of the bolt long; the
                // fork grows once the leader has passed its root.
                int at = Mathf.FloorToInt(last * (0.35f + 0.3f * Rand(seed + i * 23)) + 0.5f);
                if (!fwd || at >= to) continue;
                float side = Rand(seed + i * 29 + strike) < 0.5f ? -1f : 1f, fAng = curAng + side * (0.45f + 0.35f * Rand(seed + i * 31 + strike));
                float fLen = curLen * (0.25f + 0.2f * Rand(seed + i * 37 + strike));
                Vector2 root = cur[at], tip = root + new Vector2(Mathf.Cos(fAng), Mathf.Sin(fAng)) * fLen;
                int forkCount = BoltPoints(fork, root, tip, F.Jag * scale * 0.6f, seed + i * 211 + strike * 577, 0.14f, fine + 17);
                float forkLead = strike == 0 ? Mathf.Clamp01((lead * last - at) / Mathf.Max(1, last - at) * 1.6f) : 1f;
                Bolt(fork, Mathf.Min(forkCount, Mathf.Max(2, Mathf.CeilToInt(forkLead * forkCount))), w * 0.55f, core, rim, layer + 0.0002f, flare);
            }
        }

        // A bolt's shape at strike st: turned and resized per strike, stretched as it dies.
        private static float Shape(Vector2[] pts, Vector2 c, int st, float ang0, float len0, float gone, float scale, int seed, int i, int fine,
            out int count, out float len)
        {
            float ang = ang0 + (st != 0 ? (Rand(seed + i * 53 + st * 7) - 0.5f) * F.Sway : 0f);
            len = len0 * (1f + F.Stretch * gone) * (st != 0 ? 0.85f + 0.3f * Rand(seed + i * 59 + st * 11) : 1f);
            Vector2 end = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * len;
            count = BoltPoints(pts, c, end, F.Jag * scale, seed + i * 101 + st * 977, 0.2f, fine);
            return ang;
        }

        // S1's black clumps: splats with a red glow near the fist, each redrawn somewhere new Boil
        // times a second on its own clock, shrinking over ClumpLife.
        private static void Clumps(Vector2 c, float age, float seconds, float aim)
        {
            if (age < 0f || age >= F.ClumpLife) return;
            float left = 1f - Smooth(age / F.ClumpLife), grow = Smooth(age / 0.06f);
            for (int j = 0; j < F.Clumps; j++)
            {
                int r = Mathf.FloorToInt(seconds * F.Boil + Rand(j + 900)) * 37 + j * 11 + 950;
                float ang = aim + (Rand(r) - 0.5f) * (j < 5 ? 2.2f : 5.5f), d = (0.12f + 0.6f * Rand(r + 1)) * F.ReachBolts * 0.55f * (0.5f + 0.5f * grow);
                Vector2 at = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * d;
                float size = (0.14f + 0.14f * Rand(r + 2)) * left;
                Sprite(at, size * 2.6f, size * 2.6f, Fade(Red, 0.5f * left), glow, Overhead + 0.027f);
                DrawMesh(blobs[Mathf.Min(blobs.Length - 1, Mathf.FloorToInt(Rand(r + 3) * blobs.Length))], at, Overhead + 0.028f, size, size,
                    Rand(r + 4) * 360f, Fade(Ink, 0.95f), solid);
            }
        }

        /// <summary>Small bolts round a point: the fist before the hit, Todo's body in the zone.</summary>
        internal static void Crackle(Vector2 c, float seconds, int count, float len, float width, float alpha, int seed)
        {
            if (alpha <= 0.01f) return;
            Begin(c);
            int step = Mathf.FloorToInt(seconds * F.Boil);
            for (int i = 0; i < count; i++)
            {
                int r = seed + i * 43 + step * 311;
                float ang = Rand(r) * Mathf.PI * 2f, l = len * (0.6f + 0.4f * Rand(r + 1));
                int n = BoltPoints(small, c, c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * l, 0.06f, r + 2, 0.06f, -1);
                Bolt(small, n, width, alpha, alpha, Overhead + 0.05f + i * 0.001f);
            }
        }

        /// <summary>Black sparks crackling on the fist before a Black Flash lands.</summary>
        internal static void FistCrackle(Vector2 fist, float seconds, float warmup)
        {
            float from = F.Crackle(warmup);
            if (seconds < from || seconds >= warmup) return;
            Crackle(fist, seconds, 4, 0.3f, 0.045f, Mathf.Clamp01((seconds - from) / 0.04f), 500);
        }

        /// <summary>
        /// Everything from the hit on, <paramref name="age"/> seconds after the fist landed at
        /// <paramref name="contact"/>, driven along <paramref name="aim"/> (unit). <paramref name="seconds"/> is
        /// the cast's clock since its warmup began, which the redraws count on.
        /// </summary>
        internal static void Hit(Vector2 contact, Vector2 aim, float age, float seconds, Negative negative, Map map)
        {
            if (age < 0f || !Shown(contact, map)) return;
            Begin(contact);
            float aimRad = Mathf.Atan2(aim.y, aim.x), ca = aim.x, sa = aim.y;

            // Negative flash: pawns and ground, under the effect.
            float neg = F.Negative(age);
            if (negative != Negative.Off && neg > 0f)
            {
                if (negative == Negative.FullScreen) TodoGraphics.Negative(contact, 400f, neg, F.Grey, Overhead);
                else TodoGraphics.Negative(contact, F.DiscRadius, neg, F.Grey, Overhead, negativeDisc);
            }

            // The spark (S2): the fist's surroundings go dark and one small spark ignites and grows,
            // then the burst. b is the burst's age.
            if (age < F.SparkTime + 0.08f)
            {
                float dark = Mathf.Min(1f, age / 0.02f) * (1f - Smooth((age - F.SparkTime) / 0.08f));
                Sprite(contact, 2.8f, 2.8f, Fade(Ink, 0.75f * dark), soft, Overhead + 0.012f);
                float u = F.SparkTime > 0f ? Mathf.Clamp01(age / F.SparkTime) : 1f, fade = 1f - Smooth((age - F.SparkTime) / 0.06f);
                int step = Mathf.FloorToInt(seconds * F.Boil);
                Sprite(contact, 0.14f + 0.34f * u, 0.14f + 0.34f * u, Fade(SparkPale, 0.9f * fade), glow, Overhead + 0.014f);
                DrawMesh(disc, contact, Overhead + 0.015f, 0.03f + 0.05f * u, 0.03f + 0.05f * u, 0f, Fade(White, fade), whiteGlow);
                for (int i = 0; i < 12; i++)
                {
                    float ang = (i + Rand(i + step * 13 + 300) * 0.6f) / 12f * Mathf.PI * 2f + age * 2f;
                    float l = (0.08f + 0.45f * u) * (0.5f + 0.5f * Rand(i + step * 17 + 320));
                    var way = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    pair[0] = contact + way * 0.03f;
                    pair[1] = contact + way * l;
                    Line(pair, 0.02f, Fade(SparkPale, fade), whiteGlow, Overhead + 0.016f, Taper.End);
                }
            }
            float b = age - F.SparkTime;
            if (b < 0f) return;

            // Red glow, black core, red ring at the fist.
            float f = 1f - Smooth(b / 0.35f), core = 1f - Smooth(b / 0.22f), ring = Smooth(b / 0.25f);
            Sprite(contact, 2.4f, 2.4f, Fade(Red, 0.5f * f), glow, Overhead + 0.02f);
            float coreSize = 0.5f + 0.5f * Smooth(b / 0.15f);
            Sprite(contact, coreSize, coreSize, Fade(Ink, 0.9f * core), soft, Overhead + 0.022f);
            PaperBombGraphics.RingAt(contact, 0.3f + 0.7f * ring, Fade(Red, 0.75f * (1f - ring)), Overhead + 0.024f, false, whiteGlow);

            // Pale shards (S2) thrown out by the burst.
            if (b < F.ShardLife)
            {
                float u = b / F.ShardLife, outward = 1f - (1f - u) * (1f - u);
                for (int i = 0; i < F.ShardCount; i++)
                {
                    float ang = aimRad + (Rand(i + 330) - 0.5f) * (i < 8 ? 2.6f : 6.3f), r = 0.1f + (0.8f + 0.8f * Rand(i + 331)) * outward, l = 0.3f * (1f - u) + 0.05f;
                    var way = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    Streak(contact + way * r, contact + way * (r + l), 0.07f * (1f - 0.5f * u), Fade(Shard, 1f - u), whiteGlow, Overhead + 0.04f, 3);
                }
            }

            // Red streaks along the punch line.
            if (b < F.StreakLife)
            {
                float[] across = { -0.22f, 0.04f, 0.26f };
                for (int i = 0; i < 3; i++)
                {
                    var bx = new Vector2(contact.x - sa * across[i], contact.y + ca * across[i]);
                    Vector2 start = bx - aim * (F.StreakBack + Rand(i + 60) * 0.3f), end = bx + aim * (F.StreakOn * (0.75f + 0.25f * Rand(i + 61)));
                    Vector2 head = Vector2.Lerp(start, end, Smooth(b / 0.08f)), tail = Vector2.Lerp(start, end, Smooth((b - 0.06f) / 0.24f));
                    float w = 0.07f + 0.05f * Rand(i + 62), a = 1f - Smooth(b / F.StreakLife);
                    Streak(tail, head, w, Fade(Red, 0.85f * a), whiteGlow, Overhead + 0.026f, 6);
                    Streak(tail, head, w * 0.3f, Fade(Ink, 0.8f * a), solid, Overhead + 0.0265f, 6);
                }
            }

            // S1's black clumps, then the bolts: the main burst and a smaller second wave.
            Clumps(contact, b, seconds, aimRad);
            Burst(contact, b, seconds, aimRad, 1f, F.Count, 1000);
            Burst(contact, b - F.EchoAt, seconds, aimRad + 0.3f, F.EchoScale, Mathf.Max(3, Mathf.FloorToInt(F.Count * 0.45f + 0.5f)), 3000);

            // Black sparks fly off the target, mostly forward, and drop.
            for (int i = 0; i < F.SparkCount; i++)
            {
                float u = b / (F.SparkLife * (0.6f + 0.4f * Rand(i + 70)));
                if (u > 1f) continue;
                float ang = aimRad + (Rand(i + 71) - 0.5f) * 3.2f, d = (0.35f + 0.9f * Rand(i + 72)) * (1f - (1f - u) * (1f - u));
                float drop = 1.1f * (u * F.SparkLife) * (u * F.SparkLife);
                var tip = new Vector2(contact.x + Mathf.Cos(ang) * d, contact.y + Mathf.Sin(ang) * d - drop);
                pair[0] = new Vector2(tip.x - Mathf.Cos(ang) * 0.14f, tip.y - Mathf.Sin(ang) * 0.14f + 0.03f);
                pair[1] = tip;
                Bolt(pair, 2, 0.035f, 1f - u, 1f - u, Overhead + 0.045f);
            }

            // Red specks (S2) drift out and twinkle after the burst.
            float sk = b - F.SpeckFrom;
            if (sk >= 0f && sk < F.SpeckLife)
            {
                float u = sk / F.SpeckLife;
                for (int i = 0; i < F.Specks; i++)
                {
                    float ang = aimRad + (Rand(i + 340) - 0.5f) * 5f, d = (0.2f + 1.3f * Rand(i + 341)) * (1f + 0.3f * u);
                    float twinkle = Mathf.Max(0f, Mathf.Sin(seconds * 37f + i * 2.1f)), size = 0.035f + 0.03f * Rand(i + 342);
                    Color c = i % 3 != 0 ? Red : RedPale;
                    DrawMesh(disc, new Vector2(contact.x + Mathf.Cos(ang) * d, contact.y + Mathf.Sin(ang) * d + 0.1f * u), Overhead + 0.05f, size, size, 0f,
                        Fade(c, (1f - u) * (0.35f + 0.65f * twinkle)), whiteGlow);
                }
            }
        }

        /// <summary>Three pale red stars circling the target's head (<paramref name="foe"/> is its feet) for the stun.</summary>
        internal static void StunStars(Vector2 foe, float seconds, float age)
        {
            if (age < 0f || age >= F.StunTime) return;
            float alpha = 1f - Smooth((age - (F.StunTime - 0.15f)) / 0.15f);
            for (int i = 0; i < 3; i++)
            {
                float ang = seconds * 5f + i * 2.094f;
                GokuGraphics.Glint(new Vector2(foe.x + Mathf.Cos(ang) * 0.27f, foe.y + PawnFit.Y(0.84f) + Mathf.Sin(ang) * 0.1f), 0.1f,
                    alpha * (0.6f + 0.4f * Mathf.Sin(ang)), RedPale, 45f);
            }
        }

        /// <summary>
        /// Todo in the zone: every ZoneEvery a small black spark with a red edge flicks on his body.
        /// <paramref name="burst"/> is seconds since the burst.
        /// </summary>
        internal static void Zone(Vector2 todo, float burst, float seconds)
        {
            if (burst < F.ZoneStart) return;
            int k = Mathf.FloorToInt((burst - F.ZoneStart) / F.ZoneEvery);
            float inPeriod = burst - F.ZoneStart - k * F.ZoneEvery, start = Rand(k * 3 + 400) * (F.ZoneEvery - F.ZoneFlick);
            if (inPeriod < start || inPeriod >= start + F.ZoneFlick) return;
            Vector2 spot = PawnFit.At(todo, (Rand(k * 3 + 401) - 0.5f) * 0.4f, 0.12f + Rand(k * 3 + 402) * 0.55f);
            Crackle(spot, seconds, 3, 0.35f, 0.045f, 1f - (inPeriod - start) / F.ZoneFlick, 700 + k * 13);
        }

        /// <summary>The ordinary punch outside the window: a small white puff and 5 short impact lines.</summary>
        internal static void Plain(Vector2 contact, Vector2 aim, float age, Map map)
        {
            if (age < 0f || age > 0.2f || !Shown(contact, map)) return;
            Begin(contact);
            float aimRad = Mathf.Atan2(aim.y, aim.x), u = age / 0.2f, f = (1f - u) * (1f - u);
            Sprite(contact, 0.45f + 0.3f * u, 0.45f + 0.3f * u, Fade(White, 0.75f * f), glow, Overhead + 0.02f);
            for (int i = 0; i < 5; i++)
            {
                float ang = aimRad + (i / 4f - 0.5f) * 1.6f + (Rand(i + 3) - 0.5f) * 0.2f, near = 0.12f + u * 0.25f, far = near + 0.22f;
                var way = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                Streak(contact + way * near, contact + way * far, 0.04f, Fade(White, f), whiteGlow, Overhead + 0.03f, 3);
            }
        }

        private static Mesh[] MakeBlobs()
        {
            var made = new Mesh[5];
            const int n = 9;
            for (int v = 0; v < made.Length; v++)
            {
                var vertices = new Vector3[n + 1];
                var triangles = new int[n * 3];
                for (int i = 0; i < n; i++)
                {
                    float ang = (i + (Rand(v * 10 + i + 500) - 0.5f) * 0.5f) / n * Mathf.PI * 2f;
                    float r = Rand(v * 20 + i + 600) > 0.72f ? 0.75f + 0.2f * Rand(v * 30 + i + 700) : 0.32f + 0.18f * Rand(v * 40 + i + 800);
                    vertices[i + 1] = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                    // Clockwise seen from above, so the splat survives backface culling.
                    triangles[i * 3] = 0;
                    triangles[i * 3 + 1] = 1 + (i + 1) % n;
                    triangles[i * 3 + 2] = 1 + i;
                }
                made[v] = new Mesh { name = "Black Flash blob " + v, vertices = vertices, triangles = triangles };
                made[v].RecalculateBounds();
            }
            return made;
        }
    }
}
