using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using T = RimArt.EgoMimicryTiming;

namespace RimArt
{
    /// <summary>What the wielder's sword is doing: resting, a swing, or the grown swing.</summary>
    public enum EgoMimicryMove { Rest, Swing, Grown }

    /// <summary>
    /// The wielder as the picture needs it. <see cref="Pos"/> is a ground point on the map: in the previews
    /// where the lab draws its stand-in, in game the pawn's DrawPos (the numbers are the real-size stand-in's,
    /// so no PawnFit).
    /// </summary>
    public struct EgoMimicryWielder
    {
        public Vector2 Pos;
        /// <summary>Degrees, 0 east, 90 north: the current action's aim, or where the pawn faces at rest.</summary>
        public float Aim;
        public EgoMimicryMove Move;
        /// <summary>Seconds since the move's swing started; negative (during a lunge) holds the rest pose.</summary>
        public float MoveAge;
        /// <summary>The arm's stage, 0 (not corroded) to 4, fractional while it grows or recedes.</summary>
        public float Stage;
        /// <summary>The blade's eyes opened wide by the corroded state, 0 to 1.</summary>
        public float Open;
        /// <summary>Seconds since the last hit landed (the heal), or negative for none.</summary>
        public float FeedAge;
        /// <summary>The hand's colour on the grip: the pawn's skin in game, the sketch's in the previews.</summary>
        public Color Skin;
        /// <summary>
        /// Where the body faces: the arm's shoulder and the face over the head are placed for it, and the picture is mirrored
        /// facing west (<see cref="EgoMimicryTiming.SignOf(Rot4)"/>), so hand and shoulder stay on the same side whatever the aim.
        /// </summary>
        public Rot4 Facing;
        /// <summary>
        /// The height the pawn's body is drawn at (its DrawPos.y): Core adds a random offset of up to 0.037 per pawn
        /// (Pawn_DrawTracker.SeededYOffset) so pawns do not flicker over each other, and the pawn's own parts sit up to
        /// 0.037 above it, so the arm, the face and a sword drawn under the pawn are placed from this. The previews pass
        /// <see cref="EgoMimicryGraphics.PawnLayer"/>.
        /// </summary>
        public float Altitude;
    }

    /// <summary>
    /// The Mimicry palette, the small drawing pieces its three picture classes share, and the wielder: the
    /// corroded floor glow, the swing's crescent, the sword in its pose, the heal's chest glow and the arm.
    /// The port of Tools/VfxLab/web/sketches/ego-mimicry.js (drawFrame's wielder half); its numbers are the
    /// sketch's.
    ///
    /// The sword lies level at hand height during a swing, so it turns freely with the aim; the grown swing's
    /// raise is the one 3D motion, a 3D direction drawn with Lift. Pointing north the sword draws under the pawn,
    /// and facing north the arm does. The arm and the face are placed for the body's facing: the shoulder on the
    /// sword side (the viewer's left facing south, right facing north, the upper chest facing east or west), the
    /// face's eye and grin turned to the side the pawn faces in profile.
    ///
    /// Not drawn (the sketch's stand-ins and lab aids): the pawns, the colonist's rifle and its shot (the game
    /// draws the real ones), the lunge's afterimages (stand-in pawns), the green ring under the skipped ally,
    /// the stage pips and the dashed lines to the chosen targets.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class EgoMimicryGraphics
    {
        // The sword is Lobotomy's deep red; the arm is Limbus's pinker muscle.
        internal static readonly Color Flesh = new Color(0.60f, 0.07f, 0.06f), FleshLit = new Color(0.88f, 0.24f, 0.17f), Fibre = new Color(0.30f, 0.02f, 0.03f);
        internal static readonly Color Steel = new Color(0.23f, 0.21f, 0.23f), SteelLit = new Color(0.66f, 0.64f, 0.68f);
        /// <summary>The sketch's Ink: the source's black outlines, and the shadows and cracks.</summary>
        internal static readonly Color Outline = new Color(0.06f, 0.02f, 0.02f);
        internal static readonly Color Bone = new Color(0.86f, 0.80f, 0.66f), BoneLit = new Color(0.97f, 0.94f, 0.85f);
        internal static readonly Color GripC = new Color(0.08f, 0.07f, 0.08f), GripLit = new Color(0.30f, 0.29f, 0.31f), GripRing = new Color(0.78f, 0.12f, 0.10f);
        internal static readonly Color Sclera = new Color(0.97f, 0.94f, 0.91f), IrisGreen = new Color(0.22f, 0.72f, 0.42f), IrisBlue = new Color(0.24f, 0.40f, 0.85f),
            Pupil = new Color(0.03f, 0.02f, 0.02f);
        internal static readonly Color Meat = new Color(0.74f, 0.23f, 0.29f), MeatLit = new Color(0.93f, 0.47f, 0.50f), MeatDark = new Color(0.34f, 0.05f, 0.10f);
        internal static readonly Color Teeth = new Color(0.98f, 0.96f, 0.90f), Mouth = new Color(0.12f, 0.01f, 0.03f), Skin = new Color(0.83f, 0.70f, 0.54f);
        internal static readonly Color SplitCore = new Color(1f, 0.97f, 0.82f), SplitGlow = new Color(1f, 0.60f, 0.20f), RedStreak = new Color(0.86f, 0.11f, 0.08f),
            DarkStreak = new Color(0.28f, 0.02f, 0.02f);
        internal static readonly Color HealRed = new Color(1f, 0.28f, 0.22f), White = new Color(1f, 1f, 1f);
        internal static readonly Color Blood = ChainSickleGraphics.Blood, Dust = ChainSickleGraphics.Dust;

        internal static readonly Material puff = MaterialPool.MatFrom("RimArt/SixPaths/Puff", ShaderDatabase.Transparent);
        internal static readonly float PawnLayer = AltitudeLayer.Pawn.AltitudeFor(), ShadowLayer = AltitudeLayer.Shadows.AltitudeFor();

        private const int MostTube = 16;
        private static readonly float[] widths = new float[MostTube];

        /// <summary>
        /// Everything on the wielder this frame. <paramref name="s"/> drives the flesh's wobble and the mouth's
        /// breathing only; every timed part reads the ages in <paramref name="w"/>.
        /// </summary>
        public static void DrawWielder(in EgoMimicryWielder w, float s, Map map)
        {
            if (!Shown(w.Pos, map)) return;
            Begin(w.Pos);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            Vector2 pos = w.Pos, d = T.Dir(w.Aim);
            float sign = T.SignOf(w.Facing), stage = w.Stage, age = w.MoveAge;
            if (stage > 0f) Sprite(pos, 1.5f, 1.1f, Fade(RedStreak, 0.16f * Mathf.Clamp01(stage)), glow, Floor + 0.01f);

            var sword = new EgoMimicrySword
            {
                Hand = T.HandAt(pos, w.Aim + sign * T.Rest, sign), Blade = T.Level(w.Aim + sign * T.Rest),
                Length = T.Blade, Size = 1f, Flip = sign, Open = w.Open, Fuse = Mathf.Clamp01(stage), Skin = w.Skin,
            };
            if (w.Move == EgoMimicryMove.Swing && age >= 0f && age < T.SwingLength)
            {
                float deg = w.Aim + sign * T.SwingAngle(age);
                sword.Hand = T.HandAt(pos, deg, sign);
                sword.Blade = T.Level(deg);
                sword.Flip = T.SwingFlip(age, sign);
                EgoMimicryStrikeGraphics.Crescent(new Vector2(pos.x, pos.y + T.HandScreen), w.Aim, sign, age, T.HandOut + T.GuardA + T.Blade + 0.05f);
            }
            else if (w.Move == EgoMimicryMove.Grown && age >= 0f && age < T.GrownLength)
            {
                T.GrownPose(age, pos, w.Aim, out sword.Hand, out sword.Blade, out sword.Size, out sword.Flip, out sword.Swell, sign);
                sword.Open = sword.Swell;
            }
            T.Feed(w.FeedAge, out sword.Blood, out sword.Pulse, out sword.PulseAt, out sword.EyePulse);
            sword.Altitude = T.PointsNorth(sword.Blade) ? w.Altitude - 0.03f : Overhead + 0.05f;
            Vector2 hand = EgoMimicrySwordGraphics.Draw(sword, s, sun, strength);
            // The heal reaches the wielder: a faint red glow on the chest while the green eye glows.
            if (sword.EyePulse > 0f) Sprite(new Vector2(pos.x, pos.y + PawnBody.Chest), 0.7f, 0.8f, Fade(HealRed, 0.3f * sword.EyePulse), glow, Overhead + 0.07f);
            EgoMimicryArmGraphics.Draw(pos, hand, d, w.Facing, w.Altitude, stage, s, sun, strength);
        }

        internal static void Disc(Vector2 at, float altitude, float width, float depth, float angle, Color colour) =>
            DrawMesh(disc, at, altitude, width, depth, angle, colour, solid);

        internal static Vector2 Screen(Vector3 q) => new Vector2(q.x, q.z + PawnBody.Ground + q.y * T.Lift);

        /// <summary>Where a point with height casts its shadow on the floor.</summary>
        internal static Vector2 Shadow(Vector3 q, Vector2 sun) => new Vector2(q.x + sun.x * q.y, q.z + PawnBody.Ground + sun.y * q.y);

        /// <summary><see cref="VfxDraw.Tube"/> with the half-widths in <see cref="Widths"/>.</summary>
        internal static void Tube(Vector2[] pts, int count, Color colour, float altitude, float lo = -1f, float hi = 1f) =>
            VfxDraw.Tube(pts, widths, count, colour, altitude, lo, hi);

        /// <summary>The half-widths <see cref="Tube"/> reads, one per point; fill them before each tube.</summary>
        internal static float[] Widths => widths;
    }
}
