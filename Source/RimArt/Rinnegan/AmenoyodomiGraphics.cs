using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>One held weapon at one moment: everything <see cref="AmenoyodomiGraphics.Held"/> reads.</summary>
    public struct HeldLook
    {
        public bool fuma;
        /// <summary>The ground point it hangs over now. It is drawn <see cref="AmenoyodomiGraphics.Hold"/> cells up from here.</summary>
        public Vector2 ground;
        /// <summary>Heading, degrees: 0 east, 90 north.</summary>
        public float deg;
        /// <summary>The Fūma's turn, degrees clockwise (0 for a kunai).</summary>
        public float turn;
        /// <summary>Seconds since it was caught.</summary>
        public float since;
        /// <summary>Cells it has crept past where it was caught.</summary>
        public float crept;
        /// <summary>Cells per second it moves on now (its flight speed times the hold share).</summary>
        public float speed;
        /// <summary>Its flight speed, cells per second.</summary>
        public float fullSpeed;
        public int seed;
        /// <summary>The weapon's own texture (a kunai, Minato's kunai, the Fūma); null uses the plain kunai or the Fūma.</summary>
        public Material material;
    }

    /// <summary>
    /// Amenoyodomi's held weapon: the port of the lab's lib/amenoyodomi.js drawing (drawWeapon, eyeStar), shared by
    /// the Amenoyodomi, Amenotejikara, Raikō Kusari and Amaterasu pictures. Every function takes ages and places and
    /// keeps no state.
    ///
    /// A weapon hangs <see cref="Hold"/> cells up and is drawn that height times SixPathsHeight.Lift north of its
    /// ground point, with its shadow on the ground. The marks under it (two rings, three dots that turn as it creeps,
    /// the ripples) are level circles on the floor; the afterimages lie along its heading. Nothing needs a per-facing
    /// method.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class AmenoyodomiGraphics
    {
        // The game's weapon numbers: AG_KunaiProjectile (24 cells/s, drawSize 0.75) and AG_FumaProjectile
        // (14.4 cells/s, drawSize 1.4). The Fūma's spin is the sketches' stand-in, 720 degrees/s clockwise.
        public const float KunaiSpeed = 24f, KunaiSize = 0.75f, FumaSpeed = 14.4f, FumaSize = 1.4f, FumaSpin = 720f;
        /// <summary>Cells up while held: chest height.</summary>
        public const float Hold = 0.55f;
        /// <summary>Seconds: slowing into the hold, falling when dropped.</summary>
        public const float Ease = 0.06f, DropTime = 0.25f;

        public static readonly Color Lavender = new Color(0.80f, 0.74f, 0.98f);
        public static readonly Color LavenderDeep = new Color(0.46f, 0.36f, 0.78f);
        public static readonly Color EyeStarColour = new Color(0.90f, 0.84f, 1f);
        private static readonly Color ShadowInk = new Color(0.03f, 0.03f, 0.05f), DustColour = new Color(0.52f, 0.47f, 0.40f);

        private const float Stroke = 0.025f, InnerShare = 0.6f, FumaRing = 2.6f;
        private const float CatchLife = 0.45f, CatchReach = 1f, CatchFlash = 0.12f;
        private const float RippleLife = 1f, RippleReach = 2.2f;
        private const float WakeGap = 0.3f, WakeLife = 0.8f, WakeFrom = 0.05f;
        private const float PullIn = 0.08f, LetGoFlash = 0.1f, RingFade = 0.2f, DustLife = 0.35f;
        private static readonly float[] GhostAlpha = { 0.5f, 0.32f, 0.2f, 0.12f, 0.07f };
        private const float GhostPerSpeed = 0.08f, DotTurn = 360f;
        // The Amenoyodomi sketch's look defaults (its sliders), which the other sketches take.
        private const int Ghosts = 3;
        private const float GhostGap = 0.16f, RingR = 0.32f, RippleEvery = 2f, Strength = 0.32f;
        private static readonly Vector2 Sun = new Vector2(-0.45f, -0.32f);

        /// <summary>How long each one-off picture runs.</summary>
        public const float CatchDuration = CatchLife + 0.08f, LetGoDuration = LetGoFlash, FallDuration = DropTime + DustLife;

        private static readonly Mesh RingOuter = Ring(1f - Stroke / RingR, "RimArt amenoyodomi ring 0");
        private static readonly Mesh RingInner = Ring(1f - Stroke / (RingR * InnerShare), "RimArt amenoyodomi ring 1");
        private static readonly Material KunaiMat = MaterialPool.MatFrom("RimArt/Kunai/Kunai", ShaderDatabase.Cutout);
        private static readonly Material FumaMat = MaterialPool.MatFrom("RimArt/Fuma/Unfolded", ShaderDatabase.Cutout);
        private static readonly Material GhostKunai = MaterialPool.MatFrom("RimArt/Kunai/Kunai", ShaderDatabase.MoteGlow);
        private static readonly Material GhostFuma = MaterialPool.MatFrom("RimArt/Fuma/Unfolded", ShaderDatabase.MoteGlow);
        private static readonly Material Puff = MaterialPool.MatFrom("RimArt/SixPaths/Puff", ShaderDatabase.Transparent);
        private static readonly float ShadowLayer = AltitudeLayer.Shadows.AltitudeFor();
        public static readonly float ProjectileLayer = AltitudeLayer.Projectile.AltitudeFor();

        /// <summary>The point <paramref name="h"/> cells above <paramref name="ground"/>, as drawn (the lab's at(g, 0, 0, h)).</summary>
        public static Vector2 Up(Vector2 ground, float h) => new Vector2(ground.x, ground.y + h * SixPathsHeight.Lift);

        /// <summary>North offset of a held weapon from its ground point.</summary>
        public static float HeldLift => Hold * SixPathsHeight.Lift;

        public static float RingRadius(bool fuma) => RingR * (fuma ? FumaRing : 1f);

        /// <summary>The violet 4-point star on the caster's eye at every Amenoyodomi command.</summary>
        public static void EyeStar(Vector2 eye, float size, float alpha)
        {
            if (alpha <= 0f) return;
            Sprite(eye, size * 1.2f, size * 1.2f, Fade(LavenderDeep, alpha * 0.7f), glow, Overhead + 0.2f);
            DrawMesh(MeshPool.plane10, eye, Overhead + 0.21f, size * 2f, size * 0.12f, 0f, Fade(EyeStarColour, alpha), whiteGlow);
            DrawMesh(MeshPool.plane10, eye, Overhead + 0.21f, size * 0.12f, size * 2f, 0f, Fade(EyeStarColour, alpha), whiteGlow);
        }

        /// <summary>The weapon itself at its drawn point.</summary>
        public static void Weapon(bool fuma, Vector2 pos, float deg, float turn, float alpha = 1f, Material material = null, float layer = -1f)
        {
            if (layer < 0f) layer = ProjectileLayer;
            if (fuma) Sprite(pos, FumaSize, FumaSize, Fade(Color.white, alpha), material ?? FumaMat, layer, turn);
            else Sprite(pos, KunaiSize, KunaiSize, Fade(Color.white, alpha), material ?? KunaiMat, layer, 90f - deg);
        }

        private static void Shadow(bool fuma, Vector2 ground, float h, float deg, float turn)
        {
            var c = new Vector2(ground.x + Sun.x * h, ground.y + Sun.y * h);
            if (fuma) Sprite(c, 1f, 1f, Fade(ShadowInk, Strength * 0.7f), soft, ShadowLayer, turn);
            else Sprite(c, 0.1f, 0.5f, Fade(ShadowInk, Strength * 0.8f), soft, ShadowLayer, 90f - deg);
        }

        /// <summary>The two rings under a held weapon, radius <paramref name="r"/>, and three dots turned <paramref name="turn"/> degrees.</summary>
        private static void Marks(Vector2 g, float r, float turn, float alpha)
        {
            if (alpha <= 0f || r <= 0f) return;
            Sprite(g, r * 2.6f, r * 2.6f, Fade(LavenderDeep, 0.22f * alpha), glow, Floor + 0.017f);
            DrawMesh(RingOuter, g, Floor + 0.02f, r, r, 0f, Fade(Lavender, 0.38f * alpha), solid);
            DrawMesh(RingInner, g, Floor + 0.021f, r * InnerShare, r * InnerShare, 0f, Fade(Lavender, 0.3f * alpha), solid);
            for (int k = 0; k < 3; k++)
            {
                float a = (turn + k * 120f) * Mathf.Deg2Rad;
                DrawMesh(disc, new Vector2(g.x + Mathf.Cos(a) * r, g.y - Mathf.Sin(a) * r), Floor + 0.022f, r * 0.1f, r * 0.1f, 0f,
                    Fade(Lavender, 0.6f * alpha), solid);
            }
        }

        /// <summary>A ring spreading on the floor from r0 to r1 over life seconds.</summary>
        private static void Ripple(Vector2 g, float age, float life, float r0, float r1, float alpha, float layer = 0.019f)
        {
            if (age < 0f || age >= life) return;
            float u = age / life, r = r0 + (r1 - r0) * Smooth(u);
            DrawMesh(RingOuter, g, Floor + layer, r, r, 0f, Fade(Lavender, alpha * (1f - u) * (1f - u)), solid);
        }

        /// <summary>A little dust where a dropped weapon lands.</summary>
        public static void Dust(Vector2 g, float age, bool fuma)
        {
            if (age < 0f || age >= DustLife) return;
            float u = age / DustLife, size = fuma ? 1.3f : 0.7f;
            Sprite(new Vector2(g.x, g.y + 0.05f + 0.1f * u), size * (0.4f + 0.6f * u), size * (0.3f + 0.4f * u),
                Fade(DustColour, 0.45f * (1f - u)), Puff, Overhead + 0.005f);
        }

        /// <summary>
        /// A held weapon: its shadow, the afterimages of the motion it was stopped in (spreading out as it drifts
        /// faster), the weapon, the rings and dots under it, the ripple that leaves it every 2 s and the wake of ripples
        /// while it drifts.
        /// </summary>
        public static void Held(in HeldLook w)
        {
            float r = RingRadius(w.fuma), fade = Smooth(w.since / 0.1f);
            Vector2 dir = Turn(w.deg), pos = Up(w.ground, Hold);
            Shadow(w.fuma, w.ground, Hold, w.deg, w.turn);
            float gap = GhostGap + GhostPerSpeed * w.speed;
            for (int k = 1; k <= Ghosts; k++)
            {
                Vector2 q = Up(w.ground - dir * (gap * k), Hold);
                Color tint = Fade(Lavender, GhostAlpha[k - 1] * fade);
                if (w.fuma) Sprite(q, FumaSize, FumaSize, tint, GhostFuma, ProjectileLayer - 0.001f * k, w.turn);
                else Sprite(q, KunaiSize, KunaiSize, tint, GhostKunai, ProjectileLayer - 0.001f * k, 90f - w.deg);
            }
            Weapon(w.fuma, pos, w.deg, w.turn, 1f, w.material);
            Marks(w.ground, r, DotTurn * w.crept / Mathf.Max(0.01f, w.fullSpeed), fade);

            // A faint ripple every RippleEvery seconds, first at a time set by the seed; drawn where it was then.
            float first = RippleEvery * (0.5f + (w.seed * 0.37f) % 0.5f);
            int latest = Mathf.FloorToInt((w.since - first) / RippleEvery);
            for (int n = Mathf.Max(0, latest); n >= 0 && n >= latest - 1; n--)
            {
                float born = first + n * RippleEvery;
                if (born > w.since) continue;
                Ripple(w.ground - dir * (w.speed * (w.since - born)), w.since - born, RippleLife, r, r * RippleReach, 0.3f);
            }

            // The wake: a ring left every WakeGap cells while it moves at WakeFrom of its flight speed or more.
            if (w.speed <= 0f || w.speed < WakeFrom * w.fullSpeed) return;
            for (int k = Mathf.FloorToInt(w.crept / WakeGap); k >= 1; k--)
            {
                float back = w.crept - k * WakeGap, age = back / w.speed;
                if (age >= WakeLife) break;
                float f = age / WakeLife;
                DrawMesh(RingOuter, w.ground - dir * back, Floor + 0.018f, r * (0.8f + 0.6f * f), r * (0.8f + 0.6f * f), 0f,
                    Fade(Lavender, 0.24f * (1f - f)), solid);
            }
        }

        /// <summary>The ripple a weapon makes as it stops over <paramref name="ground"/>, and the flash on it.</summary>
        public static void Catch(bool fuma, Vector2 ground, float age)
        {
            float r = RingRadius(fuma), big = fuma ? 1.8f : 1f;
            Ripple(ground, age, CatchLife, r, CatchReach * big, 0.7f);
            Ripple(ground, age - 0.08f, CatchLife, r * 0.8f, CatchReach * big * 0.7f, 0.45f);
            if (age >= 0f && age < CatchFlash)
                Sprite(Up(ground, Hold), 0.5f * big, 0.5f * big, Fade(Lavender, 0.65f * (1f - age / CatchFlash)), glow, Overhead + 0.02f);
        }

        /// <summary>Let go: the rings pull in to the point it hung over and a small flash as it leaves at full speed.</summary>
        public static void LetGo(bool fuma, Vector2 ground, float age)
        {
            if (age < 0f) return;
            float r = RingRadius(fuma), big = fuma ? 1.8f : 1f;
            if (age < PullIn) Marks(ground, r * (1f - Smooth(age / PullIn)), 0f, 1f - age / PullIn);
            if (age < LetGoFlash)
            {
                float f = 1f - age / LetGoFlash;
                Sprite(Up(ground, Hold), 0.7f * big, 0.7f * big, Fade(Lavender, 0.7f * f), glow, Overhead + 0.03f);
                Sprite(Up(ground, Hold), 0.28f * big, 0.28f * big, Fade(Color.white, 0.9f * f), glow, Overhead + 0.031f);
            }
        }

        /// <summary>Dropped: the marks fade where it hung and it falls straight down; then a little dust where it lands.</summary>
        public static void Falling(bool fuma, Vector2 ground, float deg, float turn, float age, Material material = null, bool weapon = true)
        {
            if (age < 0f) return;
            if (age < RingFade) Marks(ground, RingRadius(fuma), 0f, 1f - age / RingFade);
            if (age < DropTime)
            {
                if (!weapon) return;
                float f = age / DropTime, h = Hold * (1f - f * f);
                Shadow(fuma, ground, h, deg, turn);
                Weapon(fuma, Up(ground, h), deg, turn, 1f, material);
                return;
            }
            Dust(ground, age - DropTime, fuma);
        }

        /// <summary>A weapon in flight at <paramref name="speed"/> cells/s: the white streak behind it (the weapon is the game's).</summary>
        public static void Streak(bool fuma, Vector2 pos, float deg, float speed, float flown)
        {
            float len = Mathf.Min(Mathf.Min(0.6f, flown), speed * 0.025f);
            if (len <= 0.03f) return;
            VfxDraw.Streak(pos - Turn(deg) * len, pos, fuma ? 0.12f : 0.05f, Fade(Color.white, 0.45f), whiteGlow, ProjectileLayer - 0.003f, 6);
        }
    }
}
