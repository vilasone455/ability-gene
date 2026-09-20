using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public class CompProperties_PowerPoleThrust : CompProperties_AbilityEffect
    {
        public float damage = 22f;
        public float armorPenetration = 0.3f;
        /// <summary>Tiles the hit pawn is carried along the line.</summary>
        public float pushCells = 3f;
        /// <summary>Larger bodies are carried less far, as Vector Shove's are.</summary>
        public bool pushScalesWithBodySize = true;
        /// <summary>Extra blunt damage when a wall or other solid thing stops the carry early.</summary>
        public float wallDamage = 10f;
        public int staggerTicks = 95;

        public CompProperties_PowerPoleThrust()
        {
            compClass = typeof(CompAbilityEffect_PowerPoleThrust);
        }
    }

    /// <summary>
    /// Extend Thrust. The pole extends from the caster toward the chosen tile and stops at the first
    /// pawn on the line, ally or enemy. This works out what the thrust meets; the hit itself lands
    /// when the picture's tip arrives (MapComponent_PowerPoleCasts).
    /// </summary>
    public class CompAbilityEffect_PowerPoleThrust : CompAbilityEffect
    {
        public new CompProperties_PowerPoleThrust Props => (CompProperties_PowerPoleThrust)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            Map map = caster.Map;
            IntVec3 from = caster.Position, to = target.Cell;
            if (from == to) return;
            Vector2 toward = new Vector2(to.x - from.x, to.z - from.z).normalized;

            Pawn victim = FirstPawnOnLine(caster, from, to, map, out float reach);
            var shot = new PowerPoleThrustShot { Contact = reach };
            IntVec3 pushTo = IntVec3.Invalid;
            if (victim != null)
            {
                float cells = Props.pushCells / (Props.pushScalesWithBodySize ? Mathf.Max(1f, victim.BodySize) : 1f);
                pushTo = PowerPoleCombat.PushDestination(victim.Position, toward, cells, map, out bool blocked);
                shot.Hit = true;
                shot.Contact = Mathf.Max(PowerPoleGraphics.RestTip + 0.1f, (victim.Position - from).LengthHorizontal - PowerPoleThrustTiming.BodyRadius);
                shot.Room = (pushTo - victim.Position).LengthHorizontal;
                shot.Blocked = blocked;
            }
            map.GetComponent<MapComponent_PowerPoleCasts>().LandThrust(caster, to, parent.def.verbProperties.warmupTime, shot, victim, pushTo, Props);
        }

        /// <summary>The first pawn on the line to the target tile, and how far the tip gets: to that pawn, to a solid thing in the way, or to the tile.</summary>
        private static Pawn FirstPawnOnLine(Pawn caster, IntVec3 from, IntVec3 to, Map map, out float reach)
        {
            reach = (to - from).LengthHorizontal;
            var line = new List<IntVec3>(GenSight.PointsOnLineOfSight(from, to));
            line.Add(to);
            for (int i = 0; i < line.Count; i++)
            {
                IntVec3 c = line[i];
                if (c == from || !c.InBounds(map)) continue;
                if (c.Filled(map))
                {
                    reach = Mathf.Max(PowerPoleGraphics.RestTip + 0.1f, (c - from).LengthHorizontal - 0.5f);
                    return null;
                }
                List<Thing> things = c.GetThingList(map);
                for (int t = 0; t < things.Count; t++)
                    if (things[t] is Pawn pawn && pawn != caster && !pawn.Dead) return pawn;
            }
            return null;
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn caster = parent.pawn;
            if (caster?.Map == null || !target.IsValid || target.Cell == caster.Position) return;
            var cells = new List<IntVec3>(GenSight.PointsOnLineOfSight(caster.Position, target.Cell));
            cells.Add(target.Cell);
            cells.Remove(caster.Position);
            GenDraw.DrawFieldEdges(cells);
        }
    }
}
