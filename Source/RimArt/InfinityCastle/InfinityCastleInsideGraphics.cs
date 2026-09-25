using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.CastleEffectGraphics;
using T = RimArt.InfinityCastleInsideTiming;

namespace RimArt
{
    /// <summary>
    /// Draws the castle with its own timeline: the void and the rooms at rest, a floor door on the dais
    /// for the carrier and one in each enemy's room with the shaft's dark lifting, the hold, Release
    /// (every room's outline answering the strum, nearest first, the strum's rings over 14 cells, a door
    /// under each enemy and then the carrier), and the castle going dark.
    ///
    /// The port of Tools/VfxLab/web/sketches/infinity-castle-castle.js. Its stand-ins are not ported:
    /// the enemies, their walk, and Nakime with the biwa and the bachi. The walk still decides where the
    /// Release doors open in the preview (<paramref name="walk"/>); the live castle map has nobody
    /// walking and opens them where the enemies landed.
    /// </summary>
    internal static class InfinityCastleInsideGraphics
    {
        /// <summary>
        /// Where the castle's cell (0, 0) has its lower-left corner for the sketch's two views: the whole
        /// castle centred on the chosen cell, or the biwa room just west of it.
        /// </summary>
        public static Vector2 CornerFor(Vector2 origin, CastleLayout castle, bool biwaView)
        {
            var cell = new Vector2(origin.x - 0.5f, origin.y - 0.5f);
            CastleRoom biwa = castle.Biwa;
            return biwaView
                ? new Vector2(cell.x - (biwa.X + biwa.W / 2f + 5f), cell.y - (biwa.Z + biwa.H / 2f))
                : new Vector2(cell.x - CastleLayout.Size / 2f, cell.y - CastleLayout.Size / 2f);
        }

        /// <summary>The preview: the sketch's castle (seed 1, 38 rooms) over the map's ground (<see cref="CastleLayers.Preview"/>).</summary>
        public static void DrawPreview(Vector3 centre, bool biwaView, float seconds, Map map)
        {
            var o = new Vector2(centre.x, centre.z);
            if (!Shown(o, map)) return;
            CastleLayout castle = CastleRoomGraphics.LayoutFor(T.Seed, T.Rooms);
            Draw(castle, CornerFor(o, castle, biwaView), seconds, seconds, true, CastleLayers.Preview, null);
        }

        /// <param name="s">The timeline's clock (doors, flashes, the fade).</param>
        /// <param name="ambient">The clock the lanterns flicker and the depth rooms drift on; the preview passes <paramref name="s"/>.</param>
        /// <param name="batch">The castle's own baked rooms, when a command has changed it; null for the seed's castle.</param>
        /// <param name="skipRoom">A room drawn elsewhere this frame (sliding), left out with its lantern glows.</param>
        public static void Draw(CastleLayout castle, Vector2 corner, float s, float ambient, bool walk, in CastleLayers layers, CellRect? view,
            CastleRoomGraphics.CastleBatch batch = null, int skipRoom = -1)
        {
            if (s < 0f || s >= T.Duration) return;
            Begin(new Vector2(corner.x + CastleLayout.Size / 2f, corner.y + CastleLayout.Size / 2f));
            CastleRoomGraphics.DrawCastle(castle, corner, ambient, layers, true, view, batch, skipRoom);

            // Release: every room answers the strum, nearest first.
            float releaseAge = s - T.Release;
            if (releaseAge >= -0.3f)
                foreach (CastleRoom room in castle.Rooms)
                    RoomFlash(room, CastleRoomGraphics.CentreOf(corner, room), releaseAge - 0.05f - castle.Dist[room.Id] * 0.03f, layers.Wall);

            // The enemies' doors: in through the floor of their rooms, out wherever they are at Release.
            List<CastleRoom> landing = castle.ArrivalRooms(T.Enemies);
            for (int i = 0; i < landing.Count; i++)
            {
                CastleRoom room = landing[i];
                Vector2 at = CellCentre(corner, room.X + room.W / 2, room.Z + room.H / 2);
                Vector2 end = walk ? T.Along(PathHome(castle, room, corner, at), T.Walked(s, i)) : at;
                float inAge = s - T.EnemyLands(i), outAge = s - T.EnemyOut(i);
                DoorAt(inAge, T.Rise * 0.75f, out float inAlpha, out float inOpen);
                if (s < T.Release) FloorDoor(at, inOpen, inAlpha, s, layers.Door);
                DoorAt(outAge, T.Sink + 0.05f, out float outAlpha, out float outOpen);
                if (outAge >= -0.2f && outAge < DoorEnd(T.Sink + 0.05f)) FloorDoor(end, outOpen, outAlpha, s, layers.Door);
                if (inAge < 0f) continue;
                if (inAge < T.Rise + DoorThrough) Rising(at, Clamp((inAge - DoorThrough) / T.Rise), layers);
                else if (outAge >= DoorThrough) Sinking(end, Clamp((outAge - DoorThrough) / T.Sink), layers);
            }

            // The carrier's doors on the dais, and the Release strum from her biwa.
            var (seatX, seatZ) = CastleLayout.SeatOf(castle.Biwa);
            var seat = new Vector2(corner.x + (float)seatX, corner.y + (float)seatZ);
            float casterIn = s - T.CasterLands, casterOut = s - T.CasterOut;
            DoorAt(casterIn, T.Rise * 0.75f, out float carrierInAlpha, out float carrierInOpen);
            if (casterIn >= -0.2f && casterIn < DoorEnd(T.Rise * 0.75f)) FloorDoor(seat, carrierInOpen, carrierInAlpha, s, layers.Door);
            DoorAt(casterOut, T.Sink + 0.05f, out float carrierOutAlpha, out float carrierOutOpen);
            if (casterOut >= -0.2f) FloorDoor(seat, carrierOutOpen, carrierOutAlpha, s, layers.Door);
            var (biwaX, biwaZ) = CastleLayout.BiwaOf((seatX, seatZ));
            Strum(new Vector2(corner.x + (float)biwaX, corner.y + (float)biwaZ), releaseAge, T.StrumReach, T.StrumLife, layers.Fx);

            // The map closing: everything goes to black.
            float fade = Clamp((s - T.FadeAt) / T.FadeFor);
            if (fade > 0f)
                Sprite(new Vector2(corner.x + CastleLayout.Size / 2f, corner.y + CastleLayout.Size / 2f), CastleLayout.Size * 3f, CastleLayout.Size * 3f,
                    Fade(CastleRoomGraphics.VoidDeep, fade), solid, layers.Fx + 0.2f);
        }

        private static Vector2 CellCentre(Vector2 corner, int x, int z) => new Vector2(corner.x + x + 0.5f, corner.y + z + 0.5f);

        /// <summary>The preview's script: from the room's middle through each doorway toward the biwa room.</summary>
        private static List<Vector2> PathHome(CastleLayout castle, CastleRoom room, Vector2 corner, Vector2 start)
        {
            var path = new List<Vector2> { start };
            foreach (var (x, z) in castle.WayHome(room)) path.Add(CellCentre(corner, x, z));
            return path;
        }
    }
}
