using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// The attack jobs of a False Face victim, reached through the MentalStateCritical think tree
    /// (1.6/Patches/AG_Itachi_ThinkTree.xml). Not the vanilla fight-enemies giver: its Wait_Combat
    /// job shoots whatever AttackTargetFinder picks, and that finder never returns a pawn of the
    /// victim's own faction. So the false Itachi is attacked by name: AttackMelee for a melee
    /// verb, AttackStatic for a ranged one that can hit from here, a walk to a shooting position
    /// otherwise. Every job is short and re-checked, so the state's end is noticed at once.
    /// </summary>
    public class JobGiver_FalseFace : JobGiver_AIFightEnemies
    {
        private const int Expiry = 120;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!(pawn.MentalState is MentalState_FalseFace state) || !state.AllyValid) return null;
            Pawn ally = state.falseItachi;
            pawn.mindState.enemyTarget = ally;

            Verb verb = pawn.TryGetAttackVerb(ally, !pawn.IsColonist, false);
            if (verb == null) return Hold();

            if (verb.verbProps.IsMeleeAttack) return Melee(ally);

            if (verb.CanHitTarget(ally))
            {
                Job shoot = JobMaker.MakeJob(JobDefOf.AttackStatic, ally);
                shoot.expiryInterval = Expiry;
                shoot.checkOverrideOnExpire = true;
                shoot.endIfCantShootTargetFromCurPos = true;
                return shoot;
            }

            if (TryFindShootingPosition(pawn, out IntVec3 dest, verb) && dest != pawn.Position)
            {
                Job walk = JobMaker.MakeJob(JobDefOf.Goto, dest);
                walk.expiryInterval = Expiry;
                walk.checkOverrideOnExpire = true;
                return walk;
            }

            // No shooting position: close in, and the next check finds the shot.
            return Melee(ally);
        }

        private static Job Melee(Pawn ally)
        {
            Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, ally);
            job.expiryInterval = Expiry;
            job.checkOverrideOnExpire = true;
            job.canBashDoors = true;
            return job;
        }

        private static Job Hold()
        {
            Job job = JobMaker.MakeJob(JobDefOf.Wait, 60);
            job.checkOverrideOnExpire = true;
            return job;
        }
    }
}
