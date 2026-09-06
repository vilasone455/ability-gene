using Verse;

namespace AbilityGenes
{
    /// <summary>
    /// The gene's balance levers, on the GeneDef so they can be changed without a rebuild -
    /// same reasoning as frozenAreInvulnerable on the stasis field.
    /// </summary>
    public class AnchorGeneExtension : DefModExtension
    {
        /// <summary>How many marks can be held at once. Three leaves one still in hand after a double clap.</summary>
        public int maxAnchors = 3;

        /// <summary>Game ticks a mark lasts. 60000 is one in-game day. Zero means marks never fade.</summary>
        public int markDurationTicks = 60000;

        /// <summary>
        /// The clap needs both hands, so the carrier does not carry a weapon at all. Setting
        /// this false leaves only the check at cast time, which turns the gene into a support
        /// ability on an armed pawn - a large balance swing, hence a field rather than a rule.
        /// </summary>
        public bool banWeapons = true;
    }
}
