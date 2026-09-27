using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// The picture of a word travelling (docs/hero-echo.md, Inumaki). Picture only: the word has
    /// already acted on everyone when this starts. Not saved; a wave lasts under a second.
    ///
    /// From the anime (ep 17 and 19): the voice is a ripple with a colour fringe coming from his
    /// mouth, and a target is wrapped in purple-white crackle. Here that is:
    /// - rings at his head for a moment (the mouth burst);
    /// - a band of ripples that moves out over exactly the cells the sound reached
    ///   (<see cref="SoundSpread"/>), at <see cref="Speed"/>, so it stops at walls, arrives late past
    ///   an open door (the doorway cost is distance) and fades toward the edge of the volume's reach;
    ///   magenta just ahead, lavender, cyan just behind, for the fringe (one vertex-coloured mesh);
    /// - crackle round each listener from the moment the band reaches them.
    /// The clock is game ticks, so it pauses and speeds up with the game.
    /// </summary>
    public class MapComponent_CursedSpeech : MapComponent
    {
        /// <summary>Cells per second the band moves.</summary>
        private const float Speed = 30f;
        /// <summary>Width, in cells, of the ripple band behind the front.</summary>
        private const float Band = 3.2f;
        /// <summary>Cells between two ripples inside the band.</summary>
        private const float Ridge = 1.5f;
        /// <summary>Quads per cell side in the band mesh, so a ripple has several vertices across it.</summary>
        private const int Sub = 3;
        /// <summary>Colour offset of the magenta and cyan copies from the front, in cells.</summary>
        private const float Fringe = 0.35f;
        /// <summary>How long the band keeps fading after it reaches the edge.</summary>
        private const float Tail = 0.35f;
        /// <summary>Cells before the edge of reach over which the band fades to nothing.</summary>
        private const float EdgeFade = 1.5f;
        private const float MouthSeconds = 0.35f;
        private const float CrackleSeconds = 0.7f;

        private static readonly Color Magenta = new Color(0.9f, 0.3f, 1f);
        private static readonly Color Lavender = new Color(0.85f, 0.8f, 1f);
        private static readonly Color Cyan = new Color(0.35f, 0.8f, 1f);
        private static readonly Color Purple = new Color(0.6f, 0.35f, 1f);

        private sealed class Wave
        {
            public Pawn speaker;
            public Vector2 origin;
            public IntVec3[] cells;
            public float[] distance;
            /// <summary>Per cell: the centre of its source (<see cref="SoundSpread.Reach"/>) and the distance there.</summary>
            public Vector2[] source;
            public float[] sourceDistance;
            public Dictionary<IntVec3, int> index;
            public Mesh mesh;
            public float reach, strength;
            public int startTick;
            public List<Pawn> listeners = new List<Pawn>();
            public List<float> heardAt = new List<float>();

            public float Seconds(int now) => (now - startTick) / 60f;
            public float Length => reach / Speed + Tail + CrackleSeconds;
        }

        private readonly List<Wave> waves = new List<Wave>();
        private static readonly List<Vector3> verts = new List<Vector3>();
        private static readonly List<Color> colors = new List<Color>();
        private static readonly List<int> tris = new List<int>();

        public MapComponent_CursedSpeech(Map map) : base(map) { }

        public void Say(Pawn speaker, List<WordListener> listeners, float reach, float doorwayCost, WordVolume volume)
        {
            // The same flood the listeners were found in, with each cell's source kept for the rings.
            var sources = new Dictionary<IntVec3, IntVec3>();
            Dictionary<IntVec3, float> reached = SoundSpread.Reach(map, speaker.Position, reach, doorwayCost, sources);
            var wave = new Wave
            {
                speaker = speaker,
                origin = Ground(speaker.DrawPos),
                cells = new IntVec3[reached.Count],
                distance = new float[reached.Count],
                source = new Vector2[reached.Count],
                sourceDistance = new float[reached.Count],
                index = new Dictionary<IntVec3, int>(reached.Count),
                reach = Mathf.Max(1f, reach),
                strength = volume == WordVolume.Whisper ? 0.6f : volume == WordVolume.Shout ? 1.25f : 1f,
                startTick = Find.TickManager.TicksGame,
            };
            int i = 0;
            foreach (KeyValuePair<IntVec3, float> pair in reached)
            {
                wave.cells[i] = pair.Key;
                wave.distance[i] = pair.Value;
                IntVec3 from = sources[pair.Key];
                wave.source[i] = Ground(from.ToVector3Shifted());
                wave.sourceDistance[i] = reached[from];
                wave.index[pair.Key] = i;
                i++;
            }
            for (int k = 0; k < listeners.Count; k++)
            {
                Pawn pawn = listeners[k].pawn;
                if (!reached.TryGetValue(pawn.Position, out float d)) continue;
                wave.listeners.Add(pawn);
                wave.heardAt.Add(d / Speed);
            }
            waves.Add(wave);
        }

        public override void MapComponentUpdate()
        {
            if (waves.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            for (int i = waves.Count - 1; i >= 0; i--)
            {
                Wave w = waves[i];
                if (w.Seconds(now) <= w.Length && now >= w.startTick) continue;
                if (w.mesh != null) Object.Destroy(w.mesh);
                waves.RemoveAt(i);
            }
            if (map != Find.CurrentMap) return;
            for (int i = 0; i < waves.Count; i++) Draw(waves[i], waves[i].Seconds(now));
        }

        private void Draw(Wave wave, float t)
        {
            VfxDraw.Begin(wave.origin);
            DrawMouth(wave, t);
            float front = t * Speed;
            if (front < wave.reach + Band + Fringe) DrawBand(wave, front);
            for (int i = 0; i < wave.listeners.Count; i++)
            {
                float since = t - wave.heardAt[i];
                if (since >= 0f && since <= CrackleSeconds) DrawCrackle(wave.listeners[i], since, i);
            }
        }

        /// <summary>Three quick rings at his head and a purple puff: the word leaving the mouth.</summary>
        private void DrawMouth(Wave wave, float t)
        {
            if (t > MouthSeconds + 0.2f) return;
            Vector2 head = wave.speaker != null && wave.speaker.Spawned
                ? Ground(wave.speaker.DrawPos) + new Vector2(0f, 0.3f) : wave.origin + new Vector2(0f, 0.3f);
            float puff = 1f - Mathf.Clamp01(t / (MouthSeconds + 0.2f));
            VfxDraw.Sprite(head, 1.1f * wave.strength, 1.1f * wave.strength, VfxDraw.Fade(Purple, 0.45f * puff), VfxDraw.glow, VfxDraw.Overhead);
            for (int r = 0; r < 3; r++)
            {
                float local = (t - r * 0.08f) / MouthSeconds;
                if (local < 0f || local > 1f) continue;
                float radius = Mathf.Lerp(0.15f, 1.3f * wave.strength, Smooth(local));
                VfxDraw.Circle(head, radius, 0.55f * (1f - local), VfxDraw.Overhead, r == 1 ? Lavender : r == 0 ? Magenta : Cyan);
            }
        }

        /// <summary>
        /// The ripple band as one mesh with a colour per vertex. Each reached cell near the front is
        /// split into <see cref="Sub"/> x <see cref="Sub"/> quads; every vertex takes the travelled
        /// distance at its own point (<see cref="DistanceAt"/>) and sums the three colour passes, so
        /// the band is a smooth ring on open ground and ends at the edge of a wall's cell. Drawn
        /// additive, so it lights the ground and never covers it.
        /// </summary>
        private void DrawBand(Wave wave, float front)
        {
            Material material = BandMaterial;
            if (material == null) return;
            verts.Clear();
            colors.Clear();
            tris.Clear();
            float lo = front - Fringe - Band - 1.5f, hi = front + Fringe + 1.5f;
            for (int i = 0; i < wave.cells.Length; i++)
            {
                float d = wave.distance[i];
                if (d < lo || d > hi) continue;
                IntVec3 cell = wave.cells[i];
                int first = verts.Count;
                bool any = false;
                for (int a = 0; a <= Sub; a++)
                    for (int b = 0; b <= Sub; b++)
                    {
                        float x = cell.x + (float)a / Sub, z = cell.z + (float)b / Sub;
                        Color c = Shade(wave, front, DistanceAt(wave, i, x, z));
                        if (c.r + c.g + c.b > 0.004f) any = true;
                        verts.Add(new Vector3(x, VfxDraw.Floor, z));
                        colors.Add(c);
                    }
                if (!any)
                {
                    verts.RemoveRange(first, verts.Count - first);
                    colors.RemoveRange(first, colors.Count - first);
                    continue;
                }
                for (int a = 0; a < Sub; a++)
                    for (int b = 0; b < Sub; b++)
                    {
                        int v = first + a * (Sub + 1) + b;
                        tris.Add(v); tris.Add(v + 1); tris.Add(v + Sub + 2);
                        tris.Add(v); tris.Add(v + Sub + 2); tris.Add(v + Sub + 1);
                    }
            }
            if (wave.mesh == null) wave.mesh = new Mesh { name = "RimArt cursed speech band" };
            wave.mesh.Clear();
            if (tris.Count == 0) return;
            wave.mesh.SetVertices(verts);
            wave.mesh.SetColors(colors);
            wave.mesh.SetTriangles(tris, 0);
            Graphics.DrawMesh(wave.mesh, Matrix4x4.identity, material, 0);
        }

        /// <summary>
        /// Travelled distance at a point: for every reached cell touching the point, the distance at
        /// that cell's source plus the straight line from it, and the smallest of those. Round the
        /// speaker, a wall corner or a doorway this is a circle, and taking the smallest keeps the
        /// band continuous where two neighbouring cells measure from different sources.
        /// </summary>
        private static float DistanceAt(Wave wave, int own, float x, float z)
        {
            float best = FromSource(wave, own, x, z);
            int fx = Mathf.FloorToInt(x), fz = Mathf.FloorToInt(z);
            bool onX = Mathf.Abs(x - Mathf.Round(x)) < 0.001f, onZ = Mathf.Abs(z - Mathf.Round(z)) < 0.001f;
            if (!onX && !onZ) return best;
            int rx = Mathf.RoundToInt(x), rz = Mathf.RoundToInt(z);
            int x0 = onX ? rx - 1 : fx, x1 = onX ? rx : fx;
            int z0 = onZ ? rz - 1 : fz, z1 = onZ ? rz : fz;
            for (int cx = x0; cx <= x1; cx++)
                for (int cz = z0; cz <= z1; cz++)
                    if (wave.index.TryGetValue(new IntVec3(cx, 0, cz), out int i) && i != own)
                        best = Mathf.Min(best, FromSource(wave, i, x, z));
            return best;
        }

        private static float FromSource(Wave wave, int i, float x, float z)
        {
            Vector2 from = wave.source[i];
            return wave.sourceDistance[i] + Mathf.Sqrt((x - from.x) * (x - from.x) + (z - from.y) * (z - from.y));
        }

        /// <summary>The three passes at one point: magenta just ahead, lavender, cyan just behind.</summary>
        private static Color Shade(Wave wave, float front, float d)
        {
            // Weaker with distance, and gone over the last EdgeFade cells before the reach: the
            // reached cells end in whole-cell steps, which would show if the band were still lit there.
            float fade = (1f - 0.6f * (d / wave.reach) * (d / wave.reach)) * Smooth(Mathf.Clamp01((wave.reach - d) / EdgeFade));
            float k = fade * wave.strength;
            float m = Pass(front + Fringe - d) * 0.35f * k;
            float l = Pass(front - d) * 0.45f * k;
            float c = Pass(front - Fringe - d) * 0.35f * k;
            return new Color(Magenta.r * m + Lavender.r * l + Cyan.r * c,
                Magenta.g * m + Lavender.g * l + Cyan.g * c,
                Magenta.b * m + Lavender.b * l + Cyan.b * c, 1f);
        }

        /// <summary>
        /// Brightness a distance <paramref name="behind"/> the front: a quick soft rise just ahead of
        /// it, full at the front, fading over <see cref="Band"/>, in ripples <see cref="Ridge"/> apart.
        /// </summary>
        private static float Pass(float behind)
        {
            if (behind < -0.5f || behind > Band) return 0f;
            float envelope = behind < 0f ? 1f + behind / 0.5f : 1f - behind / Band;
            envelope *= envelope;
            float ripple = 0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * Mathf.Max(0f, behind) / Ridge);
            return envelope * ripple;
        }

        private static Material bandMaterial;

        /// <summary>Vertex colours, added onto what is below. Made on first use, on the main thread.</summary>
        private static Material BandMaterial
        {
            get
            {
                if (bandMaterial != null) return bandMaterial;
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null) return null;
                bandMaterial = new Material(shader) { renderQueue = 3000 };
                bandMaterial.SetInt("_SrcBlend", (int)BlendMode.One);
                bandMaterial.SetInt("_DstBlend", (int)BlendMode.One);
                bandMaterial.SetInt("_Cull", (int)CullMode.Off);
                bandMaterial.SetInt("_ZWrite", 0);
                return bandMaterial;
            }
        }

        /// <summary>
        /// Purple-white crackle round a listener: a glow under it and four jagged arcs round its body,
        /// redrawn every 3 ticks so they flicker, fading out over <see cref="CrackleSeconds"/>.
        /// </summary>
        private void DrawCrackle(Pawn pawn, float since, int index)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map != map) return;
            Vector2 at = Ground(pawn.DrawPos);
            float life = 1f - since / CrackleSeconds;
            VfxDraw.Sprite(at, 1.9f, 1.9f, VfxDraw.Fade(Purple, 0.6f * life), VfxDraw.glow, VfxDraw.Overhead);
            int flicker = Find.TickManager.TicksGame / 3;
            for (int b = 0; b < 4; b++)
            {
                int seed = pawn.thingIDNumber * 97 + index * 13 + b * 7 + flicker * 131;
                if (Rand(seed) < 0.2f) continue;
                float start = b * 90f + Rand(seed + 1) * 60f;
                float sweep = 50f + Rand(seed + 2) * 50f;
                for (int p = 0; p < BoltPoints; p++)
                {
                    float angle = start + sweep * p / (BoltPoints - 1);
                    float radius = 0.55f + (Rand(seed + 10 + p) - 0.5f) * 0.35f;
                    bolt[p] = at + VfxDraw.Turn(angle) * radius;
                }
                Bolt(0.2f, VfxDraw.Fade(Purple, 0.9f * life));
                Bolt(0.06f, VfxDraw.Fade(Color.white, life));
            }
        }

        private const int BoltPoints = 6;
        private static readonly Vector2[] bolt = new Vector2[BoltPoints];

        /// <summary>One even-width ribbon along <see cref="bolt"/>, flat white additive, so it reads as a line of light.</summary>
        private static void Bolt(float width, Color colour)
        {
            VfxDraw.Sides(BoltPoints, out Vector2[] left, out Vector2[] right);
            for (int p = 0; p < BoltPoints; p++)
            {
                Vector2 along = bolt[Mathf.Min(p + 1, BoltPoints - 1)] - bolt[Mathf.Max(p - 1, 0)];
                Vector2 normal = new Vector2(-along.y, along.x).normalized * (width / 2f);
                left[p] = bolt[p] + normal;
                right[p] = bolt[p] - normal;
            }
            VfxDraw.Strip(left, right, colour, VfxDraw.whiteGlow, VfxDraw.Overhead);
        }

        private static Vector2 Ground(Vector3 v) => new Vector2(v.x, v.z);
    }
}
