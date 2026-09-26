using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// One condition a candidate must meet. Written in XML as &lt;li Class="RimArt.Trial_Skill"&gt;.
    /// Progress is read live from the pawn, so a Trial that was met can become unmet again (a skill
    /// that decays, a trait gained later); the letter is offered only while every Trial is met.
    /// </summary>
    public abstract class EchoTrial
    {
        /// <summary>Overrides the generated line on the card, for a Trial that needs its own words.</summary>
        public string label;

        public abstract float Current(Pawn pawn);
        public abstract float Target { get; }
        public virtual bool Met(Pawn pawn) => Current(pawn) >= Target;

        /// <summary>A Trial the pawn meets by not having something. Drawn as a check, not a bar.</summary>
        public virtual bool IsExclusion => false;

        protected abstract string DefaultLabel { get; }
        public string Label => label.NullOrEmpty() ? DefaultLabel : label;

        public virtual string ProgressText(Pawn pawn) =>
            IsExclusion ? (Met(pawn) ? "AG_EchoTrialYes".Translate() : "AG_EchoTrialNo".Translate()).ToString()
                : Current(pawn).ToString("0.##") + " / " + Target.ToString("0.##");

        public virtual IEnumerable<string> ConfigErrors() { yield break; }
    }

    public class Trial_Skill : EchoTrial
    {
        public SkillDef skill;
        public int level;

        public override float Current(Pawn pawn)
        {
            SkillRecord record = pawn?.skills?.GetSkill(skill);
            return record == null || record.TotallyDisabled ? 0f : record.Level;
        }

        public override float Target => level;
        protected override string DefaultLabel => skill.LabelCap + " " + level;

        public override IEnumerable<string> ConfigErrors()
        {
            if (skill == null) yield return "skill is null";
        }
    }

    /// <summary>A vanilla record (Kills, DamageTaken, PeopleCaptured ...), counted over the pawn's whole life.</summary>
    public class Trial_Record : EchoTrial
    {
        public RecordDef record;
        public float count;

        public override float Current(Pawn pawn) => pawn?.records?.GetValue(record) ?? 0f;
        public override float Target => count;
        protected override string DefaultLabel => record.LabelCap + " " + count.ToString("0");

        public override IEnumerable<string> ConfigErrors()
        {
            if (record == null) yield return "record is null";
        }
    }

    public class Trial_Stat : EchoTrial
    {
        public StatDef stat;
        public float min;

        public override float Current(Pawn pawn) => pawn == null ? 0f : pawn.GetStatValue(stat);
        public override float Target => min;
        protected override string DefaultLabel => stat.LabelCap + " " + stat.ValueToString(min);

        public override string ProgressText(Pawn pawn) =>
            stat.ValueToString(Current(pawn)) + " / " + stat.ValueToString(min);

        public override IEnumerable<string> ConfigErrors()
        {
            if (stat == null) yield return "stat is null";
        }
    }

    /// <summary>The wealth of the richest player home map.</summary>
    public class Trial_ColonyWealth : EchoTrial
    {
        public float wealth;

        public override float Current(Pawn pawn)
        {
            float best = 0f;
            foreach (Map map in Find.Maps)
                if (map.IsPlayerHome && map.wealthWatcher.WealthTotal > best) best = map.wealthWatcher.WealthTotal;
            return best;
        }

        public override float Target => wealth;
        protected override string DefaultLabel => "AG_EchoTrialWealth".Translate(wealth.ToStringMoney());
        public override string ProgressText(Pawn pawn) => Current(pawn).ToStringMoney() + " / " + wealth.ToStringMoney();
    }

    /// <summary>Excludes pawns with a trait, for example Pacifist-like traits on a fighting hero.</summary>
    public class Trial_NotTrait : EchoTrial
    {
        public TraitDef trait;
        /// <summary>With matchDegree, only this degree is excluded; otherwise any degree is.</summary>
        public int degree;
        public bool matchDegree;

        private bool Has(Pawn pawn) => pawn?.story?.traits != null && (matchDegree
            ? pawn.story.traits.HasTrait(trait, degree) : pawn.story.traits.HasTrait(trait));

        public override float Current(Pawn pawn) => Has(pawn) ? 0f : 1f;
        public override float Target => 1f;
        public override bool IsExclusion => true;

        // Single-degree traits (Psychopath) have their label on the degree data, not the trait.
        protected override string DefaultLabel => "AG_EchoTrialNot".Translate(matchDegree || trait.label.NullOrEmpty()
            ? trait.DataAtDegree(matchDegree ? degree : trait.degreeDatas[0].degree).GetLabelCapFor(null)
            : trait.LabelCap.ToString());

        public override IEnumerable<string> ConfigErrors()
        {
            if (trait == null) yield return "trait is null";
        }
    }

    /// <summary>
    /// Kills made with a weapon of one of the named defs, or with any melee or any ranged weapon.
    /// Vanilla does not record the weapon behind a kill, so GameComponent_Echoes counts these from
    /// the moment the mod is loaded.
    /// </summary>
    public class Trial_KillsWith : EchoTrial
    {
        public List<ThingDef> weapons = new List<ThingDef>();
        public bool anyMelee, anyRanged;
        public int count;
        /// <summary>Names the weapon kind on the card, such as "longsword".</summary>
        public string weaponLabel;

        public override float Current(Pawn pawn) => EchoDeeds.KillsWith(pawn, this);
        public override float Target => count;

        protected override string DefaultLabel => "AG_EchoTrialKillsWith".Translate(count,
            weaponLabel ?? (anyMelee ? "AG_EchoMeleeWeapon".Translate().ToString()
                : anyRanged ? "AG_EchoRangedWeapon".Translate().ToString()
                : weapons.Count > 0 ? weapons[0].label : "?"));

        public bool Counts(ThingDef weapon)
        {
            if (weapon == null) return false;
            if (anyMelee && weapon.IsMeleeWeapon) return true;
            if (anyRanged && weapon.IsRangedWeapon) return true;
            return weapons.Contains(weapon);
        }

        public override IEnumerable<string> ConfigErrors()
        {
            if (!anyMelee && !anyRanged && weapons.NullOrEmpty()) yield return "no weapons named";
        }
    }
}
