using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>The overload's numbers after the domain, set in XML on AG_VoidOverload.</summary>
    public class HediffCompProperties_VoidOverload : HediffCompProperties
    {
        /// <summary>How fast the overload falls once the pawn is out, per second (0.005 = 0.5 %).</summary>
        public float recoveryPerSecond = 0.005f;
        /// <summary>Consciousness is capped at 1 minus the overload, never below this.</summary>
        public float consciousnessFloor = 0.1f;
        /// <summary>A pawn that comes out with more overload than this went down, and is void-scarred once the overload has gone.</summary>
        public float scarAbove = 0.7f;
        public HediffDef scarHediff;

        public HediffCompProperties_VoidOverload()
        {
            compClass = typeof(HediffComp_VoidOverload);
        }
    }

    /// <summary>Holds <see cref="HediffCompProperties_VoidOverload"/> for <see cref="Hediff_VoidOverload"/>; does nothing itself.</summary>
    public class HediffComp_VoidOverload : HediffComp
    {
        public HediffCompProperties_VoidOverload Props => (HediffCompProperties_VoidOverload)props;
    }

    /// <summary>
    /// Void overload from Unlimited Void. Severity is the overload, 0 to 1.
    ///
    /// Inside the domain it is <see cref="building"/>: the cast sets its severity every tick (10 % per second
    /// frozen and unspared, XML on the ability) and it does nothing else, so a frozen pawn does not fall over
    /// mid-freeze. When everyone comes out (<see cref="Release"/>) its stage caps Consciousness at 1 minus the
    /// overload, never below <see cref="HediffCompProperties_VoidOverload.consciousnessFloor"/>, in 1 % steps (one
    /// cached stage per step); the overload then falls <see cref="HediffCompProperties_VoidOverload.recoveryPerSecond"/>
    /// and the hediff goes at 0. The game downs a pawn under 30 % consciousness, so an overload over 70 % downs it
    /// until it falls back to 70 %. A pawn that came out over <see cref="HediffCompProperties_VoidOverload.scarAbove"/>
    /// gets <see cref="HediffCompProperties_VoidOverload.scarHediff"/> when the overload has gone.
    ///
    /// The fall is worked out from the game tick it started on, not added up per tick, so a pawn whose hediffs
    /// tick more than once a game tick (the time lattice) does not recover faster.
    /// </summary>
    public class Hediff_VoidOverload : HediffWithComps
    {
        /// <summary>Inside the domain: the overload grows and caps nothing yet.</summary>
        public bool building = true;
        /// <summary>Came out over the scar line: the scar is given when the overload reaches 0.</summary>
        public bool scarOwed;
        /// <summary>The overload when the pawn came out, and the game tick it came out on.</summary>
        private float fromSeverity;
        /// <summary>Unset is int.MinValue, not -1: a quicktest game starts near tick 0, so a test that moves this tick back 60 s makes it negative.</summary>
        private int fromTick = int.MinValue;

        private static readonly HediffStage BuildingStage = new HediffStage();
        /// <summary>One stage per consciousness cap in hundredths, shared by every pawn.</summary>
        private static readonly Dictionary<int, HediffStage> CapStages = new Dictionary<int, HediffStage>();

        private HediffCompProperties_VoidOverload Props =>
            this.TryGetComp<HediffComp_VoidOverload>()?.Props ?? new HediffCompProperties_VoidOverload();

        /// <summary>The consciousness cap in hundredths: 100 minus the overload rounded up to a whole percent, never below the floor.</summary>
        public int CapHundredths
        {
            get
            {
                int overload = Mathf.Clamp(Mathf.CeilToInt(Severity * 100f - 0.01f), 0, 100);
                return Mathf.Max(Mathf.RoundToInt(Props.consciousnessFloor * 100f), 100 - overload);
            }
        }

        public override HediffStage CurStage => building ? BuildingStage : StageFor(CapHundredths);

        public override bool ShouldRemove => !building && Severity <= 0f;

        public override string SeverityLabel => Severity.ToStringPercent();

        private static HediffStage StageFor(int cap)
        {
            if (CapStages.TryGetValue(cap, out HediffStage stage)) return stage;
            stage = new HediffStage
            {
                capMods = new List<PawnCapacityModifier> { new PawnCapacityModifier { capacity = PawnCapacityDefOf.Consciousness, setMax = cap / 100f } },
            };
            CapStages[cap] = stage;
            return stage;
        }

        /// <summary>The pawn is taken (again) into a domain: the overload builds from what it has now and caps nothing while it does.</summary>
        public void Build()
        {
            if (building) return;
            building = true;
            if (pawn.health.hediffSet.hediffs.Contains(this)) pawn.health.Notify_HediffChanged(this);
        }

        /// <summary>The pawn is out of the domain at <paramref name="now"/>: the cap applies and the overload starts to fall.</summary>
        public void Release(int now)
        {
            building = false;
            fromSeverity = Severity;
            fromTick = now;
            if (Severity > Props.scarAbove + 0.0001f) scarOwed = true;
            if (pawn.health.hediffSet.hediffs.Contains(this)) pawn.health.Notify_HediffChanged(this);
        }

        /// <summary>For game tests: as if <paramref name="seconds"/> more had gone by since the pawn came out.</summary>
        public void SkipForTests(float seconds) => fromTick -= Mathf.RoundToInt(seconds * 60f);

        public override void Tick()
        {
            base.Tick();
            Recover();
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            Recover();
        }

        private void Recover()
        {
            if (pawn == null || pawn.Dead) return;
            int now = Find.TickManager.TicksGame;
            if (building)
            {
                // Left building by a domain that is gone (a test reset, a broken save): come out now.
                if (pawn.IsHashIntervalTick(60) && GameComponent_UnlimitedVoid.Instance?.Holds(pawn) != true) Release(now);
                return;
            }
            if (fromTick == int.MinValue) Release(now);
            float want = Mathf.Max(0f, fromSeverity - (now - fromTick) / 60f * Props.recoveryPerSecond);
            if (want >= Severity) return;
            int before = CapHundredths;
            Severity = want;
            if (CapHundredths != before) pawn.health.Notify_HediffChanged(this);
            if (Severity <= 0f && scarOwed)
            {
                scarOwed = false;
                if (Props.scarHediff != null) pawn.health.AddHediff(Props.scarHediff);
            }
        }

        public override string TipStringExtra
        {
            get
            {
                var sb = new StringBuilder(base.TipStringExtra);
                HediffCompProperties_VoidOverload props = Props;
                if (building)
                    sb.AppendLine("Frozen in Unlimited Void. When it ends, consciousness is capped at 100 % minus the overload (never below "
                                  + props.consciousnessFloor.ToStringPercent() + ").");
                else if (props.recoveryPerSecond > 0f)
                    sb.AppendLine("Falls " + props.recoveryPerSecond.ToStringPercent("0.#") + " per second: gone in "
                                  + (Severity / props.recoveryPerSecond).ToString("0") + " s.");
                if (scarOwed && props.scarHediff != null) sb.AppendLine("Went down: " + props.scarHediff.label + " when the overload has gone.");
                return sb.ToString().TrimEndNewlines();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref building, "building", true);
            Scribe_Values.Look(ref scarOwed, "scarOwed");
            Scribe_Values.Look(ref fromSeverity, "fromSeverity");
            Scribe_Values.Look(ref fromTick, "fromTick", int.MinValue);
        }
    }
}
