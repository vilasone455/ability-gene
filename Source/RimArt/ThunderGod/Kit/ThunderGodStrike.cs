using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The cut Minato makes on landing behind a mark: one melee attack with his own weapon or fists that cannot
    /// miss (he appears behind them), at a share of its damage (x1.5 for the jump, x1.0 for each link of the
    /// chain). The attack is one of the pawn's damaging melee verbs applied without the hit roll, as the Nezuko
    /// box's landing strike does; the share scales VerbProperties.AdjustedMeleeDamageAmount for this one call.
    /// Armour penetration is left as the verb makes it. Being a landed melee hit, it also leaves sealing touch.
    /// </summary>
    public static class ThunderGodStrike
    {
        private static readonly MethodInfo applyMelee = AccessTools.Method(typeof(Verb_MeleeAttack), "ApplyMeleeDamageToTarget");

        /// <summary>Set only while <see cref="Hit"/> applies its attack: whose damage is scaled, and by how much.</summary>
        internal static Pawn scaling;
        internal static float factor = 1f;

        /// <summary>One sure melee hit of <paramref name="caster"/> on <paramref name="victim"/> at <paramref name="share"/> of its damage.</summary>
        public static bool Hit(Pawn caster, Pawn victim, float share)
        {
            if (caster == null || victim == null || victim.Dead || applyMelee == null) return false;
            Verb_MeleeAttack melee = DamageVerb(caster, victim);
            if (melee == null) return false;
            if (caster.Spawned && victim.Spawned) caster.rotationTracker.FaceTarget(victim);
            scaling = caster;
            factor = share;
            try
            {
                applyMelee.Invoke(melee, new object[] { new LocalTargetInfo(victim) });
            }
            finally
            {
                scaling = null;
                factor = 1f;
            }
            if (!victim.Dead && victim.mindState != null)
            {
                victim.mindState.meleeThreat = caster;
                victim.mindState.lastMeleeThreatHarmTick = Find.TickManager.TicksGame;
            }
            return true;
        }

        /// <summary>
        /// One of his melee attacks that deals damage, chosen by the game's own weights. The game's choice
        /// (TryGetMeleeVerb) is a terrain move 4 % of the time - dirt kicked in the face, which applies a hediff
        /// and no damage - so it is not used as it is.
        /// </summary>
        private static Verb_MeleeAttack DamageVerb(Pawn caster, Pawn victim)
        {
            List<VerbEntry> verbs = caster.meleeVerbs?.GetUpdatedAvailableVerbsList(terrainTools: false);
            if (verbs != null && verbs.Where(v => v.verb is Verb_MeleeAttackDamage && v.verb.IsUsableOn(victim))
                    .TryRandomElementByWeight(v => v.GetSelectionWeight(victim), out VerbEntry pick))
                return (Verb_MeleeAttack)pick.verb;
            return caster.meleeVerbs?.TryGetMeleeVerb(victim) as Verb_MeleeAttackDamage;
        }
    }

    [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.AdjustedMeleeDamageAmount), typeof(Verb), typeof(Pawn))]
    static class Patch_VerbProperties_ThunderGodStrike
    {
        static void Postfix(Pawn attacker, ref float __result)
        {
            if (ThunderGodStrike.scaling != null && attacker == ThunderGodStrike.scaling) __result *= ThunderGodStrike.factor;
        }
    }
}
