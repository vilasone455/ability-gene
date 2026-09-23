using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The screen negative that Amenotejikara's sketch flashes on a swap (rinnegan-amenotejikara.js).
    ///
    /// RimWorld's own shaders only blend by alpha or add, so this uses Unity's built-in
    /// Hidden/Internal-Colored, which takes its blend factors as properties. With _SrcBlend
    /// OneMinusDstColor and _DstBlend OneMinusSrcAlpha, a draw coloured (a, a, a, a) leaves
    /// a * (1 - screen) + (1 - a) * screen: the map under it turned to a negative by a. A mid-grey
    /// Transparent draw after it pulls the colours toward grey, which blending cannot do fully.
    /// Both go on MetaOverlays, the top map layer, so the UI drawn after the map stays as it is.
    ///
    /// The shader is looked up by name at runtime. If the build does not include it the flash is
    /// skipped and one warning is logged; that is what the debug preview is there to find out.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class NegativeFlash
    {
        public const float Hold = .12f, Back = .1f, Grey = .25f;

        private static readonly Material Invert = MakeInvert();
        private static readonly Material GreyMat = MaterialPool.MatFrom(new MaterialRequest(BaseContent.WhiteTex, ShaderDatabase.Transparent));
        private static readonly MaterialPropertyBlock Props = new MaterialPropertyBlock();
        // Internal-Colored multiplies by vertex colour and MeshPool's planes have none, so both
        // meshes are built here with white vertex colours.
        private static readonly Mesh Quad = MakeFan(4, 45f, Mathf.Sqrt(.5f), "RimArt negative flash quad");
        private static readonly Mesh Disc = MakeFan(48, 0f, 1f, "RimArt negative flash disc");
        private static bool warned;

        /// <summary>Strength of the negative, 1 to 0, at <paramref name="age"/> seconds after the swap.</summary>
        public static float Strength(float age, float hold = Hold, float back = Back)
        {
            if (age < 0f) return 0f;
            if (age < hold) return 1f;
            float u = Mathf.Clamp01((age - hold) / back);
            return 1f - u * u * (3f - 2f * u);
        }

        /// <summary>The whole view, padded by two cells so a camera shake does not show an edge.</summary>
        public static void DrawScreen(float strength, float grey = Grey)
        {
            CellRect view = Find.CameraDriver.CurrentViewRect;
            Vector3 centre = view.CenterVector3;
            DrawBoth(Quad, centre, new Vector3(view.Width + 4f, 1f, view.Height + 4f), strength, grey);
        }

        /// <summary>A disc of the given radius in cells around one place.</summary>
        public static void DrawDisc(Vector3 centre, float radius, float strength, float grey = Grey) =>
            DrawBoth(Disc, centre, new Vector3(radius, 1f, radius), strength, grey);

        private static void DrawBoth(Mesh mesh, Vector3 centre, Vector3 scale, float strength, float grey)
        {
            if (strength <= 0f) return;
            if (Invert == null)
            {
                if (!warned) Log.Warning("[RimArt] Hidden/Internal-Colored was not found; the negative flash is skipped.");
                warned = true;
                return;
            }
            float y = AltitudeLayer.MetaOverlays.AltitudeFor();
            Props.Clear();
            Props.SetColor(ShaderPropertyIDs.Color, new Color(strength, strength, strength, strength));
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(centre.WithY(y), Quaternion.identity, scale), Invert, 0, null, 0, Props);
            if (grey <= 0f) return;
            Props.Clear();
            Props.SetColor(ShaderPropertyIDs.Color, new Color(.5f, .5f, .5f, grey * strength));
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(centre.WithY(y + .01f), Quaternion.identity, scale), GreyMat, 0, null, 0, Props);
        }

        private static Material MakeInvert()
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null) return null;
            // The Transparent queue, like the mod's own materials, so altitude alone orders it against them.
            var mat = new Material(shader) { renderQueue = 3000 };
            mat.SetInt("_SrcBlend", (int)BlendMode.OneMinusDstColor);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_Cull", (int)CullMode.Off);
            mat.SetInt("_ZWrite", 0);
            return mat;
        }

        // A flat fan in the x-z plane: segments corners at radius, the first at startDeg. Four
        // corners from 45 degrees at radius sqrt(0.5) is a 1 x 1 square.
        private static Mesh MakeFan(int segments, float startDeg, float radius, string name)
        {
            var vertices = new Vector3[segments + 1];
            var colors = new Color[segments + 1];
            var triangles = new int[segments * 3];
            colors[0] = Color.white;
            for (int i = 0; i < segments; i++)
            {
                float a = (startDeg + i * 360f / segments) * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                colors[i + 1] = Color.white;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % segments + 1;
                triangles[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = name, vertices = vertices, colors = colors, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
