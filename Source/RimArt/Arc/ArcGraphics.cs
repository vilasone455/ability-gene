using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The streak the carrier leaves crossing a gap, and the only thing this gene draws.
    ///
    /// It is a solid colour rather than one of Core's line textures on purpose. Every line-shaped
    /// mote in the game is drawn by a system that decides its own heading, and borrowing one means
    /// inheriting a texture whose "up" has to be guessed at - which is the exact mistake the
    /// panoply organ made twice with the longsword sprite. A plain quad scaled along the path has
    /// no heading of its own to be wrong about: the rotation is the compass angle of the dash and
    /// nothing else.
    ///
    /// Two layers, because one bright rectangle reads as a bar and not as speed. A wide dim glow
    /// gives it edges that fall off, and a narrow bright core inside gives it a line.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ArcGraphics
    {
        /// <summary>The core of the streak: hot, almost white, faintly gold.</summary>
        private static readonly Color CoreColour = new Color(1f, 0.97f, 0.82f);

        /// <summary>The glow around it, which is what actually carries the colour at distance.</summary>
        private static readonly Color GlowColour = new Color(0.98f, 0.82f, 0.45f);

        /// <summary>
        /// Alpha is quantised and baked into the material rather than pushed through a property
        /// block, so the fade cannot depend on whether a given shader honours one. Two colours at
        /// eight steps is sixteen materials for the life of the session, all cached by the game.
        /// </summary>
        private static Material Streak(Color colour, float alpha)
        {
            int step = Mathf.Clamp(Mathf.RoundToInt(alpha * ArcDefaults.StreakAlphaSteps), 0, ArcDefaults.StreakAlphaSteps);
            colour.a = step / (float)ArcDefaults.StreakAlphaSteps;
            return SolidColorMaterials.NewSolidColorMaterial(colour, ShaderDatabase.MoteGlow);
        }

        /// <summary>
        /// Draws the trail of one dash. <paramref name="alpha"/> runs 1 to 0 across the life of
        /// the streak; everything else is fixed.
        /// </summary>
        public static void DrawStreak(Vector3 from, Vector3 to, float alpha)
        {
            if (alpha <= 0f) return;

            Vector3 along = to - from;
            float length = along.MagnitudeHorizontal();
            if (length < 0.05f) return;

            Vector3 centre = (from + to) * 0.5f;
            centre.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            float heading = along.AngleFlat();

            // The glow is drawn slightly shorter than the core so the ends of the streak read as a
            // point rather than as two square caps stacked on each other.
            Draw(centre, heading, ArcDefaults.StreakGlowWidth, length * 0.94f,
                Streak(GlowColour, alpha * 0.5f));

            centre.y += 0.02f;
            Draw(centre, heading, ArcDefaults.StreakWidth, length, Streak(CoreColour, alpha));
        }

        private static void Draw(Vector3 centre, float heading, float width, float length, Material material)
        {
            Matrix4x4 matrix = default(Matrix4x4);
            matrix.SetTRS(centre, Quaternion.Euler(0f, heading, 0f), new Vector3(width, 1f, length));
            Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
        }
    }
}
