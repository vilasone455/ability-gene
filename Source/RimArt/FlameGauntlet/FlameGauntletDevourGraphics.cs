using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.FlameGauntletGraphics;
using T = RimArt.FlameDevourTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Devour: the arm comes forward and the fingers fan open, a red ring shows the radius,
    /// each fire in it lifts off nearest first and arches into the palm, the palm flashes and the
    /// plate glows a step hotter per arrival, and the hand closes at the result. When the meter
    /// fills with fires left, the hand snaps shut with a pale "too hot" puff. The port of
    /// Tools/VfxLab/web/sketches/flame-gauntlet-devour.js.
    ///
    /// Differences from the sketch:
    /// - The refusal puff is pale Steam coughed out of the palm (as Release's "too cold"), not the
    ///   lib's grey refused() puff, which is nearly invisible on the dark iron.
    /// - In game (DrawFires false) the map's fires and burning pawns are real: a fire is drawn only
    ///   from its Depart, shrinking over 0.2 s (then the put-out smoke for a pawn), with no scorch
    ///   (the kit leaves ash), and fires not taken or outside the radius are not drawn.
    /// The stand-in pawns are not drawn. The tally is drawn by the preview (a gizmo in game).
    /// </summary>
    public static class FlameGauntletDevourGraphics
    {
        /// <summary>Wearer to target in the preview, cells.</summary>
        public const float ScriptDistance = 4f;
        /// <summary>The preview's burning cells beyond the radius: (along, across) from the target, rounded to the grid.</summary>
        private static readonly Vector2[] Outside = { new Vector2(0f, 4.2f), new Vector2(3.5f, -2.5f) };
        private static readonly FlameDevourShot preview = new FlameDevourShot();

        /// <summary>
        /// The sketch's scenario round <paramref name="target"/>: 0 base fire (3 x 3 cells), 1 burning
        /// pawn (the pawn and four cells round it), 2 starting hot (14) (3 x 3 cells, at least 14 heat).
        /// The wearer stands <see cref="ScriptDistance"/> back along the aim. Planned.
        /// </summary>
        internal static FlameDevourShot Script(Vector2 target, float aimDegrees, int scenario, float startHeat, FlameDevourShot into = null)
        {
            FlameDevourShot shot = into ?? new FlameDevourShot();
            var f = new ChainSickleFrame(aimDegrees, Vector2.zero);
            shot.Caster = f.Ground(target, -ScriptDistance, 0f);
            shot.Target = target;
            shot.Aim = aimDegrees;
            shot.StartHeat = scenario == 2 ? Mathf.Max(startHeat, 14f) : startHeat;
            shot.DrawFires = true;
            shot.CasterAt = null;
            shot.Meals.Clear();
            shot.Outside.Clear();
            if (scenario == 1)
            {
                shot.Meals.Add(new FlameMeal { At = target, Pawn = true, Value = T.ScriptHeatPerPawn });
                shot.Meals.Add(new FlameMeal { At = target + new Vector2(1f, 0f), Value = T.ScriptHeatPerCell });
                shot.Meals.Add(new FlameMeal { At = target + new Vector2(-1f, 0f), Value = T.ScriptHeatPerCell });
                shot.Meals.Add(new FlameMeal { At = target + new Vector2(0f, 1f), Value = T.ScriptHeatPerCell });
                shot.Meals.Add(new FlameMeal { At = target + new Vector2(0f, -1f), Value = T.ScriptHeatPerCell });
            }
            else
            {
                for (int x = -1; x <= 1; x++)
                    for (int z = -1; z <= 1; z++)
                        shot.Meals.Add(new FlameMeal { At = target + new Vector2(x, z), Value = T.ScriptHeatPerCell });
            }
            foreach (Vector2 q in Outside)
            {
                Vector2 g = f.Ground(target, q.x, q.y);
                shot.Outside.Add(new Vector2(target.x + ChainSickleGraphics.Round(g.x - target.x), target.y + ChainSickleGraphics.Round(g.y - target.y)));
            }
            T.Plan(shot);
            return shot;
        }

        /// <summary>
        /// The preview: the scenario round the chosen cell (the target), fires drawn as in the sketch,
        /// and the heat tally under the wearer's feet.
        /// </summary>
        public static void DrawPreview(Vector3 centre, float aimDegrees, int scenario, float startHeat, float seconds, Map map)
        {
            FlameDevourShot shot = Script(new Vector2(centre.x, centre.z), aimDegrees, scenario, startHeat, preview);
            if (seconds < 0f || seconds >= T.End(shot)) return;
            Draw(shot, seconds, map);
            Tally(shot.Caster, T.HeatAt(shot, seconds), shot.MaxHeat, shot.OverheatAt, shot.Aim, 1f);
        }

        /// <param name="weapon">Draw the gauntlet in the hand. In game it is left off once the wearer's cast job is over.</param>
        public static void Draw(FlameDevourShot shot, float s, Map map, bool weapon = true)
        {
            if (s < 0f || s >= T.End(shot) || !Shown(shot.Caster, map)) return;
            Begin(shot.Caster);
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            var f = new ChainSickleFrame(shot.Aim, sun);
            Vector2 caster = shot.CasterAt ?? shot.Caster, o = shot.Target;
            float lead = FlameGauntletTiming.Lead;

            // The hand: at the hip at rest, forward and open during the cast, back at the result.
            float raise = Smooth((s - lead) / T.Windup);
            float closing = Smooth((s - shot.Result) / T.Close);
            float snap = shot.RefusedAt >= 0f ? Smooth((s - shot.RefusedAt) / 0.1f) : 0f;
            float open = raise * (1f - closing) * (1f - snap), fwd = raise * (1f - closing);
            Vector2 hand = f.Ground(caster, Mathf.Lerp(0.12f, 0.30f, fwd), Mathf.Lerp(0.18f, 0.12f, fwd));

            float heat = T.HeatAt(shot, s);
            float over = heat >= shot.OverheatAt ? 1f : 0f;

            // Floor first: the radius ring while the pull runs, and every fire or scorch.
            float live = Smooth((s - lead) / 0.2f) * (1f - Smooth((s - shot.Result) / 0.3f));
            Circle(o, shot.Radius, 0.45f * live, Floor + 0.03f, Ember);
            for (int i = 0; i < shot.Meals.Count; i++)
            {
                FlameMeal it = shot.Meals[i];
                bool taken = it.Eaten && s >= it.Depart;
                if (!taken && !shot.DrawFires) continue;
                float amount = taken ? 1f - Clamp01((s - it.Depart) / 0.2f) : 1f;
                if (it.Pawn)
                {
                    BurningPawn(it.At, s, amount);
                    if (taken && amount <= 0f)
                    {
                        // Put out: smoke off the pawn for a second; it stays standing.
                        float age = s - it.Depart - 0.2f;
                        for (int k = 0; k < 3; k++)
                        {
                            float u = Clamp01((age - k * 0.15f) / 1f);
                            if (u <= 0f || u >= 1f) continue;
                            Sprite(new Vector2(it.At.x + (k - 1) * 0.1f + Mathf.Sin(u * 6f + k) * 0.06f, it.At.y + 0.3f + u * 0.8f * Lift),
                                0.2f + u * 0.3f, 0.18f + u * 0.25f, Fade(Smoke, 0.45f * (1f - u)), PowerPoleGraphics.puff, Y + 0.16f);
                        }
                    }
                }
                else FireCell(it.At, s, amount, i + 1, Y + 0.03f, 0f, shot.DrawFires);
            }
            if (shot.DrawFires)
                for (int i = 0; i < shot.Outside.Count; i++) FireCell(shot.Outside[i], s, 1f, 30 + i, Y + 0.03f);

            float burn = s - shot.Result - T.BurnTick;
            Overheating(caster, s, over * Smooth((s - shot.Result) / 0.3f), over > 0f && burn >= 0f ? 1 : 0, over > 0f ? Bump(burn / 0.3f) : 0f);

            FlameGauntletPose g = weapon
                ? Gauntlet(hand, shot.Aim, heat, shot.MaxHeat, shot.OverheatAt, s, sun, strength, open)
                : Pose(Raised(hand, HandH), shot.Aim);

            // The fires in flight, and the flash where each one goes in.
            for (int i = 0; i < shot.Meals.Count; i++)
            {
                FlameMeal it = shot.Meals[i];
                if (!it.Eaten) continue;
                float u = (s - it.Depart) / T.Travel;
                if (u < 0f || u > 1f) continue;
                var from = new Vector2(it.At.x, it.At.y + (it.Pawn ? 0.45f : 0.3f) * Lift);
                float e = Clamp01(u);
                Parcel(from, g.Palm, 1f - (1f - e) * (1f - e), it.Pawn ? 1.4f : 1f);
                float near = Clamp01((u - 0.7f) / 0.3f);
                if (near > 0f) Sprite(g.Palm, 0.6f * near, 0.5f * near, Fade(Flame, 0.4f * near), glow, Y + 0.15f);
            }
            for (int i = 0; i < shot.Meals.Count; i++)
            {
                FlameMeal it = shot.Meals[i];
                if (!it.Eaten) continue;
                float age = s - it.Arrive;
                if (age >= 0f && age < 0.15f) Sprite(g.Palm, 0.7f, 0.6f, Fade(Core, 0.9f * (1f - age / 0.15f)), glow, Y + 0.152f);
            }
            // While the pull runs the open palm breathes in: a faint glow that pulses.
            if (open > 0f && s >= T.Pull && s < shot.Result)
                Sprite(g.Palm, 0.9f, 0.75f, Fade(Ember, 0.2f * open * (0.6f + 0.4f * Mathf.Sin(s * 14f))), glow, Y + 0.149f);
            // Too hot: a pale cough of steam out of the palm as it snaps shut.
            if (shot.RefusedAt >= 0f) Cough(g.Palm, g.D, s - shot.RefusedAt);
        }
    }
}
