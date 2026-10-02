using System.Collections.Generic;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Mimicry's numbers on top of the Corrosion ones (docs/ego-weapons.md, Weapon 3). An ordinary swing's damage (12 Cut)
    /// and interval (1.2 s) are the tool's power and cooldownTime in the weapon's def, so the info card shows them.
    /// </summary>
    public class CompProperties_EgoMimicry : CompProperties_EgoWeapon
    {
        /// <summary>
        /// The grown swing: the chance per ordinary swing that the blade swells for one downswing, its damage as a multiple
        /// of the swing's, and the blade's size then (the picture draws it at this size; the strip below follows it).
        /// Corrosion and Overclock never grow the blade.
        /// </summary>
        public float growChance = 0.1f;
        public float growDamageFactor = 3.5f;
        public float growScale = 2f;
        /// <summary>Half the width, in cells, of the grown blade's strip on the floor: a pawn whose cell touches it is under the blade.</summary>
        public float growHalfWidth = 0.35f;
        /// <summary>The share of the damage a hit deals to a flesh pawn that heals the wielder's injuries.</summary>
        public float healFraction = 0.1f;
        /// <summary>The arm while corroded or overclocking: the stage it starts at, the most it reaches, and the melee damage each stage adds.</summary>
        public int armStart = 1;
        public int armStages = 4;
        public float stageDamage = 0.15f;
        /// <summary>The corroded hunt: the longest lunge, in cells, that can end next to the pawn it hunts.</summary>
        public float lungeCells = 2f;
        /// <summary>Played at the wielder when the grown blade slams into something; none when unset.</summary>
        public SoundDef slamSound;

        /// <summary>A cell next to the wielder, diagonals included, is within this many cells; Overclock swings only at hostiles that close.</summary>
        public const float MeleeReach = 1.5f;

        public CompProperties_EgoMimicry()
        {
            compClass = typeof(CompEgoMimicry);
        }

        /// <summary>
        /// overclockRange is melee reach, not a number of its own in the def: Overclock swings where it stands, so the button
        /// and the job look only at hostiles next to the wielder (and the action's own check refuses one round a corner).
        /// </summary>
        public override void ResolveReferences(ThingDef parentDef)
        {
            base.ResolveReferences(parentDef);
            overclockRange = MeleeReach;
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef)) yield return error;
            if (growChance < 0f || growChance > 1f) yield return "growChance must be between 0 and 1";
            if (armStages < 0 || armStart < 0 || armStart > armStages) yield return "armStart must be between 0 and armStages";
            if (growHalfWidth <= 0f || growScale <= 0f) yield return "growHalfWidth and growScale must be above 0";
        }
    }

    /// <summary>
    /// The sword's state: the arm's stage, which grows a stage per damaging hit the wielder deals and loses one per damaging
    /// hit it takes while corroded or overclocking, and is 0 outside them. Saved with the sword. Its holder is registered
    /// with <see cref="GameComponent_EgoMimicry"/>, which draws the sword instead of Core. The swing is
    /// <see cref="Verb_EgoMimicry"/>, the corroded hunt and Overclock <see cref="EgoMimicryCorrosion"/>.
    /// </summary>
    public class CompEgoMimicry : CompEgoWeapon
    {
        /// <summary>The arm's stage now, 0 to armStages; 0 outside a corrosion or an Overclock.</summary>
        public int stage;

        public new CompProperties_EgoMimicry Props => (CompProperties_EgoMimicry)props;

        public new static CompEgoMimicry HeldBy(Pawn pawn) => pawn?.equipment?.Primary?.GetComp<CompEgoMimicry>();

        /// <summary>The swing rolls Corrosion itself once it has hit or missed (<see cref="EgoMimicryCast"/>), not when the verb fires.</summary>
        protected override bool RollsWhenUsed => false;

        /// <summary>The arm's stage while the wielder is corroded or overclocking with this sword, else 0.</summary>
        public int StageNow => Wielder != null && EgoMimicry.Special(Wielder, this) ? stage : 0;

        /// <summary>A swing's damage as a multiple of the tool's: stageDamage per arm stage, times growDamageFactor for the grown swing.</summary>
        public float DamageFactor(int armStage, bool grown) => (1f + Props.stageDamage * armStage) * (grown ? Props.growDamageFactor : 1f);

        /// <summary>One damaging hit dealt: a stage more, up to armStages. True when it grew.</summary>
        public bool Grow()
        {
            if (stage >= Props.armStages) return false;
            stage++;
            return true;
        }

        /// <summary>One damaging hit taken: a stage less, down to 0. True when it shrank.</summary>
        public bool Shrink()
        {
            if (stage <= 0) return false;
            stage--;
            return true;
        }

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            GameComponent_EgoMimicry.Instance?.Register(pawn);
        }

        /// <summary>Leaving the hands (dropped, swapped, stripped) breaks off every swing of the wielder's that has not landed yet.</summary>
        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            stage = 0;
            GameComponent_EgoMimicry.Instance?.Unregister(pawn);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref stage, "armStage");
        }
    }
}
