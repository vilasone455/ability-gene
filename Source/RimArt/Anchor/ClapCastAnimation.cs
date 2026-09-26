using Verse;

namespace RimArt
{
    /// <summary>
    /// Optional Melee Animation bridge for the clap and the double clap: one pawn, empty hands, one
    /// facing-free clip each. Without that mod nothing here does anything and the clap casts with
    /// no gesture.
    ///
    /// The clip is job-owned (see <see cref="CastClips"/>): it runs inside the pawn's own cast job,
    /// and the cast job owns the clock. The pawn changes cell in the middle of the clip, so the
    /// clip's root is moved after them (<see cref="MoveTo"/>); a clip otherwise stays drawn where it
    /// started.
    /// </summary>
    public static class ClapCastAnimation
    {
        private static readonly CastClips Clips = new CastClips("The clap", CastClips.Needs.Job | CastClips.Needs.Root, "AG_Clap", "AG_ClapTwice");

        public static bool Present => Clips.Present;

        public static bool TryStart(Pawn pawn, bool twice, JobDef job) => Clips.TryStart(pawn, twice ? 1 : 0, job, out _);

        /// <summary>False when the pawn has no clip of this job, which is also how a clip that was never started reads.</summary>
        public static bool Seek(Pawn pawn, JobDef job, float seconds) => Clips.Seek(pawn, job, seconds);

        /// <summary>Puts the clip where the pawn now stands. Call after the pawn's Position has changed.</summary>
        public static void MoveTo(Pawn pawn, JobDef job) => Clips.MoveTo(pawn, job);

        public static void Stop(Pawn pawn, JobDef job) => Clips.Stop(pawn, job);
    }
}
