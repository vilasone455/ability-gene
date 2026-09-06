using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace AbilityGenes
{
    public class HediffCompProperties_Reflection : HediffCompProperties
    {
        /// <summary>
        /// Lifetime in game ticks, counted here rather than by HediffCompProperties_Disappears
        /// for the same reason the time lattice does it: a pawn running accelerated ticks its
        /// own hediffs several times per game tick, which would expire a Disappears comp early.
        /// </summary>
        public int durationTicks = 1800;

        /// <summary>
        /// Bounce hostile projectiles that come close, instead of letting them land and be
        /// returned as damage by the TakeDamage prefix. Purely so the reversal is visible.
        /// </summary>
        public bool reflectProjectiles = true;

        /// <summary>
        /// How close a projectile has to get to be turned around. Bullets move about a cell a
        /// tick, so much below 1.5 and fast rounds step over the check between ticks - which
        /// costs nothing but the visual, since the damage is returned on impact anyway.
        /// </summary>
        public float catchRadius = 1.5f;

        public HediffCompProperties_Reflection()
        {
            compClass = typeof(HediffComp_Reflection);
        }
    }

    /// <summary>
    /// Holds the reflection open and reports the pawn to <see cref="ReflectionRegistry"/>.
    /// The reflection itself lives in the Thing.TakeDamage prefix; this comp only decides who
    /// is reflecting and for how long.
    /// </summary>
    public class HediffComp_Reflection : HediffComp
    {
        private int ticksLeft = -1;
        private int lastGameTickProcessed = -1;

        public HediffCompProperties_Reflection Props => (HediffCompProperties_Reflection)props;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);

            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead)
            {
                ReflectionRegistry.Drop(pawn);
                return;
            }

            ReflectionRegistry.Report(pawn);

            // Everything below is rate-sensitive: an accelerated pawn runs this comp several
            // times inside one game tick, which would burn the duration at the multiplier.
            int now = Find.TickManager.TicksGame;
            if (now == lastGameTickProcessed) return;
            lastGameTickProcessed = now;

            if (ticksLeft < 0)
            {
                ticksLeft = Props.durationTicks;
                Messages.Message("AG_ReflectionUp".Translate(pawn.LabelShort),
                    pawn, MessageTypeDefOf.NeutralEvent, false);
            }

            if (Props.reflectProjectiles && pawn.Spawned) BounceNearbyProjectiles(pawn);

            ticksLeft -= Mathf.Max(1, delta);
            if (ticksLeft <= 0)
            {
                ReflectionRegistry.Drop(pawn);
                pawn.health.RemoveHediff(parent);
            }
        }

        /// <summary>
        /// Hostile rounds passing within catchRadius are re-launched at whoever fired them.
        /// Friendly fire is deliberately left alone: a colonist shooting past the reflector
        /// would otherwise have their own bullet sent back at them.
        /// </summary>
        private void BounceNearbyProjectiles(Pawn pawn)
        {
            Map map = pawn.Map;
            if (map == null) return;

            List<Thing> projectiles = map.listerThings.ThingsInGroup(ThingRequestGroup.Projectile);
            if (projectiles.Count == 0) return;

            float radiusSquared = Props.catchRadius * Props.catchRadius;
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                Projectile projectile = projectiles[i] as Projectile;
                if (projectile == null || projectile.Destroyed) continue;

                Thing launcher = projectile.Launcher;
                if (launcher == null || launcher == pawn || !launcher.Spawned) continue;
                if (!launcher.HostileTo(pawn)) continue;

                Vector3 offset = projectile.ExactPosition - pawn.DrawPos;
                offset.y = 0f;
                if (offset.sqrMagnitude > radiusSquared) continue;

                VectorReflect.SendBack(projectile, pawn, launcher);
            }
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            ReflectionRegistry.Drop(Pawn);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", -1);
        }
    }
}
