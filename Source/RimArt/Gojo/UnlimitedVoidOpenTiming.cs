using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// When each part of Unlimited Void's opening on the home map happens: seconds in, numbers out, no
    /// drawing and no map. The port of Tools/VfxLab/web/sketches/gojo-unlimited-void-open.js up to the
    /// ball breaking; the constants are that sketch's defaults. Who comes back how after the break is
    /// the ability's, not the drawing's, so the preview ends when the break's flash has gone.
    /// </summary>
    internal static class UnlimitedVoidOpenTiming
    {
        /// <summary>The radius the barrier closes over (a rule and a placeholder; an XML field once there is an ability).</summary>
        public const float Radius = 9f;

        // 0.3 s before the hand sign; the sign takes 0.6 s; the barrier closes in 0.3 s and shrinks to the
        // ball in 0.5 s; the ball hangs 2 s in the preview (the real domain is 10 s); the break's flash lasts 1.2 s.
        public const float Lead = 0.3f, Warm = 0.6f, Close = 0.3f, Shrink = 0.5f, HangFor = 2f, BurstFor = 1.2f;
        /// <summary>The ball: 0.5 cells across, hanging 1.2 cells over Gojo's cell (manga ch. 227-228, a basketball).</summary>
        public const float BallSize = 0.5f, BallHeight = 1.2f;
        public const float RingFade = 1.2f;
        /// <summary>Streaks of light running into the raised hand during the sign.</summary>
        public const int Gather = 8;
        public const float OpenShake = 0.04f, BurstShake = 0.02f, BurstShakeDelay = 0.08f;

        public static float CastAt => Lead;
        public static float OpenAt => CastAt + Warm;
        public static float FullAt => OpenAt + Close;
        public static float HangAt => FullAt + Shrink;
        public static float BurstAt => HangAt + HangFor;
        public static float Duration => BurstAt + BurstFor;

        /// <summary>The raised hand of the sign, in front of the face of a pawn facing south at <paramref name="o"/>.</summary>
        public static Vector2 HandAt(Vector2 o) => new Vector2(o.x + 0.07f, o.y + 0.6f);
    }
}
