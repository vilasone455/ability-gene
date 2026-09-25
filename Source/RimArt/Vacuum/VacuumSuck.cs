using System.Collections.Generic;
using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// One Suck as the picture needs it. Things are listed in the order they fly, one
    /// <see cref="VacuumSuckTiming.Gap"/> apart; in game that is lightest first, so the heaviest lands
    /// last and stays in the mouth.
    /// </summary>
    public sealed class VacuumSuckShot
    {
        public Vector2 Caster, Target;
        public float Radius = VacuumSuckTiming.ScriptRadius;
        /// <summary>Kg inside when the cast began, in the picture's 100 kg canister.</summary>
        public float Inside;
        /// <summary>Seconds the result shows before the canister sinks.</summary>
        public float Hold = VacuumSuckTiming.ScriptHold;
        public float Slack = VacuumSuckTiming.Slack;
        public readonly List<VacuumThing> Things = new List<VacuumThing>();
        /// <summary>The index in <see cref="Things"/> of the weapon taken out of a pawn's hands, or -1.</summary>
        public int Disarmed = -1;
        /// <summary>Where that pawn stands now, for its daze marks. Null puts them where the script's pawn stood, beside its weapon.</summary>
        public Vector2? DazedAt;
        /// <summary>The preview: things are drawn at rest before they fly, and filth as a puddle on the floor.</summary>
        public bool Props;
    }

    /// <summary>
    /// Timing of Suck: seconds in, numbers out, no drawing and no map. The port of
    /// Tools/VfxLab/web/sketches/vacuum-suck.js; the constants are that sketch's defaults. The clock
    /// starts with the wand at rest; the canister rises at <see cref="Rise0"/>, which in game is the
    /// start of the warmup, and the pull starts when the ability fires (<see cref="Pull0"/>).
    /// </summary>
    public static class VacuumSuckTiming
    {
        /// <summary>The wind-up (the ability's warmupTime must match) and the pull, seconds.</summary>
        public const float Windup = 0.3f, Pull = 0.6f;
        /// <summary>The share of the pull each thing spends in the air; the next thing starts this much later.</summary>
        public const float FlightShare = 0.7f, Stagger = 0.14f;
        /// <summary>With many things the gap shrinks so the last one starts at most this long after the first.</summary>
        public const float MostSpread = 0.7f;
        public const float Blink = 0.16f;
        /// <summary>A disarmed pawn is marked for this long after its weapon leaves its hands.</summary>
        public const float Marked = 1.0f;
        public const float Slack = 0.6f;

        // The preview's script: the sketch's defaults, "armed pawn" or "loose things only".
        public const float ScriptDistance = 5f, ScriptRadius = 2f, ScriptInside = 20f, ScriptHold = 1.2f;
        public const float ChunkKg = 20f, RifleKg = 4f;
        /// <summary>In game the result shows this long before the canister sinks, so the caster is not held for the whole result.</summary>
        public const float GameHold = 0.3f;

        public static float Rise0 => VacuumGraphics.Lead;
        public static float Pull0 => VacuumGraphics.Lead + Windup;
        public static float Flight => Pull * FlightShare;
        public static float Gap(int count) => count <= 1 ? Stagger : Mathf.Min(Stagger, MostSpread / (count - 1));
        public static float Start(int i, int count) => Pull0 + i * Gap(count);
        /// <summary>The thing enters the wand's head: in game it is swallowed now.</summary>
        public static float Arrive(int i, int count) => Start(i, count) + Flight;
        /// <summary>The thing has run the hose and the canister swells.</summary>
        public static float Land(int i, int count) => Arrive(i, count) + VacuumGraphics.Bulge;
        public static float Swallow => Pull0 + Flight;
        public static float Result(int count) => Pull0 + Mathf.Max(0, count - 1) * Gap(count) + Flight + VacuumGraphics.Bulge;
        public static float Sink0(int count, float hold) => Result(count) + hold;
        /// <summary>The canister is back in the floor: the caster may go.</summary>
        public static float Home(int count, float hold) => Sink0(count, hold) + VacuumGraphics.Sink;
        public static float End(int count, float hold) => Home(count, hold) + VacuumGraphics.Tail;

        public static float End(VacuumSuckShot shot) => End(shot.Things.Count, shot.Hold);

        /// <summary>The sketch's scene round <paramref name="centre"/>: caster and target cell <see cref="ScriptDistance"/> apart.</summary>
        public static VacuumSuckShot Script(Vector2 centre, float aimDegrees, bool armed)
        {
            var toward = new Vector2(Mathf.Cos(aimDegrees * Mathf.Deg2Rad), Mathf.Sin(aimDegrees * Mathf.Deg2Rad));
            Vector2 o = centre + toward * (ScriptDistance / 2f);
            var shot = new VacuumSuckShot { Caster = centre - toward * (ScriptDistance / 2f), Target = o, Inside = ScriptInside, Props = true };
            shot.Things.Add(new VacuumThing { Kind = VacuumThingKind.Chunk, Ground = o + new Vector2(0.9f, 0.6f), Kg = ChunkKg });
            shot.Things.Add(new VacuumThing { Kind = VacuumThingKind.Filth, Ground = o + new Vector2(-0.8f, -0.7f), Colour = VacuumGraphics.Blood });
            if (armed)
            {
                shot.Things.Add(new VacuumThing { Kind = VacuumThingKind.Rifle, Ground = o + new Vector2(0.80f, -1.13f), H = VacuumGraphics.HandH, Kg = RifleKg, Spin = true });
                shot.Disarmed = 2;
            }
            return shot;
        }

        public static float ScriptEnd(bool armed) => End(armed ? 3 : 2, ScriptHold);
    }
}
