using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>Game tests for the Coil Gun kit (run with -quicktest -rimarttest=coil).</summary>
    public static class Tests_CoilGun
    {
        private static MapComponent_CoilGun Guns(RimArtTestContext t) => t.map.GetComponent<MapComponent_CoilGun>();

        private static Pawn Holder(RimArtTestContext t, IntVec3 at, out CompCoilGun gun)
        {
            Pawn holder = t.Colonist(at);
            t.Equip(holder, CoilGunDefOf.AG_CoilGun);
            // Drafted and standing, but not shooting on its own: only what the test orders.
            holder.drafter.FireAtWill = false;
            // Normal quality, so a round's damage is the def's 9 (quality scales ranged damage).
            holder.equipment.Primary.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal, ArtGenerationContext.Colony);
            gun = CompCoilGun.HeldBy(holder);
            return holder;
        }

        /// <summary>A hostile humanlike with no apparel (no armour, no shield belt), so the damage asked for is what lands.</summary>
        private static Pawn Bare(RimArtTestContext t, IntVec3 at)
        {
            Pawn pawn = t.Enemy(at, armed: false);
            pawn.apparel?.DestroyAll();
            return pawn;
        }

        private static Pawn Mech(RimArtTestContext t, IntVec3 at)
        {
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamed("Mech_Scyther");
            Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, Faction.OfMechanoids));
            GenSpawn.Spawn(pawn, at, t.map);
            RimArtTestContext.Hold(pawn);
            return pawn;
        }

        private static float Injuries(Pawn pawn) =>
            pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h => h.Severity);

        private static string Name(Thing thing) => thing == null ? "null" : thing.LabelShort + "#" + thing.thingIDNumber;

        /// <summary>Casts Chain Arc and traces until the cast job ends. Takes <paramref name="shot"/> <paramref name="shotAfterFire"/> ticks after the gun fires.</summary>
        private static IEnumerable<int> CastArc(RimArtTestContext t, Pawn holder, Pawn target, string shot = null, int shotAfterFire = 6)
        {
            Ability ability = holder.abilities.GetAbility(CoilGunDefOf.AG_CoilGun_ChainArc);
            if (!t.Check(ability != null, "the holder has Chain Arc")) yield break;
            if (!t.Check(ability.CanCast, "Chain Arc can be cast (" + ability.CanCast.Reason + ")")) yield break;
            if (!t.Check(ability.CanApplyOn((LocalTargetInfo)target), "Chain Arc can be applied on " + Name(target))) yield break;
            ability.QueueCastingJob(target, LocalTargetInfo.Invalid);
            int cast = t.Now, fired = -1;
            t.Check(holder.CurJobDef == CoilGunDefOf.AG_CastCoilGun, "the cast job started (job " + holder.CurJobDef?.defName + ")");
            bool shotTaken = shot == null;
            for (int i = 0; i < 200; i++)
            {
                if (fired < 0 && ability.lastCastTick >= cast) fired = t.Now;
                if (i % 5 == 0) t.Log((t.Now - cast) + " | " + RimArtTestContext.Describe(holder) + " | fired " + (fired >= 0 ? "+" + (t.Now - fired) : "no"));
                if (!shotTaken && fired >= 0 && t.Now - fired >= shotAfterFire)
                {
                    shotTaken = true;
                    yield return t.ShotAs(shot);
                }
                if (holder.CurJobDef != CoilGunDefOf.AG_CastCoilGun && fired >= 0 && shotTaken) break;
                yield return 1;
            }
            t.Check(fired >= 0, "the gun fired (after " + (fired - cast) + " ticks)");
            // Every hit has landed by now (0.27 s after the fire for four pawns); give it a few more ticks.
            yield return 10;
        }

        private static void LogChain(RimArtTestContext t)
        {
            List<CoilArcLink> chain = t.map.GetComponent<MapComponent_CoilGun>().lastChainForTests;
            t.Log("chain: " + (chain == null ? "none" : string.Join(" > ", chain.Select(l => Name(l.pawn) + (l.soaked ? " (soaked)" : "") + (l.mech ? " (mech)" : "") + " reach " + l.reach.ToString("0.#")))));
            foreach (MapComponent_CoilGun.HitRecord hit in t.map.GetComponent<MapComponent_CoilGun>().hitsForTests)
                t.Log("hit " + Name(hit.victim) + (hit.arc ? " arc" : " round") + ": asked " + hit.amount.ToString("0.##") + ", dealt " + hit.dealt.ToString("0.##") + " at " + hit.tick
                    + (hit.victim == null ? ", landed at " + hit.at.ToString("F1") : ""));
        }

        private static float Asked(RimArtTestContext t, Thing victim, bool arc)
        {
            float sum = 0f;
            foreach (MapComponent_CoilGun.HitRecord hit in Guns(t).hitsForTests)
                if (hit.victim == victim && hit.arc == arc) sum += hit.amount;
            return sum;
        }

        private static int Hits(RimArtTestContext t, Thing victim, bool arc) => Guns(t).hitsForTests.Count(h => h.victim == victim && h.arc == arc);

        // ------------------------------------------------------------------ the normal shot

        [RimArtTest("Coil Gun", "shot 1 the gun fires a 2-round burst and its bolts do damage")]
        private static IEnumerable<int> Burst(RimArtTestContext t)
        {
            t.Clear();
            Guns(t).hitsForTests.Clear();
            Pawn holder = Holder(t, t.center + new IntVec3(-4, 0, 0), out CompCoilGun gun);
            Pawn enemy = Bare(t, t.center + new IntVec3(2, 0, 0));
            // A good shot, so the hit roll usually lands; the damage check is skipped when both miss (shot 2 covers damage).
            holder.skills.GetSkill(SkillDefOf.Shooting).Level = 20;
            yield return 2;
            int before = Guns(t).roundsLaunched;
            float hurtBefore = Injuries(enemy);
            Job job = JobMaker.MakeJob(JobDefOf.AttackStatic, enemy);
            job.maxNumStaticAttacks = 1;
            holder.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            t.Check(holder.CurJobDef == JobDefOf.AttackStatic, "the holder is attacking (job " + holder.CurJobDef?.defName + ")");
            for (int i = 0; i < 40; i++)
            {
                if (i % 5 == 0) t.Log(RimArtTestContext.Describe(holder) + " | rounds " + (Guns(t).roundsLaunched - before) + " | enemy injuries " + Injuries(enemy).ToString("0.#"));
                if (Guns(t).roundsLaunched - before >= 2) break;
                yield return 5;
            }
            yield return 30;
            LogChain(t);
            t.Log("enemy at " + enemy.Position + " (" + RimArtTestContext.Describe(enemy) + "), holder at " + holder.Position + ", shooting " + holder.skills.GetSkill(SkillDefOf.Shooting).Level);
            int rounds = Guns(t).roundsLaunched - before;
            t.Check(rounds == 2, "one attack fired 2 rounds (" + rounds + ")");
            t.Check(gun.Units == gun.Props.capacity, "the burst used no battery (" + gun.LabelRemaining + ")");
            int hits = Hits(t, enemy, false);
            t.Log("rounds on the enemy: " + hits + " of 2 (the hit roll is Core's); injuries " + hurtBefore.ToString("0.#") + " -> " + Injuries(enemy).ToString("0.#"));
            if (hits > 0)
            {
                t.Check(Mathf.Abs(Asked(t, enemy, false) - 9f * hits) < 0.01f, "each round on a human asked 9 damage (" + Asked(t, enemy, false) + " for " + hits + ")");
                t.Check(Injuries(enemy) > hurtBefore || enemy.Dead, "the enemy was hurt");
            }
        }

        [RimArtTest("Coil Gun", "shot 2 a bolt deals 9 to a human and 14 (9 x 1.5) to a mechanoid")]
        private static IEnumerable<int> BoltDamage(RimArtTestContext t)
        {
            t.Clear();
            Guns(t).hitsForTests.Clear();
            Pawn holder = Holder(t, t.center + new IntVec3(-6, 0, 0), out _);
            Pawn human = Bare(t, t.center + new IntVec3(4, 0, 3));
            Pawn mech = Mech(t, t.center + new IntVec3(4, 0, -3));
            yield return 2;
            float humanBefore = Injuries(human), mechBefore = Injuries(mech);
            Thing gunThing = holder.equipment.Primary;
            foreach (Pawn target in new[] { human, mech })
            {
                var bolt = (Projectile)GenSpawn.Spawn(CoilGunDefOf.AG_CoilGun_Bolt, holder.Position, t.map);
                bolt.Launch(holder, holder.DrawPos, target, target, ProjectileHitFlags.IntendedTarget, false, gunThing);
            }
            yield return 7;
            yield return t.ShotAs("coil-shot-in-flight");
            yield return 30;
            // A scyther's Sharp armour (0.72 against 0.35 penetration) deflects about 1 bolt in 5; up to 3 more until one gets through.
            for (int i = 0; i < 3 && Injuries(mech) <= mechBefore && !mech.Dead; i++)
            {
                t.Log("the mechanoid is not hurt yet (deflected); bolt " + (i + 2));
                var bolt = (Projectile)GenSpawn.Spawn(CoilGunDefOf.AG_CoilGun_Bolt, holder.Position, t.map);
                bolt.Launch(holder, holder.DrawPos, mech, mech, ProjectileHitFlags.IntendedTarget, false, gunThing);
                yield return 30;
            }
            LogChain(t);
            int mechHits = Hits(t, mech, false);
            t.Check(Hits(t, human, false) == 1 && Mathf.Abs(Asked(t, human, false) - 9f) < 0.01f, "the human was asked 9 (" + Asked(t, human, false) + ")");
            t.Check(mechHits >= 1 && Mathf.Abs(Asked(t, mech, false) - 14f * mechHits) < 0.01f, "each bolt on the mechanoid asked 14 (" + Asked(t, mech, false) + " for " + mechHits + ")");
            t.Check(Injuries(human) > humanBefore || human.Dead, "the human was hurt (" + humanBefore.ToString("0.#") + " -> " + Injuries(human).ToString("0.#") + ")");
            t.Check(Injuries(mech) > mechBefore || mech.Dead, "the mechanoid was hurt (" + mechBefore.ToString("0.#") + " -> " + Injuries(mech).ToString("0.#") + ")");
            yield return t.ShotAs("coil-shot-impacts");
        }

        // ------------------------------------------------------------------ Chain Arc

        [RimArtTest("Coil Gun", "arc 1 hits the target and 3 more hostiles each within 3 cells, skipping an ally")]
        private static IEnumerable<int> ArcChain(RimArtTestContext t)
        {
            t.Clear();
            Guns(t).hitsForTests.Clear();
            IntVec3 c = t.center;
            Pawn holder = Holder(t, c + new IntVec3(-6, 0, 0), out CompCoilGun gun);
            Pawn e1 = Bare(t, c);
            Pawn ally = t.Colonist(c + new IntVec3(1, 0, -1));
            ally.drafter.Drafted = false;
            RimArtTestContext.Hold(ally);
            Pawn e2 = Bare(t, c + new IntVec3(2, 0, 1));
            Pawn e3 = Bare(t, c + new IntVec3(4, 0, 2));
            Pawn e4 = Bare(t, c + new IntVec3(6, 0, 3));
            Pawn e5 = Bare(t, c + new IntVec3(8, 0, 4));
            yield return 2;
            t.Log("ally " + ally.Position.DistanceTo(e1.Position).ToString("0.##") + " from e1, e2 " + e2.Position.DistanceTo(e1.Position).ToString("0.##")
                + ", e3 " + e3.Position.DistanceTo(e2.Position).ToString("0.##") + " from e2, e4 " + e4.Position.DistanceTo(e3.Position).ToString("0.##")
                + " from e3, e5 " + e5.Position.DistanceTo(e4.Position).ToString("0.##") + " from e4");
            foreach (int step in CastArc(t, holder, e1)) yield return step;
            LogChain(t);
            List<CoilArcLink> chain = Guns(t).lastChainForTests ?? new List<CoilArcLink>();
            t.Check(chain.Select(l => l.pawn).SequenceEqual(new[] { e1, e2, e3, e4 }), "the chain is e1 > e2 > e3 > e4 (" + chain.Count + " links)");
            foreach (Pawn p in new[] { e1, e2, e3, e4 })
                t.Check(Hits(t, p, true) == 1 && Mathf.Abs(Asked(t, p, true) - 12f) < 0.01f, Name(p) + " was hit once for 12 (" + Asked(t, p, true) + ")");
            t.Check(Hits(t, e5, true) == 0, "e5, a fifth pawn, was not hit");
            t.Check(Hits(t, ally, true) == 0, "the ally 1.4 cells from e1 was not hit");
            t.Check(gun.Units == 15, "the battery went 20 -> 15 (" + gun.LabelRemaining + ")");
            Ability ability = holder.abilities.GetAbility(CoilGunDefOf.AG_CoilGun_ChainArc);
            t.Check(ability.CooldownTicksRemaining > 1000, "Chain Arc is on its 20 s cooldown (" + ability.CooldownTicksRemaining + " ticks)");
        }

        [RimArtTest("Coil Gun", "arc 2 does not jump further than 3 cells")]
        private static IEnumerable<int> ArcReach(RimArtTestContext t)
        {
            t.Clear();
            Guns(t).hitsForTests.Clear();
            IntVec3 c = t.center;
            Pawn holder = Holder(t, c + new IntVec3(-6, 0, 0), out _);
            Pawn e1 = Bare(t, c);
            Pawn e2 = Bare(t, c + new IntVec3(3, 0, 0));
            Pawn e3 = Bare(t, c + new IntVec3(3, 0, 4));
            Pawn e4 = Bare(t, c + new IntVec3(-2, 0, 3));
            yield return 2;
            t.Log("e2 " + e2.Position.DistanceTo(e1.Position).ToString("0.##") + " from e1; e3 " + e3.Position.DistanceTo(e2.Position).ToString("0.##") + " from e2; e4 "
                + e4.Position.DistanceTo(e1.Position).ToString("0.##") + " from e1, " + e4.Position.DistanceTo(e2.Position).ToString("0.##") + " from e2");
            foreach (int step in CastArc(t, holder, e1)) yield return step;
            LogChain(t);
            List<CoilArcLink> chain = Guns(t).lastChainForTests ?? new List<CoilArcLink>();
            t.Check(chain.Select(l => l.pawn).SequenceEqual(new[] { e1, e2 }), "the chain is e1 > e2 and stops (" + chain.Count + " links)");
            t.Check(Hits(t, e2, true) == 1, "e2, exactly 3 cells from e1, was hit");
            t.Check(Hits(t, e3, true) == 0, "e3, 4 cells from e2, was not hit");
            t.Check(Hits(t, e4, true) == 0, "e4, 3.6 cells from e1, was not hit");
        }

        [RimArtTest("Coil Gun", "arc 3 jumps 6 cells from a Soaked pawn, which takes 18 (12 x 1.5)")]
        private static IEnumerable<int> ArcSoaked(RimArtTestContext t)
        {
            t.Clear();
            Guns(t).hitsForTests.Clear();
            IntVec3 c = t.center;
            Pawn holder = Holder(t, c + new IntVec3(-7, 0, 0), out _);
            Pawn e1 = Bare(t, c + new IntVec3(-2, 0, 0));
            Pawn e2 = Bare(t, c + new IntVec3(4, 0, 0));
            Pawn e3 = Bare(t, c + new IntVec3(4, 0, 4));
            WaterGunSoak.Apply(e1, 60f);
            yield return 2;
            t.Check(WaterGunSoak.IsSoaked(e1), "e1 is Soaked");
            t.Log("e2 " + e2.Position.DistanceTo(e1.Position).ToString("0.##") + " from e1; e3 " + e3.Position.DistanceTo(e2.Position).ToString("0.##") + " from e2");
            foreach (int step in CastArc(t, holder, e1, "coil-chain-arc-soaked", 7)) yield return step;
            LogChain(t);
            List<CoilArcLink> chain = Guns(t).lastChainForTests ?? new List<CoilArcLink>();
            t.Check(chain.Select(l => l.pawn).SequenceEqual(new[] { e1, e2 }), "the chain is e1 > e2 (6 cells, through the Soaked e1) and stops (" + chain.Count + " links)");
            t.Check(Mathf.Abs(Asked(t, e1, true) - 18f) < 0.01f, "Soaked e1 was asked 18 (" + Asked(t, e1, true) + ")");
            t.Check(Mathf.Abs(Asked(t, e2, true) - 12f) < 0.01f, "dry e2 was asked 12 (" + Asked(t, e2, true) + ")");
            t.Check(Hits(t, e3, true) == 0, "e3, 4 cells from the dry e2, was not hit");
        }

        [RimArtTest("Coil Gun", "arc 4 a mechanoid in the chain takes 18 (12 x 1.5)")]
        private static IEnumerable<int> ArcMech(RimArtTestContext t)
        {
            t.Clear();
            Guns(t).hitsForTests.Clear();
            IntVec3 c = t.center;
            Pawn holder = Holder(t, c + new IntVec3(-6, 0, 0), out _);
            Pawn e1 = Bare(t, c);
            Pawn mech = Mech(t, c + new IntVec3(2, 0, 1));
            yield return 2;
            foreach (int step in CastArc(t, holder, e1)) yield return step;
            LogChain(t);
            t.Check(Hits(t, mech, true) == 1 && Mathf.Abs(Asked(t, mech, true) - 18f) < 0.01f, "the mechanoid was asked 18 (" + Asked(t, mech, true) + ")");
            t.Check(Mathf.Abs(Asked(t, e1, true) - 12f) < 0.01f, "the human was asked 12 (" + Asked(t, e1, true) + ")");
        }

        [RimArtTest("Coil Gun", "arc 5 is refused below 5 charge")]
        private static IEnumerable<int> ArcRefused(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, t.center + new IntVec3(-6, 0, 0), out CompCoilGun gun);
            Pawn e1 = Bare(t, t.center);
            yield return 2;
            gun.SetCharge(4.9f);
            Ability ability = holder.abilities.GetAbility(CoilGunDefOf.AG_CoilGun_ChainArc);
            t.Log("charge " + gun.Charge.ToString("0.##") + ", CanCast " + (bool)ability.CanCast + " (" + ability.CanCast.Reason + "), gizmo disabled " + ability.GizmoDisabled(out string reason) + ": " + reason);
            t.Check(!ability.CanCast, "Chain Arc cannot be cast with 4 charge");
            t.Check(ability.GizmoDisabled(out reason) && reason != null && reason.Contains("Not enough charge"), "the gizmo is greyed out with the reason (" + reason + ")");
            gun.SetCharge(5f);
            t.Check(ability.CanCast, "with 5 charge it can be cast");
            foreach (int step in CastArc(t, holder, e1)) yield return step;
            t.Check(gun.Units == 0, "the battery went 5 -> 0 (" + gun.LabelRemaining + ")");
        }

        [RimArtTest("Coil Gun", "hold 1 chain arc holds an undrafted caster until the chain has run")]
        private static IEnumerable<int> Hold(RimArtTestContext t)
        {
            t.Clear();
            Pawn caster = CastHoldTest.Caster(t, CoilGunDefOf.AG_CoilGun);
            Pawn e1 = Bare(t, t.center + new IntVec3(6, 0, 0));
            Bare(t, t.center + new IntVec3(8, 0, 1));
            Bare(t, t.center + new IntVec3(10, 0, 0));
            var guns = Guns(t);
            foreach (int step in CastHoldTest.Run(t, caster, CoilGunDefOf.AG_CoilGun_ChainArc, e1,
                () => guns.Fired(caster), CastHoldTest.Asking(() => guns.Holds(caster)))) yield return step;
        }

        // ------------------------------------------------------------------ the battery

        private static Building Battery(RimArtTestContext t, IntVec3 at, float fill)
        {
            var building = (Building)GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Battery")), at, t.map, WipeMode.Vanish);
            building.SetFaction(Faction.OfPlayer);
            building.GetComp<CompPowerBattery>().SetStoredEnergyPct(fill);
            return building;
        }

        [RimArtTest("Coil Gun", "battery 1 recharges next to a charged battery, draining it, and stops at 20")]
        private static IEnumerable<int> Recharge(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, t.center, out CompCoilGun gun);
            Building building = Battery(t, t.center + new IntVec3(1, 0, 0), 1f);
            CompPowerBattery battery = building.GetComp<CompPowerBattery>();
            gun.SetCharge(10f);
            float stored0 = battery.StoredEnergy;
            t.Log("battery " + stored0.ToString("0.##") + " Wd at " + building.Position + " (" + building.OccupiedRect() + "), holder at " + holder.Position
                + ", nearest cell " + Mathf.Sqrt(building.OccupiedRect().ClosestDistSquaredTo(holder.Position)).ToString("0.##") + " away");
            // Real ticks: the map component refills once a second.
            for (int i = 0; i < 4; i++)
            {
                yield return 60;
                t.Log("after " + (i + 1) + " s: " + gun.LabelRemaining + " (" + gun.Charge.ToString("0.##") + "), battery " + battery.StoredEnergy.ToString("0.##") + " Wd, charging " + gun.Charging);
            }
            float gained = gun.Charge - 10f, drained = stored0 - battery.StoredEnergy;
            t.Check(gained >= 3f && gained <= 5f, "about 1 charge a second over 4 s (" + gained.ToString("0.##") + ")");
            t.Check(Mathf.Abs(drained - gained * gun.Props.wattDaysPerCharge) < 0.5f, "the battery lost 10 Wd per charge (" + drained.ToString("0.##") + " Wd for " + gained.ToString("0.##") + ")");
            t.Check(gun.Charging, "the gun reads as charging");
            yield return t.ShotAs("coil-battery-recharge");
            // Moved clock: 30 s more tops it up and stops at the capacity.
            Guns(t).RefillForTest(30f);
            float total = stored0 - battery.StoredEnergy;
            t.Log("after 30 s more: " + gun.LabelRemaining + " (" + gun.Charge.ToString("0.##") + "), battery " + battery.StoredEnergy.ToString("0.##") + " Wd, total drained " + total.ToString("0.##"));
            t.Check(Mathf.Abs(gun.Charge - 20f) < 0.001f, "the battery stopped at 20 (" + gun.Charge.ToString("0.###") + ")");
            t.Check(Mathf.Abs(total - 100f) < 0.5f, "10 charges took 100 Wd in all (" + total.ToString("0.##") + ")");
            float full = battery.StoredEnergy;
            Guns(t).RefillForTest(10f);
            t.Check(Mathf.Abs(battery.StoredEnergy - full) < 0.001f, "a full gun takes nothing more");
        }

        [RimArtTest("Coil Gun", "battery 2 no recharge alone, from an empty battery, or from 3 cells away")]
        private static IEnumerable<int> NoRecharge(RimArtTestContext t)
        {
            t.Clear();
            Pawn holder = Holder(t, t.center, out CompCoilGun gun);
            gun.SetCharge(10f);
            yield return 130;
            Guns(t).RefillForTest(30f);
            t.Check(Mathf.Abs(gun.Charge - 10f) < 0.001f, "alone: no recharge over 2 s + 30 s (" + gun.Charge.ToString("0.##") + ")");

            Building far = Battery(t, t.center + new IntVec3(3, 0, 0), 1f);
            float farStored = far.GetComp<CompPowerBattery>().StoredEnergy;
            Guns(t).RefillForTest(30f);
            t.Check(Mathf.Abs(gun.Charge - 10f) < 0.001f && Mathf.Abs(far.GetComp<CompPowerBattery>().StoredEnergy - farStored) < 0.001f,
                "a charged battery 3 cells away gives nothing (" + gun.Charge.ToString("0.##") + ")");

            Building empty = Battery(t, t.center + new IntVec3(-1, 0, 0), 0f);
            Guns(t).RefillForTest(30f);
            t.Check(Mathf.Abs(gun.Charge - 10f) < 0.001f, "an empty battery next to the holder gives nothing (" + gun.Charge.ToString("0.##") + ")");
            t.Check(empty.GetComp<CompPowerBattery>().StoredEnergy >= 0f, "the empty battery was not drawn below 0");
        }
    }
}
