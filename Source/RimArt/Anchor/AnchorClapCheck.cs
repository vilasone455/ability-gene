using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The clap needs both hands. The gene normally stops the carrier equipping a weapon at all
    /// (see Patch_EquipmentUtility_CanEquip), but a pawn can arrive already holding one - born
    /// with a weapon from pawn generation, or given the gene later - so the cast checks too.
    /// </summary>
    public static class AnchorClapCheck
    {
        public static bool HandsFree(Pawn caster, bool throwMessages)
        {
            if (caster == null) return false;
            if (caster.equipment == null || caster.equipment.Primary == null) return true;

            if (throwMessages)
            {
                Messages.Message(
                    "AG_AnchorNoWeapon".Translate(caster.LabelShort, caster.equipment.Primary.LabelShort),
                    caster, MessageTypeDefOf.RejectInput, false);
            }
            return false;
        }
    }
}
