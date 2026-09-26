using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using CS = RimArt.ChainSickleGraphics;

namespace RimArt
{
    /// <summary>Where the blade was drawn, for the drain and the glints.</summary>
    public struct SamehadaBlade
    {
        /// <summary>The hand as drawn (raised HandH), the start of the blade after the grip, and its tip.</summary>
        public Vector2 Grip, From, Tip;
        /// <summary>The direction the blade points.</summary>
        public Vector2 D;
        public float Len, Layer;
    }

    /// <summary>
    /// The drawing pieces shared by Feed, Shark Skin and Fusion: the shark-skin sword at any charge,
    /// its bandage and scales, the drain from a hit pawn into the blade, the Drained haze, the holder's
    /// heal glow, the bite, the charge tally, the torn bandage strips, and the shark form drawn over a
    /// fused holder. The port of Tools/VfxLab/web/sketches/lib/samehada.js; its numbers are that file's.
    ///
    /// The blade lies level at hand height and turns with the aim, so it is a flat shape and needs no
    /// per-facing method; it draws under the pawn layer when it points north and over it when it points
    /// south. The shark form wraps a pawn and has one draw method per facing (Up, Down, Side). The
    /// helpers (Rect, Tube, Screen, Shadow, discs) are the Chain Sickle's, as the sketch library
    /// imports them from lib/chain-sickle.js. Strips, sprites and rings come from VfxDraw: call its
    /// Begin first.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class SamehadaGraphics
    {
        // Palette. Hide is the dark blue-grey skin, Flesh the purple between spread scales, Chakra the
        // pale blue of what the blade drinks.
        internal static readonly Color Hide = new Color(0.12f, 0.14f, 0.22f), HideLit = new Color(0.24f, 0.28f, 0.40f);
        internal static readonly Color Scale = new Color(0.26f, 0.30f, 0.42f), ScaleLit = new Color(0.64f, 0.72f, 0.86f), ScaleEdge = new Color(0.05f, 0.06f, 0.11f);
        internal static readonly Color Flesh = new Color(0.42f, 0.20f, 0.48f), FleshLit = new Color(0.66f, 0.36f, 0.70f);
        internal static readonly Color Bandage = new Color(0.80f, 0.76f, 0.64f), BandageSeam = new Color(0.52f, 0.47f, 0.38f), BandageLit = new Color(0.92f, 0.89f, 0.80f);
        internal static readonly Color Bone = new Color(0.80f, 0.78f, 0.70f), Chakra = new Color(0.55f, 0.78f, 1f), ChakraDeep = new Color(0.20f, 0.45f, 0.95f), Wisp = new Color(0.72f, 0.78f, 0.88f);
        internal static readonly Color Heal = new Color(0.60f, 0.95f, 0.70f);
        internal static readonly Color Pale = CS.Cream, Skin = CS.Skin, Body = CS.Body;

        /// <summary>Hand height and where the drain leaves a standing pawn, cells up.</summary>
        internal const float HandH = 0.5f, ChestH = 0.45f;
        /// <summary>The picture's charges when none is given; in game the weapon's maxCharges.</summary>
        internal const int MaxCharges = 5;
        /// <summary>The blade's length at no charge and what each charge adds, cells. The picture only: reach stays 1 cell.</summary>
        internal const float BaseLength = 1.0f, LengthPerCharge = 0.15f;
        /// <summary>Handle and pommel, in front of the hand.</summary>
        internal const float GripLength = 0.30f;
        /// <summary>One band of scales along the blade.</summary>
        internal const float RowStep = 0.12f;
        internal const float Lead = 0.2f, Tail = 0.4f;
        internal const float Lift = SixPathsHeight.Lift;

        internal static float Y => CS.Y;
        internal static float ShadowLayer => CS.ShadowLayer;
        internal static float PawnLayer => CS.PawnLayer;

        private const int OutlineSteps = 18, WrapSteps = 10, DrainSteps = 12;
        private static readonly Vector2[] OA = new Vector2[OutlineSteps + 1], OB = new Vector2[OutlineSteps + 1];
        private static readonly Vector2[] WA = new Vector2[WrapSteps + 1], WB = new Vector2[WrapSteps + 1];
        private static readonly Vector2[] DrainPts = new Vector2[DrainSteps + 1];
        private static readonly Vector2[] Two = new Vector2[2];

        /// <summary>One scale: a small shield shape pointing along +z, 0.9 wide and 1 long. Drawn many times.</summary>
        internal static readonly Mesh scale = ScaleMesh();

        private static Mesh ScaleMesh()
        {
            // The lab's vertices (x, z) and triangles; each triangle is clockwise seen from above.
            var vertices = new[]
            {
                new Vector3(0f, 0f, 0.5f), new Vector3(0.45f, 0f, 0.15f), new Vector3(0.35f, 0f, -0.5f),
                new Vector3(-0.35f, 0f, -0.5f), new Vector3(-0.45f, 0f, 0.15f),
            };
            var mesh = new Mesh { name = "RimArt samehada scale", vertices = vertices, triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4 } };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Share of the blade still wrapped at <paramref name="charges"/>: 1 - 0.6 x charges / most.</summary>
        internal static float BandagedAt(float charges, int most = MaxCharges) => 1f - 0.6f * charges / Mathf.Max(1, most);

        internal static float BladeLength(float charges) => BaseLength + LengthPerCharge * charges;

        /// <summary>
        /// Half-width of the blade at share <paramref name="u"/> of its length: narrow at the grip, widest
        /// near the tip, blunt at the mouth. <paramref name="flare"/> spreads it.
        /// </summary>
        internal static float HalfWidth(float u, float flare = 0f)
        {
            float b = u < 0.8f ? Mathf.Lerp(0.07f, 0.17f, Smooth(u / 0.8f)) : Mathf.Lerp(0.17f, 0.13f, Smooth((u - 0.8f) / 0.2f));
            return b * (1f + 0.55f * flare);
        }

        /// <summary>A ribbon between two lines, copied into the strip pool's arrays (Strip moves them).</summary>
        internal static void Band(Vector2[] a, Vector2[] b, int count, Color colour, float altitude)
        {
            if (colour.a <= 0.001f) return;
            Sides(count, out Vector2[] sa, out Vector2[] sb);
            for (int i = 0; i < count; i++) { sa[i] = a[i]; sb[i] = b[i]; }
            Strip(sa, sb, colour, solid, altitude);
        }

        /// <summary>A filled triangle, the lab's band([a, b], [c, c]).</summary>
        internal static void Tri(Vector2 a, Vector2 b, Vector2 c, Color colour, float altitude)
        {
            if (colour.a <= 0.001f) return;
            Sides(2, out Vector2[] sa, out Vector2[] sb);
            sa[0] = a; sa[1] = b; sb[0] = c; sb[1] = c;
            Strip(sa, sb, colour, solid, altitude);
        }

        private static void Outline(Vector2 from, Vector2 d, float len, float flare)
        {
            var perp = new Vector2(-d.y, d.x);
            for (int i = 0; i <= OutlineSteps; i++)
            {
                float u = i / (float)OutlineSteps, w = i == OutlineSteps ? 0.05f : HalfWidth(u, flare);
                Vector2 x = from + d * (len * u);
                OA[i] = x + perp * w;
                OB[i] = x - perp * w;
            }
        }

        private static Vector2 At(Vector2 b, Vector2 d, float along, float side = 0f) =>
            new Vector2(b.x + d.x * along - d.y * side, b.y + d.y * along + d.x * side);

        /// <summary>
        /// The sword. <paramref name="hand"/> is a ground point, HandH up; <paramref name="deg"/> the
        /// direction the blade points (0 east, 90 north).
        /// charges 0..most, fractional while one is being gained (the tip grows); flare 0..1 is Shark
        /// Skin (scales stand off, the blade widens 55 %); tear 0..1 how much of the bandaged span is torn
        /// off; hot 0..1 a pale blue light while the blade drinks. <paramref name="layer"/> null picks
        /// under the pawn layer pointing north, over it pointing south; <paramref name="step"/> scales the
        /// small altitude steps between the parts (1 in the pictures; smaller for the held blade, which
        /// must stay inside the pawn's own layers).
        /// </summary>
        internal static SamehadaBlade Blade(Vector2 hand, float deg, float charges, Vector2 sun, float strength,
            float flare = 0f, float tear = 0f, float hot = 0f, float alpha = 1f, float? layer = null, float step = 1f, int most = MaxCharges)
        {
            float r = deg * Mathf.Deg2Rad;
            var d = new Vector2(Mathf.Cos(r), Mathf.Sin(r));
            float L = layer ?? (d.y > 0f ? PawnLayer - 0.03f : Y + 0.05f);
            float len = BladeLength(charges), wrapped = BandagedAt(Mathf.Clamp(charges, 0f, most), most) * (1f - tear);
            Vector2 p = CS.Screen(CS.At(hand, HandH)), sd = CS.Shadow(CS.At(hand, HandH), sun);

            // Shadow: grip and blade outline in one dark tone on the floor.
            Color shade = Fade(Body, strength * 0.6f * alpha);
            CS.Rect(At(sd, d, GripLength * 0.5f), GripLength, 0.11f, deg, shade, ShadowLayer);
            Outline(At(sd, d, GripLength), d, len, flare);
            Band(OA, OB, OutlineSteps + 1, shade, ShadowLayer);

            // Grip: a dark wrapped handle with a bone pommel at the hand end, and the hand on it.
            CS.Rect(At(p, d, GripLength * 0.55f), GripLength * 0.9f, 0.085f, deg, Fade(Hide, alpha), L);
            for (int i = 0; i < 4; i++)
                CS.Rect(At(p, d, GripLength * (0.22f + i * 0.18f)), 0.09f, 0.028f, deg + 62f, Fade(BandageSeam, alpha), L + 0.001f * step);
            CS.Disc(new Vector2(p.x - d.x * 0.02f, p.y - d.y * 0.02f), L + 0.002f * step, 0.075f, 0.075f, Fade(Bone, alpha));
            CS.Disc(new Vector2(p.x - d.x * 0.02f - 0.012f, p.y - d.y * 0.02f + 0.015f), L + 0.003f * step, 0.028f, 0.028f, Fade(Hide, alpha));
            Sprite(At(p, d, GripLength * 0.45f), 0.11f, 0.12f, Fade(Skin, alpha), soft, L + 0.004f * step);

            // Blade body: a dark edge band, the hide face, then the purple flesh where scales are spread.
            Vector2 from = At(p, d, GripLength);
            Outline(from, d, len, flare);
            Sides(OutlineSteps + 1, out Vector2[] ea, out Vector2[] eb);
            for (int i = 0; i <= OutlineSteps; i++)
            {
                ea[i] = OA[i] + (OA[i] - OB[i]) * 0.09f;
                eb[i] = OB[i] + (OB[i] - OA[i]) * 0.09f;
            }
            Strip(ea, eb, Fade(ScaleEdge, alpha), solid, L + 0.005f * step);
            Band(OA, OB, OutlineSteps + 1, Fade(Color.Lerp(Hide, Flesh, flare * 0.8f), alpha), L + 0.006f * step);
            Inset(OA, OB, OutlineSteps + 1, 0.5f, 0.78f, Fade(Color.Lerp(HideLit, FleshLit, flare * 0.8f), alpha * 0.8f), L + 0.007f * step);

            // Scales: rows every RowStep along the bare span, three across, pointing at the tip. Under
            // the bandage they are not drawn. Flared, each row stands off to its side.
            int rows = Mathf.FloorToInt(len / RowStep);
            float bareFrom = wrapped * len;
            for (int i = 0; i < rows; i++)
            {
                float along = (i + 0.6f) * RowStep;
                if (along < bareFrom) continue;
                float u = along / len, w = HalfWidth(u, flare), sz = Mathf.Min(0.13f, w);
                float reveal = Mathf.Clamp01((along - bareFrom) / RowStep);
                for (int k = -1; k <= 1; k++)
                {
                    if (k != 0 && w < 0.09f) continue;
                    float side = k * w * (0.55f + 0.25f * flare);
                    Vector2 c = At(from, d, along + (k == 0 ? 0f : RowStep * 0.5f), side);
                    bool lit = (i + k + 3) % 2 == 0;
                    Color tone = Color.Lerp(lit ? ScaleLit : Scale, Chakra, hot * 0.7f);
                    float angle = -deg + 90f + k * 14f * flare;
                    DrawMesh(scale, c, L + 0.008f * step, sz * (1f + 0.15f * flare), sz * 1.15f, angle, Fade(ScaleEdge, alpha * reveal), solid);
                    DrawMesh(scale, c + d * 0.006f, L + 0.009f * step, sz * 0.78f * (1f + 0.15f * flare), sz * 0.9f, angle, Fade(tone, alpha * reveal), solid);
                }
            }
            if (hot > 0f) Sprite(At(p, d, GripLength + len * 0.55f), len * 1.1f, 0.5f, Fade(Chakra, 0.45f * hot * alpha), glow, L + 0.012f * step, -deg);

            // Bandage over the wrapped span: a cream band, diagonal seams, one lit line along the top.
            if (wrapped > 0.01f)
            {
                var perp = new Vector2(-d.y, d.x);
                for (int i = 0; i <= WrapSteps; i++)
                {
                    float u = i / (float)WrapSteps * wrapped, w = HalfWidth(u, flare) * 1.08f + 0.01f;
                    Vector2 x = from + d * (len * u);
                    WA[i] = x + perp * w;
                    WB[i] = x - perp * w;
                }
                Band(WA, WB, WrapSteps + 1, Fade(Bandage, alpha), L + 0.010f * step);
                Inset(WA, WB, WrapSteps + 1, 0.55f, 0.85f, Fade(BandageLit, alpha * 0.7f), L + 0.0105f * step);
                int seams = Mathf.FloorToInt(wrapped * len / 0.095f);
                for (int i = 0; i < seams; i++)
                {
                    float along = (i + 0.5f) * 0.095f, u = along / len, w = HalfWidth(u, flare) * 2.3f;
                    CS.Rect(At(from, d, along), w, 0.022f, deg + 58f, Fade(BandageSeam, alpha * 0.9f), L + 0.011f * step);
                }
                // The frayed end where the wrap stops: two short loose tails.
                float e = wrapped * len, we = HalfWidth(e / len, flare);
                CS.Rect(At(from, d, e + 0.04f, we * 0.6f), 0.09f, 0.03f, deg + 30f, Fade(Bandage, alpha), L + 0.0112f * step);
                CS.Rect(At(from, d, e + 0.03f, -we * 0.7f), 0.07f, 0.03f, deg - 40f, Fade(Bandage, alpha), L + 0.0113f * step);
            }

            // The mouth at the tip: two small teeth either side of the point.
            Vector2 tip = At(from, d, len);
            for (int k = -1; k <= 1; k += 2)
                CS.Rect(At(from, d, len - 0.03f, k * 0.07f), 0.09f, 0.028f, deg + k * 20f, Fade(Bone, alpha), L + (0.012f + (k + 1) * 0.0001f) * step);
            return new SamehadaBlade { Grip = p, From = from, Tip = tip, D = d, Len = len, Layer = L };
        }

        /// <summary>The band between <paramref name="k0"/> and <paramref name="k1"/> of the way from a to b.</summary>
        private static void Inset(Vector2[] a, Vector2[] b, int count, float k0, float k1, Color colour, float altitude)
        {
            if (colour.a <= 0.001f) return;
            Sides(count, out Vector2[] sa, out Vector2[] sb);
            for (int i = 0; i < count; i++)
            {
                sa[i] = Vector2.LerpUnclamped(a[i], b[i], k0);
                sb[i] = Vector2.LerpUnclamped(a[i], b[i], k1);
            }
            Strip(sa, sb, colour, solid, altitude);
        }

        /// <summary>
        /// The drain: chakra pulled out of a pawn at chest height and into the blade's tip. A faint line,
        /// nine parcels sliding along it, and a haze that leaves the pawn. <paramref name="age"/> runs over
        /// <paramref name="life"/>; the parcels start at the pawn and reach the blade by the end.
        /// </summary>
        internal static void Drain(Vector2 chest, Vector2 tip, float age, float life, float alpha = 1f)
        {
            if (age < 0f || age >= life) return;
            float u = age / life, fade = Mathf.Sqrt(Mathf.Max(0f, Mathf.Sin(Mathf.Min(1f, u * 1.15f) * Mathf.PI)));
            for (int i = 0; i <= DrainSteps; i++)
            {
                float v = i / (float)DrainSteps, sag = Mathf.Max(0f, Mathf.Sin(v * Mathf.PI)) * 0.12f;
                DrainPts[i] = new Vector2(Mathf.Lerp(chest.x, tip.x, v), Mathf.Lerp(chest.y, tip.y, v) + sag);
            }
            CS.Tube(DrainPts, DrainSteps + 1, 0.02f, 0.02f, Fade(Chakra, 0.35f * fade * alpha), Y + 0.08f);
            CS.Tube(DrainPts, DrainSteps + 1, 0.07f, 0.07f, Fade(ChakraDeep, 0.25f * fade * alpha), Y + 0.079f);
            // Nine parcels leave the pawn one after another and travel the line in 55 % of the life.
            for (int i = 0; i < 9; i++)
            {
                float t0 = i / 9f * 0.45f, v = (u - t0) / 0.55f;
                if (v < 0f || v > 1f) continue;
                float idx = v * DrainSteps;
                int j = Mathf.Min(DrainSteps - 1, Mathf.FloorToInt(idx));
                float f = idx - j;
                Vector2 q = Vector2.LerpUnclamped(DrainPts[j], DrainPts[j + 1], f) + new Vector2(0f, (Rand(i + 700) - 0.5f) * 0.06f);
                float sz = 0.14f + Rand(i + 710) * 0.08f;
                Sprite(q, sz * 1.8f, sz * 1.8f, Fade(ChakraDeep, 0.5f * alpha), glow, Y + 0.081f + i * 0.00002f);
                Sprite(q, sz, sz, Fade(Pale, 0.9f * alpha), glow, Y + 0.082f + i * 0.00002f);
            }
            // The pawn goes grey where the chakra left: a haze round the chest that thins.
            Sprite(chest, 0.5f, 0.45f, Fade(Wisp, 0.35f * fade * alpha), soft, Y + 0.078f);
        }

        /// <summary>
        /// Drained on a pawn: a dim grey-blue haze round the feet and one thin ring per stack rising
        /// slowly and thinning. <paramref name="s"/> is any clock in seconds.
        /// </summary>
        /// <param name="pawnAltitude">The drained pawn's own altitude (DrawPos.y); the picture's pawn layer if null.</param>
        internal static void Drained(Vector2 pos, int stacks, float s, float alpha = 1f, float? pawnAltitude = null)
        {
            if (stacks <= 0) return;
            float k = Mathf.Min(1f, stacks / (float)MaxCharges);
            Sprite(new Vector2(pos.x, pos.y + 0.05f), 0.9f, 0.55f, Fade(Wisp, 0.18f * k * alpha), soft, VfxDraw.Floor + 0.02f);
            for (int i = 0; i < stacks; i++)
            {
                float u = Frac(s * 0.35f + i / (float)stacks), h = u * 0.8f;
                Circle(new Vector2(pos.x, pos.y + 0.1f + h * Lift), 0.28f + u * 0.05f, 0.35f * (1f - u) * alpha, (pawnAltitude ?? PawnLayer) + 0.03f + i * 0.0001f, Wisp);
            }
        }

        /// <summary>Healing on the holder: soft green light rising up the body, then gone.</summary>
        internal static void Healing(Vector2 pos, float age, float life, float alpha = 1f)
        {
            if (age < 0f || age >= life) return;
            float u = age / life;
            Sprite(new Vector2(pos.x, pos.y + 0.3f + u * 0.3f), 0.7f, 0.9f, Fade(Heal, 0.32f * Mathf.Max(0f, Mathf.Sin(u * Mathf.PI)) * alpha), glow, Y + 0.07f);
            for (int i = 0; i < 5; i++)
            {
                float v = Frac(u * 1.3f + Rand(i + 900) * 0.6f), x = pos.x + (Rand(i + 910) - 0.5f) * 0.5f;
                Sprite(new Vector2(x, pos.y + 0.1f + v * 0.9f), 0.1f, 0.1f, Fade(Heal, (1f - v) * 0.8f * alpha), glow, Y + 0.071f + i * 0.00002f);
            }
        }

        /// <summary>A hit: a short pale flash at the point of contact and a bite mark of three scratches.</summary>
        /// <param name="pawnAltitude">The bitten pawn's own altitude (DrawPos.y); the picture's pawn layer if null.</param>
        internal static void Bite(Vector2 pos, float deg, float age, float alpha = 1f, float? pawnAltitude = null)
        {
            if (age < 0f) return;
            if (age < 0.14f) Sprite(pos, 0.55f, 0.45f, Fade(Pale, (1f - age / 0.14f) * 0.8f * alpha), glow, Y + 0.09f);
            float fade = 1f - Mathf.Clamp01((age - 0.6f) / 1.4f);
            if (fade <= 0f) return;
            float r = (deg + 90f) * Mathf.Deg2Rad;
            for (int i = -1; i <= 1; i++)
            {
                var c = new Vector2(pos.x + Mathf.Cos(r) * i * 0.08f, pos.y + Mathf.Sin(r) * i * 0.08f);
                CS.Rect(c, 0.22f, 0.022f, deg + 20f, Fade(ChakraDeep, 0.6f * fade * alpha), (pawnAltitude ?? PawnLayer) + 0.04f + (i + 1) * 0.0001f);
            }
        }

        /// <summary>
        /// The charge tally: one slot per charge south of the holder's feet, or above the head when the
        /// holder aims south. Filled slots are lit scales, empty ones dark outlines.
        /// </summary>
        internal static void Tally(Vector2 pos, float charges, float aimDeg = 0f, float alpha = 1f, int most = MaxCharges)
        {
            bool above = Mathf.Sin(aimDeg * Mathf.Deg2Rad) < -0.5f;
            for (int i = 0; i < most; i++)
            {
                var c = new Vector2(pos.x - 0.07f * (most - 1) + i * 0.14f, pos.y + (above ? 1.15f : -0.55f));
                float fill = Mathf.Clamp01(charges - i);
                DrawMesh(scale, c, VfxDraw.Floor + 0.05f, 0.11f, 0.12f, 0f, Fade(ScaleEdge, 0.7f * alpha), solid);
                DrawMesh(scale, c, VfxDraw.Floor + 0.051f, 0.085f, 0.095f, 0f, Fade(Color.Lerp(Hide, ScaleLit, fill), alpha * (0.5f + 0.5f * fill)), solid);
            }
        }

        /// <summary>
        /// A torn bandage strip thrown from <paramref name="start"/> (ground, <paramref name="h0"/> up) with
        /// ground speed <paramref name="v"/>, rising <paramref name="rise"/>; it lands and stays.
        /// </summary>
        internal static void TornStrip(Vector2 start, float h0, Vector2 v, float rise, float age, float deg, float alpha = 1f, int index = 0)
        {
            if (age < 0f) return;
            const float g = 3.2f;
            float tLand = (rise + Mathf.Sqrt(rise * rise + 2f * g * h0)) / g;
            float t = Mathf.Min(age, tLand), h = Mathf.Max(0f, h0 + rise * t - 0.5f * g * t * t);
            var at = new Vector2(start.x + v.x * t, start.y + v.y * t);
            bool landed = age >= tLand;
            float turn = deg + t * 300f, tiny = index * 0.0001f;
            if (!landed) CS.Rect(at, 0.16f, 0.05f, turn, Fade(Body, 0.3f * alpha), ShadowLayer + tiny);
            CS.Rect(new Vector2(at.x, at.y + h * Lift), 0.16f, 0.045f, turn, Fade(Bandage, alpha), (landed ? VfxDraw.Floor + 0.03f : Y + 0.06f) + tiny);
        }

        /// <summary>
        /// The Shark Skin arc on the floor: the 3 cells in front, from radius <paramref name="inner"/> to
        /// <paramref name="outer"/> and <paramref name="half"/> degrees either side of the aim.
        /// </summary>
        internal static void Arc(Vector2 centre, float aimDeg, float inner, float outer, float half, float a)
        {
            if (a <= 0.001f) return;
            const int n = 20;
            for (int i = 0; i <= n; i++)
            {
                float ang = (aimDeg + Mathf.Lerp(half, -half, i / (float)n)) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                ArcIn[i] = centre + dir * inner;
                ArcOut[i] = centre + dir * outer;
            }
            Band(ArcIn, ArcOut, n + 1, Fade(Pale, a * 0.25f), VfxDraw.Floor + 0.02f);
            // The rim: its inner edge is 94 % of the way out.
            Sides(n + 1, out Vector2[] ra, out Vector2[] rb);
            for (int i = 0; i <= n; i++) { ra[i] = Vector2.LerpUnclamped(ArcIn[i], ArcOut[i], 0.94f); rb[i] = ArcOut[i]; }
            Strip(ra, rb, Fade(Pale, a), solid, VfxDraw.Floor + 0.021f);
        }

        private static readonly Vector2[] ArcIn = new Vector2[21], ArcOut = new Vector2[21];

        private static float Frac(float x) => x - Mathf.Floor(x);

        // ------------------------------------------------------------------ Fusion

        internal enum SharkFacing { Up, Down, Side }

        internal static SharkFacing FacingOf(float aimDeg)
        {
            float sn = Mathf.Sin(aimDeg * Mathf.Deg2Rad);
            return sn > 0.5f ? SharkFacing.Up : sn < -0.5f ? SharkFacing.Down : SharkFacing.Side;
        }

        /// <summary>
        /// The shark form is laid out on the sketch's 0.89-cell stand-in and fitted to a real humanlike pawn:
        /// its body and head are 1.5-cell meshes (MeshPool.HumanlikeBodyWidth, BodyTypeDef.headOffset z 0.34)
        /// and what shows of them runs from 0.51 below the pawn's position to 0.66 above. Every offset and
        /// size is scaled by FitScale and the form moves down by FitDrop (lib/samehada.js SharkFit).
        /// </summary>
        internal const float FitScale = 1.3f, FitDrop = -0.33f;

        private static Vector2 Fit(Vector2 pos, float dx, float dz) => new Vector2(pos.x + dx * FitScale, pos.y + FitDrop + dz * FitScale);

        /// <summary>
        /// The shark form over a pawn at <paramref name="pos"/>: a scaled body, head with gills, and a
        /// fin and tail drawn by facing. <paramref name="amount"/> 0..1 fades it in; <paramref name="s"/>
        /// drives the tail's sway. <paramref name="over"/> is the altitude of what covers the pawn and
        /// <paramref name="under"/> of what hangs behind it (the sketch: pawn layer + 0.012 and 0.03 less).
        /// </summary>
        internal static void SharkForm(Vector2 pos, float aimDeg, float amount, float s, float over, float under, float alpha = 1f)
        {
            if (amount <= 0f) return;
            const float S = FitScale;
            float A = alpha * amount, L = over;
            SharkFacing facing = FacingOf(aimDeg);
            bool west = Mathf.Cos(aimDeg * Mathf.Deg2Rad) < 0f;
            float sway = Mathf.Sin(s * 9f) * 0.05f;
            Color skin = Fade(Color.Lerp(Hide, HideLit, 0.25f), A), lit = Fade(HideLit, A * 0.8f), edge = Fade(ScaleEdge, A);
            // Body: an ellipse over the pawn's torso, dark hide with a lit belly stripe.
            CS.Disc(Fit(pos, 0f, 0.18f), L, 0.25f * S, 0.35f * S, skin);
            CS.Disc(Fit(pos, facing == SharkFacing.Side ? (west ? -0.06f : 0.06f) : 0f, 0.16f), L + 0.001f, 0.10f * S, 0.26f * S, lit);
            // Scales: four rows of three across the body.
            for (int r = 0; r < 4; r++)
                for (int k = -1; k <= 1; k++)
                {
                    Vector2 c = Fit(pos, k * 0.13f + (r % 2 == 1 ? 0.05f : 0f), 0.04f + r * 0.1f);
                    bool litS = (r + k) % 2 == 0;
                    DrawMesh(scale, c, L + 0.002f, 0.08f * S, 0.09f * S, 0f, edge, solid);
                    DrawMesh(scale, new Vector2(c.x, c.y + 0.005f * S), L + 0.003f, 0.06f * S, 0.07f * S, 0f, Fade(litS ? ScaleLit : Scale, A), solid);
                }
            // Head: the hide over the head leaves the eyes; gills as three short lines each side.
            CS.Disc(Fit(pos, 0f, 0.58f), L + 0.004f, 0.18f * S, 0.19f * S, skin);
            CS.Disc(Fit(pos, 0f, 0.56f), L + 0.005f, 0.12f * S, 0.10f * S, Fade(Color.Lerp(Skin, Hide, 0.55f), A));
            for (int k = -1; k <= 1; k += 2)
                for (int i = 0; i < 3; i++)
                    CS.Rect(Fit(pos, k * (0.15f + i * 0.012f), 0.47f + i * 0.06f), 0.06f * S, 0.014f * S, 80f * k, Fade(Flesh, A), L + 0.006f);
            Color finC = Fade(ScaleEdge, A), finLit = Fade(ScaleLit, A * 0.85f);
            switch (facing)
            {
                case SharkFacing.Up: SharkUp(pos, sway, finC, finLit, L, under); break;
                case SharkFacing.Down: SharkDown(pos, sway, finC, finLit, under); break;
                default: SharkSide(pos, sway, west, finC, finLit, L, under); break;
            }
            // Seams: a little purple flesh shows where the hide meets.
            Sprite(Fit(pos, 0f, 0.38f), 0.3f * S, 0.12f * S, Fade(Flesh, 0.35f * A), soft, L + 0.009f);
        }

        /// <summary>Aim north, back to the camera: the fin runs down the spine as a dark strip, the tail hangs below the body.</summary>
        private static void SharkUp(Vector2 pos, float sway, Color finC, Color finLit, float L, float under)
        {
            const float S = FitScale;
            CS.Rect(Fit(pos, 0f, 0.22f), 0.55f * S, 0.08f * S, 90f, finC, L + 0.007f);
            CS.Rect(Fit(pos, 0f, 0.22f), 0.5f * S, 0.035f * S, 90f, finLit, L + 0.008f);
            Tri(Fit(pos, -0.06f + sway, -0.05f), Fit(pos, 0.06f + sway, -0.05f), Fit(pos, sway * 2f, -0.38f), finC, under);
            Tri(Fit(pos, -0.16f + sway * 2f, -0.42f), Fit(pos, 0.16f + sway * 2f, -0.42f), Fit(pos, sway * 2f, -0.3f), finC, under + 0.0001f);
        }

        /// <summary>Aim south, face to the camera: the fin rises above the head (height drawn north), two flukes beside the feet.</summary>
        private static void SharkDown(Vector2 pos, float sway, Color finC, Color finLit, float under)
        {
            Tri(Fit(pos, -0.11f, 0.62f), Fit(pos, 0.11f, 0.62f), Fit(pos, 0.04f, 0.62f + 0.55f * Lift), finC, under);
            Tri(Fit(pos, -0.04f, 0.63f), Fit(pos, 0.06f, 0.63f), Fit(pos, 0.03f, 0.62f + 0.42f * Lift), finLit, under + 0.001f);
            for (int k = -1; k <= 1; k += 2)
                Tri(Fit(pos, k * 0.12f, -0.02f), Fit(pos, k * 0.3f + sway, -0.1f), Fit(pos, k * 0.2f + sway, 0.02f), finC, under + (k + 1) * 0.0001f);
        }

        /// <summary>Aim east or west, in profile: the fin stands up from the mid-back, the tail trails behind, away from the aim.</summary>
        private static void SharkSide(Vector2 pos, float sway, bool west, Color finC, Color finLit, float L, float under)
        {
            const float S = FitScale;
            float back = west ? 1f : -1f;
            Tri(Fit(pos, back * 0.04f, 0.3f), Fit(pos, -back * 0.16f, 0.3f), Fit(pos, back * 0.16f, 0.3f + 0.6f * Lift), finC, L + 0.007f);
            Tri(Fit(pos, -back * 0.02f, 0.31f), Fit(pos, -back * 0.11f, 0.31f), Fit(pos, back * 0.09f, 0.31f + 0.45f * Lift), finLit, L + 0.008f);
            // Tail: a thin tube out of the hip and a crescent fluke (two thin lobes).
            Vector2 tailBase = Fit(pos, back * 0.18f, 0.12f + sway), tailTip = Fit(pos, back * 0.42f, 0.1f + sway * 2f);
            Two[0] = tailBase; Two[1] = tailTip;
            CS.Tube(Two, 2, 0.045f * S, 0.0225f * S, finC, under);
            Two[0] = tailTip; Two[1] = new Vector2(tailTip.x + back * 0.06f * S, tailTip.y + 0.2f * S);
            CS.Tube(Two, 2, 0.03f * S, 0.009f * S, finC, under + 0.0001f);
            Two[0] = tailTip; Two[1] = new Vector2(tailTip.x + back * 0.05f * S, tailTip.y - 0.14f * S);
            CS.Tube(Two, 2, 0.03f * S, 0.009f * S, finC, under + 0.0002f);
        }

        /// <summary>A regen pulse every second while fused: one soft green ring rising off the body, fitted as the form is.</summary>
        /// <param name="pawnAltitude">The pawn's own altitude (its DrawPos.y, which carries a seeded offset of up to one altitude step); the picture's pawn layer if null.</param>
        internal static void RegenPulse(Vector2 pos, float s, float alpha = 1f, float? pawnAltitude = null)
        {
            float u = Frac(s);
            Circle(new Vector2(pos.x, pos.y + FitDrop + (0.15f + u * 0.5f * Lift) * FitScale), (0.22f + u * 0.12f) * FitScale, 0.45f * (1f - u) * alpha, (pawnAltitude ?? PawnLayer) + 0.03f, Heal);
        }

        /// <summary>A point <paramref name="along"/> the aim and <paramref name="across"/> it (left positive) from <paramref name="base"/>.</summary>
        internal static Vector2 Ground(Vector2 @base, float aimDeg, float along, float across)
        {
            float r = aimDeg * Mathf.Deg2Rad, ca = Mathf.Cos(r), sa = Mathf.Sin(r);
            return new Vector2(@base.x + along * ca - across * sa, @base.y + along * sa + across * ca);
        }

        /// <summary>The sketches' hand: 0.22 along the blade and 0.12 to its left of the feet.</summary>
        internal static Vector2 Hand(Vector2 feet, float bladeDeg)
        {
            float r = bladeDeg * Mathf.Deg2Rad;
            return new Vector2(feet.x + Mathf.Cos(r) * 0.22f - Mathf.Sin(r) * 0.12f, feet.y + Mathf.Sin(r) * 0.22f + Mathf.Cos(r) * 0.12f);
        }

        /// <summary>The swings mirror when aiming west (cos &lt; 0), so the blade rests on the screen's low side.</summary>
        internal static float Mirror(float aimDeg) => Mathf.Cos(aimDeg * Mathf.Deg2Rad) < -1e-6f ? -1f : 1f;
    }
}
