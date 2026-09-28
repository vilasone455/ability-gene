using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>Sealing touch's numbers, on the AG_MinatoSeal def. How long a seal lasts is its Disappears comp.</summary>
    public class MinatoSealExtension : DefModExtension
    {
        /// <summary>Seals Minato keeps at once; one more replaces the oldest.</summary>
        public int maxSeals = 3;
    }

    /// <summary>
    /// Sealing touch (passive): the Flying Thunder God formula Minato leaves on whoever his melee hits strike.
    /// A sealed pawn counts as holding one of his kunai for every Flying Thunder God ability
    /// (<see cref="ThunderGodMarks.Marked"/>). It fades after a day (the def's Disappears comp) and is lost
    /// when the pawn leaves the map. A hit on a pawn already sealed starts its day again.
    /// </summary>
    public class Hediff_MinatoSeal : HediffWithComps
    {
        /// <summary>Left the map (a world pawn now, or gone with a caravan): the seal is only good on the map it was put on.</summary>
        public override bool ShouldRemove => base.ShouldRemove || (pawn != null && !pawn.Dead && pawn.MapHeld == null);

        /// <summary>Starts the seal's day again.</summary>
        public void Renew()
        {
            ageTicks = 0;
            HediffComp_Disappears fade = this.TryGetComp<HediffComp_Disappears>();
            if (fade != null) fade.ticksToDisappear = fade.disappearsAfterTicks;
        }
    }

    public static class SealingTouch
    {
        private static readonly List<Hediff_MinatoSeal> found = new List<Hediff_MinatoSeal>();

        private static int MaxSeals => MinatoDefOf.AG_MinatoSeal.GetModExtension<MinatoSealExtension>()?.maxSeals ?? 3;

        /// <summary>Puts the seal on <paramref name="victim"/> (or renews it), dropping the oldest seal past the limit.</summary>
        public static void Seal(Pawn victim)
        {
            if (victim?.health == null || victim.Dead) return;
            Hediff_MinatoSeal seal = ThunderGodMarks.Seal(victim);
            if (seal != null)
            {
                seal.Renew();
                return;
            }
            All(found);
            int max = MaxSeals;
            while (found.Count >= max && found.Count > 0)
            {
                Hediff_MinatoSeal oldest = found[0];
                for (int i = 1; i < found.Count; i++)
                    if (found[i].ageTicks > oldest.ageTicks) oldest = found[i];
                found.Remove(oldest);
                oldest.pawn.health.RemoveHediff(oldest);
            }
            victim.health.AddHediff(MinatoDefOf.AG_MinatoSeal);
            GameComponent_Minato.Instance?.RefreshSeals();
        }

        /// <summary>Every seal on a pawn on any map, carried or not.</summary>
        public static void All(List<Hediff_MinatoSeal> into)
        {
            into.Clear();
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                IReadOnlyList<Pawn> pawns = maps[m].mapPawns.AllPawns;
                for (int i = 0; i < pawns.Count; i++)
                    if (ThunderGodMarks.Seal(pawns[i]) is Hediff_MinatoSeal seal) into.Add(seal);
            }
        }
    }

    /// <summary>
    /// Sealing touch: a landed melee hit by Minato in hero form seals the pawn it struck.
    /// Verb_MeleeAttack.TryCastShot calls ApplyMeleeDamageToTarget only after the hit and dodge rolls pass (the
    /// Samehada feed uses the same hook), and Flying Thunder God's strikes go through it too. Only damaging hits:
    /// the terrain move that kicks dirt in the face (Verb_MeleeApplyHediff) is not a touch.
    /// </summary>
    [HarmonyPatch(typeof(Verb_MeleeAttackDamage), "ApplyMeleeDamageToTarget")]
    static class Patch_MeleeAttack_SealingTouch
    {
        static void Postfix(Verb_MeleeAttackDamage __instance, LocalTargetInfo target)
        {
            if (!(target.Thing is Pawn victim) || victim.Dead) return;
            Pawn striker = __instance.CasterPawn;
            if (striker == null || victim == striker || !MinatoKit.IsMinato(striker)) return;
            SealingTouch.Seal(victim);
        }
    }
}
