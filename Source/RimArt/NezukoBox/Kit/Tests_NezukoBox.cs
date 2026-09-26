using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Game tests for the sleeper box (run with -quicktest -rimarttest=box). The wearer stands at the
    /// arena's centre facing south; pawns are drafted colonists (they stay where they are) or enemies
    /// told to wait. Long rule timers are not waited out: AgeInside moves the entry time back.
    /// </summary>
    public static class Tests_NezukoBox
    {
        private static Pawn Wearer(RimArtTestContext t, out CompNezukoBox box)
        {
            Pawn wearer = t.Colonist(t.center);
            wearer.apparel.Wear((Apparel)ThingMaker.MakeThing(NezukoBoxDefOf.AG_NezukoBox), false);
            wearer.Rotation = Rot4.South;
            box = CompNezukoBox.WornBy(wearer);
            t.Log("wearer " + RimArtTestContext.Describe(wearer) + " abilities "
                + (wearer.abilities?.abilities == null ? "null" : string.Join(",", wearer.abilities.abilities.Select(a => a.def.defName))));
            return wearer;
        }

        /// <summary>Turns the pawn and keeps it turned: undrafted, a wait job facing a cell 3 away in that direction.</summary>
        private static void Face(Pawn pawn, Rot4 rot)
        {
            // Pawn_RotationTracker turns a drafted pawn that stands idle to face south.
            if (pawn.drafter != null) pawn.drafter.Drafted = false;
            Verse.AI.Job wait = JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture, pawn.Position + rot.FacingCell * 3);
            wait.expiryInterval = 600;
            pawn.jobs.StartJob(wait, Verse.AI.JobCondition.InterruptForced);
            pawn.Rotation = rot;
        }

        private static string Where(Pawn pawn) => RimArtTestContext.Describe(pawn);

        private static void Trace(RimArtTestContext t, Pawn wearer, CompNezukoBox box, params Pawn[] others)
        {
            string line = t.Now + " | " + Where(wearer) + " box " + (box == null ? "none" : box.Full ? "holds " + box.Sleeper.LabelShort : "empty")
                + (MapComponent_NezukoBox.Exiting(box) ? " exiting" : "");
            foreach (Pawn o in others) line += " | " + Where(o);
            t.Log(line);
        }

        private static void Strip(Pawn pawn)
        {
            foreach (Apparel a in pawn.apparel?.WornApparel.ToList() ?? new List<Apparel>()) pawn.apparel.Remove(a);
        }

        private static HashSet<Hediff> Injuries(Pawn pawn) =>
            new HashSet<Hediff>(pawn.health.hediffSet.hediffs.Where(h => h is Hediff_Injury || h is Hediff_MissingPart));

        [RimArtTest("Box", "go in 1 the wearer walks to a colonist and it goes in; asleep; the wearer is slowed")]
        private static IEnumerable<int> GoIn(RimArtTestContext t)
        {
            t.Clear();
            Pawn wearer = Wearer(t, out CompNezukoBox box);
            Pawn ally = t.Colonist(t.center + new IntVec3(3, 0, 2));
            yield return 5;
            if (!t.Check(box != null, "the wearer wears the box")) yield break;
            Ability goIn = wearer.abilities.GetAbility(NezukoBoxDefOf.AG_NezukoBoxGoIn);
            if (!t.Check(goIn != null, "wearing it grants Go in")) yield break;
            float speed = wearer.GetStatValue(StatDefOf.MoveSpeed);
            t.Check(goIn.CanCast, "Go in can be cast (" + goIn.CanCast.Reason + ")");
            goIn.QueueCastingJob(new LocalTargetInfo(ally), LocalTargetInfo.Invalid);
            t.Check(wearer.CurJobDef == NezukoBoxDefOf.AG_CastNezukoBox, "the cast job started (" + wearer.CurJobDef?.defName + ")");
            bool shot = false;
            for (int i = 0; i < 100 && !box.Full; i++)
            {
                if (i % 5 == 0) Trace(t, wearer, box, ally);
                if (!shot && wearer.Position.AdjacentTo8WayOrInside(ally.Position) && wearer.CurJobDef == NezukoBoxDefOf.AG_CastNezukoBox && wearer.jobs.curDriver.CurToilIndex >= 2)
                {
                    yield return 8;
                    shot = true;
                    yield return t.ShotAs("go-in-door-open");
                }
                yield return 2;
            }
            Trace(t, wearer, box, ally);
            t.Check(box.Sleeper == ally, "the colonist is in the box");
            t.Check(!ally.Spawned && CompNezukoBox.Holding(ally) == box, "it is off the map, held by the box (" + ally.ParentHolder?.GetType().Name + ")");
            t.Check(ally.health.hediffSet.HasHediff(NezukoBoxDefOf.AG_NezukoBoxSleep), "it is asleep in a box");
            for (int i = 0; i < 30 && wearer.CurJobDef == NezukoBoxDefOf.AG_CastNezukoBox; i++) yield return 2;
            t.Check(wearer.CurJobDef != NezukoBoxDefOf.AG_CastNezukoBox, "the cast job ended after the latch (" + wearer.CurJobDef?.defName + ")");
            float slow = wearer.GetStatValue(StatDefOf.MoveSpeed);
            float expected = 1f - 0.15f * ally.BodySize;
            t.Check(UnityEngine.Mathf.Abs(slow / speed - expected) < 0.02f, "move speed x" + expected.ToString("0.00") + " (" + speed.ToString("0.00") + " -> " + slow.ToString("0.00") + ")");
            t.Check(!goIn.CanCast, "Go in cannot be cast with the box full");

        }

        [RimArtTest("Box", "worn 1 the full box in four facings (screenshots)")]
        private static IEnumerable<int> Facings(RimArtTestContext t)
        {
            t.Clear();
            Pawn wearer = Wearer(t, out CompNezukoBox box);
            Pawn ally = t.Colonist(t.center + IntVec3.East * 2);
            yield return 2;
            t.Check(box.Take(ally), "a colonist goes in");
            foreach (Rot4 rot in new[] { Rot4.South, Rot4.East, Rot4.North, Rot4.West })
            {
                Face(wearer, rot);
                yield return 20;
                t.Check(wearer.Rotation == rot, "the wearer faces " + rot.ToStringHuman() + " (" + wearer.Rotation.ToStringHuman() + ")");
                yield return t.ShotAs("worn-asleep-" + rot.ToStringHuman().ToLowerInvariant());
            }
        }

        [RimArtTest("Box", "go in 2 who may go in")]
        private static IEnumerable<int> Who(RimArtTestContext t)
        {
            t.Clear();
            Pawn wearer = Wearer(t, out CompNezukoBox box);
            Pawn awakeEnemy = t.Enemy(t.center + new IntVec3(2, 0, 0), armed: false);
            Pawn downedEnemy = t.Enemy(t.center + new IntVec3(-2, 0, 0), armed: false);
            downedEnemy.health.AddHediff(HediffDefOf.Anesthetic).Severity = 1f;
            Pawn big = PawnGenerator.GeneratePawn(PawnKindDef.Named("Muffalo"), Faction.OfPlayer);
            GenSpawn.Spawn(big, t.center + new IntVec3(0, 0, 3), t.map);
            Pawn dog = PawnGenerator.GeneratePawn(PawnKindDef.Named("Husky"), Faction.OfPlayer);
            GenSpawn.Spawn(dog, t.center + new IntVec3(0, 0, -3), t.map);
            Pawn ally = t.Colonist(t.center + new IntVec3(3, 0, 3));
            ally.health.AddHediff(NezukoBoxDefOf.AG_NezukoBoxRested);
            Pawn mech = null;
            PawnKindDef mechKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Mech_Scyther");
            if (mechKind != null)
            {
                mech = PawnGenerator.GeneratePawn(mechKind, Faction.OfMechanoids);
                GenSpawn.Spawn(mech, t.center + new IntVec3(-3, 0, 3), t.map);
                RimArtTestContext.Hold(mech);
            }
            yield return 20;
            string Why(Pawn p) => box.CannotTake(p, wearer) ?? "(allowed)";
            t.Log("awake enemy: " + Why(awakeEnemy));
            t.Log("downed enemy (downed " + downedEnemy.Downed + "): " + Why(downedEnemy));
            t.Log("muffalo size " + big.BodySize + ": " + Why(big));
            t.Log("husky size " + dog.BodySize + ": " + Why(dog));
            t.Log("colonist just out of a box: " + Why(ally));
            if (mech != null) t.Log("mechanoid: " + Why(mech));
            t.Check(box.CannotTake(awakeEnemy, wearer) != null, "an awake enemy is refused");
            t.Check(!downedEnemy.Downed || box.CannotTake(downedEnemy, wearer) == null, "a downed enemy is allowed");
            t.Check(box.CannotTake(big, wearer) != null, "a muffalo (body size " + big.BodySize + ") is refused");
            t.Check(box.CannotTake(dog, wearer) == null, "a colony husky (body size " + dog.BodySize + ") is allowed");
            t.Check(box.CannotTake(ally, wearer) != null, "a colonist just out of a box is refused");
            t.Check(box.CannotTake(wearer, wearer) != null, "the wearer itself is refused");
            if (mech != null) t.Check(box.CannotTake(mech, wearer) != null, "a mechanoid is refused");
            t.Check(box.Take(dog), "the husky goes in");
            t.Check(box.CannotTake(downedEnemy, wearer) != null, "the box is full: the downed enemy is refused now");
        }

        [RimArtTest("Box", "inside 1 bleeding and hunger stop, recreation holds, rest fills, cold does not reach")]
        private static IEnumerable<int> Inside(RimArtTestContext t)
        {
            t.Clear();
            Pawn wearer = Wearer(t, out CompNezukoBox box);
            Pawn ally = t.Colonist(t.center + IntVec3.East);
            TraitDef wimpDef = DefDatabase<TraitDef>.GetNamedSilentFail("Wimp");
            Trait wimp = wimpDef == null ? null : ally.story?.traits?.GetTrait(wimpDef);
            if (wimp != null) ally.story.traits.RemoveTrait(wimp);
            var cut = (Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut, ally, ally.RaceProps.body.corePart);
            cut.Severity = 6f;
            ally.health.AddHediff(cut, ally.RaceProps.body.corePart);
            ally.needs.food.CurLevel = 0.5f;
            if (ally.needs.joy != null) ally.needs.joy.CurLevel = 0.5f;
            ally.needs.rest.CurLevel = 0.3f;
            yield return 2;
            float bleedBefore = ally.health.hediffSet.BleedRateTotal;
            t.Check(bleedBefore > 0f, "the colonist bleeds before (" + bleedBefore.ToString("0.00") + "/day)");
            t.Check(box.Take(ally), "it goes in");
            float food = ally.needs.food.CurLevel, joy = ally.needs.joy?.CurLevel ?? -1f, rest = ally.needs.rest.CurLevel, severity = cut.Severity;
            float comfyMin = ally.GetStatValue(StatDefOf.ComfyTemperatureMin);
            yield return 2500;
            Trace(t, wearer, box);
            t.Log("after 1 h: bleed " + ally.health.hediffSet.BleedRateTotal.ToString("0.00") + " food " + food.ToString("0.000") + " -> " + ally.needs.food.CurLevel.ToString("0.000")
                + " joy " + joy.ToString("0.000") + " -> " + (ally.needs.joy?.CurLevel ?? -1f).ToString("0.000") + " rest " + rest.ToString("0.000") + " -> " + ally.needs.rest.CurLevel.ToString("0.000")
                + " cut " + severity.ToString("0.00") + " -> " + cut.Severity.ToString("0.00") + " comfy min " + comfyMin.ToString("0"));
            t.Check(ally.health.hediffSet.BleedRateTotal == 0f, "no bleeding inside");
            t.Check(UnityEngine.Mathf.Abs(ally.needs.food.CurLevel - food) < 0.005f, "food did not fall");
            t.Check(joy < 0f || UnityEngine.Mathf.Abs(ally.needs.joy.CurLevel - joy) < 0.02f, "recreation held");
            t.Check(ally.needs.rest.CurLevel > rest + 0.02f, "rest rose");
            t.Check(comfyMin < -150f, "comfortable down to " + comfyMin.ToString("0") + " C");
            t.Check(ally.GetPosture() != PawnPosture.Standing, "lying down inside (" + ally.GetPosture() + "), for the lying-down healing rate");
            t.Check(!ally.Dead && box.Sleeper == ally, "still inside and alive");
        }

        [RimArtTest("Box", "come out 1 leap next to an enemy: one hit and a stun")]
        private static IEnumerable<int> Strike(RimArtTestContext t)
        {
            t.Clear();
            Pawn wearer = Wearer(t, out CompNezukoBox box);
            Pawn ally = t.Colonist(t.center + IntVec3.West);
            Pawn enemy = t.Enemy(t.center + new IntVec3(4, 0, 0), armed: false);
            Strip(enemy);
            yield return 2;
            t.Check(box.Take(ally), "a colonist goes in");
            HashSet<Hediff> before = Injuries(enemy);
            IntVec3 land = t.center + new IntVec3(3, 0, 0);
            t.Check(box.ValidLanding(wearer, land, out string reason), "the cell next to the enemy is a valid landing (" + reason + ")");
            wearer.Map.GetComponent<MapComponent_NezukoBox>().StartExit(wearer, box, NezukoExit.Strike, land);
            bool shot = false;
            for (int i = 0; i < 50; i++)
            {
                if (i % 3 == 0) Trace(t, wearer, box, ally, enemy);
                if (!shot && ally.ParentHolder is PawnFlyer)
                {
                    shot = true;
                    yield return 10;
                    yield return t.ShotAs("come-out-leap");
                }
                if (ally.Spawned) break;
                yield return 2;
            }
            Trace(t, wearer, box, ally, enemy);
            t.Check(ally.Spawned && ally.Position == land, "the colonist landed on " + land + " (" + Where(ally) + ")");
            t.Check(!box.Full, "the box is empty");
            t.Check(ally.health.hediffSet.HasHediff(NezukoBoxDefOf.AG_NezukoBoxRested) && !ally.health.hediffSet.HasHediff(NezukoBoxDefOf.AG_NezukoBoxSleep), "awake, and just out of a box");
            HashSet<Hediff> after = Injuries(enemy);
            after.ExceptWith(before);
            t.Check(after.Count > 0 || enemy.Dead, "the enemy was hit (" + string.Join(", ", after.Select(h => h.def.defName + " " + h.Severity.ToString("0.0"))) + ")");
            t.Check(enemy.Dead || enemy.stances.stunner.Stunned, "the enemy is stunned (" + (enemy.Dead ? "dead" : enemy.stances.stunner.StunTicksLeft + " ticks") + ")");
            yield return 6;
            yield return t.ShotAs("come-out-strike");
            yield return 100;
            t.Check(enemy.Dead || !enemy.stances.stunner.Stunned, "the stun is over 1.5 s later");
        }

        [RimArtTest("Box", "come out 2 a downed enemy is let out beside the box")]
        private static IEnumerable<int> LetOut(RimArtTestContext t)
        {
            t.Clear();
            Pawn wearer = Wearer(t, out CompNezukoBox box);
            Pawn enemy = t.Enemy(t.center + IntVec3.East, armed: false);
            enemy.health.AddHediff(HediffDefOf.Anesthetic).Severity = 1f;
            yield return 5;
            t.Check(enemy.Downed, "the enemy is downed");
            t.Check(box.Take(enemy), "the downed enemy goes in");
            t.Check(!box.Leaps(enemy), "it does not leap");
            wearer.Map.GetComponent<MapComponent_NezukoBox>().StartExit(wearer, box, NezukoExit.Strike, t.center + new IntVec3(3, 0, 0));
            for (int i = 0; i < 40 && !enemy.Spawned; i++) yield return 2;
            Trace(t, wearer, box, enemy);
            IntVec3 door = t.center + IntVec3.North;
            t.Check(enemy.Spawned && enemy.Position == door, "let out on the cell behind the wearer, " + door + " (" + Where(enemy) + ")");
            t.Check(enemy.Downed, "still downed");
        }

        [RimArtTest("Box", "come out 3 the time limit and full rest let the pawn out")]
        private static IEnumerable<int> Limits(RimArtTestContext t)
        {
            t.Clear();
            Pawn wearer = Wearer(t, out CompNezukoBox box);
            Pawn ally = t.Colonist(t.center + IntVec3.East);
            yield return 2;
            ally.needs.rest.CurLevel = 0.2f;
            t.Check(box.Take(ally), "a tired colonist goes in");
            box.AgeInside(box.Props.MaxTicksInside - 60);
            yield return 30;
            t.Check(box.Full && !MapComponent_NezukoBox.Exiting(box), "0.5 s before 12 h: still inside");
            yield return 40;
            t.Check(MapComponent_NezukoBox.Exiting(box) || ally.Spawned, "at 12 h it is coming out");
            for (int i = 0; i < 40 && !ally.Spawned; i++) yield return 2;
            Trace(t, wearer, box, ally);
            t.Check(ally.Spawned && !box.Full, "out at 12 h (" + Where(ally) + ")");

            // Full rest: the wake time is set within the hour.
            Pawn other = t.Colonist(t.center + IntVec3.West);
            yield return 2;
            t.Check(box.Take(other), "a second colonist goes in");
            other.needs.rest.CurLevel = 1f;
            yield return 2;
            int now = t.Now;
            t.Check(box.WakeTick >= now - 2 && box.WakeTick <= now + box.Props.WakeWithinTicks, "fully rested: it will step out within 1 h (at +" + (box.WakeTick - now) + " ticks)");
        }

        [RimArtTest("Box", "come out 4 the wearer dies or drops the box: the pawn comes out on the spot")]
        private static IEnumerable<int> WearerGone(RimArtTestContext t)
        {
            t.Clear();
            Pawn wearer = Wearer(t, out CompNezukoBox box);
            Pawn ally = t.Colonist(t.center + IntVec3.East);
            yield return 2;
            t.Check(box.Take(ally), "a colonist goes in");
            Apparel apparel = (Apparel)box.parent;
            wearer.apparel.TryDrop(apparel, out Apparel dropped, wearer.Position, false);
            yield return 3;
            Trace(t, wearer, box, ally);
            t.Check(ally.Spawned && ally.Position.InHorDistOf(wearer.Position, 3f), "dropping the box let it out (" + Where(ally) + ")");
            t.Check(!wearer.health.hediffSet.HasHediff(NezukoBoxDefOf.AG_NezukoBoxLoad), "the wearer is not slowed any more");

            // Put it back on, fill it, and kill the wearer.
            dropped = dropped ?? apparel;
            if (dropped.Spawned) dropped.DeSpawn();
            wearer.apparel.Wear(dropped, false);
            Pawn other = t.Colonist(t.center + IntVec3.West);
            yield return 2;
            t.Check(box.Take(other), "another colonist goes in");
            IntVec3 at = wearer.Position;
            wearer.Kill(null);
            yield return 3;
            t.Check(other.Spawned && other.Position.InHorDistOf(at, 3f), "the wearer died: it came out on the spot (" + Where(other) + ")");
        }
    }
}
