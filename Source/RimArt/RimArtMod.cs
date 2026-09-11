using HarmonyLib;
using Verse;

namespace RimArt
{
    [StaticConstructorOnStartup]
    public static class RimArtMod
    {
        static RimArtMod()
        {
            Harmony harmony = new Harmony("vilasone455.rimart");
            harmony.PatchAll();

            // Combat Extended's rounds are not Verse.Projectile, so nothing PatchAll just applied
            // touches them. This looks for CE and, if it is there, teaches the three kits that act
            // on rounds in flight how to read and write its ones. A no-op when it is not.
            CombatExtendedRounds.Install(harmony);
        }
    }
}
