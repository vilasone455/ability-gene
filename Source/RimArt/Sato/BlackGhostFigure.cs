using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.BlackGhostMatter;

namespace RimArt
{
    /// <summary>What the figure does this frame, in the pose's own terms (lib/ajin-ghost-v2.js pose2 options).</summary>
    internal struct BlackGhostMotion
    {
        /// <summary>Strides taken (1 = one full cycle), how much it walks 0..1, clip seconds for the idle motion.</summary>
        internal float Gait, Walk, Seconds;
        /// <summary>Share of a swipe 0..1, negative for none; Side +1 or -1 picks the arm in the front and back views.</summary>
        internal float Swipe;
        internal int Side;
        /// <summary>Jaw open and tongue out, 0..1, from the caller (a swipe opens the jaw by itself).</summary>
        internal float Jaw, Tongue;
        /// <summary>Tear: wrist targets in (u, h) for the holding hand (index 0) and the tearing hand (index 1), and how far each has gone.</summary>
        internal bool Grip;
        internal Vector2 Hold, Tear;
        internal float Kh, Kt;
    }

    /// <summary>
    /// The v2 Black Ghost figure: the port of ghost2() and pose2() in Tools/VfxLab/web/sketches/lib/ajin-ghost-v2.js.
    /// A joint at (u, h) is drawn at feet.x + u, feet.z + h * Stand, upright in the screen plane like a pawn sprite; west
    /// is east mirrored. Heights are clamped into [lo, hi], so one routine draws the ghost growing from the feet up and
    /// breaking up from the feet up. South: front (hood pointing down at the snout, mouth, tongue); north: back (hood
    /// point up, spine groove, arms behind the body); east: profile (wedge head, hinged jaw). The head has its own draw
    /// method per facing. Scale is the sketch's default, 1. All the figure's draws stack by <see cref="BlackGhostMatter.Next"/>.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class BlackGhostFigure
    {
        /// <summary>Head top at scale 1 (front), in cells of height; claw length.</summary>
        internal const float Top = 1.87f, ClawLen = 0.34f;
        private const float SwayPeriod = 2.6f, JawSwing = 0.55f;

        // Head shapes in the head's own (u, h), origin at the top of the neck (see lib/ajin-ghost-v2.js).
        // Front: left edge from the blunt snout up to the middle of the back edge; the right edge mirrors it.
        private static readonly Vector2[] FrontEdge =
        {
            new Vector2(-0.035f, -0.11f), new Vector2(-0.09f, -0.02f), new Vector2(-0.16f, 0.08f), new Vector2(-0.24f, 0.17f),
            new Vector2(-0.29f, 0.24f), new Vector2(-0.26f, 0.30f), new Vector2(-0.16f, 0.34f), new Vector2(0f, 0.355f),
        };
        // Back: from the back of the skull (low, nearest the viewer) up to the snout tip (far).
        private static readonly Vector2[] BackEdge =
        {
            new Vector2(-0.05f, -0.10f), new Vector2(-0.19f, -0.02f), new Vector2(-0.28f, 0.07f), new Vector2(-0.25f, 0.14f),
            new Vector2(-0.15f, 0.24f), new Vector2(-0.06f, 0.32f), new Vector2(0f, 0.36f),
        };
        // Front mouth: the upper jaw's lower edge, corner to snout tip to corner, and how far each point drops fully open.
        private static readonly Vector2[] MouthFront =
        {
            new Vector2(-0.15f, 0.06f), new Vector2(-0.09f, -0.02f), new Vector2(0f, -0.115f), new Vector2(0.09f, -0.02f), new Vector2(0.15f, 0.06f),
        };
        private static readonly float[] DropFront = { 0f, 0.09f, 0.17f, 0.09f, 0f };
        // Side, snout toward +u: the top edge and the mouth line, paired by index; the last pair is the blunt snout end.
        private static readonly Vector2[] SideTop =
        {
            new Vector2(-0.08f, 0.07f), new Vector2(0f, 0.13f), new Vector2(0.10f, 0.125f), new Vector2(0.22f, 0.095f),
            new Vector2(0.32f, 0.06f), new Vector2(0.39f, 0.04f), new Vector2(0.43f, 0.02f),
        };
        private static readonly Vector2[] SideMouth =
        {
            new Vector2(-0.08f, -0.07f), new Vector2(0f, -0.06f), new Vector2(0.10f, -0.05f), new Vector2(0.22f, -0.036f),
            new Vector2(0.32f, -0.022f), new Vector2(0.39f, -0.014f), new Vector2(0.43f, -0.004f),
        };
        // The lower jaw, hinged at its first point.
        private static readonly Vector2[] JawTop =
        {
            new Vector2(0f, -0.06f), new Vector2(0.10f, -0.052f), new Vector2(0.22f, -0.04f), new Vector2(0.32f, -0.028f), new Vector2(0.40f, -0.018f),
        };
        private static readonly Vector2[] JawBottom =
        {
            new Vector2(0f, -0.13f), new Vector2(0.10f, -0.12f), new Vector2(0.22f, -0.095f), new Vector2(0.32f, -0.06f), new Vector2(0.40f, -0.03f),
        };
        private static readonly Vector2 Hinge = new Vector2(0f, -0.06f);
        // The front torso's left edge before the sway and bob; the right edge mirrors it.
        private static readonly Vector2[] FrontTorso =
        {
            new Vector2(-0.12f, 0.91f), new Vector2(-0.10f, 1.04f), new Vector2(-0.26f, 1.22f), new Vector2(-0.42f, 1.36f), new Vector2(-0.17f, 1.47f),
        };
        private static readonly float[] ThighSide = { 0.17f, 0.12f, 0.08f, 0.07f }, ThighFront = { 0.17f, 0.12f, 0.085f, 0.11f },
            ArmWidths = { 0.15f, 0.11f, 0.085f }, NeckWidths = { 0.13f, 0.12f };
        private static readonly float[] thigh = new float[4], arm = new float[3];

        // ---- the pose (pose2), kept between Pose and the draw ----

        private static readonly Vector2[][] legs = { new Vector2[4], new Vector2[4] }, arms = { new Vector2[3], new Vector2[3] };
        private static readonly Vector2[] torsoL = new Vector2[5], torsoR = new Vector2[5], torsoBack = new Vector2[4], torsoFront = new Vector2[4],
            neck = new Vector2[2];
        private static Vector2 head;
        private static float tilt, grow, jaw, tongue, flex, hc = 1f, hsn;

        // ---- this frame's mapping ----

        private static Vector2 feet;
        private static float lo, hi, mirror = 1f;

        /// <summary>After <see cref="Joints"/> or <see cref="Draw"/>: both wrists and elbows on screen (0 far or left, 1 near or right).</summary>
        internal static readonly Vector2[] Wrists = new Vector2[2], Elbows = new Vector2[2];
        /// <summary>After <see cref="Draw"/>: the pieces flakes and chunks come off, in (u, h).</summary>
        internal static readonly List<BlackGhostSeg> Segs = new List<BlackGhostSeg>();

        /// <summary>The share of the figure standing between lo and hi; 0 draws nothing.</summary>
        internal static float Shown(float lo, float hi) => Mathf.Clamp01((Mathf.Min(hi, Top) - lo) / Top);

        private static Vector2 S(Vector2 q) => new Vector2(feet.x + q.x * mirror, feet.y + Mathf.Min(hi, Mathf.Max(lo, q.y)) * Stand);
        private static Vector2 H(Vector2 q) => new Vector2(head.x + (q.x * hc - q.y * hsn) * grow, head.y + (q.x * hsn + q.y * hc) * grow);
        private static Vector2 HS(Vector2 q) => S(H(q));
        private static Vector2 HS(float u, float h) => S(H(new Vector2(u, h)));

        private static Vector2[] MapS(Vector2[] pts)
        {
            Vector2[] P = Buf(pts.Length);
            for (int i = 0; i < pts.Length; i++) P[i] = S(pts[i]);
            return P;
        }

        private static Vector2[] MapHS(Vector2[] pts, bool flip = false)
        {
            Vector2[] P = Buf(pts.Length);
            for (int i = 0; i < pts.Length; i++) P[i] = HS(flip ? new Vector2(-pts[i].x, pts[i].y) : pts[i]);
            return P;
        }

        /// <summary>The points scaled by k about their own centroid (ghost2's grown()).</summary>
        private static Vector2[] Grown(Vector2[] P, float k)
        {
            Vector2 c = Vector2.zero;
            for (int i = 0; i < P.Length; i++) c += P[i] / P.Length;
            Vector2[] G = Buf(P.Length);
            for (int i = 0; i < P.Length; i++) G[i] = c + (P[i] - c) * k;
            return G;
        }

        /// <summary>The points k of the way from L to R, paired by index.</summary>
        private static Vector2[] Across(Vector2[] L, Vector2[] R, float k)
        {
            Vector2[] P = Buf(L.Length);
            for (int i = 0; i < L.Length; i++) P[i] = Vector2.Lerp(L[i], R[i], k);
            return P;
        }

        private static Vector2[] Three(Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2[] P = Buf(3);
            P[0] = a; P[1] = b; P[2] = c;
            return P;
        }

        // ---- pose ----

        /// <summary>pose2(): the joints in (u, h) at scale 1. face 0 north, 1 east, 2 south, 3 west (west posed as east).</summary>
        private static void Pose(int face, in BlackGhostMotion m)
        {
            float ph = m.Gait * TAU, walk = m.Walk, idle = 1f - walk, busy = m.Swipe >= 0f ? 1f : 0f, t = m.Seconds, w = m.Swipe;
            // Swipe: raise over 0..0.5, strike 0.5..0.68, recover 0.68..1.
            float up = w < 0f ? 0f : w < 0.5f ? Smooth(w / 0.5f) : w < 0.68f ? 1f : 1f - Smooth((w - 0.68f) / 0.32f);
            float strike = w < 0.5f ? 0f : w < 0.68f ? Smooth((w - 0.5f) / 0.18f) : 1f - Smooth((w - 0.68f) / 0.32f);
            float sway = idle * (1f - busy) * Mathf.Sin(t * TAU / SwayPeriod);
            // Breathing and the stride bob (the sketch's reach, for the Relay order, is not ported).
            float d = idle * Mathf.Sin(t * 2.3f) * 0.012f + walk * (Mathf.Abs(Mathf.Sin(ph)) - 0.5f) * 0.05f;
            jaw = Mathf.Max(m.Jaw, up * 0.45f + strike * 0.55f);
            tongue = busy > 0f ? 0f : m.Tongue;
            flex = idle * Mathf.Sin(t * 3.1f);

            if (face != 1 && face != 3)
            {
                float hs = walk * Mathf.Sin(ph) * 0.03f;   // weight shifting side to side
                for (int k = 0; k < 2; k++)
                {
                    float s = k == 0 ? -1f : 1f, lift = walk * Mathf.Max(0f, Mathf.Sin(ph + (s < 0f ? Mathf.PI : 0f))) * 0.14f;
                    Vector2[] l = legs[k];
                    l[0] = new Vector2(s * 0.10f + hs * 0.5f, 0.95f + d * 0.5f);
                    l[1] = new Vector2(s * 0.19f, 0.52f + lift);
                    l[2] = new Vector2(s * 0.15f, 0.09f + lift * 0.5f);
                    l[3] = new Vector2(s * 0.20f, -0.01f + lift * 0.4f);
                }
                for (int i = 0; i < 5; i++)
                {
                    torsoL[i] = FrontTorso[i] + new Vector2(hs, d);
                    torsoR[i] = new Vector2(2f * hs - torsoL[i].x, torsoL[i].y);
                }
                for (int k = 0; k < 2; k++)
                {
                    float s = k == 0 ? -1f : 1f, sw = walk * Mathf.Sin(ph + (s < 0f ? 0f : Mathf.PI)) * 0.07f;
                    var sh = new Vector2(s * 0.39f + hs, 1.33f + d);
                    var el = new Vector2(s * 0.52f + hs, 0.93f + sw + d);
                    var wr = new Vector2(s * 0.49f + hs, 0.47f + sw * 1.4f + d);
                    if ((int)s == m.Side && m.Swipe >= 0f)
                    {
                        el = new Vector2(Mathf.Lerp(Mathf.Lerp(el.x, s * 0.58f, up), s * 0.10f, strike), Mathf.Lerp(Mathf.Lerp(el.y, 1.62f, up), 1.00f, strike));
                        wr = new Vector2(Mathf.Lerp(Mathf.Lerp(wr.x, s * 0.40f, up), -s * 0.34f, strike),
                            Mathf.Lerp(Mathf.Lerp(wr.y, 2.00f, up), face == 2 ? 0.34f : 0.60f, strike));
                    }
                    arms[k][0] = sh; arms[k][1] = el; arms[k][2] = wr;
                }
                if (m.Grip) GripArms(m, true);
                neck[0] = new Vector2(hs, 1.40f + d);
                neck[1] = new Vector2(hs, 1.50f + d);
                head = new Vector2(hs + 0.05f * sway, 1.50f + d - 0.05f * strike);
                tilt = -0.09f * sway;
                grow = 1f + 0.06f * strike;
            }
            else
            {
                // Profile, facing +u: pelvis back, ribcage forward, neck thrust forward and the head in front of the
                // chest; knees bent; far limbs a little apart from the near ones (near = index 1).
                float lean = 0.16f + 0.06f * strike + (m.Grip ? 0.08f * Mathf.Max(m.Kh, m.Kt) : 0f);
                for (int k = 0; k < 2; k++)
                {
                    float s = k == 0 ? -1f : 1f, a = ph + (s < 0f ? Mathf.PI : 0f);
                    float fu = walk * 0.36f * Mathf.Sin(a) + (s < 0f ? -0.08f : 0.04f), lift = walk * Mathf.Max(0f, Mathf.Cos(a)) * 0.14f;
                    Vector2[] l = legs[k];
                    l[0] = new Vector2(-0.04f, 0.95f + d * 0.5f);
                    l[1] = new Vector2(fu * 0.5f + 0.13f, 0.52f + lift);
                    l[2] = new Vector2(fu - 0.04f, 0.08f + lift * 0.45f);
                    l[3] = new Vector2(fu + 0.14f, lift * 0.3f);
                }
                torsoBack[0] = new Vector2(-0.19f, 0.90f + d);
                torsoBack[1] = new Vector2(-0.16f, 1.04f + d);
                torsoBack[2] = new Vector2(-0.10f + lean * 0.5f, 1.22f + d);
                torsoBack[3] = new Vector2(0.02f + lean, 1.38f + d);
                torsoFront[0] = new Vector2(0.10f, 0.90f + d);
                torsoFront[1] = new Vector2(0.07f, 1.03f + d);
                torsoFront[2] = new Vector2(0.24f + lean * 0.6f, 1.18f + d);
                torsoFront[3] = new Vector2(0.26f + lean, 1.32f + d);
                var sh = new Vector2(0.10f + lean, 1.33f + d);
                for (int k = 0; k < 2; k++)
                {
                    float s = k == 0 ? -1f : 1f, sw = walk * Mathf.Sin(ph + (s < 0f ? 0f : Mathf.PI));
                    var s0 = new Vector2(sh.x + (s < 0f ? 0.07f : 0f), sh.y);
                    var el = new Vector2(s0.x + 0.02f + 0.16f * sw, 0.93f + d);
                    var wr = new Vector2(s0.x + 0.12f + 0.34f * sw, 0.50f + d + 0.05f * Mathf.Abs(sw));
                    if (k == 1 && m.Swipe >= 0f)
                    {
                        el = new Vector2(Mathf.Lerp(Mathf.Lerp(el.x, sh.x - 0.15f, up), sh.x + 0.45f, strike), Mathf.Lerp(Mathf.Lerp(el.y, 1.65f, up), 1.22f, strike));
                        wr = new Vector2(Mathf.Lerp(Mathf.Lerp(wr.x, sh.x - 0.03f, up), sh.x + 0.85f, strike), Mathf.Lerp(Mathf.Lerp(wr.y, 2.00f, up), 0.84f, strike));
                    }
                    arms[k][0] = s0; arms[k][1] = el; arms[k][2] = wr;
                }
                if (m.Grip) GripArms(m, false);
                var nt = new Vector2(0.30f + lean * 1.1f, 1.44f + d);
                neck[0] = new Vector2(0.14f + lean, 1.36f + d);
                neck[1] = nt;
                head = new Vector2(nt.x + 0.03f * sway + 0.10f * strike, nt.y - 0.02f * strike);
                tilt = -0.10f + 0.05f * sway;
                grow = 1f;
            }
            hc = Mathf.Cos(tilt);
            hsn = Mathf.Sin(tilt);
        }

        /// <summary>
        /// gripArms(): each wrist moves its share k of the way to its target and the elbow bends outward (front view) or
        /// down (side view) between shoulder and wrist.
        /// </summary>
        private static void GripArms(in BlackGhostMotion m, bool front)
        {
            for (int i = 0; i < 2; i++)
            {
                Vector2 q = i == 1 ? m.Tear : m.Hold;
                float k = i == 1 ? m.Kt : m.Kh;
                if (k <= 0f) continue;
                Vector2[] a = arms[i];
                Vector2 sh = a[0], wr = Vector2.Lerp(a[2], q, k);
                var el = new Vector2((sh.x + wr.x) / 2f + (front ? (i == 1 ? 0.10f : -0.10f) : -0.04f), (sh.y + wr.y) / 2f - 0.10f);
                a[1] = Vector2.Lerp(a[1], el, k);
                a[2] = wr;
            }
        }

        /// <summary>Poses the figure and puts its wrists and elbows on screen, without drawing (for Tear's claws).</summary>
        internal static void Joints(Vector2 feetAt, int face, in BlackGhostMotion m, float lowest, float highest)
        {
            feet = feetAt;
            lo = lowest;
            hi = highest;
            mirror = face == 3 ? -1f : 1f;
            Pose(face, m);
            for (int i = 0; i < 2; i++)
            {
                Wrists[i] = S(arms[i][2]);
                Elbows[i] = S(arms[i][1]);
            }
        }

        // ---- the draw ----

        private static void AddSegs(Vector2[] pts, float[] widths)
        {
            for (int i = 0; i + 1 < pts.Length; i++) Segs.Add(new BlackGhostSeg { A = pts[i], B = pts[i + 1], W = widths[i] });
        }

        private static void AddSeg(Vector2 a, Vector2 b, float w) => Segs.Add(new BlackGhostSeg { A = a, B = b, W = w });

        /// <summary>
        /// ghost2(): the shadow, the figure for its facing and the loose bandage ends. <paramref name="fray"/> 0..1 thins the
        /// limbs by up to 15 %, adds two loose ends and lengthens them all by up to 60 %; <paramref name="travel"/> is the
        /// walking direction on screen times how much it walks (the loose ends trail against it). False when nothing of it
        /// stands between lo and hi.
        /// </summary>
        internal static bool Draw(Vector2 feetAt, int face, in BlackGhostMotion m, float lowest, float highest, Vector2 sun, float strength,
            float fray, Vector2 travel)
        {
            Joints(feetAt, face, m, lowest, highest);
            float shown = Shown(lo, hi);
            if (shown <= 0f) return false;
            Segs.Clear();
            bool side = face == 1 || face == 3, north = face == 0, south = face == 2;

            // Shadow: a dark patch under the feet and the figure's cast shadow along the sun for the height shown.
            Vector2 v = sun * (Top * shown);
            float vl = v.magnitude;
            Sprite(new Vector2(feet.x, feet.y - 0.02f), 0.62f, 0.24f, Fade(ShadowBody, strength * 2.8f), soft, ShadowLayer);
            if (vl > 0.01f)
                Sprite(feet + v * 0.5f, vl + 0.35f, 0.46f, Fade(ShadowBody, strength * 2.1f), soft, ShadowLayer, -Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);

            float back = north ? 0.8f : 1f, thin = 1f - 0.15f * fray;
            float[] thighSrc = side ? ThighSide : ThighFront;
            for (int i = 0; i < 4; i++) thigh[i] = thighSrc[i] * thin;
            for (int i = 0; i < 3; i++) arm[i] = ArmWidths[i] * thin;
            Alt = PawnLayer;

            if (!side)
            {
                for (int i = 0; i < 2; i++)
                {
                    Part(legs[i], thigh, back * 0.92f, 140 + i * 50);
                    AddSegs(legs[i], thigh);
                }
                if (north)
                {
                    // From behind the claws and arms are behind the body.
                    for (int i = 0; i < 2; i++) Claws(S(arms[i][1]), S(arms[i][2]), ClawLen, flex * (i == 1 ? 1f : -1f), back * 0.8f);
                    DrawArms(false, back);
                }
                Torso(MapS(torsoL), MapS(torsoR), back);
                if (north)
                {
                    Vector2[] spine = Buf(4);
                    spine[0] = S(new Vector2(0f, 0.95f));
                    spine[1] = S(new Vector2(0.01f, 1.12f));
                    spine[2] = S(new Vector2(0f, 1.30f));
                    spine[3] = S(new Vector2(0f, 1.45f));
                    Trail(spine, 0.035f, Fade(InkEdge, 0.9f), Next());
                }
                AddSeg(torsoL[0], torsoL[3], 0.06f);
                AddSeg(torsoR[0], torsoR[3], 0.06f);
                if (!north) DrawArms(south, back);
                Part(neck, NeckWidths, back, 300);
                bool headShown = hi >= head.y;
                if (headShown && south) HeadFront(m.Seconds);
                if (headShown && north) HeadBack(back);
                AddSegs(neck, NeckWidths);
                AddSeg(H(FrontEdge[1]), H(FrontEdge[4]), 0.05f);
                AddSeg(H(new Vector2(-FrontEdge[1].x, FrontEdge[1].y)), H(new Vector2(-FrontEdge[4].x, FrontEdge[4].y)), 0.05f);
            }
            else
            {
                // Far limbs first and darker, then torso, near leg, neck, head, near arm last.
                Part(legs[0], thigh, 0.68f, 140);
                AddSegs(legs[0], thigh);
                Part(arms[0], arm, 0.68f, 40);
                Claws(S(arms[0][1]), S(arms[0][2]), ClawLen, -flex, 0.68f);
                Torso(MapS(torsoBack), MapS(torsoFront), 1f);
                AddSeg(torsoBack[0], torsoBack[2], 0.06f);
                AddSeg(torsoFront[0], torsoFront[2], 0.06f);
                Part(legs[1], thigh, 1f, 190);
                AddSegs(legs[1], thigh);
                Part(neck, NeckWidths, 1f, 300);
                AddSegs(neck, NeckWidths);
                if (hi >= head.y) HeadSide(m.Seconds);
                AddSeg(H(SideTop[0]), H(SideTop[5]), 0.06f);
                Part(arms[1], arm, 1f, 90);
                Claws(S(arms[1][1]), S(arms[1][2]), ClawLen, flex, 1f);
                AddSegs(arms[0], arm);
                AddSegs(arms[1], arm);
            }

            LooseEnds(side, back, fray, travel, m);
            return true;
        }

        private static void DrawArms(bool claws, float back)
        {
            for (int i = 0; i < 2; i++)
            {
                Part(arms[i], arm, back, 40 + i * 50);
                if (claws) Claws(S(arms[i][1]), S(arms[i][2]), ClawLen, flex * (i == 1 ? 1f : -1f), back);
                AddSegs(arms[i], arm);
            }
        }

        /// <summary>
        /// Loose bandage ends: 3, and 2 more as it frays (from 0.3 and 0.6 of the fray, over 0.25 each); they hang, trail
        /// against the walk and ripple.
        /// </summary>
        private static void LooseEnds(bool side, float back, float fray, Vector2 travel, in BlackGhostMotion m)
        {
            float t = m.Seconds, walk = m.Walk, dl = Mathf.Sqrt(travel.x * 1.1f * travel.x * 1.1f + (1f + travel.y * 0.6f) * (1f + travel.y * 0.6f));
            for (int i = 0; i < 5; i++)
            {
                Vector2 q;
                float len;
                if (side)
                {
                    switch (i)
                    {
                        case 0: q = Vector2.Lerp(arms[1][1], arms[1][2], 0.45f); len = 0.42f; break;
                        case 1: q = torsoBack[1]; len = 0.40f; break;
                        case 2: q = H(new Vector2(-0.05f, 0.08f)); len = 0.34f; break;
                        case 3: q = legs[1][1]; len = 0.28f; break;
                        default: q = arms[0][1]; len = 0.30f; break;
                    }
                }
                else
                {
                    switch (i)
                    {
                        case 0: q = Vector2.Lerp(arms[0][1], arms[0][2], 0.45f); len = 0.42f; break;
                        case 1: q = arms[1][1]; len = 0.30f; break;
                        case 2: q = torsoL[1]; len = 0.40f; break;
                        case 3: q = legs[1][1]; len = 0.28f; break;
                        default: q = torsoR[3]; len = 0.34f; break;
                    }
                }
                float grown = i < 3 ? 1f : Smooth((fray - (i == 3 ? 0.3f : 0.6f)) / 0.25f);
                if (grown <= 0f || q.y < lo || q.y > hi) continue;
                float sway = 0.25f * Mathf.Sin(t * 1.7f + i * 2.1f) * (1f - walk * 0.5f);
                var dir = new Vector2((-travel.x * 1.1f + sway) / dl, (-1f - travel.y * 0.6f) / dl);
                LooseEnd(S(q), dir, len * (1f + 0.6f * fray) * grown, 0.05f, t, i * 1.9f, 0.04f + 0.03f * walk + 0.04f * fray,
                    Fade(LooseBandage, 0.9f * back));
            }
        }

        /// <summary>One limb: dark outline, body, the sunlit east face, then the wraps.</summary>
        private static void Part(Vector2[] pts, float[] widths, float tone, int seed)
        {
            Vector2[] P = MapS(pts);
            if (!LimbBand(P, widths, InkEdge, Next(), 0.04f)) return;
            LimbBand(P, widths, Color.Lerp(InkEdge, InkBody, tone), Next());
            LimbBand(P, widths, Color.Lerp(InkEdge, InkLit, tone), Next(), 0f, true, 0.2f, 0.85f);
            Wraps(P, widths, tone, seed);
        }

        /// <summary>
        /// Bandage wraps along a limb (screen points): bands at uneven spacing (0.085-0.205 cells), most tilted one way so
        /// they spiral, a few the other way so they cross, about half broken off short of an edge.
        /// </summary>
        private static void Wraps(Vector2[] P, float[] widths, float tone, int seed)
        {
            int n = 0;
            for (int i = 0; i + 1 < P.Length; i++)
            {
                Vector2 A = P[i], d = P[i + 1] - A;
                float L = d.magnitude;
                if (L < 0.03f) continue;
                float tx = d.x / L, tz = d.y / L, nx = -tz, nz = tx;
                for (float s = R(seed + i * 7) * 0.08f; s < L; s += 0.085f + R(seed + n * 13 + 5) * 0.12f, n++)
                {
                    float r1 = R(seed + n * 13 + 1), r2 = R(seed + n * 13 + 2), r3 = R(seed + n * 13 + 3), r4 = R(seed + n * 13 + 4);
                    float f = s / L, w = Mathf.Lerp(widths[i], widths[Mathf.Min(i + 1, widths.Length - 1)], f) * 0.5f;
                    Vector2 c = A + d * f;
                    float tiltW = (r1 - 0.25f) * w * 1.3f, a0 = r2 < 0.25f ? r2 * 1.6f : 0f, a1 = r3 < 0.25f ? 0.6f + r3 * 1.6f : 1f;
                    Vector2 At(float k)
                    {
                        float x = Mathf.Lerp(-1f, 1f, k);
                        return new Vector2(c.x + nx * w * 0.95f * x + tx * tiltW * x, c.y + nz * w * 0.95f * x + tz * tiltW * x);
                    }
                    Vector2 mid = At((a0 + a1) / 2f);
                    Trail(Three(At(a0), new Vector2(mid.x - tx * 0.02f, mid.y - tz * 0.02f), At(a1)), 0.026f + r4 * 0.02f,
                        Fade(Bandage, tone * (0.45f + r4 * 0.35f)), Next());
                }
            }
        }

        /// <summary>
        /// The torso between two screen edges (bottom to top): outline, body, lit east side, two straps crossing over the
        /// chest and broken bands round the waist and ribs.
        /// </summary>
        private static void Torso(Vector2[] Lp, Vector2[] Rp, float tone)
        {
            int n = Lp.Length;
            Vector2[] E = Buf(n), W = Buf(n);
            for (int i = 0; i < n; i++)
            {
                bool leftEast = Lp[i].x > Rp[i].x;
                E[i] = leftEast ? Lp[i] : Rp[i];
                W[i] = leftEast ? Rp[i] : Lp[i];
            }
            if (E[n - 1].y - E[0].y < 1e-4f) return;
            Vector2[] We = Buf(n), Ee = Buf(n);
            for (int i = 0; i < n; i++)
            {
                We[i] = new Vector2(W[i].x - 0.024f, W[i].y);
                Ee[i] = new Vector2(E[i].x + 0.024f, E[i].y);
            }
            Band(We, Ee, InkEdge, Next());
            Band(W, E, Color.Lerp(InkEdge, InkBody, tone), Next());
            Band(Across(W, E, 0.58f), Across(W, E, 0.94f), Color.Lerp(InkEdge, InkLit, tone), Next());
            float top = Mathf.Min(n - 1, 3) - 0.15f;
            Vector2 At(float f, float k) => Vector2.Lerp(PolyAt(W, f), PolyAt(E, f), k);
            Trail(Three(At(top, 0.08f), At(top * 0.55f, 0.5f), At(0.35f, 0.9f)), 0.05f, Fade(Bandage, 0.72f * tone), Next());
            Trail(Three(At(top, 0.92f), At(top * 0.5f, 0.5f), At(0.2f, 0.12f)), 0.045f, Fade(Bandage, 0.58f * tone), Next());
            for (int j = 0; j < 4; j++)
            {
                float f = WaistBands[j * 3], a = WaistBands[j * 3 + 1], b = WaistBands[j * 3 + 2];
                Vector2 A = At(f, a), B = At(f, b), M = At(f, (a + b) / 2f);
                Trail(Three(A, new Vector2(M.x, M.y - 0.03f), B), 0.034f, Fade(Bandage, (0.45f + 0.12f * (j % 3)) * tone), Next());
            }
        }

        // Broken bands round the waist and ribs: (index along the edges, start and end share across).
        private static readonly float[] WaistBands = { 0.25f, 0.05f, 0.8f, 0.75f, 0.3f, 1f, 1.35f, 0f, 0.55f, 1.8f, 0.45f, 0.98f };

        /// <summary>
        /// claws2(): six long claws fanning from the wrist (screen points), each curling in toward the middle of the hand.
        /// <paramref name="flexK"/> -1..1 opens and closes the fan by 30 %.
        /// </summary>
        internal static void Claws(Vector2 E, Vector2 W, float len, float flexK, float tone)
        {
            float bse = Mathf.Atan2(W.y - E.y, W.x - E.x), spread = 0.15f * (1f + 0.3f * flexK);
            for (int i = 0; i < 6; i++)
            {
                float a = bse + (i - 2.5f) * spread, l = len * (0.82f + (i == 0 || i == 5 ? -0.18f : R(i + 3) * 0.18f));
                var mid = new Vector2(W.x + Mathf.Cos(a) * l * 0.55f, W.y + Mathf.Sin(a) * l * 0.55f);
                float b = a - (i < 2.5f ? -1f : 1f) * 0.35f;
                var tip = new Vector2(mid.x + Mathf.Cos(b) * l * 0.45f, mid.y + Mathf.Sin(b) * l * 0.45f);
                Taper(Three(W, mid, tip), 0.068f, 0.012f, InkEdge, Next());
                Taper(Three(W, mid, tip), 0.046f, 0.004f, Color.Lerp(InkEdge, InkBody, tone), Next());
            }
        }

        /// <summary>Teeth along a jaw edge (screen points): small pale triangles pointing to side dir (+1 left of the line's direction, -1 right).</summary>
        private static void TeethAlong(Vector2[] P, float dir, float size, float alpha)
        {
            int k = 0;
            for (int i = 0; i + 1 < P.Length; i++)
            {
                Vector2 A = P[i], d = P[i + 1] - A;
                float L = d.magnitude;
                if (L < 0.01f) continue;
                int count = Mathf.Max(1, Round(L / (size * 1.1f)));
                float nx = -d.y / L * dir, nz = d.x / L * dir;
                for (int j = 0; j < count; j++, k++)
                {
                    float f0 = j / (float)count, f1 = (j + 1) / (float)count, fm = (f0 + f1) / 2f, len = size * (0.8f + R(k * 5 + i) * 0.5f);
                    var tip = new Vector2(A.x + d.x * fm + nx * len, A.y + d.y * fm + nz * len);
                    Vector2[] a = Buf(2), b = Buf(2);
                    a[0] = A + d * f0; a[1] = tip;
                    b[0] = A + d * f1; b[1] = tip;
                    Band(a, b, Fade(Teeth, alpha), Next());
                }
            }
        }

        /// <summary>
        /// The forked tongue: from root along angle ang (head-local (u, h)), len long, forked at 74 %, wriggling.
        /// </summary>
        private static void Tongue(Vector2 root, float ang, float len, float t)
        {
            if (len < 0.01f) return;
            float du = Mathf.Cos(ang), dh = Mathf.Sin(ang), fork = len * 0.74f;
            Vector2 At(float s, float off) => HS(new Vector2(root.x + du * s - dh * off, root.y + dh * s + du * off));
            float Wig(float s) => Mathf.Sin(s * 14f - t * 20f) * 0.012f * (s / len);
            Vector2[] main = Buf(6);
            for (int i = 0; i < 6; i++) main[i] = At(fork * i / 5f, Wig(fork * i / 5f));
            Taper(main, 0.07f, 0.05f, InkEdge, Next());
            Taper(main, 0.05f, 0.03f, TongueRed, Next());
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Vector2[] pts = Three(main[5], At((fork + len) / 2f, Wig(fork) + sg * len * 0.04f), At(len, Wig(fork) + sg * len * 0.1f));
                Taper(pts, 0.042f, 0.016f, InkEdge, Next());
                Taper(pts, 0.026f, 0.006f, TongueRed, Next());
            }
        }

        /// <summary>A saliva strand between screen points A and B, sagging sag cells.</summary>
        private static void SpitStrand(Vector2 A, Vector2 B, float sag, float alpha)
        {
            Vector2[] pts = Buf(5);
            for (int i = 0; i < 5; i++)
            {
                float f = i * 0.25f, s = Mathf.Sin(f * Mathf.PI);
                pts[i] = new Vector2(Mathf.Lerp(A.x, B.x, f) + s * sag * 0.4f, Mathf.Lerp(A.y, B.y, f) - s * sag);
            }
            Trail(pts, 0.018f, Fade(Spit, alpha), Next());
        }

        /// <summary>A loose bandage end from a screen point along dir, len long, a wave running down it and a slow curl to one side.</summary>
        private static void LooseEnd(Vector2 root, Vector2 dir, float len, float w, float t, float phase, float amp, Color colour)
        {
            float nx = -dir.y, nz = dir.x, curl = 0.10f * Mathf.Sin(t * 0.9f + phase * 1.7f) * len;
            Vector2[] pts = Buf(9);
            for (int i = 0; i <= 8; i++)
            {
                float s = i / 8f, off = amp * s * Mathf.Sin(s * 5.5f - t * 7f + phase) + curl * s * s;
                pts[i] = new Vector2(root.x + dir.x * len * s + nx * off, root.y + dir.y * len * s + nz * off);
            }
            Taper(pts, w + 0.025f, w * 0.45f + 0.02f, Fade(InkEdge, colour.a), Next());
            Taper(pts, w, w * 0.45f, colour, Next());
        }

        /// <summary>
        /// Front head: first the parts under the hood (mouth inside, lower jaw, lower teeth, saliva, tongue), then the hood
        /// with its lit east side, a ridge and two wraps, then the upper teeth along its lower edges.
        /// </summary>
        private static void HeadFront(float t)
        {
            float ta = Mathf.Clamp01((jaw - 0.1f) / 0.3f);
            Vector2[] upS = MapHS(MouthFront);
            if (jaw > 0.02f)
            {
                Vector2[] low = Buf(5), lowS = Buf(5), rimS = Buf(5);
                for (int i = 0; i < 5; i++)
                {
                    low[i] = new Vector2(MouthFront[i].x * (1f - 0.12f * jaw), MouthFront[i].y - DropFront[i] * jaw);
                    lowS[i] = HS(low[i]);
                    rimS[i] = HS(new Vector2(low[i].x, low[i].y - 0.045f));
                }
                Band(upS, lowS, Maw, Next());
                Vector2[] lowUp = Buf(5), rimDown = Buf(5);
                for (int i = 0; i < 5; i++)
                {
                    lowUp[i] = new Vector2(lowS[i].x, lowS[i].y + 0.01f);
                    rimDown[i] = new Vector2(rimS[i].x, rimS[i].y - 0.014f);
                }
                Band(lowUp, rimDown, InkEdge, Next());
                Band(lowS, rimS, InkBody, Next());
                if (ta > 0f) TeethAlong(lowS, 1f, 0.03f, ta);
                float sa = Mathf.Clamp01((jaw - 0.4f) / 0.3f) * 0.5f;
                if (sa > 0f)
                {
                    SpitStrand(upS[1], lowS[1], 0.03f, sa);
                    SpitStrand(upS[3], lowS[3], 0.03f, sa);
                }
            }
            if (tongue > 0.02f) Tongue(new Vector2(0f, -0.115f - 0.07f * jaw), -Mathf.PI / 2f + 0.12f * Mathf.Sin(t * 11f), 0.34f * tongue, t);
            Vector2[] Lp = MapHS(FrontEdge), Rp = MapHS(FrontEdge, true);
            Band(Grown(Lp, 1.09f), Grown(Rp, 1.09f), InkEdge, Next());
            Band(Lp, Rp, InkBody, Next());
            Band(Across(Lp, Rp, 0.55f), Across(Lp, Rp, 0.93f), InkLit, Next());
            Trail(Three(HS(0f, 0.33f), HS(0.005f, 0.14f), HS(0f, -0.06f)), 0.03f, Fade(InkLit, 0.7f), Next());
            Trail(Three(HS(-0.24f, 0.22f), HS(-0.05f, 0.28f), HS(0.16f, 0.31f)), 0.03f, Fade(Bandage, 0.55f), Next());
            Trail(Three(HS(-0.12f, 0.04f), HS(0.03f, 0.11f), HS(0.19f, 0.17f)), 0.026f, Fade(Bandage, 0.45f), Next());
            TeethAlong(upS, -1f, Mathf.Lerp(0.024f, 0.034f, ta), Mathf.Lerp(0.8f, 1f, ta));
        }

        /// <summary>Back head: the hood from behind, its point (the snout) away from the viewer, and a groove.</summary>
        private static void HeadBack(float tone)
        {
            Vector2[] Lp = MapHS(BackEdge), Rp = MapHS(BackEdge, true);
            Band(Grown(Lp, 1.09f), Grown(Rp, 1.09f), InkEdge, Next());
            Band(Lp, Rp, Color.Lerp(InkEdge, InkBody, tone), Next());
            Band(Across(Lp, Rp, 0.6f), Across(Lp, Rp, 0.93f), Color.Lerp(InkEdge, InkLit, tone), Next());
            Trail(Three(HS(0f, 0.32f), HS(0f, 0.12f), HS(0f, -0.08f)), 0.03f, Fade(InkEdge, 0.9f), Next());
            Trail(Three(HS(-0.26f, 0.05f), HS(0f, 0.10f), HS(0.24f, 0.16f)), 0.03f, Fade(Bandage, 0.5f * tone), Next());
        }

        /// <summary>
        /// Side head: the lower jaw swinging on its hinge (up to 31 degrees), the mouth inside and the tongue, then the wedge
        /// of the upper head over them, then the teeth (the upper row shows even with the mouth shut) and saliva.
        /// </summary>
        private static void HeadSide(float t)
        {
            float a = -jaw * JawSwing, c = Mathf.Cos(a), s = Mathf.Sin(a);
            Vector2 Rot(Vector2 q)
            {
                float du = q.x - Hinge.x, dh = q.y - Hinge.y;
                return new Vector2(Hinge.x + du * c - dh * s, Hinge.y + du * s + dh * c);
            }
            Vector2[] jt = Buf(5), jtS = Buf(5), jbS = Buf(5), upS = Buf(5);
            for (int i = 0; i < 5; i++)
            {
                jt[i] = Rot(JawTop[i]);
                jtS[i] = HS(jt[i]);
                jbS[i] = HS(Rot(JawBottom[i]));
                upS[i] = HS(SideMouth[i + 1]);
            }
            if (jaw > 0.02f) Band(upS, jtS, Maw, Next());
            Band(Grown(jtS, 1.08f), Grown(jbS, 1.08f), InkEdge, Next());
            Band(jtS, jbS, InkBody, Next());
            if (tongue > 0.02f || jaw > 0.3f) Tongue(new Vector2(0.06f, -0.06f), a * 0.5f + 0.05f * Mathf.Sin(t * 9f), 0.12f + 0.42f * tongue, t);
            Vector2[] top = MapHS(SideTop), mouth = MapHS(SideMouth);
            Band(Grown(mouth, 1.08f), Grown(top, 1.08f), InkEdge, Next());
            Band(mouth, top, InkBody, Next());
            Band(Across(mouth, top, 0.6f), Across(mouth, top, 0.92f), InkLit, Next());
            for (int j = 0; j < 2; j++)
            {
                int i = j == 0 ? 2 : 4;
                Trail(Three(mouth[i], new Vector2((mouth[i].x + top[i].x) / 2f - 0.015f * mirror, (mouth[i].y + top[i].y) / 2f), top[i]), 0.028f,
                    Fade(Bandage, 0.5f), Next());
            }
            float ta = Mathf.Clamp01((jaw - 0.1f) / 0.3f);
            TeethAlong(upS, -mirror, Mathf.Lerp(0.024f, 0.034f, ta), Mathf.Lerp(0.8f, 1f, ta));
            if (ta > 0f) TeethAlong(jtS, mirror, 0.03f, ta);
            float sa = Mathf.Clamp01((jaw - 0.4f) / 0.3f) * 0.5f;
            if (sa > 0f)
            {
                SpitStrand(upS[1], jtS[1], 0.025f, sa);
                SpitStrand(upS[2], jtS[2], 0.025f, sa);
            }
        }

        /// <summary>
        /// chunks(): bigger pieces breaking off as its time runs out (k 0..1, up to 26): each clings to the edge for the
        /// first 30 % of its life, then peels off and drifts up slowly, tumbling.
        /// </summary>
        internal static void Chunks(Vector2 feetAt, float t, float k, float lowest, float highest, float mirrorX, float layer)
        {
            if (Segs.Count == 0) return;
            int count = Round(26f * k);
            for (int i = 0; i < count; i++)
            {
                double life = 1.3 + Rd(i * 4.7 + 9) * 0.9, ph = t / life + Rd(i * 3.3 + 2);
                int cyc = (int)System.Math.Floor(ph), sd = i * 173 + cyc * 23 + 11;
                float u = (float)(ph - cyc);
                BlackGhostSeg sg = Segs[(int)System.Math.Floor(Rd(sd) * Segs.Count) % Segs.Count];
                float tt = R(sd + 1), side = R(sd + 2) < 0.5f ? -1f : 1f;
                float h0 = Mathf.Lerp(sg.A.y, sg.B.y, tt);
                if (h0 < lowest || h0 > highest) continue;
                float du = sg.B.x - sg.A.x, dh = sg.B.y - sg.A.y, L = Mathf.Sqrt(du * du + dh * dh);
                if (L == 0f) L = 1f;
                float u0 = Mathf.Lerp(sg.A.x, sg.B.x, tt) + (-dh / L) * side * sg.W * 0.45f, go = Smooth((u - 0.3f) / 0.7f);
                var at = new Vector2(feetAt.x + (u0 + ((-dh / L) * side * 0.10f + (R(sd + 3) - 0.5f) * 0.2f) * go) * mirrorX,
                    feetAt.y + (h0 + 0.35f * go) * Stand);
                Shard(i + 1, at, layer + i * 0.0002f, (0.07f + 0.06f * R(sd + 5)) * (1f - go * 0.4f), R(sd + 6) * 360f + go * 200f,
                    Fade(Flake, 1f - go * go));
            }
        }
    }
}
