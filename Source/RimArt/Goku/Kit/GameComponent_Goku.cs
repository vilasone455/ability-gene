using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// One Goku cast that holds the caster: Kamehameha or Spirit Bomb. The ability's Apply queues it
    /// and enqueues the channel job (<see cref="JobDriver_GokuChannel"/>); the job's start begins the
    /// channel, and from then the cast counts its own time on game ticks, as <see cref="UbwCast"/>
    /// does. The Echo takes the cast charge before Apply runs (EchoCastPayment); the charge and the
    /// cooldown come back on Cancel (the player's button, a move order, a revert) and stay spent when
    /// the caster is stunned, downed or killed, which is the sketch's rule.
    /// </summary>
    public abstract class GokuCast : IExposable
    {
        public Pawn caster;
        public Map home;
        /// <summary>When the ability queued the cast, and when the channel began (-1 until the job starts).</summary>
        public int queuedTick, channelTick = -1;
        /// <summary>The charge the Echo took when the ability fired; given back on a refund.</summary>
        public float paid;
        public bool broken, spent;

        /// <summary>The job has not started yet: the cast waits this long for it, then gives up.</summary>
        protected const int QueueTimeout = 60;

        public bool Queued => !broken && channelTick < 0;
        /// <summary>The cast still holds the caster: its ability buttons are disabled and the channel job runs.</summary>
        public abstract bool Busy { get; }
        public abstract string BusyReason { get; }
        /// <summary>The cell the caster faces while it channels.</summary>
        public abstract IntVec3 FacingCell { get; }
        /// <summary>The cell the caster must stay on while it channels.</summary>
        public abstract IntVec3 ChannelCell { get; }
        protected abstract AbilityDef Def { get; }
        protected abstract string Name { get; }
        /// <summary>The picture's clock: its plan starts at this many seconds.</summary>
        protected abstract float Lead { get; }

        protected Ability Ability => caster?.abilities?.GetAbility(Def);
        protected static Vector2 Vec(IntVec3 c) => new Vector2(c.x + 0.5f, c.z + 0.5f);
        protected static Vector2 Vec(Vector3 v) => new Vector2(v.x, v.z);

        /// <summary>Seconds on the picture's clock: whole ticks since the channel began, plus the lead.</summary>
        public float Seconds(int now) => Lead + (now - channelTick) / 60f;
        /// <summary>Seconds channelled: game time since the channel began.</summary>
        public float Channelled(int now) => channelTick < 0 ? 0f : (now - channelTick) / 60f;

        public virtual void StartChannel(int now)
        {
            channelTick = now;
            home = caster.Map;
        }

        /// <summary>The caster still channels: standing where it began, awake, not stunned, in the channel job, still Goku.</summary>
        protected bool Held() =>
            caster != null && caster.Spawned && caster.Map == home && !caster.Dead && !caster.Downed
            && !(caster.stances?.stunner?.Stunned ?? false) && caster.CurJobDef == GokuDefOf.AG_GokuChannel
            && caster.Position == ChannelCell && Ability != null;

        /// <summary>Whether a broken channel spends the cast: the caster was stunned, downed or killed.</summary>
        protected bool BrokenBySelf() =>
            caster == null || caster.Dead || caster.Downed || (caster.stances?.stunner?.Stunned ?? false);

        /// <summary>
        /// The cast ends before it fires. With <paramref name="spent"/> the cooldown and the charge stay
        /// spent; otherwise both come back, as UbwCast.Break gives them.
        /// </summary>
        public void Cancel(bool spent)
        {
            if (broken) return;
            broken = true;
            this.spent = spent;
            if (!spent)
            {
                Ability ability = Ability;
                if (ability != null) ability.ResetCooldown();
                // Reverted: the Echo took the ability and kept its cooldown for the next manifest.
                else if (caster != null) GameComponent_Echoes.Get?.HostRecord(caster)?.grant.Forget(Def);
                GameComponent_Echoes.Get?.Refund(paid);
                RefundMore();
            }
            paid = 0f;
            if (caster != null && caster.Spawned && caster.CurJobDef == GokuDefOf.AG_GokuChannel)
                caster.jobs.EndCurrentJob(JobCondition.InterruptForced);
            if (caster != null && channelTick >= 0)
                Messages.Message(Name + (spent ? ": the ki scattered. Charge and cooldown are spent." : ": cancelled. No charge or cooldown spent."),
                    caster, spent ? MessageTypeDefOf.NegativeEvent : MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>On Cancel: anything else the cast took and gives back (the Warp's Instant Transmission charge and cooldown).</summary>
        protected virtual void RefundMore() { }

        /// <summary>One game tick. False once the cast is over and its picture has faded, so it can be dropped.</summary>
        public abstract bool Tick(int now);

        public abstract void Draw();

        public virtual void ExposeData()
        {
            Scribe_References.Look(ref caster, "caster", true);
            Scribe_References.Look(ref home, "home");
            Scribe_Values.Look(ref queuedTick, "queuedTick");
            Scribe_Values.Look(ref channelTick, "channelTick", -1);
            Scribe_Values.Look(ref paid, "paid");
            Scribe_Values.Look(ref broken, "broken");
            Scribe_Values.Look(ref spent, "spent");
        }
    }

    /// <summary>
    /// A picture with no rules behind it: Solar Flare's and Instant Transmission's, started when the
    /// cast job's warmup begins so the flash or the vanish lands on the tick the ability fires. Not
    /// saved: a load mid-warmup loses a quarter second of light.
    /// </summary>
    public abstract class GokuPicture
    {
        public Pawn caster;
        public Map home;
        public int startTick;
        public bool fired;

        protected static Vector2 Vec(IntVec3 c) => new Vector2(c.x + 0.5f, c.z + 0.5f);
        protected static Vector2 Vec(Vector3 v) => new Vector2(v.x, v.z);

        /// <summary>Whether the picture is still to be shown.</summary>
        public abstract bool Tick(int now);
        public abstract void Draw();
    }

    /// <summary>
    /// Every Goku cast in the game: ticks the rules on game time, draws them on the map on screen, and
    /// saves the casts that hold a caster. The pictures of the instant abilities live here too.
    /// </summary>
    public sealed class GameComponent_Goku : GameComponent
    {
        private List<GokuCast> casts = new List<GokuCast>();
        private readonly List<GokuPicture> pictures = new List<GokuPicture>();

        public GameComponent_Goku(Game game) { }

        public static GameComponent_Goku Instance => Current.Game?.GetComponent<GameComponent_Goku>();

        /// <summary>The cast that holds this pawn now, if any.</summary>
        public GokuCast For(Pawn pawn)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == pawn && casts[i].Busy) return casts[i];
            return null;
        }

        /// <summary>A Spirit Bomb being channelled on this map, for the Lend energy button.</summary>
        public SpiritBombCast SpiritBombOn(Map map)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i] is SpiritBombCast bomb && bomb.Channelling && bomb.home == map) return bomb;
            return null;
        }

        /// <summary>The ability fired and its charge was taken: the cast begins when its channel job starts.</summary>
        public void Queue(GokuCast cast)
        {
            if (For(cast.caster) != null) return;
            casts.Add(cast);
        }

        /// <summary>The channel job started. False if no cast was waiting for it: the job then ends.</summary>
        public bool ChannelStarted(Pawn caster)
        {
            GokuCast cast = For(caster);
            if (cast == null || !cast.Queued) return false;
            cast.StartChannel(Find.TickManager.TicksGame);
            return true;
        }

        // ---- the pictures of the instant abilities --------------------------------------------------------------

        /// <summary>A cast job's warmup began: the picture starts, replacing one this caster never fired.</summary>
        public void Begin(GokuPicture picture)
        {
            pictures.RemoveAll(p => p.caster == picture.caster && !p.fired);
            pictures.Add(picture);
        }

        public T PictureFor<T>(Pawn caster) where T : GokuPicture
        {
            for (int i = pictures.Count - 1; i >= 0; i--)
                if (pictures[i].caster == caster && !pictures[i].fired && pictures[i] is T picture) return picture;
            return null;
        }

        /// <summary>A picture this caster started at or after <paramref name="sinceTick"/> has fired.</summary>
        public bool FiredSince(Pawn caster, int sinceTick)
        {
            for (int i = 0; i < pictures.Count; i++)
                if (pictures[i].caster == caster && pictures[i].fired && pictures[i].startTick >= sinceTick) return true;
            return false;
        }

        /// <summary>The cast job ended: a picture that never fired is dropped.</summary>
        public void WarmupEnded(Pawn caster) => pictures.RemoveAll(p => p.caster == caster && !p.fired);

        /// <summary>For game tests: drops every cast and picture.</summary>
        public void ResetForTests()
        {
            for (int i = 0; i < casts.Count; i++) if (casts[i].Busy) casts[i].Cancel(false);
            casts.Clear();
            pictures.Clear();
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = casts.Count - 1; i >= 0; i--)
                if (!casts[i].Tick(now)) casts.RemoveAt(i);
            for (int i = pictures.Count - 1; i >= 0; i--)
                if (!pictures[i].Tick(now)) pictures.RemoveAt(i);
        }

        public override void GameComponentUpdate()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            // Drawn on real pawns: heights on the body are fitted to them (see PawnFit).
            PawnFit.Begin();
            try
            {
                for (int i = 0; i < casts.Count; i++)
                    if (casts[i].home == map) casts[i].Draw();
                for (int i = 0; i < pictures.Count; i++)
                    if (pictures[i].home == map) pictures[i].Draw();
            }
            finally
            {
                PawnFit.End();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref casts, "gokuCasts", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (casts == null) casts = new List<GokuCast>();
                casts.RemoveAll(c => c == null || c.caster == null);
            }
        }
    }
}
