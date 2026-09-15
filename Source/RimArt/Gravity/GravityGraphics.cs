using UnityEngine;
using Verse;

namespace RimArt
{
    [StaticConstructorOnStartup]
    public static class GravityGraphics
    {
        private static readonly Material solid = new Material(ShaderDatabase.Transparent) { mainTexture = BaseContent.WhiteTex };
        private static readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private static readonly Mesh disc = Disc(), ring = Band(0.96f, 1.04f, 1f), spiral = Band(0.72f, 0.77f, 0.62f);
        private static Material warp;
        private static bool resolvedWarp;
        private static Texture2D icon;
        public static Material TrailMaterial => SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.7f, 0.8f, 1f, 0.55f), true);
        public static Texture2D Icon => icon ??= MakeIcon();

        public static void Draw(Vector3 centre, float seconds, float mass, float fade, bool imploded, Map map)
        {
            float power = GravityRules.Clamp(mass / GravityRules.FullMass);
            float scale = imploded ? Mathf.Max(0.02f, fade * fade) : Mathf.Min(1f, seconds * 3f + 0.1f);
            float altitude = AltitudeLayer.MoteOverhead.AltitudeFor();
            DrawMesh(disc, centre.WithY(AltitudeLayer.MoteLow.AltitudeFor()), 3f * scale, 2.1f * scale, 0f,
                new Color(0.02f, 0.01f, 0.06f, 0.28f * fade));
            DrawWarp(centre.WithY(altitude - 0.01f), seconds, scale, fade);
            Color light = Color.Lerp(new Color(0.45f, 0.55f, 1f), new Color(0.85f, 0.94f, 1f), power);
            for (int i = 0; i < 4; i++)
            {
                float size = (1.8f + i * 0.22f) * scale;
                Color colour = light; colour.a = (0.75f - i * 0.12f) * fade;
                DrawMesh(spiral, centre.WithY(altitude + i * 0.001f), size, size * 0.58f,
                    seconds * (i % 2 == 0 ? 90f : -65f) + i * 83f, colour);
            }
            Vector3 core = centre.WithY(altitude + 0.01f);
            DrawMesh(disc, core, 0.74f * scale, 0.74f * scale, 0f, new Color(0f, 0f, 0f, fade));
            DrawMesh(ring, core.WithY(altitude + 0.012f), 0.77f * scale, 0.77f * scale, 0f,
                new Color(light.r, light.g, light.b, fade));
            // Deterministic visual debris does not consume gameplay RNG or become loot.
            for (int i = 0; i < 48; i++)
            {
                float cycle = Mathf.Repeat(seconds * (0.23f + i % 5 * 0.02f) + i * 0.618034f, 1f);
                float radius = Mathf.Lerp(7.5f, 0.8f, cycle) * scale;
                float angle = i * 2.39996f + cycle * 4f;
                Vector3 pos = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (!pos.ToIntVec3().InBounds(map) || pos.ToIntVec3().Fogged(map)) continue;
                float alpha = Mathf.Sin(cycle * Mathf.PI) * fade * 0.65f;
                DrawMesh(MeshPool.plane10, pos.WithY(altitude + 0.02f), 0.045f + i % 3 * 0.018f,
                    0.035f, angle * Mathf.Rad2Deg, new Color(0.64f, 0.61f, 0.68f, alpha));
            }
            if (imploded)
                DrawMesh(ring, centre.WithY(altitude + 0.03f), 2f * fade, 1.4f * fade, 0f,
                    new Color(0.9f, 0.95f, 1f, fade));
        }

        private static void DrawWarp(Vector3 position, float time, float scale, float alpha)
        {
            if (!resolvedWarp)
            {
                resolvedWarp = true;
                Shader shader = DefDatabase<ShaderTypeDef>.GetNamedSilentFail("MoteLargeDistortionWave")?.Shader;
                var currents = ContentFinder<Texture2D>.Get("Things/Mote/PsychicDistortionCurrents", false);
                var noise = ContentFinder<Texture2D>.Get("Things/Mote/PsycastNoise", false);
                var mask = ContentFinder<Texture2D>.Get("Things/Mote/PsycastSkipFlash", false);
                if (shader != null && currents != null && noise != null && mask != null)
                {
                    warp = new Material(shader) { mainTexture = mask };
                    warp.SetTexture("_DistortionTex", currents); warp.SetTexture("_NoiseTex", noise);
                    warp.SetFloat("_distortionIntensity", 0.10f);
                }
            }
            if (warp == null) return;
            properties.SetColor(ShaderPropertyIDs.Color, new Color(1f, 1f, 1f, alpha));
            properties.SetFloat(ShaderPropertyIDs.AgeSecs, time);
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(position, Quaternion.identity,
                new Vector3(5f * scale, 1f, 5f * scale)), warp, 0, null, 0, properties);
        }
        private static void DrawMesh(Mesh mesh, Vector3 position, float width, float depth, float rotation, Color colour)
        {
            properties.SetColor(ShaderPropertyIDs.Color, colour);
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(position, Quaternion.Euler(0f, rotation, 0f),
                new Vector3(width, 1f, depth)), solid, 0, null, 0, properties);
        }
        private static Mesh Disc()
        {
            const int segments = 96;
            var vertices = new Vector3[segments + 1]; var indices = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                indices[i * 3] = 0; indices[i * 3 + 1] = (i + 1) % segments + 1; indices[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = "Gravity core", vertices = vertices, triangles = indices };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        private static Mesh Band(float inner, float outer, float fraction)
        {
            const int segments = 96;
            var vertices = new Vector3[(segments + 1) * 2]; var indices = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments, angle = t * Mathf.PI * 2f * fraction;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float taper = fraction == 1f ? 1f : Mathf.Sin(t * Mathf.PI);
                vertices[i * 2] = direction * (inner + (fraction == 1f ? 0f : t * 0.3f));
                vertices[i * 2 + 1] = direction * (inner + (outer - inner) * taper + (fraction == 1f ? 0f : t * 0.3f));
                if (i == segments) continue;
                int v = i * 2, j = i * 6;
                indices[j] = v; indices[j + 1] = v + 2; indices[j + 2] = v + 1;
                indices[j + 3] = v + 1; indices[j + 4] = v + 2; indices[j + 5] = v + 3;
            }
            var mesh = new Mesh { name = "Gravity disk", vertices = vertices, triangles = indices };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        private static Texture2D MakeIcon()
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "Gravity Well icon" };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float dx = (x - 31.5f) / 28f, dy = (y - 31.5f) / 28f;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float disk = Mathf.Sqrt(dx * dx + dy * dy * 3f);
                Color colour = Color.clear;
                if (disk < 1f && disk > 0.45f) colour = new Color(0.42f, 0.57f, 1f, 1f);
                if (radius < 0.38f) colour = new Color(0.015f, 0.015f, 0.035f, 1f);
                if (radius >= 0.34f && radius < 0.4f) colour = new Color(0.85f, 0.94f, 1f, 1f);
                texture.SetPixel(x, y, colour);
            }
            texture.Apply(); return texture;
        }
    }
}
