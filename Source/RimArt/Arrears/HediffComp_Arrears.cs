using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public class HediffCompProperties_Arrears : HediffCompProperties
    {
        /// <summary>
        /// How long the body goes untold, in game ticks. Counted here rather than by
        /// HediffCompProperties_Disappears for the same reason the vector reflex does it: a pawn
        /// running accelerated ticks its own hediffs several times per game tick, which would
        /// expire a Disappears comp early - and here that would settle the debt early too.
        /// </summary>
        public int durationTicks = 1200;

        /// <summary>
        /// Damage that maps to full severity, for display only. The ledger is the real store;
        /// this exists so the health tab can say how bad the bill has got without opening it.
        /// </summary>
        public float severityPerDamage = 120f;

        public HediffCompProperties_Arrears()
        {
            compClass = typeof(HediffComp_Arrears);
        }
    }

    /// <summary>
    /// Holds the debt. The plexus takes every wound and does not report it upward, so the body
    /// goes on as though nothing has happened - and then is told all of it in one instant.
    ///
    /// Nothing is reduced anywhere in here. What the ability sells is not less damage, it is a
    /// different *arrival shape*: attrition that a pawn survives by degrees, converted into one
    /// moment that may well not be survivable. That is the entire cost, and it needs no invented
    /// penalty on top.
    /// </summary>
    public class HediffComp_Arrears : HediffComp
    {
        /// <summary>
        /// One unpaid wound.
        ///
        /// The instigator and the weapon are deliberately not kept. They are Thing references
        /// that may be dead, despawned or on another map by the time the bill lands, and none of
        /// them change what the body is about to find out. What has to survive a save is the
        /// wound itself - which def, how much, how hard, and where - because a ledger that
        /// emptied on load would make reloading a way of not paying.
        /// </summary>
        public class Debt : IExposable
        {
            public DamageDef def;
            public float amount;
            public float armorPenetration;
            public float angle;
            public BodyPartRecord part;

            public void ExposeData()
            {
                Scribe_Defs.Look(ref def, "def");
                Scribe_Values.Look(ref amount, "amount", 0f);
                Scribe_Values.Look(ref armorPenetration, "armorPenetration", 0f);
                Scribe_Values.Look(ref angle, "angle", 0f);
                Scribe_BodyParts.Look(ref part, "part");
            }
        }

        private static Texture2D settleIcon;

        private int ticksLeft = -1;
        private int lastGameTickProcessed = -1;
        private bool settling;

        private List<Debt> ledger = new List<Debt>();

        public HediffCompProperties_Arrears Props => (HediffCompProperties_Arrears)props;

        public float Outstanding
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < ledger.Count; i++) total += ledger[i].amount;
                return total;
            }
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);

            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead)
            {
                // Dying with a debt outstanding cancels it. There is no longer a body to tell.
                ledger.Clear();
                ArrearsRegistry.Drop(this);
                return;
            }

            ArrearsRegistry.Report(this);

            // Rate-sensitive: an accelerated pawn runs this comp several times inside one game
            // tick, which would run the window down at the multiplier.
            int now = Find.TickManager.TicksGame;
            if (now == lastGameTickProcessed) return;
            lastGameTickProcessed = now;

            if (ticksLeft < 0)
            {
                ticksLeft = Props.durationTicks;
                Messages.Message("AG_ArrearsUp".Translate(pawn.LabelShort),
                    pawn, MessageTypeDefOf.NeutralEvent, false);
            }

            ticksLeft -= Mathf.Max(1, delta);
            if (ticksLeft <= 0) Settle("AG_ArrearsDue");
        }

        /// <summary>
        /// Takes one wound onto the books instead of into the body. Called from the TakeDamage
        /// prefix, which has already cancelled the instance.
        /// </summary>
        public void Record(DamageInfo dinfo)
        {
            ledger.Add(new Debt
            {
                def = dinfo.Def,
                amount = dinfo.Amount,
                armorPenetration = dinfo.ArmorPenetrationInt,
                angle = dinfo.Angle,
                part = dinfo.HitPart,
            });

            // Display only - the ledger above is what actually gets paid.
            if (Props.severityPerDamage > 0f)
            {
                parent.Severity = Mathf.Min(1f, Outstanding / Props.severityPerDamage);
            }
        }

        /// <summary>
        /// Tells the body. Every wound is re-applied in the order it was received, inside one
        /// tick.
        ///
        /// Armour is applied here and only here, which is correct but worth stating because it
        /// looks wrong. Armour reduction happens inside the damage worker, downstream of the
        /// prefix that took the wound onto the books - so nothing recorded in this ledger has
        /// met armour yet, and re-applying the raw figure now runs it through exactly once. The
        /// stored armour penetration is the original instance's, so the roll is the one the
        /// wound was always going to get, just later than it expected.
        ///
        /// The pawn may die partway down the list, which is the whole point and needs no special
        /// case: the loop stops the moment there is nobody left to tell.
        ///
        /// The hediff is removed *first* so that re-entering TakeDamage during settlement finds
        /// no ledger and lets the damage through normally. Without that, every wound paid would
        /// simply be recorded again.
        /// </summary>
        private void Settle(string messageKey)
        {
            if (settling) return;
            settling = true;

            try
            {
                Pawn pawn = Pawn;
                List<Debt> due = ledger;
                ledger = new List<Debt>();

                ArrearsRegistry.Drop(this);
                if (pawn != null && pawn.health != null && pawn.health.hediffSet.hediffs.Contains(parent))
                {
                    pawn.health.RemoveHediff(parent);
                }

                if (pawn == null || pawn.Dead || due.Count == 0) return;

                float total = 0f;
                for (int i = 0; i < due.Count; i++) total += due[i].amount;

                Messages.Message(messageKey.Translate(pawn.LabelShort, total.ToString("F0")),
                    pawn, MessageTypeDefOf.NegativeEvent, false);

                for (int i = 0; i < due.Count; i++)
                {
                    if (pawn.Dead) break;

                    Debt debt = due[i];
                    if (debt.def == null) continue;

                    DamageInfo dinfo = new DamageInfo(
                        debt.def, debt.amount, debt.armorPenetration, debt.angle, null, debt.part,
                        null, DamageInfo.SourceCategory.ThingOrUnknown);

                    pawn.TakeDamage(dinfo);
                }
            }
            finally
            {
                settling = false;
            }
        }

        /// <summary>
        /// Settling early is the interesting decision the ability offers, which is why it is a
        /// gizmo and not an automatic thing: the bill is going to arrive either way, and the
        /// only question the carrier gets to answer is whether it arrives here or wherever they
        /// happen to be standing in fifteen seconds' time.
        /// </summary>
        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            Pawn pawn = Pawn;
            if (pawn == null || !pawn.IsColonistPlayerControlled) yield break;

            if (settleIcon == null)
            {
                // Resolved from the draw path, which is always the main thread.
                settleIcon = ContentFinder<Texture2D>.Get("UI/Abilities/MechResurrection", false);
            }

            yield return new Command_Action
            {
                defaultLabel = "AG_ArrearsSettleLabel".Translate(Outstanding.ToString("F0")),
                defaultDesc = "AG_ArrearsSettleDesc".Translate(),
                icon = settleIcon,
                action = delegate { Settle("AG_ArrearsSettledEarly"); }
            };
        }

        public override string CompLabelInBracketsExtra => Outstanding.ToString("F0");

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            ArrearsRegistry.Report(this);
        }

        /// <summary>
        /// Removal by any other route than Settle - a dev tool, another mod, the pawn being
        /// healed by something that clears hediffs - must not be a way of walking away from the
        /// debt. Settle is re-entrancy guarded, so the removal it performs itself lands here
        /// harmlessly.
        /// </summary>
        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            ArrearsRegistry.Drop(this);
            if (ledger.Count > 0) Settle("AG_ArrearsDue");
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", -1);
            Scribe_Collections.Look(ref ledger, "ledger", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && ledger == null) ledger = new List<Debt>();
        }
    }
}
