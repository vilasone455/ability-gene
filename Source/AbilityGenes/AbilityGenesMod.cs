using HarmonyLib;
using Verse;

namespace AbilityGenes
{
    [StaticConstructorOnStartup]
    public static class AbilityGenesMod
    {
        static AbilityGenesMod()
        {
            new Harmony("vilasone455.abilitygenes").PatchAll();
        }
    }
}
