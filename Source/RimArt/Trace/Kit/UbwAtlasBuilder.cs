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
    /// The game's textures are not readable, so each is drawn into a render texture and read back. Game
    /// only: not linked into the lab's recorder, which draws with the lab's reference set instead.
    /// <see cref="Use"/> rebuilds the set from a list of weapons (the studied blades, once there is an
    /// ability); until then the set is Core's knife, longsword, spear, gladius and ikwa, and the monosword
    /// when Royalty is present.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class UbwAtlasBuilder
    {
        private static readonly string[] DefaultDefs = { "MeleeWeapon_Knife", "MeleeWeapon_LongSword", "MeleeWeapon_Spear", "MeleeWeapon_MonoSword", "MeleeWeapon_Gladius", "MeleeWeapon_Ikwa" };
        private const int Work = 256, Inner = UbwGraphics.AtlasCell - 2 * UbwGraphics.AtlasPad;
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
            public Color32[] Pixels;
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
                Texture2D source = ContentFinder<Texture2D>.Get(path, false);
                if (source == null) continue;
                Color32[] pixels = ReadBack(source, Work);
                if (pixels == null) continue;
                var mask = new bool[Work * Work];
                int filled = 0;
                for (int i = 0; i < mask.Length; i++) if (mask[i] = pixels[i].a > 127) filled++;
                if (filled < 64) continue;
                pictures.Add(new Picture { Def = def, Pixels = pixels, Mask = mask });
            }
            if (pictures.Count == 0) return null;

            var set = new UbwWeaponSet { Weapons = new UbwWeapon[pictures.Count], Wire = new Material[pictures.Count], Mask = new Material[pictures.Count] };
            var atlas = new Color32[UbwGraphics.AtlasSide * UbwGraphics.AtlasSide];
            for (int row = 0; row < pictures.Count; row++)
            {
                Picture pic = pictures[row];
                set.Weapons[row] = Axis(pic, row);
                Color32[] small = ReadBack(ContentFinder<Texture2D>.Get(pic.Def.graphicData.texPath, false), Inner);
                for (int col = 0; col < UbwGraphics.Shades.Length; col++) Place(atlas, small, col, row, UbwGraphics.Shades[col]);
                set.Wire[row] = Glow(Outline(pic.Mask), "UBW outline " + pic.Def.defName);
                set.Mask[row] = Glow(Blur(ToAlpha(pic.Mask), 0.15f), "UBW mask " + pic.Def.defName);
            }
            Swatches(atlas);
            var texture = new Texture2D(UbwGraphics.AtlasSide, UbwGraphics.AtlasSide, TextureFormat.RGBA32, true) { name = "UBW atlas", filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, anisoLevel = 2 };
            texture.SetPixels32(atlas);
            texture.Apply(true, true);
            set.Atlas = MaterialPool.MatFrom(new MaterialRequest(texture, ShaderDatabase.Transparent));
            return set;
        }

        /// <summary>The texture drawn at <paramref name="size"/> square and read back, rows from the bottom as Unity keeps them; null if it cannot be read.</summary>
        private static Color32[] ReadBack(Texture2D source, int size)
        {
            if (source == null) return null;
            RenderTexture target = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            RenderTexture previous = RenderTexture.active;
            Texture2D copy = null;
            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                copy = new Texture2D(size, size, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
                copy.Apply();
                return copy.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                if (copy != null) UnityEngine.Object.Destroy(copy);
            }
        }

        /// <summary>Tip, pommel and width profile of the silhouette's principal axis, as make_trace_trial_textures.py's axis(): v up, the tip the end with the larger u + v.</summary>
        private static UbwWeapon Axis(Picture pic, int row)
        {
            var us = new List<double>();
            var vs = new List<double>();
            for (int y = 0; y < Work; y++)
                for (int x = 0; x < Work; x++)
                    if (pic.Mask[y * Work + x]) { us.Add((x + 0.5) / Work); vs.Add((y + 0.5) / Work); }
            int n = us.Count;
            double mu = 0, mv = 0;
            for (int i = 0; i < n; i++) { mu += us[i]; mv += vs[i]; }
            mu /= n; mv /= n;
            double sxx = 0, syy = 0, sxy = 0;
            for (int i = 0; i < n; i++)
            {
                double du = us[i] - mu, dv = vs[i] - mv;
                sxx += du * du; syy += dv * dv; sxy += du * dv;
            }
            sxx /= n; syy /= n; sxy /= n;
            double ang = 0.5 * Math.Atan2(2 * sxy, sxx - syy), ax = Math.Cos(ang), av = Math.Sin(ang);
            double lo = double.MaxValue, hi = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                double s = (us[i] - mu) * ax + (vs[i] - mv) * av;
                if (s < lo) lo = s;
                if (s > hi) hi = s;
            }
            double e1u = mu + ax * lo, e1v = mv + av * lo, e2u = mu + ax * hi, e2v = mv + av * hi;
            bool firstIsTip = e1u + e1v > e2u + e2v;
            double tipU = firstIsTip ? e1u : e2u, tipV = firstIsTip ? e1v : e2v, pomU = firstIsTip ? e2u : e1u, pomV = firstIsTip ? e2v : e1v;
            double length = Math.Sqrt((pomU - tipU) * (pomU - tipU) + (pomV - tipV) * (pomV - tipV));
            double alongU = (pomU - tipU) / length, alongV = (pomV - tipV) / length, acrossU = -alongV, acrossV = alongU;
            var low = new double[16];
            var high = new double[16];
            for (int k = 0; k < 16; k++) { low[k] = 9; high[k] = -9; }
            for (int i = 0; i < n; i++)
            {
                double du = us[i] - tipU, dv = vs[i] - tipV;
                int k = Math.Min(15, Math.Max(0, (int)((du * alongU + dv * alongV) / length * 16)));
                double c = du * acrossU + dv * acrossV;
                if (c < low[k]) low[k] = c;
                if (c > high[k]) high[k] = c;
            }
            var width = new double[32];
            for (int k = 0; k < 16; k++)
            {
                width[k * 2] = high[k] > -9 ? low[k] : 0;
                width[k * 2 + 1] = high[k] > -9 ? high[k] : 0;
            }
            float image = pic.Def.graphicData.drawSize.x > 0f ? pic.Def.graphicData.drawSize.x : 1f;
            return new UbwWeapon(pic.Def.defName, row, image, tipU, tipV, pomU, pomV, width);
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

        // ---- the outline and the silhouette, at Work square ----------------------------------------------------------

        private static float[] ToAlpha(bool[] mask)
        {
            var a = new float[mask.Length];
            for (int i = 0; i < a.Length; i++) a[i] = mask[i] ? 1f : 0f;
            return a;
        }

        /// <summary>A square min or max filter of <paramref name="radius"/> pixels, separable.</summary>
        private static float[] Morph(float[] src, int radius, bool max)
        {
            var mid = new float[src.Length];
            var dst = new float[src.Length];
            for (int y = 0; y < Work; y++)
                for (int x = 0; x < Work; x++)
                {
                    float v = src[y * Work + x];
                    for (int k = -radius; k <= radius; k++)
                    {
                        int xx = Mathf.Clamp(x + k, 0, Work - 1);
                        float s = src[y * Work + xx];
                        v = max ? Mathf.Max(v, s) : Mathf.Min(v, s);
                    }
                    mid[y * Work + x] = v;
                }
            for (int y = 0; y < Work; y++)
                for (int x = 0; x < Work; x++)
                {
                    float v = mid[y * Work + x];
                    for (int k = -radius; k <= radius; k++)
                    {
                        int yy = Mathf.Clamp(y + k, 0, Work - 1);
                        float s = mid[yy * Work + x];
                        v = max ? Mathf.Max(v, s) : Mathf.Min(v, s);
                    }
                    dst[y * Work + x] = v;
                }
            return dst;
        }

        /// <summary>A small blur: one pass of a 3-tap kernel each way, <paramref name="side"/> the weight of each neighbour.</summary>
        private static float[] Blur(float[] src, float side)
        {
            float centre = 1f - 2f * side;
            var mid = new float[src.Length];
            var dst = new float[src.Length];
            for (int y = 0; y < Work; y++)
                for (int x = 0; x < Work; x++)
                    mid[y * Work + x] = src[y * Work + Mathf.Max(0, x - 1)] * side + src[y * Work + x] * centre + src[y * Work + Mathf.Min(Work - 1, x + 1)] * side;
            for (int y = 0; y < Work; y++)
                for (int x = 0; x < Work; x++)
                    dst[y * Work + x] = mid[Mathf.Max(0, y - 1) * Work + x] * side + mid[y * Work + x] * centre + mid[Mathf.Min(Work - 1, y + 1) * Work + x] * side;
            return dst;
        }

        /// <summary>White line on the silhouette's edge plus a fainter inset line: the trace wire, as the script draws it.</summary>
        private static float[] Outline(bool[] mask)
        {
            float[] m = ToAlpha(mask);
            float[] ring = Morph(m, 2, true), eroded = Morph(m, 1, false);
            for (int i = 0; i < ring.Length; i++) ring[i] = Mathf.Max(0f, ring[i] - eroded[i]);
            float[] inset = Morph(m, 6, false), insetEroded = Morph(inset, 1, false);
            for (int i = 0; i < ring.Length; i++) ring[i] = Mathf.Max(ring[i], Mathf.Max(0f, inset[i] - insetEroded[i]) * 0.45f);
            return Blur(ring, 0.2f);
        }

        /// <summary>White pixels with the given alpha, outermost pixels clear, as an added-light material.</summary>
        private static Material Glow(float[] alpha, string name)
        {
            var pixels = new Color32[Work * Work];
            for (int y = 0; y < Work; y++)
                for (int x = 0; x < Work; x++)
                {
                    bool edge = x == 0 || y == 0 || x == Work - 1 || y == Work - 1;
                    pixels[y * Work + x] = new Color32(255, 255, 255, edge ? (byte)0 : (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha[y * Work + x]) * 255f));
                }
            var texture = new Texture2D(Work, Work, TextureFormat.RGBA32, true) { name = name, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, anisoLevel = 2 };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return MaterialPool.MatFrom(new MaterialRequest(texture, ShaderDatabase.MoteGlow));
        }
    }
}
