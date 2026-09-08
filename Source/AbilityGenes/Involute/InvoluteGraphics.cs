using UnityEngine;
using Verse;

namespace AbilityGenes
{
    /// <summary>
    /// Three layers, because a hole is not a glow.
    ///
    /// The first attempt drew the skip texture under MoteGlow alone and was nearly invisible in
    /// play. MoteGlow is additive: it adds light to what is behind it, which is right for a
    /// muzzle flash and exactly wrong for an opening, because the one thing an opening does is
    /// let less light through than its surroundings. What reads as a hole at any zoom, on any
    /// ground, is a dark centre with a bright edge - so the void is drawn first and opaque, and
    /// only the rim and the swirl are allowed to glow.
    ///
    /// Nothing here may be touched during def parsing - see <see cref="InvoluteDefaults"/>. The
    /// materials resolve lazily so that even if something reaches this class early, the textures
    /// are only fetched from the draw path, which is always the main thread.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class InvoluteGraphics
    {
        private static Material voidMat;
        private static Material rimMat;
        private static Material swirlMat;

        /// <summary>The hole itself. Opaque, not additive - this is the layer that subtracts.</summary>
        public static Material VoidMat
        {
            get
            {
                if (voidMat == null)
                {
                    voidMat = MaterialPool.MatFrom("Things/Mote/Black", ShaderDatabase.Transparent);
                }
                return voidMat;
            }
        }

        /// <summary>The edge. Core's skip flash, which is already a bright ring.</summary>
        public static Material RimMat
        {
            get
            {
                if (rimMat == null)
                {
                    rimMat = MaterialPool.MatFrom("Things/Mote/PsycastSkipFlash", ShaderDatabase.MoteGlow);
                }
                return rimMat;
            }
        }

        /// <summary>What is on the far side. Core's skip interior, turning slowly.</summary>
        public static Material SwirlMat
        {
            get
            {
                if (swirlMat == null)
                {
                    swirlMat = MaterialPool.MatFrom("Things/Mote/SkipInnerDimension", ShaderDatabase.MoteGlow);
                }
                return swirlMat;
            }
        }

        public static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();

        /// <summary>
        /// Draws the three layers at a point. Shared so the aperture standing on the ground and
        /// the hole riding on a body are the same object at two sizes, which is what they are.
        /// </summary>
        public static void Draw(Vector3 centre, float scale, float alpha)
        {
            float pulse = 0.78f + 0.22f * Mathf.Sin(Time.realtimeSinceStartup * 2.4f);
            float spin = Time.realtimeSinceStartup * 22f;

            Vector3 pos = centre;
            pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();

            DrawLayer(VoidMat, pos, scale * InvoluteDefaults.VoidScale, 0f,
                Mult(InvoluteDefaults.VoidColor, alpha));

            pos.y += 0.02f;
            DrawLayer(RimMat, pos, scale * InvoluteDefaults.RimScale, 0f,
                Mult(InvoluteDefaults.RimColor, alpha * pulse));

            pos.y += 0.02f;
            DrawLayer(SwirlMat, pos, scale * InvoluteDefaults.SwirlScale, spin,
                Mult(InvoluteDefaults.SwirlColor, alpha * pulse));
        }

        private static Color Mult(Color colour, float alpha)
        {
            colour.a *= alpha;
            return colour;
        }

        private static void DrawLayer(Material material, Vector3 pos, float scale, float angle, Color colour)
        {
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, colour);

            Matrix4x4 matrix = default(Matrix4x4);
            matrix.SetTRS(pos, Quaternion.Euler(0f, angle, 0f), new Vector3(scale, 1f, scale));

            Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0, null, 0, PropertyBlock);
        }
    }
}
