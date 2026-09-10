using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// Plain constants, deliberately kept in a class that loads no Unity resources.
    ///
    /// Def parsing happens on a background thread, and CompProperties field initializers
    /// run there via Activator.CreateInstance. Anything those initializers touch has its
    /// static constructor fired on that thread. If that constructor loads a texture, the
    /// game logs "Tried to get a resource from a different thread" and the material is
    /// left broken for the rest of the session.
    ///
    /// So defaults referenced by CompProperties live here, and never alongside a Material.
    /// </summary>
    public static class TimeBubbleDefaults
    {
        /// <summary>
        /// Every vanilla force field is desaturated - bullet shield and the mech shields
        /// are (0.4, 0.4, 0.4), the broadshield projector (0.6, 0.6, 0.8) - and they idle
        /// around alpha 0.2. This sits just off white with a blue bias so it reads as ice
        /// rather than as another bullet shield, and stays only slightly more solid than
        /// vanilla because the player has to see exactly who got caught.
        /// </summary>
        public static readonly Color DomeColor = new Color(0.75f, 0.90f, 1f, 0.40f);
    }
}
