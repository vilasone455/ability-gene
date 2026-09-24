using System.Reflection;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// One camera move, written as the VFX lab's camera events ({ t, type: 'camera', over, zoom, pan, x, z },
    /// Tools/VfxLab/web/js/camera.js cameraMoveAt): from <see cref="At"/> it eases over <see cref="Over"/>
    /// seconds from where the moves before it got to, to <see cref="Zoom"/> times closer and
    /// <see cref="Pan"/> of the way from where the player had the camera to the point <see cref="X"/>,
    /// <see cref="Z"/> cells from the effect's cell. NaN keeps the value the moves before it reached.
    /// </summary>
    internal readonly struct CameraEvent
    {
        public readonly float At, Over, Zoom, Pan, X, Z;

        public CameraEvent(float at, float over, float zoom, float pan, float x = float.NaN, float z = float.NaN)
        {
            At = at;
            Over = over;
            Zoom = zoom;
            Pan = pan;
            X = x;
            Z = z;
        }
    }

    /// <summary>
    /// Plays camera events on the game's camera: remembers where the player had it, sets its position
    /// and size every frame while the moves run, and gives it back when they end, when the effect is
    /// cleared, or before the next one starts. Only call it while its map is the one on screen: the
    /// game has one camera, and on another map it is that map's.
    ///
    /// The camera's own position and size are private fields of CameraDriver (rootPos, rootSize),
    /// read by reflection; if a RimWorld update renames them the move is skipped with one warning and
    /// the effect plays without it. Size is how many cells the view is tall, halved, so zoom divides it,
    /// never below the closest zoom the map allows.
    /// </summary>
    internal sealed class CameraMove
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo RootPos = typeof(CameraDriver).GetField("rootPos", Fields);
        private static readonly FieldInfo RootSize = typeof(CameraDriver).GetField("rootSize", Fields);
        private static bool warned;

        private readonly CameraEvent[] events;
        private Vector3 savedPos;
        private float savedSize;
        private bool holding;

        /// <param name="events">In time order.</param>
        internal CameraMove(CameraEvent[] events) => this.events = events;

        /// <summary>When the last move has finished and the camera is where the player had it.</summary>
        internal float EndsAt => events.Length == 0 ? 0f : events[events.Length - 1].At + events[events.Length - 1].Over;

        /// <summary>Remembers where the player has the camera now, giving back one still held first.</summary>
        internal void Begin()
        {
            Release();
            if (events.Length == 0 || !Readable()) return;
            CameraDriver camera = Find.CameraDriver;
            savedPos = (Vector3)RootPos.GetValue(camera);
            savedSize = (float)RootSize.GetValue(camera);
            holding = true;
        }

        /// <summary>The camera at <paramref name="seconds"/> into the effect played on <paramref name="cell"/> (its centre).</summary>
        internal void Apply(Vector2 cell, float seconds)
        {
            if (!holding) return;
            if (seconds >= EndsAt)
            {
                Release();
                return;
            }
            MoveAt(seconds, out float pan, out float zoom, out float x, out float z);
            var focus = new Vector3(cell.x + x, savedPos.y, cell.y + z);
            CameraDriver camera = Find.CameraDriver;
            float size = Mathf.Max(savedSize / zoom, camera.config.sizeRange.min);
            camera.SetRootPosAndSize(savedPos + (focus - savedPos) * pan, size);
        }

        /// <summary>Gives the camera back where the player had it, if it is still held.</summary>
        internal void Release()
        {
            if (!holding) return;
            holding = false;
            Find.CameraDriver.SetRootPosAndSize(savedPos, savedSize);
        }

        /// <summary>The lab's cameraMoveAt: each started move eases from where the ones before it got to.</summary>
        private void MoveAt(float t, out float pan, out float zoom, out float x, out float z)
        {
            pan = 0f;
            zoom = 1f;
            x = 0f;
            z = 0f;
            int started = 0;
            while (started < events.Length && events[started].At <= t) started++;
            for (int i = 0; i < started; i++)
            {
                CameraEvent e = events[i];
                float until = i + 1 < started ? events[i + 1].At : t;
                float u = Mathf.Clamp01((until - e.At) / Mathf.Max(1e-6f, e.Over)), k = u * u * (3f - 2f * u);
                float toPan = float.IsNaN(e.Pan) ? pan : e.Pan, toZoom = float.IsNaN(e.Zoom) ? zoom : e.Zoom;
                float toX = float.IsNaN(e.X) ? x : e.X, toZ = float.IsNaN(e.Z) ? z : e.Z;
                pan += (toPan - pan) * k;
                zoom *= Mathf.Pow(toZoom / zoom, k);
                x += (toX - x) * k;
                z += (toZ - z) * k;
            }
        }

        private static bool Readable()
        {
            if (RootPos != null && RootSize != null && RootPos.FieldType == typeof(Vector3) && RootSize.FieldType == typeof(float)) return true;
            if (!warned)
            {
                warned = true;
                Log.Warning("[RimArt] CameraDriver has no rootPos/rootSize fields in this RimWorld version; camera moves are skipped.");
            }
            return false;
        }
    }
}
