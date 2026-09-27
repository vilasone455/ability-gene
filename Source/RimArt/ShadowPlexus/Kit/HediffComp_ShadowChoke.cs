using UnityEngine;
using Verse;

namespace RimArt
{
    public class HediffCompProperties_ShadowChoke : HediffCompProperties
    {
        /// <summary>How long the target stays out once the meter is full, before it starts draining.</summary>
        public int chokedOutTicks = 7200;
        /// <summary>Severity lost per second while no hands are on the throat.</summary>
        public float drainPerSecond = 0.05f;

        public HediffCompProperties_ShadowChoke()
        {
            compClass = typeof(HediffComp_ShadowChoke);
        }
    }

    /// <summary>
    /// Neck bind's suffocation meter on the held pawn: AG_ShadowChoked's severity. It only rises while
    /// <see cref="MapComponent_ShadowPlexus"/> feeds it (<see cref="Fed"/>) each tick the hands are
    /// closed. At <see cref="Full"/> the hediff's "choked out" stage caps Consciousness, which downs the
    /// pawn without killing it, for <see cref="HediffCompProperties_ShadowChoke.chokedOutTicks"/>; then,
    /// and whenever the hands come off before that, it drains at
    /// <see cref="HediffCompProperties_ShadowChoke.drainPerSecond"/> and goes when empty.
    ///
    /// Counted once per game tick, not once per comp tick, for the reason HediffComp_VectorStrain gives.
    /// </summary>
    public class HediffComp_ShadowChoke : HediffComp
    {
        /// <summary>The severity that is "choked out": just under 1 so float noise cannot keep the stage off.</summary>
        public const float Full = 0.999f;

        private int lastFedTick = -1;
        /// <summary>When the choked-out hold ends; -1 until the meter fills.</summary>
        public int holdUntilTick = -1;
        private int lastGameTickProcessed = -1;

        public HediffCompProperties_ShadowChoke Props => (HediffCompProperties_ShadowChoke)props;

        public bool ChokedOut => holdUntilTick >= 0 && Find.TickManager.TicksGame < holdUntilTick;

        /// <summary>The hands are closed this tick: no drain.</summary>
        public void Fed(int now) => lastFedTick = now;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead) return;
            int now = Find.TickManager.TicksGame;
            if (now == lastGameTickProcessed) return;
            lastGameTickProcessed = now;

            if (parent.Severity >= Full && holdUntilTick < 0) holdUntilTick = now + Props.chokedOutTicks;
            if (ChokedOut) return;
            if (now - lastFedTick <= 1) return;
            severityAdjustment -= Props.drainPerSecond / 60f * Mathf.Max(1, delta);
        }

        public override bool CompShouldRemove => parent.Severity <= 0f;

        public override string CompLabelInBracketsExtra => ChokedOut ? "AG_ShadowChokedOut".Translate().ToString() : null;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref lastFedTick, "lastFedTick", -1);
            Scribe_Values.Look(ref holdUntilTick, "holdUntilTick", -1);
        }
    }
}
