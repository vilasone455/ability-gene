using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public sealed class GameComponent_Gravity : GameComponent
    {
        private Dictionary<Pawn, int> cooldowns = new Dictionary<Pawn, int>();
        private List<Pawn> cooldownKeys;
        private List<int> cooldownValues;
        private int nextId;
        public GameComponent_Gravity(Game game) { MapComponent_Gravity.ClearLive(); }
        public static GameComponent_Gravity Instance => Current.Game?.GetComponent<GameComponent_Gravity>();
        public int NextId() => ++nextId;
        public int Remaining(Pawn pawn) => pawn != null && cooldowns.TryGetValue(pawn, out int until)
            ? Mathf.Max(0, until - Find.TickManager.TicksGame) : 0;
        public void Commit(Pawn pawn, int ticks)
        {
            if (pawn != null) cooldowns[pawn] = Find.TickManager.TicksGame + ticks;
        }
        public void ResetForTests() => cooldowns.Clear();
        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 600 != 0) return;
            foreach (var pawn in cooldowns.Where(pair => pair.Value <= Find.TickManager.TicksGame).Select(pair => pair.Key).ToArray())
                cooldowns.Remove(pawn);
        }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref nextId, "gravityNextId");
            Scribe_Collections.Look(ref cooldowns, "gravityCooldowns", LookMode.Reference, LookMode.Value,
                ref cooldownKeys, ref cooldownValues);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                cooldowns ??= new Dictionary<Pawn, int>();
        }
    }

    public sealed class MapComponent_Gravity : MapComponent
    {
        private List<GravityCast> casts = new List<GravityCast>();
        private List<GravityMotion> motions = new List<GravityMotion>();
        private readonly Dictionary<Thing, GravityMotion> motionIndex = new Dictionary<Thing, GravityMotion>();

        // Components with a cast or a moving thing. The Harmony patches on every pawn tick and every
        // DrawPos read check this list first, so a map without a well pays one count check.
        private static readonly List<MapComponent_Gravity> live = new List<MapComponent_Gravity>();
        public static void ClearLive() => live.Clear();
        private void SetLive(bool on)
        {
            bool listed = live.Contains(this);
            if (on && !listed) live.Add(this);
            else if (!on && listed) live.Remove(this);
        }

        // Owner is asked several times a tick per thing (pather patches, draw, release); the answer
        // is kept for the tick and the exact position it was asked for.
        private struct OwnerAnswer { public int tick; public Vector3 position; public GravityCast owner; }
        private readonly Dictionary<Thing, OwnerAnswer> owners = new Dictionary<Thing, OwnerAnswer>();
        private int anyFieldTick = -1;
        private bool anyField;

        // Reused per tick.
        private readonly List<Thing> nearby = new List<Thing>();
        private readonly HashSet<Thing> items = new HashSet<Thing>();
        private readonly List<GravityCast> castBuffer = new List<GravityCast>();

        public IEnumerable<GravityCast> Casts => casts;
        public IReadOnlyList<GravityMotion> Motions => motions;
        public MapComponent_Gravity(Map map) : base(map) { }

        // Any map: for starting a cast.
        public static MapComponent_Gravity On(Thing thing) => thing?.Map?.GetComponent<MapComponent_Gravity>();
        // Only a map with a cast or a moving thing: for the per-tick and per-draw patches.
        public static MapComponent_Gravity Live(Thing thing)
        {
            if (live.Count == 0 || thing == null) return null;
            Map thingMap = thing.Map;
            if (thingMap == null) return null;
            for (int i = 0; i < live.Count; i++)
                if (live[i].map == thingMap) return live[i];
            return null;
        }

        public GravityCast For(Pawn pawn)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == pawn && casts[i].Busy) return casts[i];
            return null;
        }

        // The cast that holds this pawn in its clip: attacks and orders are refused while it lasts. A well
        // that does not hold its caster (holdsCaster false) never does.
        public GravityCast Holding(Pawn pawn)
        {
            GravityCast cast = For(pawn);
            return cast != null && cast.Props.holdsCaster ? cast : null;
        }

        // Worked out once per tick.
        public bool AnyField
        {
            get
            {
                int now = Find.TickManager.TicksGame;
                if (anyFieldTick != now)
                {
                    anyFieldTick = now;
                    anyField = false;
                    for (int i = 0; i < casts.Count && !anyField; i++)
                        anyField = casts[i].Field && casts[i].Valid;
                }
                return anyField;
            }
        }

        // A cast opened or ended: forget this tick's Owner and AnyField answers.
        public void Changed() { owners.Clear(); anyFieldTick = -1; }

        public static bool CanPay(Pawn pawn, AbilityDef def, out float cost)
        {
            EchoRecord record = EchoUtility.ManifestedWith(pawn, def);
            cost = record?.def.CastCost(def) ?? 0f;
            return cost <= 0f || GameComponent_Echoes.Get.charge >= cost;
        }

        // Opens a well. The Echo cast cost is paid here, when the well is sure to open.
        public bool Begin(Pawn pawn, IntVec3 cell, AbilityDef def)
        {
            var props = CompProperties_AbilityGravityWell.For(def);
            if (props == null || For(pawn) != null || GameComponent_Gravity.Instance.Remaining(pawn) > 0
                || !GravityAcquisition.HasAbility(pawn, def) || !GravityCommands.ValidTarget(pawn, cell, props.range)) return false;
            if (!CanPay(pawn, def, out float cost))
            {
                Messages.Message("AG_EchoCastNoCharge".Translate(cost.ToString("0"), GameComponent_Echoes.Get.charge.ToString("0")),
                    pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            CastClips.Handle animation = null;
            if (props.holdsCaster && !GravityCastAnimation.Clip.TryStart(pawn, out animation)) return false;
            if (cost > 0f) GameComponent_Echoes.Get.TrySpend(cost);
            // A free caster turns to the cell and goes on with what he was doing.
            if (!props.holdsCaster) pawn.rotationTracker.FaceCell(cell);
            Vector3 at = cell.ToVector3Shifted();
            casts.Add(new GravityCast { id = GameComponent_Gravity.Instance.NextId(), caster = pawn, def = def,
                anchor = pawn.Position, origin = at, centre = at, map = map, animation = animation, paid = cost,
                startTick = Find.TickManager.TicksGame });
            SetLive(true);
            Changed();
            return true;
        }

        public bool Eligible(Thing thing)
        {
            if (thing == null || !thing.Spawned || thing.Map != map || thing.Destroyed
                || MapComponent_RetrievalHooks.IsTargeted(thing)) return false;
            // A pawn pinned by Black Receiver or carried by Banshō Ten'in is not moved (docs/hero-echo.md, Pain).
            if (thing is Pawn pawn) return !pawn.Dead && pawn.ParentHolder == map && !PainKit.Unmovable(pawn);
            return thing is Corpse || (thing.def.category == ThingCategory.Item && thing.def.EverHaulable);
        }

        // Stable strongest-field arbitration is used for movement, eating and drift.
        public GravityCast Owner(Thing thing, Vector3 position)
        {
            if (thing == null || !thing.Spawned || thing.Map != map) return null;
            int now = Find.TickManager.TicksGame;
            if (owners.TryGetValue(thing, out var answer) && answer.tick == now && answer.position == position)
                return answer.owner;
            GravityCast best = null;
            if (Eligible(thing))
            {
                float strongest = -1f;
                IntVec3 at = position.ToIntVec3();
                for (int i = 0; i < casts.Count; i++)
                {
                    var cast = casts[i];
                    if (!cast.Field || thing == cast.caster || !cast.Valid) continue;
                    float distance = (position - cast.Centre).Yto0().magnitude, radius = cast.Radius;
                    // The thing's own cell, not Thing.Position: items commit their cell late.
                    if (distance >= radius || !cast.ClearTo(at)) continue;
                    float pull = cast.Props.Pull(distance, cast.Props.Resistance(thing), radius);
                    if (pull > strongest || (pull == strongest && (best == null || cast.id < best.id)))
                    { best = cast; strongest = pull; }
                }
            }
            if (owners.Count > 4096) owners.Clear();
            owners[thing] = new OwnerAnswer { tick = now, position = position, owner = best };
            return best;
        }

        public Vector3 PositionOf(Thing thing) => motionIndex.TryGetValue(thing, out var motion)
            && motion.cell == thing.Position ? motion.position : thing.Position.ToVector3Shifted();

        public GravityMotion MotionFor(Thing thing)
        {
            if (!AnyField) return null;
            Vector3 position = PositionOf(thing);
            if (Owner(thing, position) == null) return null;
            if (!motionIndex.TryGetValue(thing, out var motion))
            {
                motion = new GravityMotion { thing = thing, position = position, start = position, cell = thing.Position };
                motions.Add(motion); motionIndex.Add(thing, motion);
                if (!(thing is Pawn)) thing.Map.mapDrawer.MapMeshDirty(thing.Position, MapMeshFlagDefOf.Things);
            }
            if (motion.cell != thing.Position)
            { motion.position = thing.Position.ToVector3Shifted(); motion.cell = thing.Position; }
            return motion;
        }

        public bool DrawPosition(Thing thing, out Vector3 position)
        {
            position = default;
            if (!motionIndex.TryGetValue(thing, out var motion) || !thing.Spawned || motion.cell != thing.Position
                || Owner(thing, motion.position) == null) return false;
            position = motion.position;
            return true;
        }

        // Things whose position is within radius of the centre, from the cells within radius + 2
        // (an item's grid cell can trail its drawn position by one commit). Fills the given list.
        private void Nearby(GravityCast cast, float radius, List<Thing> into)
        {
            into.Clear();
            IntVec3 root = cast.Cell;
            Vector3 centre = cast.Centre;
            float limit = radius * radius;
            int cells = GenRadial.NumCellsInRadius(Mathf.Min(radius + 2f, GenRadial.MaxRadialPatternRadius));
            for (int i = 0; i < cells; i++)
            {
                IntVec3 cell = root + GenRadial.RadialPattern[i];
                if (!cell.InBounds(map)) continue;
                List<Thing> things = map.thingGrid.ThingsListAtFast(cell);
                for (int j = 0; j < things.Count; j++)
                    if ((PositionOf(things[j]) - centre).Yto0().sqrMagnitude <= limit) into.Add(things[j]);
            }
        }

        // Returns the pawns it hurt.
        public List<Pawn> DamagePawns(GravityCast cast, float radius, float damage)
        {
            // Burst is called after the phase is committed; do not depend on Owner then.
            var near = new List<Thing>();
            var hit = new List<Pawn>();
            Nearby(cast, radius, near);
            foreach (var thing in near)
            {
                if (!(thing is Pawn pawn) || pawn == cast.caster || pawn.Dead || !pawn.Spawned
                    || !GravityMovement.Clear(map, cast.Cell, pawn.Position)) continue;
                if (cast.Field && Owner(pawn, PositionOf(pawn)) != cast) continue;
                // Armour penetration 0: normal armour applies. (It was -1, which the armour roll reads as
                // +1 armour on every pawn: half the hits deflected, the rest halved.)
                pawn.TakeDamage(new DamageInfo(DamageDefOf.Blunt, damage, 0f, -1f, cast.caster));
                hit.Add(pawn);
            }
            return hit;
        }

        // Items and corpses in the core are destroyed and counted; pawns are counted once and stay.
        private void EatCore(GravityCast cast)
        {
            float core = cast.Props.coreRadius;
            for (int i = motions.Count - 1; i >= 0; i--)
            {
                if (i >= motions.Count) continue;
                var motion = motions[i];
                Thing thing = motion.thing;
                if (thing == null || !thing.Spawned || motion.cell != thing.Position
                    || (motion.position - cast.Centre).Yto0().magnitude > core
                    || Owner(thing, motion.position) != cast) continue;
                if (thing is Pawn || thing.def.destroyable) cast.Eat(thing);
            }
        }

        // Drift: the centre moves toward the heaviest thing this well pulls. Target rule: among the
        // pawns (never the caster), corpses and items it owns (inside the pull radius, clear line,
        // strongest pull of all wells), the one with the largest Mass(); a tie goes to the one nearer
        // the centre, then to the lower thingIDNumber. Picked again every DriftPickTicks, or at once
        // when the target is gone or no longer owned. The goal is the target's position clamped to
        // leash cells from the cast point; the centre moves driftSpeed cells per second toward it and
        // does not enter a cell it cannot cross into or that the caster has no clear line to.
        private void Drift(GravityCast cast)
        {
            var props = cast.Props;
            if (props.driftSpeed <= 0f) return;
            int now = Find.TickManager.TicksGame;
            Thing target = cast.driftTarget;
            if (target == null || now - cast.driftPickTick >= GravityRules.DriftPickTicks || !target.Spawned
                || Owner(target, PositionOf(target)) != cast)
            {
                cast.driftTarget = target = Heaviest(cast);
                cast.driftPickTick = now;
            }
            if (target == null) return;
            Vector3 goal = PositionOf(target).Yto0(), origin = cast.origin.Yto0();
            Vector3 fromOrigin = goal - origin;
            if (fromOrigin.magnitude > props.leash) goal = origin + fromOrigin.normalized * props.leash;
            Vector3 offset = goal - cast.centre.Yto0();
            float distance = offset.magnitude;
            if (distance < 0.001f) return;
            Vector3 next = cast.centre.Yto0() + offset * Mathf.Min(1f, props.driftSpeed / 60f / distance);
            IntVec3 from = cast.Cell, to = next.ToIntVec3();
            if (to != from && (!GravityMovement.Crossable(map, from, to) || !GravityMovement.Clear(map, cast.anchor, to))) return;
            cast.centre = next;
        }

        private Thing Heaviest(GravityCast cast)
        {
            Nearby(cast, cast.Radius, nearby);
            Thing best = null;
            float heaviest = -1f, nearest = 0f;
            foreach (var thing in nearby)
            {
                if (Owner(thing, PositionOf(thing)) != cast) continue;
                float mass = cast.Props.Mass(thing), distance = (PositionOf(thing) - cast.Centre).Yto0().sqrMagnitude;
                if (mass > heaviest || (mass == heaviest && (distance < nearest
                    || (distance == nearest && thing.thingIDNumber < best.thingIDNumber))))
                { best = thing; heaviest = mass; nearest = distance; }
            }
            return best;
        }

        public override void MapComponentTick()
        {
            if (casts.Count == 0 && motions.Count == 0) { SetLive(false); return; }
            castBuffer.Clear(); castBuffer.AddRange(casts);
            foreach (var cast in castBuffer) cast.Tick();
            if (AnyField)
            {
                foreach (var cast in castBuffer)
                    if (cast.Field && cast.Valid) Drift(cast);
                // New items and corpses in a field get a motion; every item motion takes its step.
                items.Clear();
                foreach (var cast in castBuffer)
                {
                    if (!cast.Field) continue;
                    Nearby(cast, cast.Radius, nearby);
                    foreach (var thing in nearby)
                        if (!(thing is Pawn) && !motionIndex.ContainsKey(thing) && Eligible(thing)) items.Add(thing);
                }
                foreach (var item in items) MotionFor(item);
                for (int i = 0; i < motions.Count; i++)
                {
                    var motion = motions[i];
                    if (motion.thing is Pawn || motion.thing == null || !motion.thing.Spawned) continue;
                    if (MotionFor(motion.thing) == motion) GravityMovement.Step(this, motion, Vector3.zero);
                }
                foreach (var cast in castBuffer)
                {
                    if (!cast.Field) continue;
                    if (!cast.Valid) { cast.Finish(false); continue; }
                    EatCore(cast);
                    if (cast.clock.ticks > 0 && cast.clock.ticks % 60 == 0 && cast.Props.coreDamage > 0f)
                        DamagePawns(cast, cast.Props.coreRadius, cast.Props.coreDamage);
                    if (cast.clock.ticks >= cast.DurationTicks && cast.Active) cast.Finish(true);
                }
            }
            ReleaseUncontrolled();
            casts.RemoveAll(c => !c.Busy && c.tailTicks <= 0);
            if (casts.Count == 0 && motions.Count == 0) SetLive(false);
        }

        public void ReleaseUncontrolled()
        {
            for (int i = motions.Count - 1; i >= 0; i--)
            {
                var motion = motions[i];
                if (motion.thing != null && Eligible(motion.thing) && Owner(motion.thing, motion.position) != null) continue;
                if (motion.thing != null) motionIndex.Remove(motion.thing);
                GravityMovement.Release(motion);
                motions.RemoveAt(i);
            }
        }

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map || (casts.Count == 0 && motions.Count == 0)) { GravityProjectiles.Draw(map); return; }
            foreach (var cast in casts)
                if (cast.Props.look == GravityLook.GojoBlue) GojoBlueLook.Draw(this, cast);
                else if ((cast.Field || cast.tailTicks > 0) && !cast.Cell.Fogged(map))
                    GravityGraphics.Draw(cast.Centre, cast.clock.ticks / 60f, cast.Growth, cast.Radius, cast.Props.coreRadius,
                        cast.Field ? cast.TicksLeft / (float)Mathf.Max(1, cast.DurationTicks) : -1f,
                        cast.Active ? 1f : cast.tailTicks / 30f, cast.clock.imploded, cast.burstRadius, map);
            foreach (var motion in motions)
                if (!(motion.thing is Pawn) && motion.thing?.Spawned == true
                    && motion.thing.def.drawerType == DrawerType.MapMeshOnly && !motion.cell.Fogged(map))
                    motion.thing.DrawNowAt(motion.position.WithY(motion.thing.def.Altitude));
            GravityProjectiles.Draw(map);
        }

        // Seconds left, under the well.
        public override void MapComponentOnGUI()
        {
            if (Find.CurrentMap != map || casts.Count == 0) return;
            foreach (var cast in casts)
            {
                // Gravity Well's timer; Gojo's Blue picture has none.
                if (!cast.Field || cast.Props.look != GravityLook.Well || cast.Cell.Fogged(map)) continue;
                Vector3 at = cast.Centre + new Vector3(0f, 0f, -(cast.Props.coreRadius + 0.35f));
                Vector2 screen = Find.Camera.WorldToScreenPoint(at) / Prefs.UIScale;
                screen.y = UI.screenHeight - screen.y;
                GenMapUI.DrawThingLabel(screen, (cast.TicksLeft / 60f).ToString("0.0") + " s", Color.white);
            }
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref casts, "gravityCasts", LookMode.Deep);
            Scribe_Collections.Look(ref motions, "gravityMotions", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                casts ??= new List<GravityCast>(); motions ??= new List<GravityMotion>();
                motions.RemoveAll(m => m.thing == null);
                motionIndex.Clear();
                foreach (var cast in casts) cast.map = map;
                foreach (var motion in motions) motionIndex[motion.thing] = motion;
                if (casts.Count > 0 || motions.Count > 0) SetLive(true);
            }
        }
        public override void MapRemoved()
        {
            foreach (var cast in casts) cast.Finish(false);
            foreach (var cast in casts) cast.StopAnimation();
            foreach (var motion in motions) GravityMovement.Release(motion);
            casts.Clear(); motions.Clear(); motionIndex.Clear(); owners.Clear();
            SetLive(false);
            base.MapRemoved();
        }
    }
}
