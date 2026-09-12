using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// A thing thrown by hand, as a weapon verb rather than an ability.
    ///
    /// <see cref="Verb_LaunchProjectile"/> spawns its projectile inside TryCastShot, which is half
    /// a second too early - the arm has not come round yet. So the cast checks the shot the way
    /// the base class would and then hands the launch to <see cref="MapComponent_Throws"/>, which
    /// puts the object in the air on the tick the hand opens. With Melee Animation absent that
    /// component launches on the spot and the verb behaves exactly like the vanilla one.
    ///
    /// Not calling down means the base class's miss model does not run either, so the part of it
    /// a thrown object needs is reproduced below. That is not a preference: ThingDef error
    /// checking requires it.
    ///
    ///     if (LaunchesProjectile &amp;&amp; defaultProjectile != null &amp;&amp; forcedMissRadius > 0f != CausesExplosion)
    ///         yield return "has incorrect forcedMiss settings; explosive projectiles and only
    ///                       explosive projectiles should have forced miss enabled";
    ///
    /// An explosive projectile must therefore declare a forced miss radius and a non-explosive one
    /// must not, and the scatter below honours whatever is declared - including nothing, which is
    /// how a non-explosive thrown object gets pinpoint delivery without a special case.
    ///
    /// What is still given up is wild shots and cover interception, both of which are aim models
    /// for shooting at a body. These are thrown at a cell.
    ///
    /// This class was the frost bomb until the mimic beacon needed the same three behaviours.
    /// Everything that differs between two thrown things is below the line: what goes in the
    /// hand, and what the cursor draws.
    /// </summary>
    public abstract class Verb_ThrowByHand : Verb_LaunchProjectile
    {
        /// <summary>
        /// The texture put in the thrower's hand for the wind-up. Not a verb property, because for
        /// every caller so far the projectile and the thing in the hand are the same object, and
        /// naming it twice is two places to get it wrong.
        /// </summary>
        protected abstract string HandTexture { get; }

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
        /// Where the thing actually lands, which is not always where it was aimed.
        ///
        /// The same two steps the base class takes, in the same order. CalculateAdjustedForcedMiss
        /// is what keeps this from feeling random: it returns zero inside three cells, half the
        /// radius inside five and four fifths inside seven, so something lobbed into the next room
        /// goes where it was put and only a throw at the edge of range really scatters.
        ///
        /// GetForceMissFactorFor is not applied. It reads a stat that exists for mortars and
        /// returns 1 for everything else, and a hand-thrown object is not a mortar.
        ///
        /// The scattered cell is handed on as the target rather than as a miss, so the object
        /// flies to it and arrives there. Vanilla additionally widens the projectile's hit flags
        /// on a miss so it can clip things on the way; that governs what a projectile collides
        /// with in flight, and these are arcing things aimed at a patch of ground, which collide
        /// with nothing until they land.
        /// </summary>
        protected LocalTargetInfo ThrownAt()
        {
            float radius = verbProps.ForcedMissRadius;
            if (radius <= 0.5f) return currentTarget;

            float adjusted = VerbUtility.CalculateAdjustedForcedMiss(
                radius, currentTarget.Cell - caster.Position);
            if (adjusted <= 0.5f) return currentTarget;

            IntVec3 missed = GetForcedMissTarget(adjusted);
            return missed == currentTarget.Cell ? currentTarget : new LocalTargetInfo(missed);
        }
    }
}
