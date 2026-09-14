using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    [DefOf]
    public static class FumaDefOf
    {
        public static ThingDef AG_FumaShuriken, AG_FumaProjectile;
        public static JobDef AG_ThrowFuma;
        static FumaDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(FumaDefOf));
    }

    public class FumaWeapon : ThingWithComps
    {
        private Graphic folded;
        public override Graphic Graphic
        {
            get
            {
                Pawn pawn = (ParentHolder as Pawn_EquipmentTracker)?.pawn;
                bool fighting = pawn?.stances?.curStance is Stance_Busy || pawn?.CurJobDef == JobDefOf.AttackMelee;
                if (pawn == null || fighting) return base.Graphic;
                return folded ??= GraphicDatabase.Get<Graphic_Single>("RimArt/Fuma/Folded", ShaderDatabase.Cutout,
                    def.graphicData.drawSize, Color.white);
            }
        }
    }

    public class CompProperties_Fuma : CompProperties
    {
        public CompProperties_Fuma() => compClass = typeof(CompFuma);
    }

    public class CompFuma : CompEquippable
    {
        private int readyTick;
        public int Remaining => FumaRules.Remaining(readyTick, Find.TickManager.TicksGame);
        public void Released() => readyTick = Find.TickManager.TicksGame + FumaRules.CooldownTicks;

        public override IEnumerable<Gizmo> CompGetEquippedGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetEquippedGizmosExtra()) yield return gizmo;
            Pawn pawn = Holder;
            if (pawn == null || !pawn.IsColonistPlayerControlled) yield break;
            var command = new Command_Target
            {
                defaultLabel = "Throw Fūma Shuriken",
                defaultDesc = "Throw this weapon along an exact line, cutting through enemies and allies in its path. "
                    + "30 cut damage, 20% armor penetration; damage falls by 20% per pawn to a minimum of 8. "
                    + "Range 12. Retrieve and equip the landed weapon before throwing again.",
                icon = ContentFinder<Texture2D>.Get("RimArt/Fuma/IconFuma"),
                targetingParams = new TargetingParameters
                {
                    canTargetLocations = true, canTargetPawns = false, canTargetBuildings = false,
                    validator = t => Refusal(pawn, t.Cell) == null
                },
                action = t =>
                {
                    string refusal = Refusal(pawn, t.Cell);
                    if (refusal != null)
                    {
                        Messages.Message(refusal, pawn, MessageTypeDefOf.RejectInput, false);
                        return;
                    }
                    pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(FumaDefOf.AG_ThrowFuma, t.Cell, parent));
                },
                onUpdate = t => Preview(pawn, t)
            };
            string reason = PawnRefusal(pawn);
            if (pawn.CurJobDef == FumaDefOf.AG_ThrowFuma) reason = "Already throwing.";
            if (reason != null) command.Disable(reason);
            yield return command;
        }

        private string PawnRefusal(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed || !pawn.Drafted)
                return "Draft a conscious pawn first.";
            if (pawn.WorkTagIsDisabled(WorkTags.Violent)) return "This pawn cannot perform violent work.";
            if (pawn.equipment?.Primary != parent) return "Equip the Fūma Shuriken first.";
            if (Remaining > 0) return $"Ready in {Remaining / 60f:0.0} seconds.";
            return null;
        }

        public string Refusal(Pawn pawn, IntVec3 target)
        {
            string reason = PawnRefusal(pawn);
            if (reason != null) return reason;
            if (!target.InBounds(pawn.Map) || target == pawn.Position) return "Choose another ground cell.";
            if (pawn.Position.DistanceTo(target) > FumaRules.Range) return "Out of range.";
            Vector3 from = pawn.Position.ToVector3Shifted(), to = target.ToVector3Shifted();
            foreach (FumaRules.Cell step in FumaRules.Trace(from.x, from.z, to.x, to.z))
                if (Blocked(new IntVec3(step.X, 0, step.Z), pawn.Map)) return "The flight path is blocked.";
            return null;
        }

        public static bool Blocked(IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map)) return true;
            foreach (Thing thing in cell.GetThingList(map))
                if (thing.def.Fillage == FillCategory.Full && !(thing is Building_Door door && door.Open)) return true;
            return false;
        }

        private void Preview(Pawn pawn, LocalTargetInfo target)
        {
            if (!pawn.Spawned) return;
            GenDraw.DrawRadiusRing(pawn.Position, FumaRules.Range);
            if (!target.IsValid || !target.Cell.InBounds(pawn.Map)) return;
            Vector3 from = pawn.Position.ToVector3Shifted(), to = target.Cell.ToVector3Shifted();
            if (pawn.Position.DistanceTo(target.Cell) > FumaRules.Range) return;
            var cells = new List<IntVec3>();
            foreach (FumaRules.Cell step in FumaRules.Trace(from.x, from.z, to.x, to.z))
            {
                var cell = new IntVec3(step.X, 0, step.Z);
                if (Blocked(cell, pawn.Map)) { GenDraw.DrawFieldEdges(new List<IntVec3> { cell }, Color.red); break; }
                if (!step.Guard) cells.Add(cell);
            }
            GenDraw.DrawFieldEdges(cells, Color.yellow);
            foreach (IntVec3 cell in cells)
                foreach (Thing thing in cell.GetThingList(pawn.Map))
                    if (thing is Pawn other && other != pawn && !other.HostileTo(pawn))
                    { GenDraw.DrawFieldEdges(new List<IntVec3> { cell }, Color.red); break; }
            GenDraw.DrawTargetHighlight(target);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref readyTick, "fumaReadyTick");
        }
    }
}
