using System.Collections.Generic;
using UnityEngine;

namespace RimArt
{
    /// <summary>One fire Devour takes (or leaves): a burning cell or a burning pawn. Ground points are x east, y north.</summary>
    public struct FlameMeal
    {
        /// <summary>The fire's cell centre, or the pawn's ground point.</summary>
        public Vector2 At;
        /// <summary>A burning pawn (the fire rises from its body), not a cell.</summary>
        public bool Pawn;
        /// <summary>Heat it gives: 1 a cell, 2 a pawn (XML numbers in game).</summary>
        public int Value;
        /// <summary>Picture seconds: the fire lifts off, and it goes into the palm. -1 when the meter had no room for it.</summary>
        public float Depart, Arrive;
        public bool Eaten => Depart >= 0f;
    }

    /// <summary>One Devour as the picture needs it.</summary>
    public sealed class FlameDevourShot
    {
        /// <summary>The wearer's feet, and the centre of the eaten circle.</summary>
        public Vector2 Caster, Target;
        /// <summary>Degrees from the wearer to the target, 0 east, 90 north.</summary>
        public float Aim;
        /// <summary>The eaten circle's radius, cells.</summary>
        public float Radius = FlameDevourTiming.ScriptRadius;
        /// <summary>The meter: heat at the cast, the most it holds, and where Overheating starts.</summary>
        public float StartHeat, MaxHeat = FlameGauntletTiming.ScriptMaxHeat, OverheatAt = FlameGauntletTiming.ScriptOverheatAt;
        /// <summary>Every fire in the circle, nearest the palm first; the first <see cref="Eaten"/> are taken. Set by <see cref="FlameDevourTiming.Plan"/>.</summary>
        public readonly List<FlameMeal> Meals = new List<FlameMeal>();
        public int Eaten;
        /// <summary>When the meter filled with fires still left ("too hot"), or -1.</summary>
        public float RefusedAt = -1f;
        /// <summary>The hand closes; heat is final.</summary>
        public float Result;
        public float Hold = FlameDevourTiming.Hold;
        /// <summary>
        /// Previews only: draw every fire in <see cref="Meals"/> and in <see cref="Outside"/> as the
        /// sketch does, and the burning pawns. In game those are the map's own fires and pawns, and the
        /// picture draws a fire only from the moment it lifts off.
        /// </summary>
        public bool DrawFires;
        public readonly List<Vector2> Outside = new List<Vector2>();
        /// <summary>Where the wearer is now; null: <see cref="Caster"/>.</summary>
        public Vector2? CasterAt;
    }

    /// <summary>
    /// Timing of Devour: seconds in, numbers out, no drawing and no map. The port of
    /// Tools/VfxLab/web/sketches/flame-gauntlet-devour.js; the constants are that sketch's defaults.
    ///   0.00 rest, 0.20 wind-up 0.3 s (the palm opens, the ring shows), 0.50 pull: the nearest fire
    ///   lifts off and flies 0.45 s into the palm, the next 0.08 s behind; each arrival adds its heat.
    ///   The last arrival + 0.15 is the result (the hand closes), then the hold and the tail.
    /// In game the picture's clock starts at <see cref="FlameGauntletTiming.Lead"/> when the 0.3 s
    /// warmup begins, so the fires depart when the ability is applied.
    /// </summary>
    public static class FlameDevourTiming
    {
        public const float Windup = 0.3f, Travel = 0.45f, Gap = 0.08f, Close = 0.3f, Hold = 1.3f;
        /// <summary>The first Overheating burn, this long into the result (5 s in game).</summary>
        public const float BurnTick = 0.6f;
        /// <summary>The sketch's radius and heat per cell / per pawn; XML fields in game.</summary>
        public const float ScriptRadius = 3f;
        public const int ScriptHeatPerCell = 1, ScriptHeatPerPawn = 2;

        public static float Pull => FlameGauntletTiming.Lead + Windup;
        public static float End(FlameDevourShot shot) => shot.Result + shot.Hold + FlameGauntletTiming.Tail;

        /// <summary>The open palm, where the fires go in: 0.3 along the aim and 0.12 across from the feet.</summary>
        public static Vector2 Palm(Vector2 caster, float aimDegrees)
        {
            float r = aimDegrees * Mathf.Deg2Rad, ca = Mathf.Cos(r), sa = Mathf.Sin(r);
            return new Vector2(caster.x + 0.3f * ca - 0.12f * sa, caster.y + 0.3f * sa + 0.12f * ca);
        }

        /// <summary>
        /// Sorts the meals by distance from the palm, takes them while the meter has room, and sets
        /// their times, the refusal and the result. The sketch's plan().
        /// </summary>
        public static void Plan(FlameDevourShot shot)
        {
            Vector2 palm = Palm(shot.Caster, shot.Aim);
            shot.Meals.Sort((a, b) => (a.At - palm).sqrMagnitude.CompareTo((b.At - palm).sqrMagnitude));
            float heat = shot.StartHeat;
            int eaten = 0;
            bool full = false;
            for (int i = 0; i < shot.Meals.Count; i++)
            {
                FlameMeal meal = shot.Meals[i];
                if (full || heat + meal.Value > shot.MaxHeat + 0.001f)
                {
                    full = true;
                    meal.Depart = meal.Arrive = -1f;
                }
                else
                {
                    meal.Depart = Pull + eaten * Gap;
                    meal.Arrive = meal.Depart + Travel;
                    heat += meal.Value;
                    eaten++;
                }
                shot.Meals[i] = meal;
            }
            shot.Eaten = eaten;
            shot.RefusedAt = eaten < shot.Meals.Count ? Pull + eaten * Gap : -1f;
            shot.Result = eaten > 0 ? Pull + (eaten - 1) * Gap + Travel + 0.15f : Pull + 0.3f;
        }

        /// <summary>The meter at <paramref name="s"/>: each arrival adds its heat over 0.15 s.</summary>
        public static float HeatAt(FlameDevourShot shot, float s)
        {
            float heat = shot.StartHeat;
            for (int i = 0; i < shot.Meals.Count; i++)
            {
                FlameMeal meal = shot.Meals[i];
                if (meal.Eaten && s >= meal.Arrive) heat += meal.Value * Mathf.Clamp01((s - meal.Arrive) / 0.15f);
            }
            return heat;
        }
    }

    /// <summary>Numbers both Flame Gauntlet pictures share.</summary>
    public static class FlameGauntletTiming
    {
        public const float Lead = 0.2f, Tail = 0.4f;
        /// <summary>The sketch's meter; XML fields on the weapon in game.</summary>
        public const float ScriptMaxHeat = 20f, ScriptOverheatAt = 15f;
    }
}
