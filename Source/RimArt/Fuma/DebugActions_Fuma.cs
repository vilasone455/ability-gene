using Verse;

namespace RimArt
{
    public static class DebugActions_Fuma
    {
        [RimArtDebug("Fūma Shuriken", "spawn")]
        private static void Spawn() => GenPlace.TryPlaceThing(ThingMaker.MakeThing(FumaDefOf.AG_FumaShuriken),
            UI.MouseCell(), Find.CurrentMap, ThingPlaceMode.Near);
    }
}
