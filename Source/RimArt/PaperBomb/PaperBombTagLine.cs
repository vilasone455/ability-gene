using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// What a laid tag line looks like now. The strip is measured in cells along the cast direction
    /// from the caster's feet. <see cref="FuseSeconds"/> is below zero until the fuse is lit;
    /// <see cref="FuseFrom"/> is where it was lit, and it burns both ways from there.
    /// </summary>
    public struct TagLineShot
    {
        public int Tags;
        /// <summary>Seconds since the cast began. The strip has run out at <see cref="PaperBombTagLineTiming.Lay"/>.</summary>
        public float Seconds;
        public float FuseSeconds, FuseFrom;
        public float PerTag, Radius;
        /// <summary>A hand seal lit it: the caster's flash is drawn for the 0.35 s before the fuse starts.</summary>
        public bool Sealed;
    }

    /// <summary>
    /// Timing and geometry of Tag Line: seconds in, numbers out, no drawing and no map. The port of
    /// Tools/VfxLab/web/sketches/paper-bomb-tag-line.js; the constants are that sketch's defaults.
    /// </summary>
    public static class PaperBombTagLineTiming
    {
        /// <summary>Wind-up before the strip leaves the hand, and when it has all run out: the RimArt_TagFlick clip's 0.30 and 2.50.</summary>
        public const float Flick = 0.3f, Lay = 2.5f;
        /// <summary>A point on the strip lies flat this long after the leading edge passed it.</summary>
        public const float Settle = 0.45f;
        public const float Wave = 0.22f, Rise = 0.3f, HandHeight = 0.38f, Width = 0.34f;
        /// <summary>Plain strip between the caster and tag 0, so the first burst does not reach the caster.</summary>
        public const float Leader = 2.5f, HandOut = 0.35f, TagHalf = 0.42f;
        public const float Aftermath = 2.2f, BurstShake = 0.1f;

        // The preview's script: 8 tags, lit by hand 1.8 s after the strip is down.
        public const int ScriptTags = 8;
        public const float ScriptWait = 1.8f, ScriptPerTag = 0.1f, ScriptRadius = 1.1f;

        public static float Start => HandOut;
        public static float TagAt(int index) => Leader + index;
        public static float End(int tags) => TagAt(tags - 1) + TagHalf;

        /// <summary>When the fuse, lit at <paramref name="from"/>, reaches tag <paramref name="index"/> and that tag bursts, in fuse seconds.</summary>
        public static float ReachAt(int index, float from, float perTag) => Mathf.Abs(TagAt(index) - from) * perTag;
        public static float BurstAt(int index, float from, float perTag) => ReachAt(index, from, perTag) + PaperBombGraphics.Burn;

        /// <summary>Fuse seconds at which the last tag has burst.</summary>
        public static float LastBurst(int tags, float from, float perTag) =>
            Mathf.Max(BurstAt(0, from, perTag), BurstAt(tags - 1, from, perTag));

        /// <summary>The strip's leading edge. It slows as it runs out.</summary>
        public static float Front(float seconds, int tags)
        {
            float x = Mathf.Clamp01((seconds - Flick) / (Lay - Flick));
            return Mathf.Lerp(Start, End(tags), 1f - (1f - x) * (1f - x));
        }

        /// <summary>The inverse of <see cref="Front"/>: when the edge reached <paramref name="along"/>.</summary>
        public static float Passed(float along, int tags) =>
            Flick + (Lay - Flick) * (1f - Mathf.Sqrt(1f - Mathf.Clamp01((along - Start) / (End(tags) - Start))));

        public static TagLineShot Script(float seconds)
        {
            float fuse = Lay + ScriptWait;
            return new TagLineShot
            {
                Tags = ScriptTags, Seconds = seconds, FuseSeconds = seconds - fuse, FuseFrom = Start,
                PerTag = ScriptPerTag, Radius = ScriptRadius, Sealed = true,
            };
        }

        public static float ScriptFuseAt => Lay + ScriptWait;
        public static float ScriptDuration => ScriptFuseAt + LastBurst(ScriptTags, Start, ScriptPerTag) + Aftermath;
    }
}
