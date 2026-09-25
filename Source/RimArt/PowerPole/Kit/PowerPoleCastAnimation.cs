using System.Linq;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Optional Melee Animation bridge for the pole's casts: one pawn, two hands on a pole that
    /// RimArt draws itself. Without that mod nothing here does anything and the pawn stands still
    /// while the pole moves.
    ///
    /// Like ClapCastAnimation, the clip is job-owned (see <see cref="CastClips"/>): it runs inside
    /// the pawn's own cast job, and the cast job owns the clock. Like the throws, each clip is
    /// authored for one direction and ThrowAimWorker turns the hands by what is left to the real
    /// aim (<see cref="ThrowAim"/>).
    /// There are four clips per ability and none is mirrored: a mirrored Sweep would swing right to
    /// left while the pole swings left to right.
    /// </summary>
    public static class PowerPoleCastAnimation
    {
        private static readonly string[] Facings = { "East", "North", "South", "West" };
        private static readonly float[] Turns = { 0f, 90f, -90f, 180f };
        private static readonly string[] Prefixes = { "AG_PoleThrust", "AG_PoleSweep", "AG_PolePlant" };

        /// <summary>Clip number kind * 4 + facing.</summary>
        private static readonly CastClips Clips = new CastClips("The Power Pole", CastClips.Needs.Job,
            Prefixes.SelectMany(prefix => Facings.Select(facing => prefix + facing)).ToArray());

        public static bool Present => Clips.Present;

        /// <summary><paramref name="toward"/> is the unit direction of the cast. False when nothing plays; the cast goes on without a gesture.</summary>
        public static bool TryStart(Pawn pawn, PowerPoleCastKind kind, Vector2 toward, JobDef job) => Start(pawn, kind, toward, job, out _);

        /// <summary>As <see cref="TryStart"/>, and says which clip started or why none did. For finding out why a cast has no gesture.</summary>
        public static bool Start(Pawn pawn, PowerPoleCastKind kind, Vector2 toward, JobDef job, out string why)
        {
            if (!Clips.Present) { why = Clips.Missing; return false; }
            if (!Clips.CanAnimate(pawn)) { why = "the pawn cannot be animated now (not humanlike, downed, or already in an animation)"; return false; }
            // The dominant axis picks the clip, ties going to east and west, as RimWorld picks a pawn's facing.
            int facing = Mathf.Abs(toward.x) >= Mathf.Abs(toward.y) ? (toward.x >= 0f ? 0 : 3) : (toward.y > 0f ? 1 : 2);
            int clip = (int)kind * Facings.Length + facing;
            if (!Clips.TryStart(pawn, clip, job, out object renderer))
            {
                why = Clips.Present ? "Melee Animation refused " + Clips.DefName(clip) : "the bridge threw";
                return false;
            }
            ThrowAim.Set(renderer, Mathf.DeltaAngle(Turns[facing], Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg));
            why = Clips.DefName(clip);
            return true;
        }

        /// <summary>False when the pawn has no clip of this job, which is also how a clip that was never started reads.</summary>
        public static bool Seek(Pawn pawn, JobDef job, float seconds) => Clips.Seek(pawn, job, seconds);

        public static void Stop(Pawn pawn, JobDef job) => Clips.Stop(pawn, job);
    }
}
