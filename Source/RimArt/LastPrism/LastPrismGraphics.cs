using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using T = RimArt.LastPrismTiming;

namespace RimArt
{
    /// <summary>A pawn near the prism, as the picture needs it: who flickers in a beam's colour and who smokes.</summary>
    public struct LastPrismPawn
    {
        /// <summary>The pawn's DrawPos on the ground. Its hit flicker sits <see cref="PawnBody.Chest"/> north of it, at the chest.</summary>
        public Vector2 At;
        /// <summary>When it went down on the picture's clock (it smokes for 2.2 s from then and no longer flickers), or +infinity while it stands.</summary>
        public float DownAt;
    }

    /// <summary>Cells a beam from <paramref name="from"/> (a ground point) heading <paramref name="radians"/> runs before a wall stops it, at most <paramref name="max"/>.</summary>
    public delegate float LastPrismWalls(Vector2 from, float radians, float max);

    /// <summary>
    /// The prism as the picture needs it, one frame. Times are on the picture's clock. In the previews the script
    /// (<see cref="LastPrismScript"/>) fills it; in game the rules PR fills it from the wielder, its channel, the map
    /// and the weapon's XML.
    /// </summary>
    public struct LastPrismShot
    {
        /// <summary>The wielder's DrawPos on the ground, and where the prism points now (degrees, 0 east, 90 north).</summary>
        public Vector2 Wielder;
        public float Aim;
        /// <summary>A channel is running or has run: false draws the idle prism only.</summary>
        public bool Firing;
        /// <summary>When the channel started, and when the beam stopped (+infinity while it fires).</summary>
        public float ChannelAt, ReleaseAt;
        /// <summary>The beam stopped because the charge ran out: it flickers out over <see cref="LastPrismTiming.Sputter"/> s instead of fading over <see cref="LastPrismTiming.Fade"/>.</summary>
        public bool Dried;
        /// <summary>Balance, from the weapon's XML: seconds until the beams join, the fan's half angle (degrees), range, the joined beam's width and the fan's hit reach (cells).</summary>
        public float Join, Fan, Range, Width, HitReach;
        /// <summary>Where each beam stops; null lets every beam run its full range.</summary>
        public LastPrismWalls Walls;
        /// <summary>The target's DrawPos: a red ring marks it while the beam fires.</summary>
        public Vector2 Target;
        /// <summary>Pawns the beams may cross, the first <see cref="PawnCount"/> of <see cref="Pawns"/>.</summary>
        public LastPrismPawn[] Pawns;
        public int PawnCount;
        /// <summary>The wielder stands under a roof: no rainbow, no glints, the prism dulled.</summary>
        public bool Roofed;
        /// <summary>The prism is filling in the sun: it flashes as each second of beam fills. Level is the seconds of beam in it, Store what it holds.</summary>
        public bool Charging;
        public float Level, Store;
    }

    /// <summary>This frame's state of the channel and where the prism is, worked out once by <see cref="LastPrismGraphics.Draw"/> for the three drawing classes.</summary>
    internal struct LastPrismFrame
    {
        public float S, Theta, U, Live, Shrink, Opacity, Lane, Lift, JoinAt, Strength, Light;
        public bool Firing, Joins, Joined, LaneBlocked;
        /// <summary>Along the aim and to its left; the prism's base, middle and tip on the ground; the base, middle and tip as drawn at chest height.</summary>
        public Vector2 Dir, Side, BaseG, MidG, TipG, BaseS, MidS, TipS, Sun;
        /// <summary>The beams, the glow round the tip and the prism; under the pawn layer when aiming north, so they pass behind the wielder's head.</summary>
        public float BeamLayer, GlowLayer, PrismLayer;
    }

    /// <summary>
    /// Draws the Last Prism, the port of the lab's last-prism.js. Held: the pyramid prism at chest height, still,
    /// bobbing and glinting, with a small rainbow on the floor where its shadow falls. Firing: the prism rolls and
    /// shimmers, six beams (red to violet) sweep a level fan from its tip, each lighting the floor under it and
    /// stopping on walls; at the join a white flash and ring at the tip and a pulse down one beam with a white core,
    /// a pale sheath, six colour bands side by side, flow lines, sparkles and rainbow light on the floor; the target
    /// ringed in red; pawns flicker in the colour of each beam that crosses them, flash white in the joined beam and
    /// smoke when they go down; the beam fades or flickers out when it stops. Charging in the sun: a flash at the
    /// prism as each second of beam fills. <see cref="DrawMeter"/> is the preview's stand-in for the weapon's button.
    ///
    /// Every beam is a level line at chest height, so the fan lies over the floor (which is also the hit area) and
    /// turns freely with the aim; the prism is a 3D pyramid drawn face by face. No per-facing method.
    ///
    /// Not drawn (the sketch's stand-ins and aids): the wielder and the other pawns, their white tint in the beam,
    /// the walls, the roof cells, the damage bars over the pawns.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class LastPrismGraphics
    {
        internal static readonly Color White = new Color(1f, 1f, 1f), Warn = new Color(0.85f, 0.18f, 0.12f), Sunlight = new Color(1f, 0.82f, 0.32f);
        private static readonly Color SunDim = new Color(0.22f, 0.18f, 0.1f), Smoke = new Color(0.32f, 0.32f, 0.34f);
        private static readonly float ProjectileLayer = AltitudeLayer.Projectile.AltitudeFor();
        internal static readonly float ShadowLayer = AltitudeLayer.Shadows.AltitudeFor();
        internal const float Tau = 6.2831855f;
        /// <summary>The lab's full-sun shadow strength: strength / Daylight is the light, 0 at night.</summary>
        private const float Daylight = 0.32f;
        /// <summary>The meter sits MeterUp above the head top (<see cref="PawnBody.HeadTop"/>).</summary>
        private const float MeterUp = 0.28f;

        /// <summary>The sketch's hue: <paramref name="h"/> turns round the colour wheel from red, at saturation <paramref name="sat"/>.</summary>
        internal static Color Hue(float h, float sat = 0.72f)
        {
            h -= Mathf.Floor(h);
            return new Color(Wheel(5f, h, sat), Wheel(3f, h, sat), Wheel(1f, h, sat));
        }

        private static float Wheel(float n, float h, float sat)
        {
            float q = (n + h * 6f) % 6f;
            return 1f - sat * Mathf.Max(0f, Mathf.Min(q, Mathf.Min(4f - q, 1f)));
        }

        internal static Color Pale(Color c) => Color.Lerp(c, White, 0.45f);

        internal static Vector2 Around(Vector2 c, float radians, float d) => c + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * d;

        /// <summary>Everything but the meter. <paramref name="s"/> is the picture's clock; the idle prism draws for as long as it is called.</summary>
        public static void Draw(in LastPrismShot shot, float s, Map map)
        {
            if (s < 0f || !Shown(shot.Wielder, map)) return;
            Begin(shot.Wielder);
            LastPrismFrame f = Frame(shot, s, map);

            // --- the floor: the prism's shadow, the rainbow in sun, the target ring ---
            Vector2 fall = f.MidG + f.Sun * T.PrismH;
            Sprite(fall, T.PrismLen * 1.2f, T.PrismRad * 1.5f, Fade(ChainSickleGraphics.Body, f.Strength * 0.35f), soft, ShadowLayer, -f.Theta * Mathf.Rad2Deg);
            if (!shot.Roofed && f.Live == 0f) LastPrismPyramidGraphics.Rainbow(fall, f.Sun, f.Light, s);
            if (f.Firing && s >= shot.ChannelAt && s < shot.ReleaseAt) PaperBombGraphics.RingAt(shot.Target, 0.5f, Fade(Warn, 0.7f), Floor + 0.02f);

            // --- the prism, its glints in sun, the glow at its tip as it charges ---
            LastPrismPyramidGraphics.Draw(f.BaseS, f.Theta, f.Firing ? T.Roll(s, shot.ChannelAt, shot.Join, shot.ReleaseAt) : 0f, f.Sun, f.PrismLayer,
                shot.Roofed, f.Joined ? f.Live : f.U * f.Live, s);
            if (!shot.Roofed && f.Live == 0f) LastPrismPyramidGraphics.Glint(f.MidS, s, f.Light);
            LastPrismBeamGraphics.TipGlow(f);

            // --- the beams ---
            if (!f.Joined && f.Live > 0f)
            {
                LastPrismBeamGraphics.Spread(shot, f);
                LastPrismBeamGraphics.DrawFan(f);
            }
            if (f.Joined && f.Live > 0f) LastPrismBeamGraphics.DrawJoined(shot, f);
            if (f.Joins && s >= f.JoinAt && s < f.JoinAt + T.JoinFlash) LastPrismBeamGraphics.JoinFlash(f);

            DrawHits(shot, f);

            // --- the flash at the prism as each second of beam fills in the sun ---
            if (shot.Charging && !shot.Roofed)
            {
                float done = shot.Level % 1f;
                if (s > 0.1f && shot.Level < shot.Store && done < 0.15f)
                    Sprite(f.MidS, 1.2f, 1.2f, Fade(Sunlight, 0.55f * (1f - done / 0.15f) * f.Light), glow, f.GlowLayer + 0.004f);
            }
        }

        private static LastPrismFrame Frame(in LastPrismShot shot, float s, Map map)
        {
            var f = new LastPrismFrame { S = s, Firing = shot.Firing, JoinAt = shot.ChannelAt + shot.Join };
            PowerPoleGraphics.Sun(map, out f.Sun, out f.Strength);
            f.Light = Mathf.Clamp01(f.Strength / Daylight);
            f.Theta = shot.Aim * Mathf.Deg2Rad;
            f.Dir = new Vector2(Mathf.Cos(f.Theta), Mathf.Sin(f.Theta));
            f.Side = new Vector2(-f.Dir.y, f.Dir.x);
            f.Lift = PawnBody.Chest + T.Bobbing(s);
            f.BaseG = shot.Wielder + f.Dir * T.PrismGap;
            f.MidG = f.BaseG + f.Dir * (T.PrismLen / 2f);
            f.TipG = T.Tip(shot.Wielder, f.Dir);
            f.BaseS = f.BaseG + new Vector2(0f, f.Lift);
            f.MidS = f.MidG + new Vector2(0f, f.Lift);
            f.TipS = f.TipG + new Vector2(0f, f.Lift);
            f.BeamLayer = f.Dir.y > 0.3f ? ProjectileLayer : Overhead;
            f.GlowLayer = f.BeamLayer + 0.035f;
            f.PrismLayer = f.BeamLayer + 0.04f;

            // The channel: the narrowing, how much of the beam shows (it comes in over 0.15 s; when it stops it fades, or
            // flickers 25 times a second as it goes when the charge ran out), how far it has shrunk since.
            f.Joins = shot.Firing && f.JoinAt < shot.ReleaseAt;
            f.U = shot.Firing ? T.Narrowed(s, shot.ChannelAt, shot.Join) : 0f;
            float after = shot.Firing ? s - shot.ReleaseAt : -1f, endFade = shot.Dried ? T.Sputter : T.Fade;
            float flicker = shot.Dried && after > 0f ? (Rand(Mathf.FloorToInt(s * 25f) + 7) > 0.4f ? 1f : 0.15f) : 1f;
            f.Live = !shot.Firing || s < shot.ChannelAt ? 0f
                : (after < 0f ? 1f : Mathf.Max(0f, 1f - after / endFade) * flicker) * Mathf.Clamp01((s - shot.ChannelAt) / T.ChannelIn);
            f.Shrink = after > 0f ? 1f - 0.5f * Mathf.Clamp01(after / endFade) : 1f;
            f.Joined = f.Joins && s >= f.JoinAt;
            f.Opacity = T.Opacity(f.U);
            f.Lane = f.Joined ? Reach(shot, f.TipG, f.Theta) : 0f;
            f.LaneBlocked = f.Joined && f.Lane < shot.Range - 1e-6f;
            return f;
        }

        /// <summary>The sky's light, 0 at night to 1 in full sun: the rainbow's and glints' strength, and how fast the prism fills.</summary>
        public static float Light(Map map)
        {
            PowerPoleGraphics.Sun(map, out _, out float strength);
            return Mathf.Clamp01(strength / Daylight);
        }

        internal static float Reach(in LastPrismShot shot, Vector2 from, float radians) =>
            shot.Walls != null ? shot.Walls(from, radians, shot.Range) : shot.Range;

        /// <summary>
        /// Hits on pawns: a flicker and a spark in the colour of each fan beam that crosses one; in the joined beam a white
        /// flash, a ring opening 3 times a second and coloured burn streaks thrown off to both sides; smoke once down.
        /// </summary>
        private static void DrawHits(in LastPrismShot shot, in LastPrismFrame f)
        {
            float s = f.S;
            int flick = Mathf.FloorToInt(s * 20f);
            for (int j = 0; j < shot.PawnCount; j++)
            {
                LastPrismPawn pawn = shot.Pawns[j];
                bool down = s >= pawn.DownAt;
                var chest = new Vector2(pawn.At.x, pawn.At.y + PawnBody.Chest);
                if (!down && !f.Joined && f.Live > 0f)
                {
                    int q = 0;
                    for (int i = 0; i < T.Beams; i++)
                    {
                        if (!LastPrismBeamGraphics.Crosses(i, pawn.At, shot.HitReach)) continue;
                        Color c = LastPrismBeamGraphics.Colour[i];
                        Sprite(chest, 0.45f, 0.45f, Fade(c, 0.65f * f.Live * (Rand(flick + i * 13) > 0.3f ? 1f : 0.4f)), glow, Overhead + 0.04f + q * 0.001f);
                        float ang = Rand(flick * 3 + i) * Tau;
                        Streak(Around(chest, ang, 0.12f), Around(chest, ang, 0.38f), 0.04f, Fade(Pale(c), 0.8f * f.Live), whiteGlow, Overhead + 0.045f, 3);
                        q++;
                    }
                }
                if (!down && f.Joined && f.Live > 0f && T.OnLine(pawn.At.x, pawn.At.y, f.TipG.x, f.TipG.y, f.Theta, f.Lane, shot.Width / 2f))
                {
                    float ring = s * 3f % 1f;
                    Sprite(chest, 0.8f, 0.8f, Fade(White, 0.55f * f.Live), glow, Overhead + 0.05f);
                    PaperBombGraphics.RingAt(chest, 0.25f + ring * 0.5f, Fade(White, 0.7f * (1f - ring) * f.Live), Overhead + 0.051f, false, whiteGlow);
                    for (int q = 0; q < 5; q++)
                    {
                        float v = (s * 4f + Rand(q + 60)) % 1f, ang = f.Theta + (Rand(q + 61) - 0.5f) * 1.6f + (q % 2 == 1 ? 0.9f : -0.9f), r0 = 0.2f + v * 0.7f;
                        Streak(Around(chest, ang, r0), Around(chest, ang, r0 + 0.3f), 0.05f, Fade(Hue(Rand(q) + s * 0.3f, 0.5f), (1f - v) * f.Live), whiteGlow, Overhead + 0.052f, 3);
                    }
                }
                if (down)
                    for (int q = 0; q < 5; q++)
                    {
                        float v = (s - pawn.DownAt - q * 0.25f) / 1.2f;
                        if (v < 0f || v > 1f) continue;
                        // Its own small step each, so the puffs keep the sketch's order in Unity.
                        Sprite(new Vector2(pawn.At.x + (Rand(q + 70) - 0.5f) * 0.4f + v * 0.2f, pawn.At.y + 0.1f + v * 0.8f), 0.35f + v * 0.5f, 0.3f + v * 0.4f,
                            Fade(Smoke, 0.35f * ChainSickleGraphics.Bump(v)), soft, Overhead + 0.004f + (j * 5 + q) * 0.00001f);
                    }
            }
        }

        /// <summary>
        /// The preview's stand-in for the meter on the weapon's button, drawn as the sketch draws it over the wielder's head:
        /// one segment per second of beam the prism holds, filled in sun yellow, and a red flash behind it while
        /// <paramref name="warn"/> (0 to 1) is on. In game this is the gizmo, not a drawing on the map.
        /// </summary>
        public static void DrawMeter(Vector2 wielder, float level, float store, float warn)
        {
            const float pitch = 0.095f, w = 0.075f, h = 0.1f;
            int segments = Mathf.RoundToInt(store);
            float x0 = wielder.x - pitch * (segments - 1) / 2f, z = wielder.y + PawnBody.HeadTop + MeterUp;
            var middle = new Vector2(wielder.x, z);
            if (warn > 0f) Sprite(middle, pitch * store + 0.12f, h + 0.12f, Fade(Warn, 0.8f * warn), solid, Overhead + 0.3f);
            Sprite(middle, pitch * store + 0.05f, h + 0.05f, Fade(ChainSickleGraphics.Body, 0.8f), solid, Overhead + 0.301f);
            for (int k = 0; k < segments; k++)
            {
                float x = x0 + k * pitch, fill = Mathf.Clamp01(level - k);
                Sprite(new Vector2(x, z), w, h, SunDim, solid, Overhead + 0.302f);
                if (fill > 0f) Sprite(new Vector2(x - w / 2f + w * fill / 2f, z), w * fill, h, Sunlight, solid, Overhead + 0.303f);
            }
        }
    }
}
