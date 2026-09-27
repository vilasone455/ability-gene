using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.SusanooDraw;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>The sketch's Shape settings, baked: kx and kz scale the 3.5 x 3.2 design.</summary>
    public struct SusanooLook
    {
        public float kx, kz, fill, line, flameH, bladeLen, aura;

        public static SusanooLook Default => new SusanooLook { kx = 1f, kz = 1f, fill = .6f, line = 1f, flameH = .45f, bladeLen = 1.9f, aura = 1f };
    }

    /// <summary>
    /// How far each part has come (0..1): ribs rise, then the skull and the skeletal right arm,
    /// then armour, cape and face, the gourd pours the blade, the mirror's ring draws round, the
    /// eyes light last. fade* take parts away again when it breaks; dim darkens it before that.
    /// </summary>
    public struct SusanooGrowth
    {
        public bool started;
        public float pool, glint, ribs, skel, oneEye, armour, blade, mirror, eyes, flash, alpha, dim;
        public float fadeHead, fadeArms, fadeBody, fadeBase;

        public static SusanooGrowth Complete => new SusanooGrowth
        {
            started = true, pool = 1f, ribs = 1f, skel = 1f, oneEye = 1f, armour = 1f, blade = 1f, mirror = 1f, eyes = 1f, alpha = 1f,
            fadeHead = 1f, fadeArms = 1f, fadeBody = 1f, fadeBase = 1f,
        };
    }

    public struct SusanooMirrorHit
    {
        /// <summary>Seconds since the hit, and where it landed as (u, v) on the unit face.</summary>
        public float age, u, v;
    }

    /// <summary>Where the arms are this frame. Hand and mirror in design units; bladeTip in map cells when the blade reaches a target.</summary>
    public sealed class SusanooPose
    {
        public Vector2 hand = SusanooGraphics.RestHand;
        public float elbowDown = 1f;
        public float bladeDeg = SusanooGraphics.RestBladeDeg;
        public bool hasBladeTip;
        public Vector2 bladeTip;
        public Vector2 mirror = SusanooGraphics.RestMirror;
        /// <summary>The blade's length now, or -1 for the look's idle length.</summary>
        public float bladeLen = -1f;
        public float stab;
        public float mirrorFlash;
        public readonly List<SusanooMirrorHit> hits = new List<SusanooMirrorHit>();

        public void Reset()
        {
            hand = SusanooGraphics.RestHand;
            elbowDown = 1f;
            bladeDeg = SusanooGraphics.RestBladeDeg;
            hasBladeTip = false;
            mirror = SusanooGraphics.RestMirror;
            bladeLen = -1f;
            stab = 0f;
            mirrorFlash = 0f;
            hits.Clear();
        }
    }

    /// <summary>
    /// The Susanoo of lib/itachi.js: Itachi's complete armoured form, drawn upright in the screen
    /// plane and always from the front, about 3.5 cells tall. Everything that covers Itachi is
    /// drawn at Back (under the pawn), the rest at Front and above; fills are see-through, lines,
    /// flames, eyes and the blade additive. One call per frame per Susanoo.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class SusanooGraphics
    {
        internal const float DesignH = 3.5f, DesignW = 3.2f;
        /// <summary>The real-size pawn's feet, from the cell centre.</summary>
        internal const float FeetZ = -.33f;
        internal static readonly Vector2 ArmRoot = new Vector2(1.0f, 2.02f);
        internal const float Upper = .78f, Fore = .72f;
        internal static readonly Vector2 RestHand = new Vector2(-1.63f, 2.13f), RestMirror = new Vector2(1.52f, 1.26f);
        internal const float MirrorR = .70f;
        internal static readonly Vector2 GuardC = new Vector2(0f, 1.2f);
        internal const float GuardR = 1.45f, MaxStretch = 1.25f, RestBladeDeg = 100f;

        // Ribs: centre v, half width, how far the front end drops toward the sternum.
        private static readonly float[][] RibSet =
        {
            new[] { 1.78f, .46f, .20f }, new[] { 1.58f, .60f, .24f }, new[] { 1.38f, .70f, .28f }, new[] { 1.17f, .76f, .32f },
            new[] { .96f, .78f, .34f }, new[] { .75f, .74f, .34f }, new[] { .55f, .66f, .32f },
        };
        private const float RibB = .10f, SternumFoot = 1.06f, NeckTop = 2.32f, RibTop = 1.9f;

        private static readonly Vector2[][] FlameLines =
        {
            Curl(new[] { new[] { .72f, .55f }, new[] { .62f, 1.0f }, new[] { .70f, 1.45f }, new[] { .52f, 1.78f }, new[] { .34f, 1.74f }, new[] { .40f, 1.6f } }),
            Curl(new[] { new[] { .95f, 1.25f }, new[] { .86f, 1.7f }, new[] { .92f, 2.05f }, new[] { .72f, 2.22f }, new[] { .62f, 2.08f } }),
        };
        private static readonly Vector2[] TorsoEdge = SmoothEdge(new[]
        {
            new[] { 0f, .78f }, new[] { .2f, .74f }, new[] { .45f, .74f }, new[] { .9f, .88f }, new[] { 1.5f, 1.02f }, new[] { 1.95f, 1.12f },
            new[] { 2.15f, 1.1f }, new[] { 2.32f, .96f }, new[] { 2.45f, .72f }, new[] { 2.52f, .5f },
        });
        private static readonly Vector2[] CapeEdge = SmoothEdge(new[]
            { new[] { .15f, 1.40f }, new[] { .6f, 1.50f }, new[] { 1.2f, 1.46f }, new[] { 1.9f, 1.36f }, new[] { 2.2f, 1.22f }, new[] { 2.32f, 1.0f } });
        private static readonly Vector2[] ChestEdge = SmoothEdge(new[]
            { new[] { 1.38f, .62f }, new[] { 1.6f, .80f }, new[] { 1.9f, .90f }, new[] { 2.15f, .86f }, new[] { 2.32f, .66f }, new[] { 2.42f, .48f } });
        private static readonly Vector2[] HoodEdge = SmoothEdge(new[]
            { new[] { 2.2f, .66f }, new[] { 2.45f, .66f }, new[] { 2.75f, .62f }, new[] { 3.0f, .56f }, new[] { 3.22f, .44f }, new[] { 3.4f, .26f }, new[] { 3.5f, .06f } });
        private static readonly Vector2 FaceC = new Vector2(-.05f, 2.76f), FaceR = new Vector2(.36f, .44f);
        // The flame edge over the head and shoulders, left to right, so its left normal points out.
        private static readonly Vector2[] TopPath =
        {
            new Vector2(-1.62f, 1.78f), new Vector2(-1.66f, 2.05f), new Vector2(-1.5f, 2.34f), new Vector2(-1.1f, 2.46f), new Vector2(-.75f, 2.5f),
            new Vector2(-.6f, 2.72f), new Vector2(-.5f, 2.95f), new Vector2(-.4f, 3.16f), new Vector2(-.24f, 3.3f), new Vector2(0f, 3.37f),
            new Vector2(.24f, 3.3f), new Vector2(.4f, 3.16f), new Vector2(.5f, 2.95f), new Vector2(.6f, 2.72f), new Vector2(.75f, 2.5f),
            new Vector2(1.1f, 2.46f), new Vector2(1.5f, 2.34f), new Vector2(1.66f, 2.05f), new Vector2(1.62f, 1.78f),
        };
        private static readonly Vector2[] CapeLeftPath = { new Vector2(-1.42f, .3f), new Vector2(-1.5f, .7f), new Vector2(-1.47f, 1.2f), new Vector2(-1.40f, 1.8f) };
        private static readonly Vector2[] CapeRightPath = { new Vector2(1.40f, 1.8f), new Vector2(1.47f, 1.2f), new Vector2(1.5f, .7f), new Vector2(1.42f, .3f) };
        private static readonly Vector2[] BasePath = MakeBasePath();
        private static readonly Vector2[][] Strands =
        {
            new[] { new Vector2(-.34f, 3.36f), new Vector2(-.5f, 3.1f), new Vector2(-.58f, 2.75f), new Vector2(-.5f, 2.35f) },
            new[] { new Vector2(-.12f, 3.44f), new Vector2(-.36f, 3.25f), new Vector2(-.46f, 2.95f), new Vector2(-.44f, 2.6f) },
            new[] { new Vector2(.3f, 3.38f), new Vector2(.46f, 3.15f), new Vector2(.52f, 2.9f) },
            new[] { new Vector2(.52f, 2.6f), new Vector2(.5f, 2.38f), new Vector2(.38f, 2.25f) },
        };
        private static readonly float[][] GourdProfile =
        {
            new[] { -.2f, 0f }, new[] { -.18f, .08f }, new[] { -.12f, .13f }, new[] { 0f, .14f }, new[] { .08f, .1f }, new[] { .12f, .07f },
            new[] { .17f, .09f }, new[] { .22f, .09f }, new[] { .27f, .06f }, new[] { .31f, .045f }, new[] { .34f, .05f },
        };

        private static Vector2[] MakeBasePath()
        {
            var o = new Vector2[15];
            for (int i = 0; i < 15; i++)
            {
                float u = -1.3f + i / 14f * 2.6f;
                o[i] = new Vector2(u, .08f - .26f * (1f - (u / 1.3f) * (u / 1.3f)));
            }
            return o;
        }

        /// <summary>The sketch's curl(): a Catmull-Rom smoothing of (u, v) points, done on (v, u).</summary>
        private static Vector2[] Curl(float[][] pts, int n = 18)
        {
            var swapped = new float[pts.Length][];
            for (int i = 0; i < pts.Length; i++) swapped[i] = new[] { pts[i][1], pts[i][0] };
            Vector2[] e = SmoothEdge(swapped, Mathf.CeilToInt(n / (float)(pts.Length - 1)));
            var o = new Vector2[e.Length];
            for (int i = 0; i < e.Length; i++) o[i] = new Vector2(e[i].y, e[i].x);
            return o;
        }

        // ---- growth and breaking

        public static SusanooGrowth Growth(float s, float warmUp)
        {
            float g = s / warmUp;
            return new SusanooGrowth
            {
                started = g >= 0f,
                pool = Smooth(g / .15f),
                glint = g < 0f ? 0f : Mathf.Max(0f, 1f - Mathf.Abs(g - .05f) / .08f),
                ribs = Clamp((g - .10f) / .30f),
                skel = Clamp((g - .35f) / .25f),
                oneEye = Clamp((g - .52f) / .08f),
                armour = Clamp((g - .55f) / .45f),
                blade = Clamp((g - .66f) / .30f),
                mirror = Clamp((g - .70f) / .30f),
                eyes = Clamp((g - .93f) / .07f),
                flash = g < .93f ? 0f : Mathf.Max(0f, 1f - (g - .93f) / .35f),
                alpha = 1f, fadeHead = 1f, fadeArms = 1f, fadeBody = 1f, fadeBase = 1f,
            };
        }

        /// <summary>The end: parts go top first as windows of D; the bones show as the armour leaves, then sink.</summary>
        public static SusanooGrowth Breaking(float tb, float D, float dim)
        {
            float u = tb / D;
            float Out(float a, float b) => 1f - Smooth((u - a) / (b - a));
            SusanooGrowth g = SusanooGrowth.Complete;
            g.dim = dim;
            g.fadeHead = Out(0f, .35f);
            g.fadeArms = Out(.15f, .5f);
            g.fadeBody = Out(.3f, .7f);
            g.fadeBase = Out(.7f, 1f);
            g.skel = Out(.55f, .8f);
            g.ribs = Out(.6f, 1f);
            g.pool = Out(.7f, 1f);
            g.eyes = Out(0f, .3f);
            return g;
        }

        // ---- geometry

        public static float BobAt(float s) => .018f * Mathf.Sin(s * 2.2f);

        /// <summary>Where the mirror's centre goes (design units) to guard toward screen angle deg.</summary>
        public static Vector2 MirrorAt(float deg)
        {
            float a = deg * Mathf.Deg2Rad;
            var want = new Vector2(GuardC.x + Mathf.Cos(a) * GuardR, GuardC.y + Mathf.Sin(a) * GuardR);
            float dx = want.x - ArmRoot.x, dv = want.y - ArmRoot.y, d = Mathf.Sqrt(dx * dx + dv * dv), reach = (Upper + Fore) * MaxStretch - .01f;
            return d <= reach ? want : new Vector2(ArmRoot.x + dx / d * reach, ArmRoot.y + dv / d * reach);
        }

        /// <summary>The mirror's centre and radii in map cells for a pose at time s.</summary>
        public static void MirrorWorld(in SusanooFrame F, float s, Vector2 mirror, out Vector2 centre, out float rx, out float rz)
        {
            centre = F.At(mirror.x, mirror.y + BobAt(s));
            rx = MirrorR * F.kx;
            rz = MirrorR * F.kz;
        }

        /// <summary>The point on the mirror's rim facing <paramref name="from"/>, and that point as (u, v) on the unit face.</summary>
        public static Vector2 RimToward(Vector2 centre, float rx, float rz, Vector2 from, out float u, out float v)
        {
            float ang = Mathf.Atan2((from.y - centre.y) / rz, (from.x - centre.x) / rx);
            u = Mathf.Cos(ang) * .9f;
            v = Mathf.Sin(ang) * .9f;
            return new Vector2(centre.x + Mathf.Cos(ang) * rx * .96f, centre.y + Mathf.Sin(ang) * rz * .96f);
        }

        /// <summary>The Totsuka's gourd mouth in map cells for a pose at time s (where the blade leaves the gourd).</summary>
        public static Vector2 GourdMouth(in SusanooFrame F, float s, SusanooPose pose)
        {
            float b = BobAt(s);
            var root = new Vector2(-ArmRoot.x, ArmRoot.y + b);
            ElbowFor(root, new Vector2(pose.hand.x, pose.hand.y + b), -1, pose.elbowDown, Upper, Fore, out _, out Vector2 hand);
            float a = pose.bladeDeg * Mathf.Deg2Rad;
            return F.At(hand.x + Mathf.Cos(a) * .33f, hand.y + Mathf.Sin(a) * .33f);
        }

        /// <summary>The right hand's reach toward a map point: reach design units from the shoulder toward it, as seen on screen.</summary>
        public static Vector2 HandToward(in SusanooFrame F, Vector2 target, float reach = 1.42f)
        {
            Vector2 root = F.At(-ArmRoot.x, ArmRoot.y);
            float dx = (target.x - root.x) / F.kx, dv = (target.y - root.y) / F.kz, d = Mathf.Sqrt(dx * dx + dv * dv);
            if (d < 1e-6f) d = 1f;
            return new Vector2(-ArmRoot.x + dx / d * reach, ArmRoot.y + dv / d * reach);
        }

        // ---- the figure

        private struct Tone
        {
            public Color line, bone, lit, deep;
            public float fillA, lineA, aK, bob, kx, kz, alpha;
        }

        public static void Susanoo(in SusanooFrame F, float s, in SusanooLook look, in SusanooGrowth g, SusanooPose pose)
        {
            float kx = F.kx, kz = F.kz, alpha = g.alpha;
            if (!g.started || alpha <= .002f) return;
            float bob = BobAt(s);
            float warmK = 1f - g.armour;
            Color deep = Color.Lerp(Deep, Warm, warmK), lit = Color.Lerp(Lit, WarmLit, warmK);
            Color line = Color.Lerp(Line, LineWarm, warmK), bone = Color.Lerp(Bone, BoneWarm, warmK);
            float dim = g.dim;
            Color deepD = Dimmed(deep, dim), litD = Dimmed(lit, dim), lineD = Color.Lerp(line, Deep, dim * .35f);
            float flameH = look.flameH * (1f - .55f * dim);
            float fillA = look.fill * alpha, lineA = look.line * alpha * (1f - .25f * dim);
            float aK = Smooth(g.armour);
            float bodyFill = fillA * g.fadeBody, bodyLine = lineA * g.fadeBody;

            // Floor: the warm light pool, and a glow round the whole figure.
            Sprite(F.At(0f, .05f), 3.9f * kx, 2.2f * kz, A(FlameDeep, .20f * g.pool * alpha), glow, Floor + .01f);
            Sprite(F.At(0f, .05f), 2.2f * kx, 1.2f * kz, A(FlameMid, .16f * g.pool * alpha), glow, Floor + .011f);
            float halo = (.07f + .12f * g.flash) * alpha * g.fadeBody * (1f - .5f * dim);
            Sprite(F.At(0f, 1.4f), 4.2f * kx, 4.0f * kz, A(FlameDeep, halo * Mathf.Max(g.ribs * .6f, g.armour)), glow, Back - .02f);
            Sprite(F.At(0f, 2.8f), 2.2f * kx, 1.8f * kz, A(FlameDeep, halo * g.armour), glow, Back - .019f);

            // ---- behind Itachi
            Aura(F, s, Mathf.Max(g.ribs * .5f, aK) * g.fadeBody * (1f - .5f * dim) * alpha, Back - .03f,
                Smooth(g.ribs * .45f + g.skel * .25f + aK * .3f), warmK, look.aura);
            // Cape: grows out from the shoulders down.
            if (aK > 0f)
            {
                Vector2[] cape = Pts(SEdgeT, CapeEdge.Length);
                for (int i = 0; i < cape.Length; i++) cape[i] = new Vector2(Lerp(2.2f, CapeEdge[i].x, aK), Lerp(.9f, CapeEdge[i].y, aK));
                for (int i = 0; i < 2; i++)
                {
                    float v0 = i == 0 ? .15f : .6f, v1 = i == 0 ? .6f : 2.32f, f = i == 0 ? .35f : 1f;
                    Vector2[] seg = EdgeBetween(cape, cape.Length, v0, v1, out int n);
                    Pair(seg, n, F, 0f, out Vector2[] cl, out Vector2[] cr);
                    FillStrip(cl, cr, Dimmed(CapeRed, dim), .6f * bodyFill * aK * f, Back);
                }
                float aKc = aK;
                Vector2[] left = Pts(SLine, 4), right = Pts(SLine2, 4);
                for (int i = 0; i < 4; i++)
                {
                    left[i] = F.At(CapeLeftPath[i].x, CapeLeftPath[i].y);
                    right[i] = F.At(CapeRightPath[i].x, CapeRightPath[i].y);
                }
                FlameEdge(left, 4, s, flameH * .55f, alpha * aK * g.fadeBody, 10, 3, Back + .001f, 1.4f, t => Clamp(aKc * 1.6f - (1f - t)));
                FlameEdge(right, 4, s, flameH * .55f, alpha * aK * g.fadeBody, 10, 4, Back + .001f, 1.4f, t => Clamp(aKc * 1.6f - t));
            }
            // Spine: rises out of the floor to the top of the ribcage with the ribs, then on to the
            // neck with the skull. Each rib unrolls round from the spine once the spine top has passed
            // it, lowest first. Once the armour is on, the bones dim to what shows through the body.
            float spineTop = Mathf.Max(RibTop * Smooth(g.ribs), Lerp(RibTop, NeckTop, Smooth(g.skel)) * (g.skel > 0f ? 1f : 0f));
            float boneA = Lerp(1f, .15f, aK * g.fadeBody) * alpha;
            if (g.ribs > 0f)
            {
                Vector2[] sp = Pts(SLine, 2);
                sp[0] = F.At(0f, 0f);
                sp[1] = F.At(0f, spineTop + bob);
                TaperLine(sp, 2, .10f * kx, .06f * kx, bone, .6f * boneA, Back + .003f);
                for (int k = 0; .12f + k * .15f < spineTop; k++)
                    Blob(F.At(0f, .12f + k * .15f + bob), .065f * kx, .04f * kz, A(bone, .45f * boneA), Back + .004f, 0f, add);
            }
            var ribGrow = new float[7];
            for (int i = 0; i < 7; i++) ribGrow[i] = Clamp((spineTop - RibSet[i][0]) / .4f);
            // Back halves: thin and dim, seen through the body.
            for (int i = 0; i < 7; i++)
            {
                float r = ribGrow[i];
                if (r <= 0f) continue;
                float vc = RibSet[i][0], w = RibSet[i][1];
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2[] back = Pts(SLine, 11);
                    for (int j = 0; j <= 10; j++)
                    {
                        float a = j / 10f * Mathf.PI / 2f;
                        back[j] = F.At(side * Mathf.Sin(a) * w, vc + bob + Mathf.Cos(a) * RibB);
                    }
                    TaperLine(back, 11, .045f * kx, .035f * kx, bone, .35f * boneA, Back + .005f, Clamp(r * 2f));
                }
            }
            // Torso: fills in from the ribs outward; its foot fades into the floor in two steps.
            if (aK > 0f)
            {
                Vector2[] torso = Pts(SEdgeT, TorsoEdge.Length);
                for (int i = 0; i < torso.Length; i++) torso[i] = new Vector2(TorsoEdge[i].x, Lerp(.55f, TorsoEdge[i].y, aK));
                for (int i = 0; i < 3; i++)
                {
                    float v0 = i == 0 ? 0f : i == 1 ? .22f : .5f, v1 = i == 0 ? .22f : i == 1 ? .5f : 2.5f, f = i == 0 ? .25f : i == 1 ? .6f : 1f;
                    Vector2[] seg = EdgeBetween(torso, torso.Length, v0, v1, out int n);
                    Pair(seg, n, F, bob, out Vector2[] tl, out Vector2[] tr);
                    FillStrip(tl, tr, deepD, bodyFill * aK * f, Back + .006f);
                }
                Vector2[] chest = Pts(SEdgeT, ChestEdge.Length);
                for (int i = 0; i < chest.Length; i++) chest[i] = new Vector2(ChestEdge[i].x, Lerp(.3f, ChestEdge[i].y, aK));
                Pair(chest, chest.Length, F, bob, out Vector2[] pl, out Vector2[] pr);
                FillStrip(pl, pr, litD, .45f * bodyFill * aK, Back + .007f);
                // Darker blotches drifting slowly in the fill, the mottled flame look of the anime's fill.
                for (int i = 0; i < 8; i++)
                {
                    float u0 = (Rand(i + 360) - .5f) * 1.3f, rise = Mathf.Repeat(s * (.06f + .04f * Rand(i + 370)) + Rand(i + 380), 1f);
                    float v = .5f + rise * 1.7f, fade = Mathf.Sin(rise * Mathf.PI) * (.7f + .3f * Mathf.Sin(s * 1.7f + i * 2f));
                    Sprite(F.At(u0, v), (.45f + .3f * Rand(i + 390)) * kx, (.35f + .25f * Rand(i + 400)) * kz,
                        A(C(.36f, .05f, .04f), .28f * fade * aK * alpha * g.fadeBody), soft, Back + .0071f, Rand(i + 410) * 180f);
                }
                // Moving light inside the fill: soft blobs drifting up, as the anime's fill churns.
                for (int i = 0; i < 9; i++)
                {
                    float u0 = (Rand(i + 300) - .5f) * 1.5f, rise = Mathf.Repeat(s * (.22f + .1f * Rand(i + 310)) + Rand(i + 320), 1f);
                    float v = .2f + rise * 2.0f, fade = Mathf.Sin(rise * Mathf.PI);
                    Sprite(F.At(u0 + .08f * Mathf.Sin(s * 1.3f + i), v), (.55f + .3f * Rand(i + 330)) * kx, (.45f + .3f * Rand(i + 340)) * kz,
                        A(FlameMid, .07f * fade * aK * alpha * g.fadeBody * (1f - dim)), glow, Back + .0075f);
                }
            }
            // Base flames rising out of the floor along the front of the footprint.
            float baseH = flameH * .7f * (.35f + .65f * Mathf.Max(g.ribs * .5f, aK));
            Vector2[] basePts = Pts(SLine, BasePath.Length);
            float baseScale = Lerp(.6f, 1f, aK);
            for (int i = 0; i < basePts.Length; i++) basePts[i] = F.At(BasePath[i].x * baseScale, BasePath[i].y);
            FlameEdge(basePts, basePts.Length, s, baseH, alpha * Mathf.Max(.6f * g.pool, aK) * g.fadeBase, 18, 5, Back + .008f, 2f);

            // ---- in front of Itachi
            // alpha faint red wash over Itachi, so he reads as inside.
            Sprite(F.At(0f, .45f), .95f, 1.25f, A(Deep, .20f * Mathf.Max(g.ribs, aK) * alpha * Mathf.Max(g.fadeBody, .4f * g.ribs)), soft, Wash);
            // Front halves of the ribs: from the side they drop in a curve toward the sternum, lower
            // ribs more; the upper five reach the sternum, the lower ones turn up to its foot.
            for (int i = 0; i < 7; i++)
            {
                float r = ribGrow[i];
                if (r <= 0f) continue;
                float vc = RibSet[i][0], w = RibSet[i][1], drop = RibSet[i][2], gap = i < 5 ? .07f : .2f;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2[] front = Pts(SLine, 13);
                    for (int j = 0; j <= 12; j++)
                    {
                        float f = j / 12f;
                        front[j] = F.At(side * Lerp(w, gap, f * f * (3f - 2f * f) * .9f + f * .1f), vc + bob - drop * Mathf.Sin(f * Mathf.PI / 2f) * (1f - .25f * f));
                    }
                    TaperLine(front, 13, .08f * kx, .035f * kx, bone, .8f * boneA, Front + .002f, Clamp(r * 2f - 1f));
                }
            }
            float archK = Clamp(ribGrow[6] * 2f - 1f);
            if (archK > 0f)
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2[] pts = Pts(SLine, 3);
                    pts[0] = F.At(side * .04f, SternumFoot + bob);
                    pts[1] = F.At(side * .22f, .78f + bob);
                    pts[2] = F.At(side * .38f, .58f + bob);
                    TaperLine(pts, 3, .04f * kx, .05f * kx, bone, .6f * boneA, Front + .002f, archK);
                }
            float sternK = Clamp(ribGrow[0] * 2f - 1f);
            if (sternK > 0f)
            {
                Vector2[] pts = Pts(SLine, 2);
                pts[0] = F.At(0f, RibSet[0][0] - RibSet[0][2] + .02f + bob);
                pts[1] = F.At(0f, SternumFoot + bob);
                TaperLine(pts, 2, .12f * kx, .06f * kx, bone, .7f * boneA, Front + .0021f, sternK);
            }
            // Collarbones from the sternum's top out to the shoulders, with the skeletal arm.
            float clavK = Clamp(g.skel * 3f);
            if (clavK > 0f)
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2[] pts = Pts(SLine, 4);
                    pts[0] = F.At(side * .06f, RibTop - .06f + bob);
                    pts[1] = F.At(side * .45f, RibTop + .02f + bob);
                    pts[2] = F.At(side * .8f, RibTop + .12f + bob);
                    pts[3] = F.At(side * ArmRoot.x, ArmRoot.y + bob);
                    TaperLine(pts, 4, .07f * kx, .06f * kx, bone, .8f * boneA, Front + .0022f, clavK);
                    Blob(F.At(side * ArmRoot.x, ArmRoot.y + bob), .09f * kx * clavK, .08f * kz * clavK, A(bone, .5f * boneA), Front + .0023f, 0f, add);
                }

            // Torso contour (from the waist up) and chest lines, drawn on.
            if (aK > 0f)
            {
                Vector2[] seg = EdgeBetween(TorsoEdge, TorsoEdge.Length, .45f, 2.5f, out int n);
                Pair(seg, n, F, bob, out Vector2[] tl, out Vector2[] tr);
                GlowLine(tl, n, .022f * kx, lineD, .8f * bodyLine, Front + .004f, aK);
                GlowLine(tr, n, .022f * kx, lineD, .8f * bodyLine, Front + .004f, aK);
                // Flowing flame lines up the body, curling in toward the chest.
                for (int i = 0; i < FlameLines.Length; i++)
                {
                    Vector2[] src = FlameLines[i];
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector2[] pts = Pts(SLine, src.Length);
                        for (int j = 0; j < src.Length; j++) pts[j] = F.At(side * src[j].x, src[j].y + bob);
                        GlowLine(pts, src.Length, .02f * kx, lineD, .55f * bodyLine, Front + .004f, Clamp(aK * 1.5f - .3f - i * .1f), 1f);
                    }
                }
                Sprite(F.At(0f, 1.95f + bob), .7f * kx, .55f * kz, A(lineD, .5f * bodyLine * Clamp(aK * 2f - 1f)), swirlMat, Front + .005f, 0f);
            }

            // Shoulder plates, drawn over the upper arms; the fist and the mirror still draw over them.
            float ap = Smooth(Clamp(aK * 1.3f));
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 c = F.At(side * 1.22f, 2.08f + bob);
                float sc = Lerp(.5f, 1f, ap);
                FillBlob(F.At(side * 1.34f, 1.78f + bob), .42f * kx * sc, .2f * kz * sc, litD, .5f * bodyFill * ap, Front + .0236f, side * 12f);
                FillBlob(c, .5f * kx * sc, .36f * kz * sc, litD, .62f * bodyFill * ap, Front + .0237f, side * 10f);
                DrawRing(c, .5f * kx * sc, .36f * kz * sc, side * 10f, A(lineD, .5f * bodyLine * ap), add, Front + .0238f);
                Sprite(c, .62f * kx * sc, .5f * kz * sc, A(lineD, .75f * bodyLine * ap), swirlMat, Front + .0239f, side > 0 ? 0f : 180f);
            }

            var tone = new Tone { line = lineD, bone = bone, lit = litD, deep = deepD, fillA = fillA * g.fadeHead, lineA = lineA * g.fadeHead, aK = aK, bob = bob, kx = kx, kz = kz, alpha = alpha * g.fadeHead };
            Head(F, s, g, tone);

            // The flame edge over the head and shoulders, lighting from the shoulders up.
            if (aK > 0f)
            {
                Vector2[] top = Pts(SLine, TopPath.Length);
                for (int i = 0; i < top.Length; i++) top[i] = F.At(TopPath[i].x, TopPath[i].y + bob);
                float aKt = aK;
                FlameEdge(top, top.Length, s, flameH, alpha * Clamp(aK * 2f) * g.fadeHead, 40, 1, Front + .0005f, .7f,
                    t => Clamp((aKt * 1.6f - (1f - Mathf.Abs(2f * t - 1f))) / .5f) * (1f + .7f * (1f - Mathf.Abs(2f * t - 1f))));
            }

            // ---- arms
            var armTone = new Tone { line = lineD, bone = bone, lit = litD, deep = deepD, fillA = fillA * g.fadeArms, lineA = lineA * g.fadeArms, aK = aK, bob = bob, kx = kx, kz = kz, alpha = alpha };
            Arms(F, s, look, g, alpha * g.fadeArms, pose, armTone, aK * g.fadeBody);
        }

        private static Color Dimmed(Color c, float dim) => Color.Lerp(c, new Color(c.r * .62f, c.g * .5f, c.b * .5f, 1f), dim);

        // The head. Skeletal stage: a skull with one lit eye. Then the hood wrapping a tengu mask:
        // a glowing crystal on the forehead, one small dark eye, a long curved spike nose pointing
        // up and left, an ear with a ring on the right, and the mouth as a dark slot holding the
        // Susanoo's own yellow eyes, with red teeth outlined in pale line.
        private static void Head(in SusanooFrame F, float s, in SusanooGrowth g, in Tone c)
        {
            float kx = c.kx, kz = c.kz, bob = c.bob, alpha = c.alpha, aK = c.aK, fillA = c.fillA, lineA = c.lineA;
            Color line = c.line, bone = c.bone, lit = c.lit, deep = c.deep;
            float headK = Smooth(Clamp((aK - .2f) / .8f));
            float skullA = g.skel * (1f - headK) * alpha;
            if (skullA > .002f)
            {
                Vector2[] skull = Pts(SLine, 25);
                for (int j = 0; j <= 24; j++)
                {
                    float a = -Mathf.PI * .15f + j / 24f * Mathf.PI * 1.3f;
                    skull[j] = F.At(Mathf.Cos(a) * .38f, 2.86f + bob + Mathf.Sin(a) * .42f);
                }
                GlowLine(skull, 25, .05f * kx, bone, .85f * skullA, Front + .009f, g.skel);
                Vector2[] jaw = Pts(SLine, 4);
                jaw[0] = F.At(-.3f, 2.62f + bob); jaw[1] = F.At(-.2f, 2.36f + bob); jaw[2] = F.At(.2f, 2.36f + bob); jaw[3] = F.At(.3f, 2.62f + bob);
                GlowLine(jaw, 4, .045f * kx, bone, .7f * skullA, Front + .009f, g.skel);
                for (int side = -1; side <= 1; side += 2) Blob(F.At(side * .16f, 2.78f + bob), .1f * kx, .08f * kz, A(Mouth, .6f * skullA), Front + .0095f);
                Vector2 e = F.At(-.16f, 2.78f + bob);
                Sprite(e, .3f * g.oneEye, .3f * g.oneEye, A(Eye, .6f * skullA * g.oneEye), glow, Front + .0097f);
                Blob(e, .045f * kx, .03f * kz, A(EyeHot, skullA * g.oneEye), Front + .0098f, 0f, add);
            }
            if (headK <= 0f) return;
            float hA = alpha * headK;
            SusanooFrame Fr = F;
            Vector2 P(float u, float v) => Fr.At(u, v + bob);

            // Hood, from the shoulders up round the head, rising to a point into the flames.
            Vector2[] hood = Pts(SEdgeT, HoodEdge.Length);
            for (int i = 0; i < hood.Length; i++) hood[i] = new Vector2(HoodEdge[i].x, HoodEdge[i].y * Lerp(.75f, 1f, headK));
            Pair(hood, hood.Length, F, bob, out Vector2[] hl, out Vector2[] hr);
            FillStrip(hl, hr, deep, .9f * fillA * headK, Front + .009f);
            int hn = hood.Length;
            Vector2[] hoodLine = Pts(SLine, hn * 2);
            for (int i = 0; i < hn; i++)
            {
                hoodLine[i] = hr[i];
                hoodLine[hn + i] = hl[hn - 1 - i];
            }
            GlowLine(hoodLine, hn * 2, .024f * kx, line, .8f * lineA, Front + .0092f, headK);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2[] fold = Pts(SLine, 4);
                fold[0] = P(side * .56f, 2.3f); fold[1] = P(side * .52f, 2.7f); fold[2] = P(side * .44f, 3.05f); fold[3] = P(side * .3f, 3.3f);
                GlowLine(fold, 4, .016f * kx, line, .4f * lineA * headK, Front + .0092f, 1f, 1f);
            }
            // The mask face inside the hood.
            FillBlob(P(FaceC.x, FaceC.y), FaceR.x * kx, FaceR.y * kz, lit, .85f * fillA * headK, Front + .0102f);
            Vector2[] face = Pts(SLine, 33);
            for (int j = 0; j <= 32; j++)
            {
                float a = j / 32f * TAU;
                face[j] = P(FaceC.x + Mathf.Cos(a) * FaceR.x, FaceC.y + Mathf.Sin(a) * FaceR.y);
            }
            GlowLine(face, 33, .022f * kx, line, .75f * lineA, Front + .0103f, headK);
            // Ear on the far side (screen right) with a ring earring hanging from it.
            FillBlob(P(.36f, 2.86f), .07f * kx, .12f * kz, lit, .85f * fillA * headK, Front + .0101f);
            Vector2[] ear = Pts(SLine, 6);
            ear[0] = P(.33f, 2.76f); ear[1] = P(.40f, 2.8f); ear[2] = P(.43f, 2.9f); ear[3] = P(.39f, 2.97f); ear[4] = P(.34f, 2.93f); ear[5] = P(.37f, 2.86f);
            GlowLine(ear, 6, .018f * kx, line, .75f * lineA * headK, Front + .0104f);
            DrawRing(P(.4f, 2.64f), .07f * kx, .07f * kz, 0f, A(line, .9f * lineA * headK), add, Front + .0104f);
            DrawRing(P(.39f, 2.74f), .025f * kx, .025f * kz, 0f, A(line, .8f * lineA * headK), add, Front + .0104f);
            // Gem on the forehead: a glowing orange crystal with facet lines.
            Vector2 gc = P(0f, 3.17f);
            Sprite(gc, .55f * kx, .55f * kz, A(FlameMid, .6f * hA), glow, Front + .0104f);
            Vector2[] gem = Pts(SA, 7);
            for (int j = 0; j <= 6; j++)
            {
                float a = j / 6f * TAU + Mathf.PI / 6f;
                gem[j] = P(Mathf.Cos(a) * .1f, 3.17f + Mathf.Sin(a) * .11f);
            }
            Fan(gc, gem, A(C(1f, .5f, .14f), .55f * hA), add, Front + .0105f);
            Sprite(gc, .12f * kx, .12f * kz, A(C(1f, .85f, .45f), .7f * hA), glow, Front + .01052f);
            GlowLine(gem, 7, .012f * kx, line, .5f * lineA * headK, Front + .0106f);
            Vector2[] facet = Pts(SLine, 3);
            facet[0] = gem[1]; facet[1] = gc; facet[2] = gem[4];
            GlowLine(facet, 3, .009f * kx, line, .35f * lineA * headK, Front + .0106f);
            // The mask's eye: one small dark oval right of the nose root, a socket line over it.
            Blob(P(.07f, 2.95f), .07f * kx, .05f * kz, A(Mouth, .95f * hA), Front + .0107f);
            Vector2[] socket = Pts(SLine, 4);
            socket[0] = P(-.04f, 2.96f); socket[1] = P(.02f, 3.02f); socket[2] = P(.12f, 3.02f); socket[3] = P(.19f, 2.96f);
            GlowLine(socket, 4, .016f * kx, line, .65f * lineA * headK, Front + .0108f);
            // Hood strands: lines sweeping round the head down to the jaw.
            for (int i = 0; i < Strands.Length; i++)
            {
                Vector2[] src = Strands[i];
                Vector2[] pts = Pts(SLine, src.Length);
                for (int j = 0; j < src.Length; j++) pts[j] = P(src[j].x, src[j].y);
                GlowLine(pts, src.Length, .015f * kx, line, .45f * lineA * headK, Front + .0103f, 1f, 1f);
            }
            // The mouth: a dark slot under a red upper lip; inside, the Susanoo's own yellow eyes.
            Vector2[] slotT = Pts(SA, 2), slotB = Pts(SB, 2), lipA = Pts(SC, 2), lipB = Pts(SD, 2);
            slotT[0] = P(-.38f, 2.64f); slotT[1] = P(.32f, 2.65f);
            slotB[0] = P(-.32f, 2.43f); slotB[1] = P(.28f, 2.43f);
            lipA[0] = P(-.4f, 2.64f); lipA[1] = P(.33f, 2.64f);
            lipB[0] = P(-.38f, 2.71f); lipB[1] = P(.3f, 2.71f);
            FillStrip(lipA, lipB, lit, .9f * fillA * headK, Front + .0109f);
            Strip(slotT, slotB, A(Mouth, .95f * hA), flat, Front + .011f);
            Vector2[] slotLine = Pts(SLine, 5);
            slotLine[0] = slotT[0]; slotLine[1] = slotT[1]; slotLine[2] = slotB[1]; slotLine[3] = slotB[0]; slotLine[4] = slotT[0];
            GlowLine(slotLine, 5, .014f * kx, line, .45f * lineA * headK, Front + .0111f);
            if (g.eyes > 0f)
                for (int i = 0; i < 2; i++)
                {
                    float u = i == 0 ? -.19f : .1f, rot = i == 0 ? 10f : -10f;
                    Vector2 e = P(u, 2.54f);
                    float pulse = .85f + .15f * Mathf.Sin(s * 5f + i);
                    Sprite(e, .42f * kx, .24f * kz, A(EyeGlow, .7f * g.eyes * pulse * alpha), glow, Front + .0112f);
                    Blob(e, .11f * kx, .045f * kz, A(Eye, g.eyes * alpha), Front + .0113f, rot, add);
                    Blob(e, .05f * kx, .02f * kz, A(EyeHot, .8f * g.eyes * alpha), Front + .01135f, rot, add);
                    if (g.flash > 0f)
                    {
                        float f = g.flash;
                        Sprite(e, 1.1f * f, .07f, A(EyeHot, .9f * f * alpha), glow, Front + .0135f);
                        Sprite(e, .07f, .8f * f, A(EyeHot, .7f * f * alpha), glow, Front + .0135f);
                    }
                }
            // Teeth: the body's red with pale outlines. alpha fang down between the eyes, a tall tusk up
            // at the right corner, a row of block teeth along the bottom lip.
            RedTooth(P(-.1f, 2.64f), P(.01f, 2.64f), P(-.045f, 2.48f), lit, line, hA, lineA * headK, kx);
            Vector2[] lipTop = Pts(SA, 13), lipBot = Pts(SB, 13);
            for (int j = 0; j <= 12; j++)
            {
                float f = j / 12f, u = Lerp(-.32f, .28f, f);
                lipTop[j] = P(u, 2.43f + (j % 2 == 1 ? .025f : 0f));
                lipBot[j] = P(u * .95f, 2.33f - .02f * Mathf.Sin(f * Mathf.PI));
            }
            FillStrip(lipTop, lipBot, lit, .95f * fillA * headK, Front + .01142f);
            GlowLine(lipBot, 13, .014f * kx, line, .55f * lineA * headK, Front + .0115f);
            for (int j = 1; j < 6; j++)
            {
                float u = Lerp(-.32f, .28f, j / 6f);
                Vector2[] split = Pts(SLine, 2);
                split[0] = P(u, 2.35f); split[1] = P(u, 2.45f);
                GlowLine(split, 2, .01f * kx, line, .5f * lineA * headK, Front + .0115f);
            }
            RedTooth(P(.17f, 2.36f), P(.28f, 2.36f), P(.25f, 2.74f), lit, line, hA, lineA * headK, kx);
            Vector2[] jawLine = Pts(SLine, 3);
            jawLine[0] = P(-.32f, 2.36f); jawLine[1] = P(-.05f, 2.3f); jawLine[2] = P(.26f, 2.36f);
            GlowLine(jawLine, 3, .016f * kx, line, .5f * lineA * headK, Front + .0111f, 1f, 1f);
            // The tengu nose: a long curved horn from just above the mouth, pointing up and to the
            // left at about 45 degrees and ending past the hood's edge in the flames.
            const int nn = 16;
            var root = new Vector2(-.08f, 2.8f);
            var tip = new Vector2(-1.0f, 3.85f);
            float ndx = tip.x - root.x, ndv = tip.y - root.y, nl = Mathf.Sqrt(ndx * ndx + ndv * ndv), nx = ndv / nl, nv = -ndx / nl;
            Vector2[] lower = Pts(SA, nn + 1), upper = Pts(SB, nn + 1);
            for (int j = 0; j <= nn; j++)
            {
                float f = j / (float)nn, bow = .07f * Mathf.Sin(f * Mathf.PI);
                float qu = Lerp(root.x, tip.x, f) - nx * bow, qv = Lerp(root.y, tip.y, f) - nv * bow;
                float w = Lerp(.36f, .025f, Mathf.Pow(f, .7f));
                lower[j] = P(qu - nx * w / 2f, qv - nv * w / 2f);
                upper[j] = P(qu + nx * w / 2f, qv + nv * w / 2f);
            }
            FillStrip(lower, upper, lit, fillA * headK, Front + .0116f);
            GlowLine(lower, nn + 1, .018f * kx, line, .85f * lineA * headK, Front + .0117f);
            GlowLine(upper, nn + 1, .018f * kx, line, .85f * lineA * headK, Front + .0117f);
            Vector2[] nostril = Pts(SLine, 3);
            nostril[0] = P(-.1f, 2.76f); nostril[1] = P(-.05f, 2.72f); nostril[2] = P(.01f, 2.75f);
            GlowLine(nostril, 3, .014f * kx, line, .65f * lineA * headK, Front + .0117f);
        }

        private static void RedTooth(Vector2 a, Vector2 b, Vector2 c, Color lit, Color line, float hA, float lineK, float kx)
        {
            Vector2[] rim = Pts(SC, 2);
            rim[0] = b; rim[1] = c;
            Fan(a, rim, A(lit, .95f * hA), flat, Front + .0114f);
            Vector2[] outline = Pts(SLine, 4);
            outline[0] = a; outline[1] = b; outline[2] = c; outline[3] = a;
            GlowLine(outline, 4, .012f * kx, line, .6f * lineK, Front + .0115f);
        }

        private static void Arms(in SusanooFrame F, float s, in SusanooLook look, in SusanooGrowth g, float alpha, SusanooPose pose, in Tone c, float boneDim)
        {
            float kx = c.kx, kz = c.kz, bob = c.bob, aK = c.aK, fillA = c.fillA, lineA = c.lineA;
            Color line = c.line, bone = c.bone, lit = c.lit;
            var rootR = new Vector2(-ArmRoot.x, ArmRoot.y + bob);
            var rootL = new Vector2(ArmRoot.x, ArmRoot.y + bob);
            ElbowFor(rootR, new Vector2(pose.hand.x, pose.hand.y + bob), -1, pose.elbowDown, Upper, Fore, out Vector2 eR, out Vector2 hR);
            float mdx = pose.mirror.x - rootL.x, mdv = pose.mirror.y + bob - rootL.y;
            float mirrorReach = Mathf.Sqrt(mdx * mdx + mdv * mdv), stretch = Mathf.Min(MaxStretch, Mathf.Max(1f, mirrorReach / (Upper + Fore - .02f)));
            ElbowFor(rootL, new Vector2(pose.mirror.x, pose.mirror.y + bob), 1, 0f, Upper * stretch, Fore * stretch, out Vector2 eL, out Vector2 hL);

            // Skeletal right arm: grows from the shoulder to the elbow, then the forearm's two bones,
            // then the fingers closing round the gourd. It dims as the armour covers it.
            float sk = g.skel, bonesA = Lerp(1f, .35f, boneDim) * c.alpha;
            if (sk > 0f)
            {
                Vector2[] hum = Pts(SLine, 2);
                hum[0] = F.At(rootR); hum[1] = F.At(eR);
                GlowLine(hum, 2, .075f * kx, bone, .85f * bonesA, Front + .02f, Clamp(sk * 2.2f));
                float fdx = hR.x - eR.x, fdv = hR.y - eR.y, fl = Mathf.Sqrt(fdx * fdx + fdv * fdv);
                if (fl <= 1e-6f) fl = 1f;
                for (int o = 0; o < 2; o++)
                {
                    float off = o == 0 ? -.035f : .035f, nx = -fdv / fl * off, nv = fdx / fl * off;
                    Vector2[] fa = Pts(SLine, 2);
                    fa[0] = F.At(eR.x + nx, eR.y + nv); fa[1] = F.At(hR.x + nx, hR.y + nv);
                    GlowLine(fa, 2, .05f * kx, bone, .8f * bonesA, Front + .02f, Clamp(sk * 2.2f - 1.1f));
                }
                float fk = Clamp(sk * 3f - 2f);
                if (fk > 0f)
                    for (int i = 0; i < 4; i++)
                    {
                        float a0 = (-40f + i * 28f) * Mathf.Deg2Rad;
                        Vector2[] pts = Pts(SLine, 5);
                        for (int j = 0; j <= 4; j++)
                        {
                            float a = a0 + j / 4f * 1.6f * fk;
                            pts[j] = F.At(hR.x + Mathf.Cos(a) * .16f - .05f, hR.y + Mathf.Sin(a) * .16f * (1f - .1f * i));
                        }
                        GlowLine(pts, 5, .035f * kx, bone, .75f * bonesA * fk, Front + .021f);
                    }
            }

            // Armoured arms: the right with the armour, the left with the mirror.
            ArmArm(F, rootR, eR, hR, Clamp(aK * 1.4f), -1, -1f, c);
            ArmArm(F, rootL, eL, hL, Clamp(g.mirror * 1.6f), 1, MirrorR * .8f, c);

            // Gourd in the right hand, pointing along the blade, and the Totsuka pouring out of it.
            float bladeDeg = pose.bladeDeg;
            var dir = new Vector2(Mathf.Cos(bladeDeg * Mathf.Deg2Rad), Mathf.Sin(bladeDeg * Mathf.Deg2Rad));
            float gk = Clamp(g.blade * 2.5f);
            if (gk > 0f)
            {
                float gA = gk * alpha;
                SusanooFrame Fr = F;
                Vector2 G(float along, float across) => Fr.At(hR.x + dir.x * along - dir.y * across, hR.y + dir.y * along + dir.x * across);
                int gn = GourdProfile.Length;
                Vector2[] gl = Pts(SA, gn), gr = Pts(SB, gn);
                for (int i = 0; i < gn; i++)
                {
                    gl[i] = G(GourdProfile[i][0], GourdProfile[i][1] * gk);
                    gr[i] = G(GourdProfile[i][0], -GourdProfile[i][1] * gk);
                }
                FillStrip(gl, gr, lit, .8f * fillA * gk + .1f * gA, Front + .024f);
                Vector2[] outline = Pts(SLine, gn * 2 + 1);
                for (int i = 0; i < gn; i++)
                {
                    outline[i] = gl[i];
                    outline[gn + i] = gr[gn - 1 - i];
                }
                outline[gn * 2] = gl[0];
                GlowLine(outline, gn * 2 + 1, .016f * kx, line, .75f * lineA * gk, Front + .0244f);
                Vector2[] cord = Pts(SLine, 3);
                cord[0] = G(.12f, .07f); cord[1] = G(.02f, .2f); cord[2] = G(-.1f, .24f);
                GlowLine(cord, 3, .012f * kx, line, .5f * lineA * gk, Front + .0248f, 1f, 1f);
                // The armoured fist round the gourd's waist: a rounded block across the grip, finger
                // lines across the knuckles, the thumb curled over the top.
                Vector2[] fist = Pts(SLine, 21);
                for (int j = 0; j <= 20; j++)
                {
                    float a0 = j / 20f * TAU;
                    fist[j] = G(Mathf.Sin(a0) * .12f, Mathf.Cos(a0) * .19f);
                }
                FillBlob(F.At(hR), .19f * kx * gk, .13f * kz * gk, lit, .95f * fillA * gk, Front + .0246f, -(bladeDeg - 90f));
                GlowLine(fist, 21, .016f * kx, line, .75f * lineA * gk, Front + .0247f);
                for (int i = 0; i < 3; i++)
                {
                    float w0 = -.19f + (i + 1) * .095f;
                    Vector2[] finger = Pts(SLine, 2);
                    finger[0] = G(-.1f, w0); finger[1] = G(.06f, w0);
                    GlowLine(finger, 2, .012f * kx, line, .5f * lineA * gk, Front + .0247f);
                }
                Vector2[] thumb = Pts(SLine, 4);
                thumb[0] = G(.04f, .17f); thumb[1] = G(.12f, .1f); thumb[2] = G(.12f, -.02f); thumb[3] = G(.07f, -.08f);
                GlowLine(thumb, 4, .016f * kx, line, .7f * lineA * gk, Front + .0247f);
            }
            float bk = g.blade;
            if (bk > 0f)
            {
                Vector2 mouth = F.At(hR.x + dir.x * .33f, hR.y + dir.y * .33f);
                Vector2 tip;
                if (pose.hasBladeTip) tip = pose.bladeTip;
                else
                {
                    float L0 = (pose.bladeLen >= 0f ? pose.bladeLen : look.bladeLen) * Smooth(bk);
                    tip = new Vector2(mouth.x + dir.x * L0 * kx, mouth.y + dir.y * L0 * kz);
                }
                Totsuka(mouth, tip, s, alpha * Clamp(bk * 3f), pose.stab);
            }

            // The Yata Mirror on the left hand: its ring draws round, then the fill and face come in.
            float mk = g.mirror;
            if (mk > 0f)
            {
                Vector2 c0 = F.At(pose.mirror.x, pose.mirror.y + bob);
                float rx = MirrorR * kx, rz = MirrorR * kz, fk = Smooth(Clamp(mk * 1.4f - .4f));
                Vector2[] ring = Pts(SLine, 49);
                for (int j = 0; j <= 48; j++)
                {
                    float a = Mathf.PI / 2f - j / 48f * TAU;
                    ring[j] = new Vector2(c0.x + Mathf.Cos(a) * rx, c0.y + Mathf.Sin(a) * rz);
                }
                // alpha darker disc under the face so the forearm behind the shield does not show through it.
                Blob(c0, rx * .98f, rz * .98f, A(Mouth, .35f * fk * alpha), Front + .0299f);
                FillBlob(c0, rx, rz, MirrorRed, .75f * fillA * fk, Front + .03f);
                Sprite(c0, rx * 2.5f, rz * 2.5f, A(FlameDeep, .14f * fk * alpha), glow, Front + .0295f);
                Sprite(c0, rx * 2.3f, rz * 2.3f, A(line, .85f * lineA * fk), mirrorMat, Front + .031f, 0f);
                float hitFlash = pose.mirrorFlash;
                GlowLine(ring, 49, .04f * kx, Color.Lerp(line, EyeHot, hitFlash), Mathf.Min(1.4f, .9f * lineA + .6f * hitFlash), Front + .032f, Clamp(mk * 1.6f));
                if (hitFlash > 0f)
                {
                    Sprite(c0, rx * 2.7f, rz * 2.7f, A(FlameMid, .35f * hitFlash * alpha), glow, Front + .0335f);
                    Sprite(c0, rx * 2.3f, rz * 2.3f, A(LineWarm, .5f * hitFlash * alpha), mirrorMat, Front + .0336f, 0f);
                }
                for (int i = 0; i < pose.hits.Count; i++) Ripple(c0, rx, rz, pose.hits[i].age, pose.hits[i].u, pose.hits[i].v, alpha);
            }
        }

        // One armoured arm as a tapered shape: upper arm with a biceps bulge, a round elbow, a
        // forearm that swells then narrows to the wrist. stopAt ends the forearm just inside the
        // mirror's rim instead of drawing its outline across the face.
        private static void ArmArm(in SusanooFrame F, Vector2 root, Vector2 e, Vector2 h, float k, int side, float stopAt, in Tone c)
        {
            if (k <= 0f) return;
            float kx = c.kx, kz = c.kz, fillA = c.fillA, lineA = c.lineA;
            Color lit = c.lit, line = c.line;
            float reach = k;
            if (stopAt > 0f)
            {
                float L1 = (e - root).magnitude, L2 = (h - e).magnitude, f = 1f;
                for (int i = 0; i <= 20; i++)
                {
                    float t = i / 20f;
                    if ((Vector2.Lerp(e, h, t) - h).magnitude < stopAt) { f = t; break; }
                }
                reach = Mathf.Min(k, (L1 + L2 * f) / (L1 + L2));
            }
            const int N = 10;
            Vector2[] cl = Pts(SC, 2 * N + 1);
            var widths = ArmWidths;
            for (int i = 0; i <= N; i++)
            {
                float t = i / (float)N;
                cl[i] = Vector2.Lerp(root, e, t);
                widths[i] = Lerp(.48f, .36f, t) + .1f * Mathf.Sin(Mathf.Pow(t, .8f) * Mathf.PI);
            }
            for (int i = 1; i <= N; i++)
            {
                float t = i / (float)N;
                cl[N + i] = Vector2.Lerp(e, h, t);
                widths[N + i] = Lerp(.38f, .24f, t) + .1f * Mathf.Sin(Mathf.Min(1f, t * 1.5f) * Mathf.PI) * (1f - t * .5f);
            }
            int keep = Mathf.Max(1, Mathf.FloorToInt(reach * (2 * N))), n = keep + 1;
            Vector2[] pts = Pts(SD, n);
            for (int i = 0; i < n; i++) pts[i] = F.At(cl[i]);
            Vector2[] a = Pts(SA, n), b = Pts(SB, n);
            for (int i = 0; i < n; i++)
            {
                Vector2 q0 = pts[Mathf.Max(0, i - 1)], q1 = pts[Mathf.Min(n - 1, i + 1)];
                float dx = q1.x - q0.x, dz = q1.y - q0.y, l = Mathf.Sqrt(dx * dx + dz * dz);
                if (l <= 1e-6f) l = 1f;
                float w = widths[i] / 2f * kx;
                a[i] = new Vector2(pts[i].x - dz / l * w, pts[i].y + dx / l * w);
                b[i] = new Vector2(pts[i].x + dz / l * w, pts[i].y - dx / l * w);
            }
            FillStrip(a, b, lit, .8f * fillA * k, Front + .022f);
            if (keep > N) FillBlob(F.At(e), .21f * kx, .19f * kz, lit, .5f * fillA * k, Front + .0221f);
            GlowLine(a, n, .02f * kx, line, .75f * lineA * k, Front + .023f);
            GlowLine(b, n, .02f * kx, line, .75f * lineA * k, Front + .023f);
            // Bracer lines across the forearm and a flame curl on the outside of each segment.
            if (k > .7f)
            {
                float q = Clamp((k - .7f) / .3f);
                float dx = h.x - e.x, dv = h.y - e.y, L0 = Mathf.Sqrt(dx * dx + dv * dv);
                if (L0 <= 1e-6f) L0 = 1f;
                for (int i = 1; i <= 2; i++)
                {
                    float f = .35f + i * .22f;
                    Vector2 c0 = Vector2.Lerp(e, h, f);
                    float nx = -dv / L0 * .15f, nv = dx / L0 * .15f;
                    Vector2[] br = Pts(SLine, 2);
                    br[0] = F.At(c0.x - nx, c0.y - nv); br[1] = F.At(c0.x + nx, c0.y + nv);
                    GlowLine(br, 2, .018f * kx, line, .5f * lineA * q, Front + .023f);
                }
                for (int i = 0; i < 2; i++)
                {
                    Vector2 p0 = i == 0 ? root : e, p1 = i == 0 ? e : h;
                    var m = new Vector2((p0.x + p1.x) / 2f, (p0.y + p1.y) / 2f);
                    float ang = Mathf.Atan2(p1.y - p0.y, p1.x - p0.x) * Mathf.Rad2Deg;
                    Sprite(F.At(m.x + side * .12f, m.y), .34f * kx, .34f * kz, A(line, .5f * lineA * q), curlMat, Front + .0235f, -ang + 90f + (side < 0 ? 180f : 0f));
                }
            }
        }

        private static readonly float[] ArmWidths = new float[21];

        // ---- the Totsuka Blade, the mirror's ripples, and the seal

        private static readonly float[] SpineF = new float[29];

        /// <summary>
        /// The Totsuka Blade: a flowing flame stream from the gourd's mouth to <paramref name="tip"/>,
        /// in nested additive strips with a wave running along it, bright dashes and dark streaks
        /// flowing out, and sparks off the tip. stab 0..1 thins and straightens it for the thrust.
        /// </summary>
        public static void Totsuka(Vector2 mouth, Vector2 tip, float s, float alpha, float stab = 0f)
        {
            if (alpha <= .002f) return;
            float dx = tip.x - mouth.x, dz = tip.y - mouth.y, L = Mathf.Sqrt(dx * dx + dz * dz);
            if (L < .02f) return;
            float ux = dx / L, uz = dz / L, nx = -uz, nz = ux;
            const int n = 28;
            float wave = Lerp(.07f, .025f, stab), wide = Lerp(1f, .7f, stab);
            Vector2[] spine = Pts(SLine, n + 1);
            for (int i = 0; i <= n; i++)
            {
                float f = i / (float)n, off = wave * Mathf.Sin(f * 7f - s * 11f) * Mathf.Sin(f * Mathf.PI) + .03f * Mathf.Sin(f * 17f - s * 19f) * f;
                spine[i] = new Vector2(mouth.x + ux * L * f + nx * off, mouth.y + uz * L * f + nz * off);
                SpineF[i] = f;
            }
            float WidthAt(float f) => wide * (.1f + .16f * Mathf.Sin(Mathf.Min(1f, f * 1.8f) * Mathf.PI / 2f)) * (1f - f * f * f) + .015f;
            void Band(float share, Color colour, float a, float layer)
            {
                Vector2[] A0 = Pts(SSideA, n + 1), B0 = Pts(SSideB, n + 1);
                for (int i = 0; i <= n; i++)
                {
                    float w = WidthAt(SpineF[i]) * share * (1f + .12f * Mathf.Sin(SpineF[i] * 9f - s * 14f + share * 3f));
                    A0[i] = new Vector2(spine[i].x + nx * w, spine[i].y + nz * w);
                    B0[i] = new Vector2(spine[i].x - nx * w, spine[i].y - nz * w);
                }
                Strip(A0, B0, A(colour, a * alpha), add, layer);
            }
            Sprite(new Vector2((mouth.x + tip.x) / 2f, (mouth.y + tip.y) / 2f), L * 1.1f + .4f, .55f, A(FlameDeep, .12f * alpha), glow, Front + .0248f, -Mathf.Atan2(dz, dx) * Mathf.Rad2Deg);
            Band(1.25f, BladeEdge, .6f, Front + .025f);
            Band(.85f, BladeBody, .6f, Front + .0252f);
            Band(.38f, BladeCore, .75f, Front + .0254f);
            Band(.14f, BladeHot, .6f, Front + .0256f);
            // Dark streaks and bright dashes running out along the stream.
            for (int i = 0; i < 7; i++)
            {
                float f0 = Mathf.Repeat(s * (1.1f + .4f * Rand(i + 500)) + Rand(i + 510), 1f), f1 = Mathf.Min(1f, f0 + .12f);
                float side = (Rand(i + 520) - .5f) * 1.2f;
                Vector2[] pts = Pts(SLine2, 5);
                for (int j = 0; j <= 4; j++)
                {
                    float f = Lerp(f0, f1, j / 4f);
                    int k = Mathf.Min(n, Mathf.RoundToInt(f * n));
                    float w = WidthAt(f) * side;
                    pts[j] = new Vector2(spine[k].x + nx * w, spine[k].y + nz * w);
                }
                bool dark = i % 3 == 0;
                Stroke(pts, 5, .025f, A(dark ? Mouth : BladeHot, (dark ? .25f : .5f) * alpha * Mathf.Sin(f0 * Mathf.PI)), dark ? flat : add, Front + .0258f, 1f, 1f);
            }
            // Sparks and flame licks off the tip.
            for (int i = 0; i < 6; i++)
            {
                float age = Mathf.Repeat(s * 1.8f + Rand(i + 530), 1f), a0 = Mathf.Atan2(uz, ux) + (Rand(i + 540) - .5f) * 1.6f;
                float d = .08f + age * .35f, size = .09f * (1f - age) + .02f;
                Sprite(new Vector2(tip.x + Mathf.Cos(a0) * d, tip.y + Mathf.Sin(a0) * d + age * .1f), size, size, A(BladeCore, .7f * (1f - age) * alpha), glow, Front + .026f);
            }
            Sprite(tip, .35f, .35f, A(BladeCore, .35f * alpha), glow, Front + .0259f);
        }

        /// <summary>
        /// Concentric rings spreading over the mirror from where a hit lands ((u, v) on the unit
        /// face): four rings, 0.07 s apart, growing to span the face in 0.5 s; the parts outside
        /// the face are left out. alpha flash marks the point.
        /// </summary>
        public static void Ripple(Vector2 c0, float rx, float rz, float age, float au, float av, float A0)
        {
            if (age < 0f || age > .75f) return;
            var p = new Vector2(c0.x + au * rx, c0.y + av * rz);
            float f = Mathf.Max(0f, 1f - age / .15f);
            Sprite(p, .25f + .6f * f, .25f + .6f * f, A(EyeHot, .95f * f * A0), glow, Front + .034f);
            for (int k = 0; k < 4; k++)
            {
                float t = age - k * .07f;
                if (t < 0f || t > .5f) continue;
                float grow = 1f - Mathf.Pow(1f - t / .5f, 2f), r = .08f + grow * 2.0f, a = (1f - t / .5f) * (1f - k * .15f);
                Vector2[] run = Pts(SLine, 65);
                int count = 0;
                for (int j = 0; j <= 64; j++)
                {
                    float ang = j / 64f * TAU, x = p.x + Mathf.Cos(ang) * r * rx, z = p.y + Mathf.Sin(ang) * r * rz;
                    float dxu = (x - c0.x) / rx, dzu = (z - c0.y) / rz;
                    if (Mathf.Sqrt(dxu * dxu + dzu * dzu) < .96f) run[count++] = new Vector2(x, z);
                    else
                    {
                        if (count > 1) Stroke(run, count, .05f, A(LineWarm, a * A0), add, Front + .033f);
                        count = 0;
                    }
                }
                if (count > 1) Stroke(run, count, .05f, A(LineWarm, a * A0), add, Front + .033f);
            }
        }

        /// <summary>
        /// The sealed target on its way into the gourd: its silhouette, dark against the bright blade,
        /// rimmed with flame, stretching along the pull (dirDeg, toward the gourd) and shrinking as it
        /// goes, with flame licks trailing. stretch 0..1 (0 = where it stood, 1 = at the gourd).
        /// </summary>
        public static void Pulled(Vector2 pos, float dirDeg, float stretch, float alpha, float s)
        {
            if (alpha <= .002f) return;
            float rot = -dirDeg, shrink = 1f - .7f * stretch, len = (.26f + stretch * .5f) * shrink + .06f, thin = .3f * shrink * (1f - stretch * .35f);
            float a = dirDeg * Mathf.Deg2Rad;
            var fwd = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Sprite(pos, len * 3.2f, thin * 3.6f, A(FlameDeep, .55f * alpha), glow, Front + .027f, rot);
            Blob(pos, len, thin, A(C(.16f, .05f, .04f), .92f * alpha), Front + .0272f, rot);
            // The head at the front of the shape, going in first.
            var head = new Vector2(pos.x + fwd.x * len * .75f, pos.y + fwd.y * len * .75f);
            Blob(head, .12f * shrink + .02f, .12f * shrink + .02f, A(C(.2f, .06f, .05f), .92f * alpha), Front + .0273f);
            for (int i = 0; i < 11; i++)
            {
                float f = Mathf.Repeat(s * 3.5f + i / 11f, 1f), off = (Rand(i + 700) - .5f) * thin * 2.2f;
                var p0 = new Vector2(pos.x - fwd.x * (len * .7f + f * .45f) - fwd.y * off, pos.y - fwd.y * (len * .7f + f * .45f) + fwd.x * off);
                var p1 = new Vector2(p0.x - fwd.x * .3f, p0.y - fwd.y * .3f);
                Vector2[] lick = Pts(SLine, 2);
                lick[0] = p0; lick[1] = p1;
                Stroke(lick, 2, .12f * (1f - f) * shrink + .03f, A(i % 3 != 0 ? FlameMid : BladeCore, .85f * (1f - f) * alpha), add, Front + .0274f, 1f, 1f);
            }
        }

        /// <summary>Flames wrapping a pawn (the moment the Totsuka pierces it): a ring of flame tips round its body over a red glow. amount 0..1.</summary>
        public static void FlameWrap(Vector2 pos, float s, float amount)
        {
            if (amount <= .002f) return;
            Sprite(new Vector2(pos.x, pos.y + .1f), 1.1f, 1.3f, A(FlameDeep, .32f * amount), glow, Front + .0266f);
            Vector2[] ring = Pts(SLine, 29);
            for (int j = 0; j <= 28; j++)
            {
                float a = -j / 28f * TAU;
                ring[j] = new Vector2(pos.x + Mathf.Cos(a) * .34f, pos.y + .1f + Mathf.Sin(a) * .48f);
            }
            FlameEdge(ring, 29, s, .5f * amount, 1.3f * amount, 16, 11, Front + .0268f, 1.4f);
        }

        /// <summary>The seal at the gourd's mouth: a flash, a ring closing in and a swirl turning shut. age in s.</summary>
        public static void SealFlash(Vector2 pos, float age, float A0 = 1f)
        {
            if (age < 0f || age > .5f) return;
            float u = age / .5f, f = Mathf.Max(0f, 1f - age / .14f);
            Sprite(pos, 1.4f * f + .3f, 1.4f * f + .3f, A(EyeHot, .95f * f * A0), glow, Front + .036f);
            float r = .7f * (1f - u) + .08f;
            DrawRing(pos, r, r, 0f, A(LineWarm, (1f - u) * A0), add, Front + .0361f);
            Sprite(pos, .9f * (1f - u) + .2f, .9f * (1f - u) + .2f, A(LineWarm, (1f - u) * A0), swirlMat, Front + .0362f, age * 900f);
        }

        /// <summary>alpha thin ring on the floor at the true radius.</summary>
        public static void FloorRing(Vector2 pos, float radius, Color colour, float alpha)
        {
            if (alpha <= .002f) return;
            DrawRing(pos, radius, radius, 0f, A(colour, alpha), add, Floor + .02f);
        }

        /// <summary>alpha star glint (core and four rays).</summary>
        public static void Glint(Vector2 pos, float size, float alpha, Color colour, float layer = -1f)
        {
            if (alpha <= .002f) return;
            if (layer < 0f) layer = Front + .2f;
            Sprite(pos, size * .5f, size * .5f, A(colour, alpha), glow, layer);
            Sprite(pos, size * 1.6f, size * .09f, A(colour, alpha * .9f), glow, layer + .001f);
            Sprite(pos, size * .09f, size * 1.6f, A(colour, alpha * .9f), glow, layer + .001f);
        }

        /// <summary>Sparks thrown off a blocked hit at <paramref name="at"/>, back toward <paramref name="back"/> (unit direction). age in s.</summary>
        public static void Sparks(Vector2 at, Vector2 back, float age, int n = 8, float life = .35f, int seed = 0)
        {
            if (age < 0f || age > life) return;
            for (int i = 0; i < n; i++)
            {
                int k = seed * 31 + i;
                float spread = (Rand(k + 600) - .5f) * 2.2f, a = Mathf.Atan2(back.y, back.x) + spread;
                float v = 1.8f + 2.2f * Rand(k + 610), t = age, u = t / life;
                var p = new Vector2(at.x + Mathf.Cos(a) * v * t, at.y + Mathf.Sin(a) * v * t - 1.4f * t * t);
                var q = new Vector2(p.x - Mathf.Cos(a) * .08f, p.y - Mathf.Sin(a) * .08f + .04f);
                Vector2[] pts = Pts(SLine, 2);
                pts[0] = q; pts[1] = p;
                Stroke(pts, 2, .03f, A(C(1f, .86f, .5f), 1f - u), add, Front + .06f, 1f, .5f);
            }
            float sz = .5f * (1f - age / life) + .1f;
            Sprite(at, sz, sz, A(C(1f, .9f, .6f), Mathf.Max(0f, 1f - age / .12f)), glow, Front + .061f);
        }

        // Where the embers of each part start (design units) and when (share of D): the same windows as Breaking.
        /// <summary>Embers of the breaking Susanoo: small flecks of red-orange light rising and drifting from where each part was.</summary>
        public static void Embers(in SusanooFrame F, float tb, float D)
        {
            for (int j = 0; j < 3; j++)
            {
                float t0 = j == 0 ? 0f : j == 1 ? .15f : .3f, t1 = j == 0 ? .35f : j == 1 ? .5f : .7f;
                int count = j == 0 ? 34 : j == 1 ? 30 : 56;
                for (int i = 0; i < count; i++)
                {
                    int sd = j * 1000 + i * 13 + 800;
                    float born = (t0 + (t1 - t0) * Rand(sd + 4)) * D, life = .6f + .5f * Rand(sd + 5), age = tb - born;
                    if (age < 0f || age > life) continue;
                    float u = age / life;
                    float pu, pv;
                    if (j == 0) { pu = (Rand(sd + 1) - .5f) * .95f; pv = 2.3f + Rand(sd + 2) * 1.1f; }
                    else if (j == 1)
                    {
                        if (Rand(sd + 1) < .5f) { pu = -1.1f - Rand(sd + 2) * .7f; pv = .9f + Rand(sd + 3) * 1.3f; }
                        else { pu = 1.0f + Rand(sd + 2) * 1.1f; pv = .6f + Rand(sd + 3) * 1.4f; }
                    }
                    else { pu = (Rand(sd + 1) - .5f) * 2.4f; pv = .3f + Rand(sd + 2) * 2.1f; }
                    float rise = (.7f + .8f * Rand(sd + 6)) * age + .35f * age * age;
                    float sway = Mathf.Sin(age * (3f + 3f * Rand(sd + 7)) + Rand(sd + 8) * 6f) * .12f * age + (Rand(sd + 9) - .5f) * .5f * age;
                    var pos = new Vector2(F.Feet.x + pu * F.kx + sway, F.Feet.y + pv * F.kz + rise);
                    bool big = Rand(sd + 10) > .8f;
                    float size = (big ? .09f : .04f + .035f * Rand(sd + 12)) * (1f - u * .5f);
                    float flicker = .6f + .4f * Mathf.Sin(age * (22f + 14f * Rand(sd + 13)) + i);
                    float a = Mathf.Sin(Mathf.Min(1f, u * 5f) * Mathf.PI / 2f) * (1f - u) * flicker;
                    float vx = Mathf.Cos(age * (3f + 3f * Rand(sd + 7)) + Rand(sd + 8) * 6f) * .12f * (3f + 3f * Rand(sd + 7)) * age + (Rand(sd + 9) - .5f) * .5f;
                    float vz = (.7f + .8f * Rand(sd + 6)) + .7f * age;
                    float L = Mathf.Sqrt(vx * vx + vz * vz);
                    if (L <= 1e-6f) L = 1f;
                    float tail = size * (3f + 3f * Rand(sd + 14));
                    var back = new Vector2(pos.x - vx / L * tail, pos.y - vz / L * tail);
                    if (big) Sprite(pos, .3f, .3f, A(FlameDeep, .3f * a), glow, Front + .04f);
                    Vector2[] pts = Pts(SLine, 2);
                    pts[0] = back; pts[1] = pos;
                    Stroke(pts, 2, size * 2f, A(Rand(sd + 11) < .4f ? FlamePale : FlameMid, .95f * a), add, Front + .0401f, 1f, .7f);
                }
            }
        }
    }
}
