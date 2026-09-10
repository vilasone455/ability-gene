using HarmonyLib;
using Verse;

namespace RimArt
{
    [StaticConstructorOnStartup]
    public static class RimArtMod
    {
        static RimArtMod()
        {
            new Harmony("vilasone455.rimart").PatchAll();
        }
    }
}
