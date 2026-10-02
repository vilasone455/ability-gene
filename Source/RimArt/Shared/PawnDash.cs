using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// A short dash (Yamato Dash, Mimicry's lunge): the pawn is drawn moving along a straight line from one cell to another
    /// while its cell changes once, on arrival (one Notify_Teleported, the RetrievalPull pattern), so the map's sections
    /// are not dirtied every tick. The kit calls <see cref="Keep"/> when the dash starts and on every tick it lasts (runs
    /// are not saved, so a cast loaded mid-dash puts its run back), then <see cref="Arrive"/> on the arrival tick, or
    /// <see cref="Stop"/> when the dash is broken off. One Pawn_DrawTracker.DrawPos postfix draws every run.
    /// </summary>
    public static class PawnDash
    {
        private sealed class Run
        {
            public Map map;
            public IntVec3 from, to;
            public int startTick, ticks;
            public bool eased;
            /// <summary>Where the pawn was last drawn, for DrawPos read off the main thread (the clock reads Unity's time).</summary>
            public Vector3 drawn;
        }

        private static readonly Dictionary<Pawn, Run> runs = new Dictionary<Pawn, Run>();

        /// <summary>
        /// The pawn dashes from <paramref name="from"/> to <paramref name="to"/> over <paramref name="ticks"/> ticks from
        /// <paramref name="startTick"/>; <paramref name="eased"/> starts and ends it slowly (smoothstep), else at one speed.
        /// Calling it again with the same numbers changes nothing.
        /// </summary>
        public static void Keep(Pawn pawn, IntVec3 from, IntVec3 to, int startTick, int ticks, bool eased)
        {
            if (pawn == null || !pawn.Spawned) return;
            if (!runs.ContainsKey(pawn)) DropOverdue();
            if (!runs.TryGetValue(pawn, out Run run))
            {
                run = new Run { drawn = from.ToVector3Shifted() };
                runs[pawn] = run;
            }
            run.map = pawn.Map;
            run.from = from;
            run.to = to;
            run.startTick = startTick;
            run.ticks = Mathf.Max(1, ticks);
            run.eased = eased;
        }

        public static bool Running(Pawn pawn) => pawn != null && runs.ContainsKey(pawn);

        /// <summary>The share of the way a dash of <paramref name="seconds"/> has gone <paramref name="age"/> seconds in, 0 to 1.</summary>
        public static float Travel(float age, float seconds, bool eased)
        {
            float t = Mathf.Clamp01(age / Mathf.Max(0.001f, seconds));
            return eased ? VfxMath.Smooth(t) : t;
        }

        /// <summary>Where a pawn <paramref name="travel"/> of the way from one cell's centre to the other's is drawn.</summary>
        public static Vector3 At(IntVec3 from, IntVec3 to, float travel) => Vector3.Lerp(from.ToVector3Shifted(), to.ToVector3Shifted(), travel);

        /// <summary>
        /// The dash is over: the run is dropped and the pawn moves to <paramref name="to"/> if it still can. It stays where it
        /// is when it has died, gone down, left <paramref name="home"/>, or the cell is out of bounds or not standable, and,
        /// with <paramref name="needEmpty"/>, when another pawn stands there now. True when it moved.
        /// </summary>
        public static bool Arrive(Pawn pawn, Map home, IntVec3 to, bool needEmpty)
        {
            Stop(pawn);
            if (pawn == null || !pawn.Spawned || pawn.Map != home || pawn.Dead || pawn.Downed) return false;
            if (!to.InBounds(home) || !to.Standable(home) || to == pawn.Position) return false;
            if (needEmpty)
            {
                Pawn standing = to.GetFirstPawn(home);
                if (standing != null && standing != pawn) return false;
            }
            pawn.Position = to;
            pawn.Notify_Teleported(false, true);
            return true;
        }

        /// <summary>Forgets runs nobody ended, before a new one is added: a cast dropped by a load, a game left mid-dash.</summary>
        private static void DropOverdue()
        {
            int now = Find.TickManager.TicksGame;
            overdue.Clear();
            foreach (KeyValuePair<Pawn, Run> entry in runs)
                if (Overdue(entry.Value, now) || entry.Key.Map != entry.Value.map) overdue.Add(entry.Key);
            for (int i = 0; i < overdue.Count; i++) runs.Remove(overdue[i]);
        }

        /// <summary>The dash is broken off: the pawn is drawn on its own cell again.</summary>
        public static void Stop(Pawn pawn)
        {
            if (pawn != null) runs.Remove(pawn);
        }

        /// <summary>Ticks past its end a run is still drawn when nobody called Arrive or Stop (a cast dropped by a load).</summary>
        private const int Grace = 30;

        private static bool Overdue(Run run, int now) => now > run.startTick + run.ticks + Grace;

        private static readonly List<Pawn> overdue = new List<Pawn>();

        /// <summary>
        /// Where the pawn is drawn while it dashes; false when it is not dashing on the map it is on. On the main thread the
        /// point is worked out from the clock and kept; a render thread reads the kept one. A run <see cref="Grace"/> ticks
        /// past its end is not drawn, so a pawn is never drawn away from its cell for good. Nothing is removed here: pawns
        /// are drawn partly on worker threads, so the table changes only in the tick calls.
        /// </summary>
        internal static bool TryDrawAt(Pawn pawn, out Vector3 at)
        {
            at = default;
            if (runs.Count == 0 || !runs.TryGetValue(pawn, out Run run) || pawn.Map != run.map) return false;
            if (Overdue(run, Find.TickManager.TicksGame)) return false;
            if (UnityData.IsInMainThread) run.drawn = At(run.from, run.to, Travel(PictureClock.Since(run.startTick), run.ticks / 60f, run.eased));
            at = run.drawn;
            return true;
        }
    }

    [HarmonyPatch(typeof(Pawn_DrawTracker), nameof(Pawn_DrawTracker.DrawPos), MethodType.Getter)]
    public static class Patch_PawnDrawTracker_PawnDash
    {
        public static void Postfix(Pawn ___pawn, ref Vector3 __result)
        {
            if (!PawnDash.TryDrawAt(___pawn, out Vector3 at)) return;
            __result.x = at.x;
            __result.z = at.z;
        }
    }
}
