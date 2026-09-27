using Verse;

namespace RimArt
{
    /// <summary>
    /// The rule numbers every Shadow plexus ability shares: the light rule and what cuts a shadow
    /// line. Sits on Shikamaru's EchoDef (AG_Echo_Shikamaru), the one source of the kit. Numbers
    /// that belong to one ability (its reach, hold time, speeds) are on that ability's comp
    /// properties.
    /// </summary>
    public class ShadowPlexusExtension : DefModExtension
    {
        /// <summary>Sky light counts this much of itself; lamps and fires count full.</summary>
        public float skyShare = 0.5f;

        /// <summary>A cell lit under this is dark: no shadow line can lie on it and nothing is cast from it.</summary>
        public float darkBelow = 0.3f;

        /// <summary>Blind smoke at or above this density covers a cell and cuts a line on it.</summary>
        public float smokeDensity = 0.15f;

        /// <summary>A pawn at least this big standing on a line cuts it (a human is 1).</summary>
        public float minCrossingBodySize = 0.9f;

        private static readonly ShadowPlexusExtension Fallback = new ShadowPlexusExtension();

        public static ShadowPlexusExtension Get => ShadowPlexusDefOf.AG_Echo_Shikamaru?.GetModExtension<ShadowPlexusExtension>() ?? Fallback;
    }
}
