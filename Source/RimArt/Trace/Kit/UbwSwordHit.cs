using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// A sword of Unlimited Blade Works hitting as its own weapon (Full Open's volley, Draw's flight). The world's swords
    /// are pictures of weapon defs (<see cref="UbwWeapon.Name"/> is the ThingDef's defName, read by UbwAtlasBuilder); the
    /// hit is that def's strongest Cut or Stab tool at its default stuff and Normal quality, with the caster as
    /// instigator and the weapon def named on the DamageInfo, as a melee hit has. There is no miss roll and no 0.8 to 1.2
    /// damage roll: the sword is already there.
    ///
    /// The numbers follow vanilla: Tool.AdjustedBaseMeleeDamageAmount(def, stuff, damageDef) for the damage, and
    /// VerbProperties.AdjustedArmorPenetration's rule for the penetration (Tool has no AdjustedArmorPenetration of its
    /// own): the tool's armorPenetration times the def's MeleeWeapon_DamageMultiplier, or 1.5 % of the damage when the
    /// tool sets none.
    /// </summary>
    public static class UbwSwordHit
    {
        /// <summary>One sword's hit, worked out once per weapon def.</summary>
        private struct Blow
        {
            public DamageDef Def;
            public float Amount, Penetration;
            public Tool Tool;
        }

        private static readonly Dictionary<ThingDef, Blow> blows = new Dictionary<ThingDef, Blow>();

        /// <summary>
        /// The weapon def a sword of the field is a picture of. The lab's reference set (used only when no game texture
        /// could be read) names weapons without the MeleeWeapon_ prefix; the longsword stands in for anything unknown.
        /// </summary>
        public static ThingDef WeaponOf(UbwWeapon w)
        {
            ThingDef def = w != null ? DefDatabase<ThingDef>.GetNamedSilentFail(w.Name) ?? DefDatabase<ThingDef>.GetNamedSilentFail("MeleeWeapon_" + w.Name) : null;
            return def != null && def.IsMeleeWeapon ? def : DefDatabase<ThingDef>.GetNamedSilentFail("MeleeWeapon_LongSword");
        }

        private static Blow BlowOf(ThingDef def)
        {
            if (def != null && blows.TryGetValue(def, out Blow known)) return known;
            var blow = new Blow { Def = DamageDefOf.Cut, Amount = 10f, Penetration = 0.15f };
            ThingDef stuff = def != null && def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
            if (def?.tools != null)
            {
                float best = -1f;
                foreach (Tool tool in def.tools)
                    foreach (ManeuverDef maneuver in tool.Maneuvers)
                    {
                        DamageDef damage = maneuver.verb?.meleeDamageDef;
                        if (damage != DamageDefOf.Cut && damage != DamageDefOf.Stab) continue;
                        float amount = tool.AdjustedBaseMeleeDamageAmount(def, stuff, damage);
                        if (amount <= best) continue;
                        best = amount;
                        float penetration = tool.armorPenetration >= 0f
                            ? tool.armorPenetration * def.GetStatValueAbstract(StatDefOf.MeleeWeapon_DamageMultiplier, stuff)
                            : amount * 0.015f;
                        blow = new Blow { Def = damage, Amount = amount, Penetration = penetration, Tool = tool };
                    }
            }
            if (def != null) blows[def] = blow;
            return blow;
        }

        /// <summary>For game tests and the log: "Cut 17 (AP 0.26)".</summary>
        public static string Describe(ThingDef weapon)
        {
            Blow b = BlowOf(weapon);
            return b.Def.label + " " + b.Amount.ToString("0.#") + " (AP " + b.Penetration.ToString("0.##") + ")";
        }

        /// <summary>
        /// <paramref name="foe"/> takes one hit of <paramref name="weapon"/> from <paramref name="caster"/>, coming along
        /// <paramref name="heading"/> (a direction on the floor).
        /// </summary>
        public static void Strike(Pawn caster, Pawn foe, ThingDef weapon, Vector2 heading)
        {
            if (foe == null || foe.Dead || !foe.Spawned) return;
            Blow b = BlowOf(weapon);
            bool guilty = caster == null || !caster.Drafted;
            var dinfo = new DamageInfo(b.Def, b.Amount, b.Penetration, -1f, caster, null, weapon, DamageInfo.SourceCategory.ThingOrUnknown, foe, guilty);
            dinfo.SetBodyRegion(BodyPartHeight.Undefined, BodyPartDepth.Outside);
            dinfo.SetWeaponQuality(QualityCategory.Normal);
            if (b.Tool != null) dinfo.SetTool(b.Tool);
            if (heading.sqrMagnitude > 1e-4f) dinfo.SetAngle(new Vector3(heading.x, 0f, heading.y));
            foe.TakeDamage(dinfo);
        }
    }
}
