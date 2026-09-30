using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;
using static RimArt.CastleEffectGraphics;
using T = RimArt.InfinityCastleOpenTiming;

namespace RimArt
{
    /// <summary>
    /// What Infinity Castle's home-map picture shows, in cells from the target cell and seconds on the
    /// picture's clock (0 is the start of the warm-up). Take: a door under each pawn taken, nearest first,
    /// and the carrier's door last. Return: a door at each cell something comes back up through, and the
    /// carrier's. The preview's plans are the Open sketch's script; the ability builds one from the real
    /// pawns (<see cref="InfinityCastleCast"/>).
    /// </summary>
    internal sealed class CastleOpenPlan
    {
        public bool take;
        public float radius = T.Radius, strumAt = T.StrumAt;
        public readonly List<(Vector2 at, float time)> doors = new List<(Vector2 at, float time)>();
        /// <summary>Where the carrier stands (take) or comes back (return), and when her door opens; no door when null.</summary>
        public Vector2? caster;
        public float casterTime;
        /// <summary>The carrier is a real pawn: the shaft's dark closes over her and lifts off her too. The sketch draws its stand-in instead.</summary>
        public bool casterShaft;

        /// <summary>A pawn's door opens as the answer ring passes it: 0.05 s after the strum, then 0.3 s to the radius.</summary>
        public float DoorOf(Vector2 offset) => strumAt + 0.05f + offset.magnitude / Mathf.Max(0.1f, radius) * T.RingTime;

        /// <summary>The carrier goes last, a quarter second after the last pawn has sunk.</summary>
        public static float CasterAfter(float lastDoor) => lastDoor + DoorThrough + T.Sink + 0.25f;

        public float Duration
        {
            get
            {
                if (take) return casterTime + DoorEnd(T.Sink + 0.05f) + T.Hold;
                float last = caster.HasValue ? casterTime : 0f;
                foreach (var (_, time) in doors) last = Mathf.Max(last, time);
                return last + DoorEnd(T.Rise * 0.75f) + T.Hold;
            }
        }

        private static CastleOpenPlan previewTake, previewReturn;

        /// <summary>The Open sketch's script: its ten hostiles (the nearest eight taken), the carrier 9 cells west, the raider killed inside.</summary>
        public static CastleOpenPlan Preview(bool take)
        {
            if (take && previewTake != null) return previewTake;
            if (!take && previewReturn != null) return previewReturn;
            var plan = new CastleOpenPlan { take = take, caster = T.Caster };
            for (int i = 0; i < T.Taken; i++)
            {
                if (take) plan.doors.Add((T.Hostiles[i], T.DoorOf(i)));
                else if (i == T.KilledInside) plan.doors.Add((T.CorpseAt, T.CorpseDoor));
                else plan.doors.Add((T.Hostiles[i], T.BackDoorOf(i)));
            }
            plan.casterTime = take ? T.CasterDoor : T.CasterBackDoor;
            return take ? previewTake = plan : previewReturn = plan;
        }
    }

    /// <summary>
    /// Draws Infinity Castle's home-map side round the target cell. Take: the warm-up ring at the radius
    /// and pale outlines where the doors will open, the strum's rings at the carrier's biwa, the answer
    /// ring running out as a floor door snaps open under each hostile and the shaft's dark closes, then
    /// the carrier's own door. Return: the ring coming back, a door at each taken-from cell with the
    /// shaft's dark lifting, the carrier's door, the corpse's door beside the target cell.
    ///
    /// The port of Tools/VfxLab/web/sketches/infinity-castle-open.js at the home map's own layers. Its
    /// stand-ins are not ported, and with them go everything drawn on them: the pawns, Nakime and her
    /// bachi, the carried colonist, the corpse and its rifle and blood. The ability draws the same
    /// picture over the real pawns, with its own <see cref="CastleOpenPlan"/>.
    /// </summary>
    internal static class InfinityCastleOpenGraphics
    {
        public static void DrawPreview(Vector3 centre, bool take, float seconds, Map map) =>
            Draw(new Vector2(centre.x, centre.z), CastleOpenPlan.Preview(take), seconds, map);

        public static void Draw(Vector2 o, CastleOpenPlan plan, float s, Map map)
        {
            if (s < 0f || s >= plan.Duration || !Shown(o, map)) return;
            Begin(o);
            CastleLayers layers = CastleLayers.Pocket;       // the game's own heights, as on any map
            Color pale = CastleRoomGraphics.Strum;

            if (plan.take)
            {
                // Warm-up: the radius and the doors to come.
                float warm = Smooth(s / Mathf.Max(0.1f, plan.strumAt)) * (1f - Smooth((s - plan.strumAt - 0.3f) / 0.5f));
                ThinRing(o, plan.radius, 0.07f, Fade(pale, 0.45f * warm), Floor + 0.01f);
                for (int i = 0; i < plan.doors.Count; i++)
                    if (s < plan.doors[i].time)
                        DoorMark(o + plan.doors[i].at, 0.55f * Smooth(s / Mathf.Max(0.1f, plan.strumAt)) * (0.8f + 0.2f * Mathf.Sin(s * 9f + i)), layers.Door + 0.02f + i * 0.0005f);
                if (plan.caster.HasValue && s < plan.casterTime) DoorMark(o + plan.caster.Value, 0.3f * warm, layers.Door + 0.02f);

                // The answer: a ring runs from the target cell to the radius as the doors open.
                float ringAge = s - plan.strumAt - 0.05f;
                if (ringAge >= 0f && ringAge < T.RingTime + 0.4f)
                {
                    float u = Clamp(ringAge / T.RingTime), fade = 1f - Clamp((ringAge - T.RingTime) / 0.4f), r = plan.radius * EaseOut(u);
                    ThinRing(o, r, 0.12f, Fade(pale, 0.7f * fade), layers.Fx);
                    Sprite(o, r * 2f, r * 2f, Fade(pale, 0.06f * fade), soft, Floor + 0.012f);
                }

                // Each taken hostile's door snaps open, the shaft's dark closes, the door shuts and fades.
                foreach (var (offset, time) in plan.doors)
                {
                    Vector2 at = o + offset;
                    float age = s - time;
                    DoorAt(age, T.Sink + 0.05f, out float alpha, out float open);
                    if (age < DoorEnd(T.Sink + 0.05f)) FloorDoor(at, open, alpha, s, layers.Door);
                    if (age >= DoorThrough) Sinking(at, Clamp((age - DoorThrough) / T.Sink), layers);
                }

                // The carrier follows through her own door; the strum rings out from her biwa.
                if (!plan.caster.HasValue) return;
                Vector2 caster = o + plan.caster.Value;
                float casterAge = s - plan.casterTime;
                DoorAt(casterAge, T.Sink + 0.05f, out float casterAlpha, out float casterOpen);
                if (casterAge >= -0.2f) FloorDoor(caster, casterOpen, casterAlpha, s, layers.Door);
                if (plan.casterShaft && casterAge >= DoorThrough) Sinking(caster, Clamp((casterAge - DoorThrough) / T.Sink), layers);
                Strum(T.BiwaAt(caster), s - plan.strumAt, 3f, 0.5f, layers.Fx);
                return;
            }

            // Return: the ring comes back, then a door at each taken-from cell; the corpse's beside the target cell.
            float back = s - T.First;
            if (back >= 0f && back < 0.7f) ThinRing(o, plan.radius * EaseOut(back / 0.3f), 0.1f, Fade(pale, 0.5f * (1f - back / 0.7f)), layers.Fx);
            foreach (var (offset, time) in plan.doors)
            {
                Vector2 at = o + offset;
                float age = s - time;
                DoorAt(age, T.Rise * 0.75f, out float alpha, out float open);
                if (age < DoorEnd(T.Rise * 0.75f)) FloorDoor(at, open, alpha, s, layers.Door);
                if (age >= DoorThrough) Rising(at, Clamp((age - DoorThrough) / T.Rise), layers);
            }
            if (!plan.caster.HasValue) return;
            Vector2 home = o + plan.caster.Value;
            float casterBack = s - plan.casterTime;
            DoorAt(casterBack, T.Rise * 0.75f, out float backAlpha, out float backOpen);
            if (casterBack < DoorEnd(T.Rise * 0.75f)) FloorDoor(home, backOpen, backAlpha, s, layers.Door);
            if (plan.casterShaft && casterBack >= DoorThrough) Rising(home, Clamp((casterBack - DoorThrough) / T.Rise), layers);
        }
    }
}
