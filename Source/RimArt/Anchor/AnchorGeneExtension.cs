using Verse;

namespace RimArt
{
    /// <summary>
    /// The gene's balance levers, on the GeneDef so they can be changed without a rebuild -
    /// same reasoning as frozenAreInvulnerable on the stasis field.
    /// </summary>
    public class AnchorGeneExtension : DefModExtension
    {
        /// <summary>How many stones can be out at once.</summary>
        public int maxAnchors = 3;

        /// <summary>Game ticks a stone lasts. 60000 is one in-game day. Zero means stones never fade.</summary>
        public int markDurationTicks = 60000;

        /// <summary>
        /// The clap needs both hands, so the carrier does not carry a weapon at all. Setting
        /// this false leaves only the check at cast time, which turns the gene into a support
        /// ability on an armed pawn - a large balance swing, hence a field rather than a rule.
        /// </summary>
        public bool banWeapons = true;

        /// <summary>The item Mark throws. Its ThingDef holds the look; the rules are here.</summary>
        public ThingDef stoneDef;

        /// <summary>A living flesh pawn in sight this close (cells) is swapped without a stone.</summary>
        public float directSwapRange = 15f;

        /// <summary>
        /// A swap with any end farther than this (cells) from the carrier is a long-range swap:
        /// it needs every charge, spends every charge, and the stones it used are gone.
        /// </summary>
        public float longSwapRange = 25f;

        /// <summary>Claps held, shared by Clap and Double Clap.</summary>
        public int clapCharges = 3;

        /// <summary>Ticks to grow back one clap. Charges come back one at a time.</summary>
        public int chargeRechargeTicks = 600;

        /// <summary>Ticks a hostile pawn is stunned after it is swapped: it has lost track of where it is.</summary>
        public int swapStunTicks = 30;
    }
}
