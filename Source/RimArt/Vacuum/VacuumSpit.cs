using UnityEngine;

namespace RimArt
{
    /// <summary>One Spit as the picture needs it.</summary>
    public sealed class VacuumSpitShot
    {
        public Vector2 Caster, Target;
        /// <summary>Where the thing comes to rest: the target cell, or beside the pawn it hit.</summary>
        public Vector2 Rest;
        /// <summary>Kg inside when the cast began, in the picture's 100 kg canister; the thing's own kg leaves at the heave.</summary>
        public float Inside;
        public VacuumThing Thing;
        /// <summary>It hits a pawn: the big impact, the second shake and the daze marks.</summary>
        public bool HitPawn;
        /// <summary>Where the hit pawn stands now, for its daze marks. Null puts them over the target.</summary>
        public Vector2? DazedAt;
        /// <summary>Seconds the hit pawn is dazed (the ability's stun).</summary>
        public float Dazed = VacuumSpitTiming.ScriptDazed;
        public float Hold = VacuumSpitTiming.ScriptHold;
        public float Slack = VacuumSuckTiming.Slack;
        /// <summary>The preview: the thing is drawn lying where it landed after the impact.</summary>
        public bool Props;
    }

    /// <summary>
    /// Timing of Spit: seconds in, numbers out, no drawing and no map. The port of
    /// Tools/VfxLab/web/sketches/vacuum-spit.js; the constants are that sketch's defaults. The ability
    /// fires at <see cref="Heave"/>, the end of the wind-up.
    /// </summary>
    public static class VacuumSpitTiming
    {
        /// <summary>The wind-up (the ability's warmupTime must match) and the flight to the cell, seconds; the arc's height in cells.</summary>
        public const float Windup = 0.3f, Flight = 0.45f, Arc = 0.9f;
        public const float Blink = 0.16f;
        public const float LaunchShake = 0.05f, HitShake = 0.10f;

        // The preview's script: the sketch's defaults.
        public const float ScriptDistance = 5f, ScriptInside = 40f, ScriptHold = 1.2f, ScriptDazed = 1.2f;
        /// <summary>In game the result shows this long before the canister sinks.</summary>
        public const float GameHold = 0.3f;

        public static float Rise0 => VacuumGraphics.Lead;
        /// <summary>The thing starts up the hose: in game the ability fires now.</summary>
        public static float Heave => VacuumGraphics.Lead + Windup;
        public static float Launch => Heave + VacuumGraphics.Bulge;
        /// <summary>The thing lands: in game the hit and the drop happen now.</summary>
        public static float Impact => Launch + Flight;
        public static float Sink0(float hold) => Impact + hold;
        public static float Home(float hold) => Sink0(hold) + VacuumGraphics.Sink;
        public static float End(float hold) => Home(hold) + VacuumGraphics.Tail;

        /// <summary>The sketch's scene round <paramref name="centre"/>: "chunk, pawn on the cell" or "rifle, empty cell".</summary>
        public static VacuumSpitShot Script(Vector2 centre, float aimDegrees, bool chunk)
        {
            var toward = new Vector2(Mathf.Cos(aimDegrees * Mathf.Deg2Rad), Mathf.Sin(aimDegrees * Mathf.Deg2Rad));
            var across = new Vector2(-toward.y, toward.x);
            Vector2 o = centre + toward * (ScriptDistance / 2f);
            return new VacuumSpitShot
            {
                Caster = centre - toward * (ScriptDistance / 2f), Target = o,
                // A chunk that hit a pawn drops beside it.
                Rest = chunk ? o + toward * 0.35f - across * 0.55f : o,
                Inside = ScriptInside, HitPawn = chunk, Props = true,
                Thing = chunk
                    ? new VacuumThing { Kind = VacuumThingKind.Chunk, Kg = VacuumSuckTiming.ChunkKg }
                    : new VacuumThing { Kind = VacuumThingKind.Rifle, Kg = VacuumSuckTiming.RifleKg, Spin = true },
            };
        }

        public static float ScriptEnd => End(ScriptHold);
    }
}
