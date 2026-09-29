using RimWorld;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.AcceleratorGraphics;
using F = RimArt.VectorFlick;
using Taper = RimArt.GokuGraphics.Taper;

namespace RimArt
{
    /// <summary>
    /// Draws one Vector Flick, the port of the lab's accelerator-vector-flick.js: the scuff ring and
    /// dust at his foot, the pebble popping out of the floor turning, the pit that stays, the grip
    /// ring, the kicking leg, the kick flash with its ring, five air streaks and floor dust, the
    /// flight (white-edged pebble, a 3-cell trail with black edges, two cone lines, a ground shadow
    /// and a faint path line), the hit on the chest (flash, black-edged ring, six sparks) and four
    /// bits that fly on and stay until the picture ends. Monochrome: white light with a black edge
    /// on what he controls; the pebble is plain stone before the kick.
    ///
    /// Not drawn (the lab's stand-ins): the pawns, the raider's white tint and his flinch. The leg is
    /// drawn here (a tapered quad from the hip to a shoe disc, behind the body while the shoe is north
    /// of the hip), not a Melee Animation clip. The flight is a line and the rings are level circles,
    /// so there is one drawing for every aim. Heights on a pawn go through PawnFit, so between
    /// PawnFit.Begin and End the floor near a pawn is its fitted floor (PawnFit.Y(0)) and the hit is
    /// on the real chest; in the lab and the previews they are the sketch's. One shot per call and
    /// no state kept, so overlapping flicks each draw their own.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class VectorFlickGraphics
    {
        private const float Lift = SixPathsHeight.Lift;
        /// <summary>The leg: hip height as drawn, width at the hip and the ankle; the shoe's radii.</summary>
        private const float Hip = 0.1f, HipWide = 0.12f, AnkleWide = 0.1f, ShoeLong = 0.08f, ShoeDeep = 0.065f;
        private static readonly Vector2[] path = new Vector2[2], trail = new Vector2[3];

        /// <summary>
        /// Draws <paramref name="shot"/> <paramref name="seconds"/> after its warm-up began. Nothing
        /// before 0 or after <see cref="VectorFlickShot.Duration"/>, or when his cell is out of bounds or fogged.
        /// </summary>
        internal static void Draw(in VectorFlickShot shot, float seconds, Map map)
        {
            if (seconds < 0f || seconds >= shot.Duration || !Shown(shot.Feet, map)) return;
            Vector2 sun = GenCelestial.GetLightSourceInfo(map, GenCelestial.LightType.Shadow).vector * SixPathsSlamGraphics.SunScale;
            Begin(shot.Feet);

            float kick = shot.KickAt, hit = shot.HitAt, kickAge = seconds - kick, hitAge = seconds - hit;
            // The bits and the pit go over the picture's last FadeOut.
            float stay = 1f - Mathf.Clamp01((seconds - (shot.Duration - F.FadeOut)) / F.FadeOut);
            Vector2 aim = Turn(shot.Aim), pit = shot.Feet + aim * (F.Foot * PawnFit.Body);
            Vector2 target = shot.Target ?? shot.Feet + aim * shot.Distance, run = target - pit;
            float runCells = run.magnitude;
            Vector2 fly = runCells > 1e-4f ? run / runCells : aim;
            float aimRad = shot.Aim * Mathf.Deg2Rad, flyRad = Mathf.Atan2(fly.y, fly.x);
            int seed = shot.Seed;

            // --- floor: the pit, the scuff and kick dust, the bits that landed ---------------------------------------------
            Vector2 pitFloor = Up(pit, 0f);
            Sprite(pitFloor, 0.2f, 0.15f, Fade(Ink, 0.5f * Smooth(seconds / 0.1f) * stay), soft, Floor + 0.01f);
            if (seconds < 0.25f)
            {
                float u = seconds / 0.25f;
                PaperBombGraphics.RingAt(pitFloor, 0.1f + Smooth(u) * 0.3f, Fade(Edge, 0.8f * (1f - u)), Floor + 0.02f);
                PaperBombGraphics.RingAt(pitFloor, 0.08f + Smooth(u) * 0.3f, Fade(White, 0.9f * (1f - u)), Floor + 0.021f);
            }
            if (seconds < 0.4f)
                for (int i = 0; i < 5; i++)
                {
                    float u = Mathf.Clamp01((seconds - Rand(i + seed * 7) * 0.05f) / 0.35f), ang = i * 1.26f + Rand(i + 3), d = 0.08f + u * 0.25f;
                    Sprite(new Vector2(pitFloor.x + Mathf.Cos(ang) * d, pitFloor.y + Mathf.Sin(ang) * d * 0.7f + u * 0.06f), 0.14f + u * 0.16f, 0.11f + u * 0.12f,
                        Fade(GokuGraphics.Dust, 0.45f * Mathf.Sin(u * Mathf.PI)), soft, Floor + 0.03f);
                }
            if (kickAge >= 0f && kickAge < 0.6f)
                for (int i = 0; i < 9; i++)
                {
                    float u = Mathf.Clamp01((kickAge - Rand(i + 20) * 0.06f) / 0.5f), ang = aimRad + (i - 4) * 0.45f + (Rand(i + 30) - 0.5f) * 0.3f;
                    float d = 0.2f + u * (0.5f + Rand(i + 40) * 0.5f);
                    Sprite(new Vector2(pitFloor.x + Mathf.Cos(ang) * d, pitFloor.y + Mathf.Sin(ang) * d + u * 0.08f), 0.22f + u * 0.35f, 0.18f + u * 0.28f,
                        Fade(GokuGraphics.Dust, 0.4f * Mathf.Sin(u * Mathf.PI)), soft, Floor + 0.04f);
                }
            if (hitAge >= 0f)
                for (int i = 0; i < F.Bits; i++)
                {
                    // Thrown on past him in an arc from the chest, landing 0.5-1.3 cells behind; they stay.
                    float u = Mathf.Clamp01(hitAge / (0.22f + Rand(i + seed * 5) * 0.12f)), ang = flyRad + (Rand(i + 50 + seed) - 0.5f) * 1.4f;
                    float d = 0.5f + Rand(i + 60 + seed) * 0.8f, h = F.ChestUp * (1f - u) + 0.25f * Mathf.Max(0f, Mathf.Sin(u * Mathf.PI));
                    Vector2 ground = target + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (d * u);
                    if (u < 1f) Sprite(Up(ground, 0f) + sun * h, 0.1f, 0.06f, Fade(Ink, 0.35f * stay), soft, Floor + 0.05f);
                    PaperBombGraphics.Rock(Up(ground, h), 0.09f, Rand(i + 70) * 360f + hitAge * 900f * (1f - u), stay, 2 * (i % 3),
                        (u < 1f ? Overhead + 0.05f : Floor + 0.06f) + i * 0.001f);
                }

            // --- the kicking leg --------------------------------------------------------------------------------------------
            float shown = F.LegShown(seconds, shot.Warmup);
            if (shown > 0f)
            {
                F.Leg(seconds, shot.Warmup, out float reach, out float lift);
                // Behind the body whenever the shoe is north of the hip: kicking north, or drawn back while kicking south.
                Leg(shot.Feet, aim, reach, lift, shot.Pants, shot.Shoe, shown, aim.y * reach > 0.05f ? GokuGraphics.PawnLayer - 0.012f : GokuGraphics.PawnLayer + 0.012f);
            }

            // --- the pebble: the pop, the grip, the kick, the flight --------------------------------------------------------
            float startH = F.Knee + F.Apex;
            if (seconds < kick)
            {
                // Out of the floor to knee height, still rising slowly until the foot meets it. Plain stone.
                float h = F.PopHeight(seconds, shot.Warmup);
                Vector2 at = Up(pit, h);
                Sprite(pitFloor + sun * h, 0.16f, 0.1f, Fade(Ink, 0.4f * Mathf.Clamp01(seconds / 0.05f)), soft, Floor + 0.05f);
                PaperBombGraphics.Rock(at, F.Pebble, seconds * 400f, 1f, 4, aim.y > 0.5f ? GokuGraphics.PawnLayer - 0.016f : Overhead + 0.102f);
                // The grip: a ring closing onto the pebble in the last Grip seconds.
                float g = Mathf.Clamp01((seconds - (kick - F.Grip)) / F.Grip);
                if (g > 0f)
                {
                    PaperBombGraphics.RingAt(at, 0.5f - 0.38f * g, Fade(Edge, 0.9f * g), Overhead + 0.19f);
                    PaperBombGraphics.RingAt(at, 0.47f - 0.38f * g, Fade(White, 0.95f * g), Overhead + 0.191f);
                }
            }
            else if (seconds < hit + 0.6f)
            {
                // The kick: flash, ring out, air streaks fanned forward from the contact point.
                Vector2 contact = Up(pit, startH);
                if (kickAge < F.KickLife)
                {
                    float u = kickAge / F.KickLife, flash = 0.7f * (1f - u) + 0.2f;
                    Sprite(contact, flash, flash, Fade(White, 0.95f * (1f - u)), glow, Overhead + 0.2f);
                    PaperBombGraphics.RingAt(contact, 0.14f + Smooth(u) * 0.8f, Fade(Edge, 0.85f * (1f - u)), Overhead + 0.19f);
                    PaperBombGraphics.RingAt(contact, 0.11f + Smooth(u) * 0.8f, Fade(White, 0.9f * (1f - u)), Overhead + 0.191f);
                    for (int i = 0; i < 5; i++)
                    {
                        float ang = flyRad + (i - 2) * 0.32f, d0 = 0.15f + u * 0.3f, d1 = d0 + 0.35f + u * 0.6f;
                        var way = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        Streak(contact + way * d0, contact + way * d1, 0.05f, Fade(White, 0.9f * (1f - u)), whiteGlow, Overhead + 0.2f, 3);
                    }
                }

                // How far along the run the pebble is, 0 to 1, and its height there (knee to chest).
                float runLeft = Mathf.Max(0.01f, shot.Distance - F.Foot), k = Mathf.Clamp01(kickAge * shot.Speed / runLeft), flown = k * runCells;
                Vector2 head = Along(pit, target, k, startH);

                // The path line left in the air: faint, the whole way flown, gone PathFade after the hit.
                float pathFade = seconds < hit ? 1f : 1f - Mathf.Clamp01(hitAge / F.PathFade);
                if (flown > 0.05f && pathFade > 0f)
                {
                    path[0] = head;
                    path[1] = contact;
                    GokuGraphics.Line(path, 0.035f, Fade(White, 0.35f * pathFade), whiteGlow, Overhead + 0.08f, Taper.None);
                }

                if (seconds < hit)
                {
                    // In flight: trail, cone, the pebble in his colours, and its shadow.
                    float back = runCells > 1e-4f ? Mathf.Max(0f, flown - F.TrailCells) / runCells : 0f;
                    trail[0] = head;
                    trail[1] = Along(pit, target, (k + back) / 2f, startH);
                    trail[2] = Along(pit, target, back, startH);
                    GokuGraphics.Line(trail, 0.11f, Fade(Edge, 0.75f), null, Overhead + 0.09f);
                    GokuGraphics.Line(trail, 0.055f, Fade(White, 0.95f), whiteGlow, Overhead + 0.091f);
                    float cone = Mathf.Min(0.55f, flown * 0.6f);
                    if (cone > 0.05f)
                        for (int side = -1; side <= 1; side += 2)
                        {
                            float ang = flyRad + Mathf.PI + side * 0.32f;
                            var way = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                            Streak(head + way * 0.1f, head + way * (0.1f + cone), 0.04f, Fade(White, 0.85f), whiteGlow, Overhead + 0.092f, 3);
                        }
                    float h = startH + (F.ChestUp - startH) * k;
                    Sprite(Up(Vector2.Lerp(pit, target, k), 0f) + sun * h, 0.16f, 0.1f, Fade(Ink, 0.35f), soft, Floor + 0.05f);
                    Sprite(head, 0.45f, 0.45f, Fade(White, 0.8f), glow, Overhead + 0.1f);
                    PaperBombGraphics.RingAt(head, 0.1f, Fade(Edge, 0.9f), Overhead + 0.101f);
                    PaperBombGraphics.Rock(head, F.Pebble * 0.9f, kickAge * 2400f, 1f, 4, Overhead + 0.102f);
                }
            }

            // --- the hit on the chest ---------------------------------------------------------------------------------------
            if (hitAge >= 0f && hitAge <= F.HitLife)
            {
                float u = hitAge / F.HitLife, flash = 0.8f * (1f - u) + 0.2f;
                Vector2 chest = Up(target, F.ChestUp);
                Sprite(chest, flash, flash, Fade(White, 0.95f * (1f - u)), glow, Overhead + 0.2f);
                PaperBombGraphics.RingAt(chest, 0.15f + Smooth(u) * 0.6f, Fade(Edge, 0.85f * (1f - u)), Overhead + 0.19f);
                PaperBombGraphics.RingAt(chest, 0.12f + Smooth(u) * 0.6f, Fade(White, 0.9f * (1f - u)), Overhead + 0.191f);
                for (int i = 0; i < F.Sparks; i++)
                {
                    float ang = flyRad + (Rand(i + 80 + seed) - 0.5f) * 2f, d0 = 0.1f + u * 0.3f, d1 = d0 + (0.3f + 0.5f * Rand(i + 90)) * (1f - u * 0.4f);
                    var way = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    Streak(chest + way * d0, chest + way * d1, 0.045f, Fade(White, 1f - u), whiteGlow, Overhead + 0.2f, 3);
                }
            }
        }

        /// <summary>A ground point <paramref name="h"/> up (lab units, drawn x Lift north), fitted to a real pawn in game.</summary>
        private static Vector2 Up(Vector2 ground, float h) => new Vector2(ground.x, ground.y + PawnFit.H(h) * Lift);

        /// <summary>The flight <paramref name="k"/> of the way from the pit to the target, rising from the kick's height to the chest.</summary>
        private static Vector2 Along(Vector2 pit, Vector2 target, float k, float startH) =>
            Up(Vector2.Lerp(pit, target, k), startH + (F.ChestUp - startH) * k);

        /// <summary>
        /// The kicking leg: a tapered quad from the hip to a shoe disc, the shoe <paramref name="reach"/>
        /// cells along <paramref name="aim"/> and <paramref name="lift"/> up. Sized to a real pawn in game (PawnFit).
        /// </summary>
        private static void Leg(Vector2 feet, Vector2 aim, float reach, float lift, Color pants, Color shoe, float alpha, float layer)
        {
            float body = PawnFit.Body;
            var hip = new Vector2(feet.x, feet.y + PawnFit.Y(Hip));
            Vector2 foot = Up(feet + aim * (reach * body), lift), along = foot - hip;
            float length = along.magnitude;
            if (length > 0.03f)
            {
                var side = new Vector2(-along.y, along.x) / length;
                Sides(2, out Vector2[] a, out Vector2[] b);
                a[0] = hip + side * (HipWide * body / 2f);
                b[0] = hip - side * (HipWide * body / 2f);
                a[1] = foot + side * (AnkleWide * body / 2f);
                b[1] = foot - side * (AnkleWide * body / 2f);
                Strip(a, b, Fade(pants, alpha), solid, layer);
            }
            DrawMesh(disc, foot, layer + 0.001f, ShoeLong * body, ShoeDeep * body, 0f, Fade(shoe, alpha), solid);
        }
    }
}
