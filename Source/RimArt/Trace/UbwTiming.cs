using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// When each part of Unlimited Blade Works' home-map side happens: the chant, the release, the fire
    /// along the chant's lines, the ring closing, the white that takes everyone, the ring burning while
    /// they are away, the fire running back in, the white that brings them back. Seconds in, numbers out,
    /// no drawing and no map. The port of Tools/VfxLab/web/sketches/trace-ubw-cast.js; the constants are
    /// that sketch's defaults (released after verse 1, the world standing 3 s). The rules (who is taken, the
    /// radius by verse, the cooldown) are proposed and not agreed; XML fields once there is an ability.
    /// </summary>
    internal static class UbwCastTiming
    {
        /// <summary>The world's radius released after verse 1, 2 or 3; a verse takes 2 s and its lines run out in 1.8 s.</summary>
        public static readonly float[] Radius = { 6f, 9f, 12f };
        public const float VerseTime = 2f, LinesOut = 1.8f;
        public const int Lines = 10;
        public const float RingClose = 0.25f, FlashUp = 0.12f, FlashHold = 0.1f, FlashDown = 0.45f, Flare = 0.25f, After = 1.4f, FlameEvery = 0.22f;
        /// <summary>The sketch's sliders: the fire runs out in 1 s and back in 0.6 s; the world stands 3 s in the preview (20 to 30 s in game); flame height and the low ring's share of it; two branches off each line.</summary>
        public const float Run = 1f, Hold = 3f, Back = 0.6f, Flame = 0.7f, Low = 0.35f;
        public const int Verse = 1, Branches = 2;
        public const float TakenShake = 0.04f, HomeShake = 0.02f;
        /// <summary>The branches off each line: share of the line where it forks, turn off the line (radians), length as a share of the radius.</summary>
        public static readonly float[] BranchAt = { 0.38f, 0.64f }, BranchTurn = { 0.6f, 0.5f }, BranchLength = { 0.3f, 0.24f };

        public struct Plan
        {
            public int V;
            public float R, Open, Lit, Closed, Taken, Clear, Ends, Inward, Back, Home, End;
        }

        public static Plan For(int verse, float run = Run, float hold = Hold, float back = Back)
        {
            var t = new Plan { V = verse, R = Radius[verse - 1] };
            t.Open = verse * VerseTime;
            t.Lit = t.Open + run;
            t.Closed = t.Lit + RingClose;
            t.Taken = t.Closed + FlashUp;
            t.Clear = t.Taken + FlashHold + FlashDown;
            t.Ends = t.Clear + hold;
            t.Inward = t.Ends + Flare;
            t.Back = t.Inward + back;
            t.Home = t.Back + FlashUp;
            t.End = t.Home + FlashHold + FlashDown + After;
            return t;
        }

        public static float Duration => For(Verse).End;

        /// <summary>The white of a flash that starts at <paramref name="at"/>: up, held, gone.</summary>
        public static float FlashAlpha(float s, float at)
        {
            float u = s - at;
            if (u < 0f) return 0f;
            if (u < FlashUp) return Smooth(u / FlashUp);
            if (u < FlashUp + FlashHold) return 1f;
            return 1f - Smooth((u - FlashUp - FlashHold) / FlashDown);
        }

        /// <summary>How far the chant's lines have run at s: verse k carries them from the last verse's radius to its own.</summary>
        public static float ChantRadius(float s, int verses)
        {
            float r = 0f;
            for (int k = 1; k <= verses; k++)
            {
                float from = k > 1 ? Radius[k - 2] : 0f, start = (k - 1) * VerseTime;
                if (s >= start) r = from + (Radius[k - 1] - from) * Mathf.Min(1f, (s - start) / LinesOut);
            }
            return r;
        }

        public static float Smooth(float t) => SixPathsSlamTiming.Smooth(t);
    }

    /// <summary>
    /// When each part of the world's own timeline happens: the white everyone arrives in, the wall of fire
    /// running out from the caster past the map edge, the world standing, the white closing in behind a
    /// wall of fire. Seconds in, numbers out, no drawing and no map. The port of
    /// Tools/VfxLab/web/sketches/trace-ubw-world.js with its defaults (the world standing 4 s in the
    /// preview; the real world stands 20 to 30 s, until Close). The landed pawns' places are the preview's
    /// script: they only decide where no sword stands.
    /// </summary>
    internal static class UbwWorldTiming
    {
        public const float MapHalf = (float)UbwField.MapHalf;
        /// <summary>Inside the world the sun is low: shadows run twice as long as the scene's.</summary>
        public const float DuskShadow = 2f;
        /// <summary>The fire starts at Start and runs Past cells beyond the drawn field so the whole view is done; its radius goes as time^SweepPow; the white closes as (1 - time)^ClosePow.</summary>
        public const float Start = 0.15f, Past = 14f, SweepPow = 1.8f, ClosePow = 1.6f;
        /// <summary>Swords within Near cells get the trace (TraceFor s, the scan line in the first ScanFor s) and break into light BreakFor s before the white takes them.</summary>
        public const float Near = 14f, TraceFor = 0.35f, ScanFor = 0.25f, BreakFor = 0.2f, WallHeight = 1.2f;
        /// <summary>The sketch's sliders: the fire runs out in 1.5 s, the world stands 4 s, the white closes in 1.2 s.</summary>
        public const float Sweep = 1.5f, Hold = 4f, Close = 1.2f;
        public const float StartShake = 0.02f;
        /// <summary>The preview's script: where the other three landed, in cells from the caster (the cast sketch's pawns at verse 1).</summary>
        public static readonly Vector2[] Landed = { new Vector2(-2.2f, -1.6f), new Vector2(2.8f, 1.2f), new Vector2(-1.5f, 3.2f) };

        public static float Reach(double beyond) => (float)(MapHalf + beyond + Past);
        public static float Swept => Start + Sweep;
        /// <summary>The preview closes after the hold; the real world closes when asked.</summary>
        public static float CloseAt => Swept + Hold;
        public static float Shut => CloseAt + Close;
        public static float Duration => Shut + 0.35f;

        /// <summary>The fire's radius going out at s; the reach and past once the sweep is done.</summary>
        public static float OutAt(float s, float reach) => s < Start ? 0f : reach * Mathf.Pow(Mathf.Clamp01((s - Start) / Sweep), SweepPow);
        /// <summary>The white's radius coming in, <paramref name="closeAt"/> being when the close began.</summary>
        public static float InAt(float s, float closeAt, float reach) => reach * Mathf.Pow(1f - Mathf.Clamp01((s - closeAt) / Close), ClosePow);
        /// <summary>When the fire going out passes d cells from the caster.</summary>
        public static float Passes(float d, float reach) => Start + Sweep * Mathf.Pow(d / reach, 1f / SweepPow);
        /// <summary>When the white coming in reaches d cells from the caster.</summary>
        public static float Covers(float d, float closeAt, float reach) => closeAt + Close * (1f - Mathf.Pow(d / reach, 1f / ClosePow));
    }
}
