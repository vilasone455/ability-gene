namespace RimArt
{
    /// <summary>
    /// The beacon's throw, which is the shared hand-thrown verb and nothing else.
    ///
    /// No HighlightFieldRadiusAroundTarget override, deliberately: there is no area effect to
    /// draw. The beacon does one thing to one cell, and a ring under the cursor would promise a
    /// radius that does not exist.
    ///
    /// The projectile is not explosive, so unlike the frost bomb it declares no forced miss radius
    /// at all - which ThingDef error checking requires of a non-explosive projectile, and which
    /// makes the delivery pinpoint for free, because the base class returns the target unchanged
    /// for any radius at or under half a cell.
    /// </summary>
    public class Verb_ThrowMimicBeacon : Verb_ThrowByHand
    {
        protected override string HandTexture => "RimArt/Mimic/Beacon";
    }
}
