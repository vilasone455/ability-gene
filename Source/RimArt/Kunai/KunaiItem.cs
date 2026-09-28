using RimWorld;
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
    /// A planted kunai starts forbidden, so colonists leave it standing where it fell (a Flying Thunder
    /// God anchor) until the player allows it. Whatever takes it off the map (a haul, a reload, a
    /// pick-up) pulls it out and allows it again, so a carried or dropped kunai lies flat. A planted kunai never stacks: two misses into one cell stay two kunai, the second
    /// in the next cell. It shows no stack count.
    ///
    /// A kunai Minato threw carries his seal (<see cref="KunaiSeal"/>): it is drawn as his three-pronged
    /// kunai, flat or planted, is labelled as his, and stacks only with his.
    /// </summary>
    public class KunaiItem : ThingWithComps
    {
        public bool planted;

        /// <summary>Degrees clockwise from north: the way the kunai was flying when it went in.</summary>
        public float plantAngle;

        /// <summary>Where in its cell it went in, from the cell's centre (x, z).</summary>
        public Vector2 plantOffset;

        /// <summary>Minato's kunai, thrown in his hero form (<see cref="KunaiSeal"/>).</summary>
        public bool sealedByMinato;

        private static Material plantedMat, minatoPlantedMat;
        private static Graphic minatoGraphic;

        private Material PlantedMat => sealedByMinato
            ? minatoPlantedMat ??= PlantedMaterial(KunaiDefaults.MinatoPlantedTexture)
            : plantedMat ??= PlantedMaterial(KunaiDefaults.PlantedTexture);

        private static Material PlantedMaterial(string path) => GraphicDatabase.Get<Graphic_Single>(
            path, ShaderDatabase.Cutout, Vector2.one * KunaiDefaults.PlantedDrawSize, Color.white).MatSingle;

        public override Graphic Graphic => sealedByMinato
            ? minatoGraphic ??= GraphicDatabase.Get<Graphic_Single>(KunaiDefaults.MinatoTexture, ShaderDatabase.Cutout,
                def.graphicData.drawSize, Color.white)
            : base.Graphic;

        public override string LabelNoCount => sealedByMinato ? "Minato's kunai" : base.LabelNoCount;

        public override string DescriptionFlavor => sealedByMinato
            ? base.DescriptionFlavor + "\n\nThis one is Minato's: three-pronged, with his Flying Thunder God formula written down the handle. It keeps the seal until it is loaded into a belt."
            : base.DescriptionFlavor;

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
            !planted && other is KunaiItem kunai && !kunai.planted && kunai.sealedByMinato == sealedByMinato
            && base.CanStackWith(other);

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            // It was forbidden only because it stood in the ground; out of it, it is an ordinary kunai.
            if (planted) this.SetForbidden(false, warnOnFail: false);
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
            Scribe_Values.Look(ref sealedByMinato, "sealedByMinato");
        }
    }
}
