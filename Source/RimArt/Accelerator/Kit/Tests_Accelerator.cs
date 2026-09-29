using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Game tests for the Accelerator Echo (run with -quicktest -rimarttest=accelerator): the Echo's five
    /// abilities and no charge costs, Vector Flick, the reworked vector shove (wall, bowling, force returned, a
    /// thrown chunk), Plasma (bullets caught during the channel, the burst at the first pawn, a wall, a broken
    /// channel) and the manipulation's strain costs read from XML.
    /// </summary>
    public static class Tests_Accelerator
    {
        private static EchoDef Accelerator => AcceleratorDefOf.AG_Echo_Accelerator;

        private static GameComponent_Echoes Setup(RimArtTestContext t)
        {
            t.Clear();
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            echoes.ResetForTests();
            EchoDevice.workingForTests = false;
            return echoes;
        }

        /// <summary>A drafted colonist made Accelerator's Host and manifested, with a full pool and no strain.</summary>
        private static Pawn Host(RimArtTestContext t, GameComponent_Echoes echoes, IntVec3 at, out EchoRecord record)
        {
            Pawn host = t.Colonist(at);
            record = EchoUtility.ForceHost(Accelerator, host);
            echoes.charge = 100f;
            EchoUtility.Manifest(record);
            host.drafter.Drafted = true;
            host.drafter.FireAtWill = false;
            return host;
        }

        /// <summary>An unarmed enemy with no apparel, told to stand still (armour would turn the numbers into a roll).</summary>
        private static Pawn Target(RimArtTestContext t, IntVec3 at)
        {
            Pawn pawn = t.Enemy(at, armed: false);
            pawn.apparel?.DestroyAll();
            return pawn;
        }

        private static Thing Wall(RimArtTestContext t, IntVec3 at)
        {
            Thing wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.BlocksGranite);
            wall.SetFaction(Faction.OfPlayer);
            return GenSpawn.Spawn(wall, at, t.map);
        }

        private static IEnumerable<int> WaitFor(Func<bool> done, int maxTicks, int step = 2)
        {
            for (int waited = 0; waited < maxTicks && !done(); waited += step) yield return step;
        }

        private static float Health(Pawn pawn) => pawn.Dead ? 0f : pawn.health.summaryHealth.SummaryHealthPercent;
        private static bool Hurt(Pawn pawn) => pawn.Dead || pawn.Downed || Health(pawn) < 0.999f;
        private static bool Untouched(Pawn pawn) => !pawn.Dead && !pawn.Downed && Health(pawn) >= 0.999f;
        private static bool Stunned(Pawn pawn) => pawn.stances?.stunner?.Stunned == true;
        private static float Strain(Pawn pawn) => VectorStrain.Current(pawn);
        private static float InjuryTotal(Pawn pawn) =>
            pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h => h.Severity);

        private static void Finish(EchoRecord record)
        {
            EchoUtility.Revert(record, collapse: false);
            EchoDevice.workingForTests = null;
        }

        // ---- the Echo ---------------------------------------------------------------------------------------------

        [RimArtTest("Accelerator", "echo 1 the Echo grants the five abilities at no charge; the reflex booster grants none")]
        private static IEnumerable<int> Echo(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 2;
            Pawn host = Host(t, echoes, t.center, out EchoRecord record);
            yield return 2;
            var five = new[] { VectorDefOf.AG_VectorReflection, AcceleratorDefOf.AG_VectorSurge, AcceleratorDefOf.AG_VectorShove, AcceleratorDefOf.AG_VectorPlasma, AcceleratorDefOf.AG_VectorFlick };
            foreach (AbilityDef def in five)
            {
                t.Check(host.abilities.GetAbility(def) != null, "has " + def.label);
                t.Check(Accelerator.CastCost(def) == 0f, def.label + " costs no charge (" + Accelerator.CastCost(def) + ")");
            }
            Finish(record);

            Pawn plain = t.Colonist(t.center + new IntVec3(3, 0, 0));
            BodyPartRecord spine = plain.RaceProps.body.AllParts.FirstOrDefault(p => p.def.defName == "Spine");
            plain.health.AddHediff(HediffDef.Named("AG_ReflexBooster"), spine);
            yield return 2;
            t.Check(plain.health.hediffSet.HasHediff(HediffDef.Named("AG_ReflexBooster")), "the implant is installed");
            t.Check(five.All(def => plain.abilities.GetAbility(def) == null), "it grants none of the five");
        }

        // ---- Vector Flick -----------------------------------------------------------------------------------------

        [RimArtTest("Accelerator", "flick 1 always hits the target for 14 blunt; a pawn in between is not hit; no strain")]
        private static IEnumerable<int> Flick(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 2;
            Pawn host = Host(t, echoes, t.center, out EchoRecord record);
            Pawn target = Target(t, t.center + new IntVec3(8, 0, 0));
            Pawn between = Target(t, t.center + new IntVec3(4, 0, 0));
            yield return 5;
            Ability flick = host.abilities.GetAbility(AcceleratorDefOf.AG_VectorFlick);
            flick.QueueCastingJob(target, LocalTargetInfo.Invalid);
            int cast = t.Now;
            foreach (int w in WaitFor(() => Hurt(target), 120)) yield return w;
            t.Log("hit after " + (t.Now - cast) + " ticks: " + RimArtTestContext.Describe(target) + ", injuries " + InjuryTotal(target).ToString("0.#"));
            t.Check(Hurt(target), "the target is hurt");
            t.Check(InjuryTotal(target) >= 7f || target.Dead, "about 14 damage (" + InjuryTotal(target).ToString("0.#") + ")");
            t.Check(Untouched(between), "the pawn in between is not hit");
            t.Check(Strain(host) < 0.001f, "no strain (" + Strain(host).ToString("0.###") + ")");
            t.Check(flick.CooldownTicksRemaining > 0, "the cooldown runs (" + flick.CooldownTicksRemaining + " ticks)");
            yield return t.ShotAs("flick", t.center + new IntVec3(4, 0, 0), 8f);
            Finish(record);
        }

        // ---- vector shove -----------------------------------------------------------------------------------------

        private static MapComponent_Shoves Shoves(RimArtTestContext t) => t.map.GetComponent<MapComponent_Shoves>();

        [RimArtTest("Accelerator", "shove 1 a pawn thrown at a wall stops at it, takes the slam and is stunned; strain 5 %")]
        private static IEnumerable<int> ShoveWall(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 2;
            for (int z = -1; z <= 1; z++) Wall(t, t.center + new IntVec3(6, 0, z));
            Pawn target = Target(t, t.center + new IntVec3(1, 0, 0));
            Pawn host = Host(t, echoes, t.center, out EchoRecord record);
            // Cast at once: a drafted Host next to a hostile would otherwise punch it and sit in a melee cooldown.
            Ability shove = host.abilities.GetAbility(AcceleratorDefOf.AG_VectorShove);
            shove.QueueCastingJob(target, new LocalTargetInfo(t.center + new IntVec3(9, 0, 0)));
            int cast = t.Now;
            yield return 1;
            ShovePlan plan = Shoves(t).Throws.LastOrDefault();
            t.Check(plan != null && plan.walled, "the plan stops at the wall (" + (plan == null ? "no plan" : "walled " + plan.walled + ", land " + plan.land) + ")");
            for (int i = 0; i < 12; i++)
            {
                yield return 3;
                t.Log((t.Now - cast) + ": " + RimArtTestContext.Describe(target));
            }
            foreach (int w in WaitFor(() => plan != null && plan.arrived, 60)) yield return w;
            yield return 2;
            t.Check(target.Position == t.center + new IntVec3(5, 0, 0), "lies at the wall's foot (" + target.Position + ")");
            t.Check(Hurt(target), "hurt by the slam (" + InjuryTotal(target).ToString("0.#") + ")");
            t.Check(Stunned(target) || target.Downed, "stunned");
            t.Check(Math.Abs(Strain(host) - 0.05f) < 0.01f, "strain 5 % (" + Strain(host).ToString("0.###") + ")");
            yield return t.ShotAs("shove-wall", t.center + new IntVec3(3, 0, 0), 8f);
            Finish(record);
        }

        [RimArtTest("Accelerator", "shove 2 a thrown pawn bowls the pawns in its path aside and keeps going")]
        private static IEnumerable<int> ShoveLine(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 2;
            Pawn target = Target(t, t.center + new IntVec3(1, 0, 0));
            Pawn first = Target(t, t.center + new IntVec3(4, 0, 0));
            Pawn second = Target(t, t.center + new IntVec3(6, 0, 0));
            Pawn host = Host(t, echoes, t.center, out EchoRecord record);
            host.abilities.GetAbility(AcceleratorDefOf.AG_VectorShove).QueueCastingJob(target, new LocalTargetInfo(t.center + new IntVec3(12, 0, 0)));
            yield return 1;
            ShovePlan plan = Shoves(t).Throws.LastOrDefault();
            t.Check(plan != null && plan.liners.Count == 2, "two pawns in the path (" + (plan?.liners.Count ?? 0) + ")");
            foreach (int w in WaitFor(() => plan != null && plan.arrived, 90)) yield return w;
            yield return 15;
            t.Log("thrown: " + RimArtTestContext.Describe(target) + "; first: " + RimArtTestContext.Describe(first) + "; second: " + RimArtTestContext.Describe(second));
            t.Check(target.Position.x - t.center.x >= 8, "the thrown pawn went on past them (" + target.Position + ")");
            t.Check(first.Position.z != t.center.z && second.Position.z != t.center.z, "both were knocked off the line");
            t.Check(Hurt(first) && Hurt(second), "both took the slam");
            yield return t.ShotAs("shove-line", t.center + new IntVec3(5, 0, 0), 9f);
            Finish(record);
        }

        [RimArtTest("Accelerator", "shove 3 force returned: a melee hit within 1 s is added to the first pawn struck, not after")]
        private static IEnumerable<int> ShoveReturned(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 2;
            Pawn target = Target(t, t.center + new IntVec3(1, 0, 0));
            Pawn liner = Target(t, t.center + new IntVec3(4, 0, 0));
            Pawn host = Host(t, echoes, t.center, out EchoRecord record);
            host.TakeDamage(new DamageInfo(DamageDefOf.Blunt, 6f, 0f, -1f, target));
            HediffComp_ForceReturn memory = CompAbilityEffect_VectorShove.ForceReturnOf(host);
            t.Check(memory != null && memory.attacker == target && Math.Abs(memory.damage - 6f) < 0.01f, "the hit is remembered (" + (memory == null ? "no comp" : memory.damage + " from " + memory.attacker?.LabelShort) + ")");
            host.abilities.GetAbility(AcceleratorDefOf.AG_VectorShove).QueueCastingJob(target, new LocalTargetInfo(t.center + new IntVec3(12, 0, 0)));
            yield return 1;
            ShovePlan plan = Shoves(t).Throws.LastOrDefault(p => p.thrown == target);
            t.Check(plan != null && plan.returned && Math.Abs(plan.bonus - 6f) < 0.01f, "the throw carries 6 returned (" + (plan?.bonus ?? -1f) + ")");
            foreach (int w in WaitFor(() => plan != null && plan.arrived, 90)) yield return w;
            ShovePlan.Liner struck = plan?.liners.FirstOrDefault();
            t.Check(struck != null && Math.Abs(struck.bonus - 6f) < 0.01f, "the first pawn struck took it (" + (struck?.bonus ?? -1f) + ")");
            t.Check(plan != null && plan.bonus == 0f, "the thrown pawn did not also take it");
            Finish(record);

            // Past the window: nothing returned.
            yield return 5;
            Setup(t);
            yield return 2;
            Pawn late = Target(t, t.center + new IntVec3(1, 0, 0));
            Pawn host2 = Host(t, echoes, t.center, out EchoRecord record2);
            host2.TakeDamage(new DamageInfo(DamageDefOf.Blunt, 6f, 0f, -1f, late));
            // The hit is moved 75 ticks into the past rather than waited out: a drafted Host next to a hostile
            // punches it by itself, and the melee cooldown would end the queued cast.
            HediffComp_ForceReturn memory2 = CompAbilityEffect_VectorShove.ForceReturnOf(host2);
            if (memory2 != null) memory2.tick -= 75;
            host2.abilities.GetAbility(AcceleratorDefOf.AG_VectorShove).QueueCastingJob(late, new LocalTargetInfo(t.center + new IntVec3(12, 0, 0)));
            yield return 1;
            ShovePlan plan2 = Shoves(t).Throws.LastOrDefault(p => p.thrown == late);
            t.Check(plan2 != null && !plan2.returned, "a hit 1.25 s old is not returned (" + (plan2 == null ? "no throw" : "returned " + plan2.bonus) + ")");
            Finish(record2);
        }

        [RimArtTest("Accelerator", "shove 4 a stone chunk is thrown at the first pawn in its path and lands there")]
        private static IEnumerable<int> ShoveChunk(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 2;
            Thing chunk = GenSpawn.Spawn(DefDatabase<ThingDef>.GetNamed("ChunkGranite"), t.center + new IntVec3(1, 0, 0), t.map);
            Pawn shooter = Target(t, t.center + new IntVec3(8, 0, 0));
            Pawn host = Host(t, echoes, t.center, out EchoRecord record);
            host.abilities.GetAbility(AcceleratorDefOf.AG_VectorShove).QueueCastingJob(chunk, new LocalTargetInfo(t.center + new IntVec3(12, 0, 0)));
            yield return 1;
            ShovePlan plan = Shoves(t).Throws.LastOrDefault();
            t.Check(plan != null && plan.struck == shooter, "it will hit the shooter (" + (plan?.struck?.LabelShort ?? "none") + ", damage " + (plan?.thingDamage ?? 0f).ToString("0") + ")");
            yield return 10;
            t.Check(!chunk.Spawned, "the chunk is in the air");
            foreach (int w in WaitFor(() => plan != null && plan.arrived, 90)) yield return w;
            yield return 2;
            t.Check(Hurt(shooter), "the shooter is hurt (" + InjuryTotal(shooter).ToString("0.#") + ")");
            t.Check(chunk.Spawned && chunk.Position.DistanceTo(shooter.Position) <= 2f, "the chunk lies by him (" + (chunk.Spawned ? chunk.Position.ToString() : "not spawned") + ")");
            yield return t.ShotAs("shove-chunk", t.center + new IntVec3(4, 0, 0), 8f);
            Finish(record);
        }

        // ---- Plasma -----------------------------------------------------------------------------------------------

        private static MapComponent_Plasma Plasmas(RimArtTestContext t) => t.map.GetComponent<MapComponent_Plasma>();

        [RimArtTest("Accelerator", "plasma 1 the channel catches bullets; the release bursts at the first pawn, not the one behind; strain 45 %")]
        private static IEnumerable<int> PlasmaBurst(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 2;
            Pawn host = Host(t, echoes, t.center, out EchoRecord record);
            Pawn first = Target(t, t.center + new IntVec3(9, 0, 0));
            Pawn behind = Target(t, t.center + new IntVec3(13, 0, 0));
            Pawn shooter = Target(t, t.center + new IntVec3(0, 0, 11));
            yield return 5;
            Ability plasma = host.abilities.GetAbility(AcceleratorDefOf.AG_VectorPlasma);
            plasma.QueueCastingJob(t.center + new IntVec3(12, 0, 0), LocalTargetInfo.Invalid);
            int cast = t.Now;
            yield return 30;
            PlasmaChannel channel = Plasmas(t).For(host);
            t.Check(channel != null && channel.Channelling, "channelling (" + RimArtTestContext.Describe(host) + ")");
            float before = Health(host);
            var bullet = (Projectile)GenSpawn.Spawn(DefDatabase<ThingDef>.GetNamed("Bullet_Revolver"), shooter.Position, t.map);
            bullet.Launch(shooter, shooter.DrawPos, host, host, ProjectileHitFlags.All);
            yield return 20;
            t.Check(bullet.Destroyed, "the bullet was caught");
            t.Check(channel != null && channel.caught.Count >= 1, "one catch drawn (" + (channel?.caught.Count ?? 0) + ")");
            t.Check(Health(host) >= before, "Accelerator was not hit");
            yield return 30;
            yield return t.ShotAs("plasma-channel", t.center + new IntVec3(3, 0, 0), 10f);
            foreach (int w in WaitFor(() => channel != null && channel.burst, 200)) yield return w;
            t.Log("burst " + (t.Now - cast) + " ticks after the order at " + channel?.burstCell + ", stop " + channel?.stop.ToString("0.#"));
            t.Check(channel != null && channel.burst, "it burst");
            t.Check(channel != null && channel.burstCell == first.Position, "at the first pawn (" + channel?.burstCell + ")");
            yield return 5;
            t.Check(Hurt(first), "the first pawn is hurt");
            t.Check(Untouched(behind), "the pawn 4 cells behind is not");
            t.Check(Untouched(host), "Accelerator is not hurt by his burst");
            t.Check(Math.Abs(Strain(host) - 0.45f) < 0.01f, "strain 45 % (" + Strain(host).ToString("0.###") + ")");
            t.Check(plasma.CooldownTicksRemaining > 0, "the cooldown runs");
            yield return 20;
            yield return t.ShotAs("plasma-burst", t.center + new IntVec3(6, 0, 0), 10f);
            Finish(record);
        }

        [RimArtTest("Accelerator", "plasma 2 a wall across the lane stops it; the pawn behind the wall is not hit")]
        private static IEnumerable<int> PlasmaWall(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 2;
            Pawn host = Host(t, echoes, t.center, out EchoRecord record);
            for (int z = -1; z <= 1; z++) Wall(t, t.center + new IntVec3(5, 0, z));
            Pawn behind = Target(t, t.center + new IntVec3(8, 0, 0));
            yield return 5;
            host.abilities.GetAbility(AcceleratorDefOf.AG_VectorPlasma).QueueCastingJob(t.center + new IntVec3(12, 0, 0), LocalTargetInfo.Invalid);
            yield return 5;
            PlasmaChannel channel = Plasmas(t).For(host);
            foreach (int w in WaitFor(() => channel != null && channel.burst, 260)) yield return w;
            t.Check(channel != null && channel.walled && channel.burstCell == t.center + new IntVec3(4, 0, 0), "it stopped at the wall (" + channel?.burstCell + ")");
            yield return 5;
            t.Check(Untouched(behind), "the pawn behind the wall is not hit");
            Finish(record);
        }

        [RimArtTest("Accelerator", "plasma 3 a stun during the channel breaks it: no burst, no strain, the cooldown is spent")]
        private static IEnumerable<int> PlasmaBroken(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 2;
            Pawn host = Host(t, echoes, t.center, out EchoRecord record);
            Pawn first = Target(t, t.center + new IntVec3(6, 0, 0));
            yield return 5;
            Ability plasma = host.abilities.GetAbility(AcceleratorDefOf.AG_VectorPlasma);
            plasma.QueueCastingJob(first, LocalTargetInfo.Invalid);
            yield return 60;
            PlasmaChannel channel = Plasmas(t).For(host);
            t.Check(channel != null && channel.Channelling, "channelling");
            host.stances.stunner.StunFor(60, first, false, true);
            yield return 5;
            t.Check(channel != null && channel.cancelTick >= 0, "the channel is broken");
            yield return 200;
            t.Check(channel != null && !channel.burst && Untouched(first), "no burst");
            t.Check(Strain(host) < 0.001f, "no strain (" + Strain(host).ToString("0.###") + ")");
            t.Check(plasma.CooldownTicksRemaining > 0, "the cooldown is spent (" + plasma.CooldownTicksRemaining + " ticks)");
            Finish(record);
        }

        // ---- vector manipulation ----------------------------------------------------------------------------------

        [RimArtTest("Accelerator", "manipulation 1 strain costs come from the ability def")]
        private static IEnumerable<int> ManipulationStrain(RimArtTestContext t)
        {
            yield return 1;
            float[] expected = { 0.08f, 0.24f, 0.48f, 0.80f };
            for (int i = 0; i < 4; i++)
                t.Check(Math.Abs(VectorEditDefaults.StrainCostFor(i + 1) - expected[i]) < 0.001f, (i + 1) + " groups cost " + VectorEditDefaults.StrainCostFor(i + 1));
            var props = AcceleratorKit.Props<CompProperties_VectorManipulation>(VectorDefOf.AG_VectorReflection);
            float old = props.strainCosts[0];
            props.strainCosts[0] = 0.1f;
            t.Check(Math.Abs(VectorEditDefaults.StrainCostFor(1) - 0.1f) < 0.001f, "an XML change is read (" + VectorEditDefaults.StrainCostFor(1) + ")");
            props.strainCosts[0] = old;
        }
    }
}
