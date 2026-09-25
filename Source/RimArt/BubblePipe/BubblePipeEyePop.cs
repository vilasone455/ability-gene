namespace RimArt
{
    /// <summary>
    /// Timing of Eye Pop: seconds in, numbers out, no drawing and no map. The port of
    /// Tools/VfxLab/web/sketches/bubble-pipe-eye-pop.js; the constants are that sketch's defaults. The
    /// flight speed and the debuff's length of a real cast are the ability's (XML); the Script values are
    /// what the preview plays.
    ///
    /// The pipe goes up from <see cref="BubblePipeGraphics.Lead"/>, one bubble forms on the tip from
    /// <see cref="BlowAt"/> and leaves it at <see cref="LaunchAt"/>, flies at the target's face along a
    /// slight arc and bursts there at <see cref="HitAt"/>. A soapy film then stays over the eyes until
    /// <see cref="ClearAt"/>, with four tiny bubbles coming off the face and popping.
    /// </summary>
    public static class BubblePipeEyePopTiming
    {
        public const float Form = 0.2f, FaceH = 0.62f, Radius = 0.24f, Arc = 0.25f;
        /// <summary>A stand-in head is this far north of its feet; the film is drawn over it.</summary>
        public const float HeadUp = 0.58f;
        /// <summary>The tiny bubbles off the face: how many, how long each floats before it pops.</summary>
        public const int Tiny = 4;
        public const float TinyLife = 0.7f;
        public const int ScriptCost = 1;
        public const float ScriptDistance = 6f, ScriptSpeed = 5f, ScriptDebuff = 5f, ScriptBlows = 10f;

        public static float BlowAt => BubblePipeGraphics.Lead + BubblePipeGraphics.Raise;
        public static float LaunchAt => BlowAt + Form;
        public static float HitAt(float distance, float speed) => LaunchAt + distance / speed;
        public static float ClearAt(float distance, float speed, float debuff) => HitAt(distance, speed) + debuff;

        /// <summary>The preview keeps the pipe up until the debuff ends, as the sketch does.</summary>
        public static float ScriptLowerAt => ClearAt(ScriptDistance, ScriptSpeed, ScriptDebuff) + 0.35f;
        public static float ScriptDuration => ScriptLowerAt + BubblePipeGraphics.Lower + BubblePipeGraphics.Tail;
        public static float ScriptHitAt => HitAt(ScriptDistance, ScriptSpeed);

        /// <summary>A real cast lowers the pipe 0.1 s after the bubble leaves it.</summary>
        public static float CastLowerAt => LaunchAt + 0.1f;
        public static float CastDuration => CastLowerAt + BubblePipeGraphics.Lower;

        public static float Blows(float before, int cost, float seconds) =>
            before - cost * UnityEngine.Mathf.Clamp01((seconds - BlowAt) / Form);

        public static float Forming(float seconds)
        {
            float age = seconds - BlowAt;
            return age >= 0f && age < Form ? Radius * SixPathsSlamTiming.Smooth(age / Form) : 0f;
        }

        /// <summary>When tiny bubble <paramref name="k"/> leaves the face, from the hit, for a debuff of <paramref name="debuff"/> seconds.</summary>
        public static float TinyBornAfterHit(int k, float debuff) => 0.3f + k * (debuff - 0.8f) / Tiny;
    }
}
