using RimWorld;
using UnityEngine;
using Verse;
using B = RimArt.BoogieWoogie;
using F = RimArt.BlackFlash;
using S = RimArt.StoneThrow;

namespace RimArt
{
    /// <summary>
    /// Previews only: nobody moves or is hurt and no pawn is drawn. Each entry plays one Todo picture
    /// round the chosen cell, which is the cell the lab's sketch centres on, so the recorder can
    /// compare the port with the sketch. The real abilities draw through MapComponent_ClapTeleports,
    /// MapComponent_MarkFlicks, MapComponent_Anchors and MapComponent_BlackFlashes.
    /// </summary>
    public static class DebugActions_TodoPictures
    {
        private static readonly Vector2 NorthWest = new Vector2(Mathf.Cos(140f * Mathf.Deg2Rad), Mathf.Sin(140f * Mathf.Deg2Rad));

        [RimArtDebug("Todo", "boogie woogie: swap with a pawn")]
        public static void SwapPawn() => Play(TodoPreview.SwapPawn, Vector2.right);

        [RimArtDebug("Todo", "boogie woogie: swap with a stone")]
        public static void SwapStone() => Play(TodoPreview.SwapStone, Vector2.right);

        [RimArtDebug("Todo", "boogie woogie: double clap two pawns")]
        public static void DoubleClap() => Play(TodoPreview.DoubleClap, Vector2.right);

        [RimArtDebug("Todo", "stone throw: throw")]
        public static void Throw() => Play(TodoPreview.Throw, Vector2.right);

        [RimArtDebug("Todo", "stone throw: throw north-west")]
        public static void ThrowNorthWest() => Play(TodoPreview.Throw, NorthWest);

        [RimArtDebug("Todo", "stone throw: take back")]
        public static void TakeBack() => Play(TodoPreview.TakeBack, Vector2.right);

        [RimArtDebug("Todo", "black flash: flash")]
        public static void Flash() => Play(TodoPreview.Flash, Vector2.right);

        [RimArtDebug("Todo", "black flash: flash north-west")]
        public static void FlashNorthWest() => Play(TodoPreview.Flash, NorthWest);

        [RimArtDebug("Todo", "black flash: ordinary punch")]
        public static void Plain() => Play(TodoPreview.Plain, Vector2.right);

        [RimArtDebug("Todo", "clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_TodoPreview>();
            if (preview != null) preview.active = false;
        }

        private static void Play(TodoPreview play, Vector2 toward) =>
            Find.CurrentMap.GetComponent<MapComponent_TodoPreview>().Play(UI.MouseCell(), play, toward);
    }

    public enum TodoPreview { SwapPawn, SwapStone, DoubleClap, Throw, TakeBack, Flash, Plain }

    public sealed class MapComponent_TodoPreview : MapComponent
    {
        public bool active;
        private TodoPreview mode;
        private float seconds;
        private IntVec3 cell;
        private Vector2 toward;
        private bool shaken;

        public MapComponent_TodoPreview(Map map) : base(map) { }

        public static float Warmup(TodoPreview play) =>
            play == TodoPreview.DoubleClap ? B.DoubleWarmup : play == TodoPreview.SwapPawn || play == TodoPreview.SwapStone ? B.ClapWarmup
            : play == TodoPreview.Flash || play == TodoPreview.Plain ? F.Warmup : S.Place;

        public static float Duration(TodoPreview play)
        {
            switch (play)
            {
                case TodoPreview.Throw: return S.Duration(false);
                case TodoPreview.TakeBack: return S.Duration(true);
                case TodoPreview.Flash:
                case TodoPreview.Plain: return F.Duration(F.Warmup);
                default: return B.Duration(Warmup(play));
            }
        }

        public void Play(IntVec3 at, TodoPreview play, Vector2 facing)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            mode = play;
            toward = facing.normalized;
            seconds = 0f;
            shaken = false;
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            Vector3 centre = cell.ToVector3Shifted();
            var o = new Vector2(centre.x, centre.z);
            Vector2 sun = GenCelestial.GetLightSourceInfo(map, GenCelestial.LightType.Shadow).vector * SixPathsSlamGraphics.SunScale;
            float strength = 0.32f * GenCelestial.CurShadowStrength(map), warmup = Warmup(mode);

            switch (mode)
            {
                case TodoPreview.SwapPawn:
                case TodoPreview.SwapStone:
                case TodoPreview.DoubleClap:
                    Shake(warmup, B.Shake);
                    DrawSwap(o, warmup);
                    break;
                case TodoPreview.Throw:
                case TodoPreview.TakeBack:
                {
                    Vector2 todo = o - toward * S.Distance / 2f, target = todo + toward * S.Distance;
                    // Another of Todo's stones, thrown earlier, lying to his south-east: the resting look.
                    StoneThrowGraphics.Resting(new Vector2(todo.x + 1.2f, todo.y - 0.9f), seconds, 1f, 0f, 7, true);
                    if (mode == TodoPreview.Throw) StoneThrowGraphics.Throw(todo, target, seconds, S.Place, sun, strength, true);
                    else StoneThrowGraphics.TakeBack(todo, target, seconds, S.Place, sun, strength, true);
                    break;
                }
                default:
                    DrawPunch(o, warmup, mode == TodoPreview.Flash);
                    break;
            }
            if (seconds >= Duration(mode)) active = false;
        }

        private void Shake(float at, float size)
        {
            if (shaken || seconds < at) return;
            shaken = true;
            Find.CameraDriver.shaker.DoShake(size);
        }

        // The sketch's scenes: the ends 3 cells either side of the middle; the double clap's Todo
        // stands 2.5 cells north of it; the clap is made where Todo stood.
        private void DrawSwap(Vector2 o, float warmup)
        {
            Vector2 west = o + Vector2.left * B.HalfSpan, east = o + Vector2.right * B.HalfSpan;
            switch (mode)
            {
                case TodoPreview.DoubleClap:
                    BoogieWoogieGraphics.Draw(o + Vector2.up * B.CarrierNorth, seconds, warmup, true,
                        west, BoogieEnd.Pawn, BoogieEnd.Pawn, east, BoogieEnd.Pawn, BoogieEnd.Pawn, map);
                    break;
                case TodoPreview.SwapStone:
                    BoogieWoogieGraphics.Draw(west, seconds, warmup, false, west, BoogieEnd.Pawn, BoogieEnd.Stone, east, BoogieEnd.Stone, BoogieEnd.Pawn, map);
                    break;
                default:
                    BoogieWoogieGraphics.Draw(west, seconds, warmup, false, west, BoogieEnd.Pawn, BoogieEnd.Pawn, east, BoogieEnd.Pawn, BoogieEnd.Pawn, map);
                    break;
            }
        }

        // The sketch's scene: Todo on the cell, the target Reach cells along the aim. The preview's
        // script, as the sketch's stand-ins move: Todo steps into the punch and back, the target
        // rocks back and settles; the fist and the contact follow them.
        private void DrawPunch(Vector2 o, float warmup, bool flash)
        {
            float age = seconds - warmup, pull = F.Pull(warmup);
            float lean = seconds < pull ? -0.04f * VfxMath.Smooth(seconds / pull)
                : age < 0f ? Mathf.Lerp(-0.04f, 0.12f, Mathf.Clamp01((seconds - pull) / (warmup - pull)))
                : 0.12f * (1f - VfxMath.Smooth((age - 0.1f) / 0.4f));
            float rock = age < 0f ? 0f : (flash ? 0.2f : 0.08f) * Mathf.Clamp01(age / 0.05f) * (1f - VfxMath.Smooth((age - 0.05f) / 0.3f));
            Vector2 todo = o + toward * lean, foe = o + toward * (F.Reach + rock), contact = F.Contact(foe, toward);
            if (!flash)
            {
                Shake(warmup, F.PlainShake);
                BlackFlashGraphics.Plain(contact, toward, age, map);
                return;
            }
            Shake(warmup + F.SparkTime, F.Shake);
            if (age < 0f) BlackFlashGraphics.FistCrackle(F.Fist(todo, toward, contact, seconds, warmup), seconds, warmup);
            BlackFlashGraphics.Hit(contact, toward, age, seconds, BlackFlashGraphics.Negative.FullScreen, map);
            BlackFlashGraphics.StunStars(foe, seconds, age);
            BlackFlashGraphics.Zone(todo, age - F.SparkTime, seconds);
        }
    }
}
