using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// One cast of a Vergil ability, from the warmup to the end of its picture. The cast job
    /// (<see cref="JobDriver_CastVergil"/>) makes it when the warmup begins, so the pose and the picture start
    /// with the hand going to the hilt; the ability's comp tells it when it fires. From then it counts its own
    /// ticks: the rules land on whole ticks, the picture reads the same clock smoothed between them.
    ///
    /// Every cast reads the clock the way its sketch does: <see cref="Lead"/> seconds at the start of the
    /// warmup, so the sketch's phase times (JudgementCutTiming.OpenAt and the rest) can be used as they are.
    /// </summary>
    public abstract class VergilCast : IExposable
    {
        public Pawn caster;
        public Map home;
        /// <summary>The tick the sketch clock reads <see cref="Lead"/>: the start of the warmup.</summary>
        public int startTick;
        /// <summary>The tick the ability fired, -1 before.</summary>
        public int fireTick = -1;

        public bool Fired => fireTick >= 0;
        public abstract AbilityDef Def { get; }
        /// <summary>The sketch's clock at the start of the warmup.</summary>
        protected abstract float Lead { get; }
        /// <summary>The sketch's clock when the ability fires.</summary>
        protected abstract float FireAt { get; }

        /// <summary>The sketch's clock on whole ticks, for the rules.</summary>
        public float Seconds(int now) => Lead + (now - startTick) / 60f;
        /// <summary>The sketch's clock smoothed between ticks, for the picture and the pose.</summary>
        public float DrawSeconds => Lead + UbwClock.Since(startTick);
        /// <summary>The tick the sketch's clock reads <paramref name="seconds"/>.</summary>
        public int TickAt(float seconds) => startTick + Mathf.RoundToInt((seconds - Lead) * 60f);

        /// <summary>
        /// The ability fired now. The clock is set so that this tick is the sketch's fire time: a warmup cut
        /// short or stretched by a tick does not shift what follows.
        /// </summary>
        public void MarkFired(int now)
        {
            fireTick = now;
            startTick = now - Mathf.RoundToInt((FireAt - Lead) * 60f);
            home = caster.Map;
        }

        /// <summary>After the fire, whether the cast job must keep Vergil standing (the dash and sheathe, the vanish and the kneel).</summary>
        public virtual bool Holds(int now) => false;
        /// <summary>The cell Vergil faces while the job holds him, or invalid to leave him be.</summary>
        public virtual Rot4 Facing(int now) => Rot4.Invalid;

        /// <summary>One game tick. False once the cast is over and its picture has gone.</summary>
        public abstract bool Tick(int now);
        /// <summary>Fills this frame's looks: Vergil's pose, marked pawns' tints.</summary>
        public abstract void Pose(float s);
        public abstract void Draw(float s);

        /// <summary>The cast job ended while this cast still held Vergil (downed, killed, a mental break).</summary>
        public virtual void JobEnded(int now) { }

        /// <summary>A test or a debug action drops every cast: undo what the cast put on pawns.</summary>
        public virtual void Discard() { }

        public virtual void ExposeData()
        {
            Scribe_References.Look(ref caster, "caster", true);
            Scribe_References.Look(ref home, "home");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref fireTick, "fireTick", -1);
        }
    }

    /// <summary>
    /// Every Vergil cast in the game and every Style meter: ticks the rules on game time, rebuilds the looks
    /// once a frame and draws the pictures of the map on screen.
    /// </summary>
    public sealed class GameComponent_Vergil : GameComponent
    {
        private List<VergilCast> casts = new List<VergilCast>();
        private List<VergilStyleMeter> meters = new List<VergilStyleMeter>();

        public GameComponent_Vergil(Game game) { }

        public static GameComponent_Vergil Instance => Current.Game?.GetComponent<GameComponent_Vergil>();

        public IReadOnlyList<VergilCast> Casts => casts;

        /// <summary>A cast job's warmup began: the cast starts, replacing one this caster never fired for the same ability.</summary>
        public void Begin(VergilCast cast)
        {
            casts.RemoveAll(c => c.caster == cast.caster && c.Def == cast.Def && !c.Fired);
            casts.Add(cast);
        }

        /// <summary>The unfired cast of this ability, waiting for its fire tick.</summary>
        public T Waiting<T>(Pawn caster) where T : VergilCast
        {
            for (int i = casts.Count - 1; i >= 0; i--)
                if (casts[i].caster == caster && !casts[i].Fired && casts[i] is T cast) return cast;
            return null;
        }

        /// <summary>The latest cast of this kind by this caster, fired or not.</summary>
        public T Latest<T>(Pawn caster) where T : VergilCast
        {
            for (int i = casts.Count - 1; i >= 0; i--)
                if (casts[i].caster == caster && casts[i] is T cast) return cast;
            return null;
        }

        /// <summary>A cast this caster began at or after <paramref name="sinceTick"/> has fired.</summary>
        public bool FiredSince(Pawn caster, int sinceTick)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == caster && casts[i].Fired && casts[i].fireTick >= sinceTick) return true;
            return false;
        }

        /// <summary>Some fired cast still holds this caster.</summary>
        public VergilCast Holding(Pawn caster, int now)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == caster && casts[i].Fired && casts[i].Holds(now)) return casts[i];
            return null;
        }

        /// <summary>The cast job ended: an unfired cast is dropped, and a fired one still holding the caster is told.</summary>
        public void JobEnded(Pawn caster)
        {
            int now = Find.TickManager.TicksGame;
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                VergilCast cast = casts[i];
                if (cast.caster != caster) continue;
                if (!cast.Fired) casts.RemoveAt(i);
                else if (cast.Holds(now)) cast.JobEnded(now);
            }
        }

        // ---- Style ------------------------------------------------------------------------------------------------

        /// <summary>This Host's Style meter; made when first asked for with <paramref name="create"/>.</summary>
        public VergilStyleMeter Meter(Pawn host, bool create)
        {
            for (int i = 0; i < meters.Count; i++)
                if (meters[i].host == host) return meters[i];
            if (!create || host == null) return null;
            var meter = new VergilStyleMeter { host = host };
            meters.Add(meter);
            return meter;
        }

        /// <summary>For game tests: drops every cast and meter, undoing what the casts put on pawns.</summary>
        public void ResetForTests()
        {
            for (int i = 0; i < casts.Count; i++) casts[i].Discard();
            casts.Clear();
            meters.Clear();
            VergilLooks.Clear();
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                // Tick may start a cast (none do today) or end one; walk from the end so either is safe.
                if (i < casts.Count && !casts[i].Tick(now)) casts.RemoveAt(i);
            }
            if (now % VergilStyle.TickInterval == 0)
                for (int i = meters.Count - 1; i >= 0; i--)
                    if (!meters[i].Tick(now)) meters.RemoveAt(i);
        }

        private HashSet<Pawn> kneeling = new HashSet<Pawn>(), kneelingBefore = new HashSet<Pawn>();

        public override void GameComponentUpdate()
        {
            VergilLooks.Clear();
            for (int i = 0; i < casts.Count; i++)
            {
                VergilCast cast = casts[i];
                if (cast.caster == null) continue;
                cast.Pose(cast.DrawSeconds);
            }
            // The render tree picks its materials and which body parts to draw only when it recaches: a pawn
            // that starts or stops kneeling in a picture asks for one.
            VergilLooks.KneelPictured(kneeling);
            foreach (Pawn pawn in kneeling) if (!kneelingBefore.Contains(pawn)) Recache(pawn);
            foreach (Pawn pawn in kneelingBefore) if (!kneeling.Contains(pawn)) Recache(pawn);
            (kneeling, kneelingBefore) = (kneelingBefore, kneeling);
            Map map = Find.CurrentMap;
            if (map == null) return;
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].home == map || (!casts[i].Fired && casts[i].caster?.Map == map)) casts[i].Draw(casts[i].DrawSeconds);
        }

        private static void Recache(Pawn pawn)
        {
            PawnRenderNode root = pawn?.Drawer?.renderer?.renderTree?.rootNode;
            if (root != null) root.requestRecache = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref casts, "vergilCasts", LookMode.Deep);
            Scribe_Collections.Look(ref meters, "vergilStyle", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (casts == null) casts = new List<VergilCast>();
                if (meters == null) meters = new List<VergilStyleMeter>();
                // A cast that had not fired has no job left to fire it.
                casts.RemoveAll(c => c == null || c.caster == null || !c.Fired);
                meters.RemoveAll(m => m == null || m.host == null);
            }
        }
    }

    /// <summary>
    /// The cast job of Vergil's four abilities: JobDriver_CastAbility that starts the cast (pose and picture)
    /// when the warmup begins, and after the fire holds Vergil for as long as the cast says, facing where it
    /// says. The hold is what keeps an undrafted Vergil from walking off in the middle of a dash's sheathe or
    /// Judgement Cut End's kneel.
    /// </summary>
    public class JobDriver_CastVergil : JobDriver_CastAbility
    {
        private int pictureTick = -1;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailBeforeFired(Fired);
            AddFinishAction(delegate
            {
                if (job.ability != null && job.def.abilityCasting)
                    job.ability.StartCooldown(job.ability.def.cooldownTicksRange.RandomInRange);
                GameComponent_Vergil.Instance?.JobEnded(pawn);
            });

            Toil begin = ToilMaker.MakeToil("VergilBegin");
            begin.initAction = Begin;
            begin.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return begin;

            yield return Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);

            Toil hold = ToilMaker.MakeToil("VergilHold");
            hold.initAction = () => pawn.pather.StopDead();
            hold.tickAction = () =>
            {
                int now = Find.TickManager.TicksGame;
                VergilCast cast = GameComponent_Vergil.Instance?.Holding(pawn, now);
                if (cast == null)
                {
                    ReadyForNextToil();
                    return;
                }
                Rot4 facing = cast.Facing(now);
                if (facing.IsValid) pawn.Rotation = facing;
            };
            hold.handlingFacing = true;
            hold.defaultCompleteMode = ToilCompleteMode.Never;
            yield return hold;
        }

        private bool Fired() => pictureTick >= 0 && (GameComponent_Vergil.Instance?.FiredSince(pawn, pictureTick) ?? false);

        private void Begin()
        {
            pawn.pather.StopDead();
            if (pictureTick >= 0 || job.ability == null || !job.ability.CanCast) return;
            pictureTick = Find.TickManager.TicksGame;
            // Judgement Cut End has no target to turn to: he faces the camera, so the hand on the hilt shows.
            if (job.ability.def == VergilDefOf.AG_VergilJudgementCutEnd) pawn.Rotation = Rot4.South;
            VergilCast cast = VergilCasts.Make(job.ability.def, pawn, job.targetA, pictureTick);
            if (cast != null) GameComponent_Vergil.Instance?.Begin(cast);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pictureTick, "vergilPictureTick", -1);
        }
    }

    /// <summary>Which cast each ability makes.</summary>
    public static class VergilCasts
    {
        public static VergilCast Make(AbilityDef def, Pawn caster, LocalTargetInfo target, int startTick)
        {
            VergilCast cast;
            if (def == VergilDefOf.AG_VergilJudgementCut) cast = new JudgementCutCast { target = target.Cell };
            else if (def == VergilDefOf.AG_VergilYamatoDash) cast = new YamatoDashCast { dest = target.Cell };
            else if (def == VergilDefOf.AG_VergilSummonedSwords) cast = new SummonedSwordsCast();
            else if (def == VergilDefOf.AG_VergilJudgementCutEnd) cast = new JudgementCutEndCast();
            else return null;
            cast.caster = caster;
            cast.home = caster.Map;
            cast.startTick = startTick;
            return cast;
        }

        /// <summary>
        /// The cast an ability's Apply works on: the one its job began, or, when it fired without the job (a
        /// test, a direct Activate), a new one whose warmup ended now.
        /// </summary>
        public static T For<T>(Ability ability, LocalTargetInfo target) where T : VergilCast
        {
            GameComponent_Vergil vergil = GameComponent_Vergil.Instance;
            if (vergil == null) return null;
            T cast = vergil.Waiting<T>(ability.pawn);
            if (cast != null) return cast;
            int now = Find.TickManager.TicksGame;
            cast = Make(ability.def, ability.pawn, target, now - Mathf.RoundToInt(ability.def.verbProperties.warmupTime * 60f)) as T;
            if (cast != null) vergil.Begin(cast);
            return cast;
        }
    }
}
