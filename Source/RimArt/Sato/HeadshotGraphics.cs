using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.AjinResetDraw;

namespace RimArt
{
    /// <summary>
    /// Times of the Headshot Reset picture, the defaults of the lab sketch (ajin-headshot-reset.js), in seconds. The
    /// cast and the pistol's times are counted from the cast's start, the shot, or the moment he stands, as named.
    /// </summary>
    public static class HeadshotTiming
    {
        /// <summary>The gun arm comes up from the hip over Draw and holds the pistol to the temple for Aim; the shot is at ShotAt, which is the cast's length.</summary>
        public const float Draw = 0.45f, Aim = 0.15f, ShotAt = Draw + Aim, CastLength = ShotAt;
        /// <summary>After the shot: the contact flash, one smoke puff's life (four puffs 0.05 s apart), the blood mist.</summary>
        public const float Flash = 0.08f, Smoke = 0.9f, SmokeGap = 0.05f, Mist = 0.5f;
        /// <summary>Every drop of the spray has landed this long after the shot (the slowest lands at 0.53 s).</summary>
        public const float SprayLanded = 0.55f;
        /// <summary>DrawCast draws nothing from this many seconds after the cast began (the last smoke puff).</summary>
        public const float CastEnd = ShotAt + Smoke + 3 * SmokeGap;
        /// <summary>The pistol falls from his temple to the floor by his right hand over Fall seconds from the shot.</summary>
        public const float Fall = 0.35f;
        /// <summary>When he stands: the pistol flies back to his hand over PickUp, is held lowered for Hang, fades over Vanish.</summary>
        public const float PickUp = 0.7f, Hang = 0.4f, Vanish = 0.3f, RiseEnd = PickUp + Hang + Vanish;
        /// <summary>The cover's grow and peel for this Reset (ResetInPlaceShot.Rebuild, Rise).</summary>
        public const float Rebuild = 0.8f, Rise = 0.7f;
        /// <summary>The camera shake at the shot.</summary>
        public const float Shake = 0.03f;
    }

    /// <summary>One frame of the Headshot Reset cast.</summary>
    public struct HeadshotCastShot
    {
        /// <summary>The standing pawn's draw position.</summary>
        public Vector3 Pawn;
        public Rot4 Facing;
        /// <summary>Seconds since the cast began; the shot is at HeadshotTiming.ShotAt.</summary>
        public float Seconds;
        /// <summary>The drawn hand's and sleeve's colours; left at zero alpha they are the sketch's (stand-in skin, white shirt).</summary>
        public Color Skin, Sleeve;
    }

    /// <summary>
    /// The Headshot Reset picture, the port of ajin-headshot-reset.js: Satō draws a pistol, puts it to his right temple
    /// and fires. Vanilla pawns have no arms, so the gun arm and the pistol are drawn here as quads over the real pawn
    /// (only the gun arm: the other arm is the stand-in's). After the shot the game lays him down at once, so the arm is
    /// not drawn after the shot: the contact flash and smoke stay at the muzzle, the blood spray flies ballistically from
    /// the far side of his head, the pistol falls by his right hand and lies there, and when he stands it comes back to
    /// his hand, is lowered and vanishes. The lying Reset itself is AjinResetGraphics.DrawInPlace with HeadFirst.
    ///
    /// Per facing, as the sketch: south, gun hand on screen west and the spray out east; north, the same mirrored with
    /// the pistol behind the head; east, a profile with the hand and pistol over the head and the spray leaving north,
    /// behind him; west, east mirrored as the game mirrors east sprites. Poses are the sketch's offsets from the draw
    /// position.
    ///
    /// Not ported: the stand-ins (Satō, the enemy, its aim line and shots, the colonist, sandbags), the head snap and
    /// the recoil (his sprite is the game's and he lies at the shot), the stains the drops leave and the patch on the
    /// exit side (vanilla blood filth at <see cref="SprayLandings"/>), the pool under his head (vanilla filth), and after
    /// Sever the stump and the arm regrowing (vanilla pawns show no arms).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HeadshotGraphics
    {
        private static readonly Color PistolColour = new Color(0.10f, 0.10f, 0.11f), Smoke = new Color(0.72f, 0.71f, 0.69f),
            FlashColour = new Color(1f, 0.85f, 0.55f), White = new Color(1f, 1f, 1f);
        /// <summary>The floor under a standing pawn is Ground cells north of its draw position; his head is HeadH above that.</summary>
        private const float Ground = -0.30f, HeadH = 0.72f;
        /// <summary>Gravity for the spray, screen cells per second squared.</summary>
        private const float G = 9f;
        private const int Drops = 26;
        private const float ArmWidth = 0.085f;
        // Barrel angles on screen, degrees anticlockwise from east, before mirroring: hanging, at the temple from the front
        // and from the side, lying on the floor.
        private const float Hang = -80f, Temple = 0f, TempleSide = 145f, FloorDeg = 200f;
        /// <summary>Where the pistol lies, in the downed layout (head west) round the lying body: by his right hand.</summary>
        private static readonly Vector2 LieGun = new Vector2(-0.62f, -0.46f);
        /// <summary>Without the cast's facing the pistol drops onto its spot from this far up the screen.</summary>
        private const float DropFrom = 0.45f;
        private static readonly float AirLayerFront = Overhead + 0.02f, AirLayerBehind = PawnLayer - 0.007f;
        private static Vector3[][] landings;

        // South and east draw as is; north and west are mirrored (m = -1). East and west are the profile.
        private static void Side(Rot4 facing, out bool profile, out float m)
        {
            profile = facing == Rot4.East || facing == Rot4.West;
            m = facing == Rot4.North || facing == Rot4.West ? -1f : 1f;
        }

        // The way the blood leaves his head, on the floor: away from the gun side, or north (away from the camera) in profile.
        private static Vector2 Exit(bool profile, float m) => profile ? new Vector2(0f, 1f) : new Vector2(m, 0f);

        private static float MirrorDeg(float deg, float m) => m > 0f ? deg : 180f - deg;
        private static float LerpDeg(float a, float b, float u) => a + (Mathf.Repeat(b - a + 180f, 360f) - 180f) * u;
        private static float HandDeg(bool profile, float m, float raise) =>
            MirrorDeg(LerpDeg(Hang, profile ? TempleSide : Temple, Smooth(raise)), m);

        /// <summary>The gun arm's shoulder, elbow and hand standing, raised by <paramref name="raise"/> (0 hanging, 1 at the temple).</summary>
        private static void GunArm(Vector2 home, bool profile, float m, float raise, out Vector2 shoulder, out Vector2 elbow, out Vector2 hand)
        {
            float e = Smooth(raise), bend = Mathf.Max(0f, Mathf.Sin(Mathf.PI * e));
            if (!profile)
            {
                shoulder = new Vector2(-0.25f, 0.12f);
                elbow = new Vector2(Mathf.Lerp(-0.31f, -0.47f, e), Mathf.Lerp(-0.04f, 0.27f, e));
                hand = new Vector2(Mathf.Lerp(-0.34f, -0.33f, e) - 0.10f * bend, Mathf.Lerp(-0.20f, 0.44f, e));
            }
            else
            {
                shoulder = new Vector2(0.02f, 0.12f);
                elbow = new Vector2(Mathf.Lerp(0.05f, 0.23f, e), Mathf.Lerp(-0.04f, 0.23f, e));
                hand = new Vector2(0.07f + 0.08f * bend, Mathf.Lerp(-0.21f, 0.38f, e));
            }
            shoulder = home + new Vector2(shoulder.x * m, shoulder.y);
            elbow = home + new Vector2(elbow.x * m, elbow.y);
            hand = home + new Vector2(hand.x * m, hand.y);
        }

        /// <summary>A handgun gripped at <paramref name="hand"/>, barrel along <paramref name="deg"/>: outline, grip, slide. Returns the muzzle.</summary>
        private static Vector2 Pistol(Vector2 hand, float deg, float layer, float alpha)
        {
            Vector2 d = Turn(deg), n = new Vector2(d.y, -d.x);
            // The grip hangs off the barrel's lower side on screen.
            if (n.y > 0f || (Mathf.Abs(n.y) < 1e-3f && n.x > 0f)) n = -n;
            Vector2 back = hand - d * 0.03f, muzzle = hand + d * 0.12f, g0 = hand - d * 0.01f, g1 = g0 + n * 0.075f - d * 0.02f;
            Bar(back, muzzle, 0.065f, Fade(Outline, alpha), layer);
            Bar(g0, g1, 0.06f, Fade(Outline, alpha), layer + 0.0002f);
            Bar(g0, g1, 0.038f, Fade(PistolColour, alpha), layer + 0.0004f);
            Bar(back, muzzle, 0.042f, Fade(PistolColour, alpha), layer + 0.0006f);
            return muzzle;
        }

        private static void Arm(Vector2 shoulder, Vector2 elbow, Vector2 hand, Color sleeve, Color skin, float alpha)
        {
            LimbSeg(shoulder, elbow, ArmWidth, sleeve, sleeve, OnPawn + 0.0015f, alpha);
            LimbSeg(elbow, hand, ArmWidth, sleeve, skin, OnPawn + 0.0016f, alpha);
        }

        /// <summary>
        /// The cast: the gun arm comes up from the hip with the pistol (fading in over 0.08 s) and holds it to the temple;
        /// at the shot, the contact flash and smoke at the muzzle, a blood mist and 26 drops flying from the far side of
        /// his head and landing (the slow ones close, a few fast ones up to 1.5 cells out). Nothing from HeadshotTiming.CastEnd.
        /// </summary>
        public static void DrawCast(HeadshotCastShot s)
        {
            float t = s.Seconds;
            if (t < 0f || t >= HeadshotTiming.CastEnd) return;
            var home = new Vector2(s.Pawn.x, s.Pawn.z);
            Begin(home);
            Side(s.Facing, out bool profile, out float m);

            if (t < HeadshotTiming.ShotAt)
            {
                float raise = Mathf.Clamp01(t / HeadshotTiming.Draw);
                GunArm(home, profile, m, raise, out Vector2 shoulder, out Vector2 elbow, out Vector2 hand);
                Arm(shoulder, elbow, hand, s.Sleeve.a > 0f ? s.Sleeve : Shirt, s.Skin.a > 0f ? s.Skin : Skin, 1f);
                // North: the pistol at the temple is behind his head.
                Pistol(hand, HandDeg(profile, m, raise), s.Facing == Rot4.North ? BehindPawn : OnPawn + 0.002f, Mathf.Clamp01(t / 0.08f));
                return;
            }

            float age = t - HeadshotTiming.ShotAt;
            GunArm(home, profile, m, 1f, out _, out _, out Vector2 atTemple);
            Vector2 muzzle = atTemple + Turn(HandDeg(profile, m, 1f)) * 0.12f;
            if (age < HeadshotTiming.Flash)
            {
                float u = age / HeadshotTiming.Flash;
                Sprite(muzzle, 0.34f * (1f + u * 0.4f), 0.34f * (1f + u * 0.4f), Fade(FlashColour, 1f - u), glow, Overhead + 0.04f);
                Sprite(muzzle, 0.14f, 0.14f, Fade(White, 1f - u), glow, Overhead + 0.041f);
            }
            for (int j = 0; j < 4; j++)
            {
                float u = (age - j * HeadshotTiming.SmokeGap) / HeadshotTiming.Smoke;
                if (u <= 0f || u >= 1f) continue;
                Sprite(new Vector2(muzzle.x + (R(j + 600) - 0.5f) * 0.12f + u * 0.08f, muzzle.y + u * 0.38f), 0.10f + u * 0.26f, 0.09f + u * 0.22f,
                    Fade(Smoke, (1f - u) * 0.38f), Puff, Overhead + 0.035f + j * 0.0002f);
            }
            Spray(home, profile, m, age);
        }

        // One drop of the spray: its direction, speed, upward launch and the time it lands.
        private static void Drop(int i, bool profile, float baseAngle, out float a, out float v, out float up, out float land)
        {
            a = baseAngle + (R(i + 500) + R(i + 505) - 1f) * (profile ? 0.95f : 0.6f);
            v = 0.7f + Mathf.Pow(R(i + 510), 1.7f) * 2.9f;
            // East and west: the flight north already rises on screen, so no extra upward launch.
            up = profile ? (R(i + 520) - 0.6f) * 0.8f : (R(i + 520) - 0.25f) * 1.4f;
            land = (up + Mathf.Sqrt(up * up + 2f * G * HeadH)) / G;
        }

        // Where a drop is on screen u seconds after the shot: along the floor from the exit point, lifted by its height.
        private static Vector2 DropAt(Vector2 from, float a, float v, float up, float u) =>
            new Vector2(from.x + Mathf.Cos(a) * v * u, from.y + Mathf.Sin(a) * v * u + Mathf.Max(0f, HeadH + up * u - 0.5f * G * u * u));

        private static void Spray(Vector2 home, bool profile, float m, float age)
        {
            Vector2 exit = Exit(profile, m), from = home + exit * 0.2f + new Vector2(0f, Ground);
            float baseAngle = Mathf.Atan2(exit.y, exit.x), air = profile ? AirLayerBehind : AirLayerFront;
            for (int i = 0; i < Drops; i++)
            {
                Drop(i, profile, baseAngle, out float a, out float v, out float up, out float land);
                if (age >= land) continue;
                Vector2 q = DropAt(from, a, v, up, age), q0 = DropAt(from, a, v, up, Mathf.Max(0f, age - 0.035f));
                Bar(q0, q, i % 6 == 0 ? 0.05f : 0.03f, Blood, air + i * 0.0001f);
            }
            if (age >= HeadshotTiming.Mist) return;
            float w = age / HeadshotTiming.Mist;
            for (int j = 0; j < 6; j++)
            {
                float d = w * (0.25f + R(j + 560) * 0.35f), sp = (R(j + 570) - 0.5f) * 0.5f;
                var c = new Vector2(home.x + exit.x * (0.2f + d) - exit.y * sp * d, home.y + Ground + HeadH + exit.y * (0.2f + d) + exit.x * sp * d - w * w * 0.15f);
                Sprite(c, 0.12f + w * 0.3f, 0.10f + w * 0.24f, Fade(Blood, (1f - w) * 0.5f), Puff, air - 0.001f + j * 0.0001f);
            }
        }

        /// <summary>
        /// Where the spray lands, as ground offsets from the standing pawn's draw position (x east, z north, y 0), for the
        /// game's blood filth: the patch on the exit side, a smaller one past it, then each of the 26 drops. All have
        /// landed by HeadshotTiming.SprayLanded. Offsets are screen positions the sketch draws the stains at; several
        /// share a cell.
        /// </summary>
        public static IEnumerable<Vector3> SprayLandings(Rot4 facing)
        {
            if (landings == null)
            {
                landings = new Vector3[4][];
                for (int r = 0; r < 4; r++)
                {
                    Side(new Rot4(r), out bool profile, out float m);
                    Vector2 exit = Exit(profile, m), from = exit * 0.2f + new Vector2(0f, Ground);
                    var list = new Vector3[Drops + 2];
                    Vector2 patch = exit * 0.55f + new Vector2(0f, Ground);
                    list[0] = new Vector3(patch.x, 0f, patch.y);
                    list[1] = new Vector3(patch.x + exit.x * 0.2f + exit.y * 0.06f, 0f, patch.y + exit.y * 0.2f - exit.x * 0.05f);
                    float baseAngle = Mathf.Atan2(exit.y, exit.x);
                    for (int i = 0; i < Drops; i++)
                    {
                        Drop(i, profile, baseAngle, out float a, out float v, out float up, out float land);
                        Vector2 q = DropAt(from, a, v, up, land);
                        list[i + 2] = new Vector3(q.x, 0f, q.y);
                    }
                    landings[r] = list;
                }
            }
            return landings[facing.AsInt];
        }

        /// <summary>
        /// The pistol after the shot while he lies: it falls over HeadshotTiming.Fall from his temple to the floor by his
        /// right hand and lies there, turned with the body. <paramref name="castFacing"/> and <paramref name="standingAt"/>
        /// (the standing draw position; <paramref name="body"/> when null) place the temple it falls from; without a facing
        /// it drops straight onto its spot.
        /// </summary>
        public static void DrawLyingPistol(Vector3 body, float bodyAngle, float secondsSinceShot, Rot4? castFacing = null, Vector3? standingAt = null)
        {
            if (secondsSinceShot < 0f) return;
            AjinFrame f = AjinFrame.Lying(body, bodyAngle);
            Vector2 floor = f.P(LieGun), at = floor;
            float floorDeg = FloorDeg - f.turn, deg = floorDeg;
            Begin(floor);
            if (secondsSinceShot < HeadshotTiming.Fall)
            {
                float u = Smooth(secondsSinceShot / HeadshotTiming.Fall);
                Vector2 from = floor + new Vector2(0f, DropFrom);
                float fromDeg = floorDeg;
                if (castFacing.HasValue)
                {
                    Vector3 p = standingAt ?? body;
                    Side(castFacing.Value, out bool profile, out float m);
                    GunArm(new Vector2(p.x, p.z), profile, m, 1f, out _, out _, out from);
                    fromDeg = HandDeg(profile, m, 1f);
                }
                at = Vector2.Lerp(from, floor, u);
                deg = LerpDeg(fromDeg, floorDeg, u);
            }
            Pistol(at, deg, ItemLayer + 0.001f, 1f);
        }

        /// <summary>
        /// The pistol as he stands: it flies from where it lay back into his hand over HeadshotTiming.PickUp while the gun
        /// arm hangs, is held lowered for Hang, then arm and pistol fade over Vanish. <paramref name="lyingAngle"/> and
        /// <paramref name="lyingBody"/> (<paramref name="pawn"/> when null) are the body angle and draw position he lay
        /// at, so the flight starts where DrawLyingPistol left it.
        /// </summary>
        public static void DrawRisePistol(Vector3 pawn, Rot4 facing, float secondsSinceStanding, float lyingAngle = AjinResetDraw.LabDownedAngle,
            Vector3? lyingBody = null, Color? skin = null, Color? sleeve = null)
        {
            float t = secondsSinceStanding;
            if (t < 0f || t >= HeadshotTiming.RiseEnd) return;
            var home = new Vector2(pawn.x, pawn.z);
            Begin(home);
            Side(facing, out bool profile, out float m);
            AjinFrame f = AjinFrame.Lying(lyingBody ?? pawn, lyingAngle);
            Vector2 floor = f.P(LieGun);
            float floorDeg = FloorDeg - f.turn;
            GunArm(home, profile, m, 0f, out Vector2 shoulder, out Vector2 elbow, out Vector2 hand);
            float u = Smooth(t / HeadshotTiming.PickUp), alpha = 1f - Mathf.Clamp01((t - HeadshotTiming.PickUp - HeadshotTiming.Hang) / HeadshotTiming.Vanish);
            Arm(shoulder, elbow, hand, sleeve ?? Shirt, skin ?? Skin, alpha);
            Pistol(Vector2.Lerp(floor, hand, u), LerpDeg(floorDeg, MirrorDeg(Hang, m), u), OnPawn + 0.002f, alpha);
        }
    }
}
