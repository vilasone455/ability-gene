using UnityEngine;
using Verse;
using Go = RimArt.NezukoBoxGoInTiming;
using Out = RimArt.NezukoBoxComeOutTiming;

namespace RimArt
{
    /// <summary>
    /// Previews only: no pawn is moved and no stand-in is drawn. Each entry plays one sketch's default
    /// script with the chosen cell as the wearer's feet: the worn box in four facings (empty top row,
    /// asleep bottom row), Go in, and Come out with a strike, a downed pawn and time up. The sketches'
    /// default facing is south (270); the north entries show the door side. The box and its abilities
    /// are in NezukoBox/Kit.
    /// </summary>
    public static class DebugActions_NezukoBox
    {
        [RimArtDebug("Nezuko's Box", "worn")]
        public static void Worn() => Preview().Play(UI.MouseCell(), NezukoBoxPreview.Worn, 0f);

        [RimArtDebug("Nezuko's Box", "go in")]
        public static void GoIn() => Preview().Play(UI.MouseCell(), NezukoBoxPreview.GoIn, 270f);

        [RimArtDebug("Nezuko's Box", "go in north")]
        public static void GoInNorth() => Preview().Play(UI.MouseCell(), NezukoBoxPreview.GoIn, 90f);

        [RimArtDebug("Nezuko's Box", "go in downed")]
        public static void GoInDowned() => Preview().Play(UI.MouseCell(), NezukoBoxPreview.GoInDowned, 270f);

        [RimArtDebug("Nezuko's Box", "come out strike")]
        public static void Strike() => Preview().Play(UI.MouseCell(), NezukoBoxPreview.Strike, 270f);

        [RimArtDebug("Nezuko's Box", "come out strike north")]
        public static void StrikeNorth() => Preview().Play(UI.MouseCell(), NezukoBoxPreview.Strike, 90f);

        [RimArtDebug("Nezuko's Box", "come out downed")]
        public static void Downed() => Preview().Play(UI.MouseCell(), NezukoBoxPreview.Downed, 270f);

        [RimArtDebug("Nezuko's Box", "come out time up")]
        public static void TimeUp() => Preview().Play(UI.MouseCell(), NezukoBoxPreview.TimeUp, 270f);

        [RimArtDebug("Nezuko's Box", "clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_NezukoBoxPreview>();
            if (preview != null) preview.active = false;
        }

        private static MapComponent_NezukoBoxPreview Preview() =>
            Find.CurrentMap.GetComponent<MapComponent_NezukoBoxPreview>();
    }

    public enum NezukoBoxPreview { Worn, GoIn, GoInDowned, Strike, Downed, TimeUp }

    public sealed class MapComponent_NezukoBoxPreview : MapComponent
    {
        /// <summary>worn sketch: the four facings left to right, and the gap between wearers.</summary>
        private static readonly float[] Facings = { 90f, 0f, 270f, 180f };
        private const float Spacing = 1.6f, WornSeconds = 5.2f;
        /// <summary>come out sketch: the landing direction (east).</summary>
        private const float Aim = 0f;

        public bool active;
        private NezukoBoxPreview mode;
        private float seconds, duration, facing;
        private IntVec3 cell;
        private int shaken;

        public MapComponent_NezukoBoxPreview(Map map) : base(map) { }

        public void Play(IntVec3 at, NezukoBoxPreview play, float facingDegrees)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            mode = play;
            facing = facingDegrees;
            seconds = 0f;
            shaken = 0;
            duration = play == NezukoBoxPreview.Worn ? WornSeconds
                : play == NezukoBoxPreview.GoIn || play == NezukoBoxPreview.GoInDowned ? Go.End
                : Out.End(Kind(play), Out.Flight, Out.Hold);
            active = true;
        }

        private static NezukoExit Kind(NezukoBoxPreview play) =>
            play == NezukoBoxPreview.Strike ? NezukoExit.Strike : play == NezukoBoxPreview.Downed ? NezukoExit.Downed : NezukoExit.TimeUp;

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            Vector3 c = cell.ToVector3Shifted();
            var feet = new Vector2(c.x, c.z);
            VfxDraw.Begin(feet);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            float s = seconds;
            switch (mode)
            {
                case NezukoBoxPreview.Worn:
                    for (int row = 0; row < 2; row++)
                        for (int i = 0; i < 4; i++)
                        {
                            var at = new Vector2(feet.x + (i - 1.5f) * Spacing, feet.y + (row == 0 ? 1.1f : -0.35f));
                            var look = new NezukoBoxLook { Sleeping = row, T = s + i * 0.4f };
                            NezukoBoxGraphics.Box(at, Facings[i], look, sun, strength);
                        }
                    break;
                case NezukoBoxPreview.GoIn:
                case NezukoBoxPreview.GoInDowned:
                    if (s < Go.End)
                        NezukoBoxPictures.GoIn(new NezukoGoInShot { Feet = feet, Facing = facing, DownedBlood = mode == NezukoBoxPreview.GoInDowned, Scale = 1f, T = s }, s, sun, strength);
                    break;
                default:
                    NezukoExit kind = Kind(mode);
                    float a = Aim * Mathf.Deg2Rad;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Vector2 land = kind == NezukoExit.Strike ? feet + dir * Out.Distance : NezukoBoxPictures.OutsideDoor(feet, facing);
                    Shake(kind);
                    if (s < Out.End(kind, Out.Flight, Out.Hold))
                        NezukoBoxPictures.ComeOut(new NezukoComeOutShot
                        {
                            Feet = feet, Land = land, Facing = facing, Kind = kind, Enemy = kind == NezukoExit.Strike ? land + dir * 0.95f : (Vector2?)null,
                            Knock = true, Flight = Out.Flight, Peak = Out.Peak, Scale = 1f, T = s,
                        }, s, sun, strength);
                    break;
            }
            if (seconds >= duration) active = false;
        }

        /// <summary>The strike's three camera shakes (burst, landing, strike), each played once.</summary>
        private void Shake(NezukoExit kind)
        {
            if (kind != NezukoExit.Strike) return;
            float[] at = { Out.BurstAt, Out.Land(Out.Flight), Out.StrikeAt(Out.Flight) };
            float[] size = { 0.03f, 0.04f, 0.06f };
            while (shaken < at.Length && seconds >= at[shaken])
            {
                Find.CameraDriver.shaker.DoShake(size[shaken]);
                shaken++;
            }
        }
    }
}
