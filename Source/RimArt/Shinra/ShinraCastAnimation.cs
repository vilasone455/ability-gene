namespace RimArt
{
    /// <summary>Shinra Tensei's clips: one pawn, empty hands, run free (see <see cref="CastClips"/>). Each
    /// carries its own facing. A centred wave has no direction to mirror, so the caster's rotation is
    /// not read. Their body and hands are read each frame for the sleeves (<see cref="ShinraSleeves"/>).
    /// AG_ShinraPush, the clip before the tap/hold button, stays defined for saves made during one.</summary>
    public static class ShinraCastAnimation
    {
        public const int Tap = 0, Charge = 1;
        public static readonly CastClips Clip = new CastClips("Shinra Tensei", CastClips.Needs.Clock | CastClips.Needs.Parts, "AG_ShinraTap", "AG_ShinraCharge");
    }
}
