using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// The drawing pieces shared by Unlimited Void and the Gojo abilities that come after it: the kit's
    /// colours, the rim light round Gojo, the white splatter burst, the light over the whole view, and
    /// the dome's outline under the 0.6 lift. The port of the parts of the lab's lib/gojo.js that the
    /// pocket-map sketches use. Everything is a level circle, a quad or a flat polygon about one point,
    /// so nothing needs a per-facing method. Every function takes ages and times and keeps no state.
    ///
    /// Not ported: the stand-in Gojo (body, hair, blindfold, eyes, hands and the hand sign) and the
    /// old dome, cutscene, black hole and ray pieces the superseded sketches draw with.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class GojoGraphics
    {
        internal static readonly Color EyeBlue = new Color(0.35f, 0.75f, 1f), Violet = new Color(0.55f, 0.4f, 0.95f);
        internal static readonly Color Pink = new Color(1f, 0.55f, 0.85f), Teal = new Color(0.55f, 0.95f, 0.78f);
        /// <summary>lib/gojo.js calls this Gold; ThunderGodGraphics has a different Gold.</summary>
        internal static readonly Color Peach = new Color(1f, 0.92f, 0.75f);
        internal static readonly Color White = new Color(1f, 1f, 1f);
        internal static readonly Color Ice = VergilGraphics.Ice, Blue = VergilGraphics.Blue;

        internal static readonly Material PuffGlow = MaterialPool.MatFrom("RimArt/SixPaths/Puff", ShaderDatabase.MoteGlow);
        internal static readonly float PawnLayer = AltitudeLayer.Pawn.AltitudeFor(), ShadowLayer = AltitudeLayer.Shadows.AltitudeFor();
        /// <summary>The top map layer: the white over the view stays white at night and covers everything under the UI.</summary>
        internal static readonly float TopLayer = AltitudeLayer.MetaOverlays.AltitudeFor();

        /// <summary>
        /// The dome as seen from above under the 0.6 lift, radius 1: the north half is the projected
        /// sphere (an ellipse √(1 + 0.6²) tall), the south half the floor circle. Counter-clockwise
        /// from east. Scaled by the radius when drawn.
        /// </summary>
        internal const int DomeSides = 64;
        internal static readonly Vector2[] DomeOutline = MakeDomeOutline();
        /// <summary>The dome's outline filled from its ground point, radius 1.</summary>
        internal static readonly Mesh DomeFan = MakeDomeFan();

        private static Vector2[] MakeDomeOutline()
        {
            float tall = Mathf.Sqrt(1f + SixPathsHeight.Lift * SixPathsHeight.Lift);
            var points = new Vector2[DomeSides];
            for (int i = 0; i < DomeSides; i++)
            {
                float a = i / (float)DomeSides * Mathf.PI * 2f, sin = Mathf.Sin(a);
                points[i] = new Vector2(Mathf.Cos(a), sin * (sin >= 0f ? tall : 1f));
            }
            return points;
        }

        private static Mesh MakeDomeFan()
        {
            var vertices = new Vector3[DomeSides + 1];
            var indices = new int[DomeSides * 3];
            for (int i = 0; i < DomeSides; i++)
            {
                vertices[1 + i] = new Vector3(DomeOutline[i].x, 0f, DomeOutline[i].y);
                // Clockwise on screen, so the face survives backface culling (the sketch's fan runs the other way).
                indices[i * 3] = 0; indices[i * 3 + 1] = 1 + (i + 1) % DomeSides; indices[i * 3 + 2] = 1 + i;
            }
            var mesh = new Mesh { name = "Gojo dome fan", vertices = vertices, triangles = indices };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// The game's cyan-violet edge on Gojo, round the pawn standing at <paramref name="feet"/>: a
        /// glow behind the figure and two violet ovals just under the pawn layer, the size of the
        /// sketch's stand-in body and head, so a real pawn drawn over them keeps a violet edge.
        /// </summary>
        internal static void RimLight(Vector2 feet, float rim, float alpha)
        {
            if (rim <= 0f || alpha <= 0f) return;
            Sprite(new Vector2(feet.x, feet.y + 0.3f), 0.9f, 1.2f, Fade(EyeBlue, 0.5f * rim * alpha), glow, PawnLayer - 0.005f);
            Color edge = Fade(Violet, 0.7f * rim * alpha);
            DrawMesh(disc, new Vector2(feet.x, feet.y + 0.18f), PawnLayer - 0.003f, 0.26f, 0.36f, 0f, edge, solid);
            DrawMesh(disc, new Vector2(feet.x, feet.y + 0.58f), PawnLayer - 0.0031f, 0.19f, 0.2f, 0f, edge, solid);
        }

        /// <summary>
        /// The white splatter burst of the anime's first frame: a rough puff of light and 16 ragged rays
        /// of uneven length out to about <paramref name="reach"/>. <paramref name="bright"/> 0..1.
        /// </summary>
        internal static void Splatter(Vector2 at, float reach, float bright)
        {
            if (bright <= 0f) return;
            Sprite(at, reach * 1.3f, reach * 1.3f, Fade(White, 0.9f * bright), PuffGlow, Overhead + 0.09f, 30f);
            Sprite(at, reach * 0.9f, reach * 0.9f, Fade(Ice, 0.8f * bright), PuffGlow, Overhead + 0.091f, 140f);
            for (int i = 0; i < 16; i++)
            {
                Vector2 d = Turn(i * 22.5f + Rand(i + 70) * 14f);
                float length = reach * (0.45f + 0.75f * Rand(i + 80)), width = 0.14f + 0.18f * Rand(i + 90);
                Streak(at + d * 0.2f, at + d * length, width, Fade(White, bright), whiteGlow, Overhead + 0.092f, 4);
            }
        }

        /// <summary>
        /// White over everything the camera sees, padded by two cells so a camera shake does not show
        /// an edge (as Rinnegan's negative flash sizes itself). It hides the move between maps.
        /// </summary>
        internal static void WhiteView(float alpha)
        {
            if (alpha <= 0f) return;
            CellRect view = Find.CameraDriver.CurrentViewRect;
            Vector3 centre = view.CenterVector3;
            DrawMesh(MeshPool.plane10, new Vector2(centre.x, centre.z), TopLayer, view.Width + 4f, view.Height + 4f, 0f, Fade(White, alpha), solid);
        }
    }
}
