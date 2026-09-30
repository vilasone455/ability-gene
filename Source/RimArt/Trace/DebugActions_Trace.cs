using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Previews only: Trace On's four scenarios and Reinforcement, drawn 2 cells north of the chosen cell as the
    /// sketches place them, facing east. No pawn is drawn; the held weapons are, as the sketches' stand-ins for the
    /// game's own drawing (in game, a vanilla longsword and spear stand in for the lab's reference textures).
    /// </summary>
    public static class DebugActions_Trace
    {
        [RimArtDebug("Trace", "trace on: empty hand")]
        public static void TraceOnEmpty() => Play(TracePreview.EmptyHand);

        [RimArtDebug("Trace", "trace on: swap copy")]
        public static void TraceOnSwap() => Play(TracePreview.SwapCopy);

        [RimArtDebug("Trace", "trace on: real weapon")]
        public static void TraceOnReal() => Play(TracePreview.RealWeapon);

        [RimArtDebug("Trace", "trace on: downed")]
        public static void TraceOnDowned() => Play(TracePreview.Downed);

        [RimArtDebug("Trace", "reinforcement: run and hit")]
        public static void Reinforcement() => Play(TracePreview.Reinforcement);

        private static void Play(TracePreview play) =>
            Find.CurrentMap.GetComponent<MapComponent_TracePreview>().Play(UI.MouseCell(), play);
    }

    /// <summary>The first four are <see cref="TraceOnScenario"/> in order.</summary>
    public enum TracePreview { EmptyHand, SwapCopy, RealWeapon, Downed, Reinforcement }

    public sealed class MapComponent_TracePreview : MapComponent
    {
        public bool active;
        private TracePreview mode;
        private float seconds;
        private IntVec3 cell;

        public MapComponent_TracePreview(Map map) : base(map) { }

        public static float Duration(TracePreview play) =>
            play == TracePreview.Reinforcement ? TraceReinforcementTiming.Duration : TraceOnTiming.Duration((TraceOnScenario)play);

        public void Play(IntVec3 at, TracePreview play)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            mode = play;
            seconds = 0f;
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            Vector3 centre = cell.ToVector3Shifted();
            if (mode == TracePreview.Reinforcement) TraceReinforcementGraphics.DrawPreview(centre, seconds);
            else TraceOnGraphics.DrawPreview(centre, seconds, (TraceOnScenario)mode);
            if (seconds >= Duration(mode)) active = false;
        }
    }
}
