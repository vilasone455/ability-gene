using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// One sprite, drawn at an angle, four times over.
    ///
    /// A blade falling, a blade standing, a blade in flight and a blade on its way to a hand are
    /// the same object seen at four moments, so they are one draw helper with different arguments
    /// rather than four graphics. The texture is Core's longsword - this mod ships no art.
    ///
    /// Transparent rather than Cutout, because three of the four moments fade.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class PanoplyGraphics
    {
        private static Material bladeMat;
        private static Material shadowMat;
        private static Material plantedMat;

        public static Material BladeMat
        {
            get
            {
                if (bladeMat == null)
                {
                    bladeMat = MaterialPool.MatFrom(
                        "RimArt/Panoply/Blade", ShaderDatabase.Transparent);
                }
                return bladeMat;
            }
        }

        /// <summary>Core's drop pod shadow. It is a soft ellipse and that is all it needs to be.</summary>
        public static Material ShadowMat
        {
            get
            {
                if (shadowMat == null)
                {
                    shadowMat = MaterialPool.MatFrom(
                        "Things/Skyfaller/SkyfallerShadowDropPod", ShaderDatabase.Transparent);
                }
                return shadowMat;
            }
        }

        /// <summary>
        /// The blade standing in the ground, seen from the side.
        ///
        /// This is the one thing in the mod that could not be borrowed. Every weapon texture in
        /// the game is a sword photographed from directly above while lying flat, and no
        /// rotation of a picture like that produces one standing up - the first attempt turned
        /// Core's longsword through every angle it had and produced a field of dropped swords
        /// every time. A side-on sprite with the point already buried is the only thing that
        /// reads, so this kit ships two textures. See make_textures.py.
        /// </summary>
        public static Material PlantedMat
        {
            get
            {
                if (plantedMat == null)
                {
                    plantedMat = MaterialPool.MatFrom(
                        "RimArt/Panoply/BladePlanted", ShaderDatabase.Transparent);
                }
                return plantedMat;
            }
        }

        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();

        /// <summary>
        /// A whole blade, pointing where it is told. <paramref name="heading"/> is a compass
        /// angle in the game's own terms - 0 north, 90 east - and the sprite's own heading is
        /// taken off here so callers pass a direction rather than a texture correction.
        /// </summary>
        public static void DrawBlade(Vector3 pos, float heading, float scale, float alpha)
        {
            // The sprite is drawn point-up, so a compass heading is the rotation, with no
            // texture correction in the way. That is most of the reason for drawing it.
            Draw(MeshPool.plane10, BladeMat, pos, heading, scale, new Color(1f, 1f, 1f, alpha));
        }

        /// <summary>
        /// A blade standing in the ground at <paramref name="groundPos"/>, with the point of it
        /// below the surface and a shadow pooled where it went in.
        ///
        /// The cut end is placed at the given cell rather than the middle of the sprite, so a
        /// blade leaning ten degrees pivots about the place it entered the ground instead of
        /// sliding sideways as it leans.
        /// </summary>
        public static void DrawPlantedBlade(Vector3 groundPos, float lean, float scale, float alpha)
        {
            Quaternion rotation = Quaternion.Euler(0f, lean, 0f);

            // The sprite's ground line is not its middle, so the picture is pushed along its own
            // axis until the hole in it sits on the cell. A blade leaning ten degrees then
            // pivots about the place it went in, instead of sliding out of its own hole.
            float groundOffset = 0.5f - PanoplyDefaults.PlantedGroundFraction;
            Vector3 pos = groundPos + rotation * new Vector3(0f, 0f, -groundOffset * scale);
            pos.y = AltitudeLayer.ItemImportant.AltitudeFor();

            Draw(MeshPool.plane10, PlantedMat, pos, lean, scale, new Color(1f, 1f, 1f, alpha));
        }

        /// <summary>
        /// The ground beneath a blade that is not on the ground yet. Grown as the blade comes
        /// down, which is the only cue a top-down camera has for height.
        /// </summary>
        public static void DrawShadow(Vector3 groundPos, float scale, float alpha)
        {
            DrawShadowAt(groundPos, scale, alpha);
        }

        private static void DrawShadowAt(Vector3 groundPos, float scale, float alpha)
        {
            Vector3 pos = groundPos;
            pos.y = AltitudeLayer.Shadows.AltitudeFor();
            Draw(MeshPool.plane10, ShadowMat, pos, 0f, scale, new Color(1f, 1f, 1f, alpha));
        }

        private static void Draw(Mesh mesh, Material material, Vector3 pos, float angle, float scale, Color colour)
        {
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, colour);

            Matrix4x4 matrix = default(Matrix4x4);
            matrix.SetTRS(pos, Quaternion.Euler(0f, angle, 0f), new Vector3(scale, 1f, scale));

            Graphics.DrawMesh(mesh, matrix, material, 0, null, 0, PropertyBlock);
        }
    }
}
