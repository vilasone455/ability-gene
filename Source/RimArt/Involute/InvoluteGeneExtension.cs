using Verse;

namespace RimArt
{
    /// <summary>
    /// The dimension's size and generator, on the gene def. The abilities' numbers are on their own defs'
    /// comps (<see cref="ObitoRules"/>).
    /// </summary>
    public class InvoluteGeneExtension : DefModExtension
    {
        /// <summary>Volume size in cells. Kamui's dimension is square, so only X is used.</summary>
        public int volumeSizeX = 48;
        public int volumeSizeZ = 48;

        /// <summary>The map generator the volume is built from.</summary>
        public MapGeneratorDef volumeGenerator;
    }
}
