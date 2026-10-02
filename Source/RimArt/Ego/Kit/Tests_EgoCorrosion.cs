using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using static RimArt.RimArtTestContext;

namespace RimArt
{
    /// <summary>
    /// Game tests of the shared Corrosion rules (docs/ego-weapons.md), filter <c>ego</c>. None of the four E.G.O. weapons
    /// has a def yet, so the weapon is an autopistol given a <see cref="CompEgoWeapon"/> with <see cref="EgoTestAction"/>,
    /// which only records whom each firing aimed at. The checks are about the rules: who is aimed at, when, and what the
    /// wielder is left with.
    /// </summary>
    public static class Tests_EgoCorrosion
    {
        private static readonly FieldInfo CompsByType = AccessTools.Field(typeof(ThingWithComps), "compsByType");

        /// <summary>Test numbers: short, so a run takes seconds. Every chance 1 and an unreachable Shooting 21, so the first use always corrodes.</summary>
        private static CompProperties_EgoWeapon Props() => new CompProperties_EgoWeapon
        {
            actionClass = typeof(EgoTestAction),
            corrosionMinor = 1f,
            corrosionMajor = 1f,
            corrosionExtreme = 1f,
            requirementSkill = SkillDefOf.Shooting,
            requirementLevel = 21,
            corrodedDuration = 6f,
            corrodedInterval = 1f,
            exhaustionHours = 2f,
            overclockCount = 3,
            overclockInterval = 0.5f,
            overclockRange = 10f,
            overclockMood = -12,
            overclockMoodDays = 0.5f,
        };

        /// <summary>
        /// A vanilla gun (an autopistol unless <paramref name="weaponDef"/> says otherwise) in <paramref name="pawn"/>'s
        /// hands whose CompEquippable is replaced by a CompEgoWeapon with <paramref name="props"/>. ThingWithComps caches
        /// its comps by type at creation, so the cache is cleared and GetComp falls back to the list, where the new comp
        /// answers for CompEquippable too.
        /// </summary>
        private static CompEgoWeapon Arm(Pawn pawn, CompProperties_EgoWeapon props, string weaponDef = "Gun_Autopistol")
        {
            var gun = (ThingWithComps)ThingMaker.MakeThing(ThingDef.Named(weaponDef));
            var comp = new CompEgoWeapon { parent = gun };
            comp.Initialize(props);
            List<ThingComp> comps = gun.AllComps;
            comps[comps.FindIndex(c => c is CompEquippable)] = comp;
            CompsByType.SetValue(gun, null);
            if (pawn.equipment.Primary != null) pawn.equipment.DestroyEquipment(pawn.equipment.Primary);
            pawn.equipment.AddEquipment(gun);
            return comp;
        }

        private static EgoTestAction Action(CompEgoWeapon gun) => (EgoTestAction)gun.Props.Action;

        private static Hediff Exhaustion(Pawn pawn) => pawn.health.hediffSet.GetFirstHediffOfDef(EgoDefOf.AG_EgoExhausted);

        private static int ExhaustionTicks(Pawn pawn) => Exhaustion(pawn)?.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear ?? -1;

        private static string Mood(Pawn pawn) => pawn.needs?.mood == null ? "no mood" : "mood " + pawn.needs.mood.CurLevel.ToString("0.00");

        [RimArtTest("Ego", "corrosion: mood bands and the skill requirement")]
        public static IEnumerable<int> Bands(RimArtTestContext t)
        {
            t.Clear();
            Pawn pawn = t.Colonist(t.center);
            CompEgoWeapon gun = Arm(pawn, Props());
            CompProperties_EgoWeapon p = gun.Props;
            p.corrosionMinor = 0.25f;
            p.corrosionMajor = 0.75f;
            p.corrosionExtreme = 1f;
            MentalBreaker breaker = pawn.mindState.mentalBreaker;
            float minor = breaker.BreakThresholdMinor, major = breaker.BreakThresholdMajor, extreme = breaker.BreakThresholdExtreme;
            t.Log("break thresholds: minor " + minor.ToString("0.000") + ", major " + major.ToString("0.000") + ", extreme "
                + extreme.ToString("0.000") + "; Shooting " + pawn.skills.GetSkill(SkillDefOf.Shooting).Level);

            float[] moods = { Mathf.Min(1f, minor + 0.1f), (minor + major) / 2f, (major + extreme) / 2f, extreme / 2f };
            float[] met = { 0f, 0.25f, 0.75f, 1f };
            float[] unmet = { 0.25f, 0.75f, 1f, 1f };
            for (int band = 0; band < 4; band++)
            {
                pawn.needs.mood.CurLevel = moods[band];
                p.requirementLevel = 0;
                float withSkill = EgoCorrosion.Chance(pawn, p);
                p.requirementLevel = 21;
                float without = EgoCorrosion.Chance(pawn, p);
                t.Log(Mood(pawn) + ": band " + EgoCorrosion.Band(pawn) + ", chance " + withSkill + " with Shooting 0 required, "
                    + without + " with Shooting 21");
                t.Check(EgoCorrosion.Band(pawn) == band, "mood " + moods[band].ToString("0.00") + " is band " + band);
                t.Check(Mathf.Approximately(withSkill, met[band]), "band " + band + ", requirement met: " + met[band]);
                t.Check(Mathf.Approximately(without, unmet[band]), "band " + band + ", requirement not met: one band worse, " + unmet[band]);
            }

            p.requirementSkill = null;
            pawn.needs.mood.CurLevel = moods[0];
            t.Check(EgoCorrosion.Chance(pawn, p) == 0f, "no requirement skill: no requirement, so above the minor line there is no roll");
            yield return 1;
        }

        [RimArtTest("Ego", "corrosion: a shot corrodes after its burst; it fires at the nearest pawn, then exhaustion", 2400)]
        public static IEnumerable<int> ShotCorrodes(RimArtTestContext t)
        {
            t.Clear();
            Pawn shooter = t.Colonist(t.center);
            Pawn friend = t.Colonist(t.center + new IntVec3(2, 0, 0));
            // Stunned: the burst hits it, and a raider hit leaves its Wait job for a Goto; walking up, it would become the
            // nearest pawn the state fires at.
            Pawn enemy = t.Target(t.center + new IntVec3(-8, 0, 0), stunTicks: 1800);
            // A machine pistol: a burst of 3, so the state must wait for the burst to end instead of starting on shot 1.
            CompEgoWeapon gun = Arm(shooter, Props(), "Gun_MachinePistol");
            EgoTestAction fired = Action(gun);
            fired.firings.Clear();
            t.Log("shooter " + Mood(shooter) + ", band " + EgoCorrosion.Band(shooter) + ", chance " + EgoCorrosion.Chance(shooter, gun.Props)
                + "; burst " + gun.PrimaryVerb.verbProps.burstShotCount);

            shooter.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.AttackStatic, enemy), JobTag.Misc);
            int burstTicks = 0;
            foreach (int wait in WaitFor(() => shooter.MentalState is MentalState_EgoCorroded, 600))
            {
                if (gun.PrimaryVerb.Bursting) burstTicks++;
                yield return wait;
            }
            int start = t.Now;
            t.Log(start + " " + Describe(shooter) + " | state " + (shooter.MentalStateDef?.defName ?? "none") + " | ticks mid-burst before it: "
                + burstTicks + ", bursting now " + gun.PrimaryVerb.Bursting);
            if (!t.Check(shooter.MentalState is MentalState_EgoCorroded, "the first burst corroded the shooter")) yield break;
            t.Check(burstTicks > 0 && !gun.PrimaryVerb.Bursting, "the state started after the burst ended, not between its shots");
            var state = (MentalState_EgoCorroded)shooter.MentalState;
            t.Check(state.weapon == gun.parent, "the state knows its weapon");
            t.Check(!shooter.Drafted, "undrafted");
            t.Check(!shooter.IsColonistPlayerControlled, "not under the player's control");

            int lastLog = 0;
            foreach (int wait in WaitFor(() => !(shooter.MentalState is MentalState_EgoCorroded), 600))
            {
                if (t.Now - lastLog >= 30)
                {
                    lastLog = t.Now;
                    t.Log((t.Now - start) + " " + Describe(shooter) + ", health " + shooter.health.summaryHealth.SummaryHealthPercent.ToStringPercent()
                        + " | firings " + fired.firings.Count + " | enemy " + Describe(enemy));
                }
                yield return wait;
            }
            int lasted = t.Now - start;
            t.Log("state ended after " + lasted + " ticks; " + Describe(shooter));
            foreach (EgoTestAction.Firing f in fired.firings)
                t.Log("  firing at +" + (f.tick - start) + ": " + (f.target?.LabelShort ?? "none") + (f.hostilesOnly ? " (hostiles only)" : ""));

            t.Check(!shooter.InMentalState, "the state ended");
            t.Check(lasted >= 330 && lasted <= 420, "it lasted about the 6 s duration (" + lasted + " ticks)");
            t.Check(fired.firings.Count >= 5 && fired.firings.Count <= 6, "it fired every 1 s: " + fired.firings.Count + " firings");
            t.Check(fired.firings.Count > 0 && fired.firings[0].tick - start >= 55 && fired.firings[0].tick - start <= 90,
                "the first firing came about one interval after the start (mental states tick at the pawn's interval rate)");
            t.Check(fired.firings.All(f => f.target == friend), "every firing aimed at the nearest pawn, the friendly colonist 2 cells away, not the enemy 8 cells away");
            t.Check(fired.firings.All(f => !f.hostilesOnly), "corroded firings are not hostiles-only");
            t.Check(shooter.equipment.Primary == gun.parent, "the shooter still holds the weapon");
            int left = ExhaustionTicks(shooter);
            t.Check(left > 4800 && left <= 5000, "exhausted for the weapon's 2 h (" + left + " ticks left)");
            t.Check(t.Untouched(friend), "the test action hurts no one");
        }

        [RimArtTest("Ego", "corrosion: the weapon leaving the hands ends it; Corrode never replaces a state", 600)]
        public static IEnumerable<int> DroppedEnds(RimArtTestContext t)
        {
            t.Clear();
            Pawn pawn = t.Colonist(t.center);
            CompProperties_EgoWeapon props = Props();
            props.corrodedDuration = 60f;
            CompEgoWeapon gun = Arm(pawn, props);

            t.Check(EgoCorrosion.Corrode(pawn, gun), "Corrode started the state");
            yield return 30;
            t.Log(t.Now + " " + Describe(pawn) + " | state " + (pawn.MentalStateDef?.defName ?? "none"));
            t.Check(pawn.InMentalState, "still corroded after 0.5 s of a 60 s state");
            var state = pawn.MentalState as MentalState_EgoCorroded;
            t.Check(!EgoCorrosion.Corrode(pawn, gun), "Corrode on a pawn already in a mental state does nothing");
            t.Check(state != null && pawn.MentalState == state, "and the state it had is still the same one");

            // The state looks for its weapon in MentalStateTick, which runs on the pawn's interval tick: every tick on
            // screen when zoomed in, at most every 15 ticks (Thing.MaxTickIntervalRate) when the camera is elsewhere.
            bool dropped = pawn.equipment.TryDropEquipment(gun.parent, out ThingWithComps _, pawn.Position);
            int droppedAt = t.Now;
            foreach (int wait in WaitFor(() => !pawn.InMentalState, 16)) yield return wait;
            t.Log(t.Now + " " + Describe(pawn) + " | state " + (pawn.MentalStateDef?.defName ?? "none") + " | primary "
                + (pawn.equipment.Primary?.LabelShort ?? "none") + " | " + (t.Now - droppedAt) + " ticks after the drop");
            t.Check(dropped && pawn.equipment.Primary == null, "the weapon was dropped");
            t.Check(!pawn.InMentalState, "the weapon leaving the hands ended the state by the pawn's next interval tick (" + (t.Now - droppedAt) + " ticks)");
            int left = ExhaustionTicks(pawn);
            t.Check(left > 4800 && left <= 5000, "exhausted for the weapon's 2 h (" + left + " ticks left)");
        }

        [RimArtTest("Ego", "corrosion: going down ends it", 1200)]
        public static IEnumerable<int> DownedEnds(RimArtTestContext t)
        {
            t.Clear();
            Pawn pawn = t.Colonist(t.center);
            CompProperties_EgoWeapon props = Props();
            props.corrodedDuration = 60f;
            props.exhaustionHours = 1f;
            CompEgoWeapon gun = Arm(pawn, props);

            t.Check(EgoCorrosion.Corrode(pawn, gun), "Corrode started the state");
            yield return 30;
            t.Log(t.Now + " " + Describe(pawn) + " | state " + (pawn.MentalStateDef?.defName ?? "none"));
            t.Check(pawn.CurJobDef == EgoDefOf.AG_EgoCorrodedHold, "the corroded pawn stands (hold job)");
            t.Check(pawn.InMentalState, "still corroded after 0.5 s of a 60 s state");

            HealthUtility.DamageUntilDowned(pawn, allowBleedingWounds: false);
            yield return 1;
            t.Log(t.Now + " " + Describe(pawn) + " | state " + (pawn.MentalStateDef?.defName ?? "none"));
            t.Check(pawn.Downed, "the pawn is down");
            t.Check(!pawn.InMentalState, "going down ended the state");
            int left = ExhaustionTicks(pawn);
            t.Check(left > 2400 && left <= 2500, "exhausted for the weapon's 1 h (" + left + " ticks left)");

            EgoCorrosion.Exhaust(pawn, 3f);
            int longer = ExhaustionTicks(pawn);
            EgoCorrosion.Exhaust(pawn, 0.5f);
            int kept = ExhaustionTicks(pawn);
            t.Log("exhausted again for 3 h: " + longer + " ticks; then for 0.5 h: " + kept + " ticks");
            t.Check(longer == 7500 && kept == 7500, "a pawn already exhausted keeps the longer time");
        }

        [RimArtTest("Ego", "overclock: hostiles only, never rolls, costs mood", 1800)]
        public static IEnumerable<int> Overclock(RimArtTestContext t)
        {
            t.Clear();
            Pawn wielder = t.Colonist(t.center);
            wielder.drafter.FireAtWill = false;
            Pawn ally = t.Colonist(t.center + new IntVec3(1, 0, 0));
            CompEgoWeapon gun = Arm(wielder, Props());
            EgoTestAction fired = Action(gun);
            fired.firings.Clear();
            // The action rolls on every firing, as an action that shoots through the weapon's verb would.
            fired.rollInsideFire = true;
            fired.rollsPassed = 0;

            t.Check(gun.CompGetEquippedGizmosExtra().OfType<Command_EgoOverclock>().Any(), "the wielder has the Overclock button");
            t.Check(new Command_EgoOverclock(gun, wielder).Disabled, "Overclock is off with no hostile within 10 cells (an ally 1 cell away)");

            Pawn near = t.Target(t.center + new IntVec3(-5, 0, 0));
            Pawn far = t.Target(t.center + new IntVec3(0, 0, 7), faction: near.Faction);
            Pawn outside = t.Target(t.center + new IntVec3(11, 0, 0), faction: near.Faction);
            var command = new Command_EgoOverclock(gun, wielder);
            if (!t.Check(!command.Disabled, "Overclock is on with hostiles in range")) yield break;

            command.action();
            int start = t.Now;
            foreach (int wait in WaitFor(() => wielder.CurJobDef != EgoDefOf.AG_EgoOverclock, 300)) yield return wait;
            t.Log("overclock ended after " + (t.Now - start) + " ticks; " + Describe(wielder));
            foreach (EgoTestAction.Firing f in fired.firings)
                t.Log("  firing at +" + (f.tick - start) + ": " + (f.target?.LabelShort ?? "none") + (f.hostilesOnly ? " (hostiles only)" : ""));

            t.Check(fired.firings.Count == 3, "3 firings (" + fired.firings.Count + ")");
            t.Check(fired.firings.All(f => f.hostilesOnly && f.target != null && f.target.HostileTo(wielder)), "every firing aimed at a hostile");
            t.Check(fired.firings.All(f => f.target != ally && f.target != outside), "never the ally 1 cell away, never the hostile 11 cells away");
            t.Check(fired.firings.Count > 0 && fired.firings[0].target == near, "the first firing aimed at the nearest hostile");
            int span = fired.firings.Count == 3 ? fired.firings[2].tick - fired.firings[0].tick : -1;
            t.Check(span >= 58 && span <= 62, "one firing every 0.5 s (first to third: " + span + " ticks)");
            t.Check(fired.rollsPassed == 0 && !wielder.InMentalState,
                "Overclock never rolls Corrosion: the action's own rolls (every chance is 1) all came back false, " + fired.rollsPassed + " passed");
            Thought_Memory cost = wielder.needs.mood.thoughts.memories.GetFirstMemoryOfDef(EgoDefOf.AG_EgoOverclocked);
            t.Log("thought: " + (cost == null ? "none" : cost.LabelCap + ", " + cost.MoodOffset() + " mood, " + cost.DurationTicks + " ticks"));
            t.Check(cost != null && Mathf.Approximately(cost.MoodOffset(), -12f), "AG_EgoOverclocked at the weapon's -12 mood");
            t.Check(cost != null && cost.DurationTicks == 30000, "for the weapon's 0.5 days");
            t.Check(t.Untouched(ally) && t.Untouched(near) && t.Untouched(far), "the test action hurts no one");

            // Second Overclock: the hostiles leave after the first firing, so it ends at the next interval, and one
            // firing is enough to pay.
            fired.firings.Clear();
            new Command_EgoOverclock(gun, wielder).action();
            foreach (int wait in WaitFor(() => fired.firings.Count > 0, 60)) yield return wait;
            near.Destroy();
            far.Destroy();
            start = t.Now;
            foreach (int wait in WaitFor(() => wielder.CurJobDef != EgoDefOf.AG_EgoOverclock, 120)) yield return wait;
            t.Log("second overclock: " + fired.firings.Count + " firing, ended " + (t.Now - start) + " ticks after the hostiles left");
            t.Check(fired.firings.Count == 1, "one firing before the hostiles left");
            t.Check(wielder.CurJobDef != EgoDefOf.AG_EgoOverclock, "it ended with no hostile in range");
            t.Check(wielder.needs.mood.thoughts.memories.NumMemoriesOfDef(EgoDefOf.AG_EgoOverclocked) == 2, "the second Overclock paid too (2 memories)");

            // At the stack limit (3) the fourth payment renews the oldest memory with the paying weapon's numbers.
            MemoryThoughtHandler memories = wielder.needs.mood.thoughts.memories;
            EgoCorrosion.PayOverclock(wielder, gun.Props);
            CompProperties_EgoWeapon other = Props();
            other.overclockMood = -20;
            other.overclockMoodDays = 1f;
            Thought_Memory oldest = memories.OldestMemoryOfDef(EgoDefOf.AG_EgoOverclocked);
            EgoCorrosion.PayOverclock(wielder, other);
            int count = memories.NumMemoriesOfDef(EgoDefOf.AG_EgoOverclocked);
            t.Log("after 4 payments: " + count + " memories; the oldest now " + oldest.MoodOffset() + " mood, " + oldest.DurationTicks + " ticks, age " + oldest.age);
            t.Check(count == 3, "the stack stays at 3");
            t.Check(Mathf.Approximately(oldest.MoodOffset(), -20f) && oldest.DurationTicks == 60000 && oldest.age == 0,
                "the fourth payment renewed the oldest memory with the new weapon's -20 mood and 1 day");
        }
    }

    /// <summary>
    /// The test weapon's corroded attack: records each firing (tick, target, hostiles-only) and does nothing else, so no
    /// pawn is hurt. With <see cref="rollInsideFire"/> it also rolls Corrosion on each firing, as a real action that
    /// shoots through the weapon's verb would through Notify_UsedWeapon; <see cref="rollsPassed"/> counts the rolls that
    /// came back true.
    /// </summary>
    public class EgoTestAction : EgoCorrosionAction
    {
        public struct Firing
        {
            public int tick;
            public Pawn target;
            public bool hostilesOnly;
        }

        public readonly List<Firing> firings = new List<Firing>();
        public bool rollInsideFire;
        public int rollsPassed;

        public override void Fire(Pawn wielder, CompEgoWeapon weapon, Pawn target, bool hostilesOnly)
        {
            firings.Add(new Firing { tick = Find.TickManager.TicksGame, target = target, hostilesOnly = hostilesOnly });
            if (rollInsideFire && EgoCorrosion.Roll(wielder, weapon)) rollsPassed++;
        }
    }
}
