using System.Collections.Generic;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Testing the hole in a real fight is close to impossible on purpose: the pass-through only
    /// fires on the one part the hole is in, which is a few percent of incoming hits, and the
    /// carrier is being shot at while you wait for it. That is correct as balance and useless as
    /// a test loop.
    ///
    /// These take the combat out of it. Every one of them drives the same code path a bullet
    /// does - the prefix on ApplyDamageToPart - so a pass that works here works in a fight.
    /// </summary>
    public static class DebugActions_Involute
    {
        private const string Category = "Ability Genes";

        [DebugAction(Category, "Involute: name the hole part", actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void NameHolePart(Pawn pawn)
        {
            Gene_Involute gene = InvoluteUtility.GeneOf(pawn);
            if (gene == null)
            {
                Messages.Message(pawn.LabelShortCap + " has no involute organ.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            gene.EnsureHole();
            string part = gene.HolePart != null ? gene.HolePart.LabelCap.ToString() : "none";
            string coverage = gene.HolePart != null
                ? (gene.HolePart.coverageAbsWithChildren * 100f).ToString("F1") + "% of hits"
                : "-";
            string open = InvoluteUtility.IsOpen(gene) ? "connected" : "closed";
            string volume = gene.Volume != null ? "generated" : "not generated yet";

            Messages.Message(
                pawn.LabelShortCap + ": hole in " + part + " (" + coverage + "), " + open + ", volume " + volume,
                MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>
        /// Re-rolls the part so every band can be tried without generating a new pawn. The gene
        /// rolls once and only once in play, which is the right rule and the wrong one for a
        /// test session.
        /// </summary>
        [DebugAction(Category, "Involute: re-roll the hole part", actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RerollHolePart(Pawn pawn)
        {
            Gene_Involute gene = InvoluteUtility.GeneOf(pawn);
            if (gene == null) return;

            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = hediffs.Count - 1; i >= 0; i--)
            {
                if (hediffs[i].def == InvoluteDefOf.AG_InvoluteHole) pawn.health.RemoveHediff(hediffs[i]);
            }

            gene.ClearHolePart();
            gene.EnsureHole();
            NameHolePart(pawn);
        }

        /// <summary>
        /// Puts the hole wherever you point it, filter and all - which is the only way to reach
        /// the torso, and the reason this exists rather than another re-roll button.
        ///
        /// The roll draws from ResonanceUtility.CanRing, which throws out the body's core part
        /// and anything with a vital organ hanging off it, so no amount of re-rolling will ever
        /// land on a torso. That is deliberate: torso coverage is around 40% of incoming hits,
        /// which stops being a share of the fire and starts being immunity. It is exactly what
        /// you want for one test session and exactly what you do not want to ship.
        /// </summary>
        [DebugAction(Category, "Involute: move the hole to...", actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MoveHole(Pawn pawn)
        {
            Gene_Involute gene = InvoluteUtility.GeneOf(pawn);
            if (gene == null)
            {
                Messages.Message(pawn.LabelShortCap + " has no involute organ.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            List<BodyPartRecord> parts = new List<BodyPartRecord>();
            List<BodyPartRecord> all = pawn.RaceProps.body.AllParts;
            for (int i = 0; i < all.Count; i++)
            {
                if (pawn.health.hediffSet.PartIsMissing(all[i])) continue;
                parts.Add(all[i]);
            }

            parts.SortByDescending(p => p.coverageAbsWithChildren);

            Dialog_DebugOptionListLister.ShowSimpleDebugMenu(
                parts,
                p => p.LabelCap + "  -  " + (p.coverageAbsWithChildren * 100f).ToString("F1") + "% of hits"
                     + (ResonanceUtility.CanRing(pawn, p) ? "" : "  (never rolled)"),
                p =>
                {
                    gene.SetHolePart(p);
                    NameHolePart(pawn);
                });
        }

        /// <summary>Connects the hole for long enough to run a whole test without re-casting.</summary>
        [DebugAction(Category, "Involute: connect hole (10 min)", actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ConnectHole(Pawn pawn)
        {
            Gene_Involute gene = InvoluteUtility.GeneOf(pawn);
            if (gene == null) return;

            gene.EnsureHole();
            gene.EnsureVolume();

            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(InvoluteDefOf.AG_InvoluteVented);
            if (existing != null) pawn.health.RemoveHediff(existing);

            Hediff vented = HediffMaker.MakeHediff(InvoluteDefOf.AG_InvoluteVented, pawn);
            HediffComp_Vent comp = (vented as HediffWithComps)?.TryGetComp<HediffComp_Vent>();
            if (comp != null) comp.SetTicksLeft(36000);

            pawn.health.AddHediff(vented);
            InvoluteRegistry.Report(pawn);

            Messages.Message(pawn.LabelShortCap + "'s hole is connected for ten minutes.",
                MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>
        /// The one that actually tests the mechanic. Fires a rifle round straight at the hole,
        /// with the part named rather than rolled, so the pass-through is guaranteed to be the
        /// branch under test instead of a few percent chance of being it.
        ///
        /// If this works you should see the flash on the carrier, no injury on them, and a round
        /// crossing the volume when you switch to it.
        /// </summary>
        [DebugAction(Category, "Involute: put a round through the hole", actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RoundThroughHole(Pawn pawn)
        {
            Shoot(pawn, true);
        }

        /// <summary>
        /// The same round with the part left to the engine, which is what a real shot does. Run
        /// this twenty times and roughly the part's coverage share should pass through - it is
        /// the check that the single-roll write-back in the prefix did not skew the odds.
        /// </summary>
        [DebugAction(Category, "Involute: fire a round, part unrolled", actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RoundUnrolled(Pawn pawn)
        {
            Shoot(pawn, false);
        }

        private static void Shoot(Pawn pawn, bool forcePart)
        {
            Gene_Involute gene = InvoluteUtility.GeneOf(pawn);
            if (gene == null) return;

            gene.EnsureHole();

            ThingDef weapon = DefDatabase<ThingDef>.GetNamedSilentFail("Gun_AssaultRifle");
            BodyPartRecord part = forcePart ? gene.HolePart : null;

            DamageInfo dinfo = new DamageInfo(DamageDefOf.Bullet, 12f, 0.15f, -1f, null, part, weapon);
            pawn.TakeDamage(dinfo);
        }

        /// <summary>Look at the room. Generates it if this carrier has never opened the hole.</summary>
        [DebugAction(Category, "Involute: go to the volume", actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void GoToVolume(Pawn pawn)
        {
            Gene_Involute gene = InvoluteUtility.GeneOf(pawn);
            if (gene == null) return;

            Map volume = gene.EnsureVolume();
            if (volume == null)
            {
                Messages.Message("The volume could not be generated - check the log.",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            CameraJumper.TryJump(new GlobalTargetInfo(InvoluteUtility.MouthCell(volume), volume));
        }
    }
}
