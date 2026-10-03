using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace RimArt
{
    /// <summary>
    /// The cutscene camera that draws a <see cref="UbwShot"/> over the map: a second Unity camera above the map
    /// camera that sees nothing of the game (culling mask 0, as the game's portrait camera) and draws only the frame
    /// it was handed this frame, from <c>OnPostRender</c>. With no frame handed this frame it turns itself off.
    ///
    /// How it draws, since the mod uses the game's shaders only:
    ///   - before the blend, every draw goes through one perspective matrix (the lab's camera: x east, y up, z north,
    ///     pitch above the horizontal, looking north) whose depth row is half its w row, so every point lands at the
    ///     same depth: whatever the shaders do with depth, the draws cover each other in the order they come, far to
    ///     near, which is how the frame is built;
    ///   - while blending, and for the draws tied to the game view, each vertex is placed on the CPU where the lab's
    ///     shader places it: from where the camera sees it toward its game position by the blend, a vertex behind the
    ///     camera put 3 screen half-widths out in its direction; drawn with identity matrices. Textures are not
    ///     perspective-correct in that second, a difference from the lab;
    ///   - a flat frame (the map camera draws the world) keeps the map's picture and only adds the boxes over it.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class UbwShotCamera
    {
        private static Camera camera;
        internal static UbwShot shot;
        internal static int shotFrame = -1;

        static UbwShotCamera()
        {
            UbwShot.Sink = Submit;
        }

        private static void Submit(UbwShot frame)
        {
            shot = frame;
            shotFrame = Time.frameCount;
            if (camera == null) camera = Make();
            // A flat frame keeps the map camera's picture; its depth is cleared so the boxes over it are never hidden.
            camera.clearFlags = frame.Flat ? CameraClearFlags.Depth : CameraClearFlags.SolidColor;
            camera.enabled = true;
        }

        private static Camera Make()
        {
            var made = new GameObject("RimArtCutsceneCamera", typeof(Camera));
            Object.DontDestroyOnLoad(made);
            Camera c = made.GetComponent<Camera>();
            c.cullingMask = 0;
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = Color.black;
            c.useOcclusionCulling = false;
            c.renderingPath = RenderingPath.Forward;
            c.allowHDR = false;
            // Over the map camera and its subcameras, under the UI (drawn after every camera).
            c.depth = 100f;
            c.enabled = false;
            made.AddComponent<UbwShotRenderer>();
            return c;
        }
    }

    /// <summary>The cutscene camera's drawing; see <see cref="UbwShotCamera"/>.</summary>
    internal sealed class UbwShotRenderer : MonoBehaviour
    {
        /// <summary>What the camera made of a <see cref="UbwMesh3"/>: the mesh at its 3D places, and one rewritten each frame of the blend.</summary>
        private sealed class Made
        {
            public Mesh Mesh3, Flat;
            public Vector3[] Buffer;
        }

        private readonly CommandBuffer buffer = new CommandBuffer { name = "RimArt cutscene" };
        private readonly MaterialPropertyBlock props = new MaterialPropertyBlock();
        private Mesh quad;
        private Camera cam;

        private void LateUpdate()
        {
            // Nobody handed a frame this frame: the cutscene is over (or paused off screen).
            if (cam == null) cam = GetComponent<Camera>();
            if (UbwShotCamera.shotFrame != Time.frameCount) cam.enabled = false;
        }

        private void OnPostRender()
        {
            UbwShot shot = UbwShotCamera.shot;
            if (shot == null || UbwShotCamera.shotFrame != Time.frameCount) return;
            buffer.Clear();
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            if (!shot.Flat) Draws(shot, aspect);
            Overlays(shot);
            Graphics.ExecuteCommandBuffer(buffer);
        }

        // ---- the camera's matrices (Tools/VfxLab/web/js/camera.js viewProjection, yaw 0) ------------------------------

        private struct View
        {
            public Vector3 Eye, Right, Up, Forward;
            public float K, Aspect, Near, Far;
        }

        private static View ViewOf(UbwShot shot, float aspect)
        {
            float p = shot.Pitch * Mathf.Deg2Rad, cp = Mathf.Cos(p), sp = Mathf.Sin(p);
            return new View
            {
                Eye = shot.Eye, Right = new Vector3(1f, 0f, 0f), Up = new Vector3(0f, cp, -sp), Forward = new Vector3(0f, sp, cp),
                K = 1f / Mathf.Tan(shot.Fov * Mathf.Deg2Rad / 2f), Aspect = aspect, Near = shot.Near, Far = shot.Far,
            };
        }

        /// <summary>Projection times view with the depth row set to half the w row: one depth for everything, inside the clip range for any point ahead.</summary>
        private static Matrix4x4 FlatDepth(View v)
        {
            var m = new Matrix4x4();
            void Row(int r, Vector3 a, float scale)
            {
                m[r, 0] = a.x * scale; m[r, 1] = a.y * scale; m[r, 2] = a.z * scale; m[r, 3] = -Vector3.Dot(a, v.Eye) * scale;
            }
            Row(0, v.Right, v.K / v.Aspect);
            Row(1, v.Up, v.K);
            Row(2, v.Forward, 0.5f);
            Row(3, v.Forward, 1f);
            return m;
        }

        // ---- drawing ---------------------------------------------------------------------------------------------------

        private void Draws(UbwShot shot, float aspect)
        {
            View v = ViewOf(shot, aspect);
            Matrix4x4 vp = FlatDepth(v);
            float halfH = shot.CellsTall / 2f, halfW = halfH * aspect;
            int mode = -1;
            foreach (UbwDraw3 d in shot.Draws)
            {
                Made made = MadeOf(d.Mesh);
                props.Clear();
                props.SetColor(ShaderPropertyIDs.Color, d.Colour);
                bool gpu = shot.Blend <= 0f && !d.Screen;
                if (mode != (gpu ? 1 : 0))
                {
                    mode = gpu ? 1 : 0;
                    buffer.SetViewProjectionMatrices(Matrix4x4.identity, gpu ? vp : Matrix4x4.identity);
                }
                if (gpu)
                {
                    buffer.DrawMesh(made.Mesh3, Matrix4x4.TRS(d.At, Quaternion.identity, new Vector3(1f, d.Sy, 1f)), d.Material, 0, 0, props);
                    continue;
                }
                Blend(made, d, v, shot.GameCentre, halfW, halfH, d.Screen ? 1f : shot.Blend);
                buffer.DrawMesh(made.Flat, Matrix4x4.identity, d.Material, 0, 0, props);
            }
        }

        /// <summary>The lab's blend (gl.js THREE_VS), on the CPU: each vertex from where the camera sees it toward where the game view draws it, by b.</summary>
        private static void Blend(Made made, in UbwDraw3 d, in View v, Vector2 centre, float halfW, float halfH, float b)
        {
            UbwMesh3 m = d.Mesh;
            Vector3[] xyz = m.Xyz;
            Vector2[] game = m.Game;
            Vector3[] outv = made.Buffer;
            for (int i = 0; i < xyz.Length; i++)
            {
                Vector2 ndcP = Vector2.zero;
                if (b < 1f)
                {
                    Vector3 rel = new Vector3(d.At.x + xyz[i].x, d.At.y + xyz[i].y * d.Sy, d.At.z + xyz[i].z) - v.Eye;
                    float x = Vector3.Dot(v.Right, rel) * v.K / v.Aspect, y = Vector3.Dot(v.Up, rel) * v.K, w = Vector3.Dot(v.Forward, rel);
                    if (w < 0.05f) ndcP = new Vector2(x, y + 1e-4f).normalized * 3f;
                    else
                    {
                        ndcP = new Vector2(x / w, y / w);
                        float l = ndcP.magnitude;
                        if (l > 3f) ndcP *= 3f / l;
                    }
                }
                var ndcG = new Vector2((d.At.x + game[i].x - centre.x) / halfW, (d.At.z + game[i].y - centre.y) / halfH);
                Vector2 ndc = ndcP + (ndcG - ndcP) * b;
                outv[i] = new Vector3(ndc.x, ndc.y, 0f);
            }
            made.Flat.vertices = outv;
        }

        private readonly Dictionary<UbwMesh3, Made> made = new Dictionary<UbwMesh3, Made>();

        private Made MadeOf(UbwMesh3 m)
        {
            if (!made.TryGetValue(m, out Made x)) made[m] = x = new Made();
            if (m.MadeVersion == m.Version && x.Mesh3 != null) return x;
            m.MadeVersion = m.Version;
            x.Mesh3 = Fill(x.Mesh3, m.Name, m.Xyz, m);
            if (x.Buffer == null || x.Buffer.Length != m.Xyz.Length) x.Buffer = new Vector3[m.Xyz.Length];
            x.Flat = Fill(x.Flat, m.Name + " blended", x.Buffer, m);
            return x;
        }

        private static Mesh Fill(Mesh mesh, string name, Vector3[] vertices, UbwMesh3 m)
        {
            if (mesh == null) mesh = new Mesh { name = name };
            else mesh.Clear();
            mesh.indexFormat = vertices.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.uv = m.Uv;
            mesh.triangles = m.Tri;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            return mesh;
        }

        /// <summary>The boxes over the finished frame (the white, the black bars): x, y, w, h in fractions of the screen, y from the top.</summary>
        private void Overlays(UbwShot shot)
        {
            if (shot.Overlays.Count == 0) return;
            if (quad == null)
            {
                quad = new Mesh { name = "RimArt cutscene box" };
                quad.vertices = new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(1f, 0f, 0f) };
                quad.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
                quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            }
            buffer.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
            foreach (var (box, colour) in shot.Overlays)
            {
                props.Clear();
                props.SetColor(ShaderPropertyIDs.Color, colour);
                float x0 = box.x * 2f - 1f, y1 = 1f - box.y * 2f;
                buffer.DrawMesh(quad, Matrix4x4.TRS(new Vector3(x0, y1 - box.height * 2f, 0f), Quaternion.identity, new Vector3(box.width * 2f, box.height * 2f, 1f)), VfxDraw.solid, 0, 0, props);
            }
        }
    }
}
