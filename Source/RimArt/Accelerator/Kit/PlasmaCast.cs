using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>Plasma's numbers (AG_VectorPlasma).</summary>
    public class CompProperties_VectorPlasma : CompProperties_AbilityEffect
    {
        /// <summary>The channel: he stands this long before the release.</summary>
        public int channelTicks = 180;
        /// <summary>During the channel every holdable bullet this close to him is caught and destroyed.</summary>
        public float catchRadius = 8f;
        /// <summary>The lane: cells long and wide; the head crosses the full length in flightSeconds.</summary>
        public float length = 15f, width = 1f, flightSeconds = 0.3f;
        /// <summary>The burst where the head stops.</summary>
        public float burstRadius = 1.5f, damage = 50f, armorPenetration = 0.4f, chanceToStartFire = 1f;
        /// <summary>Brain strain added at the release (not on a broken channel).</summary>
        public float strainCost = 0.45f;

        public CompProperties_VectorPlasma()
        {
            compClass = typeof(CompAbilityEffect_VectorPlasma);
        }
    }

    /// <summary>
    /// Plasma's release: the cast at the end of the channel (<see cref="JobDriver_VectorPlasma"/>). The lane,
    /// the flight and the burst are run by <see cref="MapComponent_Plasma"/>.
    /// </summary>
    public class CompAbilityEffect_VectorPlasma : CompAbilityEffect
    {
        public new CompProperties_VectorPlasma Props => (CompProperties_VectorPlasma)props;

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
            if (caster?.Map == null) return;
            if (caster.Map.GetComponent<MapComponent_Plasma>().Release(caster, Props)) VectorStrain.Add(caster, Props.strainCost);
        }
    }

    /// <summary>
    /// Plasma's cast job: stand for the ability's channelTicks facing the aim, then cast (warmup 0). Its own
    /// channel rather than the verb's warmup, because a warmup is scaled by the aiming delay stat and the
    /// channel is a fixed shield. The JobDef is abilityCasting, so the cooldown starts when the job ends
    /// however it ends: a channel broken by a stun, a downing or an order spends it.
    /// </summary>
    public class JobDriver_VectorPlasma : JobDriver
    {
        private IntVec3 aim;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            job.ability?.Notify_StartedCasting();
        }

        public override string GetReport() => "AG_PlasmaReport".Translate();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => job.ability == null || (!job.ability.CanCast && !job.ability.Casting));
            this.FailOn(() => pawn.Downed || (pawn.stances?.stunner?.Stunned ?? false));
            AddFinishAction(condition =>
            {
                if (job.ability != null && job.def.abilityCasting) job.ability.StartCooldown(job.ability.def.cooldownTicksRange.RandomInRange);
                pawn.MapHeld?.GetComponent<MapComponent_Plasma>()?.Ended(pawn);
            });

            Toil begin = ToilMaker.MakeToil("PlasmaBegin");
            begin.initAction = () =>
            {
                pawn.pather.StopDead();
                // The aim is a direction: the cell as it was ordered, so a target that walks or dies changes nothing.
                aim = job.targetA.Cell;
                job.targetA = aim;
                var props = AcceleratorKit.Props<CompProperties_VectorPlasma>(job.ability.def);
                pawn.Map.GetComponent<MapComponent_Plasma>().Begin(pawn, job, aim, props);
            };
            begin.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return begin;

            Toil channel = ToilMaker.MakeToil("PlasmaChannel");
            channel.initAction = () =>
            {
                var props = AcceleratorKit.Props<CompProperties_VectorPlasma>(job.ability.def);
                ticksLeftThisToil = props?.channelTicks ?? 180;
            };
            channel.tickIntervalAction = delta => pawn.rotationTracker.FaceCell(aim);
            channel.handlingFacing = true;
            channel.defaultCompleteMode = ToilCompleteMode.Delay;
            channel.defaultDuration = 180;
            yield return channel;

            yield return Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref aim, "aim");
        }
    }

    /// <summary>
    /// One Plasma from the channel to the burst's tail. Seconds on its clock start at the channel.
    /// </summary>
    public class PlasmaChannel : IExposable
    {
        public Pawn caster;
        public Job job;
        public IntVec3 from, aim, burstCell;
        public int startTick, releaseTick = -1, cancelTick = -1, hitTick = -1;
        public bool burst;
        /// <summary>Where the head stops, in cells from the caster along the aim; a wall ended it at wallAt.</summary>
        public float stop, wallAt;
        public bool walled;
        public List<Caught> caught = new List<Caught>();
        // The numbers of the ability at cast time.
        public float length, width, flightSeconds, burstRadius, damage, armorPenetration, chanceToStartFire, catchRadius;
        public int channelTicks;

        public struct Caught
        {
            public Vector2 at;
            public int tick;
        }

        public Vector2 Toward
        {
            get
            {
                var run = new Vector2(aim.x - from.x, aim.z - from.z);
                return run.sqrMagnitude < 0.01f ? Vector2.right : run.normalized;
            }
        }

        public float AimDegrees => Mathf.Atan2(Toward.y, Toward.x) * Mathf.Rad2Deg;
        public bool Channelling => releaseTick < 0 && cancelTick < 0;

        public void ExposeData()
        {
            Scribe_References.Look(ref caster, "caster");
            Scribe_References.Look(ref job, "job");
            Scribe_Values.Look(ref from, "from");
            Scribe_Values.Look(ref aim, "aim");
            Scribe_Values.Look(ref burstCell, "burstCell");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref releaseTick, "releaseTick", -1);
            Scribe_Values.Look(ref cancelTick, "cancelTick", -1);
            Scribe_Values.Look(ref hitTick, "hitTick", -1);
            Scribe_Values.Look(ref burst, "burst");
            Scribe_Values.Look(ref stop, "stop");
            Scribe_Values.Look(ref wallAt, "wallAt");
            Scribe_Values.Look(ref walled, "walled");
            Scribe_Values.Look(ref length, "length");
            Scribe_Values.Look(ref width, "width");
            Scribe_Values.Look(ref flightSeconds, "flightSeconds");
            Scribe_Values.Look(ref burstRadius, "burstRadius");
            Scribe_Values.Look(ref damage, "damage");
            Scribe_Values.Look(ref armorPenetration, "armorPenetration");
            Scribe_Values.Look(ref chanceToStartFire, "chanceToStartFire");
            Scribe_Values.Look(ref catchRadius, "catchRadius");
            Scribe_Values.Look(ref channelTicks, "channelTicks");
            // The caught rounds are picture only and are not saved.
            if (Scribe.mode == LoadSaveMode.PostLoadInit) caught = new List<Caught>();
        }
    }

    /// <summary>
    /// Runs Plasma on a map. While a channel runs it catches bullets and checks the channel still holds; at
    /// the release it works out where the head stops (the first standing pawn or wall in the lane, else the
    /// lane's end), and when the head gets there it bursts. The clock is game ticks.
    /// </summary>
    public class MapComponent_Plasma : MapComponent
    {
        /// <summary>After the burst the channel is kept this long for the picture's tail.</summary>
        public const int TailTicks = 150;

        private List<PlasmaChannel> channels = new List<PlasmaChannel>();

        public IReadOnlyList<PlasmaChannel> Channels => channels;

        public MapComponent_Plasma(Map map) : base(map) { }

        public PlasmaChannel For(Pawn caster)
        {
            for (int i = channels.Count - 1; i >= 0; i--)
                if (channels[i].caster == caster) return channels[i];
            return null;
        }

        public void Begin(Pawn caster, Job job, IntVec3 aim, CompProperties_VectorPlasma props)
        {
            props = props ?? new CompProperties_VectorPlasma();
            channels.RemoveAll(c => c.caster == caster && c.Channelling);
            var channel = new PlasmaChannel
            {
                caster = caster, job = job, from = caster.Position, aim = aim, startTick = Find.TickManager.TicksGame,
                length = props.length, width = props.width, flightSeconds = props.flightSeconds, burstRadius = props.burstRadius,
                damage = props.damage, armorPenetration = props.armorPenetration, chanceToStartFire = props.chanceToStartFire,
                catchRadius = props.catchRadius, channelTicks = props.channelTicks,
            };
            Lane(channel);
            channels.Add(channel);
        }

        /// <summary>The job ended: a channel that was not released is broken.</summary>
        public void Ended(Pawn caster)
        {
            PlasmaChannel channel = For(caster);
            if (channel != null && channel.Channelling) channel.cancelTick = Find.TickManager.TicksGame;
        }

        /// <summary>The cast at the end of the channel. False when there is no channel to release.</summary>
        public bool Release(Pawn caster, CompProperties_VectorPlasma props)
        {
            PlasmaChannel channel = For(caster);
            if (channel == null || !channel.Channelling) return false;
            int now = Find.TickManager.TicksGame;
            channel.releaseTick = now;
            Lane(channel);
            channel.hitTick = now + Mathf.Max(1, Mathf.RoundToInt(channel.flightSeconds * channel.stop / Mathf.Max(0.5f, channel.length) * 60f));
            if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(0.07f);
            return true;
        }

        /// <summary>
        /// Where the head stops now: half a cell short of the first filled cell (a wall, a rock, a closed door),
        /// or 0.4 cells short of the first standing pawn but the caster, or the lane's full length.
        /// </summary>
        private void Lane(PlasmaChannel c)
        {
            Vector2 toward = c.Toward;
            Vector3 origin = c.from.ToVector3Shifted();
            c.walled = false;
            c.stop = c.length;
            c.burstCell = (origin + new Vector3(toward.x, 0f, toward.y) * c.length).ToIntVec3();
            IntVec3 last = c.from;
            for (float d = 0.5f; d <= c.length + 0.01f; d += 0.5f)
            {
                IntVec3 cell = (origin + new Vector3(toward.x, 0f, toward.y) * d).ToIntVec3();
                if (cell == last) continue;
                float along = (cell.x - c.from.x) * toward.x + (cell.z - c.from.z) * toward.y;
                if (!cell.InBounds(map))
                {
                    c.stop = Mathf.Max(0.5f, along - 0.5f);
                    c.burstCell = last;
                    return;
                }
                Building edifice = cell.GetEdifice(map);
                if (cell.Filled(map) || (edifice is Building_Door door && !door.Open) || (edifice != null && cell.Impassable(map)))
                {
                    c.walled = true;
                    c.wallAt = along;
                    c.stop = Mathf.Max(0.5f, along - 0.5f);
                    c.burstCell = last;
                    return;
                }
                List<Thing> here = cell.GetThingList(map);
                for (int i = 0; i < here.Count; i++)
                {
                    if (here[i] is Pawn pawn && pawn != c.caster && !pawn.Dead && !pawn.Downed)
                    {
                        c.stop = Mathf.Max(0.5f, along - 0.4f);
                        c.burstCell = cell;
                        return;
                    }
                }
                last = cell;
            }
        }

        public override void MapComponentTick()
        {
            if (channels.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            for (int i = channels.Count - 1; i >= 0; i--)
            {
                PlasmaChannel c = channels[i];
                if (c.Channelling) TickChannel(c, now);
                else if (c.releaseTick >= 0 && !c.burst && now >= c.hitTick) Burst(c);
                bool over = c.cancelTick >= 0 ? now - c.cancelTick > 30 : c.burst && now - c.hitTick > TailTicks;
                if (over) channels.RemoveAt(i);
            }
        }

        private void TickChannel(PlasmaChannel c, int now)
        {
            Pawn caster = c.caster;
            bool held = caster != null && caster.Spawned && caster.Map == map && !caster.Dead && !caster.Downed
                && !(caster.stances?.stunner?.Stunned ?? false) && caster.CurJob == c.job && caster.Position == c.from;
            if (!held)
            {
                c.cancelTick = now;
                if (caster != null && caster.CurJob == c.job && caster.jobs != null) caster.jobs.EndCurrentJob(JobCondition.InterruptForced);
                return;
            }
            if ((now - c.startTick) % 15 == 0) Lane(c);
            Catch(c, now);
        }

        private static readonly List<Thing> scratch = new List<Thing>();

        /// <summary>Every holdable bullet within catchRadius of him is bent into the hand: destroyed now, drawn spiralling in.</summary>
        private void Catch(PlasmaChannel c, int now)
        {
            List<Thing> rounds = map.listerThings.ThingsInGroup(ThingRequestGroup.Projectile);
            if (rounds.Count == 0) return;
            scratch.Clear();
            Vector3 eye = c.caster.DrawPos;
            float r2 = c.catchRadius * c.catchRadius;
            for (int i = 0; i < rounds.Count; i++)
            {
                Thing round = rounds[i];
                if (!AcceleratorKit.Holdable(round)) continue;
                if (round is Projectile projectile && projectile.Launcher == c.caster) continue;
                Vector3 at = Rounds.For(round).Position(round);
                float dx = at.x - eye.x, dz = at.z - eye.z;
                if (dx * dx + dz * dz > r2) continue;
                scratch.Add(round);
            }
            for (int i = 0; i < scratch.Count; i++)
            {
                Thing round = scratch[i];
                Vector3 at = Rounds.For(round).Position(round);
                c.caught.Add(new PlasmaChannel.Caught { at = new Vector2(at.x, at.z), tick = now });
                round.Destroy(DestroyMode.Vanish);
            }
        }

        private void Burst(PlasmaChannel c)
        {
            c.burst = true;
            if (!c.burstCell.InBounds(map)) return;
            var ignored = new List<Thing>();
            if (c.caster != null) ignored.Add(c.caster);
            GenExplosion.DoExplosion(c.burstCell, map, c.burstRadius, DamageDefOf.Flame, c.caster, Mathf.RoundToInt(c.damage), c.armorPenetration,
                chanceToStartFire: c.chanceToStartFire, ignoredThings: ignored, doVisualEffects: false, screenShakeFactor: 0f);
            if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(0.14f);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref channels, "channels", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (channels == null) channels = new List<PlasmaChannel>();
                channels.RemoveAll(c => c.caster == null);
            }
        }
    }
}
