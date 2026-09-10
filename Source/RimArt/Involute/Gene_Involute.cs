using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The organ. One hole, normally on the carrier's body, leading to a volume that is not
    /// anywhere.
    ///
    /// State lives on the gene rather than on a hediff or a map component for the same reason
    /// the anchor organ's marks do: the hole belongs to a person. It saves and loads with them,
    /// it travels between maps with them, and it is gone the moment the gene is.
    ///
    /// The volume is generated lazily, on first connection, and never before. A pocket map that
    /// exists ticks forever whether or not anyone is standing in it, so a carrier who has never
    /// opened the hole costs nothing at all.
    /// </summary>
    public class Gene_Involute : Gene
    {
        /// <summary>
        /// Which part the hole is in. Rolled once when the gene lands and never re-rolled -
        /// not player-chosen, because given the choice the player picks the torso and the
        /// pass-through becomes immunity.
        /// </summary>
        private BodyPartRecord holePart;

        /// <summary>The volume. Null until the hole is first connected.</summary>
        private Map volume;

        /// <summary>
        /// The way back. Non-null exactly while the carrier is inside: they went through their
        /// own hole, so it stays where they left it.
        /// </summary>
        private Building_Aperture aperture;

        public InvoluteGeneExtension Ext => def.GetModExtension<InvoluteGeneExtension>();

        public BodyPartRecord HolePart => holePart;

        public Map Volume => volume;

        public Building_Aperture Aperture => aperture;

        /// <summary>The carrier is standing in their own volume.</summary>
        public bool Inside => volume != null && pawn != null && pawn.MapHeld == volume;

        public override void PostAdd()
        {
            base.PostAdd();
            EnsureHole();
        }

        /// <summary>
        /// Rolls the part and puts the hole in it. Split out of PostAdd because a gene added
        /// during pawn generation can land before the body exists, and because a save from
        /// before this ran should heal itself rather than stay holeless.
        /// </summary>
        public void EnsureHole()
        {
            if (pawn == null || pawn.health == null || pawn.RaceProps == null) return;
            if (holePart != null && !pawn.health.hediffSet.PartIsMissing(holePart))
            {
                if (!HoleHediffPresent()) AddHoleHediff();
                return;
            }

            holePart = InvoluteUtility.RollHolePart(pawn, Ext);
            if (holePart == null) return;

            AddHoleHediff();
        }

        private bool HoleHediffPresent()
        {
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i].def == InvoluteDefOf.AG_InvoluteHole && hediffs[i].Part == holePart) return true;
            }
            return false;
        }

        private void AddHoleHediff()
        {
            Hediff hediff = HediffMaker.MakeHediff(InvoluteDefOf.AG_InvoluteHole, pawn, holePart);
            pawn.health.AddHediff(hediff, holePart);
        }

        /// <summary>
        /// The volume, generating it if this is the first time anything has needed it.
        ///
        /// Returns null rather than throwing if generation fails: every caller is either an
        /// ability the player pressed or a damage instance passing through, and neither is a
        /// place to take the game down.
        /// </summary>
        public Map EnsureVolume()
        {
            if (volume != null) return volume;
            volume = InvoluteUtility.GenerateVolume(this);
            return volume;
        }

        /// <summary>Test hook only - see DebugActions_Involute. The roll happens once in play.</summary>
        public void ClearHolePart()
        {
            holePart = null;
        }

        /// <summary>
        /// Moves the hole. Bypasses the candidate filter entirely, so this will happily put one
        /// in a torso - see forceHolePart for why that is a test setting and not a balance one.
        /// </summary>
        public void SetHolePart(BodyPartRecord part)
        {
            if (part == null || pawn == null || pawn.health == null) return;

            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = hediffs.Count - 1; i >= 0; i--)
            {
                if (hediffs[i].def == InvoluteDefOf.AG_InvoluteHole) pawn.health.RemoveHediff(hediffs[i]);
            }

            holePart = part;
            AddHoleHediff();
        }

        public void SetAperture(Building_Aperture value)
        {
            aperture = value;
        }

        /// <summary>
        /// Destroys the volume and everything in it. The aperture goes with it - there is no
        /// longer anything on the far side for it to be a door to.
        /// </summary>
        public void CollapseVolume()
        {
            if (aperture != null)
            {
                if (!aperture.Destroyed) aperture.Destroy(DestroyMode.Vanish);
                aperture = null;
            }

            if (volume != null)
            {
                Map doomed = volume;
                volume = null;
                PocketMapUtility.DestroyPocketMap(doomed);
            }
        }

        public override void PostRemove()
        {
            base.PostRemove();

            if (pawn != null && holePart != null && pawn.health != null)
            {
                Hediff hole = pawn.health.hediffSet.GetFirstHediffOfDef(InvoluteDefOf.AG_InvoluteHole);
                if (hole != null) pawn.health.RemoveHediff(hole);
            }

            InvoluteRegistry.Drop(pawn);
            CollapseVolume();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_BodyParts.Look(ref holePart, "holePart");
            Scribe_References.Look(ref volume, "volume");
            Scribe_References.Look(ref aperture, "aperture");
        }
    }
}
