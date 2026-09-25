using UnityEngine;

namespace RimArt
{
    /// <summary>The three scenes of the Shadow grasp preview.</summary>
    public enum GraspScene { Grenade, Blocked, Rescue }

    /// <summary>What a Shadow grasp looks like now. Points are ground points on the map.</summary>
    public struct GraspShot
    {
        /// <summary>The carrier, where the thing was picked up, and the chosen cell.</summary>
        public Vector2 Carrier, Item, Dest;
        /// <summary>Seconds since the cast began; the cast; when the slide starts and when it ends.</summary>
        public float Seconds, Cast, SlideStart, Arrive;
        /// <summary>How far toward the chosen cell the thing gets: 1, or less where a pawn stands on the path.</summary>
        public float Share;
        /// <summary>Direction from carrier to thing, and the slide's direction from that, in degrees.</summary>
        public float Aim, Turn;
        /// <summary>1 for an item; bigger for a body.</summary>
        public float HandSize;
        /// <summary>The range ring's radius now; 0 draws none.</summary>
        public float Range;
        public float Width, Sway;

        /// <summary>The share of Item to Dest the thing has slid by <paramref name="t"/>.</summary>
        public float SlidAt(float t) => Share * VfxMath.Smooth((t - SlideStart) / (Arrive - SlideStart));
    }

    /// <summary>
    /// Timing of Shadow grasp: seconds in, numbers out, no drawing and no map. The port of
    /// Tools/VfxLab/web/sketches/shadow-plexus-grasp.js; the constants are that sketch's defaults.
    /// There is no ability behind it yet. The rule (user's draft, placeholders): a loose item, a
    /// weapon, a live grenade or a downed body, and a second cell, both within 24.9 cells times the
    /// light level; the thing slides there at 12 cells a second, stopped by a pawn on the path.
    /// </summary>
    public static class ShadowGraspTiming
    {
        public const float FullRange = 24.9f, Open = 0.1f, Curl = 0.12f, LetGo = 0.12f, Back = 0.4f, Fuse = 0.35f, Tail = 1.1f;
        public const float BlockedAt = 0.6f, TurnOver = 0.9f;
        /// <summary>A body goes sideways out of the line of fire, at half the item speed, in a hand 1.9 times the size.</summary>
        public const float RescueTurn = 110f, BodySpeed = 0.5f, BodyHand = 1.9f, ScuffEvery = 0.1f;

        // The preview's script: the sketch's sliders at their defaults.
        public const float ScriptDistance = 5f, ScriptSlide = 7f, ScriptTurn = 35f, ScriptCast = 0.5f, ScriptSpeed = 12f, Width = 0.14f, Sway = 0.1f;

        public static float SlideStart(float cast) => cast + Open + Curl;

        /// <summary>
        /// The preview's shot at <paramref name="seconds"/>, laid out along the aim from the thing's cell,
        /// in local points (along the aim, to its left). The grenade scenes keep the sketch's length,
        /// which runs on past the let-go to the grenade going off, although the grenade is not drawn.
        /// </summary>
        public static GraspShot Script(GraspScene scene, float seconds, out float duration)
        {
            bool rescue = scene == GraspScene.Rescue;
            float turn = rescue ? RescueTurn : ScriptTurn, share = scene == GraspScene.Blocked ? BlockedAt : 1f;
            float slideStart = SlideStart(ScriptCast), arrive = slideStart + ScriptSlide * share / (ScriptSpeed * (rescue ? BodySpeed : 1f));
            duration = rescue ? arrive + LetGo + Back + Tail : arrive + Fuse + Tail;
            return new GraspShot
            {
                Carrier = new Vector2(-ScriptDistance, 0f), Item = Vector2.zero,
                Dest = new Vector2(Mathf.Cos(turn * Mathf.Deg2Rad), Mathf.Sin(turn * Mathf.Deg2Rad)) * ScriptSlide,
                Seconds = seconds, Cast = ScriptCast, SlideStart = slideStart, Arrive = arrive, Share = share, Aim = 0f, Turn = turn,
                HandSize = rescue ? BodyHand : 1f, Width = Width, Sway = Sway,
            };
        }

        public static float Duration(GraspScene scene)
        {
            Script(scene, 0f, out float duration);
            return duration;
        }
    }
}
