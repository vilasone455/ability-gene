using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// A Reset in progress (see <see cref="AjinReset"/>). The def keeps him from dying and holds consciousness
    /// down, so he lies downed. This counts the damage the lying body takes and says when and where he rises;
    /// the game component does the moving.
    /// </summary>
    public class Hediff_AjinReset : HediffWithComps
    {
        public AjinCause cause;
        public int startTick, riseTick;
        /// <summary>Manifested and paid: the short delay, rising at the biggest piece.</summary>
        public bool fast;
        public float paid;
        /// <summary>Set by an explosion or by damage; the body is taken off the map on the next tick.</summary>
        public bool destroyPending;
        public bool bodyDestroyed;
        public float damageTaken;
        /// <summary>Damage that destroys the lying body: the trait's share of his part health when the Reset began.</summary>
        public float destroyAtDamage;
        /// <summary>The anchor or remains holding him while the body is gone.</summary>
        public AjinAnchor holder;

        public override bool ShouldRemove => false;

        public int TicksLeft => riseTick - Find.TickManager.TicksGame;
        public float Seconds => (Find.TickManager.TicksGame - startTick).TicksToSeconds();
        public float DelaySeconds => (riseTick - startTick).TicksToSeconds();

        public override string LabelInBrackets
        {
            get
            {
                string left = TicksLeft > 0 ? TicksLeft.ToStringTicksToPeriod() : "0s";
                return holder != null ? left + ", " + holder.PieceLabel : left;
            }
        }

        public override string TipStringExtra =>
            (fast ? "AG_AjinResetFast".Translate(paid.ToString("0")) : "AG_AjinResetSlow".Translate()).Resolve();

        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);
            if (holder != null || bodyDestroyed || totalDamageDealt <= 0f) return;
            damageTaken += totalDamageDealt;
            if (damageTaken >= destroyAtDamage) destroyPending = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref cause, "cause");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref riseTick, "riseTick");
            Scribe_Values.Look(ref fast, "fast");
            Scribe_Values.Look(ref paid, "paid");
            Scribe_Values.Look(ref destroyPending, "destroyPending");
            Scribe_Values.Look(ref bodyDestroyed, "bodyDestroyed");
            Scribe_Values.Look(ref damageTaken, "damageTaken");
            Scribe_Values.Look(ref destroyAtDamage, "destroyAtDamage");
            Scribe_References.Look(ref holder, "holder");
        }
    }
}
