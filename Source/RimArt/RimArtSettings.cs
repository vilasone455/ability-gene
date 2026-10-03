using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>The mod's settings (Options, Mod settings, RimArts).</summary>
    public class RimArtSettings : ModSettings
    {
        /// <summary>Unlimited Blade Works plays its reveal shot when the world opens: 4.6 s, the game paused, any key skips it.</summary>
        public bool ubwRevealShot = true;

        private static readonly RimArtSettings fallback = new RimArtSettings();

        /// <summary>The settings in use; the defaults before the mod has read them.</summary>
        internal static RimArtSettings Get => RimArtSettingsMod.settings ?? fallback;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ubwRevealShot, "ubwRevealShot", true);
        }
    }

    /// <summary>Gives the settings their page in the game's options.</summary>
    public class RimArtSettingsMod : Mod
    {
        internal static RimArtSettings settings;

        public RimArtSettingsMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<RimArtSettings>();
        }

        public override string SettingsCategory() => "RimArts";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var list = new Listing_Standard();
            list.Begin(inRect);
            list.CheckboxLabeled("Unlimited Blade Works: reveal shot", ref settings.ubwRevealShot,
                "When the world opens, a 4.6 s camera shot looks up at the sky and its gears, tilts down over the plain while the fire runs out, and settles into the usual view. The game is paused while it plays; any key or click skips it.");
            list.End();
        }
    }
}
