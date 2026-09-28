using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Wood Release: Cutting Technique (docs/hero-echo.md "Obito"): branches shoot from his right arm along a
    /// straight line of <see cref="length"/> cells in the aimed direction and skewer every pawn on it, allies
    /// included (<see cref="hitAllies"/>): <see cref="damage"/> each at <see cref="armorPenetration"/>. The
    /// front travels <see cref="speed"/> cells a second, so a pawn is hit when it reaches it. The line stops at
    /// the first wall (<see cref="stopAtWalls"/>). No pin: the branches crumble and the pawns stay free.
    /// Warm-up, cooldown and the 2 charges are the def's; cast cost 2 charge (the EchoDef).
    /// </summary>
    public class CompProperties_AbilityWoodRelease : CompProperties_AbilityEffect
    {
        public float length = 10f;
        public float damage = 15f;
        public float armorPenetration = 0.3f;
        public DamageDef damageDef;
        public bool hitAllies = true;
        public bool stopAtWalls = true;
        /// <summary>Cells a second the branches' front travels; a pawn is hit when it reaches it.</summary>
        public float speed = 40f;

        public CompProperties_AbilityWoodRelease()
        {
            compClass = typeof(CompAbilityEffect_WoodRelease);
        }
    }

    public class CompAbilityEffect_WoodRelease : CompAbilityEffect
    {
        public new CompProperties_AbilityWoodRelease Props => (CompProperties_AbilityWoodRelease)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster?.Map == null) return;
            Vector2 aim = Aim(caster, target.Cell);
            List<IntVec3> cells = Line(caster, aim, Props, out float reach);
            ObitoFX.WoodRelease(caster, aim, reach, cells, Props);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn caster = parent.pawn;
            if (caster?.Map == null || !target.IsValid) return;
            GenDraw.DrawFieldEdges(Line(caster, Aim(caster, target.Cell), Props, out _));
        }

        /// <summary>The unit direction on the map (x, z) from the caster to the aimed cell.</summary>
        public static Vector2 Aim(Pawn caster, IntVec3 cell)
        {
            Vector3 from = caster.Position.ToVector3Shifted(), to = cell.ToVector3Shifted();
            var aim = new Vector2(to.x - from.x, to.z - from.z);
            return aim.sqrMagnitude < 1e-4f ? new Vector2(caster.Rotation.FacingCell.x, caster.Rotation.FacingCell.z) : aim.normalized;
        }

        /// <summary>
        /// The hit cells: the 1-cell line from the next cell out to <c>length</c> along <paramref name="aim"/>,
        /// cut at the first wall. <paramref name="reach"/> is how far the branches go (to the wall or the end).
        /// </summary>
        public static List<IntVec3> Line(Pawn caster, Vector2 aim, CompProperties_AbilityWoodRelease props, out float reach)
        {
            var cells = new List<IntVec3>();
            Map map = caster.Map;
            Vector3 origin = caster.Position.ToVector3Shifted();
            reach = props.length;
            for (float t = 0.5f; t <= props.length + 0.01f; t += 0.25f)
            {
                IntVec3 cell = new Vector3(origin.x + aim.x * t, 0f, origin.z + aim.y * t).ToIntVec3();
                if (cell == caster.Position) continue;
                if (!cell.InBounds(map))
                {
                    reach = t;
                    break;
                }
                if (props.stopAtWalls && Blocks(cell, map))
                {
                    reach = Mathf.Max(0.5f, t - 0.25f);
                    break;
                }
                if (!cells.Contains(cell)) cells.Add(cell);
            }
            return cells;
        }

        private static bool Blocks(IntVec3 cell, Map map)
        {
            if (cell.Impassable(map)) return true;
            Building edifice = cell.GetEdifice(map);
            return edifice != null && edifice.def.Fillage == FillCategory.Full;
        }

        /// <summary>One skewer: the hit, along the branches' direction, from Obito.</summary>
        public static void Hit(Pawn caster, Pawn victim, Vector2 aim, CompProperties_AbilityWoodRelease props)
        {
            if (victim == null || victim.Dead || !victim.Spawned) return;
            float angle = new Vector3(aim.x, 0f, aim.y).AngleFlat();
            var dinfo = new DamageInfo(props.damageDef ?? DamageDefOf.Stab, props.damage, props.armorPenetration, angle, caster,
                null, null, DamageInfo.SourceCategory.ThingOrUnknown, victim);
            victim.TakeDamage(dinfo);
        }

        /// <summary>Whether this pawn on the line is hit: never Obito, allies only with hitAllies.</summary>
        public static bool Hits(Pawn caster, Pawn victim, CompProperties_AbilityWoodRelease props) =>
            victim != caster && (props.hitAllies || victim.HostileTo(caster));
    }
}
