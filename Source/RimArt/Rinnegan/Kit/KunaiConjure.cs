using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Sasuke's kunai supply (rinnegan-amenoyodomi.js header, agreed 2026-09-27). While he is manifested his worn kunai
    /// belt regains one kunai at a time (<see cref="Ability_Amenoyodomi"/>). Those kunai are conjured: they are thrown
    /// first, fly as AG_KunaiProjectileConjured, and never become items. Where an ordinary kunai would land, plant or
    /// drop, a conjured one vanishes; one stuck in a pawn vanishes when it is pulled out or the body is left. A belt
    /// that leaves a manifested Sasuke loses the conjured kunai it still holds (<see cref="GameComponent_Rinnegan"/>).
    /// Real kunai from the colony's stock stay real.
    /// </summary>
    public static class KunaiConjure
    {
        private static readonly AccessTools.FieldRef<CompApparelVerbOwner_Charged, int> Charges =
            AccessTools.FieldRefAccess<CompApparelVerbOwner_Charged, int>("remainingCharges");

        /// <summary>
        /// True while a conjured kunai's impact runs (<see cref="Projectile_KunaiConjured"/>): KunaiEmbedding makes no
        /// item then, and a kunai it sticks into a pawn is marked conjured.
        /// </summary>
        public static bool Impacting { get; internal set; }

        /// <summary>One conjured kunai onto this belt.</summary>
        public static void AddOne(CompApparelReloadable belt)
        {
            if (belt == null || belt.RemainingCharges >= belt.MaxCharges) return;
            Charges(belt)++;
            GameComponent_Rinnegan.Instance?.AddConjured(belt.parent);
        }

        /// <summary>
        /// Called by the throw before it spends a charge: true when the kunai thrown is conjured (conjured ones go
        /// first, so the colony's real kunai stay on the belt longest).
        /// </summary>
        public static bool TakeOne(CompApparelReloadable belt)
        {
            if (belt == null) return false;
            return GameComponent_Rinnegan.Instance?.TakeConjured(belt.parent, belt.RemainingCharges) ?? false;
        }

        public static void RemoveCharges(CompApparelReloadable belt, int count)
        {
            if (belt == null || count <= 0) return;
            Charges(belt) = Mathf.Max(0, belt.RemainingCharges - count);
        }

        /// <summary>The projectile the throw launches: a conjured kunai when <paramref name="conjured"/>.</summary>
        public static ThingDef Projectile(ThingDef normal, bool conjured) => conjured ? SasukeDefOf.AG_KunaiProjectileConjured : normal;

        /// <summary>Where a conjured kunai comes to rest it is gone: a little lavender dust.</summary>
        public static void Vanish(Vector3 at, Map map)
        {
            if (map == null || !at.ShouldSpawnMotesAt(map)) return;
            RinneganPictures.Vanish(at, map);
        }
    }

    /// <summary>A kunai the Rinnegan conjured: everything an ordinary kunai does, except that it never becomes an item.</summary>
    public class Projectile_KunaiConjured : Projectile_Kunai
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            bool was = KunaiConjure.Impacting;
            KunaiConjure.Impacting = true;
            try
            {
                base.Impact(hitThing, blockedByShield);
            }
            finally
            {
                KunaiConjure.Impacting = was;
            }
        }
    }
}
