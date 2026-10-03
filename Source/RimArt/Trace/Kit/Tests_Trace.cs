using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using static RimArt.RimArtTestContext;

namespace RimArt
{
    /// <summary>
    /// Game tests for Shirou's Reinforcement and Trace On (run with -quicktest -rimarttest=trace): the library a study
    /// fills, a copy traced into an empty hand, over a real weapon and over another copy, a copy breaking when it
    /// leaves the hand or on revert, and Reinforcement's buff and slash. The screenshots are close-ups of the pictures
    /// on a real pawn in four facings. Unlimited Blade Works has its own tests (-rimarttest=ubw).
    /// </summary>
    public static class Tests_Trace
    {
        private static EchoDef Shirou => DefDatabase<EchoDef>.GetNamed("AG_Echo_Shirou");
        private static ThingDef LongSword => DefDatabase<ThingDef>.GetNamed("MeleeWeapon_LongSword");
        private static ThingDef Spear => DefDatabase<ThingDef>.GetNamed("MeleeWeapon_Spear");
        private static ThingDef Knife => DefDatabase<ThingDef>.GetNamed("MeleeWeapon_Knife");
        private static ThingDef Gladius => DefDatabase<ThingDef>.GetNamed("MeleeWeapon_Gladius");

        private static GameComponent_Echoes Setup(RimArtTestContext t)
        {
            GameComponent_Trace.Instance.ResetForTests();
            return t.ClearEchoes();
        }

        /// <summary>Shirou's Host, manifested with a full pool, empty-handed, drafted with fire at will off.</summary>
        private static Pawn Host(RimArtTestContext t, IntVec3 at, out EchoRecord record)
        {
            Pawn host = t.Host(Shirou, at, out record);
            if (host.equipment.Primary != null) host.equipment.DestroyEquipment(host.equipment.Primary);
            host.drafter.Drafted = true;
            host.drafter.FireAtWill = false;
            NoWimp(host);
            return t.Note(host);
        }

        /// <summary>Moving level and every hediff with its part, to see where a test pawn lost speed.</summary>
        private static string Health(Pawn pawn) =>
            "moving " + pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving).ToString("0.00") + ", hurt " + (pawn.health.summaryHealth.SummaryHealthPercent < 0.999f) + "; " +
            string.Join(", ", pawn.health.hediffSet.hediffs.Select(h => h.LabelCap + (h.Part != null ? " (" + h.Part.Label + ")" : "")));

        private static TraceLibraryEntry Study(Pawn pawn, ThingDef def, ThingDef stuff, QualityCategory best)
        {
            var entry = new TraceLibraryEntry { blade = def.defName, stuff = stuff?.defName, best = best };
            TraceLibrary.Of(pawn).Add(entry);
            return entry;
        }

        private static void TraceOn(Pawn host, TraceLibraryEntry entry)
        {
            Ability ability = host.abilities.GetAbility(TraceDefOf.AG_Trace_On);
            ability.CompOfType<CompAbilityEffect_TraceOn>().chosen = entry;
            ability.QueueCastingJob(host, LocalTargetInfo.Invalid);
        }

        private static ThingWithComps Copy(Pawn host) => GameComponent_Trace.Instance.HeldCopy(host);

        private static Thing Made(ThingDef def, ThingDef stuff, QualityCategory quality)
        {
            Thing thing = ThingMaker.MakeThing(def, stuff);
            thing.TryGetComp<CompQuality>()?.SetQuality(quality, null);
            return thing;
        }

        private static string Hands(Pawn pawn)
        {
            ThingWithComps held = pawn.equipment.Primary;
            string inventory = string.Join(", ", pawn.inventory.innerContainer.Select(x => x.LabelCap.ToString()));
            return "hand: " + (held == null ? "empty" : held.LabelCap + (TraceCopies.IsCopy(held) ? " (copy)" : "")) + "; inventory: " + (inventory.Length == 0 ? "empty" : inventory);
        }

        private static int OnGround(RimArtTestContext t, ThingDef def, IntVec3 near) =>
            t.map.listerThings.ThingsOfDef(def).Count(x => x.Spawned && x.Position.InHorDistOf(near, 6f));

        // ---- the library ---------------------------------------------------------------------------------------

        [RimArtTest("Trace", "library 1 a study keeps weapon, material and best quality; a copy is one quality below; after awakening a study only adds to the library")]
        private static IEnumerable<int> Library(RimArtTestContext t)
        {
            Setup(t);
            yield return 5;
            var library = new List<TraceLibraryEntry>();
            Thing steelNormal = Made(LongSword, ThingDefOf.Steel, QualityCategory.Normal), steelGood = Made(LongSword, ThingDefOf.Steel, QualityCategory.Good);
            Thing plasteelPoor = Made(LongSword, ThingDefOf.Plasteel, QualityCategory.Poor);
            t.Check(TraceLibrary.Learn(library, steelNormal) != null && library.Count == 1, "a steel longsword is learned");
            t.Check(TraceLibrary.Learn(library, steelGood) != null && library.Count == 1 && library[0].best == QualityCategory.Good, "a better one raises the best quality (" + library[0].best + ")");
            t.Check(!TraceLibrary.Adds(library, steelNormal), "a worse one adds nothing");
            t.Check(TraceLibrary.Learn(library, plasteelPoor) != null && library.Count == 2, "another material is its own entry");
            foreach (TraceLibraryEntry e in library) t.Log(TraceLibrary.Label(e, 1) + " (best " + e.best + ")");
            t.Check(TraceLibrary.CopyQuality(library[0], 1) == QualityCategory.Normal, "good studied gives a normal copy");
            t.Check(TraceLibrary.CopyQuality(new TraceLibraryEntry { blade = "x", best = QualityCategory.Awful }, 1) == QualityCategory.Awful, "never under awful");
            ThingWithComps copy = TraceLibrary.MakeCopy(library[1], 1);
            t.Check(copy.def == LongSword && copy.Stuff == ThingDefOf.Plasteel && copy.TryGetQuality(out QualityCategory q) && q == QualityCategory.Awful,
                "the plasteel copy is an awful plasteel longsword (" + copy.LabelCap + ")");
            t.Check(TraceLibrary.Refusal(new TraceLibraryEntry { blade = "AG_NoSuchBlade" }) != null, "a weapon no longer in the game is refused");
            foreach (string special in new[] { "AG_Samehada", "AG_ChainSickle", "AG_FumaShuriken", "AG_Yamato" })
                t.Check(!TraceLibrary.CanTrace(new TraceLibraryEntry { blade = special }), special + " cannot be traced ("
                    + TraceLibrary.Refusal(new TraceLibraryEntry { blade = special }) + ")");
            foreach (ThingDef vanilla in new[] { Knife, LongSword, Spear, Gladius })
                t.Check(TraceLibrary.CanTrace(new TraceLibraryEntry { blade = vanilla.defName }), vanilla.defName + " can be traced");

            Pawn pawn = t.Colonist(t.center);
            pawn.story.traits.GainTrait(new Trait(OriginBladeDefOf.AG_OriginBlade));
            BladeStudyRecord record = Current.Game.GetComponent<GameComponent_BladeStudy>().RecordFor(pawn);
            int types = record.bladeTypes.Count;
            if (!OriginBladeUtility.CanStudy(pawn))
            {
                t.Log("this colonist cannot study (incapable of violence or crafting); the awakened study step is skipped");
                yield break;
            }
            Thing spear = Made(Spear, ThingDefOf.Plasteel, QualityCategory.Excellent);
            t.Check(OriginBladeUtility.StudyAdds(pawn, spear), "after awakening a new blade can be studied");
            Current.Game.GetComponent<GameComponent_BladeStudy>().CompleteStudy(pawn, spear);
            t.Check(record.library.Any(e => e.blade == Spear.defName && e.stuff == ThingDefOf.Plasteel.defName && e.best == QualityCategory.Excellent),
                "the study went to the library");
            t.Check(record.bladeTypes.Count == types, "the awakening's blade types are unchanged (" + record.bladeTypes.Count + ")");
            t.Check(!OriginBladeUtility.StudyAdds(pawn, spear), "the same blade again adds nothing");
        }

        // ---- Trace On ------------------------------------------------------------------------------------------

        [RimArtTest("Trace", "trace on 1 an empty hand gets a plasteel longsword one quality below the best studied; pays 10, worth 0 (screenshots)")]
        private static IEnumerable<int> TraceEmpty(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            TraceLibraryEntry entry = Study(host, LongSword, ThingDefOf.Plasteel, QualityCategory.Good);
            Ability ability = host.abilities.GetAbility(TraceDefOf.AG_Trace_On);
            if (!t.Check(ability != null && ability.CanCast, "Shirou has Trace On (" + ability?.CanCast.Reason + ")")) { EndHost(record); yield break; }
            float before = echoes.charge;
            int start = t.Now;
            TraceOn(host, entry);
            t.Check(host.CurJobDef == TraceDefOf.AG_CastTrace, "the trace cast job started (" + host.CurJobDef?.defName + ")");
            yield return 20;
            t.Log("at " + (t.Now - start) + " ticks: " + Hands(host) + "; " + Describe(host));
            yield return t.ShotAs("trace-on-mid", host.Position, 2.5f);
            foreach (int w in WaitFor(() => Copy(host) != null, 120)) yield return w;
            t.Log("copy at " + (t.Now - start) + " ticks: " + Hands(host));
            ThingWithComps copy = Copy(host);
            if (!t.Check(copy != null, "a copy is in the hand")) { EndHost(record); yield break; }
            t.Check(copy.def == LongSword && copy.Stuff == ThingDefOf.Plasteel, "a plasteel longsword (" + copy.LabelCap + ")");
            t.Check(copy.TryGetQuality(out QualityCategory q) && q == QualityCategory.Normal, "normal, one below the good studied (" + q + ")");
            t.Check(copy.MarketValue == 0f, "worth nothing (" + copy.MarketValue + ")");
            float spent = before - echoes.charge;
            t.Check(spent >= 10f && spent < 11f, "the pool paid 10 (" + spent.ToString("0.##") + " with upkeep)");
            t.Check(ability.CooldownTicksRemaining > 0, "the cooldown runs (" + ability.CooldownTicksRemaining + ")");
            yield return 6;
            yield return t.ShotAs("trace-on-lit", host.Position, 2.5f);
            EndHost(record);
        }

        [RimArtTest("Trace", "trace on 2 a real weapon goes to the inventory first; a held copy breaks for the next one")]
        private static IEnumerable<int> TraceOverHeld(RimArtTestContext t)
        {
            Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            t.Equip(host, Knife);
            ThingWithComps knife = host.equipment.Primary;
            TraceLibraryEntry sword = Study(host, LongSword, ThingDefOf.Steel, QualityCategory.Normal);
            TraceLibraryEntry spear = Study(host, Spear, ThingDefOf.Steel, QualityCategory.Masterwork);
            int start = t.Now;
            TraceOn(host, sword);
            yield return 5;
            t.Log("at " + (t.Now - start) + " ticks: " + Hands(host) + "; hidden while stowed: " + (GameComponent_Trace.Instance.Stowing(host) == knife));
            foreach (int w in WaitFor(() => Copy(host) != null, 150)) yield return w;
            t.Log("copy at " + (t.Now - start) + " ticks: " + Hands(host));
            ThingWithComps first = Copy(host);
            t.Check(first?.def == LongSword, "a longsword copy is in the hand");
            t.Check(host.inventory.innerContainer.Contains(knife), "the knife went to the inventory");
            t.Check(OnGround(t, Knife, host.Position) == 0, "no knife on the ground");

            host.abilities.GetAbility(TraceDefOf.AG_Trace_On).ResetCooldown();
            yield return 20;
            start = t.Now;
            TraceOn(host, spear);
            yield return 2;
            t.Check(first == null || first.Destroyed, "the held copy broke when the next cast began");
            foreach (int w in WaitFor(() => Copy(host) != null, 150)) yield return w;
            t.Log("second copy at " + (t.Now - start) + " ticks: " + Hands(host));
            ThingWithComps second = Copy(host);
            t.Check(second?.def == Spear && second.TryGetQuality(out QualityCategory q) && q == QualityCategory.Excellent, "an excellent spear copy (" + second?.LabelCap + ")");
            t.Check(OnGround(t, LongSword, host.Position) == 0, "no longsword left on the ground");
            t.Check(host.inventory.innerContainer.Contains(knife), "the knife is still in the inventory");
            EndHost(record);
        }

        [RimArtTest("Trace", "copy 1 a copy breaks when dropped, when another weapon is equipped, when Shirou is downed, and on revert; nothing lands")]
        private static IEnumerable<int> CopyBreaks(RimArtTestContext t)
        {
            Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            TraceLibraryEntry sword = Study(host, LongSword, ThingDefOf.Steel, QualityCategory.Normal);
            Ability ability = host.abilities.GetAbility(TraceDefOf.AG_Trace_On);
            IEnumerable<int> Traced()
            {
                ability.ResetCooldown();
                TraceOn(host, sword);
                foreach (int w in WaitFor(() => Copy(host) != null, 150)) yield return w;
                yield return 20;
            }

            foreach (int w in Traced()) yield return w;
            ThingWithComps copy = Copy(host);
            bool dropped = host.equipment.TryDropEquipment(copy, out ThingWithComps landed, host.Position);
            t.Check(dropped && landed == null && copy.Destroyed, "dropped: the copy broke (" + Hands(host) + ")");
            yield return 30;
            t.Check(OnGround(t, LongSword, host.Position) == 0, "nothing on the ground");

            foreach (int w in Traced()) yield return w;
            copy = Copy(host);
            var gladius = (ThingWithComps)ThingMaker.MakeThing(Gladius, ThingDefOf.Steel);
            host.equipment.MakeRoomFor(gladius);
            host.equipment.AddEquipment(gladius);
            t.Check(copy.Destroyed && host.equipment.Primary == gladius, "a real gladius equipped: the copy broke (" + Hands(host) + ")");
            host.equipment.DestroyEquipment(gladius);

            foreach (int w in Traced()) yield return w;
            copy = Copy(host);
            EchoUtility.Revert(record, collapse: false);
            foreach (int w in WaitFor(() => copy.Destroyed, TraceCopies.CheckTicks + 5)) yield return w;
            t.Check(copy.Destroyed, "reverted: the copy broke within " + TraceCopies.CheckTicks + " ticks (" + Hands(host) + ")");

            GameComponent_Echoes.Get.charge = 100f;
            t.Check(EchoUtility.Manifest(record), "manifested again");
            host.drafter.Drafted = true;
            ability = host.abilities.GetAbility(TraceDefOf.AG_Trace_On);
            foreach (int w in Traced()) yield return w;
            copy = Copy(host);
            HealthUtility.DamageUntilDowned(host, allowBleedingWounds: false);
            yield return 30;
            t.Log(Describe(host) + "; " + Hands(host));
            t.Check(!host.Downed || copy.Destroyed, "downed: the copy broke");
            t.Check(OnGround(t, LongSword, host.Position) == 0, "no longsword on the ground");
            EndHost(record);
        }

        // ---- Reinforcement -------------------------------------------------------------------------------------

        [RimArtTest("Trace", "reinforcement 1 cast 0.5 s: move speed x1.3 and melee damage x1.4 for 20 s, no charge; a hit gets the slash (screenshots)", 3600)]
        private static IEnumerable<int> Reinforcement(RimArtTestContext t)
        {
            GameComponent_Echoes echoes = Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            t.Equip(host, LongSword);
            float speed = host.GetStatValue(StatDefOf.MoveSpeed), melee = host.GetStatValue(StatDefOf.MeleeDamageFactor);
            var hediffsAtStart = new HashSet<Hediff>(host.health.hediffSet.hediffs);
            Ability ability = host.abilities.GetAbility(TraceDefOf.AG_Trace_Reinforcement);
            if (!t.Check(ability != null && ability.CanCast, "Shirou has Reinforcement (" + ability?.CanCast.Reason + ")")) { EndHost(record); yield break; }
            float before = echoes.charge;
            int start = t.Now;
            ability.QueueCastingJob(host, LocalTargetInfo.Invalid);
            yield return 18;
            yield return t.ShotAs("reinforce-cast", host.Position, 2.5f);
            foreach (int w in WaitFor(() => host.health.hediffSet.HasHediff(TraceDefOf.AG_TraceReinforced), 90)) yield return w;
            int landed = t.Now;
            t.Log("buff at " + (landed - start) + " ticks; " + Describe(host));
            if (!t.Check(host.health.hediffSet.HasHediff(TraceDefOf.AG_TraceReinforced), "the buff landed")) { EndHost(record); yield break; }
            float speedNow = host.GetStatValue(StatDefOf.MoveSpeed), meleeNow = host.GetStatValue(StatDefOf.MeleeDamageFactor);
            t.Check(System.Math.Abs(speedNow / speed - 1.3f) < 0.01f, "move speed x1.3 (" + speed.ToString("0.00") + " -> " + speedNow.ToString("0.00") + ")");
            t.Check(System.Math.Abs(meleeNow / melee - 1.4f) < 0.01f, "melee damage x1.4 (" + melee.ToString("0.00") + " -> " + meleeNow.ToString("0.00") + ")");
            t.Check(before - echoes.charge < 1f, "no charge beyond upkeep (" + (before - echoes.charge).ToString("0.##") + ")");
            t.Check(ability.CooldownTicksRemaining > 2500, "the 45 s cooldown runs (" + ability.CooldownTicksRemaining + ")");

            host.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, host.Position + new IntVec3(6, 0, 0)), JobTag.Misc);
            yield return 25;
            yield return t.ShotAs("reinforce-run", host.Position, 3f);
            foreach (int w in WaitFor(() => !host.pather.MovingNow, 120, 5)) yield return w;
            t.Log("after the run: " + Health(host));

            // A sure hit through the verb: an AttackMelee job lets Melee Animation start a duel, whose damage skips the
            // slash hook, and the quicktest map is cold enough for hypothermia to slow the host within 20 s.
            Pawn foe = t.Note(t.Target(host.Position + new IntVec3(1, 0, 0), 900));
            yield return 2;
            for (int attempt = 0; attempt < 3 && GameComponent_Trace.Instance.HitsBy(host) == 0; attempt++)
            {
                t.Strike(host, foe);
                foreach (int w in WaitFor(() => GameComponent_Trace.Instance.HitsBy(host) > 0, 60, 2)) yield return w;
            }
            t.Log("hits with the slash: " + GameComponent_Trace.Instance.HitsBy(host) + "; foe " + Describe(foe));
            t.Check(GameComponent_Trace.Instance.HitsBy(host) > 0, "a melee hit while reinforced got the slash");
            t.Log("after the hit: " + Health(host));
            yield return 4;
            yield return t.ShotAs("reinforce-hit", host.Position, 3f);
            foe.Destroy();

            foreach (int w in WaitFor(() => !host.health.hediffSet.HasHediff(TraceDefOf.AG_TraceReinforced), 1400, 10)) yield return w;
            t.Log("buff gone after " + (t.Now - landed) + " ticks");
            t.Check(!host.health.hediffSet.HasHediff(TraceDefOf.AG_TraceReinforced) && t.Now - landed >= 1190, "the buff lasted 20 s");
            List<Hediff> added = host.health.hediffSet.hediffs.Where(h => !hediffsAtStart.Contains(h)).ToList();
            if (added.Count > 0)
            {
                t.Log("removed before the measure (the map's cold, the foe's hits): " + string.Join(", ", added.Select(h => h.LabelCap)));
                foreach (Hediff h in added) host.health.RemoveHediff(h);
            }
            float speedAfter = host.GetStatValue(StatDefOf.MoveSpeed);
            if (!t.Check(System.Math.Abs(speedAfter - speed) < 0.01f, "move speed is back (" + speed.ToString("0.00") + " -> " + speedAfter.ToString("0.00") + ")"))
                t.Log(Describe(host) + "; " + Health(host) + "\n" + StatDefOf.MoveSpeed.Worker.GetExplanationFull(StatRequest.For(host), ToStringNumberSense.Absolute, speedAfter));
            EndHost(record);
        }

        // ---- pictures on a real pawn ---------------------------------------------------------------------------

        [RimArtTest("Trace", "shots 1 Trace On and Reinforcement mid-cast in four facings (screenshots only)")]
        private static IEnumerable<int> Shots(RimArtTestContext t)
        {
            Setup(t);
            yield return 5;
            Pawn host = Host(t, t.center, out EchoRecord record);
            TraceLibraryEntry sword = Study(host, LongSword, ThingDefOf.Steel, QualityCategory.Good);
            Ability traceOn = host.abilities.GetAbility(TraceDefOf.AG_Trace_On), reinforce = host.abilities.GetAbility(TraceDefOf.AG_Trace_Reinforcement);
            foreach (Rot4 rot in new[] { Rot4.South, Rot4.East, Rot4.North, Rot4.West })
            {
                string side = rot.ToStringHuman().ToLower();
                GameComponent_Trace.Instance.ResetForTests();
                if (host.equipment.Primary != null) host.equipment.DestroyEquipment(host.equipment.Primary);
                Face(host, rot);
                yield return 3;
                traceOn.ResetCooldown();
                TraceOn(host, sword);
                yield return 22;
                yield return t.ShotAs("trace-on-" + side, host.Position, 2.5f);
                foreach (int w in WaitFor(() => Copy(host) != null && host.CurJobDef != TraceDefOf.AG_CastTrace, 120)) yield return w;
                Face(host, rot);
                yield return 3;
                reinforce.ResetCooldown();
                reinforce.QueueCastingJob(host, LocalTargetInfo.Invalid);
                yield return 18;
                yield return t.ShotAs("reinforce-" + side, host.Position, 2.5f);
                foreach (int w in WaitFor(() => host.CurJobDef != TraceDefOf.AG_CastTrace, 90)) yield return w;
                Hediff buff = host.health.hediffSet.GetFirstHediffOfDef(TraceDefOf.AG_TraceReinforced);
                if (buff != null) host.health.RemoveHediff(buff);
            }
            EndHost(record);
        }
    }
}
