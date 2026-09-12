using LudeonTK;
using Verse;

namespace RimArt
{
    public static class DebugActions_ShinraVfx
    {
        [DebugAction("RimArts", "Shinra Tensei: VFX preview", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Preview() => Play(1f);

        [DebugAction("RimArts", "Shinra Tensei: VFX slow motion", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SlowMotion() => Play(0.25f);

        [DebugAction("RimArts", "Shinra Tensei: VFX frozen peak", actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void FrozenPeak() => Play(0f, true);

        [DebugAction("RimArts", "Shinra Tensei: clear VFX", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Clear() => Find.CurrentMap?.GetComponent<MapComponent_ShinraVfx>()?.Clear();

        private static void Play(float speed, bool freeze = false) =>
            Find.CurrentMap?.GetComponent<MapComponent_ShinraVfx>()?.Preview(UI.MouseCell(), speed, freeze);
    }
}
