using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    public class CompProperties_AbilityFalseFace : CompProperties_AbilityEffect
    {
        /// <summary>How far the genjutsu reaches, in cells.</summary>
        public float radius = 15f;
        /// <summary>Only humanlike enemies are caught; animals (manhunter packs) are not.</summary>
        public bool humanlikeOnly = true;

        public CompProperties_AbilityFalseFace()
        {
            compClass = typeof(CompAbilityEffect_FalseFace);
        }
    }

    /// <summary>
    /// One button, no target. Every enemy within reach whose attention is on Itachi (its enemy
    /// target, its job target or the thing it is aiming at) is put into <see cref="MentalState_FalseFace"/>
    /// against its nearest ally. Mechanoids are immune. The charge cost is the EchoDef's.
    /// </summary>
    public class CompAbilityEffect_FalseFace : CompAbilityEffect
    {
        public new CompProperties_AbilityFalseFace Props => (CompProperties_AbilityFalseFace)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            FalseFaceCast.Cast(parent.pawn, Props.radius, Props.humanlikeOnly);
        }
    }

    public static class FalseFaceCast
    {
        private static readonly List<Pawn> scratch = new List<Pawn>();

        /// <summary>Casts on everyone in reach. Returns how many were caught.</summary>
        public static int Cast(Pawn itachi, float radius, bool humanlikeOnly)
        {
            if (itachi == null || itachi.Map == null) return 0;
            FalseFaceFX.Caster(itachi);
            int caught = 0;
            foreach (Pawn victim in Victims(itachi, radius, humanlikeOnly))
            {
                Pawn ally = NearestAlly(victim);
                if (ally == null) continue;
                if (!victim.mindState.mentalStateHandler.TryStartMentalState(ItachiDefOf.AG_FalseFace, null,
                        forced: true, forceWake: false, causedByMood: false, otherPawn: itachi))
                    continue;
                if (!(victim.MentalState is MentalState_FalseFace state)) continue;
                state.Begin(ally);
                FalseFaceFX.Victim(victim);
                caught++;
            }
            if (caught == 0 && PawnUtility.ShouldSendNotificationAbout(itachi))
                Messages.Message("AG_ItachiFalseFaceNoVictims".Translate(), itachi, MessageTypeDefOf.NeutralEvent, false);
            return caught;
        }

        /// <summary>Whether this pawn's attention is on Itachi right now: eye contact, for a genjutsu.</summary>
        public static bool TargetsItachi(Pawn other, Pawn itachi)
        {
            if (other.mindState?.enemyTarget == itachi) return true;
            Job job = other.CurJob;
            if (job != null && job.targetA.HasThing && job.targetA.Thing == itachi) return true;
            if (other.stances?.curStance is Stance_Busy busy && busy.focusTarg.HasThing && busy.focusTarg.Thing == itachi) return true;
            return false;
        }

        /// <summary>A fresh list, safe to keep while the states start.</summary>
        public static List<Pawn> Victims(Pawn itachi, float radius, bool humanlikeOnly)
        {
            var result = new List<Pawn>();
            float radiusSquared = radius * radius;
            IReadOnlyList<Pawn> everyone = itachi.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < everyone.Count; i++)
            {
                Pawn other = everyone[i];
                if (other == itachi || other.Dead || other.Downed || !other.Spawned) continue;
                if (other.mindState == null || other.Faction == null) continue;
                if (other.RaceProps.IsMechanoid) continue;
                if (humanlikeOnly && !other.RaceProps.Humanlike) continue;
                if (!other.HostileTo(itachi)) continue;
                if ((other.Position - itachi.Position).LengthHorizontalSquared > radiusSquared) continue;
                if (!TargetsItachi(other, itachi)) continue;
                result.Add(other);
            }
            return result;
        }

        /// <summary>The nearest standing pawn of the victim's own faction, or null.</summary>
        public static Pawn NearestAlly(Pawn victim)
        {
            Pawn best = null;
            float bestDist = float.MaxValue;
            IReadOnlyList<Pawn> everyone = victim.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < everyone.Count; i++)
            {
                Pawn other = everyone[i];
                if (other == victim || other.Dead || other.Downed || !other.Spawned) continue;
                if (other.Faction != victim.Faction) continue;
                float dist = (other.Position - victim.Position).LengthHorizontalSquared;
                if (dist >= bestDist) continue;
                bestDist = dist;
                best = other;
            }
            return best;
        }
    }
}
