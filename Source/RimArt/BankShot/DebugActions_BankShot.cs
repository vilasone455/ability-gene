using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Previews only: nobody is hit and nothing is drawn but the effect. Each entry plays the charged
    /// shot for one of the sketch's layouts round the chosen cell, which is the sketch's origin. The
    /// flight is worked out against the layout's own wall cells (BankShotPath.Corner and the others),
    /// not the map's, and the walls, caster and target are not drawn, as no preview draws stand-ins:
    /// the bullet turns at walls that are not on screen. Place it next to real walls of the same
    /// shape, or use the real weapon (Kits: grant kit, "Bank shot pistol"), to see it against walls.
    /// </summary>
    public static class DebugActions_BankShot
    {
        [RimArtDebug("Bank Shot", "corner")]
        public static void Corner() => Preview().Play(UI.MouseCell(), BankShotPath.Scene.Corner);

        [RimArtDebug("Bank Shot", "corridor")]
        public static void Corridor() => Preview().Play(UI.MouseCell(), BankShotPath.Scene.Corridor);

        [RimArtDebug("Bank Shot", "room")]
        public static void Room() => Preview().Play(UI.MouseCell(), BankShotPath.Scene.Room);

        [RimArtDebug("Bank Shot", "clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_BankShotPreview>();
            if (preview != null) preview.active = false;
        }

        private static MapComponent_BankShotPreview Preview() =>
            Find.CurrentMap.GetComponent<MapComponent_BankShotPreview>();
    }

    public sealed class MapComponent_BankShotPreview : MapComponent
    {
        private const int MostShakes = 8;

        public bool active;
        private BankShotShot shot;
        private float seconds;
        private IntVec3 cell;
        // The camera shakes of the shot being played, in time order, and how many have been done.
        private readonly float[] shakeAt = new float[MostShakes], shakeSize = new float[MostShakes];
        private int shakes, shaken;

        public MapComponent_BankShotPreview(Map map) : base(map) { }

        public void Play(IntVec3 at, BankShotPath.Scene layout)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            Vector3 origin = at.ToVector3Shifted();
            shot = BankShotTiming.Script(layout, new Vector2(origin.x, origin.z));
            seconds = 0f;
            shakes = shaken = 0;
            Shake(shot.FireAt, BankShotTiming.FireShake);
            for (int i = 0; i < shot.Path.Bounces.Count && shakes < MostShakes - 1; i++) Shake(shot.BounceAt(i), BankShotTiming.BounceShake);
            if (shot.Path.End != BankShotEnd.Range) Shake(shot.EndAt, BankShotTiming.EndShake);
            active = true;
        }

        private void Shake(float at, float size)
        {
            shakeAt[shakes] = at;
            shakeSize[shakes++] = size;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            while (shaken < shakes && seconds >= shakeAt[shaken]) Find.CameraDriver.shaker.DoShake(shakeSize[shaken++]);
            BankShotGraphics.Draw(shot, seconds, map, true, true, shot.Offset);
            if (seconds >= shot.Duration) active = false;
        }
    }
}
