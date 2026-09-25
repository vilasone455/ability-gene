using UnityEngine;
using Verse;
using static RimArt.VfxDraw;

namespace RimArt
{
    /// <summary>
    /// Draws one ground burst from SixPathsBurstTiming. Make one of these per burst that can show
    /// in the same frame: each owns the meshes of its eighteen shards.
    /// </summary>
    [StaticConstructorOnStartup]
    internal sealed class SixPathsBurstGraphics
    {
        private static readonly Material solid =
            new Material(ShaderDatabase.Transparent) { mainTexture = BaseContent.WhiteTex };
        private static readonly Material softGlow = MaterialPool.MatFrom("RimArt/SixPaths/SoftDisc", ShaderDatabase.MoteGlow);
        private static readonly Material puff = MaterialPool.MatFrom("RimArt/SixPaths/Puff", ShaderDatabase.Transparent);
        private static readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private static readonly Mesh ring = VfxDraw.Ring(0.965f, "Six Paths burst ring");

        private static readonly Color Body = new Color(0.035f, 0.028f, 0.050f);
        private static readonly Color Rim = new Color(0.52f, 0.36f, 0.86f);
        private static readonly Color Flash = new Color(0.87f, 0.76f, 1f);
        private static readonly Color Dust = new Color(0.52f, 0.45f, 0.37f);

        private readonly SixPathsStrip[] shards = new SixPathsStrip[SixPathsBurstTiming.Pieces];
        private readonly Vector2[] line = new Vector2[3];

        public SixPathsBurstGraphics(string name)
        {
            for (int i = 0; i < shards.Length; i++) shards[i] = new SixPathsStrip(name + " shard " + i, 3);
        }

        public void Draw(Vector3 ground, float age, float strength, float size, float dust = 0.6f)
        {
            if (!SixPathsBurstTiming.Showing(age)) return;
            float overhead = AltitudeLayer.MoteOverhead.AltitudeFor();
            DrawMesh(MeshPool.plane10, ground.WithY(overhead + 0.15f), size * 3f, size * 2f,
                Fade(Flash, SixPathsBurstTiming.Flash(age) * strength * 0.8f), softGlow);
            float radius = SixPathsBurstTiming.RingRadius(age, size);
            DrawMesh(ring, ground.WithY(AltitudeLayer.Filth.AltitudeFor()), radius, radius,
                Fade(Rim, SixPathsBurstTiming.RingAlpha(age, strength)), solid);
            for (int i = 0; i < shards.Length; i++)
            {
                if (!SixPathsBurstTiming.Piece(i, age, strength, size, dust, out BurstPiece piece)) continue;
                DrawMesh(MeshPool.plane10, (ground + new Vector3(piece.at.x, 0f, piece.at.y)).WithY(overhead + 0.10f),
                    piece.width, piece.depth, Fade(Dust, piece.puffAlpha), puff);
                line[0] = piece.tail; line[1] = piece.at; line[2] = piece.nose;
                shards[i].Line(line, piece.shardWidth);
                DrawMesh(shards[i].mesh, ground.WithY(overhead + 0.12f), 1f, 1f, Fade(Body, piece.shardAlpha), solid);
            }
        }

        private static void DrawMesh(Mesh mesh, Vector3 position, float width, float depth, Color colour, Material material)
        {
            properties.SetColor(ShaderPropertyIDs.Color, colour);
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(position, Quaternion.identity, new Vector3(width, 1f, depth)),
                material, 0, null, 0, properties);
        }

        /// <summary>A unit-radius band from <paramref name="inner"/> to 1.</summary>
    }
}
