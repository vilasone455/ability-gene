using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>Test shortcuts for the real weapon. The drawing previews are in DebugActions_ChainSickle.</summary>
    public static class DebugActions_ChainSickleKit
    {
        /// <summary>Puts a chain sickle in the clicked pawn's hands, dropping what it held.</summary>
        [RimArtDebug("Chain Sickle", "give chain sickle to pawn", RimArtDebugKind.Pawn)]
        private static void Give(Pawn pawn)
        {
            if (pawn.equipment == null)
            {
                Messages.Message("this pawn cannot hold a weapon", MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (pawn.equipment.Primary != null)
                pawn.equipment.TryDropEquipment(pawn.equipment.Primary, out _, pawn.Position, false);
            pawn.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(ChainSickleDefOf.AG_ChainSickle));
        }

        /// <summary>Drops a chain sickle on the clicked cell.</summary>
        [RimArtDebug("Chain Sickle", "spawn chain sickle on ground")]
        private static void Spawn() =>
            GenSpawn.Spawn(ThingMaker.MakeThing(ChainSickleDefOf.AG_ChainSickle), UI.MouseCell(), Find.CurrentMap);

        /// <summary>Says the clicked pawn's weight: mass, gear and what a 75 kg-carry holder's ratio would be.</summary>
        [RimArtDebug("Chain Sickle", "show weight of pawn", RimArtDebugKind.Pawn)]
        private static void Weight(Pawn pawn)
        {
            float body = pawn.GetStatValue(StatDefOf.Mass), gear = MassUtility.GearAndInventoryMass(pawn);
            Messages.Message(pawn.LabelShortCap + ": body " + body.ToString("0.#") + " kg + gear " + gear.ToString("0.#") + " kg = "
                + (body + gear).ToString("0.#") + " kg; carries " + ChainSickleCombat.Carry(pawn).ToString("0.#") + " kg.",
                pawn, MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>Ends the clicked holder's snag and pin.</summary>
        [RimArtDebug("Chain Sickle", "release snag", RimArtDebugKind.Pawn)]
        private static void Release(Pawn pawn)
        {
            CompChainSickle sickle = CompChainSickle.HeldBy(pawn);
            if (sickle?.snagged != null) MapComponent_ChainSickle.Release(pawn, sickle, "released (debug)");
        }
    }
}
