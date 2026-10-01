using System.Collections.Generic;
using UnityEngine;
using Verse;
using T = RimArt.EgoParadiseLostTiming;

namespace RimArt
{
    /// <summary>The sketch's four scenes ("Show").</summary>
    public enum EgoParadiseLostScene { RoomHit, RoomHitOutdoors, Corroded, Overclock }

    /// <summary>One pawn of a preview scene. Positions are cells from the wielder's cell centre.</summary>
    public sealed class EgoParadiseLostPerson
    {
        public Vector2 Start;
        public bool Hostile, Walks;
        /// <summary>When it is hit, on the scene's clock, in order.</summary>
        public readonly List<float> Hits = new List<float>();
    }

    /// <summary>One shot of the room hit: when it leaves the staff, when its thorns rise, the pawn aimed at (index into People) and how many pawns it hit.</summary>
    public struct EgoParadiseLostShot
    {
        public float At, HitAt;
        public int Aimed, Count;
    }

    /// <summary>
    /// The preview's script, the sketch's plan(): who stands where, who walks in, who is hit when, when the
    /// rings fire and when the scene ends. Nothing here is a rule. Which pawns get thorns (every hostile in
    /// the aimed pawn's room, or within the outdoors radius of it; every pawn inside a ring, or only hostiles
    /// when overclocked) is worked out for the sketch's stand-ins so the preview can pass their positions to
    /// the pictures; in game the rules decide it.
    ///
    /// Room hit: three raiders walk in from the east at 0.8 cells/s and stop 1.6 cells from the wielder, a
    /// fourth stands in the next room (outdoors: 7 cells behind the wielder), a colonist stands in the room.
    /// Three shots 2 s apart from 0.4 s; each pawn hit walks at 40 % speed for 1 s. Corroded: three rings
    /// 2.5 s apart (the rule's 6 s cut short) from 0.9 s, every pawn within 6 cells hit as the edge passes.
    /// Overclock: three rings 2 s apart, hostiles only.
    /// </summary>
    public sealed class EgoParadiseLostScript
    {
        /// <summary>The showcase: the first shot at Lead s, the result held Hold s; rings ScriptRingInterval s apart when corroded.</summary>
        public const float Lead = 0.4f, Hold = 1.2f, ScriptRingInterval = 2.5f;
        /// <summary>The raiders walk in at WalkSpeed cells/s and stop StopAt cells from the wielder.</summary>
        public const float WalkSpeed = 0.8f, StopAt = 1.6f;
        /// <summary>The showcase's shots, raiders in the room, and rings when corroded (the rule's 30 s would be five).</summary>
        public const int Shots = 3, Raiders = 3, Rings = 3;
        /// <summary>The room's floor: cells x -2..6, z -3..3 from the wielder's cell; a pawn is in it up to half a cell past those.</summary>
        public const float RoomX0 = -2f, RoomX1 = 6f, RoomZ0 = -3f, RoomZ1 = 3f;
        /// <summary>Where the raiders start, the first Raiders of these.</summary>
        public static readonly Vector2[] RaiderSlots =
        {
            new Vector2(4.2f, -0.6f), new Vector2(5.4f, 1.4f), new Vector2(5.6f, -2.2f), new Vector2(3.6f, 2.4f),
            new Vector2(4.8f, 0.6f), new Vector2(5.8f, -1.0f), new Vector2(3.9f, -2.4f),
        };

        public readonly EgoParadiseLostScene Scene;
        public readonly List<EgoParadiseLostPerson> People = new List<EgoParadiseLostPerson>();
        public readonly List<EgoParadiseLostShot> ShotList = new List<EgoParadiseLostShot>();
        public readonly List<float> RingTimes = new List<float>();
        /// <summary>When the corroded look starts to go (rings only), and when the scene ends.</summary>
        public readonly float ExitAt, End;
        /// <summary>Camera shakes, the sketch's events(): a small one as each shot's thorns rise, a bigger one as each ring starts to spread.</summary>
        public readonly List<float> ShakeAt = new List<float>(), ShakeBy = new List<float>();

        public bool Room => Scene == EgoParadiseLostScene.RoomHit || Scene == EgoParadiseLostScene.RoomHitOutdoors;
        public bool Outdoors => Scene == EgoParadiseLostScene.RoomHitOutdoors;

        public EgoParadiseLostScript(EgoParadiseLostScene scene)
        {
            Scene = scene;
            if (Room)
            {
                for (int i = 0; i < Raiders; i++) Add(RaiderSlots[i], true, true);
                Add(Outdoors ? new Vector2(-4.6f, 3.0f) : new Vector2(8.6f, 0.4f), true, false);
                Add(new Vector2(0.8f, 2.0f), false, false);
                for (int k = 0; k < Shots; k++)
                {
                    float t = Lead + k * T.ShotInterval, hitAt = t + T.HitDelay;
                    // The aimed pawn: the nearest hostile the wielder can reach (in the room, or anywhere outdoors).
                    int aimed = -1;
                    float best = float.MaxValue;
                    for (int i = 0; i < People.Count; i++)
                    {
                        EgoParadiseLostPerson q = People[i];
                        Vector2 at = PosAt(q, t);
                        if (!q.Hostile || (!Outdoors && !InRoom(at)) || at.magnitude >= best) continue;
                        best = at.magnitude;
                        aimed = i;
                    }
                    Vector2 centre = PosAt(People[aimed], t);
                    int count = 0;
                    var hit = new List<EgoParadiseLostPerson>();
                    foreach (EgoParadiseLostPerson q in People)
                    {
                        Vector2 at = PosAt(q, t);
                        if (q.Hostile && (Outdoors ? (at - centre).magnitude <= T.OutdoorRadius : InRoom(at))) hit.Add(q);
                    }
                    foreach (EgoParadiseLostPerson q in hit) { q.Hits.Add(hitAt); count++; }
                    ShotList.Add(new EgoParadiseLostShot { At = t, HitAt = hitAt, Aimed = aimed, Count = count });
                    ShakeAt.Add(hitAt);
                    ShakeBy.Add(0.012f);
                }
                End = ShotList[Shots - 1].HitAt + T.ThornLife + T.Sink + Hold;
                ExitAt = End;
                return;
            }
            bool corroded = scene == EgoParadiseLostScene.Corroded;
            if (corroded)
            {
                Add(new Vector2(1.3f, 0.7f), false, false);
                Add(new Vector2(-2.6f, -1.5f), true, false);
                Add(new Vector2(3.4f, -2.4f), false, false);
                Add(new Vector2(-4.2f, 3.6f), false, false);
                Add(new Vector2(6.6f, 2.3f), false, false);
            }
            else
            {
                Add(new Vector2(1.3f, 0.7f), false, false);
                Add(new Vector2(-2.6f, -1.5f), true, false);
                Add(new Vector2(3.2f, 2.0f), true, false);
                Add(new Vector2(3.4f, -2.4f), false, false);
                Add(new Vector2(-6.4f, 2.6f), true, false);
            }
            float every = corroded ? ScriptRingInterval : T.OverclockInterval;
            int rings = corroded ? Rings : T.OverclockRings;
            for (int k = 0; k < rings; k++)
            {
                float t = T.Enter + k * every;
                RingTimes.Add(t);
                ShakeAt.Add(t + T.RingDelay);
                ShakeBy.Add(0.035f);
                foreach (EgoParadiseLostPerson q in People)
                {
                    float d = q.Start.magnitude;
                    if (d > T.RingRadius || (!corroded && !q.Hostile)) continue;
                    q.Hits.Add(t + T.RingHitAge(d, T.RingRadius));
                }
            }
            ExitAt = RingTimes[RingTimes.Count - 1] + T.RingDelay + T.Spread + 1f;
            End = ExitAt + T.Exit + Hold;
        }

        private void Add(Vector2 start, bool hostile, bool walks) =>
            People.Add(new EgoParadiseLostPerson { Start = start, Hostile = hostile, Walks = walks });

        /// <summary>Whether a point (cells from the wielder) is on the room's floor, up to half a cell past its edge cells.</summary>
        public static bool InRoom(Vector2 q) =>
            q.x > RoomX0 - 0.5f && q.x < RoomX1 + 0.5f && q.y > RoomZ0 - 0.5f && q.y < RoomZ1 + 0.5f;

        /// <summary>Seconds of walking a pawn has done by <paramref name="s"/>, its slow windows (40 % speed for 1 s after each hit) taken off.</summary>
        public static float Walked(EgoParadiseLostPerson q, float s)
        {
            float slow = 0f;
            foreach (float h in q.Hits) slow += Mathf.Max(0f, Mathf.Min(s, h + T.SlowSeconds) - Mathf.Max(0f, h));
            return Mathf.Max(0f, s - (1f - T.SlowFactor) * slow);
        }

        /// <summary>Where a pawn stands at <paramref name="s"/>, cells from the wielder: walking straight at him until StopAt cells off.</summary>
        public static Vector2 PosAt(EgoParadiseLostPerson q, float s)
        {
            if (!q.Walks) return q.Start;
            float d0 = q.Start.magnitude;
            Vector2 toward = -q.Start / (d0 > 0f ? d0 : 1f);
            return q.Start + toward * Mathf.Min(WalkSpeed * Walked(q, s), Mathf.Max(0f, d0 - StopAt));
        }

        /// <summary>The corroded look, 0 to 1 (0 throughout a room hit).</summary>
        public float Look(float s) => Room ? 0f : T.Look(s, ExitAt);

        /// <summary>
        /// The wielder's facing: toward the pawn aimed at by the last shot so far (the first before any) in a room
        /// hit, <paramref name="otherwise"/> when corroded or overclocked.
        /// </summary>
        public Rot4 Facing(float s, Rot4 otherwise)
        {
            if (!Room) return otherwise;
            EgoParadiseLostShot shot = ShotList[0];
            foreach (EgoParadiseLostShot sh in ShotList) if (sh.At <= s) shot = sh;
            Vector2 at = PosAt(People[shot.Aimed], Mathf.Max(0f, s));
            return T.FacingOf(Mathf.Atan2(at.y, at.x) * Mathf.Rad2Deg);
        }

        /// <summary>The apple and halo flash on the staff: the last shot's, or the brightest ring's.</summary>
        public float Flash(float s)
        {
            float flash = 0f;
            foreach (EgoParadiseLostShot sh in ShotList) if (sh.At <= s) flash = T.ShotFlash(s - sh.At);
            foreach (float t in RingTimes) flash = Mathf.Max(flash, T.RingFlashAt(s - t));
            return flash;
        }
    }
}
