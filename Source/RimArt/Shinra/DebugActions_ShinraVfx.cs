using Verse;

namespace RimArt
{
    /// <summary>
    /// The picture on the clicked cell, with no pawn: the lab sketch's timeline (pain-shinra-tensei.js) for a
    /// 3 s hold (4 cells), a 1.5 s hold (3 cells) and a tap (2.5 cells). The recorder plays each for the lab.
    /// </summary>
    public static class DebugActions_ShinraVfx
    {
        [RimArtDebug("Shinra Tensei", "VFX preview")]
        private static void Preview() => Play(3f, 1f);

        [RimArtDebug("Shinra Tensei", "VFX 1.5 s hold")]
        private static void Hold() => Play(1.5f, 1f);

        [RimArtDebug("Shinra Tensei", "VFX tap")]
        private static void Tap() => Play(0f, 1f);

        [RimArtDebug("Shinra Tensei", "VFX slow motion")]
        private static void SlowMotion() => Play(3f, 0.25f);

        [RimArtDebug("Shinra Tensei", "VFX frozen peak")]
        private static void FrozenPeak() => Play(3f, 0f, 0.3f);

        [RimArtDebug("Shinra Tensei", "clear VFX", RimArtDebugKind.Now)]
        private static void Clear() => Find.CurrentMap?.GetComponent<MapComponent_ShinraVfx>()?.Clear();

        private static void Play(float held, float speed, float freezeAt = -1f) =>
            Find.CurrentMap?.GetComponent<MapComponent_ShinraVfx>()?.Preview(UI.MouseCell(), held, speed, freezeAt);
    }
}
