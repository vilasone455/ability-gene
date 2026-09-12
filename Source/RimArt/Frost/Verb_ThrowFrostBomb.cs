using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The throw, as a weapon verb rather than an ability.
    ///
    /// The frost bomb used to be an ability granted by a worn device, which put a psycast-shaped
    /// gizmo in the ability bar for something that is a grenade. It is a weapon now: equipped,
    /// aimed and thrown like the game's own grenades, and the whole of what this class does is
    /// keep the hand-thrown animation working in that form.
    ///
    /// <see cref="Verb_LaunchProjectile"/> spawns its projectile inside TryCastShot, which is
    /// half a second too early - the arm has not come round yet. So the cast checks the shot the
    /// way the base class would and then hands the launch to <see cref="MapComponent_Throws"/>,
    /// which puts the grenade in the air on the tick the hand opens. With Melee Animation absent
    /// that component launches on the spot and the verb behaves exactly like the vanilla one.
    ///
    /// Not calling down means the base class's miss model does not run either, so the part of it
    /// that a grenade needs is reproduced below. That is not a preference: ThingDef error
    /// checking requires it.
    ///
    ///     if (LaunchesProjectile &amp;&amp; defaultProjectile != null &amp;&amp; forcedMissRadius > 0f != CausesExplosion)
    ///         yield return "has incorrect forcedMiss settings; explosive projectiles and only
    ///                       explosive projectiles should have forced miss enabled";
    ///
    /// An explosive projectile must have a forced miss radius, so the choice was between
    /// declaring one and not honouring it - a def that lies about its own accuracy - and
    /// scattering for real. Scattering for real is also the more honest answer to "make it an
    /// actual grenade": every grenade in the game misses, and the falloff curve makes it miss in
    /// the right way, which is not at all under three cells and fully only past seven.
    ///
    /// What is still given up is wild shots and cover interception, both of which are aim models
    /// for shooting at a body. This is thrown at a cell.
    /// </summary>
    public class Verb_ThrowFrostBomb : Verb_LaunchProjectile
    {
        /// <summary>
        /// The texture put in the thrower's hand for the wind-up. Not a verb property, because
        /// the projectile and the thing in the hand are the same object and naming it twice is
        /// two places to get it wrong.
        /// </summary>
        private const string HandTexture = "RimArt/Frost/Bomb";

        protected override bool TryCastShot()
        {
            if (currentTarget.HasThing && currentTarget.Thing.Map != caster.Map) return false;

            ThingDef projectile = Projectile;
            if (projectile == null) return false;

            // Asked for the same reason and in the same order as the base class: a burst that
            // has lost line of sight stops here rather than throwing into a wall.
            bool hasLine = TryFindShootLineFromTo(caster.Position, currentTarget, out _);
            if (verbProps.stopBurstWithoutLos && !hasLine) return false;

            Pawn thrower = CasterPawn;
            if (thrower == null) return false;

            lastShotTick = Find.TickManager.TicksGame;
            MapComponent_Throws.Begin(thrower, ThrownAt(), projectile, HandTexture);
            return true;
        }

        /// <summary>
        /// Where the bomb actually lands, which is not always where it was aimed.
        ///
        /// The same two steps the base class takes, in the same order. CalculateAdjustedForcedMiss
        /// is what keeps this from feeling random: it returns zero inside three cells, half the
        /// radius inside five and four fifths inside seven, so a bomb lobbed into the next room
        /// goes where it was put and only a throw at the edge of range really scatters.
        ///
        /// GetForceMissFactorFor is not applied. It reads a stat that exists for mortars and
        /// returns 1 for everything else, and a hand-thrown grenade is not a mortar.
        ///
        /// The scattered cell is handed on as the target rather than as a miss, so the grenade
        /// flies to it and bursts there. Vanilla additionally widens the projectile's hit flags
        /// on a miss so it can clip things on the way; that governs what a projectile collides
        /// with in flight, and this one is an arcing explosive aimed at a patch of ground, which
        /// collides with nothing until it lands.
        /// </summary>
        private LocalTargetInfo ThrownAt()
        {
            float radius = verbProps.ForcedMissRadius;
            if (radius <= 0.5f) return currentTarget;

            float adjusted = VerbUtility.CalculateAdjustedForcedMiss(
                radius, currentTarget.Cell - caster.Position);
            if (adjusted <= 0.5f) return currentTarget;

            IntVec3 missed = GetForcedMissTarget(adjusted);
            return missed == currentTarget.Cell ? currentTarget : new LocalTargetInfo(missed);
        }

        /// <summary>
        /// The cloud the player is about to make, drawn under the cursor while aiming.
        ///
        /// Worth overriding because the freeze is wider than the blast: the base class reports the
        /// projectile's explosionRadius, and the cold reaches <see cref="FrostDefaults.BurstRadius"/>
        /// past it. Drawing the smaller ring would put the edge of the picture inside the edge of
        /// the effect, which on an item that catches allies is the one error worth spending a
        /// method on. A radius discovered after the throw is a trap; a radius drawn before it is
        /// a decision.
        ///
        /// needLOSToCenter stays true, which gets the blast-shaped preview rather than a plain
        /// disc - and that is also the truth, since the burst tests line of sight from its centre
        /// exactly as the explosion does.
        /// </summary>
        public override float HighlightFieldRadiusAroundTarget(out bool needLOSToCenter)
        {
            needLOSToCenter = true;
            return FrostDefaults.BurstRadius;
        }
    }
}
