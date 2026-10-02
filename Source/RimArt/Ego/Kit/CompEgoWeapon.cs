using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// One E.G.O. weapon's Corrosion numbers (docs/ego-weapons.md). Every value is a placeholder until the balance pass and
    /// is set in the weapon's def. Times are seconds unless the name says hours or days.
    /// </summary>
    public class CompProperties_EgoWeapon : CompProperties
    {
        /// <summary>The weapon's <see cref="EgoCorrosionAction"/> subclass.</summary>
        public Type actionClass;

        /// <summary>Chance that one use corrodes, by the wielder's mood band: minor to major, major to extreme, below extreme. Above the minor line there is no roll.</summary>
        public float corrosionMinor = 0.25f;
        public float corrosionMajor = 0.75f;
        public float corrosionExtreme = 1f;

        /// <summary>A wielder whose <see cref="requirementSkill"/> is under <see cref="requirementLevel"/> rolls one band worse (Lobotomy's gear requirement). No skill set: no requirement.</summary>
        public SkillDef requirementSkill;
        public int requirementLevel;

        /// <summary>The corroded state lasts this long and fires the action every <see cref="corrodedInterval"/>; then exhaustion for <see cref="exhaustionHours"/> game hours.</summary>
        public float corrodedDuration = 30f;
        public float corrodedInterval = 3f;
        public float exhaustionHours = 2f;

        /// <summary>
        /// Overclock: the action <see cref="overclockCount"/> times, one every <see cref="overclockInterval"/>, at the nearest
        /// hostile within <see cref="overclockRange"/> cells; then <see cref="overclockMood"/> mood for
        /// <see cref="overclockMoodDays"/> game days. The button is off while no hostile is within the range, so an area
        /// action sets the range to its radius.
        /// </summary>
        public int overclockCount = 6;
        public float overclockInterval = 1f;
        public float overclockRange = 30f;
        public int overclockMood = -15;
        public float overclockMoodDays = 1f;

        private EgoCorrosionAction action;

        public EgoCorrosionAction Action => action ?? (action = (EgoCorrosionAction)Activator.CreateInstance(actionClass));

        public int DurationTicks => corrodedDuration.SecondsToTicks();
        public int IntervalTicks => Math.Max(1, corrodedInterval.SecondsToTicks());
        public int OverclockIntervalTicks => Math.Max(1, overclockInterval.SecondsToTicks());

        public CompProperties_EgoWeapon()
        {
            compClass = typeof(CompEgoWeapon);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef)) yield return error;
            if (actionClass == null || actionClass.IsAbstract || !typeof(EgoCorrosionAction).IsAssignableFrom(actionClass))
                yield return "actionClass must name a RimArt.EgoCorrosionAction subclass";
            if (!typeof(CompEgoWeapon).IsAssignableFrom(compClass))
                yield return "compClass must be CompEgoWeapon or a subclass";
            if (corrodedInterval <= 0f || overclockInterval <= 0f)
                yield return "corrodedInterval and overclockInterval must be above 0";
        }
    }

    /// <summary>
    /// An E.G.O. weapon: rolls Corrosion on every use and gives its wielder the Overclock button. It is the weapon's
    /// CompEquippable (the def lists its comps with Inherit="False"), as CompFlameGauntlet is, because the game asks only
    /// Pawn_EquipmentTracker.PrimaryEq for equipped gizmos. A weapon with rules of its own (Magic Bullet's count)
    /// subclasses it.
    /// </summary>
    public class CompEgoWeapon : CompEquippable
    {
        public CompProperties_EgoWeapon Props => (CompProperties_EgoWeapon)props;

        public Pawn Wielder => Holder;

        public static CompEgoWeapon HeldBy(Pawn pawn) => pawn?.equipment?.Primary?.GetComp<CompEgoWeapon>();

        /// <summary>
        /// Once per shot or swing that went off: Verb.TryCastNextBurstShot calls it through ThingWithComps after
        /// TryCastShot succeeds. A roll that passes corrodes the pawn after the burst, not inside this call. A weapon whose
        /// attack skips the verb calls <see cref="EgoCorrosion.Roll"/> itself.
        /// </summary>
        public override void Notify_UsedWeapon(Pawn pawn)
        {
            base.Notify_UsedWeapon(pawn);
            EgoCorrosion.Roll(pawn, this);
        }

        public override IEnumerable<Gizmo> CompGetEquippedGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetEquippedGizmosExtra()) yield return gizmo;
            Pawn holder = Holder;
            if (holder != null && holder.IsColonistPlayerControlled) yield return new Command_EgoOverclock(this, holder);
        }
    }
}
