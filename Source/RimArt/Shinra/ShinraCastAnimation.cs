namespace RimArt
{
    /// <summary>Shinra Tensei's push clip: one pawn, empty hands, run free (see
    /// <see cref="CastClips"/>). The clip carries its own facing. A centred wave has no direction
    /// to mirror, so the caster's rotation is not read. Its body and hands are read each frame for
    /// the sleeves (<see cref="ShinraSleeves"/>).</summary>
    public static class ShinraCastAnimation
    {
        public static readonly CastClips Clip = new CastClips("Shinra Tensei", CastClips.Needs.Clock | CastClips.Needs.Parts, "AG_ShinraPush");
    }
}
