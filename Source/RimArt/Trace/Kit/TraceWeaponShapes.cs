using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// A weapon texture read the way make_trace_trial_textures.py reads the lab's reference weapons: the principal
    /// axis of its alpha gives the tip, the pommel and the width profile (<see cref="UbwWeapon"/>), and the alpha
    /// gives the outline (the trace wire) and the soft silhouette (the scan line), as added-light materials.
    /// Unlimited Blade Works builds its atlas from these (<see cref="UbwAtlasBuilder"/>); Trace On and
    /// Reinforcement draw on the weapon in the hand with them (<see cref="For"/>).
    ///
    /// The game's textures are not readable, so each is drawn into a render texture and read back. Game only: the
    /// lab's recorder draws with the reference set instead. Shapes are kept per texture; a texture that cannot be
    /// read is remembered and gives null.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class TraceWeaponShapes
    {
        internal const int Work = 256;

        private sealed class Read
        {
            public UbwWeapon W;
            public Material Wire, Mask;
        }

        private static readonly Dictionary<Texture, Read> reads = new Dictionary<Texture, Read>();
        private static readonly Dictionary<Material, TraceShape> shapes = new Dictionary<Material, TraceShape>();

        static TraceWeaponShapes()
        {
            TraceHandGraphics.Provider = LabStandIn;
        }

        /// <summary>The shape of <paramref name="weapon"/> as the game draws it (its own material, stuff colour and size), or null.</summary>
        public static TraceShape For(Thing weapon)
        {
            Graphic graphic = weapon?.Graphic;
            if (graphic == null) return null;
            Material face = graphic is Graphic_StackCount stack ? stack.SubGraphicForStackCount(1, weapon.def).MatSingleFor(weapon) : graphic.MatSingleFor(weapon);
            return For(face, graphic.drawSize.x, weapon.def.defName);
        }

        private static TraceShape For(Material face, float image, string name)
        {
            if (face == null) return null;
            if (shapes.TryGetValue(face, out TraceShape shape)) return shape;
            Read read = ReadOf(face.mainTexture as Texture2D, image, name);
            shape = read == null ? null : new TraceShape { W = read.W, Face = face, Wire = read.Wire, Mask = read.Mask, Colour = face.color };
            shapes[face] = shape;
            return shape;
        }

        private static Read ReadOf(Texture2D texture, float image, string name)
        {
            if (texture == null) return null;
            if (reads.TryGetValue(texture, out Read read)) return read;
            try
            {
                bool[] mask = MaskOf(ReadBack(texture, Work));
                if (mask != null)
                    read = new Read { W = Axis(mask, name, 0, image), Wire = Wire(mask, "Trace outline " + name), Mask = Silhouette(mask, "Trace mask " + name) };
            }
            catch (Exception e)
            {
                Log.Warning("[RimArt] Trace could not read the texture of " + name + ": " + e.Message);
                read = null;
            }
            reads[texture] = read;
            return read;
        }

        /// <summary>The previews in game: a vanilla weapon for each of the lab's reference names.</summary>
        private static TraceShape LabStandIn(string name)
        {
            string defName = name == "Knife" ? "MeleeWeapon_Knife" : name == "Spear" ? "MeleeWeapon_Spear"
                : name == "MonoSword" ? "MeleeWeapon_MonoSword" : "MeleeWeapon_LongSword";
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName) ?? DefDatabase<ThingDef>.GetNamedSilentFail("MeleeWeapon_LongSword");
            if (def?.graphic == null) return null;
            return For(def.graphic.MatSingle, def.graphicData.drawSize.x, def.defName);
        }

        // ---- reading a texture ---------------------------------------------------------------------------------

        /// <summary>The texture drawn at <paramref name="size"/> square and read back, rows from the bottom as Unity keeps them; null if it cannot be read.</summary>
        internal static Color32[] ReadBack(Texture2D source, int size)
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

        /// <summary>Which pixels of a <see cref="Work"/>-square read-back are the weapon (alpha over half); null if fewer than 64 are.</summary>
        internal static bool[] MaskOf(Color32[] pixels)
        {
            if (pixels == null) return null;
            var mask = new bool[Work * Work];
            int filled = 0;
            for (int i = 0; i < mask.Length; i++) if (mask[i] = pixels[i].a > 127) filled++;
            return filled < 64 ? null : mask;
        }

        /// <summary>Tip, pommel and width profile of the silhouette's principal axis, as make_trace_trial_textures.py's axis(): v up, the tip the end with the larger u + v.</summary>
        internal static UbwWeapon Axis(bool[] mask, string name, int row, double image)
        {
            var us = new List<double>();
            var vs = new List<double>();
            for (int y = 0; y < Work; y++)
                for (int x = 0; x < Work; x++)
                    if (mask[y * Work + x]) { us.Add((x + 0.5) / Work); vs.Add((y + 0.5) / Work); }
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
            return new UbwWeapon(name, row, image > 0 ? image : 1, tipU, tipV, pomU, pomV, width);
        }

        /// <summary>White line on the silhouette's edge plus a fainter inset line: the trace wire, as the script draws it.</summary>
        internal static Material Wire(bool[] mask, string name)
        {
            float[] m = ToAlpha(mask);
            float[] ring = Morph(m, 2, true), eroded = Morph(m, 1, false);
            for (int i = 0; i < ring.Length; i++) ring[i] = Mathf.Max(0f, ring[i] - eroded[i]);
            float[] inset = Morph(m, 6, false), insetEroded = Morph(inset, 1, false);
            for (int i = 0; i < ring.Length; i++) ring[i] = Mathf.Max(ring[i], Mathf.Max(0f, inset[i] - insetEroded[i]) * 0.45f);
            return Glow(Blur(ring, 0.2f), name);
        }

        /// <summary>The silhouette, softened a little: the scan line.</summary>
        internal static Material Silhouette(bool[] mask, string name) => Glow(Blur(ToAlpha(mask), 0.15f), name);

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
