using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimArt
{
    /// <summary>
    /// Game tests for Unlimited Void (run with -quicktest -rimarttest=gojo: void): the take within 9 cells with offsets
    /// kept, the freeze and the unfrozen mech, the touch, the overload after a short Release and after the full
    /// 10 s (downed, then scarred), the return to matching cells and the pocket map's removal, the ends when Gojo
    /// goes down and when the Echo pool empties, and the goodwill a neutral visitor costs. Gojo is a drafted
    /// colonist given the ability with GainAbility (no Echo). Long waits are skipped with
    /// <see cref="Hediff_VoidOverload.SkipForTests"/>.
    /// </summary>
    public static class Tests_GojoVoid
    {
        private static AbilityDef Void => VoidDefOf.AG_GojoUnlimitedVoid;
        private static GameComponent_UnlimitedVoid Casts => GameComponent_UnlimitedVoid.Instance;

        private static void Setup(RimArtTestContext t)
        {
            Casts.ResetForTests();
            t.Clear();
        }

        /// <summary>Puts the camera back on the test map and drops any domain left standing, so the next test starts at home.</summary>
        private static void Cleanup(RimArtTestContext t)
        {
            Casts?.ResetForTests();
            if (Find.CurrentMap != t.map && Find.Maps.Contains(t.map)) CameraJumper.TryJump(t.center, t.map);
        }

        /// <summary>A drafted colonist with Unlimited Void who does not fire or fight on his own.</summary>
        private static Pawn Gojo(RimArtTestContext t)
        {
            Pawn gojo = t.Colonist(t.center);
            gojo.drafter.FireAtWill = false;
            gojo.abilities.GainAbility(Void);
            return gojo;
        }

        private static Pawn Colonist(RimArtTestContext t, IntVec3 offset)
        {
            Pawn pawn = t.Colonist(t.center + offset);
            pawn.drafter.FireAtWill = false;
            return pawn;
        }

        /// <summary>A visitor from a faction that is not hostile to the colony, told to stand still.</summary>
        private static Pawn Visitor(RimArtTestContext t, Faction faction, IntVec3 offset)
        {
            var request = new PawnGenerationRequest(faction.def.basicMemberKind ?? PawnKindDefOf.Villager, faction, dontGiveWeapon: true);
            Pawn pawn = PawnGenerator.GeneratePawn(request);
            GenSpawn.Spawn(pawn, t.center + offset, t.map);
            RimArtTestContext.Hold(pawn);
            return pawn;
        }

        /// <summary>Casts the ability and waits until everyone is in the pocket map. Null if it never got there.</summary>
        private static IEnumerable<int> CastAndTake(RimArtTestContext t, Pawn gojo, Action<UnlimitedVoidCast> got)
        {
            gojo.abilities.GetAbility(Void).QueueCastingJob(gojo, LocalTargetInfo.Invalid);
            UnlimitedVoidCast cast = null;
            foreach (int w in WaitFor(() => (cast = Casts.For(gojo)) != null && cast.Standing, 240, 1)) yield return w;
            got(cast != null && cast.Standing ? cast : null);
        }

        private static IEnumerable<int> WaitFor(Func<bool> done, int maxTicks, int step = 2)
        {
            for (int waited = 0; waited < maxTicks && !done(); waited += step) yield return step;
        }

        private static IEnumerable<int> WaitUntil(int tick)
        {
            while (Find.TickManager.TicksGame < tick) yield return 1;
        }

        private static Hediff_VoidOverload Overload(Pawn pawn) =>
            pawn.health.hediffSet.GetFirstHediffOfDef(VoidDefOf.AG_VoidOverload) as Hediff_VoidOverload;

        private static float OverloadOf(Pawn pawn) => Overload(pawn)?.Severity ?? 0f;
        private static bool Scarred(Pawn pawn) => pawn.health.hediffSet.HasHediff(VoidDefOf.AG_VoidScarred);
        private static float Consciousness(Pawn pawn) => pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
        private static bool Stunned(Pawn pawn) => pawn.stances?.stunner?.Stunned == true;

        private static void LogPawns(RimArtTestContext t, UnlimitedVoidCast cast, params Pawn[] pawns)
        {
            foreach (Pawn p in pawns)
            {
                string map = p.MapHeld == null ? "none" : p.MapHeld == t.map ? "home" : cast?.pocket == p.MapHeld ? "void" : "map " + p.MapHeld.uniqueID;
                t.Log(RimArtTestContext.Describe(p) + " map=" + map + " overload=" + OverloadOf(p).ToString("0.###")
                      + " consciousness=" + (p.Dead ? 0f : Consciousness(p)).ToString("0.##"));
            }
        }

        // ---- the take and the return --------------------------------------------------------------------------------

        [RimArtTest("Gojo", "void 1 takes everyone within 9 cells at their offsets, leaves one at 10, and brings them back to the same cells", 1500)]
        private static IEnumerable<int> TakeAndReturn(RimArtTestContext t)
        {
            Setup(t);
            yield return 5;
            try
            {
                Pawn gojo = Gojo(t);
                Pawn ally = Colonist(t, new IntVec3(3, 0, 2));
                Pawn edge = t.Enemy(t.center + new IntVec3(0, 0, -9), armed: false);
                Pawn far = t.Enemy(t.center + new IntVec3(10, 0, 0), armed: false);
                Pawn animal = null;
                PawnKindDef muffalo = DefDatabase<PawnKindDef>.GetNamedSilentFail("Muffalo");
                if (muffalo != null) animal = (Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(muffalo), t.center + new IntVec3(-4, 0, 4), t.map);
                var inside = new List<Pawn> { ally, edge };
                if (animal != null) inside.Add(animal);
                IntVec3 gojoFrom = gojo.Position, farFrom = far.Position;
                yield return 2;

                UnlimitedVoidCast cast = null;
                foreach (int w in CastAndTake(t, gojo, c => cast = c)) yield return w;
                LogPawns(t, cast, gojo, ally, edge, far);
                if (!t.Check(cast != null, "the domain opened (" + RimArtTestContext.Describe(gojo) + ")")) yield break;
                Map pocket = cast.pocket;
                IntVec3 middle = cast.Middle;
                t.Check(pocket != null && pocket.IsPocketMap && pocket.Size.x == 40 && pocket.Size.z == 40, "a 40 x 40 pocket map was made");
                t.Check(gojo.Map == pocket && gojo.Position == middle, "Gojo is in the middle of it");
                // Where each stood when the barrier closed (the animal may have wandered a cell before).
                var from = inside.ToDictionary(p => p, p => cast.Record(p)?.from ?? IntVec3.Invalid);
                foreach (Pawn p in inside)
                    t.Check(p.Map == pocket && p.Position == middle + (from[p] - gojoFrom),
                        p.LabelShort + " was taken at its offset " + (from[p] - gojoFrom) + " (at " + (p.Spawned ? (p.Position - middle).ToString() : "nowhere") + ")");
                t.Check(far.Map == t.map && far.Position == farFrom, "the enemy 10 cells away stayed home");
                t.Check(inside.All(p => cast.Record(p)?.frozen == true && Stunned(p)), "everyone taken with a brain is frozen");
                t.Check(!Stunned(gojo) && Overload(gojo) == null, "Gojo is not frozen");
                var comp = gojo.abilities.GetAbility(Void).CompOfType<CompAbilityEffect_UnlimitedVoid>();
                bool disabled = comp.GizmoDisabled(out string why);
                t.Check(!comp.CanCast && disabled, "the ability cannot be cast from inside (" + why + ")");

                yield return 30;
                cast.End("test");
                foreach (int w in WaitFor(() => cast.returnTick >= 0, 120, 1)) yield return w;
                LogPawns(t, cast, gojo, ally, edge, far);
                if (!t.Check(cast.returnTick >= 0, "everyone came back")) yield break;
                t.Check(gojo.Map == t.map && gojo.Position == gojoFrom, "Gojo is back on his cell " + gojoFrom);
                foreach (Pawn p in inside)
                    t.Check(p.MapHeld == t.map && p.Position == from[p], p.LabelShort + " is back on its cell " + from[p]);
                foreach (int w in WaitFor(() => !Find.Maps.Contains(pocket), 300)) yield return w;
                t.Check(!Find.Maps.Contains(pocket), "the pocket map was removed");
                t.Check(gojo.abilities.GetAbility(Void).CooldownTicksRemaining > 0, "the cooldown is spent");
            }
            finally
            {
                Cleanup(t);
            }
        }

        [RimArtTest("Gojo", "void 2 a flesh enemy is frozen, a mechanoid is taken but acts", 1500)]
        private static IEnumerable<int> MechActs(RimArtTestContext t)
        {
            Setup(t);
            yield return 5;
            try
            {
                Pawn gojo = Gojo(t);
                Pawn raider = t.Enemy(t.center + new IntVec3(0, 0, 4), armed: false);
                PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Mech_Scyther") ?? DefDatabase<PawnKindDef>.GetNamedSilentFail("Mech_Lancer");
                if (!t.Check(kind != null, "a mechanoid kind exists")) yield break;
                Pawn mech = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, Faction.OfMechanoids));
                GenSpawn.Spawn(mech, t.center + new IntVec3(-8, 0, -4), t.map);
                RimArtTestContext.Hold(mech);
                yield return 2;

                UnlimitedVoidCast cast = null;
                foreach (int w in CastAndTake(t, gojo, c => cast = c)) yield return w;
                if (!t.Check(cast != null, "the domain opened")) yield break;
                IntVec3 mechLanded = mech.Position;
                yield return 60;
                LogPawns(t, cast, gojo, raider, mech);
                t.Log("the mech moved " + mechLanded + " -> " + mech.Position + ", lord " + (mech.GetLord()?.LordJob?.GetType().Name ?? "none"));
                t.Check(mech.Map == cast.pocket, "the mechanoid was taken");
                t.Check(cast.Record(mech)?.frozen == false && !Stunned(mech) && Overload(mech) == null, "the mechanoid is not frozen and has no overload");
                t.Check(mech.GetLord()?.LordJob is LordJob_AssaultColony, "the hostile mechanoid fights inside (assault lord)");
                t.Check(cast.Record(raider)?.frozen == true && Stunned(raider), "the raider is frozen");
                t.Check(OverloadOf(raider) > 0.08f && Overload(raider).building, "the raider's overload builds (" + OverloadOf(raider).ToString("0.###") + " after 1 s)");
                cast.End("test");
                foreach (int w in WaitFor(() => cast.returnTick >= 0, 120, 1)) yield return w;
                t.Check(cast.returnTick >= 0 && (mech.Dead || mech.MapHeld == t.map), "the mechanoid came back too");
            }
            finally
            {
                Cleanup(t);
            }
        }

        // ---- the touch --------------------------------------------------------------------------------------------

        [RimArtTest("Gojo", "void 3 a colonist next to Gojo for 0.3 s is spared, an adjacent enemy and a far colonist are not", 1500)]
        private static IEnumerable<int> Touch(RimArtTestContext t)
        {
            Setup(t);
            yield return 5;
            try
            {
                Pawn gojo = Gojo(t);
                Pawn near = Colonist(t, new IntVec3(1, 0, 0));
                Pawn enemy = t.Enemy(t.center + new IntVec3(-1, 0, 0), armed: false);
                Pawn far = Colonist(t, new IntVec3(5, 0, 0));
                yield return 2;

                UnlimitedVoidCast cast = null;
                foreach (int w in CastAndTake(t, gojo, c => cast = c)) yield return w;
                if (!t.Check(cast != null, "the domain opened")) yield break;
                foreach (int w in WaitUntil(cast.takeTick + 30)) yield return w;
                VoidTaken nearRecord = cast.Record(near), enemyRecord = cast.Record(enemy), farRecord = cast.Record(far);
                t.Log("near spared at +" + (nearRecord.sparedTick - cast.takeTick) + " ticks, touch " + nearRecord.touchTicks + "; enemy touch " + enemyRecord.touchTicks);
                t.Check(nearRecord.Spared && nearRecord.sparedTick - cast.takeTick <= 20, "the adjacent colonist was spared after 0.3 s");
                t.Check(!enemyRecord.Spared, "the adjacent enemy was not spared");
                t.Check(!farRecord.Spared, "the colonist 5 cells away was not spared");
                t.Check(Stunned(near), "a spared pawn stays frozen");

                foreach (int w in WaitUntil(cast.takeTick + 60)) yield return w;
                cast.End("released");
                foreach (int w in WaitFor(() => cast.returnTick >= 0, 120, 1)) yield return w;
                LogPawns(t, cast, gojo, near, enemy, far);
                t.Check(Mathf.Abs(OverloadOf(near) - 0.03f) < 0.006f, "the spared colonist kept 3 % (" + OverloadOf(near).ToString("0.###") + ")");
                t.Check(Mathf.Abs(OverloadOf(enemy) - 0.10f) < 0.006f, "the enemy has 10 % after 1 s (" + OverloadOf(enemy).ToString("0.###") + ")");
                t.Check(Mathf.Abs(OverloadOf(far) - 0.10f) < 0.006f, "the far colonist has 10 % after 1 s (" + OverloadOf(far).ToString("0.###") + ")");
            }
            finally
            {
                Cleanup(t);
            }
        }

        // ---- the overload -----------------------------------------------------------------------------------------

        [RimArtTest("Gojo", "void 4 Release after 3 s: overload 30 %, consciousness 70 %, standing, no scar; gone after 60 s", 1500)]
        private static IEnumerable<int> ShortRelease(RimArtTestContext t)
        {
            Setup(t);
            yield return 5;
            try
            {
                Pawn gojo = Gojo(t);
                Pawn ally = Colonist(t, new IntVec3(0, 0, 3));
                Pawn enemy = t.Enemy(t.center + new IntVec3(4, 0, 0), armed: false);
                yield return 2;
                float allyBefore = Consciousness(ally), enemyBefore = Consciousness(enemy);

                UnlimitedVoidCast cast = null;
                foreach (int w in CastAndTake(t, gojo, c => cast = c)) yield return w;
                if (!t.Check(cast != null, "the domain opened")) yield break;
                foreach (int w in WaitUntil(cast.takeTick + 180)) yield return w;
                t.Check(ally.Map == cast.pocket && !ally.Downed, "inside, at 30 % the ally is frozen standing (no cap yet)");
                cast.End("released");
                foreach (int w in WaitFor(() => cast.returnTick >= 0, 120, 1)) yield return w;
                yield return 2;
                LogPawns(t, cast, gojo, ally, enemy);
                foreach (var (pawn, before) in new[] { (ally, allyBefore), (enemy, enemyBefore) })
                {
                    Hediff_VoidOverload overload = Overload(pawn);
                    t.Check(overload != null && !overload.building && Mathf.Abs(overload.Severity - 0.30f) < 0.01f,
                        pawn.LabelShort + ": overload 30 % (" + OverloadOf(pawn).ToString("0.###") + ")");
                    float want = Mathf.Min(before, 0.70f), level = Consciousness(pawn);
                    t.Check(level <= want + 0.005f && level >= want - 0.025f, pawn.LabelShort + ": consciousness about 70 % (" + level.ToString("0.##") + ", was " + before.ToString("0.##") + ")");
                    t.Check(!pawn.Downed, pawn.LabelShort + " is not downed");
                    t.Check(overload != null && !overload.scarOwed, pawn.LabelShort + " owes no scar");
                    overload?.SkipForTests(61f);
                }
                yield return 3;
                LogPawns(t, cast, ally, enemy);
                t.Check(Overload(ally) == null && Overload(enemy) == null, "60 s later the overload is gone");
                t.Check(!Scarred(ally) && !Scarred(enemy), "and nobody is scarred");
                t.Check(Mathf.Abs(Consciousness(ally) - allyBefore) < 0.01f, "the ally's consciousness is back (" + Consciousness(ally).ToString("0.##") + ")");
            }
            finally
            {
                Cleanup(t);
            }
        }

        [RimArtTest("Gojo", "void 5 the full 10 s: overload 100 %, consciousness 10 %, downed until 70 %, then void-scarred", 2400)]
        private static IEnumerable<int> FullDomain(RimArtTestContext t)
        {
            Setup(t);
            yield return 5;
            try
            {
                Pawn gojo = Gojo(t);
                Pawn ally = Colonist(t, new IntVec3(-3, 0, 0));
                Pawn enemy = t.Enemy(t.center + new IntVec3(3, 0, 0), armed: false);
                yield return 2;
                float allyBefore = Consciousness(ally);

                UnlimitedVoidCast cast = null;
                foreach (int w in CastAndTake(t, gojo, c => cast = c)) yield return w;
                if (!t.Check(cast != null, "the domain opened")) yield break;
                foreach (int w in WaitFor(() => cast.returnTick >= 0, 800, 5)) yield return w;
                yield return 2;
                LogPawns(t, cast, gojo, ally, enemy);
                if (!t.Check(cast.returnTick >= 0, "the domain ended by itself")) yield break;
                t.Check(cast.endReason == "time" && cast.endTick - cast.takeTick == 600, "it ended on time, 600 ticks after the take (" + (cast.endTick - cast.takeTick) + ", " + cast.endReason + ")");
                t.Check(ally.MapHeld == t.map && enemy.MapHeld == t.map, "both came back");
                foreach (Pawn pawn in new[] { ally, enemy })
                {
                    Hediff_VoidOverload overload = Overload(pawn);
                    t.Check(overload != null && overload.Severity >= 0.999f, pawn.LabelShort + ": overload 100 % (" + OverloadOf(pawn).ToString("0.###") + ")");
                    t.Check(Consciousness(pawn) <= 0.105f, pawn.LabelShort + ": consciousness 10 % (" + Consciousness(pawn).ToString("0.##") + ")");
                    t.Check(pawn.Downed, pawn.LabelShort + " is downed");
                    t.Check(overload != null && overload.scarOwed, pawn.LabelShort + " owes the scar");
                }

                // 59 s later the overload is just over 70 %: still down. Past 70 % it gets up.
                Hediff_VoidOverload allyOverload = Overload(ally);
                allyOverload.SkipForTests(59f);
                yield return 2;
                t.Check(ally.Downed && allyOverload.Severity > 0.7f, "59 s on, at " + allyOverload.Severity.ToString("0.###") + ", the ally is still down");
                allyOverload.SkipForTests(2f);
                yield return 2;
                t.Check(!ally.Downed && allyOverload.Severity < 0.7f, "61 s on, at " + allyOverload.Severity.ToString("0.###") + ", the ally is up (consciousness "
                                                                        + Consciousness(ally).ToString("0.##") + ")");
                t.Check(!Scarred(ally), "not scarred while the overload lasts");
                allyOverload.SkipForTests(150f);
                yield return 3;
                LogPawns(t, cast, ally);
                t.Check(Overload(ally) == null, "the overload is gone after 200 s");
                t.Check(Scarred(ally), "then the ally is void-scarred");
                t.Check(Mathf.Abs(Consciousness(ally) - (allyBefore - 0.15f)) < 0.02f, "scarred consciousness is 15 % lower (" + Consciousness(ally).ToString("0.##")
                                                                                     + ", was " + allyBefore.ToString("0.##") + ")");
                Hediff scar = ally.health.hediffSet.GetFirstHediffOfDef(VoidDefOf.AG_VoidScarred);
                HediffComp_Disappears lasts = scar?.TryGetComp<HediffComp_Disappears>();
                t.Check(lasts != null && lasts.ticksToDisappear > 119000, "the scar lasts 2 days (" + (lasts?.ticksToDisappear ?? 0) + " ticks)");
            }
            finally
            {
                Cleanup(t);
            }
        }

        // ---- the other ends ---------------------------------------------------------------------------------------

        [RimArtTest("Gojo", "void 6 the domain ends when Gojo is downed", 1500)]
        private static IEnumerable<int> GojoDowned(RimArtTestContext t)
        {
            Setup(t);
            yield return 5;
            try
            {
                Pawn gojo = Gojo(t);
                Pawn enemy = t.Enemy(t.center + new IntVec3(3, 0, 0), armed: false);
                IntVec3 gojoFrom = gojo.Position;
                yield return 2;

                UnlimitedVoidCast cast = null;
                foreach (int w in CastAndTake(t, gojo, c => cast = c)) yield return w;
                if (!t.Check(cast != null, "the domain opened")) yield break;
                foreach (int w in WaitUntil(cast.takeTick + 30)) yield return w;
                gojo.health.AddHediff(HediffDefOf.Anesthetic);
                t.Check(gojo.Downed, "Gojo is downed (anesthetic)");
                foreach (int w in WaitFor(() => cast.endTick >= 0, 10, 1)) yield return w;
                t.Check(cast.endTick >= 0 && cast.endReason == "Gojo went down", "the domain ended (" + cast.endReason + ")");
                foreach (int w in WaitFor(() => cast.returnTick >= 0, 120, 1)) yield return w;
                LogPawns(t, cast, gojo, enemy);
                t.Check(gojo.MapHeld == t.map && gojo.Position == gojoFrom, "Gojo came back on his cell, downed");
                t.Check(enemy.MapHeld == t.map, "the enemy came back");
                t.Check(OverloadOf(enemy) > 0.04f && OverloadOf(enemy) < 0.07f, "the enemy's overload stopped at about 5 % (" + OverloadOf(enemy).ToString("0.###") + ")");
            }
            finally
            {
                Cleanup(t);
            }
        }

        [RimArtTest("Gojo", "void 7 the domain ends when the Echo pool empties", 1500)]
        private static IEnumerable<int> PoolEmpties(RimArtTestContext t)
        {
            Setup(t);
            GameComponent_Echoes.Get.ResetForTests();
            yield return 5;
            try
            {
                Pawn gojo = Gojo(t);
                Pawn ally = Colonist(t, new IntVec3(0, 0, -3));
                yield return 2;

                UnlimitedVoidCast cast = null;
                foreach (int w in CastAndTake(t, gojo, c => cast = c)) yield return w;
                if (!t.Check(cast != null, "the domain opened")) yield break;
                foreach (int w in WaitUntil(cast.takeTick + 30)) yield return w;
                EchoUtility.EmptyPool();
                t.Check(cast.endTick >= 0 && cast.endReason == "the Echo pool emptied", "the domain ended (" + cast.endReason + ")");
                foreach (int w in WaitFor(() => cast.returnTick >= 0, 120, 1)) yield return w;
                t.Check(gojo.MapHeld == t.map && ally.MapHeld == t.map, "both came back");
            }
            finally
            {
                Cleanup(t);
            }
        }

        [RimArtTest("Gojo", "void 8 an unspared neutral visitor costs its faction 15 goodwill; a spared one costs none", 2400)]
        private static IEnumerable<int> Goodwill(RimArtTestContext t)
        {
            Setup(t);
            yield return 5;
            try
            {
                Faction faction = Find.FactionManager.RandomNonHostileFaction(allowNonHumanlike: false);
                if (!t.Check(faction != null, "there is a non-hostile faction")) yield break;
                Pawn gojo = Gojo(t);
                Pawn visitor = Visitor(t, faction, new IntVec3(1, 0, 0));
                yield return 2;

                int before = faction.GoodwillWith(Faction.OfPlayer);
                UnlimitedVoidCast cast = null;
                foreach (int w in CastAndTake(t, gojo, c => cast = c)) yield return w;
                if (!t.Check(cast != null, "the first domain opened")) yield break;
                foreach (int w in WaitUntil(cast.takeTick + 30)) yield return w;
                t.Check(cast.Record(visitor).Spared, "the visitor next to Gojo was spared");
                cast.End("released");
                foreach (int w in WaitFor(() => cast.returnTick >= 0, 120, 1)) yield return w;
                int afterSpared = faction.GoodwillWith(Faction.OfPlayer);
                t.Check(afterSpared == before, "a spared visitor costs nothing (" + before + " -> " + afterSpared + ")");

                // Again, with the visitor 3 cells off so it stays unspared.
                gojo.abilities.GetAbility(Void).ResetCooldown();
                if (visitor.Spawned && !visitor.Dead)
                {
                    visitor.DeSpawn();
                    GenSpawn.Spawn(visitor, t.center + new IntVec3(3, 0, 0), t.map);
                    RimArtTestContext.Hold(visitor);
                }
                foreach (int w in WaitFor(() => Casts.For(gojo) == null, 120)) yield return w;
                cast = null;
                foreach (int w in CastAndTake(t, gojo, c => cast = c)) yield return w;
                if (!t.Check(cast != null, "the second domain opened")) yield break;
                foreach (int w in WaitUntil(cast.takeTick + 30)) yield return w;
                cast.End("released");
                foreach (int w in WaitFor(() => cast.returnTick >= 0, 120, 1)) yield return w;
                int afterUnspared = faction.GoodwillWith(Faction.OfPlayer);
                // 15 before the game's own adjustments (CalculateAdjustedGoodwillChange), so only the direction is checked exactly.
                t.Check(!cast.Record(visitor).Spared && afterUnspared < afterSpared,
                    "an unspared visitor costs goodwill (" + afterSpared + " -> " + afterUnspared + ", 15 asked)");
            }
            finally
            {
                Cleanup(t);
            }
        }
    }
}
