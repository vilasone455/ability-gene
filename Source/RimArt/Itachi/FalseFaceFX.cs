using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// False Face has no lab sketch; this is the small effect agreed for the port: a red glint at
    /// Itachi's eyes when he casts, a red flash and a burst of crow feathers on each victim, a
    /// small red mark over each victim's head while the genjutsu lasts, and two feathers when it
    /// breaks. The feathers are the dispersal kit's own, so the two abilities read as one body.
    /// </summary>
    public static class FalseFaceFX
    {
        private static readonly Color Red = new Color(0.85f, 0.1f, 0.15f);

        public static void Caster(Pawn itachi)
        {
            if (itachi?.Map == null) return;
            SusanooFX.Fleck(ItachiDefOf.AG_ItachiGlint, itachi.DrawPos + Vector3.forward * 0.35f, itachi.Map, 0.9f, Red);
        }

        public static void Victim(Pawn victim)
        {
            if (victim?.Map == null) return;
            SusanooFX.Fleck(ItachiDefOf.AG_ItachiFlash, victim.DrawPos + Vector3.forward * 0.3f, victim.Map, 1.6f, Red);
            DispersalFX.Feathers(victim.DrawPos, victim.Map, 7, 0.9f);
        }

        public static void Mark(Pawn victim)
        {
            if (victim?.Map == null || !victim.Spawned) return;
            SusanooFX.Fleck(ItachiDefOf.AG_ItachiFalseFaceMark, victim.DrawPos + Vector3.forward * 0.95f, victim.Map, 0.5f, Red);
        }

        public static void End(Pawn victim)
        {
            if (victim?.Map == null || !victim.Spawned) return;
            DispersalFX.Feathers(victim.DrawPos, victim.Map, 2, 0.4f);
        }
    }
}
