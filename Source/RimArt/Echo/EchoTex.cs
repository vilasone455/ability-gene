using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Every Echo texture. Kept out of ITab_EchoDevice: inspector tabs are created while defs load on
    /// a worker thread, and a texture made there fails ("Tried to create a texture from a different
    /// thread"), which left every bar in the device tab black.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class EchoTex
    {
        public static readonly Texture2D Charge = SolidColorMaterials.NewSolidColorTexture(new Color(0.35f, 0.62f, 0.95f));
        public static readonly Texture2D ChargeLow = SolidColorMaterials.NewSolidColorTexture(new Color(0.9f, 0.35f, 0.25f));
        public static readonly Texture2D TrialDone = SolidColorMaterials.NewSolidColorTexture(new Color(0.35f, 0.75f, 0.35f));
        public static readonly Texture2D TrialOpen = SolidColorMaterials.NewSolidColorTexture(new Color(0.85f, 0.7f, 0.25f));
        public static readonly Texture2D Manifest = ContentFinder<Texture2D>.Get("RimArt/Echo/IconManifest");
        public static readonly Texture2D Trials = ContentFinder<Texture2D>.Get("RimArt/Echo/IconTrials");
    }
}
