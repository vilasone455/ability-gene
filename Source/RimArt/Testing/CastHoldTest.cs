using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// The shared check for kit cast jobs that hold the caster after the ability fires (CastJobFail).
    /// It casts, reads the caster every tick, and checks that the job outlived the tick the ability
    /// fired on, stayed on every tick the picture still held, ended through its own toils
    /// (JobCondition.Succeeded, not Incompletable from a fail condition), and that the caster did
    /// not move before it ended, other than by the ability itself on the tick it fired.
    /// </summary>
    public static class CastHoldTest
    {
        /// <summary>
        /// <paramref name="fired"/> is the kit MapComponent's Fired(caster); it is traced and checked
        /// against the fire tick. The fire tick itself is Ability.lastCastTick, which the game sets when
        /// the warmup ends, so it is known even when the job ended on that tick. <paramref name="holds"/>
        /// gets the ticks since the cast was ordered and says whether the job must still be running then.
        /// </summary>
        public static IEnumerable<int> Run(RimArtTestContext t, Pawn caster, AbilityDef def, LocalTargetInfo target,
            Func<bool> fired, Func<int, bool> holds, int maxTicks = 300)
        {
            yield return 2;
            Ability ability = caster.abilities.GetAbility(def);
            if (!t.Check(ability != null, "the caster has " + def.defName)) yield break;
            t.Log("CanCast: " + (bool)ability.CanCast + " " + ability.CanCast.Reason);
            if (!t.Check(ability.CanCast && ability.CanApplyOn(target), def.defName + " can be cast on " + target)) yield break;

            IntVec3 from = caster.Position, stands = from;
            ability.QueueCastingJob(target, LocalTargetInfo.Invalid);
            int cast = t.Now;
            JobDef jobDef = def.jobDef;
            if (!t.Check(caster.CurJobDef == jobDef, "the cast job started (job " + caster.CurJobDef?.defName + ")")) yield break;
            JobCondition? condition = null;
            caster.jobs.curDriver.AddFinishAction(c => condition = c);

            int fireTick = -1, jobEndTick = -1, lastHeld = -1, heldTicks = 0, missedTicks = 0, movedTick = -1, firedTicks = 0, runningAfterFire = 0;
            for (int i = 0; i < maxTicks; i++)
            {
                int since = t.Now - cast;
                bool running = caster.CurJobDef == jobDef && condition == null;
                // The ability may move the caster itself (a clap swap); from then it has to stay where it landed.
                if (fireTick < 0 && ability.lastCastTick >= cast)
                {
                    fireTick = ability.lastCastTick;
                    stands = caster.Position;
                }
                else if (running && caster.Position != stands && movedTick < 0) movedTick = t.Now;
                if (!running && jobEndTick < 0) jobEndTick = t.Now;
                if (fireTick >= 0 && running)
                {
                    runningAfterFire++;
                    if (fired()) firedTicks++;
                }
                if (fireTick >= 0 && holds(since))
                {
                    lastHeld = t.Now;
                    if (running) heldTicks++;
                    else missedTicks++;
                }
                if (i % 6 == 0 || t.Now == jobEndTick)
                    t.Log(since + " | " + RimArtTestContext.Describe(caster) + " | fired " + fired() + ", holds " + holds(since));
                if (jobEndTick >= 0 && t.Now - jobEndTick >= 30) break;
                yield return 1;
            }

            t.Log("ordered " + cast + ", fired " + fireTick + " (+" + (fireTick - cast) + "), picture held until " + lastHeld
                + " (+" + (lastHeld - cast) + "), job ended " + jobEndTick + " (+" + (jobEndTick - cast) + ") with " + condition);
            t.Check(fireTick >= 0, "the ability fired");
            t.Check(firedTicks == runningAfterFire, "the kit's Fired() was true on every tick the job ran after the fire (" + firedTicks + " of " + runningAfterFire + ")");
            t.Check(jobEndTick >= 0, "the cast job ended");
            t.Check(jobEndTick > fireTick, "the cast job outlived the tick the ability fired on");
            t.Check(missedTicks == 0, "the job ran on every tick the picture held (" + heldTicks + " held, " + missedTicks + " missed)");
            t.Check(condition == JobCondition.Succeeded, "the job ended through its own toils (" + condition + ")");
            t.Check(movedTick < 0, "the caster stayed put until the job ended (" + from + (stands != from ? ", after the fire " + stands : "")
                + (movedTick < 0 ? ")" : "; moved at " + movedTick + ")"));
        }

        /// <summary>A holds function for a job that holds for a fixed time from the order: true until 2 ticks before it.</summary>
        public static Func<int, bool> For(float seconds)
        {
            int ticks = UnityEngine.Mathf.RoundToInt(seconds * 60f) - 2;
            return since => since < ticks;
        }

        /// <summary>A holds function that asks the kit's MapComponent.</summary>
        public static Func<int, bool> Asking(Func<bool> holds) => _ => holds();

        /// <summary>An undrafted colonist, so a job ended early shows as the pawn wandering off.</summary>
        public static Pawn Caster(RimArtTestContext t, ThingDef weapon = null)
        {
            Pawn caster = t.Colonist(t.center);
            if (weapon != null) t.Equip(caster, weapon);
            caster.drafter.Drafted = false;
            RimArtTestContext.Hold(caster);
            return caster;
        }
    }
}
