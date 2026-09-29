using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Chibaku Tensei's Release button, after the ability's own while Pain's ball holds (from the moment it has formed
    /// until its seams open): the ball cracks now and bursts <see cref="ChibakuBall.CrackTime"/> later
    /// (<see cref="MapComponent_ChibakuPlates.Release"/>). The cooldown and charge were spent at the cast and stay spent.
    /// </summary>
    [HarmonyPatch(typeof(Ability), nameof(Ability.GetGizmos))]
    public static class Patch_ChibakuRelease
    {
        public const string Label = "Release";

        public static void Postfix(Ability __instance, ref IEnumerable<Command> __result)
        {
            if (__instance.def == PainDefOf.AG_PainChibakuTensei) __result = WithRelease(__result, __instance);
        }

        private static IEnumerable<Command> WithRelease(IEnumerable<Command> own, Ability ability)
        {
            foreach (Command c in own) yield return c;
            Pawn pawn = ability.pawn;
            // Only the first source of the ability supplies the button.
            if (pawn?.abilities?.AllAbilitiesForReading.FirstOrDefault(a => a.def == ability.def) != ability) yield break;
            MapComponent_ChibakuPlates component = MapComponent_ChibakuPlates.Of(pawn.Map);
            if (component == null || !component.CanRelease(pawn)) yield break;
            float left = component.HoldLeft(pawn) - ChibakuBall.CrackTime;
            yield return new Command_Action
            {
                defaultLabel = Label,
                defaultDesc = "Let the ball go now instead of in " + left.ToString("0.0") + " s: it cracks and bursts " + ChibakuBall.CrackTime.ToString("0.0")
                    + " s later. Those inside take the crush only for the seconds they were held, plus the fall. Shinra Tensei and Banshō Ten'in are free after the burst. The cooldown and charge stay spent.",
                icon = ability.def.uiIcon,
                groupable = false,
                action = () => component.Release(pawn),
            };
        }
    }
}
