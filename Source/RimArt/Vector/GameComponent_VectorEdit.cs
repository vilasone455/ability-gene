using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Owns the edited-projectile table across saves, and cleans up after the ability this one
    /// replaced.
    ///
    /// The table is committed state - which rounds are flying at what force - so it is scribed
    /// here rather than left to rebuild itself; a save taken mid-flight has to reload with the
    /// same rounds still moving at the same speed. The editor's draft groups are not saved and
    /// never have been: a session cannot outlive the frame the game was saved in.
    /// </summary>
    public class GameComponent_VectorEdit : GameComponent
    {
        public GameComponent_VectorEdit(Game game)
        {
            // A new game or a load replaces the Game object, and with it every projectile the
            // old table was keyed on. Nothing here survives that.
            VectorEditSession.Abandon();
            VectorEditRegistry.Clear();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            VectorEditRegistry.Expose();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            MigrateLegacyReflection();
        }

        /// <summary>
        /// Reflection is gone, and a save made while it was up must not keep its effects.
        ///
        /// Two things are left behind. The hediff rooted its carrier and refused every
        /// reservation aimed at them, and with the comp that maintained it removed it would sit
        /// there forever doing the rooting and nothing else - so it is taken off. And the old
        /// ability had a one-day cooldown against the new five seconds, so a carrier who cast it
        /// shortly before saving would come back unable to use the replacement for most of a
        /// day; the remaining cooldown is clamped to the new maximum instead.
        /// </summary>
        private static void MigrateLegacyReflection()
        {
            HediffDef legacy = DefDatabase<HediffDef>.GetNamed("AG_VectorReflection", false);

            List<Pawn> pawns = PawnsFinder.AllMapsWorldAndTemporary_Alive;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null) continue;

                if (legacy != null && pawn.health != null)
                {
                    Hediff held = pawn.health.hediffSet.GetFirstHediffOfDef(legacy);
                    if (held != null) pawn.health.RemoveHediff(held);
                }

                if (pawn.abilities == null) continue;

                // includeTemporary, which is what reaches abilities granted by a hediff rather
                // than held by the tracker itself. The reflex booster is an implant, so its two
                // abilities live on the hediff and the default lookup never sees them.
                Ability ability = pawn.abilities.GetAbility(VectorDefOf.AG_VectorReflection, true);
                if (ability == null) continue;
                if (ability.CooldownTicksRemaining <= VectorEditDefaults.CooldownTicks) continue;

                ability.StartCooldown(VectorEditDefaults.CooldownTicks);
            }
        }
    }
}
