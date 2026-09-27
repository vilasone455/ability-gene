using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>The Goku kit's gameplay entries in the RimArts debug window; the picture previews are in DebugActions_Goku.</summary>
    public static class DebugActions_GokuKit
    {
        [RimArtDebug("Goku", "make pawn Goku's Host (manifested, full charge)", RimArtDebugKind.Pawn)]
        private static void MakeHost(Pawn pawn)
        {
            EchoRecord record = EchoUtility.ForceHost(GokuDefOf.AG_Echo_Goku, pawn);
            if (record == null)
            {
                Messages.Message("Goku already has a Host.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            DebugActions_Echo.Fill();
            EchoUtility.Manifest(record);
        }

        [RimArtDebug("Goku", "cancel Goku casts (refund)", RimArtDebugKind.Now)]
        private static void CancelCasts() => GameComponent_Goku.Instance?.ResetForTests();

        [RimArtDebug("Goku", "all colonists lend energy", RimArtDebugKind.Now)]
        private static void AllLend()
        {
            Map map = Find.CurrentMap;
            SpiritBombCast bomb = map == null ? null : GameComponent_Goku.Instance?.SpiritBombOn(map);
            if (bomb == null)
            {
                Messages.Message("No Spirit Bomb is being channelled on this map.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
            {
                if (pawn == bomb.caster || pawn.Downed || SpiritBombCast.IsLending(pawn, bomb.caster)) continue;
                Job job = JobMaker.MakeJob(GokuDefOf.AG_GokuLend, bomb.caster);
                job.playerForced = true;
                pawn.jobs.TryTakeOrderedJob(job, pawn.Drafted ? JobTag.DraftedOrder : JobTag.Misc);
            }
        }

        [RimArtDebug("Goku", "downed recovery +1", RimArtDebugKind.Pawn)]
        private static void Recovery(Pawn pawn)
        {
            PawnDeeds deeds = GameComponent_Echoes.Get?.DeedsFor(pawn, true);
            if (deeds != null) deeds.downedRecoveries++;
        }
    }
}
