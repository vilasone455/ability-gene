using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Vector shove's numbers (AG_VectorShove). The destination fields (destination, range,
    /// requiresLineOfSight) are vanilla's second pick; the rest is the throw.
    /// </summary>
    public class CompProperties_AbilityVectorShove : CompProperties_EffectWithDest
    {
        /// <summary>Cells a pawn is thrown, divided by its body size when <see cref="scaleByBodySize"/> (never multiplied).</summary>
        public float pawnCells = 8f;
        public bool scaleByBodySize = true;
        /// <summary>Speed of every thrown body and thing.</summary>
        public float cellsPerSecond = 20f;
        /// <summary>Blunt per cell the thrown pawn travels; blunt added when a wall ends the throw; its stun.</summary>
        public float damagePerCell = 1.5f;
        public float slamDamage = 8f;
        public int stunTicks = 60;
        /// <summary>A pawn in the path: knocked this far aside, this much blunt, this stun.</summary>
        public float knockCells = 1f;
        public float knockDamage = 8f;
        public int knockStunTicks = 30;
        /// <summary>A loose thing: cells thrown, blunt per kg of its mass, the most it can do.</summary>
        public float thingCells = 12f;
        public float damagePerKg = 1f;
        public float maxThingDamage = 40f;
        /// <summary>A melee hit on Accelerator this recent is returned (ticks).</summary>
        public int forceReturnTicks = 60;
        public float strainCost = 0.05f;

        public CompProperties_AbilityVectorShove()
        {
            compClass = typeof(CompAbilityEffect_VectorShove);
        }
    }

    /// <summary>
    /// Vector shove, reworked (accelerator-vector-shove.js): touch one adjacent pawn or loose thing, then pick
    /// a direction (vanilla's second pick, like the Skip psycast). The throw itself is worked out here and run
    /// by <see cref="MapComponent_Shoves"/>.
    /// </summary>
    public class CompAbilityEffect_VectorShove : CompAbilityEffect_WithDest
    {
        public new CompProperties_AbilityVectorShove Props => (CompProperties_AbilityVectorShove)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!ShoveTarget(target.Thing, parent.pawn))
            {
                if (throwMessages && target.IsValid)
                    Messages.Message("AG_ShoveNeedsTarget".Translate(), target.ToTargetInfo(parent.pawn.Map), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        /// <summary>A pawn (not Accelerator) or a loose stone chunk, corpse or weapon on the ground.</summary>
        public static bool ShoveTarget(Thing thing, Pawn caster)
        {
            if (thing == null || !thing.Spawned || thing == caster) return false;
            if (thing is Pawn) return true;
            return Throwable(thing);
        }

        public static bool Throwable(Thing thing)
        {
            if (thing == null || thing.def.category != ThingCategory.Item) return false;
            if (thing is Corpse || thing.def.IsWeapon) return true;
            var categories = thing.def.thingCategories;
            return categories != null && (categories.Contains(ThingCategoryDefOf.Chunks) || categories.Contains(ThingCategoryDefOf.StoneChunks));
        }

        /// <summary>The destination: any cell but the target's own, on the map.</summary>
        public override bool CanHitTarget(LocalTargetInfo target)
        {
            if (!target.IsValid || parent.pawn?.Map == null || !target.Cell.InBounds(parent.pawn.Map)) return false;
            return selectedTarget.IsValid && target.Cell != selectedTarget.Cell;
        }

        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            if (!selectedTarget.IsValid || !target.IsValid || target.Cell == selectedTarget.Cell || parent.pawn?.Map == null) return null;
            ShovePlan plan = ShovePlan.Make(selectedTarget.Thing, Dir(selectedTarget.Thing, target.Cell), Props, parent.pawn);
            return plan == null ? null : "AG_ShoveLabel".Translate(plan.travel.ToString("0.#"));
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Thing thing = target.Thing;
            Pawn caster = parent.pawn;
            if (thing == null || !thing.Spawned || caster?.Map == null || !dest.IsValid) return;
            ShovePlan plan = ShovePlan.Make(thing, Dir(thing, dest.Cell), Props, caster);
            if (plan == null) return;
            if (thing is Pawn victim)
            {
                // Force returned: the target's last melee hit on him, if it was recent enough.
                HediffComp_ForceReturn record = ForceReturnOf(caster);
                plan.bonus = record?.Returned(victim, Props.forceReturnTicks) ?? 0f;
            }
            caster.Map.GetComponent<MapComponent_Shoves>().Begin(plan);
            VectorStrain.Add(caster, Props.strainCost);
        }

        public static HediffComp_ForceReturn ForceReturnOf(Pawn pawn)
        {
            List<Hediff> hediffs = pawn?.health?.hediffSet?.hediffs;
            if (hediffs == null) return null;
            for (int i = 0; i < hediffs.Count; i++)
                if (hediffs[i] is HediffWithComps with && with.TryGetComp<HediffComp_ForceReturn>() is HediffComp_ForceReturn comp) return comp;
            return null;
        }

        public static Vector2 Dir(Thing thing, IntVec3 toward)
        {
            Vector3 from = thing.Position.ToVector3Shifted(), to = toward.ToVector3Shifted();
            var dir = new Vector2(to.x - from.x, to.z - from.z);
            return dir.sqrMagnitude < 1e-6f ? Vector2.right : dir.normalized;
        }
    }

    /// <summary>
    /// One throw, worked out when it is cast: the cells it crosses, who it bowls over and when, where it
    /// stops and why. Distances are cells from the target's start cell along <see cref="dir"/>; times are
    /// ticks after the throw.
    /// </summary>
    public class ShovePlan : IExposable
    {
        public Pawn caster;
        public Thing thing;
        public Pawn thrown;
        public Vector2 dir;
        public IntVec3 start, land;
        public float travel, speed;
        /// <summary>A wall (or a closed door) ended it: the pawn slams, the thing hits the wall.</summary>
        public bool walled;
        public Thing wall;
        /// <summary>For a thrown thing: the pawn it hits.</summary>
        public Pawn struck;
        public float bonus;
        public List<Liner> liners = new List<Liner>();
        public int startTick, arriveTick;
        public bool arrived;
        /// <summary>The thing in flight (not spawned while it flies).</summary>
        public Thing flying;
        // The numbers of the ability at cast time.
        public float damagePerCell, slamDamage, knockDamage, thingDamage;
        public int stunTicks, knockStunTicks;
        public float knockCells;

        public class Liner : IExposable
        {
            public Pawn pawn;
            public float along;
            /// <summary>The path cell the pawn stood on when the throw was cast.</summary>
            public IntVec3 cell;
            public int tick;
            public bool done;
            /// <summary>Where it stood when struck, and the returned force it took (for the picture).</summary>
            public Vector2 at;
            public float bonus;

            public void ExposeData()
            {
                Scribe_References.Look(ref pawn, "pawn");
                Scribe_Values.Look(ref along, "along");
                Scribe_Values.Look(ref cell, "cell");
                Scribe_Values.Look(ref tick, "tick");
                Scribe_Values.Look(ref done, "done");
                Scribe_Values.Look(ref bonus, "bonus");
            }
        }

        public bool IsThing => thrown == null;
        public int TicksFor(float cells) => Mathf.Max(1, Mathf.CeilToInt(cells / Mathf.Max(0.1f, speed) * 60f));

        public static ShovePlan Make(Thing thing, Vector2 dir, CompProperties_AbilityVectorShove props, Pawn caster)
        {
            if (thing == null || !thing.Spawned || props == null) return null;
            Map map = thing.Map;
            var plan = new ShovePlan
            {
                caster = caster, thing = thing, thrown = thing as Pawn, dir = dir, start = thing.Position, land = thing.Position,
                speed = props.cellsPerSecond, damagePerCell = props.damagePerCell, slamDamage = props.slamDamage,
                knockDamage = props.knockDamage, stunTicks = props.stunTicks, knockStunTicks = props.knockStunTicks, knockCells = props.knockCells,
            };
            float cells;
            if (plan.thrown != null) cells = props.scaleByBodySize ? props.pawnCells / Mathf.Max(1f, plan.thrown.BodySize) : props.pawnCells;
            else
            {
                cells = props.thingCells;
                plan.thingDamage = Mathf.Min(props.maxThingDamage, thing.GetStatValue(StatDefOf.Mass) * thing.stackCount * props.damagePerKg);
            }
            Vector3 origin = thing.Position.ToVector3Shifted();
            IntVec3 last = plan.start;
            for (float d = 0.5f; d <= cells + 0.01f; d += 0.5f)
            {
                IntVec3 c = (origin + new Vector3(dir.x, 0f, dir.y) * d).ToIntVec3();
                if (c == last) continue;
                if (!c.InBounds(map)) break;
                Building edifice = c.GetEdifice(map);
                bool shut = edifice is Building_Door door && !door.Open;
                if (c.Filled(map) || c.Impassable(map) || shut || !c.Standable(map))
                {
                    plan.walled = true;
                    plan.wall = edifice;
                    break;
                }
                last = c;
                plan.land = c;
                List<Thing> here = c.GetThingList(map);
                for (int i = 0; i < here.Count; i++)
                {
                    if (!(here[i] is Pawn other) || other == thing || other == caster || other.Dead) continue;
                    float along = (c.ToVector3Shifted() - origin).x * dir.x + (c.ToVector3Shifted() - origin).z * dir.y;
                    if (plan.thrown == null)
                    {
                        // A thing hits the first standing pawn and drops there.
                        if (other.Downed) continue;
                        plan.struck = other;
                        break;
                    }
                    plan.liners.Add(new Liner { pawn = other, along = along, cell = c });
                }
                if (plan.struck != null) break;
            }
            plan.travel = (plan.land - plan.start).LengthHorizontal;
            return plan;
        }

        /// <summary>A free standable cell <paramref name="cells"/> aside of the line, on the pawn's own side first.</summary>
        public static IntVec3 KnockCell(Pawn pawn, IntVec3 at, Vector2 dir, float cells, Map map, IntVec3 start)
        {
            var side = new Vector2(-dir.y, dir.x);
            Vector3 rel = pawn.DrawPos - start.ToVector3Shifted();
            float cross = rel.x * side.x + rel.z * side.y;
            float first = Mathf.Abs(cross) < 0.1f ? (pawn.thingIDNumber % 2 == 0 ? 1f : -1f) : Mathf.Sign(cross);
            foreach (float sign in new[] { first, -first })
            {
                Vector3 to = at.ToVector3Shifted() + new Vector3(side.x, 0f, side.y) * (cells * sign);
                IntVec3 c = to.ToIntVec3();
                if (c != at && c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map) == null) return c;
            }
            return at;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref caster, "caster");
            Scribe_References.Look(ref thrown, "thrown");
            Scribe_Deep.Look(ref flying, "flying");
            Scribe_Values.Look(ref dir, "dir");
            Scribe_Values.Look(ref start, "start");
            Scribe_Values.Look(ref land, "land");
            Scribe_Values.Look(ref travel, "travel");
            Scribe_Values.Look(ref speed, "speed");
            Scribe_Values.Look(ref walled, "walled");
            Scribe_References.Look(ref wall, "wall");
            Scribe_References.Look(ref struck, "struck");
            Scribe_Values.Look(ref bonus, "bonus");
            Scribe_Collections.Look(ref liners, "liners", LookMode.Deep);
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref arriveTick, "arriveTick");
            Scribe_Values.Look(ref arrived, "arrived");
            Scribe_Values.Look(ref damagePerCell, "damagePerCell");
            Scribe_Values.Look(ref slamDamage, "slamDamage");
            Scribe_Values.Look(ref knockDamage, "knockDamage");
            Scribe_Values.Look(ref thingDamage, "thingDamage");
            Scribe_Values.Look(ref stunTicks, "stunTicks");
            Scribe_Values.Look(ref knockStunTicks, "knockStunTicks");
            Scribe_Values.Look(ref knockCells, "knockCells");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && liners == null) liners = new List<Liner>();
        }
    }

    /// <summary>
    /// A pawn thrown by vector shove or knocked aside by one: a straight flight at the shove's speed with no
    /// height. The def's flightSpeed is only a default; <see cref="cellsPerSecond"/> sets the flight time.
    /// </summary>
    public class PawnFlyer_VectorThrown : PawnFlyer
    {
        public float cellsPerSecond = 20f, distance = 1f;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad) ticksFlightTime = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(0.1f, cellsPerSecond) * 60f));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref cellsPerSecond, "cellsPerSecond", 20f);
            Scribe_Values.Look(ref distance, "distance", 1f);
        }

        public static void Throw(Pawn pawn, IntVec3 to, float cellsPerSecond)
        {
            if (pawn == null || !pawn.Spawned || to == pawn.Position) return;
            Map map = pawn.Map;
            float distance = (to - pawn.Position).LengthHorizontal;
            var flyer = (PawnFlyer_VectorThrown)MakeFlyer(AcceleratorDefOf.AG_VectorThrown, pawn, to, null, null);
            if (flyer == null) return;
            flyer.cellsPerSecond = cellsPerSecond;
            flyer.distance = distance;
            GenSpawn.Spawn(flyer, to, map);
        }
    }

    /// <summary>
    /// Runs vector shoves on a map: the thrown pawn in its flyer, the pawns it bowls over as it passes, the slam
    /// or landing, and a thrown thing's flight and hit. Also keeps the melee hits on Accelerator that the
    /// picture marks (the force-returned window). The clock is game ticks.
    /// </summary>
    public class MapComponent_Shoves : MapComponent
    {
        private List<ShovePlan> throws = new List<ShovePlan>();

        /// <summary>A melee hit on Accelerator, for the window ring and the hit flash (not saved).</summary>
        public struct Hit
        {
            public Pawn accelerator, attacker;
            public int tick;
        }
        public readonly List<Hit> hits = new List<Hit>();

        public IReadOnlyList<ShovePlan> Throws => throws;

        public MapComponent_Shoves(Map map) : base(map) { }

        public void MeleeHit(Pawn accelerator, Pawn attacker)
        {
            hits.RemoveAll(h => h.accelerator == accelerator);
            hits.Add(new Hit { accelerator = accelerator, attacker = attacker, tick = Find.TickManager.TicksGame });
        }

        public void Begin(ShovePlan plan)
        {
            int now = Find.TickManager.TicksGame;
            plan.startTick = now;
            plan.arriveTick = now + plan.TicksFor(plan.travel);
            foreach (ShovePlan.Liner liner in plan.liners) liner.tick = now + plan.TicksFor(liner.along);
            if (plan.thrown != null)
            {
                if (plan.land != plan.start) PawnFlyer_VectorThrown.Throw(plan.thrown, plan.land, plan.speed);
            }
            else
            {
                plan.flying = plan.thing;
                if (plan.flying.Spawned) plan.flying.DeSpawn();
            }
            // The one who hit him has been answered.
            hits.RemoveAll(h => h.accelerator == plan.caster);
            throws.Add(plan);
        }

        public override void MapComponentTick()
        {
            if (throws.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            for (int i = throws.Count - 1; i >= 0; i--)
            {
                ShovePlan plan = throws[i];
                if (plan.thrown != null) TickPawnThrow(plan, now);
                else TickThingThrow(plan, now);
                // Kept for the picture's tail after the arrival.
                if (plan.arrived && now - plan.arriveTick > 150) throws.RemoveAt(i);
            }
        }

        private void TickPawnThrow(ShovePlan plan, int now)
        {
            foreach (ShovePlan.Liner liner in plan.liners)
            {
                if (liner.done) continue;
                if (now < liner.tick) break;
                liner.done = true;
                Pawn other = liner.pawn;
                // Gone, or walked off the line since the cast: the body passes.
                if (other == null || other.Dead || !other.Spawned || other.Map != map || other.Position.DistanceTo(liner.cell) > 1.5f) continue;
                liner.at = new Vector2(other.DrawPos.x, other.DrawPos.z);
                IntVec3 to = ShovePlan.KnockCell(other, other.Position, plan.dir, plan.knockCells, map, plan.start);
                // Force returned goes to the first pawn struck; the thrown pawn keeps it only if it strikes none.
                float damage = plan.knockDamage + plan.bonus;
                liner.bonus = plan.bonus;
                plan.bonus = 0f;
                Hurt(other, damage, plan);
                if (other.Dead || !other.Spawned) continue;
                other.stances?.stunner?.StunFor(plan.knockStunTicks, plan.caster, false, true);
                if (to != other.Position) PawnFlyer_VectorThrown.Throw(other, to, plan.knockCells / 0.2f);
            }
            if (plan.arrived || now < plan.arriveTick) return;
            Pawn thrown = plan.thrown;
            // The flyer lands on its own tick; wait for it.
            if (!thrown.Dead && !thrown.Spawned && now < plan.arriveTick + 10) return;
            plan.arrived = true;
            if (thrown.Dead || !thrown.Spawned || thrown.Map != map) return;
            float hit = plan.travel * plan.damagePerCell + (plan.walled ? plan.slamDamage : 0f) + plan.bonus;
            Hurt(thrown, hit, plan);
            if (!thrown.Dead) thrown.stances?.stunner?.StunFor(plan.stunTicks, plan.caster, false, true);
            if (plan.walled && Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(0.1f);
        }

        private void TickThingThrow(ShovePlan plan, int now)
        {
            if (plan.arrived || now < plan.arriveTick) return;
            plan.arrived = true;
            Thing thing = plan.flying;
            plan.flying = null;
            if (plan.struck != null && !plan.struck.Dead && plan.struck.Spawned && plan.struck.Map == map)
                Hurt(plan.struck, plan.thingDamage, plan);
            else if (plan.walled && plan.wall != null && !plan.wall.Destroyed && plan.wall.def.useHitPoints)
                plan.wall.TakeDamage(new DamageInfo(DamageDefOf.Blunt, plan.thingDamage, 0f, Angle(plan), plan.caster));
            if (thing != null && !thing.Destroyed)
                GenPlace.TryPlaceThing(thing, plan.land, map, ThingPlaceMode.Near);
        }

        private static float Angle(ShovePlan plan) => new Vector3(plan.dir.x, 0f, plan.dir.y).AngleFlat();

        private static void Hurt(Pawn pawn, float damage, ShovePlan plan)
        {
            if (damage < 0.5f) return;
            pawn.TakeDamage(new DamageInfo(DamageDefOf.Blunt, damage, 0f, Angle(plan), plan.caster));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref throws, "throws", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && throws == null) throws = new List<ShovePlan>();
        }
    }
}
