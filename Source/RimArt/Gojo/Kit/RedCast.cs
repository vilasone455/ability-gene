using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using R = RimArt.GojoRed;

namespace RimArt
{
    /// <summary>
    /// Red's numbers (AG_GojoRed). The range (20), line of sight and the charge at the finger (the verb's warmup,
    /// 0.6 s: the picture's arm and charge) are the verb's.
    /// </summary>
    public class CompProperties_GojoRed : CompProperties_AbilityEffect
    {
        /// <summary>Red's speed in flight, cells per second.</summary>
        public float speed = 20f;
        /// <summary>The first pawn or loose thing hit: cells it is thrown along Red's line, blunt per cell it travels, blunt added when a wall stops it.</summary>
        public float throwCells = 6f, damagePerCell = 1.5f, slamDamage = 10f;
        /// <summary>Every other pawn and loose thing within burstRadius of the burst: pushed pushCells straight away from it; a pawn takes pushDamage blunt.</summary>
        public float burstRadius = 1.5f, pushCells = 2f, pushDamage = 8f;

        public CompProperties_GojoRed()
        {
            compClass = typeof(CompAbilityEffect_GojoRed);
        }
    }

    /// <summary>
    /// Reversal: Red. The fire tick (the end of the warmup) is when Red leaves the finger; the flight, the burst,
    /// the throw and the pushes are <see cref="RedShot"/>, run by <see cref="MapComponent_GojoKit"/>.
    /// </summary>
    public class CompAbilityEffect_GojoRed : CompAbilityEffect
    {
        public new CompProperties_GojoRed Props => (CompProperties_GojoRed)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent.pawn;
            if (!target.IsValid || caster == null || target.Cell == caster.Position) return false;
            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (caster?.Map == null || !target.IsValid) return;
            caster.Map.GetComponent<MapComponent_GojoKit>()?.FireRed(parent, target.Cell);
        }
    }

    /// <summary>
    /// Red's cast job: JobDriver_CastAbility that starts the picture (<see cref="RedShot"/>) when the warmup begins
    /// and, after the fire, keeps Gojo standing with his arm out until the picture lowers it (0.7 s after the
    /// burst, or 0.6 s after Red reaches a Blue for Hollow Purple). A cast called off before the fire costs nothing
    /// and starts no cooldown; one that fired had its cooldown and charge taken by Ability.Activate.
    /// </summary>
    public class JobDriver_CastGojoRed : JobDriver_CastAbility
    {
        private int shotTick = -1;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => shotTick < 0 && !job.ability.CanCast && !job.ability.Casting);
            // A targeted thing gone during the warmup calls it off; after the fire Red flies on whatever it does.
            this.FailOn(() => job.targetA.HasThing && !job.targetA.Thing.Spawned && !Fired);
            AddFinishAction(delegate { pawn.MapHeld?.GetComponent<MapComponent_GojoKit>()?.JobEnded(pawn); });

            Toil begin = ToilMaker.MakeToil("GojoRedBegin");
            begin.initAction = Begin;
            begin.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return begin;

            yield return Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);

            Toil hold = ToilMaker.MakeToil("GojoRedHold");
            hold.initAction = () => pawn.pather.StopDead();
            hold.tickAction = () =>
            {
                RedShot shot = pawn.Map?.GetComponent<MapComponent_GojoKit>()?.Holding(pawn, Find.TickManager.TicksGame);
                if (shot == null)
                {
                    ReadyForNextToil();
                    return;
                }
                pawn.rotationTracker.FaceCell(shot.target);
            };
            hold.handlingFacing = true;
            hold.defaultCompleteMode = ToilCompleteMode.Never;
            yield return hold;
        }

        /// <summary>The shot this job began has left the finger.</summary>
        private bool Fired
        {
            get
            {
                IReadOnlyList<RedShot> reds = pawn.MapHeld?.GetComponent<MapComponent_GojoKit>()?.Reds;
                if (reds == null || shotTick < 0) return false;
                for (int i = 0; i < reds.Count; i++)
                    if (reds[i].caster == pawn && reds[i].startTick == shotTick) return reds[i].Fired;
                return false;
            }
        }

        private void Begin()
        {
            pawn.pather.StopDead();
            if (shotTick >= 0 || job.ability == null || !job.ability.CanCast) return;
            shotTick = Find.TickManager.TicksGame;
            pawn.Map.GetComponent<MapComponent_GojoKit>().BeginRed(pawn, job.targetA.Cell, shotTick, job.ability.def);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref shotTick, "gojoRedShotTick", -1);
        }
    }

    /// <summary>
    /// A thing Red's burst moves: the thing hit, thrown along Red's line, or one pushed straight away from the
    /// burst. A pawn flies in a <see cref="PawnFlyer_VectorThrown"/> and takes its damage when it lands; an item
    /// or corpse is taken off the map, drawn along its way and put down where it lands.
    /// </summary>
    public sealed class RedMove : IExposable
    {
        public Pawn pawn;
        /// <summary>The item or corpse while it flies (off the map).</summary>
        public Thing item;
        /// <summary>Its ground point at the burst, its unit way, the cell it lands in and the cells it covers.</summary>
        public Vector2 from, way;
        public IntVec3 land;
        public float cells;
        /// <summary>A wall stopped it; <see cref="face"/> is how far along its way the wall's cell begins.</summary>
        public bool walled;
        public float face;
        /// <summary>Seconds it takes; the tick it lands; blunt to a pawn when it lands.</summary>
        public float seconds;
        public int landTick;
        public float damage;
        public bool thrown, landed;

        public Vector2 Land => GojoKit.Ground(land);

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Deep.Look(ref item, "item");
            Scribe_Values.Look(ref from, "from");
            Scribe_Values.Look(ref way, "way");
            Scribe_Values.Look(ref land, "land");
            Scribe_Values.Look(ref cells, "cells");
            Scribe_Values.Look(ref walled, "walled");
            Scribe_Values.Look(ref face, "face");
            Scribe_Values.Look(ref seconds, "seconds");
            Scribe_Values.Look(ref landTick, "landTick");
            Scribe_Values.Look(ref damage, "damage");
            Scribe_Values.Look(ref thrown, "thrown");
            Scribe_Values.Look(ref landed, "landed");
        }
    }

    /// <summary>
    /// One Red, from the warmup to the end of its picture. The picture's clock is 0 when the warmup begins
    /// (<see cref="startTick"/>); Red leaves the finger at the fire tick, <see cref="GojoRed.Tip"/> cells in front of
    /// Gojo's point, and flies straight at the target cell at <c>speed</c>. Each tick its path is walked in 0.1-cell
    /// steps; the first of these ends it:
    /// <list type="bullet">
    /// <item>a cell no shot can pass over (a wall, rock, a closed door): it bursts there, nothing thrown. Checked
    /// first, so Purple never forms through a wall that stands beside Blue.</item>
    /// <item>within Hollow Purple's <c>blueRadius</c> of the centre of an active Blue that Gojo cast, while Purple is
    /// ready and paid for: both are used up and <see cref="PurpleRun"/> starts (no burst). Not ready: Red passes on.</item>
    /// <item>a cell holding a pawn (not Gojo) or a loose thing: it bursts at that thing (the one furthest back along
    /// the line, a pawn before a thing), which is thrown. Not inside an active Blue's pull while Purple is ready: there
    /// Red flies through (<see cref="MapComponent_GojoKit.PassesThroughBlue"/>).</item>
    /// <item>the target cell's centre: it bursts there, nothing thrown.</item>
    /// </list>
    /// </summary>
    public sealed class RedShot : IExposable
    {
        public Pawn caster;
        public IntVec3 target;
        /// <summary>The picture's 0 (the warmup began) and the fire tick (-1 before).</summary>
        public int startTick, fireTick = -1;
        /// <summary>The charge the picture draws before the fire (the warmup as it will be, less the arm's lead).</summary>
        public float expectedCharge = R.Charge;
        /// <summary>From the fire: Gojo's ground point, Red's unit way, the target's distance, Red's centre now (cells from the point).</summary>
        public Vector2 origin, dir;
        public float targetDist, along;
        /// <summary>The burst: its tick, where along the line, whether a wall stopped Red itself.</summary>
        public int burstTick = -1;
        public float burstAlong;
        public bool wallBurst;
        /// <summary>Taken into Hollow Purple at <see cref="usedTick"/>: no burst, the picture stops.</summary>
        public bool used;
        public int usedTick = -1;
        public RedMove hit;
        public List<RedMove> pushed = new List<RedMove>();
        // The ability's numbers at the fire.
        public float speed = R.Speed, throwCells = R.ThrowCells, damagePerCell = 1.5f, slamDamage = 10f,
            burstRadius = R.BurstRadius, pushCells = R.PushCells, pushDamage = 8f;

        // Not saved: the cell Red is in, the camera shakes made, and the picture's guess at where it will burst.
        private IntVec3 lastCell = IntVec3.Invalid;
        private int shaken;
        private float plannedDist = -1f;
        private bool plannedThing;

        public bool Fired => fireTick >= 0;
        public bool Burst => burstTick >= 0;

        /// <summary>The picture's charge: the warmup as it was, from the arm's start to the fire, less the arm's lead.</summary>
        public float Charge => Fired ? Mathf.Max(0.05f, (fireTick - startTick) / 60f - R.Start) : expectedCharge;

        /// <summary>Where Red bursts, cells from Gojo's point: the burst, or before it the picture's guess.</summary>
        public float Dist => Burst ? burstAlong : plannedDist >= 0f ? plannedDist : targetDist;

        /// <summary>The picture's time of the burst and its length on its own clock.</summary>
        public float Arrive => R.Arrive(Charge, Dist, speed);
        public float FlyTime => hit != null && hit.cells > 0f ? R.FlyTime(throwCells, hit.cells) : 0f;
        public float EndSeconds => R.End(Arrive, FlyTime);

        public int TickAt(float seconds) => startTick + Mathf.CeilToInt(seconds * 60f);

        /// <summary>Gojo's arm is out until 0.7 s after the burst (the picture's ArmOut), or 0.6 s after Red met a Blue.</summary>
        public bool Holds(int now)
        {
            if (!Fired) return false;
            if (used) return now < usedTick + 36;
            if (!Burst) return true;
            return now < TickAt(Arrive + 0.7f);
        }

        public void Fire(Ability ability, IntVec3 cell, int now)
        {
            fireTick = now;
            target = cell;
            origin = GojoKit.Ground(caster.DrawPos);
            Vector2 to = GojoKit.Ground(cell) - origin;
            targetDist = to.magnitude;
            dir = targetDist > 0.001f ? to / targetDist : Vector2.right;
            along = Mathf.Min(R.Tip, targetDist);
            lastCell = caster.Position;
            var props = GojoKit.Props<CompProperties_GojoRed>(ability.def) ?? new CompProperties_GojoRed();
            speed = props.speed;
            throwCells = props.throwCells;
            damagePerCell = props.damagePerCell;
            slamDamage = props.slamDamage;
            burstRadius = props.burstRadius;
            pushCells = props.pushCells;
            pushDamage = props.pushDamage;
        }

        /// <summary>One game tick. False once the shot is over and its picture has gone.</summary>
        public bool Tick(MapComponent_GojoKit kit, int now)
        {
            Map map = kit.map;
            if (!Fired)
            {
                Plan(kit, kit.Watch(caster), AimOrigin, AimDir(AimOrigin), 0f, GojoKit.Ground(target) - AimOrigin);
                // The cast job drops a shot whose warmup was called off; this is only a guard.
                return now - startTick < 600;
            }
            // Taken into Hollow Purple: nothing is drawn; kept while Gojo's arm is still out (the cast job asks).
            if (used) return Holds(now);
            if (!Burst)
            {
                MapComponent_GojoKit.BlueWatch watch = kit.Watch(caster);
                Fly(kit, watch, now);
                if (!Burst && !used) Plan(kit, watch, origin, dir, along, GojoKit.Ground(target) - origin);
            }
            if (!Burst) return true;
            TickMoves(map, now);
            return now <= TickAt(EndSeconds) + 1;
        }

        private Vector2 AimOrigin => caster != null && caster.Spawned ? GojoKit.Ground(caster.DrawPos) : GojoKit.Ground(target);

        private Vector2 AimDir(Vector2 from)
        {
            Vector2 to = GojoKit.Ground(target) - from;
            return to.sqrMagnitude > 1e-6f ? to.normalized : Vector2.right;
        }

        // ---- flight ---------------------------------------------------------------------------------------------------

        private void Fly(MapComponent_GojoKit kit, in MapComponent_GojoKit.BlueWatch watch, int now)
        {
            Map map = kit.map;
            float next = Mathf.Min(targetDist, R.Tip + (now - fireTick) / 60f * speed);
            // At the fire tick the walk is the single point at the tip; after that it is this tick's stretch.
            float a = now == fireTick ? along : Mathf.Min(next, along + 0.1f);
            while (true)
            {
                Vector2 p = origin + dir * a;
                IntVec3 c = GojoKit.Cell(p);
                bool entered = c != lastCell;
                if (entered)
                {
                    lastCell = c;
                    if (!c.InBounds(map))
                    {
                        BurstAt(map, a, null, false, now);
                        return;
                    }
                    // A wall stops Red before Blue can take it: Purple never forms through a wall beside Blue.
                    if (GojoKit.Wall(c, map))
                    {
                        // Just short of the face, so the burst point stays in the open cell.
                        BurstAt(map, Mathf.Max(0f, a - 0.1f), null, true, now);
                        return;
                    }
                }
                if (kit.TryHollowPurple(this, p, now, watch)) return;
                if (entered)
                {
                    float at = a;
                    Thing first = MapComponent_GojoKit.PassesThroughBlue(watch, p) ? null : First(c, map, a, out at);
                    if (first != null)
                    {
                        BurstAt(map, at, first, false, now);
                        return;
                    }
                }
                if (a >= next - 1e-4f) break;
                a = Mathf.Min(next, a + 0.1f);
            }
            along = next;
            if (along >= targetDist - 1e-4f) BurstAt(map, targetDist, null, false, now);
        }

        /// <summary>
        /// The pawn (not Gojo) or loose thing in <paramref name="c"/> Red hits: the one furthest back along the line,
        /// a pawn before a thing at the same place. <paramref name="at"/> is where along the line it is (never before
        /// <paramref name="from"/>, where Red entered the cell).
        /// </summary>
        private Thing First(IntVec3 c, Map map, float from, out float at)
        {
            at = from;
            Thing best = null;
            float bestAlong = float.MaxValue;
            List<Thing> things = c.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                bool pawn = thing is Pawn p && p != caster && !p.Dead;
                if (!pawn && !GojoKit.Loose(thing)) continue;
                float on = Vector2.Dot(GojoKit.Ground(thing.DrawPos) - origin, dir) - (pawn ? 0.01f : 0f);
                if (on < bestAlong)
                {
                    best = thing;
                    bestAlong = on;
                }
            }
            if (best != null) at = Mathf.Max(from, bestAlong);
            return best;
        }

        /// <summary>
        /// The picture's guess at the burst: the same walk as the flight from where Red is to the target, flying through
        /// Blue's pull as the flight does but not stopping at Blue itself.
        /// </summary>
        private void Plan(MapComponent_GojoKit kit, in MapComponent_GojoKit.BlueWatch watch, Vector2 from, Vector2 way, float start, Vector2 toTarget)
        {
            Map map = kit.map;
            float end = toTarget.magnitude;
            plannedDist = end;
            plannedThing = false;
            IntVec3 seen = caster != null ? caster.Position : GojoKit.Cell(from);
            for (float a = Mathf.Max(start, R.Tip); a <= end + 1e-4f; a += 0.1f)
            {
                IntVec3 c = GojoKit.Cell(from + way * a);
                if (c == seen) continue;
                seen = c;
                if (!c.InBounds(map)) { plannedDist = a; return; }
                if (GojoKit.Wall(c, map)) { plannedDist = Mathf.Max(0f, a - 0.1f); return; }
                if (MapComponent_GojoKit.PassesThroughBlue(watch, from + way * a)) continue;
                List<Thing> things = c.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                    if ((things[i] is Pawn p && p != caster && !p.Dead) || GojoKit.Loose(things[i]))
                    {
                        plannedDist = Mathf.Max(a, Vector2.Dot(GojoKit.Ground(things[i].DrawPos) - from, way));
                        plannedThing = true;
                        return;
                    }
            }
        }

        public void Use(int now)
        {
            used = true;
            usedTick = now;
        }

        // ---- the burst ------------------------------------------------------------------------------------------------

        private void BurstAt(Map map, float at, Thing first, bool wall, int now)
        {
            burstTick = now;
            burstAlong = at;
            along = at;
            wallBurst = wall;
            Vector2 point = origin + dir * at;
            if (first != null) hit = Move(map, first, dir, throwCells, damagePerCell, slamDamage, R.FlyTime(throwCells, throwCells), true, now);
            IntVec3 centre = GojoKit.Cell(point);
            if (!centre.InBounds(map)) centre = centre.ClampInsideMap(map);
            var near = new List<Thing>();
            int cells = GenRadial.NumCellsInRadius(Mathf.Min(burstRadius + 1.5f, GenRadial.MaxRadialPatternRadius));
            for (int i = 0; i < cells; i++)
            {
                IntVec3 c = centre + GenRadial.RadialPattern[i];
                if (!c.InBounds(map) || (c != centre && !GenSight.LineOfSight(centre, c, map, true))) continue;
                List<Thing> things = c.GetThingList(map);
                for (int j = 0; j < things.Count; j++)
                {
                    Thing thing = things[j];
                    if (thing == first || near.Contains(thing)) continue;
                    if (!(thing is Pawn pawn ? GojoKit.Movable(pawn, caster) : GojoKit.Loose(thing))) continue;
                    if ((GojoKit.Ground(thing.DrawPos) - point).magnitude > burstRadius) continue;
                    near.Add(thing);
                }
            }
            foreach (Thing thing in near)
            {
                Vector2 off = GojoKit.Ground(thing.DrawPos) - point;
                Vector2 way = off.sqrMagnitude > 0.0025f ? off.normalized : dir;
                RedMove move = Move(map, thing, way, pushCells, 0f, 0f, R.PushTime, false, now);
                if (move == null) continue;
                move.damage = pushDamage;
                pushed.Add(move);
            }
        }

        /// <summary>
        /// Moves <paramref name="thing"/> <paramref name="cells"/> along <paramref name="way"/> over
        /// <paramref name="seconds"/> (the thrown thing's time is worked out again for how far it gets). A pawn
        /// Pain's kit holds is not moved; when it is the thing hit it still stops Red, and takes nothing.
        /// </summary>
        private RedMove Move(Map map, Thing thing, Vector2 way, float cells, float perCell, float slam, float seconds, bool thrown, int now)
        {
            if (thing is Pawn pawn && !GojoKit.Movable(pawn, caster)) return null;
            Vector2 from = GojoKit.Ground(thing.DrawPos);
            IntVec3 land = GojoKit.Walk(map, GojoKit.Ground(thing.Position), way, cells, out bool walled, out float face);
            float travel = (land - thing.Position).LengthHorizontal;
            if (thrown) seconds = travel > 0f ? R.FlyTime(cells, travel) : 0f;
            var move = new RedMove
            {
                pawn = thing as Pawn, from = from, way = way, land = land, cells = travel, walled = walled, face = face,
                seconds = seconds, landTick = now + Mathf.Max(0, Mathf.CeilToInt(seconds * 60f)), thrown = thrown,
                damage = travel * perCell + (walled ? slam : 0f),
            };
            if (move.pawn != null)
            {
                if (land != move.pawn.Position && travel > 0f) PawnFlyer_VectorThrown.Throw(move.pawn, land, travel / Mathf.Max(0.05f, seconds));
            }
            else if (land != thing.Position)
            {
                move.item = thing;
                thing.DeSpawn();
            }
            else move.landTick = now;
            return move;
        }

        private void TickMoves(Map map, int now)
        {
            if (hit != null) Land(map, hit, now);
            for (int i = 0; i < pushed.Count; i++) Land(map, pushed[i], now);
        }

        private void Land(Map map, RedMove move, int now)
        {
            if (move.landed || now < move.landTick) return;
            Pawn pawn = move.pawn;
            if (pawn != null)
            {
                // The flyer lands on its own tick; wait for it.
                if (!pawn.Dead && !pawn.Spawned && now < move.landTick + 10) return;
                move.landed = true;
                if (pawn.Dead || !pawn.Spawned || pawn.Map != map || move.damage < 0.5f) return;
                pawn.TakeDamage(new DamageInfo(DamageDefOf.Blunt, move.damage, 0f, GojoKit.Angle(move.way), caster));
                return;
            }
            PutDown(map, move);
        }

        /// <summary>Puts down at once every item still in flight, for a shot being dropped early: none is left off the map.</summary>
        public void PutDownItems(Map map)
        {
            if (hit != null) PutDown(map, hit);
            for (int i = 0; i < pushed.Count; i++) PutDown(map, pushed[i]);
        }

        /// <summary>
        /// An item's landing: near its cell, or on the cell itself when nothing near will take it. The item is off
        /// the map while it flies, so failing to place it would lose it.
        /// </summary>
        private static void PutDown(Map map, RedMove move)
        {
            move.landed = true;
            Thing item = move.item;
            move.item = null;
            if (item == null || item.Destroyed || item.Spawned) return;
            if (!GenPlace.TryPlaceThing(item, move.land, map, ThingPlaceMode.Near))
                GenSpawn.Spawn(item, move.land, map, WipeMode.VanishOrMoveAside);
        }

        /// <summary>Where a moving item is drawn: the picture's decelerating throw, or its push.</summary>
        public Vector2 ItemAt(RedMove move, float age)
        {
            if (age <= 0f) return move.from;
            float gone = move.thrown ? R.Thrown(age, throwCells, move.cells) : move.cells * R.Pushed(age);
            return move.from + move.way * Mathf.Min(move.cells, gone);
        }

        /// <summary>A moving body's ground point now: the pawn (or its flyer), or the item along its way.</summary>
        public Vector2 BodyAt(RedMove move, float age)
        {
            Pawn pawn = move.pawn;
            if (pawn != null)
            {
                if (pawn.Spawned) return GojoKit.Ground(pawn.DrawPos);
                if (pawn.ParentHolder is PawnFlyer flyer && flyer.Spawned) return GojoKit.Ground(flyer.DrawPos);
                return move.Land;
            }
            return move.landed ? move.Land : ItemAt(move, age);
        }

        // ---- the picture --------------------------------------------------------------------------------------------------

        private GojoRedPushed[] pushedScratch = new GojoRedPushed[4];

        public void Draw(Map map)
        {
            if (used) return;
            float s = UbwClock.Since(startTick);
            Vector2 gojo = Fired ? origin : AimOrigin, way = Fired ? dir : AimDir(gojo);
            float age = s - Arrive;
            Shake(s, age);
            if (pushedScratch.Length < pushed.Count) pushedScratch = new GojoRedPushed[pushed.Count];
            int count = 0;
            if (Burst)
                for (int i = 0; i < pushed.Count; i++)
                    pushedScratch[count++] = new GojoRedPushed { From = pushed[i].from, Now = BodyAt(pushed[i], age) };
            RedMove body = hit;
            bool thrown = Burst ? body != null : plannedThing;
            float fly = FlyTime, stop = body?.cells ?? 0f;
            GojoRedGraphics.Draw(new GojoRedShot
            {
                Gojo = gojo, Aim = Mathf.Atan2(way.y, way.x) * Mathf.Rad2Deg, Dist = Dist, Charge = Charge, Speed = speed,
                BurstRadius = burstRadius, WashRadius = R.WashRadius, PushCells = pushCells,
                Thrown = thrown, Body = body != null ? BodyAt(body, age) : gojo + way * Dist, BodyHeight = 0f, Lying = false,
                StopAge = fly, StopCells = stop, HitsWall = body != null && body.walled,
                // The wall's face from the burst point; the body started where Red burst.
                WallFace = body != null ? body.face : 0f, SlamHeight = R.Lift(fly, throwCells),
                TouchAge = R.ThrowTime(throwCells) * 0.5f, TouchCells = throwCells * 0.75f,
                Pushed = pushedScratch, PushedCount = count, Sleeve = GojoKit.Sleeve(caster), Skin = GojoKit.Skin(caster),
            }, s, map);
            if (!Burst) return;
            // A thrown or pushed item is drawn as itself along its way (it is off the map while it moves).
            if (body?.item != null) DrawItem(body, age);
            for (int i = 0; i < pushed.Count; i++)
                if (pushed[i].item != null) DrawItem(pushed[i], age);
        }

        private void DrawItem(RedMove move, float age)
        {
            Vector2 at = ItemAt(move, age);
            move.item.DrawNowAt(new Vector3(at.x, AltitudeLayer.Projectile.AltitudeFor(), at.y));
        }

        /// <summary>The sketch's shakes: the fire, the burst, the slam on a wall. Only for the map on screen.</summary>
        private void Shake(float s, float age)
        {
            if (shaken == 0 && Fired && s >= R.Fire(Charge))
            {
                shaken = 1;
                if (s < R.Fire(Charge) + 0.25f) Find.CameraDriver.shaker.DoShake(R.FireShake);
            }
            if (shaken == 1 && Burst && age >= 0f)
            {
                shaken = 2;
                if (age < 0.25f) Find.CameraDriver.shaker.DoShake(R.BurstShake);
            }
            if (shaken == 2 && hit != null && hit.walled && age >= FlyTime)
            {
                shaken = 3;
                if (age < FlyTime + 0.25f) Find.CameraDriver.shaker.DoShake(R.SlamShake);
            }
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref caster, "caster");
            Scribe_Values.Look(ref target, "target");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref fireTick, "fireTick", -1);
            Scribe_Values.Look(ref expectedCharge, "expectedCharge", R.Charge);
            Scribe_Values.Look(ref origin, "origin");
            Scribe_Values.Look(ref dir, "dir");
            Scribe_Values.Look(ref targetDist, "targetDist");
            Scribe_Values.Look(ref along, "along");
            Scribe_Values.Look(ref burstTick, "burstTick", -1);
            Scribe_Values.Look(ref burstAlong, "burstAlong");
            Scribe_Values.Look(ref wallBurst, "wallBurst");
            Scribe_Values.Look(ref used, "used");
            Scribe_Values.Look(ref usedTick, "usedTick", -1);
            Scribe_Deep.Look(ref hit, "hit");
            Scribe_Collections.Look(ref pushed, "pushed", LookMode.Deep);
            Scribe_Values.Look(ref speed, "speed", R.Speed);
            Scribe_Values.Look(ref throwCells, "throwCells", R.ThrowCells);
            Scribe_Values.Look(ref damagePerCell, "damagePerCell", 1.5f);
            Scribe_Values.Look(ref slamDamage, "slamDamage", 10f);
            Scribe_Values.Look(ref burstRadius, "burstRadius", R.BurstRadius);
            Scribe_Values.Look(ref pushCells, "pushCells", R.PushCells);
            Scribe_Values.Look(ref pushDamage, "pushDamage", 8f);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pushed ??= new List<RedMove>();
                // Red in flight goes on from its saved point; it re-checks the cell it is in.
                lastCell = IntVec3.Invalid;
                if (Fired && !Burst) lastCell = GojoKit.Cell(origin + dir * along);
                shaken = 3;
            }
        }
    }
}
