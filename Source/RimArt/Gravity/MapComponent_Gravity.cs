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
        public GameComponent_Gravity(Game game) { }
        public static GameComponent_Gravity Instance => Current.Game?.GetComponent<GameComponent_Gravity>();
        public int NextId() => ++nextId;
        public int Remaining(Pawn pawn) => pawn != null && cooldowns.TryGetValue(pawn, out int until)
            ? Mathf.Max(0, until - Find.TickManager.TicksGame) : 0;
        public void Commit(Pawn pawn)
        {
            if (pawn != null) cooldowns[pawn] = Find.TickManager.TicksGame + GravityRules.CooldownTicks;
        }
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
        public IEnumerable<GravityCast> Casts => casts;
        public bool AnyField => casts.Any(c => c.Field && c.Valid);
        public MapComponent_Gravity(Map map) : base(map) { }
        public static MapComponent_Gravity On(Thing thing) => thing?.Map?.GetComponent<MapComponent_Gravity>();
        public GravityCast For(Pawn pawn) => casts.FirstOrDefault(c => c.caster == pawn && c.Busy);

        public void Begin(Pawn pawn, IntVec3 cell)
        {
            if (For(pawn) != null || GameComponent_Gravity.Instance.Remaining(pawn) > 0
                || !GravityAcquisition.HasEye(pawn) || !GravityCommands.ValidTarget(pawn, cell)) return;
            if (!GravityCastAnimation.TryStart(pawn, out var animation)) return;
            casts.Add(new GravityCast { id = GameComponent_Gravity.Instance.NextId(), caster = pawn,
                anchor = pawn.Position, cell = cell, map = map, animation = animation });
        }

        public static float Mass(Thing thing)
        {
            if (thing is Pawn pawn) return GravityRules.BodyMass * pawn.BodySize;
            if (thing is Corpse corpse) return GravityRules.BodyMass * corpse.InnerPawn.BodySize;
            return Mathf.Max(0f, thing.GetStatValue(StatDefOf.Mass) * thing.stackCount);
        }
        public static float Resistance(Thing thing) => thing is Pawn pawn
            ? pawn.BodySize : Mass(thing) / GravityRules.BodyMass;

        public bool Eligible(Thing thing)
        {
            if (thing == null || !thing.Spawned || thing.Map != map || thing.Destroyed
                || MapComponent_RetrievalHooks.IsTargeted(thing)) return false;
            if (thing is Pawn pawn) return !pawn.Dead && pawn.ParentHolder == map;
            return thing is Corpse || (thing.def.category == ThingCategory.Item && thing.def.EverHaulable);
        }

        // Stable strongest-field arbitration is used for both movement and mass accounting.
        public GravityCast Owner(Thing thing, Vector3 position)
        {
            if (!Eligible(thing)) return null;
            GravityCast best = null;
            float strongest = -1f;
            foreach (var cast in casts)
            {
                if (!cast.Field || !cast.Valid || thing == cast.caster) continue;
                float distance = (position - cast.Centre).Yto0().magnitude;
                if (distance >= GravityRules.Radius || !GravityMovement.Clear(map, cast.cell, thing.Position)) continue;
                float pull = GravityRules.Pull(distance, Resistance(thing));
                if (pull > strongest || (pull == strongest && (best == null || cast.id < best.id)))
                { best = cast; strongest = pull; }
            }
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
                motion = new GravityMotion { thing = thing, position = position, cell = thing.Position };
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

        private IEnumerable<Thing> Nearby(GravityCast cast, float radius)
        {
            // Cell enumeration stays local even on a map with a large stockpile.
            foreach (var cell in GenRadial.RadialCellsAround(cast.cell, radius + 1f, true))
                if (cell.InBounds(map))
                    foreach (var thing in cell.GetThingList(map).ToArray())
                        if ((PositionOf(thing) - cast.Centre).Yto0().magnitude <= radius) yield return thing;
        }
        public float CoreMass(GravityCast cast) => Nearby(cast, GravityRules.Core).Distinct()
            .Where(t => Owner(t, PositionOf(t)) == cast).Sum(Mass);

        public void DamagePawns(GravityCast cast, float radius, float damage)
        {
            // Burst is called after the phase is committed; do not depend on Owner here.
            foreach (Pawn pawn in Nearby(cast, radius).OfType<Pawn>().Distinct().ToArray())
            {
                if (pawn == cast.caster || pawn.Dead || !GravityMovement.Clear(map, cast.cell, pawn.Position)) continue;
                if (cast.Field && Owner(pawn, PositionOf(pawn)) != cast) continue;
                pawn.TakeDamage(new DamageInfo(DamageDefOf.Blunt, damage, -1f, -1f, cast.caster));
            }
        }

        public override void MapComponentTick()
        {
            foreach (var cast in casts.ToArray())
            {
                if (cast.Field) cast.mass = CoreMass(cast);
                cast.Tick();
            }
            if (AnyField)
            {
                var items = new HashSet<Thing>();
                foreach (var cast in casts.Where(c => c.Field))
                    foreach (var thing in Nearby(cast, GravityRules.Radius))
                        if (!(thing is Pawn) && Eligible(thing)) items.Add(thing);
                foreach (var item in items)
                {
                    var motion = MotionFor(item);
                    if (motion != null) GravityMovement.Step(this, motion, Vector3.zero);
                }
                foreach (var cast in casts.Where(c => c.Field).ToArray())
                {
                    if (!cast.Valid) { cast.Finish(false); continue; }
                    cast.mass = CoreMass(cast);
                    if (cast.clock.ticks > 0 && cast.clock.ticks % 60 == 0)
                        DamagePawns(cast, GravityRules.Core, GravityRules.CoreDamage);
                    if (cast.clock.ticks >= GravityRules.DurationTicks && cast.Active) cast.Finish(true);
                }
            }
            ReleaseUncontrolled();
            casts.RemoveAll(c => !c.Busy && c.tailTicks <= 0);
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
            if (Find.CurrentMap != map) return;
            foreach (var cast in casts)
                if ((cast.Field || cast.tailTicks > 0) && !cast.cell.Fogged(map))
                    GravityGraphics.Draw(cast.Centre, cast.clock.ticks / 60f, cast.mass,
                        cast.Active ? 1f : cast.tailTicks / 30f, cast.clock.imploded, map);
            foreach (var motion in motions)
                if (!(motion.thing is Pawn) && motion.thing?.Spawned == true
                    && motion.thing.def.drawerType == DrawerType.MapMeshOnly && !motion.cell.Fogged(map))
                    motion.thing.DrawNowAt(motion.position.WithY(motion.thing.def.Altitude));
            GravityProjectiles.Draw(map);
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref casts, "gravityCasts", LookMode.Deep);
            Scribe_Collections.Look(ref motions, "gravityMotions", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                casts ??= new List<GravityCast>(); motions ??= new List<GravityMotion>();
                motionIndex.Clear();
                foreach (var cast in casts) cast.map = map;
                foreach (var motion in motions)
                    if (motion.thing != null) motionIndex[motion.thing] = motion;
            }
        }
        public override void MapRemoved()
        {
            foreach (var cast in casts) cast.Finish(false);
            foreach (var cast in casts) cast.StopAnimation();
            foreach (var motion in motions) GravityMovement.Release(motion);
            casts.Clear(); motions.Clear(); motionIndex.Clear();
            base.MapRemoved();
        }
    }
}
