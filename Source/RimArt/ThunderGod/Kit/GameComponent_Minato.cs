using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// One cast of a Minato ability, from the warmup to the end of its picture (the pattern of VergilCast). The
    /// cast job makes it when the warmup begins; the ability's comp tells it when it fires. From then it counts
    /// its own ticks: the rules land on whole ticks, the picture reads the same clock smoothed between them.
    /// The clock reads <see cref="Lead"/> at the start of the warmup, so the sketches' phase times
    /// (ThunderGodJumpTiming.ArriveAt and the rest) are used as they are.
    /// </summary>
    public abstract class MinatoCast : IExposable
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
        public float DrawSeconds => Lead + UbwClock.Since(startTick);
        public int TickAt(float seconds) => startTick + Mathf.RoundToInt((seconds - Lead) * 60f);

        /// <summary>The ability fired now: this tick becomes the sketch's fire time.</summary>
        public void MarkFired(int now)
        {
            fireTick = now;
            startTick = now - Mathf.RoundToInt((FireAt - Lead) * 60f);
            home = caster.Map;
        }

        /// <summary>After the fire, whether the cast job must keep Minato standing.</summary>
        public virtual bool Holds(int now) => false;
        public virtual Rot4 Facing(int now) => Rot4.Invalid;

        /// <summary>One game tick. False once the cast is over and its picture has gone.</summary>
        public abstract bool Tick(int now);
        /// <summary>Fills this frame's looks.</summary>
        public virtual void Pose(float s) { }
        public abstract void Draw(float s);

        /// <summary>The cast job ended while this cast still held Minato (downed, killed, a mental break).</summary>
        public virtual void JobEnded(int now) { }
        /// <summary>A test drops every cast.</summary>
        public virtual void Discard() { }

        /// <summary>Minato can still act on the map the cast began on.</summary>
        protected bool CasterFit => caster != null && caster.Spawned && caster.Map == home && !caster.Dead && !caster.Downed;

        /// <summary>
        /// Moves Minato to <paramref name="cell"/> in one step (a teleport: no path, no walk). The pather is stopped
        /// so an order he had does not drag him back through the old cell.
        /// </summary>
        protected void Teleport(IntVec3 cell)
        {
            if (!CasterFit || !cell.IsValid || cell == caster.Position) return;
            caster.pather?.StopDead();
            caster.Position = cell;
            caster.Notify_Teleported(false, true);
        }

        public virtual void ExposeData()
        {
            Scribe_References.Look(ref caster, "caster", true);
            Scribe_References.Look(ref home, "home");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref fireTick, "fireTick", -1);
        }
    }

    /// <summary>
    /// Every Minato cast in the game: ticks the rules on game time, rebuilds the looks once a frame, draws the
    /// pictures of the map on screen and the seals of sealing touch.
    /// </summary>
    public sealed class GameComponent_Minato : GameComponent
    {
        private List<MinatoCast> casts = new List<MinatoCast>();
        /// <summary>Sealing touch's seals on any map, refreshed every <see cref="SealRefresh"/> ticks and when one is added.</summary>
        private readonly List<Hediff_MinatoSeal> seals = new List<Hediff_MinatoSeal>();
        private const int SealRefresh = 30;

        public GameComponent_Minato(Game game)
        {
            // Barriers of a game that was left behind.
            GuidingThunderCast.Live.Clear();
        }

        public static GameComponent_Minato Instance => Current.Game?.GetComponent<GameComponent_Minato>();

        public IReadOnlyList<MinatoCast> Casts => casts;

        public void Begin(MinatoCast cast)
        {
            casts.RemoveAll(c => c.caster == cast.caster && c.Def == cast.Def && !c.Fired);
            casts.Add(cast);
        }

        public T Waiting<T>(Pawn caster) where T : MinatoCast
        {
            for (int i = casts.Count - 1; i >= 0; i--)
                if (casts[i].caster == caster && !casts[i].Fired && casts[i] is T cast) return cast;
            return null;
        }

        public T Latest<T>(Pawn caster) where T : MinatoCast
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

        public MinatoCast Holding(Pawn caster, int now)
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
                MinatoCast cast = casts[i];
                if (cast.caster != caster) continue;
                if (!cast.Fired) casts.RemoveAt(i);
                else if (cast.Holds(now)) cast.JobEnded(now);
            }
        }

        public void RefreshSeals() => SealingTouch.All(seals);

        public void ResetForTests()
        {
            for (int i = 0; i < casts.Count; i++) casts[i].Discard();
            casts.Clear();
            MinatoLooks.Clear();
            seals.Clear();
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = casts.Count - 1; i >= 0; i--)
                if (i < casts.Count && !casts[i].Tick(now)) casts.RemoveAt(i);
            if (now % SealRefresh == 0) RefreshSeals();
        }

        public override void GameComponentUpdate()
        {
            MinatoLooks.Clear();
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster != null) casts[i].Pose(casts[i].DrawSeconds);
            Map map = Find.CurrentMap;
            if (map == null) return;
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].home == map || (!casts[i].Fired && casts[i].caster?.Map == map)) casts[i].Draw(casts[i].DrawSeconds);
            DrawSeals(map);
        }

        /// <summary>
        /// Sealing touch has no item to show it, so the seal is drawn on the pawn: a small ring of the formula's
        /// script round its feet, turning slowly (the glyphs of the pictures' floor script, solid gold so it shows on
        /// a pale body as well as a dark one), and the gold glow the pictures light on a sealed kunai at the chest.
        /// </summary>
        private void DrawSeals(Map map)
        {
            if (seals.Count == 0) return;
            float now = Time.realtimeSinceStartup, beat = 0.42f + 0.12f * Mathf.Sin(now * 2.6f);
            for (int i = 0; i < seals.Count; i++)
            {
                Pawn pawn = seals[i]?.pawn;
                if (pawn == null || !pawn.Spawned || pawn.Map != map || pawn.Dead || seals[i].ShouldRemove) continue;
                Vector2 feet = MinatoKit.Ground(pawn.DrawPos);
                VfxDraw.Begin(feet);
                ThunderGodGraphics.ScriptRing(feet, SealRing, SealGlyphs, 1000f, 0f, 0f, 0f, SealTurn * now + i * 40f, NoFlash, NoFlash, 0);
                ThunderGodGraphics.SealOnGround(new Vector2(feet.x, feet.y + ThunderGodTiming.Chest), beat);
            }
        }

        private const float SealRing = 0.5f, SealTurn = 12f;
        private const int SealGlyphs = 13;
        private static readonly float[] NoFlash = new float[0];

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref casts, "minatoCasts", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (casts == null) casts = new List<MinatoCast>();
                // A cast that had not fired has no job left to fire it.
                casts.RemoveAll(c => c == null || c.caster == null || !c.Fired);
            }
        }
    }

    /// <summary>
    /// The cast job of Minato's four abilities: JobDriver_CastAbility that starts the cast (its picture) when the
    /// warmup begins and, after the fire, holds Minato for as long as the cast says: the jump until the cut lands,
    /// the chain until the last one, Guiding Thunder while the barrier stands, the Rasengan through the thrust.
    /// A Rasengan on an unmarked pawn walks up to it first, as a touch ability does.
    /// </summary>
    public class JobDriver_CastMinato : JobDriver_CastAbility
    {
        private int pictureTick = -1;

        /// <summary>
        /// The player can call him off while he is still walking up to a Rasengan's target (the job def allows
        /// it); from the start of the warmup the cast holds him.
        /// </summary>
        public override bool PlayerInterruptable => pictureTick < 0;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailBeforeFired(Fired);
            AddFinishAction(delegate
            {
                // A walk called off before the warmup began cast nothing: no cooldown.
                if (job.ability != null && job.def.abilityCasting && pictureTick >= 0)
                    job.ability.StartCooldown(job.ability.def.cooldownTicksRange.RandomInRange);
                GameComponent_Minato.Instance?.JobEnded(pawn);
            });

            Toil begin = ToilMaker.MakeToil("MinatoBegin");
            begin.initAction = Begin;
            begin.defaultCompleteMode = ToilCompleteMode.Instant;

            if (job.ability?.def == MinatoDefOf.AG_Rasengan)
            {
                yield return Toils_Jump.JumpIf(begin, () => RasenganCast.FromRange(pawn, job.targetA.Thing as Pawn)
                    || pawn.CanReachImmediate(job.targetA, PathEndMode.Touch));
                Toil walk = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
                // The target is no longer one the Rasengan takes (dead, gone, or Minato himself).
                walk.FailOn(() => job.ability?.CompOfType<CompAbilityEffect_Rasengan>()?.Valid(job.targetA) == false);
                yield return walk;
            }
            yield return begin;

            yield return Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);

            Toil hold = ToilMaker.MakeToil("MinatoHold");
            hold.initAction = () => pawn.pather.StopDead();
            hold.tickAction = () =>
            {
                int now = Find.TickManager.TicksGame;
                MinatoCast cast = GameComponent_Minato.Instance?.Holding(pawn, now);
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

        private bool Fired() => pictureTick >= 0 && (GameComponent_Minato.Instance?.FiredSince(pawn, pictureTick) ?? false);

        private void Begin()
        {
            pawn.pather.StopDead();
            if (pictureTick >= 0 || job.ability == null || !job.ability.CanCast) return;
            pictureTick = Find.TickManager.TicksGame;
            MinatoCast cast = MinatoCasts.Make(job.ability.def, pawn, job.targetA, pictureTick);
            if (cast != null) GameComponent_Minato.Instance?.Begin(cast);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pictureTick, "minatoPictureTick", -1);
        }
    }

    public static class MinatoCasts
    {
        public static MinatoCast Make(AbilityDef def, Pawn caster, LocalTargetInfo target, int startTick)
        {
            MinatoCast cast;
            if (def == MinatoDefOf.AG_ThunderGodJump) cast = new ThunderGodJumpCast();
            else if (def == MinatoDefOf.AG_ThunderGodChain) cast = new ThunderGodChainCast();
            else if (def == MinatoDefOf.AG_GuidingThunder) cast = new GuidingThunderCast();
            else if (def == MinatoDefOf.AG_Rasengan) cast = new RasenganCast { target = target.Thing as Pawn };
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
        public static T For<T>(Ability ability, LocalTargetInfo target) where T : MinatoCast
        {
            GameComponent_Minato minato = GameComponent_Minato.Instance;
            if (minato == null) return null;
            T cast = minato.Waiting<T>(ability.pawn);
            if (cast != null) return cast;
            int now = Find.TickManager.TicksGame;
            cast = Make(ability.def, ability.pawn, target, now - Mathf.RoundToInt(ability.def.verbProperties.warmupTime * 60f)) as T;
            if (cast != null) minato.Begin(cast);
            return cast;
        }
    }

    /// <summary>Every Minato ability is disabled while another of his casts still holds him.</summary>
    public abstract class CompAbilityEffect_Minato : CompAbilityEffect
    {
        public override bool GizmoDisabled(out string reason)
        {
            if (GameComponent_Minato.Instance?.Holding(parent.pawn, Find.TickManager.TicksGame) != null)
            {
                reason = "Minato is in the middle of a technique.";
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
