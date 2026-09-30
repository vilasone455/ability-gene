using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using T = RimArt.Plasma;

namespace RimArt
{
    /// <summary>
    /// Previews only: nobody moves or is hurt and no pawn is drawn. Each entry plays the Plasma picture
    /// round the chosen cell, which is the middle of the lane as in the lab's sketch, so the recorder can
    /// compare the port with the sketch. The real ability draws through the kit's own component.
    /// </summary>
    public static class DebugActions_PlasmaPreview
    {
        [RimArtDebug("Accelerator", "plasma: default")]
        public static void Default() => Play(0f, false, true, -1f);

        [RimArtDebug("Accelerator", "plasma: wall")]
        public static void Wall() => Play(0f, true, true, -1f);

        [RimArtDebug("Accelerator", "plasma: north-west")]
        public static void NorthWest() => Play(140f, false, true, -1f);

        [RimArtDebug("Accelerator", "plasma: no shooters")]
        public static void NoShooters() => Play(0f, false, false, -1f);

        [RimArtDebug("Accelerator", "plasma: channel broken")]
        public static void Broken() => Play(0f, false, true, MapComponent_PlasmaPreview.BreakAt);

        [RimArtDebug("Accelerator", "plasma: clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_PlasmaPreview>();
            if (preview != null) preview.active = false;
        }

        private static void Play(float aim, bool wall, bool shooters, float breakAt) =>
            Find.CurrentMap.GetComponent<MapComponent_PlasmaPreview>().Play(UI.MouseCell(), aim, wall, shooters, breakAt);
    }

    /// <summary>
    /// Plays Plasma on its own clock, which is the sketch's: the channel starts <see cref="Plasma.Lead"/> s
    /// in. The sketch's stand-ins live here, not in <see cref="PlasmaGraphics"/>: the two shooters' muzzle
    /// flashes, their rounds up to the pull ring (then handed over as caught rounds), the rounds fired
    /// after the release that fly on and hit the caster, and the fire on the pawn that is hit. The rest
    /// of the sketch's pawns and the wall are not drawn.
    /// </summary>
    public sealed class MapComponent_PlasmaPreview : MapComponent
    {
        /// <summary>The "channel broken" entry breaks the channel this long after it starts.</summary>
        public const float BreakAt = 1.8f;
        private static readonly Color Round = new Color(1f, 0.9f, 0.55f), Muzzle = new Color(1f, 0.85f, 0.5f);

        public bool active;
        private float seconds, duration, aim, breakAt;
        private bool wall, shooters;
        private IntVec3 cell;
        private readonly List<PlasmaCatch> caught = new List<PlasmaCatch>();
        private readonly List<Vector2> shakes = new List<Vector2>();
        private int shaken;

        public MapComponent_PlasmaPreview(Map map) : base(map) { }

        /// <summary>The preview's stop and when the head reaches it, on the sketch's clock.</summary>
        public static float Stop(bool wall) => T.ScriptStop(T.Length, T.FirstAt, wall);
        public static float HitAt(bool wall) => T.Lead + T.HitAt(T.Channel, T.Flight, Stop(wall), T.Length);

        public void Play(IntVec3 at, float degrees, bool walled, bool shoot, float breakAfter)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            aim = degrees;
            wall = walled;
            shooters = shoot;
            breakAt = breakAfter;
            seconds = 0f;
            bool broken = breakAt >= 0f;
            duration = broken ? T.Lead + breakAt + T.CancelFade : HitAt(wall) + T.Tail;
            // The sketch's events(): release, burst, and a smaller one 0.2 s after the burst.
            shakes.Clear();
            shaken = 0;
            if (!broken)
            {
                shakes.Add(new Vector2(T.Lead + T.Channel, T.FireShake));
                shakes.Add(new Vector2(HitAt(wall), T.HitShake));
                shakes.Add(new Vector2(HitAt(wall) + T.AfterShakeDelay, T.AfterShake));
            }
            // Every round that crosses the pull ring during the channel is caught there.
            caught.Clear();
            if (shooters)
            {
                Vector2 feet = Feet();
                for (int j = 0; j < T.ShooterAlong.Length; j++)
                {
                    Vector2 from = Shooter(feet, j), way = (feet - from).normalized;
                    float enter = Enter(feet, from);
                    for (int n = 0; n < T.MostShots && T.ShotAt(j, n) <= T.LastShot; n++)
                    {
                        float tEnter = T.ShotAt(j, n) + enter / T.RoundSpeed;
                        if (Caught(tEnter)) caught.Add(new PlasmaCatch(from + way * enter, tEnter - T.Lead));
                    }
                }
            }
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            while (shaken < shakes.Count && seconds >= shakes[shaken].x) Find.CameraDriver.shaker.DoShake(shakes[shaken++].y);
            Vector2 feet = Feet();
            float s = seconds;
            Begin(feet);
            if (shooters) DrawRounds(feet, s);
            bool walled = wall && T.ScriptWallAt(T.FirstAt) < T.Length;
            PlasmaGraphics.Draw(new PlasmaShot
            {
                Feet = feet, Aim = aim, Length = T.Length, Width = T.Width, Radius = T.Radius, Pull = T.Pull, BallSize = T.BallSize,
                Channel = T.Channel, Flight = T.Flight, Stop = Stop(wall), Walled = walled, WallAt = T.ScriptWallAt(T.FirstAt),
                Seconds = s - T.Lead, Cancelled = breakAt, Caught = caught, RealFires = false,
                Sleeve = AcceleratorGraphics.Shirt, Skin = AcceleratorGraphics.Skin,
            }, map);
            // The pawn it hit lies down and burns (not with the wall on: the wall took the hit).
            float age = s - HitAt(wall);
            if (breakAt < 0f && !walled && age >= 0.3f && Shown(feet, map))
                FlameGauntletGraphics.BurningPawn(GokuTiming.Place(feet, Turn(aim), T.FirstAt) + new Vector2(0.1f, 0f), s, 0.8f * (1f - 0.4f * Smooth(age / T.Tail)));
            if (seconds >= duration) active = false;
        }

        // The sketch's two shooters. A round is caught if it reaches the pull ring during the channel; one
        // that reaches it outside the channel flies on and hits the caster.
        private void DrawRounds(Vector2 feet, float s)
        {
            float chest = GokuGraphics.ChestOn;
            for (int j = 0; j < T.ShooterAlong.Length; j++)
            {
                Vector2 from = Shooter(feet, j), way = (feet - from).normalized;
                float dist = Vector2.Distance(feet, from), enter = Enter(feet, from);
                for (int n = 0; n < T.MostShots; n++)
                {
                    float fired = T.ShotAt(j, n), shot = s - fired;
                    if (fired > T.LastShot) break;
                    if (shot < 0f) continue;
                    if (shot < 0.07f)
                        Sprite(new Vector2(from.x + way.x * 0.45f, from.y + way.y * 0.45f + chest), 0.4f, 0.4f, Fade(Muzzle, 1f - shot / 0.07f), glow, Overhead + 0.2f);
                    float tEnter = fired + enter / T.RoundSpeed;
                    if (Caught(tEnter) && s >= tEnter) continue;                      // the picture draws it from here
                    float d = shot * T.RoundSpeed;
                    if (d >= dist)
                    {
                        // It arrived: a hit on the caster.
                        float since = (d - dist) / T.RoundSpeed;
                        if (since >= 0.3f) continue;
                        float f = 1f - since / 0.3f;
                        Sprite(new Vector2(feet.x, feet.y + chest), 1.1f * f + 0.3f, 1.1f * f + 0.3f, Fade(AcceleratorGraphics.White, f), glow, Overhead + 0.2f);
                        Sprite(new Vector2(feet.x - way.x * 0.2f, feet.y - way.y * 0.2f + 0.25f), 0.6f + 0.4f * (1f - f), 0.5f + 0.3f * (1f - f), Fade(Ink, 0.6f * f), soft, Overhead + 0.19f);
                        continue;
                    }
                    var at = new Vector2(from.x + way.x * d, from.y + way.y * d + chest);
                    Streak(at - way * 0.55f, at, 0.09f, Fade(Round, 0.95f), whiteGlow, Overhead + 0.15f, 3);
                    Sprite(at, 0.16f, 0.16f, Fade(AcceleratorGraphics.White, 0.9f), glow, Overhead + 0.151f);
                }
            }
        }

        private bool Caught(float tEnter) => tEnter >= T.Lead && tEnter < T.Lead + (breakAt >= 0f ? breakAt : T.Channel);

        private Vector2 Feet()
        {
            Vector3 centre = cell.ToVector3Shifted();
            return new Vector2(centre.x, centre.z) - Turn(aim) * (T.Length / 2f);
        }

        private Vector2 Shooter(Vector2 feet, int j) => GokuTiming.Place(feet, Turn(aim), T.ShooterAlong[j], T.ShooterSide[j] * (T.Pull + T.ShooterGap));

        /// <summary>Cells a round from <paramref name="from"/> flies before it crosses the pull ring.</summary>
        private static float Enter(Vector2 feet, Vector2 from) => Mathf.Max(0f, Vector2.Distance(feet, from) - T.Pull);
    }
}
