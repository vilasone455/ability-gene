using Verse;

namespace RimArt
{
    /// <summary>
    /// The gene's balance levers, on the GeneDef so they can be changed without a rebuild -
    /// same reasoning as <see cref="AnchorGeneExtension"/>.
    ///
    /// This gene needs them more than the others do. Scatter is the only thing in the mod that
    /// makes a damage instance not happen at all, and the phase barrier's own notes explain
    /// at length why that is a line worth not crossing. It is crossed here on purpose, and
    /// everything that keeps it honest is a number in this file.
    /// </summary>
    public class DispersalGeneExtension : DefModExtension
    {
        /// <summary>Shared charges available for Scatter and Murder.</summary>
        public int maxCharges = 3;

        /// <summary>One charge per in-game hour (2500 ticks).</summary>
        public int rechargeTicks = 2500;

        /// <summary>Ignore minor hits so they do not consume a charge.</summary>
        public float minimumDamage = 6f;

        /// <summary>Blood loss from emergency Scatter. Deliberate Murder has no blood cost.</summary>
        public float bloodLossPerScatter = 0.04f;

        /// <summary>Closest the flock will put the carrier back down, in cells.</summary>
        public int minRange = 5;

        /// <summary>
        /// Furthest the flock will put the carrier back down, in cells.
        ///
        /// Ten is inside a rifle's range on purpose. Scatter is not an escape - it moves the
        /// carrier out of the swing that was about to land and nowhere near out of the fight.
        /// </summary>
        public int maxRange = 10;
    }
}
