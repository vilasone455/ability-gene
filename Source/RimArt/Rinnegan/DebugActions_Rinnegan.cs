using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Tests for NegativeFlash, before any Rinnegan ability exists. They answer one question the
    /// VFX lab cannot: does Hidden/Internal-Colored's invert blend work in RimWorld's build.
    /// "held 1 s" keeps the negative long enough to judge its colours.
    /// </summary>
    public static class DebugActions_Rinnegan
    {
        [RimArtDebug("Rinnegan", "negative flash full screen")]
        public static void Screen() => Play(false, NegativeFlash.Hold);

        [RimArtDebug("Rinnegan", "negative flash full screen held 1 s")]
        public static void ScreenHeld() => Play(false, 1f);

        [RimArtDebug("Rinnegan", "negative flash discs")]
        public static void Discs() => Play(true, NegativeFlash.Hold);

        [RimArtDebug("Rinnegan", "negative flash discs held 1 s")]
        public static void DiscsHeld() => Play(true, 1f);

        private static void Play(bool discs, float hold) =>
            Find.CurrentMap.GetComponent<MapComponent_RinneganPreview>().Play(UI.MouseCell(), discs, hold);
    }

    /// <summary>Plays one flash on the clicked cell. Discs: one there and one 4 cells east.</summary>
    public sealed class MapComponent_RinneganPreview : MapComponent
    {
        private const float DiscRadius = 1.5f, DiscApart = 4f;

        private bool active, discs;
        private float seconds, hold;
        private IntVec3 cell;

        public MapComponent_RinneganPreview(Map map) : base(map) { }

        public void Play(IntVec3 at, bool discs, float hold)
        {
            if (!at.InBounds(map)) return;
            cell = at;
            this.discs = discs;
            this.hold = hold;
            seconds = 0f;
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            float strength = NegativeFlash.Strength(seconds, hold);
            if (seconds > hold + NegativeFlash.Back)
            {
                active = false;
                return;
            }
            if (!discs)
            {
                NegativeFlash.DrawScreen(strength);
                return;
            }
            Vector3 centre = cell.ToVector3Shifted();
            NegativeFlash.DrawDisc(centre, DiscRadius, strength);
            NegativeFlash.DrawDisc(centre + new Vector3(DiscApart, 0f, 0f), DiscRadius, strength);
        }
    }
}
