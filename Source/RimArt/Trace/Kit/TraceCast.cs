using RimWorld;
using UnityEngine;
using Verse;
using static RimArt.VfxMath;
using R = RimArt.TraceReinforcementTiming;
using T = RimArt.TraceOnTiming;

namespace RimArt
{
    /// <summary>
    /// One cast of Trace On or Reinforcement, from the start of its job (<see cref="JobDriver_CastTrace"/>) to the end
    /// of its picture. The clock reads 0 at the job's start: Trace On first spends <see cref="lead"/> putting a real
    /// weapon away (or after a held copy broke), then the warmup of <see cref="cast"/> s traces the copy, which is in
    /// the hand at <see cref="LitAt"/>. The ability's comp marks the fire, and the clock is set so that tick is LitAt.
    ///
    /// Not saved: a game loaded mid-cast finishes the cast without its picture.
    /// </summary>
    public sealed class TraceCast
    {
        public Pawn caster;
        public AbilityDef def;
        /// <summary>The tick the clock reads 0.</summary>
        public int startTick;
        public int fireTick = -1;
        /// <summary>Seconds from the job's start to the warmup's.</summary>
        public float lead;
        /// <summary>The warmup in seconds: the def's until it begins, then the pawn's own (aiming delay stretches it).</summary>
        public float cast;
        /// <summary>Trace On: the copy the warmup traces, made at the start so the picture shows the real thing.</summary>
        public ThingWithComps pending;
        /// <summary>Trace On: the real weapon on its way to the inventory during the lead; the game does not draw it then.</summary>
        public ThingWithComps stowing;
        private UbwPose stowFrom;
        private TraceShape stowShape;
        private float stowAltitude;

        /// <summary>After the fire the job keeps the pawn standing this long: while the wire fades off the copy, or one step before a run.</summary>
        private const int TraceOnHold = 18, ReinforcementHold = 6;

        public bool TraceOn => def == TraceDefOf.AG_Trace_On;
        public bool Fired => fireTick >= 0;
        public float LitAt => lead + cast;
        public float Seconds => UbwClock.Since(startTick);

        public void MarkFired(int now)
        {
            fireTick = now;
            startTick = now - Mathf.RoundToInt(LitAt * 60f);
        }

        public bool Holds(int now) => Fired && now < fireTick + (TraceOn ? TraceOnHold : ReinforcementHold);

        /// <summary>False once the cast is over: its picture has gone, or it never fired and its caster is gone.</summary>
        public bool Tick(int now)
        {
            if (caster == null || caster.Destroyed || caster.Dead) return false;
            if (!Fired) return now - startTick < 1200;
            float after = TraceOn ? Mathf.Max(T.WireFade, T.Glint) : Mathf.Max(R.LinesFade, R.Ring - cast);
            return (now - fireTick) / 60f < after + 0.05f;
        }

        /// <summary>Where the real weapon is when the stow starts: drawn from there to the hip.</summary>
        public void CaptureStow()
        {
            stowShape = TraceWeaponShapes.For(stowing);
            if (!TraceHands.Pose(caster, stowing, stowShape, true, out stowFrom, out stowAltitude)) stowShape = null;
        }

        public void Draw(Map map)
        {
            if (!caster.Spawned || caster.Map != map || !VfxDraw.Shown(new Vector2(caster.DrawPos.x, caster.DrawPos.z), map)) return;
            if (TraceOn) DrawTraceOn();
            else DrawReinforcement();
        }

        private void DrawTraceOn()
        {
            float s = Seconds;
            string key = "trace on " + caster.thingIDNumber;
            if (stowing != null && stowShape != null && s < lead)
            {
                TraceBody standing = TraceHands.Body(caster, stowFrom);
                UbwPose hip = TraceOnGraphics.Hip(stowFrom, TraceHands.Hip(standing), TraceOnGraphics.Angle(stowFrom));
                TraceOnGraphics.Stowing(key + " stow", stowShape, stowFrom, hip, Smooth(s / T.StowTime), stowAltitude, false);
            }
            Thing copy = Fired ? caster.equipment?.Primary : pending;
            if (copy == null || (Fired && !TraceCopies.IsCopy(copy))) return;
            TraceShape shape = TraceWeaponShapes.For(copy);
            if (!TraceHands.Pose(caster, copy, shape, true, out UbwPose pose, out float altitude)) return;
            TraceBody body = TraceHands.Body(caster, pose);
            TraceOnGraphics.TraceIn(key, shape, pose, s, new TraceTimes(lead, cast), body, TraceHands.Grip(pose), altitude, !Fired, false);
        }

        private void DrawReinforcement()
        {
            float s = Seconds;
            string key = "reinforce " + caster.thingIDNumber;
            ThingWithComps weapon = caster.equipment?.Primary;
            TraceShape shape = weapon != null ? TraceWeaponShapes.For(weapon) : null;
            bool drawn = TraceHands.Pose(caster, weapon, shape, false, out UbwPose pose, out float altitude);
            if (!drawn) TraceHands.Pose(caster, weapon, shape, true, out pose, out _);
            TraceBody body = TraceHands.Body(caster, pose);
            TraceReinforcementGraphics.CastRing(body.Me, s);
            TraceReinforcementGraphics.CastLines(key, body, s, cast);
            if (!drawn) return;
            float climb = Mathf.Clamp01((s - R.Climb(cast)) / R.ClimbTime(cast));
            TraceReinforcementGraphics.WeaponGlow(key, shape, pose, climb, s - cast, s, 1f, altitude);
        }
    }
}
