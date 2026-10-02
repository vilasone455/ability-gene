using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Previews only: there is no weapon, no ability and no def behind any of this; nobody is burnt and no pawn or wall
    /// is drawn. Each entry plays the Last Prism picture round the chosen cell, which is the cell the lab's sketch
    /// centres on: the wielder stands 7 cells behind it, facing the entry's aim, with the sketch's seven pawns and
    /// three wall cells placed from there (they are the script's, not the map's). It runs on the sketch's clock, so the
    /// recorder can compare the port with last-prism.js.
    /// </summary>
    public static class DebugActions_LastPrism
    {
        [RimArtDebug("Last Prism", "fires")]
        public static void Fires() => Play(LastPrismScene.Fires, 0f);

        [RimArtDebug("Last Prism", "fires, aim 90")]
        public static void FiresNorth() => Play(LastPrismScene.Fires, 90f);

        [RimArtDebug("Last Prism", "fires, aim 180")]
        public static void FiresWest() => Play(LastPrismScene.Fires, 180f);

        [RimArtDebug("Last Prism", "fires, aim 270")]
        public static void FiresSouth() => Play(LastPrismScene.Fires, 270f);

        [RimArtDebug("Last Prism", "fires, aim 225")]
        public static void FiresSouthWest() => Play(LastPrismScene.Fires, 225f);

        [RimArtDebug("Last Prism", "runs dry")]
        public static void RunsDry() => Play(LastPrismScene.RunsDry, 0f);

        [RimArtDebug("Last Prism", "charges in the sun")]
        public static void Charges() => Play(LastPrismScene.Charges, 0f);

        [RimArtDebug("Last Prism", "under a roof")]
        public static void Roofed() => Play(LastPrismScene.Roofed, 0f);

        [RimArtDebug("Last Prism", "clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_LastPrismPreview>();
            if (preview != null) preview.active = false;
        }

        private static void Play(LastPrismScene scene, float aim) =>
            Find.CurrentMap.GetComponent<MapComponent_LastPrismPreview>().Play(UI.MouseCell(), scene, aim);
    }

    /// <summary>
    /// Plays the Last Prism on the sketch's clock with the defs' numbers (the sketch's defaults). The fight is <see cref="LastPrismScript"/>'s:
    /// each frame takes the prism's aim, the target, where the pawns stand and when they went down from it, and stops
    /// the beams at its wall cells. The sketch's meter over the wielder's head is drawn too (<see cref="LastPrismGraphics.DrawMeter"/>),
    /// standing in for the weapon's button. Under a roof is the scenario's, not the map's.
    /// </summary>
    public sealed class MapComponent_LastPrismPreview : MapComponent
    {
        public bool active;
        private float seconds, aim;
        private LastPrismScene scene;
        private IntVec3 cell;
        private bool shaken;
        private LastPrismScript script;
        private Vector2 origin;
        private readonly LastPrismPawn[] pawns = new LastPrismPawn[LastPrismScript.Count];
        private readonly LastPrismWalls walls;

        public MapComponent_LastPrismPreview(Map map) : base(map)
        {
            walls = Walls;
        }

        public void Play(IntVec3 at, LastPrismScene play, float degrees)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            scene = play;
            aim = degrees;
            script = LastPrismScript.For(play, degrees);
            seconds = 0f;
            shaken = false;
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            float s = seconds;
            if (s >= script.End)
            {
                active = false;
                return;
            }
            Vector3 centre = cell.ToVector3Shifted();
            origin = new Vector2(centre.x, centre.z);
            float light = LastPrismGraphics.Light(map);
            CompProperties_LastPrismFire p = CompProperties_LastPrismFire.Of;
            var shot = new LastPrismShot
            {
                Wielder = origin, Aim = aim, Join = p.joinSeconds, Fan = p.fanDegrees, Range = p.Range, Width = p.width, HitReach = p.fanReach,
                Walls = walls, Pawns = pawns, Roofed = scene == LastPrismScene.Roofed, Charging = scene == LastPrismScene.Charges,
                Level = script.Level(s, light), Store = LastPrismScript.Store,
            };
            if (LastPrismScript.Shoots(scene))
            {
                int k = script.Frame(s);
                shot.Wielder = origin + script.Caster;
                shot.Aim = script.Aims[k] * Mathf.Rad2Deg;
                shot.Firing = true;
                shot.ChannelAt = (float)LastPrismScript.Lead;
                shot.ReleaseAt = (float)script.ReleaseAt;
                shot.Dried = script.Dried;
                shot.Target = origin + script.Pos(script.Targets[k], s);
                for (int j = 0; j < LastPrismScript.Count; j++)
                    pawns[j] = new LastPrismPawn { At = origin + script.Pos(j, s), DownAt = (float)script.DownAt[j] };
                shot.PawnCount = LastPrismScript.Count;
                // The sketch's events(): a small shake at the join.
                if (!shaken && script.Joins && s >= script.JoinAt)
                {
                    shaken = true;
                    Find.CameraDriver.shaker.DoShake(0.05f);
                }
            }
            LastPrismGraphics.Draw(shot, s, map);
            LastPrismGraphics.DrawMeter(shot.Wielder, shot.Level, shot.Store, script.Warn(s));
        }

        // The script's wall cells, placed round the chosen cell.
        private float Walls(Vector2 from, float radians, float max) => (float)script.Reach(from.x - origin.x, from.y - origin.y, radians, max);
    }
}
