using LudeonTK;
using Verse;

namespace RimArt
{
    public static class DebugActions_Fuma
    {
        [DebugAction("RimArts", "Spawn Fūma Shuriken", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Spawn() => GenPlace.TryPlaceThing(ThingMaker.MakeThing(FumaDefOf.AG_FumaShuriken),
            UI.MouseCell(), Find.CurrentMap, ThingPlaceMode.Near);
    }
}
