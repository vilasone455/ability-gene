using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using static RimArt.CastleEffectGraphics;

namespace RimArt
{
    /// <summary>
    /// Shift, the castle command: a room slides straight until its wall touches another room, at most
    /// shiftMaxCells, with everything in it riding along. Its doorways close as the rooms part and a new
    /// doorway opens where it now touches a room along 3 or more cells.
    ///
    /// The cells move once, at the strum, and only the picture slides: the room's floor, walls and
    /// lanterns are rebuilt at the destination at once (with the room's things and pawns), and for the
    /// slide's length the room is drawn on its own, from where it was to where it is, with its riders
    /// drawn behind their real cells by the same offset (<see cref="Patch_PawnRenderer_CastleRide"/>) and
    /// stunned so nobody walks off a room whose picture has not arrived. The picture runs on the map's
    /// own clock, real seconds whether or not the game is paused, as the lanterns do: a command is played
    /// from a paused game as often as not. A save made mid-slide loads with the move done and the
    /// picture skipped. The one thing left until the stop is the wall in the far
    /// cell of each new doorway, so the neighbours cannot step into a doorway before it is drawn open.
    ///
    /// The Void rule: a pawn standing in either cell of a doorway that breaks is left over the void. It
    /// drops, and comes up in a random room through a floor door, unharmed but stunned a moment.
    ///
    /// Numbers are on the AG_InfinityCastle def (<see cref="InfinityCastleRules"/>); the timings that
    /// shape the picture are the Shift sketch's (Tools/VfxLab/web/sketches/infinity-castle-shift.js).
    /// </summary>
    public sealed class CastleShift : IExposable
    {
        public int room, dx, dz, distance;
        /// <summary>When the strum was, on the castle map's clock (real seconds since the castle opened).</summary>
        public float startAt;
        public bool blocked;
        /// <summary>Where the room was, so a loaded castle can still finish the stop's bookkeeping.</summary>
        public int oldX, oldZ;
        /// <summary>The far cell of each new doorway: its wall comes out at the stop.</summary>
        public List<IntVec3> madeCells = new List<IntVec3>();
        public List<Pawn> riders = new List<Pawn>();
        public bool stopped;

        // The picture, not saved: a castle loaded mid-slide finishes without it.
        internal List<CastleDoorway> broken = new List<CastleDoorway>(), made = new List<CastleDoorway>();
        internal CastleRoomGraphics.CastleBatch movingRoom;
        internal (Vector2 a, Vector2 b)? seam;
        internal bool picture, shook;

        // The Shift sketch's order, in seconds from the strum.
        public const float Close0 = 0.05f, Slide0 = 0.25f, CloseFor = 0.15f, OpenAfter = 0.15f, OpenFor = 0.2f, DustFor = 0.7f;

        public float SlideTime => 0.25f + distance / InfinityCastleRules.Of.shiftCellsPerSecond;
        public float StopAt => Slide0 + SlideTime;
        public float OpenAt => StopAt + OpenAfter;
        public float EndAt => OpenAt + OpenFor + DustFor;

        public float AgeAt(float now) => now - startAt;

        /// <summary>How far the room has slid at <paramref name="t"/>: speeding up into a wall it hits, easing to a stop in open void.</summary>
        public float Slid(float t)
        {
            float u = Clamp((t - Slide0) / SlideTime);
            return distance * (blocked ? Mathf.Pow(u, 1.5f) : VfxMath.Smooth(u));
        }

        /// <summary>The picture's offset from the real cells at <paramref name="t"/>: zero once the room has arrived.</summary>
        public Vector2 Behind(float t)
        {
            float back = Slid(t) - distance;
            return new Vector2(dx * back, dz * back);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref room, "room");
            Scribe_Values.Look(ref dx, "dx");
            Scribe_Values.Look(ref dz, "dz");
            Scribe_Values.Look(ref distance, "distance");
            Scribe_Values.Look(ref startAt, "startAt");
            Scribe_Values.Look(ref blocked, "blocked");
            Scribe_Values.Look(ref oldX, "oldX");
            Scribe_Values.Look(ref oldZ, "oldZ");
            Scribe_Values.Look(ref stopped, "stopped");
            Scribe_Collections.Look(ref madeCells, "madeCells", LookMode.Value);
            Scribe_Collections.Look(ref riders, "riders", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                madeCells = madeCells ?? new List<IntVec3>();
                riders = riders ?? new List<Pawn>();
                riders.RemoveAll(p => p == null);
                picture = false;
                startAt = -1000f;                                   // long over: the stop happens on the first update
            }
        }
    }

    /// <summary>
    /// Crush, the castle command: a room's four walls slam in and draw back, and everyone in the outer
    /// band of its floor is hit. The hit lands at <see cref="Hit"/> after the strum; the picture (the
    /// band lit, the slabs, dust, splinters, the stun stars) is the Crush sketch's. A castle loaded with a
    /// crush in flight lands the hit at once and skips the picture.
    /// </summary>
    public sealed class CastleCrush : IExposable
    {
        public int room;
        public float startAt;
        public bool hitDone;

        // The Crush sketch's order, in seconds from the strum.
        public const float Mark = 0.05f, Telegraph = 0.3f, SlamFor = 0.12f, HoldFor = 0.3f, BackFor = 0.8f;
        public const float Slam = Mark + Telegraph, Hit = Slam + SlamFor, Back = Hit + HoldFor, Open = Back + BackFor;
        /// <summary>Over when the walls are back and the last stun star has gone (1 s from the hit).</summary>
        public const float Length = Open + 0.2f;

        public float AgeAt(float now) => now - startAt;

        /// <summary>How far in the walls are at <paramref name="t"/>, 0..1: speeding up into the hit, a short hold, easing back.</summary>
        public static float In(float t)
        {
            if (t < Slam) return 0f;
            if (t < Hit) return Mathf.Pow((t - Slam) / SlamFor, 2f);
            if (t < Back) return 1f;
            return 1f - VfxMath.Smooth((t - Back) / BackFor);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref room, "room");
            Scribe_Values.Look(ref startAt, "startAt");
            Scribe_Values.Look(ref hitDone, "hitDone");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) startAt = -1000f;
        }
    }

    /// <summary>A Seal or Open being drawn: which doorway, which way, and when the strum was. Not saved; a loaded seal is simply shut.</summary>
    internal sealed class CastleSealAnim
    {
        public string key;
        public CastleDoorway door;
        public bool sealing;
        public float startAt;
        /// <summary>Both pictures are over within this: the bar's 0.25 s from 0.2 s, plus the outline's fade.</summary>
        public const float Length = 0.6f;
    }

    /// <summary>
    /// A pawn going through the floor: Drop (the carrier picks the pawn and the room) and the Void rule
    /// (a random room). The move is at once; the picture is the Drop sketch's: a door opens under the
    /// pawn <see cref="doorUnder"/> after the strum, the pawn sinks for 0.4 s and is gone, a door opens
    /// in the far room 0.2 s later and the pawn rises out of it over 0.55 s. While it sinks the pawn is
    /// drawn back at its old cell; in between it is not drawn at all.
    /// </summary>
    public sealed class CastleDrop : IExposable
    {
        public Pawn pawn;
        public IntVec3 from, to;
        public int fromRoom = -1, toRoom = -1;
        public float startAt;
        /// <summary>Seconds after the strum before the door opens: 0.1 for Drop, 0 for the Void rule.</summary>
        public float doorUnder;
        /// <summary>Whether this was a strum (Drop): the rings from the dais and the room's flash.</summary>
        public bool strum;

        public const float Sink = 0.4f, Rise = 0.55f;
        public float SinkStart => doorUnder + DoorThrough;
        public float SinkEnd => SinkStart + Sink;
        public float Arrive => SinkEnd + 0.2f;
        public float AgeAt(float now) => now - startAt;
        public bool DoneAt(float now) => AgeAt(now) > Arrive + DoorEnd(Rise * 0.75f);

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref from, "from");
            Scribe_Values.Look(ref to, "to");
            Scribe_Values.Look(ref fromRoom, "fromRoom", -1);
            Scribe_Values.Look(ref toRoom, "toRoom", -1);
            Scribe_Values.Look(ref startAt, "startAt");
            Scribe_Values.Look(ref doorUnder, "doorUnder");
            Scribe_Values.Look(ref strum, "strum");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) startAt = -1000f;
        }
    }

    /// <summary>
    /// Where the castle's riders are drawn: each pawn's picture offset from its real cell while its room
    /// slides, and the pawns hidden while they fall through the void. Read by the render patch every
    /// frame, written by the castle's map component; static because there is one renderer.
    /// </summary>
    internal static class InfinityCastleRide
    {
        private static readonly Dictionary<Pawn, Vector3> offsets = new Dictionary<Pawn, Vector3>();
        private static readonly HashSet<Pawn> hidden = new HashSet<Pawn>();

        public static int Count => offsets.Count + hidden.Count;
        public static void Ride(Pawn pawn, Vector2 behind) { hidden.Remove(pawn); offsets[pawn] = new Vector3(behind.x, 0f, behind.y); }
        public static void Hide(Pawn pawn) { offsets.Remove(pawn); hidden.Add(pawn); }
        public static void Release(Pawn pawn) { offsets.Remove(pawn); hidden.Remove(pawn); }
        public static void Clear() { offsets.Clear(); hidden.Clear(); }
        public static bool Hidden(Pawn pawn) => hidden.Contains(pawn);
        public static bool Offset(Pawn pawn, out Vector3 offset) => offsets.TryGetValue(pawn, out offset);
    }

    /// <summary>
    /// The game draws every pawn from results computed for where the pawn really is, so a rider is moved
    /// by changing the position every phase is asked for. A falling pawn is not drawn at all.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.DynamicDrawPhaseAt))]
    internal static class Patch_PawnRenderer_CastleRide
    {
        private static bool Prefix(Pawn ___pawn, ref Vector3 drawLoc)
        {
            if (InfinityCastleRide.Count == 0 || ___pawn == null) return true;
            if (InfinityCastleRide.Hidden(___pawn)) return false;
            if (InfinityCastleRide.Offset(___pawn, out Vector3 offset)) drawLoc += offset;
            return true;
        }
    }
}
