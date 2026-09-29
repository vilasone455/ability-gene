using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Debug window kit "Sato": make a Host ready to play (manifested, full pool), end a Reset now, and start one
    /// by a kill or an explosion without setting it up by hand. The picture previews are in
    /// Source/RimArt/Sato/DebugActions_*Preview.cs.
    /// </summary>
    public static class DebugActions_Sato
    {
        [RimArtDebug("Sato", "make Host (manifested, full pool)", RimArtDebugKind.Pawn)]
        private static void MakeHost(Pawn pawn)
        {
            if (pawn?.RaceProps?.Humanlike != true) return;
            EchoRecord record = EchoUtility.ForceHost(SatoDefOf.AG_Echo_Sato, pawn);
            if (record == null) return;
            GameComponent_Echoes.Get.charge = GameComponent_Echoes.Get.MaxCharge;
            if (!record.manifested) EchoUtility.Manifest(record);
        }

        [RimArtDebug("Sato", "kill (starts a Reset)", RimArtDebugKind.Pawn)]
        private static void Kill(Pawn pawn)
        {
            if (AjinReset.IsAjin(pawn)) pawn.Kill(null);
        }

        [RimArtDebug("Sato", "blow up (explosion Reset)", RimArtDebugKind.Pawn)]
        private static void BlowUp(Pawn pawn)
        {
            if (pawn.Spawned) GenExplosion.DoExplosion(pawn.Position, pawn.Map, 1.9f, DamageDefOf.Bomb, null, 20);
        }

        /// <summary>Every Reset in progress ends on the next tick, held ones (body destroyed) included.</summary>
        [RimArtDebug("Sato", "end every Reset now")]
        private static void RiseAll()
        {
            GameComponent_Sato sato = GameComponent_Sato.Instance;
            if (sato == null) return;
            foreach (Pawn pawn in sato.Resetting)
            {
                Hediff_AjinReset reset = AjinReset.Resetting(pawn);
                if (reset != null) reset.riseTick = Find.TickManager.TicksGame + 1;
            }
        }
    }
}
