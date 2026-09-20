using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public enum PowerPoleCastKind { Thrust, Sweep, Strike }

    /// <summary>
    /// Plays the pole's pictures for real casts and does what each picture shows at the moment it
    /// shows it: the thrust's hit lands when the tip arrives, the sweep hits each pawn as the pole
    /// passes it, and the vault's strike lands just before the wielder does.
    ///
    /// A cast is known from the moment its warmup starts (<see cref="Begin"/>, from
    /// JobDriver_CastPowerPole), because the pole slides back or is planted during the warmup. The
    /// ability's comp then says what the cast does (<see cref="LandThrust"/> and the others).
    ///
    /// The clock is game ticks, so the picture pauses and speeds up with the game. Nothing is saved:
    /// a game loaded in the middle of a cast loses the rest of that cast's picture and any hit it
    /// had not dealt yet.
    /// </summary>
    public class MapComponent_PowerPoleCasts : MapComponent
    {
        private sealed class Cast
        {
            public Pawn caster;
            public PowerPoleCastKind kind;
            public int startTick, warmupTicks;
            public bool landed;
            /// <summary>The pole is back to its carried length: the held staff is drawn again and the caster may go.</summary>
            public bool home;
            public bool retractHeard, whipHeard, strikeHeard;
            /// <summary>Where the caster stood when the cast began, and the unit direction of the cast.</summary>
            public Vector2 feet, toward;

            public PowerPoleThrustShot thrust;
            public CompProperties_PowerPoleThrust thrustProps;
            public Pawn victim;
            public IntVec3 pushTo;
            public bool hitDone, slamDone;

            public PowerPoleSweepShot sweep;
            public CompProperties_PowerPoleSweep sweepProps;
            public List<Pawn> sweepVictims;
            public bool[] sweepDone;

            public PowerPoleStrikeShot strike;
            public CompProperties_PowerPoleStrike strikeProps;
            public PawnFlyer_PowerPoleVault flyer;
            public IntVec3 strikeCell;
            public bool struck;
            public int landTick = -1;
        }

        /// <summary>A cast whose warmup ran out this long ago without landing was interrupted.</summary>
        private const float Overdue = 0.25f;
        /// <summary>Pawns with a cast in progress, on any map: the held staff is not drawn for them.</summary>
        private static readonly HashSet<Pawn> casting = new HashSet<Pawn>();

        private readonly List<Cast> casts = new List<Cast>();

        public MapComponent_PowerPoleCasts(Map map) : base(map) { }

        /// <summary>The pawn's pole is out, drawn by a cast's picture, so the ordinary held staff is not.</summary>
        public static bool IsCasting(Pawn pawn) => pawn != null && casting.Contains(pawn);

        /// <summary>
        /// A cast's warmup has begun. Walls do not move during a warmup, so what can be known now is
        /// worked out now: the sweep's length per angle and the vault's landing cell. Who is hit is
        /// decided when the cast lands.
        /// </summary>
        public void Begin(Pawn caster, Ability ability, LocalTargetInfo target)
        {
            Ended(caster);
            if (caster == null || ability == null || !target.IsValid) return;
            Cast cast = Add(caster, KindOf(ability), target.Cell, Find.TickManager.TicksGame, ability.def.verbProperties.warmupTime);
            if (cast.kind == PowerPoleCastKind.Sweep)
            {
                var comp = ability.CompOfType<CompAbilityEffect_PowerPoleSweep>();
                cast.sweepProps = comp.Props;
                cast.sweep = comp.Shot(caster, cast.feet, cast.toward, null);
            }
            else if (cast.kind == PowerPoleCastKind.Strike)
            {
                var comp = ability.CompOfType<CompAbilityEffect_PowerPoleStrike>();
                cast.strikeProps = comp.Props;
                if (!comp.TryShot(caster, target.Cell, out cast.strike, out cast.toward, out _)) Ended(caster);
            }
        }

        /// <summary>The kind and direction of the cast this pawn has begun, for its clip.</summary>
        public bool TryGetCast(Pawn caster, out PowerPoleCastKind kind, out Vector2 toward)
        {
            Cast cast = casts.Find(c => c.caster == caster && !c.landed);
            kind = cast?.kind ?? PowerPoleCastKind.Thrust;
            toward = cast?.toward ?? Vector2.right;
            return cast != null;
        }

        private Cast Add(Pawn caster, PowerPoleCastKind kind, IntVec3 target, int startTick, float warmup)
        {
            Vector3 stands = caster.DrawPos;
            Vector2 toward = new Vector2(target.x - caster.Position.x, target.z - caster.Position.z);
            var cast = new Cast
            {
                caster = caster, kind = kind, startTick = startTick, warmupTicks = Mathf.RoundToInt(warmup * 60f),
                feet = new Vector2(stands.x, stands.z), toward = toward.sqrMagnitude < 0.01f ? Vector2.right : toward.normalized,
            };
            casts.Add(cast);
            casting.Add(caster);
            return cast;
        }

        private static PowerPoleCastKind KindOf(Ability ability)
        {
            if (ability.CompOfType<CompAbilityEffect_PowerPoleSweep>() != null) return PowerPoleCastKind.Sweep;
            if (ability.CompOfType<CompAbilityEffect_PowerPoleStrike>() != null) return PowerPoleCastKind.Strike;
            return PowerPoleCastKind.Thrust;
        }

        /// <summary>The cast that has begun and not landed, or a new one for a cast that was never begun.</summary>
        private Cast Landing(Pawn caster, PowerPoleCastKind kind, IntVec3 target, float warmup)
        {
            Cast cast = casts.Find(c => c.caster == caster && c.kind == kind && !c.landed)
                ?? Add(caster, kind, target, Find.TickManager.TicksGame - Mathf.RoundToInt(warmup * 60f), warmup);
            cast.landed = true;
            return cast;
        }

        public void LandThrust(Pawn caster, IntVec3 target, float warmup, PowerPoleThrustShot shot, Pawn victim, IntVec3 pushTo,
            CompProperties_PowerPoleThrust props)
        {
            Cast cast = Landing(caster, PowerPoleCastKind.Thrust, target, warmup);
            cast.thrust = shot;
            PowerPoleSound.Play(PowerPoleSoundDefOf.AG_PowerPoleExtend, map, cast.feet);
            cast.victim = victim;
            cast.pushTo = pushTo;
            cast.thrustProps = props;
        }

        public void LandSweep(Pawn caster, IntVec3 target, float warmup, PowerPoleSweepShot shot, List<Pawn> victims,
            CompProperties_PowerPoleSweep props)
        {
            Cast cast = Landing(caster, PowerPoleCastKind.Sweep, target, warmup);
            cast.sweep = shot;
            PowerPoleSound.Play(PowerPoleSoundDefOf.AG_PowerPoleSwing, map, cast.feet);
            cast.sweepVictims = victims;
            cast.sweepDone = new bool[victims.Count];
            cast.sweepProps = props;
        }

        public void LandStrike(Pawn caster, IntVec3 target, float warmup, PowerPoleStrikeShot shot, Vector2 toward,
            PawnFlyer_PowerPoleVault flyer, CompProperties_PowerPoleStrike props)
        {
            Cast cast = Landing(caster, PowerPoleCastKind.Strike, target, warmup);
            cast.strike = shot;
            PowerPoleSound.Play(PowerPoleSoundDefOf.AG_PowerPoleVault, map, cast.feet);
            cast.toward = toward;
            cast.flyer = flyer;
            cast.strikeCell = target;
            cast.strikeProps = props;
        }

        /// <summary>The caster's cast job is over. A cast that never landed has nothing left to show.</summary>
        public void Ended(Pawn caster)
        {
            for (int i = casts.Count - 1; i >= 0; i--)
                if (casts[i].caster == caster && !casts[i].landed) Remove(i);
        }

        /// <summary>Whether the caster's cast job should still hold it in place: the pole is out.</summary>
        public bool Holds(Pawn caster)
        {
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < casts.Count; i++)
            {
                Cast cast = casts[i];
                if (cast.caster != caster || !cast.landed) continue;
                float seconds = (now - cast.startTick) / 60f;
                if (cast.kind == PowerPoleCastKind.Thrust && seconds < PowerPoleThrustTiming.HomeAt) return true;
                if (cast.kind == PowerPoleCastKind.Sweep && seconds < PowerPoleSweepTiming.HomeAt) return true;
            }
            return false;
        }

        private void Remove(int index)
        {
            Pawn caster = casts[index].caster;
            casts.RemoveAt(index);
            Release(caster);
        }

        private void Release(Pawn caster)
        {
            if (!casts.Exists(c => c.caster == caster && !c.home)) casting.Remove(caster);
        }

        /// <summary>Seconds on the cast's own clock. A vault in the air reads the flyer, so the pole stays in the wielder's hands.</summary>
        private static float Seconds(Cast cast, int now)
        {
            if (cast.kind != PowerPoleCastKind.Strike || !cast.landed) return (now - cast.startTick) / 60f;
            if (cast.landTick >= 0) return cast.strike.LandAt + (now - cast.landTick) / 60f;
            return cast.strike.LaunchAt + (cast.flyer != null ? cast.flyer.Progress : 1f) * cast.strike.Flight;
        }

        public override void MapComponentTick()
        {
            if (casts.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                Cast cast = casts[i];
                float seconds = Seconds(cast, now);
                if (!cast.landed)
                {
                    if (seconds > cast.warmupTicks / 60f + Overdue || cast.caster == null || !cast.caster.Spawned || cast.caster.Map != map) Remove(i);
                    continue;
                }
                float homeAt = cast.kind == PowerPoleCastKind.Thrust ? PowerPoleThrustTiming.HomeAt
                    : cast.kind == PowerPoleCastKind.Sweep ? PowerPoleSweepTiming.HomeAt : cast.strike.HomeAt;
                float retractAt = cast.kind == PowerPoleCastKind.Thrust ? PowerPoleThrustTiming.RetractAt
                    : cast.kind == PowerPoleCastKind.Sweep ? PowerPoleSweepTiming.RetractAt : cast.strike.PinEndAt;
                if (!cast.retractHeard && seconds >= retractAt)
                {
                    cast.retractHeard = true;
                    PowerPoleSound.Play(PowerPoleSoundDefOf.AG_PowerPoleRetract, map, Stands(cast));
                }
                if (!cast.home && seconds >= homeAt)
                {
                    cast.home = true;
                    Release(cast.caster);
                }
                switch (cast.kind)
                {
                    case PowerPoleCastKind.Thrust:
                        TickThrust(cast, seconds);
                        if (seconds >= PowerPoleThrustTiming.Duration) Remove(i);
                        break;
                    case PowerPoleCastKind.Sweep:
                        TickSweep(cast, seconds);
                        if (seconds >= PowerPoleSweepTiming.Duration) Remove(i);
                        break;
                    default:
                        TickStrike(cast, seconds, now);
                        if (seconds >= cast.strike.Duration) Remove(i);
                        break;
                }
            }
        }

        /// <summary>Where the caster is now, for a sound: on the map, or where it began while it is inside the flyer.</summary>
        private static Vector2 Stands(Cast cast)
        {
            if (cast.caster == null || !cast.caster.Spawned) return cast.feet;
            return new Vector2(cast.caster.Position.x + 0.5f, cast.caster.Position.z + 0.5f);
        }

        private void TickThrust(Cast cast, float seconds)
        {
            Pawn victim = cast.victim;
            if (victim == null) return;
            if (!cast.hitDone && seconds >= PowerPoleThrustTiming.HitAt)
            {
                cast.hitDone = true;
                if (!Hittable(victim)) { cast.victim = null; return; }
                Shake(PowerPoleThrustTiming.HitShake);
                PowerPoleSound.Play(PowerPoleSoundDefOf.AG_PowerPoleHit, map, cast.feet + cast.toward * cast.thrust.Contact);
                PowerPoleCombat.Hit(victim, cast.caster, cast.thrustProps.damage, cast.thrustProps.armorPenetration, cast.toward, cast.thrustProps.staggerTicks);
                if (Hittable(victim) && cast.pushTo != victim.Position) PowerPoleCombat.Carry(victim, cast.pushTo, map);
            }
            // After the carried pawn is back on the map: a wall that stopped it early hurts.
            if (cast.hitDone && !cast.slamDone && seconds >= PowerPoleThrustTiming.PushedAt + 0.05f)
            {
                cast.slamDone = true;
                if (!cast.thrust.Blocked || !Hittable(victim)) return;
                Shake(PowerPoleThrustTiming.WallShake);
                PowerPoleSound.Play(PowerPoleSoundDefOf.AG_PowerPoleWall, map, cast.feet + cast.toward * cast.thrust.WallFace);
                PowerPoleCombat.Hit(victim, cast.caster, cast.thrustProps.wallDamage, 0f, cast.toward, 0);
            }
        }

        private void TickSweep(Cast cast, float seconds)
        {
            for (int v = 0; v < cast.sweepVictims.Count; v++)
            {
                if (cast.sweepDone[v] || seconds < cast.sweep.HitTime[v]) continue;
                cast.sweepDone[v] = true;
                Pawn victim = cast.sweepVictims[v];
                if (!Hittable(victim)) continue;
                Shake(PowerPoleSweepTiming.HitShake);
                PowerPoleSound.Play(PowerPoleSoundDefOf.AG_PowerPoleHit, map, cast.feet + cast.sweep.HitPlace[v]);
                // The pole is moving clockwise round the caster when it meets the pawn.
                Vector2 from = cast.sweep.HitPlace[v].normalized;
                PowerPoleCombat.Hit(victim, cast.caster, cast.sweepProps.damage, cast.sweepProps.armorPenetration, new Vector2(from.y, -from.x), cast.sweepProps.staggerTicks);
            }
        }

        private void TickStrike(Cast cast, float seconds, int now)
        {
            if (!cast.whipHeard && seconds >= cast.strike.PeakAt)
            {
                cast.whipHeard = true;
                PowerPoleSound.Play(PowerPoleSoundDefOf.AG_PowerPoleSwing, map, cast.feet + cast.toward * (cast.strike.Landing / 2f));
            }
            if (!cast.strikeHeard && seconds >= cast.strike.StrikeStartAt)
            {
                cast.strikeHeard = true;
                PowerPoleSound.Play(PowerPoleSoundDefOf.AG_PowerPoleExtend, map, cast.feet + cast.toward * cast.strike.Landing);
            }
            if (!cast.struck && seconds >= cast.strike.StrikeHitAt)
            {
                cast.struck = true;
                Shake(PowerPoleStrikeTiming.StrikeShake);
                PowerPoleSound.Play(PowerPoleSoundDefOf.AG_PowerPoleSlam, map, new Vector2(cast.strikeCell.x + 0.5f, cast.strikeCell.z + 0.5f));
                PowerPoleCombat.Strike(cast.strikeCell, map, cast.caster, cast.toward, cast.strikeProps);
            }
            if (cast.landTick < 0 && (cast.flyer == null || cast.flyer.Destroyed) && cast.caster.Spawned)
            {
                cast.landTick = now;
                // The pole is still pinned on the target and then retracts: the wielder stands for that long.
                PowerPoleCombat.StandFor(cast.caster, Mathf.RoundToInt((PowerPoleStrikeTiming.Pinned + PowerPoleStrikeTiming.Retract) * 60f));
            }
        }

        private bool Hittable(Pawn pawn) => pawn != null && pawn.Spawned && pawn.Map == map && !pawn.Dead;

        private void Shake(float size)
        {
            if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(size);
        }

        public override void MapComponentUpdate()
        {
            if (casts.Count == 0 || Find.CurrentMap != map) return;
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < casts.Count; i++)
            {
                Cast cast = casts[i];
                float seconds = Seconds(cast, now);
                // Until the cast lands the clock stops at the end of the warmup's part of the picture.
                switch (cast.kind)
                {
                    case PowerPoleCastKind.Thrust:
                        PowerPoleThrustGraphics.Draw(cast.feet, cast.toward, cast.thrust, cast.landed ? seconds : Mathf.Min(seconds, PowerPoleThrustTiming.ThrustAt - 0.001f), map);
                        break;
                    case PowerPoleCastKind.Sweep:
                        PowerPoleSweepGraphics.Draw(cast.feet, cast.sweep, cast.landed ? seconds : Mathf.Min(seconds, PowerPoleSweepTiming.SwingAt - 0.001f), map);
                        break;
                    default:
                        PowerPoleStrikeGraphics.Draw(cast.feet, cast.toward, cast.strike, cast.strikeProps.staggerRadius, 0f,
                            cast.landed ? seconds : Mathf.Min(seconds, cast.strike.LaunchAt - 0.001f), map);
                        break;
                }
            }
        }

        public override void MapRemoved()
        {
            for (int i = casts.Count - 1; i >= 0; i--) Remove(i);
        }
    }
}
