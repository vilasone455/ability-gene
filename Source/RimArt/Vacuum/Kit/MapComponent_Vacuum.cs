using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using Digest = RimArt.VacuumDigestTiming;
using Spit = RimArt.VacuumSpitTiming;
using Suck = RimArt.VacuumSuckTiming;

namespace RimArt
{
    public enum VacuumCastKind { Suck, Spit, Digest }

    /// <summary>
    /// Everything the Vacuum does on a map:
    /// - plays Suck's, Spit's and Digest's pictures for real casts and applies each effect when the
    ///   picture shows it: a thing leaves the ground (or a pawn's hands) as it lifts off and is
    ///   swallowed as it enters the head; the spat thing hits and lands at the impact; the stomach
    ///   drains over the chew;
    /// - once a second, finds who holds a vacuum, finishes flights a load cut short and keeps each
    ///   holder's slow in step with the kg inside.
    ///
    /// A cast is known from the moment its warmup starts (<see cref="Begin"/>, from
    /// JobDriver_CastVacuum), because the canister rises during the warmup. The ability's comp then
    /// says what the cast does (<see cref="LandSuck"/>, <see cref="LandSpit"/>, <see cref="LandDigest"/>).
    /// The clock is game ticks. Nothing about a cast is saved: a game loaded mid-cast loses the rest of
    /// its picture and any hit not yet dealt. The contents are saved on the vacuum, including things
    /// in flight (CompVacuum.Settle swallows those after a load).
    /// </summary>
    public class MapComponent_Vacuum : MapComponent
    {
        private sealed class Cast
        {
            public Pawn caster;
            public CompVacuum vacuum;
            public VacuumCastKind kind;
            public int startTick, warmupTicks;
            public bool landed;
            /// <summary>The canister is back in the floor: Core's held weapon is drawn again and the caster may go.</summary>
            public bool home;
            /// <summary>Picture kg per real kg: the picture's canister is 100 kg whatever the XML capacity.</summary>
            public float scale;

            public VacuumSuckShot suck;
            public List<VacuumSuckTargets.Taken> taken;
            public bool[] started, arrived;
            public Pawn disarmed;

            public VacuumSpitShot spit;
            public CompProperties_VacuumSpit spitProps;
            public Thing spat;
            public Pawn target;
            public IntVec3 aimedCell;
            public bool launched, impacted;

            public VacuumDigestShot digest;
            public float digestKg;
            public bool burped;
            public int bites;

            public float Seconds(int now) => VacuumGraphics.Lead + (now - startTick) / 60f;

            /// <summary>When the ability fires: the picture holds just before it until then.</summary>
            public float Fire => kind == VacuumCastKind.Suck ? Suck.Pull0 : kind == VacuumCastKind.Spit ? Spit.Heave : Digest.Chew0;

            public float Home => kind == VacuumCastKind.Suck ? Suck.Home(suck.Things.Count, suck.Hold)
                : kind == VacuumCastKind.Spit ? Spit.Home(spit.Hold) : Digest.Home(digest);

            public float End => kind == VacuumCastKind.Suck ? Suck.End(suck)
                : kind == VacuumCastKind.Spit ? Spit.End(spit.Hold) : Digest.End(digest);
        }

        /// <summary>A cast whose warmup ran out this long ago without landing was interrupted.</summary>
        private const float Overdue = 0.25f;
        /// <summary>Pawns whose weapon a cast's picture is drawing, on any map: Core's held vacuum is not drawn for them.</summary>
        private static readonly HashSet<Pawn> casting = new HashSet<Pawn>();

        private readonly List<Cast> casts = new List<Cast>();
        private readonly List<Pawn> holders = new List<Pawn>();

        /// <summary>The last Spit hit on this map: who, and the damage it was dealt before armour. For the game tests.</summary>
        internal Pawn lastSpitVictim;
        internal float lastSpitDamage = -1f;

        public MapComponent_Vacuum(Map map) : base(map) { }

        public static bool IsCasting(Pawn pawn) => pawn != null && casting.Contains(pawn);

        public void Register(Pawn pawn)
        {
            if (pawn != null && !holders.Contains(pawn)) holders.Add(pawn);
        }

        /// <summary>Whether a cast on this map is still carrying things for this vacuum.</summary>
        public bool CastingWith(CompVacuum vacuum)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].vacuum == vacuum) return true;
            return false;
        }

        private static Vector2 Ground(Thing thing)
        {
            Vector3 at = thing.DrawPos;
            return new Vector2(at.x, at.z);
        }

        private static Vector2 Ground(IntVec3 cell) => new Vector2(cell.x + 0.5f, cell.z + 0.5f);

        private static IntVec3 CellOf(Vector2 at) => new IntVec3(Mathf.FloorToInt(at.x), 0, Mathf.FloorToInt(at.y));

        private static float Degrees(Vector2 run) => run.sqrMagnitude < 0.0001f ? 0f : Mathf.Atan2(run.y, run.x) * Mathf.Rad2Deg;

        /// <summary>A pawn's facing as the pictures' degrees: 0 east, 90 north.</summary>
        private static float Facing(Pawn pawn) => 90f - pawn.Rotation.AsAngle;

        /// <summary>How a thing is drawn in flight: its own material, a lump for a corpse, flecks of its colour for filth.</summary>
        private static VacuumThing Picture(Thing thing, float kg, float scale)
        {
            var p = new VacuumThing { Ground = Ground(thing), Kg = kg * scale, Kind = VacuumThingKind.Lump };
            if (thing is Filth)
            {
                p.Kind = VacuumThingKind.Filth;
                Color c = thing.Graphic?.Color ?? VacuumGraphics.Blood;
                p.Colour = new Color(c.r, c.g, c.b, 1f);
                return p;
            }
            if (thing is Corpse || thing.Graphic == null) return p;
            Material mat = thing.Graphic.MatSingleFor(thing);
            if (mat == null || mat == BaseContent.BadMat) return p;
            p.Kind = VacuumThingKind.Real;
            p.Mat = mat;
            p.Colour = mat.color;
            p.Size = thing.Graphic.drawSize;
            p.Spin = thing.def.IsWeapon;
            return p;
        }

        // ------------------------------------------------------------------ casts

        /// <summary>A cast's warmup has begun: the canister rises toward the target.</summary>
        public void Begin(Pawn caster, Ability ability, LocalTargetInfo target)
        {
            Ended(caster);
            CompVacuum vacuum = CompVacuum.HeldBy(caster);
            if (caster == null || ability == null || vacuum == null) return;
            VacuumCastKind kind = ability.CompOfType<CompAbilityEffect_VacuumSuck>() != null ? VacuumCastKind.Suck
                : ability.CompOfType<CompAbilityEffect_VacuumSpit>() != null ? VacuumCastKind.Spit : VacuumCastKind.Digest;
            if (kind != VacuumCastKind.Digest && !target.IsValid) return;
            Add(caster, vacuum, kind, target, Find.TickManager.TicksGame, ability.def.verbProperties.warmupTime);
        }

        private Cast Add(Pawn caster, CompVacuum vacuum, VacuumCastKind kind, LocalTargetInfo target, int startTick, float warmup)
        {
            Vector2 feet = Ground(caster);
            var cast = new Cast
            {
                caster = caster, vacuum = vacuum, kind = kind, startTick = startTick, warmupTicks = Mathf.RoundToInt(warmup * 60f),
                scale = vacuum.DrawnScale,
            };
            float inside = vacuum.DrawnKg;
            switch (kind)
            {
                case VacuumCastKind.Suck:
                    cast.suck = new VacuumSuckShot { Caster = feet, Target = Ground(target.Cell), Inside = inside, Hold = Suck.GameHold };
                    break;
                case VacuumCastKind.Spit:
                    Vector2 to = target.Thing is Pawn pawn ? Ground(pawn) : Ground(target.Cell);
                    cast.spit = new VacuumSpitShot { Caster = feet, Target = to, Rest = to, Inside = inside, Hold = Spit.GameHold };
                    if (vacuum.Mouth != null) cast.spit.Thing = Picture(vacuum.Mouth, vacuum.MouthKg, cast.scale);
                    break;
                default:
                    cast.digest = new VacuumDigestShot { Caster = feet, Aim = Facing(caster), Kg = inside, Chew = 1f, Hold = Digest.GameHold };
                    break;
            }
            casts.Add(cast);
            casting.Add(caster);
            return cast;
        }

        /// <summary>The cast that has begun and not landed, or a new one for a cast that was never begun.</summary>
        private Cast Landing(Pawn caster, VacuumCastKind kind, LocalTargetInfo target)
        {
            Cast cast = casts.Find(c => c.caster == caster && c.kind == kind && !c.landed);
            if (cast == null)
            {
                CompVacuum vacuum = CompVacuum.HeldBy(caster);
                if (vacuum == null) return null;
                float warmup = kind == VacuumCastKind.Suck ? Suck.Windup : kind == VacuumCastKind.Spit ? Spit.Windup : Digest.Chew0 - Digest.Rise0;
                cast = Add(caster, vacuum, kind, target, Find.TickManager.TicksGame - Mathf.RoundToInt(warmup * 60f), warmup);
            }
            // The clock is put on the ability's fire time, whatever the warmup was, so the picture's pull
            // (or heave, or chew) starts now.
            cast.startTick = Find.TickManager.TicksGame - Mathf.RoundToInt((cast.Fire - VacuumGraphics.Lead) * 60f);
            cast.landed = true;
            return cast;
        }

        public void LandSuck(Pawn caster, IntVec3 cell, CompProperties_VacuumSuck props)
        {
            Cast cast = Landing(caster, VacuumCastKind.Suck, cell);
            if (cast == null) return;
            CompVacuum vacuum = cast.vacuum;
            cast.taken = new List<VacuumSuckTargets.Taken>(VacuumSuckTargets.Find(caster, cell, props, vacuum.Fullness, vacuum.Props.capacityKg));
            VacuumSuckShot shot = cast.suck;
            shot.Target = Ground(cell);
            shot.Radius = props.radius;
            shot.Inside = vacuum.DrawnKg;
            shot.Things.Clear();
            for (int i = 0; i < cast.taken.Count; i++)
            {
                VacuumSuckTargets.Taken t = cast.taken[i];
                VacuumThing picture = Picture(t.thing, t.kg, cast.scale);
                if (t.from != null)
                {
                    // The weapon leaves from the pawn's hands, at hand height, as the sketch's rifle does.
                    picture.Ground = Ground(t.from) + new Vector2(0.25f, 0.02f);
                    picture.H = VacuumGraphics.HandH;
                    shot.Disarmed = i;
                    cast.disarmed = t.from;
                }
                shot.Things.Add(picture);
            }
            cast.started = new bool[cast.taken.Count];
            cast.arrived = new bool[cast.taken.Count];
        }

        public void LandSpit(Pawn caster, LocalTargetInfo target, CompProperties_VacuumSpit props)
        {
            Cast cast = Landing(caster, VacuumCastKind.Spit, target);
            if (cast == null) return;
            CompVacuum vacuum = cast.vacuum;
            cast.spitProps = props;
            cast.spat = vacuum.Mouth;
            cast.target = target.Thing as Pawn;
            cast.aimedCell = target.Cell;
            VacuumSpitShot shot = cast.spit;
            shot.Inside = vacuum.DrawnKg;
            shot.Target = cast.target != null && cast.target.Spawned ? Ground(cast.target) : Ground(target.Cell);
            shot.Thing = Picture(cast.spat, vacuum.MouthKg, cast.scale);
            shot.Dazed = props.stunSeconds;
            // Who the thing is going to hit, as far as can be told now: it lands beside that pawn.
            Pawn victim = Victim(cast);
            shot.HitPawn = victim != null;
            shot.Rest = victim != null ? new VacuumFrame(Degrees(shot.Target - shot.Caster), Vector2.zero).Place(shot.Target, 0.35f, -0.55f) : shot.Target;
        }

        public void LandDigest(Pawn caster, CompProperties_VacuumDigest props)
        {
            Cast cast = Landing(caster, VacuumCastKind.Digest, caster);
            if (cast == null) return;
            CompVacuum vacuum = cast.vacuum;
            // The mouth's thing goes down first; then the stomach drains over the chew.
            vacuum.DigestMouth();
            cast.digestKg = vacuum.Fullness;
            cast.digest.Kg = cast.digestKg * cast.scale;
            cast.digest.Chew = Mathf.Max(props.minSeconds, cast.digestKg * props.secondsPerKg);
            vacuum.Sync();
        }

        /// <summary>The caster's cast job is over. A cast that never landed has nothing left to show; a Digest stops chewing.</summary>
        public void Ended(Pawn caster)
        {
            int now = Find.TickManager.TicksGame;
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                Cast cast = casts[i];
                if (cast.caster != caster) continue;
                if (!cast.landed)
                {
                    Remove(i);
                    continue;
                }
                if (cast.home) continue;
                cast.home = true;
                if (cast.kind == VacuumCastKind.Digest && cast.Seconds(now) < Digest.Done(cast.digest))
                {
                    cast.digest.StopAt = cast.Seconds(now);
                    cast.vacuum.SetStomach(cast.digestKg * (1f - Digest.Digested(cast.digest, cast.digest.StopAt)));
                    cast.vacuum.Sync();
                }
                Release(caster);
            }
        }

        /// <summary>Whether the caster's cast has landed and its job has not ended: the job holds from here (CastJobFail).</summary>
        public bool Fired(Pawn caster)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == caster && casts[i].landed && !casts[i].home) return true;
            return false;
        }

        /// <summary>Whether the caster's cast job should still hold it in place: until the canister is back in the floor.</summary>
        public bool Holds(Pawn caster)
        {
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == caster && casts[i].landed && !casts[i].home && casts[i].Seconds(now) < casts[i].Home) return true;
            return false;
        }

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
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                Cast cast = casts[i];
                float s = cast.Seconds(now);
                if (!cast.landed)
                {
                    if (s > VacuumGraphics.Lead + cast.warmupTicks / 60f + Overdue || cast.caster == null || !cast.caster.Spawned || cast.caster.Map != map) Remove(i);
                    continue;
                }
                if (!cast.home && (s >= cast.Home || !cast.caster.Spawned || cast.caster.Downed))
                {
                    cast.home = true;
                    Release(cast.caster);
                }
                if (cast.kind == VacuumCastKind.Suck) TickSuck(cast, s);
                else if (cast.kind == VacuumCastKind.Spit) TickSpit(cast, s);
                else TickDigest(cast, s);
                if (s >= cast.End) Remove(i);
            }
            if (now % 60 == 0) Rescan();
        }

        /// <summary>Once a second: who holds a vacuum; flights a load cut short are finished; each slow matches its kg.</summary>
        private void Rescan()
        {
            holders.Clear();
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                CompVacuum vacuum = CompVacuum.HeldBy(pawns[i]);
                if (vacuum == null)
                {
                    if (VacuumLoad.Of(pawns[i]) != null) VacuumLoad.Remove(pawns[i]);
                    continue;
                }
                holders.Add(pawns[i]);
                if (vacuum.HasInbound && !CastingWith(vacuum)) vacuum.Settle();
                vacuum.Sync();
            }
        }

        /// <summary>Suck's effects at the picture's times: each thing leaves the ground as it lifts off and is swallowed as it enters the head.</summary>
        private void TickSuck(Cast cast, float s)
        {
            if (cast.taken == null) return;
            int count = cast.taken.Count;
            for (int i = 0; i < count; i++)
            {
                VacuumSuckTargets.Taken t = cast.taken[i];
                if (!cast.started[i] && s >= Suck.Start(i, count))
                {
                    cast.started[i] = true;
                    if (!Lift(cast, t)) Gone(cast, i);
                }
                if (!cast.arrived[i] && s >= Suck.Arrive(i, count))
                {
                    cast.arrived[i] = true;
                    if (cast.suck.Things[i].Gone) continue;
                    if (t.thing is Filth)
                    {
                        if (!t.thing.Destroyed && t.thing.Spawned) t.thing.Destroy();
                    }
                    else
                    {
                        cast.vacuum.Swallow(t.thing, t.kg);
                        VacuumSound.Play(VacuumSound.Swallowed, map, cast.suck.Caster);
                    }
                    cast.vacuum.Sync();
                }
            }
        }

        /// <summary>A thing starts its flight: off the ground, or out of the pawn's hands, into the vacuum's keeping.</summary>
        private bool Lift(Cast cast, VacuumSuckTargets.Taken t)
        {
            Thing thing = t.thing;
            if (thing == null || thing.Destroyed) return false;
            if (thing is Filth) return thing.Spawned && thing.Map == map;
            if (t.from != null)
            {
                Pawn pawn = t.from;
                if (pawn.Dead || pawn.equipment?.Primary != thing) return false;
                pawn.equipment.Remove((ThingWithComps)thing);
                return cast.vacuum.AddInbound(thing);
            }
            if (!thing.Spawned || thing.Map != map) return false;
            return cast.vacuum.AddInbound(thing);
        }

        private static void Gone(Cast cast, int i)
        {
            VacuumThing picture = cast.suck.Things[i];
            picture.Gone = true;
            cast.suck.Things[i] = picture;
        }

        /// <summary>Who a spat thing hits: the targeted pawn if it is still close to where it was aimed at, else a pawn on the aimed cell.</summary>
        private Pawn Victim(Cast cast)
        {
            if (Hittable(cast.target) && cast.target.Position.DistanceTo(cast.aimedCell) <= cast.spitProps.followRadius) return cast.target;
            if (!cast.aimedCell.InBounds(map)) return null;
            List<Thing> things = cast.aimedCell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
                if (things[i] is Pawn pawn && pawn != cast.caster && Hittable(pawn)) return pawn;
            return null;
        }

        /// <summary>Spit's effects at the picture's times: the launch, then the hit and the landing at the impact.</summary>
        private void TickSpit(Cast cast, float s)
        {
            if (!cast.launched && s >= Spit.Launch)
            {
                cast.launched = true;
                Shake(Spit.LaunchShake);
                VacuumSound.Play(VacuumSound.Launch, map, cast.spit.Caster);
            }
            if (cast.impacted || s < Spit.Impact) return;
            cast.impacted = true;
            CompVacuum vacuum = cast.vacuum;
            if (cast.spat == null || vacuum.Mouth != cast.spat) return;
            float kg = vacuum.MouthKg;
            Pawn victim = Victim(cast);
            if (victim != null)
            {
                CompProperties_VacuumSpit props = cast.spitProps;
                float damage = Mathf.Min(props.maxDamage, props.damagePerKg * kg);
                lastSpitVictim = victim;
                lastSpitDamage = damage;
                Shake(Spit.HitShake);
                VacuumSound.Play(VacuumSound.Hit, map, Ground(victim));
                if (damage >= 1f)
                {
                    float angle = (victim.Position - cast.caster.Position).AngleFlat;
                    victim.TakeDamage(new DamageInfo(DamageDefOf.Blunt, damage, props.armorPenetration, angle, cast.caster, null, VacuumDefOf.AG_Vacuum,
                        DamageInfo.SourceCategory.ThingOrUnknown, victim));
                }
                if (Hittable(victim) && props.stunSeconds > 0f)
                    victim.stances?.stunner?.StunFor(Mathf.RoundToInt(props.stunSeconds * 60f), cast.caster, false);
            }
            IntVec3 rest = CellOf(cast.spit.Rest);
            if (!rest.InBounds(map)) rest = cast.aimedCell;
            vacuum.DropMouth(rest, map);
            vacuum.Sync();
        }

        /// <summary>Digest's effects: the stomach drains over the chew; the burp at the end.</summary>
        private void TickDigest(Cast cast, float s)
        {
            VacuumDigestShot shot = cast.digest;
            if (shot.StopAt < float.MaxValue) return;
            float done = Digest.Done(shot);
            if (s >= Digest.Chew0 && s < done)
            {
                cast.vacuum.SetStomach(cast.digestKg * (1f - Digest.Digested(shot, s)));
                int bite = Mathf.FloorToInt((s - Digest.Chew0) * Digest.ChompHz);
                if (bite != cast.bites)
                {
                    cast.bites = bite;
                    if (bite % 2 == 0) VacuumSound.Play(VacuumSound.Chew, map, shot.Caster);
                }
                if (Find.TickManager.TicksGame % 10 == 0) cast.vacuum.Sync();
            }
            if (!cast.burped && s >= done)
            {
                cast.burped = true;
                cast.vacuum.SetStomach(0f);
                cast.vacuum.Sync();
                Shake(Digest.BurpShake);
                VacuumSound.Play(VacuumSound.Burp, map, shot.Caster);
            }
        }

        private bool Hittable(Pawn pawn) => pawn != null && pawn.Spawned && pawn.Map == map && !pawn.Dead;

        private void Shake(float size)
        {
            if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(size);
        }

        // ------------------------------------------------------------------ drawing

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map) return;
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < casts.Count; i++)
            {
                Cast cast = casts[i];
                float s = cast.Seconds(now);
                // Before the ability fires the picture holds at the end of the wind-up.
                if (!cast.landed) s = Mathf.Min(s, cast.Fire - 0.001f);
                bool weapon = !cast.home;
                switch (cast.kind)
                {
                    case VacuumCastKind.Suck:
                        cast.suck.DazedAt = cast.disarmed != null && cast.disarmed.Spawned && cast.disarmed.Map == map ? Ground(cast.disarmed) : (Vector2?)null;
                        VacuumSuckGraphics.Draw(cast.suck, s, map, weapon);
                        break;
                    case VacuumCastKind.Spit:
                        Pawn victim = cast.landed && cast.spitProps != null ? Victim(cast) : null;
                        cast.spit.DazedAt = victim != null ? Ground(victim) : (Vector2?)null;
                        VacuumSpitGraphics.Draw(cast.spit, s, map, weapon);
                        break;
                    default:
                        VacuumDigestGraphics.Draw(cast.digest, s, map, weapon);
                        break;
                }
            }
        }

        public override void MapRemoved()
        {
            for (int i = casts.Count - 1; i >= 0; i--) Remove(i);
            holders.Clear();
        }
    }

    /// <summary>The kit's sounds: vanilla placeholders until it has its own. Suck's wind-up sound is the ability's warmupStartSound.</summary>
    internal static class VacuumSound
    {
        internal static SoundDef Swallowed => SoundDef.Named("Standard_Pickup");
        internal static SoundDef Launch => SoundDef.Named("ThrowGrenade");
        internal static SoundDef Hit => SoundDef.Named("Pawn_Melee_Punch_HitPawn");
        internal static SoundDef Chew => SoundDef.Named("Crunch");
        internal static SoundDef Burp => SoundDef.Named("Hive_Spawn");

        public static void Play(SoundDef sound, Map map, Vector2 ground) => PowerPoleSound.Play(sound, map, ground);
    }
}
