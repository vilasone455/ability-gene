using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>Where the gauntlet's parts are on screen, as <see cref="FlameGauntletGraphics.Gauntlet"/> placed them.</summary>
    internal struct FlameGauntletPose
    {
        /// <summary>The wrist, the open palm, the elbow and the fist, all drawn points (height already added).</summary>
        public Vector2 Hand, Palm, Elbow, Fist;
        /// <summary>The two elbow vents: the -1 side, then the +1 side.</summary>
        public Vector2 VentA, VentB;
        /// <summary>The aim on screen, a unit vector.</summary>
        public Vector2 D;
        public float Layer;
    }

    /// <summary>
    /// The drawing pieces shared by the Flame Gauntlet's Devour and Release: the gauntlet and its
    /// heat glow, a flame tongue, a burning cell, the scorch, a burning pawn, a parcel of fire in
    /// flight, the Overheating state, the heat tally, and Release's jet, ground wave, soot, sheet,
    /// vent exhaust and knuckle wisp. The port of Tools/VfxLab/web/sketches/lib/flame-gauntlet.js;
    /// its numbers are that file's. The sketches' stand-in pawns are not drawn.
    ///
    /// Differences from the sketch:
    /// - A flame tongue is three nested quads of Textures/RimArt/FlameGauntlet/Tongue.png (the
    ///   sketch's width curve in the alpha) instead of three strips rebuilt every frame. Each quad
    ///   stands on the tongue's base and is turned about it so its tip lands where the sketch's
    ///   swaying, leaning tip does; the sketch bends the upper part, the quad stays straight.
    /// - A fire cell's tongues are ordered by their height within the cell, not by world z, so the
    ///   order against the other layers does not change with the map position.
    /// - The jet's five licks are short-lived strips, as in the sketch.
    ///
    /// Ground points are Vector2 (x east, y north). Height is drawn Lift cells north per cell up.
    /// Call VfxDraw.Begin first.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class FlameGauntletGraphics
    {
        // Palette: blackened iron with brass seams; fire is ember red under orange under a yellow-white core.
        internal static readonly Color Iron = new Color(0.16f, 0.15f, 0.16f), IronLit = new Color(0.34f, 0.32f, 0.34f), IronDark = new Color(0.05f, 0.05f, 0.06f);
        internal static readonly Color Brass = new Color(0.62f, 0.48f, 0.22f), BrassLit = new Color(0.86f, 0.72f, 0.40f);
        internal static readonly Color Ember = new Color(0.86f, 0.22f, 0.05f), Flame = new Color(1f, 0.55f, 0.10f), Core = new Color(1f, 0.92f, 0.60f);
        internal static readonly Color Hot = new Color(1f, 0.30f, 0.12f), Smoke = new Color(0.30f, 0.28f, 0.27f), Char = new Color(0.09f, 0.07f, 0.06f);
        internal static readonly Color Soot = new Color(0.24f, 0.22f, 0.21f), Steam = new Color(0.80f, 0.78f, 0.76f);
        internal static Color Body => ChainSickleGraphics.Body;

        /// <summary>Hand height, cells.</summary>
        internal const float HandH = 0.5f;
        /// <summary>The forearm plate, elbow to wrist, and its width.</summary>
        internal const float ForearmLen = 0.42f, ForearmW = 0.15f;
        internal const float Lift = SixPathsHeight.Lift;

        internal static float Y => ChainSickleGraphics.Y;
        internal static float ShadowLayer => ChainSickleGraphics.ShadowLayer;
        internal static float PawnLayer => ChainSickleGraphics.PawnLayer;

        /// <summary>The flame silhouette: white, alpha is the shape; base at the bottom centre.</summary>
        internal static readonly Material tongue = MaterialPool.MatFrom("RimArt/FlameGauntlet/Tongue", ShaderDatabase.Transparent);
        /// <summary>Tongue.png: the base sits this share of the height above the bottom edge, the tip this share above the base.</summary>
        private const float TongueFoot = 0.03f, TongueSpan = 0.95f;
        /// <summary>Tongue.png's half-width where the width curve is 1, as a share of the image width.</summary>
        private const float TongueHalf = 0.44f;
        /// <summary>The most a tongue quad turns from upright, degrees.</summary>
        private const float MostTilt = 35f;

        private static readonly Vector2[] Lick = new Vector2[9];

        internal static float Clamp01(float x) => Mathf.Clamp01(x);
        internal static float EaseOut(float x) => ChainSickleGraphics.EaseOut(x);
        internal static float Bump(float x) => ChainSickleGraphics.Bump(x);
        internal static Vector2 Raised(Vector2 ground, float h) => new Vector2(ground.x, ground.y + h * Lift);

        internal static void Disc(Vector2 at, float altitude, float width, float depth, Color colour) =>
            DrawMesh(disc, at, altitude, width, depth, 0f, colour, solid);

        /// <summary>A quad <paramref name="length"/> long along <paramref name="degrees"/> (0 east, 90 north) and <paramref name="width"/> across.</summary>
        internal static void Rect(Vector2 at, float length, float width, float degrees, Color colour, float altitude) =>
            Sprite(at, length, width, colour, solid, altitude, -degrees);

        // ------------------------------------------------------------------ fire

        /// <summary>
        /// One tongue of flame rising <paramref name="h"/> cells from <paramref name="at"/> (a drawn
        /// point), <paramref name="w"/> its half-width, swaying with the clip time: an additive glow at
        /// the base and three nested quads, ember over orange over the pale core. <paramref name="lean"/>
        /// tilts it by that many cells per cell of height.
        /// </summary>
        internal static void Tongue(Vector2 at, float h, float w, float s, int seed, float alpha, float layer, Vector2 lean = default)
        {
            if (h <= 0.01f || alpha <= 0f) return;
            float flick = 1f + 0.12f * Mathf.Sin(s * 13f + seed * 5.1f) + 0.06f * Mathf.Sin(s * 23f + seed * 2.7f);
            // The sketch's sideways sway at the tip (u = 1); it grows with u squared up the tongue.
            float sway = (Mathf.Sin(s * 7f + seed * 6.3f + 2.2f) * 0.10f + Mathf.Sin(s * 11.5f + seed) * 0.04f) * w * 4f;
            Sprite(at, w * 3.2f, w * 2.2f, Fade(Flame, 0.35f * alpha), glow, layer - 0.001f);
            TongueQuad(at, h * flick, w, sway, lean, Fade(Ember, 0.9f * alpha), layer);
            TongueQuad(at, h * 0.8f * flick, w * 0.62f, sway, lean, Fade(Flame, 0.95f * alpha), layer + 0.001f);
            TongueQuad(at, h * 0.55f * flick, w * 0.32f, sway, lean, Fade(Core, 0.95f * alpha), layer + 0.002f);
        }

        /// <summary>One Tongue.png quad: its base on <paramref name="at"/>, its tip where the sketch's spine ends.</summary>
        private static void TongueQuad(Vector2 at, float hh, float half, float sway, Vector2 lean, Color colour, float layer)
        {
            var tip = new Vector2(sway + lean.x * hh, hh * Lift + lean.y * hh);
            if (tip.y < 1e-4f) return;
            // A short tongue would lie down under the sway, which the sketch's strip only bends; the
            // turn is held to MostTilt degrees from upright.
            float tilt = Mathf.Clamp(Mathf.Atan2(tip.x, tip.y) * Mathf.Rad2Deg, -MostTilt, MostTilt);
            var up = new Vector2(Mathf.Sin(tilt * Mathf.Deg2Rad), Mathf.Cos(tilt * Mathf.Deg2Rad));
            float tall = tip.y / up.y / TongueSpan;
            Vector2 centre = at + up * (tall * (0.5f - TongueFoot));
            Sprite(centre, half / TongueHalf, tall, colour, tongue, layer, tilt);
        }

        /// <summary>
        /// A burning map cell: the scorch under it (<paramref name="scorch"/>), an additive glow, five
        /// tongues from points inside the cell and four embers drifting up. <paramref name="amount"/> 0..1
        /// scales the fire; <paramref name="flare"/> makes the tongues that much taller and the glow
        /// brighter; <paramref name="alpha"/> fades the fire (not the scorch).
        /// </summary>
        internal static void FireCell(Vector2 c, float s, float amount, int seed, float layer, float flare = 0f, bool scorch = true, float alpha = 1f)
        {
            if (scorch) Scorch(c, 1f);
            if (amount <= 0f || alpha <= 0f) return;
            Sprite(new Vector2(c.x, c.y + 0.1f), (1.4f * amount + 0.3f) * (1f + 0.3f * flare), (1.1f * amount + 0.3f) * (1f + 0.3f * flare),
                Fade(Flame, 0.30f * amount * (1f + flare) * alpha), glow, Floor + 0.012f);
            for (int i = 0; i < 5; i++)
            {
                float r1 = Rand(seed * 17 + i), r2 = Rand(seed * 17 + i + 40);
                var at = new Vector2(c.x + (r1 - 0.5f) * 0.62f, c.y + (r2 - 0.5f) * 0.55f);
                float h = (0.32f + Rand(seed * 17 + i + 80) * 0.38f) * amount * (1f + flare);
                float w = (0.11f + Rand(seed * 17 + i + 120) * 0.06f) * (0.5f + 0.5f * amount) * (1f + 0.25f * flare);
                Tongue(at, h, w, s, seed * 7 + i, alpha, layer + i * 0.0005f - (at.y - c.y) * 1e-4f);
            }
            // Embers drifting up out of the fire.
            for (int i = 0; i < 4; i++)
            {
                float life = 0.9f + Rand(seed + i + 300) * 0.5f, u = (s * 0.8f + Rand(seed + i + 310) * 3f) % life / life;
                float x = c.x + (Rand(seed + i + 320) - 0.5f) * 0.5f + Mathf.Sin(u * 9f + i) * 0.06f, h = u * (0.9f + Rand(seed + i + 330) * 0.4f);
                Sprite(new Vector2(x, c.y + h * Lift), 0.06f, 0.06f, Fade(Core, (1f - u) * 0.9f * amount * alpha), glow, Y + 0.12f);
            }
        }

        /// <summary>The black mark a fire leaves on a cell. Stays.</summary>
        internal static void Scorch(Vector2 c, float alpha = 1f)
        {
            Sprite(c, 1.05f, 0.95f, Fade(Char, 0.55f * alpha), soft, Floor + 0.006f);
            Sprite(new Vector2(c.x + 0.12f, c.y - 0.08f), 0.55f, 0.45f, Fade(Char, 0.5f * alpha), PowerPoleGraphics.puff, Floor + 0.007f);
            Sprite(new Vector2(c.x - 0.15f, c.y + 0.1f), 0.5f, 0.4f, Fade(Char, 0.45f * alpha), PowerPoleGraphics.puff, Floor + 0.007f);
        }

        private static readonly Vector3[] PawnSpots =
        {
            new Vector3(-0.12f, 0.12f, 0.45f), new Vector3(0.10f, 0.22f, 0.55f), new Vector3(0.02f, 0.42f, 0.40f), new Vector3(-0.06f, 0.55f, 0.35f),
        };

        /// <summary>Fire on a standing pawn: a glow and tongues from the hips, chest and a shoulder. <paramref name="amount"/> 0..1.</summary>
        internal static void BurningPawn(Vector2 pos, float s, float amount = 1f, int seed = 5)
        {
            if (amount <= 0f) return;
            Sprite(new Vector2(pos.x, pos.y + 0.3f), 0.9f * amount + 0.2f, 1.0f * amount + 0.2f, Fade(Flame, 0.30f * amount), glow, PawnLayer + 0.02f);
            for (int i = 0; i < PawnSpots.Length; i++)
            {
                Vector3 q = PawnSpots[i];
                Tongue(new Vector2(pos.x + q.x, pos.y + q.y), q.z * amount, 0.11f * (0.5f + 0.5f * amount), s, seed * 3 + i, 1f, PawnLayer + 0.03f + i * 0.001f);
            }
        }

        /// <summary>
        /// A parcel of fire in flight from <paramref name="from"/> to <paramref name="to"/> (drawn
        /// points) along an arch, <paramref name="u"/> 0..1 of the way: a core head and an ember tail.
        /// Returns the head.
        /// </summary>
        internal static Vector2 Parcel(Vector2 from, Vector2 to, float u, float size = 1f)
        {
            for (int i = 5; i >= 0; i--)
            {
                float q = Clamp01(u - i * 0.045f), k = 1f - i / 6f;
                Vector2 pt = Arch(from, to, q, size);
                Sprite(pt, 0.34f * k * size, 0.30f * k * size, Fade(Ember, 0.55f * k), soft, Y + 0.14f + i * 0.0005f);
                Sprite(pt, 0.26f * k * size, 0.22f * k * size, Fade(Flame, 0.9f * k), glow, Y + 0.141f + i * 0.0005f);
            }
            Vector2 head = Arch(from, to, u, size);
            Sprite(head, 0.18f * size, 0.16f * size, Fade(Core, 1f), glow, Y + 0.145f);
            Sprite(head, 0.5f * size, 0.4f * size, Fade(Flame, 0.35f), glow, Y + 0.146f);
            return head;
        }

        private static Vector2 Arch(Vector2 from, Vector2 to, float q, float size) =>
            new Vector2(Mathf.Lerp(from.x, to.x, q), Mathf.Lerp(from.y, to.y, q) + Mathf.Sin(q * Mathf.PI) * 0.55f * Lift * size);

        // ------------------------------------------------------------------ the gauntlet

        /// <summary>Where the parts go for a wrist drawn at <paramref name="p"/>, pointing <paramref name="degrees"/>.</summary>
        internal static FlameGauntletPose Pose(Vector2 p, float degrees, float? layer = null)
        {
            Vector2 d = Turn(degrees);
            return new FlameGauntletPose
            {
                Hand = p, D = d,
                Layer = layer ?? (d.y > 0f ? PawnLayer - 0.03f : Y + 0.05f),
                Elbow = Along(p, d, -ForearmLen), Palm = Along(p, d, 0.14f), Fist = Along(p, d, 0.05f),
                VentA = Along(p, d, -ForearmLen * 0.85f, -ForearmW * 0.28f), VentB = Along(p, d, -ForearmLen * 0.85f, ForearmW * 0.28f),
            };
        }

        /// <summary>A point <paramref name="along"/> the aim and <paramref name="side"/> across it (left of the aim is +).</summary>
        private static Vector2 Along(Vector2 at, Vector2 d, float along, float side = 0f) =>
            new Vector2(at.x + d.x * along - d.y * side, at.y + d.y * along + d.x * side);

        /// <summary>
        /// The gauntlet on the hand, lying level at hand height and turned with the aim.
        /// <paramref name="hand"/> is the ground point under the wrist. <paramref name="open"/> 0..1
        /// spreads the fingers into the maw (Devour); <paramref name="lean"/> 0..1 slides the heat glow
        /// to the fist (Release wind-up). It draws under the pawn layer when it points north.
        /// </summary>
        internal static FlameGauntletPose Gauntlet(Vector2 hand, float degrees, float heat, float maxHeat, float overheatAt, float s,
            Vector2 sun, float strength, float open = 0f, float lean = 0f, float alpha = 1f)
        {
            FlameGauntletPose g = Pose(Raised(hand, HandH), degrees);
            Vector2 p = g.Hand, d = g.D, sd = hand + sun * HandH;
            float layer = g.Layer;

            // Shadow: forearm and fist in one dark tone on the floor.
            Rect(Along(sd, d, -ForearmLen / 2f), ForearmLen, ForearmW, degrees, Fade(Body, strength * 0.6f * alpha), ShadowLayer);
            Disc(sd + d * 0.06f, ShadowLayer + 0.001f, 0.13f, 0.12f, Fade(Body, strength * 0.6f * alpha));

            // Forearm: a dark iron plate with a lit top strip, three brass seams and two vents at the elbow.
            Rect(Along(p, d, -ForearmLen / 2f), ForearmLen + 0.03f, ForearmW + 0.03f, degrees, Fade(IronDark, alpha), layer);
            Rect(Along(p, d, -ForearmLen / 2f), ForearmLen, ForearmW, degrees, Fade(Iron, alpha), layer + 0.001f);
            Rect(Along(p, d, -ForearmLen / 2f, ForearmW * 0.22f), ForearmLen * 0.92f, ForearmW * 0.32f, degrees, Fade(IronLit, alpha * 0.85f), layer + 0.002f);
            for (int i = 0; i < 3; i++) Rect(Along(p, d, -ForearmLen * (0.22f + i * 0.26f)), 0.02f, ForearmW + 0.02f, degrees, Fade(Brass, alpha), layer + 0.003f);
            Rect(g.VentA, 0.07f, 0.03f, degrees, Fade(IronDark, alpha), layer + 0.003f);
            Rect(g.VentB, 0.07f, 0.03f, degrees, Fade(IronDark, alpha), layer + 0.003f);

            // The hand: a fist (closed) or a spread claw with a glowing palm (open).
            Vector2 fist = g.Fist;
            Disc(fist, layer + 0.004f, 0.12f, 0.11f, Fade(IronDark, alpha));
            Disc(fist, layer + 0.005f, 0.10f, 0.09f, Fade(Iron, alpha));
            Disc(new Vector2(fist.x - 0.01f, fist.y + 0.015f), layer + 0.006f, 0.06f, 0.05f, Fade(IronLit, alpha * 0.8f));
            float spread = Smooth(open);
            for (int i = 0; i < 4; i++)
            {
                // Four finger plates: closed they lie together over the knuckles; open they fan out 26 degrees apart.
                float side = (i - 1.5f) * 0.045f, a = degrees + (i - 1.5f) * 26f * spread, len = 0.13f + 0.05f * spread;
                Vector2 from = Along(fist, d, 0.04f, side * (1f - spread * 0.4f)), fd = Turn(a);
                Rect(from + fd * (len / 2f), len + 0.02f, 0.05f, a, Fade(IronDark, alpha), layer + 0.007f);
                Rect(from + fd * (len / 2f), len, 0.035f, a, Fade(Iron, alpha), layer + 0.008f);
                Rect(from + fd * 0.02f, 0.03f, 0.04f, a, Fade(Brass, alpha), layer + 0.009f);
            }
            if (spread > 0f)
            {
                // The palm: an ember-red mouth that glows, hotter the more heat the gauntlet holds.
                float k = 0.3f + 0.7f * Clamp01(heat / maxHeat);
                Disc(g.Palm, layer + 0.0095f, 0.075f * spread, 0.065f * spread, Fade(Ember, alpha * spread));
                Disc(g.Palm, layer + 0.0096f, 0.045f * spread, 0.04f * spread, Fade(Core, alpha * spread * (0.6f + 0.4f * k)));
                Sprite(g.Palm, 0.5f * spread, 0.42f * spread, Fade(Flame, 0.45f * spread * alpha), glow, layer + 0.0097f);
            }
            Heat(g, degrees, heat, maxHeat, overheatAt, s, lean, alpha, true);
            return g;
        }

        /// <summary>
        /// The heat on the gauntlet (no fire on it): the plate tint, the hot seams and vents, the halo
        /// along the forearm that slides to the fist with <paramref name="lean"/>, and above
        /// <paramref name="overheatAt"/> the red pulsing halo and smoke from the elbow vents.
        /// <paramref name="plate"/> false leaves out the tint, seams and vents (the held weapon, whose
        /// texture is not the same shape).
        /// </summary>
        private static void Heat(in FlameGauntletPose g, float degrees, float heat, float maxHeat, float overheatAt, float s, float lean, float alpha, bool plate)
        {
            Vector2 p = g.Hand, d = g.D;
            float layer = g.Layer;
            float k = Clamp01(heat / maxHeat), over = Clamp01((heat - overheatAt) / Mathf.Max(0.001f, maxHeat - overheatAt));
            if (k > 0f)
            {
                Color hot = k < 0.5f ? Color.Lerp(Ember, Flame, k * 2f) : Color.Lerp(Flame, Core, (k - 0.5f) * 2f);
                if (plate)
                {
                    // The plate takes the tint, strongest at the wrist end.
                    Rect(Along(p, d, -ForearmLen * 0.42f), ForearmLen * 0.85f, ForearmW * 0.8f, degrees, Fade(hot, alpha * (0.15f + 0.55f * k)), layer + 0.0035f);
                    for (int i = 0; i < 3; i++)
                        Rect(Along(p, d, -ForearmLen * (0.22f + i * 0.26f)), 0.024f, ForearmW + 0.02f, degrees, Fade(hot, alpha * Clamp01(k * 3f - i * 0.6f)), layer + 0.0036f);
                    Rect(g.VentA, 0.07f, 0.03f, degrees, Fade(hot, alpha * Clamp01(k * 2f)), layer + 0.0037f);
                    Rect(g.VentB, 0.07f, 0.03f, degrees, Fade(hot, alpha * Clamp01(k * 2f)), layer + 0.0037f);
                }
                // Halo: along the forearm, sliding to the fist with lean, pulsing when overheating.
                float pulse = 1f + 0.25f * over * Mathf.Sin(s * 9f);
                Vector2 c = Along(p, d, Mathf.Lerp(-ForearmLen * 0.45f, 0.05f, lean));
                Sprite(c, ForearmLen * 1.5f * (1f - lean * 0.5f) * k * pulse + 0.15f, 0.45f * k * pulse + 0.12f, Fade(hot, alpha * 0.45f * k), glow, layer + 0.012f, -degrees);
                if (over > 0f) Sprite(c, ForearmLen * 2.2f * pulse, 0.8f * pulse, Fade(Hot, 0.4f * over * alpha), glow, layer + 0.0125f, -degrees);
            }
            if (over > 0f)
            {
                for (int i = 0; i < 3; i++)
                {
                    const float life = 1.4f;
                    float u = (s * 0.7f + Rand(i + 500) * 2f) % life / life;
                    Vector2 at = Along(p, d, -ForearmLen * 0.85f + (Rand(i + 510) - 0.5f) * 0.1f, (i - 1) * 0.06f);
                    Sprite(new Vector2(at.x + Mathf.Sin(u * 5f + i) * 0.08f, at.y + u * 0.7f * Lift), 0.18f + u * 0.3f, 0.16f + u * 0.25f,
                        Fade(Smoke, 0.45f * (1f - u) * over * alpha), PowerPoleGraphics.puff, Y + 0.16f);
                }
            }
        }

        /// <summary>
        /// The held gauntlet between casts. Vanilla draws the weapon texture; this draws only its heat:
        /// the halo along the forearm and, at <paramref name="overheatAt"/> or more, the red pulsing
        /// halo, smoke from the elbow vents and the Overheating haze round the wearer. Nothing at no heat.
        /// <paramref name="hand"/> is the point the weapon is drawn at (no height is added to it); the
        /// forearm runs back from it against <paramref name="aimDegrees"/>. The wearer is taken to stand
        /// where the sketch's resting hand puts them: 0.12 cells behind the hand and 0.18 to its right.
        /// </summary>
        internal static void DrawHeld(Vector2 hand, float aimDegrees, float heat, float maxHeat, float overheatAt, float seconds, Map map)
        {
            if (heat <= 0f || map == null || !Shown(hand, map)) return;
            Begin(hand);
            FlameGauntletPose g = Pose(hand, aimDegrees);
            Heat(g, aimDegrees, heat, maxHeat, overheatAt, seconds, 0f, 1f, false);
            if (heat >= overheatAt) Overheating(Along(hand, g.D, -0.12f, -0.18f), seconds, 1f);
        }

        /// <summary>
        /// The wearer's Overheating state: a red haze at the arm, a burn mark on the gauntlet arm for
        /// each tick that has landed, and a flash for a fresh one (<paramref name="flash"/> 0..1).
        /// </summary>
        internal static void Overheating(Vector2 pos, float s, float over, int ticks = 0, float flash = 0f)
        {
            if (over <= 0f && ticks <= 0) return;
            if (over > 0f) Circle(new Vector2(pos.x, pos.y + 0.1f), 0.36f + 0.03f * Mathf.Sin(s * 6f), 0.35f * over, PawnLayer + 0.03f, Hot);
            for (int i = 0; i < ticks; i++)
            {
                var c = new Vector2(pos.x + 0.16f + (Rand(i + 700) - 0.5f) * 0.06f, pos.y + 0.36f + (Rand(i + 710) - 0.5f) * 0.06f);
                Disc(c, PawnLayer + 0.012f, 0.045f, 0.04f, Fade(Char, 0.9f));
                Disc(c, PawnLayer + 0.013f, 0.025f, 0.022f, Fade(Ember, 0.9f));
            }
            if (flash > 0f) Sprite(new Vector2(pos.x + 0.16f, pos.y + 0.36f), 0.35f, 0.3f, Fade(Hot, 0.8f * flash), glow, PawnLayer + 0.04f);
        }

        /// <summary>
        /// The heat tally: one tick per heat point (<paramref name="maxHeat"/> rounded) south of the
        /// wearer's feet, or above the head when aiming south so the row does not sit on the target.
        /// Filled ticks are flame orange; the ticks from <paramref name="overheatAt"/> up are edged red.
        /// </summary>
        internal static void Tally(Vector2 feet, float heat, float maxHeat, float overheatAt, float aimDegrees, float alpha)
        {
            int count = ChainSickleGraphics.Round(maxHeat);
            bool above = Mathf.Sin(aimDegrees * Mathf.Deg2Rad) < -0.5f;
            float z = feet.y + (above ? 1.15f : -0.55f);
            const float step = 0.085f, w = 0.06f;
            float x0 = feet.x - step * (count - 1) / 2f;
            for (int i = 0; i < count; i++)
            {
                float fill = Clamp01(heat - i);
                bool zone = i >= overheatAt;
                var c = new Vector2(x0 + i * step, z);
                Rect(c, w + 0.015f, 0.13f, 90f, Fade(zone ? Ember : IronDark, 0.8f * alpha), Floor + 0.05f);
                Rect(c, w, 0.11f, 90f, Fade(Color.Lerp(Iron, zone ? Hot : Flame, fill), alpha * (0.6f + 0.4f * fill)), Floor + 0.051f);
            }
        }

        /// <summary>A short grey puff at a drawn point: "refused". The lib's shared puff; Devour draws its own pale one.</summary>
        internal static void Refused(Vector2 pos, float age, float life = 0.5f)
        {
            if (age < 0f || age >= life) return;
            float u = age / life;
            Sprite(new Vector2(pos.x, pos.y + u * 0.3f), 0.3f + u * 0.3f, 0.28f + u * 0.25f, Fade(Smoke, 0.6f * (1f - u)), PowerPoleGraphics.puff, Y + 0.17f);
        }

        /// <summary>
        /// A pale cough of steam out of a drawn point along <paramref name="d"/>: three Steam puffs
        /// that drift 0.35 cells out, rise and fade over 0.6 s. Release's "too cold" puff, and Devour's
        /// "too hot" one.
        /// </summary>
        internal static void Cough(Vector2 from, Vector2 d, float age)
        {
            var side = new Vector2(-d.y, d.x);
            for (int k = 0; k < 3; k++)
            {
                float u = (age - k * 0.07f) / 0.6f;
                if (u <= 0f || u >= 1f) continue;
                Vector2 q = from + d * (0.35f * EaseOut(u)) + side * ((k - 1) * 0.08f * u);
                Sprite(new Vector2(q.x, q.y + 0.25f * u * Lift), 0.18f + u * 0.35f, 0.16f + u * 0.3f, Fade(Steam, 0.5f * (1f - u)), PowerPoleGraphics.puff, Y + 0.17f + k * 1e-4f);
            }
        }

        // ------------------------------------------------------------------ Release

        /// <summary>
        /// The blast out of the fist: a flash at the knuckles and five licks of flame that fan out and
        /// dive from hand height onto the floor at <paramref name="land"/>. <paramref name="root"/> is the
        /// ground point under the fist, <paramref name="side"/> the unit vector across the aim,
        /// <paramref name="age"/> seconds since the release. Gone at JetLife.
        /// </summary>
        internal static void Jet(Vector2 root, Vector2 land, Vector2 side, float age, float s, float spread = 0.55f, float layer = float.NaN)
        {
            const float JetLand = FlameReleaseTiming.JetLand, JetLife = FlameReleaseTiming.JetLife;
            if (age < 0f || age >= JetLife) return;
            if (float.IsNaN(layer)) layer = Y + 0.06f;
            float tip = Clamp01(age / JetLand), tail = Smooth(Clamp01((age - 0.08f) / (JetLife - 0.08f)));
            float fade = 1f - Smooth(Clamp01((age - 0.12f) / (JetLife - 0.12f)));
            if (age < 0.12f)
            {
                float f = 1f - age / 0.12f;
                Sprite(JetAt(root, land, side, 0f, 0f), 0.5f + 0.9f * f, 0.45f + 0.75f * f, Fade(Core, 0.95f * f), glow, layer + 0.004f);
            }
            for (int i = 0; i < 5; i++)
            {
                float u = Mathf.Lerp(tail, tip, (i + 0.5f) / 5f);
                Sprite(JetAt(root, land, side, u, 0f), 0.45f + u * 0.7f, 0.4f + u * 0.55f, Fade(Flame, 0.45f * fade), glow, layer - 0.002f);
            }
            // Each lick: a strip narrow at the fist, widest near its end, with a round end.
            for (int j = 0; j < 5; j++)
            {
                float k = (j - 2) / 2f * spread + Mathf.Sin(s * 31f + j * 1.7f) * 0.04f, reach = tip * (0.8f + 0.2f * Rand(j + 900)), size = 0.8f + 0.4f * Rand(j + 910);
                if (reach - tail < 0.04f) continue;
                for (int i = 0; i <= 8; i++)
                {
                    float u = Mathf.Lerp(tail, reach, i / 8f);
                    Vector2 q = JetAt(root, land, side, u, k);
                    Lick[i] = new Vector2(q.x + Mathf.Sin(s * 40f + j + u * 9f) * 0.02f, q.y + Mathf.Cos(s * 37f + j + u * 7f) * 0.02f);
                }
                LickStrip(0.13f * size, Fade(Ember, 0.85f * fade), layer);
                LickStrip(0.085f * size, Fade(Flame, 0.9f * fade), layer + 0.001f);
                LickStrip(0.04f * size, Fade(Core, 0.95f * fade), layer + 0.002f);
            }
            // Where it lands: a hot ring that runs out over the floor.
            if (age >= JetLand && age < JetLand + 0.25f)
            {
                float u = (age - JetLand) / 0.25f;
                Circle(land, 0.25f + u * 1.1f, 0.55f * (1f - u), Floor + 0.03f, Flame);
            }
        }

        private static Vector2 JetAt(Vector2 root, Vector2 land, Vector2 side, float u, float k)
        {
            float h = HandH * (1f - u * u);
            return new Vector2(Mathf.Lerp(root.x, land.x, u) + side.x * k * u, Mathf.Lerp(root.y, land.y, u) + side.y * k * u + h * Lift);
        }

        /// <summary>A strip through <see cref="Lick"/>, half-width k (0.35 + 0.65 u) sqrt(1 - u^3) at u along it.</summary>
        private static void LickStrip(float k, Color colour, float layer)
        {
            if (colour.a <= 0.001f) return;
            const int n = 8;
            Sides(n + 1, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i <= n; i++)
            {
                Vector2 pr = Lick[Mathf.Max(0, i - 1)], nx = Lick[Mathf.Min(n, i + 1)];
                Vector2 t = nx - pr;
                float L = t.magnitude;
                t = L > 0f ? t / L : Vector2.right;
                float u = i / (float)n, w = k * (0.35f + 0.65f * u) * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u * u));
                a[i] = new Vector2(Lick[i].x + t.y * w, Lick[i].y - t.x * w);
                b[i] = new Vector2(Lick[i].x - t.y * w, Lick[i].y + t.x * w);
            }
            Strip(a, b, colour, solid, layer);
        }

        /// <summary>
        /// The lit width across the aim at distance <paramref name="d"/> along it, cell edges
        /// included: <paramref name="lo"/>/<paramref name="hi"/> are the lit columns of each row
        /// (index = row), <paramref name="last"/> the last lit row. Blends from one row to the next so
        /// the front fans out, and narrows to 0.5 at the fist before row 1 (from <paramref name="start"/>).
        /// </summary>
        internal static Vector2 SpanAt(float[] lo, float[] hi, int last, float d, float start)
        {
            int a0 = Mathf.Min(Mathf.Max(1, Mathf.FloorToInt(d)), last), a1 = Mathf.Min(a0 + 1, last);
            float k = d < 1f ? 0f : Smooth(Clamp01(d - a0));
            float l = Mathf.Lerp(lo[a0], lo[a1], k) - 0.5f, h = Mathf.Lerp(hi[a0], hi[a1], k) + 0.5f;
            if (d >= 1f) return new Vector2(l, h);
            float mid = (l + h) / 2f, pinch = Clamp01((d - start) / (1f - start));
            return new Vector2(Mathf.Lerp(mid - 0.25f, l, pinch), Mathf.Lerp(mid + 0.25f, h, pinch));
        }

        /// <summary>
        /// The ground wave: fire running along the floor, as wide as <paramref name="span"/> (x = lo,
        /// y = hi, across the aim). A faint wide light, a hot line at the front and a glow a cell
        /// behind it; a row of tall tongues on a bowed line (the middle <paramref name="bow"/> ahead of
        /// the edges) leaning forward, a lower row behind; sparks thrown ahead. <paramref name="D"/> is
        /// how far along the front is, <paramref name="from"/> where it started (no light behind that),
        /// <paramref name="amount"/> 0..1 shrinks it as it sinks.
        /// </summary>
        internal static void Wave(in ChainSickleFrame f, Vector2 caster, float D, Vector2 span, float s, float amount, float from, float bow, float layer = float.NaN)
        {
            if (amount <= 0f) return;
            if (float.IsNaN(layer)) layer = Y + 0.05f;
            float lo = span.x, hi = span.y, mid = (lo + hi) / 2f, half = Mathf.Max(0.25f, (hi - lo) / 2f);
            // Flames rise north; a forward lean must not cancel that when the aim is south.
            var lean = new Vector2(f.ca * 0.45f, Mathf.Max(-0.2f, f.sa * 0.45f));
            Sprite(f.Ground(caster, D - 0.3f, mid), half * 2f + 2.2f, half * 2f + 2.2f, Fade(Flame, 0.13f * amount), glow, Floor + 0.017f);
            int n = Mathf.Max(2, ChainSickleGraphics.Round((hi - lo) / 0.3f));
            for (int i = 0; i <= n; i++)
            {
                float k = Mathf.Lerp(lo + 0.15f, hi - 0.15f, i / (float)n), a = Front(D, bow, k, mid, half);
                Sprite(f.Ground(caster, a - 0.05f, k), 0.6f, 0.55f, Fade(Core, 0.2f * amount), glow, Floor + 0.021f);
                if (a - 0.5f > from) Sprite(f.Ground(caster, a - 0.5f, k), 1f, 0.9f, Fade(Flame, 0.16f * amount), glow, Floor + 0.02f);
                if (a - 1.1f > from) Sprite(f.Ground(caster, a - 1.1f, k), 1.1f, 1f, Fade(Ember, 0.12f * amount), glow, Floor + 0.019f);
            }
            int m = Mathf.Max(2, ChainSickleGraphics.Round((hi - lo) / 0.27f));
            for (int row = 1; row >= 0; row--)
            {
                int count = row == 1 ? m - 1 : m;
                for (int i = 0; i < count; i++)
                {
                    float k = lo + (i + (row == 1 ? 1f : 0.5f)) * (hi - lo) / m, r = Rand(i * 7 + row * 50 + 1);
                    float e = (k - mid) / (half + 0.15f), edge = 1f - 0.45f * e * e;
                    float h = (row == 1 ? 0.45f + 0.3f * r : 0.78f + 0.42f * r) * amount * edge, w = (row == 1 ? 0.13f : 0.17f) * (0.6f + 0.4f * amount);
                    Vector2 at = f.Ground(caster, Front(D, bow, k, mid, half) - row * 0.35f + (Rand(i * 7 + row * 50 + 2) - 0.5f) * 0.12f, k);
                    Vector2 l = row == 1 ? lean * 0.6f : lean;
                    Tongue(at, h, w, s, 120 + i * 3 + row * 40, 1f, layer + (row == 1 ? 0f : 0.004f) + i * 1e-4f, l);
                    // The front row glows along its height, so it stays the brightest fire on screen.
                    if (row == 0)
                        Sprite(new Vector2(at.x + l.x * h * 0.4f, at.y + h * 0.4f * Lift + l.y * h * 0.4f), 0.5f, 0.35f + h * 0.5f, Fade(Flame, 0.2f * amount), glow, layer + 0.0035f);
                }
            }
            for (int i = 0; i < 12; i++)
            {
                float life = 0.3f + Rand(i + 640) * 0.15f, ph = (s * 2.7f + Rand(i + 600) * 3f) % 1f, age = ph * life;
                float k = Mathf.Lerp(lo, hi, Rand(i + 610)), h = age * (1.8f + Rand(i + 630)) - age * age * 6f;
                if (h < 0f) continue;
                Vector2 q = f.Ground(caster, Front(D, bow, k, mid, half) + 0.1f + age * (2.5f + Rand(i + 620) * 2.5f), k);
                var pt = new Vector2(q.x, q.y + h * Lift);
                Sprite(pt, 0.17f, 0.15f, Fade(Flame, (1f - ph) * 0.35f * amount), glow, Y + 0.119f);
                Sprite(pt, 0.07f, 0.07f, Fade(Core, (1f - ph) * 0.95f * amount), glow, Y + 0.12f);
            }
        }

        private static float Front(float D, float bow, float k, float mid, float half)
        {
            float x = (k - mid) / half;
            return D - bow * x * x;
        }

        /// <summary>
        /// Soot the wave leaves: puffs born along the front as it passes, rising and spreading for
        /// about 1.5 s, lit orange from below while young. <paramref name="t0"/> is when the front left
        /// <paramref name="from"/>, <paramref name="speed"/> its cells a second, <paramref name="to"/>
        /// where it stopped; the width is <see cref="SpanAt"/> of the rows.
        /// </summary>
        internal static void WaveSmoke(in ChainSickleFrame f, Vector2 caster, float t0, float speed, float from, float to,
            float[] lo, float[] hi, int last, float s, float layer = float.NaN)
        {
            if (float.IsNaN(layer)) layer = Y + 0.09f;
            for (int k = 0; ; k++)
            {
                float d = from + 0.3f + k * 0.45f;
                if (d > to) break;
                Vector2 sp = SpanAt(lo, hi, last, d, from);
                float born = t0 + (d - from) / speed;
                for (int j = 0; j < 2; j++)
                {
                    float life = 1.3f + Rand(k * 5 + j + 700) * 0.5f, u = (s - born - j * 0.09f) / life;
                    if (u <= 0f || u >= 1f) continue;
                    float across = Mathf.Lerp(sp.x + 0.25f, sp.y - 0.25f, Rand(k * 5 + j + 710)), rise = 0.5f + u * 1.8f;
                    Vector2 g = f.Ground(caster, d - 0.25f + u * 0.3f, across);
                    var pos = new Vector2(g.x + Mathf.Sin(u * 4f + k) * 0.1f, g.y + rise * Lift);
                    float a = Mathf.Min(1f, u * 5f) * (1f - u);
                    Sprite(pos, 0.7f + u * 1.5f, 0.6f + u * 1.2f, Fade(Soot, 0.5f * a), PowerPoleGraphics.puff, layer + k * 1e-4f);
                    if (u < 0.35f) Sprite(pos, 0.5f + u * 0.8f, 0.42f + u * 0.7f, Fade(Ember, 0.3f * (1f - u / 0.35f)), glow, layer + k * 1e-4f + 5e-5f);
                }
            }
        }

        /// <summary>
        /// The low sheet of flame the wave leaves on a lit cell for a moment: a floor glow and four
        /// short tongues toward the corners, so the lit cells read as one burning area. <paramref name="amount"/> 0..1.
        /// </summary>
        internal static void Sheet(Vector2 c, float s, float amount, int seed, float layer)
        {
            if (amount <= 0f) return;
            Sprite(c, 1.5f, 1.3f, Fade(Flame, 0.24f * amount), glow, Floor + 0.013f);
            for (int i = 0; i < 4; i++)
            {
                float qx = (i % 2 == 1 ? 0.3f : -0.3f) + (Rand(seed * 13 + i) - 0.5f) * 0.16f, qz = (i < 2 ? -0.32f : 0.26f) + (Rand(seed * 13 + i + 20) - 0.5f) * 0.14f;
                Tongue(new Vector2(c.x + qx, c.y + qz), (0.2f + Rand(seed * 13 + i + 40) * 0.2f) * amount, 0.09f + 0.04f * amount, s,
                    seed * 11 + i + 200, Mathf.Min(1f, amount * 1.5f), layer - qz * 1e-3f);
            }
        }

        /// <summary>The gauntlet dumping its heat at the Release: pale smoke blown back out of both elbow vents, gone in 0.7 s.</summary>
        internal static void Exhaust(in FlameGauntletPose g, float age, float amount = 1f)
        {
            if (age < 0f || age > 0.9f || amount <= 0f) return;
            var side = new Vector2(-g.D.y, g.D.x);
            for (int j = 0; j < 2; j++)
            {
                Vector2 v = j == 0 ? g.VentA : g.VentB;
                for (int i = 0; i < 4; i++)
                {
                    float u = (age - i * 0.05f) / 0.7f;
                    if (u <= 0f || u >= 1f) continue;
                    float back = 0.08f + EaseOut(u) * (0.5f + 0.1f * i), out_ = (j == 1 ? 1f : -1f) * EaseOut(u) * 0.18f, rise = u * 0.3f;
                    Sprite(new Vector2(v.x - g.D.x * back + side.x * out_, v.y - g.D.y * back + side.y * out_ + rise * Lift),
                        0.13f + u * 0.48f, 0.11f + u * 0.4f, Fade(Steam, 0.6f * (1f - u) * amount), PowerPoleGraphics.puff, Y + 0.165f);
                }
            }
        }

        /// <summary>A thin wisp of smoke off the knuckles after the Release: four puffs 0.18 s apart.</summary>
        internal static void Wisp(Vector2 pos, float age, float life = 1.2f)
        {
            for (int i = 0; i < 4; i++)
            {
                float u = (age - i * 0.18f) / life;
                if (u <= 0f || u >= 1f) continue;
                Sprite(new Vector2(pos.x + Mathf.Sin(u * 7f + i) * 0.05f, pos.y + u * 0.9f * Lift), 0.08f + u * 0.25f, 0.08f + u * 0.22f,
                    Fade(Steam, 0.3f * Mathf.Sin(Mathf.PI * u)), PowerPoleGraphics.puff, Y + 0.166f);
            }
        }
    }
}
