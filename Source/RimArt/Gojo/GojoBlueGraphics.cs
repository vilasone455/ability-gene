using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using T = RimArt.GojoBlue;
using Taper = RimArt.GokuGraphics.Taper;

namespace RimArt
{
    /// <summary>A pawn Blue pulls: where it stood when the pull began and where it is now, both ground points.</summary>
    public struct BlueDrag
    {
        public Vector2 From, Now;

        public BlueDrag(Vector2 from, Vector2 now)
        {
            From = from;
            Now = now;
        }
    }

    /// <summary>What one Lapse: Blue looks like now. Points are ground points on the map.</summary>
    public struct BlueCast
    {
        /// <summary>Gojo's position (the pawn's DrawPos in game) and the Blue's centre; the arm points from one to the other.</summary>
        public Vector2 Caster, Centre;
        /// <summary>The picture's clock: 0 when Gojo starts to raise his arm; the Blue opens at <see cref="GojoBlue.OpenAt"/>.</summary>
        public float Seconds;
        /// <summary>The hold before the rush-in (s); the pull, core and implosion radius (cells); how dark the ground goes, 0 to 1.</summary>
        public float Hold, PullRadius, CoreRadius, BurstRadius, Dark;
        /// <summary>Pawns being pulled, or null. Each leaves two drag marks on the floor from where it started to where it is.</summary>
        public List<BlueDrag> Dragged;
        /// <summary>Gojo's sleeve and skin colours, for the drawn arm.</summary>
        public Color Sleeve, Skin;
    }

    /// <summary>
    /// Draws Lapse: Blue, the port of the lab's gojo-blue-v2.js. Gojo's arm goes up pointing at the cell with
    /// blue light at the hand and two cyan-white arcs sweeping round him; a star glint opens at the cell and
    /// the ball grows into the 1-cell bubble: a translucent cyan sphere with a dark inside, a whirlpool of
    /// bands, a beating white point, wisps and a highlight. While it holds, the ground inside the pull goes
    /// dark blue, fog puffs on three spiral arms turn over it, pale streaks curl in, 48 slabs, planks and
    /// chips are torn out of the floor one after another (their holes stay), spiral up to orbit the ball,
    /// in front of it on the south half of the orbit and behind it on the north half, and are packed into
    /// it where they sink in a spiral; cracks run out across the floor and light rays leave the ball. Then
    /// everything rushes into the centre and it implodes: a white and a blue flash, 12 rays, a ring out to
    /// the implosion radius, 16 pieces thrown out that stay, and a grey dust cloud that rises and thins. The
    /// crater with its torn rim, the holes, the cracks and the drag marks stay to the end.
    ///
    /// Everything is a level circle, a spiral, a quad or a line at one height, so nothing needs a per-facing
    /// method. Height is drawn north (0.6 per cell) with shadows on the floor. Light is additive; the fog
    /// and the darkening are see-through.
    ///
    /// Not drawn (the sketch's stand-ins): Gojo's body and his blue tint, the pulled pawns with their blue
    /// tint, lift and white flash when held (see <see cref="GojoBlue.PawnLight"/>, <see cref="GojoBlue.HeldLiftAt"/>,
    /// <see cref="GojoBlue.HeldFlash"/>), and the pulled rock and rifle, which in game are real things the
    /// ability moves. Only their drag marks are drawn, from <see cref="BlueCast.Dragged"/>.
    /// </summary>
    public static class GojoBlueGraphics
    {
        private const float Tau = 6.2831855f;
        private static readonly Color Royal = new Color(0.12f, 0.32f, 0.95f), Abyss = new Color(0.01f, 0.03f, 0.16f), Pale = new Color(0.7f, 0.85f, 1f),
            Cyan = new Color(0.45f, 0.85f, 1f), Crater = new Color(0.14f, 0.12f, 0.1f), Torn = new Color(0.45f, 0.38f, 0.3f),
            Wood = new Color(0.4f, 0.28f, 0.16f), WoodLit = new Color(0.55f, 0.4f, 0.24f), Cloud = new Color(0.62f, 0.62f, 0.64f);
        private static readonly Color Blue = VergilGraphics.Blue, Deep = VergilGraphics.Deep, Ice = VergilGraphics.Ice, Void = VergilGraphics.Void,
            White = GojoGraphics.White;
        private static readonly Material puff = MaterialPool.MatFrom("RimArt/SixPaths/Puff", ShaderDatabase.Transparent);
        private static readonly float[] EdgeRing = { 0.985f, 0.95f, 0.91f, 0.87f, 0.83f }, EdgeAlpha = { 0.38f, 0.28f, 0.19f, 0.12f, 0.07f };
        private static readonly BluePiece[] pieces = new BluePiece[T.Pieces];
        private static readonly int[] packed = new int[T.Pieces];
        private static float piecesHold = -1f, piecesPull, piecesCore;

        public static void Draw(in BlueCast cast, Map map)
        {
            float s = cast.Seconds, hold = cast.Hold, pullR = cast.PullRadius, coreR = cast.CoreRadius, lift = SixPathsHeight.Lift;
            if (s < 0f || s >= T.Duration(hold) || !Shown(cast.Centre, map)) return;
            Begin(cast.Centre);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out _);
            Vector2 cf = cast.Centre, feet = cast.Caster, dir = cf - feet;
            float aim = dir.sqrMagnitude > 1e-6f ? Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg : 0f, chest = GokuGraphics.ChestOn;
            Vector2 c = Up(cf, T.BallUp);
            Vector2 Polar(float q, float r, float up = 0f) => new Vector2(cf.x + Mathf.Cos(q) * r, cf.y + Mathf.Sin(q) * r + up * lift);
            float open = T.OpenAt, full = T.FullAt, implode = T.ImplodeAt(hold), burstAt = T.BurstAt(hold);
            bool live = s >= open && s < burstAt;
            float grow = T.Grown(s), shrink = T.Shrink(s, hold), k = grow * shrink, late = T.Late(s, hold), swirl = T.Swirl(s, hold);
            float age = s - burstAt, held = Mathf.Clamp01((s - full) / 0.4f);
            bool burst = age >= 0f;
            MakePieces(hold, pullR, coreR);

            // --- floor: darkening, the pull ring, holes, cracks, drag marks, crater ------------------------------------
            if (live)
            {
                Sprite(cf, pullR * 2.9f, pullR * 2.7f, Fade(Void, cast.Dark * k), soft, Floor + 0.02f);
                Sprite(cf, pullR * 2f, pullR * 1.85f, Fade(Void, cast.Dark * 0.8f * k), soft, Floor + 0.021f);
                Sprite(cf, pullR * 2.6f, pullR * 2.4f, Fade(Void, cast.Dark * 0.35f * k), soft, Overhead + 0.001f);
                Sprite(cf, pullR * 1.8f, pullR * 1.6f, Fade(Blue, 0.3f * k), glow, Floor + 0.03f);
                PaperBombGraphics.RingAt(cf, pullR, Fade(Blue, 0.22f * k), Floor + 0.031f, false, whiteGlow);
            }
            for (int i = 0; i < T.Pieces; i++)
            {
                ref BluePiece g = ref pieces[i];
                if (s < g.Born || g.Kind == BluePieceKind.Chip) continue;
                float hole = Smooth((s - g.Born) / 0.15f);
                Vector2 at = Polar(g.A0, g.R0);
                Sprite(at, g.Size * 1.6f * hole, g.Size * 1.2f * hole, Fade(Crater, 0.7f), soft, Floor + 0.009f);
                Sprite(new Vector2(at.x + 0.04f, at.y + 0.04f), g.Size * 1.9f * hole, g.Size * 1.5f * hole, Fade(Torn, 0.35f), soft, Floor + 0.0085f);
            }
            for (int i = 0; i < T.Cracks; i++)
            {
                float grown = Smooth((s - full - 0.5f - 0.15f * i * (hold / 3f)) / (hold * 0.5f));
                if (grown <= 0f) continue;
                float q = i * Tau / T.Cracks + (Rand(i + 1300) - 0.5f) * 0.5f, len = (coreR * 0.6f + (1.5f + 2.1f * Rand(i + 1310))) * grown;
                Vector2 way = new Vector2(Mathf.Cos(q), Mathf.Sin(q)), side = new Vector2(-way.y, way.x);
                Vector2[] pts = GokuGraphics.Points(10);
                for (int m = 0; m <= 9; m++)
                {
                    float d = coreR * 0.8f + len * m / 9f, off = m > 0 ? (Rand(i * 13 + m + 1320) - 0.5f) * 0.16f : 0f;
                    pts[m] = cf + way * d + side * off;
                }
                GokuGraphics.Line(pts, 0.055f, Fade(VfxDraw.Ink, 0.65f), solid, Floor + 0.011f);
                if (live && s < implode) GokuGraphics.Line(pts, 0.1f, Fade(Cyan, 0.25f * k), whiteGlow, Floor + 0.012f);
            }
            if (cast.Dragged != null)
                for (int i = 0; i < cast.Dragged.Count; i++) DragMarks(cast.Dragged[i]);
            if (burst)
            {
                float g = Smooth(age / 0.1f), cr = T.CraterRadius * 2f * g;
                Sprite(cf, cr * 1.5f, cr * 1.2f, Fade(Torn, 0.55f), soft, Floor + 0.0095f);
                Sprite(cf, cr, cr * 0.8f, Fade(Crater, 0.9f), soft, Floor + 0.0096f);
                PaperBombGraphics.RingAt(cf, T.CraterRadius * 1.1f * g, Fade(Torn, 0.75f), Floor + 0.0097f, true);
                PaperBombGraphics.RingAt(cf, T.CraterRadius * 1.3f * g, Fade(Crater, 0.35f), Floor + 0.0094f);
            }

            // --- the storm: fog turning over the pull, dark below and faint cyan light above -----------------------
            if (live)
            {
                int perArm = (T.Fog + T.FogArms - 1) / T.FogArms;
                for (int i = 0; i < T.Fog; i++)
                {
                    // Puffs along FogArms spiral arms that turn, faster inside, so the fog reads as one vortex.
                    int arm = i % T.FogArms;
                    float v = (i / T.FogArms + 0.5f) / perArm, r0 = 1f + (pullR - 0.6f) * v + (Rand(i + 1400) - 0.5f) * 0.4f;
                    float rr = r0 * (1f - 0.15f * held) * (s >= implode ? shrink : 1f);
                    float turn = (s - open) * 2.6f * 2f / Mathf.Max(1f, r0) + (s >= implode ? (s - implode) * 25f : 0f);
                    float q = arm * Tau / T.FogArms + v * 2.8f - turn, size = (1.8f + 1.4f * v + 0.5f * Rand(i + 1420)) * (s >= implode ? 0.5f + 0.5f * shrink : 1f);
                    Vector2 at = Polar(q, rr, 0.2f);
                    Sprite(at, size, size * 0.8f, Fade(Deep, 0.42f * k), puff, Floor + 0.05f + i * 0.0003f, Rand(i + 1430) * 360f + s * 40f);
                    if (i % 2 == 1)
                        Sprite(at, size * 0.7f, size * 0.5f, Fade(Cyan, 0.13f * k), GojoGraphics.PuffGlow, Overhead + 0.02f + i * 0.0003f, Rand(i + 1440) * 360f - s * 60f);
                }
            }

            // --- Gojo's arm; debris behind the bubble draws under it, in front over it -------------------------------
            GojoGraphics.PointingArm(feet, aim, T.ArmOut(s), chest, cast.Sleeve, cast.Skin);
            int packedCount = 0;
            for (int i = 0; i < T.Pieces; i++)
            {
                ref BluePiece g = ref pieces[i];
                BluePieceState state = T.PieceAt(g, s, hold, out float r, out float q, out float h);
                if (state == BluePieceState.Packed)
                {
                    if (live && g.Kind != BluePieceKind.Chip) packed[packedCount++] = i;
                    continue;
                }
                if (state == BluePieceState.Ground || !live) continue;
                Vector2 ground = Polar(q, r), at = Polar(q, r, h);
                float spin = Rand(g.Index + 1500) * 360f + (s - g.Born) * (200f + 300f * Rand(g.Index + 1510));
                if (h > 0.05f)
                    Sprite(ground + sun * h, g.Size * 1.1f, g.Size * 0.7f, Fade(VfxDraw.Ink, 0.3f * Mathf.Clamp01(1.4f - h)), soft, Floor + 0.05f);
                bool front = Mathf.Sin(q) < 0f && r < coreR * 1.8f;
                Debris(g.Kind, g.Index, g.Size, at, spin, front ? Overhead + 0.135f + g.Index * 0.0002f : Overhead + 0.05f + g.Index * 0.0002f, 1f);
            }

            // --- Gojo: blue at the hand, cyan arcs round him ------------------------------------------------------
            float handK = T.HandLight(s);
            if (handK > 0f)
            {
                Vector2 hand = new Vector2(feet.x, feet.y + chest) + Turn(aim) * (GojoGraphics.ArmReach + 0.1f);
                Sprite(hand, 0.8f, 0.8f, Fade(Blue, 0.6f * handK), glow, Overhead + 0.1f);
                Sprite(hand, 0.28f, 0.28f, Fade(White, 0.85f * handK), glow, Overhead + 0.101f);
            }
            float arcK = T.Arcs(s);
            if (arcK > 0f)
            {
                Vector2 middle = new Vector2(feet.x, feet.y + chest);
                float sweep = T.ArcSweep * Mathf.Deg2Rad * Smooth(Mathf.Clamp01((s - 0.1f) / 0.6f));
                for (int i = 0; i < T.ArcRadius.Length; i++) Ribbon(middle, T.ArcRadius[i], T.ArcFrom[i] * Mathf.Deg2Rad - sweep, 2.1f, arcK);
            }

            // --- open, streaks, rays, the bubble ----------------------------------------------------------------------
            if (s >= open && s < full + 0.1f)
            {
                float kk = Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Clamp01((s - open) / (T.Grow + 0.1f))));
                GokuGraphics.Glint(c, 0.6f + 1.4f * kk, 0.95f * kk, Ice, 20f);
            }
            if (s >= full && s < burstAt)
            {
                for (int i = 0; i < T.Streaks; i++)
                {
                    float per = T.StreakPeriod * (1f - 0.4f * late), ph = ((s - full) / per + Rand(i + 900)) % 1f, a0 = Rand(i + 910) * Tau;
                    Vector2[] pts = GokuGraphics.Points(7);
                    for (int m = 0; m <= 6; m++)
                    {
                        float v = Mathf.Max(0f, ph - 0.22f + 0.22f * m / 6f), rr = coreR + (pullR - coreR) * Mathf.Pow(1f - v, 1.4f);
                        pts[m] = Polar(a0 - 2f * v, rr * shrink, T.BallUp * v * v);
                    }
                    GokuGraphics.Line(pts, 0.045f, Fade(i % 3 != 0 ? Ice : White, 0.5f * k * Mathf.Sin(Mathf.PI * ph)), whiteGlow, Overhead + 0.09f, Taper.Both);
                }
                float rayK = Smooth((s - full - 0.8f) / 0.5f) * (0.5f + 0.5f * late) * shrink;
                if (rayK > 0f)
                    for (int i = 0; i < T.Rays; i++)
                    {
                        float q = Rand(i + 1600) * Tau + s * 0.4f * (i % 2 == 1 ? 1f : -1f), len = (1.4f + 1.6f * Rand(i + 1610)) * (0.7f + 0.3f * Mathf.Sin(s * 9f + i * 2f));
                        float flicker = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(s * 17f + i * 3.1f));
                        Vector2 way = new Vector2(Mathf.Cos(q), Mathf.Sin(q));
                        Vector2[] pts = GokuGraphics.Points(3);
                        pts[0] = c; pts[1] = c + way * (len * 0.5f); pts[2] = c + way * len;
                        GokuGraphics.Line(pts, 0.14f, Fade(Ice, 0.35f * rayK * flicker), whiteGlow, Overhead + 0.095f, Taper.End);
                    }
            }
            if (live)
            {
                int first = Mathf.Max(0, packedCount - T.MostPacked);
                Sphere(c, coreR * grow * shrink, s, swirl, first, packedCount, s >= implode ? 0.6f + 0.4f * shrink : 1f);
            }

            // --- the implosion ------------------------------------------------------------------------------------------
            if (burst) Implosion(cf, c, age, cast.BurstRadius, sun);
        }

        /// <summary>A ground point raised <paramref name="up"/> cells, as it is drawn.</summary>
        private static Vector2 Up(Vector2 ground, float up) => new Vector2(ground.x, ground.y + up * SixPathsHeight.Lift);

        /// <summary>The pieces for this hold and these radii, made again only when they change.</summary>
        private static void MakePieces(float hold, float pullR, float coreR)
        {
            if (hold == piecesHold && pullR == piecesPull && coreR == piecesCore) return;
            piecesHold = hold; piecesPull = pullR; piecesCore = coreR;
            for (int i = 0; i < T.Pieces; i++) pieces[i] = T.Piece(i, hold, pullR, coreR);
        }

        /// <summary>Two dark lines 0.2 cells apart along a pulled pawn's path, from just past where it started to just behind it.</summary>
        private static void DragMarks(in BlueDrag drag)
        {
            Vector2 run = drag.Now - drag.From;
            float moved = run.magnitude;
            if (moved <= 0.05f) return;
            Vector2 way = run / moved, side = new Vector2(-way.y, way.x);
            for (int j = 0; j < 2; j++)
            {
                Vector2 across = side * (j == 0 ? -0.1f : 0.1f);
                Vector2[] pts = GokuGraphics.Points(2);
                pts[0] = drag.From + way * 0.1f + across;
                pts[1] = drag.Now - way * 0.25f + across;
                GokuGraphics.Line(pts, 0.045f, Fade(VfxDraw.Ink, 0.28f), solid, Floor + 0.012f, Taper.None);
            }
        }

        /// <summary>A cyan arc with a white core round <paramref name="centre"/>, from <paramref name="head"/> back over <paramref name="span"/> radians.</summary>
        private static void Ribbon(Vector2 centre, float radius, float head, float span, float alpha)
        {
            if (alpha <= 0f) return;
            Vector2[] pts = GokuGraphics.Points(17);
            for (int k = 0; k <= 16; k++)
            {
                float q = head + span * (1f - k / 16f);
                pts[k] = new Vector2(centre.x + Mathf.Cos(q) * radius, centre.y + Mathf.Sin(q) * radius);
            }
            GokuGraphics.Line(pts, 0.18f, Fade(Cyan, 0.4f * alpha), whiteGlow, Overhead + 0.08f, Taper.Both);
            GokuGraphics.Line(pts, 0.055f, Fade(White, 0.85f * alpha), whiteGlow, Overhead + 0.081f, Taper.Both);
        }

        /// <summary>A piece of debris at a drawn point: a slab or chip is a lit lump, a plank a two-tone board.</summary>
        private static void Debris(BluePieceKind kind, int index, float size, Vector2 at, float spin, float altitude, float alpha)
        {
            if (kind == BluePieceKind.Plank)
            {
                DrawMesh(MeshPool.plane10, at, altitude, 0.17f, size, spin, Fade(Wood, alpha), solid);
                DrawMesh(MeshPool.plane10, new Vector2(at.x - 0.02f, at.y + 0.02f), altitude + 0.0005f, 0.08f, size * 0.92f, spin, Fade(WoodLit, alpha), solid);
            }
            else PaperBombGraphics.Rock(at, size, spin, alpha, kind == BluePieceKind.Slab ? 2 * (index % 3) : 1 + 2 * (index % 3), altitude);
        }

        /// <summary>
        /// The bubble, lit from its edge, with a whirlpool inside and a white point at its heart. Back to front:
        /// a soft blue and cyan halo; a royal-blue body darkening to near-black in the middle; the packed
        /// debris (pieces[packed[first..count]]) sinking in a spiral, with the body drawn over them again;
        /// 5 thin cyan rings fading inward from the rim and a faint pale line; 12 swirl bands turning faster
        /// nearer the middle with white foam at their heads; glitter; the eye (dark disc, pale ring, beating
        /// white point, 2 short rays); 6 wisps flicking off the edge; a soft highlight and a glint upper left.
        /// </summary>
        private static void Sphere(Vector2 at, float R, float s, float swirl, int first, int count, float alpha)
        {
            if (R <= 0.01f || alpha <= 0f) return;
            float beat = 1f + 0.08f * Mathf.Sin(s * 11f), spin = swirl * 0.6f;
            Sprite(at, R * 4.4f, R * 4.4f, Fade(Blue, 0.4f * alpha), glow, Overhead + 0.1f);
            Sprite(at, R * 2.7f, R * 2.7f, Fade(Cyan, 0.28f * alpha), glow, Overhead + 0.1005f);
            DrawMesh(disc, at, Overhead + 0.101f, R, R, 0f, Fade(Royal, 0.92f * alpha), solid);
            Sprite(at, R * 1.9f, R * 1.9f, Fade(Abyss, 0.95f * alpha), soft, Overhead + 0.1015f);
            // Packed debris: each piece sinks from 0.85 R to 0.2 R over 1.5 s after it arrived, turning faster
            // as it goes in; the body is drawn over it again at 25 % so it sits inside.
            for (int n = first; n < count; n++)
            {
                ref BluePiece g = ref pieces[packed[n]];
                float u = Mathf.Clamp01((s - g.PackedAt) / 1.5f), d = R * (0.85f - 0.65f * u * u), q = Rand(g.Index + 1200) * Tau + swirl * (0.3f + 1.2f * u);
                Debris(g.Kind, g.Index, g.Size * (0.75f - 0.35f * u), new Vector2(at.x + Mathf.Cos(q) * d, at.y + Mathf.Sin(q) * d),
                    Rand(g.Index + 1210) * 360f + swirl * 25f, Overhead + 0.102f + (n - first) * 0.0001f, (0.8f - 0.4f * u) * alpha);
            }
            DrawMesh(disc, at, Overhead + 0.104f, R * 0.97f, R * 0.97f, 0f, Fade(Royal, 0.25f * alpha), solid);
            Sprite(at, R * 1.4f, R * 1.4f, Fade(Abyss, 0.7f * alpha), soft, Overhead + 0.1042f);
            // The lit edge: thin cyan rings fading inward from the rim, and a faint pale line on the rim.
            for (int j = 0; j < EdgeRing.Length; j++)
                PaperBombGraphics.RingAt(at, R * EdgeRing[j], Fade(Cyan, EdgeAlpha[j] * alpha), Overhead + 0.1045f + j * 0.0001f, false, whiteGlow);
            PaperBombGraphics.RingAt(at, R, Fade(Ice, 0.25f * alpha), Overhead + 0.105f, false, whiteGlow);
            // Swirl bands: the whirlpool. Inner bands turn faster.
            for (int k = 0; k < T.Bands; k++)
            {
                float v = k / (float)(T.Bands - 1), rr = R * (0.3f + 0.62f * v), w = 3.2f * Mathf.Pow(R / rr, 1.1f), a0 = Rand(k + 1250) * Tau + swirl * w * 0.35f;
                float span = 0.9f + 0.8f * Rand(k + 1260), width = R * (0.05f + 0.05f * Rand(k + 1270));
                Vector2[] pts = GokuGraphics.Points(9);
                for (int m = 0; m <= 8; m++)
                {
                    float q = a0 + span * m / 8f, wob = 1f + 0.04f * Mathf.Sin(m * 1.7f + s * 6f + k);
                    pts[m] = new Vector2(at.x + Mathf.Cos(q) * rr * wob, at.y + Mathf.Sin(q) * rr * wob);
                }
                Vector2 head = pts[0];
                GokuGraphics.Line(pts, width, Fade(Color.Lerp(Cyan, Pale, v), 0.42f * alpha), whiteGlow, Overhead + 0.106f + k * 0.0002f, Taper.Both);
                Sprite(head, width * 2.4f, width * 2.4f, Fade(White, 0.55f * alpha), glow, Overhead + 0.1085f);
            }
            for (int i = 0; i < T.Specks; i++)
            {
                float d = R * (0.55f + 0.5f * Rand(i + 750)), q = Rand(i + 730) * Tau + swirl * 3.2f * Mathf.Pow(R / d, 1.1f) * 0.35f;
                float twinkle = 0.3f + 0.7f * Mathf.Abs(Mathf.Sin(s * 13f + i * 1.7f)), size = 0.04f + 0.05f * Rand(i + 760);
                Sprite(new Vector2(at.x + Mathf.Cos(q) * d, at.y + Mathf.Sin(q) * d), size * 1.6f, size * 1.6f, Fade(i % 3 != 0 ? White : Cyan, twinkle * alpha), glow, Overhead + 0.109f);
            }
            // The eye.
            DrawMesh(disc, at, Overhead + 0.11f, R * 0.17f, R * 0.17f, 0f, Fade(Abyss, 0.9f * alpha), solid);
            PaperBombGraphics.RingAt(at, R * 0.2f, Fade(Ice, 0.55f * alpha), Overhead + 0.1105f, false, whiteGlow);
            Sprite(at, R * 0.55f * beat, R * 0.55f * beat, Fade(Cyan, 0.55f * alpha), glow, Overhead + 0.111f);
            Sprite(at, R * 0.22f * beat, R * 0.22f * beat, Fade(White, alpha), glow, Overhead + 0.1112f);
            for (int i = 0; i < 2; i++)
            {
                Vector2 way = Turn(i * 90f + 30f * Mathf.Sin(s * 2f) + 20f) * (R * (0.12f + 0.14f * Mathf.Abs(Mathf.Sin(s * 19f + i * 2.3f))));
                Vector2[] pts = GokuGraphics.Points(3);
                pts[0] = at - way; pts[1] = at; pts[2] = at + way;
                GokuGraphics.Line(pts, R * 0.03f, Fade(White, 0.5f * alpha), whiteGlow, Overhead + 0.1114f, Taper.Both);
            }
            // Wisps flicking off the edge the way it turns.
            for (int i = 0; i < T.Wisps; i++)
            {
                float a0 = i * Tau / T.Wisps + spin, life = (s * 1.6f + Rand(i + 1280)) % 1f;
                Vector2[] pts = GokuGraphics.Points(7);
                for (int m = 0; m <= 6; m++)
                {
                    float v = m / 6f, rr = R * (1f + 0.45f * v * life + 0.05f), q = a0 - v * 0.9f;
                    pts[m] = new Vector2(at.x + Mathf.Cos(q) * rr, at.y + Mathf.Sin(q) * rr);
                }
                GokuGraphics.Line(pts, R * 0.09f, Fade(Cyan, 0.5f * alpha * Mathf.Max(0f, Mathf.Sin(Mathf.PI * life))), whiteGlow, Overhead + 0.1116f, Taper.Both);
            }
            // Soft highlight and a glint on the upper left: the glossy marble of the anime's close-ups.
            Sprite(new Vector2(at.x - R * 0.36f, at.y + R * 0.42f), R * 0.55f, R * 0.3f, Fade(White, 0.3f * alpha), glow, Overhead + 0.112f, 35f);
            DrawMesh(disc, new Vector2(at.x - R * 0.44f, at.y + R * 0.52f), Overhead + 0.1122f, R * 0.06f, R * 0.05f, 0f, Fade(White, 0.9f * alpha), solid);
        }

        /// <summary>
        /// The implosion, <paramref name="age"/> s after it: a white and a blue flash and 12 rays for 0.22 s, a
        /// ring out to the implosion radius for 0.35 s, a grey dust cloud of 20 puffs rising and spreading for
        /// 1.8 s, and 16 pieces thrown out 1 to 3.5 cells that land and stay.
        /// </summary>
        private static void Implosion(Vector2 cf, Vector2 c, float age, float burstR, Vector2 sun)
        {
            if (age < 0.22f)
            {
                float f = age / 0.22f;
                Sprite(c, 3f * (1f - f) + 0.5f, 3f * (1f - f) + 0.5f, Fade(White, 0.95f * (1f - f)), glow, Overhead + 0.2f);
                Sprite(c, 6f * (1f - 0.4f * f), 6f * (1f - 0.4f * f), Fade(Blue, 0.65f * (1f - f)), glow, Overhead + 0.199f);
                for (int i = 0; i < 12; i++)
                {
                    float q = i * Tau / 12f + Rand(i + 1700) * 0.4f, len = (2.5f + 1.5f * Rand(i + 1710)) * (0.4f + 0.6f * f);
                    Vector2[] pts = GokuGraphics.Points(2);
                    pts[0] = c; pts[1] = new Vector2(c.x + Mathf.Cos(q) * len, c.y + Mathf.Sin(q) * len);
                    GokuGraphics.Line(pts, 0.18f, Fade(Ice, 0.7f * (1f - f)), whiteGlow, Overhead + 0.198f, Taper.End);
                }
            }
            if (age < 0.35f)
            {
                float f = age / 0.35f, rr = 0.3f + (burstR - 0.3f) * (1f - (1f - f) * (1f - f));
                PaperBombGraphics.RingAt(cf, rr, Fade(White, 0.85f * (1f - f)), Floor + 0.035f, false, whiteGlow);
                PaperBombGraphics.RingAt(cf, rr * 1.04f, Fade(Blue, 0.6f * (1f - f)), Floor + 0.034f, true, whiteGlow);
            }
            float lift = SixPathsHeight.Lift;
            // The dust cloud: grey puffs rising from the crater and spreading, then thinning.
            for (int i = 0; i < T.Clouds; i++)
            {
                float u = Mathf.Clamp01((age - Rand(i + 1800) * 0.12f) / 1.8f);
                if (u <= 0f || u >= 1f) continue;
                float q = Rand(i + 1810) * Tau, spread = Smooth(u), rr = 0.3f + (1f + 1.8f * Rand(i + 1820)) * spread, up = 0.2f + (0.5f + 1.6f * Rand(i + 1830)) * spread;
                float size = (1.3f + 2.4f * spread) * (0.8f + 0.4f * Rand(i + 1840));
                var at = new Vector2(cf.x + Mathf.Cos(q) * rr, cf.y + Mathf.Sin(q) * rr + up * lift);
                Sprite(at, size, size * 0.85f, Fade(Cloud, 0.75f * Mathf.Pow(1f - u, 1.3f) * Smooth(u / 0.06f)), puff, Overhead + 0.03f + i * 0.0004f, Rand(i + 1850) * 360f + u * 60f);
            }
            // Pieces thrown out: they land and stay.
            for (int i = 0; i < T.Thrown; i++)
            {
                bool plank = i % 4 == 0;
                float size = plank ? 0.4f : 0.18f + 0.2f * Rand(i + 1900);
                float q = i * Tau / T.Thrown + (Rand(i + 1910) - 0.5f) * 0.5f, reach = 1f + 2.5f * Rand(i + 1920), time = 0.3f + 0.2f * Rand(i + 1930), u = Mathf.Clamp01(age / time);
                float rr = reach * (1f - (1f - u) * (1f - u)), h = T.BallUp * (1f - u) + 0.7f * Mathf.Max(0f, Mathf.Sin(Mathf.PI * u));
                var ground = new Vector2(cf.x + Mathf.Cos(q) * rr, cf.y + Mathf.Sin(q) * rr);
                if (u < 1f) Sprite(ground + sun * h, size, size * 0.6f, Fade(VfxDraw.Ink, 0.35f), soft, Floor + 0.05f);
                Debris(plank ? BluePieceKind.Plank : BluePieceKind.Slab, i + 2000, size, new Vector2(ground.x, ground.y + h * lift),
                    Rand(i + 1940) * 360f + age * 700f * (1f - u), u < 1f ? Overhead + 0.05f : Floor + 0.06f, 1f);
            }
        }
    }
}
