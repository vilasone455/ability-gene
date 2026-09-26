using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// Every change to an Echo's state goes through here: tuning, Trials, awakening, manifesting and
    /// reverting. The device tab, the gizmos, the letter and the debug window all call these, so
    /// they cannot disagree on what awakening or reverting means.
    /// </summary>
    public static class EchoUtility
    {
        /// <summary>
        /// Raised after the pool empties and every Host has reverted. Pocket-space kits close their
        /// space and return everyone inside when this fires.
        /// </summary>
        public static event Action PoolEmptied;

        public static IEnumerable<EchoDef> AllEchoes =>
            DefDatabase<EchoDef>.AllDefsListForReading.OrderBy(d => d.order).ThenBy(d => d.label);

        // ---- candidates and Trials ----

        public static bool CanBeCandidate(Pawn pawn, out string reason)
        {
            reason = null;
            if (pawn == null || pawn.Dead) { reason = "dead"; return false; }
            if (!pawn.IsColonist || pawn.Faction != Faction.OfPlayer)
            {
                reason = "AG_EchoNotColonist".Translate();
                return false;
            }
            if (!pawn.RaceProps.Humanlike || pawn.story == null || pawn.skills == null)
            {
                reason = "AG_EchoNotHumanlike".Translate();
                return false;
            }
            if (!pawn.DevelopmentalStage.Adult())
            {
                reason = "AG_EchoNotAdult".Translate();
                return false;
            }
            if (GameComponent_Echoes.Get?.HostRecord(pawn) != null)
            {
                reason = "AG_EchoAlreadyHost".Translate();
                return false;
            }
            return true;
        }

        public static bool TrialsMet(EchoRecord record) =>
            record.candidate != null && record.def.trials.All(t => t.Met(record.candidate));

        /// <summary>Tunes the device to one Echo and one candidate. Any other tuning stops.</summary>
        public static void Tune(EchoDef def, Pawn candidate)
        {
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            EchoRecord record = echoes.RecordFor(def);
            if (record.state == EchoState.Awakened || record.state == EchoState.Closed) return;
            if (!CanBeCandidate(candidate, out _)) return;
            EchoRecord previous = echoes.Tuned;
            if (previous != null && previous != record) StopTuning(previous, lost: false);

            record.state = EchoState.Tracking;
            record.candidate = candidate;
            record.offered = false;
            record.announced.Clear();
            // Trials already met when tuning are not news.
            for (int i = 0; i < def.trials.Count; i++)
                if (def.trials[i].Met(candidate)) record.announced.Add(i);
            Messages.Message("AG_EchoTuned".Translate(def.LabelCap, candidate.LabelShortCap), candidate,
                MessageTypeDefOf.NeutralEvent, false);
            CheckTrials(record);
        }

        public static void StopTuning(EchoRecord record, bool lost)
        {
            if (record.state != EchoState.Tracking) return;
            if (lost && record.candidate != null)
                Messages.Message("AG_EchoCandidateLost".Translate(record.def.LabelCap, record.candidate.LabelShortCap),
                    MessageTypeDefOf.NegativeEvent, false);
            record.state = EchoState.Untuned;
            record.candidate = null;
            record.offered = false;
            record.announced.Clear();
        }

        public static void CheckTrials(EchoRecord record)
        {
            Pawn pawn = record.candidate;
            if (record.state != EchoState.Tracking || pawn == null) return;
            List<EchoTrial> trials = record.def.trials;
            for (int i = 0; i < trials.Count; i++)
            {
                if (record.announced.Contains(i) || !trials[i].Met(pawn)) continue;
                record.announced.Add(i);
                Messages.Message("AG_EchoTrialCleared".Translate(pawn.LabelShortCap, record.def.LabelCap, trials[i].Label),
                    pawn, MessageTypeDefOf.PositiveEvent, false);
            }
            if (record.offered || !TrialsMet(record)) return;

            record.offered = true;
            var letter = (ChoiceLetter_EchoAwakening)LetterMaker.MakeLetter(
                "AG_EchoAwakenLabel".Translate(record.def.LabelCap),
                AwakenText(record.def, pawn), EchoDefOf.AG_EchoAwakening, pawn);
            letter.echo = record.def;
            letter.pawn = pawn;
            Find.LetterStack.ReceiveLetter(letter);
        }

        public static string AwakenText(EchoDef def, Pawn pawn)
        {
            string text = "AG_EchoAwakenText".Translate(pawn.LabelShortCap, def.LabelCap);
            List<string> costs = CostLines(def, pawn).ToList();
            if (costs.Count > 0) text += "\n\n" + "AG_EchoCostHeader".Translate() + "\n" + costs.ToLineList("  - ");
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            if (echoes.HostCount >= echoes.HeroCap)
                text += "\n\n" + "AG_EchoCapFull".Translate(echoes.HostCount, echoes.HeroCap);
            return text;
        }

        // ---- costs ----

        /// <summary>Traits this cost would remove from the pawn. Traits from genes are suppressed, not removed.</summary>
        private static IEnumerable<Trait> Replaced(EchoTraitCost cost, Pawn pawn)
        {
            if (pawn?.story?.traits == null) yield break;
            foreach (Trait trait in pawn.story.traits.allTraits)
            {
                if (trait.sourceGene != null) continue;
                if (trait.def == cost.trait)
                {
                    if (trait.Degree != cost.degree) yield return trait;
                    continue;
                }
                if (cost.trait.ConflictsWith(trait)) yield return trait;
            }
        }

        /// <summary>One line per cost as it would apply to this pawn, for the card and the letter.</summary>
        public static IEnumerable<string> CostLines(EchoDef def, Pawn pawn)
        {
            foreach (EchoTraitCost cost in def.forcedTraits)
            {
                if (pawn != null && pawn.story?.traits?.HasTrait(cost.trait, cost.degree) == true)
                {
                    yield return "AG_EchoCostTraitHas".Translate(cost.Label);
                    continue;
                }
                List<Trait> replaced = pawn == null ? new List<Trait>() : Replaced(cost, pawn).ToList();
                yield return replaced.Count == 0
                    ? "AG_EchoCostTrait".Translate(cost.Label)
                    : "AG_EchoCostTraitReplace".Translate(cost.Label, replaced.Select(t => t.LabelCap).ToCommaList());
            }
        }

        private static void ApplyCosts(EchoDef def, Pawn pawn)
        {
            if (pawn.story?.traits == null) return;
            foreach (EchoTraitCost cost in def.forcedTraits)
            {
                if (pawn.story.traits.HasTrait(cost.trait, cost.degree)) continue;
                foreach (Trait trait in Replaced(cost, pawn).ToList()) pawn.story.traits.RemoveTrait(trait);
                pawn.story.traits.GainTrait(new Trait(cost.trait, cost.degree, forced: true), suppressConflicts: true);
            }
        }

        // ---- awakening ----

        public static bool CanAwaken(EchoRecord record, out string reason)
        {
            reason = null;
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            if (record.state != EchoState.Tracking || !TrialsMet(record))
            {
                reason = "AG_EchoTrialsNotMet".Translate();
                return false;
            }
            if (echoes.HostCount >= echoes.HeroCap)
            {
                reason = "AG_EchoCapFull".Translate(echoes.HostCount, echoes.HeroCap);
                return false;
            }
            return true;
        }

        /// <summary>Makes a Host without Trials, for the debug window and tests. Costs still apply.</summary>
        public static EchoRecord ForceHost(EchoDef def, Pawn pawn)
        {
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            EchoRecord old = echoes.HostRecord(pawn);
            if (old != null) return old;
            EchoRecord record = echoes.RecordFor(def);
            if (record.state == EchoState.Awakened) return null;
            ApplyCosts(def, pawn);
            record.state = EchoState.Awakened;
            record.host = pawn;
            record.candidate = null;
            record.offered = false;
            record.announced.Clear();
            return record;
        }

        /// <summary>The single place a colonist becomes a Host.</summary>
        public static bool Awaken(EchoRecord record)
        {
            if (!CanAwaken(record, out string reason))
            {
                Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            Pawn pawn = record.candidate;
            ApplyCosts(record.def, pawn);
            record.state = EchoState.Awakened;
            record.host = pawn;
            record.candidate = null;
            record.offered = false;
            record.announced.Clear();
            Find.LetterStack.ReceiveLetter("AG_EchoAwakenedLabel".Translate(record.def.LabelCap),
                "AG_EchoAwakened".Translate(pawn.LabelShortCap, record.def.LabelCap), LetterDefOf.PositiveEvent, pawn);
            return true;
        }

        public static void CloseSeat(EchoRecord record)
        {
            if (record.manifested) Revert(record, collapse: false);
            Pawn host = record.host;
            record.state = EchoState.Closed;
            Find.LetterStack.ReceiveLetter("AG_EchoClosedLabel".Translate(record.def.LabelCap),
                "AG_EchoClosed".Translate(host?.LabelShortCap ?? "?", record.def.LabelCap), LetterDefOf.NegativeEvent);
        }

        // ---- manifest and revert ----

        public static bool CanManifest(EchoRecord record, out string reason)
        {
            reason = null;
            Pawn pawn = record.host;
            if (record.state != EchoState.Awakened || pawn == null || pawn.Dead)
            {
                reason = "AG_EchoNoHost".Translate();
                return false;
            }
            if (pawn.Downed)
            {
                reason = "AG_EchoDowned".Translate();
                return false;
            }
            if (pawn.health.hediffSet.HasHediff(EchoDefOf.AG_EchoCollapse))
            {
                reason = "AG_EchoCollapsed".Translate();
                return false;
            }
            if (GameComponent_Echoes.Get.charge <= 0f)
            {
                reason = "AG_EchoNoCharge".Translate();
                return false;
            }
            return true;
        }

        public static bool Manifest(EchoRecord record)
        {
            if (record.manifested || !CanManifest(record, out _)) return false;
            Pawn pawn = record.host;
            record.manifested = true;
            pawn.health.AddHediff(record.def.manifestHediff);
            pawn.Notify_DisabledWorkTypesChanged();
            record.grant.Give(pawn, record.def.abilities);
            ApplyLook(record, pawn);
            // Hero form does no colony work: drop the job it was doing unless it is fighting.
            if (!pawn.Drafted && pawn.CurJob != null && pawn.jobs != null)
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            FleckMaker.ThrowLightningGlow(pawn.DrawPos, pawn.MapHeld, 1.5f);
            return true;
        }

        public static void Revert(EchoRecord record, bool collapse)
        {
            if (!record.manifested) return;
            Pawn pawn = record.host;
            record.manifested = false;
            if (pawn == null) return;
            // manifested is already false, so GrantedByOtherSource does not count the Echo itself.
            record.grant.Take(pawn, record.def.abilities, null);
            Hediff hediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(record.def.manifestHediff);
            if (hediff != null) pawn.health.RemoveHediff(hediff);
            if (!pawn.Dead) pawn.Notify_DisabledWorkTypesChanged();
            RestoreLook(record, pawn);
            if (collapse && !pawn.Dead) pawn.health.AddHediff(EchoDefOf.AG_EchoCollapse);
        }

        public static void EmptyPool()
        {
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            List<EchoRecord> manifested = echoes.Manifested.ToList();
            foreach (EchoRecord record in manifested)
            {
                Revert(record, collapse: true);
                if (record.host != null)
                    Messages.Message("AG_EchoPoolEmpty".Translate(record.host.LabelShortCap, record.def.LabelCap),
                        record.host, MessageTypeDefOf.NegativeEvent, false);
            }
            PoolEmptied?.Invoke();
        }

        /// <summary>Hair colour, and the standard body on humans. Other races keep their own body.</summary>
        private static void ApplyLook(EchoRecord record, Pawn pawn)
        {
            if (pawn.story == null) return;
            if (record.def.hairColor.a > 0f)
            {
                record.savedHairColor = pawn.story.HairColor;
                record.hairChanged = true;
                pawn.story.HairColor = record.def.hairColor;
            }
            if (record.def.bodyType != null && pawn.def == ThingDefOf.Human && pawn.DevelopmentalStage.Adult()
                && pawn.story.bodyType != record.def.bodyType)
            {
                record.savedBodyType = pawn.story.bodyType;
                pawn.story.bodyType = record.def.bodyType;
            }
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        }

        private static void RestoreLook(EchoRecord record, Pawn pawn)
        {
            if (pawn.story == null) return;
            if (record.hairChanged)
            {
                pawn.story.HairColor = record.savedHairColor;
                record.hairChanged = false;
            }
            if (record.savedBodyType != null)
            {
                pawn.story.bodyType = record.savedBodyType;
                record.savedBodyType = null;
            }
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        }

        // ---- abilities ----

        /// <summary>The record of a Host currently manifesting an Echo that grants this ability.</summary>
        public static EchoRecord ManifestedWith(Pawn pawn, AbilityDef ability)
        {
            EchoRecord record = GameComponent_Echoes.Get?.HostRecord(pawn);
            return record != null && record.manifested && record.def.abilities.Contains(ability) ? record : null;
        }
    }
}
