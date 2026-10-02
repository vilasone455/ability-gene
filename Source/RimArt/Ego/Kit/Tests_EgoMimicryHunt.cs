using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using static RimArt.RimArtTestContext;
using static RimArt.Tests_EgoMimicry;

namespace RimArt
{
    /// <summary>
    /// Game tests of Mimicry's corroded hunt, its lunge, Overclock, a swing through a save and a load, and swings broken off,
    /// filter <c>ego: mimicry</c>. Setup and the trace lines are <see cref="Tests_EgoMimicry"/>'s.
    /// </summary>
    public static class Tests_EgoMimicryHunt
    {
        private static readonly FieldInfo LoadedObjects = AccessTools.Field(typeof(CrossRefHandler), "loadedObjectDirectory");

        private static GameComponent_EgoMimicry Game => Tests_EgoMimicry.Game;

        /// <summary>Out of any mental state, exhaustion taken off, drafted so it stands: ready to be corroded again.</summary>
        private static void Settle(Pawn pawn)
        {
            pawn.MentalState?.RecoverFromState();
            Hediff exhausted = pawn.health.hediffSet.GetFirstHediffOfDef(EgoDefOf.AG_EgoExhausted);
            if (exhausted != null) pawn.health.RemoveHediff(exhausted);
            pawn.drafter.Drafted = true;
        }

        /// <summary>Takes a pawn off the test, dead or alive.</summary>
        internal static void Remove(Pawn pawn)
        {
            if (pawn == null) return;
            if (pawn.Dead) pawn.Corpse?.Destroy();
            else if (!pawn.Destroyed) pawn.Destroy();
        }

        [RimArtTest("Ego", "mimicry: corroded, it hunts the nearest pawn it can reach, lunges at most 2 cells, keeps cutting a downed pawn, then turns to the next", 3600)]
        public static IEnumerable<int> Hunt(RimArtTestContext t)
        {
            t.Clear();
            Game.Clear();
            IntVec3 c = t.center;
            Pawn wielder = t.Colonist(c + new IntVec3(-5, 0, 0));
            CompEgoMimicry sword = Arm(t, wielder);
            Pawn downed = t.Down(t.Colonist(c + new IntVec3(2, 0, 0)));
            // Nearer than the downed colonist but walled in: never the target.
            IntVec3 cell = c + new IntVec3(-5, 0, 3);
            t.Room(cell, cell);
            Pawn walled = t.Colonist(cell);
            t.Log("start: " + RimArtTestContext.Describe(wielder) + " | " + State(downed) + " | walled in at " + cell);
            t.Check(EgoCorrosion.Nearest(wielder) == walled && EgoCorrosion.Nearest(wielder, sword) == downed,
                "the walled-in pawn is the nearest, but the nearest Mimicry can reach is the downed colonist");
            t.Check(EgoCorrosion.Corrode(wielder, sword), "corroded");
            t.Check(sword.stage == 1, "the arm starts at stage 1 (" + sword.stage + ")");

            var seen = new List<EgoMimicryCast>();
            int start = t.Now;
            foreach (int w in WaitFor(() => seen.Count(s => s.target == downed && s.resolved && s.landed) >= 3 || downed.Dead, 1500))
            {
                Watch(seen);
                if ((t.Now - start) % 30 == 0) t.Log("+" + (t.Now - start) + " " + RimArtTestContext.Describe(wielder) + " stage " + sword.stage + " | " + State(downed));
                yield return w;
            }
            foreach (EgoMimicryCast s in seen) t.Log("  " + Line(s));
            t.Check(seen.Count > 0 && seen.All(s => s.target == downed), "every swing went at the downed colonist, none at the walled-in one");
            // Whether a firing finds it 2 or 3 cells off depends on its walking pace; the lunge itself is the next test's.
            t.Log("lunges: " + string.Join(", ", seen.Where(s => s.Lunges).Select(s => s.lungeFrom.DistanceTo(s.lungeTo).ToString("0.00"))));
            t.Check(seen.All(s => !s.Lunges || s.lungeFrom.DistanceTo(s.lungeTo) <= 2.0001f), "every lunge was at most 2 cells");
            int cuts = seen.Count(s => s.target == downed && s.resolved && s.landed);
            t.Check(cuts >= 3 || downed.Dead, "it kept cutting the downed pawn (" + cuts + " hits" + (downed.Dead ? ", dead" : "") + ")");
            t.Check(sword.stage > 1, "its hits grew the arm (stage " + sword.stage + ")");
            t.Check(seen.All(s => s.source == EgoMimicrySource.Corroded && !s.grown), "corroded swings, never grown");

            // The next pawn: the downed one is gone, a hostile stands 3 cells off.
            Remove(downed);
            Pawn next = t.Target(wielder.Position + new IntVec3(0, 0, -3), stunTicks: 3000);
            seen.Clear();
            foreach (int w in WaitFor(() => seen.Any(s => s.target == next && s.resolved), 600))
            {
                Watch(seen);
                yield return w;
            }
            foreach (EgoMimicryCast s in seen) t.Log("  " + Line(s));
            t.Check(seen.Any(s => s.target == next), "it turned to the hostile");
            wielder.MentalState?.RecoverFromState();
            yield return 1;
            t.Check(sword.stage == 0, "the arm is gone when the corrosion ends");
        }

        [RimArtTest("Ego", "mimicry: the lunge lands next to its target and never through a wall, a diagonal gap or onto a pawn; a lunge broken off moves nobody and cuts nobody", 2400)]
        public static IEnumerable<int> Lunge(RimArtTestContext t)
        {
            t.Clear();
            Game.Clear();
            IntVec3 c = t.center;
            Pawn wielder = t.Colonist(c);
            CompEgoMimicry sword = Arm(t, wielder);
            Pawn target = t.Target(c + new IntVec3(3, 0, 0), stunTicks: 3000);
            float cells = sword.Props.lungeCells;
            t.Check(EgoMimicry.Landing(wielder, target, cells, out IntVec3 land) && land == c + new IntVec3(2, 0, 0), "open ground: it lands 2 cells on, next to the target (" + land + ")");
            Pawn standing = t.Colonist(c + new IntVec3(2, 0, 0));
            t.Check(!EgoMimicry.Landing(wielder, target, cells, out land), "with a pawn on the only cell in reach, no lunge (" + land + ")");
            standing.Destroy();
            Thing wall = t.Wall(c + new IntVec3(1, 0, 0));
            t.Check(!EgoMimicry.Landing(wielder, target, cells, out land), "with a wall on the way, no lunge (" + land + ")");
            wall.Destroy();
            Pawn diagonal = t.Target(c + new IntVec3(-2, 0, 2), stunTicks: 3000, faction: target.Faction);
            Thing w1 = t.Wall(c + new IntVec3(-1, 0, 0)), w2 = t.Wall(c + new IntVec3(0, 0, 1));
            t.Check(!EgoMimicry.Landing(wielder, diagonal, cells, out land), "between two walls on the diagonal, no lunge (" + land + ")");
            w2.Destroy();
            t.Check(!EgoMimicry.Landing(wielder, diagonal, cells, out land), "one wall on the corner still blocks the diagonal, as in Core's pathing (" + land + ")");
            w1.Destroy();
            t.Check(EgoMimicry.Landing(wielder, diagonal, cells, out land) && land == c + new IntVec3(-1, 0, 1), "with both gone it lands on the diagonal (" + land + ")");
            Remove(diagonal);

            // Broken off by the sword leaving the hands: the wielder stays, the target is untouched.
            t.Check(EgoCorrosion.Corrode(wielder, sword), "corroded");
            t.Check(EgoCorrosion.Fire(wielder, sword, target, hostilesOnly: false), "a firing at the target 3 cells off");
            EgoMimicryCast cast = Game.Latest(wielder);
            t.Check(cast != null && cast.Lunges && !cast.arrived, "it is lunging");
            yield return 3;
            t.Log("mid-lunge: " + RimArtTestContext.Describe(wielder) + " drawn at " + wielder.DrawPos.ToString("F2"));
            t.Check(wielder.Position == c && PawnDash.Running(wielder), "mid-lunge its cell has not changed and it is drawn moving");
            wielder.equipment.TryDropEquipment(sword.parent, out ThingWithComps dropped, wielder.Position);
            yield return 20;
            t.Log(Line(cast) + " | " + RimArtTestContext.Describe(wielder) + " | " + State(target));
            t.Check(cast.cancelled && wielder.Position == c && !PawnDash.Running(wielder) && t.Untouched(target), "dropped mid-lunge: broken off, it did not move, nothing landed");
            t.Check(!(wielder.MentalState is MentalState_EgoCorroded), "and the corrosion ended with the sword out of its hands");

            // The landing cell taken during the lunge: it stays, and the swing misses out of reach. Each phase has a fresh
            // wielder (a corrosion's exhaustion halves Moving, which downs a pawn with weak legs) and ends its corrosion as
            // soon as the swing lands, before the hunt's walk moves it (the swing holds it until then).
            Remove(wielder);
            wielder = t.Colonist(c);
            sword = Arm(t, wielder);
            t.Check(EgoCorrosion.Corrode(wielder, sword), "corroded again (" + RimArtTestContext.Describe(wielder) + ")");
            EgoCorrosion.Fire(wielder, sword, target, hostilesOnly: false);
            cast = Game.Latest(wielder);
            Pawn blocker = t.Target(cast.lungeTo, stunTicks: 3000, faction: target.Faction);
            foreach (int w in WaitFor(() => cast.resolved || cast.cancelled, 60)) yield return w;
            Settle(wielder);
            t.Log(Line(cast) + " | " + RimArtTestContext.Describe(wielder) + " | " + State(target) + " | " + State(blocker));
            t.Check(wielder.Position == c && cast.resolved && !cast.landed && t.Untouched(target) && t.Untouched(blocker),
                "with a pawn on the landing cell it stayed put, and the swing hit nobody");
            Remove(blocker);

            // A lunge that lands: the cell changes once, on arrival, and the swing goes at the target.
            Remove(wielder);
            wielder = t.Colonist(c);
            sword = Arm(t, wielder);
            t.Check(EgoCorrosion.Corrode(wielder, sword), "corroded again (" + RimArtTestContext.Describe(wielder) + ")");
            EgoCorrosion.Fire(wielder, sword, target, hostilesOnly: false);
            cast = Game.Latest(wielder);
            int moves = 0;
            IntVec3 last = wielder.Position;
            foreach (int w in WaitFor(() => cast.resolved || cast.cancelled, 60))
            {
                if (wielder.Position != last) { moves++; last = wielder.Position; }
                yield return w;
            }
            Settle(wielder);
            t.Log(Line(cast) + " | " + RimArtTestContext.Describe(wielder) + " | " + State(target));
            t.Check(cast.Lunges && wielder.Position == c + new IntVec3(2, 0, 0) && moves == 1 && cast.resolved,
                "it landed 2 cells on, its cell changing once, and swung (hit: " + cast.landed + ")");
        }

        [RimArtTest("Ego", "mimicry: Overclock swings 5 times standing still at the standing hostile in reach, never an ally or one out of reach, stops early without one, and costs 20 mood", 2400)]
        public static IEnumerable<int> Overclock(RimArtTestContext t)
        {
            t.Clear();
            Game.Clear();
            IntVec3 c = t.center;
            Pawn wielder = t.Colonist(c);
            CompEgoMimicry sword = Arm(t, wielder);
            Pawn foe = t.Target(c + new IntVec3(0, 0, -6), stunTicks: 3000);
            Pawn ally = t.Colonist(c + IntVec3.North);
            Pawn far = t.Target(c + new IntVec3(-3, 0, 0), stunTicks: 3000, faction: foe.Faction);
            Step(foe, c + IntVec3.East);
            int memories = wielder.needs.mood.thoughts.memories.NumMemoriesOfDef(EgoDefOf.AG_EgoOverclocked), rolls = EgoMimicry.rollsForTests;
            var command = new Command_EgoOverclock(sword, wielder);
            t.Check(!command.Disabled, "the button is on with a hostile next to the wielder");
            wielder.jobs.TryTakeOrderedJob(JobMaker.MakeJob(EgoDefOf.AG_EgoOverclock, sword.parent), JobTag.Misc);
            var seen = new List<EgoMimicryCast>();
            foreach (int w in WaitFor(() => wielder.CurJobDef != EgoDefOf.AG_EgoOverclock, 600))
            {
                Watch(seen);
                if (seen.Count > 0 && seen.Last().resolved) InjuryHeal.Heal(foe, 1000f);
                yield return w;
            }
            foreach (EgoMimicryCast s in seen) t.Log("  " + Line(s));
            t.Log(RimArtTestContext.Describe(wielder) + " | " + State(foe) + " | " + State(ally) + " | " + State(far));
            t.Check(seen.Count == 5 && seen.All(s => s.target == foe && s.source == EgoMimicrySource.Overclock), "5 swings, all at the hostile next to it");
            t.Check(seen.All(s => !s.Lunges && !s.grown) && wielder.Position == c, "standing still: no lunge, no grown swing");
            bool spaced = true;
            for (int i = 1; i < seen.Count; i++) spaced &= seen[i].swingTick - seen[i - 1].swingTick >= 59;
            t.Check(spaced, "one a second (" + string.Join(", ", seen.Select(s => s.swingTick.ToString())) + ")");
            t.Check(t.Untouched(ally) && t.Untouched(far), "the ally next to it and the hostile 3 cells off are untouched");
            t.Check(EgoMimicry.rollsForTests == rolls && !wielder.InMentalState, "Overclock never rolls Corrosion");
            Thought_Memory paid = wielder.needs.mood.thoughts.memories.Memories.Where(m => m.def == EgoDefOf.AG_EgoOverclocked).OrderBy(m => m.age).FirstOrDefault();
            t.Check(paid != null && paid.moodOffset == -20 && wielder.needs.mood.thoughts.memories.NumMemoriesOfDef(EgoDefOf.AG_EgoOverclocked) == memories + 1,
                "it cost -20 mood (" + paid?.moodOffset + ")");

            // Early end: the hostile is gone after the second swing; the job stops at the next firing and still pays.
            Step(foe, c + new IntVec3(0, 0, -6));
            foreach (int w in WaitFor(() => Free(wielder) && !Game.Busy(wielder), 120)) yield return w;
            Step(foe, c + IntVec3.East);
            wielder.jobs.TryTakeOrderedJob(JobMaker.MakeJob(EgoDefOf.AG_EgoOverclock, sword.parent), JobTag.Misc);
            seen.Clear();
            foreach (int w in WaitFor(() => wielder.CurJobDef != EgoDefOf.AG_EgoOverclock, 600))
            {
                Watch(seen);
                if (seen.Count(s => s.resolved) >= 2 && foe.Spawned) foe.DeSpawn();
                yield return w;
            }
            foreach (EgoMimicryCast s in seen) t.Log("  " + Line(s));
            t.Check(seen.Count == 2, "with no hostile left in reach it stopped after 2 swings (" + seen.Count + ")");
            t.Check(wielder.needs.mood.thoughts.memories.NumMemoriesOfDef(EgoDefOf.AG_EgoOverclocked) == memories + 2, "and still paid");
            t.Check(new Command_EgoOverclock(sword, wielder).Disabled, "the button is off with no hostile in reach (the one 3 cells off does not count)");
            Remove(foe);
        }

        /// <summary>
        /// <paramref name="cast"/> written to a file and read back as a save carries it. The pawns, the sword and the map are
        /// live, not in the file: they are put in the loader's directory, so the references resolve to them as after a real
        /// load. The saver's "referenced but not deep-saved" warnings are cleared for the same reason.
        /// </summary>
        private static EgoMimicryCast RoundTrip(EgoMimicryCast cast, params ILoadReferenceable[] live)
        {
            string path = Path.Combine(Path.GetTempPath(), "rimart-mimicry-cast.xml");
            Scribe.saver.InitSaving(path, "mimicryCastTest");
            try
            {
                Scribe_Deep.Look(ref cast, "cast");
                Scribe.saver.loadIDsErrorsChecker.Clear();
            }
            finally
            {
                Scribe.saver.FinalizeSaving();
            }
            EgoMimicryCast loaded = null;
            Scribe.loader.InitLoading(path);
            try
            {
                var directory = (LoadedObjectDirectory)LoadedObjects.GetValue(Scribe.loader.crossRefs);
                foreach (ILoadReferenceable thing in live.Distinct()) directory.RegisterLoaded(thing);
                Scribe_Deep.Look(ref loaded, "cast");
            }
            finally
            {
                Scribe.loader.FinalizeLoading();
            }
            return loaded;
        }

        /// <summary>The sword's saved data as the save writes it.</summary>
        private sealed class SwordData : IExposable
        {
            public CompEgoMimicry sword;
            public void ExposeData() => sword.PostExposeData();
        }

        [RimArtTest("Ego", "mimicry: a swing saved before contact lands once after the load, one saved after never lands again; dropping, swapping, going down, dying and leaving the map break a swing off", 3000)]
        public static IEnumerable<int> SaveAndBreak(RimArtTestContext t)
        {
            t.Clear();
            Game.Clear();
            float grow = Props.growChance;
            Props.growChance = 0f;
            try
            {
                IntVec3 c = t.center, park = c + new IntVec3(0, 0, -6);
                Pawn wielder = t.Colonist(c);
                CompEgoMimicry sword = Arm(t, wielder);
                Pawn foe = t.Target(park, stunTicks: 3000);
                NoWimp(foe);
                var casts = new List<EgoMimicryCast>();

                // Saved before contact: the loaded swing lands, once.
                foreach (int w in StrikeAt(t, wielder, foe, c + IntVec3.East, casts)) yield return w;
                EgoMimicryCast cast = casts.Last();
                if (!t.Check(cast != null, "a strike")) yield break;
                EgoMimicryCast loaded = RoundTrip(cast, wielder, sword.parent, foe, t.map);
                t.Log("before: " + Line(cast) + " | loaded: " + Line(loaded));
                t.Check(loaded != null && loaded.wielder == wielder && loaded.weapon == sword.parent && loaded.target == foe && loaded.home == t.map
                    && loaded.swingTick == cast.swingTick && loaded.aim == cast.aim && loaded.source == cast.source && !loaded.resolved,
                    "the swing round-trips: who, at whom, when, from where, not landed yet");
                Game.ReplaceForTests(cast, loaded);
                foreach (int w in WaitFor(() => loaded.resolved, 60)) yield return w;
                Step(foe, park);
                yield return 5;
                t.Log("after: " + Line(loaded) + " | " + State(foe));
                t.Check(loaded.landed && loaded.struck.Count == 1 && t.Struck(foe) && !cast.resolved, "the loaded swing landed once (the old one is gone)");

                // Saved after contact: it never lands again.
                InjuryHeal.Heal(foe, 1000f);
                foreach (int w in StrikeAt(t, wielder, foe, c + IntVec3.East, casts)) yield return w;
                cast = casts.Last();
                if (!t.Check(cast != null, "a second strike")) yield break;
                foreach (int w in WaitFor(() => cast.resolved, 60)) yield return w;
                t.Note(foe);
                loaded = RoundTrip(cast, wielder, sword.parent, foe, t.map);
                t.Check(loaded.resolved, "a swing saved after its contact frame loads as landed");
                Game.ReplaceForTests(cast, loaded);
                yield return 40;
                Step(foe, park);
                t.Log("after: " + Line(loaded) + " | " + State(foe));
                t.Check(loaded.struck.Count == 0 && !t.Struck(foe), "and lands nothing again");
                sword.stage = 3;
                string xml = Scribe.saver.DebugOutputFor(new SwordData { sword = sword });
                t.Check(xml.Contains("<armStage>3</armStage>"), "the arm's stage is saved with the sword");
                sword.stage = 0;

                // Broken off before contact: nothing lands, nothing rolls.
                string[] ways = { "dropped", "swapped", "downed", "killed", "left the map" };
                foreach (string way in ways)
                {
                    InjuryHeal.Heal(foe, 1000f);
                    t.Note(foe);
                    int rolls = EgoMimicry.rollsForTests;
                    foreach (int w in StrikeAt(t, wielder, foe, c + IntVec3.East, casts)) yield return w;
                    cast = casts.Last();
                    if (!t.Check(cast != null, "a strike before the wielder " + way)) yield break;
                    yield return 4;
                    switch (way)
                    {
                        case "dropped":
                            wielder.equipment.TryDropEquipment(sword.parent, out ThingWithComps dropped, wielder.Position);
                            break;
                        case "swapped":
                            t.Equip(wielder, ThingDef.Named("MeleeWeapon_Knife"));
                            break;
                        case "downed":
                            t.Down(wielder);
                            break;
                        case "killed":
                            wielder.Kill(null);
                            break;
                        case "left the map":
                            wielder.DeSpawn();
                            break;
                    }
                    yield return 20;
                    Step(foe, park);
                    t.Log(way + ": " + Line(cast) + " | " + State(foe));
                    t.Check(cast.cancelled && !cast.resolved && !t.Struck(foe) && EgoMimicry.rollsForTests == rolls, "a swing whose wielder " + way + " was broken off: nothing landed, no roll");
                    if (way == "dropped" || way == "swapped")
                    {
                        sword = Arm(t, wielder);
                        continue;
                    }
                    Remove(wielder);
                    wielder = t.Colonist(c);
                    sword = Arm(t, wielder);
                }
            }
            finally
            {
                Props.growChance = grow;
            }
        }
    }
}
