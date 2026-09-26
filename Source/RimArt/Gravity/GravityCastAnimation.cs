namespace RimArt
{
    /// <summary>Gravity Well's dedicated one-hand gather and clench clip, run free (see
    /// <see cref="CastClips"/>). The authored south-facing hand stays visible through the
    /// gathering pose.</summary>
    public static class GravityCastAnimation
    {
        public static readonly CastClips Clip = new CastClips("Gravity Well", CastClips.Needs.Clock, "AG_GravityChannel");
    }
}
