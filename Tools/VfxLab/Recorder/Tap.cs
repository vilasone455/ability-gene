using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
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

        public static void BeginFrame()
        {
            calls = new List<Call>();
            events = new List<GameEvent>();
        }

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

    public sealed class Frame
    {
        public float wall;
        public float? clock;
        public readonly List<(string mesh, Call call)> calls = new();
        public List<GameEvent> events;
        public int hash;
    }

    public sealed class Recording
    {
        public string label, kit, slug;
        public readonly List<Frame> frames = new();
        public readonly Dictionary<string, (Vector3[] v, Vector2[] uv, int[] tri, string name)> meshes = new();
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
                    keys[call.mesh] = key = call.mesh.id + "#" + ContentHash(call.mesh).ToString("x8");
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
            frame.hash = hash.ToHashCode();
            frames.Add(frame);
            return frame;
        }

        private static int ContentHash(Mesh mesh)
        {
            var hash = new HashCode();
            foreach (Vector3 v in mesh.vertices) { hash.Add(v.x); hash.Add(v.z); }
            if (mesh.uv != null) foreach (Vector2 v in mesh.uv) { hash.Add(v.x); hash.Add(v.y); }
            foreach (int i in mesh.triangles) hash.Add(i);
            return hash.ToHashCode();
        }

        public float Seconds => frames.Count / 60f;

        public void Write(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using var stream = File.Create(path);
            using var json = new Utf8JsonWriter(stream);
            json.WriteStartObject();
            json.WriteString("label", label);
            json.WriteString("kit", kit);
            json.WriteString("source", "recorded");
            json.WriteNumber("fps", 60);
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
                json.WriteEndArray();
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
