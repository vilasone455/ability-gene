using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// One cast of Banshō Ten'in, Black Receiver or Chibaku Tensei, from the warmup to the end of its picture (the pattern of
    /// MinatoCast). The cast job makes it when the warmup begins; the ability's comp tells it when it fires. From
    /// then it counts its own ticks: the rules land on whole ticks, the picture reads the same clock smoothed
    /// between them. The clock reads <see cref="Lead"/> at the start of the warmup, so the sketches' phase times are
    /// used as they are.
    /// </summary>
    public abstract class PainCast : IExposable
    {
        public Pawn caster;
        public Map home;
        /// <summary>The tick the sketch clock reads <see cref="Lead"/>: the start of the warmup.</summary>
        public int startTick;
        /// <summary>The tick the ability fired, -1 before.</summary>
        public int fireTick = -1;

        public bool Fired => fireTick >= 0;
        public abstract AbilityDef Def { get; }
        protected abstract float Lead { get; }
        protected abstract float FireAt { get; }

        public float Seconds(int now) => Lead + (now - startTick) / 60f;
        public float DrawSeconds => Lead + PictureClock.Since(startTick);
        public int TickAt(float seconds) => startTick + Mathf.RoundToInt((seconds - Lead) * 60f);

        /// <summary>The ability fired now: this tick becomes the sketch's fire time.</summary>
        public void MarkFired(int now)
        {
            fireTick = now;
            startTick = now - Mathf.RoundToInt((FireAt - Lead) * 60f);
            home = caster.Map;
        }

        /// <summary>After the fire, whether the cast job must keep Pain standing.</summary>
        public virtual bool Holds(int now) => false;
        public virtual Rot4 Facing(int now) => Rot4.Invalid;

        /// <summary>One game tick. False once the cast is over and its picture has gone.</summary>
        public abstract bool Tick(int now);
        /// <summary>Fills this frame's looks (<see cref="PainLooks"/>).</summary>
        public virtual void Pose(float s) { }
        public abstract void Draw(float s);

        /// <summary>The cast job ended while this cast still held Pain (downed, killed, a mental break).</summary>
        public virtual void JobEnded(int now) { }
        /// <summary>A test drops every cast.</summary>
        public virtual void Discard() { }

        /// <summary>Pain can still act on the map the cast began on.</summary>
        protected bool CasterFit => caster != null && caster.Spawned && caster.Map == home && !caster.Dead && !caster.Downed;

        public virtual void ExposeData()
        {
            Scribe_References.Look(ref caster, "caster", true);
            Scribe_References.Look(ref home, "home");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref fireTick, "fireTick", -1);
        }
    }

    /// <summary>
    /// Pain's casts and rods in the game: ticks the rules on game time, rebuilds the looks once a frame, draws the
    /// pictures of the map on screen, and keeps the Shinra Tensei / Banshō Ten'in gap per pawn.
    /// </summary>
    public sealed class GameComponent_Pain : GameComponent
    {
        private List<PainCast> casts = new List<PainCast>();
        /// <summary>Tick until which Shinra Tensei and Banshō Ten'in wait, per Pain.</summary>
        private Dictionary<Pawn, int> devaUntil = new Dictionary<Pawn, int>();
        private List<Pawn> devaKeys;
        private List<int> devaValues;
        /// <summary>Every pawn holding Black Receiver rods (<see cref="Hediff_PainRods"/>), on any map.</summary>
        private readonly List<Hediff_PainRods> rodded = new List<Hediff_PainRods>();
        /// <summary>What Black Receiver leaves for a while: holes in the floor, breaking rods, dust and flashes.</summary>
        private readonly List<PainMark> marks = new List<PainMark>();

        public GameComponent_Pain(Game game) { }

        public static GameComponent_Pain Instance => Current.Game?.GetComponent<GameComponent_Pain>();

        public IReadOnlyList<PainCast> Casts => casts;
        public IReadOnlyList<Hediff_PainRods> Rodded => rodded;

        public int DevaUntil(Pawn pawn) => pawn != null && devaUntil.TryGetValue(pawn, out int until) ? until : 0;
        public void SetDevaUntil(Pawn pawn, int tick) { if (pawn != null) devaUntil[pawn] = tick; }

        public void Begin(PainCast cast)
        {
            casts.RemoveAll(c => c.caster == cast.caster && c.Def == cast.Def && !c.Fired);
            casts.Add(cast);
        }

        public T Waiting<T>(Pawn caster) where T : PainCast
        {
            for (int i = casts.Count - 1; i >= 0; i--)
                if (casts[i].caster == caster && !casts[i].Fired && casts[i] is T cast) return cast;
            return null;
        }

        public T Latest<T>(Pawn caster) where T : PainCast
        {
            for (int i = casts.Count - 1; i >= 0; i--)
                if (casts[i].caster == caster && casts[i] is T cast) return cast;
            return null;
        }

        public bool FiredSince(Pawn caster, int sinceTick)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == caster && casts[i].Fired && casts[i].fireTick >= sinceTick) return true;
            return false;
        }

        public PainCast Holding(Pawn caster, int now)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == caster && casts[i].Fired && casts[i].Holds(now)) return casts[i];
            return null;
        }

        public void JobEnded(Pawn caster)
        {
            int now = Find.TickManager.TicksGame;
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                PainCast cast = casts[i];
                if (cast.caster != caster) continue;
                if (!cast.Fired) casts.RemoveAt(i);
                else if (cast.Holds(now)) cast.JobEnded(now);
            }
        }

        /// <summary>A pawn lying face-down after a Banshō slam; <paramref name="toPain"/> is the unit way to the Pain who pulled it.</summary>
        public bool FaceDown(Pawn pawn, out Vector2 toPain)
        {
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < casts.Count; i++)
                if (casts[i] is BanshoCast pull && pull.target == pawn && pull.LiesFaceDown(now))
                {
                    toPain = new Vector2(-pull.aim.x, -pull.aim.z);
                    return true;
                }
            toPain = default;
            return false;
        }

        /// <summary>A pawn in a Banshō pull right now (lifted, flying or dragged): nothing else moves it.</summary>
        public bool InPull(Pawn pawn)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i] is BanshoCast pull && pull.Carries(pawn)) return true;
            return false;
        }

        public void Register(Hediff_PainRods rods)
        {
            if (!rodded.Contains(rods)) rodded.Add(rods);
        }

        public void AddMark(PainMark mark)
        {
            // Oldest first; the picture fades each one out long before this cap matters.
            if (marks.Count >= 96) marks.RemoveAt(0);
            marks.Add(mark);
        }

        public void ResetForTests()
        {
            for (int i = 0; i < casts.Count; i++) casts[i].Discard();
            casts.Clear();
            devaUntil.Clear();
            marks.Clear();
            PainLooks.Clear();
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = casts.Count - 1; i >= 0; i--)
                if (i < casts.Count && !casts[i].Tick(now)) casts.RemoveAt(i);
            for (int i = rodded.Count - 1; i >= 0; i--)
                if (i < rodded.Count && !rodded[i].TickRods(now)) rodded.RemoveAt(i);
            for (int i = marks.Count - 1; i >= 0; i--)
                if (now - marks[i].tick > marks[i].LifeTicks) marks.RemoveAt(i);
        }

        public override void GameComponentUpdate()
        {
            PainLooks.Clear();
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster != null) casts[i].Pose(casts[i].DrawSeconds);
            for (int i = 0; i < rodded.Count; i++) rodded[i].Pose();
            Map map = Find.CurrentMap;
            if (map == null) return;
            // Shinra Tensei's pushed pawns fly to where the push put them (a look like the ones above).
            map.GetComponent<MapComponent_ShinraCasts>()?.PoseFlights();
            for (int i = 0; i < marks.Count; i++)
                if (marks[i].map == map) marks[i].Draw();
            for (int i = 0; i < rodded.Count; i++)
                if (rodded[i].pawn.MapHeld == map) rodded[i].Draw();
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].home == map || (!casts[i].Fired && casts[i].caster?.Map == map)) casts[i].Draw(casts[i].DrawSeconds);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref casts, "painCasts", LookMode.Deep);
            Scribe_Collections.Look(ref devaUntil, "painDevaUntil", LookMode.Reference, LookMode.Value, ref devaKeys, ref devaValues);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (casts == null) casts = new List<PainCast>();
                // A cast that had not fired has no job left to fire it.
                casts.RemoveAll(c => c == null || c.caster == null || !c.Fired);
                if (devaUntil == null) devaUntil = new Dictionary<Pawn, int>();
                devaUntil.RemoveAll(pair => pair.Key == null);
            }
        }
    }

    public enum PainMarkKind
    {
        /// <summary>A hole with cracks where a rod went into the floor (30 s, fading).</summary>
        Hole,
        /// <summary>A rod that broke: eaten from the knob down and shedding flakes (0.8 s).</summary>
        Broken,
        /// <summary>The pale flash when every rod in a pawn breaks at once (0.3 s).</summary>
        AllBreak,
        /// <summary>Dust where a pinned pawn hit the floor (0.6 s).</summary>
        PinDust,
    }

    /// <summary>What Black Receiver leaves behind for a while, drawn by <see cref="PainPictures.Mark"/> from its tick.</summary>
    public struct PainMark
    {
        public PainMarkKind kind;
        public Map map;
        /// <summary>The pawn a breaking rod was in (followed while it stands), or null.</summary>
        public Pawn pawn;
        /// <summary>Ground point; for a rod, the pawn's ground point when it broke.</summary>
        public Vector2 at;
        /// <summary>A rod in a lying pawn: the body's head way and the lean toward Pain. A rod in a standing pawn: the way it came from.</summary>
        public Vector2 head, lean, toward;
        public int index, tick, seed;
        public bool lying;

        public int LifeTicks => kind == PainMarkKind.Hole ? Mathf.CeilToInt(BlackReceiverTiming.HoleLife * 60f) : 60;

        public void Draw() => PainPictures.Mark(this, PictureClock.Since(tick));
    }

    /// <summary>
    /// The cast job of Banshō Ten'in, Black Receiver and Chibaku Tensei: JobDriver_CastAbility that starts the cast (its
    /// picture) when the warmup begins and, after the fire, holds Pain for as long as the cast says: Banshō until the
    /// slam, Black Receiver until the rod lands, Chibaku Tensei until the ball is formed and his hand is down. A cast called off before it fired costs nothing and starts no cooldown; one
    /// that fired had its cooldown (and charge) taken by Ability.Activate.
    /// </summary>
    public class JobDriver_CastPain : JobDriver_CastAbility
    {
        private int pictureTick = -1;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => pictureTick < 0 && !job.ability.CanCast && !job.ability.Casting);
            AddFinishAction(delegate { GameComponent_Pain.Instance?.JobEnded(pawn); });

            Toil begin = ToilMaker.MakeToil("PainBegin");
            begin.initAction = Begin;
            begin.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return begin;

            yield return Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);

            Toil hold = ToilMaker.MakeToil("PainHold");
            hold.initAction = () => pawn.pather.StopDead();
            hold.tickAction = () =>
            {
                int now = Find.TickManager.TicksGame;
                PainCast cast = GameComponent_Pain.Instance?.Holding(pawn, now);
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

        private void Begin()
        {
            pawn.pather.StopDead();
            if (pictureTick >= 0 || job.ability == null || !job.ability.CanCast) return;
            pictureTick = Find.TickManager.TicksGame;
            PainCast cast = PainCasts.Make(job.ability.def, pawn, job.targetA, pictureTick);
            if (cast != null) GameComponent_Pain.Instance?.Begin(cast);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pictureTick, "painPictureTick", -1);
        }
    }

    public static class PainCasts
    {
        public static PainCast Make(AbilityDef def, Pawn caster, LocalTargetInfo target, int startTick)
        {
            PainCast cast;
            if (def == PainDefOf.AG_PainBanshoTenin) cast = new BanshoCast { target = target.Pawn };
            else if (def == PainDefOf.AG_PainBlackReceiver) cast = new BlackReceiverCast { target = target.Pawn };
            else if (def == PainDefOf.AG_PainChibakuTensei) cast = new ChibakuCast { cell = target.Cell };
            else return null;
            cast.caster = caster;
            cast.home = caster.Map;
            cast.startTick = startTick;
            return cast;
        }

        /// <summary>
        /// The cast an ability's Apply works on: the one its job began, or, when it fired without the job (a test, a
        /// direct Activate), a new one whose warmup ended now.
        /// </summary>
        public static T For<T>(Ability ability, LocalTargetInfo target) where T : PainCast
        {
            GameComponent_Pain pain = GameComponent_Pain.Instance;
            if (pain == null) return null;
            T cast = pain.Waiting<T>(ability.pawn);
            if (cast != null) return cast;
            int now = Find.TickManager.TicksGame;
            cast = Make(ability.def, ability.pawn, target, now - Mathf.RoundToInt(ability.def.verbProperties.warmupTime * 60f)) as T;
            if (cast != null) pain.Begin(cast);
            return cast;
        }
    }

    /// <summary>Pain's cast abilities are disabled while another of his casts still holds him.</summary>
    public abstract class CompAbilityEffect_Pain : CompAbilityEffect
    {
        public override bool GizmoDisabled(out string reason)
        {
            if (GameComponent_Pain.Instance?.Holding(parent.pawn, Find.TickManager.TicksGame) != null)
            {
                reason = "Pain is in the middle of a technique.";
                return true;
            }
            return base.GizmoDisabled(out reason);
        }

        protected static void Reject(string problem, Pawn pawn, bool throwMessages)
        {
            if (throwMessages && problem != null) Messages.Message(problem, pawn, MessageTypeDefOf.RejectInput, false);
        }
    }
}
