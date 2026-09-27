using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimArt
{
    /// <summary>Game tests for Inumaki's words (run with -quicktest -rimarttest=inumaki).</summary>
    public static class Tests_Larynx
    {
        private static EchoDef Inumaki => DefDatabase<EchoDef>.GetNamed("AG_Echo_Inumaki");
        private static AbilityDef Word(string name) => DefDatabase<AbilityDef>.GetNamed("AG_Imperative_" + name);

        /// <summary>
        /// A cleared arena with a manifested Inumaki at the centre. Drafted, so he does not flee from
        /// an armed enemy out of the word's reach (an undrafted colonist does).
        /// </summary>
        private static Pawn Setup(RimArtTestContext t, WordVolume volume = WordVolume.Speak)
        {
            t.Clear();
            GameComponent_Echoes echoes = GameComponent_Echoes.Get;
            echoes.ResetForTests();
            EchoDevice.workingForTests = false;
            Pawn inumaki = t.Colonist(t.center);
            EchoRecord record = EchoUtility.ForceHost(Inumaki, inumaki);
            echoes.charge = 100f;
            EchoUtility.Manifest(record);
            if (volume != WordVolume.Speak) SetVolume(inumaki, volume);
            return inumaki;
        }

        private static void TearDown() => EchoDevice.workingForTests = null;

        private static void SetVolume(Pawn inumaki, WordVolume volume)
        {
            foreach (Hediff hediff in inumaki.health.hediffSet.hediffs)
            {
                HediffComp_WordVolume comp = hediff.TryGetComp<HediffComp_WordVolume>();
                if (comp != null) comp.volume = volume;
            }
        }

        private static Ability AbilityOf(Pawn inumaki, string word) => inumaki.abilities.GetAbility(Word(word));

        private static CompAbilityEffect_Imperative CompOf(Pawn inumaki, string word) =>
            AbilityOf(inumaki, word).EffectComps.OfType<CompAbilityEffect_Imperative>().First();

        /// <summary>Says the word at once, skipping the warm-up.</summary>
        private static void Say(Pawn inumaki, string word) =>
            AbilityOf(inumaki, word).Activate(new LocalTargetInfo(inumaki), LocalTargetInfo.Invalid);

        private static Pawn Enemy(RimArtTestContext t, int dx, int dz, bool armed = false)
        {
            Pawn pawn = t.Enemy(t.center + new IntVec3(dx, 0, dz), armed);
            // Armour is not under test; random apparel could deflect a hit.
            pawn.apparel?.DestroyAll();
            return pawn;
        }

        private static Pawn Colonist(RimArtTestContext t, int dx, int dz)
        {
            Pawn pawn = t.Colonist(t.center + new IntVec3(dx, 0, dz));
            pawn.drafter.Drafted = false;
            RimArtTestContext.Hold(pawn);
            return pawn;
        }

        private static void MakeDeaf(Pawn pawn)
        {
            foreach (BodyPartRecord ear in pawn.health.hediffSet.GetNotMissingParts().Where(p => p.def.defName == "Ear").ToList())
                pawn.health.AddHediff(HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, pawn, ear), ear);
        }

        /// <summary>
        /// Stands the pawn still on a job that is not Wait: to Stop, a pawn on Wait is already doing
        /// what the word says and costs x0.15, which would hide the hostile factor.
        /// </summary>
        private static void Stand(Pawn pawn) =>
            pawn.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture, 6000), JobCondition.InterruptForced);

        private static int Hurts(Pawn pawn) =>
            pawn.health.hediffSet.hediffs.Count(h => h is Hediff_Injury || h is Hediff_MissingPart);

        private static string Injuries(Pawn pawn) => string.Join(", ", pawn.health.hediffSet.hediffs
            .Where(h => h is Hediff_Injury || h is Hediff_MissingPart)
            .Select(h => h.LabelCap + " (" + h.Part?.Label + ", " + (h.sourceDef?.defName ?? h.sourceLabel ?? "?") + ")"));

        private static bool HeardWord(Pawn pawn, int holdTicks) =>
            pawn.CurJobDef == JobDefOf.Wait && pawn.CurJob.expiryInterval == holdTicks;

        [RimArtTest("Inumaki", "sound 1 walls stop a word, an open door passes it for 3 reach, a closed door stops it")]
        private static IEnumerable<int> WallsAndDoors(RimArtTestContext t)
        {
            t.Clear();
            IntVec3 c = t.center;
            Building_Door door = null;
            for (int dz = -6; dz <= 6; dz++)
            {
                IntVec3 cell = c + new IntVec3(3, 0, dz);
                if (dz == 0)
                    door = (Building_Door)GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Door, ThingDefOf.WoodLog), cell, t.map);
                else
                    GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog), cell, t.map);
            }
            yield return 1;

            IntVec3 sameSide = c + new IntVec3(2, 0, 4);
            IntVec3 pastDoor = c + new IntVec3(4, 0, 0);
            IntVec3 behindWall = c + new IntVec3(4, 0, 4);

            Dictionary<IntVec3, float> closed = SoundSpread.Reach(t.map, c, 8f, 3f);
            t.Check(closed.ContainsKey(sameSide), "a cell on his side 4.8 away is reached");
            t.Check(!closed.ContainsKey(pastDoor), "the closed door stops it");
            t.Check(!closed.ContainsKey(behindWall), "the wall stops it");

            AccessTools.Field(typeof(Building_Door), "openInt").SetValue(door, true);
            t.Check(door.Open, "the door is open");
            Dictionary<IntVec3, float> open = SoundSpread.Reach(t.map, c, 8f, 3f);
            t.Check(open.TryGetValue(pastDoor, out float d) && System.Math.Abs(d - 7f) < 0.01f,
                "past the open door at 4 straight cells + 3 doorway = 7 (got " + d + ")");
            t.Check(!open.ContainsKey(behindWall), "the cell behind the wall 4 along it is still out of reach (10.1 by the door)");
            Dictionary<IntVec3, float> whisper = SoundSpread.Reach(t.map, c, 3f, 3f);
            t.Check(!whisper.ContainsKey(c + new IntVec3(3, 0, 0)), "a whisper (3) does not get into the doorway (3 + 3)");
        }

        [RimArtTest("Inumaki", "stop 1 speak reaches 8: an enemy at 5 and a colonist at 6 stop, an enemy at 10 does not; throat sums")]
        private static IEnumerable<int> StopAtSpeak(RimArtTestContext t)
        {
            Pawn inumaki = Setup(t);
            Pawn near = Enemy(t, 5, 0);
            Pawn friend = Colonist(t, 0, 6);
            Pawn far = Enemy(t, -10, 0);
            Stand(near);
            Stand(friend);
            Stand(far);
            yield return 2;

            List<WordListener> listeners = LarynxUtility.Listeners(inumaki, ImperativeWord.Stop, WordVolume.Speak);
            t.Log("listeners: " + string.Join(", ", listeners.Select(l => l.pawn.LabelShort + " x" + l.factor.ToString("0.###"))));
            float expected = LarynxUtility.WearFor(listeners, 0.03f, WordVolume.Speak);
            Say(inumaki, "Stop");
            yield return 1;
            t.Log(RimArtTestContext.Describe(near));
            t.Log(RimArtTestContext.Describe(friend));
            t.Log(RimArtTestContext.Describe(far));
            t.Check(HeardWord(near, 180), "the enemy at 5 stopped");
            t.Check(HeardWord(friend, 180), "the colonist at 6 stopped");
            t.Check(!HeardWord(far, 180), "the enemy at 10 did not hear it");
            float wear = LarynxUtility.CurrentWear(inumaki);
            t.Log("throat " + wear.ToString("0.####") + ", expected " + expected.ToString("0.####") + " (0.03 x (hostile 2 + colonist 1) = 0.09 at full consciousness)");
            t.Check(System.Math.Abs(wear - expected) < 0.0001f && System.Math.Abs(wear - 0.09f) < 0.02f, "throat = 0.03 x the listeners' factors");
            TearDown();
        }

        [RimArtTest("Inumaki", "volume 1 whisper 3 cells at x0.5, shout 16 cells at x2")]
        private static IEnumerable<int> Volumes(RimArtTestContext t)
        {
            Pawn inumaki = Setup(t, WordVolume.Whisper);
            Pawn five = Enemy(t, 5, 0);
            Enemy(t, -11, 0);
            yield return 2;

            t.Check(LarynxUtility.Listeners(inumaki, ImperativeWord.Stop, WordVolume.Whisper).Count == 0, "a whisper reaches no one at 5");
            t.Check(CompOf(inumaki, "Stop").GizmoDisabled(out string reason), "stop is disabled when no one would hear (" + reason + ")");

            List<WordListener> speak = LarynxUtility.Listeners(inumaki, ImperativeWord.Stop, WordVolume.Speak);
            List<WordListener> shout = LarynxUtility.Listeners(inumaki, ImperativeWord.Stop, WordVolume.Shout);
            t.Check(speak.Count == 1 && speak[0].pawn == five, "speak reaches the one at 5 only");
            t.Check(shout.Count == 2, "shout reaches both (5 and 11)");
            float oneAtSpeak = LarynxUtility.WearFor(speak, 0.03f, WordVolume.Speak);
            float oneAtShout = LarynxUtility.WearFor(shout.Where(l => l.pawn == five).ToList(), 0.03f, WordVolume.Shout);
            t.Log("one hostile: speak " + oneAtSpeak.ToString("0.####") + ", shout " + oneAtShout.ToString("0.####"));
            t.Check(System.Math.Abs(oneAtShout - 2f * oneAtSpeak) < 0.0001f, "shout costs x2 of speak");

            SetVolume(inumaki, WordVolume.Shout);
            t.Check(LarynxUtility.VolumeOf(inumaki) == WordVolume.Shout, "the hero form reports shout");
            t.Check(!CompOf(inumaki, "Stop").GizmoDisabled(out _), "stop is enabled at shout");
            TearDown();
        }

        [RimArtTest("Inumaki", "crush 1 hurts and stuns every listener, colonists too")]
        private static IEnumerable<int> Crush(RimArtTestContext t)
        {
            Pawn inumaki = Setup(t);
            Pawn enemy = Enemy(t, 3, 0);
            Pawn friend = Colonist(t, 0, 3);
            yield return 2;
            int enemyBefore = Hurts(enemy), friendBefore = Hurts(friend), selfBefore = Hurts(inumaki);
            Say(inumaki, "Crush");
            yield return 1;
            t.Log(RimArtTestContext.Describe(enemy));
            t.Log(RimArtTestContext.Describe(friend));
            t.Check(Hurts(enemy) > enemyBefore, "the enemy is hurt");
            t.Check(Hurts(friend) > friendBefore, "the colonist is hurt");
            t.Check(enemy.stances.stunner.Stunned, "the enemy is stunned");
            t.Check(friend.stances.stunner.Stunned, "the colonist is stunned");
            t.Check(Hurts(inumaki) == selfBefore, "Inumaki is not hurt");
            TearDown();
        }

        [RimArtTest("Inumaki", "explode 1 bursts on the listener, hits a deaf pawn beside it, not Inumaki; mechs don't hear")]
        private static IEnumerable<int> Explode(RimArtTestContext t)
        {
            Pawn inumaki = Setup(t);
            Pawn enemy = Enemy(t, 5, 0);
            Pawn deaf = Colonist(t, 6, 0);
            MakeDeaf(deaf);
            Pawn mech = null;
            PawnKindDef scyther = DefDatabase<PawnKindDef>.GetNamedSilentFail("Mech_Scyther");
            if (scyther != null)
            {
                mech = PawnGenerator.GeneratePawn(new PawnGenerationRequest(scyther, Faction.OfMechanoids));
                GenSpawn.Spawn(mech, t.center + new IntVec3(-4, 0, 0), t.map);
                RimArtTestContext.Hold(mech);
            }
            yield return 2;

            t.Log("deaf pawn's Hearing: " + deaf.health.capacities.GetLevel(PawnCapacityDefOf.Hearing).ToString("0.##"));
            List<WordListener> listeners = LarynxUtility.Listeners(inumaki, ImperativeWord.Explode, WordVolume.Speak);
            t.Log("listeners: " + string.Join(", ", listeners.Select(l => l.pawn.LabelShort)));
            t.Check(listeners.Count == 1 && listeners[0].pawn == enemy, "only the enemy hears it");
            if (mech != null)
            {
                t.Check(!LarynxUtility.CanHear(mech), "the mechanoid cannot hear");
                // Gone before the word, so it cannot attack anyone while the burst is checked.
                mech.Destroy();
            }

            int enemyBefore = Hurts(enemy), deafBefore = Hurts(deaf), selfBefore = Hurts(inumaki);
            t.Log("inumaki before the word: " + Injuries(inumaki));
            Say(inumaki, "Explode");
            yield return 60;
            t.Log(RimArtTestContext.Describe(enemy));
            t.Log(RimArtTestContext.Describe(deaf));
            t.Check(enemy.Dead || Hurts(enemy) > enemyBefore, "the enemy took the burst");
            t.Check(deaf.Dead || Hurts(deaf) > deafBefore, "the deaf colonist beside it was hit too");
            t.Log("inumaki after: " + Injuries(inumaki));
            t.Check(!inumaki.Dead && Hurts(inumaki) == selfBefore, "Inumaki is not hurt (old scars are counted before)");
            float wear = LarynxUtility.CurrentWear(inumaki);
            t.Log("throat " + wear.ToString("0.###") + " (0.35 x hostile 2 = 0.70 at full consciousness)");
            t.Check(wear > 0.5f && wear < 0.75f, "throat about 70 %");
            t.Check(AbilityOf(inumaki, "Explode").CooldownTicksRemaining > 50000, "explode is on its 1-day cooldown");
            TearDown();
        }

        [RimArtTest("Inumaki", "throat 1 a word that would pass 100 % cannot be said")]
        private static IEnumerable<int> ThroatCap(RimArtTestContext t)
        {
            Pawn inumaki = Setup(t);
            Pawn friend = Colonist(t, 0, 4);
            yield return 2;
            LarynxUtility.ApplyWear(inumaki, 0.5f);
            t.Check(!CompOf(inumaki, "Explode").GizmoDisabled(out _), "50 % + one colonist (35 %) = 85 %: allowed");
            Pawn enemy = Enemy(t, 4, 0);
            yield return 2;
            bool disabled = CompOf(inumaki, "Explode").GizmoDisabled(out string reason);
            t.Log("with an enemy too: " + reason);
            t.Check(disabled, "50 % + colonist 35 % + enemy 70 % = 155 %: refused");
            t.Check(!CompOf(inumaki, "Stop").GizmoDisabled(out _), "stop (9 %) is still allowed");
            TearDown();
        }

        [RimArtTest("Inumaki", "words 1 drop disarms a listener; come and run move them")]
        private static IEnumerable<int> JobWords(RimArtTestContext t)
        {
            Pawn inumaki = Setup(t);
            Pawn armed = Enemy(t, 4, 0, armed: true);
            yield return 2;
            t.Log("weapon: " + (armed.equipment.Primary?.LabelShort ?? "none"));
            Say(inumaki, "Drop");
            yield return 90;
            t.Log(RimArtTestContext.Describe(armed));
            t.Check(armed.equipment.Primary == null, "the enemy dropped its weapon");

            RimArtTestContext.Hold(armed);
            t.Log("inumaki: " + RimArtTestContext.Describe(inumaki) + ", can speak " + LarynxUtility.CanSpeak(inumaki)
                + ", Talking " + inumaki.health.capacities.GetLevel(PawnCapacityDefOf.Talking).ToString("0.##")
                + ", throat " + LarynxUtility.CurrentWear(inumaki).ToString("0.###")
                + ", come listeners " + LarynxUtility.Listeners(inumaki, ImperativeWord.Come, WordVolume.Speak).Count
                + ", enemy hears " + LarynxUtility.CanHear(armed));
            Say(inumaki, "Come");
            t.Log("same tick: " + RimArtTestContext.Describe(armed));
            yield return 1;
            t.Log(RimArtTestContext.Describe(armed));
            t.Check(armed.CurJobDef == JobDefOf.Goto && armed.CurJob.targetA.Cell == inumaki.Position, "come: walking to Inumaki");

            RimArtTestContext.Hold(armed);
            Say(inumaki, "Run");
            yield return 1;
            t.Log(RimArtTestContext.Describe(armed));
            t.Check(armed.CurJobDef == JobDefOf.Flee, "run: fleeing");
            TearDown();
        }

        [RimArtTest("Inumaki", "syrup 1 drinking one herbal medicine takes 30 % off the throat")]
        private static IEnumerable<int> Syrup(RimArtTestContext t)
        {
            Pawn inumaki = Setup(t);
            LarynxUtility.ApplyWear(inumaki, 0.5f);
            Thing medicine = ThingMaker.MakeThing(ThingDefOf.MedicineHerbal);
            medicine.stackCount = 3;
            GenSpawn.Spawn(medicine, t.center + new IntVec3(2, 0, 0), t.map);
            yield return 1;
            Job job = JobMaker.MakeJob(LarynxDefOf.AG_DrinkThroatSyrup, medicine);
            job.count = 1;
            inumaki.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            yield return 250;
            float wear = LarynxUtility.CurrentWear(inumaki);
            t.Log("throat " + wear.ToString("0.###") + ", herbal medicine left " + medicine.stackCount);
            t.Check(System.Math.Abs(wear - 0.2f) < 0.01f, "throat 50 % -> 20 %");
            t.Check(medicine.stackCount == 2, "one herbal medicine used");
            TearDown();
        }

        [RimArtTest("Inumaki", "cast 1 the button's path: cast on himself, 0.4 s warm-up, then the word lands")]
        private static IEnumerable<int> CastPath(RimArtTestContext t)
        {
            Pawn inumaki = Setup(t);
            Pawn enemy = Enemy(t, 5, 0);
            Stand(enemy);
            yield return 2;
            Ability stop = AbilityOf(inumaki, "Stop");
            t.Check(!stop.GizmoDisabled(out string reason), "the button is enabled (" + (reason ?? "no reason") + ")");
            // What Command_Ability.ProcessInput does for an ability with targetRequired false.
            stop.QueueCastingJob(inumaki, LocalTargetInfo.Invalid);
            yield return 1;
            t.Log("after queueing: " + RimArtTestContext.Describe(inumaki));
            t.Check(inumaki.CurJob?.ability == stop, "Inumaki is casting stop");
            yield return 45;
            t.Log(RimArtTestContext.Describe(enemy));
            t.Check(HeardWord(enemy, 180), "the enemy stopped after the warm-up");
            t.Check(stop.CooldownTicksRemaining > 0, "stop is on cooldown");
            t.Check(LarynxUtility.CurrentWear(inumaki) > 0f, "the throat paid");
            TearDown();
        }

        /// <summary>
        /// Pictures only: a shout in a yard with a wall and an open door, screenshots of the wave at
        /// four moments. Nothing is checked beyond the word landing; read the PNGs.
        /// </summary>
        [RimArtTest("Inumaki", "vfx 1 a shout's wave stops at a wall and squeezes through an open door")]
        private static IEnumerable<int> WaveShots(RimArtTestContext t)
        {
            Pawn inumaki = Setup(t, WordVolume.Shout);
            IntVec3 c = t.center;
            Building_Door door = null;
            for (int dz = -9; dz <= 9; dz++)
            {
                IntVec3 cell = c + new IntVec3(5, 0, dz);
                if (dz == 1)
                    door = (Building_Door)GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Door, ThingDefOf.WoodLog), cell, t.map);
                else
                    GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog), cell, t.map);
            }
            AccessTools.Field(typeof(Building_Door), "openInt").SetValue(door, true);
            AccessTools.Field(typeof(Building_Door), "holdOpenInt").SetValue(door, true);
            Pawn[] listeners = { Enemy(t, 3, 3), Enemy(t, -6, -2), Enemy(t, 8, 1), Enemy(t, 8, 7), Colonist(t, -1, -6) };
            foreach (Pawn pawn in listeners) Stand(pawn);
            yield return 2;
            Say(inumaki, "Stop");
            yield return 3;
            yield return t.ShotAs("wave-0.05s");
            yield return 6;
            yield return t.ShotAs("wave-0.15s");
            yield return 8;
            yield return t.ShotAs("wave-0.28s");
            yield return 12;
            yield return t.ShotAs("wave-0.48s");
            yield return 10;
            yield return t.ShotAs("wave-0.65s");
            yield return 21;
            yield return t.ShotAs("wave-1.00s");
            t.Check(HeardWord(listeners[0], 180), "the enemy in the yard stopped");
            t.Check(HeardWord(listeners[2], 180), "the enemy past the open door stopped");
            TearDown();
        }

        /// <summary>
        /// Not a check: saves one screenshot per game tick for a video of the kit (kit "Clip", so
        /// -rimarttest=inumaki leaves it out; run it with -rimarttest="clip: inumaki"). A yard with a
        /// wall and an open door; stop at shout, then crush and explode at speak, each through the
        /// button's path (warm-up included). The throat is cleared between words so all three are said.
        /// </summary>
        [RimArtTest("Clip", "inumaki words", 20000)]
        private static IEnumerable<int> Clip(RimArtTestContext t)
        {
            Pawn inumaki = Setup(t, WordVolume.Shout);
            IntVec3 c = t.center;
            Building_Door door = null;
            for (int dz = -9; dz <= 9; dz++)
            {
                IntVec3 cell = c + new IntVec3(5, 0, dz);
                if (dz == 1)
                    door = (Building_Door)GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Door, ThingDefOf.WoodLog), cell, t.map);
                else
                    GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog), cell, t.map);
            }
            AccessTools.Field(typeof(Building_Door), "openInt").SetValue(door, true);
            AccessTools.Field(typeof(Building_Door), "holdOpenInt").SetValue(door, true);
            Pawn[] pawns = { Enemy(t, 3, 3), Enemy(t, 2, -3), Enemy(t, -5, -1), Enemy(t, 8, 1), Enemy(t, 9, 6), Colonist(t, -2, 5) };
            foreach (Pawn pawn in pawns) Stand(pawn);
            yield return 2;

            int frame = 0;
            foreach (int step in Say(t, inumaki, "Stop", 100, () => frame++)) yield return step;

            ClearThroat(inumaki);
            SetVolume(inumaki, WordVolume.Speak);
            foreach (Pawn pawn in pawns) if (!pawn.Dead && !pawn.Downed) Stand(pawn);
            foreach (int step in Say(t, inumaki, "Crush", 70, () => frame++)) yield return step;

            ClearThroat(inumaki);
            foreach (int step in Say(t, inumaki, "Explode", 90, () => frame++)) yield return step;
            t.Log("frames: " + frame);
            TearDown();
        }

        /// <summary>Queues the word as the button does, then one screenshot per tick for the warm-up and <paramref name="ticks"/> more.</summary>
        private static IEnumerable<int> Say(RimArtTestContext t, Pawn inumaki, string word, int ticks, System.Func<int> next)
        {
            AbilityOf(inumaki, word).QueueCastingJob(inumaki, LocalTargetInfo.Invalid);
            for (int i = 0; i < 26 + ticks; i++)
            {
                yield return t.ShotAs("f" + next().ToString("000"));
                yield return 1;
            }
        }

        private static void ClearThroat(Pawn pawn)
        {
            Hediff wear = pawn.health.hediffSet.GetFirstHediffOfDef(LarynxDefOf.AG_LarynxWear);
            if (wear != null) pawn.health.RemoveHediff(wear);
        }

        [RimArtTest("Inumaki", "echo 1 the Echo grants six words at 0 charge; the volume toggle is on the hero form")]
        private static IEnumerable<int> Echo(RimArtTestContext t)
        {
            Pawn inumaki = Setup(t);
            Pawn enemy = Enemy(t, 4, 0);
            yield return 2;
            string[] words = { "Stop", "Drop", "Crush", "Come", "Run", "Explode" };
            t.Check(words.All(w => AbilityOf(inumaki, w) != null), "has all six words");
            t.Check(DefDatabase<AbilityDef>.GetNamedSilentFail("AG_Imperative_Kneel") == null, "kneel is gone");
            t.Check(DefDatabase<TraitDef>.GetNamedSilentFail("AG_CommandingVoice") == null, "the Commanding Voice trait is gone");
            t.Check(LarynxUtility.VolumeOf(inumaki) == WordVolume.Speak, "volume starts at speak");
            Hediff form = inumaki.health.hediffSet.GetFirstHediffOfDef(Inumaki.manifestHediff);
            t.Check(form?.TryGetComp<HediffComp_WordVolume>() != null, "the hero form carries the volume toggle");
            float charge = GameComponent_Echoes.Get.charge;
            Say(inumaki, "Stop");
            t.Check(System.Math.Abs(GameComponent_Echoes.Get.charge - charge) < 0.0001f, "stop cost no charge");
            t.Check(HeardWord(enemy, 180), "and the enemy stopped");
            TearDown();
        }
    }
}
