using UnityEngine;

namespace RimArt
{
    /// <summary>What marks one end of a clap before the contact spends it.</summary>
    public enum ClapMark { None, Pawn, Tile }

    /// <summary>
    /// One end of a clap: a cell something leaves, arrives at, or both. <see cref="ring"/> false is
    /// the cell a tile-move leaves, which gets the puff and one falling card and no ring.
    /// </summary>
    public struct ClapEnd
    {
        public Vector2 ground;
        public int suit;
        public bool ring;
        public ClapMark mark;
    }

    /// <summary>
    /// Times and sizes of the clap teleport picture, the defaults of the lab sketch
    /// (Tools/VfxLab/web/sketches/anchor-clap-teleport.js). Seconds in, numbers out, no drawing.
    ///
    /// The clock starts with the warmup. The contact is the warmup's end, which is where the swap
    /// happens, so it is not a constant here: the caller reads it from the ability's warmupTime.
    /// <see cref="FirstContact"/> and <see cref="SecondContact"/> are the clips' own palm
    /// contacts and what the previews use; make_clap_anim.py and the two AbilityDefs carry the
    /// same two numbers.
    ///
    /// None of this is balance. Marks held, mark life, ranges and cooldowns are on
    /// AnchorGeneExtension and the AbilityDefs.
    /// </summary>
    public static class ClapTeleport
    {
        public const float FirstContact = 0.5f, SecondContact = 0.75f;
        /// <summary>Lengths of RimArt_Clap and RimArt_ClapTwice, the last keys in make_clap_anim.py.</summary>
        public const float ClipLength = 0.95f, ClipTwiceLength = 1.2f;
        /// <summary>The cards start rising this long before the contact.</summary>
        public const float Rise = 0.2f;
        /// <summary>The puff is opaque from <see cref="PuffIn"/> before the contact to this long after it.</summary>
        public const float Cover = 0.15f, PuffIn = 0.05f, PuffFade = 0.4f;
        /// <summary>The cards on the floor are gone this long after the contact; they fade over the last <see cref="FloorFade"/>.</summary>
        public const float Fade = 1.2f, FloorFade = 0.4f, Tail = 0.25f;
        public const float FlashLife = 0.12f, SparkleLife = 0.45f, PalmStarLife = 0.18f;
        public const float Shake = 0.02f;

        public const int Cards = 8, Sparkles = 6;
        public const float Radius = 0.55f, Height = 1.1f, Spin = 540f, Scatter = 1.25f, Puff = 0.7f;
        public const float CardWidth = 0.13f, CardHeight = 0.18f, MarkScale = 1.5f, Edge = 0.022f;
        /// <summary>Cells above the ground: 1.65 draws a pawn's mark card just clear of the head.</summary>
        public const float MarkHeight = 1.65f;
        /// <summary>Radians per second a falling card turns about its own upright axis.</summary>
        public const float Flip = 11f;
        /// <summary>Height is drawn as a shift north, as everywhere in the mod's top-down drawing.</summary>
        public const float Lift = SixPathsHeight.Lift;
        /// <summary>The palms are this far up the screen from the carrier's cell centre when they meet.</summary>
        public const float PalmsNorth = 0.05f;

        public const int Spade = 0, Heart = 1, Club = 2;

        public static float Duration(float contact) => contact + Fade + Tail;

        /// <summary>
        /// Seconds into the clip a cast with this warmup starts, so the clip's last palm contact
        /// lands on the warmup's end. Zero when the warmup is as long as the clip's contact or longer.
        /// </summary>
        public static float ClipOffset(float warmup, bool twice) => Mathf.Max(0f, (twice ? SecondContact : FirstContact) - warmup);

        /// <summary>A double clap's first palm contact, counted from the start of the warmup.</summary>
        public static float FirstContactAt(float warmup) => warmup - (SecondContact - FirstContact);

        /// <summary>A point <paramref name="height"/> cells above a ground point.</summary>
        public static Vector2 Above(Vector2 ground, float east, float north, float height) =>
            new Vector2(ground.x + east, ground.y + north + height * Lift);
    }
}
