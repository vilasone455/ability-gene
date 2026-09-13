using Verse;

namespace RimArt
{
    /// <summary>A thrown handful of makibishi. Deals no damage itself; spikes the patch where it lands.</summary>
    public class Projectile_Makibishi : Projectile
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            // Read before the base call, which destroys this.
            Map map = Map;
            IntVec3 cell = ExactPosition.ToIntVec3();
            Pawn thrower = launcher as Pawn;

            base.Impact(hitThing, blockedByShield);

            if (map == null || blockedByShield) return;
            MakibishiPatch.Scatter(cell, map, thrower);
        }
    }
}
