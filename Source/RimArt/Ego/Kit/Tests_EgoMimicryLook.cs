using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using static RimArt.RimArtTestContext;
using static RimArt.Tests_EgoMimicry;

namespace RimArt
{
    /// <summary>
    /// Screenshots of Mimicry's picture on a real pawn, filter <c>ego: mimicry: look</c>: the held sword, a swing and the grown
    /// slam facing each way, and the arm at every stage. Read the shots, not the checks: the checks only say each moment was
    /// reached.
    /// </summary>
    public static class Tests_EgoMimicryLook
    {
        private static GameComponent_EgoMimicry Game => Tests_EgoMimicry.Game;

        private static readonly Rot4[] Facings = { Rot4.North, Rot4.East, Rot4.South, Rot4.West };

        /// <summary>Drafted and facing <paramref name="rot"/>: a drafted pawn shows its weapon, and the job's facing keeps Core from turning it south.</summary>
        private static void Hold(Pawn pawn, Rot4 rot)
        {
            pawn.drafter.Drafted = true;
            Job wait = JobMaker.MakeJob(JobDefOf.Wait_Combat);
            wait.overrideFacing = rot;
            wait.expiryInterval = 900;
            pawn.jobs.StartJob(wait, JobCondition.InterruptForced);
            pawn.Rotation = rot;
        }

        [RimArtTest("Ego", "mimicry: look: the held sword, a swing and the grown slam facing each way, and the arm at every stage (screenshots)", 6000)]
        public static IEnumerable<int> Look(RimArtTestContext t)
        {
            t.Clear();
            Game.Clear();
            float grow = Props.growChance;
            try
            {
                IntVec3 c = t.center;
                Pawn wielder = t.Colonist(c);
                CompEgoMimicry sword = Arm(t, wielder);
                foreach (Rot4 rot in Facings)
                {
                    Hold(wielder, rot);
                    yield return 2;
                    t.Check(wielder.Rotation == rot, "facing " + rot.ToStringHuman());
                    yield return t.ShotAs("mimicry-held-" + rot.ToStringHuman().ToLower(), c, 3f);
                }
                // A swing and a slam at a stunned hostile on each side; the facing job ends first, or it keeps turning the pawn.
                wielder.jobs.EndCurrentJob(JobCondition.InterruptForced);
                foreach (bool grown in new[] { false, true })
                {
                    Props.growChance = grown ? 1f : 0f;
                    foreach (Rot4 rot in Facings)
                    {
                        Pawn foe = t.Target(c + new IntVec3(0, 0, -6), stunTicks: 3000);
                        var casts = new List<EgoMimicryCast>();
                        foreach (int w in StrikeAt(t, wielder, foe, c + rot.FacingCell, casts)) yield return w;
                        EgoMimicryCast cast = casts[0];
                        if (!t.Check(cast != null, "a strike facing " + rot.ToStringHuman())) yield break;
                        int at = cast.swingTick + EgoMimicryCast.Ticks(grown ? EgoMimicryTiming.SlamAt : EgoMimicryTiming.HitAt) - 2;
                        foreach (int w in WaitFor(() => t.Now >= at, 60)) yield return w;
                        t.Log((grown ? "slam " : "swing ") + rot.ToStringHuman() + ": " + Line(cast) + ", facing " + wielder.Rotation.ToStringHuman());
                        yield return t.ShotAs("mimicry-" + (grown ? "slam-" : "swing-") + rot.ToStringHuman().ToLower(), c, grown ? 4f : 3f);
                        foreach (int w in WaitFor(() => cast.resolved, 30)) yield return w;
                        t.Check(cast.resolved && cast.grown == grown, (grown ? "a slam " : "a swing ") + "facing " + rot.ToStringHuman() + " landed");
                        // Gone before the wielder's cooldown ends, or its Wait job would punch it.
                        Tests_EgoMimicryHunt.Remove(foe);
                        foreach (int w in WaitFor(() => !Game.Busy(wielder), 120)) yield return w;
                    }
                }

                // The arm: corroded alone on the map, it stands; each stage set by hand, then stage 4 facing each way.
                Props.growChance = grow;
                foreach (int w in WaitFor(() => !Game.Busy(wielder), 120)) yield return w;
                wielder.Rotation = Rot4.South;
                EgoCorrosion.Corrode(wielder, sword);
                yield return 60;
                for (int stage = 1; stage <= 4; stage++)
                {
                    sword.stage = stage;
                    wielder.Rotation = Rot4.South;
                    yield return 40;
                    t.Log("stage " + stage + ": drawn " + Game.LookOf(wielder)?.Stage.ToString("0.00") + ", " + RimArtTestContext.Describe(wielder));
                    yield return t.ShotAs("mimicry-arm-" + stage, c, 3f);
                }
                foreach (Rot4 rot in Facings)
                {
                    wielder.Rotation = rot;
                    yield return 2;
                    yield return t.ShotAs("mimicry-arm-4-" + rot.ToStringHuman().ToLower(), c, 3f);
                }
                wielder.Rotation = Rot4.South;
                wielder.MentalState?.RecoverFromState();
                yield return 25;
                t.Log("receding: drawn " + Game.LookOf(wielder)?.Stage.ToString("0.00"));
                t.Check(sword.stage == 0 && Game.LookOf(wielder) != null && Game.LookOf(wielder).Stage > 0f, "after the state the arm recedes (drawn, not gone at once)");
                yield return t.ShotAs("mimicry-arm-receding", c, 3f);
                yield return 60;
                t.Check(Game.LookOf(wielder) == null, "and is gone a second later");
            }
            finally
            {
                Props.growChance = grow;
            }
        }
    }
}
