using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>Which body part def each piece is, so another race can list its own parts.</summary>
    public class SeverPart
    {
        public AjinPiece piece;
        public BodyPartDef part;
    }

    public class CompProperties_Sever : CompProperties_AbilityEffect
    {
        public int maxAnchors = 2;
        public float rotDays = 3f;
        public List<SeverPart> parts = new List<SeverPart>();

        public CompProperties_Sever() { compClass = typeof(CompAbilityEffect_Sever); }
    }

    /// <summary>
    /// Sever: the button asks for a part first (<see cref="Command_Sever"/>), then the cell. The part comes off
    /// with a bleeding stump and lands there as an anchor; with the anchors full the oldest crumbles.
    /// </summary>
    public class CompAbilityEffect_Sever : CompAbilityEffect
    {
        /// <summary>The part picked from the button's menu for the next cast.</summary>
        public BodyPartRecord chosen;

        public new CompProperties_Sever Props => (CompProperties_Sever)props;

        /// <summary>Flight time of the thrown part (picture only; it is placed at once).</summary>
        public const int FlightTicks = 18;

        /// <summary>His parts that can come off now, in menu order: natural, present, nothing artificial above them.</summary>
        public IEnumerable<(AjinPiece piece, BodyPartRecord part)> Choices(Pawn pawn)
        {
            HediffSet set = pawn.health.hediffSet;
            foreach (SeverPart entry in Props.parts)
            {
                var found = set.GetNotMissingParts().Where(p => p.def == entry.part && !set.PartOrAnyAncestorHasDirectlyAddedParts(p)).ToList();
                if (entry.piece == AjinPiece.Finger)
                {
                    // Ten fingers would flood the menu: offer one, from the left hand first.
                    BodyPartRecord finger = found.OrderBy(p => p.parent?.Label?.Contains("right") == true ? 1 : 0).FirstOrDefault();
                    if (finger != null) yield return (entry.piece, finger);
                    continue;
                }
                foreach (BodyPartRecord part in found) yield return (entry.piece, part);
            }
        }

        public AjinPiece? PieceOf(BodyPartRecord part) => Props.parts.FirstOrDefault(p => p.part == part?.def)?.piece;

        public override bool GizmoDisabled(out string reason)
        {
            if (!Choices(parent.pawn).Any())
            {
                reason = "AG_SeverNothingLeft".Translate();
                return true;
            }
            return base.GizmoDisabled(out reason);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = parent.pawn;
            Map map = pawn.Map;
            if (map == null) return;
            BodyPartRecord part = chosen;
            if (part == null || pawn.health.hediffSet.PartIsMissing(part) || !Choices(pawn).Any(c => c.part == part))
                part = Choices(pawn).Select(c => c.part).FirstOrDefault();
            AjinPiece? piece = PieceOf(part);
            chosen = null;
            if (part == null || piece == null) return;
            Cut(pawn, part);

            List<AjinAnchor> anchors = AjinReset.Anchors(pawn, map);
            for (int i = 0; anchors.Count - i >= Props.maxAnchors && i < anchors.Count; i++) anchors[i].Crumble();

            var anchor = (AjinAnchor)ThingMaker.MakeThing(SatoDefOf.AG_AjinAnchor);
            anchor.owner = pawn;
            anchor.piece = piece.Value;
            anchor.madeTick = Find.TickManager.TicksGame;
            anchor.rotTicks = Mathf.RoundToInt(Props.rotDays * GenDate.TicksPerDay);
            anchor.angle = Rand.Range(0f, 360f);
            anchor.thrownFrom = pawn.DrawPos;
            anchor.landTick = anchor.madeTick + FlightTicks;
            GenSpawn.Spawn(anchor, target.Cell, map);
        }

        /// <summary>The part comes off as a clean cut: a fresh stump that bleeds, as vanilla leaves after a cut-off limb.</summary>
        public static void Cut(Pawn pawn, BodyPartRecord part)
        {
            var missing = (Hediff_MissingPart)HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, pawn, part);
            missing.lastInjury = HediffDefOf.Cut;
            pawn.health.AddHediff(missing, part);
            if (pawn.Spawned && pawn.RaceProps.BloodDef != null)
                FilthMaker.TryMakeFilth(pawn.Position, pawn.Map, pawn.RaceProps.BloodDef, pawn.LabelIndefinite(), 2);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_BodyParts.Look(ref chosen, "chosen");
        }
    }

    /// <summary>Sever's button: a menu of the parts he can cut off, then the usual targeting.</summary>
    [StaticConstructorOnStartup]
    public class Command_Sever : Command_Ability
    {
        public Command_Sever(Ability ability, Pawn pawn) : base(ability, pawn) { }

        public override void ProcessInput(Event ev)
        {
            CompAbilityEffect_Sever comp = ability.CompOfType<CompAbilityEffect_Sever>();
            if (comp == null)
            {
                base.ProcessInput(ev);
                return;
            }
            var options = new List<FloatMenuOption>();
            foreach ((AjinPiece piece, BodyPartRecord part) in comp.Choices(Pawn))
            {
                BodyPartRecord picked = part;
                string label = piece == AjinPiece.Finger ? "AG_SeverFinger".Translate(part.parent?.Label ?? "").Resolve() : part.LabelCap;
                options.Add(new FloatMenuOption(label, () =>
                {
                    comp.chosen = picked;
                    base.ProcessInput(ev);
                }));
            }
            if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
