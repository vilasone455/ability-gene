using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Pump = RimArt.WaterGunPumpTiming;
using Shot = RimArt.WaterGunStreamTiming;

namespace RimArt
{
    public enum WaterGunCastKind { Stream, Pump }

    /// <summary>
    /// Everything the Water Gun does on a map:
    /// - plays Stream Shot's and Hydro Pump's pictures for real casts and lands each hit when the
    ///   picture shows it (the jet arriving, the blast front reaching a pawn);
    /// - refills the bags of the pawns holding a gun, once a second;
    /// - draws the bag on each holder's back with its level, when no cast is drawing the weapon.
    ///
    /// A cast is known from the moment its warmup starts (<see cref="Begin"/>, from
    /// JobDriver_CastWaterGun), because the gun comes up and the bag is pumped during the warmup. The
    /// ability's comp then says what the cast does (<see cref="LandStream"/>, <see cref="LandPump"/>).
    /// The clock is game ticks. Nothing about a cast is saved: a game loaded mid-cast loses the rest of
    /// its picture and any hit not yet dealt. The water itself is saved on the gun.
    /// </summary>
    public class MapComponent_WaterGun : MapComponent
    {
        private sealed class Cast
        {
            public Pawn caster;
            public WaterGunCastKind kind;
            public int startTick, warmupTicks;
            public bool landed;
            /// <summary>The gun is back at rest: the held gun and the idle bag are drawn again and the caster may go.</summary>
            public bool home;
            public Vector2 feet;
            public float aim, units;

            public WaterStreamShot stream;
            public CompProperties_StreamShot streamProps;
            public Pawn target;
            public IntVec3 aimedCell;
            public bool hitDone;

            public WaterPumpShot pump;
            public CompProperties_HydroPump pumpProps;
            public Pawn[] victims;
            public IntVec3[] pushTo;
            public bool[] victimHit, victimDown;
            public List<IntVec3> cone;
            public bool blastDone, coneDry;

            public float Seconds(int now) => WaterGunGraphics.Lead + (now - startTick) / 60f;
        }

        /// <summary>A cast whose warmup ran out this long ago without landing was interrupted.</summary>
        private const float Overdue = 0.25f;
        /// <summary>Ticks between refills and between looks for who holds a gun.</summary>
        private const int RefillInterval = 60;
        /// <summary>Pawns whose weapon a cast's picture is drawing, on any map: Core's held gun is not drawn for them.</summary>
        private static readonly HashSet<Pawn> casting = new HashSet<Pawn>();

        private readonly List<Cast> casts = new List<Cast>();
        private readonly List<Pawn> holders = new List<Pawn>();

        public MapComponent_WaterGun(Map map) : base(map) { }

        public static bool IsCasting(Pawn pawn) => pawn != null && casting.Contains(pawn);

        public void Register(Pawn pawn)
        {
            if (pawn != null && !holders.Contains(pawn)) holders.Add(pawn);
        }

        private static Vector2 Ground(Thing thing)
        {
            Vector3 at = thing.DrawPos;
            return new Vector2(at.x, at.z);
        }

        private static Vector2 Ground(IntVec3 cell) => new Vector2(cell.x + 0.5f, cell.z + 0.5f);

        private static float Degrees(Vector2 run) => run.sqrMagnitude < 0.0001f ? 0f : Mathf.Atan2(run.y, run.x) * Mathf.Rad2Deg;

        // ------------------------------------------------------------------ casts

        /// <summary>A cast's warmup has begun: the gun comes up (and for Hydro Pump the bag is pumped) toward the target.</summary>
        public void Begin(Pawn caster, Ability ability, LocalTargetInfo target)
        {
            Ended(caster);
            if (caster == null || ability == null || !target.IsValid) return;
            WaterGunCastKind kind = ability.CompOfType<CompAbilityEffect_HydroPump>() != null ? WaterGunCastKind.Pump : WaterGunCastKind.Stream;
            Cast cast = Add(caster, kind, target, Find.TickManager.TicksGame, ability.def.verbProperties.warmupTime);
            if (kind == WaterGunCastKind.Pump) cast.pumpProps = ability.CompOfType<CompAbilityEffect_HydroPump>().Props;
        }

        private Cast Add(Pawn caster, WaterGunCastKind kind, LocalTargetInfo target, int startTick, float warmup)
        {
            Vector2 feet = Ground(caster);
            Vector2 to = target.Thing is Pawn pawn ? Ground(pawn) : Ground(target.Cell);
            Vector2 aimRun = kind == WaterGunCastKind.Pump ? WaterGunCone.Toward(caster.Position, target.Cell) : to - feet;
            var cast = new Cast
            {
                caster = caster, kind = kind, startTick = startTick, warmupTicks = Mathf.RoundToInt(warmup * 60f), feet = feet,
                aim = Degrees(aimRun), units = CompWaterGun.HeldBy(caster)?.DrawnUnits ?? WaterGunGraphics.BagCap,
            };
            cast.stream = new WaterStreamShot { Target = to, Units = cast.units, OnPawn = target.Thing is Pawn, GunHold = Shot.GameGunHold, Hold = Shot.ScriptHold };
            casts.Add(cast);
            casting.Add(caster);
            return cast;
        }

        /// <summary>The cast that has begun and not landed, or a new one for a cast that was never begun.</summary>
        private Cast Landing(Pawn caster, WaterGunCastKind kind, LocalTargetInfo target, float warmup)
        {
            Cast cast = casts.Find(c => c.caster == caster && c.kind == kind && !c.landed)
                ?? Add(caster, kind, target, Find.TickManager.TicksGame - Mathf.RoundToInt(warmup * 60f), warmup);
            cast.landed = true;
            return cast;
        }

        public void LandStream(Pawn caster, LocalTargetInfo target, float warmup, CompProperties_StreamShot props)
        {
            Cast cast = Landing(caster, WaterGunCastKind.Stream, target, warmup);
            cast.streamProps = props;
            cast.target = target.Thing as Pawn;
            cast.aimedCell = target.Cell;
            // The jet goes where the target is now.
            Vector2 to = cast.target != null && cast.target.Spawned ? Ground(cast.target) : Ground(target.Cell);
            cast.stream.Target = to;
            cast.stream.OnPawn = cast.target != null;
            cast.stream.Burning = cast.target != null ? cast.target.IsBurning() : FireUtility.NumFiresAt(target.Cell, map) > 0;
            cast.aim = Degrees(to - cast.feet);
        }

        public void LandPump(Pawn caster, IntVec3 target, float warmup, CompProperties_HydroPump props)
        {
            Cast cast = Landing(caster, WaterGunCastKind.Pump, target, warmup);
            cast.pumpProps = props;
            IntVec3 from = caster.Position;
            Vector2 toward = WaterGunCone.Toward(from, target);
            cast.aim = Degrees(toward);
            cast.cone = WaterGunCone.Cells(caster, target, props.length, props.width);

            var victims = new List<Pawn>();
            for (int i = 0; i < cast.cone.Count; i++)
            {
                List<Thing> things = cast.cone[i].GetThingList(map);
                for (int t = 0; t < things.Count; t++)
                    if (things[t] is Pawn pawn && pawn != caster && !pawn.Dead && !victims.Contains(pawn)) victims.Add(pawn);
            }
            int n = victims.Count;
            cast.victims = victims.ToArray();
            cast.pushTo = new IntVec3[n];
            cast.victimHit = new bool[n];
            cast.victimDown = new bool[n];
            var drawn = new WaterPumpVictim[n];
            for (int i = 0; i < n; i++)
            {
                Pawn victim = victims[i];
                WaterGunCone.Contains(from, toward, victim.Position, props.length, props.width, out float along, out float across);
                cast.pushTo[i] = PowerPoleCombat.PushDestination(victim.Position, toward, props.pushCells, map, out _);
                drawn[i] = new WaterPumpVictim
                {
                    Along = along, Across = across, Pushed = (cast.pushTo[i] - victim.Position).LengthHorizontal, Burning = victim.IsBurning(),
                };
            }
            cast.pump = new WaterPumpShot
            {
                Length = props.length, Width = props.width, Push = props.pushCells, Down = props.knockdownSeconds, Units = cast.units,
                GunHold = Pump.GameGunHold, Victims = drawn,
            };
        }

        /// <summary>The caster's cast job is over. A cast that never landed has nothing left to show.</summary>
        public void Ended(Pawn caster)
        {
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                if (casts[i].caster != caster) continue;
                if (!casts[i].landed) Remove(i);
                else if (!casts[i].home) { casts[i].home = true; Release(caster); }
            }
        }

        /// <summary>Whether the caster's cast has landed and its job has not ended: the job holds from here (CastJobFail).</summary>
        public bool Fired(Pawn caster)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == caster && casts[i].landed && !casts[i].home) return true;
            return false;
        }

        /// <summary>Whether the caster's cast job should still hold it in place: the gun is still up.</summary>
        public bool Holds(Pawn caster)
        {
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == caster && casts[i].landed && !casts[i].home && casts[i].Seconds(now) < GunDown(casts[i])) return true;
            return false;
        }

        private static float GunDown(Cast cast) => cast.kind == WaterGunCastKind.Pump
            ? Pump.GunDown(Pump.GameGunHold)
            : Shot.GunDown((cast.stream.Target - cast.feet).magnitude, Shot.GameGunHold);

        private static float End(Cast cast) => cast.kind == WaterGunCastKind.Pump
            ? Pump.End(cast.pump.Down, Pump.GameGunHold)
            : Shot.End((cast.stream.Target - cast.feet).magnitude, Shot.GameGunHold, Shot.ScriptHold);

        private void Remove(int index)
        {
            Pawn caster = casts[index].caster;
            casts.RemoveAt(index);
            Release(caster);
        }

        private void Release(Pawn caster)
        {
            if (!casts.Exists(c => c.caster == caster && !c.home)) casting.Remove(caster);
        }

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % RefillInterval == 0) Refill();
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                Cast cast = casts[i];
                float seconds = cast.Seconds(now);
                if (!cast.landed)
                {
                    if (seconds > WaterGunGraphics.Lead + cast.warmupTicks / 60f + Overdue || cast.caster == null || !cast.caster.Spawned || cast.caster.Map != map) Remove(i);
                    continue;
                }
                if (!cast.home && (seconds >= GunDown(cast) || !cast.caster.Spawned || cast.caster.Downed))
                {
                    cast.home = true;
                    Release(cast.caster);
                }
                if (cast.kind == WaterGunCastKind.Pump) TickPump(cast, seconds);
                else TickStream(cast, seconds);
                if (seconds >= End(cast)) Remove(i);
            }
        }

        private void TickStream(Cast cast, float seconds)
        {
            if (cast.hitDone || seconds < Shot.Hit((cast.stream.Target - cast.feet).magnitude)) return;
            cast.hitDone = true;
            CompProperties_StreamShot props = cast.streamProps;
            Shake(Shot.HitShake);
            WaterGunSoak.ExtinguishCell(cast.aimedCell, map);
            Pawn victim = cast.target;
            if (!Hittable(victim) || victim.Position.DistanceTo(cast.aimedCell) > props.followRadius)
            {
                cast.target = null;
                return;
            }
            WaterGunSoak.ExtinguishCell(victim.Position, map);
            Hit(victim, cast.caster, props.damage, props.armorPenetration, cast.aim);
            if (Hittable(victim)) WaterGunSoak.Apply(victim, props.soakedSeconds);
        }

        private void TickPump(Cast cast, float seconds)
        {
            CompProperties_HydroPump props = cast.pumpProps;
            if (!cast.blastDone && seconds >= Pump.Blast)
            {
                cast.blastDone = true;
                Shake(Pump.BlastShake);
            }
            if (!cast.coneDry && seconds >= Pump.Blast + Pump.Front)
            {
                cast.coneDry = true;
                for (int i = 0; i < cast.cone.Count; i++) WaterGunSoak.ExtinguishCell(cast.cone[i], map);
            }
            for (int v = 0; v < cast.victims.Length; v++)
            {
                Pawn victim = cast.victims[v];
                float reach = Pump.ReachAt(cast.pump.Victims[v].Along, props.length);
                if (!cast.victimHit[v] && seconds >= reach)
                {
                    cast.victimHit[v] = true;
                    if (!Hittable(victim)) { cast.victimDown[v] = true; continue; }
                    WaterGunSoak.Extinguish(victim);
                    Hit(victim, cast.caster, props.damage, props.armorPenetration, cast.aim);
                    if (!Hittable(victim)) { cast.victimDown[v] = true; continue; }
                    WaterGunSoak.Apply(victim, props.soakedSeconds);
                    if (cast.pushTo[v] != victim.Position && cast.pushTo[v].Standable(map))
                    {
                        PawnFlyer flyer = PawnFlyer.MakeFlyer(WaterGunDefOf.AG_WaterGunPushed, victim, cast.pushTo[v], null, null);
                        if (flyer != null) GenSpawn.Spawn(flyer, cast.pushTo[v], map);
                    }
                }
                // Knocked down once it has slid: a stun for the knockdown, after the flyer has put it back on the map.
                if (cast.victimHit[v] && !cast.victimDown[v] && seconds >= reach + Pump.Slide + 0.05f && (!Hittable(victim) || victim.Spawned))
                {
                    cast.victimDown[v] = true;
                    if (Hittable(victim)) victim.stances?.stunner?.StunFor(Mathf.RoundToInt(props.knockdownSeconds * 60f), cast.caster, false);
                }
            }
        }

        private static void Hit(Pawn victim, Pawn caster, float damage, float armorPenetration, float aimDegrees)
        {
            if (damage < 1f) return;
            float angle = new Vector3(Mathf.Cos(aimDegrees * Mathf.Deg2Rad), 0f, Mathf.Sin(aimDegrees * Mathf.Deg2Rad)).AngleFlat();
            victim.TakeDamage(new DamageInfo(DamageDefOf.Blunt, damage, armorPenetration, angle, caster, null, WaterGunDefOf.AG_WaterGun,
                DamageInfo.SourceCategory.ThingOrUnknown, victim));
        }

        private bool Hittable(Pawn pawn) => pawn != null && pawn.Spawned && pawn.Map == map && !pawn.Dead;

        private void Shake(float size)
        {
            if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(size);
        }

        // ------------------------------------------------------------------ refill

        /// <summary>Once a second: find who holds a gun, and refill their bags next to water or in rain.</summary>
        private void Refill()
        {
            holders.Clear();
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                CompWaterGun gun = CompWaterGun.HeldBy(pawn);
                if (gun == null) continue;
                holders.Add(pawn);
                if (gun.Water >= gun.Props.capacity) continue;
                float perSecond = 0f;
                if (NearWater(pawn.Position)) perSecond = gun.Props.refillPerSecondNearWater;
                else if (!map.roofGrid.Roofed(pawn.Position) && map.weatherManager.RainRate >= gun.Props.minRainRate) perSecond = gun.Props.refillPerSecondInRain;
                if (perSecond > 0f) gun.Add(perSecond * RefillInterval / 60f);
            }
        }

        private bool NearWater(IntVec3 cell)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var c = new IntVec3(cell.x + dx, 0, cell.z + dz);
                    if (c.InBounds(map) && c.GetTerrain(map).IsWater) return true;
                }
            return false;
        }

        // ------------------------------------------------------------------ drawing

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map) return;
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < casts.Count; i++)
            {
                Cast cast = casts[i];
                float seconds = cast.Seconds(now);
                bool gun = !cast.home;
                if (cast.kind == WaterGunCastKind.Pump)
                {
                    if (!cast.landed)
                    {
                        // Until it lands the clock stops just before the blast, and no pawn is hit yet.
                        CompProperties_HydroPump props = cast.pumpProps;
                        var waiting = new WaterPumpShot
                        {
                            Length = props?.length ?? Pump.ScriptLength, Width = props?.width ?? Pump.ScriptWidth, Push = props?.pushCells ?? Pump.ScriptPush,
                            Down = props?.knockdownSeconds ?? Pump.ScriptDown, Units = cast.units, GunHold = Pump.GameGunHold,
                        };
                        WaterGunPumpGraphics.Draw(cast.feet, cast.aim, waiting, Mathf.Min(seconds, Pump.Blast - 0.001f), map, gun);
                        continue;
                    }
                    for (int v = 0; v < cast.victims.Length; v++)
                        cast.pump.Victims[v].LiveAt = cast.victims[v] != null && cast.victims[v].Spawned && cast.victims[v].Map == map ? Ground(cast.victims[v]) : (Vector2?)null;
                    WaterGunPumpGraphics.Draw(cast.feet, cast.aim, cast.pump, seconds, map, gun);
                }
                else
                {
                    cast.stream.DripAt = cast.target != null && cast.target.Spawned && cast.target.Map == map ? Ground(cast.target) : (Vector2?)null;
                    WaterGunStreamGraphics.Draw(cast.feet, cast.stream, cast.landed ? seconds : Mathf.Min(seconds, Shot.Fire - 0.001f), map, gun);
                }
            }
            for (int i = 0; i < holders.Count; i++)
            {
                Pawn pawn = holders[i];
                if (pawn == null || !pawn.Spawned || pawn.Map != map || pawn.Downed || IsCasting(pawn) || pawn.GetPosture() != PawnPosture.Standing) continue;
                CompWaterGun comp = CompWaterGun.HeldBy(pawn);
                if (comp == null) continue;
                WaterGunGraphics.IdleBag(Ground(pawn), Facing(pawn.Rotation), comp.DrawnUnits, map);
            }
        }

        private static float Facing(Rot4 rotation)
        {
            if (rotation == Rot4.North) return 90f;
            if (rotation == Rot4.West) return 180f;
            if (rotation == Rot4.South) return 270f;
            return 0f;
        }

        public override void MapRemoved()
        {
            for (int i = casts.Count - 1; i >= 0; i--) Remove(i);
        }
    }
}
