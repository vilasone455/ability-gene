using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>One tinted fleck, optional: a missing def keeps the effect quiet. False Face's marks use it.</summary>
    public static class SusanooFX
    {
        internal static void Fleck(FleckDef def, Vector3 pos, Map map, float scale, Color colour)
        {
            if (def == null || map == null || !pos.ShouldSpawnMotesAt(map)) return;
            FleckCreationData data = FleckMaker.GetDataStatic(pos, map, def, scale);
            data.instanceColor = colour;
            map.flecks.CreateFleck(data);
        }
    }
}
