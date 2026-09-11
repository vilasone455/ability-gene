using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public class HediffCompProperties_VectorSurge : HediffCompProperties
    {
        /// <summary>
        /// Lifetime in game ticks, counted here rather than by a Disappears comp for the same
        /// reason the time lattice counts its own: a pawn running accelerated ticks its hediffs
        /// several times per game tick, which would expire this early for anyone also carrying a
        /// neural accelerator.
        ///
        /// Game ticks, not seconds of wall clock. At quarter rate 300 of them is five seconds of
        /// game time and about twenty of real time, which is the number that actually matters to
        /// the player holding the mouse.
        /// </summary>
        public int durationTicks = 300;

        /// <summary>
        /// Brain strain charged once, when the surge opens.
        ///
        /// Smaller than any edit, because it buys time rather than doing anything to the world -
        /// but not free, because a reflex that costs nothing is one there is never a reason to
        /// turn off. If the charge is what tips the carrier into overload they go down with the
        /// surge still running, which is the honest outcome of spending a reflex you could not
        /// afford.
        /// </summary>
        public float strainCost = 0.10f;

        public HediffCompProperties_VectorSurge()
        {
            compClass = typeof(HediffComp_VectorSurge);
        }
    }

    /// <summary>
    /// Holds the surge open and reports the carrier to <see cref="VectorSurgeRegistry"/>. The
    /// slowing itself lives in the TickRateMultiplier postfix; this comp only decides who is
    /// surging and for how long.
    ///
    /// Worth being plain about what this does and does not do. It changes no in-game
    /// relationship at all: the bullet still crosses a cell in the same number of ticks, the
    /// carrier still walks at the same cells per tick, and every cooldown, decay and duration in
    /// the mod is counted in game ticks and so is untouched. What changes is how much real time
    /// one game tick takes, which is to say how long the player has to look at it. The fiction
    /// carries that honestly - the carrier's perception is what sped up - but the effect is on
    /// the person holding the mouse, not on the pawn.
    /// </summary>
    public class HediffComp_VectorSurge : HediffComp
    {
        private int ticksLeft = -1;
        private int lastGameTickProcessed = -1;
        private static Texture2D dropIcon;

        public HediffCompProperties_VectorSurge Props => (HediffCompProperties_VectorSurge)props;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);

            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead)
            {
                VectorSurgeRegistry.Drop(this);
                return;
            }

            VectorSurgeRegistry.Report(this);

            // Everything below is rate-sensitive: an accelerated pawn runs this comp several
            // times inside one game tick, which would burn the duration at the multiplier.
            int now = Find.TickManager.TicksGame;
            if (now == lastGameTickProcessed) return;
            lastGameTickProcessed = now;

            if (ticksLeft < 0)
            {
                ticksLeft = Props.durationTicks;
                VectorStrain.Add(pawn, Props.strainCost);
                Messages.Message("AG_VectorSurgeUp".Translate(pawn.LabelShort),
                    pawn, MessageTypeDefOf.NeutralEvent, false);
            }

            ticksLeft -= Mathf.Max(1, delta);
            if (ticksLeft > 0) return;

            VectorSurgeRegistry.Drop(this);
            pawn.health.RemoveHediff(parent);
        }

        /// <summary>
        /// Dropping it early matters more here than for most held effects. Twenty seconds of
        /// real time is a long while to sit through once the shooting has stopped, and a player
        /// who has finished deciding should not have to wait out the reflex to get their game
        /// back at speed.
        /// </summary>
        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            Pawn pawn = Pawn;
            if (pawn == null || !pawn.IsColonistPlayerControlled) yield break;

            if (dropIcon == null)
            {
                // Resolved from the draw path, which is always the main thread.
                dropIcon = ContentFinder<Texture2D>.Get("UI/Abilities/MechSmokepop", false);
            }

            yield return new Command_Action
            {
                defaultLabel = "AG_VectorSurgeDropLabel".Translate(),
                defaultDesc = "AG_VectorSurgeDropDesc".Translate(),
                icon = dropIcon,
                action = delegate
                {
                    VectorSurgeRegistry.Drop(this);
                    pawn.health.RemoveHediff(parent);
                }
            };
        }

        public override string CompTipStringExtra =>
            ticksLeft > 0 ? "AG_VectorSurgeTip".Translate(ticksLeft.ToStringSecondsFromTicks()) : null;

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            VectorSurgeRegistry.Drop(this);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", -1);
        }
    }
}
