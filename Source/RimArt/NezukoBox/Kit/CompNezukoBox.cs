using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimArt
{
    [DefOf]
    public static class NezukoBoxDefOf
    {
        public static ThingDef AG_NezukoBox;
        public static ThingDef AG_NezukoBoxLeap;
        public static AbilityDef AG_NezukoBoxGoIn;
        public static JobDef AG_CastNezukoBox;
        public static HediffDef AG_NezukoBoxSleep;
        public static HediffDef AG_NezukoBoxLoad;
        public static HediffDef AG_NezukoBoxRested;

        static NezukoBoxDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(NezukoBoxDefOf));
    }

    /// <summary>The box's rules. Every number is a placeholder set in AG_NezukoBox's XML.</summary>
    public class CompProperties_NezukoBox : CompProperties
    {
        /// <summary>The largest body size that fits (every adult human is 1.0).</summary>
        public float maxBodySize = 1f;
        /// <summary>The wearer's move speed drops by this x the body size inside.</summary>
        public float slowPerBodySize = 0.15f;
        /// <summary>The wearer never drops below this share of its move speed.</summary>
        public float minMoveFactor = 0.3f;
        /// <summary>The longest a pawn stays inside, hours; at the limit it steps out by itself.</summary>
        public float maxHoursInside = 12f;
        /// <summary>Once fully rested the pawn steps out within this many hours (a random time).</summary>
        public float wakeWithinHours = 1f;
        /// <summary>Rest at or above this counts as fully rested.</summary>
        public float restedLevel = 0.99f;
        /// <summary>Rest gained inside is sleeping on the ground's (no bed): 0.8 rest effectiveness.</summary>
        public float restEffectiveness = 0.8f;
        /// <summary>A pawn that came out cannot go back into any box for this many hours (AG_NezukoBoxRested).</summary>
        public float reentryHours = 2f;
        /// <summary>Come out's leap: the farthest landing cell from the wearer.</summary>
        public float leapRange = 4f;
        /// <summary>The strike on landing stuns the enemy this long.</summary>
        public float stunSeconds = 1.5f;

        public int MaxTicksInside => Mathf.RoundToInt(maxHoursInside * GenDate.TicksPerHour);
        public int WakeWithinTicks => Mathf.RoundToInt(wakeWithinHours * GenDate.TicksPerHour);
        public int ReentryTicks => Mathf.RoundToInt(reentryHours * GenDate.TicksPerHour);
        public int StunTicks => Mathf.RoundToInt(stunSeconds * 60f);

        public CompProperties_NezukoBox()
        {
            compClass = typeof(CompNezukoBox);
        }
    }

    /// <summary>
    /// The box worn on the back, holding at most one pawn.
    ///
    /// The pawn inside is in <see cref="inner"/>, off the map. Worn apparel is never ticked, so
    /// <see cref="GameComponent_NezukoBox"/> ticks every full box: it ticks the pawn (health, needs,
    /// disease and immunity run), keeps its rest filling as sleeping on the ground does and its
    /// recreation where it was, and lets it out at the time limit, when rested, or when the box is no
    /// longer on a living wearer. AG_NezukoBoxSleep stops bleeding and hunger and keeps cold and heat
    /// away; the holder is an <see cref="IThingHolderWithDrawnPawn"/> lying down, so wounds heal at the
    /// lying-down rate. Damage and sunlight cannot reach a pawn that is not on the map.
    ///
    /// Leaving is started by the Come out gizmo or by the rules and played by
    /// <see cref="MapComponent_NezukoBox"/>, which calls <see cref="TakeOut"/> or
    /// <see cref="ReleaseAt"/> at the picture's moment. A game saved in between keeps the pawn inside.
    /// </summary>
    public class CompNezukoBox : ThingComp, IThingHolderWithDrawnPawn
    {
        private ThingOwner<Thing> inner;
        private int enteredTick = -1, wakeTick = -1;
        private float joyLevel = -1f;
        /// <summary>Where the box last was on a map, for letting the pawn out if the box is destroyed.</summary>
        private IntVec3 lastCell = IntVec3.Invalid;
        private Map lastMap;
        private Game registeredIn;

        private static readonly List<CompNezukoBox> full = new List<CompNezukoBox>();

        public CompNezukoBox()
        {
            inner = new ThingOwner<Thing>(this, true, LookMode.Deep);
        }

        public CompProperties_NezukoBox Props => (CompProperties_NezukoBox)props;

        public Pawn Wearer => (parent as Apparel)?.Wearer;

        /// <summary>The pawn inside, or null.</summary>
        public Pawn Sleeper
        {
            get
            {
                for (int i = 0; i < inner.Count; i++)
                    if (inner[i] is Pawn p) return p;
                return null;
            }
        }

        public bool Full => Sleeper != null;
        public int TicksInside => Full ? Find.TickManager.TicksGame - enteredTick : 0;

        /// <summary>The box worn by <paramref name="pawn"/>, or null.</summary>
        public static CompNezukoBox WornBy(Pawn pawn)
        {
            List<Apparel> worn = pawn?.apparel?.WornApparel;
            if (worn == null) return null;
            for (int i = 0; i < worn.Count; i++)
            {
                CompNezukoBox box = worn[i].TryGetComp<CompNezukoBox>();
                if (box != null) return box;
            }
            return null;
        }

        /// <summary>The box <paramref name="pawn"/> is inside, or null.</summary>
        public static CompNezukoBox Holding(Pawn pawn) => pawn?.ParentHolder as CompNezukoBox;

        // ------------------------------------------------------------------ IThingHolder

        public ThingOwner GetDirectlyHeldThings() => inner;

        public void GetChildHolders(List<IThingHolder> outChildren) => ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());

        public float HeldPawnDrawPos_Y => parent.DrawPos.y;
        public float HeldPawnBodyAngle => 0f;
        /// <summary>Lying down: Pawn_HealthTracker heals a pawn that is not standing at 12 a day instead of 8, as on the ground.</summary>
        public PawnPosture HeldPawnPosture => PawnPosture.LayingOnGroundNormal;

        // ------------------------------------------------------------------ going in

        /// <summary>Why <paramref name="target"/> cannot go into this box worn by <paramref name="wearer"/>, or null if it can.</summary>
        public string CannotTake(Pawn target, Pawn wearer)
        {
            if (target == null || target == wearer) return "Target another pawn.";
            if (Full) return "The box is full: " + Sleeper.LabelShort + " is inside.";
            if (!target.Spawned || target.Dead) return "Not here.";
            if (!target.RaceProps.IsFlesh) return "Only living flesh fits in the box.";
            if (target.BodySize > Props.maxBodySize + 0.001f)
                return target.LabelShort + " is too big: body size " + target.BodySize.ToString("0.##") + ", the box takes " + Props.maxBodySize.ToString("0.##") + ".";
            Hediff rested = target.health.hediffSet.GetFirstHediffOfDef(NezukoBoxDefOf.AG_NezukoBoxRested);
            if (rested != null)
            {
                int left = rested.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear ?? 0;
                return target.LabelShort + " came out of a box recently; can go in again in " + left.ToStringTicksToPeriod() + ".";
            }
            if (!target.Downed)
            {
                bool colony = target.Faction == Faction.OfPlayer || target.IsSlaveOfColony;
                if (!colony || target.IsPrisoner) return "Only colonists, slaves, colony animals and downed pawns go in.";
                if (target.InMentalState) return target.LabelShort + " is in a mental break.";
            }
            CompNezukoBox own = WornBy(target);
            if (own != null && own.Full) return target.LabelShort + " is carrying someone in a box.";
            return null;
        }

        /// <summary>Puts <paramref name="target"/> in the box now. It is taken off the map.</summary>
        public bool Take(Pawn target)
        {
            if (target == null || Full) return false;
            Map map = target.MapHeld;
            if (target.Spawned)
            {
                target.jobs?.StopAll();
                target.pather?.StopDead();
                if (target.drafter != null && target.Drafted) target.drafter.Drafted = false;
                target.DeSpawn(DestroyMode.WillReplace);
            }
            if (!inner.TryAdd(target, false))
            {
                if (map != null && !target.Spawned) GenSpawn.Spawn(target, lastCell.IsValid ? lastCell : Wearer?.PositionHeld ?? IntVec3.Zero, map);
                return false;
            }
            enteredTick = Find.TickManager.TicksGame;
            wakeTick = -1;
            joyLevel = target.needs?.joy?.CurLevel ?? -1f;
            if (!target.health.hediffSet.HasHediff(NezukoBoxDefOf.AG_NezukoBoxSleep))
                target.health.AddHediff(NezukoBoxDefOf.AG_NezukoBoxSleep);
            Register();
            SyncLoad();
            return true;
        }

        // ------------------------------------------------------------------ coming out

        /// <summary>Whether the pawn inside leaps out with a strike (a colony pawn that is not downed) or comes out plainly.</summary>
        public bool Leaps(Pawn sleeper) =>
            sleeper != null && !sleeper.Downed && (sleeper.Faction == Faction.OfPlayer || sleeper.IsSlaveOfColony) && !sleeper.IsPrisoner;

        /// <summary>Takes the pawn out of the box without placing it (for the leap's flyer).</summary>
        public Pawn TakeOut()
        {
            Pawn pawn = Sleeper;
            if (pawn == null) return null;
            inner.Remove(pawn);
            Woke(pawn);
            return pawn;
        }

        /// <summary>Lets the pawn out on <paramref name="cell"/> (or the nearest cell it can stand on).</summary>
        public Pawn ReleaseAt(IntVec3 cell, Map map, Rot4? facing = null)
        {
            Pawn pawn = Sleeper;
            if (pawn == null || map == null) return null;
            inner.Remove(pawn);
            Woke(pawn);
            IntVec3 at = StandableNear(cell, map, pawn);
            GenSpawn.Spawn(pawn, at, map, facing ?? Rot4.South);
            DropOthers(at, map);
            return pawn;
        }

        /// <summary>A pawn that died inside is a corpse: it is dropped with the rest of what is inside.</summary>
        private void DropOthers(IntVec3 cell, Map map)
        {
            for (int i = inner.Count - 1; i >= 0; i--)
                inner.TryDrop(inner[i], cell, map, ThingPlaceMode.Near, out _);
        }

        private void Woke(Pawn pawn)
        {
            // Inside, the job tracker gives the pawn Core's Carried job; a flyer would restore it on landing.
            pawn.jobs?.StopAll();
            Hediff sleep = pawn.health.hediffSet.GetFirstHediffOfDef(NezukoBoxDefOf.AG_NezukoBoxSleep);
            if (sleep != null) pawn.health.RemoveHediff(sleep);
            if (!pawn.Dead)
            {
                Hediff rested = pawn.health.AddHediff(NezukoBoxDefOf.AG_NezukoBoxRested);
                rested.TryGetComp<HediffComp_Disappears>()?.SetDuration(Props.ReentryTicks);
            }
            enteredTick = -1;
            wakeTick = -1;
            joyLevel = -1f;
            SyncLoad();
        }

        internal static IntVec3 StandableNear(IntVec3 cell, Map map, Pawn pawn)
        {
            if (cell.InBounds(map) && cell.Standable(map)) return cell;
            if (CellFinder.TryFindRandomCellNear(cell, map, 3, c => c.Standable(map) && !c.Fogged(map), out IntVec3 near)) return near;
            return cell.InBounds(map) ? cell : map.Center;
        }

        /// <summary>The cell just outside the door: behind the wearer, or the nearest standable one.</summary>
        internal static IntVec3 OutsideDoor(Pawn wearer)
        {
            IntVec3 behind = wearer.Position - wearer.Rotation.FacingCell;
            return StandableNear(behind, wearer.Map, wearer);
        }

        // ------------------------------------------------------------------ the wearer

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            SyncLoad();
            pawn.MapHeld?.GetComponent<MapComponent_NezukoBox>()?.Register(pawn);
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            // The pawn inside is let out on the next tick, where the box now is.
            RemoveLoad(pawn);
        }

        /// <summary>The wearer's AG_NezukoBoxLoad: move speed x (1 - slowPerBodySize x the body size inside).</summary>
        public void SyncLoad()
        {
            Pawn wearer = Wearer;
            if (wearer?.health == null || wearer.Dead) return;
            Pawn sleeper = Sleeper;
            Hediff_NezukoBoxLoad load = wearer.health.hediffSet.GetFirstHediffOfDef(NezukoBoxDefOf.AG_NezukoBoxLoad) as Hediff_NezukoBoxLoad;
            if (sleeper == null)
            {
                if (load != null) wearer.health.RemoveHediff(load);
                return;
            }
            if (load == null)
            {
                load = (Hediff_NezukoBoxLoad)HediffMaker.MakeHediff(NezukoBoxDefOf.AG_NezukoBoxLoad, wearer);
                load.Set(Props, sleeper.BodySize);
                wearer.health.AddHediff(load);
            }
            else load.Set(Props, sleeper.BodySize);
        }

        private static void RemoveLoad(Pawn pawn)
        {
            Hediff load = pawn?.health?.hediffSet?.GetFirstHediffOfDef(NezukoBoxDefOf.AG_NezukoBoxLoad);
            if (load != null) pawn.health.RemoveHediff(load);
        }

        // ------------------------------------------------------------------ ticking

        private void Register()
        {
            registeredIn = Current.Game;
            if (!full.Contains(this)) full.Add(this);
        }

        /// <summary>Called every tick by GameComponent_NezukoBox for each full box of this game.</summary>
        internal static void TickAll()
        {
            for (int i = full.Count - 1; i >= 0; i--)
            {
                if (i >= full.Count) continue;
                CompNezukoBox box = full[i];
                if (box.registeredIn != Current.Game || box.inner.Count == 0)
                {
                    full.RemoveAt(i);
                    continue;
                }
                box.TickInner();
            }
        }

        private void TickInner()
        {
            Map mapHeld = parent.MapHeld;
            if (mapHeld != null)
            {
                lastMap = mapHeld;
                lastCell = parent.PositionHeld;
            }
            Pawn sleeper = Sleeper;
            Pawn wearer = Wearer;

            // Off a living wearer (dropped, stripped, the wearer died): out on the spot.
            if (wearer == null || wearer.Dead)
            {
                if (mapHeld != null) ReleaseAt(parent.PositionHeld, mapHeld);
                else if (wearer != null && wearer.GetCaravan() is Caravan caravan) ReleaseTo(caravan);
                return;
            }
            if (sleeper == null)
            {
                // A pawn that died inside: drop the corpse.
                if (mapHeld != null) DropOthers(parent.PositionHeld, mapHeld);
                return;
            }

            // Before the pawn's tick: Need_Rest counts a pawn as resting only if TickResting was called
            // this tick or the one before, and its interval runs inside the pawn's tick.
            sleeper.needs?.rest?.TickResting(Props.restEffectiveness);
            inner.DoTick();
            if (sleeper.Dead || Sleeper == null) return;
            if (joyLevel >= 0f && sleeper.needs?.joy != null && sleeper.IsHashIntervalTick(150)) sleeper.needs.joy.CurLevel = joyLevel;

            if (!wearer.Spawned || MapComponent_NezukoBox.Exiting(this)) return;
            int now = Find.TickManager.TicksGame;
            bool timeUp = now - enteredTick >= Props.MaxTicksInside;
            Need_Rest rest = sleeper.needs?.rest;
            if (rest != null && rest.CurLevel >= Props.restedLevel)
            {
                if (wakeTick < 0) wakeTick = now + Rand.Range(0, Props.WakeWithinTicks + 1);
            }
            bool rested = wakeTick >= 0 && now >= wakeTick;
            if (timeUp || rested)
                wearer.Map.GetComponent<MapComponent_NezukoBox>()?.StartExit(wearer, this, sleeper.Downed ? NezukoExit.Downed : NezukoExit.TimeUp, IntVec3.Invalid);
        }

        private void ReleaseTo(Caravan caravan)
        {
            Pawn pawn = Sleeper;
            if (pawn == null) return;
            inner.Remove(pawn);
            Woke(pawn);
            caravan.AddPawn(pawn, true);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            Map map = previousMap ?? lastMap;
            if (inner.Count > 0 && map != null && lastCell.IsValid)
            {
                if (Sleeper != null) ReleaseAt(lastCell, map);
                DropOthers(lastCell, map);
            }
        }

        // ------------------------------------------------------------------ test hooks

        /// <summary>Test-only: moves the entry time back, so a test need not wait out the hours.</summary>
        internal void AgeInside(int ticks)
        {
            if (Full) enteredTick -= ticks;
        }

        /// <summary>Test-only: the wake time once rested, or -1.</summary>
        internal int WakeTick => wakeTick;

        // ------------------------------------------------------------------ UI

        public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetWornGizmosExtra()) yield return g;
            Pawn wearer = Wearer;
            Pawn sleeper = Sleeper;
            if (wearer == null || sleeper == null || wearer.Faction != Faction.OfPlayer) yield break;
            if (Leaps(sleeper))
            {
                var leap = new Command_Action
                {
                    defaultLabel = "Come out",
                    defaultDesc = "Wake " + sleeper.LabelShort + ": it bursts out of the top of the box and leaps to a cell within " + Props.leapRange.ToString("0.#")
                        + " tiles. If an enemy stands next to where it lands, it makes one melee attack that cannot miss and stuns that enemy for "
                        + Props.stunSeconds.ToString("0.#") + " seconds. Works while the wearer is downed.\n\n" + StatusLine(sleeper),
                    icon = NezukoBoxTextures.ComeOut,
                    action = () => BeginLeapTargeting(wearer),
                };
                string why = CannotExitNow(wearer);
                if (why != null) leap.Disable(why);
                yield return leap;
            }
            else
            {
                var letOut = new Command_Action
                {
                    defaultLabel = "Let out",
                    defaultDesc = "Open the door and let " + sleeper.LabelShort + " out next to the box" + (sleeper.Downed ? ", laid down." : ".")
                        + " Pawns not of the colony and downed pawns come out this way.\n\n" + StatusLine(sleeper),
                    icon = NezukoBoxTextures.LetOut,
                    action = () => wearer.Map.GetComponent<MapComponent_NezukoBox>()?.StartExit(wearer, this, sleeper.Downed ? NezukoExit.Downed : NezukoExit.TimeUp, IntVec3.Invalid),
                };
                string why = CannotExitNow(wearer);
                if (why != null) letOut.Disable(why);
                yield return letOut;
            }
        }

        private string CannotExitNow(Pawn wearer)
        {
            if (!wearer.Spawned) return "The wearer is not on a map.";
            if (MapComponent_NezukoBox.Exiting(this)) return "Already coming out.";
            return null;
        }

        /// <summary>Who is inside, for how long, and how rested.</summary>
        public string StatusLine(Pawn sleeper)
        {
            string line = "Inside: " + sleeper.LabelShort + " (body size " + sleeper.BodySize.ToString("0.##") + "), asleep "
                + TicksInside.ToStringTicksToPeriod() + " of " + Props.MaxTicksInside.ToStringTicksToPeriod() + ".";
            Need_Rest rest = sleeper.needs?.rest;
            if (rest != null) line += " Rest " + rest.CurLevelPercentage.ToStringPercent() + ".";
            return line;
        }

        public override string CompInspectStringExtra()
        {
            Pawn sleeper = Sleeper;
            return sleeper == null ? "Empty." : StatusLine(sleeper);
        }

        /// <summary>A valid landing cell for the leap: in range and in sight of the wearer, and a cell the pawn can land on.</summary>
        public bool ValidLanding(Pawn wearer, IntVec3 cell, out string reason)
        {
            reason = null;
            Map map = wearer?.Map;
            if (map == null || !cell.InBounds(map)) { reason = "Not on the map."; return false; }
            if (cell.DistanceTo(wearer.Position) > Props.leapRange + 0.001f) { reason = "Out of range."; return false; }
            if (cell == wearer.Position) { reason = "Pick a cell next to or away from the wearer."; return false; }
            Pawn sleeper = Sleeper;
            if (!JumpUtility.ValidJumpTarget(sleeper ?? (Thing)wearer, map, cell)) { reason = "Cannot land there."; return false; }
            if (!GenSight.LineOfSight(wearer.Position, cell, map)) { reason = "No line of sight."; return false; }
            return true;
        }

        private void BeginLeapTargeting(Pawn wearer)
        {
            var parms = new TargetingParameters { canTargetLocations = true, canTargetPawns = true, canTargetBuildings = false, canTargetItems = false, mapObjectTargetsMustBeAutoAttackable = false };
            Find.Targeter.BeginTargeting(parms,
                target =>
                {
                    if (!ValidLanding(wearer, target.Cell, out string reason))
                    {
                        Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                        return;
                    }
                    wearer.Map.GetComponent<MapComponent_NezukoBox>()?.StartExit(wearer, this, NezukoExit.Strike, target.Cell);
                },
                target =>
                {
                    if (target.IsValid && ValidLanding(wearer, target.Cell, out _)) GenDraw.DrawTargetHighlight(target.Cell);
                },
                target => target.IsValid && ValidLanding(wearer, target.Cell, out _),
                wearer, null, NezukoBoxTextures.ComeOut, true,
                null,
                target =>
                {
                    if (wearer.Spawned) GenDraw.DrawRadiusRing(wearer.Position, Props.leapRange);
                });
        }

        // ------------------------------------------------------------------ saving

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref inner, "inner", this);
            Scribe_Values.Look(ref enteredTick, "enteredTick", -1);
            Scribe_Values.Look(ref wakeTick, "wakeTick", -1);
            Scribe_Values.Look(ref joyLevel, "joyLevel", -1f);
            Scribe_Values.Look(ref lastCell, "lastCell", IntVec3.Invalid);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (inner == null) inner = new ThingOwner<Thing>(this, true, LookMode.Deep);
                if (inner.Count > 0) Register();
            }
        }
    }

    /// <summary>Ticks every full box: worn apparel is not ticked by the game.</summary>
    public sealed class GameComponent_NezukoBox : GameComponent
    {
        public GameComponent_NezukoBox(Game game) { }

        public override void GameComponentTick() => CompNezukoBox.TickAll();
    }

    /// <summary>
    /// The wearer's load: severity is the body size inside, and the one stage is made here, move speed
    /// times 1 - slowPerBodySize x body size (the box's XML), because a def stage cannot read a number
    /// off the box. Hediff_VacuumFull's shape.
    /// </summary>
    public class Hediff_NezukoBoxLoad : Hediff
    {
        private float slowPerBodySize = 0.15f, minMoveFactor = 0.3f;
        private HediffStage stage;
        private StatModifier moveFactor;

        public void Set(CompProperties_NezukoBox props, float bodySize)
        {
            slowPerBodySize = props.slowPerBodySize;
            minMoveFactor = props.minMoveFactor;
            Severity = Mathf.Max(0.01f, bodySize);
        }

        public float MoveFactor => Mathf.Max(minMoveFactor, 1f - slowPerBodySize * Severity);

        public override HediffStage CurStage
        {
            get
            {
                if (stage == null)
                {
                    moveFactor = new StatModifier { stat = StatDefOf.MoveSpeed, value = 1f };
                    stage = new HediffStage { statFactors = new List<StatModifier> { moveFactor } };
                }
                moveFactor.value = MoveFactor;
                return stage;
            }
        }

        public override bool ShouldRemove => false;

        public override string LabelInBrackets => "move " + ((MoveFactor - 1f) * 100f).ToString("0") + "%";

        public override string TipStringExtra
        {
            get
            {
                string text = base.TipStringExtra;
                CompNezukoBox box = CompNezukoBox.WornBy(pawn);
                Pawn sleeper = box?.Sleeper;
                if (sleeper == null) return text;
                return (text.NullOrEmpty() ? "" : text + "\n") + box.StatusLine(sleeper);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref slowPerBodySize, "slowPerBodySize", 0.15f);
            Scribe_Values.Look(ref minMoveFactor, "minMoveFactor", 0.3f);
        }
    }

    [StaticConstructorOnStartup]
    internal static class NezukoBoxTextures
    {
        internal static readonly Texture2D ComeOut = ContentFinder<Texture2D>.Get("RimArt/NezukoBox/IconComeOut");
        internal static readonly Texture2D LetOut = ContentFinder<Texture2D>.Get("RimArt/NezukoBox/IconLetOut");
    }

    /// <summary>The kit's sounds: vanilla placeholders by name, so a missing one is silent, not an error.</summary>
    internal static class NezukoBoxSounds
    {
        internal static void Play(string defName, IntVec3 cell, Map map)
        {
            if (map == null) return;
            DefDatabase<SoundDef>.GetNamedSilentFail(defName)?.PlayOneShot(new TargetInfo(cell, map));
        }

        internal const string DoorOpen = "Door_OpenManual", In = "Hive_Spawn", Latch = "Door_CloseManual", Burst = "BulletImpact_Wood", Kick = "Pawn_Melee_Punch_HitPawn";
    }
}
