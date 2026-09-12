using RimWorld;
using Verse;

namespace RimArt
{
    public class CompProperties_AbilityDeployToyCar : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityDeployToyCar()
        {
            compClass = typeof(CompAbilityEffect_DeployToyCar);
        }
    }

    /// <summary>
    /// Puts a car on the ground just in front of the caster.
    ///
    /// Deploying does not take control, deliberately. The two are separate acts because parked
    /// is a real state: a car placed in a corridor before a raid arrives costs nothing to leave
    /// there, and only costs an operator when somebody picks up the handset. Merging deploy and
    /// control would delete that.
    ///
    /// The car is selected on the way out, so "Take control" is one click away rather than a
    /// hunt for a small sprite under the caster.
    /// </summary>
    public class CompAbilityEffect_DeployToyCar : CompAbilityEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            if (caster?.Map == null || !caster.Spawned) return;

            ThingDef def = ToyCarDefOf.AG_ToyCar;
            if (def == null) return;

            IntVec3 cell = FreeCellNear(caster);
            if (!cell.IsValid)
            {
                Messages.Message("No clear space nearby to deploy the toy car.", caster,
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            Thing car = GenSpawn.Spawn(ThingMaker.MakeThing(def), cell, caster.Map, caster.Rotation);
            car.SetFactionDirect(caster.Faction);

            Find.Selector.ClearSelection();
            Find.Selector.Select(car);
        }

        /// <summary>
        /// A cell for the car that is not already holding one.
        ///
        /// Two cars in one cell is legal for the game and unusable for the player: they overlap
        /// exactly and only one can be clicked. Walking outwards costs nothing and means a
        /// second car deployed on the same spot lands beside the first.
        /// </summary>
        private static IntVec3 FreeCellNear(Pawn caster)
        {
            Map map = caster.Map;
            IntVec3 front = caster.Position + caster.Rotation.FacingCell;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(front, 2f, true))
            {
                if (cell == caster.Position || !cell.InBounds(map) || !cell.Standable(map)) continue;
                if (cell.GetFirstPawn(map) != null || cell.GetFirstThing<ToyCar>(map) != null) continue;
                Building_Door door = cell.GetDoor(map);
                if (door != null && !door.Open) continue;
                if (!GenSight.LineOfSight(caster.Position, cell, map)) continue;
                return cell;
            }

            return IntVec3.Invalid;
        }
    }
}
