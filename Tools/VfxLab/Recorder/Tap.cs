using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using RimArt;
using UnityEngine;

namespace RimArt.VfxLab
{
    public sealed class Call
    {
        public Mesh mesh;
        public Matrix4x4 matrix;
        public Material material;
        public Color colour;
        public float? age;
    }

    public sealed class GameEvent
    {
        public string type, def;
        public float value;
    }

    /// <summary>
    /// Where Graphics.DrawMesh, CameraShaker.DoShake and SoundStarter.PlayOneShot land. It only
    /// collects: turning a frame into data is <see cref="Recording"/>'s job, done at end of frame.
    /// </summary>
    public static class Tap
    {
        public static List<Call> calls = new();
        public static List<GameEvent> events = new();
        /// <summary>The cutscene frame a preview handed to <see cref="UbwShot.Sink"/> this frame, read at end of frame as its meshes are.</summary>
        internal static UbwShot shot;

        public static void BeginFrame()
        {
            calls = new List<Call>();
            events = new List<GameEvent>();
            shot = null;
        }

        internal static void Shot(UbwShot frame) => shot = frame;

        public static void Draw(Mesh mesh, Matrix4x4 matrix, Material material, MaterialPropertyBlock properties)
        {
            if (mesh == null || material == null) return;
            // The property block is copied now, as Unity copies it when DrawMesh is called; the
            // mesh is read later, at end of frame, as Unity reads it when the frame renders.
            calls.Add(new Call
            {
                mesh = mesh,
                matrix = matrix,
                material = material,
                colour = properties?.colour ?? material.color,
                age = properties?.age,
            });
        }

        public static void Event(string type, float value, string def) =>
            events.Add(new GameEvent { type = type, value = value, def = def });
    }

    /// <summary>A cutscene frame's camera (or Flat: the map camera, at the game framing) and its boxes over the screen.</summary>
    public sealed class Shot3
    {
        public bool flat;
        public float x, y, z, pitch, fov, near, far, blend, cx, cz, cellsTall;
        public readonly List<(Rect box, Color colour)> overlays = new();
    }

    public sealed class Frame
    {
        public float wall;
        public float? clock;
        public readonly List<(string mesh, Call call)> calls = new();
        public Shot3 shot;
        internal readonly List<(string mesh, UbwDraw3 draw)> draws = new();
        public List<GameEvent> events;
        public int hash;
    }

    public sealed class Recording
    {
        public string label, kit, slug;
        public readonly List<Frame> frames = new();
        public readonly Dictionary<string, (Vector3[] v, Vector2[] uv, int[] tri, string name)> meshes = new();
        internal readonly Dictionary<string, (Vector3[] xyz, Vector2[] game, Vector2[] uv, int[] tri, string name)> meshes3 = new();
        public readonly Dictionary<int, Material> materials = new();
        public readonly List<(string name, float wall)> phases = new();
        public bool still;

        /// <summary>Snapshots every mesh the frame drew, in the state it ends the frame in.</summary>
        public Frame Capture(float wall, float? clock)
        {
            var frame = new Frame { wall = wall, clock = clock, events = Tap.events };
            var hash = new HashCode();
            var keys = new Dictionary<Mesh, string>();
            foreach (Call call in Tap.calls)
            {
                // Keyed by content, not by write count: a kit that rewrites identical vertices
                // every frame still stores the shape once, and a frozen preview stays still.
                if (!keys.TryGetValue(call.mesh, out string key))
                {
                    // System.HashCode is seeded per process, so two shapes of one pooled mesh can share a hash in one
                    // run and not the next; a key already holding another shape gets a suffix, or that frame would
                    // replay the stored shape (seen once as one wrong strip in Sato's tear).
                    string first = call.mesh.id + "#" + ContentHash(call.mesh).ToString("x8");
                    key = first;
                    for (int n = 1; meshes.TryGetValue(key, out var kept) && !Same(kept.v, kept.uv, kept.tri, call.mesh); n++)
                        key = first + "-" + n;
                    keys[call.mesh] = key;
                }
                if (!meshes.ContainsKey(key))
                    meshes[key] = ((Vector3[])call.mesh.vertices.Clone(), (Vector2[])call.mesh.uv?.Clone(),
                        (int[])call.mesh.triangles.Clone(), call.mesh.name);
                materials[call.material.id] = call.material;
                frame.calls.Add((key, call));
                hash.Add(key);
                hash.Add(call.material.id);
                hash.Add(call.matrix.position.x); hash.Add(call.matrix.position.y); hash.Add(call.matrix.position.z);
                hash.Add(call.matrix.rotation); hash.Add(call.matrix.scale.x); hash.Add(call.matrix.scale.z);
                hash.Add(call.colour.r); hash.Add(call.colour.g); hash.Add(call.colour.b); hash.Add(call.colour.a);
            }
            if (Tap.shot != null)
            {
                UbwShot s = Tap.shot;
                frame.shot = new Shot3
                {
                    flat = s.Flat, x = s.Eye.x, y = s.Eye.y, z = s.Eye.z, pitch = s.Pitch, fov = s.Fov, near = s.Near, far = s.Far, blend = s.Blend,
                    cx = s.GameCentre.x, cz = s.GameCentre.y, cellsTall = s.CellsTall,
                };
                frame.shot.overlays.AddRange(s.Overlays);
                foreach (UbwDraw3 d in s.Draws)
                {
                    string first = d.Mesh.Name + "#" + ContentHash3(d.Mesh).ToString("x8"), key = first;
                    for (int n = 1; meshes3.TryGetValue(key, out var kept) && !Same3(kept, d.Mesh); n++)
                        key = first + "-" + n;
                    if (!meshes3.ContainsKey(key))
                        meshes3[key] = ((Vector3[])d.Mesh.Xyz.Clone(), (Vector2[])d.Mesh.Game.Clone(), (Vector2[])d.Mesh.Uv.Clone(), (int[])d.Mesh.Tri.Clone(), d.Mesh.Name);
                    materials[d.Material.id] = d.Material;
                    frame.draws.Add((key, d));
                    hash.Add(key);
                }
                hash.Add(s.Eye.x); hash.Add(s.Eye.y); hash.Add(s.Eye.z); hash.Add(s.Pitch); hash.Add(s.Blend); hash.Add(s.Overlays.Count);
            }
            frame.hash = hash.ToHashCode();
            frames.Add(frame);
            return frame;
        }

        private static bool Same(Vector3[] v, Vector2[] uv, int[] tri, Mesh mesh)
        {
            Vector3[] mv = mesh.vertices;
            Vector2[] muv = mesh.uv;
            if (v.Length != mv.Length || tri.Length != mesh.triangles.Length || (uv == null) != (muv == null)) return false;
            for (int i = 0; i < v.Length; i++) if (v[i].x != mv[i].x || v[i].y != mv[i].y || v[i].z != mv[i].z) return false;
            if (uv != null)
            {
                if (uv.Length != muv.Length) return false;
                for (int i = 0; i < uv.Length; i++) if (uv[i].x != muv[i].x || uv[i].y != muv[i].y) return false;
            }
            return tri.AsSpan().SequenceEqual(mesh.triangles);
        }

        private static bool Same3((Vector3[] xyz, Vector2[] game, Vector2[] uv, int[] tri, string name) kept, UbwMesh3 mesh)
        {
            if (kept.xyz.Length != mesh.Xyz.Length || kept.game.Length != mesh.Game.Length || kept.uv.Length != mesh.Uv.Length) return false;
            for (int i = 0; i < kept.xyz.Length; i++)
                if (kept.xyz[i].x != mesh.Xyz[i].x || kept.xyz[i].y != mesh.Xyz[i].y || kept.xyz[i].z != mesh.Xyz[i].z) return false;
            for (int i = 0; i < kept.game.Length; i++) if (kept.game[i].x != mesh.Game[i].x || kept.game[i].y != mesh.Game[i].y) return false;
            for (int i = 0; i < kept.uv.Length; i++) if (kept.uv[i].x != mesh.Uv[i].x || kept.uv[i].y != mesh.Uv[i].y) return false;
            return kept.tri.AsSpan().SequenceEqual(mesh.Tri);
        }

        private static int ContentHash3(UbwMesh3 mesh)
        {
            var hash = new HashCode();
            foreach (Vector3 v in mesh.Xyz) { hash.Add(v.x); hash.Add(v.y); hash.Add(v.z); }
            foreach (Vector2 v in mesh.Game) { hash.Add(v.x); hash.Add(v.y); }
            foreach (Vector2 v in mesh.Uv) { hash.Add(v.x); hash.Add(v.y); }
            foreach (int i in mesh.Tri) hash.Add(i);
            return hash.ToHashCode();
        }

        private static int ContentHash(Mesh mesh)
        {
            var hash = new HashCode();
            foreach (Vector3 v in mesh.vertices) { hash.Add(v.x); hash.Add(v.z); }
            if (mesh.uv != null) foreach (Vector2 v in mesh.uv) { hash.Add(v.x); hash.Add(v.y); }
            foreach (int i in mesh.triangles) hash.Add(i);
            return hash.ToHashCode();
        }

        /// <summary>Frames a second kept (60 unless the kit keeps fewer).</summary>
        public float fps = 60f;
        public float Seconds => frames.Count / fps;

        public void Write(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using var stream = File.Create(path);
            using var json = new Utf8JsonWriter(stream);
            json.WriteStartObject();
            json.WriteString("label", label);
            json.WriteString("kit", kit);
            json.WriteString("source", "recorded");
            Num(json, "fps", fps);
            json.WriteBoolean("still", still);

            json.WriteStartArray("phases");
            foreach (var (name, wall) in phases)
            {
                json.WriteStartObject();
                json.WriteString("name", name);
                Num(json, "t", wall);
                json.WriteEndObject();
            }
            json.WriteEndArray();

            json.WriteStartObject("materials");
            foreach (var (id, m) in materials)
            {
                json.WriteStartObject(id.ToString());
                json.WriteString("shader", m.shader?.name ?? "Transparent");
                json.WriteString("tex", (m.mainTexture as Texture2D)?.path ?? "white");
                json.WriteStartObject("textures");
                foreach (var (k, v) in m.textures) json.WriteString(k, v);
                json.WriteEndObject();
                json.WriteStartObject("floats");
                foreach (var (k, v) in m.floats) Num(json, k, v);
                json.WriteEndObject();
                json.WriteEndObject();
            }
            json.WriteEndObject();

            json.WriteStartObject("meshes");
            foreach (var (key, mesh) in meshes)
            {
                json.WriteStartObject(key);
                json.WriteString("name", mesh.name);
                json.WriteStartArray("v");
                foreach (Vector3 p in mesh.v) { Num(json, p.x); Num(json, p.z); }
                json.WriteEndArray();
                if (mesh.uv != null && mesh.uv.Length == mesh.v.Length)
                {
                    json.WriteStartArray("uv");
                    foreach (Vector2 p in mesh.uv) { Num(json, p.x); Num(json, p.y); }
                    json.WriteEndArray();
                }
                json.WriteStartArray("tri");
                foreach (int i in mesh.tri) json.WriteNumberValue(i);
                json.WriteEndArray();
                json.WriteEndObject();
            }
            // A cutscene's meshes: v3 the 3D places, game the game positions (the lab's Mesh.setXYZ + setGame); the lab takes v from game.
            foreach (var (key, mesh) in meshes3)
            {
                json.WriteStartObject(key);
                json.WriteString("name", mesh.name);
                json.WriteStartArray("v3");
                foreach (Vector3 p in mesh.xyz) { Num(json, p.x); Num(json, p.y); Num(json, p.z); }
                json.WriteEndArray();
                json.WriteStartArray("game");
                foreach (Vector2 p in mesh.game) { Num(json, p.x); Num(json, p.y); }
                json.WriteEndArray();
                json.WriteStartArray("uv");
                foreach (Vector2 p in mesh.uv) { Num(json, p.x); Num(json, p.y); }
                json.WriteEndArray();
                json.WriteStartArray("tri");
                foreach (int i in mesh.tri) json.WriteNumberValue(i);
                json.WriteEndArray();
                json.WriteEndObject();
            }
            json.WriteEndObject();

            json.WriteStartArray("frames");
            foreach (Frame frame in frames)
            {
                json.WriteStartObject();
                Num(json, "t", frame.wall);
                if (frame.clock.HasValue) Num(json, "clock", frame.clock.Value);
                json.WriteStartArray("calls");
                foreach (var (key, call) in frame.calls)
                {
                    // [mesh, material, x, altitude, z, rotation, scaleX, scaleZ, r, g, b, a, age?]
                    json.WriteStartArray();
                    json.WriteStringValue(key);
                    json.WriteNumberValue(call.material.id);
                    Num(json, call.matrix.position.x); Num(json, call.matrix.position.y); Num(json, call.matrix.position.z);
                    Num(json, call.matrix.rotation); Num(json, call.matrix.scale.x); Num(json, call.matrix.scale.z);
                    Num(json, call.colour.r); Num(json, call.colour.g); Num(json, call.colour.b); Num(json, call.colour.a);
                    if (call.age.HasValue) Num(json, call.age.Value);
                    json.WriteEndArray();
                }
                // A cutscene's draws: [mesh, material, x, y, z, 0, 1, 1, r, g, b, a, 0, sy, flags]; flags 1 drawn where the game
                // view draws it, 2 writes no depth, 4 never depth tested.
                foreach (var (key, d) in frame.draws)
                {
                    json.WriteStartArray();
                    json.WriteStringValue(key);
                    json.WriteNumberValue(d.Material.id);
                    Num(json, d.At.x); Num(json, d.At.y); Num(json, d.At.z);
                    Num(json, 0f); Num(json, 1f); Num(json, 1f);
                    Num(json, d.Colour.r); Num(json, d.Colour.g); Num(json, d.Colour.b); Num(json, d.Colour.a);
                    Num(json, 0f);
                    Num(json, d.Sy);
                    json.WriteNumberValue((d.Screen ? 1 : 0) | (d.Depth == UbwDepth.NoWrite ? 2 : 0) | (d.Depth == UbwDepth.Over ? 4 : 0));
                    json.WriteEndArray();
                }
                json.WriteEndArray();
                if (frame.shot != null)
                {
                    Shot3 c = frame.shot;
                    json.WriteStartObject("camera");
                    json.WriteBoolean("flat", c.flat);
                    Num(json, "x", c.x); Num(json, "y", c.y); Num(json, "z", c.z); Num(json, "pitch", c.pitch); Num(json, "fov", c.fov);
                    Num(json, "near", c.near); Num(json, "far", c.far); Num(json, "blend", c.blend);
                    Num(json, "cx", c.cx); Num(json, "cz", c.cz); Num(json, "cellsTall", c.cellsTall);
                    json.WriteEndObject();
                    json.WriteStartArray("overlays");
                    foreach (var (box, colour) in c.overlays)
                    {
                        json.WriteStartObject();
                        Num(json, "x", box.x); Num(json, "y", box.y); Num(json, "w", box.width); Num(json, "h", box.height);
                        Num(json, "r", colour.r); Num(json, "g", colour.g); Num(json, "b", colour.b); Num(json, "a", colour.a);
                        json.WriteEndObject();
                    }
                    json.WriteEndArray();
                }
                if (frame.events.Count > 0)
                {
                    json.WriteStartArray("events");
                    foreach (GameEvent e in frame.events)
                    {
                        json.WriteStartObject();
                        json.WriteString("type", e.type);
                        if (e.def != null) json.WriteString("def", e.def);
                        Num(json, "value", e.value);
                        json.WriteEndObject();
                    }
                    json.WriteEndArray();
                }
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }

        private static void Num(Utf8JsonWriter json, float v) =>
            json.WriteNumberValue(float.IsFinite(v) ? Math.Round((double)v, 4) : 0d);

        private static void Num(Utf8JsonWriter json, string name, float v) =>
            json.WriteNumber(name, float.IsFinite(v) ? Math.Round((double)v, 4) : 0d);
    }
}
