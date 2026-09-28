using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// One bend of space, ported from lib/obito.js: a Kamui vortex (every point within reach turns clockwise
    /// round a centre and is pulled into it, nearer points first; k 0 is untouched, 1 is gone) or a small
    /// twist that does not pull in (where something passes through the phased body).
    /// </summary>
    internal struct KamuiWarp
    {
        public bool vortex;
        public Vector2 c;
        public float k, turns, reach, lead;
        public float amount;

        public static KamuiWarp Vortex(Vector2 c, float k, float turns = 1.5f, float reach = 1.2f, float lead = 0.8f) =>
            new KamuiWarp { vortex = true, c = c, k = k, turns = turns, reach = reach, lead = lead };

        public static KamuiWarp Twist(Vector2 c, float amount, float reach = 0.4f) =>
            new KamuiWarp { vortex = false, c = c, amount = amount, reach = reach };

        /// <summary>Whether it moves anything at all.</summary>
        public bool Active => vortex ? k > 0f : Mathf.Abs(amount) >= 0.002f;

        public Vector2 Apply(Vector2 p)
        {
            float dx = p.x - c.x, dz = p.y - c.y, r = Mathf.Sqrt(dx * dx + dz * dz);
            if (vortex)
            {
                float rho = Mathf.Min(1f, r / reach), q = ObitoGraphics.Clamp01(k * (1f + lead) - lead * rho);
                if (q <= 0f) return p;
                float e = q * q * (3f - 2f * q);
                float a = Mathf.Atan2(dz, dx) - turns * Mathf.PI * 2f * e * (1.25f - 0.5f * rho);
                float rr = r * Mathf.Pow(1f - e, 1.4f);
                return new Vector2(c.x + Mathf.Cos(a) * rr, c.y + Mathf.Sin(a) * rr);
            }
            if (r >= reach) return p;
            float f = 1f - r / reach, t = Mathf.Atan2(dz, dx) - amount * Mathf.PI * 2f * f * f, rt = r * (1f - 0.25f * Mathf.Abs(amount) * f);
            return new Vector2(c.x + Mathf.Cos(t) * rt, c.y + Mathf.Sin(t) * rt);
        }
    }

    /// <summary>
    /// A pawn or an item drawn as its picture on a grid mesh, so a Kamui warp can bend it (the sketches'
    /// picture()). The picture of a pawn is the pawn itself: the game's own pawn-cache camera renders it into a
    /// render texture (the same render the game blits when zoomed out, here at 128 px a cell), so costume, mask,
    /// wounds and all come along. An item's picture is its own graphic.
    ///
    /// Live pictures (Obito while phased, twisting or winding in and out) are rendered again every frame they
    /// are drawn; a still (a target being absorbed, a stored thing coming out) is rendered once.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class KamuiBend
    {
        /// <summary>Pixels a side of a pawn picture, and the cells it covers (the cache camera's ortho size 1 is 2 cells).</summary>
        private const int Pixels = 256;
        internal const float PawnCells = 2f;
        /// <summary>Grid points a side, minus one. The sketch bends a 1.5-cell picture on 20; this is 2 cells on 26.</summary>
        private const int N = 26;

        private static readonly Dictionary<Pawn, RenderTexture> live = new Dictionary<Pawn, RenderTexture>();
        private static readonly Stack<RenderTexture> spare = new Stack<RenderTexture>();
        private static readonly Dictionary<Texture, Material> materials = new Dictionary<Texture, Material>();
        private static readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();

        private static readonly List<Mesh> meshes = new List<Mesh>();
        private static int meshFrame = -1, meshesTaken;
        private static readonly Vector3[] vertices = new Vector3[(N + 1) * (N + 1)];
        private static readonly Vector2[] uvs = new Vector2[(N + 1) * (N + 1)];
        private static readonly int[] triangles = MakeTriangles();

        /// <summary>How many bent pictures have been drawn since the game started, for the tests.</summary>
        internal static int Drawn { get; private set; }

        private static int[] MakeTriangles()
        {
            var t = new int[N * N * 6];
            int k = 0;
            for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                {
                    int a = j * (N + 1) + i, b = a + N + 1;
                    t[k++] = a; t[k++] = b; t[k++] = b + 1;
                    t[k++] = a; t[k++] = b + 1; t[k++] = a + 1;
                }
            return t;
        }

        private static RenderTexture NewTexture()
        {
            if (spare.Count > 0) return spare.Pop();
            var rt = new RenderTexture(Pixels, Pixels, 24, RenderTextureFormat.ARGB32) { name = "RimArt Kamui picture", filterMode = FilterMode.Bilinear };
            rt.Create();
            return rt;
        }

        /// <summary>Hands a still back for reuse.</summary>
        internal static void Free(Texture texture)
        {
            if (texture is RenderTexture rt && !live.ContainsValue(rt)) spare.Push(rt);
        }

        /// <summary>The pawn rendered as the game draws it, facing <paramref name="facing"/>, into <paramref name="into"/> (or a new texture).</summary>
        internal static RenderTexture Render(Pawn pawn, Rot4 facing, RenderTexture into = null)
        {
            RenderTexture rt = into ?? NewTexture();
            if (pawn?.Drawer?.renderer == null) return rt;
            Find.PawnCacheRenderer.RenderPawn(pawn, rt, Vector3.zero, 1f, 0f, facing);
            return rt;
        }

        /// <summary>This frame's live picture of <paramref name="pawn"/>.</summary>
        internal static RenderTexture Live(Pawn pawn, Rot4 facing)
        {
            if (!live.TryGetValue(pawn, out RenderTexture rt) || rt == null)
            {
                rt = NewTexture();
                live[pawn] = rt;
            }
            return Render(pawn, facing, rt);
        }

        /// <summary>A pawn that is no longer bent gives its live texture back.</summary>
        internal static void ReleaseLive(Pawn pawn)
        {
            if (pawn != null && live.TryGetValue(pawn, out RenderTexture rt))
            {
                live.Remove(pawn);
                if (rt != null) spare.Push(rt);
            }
        }

        /// <summary>
        /// The picture of a thing about to be taken (absorbed) or put out (released): a pawn rendered once; an
        /// item's own graphic. <paramref name="size"/> is the cells the picture covers.
        /// </summary>
        internal static Texture Still(Thing thing, out float size, out bool owned)
        {
            owned = false;
            size = 1f;
            if (thing is Pawn pawn)
            {
                owned = true;
                size = PawnCells;
                return Render(pawn, pawn.Rotation);
            }
            Graphic graphic = thing?.Graphic;
            if (graphic == null) return null;
            size = Mathf.Max(graphic.drawSize.x, graphic.drawSize.y);
            return graphic.MatAt(thing.Rotation, thing)?.mainTexture;
        }

        private static Material MaterialFor(Texture texture)
        {
            if (!materials.TryGetValue(texture, out Material material) || material == null)
            {
                material = new Material(ShaderDatabase.Transparent) { mainTexture = texture, name = "RimArt Kamui picture" };
                materials[texture] = material;
            }
            return material;
        }

        private static Mesh NextMesh()
        {
            if (Time.frameCount != meshFrame)
            {
                meshFrame = Time.frameCount;
                meshesTaken = 0;
            }
            if (meshesTaken == meshes.Count)
            {
                var mesh = new Mesh { name = "RimArt Kamui bend " + meshes.Count };
                mesh.MarkDynamic();
                meshes.Add(mesh);
            }
            return meshes[meshesTaken++];
        }

        /// <summary>
        /// Draws <paramref name="texture"/> as a picture <paramref name="size"/> cells square centred on
        /// <paramref name="centre"/> (map x, z), each grid point moved by every warp in order, at
        /// <paramref name="altitude"/>, coloured <paramref name="tint"/> (alpha is the see-through).
        /// </summary>
        internal static void Draw(Texture texture, Vector2 centre, float size, float altitude, Color tint, List<KamuiWarp> warps)
        {
            if (texture == null || tint.a <= 0.005f) return;
            int v = 0;
            for (int j = 0; j <= N; j++)
                for (int i = 0; i <= N; i++, v++)
                {
                    var p = new Vector2(centre.x + (i / (float)N - 0.5f) * size, centre.y + (j / (float)N - 0.5f) * size);
                    if (warps != null)
                        for (int w = 0; w < warps.Count; w++)
                            if (warps[w].Active) p = warps[w].Apply(p);
                    vertices[v] = new Vector3(p.x - centre.x, 0f, p.y - centre.y);
                    uvs[v] = new Vector2(i / (float)N, j / (float)N);
                }
            Mesh mesh = NextMesh();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            if (mesh.triangles.Length != triangles.Length) mesh.triangles = triangles;
            mesh.RecalculateBounds();
            properties.SetColor(ShaderPropertyIDs.Color, tint);
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(new Vector3(centre.x, altitude, centre.y), Quaternion.identity, Vector3.one),
                MaterialFor(texture), 0, null, 0, properties);
            Drawn++;
        }
    }
}
