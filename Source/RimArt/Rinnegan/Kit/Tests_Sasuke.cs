using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>
    /// Game tests for the Sasuke Echo (run with -quicktest -rimarttest=sasuke): the Echo, Amenoyodomi's hold, let-go and
    /// drops, the conjured kunai, the held Fūma, Amenotejikara, Raikō Kusari and Amaterasu.
    /// </summary>
    public static class Tests_Sasuke
    {
        private static GameComponent_Rinnegan Rinnegan => GameComponent_Rinnegan.Instance;

        private static GameComponent_Echoes Setup(RimArtTestContext t)
        {
            Rinnegan.ResetForTests();
            t.Clear();
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            echoes.ResetForTests();
            EchoDevice.workingForTests = false;
            return echoes;
        }

        /// <summary>A colonist made Sasuke's Host and manifested, with a full pool and a kunai belt, undrafted and held still.</summary>
        private static Pawn Host(RimArtTestContext t, GameComponent_Echoes echoes, IntVec3 at, out EchoRecord record)
        {
            Pawn host = t.Colonist(at);
            record = EchoUtility.ForceHost(SasukeDefOf.AG_Echo_Sasuke, host);
            echoes.charge = 100f;
            EchoUtility.Manifest(record);
            host.drafter.Drafted = false;
            RimArtTestContext.Hold(host);
            Trait wimp = host.story?.traits?.GetTrait(TraitDefOf.Wimp);
            if (wimp != null) host.story.traits.RemoveTrait(wimp);
            host.apparel.Wear((Apparel)ThingMaker.MakeThing(KunaiDefOf.AG_KunaiBelt));
            return host;
        }

        private static void Finish(EchoRecord record)
        {
            if (record != null && record.manifested) EchoUtility.Revert(record, collapse: false);
            EchoDevice.workingForTests = null;
        }

        /// <summary>A hostile that stands still: unarmed, stripped of armour, stunned.</summary>
        private static Pawn Target(RimArtTestContext t, IntVec3 at, int stunTicks = 900)
        {
            Pawn pawn = t.Enemy(at, armed: false);
            pawn.apparel?.DestroyAll();
            pawn.stances.stunner.StunFor(stunTicks, null, false);
            return pawn;
        }

        private static Ability_Amenoyodomi Toggle(Pawn host) => SasukeKit.Amenoyodomi(host);

        private static bool Throw(Pawn host, LocalTargetInfo at)
        {
            Ability ability = host.abilities?.GetAbility(KunaiDefOf.AG_ThrowKunai);
            return ability != null && ability.Activate(at, at);
        }

        private static IEnumerable<int> WaitFor(Func<bool> done, int maxTicks, int step = 1)
        {
            for (int waited = 0; waited < maxTicks && !done(); waited += step) yield return step;
        }

        private static CompApparelReloadable Belt(Pawn host) => KunaiBelt.WornBy(host);

        private static int Injuries(Pawn pawn) => pawn.Dead ? 999 : pawn.health.hediffSet.hediffs.Count(h => h is Hediff_Injury || h is Hediff_MissingPart);

        private static IEnumerable<Thing> KunaiItems(RimArtTestContext t) => t.map.listerThings.ThingsOfDef(KunaiDefOf.AG_Kunai).Where(k => k.Spawned);

        private static string Where(HeldWeapon w) => w == null ? "none" : "(" + w.at.x.ToString("0.00") + ", " + w.at.z.ToString("0.00") + ")";

        // ---- Echo -------------------------------------------------------------------------------------------------

        [RimArtTest("Sasuke", "echo 1 Manifest grants the four abilities with their costs, +0.4 move speed and black hair; revert takes them back")]
        private static IEnumerable<int> Echo(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            EchoDef echo = SasukeDefOf.AG_Echo_Sasuke;
            t.Check(echo.trials.Count == 3 && echo.upkeepPerHour == 12f, "3 trials, upkeep 12 per hour");
            t.Check(echo.CastCost(SasukeDefOf.AG_SasukeAmenoyodomi) == 0f && echo.CastCost(SasukeDefOf.AG_SasukeAmenotejikara) == 2f
                && echo.CastCost(SasukeDefOf.AG_SasukeRaikoKusari) == 8f && echo.CastCost(SasukeDefOf.AG_SasukeAmaterasu) == 5f,
                "cast costs 0 / 2 / 8 / 5");
            Pawn host = t.Colonist(t.center);
            RimArtTestContext.Hold(host);
            float speed = host.GetStatValue(StatDefOf.MoveSpeed);
            EchoRecord record = EchoUtility.ForceHost(echo, host);
            echoes.charge = 100f;
            EchoUtility.Manifest(record);
            yield return 5;
            foreach (AbilityDef def in echo.abilities)
                t.Check(host.abilities.GetAbility(def) != null, "Sasuke has " + def.label);
            t.Check(host.abilities.GetAbility(SasukeDefOf.AG_SasukeAmenoyodomi) is Ability_Amenoyodomi, "Amenoyodomi is the toggle ability");
            t.Check(host.abilities.GetAbility(SasukeDefOf.AG_SasukeAmaterasu) is Ability_Amaterasu, "Amaterasu carries Release");
            float manifested = host.GetStatValue(StatDefOf.MoveSpeed);
            t.Check(Mathf.Abs(manifested - speed - 0.4f) < 0.05f, "move speed " + speed.ToString("0.00") + " -> " + manifested.ToString("0.00"));
            t.Check(host.story.traits.HasTrait(TraitDef.Named("NaturalMood"), -1), "Pessimist");
            Finish(record);
            yield return 5;
            t.Check(echo.abilities.All(def => host.abilities.GetAbility(def) == null), "revert takes the four abilities back");
        }

        // ---- Amenoyodomi ------------------------------------------------------------------------------------------

        [RimArtTest("Sasuke", "hold 1 hang: throws at cells stop in the air and creep; a throw at a pawn is normal; Let go sends each on its line (screenshot)")]
        private static IEnumerable<int> Hold(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            IntVec3 c = t.center;
            Pawn host = Host(t, echoes, c + new IntVec3(-5, 0, 0), out EchoRecord record);
            Ability throwKunai = host.abilities.GetAbility(KunaiDefOf.AG_ThrowKunai);
            t.Check(throwKunai != null && !throwKunai.verb.targetParams.canTargetLocations, "off: throw kunai cannot target a cell");
            Toggle(host).SetMode(HoldMode.Hang);
            t.Check(throwKunai != null && throwKunai.verb.targetParams.canTargetLocations, "hang: throw kunai can target a cell");
            t.Check(throwKunai.verb.targetParams.canTargetPawns, "  and still targets pawns");

            // Three at cells 3 east of the host, fanned.
            IntVec3[] cells = { c + new IntVec3(-2, 0, 2), c + new IntVec3(-2, 0, 0), c + new IntVec3(-2, 0, -2) };
            foreach (IntVec3 cell in cells)
            {
                t.Check(Throw(host, cell), "threw at " + (cell - c));
                yield return 3;
            }
            foreach (int step in WaitFor(() => Rinnegan.CountHeldBy(host) == 3, 120)) yield return step;
            List<HeldWeapon> held = Rinnegan.HeldBy(host);
            t.Check(held.Count == 3, "three kunai held (" + held.Count + ")");
            // Throws 3 ticks apart launch out of order (the clip is busy), so match each to its nearest cell.
            for (int i = 0; i < held.Count; i++)
            {
                HeldWeapon w = held[i];
                float nearest = cells.Min(cell => (w.at - cell.ToVector3Shifted().WithY(0f)).magnitude);
                t.Check(nearest < 0.6f, "kunai " + i + " hangs over one of the cells " + Where(w));
                t.Check(!w.shot.Destroyed && w.shot.Spawned, "  its projectile is still spawned");
                t.Check((w.shot.ExactPosition.WithY(0f) - w.at).magnitude < 0.01f, "  and reported where it hangs");
            }
            Vector3 start = held.Count > 0 ? held[0].at : Vector3.zero;
            yield return 60;
            float crept = held.Count > 0 ? (held[0].at - start).magnitude : 0f;
            t.Check(Mathf.Abs(crept - 0.24f) < 0.03f, "hang creeps 0.24 cells a second (" + crept.ToString("0.000") + ")");
            yield return t.ShotAs("sasuke-hold", c + new IntVec3(-2, 0, 0), 8f);

            // A throw at a pawn is a normal throw.
            Pawn near = Target(t, c + new IntVec3(-3, 0, -5));
            t.Check(Throw(host, near), "threw at a pawn");
            yield return 40;
            t.Check(Rinnegan.CountHeldBy(host) == 3, "a throw at a pawn is not held (" + Rinnegan.CountHeldBy(host) + " held)");

            // Let go: a raider stands 5 cells on along the middle one's line.
            HeldWeapon middle = held.OrderBy(w => Mathf.Abs(w.at.z - c.z - 0.5f)).FirstOrDefault();
            Pawn behind = Target(t, middle != null ? (middle.at + middle.heading * 5f).ToIntVec3() : c + new IntVec3(3, 0, 0));
            int injuriesBefore = Injuries(behind);
            var before = new HashSet<Thing>(KunaiItems(t));
            Rinnegan.LetGo(host);
            t.Check(Rinnegan.CountHeldBy(host) == 0, "let go: nothing held");
            foreach (int step in WaitFor(() => Injuries(behind) > injuriesBefore, 90)) yield return step;
            t.Check(Injuries(behind) > injuriesBefore, "the middle kunai hit the raider on its line");
            yield return 60;
            int landed = KunaiItems(t).Count(k => !before.Contains(k));
            t.Check(landed >= 2, "the other two flew on and came down as kunai (" + landed + " new kunai items)");
            Finish(record);
        }

        [RimArtTest("Sasuke", "hold 2 drift moves 10x faster; toggling off drops them as kunai; a 6th throw lands; a wall and the 60 s limit drop them (screenshot)")]
        private static IEnumerable<int> Drops(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            IntVec3 c = t.center;
            Pawn host = Host(t, echoes, c + new IntVec3(-6, 0, 0), out EchoRecord record);
            var earlier = new HashSet<Thing>(KunaiItems(t));
            Func<int> onTheGround = () => KunaiItems(t).Where(k => !earlier.Contains(k)).Sum(k => k.stackCount);
            Toggle(host).SetMode(HoldMode.Hang);
            // Six throws, cells 3 apart north to south: five are held, the sixth lands.
            for (int i = 0; i < 6; i++)
            {
                Throw(host, c + new IntVec3(-3, 0, 6 - i * 2));
                yield return 3;
            }
            yield return 60;
            t.Check(Rinnegan.CountHeldBy(host) == 5, "five held (" + Rinnegan.CountHeldBy(host) + ")");
            t.Check(onTheGround() == 1, "the sixth landed as a kunai (" + onTheGround() + " on the ground)");

            HeldWeapon first = Rinnegan.HeldBy(host).First();
            Vector3 start = first.at;
            Toggle(host).SetMode(HoldMode.Drift);
            yield return 30;
            float moved = (first.at - start).magnitude;
            t.Check(Mathf.Abs(moved - 1.2f) < 0.1f, "drift: 2.4 cells a second (" + moved.ToString("0.00") + " in 0.5 s)");
            yield return t.ShotAs("sasuke-drift", c + new IntVec3(-1, 0, 1), 9f);

            // A wall two cells ahead of one: it drops at the wall's face.
            HeldWeapon walled = Rinnegan.HeldBy(host)[1];
            IntVec3 wallCell = (walled.at + walled.heading * 1.6f).ToIntVec3();
            GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog), wallCell, t.map);
            foreach (int step in WaitFor(() => !Rinnegan.Held.Contains(walled), 120)) yield return step;
            t.Check(!Rinnegan.Held.Contains(walled), "the drifting kunai dropped at the wall");
            Toggle(host).SetMode(HoldMode.Hang);

            // The 60 s limit: its clock moved back.
            HeldWeapon old = Rinnegan.HeldBy(host)[0];
            old.caughtTick -= SasukeKit.HoldProps.holdTicks;
            yield return 2;
            t.Check(!Rinnegan.Held.Contains(old), "a kunai held 60 s dropped");

            int onGround = onTheGround();
            int stillHeld = Rinnegan.CountHeldBy(host);
            Toggle(host).SetMode(HoldMode.Off);
            yield return 2;
            t.Check(Rinnegan.CountHeldBy(host) == 0, "off: nothing held");
            t.Check(onTheGround() == onGround + stillHeld,
                "the " + stillHeld + " held dropped as kunai (" + onTheGround() + " on the ground)");
            Finish(record);
        }

        [RimArtTest("Sasuke", "hold 3 held kunai drop when Sasuke reverts and when he is downed")]
        private static IEnumerable<int> DropOnDown(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            IntVec3 c = t.center;
            Pawn host = Host(t, echoes, c + new IntVec3(-4, 0, 0), out EchoRecord record);
            Toggle(host).SetMode(HoldMode.Hang);
            Throw(host, c + new IntVec3(-1, 0, 2));
            yield return 60;
            t.Check(Rinnegan.CountHeldBy(host) == 1, "one held");
            EchoUtility.Revert(record, collapse: false);
            yield return 2;
            t.Check(Rinnegan.CountHeldBy(host) == 0, "revert: it dropped");

            echoes.charge = 100f;
            t.Check(EchoUtility.Manifest(record), "manifested again");
            yield return 2;
            t.Check(Toggle(host)?.mode == HoldMode.Off, "Amenoyodomi starts off again");
            Toggle(host).SetMode(HoldMode.Hang);
            Throw(host, c + new IntVec3(-1, 0, 2));
            yield return 3;
            Throw(host, c + new IntVec3(-1, 0, -2));
            yield return 60;
            t.Check(Rinnegan.CountHeldBy(host) == 2, "two held");
            HealthUtility.DamageUntilDowned(host, false);
            yield return 2;
            t.Check(host.Downed, "Sasuke is downed");
            t.Check(Rinnegan.CountHeldBy(host) == 0, "downed: both dropped");
            Finish(record);
        }

        [RimArtTest("Sasuke", "supply 1 the belt regains a conjured kunai every 5 s; a thrown one never becomes an item; a revert takes the rest")]
        private static IEnumerable<int> Supply(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            IntVec3 c = t.center;
            Pawn host = Host(t, echoes, c + new IntVec3(-4, 0, 0), out EchoRecord record);
            CompApparelReloadable belt = Belt(host);
            var earlier = new HashSet<Thing>(KunaiItems(t));
            Func<bool> newKunai = () => KunaiItems(t).Any(k => !earlier.Contains(k));
            KunaiConjure.RemoveCharges(belt, belt.RemainingCharges);
            t.Check(belt.RemainingCharges == 0, "belt emptied");
            yield return 310;
            t.Check(belt.RemainingCharges == 1 && Rinnegan.ConjuredIn(belt.parent) == 1, "one conjured kunai after 5 s (" + belt.LabelRemaining + ")");
            yield return 300;
            t.Check(belt.RemainingCharges == 2, "two after 10 s (" + belt.LabelRemaining + ")");

            // Thrown with Amenoyodomi off at an empty cell next to a raider (a pawn target): it hits or lands, and
            // either way no kunai item appears; one stuck in a pawn is conjured and gives nothing back when pulled.
            Pawn raider = Target(t, c + new IntVec3(1, 0, 0));
            int injuries = Injuries(raider);
            Func<bool> flying = () => t.map.listerThings.ThingsOfDef(SasukeDefOf.AG_KunaiProjectileConjured).Any();
            Throw(host, raider);
            t.Check(Rinnegan.ConjuredIn(belt.parent) == 1, "the throw spent a conjured kunai (" + belt.LabelRemaining + ")");
            foreach (int step in WaitFor(flying, 60)) yield return step;
            bool sawConjured = flying();
            foreach (int step in WaitFor(() => !flying(), 90)) yield return step;
            yield return 30;
            t.Check(sawConjured, "the throw flew as a conjured kunai");
            t.Check(!newKunai(), "no kunai item on the ground");
            Hediff_EmbeddedKunai stuck = raider.health.hediffSet.hediffs.OfType<Hediff_EmbeddedKunai>().FirstOrDefault();
            t.Log(Injuries(raider) > injuries ? "the kunai hit" + (stuck != null ? " and stuck" : "") : "the kunai missed");
            if (stuck != null)
            {
                t.Check(stuck.conjured, "the stuck kunai is conjured");
                Pawn puller = t.Colonist(c + new IntVec3(2, 0, 1));
                RimArtTestContext.Hold(puller);
                raider.stances.stunner.StunFor(300, null, false);
                raider.health.AddHediff(HediffDefOf.Anesthetic);
                KunaiEmbedding.TryPull(puller, raider);
                yield return 2;
                t.Check(!newKunai() && puller.inventory.innerContainer.All(th => th.def != KunaiDefOf.AG_Kunai),
                    "pulled out, it gave nothing back");
            }

            // Revert with conjured kunai on the belt: they go.
            int charges = belt.RemainingCharges, conjured = Rinnegan.ConjuredIn(belt.parent);
            Finish(record);
            yield return 61;
            t.Check(belt.RemainingCharges == charges - conjured, "revert: the belt lost its " + conjured + " conjured kunai (" + belt.LabelRemaining + ")");
        }

        [RimArtTest("Sasuke", "fuma 1 with Amenoyodomi on the Fūma cuts its line and hangs at the end; Let go flies it 12 more, cutting again, and it lands (screenshot)")]
        private static IEnumerable<int> Fuma(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            IntVec3 c = t.center;
            Pawn host = Host(t, echoes, c + new IntVec3(-8, 0, 0), out EchoRecord record);
            var fuma = (ThingWithComps)ThingMaker.MakeThing(FumaDefOf.AG_FumaShuriken, GenStuff.DefaultStuffFor(FumaDefOf.AG_FumaShuriken));
            host.equipment.AddEquipment(fuma);
            Toggle(host).SetMode(HoldMode.Hang);
            Pawn onWay = Target(t, c + new IntVec3(-5, 0, 0));
            Pawn further = Target(t, c + new IntVec3(2, 0, 0));
            int a0 = Injuries(onWay), b0 = Injuries(further);
            t.Check(Projectile_Fuma.Release(host, fuma, c + new IntVec3(-3, 0, 0)), "Fūma thrown 5 cells east");
            foreach (int step in WaitFor(() => Rinnegan.CountHeldBy(host) == 1, 90)) yield return step;
            HeldWeapon held = Rinnegan.HeldBy(host).FirstOrDefault();
            t.Check(held != null && held.IsFuma, "the Fūma hangs at the end of its line " + Where(held));
            t.Check(Injuries(onWay) > a0, "it cut the raider on the way");
            t.Check(Injuries(further) == b0, "the raider further on is untouched");
            yield return 30;
            yield return t.ShotAs("sasuke-fuma", c + new IntVec3(-3, 0, 0), 7f);
            Rinnegan.LetGo(host);
            foreach (int step in WaitFor(() => Injuries(further) > b0, 90)) yield return step;
            t.Check(Injuries(further) > b0, "let go: it cut the raider further on");
            yield return 60;
            Thing landed = t.map.listerThings.ThingsOfDef(FumaDefOf.AG_FumaShuriken).FirstOrDefault(th => th.Spawned);
            t.Check(landed != null, "the Fūma landed (" + landed?.Position + ")");
            Finish(record);
        }

        [RimArtTest("Sasuke", "cast 1 through the game's cast jobs, as a click does: kunai at two cells (held), Raikō Kusari after its warmup, Amaterasu on a held kunai, Amenotejikara Sasuke <-> raider (screenshot)")]
        private static IEnumerable<int> CastJobs(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            IntVec3 c = t.center;
            Pawn host = Host(t, echoes, c + new IntVec3(-5, 0, 0), out EchoRecord record);
            host.drafter.Drafted = true;
            host.drafter.FireAtWill = false;
            Toggle(host).SetMode(HoldMode.Hang);
            Ability throwKunai = host.abilities.GetAbility(KunaiDefOf.AG_ThrowKunai);
            IntVec3 north = c + new IntVec3(-2, 0, 2), south = c + new IntVec3(-2, 0, -2);
            t.Check(throwKunai.verb.ValidateTarget(north, false), "the throw's verb accepts a cell while Amenoyodomi is on");
            throwKunai.QueueCastingJob(new LocalTargetInfo(north), new LocalTargetInfo(north));
            foreach (int step in WaitFor(() => Rinnegan.CountHeldBy(host) == 1, 120)) yield return step;
            t.Check(Rinnegan.CountHeldBy(host) == 1, "the first kunai is held (job " + host.CurJobDef?.defName + ")");
            // The throw's 1.5 s cooldown first, as in play.
            foreach (int step in WaitFor(() => throwKunai.CanCast, 120)) yield return step;
            t.Log("before the second throw: can cast " + throwKunai.CanCast.Accepted + ", cooldown " + throwKunai.CooldownTicksRemaining
                + ", job " + host.CurJobDef?.defName + ", queue " + host.jobs.jobQueue.Count + ", belt " + Belt(host)?.LabelRemaining);
            throwKunai.QueueCastingJob(new LocalTargetInfo(south), new LocalTargetInfo(south));
            for (int i = 0; i < 6 && Rinnegan.CountHeldBy(host) < 2; i++)
            {
                yield return 20;
                t.Log("  +" + (i + 1) * 20 + " ticks: held " + Rinnegan.CountHeldBy(host) + ", job " + host.CurJobDef?.defName
                    + ", queue " + host.jobs.jobQueue.Count);
            }
            t.Check(Rinnegan.CountHeldBy(host) == 2, "the second kunai is held");

            Pawn raider = Target(t, c + new IntVec3(-2, 0, 0), 1200);
            Ability net = host.abilities.GetAbility(SasukeDefOf.AG_SasukeRaikoKusari);
            net.QueueCastingJob(new LocalTargetInfo(host), LocalTargetInfo.Invalid);
            yield return 25;
            yield return t.ShotAs("sasuke-charge", host.Position, 5f);
            foreach (int step in WaitFor(() => Rinnegan.NetOf(host) != null, 90)) yield return step;
            yield return 10;
            t.Check(Rinnegan.NetOf(host) != null, "Raikō Kusari formed after its warmup");
            t.Check(Rinnegan.NetOf(host)?.Caught(raider) == true, "  and caught the raider between the kunai");

            Ability amaterasu = host.abilities.GetAbility(SasukeDefOf.AG_SasukeAmaterasu);
            IntVec3 heldCell = Rinnegan.HeldBy(host)[0].at.ToIntVec3();
            amaterasu.QueueCastingJob(new LocalTargetInfo(heldCell), LocalTargetInfo.Invalid);
            yield return 18;
            yield return t.ShotAs("sasuke-gaze", host.Position, 4f);
            foreach (int step in WaitFor(() => Rinnegan.HeldBy(host).All(w => w.burning), 90)) yield return step;
            yield return 60;
            yield return t.ShotAs("sasuke-blood", host.Position, 4f);
            t.Check(Rinnegan.HeldBy(host).All(w => w.burning), "Amaterasu lit the held kunai");
            List<string> labels = host.GetGizmos().OfType<Command>().Select(g => g.LabelCap.ToString()).ToList();
            t.Check(labels.Contains("Amenoyodomi: hang"), "the toggle button reads \"Amenoyodomi: hang\"");
            t.Check(labels.Any(l => l.StartsWith("Let go (2)")), "a Let go (2) button");
            t.Check(labels.Contains("Release"), "a Release button");

            Pawn other = Target(t, c + new IntVec3(1, 0, -4), 600);
            IntVec3 hostCell = host.Position, otherCell = other.Position;
            Ability swap = host.abilities.GetAbility(SasukeDefOf.AG_SasukeAmenotejikara);
            swap.QueueCastingJob(new LocalTargetInfo(host), new LocalTargetInfo(other));
            foreach (int step in WaitFor(() => host.Position == otherCell, 60)) yield return step;
            t.Check(host.Position == otherCell && other.Position == hostCell, "Amenotejikara swapped Sasuke and the raider");
            Finish(record);
        }

        // ---- Amenotejikara ----------------------------------------------------------------------------------------

        [RimArtTest("Sasuke", "swap 1 Amenotejikara swaps Sasuke with a raider, a raider with a held kunai and an item with Sasuke; two other pawns and a big one are refused (screenshots)")]
        private static IEnumerable<int> Swap(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            IntVec3 c = t.center;
            Pawn host = Host(t, echoes, c + new IntVec3(-4, 0, 0), out EchoRecord record);
            Ability swap = host.abilities.GetAbility(SasukeDefOf.AG_SasukeAmenotejikara);
            var comp = swap.CompOfType<CompAbilityEffect_Amenotejikara>();

            Pawn raider = Target(t, c + new IntVec3(3, 0, 1));
            IntVec3 hostCell = host.Position, raiderCell = raider.Position;
            t.Check(comp.CanApplyOn(host, raider), "Sasuke and a raider can swap");
            swap.Activate(host, raider);
            yield return 2;
            t.Check(host.Position == raiderCell && raider.Position == hostCell, "Sasuke and the raider changed places");
            Shader invert = Shader.Find("Hidden/Internal-Colored");
            t.Log("negative flash shader Hidden/Internal-Colored: " + (invert == null ? "missing" : "found, supported " + invert.isSupported));
            yield return t.ShotAs("sasuke-swap-flash", c + new IntVec3(0, 0, 0), 9f);
            // The flash held for a second by the debug preview, so a screenshot cannot miss it.
            t.map.GetComponent<MapComponent_RinneganPreview>()?.Play(c, false, 1f);
            yield return 20;
            yield return t.ShotAs("sasuke-flash-held", c, 9f);

            yield return 8;
            yield return t.ShotAs("sasuke-swap-pattern", c + new IntVec3(0, 0, 0), 9f);

            Pawn other = Target(t, c + new IntVec3(1, 0, -3));
            t.Check(!comp.CanApplyOn(raider, other), "two other pawns cannot be swapped");

            // A raider and a held kunai.
            Toggle(host).SetMode(HoldMode.Hang);
            Throw(host, c + new IntVec3(0, 0, 4));
            foreach (int step in WaitFor(() => Rinnegan.CountHeldBy(host) == 1, 90)) yield return step;
            HeldWeapon held = Rinnegan.HeldBy(host).FirstOrDefault();
            IntVec3 heldCell = held?.at.ToIntVec3() ?? IntVec3.Invalid;
            IntVec3 otherCell = other.Position;
            t.Check(held != null && comp.CanApplyOn(other, new LocalTargetInfo(heldCell)), "a raider and a held kunai can swap");
            swap.Activate(other, new LocalTargetInfo(heldCell));
            yield return 2;
            t.Check(other.Position == heldCell, "the raider is where the kunai hung (" + other.Position + ")");
            t.Check(held != null && held.at.ToIntVec3() == otherCell && Rinnegan.Held.Contains(held), "the kunai hangs where the raider stood " + Where(held));
            yield return 4;
            yield return t.ShotAs("sasuke-swap-kunai", c + new IntVec3(0, 0, 0), 9f);

            // An item and Sasuke.
            Thing steel = GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Steel), c + new IntVec3(-2, 0, -4), t.map);
            IntVec3 steelCell = steel.Position;
            hostCell = host.Position;
            swap.Activate(steel, host);
            yield return 2;
            t.Check(host.Position == steelCell && steel.Position == hostCell, "Sasuke and the steel changed places");

            // Too big.
            PawnKindDef thrumbo = DefDatabase<PawnKindDef>.GetNamedSilentFail("Thrumbo");
            if (thrumbo != null)
            {
                Pawn big = PawnGenerator.GeneratePawn(thrumbo);
                GenSpawn.Spawn(big, c + new IntVec3(3, 0, -4), t.map);
                t.Check(!comp.CanApplyOn(host, big), "a thrumbo (body size " + big.BodySize + ") is refused");
            }
            Finish(record);
        }

        // ---- Raikō Kusari -----------------------------------------------------------------------------------------

        [RimArtTest("Sasuke", "net 1 Raikō Kusari catches a raider on the line and breaks his shield, burns him, spares Sasuke, and a mech stays stunned after; Let go frees them and the kunai leave charged (screenshot)")]
        private static IEnumerable<int> Net(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            IntVec3 c = t.center;
            Pawn host = Host(t, echoes, c + new IntVec3(-6, 0, 0), out EchoRecord record);
            Toggle(host).SetMode(HoldMode.Hang);
            // Two kunai 4 cells apart north-south through the column x = -1.
            Throw(host, c + new IntVec3(-1, 0, 2));
            yield return 3;
            Throw(host, c + new IntVec3(-1, 0, -2));
            foreach (int step in WaitFor(() => Rinnegan.CountHeldBy(host) == 2, 90)) yield return step;

            Pawn raider = t.Enemy(c + new IntVec3(-1, 0, 0), armed: false);
            raider.apparel?.DestroyAll();
            RimArtTestContext.Hold(raider);
            var shieldBelt = (Apparel)ThingMaker.MakeThing(ThingDef.Named("Apparel_ShieldBelt"));
            raider.apparel.Wear(shieldBelt);
            CompShield shield = shieldBelt.TryGetComp<CompShield>();
            PawnKindDef scyther = DefDatabase<PawnKindDef>.GetNamedSilentFail("Mech_Scyther");
            Pawn mech = null;
            if (scyther != null)
            {
                mech = PawnGenerator.GeneratePawn(scyther, Faction.OfMechanoids);
                GenSpawn.Spawn(mech, c + new IntVec3(-1, 0, 1), t.map);
                RimArtTestContext.Hold(mech);
            }
            yield return 30;
            float shieldBefore = shield?.Energy ?? 0f;
            int injuries = Injuries(raider);

            Ability net = host.abilities.GetAbility(SasukeDefOf.AG_SasukeRaikoKusari);
            t.Check(!net.GizmoDisabled(out string reason), "the net can be cast (" + reason + ")");
            net.Activate(host, host);
            // The run lights a link 0.1 s after the leap; the line catches then.
            yield return 10;
            RaikoNet made = Rinnegan.NetOf(host);
            t.Check(made != null && made.LinkCount == 1, "a net with one link");
            t.Check(made != null && made.Caught(raider) && raider.stances.stunner.Stunned, "the raider on the line is caught");
            t.Check(shield == null || shieldBefore <= 0f || shield.Energy <= 0f, "his shield broke (" + shieldBefore.ToString("0.00") + " -> " + shield?.Energy.ToString("0.00") + ")");
            t.Check(mech == null || made.Caught(mech), "the mech on the line is caught");
            t.Check(made == null || !made.Caught(host), "Sasuke is not caught");
            yield return 65;
            t.Check(Injuries(raider) > injuries, "the caught raider burned");
            yield return t.ShotAs("sasuke-net", c + new IntVec3(-1, 0, 0), 7f);

            Rinnegan.LetGo(host);
            yield return 2;
            yield return t.ShotAs("sasuke-net-letgo", c + new IntVec3(-1, 0, 0), 7f);
            t.Check(Rinnegan.NetOf(host) == null, "let go: the net is gone");
            t.Check(!raider.stances.stunner.Stunned, "the raider is free");
            t.Check(mech == null || mech.stances.stunner.Stunned, "the mech is still stunned (EMP)");
            yield return 30;
            yield return t.ShotAs("sasuke-net-after", c + new IntVec3(-1, 0, 0), 7f);
            Finish(record);
        }

        // ---- Amaterasu --------------------------------------------------------------------------------------------

        [RimArtTest("Sasuke", "amaterasu 1 black flames burn a raider and jump to his neighbour, never to Sasuke; Bleeding eye, then blind; Release puts them out (screenshot)")]
        private static IEnumerable<int> Flames(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            IntVec3 c = t.center;
            Pawn host = Host(t, echoes, c + new IntVec3(-3, 0, 0), out EchoRecord record);
            Pawn raider = Target(t, c + new IntVec3(2, 0, 0));
            Pawn neighbour = Target(t, c + new IntVec3(3, 0, 0));
            Pawn beside = t.Colonist(c + new IntVec3(-2, 0, 0));
            RimArtTestContext.Hold(beside);
            int injuries = Injuries(raider);

            Ability amaterasu = host.abilities.GetAbility(SasukeDefOf.AG_SasukeAmaterasu);
            t.Check(!amaterasu.CompOfType<CompAbilityEffect_Amaterasu>().Valid(new LocalTargetInfo(host)), "Sasuke cannot target himself");
            // Spread made certain for the test.
            var flameProps = (HediffCompProperties_Amaterasu)SasukeDefOf.AG_AmaterasuFlame.comps.First(p => p is HediffCompProperties_Amaterasu);
            float chance = flameProps.spreadChancePerSecond;
            flameProps.spreadChancePerSecond = 1f;
            try
            {
                amaterasu.Activate(raider, raider);
                yield return 2;
                t.Check(Amaterasu.Burning(raider), "the raider burns");
                yield return 16;
                yield return t.ShotAs("sasuke-amaterasu-ignite", c + new IntVec3(2, 0, 0), 5f);
                t.Check(host.health.hediffSet.HasHediff(SasukeDefOf.AG_BleedingEye), "Sasuke's eye bleeds");
                yield return 125;
                t.Check(Injuries(raider) > injuries, "the flames hurt him");
                t.Check(Amaterasu.Burning(neighbour), "his neighbour caught");
                yield return t.ShotAs("sasuke-amaterasu", c + new IntVec3(2, 0, 0), 6f);

                // Put the host beside a burning pawn: he never catches.
                Amaterasu.Ignite(beside, host, 600);
                yield return 125;
                t.Check(!Amaterasu.Burning(host), "Sasuke never catches");
            }
            finally
            {
                flameProps.spreadChancePerSecond = chance;
            }

            amaterasu.Activate(neighbour, neighbour);
            yield return 2;
            t.Check(host.health.hediffSet.HasHediff(SasukeDefOf.AG_AmaterasuBlind), "a second cast while the eye bleeds blinds him");

            t.Check(Rinnegan.AnyBurningBy(host), "Release is offered");
            Rinnegan.Release(host);
            yield return 2;
            t.Check(!Amaterasu.Burning(raider) && !Amaterasu.Burning(neighbour) && !Amaterasu.Burning(beside), "Release put every flame out");
            yield return 18;
            yield return t.ShotAs("sasuke-amaterasu-release", c + new IntVec3(1, 0, 0), 6f);
            Finish(record);
        }

        [RimArtTest("Sasuke", "amaterasu 2 lit held kunai: one lets go into a raider and lights him, one burns on the ground and lights a raider standing there; a lit Fūma lands burning and cannot be picked up (screenshots)")]
        private static IEnumerable<int> LitWeapons(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            IntVec3 c = t.center;
            Pawn host = Host(t, echoes, c + new IntVec3(-6, 0, 0), out EchoRecord record);
            Toggle(host).SetMode(HoldMode.Hang);
            Throw(host, c + new IntVec3(-3, 0, 0));
            yield return 3;
            Throw(host, c + new IntVec3(-3, 0, 4));
            foreach (int step in WaitFor(() => Rinnegan.CountHeldBy(host) == 2, 90)) yield return step;
            // The raider stands 4 cells on along the east kunai's real line (it leaves from the hand, a few degrees off).
            HeldWeapon east = Rinnegan.HeldBy(host).OrderBy(w => Mathf.Abs(w.at.z - c.z - 0.5f)).FirstOrDefault();
            Pawn raider = Target(t, east != null ? (east.at + east.heading * 4f).ToIntVec3() : c + new IntVec3(1, 0, 0));
            HeldWeapon first = Rinnegan.HeldBy(host).FirstOrDefault();
            Ability amaterasu = host.abilities.GetAbility(SasukeDefOf.AG_SasukeAmaterasu);
            IntVec3 heldCell = first?.at.ToIntVec3() ?? IntVec3.Invalid;
            t.Check(amaterasu.CompOfType<CompAbilityEffect_Amaterasu>().Valid(new LocalTargetInfo(heldCell)), "a held kunai's cell is a target");
            amaterasu.Activate(new LocalTargetInfo(heldCell), new LocalTargetInfo(heldCell));
            yield return 2;
            t.Check(Rinnegan.HeldBy(host).All(w => w.burning), "both held kunai are lit");
            yield return 40;
            yield return t.ShotAs("sasuke-amaterasu-held", c + new IntVec3(-2, 0, 2), 6f);

            Rinnegan.LetGo(host);
            yield return 4;
            yield return t.ShotAs("sasuke-amaterasu-flying", c + new IntVec3(0, 0, 2), 8f);
            foreach (int step in WaitFor(() => Amaterasu.Burning(raider), 90)) yield return step;
            t.Check(Amaterasu.Burning(raider), "the kunai on his line lit the raider");
            yield return 60;
            BlackFire fire = Rinnegan.Fires.FirstOrDefault();
            t.Check(fire != null, "the other burns on the ground" + (fire != null ? " at " + fire.cell : ""));
            if (fire != null)
            {
                yield return t.ShotAs("sasuke-amaterasu-floor", fire.cell, 5f);
                Pawn walker = Target(t, fire.cell);
                yield return 20;
                t.Check(Amaterasu.Burning(walker), "a raider standing in it caught");
            }

            // A lit Fūma.
            var fuma = (ThingWithComps)ThingMaker.MakeThing(FumaDefOf.AG_FumaShuriken, GenStuff.DefaultStuffFor(FumaDefOf.AG_FumaShuriken));
            host.equipment.AddEquipment(fuma);
            Projectile_Fuma.Release(host, fuma, c + new IntVec3(-3, 0, -4));
            foreach (int step in WaitFor(() => Rinnegan.HeldBy(host).Any(w => w.IsFuma), 90)) yield return step;
            HeldWeapon heldFuma = Rinnegan.HeldBy(host).FirstOrDefault(w => w.IsFuma);
            if (heldFuma != null) heldFuma.burning = true;
            Toggle(host).SetMode(HoldMode.Off);
            yield return 5;
            Thing lying = t.map.listerThings.ThingsOfDef(FumaDefOf.AG_FumaShuriken).FirstOrDefault(th => th.Spawned);
            t.Check(lying != null && Rinnegan.Burning(lying), "the dropped Fūma lies burning");
            yield return 30;
            if (lying != null) yield return t.ShotAs("sasuke-amaterasu-fuma", lying.Position, 5f);
            t.Check(lying != null && !EquipmentUtility.CanEquip(lying, host, out string why) && why != null, "and cannot be picked up");
            Rinnegan.Release(host);
            t.Check(lying != null && EquipmentUtility.CanEquip(lying, host), "after Release it can");
            Finish(record);
        }
    }
}
