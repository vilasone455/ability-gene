using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The flock, and the only thing this gene draws.
    ///
    /// The crows are this mod's own sprites rather than Odyssey's. That is not a licensing
    /// dodge - a texture referenced by path is loaded out of the player's own game files and
    /// never redistributed, and the arc tendon already leans on another mod that way. It is
    /// that Odyssey's Crow_Flying frames are a naturalistic bird, lit, with visible primaries,
    /// and what belongs here is a cutout. The thing being drawn is not a crow. It is a person
    /// who is currently not present, wearing the shape of some.
    ///
    /// Each frame is drawn pointing north, because that is what an unrotated RimWorld sprite is
    /// assumed to do, and the quad is turned to the bird's own heading. Every bird in a flock
    /// has a different heading - they are not in formation - which is the reason this could not
    /// be one of Core's line motes and had to be a rotated quad per bird.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class DispersalGraphics
    {
        /// <summary>
        /// Materials cached by frame and by quantised alpha, for the same reason
        /// <see cref="ArcGraphics"/> caches its streak: the fade is baked into the material
        /// rather than pushed through a property block, so it cannot depend on whether a given
        /// shader honours one. Four frames at eight steps is thirty-six materials once.
        /// </summary>
        private static readonly Material[,] cache =
            new Material[DispersalDefaults.FrameCount, DispersalDefaults.AlphaSteps + 1];

        private static readonly Material[] feeding = new Material[4];

        public static void DrawFeedingCrow(Vector3 position, float heading, int frame, float alpha)
        {
            if (alpha <= 0f) return;
            frame = Mathf.Clamp(frame, 0, feeding.Length - 1);
            if (feeding[frame] == null)
                feeding[frame] = MaterialPool.MatFrom("RimArt/Dispersal/CrowFeed" + frame,
                    ShaderDatabase.Mote);
            position.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            Matrix4x4 matrix = default(Matrix4x4);
            matrix.SetTRS(position, Quaternion.Euler(0f, heading, 0f),
                new Vector3(0.65f, 1f, 0.65f));
            Graphics.DrawMesh(MeshPool.plane10, matrix, feeding[frame], 0);
        }

        private static Material Crow(int frame, float alpha)
        {
            frame = Mathf.Clamp(frame, 0, DispersalDefaults.FrameCount - 1);
            int step = Mathf.Clamp(Mathf.RoundToInt(alpha * DispersalDefaults.AlphaSteps),
                0, DispersalDefaults.AlphaSteps);

            Material material = cache[frame, step];
            if (material == null)
            {
                Color colour = new Color(1f, 1f, 1f, step / (float)DispersalDefaults.AlphaSteps);

                // Mote rather than MoteGlow: MoteGlow is additive, and an additive black bird
                // is nothing at all.
                material = MaterialPool.MatFrom("RimArt/Dispersal/Crow" + frame,
                    ShaderDatabase.Mote, colour);
                cache[frame, step] = material;
            }
            return material;
        }

        /// <summary>One bird, at a point on its own route, facing the way it is going.</summary>
        public static void DrawCrow(Vector3 position, float heading, int frame, float alpha)
        {
            if (alpha <= 0f) return;

            position.y = AltitudeLayer.MoteOverhead.AltitudeFor();

            Matrix4x4 matrix = default(Matrix4x4);
            matrix.SetTRS(position, Quaternion.Euler(0f, heading, 0f),
                new Vector3(DispersalDefaults.CrowSize, 1f, DispersalDefaults.CrowSize));

            Graphics.DrawMesh(MeshPool.plane10, matrix, Crow(frame, alpha), 0);
        }
    }
}
