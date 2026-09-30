using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using static RimArt.RimArtTestContext;

namespace RimArt
{
    /// <summary>
    /// Game tests for Shikamaru's kit (run with -quicktest -rimarttest=shadow). Light is overridden
    /// (<see cref="ShadowLight.levelForTests"/>) so a scenario does not depend on the hour; abilities
    /// are fired with Activate, which skips the warm-up but pays the Echo charge.
    /// </summary>
    public static class Tests_ShadowPlexus
    {
        private static readonly IntVec3 East = new IntVec3(1, 0, 0), North = new IntVec3(0, 0, 1);

        private static MapComponent_ShadowPlexus Plexus(RimArtTestContext t) => MapComponent_ShadowPlexus.Of(t.map);

        /// <summary>A cleared arena lit everywhere, with a manifested, drafted Shikamaru at the centre.</summary>
        private static Pawn Setup(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = t.ClearEchoes();
            ShadowLight.levelForTests = _ => 1f;
            Plexus(t)?.ReleaseAll();
            Pawn shikamaru = t.Host(ShadowPlexusDefOf.AG_Echo_Shikamaru, t.center, out EchoRecord record);
            return shikamaru;
        }

        private static void TearDown()
        {
            EchoDevice.workingForTests = null;
            ShadowLight.levelForTests = null;
        }

        /// <summary>
        /// The pawn's ability. Its effect comps are touched first: Ability.CanApplyOn(LocalTargetInfo)
        /// reads the lazily built list straight from the field, so an ability never activated or drawn
        /// would say yes to everything.
        /// </summary>
        private static Ability Of(Pawn pawn, AbilityDef def)
        {
            Ability ability = pawn.abilities.GetAbility(def);
            _ = ability?.EffectComps;
            return ability;
        }

        private static void Cast(Pawn pawn, AbilityDef def, LocalTargetInfo target) => Of(pawn, def).Activate(target, LocalTargetInfo.Invalid);

        private static void Cast(Pawn pawn, AbilityDef def, LocalTargetInfo target, LocalTargetInfo dest) => Of(pawn, def).Activate(target, dest);

        private static Pawn Enemy(RimArtTestContext t, int dx, int dz)
        {
            return t.Target(t.center + new IntVec3(dx, 0, dz));
        }

        private static Thing Steel(RimArtTestContext t, int dx, int dz) =>
            GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Steel), t.center + new IntVec3(dx, 0, dz), t.map);

        /// <summary>Moves the caster one cell the way a walked step does: a new cell, the path follower told.</summary>
        private static void Step(Pawn pawn, IntVec3 delta)
        {
            pawn.Position += delta;
            pawn.Notify_Teleported(false, false);
        }

        private static Hediff Choked(Pawn pawn) => pawn.health.hediffSet.GetFirstHediffOfDef(ShadowPlexusDefOf.AG_ShadowChoked);

        // ------------------------------------------------------------------ the line

        [RimArtTest("Shadow Plexus", "line 0 a pawn standing on a line is found by the line check")]
        private static IEnumerable<int> LineCrossing(RimArtTestContext t)
        {
            Pawn shikamaru = Setup(t);
            Pawn enemy = Enemy(t, 8, 0);
            Pawn walker = t.Colonist(t.center + East * 4);
            yield return 2;
            IntVec3 a = shikamaru.Position, b = enemy.Position;
            t.Log("cells: " + string.Join(" ", GenSight.PointsOnLineOfSight(a, b).Select(c => c.ToString())));
            t.Log("walker " + RimArtTestContext.Describe(walker) + " size " + walker.BodySize + " downed " + walker.Downed + " dead " + walker.Dead);
            t.Log("things at +4: " + string.Join(", ", (t.center + East * 4).GetThingList(t.map).Select(x => x.LabelShort + " (" + x.GetType().Name + ")")));
            t.Log("min crossing size " + ShadowPlexusExtension.Get.minCrossingBodySize + ", walkable " + (t.center + East * 4).Walkable(t.map));
            LineBreak r = ShadowLines.Check(t.map, a, b, shikamaru, enemy, true, true, out IntVec3 at, out Pawn by);
            t.Log("check: " + r + " at " + at + " by " + by?.LabelShort);
            LineBreak r2 = ShadowLines.Check(t.map, a, b, null, null, false, false, out IntVec3 at2, out Pawn by2);
            t.Log("check without light or smoke: " + r2 + " at " + at2 + " by " + by2?.LabelShort);
            t.Check(r == LineBreak.Crossed && by == walker, "the pawn on the line is found");
            TearDown();
        }

        // ------------------------------------------------------------------ Imitation

        [RimArtTest("Shadow Plexus", "imitation 1 holds a pawn, drags it with each step, lets go at 15 s and costs 3 charge")]
        private static IEnumerable<int> ImitationHoldAndDrag(RimArtTestContext t)
        {
            Pawn shikamaru = Setup(t);
            Pawn enemy = Enemy(t, 6, 0);
            IntVec3 start = enemy.Position;
            yield return 2;
            Cast(shikamaru, ShadowPlexusDefOf.AG_ShadowImitation, enemy);
            yield return 2;
            MapComponent_ShadowPlexus plexus = Plexus(t);
            ShadowHold hold = plexus.HoldOf(shikamaru, enemy);
            t.Check(hold != null, "the hold exists");
            t.Check(Stunned(enemy), "the target is stunned: " + RimArtTestContext.Describe(enemy));
            t.Check(GameComponent_Echoes.Get.charge == 97f, "3 charge paid (" + GameComponent_Echoes.Get.charge + ")");

            for (int k = 0; k < 3; k++)
            {
                Step(shikamaru, North);
                yield return 12;
                t.Log("step " + (k + 1) + ": " + RimArtTestContext.Describe(enemy));
            }
            t.Check(enemy.Position == start + North * 3, "the target was dragged 3 cells north (" + enemy.Position + " from " + start + ")");

            int ticksLeft = hold.endTick - t.Now + 8;
            yield return ticksLeft;
            t.Check(plexus.HoldOf(shikamaru, enemy) == null, "the hold is over after 15 s");
            t.Check(!Stunned(enemy), "the stun is over: " + RimArtTestContext.Describe(enemy));
            TearDown();
        }

        [RimArtTest("Shadow Plexus", "imitation 2 a pawn crossing the line cuts it; a dark cell under it kills it")]
        private static IEnumerable<int> ImitationLineBreaks(RimArtTestContext t)
        {
            Pawn shikamaru = Setup(t);
            Pawn enemy = Enemy(t, 8, 0);
            yield return 2;
            Cast(shikamaru, ShadowPlexusDefOf.AG_ShadowImitation, enemy);
            yield return 2;
            MapComponent_ShadowPlexus plexus = Plexus(t);
            ShadowHold hold = plexus.HoldOf(shikamaru, enemy);
            t.Check(hold != null && Stunned(enemy), "held and stunned");
            Pawn walker = t.Colonist(t.center + East * 4);
            walker.drafter.Drafted = false;
            RimArtTestContext.Hold(walker);
            yield return 3;
            t.Check(plexus.HoldOf(shikamaru, enemy) == null, "the hold ended when a pawn stood on the line");
            t.Check(hold.end == ImitationEnd.Cut, "it ended as Cut (" + hold.end + ")");
            t.Check(!Stunned(enemy), "the stun stopped: " + RimArtTestContext.Describe(enemy));
            walker.Destroy();
            yield return 2;

            Cast(shikamaru, ShadowPlexusDefOf.AG_ShadowImitation, enemy);
            yield return 2;
            ShadowHold second = plexus.HoldOf(shikamaru, enemy);
            t.Check(second != null, "held again");
            IntVec3 dark = t.center + East * 4;
            ShadowLight.levelForTests = c => c == dark ? 0.1f : 1f;
            yield return 3;
            t.Check(plexus.HoldOf(shikamaru, enemy) == null && second.end == ImitationEnd.Dark, "a dark cell under the line ended it as Dark (" + second.end + ")");
            TearDown();
        }

        [RimArtTest("Shadow Plexus", "imitation 3 in the dark nothing can be cast and reach is 0")]
        private static IEnumerable<int> ImitationDark(RimArtTestContext t)
        {
            Pawn shikamaru = Setup(t);
            Pawn enemy = Enemy(t, 3, 0);
            yield return 2;
            Ability imitation = Of(shikamaru, ShadowPlexusDefOf.AG_ShadowImitation);
            t.Check(imitation.CanApplyOn((LocalTargetInfo)enemy), "in full light a pawn 3 cells away can be held");
            ShadowLight.levelForTests = _ => 0.2f;
            t.Check(imitation.GizmoDisabled(out string reason) && reason == "AG_ShadowNoLight".Translate(), "greyed out in the dark (" + reason + ")");
            t.Check(!imitation.CanApplyOn((LocalTargetInfo)enemy), "and the pawn cannot be held");
            ShadowLight.levelForTests = _ => 0.5f;
            t.Check(ShadowPlexusCast.Reach(shikamaru, 19.9f) > 9.9f && ShadowPlexusCast.Reach(shikamaru, 19.9f) < 10f, "at 50 % light the reach is 9.95 (" + ShadowPlexusCast.Reach(shikamaru, 19.9f) + ")");
            Pawn far = Enemy(t, 11, 0);
            yield return 1;
            t.Check(!imitation.CanApplyOn((LocalTargetInfo)far) && imitation.CanApplyOn((LocalTargetInfo)enemy), "a pawn at 11 is out of reach, one at 3 is in");
            TearDown();
        }

        // ------------------------------------------------------------------ Seam

        [RimArtTest("Shadow Plexus", "seam 1 the leash: a walker drags the stack, the still one is pulled, the smaller body loses")]
        private static IEnumerable<int> SeamLeash(RimArtTestContext t)
        {
            Pawn shikamaru = Setup(t);
            // a stands off the caster's line to the stack: a pawn on that line would refuse the cast.
            Pawn a = Enemy(t, 2, 2);
            Thing stack = Steel(t, 4, 0);
            yield return 2;
            Cast(shikamaru, ShadowPlexusDefOf.AG_ShadowSeam, a, stack);
            yield return 2;
            MapComponent_ShadowPlexus plexus = Plexus(t);
            t.Check(plexus.SeamOf(shikamaru, a) != null && plexus.SeamOf(shikamaru, stack) != null, "the seam holds both");
            bool within = true;
            for (int k = 0; k < 8; k++)
            {
                Step(a, East);
                yield return 2;
                within &= a.Position.DistanceTo(stack.Position) <= 4f;
                t.Log("walk " + (k + 1) + ": a " + a.Position + " stack " + stack.Position + " apart " + a.Position.DistanceTo(stack.Position).ToString("0.0"));
            }
            t.Check(within, "the stack stayed within 4 cells every step");
            t.Check(stack.Spawned && stack.Position != t.center + East * 4, "the stack was dragged (" + stack.Position + ")");
            plexus.ReleaseAll();
            stack.Destroy();
            a.Destroy();
            yield return 2;

            // Two pawns 4 apart, neither on the caster's line to the other: only the mover moves, the
            // other is pulled after it.
            Pawn b = Enemy(t, 2, 3);
            Pawn c = Enemy(t, -2, 3);
            yield return 2;
            Cast(shikamaru, ShadowPlexusDefOf.AG_ShadowSeam, b, c);
            yield return 2;
            t.Check(plexus.SeamOf(shikamaru, b) != null, "the two pawns are sewn");
            IntVec3 bStart = b.Position;
            for (int k = 0; k < 5; k++) { Step(c, -East); yield return 2; }
            t.Check(b.Position != bStart && b.Position.DistanceTo(c.Position) <= 4f, "the still pawn was pulled after the walker (" + b.Position + " to " + c.Position + ")");
            plexus.ReleaseAll();
            b.Destroy();
            c.Destroy();
            yield return 2;

            // Both move apart in one tick: the item (size 0) is pulled, the pawn keeps its step. The
            // seam runs along z = 2, clear of the caster: a pawn on the seam, him included, would cut it.
            Pawn d = Enemy(t, -3, 2);
            Thing crate = Steel(t, 1, 2);
            yield return 2;
            Cast(shikamaru, ShadowPlexusDefOf.AG_ShadowSeam, d, crate);
            yield return 2;
            t.Check(plexus.SeamOf(shikamaru, crate) != null, "the pawn and the crate are sewn");
            Step(d, -East);
            crate.Position += East;
            IntVec3 dMoved = d.Position;
            yield return 2;
            t.Check(d.Position == dMoved, "the pawn kept its step (" + d.Position + ")");
            t.Check(crate.Position.DistanceTo(d.Position) <= 4f, "the item was pulled back within 4 (" + crate.Position + ")");
            TearDown();
        }

        // ------------------------------------------------------------------ Grasp

        [RimArtTest("Shadow Plexus", "grasp 1 slides an item to the cell, stops in a pawn's cell, drags a downed body")]
        private static IEnumerable<int> GraspSlide(RimArtTestContext t)
        {
            Pawn shikamaru = Setup(t);
            Thing steel = Steel(t, 5, 0);
            IntVec3 dest = t.center + East * 12;
            yield return 2;
            Cast(shikamaru, ShadowPlexusDefOf.AG_ShadowGrasp, steel, dest);
            // 0.5 s cast + 0.22 s hand + 7 cells at 12 cells/s (5 ticks each) = 78 ticks.
            yield return 100;
            t.Check(steel.Spawned && steel.Position == dest, "the steel slid to the chosen cell (" + steel.Position + ")");
            t.Check(Plexus(t).SlideOf(shikamaru) == null, "the slide is over");
            steel.Destroy();

            Thing second = Steel(t, 5, 0);
            Pawn blocker = Enemy(t, 9, 0);
            yield return 2;
            Cast(shikamaru, ShadowPlexusDefOf.AG_ShadowGrasp, second, dest);
            yield return 100;
            t.Check(second.Position == blocker.Position, "a pawn on the path stops it in that pawn's cell (" + second.Position + ", pawn " + blocker.Position + ")");
            second.Destroy();
            blocker.Destroy();

            Pawn downed = Enemy(t, 5, 3);
            downed.health.AddHediff(HediffMaker.MakeHediff(EchoDefOf.AG_EchoCollapse, downed));
            yield return 2;
            t.Check(downed.Downed, "the enemy is downed: " + RimArtTestContext.Describe(downed));
            IntVec3 bodyDest = t.center + new IntVec3(12, 0, 3);
            Ability grasp = Of(shikamaru, ShadowPlexusDefOf.AG_ShadowGrasp);
            t.Check(grasp.CanApplyOn((LocalTargetInfo)downed), "a downed pawn can be picked");
            Cast(shikamaru, ShadowPlexusDefOf.AG_ShadowGrasp, downed, bodyDest);
            // 7 cells at 6 cells/s = 70 ticks, after the 43-tick hand.
            yield return 140;
            t.Check(downed.Spawned && downed.Position == bodyDest && !downed.Dead, "the body was dragged to the cell (" + RimArtTestContext.Describe(downed) + ")");
            Pawn standing = Enemy(t, 3, -3);
            yield return 1;
            t.Check(!grasp.CanApplyOn((LocalTargetInfo)standing), "a standing pawn cannot be picked");
            TearDown();
        }

        // ------------------------------------------------------------------ Double

        [RimArtTest("Shadow Plexus", "double 1 the other abilities are cast from the double, which copies his steps")]
        private static IEnumerable<int> DoubleOrigin(RimArtTestContext t)
        {
            Pawn shikamaru = Setup(t);
            // Half light: his own reach is 9.95, the double's the same from where it stands. Everything
            // stays inside the cleared arena (12 cells from the centre).
            ShadowLight.levelForTests = _ => 0.5f;
            IntVec3 spot = t.center + East * 5;
            Pawn enemy = Enemy(t, 11, 0);
            yield return 2;
            Ability imitation = Of(shikamaru, ShadowPlexusDefOf.AG_ShadowImitation);
            t.Check(!imitation.CanApplyOn((LocalTargetInfo)enemy), "a pawn 11 cells away is out of his own reach at half light");
            Cast(shikamaru, ShadowPlexusDefOf.AG_ShadowDouble, spot);
            yield return 2;
            MapComponent_ShadowPlexus plexus = Plexus(t);
            t.Check(plexus.DoubleOf(shikamaru) != null, "the double is out");
            t.Check(!plexus.CastsFromDouble(shikamaru), "it has not risen yet");
            yield return 30;
            t.Check(plexus.CastsFromDouble(shikamaru) && plexus.OriginCell(shikamaru) == spot, "after the rise the origin is the double's cell (" + plexus.OriginCell(shikamaru) + ")");
            t.Check(imitation.CanApplyOn((LocalTargetInfo)enemy), "the pawn is 6 cells from the double, in reach");
            Cast(shikamaru, ShadowPlexusDefOf.AG_ShadowImitation, enemy);
            yield return 2;
            t.Check(plexus.HoldOf(shikamaru, enemy) != null, "held from the double");
            IntVec3 enemyStart = enemy.Position;
            Step(shikamaru, North);
            yield return 3;
            t.Check(plexus.DoubleOf(shikamaru).cell == spot + North, "the double copied the step (" + plexus.DoubleOf(shikamaru).cell + ")");
            t.Check(enemy.Position == enemyStart + North, "and the held pawn was dragged with it (" + enemy.Position + ")");
            TearDown();
        }

        [RimArtTest("Shadow Plexus", "double 2 ends when a pawn crosses the tie line or its cell goes dark")]
        private static IEnumerable<int> DoubleEnds(RimArtTestContext t)
        {
            Pawn shikamaru = Setup(t);
            IntVec3 spot = t.center + East * 8;
            yield return 2;
            Cast(shikamaru, ShadowPlexusDefOf.AG_ShadowDouble, spot);
            yield return 30;
            MapComponent_ShadowPlexus plexus = Plexus(t);
            ShadowDoubleRecord first = plexus.DoubleOf(shikamaru);
            t.Check(first != null && first.Stands(t.Now), "the double stands");
            Pawn walker = t.Colonist(t.center + East * 4);
            yield return 3;
            t.Log("walker " + RimArtTestContext.Describe(walker));
            t.Check(plexus.DoubleOf(shikamaru) == null && first.end == DoubleEnd.FireGoesOut, "a pawn on the tie line ends it (" + first.end + ")");
            walker.Destroy();
            yield return 2;

            Cast(shikamaru, ShadowPlexusDefOf.AG_ShadowDouble, spot);
            yield return 30;
            ShadowDoubleRecord second = plexus.DoubleOf(shikamaru);
            t.Check(second != null && second.Stands(t.Now), "the second double stands");
            ShadowLight.levelForTests = c => c == spot ? 0.1f : 1f;
            yield return 3;
            t.Check(plexus.DoubleOf(shikamaru) == null && second.end == DoubleEnd.FireGoesOut, "its cell going dark ends it (" + second.end + ")");
            ShadowLight.levelForTests = _ => 0.1f;
            Ability dbl = Of(shikamaru, ShadowPlexusDefOf.AG_ShadowDouble);
            dbl.ResetCooldown();
            t.Check(!dbl.GizmoDisabled(out _), "the double's button is not greyed out in the dark");
            t.Check(!dbl.CanApplyOn((LocalTargetInfo)spot), "but a dark cell cannot be chosen");
            TearDown();
        }

        // ------------------------------------------------------------------ Neck bind

        [RimArtTest("Shadow Plexus", "neck bind 1 the channel holds him, the target passes out at 100 % and wakes after the hold", 2400)]
        private static IEnumerable<int> NeckBindKnockout(RimArtTestContext t)
        {
            Pawn shikamaru = Setup(t);
            Pawn enemy = Enemy(t, 5, 0);
            yield return 2;
            Ability bind = Of(shikamaru, ShadowPlexusDefOf.AG_ShadowNeckBind);
            t.Check(bind.GizmoDisabled(out string reason), "greyed out while nobody is held (" + reason + ")");
            Cast(shikamaru, ShadowPlexusDefOf.AG_ShadowImitation, enemy);
            yield return 2;
            MapComponent_ShadowPlexus plexus = Plexus(t);
            t.Check(!bind.GizmoDisabled(out _), "available once a pawn is held");
            foreach (int wait in CastHoldTest.Run(t, shikamaru, ShadowPlexusDefOf.AG_ShadowNeckBind, enemy,
                () => plexus.Fired(shikamaru), CastHoldTest.Asking(() => plexus.Holds(shikamaru)), 700))
                yield return wait;
            Hediff choked = Choked(enemy);
            t.Check(choked != null && choked.Severity >= HediffComp_ShadowChoke.Full, "suffocation is full (" + choked?.Severity + ")");
            t.Check(enemy.Downed && !enemy.Dead, "the target is down and alive: " + RimArtTestContext.Describe(enemy));
            t.Check(plexus.HoldOf(shikamaru, enemy) == null, "the hold let go with the hands");
            float charge = GameComponent_Echoes.Get.charge;
            t.Check(ShadowPlexusDefOf.AG_Echo_Shikamaru.CastCost(ShadowPlexusDefOf.AG_ShadowNeckBind) == 2f && charge <= 95f && charge > 90f,
                "3 + 2 charge paid, less the upkeep over the channel (" + charge + ")");

            HediffComp_ShadowChoke comp = choked.TryGetComp<HediffComp_ShadowChoke>();
            t.Check(comp != null && comp.ChokedOut, "the choked-out hold is on");
            // Not waited out: the hold is moved to end 1 s from now, then the drain is watched.
            comp.holdUntilTick = t.Now + 60;
            yield return 60 + 120;
            t.Check(!enemy.Downed, "after the hold the target is up: " + RimArtTestContext.Describe(enemy));
            float before = Choked(enemy)?.Severity ?? 0f;
            yield return 60;
            float after = Choked(enemy)?.Severity ?? 0f;
            t.Check(before < HediffComp_ShadowChoke.Full && after < before, "the suffocation drains (" + before.ToString("0.00") + " to " + after.ToString("0.00") + ")");
            TearDown();
        }

        [RimArtTest("Shadow Plexus", "neck bind 2 a cut line drops the hands and the meter drains; mechs and unheld pawns are refused; a move order ends it")]
        private static IEnumerable<int> NeckBindCutAndRefusals(RimArtTestContext t)
        {
            Pawn shikamaru = Setup(t);
            Pawn enemy = Enemy(t, 5, 0);
            yield return 2;
            Cast(shikamaru, ShadowPlexusDefOf.AG_ShadowImitation, enemy);
            yield return 2;
            MapComponent_ShadowPlexus plexus = Plexus(t);
            Ability bind = Of(shikamaru, ShadowPlexusDefOf.AG_ShadowNeckBind);
            bind.QueueCastingJob(enemy, LocalTargetInfo.Invalid);
            yield return 5 * 60;
            ShadowNeckBindCast cast = plexus.BindOf(shikamaru);
            t.Check(cast != null, "the bind is choking at 5 s: " + RimArtTestContext.Describe(shikamaru));
            float sev = Choked(enemy)?.Severity ?? -1f;
            t.Check(sev > 0.2f && sev < 0.5f, "about 2.9 s of choke (" + sev.ToString("0.00") + ")");
            Pawn walker = t.Colonist(t.center + East * 2);
            walker.drafter.Drafted = false;
            RimArtTestContext.Hold(walker);
            yield return 4;
            t.Check(plexus.BindOf(shikamaru) == null && cast.cut, "the hands fell off when the line was cut (cut " + cast.cut + ")");
            t.Check(shikamaru.CurJobDef != ShadowPlexusDefOf.AG_CastShadowNeckBind, "the channel job ended: " + RimArtTestContext.Describe(shikamaru));
            float before = Choked(enemy)?.Severity ?? 0f;
            yield return 60;
            float after = Choked(enemy)?.Severity ?? 0f;
            t.Check(!enemy.Downed && after < before, "the target stands and the meter drains (" + before.ToString("0.00") + " to " + after.ToString("0.00") + ")");
            walker.Destroy();
            yield return 2;

            Pawn unheld = Enemy(t, 0, -5);
            yield return 1;
            t.Check(!bind.CanApplyOn((LocalTargetInfo)unheld), "a pawn that is not held is refused");
            PawnKindDef scyther = DefDatabase<PawnKindDef>.GetNamedSilentFail("Mech_Scyther");
            if (scyther != null && Faction.OfMechanoids != null)
            {
                Pawn mech = PawnGenerator.GeneratePawn(new PawnGenerationRequest(scyther, Faction.OfMechanoids));
                GenSpawn.Spawn(mech, t.center + new IntVec3(0, 0, 5), t.map);
                RimArtTestContext.Hold(mech);
                yield return 1;
                Cast(shikamaru, ShadowPlexusDefOf.AG_ShadowImitation, mech);
                yield return 2;
                t.Check(plexus.HoldOf(shikamaru, mech) != null, "a mech can be held");
                t.Check(!bind.CanApplyOn((LocalTargetInfo)mech), "but not choked");
                plexus.ReleaseAll();
                mech.Destroy();
            }
            else t.Log("no Mech_Scyther or mechanoid faction: the mech check was skipped");
            yield return 2;

            Pawn third = Enemy(t, -5, 0);
            yield return 2;
            Cast(shikamaru, ShadowPlexusDefOf.AG_ShadowImitation, third);
            yield return 2;
            bind.ResetCooldown();
            bind.QueueCastingJob(third, LocalTargetInfo.Invalid);
            yield return 60;
            t.Check(plexus.BindOf(shikamaru) != null && shikamaru.CurJobDef == ShadowPlexusDefOf.AG_CastShadowNeckBind, "channelling: " + RimArtTestContext.Describe(shikamaru));
            Job goTo = JobMaker.MakeJob(JobDefOf.Goto, t.center + North * 3);
            shikamaru.jobs.TryTakeOrderedJob(goTo, JobTag.Misc);
            yield return 3;
            t.Check(plexus.BindOf(shikamaru) == null, "a move order ended the channel: " + RimArtTestContext.Describe(shikamaru));
            t.Check(plexus.HoldOf(shikamaru, third) != null, "the hold itself is still on");
            TearDown();
        }

        // ------------------------------------------------------------------ charge

        [RimArtTest("Shadow Plexus", "cast 1 imitation is greyed out when the pool is short and pays 3 when it fires")]
        private static IEnumerable<int> CastCost(RimArtTestContext t)
        {
            Pawn shikamaru = Setup(t);
            Pawn enemy = Enemy(t, 4, 0);
            yield return 2;
            Ability imitation = Of(shikamaru, ShadowPlexusDefOf.AG_ShadowImitation);
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            float cost = ShadowPlexusDefOf.AG_Echo_Shikamaru.CastCost(ShadowPlexusDefOf.AG_ShadowImitation);
            t.Check(cost == 3f, "imitation costs 3 (" + cost + ")");
            echoes.charge = 2f;
            t.Check(imitation.GizmoDisabled(out string reason), "greyed out with 2 charge (" + reason + ")");
            bool result = true;
            t.Check(!EchoCastPayment.Pay(imitation, ref result) && !result, "the cast is refused when the pool is short");
            echoes.charge = 50f;
            t.Check(!imitation.GizmoDisabled(out _), "available with 50");
            Cast(shikamaru, ShadowPlexusDefOf.AG_ShadowImitation, enemy);
            yield return 2;
            t.Check(echoes.charge == 47f, "3 paid at the fire (" + echoes.charge + ")");
            TearDown();
        }

        // ------------------------------------------------------------------ pawn height

        /// <summary>
        /// Close shots for the pawn height fit: the shadow double standing 3 cells east of Shikamaru, next
        /// to a real enemy one cell further on, so its silhouette can be set against two real pawns.
        /// </summary>
        [RimArtTest("Shadow Plexus", "height 1 the double stands the size of a real pawn (screenshots)")]
        private static IEnumerable<int> HeightDouble(RimArtTestContext t)
        {
            Pawn shikamaru = HeightShots.Plain(Setup(t), strip: false);
            IntVec3 spot = t.center + East * 3;
            Pawn enemy = HeightShots.Target(t, t.center + East * 4);
            enemy.stances.stunner.StunFor(600, null, false);
            yield return 2;
            Cast(shikamaru, ShadowPlexusDefOf.AG_ShadowDouble, spot);
            yield return 10;
            yield return HeightShots.Shoot(t, "shadow double rising", t.center + East * 2, shikamaru, enemy);
            yield return 30;
            yield return HeightShots.Shoot(t, "shadow double standing", t.center + East * 2, shikamaru, enemy);
            TearDown();
        }
    }
}
