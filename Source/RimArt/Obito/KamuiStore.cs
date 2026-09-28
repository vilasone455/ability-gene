using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Kamui: Store (docs/hero-echo.md "Obito"): absorb and release are one ability.
    ///
    /// Absorb: by touch, a pawn up to <see cref="maxBodySize"/> or one item stack, warm-up 0.4 s (the verb's),
    /// none on an enemy whose attack went through him in the last <see cref="counterSeconds"/>. Held enemies
    /// are stunned in the dimension the whole time (no capture); allies stay awake. Cast cost 1 charge.
    ///
    /// Release: pick a stored thing, then a cell within <see cref="releaseRange"/> in sight. An enemy comes out
    /// stunned for <see cref="releaseStunSeconds"/>. Its own cooldown; no charge.
    /// </summary>
    public class CompProperties_AbilityKamuiStore : CompProperties_AbilityEffect
    {
        public float maxBodySize = 1.2f;
        public float counterSeconds = 5f;
        public float releaseRange = 6f;
        public float releaseStunSeconds = 2f;
        public float releaseCooldownSeconds = 5f;

        public CompProperties_AbilityKamuiStore()
        {
            compClass = typeof(CompAbilityEffect_KamuiStore);
        }
    }

    public class CompAbilityEffect_KamuiStore : CompAbilityEffect
    {
        public new CompProperties_AbilityKamuiStore Props => (CompProperties_AbilityKamuiStore)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            InvoluteUtility.GeneOf(parent.pawn)?.Absorb(target.Thing);
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest) => Valid(target) && base.CanApplyOn(target, dest);

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent.pawn;
            Gene_Involute gene = InvoluteUtility.GeneOf(caster);
            if (gene == null) return false;
            string reason = null;
            Thing thing = target.Thing;
            if (thing == null || thing == caster) reason = "AG_KamuiStoreNeedsTarget".Translate();
            else if (gene.Inside) reason = "AG_KamuiStoreInside".Translate(caster.LabelShortCap);
            else if (thing is Pawn p && p.BodySize > Props.maxBodySize) reason = "AG_KamuiStoreTooBig".Translate(p.LabelShortCap);
            else if (!(thing is Pawn) && thing.def.category != ThingCategory.Item) reason = "AG_KamuiStoreNeedsTarget".Translate();
            if (reason == null) return base.Valid(target, throwMessages);
            if (throwMessages) Messages.Message(reason, caster, MessageTypeDefOf.RejectInput, false);
            return false;
        }

        public override bool GizmoDisabled(out string reason)
        {
            reason = null;
            Gene_Involute gene = InvoluteUtility.GeneOf(parent.pawn);
            if (gene == null)
            {
                reason = "AG_KamuiNoGene".Translate(parent.pawn.LabelShortCap);
                return true;
            }
            if (gene.Inside)
            {
                reason = "AG_KamuiStoreInside".Translate(parent.pawn.LabelShortCap);
                return true;
            }
            return base.GizmoDisabled(out reason);
        }
    }

    /// <summary>
    /// Store's touch verb. The warm-up is the def's, except on an enemy whose attack went through Obito in the
    /// last few seconds: then the absorb fires the moment he touches it.
    /// </summary>
    public class Verb_KamuiStore : Verb_CastAbilityTouch
    {
        public override float WarmupTime
        {
            get
            {
                Gene_Involute gene = InvoluteUtility.GeneOf(CasterPawn);
                return gene != null && gene.PassedThroughRecently(currentTarget.Thing) ? 0f : base.WarmupTime;
            }
        }
    }

    /// <summary>Kamui: Store's ability: the absorb button, and the release button while anything is stored.</summary>
    public class Ability_KamuiStore : Ability
    {
        public Ability_KamuiStore() { }
        public Ability_KamuiStore(Pawn pawn) : base(pawn) { }
        public Ability_KamuiStore(Pawn pawn, Precept sourcePrecept) : base(pawn, sourcePrecept) { }
        public Ability_KamuiStore(Pawn pawn, AbilityDef def) : base(pawn, def) { }
        public Ability_KamuiStore(Pawn pawn, Precept sourcePrecept, AbilityDef def) : base(pawn, sourcePrecept, def) { }

        public override IEnumerable<Command> GetGizmos()
        {
            foreach (Command command in base.GetGizmos()) yield return command;
            Gene_Involute gene = InvoluteUtility.GeneOf(pawn);
            if (gene == null || gene.Stored.Count == 0) yield break;
            if (pawn.Drafted && !def.showWhenDrafted) yield break;
            yield return ReleaseCommand(gene);
        }

        private Command ReleaseCommand(Gene_Involute gene)
        {
            var command = new Command_Action
            {
                defaultLabel = "AG_KamuiReleaseLabel".Translate(gene.Stored.Count),
                defaultDesc = "AG_KamuiReleaseDesc".Translate(ObitoRules.Store.releaseRange.ToString("0"), ObitoRules.Store.releaseStunSeconds.ToString("0.#")),
                icon = ObitoGraphics.ReleaseIcon,
                Order = 5.6f,
                action = () => ChooseStored(gene),
            };
            if (!gene.ActiveNow) command.Disable("AG_KamuiNotManifested".Translate(pawn.LabelShortCap));
            else if (gene.Inside) command.Disable("AG_KamuiStoreInside".Translate(pawn.LabelShortCap));
            else if (gene.Intangible) command.Disable("AG_KamuiPhasedCannot".Translate(pawn.LabelShortCap));
            else if (pawn.Downed || !pawn.Spawned) command.Disable("CommandDisabledUnconscious".TranslateWithBackup("CommandCallRoyalAidUnconscious").Formatted(pawn));
            else if (gene.Releasing) command.Disable("AG_KamuiReleasing".Translate());
            else if (!gene.ReleaseReady)
                command.Disable("AbilityOnCooldown".Translate((gene.ReleaseReadyTick - Find.TickManager.TicksGame).ToStringTicksToPeriod()).Resolve());
            return command;
        }

        private void ChooseStored(Gene_Involute gene)
        {
            var options = new List<FloatMenuOption>();
            foreach (Thing thing in gene.Stored.OrderBy(t => t is Pawn ? 0 : 1))
            {
                Thing chosen = thing;
                string label = thing is Pawn p && p.HostileTo(Faction.OfPlayer)
                    ? "AG_KamuiReleaseHostile".Translate(thing.LabelCap).ToString()
                    : thing.LabelCap.ToString();
                options.Add(new FloatMenuOption(label, () => TargetCell(gene, chosen), thing, Color.white));
            }
            if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
        }

        private void TargetCell(Gene_Involute gene, Thing thing)
        {
            float range = ObitoRules.Store.releaseRange;
            var parms = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetPawns = false,
                canTargetBuildings = false,
                canTargetItems = false,
            };
            Find.Targeter.BeginTargeting(parms,
                target => gene.BeginRelease(thing, pawn.Map, target.Cell),
                target =>
                {
                    GenDraw.DrawRadiusRing(pawn.Position, range);
                    if (ValidRelease(pawn, target.Cell, range)) GenDraw.DrawTargetHighlight(target);
                },
                target => ValidRelease(pawn, target.Cell, range),
                pawn, null, ObitoGraphics.ReleaseIcon);
        }

        public static bool ValidRelease(Pawn caster, IntVec3 cell, float range)
        {
            Map map = caster.Map;
            if (map == null || !cell.InBounds(map) || cell.Fogged(map) || !cell.Standable(map)) return false;
            if (cell.DistanceTo(caster.Position) > range) return false;
            return GenSight.LineOfSight(caster.Position, cell, map, true);
        }
    }
}
