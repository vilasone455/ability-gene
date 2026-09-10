using Verse;

namespace RimArt
{
    /// <summary>
    /// The gene's balance levers, on the GeneDef so they can be changed without a rebuild -
    /// same reasoning as banWeapons on the anchor organ.
    /// </summary>
    public class InvoluteGeneExtension : DefModExtension
    {
        /// <summary>Volume size in cells. Small on purpose: an unattended map ticks forever.</summary>
        public int volumeSizeX = 32;
        public int volumeSizeZ = 32;

        /// <summary>The map generator the volume is built from.</summary>
        public MapGeneratorDef volumeGenerator;

        /// <summary>
        /// Put the hole in this part instead of rolling for one, and skip the candidate filter
        /// while doing it.
        ///
        /// This is a testing and tuning lever, not a balance one, and it is the only way to
        /// reach the torso: the roll draws from ResonanceUtility.CanRing, which excludes the
        /// body's core part and anything with a vital organ hanging off it. A hole in a torso
        /// eats something like 40% of incoming fire, which is not a share, it is immunity.
        ///
        /// Leave null to roll.
        /// </summary>
        public BodyPartDef forceHolePart;

        /// <summary>
        /// The largest body that fits through. Human is 1.0, a scyther is 1.0, a megaspider is
        /// 1.2; a centipede is 3.0 and every Biotech boss mech is 3.5 or more.
        ///
        /// This is what pays for a short cooldown. Swallow removes a target from a fight with no
        /// damage roll and nothing to resist, and against a diabolus that is not an ability, it
        /// is a delete key. Capping by size stops exactly that case and leaves the rescue - a
        /// colonist is 1.0 - completely alone.
        ///
        /// It also needs no excuse. The hole is hand-sized. A person barely fits.
        /// </summary>
        public float maxSwallowBodySize = 1.2f;

        /// <summary>
        /// Drop the core-part and vital-organ exclusions from the candidate set, so the roll can
        /// reach the torso and the head instead of only ever finding a limb.
        ///
        /// This is a real balance decision, not a test switch. A torso hole eats around 40% of
        /// incoming fire, which is close to bulletproof - but the hole's efficiency penalty then
        /// lands on the part the whole body hangs off, so that carrier is very hard to shoot and
        /// barely able to work. It is a trade rather than a jackpot, and it is rare: the torso is
        /// one candidate among roughly fifteen.
        ///
        /// Raise maxHoleCoverage alongside it or nothing changes - a torso is around 40% and the
        /// hand-sized band tops out at 10%.
        /// </summary>
        public bool allowAnyPart = false;

        /// <summary>
        /// Coverage bounds for the part the hole is rolled onto, as a share of the whole body.
        /// This is the entire balance of the pass-through: RimWorld picks hit parts by coverage
        /// weight, so a hole in the torso is near-immunity and a hole in a finger is nothing.
        /// Hand-sized is the band that makes it a real effect without making it the only one.
        /// </summary>
        public float minHoleCoverage = 0.02f;
        public float maxHoleCoverage = 0.10f;

        /// <summary>
        /// What the hole costs, permanently, as an efficiency offset on the part it is in.
        /// Stated on the part rather than as a capacity penalty so it stays correct wherever
        /// the roll lands - a hole in a hand costs manipulation, a hole in a leg costs walking,
        /// and the engine works out which without this mod naming either.
        /// </summary>
        public float holeEfficiencyOffset = -0.5f;
    }
}
