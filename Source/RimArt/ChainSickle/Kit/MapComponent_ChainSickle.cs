using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Snag = RimArt.ChainSickleSnagTiming;
using Stake = RimArt.ChainSickleStakeTiming;

namespace RimArt
{
    public enum ChainSickleCastKind { Snag, Stake }

    /// <summary>
    /// Everything the Chain Sickle does on a map:
    /// - plays Snag's and Stake's pictures for real casts and applies each effect when the picture
    ///   shows it: Snag's hit and the start of the snag at the wrap, the reel (or the holder's drag)
    ///   at the reel, the end of the reel; Stake's pin when the weight hits the floor;
    /// - checks every snag each tick and lets go when the rules say so (see <see cref="Check"/>);
    /// - draws each snag's chain between casts, from the holder's hand to the coil on the target.
    ///
    /// A cast is known from the moment its warmup starts (<see cref="Begin"/>, from
    /// JobDriver_CastChainSickle). The clock is game ticks. Nothing about a cast is saved: a game
    /// loaded mid-cast loses the rest of its picture and any effect not yet applied. The snag itself
    /// (who is snagged, whether staked, when it ends) is saved on the weapon (CompChainSickle).
    /// </summary>
    public class MapComponent_ChainSickle : MapComponent
    {
        private sealed class Cast
        {
            public Pawn caster, target;
            public CompChainSickle sickle;
            public ChainSickleCastKind kind;
            public int startTick, warmupTicks;
            public bool landed, home;

            public ChainSnagShot snag;
            public CompProperties_ChainSickleSnag snagProps;
            public bool thrown, hitDone, reelStarted, reelDone;

            public ChainStakeShot stake;
            public CompProperties_ChainSickleStake stakeProps;
            public bool stakeDone;

            public float Seconds(int now) => (kind == ChainSickleCastKind.Snag ? Snag.Spin0 : 0f) + (now - startTick) / 60f;
        }

        /// <summary>A cast whose warmup ran out this long ago without landing was interrupted.</summary>
        private const float Overdue = 0.25f;
        /// <summary>Stake holds the holder this long after the weight is in the floor.</summary>
        private const float StakeHold = 0.2f;

        private readonly List<Cast> casts = new List<Cast>();
        /// <summary>Sickles with a snag on this map.</summary>
        private readonly List<CompChainSickle> links = new List<CompChainSickle>();

        public MapComponent_ChainSickle(Map map) : base(map) { }

        private static float Degrees(Vector2 run) => run.sqrMagnitude < 0.0001f ? 0f : Mathf.Atan2(run.y, run.x) * Mathf.Rad2Deg;

        private Vector2 GroundOf(Pawn pawn, Vector2 fallback) => ChainSickleCombat.Ground(pawn, map) ?? fallback;

        private static Vector2 CellGround(IntVec3 cell) => new Vector2(cell.x + 0.5f, cell.z + 0.5f);

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            // Snags saved on the weapons: pick them up again.
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                CompChainSickle sickle = CompChainSickle.HeldBy(pawns[i]);
                if (sickle?.snagged != null && !links.Contains(sickle)) links.Add(sickle);
            }
        }

        // ------------------------------------------------------------------ casts

        /// <summary>A cast's warmup has begun: the weight is spun up (Snag) or the chain held taut (Stake).</summary>
        public void Begin(Pawn caster, Ability ability, LocalTargetInfo target)
        {
            Ended(caster);
            if (caster == null || ability == null || !(target.Thing is Pawn pawn)) return;
            ChainSickleCastKind kind = ability.CompOfType<CompAbilityEffect_ChainSickleStake>() != null ? ChainSickleCastKind.Stake : ChainSickleCastKind.Snag;
            Add(caster, pawn, kind, Find.TickManager.TicksGame, ability.def.verbProperties.warmupTime);
        }

        private Cast Add(Pawn caster, Pawn target, ChainSickleCastKind kind, int startTick, float warmup)
        {
            CompChainSickle sickle = CompChainSickle.HeldBy(caster);
            if (sickle == null) return null;
            Vector2 feet = GroundOf(caster, CellGround(caster.Position)), to = GroundOf(target, CellGround(target.Position));
            ChainSickleWeight w = ChainSickleCombat.Weigh(caster, target, sickle.Props);
            var cast = new Cast
            {
                caster = caster, target = target, sickle = sickle, kind = kind, startTick = startTick, warmupTicks = Mathf.RoundToInt(warmup * 60f),
            };
            if (kind == ChainSickleCastKind.Snag)
            {
                cast.snag = new ChainSnagShot
                {
                    Caster0 = feet, Start = to, Aim = Degrees(to - feet), Reel = w.Reel, Dragged = w.Dragged, Size = target.BodySize,
                    Range = sickle.Props.chainLength, Hold = Snag.Hold,
                };
            }
            else
            {
                cast.stake = new ChainStakeShot { Caster = feet, Target = to, Aim = Degrees(to - feet), Size = target.BodySize, Pin = w.Pin, SwingAt = -1f };
            }
            casts.Add(cast);
            return cast;
        }

        /// <summary>The cast that has begun and not landed, or a new one for a cast that was never begun.</summary>
        private Cast Landing(Pawn caster, Pawn target, ChainSickleCastKind kind, float warmup)
        {
            Cast cast = casts.Find(c => c.caster == caster && c.kind == kind && !c.landed)
                ?? Add(caster, target, kind, Find.TickManager.TicksGame - Mathf.RoundToInt(warmup * 60f), warmup);
            if (cast != null) cast.landed = true;
            return cast;
        }

        public void LandSnag(Pawn caster, Pawn target, float warmup, CompProperties_ChainSickleSnag props)
        {
            Cast cast = Landing(caster, target, ChainSickleCastKind.Snag, warmup);
            if (cast == null) return;
            cast.snagProps = props;
            cast.target = target;
            Vector2 feet = GroundOf(caster, cast.snag.Caster0), to = GroundOf(target, cast.snag.Start);
            cast.snag.Caster0 = feet;
            cast.snag.Start = to;
            cast.snag.Aim = Degrees(to - feet);
        }

        public void LandStake(Pawn caster, Pawn target, float warmup, CompProperties_ChainSickleStake props)
        {
            Cast cast = Landing(caster, target, ChainSickleCastKind.Stake, warmup);
            if (cast == null) return;
            cast.stakeProps = props;
            cast.target = target;
            Vector2 feet = GroundOf(caster, cast.stake.Caster), to = GroundOf(target, cast.stake.Target);
            cast.stake.Target = to;
            cast.stake.Aim = Degrees(to - feet);
            cast.stake.Pin = ChainSickleCombat.Weigh(caster, target, cast.sickle.Props).Pin;
        }

        /// <summary>The caster's cast job is over. A cast that never landed has nothing left to show.</summary>
        public void Ended(Pawn caster)
        {
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                if (casts[i].caster != caster) continue;
                if (!casts[i].landed) casts.RemoveAt(i);
                else casts[i].home = true;
            }
        }

        /// <summary>Whether the caster's cast job should still hold it in place.</summary>
        public bool Holds(Pawn caster)
        {
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < casts.Count; i++)
            {
                Cast cast = casts[i];
                if (cast.caster != caster || !cast.landed || cast.home) continue;
                float hold = cast.kind == ChainSickleCastKind.Snag ? Snag.ReelEnd(cast.snag.Reel) : Stake.Staked + StakeHold;
                if (cast.Seconds(now) < hold) return true;
            }
            return false;
        }

        private bool Pictured(Pawn holder)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == holder && casts[i].landed) return true;
            return false;
        }

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                Cast cast = casts[i];
                float seconds = cast.Seconds(now);
                if (!cast.landed)
                {
                    float start = cast.kind == ChainSickleCastKind.Snag ? Snag.Spin0 : 0f;
                    if (seconds > start + cast.warmupTicks / 60f + Overdue || cast.caster == null || !cast.caster.Spawned || cast.caster.Map != map) casts.RemoveAt(i);
                    continue;
                }
                bool keep = cast.kind == ChainSickleCastKind.Snag ? TickSnag(cast, seconds, now) : TickStake(cast, seconds, now);
                if (!keep) casts.RemoveAt(i);
            }
            for (int i = links.Count - 1; i >= 0; i--) Check(links[i], now);
        }

        /// <summary>Snag's effects at the picture's times. False when the cast is over.</summary>
        private bool TickSnag(Cast cast, float s, int now)
        {
            Pawn caster = cast.caster, target = cast.target;
            if (!cast.thrown && s >= Snag.Throw0)
            {
                cast.thrown = true;
                PowerPoleSound.Play(PowerPoleSoundDefOf.AG_PowerPoleSwing, map, cast.snag.Caster0);
            }
            if (!cast.hitDone && s >= Snag.Hit)
            {
                cast.hitDone = true;
                Vector2? at = ChainSickleCombat.Ground(target, map), from = ChainSickleCombat.Ground(caster, map);
                // The weight misses a pawn that has gone, gone down, or moved out of the chain's reach.
                if (at == null || from == null || target.Dead || target.Downed || (at.Value - from.Value).magnitude > cast.sickle.Props.chainLength + 1f)
                {
                    if (caster.Faction == Faction.OfPlayer) Messages.Message("Snag missed " + target.LabelShort + ".", caster, MessageTypeDefOf.RejectInput, false);
                    return false;
                }
                cast.snag.Start = at.Value;
                Shake(Snag.HitShake);
                PowerPoleSound.Play(PowerPoleSoundDefOf.AG_PowerPoleHit, map, at.Value);
                ChainSickleCombat.Hit(target, caster, cast.snagProps?.damage ?? 4f, cast.snagProps?.armorPenetration ?? 0f, at.Value - from.Value);
                if (target.Dead || target.Downed) return true;
                StartLink(cast.sickle, target);
                // The coil holds the target still until the reel begins.
                target.pather?.StopDead();
                target.stances?.stunner?.StunFor(Mathf.CeilToInt((Snag.Reel0 - Snag.Hit) * 60f) + 2, caster, false, false);
            }
            if (cast.hitDone && !cast.reelStarted && s >= Snag.Reel0)
            {
                cast.reelStarted = true;
                if (!cast.sickle.Snags(target) || !target.Spawned || !caster.Spawned) return s < Snag.End(cast.snag.Reel, cast.snag.Hold);
                StartReel(cast);
                Shake(Snag.ReelShake);
            }
            if (cast.reelStarted && !cast.reelDone && s >= Snag.ReelEnd(cast.snag.Reel))
            {
                cast.reelDone = true;
                if (cast.sickle.Snags(target)) cast.sickle.ReelDone(now);
            }
            return s < Snag.End(cast.snag.Reel, cast.snag.Hold);
        }

        /// <summary>Weighs the pair again where they stand now and starts the flyer that reels the target or drags the holder.</summary>
        private void StartReel(Cast cast)
        {
            Pawn caster = cast.caster, target = cast.target;
            CompProperties_ChainSickle props = cast.sickle.Props;
            ChainSickleWeight w = ChainSickleCombat.Weigh(caster, target, props);
            Pawn mover;
            IntVec3 dest;
            if (w.Dragged)
            {
                mover = caster;
                dest = ChainSickleCombat.ReelCell(caster.Position, target.Position, props.dragCells, target.Position, caster, map);
            }
            else
            {
                mover = target;
                dest = ChainSickleCombat.ReelCell(target.Position, caster.Position, w.Pull, caster.Position, target, map);
            }
            float moved = (dest - mover.Position).LengthHorizontal;
            Vector2 feet = GroundOf(caster, cast.snag.Caster0), to = GroundOf(target, cast.snag.Start);
            cast.snag.Caster0 = feet;
            cast.snag.Start = to;
            cast.snag.Aim = Degrees(to - feet);
            cast.snag.Dragged = w.Dragged;
            cast.snag.Reel = w.Reel;
            cast.snag.Pull = w.Dragged ? 0f : moved;
            cast.snag.Back = w.Dragged ? moved : 0f;
            if (moved < 0.5f || !dest.Standable(map)) return;
            var flyer = PawnFlyer.MakeFlyer(ChainSickleDefOf.AG_ChainSickleReeled, mover, dest, null, null) as PawnFlyer_ChainSickleReel;
            if (flyer == null) return;
            flyer.SetDuration(Mathf.Max(1, Mathf.RoundToInt(w.Reel * 60f)));
            GenSpawn.Spawn(flyer, dest, map);
        }

        /// <summary>Stake's pin when the weight hits the floor. False when the cast is over.</summary>
        private bool TickStake(Cast cast, float s, int now)
        {
            Pawn caster = cast.caster, target = cast.target;
            if (!cast.stakeDone && s >= Stake.Staked)
            {
                cast.stakeDone = true;
                if (!cast.sickle.Snags(target) || !target.Spawned || target.Dead) return false;
                float pin = ChainSickleCombat.Weigh(caster, target, cast.sickle.Props).Pin;
                if (pin <= 0f) return false;
                cast.stake.Pin = pin;
                int ticks = Mathf.RoundToInt(pin * 60f);
                Shake(Stake.StakeShake);
                PowerPoleSound.Play(PowerPoleSoundDefOf.AG_PowerPoleSlam, map, cast.stake.Target);
                if ((cast.stakeProps?.dropWeapon ?? true) && target.equipment?.Primary != null)
                    target.equipment.TryDropEquipment(target.equipment.Primary, out _, target.Position, false);
                Hediff staked = target.health.hediffSet.GetFirstHediffOfDef(ChainSickleDefOf.AG_ChainStaked);
                if (staked == null)
                {
                    staked = HediffMaker.MakeHediff(ChainSickleDefOf.AG_ChainStaked, target);
                    target.health.AddHediff(staked);
                }
                staked.TryGetComp<HediffComp_Disappears>()?.SetDuration(ticks);
                target.pather?.StopDead();
                target.stances?.stunner?.StunFor(ticks, caster, false, false);
                cast.sickle.StartPin(now, pin);
            }
            if (cast.stakeDone && !cast.sickle.Snags(target)) return false;
            return s < Stake.Staked + Mathf.Max(cast.stake.Pin, 0.1f);
        }

        // ------------------------------------------------------------------ the snag

        private void StartLink(CompChainSickle sickle, Pawn target)
        {
            if (sickle.snagged != null && sickle.snagged != target) Release(sickle.Holder, sickle, null);
            sickle.StartSnag(target);
            if (!links.Contains(sickle)) links.Add(sickle);
        }

        /// <summary>
        /// Lets go of a snag when: the sickle is no longer held; the holder or the target is dead,
        /// down or off this map; they are further apart than the chain's length; the snag's time ran
        /// out (not staked); or the pin is over (staked: Stake uses the snag up).
        /// </summary>
        private void Check(CompChainSickle sickle, int now)
        {
            Pawn target = sickle.snagged, holder = sickle.Holder;
            if (target == null) { links.Remove(sickle); return; }
            string label = target.LabelShort;
            if (holder == null || holder.Dead || holder.Downed) { Release(holder, sickle, "the holder let go of " + label); return; }
            if (target.Dead || target.Destroyed) { Release(holder, sickle, null); return; }
            if (target.Downed) { Release(holder, sickle, label + " is down"); return; }
            Vector2? h = ChainSickleCombat.Ground(holder, map), t = ChainSickleCombat.Ground(target, map);
            if (h == null || t == null) { Release(holder, sickle, null); return; }
            if ((h.Value - t.Value).magnitude > sickle.Props.chainLength)
            {
                Release(holder, sickle, sickle.staked ? "the stake was pulled out: " + label + " is out of the chain's reach" : label + " is out of the chain's reach");
                return;
            }
            if (sickle.staked)
            {
                if (now >= sickle.pinEndsTick) Release(holder, sickle, null);
                return;
            }
            // Loaded in the middle of a reel: the snag's clock starts now.
            if (sickle.snagEndsTick < 0 && !Pictured(holder)) sickle.ReelDone(now);
            if (sickle.snagEndsTick >= 0 && now >= sickle.snagEndsTick) Release(holder, sickle, "the chain slipped off " + label);
        }

        /// <summary>Ends a snag, and the pin with it, and says why when a reason is given.</summary>
        public static void Release(Pawn holder, CompChainSickle sickle, string reason)
        {
            Pawn target = sickle.snagged;
            if (target != null && sickle.staked && !target.Dead)
            {
                int left = sickle.pinEndsTick - Find.TickManager.TicksGame;
                StunHandler stunner = target.stances?.stunner;
                // Only the pin's own stun: a longer one from something else is left alone.
                if (stunner != null && stunner.Stunned && stunner.StunTicksLeft <= left + 2) stunner.StopStun();
                Hediff staked = target.health?.hediffSet?.GetFirstHediffOfDef(ChainSickleDefOf.AG_ChainStaked);
                if (staked != null) target.health.RemoveHediff(staked);
            }
            sickle.ClearSnag();
            Map map = holder?.MapHeld ?? sickle.parent.MapHeld;
            MapComponent_ChainSickle component = map?.GetComponent<MapComponent_ChainSickle>();
            // A Stake picture sees the snag gone on its next tick and ends there.
            component?.links.Remove(sickle);
            if (reason != null && holder != null && holder.Faction == Faction.OfPlayer)
                Messages.Message("Chain sickle: " + reason + ".", holder, MessageTypeDefOf.NeutralEvent, false);
        }

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
                float seconds = cast.Seconds(now);
                bool weapon = !cast.home;
                Vector2? caster = ChainSickleCombat.Ground(cast.caster, map), target = ChainSickleCombat.Ground(cast.target, map);
                if (cast.kind == ChainSickleCastKind.Snag)
                {
                    cast.snag.CasterAt = caster;
                    cast.snag.TargetAt = target;
                    ChainSickleSnagGraphics.Draw(cast.snag, cast.landed ? seconds : Mathf.Min(seconds, Snag.Throw0 - 0.001f), map, weapon);
                }
                else
                {
                    if (caster.HasValue) cast.stake.Caster = caster.Value;
                    cast.stake.BodyAt = target;
                    Vector2 body = target ?? cast.stake.Target;
                    float d = Stake.ScriptDistance;
                    cast.stake.Slack = Mathf.Clamp01((d - (body - cast.stake.Caster).magnitude) / (d - 1f));
                    ChainSickleStakeGraphics.Draw(cast.stake, cast.landed ? seconds : Mathf.Min(seconds, Stake.Yank0 - 0.001f), map, weapon);
                }
            }
            for (int i = 0; i < links.Count; i++)
            {
                CompChainSickle sickle = links[i];
                Pawn holder = sickle.Holder;
                if (holder == null || sickle.snagged == null || Pictured(holder)) continue;
                Vector2? h = ChainSickleCombat.Ground(holder, map), t = ChainSickleCombat.Ground(sickle.snagged, map);
                if (h == null || t == null) continue;
                ChainSickleLink.Draw(h.Value, t.Value, sickle.snagged.BodySize, map);
            }
        }

        public override void MapRemoved()
        {
            casts.Clear();
            links.Clear();
        }
    }

    /// <summary>The snag between casts: the live chain from the holder's hand to the coil on the target.</summary>
    internal static class ChainSickleLink
    {
        public static void Draw(Vector2 holder, Vector2 target, float size, Map map)
        {
            if (!VfxDraw.Shown(holder, map) && !VfxDraw.Shown(target, map)) return;
            VfxDraw.Begin(holder);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            ChainSickleGraphics.Snagged(holder, target, size, sun, strength);
        }
    }
}
