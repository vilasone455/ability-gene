using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using static RimArt.RimArtTestContext;

namespace RimArt
{
    /// <summary>
    /// Game tests for Pain's Gravity Well (run with -quicktest -rimarttest="Gravity Well"): growth from what it
    /// eats, the duration cap, the implosion from eaten, drift and its leash, eaten rounds, the Echo charge, and a
    /// timing trace with 20 pawns and a stockpile in the well.
    /// </summary>
    public static class Tests_GravityWell
    {
        private static AbilityDef Well => DefDatabase<AbilityDef>.GetNamed("AG_GravityWell");
        private static CompProperties_AbilityGravityWell Props => CompProperties_AbilityGravityWell.For(Well);

        private static MapComponent_Gravity Setup(RimArtTestContext t)
        {
            t.Clear();
            GameComponent_Gravity.Instance.ResetForTests();
            GameComponent_Echoes.Get.ResetForTests();
            startHealth.Clear();
            return t.map.GetComponent<MapComponent_Gravity>();
        }

        private static void NoWimp(Pawn pawn)
        {
            NoWimp(pawn);
        }

        /// <summary>A drafted, unarmed, unclothed colonist with the ability granted directly (not through the Echo).</summary>
        private static Pawn Caster(RimArtTestContext t, IntVec3 at, bool grant = true)
        {
            Pawn pawn = t.Colonist(at);
            pawn.apparel?.DestroyAll();
            pawn.equipment?.DestroyAllEquipment();
            pawn.drafter.FireAtWill = false;
            NoWimp(pawn);
            if (grant) pawn.abilities.GainAbility(Well);
            startHealth[pawn] = new HashSet<Hediff>(Wounds(pawn));
            return pawn;
        }

        /// <summary>
        /// A hostile that stands still: a baseliner (some xenotypes deflect blunt damage), unarmed, no
        /// apparel, no Wimp, stunned.
        /// </summary>
        private static Pawn Victim(RimArtTestContext t, IntVec3 at, int stunTicks = 3000)
        {
            Faction faction = Find.FactionManager.RandomEnemyFaction(allowNonHumanlike: false);
            var request = new PawnGenerationRequest(faction?.def.basicMemberKind ?? PawnKindDefOf.Villager, faction,
                mustBeCapableOfViolence: true, dontGiveWeapon: true,
                forcedXenotype: ModsConfig.BiotechActive ? XenotypeDefOf.Baseliner : null);
            Pawn pawn = PawnGenerator.GeneratePawn(request);
            GenSpawn.Spawn(pawn, at, t.map);
            RimArtTestContext.Hold(pawn);
            pawn.apparel?.DestroyAll();
            NoWimp(pawn);
            pawn.stances.stunner.StunFor(stunTicks, null, false);
            startHealth[pawn] = new HashSet<Hediff>(Wounds(pawn));
            return pawn;
        }

        // Hurt: dead, or an injury or missing part that was not there when the pawn was set up. Read
        // from the hediffs; the summary health is cached and a destroyed part removes the injuries under it.
        private static readonly Dictionary<Pawn, HashSet<Hediff>> startHealth = new Dictionary<Pawn, HashSet<Hediff>>();
        private static IEnumerable<Hediff> Wounds(Pawn pawn) => pawn.health.hediffSet.hediffs.Where(h => h is Hediff_Injury || h is Hediff_MissingPart);
        private static bool Hurt(Pawn pawn) => pawn.Dead
            || Wounds(pawn).Any(h => !startHealth.TryGetValue(pawn, out var old) || !old.Contains(h));
        private static string Injuries(Pawn pawn) => string.Join(", ", Wounds(pawn)
            .Select(h => h.LabelCap + " " + h.Part?.Label + (startHealth.TryGetValue(pawn, out var old) && old.Contains(h) ? " (old)" : "")));

        private static Thing Item(RimArtTestContext t, ThingDef def, int count, IntVec3 at)
        {
            Thing thing = ThingMaker.MakeThing(def);
            thing.stackCount = count;
            return GenSpawn.Spawn(thing, at, t.map);
        }

        private static GravityCast Open(MapComponent_Gravity component, Pawn caster, IntVec3 cell) =>
            component.Begin(caster, cell, Well) ? component.For(caster) : null;

        private static string State(GravityCast cast) => $"{cast.clock.phase} tick {cast.clock.ticks}: eaten {cast.eaten:0.##}, "
            + $"radius {cast.Radius:0.00}, duration {cast.DurationTicks / 60f:0.00} s, left {cast.TicksLeft / 60f:0.00} s, "
            + $"centre ({cast.Centre.x - cast.origin.x:+0.00;-0.00}, {cast.Centre.z - cast.origin.z:+0.00;-0.00}) from the cast point";

        // Overrides some balance fields for one test; Restore puts the XML values back.
        private static readonly Dictionary<string, float> saved = new Dictionary<string, float>();
        private static void Override(string field, float value)
        {
            var info = typeof(CompProperties_AbilityGravityWell).GetField(field);
            if (!saved.ContainsKey(field)) saved[field] = (float)info.GetValue(Props);
            info.SetValue(Props, value);
        }
        private static void Restore()
        {
            foreach (var pair in saved) typeof(CompProperties_AbilityGravityWell).GetField(pair.Key).SetValue(Props, pair.Value);
            saved.Clear();
        }

        [RimArtTest("Gravity Well", "1 grows: items and a pawn are eaten once each, radius and duration rise", 2400)]
        private static IEnumerable<int> Grows(RimArtTestContext t)
        {
            var component = Setup(t);
            IntVec3 c = t.center;
            var props = Props;
            Pawn caster = Caster(t, c + new IntVec3(-8, 0, 0));
            Thing near = Item(t, ThingDefOf.Steel, 50, c + new IntVec3(1, 0, 0));
            Thing pulled = Item(t, ThingDefOf.Steel, 40, c + new IntVec3(2, 0, 1));
            Thing wood = Item(t, ThingDefOf.WoodLog, 30, c + new IntVec3(0, 0, -2));
            Thing far = Item(t, ThingDefOf.Steel, 20, c + new IntVec3(6, 0, 0));
            IntVec3 farCell = far.Position;
            Pawn victim = Victim(t, c + new IntVec3(0, 0, 2));
            var things = new[] { near, pulled, wood, far };
            float items = things.Sum(props.Mass), body = props.bodyMass * victim.BodySize;
            t.Log($"items {string.Join(", ", things.Select(x => x.LabelCap + " " + props.Mass(x).ToString("0.#") + " kg"))}; pawn {body:0.#}");
            GravityCast cast = Open(component, caster, c);
            if (!t.Check(cast != null, "the well opened")) yield break;
            t.Check(Mathf.Approximately(cast.Radius, 3f) && cast.DurationTicks == 240, $"opens at radius 3 and 4 s ({State(cast)})");
            yield return GravityRules.OpeningTicks + 1;
            t.Log(State(cast));
            yield return t.ShotAs("well opening");
            t.Check(near.Destroyed, "the stack placed in the core was eaten on the first tick");
            t.Check(!far.Destroyed && far.Position == farCell && cast.Radius < 6f, "the stack 6 cells out is outside the opening pull and has not moved");
            float lastRadius = cast.Radius;
            int lastDuration = cast.DurationTicks;
            bool neverShrank = true;
            for (int i = 0; i < 90 && cast.Active && (things.Any(x => !x.Destroyed) || !cast.eatenPawns.Contains(victim.thingIDNumber)); i++)
            {
                yield return 10;
                if (i % 3 == 0) t.Log(State(cast) + $"; left: {string.Join(", ", things.Where(x => !x.Destroyed).Select(x => x.def.defName + "@" + x.Position))}");
                neverShrank &= cast.Radius >= lastRadius - 0.0001f && cast.DurationTicks >= lastDuration;
                lastRadius = cast.Radius; lastDuration = cast.DurationTicks;
            }
            t.Log(State(cast));
            t.Check(things.All(x => x.Destroyed), "all four stacks reached the core and were destroyed");
            t.Check(cast.eatenPawns.Count(id => id == victim.thingIDNumber) == 1, "the pawn was counted once");
            t.Check(Mathf.Abs(cast.eaten - (items + body)) < 0.05f, $"eaten {cast.eaten:0.##} = items {items:0.##} + pawn {body:0.##}, each once");
            t.Check(neverShrank, "radius and duration only rose");
            t.Check(Mathf.Abs(cast.Radius - props.PullRadius(cast.eaten)) < 0.001f && cast.Radius > 6f,
                $"radius {cast.Radius:0.00} = 3 + 7 x eaten / 200");
            t.Check(cast.DurationTicks == Mathf.RoundToInt(Mathf.Min(15f, 4f + cast.eaten / 10f) * 60f),
                $"duration {cast.DurationTicks / 60f:0.00} s = 4 s + 1 s per 10 eaten");
            t.Check(victim.Dead || (victim.Spawned && (victim.Position.ToVector3Shifted() - cast.Centre).Yto0().magnitude <= props.coreRadius + 0.75f),
                "the pawn is held at the core, not destroyed");
            yield return 60;
            t.Check(victim.Dead || Hurt(victim), "the held pawn took core damage");
            yield return t.ShotAs("well grown");
            cast.Finish(true);
            yield return 40;
        }

        [RimArtTest("Gravity Well", "2 duration: 4 s + 1 s per 10 eaten, at most 15 s, then it implodes", 2600)]
        private static IEnumerable<int> Duration(RimArtTestContext t)
        {
            var component = Setup(t);
            IntVec3 c = t.center;
            Pawn first = Caster(t, c + new IntVec3(-8, 0, 0));
            Item(t, ThingDefOf.Steel, 60, c);
            GravityCast cast = Open(component, first, c);
            if (!t.Check(cast != null, "the first well opened")) yield break;
            yield return GravityRules.OpeningTicks + 1;
            t.Log(State(cast));
            t.Check(Mathf.Abs(cast.eaten - 30f) < 0.01f && cast.DurationTicks == 420, "30 eaten gives 7 s");
            foreach (int step in WaitFor(() => !cast.Active, 600)) yield return step;
            t.Log(State(cast));
            t.Check(!cast.Active && cast.clock.imploded && cast.clock.ticks == 420, $"imploded by itself after {cast.clock.ticks / 60f:0.00} s");
            yield return 40;
            first.Destroy();

            Pawn second = Caster(t, c + new IntVec3(-8, 0, -3));
            foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(new TargetInfo(c, t.map)).Append(c))
                Item(t, ThingDefOf.Steel, 75, cell);
            cast = Open(component, second, c);
            if (!t.Check(cast != null, "the second well opened")) yield break;
            yield return GravityRules.OpeningTicks + 1;
            t.Log(State(cast));
            t.Check(cast.eaten >= 330f && cast.DurationTicks == 900 && Mathf.Approximately(cast.Radius, 10f),
                $"{cast.eaten:0.#} eaten is capped at 15 s and radius 10");
            foreach (int step in WaitFor(() => !cast.Active, 1000, 30))
            {
                if (cast.clock.ticks % 150 < 30) t.Log(State(cast));
                yield return step;
            }
            foreach (int step in WaitFor(() => !cast.Active, 40)) yield return step;
            t.Log(State(cast));
            t.Check(!cast.Active && cast.clock.imploded && cast.clock.ticks == 900, $"imploded by itself after {cast.clock.ticks / 60f:0.00} s");
            t.Check(Mathf.Approximately(cast.burstDamage, 45f) && Mathf.Approximately(cast.burstRadius, 3f),
                $"full implosion {cast.burstDamage:0.#} damage, radius {cast.burstRadius:0.##}");
            yield return 40;
        }

        private static readonly List<string> damageTrace = new List<string>();
        public static void TraceDamage(Thing __instance, DamageInfo dinfo, DamageWorker.DamageResult __result) =>
            damageTrace.Add($"TakeDamage {__instance.LabelShort} ({(__instance as Pawn)?.kindDef.defName}, {(__instance as Pawn)?.genes?.Xenotype?.defName}): "
                + $"{dinfo.Def.defName} {dinfo.Amount:0.#}, dealt {__result?.totalDamageDealt:0.#}, deflected {__result?.deflected}, "
                + $"parts [{string.Join(", ", __result?.parts?.Select(x => x.Label) ?? new string[0])}], hediffs [{string.Join(", ", __result?.hediffs?.Select(x => x.LabelCap) ?? new string[0])}]");

        [RimArtTest("Gravity Well", "3 implosion: damage and radius follow eaten", 1200)]
        private static IEnumerable<int> Implosion(RimArtTestContext t)
        {
            var component = Setup(t);
            var props = Props;
            t.Check(props.Damage(0f) == 15f && props.Damage(100f) == 30f && props.Damage(200f) == 45f && props.Damage(900f) == 45f,
                "damage 15 / 30 / 45 / 45 at 0 / 100 / 200 / 900 eaten");
            t.Check(props.BurstRadius(0f) == 2f && props.BurstRadius(100f) == 2.5f && props.BurstRadius(400f) == 3f,
                "radius 2 / 2.5 / 3 at 0 / 100 / 400 eaten");
            IntVec3 c = t.center;
            Pawn caster = Caster(t, c + new IntVec3(-8, 0, 0));
            Item(t, ThingDefOf.Steel, 75, c);
            Item(t, ThingDefOf.Steel, 75, c + new IntVec3(1, 0, 0));
            Item(t, ThingDefOf.Steel, 50, c + new IntVec3(0, 0, 1));
            GravityCast cast = Open(component, caster, c);
            if (!t.Check(cast != null, "the well opened")) yield break;
            yield return GravityRules.OpeningTicks + 1;
            t.Log(State(cast));
            t.Check(Mathf.Abs(cast.eaten - 100f) < 0.01f, "100 eaten");
            var insides = new[] { new IntVec3(2, 0, 0), new IntVec3(-2, 0, 0), new IntVec3(1, 0, -2) }.Select(d => Victim(t, c + d)).ToList();
            var outsides = new[] { new IntVec3(0, 0, 3), new IntVec3(-3, 0, 0) }.Select(d => Victim(t, c + d)).ToList();
            var harmony = new Harmony("RimArt.GravityDamageTrace");
            harmony.Patch(AccessTools.Method(typeof(Thing), nameof(Thing.TakeDamage)),
                postfix: new HarmonyMethod(typeof(Tests_GravityWell), nameof(TraceDamage)));
            damageTrace.Clear();
            try { cast.Finish(true); }
            finally { harmony.UnpatchAll("RimArt.GravityDamageTrace"); }
            foreach (string line in damageTrace) t.Log(line);
            yield return 2;
            t.Log($"burst {cast.burstDamage:0.##} damage, radius {cast.burstRadius:0.##}, hit [{string.Join(", ", cast.burstHit.Select(p => p.LabelShort))}]");
            foreach (Pawn pawn in insides.Concat(outsides)) t.Log($"{pawn.LabelShort} at {pawn.Position - c}: {Injuries(pawn)}");
            t.Check(insides.All(cast.burstHit.Contains) && !outsides.Any(cast.burstHit.Contains),
                "the burst chose the three pawns 2-2.24 cells out and not the two 3 cells out");
            t.Check(Mathf.Approximately(cast.burstDamage, 30f) && Mathf.Approximately(cast.burstRadius, 2.5f), "30 damage in 2.5 cells");
            t.Check(insides.All(Hurt), "the pawns 2-2.24 cells out were hurt");
            t.Check(!outsides.Any(Hurt), "the pawns 3 cells out were not");
            int cooldown = GameComponent_Gravity.Instance.Remaining(caster, Well);
            t.Check(cooldown == 2398, $"cooldown 40 s ({cooldown} ticks left 2 ticks later)");
            yield return 40;
        }

        [RimArtTest("Gravity Well", "4 drift: toward the heaviest thing, an ally, at 0.5 cells per second", 1200)]
        private static IEnumerable<int> Drift(RimArtTestContext t)
        {
            var component = Setup(t);
            IntVec3 c = t.center;
            Pawn caster = Caster(t, c + new IntVec3(-8, 0, 0));
            Pawn ally = t.Colonist(c + new IntVec3(0, 0, 2));
            ally.drafter.Drafted = false;
            ally.apparel?.DestroyAll();
            NoWimp(ally);
            ally.stances.stunner.StunFor(600, null, false);
            Thing light = Item(t, ThingDefOf.Steel, 10, c + new IntVec3(0, 0, -2));
            GravityCast cast = Open(component, caster, c);
            if (!t.Check(cast != null, "the well opened")) yield break;
            yield return GravityRules.OpeningTicks;
            Vector3 start = cast.Centre;
            float fastest = 0f;
            Vector3 last = start;
            for (int i = 0; i < 30; i++)
            {
                yield return 1;
                fastest = Mathf.Max(fastest, (cast.Centre - last).Yto0().magnitude * 60f);
                last = cast.Centre;
            }
            t.Log(State(cast) + $"; target {cast.driftTarget?.LabelShort ?? "none"}; fastest {fastest:0.###} cells/s");
            t.Check(cast.driftTarget == ally, "the target is the ally (60) rather than the steel (5)");
            // The ally is pulled in at up to 6 cells per second and the centre stops once it holds it.
            t.Check(cast.Centre.z > start.z + 0.05f && Mathf.Abs(cast.Centre.x - start.x) < 0.02f, "the centre moved north, toward the ally");
            t.Check(fastest <= 0.5f + 0.01f, "no faster than 0.5 cells per second");
            cast.Finish(false);
            yield return 40;
        }

        [RimArtTest("Gravity Well", "5 drift leash: never more than 5 cells from the cast point", 1800)]
        private static IEnumerable<int> Leash(RimArtTestContext t)
        {
            var component = Setup(t);
            IntVec3 c = t.center;
            // No pull, so the target stays 8 cells out; a 9-cell radius so it is inside; 12 s to walk 5 cells.
            Override("pullSpeed", 0f);
            Override("minRadius", 9f);
            Override("baseSeconds", 12f);
            try
            {
                Pawn caster = Caster(t, c + new IntVec3(-8, 0, 0));
                Pawn target = Victim(t, c + new IntVec3(8, 0, 0));
                GravityCast cast = Open(component, caster, c);
                if (!t.Check(cast != null, "the well opened")) yield break;
                yield return GravityRules.OpeningTicks;
                float farthest = 0f;
                for (int i = 0; i < 26 && cast.Active; i++)
                {
                    yield return 30;
                    float from = (cast.Centre - cast.origin).Yto0().magnitude;
                    farthest = Mathf.Max(farthest, from);
                    if (i % 4 == 0) t.Log(State(cast) + $"; {from:0.00} cells from the cast point");
                }
                float final = (cast.Centre - cast.origin).Yto0().magnitude;
                t.Log(State(cast) + $"; farthest {farthest:0.000}");
                t.Check(cast.driftTarget == target, "the target was the pawn 8 cells east");
                t.Check(farthest <= 5.001f && final >= 4.99f, $"stopped at the 5-cell leash ({final:0.000})");
                t.Check(Mathf.Abs(cast.Centre.z - cast.origin.z) < 0.01f && cast.Centre.x > cast.origin.x, "moved straight east");
                cast.Finish(false);
            }
            finally { Restore(); }
            yield return 40;
        }

        [RimArtTest("Gravity Well", "6 rounds: a bullet counts 2, a grenade 10 and does not explode", 1200)]
        private static IEnumerable<int> RoundsEaten(RimArtTestContext t)
        {
            var component = Setup(t);
            IntVec3 c = t.center;
            Pawn caster = Caster(t, c + new IntVec3(-8, 0, 0));
            Pawn shooter = Caster(t, c + new IntVec3(0, 0, -8), grant: false);
            IntVec3 aim = c + new IntVec3(0, 0, 9);
            GravityCast cast = Open(component, caster, c);
            if (!t.Check(cast != null, "the well opened")) yield break;
            yield return GravityRules.OpeningTicks + 1;
            var bullet = (Projectile)GenSpawn.Spawn(DefDatabase<ThingDef>.GetNamed("Bullet_Revolver"), shooter.Position, t.map);
            bullet.Launch(shooter, shooter.DrawPos, aim, aim, ProjectileHitFlags.IntendedTarget);
            foreach (int step in WaitFor(() => bullet.Destroyed, 60)) yield return step;
            t.Log(State(cast));
            t.Check(bullet.Destroyed && Mathf.Abs(cast.eaten - 2f) < 0.001f, "the bullet was eaten: 2");
            var grenade = (Projectile)GenSpawn.Spawn(DefDatabase<ThingDef>.GetNamed("Proj_GrenadeFrag"), shooter.Position, t.map);
            grenade.Launch(shooter, shooter.DrawPos, aim, aim, ProjectileHitFlags.IntendedTarget);
            foreach (int step in WaitFor(() => grenade.Destroyed, 150)) yield return step;
            t.Log(State(cast));
            t.Check(grenade.Destroyed && Mathf.Abs(cast.eaten - 12f) < 0.001f, "the grenade was eaten: 10");
            yield return 120;
            t.Check(!Hurt(shooter) && !Hurt(caster) && !t.map.listerThings.AllThings.Any(x => x.def.defName == "Proj_GrenadeFrag"),
                "no explosion afterwards");
            cast.Finish(false);
            yield return 40;
        }

        [RimArtTest("Gravity Well", "7 echo: Pain pays 20 charge once when the well opens; a short pool disables it", 1500)]
        private static IEnumerable<int> EchoCharge(RimArtTestContext t)
        {
            var component = Setup(t);
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            EchoDevice.workingForTests = false;
            try
            {
                IntVec3 c = t.center;
                EchoDef pain = DefDatabase<EchoDef>.GetNamed("AG_Echo_Pain");
                Pawn host = Caster(t, c + new IntVec3(-8, 0, 0), grant: false);
                EchoRecord record = EchoUtility.ForceHost(pain, host);
                echoes.charge = 100f;
                t.Check(EchoUtility.Manifest(record), "Pain manifested");
                host.drafter.Drafted = true;
                yield return 2;
                Ability ability = host.abilities.GetAbility(Well, true);
                float cost = pain.CastCost(Well);
                t.Check(ability != null, "the Echo grants Gravity Well");
                t.Check(cost == 20f, $"Pain's cast cost for it is {cost}");
                if (ability == null) yield break;

                echoes.charge = cost - 1f;
                var button = ability.GetGizmos().OfType<Command_Action>().FirstOrDefault();
                t.Check(button != null && button.Disabled, $"the button is disabled with {echoes.charge} charge ({button?.disabledReason})");
                t.Check(!component.Begin(host, c, Well) && component.For(host) == null && echoes.charge == cost - 1f,
                    "it does not open and takes nothing");

                echoes.charge = 100f;
                button = ability.GetGizmos().OfType<Command_Action>().FirstOrDefault();
                t.Check(button != null && !button.Disabled, "the button is enabled with 100 charge");
                GravityCast cast = Open(component, host, c);
                t.Check(cast != null && Mathf.Abs(echoes.charge - 80f) < 0.001f, $"it opened and took 20 (now {echoes.charge:0.###})");
                if (cast == null) yield break;
                yield return 120;
                float upkeep = pain.upkeepPerHour * 125f / 2500f;
                t.Log(State(cast) + $"; charge {echoes.charge:0.###}");
                t.Check(cast.Field && echoes.charge > 80f - upkeep - 0.1f && echoes.charge <= 80f,
                    "open for 1.5 s: nothing more taken beyond upkeep");
                cast.Finish(false);
                yield return 2;
                t.Check(echoes.charge > 80f - upkeep - 0.1f && echoes.charge <= 80f, "no refund once it has opened");

                GameComponent_Gravity.Instance.ResetForTests();
                foreach (int step in WaitFor(() => component.For(host) == null, 60)) yield return step;
                float before = echoes.charge;
                cast = Open(component, host, c);
                t.Check(cast != null && Mathf.Abs(echoes.charge - (before - 20f)) < 0.001f, "a second cast took 20");
                if (cast == null) yield break;
                yield return 10;
                cast.Finish(false);
                // Upkeep may have taken one 60-tick step (0.36) in the 10 ticks.
                t.Check(echoes.charge > before - 0.4f && echoes.charge <= before + 0.001f,
                    $"cancelled while opening: the 20 came back ({before:0.###} before, {echoes.charge:0.###} after)");
                t.Check(GameComponent_Gravity.Instance.Remaining(host, Well) == 0, "and no cooldown");
                yield return 40;
                EchoUtility.Revert(record, collapse: false);
            }
            finally { EchoDevice.workingForTests = null; }
        }

        // ---- performance ----

        private static readonly Stopwatch tickWatch = new Stopwatch(), compWatch = new Stopwatch(), updateWatch = new Stopwatch(), meshWatch = new Stopwatch();
        private static long tickTime, compTime, updateTime, meshTime;
        private static int ticks, frames, startPaths, meshDirty;
        public static void TickPre() => tickWatch.Restart();
        public static void TickPost() { tickTime += tickWatch.ElapsedTicks; ticks++; }
        public static void CompPre() => compWatch.Restart();
        public static void CompPost() => compTime += compWatch.ElapsedTicks;
        public static void UpdatePre() => updateWatch.Restart();
        public static void UpdatePost() { updateTime += updateWatch.ElapsedTicks; frames++; }
        public static void MeshPre() => meshWatch.Restart();
        public static void MeshPost() => meshTime += meshWatch.ElapsedTicks;
        public static void CountPath() => startPaths++;
        public static void CountDirty() => meshDirty++;

        private static void ResetTimes() { tickTime = compTime = updateTime = meshTime = 0; ticks = frames = startPaths = meshDirty = 0; }
        private static double Ms(long stopwatchTicks) => stopwatchTicks * 1000.0 / Stopwatch.Frequency;
        private static void Report(RimArtTestContext t, string window)
        {
            t.Log($"{window}: {ticks} ticks, {frames} frames; DoSingleTick {Ms(tickTime) / Mathf.Max(1, ticks):0.000} ms/tick, "
                + $"MapComponentTick {Ms(compTime) / Mathf.Max(1, ticks):0.000} ms/tick, MapUpdate {Ms(updateTime) / Mathf.Max(1, frames):0.000} ms/frame "
                + $"(mesh regen {Ms(meshTime) / Mathf.Max(1, frames):0.000}), StartPath {startPaths} ({startPaths / (float)Mathf.Max(1, ticks):0.00}/tick), "
                + $"MapMeshDirty {meshDirty} ({meshDirty / (float)Mathf.Max(1, ticks):0.00}/tick)");
            ResetTimes();
        }

        private static Harmony Hooks()
        {
            var harmony = new Harmony("RimArt.GravityPerf");
            var self = typeof(Tests_GravityWell);
            harmony.Patch(AccessTools.Method(typeof(TickManager), nameof(TickManager.DoSingleTick)),
                new HarmonyMethod(self, nameof(TickPre)), new HarmonyMethod(self, nameof(TickPost)));
            harmony.Patch(AccessTools.Method(typeof(MapComponent_Gravity), nameof(MapComponent_Gravity.MapComponentTick)),
                new HarmonyMethod(self, nameof(CompPre)), new HarmonyMethod(self, nameof(CompPost)));
            harmony.Patch(AccessTools.Method(typeof(Map), nameof(Map.MapUpdate)),
                new HarmonyMethod(self, nameof(UpdatePre)), new HarmonyMethod(self, nameof(UpdatePost)));
            harmony.Patch(AccessTools.Method(typeof(MapDrawer), nameof(MapDrawer.MapMeshDrawerUpdate_First)),
                new HarmonyMethod(self, nameof(MeshPre)), new HarmonyMethod(self, nameof(MeshPost)));
            harmony.Patch(AccessTools.Method(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.StartPath)),
                new HarmonyMethod(self, nameof(CountPath)));
            harmony.Patch(AccessTools.Method(typeof(MapDrawer), nameof(MapDrawer.MapMeshDirty),
                new[] { typeof(IntVec3), typeof(ulong), typeof(bool), typeof(bool) }), new HarmonyMethod(self, nameof(CountDirty)));
            return harmony;
        }

        // The same scenario was run against the old 8-cell well (main 1051f6d) for the before numbers.
        private static IEnumerable<int> Performance(RimArtTestContext t)
        {
            var component = Setup(t);
            IntVec3 well = t.center;
            Pawn caster = Caster(t, well + new IntVec3(-11, 0, 0));
            ThingDef[] stock = { ThingDefOf.Steel, ThingDefOf.WoodLog, ThingDefOf.Silver, ThingDefOf.ComponentIndustrial, ThingDefOf.MedicineHerbal, ThingDefOf.Plasteel };
            int stacks = 0;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(well, 7f, true))
            {
                if (cell.DistanceTo(well) < 2f || (cell.x + cell.z) % 2 != 0) continue;
                Thing item = ThingMaker.MakeThing(stock[stacks % stock.Length]);
                item.stackCount = item.def.stackLimit;
                GenSpawn.Spawn(item, cell, t.map);
                stacks++;
            }
            IntVec3 away = (well + new IntVec3(0, 0, 40)).ClampInsideMap(t.map);
            for (int i = 0; i < 20; i++)
            {
                float angle = i * Mathf.PI * 2f / 20f, radius = 1.5f + (i % 4);
                IntVec3 at = (well.ToVector3Shifted() + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius).ToIntVec3();
                Pawn pawn = t.Enemy(at, armed: false);
                pawn.apparel?.DestroyAll();
                if (i % 2 == 0) pawn.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto, away), JobCondition.InterruptForced);
            }
            t.Log($"{stacks} item stacks within 2-7 cells, 20 unarmed enemies at 1.5-4.5 cells, 10 walking away");
            yield return 30;
            var harmony = Hooks();
            try
            {
                ResetTimes();
                yield return 60;
                Report(t, "no well, 60 ticks");
                GravityCast cast = Open(component, caster, well);
                if (!t.Check(cast != null, "the well opened")) yield break;
                yield return GravityRules.OpeningTicks;
                ResetTimes();
                yield return 60;
                Report(t, "field ticks 1-60");
                yield return 180;
                Report(t, "field ticks 61-240");
                yield return 120;
                Report(t, "field ticks 241-360");
                t.Log(State(cast) + $"; {t.map.listerThings.AllThings.Count(x => x.def.category == ThingCategory.Item && x.Position.DistanceTo(well) < 12f)} stacks left");
                if (cast.Active) cast.Finish(true);
                yield return 60;
                Report(t, "after the well, 60 ticks");
            }
            finally { harmony.UnpatchAll("RimArt.GravityPerf"); }
        }

        [RimArtTest("Gravity Well", "9a performance: old geometry (fixed 8 cells, 6 s, no drift), 20 pawns and a stockpile", 1800)]
        private static IEnumerable<int> PerformanceFixed(RimArtTestContext t)
        {
            Override("minRadius", 8f);
            Override("maxRadius", 8f);
            Override("baseSeconds", 6f);
            Override("secondsPerEaten", 0f);
            Override("driftSpeed", 0f);
            try { foreach (int step in Performance(t)) yield return step; }
            finally { Restore(); }
        }

        [RimArtTest("Gravity Well", "9c performance: old geometry and nothing eaten (core -0.01), 20 pawns and a stockpile", 1800)]
        private static IEnumerable<int> PerformanceNothingEaten(RimArtTestContext t)
        {
            // The old well kept its stockpile; with no core nothing is eaten, so the load stays for the 6 s.
            Override("minRadius", 8f);
            Override("maxRadius", 8f);
            Override("baseSeconds", 6f);
            Override("secondsPerEaten", 0f);
            Override("driftSpeed", 0f);
            Override("coreRadius", -0.01f);
            try { foreach (int step in Performance(t)) yield return step; }
            finally { Restore(); }
        }

        [RimArtTest("Gravity Well", "9b performance: Pain's defaults, 20 pawns and a stockpile", 1800)]
        private static IEnumerable<int> PerformanceDefault(RimArtTestContext t) => Performance(t);
    }
}
