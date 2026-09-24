using Verse;

namespace RimArt
{
    /// <summary>
    /// The castle's balance numbers, on the AG_InfinityCastle MapGeneratorDef so they change without a
    /// rebuild. Every number in docs/infinity-castle-kit.md that is not a shape lives here.
    /// </summary>
    public class InfinityCastleRules : DefModExtension
    {
        /// <summary>Shift: a room slides at most this many cells.</summary>
        public int shiftMaxCells = 20;

        /// <summary>Shift: how fast a room slides. The slide also takes 0.25 s to get going.</summary>
        public float shiftCellsPerSecond = 8f;

        /// <summary>Strums are at least this far apart: one command, then a pause.</summary>
        public float strumGapSeconds = 1.5f;

        /// <summary>The Void rule: a pawn dropped through the void is stunned this long after it lands.</summary>
        public float voidDropStunSeconds = 1f;

        private static readonly InfinityCastleRules fallback = new InfinityCastleRules();

        /// <summary>The numbers from the def, or the defaults above if the def carries none.</summary>
        public static InfinityCastleRules Of => InfinityCastleDefOf.AG_InfinityCastle?.GetModExtension<InfinityCastleRules>() ?? fallback;
    }
}
