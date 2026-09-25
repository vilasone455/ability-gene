using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.ThunderGodGraphics;
using T = RimArt.BankShotTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Bank Shot's charged shot: the charge on the barrel, the muzzle flash, the bullet and
    /// its tracer, the trace and smoke left along the flight, the ricochet at each wall contact, the
    /// embed, the hit on a pawn, and the aim line with the targeted wall cell. The port of
    /// Tools/VfxLab/web/sketches/bank-shot.js and lib/bank-shot.js; its numbers are those files'.
    ///
    /// The bullet flies level at <see cref="BankShotTiming.HandHeight"/>, so every point of the flight
    /// is a ground point lifted north, and every shape is a level line, quad or soft sprite: nothing
    /// has a per-facing method. Every routine takes ages and keeps no state; what lands (chips on a
    /// wall, blood on the floor) is drawn for every later time so it stays.
    ///
    /// Not ported (the lab's stand-ins): the walls, the caster and target figures, the target's
    /// flinch and the red blocked straight line. The heavy pistol is drawn only by the preview
    /// (<c>gun</c>); in game the pawn's own weapon graphic is drawn aiming during the charge.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class BankShotGraphics
    {
        internal static readonly Color Tracer = new Color(1f, 0.93f, 0.62f), TracerHot = new Color(1f, 0.58f, 0.18f), Core = new Color(1f, 1f, 1f),
            Smoke = new Color(0.42f, 0.40f, 0.37f), Chip = new Color(0.12f, 0.11f, 0.10f), ChipLit = new Color(0.62f, 0.58f, 0.54f),
            Blocked = new Color(0.75f, 0.16f, 0.10f);
        // lib/chain-sickle.js, lib/goku.js and six-paths-solid.js colours the sketch borrows.
        private static readonly Color Pale = new Color(1f, 0.96f, 0.85f), Blood = new Color(0.50f, 0.07f, 0.06f),
            Iron = new Color(0.30f, 0.31f, 0.34f), IronLit = new Color(0.52f, 0.54f, 0.58f), IronDark = new Color(0.09f, 0.09f, 0.11f),
            Body = new Color(0.035f, 0.028f, 0.050f);

        private const float Lift = SixPathsHeight.Lift;
        private static readonly float ShadowLayer = AltitudeLayer.Shadows.AltitudeFor(), PawnLayer = AltitudeLayer.Pawn.AltitudeFor();
        /// <summary>A layer above every wall top, for the chip and the targeted cell: the Paper Bomb sketches' WallTop.</summary>
        private static readonly float WallTop = PawnLayer + 0.05f;

        private static readonly List<double> cuts = new List<double>();

        private static Vector2 Lifted(Vector2 ground) => new Vector2(ground.x, ground.y + T.HandHeight * Lift);

        private static Vector2 At(in BankShotShot shot, double d, out Vector2 dir)
        {
            shot.Path.Along(d, out double x, out double z, out double dx, out double dz);
            dir = new Vector2((float)dx, (float)dz);
            return shot.Map(x, z);
        }

        private static Color Tracing(int bounces, int maxBounces) => Color.Lerp(Tracer, TracerHot, T.Heat(bounces, maxBounces));

        /// <summary>
        /// The targeter's aim line for a real cast. The square on the targeted wall is drawn on the wall
        /// cell itself, because RimWorld draws a wall flat on its cell; the sketch's stand-in walls are a
        /// cell tall, so the sketch (and the preview) draws it a cell's height north.
        /// </summary>
        public static void DrawAim(in BankShotShot shot, Map map)
        {
            if (shot.Path == null || !Shown(shot.Caster, map)) return;
            Begin(shot.Caster);
            Preview(shot, 1f, 0f);
        }

        /// <summary>
        /// The whole shot at <paramref name="seconds"/> on its clock. <paramref name="gun"/> draws the
        /// sketch's heavy pistol (the preview has no pawn to hold one); <paramref name="ui"/> draws the
        /// aim line until the shot; <paramref name="tileLift"/> is how far north the targeted wall's
        /// square is drawn (see <see cref="DrawAim"/>).
        /// </summary>
        public static void Draw(in BankShotShot shot, float seconds, Map map, bool gun, bool ui, Vector2? anchor = null, float tileLift = Lift)
        {
            if (shot.Path == null || seconds < 0f || seconds >= shot.Duration) return;
            if (!Shown(shot.Caster, map)) return;
            Begin(anchor ?? shot.Caster);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);

            BankShotPath path = shot.Path;
            Vector2 dir = Turn(shot.Aim), hand = shot.Caster + dir * T.GripAlong, muzzle = shot.Caster + dir * T.MuzzleAlong;
            float fire = shot.FireAt, endAt = shot.EndAt, length = (float)path.Length;
            bool fired = seconds >= fire;
            float flown = fired ? Mathf.Min(length, (seconds - fire) * shot.Speed) : 0f;
            float endAge = seconds - endAt;

            if (path.End == BankShotEnd.Hit)
            {
                At(shot, path.Length, out Vector2 lastDir);
                Wound(shot.Map(path.HitCellX, path.HitCellZ), lastDir, endAge, path.Bounces.Count);
            }

            if (gun) Pistol(hand, shot.Aim, sun, strength, fired ? T.Kick * T.Bump((seconds - fire) / T.KickTime) : 0f);

            float fade = fired ? 1f - Mathf.Clamp01((seconds - fire) / T.Fade) : 1f;
            if (ui) Preview(shot, fade, tileLift);
            Charge(hand, muzzle, shot.Aim, Mathf.Clamp01((seconds - T.Lead) / shot.Charge) * fade, seconds);
            if (!fired) return;

            Muzzle(muzzle, shot.Aim, seconds - fire);
            Trail(shot, flown, seconds, fire);
            for (int i = 0; i < path.Bounces.Count; i++)
                Ricochet(shot.Map(path.Bounces[i].X, path.Bounces[i].Z), path.Bounces[i].NormalX, path.Bounces[i].NormalZ, seconds - shot.BounceAt(i), path.Bounces[i].N);
            if (path.End == BankShotEnd.Embed) Embed(shot.Map(path.EndPoint.X, path.EndPoint.Z), path.Embed.NormalX, path.Embed.NormalZ, endAge, shot.MaxBounces);
            if (seconds < endAt) Bullet(shot, flown, path.BouncesBy(flown));
            else if (path.End == BankShotEnd.Range && endAge < 0.2f) Bullet(shot, length, path.Bounces.Count);
        }

        /// <summary>A quad whose long side points along <paramref name="degrees"/>: the sketch's rect.</summary>
        private static void Rect(Vector2 centre, float along, float across, float degrees, Color colour, float altitude) =>
            Sprite(centre, along, across, colour, solid, altitude, -degrees);

        /// <summary>A two-point line of fixed width: the lib's line(..., 'none').</summary>
        private static void Segment(Vector2 a, Vector2 b, float width, Color colour, Material material, float altitude)
        {
            Vector2[] pts = GokuGraphics.Points(2);
            pts[0] = a;
            pts[1] = b;
            GokuGraphics.Line(pts, width, colour, material, altitude, GokuGraphics.Taper.None);
        }

        /// <summary>The sketch's heavy pistol lying level at hand height along the aim; <paramref name="kick"/> slides it back.</summary>
        internal static void Pistol(Vector2 hand, float degrees, Vector2 sun, float strength, float kick)
        {
            Vector2 dir = Turn(degrees), across = new Vector2(-dir.y, dir.x), ground = hand - dir * kick;
            Vector2 p = new Vector2(ground.x, ground.y + T.HandHeight * Lift), shadow = ground + sun * T.HandHeight;
            float layer = PawnLayer + 0.06f;
            Rect(shadow + dir * 0.16f, 0.48f, 0.11f, degrees, Fade(Body, strength * 0.5f), ShadowLayer);
            Rect(p + dir * -0.03f + across * -0.07f, 0.15f, 0.075f, degrees - 70f, IronDark, layer);
            Rect(p + dir * 0.14f, 0.36f, 0.09f, degrees, IronDark, layer + 0.002f);
            Rect(p + dir * 0.17f + across * 0.005f, 0.30f, 0.055f, degrees, Iron, layer + 0.004f);
            Rect(p + dir * 0.36f, 0.10f, 0.045f, degrees, Iron, layer + 0.0041f);
            Rect(p + dir * 0.17f + across * 0.025f, 0.27f, 0.014f, degrees, Fade(IronLit, 0.9f), layer + 0.006f);
        }

        /// <summary>
        /// The charge on the barrel, <paramref name="u"/> 0 to 1: a glow at the muzzle that grows and
        /// turns from pale to hot, a heat line along the barrel, 12 motes drawn in from about a cell
        /// round. At full charge the glow flickers. All additive light.
        /// </summary>
        internal static void Charge(Vector2 hand, Vector2 muzzle, float degrees, float u, float seconds)
        {
            if (u <= 0f) return;
            Vector2 q = Lifted(muzzle), dir = Turn(degrees);
            Color col = Color.Lerp(Tracer, TracerHot, u);
            float flick = u >= 0.97f ? 0.12f * Mathf.Sin(seconds * 42f) : 0f, a = Mathf.Clamp01(u * 1.2f) + flick;
            Streak(Lifted(hand + dir * 0.05f), q, 0.09f + 0.07f * u, Fade(col, 0.8f * a), whiteGlow, Overhead + 0.07f, 4);
            Sprite(q, 0.3f + 0.8f * u, 0.27f + 0.7f * u, Fade(col, 0.6f * a), glow, Overhead + 0.071f);
            Sprite(q, 0.12f + 0.24f * u, 0.11f + 0.21f * u, Fade(Core, 0.95f * a), glow, Overhead + 0.072f);
            for (int i = 0; i < 12; i++)
            {
                float turn = seconds * (1.1f + Rand(i + 300) * 0.5f) + Rand(i + 310), ph = turn - Mathf.Floor(turn);   // 0 far out, 1 at the muzzle
                float r = (1.15f - 0.25f * u) * (1f - ph) + 0.04f, ang = Rand(i + 320) * Mathf.PI * 2f + seconds * 1.6f * (i % 2 == 1 ? 1f : -1f) + ph * 1.2f;
                var g = new Vector2(q.x + Mathf.Cos(ang) * r, q.y + Mathf.Sin(ang) * r * 0.75f);
                var tail = new Vector2(q.x + Mathf.Cos(ang) * (r + 0.12f), q.y + Mathf.Sin(ang) * (r + 0.12f) * 0.75f);
                float al = Mathf.Clamp01(u * 1.5f) * Mathf.Max(0f, Mathf.Sin(ph * Mathf.PI)) * 0.8f;
                Streak(tail, g, 0.04f, Fade(col, al), whiteGlow, Overhead + 0.073f, 3);
                Sprite(g, 0.09f, 0.08f, Fade(Core, al), glow, Overhead + 0.074f);
            }
        }

        /// <summary>The muzzle flash and the smoke after it. <paramref name="muzzle"/> is the muzzle's ground point.</summary>
        internal static void Muzzle(Vector2 muzzle, float degrees, float age)
        {
            if (age < 0f) return;
            Vector2 q = Lifted(muzzle), dir = Turn(degrees);
            if (age < 0.09f)
            {
                float a = 1f - age / 0.09f;
                GokuGraphics.Glint(q, 0.55f * (1f + age * 3f), a, GokuGraphics.Flare, degrees);
                Streak(q, q + dir * (0.55f + age * 3f), 0.16f * a, Fade(GokuGraphics.Flare, a), whiteGlow, Overhead + 0.12f, 4);
            }
            for (int i = 0; i < 3; i++)
            {
                float u = (age - i * 0.06f) / 0.7f;
                if (u < 0f || u > 1f) continue;
                Vector2 g = q + dir * (0.25f + u * 0.8f + i * 0.12f);
                Sprite(new Vector2(g.x + (Rand(i + 700) - 0.5f) * 0.2f, g.y + u * 0.35f + (Rand(i + 710) - 0.5f) * 0.15f), 0.25f + u * 0.5f, 0.22f + u * 0.45f,
                    Fade(Smoke, (1f - u) * 0.32f), PowerPoleGraphics.puff, Overhead + 0.02f + i * 0.0002f);
            }
        }

        /// <summary>The bullet <paramref name="d"/> cells along the flight with its tracer tail; <paramref name="bounces"/> taken so far.</summary>
        internal static void Bullet(in BankShotShot shot, float d, int bounces)
        {
            Vector2 q = Lifted(At(shot, d, out Vector2 dir));
            float tail = Mathf.Min(0.5f, d), w = T.TracerWidth(bounces);
            Color col = Tracing(bounces, shot.MaxBounces);
            Streak(q - dir * tail, q, w * 1.6f, Fade(col, 0.9f), whiteGlow, Overhead + 0.10f, 6);
            Sprite(q, 0.30f + 0.05f * bounces, 0.26f + 0.05f * bounces, Fade(col, 0.8f), glow, Overhead + 0.11f);
            Streak(q - dir * 0.16f, q + dir * 0.04f, w * 0.55f, Fade(Core, 1f), whiteGlow, Overhead + 0.115f, 4);
        }

        /// <summary>
        /// The flown part of the flight: a bright trace that fades <see cref="BankShotTiming.TrailLife"/>
        /// after the bullet passed, and a smoke thread under it that lingers. Cut every half cell and at
        /// every bounce, so no piece crosses a corner.
        /// </summary>
        internal static void Trail(in BankShotShot shot, float flown, float seconds, float fireAt)
        {
            BankShotPath path = shot.Path;
            cuts.Clear();
            cuts.Add(0);
            for (double d = 0.5; d < flown; d += 0.5) cuts.Add(d);
            for (int i = 0; i < path.Bounces.Count; i++) if (path.Bounces[i].D < flown) cuts.Add(path.Bounces[i].D);
            cuts.Add(flown);
            cuts.Sort();
            int n = 0;
            for (int i = 0; i + 1 < cuts.Count; i++)
            {
                double d0 = cuts[i], d1 = cuts[i + 1];
                if (d1 - d0 < 0.01) continue;
                float age = seconds - (fireAt + (float)((d0 + d1) / 2) / shot.Speed);
                if (age < 0f) continue;
                while (n < path.Bounces.Count && path.Bounces[n].D <= d0 + 0.001) n++;
                Vector2 a = Lifted(At(shot, d0, out _)), b = Lifted(At(shot, d1, out _));
                float bright = Mathf.Exp(-age / T.TrailLife);
                Segment(a, b, T.TracerWidth(n) * (0.7f + 0.3f * bright), Fade(Tracing(n, shot.MaxBounces), 0.75f * bright), whiteGlow, Overhead + 0.08f);
                Segment(a, b, 0.05f + 0.04f * Mathf.Clamp01(age / 1.2f), Fade(Smoke, 0.30f * Mathf.Exp(-age / 1.8f) * Mathf.Clamp01(age / 0.15f)), solid, Overhead + 0.015f);
            }
        }

        /// <summary>
        /// The <paramref name="n"/>-th ricochet at <paramref name="at"/>, <paramref name="age"/> seconds
        /// ago, off a face with the given outward normal. The chip stays; the flash, sparks and dust last
        /// under half a second.
        /// </summary>
        internal static void Ricochet(Vector2 at, int normalX, int normalZ, float age, int n)
        {
            if (age < 0f) return;
            Vector2 q = Lifted(at);
            float nDeg = Mathf.Atan2(normalZ, normalX) * Mathf.Rad2Deg, k = 0.8f + 0.25f * n;
            Sprite(q, 0.16f * k, 0.13f * k, Fade(Chip, 0.85f), soft, WallTop + 0.01f);
            for (int i = 0; i < 4; i++)
            {
                float a = (nDeg + (Rand(i + 20 + n * 9) - 0.5f) * 160f) * Mathf.Deg2Rad, r = 0.06f + Rand(i + 30 + n * 9) * 0.07f;
                Sprite(new Vector2(q.x + Mathf.Cos(a) * r, q.y + Mathf.Sin(a) * r), 0.06f, 0.05f, Fade(ChipLit, 0.8f), soft, WallTop + 0.011f + i * 0.0001f);
            }
            if (age < 0.12f) GokuGraphics.Glint(q, (0.45f + 0.12f * n) * (1f + age * 2f), 1f - age / 0.12f, GokuGraphics.Flare, nDeg);
            int sparks = 7 + 3 * n;
            for (int i = 0; i < sparks; i++)
            {
                float life = 0.22f + Rand(i + n * 31) * 0.18f, u = age / life;
                if (u > 1f) continue;
                float a = (nDeg + (Rand(i + 50 + n * 31) - 0.5f) * 150f) * Mathf.Deg2Rad, v = (2.5f + Rand(i + 60 + n * 31) * 4f) * k;
                float x = q.x + Mathf.Cos(a) * v * age, z = q.y + Mathf.Sin(a) * v * age - 5f * age * age;
                float dx = Mathf.Cos(a) * v, dz = Mathf.Sin(a) * v - 10f * age, len = Mathf.Sqrt(dx * dx + dz * dz), tip = 0.06f + 0.05f * (1f - u);
                if (len == 0f) len = 1f;
                Streak(new Vector2(x - dx / len * tip, z - dz / len * tip), new Vector2(x, z), 0.035f, Fade(Color.Lerp(GokuGraphics.Flare, TracerHot, u), 1f - u * u),
                    whiteGlow, Overhead + 0.13f, 3);
            }
            for (int i = 0; i < 3; i++)
            {
                float u = (age - i * 0.04f) / 0.5f;
                if (u < 0f || u > 1f) continue;
                var g = new Vector2(q.x + normalX * (0.15f + u * 0.45f) + (Rand(i + 80 + n) - 0.5f) * 0.25f, q.y + normalZ * (0.15f + u * 0.45f) + u * 0.25f);
                Sprite(g, 0.22f + u * 0.35f, 0.2f + u * 0.3f, Fade(PowerPoleGraphics.Dust, (1f - u) * 0.35f), PowerPoleGraphics.puff, Overhead + 0.03f + i * 0.0002f);
            }
        }

        /// <summary>The bullet embeds after its last allowed contact: the ricochet of that contact, a bigger chip and a fading glow.</summary>
        internal static void Embed(Vector2 at, int normalX, int normalZ, float age, int maxBounces)
        {
            if (age < 0f) return;
            Ricochet(at, normalX, normalZ, age, maxBounces);
            Vector2 q = Lifted(at);
            Sprite(q, 0.22f, 0.18f, Fade(Chip, 0.9f), soft, WallTop + 0.012f);
            if (age < 0.5f) Sprite(q, 0.3f, 0.25f, Fade(TracerHot, 0.6f * (1f - age / 0.5f)), glow, Overhead + 0.05f);
        }

        /// <summary>
        /// The bullet hits a pawn standing at <paramref name="feet"/>, flying along <paramref name="dir"/>:
        /// a flash at the chest, blood thrown on past the pawn, a spatter on the floor that stays.
        /// </summary>
        internal static void Wound(Vector2 feet, Vector2 dir, float age, int bounces)
        {
            if (age < 0f) return;
            var chest = new Vector2(feet.x, feet.y + T.ChestHeight * Lift);
            float k = 0.8f + 0.2f * bounces, heading = Mathf.Atan2(dir.y, dir.x);
            if (age < 0.1f) GokuGraphics.Glint(chest, 0.5f * k, 1f - age / 0.1f, GokuGraphics.Flare);
            if (age < 0.12f) Sprite(chest, 0.5f * k, 0.4f * k, Fade(Blood, 0.8f * (1f - age / 0.12f)), soft, Overhead + 0.09f);
            for (int i = 0; i < 8; i++)
            {
                float life = 0.25f + Rand(i + 900) * 0.12f, u = age / life;
                if (u > 1f) continue;
                float a = heading + (Rand(i + 910) - 0.5f) * 1.1f, v = (2f + Rand(i + 920) * 3.5f) * k;
                Sprite(new Vector2(chest.x + Mathf.Cos(a) * v * age, chest.y + Mathf.Sin(a) * v * age - 6f * age * age), 0.16f * k, 0.12f * k,
                    Fade(Blood, 1f - u * u), soft, Overhead + 0.0901f + i * 0.0001f);
            }
            // Floor spatter beyond the pawn, growing over 0.3 s and then staying. The sketch turns it by
            // +heading where every other quad turns by -heading; kept as the sketch draws it.
            float grow = Mathf.Clamp01(age / 0.3f);
            Sprite(feet + dir * 0.55f, 1.0f * grow * k, 0.55f * grow * k, Fade(Blood, 0.75f * grow), soft, Floor + 0.02f, heading * Mathf.Rad2Deg);
            for (int i = 0; i < 5; i++)
            {
                if (grow < 0.5f + i * 0.1f) continue;
                float a = heading + (Rand(i + 930) - 0.5f) * 0.9f, r = 0.5f + Rand(i + 940) * 0.9f * k;
                Sprite(new Vector2(feet.x + Mathf.Cos(a) * r, feet.y + Mathf.Sin(a) * r), 0.14f + Rand(i + 950) * 0.12f, 0.11f + Rand(i + 960) * 0.09f,
                    Fade(Blood, 0.8f), soft, Floor + 0.021f + i * 0.0001f);
            }
        }

        /// <summary>
        /// The aim line the player sees before the shot: pale dashes along the whole flight, a square on
        /// the wall cell of the first contact, a dot at every contact and a red dot where it embeds.
        /// Used by the targeter too. <paramref name="alpha"/> fades it.
        /// </summary>
        internal static void Preview(in BankShotShot shot, float alpha, float tileLift)
        {
            if (alpha <= 0f) return;
            BankShotPath path = shot.Path;
            const double dash = 0.32, gap = 0.22;
            for (double d = 0; d < path.Length; d += dash + gap)
                Streak(Lifted(At(shot, d, out _)), Lifted(At(shot, System.Math.Min(path.Length, d + dash), out _)), 0.05f, Fade(Pale, 0.55f * alpha), whiteGlow, Overhead + 0.05f, 2);
            if (path.Bounces.Count > 0)
            {
                Vector2 c = shot.Map(path.Bounces[0].CellX, path.Bounces[0].CellZ);
                c.y += tileLift;
                const float h = 0.48f, edge = 0.05f;
                Color line = Fade(Pale, 0.8f * alpha);
                Band(new Vector2(c.x - h, c.y + h), new Vector2(c.x + h, c.y + h), new Vector2(c.x - h, c.y + h - edge), new Vector2(c.x + h, c.y + h - edge), line, WallTop + 0.02f);
                Band(new Vector2(c.x - h, c.y - h + edge), new Vector2(c.x + h, c.y - h + edge), new Vector2(c.x - h, c.y - h), new Vector2(c.x + h, c.y - h), line, WallTop + 0.0201f);
                Band(new Vector2(c.x - h, c.y - h), new Vector2(c.x - h, c.y + h), new Vector2(c.x - h + edge, c.y - h), new Vector2(c.x - h + edge, c.y + h), line, WallTop + 0.0202f);
                Band(new Vector2(c.x + h - edge, c.y - h), new Vector2(c.x + h - edge, c.y + h), new Vector2(c.x + h, c.y - h), new Vector2(c.x + h, c.y + h), line, WallTop + 0.0203f);
            }
            for (int i = 0; i < path.Bounces.Count; i++)
                Sprite(Lifted(shot.Map(path.Bounces[i].X, path.Bounces[i].Z)), 0.2f, 0.17f, Fade(Pale, 0.7f * alpha), soft, Overhead + 0.06f + i * 0.0001f);
            if (path.End == BankShotEnd.Embed)
                Sprite(Lifted(shot.Map(path.EndPoint.X, path.EndPoint.Z)), 0.16f, 0.14f, Fade(Blocked, 0.7f * alpha), soft, Overhead + 0.0605f);
        }

        /// <summary>The lib's two-point band: a quad from the line a0-a1 to the line b0-b1.</summary>
        private static void Band(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1, Color colour, float altitude)
        {
            Sides(2, out Vector2[] a, out Vector2[] b);
            a[0] = a0; a[1] = a1; b[0] = b0; b[1] = b1;
            Strip(a, b, colour, solid, altitude);
        }
    }
}
