using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using T = RimArt.AmaterasuTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Amaterasu, the port of the lab's rinnegan-amaterasu.js with its defaults (Storm 4 look, violet light):
    /// the red glint on the caster's eye and the blood from it, the screen dim, the black fire on a pawn (the cast's
    /// burst, shards and ground flames, the strands, tongues and blotches, the wisps and flecks torn off the top, the
    /// neighbour's jump), the stain on the floor, the flames on a held, flying or landed kunai or Fūma, a flying
    /// kunai's tail and the flecks a flying weapon leaves, and the smoke when a fire goes out.
    ///
    /// Every function takes "seconds since X" and places and keeps no state between frames except VfxDraw's strip
    /// pool, so the game can call it every frame with smoothed seconds. A fire's "sinceOut" is seconds since it went
    /// out (Release, or its time ran out); negative while it burns, for example minus the seconds left, or
    /// float.NegativeInfinity. The stand-in pawns and weapons of the sketch are not drawn here: the pawn is the
    /// game's, and the weapons are AmenoyodomiGraphics' and the projectiles'.
    ///
    /// Strands, tongues, wisps, veins and the tail are strips (VfxDraw.Strip) on a wavy spine with height drawn north by
    /// SixPathsHeight.Lift; blotches, flecks and shards are quads of RimArt/Rinnegan/BlackBlot and BlackShred
    /// (make_sasuke_textures.py, the lab's lab/black-blot and lab/black-shred pixel for pixel). Everything is screen
    /// oriented, so there is no per-facing drawing.
    ///
    /// Altitudes: strands rooted on the floor north of a pawn's feet draw under the pawn (Pawn - 0.02), the rest over
    /// it (MoteOverhead + 0.03). What the sketch lays on the stand-in pawn itself (the dark cover, the blotches, the
    /// blood) is drawn at Pawn + 0.037 + the sketch's offset, not Pawn + the offset: the game draws a pawn's parts up
    /// to 100 x 0.000366 = 0.0366 above its altitude (PawnRenderUtility.AltitudeForLayer), and the sketch's +0.003 to
    /// +0.01 would sit under its clothes and head.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class AmaterasuGraphics
    {
        private static readonly Material Shred = MaterialPool.MatFrom("RimArt/Rinnegan/BlackShred", ShaderDatabase.Transparent);
        private static readonly Material Blot = MaterialPool.MatFrom("RimArt/Rinnegan/BlackBlot", ShaderDatabase.Transparent);
        private static readonly Material Puff = MaterialPool.MatFrom("RimArt/SixPaths/Puff", ShaderDatabase.Transparent);
        private static readonly Material KunaiLying = MaterialPool.MatFrom("RimArt/Kunai/Kunai", ShaderDatabase.Cutout);
        private static readonly Material FumaLying = MaterialPool.MatFrom("RimArt/Fuma/Unfolded", ShaderDatabase.Cutout);

        // The flame is one colour, black; the blood and the eye glint are the only red.
        private static readonly Color Black = new Color(0.010f, 0.008f, 0.014f);
        private static readonly Color ScorchColour = new Color(0.05f, 0.035f, 0.04f), SmokeColour = new Color(0.17f, 0.16f, 0.18f);
        private static readonly Color Crimson = new Color(0.75f, 0.08f, 0.12f), EmberLit = new Color(0.85f, 0.16f, 0.14f);
        private static readonly Color BloodColour = new Color(0.52f, 0.03f, 0.05f);
        private static readonly Color VioletLight = new Color(0.50f, 0.24f, 0.95f), CrimsonLight = new Color(0.85f, 0.10f, 0.12f);

        private static readonly float PawnLayer = AltitudeLayer.Pawn.AltitudeFor();
        /// <summary>Just over the top of a real pawn's render tree (layer 100 x 0.000366).</summary>
        private static readonly float OnPawn = PawnLayer + 0.037f;
        // Over every picture the kit draws, as NegativeFlash is: MetaOverlays is past the camera's near plane at the
        // closest zoom in the game and a draw there does not show.
        private static readonly float TopLayer = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.4f;
        private const float Lift = SixPathsHeight.Lift;

        // Where the base and core tongues stand across a pawn's feet, as shares of the half-width, and how far north.
        private static readonly float[] BaseX = { -0.62f, -0.2f, 0.12f, 0.5f, -0.4f }, BaseZ = { 0.06f, -0.08f, 0.1f, -0.04f, -0.12f };
        private static readonly float[] CoreX = { -0.3f, 0.04f, 0.34f }, CoreZ = { 0.14f, 0.18f, 0.12f };
        private static readonly float[] KunaiRootsAt = { -0.24f, -0.16f, -0.08f, 0f, 0.08f, 0.16f, 0.24f };
        private static readonly float[] BladeRootsAt = { 0.16f, 0.27f, 0.38f, 0.47f, 0.55f }, BladeFull = { 0.7f, 1f, 0.95f, 0.65f };

        private const int StrandSteps = 16, WispSteps = 16, TailSteps = 12, VeinPoints = 7;
        private const int Parts = T.Strands + T.BaseTongues + T.CoreTongues;

        // Scratch, rewritten by each call: the last strand's spine and half-widths, a wisp's spine, the pawn's parts
        // and a small thing's flame roots.
        private static readonly Vector2[] spine = new Vector2[StrandSteps + 1];
        private static readonly float[] spineHalf = new float[StrandSteps + 1];
        private static readonly Vector2[] wispSpine = new Vector2[WispSteps + 1];
        private static readonly float[] wispAngle = new float[WispSteps + 1], wispHalf = new float[WispSteps + 1];
        private static readonly Vector2[] veinPoints = new Vector2[VeinPoints];
        private static readonly Part[] parts = new Part[Parts];
        private static readonly int[] partOrder = new int[Parts];
        private static readonly Root[] roots = new Root[32];
        private static readonly int[] rootOrder = new int[32];
        private static int rootCount;
        private static readonly Spot[] spots = new Spot[5];

        private struct Part { public int k; public bool isBase, core; public float x, dz, rootH; }
        private struct Root { public Vector2 at; public float h, full, hw; public int k; public bool vein, isBase; }
        private struct Spot { public Vector2 at; public float h, size; public int k; }

        // ---- The caster -----------------------------------------------------------------------------------------

        /// <summary>
        /// The red glint on the caster's eye while he looks: <paramref name="age"/> seconds since the warmup began. It
        /// grows from 0.16 to 0.24 and from half to full strength over the warmup and goes out 0.08 s after it. Call it
        /// from the start of the warmup until age reaches warmup + <see cref="AmaterasuTiming.GlintFade"/>.
        /// </summary>
        public static void Gaze(Vector2 eye, float age, float warmup = T.Warmup)
        {
            warmup = Mathf.Max(0.01f, warmup);
            if (age < 0f || age >= warmup + T.GlintFade) return;
            float size = 0.16f + 0.08f * Mathf.Clamp01(age / warmup);
            float alpha = age < warmup ? 0.5f + 0.5f * age / warmup : 1f - (age - warmup) / T.GlintFade;
            if (alpha <= 0f) return;
            Sprite(eye, size * 1.2f, size * 1.2f, Fade(Crimson, alpha * 0.7f), glow, Overhead + 0.2f);
            DrawMesh(MeshPool.plane10, eye, Overhead + 0.21f, size * 2f, size * 0.12f, 0f, Fade(EmberLit, alpha), whiteGlow);
            DrawMesh(MeshPool.plane10, eye, Overhead + 0.21f, size * 0.12f, size * 2f, 0f, Fade(EmberLit, alpha), whiteGlow);
        }

        /// <summary>
        /// The Bleeding eye: two streaks of blood run down from the casting eye, <paramref name="age"/> seconds after the
        /// cast. The first grows from 0.05 to 0.2 cells over 1.2 s, the second (0.03 to 0.11) starts 0.3 s later. Call
        /// it for as long as the Bleeding eye lasts; it has no end of its own.
        /// </summary>
        public static void Blood(Vector2 eye, float age)
        {
            if (age < 0f) return;
            float l1 = 0.05f + 0.15f * Smooth(age / T.BloodRun), l2 = 0.03f + 0.08f * Smooth((age - T.SecondStreak) / T.BloodRun);
            DrawMesh(MeshPool.plane10, new Vector2(eye.x + 0.025f, eye.y - 0.04f - l1 / 2f), OnPawn + 0.01f, 0.03f, l1, 0f, BloodColour, solid);
            if (age > T.SecondStreak)
                DrawMesh(MeshPool.plane10, new Vector2(eye.x - 0.02f, eye.y - 0.04f - l2 / 2f), OnPawn + 0.01f, 0.022f, l2, 0f, BloodColour, solid);
        }

        /// <summary>
        /// The screen dim at ignition, <paramref name="age"/> seconds after it: black at 0.18, in over 0.08 s and out by
        /// 0.18 s, as a 400-cell quad over the map just above the kit's pictures, centred on <paramref name="at"/>.
        /// </summary>
        public static void Dim(Vector2 at, float age)
        {
            if (age < 0f || age >= T.DimLife) return;
            float a = T.Dim * (age < T.DimIn ? age / T.DimIn : 1f - Smooth((age - T.DimIn) / (T.DimLife - T.DimIn)));
            DrawMesh(MeshPool.plane10, at, TopLayer, 400f, 400f, 0f, new Color(0f, 0f, 0f, a), solid);
        }

        // ---- The fire on a pawn ---------------------------------------------------------------------------------

        /// <summary>
        /// The black fire on one pawn standing with its feet on <paramref name="feet"/> (the game's DrawPos less 0.3
        /// cells north). <paramref name="age"/>: seconds the fire has shown, <see cref="AmaterasuTiming.FireAge"/> of the
        /// seconds since the hediff was added; for a spread it starts at -0.22, while the flecks jump across from
        /// <paramref name="from"/> (the feet of the pawn it caught from, which also sets the side it creeps in from).
        /// <paramref name="sinceOut"/>: seconds since it went out, negative while it burns. <paramref name="seed"/>: the
        /// pawn's thingIDNumber. <paramref name="scale"/> multiplies the half-width 0.55 and height 2.4 and the body
        /// offsets. <paramref name="burstSeed"/>: seed of a hit's chest burst; -1 takes seed + 1300.
        ///
        /// Draws, by <paramref name="how"/>: the cast's burst, shards, soot splash and ground flames and the swell from
        /// wide and low to a column; a hit's chest burst; then 26 strands, 5 base and 3 core tongues on their own
        /// grow-tear-regrow cycles with violet veins, 8 blotches over the body and a dark cover over it, wisps and flecks
        /// torn off the tops, 12 loose flecks, and after it goes out the flames sink over 0.3 s and grey smoke rises for
        /// 0.8 s. The floor is <see cref="PawnStain"/>. Call it until sinceOut reaches
        /// <see cref="AmaterasuTiming.OutDuration"/>.
        /// </summary>
        public static void Pawn(Vector2 feet, float age, float sinceOut, AmaterasuCatch how, int seed, Vector2 from = default,
            float scale = 1f, AmaterasuLight light = AmaterasuLight.Violet, int burstSeed = -1)
        {
            if (age <= sinceOut) return;
            seed = Seed(seed);
            Begin(feet);
            if (how == AmaterasuCatch.Spread && age < 0f)
            {
                Jump(from, feet, age + T.JumpTime);
                return;
            }
            if (age < 0f) return;

            float w = T.Width * scale, height = T.Height * scale;
            float rel = 1f - Smooth(sinceOut / T.Sink), clock = age * T.Speed, lastBirth = (age - sinceOut) * T.Speed;
            bool cast = how == AmaterasuCatch.Cast;
            Vector2 toward = from - feet;
            float side = how == AmaterasuCatch.Spread && toward.sqrMagnitude > 1e-8f ? toward.x / toward.magnitude : 0f;
            // Storm 4's growth on a cast: wide and low first, then a column over 0.9 s. Any other catch comes in at its
            // full shape.
            float swell = cast ? 1f - Smooth(age / T.Swell) : 0f, widthF = 1f + T.SwellWidth * swell, heightF = 1f - T.SwellHeight * swell;
            bool lit = LightOf(light, out Color lightColour);

            if (cast) Eruption(feet, age, seed, scale);
            if (how == AmaterasuCatch.Hit)
                Burst(Up(feet, 0.55f * scale), age, burstSeed >= 0 ? burstSeed : seed + 1300, 0.9f * scale, 8);
            if (cast && rel > 0f) Clumps(feet, age, clock, seed, height, scale, lit, lightColour);

            if (rel > 0f)
            {
                // The pawn inside: a dark cover and blotches clinging to it and licking upward.
                float cover = CatchAt(how, side, w, scale, 0f, age, 0.3f * scale) * rel;
                Sprite(At(feet, 0f, 0.3f * scale), 0.5f * scale, 0.9f * scale, Fade(Black, 0.3f * cover), soft, OnPawn + 0.003f);
                for (int i = 0; i < T.Blotches; i++)
                {
                    int k = seed + 500 + i * 11;
                    bool head = i >= 6;
                    float bx = (Rand(k) - 0.5f) * (head ? 0.2f : 0.36f) * scale;
                    float bz = (head ? 0.46f + Rand(k + 1) * 0.2f : -0.06f + Rand(k + 1) * 0.46f) * scale;
                    float g = CatchAt(how, side, w, scale, bx, age, bz) * rel;
                    if (g <= 0f) continue;
                    float period = 0.7f + Rand(k + 2) * 0.5f, u = ((clock + Rand(k + 3) * period) / period) % 1f;
                    float size = (head ? 0.17f : 0.21f) * (0.8f + 0.4f * Rand(k + 4)) * (1f - 0.35f * u) * g * scale;
                    Sprite(At(feet, bx + Mathf.Sin(clock * 3f + k) * 0.02f, bz + u * 0.14f), size, size * 1.15f,
                        Fade(Black, Mathf.Min(1f, Mathf.Sin(u * Mathf.PI) * 2.2f) * 0.95f), Blot, OnPawn + 0.004f + i * 0.0002f, Spin(k, clock));
                }
                Strands(feet, w, height, age, clock, lastBirth, rel, swell, widthF, heightF, how, side, scale, seed, lit, lightColour);
            }

            // Loose flecks rising out of the flames. Births stop when it goes out; flecks in the air finish.
            for (int i = 0; i < T.LooseFlecks; i++)
            {
                int k = seed + 300 + i * 17;
                float period = 0.6f + Rand(k) * 0.35f, offset = Rand(k + 1) * period;
                int n = Mathf.FloorToInt((Mathf.Min(clock, lastBirth) - offset) / period);
                if (n < 0) continue;
                float born = offset + n * period, u = (clock - born) / period;
                if (u < 0f || u >= 1f) continue;
                int r = k + n * 31;
                float x = (Rand(r) * 2f - 1f) * w * 0.8f, z0 = (Rand(r + 5) - 0.5f) * 0.2f * scale;
                float h0 = height * 0.5f * (0.4f + 0.6f * Rand(r + 2)) * CatchAt(how, side, w, scale, x, born / T.Speed, 0f);
                float size = 0.05f + Rand(r + 4) * 0.07f, wave = Mathf.Sin(u * 5f + r);
                Sprite(At(feet, x + wave * 0.1f, z0, h0 + u * (0.8f + Rand(r + 3) * 0.8f)), size * (1f - 0.5f * u), size * 2f * (1f - 0.4f * u),
                    Fade(Black, Mathf.Min(1f, Mathf.Sin(u * Mathf.PI) * 1.4f)), Shred, Overhead + 0.13f + i * 0.0002f, wave * 35f);
            }

            // After it goes out: a last puff of grey smoke rises from above the head and thins out.
            if (sinceOut >= 0f && sinceOut < T.SmokeLife)
                for (int i = 0; i < 6; i++)
                {
                    int k = seed + 700 + i * 5;
                    float u = Mathf.Clamp01((sinceOut - i * 0.04f) / (T.SmokeLife - 0.2f));
                    if (u <= 0f || u >= 1f) continue;
                    float size = (0.35f + u * 0.6f) * 1.3f;
                    Sprite(At(feet, (Rand(k) - 0.5f) * w * 1.2f, 0f, (1.1f + u * (1.2f + Rand(k + 1))) * scale), size, size,
                        Fade(SmokeColour, 0.22f * Mathf.Sin(u * Mathf.PI)), Puff, Overhead + 0.12f + i * 0.0002f);
                }
        }

        /// <summary>
        /// The floor round a burning pawn: a wide soft shadow (4.6 half-widths across), a scorch (2.3) and a dark pool
        /// (1.9), in over 0.25 s. After the fire goes out the shadow thins to 0.3 and the pool to half; the scorch stays.
        /// All of it holds for <see cref="AmaterasuTiming.StainHold"/> s after the fire went out and fades over
        /// <see cref="AmaterasuTiming.StainFade"/> s. <paramref name="age"/> is the same fire age as
        /// <see cref="Pawn"/>'s; call it at the pawn's feet while it burns and at the point where it went out afterwards,
        /// until sinceOut reaches <see cref="AmaterasuTiming.StainDuration"/>.
        /// </summary>
        public static void PawnStain(Vector2 feet, float age, float sinceOut, int seed, float scale = 1f)
        {
            float w = T.Width * scale;
            Stain(feet, At(feet, 0f, -0.05f * scale), age, sinceOut, Seed(seed), w * 4.6f, 0.38f, w * 2.3f, w * 1.9f, 0.65f);
        }

        /// <summary>The pawn's strands and tongues and what tears off their tops, drawn north to south.</summary>
        private static void Strands(Vector2 c, float w, float height, float age, float clock, float lastBirth, float rel, float swell,
            float widthF, float heightF, AmaterasuCatch how, float side, float scale, int seed, bool lit, Color light)
        {
            for (int i = 0; i < Parts; i++)
            {
                int k = seed + i * 13;
                bool isBase = i >= T.Strands, core = i >= T.Strands + T.BaseTongues;
                float rootH = 0f;
                // Base tongues sit at fixed, uneven places across the feet so they never line up into a block.
                float x = core ? CoreX[i - T.Strands - T.BaseTongues] * w : isBase ? BaseX[i - T.Strands] * w : (Rand(k) * 2f - 1f) * w * 0.8f;
                float dz = (core ? CoreZ[i - T.Strands - T.BaseTongues] : isBase ? BaseZ[i - T.Strands] : (Rand(k + 1) - 0.5f) * 0.3f) * scale;
                if (!isBase && Rand(k + 2) > 0.7f) rootH = (0.2f + Rand(k + 3) * 0.6f) * scale; // a few start on the body
                parts[i] = new Part { k = k, isBase = isBase, core = core, x = x, dz = dz, rootH = rootH };
                // Stable insertion by dz, north first.
                int j = i;
                while (j > 0 && parts[partOrder[j - 1]].dz < dz) { partOrder[j] = partOrder[j - 1]; j--; }
                partOrder[j] = i;
            }

            for (int order = 0; order < Parts; order++)
            {
                int i = partOrder[order];
                Part q = parts[i];
                int k = q.k;
                float x = q.x, dz = q.dz, rootH = q.rootH;
                float env = Mathf.Sqrt(Mathf.Max(0f, 1f - (x / w) * (x / w))), tall = Rand(k + 4);
                float full = height * (q.core ? 0.68f + 0.2f * tall : q.isBase ? 0.18f + 0.22f * tall : (0.35f + 0.65f * env) * (0.45f + 0.55f * tall));
                float hw = (q.core ? 0.15f + 0.05f * Rand(k + 5) : q.isBase ? 0.09f + 0.06f * Rand(k + 5) : 0.035f + 0.06f * (1f - tall)) * scale;
                float period = q.isBase ? 0.4f + Rand(k + 6) * 0.2f : 0.55f + Rand(k + 6) * 0.4f, offset = Rand(k + 7) * period;
                Cycle(clock, period, offset, out int n0, out float grown);
                float g = CatchAt(how, side, w, scale, x, age, rootH);
                float h = full * grown * g * heightF * rel;
                Vector2 root = At(c, x * widthF, dz, rootH);
                float lean = x / w * (q.isBase ? 0.25f : 0.1f) + 0.03f * Mathf.Sin(clock * 1.3f + k);
                bool behind = rootH == 0f && dz > 0.02f * scale;
                float layer = (behind ? PawnLayer - 0.02f : Overhead + 0.03f) + order * 0.0004f;
                bool drawn = Strand(root, h, hw * (0.5f + 0.5f * rel) * (q.isBase ? 1f + 0.6f * swell : 1f), clock, k, Mathf.Min(1f, g * 3f),
                    layer, lean, rootH > 0f ? 0.2f : 0.6f);
                if (drawn && lit) Veins(clock, k, g * rel * (q.isBase ? 0.8f : 1f), layer + 0.0001f, light);

                // The torn-off top of this strand's last two cycles. From the flanks it is a curling wisp that flies
                // outward (Storm 4); from the middle a small fleck that rises. None are born after it goes out.
                if (q.isBase) continue;
                for (int m = 0; m < 2; m++)
                {
                    int n = n0 - m;
                    float tear = (n + 0.8f) * period - offset, life = 0.5f + Rand(k + n * 31) * 0.3f, fu = (clock - tear) / life;
                    if (tear < 0f || tear > lastBirth || fu < 0f || fu >= 1f) continue;
                    int kk = k + n * 31;
                    float top = full * CatchAt(how, side, w, scale, x, tear / T.Speed, rootH);
                    float fade = 1f - Smooth((fu - 0.45f) / 0.55f);
                    if (Mathf.Abs(x) > w * 0.45f)
                    {
                        // About a third of the flank tears throw a wisp: 3-6 in the air at once, as in Storm 4.
                        if (Rand(kk + 9) > 0.35f) continue;
                        float out_ = 1f - (1f - fu) * (1f - fu), flank = x >= 0f ? 1f : -1f;
                        Vector2 start = At(c, x * widthF + lean * top + flank * out_ * (0.25f + Rand(kk + 1) * 0.35f), dz,
                            rootH + top * 0.75f + out_ * (0.3f + Rand(kk + 2) * 0.45f) * height / T.Height);
                        float spread = (20f + Rand(kk + 3) * 50f) * Mathf.Deg2Rad, dir = flank > 0f ? spread : Mathf.PI - spread;
                        Wisp(start, dir, (0.35f + Rand(kk + 4) * 0.3f) * (1f - 0.45f * fu) * height / T.Height,
                            (0.07f + Rand(kk + 7) * 0.05f) * (1f - 0.3f * fu), -flank * (2.6f + Rand(kk + 6) * 2f + fu * 2.5f),
                            fade * Mathf.Min(1f, T.Flecks), Overhead + 0.127f + (i % 20) * 0.0003f, lit, light, kk);
                        continue;
                    }
                    Vector2 pos = At(c, x * widthF + lean * top + (Rand(kk + 1) - 0.5f) * 0.25f * fu + Mathf.Sin(fu * 5f + kk) * 0.05f, dz,
                        rootH + top * 0.85f + fu * (0.5f + Rand(kk + 2) * 0.6f) * height / T.Height);
                    // About twice as tall as wide: a torn piece, not a leaf.
                    float fw = Mathf.Max(0.08f, hw * 2f) * (0.8f + 0.5f * Rand(kk + 3)) * (1f - 0.45f * fu);
                    Sprite(pos, fw, fw * (1.5f + 0.7f * Rand(kk + 4)) * (1f - 0.2f * fu), Fade(Black, fade * Mathf.Min(1f, T.Flecks)), Shred,
                        Overhead + 0.128f + (i % 20) * 0.0002f, (Rand(kk + 5) - 0.5f) * 70f + Mathf.Sin(fu * 4f + kk) * 25f);
                }
            }
        }

        /// <summary>
        /// How far part x (cells across from the middle, h up the body) has caught, 0 to 1, at age a. It catches after a
        /// delay: from the touching side on a spread, from the feet up on a step-in, from the chest out otherwise.
        /// </summary>
        private static float CatchAt(AmaterasuCatch how, float side, float w, float scale, float x, float a, float h)
        {
            float delay = how == AmaterasuCatch.Spread ? Mathf.Clamp01((-side * x / w + 1f) / 2f) * T.SpreadCreep
                : how == AmaterasuCatch.Stepped ? Mathf.Clamp01(h / (T.StepHeight * scale)) * T.StepCreep
                : Mathf.Abs(x) / w * T.ChestCreep;
            return Smooth((a - delay) / T.Rise);
        }

        /// <summary>
        /// A strand's life on the flame clock: it grows from 35 % to full height over the first 45 % of its cycle, holds,
        /// and over the last 20 % its top tears off while the stem drops back to 35 %. n is the cycle number.
        /// </summary>
        private static void Cycle(float clock, float period, float offset, out int n, out float grown)
        {
            float c = (clock + offset) / period;
            n = Mathf.FloorToInt(c);
            float u = c - n;
            grown = 0.35f + 0.65f * (Smooth(u / 0.45f) - Smooth((u - 0.8f) / 0.2f));
        }

        /// <summary>
        /// The neighbour's catch: three flecks jump from the pawn at <paramref name="a"/> to the one at
        /// <paramref name="b"/> in an arc 0.75-1.2 cells up, <paramref name="age"/> 0 to 0.22 s. Skipped when the two
        /// are on one spot or more than 3 cells apart (no source point given).
        /// </summary>
        private static void Jump(Vector2 a, Vector2 b, float age)
        {
            if (age < 0f || age > T.JumpTime) return;
            Vector2 d = b - a;
            float apart = d.magnitude;
            if (apart < 0.05f || apart > 3f) return;
            d /= apart;
            Vector2 start = a + d * 0.3f, end = b - d * 0.2f;
            for (int i = 0; i < 3; i++)
            {
                float u = Mathf.Clamp01((age - i * 0.03f) / (T.JumpTime - 0.06f));
                if (u <= 0f || u >= 1f) continue;
                float h = 0.75f + 0.45f * Mathf.Sin(u * Mathf.PI) + i * 0.12f;
                var pos = new Vector2(Mathf.Lerp(start.x, end.x, u), Mathf.Lerp(start.y, end.y, u) + (i - 1) * 0.07f + h * Lift);
                float slope = Mathf.Atan2(end.y - start.y + 0.45f * Mathf.PI * Mathf.Cos(u * Mathf.PI) * Lift, end.x - start.x);
                Sprite(pos, 0.07f, 0.2f, Fade(Black, Mathf.Sin(u * Mathf.PI) * 1.5f), Shred, Overhead + 0.135f + i * 0.0003f,
                    90f - slope * Mathf.Rad2Deg);
            }
        }

        /// <summary>
        /// The cast: a black mass bursts out of the chest (5 blots, 0.2-0.3 s) and throws 18 shards up and out (the
        /// upward ones furthest, none straight down, 0.32-0.57 s), and a soot splash rings the feet (0.35 s).
        /// </summary>
        private static void Eruption(Vector2 c, float age, int seed, float scale)
        {
            if (age < 0f || age > T.EruptionLife) return;
            float splash = age / T.SplashLife;
            if (splash < 1f)
            {
                float d = (0.9f + 2.4f * Smooth(splash)) * scale;
                Sprite(c, d, d, Fade(Black, 0.5f * (1f - splash)), Puff, Floor + 0.022f);
            }
            Vector2 focus = Up(c, 0.55f * scale);
            for (int i = 0; i < T.Masses; i++)
            {
                int k = seed + 950 + i * 3;
                float u = age / (0.2f + Rand(k) * 0.1f);
                if (u >= 1f) continue;
                float size = (0.35f + 0.7f * Smooth(u * 2f)) * (0.7f + 0.5f * Rand(k + 1)) * scale;
                Sprite(At(focus, (Rand(k + 2) - 0.5f) * 0.4f, (Rand(k + 3) - 0.3f) * 0.5f), size, size * 1.2f,
                    Fade(Black, 1f - Smooth((u - 0.4f) / 0.6f)), Blot, Overhead + 0.139f + i * 0.0002f, Rand(k + 4) * 360f);
            }
            for (int i = 0; i < T.Shards; i++)
            {
                int k = seed + 900 + i * 7;
                float life = 0.32f + Rand(k) * 0.25f, u = age / life;
                if (u >= 1f) continue;
                float a = (15f + Rand(k + 1) * 150f) * Mathf.Deg2Rad;
                float d = (0.5f + Rand(k + 2) * 1.1f) * (0.7f + 0.5f * Mathf.Sin(a)) * scale * (1f - Cube(1f - u));
                float len = (0.22f + Rand(k + 3) * 0.35f) * scale * (1f - 0.5f * u), wid = len * (0.22f + Rand(k + 4) * 0.18f);
                Sprite(new Vector2(focus.x + Mathf.Cos(a) * d, focus.y + Mathf.Sin(a) * d), wid, len,
                    Fade(Black, 1f - Smooth((u - 0.45f) / 0.55f)), Shred, Overhead + 0.14f + i * 0.0003f, 90f - a * Mathf.Rad2Deg);
            }
        }

        /// <summary>
        /// A small black burst at <paramref name="pos"/> (already lifted): a blot swells for 0.22 s and
        /// <paramref name="count"/> shards fly up and out, none straight down, gone by 0.5 s. A held weapon catching, and
        /// a burning weapon striking a pawn's chest.
        /// </summary>
        private static void Burst(Vector2 pos, float age, int seed, float scale, int count)
        {
            if (age < 0f || age > T.BurstLife) return;
            float u = age / 0.22f;
            if (u < 1f)
            {
                float size = (0.25f + 0.55f * Smooth(u * 2f)) * scale;
                Sprite(pos, size, size * 1.15f, Fade(Black, 1f - Smooth((u - 0.4f) / 0.6f)), Blot, Overhead + 0.139f, Rand(seed) * 360f);
            }
            for (int i = 0; i < count; i++)
            {
                int k = seed + 900 + i * 7;
                float life = 0.25f + Rand(k) * 0.2f, v = age / life;
                if (v >= 1f) continue;
                float a = (20f + Rand(k + 1) * 140f) * Mathf.Deg2Rad, d = (0.25f + Rand(k + 2) * 0.55f) * scale * (1f - Cube(1f - v));
                float len = (0.14f + Rand(k + 3) * 0.2f) * scale * (1f - 0.5f * v), wid = len * (0.25f + Rand(k + 4) * 0.15f);
                Sprite(new Vector2(pos.x + Mathf.Cos(a) * d, pos.y + Mathf.Sin(a) * d), wid, len,
                    Fade(Black, 1f - Smooth((v - 0.45f) / 0.55f)), Shred, Overhead + 0.14f + i * 0.0003f, 90f - a * Mathf.Rad2Deg);
            }
        }

        /// <summary>
        /// Storm 4: as the target catches, 6 small black flames (3 strands each) pop up 0.6-1.05 cells round it, 0.035 s
        /// apart, and die down within 0.7-1.1 s. Splash, not a burning area. North of the feet they draw under the pawn.
        /// </summary>
        private static void Clumps(Vector2 c, float age, float clock, int seed, float height, float scale, bool lit, Color light)
        {
            if (age < 0f || age > T.ClumpLife) return;
            for (int i = 0; i < T.Clumps; i++)
            {
                int k = seed + 800 + i * 19;
                float t0 = 0.02f + i * 0.035f, u = (age - t0) / (0.7f + Rand(k) * 0.4f);
                if (u <= 0f || u >= 1f) continue;
                float a = (i * 60f + 25f + Rand(k + 1) * 30f) * Mathf.Deg2Rad, r = (0.6f + Rand(k + 2) * 0.45f) * scale;
                Vector2 q = At(c, Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                float life = Smooth(u / 0.15f) * (1f - Smooth((u - 0.55f) / 0.45f));
                float layer = (q.y > c.y + 0.05f ? PawnLayer - 0.03f : Overhead + 0.025f) + i * 0.001f;
                for (int j = 0; j < 3; j++)
                {
                    int kk = k + j * 7;
                    bool drawn = Strand(At(q, (j - 1) * 0.07f, 0f), (0.3f + Rand(kk) * 0.35f) * life * height / T.Height,
                        0.045f + Rand(kk + 1) * 0.025f, clock, kk, Mathf.Min(1f, life * 3f), layer + j * 0.0002f, (j - 1) * 0.15f);
                    if (drawn && lit) Veins(clock, kk, life * 0.8f, layer + j * 0.0002f + 0.0001f, light);
                }
            }
        }

        // ---- Strands, veins and wisps ---------------------------------------------------------------------------

        /// <summary>
        /// One strand: a black ribbon on a wavy spine, 16 steps from <paramref name="root"/> (drawn point) up
        /// <paramref name="h"/> cells. The wave runs up it, each edge boils upward on its own rhythm, and the width tapers
        /// from rootW of <paramref name="hw"/> at the root to a point. <paramref name="lean"/> and <paramref name="leanZ"/>
        /// push the tip east and north per cell of height. Leaves its spine in the scratch for <see cref="Veins"/>; false
        /// when too small to draw.
        /// </summary>
        private static bool Strand(Vector2 root, float h, float hw, float clock, int seed, float alpha, float layer, float lean,
            float rootW = 0.6f, float leanZ = 0f)
        {
            if (h < 0.03f || hw < 0.004f || alpha <= 0.003f) return false;
            float phase = Rand(seed) * 6.283f, f1 = 5f + Rand(seed + 1) * 3f, f2 = 2f + Rand(seed + 2) * 1.5f;
            float amp = 0.05f + 0.05f * h;
            Sides(StrandSteps + 1, out Vector2[] left, out Vector2[] right);
            for (int j = 0; j <= StrandSteps; j++)
            {
                float u = j / (float)StrandSteps;
                float sway = amp * u * (0.7f * Mathf.Sin(u * f1 - clock * 7f + phase) + 0.5f * u * Mathf.Sin(u * f2 - clock * 3.3f + phase * 1.7f));
                float cx = root.x + lean * u * h + sway, cz = root.y + h * Lift * u + leanZ * u * h;
                float profile = (rootW + (1f - rootW) * Mathf.Sin(Mathf.Min(1f, u / 0.3f) * Mathf.PI / 2f)) * Mathf.Pow(1f - u, 0.8f);
                float ragL = 1f + 0.38f * Mathf.Sin(u * 17f - clock * 10f + phase) * Mathf.Sin(u * 5.3f + phase * 2f);
                float ragR = 1f + 0.38f * Mathf.Sin(u * 15f - clock * 9f + phase * 1.3f) * Mathf.Sin(u * 4.1f + phase * 3f);
                left[j] = new Vector2(cx - hw * profile * ragL, cz);
                right[j] = new Vector2(cx + hw * profile * ragR, cz);
                spine[j] = new Vector2(cx, cz);
                spineHalf[j] = hw * profile;
            }
            Strip(left, right, Fade(Black, alpha), solid, layer);
            return true;
        }

        /// <summary>
        /// The light inside the last strand (Storm 4's violet streaks): two thin wavy lines crawl up it and flicker in
        /// steps 12 times a second, plus one speck. They stay inside the strand's width, so they only show on the black.
        /// </summary>
        private static void Veins(float clock, int seed, float alpha, float layer, Color light)
        {
            if (alpha <= 0.01f) return;
            int step = Mathf.FloorToInt(clock * 12f);
            for (int v = 0; v < 2; v++)
            {
                int k = seed + v * 101;
                float len = 0.2f + Rand(k) * 0.2f;
                float f = (clock * (0.45f + Rand(k + 1) * 0.35f) + Rand(k + 2)) % 1f;
                float u0 = 0.05f + f * (0.8f - len), off = (Rand(k + 3) - 0.5f) * 0.55f;
                float a = alpha * (0.3f + 0.7f * Rand(step * 13 + k)) * Mathf.Sin(f * Mathf.PI);
                if (a <= 0.01f) continue;
                for (int j = 0; j < VeinPoints; j++)
                {
                    float u = u0 + len * j / (VeinPoints - 1f);
                    Vector2 q = Along(u, off);
                    veinPoints[j] = new Vector2(q.x + Mathf.Sin(u * 23f + clock * 9f + k) * 0.012f, q.y);
                }
                Trail(veinPoints, 0.018f + Rand(k + 4) * 0.01f, Fade(light, a), whiteGlow, layer);
            }
            Vector2 speck = Along(0.15f + Rand(seed + 7) * 0.5f, (Rand(seed + 8) - 0.5f) * 0.8f);
            Sprite(speck, 0.05f, 0.05f, Fade(light, alpha * Rand(step * 7 + seed) * 0.9f), glow, layer + 0.00005f);
        }

        /// <summary>The point u (0 to 1) up the last strand's spine, off half-widths across it.</summary>
        private static Vector2 Along(float u, float off)
        {
            float f = Mathf.Clamp01(u) * StrandSteps, t;
            int i0 = Mathf.Min(StrandSteps - 1, Mathf.FloorToInt(f));
            t = f - i0;
            Vector2 p0 = spine[i0], p1 = spine[i0 + 1];
            float half = spineHalf[i0] + (spineHalf[i0 + 1] - spineHalf[i0]) * t;
            return new Vector2(p0.x + (p1.x - p0.x) * t + off * half, p0.y + (p1.y - p0.y) * t);
        }

        /// <summary>
        /// A black wisp flung off the side of the flame (Storm 4, 1:08-1:09): a brush stroke with a blunt, ragged head
        /// that leaves at <paramref name="dir"/> radians, thins along its length and curls at the tail by
        /// <paramref name="curl"/> radians per cell (its sign is the side it curls to). The light is a short streak on
        /// the inside of the curl near the head.
        /// </summary>
        private static void Wisp(Vector2 start, float dir, float len, float width, float curl, float alpha, float layer, bool lit,
            Color light, int seed)
        {
            if (alpha <= 0.01f || len < 0.02f) return;
            float ds = len / WispSteps, ang = dir, x = start.x, z = start.y;
            for (int j = 0; j <= WispSteps; j++)
            {
                float t = j / (float)WispSteps;
                wispSpine[j] = new Vector2(x, z);
                wispAngle[j] = ang;
                wispHalf[j] = width * 0.5f * (t < 0.1f ? 0.55f + 4.5f * t : 1f) * Mathf.Pow(1f - t, 1.1f);
                ang += curl * 3f * t * t * ds;
                x += Mathf.Cos(ang) * ds;
                z += Mathf.Sin(ang) * ds;
            }
            Sides(WispSteps + 1, out Vector2[] left, out Vector2[] right);
            for (int j = 0; j <= WispSteps; j++)
            {
                var normal = new Vector2(-Mathf.Sin(wispAngle[j]), Mathf.Cos(wispAngle[j]));
                left[j] = wispSpine[j] + normal * wispHalf[j];
                right[j] = wispSpine[j] - normal * wispHalf[j];
            }
            Strip(left, right, Fade(Black, alpha), solid, layer);
            Sprite(start, width * 1.25f, width * 1.25f, Fade(Black, alpha), Blot, layer - 0.00005f, Rand(seed) * 360f);
            if (!lit) return;
            float inside = curl < 0f ? -1f : 1f;
            for (int j = 0; j < VeinPoints; j++)
            {
                int s = j + 1;
                veinPoints[j] = new Vector2(wispSpine[s].x - Mathf.Sin(wispAngle[s]) * wispHalf[s] * 0.5f * inside,
                    wispSpine[s].y + Mathf.Cos(wispAngle[s]) * wispHalf[s] * 0.5f * inside);
            }
            Trail(veinPoints, width * 0.2f, Fade(light, alpha * 0.6f), whiteGlow, layer + 0.0001f);
        }

        /// <summary>A line through <paramref name="points"/>, <paramref name="width"/> across at its middle and nothing at either end (the lab's trail).</summary>
        private static void Trail(Vector2[] points, float width, Color colour, Material material, float layer)
        {
            int n = points.Length;
            Sides(n, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i < n; i++)
            {
                Vector2 along = points[Mathf.Min(n - 1, i + 1)] - points[Mathf.Max(0, i - 1)];
                float length = along.magnitude;
                if (length == 0f) length = 1f;
                float half = Mathf.Sin(i / (n - 1f) * Mathf.PI) * width / 2f;
                var side = new Vector2(-along.y / length * half, along.x / length * half);
                a[i] = points[i] + side;
                b[i] = points[i] - side;
            }
            Strip(a, b, colour, material, layer);
        }

        // ---- Burning weapons ------------------------------------------------------------------------------------

        /// <summary>
        /// The small black burst where a held weapon catches, <paramref name="age"/> seconds after it caught, at the
        /// point it hung over then (<paramref name="ground"/>; drawn at AmenoyodomiGraphics.Hold). A kunai: one burst of
        /// 6 shards. The Fūma: one at the hub and one on each blade 0.02-0.11 s later, <paramref name="turn"/> its turn
        /// then. Gone by 0.61 s. <paramref name="seed"/> is the weapon's.
        /// </summary>
        public static void Lit(bool fuma, Vector2 ground, float turn, float age, int seed)
        {
            if (age < 0f || age > T.BurstLife + 0.11f) return;
            seed = Seed(seed);
            Begin(ground);
            if (!fuma)
            {
                Burst(Up(ground, AmenoyodomiGraphics.Hold), age, seed, 0.7f, 6);
                return;
            }
            Burst(Up(ground, AmenoyodomiGraphics.Hold), age, seed + 90, 0.8f, 6);
            for (int k = 0; k < 4; k++)
                Burst(Up(Blade(ground, turn, k, 0.38f), AmenoyodomiGraphics.Hold), age - 0.02f - k * 0.03f, seed + k * 20, 0.75f, 5);
        }

        /// <summary>
        /// Flames on a weapon Amenoyodomi holds, hanging over <paramref name="ground"/> (drawn at Hold). A kunai: seven
        /// strands along it, tallest over its middle (up to 0.8 cells), two broad base tongues and two clinging
        /// blotches, along <paramref name="deg"/> (its heading). The Fūma: four strands and a tip tongue on each blade,
        /// up to 1.15 cells, measured off RimArt/Fuma/Unfolded (blade k runs at 22 + 40 r + 90 k degrees at r texture
        /// widths from the middle, turned by <paramref name="turn"/> degrees clockwise), a base tongue per blade, and
        /// blotches on the hub and blades. <paramref name="age"/>: seconds since it caught (the flames grow in over
        /// 0.15 s, the Fūma's 0.2 s). <paramref name="order"/>: 0 for the weapon furthest north, one more per weapon
        /// south of it. Call <see cref="Lit"/> for the burst.
        /// </summary>
        public static void HeldFlames(bool fuma, Vector2 ground, float deg, float turn, float age, float sinceOut, int seed, int order = 0,
            float scale = 1f, AmaterasuLight light = AmaterasuLight.Violet) =>
            WeaponFlames(fuma, ground, deg, turn, age, sinceOut, seed, order, scale, light, false);

        /// <summary>
        /// Flames on a lit weapon in flight after a let-go, over <paramref name="ground"/> (drawn at Hold), heading
        /// <paramref name="deg"/>. A kunai's flames are 0.7 as tall and lean back 1.3 cells per cell of height. On the
        /// Fūma the blades spin too fast to root flames on, so ten tongues stand round the rim, longest at the back and
        /// bent back 1.2 cells per cell, with three base tongues over the hub and a blotch on it. No flecks tear off
        /// (<see cref="PathFlecks"/> drops them along the path). Arguments as <see cref="HeldFlames"/>.
        /// </summary>
        public static void FlyingFlames(bool fuma, Vector2 ground, float deg, float age, float sinceOut, int seed, int order = 0,
            float scale = 1f, AmaterasuLight light = AmaterasuLight.Violet) =>
            WeaponFlames(fuma, ground, deg, 0f, age, sinceOut, seed, order, scale, light, true);

        private static void WeaponFlames(bool fuma, Vector2 ground, float deg, float turn, float age, float sinceOut, int seed, int order,
            float scale, AmaterasuLight light, bool flying)
        {
            if (age < 0f || age <= sinceOut) return;
            float rel = 1f - Smooth(sinceOut / T.Sink);
            if (rel <= 0f) return;
            seed = Seed(seed);
            Begin(ground);
            float clock = age * T.Speed, g = Smooth(age / (fuma ? T.FumaCatch : T.KunaiCatch)), lastBirth = (age - sinceOut) * T.Speed;
            float layer = Overhead + 0.06f + order * 0.006f, h = AmenoyodomiGraphics.Hold;
            Vector2 e = Turn(deg), lean = flying ? -e * (fuma ? T.FumaLean : T.TailLean) : Vector2.zero;
            bool lit = LightOf(light, out Color lightColour);
            int spotCount;
            rootCount = 0;
            if (!fuma)
            {
                // Seven strands along the kunai and two broad base tongues that join them at the bottom, so it reads as
                // one flame, not a comb.
                for (int j = 0; j < KunaiRootsAt.Length; j++)
                {
                    float u = KunaiRootsAt[j], env = 1f - (u / 0.3f) * (u / 0.3f);
                    int k = seed + j * 17;
                    AddRoot(ground + e * u, h, k, (0.3f + 0.45f * env) * (0.8f + 0.3f * Rand(k + 3)) * (flying ? T.KunaiFlying : 1f) * scale,
                        0.035f + 0.025f * Rand(k + 5), j == 2 || j == 4, false);
                }
                AddRoot(ground + e * -0.1f, h, seed + 90, 0.2f * scale, 0.075f, false, true);
                AddRoot(ground + e * 0.08f, h, seed + 97, 0.2f * scale, 0.075f, false, true);
                spots[0] = new Spot { at = ground + e * -0.05f, h = h, k = seed + 60, size = 0.13f };
                spots[1] = new Spot { at = ground + e * 0.12f, h = h, k = seed + 69, size = 0.13f };
                spotCount = 2;
            }
            else
            {
                spots[0] = new Spot { at = ground, h = h, k = seed + 60, size = 0.2f };
                spotCount = 1;
                if (!flying)
                    for (int k = 0; k < 4; k++)
                        spots[spotCount++] = new Spot { at = Blade(ground, turn, k, 0.33f), h = h, k = seed + 60 + (k + 1) * 9, size = 0.15f };
                if (flying) FumaTrailRoots(ground, e, h, seed, scale);
                else FumaRoots(ground, turn, h, seed, scale);
            }
            Cling(spotCount, clock, g, rel, layer - 0.002f);
            SmallFlames(clock, g, rel, lean, lit, lightColour, layer, lastBirth);
        }

        /// <summary>
        /// The black tail a flying burning kunai drags: a ragged band over the last 0.09 s of its path, 0.07 cells wide at
        /// the weapon, at Hold height. <paramref name="head"/>: its ground point now, or where it stopped;
        /// <paramref name="speed"/>: cells/s; <paramref name="flown"/>: seconds from the let-go to now or to where it
        /// stopped; <paramref name="after"/>: seconds since it stopped (0 while it flies; the tail runs into it over
        /// 0.09 s). <paramref name="age"/>: seconds since it caught fire.
        /// </summary>
        public static void Tail(Vector2 head, float deg, float speed, float flown, float after, float age, float sinceOut, int seed)
        {
            if (age < 0f || age <= sinceOut) return;
            float width = 0.07f * (1f - Smooth(sinceOut / T.Sink));
            float span = Mathf.Min(flown, T.TailTime - Mathf.Max(0f, after));
            if (span <= 0.003f || width <= 0f) return;
            seed = Seed(seed);
            Begin(head);
            float clock = age * T.Speed;
            Vector2 a = Up(head, AmenoyodomiGraphics.Hold), b = a - Turn(deg) * (speed * span);
            Vector2 d = a - b;
            float len = d.magnitude;
            if (len < 0.02f) return;
            var normal = new Vector2(-d.y / len, d.x / len);
            Sides(TailSteps + 1, out Vector2[] left, out Vector2[] right);
            for (int j = 0; j <= TailSteps; j++)
            {
                float u = j / (float)TailSteps, w = width * Mathf.Pow(1f - u, 0.7f);
                Vector2 p = Vector2.LerpUnclamped(a, b, u);
                float wl = w * (1f + 0.4f * Mathf.Sin(u * 17f + clock * 30f + seed)), wr = w * (1f + 0.4f * Mathf.Sin(u * 13f - clock * 27f + seed * 1.7f));
                left[j] = p + normal * wl;
                right[j] = p - normal * wr;
            }
            Strip(left, right, Fade(Black, 0.8f), solid, Overhead + 0.125f);
        }

        /// <summary>
        /// Flecks a burning weapon leaves in the air along its path: one every <see cref="AmaterasuTiming.FleckGap"/>
        /// cells, each rising a little and fading within 0.3-0.45 s. <paramref name="from"/>: the ground point it was let
        /// go at; <paramref name="speed"/>: cells/s; <paramref name="flown"/>: seconds it flew burning (to now, or to
        /// where it stopped or the fire went out); <paramref name="since"/>: seconds since the let-go. Call it until since
        /// exceeds flown by 0.45 s.
        /// </summary>
        public static void PathFlecks(Vector2 from, float deg, float speed, float flown, float since, int seed)
        {
            if (flown <= 0f || speed <= 0f || since < 0f) return;
            seed = Seed(seed);
            Begin(from);
            Vector2 dir = Turn(deg);
            float reach = flown * speed;
            int first = Mathf.Max(0, Mathf.FloorToInt((since - 0.45f) * speed / T.FleckGap) - 1);
            for (int i = first; i * T.FleckGap < reach; i++)
            {
                int k = seed + 400 + i * 13;
                float along = (i + Rand(k)) * T.FleckGap, born = along / speed;
                if (born >= flown) break;
                float life = 0.3f + Rand(k + 1) * 0.15f, u = (since - born) / life;
                if (u < 0f || u >= 1f) continue;
                Vector2 q = from + dir * along;
                float size = 0.05f + Rand(k + 2) * 0.06f;
                Sprite(At(q, (Rand(k + 3) - 0.5f) * 0.25f, (Rand(k + 4) - 0.5f) * 0.15f, AmenoyodomiGraphics.Hold + 0.1f + u * (0.25f + Rand(k + 5) * 0.3f)),
                    size * (1f - 0.4f * u), size * 2f * (1f - 0.3f * u), Fade(Black, Mathf.Min(1f, Mathf.Sin(u * Mathf.PI) * 1.6f)), Shred,
                    Overhead + 0.13f + (i % 64) * 0.0002f, (Rand(k + 6) - 0.5f) * 60f);
            }
        }

        /// <summary>
        /// A burning weapon lying on the floor at <paramref name="ground"/>, or the patch a lit conjured kunai leaves on
        /// its cell (<see cref="Lying"/> draws the kunai). A kunai: 9 strands spread 0.36 cells round it (0.6 as deep as
        /// wide, tallest in the middle) and three base tongues. The Fūma: its blade flames, <paramref name="turn"/> the
        /// turn it came to rest at. <paramref name="age"/>: seconds since it came down; <paramref name="litAge"/>:
        /// seconds since it caught fire, which keeps the flames' rhythm from the air (below 0 takes age).
        /// After it goes out the flames sink over 0.3 s and a small puff of smoke rises. The floor is
        /// <see cref="WeaponStain"/>. Call it until sinceOut reaches <see cref="AmaterasuTiming.OutDuration"/>.
        /// </summary>
        public static void OnFloor(bool fuma, Vector2 ground, float turn, float age, float sinceOut, int seed, float litAge = -1f,
            float scale = 1f, AmaterasuLight light = AmaterasuLight.Violet)
        {
            if (litAge < 0f) litAge = age;
            if (age < 0f || litAge <= sinceOut) return;
            seed = Seed(seed);
            Begin(ground);
            float rel = 1f - Smooth(sinceOut / T.Sink);
            if (rel > 0f)
            {
                rootCount = 0;
                if (fuma) FumaRoots(ground, turn, 0f, seed + 77, scale);
                else FloorRoots(ground, 9, T.KunaiPatch, seed + 30, scale);
                bool lit = LightOf(light, out Color lightColour);
                SmallFlames(litAge * T.Speed, 1f, rel, Vector2.zero, lit, lightColour, Overhead + 0.05f, (litAge - sinceOut) * T.Speed);
            }
            if (sinceOut < 0f || sinceOut >= T.SmokeLife || sinceOut > age) return;
            int floorSeed = seed + (fuma ? 90 : 50);
            float size = fuma ? T.FumaStain : T.KunaiStain;
            for (int i = 0; i < 3; i++)
            {
                int k = floorSeed + 700 + i * 5;
                float u = Mathf.Clamp01((sinceOut - i * 0.05f) / (T.SmokeLife - 0.2f));
                if (u <= 0f || u >= 1f) continue;
                float puff = (0.3f + u * 0.5f) * 1.3f;
                Sprite(At(ground, (Rand(k) - 0.5f) * size, 0f, 0.3f + u * (0.8f + Rand(k + 1))), puff, puff,
                    Fade(SmokeColour, 0.2f * Mathf.Sin(u * Mathf.PI)), Puff, Overhead + 0.12f + i * 0.0002f);
            }
        }

        /// <summary>
        /// The floor under a burning weapon lying there: a soft shadow 3 sizes across, a scorch (1.8) and a see-through
        /// pool (1.4), the size 0.45 for a kunai and 0.75 for the Fūma; in over 0.25 s. After it goes out the shadow thins
        /// to 0.3 and the pool to half; the scorch stays. Holds and fades as <see cref="PawnStain"/>. Call it from the
        /// moment it comes down until sinceOut reaches <see cref="AmaterasuTiming.StainDuration"/>.
        /// </summary>
        public static void WeaponStain(bool fuma, Vector2 ground, float age, float sinceOut, int seed)
        {
            float size = fuma ? T.FumaStain : T.KunaiStain;
            Stain(ground, ground, age, sinceOut, Seed(seed) + (fuma ? 90 : 50), size * 3f, 0.32f, size * 1.8f, size * 1.4f, 0.55f);
        }

        private static void Stain(Vector2 c, Vector2 pool, float age, float sinceOut, int seed, float shadowSize, float shadowAlpha,
            float scorchSize, float poolSize, float poolAlpha)
        {
            if (age < 0f) return;
            float fade = 1f - Smooth((sinceOut - T.StainHold) / T.StainFade);
            if (fade <= 0f) return;
            float stain = Smooth(age / T.StainIn) * fade, rel = 1f - Smooth(sinceOut / T.Sink);
            Sprite(c, shadowSize, shadowSize, Fade(Black, shadowAlpha * stain * (0.3f + 0.7f * rel)), soft, Floor + 0.016f);
            Sprite(c, scorchSize, scorchSize, Fade(ScorchColour, 0.6f * stain), Puff, Floor + 0.018f);
            Sprite(pool, poolSize, poolSize, Fade(Black, poolAlpha * stain * (0.5f + 0.5f * rel)), Blot, Floor + 0.02f, seed % 360);
        }

        /// <summary>
        /// A kunai or the Fūma lying on the floor, as lib/amenoyodomi.js draws a landed weapon: the kunai 0.62 cells at
        /// Filth + 0.06 pointing <paramref name="deg"/>, the Fūma 1.4 cells at Filth + 0.05 turned <paramref name="turn"/>.
        /// For the conjured kunai's patch, which has no item; <paramref name="material"/> null takes the plain kunai or
        /// the Fūma.
        /// </summary>
        public static void Lying(bool fuma, Vector2 ground, float deg, float turn, Material material = null)
        {
            if (fuma) Sprite(ground, AmenoyodomiGraphics.FumaSize, AmenoyodomiGraphics.FumaSize, Color.white, material ?? FumaLying, Floor + 0.05f, turn);
            else Sprite(ground, 0.62f, 0.62f, Color.white, material ?? KunaiLying, Floor + 0.06f, 90f - deg);
        }

        /// <summary>Blotches on the spots filled in, licking upward on their own cycles; the thing shows between them.</summary>
        private static void Cling(int count, float clock, float g, float rel, float layer)
        {
            for (int i = 0; i < count; i++)
            {
                Spot q = spots[i];
                float period = 0.7f + Rand(q.k + 2) * 0.5f, u = ((clock + Rand(q.k + 3) * period) / period) % 1f;
                float size = q.size * (0.8f + 0.4f * Rand(q.k + 4)) * (1f - 0.35f * u) * g * rel;
                if (size <= 0.01f) continue;
                Sprite(At(q.at, Mathf.Sin(clock * 3f + q.k) * 0.015f, u * 0.08f, q.h), size, size * 1.15f,
                    Fade(Black, Mathf.Min(1f, Mathf.Sin(u * Mathf.PI) * 2.2f) * 0.95f), Blot, layer + i * 0.0002f, Spin(q.k, clock));
            }
        }

        private static void AddRoot(Vector2 at, float h, int k, float full, float hw, bool vein, bool isBase)
        {
            int i = rootCount++;
            roots[i] = new Root { at = at, h = h, k = k, full = full, hw = hw, vein = vein, isBase = isBase };
            // Stable insertion by ground north, north first.
            int j = i;
            while (j > 0 && roots[rootOrder[j - 1]].at.y < at.y) { rootOrder[j] = rootOrder[j - 1]; j--; }
            rootOrder[j] = i;
        }

        /// <summary>
        /// Flames on the roots filled in, drawn north first: one strand per root, each on its own grow-tear-regrow cycle
        /// like a pawn's; about half the torn tops rise as flecks. Base roots are short broad tongues that never tear. A
        /// moving thing (<paramref name="lean"/> not zero) leans its flames back and tears none.
        /// </summary>
        private static void SmallFlames(float clock, float g, float rel, Vector2 lean, bool lit, Color light, float layer, float lastBirth)
        {
            bool moving = lean.x != 0f || lean.y != 0f;
            for (int i = 0; i < rootCount; i++)
            {
                Root r = roots[rootOrder[i]];
                float period = r.isBase ? 0.4f + Rand(r.k + 6) * 0.2f : 0.45f + Rand(r.k + 6) * 0.35f, offset = Rand(r.k + 7) * period;
                Cycle(clock, period, offset, out int n0, out float grown);
                Vector2 root = Up(r.at, r.h);
                bool drawn = Strand(root, r.full * grown * g * rel, r.hw * (0.5f + 0.5f * rel), clock, r.k, Mathf.Min(1f, g * 3f),
                    layer + i * 0.0004f, lean.x, 0.6f, lean.y);
                if (drawn && r.vein && lit) Veins(clock, r.k, g * rel, layer + i * 0.0004f + 0.0001f, light);
                if (moving || r.isBase) continue;
                for (int m = 0; m < 2; m++)
                {
                    int n = n0 - m, kk = r.k + n * 31;
                    float tear = (n + 0.8f) * period - offset, life = 0.45f + Rand(kk) * 0.25f, fu = (clock - tear) / life;
                    if (tear < 0f || tear > lastBirth || fu < 0f || fu >= 1f || Rand(kk + 9) > 0.5f) continue;
                    float fw = Mathf.Max(0.05f, r.hw * 1.8f) * (0.8f + 0.5f * Rand(kk + 3)) * (1f - 0.45f * fu);
                    Sprite(At(root, (Rand(kk + 1) - 0.5f) * 0.15f * fu + Mathf.Sin(fu * 5f + kk) * 0.03f, 0f, r.full * g * 0.85f + fu * (0.3f + Rand(kk + 2) * 0.35f)),
                        fw, fw * (1.5f + 0.7f * Rand(kk + 4)) * (1f - 0.2f * fu), Fade(Black, (1f - Smooth((fu - 0.45f) / 0.55f)) * Mathf.Min(1f, T.Flecks)),
                        Shred, Overhead + 0.128f + (i % 20) * 0.0002f, (Rand(kk + 5) - 0.5f) * 70f + Mathf.Sin(fu * 4f + kk) * 25f);
                }
            }
        }

        /// <summary>
        /// Roots for a burning kunai on the floor: <paramref name="count"/> strands spread round c on a golden-angle
        /// spiral, 0.6 as deep as wide so the patch reads as flat, tallest in the middle, and three base tongues.
        /// </summary>
        private static void FloorRoots(Vector2 c, int count, float spread, int seed, float scale)
        {
            for (int j = 0; j < count; j++)
            {
                int k = seed + j * 23;
                float a = j * 2.399f + Rand(k) * 0.6f, r = Mathf.Sqrt((j + 0.5f) / count) * spread;
                AddRoot(new Vector2(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r * 0.6f), 0f, k,
                    (0.5f + 0.5f * (1f - r / spread)) * (0.75f + 0.35f * Rand(k + 3)) * scale, 0.04f + 0.035f * Rand(k + 5), j % 3 == 0, false);
            }
            AddRoot(new Vector2(c.x - 0.5f * spread, c.y - 0.04f), 0f, seed + 500, 0.28f * scale, 0.08f, false, true);
            AddRoot(new Vector2(c.x + 0.1f * spread, c.y), 0f, seed + 507, 0.28f * scale, 0.08f, false, true);
            AddRoot(new Vector2(c.x + 0.55f * spread, c.y + 0.04f), 0f, seed + 514, 0.28f * scale, 0.08f, false, true);
        }

        /// <summary>
        /// Blade k of the Fūma at r texture widths from its middle, turned clockwise by turn degrees, as a ground point.
        /// Measured off RimArt/Fuma/Unfolded: the blades curl, 22 + 40 r + 90 k degrees.
        /// </summary>
        private static Vector2 Blade(Vector2 c, float turn, int k, float r)
        {
            float a = (22f + 40f * r + 90f * k - turn) * Mathf.Deg2Rad;
            return new Vector2(c.x + Mathf.Cos(a) * r * AmenoyodomiGraphics.FumaSize, c.y + Mathf.Sin(a) * r * AmenoyodomiGraphics.FumaSize);
        }

        /// <summary>
        /// Four strands along each blade, tallest mid-blade, a short tongue at its tip and a broad base tongue that joins
        /// them; the second strand carries the light. The flames rise well above the blades, because black on the dark
        /// blades does not read.
        /// </summary>
        private static void FumaRoots(Vector2 c, float turn, float h, int seed, float scale)
        {
            for (int k = 0; k < 4; k++)
            {
                for (int j = 0; j < BladeRootsAt.Length; j++)
                {
                    int kk = seed + k * 41 + j * 7;
                    bool tip = j == 4;
                    AddRoot(Blade(c, turn, k, BladeRootsAt[j]), h, kk, (tip ? 0.35f : BladeFull[j] * (0.8f + 0.35f * Rand(kk + 3))) * scale,
                        tip ? 0.035f : 0.045f + 0.03f * Rand(kk + 5), j == 1, false);
                }
                AddRoot(Blade(c, turn, k, 0.32f), h, seed + 300 + k * 13, 0.35f * scale, 0.085f, false, true);
            }
        }

        /// <summary>
        /// In flight the fire streams back off the whole Fūma like a comet: ten tongues round the rim (0.42 of its size
        /// out), bent back along the path, longest at the back, and three base tongues over the hub. They do not turn
        /// with the blades.
        /// </summary>
        private static void FumaTrailRoots(Vector2 c, Vector2 d, float h, int seed, float scale)
        {
            float back = Mathf.Atan2(-d.y, -d.x);
            for (int i = 0; i < 10; i++)
            {
                int k = seed + 500 + i * 11;
                float a = i * Mathf.PI / 5f + 0.2f, rear = (1f + Mathf.Cos(a - back)) / 2f;
                AddRoot(new Vector2(c.x + Mathf.Cos(a) * 0.42f * AmenoyodomiGraphics.FumaSize, c.y + Mathf.Sin(a) * 0.42f * AmenoyodomiGraphics.FumaSize), h, k,
                    (0.4f + 0.8f * rear) * (0.8f + 0.3f * Rand(k + 3)) * scale, 0.07f + 0.04f * Rand(k + 5), i % 2 == 0, false);
            }
            for (int j = 0; j < 3; j++)
            {
                float u = (j - 1) * 0.18f;
                AddRoot(new Vector2(c.x + d.y * u, c.y - d.x * u), h, seed + 600 + j * 7, 0.4f * scale, 0.1f, false, true);
            }
        }

        // ---- Small helpers --------------------------------------------------------------------------------------

        /// <summary>The lab's at(o, x, z, h): x east, z north, h cells up drawn north by Lift.</summary>
        private static Vector2 At(Vector2 o, float x, float z, float h = 0f) => new Vector2(o.x + x, o.y + z + h * Lift);

        private static Vector2 Up(Vector2 ground, float h) => AmenoyodomiGraphics.Up(ground, h);

        private static float Cube(float v) => v * v * v;

        /// <summary>A blotch's turn: (k x 47 + clock x 40) mod 360, with k x 47 reduced first so float keeps its precision.</summary>
        private static float Spin(int k, float clock) => ((k * 47) % 360 + clock * 40f) % 360f;

        /// <summary>
        /// Seeds are kept under 8192 so the sine arguments that add them (sin(clock x 3 + k)) keep float precision; the
        /// sketch's seeds (100 to 3100) pass unchanged. A thingIDNumber is fine to pass.
        /// </summary>
        private static int Seed(int seed) => ((seed % 8192) + 8192) % 8192;

        private static bool LightOf(AmaterasuLight light, out Color colour)
        {
            colour = light == AmaterasuLight.Crimson ? CrimsonLight : VioletLight;
            return light != AmaterasuLight.None;
        }
    }
}
