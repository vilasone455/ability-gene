using UnityEngine;
using P = RimArt.ShadowPlexusTiming;

namespace RimArt
{
    /// <summary>What a Shadow neck bind looks like now. Points are ground points on the map.</summary>
    public struct NeckBindShot
    {
        public Vector2 Carrier, Target;
        /// <summary>The direction of the Imitation line from carrier to target, in degrees.</summary>
        public float Aim;
        /// <summary>Seconds since the hands left the carrier; how long they take to climb the body; when they let go.</summary>
        public float Seconds, Climb, Release;
        /// <summary>Choke seconds from closed hands to unconscious.</summary>
        public float Choke;
        /// <summary>The line was cut, at <see cref="CutAt"/> of carrier to target; otherwise the target passed out.</summary>
        public bool Cut;
        public float CutAt;
        public float Width, Sway;

        public float Closed => ShadowNeckBindTiming.Crawl + Climb;

        /// <summary>The suffocation now, 0 to 1: it rises while the hands are closed and drains once a cut line lets go.</summary>
        public float Severity
        {
            get
            {
                float since = Seconds - Release;
                if (since < 0f) return Mathf.Clamp01((Seconds - Closed) / Choke);
                return Cut ? Mathf.Max(0f, Mathf.Clamp01((Release - Closed) / Choke) - ShadowNeckBindTiming.Drain * since) : 1f;
            }
        }
    }

    /// <summary>
    /// Timing of Shadow neck bind: seconds in, numbers out, no drawing and no map. The port of
    /// Tools/VfxLab/web/sketches/shadow-plexus-neck-bind.js; the constants are that sketch's defaults.
    /// There is no ability behind it yet. The proposal (none of it agreed): cast on a pawn already
    /// held by Imitation or sewn by Seam; two hands crawl along the line, climb the body and close on
    /// the neck; suffocation 12.5 % a second, unconscious at 100 %; a broken line lets go and it
    /// drains at 5 % a second.
    /// </summary>
    public static class ShadowNeckBindTiming
    {
        public const float Drain = 0.05f, CutShare = 0.5f, Crawl = 0.6f, LineBack = 0.5f, Tail = 1.2f;
        public const float HandSize = 0.36f, Beside = 0.11f, NeckHeight = 0.41f, NeckApart = 0.15f, TurnIn = 40f, ArmWidth = 0.06f, PoolRadius = 0.42f;

        // The preview's script: the sketch's sliders at their defaults.
        public const float ScriptDistance = 6f, ScriptClimb = 1.5f, ScriptChoke = 8f, Width = 0.17f, Sway = 0.1f;

        public static float Release(bool cut) => Crawl + ScriptClimb + ScriptChoke * (cut ? CutShare : 1f);
        public static float Duration(bool cut) => Release(cut) + (cut ? P.SnapTime + 1.6f : LineBack + Tail);
    }
}
