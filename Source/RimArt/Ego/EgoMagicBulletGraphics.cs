using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using T = RimArt.EgoMagicBulletTiming;

namespace RimArt
{
    /// <summary>One thing the shot line crosses: a pawn (its DrawPos) or a wall cell (the point on the line where it crosses).</summary>
    public struct EgoMagicBulletHit
    {
        public Vector2 At;
        /// <summary>Cells from the muzzle along the line. The bullet reaches it Along / 120 s after the shot.</summary>
        public float Along;
        public bool Wall;
    }

    /// <summary>
    /// One Magic Bullet shot as the picture needs it. Points are ground points on the map: a pawn's DrawPos in
    /// game, the sketch's stand-in positions in the preview.
    /// </summary>
    public struct EgoMagicBulletShot
    {
        /// <summary>The shooter's DrawPos when the shot fires: the line starts <see cref="EgoMagicBulletTiming.MuzzleAlong"/> cells from it.</summary>
        public Vector2 Shooter;
        /// <summary>Where the shooter's body is drawn now. The rifle, the count pips and the corroded look follow it; the preview rocks it back after the shot.</summary>
        public Vector2 Stand;
        /// <summary>
        /// Degrees, 0 east, 90 north: where the rifle aims before the shot. Corroded, the gun has already turned it
        /// to the nearest living pawn. On the seventh it is where the rifle turns from.
        /// </summary>
        public float Aim;
        /// <summary>The seventh's line, degrees: toward the beloved.</summary>
        public float SeventhAim;
        /// <summary>The shot number, 1 to 7; 7 is the seventh.</summary>
        public int Shot;
        /// <summary>The aim time (s) and the line's range (cells); balance, from the weapon's XML.</summary>
        public float Lead, Range;
        /// <summary>The weapon's corrosion is on: the contract circle, veins, eyes, wisps, lit barrel and violet pips.</summary>
        public bool Corroded;
        /// <summary>What the line crosses, the first <see cref="HitCount"/> of <see cref="Hits"/>, in any order.</summary>
        public EgoMagicBulletHit[] Hits;
        public int HitCount;
        /// <summary>
        /// A newer shot by the same shooter is up: it draws the rifle, the count and the corroded look, and this one draws
        /// only what its line left (circles, beam, bullet, hits). Nothing at all before it has fired.
        /// </summary>
        public bool LineOnly;
        /// <summary>
        /// The height the shooter's body is drawn at, its DrawPos.y: the rifle and the corroded look go
        /// <see cref="PawnBody.Over"/> above it, over the pawn's own parts. The preview passes <see cref="EgoMagicBulletGraphics.PawnLayer"/>.
        /// </summary>
        public float Altitude;
    }

    /// <summary>
    /// Draws a Magic Bullet shot, the port of the lab's ego-magic-bullet.js. The aim: the rifle level at chest
    /// height (black barrel, gold band round the chamber, navy stock), seven count pips over the head, the
    /// chamber glowing with blue sparks drifting up, the magic circles (1 to 5 by the shot number) opening a
    /// cell ahead of the muzzle and sliding back to 0.4 cells. The shot: a muzzle glint, the rifle kicks back
    /// 0.16 cells and its barrel jumps up 40 degrees, the light at the first circle with short rays, a pale air
    /// ring spreading 1.6 cells, gun smoke along the line, cyan speed lines, the bullet at 120 cells/s and the
    /// beam it leaves (core, halo, drifting streaks, 1 to 3 flickering bolts) fading in 2-cell pieces. Every
    /// pawn crossed gets the hit (EgoMagicBulletHitGraphics) and every wall a punched hole. The seventh turns
    /// the rifle to the beloved over 0.35 s first, opens five circles and a ring round the shooter, and fires
    /// the wide cyan-white beam. Corroded adds the look in EgoMagicBulletCircleGraphics.Corroded.
    ///
    /// The beam, bullet, air ring and smoke are level lines, quads and circles at chest height; the circles are
    /// gates facing the aim (EgoMagicBulletCircleGraphics.GateAxes), so nothing has a per-facing method. The rifle and
    /// the corroded look on the shooter are drawn from its own height (<see cref="EgoMagicBulletShot.Altitude"/>).
    ///
    /// Not drawn (the sketch's stand-ins and lab aids): every pawn's body and shadow, the hit pawns' flinch, the
    /// wall cells, the beloved's heart, the red dashed warning line to the beloved after shot six and the violet
    /// dashed line and floor ring on the corroded gun's target.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class EgoMagicBulletGraphics
    {
        internal static readonly Color White = new Color(1f, 1f, 1f), Beam = new Color(0.55f, 0.80f, 1f), BeamEdge = new Color(0.20f, 0.45f, 1f);
        internal static readonly Color CircleBlue = new Color(0.35f, 0.62f, 1f), CircleBright = new Color(0.50f, 0.78f, 1f), CircleDeep = new Color(0.12f, 0.22f, 0.80f);
        internal static readonly Color Cyan = new Color(0.55f, 0.96f, 1f), Halo = new Color(0.65f, 0.85f, 1f), Violet = new Color(0.72f, 0.62f, 1f);
        internal static readonly Color Barrel = new Color(0.07f, 0.07f, 0.09f), BarrelLit = new Color(0.30f, 0.31f, 0.36f),
            Gold = new Color(0.86f, 0.68f, 0.26f), Navy = new Color(0.11f, 0.14f, 0.38f);
        internal static readonly Color BeamViolet = new Color(0.55f, 0.45f, 1f), Pale = new Color(0.92f, 0.90f, 0.84f);
        internal static readonly Color Smoke = new Color(0.04f, 0.03f, 0.05f), Eye = new Color(0.45f, 0.75f, 1f),
            Fire = new Color(1f, 0.50f, 0.12f), FireCore = new Color(1f, 0.90f, 0.50f);
        internal static readonly Color Stagger = new Color(1f, 0.85f, 0.25f), Bolt = new Color(1f, 0.62f, 0.18f);
        /// <summary>The corroded look's veins, and its violet (the count pips).</summary>
        internal static readonly Color Vein = new Color(0.34f, 0.18f, 0.62f), Corrupt = new Color(0.55f, 0.28f, 0.88f);

        internal static readonly Material Puff = MaterialPool.MatFrom("RimArt/SixPaths/Puff", ShaderDatabase.Transparent);
        internal static readonly float PawnLayer = AltitudeLayer.Pawn.AltitudeFor(), ShadowLayer = AltitudeLayer.Shadows.AltitudeFor();
        /// <summary>A layer above every wall top, for the punched hole: the Paper Bomb sketches' WallTop. A wall has no seeded height, so it stays fixed.</summary>
        internal static readonly float WallTop = PawnLayer + 0.05f;
        internal const float Tau = 6.2831855f;

        /// <summary>A ground point raised to chest height as drawn: the line the bullet flies along.</summary>
        internal static Vector2 AtChest(Vector2 ground) => new Vector2(ground.x, ground.y + PawnBody.Chest);

        /// <summary>Everything from the aim to the tail. <paramref name="s"/> is the picture's clock, 0 when the shooter starts to aim.</summary>
        public static void Draw(in EgoMagicBulletShot shot, float s, Map map)
        {
            bool seventh = shot.Shot >= T.Shots;
            float fire = T.Fire(shot.Lead, seventh), flight = T.Flight(shot.Range);
            if (s < 0f || s >= T.End(shot.Lead, seventh, shot.Range) || !Shown(shot.Shooter, map)) return;
            Begin(shot.Shooter);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            int tier = T.Tier(shot.Shot);
            float lineDeg = seventh ? shot.SeventhAim : shot.Aim;
            Vector2 d = Turn(lineDeg), start = shot.Shooter + d * T.MuzzleAlong;
            bool fired = s >= fire;
            float age = fired ? s - fire : -1f, flown = fired ? Mathf.Min(shot.Range, age * T.Speed) : 0f;

            // --- what the line crosses: the hits on pawns, the holes punched in walls ---
            for (int i = 0; i < shot.HitCount && fired; i++)
            {
                EgoMagicBulletHit h = shot.Hits[i];
                float hitAge = s - (fire + h.Along / T.Speed);
                if (h.Wall) EgoMagicBulletHitGraphics.Punch(h.At, d, hitAge);
                else EgoMagicBulletHitGraphics.Wound(h.At, d, hitAge, tier, s);
            }

            // --- the shooter: the count, the rifle with its kick, the corroded look, the chamber glow ---
            float gunDeg = T.GunDegrees(s, shot.Lead, shot.Aim, seventh, shot.SeventhAim);
            Vector2 gd = Turn(gunDeg), stand = shot.Stand, headTop = new Vector2(stand.x, stand.y + PawnBody.HeadTop);
            Vector2 hand = stand + gd * T.GripAlong, muzzle = default;
            if (!shot.LineOnly)
            {
                Counter(headTop, shot.Shot, s, shot.Corroded ? Corrupt : Beam);
                Rifle(hand, gunDeg, shot.Altitude, sun, strength, T.Kick(age), T.Tilt(age), out Vector2 chamber, out muzzle);
                if (shot.Corroded) EgoMagicBulletCircleGraphics.Corroded(stand, chamber, muzzle, shot.Altitude, sun, strength, s);
                ChamberGlow(chamber, muzzle, headTop, T.ChamberGlow(s, fire), s);
            }
            else if (!fired) return;

            // --- the magic circles: open ahead of the muzzle, slide back to it, stay on the shot line once fired ---
            float open = T.Open(s, shot.Lead, seventh, shot.Range), ahead = T.Ahead(s, shot.Lead, seventh);
            Vector2 gateDir = fired ? d : gd, gateAt = fired ? AtChest(hand) + d * (T.RifleLen * 0.72f) : muzzle;
            EgoMagicBulletCircleGraphics.GateAxes(gateDir, out Vector2 H, out Vector2 V);
            Vector2 gate = gateAt + gateDir * ahead;
            for (int i = 0, n = T.Circles(shot.Shot); i < n; i++)
                EgoMagicBulletCircleGraphics.Draw(gateAt + gateDir * (ahead + i * T.CircleStep), T.CircleRadius * (1f + i * T.CircleGrow),
                    open * Mathf.Clamp01(1f - i * 0.1f), s, i + 1, H, V, H, true, sun, strength, Overhead + 0.04f + i * 0.00002f);
            if (tier == 2 && open > 0f)
            {
                Vector2 round = AtChest(shot.Shooter);
                Circle(round, open, 0.8f * open, Overhead + 0.037f, CircleBlue);
                Sprite(round, 2.4f * open, 2.4f * open, Fade(CircleDeep, 0.25f * open), glow, Overhead + 0.0365f);
            }
            if (!fired) return;

            // --- the shot: the air ring, gun smoke, speed lines past the circle, the muzzle glint, the beam, the bullet ---
            ShockRing(gate, age, tier);
            Vector2 mq = AtChest(hand + d * (T.RifleLen * 0.72f));
            MuzzleSmoke(mq, d, age);
            if (age < 0.3f)
                for (int i = 0; i < 8; i++)
                {
                    float v = age / 0.3f, along = -0.4f + Rand(i + 800) * 1.4f + v * 1.6f, acr = (Rand(i + 810) - 0.5f) * 1.8f, len = 0.25f + Rand(i + 820) * 0.4f;
                    var from = new Vector2(gate.x + d.x * along - d.y * acr, gate.y + d.y * along + d.x * acr);
                    Streak(from, from + d * len, 0.03f, Fade(Cyan, 0.9f * (1f - v)), whiteGlow, Overhead + 0.09f, 3);
                }
            float w = T.BeamWidth * T.TierWidth[tier];
            if (age < 0.07f) GokuGraphics.Glint(mq, 0.4f, 1f - age / 0.07f, Beam, lineDeg);
            EgoMagicBulletBeamGraphics.Draw(start, d, flown, age, w, T.BeamLife, s, tier, gate);
            if (flown < shot.Range)
            {
                Vector2 b = AtChest(start + d * flown);
                Streak(b - d * 0.9f, b, w * 2.2f, Fade(Beam, 0.9f), whiteGlow, Overhead + 0.10f, 6);
                Sprite(b, 0.32f, 0.28f, Fade(Beam, 0.9f), glow, Overhead + 0.11f);
                Streak(b - d * 0.2f, b + d * 0.04f, w * 0.8f, White, whiteGlow, Overhead + 0.115f, 4);
                // The small dark tip the source bullet has.
                Sprite(b + d * 0.06f, 0.07f, 0.05f, Fade(Smoke, 0.9f), soft, Overhead + 0.116f, lineDeg);
            }
        }

        /// <summary>
        /// The rifle held level along <paramref name="aimDeg"/> with no shot up: the count (pip <paramref name="shot"/> lit
        /// as the next) and, when <paramref name="corroded"/>, the corroded look. Drawn while the weapon has its wielder
        /// between shots, so the rifle and the look do not go out in the gaps. <paramref name="s"/> drives the pulses;
        /// <paramref name="body"/> is the wielder's DrawPos.y.
        /// </summary>
        public static void DrawHeld(Vector2 stand, float body, float aimDeg, int shot, bool corroded, float s, Map map)
        {
            if (!Shown(stand, map)) return;
            Begin(stand);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            Counter(new Vector2(stand.x, stand.y + PawnBody.HeadTop), shot, s, corroded ? Corrupt : Beam);
            Rifle(stand + Turn(aimDeg) * T.GripAlong, aimDeg, body, sun, strength, 0f, 0f, out Vector2 chamber, out Vector2 muzzle);
            if (corroded) EgoMagicBulletCircleGraphics.Corroded(stand, chamber, muzzle, body, sun, strength, s);
        }

        /// <summary>
        /// The seven pips 0.32 cells over the head top. Spent ones are dark; the next one is lit with a glow; pip
        /// seven is the big one and pulses when it is next.
        /// </summary>
        private static void Counter(Vector2 headTop, int shot, float s, Color tint)
        {
            const float pitch = 0.13f;
            float x0 = headTop.x - pitch * 3f, z = headTop.y + 0.32f;
            for (int i = 0; i < T.Shots; i++)
            {
                int n = i + 1;
                bool spent = n < shot, next = n == shot, last = n == T.Shots;
                float warm = next && last ? 0.5f + 0.5f * Mathf.Sin(s * 9f) : 0f, r = last ? 0.06f : 0.045f;
                var at = new Vector2(x0 + i * pitch, z);
                DrawMesh(disc, at, Overhead + 0.2f, r, r, 0f, spent ? CircleDeep : Fade(CircleDeep, 0.25f), solid);
                if (next) Sprite(at, 0.18f, 0.18f, Fade(tint, 0.5f + 0.3f * warm), glow, Overhead + 0.201f);
                if (!spent) DrawMesh(disc, at, Overhead + 0.202f, r + 0.005f, r + 0.005f, 0f, Fade(tint, 0.5f), solid);
            }
        }

        /// <summary>
        /// The rifle, level at chest height along <paramref name="deg"/> from the hand's ground point and drawn over
        /// the pawn: 0.03 over the corroded look's veins, from the pawn's height <paramref name="body"/>. <paramref name="kick"/> slides it back; <paramref name="tilt"/> (degrees) swings the muzzle up:
        /// cos(tilt) along the aim and Lift x sin(tilt) north, so facing east or west the barrel visibly rises and
        /// facing north or south it shortens. Its shadow stays flat along the aim.
        /// </summary>
        private static void Rifle(Vector2 hand, float deg, float body, Vector2 sun, float strength, float kick, float tilt, out Vector2 chamber, out Vector2 muzzle)
        {
            float layer = body + PawnBody.Over + 0.03f, tr = tilt * Mathf.Deg2Rad, ct = Mathf.Cos(tr);
            Vector2 a = Turn(deg), b = hand - a * kick, q = AtChest(b), sd = b + sun * 0.9f;
            var dv = new Vector2(a.x * ct, a.y * ct + T.Lift * Mathf.Sin(tr));
            float len = dv.magnitude, sdeg = Mathf.Atan2(dv.y, dv.x) * Mathf.Rad2Deg, L = T.RifleLen * len;
            Vector2 dd = dv / len;
            ChainSickleGraphics.Rect(sd + a * (T.RifleLen * 0.22f), T.RifleLen * ct, T.RifleW * 1.3f, deg, Fade(ChainSickleGraphics.Body, strength * 0.6f), ShadowLayer);
            ChainSickleGraphics.Rect(q - dd * (L * 0.17f), L * 0.28f, T.RifleW * 1.15f, sdeg, Navy, layer);
            ChainSickleGraphics.Rect(q + dd * (L * 0.28f), L * 0.88f, T.RifleW * 0.6f, sdeg, Barrel, layer + 0.002f);
            ChainSickleGraphics.Rect(q + dd * (L * 0.02f), L * 0.16f, T.RifleW, sdeg, Barrel, layer + 0.003f);
            ChainSickleGraphics.Rect(q + dd * (L * 0.02f), L * 0.13f, T.RifleW * 0.55f, sdeg, Gold, layer + 0.004f);
            ChainSickleGraphics.Rect(q + dd * (L * 0.11f), L * 0.02f, T.RifleW * 0.95f, sdeg, Gold, layer + 0.004f);
            ChainSickleGraphics.Rect(new Vector2(q.x + dd.x * L * 0.3f, q.y + dd.y * L * 0.3f + 0.012f), L * 0.8f, 0.012f, sdeg, Fade(BarrelLit, 0.8f), layer + 0.005f);
            chamber = q + dd * (L * 0.04f);
            muzzle = q + dd * (L * 0.72f);
        }

        /// <summary>
        /// Before the shot, <paramref name="u"/> 0 to 1: a blue glow at the chamber and six blue sparks drifting up,
        /// three round the muzzle and three round the head, each rising 0.5 cells over 1.1 s.
        /// </summary>
        private static void ChamberGlow(Vector2 c, Vector2 muzzle, Vector2 head, float u, float s)
        {
            if (u <= 0f) return;
            Sprite(c, 0.18f + 0.22f * u, 0.16f + 0.2f * u, Fade(CircleBlue, 0.6f * u), glow, Overhead + 0.07f);
            Sprite(c, 0.08f + 0.08f * u, 0.07f + 0.07f * u, Fade(White, 0.8f * u), glow, Overhead + 0.071f);
            for (int i = 0; i < 6; i++)
            {
                float ph = (s * 0.9f + Rand(i + 120)) % 1f;
                Vector2 home = i < 3 ? muzzle : head;
                var g = new Vector2(home.x + (Rand(i + 130) - 0.5f) * 1.0f, home.y + (Rand(i + 140) - 0.5f) * 0.5f + ph * 0.5f);
                float a = u * Mathf.Sin(ph * Mathf.PI);
                Sprite(g, 0.14f, 0.12f, Fade(CircleBlue, 0.7f * a), glow, Overhead + 0.072f);
                Sprite(g, 0.06f, 0.05f, Fade(White, 0.9f * a), glow, Overhead + 0.073f);
            }
        }

        /// <summary>
        /// The shot's pressure in the air: a thin pale ring at chest height spreading from the first circle to
        /// 1.6 cells (x1.3 per tier) in 0.3 s and thinning as it goes. Level, so it looks the same from every aim.
        /// </summary>
        private static void ShockRing(Vector2 c, float age, int tier)
        {
            if (age < 0f || age > T.ShockTime) return;
            float u = age / T.ShockTime, r = T.ShockRadius * (1f + 0.3f * tier) * (1f - Mathf.Pow(1f - u, 2.5f)), a = (1f - u) * 0.6f;
            // Four rings 2 % apart in two colours: each its own altitude so the lab's call order holds in game.
            for (int j = 0; j < 4; j++) Circle(c, r * (1f - j * 0.02f), a * (j > 0 ? 0.3f : 0.6f), Overhead + 0.035f + j * 0.00005f, j > 0 ? Halo : Pale);
            Sprite(c, r * 2.1f, r * 2.1f, Fade(Halo, 0.08f * (1f - u)), soft, Overhead + 0.034f);
        }

        /// <summary>Gun smoke: six thin pale puffs pushed from the muzzle along the line at chest height, out to 2.5 cells in 0.35 to 0.5 s.</summary>
        private static void MuzzleSmoke(Vector2 mq, Vector2 d, float age)
        {
            if (age < 0f || age > 0.5f) return;
            for (int i = 0; i < 6; i++)
            {
                float u = Mathf.Clamp01(age / (0.35f + 0.15f * Rand(i + 90)));
                if (u >= 1f) continue;
                float along = 0.2f + 2.3f * (1f - (1f - u) * (1f - u)) * (0.6f + 0.4f * Rand(i + 100)), acr = (Rand(i + 110) - 0.5f) * 0.35f * (1f + u);
                var q = new Vector2(mq.x + d.x * along - d.y * acr, mq.y + d.y * along + d.x * acr + u * 0.1f);
                float size = 0.18f + 0.3f * u;
                Sprite(q, size, size * 0.85f, Fade(Pale, 0.3f * (1f - u) * (1f - u)), Puff, Overhead + 0.03f);
            }
        }
    }
}
