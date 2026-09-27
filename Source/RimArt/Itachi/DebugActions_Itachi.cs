using System.Linq;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>Itachi's kit in the RimArts debug window. Echo's "fill charge" and Dispersal's "refill the plexus" also apply.</summary>
    public static class DebugActions_Itachi
    {
        private static EchoDef Itachi => DefDatabase<EchoDef>.GetNamed("AG_Echo_Itachi");

        [RimArtDebug("Itachi", "make Host + manifest", RimArtDebugKind.Pawn)]
        private static void MakeHost(Pawn pawn)
        {
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            EchoRecord record = EchoUtility.ForceHost(Itachi, pawn) ?? echoes.HostRecord(pawn);
            if (record == null || record.def != Itachi)
            {
                Messages.Message(pawn.LabelShortCap + " cannot host Itachi (the seat is taken or the pawn hosts another Echo).",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            DebugActions_Echo.Fill();
            EchoUtility.Manifest(record);
        }

        [RimArtDebug("Itachi", "false face (no cost, no cooldown)", RimArtDebugKind.Pawn)]
        private static void FalseFace(Pawn pawn)
        {
            CompProperties_AbilityFalseFace props = ItachiDefOf.AG_ItachiFalseFace.comps
                .OfType<CompProperties_AbilityFalseFace>().FirstOrDefault();
            int caught = FalseFaceCast.Cast(pawn, props?.radius ?? 15f, props?.humanlikeOnly ?? true);
            Messages.Message(pawn.LabelShortCap + ": False Face caught " + caught + ".", MessageTypeDefOf.NeutralEvent, false);
        }

        [RimArtDebug("Itachi", "susanoo 12 s (no cost)", RimArtDebugKind.Pawn)]
        private static void Susanoo(Pawn pawn)
        {
            if (pawn.health.hediffSet.HasHediff(ItachiDefOf.AG_Susanoo)) return;
            Hediff hediff = HediffMaker.MakeHediff(ItachiDefOf.AG_Susanoo, pawn);
            HediffComp_Disappears timer = hediff.TryGetComp<HediffComp_Disappears>();
            if (timer != null)
                timer.ticksToDisappear = ItachiDefOf.AG_ItachiSusanoo.GetStatValueAbstract(StatDefOf.Ability_Duration).SecondsToTicks();
            pawn.health.AddHediff(hediff);
        }

        /// <summary>Cuts the pawn until it is at 30 % health or less, without downing it, so the seal can be tried.</summary>
        [RimArtDebug("Itachi", "weaken to 30 % health", RimArtDebugKind.Pawn)]
        private static void Weaken(Pawn pawn)
        {
            for (int i = 0; i < 60 && !pawn.Dead && !pawn.Downed
                 && pawn.health.summaryHealth.SummaryHealthPercent > 0.3f; i++)
                pawn.TakeDamage(new DamageInfo(DamageDefOf.Cut, 4f, 999f));
            Messages.Message(pawn.LabelShortCap + " is at " + (pawn.health.summaryHealth.SummaryHealthPercent * 100f).ToString("0") + " %.",
                MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>The selected Host stabs the pawn under the mouse with the Totsuka Blade.</summary>
        [RimArtDebug("Itachi", "totsuka stab the pawn here")]
        private static void Stab()
        {
            Map map = Find.CurrentMap;
            Pawn target = UI.MouseCell().GetFirstPawn(map);
            if (target == null) return;
            foreach (Pawn selected in Find.Selector.SelectedPawns)
            {
                HediffComp_Susanoo comp = SusanooRegistry.HolderFor(selected);
                if (comp == null) continue;
                comp.Stab(target);
                return;
            }
            Messages.Message("Select a pawn with a Susanoo up first.", MessageTypeDefOf.RejectInput, false);
        }
    }
}
