using RimWorld;
using Verse;

namespace RimArt
{
    public class CompProperties_AbilityRetrievalHook : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityRetrievalHook()
        {
            compClass = typeof(CompAbilityEffect_RetrievalHook);
        }
    }

    /// <summary>
    /// Fires the retrieval hook. Aiming, target reasons and the gizmo state live here; the flight
    /// and drag run in <see cref="MapComponent_RetrievalHooks"/>.
    /// </summary>
    public class CompAbilityEffect_RetrievalHook : CompAbilityEffect
    {
        private CompRetrievalHookBelt Belt => CompRetrievalHookBelt.WornBy(parent.pawn);

        public override bool CanCast => base.CanCast && Unavailable() == null;

        /// <summary>Why the ability cannot be fired right now, or null.</summary>
        private string Unavailable()
        {
            Pawn pawn = parent.pawn;
            CompRetrievalHookBelt belt = Belt;
            if (belt == null) return "Requires a retrieval hook belt.";
            if (!pawn.IsColonistPlayerControlled) return "Only player-controlled colonists can fire it.";
            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return pawn.LabelShortCap + " cannot manipulate.";
            if (MapComponent_RetrievalHooks.IsPulling(pawn)) return "The tether is already out on a target.";
            if (!belt.Loaded)
                return "The tether is out. Use Reel in tether first (" + belt.ReloadFraction.ToStringPercent("F0") + " done).";
            return null;
        }

        public override bool GizmoDisabled(out string reason)
        {
            reason = Unavailable();
            return reason != null || base.GizmoDisabled(out reason);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            string refusal = RefusalFor(target, out _);
            if (refusal == null) return true;
            if (throwMessages)
                Messages.Message("Cannot use retrieval hook: " + refusal, parent.pawn, MessageTypeDefOf.RejectInput, false);
            return false;
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            // The base version reads target.Pawn and would throw on an item.
            return RefusalFor(target, out _) == null;
        }

        private string RefusalFor(LocalTargetInfo target, out int take)
        {
            take = 0;
            if (!target.HasThing) return "Target a downed person or an item.";
            return RetrievalTargets.Refusal(parent.pawn, target.Thing, out take);
        }

        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            if (!target.HasThing) return null;
            string refusal = RefusalFor(target, out int take);
            if (refusal != null) return refusal;

            Thing thing = target.Thing;
            if (thing is Pawn pawn)
            {
                bool injured = pawn.health.hediffSet.GetFirstHediff<Hediff_Injury>() != null;
                return "Pull " + pawn.LabelShort + (injured
                    ? ". Worsens one wound by up to 1 and removes its bandage."
                    : ". No injuries to worsen.");
            }

            float mass = thing.GetStatValue(StatDefOf.Mass);
            return "Pull " + take + " of " + thing.stackCount + " (" + (take * mass).ToString("0.#") + " kg of "
                   + RetrievalHookDefaults.ItemCapacityKg.ToString("0") + " kg)";
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            if (RefusalFor(target, out _) == null) RetrievalHookGraphics.DrawPreview(parent.pawn, target);
        }

        public override string ExtraTooltipPart()
        {
            CompRetrievalHookBelt belt = Belt;
            if (belt == null) return null;
            return belt.Loaded ? "Tether: loaded" : "Tether: out, reeled in " + belt.ReloadFraction.ToStringPercent("F0");
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            CompRetrievalHookBelt belt = Belt;
            if (caster == null || belt == null) return;

            // Every launched shot costs the reload, including ones that miss or are interrupted.
            belt.Unload();
            if (!target.HasThing || !caster.Spawned) return;

            MapComponent_RetrievalHooks.Launch(caster, (Apparel)belt.parent, target.Thing);
        }
    }
}
