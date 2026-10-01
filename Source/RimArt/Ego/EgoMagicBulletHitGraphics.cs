using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.EgoMagicBulletGraphics;
using T = RimArt.EgoMagicBulletTiming;

namespace RimArt
{
    /// <summary>
    /// What Magic Bullet's line does to what it crosses. A pawn: lit yellow-white for 0.15 s, a burst of 14 thin
    /// yellow spikes and 4 long rays over 0.35 s, an orange bolt arcing past it along the beam (two on the
    /// seventh) re-drawn every 1/18 s for 0.4 s, 12 orange sparks flung on forward, 3 orange streak lines flying
    /// on past it (7 on the seventh), a small ball of fire for 0.3 s and flames rising off it for 0.6 s, blood
    /// thrown on past and a floor spatter that stays. The seventh's hit is 1.5x the size. A wall: a dark hole
    /// that stays, a blue glint for 0.1 s, dust off both faces for half a second; the bullet does not slow.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class EgoMagicBulletHitGraphics
    {
        private static readonly Color HoleRim = new Color(0.16f, 0.14f, 0.12f);

        /// <summary>
        /// A pawn standing at <paramref name="pos"/> (its DrawPos), hit <paramref name="age"/> s ago by a line running
        /// along <paramref name="d"/>. The floor spatter stays for as long as the picture runs.
        /// </summary>
        internal static void Wound(Vector2 pos, Vector2 d, float age, int tier, float s)
        {
            if (age < 0f) return;
            var chest = new Vector2(pos.x, pos.y + PawnBody.Chest);
            float k = 1f + tier * 0.25f, aim = Mathf.Atan2(d.y, d.x);
            int frame = Mathf.FloorToInt(s * 18f);
            if (age < 0.15f)
            {
                // The pawn lit.
                float f = 1f - age / 0.15f;
                Sprite(new Vector2(chest.x, chest.y - 0.12f), 0.7f * k, 1.0f * k, Fade(Stagger, 0.7f * f), glow, Overhead + 0.091f);
                Sprite(chest, 0.45f * k, 0.45f * k, Fade(White, 0.8f * f), glow, Overhead + 0.0911f);
            }
            if (age < 0.35f)
            {
                // The stagger burst.
                float u = age / 0.35f, grow = Mathf.Sqrt(Mathf.Min(1f, age / 0.08f)), fade = 1f - u * u;
                Sprite(chest, 1.1f * k * grow, 1.0f * k * grow, Fade(Stagger, 0.5f * fade), glow, Overhead + 0.093f);
                Sprite(chest, 0.5f * k * grow, 0.45f * k * grow, Fade(FireCore, 0.9f * fade), glow, Overhead + 0.0931f);
                for (int i = 0; i < 14; i++)
                {
                    float t = i / 14f * Tau + 0.2f + Rand(i + 700) * 0.3f, l = (0.35f + 0.6f * Rand(i + 710)) * k * grow;
                    Streak(chest, new Vector2(chest.x + Mathf.Cos(t) * l, chest.y + Mathf.Sin(t) * l * 0.85f), (0.055f - 0.025f * u) * k,
                        Fade(Stagger, 0.95f * fade), whiteGlow, Overhead + 0.094f, 4);
                }
                for (int i = 0; i < 4; i++)
                {
                    float t = i / 4f * Tau + 0.6f, l = (1.1f + 0.4f * Rand(i + 720)) * k * grow;
                    Streak(chest, new Vector2(chest.x + Mathf.Cos(t) * l, chest.y + Mathf.Sin(t) * l * 0.85f), 0.03f,
                        Fade(FireCore, 0.8f * fade), whiteGlow, Overhead + 0.0941f, 3);
                }
            }
            if (age < 0.4f)
            {
                // The orange bolt past the target; a quarter of the frames leave it out.
                float f = 1f - age / 0.4f;
                for (int b = 0; b < (tier > 0 ? 2 : 1); b++)
                {
                    if (Rand(frame * 3 + b) < 0.25f) continue;
                    float back = 0.7f + Rand(frame + b * 7) * 0.5f, acr = (Rand(frame * 5 + b) - 0.5f) * 0.9f;
                    var from = new Vector2(chest.x - d.x * back - d.y * acr, chest.y - d.y * back + d.x * acr);
                    EgoMagicBulletBeamGraphics.Zigzag(from, aim + (Rand(frame * 7 + b * 3) - 0.5f) * 0.5f, (1.5f + Rand(frame + b) * 0.8f) * k, 7, frame * 11 + b * 29,
                        0.045f, Fade(Bolt, 0.95f * f), Fade(Fire, 0.5f * f), Overhead + 0.097f);
                }
            }
            for (int i = 0; i < 12; i++)
            {
                // Sparks flung on forward, as short streaks.
                float life = 0.3f + Rand(i + 800) * 0.2f, u = age / life;
                if (u > 1f) continue;
                float a = aim + (Rand(i + 810) - 0.5f) * 1.6f, v = (2.5f + Rand(i + 820) * 4f) * k;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 q = chest + dir * (v * age);
                Streak(q - dir * (v * 0.03f), q, 0.035f, Fade(i % 3 != 0 ? Bolt : FireCore, 1f - u), whiteGlow, Overhead + 0.0975f, 3);
            }
            if (age < 0.45f)
                for (int i = 0; i < 3 + tier * 2; i++)
                {
                    // Streak lines flying on past the target.
                    float u = age / 0.45f, a0 = Rand(i + 600) * 1.5f + u * 3f, l = 0.6f + Rand(i + 610) * 1.2f, acr = (Rand(i + 620) - 0.5f) * 1.2f;
                    var from = new Vector2(chest.x + d.x * a0 - d.y * acr, chest.y + d.y * a0 + d.x * acr);
                    Streak(from, from + d * l, 0.05f, Fade(Bolt, 0.9f * (1f - u)), whiteGlow, Overhead + 0.098f, 3);
                }
            if (age < 0.3f)
            {
                // The burn: a small ball of fire that flares and thins.
                float u = age / 0.3f, size = (0.25f + 0.35f * Mathf.Sqrt(u)) * k;
                Sprite(chest, size, size * 0.85f, Fade(Fire, 0.8f * (1f - u * u)), Puff, Overhead + 0.092f);
            }
            for (int i = 0; i < 5; i++)
            {
                // Flames rising off them for 0.6 s.
                float u = age / 0.6f;
                if (u > 1f) continue;
                float ph = (age * (2.2f + Rand(i + 500)) + Rand(i + 510)) % 1f, x = chest.x + (Rand(i + 520) - 0.5f) * 0.4f * k, lick = Mathf.Sin(ph * Mathf.PI);
                var at = new Vector2(x, chest.y - 0.15f + ph * 0.5f);
                Sprite(at, (0.14f + 0.12f * (1f - ph)) * k, (0.18f + 0.14f * (1f - ph)) * k, Fade(Fire, 0.85f * lick * (1f - u)), Puff, Overhead + 0.095f);
                Sprite(at, 0.07f * k, 0.09f * k, Fade(FireCore, 0.7f * lick * (1f - u)), glow, Overhead + 0.096f);
            }
            if (age < 0.12f) Sprite(chest, 0.5f * k, 0.4f * k, Fade(ChainSickleGraphics.Blood, 0.8f * (1f - age / 0.12f)), soft, Overhead + 0.09f);
            for (int i = 0; i < 8; i++)
            {
                // Blood thrown on past, falling 6 cells/s^2 on screen.
                float life = 0.25f + Rand(i + 900) * 0.12f, u = age / life;
                if (u > 1f) continue;
                float a = aim + (Rand(i + 910) - 0.5f) * 1.1f, v = (2f + Rand(i + 920) * 3.5f) * k;
                var at = new Vector2(chest.x + Mathf.Cos(a) * v * age, chest.y + Mathf.Sin(a) * v * age - 6f * age * age);
                Sprite(at, 0.16f * k, 0.12f * k, Fade(ChainSickleGraphics.Blood, 1f - u * u), soft, Overhead + 0.09f);
            }
            // The spatter on the floor past them, which stays; the sketch turns it by the aim's degrees.
            float g = Mathf.Clamp01(age / 0.3f);
            Sprite(pos + d * 0.55f, 1.0f * g * k, 0.55f * g * k, Fade(ChainSickleGraphics.Blood, 0.75f * g), soft, Floor + 0.02f, aim * Mathf.Rad2Deg);
            for (int i = 0; i < 5; i++)
            {
                if (g < 0.5f + i * 0.1f) continue;
                float a = aim + (Rand(i + 930) - 0.5f) * 0.9f, r = 0.5f + Rand(i + 940) * 0.9f * k;
                Sprite(new Vector2(pos.x + Mathf.Cos(a) * r, pos.y + Mathf.Sin(a) * r), 0.14f + Rand(i + 950) * 0.12f, 0.11f + Rand(i + 960) * 0.09f,
                    Fade(ChainSickleGraphics.Blood, 0.8f), soft, Floor + 0.021f);
            }
        }

        /// <summary>
        /// The line punching through a wall where it crosses at <paramref name="at"/> (a ground point on the line),
        /// <paramref name="age"/> s ago: a dark hole on the wall top that stays, a blue glint, dust off both faces.
        /// </summary>
        internal static void Punch(Vector2 at, Vector2 d, float age)
        {
            if (age < 0f) return;
            Vector2 q = AtChest(at);
            Sprite(q, 0.2f, 0.16f, Fade(Smoke, 0.9f), soft, WallTop + 0.01f);
            Sprite(q, 0.3f, 0.24f, Fade(HoleRim, 0.5f), soft, WallTop + 0.009f);
            if (age < 0.1f) GokuGraphics.Glint(q, 0.4f, 1f - age / 0.1f, Beam, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            for (int i = 0; i < 4; i++)
            {
                float u = (age - i * 0.04f) / 0.5f;
                if (u < 0f || u > 1f) continue;
                Vector2 g = q + d * ((i % 2 == 1 ? 1f : -1f) * (0.3f + u * 0.5f));
                Sprite(new Vector2(g.x + (Rand(i + 80) - 0.5f) * 0.25f, g.y + u * 0.25f), 0.22f + u * 0.35f, 0.2f + u * 0.3f,
                    Fade(ChainSickleGraphics.Dust, (1f - u) * 0.35f), Puff, Overhead + 0.03f);
            }
        }
    }
}
