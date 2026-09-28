using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimArt
{
    /// <summary>Obito's kit in the RimArts debug window. Echo's "fill charge" also applies.</summary>
    public static class DebugActions_Obito
    {
        [RimArtDebug("Obito", "make Host + manifest", RimArtDebugKind.Pawn)]
        private static void MakeHost(Pawn pawn)
        {
            EchoDef obito = ObitoDefOf.AG_Echo_Obito;
            EchoRecord record = EchoUtility.ForceHost(obito, pawn) ?? GameComponent_Echoes.Get.HostRecord(pawn);
            if (record == null || record.def != obito)
            {
                Messages.Message(pawn.LabelShortCap + " cannot host Obito (the seat is taken or the pawn hosts another Echo).",
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            EchoUtility.EnsureAwakenGenes(obito, pawn);
            DebugActions_Echo.Fill();
            EchoUtility.Manifest(record);
        }

        [RimArtDebug("Obito", "fill the phase pool", RimArtDebugKind.Pawn)]
        private static void FillPool(Pawn pawn) => InvoluteUtility.GeneOf(pawn)?.SetPoolSeconds(999f);

        [RimArtDebug("Obito", "phase on / off", RimArtDebugKind.Pawn)]
        private static void TogglePhase(Pawn pawn) => InvoluteUtility.GeneOf(pawn)?.TogglePhase();

        /// <summary>Shoots the pawn with a rifle round from 6 cells east, through the same damage path a real shot takes.</summary>
        [RimArtDebug("Obito", "shoot at the pawn", RimArtDebugKind.Pawn)]
        private static void Shoot(Pawn pawn)
        {
            ThingDef gun = DefDatabase<ThingDef>.GetNamedSilentFail("Gun_AssaultRifle");
            pawn.TakeDamage(new DamageInfo(DamageDefOf.Bullet, 11f, 0.16f, 270f, null, null, gun));
        }

        [RimArtDebug("Obito", "into the dimension (no cost)", RimArtDebugKind.Pawn)]
        private static void GoIn(Pawn pawn)
        {
            Gene_Involute gene = InvoluteUtility.GeneOf(pawn);
            if (gene == null || gene.Inside) return;
            gene.Enter();
        }

        [RimArtDebug("Obito", "out of the dimension where he went in", RimArtDebugKind.Pawn)]
        private static void GoOut(Pawn pawn) => InvoluteUtility.GeneOf(pawn)?.ExitToWhereHeLeft();

        /// <summary>The selected Obito absorbs the pawn or item under the mouse at once (no cost, no touch).</summary>
        [RimArtDebug("Obito", "absorb the thing here")]
        private static void AbsorbHere()
        {
            Map map = Find.CurrentMap;
            IntVec3 cell = UI.MouseCell();
            Thing target = (Thing)cell.GetFirstPawn(map) ?? cell.GetFirstItem(map);
            Pawn obito = Find.Selector.SelectedPawns.FirstOrDefault(p => InvoluteUtility.GeneOf(p) != null);
            if (target == null || obito == null)
            {
                Messages.Message("Select a pawn with Kamui and put the mouse on a pawn or item.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            InvoluteUtility.GeneOf(obito).Absorb(target);
        }

        [RimArtDebug("Obito", "list the dimension's contents", RimArtDebugKind.Pawn)]
        private static void List(Pawn pawn)
        {
            Gene_Involute gene = InvoluteUtility.GeneOf(pawn);
            if (gene == null) return;
            string stored = string.Join(", ", gene.Stored.Select(t => t.LabelShort));
            Map volume = gene.Volume;
            MapComponent_KamuiDimension kamui = volume?.GetComponent<MapComponent_KamuiDimension>();
            Messages.Message(pawn.LabelShortCap + ": dimension " + (volume == null ? "not built" : "seed " + kamui?.seed)
                + ", inside " + gene.Inside + ", phase " + gene.Phase + " pool " + (gene.PoolTicks / 60f).ToString("0.0") + " s"
                + ", stored: " + (stored.Length > 0 ? stored : "nothing"), MessageTypeDefOf.NeutralEvent, false);
        }

        [RimArtDebug("Obito", "go to the dimension (camera)", RimArtDebugKind.Pawn)]
        private static void Look(Pawn pawn)
        {
            Map volume = InvoluteUtility.GeneOf(pawn)?.EnsureVolume();
            if (volume != null) CameraJumper.TryJump(new GlobalTargetInfo(InvoluteUtility.MouthCell(volume), volume));
        }
    }
}
