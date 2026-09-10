using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// A blade coming out of the ground and going to a hand.
    ///
    /// The pattern is vanilla's: a skyfaller and a pawn flyer are both a thing in the air that
    /// is holding something and hands it over when it arrives. This one holds nothing on the
    /// way, because what it delivers is ordinary steel that did not exist while it was flying -
    /// see <see cref="Deliver"/> for why that is the honest version.
    ///
    /// RimWorld pawns have no arms to catch with, so the catch is what the game can actually
    /// show: the blade arrives, and the weapon is in the hand on the next frame.
    /// </summary>
    public class BladeFlight : ThingWithComps
    {
        private Pawn owner;
        private Pawn recipient;
        private Vector3 start;
        private int ticksLeft;
        private int totalTicks = PanoplyDefaults.GraspMinTicks;

        public void Configure(Pawn newOwner, Pawn newRecipient, Vector3 from)
        {
            owner = newOwner;
            recipient = newRecipient;
            start = from;

            float distance = (newRecipient.DrawPos - from).MagnitudeHorizontal();
            totalTicks = Mathf.Clamp(
                Mathf.RoundToInt(distance * PanoplyDefaults.GraspTicksPerCell),
                PanoplyDefaults.GraspMinTicks, PanoplyDefaults.GraspMaxTicks);
            ticksLeft = totalTicks;
        }

        private Vector3 Destination
        {
            get
            {
                if (recipient != null && recipient.Spawned) return recipient.DrawPos;
                return DrawPos;
            }
        }

        protected override void Tick()
        {
            base.Tick();

            ticksLeft--;
            if (ticksLeft <= 0) Deliver();
        }

        /// <summary>
        /// What lands in the hand is a real steel longsword.
        ///
        /// The blade in the ground was never an item - it could not be picked up, hauled or
        /// traded - so this is the one moment the gene puts real matter into the world, and it
        /// puts it there permanently. That is the trade grasp is making: a blade taken out of
        /// the field stops costing the carrier anything and starts being a weapon somebody owns.
        ///
        /// A hand that is already full is refused at targeting time rather than emptied here.
        /// Silently dropping a colonist's rifle to give them a sword is the kind of help nobody
        /// asked for.
        /// </summary>
        private void Deliver()
        {
            Map map = Map;
            IntVec3 cell = Position;

            if (map == null || recipient == null || recipient.Destroyed)
            {
                Destroy(DestroyMode.Vanish);
                return;
            }

            ThingWithComps sword = (ThingWithComps)ThingMaker.MakeThing(
                PanoplyDefOf.MeleeWeapon_LongSword, ThingDefOf.Steel);

            CompQuality quality = sword.TryGetComp<CompQuality>();
            if (quality != null) quality.SetQuality(QualityCategory.Normal, ArtGenerationContext.Colony);

            PanoplyUtility.ImpactEffect(recipient.Position, map);

            if (PanoplyGrasp.CanTakeBlade(recipient))
            {
                recipient.equipment.AddEquipment(sword);
            }
            else
            {
                GenPlace.TryPlaceThing(sword, recipient.Position, map, ThingPlaceMode.Near);
            }

            Destroy(DestroyMode.Vanish);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            float progress = 1f - Mathf.Clamp01(ticksLeft / (float)totalTicks);

            Vector3 end = Destination;
            Vector3 pos = Vector3.Lerp(start, end, progress);

            // An arc, so it reads as thrown rather than dragged along the floor.
            pos.z += Mathf.Sin(progress * Mathf.PI) * PanoplyDefaults.GraspArcHeight;
            pos.y = AltitudeLayer.Skyfaller.AltitudeFor();

            float heading = (end - start).AngleFlat();

            PanoplyGraphics.DrawShadow(Vector3.Lerp(start, end, progress), 0.5f, 0.3f);
            PanoplyGraphics.DrawBlade(pos, heading, PanoplyDefaults.BladeDrawSize, 1f);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref owner, "owner");
            Scribe_References.Look(ref recipient, "recipient");
            Scribe_Values.Look(ref start, "start");
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", 0);
            Scribe_Values.Look(ref totalTicks, "totalTicks", PanoplyDefaults.GraspMinTicks);
        }
    }
}
