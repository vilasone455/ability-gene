using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// When each part of Unlimited Void's inside happens: seconds in, numbers out, no drawing and no
    /// map. The port of Tools/VfxLab/web/sketches/gojo-unlimited-void-inside.js; the constants are that
    /// sketch's defaults ("whole domain", Cursed Clash colours, camera push on). Seconds count from the
    /// white arrival, when everyone lands in the pocket map.
    /// </summary>
    internal static class UnlimitedVoidInsideTiming
    {
        /// <summary>How long the domain lasts (a rule and a placeholder; an XML field once there is an ability).</summary>
        public const float Hold = 10f;
        /// <summary>The radius the domain took everyone from; the arrival's burst ring shows it.</summary>
        public const float Radius = 9f;

        public const float Collapse = 0.7f, Arrive = 0.8f, Tail = 0.15f;
        public const float BurstRing = 0.55f, WhiteFade = 0.25f, SplatterReach = 4f;
        // The speed lines: 360 streaks, each reaching back 0.75 of its distance, 2 s from the point to
        // the edge, running for 1.6 s; the space flies in from 16 x nearer; the camera pushes in 1.25 x.
        public const int Streaks = 360;
        public const float LinesFor = 1.6f, LineTrip = 2f, Length = 0.75f, FlyIn = 16f, Push = 1.25f;
        public const float LinesFrom = 0.08f, SpeedLinesAt = 0.1f;
        /// <summary>The black hole: disc radius 3.2, 7.5 cells north of where Gojo lands, the vanishing point of the lines.</summary>
        public const float HoleRadius = 3.2f, HoleNorth = 7.5f;

        public static float LightAt => LinesFor - 0.6f;
        public static float OpensAt => LinesFor - 0.25f;
        /// <summary>The fly-in has settled and the camera starts back.</summary>
        public static float LandAt => LinesFor + 0.45f;
        public static float WhiteBackAt => Hold + Collapse * 0.6f;
        public static float Duration => Hold + Collapse + Tail;

        /// <summary>The vanishing point, where the black hole opens.</summary>
        public static Vector2 PointFor(Vector2 o) => new Vector2(o.x, o.y + HoleNorth);

        /// <summary>The space fading in as the white clears.</summary>
        public static float FadeIn(float s) => Smooth((s - 0.1f) / (Arrive * 0.9f));
        /// <summary>The speed lines and their dust, 0..1.</summary>
        public static float Lines(float s) => Mathf.Clamp01((s - LinesFrom) / 0.15f) * (1f - Smooth((s - LinesFor) / 0.45f));
        /// <summary>The black hole opening under the white light, 0..1.</summary>
        public static float Open(float s) => Smooth((s - OpensAt) / 0.7f);
        /// <summary>The white light at the vanishing point, 0..1.</summary>
        public static float Light(float s) => Smooth((s - LightAt) / 0.45f) * (1f - Smooth((s - (LinesFor - 0.15f)) / 0.45f));
        /// <summary>The collapse at the end, 0..1.</summary>
        public static float Ending(float s) => Mathf.Clamp01((s - Hold) / Collapse);
        /// <summary>The streaks stop this far from the point; the gap widens with the opening black hole, so they pour out of its rim.</summary>
        public static float Hollow(float s) => Mathf.Max(1.2f, HoleRadius * Open(s) * 1.25f);

        /// <summary>
        /// The fly-in: the space's scale about the vanishing point at <paramref name="s"/>, starting
        /// <see cref="FlyIn"/> times nearer (and that much smaller), fast at first and landed (1) as the
        /// lines end, as a camera flying toward the point and stopping would see it.
        /// </summary>
        public static float Flight(float s)
        {
            float u = Mathf.Clamp01((s - LinesFrom) / (LandAt - LinesFrom));
            return Mathf.Exp(-Mathf.Log(Mathf.Max(1f, FlyIn)) * (1f - u) * (1f - u));
        }

        /// <summary>The sketch's camera events: push in on the vanishing point while the lines run, then back.</summary>
        public static readonly CameraEvent[] CameraEvents =
        {
            new CameraEvent(SpeedLinesAt, LinesFor - SpeedLinesAt, Push, 1f, 0f, HoleNorth),
            new CameraEvent(LinesFor + 0.45f, 1f, 1f, 0f),
        };

        private static float Smooth(float t) => SixPathsSlamTiming.Smooth(t);
    }
}
