using UnityEngine;
using Verse;
using static RimArt.ThunderGodGraphics;

namespace RimArt
{
    /// <summary>
    /// Kamui's dimension on the Fold organ's pocket map: keeps the layout the map was generated from and
    /// draws the block field over its terrain with the lab's meshes (<see cref="KamuiGraphics"/>). It also
    /// answers where things arrive: Obito and allies at the middle of the main top, held enemies each on
    /// an island of their own.
    ///
    /// Every map has one of these (vanilla makes every MapComponent everywhere); it does nothing unless
    /// its map is a dimension (a seed is set by <see cref="GenStep_InvoluteVolume"/>) and is the map on
    /// screen. A volume made before the dimension existed has no seed and keeps its old look.
    /// </summary>
    public sealed class MapComponent_KamuiDimension : MapComponent
    {
        public int seed, islands = KamuiLayout.DefaultIslands;
        public double cover = KamuiLayout.DefaultCover;
        public string palette = KamuiGraphics.Fight;
        private KamuiLayout layout;
        private KamuiGraphics.KamuiField field;
        /// <summary>Under everything, for the far corners at full zoom-out (the generator def turns the grey map-edge frame off).</summary>
        private const float Backstop = 700f;

        public MapComponent_KamuiDimension(Map map) : base(map) { }

        public bool IsKamui => seed > 0;

        /// <summary>The layout this map was generated from; the same seed and settings give it back exactly.</summary>
        internal KamuiLayout Layout
        {
            get
            {
                if (layout != null || !IsKamui) return layout;
                layout = KamuiLayout.Generate(seed, System.Math.Min(map.Size.x, map.Size.z), cover, islands);
                return layout;
            }
        }

        /// <summary>Called by the GenStep: this map is the dimension for this seed and settings.</summary>
        public void Begin(int seed, double cover, int islands, string palette)
        {
            this.seed = seed;
            this.cover = cover;
            this.islands = islands;
            this.palette = palette;
            layout = null;
            field = null;
        }

        /// <summary>The middle of the main top, where Obito and allies arrive; invalid on a map that is not a dimension.</summary>
        public IntVec3 MouthCell => IsKamui ? new IntVec3(Layout.Mouth.x, 0, Layout.Mouth.z) : IntVec3.Invalid;

        /// <summary>
        /// Where the next held enemy lands: the landing cell of the first island nobody stands on; when
        /// every island is taken, a free cell on the first island that has one. Invalid when there is no
        /// island (a map that is not a dimension, or one with no room left on any island).
        /// </summary>
        public IntVec3 IslandLanding()
        {
            KamuiLayout l = Layout;
            if (l == null || l.Landings.Count == 0) return IntVec3.Invalid;
            for (int i = 0; i < l.Landings.Count; i++)
            {
                var cell = new IntVec3(l.Landings[i].x, 0, l.Landings[i].z);
                if (cell.Standable(map) && cell.GetFirstPawn(map) == null) return cell;
            }
            for (int i = 0; i < l.Landings.Count; i++)
            {
                var cell = new IntVec3(l.Landings[i].x, 0, l.Landings[i].z);
                int group = l.TopAt(cell.x, cell.z).Group;
                if (CellFinder.TryFindRandomCellNear(cell, map, 4,
                        c => c.Standable(map) && c.GetFirstPawn(map) == null && l.TopAt(c.x, c.z)?.Group == group, out IntVec3 found))
                    return found;
            }
            return IntVec3.Invalid;
        }

        public override void MapComponentUpdate()
        {
            if (!IsKamui || Find.CurrentMap != map) return;
            if (field == null) field = KamuiGraphics.KamuiField.Build(Layout);
            DrawMesh(MeshPool.plane10, new Vector2(map.Size.x / 2f, map.Size.z / 2f), KamuiLayers.Pocket.Back - 0.002f,
                Backstop, Backstop, 0f, KamuiGraphics.VoidOf(palette), solid);
            KamuiGraphics.Draw(Layout, field, Vector2.zero, KamuiLayers.Pocket, palette);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref seed, "kamuiSeed");
            Scribe_Values.Look(ref cover, "kamuiCover", KamuiLayout.DefaultCover);
            Scribe_Values.Look(ref islands, "kamuiIslands", KamuiLayout.DefaultIslands);
            Scribe_Values.Look(ref palette, "kamuiPalette", KamuiGraphics.Fight);
        }
    }
}
