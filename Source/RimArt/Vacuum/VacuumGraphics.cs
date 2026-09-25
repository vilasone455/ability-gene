using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// The aim frame of a Vacuum picture: cells along the cast direction, across it, and h cells up.
    /// <see cref="Place"/> is where a point is drawn (height shifted north), <see cref="Cast"/> where
    /// its shadow falls.
    /// </summary>
    public readonly struct VacuumFrame
    {
        public readonly float ca, sa;
        public readonly Vector2 sun;

        public VacuumFrame(float aimDegrees, Vector2 sun)
        {
            float a = aimDegrees * Mathf.Deg2Rad;
            ca = Mathf.Cos(a);
            sa = Mathf.Sin(a);
            this.sun = sun;
        }

        public Vector2 Place(Vector2 at, float along, float across, float h = 0f) =>
            new Vector2(at.x + along * ca - across * sa, at.y + along * sa + across * ca + h * SixPathsHeight.Lift);

        public Vector2 Cast(Vector2 at, float along, float across, float h = 0f) =>
            new Vector2(at.x + along * ca - across * sa + sun.x * h, at.y + along * sa + across * ca + sun.y * h);
    }

    /// <summary>How the weapon is posed in one frame: see <see cref="VacuumGraphics.Weapon"/>.</summary>
    public struct VacuumPose
    {
        /// <summary>0..1, how far the canister is out of the floor (0 = the wand alone, the hose coiled at the hip).</summary>
        public float Present;
        /// <summary>0..1, dirt ring and dust at the canister's base while it rises or sinks.</summary>
        public float Churn;
        /// <summary>Extra size from the fullness: <see cref="VacuumGraphics.SwellFor"/> of the kg inside.</summary>
        public float Swell;
        /// <summary>Momentary swell as a thing lands (negative squashes).</summary>
        public float Pulse;
        public float Blink, MouthOpen, Slack;
        /// <summary>Where things are in the hose, 0 at the wand to 1 at the canister: the first <see cref="BulgeCount"/> of <see cref="Bulges"/>.</summary>
        public float[] Bulges;
        public int BulgeCount;
    }

    /// <summary>What <see cref="VacuumGraphics.Weapon"/> drew, for the pieces that follow it.</summary>
    public struct VacuumParts
    {
        /// <summary>The canister's ground centre, radius and height; its drawn top and its altitude.</summary>
        public Vector2 C;
        public float R, H, Top, Layer;
        /// <summary>The wand's head on the ground and as drawn at hand height.</summary>
        public Vector2 TipG, TipS;
    }

    /// <summary>
    /// The drawing shared by the Vacuum's Suck, Spit and Digest: the conjured canister with its face,
    /// the hose, the hip coil, the wand with its head, the churn of dirt when the canister rises out of
    /// or sinks into the floor, and the sketches' stand-in chunk and rifle. The port of
    /// Tools/VfxLab/web/sketches/lib/vacuum.js; its numbers are that file's. The canister is a level
    /// cylinder and the hose and wand turn with the aim, so nothing here has a per-facing method.
    /// Every routine takes ages and amounts and keeps no state. Call VfxDraw.Begin with the effect's
    /// ground point first.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class VacuumGraphics
    {
        internal static readonly Color Body = new Color(0.035f, 0.028f, 0.050f);
        internal static readonly Color Skin = new Color(0.83f, 0.70f, 0.54f), Pale = new Color(1f, 0.96f, 0.85f), Dust = new Color(0.80f, 0.74f, 0.63f);
        internal static readonly Color Can = new Color(0.12f, 0.30f, 0.33f), CanLit = new Color(0.22f, 0.46f, 0.48f),
            CanDark = new Color(0.06f, 0.16f, 0.18f), CanRim = new Color(0.34f, 0.60f, 0.62f);
        internal static readonly Color Hose = new Color(0.26f, 0.26f, 0.30f), HoseLit = new Color(0.44f, 0.44f, 0.50f), HoseDark = new Color(0.07f, 0.07f, 0.09f);
        internal static readonly Color EyeWhite = new Color(0.97f, 0.97f, 0.95f), Pupil = new Color(0.05f, 0.05f, 0.06f);
        internal static readonly Color MouthIn = new Color(0.32f, 0.06f, 0.08f), Lip = new Color(0.05f, 0.05f, 0.06f), Tooth = new Color(0.95f, 0.93f, 0.85f);
        internal static readonly Color Stone = new Color(0.40f, 0.39f, 0.41f), StoneTop = new Color(0.57f, 0.56f, 0.59f);
        internal static readonly Color Wood = new Color(0.38f, 0.23f, 0.12f), Steel = new Color(0.32f, 0.34f, 0.38f), Blood = new Color(0.44f, 0.06f, 0.06f);
        /// <summary>The stand-in lump for a thing whose own graphic cannot be drawn in flight (a corpse).</summary>
        internal static readonly Color Flesh = new Color(0.45f, 0.30f, 0.26f), FleshTop = new Color(0.58f, 0.42f, 0.36f);

        /// <summary>The wand's height when lifted, cells.</summary>
        internal const float HandH = 0.5f;
        /// <summary>Canister radius and height, cells, and where it rises from the caster's feet in the aim frame.</summary>
        internal const float CanR = 0.45f, CanH = 0.65f, CanAlong = -0.55f, CanAcross = 0.85f;
        /// <summary>The lifted wand along the aim from the caster's feet.</summary>
        internal const float NozzleBack = 0.30f, NozzleTip = 0.80f;
        internal const float HoseW = 0.085f;
        /// <summary>One thing takes this long to run the hose.</summary>
        internal const float Bulge = 0.35f;
        /// <summary>The wand alone, the canister rising, sinking, the wand alone again, seconds.</summary>
        internal const float Lead = 0.25f, Rise = 0.25f, Sink = 0.3f, Tail = 0.25f;
        /// <summary>The picture's full canister: six swell steps at this many kg. The rule's capacity is an XML field; the kit passes kg scaled to it.</summary>
        internal const float Capacity = 100f, FullSwell = 6f;

        internal static readonly float Y = AltitudeLayer.MoteOverhead.AltitudeFor();
        internal static readonly float ShadowLayer = AltitudeLayer.Shadows.AltitudeFor();
        internal static readonly float PawnLayer = AltitudeLayer.Pawn.AltitudeFor();

        private const int MostPoints = VfxDraw.MostPoints;
        // One line of points and its half-widths, filled by a routine and handed to Tube. Only ever
        // used for one tube at a time; Tube copies them into a strip from the pool.
        private static readonly Vector2[] P = new Vector2[MostPoints];
        private static readonly float[] W = new float[MostPoints];
        private static readonly Vector2[] A = new Vector2[MostPoints], C = new Vector2[MostPoints];
        // The hose's points and its shadow's, kept apart from P because three tubes are drawn from them.
        private static readonly Vector2[] HosePts = new Vector2[32], HoseShadow = new Vector2[32];

        internal static float SwellFor(float kg) => FullSwell * kg / Capacity;

        internal static float Bump(float x) => x >= 0f && x <= 1f ? Mathf.Max(0f, Mathf.Sin(x * Mathf.PI)) : 0f;

        internal static void Disc(Vector2 at, float altitude, float width, float depth, Color colour) =>
            DrawMesh(disc, at, altitude, width, depth, 0f, colour, solid);

        /// <summary>A quad whose long side points along <paramref name="degrees"/> (0 east, 90 north).</summary>
        internal static void Rect(Vector2 at, float length, float width, float degrees, Color colour, float altitude) =>
            Sprite(at, length, width, colour, solid, altitude, -degrees);

        /// <summary>
        /// A ribbon through the first <paramref name="count"/> points of <paramref name="pts"/>, W[i]
        /// cells to each side scaled by <paramref name="lo"/> and <paramref name="hi"/> (-1 and 1 is the
        /// full width). Widths are measured across the line's own direction on screen.
        /// </summary>
        private static void Tube(Vector2[] pts, int count, Color colour, float altitude, float lo = -1f, float hi = 1f)
        {
            if (count < 2 || colour.a <= 0.001f) return;
            int n = count - 1;
            Sides(count, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i <= n; i++)
            {
                Vector2 pr = pts[Mathf.Max(0, i - 1)], nx = pts[Mathf.Min(n, i + 1)];
                float dx = nx.x - pr.x, dz = nx.y - pr.y, L = Mathf.Sqrt(dx * dx + dz * dz);
                if (L <= 0f) L = 1f;
                dx /= L; dz /= L;
                float w = W[i];
                Vector2 q = pts[i];
                a[i] = new Vector2(q.x - dz * w * lo, q.y + dx * w * lo);
                b[i] = new Vector2(q.x - dz * w * hi, q.y + dx * w * hi);
            }
            Strip(a, b, colour, solid, altitude);
        }

        /// <summary>A band between the first <paramref name="count"/> points of A and C.</summary>
        private static void Band(int count, Color colour, float altitude)
        {
            if (count < 2 || colour.a <= 0.001f) return;
            Sides(count, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i < count; i++) { a[i] = A[i]; b[i] = C[i]; }
            Strip(a, b, colour, solid, altitude);
        }

        /// <summary>A two-point band from a0-a1 to b0-b1, the sketch's band() with two corners a side.</summary>
        internal static void Quad(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1, Color colour, float altitude)
        {
            A[0] = a0; A[1] = a1; C[0] = b0; C[1] = b1;
            Band(2, colour, altitude);
        }

        /// <summary>The sketch's trail(): a line through <paramref name="count"/> points of P, widest in the middle.</summary>
        internal static void Trail(Vector2[] pts, int count, float width, Color colour, float altitude)
        {
            if (count < 2 || colour.a <= 0.001f) return;
            for (int i = 0; i < count; i++)
            {
                Vector2 prev = pts[Mathf.Max(0, i - 1)], next = pts[Mathf.Min(count - 1, i + 1)];
                float dx = next.x - prev.x, dz = next.y - prev.y, len = Mathf.Sqrt(dx * dx + dz * dz);
                if (len <= 0f) len = 1f;
                float w = Mathf.Sin(i / (float)(count - 1) * Mathf.PI) * width / 2f;
                A[i] = new Vector2(pts[i].x - dz / len * w, pts[i].y + dx / len * w);
                C[i] = new Vector2(pts[i].x + dz / len * w, pts[i].y - dx / len * w);
            }
            Band(count, colour, altitude);
        }

        private static float HoseWidth(in VacuumPose pose, float u)
        {
            float w = HoseW;
            for (int i = 0; i < pose.BulgeCount; i++)
            {
                float k = (u - pose.Bulges[i]) / 0.07f;
                w += HoseW * 1.1f * Mathf.Exp(-k * k);
            }
            return w;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>
        /// The whole weapon for one frame: the canister with its face, the hose, the hip coil and the
        /// wand. At rest the wand hangs at the right side with its floor head on the ground ahead and
        /// the hose coiled at the left hip; as the canister rises the wand lifts to hand height and
        /// points along the aim, and the hose grows out to the canister's top.
        /// </summary>
        internal static VacuumParts Weapon(in VacuumFrame f, Vector2 caster, in VacuumPose pose, float strength)
        {
            float present = pose.Present, churn = pose.Churn, swell = pose.Swell, pulse = pose.Pulse;
            float R = CanR * (1f + 0.05f * swell + 0.10f * pulse), H = CanH * present * (1f + 0.04f * swell + 0.08f * pulse);
            Vector2 Cn = f.Place(caster, CanAlong, CanAcross);
            float canLayer = PawnLayer + (Cn.y < caster.y ? 0.012f : -0.012f);
            float lift = Smooth(present);

            if (churn > 0f)
            {
                Circle(Cn, CanR + 0.08f, 0.7f * churn, Floor + 0.02f, HoseDark);
                Sprite(Cn, CanR * 2.6f, CanR * 2.0f, Fade(Body, 0.35f * churn), soft, Floor + 0.01f);
                for (int k = 0; k < 8; k++)
                {
                    float th = Rand(k + 400) * Mathf.PI * 2f, d = CanR + 0.1f + churn * 0.35f * (0.5f + Rand(k + 410));
                    Sprite(new Vector2(Cn.x + Mathf.Cos(th) * d, Cn.y + Mathf.Sin(th) * d * 0.7f + churn * 0.1f * SixPathsHeight.Lift),
                        0.28f, 0.22f, Fade(Dust, 0.6f * churn), PowerPoleGraphics.puff, Y - 0.04f + k * 0.0001f);
                }
            }
            float top = Cn.y + H * SixPathsHeight.Lift;
            if (present > 0f)
            {
                Sprite(new Vector2(Cn.x + f.sun.x * H * 0.6f, Cn.y + f.sun.y * H * 0.6f), R * 2.8f, R * 1.6f, Fade(Body, strength), soft, ShadowLayer);
                for (int i = 0; i <= 24; i++)
                {
                    float th = Mathf.PI + i / 24f * Mathf.PI, x = Cn.x + Mathf.Cos(th) * R, z = Cn.y + Mathf.Sin(th) * R;
                    A[i] = new Vector2(x, z);
                    C[i] = new Vector2(x, z + H * SixPathsHeight.Lift);
                }
                Band(25, CanDark, canLayer);
                for (int i = 0; i <= 8; i++)
                {
                    float th = Mathf.PI * 1.5f + (i / 8f - 0.5f) * 0.9f, x = Cn.x + Mathf.Cos(th) * R, z = Cn.y + Mathf.Sin(th) * R;
                    A[i] = new Vector2(x, z + H * SixPathsHeight.Lift * 0.06f);
                    C[i] = new Vector2(x, z + H * SixPathsHeight.Lift * 0.94f);
                }
                Band(9, Can, canLayer + 0.002f);
                Disc(new Vector2(Cn.x, top), canLayer + 0.004f, R, R, Can);
                Disc(new Vector2(Cn.x, top + R * 0.06f), canLayer + 0.005f, R * 0.82f, R * 0.82f, CanLit);
                Circle(new Vector2(Cn.x, top), R, 0.9f, canLayer + 0.006f, CanRim);
                Disc(new Vector2(Cn.x, top), canLayer + 0.007f, 0.10f, 0.10f, HoseDark);   // hose socket
                // Face on the south side: two eyes that blink, a mouth that opens.
                float eyeZ = Cn.y - R * 0.90f + H * SixPathsHeight.Lift * 0.66f, eyeH = 0.15f * (1f - 0.9f * pose.Blink);
                for (int e = 0; e < 2; e++)
                {
                    float sx = e == 0 ? -1f : 1f, ex = Cn.x + sx * R * 0.42f;
                    Disc(new Vector2(ex, eyeZ), canLayer + 0.008f + e * 0.0001f, 0.15f, eyeH, EyeWhite);
                    Disc(new Vector2(ex + f.ca * 0.03f, eyeZ + f.sa * 0.03f * SixPathsHeight.Lift), canLayer + 0.009f + e * 0.0001f, 0.07f, Mathf.Min(eyeH, 0.085f), Pupil);
                }
                float mZ = Cn.y - R + H * SixPathsHeight.Lift * 0.30f, mW = R * 1.15f, mH = 0.045f + 0.19f * pose.MouthOpen;
                Disc(new Vector2(Cn.x, mZ), canLayer + 0.0083f, mW / 2f + 0.02f, mH / 2f + 0.02f, Lip);
                Disc(new Vector2(Cn.x, mZ), canLayer + 0.0093f, mW / 2f, mH / 2f, MouthIn);
                for (int k = 0; k < 4; k++)
                {
                    float tx = Cn.x + (k - 1.5f) * mW * 0.22f, th = Mathf.Min(0.05f, mH * 0.45f);
                    Disc(new Vector2(tx, mZ + mH / 2f - th / 2f - 0.005f), canLayer + 0.010f + k * 0.0001f, 0.035f, th / 2f, Tooth);
                }
            }

            // The wand (aim frame: along, across, height) between its rest and its lifted pose.
            float wa0 = Lerp(0.10f, NozzleBack, lift), wa1 = Lerp(-0.26f, 0f, lift), wa2 = Lerp(0.55f, HandH, lift);   // the grip
            float wb0 = Lerp(0.62f, NozzleTip, lift), wb1 = Lerp(-0.42f, 0f, lift), wb2 = Lerp(0.04f, HandH, lift);    // the head
            // The hose: a curve from the grip (u = 0) to the canister's top (u = 1). While the canister
            // rises or sinks only the first `present` share of it exists.
            float p10 = (NozzleBack + CanAlong) / 2f - 0.1f, p11 = CanAcross * 0.55f + pose.Slack, p12 = HandH + 0.35f + pose.Slack * 0.3f;
            const int N = 30;
            int M = Mathf.Max(2, Mathf.FloorToInt(N * present + 0.5f));
            if (present > 0f)
            {
                for (int i = 0; i <= M; i++)
                {
                    float u = i / (float)N, w0 = (1f - u) * (1f - u), w1 = 2f * (1f - u) * u, w2 = u * u;
                    float q0 = w0 * wa0 + w1 * p10 + w2 * CanAlong, q1 = w0 * wa1 + w1 * p11 + w2 * CanAcross, q2 = w0 * wa2 + w1 * p12 + w2 * H;
                    HosePts[i] = f.Place(caster, q0, q1, q2);
                    HoseShadow[i] = f.Cast(caster, q0, q1, q2);
                }
                for (int i = 0; i <= M; i++) W[i] = HoseWidth(pose, i / (float)M);
                Tube(HoseShadow, M + 1, Fade(Body, strength * 0.8f), ShadowLayer + 0.0001f);
                for (int i = 0; i <= M; i++) W[i] += 0.025f;
                Tube(HosePts, M + 1, HoseDark, Y);
                for (int i = 0; i <= M; i++) W[i] -= 0.025f;
                Tube(HosePts, M + 1, Hose, Y + 0.002f);
                Tube(HosePts, M + 1, HoseLit, Y + 0.004f, 0.15f, 0.6f);
            }
            // At rest the hose is coiled at the left hip: two loops and a short run to the grip. The
            // loops unwind as the canister rises. When the hip is on the far side of the pawn (north on
            // screen) the coil hangs behind the body.
            if (lift < 1f)
            {
                const float c0 = -0.10f, c1 = 0.42f, c2 = 0.20f;
                float r0 = 0.22f * (1f - lift), cw = HoseW * 0.7f;
                Vector2 coilC = f.Place(caster, c0, c1, c2);
                float CL = coilC.y > caster.y + 0.05f ? PawnLayer - 0.03f : Y;
                // Where the coil draws at Y it shares altitudes with the hose under it and the wand over it:
                // the first loop goes a hair above the hose, the second a hair below the wand, the run
                // above the first loop's outline, the order the sketch drew them in.
                bool atY = CL == Y;
                for (int loop = 0; loop < 2; loop++)
                {
                    for (int i = 0; i <= 26; i++)
                    {
                        float th = i / 26f * Mathf.PI * 2f, r = r0 * (1f - loop * 0.30f - 0.06f * i / 26f);
                        P[i] = f.Place(caster, c0 + Mathf.Cos(th) * r, c1 + Mathf.Sin(th) * r * 0.85f, c2 + loop * 0.06f);
                    }
                    float nudge = atY ? (loop == 0 ? 0.0003f : -0.0003f) : 0f;
                    for (int i = 0; i <= 26; i++) W[i] = cw + 0.02f;
                    Tube(P, 27, HoseDark, CL + loop * 0.006f + nudge);
                    for (int i = 0; i <= 26; i++) W[i] = cw;
                    Tube(P, 27, Hose, CL + 0.002f + loop * 0.006f + nudge);
                    Tube(P, 27, HoseLit, CL + 0.004f + loop * 0.006f + nudge, 0.1f, 0.55f);
                }
                P[0] = f.Place(caster, wa0, wa1, wa2);
                P[1] = f.Place(caster, Lerp(0.02f, NozzleBack - 0.1f, lift), Lerp(0.04f, 0.12f, lift), 0.5f);
                P[2] = f.Place(caster, c0 + r0, c1 - r0 * 0.3f, c2);
                W[0] = W[1] = W[2] = HoseW + 0.025f;
                Tube(P, 3, HoseDark, CL - 0.002f);
                W[0] = W[1] = W[2] = HoseW;
                Tube(P, 3, Hose, CL + (atY ? 0.0005f : 0.0001f));
            }

            // The wand: a tube from the grip to the head, and the head, a wide flat bar across the wand
            // with a dark slot. At rest the bar is a floor head; lifted it is the mouth things pass.
            Vector2 wa = f.Place(caster, wa0, wa1, wa2), wb = f.Place(caster, wb0, wb1, wb2);
            Vector2 sa = f.Cast(caster, wa0, wa1, wa2), sb = f.Cast(caster, wb0, wb1, wb2);
            P[0] = sa; P[1] = sb;
            W[0] = 0.065f; W[1] = 0.09f;
            Tube(P, 2, Fade(Body, strength * 0.7f), ShadowLayer + 0.0002f);
            P[0] = wa; P[1] = Vector2.Lerp(wa, wb, 0.5f); P[2] = wb;
            W[0] = 0.065f + 0.025f; W[1] = Lerp(0.065f, 0.09f, 0.5f) + 0.025f; W[2] = 0.09f + 0.025f;
            Tube(P, 3, HoseDark, Y + 0.006f);
            W[0] = 0.065f; W[1] = Lerp(0.065f, 0.09f, 0.5f); W[2] = 0.09f;
            Tube(P, 3, Hose, Y + 0.008f);
            Tube(P, 3, HoseLit, Y + 0.010f, 0.15f, 0.6f);
            float headDeg = Mathf.Atan2(wb.y - wa.y, wb.x - wa.x) * Mathf.Rad2Deg + 90f;
            float headLen = Lerp(0.58f, 0.30f, lift), headWid = Lerp(0.17f, 0.13f, lift);
            Sprite(sb, headLen * 1.1f, headWid * 1.6f, Fade(Body, strength * 0.6f), soft, ShadowLayer + 0.0003f);
            Rect(wb, headLen + 0.04f, headWid + 0.04f, headDeg, HoseDark, Y + 0.012f);
            Rect(wb, headLen, headWid, headDeg, Hose, Y + 0.014f);
            Rect(new Vector2(wb.x, wb.y + headWid * 0.22f), headLen * 0.9f, headWid * 0.22f, headDeg, HoseLit, Y + 0.016f);
            float fwd = (headDeg - 90f) * Mathf.Deg2Rad;
            Rect(new Vector2(wb.x + Mathf.Cos(fwd) * headWid * 0.18f, wb.y + Mathf.Sin(fwd) * headWid * 0.18f), headLen * 0.82f, headWid * 0.38f, headDeg, Lip, Y + 0.018f);

            return new VacuumParts
            {
                C = Cn, R = R, H = H, Top = top, Layer = canLayer,
                TipG = f.Place(caster, NozzleTip, 0f), TipS = f.Place(caster, NozzleTip, 0f, HandH),
            };
        }

        /// <summary>The sketch's stand-in stone chunk: <paramref name="g"/> the ground point, <paramref name="h"/> its height; scale shrinks it near the mouth.</summary>
        internal static void Chunk(Vector2 g, float h, float scale, float layer, Vector2 sun, float strength, bool flesh = false)
        {
            Sprite(new Vector2(g.x + sun.x * (h + 0.15f), g.y + sun.y * (h + 0.15f)), 0.75f * scale, 0.45f * scale, Fade(Body, strength), soft, ShadowLayer + 0.0004f);
            Disc(new Vector2(g.x, g.y + h * SixPathsHeight.Lift), layer, 0.30f * scale, 0.25f * scale, flesh ? Flesh : Stone);
            Disc(new Vector2(g.x, g.y + h * SixPathsHeight.Lift + 0.11f * scale), layer + 0.002f, 0.27f * scale, 0.21f * scale, flesh ? FleshTop : StoneTop);
        }

        /// <summary>The sketch's stand-in rifle, its long side along <paramref name="degrees"/>.</summary>
        internal static void Rifle(Vector2 g, float h, float scale, float degrees, float layer, Vector2 sun, float strength)
        {
            var c = new Vector2(g.x, g.y + h * SixPathsHeight.Lift);
            float L = 0.6f * scale, Wd = 0.09f * scale;
            Sprite(new Vector2(g.x + sun.x * h, g.y + sun.y * h), 0.6f * scale, 0.25f * scale, Fade(Body, strength * 0.8f), soft, ShadowLayer + 0.0005f);
            Rect(c, L, Wd, degrees, Wood, layer + 0.003f);
            float r = degrees * Mathf.Deg2Rad;
            Rect(new Vector2(c.x + Mathf.Cos(r) * L * 0.28f, c.y + Mathf.Sin(r) * L * 0.28f), L * 0.5f, Wd * 0.55f, degrees, Steel, layer + 0.004f);
        }

        /// <summary>
        /// A thing in the air: the sketch's chunk or rifle for the preview, or a real thing's own
        /// material (a corpse gets a stand-in lump). <paramref name="degrees"/> turns a rifle or a
        /// spinning thing.
        /// </summary>
        internal static void Thing(in VacuumThing thing, Vector2 g, float h, float scale, float degrees, float layer, Vector2 sun, float strength)
        {
            switch (thing.Kind)
            {
                case VacuumThingKind.Chunk:
                    Chunk(g, h, scale, layer, sun, strength);
                    return;
                case VacuumThingKind.Lump:
                    Chunk(g, h, scale, layer, sun, strength, true);
                    return;
                case VacuumThingKind.Rifle:
                    Rifle(g, h, scale, degrees, layer, sun, strength);
                    return;
                case VacuumThingKind.Real:
                    if (thing.Mat == null) return;
                    Vector2 size = thing.Size * scale;
                    Sprite(new Vector2(g.x + sun.x * (h + 0.15f), g.y + sun.y * (h + 0.15f)), size.x * 0.9f, size.y * 0.5f, Fade(Body, strength * 0.8f), soft, ShadowLayer + 0.0004f);
                    Sprite(new Vector2(g.x, g.y + h * SixPathsHeight.Lift), size.x, size.y, thing.Colour, thing.Mat, layer + 0.003f, thing.Spin ? -degrees : 0f);
                    return;
            }
        }

        /// <summary>The circling marks over a dazed pawn at <paramref name="at"/>, fading out after <paramref name="lasts"/> seconds.</summary>
        internal static void Daze(Vector2 at, float s, float age, float lasts)
        {
            if (age < 0f) return;
            float fade = 1f - Smooth((age - lasts) / 0.3f);
            for (int i = 0; i < 3; i++)
            {
                float turn = s * 5f + i * 2.094f;
                Sprite(new Vector2(at.x + Mathf.Cos(turn) * 0.2f, at.y + 0.86f + Mathf.Sin(turn) * 0.06f), 0.08f, 0.08f, Fade(Pale, 0.9f * fade), soft, Y + 0.03f + i * 0.0001f);
            }
        }
    }

    public enum VacuumThingKind { Chunk, Rifle, Filth, Real, Lump }

    /// <summary>
    /// One thing a picture moves: the sketches' stand-ins for the preview, or a real thing. A real one
    /// is drawn with its own material at its own size; filth streams as flecks of its colour.
    /// </summary>
    public struct VacuumThing
    {
        public VacuumThingKind Kind;
        /// <summary>Where it starts on the ground, and its height there (a weapon in a pawn's hands is at hand height).</summary>
        public Vector2 Ground;
        public float H;
        /// <summary>Its mass, kg; filth 0.</summary>
        public float Kg;
        public Material Mat;
        public Color Colour;
        public Vector2 Size;
        /// <summary>It turns in flight, as the sketch's rifle does.</summary>
        public bool Spin;
        /// <summary>It was not there to take (moved, picked up, destroyed): nothing is drawn for it.</summary>
        public bool Gone;
    }
}
