using Verse;

namespace RimArt
{
    public static class DebugActions_ShinraVfx
    {
        [RimArtDebug("Shinra Tensei", "VFX preview")]
        private static void Preview() => Play(1f);

        [RimArtDebug("Shinra Tensei", "VFX slow motion")]
        private static void SlowMotion() => Play(0.25f);

        [RimArtDebug("Shinra Tensei", "VFX frozen peak")]
        private static void FrozenPeak() => Play(0f, true);

        [RimArtDebug("Shinra Tensei", "clear VFX", RimArtDebugKind.Now)]
        private static void Clear() => Find.CurrentMap?.GetComponent<MapComponent_ShinraVfx>()?.Clear();

        private static void Play(float speed, bool freeze = false) =>
            Find.CurrentMap?.GetComponent<MapComponent_ShinraVfx>()?.Preview(UI.MouseCell(), speed, freeze);
    }
}
