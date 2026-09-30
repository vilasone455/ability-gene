using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using F = RimArt.VectorFlick;
using V = RimArt.VectorApply;
using Taper = RimArt.GokuGraphics.Taper;

namespace RimArt
{
    /// <summary>
    /// Previews only: nobody is hurt, no round is changed and no pawn is drawn. Each entry plays Vector
    /// Flick or the manipulation's Apply round the chosen cell, which is the cell the lab's sketch
    /// centres on, so the recorder can compare the port with the sketch. The Apply preview also flies
    /// the sketch's 8 rounds (its yellow bullet stand-ins) and draws its stand-in of the paused panel,
    /// because in game the real bullets and VectorEditDrawer draw those.
    /// </summary>
    public static class DebugActions_FlickApplyPictures
    {
        [RimArtDebug("Accelerator", "vector flick: one raider")]
        public static void FlickOne() => Play(FlickApplyPreview.Flick, 15f);

        [RimArtDebug("Accelerator", "vector flick: one raider, aim north")]
        public static void FlickNorth() => Play(FlickApplyPreview.Flick, 90f);

        [RimArtDebug("Accelerator", "vector flick: one raider, aim north-west")]
        public static void FlickNorthWest() => Play(FlickApplyPreview.Flick, 135f);

        [RimArtDebug("Accelerator", "vector flick: three raiders, 2 s cooldown")]
        public static void FlickThree() => Play(FlickApplyPreview.FlickThree, 15f);

        [RimArtDebug("Accelerator", "vector apply: all 4 groups changed")]
        public static void ApplyAll() => Play(FlickApplyPreview.Apply4, 0f);

        [RimArtDebug("Accelerator", "vector apply: 2 groups changed")]
        public static void ApplyTwo() => Play(FlickApplyPreview.Apply2, 0f);

        [RimArtDebug("Accelerator", "clear vector flick / apply preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_FlickApplyPreview>();
            if (preview != null) preview.active = false;
        }

        private static void Play(FlickApplyPreview play, float degrees) =>
            Find.CurrentMap.GetComponent<MapComponent_FlickApplyPreview>().Play(UI.MouseCell(), play, degrees);
    }

    public enum FlickApplyPreview { Flick, FlickThree, Apply4, Apply2 }

    public sealed class MapComponent_FlickApplyPreview : MapComponent
    {
        public bool active;
        private FlickApplyPreview mode;
        private float seconds, degrees;
        private IntVec3 cell;

        // --- Vector Flick: the sketch's scene -------------------------------------------------------------------------------
        /// <summary>The sketch stands this long before the first warm-up; the raider is Cells out along the aim.</summary>
        public const float FlickLead = 0.3f, Cells = 9f;
        private static readonly Color Pants = new Color(0.34f, 0.34f, 0.37f), Shoe = new Color(0.07f, 0.07f, 0.08f);
        /// <summary>The three-kick scenario's raiders: degrees off the aim, share of the distance.</summary>
        private static readonly float[,] Spread = { { 0f, 1f }, { 30f, 0.75f }, { -24f, 0.8f } };

        // --- Apply: the sketch's volley, in its frame (x toward the shooters, z across) --------------------------------------
        /// <summary>The panel opens at Freeze and Apply is pressed at ApplyAt; rounds fly at Speed; the shooters stand Shooters out.</summary>
        public const float Freeze = 0.5f, ApplyAt = 1.3f, Speed = 42f, Shooters = 9f, BaseRange = 20f, UnchangedRange = 20f, OnLine = 0.45f, Reach = 12f;
        private static readonly float[] ShooterAcross = { 2.4f, 0f, -2.2f };
        /// <summary>This preview's copy of VectorEditDefaults.StrainCosts (not linked in the lab's recorder).</summary>
        private static readonly float[] Strain = { 0.08f, 0.24f, 0.48f, 0.8f };
        // Rounds: shooter, cells before the point it was aimed at when caught, across at Accelerator, group 1-4 or 0.
        private static readonly float[,] RoundLayout =
        {
            { 1, 3.2f, 0.1f, 1 }, { 1, 4.6f, -0.15f, 1 }, { 1, 6f, 0.2f, 1 }, { 0, 2.8f, 0.2f, 2 },
            { 0, 4.4f, -0.1f, 2 }, { 2, 3.6f, 0.15f, 3 }, { 2, 5.5f, -0.2f, 4 }, { 2, 2.2f, 0.9f, 0 },
        };
        /// <summary>Each group's rotation (degrees) and force: group 1 at the sketch's slider defaults, 2-4 fixed.</summary>
        private static readonly float[,] Groups = { { 180f, 1f }, { 110f, 0.5f }, { 180f, 2f }, { -70f, 0.25f } };
        private static readonly Color Bullet = new Color(1f, 0.86f, 0.5f), Grey = new Color(0.6f, 0.6f, 0.6f);
        private static readonly Color[] GroupColours =
            { new Color(0.35f, 0.85f, 1f), new Color(1f, 0.65f, 0.25f), new Color(1f, 0.45f, 0.85f), new Color(0.45f, 0.95f, 0.5f) };
        private static readonly Vector2[] panelLine = new Vector2[2];

        /// <summary>One round of the volley, worked out from the layout alone, as the sketch's plan().</summary>
        private struct Round
        {
            public Vector2 shooter, heading, caught, turned;
            public float fired, force, speed, end, lands;
            public int group;
            public bool changed;
        }

        private Round[] rounds;
        private VectorApplyShot applyShot;

        public MapComponent_FlickApplyPreview(Map map) : base(map) { }

        public void Play(IntVec3 at, FlickApplyPreview play, float aim)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            mode = play;
            degrees = aim;
            seconds = 0f;
            active = true;
            if (play == FlickApplyPreview.Apply4 || play == FlickApplyPreview.Apply2) PlanVolley(play == FlickApplyPreview.Apply4 ? 4 : 2);
        }

        // --- the timelines, which the recorder's phase markers read too ---------------------------------------------------

        private static int Kicks(FlickApplyPreview play) => play == FlickApplyPreview.FlickThree ? 3 : 1;

        private static VectorFlickShot FlickShot(Vector2 feet, float aim, int k) => new VectorFlickShot
        {
            Feet = feet, Aim = aim + Spread[k, 0], Distance = Mathf.Min(F.MaxRange, Mathf.Max(1.5f, Cells * Spread[k, 1])),
            Warmup = F.Warmup, Speed = F.Speed, Pants = Pants, Shoe = Shoe, Seed = k,
        };

        private static float WarmStart(int k) => FlickLead + k * (F.Warmup + F.Cooldown);

        /// <summary>The sketch's markers: Stand, then per kick Warm-up, Kick and Hit (numbered with three raiders).</summary>
        public static List<(string name, float seconds)> FlickPhases(bool three)
        {
            var phases = new List<(string, float)> { ("Stand", 0f) };
            int kicks = three ? 3 : 1;
            for (int k = 0; k < kicks; k++)
            {
                string n = three ? " " + (k + 1) : "";
                VectorFlickShot shot = FlickShot(Vector2.zero, 0f, k);
                float warm = WarmStart(k);
                phases.Add(("Warm-up" + n, warm));
                phases.Add(("Kick" + n, warm + shot.KickAt));
                phases.Add(("Hit" + n, warm + shot.HitAt));
            }
            return phases;
        }

        private static float FlickDuration(FlickApplyPreview play)
        {
            int last = Kicks(play) - 1;
            return WarmStart(last) + FlickShot(Vector2.zero, 0f, last).Duration;
        }

        /// <summary>The sketch's markers: Volley, Paused, Apply and the last round stopping.</summary>
        public static List<(string name, float seconds)> ApplyPhases(int groups)
        {
            Round[] plan = Plan(groups);
            float last = 0f;
            foreach (Round r in plan) last = Mathf.Max(last, r.lands);
            return new List<(string, float)> { ("Volley", 0f), ("Paused, panel open", Freeze), ("Apply", ApplyAt), ("Last round stops", last) };
        }

        private static float ApplyDuration(Round[] plan)
        {
            float end = 0f;
            foreach (Round r in plan) end = Mathf.Max(end, r.lands + V.TrailLength(r.force) / r.speed);
            return end + V.Tail;
        }

        private static Vector2 Rotate(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>Every round of the volley in its frame, from the layout alone: where it was caught, where it goes and where it stops.</summary>
        private static Round[] Plan(int groups)
        {
            int count = RoundLayout.GetLength(0);
            var pawns = new List<Vector2> { Vector2.zero };
            foreach (float z in ShooterAcross) pawns.Add(new Vector2(Shooters, z));
            var plan = new Round[count];
            for (int i = 0; i < count; i++)
            {
                int si = (int)RoundLayout[i, 0], g = (int)RoundLayout[i, 3];
                var r = new Round { shooter = new Vector2(Shooters, ShooterAcross[si]), group = g };
                var aimAt = new Vector2(0f, RoundLayout[i, 2]);
                r.heading = (aimAt - r.shooter).normalized;
                r.caught = aimAt - r.heading * RoundLayout[i, 1];
                r.fired = Freeze - (r.caught - r.shooter).magnitude / Speed;
                r.changed = g > 0 && g <= groups;
                float rot = r.changed ? Groups[g - 1, 0] : 0f;
                r.force = r.changed ? Groups[g - 1, 1] : 1f;
                r.turned = r.changed ? Rotate(r.heading, rot) : r.heading;
                r.speed = Speed * r.force;
                // The first pawn on the new line inside the range, or the end of the range.
                r.end = r.changed ? BaseRange * r.force : UnchangedRange;
                foreach (Vector2 q in pawns)
                {
                    Vector2 d = q - r.caught;
                    float along = Vector2.Dot(d, r.turned), off = Mathf.Abs(d.x * r.turned.y - d.y * r.turned.x);
                    if (along > 0.3f && along < r.end && off < OnLine) r.end = along;
                }
                r.lands = ApplyAt + r.end / r.speed;
                plan[i] = r;
            }
            return plan;
        }

        private void PlanVolley(int groups)
        {
            rounds = Plan(groups);
            int changed = 0;
            foreach (Round r in rounds) if (r.changed) changed++;
            applyShot = new VectorApplyShot { Strain = Strain[groups - 1], Reach = Reach, Rounds = new VectorApplyRound[changed] };
        }

        private float Duration() => mode == FlickApplyPreview.Apply4 || mode == FlickApplyPreview.Apply2 ? ApplyDuration(rounds) : FlickDuration(mode);

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            float before = seconds;
            seconds += Time.unscaledDeltaTime;
            Vector3 centre = cell.ToVector3Shifted();
            var o = new Vector2(centre.x, centre.z);
            if (mode == FlickApplyPreview.Apply4 || mode == FlickApplyPreview.Apply2) DrawApply(o, before);
            else DrawFlick(o, before);
            if (seconds >= Duration()) active = false;
        }

        private void ShakeAt(float before, float at, float size)
        {
            if (before < at && seconds >= at) Find.CameraDriver.shaker.DoShake(size);
        }

        // The sketch's scene: Accelerator half the raider distance back from the cell along the aim, the
        // raider as far past it; each kick starts a warm-up plus the cooldown after the last.
        private void DrawFlick(Vector2 o, float before)
        {
            Vector2 feet = o - VfxDraw.Turn(degrees) * (Cells / 2f);
            for (int k = 0; k < Kicks(mode); k++)
            {
                VectorFlickShot shot = FlickShot(feet, degrees, k);
                float warm = WarmStart(k);
                ShakeAt(before, warm + shot.KickAt, F.KickShake);
                ShakeAt(before, warm + shot.HitAt, F.HitShake);
                VectorFlickGraphics.Draw(shot, seconds - warm, map);
            }
        }

        // The sketch's scene: the volley from the east, Accelerator half the shooter distance west of the
        // cell. The preview's script: the rounds fly in, stop while the panel is open, and after Apply
        // fly on along their new headings; their live points feed the picture.
        private void DrawApply(Vector2 o, float before)
        {
            Vector2 feet = o - Vector2.right * (Shooters / 2f);
            float chest = GokuGraphics.Chest, age = seconds - ApplyAt;
            bool applied = age >= 0f;
            Vector2 OnMap(Vector2 v, bool up = false) => new Vector2(feet.x + v.x, feet.y + v.y + (up ? chest : 0f));
            Begin(feet);
            ShakeAt(before, ApplyAt, V.Shake);

            // The stand-in for the paused panel's lines (VectorEditDrawer draws the real ones).
            float panel = seconds < Freeze ? 0f : applied ? 1f - Mathf.Clamp01(age / 0.08f) : VfxMath.Smooth((seconds - Freeze) / 0.1f);
            if (panel > 0f)
            {
                PaperBombGraphics.RingAt(feet, Reach, Fade(AcceleratorGraphics.White, 0.3f * panel), Floor + 0.02f);
                foreach (Round r in rounds)
                {
                    Vector2 c = OnMap(r.caught, true);
                    panelLine[0] = OnMap(r.caught - r.heading * 1.4f, true);
                    panelLine[1] = c;
                    GokuGraphics.Line(panelLine, 0.03f, Fade(Grey, 0.7f * panel), null, Overhead + 0.03f, Taper.None);
                    if (!r.changed)
                    {
                        PaperBombGraphics.RingAt(c, 0.28f, Fade(AcceleratorGraphics.White, 0.5f * panel), Overhead + 0.031f);
                        continue;
                    }
                    Color colour = GroupColours[r.group - 1];
                    PaperBombGraphics.RingAt(c, 0.3f, Fade(colour, 0.95f * panel), Overhead + 0.031f);
                    panelLine[0] = c;
                    panelLine[1] = OnMap(r.caught + r.turned * r.end, true);
                    GokuGraphics.Line(panelLine, 0.04f, Fade(colour, 0.75f * panel), null, Overhead + 0.03f, Taper.None);
                }
            }

            // The rounds (the sketch's yellow stand-ins for the vanilla bullet), and the live points of the changed ones.
            int n = 0;
            for (int i = 0; i < rounds.Length; i++)
            {
                Round r = rounds[i];
                if (seconds < r.fired) continue;
                Vector2 pos, dir;
                bool flying;
                if (seconds < Freeze) { pos = r.shooter + r.heading * ((seconds - r.fired) * Speed); dir = r.heading; flying = true; }
                else if (!applied) { pos = r.caught; dir = r.heading; flying = false; }
                else { float d = Mathf.Min(r.end, age * r.speed); pos = r.caught + r.turned * d; dir = r.turned; flying = d < r.end; }
                if (r.changed)
                    applyShot.Rounds[n++] = new VectorApplyRound
                    {
                        Caught = OnMap(r.caught, true), OldHeading = r.heading, NewHeading = r.turned, Force = r.force, Speed = r.speed,
                        Live = OnMap(pos, true), Exists = flying, Seed = i,
                    };
                if (!flying && applied) continue;
                Vector2 at = OnMap(pos, true);
                float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                DrawMesh(MeshPool.plane10, at - dir * 0.12f, Overhead + 0.1f, 0.06f, 0.32f, 90f - ang, Bullet, solid);
                Sprite(at, 0.22f, 0.22f, Fade(Bullet, 0.7f), glow, Overhead + 0.101f);
            }

            if (!applied) return;
            applyShot.Feet = feet;
            VectorApplyGraphics.Draw(applyShot, age, map);
        }
    }
}
