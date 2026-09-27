using HarmonyLib;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The Yata Mirror: while a Susanoo stands, every hit from outside is absorbed before it lands.
    /// A prefix on Pawn.PreApplyDamage, as the flame gauntlet's burn immunity is, returning at the
    /// first check for every pawn without one. What gets through: damage Itachi did to himself,
    /// a fire already burning on him, and anything that is not violence (surgery, healing).
    /// Damage with no instigator (a roof, a stray explosion) is from outside too, so it is blocked.
    ///
    /// Scatter's Thing.TakeDamage prefix runs before this one; <see cref="Scatter.Applies"/> stands
    /// down while the registry holds this pawn, so the Mirror sees the hit first in effect.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    public static class Patch_Susanoo_YataMirror
    {
        [HarmonyPriority(Priority.High)]
        public static bool Prefix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
        {
            if (SusanooRegistry.Count == 0) return true;
            HediffComp_Susanoo comp = SusanooRegistry.HolderFor(__instance);
            if (comp == null || !Blocks(__instance, dinfo)) return true;
            absorbed = true;
            comp.Blocked(dinfo);
            return false;
        }

        public static bool Blocks(Pawn pawn, DamageInfo dinfo)
        {
            if (dinfo.Def == null || !dinfo.Def.ExternalViolenceFor(pawn)) return false;
            Thing instigator = dinfo.Instigator;
            if (instigator == pawn) return false;
            if (instigator is Fire fire && fire.parent == pawn) return false;
            return true;
        }
    }
}
