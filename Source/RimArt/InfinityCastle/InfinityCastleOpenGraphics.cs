using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.CastleEffectGraphics;
using T = RimArt.InfinityCastleOpenTiming;

namespace RimArt
{
    /// <summary>
    /// Draws Infinity Castle's home-map side round the target cell. Take: the warm-up ring at the radius
    /// and pale outlines where the doors will open, the strum's rings at the carrier's biwa, the answer
    /// ring running out as a floor door snaps open under each hostile and the shaft's dark closes, then
    /// the carrier's own door. Return: the ring coming back, a door at each taken-from cell with the
    /// shaft's dark lifting, the carrier's door, the corpse's door beside the target cell.
    ///
    /// The port of Tools/VfxLab/web/sketches/infinity-castle-open.js at the home map's own layers. Its
    /// stand-ins are not ported, and with them go everything drawn on them: the pawns, Nakime and her
    /// bachi, the carried colonist, the corpse and its rifle and blood. Those are the ability's.
    /// </summary>
    internal static class InfinityCastleOpenGraphics
    {
        public static void DrawPreview(Vector3 centre, bool take, float seconds, Map map) =>
            Draw(new Vector2(centre.x, centre.z), take, seconds, map);

        public static void Draw(Vector2 o, bool take, float s, Map map)
        {
            if (s < 0f || s >= (take ? T.TakeDuration : T.ReturnDuration) || !Shown(o, map)) return;
            Begin(o);
            CastleLayers layers = CastleLayers.Pocket;       // the game's own heights, as on any map
            Vector2 caster = o + T.Caster;
            Color pale = CastleRoomGraphics.Strum;

            if (take)
            {
                // Warm-up: the radius and the doors to come.
                float warm = Smooth(s / Mathf.Max(0.1f, T.StrumAt)) * (1f - Smooth((s - T.StrumAt - 0.3f) / 0.5f));
                ThinRing(o, T.Radius, 0.07f, Fade(pale, 0.45f * warm), Floor + 0.01f);
                for (int i = 0; i < T.Taken; i++)
                    if (s < T.DoorOf(i))
                        DoorMark(o + T.Hostiles[i], 0.55f * Smooth(s / Mathf.Max(0.1f, T.StrumAt)) * (0.8f + 0.2f * Mathf.Sin(s * 9f + i)), layers.Door + 0.02f + i * 0.0005f);
                if (s < T.CasterDoor) DoorMark(caster, 0.3f * warm, layers.Door + 0.02f);

                // The answer: a ring runs from the target cell to the radius as the doors open.
                float ringAge = s - T.StrumAt - 0.05f;
                if (ringAge >= 0f && ringAge < T.RingTime + 0.4f)
                {
                    float u = Clamp(ringAge / T.RingTime), fade = 1f - Clamp((ringAge - T.RingTime) / 0.4f), r = T.Radius * EaseOut(u);
                    ThinRing(o, r, 0.12f, Fade(pale, 0.7f * fade), layers.Fx);
                    Sprite(o, r * 2f, r * 2f, Fade(pale, 0.06f * fade), soft, Floor + 0.012f);
                }

                // Each taken hostile's door snaps open, the shaft's dark closes, the door shuts and fades.
                for (int i = 0; i < T.Taken; i++)
                {
                    Vector2 at = o + T.Hostiles[i];
                    float age = s - T.DoorOf(i);
                    DoorAt(age, T.Sink + 0.05f, out float alpha, out float open);
                    if (age < DoorEnd(T.Sink + 0.05f)) FloorDoor(at, open, alpha, s, layers.Door);
                    if (age >= DoorThrough) Sinking(at, Clamp((age - DoorThrough) / T.Sink), layers);
                }

                // The carrier follows through her own door; the strum rings out from her biwa.
                float casterAge = s - T.CasterDoor;
                DoorAt(casterAge, T.Sink + 0.05f, out float casterAlpha, out float casterOpen);
                if (casterAge >= -0.2f) FloorDoor(caster, casterOpen, casterAlpha, s, layers.Door);
                Strum(T.BiwaAt(caster), s - T.StrumAt, 3f, 0.5f, layers.Fx);
                return;
            }

            // Return: the ring comes back, then a door at each taken-from cell; the corpse's beside the target cell.
            float back = s - T.First;
            if (back >= 0f && back < 0.7f) ThinRing(o, T.Radius * EaseOut(back / 0.3f), 0.1f, Fade(pale, 0.5f * (1f - back / 0.7f)), layers.Fx);
            for (int i = 0; i < T.Taken; i++)
            {
                bool dead = i == T.KilledInside;
                Vector2 at = o + (dead ? T.CorpseAt : T.Hostiles[i]);
                float age = s - (dead ? T.CorpseDoor : T.BackDoorOf(i));
                DoorAt(age, T.Rise * 0.75f, out float alpha, out float open);
                if (age < DoorEnd(T.Rise * 0.75f)) FloorDoor(at, open, alpha, s, layers.Door);
                if (age >= DoorThrough) Rising(at, Clamp((age - DoorThrough) / T.Rise), layers);
            }
            float casterBack = s - T.CasterBackDoor;
            DoorAt(casterBack, T.Rise * 0.75f, out float backAlpha, out float backOpen);
            if (casterBack < DoorEnd(T.Rise * 0.75f)) FloorDoor(caster, backOpen, backAlpha, s, layers.Door);
        }
    }
}
