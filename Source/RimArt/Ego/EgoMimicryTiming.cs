using System;
using UnityEngine;
using Verse;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// Times, poses and sizes of the Mimicry pictures (the E.G.O. flesh greatsword), the defaults of the
    /// lab sketch Tools/VfxLab/web/sketches/ego-mimicry.js. Seconds in, numbers out, no drawing and no map.
    ///
    /// A swing: the wind-up turns the blade edge-forward and takes it back to <see cref="Back"/> degrees
    /// behind the aim on the hand side in <see cref="Windup"/> s; the swing runs to <see cref="Through"/>
    /// degrees past the aim in <see cref="Swing"/> s, eased out, and hits when the blade crosses the aim
    /// (<see cref="HitAt"/>, 0.22 s in); it holds <see cref="SwingHold"/> s and returns to
    /// <see cref="Rest"/> in <see cref="Recover"/> s. The grown swing: the blade swells to
    /// <see cref="GrownScale"/>x in <see cref="Swell"/> s, rises overhead in <see cref="Raise"/> s, slams
    /// down in <see cref="Slam"/> s (the hit, <see cref="SlamAt"/>), holds <see cref="SlamHold"/> s and
    /// shrinks back in <see cref="Shrink"/> s.
    ///
    /// Body points use the lab's real-size stand-in (lib/pawn.js, average body, measured in game on
    /// 2026-09-29), so they are DrawPos plus these numbers with no PawnFit. A point with height is a
    /// Vector3: x east, y cells up, z north, with x and z in the pawn's DrawPos frame; it is drawn at
    /// z + <see cref="Ground"/> + y x <see cref="Lift"/> and its shadow falls from z + Ground.
    ///
    /// The balance numbers (<see cref="HealFraction"/> to <see cref="Adjacent"/>) are the sketch's
    /// placeholders from docs/ego-weapons.md: the rules PR reads them from CompProperties_EgoWeapon's XML
    /// and passes what a picture needs in its struct. Nothing here draws or reads them yet except the
    /// preview's script.
    /// </summary>
    public static class EgoMimicryTiming
    {
        // Balance placeholders, XML later: each hit heals 10 % of the damage dealt; 10 % per swing (or every
        // 8th hit) swells the blade for one downswing at 3.5x damage; 4 arm stages at +15 % melee damage each.
        public const float HealFraction = 0.1f, GrowChance = 0.1f, GrowDamageFactor = 3.5f, StageDamage = 0.15f;
        public const int GrowEveryHits = 8, ArmStages = 4;
        // Corrosion (the shared E.G.O. system): one action every 1.5 s for 40 s (the preview cuts it to 8.7 s);
        // Overclock: 5 actions 1 s apart at standing hostiles in reach only, standing still. A pawn nearer than
        // Adjacent cells is swung at where it stands; while corroded, anyone further is reached by a lunge of up
        // to LungeCells first. The rules read their numbers from CompProperties_EgoMimicry, not these.
        public const float CorrodedInterval = 1.5f, OverclockInterval = 1f, LungeCells = 2f, Adjacent = 1.6f;
        public const int CorrodedActions = 4, OverclockActions = 5;

        /// <summary>The blade's length in cells at size 1 (from the guard), and its size for the grown swing.</summary>
        public const float Blade = 1.09f, GrownScale = 2f;
        /// <summary>Swing angles from the aim on the hand side, in degrees: at rest, wound up, and where the swing ends.</summary>
        public const float Rest = -35f, Back = -110f, Through = 45f;
        public const float Windup = 0.18f, Swing = 0.12f, SwingHold = 0.06f, Recover = 0.3f;
        /// <summary>The wrist turns the blade edge-forward in WristTurn s at the start of the wind-up and back after the hold.</summary>
        public const float WristTurn = 0.08f;
        /// <summary>The crescent's tail runs CrescentLag s behind the blade; it fades over CrescentFade s after the swing.</summary>
        public const float CrescentLag = 0.06f, CrescentFade = 0.22f, CrescentThick = 0.42f;
        public const float Swell = 0.3f, Raise = 0.3f, Slam = 0.1f, SlamHold = 0.25f, Shrink = 0.45f;
        /// <summary>The overhead hands' height and the slam's low hands' height, cells up; the lean of the raised blade.</summary>
        public const float TopH = 1.5f, LowH = 0.45f;
        /// <summary>The arm's stages grow in StageGrow s, lose one in StageShrink s; the flesh creeps over the hand in Enter s and the arm recedes in Exit s.</summary>
        public const float StageGrow = 0.35f, StageShrink = 0.3f, Enter = 0.9f, Exit = 1f;
        /// <summary>A lunge takes LungeTime s; a downed pawn falls in FallTime s.</summary>
        public const float LungeTime = 0.2f, FallTime = 0.35f;
        /// <summary>The heal after a hit lasts FeedTime s: the blood leaves the steel in 0.35 s, the glow reaches the eye in 0.35 s, the eye glows 0.3 s.</summary>
        public const float FeedTime = 0.8f;

        public const float Lift = SixPathsHeight.Lift;
        /// <summary>The hands' height on screen above DrawPos (the body's other points are <see cref="PawnBody"/>).</summary>
        public const float HandScreen = -0.05f;
        /// <summary>The hands' and the chest's height in cells up from the ground contact.</summary>
        public const float HandH = (HandScreen - PawnBody.Ground) / Lift, ChestH = (PawnBody.Chest - PawnBody.Ground) / Lift;
        /// <summary>The hand sits HandOut cells in front of the body along the blade and HandAcross to the hand side.</summary>
        public const float HandOut = 0.18f, HandAcross = 0.1f;
        /// <summary>Along the sword from the hand, in cells at size 1: the pommel and the guard.</summary>
        public const float PommelA = -0.06f, GuardA = 0.22f;

        /// <summary>The share of the swing's time when the blade crosses the aim: the swing eases out with a cube, so 1 - cbrt(1 - 110/155) = 0.34.</summary>
        public static readonly float HitFrac = 1f - (float)Math.Pow(1.0 - (0.0 - Back) / (Through - Back), 1.0 / 3.0);

        /// <summary>Seconds from a swing's start to its hit.</summary>
        public static float HitAt => Windup + Swing * HitFrac;
        public static float SwingLength => Windup + Swing + SwingHold + Recover;

        /// <summary>The grown swing's marks from its start: the raise, the top, the hit, the end of the hold, shrunk, the end.</summary>
        public static float RaiseAt => Swell;
        public static float TopAt => Swell + Raise;
        public static float SlamAt => TopAt + Slam;
        public static float RiseAt => SlamAt + SlamHold;
        public static float ShrunkAt => RiseAt + Shrink;
        public static float GrownLength => ShrunkAt + 0.15f;

        internal static float EaseOut(float x)
        {
            float u = 1f - Mathf.Clamp01(x);
            return 1f - u * u * u;
        }

        /// <summary>sin(pi x) on 0..1, 0 outside; clamped at 0 because single-precision sin(pi) is slightly negative.</summary>
        internal static float Bump(float x) => x >= 0f && x <= 1f ? Mathf.Max(0f, Mathf.Sin(x * Mathf.PI)) : 0f;

        internal static Vector2 Dir(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

        /// <summary>Left of <paramref name="d"/>.</summary>
        internal static Vector2 Side(Vector2 d) => new Vector2(-d.y, d.x);

        internal static float DegOf(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;

        internal static Vector2 Unit(Vector2 v)
        {
            float l = Mathf.Sqrt(v.x * v.x + v.y * v.y);
            if (l == 0f) l = 1f;
            return new Vector2(v.x / l, v.y / l);
        }

        internal static Vector3 Unit(Vector3 v)
        {
            float l = Mathf.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
            if (l == 0f) l = 1f;
            return new Vector3(v.x / l, v.y / l, v.z / l);
        }

        /// <summary>A level direction of <paramref name="degrees"/> as a Vector3 (x, up, z).</summary>
        internal static Vector3 Level(float degrees)
        {
            Vector2 d = Dir(degrees);
            return new Vector3(d.x, 0f, d.y);
        }

        /// <summary>-1 when aiming west of north-south: the picture is mirrored so the flesh stays on top (the sketch's rule).</summary>
        public static float SignOf(float aim) => Mathf.Cos(aim * Mathf.Deg2Rad) < -1e-6f ? -1f : 1f;

        /// <summary>
        /// The mirror for a pawn facing <paramref name="facing"/>: -1 facing west, as Core mirrors a west-facing pawn and its
        /// weapon, else 1. A pawn facing north at a target a little west of north keeps the sword in its right hand, where
        /// <see cref="SignOf"/> would mirror it.
        /// </summary>
        public static float SignOf(Rot4 facing) => facing == Rot4.West ? -1f : 1f;

        /// <summary>The aim straight ahead of a pawn facing <paramref name="facing"/>: 0 east, 90 north, 180 west, 270 south.</summary>
        public static float AimOf(Rot4 facing) => facing == Rot4.East ? 0f : facing == Rot4.North ? 90f : facing == Rot4.West ? 180f : 270f;

        /// <summary>The facing a pawn aiming <paramref name="aim"/> degrees turns to (Core's flat angles run clockwise from north).</summary>
        public static Rot4 FacingOf(float aim) => Rot4.FromAngleFlat(90f - aim);

        /// <summary>The hand side: right of the aim, mirrored when <paramref name="sign"/> is -1 (0 takes <see cref="SignOf(float)"/>).</summary>
        public static Vector2 HandSide(float aim, float sign = 0f)
        {
            Vector2 s = Side(Dir(aim));
            return s * -(sign != 0f ? sign : SignOf(aim));
        }

        /// <summary>The blade's angle from the aim on the hand side, <paramref name="age"/> s into a swing (before the start it rests).</summary>
        public static float SwingAngle(float age)
        {
            if (age < 0f) return Rest;
            if (age < Windup) return Mathf.Lerp(Rest, Back, Smooth(age / Windup));
            if (age < Windup + Swing) return Mathf.Lerp(Back, Through, EaseOut((age - Windup) / Swing));
            float r0 = Windup + Swing + SwingHold;
            return age < r0 ? Through : Mathf.Lerp(Through, Rest, Smooth((age - r0) / Recover));
        }

        /// <summary>The wrist: +1 x sign shows the flesh on top at rest, -1 x sign the edge leading from the wind-up to the end of the hold.</summary>
        public static float SwingFlip(float age, float sign)
        {
            float turn = Smooth(age / WristTurn) * (1f - Smooth((age - Windup - Swing - SwingHold) / WristTurn));
            return sign * (1f - 2f * turn);
        }

        /// <summary>The hand: out in front of the body along <paramref name="degrees"/>, a little to the hand side, <paramref name="h"/> cells up.</summary>
        public static Vector3 HandAt(Vector2 pos, float degrees, float sign, float h = HandH)
        {
            Vector2 d = Dir(degrees), q = pos + d * HandOut + Side(d) * (-HandAcross * sign);
            return new Vector3(q.x, h, q.y);
        }

        /// <summary>
        /// The crescent behind a swing <paramref name="age"/> s in: the head (at the blade) and tail angles from
        /// the aim on the hand side, and its fade. False when there is nothing to draw.
        /// </summary>
        public static bool CrescentSpan(float age, out float head, out float tail, out float fade)
        {
            head = tail = fade = 0f;
            if (age < Windup || age > Windup + Swing + CrescentFade) return false;
            head = Mathf.Lerp(Back, Through, EaseOut((age - Windup) / Swing));
            tail = Mathf.Lerp(Back, Through, EaseOut((age - Windup - CrescentLag) / Swing));
            fade = 1f - Smooth((age - Windup - Swing) / CrescentFade);
            return head - tail >= 2f && fade > 0f;
        }

        /// <summary>
        /// The grown swing's pose <paramref name="age"/> s after it starts, for a wielder at <paramref name="pos"/>
        /// aiming <paramref name="aim"/>: the hand, the blade's 3D direction, its size, the wrist (flip) and the
        /// swell (0 to 1, which also opens the eyes). The raised blade leans back over the hand-side shoulder;
        /// its lean toward the hand side is 0.2 + 0.8 |sin aim|, so aiming north or south the cut is a diagonal
        /// on screen instead of a line along the screen's vertical axis.
        /// </summary>
        public static void GrownPose(float age, Vector2 pos, float aim, out Vector3 hand, out Vector3 blade, out float size,
            out float flip, out float swell, float sign = 0f)
        {
            if (sign == 0f) sign = SignOf(aim);
            Vector2 d = Dir(aim), hs = HandSide(aim, sign);
            float lean = 0.2f + 0.8f * Mathf.Abs(Mathf.Sin(aim * Mathf.Deg2Rad));
            Vector3 R = Level(aim + sign * Rest);
            Vector3 T = Unit(new Vector3(hs.x * 0.45f * lean - d.x * 0.55f, 0.8f, hs.y * 0.45f * lean - d.y * 0.55f));
            Vector3 B = SlamDir(aim);
            Vector3 hRest = HandAt(pos, aim + sign * Rest, sign);
            Vector2 top = pos + d * 0.05f + hs * 0.1f;
            var hTop = new Vector3(top.x, TopH, top.y);
            Vector3 hLow = LowHand(pos, aim, sign);
            // The edge leads the cut: which side of the raised blade faces the slam's motion on screen.
            Vector2 Ts = Unit(new Vector2(T.x, T.z + T.y * Lift)), Bs = new Vector2(B.x, B.z + B.y * Lift), n = Side(Ts);
            float slamFlip = n.x * (Bs.x - Ts.x) + n.y * (Bs.y - Ts.y) > 0f ? -1f : 1f;
            if (age < RaiseAt)
            {
                blade = R;
                hand = hRest;
            }
            else if (age < TopAt)
            {
                float e = Smooth((age - RaiseAt) / Raise);
                blade = Lerp3(R, T, e);
                hand = Mix3(hRest, hTop, e);
            }
            else if (age < SlamAt)
            {
                float c = Mathf.Clamp01((age - TopAt) / Slam), e = c * c;
                blade = Lerp3(T, B, e);
                hand = Mix3(hTop, hLow, e);
            }
            else if (age < RiseAt)
            {
                blade = B;
                hand = hLow;
            }
            else
            {
                float e = Smooth((age - RiseAt) / Shrink);
                blade = Lerp3(B, R, e);
                hand = Mix3(hLow, hRest, e);
            }
            swell = age < RaiseAt ? Smooth(age / RaiseAt) : age < RiseAt ? 1f : 1f - Smooth((age - RiseAt) / Shrink);
            size = 1f + (GrownScale - 1f) * swell;
            float turn = Smooth((age - RaiseAt) / 0.1f) * (1f - Smooth((age - RiseAt) / 0.15f));
            flip = Mathf.Lerp(sign, slamFlip, turn);
        }

        /// <summary>The blade's direction at the bottom of the slam: along the aim, 0.18 down.</summary>
        public static Vector3 SlamDir(float aim)
        {
            Vector2 d = Dir(aim);
            return Unit(new Vector3(d.x, -0.18f, d.y));
        }

        /// <summary>The hands at the bottom of the slam: 0.35 cells along the aim, 0.05 to the hand side, <see cref="LowH"/> up.</summary>
        public static Vector3 LowHand(Vector2 pos, float aim, float sign = 0f)
        {
            Vector2 q = pos + Dir(aim) * 0.35f + HandSide(aim, sign) * 0.05f;
            return new Vector3(q.x, LowH, q.y);
        }

        /// <summary>
        /// The grown blade's footprint on the floor, as points in the DrawPos frame (the floor is at z + Ground):
        /// from 0.1 past the guard to the tip at <paramref name="scale"/> x the blade. The rules read the same points as map
        /// coordinates (x, z) for who is under the blade: at the default size 2 the strip runs from 0.67 to 2.71 cells along
        /// the aim.
        /// </summary>
        public static void SlamFootprint(Vector2 pos, float aim, float scale, out Vector2 from, out Vector2 to, float sign = 0f)
        {
            Vector3 low = LowHand(pos, aim, sign), B = SlamDir(aim);
            from = new Vector2(low.x + B.x * (GuardA + 0.1f), low.z + B.z * (GuardA + 0.1f));
            float tip = GuardA + Blade * scale;
            to = new Vector2(low.x + B.x * tip, low.z + B.z * tip);
        }

        /// <summary>
        /// The heal <paramref name="age"/> s after a hit: the blood on the steel (1 to 0 in 0.35 s), the red glow
        /// sliding along the flesh into the green eye (its strength and where it is, 0.78 to 0.22 of the blade,
        /// over 0.3 s from 0.05 s), and the eye's glow (0.3 to 0.6 s). All 0 before the hit and after FeedTime.
        /// </summary>
        public static void Feed(float age, out float blood, out float pulse, out float pulseAt, out float eyePulse)
        {
            if (age < 0f || age > FeedTime)
            {
                blood = pulse = pulseAt = eyePulse = 0f;
                return;
            }
            blood = 1f - Smooth(age / 0.35f);
            pulse = Bump(Mathf.Clamp01((age - 0.05f) / 0.3f));
            pulseAt = Mathf.Lerp(0.78f, 0.22f, Smooth((age - 0.05f) / 0.3f));
            eyePulse = Bump(Mathf.Clamp01((age - 0.3f) / 0.3f));
        }

        /// <summary>True when the blade points north on screen more than 0.35 of its length: then the sword and the arm draw under the pawn.</summary>
        public static bool PointsNorth(Vector3 blade)
        {
            float z = blade.z + blade.y * Lift;
            return z > 0.35f * Mathf.Sqrt(blade.x * blade.x + z * z);
        }

        private static Vector3 Lerp3(Vector3 a, Vector3 b, float t) =>
            Unit(new Vector3(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t), Mathf.Lerp(a.z, b.z, t)));

        private static Vector3 Mix3(Vector3 a, Vector3 b, float t) =>
            new Vector3(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t), Mathf.Lerp(a.z, b.z, t));
    }
}
