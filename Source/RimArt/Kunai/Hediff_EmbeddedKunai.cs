using Verse;

namespace RimArt
{
    /// <summary>
    /// One kunai stuck in one body part. The kunai item does not exist while it is stuck; this
    /// hediff stands for it, and gives it back exactly once:
    ///
    ///   pulled out   <see cref="KunaiEmbedding.TryPull"/> marks it spent and hands the item over
    ///   pawn dies    dropped when the corpse spawns, at the corpse
    ///   removed      any other removal (surgery, the part being destroyed, healing) drops it at
    ///                the pawn
    ///
    /// <see cref="wound"/> is the injury the kunai made. Its bleeding is halved while this is
    /// active (<see cref="Patch_HediffInjury_EmbeddedKunaiBleed"/>).
    /// </summary>
    public class Hediff_EmbeddedKunai : HediffWithComps
    {
        public Hediff_Injury wound;

        /// <summary>Degrees the drawn kunai leans, rolled once so several stuck kunai do not line up.</summary>
        public float drawAngle;

        private bool spent;

        /// <summary>Still holding its kunai, on a living pawn.</summary>
        public bool Active => !spent && pawn != null && !pawn.Dead;

        public override bool Visible => !spent && base.Visible;

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            drawAngle = Rand.Range(-35f, 35f);
        }

        /// <summary>
        /// Never merges. HediffSet.AddDirect offers every new hediff to the existing ones, and the
        /// base Hediff merges any two of the same def on the same part by adding severity - which
        /// for a second kunai in the torso would keep one hediff and silently delete the second
        /// kunai. Each stuck kunai is its own hediff holding its own item.
        /// </summary>
        public override bool TryMergeWith(Hediff other) => false;

        /// <summary>Marks the kunai as handed out. The caller removes the hediff and places the item.</summary>
        public void MarkPulled()
        {
            spent = true;
        }

        public override void Notify_PawnCorpseSpawned()
        {
            base.Notify_PawnCorpseSpawned();
            if (spent) return;
            spent = true;
            KunaiEmbedding.DropKunai(pawn.PositionHeld, pawn.MapHeld);
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            if (spent) return;
            spent = true;
            KunaiEmbedding.DropKunai(pawn.PositionHeld, pawn.MapHeld);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            // A healed wound is gone from the hediff set, and a reference to it would fail to
            // resolve on load. The halved bleeding no longer applies to anything then anyway.
            if (Scribe.mode == LoadSaveMode.Saving && wound != null
                && (pawn?.health?.hediffSet == null || !pawn.health.hediffSet.hediffs.Contains(wound)))
                wound = null;
            Scribe_References.Look(ref wound, "wound");
            Scribe_Values.Look(ref drawAngle, "drawAngle", 0f);
            Scribe_Values.Look(ref spent, "spent", false);
        }
    }
}
