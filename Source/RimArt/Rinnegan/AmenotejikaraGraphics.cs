using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// Times and sizes of the Amenotejikara picture, the defaults of the lab sketch
    /// (Tools/VfxLab/web/sketches/rinnegan-amenotejikara.js). Seconds after the swap; no drawing.
    /// </summary>
    public static class AmenotejikaraTiming
    {
        /// <summary>The eye star on the caster, the pale afterimage left at each end.</summary>
        public const float EyeLife = 0.1f, AfterLife = 0.2f;
        /// <summary>The ghosts cross in Cross; the pattern ripples out in Ripple, then fades over Fade.</summary>
        public const float Cross = 0.1f, Ripple = 0.14f, Fade = 0.5f;
        /// <summary>Pattern radius at the swap, radius it ripples out to, degrees the tomoe turn.</summary>
        public const float Radius = 1.2f, Grow = 1.7f, TurnDegrees = 60f;
        /// <summary>The negative flash: held, then back to normal over Back; pulled toward grey; disc radius.</summary>
        public const float FlashHold = 0.12f, FlashBack = 0.1f, Grey = 0.25f, Local = 1.5f;
        /// <summary>How far north of its feet a pawn's ghost and afterimage are drawn.</summary>
        public const float PawnLift = 0.38f;

        /// <summary>The picture runs this long after the swap.</summary>
        public const float Duration = Ripple + Fade + 0.4f;
    }

    /// <summary>What stands at one end of a swap, for the picture.</summary>
    public enum SwapShape { Pawn, Kunai, Item }

    /// <summary>
    /// The Amenotejikara picture: the port of rinnegan-amenotejikara.js. At both ends the Rinnegan pattern on the floor
    /// (4 rings, pupil, 6 tomoe) ripples out, turns and fades; a pale ghost of each old occupant crosses to the other
    /// end in 0.1 s, the two passing mid-way; a faint afterimage stays where each was; the caster's eye flashes a
    /// small violet star; the screen goes negative for 0.12 s. Everything is a level circle or a flat quad, so there
    /// is no per-facing method. Every function takes the age since the swap.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class AmenotejikaraGraphics
    {
        private static readonly float[] RingFractions = { 0.28f, 0.52f, 0.76f, 1f };
        private const float Stroke = 0.035f, GhostW = 0.5f, GhostH = 0.95f;
        private static readonly Mesh[] RingMeshes = MakeRings();
        private static readonly float PawnLayer = AltitudeLayer.Pawn.AltitudeFor();
        private static readonly Material KunaiMat = MaterialPool.MatFrom("RimArt/Kunai/Kunai", ShaderDatabase.Cutout);

        private static Mesh[] MakeRings()
        {
            var rings = new Mesh[RingFractions.Length];
            for (int i = 0; i < rings.Length; i++)
                rings[i] = Ring(1f - Stroke / (AmenotejikaraTiming.Radius * RingFractions[i]), "RimArt amenotejikara ring " + i);
            return rings;
        }

        /// <summary>The small violet star on the caster's eye, at the cast.</summary>
        public static void Eye(Vector2 eye, float age)
        {
            if (age < 0f || age >= AmenotejikaraTiming.EyeLife) return;
            AmenoyodomiGraphics.EyeStar(eye, 0.22f, 1f - age / AmenotejikaraTiming.EyeLife);
        }

        /// <summary>
        /// The Rinnegan pattern round one end (feet or ground point): on at the swap, ripples out and turns, then fades.
        /// <paramref name="n"/> is the end, 0 or 1 (the second is turned a little further).
        /// </summary>
        public static void Pattern(Vector2 centre, float age, int n)
        {
            if (age < 0f || age >= AmenotejikaraTiming.Duration) return;
            float alpha = 1f - Smooth((age - AmenotejikaraTiming.Ripple) / AmenotejikaraTiming.Fade);
            float rip = Smooth(age / AmenotejikaraTiming.Ripple);
            float radius = Mathf.Lerp(AmenotejikaraTiming.Radius, AmenotejikaraTiming.Grow, rip);
            float turn = AmenotejikaraTiming.TurnDegrees * rip * Mathf.Deg2Rad + n * 0.4f;
            DrawPattern(centre, radius, turn, alpha, Floor + 0.03f);
        }

        private static void DrawPattern(Vector2 c, float radius, float turn, float alpha, float layer)
        {
            if (alpha <= 0f) return;
            Begin(c);
            Color lavender = Fade(AmenoyodomiGraphics.Lavender, alpha);
            Sprite(c, radius * 2.6f, radius * 2.6f, Fade(AmenoyodomiGraphics.LavenderDeep, 0.35f * alpha), glow, layer);
            for (int i = 0; i < RingFractions.Length; i++)
                DrawMesh(RingMeshes[i], c, layer + 0.002f, radius * RingFractions[i], radius * RingFractions[i], 0f, lavender, solid);
            DrawMesh(disc, c, layer + 0.003f, radius * 0.09f, radius * 0.09f, 0f, lavender, solid);
            // Tomoe: a round head on the ring with a tail running back along it; 3 on ring 1, 3 on ring 2.
            for (int i = 0; i < 6; i++)
            {
                float ringR = radius * RingFractions[i < 3 ? 0 : 1];
                float a0 = turn + i * (Mathf.PI * 2f / 3f) + (i < 3 ? 0f : Mathf.PI / 3f);
                var head = new Vector2(c.x + Mathf.Cos(a0) * ringR, c.y + Mathf.Sin(a0) * ringR);
                DrawMesh(disc, head, layer + 0.004f, radius * 0.075f, radius * 0.075f, 0f, lavender, solid);
                Vector2[] pts = Points(7);
                for (int k = 0; k <= 6; k++)
                {
                    float a = a0 - k / 6f * 0.75f;
                    pts[k] = new Vector2(c.x + Mathf.Cos(a) * ringR, c.y + Mathf.Sin(a) * ringR);
                }
                Trail(pts, radius * 0.1f, lavender, layer + 0.004f);
            }
        }

        /// <summary>
        /// The pale ghost of what stood at <paramref name="from"/> crossing to <paramref name="to"/> (both feet or ground
        /// points), with a streak a quarter of the way back, fading over the second half of the crossing. A kunai's
        /// ghost is the kunai itself, pale, at its hanging height.
        /// </summary>
        public static void Ghost(Vector2 from, Vector2 to, float age, SwapShape shape, float kunaiDeg)
        {
            if (age < 0f || age >= AmenotejikaraTiming.Cross) return;
            float u = age / AmenotejikaraTiming.Cross, a = 1f - Mathf.Max(0f, u - 0.5f) * 2f;
            float lift = shape == SwapShape.Kunai ? AmenoyodomiGraphics.HeldLift : shape == SwapShape.Item ? 0.1f : AmenotejikaraTiming.PawnLift;
            Vector2 Along(float v) => Vector2.Lerp(from, to, v) + new Vector2(0f, lift);
            Vector2[] pts = Points(7);
            for (int k = 0; k <= 6; k++) pts[k] = Along(Mathf.Max(0f, u - 0.25f * (1f - k / 6f)));
            Begin(from);
            Trail(pts, 0.3f, Fade(AmenoyodomiGraphics.LavenderDeep, 0.7f * a), Overhead + 0.09f);
            Vector2 g = Along(u);
            if (shape == SwapShape.Kunai) PaleKunai(g, kunaiDeg, a);
            else if (shape == SwapShape.Item) Sprite(g, GhostW * 0.8f, GhostW * 0.8f, Fade(AmenoyodomiGraphics.Lavender, 0.9f * a), glow, Overhead + 0.1f);
            else Sprite(g, GhostW, GhostH, Fade(AmenoyodomiGraphics.Lavender, 0.9f * a), glow, Overhead + 0.1f);
        }

        /// <summary>The faint afterimage of what stood at <paramref name="at"/> (feet or ground point) before the swap.</summary>
        public static void Afterimage(Vector2 at, float age, SwapShape shape, float kunaiDeg)
        {
            if (age < 0f || age >= AmenotejikaraTiming.AfterLife) return;
            float a = 0.35f * (1f - age / AmenotejikaraTiming.AfterLife);
            if (shape == SwapShape.Kunai) PaleKunai(AmenoyodomiGraphics.Up(at, AmenoyodomiGraphics.Hold), kunaiDeg, a);
            else if (shape == SwapShape.Item) Sprite(new Vector2(at.x, at.y + 0.1f), 0.4f, 0.4f, Fade(AmenoyodomiGraphics.Lavender, a), glow, PawnLayer + 0.02f);
            else Sprite(new Vector2(at.x, at.y + AmenotejikaraTiming.PawnLift), 0.3f, 0.8f, Fade(AmenoyodomiGraphics.Lavender, a), glow, PawnLayer + 0.02f);
        }

        private static void PaleKunai(Vector2 pos, float deg, float alpha)
        {
            if (alpha <= 0f) return;
            Sprite(pos, AmenoyodomiGraphics.KunaiSize, AmenoyodomiGraphics.KunaiSize, Fade(AmenoyodomiGraphics.Lavender, alpha), KunaiMat,
                AmenoyodomiGraphics.ProjectileLayer + 0.01f, 90f - deg);
        }

        /// <summary>
        /// The negative flash: the whole view inverted and pulled toward grey for <see cref="AmenotejikaraTiming.FlashHold"/>,
        /// then back over <see cref="AmenotejikaraTiming.FlashBack"/>. <paramref name="discs"/> draws it only round the two
        /// ends instead (NegativeFlash.cs).
        /// </summary>
        public static void Flash(float age, bool discs, Vector2 a, Vector2 b)
        {
            if (age < 0f) return;
            float strength = age < AmenotejikaraTiming.FlashHold ? 1f
                : 1f - Smooth((age - AmenotejikaraTiming.FlashHold) / AmenotejikaraTiming.FlashBack);
            if (strength <= 0f) return;
            if (!discs)
            {
                NegativeFlash.DrawScreen(strength, AmenotejikaraTiming.Grey);
                return;
            }
            NegativeFlash.DrawDisc(new Vector3(a.x, 0f, a.y), AmenotejikaraTiming.Local, strength, AmenotejikaraTiming.Grey);
            NegativeFlash.DrawDisc(new Vector3(b.x, 0f, b.y), AmenotejikaraTiming.Local, strength, AmenotejikaraTiming.Grey);
        }

        private static readonly Vector2[][] points = new Vector2[16][];
        private static Vector2[] Points(int n) => points[n] ?? (points[n] = new Vector2[n]);

        /// <summary>The lab's trail(): a band along the points, widest in the middle (sin), zero at both ends.</summary>
        internal static void Trail(Vector2[] pts, float width, Color colour, float layer)
        {
            int n = pts.Length;
            Sides(n, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = pts[Mathf.Max(0, i - 1)], next = pts[Mathf.Min(n - 1, i + 1)];
                Vector2 d = next - prev;
                float len = d.magnitude;
                if (len < 1e-6f) len = 1f;
                float w = Mathf.Sin(i / (float)(n - 1) * Mathf.PI) * width / 2f;
                a[i] = new Vector2(pts[i].x - d.y / len * w, pts[i].y + d.x / len * w);
                b[i] = new Vector2(pts[i].x + d.y / len * w, pts[i].y - d.x / len * w);
            }
            Strip(a, b, colour, solid, layer);
        }
    }
}
