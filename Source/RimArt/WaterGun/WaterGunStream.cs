using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// One Stream Shot as the picture needs it. Positions are the caster's feet and the target's
    /// ground point; the jet ends at the target's chest (<see cref="OnPawn"/>) or just above the floor.
    /// </summary>
    public struct WaterStreamShot
    {
        public Vector2 Target;
        /// <summary>Water in the bag when the cast began; the picture drains <see cref="WaterGunStreamTiming.Cost"/> from it at the shot.</summary>
        public float Units;
        /// <summary>Seconds the gun stays up after the hit, and seconds the result (puddle, drips) shows after the hit.</summary>
        public float GunHold, Hold;
        public bool OnPawn, Burning;
        /// <summary>Where the soaked target is now, for its drips. Null draws none.</summary>
        public Vector2? DripAt;
    }

    /// <summary>
    /// Timing of Stream Shot: seconds in, numbers out, no drawing and no map. The port of
    /// Tools/VfxLab/web/sketches/water-gun-stream.js; the constants are that sketch's defaults. The
    /// sketch's flight is 0.25 s for its 6-cell default; here it is distance / <see cref="Speed"/>, the
    /// same 0.25 s at 6 cells.
    /// </summary>
    public static class WaterGunStreamTiming
    {
        /// <summary>The jet lasts this long at the muzzle; the picture's drain per shot; jet width in cells; jet speed in cells a second.</summary>
        public const float Jet = 0.12f, Cost = 1f, Width = 0.07f, Speed = 24f;
        public const float HitShake = 0.02f;

        // The preview's script: the sketch's defaults.
        public const float ScriptDistance = 6f, ScriptUnits = 18f, ScriptHold = 1.5f;
        /// <summary>In game the gun comes down this soon after the hit, so the caster is not held for the whole result.</summary>
        public const float GameGunHold = 0.2f;

        public static float Raise0 => WaterGunGraphics.Lead;
        public static float Fire => WaterGunGraphics.Lead + WaterGunGraphics.Raise;
        public static float Flight(float distance) => Mathf.Max(0.1f, distance / Speed);
        public static float Hit(float distance) => Fire + Flight(distance);
        public static float JetEnd(float distance) => Hit(distance) + Jet;
        public static float Lower0(float distance, float gunHold) => Hit(distance) + gunHold;
        /// <summary>The gun is back at rest.</summary>
        public static float GunDown(float distance, float gunHold) => Lower0(distance, gunHold) + WaterGunGraphics.Lower;
        public static float End(float distance, float gunHold, float hold) =>
            Hit(distance) + Mathf.Max(gunHold, hold) + WaterGunGraphics.Lower + WaterGunGraphics.Tail;

        public static float ScriptEnd => End(ScriptDistance, ScriptHold, ScriptHold);
    }
}
