using UnityEngine;
using Verse;

namespace AbilityGenes
{
    /// <summary>Shared procedural summon rings and tapered trails; no external art dependency.</summary>
    [StaticConstructorOnStartup]
    public static class PanoplyRainGraphics
    {
        private static readonly Material Glow = new Material(ShaderDatabase.MoteGlow)
        {
            name = "Panoply rain glow", mainTexture = BaseContent.WhiteTex
        };
        private static readonly Mesh Ring = MakeRing();
        private static readonly Mesh Trail = MakeTrail();
        private static readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();
        private static readonly Color Gold = new Color(1f, 0.68f, 0.22f);

        public static void DrawGate(Vector3 pos, float alpha, int age, int seed)
        {
            if (alpha <= 0f) return;
            float opening = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 16f));
            float pulse = 0.88f + 0.12f * Mathf.Sin(age * 0.13f + seed);
            Draw(Ring, pos, 0f, new Vector3(1.25f, 1f, 0.5f) * opening, Gold, alpha * pulse);
            pos.y += 0.002f;
            Draw(Ring, pos, 0f, new Vector3(0.98f, 1f, 0.36f) * opening,
                new Color(1f, 0.92f, 0.65f), alpha * 0.75f);

            // Six orbiting sparks make the gate move even while its sword is held still.
            for (int i = 0; i < 6; i++)
            {
                float phase = i * Mathf.PI / 3f + age * 0.025f + seed;
                Vector3 spark = pos + new Vector3(Mathf.Cos(phase) * 0.66f, 0.003f,
                    Mathf.Sin(phase) * 0.28f) * opening;
                Draw(MeshPool.plane10, spark, 45f, new Vector3(0.055f, 1f, 0.055f), Gold, alpha);
            }
        }

        public static void DrawLandingMark(Vector3 pos, float alpha, float descent)
        {
            pos.y = AltitudeLayer.MetaOverlays.AltitudeFor();
            float size = Mathf.Lerp(0.85f, 0.48f, descent);
            Draw(Ring, pos, 0f, new Vector3(size, 1f, size), Gold, alpha * (0.2f + descent * 0.4f));
        }

        public static void DrawTrail(Vector3 pos, Vector3 backward, float descent)
        {
            float heading = Mathf.Atan2(backward.x, backward.z) * Mathf.Rad2Deg;
            pos.y -= 0.003f;
            float length = Mathf.Lerp(0.35f, 1.8f, descent);
            Draw(Trail, pos, heading, new Vector3(0.22f, 1f, length), Gold, 0.5f);
            pos.y += 0.001f;
            Draw(Trail, pos, heading, new Vector3(0.065f, 1f, length * 0.85f),
                new Color(1f, 0.96f, 0.8f), 0.8f);
        }

        private static void Draw(Mesh mesh, Vector3 pos, float angle, Vector3 scale, Color color, float alpha)
        {
            color.a = alpha;
            Properties.SetColor(ShaderPropertyIDs.Color, color);
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(pos, Quaternion.Euler(0f, angle, 0f), scale),
                Glow, 0, null, 0, Properties);
        }

        private static Mesh MakeRing()
        {
            const int segments = 64;
            Vector3[] vertices = new Vector3[segments * 2];
            int[] triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                int v = i * 2;
                int next = (v + 2) % vertices.Length;
                vertices[v] = radial * 0.5f;
                vertices[v + 1] = radial * 0.465f;
                int t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = next;
                triangles[t + 3] = next;
                triangles[t + 4] = v + 1;
                triangles[t + 5] = next + 1;
            }
            Mesh mesh = new Mesh { name = "Panoply summon ring", vertices = vertices,
                uv = new Vector2[vertices.Length], triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh MakeTrail()
        {
            Mesh mesh = new Mesh
            {
                name = "Panoply blade trail",
                vertices = new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(0f, 0f, 1f), new Vector3(0.5f, 0f, 0f) },
                uv = new Vector2[3],
                colors = new[] { Color.white, new Color(1f, 1f, 1f, 0f), Color.white },
                triangles = new[] { 0, 1, 2 }
            };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
