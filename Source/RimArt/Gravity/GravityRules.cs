using System;

namespace RimArt
{
    // Shape and logic constants. Balance numbers are XML fields on CompProperties_AbilityGravityWell.
    public static class GravityRules
    {
        // The opening matches the gather clip's first 0.5 s (GravityCast seeks ticks / 60).
        public const int OpeningTicks = 30;
        // An item in a well commits its map cell at most every 15 ticks and is drawn at its exact
        // position in between. Each commit re-prints the Things layer of its map section.
        public const int ItemCommitTicks = 15;
        // A dragged pawn that is off its path asks for a new path at most every 20 ticks.
        public const int RepathTicks = 20;
        // Clear-line answers per cell are kept for 30 ticks: a door or wall change shows within 0.5 s.
        public const int ClearRefreshTicks = 30;
        // The drift target is chosen again every 15 ticks.
        public const int DriftPickTicks = 15;
        public static float Clamp(float value) => Math.Max(0f, Math.Min(1f, value));
    }

    public enum GravityPhase { Opening, Channel, Finished }

    public sealed class GravityClock
    {
        public GravityPhase phase;
        public int ticks;
        public bool activated, imploded;
        // True once the channel has run durationTicks.
        public bool Tick(int durationTicks)
        {
            if (phase == GravityPhase.Finished) return false;
            ticks++;
            if (phase == GravityPhase.Opening && ticks >= GravityRules.OpeningTicks)
            { phase = GravityPhase.Channel; ticks = 0; activated = true; }
            return phase == GravityPhase.Channel && ticks >= durationTicks;
        }
        public bool Finish(bool implode)
        {
            if (phase == GravityPhase.Finished) return false;
            imploded = implode && activated;
            phase = GravityPhase.Finished;
            return imploded;
        }
    }
}
