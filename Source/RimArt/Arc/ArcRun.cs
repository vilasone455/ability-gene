using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// One cast of the arc, from the moment it leaves to the moment it runs out of people.
    ///
    /// The run is a two-phase loop and both phases are one line of state. Dash puts the carrier
    /// beside somebody and remembers who; strike, ten ticks later, throws the blow and waits out
    /// however long the animation says it takes. Everything else - the beat between people, the
    /// afterimages, the recoil - hangs off those two moments.
    ///
    /// It is scribed because a fight can be saved in the middle of one. What is not scribed is
    /// the afterimages, which are a fraction of a second of decoration and would be wrong on the
    /// other side of a reload anyway.
    /// </summary>
    public class ArcRun : IExposable
    {
        private Pawn carrier;
        private List<Pawn> chain = new List<Pawn>();
        private int index;
        private int waitTicks;
        private int hops;
        private int animationWait;

        private Pawn pendingVictim;
        private bool pendingFlipX;
        private bool pendingAnimated;

        private readonly List<Ghost> ghosts = new List<Ghost>();
        private Vector3 streakFrom;
        private Vector3 streakTo;
        private int streakTicks;

        public ArcRun() { }

        public ArcRun(Pawn carrier, List<Pawn> chain)
        {
            this.carrier = carrier;
            this.chain = chain;
        }

        public Pawn Carrier => carrier;

        /// <returns>False when the run is over and should be dropped.</returns>
        public bool Tick(Map map)
        {
            TickTrail();

            if (carrier == null || carrier.Dead || !carrier.Spawned || carrier.Downed || carrier.Map != map)
                return false;

            if (waitTicks > 0)
            {
                waitTicks--;
                return true;
            }

            if (pendingVictim != null)
            {
                Strike();
                return true;
            }

            // The previous strike has to be finished before the next arc leaves, or the carrier
            // would be teleported out of an animation that is still holding them.
            if (MeleeAnimation.IsAnimating(carrier))
            {
                animationWait++;
                return animationWait <= ArcDefaults.AnimationWaitLimitTicks;
            }

            animationWait = 0;
            return Dash(map);
        }

        /// <summary>
        /// Steps to the next person the chain named, skipping anyone who has stopped being a
        /// target since the cast - someone else killed them, they went down, they are inside
        /// another animation. The arc does not look for a replacement: the chain was decided
        /// when it was cast, and it spends what it was given.
        /// </summary>
        private bool Dash(Map map)
        {
            while (index < chain.Count)
            {
                Pawn victim = chain[index++];
                if (!ArcUtility.IsStrikeable(carrier, victim)) continue;
                if (!ArcUtility.TryFindLanding(carrier, victim, out IntVec3 cell, out bool flipX, out bool animated)) continue;

                if (cell != carrier.Position)
                {
                    Vector3 from = carrier.DrawPos;
                    carrier.Position = cell;
                    carrier.Notify_Teleported(true, true);
                    Vector3 to = carrier.DrawPos;

                    ArcFX.Depart(from, to, map);
                    ArcFX.Trail(from, to, map);
                    ArcFX.Arrive(to, map);
                    AddTrail(from, to);
                }

                // Arrive looking at them, and stay put. Notify_Teleported drops whatever job the
                // carrier had, which leaves them free to wander off in the beat before the strike
                // and get yanked back mid-step; a short wait holds them still until the animation
                // takes over, and expires on its own if the arc is cut short.
                carrier.rotationTracker?.FaceTarget(victim);
                Hold(ArcDefaults.StrikeDelayTicks + ArcDefaults.HoldMarginTicks);

                pendingVictim = victim;
                pendingFlipX = flipX;
                pendingAnimated = animated;
                waitTicks = ArcDefaults.StrikeDelayTicks;
                return true;
            }

            return false;
        }

        private void Hold(int ticks)
        {
            if (carrier.jobs == null) return;

            Job wait = JobMaker.MakeJob(JobDefOf.Wait);
            wait.expiryInterval = ticks;
            carrier.jobs.StartJob(wait, JobCondition.InterruptForced);
        }

        /// <summary>
        /// The blow. Recoil is charged here rather than at the end of the run, so an arc that is
        /// cut short by its carrier being shot has still cost exactly what it delivered.
        /// </summary>
        private void Strike()
        {
            Pawn victim = pendingVictim;
            pendingVictim = null;

            if (!ArcUtility.IsStrikeable(carrier, victim)) return;

            hops++;
            if (ArcDefOf.AG_ArcRecoil != null)
                HealthUtility.AdjustSeverity(carrier, ArcDefOf.AG_ArcRecoil, ArcDefaults.RecoilPerHop);

            if (pendingAnimated && MeleeAnimation.TryStrike(carrier, victim, pendingFlipX, out int durationTicks))
            {
                waitTicks = durationTicks + ArcDefaults.HopGapTicks;
                return;
            }

            // No animation for what this carrier is holding, or nowhere to stand for one. The
            // arc still arrived, so it still swings - with the carrier's own melee verb, which
            // is the same swing they would have thrown standing there anyway.
            carrier.meleeVerbs?.TryMeleeAttack(victim);
            waitTicks = ArcDefaults.UnanimatedHopTicks;
        }

        /// <summary>
        /// The streak, then solid copies of the carrier along the line it just crossed.
        ///
        /// The three phases are the whole reason this works. As of 1.6 a pawn is drawn from
        /// results the render tree computed earlier in the frame for wherever that pawn actually
        /// is, so calling RenderPawnAt with a position of your own draws nothing new - the first
        /// version of this did exactly that and produced no afterimages at all. Forcing
        /// EnsureInitialized and ParallelPreDraw at the ghost position first is what re-points
        /// those results, and it is the same thing Melee Animation's own render patch does to put
        /// a pawn somewhere the game did not expect.
        ///
        /// The results are then pushed back to the carrier's real position, so nothing else that
        /// draws them this frame inherits the last ghost's.
        ///
        /// Like the time lattice's afterimages these are solid: PawnRenderer exposes no alpha, so
        /// the trail thins by losing ghosts rather than by fading them.
        /// </summary>
        public void Draw()
        {
            if (carrier == null || !carrier.Spawned) return;

            if (streakTicks > 0)
                ArcGraphics.DrawStreak(streakFrom, streakTo, streakTicks / (float)ArcDefaults.StreakTicks);

            if (ghosts.Count == 0) return;

            PawnRenderer renderer = carrier.Drawer?.renderer;
            if (renderer == null) return;

            for (int i = 0; i < ghosts.Count; i++)
            {
                Vector3 position = ghosts[i].position;
                position.y = AltitudeLayer.Pawn.AltitudeFor() - 0.01f * (i + 1);
                DrawCarrierAt(renderer, position, true);
            }

            DrawCarrierAt(renderer, carrier.DrawPos, false);
        }

        private static void DrawCarrierAt(PawnRenderer renderer, Vector3 position, bool draw)
        {
            renderer.DynamicDrawPhaseAt(DrawPhase.EnsureInitialized, position, null, true);
            renderer.DynamicDrawPhaseAt(DrawPhase.ParallelPreDraw, position, null, true);
            if (draw) renderer.DynamicDrawPhaseAt(DrawPhase.Draw, position, null, true);
        }

        /// <summary>
        /// One dash's worth of trail. Ghosts are spaced evenly along the line and given lifetimes
        /// by their place in it, so the trail retracts toward the carrier instead of blinking out
        /// all at once.
        /// </summary>
        private void AddTrail(Vector3 from, Vector3 to)
        {
            streakFrom = from;
            streakTo = to;
            streakTicks = ArcDefaults.StreakTicks;

            ghosts.Clear();
            for (int i = 0; i < ArcDefaults.GhostCount; i++)
            {
                float along = (i + 1f) / (ArcDefaults.GhostCount + 1f);
                ghosts.Add(new Ghost
                {
                    position = Vector3.Lerp(from, to, along),
                    ticksLeft = Mathf.Max(1, ArcDefaults.GhostTicks * (i + 1) / ArcDefaults.GhostCount)
                });
            }
        }

        private void TickTrail()
        {
            if (streakTicks > 0) streakTicks--;

            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                Ghost ghost = ghosts[i];
                ghost.ticksLeft--;
                if (ghost.ticksLeft <= 0) ghosts.RemoveAt(i);
                else ghosts[i] = ghost;
            }
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref carrier, "carrier");
            Scribe_Collections.Look(ref chain, "chain", LookMode.Reference);
            Scribe_References.Look(ref pendingVictim, "pendingVictim");
            Scribe_Values.Look(ref index, "index");
            Scribe_Values.Look(ref waitTicks, "waitTicks");
            Scribe_Values.Look(ref hops, "hops");
            Scribe_Values.Look(ref animationWait, "animationWait");
            Scribe_Values.Look(ref pendingFlipX, "pendingFlipX");
            Scribe_Values.Look(ref pendingAnimated, "pendingAnimated");

            if (Scribe.mode == LoadSaveMode.PostLoadInit && chain == null)
                chain = new List<Pawn>();
        }

        private struct Ghost
        {
            public Vector3 position;
            public int ticksLeft;
        }
    }
}
