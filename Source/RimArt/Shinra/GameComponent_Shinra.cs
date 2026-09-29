using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    public sealed class ShinraPawnState : IExposable
    {
        public Pawn pawn;
        public Map map;
        public Vector3 centre;
        public ShinraCharge charge = new ShinraCharge();
        public bool active, autoRelease;
        public int cooldownUntil, defenseUntil;
        public List<Thing> redirected = new List<Thing>();
        public CastClips.Handle animation;
        public bool restore;
        /// <summary>False in a save made before the tap/hold button; such a cast is dropped on load.</summary>
        internal bool tapHold = true;
        public bool Protected => defenseUntil > Find.TickManager.TicksGame;

        // The rule for this cast is on the charge (ShinraCharge), where the ShinraCombat console test reaches it.
        private static ShinraTuning T => ShinraTuning.Get;
        public float Radius => charge.Radius;
        public float PushCells => charge.PushCells;
        public float WallDamage => charge.WallDamage;
        public float ShotLimit => charge.ShotLimit;
        public bool TurnsExplosives => charge.TurnsExplosives;
        public int DeflectTicks => Mathf.RoundToInt((charge.Quick ? T.tapDeflectSeconds : T.deflectSeconds) * 60f);
        public int CooldownTicks => Mathf.RoundToInt((charge.Size?.cooldownSeconds ?? T.tapCooldownSeconds) * 60f);
        /// <summary>What the release pays: the Echo's cast cost for a charged one, the tuning's for the quick one; nothing without an Echo.</summary>
        public float Cost
        {
            get
            {
                float echo = PainKit.Cost(pawn, PainDefOf.AG_ShinraTensei);
                return echo <= 0f ? 0f : charge.Quick ? T.tapEchoCost : echo;
            }
        }
        /// <summary>What the picture draws for this cast (<see cref="ShinraDomeGraphics"/>).</summary>
        public ShinraDomeCast DomeCast => new ShinraDomeCast { radius = Radius, power = charge.Power, tap = charge.tap, quick = charge.Quick };

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref centre, "centre");
            Scribe_Values.Look(ref active, "active");
            Scribe_Values.Look(ref autoRelease, "autoRelease");
            Scribe_Values.Look(ref cooldownUntil, "cooldownUntil");
            Scribe_Values.Look(ref defenseUntil, "defenseUntil");
            Scribe_Values.Look(ref charge.tap, "tap");
            Scribe_Values.Look(ref charge.size, "size", -1);
            Scribe_Values.Look(ref charge.ticks, "chargeTicks");
            Scribe_Values.Look(ref charge.time, "clipTime");
            Scribe_Values.Look(ref charge.releasing, "releasing");
            Scribe_Values.Look(ref charge.burst, "burst");
            Scribe_Values.Look(ref tapHold, "tapHold", false);
            Scribe_Collections.Look(ref redirected, "redirected", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            { redirected ??= new List<Thing>(); restore = active; }
        }

        /// <summary>
        /// Lets go of a hold: the size is the one reached (short of the first, the quick version) and its cost and
        /// cooldown are taken now. A release the Echo cannot pay is called off and costs nothing. A tap was released
        /// when it started.
        /// </summary>
        public void Release()
        {
            if (!active || charge.releasing) return;
            charge.Release();
            if (!PayOrCancel()) return;
            cooldownUntil = Find.TickManager.TicksGame + CooldownTicks;
        }

        internal bool PayOrCancel()
        {
            float cost = Cost;
            if (cost <= 0f || GameComponent_Echoes.Get?.TrySpend(cost) == true) return true;
            float left = GameComponent_Echoes.Get?.charge ?? 0f;
            Messages.Message("AG_EchoCastNoCharge".Translate(cost.ToString("0"), left.ToString("0")), pawn, MessageTypeDefOf.RejectInput, false);
            Cancel();
            return false;
        }

        public void Cancel()
        {
            animation?.Stop();
            animation = null;
            active = false;
            if (pawn?.CurJobDef?.defName == "AM_InAnimation")
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }

        /// <summary>Why a new Shinra Tensei cannot start now, or null.</summary>
        public string CannotStart()
        {
            int now = Find.TickManager.TicksGame;
            if (active) return charge.releasing ? "Recovering from release." : null;
            if (cooldownUntil > now) return $"Cooldown: {(cooldownUntil - now) / 60f:0.0}s";
            if (PainKit.DevaGapLeft(pawn) > 0f) return $"Shinra Tensei and Banshō Ten'in share a gap: {PainKit.DevaGapLeft(pawn):0.0} s left.";
            if (PainKit.ChibakuLock(pawn) is string held) return held;
            if (GameComponent_Pain.Instance?.Holding(pawn, now) != null) return "Pain is in the middle of a technique.";
            float echo = PainKit.Cost(pawn, PainDefOf.AG_ShinraTensei), pool = GameComponent_Echoes.Get?.charge ?? 0f;
            if (echo > 0f && pool < T.tapEchoCost) return "AG_EchoCastNoCharge".Translate(T.tapEchoCost.ToString("0"), pool.ToString("0"));
            if (!GameComponent_Shinra.HasEye(pawn) || !ShinraCastAnimation.Clip.CanAnimate(pawn))
                return "Requires Pain's hero form, a standing humanlike caster, and Melee Animation.";
            return null;
        }
    }

    public sealed class GameComponent_Shinra : GameComponent
    {
        private bool migrate = true;
        private List<ShinraPawnState> states = new List<ShinraPawnState>();
        public static GameComponent_Shinra Instance => Current.Game.GetComponent<GameComponent_Shinra>();
        public GameComponent_Shinra(Game game) { }
        public IEnumerable<ShinraPawnState> States => states;
        public ShinraPawnState For(Pawn pawn)
        {
            var state = states.Find(s => s.pawn == pawn);
            if (state == null) { state = new ShinraPawnState { pawn = pawn }; states.Add(state); }
            return state;
        }
        /// <summary>
        /// Pawn has Shinra Tensei: since the Pain port only his Echo grants it (the repulsion eye grants nothing).
        /// The name is kept from when the eye was the source.
        /// </summary>
        public static bool HasEye(Pawn pawn) => PainKit.Has(pawn, PainDefOf.AG_ShinraTensei);

        /// <summary>
        /// Starts the quick version (<paramref name="tap"/>, paid now) or a hold. False when it cannot start now
        /// (<see cref="ShinraPawnState.CannotStart"/>) or the clip does not play.
        /// </summary>
        public bool Start(Pawn pawn, bool tap)
        {
            var s = For(pawn);
            if (s.active || s.CannotStart() != null) return false;
            if (!ShinraCastAnimation.Clip.TryStart(pawn, tap ? ShinraCastAnimation.Tap : ShinraCastAnimation.Charge, out CastClips.Handle animation))
                return false;
            s.map = pawn.Map;
            s.centre = pawn.Position.ToVector3Shifted();
            s.charge = tap ? ShinraCharge.Tap() : ShinraCharge.Hold();
            s.animation = animation;
            s.active = true;
            s.restore = false;
            s.tapHold = true;
            s.redirected.Clear();
            if (tap)
            {
                if (!s.PayOrCancel()) return false;
                s.cooldownUntil = Find.TickManager.TicksGame + s.CooldownTicks;
            }
            else ShinraSound.Charge(s.map, pawn.Position);
            return true;
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref states, "shinraPawns", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) states ??= new List<ShinraPawnState>();
        }
        public override void GameComponentTick()
        {
            if (migrate)
            {
                migrate = false;
                foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead)
                    ShinraAcquisition.Migrate(pawn);
            }
            foreach (var s in states)
            {
                if (!s.Protected) s.redirected.Clear();
                if (!s.active) continue;
                if (s.pawn == null || !s.pawn.Spawned || s.pawn.Map != s.map || s.pawn.Dead
                    || s.pawn.Downed || s.pawn.stances.stunner.Stunned || !HasEye(s.pawn)
                    || s.pawn.Position != s.centre.ToIntVec3())
                { s.Cancel(); continue; }
                if (s.restore)
                {
                    s.restore = false;
                    int clip = s.charge.tap ? ShinraCastAnimation.Tap : ShinraCastAnimation.Charge;
                    // A cast from before the tap/hold button played the old clip on other times: drop it.
                    bool restored = s.tapHold && ShinraCastAnimation.Clip.TryRestore(s.pawn, out s.animation, clip);
                    // A release interrupted by loading keeps its cooldown and any committed effects.
                    if (s.charge.releasing || !restored) { s.Cancel(); continue; }
                    s.animation.Seek(s.charge.time);
                }
                if (s.pawn.CurJobDef?.defName != "AM_InAnimation"
                    || s.animation == null || !s.animation.Read(out _, out bool finished) || finished)
                { s.Cancel(); continue; }
                float speed = ShinraCastAnimation.Clip.Speed;
                if (s.autoRelease && !s.charge.releasing && s.charge.Power >= 1f
                    && ShinraCombat.Threatened(s, s.charge.SecondsToBurst(speed) + 0.15f)) s.Release();
                if (!s.active) continue;
                bool burst = s.charge.Advance(speed);
                // Validate the renderer before committing effects, regardless of visibility.
                if (!s.animation.Seek(Mathf.Min(s.charge.time, s.charge.End)))
                { s.Cancel(); continue; }
                if (burst)
                {
                    s.defenseUntil = Find.TickManager.TicksGame + s.DeflectTicks;
                    PainKit.StartDevaGap(s.pawn);
                    List<ShinraFlight> flights = ShinraCombat.Push(s);
                    ShinraSound.Release(s.map, s.centre.ToIntVec3());
                    s.map.GetComponent<MapComponent_ShinraCasts>()?.Burst(s, flights);
                }
                if (s.charge.time >= s.charge.End) s.Cancel();
            }
        }
    }
}
