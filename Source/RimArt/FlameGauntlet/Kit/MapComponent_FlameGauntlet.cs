using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Devour = RimArt.FlameDevourTiming;
using Release = RimArt.FlameReleaseTiming;

namespace RimArt
{
    public enum FlameCastKind { Devour, Release }

    /// <summary>
    /// Everything the Flame Gauntlet does on a map:
    /// - plays Devour's and Release's pictures for real casts and applies each effect when the
    ///   picture shows it: a fire goes out when it lifts off and its Heat arrives when it reaches the
    ///   palm; a cone cell catches fire when the ground wave reaches it;
    /// - once a second, keeps each holder's Overheating state and its arm burns (FlameGauntletHeat);
    /// - draws the heat on idle holders and the meter's ticks under a selected holder.
    ///
    /// A cast is known from the moment its warmup starts (<see cref="Begin"/>, from
    /// JobDriver_CastFlameGauntlet). The clock is game ticks. Nothing about a cast is saved: a game
    /// loaded mid-cast loses the rest of its picture and any effect not yet applied. Heat is saved on
    /// the weapon (CompFlameGauntlet).
    /// </summary>
    public class MapComponent_FlameGauntlet : MapComponent
    {
        private sealed class Cast
        {
            public Pawn caster;
            public CompFlameGauntlet gauntlet;
            public FlameCastKind kind;
            public int startTick, warmupTicks;
            public bool landed, home;
            public IntVec3 target;

            public FlameDevourShot devour;
            /// <summary>The fire behind each of devour.Meals, same order; null once it is gone.</summary>
            public List<Fire> fires;
            public bool[] departed, arrived;
            public bool pulled, refused;

            public FlameReleaseShot release;
            public List<IntVec3> cells;
            public bool[] lit;
            public bool released;
            public CompProperties_FlameRelease releaseProps;

            public float Seconds(int now) => FlameGauntletTiming.Lead + (now - startTick) / 60f;
            public float Result => kind == FlameCastKind.Devour ? devour.Result : Release.Result(release);
            public float End => kind == FlameCastKind.Devour ? Devour.End(devour) : Release.End(release);
        }

        /// <summary>A cast whose warmup ran out this long ago without being applied was interrupted.</summary>
        private const float Overdue = 0.25f;

        private readonly List<Cast> casts = new List<Cast>();
        private readonly List<Pawn> holders = new List<Pawn>();

        public MapComponent_FlameGauntlet(Map map) : base(map) { }

        public void Register(Pawn pawn)
        {
            if (pawn != null && !holders.Contains(pawn)) holders.Add(pawn);
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            Rescan();
        }

        private void Rescan()
        {
            holders.Clear();
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
                if (CompFlameGauntlet.HeldBy(pawns[i]) != null) holders.Add(pawns[i]);
        }

        private static float Degrees(Vector2 run) => run.sqrMagnitude < 0.0001f ? 0f : Mathf.Atan2(run.y, run.x) * Mathf.Rad2Deg;

        private static Vector2 Feet(Pawn pawn) => new Vector2(pawn.DrawPos.x, pawn.DrawPos.z);

        private static Vector2 CellGround(IntVec3 cell) => new Vector2(cell.x + 0.5f, cell.z + 0.5f);

        /// <summary>A pawn's facing as the pictures' degrees: 0 east, 90 north.</summary>
        private static float Facing(Pawn pawn) => 90f - pawn.Rotation.AsAngle;

        // ------------------------------------------------------------------ casts

        /// <summary>A cast's warmup has begun: the picture starts the wind-up.</summary>
        public void Begin(Pawn caster, Ability ability, LocalTargetInfo target)
        {
            Ended(caster);
            CompFlameGauntlet gauntlet = CompFlameGauntlet.HeldBy(caster);
            if (caster == null || ability == null || gauntlet == null || !target.Cell.IsValid) return;
            var cast = new Cast
            {
                caster = caster, gauntlet = gauntlet, target = target.Cell, startTick = Find.TickManager.TicksGame,
                warmupTicks = Mathf.RoundToInt(ability.def.verbProperties.warmupTime * 60f),
            };
            CompProperties_FlameRelease release = ability.CompOfType<CompAbilityEffect_FlameRelease>()?.Props;
            if (release != null)
            {
                cast.kind = FlameCastKind.Release;
                cast.releaseProps = release;
                PlanRelease(cast);
            }
            else
            {
                cast.kind = FlameCastKind.Devour;
                PlanDevour(cast, ability.CompOfType<CompAbilityEffect_FlameDevour>()?.Props?.radius ?? Devour.ScriptRadius);
            }
            casts.Add(cast);
        }

        /// <summary>The cast that has begun and not been applied, or a new one started a warmup ago.</summary>
        private Cast Applying(Pawn caster, FlameCastKind kind, IntVec3 target, float warmup)
        {
            Cast cast = casts.Find(c => c.caster == caster && c.kind == kind && !c.landed);
            if (cast == null)
            {
                CompFlameGauntlet gauntlet = CompFlameGauntlet.HeldBy(caster);
                if (gauntlet == null) return null;
                cast = new Cast
                {
                    caster = caster, gauntlet = gauntlet, kind = kind, target = target,
                    startTick = Find.TickManager.TicksGame - Mathf.RoundToInt(warmup * 60f), warmupTicks = Mathf.RoundToInt(warmup * 60f),
                };
                casts.Add(cast);
            }
            cast.target = target;
            cast.landed = true;
            return cast;
        }

        public void LandDevour(Pawn caster, IntVec3 target, CompProperties_FlameDevour props)
        {
            Cast cast = Applying(caster, FlameCastKind.Devour, target, Devour.Windup);
            if (cast == null) return;
            // The fires as they are now: some may have burned out or spread during the warmup.
            PlanDevour(cast, props.radius);
        }

        public void LandRelease(Pawn caster, IntVec3 target, CompProperties_FlameRelease props)
        {
            Cast cast = Applying(caster, FlameCastKind.Release, target, Release.Windup);
            if (cast == null) return;
            cast.releaseProps = props;
            PlanRelease(cast);
            // The Heat leaves the gauntlet at the release; the picture's meter counts it down cell by cell.
            CompProperties_FlameGauntlet g = cast.gauntlet.Props;
            cast.gauntlet.SetHeat(cast.release.StartHeat - cast.release.Lit * g.heatPerConeCell);
        }

        private void PlanDevour(Cast cast, float radius)
        {
            Pawn caster = cast.caster;
            CompProperties_FlameGauntlet g = cast.gauntlet.Props;
            Vector2 feet = Feet(caster), to = CellGround(cast.target);
            var shot = new FlameDevourShot
            {
                Caster = feet, Target = to, Aim = Degrees(to - feet), Radius = radius,
                StartHeat = cast.gauntlet.Heat, MaxHeat = g.maxHeat, OverheatAt = g.overheatAt,
            };
            List<FlameGauntletFires.Found> found = FlameGauntletFires.InRadius(map, cast.target, radius);
            var fires = new List<Fire>(found.Count);
            for (int i = 0; i < found.Count; i++)
            {
                FlameGauntletFires.Found f = found[i];
                shot.Meals.Add(new FlameMeal
                {
                    At = f.pawn != null ? Feet(f.pawn) : CellGround(f.fire.Position), Pawn = f.pawn != null,
                    Value = f.pawn != null ? g.heatPerPawn : g.heatPerCell,
                });
                fires.Add(f.fire);
            }
            // Plan sorts the meals; put the fires back in the same order by their place.
            var unsorted = new List<FlameMeal>(shot.Meals);
            Devour.Plan(shot);
            cast.fires = new List<Fire>(shot.Meals.Count);
            var used = new bool[unsorted.Count];
            for (int i = 0; i < shot.Meals.Count; i++)
            {
                Fire match = null;
                for (int j = 0; j < unsorted.Count; j++)
                {
                    if (used[j] || unsorted[j].Pawn != shot.Meals[i].Pawn || unsorted[j].At != shot.Meals[i].At) continue;
                    used[j] = true;
                    match = fires[j];
                    break;
                }
                cast.fires.Add(match);
            }
            cast.devour = shot;
            cast.departed = new bool[shot.Meals.Count];
            cast.arrived = new bool[shot.Meals.Count];
        }

        private void PlanRelease(Cast cast)
        {
            Pawn caster = cast.caster;
            CompProperties_FlameGauntlet g = cast.gauntlet.Props;
            CompProperties_FlameRelease props = cast.releaseProps;
            Vector2 feet = Feet(caster), to = CellGround(cast.target);
            List<FlameGauntletCone.Cell> cone = FlameGauntletCone.Cells(caster, cast.target, props.length, props.width);
            float heat = cast.gauntlet.Heat;
            int lit = heat + 0.0001f < g.minReleaseHeat ? 0 : Mathf.Min(cone.Count, Mathf.FloorToInt((heat + 0.0001f) / Mathf.Max(1, g.heatPerConeCell)));
            var shot = new FlameReleaseShot
            {
                Caster = feet, Aim = Degrees(to - feet), StartHeat = heat, MaxHeat = g.maxHeat, OverheatAt = g.overheatAt,
                Length = props.length, Width = props.width, Lit = lit,
            };
            cast.cells = new List<IntVec3>(cone.Count);
            for (int i = 0; i < cone.Count; i++)
            {
                shot.Cells.Add(new FlameConeCell { At = CellGround(cone[i].cell), Row = cone[i].row, Across = cone[i].across });
                cast.cells.Add(cone[i].cell);
            }
            cast.release = shot;
            cast.lit = new bool[cone.Count];
        }

        /// <summary>The caster's cast job is over. A cast never applied has nothing left to show.</summary>
        public void Ended(Pawn caster)
        {
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                if (casts[i].caster != caster) continue;
                if (!casts[i].landed) casts.RemoveAt(i);
                else casts[i].home = true;
            }
        }

        /// <summary>Whether the caster's cast has been applied and its job has not ended.</summary>
        public bool Applied(Pawn caster)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == caster && casts[i].landed && !casts[i].home) return true;
            return false;
        }

        /// <summary>Whether the caster's cast job should still hold it in place: until the result.</summary>
        public bool Holds(Pawn caster)
        {
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < casts.Count; i++)
            {
                Cast cast = casts[i];
                if (cast.caster == caster && cast.landed && !cast.home && cast.Seconds(now) < cast.Result) return true;
            }
            return false;
        }

        private Cast Casting(Pawn caster)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == caster && !casts[i].home) return casts[i];
            return null;
        }

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                Cast cast = casts[i];
                float s = cast.Seconds(now);
                if (!cast.landed)
                {
                    if (s > FlameGauntletTiming.Lead + cast.warmupTicks / 60f + Overdue || !cast.caster.Spawned || cast.caster.Map != map) casts.RemoveAt(i);
                    continue;
                }
                if (cast.kind == FlameCastKind.Devour) TickDevour(cast, s);
                else TickRelease(cast, s);
                if (s >= cast.End) casts.RemoveAt(i);
            }
            if (now % 60 == 0)
            {
                Rescan();
                for (int i = 0; i < holders.Count; i++) FlameGauntletHeat.Tick(holders[i], now);
            }
        }

        /// <summary>Devour's effects at the picture's times: each fire goes out as it lifts off, its Heat arrives at the palm.</summary>
        private void TickDevour(Cast cast, float s)
        {
            FlameDevourShot shot = cast.devour;
            if (!cast.pulled && s >= Devour.Pull)
            {
                cast.pulled = true;
                FlameGauntletSound.Play(FlameGauntletSound.Pull, map, shot.Caster);
            }
            for (int i = 0; i < shot.Meals.Count; i++)
            {
                FlameMeal meal = shot.Meals[i];
                if (!meal.Eaten) continue;
                if (!cast.departed[i] && s >= meal.Depart)
                {
                    cast.departed[i] = true;
                    Fire fire = cast.fires[i];
                    if (fire == null || fire.Destroyed) cast.fires[i] = null;
                    else
                    {
                        IntVec3 cell = fire.Position;
                        bool onPawn = fire.parent is Pawn;
                        fire.Destroy();
                        if (!onPawn) FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_Ash);
                    }
                }
                if (!cast.arrived[i] && s >= meal.Arrive)
                {
                    cast.arrived[i] = true;
                    // A fire that had burned out before it was taken brings nothing.
                    if (cast.fires[i] != null) cast.gauntlet.Add(meal.Value);
                }
            }
            if (!cast.refused && shot.RefusedAt >= 0f && s >= shot.RefusedAt)
            {
                cast.refused = true;
                FlameGauntletSound.Play(FlameGauntletSound.Refused, map, shot.Caster);
                if (cast.caster.Faction == Faction.OfPlayer)
                    Messages.Message("Flame gauntlet: too hot, the rest of the fire is left burning.", cast.caster, MessageTypeDefOf.RejectInput, false);
            }
        }

        /// <summary>Release's effects at the picture's times: the shake at the release, a fire in each cell as the wave reaches it.</summary>
        private void TickRelease(Cast cast, float s)
        {
            FlameReleaseShot shot = cast.release;
            if (!cast.released && s >= Release.Go)
            {
                cast.released = true;
                if (shot.Lit > 0)
                {
                    FlameGauntletSound.Play(FlameGauntletSound.Release, map, shot.Caster);
                    if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(Release.Shake);
                }
                else FlameGauntletSound.Play(FlameGauntletSound.Refused, map, shot.Caster);
            }
            for (int i = 0; i < shot.Lit; i++)
            {
                if (cast.lit[i] || s < Release.EruptAt(shot, i)) continue;
                cast.lit[i] = true;
                Ignite(cast.cells[i], cast.caster, cast.releaseProps);
            }
        }

        /// <summary>
        /// Starts a fire in the cell whatever it stands on, unless one is there, with a chemfuel
        /// puddle under it (fuelPuddle) so it keeps burning on bare ground, and sets pawns standing in
        /// it alight.
        /// </summary>
        private void Ignite(IntVec3 cell, Pawn caster, CompProperties_FlameRelease props)
        {
            if (!cell.InBounds(map) || cell.Impassable(map)) return;
            if (props.fuelPuddle) FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_Fuel);
            if (!cell.ContainsStaticFire(map))
            {
                var fire = (Fire)ThingMaker.MakeThing(ThingDefOf.Fire);
                fire.fireSize = props.fireSize;
                fire.instigator = caster;
                GenSpawn.Spawn(fire, cell, map, Rot4.North);
            }
            List<Thing> things = cell.GetThingList(map);
            for (int i = things.Count - 1; i >= 0; i--)
                if (things[i] is Pawn pawn && pawn != caster) pawn.TryAttachFire(props.pawnFireSize, caster);
        }

        // ------------------------------------------------------------------ drawing

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map) return;
            int now = Find.TickManager.TicksGame;
            float clock = Time.realtimeSinceStartup;
            for (int i = 0; i < casts.Count; i++)
            {
                Cast cast = casts[i];
                float s = cast.Seconds(now);
                // Before the ability is applied the picture holds at the end of the wind-up.
                if (!cast.landed) s = Mathf.Min(s, (cast.kind == FlameCastKind.Devour ? Devour.Pull : Release.Go) - 0.001f);
                if (cast.caster.Spawned && cast.caster.Map == map)
                {
                    if (cast.kind == FlameCastKind.Devour) cast.devour.CasterAt = Feet(cast.caster);
                    else cast.release.CasterAt = Feet(cast.caster);
                }
                if (cast.kind == FlameCastKind.Devour) FlameGauntletDevourGraphics.Draw(cast.devour, s, map, !cast.home);
                else FlameGauntletReleaseGraphics.Draw(cast.release, s, map, !cast.home);
            }
            for (int i = 0; i < holders.Count; i++)
            {
                Pawn pawn = holders[i];
                CompFlameGauntlet gauntlet = CompFlameGauntlet.HeldBy(pawn);
                if (gauntlet == null || !pawn.Spawned || pawn.Map != map || pawn.Position.Fogged(map)) continue;
                CompProperties_FlameGauntlet g = gauntlet.Props;
                Cast cast = Casting(pawn);
                Vector2 feet = Feet(pawn);
                float aim = Facing(pawn), heat = gauntlet.Heat;
                if (cast != null)
                {
                    float s = cast.Seconds(now);
                    aim = cast.kind == FlameCastKind.Devour ? cast.devour.Aim : cast.release.Aim;
                    heat = cast.kind == FlameCastKind.Devour ? Devour.HeatAt(cast.devour, s) : Release.HeatAt(cast.release, s);
                }
                else
                {
                    float r = aim * Mathf.Deg2Rad;
                    Vector2 hand = feet + new Vector2(0.12f * Mathf.Cos(r) - 0.18f * Mathf.Sin(r), 0.12f * Mathf.Sin(r) + 0.18f * Mathf.Cos(r));
                    FlameGauntletGraphics.DrawHeld(hand, aim, heat, g.maxHeat, g.overheatAt, clock, map);
                }
                if (Find.Selector.IsSelected(pawn) || cast != null) FlameGauntletGraphics.Tally(feet, heat, g.maxHeat, g.overheatAt, aim, 1f);
            }
        }

        public override void MapRemoved()
        {
            casts.Clear();
            holders.Clear();
        }
    }

    /// <summary>
    /// Overheating: at the overheat line or over, the wearer has AG_FlameOverheating (slower and
    /// clumsier, XML stages) and a burn lands on the gauntlet arm every overheatBurnSeconds. The
    /// burn is added as an injury, not dealt as damage, because the gauntlet's own immunity blocks
    /// fire damage. Ends as soon as the Heat drops under the line or the gauntlet is put down.
    /// </summary>
    public static class FlameGauntletHeat
    {
        private static readonly Dictionary<Pawn, int> NextBurn = new Dictionary<Pawn, int>();

        public static void Tick(Pawn pawn, int now)
        {
            CompFlameGauntlet gauntlet = CompFlameGauntlet.HeldBy(pawn);
            if (gauntlet == null || pawn.Dead)
            {
                EndOverheating(pawn);
                return;
            }
            if (!gauntlet.Overheating)
            {
                EndOverheating(pawn);
                return;
            }
            CompProperties_FlameGauntlet g = gauntlet.Props;
            if (pawn.health.hediffSet.GetFirstHediffOfDef(FlameGauntletDefOf.AG_FlameOverheating) == null)
                pawn.health.AddHediff(FlameGauntletDefOf.AG_FlameOverheating);
            int every = Mathf.Max(1, Mathf.RoundToInt(g.overheatBurnSeconds * 60f));
            if (!NextBurn.TryGetValue(pawn, out int next))
            {
                NextBurn[pawn] = now + every;
                return;
            }
            if (now < next) return;
            NextBurn[pawn] = now + every;
            BurnArm(pawn, g.overheatBurnSeverity);
        }

        public static void EndOverheating(Pawn pawn)
        {
            if (pawn == null) return;
            NextBurn.Remove(pawn);
            Hediff hot = pawn.health?.hediffSet?.GetFirstHediffOfDef(FlameGauntletDefOf.AG_FlameOverheating);
            if (hot != null) pawn.health.RemoveHediff(hot);
        }

        /// <summary>The arm the gauntlet is on: the first arm (or hand, or shoulder) the pawn still has.</summary>
        public static BodyPartRecord GauntletArm(Pawn pawn)
        {
            BodyPartRecord best = null;
            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (part.def == BodyPartDefOf.Arm) return part;
                if (best == null && (part.def == BodyPartDefOf.Hand || part.def == BodyPartDefOf.Shoulder)) best = part;
            }
            return best;
        }

        public static void BurnArm(Pawn pawn, float severity)
        {
            BodyPartRecord arm = GauntletArm(pawn);
            var burn = (Hediff_Injury)HediffMaker.MakeHediff(DamageDefOf.Burn.hediff, pawn, arm);
            burn.Severity = severity;
            pawn.health.AddHediff(burn, arm);
        }
    }

    /// <summary>The kit's sounds: vanilla placeholders until it has its own.</summary>
    internal static class FlameGauntletSound
    {
        internal static SoundDef Pull => SoundDef.Named("FireSpew_Warmup");
        internal static SoundDef Release => SoundDef.Named("FireSpew_Resolve");
        internal static SoundDef Refused => SoundDef.Named("Interact_BeatFire");

        public static void Play(SoundDef sound, Map map, Vector2 ground) => PowerPoleSound.Play(sound, map, ground);
    }
}
