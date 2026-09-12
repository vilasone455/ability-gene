using System.Collections.Generic;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The cryogenic burst, hung on the damage rather than on the thing that delivered it.
    ///
    /// This used to live on a Projectile_Explosive subclass, which worked and was simpler to
    /// read - and made the bomb impossible to hand to Combat Extended. Their verb spawns a
    /// projectile and casts it straight to ProjectileCE:
    ///
    ///     protected virtual ProjectileCE SpawnProjectile()
    ///         =&gt; (ProjectileCE)ThingMaker.MakeThing(Projectile, null);
    ///
    /// so any projectile CE throws has to be one of theirs, and a subclass of ours cannot be.
    /// Moving the freeze onto the DamageDef unties the two: whatever spawns the explosion and
    /// whatever class it belongs to, an explosion of AG_Cryo freezes what it touches.
    ///
    /// It also makes the rule honest in the other direction. A frost bomb burning in a fire
    /// detonates through CompProperties_Explosive with no projectile involved at all, and that
    /// blast now freezes the room exactly like a thrown one - which is what a player who has
    /// just watched a rack of charge tubes cook off would expect.
    /// </summary>
    public class DamageWorker_Cryo : DamageWorker_AddInjury
    {
        /// <summary>
        /// Freezes everything the blast reaches, once, as the explosion starts.
        ///
        /// ExplosionStart rather than ExplosionAffectCell: the burst walks its own cells and
        /// decides its own falloff, and a per-cell hook would freeze a pawn once for every cell
        /// it stands in. Called before the explosion resolves its damage, which does not matter
        /// to a freeze that reads nothing but position - and matters the right way for a pawn the
        /// blast is about to kill, who is stunned and then dies rather than being stunned as a
        /// corpse.
        /// </summary>
        public override void ExplosionStart(Explosion explosion, List<IntVec3> cellsToAffect)
        {
            base.ExplosionStart(explosion, cellsToAffect);

            if (explosion == null) return;
            FrostBurst.At(explosion.Map, explosion.Position, explosion.instigator);
        }
    }
}
