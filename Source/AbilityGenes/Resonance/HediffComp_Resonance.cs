using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace AbilityGenes
{
    public class HediffCompProperties_Resonance : HediffCompProperties
    {
        /// <summary>
        /// Lifetime in game ticks, counted here rather than by HediffCompProperties_Disappears
        /// for the same reason the vector reflex does it: a pawn running accelerated ticks its
        /// own hediffs several times per game tick, which would expire a Disappears comp early.
        /// </summary>
        public int durationTicks = 1800;

        /// <summary>
        /// How long one struck part keeps its note, in game ticks. This is the whole difficulty
        /// of the ability. Long enough and every part on the field is live at once and the
        /// second blow is guaranteed; short enough and it is sustained pressure on one body,
        /// which is what it is for. Five seconds is about two or three swings.
        /// </summary>
        public int resonanceTicks = 300;

        /// <summary>
        /// Whether the carrier rings too. This is the cost, and turning it off is a large
        /// balance swing rather than a detail - hence a field, the way banWeapons is one on the
        /// anchor organ.
        /// </summary>
        public bool selfResonance = true;

        /// <summary>What the note is delivered as. Crush reads as a part shaking itself apart.</summary>
        public DamageDef damageDef;

        public HediffCompProperties_Resonance()
        {
            compClass = typeof(HediffComp_Resonance);
        }
    }

    /// <summary>
    /// Holds the note open and owns every part currently ringing because of it. The decision of
    /// what happens on a given blow lives in <see cref="Struck"/>; the postfix on
    /// Thing.TakeDamage only decides which blows get to ask.
    ///
    /// Live notes are deliberately not saved. They last five seconds, and a BodyPartRecord is
    /// resolved by walking a body rather than by an id, so persisting them would cost a
    /// resolver and a class of load-order bugs to buy back a window shorter than the pause
    /// between clicking save and the game coming back.
    /// </summary>
    public class HediffComp_Resonance : HediffComp
    {
        private struct Note
        {
            public Pawn pawn;
            public BodyPartRecord part;
            public int expires;
        }

        private int ticksLeft = -1;
        private int lastGameTickProcessed = -1;

        private readonly List<Note> live = new List<Note>();

        public HediffCompProperties_Resonance Props => (HediffCompProperties_Resonance)props;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);

            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead)
            {
                Stop();
                return;
            }

            ResonanceRegistry.Report(this);

            // Everything below is rate-sensitive: an accelerated pawn runs this comp several
            // times inside one game tick, which would burn both clocks at the multiplier.
            int now = Find.TickManager.TicksGame;
            if (now == lastGameTickProcessed) return;
            lastGameTickProcessed = now;

            if (ticksLeft < 0)
            {
                ticksLeft = Props.durationTicks;
                Messages.Message("AG_ResonanceUp".Translate(pawn.LabelShort),
                    pawn, MessageTypeDefOf.NeutralEvent, false);
            }

            Prune(now);

            ticksLeft -= Mathf.Max(1, delta);
            if (ticksLeft <= 0)
            {
                Stop();
                pawn.health.RemoveHediff(parent);
            }
        }

        /// <summary>
        /// One landed melee blow, already resolved, on a part the engine has named.
        ///
        /// Either the part was already ringing from an earlier blow and this one arrives in
        /// phase - in which case it comes off - or it was not, and now it is. Nothing else
        /// happens either way: the blow itself has already been applied in full by the time
        /// this is reached, so a part that cannot ring simply takes an ordinary hit.
        /// </summary>
        public void Struck(Pawn victim, BodyPartRecord part)
        {
            if (victim == null || victim.Dead || part == null) return;
            if (!ResonanceUtility.CanRing(victim, part)) return;

            int now = Find.TickManager.TicksGame;

            // Any existing entry for this exact part is taken out of the list either way. A live
            // one is the second blow arriving in phase; a stale one is a note that already rang
            // itself out, and leaving it behind would let the same part accumulate entries until
            // one of the old ones fired by accident.
            for (int i = live.Count - 1; i >= 0; i--)
            {
                Note note = live[i];
                if (note.pawn != victim || note.part != part) continue;

                live.RemoveAt(i);
                if (note.expires <= now) continue;

                Break(victim, part);
                return;
            }

            live.Add(new Note { pawn = victim, part = part, expires = now + Props.resonanceTicks });

            if (victim.Spawned && victim.Map != null)
            {
                MoteMaker.ThrowText(victim.DrawPos, victim.Map,
                    "AG_ResonanceRinging".Translate(part.LabelCap), new Color(1f, 0.85f, 0.4f), 2.5f);
            }
        }

        private void Break(Pawn victim, BodyPartRecord part)
        {
            string partLabel = part.LabelCap;

            ResonanceUtility.Shatter(victim, part, Pawn, Props.damageDef);

            Messages.Message(
                "AG_ResonanceBroke".Translate(victim.LabelShortCap, partLabel),
                victim, MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>
        /// Drops notes that have run out, and notes on anyone who has since died or left. A note
        /// on a pawn who is gone is not merely stale - the second blow can never arrive - so
        /// there is nothing to keep.
        /// </summary>
        private void Prune(int now)
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                Note note = live[i];
                if (note.expires > now && note.pawn != null && !note.pawn.Dead
                    && !note.pawn.Destroyed) continue;
                live.RemoveAt(i);
            }
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            ResonanceRegistry.Report(this);
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            Stop();
        }

        private void Stop()
        {
            ResonanceRegistry.Drop(this);
            live.Clear();
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", -1);
        }
    }
}
