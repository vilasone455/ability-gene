using UnityEngine;

namespace RimArt
{
    /// <summary>What stands at one end of a clap before or after the swap.</summary>
    public enum BoogieEnd { None, Pawn, Stone }

    /// <summary>
    /// Times and sizes of the Boogie Woogie clap picture, the defaults of the lab sketch
    /// (Tools/VfxLab/web/sketches/todo-boogie-woogie.js). Seconds in, numbers out, no drawing.
    ///
    /// The clock starts with the warmup. The swap is the warmup's end (Clap 0.3 s, Double Clap
    /// 0.5 s), read from the ability by the caller. The clap clips start late
    /// (ClapTeleport.ClipOffset) so the last palm contact lands on the swap; a Double Clap's first
    /// contact is ClapTeleport.FirstContactAt. None of this is balance.
    /// </summary>
    public static class BoogieWoogie
    {
        /// <summary>The picture runs this long after the swap.</summary>
        public const float Tail = 0.85f;
        /// <summary>The previews' warmups, the AbilityDefs' numbers.</summary>
        public const float ClapWarmup = 0.3f, DoubleWarmup = 0.5f;

        // The clap burst ("pan"): dashes and drops burst out of the palms in two waves a frame apart,
        // fly out in the first 45 % of their life, curling, then hang and fade; a faint ring opens.
        public const int Dashes = 16, Drops = 6;
        public const float Reach = 1f, Life = 0.4f, BurstStart = 0.15f, FirstScale = 0.6f, RingReach = 1.3f;
        public const float SecondWave = 0.065f, Curl = 0.25f, Turn = 0.45f;
        // The swap (S1): two ink frames on white, who stood there then who stands there now.
        public const float Ink = 0.13f, InkFade = 0.06f;
        public const int InkStrokes = 11, InkShards = 5;
        // The arrival (S2): three crescents swirl round whoever arrived, then flecks drift and hang.
        public const float SwooshFrom = 0.11f, SwooshLife = 0.22f, FleckFrom = 0.12f, Fleck = 0.6f;
        public const int Flecks = 14;
        public const float Shake = 0.02f;
        /// <summary>The preview's ends are this far either side of the middle; the double clap's Todo stands this far north.</summary>
        public const float HalfSpan = 3f, CarrierNorth = 2.5f;

        public static float Duration(float warmup) => warmup + Tail;

        /// <summary>A Double Clap's first palm contact, from the start of the warmup.</summary>
        public static float FirstContactAt(float warmup) => ClapTeleport.FirstContactAt(warmup);

        // How far up the screen the clip's main hand is from the feet, every 0.05 s from the palm
        // contact at 0.5 s (RimArt_Clap and RimArt_ClapTwice, read off the lab's clip): the burst
        // is drawn there, on the body's centre line, as the sketch draws it at clip.hand.
        private static readonly float[] ClapPalms = { 0.05f, 0.045f, 0.058f, 0.087f, 0.116f, 0.13f, 0.12f, 0.096f, 0.071f, 0.06f };
        private static readonly float[] TwicePalms = { 0.05f, 0.051f, 0.057f, 0.06f, 0.055f, 0.05f, 0.046f, 0.058f, 0.087f, 0.116f, 0.13f, 0.12f, 0.096f, 0.071f, 0.06f };

        /// <summary>The palms' height on screen above the feet, <paramref name="seconds"/> after the warmup began.</summary>
        public static float PalmsNorth(float seconds, float warmup, bool twice)
        {
            float clip = seconds + ClapTeleport.ClipOffset(warmup, twice) - ClapTeleport.FirstContact;
            float[] table = twice ? TwicePalms : ClapPalms;
            if (clip <= 0f) return table[0];
            float at = clip / 0.05f;
            int i = Mathf.FloorToInt(at);
            if (i >= table.Length - 1) return table[table.Length - 1];
            return Mathf.Lerp(table[i], table[i + 1], at - i);
        }
    }
}
