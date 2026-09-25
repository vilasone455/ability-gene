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
        public float tail = -1f;
        public bool Protected => defenseUntil > Find.TickManager.TicksGame;
        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref centre, "centre");
            Scribe_Values.Look(ref active, "active");
            Scribe_Values.Look(ref autoRelease, "autoRelease");
            Scribe_Values.Look(ref cooldownUntil, "cooldownUntil");
            Scribe_Values.Look(ref defenseUntil, "defenseUntil");
            Scribe_Values.Look(ref tail, "tail", -1f);
            Scribe_Values.Look(ref charge.ticks, "chargeTicks");
            Scribe_Values.Look(ref charge.time, "clipTime");
            Scribe_Values.Look(ref charge.releasing, "releasing");
            Scribe_Values.Look(ref charge.burst, "burst");
            Scribe_Collections.Look(ref redirected, "redirected", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            { redirected ??= new List<Thing>(); restore = active; }
        }
        public void Release()
        {
            if (!active || charge.releasing) return;
            charge.releasing = true;
            cooldownUntil = Find.TickManager.TicksGame + ShinraCharge.CooldownTicks;
        }
        public void Cancel()
        {
            animation?.Stop();
            animation = null;
            active = false;
            if (pawn?.CurJobDef?.defName == "AM_InAnimation")
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            if (charge.burst && tail < 0f) tail = charge.time;
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
        public static bool HasEye(Pawn pawn) => pawn?.health?.hediffSet.hediffs.Any(h =>
            h.def.defName == "AG_RepulsionEye" || h.def.defName == "AG_ShinraTenseiKit") == true;
        public void Begin(Pawn pawn, CastClips.Handle animation)
        {
            var s = For(pawn);
            if (s.active || s.cooldownUntil > Find.TickManager.TicksGame) { animation.Stop(); return; }
            s.map = pawn.Map;
            s.centre = pawn.Position.ToVector3Shifted();
            s.charge = new ShinraCharge();
            s.animation = animation;
            s.active = true;
            s.restore = false;
            s.tail = -1f;
            s.redirected.Clear();
            ShinraSound.Charge(s.map, pawn.Position);
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
                if (s.tail >= 0f)
                {
                    s.tail += 1f / 60f;
                    if (s.tail >= ShinraVfxTiming.Duration) s.tail = -1f;
                }
                if (!s.Protected) s.redirected.Clear();
                if (!s.active) continue;
                if (s.pawn == null || !s.pawn.Spawned || s.pawn.Map != s.map || s.pawn.Dead
                    || s.pawn.Downed || s.pawn.stances.stunner.Stunned || !HasEye(s.pawn)
                    || s.pawn.Position != s.centre.ToIntVec3())
                { s.Cancel(); continue; }
                if (s.restore)
                {
                    s.restore = false;
                    bool restored = ShinraCastAnimation.Clip.TryRestore(s.pawn, out s.animation);
                    // A release interrupted by loading keeps its cooldown and any committed effects.
                    if (s.charge.releasing || !restored) { s.Cancel(); continue; }
                    if (s.charge.Held) s.charge.time = ShinraCharge.Hold;
                    s.animation.Seek(s.charge.time);
                }
                if (s.pawn.CurJobDef?.defName != "AM_InAnimation"
                    || s.animation == null || !s.animation.Read(out _, out bool finished) || finished)
                { s.Cancel(); continue; }
                float speed = ShinraCastAnimation.Clip.Speed;
                if (s.autoRelease && !s.charge.releasing && s.charge.Power >= 1f
                    && ShinraCombat.Threatened(s, s.charge.SecondsToBurst(speed) + 0.15f)) s.Release();
                bool burst = s.charge.Advance(speed);
                // Validate the renderer before committing effects, regardless of visibility.
                if (!s.animation.Seek(Mathf.Min(s.charge.time, ShinraCharge.End)))
                { s.Cancel(); continue; }
                if (burst)
                {
                    s.defenseUntil = Find.TickManager.TicksGame + ShinraCharge.DefenseTicks;
                    ShinraCombat.Push(s);
                    ShinraSound.Release(s.map, s.centre.ToIntVec3());
                }
                if (s.charge.time >= ShinraCharge.End)
                { s.tail = s.charge.time; s.Cancel(); }
            }
        }
    }
}
