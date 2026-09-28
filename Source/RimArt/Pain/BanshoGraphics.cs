using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using G = RimArt.PainGraphics;
using T = RimArt.BanshoTiming;

namespace RimArt
{
    /// <summary>
    /// Where things are for one frame of a Banshō Ten'in picture. Ground points are feet. In the game the target and
    /// the blocker are the real pawns' live ground points; in the preview they come from <see cref="BanshoGraphics.Scripted"/>.
    /// </summary>
    public struct BanshoView
    {
        /// <summary>Pain's ground point now: his arm, the palm core and the eye glint are drawn from it.</summary>
        public Vector2 pain;
        /// <summary>
        /// Pain's ground point at the cast: the aim line, floor streaks, furrows, landing, slam and crater are placed from
        /// it, so they stay if he moves. Left zero, <see cref="pain"/> is used.
        /// </summary>
        public Vector2 castAt;
        /// <summary>Unit way from Pain to the target at the cast.</summary>
        public Vector2 aim;
        /// <summary>From BanshoTiming.Times: the start distance, heavy, blocked and every phase time.</summary>
        public BanshoTimes times;
        /// <summary>The target's ground point now, and its height now (cells up; drawn Lift × height north).</summary>
        public Vector2 target;
        public float height;
        /// <summary>The blocker's ground point now (blocked only).</summary>
        public Vector2 block;
        /// <summary>Draw the lab's stand-ins (Pain, the pulled pawn, its two afterimages, the blocker, the thrumbo). The game passes false.</summary>
        public bool standIns;
    }

    /// <summary>
    /// The Banshō Ten'in picture: the port of pain-bansho-tenin.js, look "Storm 4: black core, pale glow" only (the
    /// Naruto Mobile and anime looks and the Rinnegan cut-in are not ported). Pain's arm comes up with the fingers
    /// spread and a black core in a pale glow forms past the fingertips; a see-through dark sphere closes on the
    /// target's chest and a dashed line shows the pull; at the grip the core stretches into a teardrop aimed at the
    /// target and dark streaks flow into its point; the feet scrape two furrows, dust puffs where they leave the floor;
    /// in flight pale speed lines, a dark smoke trail and dust streaks on the floor run toward Pain; at the catch the
    /// core snaps round with a soft flash and goes into the hand, which closes on the head and pushes it down; the slam
    /// throws pale rays, a dust ring and a cloud, 7 plates of floor tilt up round a dent with cracks (a gap on Pain's
    /// side) and rocks fly out. Blocked: a flash and dust between the two pawns and a crack where the target drops.
    /// Dragged: a drag scuff, dust at the feet, the sphere 2.1 times as big.
    ///
    /// Every piece is a level circle, a flat quad or a strip turned with the aim; the plates are flat polygons with
    /// their tops raised north by their height, more upright on the south side. No per-facing method. The plates,
    /// dent, cracks, furrows and rocks stay until the picture's last Tail (0.4) seconds and fade out over them.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class BanshoGraphics
    {
        private static readonly Color InkBlack = new Color(0.03f, 0.02f, 0.03f), StreakC = new Color(0.92f, 0.88f, 0.78f);
        private static readonly Color EarthDark = new Color(0.2f, 0.15f, 0.1f), EarthLit = new Color(0.56f, 0.45f, 0.33f),
            StoneDark = new Color(0.25f, 0.23f, 0.21f), StoneLit = new Color(0.6f, 0.57f, 0.52f), SlabEdge = new Color(0.08f, 0.06f, 0.04f);
        // Stand-ins: the sandbags, the thrumbo, and the lib's shadow colour for them (six-paths-solid.js Body).
        private static readonly Color Bag = new Color(0.64f, 0.57f, 0.42f), BagDark = new Color(0.36f, 0.31f, 0.22f),
            Beast = new Color(0.45f, 0.36f, 0.28f), Body = new Color(0.035f, 0.028f, 0.050f);

        private static readonly float Y = Overhead, BuildingLayer = AltitudeLayer.Building.AltitudeFor();
        private const float Lift = SixPathsHeight.Lift, Tau = Mathf.PI * 2f;

        // A dark edge on a see-through circle: inner radius and alpha of six stacked bands, darkest at the rim.
        private static readonly float[] EdgeInner = { 0.97f, 0.93f, 0.89f, 0.84f, 0.79f, 0.73f }, EdgeAlpha = { 0.4f, 0.2f, 0.13f, 0.1f, 0.07f, 0.05f };
        private static readonly Mesh[] EdgeBands = MakeEdges();

        private static Mesh[] MakeEdges()
        {
            var made = new Mesh[EdgeInner.Length];
            for (int i = 0; i < made.Length; i++) made[i] = Ring(EdgeInner[i], "RimArt bansho edge " + i);
            return made;
        }

        // Scratch, so a frame allocates nothing.
        private const int CorePoints = 44;
        private static readonly Vector2[] core = new Vector2[CorePoints + 2], face = new Vector2[6], grown = new Vector2[6], cast = new Vector2[6];
        private static readonly Slab[] slabs = new Slab[16];
        private static readonly Vector2[] bagAt = new Vector2[24];
        private static readonly float[] bagH = new float[24];
        private static readonly Vector2[] T3 = new Vector2[3];

        private struct Slab
        {
            public int k;
            public float th, r0, w, len, tau, z;
            public bool stone;
            public float j0, j1, j2, j3;
        }

        /// <summary>
        /// The preview's script: the view at <paramref name="s"/> with the target and the blocker where the sketch's
        /// stand-ins are (BanshoTiming's motion, the blocker walking in and knocked back). The thrumbo's jitter and the
        /// blocker's shake are added by the stand-in drawing only, as in the sketch.
        /// </summary>
        public static BanshoView Scripted(Vector2 pain, Vector2 aim, in BanshoTimes t, float s, bool standIns)
        {
            var across = new Vector2(-aim.y, aim.x);
            return new BanshoView
            {
                pain = pain, castAt = pain, aim = aim, times = t, standIns = standIns,
                target = pain + aim * T.AlongAt(s, t),
                height = T.HeightAt(s, t),
                block = pain + aim * (t.blockAt - T.KnockAt(s, t)) + across * T.BlockerSide(s, t),
            };
        }

        /// <summary>
        /// One frame at sketch time <paramref name="s"/> (0 at rest, BanshoTiming.Lead when the warm-up begins). Draws
        /// nothing before 0, from times.end, or when Pain's cast cell or the target's start cell is off the map or fogged.
        /// </summary>
        public static void Draw(in BanshoView v, float s, Map map)
        {
            BanshoTimes t = v.times;
            if (s < 0f || s >= t.end) return;
            Vector2 P0 = v.castAt.sqrMagnitude < 1e-8f ? v.pain : v.castAt, me = v.pain, aim = v.aim, back = aim, toward = -aim;
            Vector2 start = P0 + aim * t.distance;
            if (!Shown(P0, map) || !Shown(start, map)) return;
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            Begin((P0 + start) / 2f);

            Vector2 L = P0 + aim * T.LandGap, g = v.target;
            float h = v.height, a = Vector2.Dot(g - P0, aim), aimDeg = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            bool pulling = s >= t.grip && s < t.arrive, normal = t.Normal;
            float warmU = Mathf.Clamp01((s - t.cast) / t.warm), stay = T.Lasting(s, t);
            Color tint = t.heavy ? Beast : G.EnemyColour;

            // --- on the floor: the aim line while it is aimed, streaks, drag marks ---------------------------------
            if (s >= t.cast && s < t.grip + 0.1f)
            {
                float vis = Smooth(Mathf.Clamp01(warmU * 3f)) * (1f - Mathf.Clamp01((s - t.grip) / 0.1f));
                for (int i = 0; i < 12; i++)
                    ChainSickleGraphics.Rect(P0 + aim * (0.7f + (t.distance - 1.2f) * i / 12f + 0.1f), 0.2f, 0.035f, aimDeg,
                        Fade(G.PaleBlue, 0.4f * vis), Floor + 0.012f);
            }
            if (s >= t.grip && s < t.arrive + 0.25f) FloorStreaks(P0, aimDeg, t, s);
            // Scuffs and the drop crack are placed where the motion puts the target, so they stay if it moves on.
            if (t.heavy && s >= t.grip) ChainSickleGraphics.Scuff(start, P0 + aim * T.AlongAt(s, t), 1f, 0.45f * stay);
            // The tug: the feet scrape two short furrows, and a puff of dust where they leave the floor.
            Vector2 liftAt = P0 + aim * (t.distance - t.slide);
            if (!t.heavy && s >= t.grip) ChainSickleGraphics.Scuff(start, liftAt, Mathf.Clamp01((s - t.grip) / t.tug), 0.4f * stay);
            if (!t.heavy && s >= t.lift)
                for (int i = 0; i < 6; i++)
                {
                    float u = Mathf.Clamp01((s - t.lift) / (0.35f + Rand(i + 780) * 0.2f)), an = i * 1.05f + Rand(i + 781) * 0.5f,
                        d = 0.1f + T.EaseOut(u) * 0.45f;
                    if (u < 1f)
                        Sprite(new Vector2(liftAt.x + Mathf.Cos(an) * d, liftAt.y + Mathf.Sin(an) * d * 0.8f + u * 0.15f), 0.25f + u * 0.35f,
                            0.2f + u * 0.3f, Fade(G.DustC, 0.5f * (1f - u)), PowerPoleGraphics.puff, Y + 0.005f);
                }
            if (normal) Crater(L, s - t.down, T.Slabs, toward, sun, strength, stay);
            if (t.blocked && s >= t.down)
                Crack(P0 + aim * T.AlongAt(t.down, t), 0.9f * Smooth((s - t.down) / 0.08f), 21, stay);

            // --- stand-ins on the ground, north first; the pulled pawn in the air is drawn above them ----------------
            if (v.standIns) GroundStandIns(v, s, sun, strength, tint);

            // Pain's arm: up at the target during the warm-up with the fingers spread, held through the pull; at the
            // catch the fingers close on the head and the hand pushes it down.
            float armUp = Smooth((s - t.cast) / (t.warm * 0.6f)), armBack = Smooth((s - (normal ? t.down + 0.45f : t.arrive + 0.35f)) / 0.3f);
            float push = normal ? Smooth((s - t.arrive) / T.Catch) : 0f;
            float reach = G.Reach * armUp * (1f - armBack), handH = Mathf.Lerp(Mathf.Lerp(G.HandH, 0.32f, push), 0.38f, armBack);
            if (armUp > 0f && armBack < 1f)
                G.Arm(G.Place(me, aim, 0.05f, -0.1f, G.ShoulderH), G.Place(me, aim, 0.12f + reach, -0.1f, handH), back, push);

            // --- the pulled pawn in the air: its shadow, and as stand-ins two afterimages and the body ---------------
            if (T.Flying(s, t))
            {
                Sprite(g + sun * (0.3f + h), 0.8f, 0.4f, Fade(Ink, strength / (1f + h)), soft, G.ShadowLayer);
                if (v.standIns)
                {
                    float tilt = T.Tilt(s, t), blur = Mathf.Clamp01((s - t.lift) / 0.08f) * (1f - Mathf.Clamp01((s - t.arrive) / 0.04f));
                    for (int k = 2; k >= 1; k--)
                        Flying(g + aim * (0.32f * k), h, tint, back, tilt, s, (k == 1 ? 0.3f : 0.14f) * blur, Y + 0.015f + k * 0.001f);
                    Flying(g, h, tint, back, tilt, s, 1f, Y + 0.02f);
                }
            }

            // --- the force: the palm core, what holds the target ---------------------------------------------------
            Vector2 chest = t.heavy ? new Vector2(g.x, g.y + 0.45f) : new Vector2(g.x, g.y + 0.3f + h * Lift);
            // The core stretches toward the target as the pull takes hold (a dragged beast: as the drag starts), snaps
            // back round at the catch and goes into the grip in 0.1 s; after a drag it relaxes and fades.
            float caught = t.heavy ? 0f : Mathf.Clamp01((s - t.arrive) / 0.1f);
            float orbA = Smooth(Mathf.Clamp01(warmU * 1.4f)) * (t.heavy ? 1f - Mathf.Clamp01((s - t.arrive - 0.2f) / 0.15f) : 1f - caught);
            float stretch = (t.heavy ? Smooth((s - t.grip) / 0.2f) * (1f - Smooth((s - t.arrive) / 0.1f))
                : Smooth((s - t.grip) / t.tug) * (1f - Smooth((s - t.arrive) / 0.05f))) * (1f + 0.06f * Mathf.Sin(s * 40f));
            float strain = t.heavy && pulling ? 0.02f * Mathf.Sin(s * 70f) : 0f;
            Vector2 orbAt = G.Place(me, aim, 0.12f + reach + T.OrbGap + T.OrbR, -0.1f, handH);
            Vector2 point = PalmCore(new Vector2(orbAt.x + strain, orbAt.y), T.OrbR * Smooth(Mathf.Clamp01(warmU * 1.25f)) * (1f - 0.6f * Smooth(caught)),
                back, stretch, s, orbA);
            if (!t.heavy && s >= t.arrive && s < t.arrive + 0.15f)
            {
                // The snap back to round: a soft flash that spreads and fades.
                float u = (s - t.arrive) / 0.15f, size = T.OrbR * Mathf.Lerp(3f, 6.5f, T.EaseOut(u));
                Sprite(orbAt, size, size, Fade(G.PaleBlue, 0.6f * (1f - u)), glow, Y + 0.0995f);
            }
            Inflow(chest, point, s, Mathf.Clamp01((s - t.grip) / 0.1f) * (1f - Mathf.Clamp01((s - t.arrive) / 0.05f)));
            float holdA = s < t.grip ? Smooth(Mathf.Clamp01(warmU * 2f)) : 1f - Mathf.Clamp01((s - t.arrive) / 0.12f);
            float big = t.heavy ? 2.1f : 1f;
            float R = s < t.grip ? Mathf.Lerp(t.heavy ? 1.6f : 1.15f, T.BindR * big, Smooth(warmU)) : T.BindR * big * (1f + 0.04f * Mathf.Sin(s * 40f));
            Hold(chest, R, holdA);
            if (!t.heavy && s >= t.lift && s < t.arrive)
            {
                Trail(chest, back, t.distance - t.slide - a);
                SpeedLines(chest, back, s, Mathf.Clamp01((s - t.lift) / 0.05f));
            }
            if (!t.heavy && s >= t.grip && s < t.lift) ChainSickleGraphics.Kick(g, s - t.grip, 1f, 13);   // dust at the scraping feet
            if (t.heavy && pulling) ChainSickleGraphics.Kick(g, s - t.grip, 1f, 5);

            // --- the landing -----------------------------------------------------------------------------------------
            if (normal) Slam(L, s - t.down);
            if (t.blocked && s >= t.arrive)
            {
                Vector2 B = v.block, hit = new Vector2((g.x + B.x) / 2f, (g.y + B.y) / 2f + 0.35f);
                float age = s - t.arrive;
                Sprite(hit, 0.9f, 0.8f, Fade(G.PaleBlue, 0.8f * Mathf.Clamp01(1f - age / 0.12f)), glow, Y + 0.12f);
                for (int i = 0; i < 6; i++)
                {
                    float u = Mathf.Clamp01(age / (0.4f + Rand(i + 760) * 0.3f)), an = i * 1.05f + Rand(i + 761);
                    if (u < 1f)
                        Sprite(new Vector2(g.x + Mathf.Cos(an) * (0.2f + u * 0.6f), g.y + Mathf.Sin(an) * (0.2f + u * 0.6f) * 0.8f + u * 0.2f),
                            0.3f + u * 0.4f, 0.26f + u * 0.3f, Fade(G.DustC, 0.5f * (1f - u)), PowerPoleGraphics.puff, Y + 0.06f);
                }
            }
            if (t.heavy && s >= t.arrive) ChainSickleGraphics.Kick(g, s - t.arrive, 1f - Mathf.Clamp01((s - t.arrive) / 0.4f), 9);

            // Stunned: stars over the head of whoever was hit (the lying head for the target).
            float stunFor = normal ? t.stun : T.BlockStun;
            float sa = Mathf.Clamp01((s - t.down) / 0.15f) * Mathf.Clamp01((t.down + stunFor - s) / 0.2f);
            if (!t.heavy && s >= t.down && stunFor > 0f)
            {
                Vector2 head = new Vector2(g.x + toward.x * 0.4f, g.y + 0.08f + toward.y * 0.4f);
                GokuGraphics.StunStars(new Vector2(head.x, head.y + 0.22f - 0.84f), s, sa);
                if (t.blocked) GokuGraphics.StunStars(v.block, s, sa);
            }

            // The Rinnegan: a glint at Pain's eyes as the warm-up starts.
            AmenoyodomiGraphics.EyeStar(new Vector2(me.x, me.y + 0.6f), 0.28f, ChainSickleGraphics.Bump(Mathf.Clamp01((s - t.cast) / 0.35f)));
        }

        // ------------------------------------------------------------------ stand-ins (previews only)

        // Pain, the target on the ground (standing, leaning into the tug, or face-down) and the blocker, north first.
        private static void GroundStandIns(in BanshoView v, float s, Vector2 sun, float strength, Color tint)
        {
            BanshoTimes t = v.times;
            Vector2 g = v.target, B = v.block;
            // 0 Pain, 1 the target, 2 the blocker; -1 not drawn now.
            int target = t.heavy || s < t.lift || s >= t.down ? 1 : -1, blocker = t.blocked ? 2 : -1;
            int n = 0;
            int[] order = Order;
            float[] zs = OrderZ;
            order[n] = 0; zs[n++] = v.pain.y;
            if (target >= 0) { order[n] = 1; zs[n++] = g.y; }
            if (blocker >= 0) { order[n] = 2; zs[n++] = B.y; }
            // Stable, north (larger z) first.
            for (int i = 1; i < n; i++)
                for (int j = i; j > 0 && zs[j] > zs[j - 1]; j--)
                {
                    (zs[j], zs[j - 1]) = (zs[j - 1], zs[j]);
                    (order[j], order[j - 1]) = (order[j - 1], order[j]);
                }
            for (int i = 0; i < n; i++)
            {
                if (order[i] == 0) G.Pain(v.pain, sun, strength);
                else if (order[i] == 2)
                {
                    float shake = s >= t.arrive && s < t.arrive + 0.2f ? Mathf.Sin(s * 90f) * 0.03f : 0f;
                    G.Standing(new Vector2(B.x + shake, B.y), G.EnemyColour, sun, strength);
                }
                else if (t.heavy) BeastStandIn(g + new Vector2(-v.aim.y, v.aim.x) * T.HeavyJitter(s, t), 4f, sun, strength, tint);
                else if (s < t.grip) G.Standing(g, tint, sun, strength);
                else if (s < t.lift)
                {
                    Sprite(g + sun * 0.3f, 0.8f, 0.4f, Fade(Ink, strength), soft, G.ShadowLayer);
                    Flying(g, 0f, tint, v.aim, -T.TugLean * Smooth((s - t.grip) / t.tug), s, 1f, G.PawnLayer);
                }
                else G.Lying(g, tint, -v.aim, sun, strength);
            }
        }

        private static readonly int[] Order = new int[3];
        private static readonly float[] OrderZ = new float[3];

        /// <summary>
        /// Stand-in: a pawn pulled back-first, drawn Lift × h north of its ground point g. The body turns so the head
        /// trails (BanshoTiming.Head) and the arms trail behind the shoulders. Its shadow is drawn by the caller.
        /// </summary>
        private static void Flying(Vector2 g, float h, Color colour, Vector2 back, float tilt, float s, float alpha, float layer)
        {
            var mid = new Vector2(g.x, g.y + G.BodyZ + h * Lift);
            Vector2 hd = T.Head(tilt, back), p = new Vector2(-hd.y, hd.x);
            Vector2[] pts = GokuGraphics.Points(2);
            for (int k = 0; k < 2; k++)
            {
                float side = k == 0 ? -1f : 1f, wave = Mathf.Sin(s * 34f + side) * 0.05f;
                Vector2 sh = mid + hd * 0.16f + p * (side * 0.13f);
                pts[0] = sh;
                pts[1] = sh + back * 0.3f + p * (side * (0.1f + wave));
                GokuGraphics.Line(pts, 0.07f, Fade(G.Skin, alpha), solid, layer - 0.001f, GokuGraphics.Taper.None);
            }
            DrawMesh(disc, mid, layer, 0.22f, 0.32f, Mathf.Atan2(hd.x, hd.y) * Mathf.Rad2Deg, Fade(colour, alpha), solid);
            DrawMesh(disc, mid + hd * 0.4f, layer + 0.002f, 0.16f, 0.17f, 0f, Fade(G.Skin, alpha), solid);
        }

        // Stand-in animal: a long body disc and a head at the far end, scaled by body size (lib/chain-sickle.js beast).
        private static void BeastStandIn(Vector2 pos, float size, Vector2 sun, float strength, Color colour)
        {
            float k = Mathf.Sqrt(size);
            Sprite(pos + sun * (0.5f * k), 1.3f * k, 0.6f * k, Fade(Body, strength), soft, G.ShadowLayer);
            DrawMesh(disc, new Vector2(pos.x, pos.y + 0.2f * k), G.PawnLayer, 0.42f * k, 0.3f * k, 0f, colour, solid);
            DrawMesh(disc, new Vector2(pos.x + 0.34f * k, pos.y + 0.3f * k), G.PawnLayer + 0.002f, 0.17f * k, 0.15f * k, 0f,
                Color.Lerp(colour, G.Skin, 0.3f), solid);
        }

        /// <summary>
        /// Stand-in: a wall of three sandbag cells across the line, <paramref name="along"/> cells from Pain: a bottom
        /// course two bags deep and three across, a top course of two 0.24 cells up. Bags are drawn north first.
        /// </summary>
        public static void Sandbags(Vector2 P0, Vector2 aim, float along, Map map)
        {
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            float aimDeg = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg, turn = -(aimDeg + 90f);
            int n = 0;
            for (int c = -1; c <= 1; c++)
            {
                Vector2 g = G.Place(P0, aim, along, c);
                ChainSickleGraphics.Rect(g + sun * 0.3f, 0.8f, 1f, aimDeg, Fade(Ink, strength * 0.9f), G.ShadowLayer);
                for (int di = 0; di < 2; di++)
                    for (int b = 0; b < 3; b++)
                    {
                        bagAt[n] = G.Place(P0, aim, along + (di == 0 ? -0.17f : 0.17f), c + (b - 1) * 0.32f);
                        bagH[n++] = 0.1f;
                    }
                for (int b = 0; b < 2; b++)
                {
                    bagAt[n] = G.Place(P0, aim, along, c + (b - 0.5f) * 0.32f);
                    bagH[n++] = 0.24f;
                }
            }
            // Stable sort: lower course first, then north first.
            for (int i = 1; i < n; i++)
                for (int j = i; j > 0 && (bagH[j] < bagH[j - 1] || (bagH[j] == bagH[j - 1] && bagAt[j].y > bagAt[j - 1].y)); j--)
                {
                    (bagH[j], bagH[j - 1]) = (bagH[j - 1], bagH[j]);
                    (bagAt[j], bagAt[j - 1]) = (bagAt[j - 1], bagAt[j]);
                }
            for (int i = 0; i < n; i++)
            {
                float lay = BuildingLayer + i * 0.0004f, z = bagAt[i].y + bagH[i] * Lift;
                DrawMesh(disc, new Vector2(bagAt[i].x, z), lay, 0.19f, 0.14f, turn, BagDark, solid);
                DrawMesh(disc, new Vector2(bagAt[i].x - 0.012f, z + 0.025f), lay + 0.0002f, 0.16f, 0.1f, turn, Bag, solid);
            }
        }

        // ------------------------------------------------------------------ the force

        /// <summary>
        /// The black core past the palm (Naruto Mobile's): a black hole in a soft pale glow, no hard line on it, its edge
        /// wobbling. <paramref name="stretch"/> 1 pulls it into a teardrop 1.6 times as long as wide, its point along
        /// <paramref name="dir"/>, the round back staying put; the glow brightens and stretches with it. Returns the
        /// point, where the streaks flow in.
        /// </summary>
        private static Vector2 PalmCore(Vector2 c, float r, Vector2 dir, float stretch, float s, float alpha)
        {
            float tip = r * (1f + 1.2f * stretch);
            Vector2 point = c + dir * tip;
            if (r <= 0.005f || alpha <= 0f) return point;
            float deg = -Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, pull = 0.7f + 0.3f * stretch;
            Vector2 mid = c + dir * ((tip - r) * 0.5f);
            Sprite(mid, r * 5.6f + tip - r, r * 5.6f, Fade(G.PaleBlue, 0.35f * alpha * pull), glow, Y + 0.1f, deg);                  // wide faint glow
            Sprite(mid, r * 3.4f + (tip - r) * 1.1f, r * 3.4f, Fade(G.PaleBlue, 0.8f * alpha * pull), glow, Y + 0.1005f, deg);       // the glow it sits in
            Sprite(mid, r * 2.5f + (tip - r) * 1.05f, r * 2.5f, Fade(G.PaleBlue, 0.9f * alpha * pull), glow, Y + 0.1008f, deg);      // bright soft edge
            core[0] = c;
            for (int i = 0; i <= CorePoints; i++)
            {
                float an = i / (float)CorePoints * Tau, front = Mathf.Max(0f, Mathf.Cos(an));
                float wob = 1f + 0.03f * Mathf.Sin(2f * an + s * 4f) + 0.02f * Mathf.Sin(3f * an - s * 6f);
                float x = r * wob * Mathf.Cos(an) + (tip - r) * Mathf.Pow(front, 1.5f), y = r * wob * Mathf.Sin(an) * (1f - 0.25f * front * stretch);
                core[i + 1] = new Vector2(c.x + dir.x * x - dir.y * y, c.y + dir.y * x + dir.x * y);
            }
            G.Poly(core, CorePoints + 2, Fade(G.Core, alpha), Y + 0.102f);
            return point;
        }

        /// <summary>
        /// Ten dark streaks in the air from the target's chest to the core's point, spread round the target and
        /// converging (Storm 4's streaks that close on the catch point). Each a thin core in a wider faint haze.
        /// </summary>
        private static void Inflow(Vector2 from, Vector2 to, float s, float alpha)
        {
            Vector2 d = to - from;
            float D = d.magnitude;
            if (D < 0.35f || alpha <= 0f) return;
            var p = new Vector2(-d.y / D, d.x / D);
            for (int i = 0; i < 10; i++)
            {
                float ph = (s * 2.4f + Rand(i + 900)) % 1f, off = (Rand(i + 901) - 0.5f) * 1.2f, len = Mathf.Min(0.55f * D, 0.6f + Rand(i + 902) * 0.6f) / D;
                float u0 = Mathf.Max(0f, ph - len), fade = alpha * Mathf.Max(0f, Mathf.Sin(ph * Mathf.PI));
                Vector2 a0 = from + d * u0 + p * (off * Mathf.Pow(1f - u0, 1.3f)), a1 = from + d * ph + p * (off * Mathf.Pow(1f - ph, 1.3f));
                Streak(a0, a1, 0.09f, Fade(G.Core, 0.12f * fade), solid, Y + 0.029f, 4);
                Streak(a0, a1, 0.026f, Fade(G.Core, 0.42f * fade), solid, Y + 0.03f, 4);
            }
        }

        /// <summary>What holds the target (Nagato's pull): a see-through black sphere round the chest, darkest at its edge.</summary>
        private static void Hold(Vector2 c, float R, float alpha)
        {
            if (alpha <= 0f) return;
            DrawMesh(disc, c, Y + 0.04f, R, R, 0f, Fade(G.Core, 0.34f * alpha), solid);
            // Darkest at the rim, fading inward over a quarter of the radius: six stacked bands in small steps.
            for (int k = 0; k < EdgeBands.Length; k++)
                DrawMesh(EdgeBands[k], c, Y + 0.041f + k * 0.0002f, R, R, 0f, Fade(G.Core, EdgeAlpha[k] * alpha), solid);
        }

        /// <summary>Dark smoke behind the pulled pawn, up to 1.8 cells, as long as it has flown.</summary>
        private static void Trail(Vector2 chest, Vector2 back, float gone)
        {
            float len = Mathf.Min(1.8f, gone);
            if (len < 0.05f) return;
            Vector2[] pts = GokuGraphics.Points(9);
            for (int i = 0; i <= 8; i++)
            {
                float u = i / 8f, wob = Mathf.Sin(u * 7f + gone * 3f) * 0.04f * u;
                pts[i] = new Vector2(chest.x + back.x * len * u - back.y * wob, chest.y + back.y * len * u + back.x * wob);
            }
            GokuGraphics.Line(pts, 0.36f, Fade(G.Core, 0.3f), solid, Y + 0.016f, GokuGraphics.Taper.End);
        }

        /// <summary>Six pale speed lines behind the pawn, at its height, flickering.</summary>
        private static void SpeedLines(Vector2 chest, Vector2 back, float s, float alpha)
        {
            var p = new Vector2(-back.y, back.x);
            for (int i = 0; i < 6; i++)
            {
                float c = (i - 2.5f) * 0.13f + (Rand(i + 500) - 0.5f) * 0.06f, st = 0.3f + Rand(i + 501) * 0.25f, len = 0.5f + Rand(i + 502) * 0.8f;
                float flick = 0.55f + 0.45f * Mathf.Sin(s * 47f + i * 2.3f);
                Vector2 a = chest + back * st + p * c;
                Streak(a, a + back * len, 0.04f, Fade(G.PaleBlue, 0.6f * flick * alpha), whiteGlow, Y + 0.018f, 3);
            }
        }

        /// <summary>
        /// Sixteen dust streaks on the floor in a 44-degree fan round the pull line, sliding in toward Pain at 0.65 of
        /// the pull's speed: the anime's speed lines, laid on the ground.
        /// </summary>
        private static void FloorStreaks(Vector2 P0, float aimDeg, in BanshoTimes t, float s)
        {
            float span = t.distance + 1.2f, v = t.speed * (t.heavy ? T.HeavySpeed : 1f) * 0.65f;
            for (int i = 0; i < 16; i++)
            {
                float born = t.grip + Rand(i + 300) * (t.tug + t.fly) * 0.9f, life = 0.22f + Rand(i + 301) * 0.14f, age = s - born;
                if (age < 0f || age > life) continue;
                Vector2 way = Turn(aimDeg + (Rand(i + 302) - 0.5f) * 44f);
                float r0 = 0.6f + Rand(i + 303) * span - v * age;
                if (r0 < 0.45f) continue;
                float r1 = r0 + (0.5f + Rand(i + 304) * 0.9f) * (1f - 0.5f * age / life);
                Streak(P0 + way * r0, P0 + way * r1, 0.06f, Fade(StreakC, 0.4f * Mathf.Max(0f, Mathf.Sin(age / life * Mathf.PI))), solid, Floor + 0.016f, 4);
            }
        }

        // ------------------------------------------------------------------ the slam

        /// <summary>The slam at <paramref name="L"/>: a pale flash, eight pale rays for 0.2 s, a dust ring that runs out and rises, a cloud that hangs 1.7 s.</summary>
        private static void Slam(Vector2 L, float age)
        {
            if (age < 0f) return;
            float fl = Mathf.Clamp01(1f - age / 0.14f);
            Sprite(new Vector2(L.x, L.y + 0.25f), 1.7f, 1.3f, Fade(G.PaleBlue, 0.7f * fl), glow, Y + 0.12f);
            if (age < 0.2f)
                for (int i = 0; i < 8; i++)
                {
                    float an = (i * 45f + 22f + (Rand(i + 700) - 0.5f) * 20f) * Mathf.Deg2Rad, r0 = 0.25f + age * 3.2f, r1 = r0 + 0.45f + Rand(i + 701) * 0.45f;
                    var way = new Vector2(Mathf.Cos(an), Mathf.Sin(an));
                    var c = new Vector2(L.x, L.y + 0.2f);
                    Streak(c + way * r0, c + way * r1, 0.08f, Fade(G.PaleBlue, Mathf.Clamp01(1f - age / 0.2f)), whiteGlow, Y + 0.13f, 4);
                }
            for (int i = 0; i < 14; i++)
            {
                float life = 0.6f + Rand(i + 740) * 0.4f, u = age / life;
                if (u > 1f) continue;
                float an = i / 14f * Tau + Rand(i + 741) * 0.4f, d = 0.3f + T.EaseOut(u) * (0.7f + Rand(i + 742) * 0.7f), hh = u * 0.3f;
                Sprite(new Vector2(L.x + Mathf.Cos(an) * d, L.y + Mathf.Sin(an) * d * 0.85f + hh * Lift), 0.42f + u * 0.6f, 0.36f + u * 0.5f,
                    Fade(G.DustC, 0.55f * (1f - u) * Mathf.Clamp01(u * 10f)), PowerPoleGraphics.puff, Y + 0.06f);
            }
            for (int i = 0; i < 4; i++)
            {
                float u = Mathf.Clamp01((age - 0.05f) / 1.7f);
                if (u <= 0f || u >= 1f) continue;
                float an = i * 1.7f + 0.4f, d = 0.25f + u * 0.4f;
                Sprite(new Vector2(L.x + Mathf.Cos(an) * d, L.y + 0.15f + Mathf.Sin(an) * d * 0.6f + u * 0.5f), 0.9f + u * 0.9f, 0.75f + u * 0.8f,
                    Fade(G.DustC, 0.35f * Mathf.Sin(u * Mathf.PI)), PowerPoleGraphics.puff, Y + 0.07f);
            }
        }

        /// <summary>
        /// Cracked floor: six short dark radial lines and a dark patch (lib/chain-sickle.js crack), with
        /// <paramref name="alpha"/> to fade them out.
        /// </summary>
        private static void Crack(Vector2 pos, float amount, int seed, float alpha)
        {
            if (amount <= 0f || alpha <= 0f) return;
            for (int i = 0; i < 6; i++)
            {
                float an = i / 6f * Tau + Rand(i + seed + 400) * 0.6f, len = (0.18f + Rand(i + seed + 410) * 0.2f) * amount;
                var dir = new Vector2(Mathf.Cos(an), Mathf.Sin(an));
                Vector2 p0 = pos + dir * 0.1f, p1 = pos + dir * (0.1f + len);
                T3[0] = p0;
                T3[1] = (p0 + p1) / 2f + new Vector2(Mathf.Sin(an) * 0.03f, -Mathf.Cos(an) * 0.03f);
                T3[2] = p1;
                ChainSickleGraphics.Tube(T3, 3, 0.035f, 0.007f, Fade(Body, Mathf.Min(1f, 0.6f * amount) * alpha), Floor + 0.0201f + i * 0.0001f);
            }
            Sprite(pos, 0.5f * amount, 0.32f * amount, Fade(Body, Mathf.Min(1f, 0.35f * amount) * alpha), soft, Floor + 0.015f);
        }

        /// <summary>
        /// The crater at <paramref name="L"/>: a dent, cracks, <paramref name="count"/> plates round it with an 80-degree
        /// gap on Pain's side (<paramref name="toward"/>), nine rocks thrown out 0.6 to 1.3 cells.
        /// </summary>
        private static void Crater(Vector2 L, float age, int count, Vector2 toward, Vector2 sun, float strength, float alpha)
        {
            if (age < 0f || alpha <= 0f) return;
            float e = Smooth(age / 0.09f);
            Sprite(L, 1.35f * e, 1.0f * e, Fade(InkBlack, 0.3f * alpha), soft, Floor + 0.02f);
            Crack(L, 1.7f * e, 17, alpha);
            float gap = Mathf.Atan2(toward.y, toward.x);
            count = Mathf.Min(count, slabs.Length);
            for (int k = 0; k < count; k++)
            {
                float th = gap + (40f + (k + 0.5f + (Rand(k * 7 + 1) - 0.5f) * 0.5f) / count * 280f) * Mathf.Deg2Rad;
                // South plates stand more upright: leaning south moves the top south as fast as height moves it north,
                // so a south plate at the north plates' lean would fold into a line.
                float rise = Smooth((age - Rand(k * 7 + 6) * 0.04f) / 0.08f), south = Mathf.Max(0f, -Mathf.Sin(th)), r0 = 0.5f + Rand(k * 7 + 2) * 0.15f;
                slabs[k] = new Slab
                {
                    k = k, th = th, r0 = r0, w = 0.15f + Rand(k * 7 + 3) * 0.09f, len = (0.22f + Rand(k * 7 + 4) * 0.18f) * rise,
                    tau = (40f + Rand(k * 7 + 5) * 15f + 30f * south) * Mathf.Deg2Rad, z = L.y + Mathf.Sin(th) * r0, stone = k % 3 == 1,
                    j0 = Rand(k * 13 + 90) - 0.5f, j1 = Rand(k * 13 + 91) - 0.5f, j2 = Rand(k * 13 + 92) - 0.5f, j3 = Rand(k * 13 + 93) - 0.5f,
                };
            }
            // Stable, north first.
            for (int i = 1; i < count; i++)
                for (int j = i; j > 0 && slabs[j].z > slabs[j - 1].z; j--) (slabs[j], slabs[j - 1]) = (slabs[j - 1], slabs[j]);
            for (int i = 0; i < count; i++) DrawSlab(L, slabs[i], sun, strength, Y + 0.03f + i * 0.002f, alpha);
            for (int i = 0; i < 9; i++)
            {
                float an = Rand(i * 5 + 50) * Tau, far = 0.6f + Rand(i * 5 + 51) * 0.7f, u = Mathf.Clamp01(age / (0.3f + Rand(i * 5 + 52) * 0.15f));
                float d = far * T.EaseOut(u), hh = 0.4f * Mathf.Max(0f, Mathf.Sin(u * Mathf.PI));
                PaperBombGraphics.Rock(new Vector2(L.x + Mathf.Cos(an) * d, L.y + Mathf.Sin(an) * d + hh * Lift), 0.09f + Rand(i * 5 + 53) * 0.07f,
                    an * Mathf.Rad2Deg + u * 200f, alpha, i, u < 1f ? Y + 0.025f : Floor + 0.03f);
            }
        }

        /// <summary>
        /// One plate of floor broken up by the slam: base on the floor, the torn top (four points at different heights)
        /// raised and leaning out, earth on most, stone on every third; lit by how far its upper face turns to the sun.
        /// </summary>
        private static void DrawSlab(Vector2 L, in Slab sl, Vector2 sun, float strength, float layer, float alpha)
        {
            if (sl.len <= 0.005f) return;
            var r = new Vector2(Mathf.Cos(sl.th), Mathf.Sin(sl.th));
            var tn = new Vector2(-r.y, r.x);
            Vector2 c = L + r * sl.r0;
            float outD = sl.len * Mathf.Cos(sl.tau), up = sl.len * Mathf.Sin(sl.tau);
            face[0] = c + tn * sl.w;
            face[1] = c - tn * sl.w;
            float k2 = -0.85f, f2 = 0.95f + 0.1f * sl.j0, k3 = -0.3f, f3 = 1.05f + 0.15f * sl.j1, k4 = 0.3f, f4 = 0.8f + 0.15f * sl.j2, k5 = 0.8f, f5 = 1f + 0.12f * sl.j3;
            face[2] = SlabPoint(c, tn, r, sl.w, outD, up, k2, f2, false, sun);
            face[3] = SlabPoint(c, tn, r, sl.w, outD, up, k3, f3, false, sun);
            face[4] = SlabPoint(c, tn, r, sl.w, outD, up, k4, f4, false, sun);
            face[5] = SlabPoint(c, tn, r, sl.w, outD, up, k5, f5, false, sun);
            cast[0] = face[0];
            cast[1] = face[1];
            cast[2] = SlabPoint(c, tn, r, sl.w, outD, up, k2, f2, true, sun);
            cast[3] = SlabPoint(c, tn, r, sl.w, outD, up, k3, f3, true, sun);
            cast[4] = SlabPoint(c, tn, r, sl.w, outD, up, k4, f4, true, sun);
            cast[5] = SlabPoint(c, tn, r, sl.w, outD, up, k5, f5, true, sun);
            G.Poly(cast, 6, Fade(Ink, strength * alpha), G.ShadowLayer);
            // Lit by how far its upper face turns to the light (the light comes from against the shadows).
            float st = Mathf.Sin(sl.tau), nx = -r.x * st, nz = -r.y * st, ny = Mathf.Cos(sl.tau), lx = -sun.x, lz = -sun.y;
            float lit = Mathf.Clamp01((nx * lx + ny + nz * lz) / Mathf.Sqrt(lx * lx + 1f + lz * lz));
            Color dark = sl.stone ? StoneDark : EarthDark, light = sl.stone ? StoneLit : EarthLit;
            // The dark edge: the face grown 0.025 outward from its centre.
            Vector2 mid = Vector2.zero;
            for (int i = 0; i < 6; i++) mid += face[i];
            mid /= 6f;
            for (int i = 0; i < 6; i++)
            {
                Vector2 o = face[i] - mid;
                float l = o.magnitude;
                grown[i] = l > 0f ? face[i] + o / l * 0.025f : face[i];
            }
            G.Poly(grown, 6, Fade(SlabEdge, alpha), layer);
            G.Poly(face, 6, Fade(Color.Lerp(dark, light, 0.15f + 0.7f * lit), alpha), layer + 0.0005f);
            Vector2[] top = GokuGraphics.Points(4);
            for (int i = 0; i < 4; i++) top[i] = face[i + 2];
            GokuGraphics.Line(top, 0.035f, Fade(light, (0.3f + 0.5f * lit) * alpha), solid, layer + 0.001f, GokuGraphics.Taper.None);
        }

        // A point of a plate: k across (share of its half-width), f up its length; drawn raised north, or its shadow along the sun.
        private static Vector2 SlabPoint(Vector2 c, Vector2 tn, Vector2 r, float w, float outD, float up, float k, float f, bool shadow, Vector2 sun)
        {
            Vector2 q = c + tn * (w * k) + r * (outD * f);
            return shadow ? q + sun * (up * f) : new Vector2(q.x, q.y + up * f * Lift);
        }
    }
}
