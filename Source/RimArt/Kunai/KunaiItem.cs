using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The kunai item (AG_Kunai). A thrown kunai that misses and comes down on open ground is planted:
    /// it stands in the ground at the angle it flew, drawn with <see cref="KunaiDefaults.PlantedTexture"/>
    /// instead of lying flat (<see cref="KunaiEmbedding.PlantKunai"/>). Otherwise it is the ordinary
    /// item: hauled, loaded into the belt, a Flying Thunder God anchor on the ground.
    ///
    /// Whatever takes it off the map (a haul, a reload, a pick-up) pulls it out, so a carried or dropped
    /// kunai lies flat. A planted kunai never stacks: two misses into one cell stay two kunai, the second
    /// in the next cell. It shows no stack count.
    /// </summary>
    public class KunaiItem : ThingWithComps
    {
        public bool planted;

        /// <summary>Degrees clockwise from north: the way the kunai was flying when it went in.</summary>
        public float plantAngle;

        /// <summary>Where in its cell it went in, from the cell's centre (x, z).</summary>
        public Vector2 plantOffset;

        private static Material plantedMat;

        private static Material PlantedMat => plantedMat ??= GraphicDatabase.Get<Graphic_Single>(
            KunaiDefaults.PlantedTexture, ShaderDatabase.Cutout, Vector2.one * KunaiDefaults.PlantedDrawSize, Color.white).MatSingle;

        public override void Print(SectionLayer layer)
        {
            if (!planted)
            {
                base.Print(layer);
                return;
            }
            Vector3 at = DrawPos + new Vector3(plantOffset.x, 0f, plantOffset.y);
            Printer_Plane.PrintPlane(layer, at, Vector2.one * KunaiDefaults.PlantedDrawSize, PlantedMat, plantAngle);
        }

        /// <summary>No stack count under a planted kunai: it is always one, and the label hides the ground round it.</summary>
        public override void DrawGUIOverlay()
        {
            if (!planted) base.DrawGUIOverlay();
        }

        public override bool CanStackWith(Thing other) =>
            !planted && !(other is KunaiItem kunai && kunai.planted) && base.CanStackWith(other);

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            planted = false;
            plantOffset = Vector2.zero;
            base.DeSpawn(mode);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref planted, "planted");
            Scribe_Values.Look(ref plantAngle, "plantAngle");
            Scribe_Values.Look(ref plantOffset, "plantOffset");
        }
    }
}
