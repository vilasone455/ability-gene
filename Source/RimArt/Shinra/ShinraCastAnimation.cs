namespace RimArt
{
    /// <summary>Shinra Tensei's push clip: one pawn, empty hands, run free (see
    /// <see cref="CastClips"/>). The clip carries its own facing. A centred wave has no direction
    /// to mirror, so the caster's rotation is not read.</summary>
    public static class ShinraCastAnimation
    {
        public static readonly CastClips Clip = new CastClips("Shinra Tensei", CastClips.Needs.Clock, "AG_ShinraPush");
    }
}
