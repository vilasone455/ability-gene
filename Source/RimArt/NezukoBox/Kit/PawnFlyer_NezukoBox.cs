using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The pawn leaping out of the box. Core's flyer moves it along the ground (the def's progressCurve is
    /// the sketch's smoothstep) with no height of its own (heightFactor 0); this draws it raised the way
    /// nezuko-box-come-out.js raises its stand-in: from the box top down to the floor, plus a sine arc
    /// to the peak, lifted SixPathsHeight.Lift per cell. On landing it strikes (<see cref="NezukoBoxStrike"/>).
    /// </summary>
    public class PawnFlyer_NezukoBox : PawnFlyer
    {
        private int stunTicks = 90;
        private bool draft;
        private float startHeight = NezukoBoxGraphics.H1 * NezukoBoxGraphics.FitScale, peak = NezukoBoxComeOutTiming.Peak * NezukoBoxGraphics.FitScale;

        public float Progress => ticksFlightTime <= 0 ? 1f : Mathf.Clamp01(ticksFlying / (float)ticksFlightTime);

        /// <summary>Height above the floor now, cells.</summary>
        public float Height
        {
            get
            {
                float u = Progress;
                return Mathf.Lerp(startHeight, 0f, u) + Mathf.Sin(u * Mathf.PI) * peak;
            }
        }

        public void Setup(int stun, bool draftOnLanding, float fromHeight, float peakHeight)
        {
            stunTicks = stun;
            draft = draftOnLanding;
            startHeight = fromHeight;
            peak = peakHeight;
        }

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            if (FlyingPawn == null) return;
            // DrawPos refreshes the flyer's place on the map; the base draw is not called, it would draw
            // the pawn a second time on the ground.
            Vector3 at = DrawPos;
            at.z += Height * SixPathsHeight.Lift;
            FlyingPawn.DynamicDrawPhaseAt(phase, at);
            if (phase == DrawPhase.Draw) DrawAt(drawLoc, flip);
        }

        protected override void RespawnPawn()
        {
            Pawn pawn = FlyingPawn;
            Map map = Map;
            base.RespawnPawn();
            if (pawn == null || !pawn.Spawned) return;
            if (draft && pawn.drafter != null && !pawn.Downed) pawn.drafter.Drafted = true;
            Pawn enemy = NezukoBoxStrike.Strike(pawn, startVec, stunTicks);
            NezukoBoxSounds.Play(enemy != null ? NezukoBoxSounds.Kick : "Pawn_Melee_BigBash_Miss", pawn.Position, map);
            map.GetComponent<MapComponent_NezukoBox>()?.Landed(pawn, enemy);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref stunTicks, "stunTicks", 90);
            Scribe_Values.Look(ref draft, "draft", false);
            Scribe_Values.Look(ref startHeight, "startHeight", NezukoBoxGraphics.H1 * NezukoBoxGraphics.FitScale);
            Scribe_Values.Look(ref peak, "peak", NezukoBoxComeOutTiming.Peak * NezukoBoxGraphics.FitScale);
        }
    }

    /// <summary>The strike on landing: one melee attack with the pawn's own weapon or natural attack that cannot miss, and a stun.</summary>
    public static class NezukoBoxStrike
    {
        private static readonly MethodInfo applyMelee = AccessTools.Method(typeof(Verb_MeleeAttack), "ApplyMeleeDamageToTarget");

        public static Rot4 Toward(IntVec3 from, IntVec3 to) =>
            from == to ? Rot4.South : Rot4.FromAngleFlat((to - from).ToVector3().AngleFlat());

        /// <summary>
        /// The enemy next to <paramref name="pawn"/>'s cell it strikes: a hostile, standing pawn in one of
        /// the 8 cells around it, the one most in line with the leap first.
        /// </summary>
        public static Pawn Target(Pawn pawn, Vector3 from)
        {
            Map map = pawn.Map;
            Vector3 leap = pawn.DrawPos - from;
            leap.y = 0f;
            Pawn best = null;
            float bestScore = float.MinValue;
            foreach (IntVec3 cell in GenAdjFast.AdjacentCells8Way(pawn.Position))
            {
                if (!cell.InBounds(map)) continue;
                foreach (Thing thing in cell.GetThingList(map))
                {
                    if (!(thing is Pawn other) || other == pawn || other.Dead || other.Downed || !other.HostileTo(pawn)) continue;
                    Vector3 to = other.DrawPos - pawn.DrawPos;
                    to.y = 0f;
                    float score = leap.sqrMagnitude < 1e-4f ? 0f : Vector3.Dot(leap.normalized, to.normalized);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = other;
                    }
                }
            }
            return best;
        }

        /// <summary>Strikes the enemy next to the landing cell, if any. Returns it.</summary>
        public static Pawn Strike(Pawn pawn, Vector3 from, int stunTicks)
        {
            Pawn enemy = Target(pawn, from);
            if (enemy == null) return null;
            pawn.rotationTracker.FaceTarget(enemy);
            Hit(pawn, enemy);
            if (!enemy.Dead && enemy.stances?.stunner != null) enemy.stances.stunner.StunFor(stunTicks, pawn, true, false);
            enemy.mindState.meleeThreat = pawn;
            enemy.mindState.lastMeleeThreatHarmTick = Find.TickManager.TicksGame;
            return enemy;
        }

        /// <summary>The pawn's melee verb against the enemy, applied without the hit roll (Verb_MeleeAttack.ApplyMeleeDamageToTarget).</summary>
        public static bool Hit(Pawn pawn, Pawn enemy)
        {
            Verb verb = pawn.meleeVerbs?.TryGetMeleeVerb(enemy);
            if (!(verb is Verb_MeleeAttack melee) || applyMelee == null) return false;
            applyMelee.Invoke(melee, new object[] { new LocalTargetInfo(enemy) });
            return true;
        }
    }
}
