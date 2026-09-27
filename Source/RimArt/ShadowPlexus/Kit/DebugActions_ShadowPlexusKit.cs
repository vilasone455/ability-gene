using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>Shikamaru's kit, for play tests. The pictures' previews are DebugActions_ShadowPlexus.</summary>
    public static class DebugActions_ShadowPlexusKit
    {
        [RimArtDebug("Shadow Plexus", "make Shikamaru (Host + manifest)", RimArtDebugKind.Pawn)]
        private static void MakeShikamaru(Pawn pawn)
        {
            EchoRecord record = EchoUtility.ForceHost(ShadowPlexusDefOf.AG_Echo_Shikamaru, pawn);
            if (record != null && !record.manifested) EchoUtility.Manifest(record);
        }

        [RimArtDebug("Shadow Plexus", "light level here")]
        private static void LightHere()
        {
            Map map = Find.CurrentMap;
            IntVec3 cell = UI.MouseCell();
            if (map == null || !cell.InBounds(map)) return;
            float level = ShadowLight.Level(map, cell);
            Messages.Message("Light at " + cell + ": " + Mathf.RoundToInt(level * 100f) + " % (sky " + map.skyManager.CurSkyGlow.ToString("0.00")
                + (map.roofGrid.Roofed(cell) ? ", roofed" : "") + ", lamps " + map.glowGrid.GroundGlowAt(cell, false, true).ToString("0.00")
                + (ShadowLight.Smoky(map, cell) ? ", smoke" : "") + "); imitation reaches " + ShadowLight.Reach(19.9f, map, cell).ToString("0.#"),
                MessageTypeDefOf.NeutralEvent, false);
        }

        [RimArtDebug("Shadow Plexus", "release every hold", RimArtDebugKind.Now)]
        private static void ReleaseAll() => MapComponent_ShadowPlexus.Of(Find.CurrentMap)?.ReleaseAll();

        [RimArtDebug("Shadow Plexus", "light override: full everywhere", RimArtDebugKind.Now)]
        private static void FullLight() => ShadowLight.levelForTests = _ => 1f;

        [RimArtDebug("Shadow Plexus", "light override: clear", RimArtDebugKind.Now)]
        private static void ClearLight() => ShadowLight.levelForTests = null;
    }
}
