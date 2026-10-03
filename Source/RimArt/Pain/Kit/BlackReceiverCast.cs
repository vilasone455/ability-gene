using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public class CompProperties_BlackReceiver : CompProperties_AbilityEffect
    {
        /// <summary>Cells per second in flight.</summary>
        public float speed = 30f;
        /// <summary>At this many cells or less Pain stabs instead of throwing.</summary>
        public float stabRange = 1.5f;
        public float damage = 5f, armorPenetration = 0.2f;
        /// <summary>Each rod breaks this long after it landed.</summary>
        public float rodSeconds = 8f;
        /// <summary>Rods one pawn can hold (a new one replaces the oldest), and how many pin it.</summary>
        public int maxRods = 3, pinRods = 3;

        public int RodTicks => Mathf.RoundToInt(rodSeconds * 60f);

        public CompProperties_BlackReceiver()
        {
            compClass = typeof(CompAbilityEffect_BlackReceiver);
        }
    }

    /// <summary>
    /// Black Receiver (the sketch pain-black-receiver.js): one hostile pawn or animal up to 12 cells away in sight;
    /// a berserk colonist is hostile. Not a Deva Path technique, so the Shinra Tensei / Banshō gap does not apply.
    /// The throw or stab is <see cref="BlackReceiverCast"/>; the rods are <see cref="Hediff_PainRods"/>.
    /// </summary>
    public class CompAbilityEffect_BlackReceiver : CompAbilityEffect_Pain
    {
        public new CompProperties_BlackReceiver Props => (CompProperties_BlackReceiver)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!PainKit.Foe(parent.pawn, target.Pawn))
            {
                Reject("Black Receiver needs a standing hostile pawn or animal.", parent.pawn, throwMessages);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (!PainKit.Foe(parent.pawn, target.Pawn)) return;
            PainCasts.For<BlackReceiverCast>(parent, target)?.Fire(target.Pawn, Find.TickManager.TicksGame);
        }
    }

    /// <summary>
    /// One rod. Warmup 0.3 s: the rod grows in the palm. At the fire tick, at <c>stabRange</c> or less Pain stabs it
    /// in at once; further away it flies from his hand at <c>speed</c>, turning each tick toward where the target is
    /// now, and the first standing pawn it crosses takes it (whoever that is). A wall breaks it; past range + 2 cells
    /// it falls and breaks. Pain stands until the rod has landed.
    /// </summary>
    public sealed class BlackReceiverCast : PainCast
    {
        public Pawn target;
        public bool stab, done;
        /// <summary>A stab into a pawn lying on the floor (pinned, or face-down after Banshō): the picture drives the rod down.</summary>
        public bool stabLying;
        /// <summary>The rod's point in flight (cell space, y 0), where it left the hand, where it came to rest.</summary>
        public Vector3 rod, from, landedAt;
        public int landTick = -1;
        public Pawn hit;

        public const float LeadTime = 0.2f, HandReach = 0.5f, Tail = 0.4f;

        public override AbilityDef Def => PainDefOf.AG_PainBlackReceiver;
        protected override float Lead => LeadTime;
        protected override float FireAt => LeadTime + Warmup;
        public float FireSeconds => FireAt;

        /// <summary>The target lies on the floor now and is close enough to stab: the picture's stab from above.</summary>
        public static bool Lying(Pawn pawn) => PainRods.Pinned(pawn) || (GameComponent_Pain.Instance?.FaceDown(pawn, out _) ?? false);

        public bool StabsLying()
        {
            if (Fired) return stabLying;
            if (target == null || !target.Spawned || caster == null || !caster.Spawned) return false;
            Vector3 to = target.DrawPos - caster.Position.ToVector3Shifted();
            to.y = 0f;
            return to.magnitude <= P.stabRange && Lying(target);
        }

        private static float Warmup => PainDefOf.AG_PainBlackReceiver.verbProperties.warmupTime;
        private static CompProperties_BlackReceiver P => PainKit.ReceiverProps;

        public void Fire(Pawn pawn, int now)
        {
            MarkFired(now);
            target = pawn;
            Vector3 centre = caster.Position.ToVector3Shifted();
            Vector3 to = pawn.DrawPos - centre;
            to.y = 0f;
            stab = to.magnitude <= P.stabRange;
            stabLying = stab && Lying(pawn);
            from = centre + (to.sqrMagnitude > 1e-4f ? to.normalized : Vector3.forward) * HandReach;
            from.y = 0f;
            rod = from;
            if (stab) Land(pawn, now);
        }

        public Vector3 Aim
        {
            get
            {
                Vector3 to = (target != null && target.Spawned ? target.DrawPos : rod) - caster.DrawPos;
                to.y = 0f;
                return to.sqrMagnitude > 1e-4f ? to.normalized : Vector3.forward;
            }
        }

        public override bool Holds(int now) => Fired && !done;

        public override Rot4 Facing(int now) => Rot4.FromAngleFlat(Mathf.Atan2(Aim.x, Aim.z) * Mathf.Rad2Deg);

        public override bool Tick(int now)
        {
            if (!Fired) return now - startTick < 600;
            if (!done)
            {
                if (!CasterFit) { done = true; Drop(now); }
                else Fly(now);
            }
            return !done || now - Mathf.Max(landTick, fireTick) < Mathf.RoundToInt(Tail * 60f) + 30;
        }

        /// <summary>One tick of flight: <c>speed</c> / 60 cells toward the target's live position, in 0.1-cell steps.</summary>
        private void Fly(int now)
        {
            Vector3 goal = target != null && target.Spawned && target.Map == home ? target.DrawPos : rod + (rod - from).normalized;
            goal.y = 0f;
            Vector3 dir = goal - rod;
            if (dir.sqrMagnitude < 1e-6f) dir = (rod - from).sqrMagnitude > 1e-6f ? rod - from : Vector3.forward;
            dir.Normalize();
            float step = P.speed / 60f;
            int n = Mathf.Max(1, Mathf.CeilToInt(step / 0.1f));
            IntVec3 last = rod.ToIntVec3();
            for (int i = 1; i <= n; i++)
            {
                Vector3 next = rod + dir * (step / n);
                IntVec3 cell = next.ToIntVec3();
                if (!cell.InBounds(home) || (cell != last && cell.Impassable(home)))
                {
                    done = true;
                    Drop(now);
                    return;
                }
                rod = next;
                Pawn pawn = Catcher(cell);
                if (pawn != null)
                {
                    Land(pawn, now);
                    return;
                }
                last = cell;
            }
            float range = PainDefOf.AG_PainBlackReceiver.verbProperties.range + 2f;
            if ((rod - from).magnitude > range)
            {
                done = true;
                Drop(now);
            }
        }

        /// <summary>The first standing pawn in the cell other than Pain; the target is taken wherever it is drawn.</summary>
        private Pawn Catcher(IntVec3 cell)
        {
            if (target != null && target.Spawned && target.Map == home && !target.Dead
                && (target.DrawPos - rod).Yto0().sqrMagnitude <= 0.35f * 0.35f) return target;
            var things = cell.GetThingList(home);
            for (int i = 0; i < things.Count; i++)
                if (things[i] is Pawn pawn && pawn != caster && !pawn.Dead && !pawn.Downed
                    && pawn.GetPosture() == PawnPosture.Standing) return pawn;
            return null;
        }

        private void Land(Pawn pawn, int now)
        {
            done = true;
            hit = pawn;
            landTick = now;
            landedAt = rod;
            Vector3 back = caster.DrawPos - pawn.DrawPos;
            back.y = 0f;
            Vector2 toward = back.sqrMagnitude > 1e-4f ? new Vector2(back.x, back.z).normalized : Vector2.up;
            pawn.TakeDamage(new DamageInfo(DamageDefOf.Stab, P.damage, P.armorPenetration, Mathf.Atan2(-toward.x, -toward.y) * Mathf.Rad2Deg, caster));
            if (pawn.Dead || !pawn.Spawned) return;
            PainPictures.Shake(pawn.Map, BlackReceiverTiming.ShakeHit);
            Hediff_PainRods.For(pawn).Add(caster, toward, stabLying, now);
        }

        /// <summary>The rod missed or hit a wall: it breaks where it is.</summary>
        private void Drop(int now)
        {
            landTick = now;
            landedAt = rod;
            // It goes into the floor where it fell: a hole with cracks.
            if (home != null) GameComponent_Pain.Instance?.AddMark(new PainMark { kind = PainMarkKind.Hole, map = home,
                at = new Vector2(rod.x, rod.z), tick = now, seed = now % 97 });
        }

        public override void Discard() => done = true;

        public override void Draw(float s) => PainPictures.Receiver(this, s);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref target, "target");
            Scribe_References.Look(ref hit, "hit");
            Scribe_Values.Look(ref stab, "stab");
            Scribe_Values.Look(ref stabLying, "stabLying");
            Scribe_Values.Look(ref done, "done");
            Scribe_Values.Look(ref rod, "rod");
            Scribe_Values.Look(ref from, "from");
            Scribe_Values.Look(ref landedAt, "landedAt");
            Scribe_Values.Look(ref landTick, "landTick", -1);
        }
    }

    /// <summary>One rod in a pawn.</summary>
    public sealed class PainRod : IExposable
    {
        public Pawn thrower;
        public int landTick;
        /// <summary>Unit map direction from the pawn toward Pain when it landed; the rod's knob end points this way.</summary>
        public Vector2 toward;
        /// <summary>Driven in from above into a pawn lying on the floor: it went on into the floor.</summary>
        public bool stabbed;
        /// <summary>0..2: which of the three places on the body it went into (the picture spreads them).</summary>
        public int slot;

        public void ExposeData()
        {
            Scribe_References.Look(ref thrower, "thrower");
            Scribe_Values.Look(ref landTick, "landTick");
            Scribe_Values.Look(ref toward, "toward");
            Scribe_Values.Look(ref stabbed, "stabbed");
            Scribe_Values.Look(ref slot, "slot");
        }
    }

    /// <summary>
    /// Black Receiver's rods in one pawn. Severity = rods in it. Each rod breaks <c>rodSeconds</c> after it landed,
    /// and every rod breaks at once when the Pain that threw it is downed, killed or leaves hero form (no longer has
    /// the ability). At most <c>maxRods</c>; a new one replaces the oldest. At <c>pinRods</c> the pawn is pinned: a
    /// short stun renewed every tick (never StopStun, which would end other stuns), drawn lying on its back 0.35
    /// cells further from Pain; when a rod breaks it gets up in 0.3 s. Ticked by <see cref="GameComponent_Pain"/>.
    /// </summary>
    public class Hediff_PainRods : Hediff
    {
        public List<PainRod> rods = new List<PainRod>();
        /// <summary>The tick it was pinned (the fall), and the tick the pin ended (the get-up); -1 when not.</summary>
        public int pinnedTick = -1, freedTick = -1;
        /// <summary>The last tick something tried to move it while pinned (the rods flare), -1 never.</summary>
        public int flaredTick = -1;
        /// <summary>The way it fell: away from Pain. Its head points this way while it lies pinned.</summary>
        public Vector2 fallAway = Vector2.down;

        public bool Pinned => pinnedTick >= 0;

        public static Hediff_PainRods For(Pawn pawn)
        {
            var rods = pawn.health.hediffSet.GetFirstHediffOfDef(PainDefOf.AG_PainRods) as Hediff_PainRods;
            if (rods != null) return rods;
            rods = (Hediff_PainRods)HediffMaker.MakeHediff(PainDefOf.AG_PainRods, pawn);
            pawn.health.AddHediff(rods);
            return rods;
        }

        public void Add(Pawn thrower, Vector2 toward, bool stabbed, int now)
        {
            CompProperties_BlackReceiver props = PainKit.ReceiverProps;
            while (rods.Count >= props.maxRods) Break(0, now, 0);
            int slot = 0;
            while (rods.Exists(r => r.slot == slot)) slot++;
            var rod = new PainRod { thrower = thrower, landTick = now, toward = toward, stabbed = stabbed, slot = slot };
            rods.Add(rod);
            Severity = rods.Count;
            GameComponent_Pain.Instance?.Register(this);
            // Stabbed through a pawn lying face-down: the rod goes on into the floor.
            if (stabbed && !Pinned && GameComponent_Pain.Instance.FaceDown(pawn, out Vector2 toPain))
                AddHole(slot, PainPictures.LyingGround(pawn.DrawPos), toPain, now);
            if (Pinned)
            {
                // A rod added to a pinned pawn goes through it into the floor too.
                AddHole(slot, LiesAt, fallAway, now);
                pawn.stances?.stunner?.StunFor(5, thrower, false, false);
            }
            else if (rods.Count >= props.pinRods)
            {
                pinnedTick = now;
                freedTick = -1;
                fallAway = -toward;
                pawn.stances?.stunner?.StunFor(5, thrower, false, false);
                PainPictures.Shake(pawn.Map, stabbed ? BlackReceiverTiming.ShakeStabPin : BlackReceiverTiming.ShakePin);
                // It falls on its back: the rods go through into the floor under it, and dust where it lands.
                foreach (PainRod r in rods) AddHole(r.slot, LiesAt, fallAway, now);
                if (pawn.Spawned) GameComponent_Pain.Instance?.AddMark(new PainMark { kind = PainMarkKind.PinDust, map = pawn.Map, at = LiesAt, tick = now });
            }
        }

        /// <summary>Its cell centre as the sketches' feet, and where it lies once pinned: fallen 0.35 cells away from Pain.</summary>
        public Vector2 StoodAt => PainPictures.Ground(pawn.Position.ToVector3Shifted());
        public Vector2 LiesAt
        {
            get
            {
                Vector3 c = pawn.Position.ToVector3Shifted();
                return PainPictures.LyingGround(new Vector3(c.x + fallAway.x * BlackReceiverTiming.KnockBack, 0f, c.z + fallAway.y * BlackReceiverTiming.KnockBack));
            }
        }

        private void AddHole(int slot, Vector2 ground, Vector2 head, int now)
        {
            if (!pawn.Spawned) return;
            GameComponent_Pain.Instance?.AddMark(new PainMark { kind = PainMarkKind.Hole, map = pawn.Map,
                at = BlackReceiverTiming.HoleAt(slot, ground, head), tick = now, seed = slot * 3 });
        }

        /// <summary>Something tried to move it while pinned: the rods flare (Shinra Tensei's push, Gravity Well).</summary>
        public void Flared() => flaredTick = Find.TickManager.TicksGame;

        /// <summary>Rod <paramref name="index"/> breaks; its picture crumbles from <paramref name="delay"/> ticks from now.</summary>
        private void Break(int index, int now, int delay)
        {
            PainRod rod = rods[index];
            rods.RemoveAt(index);
            if (!pawn.Spawned) return;
            var mark = new PainMark { kind = PainMarkKind.Broken, map = pawn.Map, pawn = pawn, index = rod.slot, tick = now + delay, toward = rod.toward };
            if (Pinned)
            {
                mark.lying = true;
                mark.at = LiesAt;
                mark.head = fallAway;
            }
            else if (GameComponent_Pain.Instance.FaceDown(pawn, out Vector2 toPain))
            {
                mark.lying = true;
                mark.at = PainPictures.LyingGround(pawn.DrawPos);
                mark.head = toPain;
                mark.lean = toPain;
            }
            else mark.at = PainKit.Ground(pawn.DrawPos);
            GameComponent_Pain.Instance?.AddMark(mark);
        }

        private static bool Fit(Pawn thrower) =>
            thrower != null && !thrower.Dead && !thrower.Downed && PainKit.Has(thrower, PainDefOf.AG_PainBlackReceiver);

        /// <summary>One game tick. False once no rod is left (the hediff is removed).</summary>
        public bool TickRods(int now)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed || !pawn.health.hediffSet.hediffs.Contains(this)) return false;
            CompProperties_BlackReceiver props = PainKit.ReceiverProps;
            // Every rod of a Pain who went down, died or left hero form breaks at once: a pale flash, and the rods
            // crumble 0.03 s apart (the sketch's stagger).
            bool all = rods.Count > 1 && rods.TrueForAll(r => !Fit(r.thrower));
            if (all && pawn.Spawned)
                GameComponent_Pain.Instance?.AddMark(new PainMark { kind = PainMarkKind.AllBreak, map = pawn.Map,
                    at = Pinned ? LiesAt : PainKit.Ground(pawn.DrawPos), tick = now });
            int stagger = 0;
            for (int i = rods.Count - 1; i >= 0; i--)
            {
                bool unfit = !Fit(rods[i].thrower);
                if (now - rods[i].landTick >= props.RodTicks || unfit)
                    Break(i, now, all ? Mathf.RoundToInt(stagger++ * BlackReceiverTiming.BreakStagger * 60f) : 0);
            }
            if (rods.Count == 0)
            {
                pawn.health.RemoveHediff(this);
                return false;
            }
            Severity = rods.Count;
            if (rods.Count >= props.pinRods)
            {
                if (!Pinned) pinnedTick = now;
                pawn.stances?.stunner?.StunFor(5, rods[0].thrower, false, false);
            }
            else if (Pinned)
            {
                pinnedTick = -1;
                freedTick = now;
            }
            return true;
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            // Saves: the component's list is not saved, so a loaded hediff signs itself up again.
            GameComponent_Pain.Instance?.Register(this);
        }

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            GameComponent_Pain.Instance?.Register(this);
        }

        public override bool ShouldRemove => rods.Count == 0;

        public override string LabelInBrackets => rods.Count >= (PainKit.ReceiverProps?.pinRods ?? 3) ? "pinned" : rods.Count + (rods.Count == 1 ? " rod" : " rods");

        /// <summary>
        /// Lying on its back while pinned, and getting up after (BlackReceiverTiming): it turns onto its back by
        /// FallShare over 0.18 s while it is thrown 0.35 cells away from Pain (FallBack), and back up by RiseShare over
        /// 0.3 s.
        /// </summary>
        public void Pose()
        {
            if (!pawn.Spawned) return;
            float turn, back;
            if (Pinned)
            {
                float since = PictureClock.Since(pinnedTick);
                turn = BlackReceiverTiming.FallShare(since);
                back = BlackReceiverTiming.FallBack(since);
            }
            else if (freedTick >= 0)
            {
                float since = PictureClock.Since(freedTick);
                if (since >= BlackReceiverTiming.GetUp) return;
                turn = 1f - BlackReceiverTiming.RiseShare(since);
                back = BlackReceiverTiming.KnockBack * turn;
            }
            else return;
            if (turn <= 0f && back <= 0f) return;
            PainLook look = PainLooks.For(pawn);
            Vector3 at = pawn.Position.ToVector3Shifted();
            look.moved = true;
            look.drawAt = new Vector3(at.x + fallAway.x * back, 0f, at.z + fallAway.y * back);
            look.angle = PainLooks.HeadAngle(fallAway, turn);
            look.facing = Rot4.South;
            look.lying = turn > 0.5f;
        }

        public void Draw() => PainPictures.Rods(this);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref rods, "rods", LookMode.Deep);
            Scribe_Values.Look(ref pinnedTick, "pinnedTick", -1);
            Scribe_Values.Look(ref freedTick, "freedTick", -1);
            Scribe_Values.Look(ref flaredTick, "flaredTick", -1);
            Scribe_Values.Look(ref fallAway, "fallAway", Vector2.down);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (rods == null) rods = new List<PainRod>();
                rods.RemoveAll(r => r == null);
            }
        }
    }

    public static class PainRods
    {
        public static Hediff_PainRods Of(Pawn pawn) =>
            pawn?.health?.hediffSet?.GetFirstHediffOfDef(PainDefOf.AG_PainRods) as Hediff_PainRods;

        public static int Count(Pawn pawn) => Of(pawn)?.rods.Count ?? 0;

        public static bool Pinned(Pawn pawn) => Of(pawn)?.Pinned == true;
    }
}
