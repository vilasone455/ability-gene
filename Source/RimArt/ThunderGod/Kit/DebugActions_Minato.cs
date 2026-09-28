using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>The Minato kit's gameplay entries in the RimArts debug window; the picture previews are under "Flying Thunder God".</summary>
    public static class DebugActions_Minato
    {
        [RimArtDebug("Minato", "make pawn Minato's Host (manifested, full charge, a full kunai belt)", RimArtDebugKind.Pawn)]
        private static void MakeHost(Pawn pawn)
        {
            EchoRecord record = EchoUtility.ForceHost(MinatoDefOf.AG_Echo_Minato, pawn);
            if (record == null)
            {
                Messages.Message("Minato already has a Host.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            DebugActions_Echo.Fill();
            EchoUtility.Manifest(record);
            if (KunaiBelt.WornBy(pawn) == null && pawn.apparel != null)
                pawn.apparel.Wear((Apparel)ThingMaker.MakeThing(KunaiDefOf.AG_KunaiBelt), dropReplacedApparel: true);
        }

        [RimArtDebug("Minato", "stick one of Minato's kunai in the pawn", RimArtDebugKind.Pawn)]
        private static void Stick(Pawn pawn) => StickSealed(pawn);

        [RimArtDebug("Minato", "seal the pawn (sealing touch)", RimArtDebugKind.Pawn)]
        private static void Seal(Pawn pawn) => SealingTouch.Seal(pawn);

        [RimArtDebug("Minato", "plant one of Minato's kunai here", RimArtDebugKind.Cell)]
        private static void Plant()
        {
            Map map = Find.CurrentMap;
            IntVec3 cell = UI.MouseCell();
            if (!KunaiEmbedding.PlantKunai(cell.ToVector3Shifted(), Rand.Range(0f, 360f), map, true))
                KunaiEmbedding.DropKunai(cell, map, true);
        }

        /// <summary>One of Minato's kunai in the pawn's torso, as a hit would leave it (a small stab wound under it).</summary>
        public static bool StickSealed(Pawn pawn)
        {
            if (pawn?.health == null || pawn.Dead) return false;
            BodyPartRecord torso = pawn.RaceProps.body.corePart;
            Hediff_Injury wound = HediffMaker.MakeHediff(DamageDefOf.Stab.hediff, pawn, torso) as Hediff_Injury;
            if (wound != null)
            {
                wound.Severity = 1f;
                pawn.health.AddHediff(wound, torso);
            }
            var kunai = (Hediff_EmbeddedKunai)HediffMaker.MakeHediff(KunaiDefOf.AG_EmbeddedKunai, pawn, torso);
            kunai.wound = wound;
            kunai.sealedByMinato = true;
            pawn.health.AddHediff(kunai, torso);
            return true;
        }

        /// <summary>A sealed kunai lying flat on the cell (tests and the debug window).</summary>
        public static KunaiItem LaySealed(IntVec3 cell, Map map)
        {
            Thing item = KunaiEmbedding.MakeKunai(true);
            return GenSpawn.Spawn(item, cell, map) as KunaiItem;
        }
    }
}
