using Verse;

namespace RimArt
{
    public class HediffCompProperties_Vent : HediffCompProperties
    {
        /// <summary>
        /// How long the hole stays connected, in game ticks.
        ///
        /// Counted here rather than by HediffCompProperties_Disappears for the same reason the
        /// deferred plexus and the vector reflex count their own: a pawn running accelerated
        /// ticks its own hediffs several times per game tick, which would close the hole early.
        /// </summary>
        public int durationTicks = 1800;

        public HediffCompProperties_Vent()
        {
            compClass = typeof(HediffComp_Vent);
        }
    }

    /// <summary>
    /// The window in which the hole leads somewhere.
    ///
    /// The hole itself is permanent and is not this comp's business - it is a hediff on a part
    /// and it costs the carrier that part whether or not anything is connected. What this owns
    /// is the connection, which is the half that had to be a decision rather than a stat.
    /// </summary>
    public class HediffComp_Vent : HediffComp
    {
        private int ticksLeft = -1;
        private int lastGameTickProcessed = -1;

        public HediffCompProperties_Vent Props => (HediffCompProperties_Vent)props;

        /// <summary>Test hook only - see DebugActions_Involute.</summary>
        public void SetTicksLeft(int value)
        {
            ticksLeft = value;
        }

        public override void CompPostMake()
        {
            base.CompPostMake();
            ticksLeft = Props.durationTicks;
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            Pawn pawn = parent.pawn;
            if (pawn == null) return;

            // The connection is read from inside the damage worker, so it has to be reported
            // every pawn tick rather than looked up - see InvoluteRegistry.
            InvoluteRegistry.Report(pawn);

            int now = Find.TickManager.TicksGame;
            if (now == lastGameTickProcessed) return;
            lastGameTickProcessed = now;

            ticksLeft--;
            if (ticksLeft > 0) return;

            InvoluteRegistry.Drop(pawn);
            pawn.health.RemoveHediff(parent);
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            InvoluteRegistry.Drop(parent.pawn);
        }

        public override string CompTipStringExtra
        {
            get
            {
                return "AG_InvoluteVentedTip".Translate((ticksLeft / 60f).ToString("F0"));
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", -1);
            Scribe_Values.Look(ref lastGameTickProcessed, "lastGameTickProcessed", -1);
        }
    }
}
