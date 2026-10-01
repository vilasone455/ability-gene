using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using static RimArt.RimArtTestContext;
using static RimArt.Tests_Ubw;

namespace RimArt
{
    /// <summary>
    /// Game tests for Unlimited Blade Works' commands inside the world (run with -quicktest -rimarttest=ubw): Full Open
    /// released and cancelled, Pin, Draw, Arm and Intercept, with the world's time each one spends. Every pawn stands
    /// within the verse's radius of the caster before the take and lands at the same offset; hostiles are stunned in the
    /// world so their assault lord does not move them. The pawns' state is logged as the swords fly.
    /// </summary>
    public static class Tests_UbwCommands
    {
        private static float Cost => UbwRules.Of.swordCostSeconds;

        /// <summary>Stunned for 20 s, so the assault lord the world gives a hostile does not walk it off its spot.</summary>
        private static void Still(Pawn p) => p.stances.stunner.StunFor(1200, null, false, false);

        /// <summary>The quicktest map is cold: a nude pawn loses Moving to hypothermia in about 20 s, which would down the caster and close the world.</summary>
        private static void Warm(params Pawn[] pawns)
        {
            foreach (Pawn p in pawns)
            {
                Hediff cold = p.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Hypothermia);
                if (cold != null) p.health.RemoveHediff(cold);
            }
        }

        /// <summary>A close-up screenshot of the world at <paramref name="at"/>; the runner shoots the map on screen, so the world is put there first.</summary>
        private static int Shot(RimArtTestContext t, UbwCast cast, string name, IntVec3 at)
        {
            if (Find.CurrentMap != cast.world) Current.Game.CurrentMap = cast.world;
            return t.ShotAs("ubw " + name, at, 5f);
        }

        /// <summary>Waits until the world's opening fire has run out (UbwWorldTiming.Swept after the take), so a screenshot shows the swords, not the white.</summary>
        private static IEnumerable<int> Opened(RimArtTestContext t, UbwCast cast) =>
            WaitFor(() => (t.Now - cast.takenTick) / 60f >= UbwWorldTiming.Swept + 0.1f, 300, 5);

        /// <summary>The next test starts on the map on screen: the home map again.</summary>
        private static void Home(RimArtTestContext t)
        {
            if (Find.CurrentMap != t.map && Find.Maps.Contains(t.map)) Current.Game.CurrentMap = t.map;
        }

        private static string Health(Pawn p) => p.Dead ? "dead" : p.health.summaryHealth.SummaryHealthPercent.ToString("0.##") + (p.Downed ? " DOWNED" : "");

        private static void LogField(RimArtTestContext t, UbwCast cast, string when)
        {
            UbwFieldState field = cast.Inside.Field;
            t.Log(t.Now + " " + when + ": spent " + cast.spent.ToString("0.##") + " s, left " + cast.WorldSecondsLeft(t.Now).ToString("0.##") + " s, holes " + field.TakenCount
                  + ", stuck " + field.LandedCount);
        }

        [RimArtTest("Ubw", "commands 1 Full Open: 8 swords rise, Release fires them at the target, 4 s of the world spent", 3000)]
        private static IEnumerable<int> FullOpen(RimArtTestContext t)
        {
            Setup(t);
            yield return 5;
            Pawn host = Host(t, out EchoRecord record);
            Pawn foe = t.Target(t.center + new IntVec3(3, 0, 1));
            yield return 2;
            var run = new Run();
            foreach (int w in IntoWorld(t, host, run)) yield return w;
            UbwCast cast = run.cast;
            if (cast == null) yield break;
            Still(foe);
            t.Note(foe);
            MapComponent_UnlimitedBladeWorks inside = cast.Inside;
            inside.Rebake();
            LogField(t, cast, "landed");
            t.Log("bake: whole field " + MapComponent_UnlimitedBladeWorks.lastBakeMs.ToString("0.0") + " ms");
            int holes = inside.Field.TakenCount, stuck = inside.Field.LandedCount;
            float spent = cast.spent;
            int began = t.Now;

            cast.fullOpen.Begin(cast, foe, t.Now);
            t.Check(cast.fullOpen.Charging && host.CurJobDef == UbwDefOf.AG_UbwFullOpen, "charging, the caster held (" + Describe(host) + ")");
            foreach (int w in WaitFor(() => cast.fullOpen.hovering.Count >= 8 || !cast.fullOpen.Charging, 300, 1)) yield return w;
            int n = cast.fullOpen.hovering.Count;
            t.Log(t.Now + " " + n + " swords hovering after " + ((t.Now - began) / 60f).ToString("0.00") + " s | " + Describe(host));
            if (!t.Check(n == 8, "8 swords rose, one every 0.1 s (" + n + ")")) yield break;
            cast.fullOpen.Release(cast, t.Now);
            t.Check(!cast.fullOpen.Charging && cast.fullOpen.flying.Count == 8, "Release fired all 8");
            yield return 8;
            yield return Shot(t, cast, "full open volley", foe.PositionHeld);
            for (int i = 0; i < 12 && cast.fullOpen.flying.Count > 0; i++)
            {
                yield return 10;
                t.Log(t.Now + " foe " + Health(foe) + ", flying " + cast.fullOpen.flying.Count + ", struck " + cast.fullOpen.flying.Count(v => v.struck) + " | " + Describe(host));
            }
            foreach (int w in WaitFor(() => cast.fullOpen.flying.Count == 0, 300, 5)) yield return w;
            yield return Shot(t, cast, "full open stuck", foe.PositionHeld);
            LogField(t, cast, "after the volley");
            float rebuild = inside.Rebake();
            t.Log("bake: " + MapComponent_UnlimitedBladeWorks.lastRebuildRows + " rows rebuilt in " + MapComponent_UnlimitedBladeWorks.lastRebuildMs.ToString("0.0") + " ms (this call " + rebuild.ToString("0.0") + " ms)");
            t.Check(t.Hurt(foe), "the target was hit (" + Health(foe) + ")");
            t.Check(Math.Abs(cast.spent - spent - 8 * Cost) < 0.01f, "8 x " + Cost + " s spent (" + (cast.spent - spent).ToString("0.##") + ")");
            float expected = UbwRules.Of.WorldSecondsFor(1) - (t.Now - cast.takenTick) / 60f - cast.spent;
            t.Check(Math.Abs(cast.WorldSecondsLeft(t.Now) - expected) < 0.05f, "the world's time left counts it (" + cast.WorldSecondsLeft(t.Now).ToString("0.00") + " s)");
            t.Check(inside.Field.TakenCount - holes == 8, "8 holes in the field (" + (inside.Field.TakenCount - holes) + ")");
            t.Check(inside.Field.LandedCount - stuck == 8, "8 swords stuck in the ground past the target (" + (inside.Field.LandedCount - stuck) + ")");
            t.Check(host.CurJobDef != UbwDefOf.AG_UbwFullOpen, "the caster is free again");
            cast.closeOrdered = true;
            foreach (int w in WaitFor(() => cast.returned, 1200, 5)) yield return w;
            Home(t);
            EndHost(record);
        }

        [RimArtTest("Ubw", "commands 2 Full Open cancelled by a move order: the swords drop back, the time stays spent", 3000)]
        private static IEnumerable<int> FullOpenCancel(RimArtTestContext t)
        {
            Setup(t);
            yield return 5;
            Pawn host = Host(t, out EchoRecord record);
            Pawn foe = t.Target(t.center + new IntVec3(-3, 0, 2));
            yield return 2;
            var run = new Run();
            foreach (int w in IntoWorld(t, host, run)) yield return w;
            UbwCast cast = run.cast;
            if (cast == null) yield break;
            Still(foe);
            UbwFieldState field = cast.Inside.Field;
            int holes = field.TakenCount;
            float spent = cast.spent;

            cast.fullOpen.Begin(cast, foe, t.Now);
            foreach (int w in WaitFor(() => cast.fullOpen.hovering.Count >= 7 || !cast.fullOpen.Charging, 300, 1)) yield return w;
            t.Log(t.Now + " " + cast.fullOpen.hovering.Count + " hovering | " + Describe(host));
            yield return Shot(t, cast, "full open hover", foe.Position);
            host.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, host.Position + new IntVec3(0, 0, -2)), JobTag.DraftedOrder);
            foreach (int w in WaitFor(() => !cast.fullOpen.Charging, 30, 1)) yield return w;
            t.Log(t.Now + " after the move order: dropping " + cast.fullOpen.dropping.Count + " | " + Describe(host));
            t.Check(!cast.fullOpen.Charging, "the move order ended the charge");
            int dropped = cast.fullOpen.dropping.Count;
            t.Check(dropped >= 4 && cast.fullOpen.hovering.Count == 0, "the hovering swords drop back (" + dropped + ")");
            foreach (int w in WaitFor(() => cast.fullOpen.dropping.Count == 0, 120, 1)) yield return w;
            LogField(t, cast, "dropped back");
            t.Check(field.TakenCount == holes, "every sword stands in its own hole again (" + (field.TakenCount - holes) + " holes left)");
            t.Check(Math.Abs(cast.spent - spent - dropped * Cost) < 0.01f, "the time stays spent: " + dropped + " x " + Cost + " s (" + (cast.spent - spent).ToString("0.##") + ")");
            t.Check(t.Untouched(foe), "the target was not hit (" + Health(foe) + ")");
            cast.closeOrdered = true;
            foreach (int w in WaitFor(() => cast.returned, 1200, 5)) yield return w;
            Home(t);
            EndHost(record);
        }

        [RimArtTest("Ubw", "commands 3 Pin: 4 swords down the target within 1.5 s, 2 Cut each, up again after 12 s", 3600)]
        private static IEnumerable<int> Pin(RimArtTestContext t)
        {
            Setup(t);
            yield return 5;
            Pawn host = Host(t, out EchoRecord record);
            Pawn foe = t.Target(t.center + new IntVec3(0, 0, 3));
            NoWimp(foe);
            yield return 2;
            var run = new Run();
            foreach (int w in IntoWorld(t, host, run)) yield return w;
            UbwCast cast = run.cast;
            if (cast == null) yield break;
            Still(foe);
            t.Note(foe);
            float spent = cast.spent;
            int order = t.Now;

            cast.pins.Begin(cast, foe, t.Now);
            t.Check(cast.pins.pins.Count == 1 && cast.pins.pins[0].swords.Count == 4, "4 swords fly in");
            foreach (int w in WaitFor(() => foe.Downed || foe.Dead, 90, 1)) yield return w;
            float downAt = (t.Now - order) / 60f;
            t.Log(t.Now + " " + Describe(foe) + " " + Health(foe) + " after " + downAt.ToString("0.00") + " s");
            t.Check(foe.Downed && !foe.Dead, "downed within 1.5 s (" + downAt.ToString("0.00") + " s)");
            Hediff pinned = foe.health.hediffSet.GetFirstHediffOfDef(UbwDefOf.AG_UbwPinned);
            t.Check(pinned != null, "pinned (AG_UbwPinned, " + (pinned?.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear ?? 0) + " ticks)");
            foreach (int w in WaitFor(() => cast.pins.pins.Count > 0 && cast.pins.pins[0].swords.All(s => s.struck), 60, 1)) yield return w;
            yield return 20;
            yield return Shot(t, cast, "pin", foe.Position);
            int cuts = foe.health.hediffSet.hediffs.Count(h => h is Hediff_Injury && h.def == HediffDefOf.Cut);
            t.Check(cuts >= 1 && t.Hurt(foe), "the swords cut it (" + cuts + " cuts, " + Health(foe) + ")");
            t.Check(Math.Abs(cast.spent - spent - 4 * Cost) < 0.01f, "4 x " + Cost + " s spent (" + (cast.spent - spent).ToString("0.##") + ")");
            for (int i = 0; i < 6; i++)
            {
                yield return 120;
                Still(foe);
                Warm(host, foe);
                t.Log(t.Now + " " + Describe(foe) + " pinned " + foe.health.hediffSet.HasHediff(UbwDefOf.AG_UbwPinned));
            }
            foreach (int w in WaitFor(() => !foe.health.hediffSet.HasHediff(UbwDefOf.AG_UbwPinned), 120, 5)) yield return w;
            yield return 5;
            float upAt = (t.Now - order) / 60f;
            t.Log(t.Now + " " + Describe(foe) + " after " + upAt.ToString("0.0") + " s");
            t.Check(!foe.Dead && !foe.Downed && upAt >= 12f && upAt < 14f, "up again after 12 s (" + upAt.ToString("0.0") + " s)");
            t.Check(cast.Standing, "the world still stands (" + cast.WorldSecondsLeft(t.Now).ToString("0.0") + " s left)");
            cast.closeOrdered = true;
            foreach (int w in WaitFor(() => cast.returned, 1200, 5)) yield return w;
            Home(t);
            EndHost(record);
        }

        [RimArtTest("Ubw", "commands 4 Draw: the sword cuts the raider on its lane, the caught copy replaces the knife and outlasts the world", 3000)]
        private static IEnumerable<int> Draw(RimArtTestContext t)
        {
            Setup(t);
            yield return 5;
            Pawn host = Host(t, out EchoRecord record);
            ThingDef knifeDef = DefDatabase<ThingDef>.GetNamed("MeleeWeapon_Knife");
            var knife = (ThingWithComps)ThingMaker.MakeThing(knifeDef, knifeDef.MadeFromStuff ? GenStuff.DefaultStuffFor(knifeDef) : null);
            TraceCopies.Give(host, knife);
            Pawn foe = t.Target(t.center + new IntVec3(3, 0, 0));
            yield return 2;
            var run = new Run();
            foreach (int w in IntoWorld(t, host, run)) yield return w;
            UbwCast cast = run.cast;
            if (cast == null) yield break;
            Still(foe);
            t.Note(foe);
            MapComponent_UnlimitedBladeWorks inside = cast.Inside;
            if (!t.Check(host.equipment.Primary == knife, "the caster holds the traced knife in the world")) yield break;

            // A sword about 5 cells east of the caster, and the raider moved within the world onto the cell of its lane
            // that lies closest to the line from the sword to the caster's hand.
            Vector2 me = inside.Local(host.DrawPos), hand = me + new Vector2((float)UbwCommandTiming.Hand.X, (float)UbwCommandTiming.Hand.Z);
            UbwSword pick = inside.Field.Nearest(me + new Vector2(5f, 0f), 2.5f);
            if (!t.Check(pick != null, "a sword stands about 5 cells east of the caster")) yield break;
            var g = new Vector2((float)pick.X, (float)pick.Z);
            float reach = (float)(pick.W.Length * pick.W.Image * pick.Size / 2), best = float.MaxValue;
            IntVec3 lane = IntVec3.Invalid;
            for (float share = 0.25f; share <= 0.751f; share += 0.05f)
            {
                Vector2 q = Vector2.Lerp(g, hand, share) + inside.Origin;
                var cell = new IntVec3(Mathf.FloorToInt(q.x), 0, Mathf.FloorToInt(q.y));
                float off = UbwDraw.DistanceToSegment(inside.Local(cell.ToVector3Shifted()), g, hand);
                if (off < best && cell != host.Position && cell.Standable(cast.world))
                {
                    best = off;
                    lane = cell;
                }
            }
            if (!t.Check(lane.IsValid && best < reach * 0.8f, "a cell on the lane " + best.ToString("0.00") + " cells off the line (reach " + reach.ToString("0.00") + ")")) yield break;
            foe.Position = lane;
            foe.Notify_Teleported();
            t.Log("raider moved to " + lane + "; drawing " + pick.W.Name + " at " + g.ToString("F1") + " cells from the middle corner");
            ThingDef weapon = UbwSwordHit.WeaponOf(pick.W);
            t.Log("drawing " + pick.W.Name + " at " + pick.X.ToString("0.0") + ", " + pick.Z.ToString("0.0") + ": " + UbwSwordHit.Describe(weapon));
            foreach (int w in Opened(t, cast)) yield return w;
            Still(foe);
            float spent = cast.spent;
            cast.draws.Begin(cast, pick, t.Now);
            yield return 16;
            yield return Shot(t, cast, "draw", host.Position + new IntVec3(2, 0, 0));
            for (int i = 0; i < 10 && !cast.draws.flights[0].caught && cast.draws.flights[0].ended == int.MinValue; i++)
            {
                yield return 5;
                t.Log(t.Now + " foe " + Health(foe) + ", cut " + cast.draws.flights[0].cut.Count + " | " + Describe(host));
            }
            foreach (int w in WaitFor(() => cast.draws.flights[0].ended != int.MinValue, 120, 1)) yield return w;
            ThingWithComps held = host.equipment.Primary;
            t.Check(cast.draws.flights[0].cut.Contains(foe) && t.Hurt(foe), "the raider on the lane was cut (" + Health(foe) + ")");
            t.Check(held != null && held.def == weapon && TraceCopies.IsCopy(held), "the caster holds a traced " + weapon.label + " (" + (held?.LabelCap ?? "nothing") + ")");
            t.Check(knife.Destroyed, "the traced knife broke");
            t.Check(Math.Abs(cast.spent - spent - Cost) < 0.01f, Cost + " s spent (" + (cast.spent - spent).ToString("0.##") + ")");
            cast.closeOrdered = true;
            foreach (int w in WaitFor(() => cast.returned, 1200, 5)) yield return w;
            yield return 40;
            LogPawns(t, host);
            t.Check(host.MapHeld == t.map && host.equipment.Primary == held && !held.Destroyed, "home, the caster still holds the copy (" + (host.equipment.Primary?.LabelCap ?? "nothing") + ")");
            Home(t);
            EndHost(record);
        }

        [RimArtTest("Ubw", "commands 5 Arm: a colonist takes a sword as a copy, its knife to the inventory; the copy breaks at the close", 3000)]
        private static IEnumerable<int> Arm(RimArtTestContext t)
        {
            Setup(t);
            yield return 5;
            Pawn host = Host(t, out EchoRecord record);
            Pawn ally = t.Colonist(t.center + new IntVec3(-2, 0, -2));
            t.Equip(ally, DefDatabase<ThingDef>.GetNamed("MeleeWeapon_Knife"));
            ThingWithComps own = ally.equipment.Primary;
            yield return 2;
            var run = new Run();
            foreach (int w in IntoWorld(t, host, run)) yield return w;
            UbwCast cast = run.cast;
            if (cast == null || !t.Check(ally.Map == cast.world, "the ally is in the world")) yield break;
            t.Check(UbwCommands.ArmButton(ally) != null && UbwCommands.ArmButton(host) == null, "the ally has the Arm button, the caster does not");
            foreach (int w in Opened(t, cast)) yield return w;
            float spent = cast.spent;
            cast.arms.Begin(cast, ally, t.Now);
            yield return 26;
            yield return Shot(t, cast, "arm", ally.Position);
            for (int i = 0; i < 6; i++)
            {
                yield return 10;
                t.Log(t.Now + " " + Describe(ally) + " holds " + (ally.equipment.Primary?.LabelCap ?? "nothing"));
            }
            ThingWithComps copy = ally.equipment.Primary;
            TraceCopy record2 = GameComponent_Trace.Instance.CopyOf(copy);
            t.Check(copy != null && copy != own && record2 != null && record2.ubwArm, "the ally holds an Arm copy (" + (copy?.LabelCap ?? "nothing") + ")");
            t.Check(ally.inventory.innerContainer.Contains(own), "its knife went to the inventory");
            t.Check(Math.Abs(cast.spent - spent - Cost) < 0.01f, Cost + " s spent (" + (cast.spent - spent).ToString("0.##") + ")");
            yield return TraceCopies.CheckTicks + 5;
            t.Check(copy != null && !copy.Destroyed, "the copy survives the Trace On check inside the world");
            cast.closeOrdered = true;
            foreach (int w in WaitFor(() => cast.returned, 1200, 5)) yield return w;
            yield return 5;
            LogPawns(t, ally);
            t.Check(copy == null || copy.Destroyed, "the copy broke when the world closed");
            t.Check(ally.MapHeld == t.map && ally.equipment.Primary == null, "the ally is home with empty hands (" + (ally.equipment.Primary?.LabelCap ?? "nothing") + ")");
            Home(t);
            EndHost(record);
        }

        [RimArtTest("Ubw", "commands 6 Intercept: a shot from 9 cells is met and stopped for 0.5 s; one from 2 cells is not and costs nothing", 3600)]
        private static IEnumerable<int> Intercept(RimArtTestContext t)
        {
            Setup(t);
            yield return 5;
            Pawn host = Host(t, out EchoRecord record);
            // One faction for both, or rival raiders fight each other.
            Faction raiders = Find.FactionManager.RandomEnemyFaction(allowNonHumanlike: false);
            Pawn far = t.Target(t.center + new IntVec3(9, 0, 0), bare: false, faction: raiders);
            Pawn near = t.Target(t.center + new IntVec3(-2, 0, 0), bare: false, faction: raiders);
            yield return 2;
            var run = new Run();
            foreach (int w in IntoWorld(t, host, run, verse: 2)) yield return w;
            UbwCast cast = run.cast;
            if (cast == null || !t.Check(far.Map == cast.world && near.Map == cast.world, "both raiders were taken (9 cells: verse 2)")) yield break;
            Still(far);
            Still(near);
            t.Note(host);
            cast.intercept = true;
            foreach (int w in Opened(t, cast)) yield return w;
            LogPawns(t, host, far, near);

            ThingDef gun = DefDatabase<ThingDef>.GetNamed("Gun_Revolver");
            float spent = cast.spent;
            Projectile shot = Fire(far, host, gun);
            int fired = t.Now;
            for (int i = 0; i < 40 && shot.Spawned; i++)
            {
                yield return 1;
                if (i % 3 == 0) t.Log(t.Now + " shot at " + shot.ExactPosition.ToString("F1") + ", meets " + cast.intercepts.meets.Count);
            }
            UbwMeet meet = cast.intercepts.meets.FirstOrDefault(m => m.shot == shot);
            if (meet != null) yield return Shot(t, cast, "intercept", (cast.Inside.Origin + meet.point).ToVector3().ToIntVec3());
            t.Log("stopped " + ((t.Now - fired) / 60f).ToString("0.00") + " s after the shot, " + (meet == null ? "no meeting" : meet.along.ToString("0.0") + " cells out"));
            t.Check(!shot.Spawned && meet != null && meet.done != int.MinValue, "the shot was met and ended");
            t.Check(Math.Abs(cast.spent - spent - Cost) < 0.01f, Cost + " s spent (" + (cast.spent - spent).ToString("0.##") + ")");
            t.Check(t.Untouched(host), "the caster was not hit (" + Health(host) + ")");

            spent = cast.spent;
            Projectile close = Fire(near, host, gun);
            foreach (int w in WaitFor(() => !close.Spawned, 60, 1)) yield return w;
            t.Check(!cast.intercepts.meets.Any(m => m.shot == close), "the shot from 2 cells was not met");
            t.Check(Math.Abs(cast.spent - spent) < 0.001f, "and cost nothing (" + (cast.spent - spent).ToString("0.##") + ")");
            LogPawns(t, host, far, near);
            cast.closeOrdered = true;
            foreach (int w in WaitFor(() => cast.returned, 1200, 5)) yield return w;
            Home(t);
            EndHost(record);
        }

        [RimArtTest("Ubw", "commands 7 the five previews play over the home map without errors (screenshots)", 3000)]
        private static IEnumerable<int> Previews(RimArtTestContext t)
        {
            Setup(t);
            yield return 5;
            var preview = t.map.GetComponent<MapComponent_UbwPreview>();
            foreach (UbwCommandPreview command in Enum.GetValues(typeof(UbwCommandPreview)))
            {
                preview.Play(t.center, UbwPreview.Commands, command);
                t.Check(preview.active, command + " plays");
                yield return command == UbwCommandPreview.FullOpen ? 150 : command == UbwCommandPreview.Pin ? 80 : command == UbwCommandPreview.Intercept ? 50 : 45;
                yield return t.ShotAs("ubw preview " + command, t.center + new IntVec3(0, 0, 2), 5f);
                yield return 30;
                preview.Stop();
            }
        }

        /// <summary>
        /// One round of <paramref name="gun"/>'s projectile from <paramref name="from"/> at <paramref name="at"/>, launched
        /// directly (no aim roll) with the shooter as launcher, as the gun's verb would launch it.
        /// </summary>
        private static Projectile Fire(Pawn from, Pawn at, ThingDef gun)
        {
            ThingDef bullet = gun.Verbs[0].defaultProjectile;
            var shot = (Projectile)GenSpawn.Spawn(bullet, from.Position, from.Map);
            shot.Launch(from, from.DrawPos, at, at, ProjectileHitFlags.IntendedTarget);
            return shot;
        }
    }
}
