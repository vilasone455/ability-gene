using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Line = RimArt.PaperBombTagLineTiming;
using Wrap = RimArt.PaperBombShroudTiming;
using Pin = RimArt.PaperBombTagThrowTiming;

namespace RimArt
{
    /// <summary>
    /// Everything the Paper Bomb kit has out on a map: thrown tags in flight or burning, laid tag
    /// lines, and shrouds on their way to going off. It plays each one's picture and does what the
    /// picture shows at the moment it shows it. All of it is saved, because a laid line lasts a day.
    /// The clock is game ticks, so pictures pause and speed up with the game.
    ///
    /// Left out on purpose for now: a thrown tag is not intercepted on its way, and rain and fire do
    /// nothing to a laid line (docs/paper-bomb-kit.md says they should).
    /// </summary>
    public class MapComponent_PaperBomb : MapComponent
    {
        private sealed class Thrown : IExposable
        {
            public Pawn caster;
            public Vector2 feet, toward, anchor;
            public float distance, fuse, radius, buildingFactor;
            public int damage, castTick;
            public LocalTargetInfo flyTo;
            public bool landed, burst;
            public TagThrowTarget kind;
            public Thing stuckIn;

            public float Seconds(int now) => Pin.Release + (now - castTick) / 60f;

            public void ExposeData()
            {
                Scribe_References.Look(ref caster, "caster");
                Scribe_References.Look(ref stuckIn, "stuckIn");
                Scribe_TargetInfo.Look(ref flyTo, "flyTo");
                Scribe_Values.Look(ref feet, "feet");
                Scribe_Values.Look(ref toward, "toward");
                Scribe_Values.Look(ref anchor, "anchor");
                Scribe_Values.Look(ref distance, "distance");
                Scribe_Values.Look(ref fuse, "fuse");
                Scribe_Values.Look(ref radius, "radius");
                Scribe_Values.Look(ref buildingFactor, "buildingFactor", 1f);
                Scribe_Values.Look(ref damage, "damage");
                Scribe_Values.Look(ref castTick, "castTick");
                Scribe_Values.Look(ref landed, "landed");
                Scribe_Values.Look(ref burst, "burst");
                Scribe_Values.Look(ref kind, "kind");
            }
        }

        private sealed class Laid : IExposable
        {
            public Pawn caster;
            public Vector2 feet, toward;
            public int tags, damage, castTick, expireTick, fuseTick = -1, burstMask;
            public float perTag, radius, fuseFrom;
            public bool tripwire, byHand;

            public float Seconds(int now) => (now - castTick) / 60f;
            public bool Down(int now) => Seconds(now) >= Line.Lay;
            public bool Fusing => fuseTick >= 0;
            public IntVec3 Cell(int index)
            {
                Vector2 at = feet + toward * Line.TagAt(index);
                return new Vector3(at.x, 0f, at.y).ToIntVec3();
            }

            public void ExposeData()
            {
                Scribe_References.Look(ref caster, "caster");
                Scribe_Values.Look(ref feet, "feet");
                Scribe_Values.Look(ref toward, "toward");
                Scribe_Values.Look(ref tags, "tags");
                Scribe_Values.Look(ref damage, "damage");
                Scribe_Values.Look(ref castTick, "castTick");
                Scribe_Values.Look(ref expireTick, "expireTick");
                Scribe_Values.Look(ref fuseTick, "fuseTick", -1);
                Scribe_Values.Look(ref burstMask, "burstMask");
                Scribe_Values.Look(ref perTag, "perTag", 0.1f);
                Scribe_Values.Look(ref radius, "radius");
                Scribe_Values.Look(ref fuseFrom, "fuseFrom");
                Scribe_Values.Look(ref tripwire, "tripwire");
                Scribe_Values.Look(ref byHand, "byHand");
            }
        }

        private sealed class Wrapped : IExposable
        {
            public Pawn caster, victim;
            public Vector2 feet, at;
            public float held, radius;
            public int damage, targetDamage, castTick;
            public bool stunned, burst;

            public float Seconds(int now) => Wrap.Wind + (now - castTick) / 60f;

            public void ExposeData()
            {
                Scribe_References.Look(ref caster, "caster");
                Scribe_References.Look(ref victim, "victim");
                Scribe_Values.Look(ref feet, "feet");
                Scribe_Values.Look(ref at, "at");
                Scribe_Values.Look(ref held, "held");
                Scribe_Values.Look(ref radius, "radius");
                Scribe_Values.Look(ref damage, "damage");
                Scribe_Values.Look(ref targetDamage, "targetDamage");
                Scribe_Values.Look(ref castTick, "castTick");
                Scribe_Values.Look(ref stunned, "stunned");
                Scribe_Values.Look(ref burst, "burst");
            }
        }

        /// <summary>How often a tripwire line looks for a hostile standing on a tag.</summary>
        private const int TripwireInterval = 15;

        private List<Thrown> thrown = new List<Thrown>();
        private List<Laid> lines = new List<Laid>();
        private List<Wrapped> shrouds = new List<Wrapped>();
        /// <summary>
        /// Casters whose throw, line or shroud has left the hand in a cast job that has not ended yet
        /// (<see cref="Fired"/>). Not saved: a game loaded during the hold ends that job, as before.
        /// </summary>
        private readonly HashSet<Pawn> fired = new HashSet<Pawn>();

        public MapComponent_PaperBomb(Map map) : base(map) { }

        public void Throw(Pawn caster, Vector2 feet, LocalTargetInfo flyTo, CompProperties_TagThrow props)
        {
            Vector2 run = Ground(flyTo.Cell) - feet;
            float distance = Mathf.Max(0.5f, run.magnitude);
            thrown.Add(new Thrown
            {
                caster = caster, feet = feet, toward = run / distance, distance = distance, anchor = Ground(flyTo.Cell), flyTo = flyTo,
                fuse = props.fuseSeconds, radius = props.radius, damage = props.damage, buildingFactor = props.stuckBuildingFactor,
                castTick = Find.TickManager.TicksGame,
            });
            fired.Add(caster);
        }

        public void Lay(Pawn caster, Vector2 feet, Vector2 toward, int tags, bool tripwire, float warmup, CompProperties_TagLine props)
        {
            int now = Find.TickManager.TicksGame;
            lines.Add(new Laid
            {
                caster = caster, feet = feet, toward = toward, tags = tags, tripwire = tripwire, damage = props.damage, radius = props.radius,
                perTag = props.perTagSeconds, castTick = now - Mathf.RoundToInt(warmup * 60f), expireTick = now + props.lifetimeTicks,
            });
            fired.Add(caster);
        }

        public void Shroud(Pawn caster, Vector2 feet, Pawn victim, CompProperties_Shroud props)
        {
            shrouds.Add(new Wrapped
            {
                caster = caster, victim = victim, feet = feet, at = Ground(victim), held = props.heldSeconds, radius = props.radius,
                damage = props.damage, targetDamage = props.targetDamage, castTick = Find.TickManager.TicksGame,
            });
            fired.Add(caster);
        }

        /// <summary>Whether the caster's tag has left the hand in the cast job still running: the job holds from here (CastJobFail).</summary>
        public bool Fired(Pawn caster) => fired.Contains(caster);

        /// <summary>The caster's cast job is over.</summary>
        public void Ended(Pawn caster) => fired.Remove(caster);

        public bool HasArmedLine(Pawn caster)
        {
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].caster == caster && !lines[i].Fusing && lines[i].Down(now)) return true;
            return false;
        }

        /// <summary>The hand seal: every line of this caster that is down and not burning is lit at the caster's end.</summary>
        public void Detonate(Pawn caster)
        {
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < lines.Count; i++)
            {
                Laid line = lines[i];
                if (line.caster != caster || line.Fusing || !line.Down(now)) continue;
                line.fuseTick = now;
                line.fuseFrom = Line.Start;
                line.byHand = true;
            }
        }

        /// <summary>The scroll's toggle changes the lines its holder has already laid, on every map.</summary>
        public static void SetTripwire(Pawn caster, bool tripwire)
        {
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                MapComponent_PaperBomb comp = maps[m].GetComponent<MapComponent_PaperBomb>();
                if (comp == null) continue;
                for (int i = 0; i < comp.lines.Count; i++)
                    if (comp.lines[i].caster == caster && !comp.lines[i].Fusing) comp.lines[i].tripwire = tripwire;
            }
        }

        private static Vector2 Ground(IntVec3 cell) => new Vector2(cell.x + 0.5f, cell.z + 0.5f);

        private static Vector2 Ground(Thing thing)
        {
            Vector3 stands = thing.DrawPos;
            return new Vector2(stands.x, stands.z);
        }

        private static IntVec3 CellOf(Vector2 ground) => new Vector3(ground.x, 0f, ground.y).ToIntVec3();

        public override void MapComponentTick()
        {
            if (thrown.Count == 0 && lines.Count == 0 && shrouds.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            for (int i = thrown.Count - 1; i >= 0; i--) if (!Tick(thrown[i], now)) thrown.RemoveAt(i);
            for (int i = lines.Count - 1; i >= 0; i--) if (!Tick(lines[i], now)) lines.RemoveAt(i);
            for (int i = shrouds.Count - 1; i >= 0; i--) if (!Tick(shrouds[i], now)) shrouds.RemoveAt(i);
        }

        private bool Tick(Thrown tag, int now)
        {
            float seconds = tag.Seconds(now);
            if (!tag.landed && seconds >= Pin.LandAt)
            {
                tag.landed = true;
                Thing hit = tag.flyTo.Thing;
                IntVec3 cell = tag.flyTo.Cell;
                if (hit is Pawn pawn && pawn.Spawned && pawn.Map == map && pawn.Position.DistanceTo(cell) <= 1.5f)
                {
                    tag.kind = TagThrowTarget.Pawn;
                    tag.stuckIn = pawn;
                }
                else
                {
                    Building solid = cell.InBounds(map) ? cell.GetEdifice(map) : null;
                    if (solid != null && solid.def.Fillage == FillCategory.Full)
                    {
                        tag.kind = TagThrowTarget.Wall;
                        tag.stuckIn = solid;
                    }
                    else tag.kind = TagThrowTarget.Floor;
                }
            }
            if (tag.landed && !tag.burst)
            {
                // A carried tag follows its carrier; if the carrier is gone it lies where they last were.
                if (tag.kind == TagThrowTarget.Pawn && tag.stuckIn != null && tag.stuckIn.Spawned && tag.stuckIn.Map == map) tag.anchor = Ground(tag.stuckIn);
                if (seconds >= Pin.BurstAt(tag.fuse))
                {
                    tag.burst = true;
                    if (tag.kind == TagThrowTarget.Wall && tag.stuckIn != null && !tag.stuckIn.Destroyed && tag.buildingFactor > 1f)
                        tag.stuckIn.TakeDamage(new DamageInfo(DamageDefOf.Bomb, tag.damage * (tag.buildingFactor - 1f), 0f, -1f, tag.caster));
                    Explode(tag.anchor, tag.radius, tag.damage, tag.caster, Pin.BurstShake);
                }
            }
            return seconds < Pin.Duration(tag.fuse);
        }

        private bool Tick(Laid line, int now)
        {
            if (!line.Fusing)
            {
                if (now >= line.expireTick) return false;
                if (line.tripwire && line.Down(now) && now % TripwireInterval == 0)
                    for (int i = 0; i < line.tags; i++)
                    {
                        if (!Stepped(line.Cell(i), line.caster)) continue;
                        line.fuseTick = now;
                        line.fuseFrom = Line.TagAt(i);
                        break;
                    }
                return true;
            }
            float fuseSeconds = (now - line.fuseTick) / 60f;
            for (int i = 0; i < line.tags; i++)
            {
                if ((line.burstMask & (1 << i)) != 0 || fuseSeconds < Line.BurstAt(i, line.fuseFrom, line.perTag)) continue;
                line.burstMask |= 1 << i;
                Explode(line.feet + line.toward * Line.TagAt(i), line.radius, line.damage, line.caster, Line.BurstShake);
            }
            return fuseSeconds < Line.LastBurst(line.tags, line.fuseFrom, line.perTag) + Line.Aftermath;
        }

        /// <summary>A pawn hostile to the line's owner stands on this cell.</summary>
        private bool Stepped(IntVec3 cell, Pawn owner)
        {
            if (!cell.InBounds(map) || owner == null) return false;
            List<Thing> things = cell.GetThingList(map);
            for (int t = 0; t < things.Count; t++)
                if (things[t] is Pawn pawn && !pawn.Dead && !pawn.Downed && pawn.HostileTo(owner)) return true;
            return false;
        }

        private bool Tick(Wrapped shroud, int now)
        {
            float seconds = shroud.Seconds(now);
            Pawn victim = shroud.victim;
            bool there = victim != null && victim.Spawned && victim.Map == map;
            if (there && !shroud.burst) shroud.at = Ground(victim);
            if (!shroud.stunned && seconds >= Wrap.FirstLand)
            {
                shroud.stunned = true;
                if (there && !victim.Dead) victim.stances?.stunner?.StunFor(Mathf.RoundToInt(shroud.held * 60f), shroud.caster, false);
            }
            if (!shroud.burst && seconds >= Wrap.BurstAt(shroud.held))
            {
                shroud.burst = true;
                if (there && !victim.Dead && shroud.targetDamage > shroud.damage)
                    victim.TakeDamage(new DamageInfo(DamageDefOf.Bomb, shroud.targetDamage - shroud.damage, 0f, -1f, shroud.caster));
                Explode(shroud.at, shroud.radius, shroud.damage, shroud.caster, Wrap.BurstShake);
            }
            return seconds < Wrap.Duration(shroud.held);
        }

        private void Explode(Vector2 ground, float radius, int damage, Thing instigator, float shake)
        {
            IntVec3 cell = CellOf(ground);
            if (!cell.InBounds(map)) return;
            if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(shake);
            GenExplosion.DoExplosion(cell, map, radius, DamageDefOf.Bomb, instigator, damage);
        }

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map || (thrown.Count == 0 && lines.Count == 0 && shrouds.Count == 0)) return;
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < thrown.Count; i++)
            {
                Thrown tag = thrown[i];
                PaperBombTagThrowGraphics.Draw(tag.feet, tag.toward, tag.distance, tag.landed ? tag.kind : TagThrowTarget.Floor, tag.anchor, tag.fuse, tag.radius, tag.Seconds(now), map);
            }
            for (int i = 0; i < lines.Count; i++)
            {
                Laid line = lines[i];
                var shot = new TagLineShot
                {
                    Tags = line.tags, Seconds = line.Seconds(now), FuseSeconds = line.Fusing ? (now - line.fuseTick) / 60f : -1f,
                    FuseFrom = line.fuseFrom, PerTag = line.perTag, Radius = line.radius, Sealed = line.byHand,
                };
                PaperBombTagLineGraphics.Draw(line.feet, line.toward, shot, map);
            }
            for (int i = 0; i < shrouds.Count; i++)
            {
                Wrapped shroud = shrouds[i];
                PaperBombShroudGraphics.Draw(shroud.feet, shroud.at, shroud.held, shroud.radius, shroud.Seconds(now), map);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref thrown, "thrownTags", LookMode.Deep);
            Scribe_Collections.Look(ref lines, "tagLines", LookMode.Deep);
            Scribe_Collections.Look(ref shrouds, "shrouds", LookMode.Deep);
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            if (thrown == null) thrown = new List<Thrown>();
            if (lines == null) lines = new List<Laid>();
            if (shrouds == null) shrouds = new List<Wrapped>();
        }
    }
}
