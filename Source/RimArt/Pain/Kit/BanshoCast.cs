using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public class CompProperties_BanshoTenin : CompProperties_AbilityEffect
    {
        /// <summary>The pull takes hold: seconds, and cells the target slides toward Pain before it is lifted.</summary>
        public float tugSeconds = 0.15f, tugSlide = 0.25f;
        /// <summary>Cells per second in the air.</summary>
        public float speed = 20f;
        public float slamDamage = 15f, slamStunSeconds = 2f;
        /// <summary>A standing pawn in the line: both take this and are stunned this long.</summary>
        public float blockDamage = 8f, blockStunSeconds = 1f;
        /// <summary>Over this body size the target is dragged: this share of the distance at this share of the speed.</summary>
        public float heavyBodySize = 2f, heavyShare = 0.5f, heavySpeedFactor = 0.35f;
        /// <summary>After Banshō Ten'in or Shinra Tensei, the other waits this long.</summary>
        public float devaGapSeconds = 5f;

        public CompProperties_BanshoTenin()
        {
            compClass = typeof(CompAbilityEffect_BanshoTenin);
        }
    }

    /// <summary>
    /// Banshō Ten'in (the sketch pain-bansho-tenin.js): one hostile pawn or animal up to 12 cells away in sight
    /// (the verb's range and line of sight), not downed and not pinned. The pull itself is <see cref="BanshoCast"/>.
    /// </summary>
    public class CompAbilityEffect_BanshoTenin : CompAbilityEffect_Pain
    {
        public new CompProperties_BanshoTenin Props => (CompProperties_BanshoTenin)props;

        public override bool GizmoDisabled(out string reason)
        {
            float gap = PainKit.DevaGapLeft(parent.pawn);
            if (gap > 0f)
            {
                reason = "Shinra Tensei and Banshō Ten'in share a gap: " + gap.ToString("0.0") + " s left.";
                return true;
            }
            if (GameComponent_Shinra.Instance?.For(parent.pawn).active == true)
            {
                reason = "Shinra Tensei is charging.";
                return true;
            }
            if (PainKit.ChibakuLock(parent.pawn) is string held)
            {
                reason = held;
                return true;
            }
            return base.GizmoDisabled(out reason);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            string problem = Problem(parent.pawn, target.Pawn);
            if (problem == null) return base.Valid(target, throwMessages);
            Reject(problem, parent.pawn, throwMessages);
            return false;
        }

        public static string Problem(Pawn caster, Pawn target)
        {
            if (target == null) return "Banshō Ten'in pulls a pawn.";
            if (!PainKit.Foe(caster, target)) return "Banshō Ten'in pulls a standing hostile pawn or animal.";
            if (PainKit.Unmovable(target)) return target.LabelShortCap + " cannot be moved now.";
            return null;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = target.Pawn;
            if (Problem(parent.pawn, pawn) != null) return;
            PainCasts.For<BanshoCast>(parent, target)?.Grip(pawn, Find.TickManager.TicksGame);
        }
    }

    /// <summary>
    /// One pull. The fire tick is the grip (the sketch's 0.6 s): the target slides <c>tugSlide</c> toward Pain over
    /// <c>tugSeconds</c>, is lifted and flies at <c>speed</c> straight at the cell in front of Pain, over low cover;
    /// 0.1 s after it arrives Pain pushes it down: slam damage and stun, and it lies face-down while stunned. The
    /// target's cell follows it as it crosses cells; its draw point follows the pull smoothly (<see cref="PainLook"/>).
    /// A standing pawn or a wall in the line stops it (block damage and stun to both; it drops where it stopped). A
    /// body over <c>heavyBodySize</c> is dragged on the floor instead: part of the distance, slower, no slam. Pain
    /// stands until the slam. The motion is the sketch's times()/alongAt()/heightAt().
    /// </summary>
    public sealed class BanshoCast : PainCast
    {
        public Pawn target;
        /// <summary>Pain's cell centre and the unit vector from him to the target, fixed at the grip.</summary>
        public Vector3 origin, aim;
        /// <summary>Cells from Pain to the target at the grip.</summary>
        public float distance;
        public bool heavy, blocked, landed, slammed, aborted;
        /// <summary>Where along the aim a block stopped it, and the tick it did.</summary>
        public float blockAlong;
        public int blockTick = -1;
        public Pawn blocker;

        // Sketch timing and shape (pain-bansho-tenin.js): rest before the warmup, the catch and the drop after a
        // block, the tail after the stun, the lift height and how fast it is reached, the cell in front of Pain.
        public const float LeadTime = 0.2f, Catch = 0.1f, Drop = 0.12f, Tail = 0.4f, LiftHeight = 0.6f, LiftUp = 0.07f;
        public const float LandGap = 1f, TugLean = 0.3f;

        public override AbilityDef Def => PainDefOf.AG_PainBanshoTenin;
        public override SoundDef WarmupSound => PainDefOf.AG_PainBanshoCast;
        protected override float Lead => LeadTime;
        protected override float FireAt => LeadTime + Warmup;

        private static float Warmup => PainDefOf.AG_PainBanshoTenin.verbProperties.warmupTime;
        private static CompProperties_BanshoTenin P => PainKit.BanshoProps;

        // ---- the sketch's times -------------------------------------------------------------------------------
        public float GripAt => FireAt;
        public float Tug => heavy ? 0f : P.tugSeconds;
        public float Slide => heavy ? 0f : P.tugSlide;
        public float Lift => GripAt + Tug;
        public float Path => heavy ? Mathf.Max(0f, distance - LandGap) * P.heavyShare : Mathf.Max(0f, distance - Slide - LandGap);
        public float Fly => Path / Mathf.Max(0.01f, P.speed * (heavy ? P.heavySpeedFactor : 1f));
        public float Arrive => blocked ? (blockTick - startTick) / 60f + Lead : Lift + Fly;
        public float Down => Arrive + (heavy ? 0f : blocked ? Drop : Catch);
        public float End => Down + Mathf.Max(BanshoTiming.Hold, heavy || blocked ? P.blockStunSeconds : P.slamStunSeconds) + Tail;

        /// <summary>The picture's times (BanshoTiming): its arrival point after a block is <see cref="blockAlong"/>.</summary>
        public BanshoTimes Times => TimesFor(distance, heavy, blocked, blockAlong);

        public static BanshoTimes TimesFor(float distance, bool heavy, bool blocked, float blockAlong) =>
            new BanshoTimes(distance, heavy, blocked, blockAlong - BanshoTiming.BlockGap, Warmup, P.speed, BanshoTiming.Hold, P.slamStunSeconds);

        /// <summary>Cells from Pain along the aim at sketch time <paramref name="s"/> (alongAt).</summary>
        public float AlongAt(float s)
        {
            if (s < GripAt) return distance;
            if (blocked && s >= Arrive) return blockAlong;
            if (s < Lift) { float t = (s - GripAt) / Tug; return distance - Slide * t * t; }
            float u = Mathf.Clamp01((s - Lift) / Mathf.Max(0.001f, Fly));
            if (heavy) return distance - Path * VfxMath.Smooth(u);
            float k = Mathf.Min(0.6f, 2f * Slide / Mathf.Max(0.001f, Tug) * Fly / Mathf.Max(0.001f, Path));
            return distance - Slide - Path * (k * u + (1f - k) * Mathf.Pow(u, 1.6f));
        }

        /// <summary>Cells above the floor at sketch time <paramref name="s"/> (heightAt).</summary>
        public float HeightAt(float s)
        {
            if (heavy || s < Lift) return 0f;
            float up = LiftHeight * VfxMath.Smooth(Mathf.Clamp01((s - Lift) / LiftUp));
            if (s < Arrive) return up;
            float d = Mathf.Clamp01((s - Arrive) / Mathf.Max(0.001f, Down - Arrive));
            return up * (1f - d * d);
        }

        /// <summary>The body's lean: below 0 toward Pain (the tug), 1 lying along the pull line (the sketch's tilt).</summary>
        public float TiltAt(float s)
        {
            if (s < GripAt) return 0f;
            if (s < Lift) return -TugLean * VfxMath.Smooth(Mathf.Clamp01((s - GripAt) / Mathf.Max(0.001f, Tug)));
            if (s < Arrive) return Mathf.Lerp(-TugLean, 0.7f, VfxMath.Smooth(Mathf.Clamp01((s - Lift) / 0.08f)));
            return Mathf.Lerp(0.7f, 1f, Mathf.Clamp01((s - Arrive) / Mathf.Max(0.001f, Down - Arrive)));
        }

        public Vector3 CentreAt(float along) => origin + aim * along;

        /// <summary>The target is carried by the pull now: from the grip to where it comes to rest.</summary>
        public bool Carries(Pawn pawn) => pawn == target && Fired && !aborted && !landed;

        public void Grip(Pawn pawn, int now)
        {
            MarkFired(now);
            target = pawn;
            origin = caster.Position.ToVector3Shifted();
            Vector3 to = pawn.Position.ToVector3Shifted() - origin;
            to.y = 0f;
            distance = to.magnitude;
            aim = distance > 0.001f ? to / distance : Vector3.forward;
            heavy = pawn.BodySize > P.heavyBodySize;
            PainKit.StartDevaGap(caster);
            pawn.pather?.StopDead();
            Hold(now);
            Shake(heavy ? BanshoTiming.ShakeDrag : BanshoTiming.ShakeGrip);
            SoundLayers.Play(PainDefOf.AG_PainBanshoPull, home, pawn.Position);
        }

        /// <summary>The sketch's camera shakes (grip, drag, block, slam), only when the pull is on the map on screen.</summary>
        private void Shake(float size)
        {
            if (home != null && Find.CurrentMap == home) Find.CameraDriver.shaker.DoShake(size);
        }

        public override bool Holds(int now) => Fired && !aborted && now < TickAt(Down) + 1;

        public override Rot4 Facing(int now) => Rot4.FromAngleFlat(Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg);

        /// <summary>The target cannot act while carried: a short stun renewed every tick (never StopStun, which would end other stuns).</summary>
        private void Hold(int now)
        {
            target?.stances?.stunner?.StunFor(5, caster, false, false);
        }

        public override bool Tick(int now)
        {
            if (!Fired) return now - startTick < 600;
            float s = Seconds(now);
            if (!landed && !aborted)
            {
                if (!CasterFit || target == null || !target.Spawned || target.Map != home || target.Dead)
                {
                    // Pain went down or the target is gone: it drops where it is, nothing more happens.
                    aborted = true;
                    Settle();
                }
                else
                {
                    Hold(now);
                    if (s >= Lift && s < Arrive) Move(s);
                    if (!landed && s >= Arrive) Land();
                }
            }
            if (landed && !slammed && s >= Down) Slam();
            return s < End;
        }

        /// <summary>The target moves on to this tick's point; the first standing pawn or wall in the way stops it.</summary>
        private void Move(float s)
        {
            float from = AlongAt(Mathf.Max(Lift, s - 1f / 60f)), to = AlongAt(s);
            int steps = Mathf.Max(1, Mathf.CeilToInt((from - to) / 0.1f));
            IntVec3 last = target.Position;
            float safe = from;
            for (int i = 1; i <= steps; i++)
            {
                float along = Mathf.Lerp(from, to, i / (float)steps);
                IntVec3 cell = CentreAt(along).ToIntVec3();
                if (cell != last)
                {
                    Pawn other = Blocker(cell);
                    if (other != null || !cell.InBounds(home) || !cell.Walkable(home))
                    {
                        // It stops at the last point short of the pawn or wall.
                        Block(safe, other);
                        return;
                    }
                    target.Position = cell;
                    last = cell;
                }
                safe = along;
            }
        }

        private Pawn Blocker(IntVec3 cell)
        {
            if (!cell.InBounds(home)) return null;
            var things = cell.GetThingList(home);
            for (int i = 0; i < things.Count; i++)
                if (things[i] is Pawn pawn && pawn != target && pawn != caster && !pawn.Dead && !pawn.Downed
                    && pawn.GetPosture() == PawnPosture.Standing && !(GameComponent_Pain.Instance?.InPull(pawn) ?? false))
                    return pawn;
            return null;
        }

        private void Block(float along, Pawn other)
        {
            blocked = true;
            blockTick = Find.TickManager.TicksGame;
            blockAlong = Mathf.Clamp(along, LandGap, distance);
            blocker = other;
            float damage = P.blockDamage;
            int stun = Mathf.RoundToInt(P.blockStunSeconds * 60f);
            Hit(target, damage, stun);
            if (other != null) Hit(other, damage, stun);
            Shake(BanshoTiming.ShakeBlock);
            Land();
        }

        private void Hit(Pawn pawn, float damage, int stunTicks)
        {
            if (pawn == null || pawn.Dead) return;
            pawn.TakeDamage(new DamageInfo(DamageDefOf.Blunt, damage, 0f, -1f, caster));
            if (!pawn.Dead) pawn.stances?.stunner?.StunFor(stunTicks, caster, true, true);
        }

        /// <summary>The target comes to rest: on the cell in front of Pain, where it was blocked, or where it was dropped.</summary>
        private void Land()
        {
            landed = true;
            float along = blocked ? blockAlong : AlongAt(Arrive);
            IntVec3 cell = Standable(along);
            if (cell.IsValid && cell != target.Position)
            {
                target.pather?.StopDead();
                target.Position = cell;
            }
            target.Notify_Teleported(true, true);
            if (heavy) slammed = true;
        }

        /// <summary>An aborted pull: the target stays on the cell it has reached.</summary>
        private void Settle()
        {
            landed = true;
            slammed = true;
            if (target != null && target.Spawned && !target.Dead) target.Notify_Teleported(true, true);
        }

        /// <summary>The cell at <paramref name="along"/>, or the nearest standable one further from Pain on the line.</summary>
        private IntVec3 Standable(float along)
        {
            for (float a = along; a <= distance + 0.5f; a += 0.25f)
            {
                IntVec3 cell = CentreAt(a).ToIntVec3();
                if (cell != caster.Position && cell.InBounds(home) && cell.Standable(home)) return cell;
            }
            return target.Position;
        }

        private void Slam()
        {
            slammed = true;
            if (blocked || aborted || target == null || !target.Spawned || target.Dead) return;
            target.TakeDamage(new DamageInfo(DamageDefOf.Blunt, P.slamDamage, 0f, -1f, caster));
            if (!target.Dead) target.stances?.stunner?.StunFor(Mathf.RoundToInt(P.slamStunSeconds * 60f), caster, true, false);
            Shake(BanshoTiming.ShakeSlam);
            SoundLayers.Play(PainDefOf.AG_PainBanshoSlam, home, target.PositionHeld);
        }

        /// <summary>Drawn face-down after the slam: while its stun lasts, until the picture's tail.</summary>
        public bool LiesFaceDown(int now) => Fired && landed && slammed && !blocked && !aborted && !heavy && target != null
            && target.stances?.stunner?.Stunned == true && Seconds(now) < End - Tail;

        public override void JobEnded(int now)
        {
            if (landed) return;
            aborted = true;
            Settle();
        }

        public override void Discard()
        {
            aborted = true;
            landed = slammed = true;
        }

        public override void Pose(float s)
        {
            if (!Fired || target == null || !target.Spawned || target.Map != home || target.Dead) return;
            Vector2 back = new Vector2(aim.x, aim.z);
            PainLook look = PainLooks.For(target);
            if (!landed || (s < Down && !aborted))
            {
                // Carried: drawn at the pull's point, lifted north by the height (the sketch draws heights x Lift).
                Vector3 at = CentreAt(AlongAt(s));
                look.moved = true;
                look.drawAt = new Vector3(at.x, 0f, at.z + HeightAt(s) * PainPictures.Lift);
                if (!heavy)
                {
                    float tilt = TiltAt(s);
                    Vector2 head = new Vector2(back.x * tilt, 1f - Mathf.Abs(tilt) + back.y * tilt);
                    look.angle = PainLooks.HeadAngle(head, 1f);
                    look.facing = Rot4.South;
                }
                return;
            }
            // Face-down with the head at Pain while the slam's stun lasts.
            if (LiesFaceDown(Find.TickManager.TicksGame))
            {
                look.angle = PainLooks.HeadAngle(-back, 1f);
                look.facing = Rot4.North;
                look.lying = true;
            }
        }

        public override void Draw(float s) => PainPictures.Bansho(this, s);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref target, "target");
            Scribe_References.Look(ref blocker, "blocker");
            Scribe_Values.Look(ref origin, "origin");
            Scribe_Values.Look(ref aim, "aim");
            Scribe_Values.Look(ref distance, "distance");
            Scribe_Values.Look(ref heavy, "heavy");
            Scribe_Values.Look(ref blocked, "blocked");
            Scribe_Values.Look(ref landed, "landed");
            Scribe_Values.Look(ref slammed, "slammed");
            Scribe_Values.Look(ref aborted, "aborted");
            Scribe_Values.Look(ref blockAlong, "blockAlong");
            Scribe_Values.Look(ref blockTick, "blockTick", -1);
        }
    }
}
