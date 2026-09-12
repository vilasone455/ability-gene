using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Projecting a decoy without the beacon, the throw, the research or the recipe.
    ///
    /// The point of this action is that the one thing in this feature that can fail outright is
    /// whether enemy AI takes the bait, and none of the machinery above changes that answer. A
    /// decoy spawned by hand in front of a raid proves or disproves the whole design in about
    /// twenty seconds, which is why it exists before any of the rest of it.
    /// </summary>
    public static class DebugActions_Mimic
    {
        private const string Category = "RimArts";

        [DebugAction(Category, "Mimic: project decoy here", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ProjectDecoy()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;

            IntVec3 cell = UI.MouseCell();
            if (!cell.InBounds(map) || !cell.Standable(map)) return;

            // Selected colonist first, so the tester chooses who is copied; otherwise anybody.
            Pawn source = Find.Selector.SelectedPawns.FirstOrDefault(p => p.Map == map)
                          ?? map.mapPawns.FreeColonistsSpawned.FirstOrDefault();

            if (source == null)
            {
                Messages.Message("no colonist on this map to copy", MessageTypeDefOf.RejectInput, false);
                return;
            }

            MimicDecoy decoy = MimicProjection.Spawn(source, cell, map);
            if (decoy == null)
                Messages.Message("no room to project there", MessageTypeDefOf.RejectInput, false);
        }
    }
}
