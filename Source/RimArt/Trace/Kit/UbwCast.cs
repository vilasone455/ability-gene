using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using T = RimArt.UbwCastTiming;

namespace RimArt
{
    /// <summary>
    /// Game time in seconds since a tick, for drawing: whole ticks plus the share of the next one that has
    /// gone by in real time at the current speed, so a picture on game time moves smoothly between ticks
    /// and stops when the game is paused. The rules never read this; they count whole ticks.
    /// </summary>
    internal static class UbwClock
    {
        private static int lastTick = -1;
        private static float lastReal;

        public static float Since(int tick)
        {
            TickManager ticks = Find.TickManager;
            int now = ticks.TicksGame;
            if (now != lastTick)
            {
                lastTick = now;
                lastReal = Time.realtimeSinceStartup;
            }
            float share = ticks.Paused ? 0f : Mathf.Clamp((Time.realtimeSinceStartup - lastReal) * 60f * ticks.TickRateMultiplier, 0f, 0.99f);
            return (now - tick + share) / 60f;
        }
    }

    /// <summary>One pawn taken into the world: the cell it stood on and the lord it belonged to.</summary>
    public sealed class UbwTaken : IExposable
    {
        public Pawn pawn;
        public IntVec3 from;
        public Lord lord;

        public void ExposeData()
        {
            // A lord that has ended is saved nowhere, and a reference to it would not resolve on load.
            if (Scribe.mode == LoadSaveMode.Saving && lord != null && (lord.Map == null || !lord.Map.lordManager.lords.Contains(lord))) lord = null;
            Scribe_References.Look(ref pawn, "pawn", true);
            Scribe_Values.Look(ref from, "from");
            Scribe_References.Look(ref lord, "lord");
        }
    }

    /// <summary>
    /// One cast of Unlimited Blade Works, from the chant to the return. The rules (proposed 2026-09-24, taken
    /// as placeholders 2026-09-25; the numbers are <see cref="UbwRules"/> on the ability def):
    ///
    /// - The caster stands and chants up to 3 verses (verseSeconds each). The player presses Release during a
    ///   verse and the world opens when that verse ends; after verse 3 it opens by itself. A chant that is
    ///   broken or cancelled before the release gives the cooldown and the Echo's cast charge back.
    /// - The ability comes from the Shirou Echo and is taken away when the Host reverts (by hand or when
    ///   the pool runs dry). Without it the chant breaks, a released world does not open, and a standing
    ///   world closes, as if the caster were downed.
    /// - Released after verse V, everyone standing within radiusByVerse[V] of the cell the chant began on
    ///   (allies, enemies, animals; the caster always) is taken into a 40 x 40 pocket map, each at its offset
    ///   from the caster, who lands in the middle. Downed pawns stay. The moment is the full white of the
    ///   cast picture (<see cref="T.Plan.Taken"/>).
    /// - Hostile pawns taken are put in an assault lord of their faction inside the world; the map edge is
    ///   the world's edge, nobody can leave.
    /// - The world stands worldSecondsByVerse[V] of game time, and ends early if the caster is downed, dies,
    ///   leaves or loses the ability, or on Close.
    /// - Then everyone alive goes back to the cell they were taken from (the nearest free cell), downed or
    ///   not, and back into the lord they had if it still exists (else hostiles get a new assault lord);
    ///   corpses and every item in the world drop round the cast point; the world is removed.
    ///
    /// The clocks: the home side runs on the chant's start tick (s in the cast picture's plan), the world on
    /// the take tick. The return is at the world's full white (the close plus <see cref="UbwWorldTiming.Close"/>);
    /// the home ring is told when that is, so its fire runs back in and its white peaks on the same tick.
    /// </summary>
    public sealed class UbwCast : IExposable
    {
        public Pawn caster;
        public Map home;
        /// <summary>The cell the chant began on: the middle of the circle, and where things drop on the return.</summary>
        public IntVec3 centre;
        /// <summary>When the ability queued the chant, and when the chant began (-1 until the job starts).</summary>
        public int queuedTick, chantTick = -1;
        /// <summary>The verse the player asked to release after (0: not asked), and the verse it was released after (0: still chanting).</summary>
        public int releaseAfter, verse;
        public Map world;
        public int takenTick = -1;
        /// <summary>When the close began on the world's clock (seconds since the take), or -1 while the world stands.</summary>
        public float closeAt = -1f;
        public bool closeOrdered, returned, broken, fizzled;
        /// <summary>The charge the Echo took when the ability fired; given back if the chant breaks.</summary>
        public float paid;
        public List<UbwTaken> taken = new List<UbwTaken>();
        private bool shookTaken, shookHome;

        /// <summary>The job has not started yet: the chant waits this long for it, then gives up.</summary>
        private const int QueueTimeout = 60;
        private const float Never = 1e6f;

        public bool Queued => !broken && chantTick < 0;
        public bool Chanting => !broken && chantTick >= 0 && verse == 0;
        public bool Released => !broken && !fizzled && verse > 0 && takenTick < 0;
        public bool Standing => takenTick >= 0 && !returned;
        /// <summary>The cast still holds the caster: queued, chanting, released, or its world stands.</summary>
        public bool Busy => Queued || Chanting || Released || Standing;

        private static UbwRules Rules => UbwRules.Of;

        public float Seconds(int now) => (now - chantTick) / 60f;
        public int VerseAt(float s) => Mathf.Clamp(Mathf.FloorToInt(s / T.VerseTime) + 1, 1, 3);
        private float TakenAt => T.For(verse).Taken;
        /// <summary>On the chant's clock: when everyone comes back (the world's white is full).</summary>
        private float ReturnAt => closeAt < 0f ? Never : TakenAt + closeAt + UbwWorldTiming.Close;
        /// <summary>How long the home ring burns low after the white clears, so the fire runs back in and its white peaks at <see cref="ReturnAt"/>.</summary>
        private float Hold => closeAt < 0f ? Never : ReturnAt - (T.Flare + T.Back + T.FlashUp) - T.For(verse).Clear;
        public float WorldSecondsLeft(int now) => takenTick < 0 ? Rules.WorldSecondsFor(verse) : Mathf.Max(0f, Rules.WorldSecondsFor(verse) - (now - takenTick) / 60f);

        public UbwCast() { }

        public UbwCast(Pawn caster, int now, float paid)
        {
            this.caster = caster;
            this.paid = paid;
            home = caster.Map;
            centre = caster.Position;
            queuedTick = now;
        }

        private Ability Ability => caster?.abilities?.GetAbility(UbwDefOf.AG_Trace_UnlimitedBladeWorks);

        // ---- the chant --------------------------------------------------------------------------------------------

        public void StartChant(int now)
        {
            chantTick = now;
            centre = caster.Position;
            home = caster.Map;
            T.Configure(Rules.radiusByVerse, Rules.verseSeconds);
        }

        /// <summary>The Release button: the world opens when the verse being said ends.</summary>
        public void AskRelease(int now)
        {
            if (Chanting && releaseAfter == 0) releaseAfter = VerseAt(Seconds(now));
        }

        private bool ChantHeld() =>
            caster != null && caster.Spawned && caster.Map == home && !caster.Dead && !caster.Downed && caster.CurJobDef == UbwDefOf.AG_UbwChant
            && Ability != null;

        /// <summary>The chant stops before the release: nothing opens, and the cooldown and the charge come back.</summary>
        public void Break(string why)
        {
            if (broken) return;
            broken = true;
            Ability ability = Ability;
            if (ability != null) ability.ResetCooldown();
            // Reverted: the Echo took the ability and kept its cooldown for the next manifest.
            else if (caster != null) GameComponent_Echoes.Get?.HostRecord(caster)?.grant.Forget(UbwDefOf.AG_Trace_UnlimitedBladeWorks);
            GameComponent_Echoes.Get?.Refund(paid);
            paid = 0f;
            if (caster != null && caster.Spawned && caster.CurJobDef == UbwDefOf.AG_UbwChant) caster.jobs.EndCurrentJob(JobCondition.InterruptForced);
            if (why != null && caster != null) Messages.Message(why, caster, MessageTypeDefOf.NeutralEvent, false);
        }

        private void Release(int v)
        {
            verse = v;
            world = UnlimitedBladeWorksMap.Make(home, new List<IntVec3> { IntVec3.Zero }, Rules.worldV2);
            if (world == null)
            {
                verse = 0;
                Break("Unlimited Blade Works: the world could not be made. No cooldown or charge spent.");
                return;
            }
            world.GetComponent<MapComponent_UnlimitedBladeWorks>().driven = true;
        }

        // ---- the take ---------------------------------------------------------------------------------------------

        private void Take(int now)
        {
            // Released, then downed, gone or reverted before the white: the world never opens, the cooldown and charge stay spent.
            if (caster == null || caster.Dead || caster.Downed || !caster.Spawned || caster.Map != home || Ability == null)
            {
                fizzled = true;
                if (world != null) UnlimitedBladeWorksMap.CloseLater(world);
                if (caster != null) Messages.Message("Unlimited Blade Works: " + caster.LabelShortCap + " lost the world before it opened.", MessageTypeDefOf.NegativeEvent, false);
                return;
            }

            float r = Rules.RadiusFor(verse);
            var pawns = new List<Pawn> { caster };
            foreach (Pawn p in home.mapPawns.AllPawnsSpawned)
                if (p != caster && !p.Dead && !p.Downed && (p.Position - centre).LengthHorizontalSquared <= r * r) pawns.Add(p);

            bool watching = Find.CurrentMap == home;
            var selected = new HashSet<Pawn>(Find.Selector.SelectedPawns);
            var component = world.GetComponent<MapComponent_UnlimitedBladeWorks>();
            IntVec3 middle = component.CentreCell;
            var keep = new List<IntVec3>();
            var hostile = new List<Pawn>();
            foreach (Pawn p in pawns)
            {
                IntVec3 want = p == caster ? middle : middle + (p.Position - centre);
                IntVec3 cell = FreeCellNear(world, want);
                Lord lord = p.GetLord();
                taken.Add(new UbwTaken { pawn = p, from = p.Position, lord = lord });
                lord?.RemovePawn(p);
                Move(p, cell, world);
                keep.Add(cell - middle);
                if (p.Faction != null && p.HostileTo(Faction.OfPlayer)) hostile.Add(p);
            }
            component.Landed(keep, now);
            takenTick = now;
            Assault(hostile, world);

            if (watching)
            {
                CameraJumper.TryJump(new GlobalTargetInfo(caster));
                foreach (Pawn p in pawns) if (selected.Contains(p)) Find.Selector.Select(p, false, false);
            }
            Messages.Message("Unlimited Blade Works: " + pawns.Count + " taken into the world for " + Rules.WorldSecondsFor(verse).ToString("0") + " s.",
                caster, MessageTypeDefOf.NeutralEvent, false);
        }

        // ---- the world stands, then closes -------------------------------------------------------------------------

        private bool CasterHolds() => caster != null && !caster.Dead && !caster.Downed && caster.Spawned && caster.Map == world && Ability != null;

        private void BeginClose(float w)
        {
            var component = world != null && Find.Maps.Contains(world) ? world.GetComponent<MapComponent_UnlimitedBladeWorks>() : null;
            closeAt = component != null ? component.CloseAt(w) : w;
        }

        // ---- the return -------------------------------------------------------------------------------------------

        private void Return()
        {
            returned = true;
            if (world == null || !Find.Maps.Contains(world)) return;
            Map to = home != null && Find.Maps.Contains(home) ? home : Find.AnyPlayerHomeMap;
            if (to == null)
            {
                Messages.Message("Unlimited Blade Works: there is no map to return to; the world stays open.", MessageTypeDefOf.NegativeEvent, false);
                return;
            }
            IntVec3 drop = to == home ? centre : to.Center;
            bool watching = Find.CurrentMap == world;
            var selected = new HashSet<Pawn>(Find.Selector.SelectedPawns);
            var moved = new List<Pawn>();
            var hostile = new List<Pawn>();
            var leaving = new List<Pawn>();

            // Everyone alive, back where they stood. A pawn carried by another is inside its carrier and goes with it.
            foreach (UbwTaken t in taken)
            {
                Pawn p = t.pawn;
                if (p == null || p.Destroyed || p.Dead || !p.Spawned || p.Map != world) continue;
                p.GetLord()?.RemovePawn(p);
                Move(p, FreeCellNear(to, to == home ? t.from : drop), to);
                moved.Add(p);
                if (t.lord != null && to.lordManager.lords.Contains(t.lord)) t.lord.AddPawn(p);
                else if (p.Faction != null && p.HostileTo(Faction.OfPlayer)) hostile.Add(p);
                else if (t.lord != null && p.Faction != null && p.Faction != Faction.OfPlayer) leaving.Add(p);
            }
            // Anyone else who is in the world by now, round the cast point.
            foreach (Pawn p in world.mapPawns.AllPawnsSpawned.ToList())
            {
                if (p.Dead) continue;
                p.GetLord()?.RemovePawn(p);
                Move(p, FreeCellNear(to, drop), to);
                moved.Add(p);
                if (p.Faction != null && p.HostileTo(Faction.OfPlayer)) hostile.Add(p);
            }
            // Corpses, dropped weapons and everything else lying in the world, round the cast point.
            foreach (Thing thing in world.listerThings.AllThings.ToList())
            {
                if (thing.Destroyed || !thing.Spawned || thing.def.category != ThingCategory.Item) continue;
                thing.DeSpawn();
                GenPlace.TryPlaceThing(thing, drop, to, ThingPlaceMode.Near);
            }
            Assault(hostile, to);
            foreach (IGrouping<Faction, Pawn> group in leaving.GroupBy(p => p.Faction))
                LordMaker.MakeNewLord(group.Key, new LordJob_ExitMapBest(LocomotionUrgency.Walk), to, group);

            if (watching)
            {
                CameraJumper.TryJump(new GlobalTargetInfo(drop, to));
                foreach (Pawn p in moved) if (selected.Contains(p)) Find.Selector.Select(p, false, false);
            }
            UnlimitedBladeWorksMap.CloseLater(world);
        }

        // ---- moving pawns -----------------------------------------------------------------------------------------

        /// <summary>Takes a pawn off its map and puts it on another, keeping it drafted if it was.</summary>
        private static void Move(Pawn p, IntVec3 cell, Map to)
        {
            bool drafted = p.Drafted;
            Rot4 facing = p.Rotation;
            p.DeSpawnOrDeselect();
            GenSpawn.Spawn(p, cell, to, facing);
            p.Notify_Teleported(true, true);
            if (drafted && p.drafter != null && !p.Downed) p.drafter.Drafted = true;
        }

        /// <summary>The nearest cell to <paramref name="want"/> a pawn can stand on with no other pawn on it.</summary>
        private static IntVec3 FreeCellNear(Map map, IntVec3 want)
        {
            want = new IntVec3(Mathf.Clamp(want.x, 1, map.Size.x - 2), 0, Mathf.Clamp(want.z, 1, map.Size.z - 2));
            int cells = GenRadial.NumCellsInRadius(8f);
            for (int i = 0; i < cells; i++)
            {
                IntVec3 c = want + GenRadial.RadialPattern[i];
                if (c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map) == null) return c;
            }
            return CellFinder.StandableCellNear(want, map, 20f);
        }

        /// <summary>Hostile pawns fight on: an assault lord for each faction, with no fleeing and no kidnapping.</summary>
        private static void Assault(List<Pawn> pawns, Map map)
        {
            foreach (IGrouping<Faction, Pawn> group in pawns.GroupBy(p => p.Faction))
                LordMaker.MakeNewLord(group.Key, new LordJob_AssaultColony(group.Key, false, false, false, false, false), map, group);
        }

        // ---- the clock --------------------------------------------------------------------------------------------

        /// <summary>One game tick. False once the cast is over and its picture has faded, so it can be dropped.</summary>
        public bool Tick(int now)
        {
            if (broken || fizzled) return false;
            if (Queued)
            {
                if (now - queuedTick > QueueTimeout) Break(null);
                return !broken;
            }
            float s = Seconds(now);
            if (Chanting)
            {
                if (!ChantHeld())
                {
                    Break("Unlimited Blade Works: the chant broke. No cooldown or charge spent.");
                    return false;
                }
                if (releaseAfter > 0 && s >= releaseAfter * T.VerseTime) Release(releaseAfter);
                else if (s >= 3 * T.VerseTime) Release(3);
                return !broken;
            }
            if (Released)
            {
                if (s >= TakenAt) Take(now);
                return !fizzled;
            }
            if (Standing)
            {
                float w = (now - takenTick) / 60f;
                if (closeAt < 0f && (closeOrdered || w >= Rules.WorldSecondsFor(verse) || !CasterHolds())) BeginClose(w);
                if (closeAt >= 0f && w >= closeAt + UbwWorldTiming.Close) Return();
                return true;
            }
            return s < T.For(verse, T.Run, Hold).End;
        }

        // ---- the home side's picture --------------------------------------------------------------------------------

        public void Draw()
        {
            if (broken || fizzled || chantTick < 0 || home == null) return;
            float s = UbwClock.Since(chantTick);
            var o = new Vector2(centre.x + 0.5f, centre.z + 0.5f);
            if (verse == 0)
            {
                int v = VerseAt(s);
                UbwCastGraphics.Draw(o, v, Mathf.Min(s, v * T.VerseTime - 0.001f), home, Never);
                return;
            }
            UbwCastGraphics.Draw(o, verse, s, home, Hold);
            if (!shookTaken && s >= TakenAt)
            {
                shookTaken = true;
                Find.CameraDriver.shaker.DoShake(T.TakenShake);
            }
            if (!shookHome && s >= ReturnAt)
            {
                shookHome = true;
                Find.CameraDriver.shaker.DoShake(T.HomeShake);
            }
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref caster, "caster", true);
            Scribe_References.Look(ref home, "home");
            Scribe_Values.Look(ref centre, "centre");
            Scribe_Values.Look(ref queuedTick, "queuedTick");
            Scribe_Values.Look(ref chantTick, "chantTick", -1);
            Scribe_Values.Look(ref releaseAfter, "releaseAfter");
            Scribe_Values.Look(ref verse, "verse");
            Scribe_References.Look(ref world, "world");
            Scribe_Values.Look(ref takenTick, "takenTick", -1);
            Scribe_Values.Look(ref closeAt, "closeAt", -1f);
            Scribe_Values.Look(ref closeOrdered, "closeOrdered");
            Scribe_Values.Look(ref returned, "returned");
            Scribe_Values.Look(ref broken, "broken");
            Scribe_Values.Look(ref fizzled, "fizzled");
            Scribe_Values.Look(ref paid, "paid");
            Scribe_Collections.Look(ref taken, "taken", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (taken == null) taken = new List<UbwTaken>();
                shookTaken = shookHome = true;
            }
        }
    }
}
