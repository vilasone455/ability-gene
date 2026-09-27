using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// What the Susanoo mechanic shows until its picture is ported (PR 2): a flash on the Mirror
    /// side of a blocked hit, a glint at a stab, a larger one where a target was sealed. Every
    /// fleck is optional, so a missing def keeps the mechanic quiet rather than throwing inside
    /// a damage prefix.
    /// </summary>
    public static class SusanooFX
    {
        private static readonly Color Orange = new Color(1f, 0.55f, 0.2f);
        private static readonly Color Ember = new Color(0.9f, 0.25f, 0.1f);

        public static void Block(Pawn pawn, DamageInfo dinfo)
        {
            if (pawn?.Map == null) return;
            Vector3 dir = Vector3.zero;
            if (dinfo.Instigator != null && dinfo.Instigator.Spawned)
                dir = (dinfo.Instigator.DrawPos - pawn.DrawPos).normalized;
            else if (dinfo.Angle >= 0f)
                dir = Quaternion.AngleAxis(dinfo.Angle, Vector3.up) * Vector3.forward * -1f;
            Fleck(ItachiDefOf.AG_ItachiFlash, pawn.DrawPos + dir * 0.9f + Vector3.forward * 0.4f, pawn.Map, 1.3f, Orange);
        }

        public static void Stab(Pawn pawn, Pawn target, bool sealedTarget, Vector3? at = null, Map map = null)
        {
            map = map ?? pawn?.Map;
            if (map == null) return;
            Vector3 pos = at ?? target?.DrawPos ?? pawn.DrawPos;
            if (sealedTarget)
            {
                Fleck(ItachiDefOf.AG_ItachiFlash, pos + Vector3.forward * 0.3f, map, 2.2f, Ember);
                Fleck(ItachiDefOf.AG_ItachiGlint, pos + Vector3.forward * 0.8f, map, 1.4f, Orange);
            }
            else
            {
                Fleck(ItachiDefOf.AG_ItachiGlint, pos + Vector3.forward * 0.5f, map, 1f, Orange);
            }
        }

        internal static void Fleck(FleckDef def, Vector3 pos, Map map, float scale, Color colour)
        {
            if (def == null || map == null || !pos.ShouldSpawnMotesAt(map)) return;
            FleckCreationData data = FleckMaker.GetDataStatic(pos, map, def, scale);
            data.instanceColor = colour;
            map.flecks.CreateFleck(data);
        }
    }
}
