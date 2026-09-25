using UnityEngine;

namespace RimArt
{
    /// <summary>A pawn Hydro Pump hits, in the cast's frame: cells along the aim and across it from the caster's feet.</summary>
    public struct WaterPumpVictim
    {
        public float Along, Across;
        /// <summary>Cells it slid along the aim: the push, or less where a wall stopped it.</summary>
        public float Pushed;
        public bool Burning;
        /// <summary>Where the pawn is now, for its drips. Null uses the scripted slide.</summary>
        public Vector2? LiveAt;
    }

    /// <summary>One Hydro Pump as the picture needs it. Cone length and end width are the rule's, so the floor wedge is the true hit area.</summary>
    public struct WaterPumpShot
    {
        public float Length, Width, Push, Down;
        /// <summary>Water in the bag when the cast began; the picture drains <see cref="WaterGunPumpTiming.Cost"/> over the spray.</summary>
        public float Units;
        /// <summary>Seconds from the blast until the gun starts to come down.</summary>
        public float GunHold;
        public WaterPumpVictim[] Victims;
    }

    /// <summary>
    /// Timing and geometry of Hydro Pump: seconds in, numbers out, no drawing and no map. The port of
    /// Tools/VfxLab/web/sketches/water-gun-hydro-pump.js; the constants are that sketch's defaults.
    /// </summary>
    public static class WaterGunPumpTiming
    {
        /// <summary>Pump wind-up, spray, seconds for the blast front to reach the cone's end, a pawn's slide, getting up.</summary>
        public const float Windup = 0.35f, Spray = 0.5f, Front = 0.2f, Slide = 0.4f, Rise = 0.3f;
        public const int Jets = 5;
        /// <summary>The picture's drain per cast, and the jets' width in cells.</summary>
        public const float Cost = 10f, JetWidth = 0.075f;
        public const float BlastShake = 0.08f;
        /// <summary>The cone starts at the muzzle, not at the feet.</summary>
        public const float Apex = WaterGunGraphics.MuzzleAlong;

        // The preview's script: the sketch's defaults and its three pawns inside the cone (the one
        // outside is not hit and draws nothing).
        public const float ScriptLength = 5f, ScriptWidth = 3f, ScriptPush = 3f, ScriptDown = 2f, ScriptUnits = 30f;
        /// <summary>In game the gun comes down this long after the spray has reached the cone's end.</summary>
        public const float GameGunLinger = 0.2f;

        public static float Raise0 => WaterGunGraphics.Lead;
        public static float Pump0 => WaterGunGraphics.Lead + WaterGunGraphics.Raise;
        public static float Blast => Pump0 + Windup;
        public static float SprayEnd => Blast + Spray;
        public static float Up(float down) => Blast + Front + Slide + down;
        public static float Lower0(float gunHold) => Blast + gunHold;
        public static float End(float down, float gunHold) =>
            Mathf.Max(Lower0(gunHold), Up(down) + Rise + 0.5f) + WaterGunGraphics.Lower + WaterGunGraphics.Tail;

        /// <summary>The sketch lowers the gun after the pawns are up.</summary>
        public static float ScriptGunHold => Front + Slide + ScriptDown + Rise + 0.5f;
        public static float GameGunHold => Spray + Front + GameGunLinger;
        public static float GunDown(float gunHold) => Lower0(gunHold) + WaterGunGraphics.Lower;

        /// <summary>Half the cone's width at <paramref name="along"/> cells from the feet.</summary>
        public static float HalfWidth(float along, float length, float width) =>
            width / 2f * Mathf.Clamp01((along - Apex) / (length - Apex));

        /// <summary>When the blast front reaches a pawn <paramref name="along"/> cells out.</summary>
        public static float ReachAt(float along, float length) => Blast + Front * along / length;

        public static WaterPumpShot Script()
        {
            return new WaterPumpShot
            {
                Length = ScriptLength, Width = ScriptWidth, Push = ScriptPush, Down = ScriptDown, Units = ScriptUnits, GunHold = ScriptGunHold,
                Victims = new[]
                {
                    new WaterPumpVictim { Along = 2.0f, Across = 0.45f, Pushed = ScriptPush },
                    new WaterPumpVictim { Along = 3.4f, Across = -0.85f, Pushed = ScriptPush },
                    new WaterPumpVictim { Along = 4.4f, Across = 0.35f, Pushed = ScriptPush, Burning = true },
                },
            };
        }

        public static float ScriptEnd => End(ScriptDown, ScriptGunHold);
    }
}
