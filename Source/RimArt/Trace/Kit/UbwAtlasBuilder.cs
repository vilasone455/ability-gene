using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Builds the world's weapon set from the weapons' own textures, at first use in game: the atlas that
    /// holds every weapon in every shade for the baked field, and per weapon its outline (the trace wire)
    /// and its silhouette (the scan line), with the same layout and shades as make_trace_trial_textures.py
    /// gives the lab's reference atlas. Each texture's tip, pommel and width profile are read from its alpha
    /// the way that script reads them, so the standing poses and cuts come from the same numbers.
    ///
    /// The reading itself (render-texture read-back, axis, outline, silhouette) is <see cref="TraceWeaponShapes"/>,
    /// which Trace On and Reinforcement use too. Game only: not linked into the lab's recorder, which draws with
    /// the lab's reference set instead.
    /// <see cref="Use"/> rebuilds the set from a list of weapons (the studied blades, once there is an
    /// ability); until then the set is Core's knife, longsword, spear, gladius and ikwa, and the monosword
    /// when Royalty is present.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class UbwAtlasBuilder
    {
        private static readonly string[] DefaultDefs = { "MeleeWeapon_Knife", "MeleeWeapon_LongSword", "MeleeWeapon_Spear", "MeleeWeapon_MonoSword", "MeleeWeapon_Gladius", "MeleeWeapon_Ikwa" };
        private const int Work = TraceWeaponShapes.Work, Inner = UbwGraphics.AtlasCell - 2 * UbwGraphics.AtlasPad;
        private static UbwWeaponSet built;
        private static List<ThingDef> wanted;
        private static bool failed;

        static UbwAtlasBuilder()
        {
            UbwGraphics.Provider = Current;
        }

        /// <summary>Draw the world with these weapons from now on (up to six with a readable single texture); the next draw rebuilds the set.</summary>
        public static void Use(List<ThingDef> weapons)
        {
            wanted = weapons;
            built = null;
            failed = false;
        }

        /// <summary>The set built from the weapons' textures, made on the first draw; null (the lab's set) if no texture could be read.</summary>
        private static UbwWeaponSet Current()
        {
            if (built != null || failed) return built;
            try
            {
                built = Build(wanted ?? Defaults());
            }
            catch (Exception e)
            {
                Log.Error("[RimArt] Unlimited Blade Works could not build its weapon atlas: " + e);
                built = null;
            }
            if (built == null) failed = true;
            return built;
        }

        private static List<ThingDef> Defaults()
        {
            var defs = new List<ThingDef>();
            foreach (string name in DefaultDefs)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (def != null) defs.Add(def);
            }
            return defs;
        }

        private sealed class Picture
        {
            public ThingDef Def;
            public bool[] Mask;
        }

        private static UbwWeaponSet Build(List<ThingDef> defs)
        {
            var pictures = new List<Picture>();
            foreach (ThingDef def in defs)
            {
                if (pictures.Count >= UbwGraphics.AtlasRows) break;
                string path = def?.graphicData?.texPath;
                if (path.NullOrEmpty()) continue;
                bool[] mask = TraceWeaponShapes.MaskOf(TraceWeaponShapes.ReadBack(ContentFinder<Texture2D>.Get(path, false), Work));
                if (mask != null) pictures.Add(new Picture { Def = def, Mask = mask });
            }
            if (pictures.Count == 0) return null;

            var set = new UbwWeaponSet { Weapons = new UbwWeapon[pictures.Count], Wire = new Material[pictures.Count], Mask = new Material[pictures.Count] };
            var atlas = new Color32[UbwGraphics.AtlasSide * UbwGraphics.AtlasSide];
            for (int row = 0; row < pictures.Count; row++)
            {
                Picture pic = pictures[row];
                float image = pic.Def.graphicData.drawSize.x > 0f ? pic.Def.graphicData.drawSize.x : 1f;
                set.Weapons[row] = TraceWeaponShapes.Axis(pic.Mask, pic.Def.defName, row, image);
                Color32[] small = TraceWeaponShapes.ReadBack(ContentFinder<Texture2D>.Get(pic.Def.graphicData.texPath, false), Inner);
                for (int col = 0; col < UbwGraphics.Shades.Length; col++) Place(atlas, small, col, row, UbwGraphics.Shades[col]);
                set.Wire[row] = TraceWeaponShapes.Wire(pic.Mask, "UBW outline " + pic.Def.defName);
                set.Mask[row] = TraceWeaponShapes.Silhouette(pic.Mask, "UBW mask " + pic.Def.defName);
            }
            Swatches(atlas);
            var texture = new Texture2D(UbwGraphics.AtlasSide, UbwGraphics.AtlasSide, TextureFormat.RGBA32, true) { name = "UBW atlas", filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, anisoLevel = 2 };
            texture.SetPixels32(atlas);
            texture.Apply(true, true);
            set.Atlas = MaterialPool.MatFrom(new MaterialRequest(texture, ShaderDatabase.Transparent));
            return set;
        }

        /// <summary>The picture, in its colour times <paramref name="shade"/>, into cell (col, row) of the atlas, inside the cell's padding.</summary>
        private static void Place(Color32[] atlas, Color32[] small, int col, int row, float shade)
        {
            if (small == null) return;
            int x0 = col * UbwGraphics.AtlasCell + UbwGraphics.AtlasPad, yTop = row * UbwGraphics.AtlasCell + UbwGraphics.AtlasPad;
            for (int y = 0; y < Inner; y++)
                for (int x = 0; x < Inner; x++)
                {
                    // The small picture's rows run from the bottom; row 0 of the atlas is its top.
                    Color32 p = small[(Inner - 1 - y) * Inner + x];
                    if (p.a < 8) p.a = 0;
                    Put(atlas, x0 + x, yTop + y, new Color32((byte)Mathf.RoundToInt(p.r * shade), (byte)Mathf.RoundToInt(p.g * shade), (byte)Mathf.RoundToInt(p.b * shade), p.a));
                }
        }

        /// <summary>A pixel of the atlas by column and row from the top of the image.</summary>
        private static void Put(Color32[] atlas, int x, int yFromTop, Color32 c) =>
            atlas[(UbwGraphics.AtlasSide - 1 - yFromTop) * UbwGraphics.AtlasSide + x] = c;

        /// <summary>Row 6: flat swatches for the ground marks: crack, slit, the three soils, the soft contact shadow, white.</summary>
        private static void Swatches(Color32[] atlas)
        {
            Color[] flat =
            {
                new Color(0f, 0f, 0f, 0.5f), new Color(0.05f, 0.04f, 0.03f, 0.92f), new Color(0.2f, 0.14f, 0.09f, 1f), new Color(0.36f, 0.27f, 0.18f, 1f),
                new Color(0.55f, 0.43f, 0.3f, 1f), Color.clear, new Color(1f, 1f, 1f, 1f),
            };
            for (int col = 0; col < flat.Length; col++)
            {
                int x0 = col * UbwGraphics.AtlasCell + UbwGraphics.AtlasPad, yTop = UbwGraphics.SwatchRow * UbwGraphics.AtlasCell + UbwGraphics.AtlasPad;
                for (int y = 0; y < Inner; y++)
                    for (int x = 0; x < Inner; x++)
                    {
                        Color c = flat[col];
                        if (col == UbwGraphics.SwContact)
                        {
                            // The soft disc's alpha at 0.34: (1 - r)^1.8 from the middle, as the lab's soft disc.
                            float r = Mathf.Sqrt(Mathf.Pow((x + 0.5f) / Inner - 0.5f, 2f) + Mathf.Pow((y + 0.5f) / Inner - 0.5f, 2f)) * 2f;
                            c = new Color(0f, 0f, 0f, Mathf.Pow(Mathf.Max(0f, 1f - r), 1.8f) * 0.34f);
                        }
                        Put(atlas, x0 + x, yTop + y, c);
                    }
            }
        }
    }
}
