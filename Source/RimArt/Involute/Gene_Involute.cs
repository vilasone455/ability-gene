using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimArt
{
    public enum KamuiPhaseState
    {
        Solid,
        Phased,
        /// <summary>Toggled off: still intangible and unable to act until the turn to solid ends.</summary>
        TurningSolid,
    }

    /// <summary>
    /// Obito's Echo-only gene (the fold organ, docs/hero-echo.md "Obito"): the Kamui dimension he owns and the
    /// state of his four abilities. The Echo adds it on awakening and it stays for life, so the dimension and
    /// what he stored in it outlive a revert; the abilities come from the Echo and work only in hero form.
    ///
    /// State lives on the gene because it belongs to the person: it saves with them, travels between maps with
    /// them, and is gone with the gene. The numbers are the ability defs' comps (<see cref="ObitoRules"/>).
    ///
    /// - Kamui: Phase. A pool of intangible time, spent while phased and refilled while solid. While
    ///   intangible every hit goes into the dimension (<see cref="Patch_Pawn_KamuiPhase"/>) and he cannot
    ///   attack, carry or cast anything but Phase itself.
    /// - Kamui: Warp. In: he stands at the dimension's mouth, and the map and cell he left are kept. Out: any
    ///   revealed cell of that map, after a warning mark.
    /// - Kamui: Store. What he absorbed (pawns, item stacks) is listed here and stays in the dimension until
    ///   released. Held enemies are stunned there by <see cref="MapComponent_KamuiDimension"/>.
    /// - On his death or the loss of the gene, everything in the dimension comes out where he is (or, if he
    ///   died inside, where he went in).
    /// </summary>
    public class Gene_Involute : Gene
    {
        /// <summary>The dimension. Generated on awakening (<see cref="PostAdd"/>), or on first use.</summary>
        private Map volume;

        // ---- Kamui: Phase ----
        private KamuiPhaseState phase = KamuiPhaseState.Solid;
        /// <summary>Ticks of phase left; -1 until the first tick fills it.</summary>
        private float pool = -1f;
        private int solidAtTick = -1;
        private int phaseChangedTick = -99999;

        // ---- Kamui: Warp ----
        private Map fromMap;
        private IntVec3 fromCell = IntVec3.Invalid;
        private Map exitMap;
        private IntVec3 exitCell = IntVec3.Invalid;
        private int exitTick = -1;

        // ---- Kamui: Store ----
        private List<Thing> stored = new List<Thing>();
        private Thing releasing;
        private Map releaseMap;
        private IntVec3 releaseCell = IntVec3.Invalid;
        private int releaseTick = -1;
        private int releaseReadyTick = -1;
        /// <summary>Pawns whose attack went through him, and when. Not saved: the window is seconds long.</summary>
        private readonly Dictionary<Pawn, int> passedThrough = new Dictionary<Pawn, int>();

        public InvoluteGeneExtension Ext => def.GetModExtension<InvoluteGeneExtension>();

        public Map Volume => volume;

        /// <summary>He is standing in his own dimension.</summary>
        public bool Inside => volume != null && pawn != null && pawn.MapHeld == volume;

        /// <summary>The abilities and the pass-through work only in hero form.</summary>
        public bool ActiveNow => pawn != null && EchoUtility.GeneActive(pawn, def);

        public override void PostAdd()
        {
            base.PostAdd();
            ObitoFX.Know(this);
            // The dimension is built when the kit is gained, not on the first hit: a pocket map made mid-fight
            // is a hitch. Pawn generation and loading add genes before a game is running; those build it on
            // first use instead.
            if (Current.ProgramState == ProgramState.Playing && pawn != null && pawn.Spawned && pawn.IsColonist)
                EnsureVolume();
        }

        /// <summary>
        /// The volume, generating it if nothing has needed it yet. Returns null rather than throwing if
        /// generation fails: every caller is an ability or a hit passing through, not a place to end the game.
        /// </summary>
        public Map EnsureVolume()
        {
            if (volume != null) return volume;
            volume = InvoluteUtility.GenerateVolume(this);
            volume?.GetComponent<MapComponent_KamuiDimension>()?.SetOwner(pawn);
            return volume;
        }

        // =============================================================================== Kamui: Phase

        public KamuiPhaseState Phase => phase;
        public bool Intangible => phase != KamuiPhaseState.Solid;
        public bool Phased => phase == KamuiPhaseState.Phased;
        public int PhaseChangedTick => phaseChangedTick;
        public int SolidAtTick => solidAtTick;
        public float PoolMax => ObitoRules.Phase.poolSeconds * 60f;
        public float PoolTicks => pool < 0f ? PoolMax : pool;

        /// <summary>The Phase button: phase if solid and the pool allows, turn solid if phased.</summary>
        public void TogglePhase()
        {
            if (phase == KamuiPhaseState.Phased)
            {
                TurnSolid();
                return;
            }
            if (phase != KamuiPhaseState.Solid) return;
            if (!CanStartPhase(out string reason))
            {
                Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }
            StartPhase();
        }

        public bool CanStartPhase(out string reason)
        {
            reason = null;
            if (PoolTicks < ObitoRules.Phase.minStartSeconds * 60f)
            {
                reason = "AG_KamuiPhasePoolLow".Translate(pawn.LabelShortCap, (PoolTicks / 60f).ToString("0.0"));
                return false;
            }
            if (Inside && exitTick >= 0)
            {
                reason = "AG_KamuiWarpExiting".Translate(pawn.LabelShortCap);
                return false;
            }
            return true;
        }

        private void StartPhase()
        {
            phase = KamuiPhaseState.Phased;
            phaseChangedTick = Find.TickManager.TicksGame;
            KamuiPhaseRegistry.Add(this);
            // He cannot carry or aim while intangible: what he held falls, a warm-up in progress stops.
            if (pawn.carryTracker?.CarriedThing != null) pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
            if (pawn.stances?.curStance is Stance_Warmup warmup && !IsPhaseVerb(warmup.verb)) pawn.stances.CancelBusyStanceSoft();
            ObitoFX.PhaseChanged(pawn, true);
        }

        public void TurnSolid()
        {
            if (phase != KamuiPhaseState.Phased) return;
            phase = KamuiPhaseState.TurningSolid;
            phaseChangedTick = Find.TickManager.TicksGame;
            solidAtTick = phaseChangedTick + Mathf.Max(1, Mathf.RoundToInt(ObitoRules.Phase.solidSeconds * 60f));
            ObitoFX.PhaseChanged(pawn, false);
        }

        /// <summary>Solid at once: downed, dead, reverted or leaving the kit.</summary>
        public void ForceSolid()
        {
            if (phase == KamuiPhaseState.Solid) return;
            bool wasPhased = phase == KamuiPhaseState.Phased;
            phase = KamuiPhaseState.Solid;
            phaseChangedTick = Find.TickManager.TicksGame;
            KamuiPhaseRegistry.Remove(this);
            if (wasPhased) ObitoFX.PhaseChanged(pawn, false);
        }

        public static bool IsPhaseVerb(Verb verb) => verb is Verb_CastAbility cast && cast.ability?.def == ObitoDefOf.AG_KamuiPhase;

        /// <summary>A hit went through him: it goes into the dimension, and its attacker can be absorbed at once.</summary>
        public void PassedThrough(DamageInfo dinfo)
        {
            if (dinfo.Instigator is Pawn attacker && attacker != pawn) passedThrough[attacker] = Find.TickManager.TicksGame;
            ObitoFX.PassedThrough(pawn, dinfo);
            InvoluteUtility.PassThrough(this, dinfo);
        }

        public bool PassedThroughRecently(Thing thing)
        {
            if (!(thing is Pawn p) || !passedThrough.TryGetValue(p, out int tick)) return false;
            return Find.TickManager.TicksGame - tick <= ObitoRules.Store.counterSeconds * 60f;
        }

        public IEnumerable<Pawn> RecentAttackers()
        {
            int now = Find.TickManager.TicksGame, window = Mathf.RoundToInt(ObitoRules.Store.counterSeconds * 60f);
            foreach (KeyValuePair<Pawn, int> entry in passedThrough)
                if (now - entry.Value <= window) yield return entry.Key;
        }

        /// <summary>When the attack of this pawn last went through him, or -1.</summary>
        public int PassedThroughTick(Pawn p) => p != null && passedThrough.TryGetValue(p, out int tick) ? tick : -1;

        // =============================================================================== Kamui: Warp

        public Map FromMap => fromMap;
        public IntVec3 FromCell => fromCell;
        public bool ExitPending => exitTick >= 0;
        public Map ExitMap => exitMap;
        public IntVec3 ExitCell => exitCell;
        public int ExitTick => exitTick;

        /// <summary>The map he comes out on: the one he left, or a home map if that one is gone.</summary>
        public Map OutsideMap => fromMap != null && Find.Maps.Contains(fromMap) ? fromMap : Find.AnyPlayerHomeMap;

        /// <summary>The warm-up is done: he is inside, at the dimension's mouth.</summary>
        public bool Enter()
        {
            Map origin = pawn.Map;
            if (origin == null || origin == volume) return false;
            Map inside = EnsureVolume();
            if (inside == null) return false;
            IntVec3 mouth = InvoluteUtility.MouthCell(inside);
            if (!mouth.IsValid) return false;
            fromMap = origin;
            fromCell = pawn.Position;
            ObitoFX.WarpedIn(pawn);
            Move(pawn, InvoluteUtility.FreeCellNear(inside, mouth), inside, true);
            return true;
        }

        /// <summary>He picked the cell he comes out at; the warning mark shows there first.</summary>
        public void BeginExit(Map map, IntVec3 cell)
        {
            if (!Inside || map == null || exitTick >= 0) return;
            exitMap = map;
            exitCell = cell;
            exitTick = Find.TickManager.TicksGame + Mathf.Max(1, Mathf.RoundToInt(ObitoRules.Warp.exitMarkSeconds * 60f));
            ObitoFX.ExitMarked(pawn, map, cell, exitTick);
        }

        private void FinishExit()
        {
            Map map = exitMap;
            IntVec3 cell = exitCell;
            exitTick = -1;
            exitMap = null;
            exitCell = IntVec3.Invalid;
            if (!Inside || map == null || !Find.Maps.Contains(map)) return;
            IntVec3 to = InvoluteUtility.FreeCellNear(map, cell);
            Move(pawn, to, map, true);
            ObitoFX.WarpedOut(pawn);
            fromMap = null;
            fromCell = IntVec3.Invalid;
            Ability warp = pawn.abilities?.GetAbility(ObitoDefOf.AG_KamuiWarp);
            warp?.StartCooldown(Mathf.RoundToInt(ObitoRules.Warp.cooldownAfterExitSeconds * 60f));
        }

        /// <summary>Out at once, where he went in: the form ended while he was inside.</summary>
        public void ExitToWhereHeLeft()
        {
            exitTick = -1;
            if (!Inside) return;
            Map map = OutsideMap;
            if (map == null) return;
            IntVec3 cell = map == fromMap && fromCell.IsValid ? fromCell : map.Center;
            Move(pawn, InvoluteUtility.FreeCellNear(map, cell), map, true);
            fromMap = null;
            fromCell = IntVec3.Invalid;
        }

        /// <summary>Takes a pawn off its map and puts it on another, drafted if it was; the camera follows a selected one.</summary>
        public static void Move(Pawn p, IntVec3 cell, Map to, bool follow)
        {
            bool drafted = p.Drafted, selected = Find.Selector.IsSelected(p);
            Rot4 facing = p.Rotation;
            Lord lord = p.GetLord();
            p.DeSpawnOrDeselect();
            GenSpawn.Spawn(p, cell, to, facing);
            p.Notify_Teleported(true, true);
            lord?.Notify_PawnLost(p, PawnLostCondition.ExitedMap);
            if (drafted && p.drafter != null && !p.Downed) p.drafter.Drafted = true;
            if (follow && selected && p.Faction == Faction.OfPlayer)
            {
                CameraJumper.TryJump(new GlobalTargetInfo(p));
                Find.Selector.Select(p, false, false);
            }
        }

        // =============================================================================== Kamui: Store

        /// <summary>What he absorbed and has not released, still in the dimension.</summary>
        public List<Thing> Stored
        {
            get
            {
                stored.RemoveAll(t => !IsHeld(t));
                return stored;
            }
        }

        public bool Releasing => releaseTick >= 0;
        public Thing ReleasingThing => releasing;
        public int ReleaseReadyTick => releaseReadyTick;
        public bool ReleaseReady => Find.TickManager.TicksGame >= releaseReadyTick;

        private bool IsHeld(Thing thing)
        {
            if (thing == null || volume == null) return false;
            if (thing is Pawn p && p.Dead) return p.Corpse != null && p.Corpse.MapHeld == volume;
            return thing.MapHeld == volume && !thing.Destroyed;
        }

        /// <summary>Touched: the target goes into the dimension, an enemy onto an island of its own.</summary>
        public bool Absorb(Thing target)
        {
            if (target == null || !target.Spawned || target == pawn) return false;
            Map inside = EnsureVolume();
            if (inside == null) return false;
            ObitoFX.Absorbed(pawn, target);
            if (target is Pawn victim)
            {
                IntVec3 landing = InvoluteUtility.LandingCell(inside, victim, pawn);
                if (!landing.IsValid) return false;
                passedThrough.Remove(victim);
                Move(victim, landing, inside, false);
                if (victim.HostileTo(Faction.OfPlayer)) victim.stances?.stunner.StunFor(MapComponent_KamuiDimension.HoldStunTicks, pawn, false, false);
            }
            else
            {
                IntVec3 mouth = InvoluteUtility.MouthCell(inside);
                target.DeSpawnOrDeselect();
                if (!GenPlace.TryPlaceThing(target, mouth, inside, ThingPlaceMode.Near)) return false;
            }
            if (!stored.Contains(target)) stored.Add(target);
            return true;
        }

        /// <summary>The release starts: the thing comes out at the cell once the picture has unwound it.</summary>
        public void BeginRelease(Thing thing, Map map, IntVec3 cell)
        {
            if (releaseTick >= 0 || thing == null || map == null) return;
            releasing = thing;
            releaseMap = map;
            releaseCell = cell;
            releaseTick = Find.TickManager.TicksGame + ObitoFX.ReleaseLandTicks;
            releaseReadyTick = Find.TickManager.TicksGame + Mathf.RoundToInt(ObitoRules.Store.releaseCooldownSeconds * 60f);
            ObitoFX.ReleaseStarted(pawn, thing, map, cell);
        }

        private void FinishRelease()
        {
            Thing thing = releasing;
            Map map = releaseMap;
            IntVec3 cell = releaseCell;
            releasing = null;
            releaseMap = null;
            releaseCell = IntVec3.Invalid;
            releaseTick = -1;
            if (thing == null || map == null || !Find.Maps.Contains(map) || !IsHeld(thing)) return;
            stored.Remove(thing);
            PutOut(thing, map, cell, Mathf.RoundToInt(ObitoRules.Store.releaseStunSeconds * 60f));
        }

        /// <summary>
        /// One thing out of the dimension onto a map. An enemy is stunned for <paramref name="stunTicks"/> and
        /// fights on: a new assault lord for its faction, without fleeing or kidnapping.
        /// </summary>
        public void PutOut(Thing thing, Map map, IntVec3 cell, int stunTicks)
        {
            if (thing is Pawn p && p.Dead && p.Corpse != null) thing = p.Corpse;
            if (thing is Pawn pawnOut)
            {
                Move(pawnOut, InvoluteUtility.FreeCellNear(map, cell), map, false);
                if (pawnOut.HostileTo(Faction.OfPlayer))
                {
                    pawnOut.stances?.stunner.StunFor(stunTicks, pawn, false, true);
                    if (pawnOut.GetLord() == null && pawnOut.Faction != null && pawnOut.RaceProps.Humanlike && !pawnOut.Downed)
                        LordMaker.MakeNewLord(pawnOut.Faction, new LordJob_AssaultColony(pawnOut.Faction, false, false, false, false, false), map,
                            new List<Pawn> { pawnOut });
                }
                return;
            }
            if (thing.Spawned) thing.DeSpawnOrDeselect();
            GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near);
        }

        /// <summary>
        /// Everything in the dimension comes out at <paramref name="cell"/>: his death, the gene's loss. Pawns,
        /// corpses and items; not the dimension's own lights or rounds still in flight.
        /// </summary>
        public void EmptyOut(Map map, IntVec3 cell)
        {
            if (volume == null || map == null || map == volume) return;
            exitTick = -1;
            releaseTick = -1;
            releasing = null;
            List<Thing> contents = volume.listerThings.AllThings
                .Where(t => t.Spawned && (t is Pawn || t.def.category == ThingCategory.Item)).ToList();
            foreach (Thing thing in contents)
                PutOut(thing, map, cell, Mathf.RoundToInt(ObitoRules.Store.releaseStunSeconds * 60f));
            stored.Clear();
            fromMap = null;
            fromCell = IntVec3.Invalid;
        }

        /// <summary>Anything left in the dimension that is not his: pawns and items, for his death and the loss of the kit.</summary>
        public bool HoldsAnything => volume != null && volume.listerThings.AllThings.Any(t => t.Spawned && t != pawn
            && (t is Pawn || t.def.category == ThingCategory.Item));

        // =============================================================================== ticking

        public override void Tick()
        {
            base.Tick();
            if (pawn == null || pawn.Dead) return;
            int now = Find.TickManager.TicksGame;
            if (now % 60 == 0) ObitoFX.Know(this);
            TickPhase(now);
            if (exitTick >= 0 && now >= exitTick) FinishExit();
            if (releaseTick >= 0 && now >= releaseTick) FinishRelease();
            if (pawn.IsHashIntervalTick(60))
            {
                // The form ended inside the dimension (revert, the pool emptied): he comes out where he went in.
                if (Inside && !ActiveNow) ExitToWhereHeLeft();
                if (passedThrough.Count > 0)
                {
                    float window = ObitoRules.Store.counterSeconds * 60f;
                    foreach (Pawn old in passedThrough.Where(e => now - e.Value > window || e.Key.Destroyed).Select(e => e.Key).ToList())
                        passedThrough.Remove(old);
                }
            }
        }

        private void TickPhase(int now)
        {
            CompProperties_AbilityKamuiPhase rules = ObitoRules.Phase;
            float max = rules.poolSeconds * 60f;
            if (pool < 0f) pool = max;
            switch (phase)
            {
                case KamuiPhaseState.Phased:
                    pool -= 1f;
                    if (pool <= 0f)
                    {
                        pool = 0f;
                        TurnSolid();
                        if (pawn.Faction == Faction.OfPlayer)
                            Messages.Message("AG_KamuiPhaseSpent".Translate(pawn.LabelShortCap), pawn, MessageTypeDefOf.NeutralEvent, false);
                    }
                    break;
                case KamuiPhaseState.TurningSolid:
                    pool = Mathf.Max(0f, pool - 1f);
                    if (now >= solidAtTick)
                    {
                        phase = KamuiPhaseState.Solid;
                        KamuiPhaseRegistry.Remove(this);
                    }
                    break;
                default:
                    if (pool < max) pool = Mathf.Min(max, pool + rules.refillPerSolidSecond);
                    break;
            }
            if (phase != KamuiPhaseState.Solid && (pawn.Downed || !pawn.Spawned || !ActiveNow)) ForceSolid();
        }

        /// <summary>Test hook: sets the pool, in seconds.</summary>
        public void SetPoolSeconds(float seconds) => pool = Mathf.Clamp(seconds * 60f, 0f, PoolMax);

        // =============================================================================== death and loss

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            ForceSolid();
            // The corpse is not made yet; the dimension's component empties it on its next check.
        }

        /// <summary>
        /// Where his dimension's contents go now that he is dead: where he lies, or, if he died inside, where
        /// he went in (his corpse goes out too).
        /// </summary>
        public void EmptyOutAfterDeath()
        {
            if (volume == null) return;
            Thing body = (Thing)pawn.Corpse ?? pawn;
            if (body.MapHeld == volume)
            {
                Map map = OutsideMap;
                if (map == null) return;
                EmptyOut(map, map == fromMap && fromCell.IsValid ? fromCell : map.Center);
                return;
            }
            if (body.MapHeld != null) EmptyOut(body.MapHeld, body.PositionHeld);
        }

        public override void PostRemove()
        {
            base.PostRemove();
            ForceSolid();
            ObitoFX.Forget(pawn);
            if (volume == null) return;
            // The kit is gone: the dimension's contents come out where he is, then the dimension closes.
            if (Inside) ExitToWhereHeLeft();
            if (pawn?.MapHeld != null && pawn.MapHeld != volume) EmptyOut(pawn.MapHeld, pawn.PositionHeld);
            Map doomed = volume;
            volume = null;
            PocketMapUtility.DestroyPocketMap(doomed);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref volume, "volume");
            Scribe_Values.Look(ref phase, "kamuiPhase", KamuiPhaseState.Solid);
            Scribe_Values.Look(ref pool, "kamuiPool", -1f);
            Scribe_Values.Look(ref solidAtTick, "kamuiSolidAt", -1);
            Scribe_Values.Look(ref phaseChangedTick, "kamuiPhaseChanged", -99999);
            Scribe_References.Look(ref fromMap, "warpFromMap");
            Scribe_Values.Look(ref fromCell, "warpFromCell", IntVec3.Invalid);
            Scribe_References.Look(ref exitMap, "warpExitMap");
            Scribe_Values.Look(ref exitCell, "warpExitCell", IntVec3.Invalid);
            Scribe_Values.Look(ref exitTick, "warpExitTick", -1);
            Scribe_Collections.Look(ref stored, "stored", LookMode.Reference);
            Scribe_References.Look(ref releasing, "releasing");
            Scribe_References.Look(ref releaseMap, "releaseMap");
            Scribe_Values.Look(ref releaseCell, "releaseCell", IntVec3.Invalid);
            Scribe_Values.Look(ref releaseTick, "releaseTick", -1);
            Scribe_Values.Look(ref releaseReadyTick, "releaseReadyTick", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (stored == null) stored = new List<Thing>();
                stored.RemoveAll(t => t == null);
                if (Intangible) KamuiPhaseRegistry.Add(this);
                ObitoFX.Know(this);
            }
        }
    }
}
