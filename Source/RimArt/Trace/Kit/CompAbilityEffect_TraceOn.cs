using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public class CompProperties_TraceOn : CompProperties_AbilityEffect
    {
        /// <summary>Quality levels a copy is below the best one of its weapon and material studied (never under awful).</summary>
        public int qualityBelow = 1;

        public CompProperties_TraceOn() => compClass = typeof(CompAbilityEffect_TraceOn);

        /// <summary>The ability def's <see cref="qualityBelow"/>, for labels outside the ability.</summary>
        public static int QualityBelow => TraceDefOf.AG_Trace_On?.comps?.OfType<CompProperties_TraceOn>().FirstOrDefault()?.qualityBelow ?? 1;
    }

    /// <summary>
    /// Trace On (docs/hero-echo.md, Shirou). The button lists the pawn's trace library; the pick is kept here
    /// (<see cref="chosen"/>) and the cast job (<see cref="JobDriver_CastTrace"/>) first puts away what the hand
    /// holds: a copy breaks, a real weapon goes to the inventory. When the warmup ends the copy is put in the hand:
    /// the studied weapon in its material, <see cref="CompProperties_TraceOn.qualityBelow"/> below the best one
    /// studied. A copy breaks when it leaves the hand (<see cref="TraceCopies"/>).
    /// </summary>
    public class CompAbilityEffect_TraceOn : CompAbilityEffect
    {
        public TraceLibraryEntry chosen;

        public new CompProperties_TraceOn Props => (CompProperties_TraceOn)props;

        /// <summary>The pick if it can still be traced, else null.</summary>
        public TraceLibraryEntry Chosen => chosen != null && TraceLibrary.CanTrace(chosen)
            && TraceLibrary.Of(parent.pawn).Any(e => e.Same(chosen)) ? chosen : null;

        public override bool GizmoDisabled(out string reason)
        {
            if (!TraceLibrary.Of(parent.pawn).Any(TraceLibrary.CanTrace))
            {
                reason = "AG_TraceNothingStudied".Translate(parent.pawn.LabelShort);
                return true;
            }
            return base.GizmoDisabled(out reason);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false) => Chosen != null && base.Valid(target, throwMessages);

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = parent.pawn;
            GameComponent_Trace trace = GameComponent_Trace.Instance;
            // The cast made its copy when it began, so the picture traced this one; a cast from a loaded save has none.
            ThingWithComps copy = trace?.CastOf(pawn, parent.def)?.pending ?? TraceLibrary.MakeCopy(Chosen, Props.qualityBelow);
            if (copy == null || pawn.equipment == null) return;
            TraceCopies.ClearHands(pawn);
            TraceCopies.Give(pawn, copy);
            trace?.Fired(pawn, parent.def);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref chosen, "traceChosen");
        }
    }

    /// <summary>Trace On's button: a menu of the blades in the library, each labelled as its copy will be, then the cast.</summary>
    public class Command_TraceOn : Command_Ability
    {
        public Command_TraceOn(Ability ability, Pawn pawn) : base(ability, pawn) { }

        public override void ProcessInput(Event ev)
        {
            var comp = ability.CompOfType<CompAbilityEffect_TraceOn>();
            if (comp == null)
            {
                base.ProcessInput(ev);
                return;
            }
            var options = new List<FloatMenuOption>();
            foreach (TraceLibraryEntry entry in TraceLibrary.Of(Pawn).OrderBy(e => e.Blade?.label ?? e.blade).ThenBy(e => e.stuff))
            {
                TraceLibraryEntry picked = entry;
                string label = TraceLibrary.Label(entry, comp.Props.qualityBelow), refusal = TraceLibrary.Refusal(entry);
                if (refusal != null)
                {
                    options.Add(new FloatMenuOption(label + ": " + refusal, null));
                    continue;
                }
                ThingDef def = entry.Blade;
                options.Add(new FloatMenuOption(label, () =>
                {
                    comp.chosen = picked;
                    base.ProcessInput(ev);
                }, def.uiIcon, TraceLibrary.StuffFor(entry, def) is ThingDef stuff ? def.GetColorForStuff(stuff) : def.uiIconColor));
            }
            if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
