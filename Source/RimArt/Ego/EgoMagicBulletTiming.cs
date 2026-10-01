using UnityEngine;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// Times and sizes of the Magic Bullet picture, the defaults of the lab sketch
    /// (Tools/VfxLab/web/sketches/ego-magic-bullet.js). Seconds in, numbers out, no drawing and no map.
    ///
    /// The picture's clock is 0 when the shooter starts to aim. Shots 1 to 6 fire at the aim time
    /// (<see cref="Lead"/>, 0.45 s); the seventh first turns the rifle to the beloved over
    /// <see cref="Swing"/> (0.35 s) and fires at 0.80 s. The bullet flies at <see cref="Speed"/> (120 cells/s),
    /// so it reaches the 40-cell range 0.33 s after the shot; the result is held <see cref="Hold"/> (1.5 s) and
    /// the picture ends <see cref="Tail"/> (0.5 s) after that: 2.78 s for shots 1 to 6, 3.13 s for the seventh.
    ///
    /// The balance numbers (<see cref="Range"/>, <see cref="Shots"/>, <see cref="Lead"/>) are the sketch's
    /// placeholders: the weapon will read them from its XML and pass range and aim time in
    /// <see cref="EgoMagicBulletShot"/>. Damage (18, the seventh 30) and the seventh's 20 s cooldown are rules
    /// the picture does not draw, so they are not here. The rest is shape and timing.
    ///
    /// Heights on a pawn are lib/pawn.js's real-size pawn (average body), measured in game: they are added to a
    /// pawn's DrawPos as they are and do not go through <see cref="PawnFit"/>, which fits the old 0.89-cell
    /// stand-in and would move them.
    /// </summary>
    public static class EgoMagicBulletTiming
    {
        /// <summary>Balance, the sketch's placeholders for the weapon's XML: the line's range (cells) and the aim time before the shot (s).</summary>
        public const float Range = 40f, Lead = 0.45f;
        /// <summary>The shot number that is the seventh: the count on the gun runs 1 to Shots and resets after it.</summary>
        public const int Shots = 7;

        /// <summary>The bullet's speed, cells/s: near-instant (40 cells in 0.33 s); the beam it leaves is what the player sees.</summary>
        public const float Speed = 120f;
        /// <summary>The beam fades in pieces this many cells long, each from when the bullet passed it.</summary>
        public const float Chunk = 2f;
        /// <summary>The seventh's turn to the beloved (s); the fade after the hold (s); the result is held this long (s).</summary>
        public const float Swing = 0.35f, Tail = 0.5f, Hold = 1.5f;
        /// <summary>A beam piece fades to 1/e in BeamLife s; the circles close over CircleClose s, 0.35 s after the bullet reaches the range.</summary>
        public const float BeamLife = 0.6f, CircleClose = 0.25f;
        /// <summary>The barrel jumps up KickTilt degrees in KickUp s, then swings down as 40 x e^(-3.2 t) cos(2.9 t): level again at 0.59 s.</summary>
        public const float KickTilt = 40f, KickUp = 0.05f, KickDamp = 3.2f, KickSwing = 2.9f;
        /// <summary>The rifle slides back KickSlide cells in the hands; the shooter rocks back RockBack cells.</summary>
        public const float KickSlide = 0.16f, RockBack = 0.09f;
        /// <summary>The air ring grows from the first circle to ShockRadius cells (x1.3 per tier) in ShockTime s.</summary>
        public const float ShockRadius = 1.6f, ShockTime = 0.3f;
        /// <summary>The circles open CircleFar cells ahead of the muzzle and slide back to CircleNear before the shot; each next one stands CircleStep further on and 45 % bigger.</summary>
        public const float CircleFar = 1f, CircleNear = 0.4f, CircleStep = 0.48f, CircleGrow = 0.45f;
        /// <summary>Circles per shot number, 1 to 7 (Limbus: circles per Magic Bullet count).</summary>
        public static readonly int[] CirclesFor = { 1, 1, 2, 2, 3, 4, 5 };
        /// <summary>The first circle's radius (cells) and the beam's width (cells) for shots 1 to 3; tiers widen it 1.5x and 2.2x.</summary>
        public const float CircleRadius = 0.55f, BeamWidth = 0.07f;
        public static readonly float[] TierWidth = { 1f, 1.5f, 2.2f };
        /// <summary>The rifle: 1.4 cells long and 0.075 wide (9 : 1, like the icon), held GripAlong cells ahead of the shooter's centre.</summary>
        public const float RifleLen = 1.4f, RifleW = 0.075f, GripAlong = 0.1f;
        /// <summary>The muzzle, cells from the shooter's centre along the aim: where the shot line starts.</summary>
        public const float MuzzleAlong = GripAlong + RifleLen * 0.72f;
        /// <summary>Cells between the beam bolts' bends.</summary>
        public const float BoltStep = 0.5f;
        /// <summary>Camera shakes on the shot, shots 1 to 6 and the seventh.</summary>
        public const float FireShake = 0.035f, SeventhShake = 0.06f;
        public const float Lift = SixPathsHeight.Lift;


        /// <summary>
        /// The preview's script, the sketch's stand-ins, in the aim frame (cells along the aim, cells to its left)
        /// from the shooter: the raider in front of the wall, three wall cells across the aim, the colonist behind
        /// the wall (corroded: walked up 2 cells away, nearer than the raider).
        /// </summary>
        public const float RaiderAlong = 2.5f, WallAlong = 4f, ColonistAlong = 6.5f, CorrodedAlong = 1.29f, CorrodedAcross = 1.53f;
        public const int WallHalfWidth = 1;
        /// <summary>
        /// The preview's script: the seventh's line is BelovedDeg from the aim, with the raider between and the
        /// beloved on it (cells from the shooter). A pawn within HitWidth cells of the line is hit.
        /// </summary>
        public const float BelovedDeg = 125f, BelovedDist = 5.5f, BetweenDist = 2.8f, HitWidth = 0.45f;

        /// <summary>Tier of the look: 0 for shots 1 to 3 (thin blue), 1 for 4 to 6 (wider, blue-violet), 2 for the seventh (cyan-white).</summary>
        public static int Tier(int shot) => shot >= Shots ? 2 : shot >= 4 ? 1 : 0;

        public static int Circles(int shot) => CirclesFor[Mathf.Clamp(shot, 1, Shots) - 1];

        /// <summary>When the shot fires: the aim time, plus the turn on the seventh.</summary>
        public static float Fire(float lead, bool seventh) => lead + (seventh ? Swing : 0f);

        /// <summary>Seconds the bullet takes to reach the range.</summary>
        public static float Flight(float range) => range / Speed;

        /// <summary>The picture's length on its own clock.</summary>
        public static float End(float lead, bool seventh, float range) => Fire(lead, seventh) + Flight(range) + Hold + Tail;

        /// <summary>sin(pi x) on 0..1, else 0, clamped at 0 so a Pow of it never sees single precision's -0.0000001.</summary>
        public static float Bump(float x) => x >= 0f && x <= 1f ? Mathf.Max(0f, Mathf.Sin(x * Mathf.PI)) : 0f;

        /// <summary>
        /// The shooter rocking back along the shot line, cells, <paramref name="age"/> s after the shot (negative
        /// before it): out and back over 0.3 s with 30 % held and let go over the next 0.5 s. The preview's script:
        /// the picture cannot move a real pawn, so the preview moves the drawn body point and an ability may pass
        /// its own.
        /// </summary>
        public static float Rock(float age) =>
            age < 0f ? 0f : RockBack * (Bump(age / 0.3f) * 0.7f + 0.3f * Mathf.Clamp01(age / 0.1f) * (1f - Smooth((age - 0.3f) / 0.5f)));

        /// <summary>Cells the rifle has slid back in the hands: out and back over 0.12 s, a quarter held and let go over 0.5 s.</summary>
        public static float Kick(float age) =>
            age < 0f ? 0f : KickSlide * (Bump(age / 0.12f) * 0.75f + 0.25f * Mathf.Clamp01(age / 0.06f) * (1f - Smooth((age - 0.12f) / 0.5f)));

        /// <summary>The barrel's tilt in degrees: up to <see cref="KickTilt"/> in <see cref="KickUp"/> s, then a damped swing, never below level.</summary>
        public static float Tilt(float age)
        {
            if (age < 0f) return 0f;
            if (age < KickUp) return KickTilt * Mathf.Sin(age / KickUp * Mathf.PI / 2f);
            float t = age - KickUp;
            return KickTilt * Mathf.Max(0f, Mathf.Exp(-KickDamp * t) * Mathf.Cos(KickSwing * t));
        }

        /// <summary>The chamber glow, 0 to 1: grows from 0.02 s to the shot and goes out over 0.12 s after it.</summary>
        public static float ChamberGlow(float s, float fire)
        {
            float age = s - fire;
            return Mathf.Clamp01((s - 0.02f) / (fire - 0.02f)) * (age >= 0f ? 1f - Mathf.Clamp01(age / 0.12f) : 1f);
        }

        /// <summary>
        /// How open the circles are, 0 to 1. Shots 1 to 6 open over the first 40 % of the aim; the seventh opens on
        /// its new line in the second half of the turn. Once fired they stay while the beam is up and close
        /// <see cref="CircleClose"/> s, 0.35 s after the bullet reaches the range.
        /// </summary>
        public static float Open(float s, float lead, bool seventh, float range)
        {
            float u = seventh ? Mathf.Clamp01((s - lead - Swing * 0.5f) / (Swing * 0.5f + 0.05f)) : Mathf.Clamp01(s / (lead * 0.4f));
            float age = s - Fire(lead, seventh);
            return Smooth(u) * (age >= 0f ? 1f - Smooth((age - Flight(range) - 0.35f) / CircleClose) : 1f);
        }

        /// <summary>Cells ahead of the muzzle the first circle stands: it opens at <see cref="CircleFar"/> and slides back to <see cref="CircleNear"/> just before the shot.</summary>
        public static float Ahead(float s, float lead, bool seventh)
        {
            float u = seventh ? Mathf.Clamp01((s - lead - Swing * 0.8f) / 0.15f) : Mathf.Clamp01((s - lead * 0.4f) / (lead * 0.6f));
            return CircleFar + (CircleNear - CircleFar) * Smooth(u);
        }

        /// <summary>The rifle's direction now, degrees: the aim, or on the seventh turning from the aim to the beloved over <see cref="Swing"/> s by the short way.</summary>
        public static float GunDegrees(float s, float lead, float aim, bool seventh, float seventhAim)
        {
            if (!seventh) return aim;
            float turn = Smooth((s - lead) / Swing), delta = Mathf.Repeat(seventhAim - aim + 180f, 360f) - 180f;
            return aim + turn * delta;
        }

        /// <summary>
        /// The preview's script: the sketch's stand-ins round a shooter at <paramref name="o"/> aiming
        /// <paramref name="aim"/> degrees, in the sketch's order raider, colonist, raider between, beloved. With
        /// <paramref name="corroded"/> the colonist stands nearer than the raider.
        /// </summary>
        public static void ScriptPawns(Vector2 o, float aim, bool corroded, Vector2[] into)
        {
            into[0] = InAimFrame(o, aim, RaiderAlong, 0f);
            into[1] = corroded ? InAimFrame(o, aim, CorrodedAlong, CorrodedAcross) : InAimFrame(o, aim, ColonistAlong, 0f);
            into[2] = o + VfxDraw.Turn(aim + BelovedDeg) * BetweenDist;
            into[3] = o + VfxDraw.Turn(aim + BelovedDeg) * BelovedDist;
        }

        /// <summary>The preview's script: the three wall cells' centres, snapped to whole cells from <paramref name="o"/> as the sketch's walls are.</summary>
        public static void ScriptWalls(Vector2 o, float aim, Vector2[] into)
        {
            for (int k = -WallHalfWidth; k <= WallHalfWidth; k++)
            {
                Vector2 c = InAimFrame(o, aim, WallAlong, k);
                // JavaScript's Math.round: halves go up.
                into[k + WallHalfWidth] = new Vector2(o.x + Mathf.Floor(c.x - o.x + 0.5f), o.y + Mathf.Floor(c.y - o.y + 0.5f));
            }
        }

        /// <summary>The preview's script: the beloved's direction from the shooter.</summary>
        public static float ScriptSeventhAim(float aim) => aim + BelovedDeg;

        /// <summary>
        /// The preview's script: the corroded gun's own aim, toward the nearest of <paramref name="pawns"/> (the first
        /// of equals, as the sketch's reduce), or <paramref name="aim"/> when there is nobody.
        /// </summary>
        public static float ScriptNearestAim(Vector2 o, float aim, Vector2[] pawns, int count)
        {
            int best = -1;
            for (int i = 0; i < count; i++)
                if (best < 0 || (pawns[i] - o).magnitude < (pawns[best] - o).magnitude) best = i;
            if (best < 0) return aim;
            Vector2 to = pawns[best] - o;
            return Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// The preview's script: what the line from <paramref name="start"/> along <paramref name="d"/> crosses, nearest
        /// first, walls listed before pawns at the same distance as the sketch's stable sort does. A wall cell is
        /// crossed when the line passes within half a cell of its centre and is marked where the line crosses; a pawn
        /// within <see cref="HitWidth"/>, marked where it stands. Returns how many were written.
        /// </summary>
        public static int ScriptHits(Vector2 start, Vector2 d, Vector2[] walls, int wallCount, Vector2[] pawns, int pawnCount, EgoMagicBulletHit[] into)
        {
            int n = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                bool wall = pass == 0;
                Vector2[] from = wall ? walls : pawns;
                for (int i = 0, count = wall ? wallCount : pawnCount; i < count; i++)
                {
                    Vector2 q = from[i] - start;
                    float along = q.x * d.x + q.y * d.y, across = Mathf.Abs(-q.x * d.y + q.y * d.x);
                    if (along <= 0f || across > (wall ? 0.5f : HitWidth)) continue;
                    var hit = new EgoMagicBulletHit { At = wall ? start + d * along : from[i], Along = along, Wall = wall };
                    int at = n++;
                    while (at > 0 && into[at - 1].Along > along) { into[at] = into[at - 1]; at--; }
                    into[at] = hit;
                }
            }
            return n;
        }

        /// <summary>The point <paramref name="along"/> cells down the aim from <paramref name="o"/> and <paramref name="across"/> cells to its left.</summary>
        public static Vector2 InAimFrame(Vector2 o, float aim, float along, float across)
        {
            Vector2 a = VfxDraw.Turn(aim);
            return o + a * along + new Vector2(-a.y, a.x) * across;
        }
    }
}
