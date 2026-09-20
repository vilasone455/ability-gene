using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public class CompProperties_PowerPoleSweep : CompProperties_AbilityEffect
    {
        /// <summary>Tiles the pole extends to.</summary>
        public float reach = 4f;
        /// <summary>Degrees swept, centred on the aim.</summary>
        public float arc = 180f;
        public float damage = 12f;
        public float armorPenetration = 0.18f;
        public int staggerTicks = 95;

        public CompProperties_PowerPoleSweep()
        {
            compClass = typeof(CompAbilityEffect_PowerPoleSweep);
        }
    }

    /// <summary>
    /// Sweep. The pole swings through the arc in front of the caster and hits every pawn in it, ally
    /// or enemy, that no wall shields. The pole's length at each degree is the distance to the first
    /// solid thing on that line, capped at the reach: one table per cast, used by the rule and by
    /// the picture alike, so the floor outline is the true hit area. Each hit lands when the
    /// picture's pole passes the pawn (MapComponent_PowerPoleCasts).
    /// </summary>
    public class CompAbilityEffect_PowerPoleSweep : CompAbilityEffect
    {
        private const float Step = 0.2f;

        public new CompProperties_PowerPoleSweep Props => (CompProperties_PowerPoleSweep)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (target.Cell == caster.Position) return;
            Vector3 stands = caster.DrawPos;
            Vector2 toward = new Vector2(target.Cell.x - caster.Position.x, target.Cell.z - caster.Position.z).normalized;
            var victims = new List<Pawn>();
            PowerPoleSweepShot shot = Shot(caster, new Vector2(stands.x, stands.z), toward, victims);
            caster.Map.GetComponent<MapComponent_PowerPoleCasts>().LandSweep(caster, target.Cell, parent.def.verbProperties.warmupTime, shot, victims, Props);
        }

        /// <summary>The length table, and when <paramref name="victims"/> is given, the pawns the swing will hit and when.</summary>
        public PowerPoleSweepShot Shot(Pawn caster, Vector2 feet, Vector2 toward, List<Pawn> victims)
        {
            Map map = caster.Map;
            var shot = new PowerPoleSweepShot { Aim = ThunderGodTiming.Degrees(toward), Reach = Props.reach, Arc = Props.arc, Length = new float[Mathf.RoundToInt(Props.arc) + 1] };
            for (int i = 0; i < shot.Length.Length; i++)
            {
                Vector2 line = ThunderGodGraphics.Turn(shot.Aim + shot.Half - i);
                float length = Props.reach;
                for (float d = Step; d <= Props.reach; d += Step)
                {
                    IntVec3 c = new Vector3(feet.x + line.x * d, 0f, feet.y + line.y * d).ToIntVec3();
                    if (c == caster.Position) continue;
                    if (c.InBounds(map) && !c.Filled(map)) continue;
                    length = Mathf.Max(0.4f, d - Step - PowerPoleSweepTiming.WallGap);
                    break;
                }
                shot.Length[i] = length;
            }
            if (victims == null) return shot;

            var places = new List<Vector2>();
            var times = new List<float>();
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == caster || pawn.Dead) continue;
                Vector3 at = pawn.DrawPos;
                var offset = new Vector2(at.x - feet.x, at.z - feet.y);
                float range = offset.magnitude;
                if (range > Props.reach + PowerPoleSweepTiming.BodyRadius || range < 0.01f) continue;
                float phi = Mathf.DeltaAngle(shot.Aim, Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg);
                float passes = PowerPoleSweepTiming.Passes(phi, shot);
                if (passes < 0f || range > shot.LengthAt(phi) + PowerPoleSweepTiming.BodyRadius) continue;
                victims.Add(pawn);
                places.Add(offset);
                times.Add(passes);
            }
            shot.HitPlace = places.ToArray();
            shot.HitShove = new Vector2[places.Count];
            shot.HitTime = times.ToArray();
            return shot;
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn caster = parent.pawn;
            if (caster?.Map == null || !target.IsValid || target.Cell == caster.Position) return;
            Vector2 toward = new Vector2(target.Cell.x - caster.Position.x, target.Cell.z - caster.Position.z).normalized;
            Vector3 stands = caster.DrawPos;
            PowerPoleSweepShot shot = Shot(caster, new Vector2(stands.x, stands.z), toward, null);
            var cells = new List<IntVec3>();
            foreach (IntVec3 c in GenRadial.RadialCellsAround(caster.Position, Props.reach, false))
            {
                if (!c.InBounds(caster.Map)) continue;
                var offset = new Vector2(c.x - caster.Position.x, c.z - caster.Position.z);
                float phi = Mathf.DeltaAngle(shot.Aim, Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg);
                if (Mathf.Abs(phi) <= shot.Half && offset.magnitude <= shot.LengthAt(phi) + PowerPoleSweepTiming.BodyRadius) cells.Add(c);
            }
            GenDraw.DrawFieldEdges(cells);
        }
    }
}
