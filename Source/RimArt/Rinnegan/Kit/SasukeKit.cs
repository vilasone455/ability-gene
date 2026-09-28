using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    [DefOf]
    public static class SasukeDefOf
    {
        public static EchoDef AG_Echo_Sasuke;

        public static AbilityDef AG_SasukeAmenoyodomi;
        public static AbilityDef AG_SasukeAmenotejikara;
        public static AbilityDef AG_SasukeRaikoKusari;
        public static AbilityDef AG_SasukeAmaterasu;

        public static HediffDef AG_EchoManifest_Sasuke;
        public static HediffDef AG_AmaterasuFlame;
        public static HediffDef AG_BleedingEye;
        public static HediffDef AG_AmaterasuBlind;

        /// <summary>A kunai conjured onto Sasuke's belt: never becomes an item (<see cref="KunaiConjure"/>).</summary>
        public static ThingDef AG_KunaiProjectileConjured;

        static SasukeDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SasukeDefOf));
        }
    }

    /// <summary>What Sasuke's four abilities share.</summary>
    public static class SasukeKit
    {
        public static CompProperties_Amenoyodomi HoldProps => VergilKit.Props<CompProperties_Amenoyodomi>(SasukeDefOf.AG_SasukeAmenoyodomi);
        public static CompProperties_RaikoKusari NetProps => VergilKit.Props<CompProperties_RaikoKusari>(SasukeDefOf.AG_SasukeRaikoKusari);
        public static CompProperties_Amaterasu FlameProps => VergilKit.Props<CompProperties_Amaterasu>(SasukeDefOf.AG_SasukeAmaterasu);

        /// <summary>This pawn's Amenoyodomi, while it is Sasuke's manifested Host.</summary>
        public static Ability_Amenoyodomi Amenoyodomi(Pawn pawn) =>
            pawn?.abilities?.GetAbility(SasukeDefOf.AG_SasukeAmenoyodomi) as Ability_Amenoyodomi;

        /// <summary>Amenoyodomi is on (hang or drift) for this pawn.</summary>
        public static bool Holding(Pawn pawn)
        {
            Ability_Amenoyodomi ability = Amenoyodomi(pawn);
            return ability != null && ability.mode != HoldMode.Off;
        }

        /// <summary>A manifested Sasuke: never catches Amaterasu and is never caught by his own net.</summary>
        public static bool IsSasuke(Pawn pawn) => pawn?.abilities?.GetAbility(SasukeDefOf.AG_SasukeAmaterasu) != null;

        /// <summary>
        /// The caster can keep weapons in the air: alive, awake, not downed, on <paramref name="map"/>. Held weapons drop
        /// the moment this fails.
        /// </summary>
        public static bool CanHold(Pawn caster, Map map) =>
            caster != null && !caster.Dead && caster.Spawned && caster.Map == map && !caster.Downed && caster.Awake();

        public static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
