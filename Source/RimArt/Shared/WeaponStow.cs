using Verse;

namespace RimArt
{
    /// <summary>
    /// Empties a hand for another weapon: a bound weapon (a traced copy, a hero weapon, <see cref="BoundWeapon"/>) is
    /// handed to its owner, who breaks or removes it; any other weapon goes to the pawn's inventory, or to the ground
    /// at its feet when it has none. Used by Trace On's cast (<see cref="JobDriver_CastTrace"/>), its catch-all
    /// (<see cref="TraceCopies.ClearHands"/>), the Echo's manifest weapon (<see cref="EchoWeapon"/>) and Unlimited Blade
    /// Works' Draw and Arm.
    /// </summary>
    public static class WeaponStow
    {
        /// <summary>Where the weapon went.</summary>
        public enum Result { NotHeld, Gone, Inventory, Dropped }

        /// <summary>
        /// Takes <paramref name="held"/> out of <paramref name="pawn"/>'s hand. BoundWeapon.Leave is asked first because
        /// TryTransferEquipmentToContainer does not go through the drop patch: without it a copy would land in the
        /// inventory and break only at the next check, 30 ticks later. The drop is at the pawn's PositionHeld and only
        /// while it or its holder is spawned (a pawn in a container keeps the weapon in hand).
        /// </summary>
        public static Result Stow(Pawn pawn, ThingWithComps held)
        {
            if (held == null || pawn?.equipment == null || !pawn.equipment.Contains(held)) return Result.NotHeld;
            if (BoundWeapon.Leave(held)) return Result.Gone;
            if (pawn.inventory != null && pawn.equipment.TryTransferEquipmentToContainer(held, pawn.inventory.innerContainer)) return Result.Inventory;
            if (pawn.SpawnedOrAnyParentSpawned) pawn.equipment.TryDropEquipment(held, out _, pawn.PositionHeld, forbid: false);
            return Result.Dropped;
        }

        /// <summary>Takes whatever is in the primary hand out of it (<see cref="Stow(Pawn, ThingWithComps)"/>).</summary>
        public static Result Stow(Pawn pawn) => Stow(pawn, pawn?.equipment?.Primary);
    }
}
