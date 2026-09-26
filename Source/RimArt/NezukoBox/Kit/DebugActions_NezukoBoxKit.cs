using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>Test shortcuts for the real box. The drawing previews are in DebugActions_NezukoBox.</summary>
    public static class DebugActions_NezukoBoxKit
    {
        /// <summary>Puts an empty box on the clicked pawn's back.</summary>
        [RimArtDebug("Nezuko's Box", "give box to pawn", RimArtDebugKind.Pawn)]
        private static void Give(Pawn pawn)
        {
            if (pawn.apparel == null)
            {
                Messages.Message("this pawn cannot wear apparel", MessageTypeDefOf.RejectInput, false);
                return;
            }
            pawn.apparel.Wear((Apparel)ThingMaker.MakeThing(NezukoBoxDefOf.AG_NezukoBox), false);
        }

        /// <summary>Drops an empty box on the clicked cell.</summary>
        [RimArtDebug("Nezuko's Box", "spawn box on ground")]
        private static void Spawn() =>
            GenSpawn.Spawn(ThingMaker.MakeThing(NezukoBoxDefOf.AG_NezukoBox), UI.MouseCell(), Find.CurrentMap);

        /// <summary>Puts a new colonist straight into the clicked wearer's box, without the picture.</summary>
        [RimArtDebug("Nezuko's Box", "fill box with a new colonist", RimArtDebugKind.Pawn)]
        private static void Fill(Pawn pawn)
        {
            CompNezukoBox box = CompNezukoBox.WornBy(pawn);
            if (box == null || box.Full) return;
            Pawn sleeper = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer, forceNoGear: true));
            GenSpawn.Spawn(sleeper, CompNezukoBox.OutsideDoor(pawn), pawn.Map);
            box.Take(sleeper);
        }

        /// <summary>Moves the time inside on by 11.9 hours: the pawn steps out about 6 minutes later.</summary>
        [RimArtDebug("Nezuko's Box", "box time +11.9 h", RimArtDebugKind.Pawn)]
        private static void Age(Pawn pawn) => CompNezukoBox.WornBy(pawn)?.AgeInside((int)(11.9f * GenDate.TicksPerHour));

        /// <summary>Fills the rest of the pawn inside: it steps out within the hour.</summary>
        [RimArtDebug("Nezuko's Box", "rest the sleeper fully", RimArtDebugKind.Pawn)]
        private static void Rest(Pawn pawn)
        {
            Pawn sleeper = CompNezukoBox.WornBy(pawn)?.Sleeper;
            if (sleeper?.needs?.rest != null) sleeper.needs.rest.CurLevel = 1f;
        }
    }
}
