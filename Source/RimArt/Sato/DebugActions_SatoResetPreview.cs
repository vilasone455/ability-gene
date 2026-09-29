using RimWorld;
using UnityEngine;
using Verse;
using H = RimArt.HeadshotTiming;

namespace RimArt
{
    /// <summary>
    /// Previews only: nobody is hurt, nothing is spawned and no pawn is drawn. Each entry plays one scene of the Reset
    /// sketches (ajin-reset.js, ajin-headshot-reset.js) round the chosen cell, the cell the sketch centres on, at the
    /// sketches' default settings, so the recorder can compare the port with the sketch. The real Reset draws through
    /// the kit code (Sato/Kit).
    /// </summary>
    public static class DebugActions_SatoResetPreview
    {
        [RimArtDebug("Sato", "reset: in place")]
        public static void InPlace() => Play(SatoResetPreview.InPlace);

        [RimArtDebug("Sato", "reset: at anchor")]
        public static void AtAnchor() => Play(SatoResetPreview.AtAnchor);

        [RimArtDebug("Sato", "reset: pieces")]
        public static void Pieces() => Play(SatoResetPreview.Pieces);

        [RimArtDebug("Sato", "headshot: wounded, south")]
        public static void HeadshotSouth() => Play(SatoResetPreview.HeadshotSouth);

        [RimArtDebug("Sato", "headshot: wounded, north")]
        public static void HeadshotNorth() => Play(SatoResetPreview.HeadshotNorth);

        [RimArtDebug("Sato", "headshot: wounded, east")]
        public static void HeadshotEast() => Play(SatoResetPreview.HeadshotEast);

        [RimArtDebug("Sato", "headshot: wounded, west")]
        public static void HeadshotWest() => Play(SatoResetPreview.HeadshotWest);

        [RimArtDebug("Sato", "headshot: after sever, south")]
        public static void HeadshotSevered() => Play(SatoResetPreview.HeadshotSevered);

        [RimArtDebug("Sato", "headshot: lying at body angle 100, south")]
        public static void HeadshotTurned() => Play(SatoResetPreview.HeadshotTurned);

        [RimArtDebug("Sato", "reset: clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_SatoResetPreview>();
            if (preview != null) preview.active = false;
        }

        private static void Play(SatoResetPreview play) =>
            Find.CurrentMap.GetComponent<MapComponent_SatoResetPreview>().Play(UI.MouseCell(), play);
    }

    public enum SatoResetPreview { InPlace, AtAnchor, Pieces, HeadshotSouth, HeadshotNorth, HeadshotEast, HeadshotWest, HeadshotSevered, HeadshotTurned }

    /// <summary>
    /// The sketches' scenes on their own clock, with the sketches' compressed delays (the game's are 20 s and 6 s).
    /// The preview's script: while he lies it passes the lab's lying stand-in's centre, 0.17 cells south of the cell
    /// centre (the real pawn lies centred on its draw position), and body angle 270 (the lab's downed layout, head
    /// west); from the delay's end he stands on the cell centre. The "body angle 100" scene has no sketch: it lies
    /// centred on the cell at 100 degrees to show the lying overlays turning with the body.
    /// </summary>
    public sealed class MapComponent_SatoResetPreview : MapComponent
    {
        // ajin-reset.js: 0.35 s lying before the Reset begins, Delay 1.2 + Rebuild 1.1 (the ring fills over both), Rise
        // 0.9, 0.9 s after. At anchor: the hand 1 cell east of the cell centre, the finger at (1.9, 1.35).
        public const float ResetHold = 0.35f, ResetDelay = 1.2f + AjinResetTiming.Rebuild, ResetTail = 0.9f;
        public const float ResetDuration = ResetHold + ResetDelay + AjinResetTiming.Rise + ResetTail;
        // ajin-headshot-reset.js: 0.5 s wounded before the cast; the sketch's fall 0.1 s after the shot, lying 0.35 s
        // later; play dead 1.2 + Rebuild 0.8; Rise 0.7; 1.1 s after.
        public const float HeadshotHold = 0.5f, HeadshotShot = HeadshotHold + H.ShotAt, HeadshotLie = HeadshotShot + 0.1f + H.Fall;
        public const float HeadshotDelay = 1.2f + H.Rebuild, HeadshotStand = HeadshotLie + HeadshotDelay;
        public const float HeadshotDuration = HeadshotStand + H.Rise + 1.1f;
        // The pieces scene: all five lie for PiecesCrumble s, then crumble one after another 0.3 s apart.
        public const float PiecesCrumble = 1f, PiecesGap = 0.3f, PiecesDuration = PiecesCrumble + 4 * PiecesGap + AjinResetTiming.Crumble + 0.3f;
        public const float LabLyingDrop = 0.17f, DownedAngle = 270f;
        /// <summary>The turned scene: a body angle PawnRenderer can give a downed pawn (45-135 or 225-315), head east and a little south.</summary>
        public const float TurnedAngle = 100f;

        private static readonly Vector2 ResetHand = new Vector2(1.0f, 0f), ResetFinger = new Vector2(1.9f, 1.35f);
        private const float HandAngle = 20f, FingerAngle = -30f;
        // "after Sever": the anchor ring 1.5 cells east and 1.1 south; the arm lies from (-0.17, 0.05) to (0.15, -0.04) of it.
        private static readonly Vector2 SeverAnchor = new Vector2(1.5f, -1.1f), SeverArm = new Vector2(-0.01f, 0.005f);
        private static readonly float SeverArmAngle = Mathf.Atan2(-0.09f, 0.32f) * Mathf.Rad2Deg;
        private static readonly AjinPiece[] PieceRow = { AjinPiece.Leg, AjinPiece.Arm, AjinPiece.Hand, AjinPiece.Finger, AjinPiece.Ear };
        private static readonly float[] PieceAngles = { 10f, -15.7f, 20f, -30f, 60f };

        public bool active;
        private SatoResetPreview mode;
        private float seconds;
        private IntVec3 cell;
        private bool shaken;

        public MapComponent_SatoResetPreview(Map map) : base(map) { }

        public static float Duration(SatoResetPreview play) =>
            play == SatoResetPreview.InPlace || play == SatoResetPreview.AtAnchor ? ResetDuration
            : play == SatoResetPreview.Pieces ? PiecesDuration : HeadshotDuration;

        public void Play(IntVec3 at, SatoResetPreview play)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            mode = play;
            seconds = 0f;
            shaken = false;
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            Vector3 o = cell.ToVector3Shifted();
            switch (mode)
            {
                case SatoResetPreview.InPlace: DrawInPlace(o); break;
                case SatoResetPreview.AtAnchor: DrawAtAnchor(o); break;
                case SatoResetPreview.Pieces: DrawPieces(o); break;
                default: DrawHeadshot(o); break;
            }
            if (seconds >= Duration(mode)) active = false;
        }

        private static Vector3 At(Vector3 o, Vector2 offset) => new Vector3(o.x + offset.x, o.y, o.z + offset.y);
        private static Vector3 Lying(Vector3 o) => new Vector3(o.x, o.y, o.z - LabLyingDrop);

        private void DrawInPlace(Vector3 o)
        {
            float t = seconds - ResetHold;
            bool lying = t < ResetDelay;
            AjinResetGraphics.DrawInPlace(new ResetInPlaceShot
            {
                Body = lying ? Lying(o) : o, BodyAngle = DownedAngle, Lying = lying, Seconds = t, Delay = ResetDelay,
                Rebuild = AjinResetTiming.Rebuild, Rise = AjinResetTiming.Rise, Timer = true, Wounds = 3, Clock = seconds,
            });
        }

        private void DrawAtAnchor(Vector3 o)
        {
            float t = seconds - ResetHold;
            Vector3 hand = At(o, ResetHand), finger = At(o, ResetFinger);
            // Before the Reset begins the game draws the lying hand; from then on DrawAtAnchor does.
            if (t < 0f) AjinResetGraphics.DrawPiece(hand, AjinPiece.Hand, HandAngle);
            Vector2 sun = GenCelestial.GetLightSourceInfo(map, GenCelestial.LightType.Shadow).vector * SixPathsSlamGraphics.SunScale;
            AjinResetGraphics.DrawAtAnchor(new ResetAnchorShot
            {
                Anchor = hand, Piece = AjinPiece.Hand, PieceAngle = HandAngle, Seconds = t, Delay = ResetDelay,
                Rebuild = AjinResetTiming.Rebuild, Rise = AjinResetTiming.Rise, Timer = true,
                Sun = sun, Shadow = 0.32f * GenCelestial.CurShadowStrength(map), Clock = seconds,
            });
            // His other anchor, a finger, crumbles when he stands.
            if (t < ResetDelay) AjinResetGraphics.DrawPiece(finger, AjinPiece.Finger, FingerAngle);
            else AjinResetGraphics.DrawCrumble(finger, AjinPiece.Finger, FingerAngle, t - ResetDelay);
        }

        private void DrawPieces(Vector3 o)
        {
            for (int i = 0; i < PieceRow.Length; i++)
            {
                Vector3 at = At(o, new Vector2(i - 2f, 0f));
                float crumble = seconds - PiecesCrumble - i * PiecesGap;
                AjinResetGraphics.DrawAnchorRing(at, 1f - Mathf.Clamp01(crumble / AjinResetTiming.Crumble));
                if (crumble < 0f) AjinResetGraphics.DrawPiece(at, PieceRow[i], PieceAngles[i]);
                else AjinResetGraphics.DrawCrumble(at, PieceRow[i], PieceAngles[i], crumble);
            }
        }

        private void DrawHeadshot(Vector3 o)
        {
            Rot4 facing = mode == SatoResetPreview.HeadshotNorth ? Rot4.North : mode == SatoResetPreview.HeadshotEast ? Rot4.East
                : mode == SatoResetPreview.HeadshotWest ? Rot4.West : Rot4.South;
            bool severed = mode == SatoResetPreview.HeadshotSevered, turned = mode == SatoResetPreview.HeadshotTurned;
            float t = seconds, angle = turned ? TurnedAngle : DownedAngle;
            // The turned scene has no sketch to match, so the body lies centred on the cell as a real pawn does.
            Vector3 lying = turned ? o : Lying(o);
            if (severed)
            {
                Vector3 anchor = At(o, SeverAnchor);
                AjinResetGraphics.DrawAnchorRing(anchor, 1f);
                AjinResetGraphics.DrawPiece(At(anchor, SeverArm), AjinPiece.Arm, SeverArmAngle);
            }
            HeadshotGraphics.DrawCast(new HeadshotCastShot { Pawn = o, Facing = facing, Seconds = t - HeadshotHold });
            if (!shaken && t >= HeadshotShot)
            {
                shaken = true;
                Find.CameraDriver.shaker.DoShake(H.Shake);
            }
            if (t >= HeadshotShot && t < HeadshotStand) HeadshotGraphics.DrawLyingPistol(lying, angle, t - HeadshotShot, facing, o);
            // The sketch lies from its fall's end; in game the Reset begins at the shot.
            bool down = t < HeadshotStand;
            AjinResetGraphics.DrawInPlace(new ResetInPlaceShot
            {
                Body = down ? lying : o, BodyAngle = angle, Lying = down, Seconds = t - HeadshotLie, Delay = HeadshotDelay,
                Rebuild = H.Rebuild, Rise = H.Rise, HeadFirst = true, Timer = true, Wounds = severed ? 2 : 3, Clock = t,
            });
            HeadshotGraphics.DrawRisePistol(o, facing, t - HeadshotStand, angle, lying);
        }
    }
}
