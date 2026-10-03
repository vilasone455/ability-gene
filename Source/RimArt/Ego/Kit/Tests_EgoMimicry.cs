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
    /// Game tests of Mimicry (docs/ego-weapons.md, Weapon 3) with the real def, filter <c>ego: mimicry</c>: the swing and its
    /// contact frame, the grown slam, the arm, the def's numbers. The hunt, the lunge, Overclock, saving and breaking off are
    /// in <see cref="Tests_EgoMimicryHunt"/>, the screenshots in <see cref="Tests_EgoMimicryLook"/>. The wielder's mood is full
    /// and its Melee 12, over the requirement, so no swing corrodes unless a test lowers the mood. Targets are stunned for the
    /// whole test: a drafted wielder's Wait job punches an adjacent hostile that is a threat, and a stunned one is not.
    /// </summary>
    public static class Tests_EgoMimicry
    {
        private static readonly FieldInfo CompsByType = AccessTools.Field(typeof(ThingWithComps), "compsByType");

        internal static GameComponent_EgoMimicry Game => GameComponent_EgoMimicry.Instance;

        internal static CompProperties_EgoMimicry Props => (CompProperties_EgoMimicry)EgoDefOf.AG_EgoMimicry.comps.First(c => c is CompProperties_EgoMimicry);

        /// <summary>The sword in <paramref name="wielder"/>'s hands, Melee 12, mood full, no Wimp.</summary>
        internal static CompEgoMimicry Arm(RimArtTestContext t, Pawn wielder)
        {
            t.Equip(wielder, EgoDefOf.AG_EgoMimicry);
            if (wielder.drafter != null) wielder.drafter.FireAtWill = false;
            if (wielder.needs?.mood != null) wielder.needs.mood.CurLevel = 1f;
            SkillRecord melee = wielder.skills?.GetSkill(SkillDefOf.Melee);
            if (melee != null) melee.Level = 12;
            NoWimp(wielder);
            return CompEgoMimicry.HeldBy(wielder);
        }

        /// <summary>The severity of the pawn's injuries that can heal.</summary>
        internal static float Injuries(Pawn pawn) => pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(h => !h.IsPermanent()).Sum(h => h.Severity);

        /// <summary>A bruise of <paramref name="severity"/> on the torso: an injury to heal that does not bleed.</summary>
        internal static void Bruise(Pawn pawn, float severity)
        {
            BodyPartRecord torso = pawn.RaceProps.body.corePart;
            Hediff bruise = HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("Bruise"), pawn, torso);
            bruise.Severity = severity;
            pawn.health.AddHediff(bruise, torso);
        }

        internal static string State(Pawn pawn) => pawn.LabelShort + " " + (pawn.Dead ? "dead" : pawn.health.summaryHealth.SummaryHealthPercent.ToStringPercent()
            + ", injuries " + Injuries(pawn).ToString("0.0") + (pawn.Downed ? ", DOWN" : ""));

        internal static string Line(EgoMimicryCast c) => c.source + (c.grown ? " grown" : "") + (c.Lunges ? " lunge " + c.lungeFrom + "->" + c.lungeTo : "")
            + " at " + (c.target?.LabelShort ?? "nothing") + ", swing " + c.swingTick + ", contact " + c.ContactTick
            + (c.cancelled ? ", BROKEN OFF" : c.resolved ? ", landed " + c.landed + " stage " + c.stageAtContact + " struck "
                + string.Join(", ", c.struck.Select(s => s.Key.LabelShort + " " + s.Value.ToString("0.0"))) : ", pending");

        /// <summary>Adds every swing the game has started since the last call to <paramref name="seen"/>.</summary>
        internal static void Watch(List<EgoMimicryCast> seen)
        {
            foreach (EgoMimicryCast c in Game.Casts)
                if (!seen.Contains(c)) seen.Add(c);
        }

        /// <summary>Moves a pawn to <paramref name="cell"/> at once, keeping its job.</summary>
        internal static void Step(Pawn pawn, IntVec3 cell)
        {
            pawn.Position = cell;
            pawn.Notify_Teleported(false, true);
        }

        /// <summary>
        /// Once the wielder is free, steps <paramref name="target"/> onto <paramref name="cell"/> and strikes it in the same
        /// tick, and adds the swing to <paramref name="into"/> (null when the strike did not start one). A hostile next to a
        /// drafted wielder is punched by the wielder's own Wait job whenever its cooldown ends, and a hostile spawned next to
        /// it punches first: tests spawn their targets out of reach and keep them there except for their own strikes.
        /// </summary>
        internal static IEnumerable<int> StrikeAt(RimArtTestContext t, Pawn wielder, Pawn target, IntVec3 cell, List<EgoMimicryCast> into)
        {
            foreach (int w in WaitFor(() => Free(wielder) && !Game.Busy(wielder), 240)) yield return w;
            Step(target, cell);
            bool started = t.Strike(wielder, target);
            EgoMimicryCast cast = Game.Latest(wielder);
            into.Add(started && cast != null && cast.target == target && cast.swingTick == t.Now ? cast : null);
        }

        /// <summary>
        /// Whether the sword's hit, built 200 times at <paramref name="factor"/>, stays within 0.8 to 1.2 of the tool's damage
        /// times it. The damage a pawn takes can be more: its own genes and health multiply what comes in.
        /// </summary>
        internal static bool Scaled(RimArtTestContext t, CompEgoMimicry sword, Pawn wielder, Thing target, float factor, string label)
        {
            Verb_EgoMimicry verb = Verb_EgoMimicry.Of(sword.parent);
            float tool = verb.verbProps.AdjustedMeleeDamageAmount(verb, wielder) * factor, lo = float.MaxValue, hi = 0f;
            for (int i = 0; i < 200; i++)
            {
                float amount = verb.Damage(target, factor).Amount;
                lo = Mathf.Min(lo, amount);
                hi = Mathf.Max(hi, amount);
            }
            t.Log(label + ": x" + factor.ToString("0.00") + " -> " + lo.ToString("0.0") + " to " + hi.ToString("0.0") + " (tool " + tool.ToString("0.0") + ")");
            return lo >= tool * 0.8f - 0.01f && hi <= tool * 1.2f + 0.01f && hi - lo > tool * 0.2f;
        }

        /// <summary>Whether a hit fed the wielder at or after <paramref name="tick"/> (the rule's own record; natural healing also lowers injuries).</summary>
        internal static bool FedSince(Pawn wielder, int tick) => (Game.LookOf(wielder)?.feedTick ?? int.MinValue) >= tick;

        /// <summary>Absorbs every hit (ThingWithComps.PreApplyDamage asks the comps first), as armour that turns a hit to nothing.</summary>
        internal sealed class Absorb : ThingComp
        {
            public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed) => absorbed = true;
        }

        internal static ThingComp Shield(Pawn pawn)
        {
            var comp = new Absorb { parent = pawn, props = new CompProperties() };
            pawn.AllComps.Add(comp);
            CompsByType.SetValue(pawn, null);
            return comp;
        }

        internal static void Unshield(Pawn pawn, ThingComp comp)
        {
            pawn.AllComps.Remove(comp);
            CompsByType.SetValue(pawn, null);
        }

        [RimArtTest("Ego", "mimicry: a swing lands at its contact frame, heals 10 % from flesh only, nothing when absorbed, one roll per swing, hit or miss", 2400)]
        public static IEnumerable<int> Contact(RimArtTestContext t)
        {
            t.Clear();
            Game.Clear();
            float grow = Props.growChance;
            Props.growChance = 0f;
            try
            {
                IntVec3 c = t.center, park = c + new IntVec3(0, 0, -6);
                Pawn wielder = t.Colonist(c);
                CompEgoMimicry sword = Arm(t, wielder);
                Bruise(wielder, 12f);
                Pawn flesh = t.Target(park, stunTicks: 3000);
                NoWimp(flesh);
                int rolls = EgoMimicry.rollsForTests;
                Verb_EgoMimicry verb = Verb_EgoMimicry.Of(sword.parent);
                t.Log("tool: " + verb.verbProps.AdjustedMeleeDamageAmount(verb, wielder).ToString("0.0") + " dmg, cooldown "
                    + verb.verbProps.AdjustedCooldownTicks(verb, wielder) + " ticks; " + State(wielder));
                var casts = new List<EgoMimicryCast>();

                // A hit on flesh: nothing on the first tick, the hit at the contact frame, 10 % of it healed, one roll.
                float before = Injuries(wielder);
                foreach (int w in StrikeAt(t, wielder, flesh, c + IntVec3.East, casts)) yield return w;
                EgoMimicryCast cast = casts.Last();
                int start = t.Now;
                t.Check(cast != null && !cast.resolved && !t.Struck(flesh), "the strike started a swing and nothing landed on its first tick");
                if (cast == null) yield break;
                t.Check(cast.ContactTick - cast.swingTick == 13, "the contact frame is 13 ticks (0.22 s) into the swing (" + (cast.ContactTick - cast.swingTick) + ")");
                foreach (int w in WaitFor(() => cast.resolved, 60)) yield return w;
                Step(flesh, park);
                t.Log("+" + (t.Now - start) + " " + Line(cast) + " | " + State(flesh) + " | " + State(wielder));
                float dealt = cast.struck.Count > 0 ? cast.struck[0].Value : 0f, healed = before - Injuries(wielder);
                t.Check(t.Now - start >= 12, "the hit came at the contact frame, " + (t.Now - start) + " ticks after the strike");
                t.Check(t.Struck(flesh) && dealt > 0f, "it hit the target for " + dealt.ToString("0.0"));
                t.Check(healed >= dealt * 0.1f - 0.02f && healed <= dealt * 0.1f + 0.3f, "the wielder healed 10 % of it (" + healed.ToString("0.00") + " of " + dealt.ToString("0.0") + ")");
                t.Check(EgoMimicry.rollsForTests == rolls + 1, "one Corrosion roll for the swing");
                t.Check(Scaled(t, sword, wielder, flesh, 1f, "an ordinary hit"), "an ordinary hit is the tool's 12, 0.8 to 1.2 of it");

                // A mechanoid: hit, nothing healed.
                Pawn mech = t.Mech(c + new IntVec3(6, 0, 0));
                if (mech != null)
                {
                    mech.stances.stunner.StunFor(3000, null, false);
                    before = Injuries(wielder);
                    foreach (int w in StrikeAt(t, wielder, mech, c + IntVec3.North, casts)) yield return w;
                    cast = casts.Last();
                    if (t.Check(cast != null, "a strike at the mechanoid"))
                    {
                        foreach (int w in WaitFor(() => cast.resolved, 60)) yield return w;
                        t.Log(Line(cast) + " | " + State(wielder));
                        t.Check(cast.landed && !FedSince(wielder, cast.ContactTick) && Injuries(wielder) >= before - 0.15f,
                            "a hit on a mechanoid heals nothing (" + (before - Injuries(wielder)).ToString("0.00") + ", natural healing aside)");
                        t.Check(EgoMimicry.rollsForTests == rolls + 2, "one more roll");
                    }
                    mech.Destroy();
                }

                // Absorbed: the hit lands but deals nothing, so nothing is healed.
                ThingComp shield = Shield(flesh);
                before = Injuries(wielder);
                int rollsNow = EgoMimicry.rollsForTests;
                t.Note(flesh);
                foreach (int w in StrikeAt(t, wielder, flesh, c + IntVec3.East, casts)) yield return w;
                cast = casts.Last();
                if (t.Check(cast != null, "a strike at the shielded pawn"))
                {
                    foreach (int w in WaitFor(() => cast.resolved, 60)) yield return w;
                    Step(flesh, park);
                    t.Log(Line(cast) + " | " + State(flesh) + " | " + State(wielder));
                    t.Check(cast.landed && cast.struck.Count == 1 && cast.struck[0].Value <= 0f && !t.Struck(flesh), "an absorbed hit lands and deals nothing");
                    t.Check(!FedSince(wielder, cast.ContactTick) && Injuries(wielder) >= before - 0.15f, "and heals nothing (" + (before - Injuries(wielder)).ToString("0.00") + ", natural healing aside)");
                    t.Check(EgoMimicry.rollsForTests == rollsNow + 1, "it still rolls once");
                }
                Unshield(flesh, shield);

                // Out of reach at contact: a miss, no damage, still one roll; with the mood at 0 that roll corrodes.
                wielder.needs.mood.CurLevel = 0f;
                rollsNow = EgoMimicry.rollsForTests;
                t.Note(flesh);
                foreach (int w in StrikeAt(t, wielder, flesh, c + IntVec3.East, casts)) yield return w;
                cast = casts.Last();
                Step(flesh, c + new IntVec3(4, 0, 0));
                if (t.Check(cast != null, "a strike at the pawn that steps away"))
                {
                    foreach (int w in WaitFor(() => cast.resolved, 60)) yield return w;
                    yield return 3;
                    t.Log(Line(cast) + " | " + State(flesh) + " | " + RimArtTestContext.Describe(wielder) + " state " + wielder.MentalStateDef?.defName);
                    t.Check(!cast.landed && cast.struck.Count == 0 && !t.Struck(flesh), "a target that stepped out of reach is missed and untouched");
                    t.Check(EgoMimicry.rollsForTests == rollsNow + 1, "the miss rolls once too");
                    t.Check(wielder.MentalState is MentalState_EgoCorroded, "and with the mood at 0 that roll corroded the wielder");
                }
                wielder.MentalState?.RecoverFromState();
            }
            finally
            {
                Props.growChance = grow;
            }
        }

        [RimArtTest("Ego", "mimicry: at 0 % no swing grows; at 100 % the slam strikes each hostile under the blade once, not allies, pawns off the strip or behind a wall, nor a target that stepped away", 3600)]
        public static IEnumerable<int> Grown(RimArtTestContext t)
        {
            t.Clear();
            Game.Clear();
            float grow = Props.growChance;
            try
            {
                IntVec3 c = t.center, park = c + new IntVec3(0, 0, -6);
                Pawn wielder = t.Colonist(c);
                CompEgoMimicry sword = Arm(t, wielder);
                Pawn a = t.Target(park, stunTicks: 3000);
                Faction foes = a.Faction;
                var casts = new List<EgoMimicryCast>();

                // 0 %: three swings, none grown.
                Props.growChance = 0f;
                for (int i = 0; i < 3; i++)
                {
                    foreach (int w in StrikeAt(t, wielder, a, c + IntVec3.East, casts)) yield return w;
                    foreach (int w in WaitFor(() => casts.Last() == null || casts.Last().resolved, 60)) yield return w;
                    Step(a, park);
                    InjuryHeal.Heal(a, 1000f);
                }
                t.Log("at 0 %: " + string.Join(" | ", casts.Select(x => x == null ? "no swing" : Line(x))));
                t.Check(casts.Count == 3 && casts.All(x => x != null && !x.grown), "at 0 % none of 3 swings grew");
                t.Note(a);

                // 100 %: the slam along the east strip (0.67 to 2.71 cells). The hostiles first (a new hostile punches whoever is
                // next to it before its stun), the colonist last, holding still: its own Wait job would punch them.
                Props.growChance = 1f;
                foreach (int w in WaitFor(() => Free(wielder) && !Game.Busy(wielder), 240)) yield return w;
                Pawn b = t.Target(c + new IntVec3(2, 0, 0), stunTicks: 3000, faction: foes);
                Pawn north = t.Target(c + new IntVec3(2, 0, 1), stunTicks: 3000, faction: foes);
                Pawn south = t.Target(c + new IntVec3(2, 0, -1), stunTicks: 3000, faction: foes);
                Pawn far = t.Target(c + new IntVec3(4, 0, 0), stunTicks: 3000, faction: foes);
                Pawn ally = t.Colonist(c + new IntVec3(3, 0, 0));
                ally.drafter.Drafted = false;
                ally.jobs.StartJob(JobMaker.MakeJob(EgoDefOf.AG_EgoCorrodedHold), JobCondition.InterruptForced);
                int rolls = EgoMimicry.rollsForTests;
                foreach (int w in StrikeAt(t, wielder, a, c + IntVec3.East, casts)) yield return w;
                EgoMimicryCast cast = casts.Last();
                if (!t.Check(cast != null && cast.grown, "at 100 % the swing grew")) yield break;
                yield return 20;
                t.Check(t.Untouched(a) && t.Untouched(b), "nothing has landed 20 ticks in (the slam is at " + (cast.ContactTick - cast.swingTick) + ")");
                yield return t.ShotAs("mimicry-grown-raise", c + new IntVec3(1, 0, 0), 4f);
                foreach (int w in WaitFor(() => cast.resolved, 60)) yield return w;
                yield return 2;
                t.Log(Line(cast));
                t.Log(string.Join(" | ", new[] { a, b, ally, north, south, far }.Select(State)));
                yield return t.ShotAs("mimicry-grown-slam", c + new IntVec3(1, 0, 0), 4f);
                if (a.Spawned) Step(a, park);
                t.Check(cast.struck.Count == 2 && cast.struck.Any(x => x.Key == a) && cast.struck.Any(x => x.Key == b), "the slam struck the two hostiles under the strip, each once");
                t.Check(t.Untouched(ally) && t.Untouched(north) && t.Untouched(south) && t.Untouched(far), "not the colonist under it, nor the hostiles beside the strip or past its end");
                t.Check(cast.struck.All(x => x.Value > 0f), "each took damage");
                t.Check(Scaled(t, sword, wielder, b, sword.DamageFactor(0, true), "a slam's hit"), "a slam's hit is 3.5 x the tool's, 0.8 to 1.2 of it");
                t.Check(EgoMimicry.rollsForTests == rolls + 1, "the slam rolled once, not once per pawn");
                foreach (int w in WaitFor(() => t.Now - cast.swingTick >= 78, 120)) yield return w;
                t.Check(wielder.stances.curStance is Stance_Cooldown, "78 ticks after the slam's swing began the wielder is still cooling down (1.2 s would be over at 72)");
                foreach (int w in WaitFor(() => Free(wielder), 60)) yield return w;
                t.Log("free again " + (t.Now - cast.swingTick) + " ticks after the swing began");
                t.Check(t.Now - cast.swingTick >= 83 && t.Now - cast.swingTick <= 86, "the grown swing's 1.4 s recovery (84 ticks) set the interval");

                // North, with a wall: the hostile behind it is under the strip but not struck; then a target that steps off.
                foreach (Pawn p in new[] { a, b, ally, north, south, far }) Tests_EgoMimicryHunt.Remove(p);
                IntVec3 w2 = c + new IntVec3(-6, 0, -6), park2 = w2 + new IntVec3(-4, 0, 0);
                Pawn second = t.Colonist(w2);
                Arm(t, second);
                Pawn aimed = t.Target(park2, stunTicks: 3000, faction: foes);
                t.Wall(w2 + new IntVec3(0, 0, 2));
                Pawn behind = t.Target(w2 + new IntVec3(0, 0, 3), stunTicks: 3000, faction: foes);
                foreach (int w in StrikeAt(t, second, aimed, w2 + new IntVec3(0, 0, 1), casts)) yield return w;
                cast = casts.Last();
                if (t.Check(cast != null && cast.grown, "a slam north"))
                {
                    foreach (int w in WaitFor(() => cast.resolved, 60)) yield return w;
                    t.Log(Line(cast) + " | " + State(behind));
                    t.Check(cast.struck.Count == 1 && cast.struck[0].Key == aimed && t.Untouched(behind), "the wall kept the hostile behind it out");
                }
                Tests_EgoMimicryHunt.Remove(aimed);
                Pawn mover = t.Target(park2, stunTicks: 3000, faction: foes);
                foreach (int w in StrikeAt(t, second, mover, w2 + new IntVec3(0, 0, 1), casts)) yield return w;
                cast = casts.Last();
                if (t.Check(cast != null && cast.grown, "a slam at a pawn that steps away"))
                {
                    yield return 20;
                    Step(mover, w2 + new IntVec3(-2, 0, 1));
                    foreach (int w in WaitFor(() => cast.resolved, 60)) yield return w;
                    t.Log(Line(cast) + " | " + State(mover));
                    t.Check(cast.struck.Count == 0 && t.Untouched(mover), "a target that stepped off the strip before the slam is not struck");
                }
            }
            finally
            {
                Props.growChance = grow;
            }
        }

        [RimArtTest("Ego", "mimicry: Overclock's arm starts at 1, grows per damaging hit dealt, shrinks per damaging hit taken, stays within 0 to 4, +15 % per stage, gone when it ends", 2400)]
        public static IEnumerable<int> ArmStages(RimArtTestContext t)
        {
            t.Clear();
            Game.Clear();
            IntVec3 c = t.center;
            Pawn wielder = t.Colonist(c);
            CompEgoMimicry sword = Arm(t, wielder);
            Pawn foe = t.Target(c + new IntVec3(0, 0, -6), stunTicks: 3000);
            NoWimp(foe);
            bool table = true;
            for (int s = 0; s <= 4; s++) table &= Mathf.Abs(sword.DamageFactor(s, false) - (1f + 0.15f * s)) < 1e-4f;
            t.Check(table && Mathf.Abs(sword.DamageFactor(0, true) - 3.5f) < 1e-4f, "damage x(1 + 0.15 per stage), the grown swing x3.5");
            bool scaled = true;
            for (int s = 0; s <= 4; s++) scaled &= Scaled(t, sword, wielder, foe, sword.DamageFactor(s, false), "stage " + s);
            t.Check(scaled, "each stage's hit is the tool's x(1 + 0.15 per stage), 0.8 to 1.2 of it");
            t.Check(sword.stage == 0 && sword.StageNow == 0, "no arm outside a corrosion or Overclock");

            Step(foe, c + IntVec3.East);
            wielder.jobs.TryTakeOrderedJob(JobMaker.MakeJob(EgoDefOf.AG_EgoOverclock, sword.parent), JobTag.Misc);
            yield return 1;
            t.Log("overclock: " + RimArtTestContext.Describe(wielder) + ", stage " + sword.stage);
            t.Check(EgoMimicry.Special(wielder, sword) && sword.stage == 1, "Overclock starts the arm at stage 1");
            // The limits, through the rules while the state runs, before the first swing lands.
            sword.stage = 4;
            EgoMimicry.Landed(wielder, sword, foe, 5f);
            t.Check(sword.stage == 4, "a damaging hit at stage 4 stays at 4");
            sword.stage = 0;
            EgoMimicry.HitTaken(wielder, new DamageInfo(DamageDefOf.Blunt, 3f, 0f, -1f, foe), 3f);
            t.Check(sword.stage == 0, "a hit taken at stage 0 stays at 0");
            sword.stage = 1;
            InjuryHeal.Heal(foe, 1000f);

            var seen = new List<EgoMimicryCast>();
            var done = new HashSet<EgoMimicryCast>();
            int expected = 1, resolved = 0;
            bool grewRight = true;
            foreach (int w in WaitFor(() => wielder.CurJobDef != EgoDefOf.AG_EgoOverclock, 600))
            {
                Watch(seen);
                foreach (EgoMimicryCast cast in seen)
                {
                    if (!cast.resolved || done.Contains(cast)) continue;
                    done.Add(cast);
                    resolved++;
                    float dealt = cast.struck.Count > 0 ? cast.struck[0].Value : 0f;
                    if (dealt > 0f) expected = Mathf.Min(4, expected + 1);
                    grewRight &= sword.stage == expected;
                    t.Log(t.Now + " " + Line(cast) + " -> stage " + sword.stage + " (expected " + expected + ") | " + State(foe));
                    InjuryHeal.Heal(foe, 1000f);
                    if (resolved == 2)
                    {
                        wielder.TakeDamage(new DamageInfo(DamageDefOf.Blunt, 3f, 0f, -1f, foe));
                        expected = Mathf.Max(0, expected - 1);
                        t.Log("the wielder is hit for 3: stage " + sword.stage + " (expected " + expected + ")");
                        grewRight &= sword.stage == expected;
                        ThingComp shield = Shield(wielder);
                        wielder.TakeDamage(new DamageInfo(DamageDefOf.Blunt, 3f, 0f, -1f, foe));
                        Unshield(wielder, shield);
                        t.Log("an absorbed hit: stage " + sword.stage);
                        grewRight &= sword.stage == expected;
                    }
                }
                yield return w;
            }
            t.Check(resolved == 5, "Overclock swung 5 times (" + resolved + ")");
            t.Check(grewRight, "each damaging hit dealt added a stage up to 4, the damaging hit taken removed one, the absorbed one nothing");
            t.Check(sword.stage == 0 && sword.StageNow == 0 && !EgoMimicry.Special(wielder, sword), "when Overclock ended the arm and its bonus were gone at once");
        }

        [RimArtTest("Ego", "mimicry: the def's numbers, only the sword gets its verb, and Core never draws it held", 600)]
        public static IEnumerable<int> Def(RimArtTestContext t)
        {
            t.Clear();
            Game.Clear();
            Pawn wielder = t.Colonist(t.center);
            CompEgoMimicry sword = Arm(t, wielder);
            CompProperties_EgoMimicry p = sword.Props;
            t.Log("corrosion " + p.corrosionMinor + "/" + p.corrosionMajor + "/" + p.corrosionExtreme + ", " + p.requirementSkill?.defName + " " + p.requirementLevel
                + ", " + p.corrodedDuration + " s every " + p.corrodedInterval + " s, exhausted " + p.exhaustionHours + " h; overclock " + p.overclockCount + " x "
                + p.overclockInterval + " s within " + p.overclockRange + ", " + p.overclockMood + " for " + p.overclockMoodDays + " d; grow " + p.growChance + " x"
                + p.growDamageFactor + " size " + p.growScale + " half-width " + p.growHalfWidth + "; heal " + p.healFraction + "; arm " + p.armStart + "/" + p.armStages
                + " +" + p.stageDamage + "; lunge " + p.lungeCells);
            t.Check(p.corrosionMinor == 0.5f && p.corrosionMajor == 1f && p.corrosionExtreme == 1f && p.requirementSkill == SkillDefOf.Melee && p.requirementLevel == 8,
                "bands 50/100/100 %, Melee 8");
            t.Check(p.corrodedDuration == 40f && p.corrodedInterval == 1.5f && p.exhaustionHours == 3f, "40 s, a firing every 1.5 s, 3 h exhausted");
            t.Check(p.overclockCount == 5 && p.overclockInterval == 1f && p.overclockMood == -20 && p.overclockMoodDays == 1f && p.overclockRange == 1.5f,
                "Overclock 5 swings 1 s apart at melee reach (1.5), -20 mood for 1 day");
            t.Check(p.growChance == 0.1f && p.growDamageFactor == 3.5f && p.growScale == 2f && p.growHalfWidth == 0.35f && p.healFraction == 0.1f
                && p.armStart == 1 && p.armStages == 4 && p.stageDamage == 0.15f && p.lungeCells == 2f, "grown 10 % x3.5 size 2 half-width 0.35, heal 10 %, arm 1 to 4 at +15 %, lunge 2");
            Verb_EgoMimicry verb = Verb_EgoMimicry.Of(sword.parent);
            List<Verb> verbs = sword.AllVerbs;
            t.Log("verbs: " + string.Join(", ", verbs.Select(v => v.GetType().Name + " " + v.maneuver?.defName + " " + v.tool?.power + "/" + v.tool?.cooldownTime)));
            t.Check(verb != null && verbs.Count == 1 && verb.tool.power == 12f && verb.tool.cooldownTime == 1.2f && verb.maneuver?.defName == "AG_EgoMimicrySlash",
                "one verb, Verb_EgoMimicry, 12 power every 1.2 s");
            var longsword = (ThingWithComps)ThingMaker.MakeThing(ThingDef.Named("MeleeWeapon_LongSword"), ThingDefOf.Steel);
            t.Check(Verb_EgoMimicry.Of(longsword) == null, "a longsword's cut is still Core's (the maneuver needs the sword's own capacity)");
            longsword.Destroy();
            t.Check(!HeldWeaponHide.Shown(sword.parent), "Core does not draw the held sword (the picture does)");
            t.Check(Game.Holds(wielder), "the wielder is registered as a holder");
            yield return 1;
        }
    }
}
