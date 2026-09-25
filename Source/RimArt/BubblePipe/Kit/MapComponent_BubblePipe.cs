using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using Burst = RimArt.BubblePipeDriftingBurstTiming;
using Eye = RimArt.BubblePipeEyePopTiming;

namespace RimArt
{
    /// <summary>
    /// Everything the Bubble Pipe has out on a map: the pipe's picture on a caster, the drifting
    /// bubbles of Drifting Burst, Eye Pop's bubble in flight, the soap film on soaped pawns, and the jar
    /// on every holder's hip. It does what each picture shows at the moment it shows it: a bubble pops
    /// when a pawn touches it, Eye Pop's soap lands when the bubble reaches the face. It also refills
    /// the jars of holders standing at water.
    ///
    /// Bubbles, flights and films are saved; the pipe's picture on a caster is not (it lasts under
    /// 2 s, and a game loaded mid-cast only loses the rest of that picture). The clock is game ticks,
    /// so pictures pause and speed up with the game.
    ///
    /// Touch checks run every <see cref="TouchInterval"/> ticks and look only at the cells round each
    /// bubble, not at every pawn on the map.
    /// </summary>
    public class MapComponent_BubblePipe : MapComponent
    {
        private sealed class Cast
        {
            public Pawn caster;
            public bool eyePop, landed;
            /// <summary>The cast job this cast landed in has ended.</summary>
            public bool jobOver;
            public int startTick, warmupTicks, cost, count;
            public float blowsBefore, cap;
            public Vector2 feet, toward;

            public float Seconds(int now) => (now - startTick) / 60f;
            public float Duration => eyePop ? Eye.CastDuration : Burst.CastDuration(count);
        }

        private sealed class Cloud : IExposable
        {
            public Pawn caster;
            public Vector2 feet, toward;
            public int blowTick, count, staggerTicks;
            public float spread, speed, life, touch, blast, damage, armorPenetration, staggerMoveSpeedFactor;
            public SoundDef soundPop;
            // Per bubble: when it popped on the cloud's clock (-1 not yet), where it was, and whether the pop was harmless (a wall).
            public List<float> popAt, popAlong, popAcross, popHeight;
            public List<bool> harmless;
            public DriftingPop[] pops;
            public bool[] harmlessNow;

            public float Seconds(int now) => Burst.BlowAt + (now - blowTick) / 60f;
            public float EndsAt => Burst.ExpireAt(life) + 0.7f;

            public void Prepare()
            {
                if (popAt == null || popAt.Count != count)
                {
                    popAt = new List<float>(); popAlong = new List<float>(); popAcross = new List<float>(); popHeight = new List<float>(); harmless = new List<bool>();
                    for (int i = 0; i < count; i++) { popAt.Add(-1f); popAlong.Add(0f); popAcross.Add(0f); popHeight.Add(0f); harmless.Add(false); }
                }
                pops = new DriftingPop[count];
                harmlessNow = new bool[count];
                for (int i = 0; i < count; i++)
                {
                    pops[i] = new DriftingPop { Popped = popAt[i] >= 0f, At = popAt[i], Along = popAlong[i], Across = popAcross[i], Height = popHeight[i] };
                    harmlessNow[i] = harmless[i];
                }
            }

            public void Popped(int i, float at, DriftingBubble b, bool isHarmless)
            {
                popAt[i] = at; popAlong[i] = b.Along; popAcross[i] = b.Across; popHeight[i] = b.Height; harmless[i] = isHarmless;
                pops[i] = new DriftingPop { Popped = true, At = at, Along = b.Along, Across = b.Across, Height = b.Height };
                harmlessNow[i] = isHarmless;
            }

            public void ExposeData()
            {
                Scribe_References.Look(ref caster, "caster");
                Scribe_Values.Look(ref feet, "feet");
                Scribe_Values.Look(ref toward, "toward");
                Scribe_Values.Look(ref blowTick, "blowTick");
                Scribe_Values.Look(ref count, "count");
                Scribe_Values.Look(ref staggerTicks, "staggerTicks");
                Scribe_Values.Look(ref spread, "spread");
                Scribe_Values.Look(ref speed, "speed");
                Scribe_Values.Look(ref life, "life");
                Scribe_Values.Look(ref touch, "touch");
                Scribe_Values.Look(ref blast, "blast");
                Scribe_Values.Look(ref damage, "damage");
                Scribe_Values.Look(ref armorPenetration, "armorPenetration");
                Scribe_Values.Look(ref staggerMoveSpeedFactor, "staggerMoveSpeedFactor", StaggerHandler.DefaultStaggerMoveSpeedFactor);
                Scribe_Defs.Look(ref soundPop, "soundPop");
                Scribe_Collections.Look(ref popAt, "popAt", LookMode.Value);
                Scribe_Collections.Look(ref popAlong, "popAlong", LookMode.Value);
                Scribe_Collections.Look(ref popAcross, "popAcross", LookMode.Value);
                Scribe_Collections.Look(ref popHeight, "popHeight", LookMode.Value);
                Scribe_Collections.Look(ref harmless, "harmless", LookMode.Value);
                if (Scribe.mode == LoadSaveMode.PostLoadInit) Prepare();
            }
        }

        private sealed class Shot : IExposable
        {
            public Pawn caster, victim;
            public Vector2 feet, toward, target;
            public int launchTick, debuffTicks;
            public float distance, speed;
            public HediffDef hediff;
            public SoundDef soundHit;
            public bool hit;

            public float Seconds(int now) => Eye.LaunchAt + (now - launchTick) / 60f;
            public float HitAt => Eye.HitAt(distance, speed);

            public void ExposeData()
            {
                Scribe_References.Look(ref caster, "caster");
                Scribe_References.Look(ref victim, "victim");
                Scribe_Values.Look(ref feet, "feet");
                Scribe_Values.Look(ref toward, "toward");
                Scribe_Values.Look(ref target, "target");
                Scribe_Values.Look(ref launchTick, "launchTick");
                Scribe_Values.Look(ref debuffTicks, "debuffTicks");
                Scribe_Values.Look(ref distance, "distance");
                Scribe_Values.Look(ref speed, "speed", 5f);
                Scribe_Defs.Look(ref hediff, "hediff");
                Scribe_Defs.Look(ref soundHit, "soundHit");
                Scribe_Values.Look(ref hit, "hit");
            }
        }

        private sealed class Soaped : IExposable
        {
            public Pawn victim;
            public HediffDef hediff;
            public int hitTick, clearTick;

            public void ExposeData()
            {
                Scribe_References.Look(ref victim, "victim");
                Scribe_Defs.Look(ref hediff, "hediff");
                Scribe_Values.Look(ref hitTick, "hitTick");
                Scribe_Values.Look(ref clearTick, "clearTick");
            }
        }

        /// <summary>How often bubbles look for a pawn touching them. A walking pawn covers about 0.3 cells in this time, well under a bubble's width.</summary>
        private const int TouchInterval = 4;
        /// <summary>How often the holders are listed again and their jars refilled.</summary>
        private const int HolderInterval = 60;
        /// <summary>A cast whose warmup ran out this long ago without blowing was interrupted.</summary>
        private const float Overdue = 0.25f;

        private static readonly List<Pawn> hit = new List<Pawn>();

        private readonly List<Cast> casts = new List<Cast>();
        private readonly List<Pawn> holders = new List<Pawn>();
        private List<Cloud> clouds = new List<Cloud>();
        private List<Shot> shots = new List<Shot>();
        private List<Soaped> soaped = new List<Soaped>();

        public MapComponent_BubblePipe(Map map) : base(map) { }

        private static Vector2 Ground(Thing thing)
        {
            Vector3 stands = thing.DrawPos;
            return new Vector2(stands.x, stands.z);
        }

        private static IntVec3 CellOf(Vector2 ground) => new Vector3(ground.x, 0f, ground.y).ToIntVec3();

        private static Vector2 Toward(Vector2 from, Vector2 to)
        {
            Vector2 run = to - from;
            return run.sqrMagnitude < 0.0001f ? Vector2.right : run.normalized;
        }

        // ------------------------------------------------------------ casts

        /// <summary>A cast's warmup has begun: the pipe goes up. What it does is decided when the warmup ends (<see cref="Blow"/>, <see cref="Shoot"/>).</summary>
        public void Begin(Pawn caster, Ability ability, LocalTargetInfo target)
        {
            Ended(caster);
            CompBubblePipe pipe = CompBubblePipe.HeldBy(caster);
            if (caster == null || ability == null || !target.IsValid || pipe == null) return;
            var burst = ability.CompOfType<CompAbilityEffect_DriftingBurst>();
            var eye = ability.CompOfType<CompAbilityEffect_EyePop>();
            if (burst == null && eye == null) return;
            Vector2 feet = Ground(caster);
            Vector2 aim = target.HasThing ? Ground(target.Thing) : new Vector2(target.Cell.x + 0.5f, target.Cell.z + 0.5f);
            casts.Add(new Cast
            {
                caster = caster, eyePop = eye != null, startTick = Find.TickManager.TicksGame,
                warmupTicks = Mathf.RoundToInt(ability.def.verbProperties.warmupTime * 60f),
                cost = eye != null ? eye.Props.blows : burst.Props.blows, count = burst?.Props.count ?? 1,
                blowsBefore = pipe.Soap, cap = pipe.Props.maxBlows, feet = feet, toward = Toward(feet, aim),
            });
        }

        /// <summary>The cast that has begun and not blown, or a new one for a cast that was never begun. Its clock is set so that now is the blow.</summary>
        private Cast Landing(Pawn caster, bool eyePop, Vector2 aim, float blowsBefore, float cap, int cost, int count)
        {
            Cast cast = casts.Find(c => c.caster == caster && c.eyePop == eyePop && !c.landed);
            if (cast == null)
            {
                Vector2 feet = Ground(caster);
                cast = new Cast { caster = caster, eyePop = eyePop, feet = feet, toward = Toward(feet, aim) };
                casts.Add(cast);
            }
            cast.landed = true;
            cast.blowsBefore = blowsBefore;
            cast.cap = cap;
            cast.cost = cost;
            cast.count = count;
            cast.startTick = Find.TickManager.TicksGame - Mathf.RoundToInt(Burst.BlowAt * 60f);
            return cast;
        }

        /// <summary>Drifting Burst's warmup has ended: the bubbles start leaving the tip now.</summary>
        public void Blow(Pawn caster, IntVec3 target, float blowsBefore, float cap, CompProperties_DriftingBurst props)
        {
            Cast cast = Landing(caster, false, new Vector2(target.x + 0.5f, target.z + 0.5f), blowsBefore, cap, props.blows, props.count);
            var cloud = new Cloud
            {
                caster = caster, feet = cast.feet, toward = cast.toward, blowTick = Find.TickManager.TicksGame, count = Mathf.Max(1, props.count),
                spread = props.spreadDegrees, speed = props.driftSpeed, life = props.lifeSeconds, touch = props.touchRadius, blast = props.blastRadius,
                damage = props.damage, armorPenetration = props.armorPenetration, staggerTicks = props.staggerTicks,
                staggerMoveSpeedFactor = props.staggerMoveSpeedFactor, soundPop = props.soundPop,
            };
            cloud.Prepare();
            clouds.Add(cloud);
        }

        /// <summary>Eye Pop's warmup has ended: the bubble forms now, leaves the tip 0.2 s later and reaches the face at the flight speed.</summary>
        public void Shoot(Pawn caster, Pawn victim, float blowsBefore, float cap, CompProperties_EyePop props)
        {
            Cast cast = Landing(caster, true, Ground(victim), blowsBefore, cap, props.blows, 1);
            Vector2 target = Ground(victim);
            shots.Add(new Shot
            {
                caster = caster, victim = victim, feet = cast.feet, toward = cast.toward, target = target,
                launchTick = Find.TickManager.TicksGame + Mathf.RoundToInt(Eye.Form * 60f),
                distance = (target - cast.feet).magnitude, speed = Mathf.Max(0.5f, props.flightSpeed),
                debuffTicks = Mathf.RoundToInt(props.debuffSeconds * 60f), hediff = props.hediff, soundHit = props.soundHit,
            });
        }

        /// <summary>The caster's cast job is over. A cast that never blew has nothing left to show.</summary>
        public void Ended(Pawn caster)
        {
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                if (casts[i].caster != caster) continue;
                if (!casts[i].landed) casts.RemoveAt(i);
                else casts[i].jobOver = true;
            }
        }

        /// <summary>Whether the caster's cast has blown and its job has not ended: the job holds from here (CastJobFail).</summary>
        public bool Fired(Pawn caster)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == caster && casts[i].landed && !casts[i].jobOver) return true;
            return false;
        }

        /// <summary>Whether the cast job should still hold the caster: the pipe is not lowered yet.</summary>
        public bool Holds(Pawn caster)
        {
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == caster && casts[i].landed && casts[i].Seconds(now) < casts[i].Duration) return true;
            return false;
        }

        private bool IsCasting(Pawn pawn)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == pawn) return true;
            return false;
        }

        // ------------------------------------------------------------ holders

        /// <summary>A pawn on this map took up a pipe: its jar is drawn from now on, not only after the next listing.</summary>
        public void Track(Pawn pawn)
        {
            if (pawn != null && !holders.Contains(pawn)) holders.Add(pawn);
        }

        private void ListHolders()
        {
            holders.Clear();
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
                if (CompBubblePipe.HeldBy(pawns[i]) != null) holders.Add(pawns[i]);
        }

        private void RefillAtWater()
        {
            for (int i = 0; i < holders.Count; i++)
            {
                Pawn pawn = holders[i];
                CompBubblePipe pipe = CompBubblePipe.HeldBy(pawn);
                if (pipe == null || pipe.Full || !pawn.Spawned || pawn.Map != map || pawn.pather.Moving || !AtWater(pawn.Position)) continue;
                pipe.Refill(pipe.Props.refillPerSecond * HolderInterval / 60f);
            }
        }

        /// <summary>The cell or one of the eight round it is water.</summary>
        private bool AtWater(IntVec3 at)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    IntVec3 c = new IntVec3(at.x + dx, 0, at.z + dz);
                    if (c.InBounds(map) && c.GetTerrain(map).IsWater) return true;
                }
            return false;
        }

        // ------------------------------------------------------------ ticks

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % HolderInterval == 0)
            {
                ListHolders();
                RefillAtWater();
            }
            if (casts.Count == 0 && clouds.Count == 0 && shots.Count == 0 && soaped.Count == 0) return;

            for (int i = casts.Count - 1; i >= 0; i--)
            {
                Cast cast = casts[i];
                float seconds = cast.Seconds(now);
                bool gone = cast.caster == null || !cast.caster.Spawned || cast.caster.Map != map;
                if (cast.landed ? seconds >= cast.Duration : gone || seconds > cast.warmupTicks / 60f + Overdue) casts.RemoveAt(i);
            }
            for (int i = clouds.Count - 1; i >= 0; i--)
            {
                Cloud cloud = clouds[i];
                float seconds = cloud.Seconds(now);
                if (seconds >= cloud.EndsAt) { clouds.RemoveAt(i); continue; }
                if (now % TouchInterval == 0 && seconds < Burst.ExpireAt(cloud.life)) Touch(cloud, seconds);
            }
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                Shot shot = shots[i];
                float seconds = shot.Seconds(now);
                Pawn victim = shot.victim;
                bool there = victim != null && victim.Spawned && victim.Map == map && !victim.Dead;
                if (!shot.hit)
                {
                    if (there) shot.target = Ground(victim);
                    if (seconds >= shot.HitAt)
                    {
                        shot.hit = true;
                        if (there) SoapEyes(shot, now);
                    }
                }
                if (seconds >= shot.HitAt + 0.4f) shots.RemoveAt(i);
            }
            for (int i = soaped.Count - 1; i >= 0; i--)
            {
                Soaped s = soaped[i];
                if (now >= s.clearTick || s.victim == null || s.victim.Dead || s.victim.health.hediffSet.GetFirstHediffOfDef(s.hediff) == null) soaped.RemoveAt(i);
            }
        }

        /// <summary>Pops every bubble of the cloud that a pawn is touching or that has drifted into a wall.</summary>
        private void Touch(Cloud cloud, float seconds)
        {
            var frame = new BubbleFrame(cloud.toward, Vector2.zero);
            for (int i = 0; i < cloud.count; i++)
            {
                if (cloud.pops[i].Popped || !Burst.At(i, cloud.count, cloud.spread, cloud.speed, seconds, out DriftingBubble b) || b.Forming) continue;
                Vector2 ground = frame.Place(cloud.feet, b.Along, b.Across);
                IntVec3 cell = CellOf(ground);
                if (!cell.InBounds(map) || cell.Filled(map))
                {
                    cloud.Popped(i, seconds, b, true);
                    continue;
                }
                if (Toucher(ground, cell, cloud.touch, cloud.caster) == null) continue;
                cloud.Popped(i, seconds, b, false);
                Blast(cloud, ground, cell);
            }
        }

        /// <summary>
        /// The first pawn other than the caster whose drawn position is within <paramref name="reach"/> of the
        /// bubble's ground point. Downed pawns lie under it and do not count. A walking pawn is drawn up
        /// to a cell away from the cell it is listed in, hence the extra ring of cells searched.
        /// </summary>
        private Pawn Toucher(Vector2 ground, IntVec3 cell, float reach, Pawn caster)
        {
            int r = Mathf.CeilToInt(reach) + 1;
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                {
                    IntVec3 c = new IntVec3(cell.x + dx, 0, cell.z + dz);
                    if (!c.InBounds(map)) continue;
                    List<Thing> things = map.thingGrid.ThingsListAtFast(c);
                    for (int t = 0; t < things.Count; t++)
                        if (things[t] is Pawn pawn && pawn != caster && !pawn.Dead && !pawn.Downed && (Ground(pawn) - ground).sqrMagnitude < reach * reach)
                            return pawn;
                }
            return null;
        }

        /// <summary>The pop: blunt damage and a stagger to every pawn but the caster within the blast radius.</summary>
        private void Blast(Cloud cloud, Vector2 ground, IntVec3 cell)
        {
            cloud.soundPop?.PlayOneShot(new TargetInfo(cell, map));
            hit.Clear();
            int r = Mathf.CeilToInt(cloud.blast) + 1;
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                {
                    IntVec3 c = new IntVec3(cell.x + dx, 0, cell.z + dz);
                    if (!c.InBounds(map)) continue;
                    List<Thing> things = map.thingGrid.ThingsListAtFast(c);
                    for (int t = 0; t < things.Count; t++)
                        if (things[t] is Pawn pawn && pawn != cloud.caster && !pawn.Dead && (Ground(pawn) - ground).sqrMagnitude <= cloud.blast * cloud.blast)
                            hit.Add(pawn);
                }
            for (int i = 0; i < hit.Count; i++)
            {
                Pawn pawn = hit[i];
                if (cloud.damage >= 1f)
                {
                    Vector2 away = Ground(pawn) - ground;
                    Vector2 way = away.sqrMagnitude < 0.0001f ? cloud.toward : away;
                    float angle = new Vector3(way.x, 0f, way.y).AngleFlat();
                    pawn.TakeDamage(new DamageInfo(DamageDefOf.Blunt, cloud.damage, cloud.armorPenetration, angle, cloud.caster, null,
                        BubblePipeDefOf.AG_BubblePipe, DamageInfo.SourceCategory.ThingOrUnknown, pawn));
                }
                if (cloud.staggerTicks > 0 && pawn.Spawned && !pawn.Dead) pawn.stances?.stagger?.StaggerFor(cloud.staggerTicks, cloud.staggerMoveSpeedFactor);
            }
            hit.Clear();
        }

        /// <summary>The soap lands: the hediff for the ability's length, started again on a pawn that already has it.</summary>
        private void SoapEyes(Shot shot, int now)
        {
            Pawn victim = shot.victim;
            shot.soundHit?.PlayOneShot(new TargetInfo(victim.Position, map));
            if (shot.hediff == null || victim.health == null) return;
            Hediff hediff = victim.health.hediffSet.GetFirstHediffOfDef(shot.hediff) ?? victim.health.AddHediff(shot.hediff);
            HediffComp_Disappears disappears = hediff?.TryGetComp<HediffComp_Disappears>();
            if (disappears != null)
            {
                disappears.ticksToDisappear = shot.debuffTicks;
                disappears.disappearsAfterTicks = shot.debuffTicks;
            }
            Soaped entry = soaped.Find(s => s.victim == victim);
            if (entry == null) soaped.Add(entry = new Soaped { victim = victim });
            entry.hediff = shot.hediff;
            entry.hitTick = now;
            entry.clearTick = now + shot.debuffTicks;
        }

        // ------------------------------------------------------------ drawing

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map) return;
            int now = Find.TickManager.TicksGame;

            for (int i = 0; i < holders.Count; i++)
            {
                Pawn pawn = holders[i];
                CompBubblePipe pipe = CompBubblePipe.HeldBy(pawn);
                if (pipe == null || !pawn.Spawned || pawn.Map != map || IsCasting(pawn) || pawn.GetPosture() != PawnPosture.Standing) continue;
                IntVec3 facing = pawn.Rotation.FacingCell;
                BubblePipeGraphics.DrawJarOn(Ground(pawn), new Vector2(facing.x, facing.z), pipe.Soap, pipe.Props.maxBlows, map);
            }

            for (int i = 0; i < casts.Count; i++)
            {
                Cast cast = casts[i];
                float seconds = cast.Seconds(now);
                // Until the warmup ends the clock stops just before the blow, with the pipe at the mouth.
                if (!cast.landed) seconds = Mathf.Min(seconds, Burst.BlowAt - 0.001f);
                if (cast.eyePop)
                    BubblePipeEyePopGraphics.DrawCaster(cast.feet, cast.toward, seconds, Eye.CastLowerAt, cast.blowsBefore, cast.cost, map, cast.cap);
                else
                    BubblePipeDriftingBurstGraphics.DrawCaster(cast.feet, cast.toward, seconds, Burst.CastLowerAt(cast.count), cast.blowsBefore, cast.cost,
                        cast.count, cast.cap, map);
            }

            for (int i = 0; i < clouds.Count; i++)
            {
                Cloud cloud = clouds[i];
                BubblePipeDriftingBurstGraphics.DrawCloud(cloud.feet, cloud.toward, cloud.Seconds(now), cloud.count, cloud.spread, cloud.speed, cloud.life,
                    cloud.blast, cloud.pops, true, map, cloud.harmlessNow);
            }

            for (int i = 0; i < shots.Count; i++)
            {
                Shot shot = shots[i];
                BubblePipeEyePopGraphics.DrawShot(shot.feet + shot.toward * BubblePipeGraphics.TipAlong, shot.target, shot.Seconds(now), shot.HitAt, map);
            }

            float filmAltitude = AltitudeLayer.PawnState.AltitudeFor();
            for (int i = 0; i < soaped.Count; i++)
            {
                Soaped s = soaped[i];
                Pawn victim = s.victim;
                if (victim == null || !victim.Spawned || victim.Map != map || victim.Rotation == Rot4.North) continue;
                Vector2 ground = Ground(victim);
                Vector3 head = victim.Drawer.renderer.BaseHeadOffsetAt(victim.Rotation);
                float sinceHit = (now - s.hitTick) / 60f, untilClear = (s.clearTick - now) / 60f;
                BubblePipeEyePopGraphics.DrawSoaped(ground, new Vector2(ground.x + head.x, ground.y + head.z), sinceHit, untilClear,
                    (s.clearTick - s.hitTick) / 60f, sinceHit, filmAltitude, map);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref clouds, "bubbleClouds", LookMode.Deep);
            Scribe_Collections.Look(ref shots, "eyePopShots", LookMode.Deep);
            Scribe_Collections.Look(ref soaped, "soapedPawns", LookMode.Deep);
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            if (clouds == null) clouds = new List<Cloud>();
            if (shots == null) shots = new List<Shot>();
            if (soaped == null) soaped = new List<Soaped>();
            clouds.RemoveAll(c => c == null);
            shots.RemoveAll(s => s == null);
            soaped.RemoveAll(s => s == null || s.victim == null);
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            ListHolders();
        }
    }
}
