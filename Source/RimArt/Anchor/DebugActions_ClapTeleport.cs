using UnityEngine;
using Verse;
using T = RimArt.ClapTeleport;

namespace RimArt
{
    /// <summary>
    /// Previews only: nobody moves and no pawn is drawn. Each entry plays the clap teleport's drawing
    /// round the chosen cell, which is the middle of the scene as it is in the lab's sketch - the two
    /// ends are 3 cells west and 3 cells east of it. The real ability draws through
    /// MapComponent_ClapTeleports; this exists so the recorder can compare the port with the sketch.
    /// </summary>
    public static class DebugActions_ClapTeleport
    {
        [RimArtDebug("Clap teleport", "swap with a pawn")]
        public static void Swap() => Preview().Play(UI.MouseCell(), ClapPreview.Swap);

        [RimArtDebug("Clap teleport", "move to a tile")]
        public static void Tile() => Preview().Play(UI.MouseCell(), ClapPreview.Tile);

        [RimArtDebug("Clap teleport", "double clap two pawns")]
        public static void Twice() => Preview().Play(UI.MouseCell(), ClapPreview.Twice);

        [RimArtDebug("Clap teleport", "clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_ClapPreview>();
            if (preview != null) preview.active = false;
        }

        private static MapComponent_ClapPreview Preview() => Find.CurrentMap.GetComponent<MapComponent_ClapPreview>();
    }

    public enum ClapPreview { Swap, Tile, Twice }

    public sealed class MapComponent_ClapPreview : MapComponent
    {
        /// <summary>Half the distance between the two ends, and how far north of the middle the double clap's carrier stands.</summary>
        public const float HalfSpan = 3f, CarrierNorth = 2.5f;

        public bool active;
        private ClapPreview mode;
        private float seconds;
        private IntVec3 cell;
        private bool shaken;
        private readonly ClapEnd[] ends = new ClapEnd[2];

        public MapComponent_ClapPreview(Map map) : base(map) { }

        public static float Contact(ClapPreview mode) => mode == ClapPreview.Twice ? T.SecondContact : T.FirstContact;

        public void Play(IntVec3 target, ClapPreview play)
        {
            if (!target.InBounds(map) || target.Fogged(map)) return;
            cell = target;
            mode = play;
            seconds = 0f;
            shaken = false;
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            float contact = Contact(mode);
            if (!shaken && seconds >= contact)
            {
                shaken = true;
                Find.CameraDriver.shaker.DoShake(T.Shake);
            }

            Vector3 centre = cell.ToVector3Shifted();
            var middle = new Vector2(centre.x, centre.z);
            Vector2 west = middle + Vector2.left * HalfSpan, east = middle + Vector2.right * HalfSpan;
            Vector2 carrier = mode == ClapPreview.Twice ? middle + Vector2.up * CarrierNorth : west;
            switch (mode)
            {
                case ClapPreview.Twice:
                    ends[0] = new ClapEnd { ground = west, suit = T.Spade, ring = true, mark = ClapMark.Pawn };
                    ends[1] = new ClapEnd { ground = east, suit = T.Heart, ring = true, mark = ClapMark.Pawn };
                    break;
                case ClapPreview.Tile:
                    ends[0] = new ClapEnd { ground = west, suit = T.Club, ring = false, mark = ClapMark.None };
                    ends[1] = new ClapEnd { ground = east, suit = T.Club, ring = true, mark = ClapMark.Tile };
                    break;
                default:
                    ends[0] = new ClapEnd { ground = west, suit = T.Heart, ring = true, mark = ClapMark.None };
                    ends[1] = new ClapEnd { ground = east, suit = T.Heart, ring = true, mark = ClapMark.Pawn };
                    break;
            }

            for (int i = 0; i < ends.Length; i++) ClapTeleportGraphics.DrawEnd(ends[i], i, seconds, contact, true, map);
            var palms = new Vector2(carrier.x, carrier.y + T.PalmsNorth);
            ClapTeleportGraphics.PalmStar(palms, seconds - T.FirstContact, 0);
            if (mode == ClapPreview.Twice) ClapTeleportGraphics.PalmStar(palms, seconds - T.SecondContact, 1);
            if (seconds >= T.Duration(contact)) active = false;
        }
    }
}
