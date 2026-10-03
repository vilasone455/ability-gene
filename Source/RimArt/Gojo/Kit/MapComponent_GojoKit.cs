using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using R = RimArt.GojoRed;

namespace RimArt
{
    /// <summary>
    /// Runs Gojo's Reds (<see cref="RedShot"/>) and Hollow Purples (<see cref="PurpleRun"/>) on a map: the rules on game
    /// ticks, the pictures once a frame between PawnFit.Begin and End. Blue is a Gravity Well cast
    /// (<see cref="MapComponent_Gravity"/>, look <see cref="GojoBlueLook"/>); a Red meeting one makes Purple here.
    /// </summary>
    public sealed class MapComponent_GojoKit : MapComponent
    {
        private List<RedShot> reds = new List<RedShot>();
        private List<PurpleRun> purples = new List<PurpleRun>();

        public MapComponent_GojoKit(Map map) : base(map) { }

        public IReadOnlyList<RedShot> Reds => reds;
        public IReadOnlyList<PurpleRun> Purples => purples;

        public static MapComponent_GojoKit On(Map map) => map?.GetComponent<MapComponent_GojoKit>();

        // ---- Red ----------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The warmup begins (the cast job): the picture starts now. Its charge is the warmup as the verb will run it
        /// (scaled by the aiming delay), less the arm's lead.
        /// </summary>
        public void BeginRed(Pawn caster, IntVec3 target, int now, AbilityDef def)
        {
            reds.RemoveAll(r => r.caster == caster && !r.Fired);
            float warmup = def.verbProperties.warmupTime * caster.GetStatValue(StatDefOf.AimingDelayFactor);
            reds.Add(new RedShot { caster = caster, target = target, startTick = now, expectedCharge = Mathf.Max(0.05f, warmup - R.Start) });
        }

        /// <summary>
        /// The ability fired: the shot its job began, or, when it fired without the job (a direct Activate), a new
        /// one whose warmup ended now.
        /// </summary>
        public void FireRed(Ability ability, IntVec3 target)
        {
            int now = Find.TickManager.TicksGame;
            Pawn caster = ability.pawn;
            RedShot shot = reds.Find(r => r.caster == caster && !r.Fired);
            if (shot == null)
            {
                shot = new RedShot { caster = caster, startTick = now - Mathf.RoundToInt((R.Start + R.Charge) * 60f) };
                reds.Add(shot);
            }
            shot.Fire(ability, target, now);
        }

        /// <summary>Red's cast job ended: a shot that never fired is dropped (no cost, no cooldown).</summary>
        public void JobEnded(Pawn caster) => reds.RemoveAll(r => r.caster == caster && !r.Fired);

        /// <summary>The fired Red that keeps <paramref name="caster"/> standing with his arm out.</summary>
        public RedShot Holding(Pawn caster, int now)
        {
            for (int i = 0; i < reds.Count; i++)
                if (reds[i].caster == caster && reds[i].Holds(now)) return reds[i];
            return null;
        }

        // ---- Hollow Purple ----------------------------------------------------------------------------------------------

        /// <summary>
        /// What a Red's walk needs to know about its Gojo's Blues, worked out once per tick rather than at every
        /// 0.1-cell step: the Blues he has pulling now, whether Hollow Purple is ready, and Purple's numbers.
        /// </summary>
        public readonly struct BlueWatch
        {
            public readonly List<GravityCast> blues;
            public readonly bool purpleReady;
            public readonly CompProperties_GojoHollowPurple purple;

            public BlueWatch(List<GravityCast> blues, bool purpleReady, CompProperties_GojoHollowPurple purple)
            {
                this.blues = blues;
                this.purpleReady = purpleReady;
                this.purple = purple;
            }
        }

        private static CompProperties_GojoHollowPurple purpleProps;
        private static CompProperties_GojoHollowPurple PurpleProps =>
            purpleProps ??= GojoKit.Props<CompProperties_GojoHollowPurple>(GojoKitDefOf.AG_GojoHollowPurple);

        /// <summary>
        /// <paramref name="gojo"/>'s Blues pulling on this map now, and whether Purple is ready. The list is reused by
        /// the next call: use the watch within the tick it was made for.
        /// </summary>
        public BlueWatch Watch(Pawn gojo)
        {
            blueBuffer.Clear();
            MapComponent_Gravity gravity = map.GetComponent<MapComponent_Gravity>();
            if (gravity != null)
                foreach (GravityCast blue in gravity.Casts)
                    if (blue.caster == gojo && blue.def == GojoKitDefOf.AG_GojoBlue && blue.Field) blueBuffer.Add(blue);
            bool ready = blueBuffer.Count > 0 && PurpleReady(gojo, GojoKitDefOf.AG_GojoHollowPurple, out _, out _);
            return new BlueWatch(blueBuffer, ready, PurpleProps);
        }

        private readonly List<GravityCast> blueBuffer = new List<GravityCast>();

        /// <summary>
        /// Called at each step of a Red's flight. When the step is within Hollow Purple's blueRadius of the centre
        /// of an active Blue cast by the same Gojo: if Purple is ready (Gojo has it, off cooldown, the Echo pool can
        /// pay its charge) it is paid, its cooldown starts, the Blue closes at once with no implosion (letting go of
        /// what it held; its own cooldown starts), the Red is used up and Purple starts at the Blue's centre along
        /// Red's way. True then. Otherwise Red passes through.
        /// </summary>
        public bool TryHollowPurple(RedShot shot, Vector2 at, int now, in BlueWatch watch)
        {
            AbilityDef def = GojoKitDefOf.AG_GojoHollowPurple;
            var props = watch.purple;
            if (!watch.purpleReady || props == null) return false;
            foreach (GravityCast blue in watch.blues)
            {
                if (!blue.Field) continue;
                Vector2 centre = GojoKit.Ground(blue.Centre);
                if ((centre - at).magnitude > props.blueRadius) continue;
                // Ready was worked out this tick; asked again once here, where the charge is taken.
                if (!PurpleReady(shot.caster, def, out Ability purple, out float cost)) return false;
                if (cost > 0f && !GameComponent_Echoes.Get.TrySpend(cost)) return false;
                purple.StartCooldown(def.cooldownTicksRange.RandomInRange);
                blue.Finish(false);
                shot.Use(now);
                purples.Add(new PurpleRun
                {
                    caster = shot.caster, comboTick = now, start = centre, dir = shot.dir, armAt = shot.origin,
                    blueDist = Mathf.Max(0.05f, Vector2.Dot(centre - shot.origin, shot.dir)),
                    travel = Mathf.Min(props.maxTravel, PurpleRun.ToEdge(map, centre, shot.dir)),
                    radius = props.radius, speed = props.speed, centreRow = props.centreRow, damage = props.damage,
                });
                return true;
            }
            return false;
        }

        /// <summary>
        /// While Hollow Purple is ready, Red flies through what is inside the pull of an active Blue that Gojo cast, so the
        /// pawns Blue has caught do not stop it short of the centre (the user's rule, 2026-09-30). Walls still stop it.
        /// </summary>
        public static bool PassesThroughBlue(in BlueWatch watch, Vector2 at)
        {
            if (!watch.purpleReady) return false;
            foreach (GravityCast blue in watch.blues)
                if (blue.Field && (GojoKit.Ground(blue.Centre) - at).magnitude <= blue.Radius) return true;
            return false;
        }

        /// <summary>Gojo has Hollow Purple, it is off cooldown and the Echo pool (when he is a manifested Host) can pay it.</summary>
        public static bool PurpleReady(Pawn gojo, AbilityDef def, out Ability purple, out float cost)
        {
            cost = 0f;
            purple = gojo?.abilities?.GetAbility(def, includeTemporary: true);
            if (purple == null || gojo.Dead || purple.CooldownTicksRemaining > 0) return false;
            // Purple is never Activated, so the Echo's cast cost is taken here (as EchoCastPayment.Pay would).
            EchoRecord record = EchoUtility.ManifestedWith(gojo, def);
            cost = record?.def.CastCost(def) ?? 0f;
            return cost <= 0f || GameComponent_Echoes.Get.charge >= cost;
        }

        public void ResetForTests()
        {
            foreach (RedShot shot in reds) shot.PutDownItems(map);
            reds.Clear();
            purples.Clear();
        }

        public override void MapComponentTick()
        {
            if (reds.Count == 0 && purples.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            for (int i = reds.Count - 1; i >= 0; i--)
                if (i < reds.Count && !reds[i].Tick(this, now))
                {
                    reds[i].PutDownItems(map);
                    reds.RemoveAt(i);
                }
            for (int i = purples.Count - 1; i >= 0; i--)
                if (!purples[i].Tick(map, now)) purples.RemoveAt(i);
        }

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map || (reds.Count == 0 && purples.Count == 0)) return;
            PawnFit.Begin();
            try
            {
                for (int i = 0; i < reds.Count; i++) reds[i].Draw(map);
                for (int i = 0; i < purples.Count; i++) purples[i].Draw(map);
            }
            finally
            {
                PawnFit.End();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref reds, "gojoReds", LookMode.Deep);
            Scribe_Collections.Look(ref purples, "gojoPurples", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                reds ??= new List<RedShot>();
                purples ??= new List<PurpleRun>();
                // A shot that had not fired has no job left to fire it. A fired one is kept even if its Gojo did not
                // load: it may be carrying an item off the map, which it puts down when it lands.
                reds.RemoveAll(r => r == null || !r.Fired);
                purples.RemoveAll(p => p == null);
            }
        }
    }
}
