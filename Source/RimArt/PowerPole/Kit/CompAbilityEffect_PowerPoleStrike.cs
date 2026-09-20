using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public class CompProperties_PowerPoleStrike : CompProperties_AbilityEffect
    {
        public float damage = 20f;
        public float armorPenetration = 0.3f;
        public int stunTicks = 90;
        /// <summary>Tiles round the target tile in which every other pawn is staggered.</summary>
        public float staggerRadius = 1.5f;
        public int staggerTicks = 95;

        public CompProperties_PowerPoleStrike()
        {
            compClass = typeof(CompAbilityEffect_PowerPoleStrike);
        }
    }

    /// <summary>
    /// Vault Strike. The wielder is carried to the tile next to the target, on the side it came
    /// from, by a flyer whose def holds the time in the air and the peak. The strike on the target
    /// tile lands just before the wielder does (MapComponent_PowerPoleCasts). No line of sight is
    /// needed and roofs do not matter.
    /// </summary>
    public class CompAbilityEffect_PowerPoleStrike : CompAbilityEffect
    {
        public new CompProperties_PowerPoleStrike Props => (CompProperties_PowerPoleStrike)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages)) return false;
            if (LandingCell(parent.pawn, target.Cell).IsValid) return true;
            if (throwMessages) Messages.Message("AG_PowerPoleNoLanding".Translate(), parent.pawn, MessageTypeDefOf.RejectInput, false);
            return false;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            Map map = caster.Map;
            if (!TryShot(caster, target.Cell, out PowerPoleStrikeShot shot, out Vector2 toward, out IntVec3 landing)) return;

            // MakeFlyer takes the pawn off the map, and with it the player's selection.
            bool wasSelected = Find.Selector.IsSelected(caster);
            var flyer = (PawnFlyer_PowerPoleVault)PawnFlyer.MakeFlyer(PowerPoleDefOf.AG_PowerPoleVault, caster, landing, null, null, false, null, parent, target);
            if (flyer == null) return;
            GenSpawn.Spawn(flyer, landing, map);
            if (wasSelected) Find.Selector.Select(caster, false, false);

            shot.Flight = flyer.FlightSeconds;
            map.GetComponent<MapComponent_PowerPoleCasts>().LandStrike(caster, target.Cell, parent.def.verbProperties.warmupTime, shot, toward, flyer, Props);
        }

        /// <summary>The numbers the picture needs: the line the wielder flies along and where the target tile is from it.</summary>
        public bool TryShot(Pawn caster, IntVec3 target, out PowerPoleStrikeShot shot, out Vector2 toward, out IntVec3 landing)
        {
            shot = default;
            toward = Vector2.right;
            landing = LandingCell(caster, target);
            if (!landing.IsValid) return false;
            var flight = new Vector2(landing.x - caster.Position.x, landing.z - caster.Position.z);
            var toTarget = new Vector2(target.x - caster.Position.x, target.z - caster.Position.z);
            if (flight.sqrMagnitude > 0.01f) toward = flight.normalized;
            else if (toTarget.sqrMagnitude > 0.01f) toward = toTarget.normalized;
            var left = new Vector2(-toward.y, toward.x);
            PawnFlyerProperties flyer = PowerPoleDefOf.AG_PowerPoleVault.pawnFlyer;
            shot = new PowerPoleStrikeShot
            {
                Landing = flight.magnitude, TargetAlong = Vector2.Dot(toTarget, toward), TargetAcross = Vector2.Dot(toTarget, left),
                Flight = flyer.flightDurationMin, Peak = flyer.heightFactor / SixPathsHeight.Lift,
            };
            return true;
        }

        /// <summary>The free tile next to the target that is nearest the wielder, or the wielder's own tile when it already stands next to it.</summary>
        public static IntVec3 LandingCell(Pawn caster, IntVec3 target)
        {
            Map map = caster.Map;
            if (map == null || !target.InBounds(map)) return IntVec3.Invalid;
            if (caster.Position.AdjacentTo8Way(target)) return caster.Position;
            IntVec3 best = IntVec3.Invalid;
            float nearest = float.MaxValue;
            for (int i = 0; i < 8; i++)
            {
                IntVec3 c = target + GenAdj.AdjacentCells[i];
                if (!JumpUtility.ValidJumpTarget(caster, map, c) || c.GetFirstPawn(map) != null) continue;
                float distance = (c - caster.Position).LengthHorizontalSquared;
                if (distance >= nearest) continue;
                nearest = distance;
                best = c;
            }
            return best;
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn caster = parent.pawn;
            if (caster?.Map == null || !target.IsValid) return;
            GenDraw.DrawRadiusRing(target.Cell, Props.staggerRadius);
            IntVec3 landing = LandingCell(caster, target.Cell);
            if (landing.IsValid) GenDraw.DrawFieldEdges(new List<IntVec3> { landing });
        }
    }
}
