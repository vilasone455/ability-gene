using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class WaterGunDefOf
    {
        public static ThingDef AG_WaterGun;
        /// <summary>Slides a pawn Hydro Pump pushes, over the picture's 0.4 s slide.</summary>
        public static ThingDef AG_WaterGunPushed;
        public static HediffDef AG_Soaked;

        static WaterGunDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(WaterGunDefOf));
        }
    }

    /// <summary>What both water abilities share: they need the gun in hand and enough water in its bag.</summary>
    public abstract class CompAbilityEffect_WaterGun : CompAbilityEffect
    {
        protected CompWaterGun Gun => CompWaterGun.HeldBy(parent.pawn);

        /// <summary>Units one cast spends.</summary>
        protected abstract int Cost { get; }

        public override bool CanCast => base.CanCast && Unavailable() == null;

        private string Unavailable()
        {
            CompWaterGun gun = Gun;
            if (gun == null) return "Requires a water gun in hand.";
            if (!parent.pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return parent.pawn.LabelShortCap + " cannot manipulate.";
            if (gun.Units <= 0) return "No water. The bag refills next to water or in rain.";
            if (gun.Units < Cost) return "Not enough water: needs " + Cost + ", the bag has " + gun.Units + ".";
            return null;
        }

        public override bool GizmoDisabled(out string reason)
        {
            reason = Unavailable();
            return reason != null || base.GizmoDisabled(out reason);
        }

        public override string ExtraTooltipPart()
        {
            CompWaterGun gun = Gun;
            return gun == null ? null : "Water: " + gun.LabelRemaining;
        }

        protected bool TrySpend()
        {
            CompWaterGun gun = Gun;
            if (gun == null || gun.Units < Cost) return false;
            gun.Spend(Cost);
            return true;
        }
    }

    public class CompProperties_StreamShot : CompProperties_AbilityEffect
    {
        public int cost = 1;
        public float damage = 6f;
        public float armorPenetration = 0f;
        public float soakedSeconds = 60f;
        /// <summary>The jet lands on a pawn that has moved at most this far from where it was aimed at.</summary>
        public float followRadius = 1.5f;

        public CompProperties_StreamShot()
        {
            compClass = typeof(CompAbilityEffect_StreamShot);
        }
    }

    /// <summary>
    /// Stream Shot. One jet at a pawn or a cell; it always lands (no hit roll). On a pawn: blunt
    /// damage and Soaked. Fire on the pawn and on the target cell is put out. The hit lands when the
    /// picture's jet arrives (MapComponent_WaterGun).
    /// </summary>
    public class CompAbilityEffect_StreamShot : CompAbilityEffect_WaterGun
    {
        public new CompProperties_StreamShot Props => (CompProperties_StreamShot)props;

        protected override int Cost => Props.cost;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster?.Map == null || !target.IsValid || !TrySpend()) return;
            caster.Map.GetComponent<MapComponent_WaterGun>().LandStream(caster, target, parent.def.verbProperties.warmupTime, Props);
        }
    }

    public class CompProperties_HydroPump : CompProperties_AbilityEffect
    {
        public int cost = 10;
        /// <summary>Cone length from the caster, and its width at the far end, in cells.</summary>
        public float length = 5f;
        public float width = 3f;
        public float pushCells = 3f;
        public float knockdownSeconds = 2f;
        public float damage = 8f;
        public float armorPenetration = 0f;
        public float soakedSeconds = 60f;

        public CompProperties_HydroPump()
        {
            compClass = typeof(CompAbilityEffect_HydroPump);
        }
    }

    /// <summary>
    /// Hydro Pump. A cone along the chosen direction from the muzzle: every pawn in it that the caster
    /// can see (allies too) takes blunt damage, is Soaked, slides the push along the aim and is
    /// stunned for the knockdown. Fire in the cone is put out. The hits land as the picture's blast
    /// front reaches each pawn (MapComponent_WaterGun).
    /// </summary>
    public class CompAbilityEffect_HydroPump : CompAbilityEffect_WaterGun
    {
        public new CompProperties_HydroPump Props => (CompProperties_HydroPump)props;

        protected override int Cost => Props.cost;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster?.Map == null || !target.IsValid || target.Cell == caster.Position || !TrySpend()) return;
            caster.Map.GetComponent<MapComponent_WaterGun>().LandPump(caster, target.Cell, parent.def.verbProperties.warmupTime, Props);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn caster = parent.pawn;
            if (caster?.Map == null || !target.IsValid || target.Cell == caster.Position) return;
            GenDraw.DrawFieldEdges(WaterGunCone.Cells(caster, target.Cell, Props.length, Props.width));
        }
    }

    /// <summary>
    /// Hydro Pump's area. A cell is in the cone when its centre, in the aim frame from the caster's
    /// cell, is between 0.5 and the length along the aim and no more than the wedge's half width
    /// plus half a cell across it, and the caster can see it. The floor wedge the picture draws is the
    /// same wedge; the half-cell margin is a pawn's body.
    /// </summary>
    public static class WaterGunCone
    {
        public static Vector2 Toward(IntVec3 from, IntVec3 to)
        {
            var run = new Vector2(to.x - from.x, to.z - from.z);
            return run.sqrMagnitude < 0.01f ? Vector2.right : run.normalized;
        }

        public static bool Contains(IntVec3 from, Vector2 toward, IntVec3 cell, float length, float width, out float along, out float across)
        {
            float dx = cell.x - from.x, dz = cell.z - from.z;
            along = dx * toward.x + dz * toward.y;
            across = -dx * toward.y + dz * toward.x;
            return along >= 0.5f && along <= length && Mathf.Abs(across) <= WaterGunPumpTiming.HalfWidth(along, length, width) + 0.5f;
        }

        public static List<IntVec3> Cells(Pawn caster, IntVec3 target, float length, float width)
        {
            var cells = new List<IntVec3>();
            Map map = caster.Map;
            IntVec3 from = caster.Position;
            Vector2 toward = Toward(from, target);
            int reach = Mathf.CeilToInt(length + width);
            foreach (IntVec3 c in GenRadial.RadialCellsAround(from, reach, false))
                if (c.InBounds(map) && Contains(from, toward, c, length, width, out _, out _) && GenSight.LineOfSight(from, c, map, true))
                    cells.Add(c);
            return cells;
        }
    }

    /// <summary>Soaked: applied, refreshed, and what it does to fire.</summary>
    public static class WaterGunSoak
    {
        private static readonly List<Thing> Buffer = new List<Thing>();

        public static bool IsSoaked(Pawn pawn) =>
            pawn?.health?.hediffSet != null && pawn.health.hediffSet.GetFirstHediffOfDef(WaterGunDefOf.AG_Soaked) != null;

        /// <summary>Adds Soaked for <paramref name="seconds"/>, or sets an existing one back to that, and puts out fire on the pawn.</summary>
        public static void Apply(Pawn pawn, float seconds)
        {
            if (pawn?.health == null || pawn.Dead) return;
            Extinguish(pawn);
            if (seconds <= 0f) return;
            Hediff soaked = pawn.health.hediffSet.GetFirstHediffOfDef(WaterGunDefOf.AG_Soaked);
            if (soaked == null)
            {
                soaked = HediffMaker.MakeHediff(WaterGunDefOf.AG_Soaked, pawn);
                pawn.health.AddHediff(soaked);
            }
            soaked.TryGetComp<HediffComp_Disappears>()?.SetDuration(Mathf.RoundToInt(seconds * 60f));
        }

        /// <summary>Puts out the fire attached to a thing, if any. Returns whether there was one.</summary>
        public static bool Extinguish(Thing thing)
        {
            Thing fire = thing?.GetAttachment(ThingDefOf.Fire);
            if (fire == null || fire.Destroyed) return false;
            fire.Destroy();
            return true;
        }

        /// <summary>Puts out every fire on a cell: fires lying there and fires on things standing there.</summary>
        public static bool ExtinguishCell(IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map)) return false;
            // Fires attached to a pawn are spawned on its cell too, so every fire here is one of these.
            // A copy, because destroying a fire changes the cell's list.
            Buffer.Clear();
            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
                if (things[i] is Fire) Buffer.Add(things[i]);
            for (int i = 0; i < Buffer.Count; i++)
                if (!Buffer[i].Destroyed) Buffer[i].Destroy();
            bool any = Buffer.Count > 0;
            Buffer.Clear();
            return any;
        }
    }
}
