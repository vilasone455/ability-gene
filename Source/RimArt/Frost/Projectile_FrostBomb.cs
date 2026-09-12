using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The frost bomb in flight, and the burst it leaves.
    ///
    /// Built on Projectile_Explosive rather than on Projectile so the grenade gets the game's own
    /// arc, fuse, impact sound and explosion for free - and, more to the point, so Combat Extended
    /// and every other mod that knows what a grenade is keeps recognising this one. The explosion
    /// itself is tuned in XML to be almost harmless; the damage here is the cold, not the bang.
    ///
    /// <see cref="Explode"/> is the only override, and it has to read the projectile's own state
    /// before calling down, because the base implementation destroys the projectile on its way
    /// through and every field on this object is gone by the time it returns.
    /// </summary>
    public class Projectile_FrostBomb : Projectile_Explosive
    {
        protected override void Explode()
        {
            Map map = Map;
            IntVec3 centre = Position;
            Thing instigator = launcher;

            base.Explode();

            FrostBurst.At(map, centre, instigator);
        }
    }
}
