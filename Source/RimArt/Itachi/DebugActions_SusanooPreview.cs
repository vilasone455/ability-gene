using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.SusanooDraw;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// The Susanoo sketch's four scenarios as previews, for the debug window and the lab's
    /// recorder (Tools/VfxLab/Recorder/Catalog.cs reads the phases from SusanooTiming). The
    /// stand-in pawns, rounds and dropped gear of the sketch are not drawn; the attackers'
    /// positions are the sketch's script (direction 30 degrees, seal target 3.5 cells).
    /// </summary>
    public static class DebugActions_SusanooPreview
    {
        [RimArtDebug("Susanoo", "raise")]
        public static void Raise() => Preview().Play(UI.MouseCell(), SusanooPreview.Raise);

        [RimArtDebug("Susanoo", "block")]
        public static void Block() => Preview().Play(UI.MouseCell(), SusanooPreview.Block);

        [RimArtDebug("Susanoo", "seal")]
        public static void Seal() => Preview().Play(UI.MouseCell(), SusanooPreview.Seal);

        [RimArtDebug("Susanoo", "hit")]
        public static void Hit() => Preview().Play(UI.MouseCell(), SusanooPreview.Hit);

        [RimArtDebug("Susanoo", "end")]
        public static void End() => Preview().Play(UI.MouseCell(), SusanooPreview.End);

        [RimArtDebug("Susanoo", "clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_SusanooPreview>();
            if (preview != null) preview.active = false;
        }

        private static MapComponent_SusanooPreview Preview() => Find.CurrentMap.GetComponent<MapComponent_SusanooPreview>();
    }

    public enum SusanooPreview { Raise, Block, Seal, Hit, End }

    public sealed class MapComponent_SusanooPreview : MapComponent
    {
        /// <summary>The sketch's Mechanic settings: attacker direction (0 = east) and the seal target's distance.</summary>
        private const float Dir = 30f, Dist = 3.5f;

        public bool active;
        private SusanooPreview mode;
        private float seconds, duration;
        private IntVec3 cell;
        private int shaken;
        private readonly SusanooPose pose = new SusanooPose();
        private SusanooSword sword;

        public MapComponent_SusanooPreview(Map map) : base(map) { }

        public void Play(IntVec3 at, SusanooPreview play)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            mode = play;
            seconds = 0f;
            shaken = 0;
            Vector3 c = cell.ToVector3Shifted();
            var origin = new Vector2(c.x, c.z);
            float a = Dir * Mathf.Deg2Rad;
            sword = play == SusanooPreview.Seal || play == SusanooPreview.Hit
                ? new SusanooSword(0f, origin, new Vector2(origin.x + Mathf.Cos(a) * Dist, origin.y + Mathf.Sin(a) * Dist), play == SusanooPreview.Seal)
                : null;
            duration = play == SusanooPreview.Raise ? SusanooTiming.RaiseLength()
                : play == SusanooPreview.Block ? SusanooTiming.BlockLength
                : play == SusanooPreview.End ? SusanooTiming.EndTimes().end
                : sword.T.end;
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            float s = seconds;
            Vector3 c = cell.ToVector3Shifted();
            var origin = new Vector2(c.x, c.z);
            var F = new SusanooFrame { Feet = new Vector2(origin.x, origin.y + SusanooGraphics.FeetZ), kx = 1f, kz = 1f };
            SusanooLook look = SusanooLook.Default;
            SusanooGrowth g = SusanooGrowth.Complete;
            pose.Reset();
            // Itachi's head at the real pawn's size, for the Sharingan glint and the coughs.
            var head = new Vector2(origin.x, origin.y + SusanooGraphics.FeetZ + .58f * 1.3f);

            switch (mode)
            {
                case SusanooPreview.Raise:
                    g = SusanooGraphics.Growth(s - SusanooTiming.Lead, SusanooTiming.WarmUp);
                    if (g.glint > 0f)
                        for (int side = -1; side <= 1; side += 2)
                            SusanooGraphics.Glint(new Vector2(head.x + side * .07f, head.y - .01f), .5f, g.glint, Color.Lerp(Sharingan, EyeHot, .35f));
                    break;
                case SusanooPreview.End:
                {
                    SusanooTiming.End T = SusanooTiming.EndTimes();
                    float tb = s - SusanooTiming.BreakAt, dim = Smooth((s - SusanooTiming.EndIdle) / SusanooTiming.DimTime);
                    if (s < SusanooTiming.BreakAt) g.dim = dim;
                    else g = SusanooGraphics.Breaking(tb, SusanooTiming.BreakUp, 1f);
                    if (tb >= 0f) SusanooGraphics.Embers(F, tb, SusanooTiming.BreakUp);
                    float hunch = Smooth((s - T.hunch) / .2f) * .8f;
                    for (int j = 0; j < 2; j++)
                    {
                        float cough = j == 0 ? T.cough0 : T.cough1;
                        if (s >= cough) hunch += .35f * Mathf.Exp(-(s - cough) * 9f);
                    }
                    var bowed = new Vector2(head.x, head.y - hunch * .17f * 1.5f);
                    SusanooBlood.Cough(origin, bowed, s - T.cough0, 0, true);
                    SusanooBlood.Cough(origin, bowed, s - T.cough1, 1, true);
                    break;
                }
                case SusanooPreview.Block:
                    BlockScript(s, origin, F);
                    break;
                default:
                    SusanooGraphics.FloorRing(origin, SusanooTiming.SealRange, FlameMid, .35f);
                    sword.Apply(s, s, F, look, pose, true);
                    Shake(sword.T.pierce, sword.weak ? .1f : .06f);
                    break;
            }
            SusanooGraphics.Susanoo(F, s, look, g, pose);
            if (seconds >= duration) active = false;
        }

        /// <summary>The Block scenario: a shooter at 9 cells fires three rounds, then a sword raider runs in from the far side and strikes.</summary>
        private void BlockScript(float t, Vector2 origin, in SusanooFrame F)
        {
            SusanooGraphics.FloorRing(origin, SusanooTiming.EdgeRadius, FlameMid, .35f);
            Vector2 guardC = F.At(SusanooGraphics.GuardC);
            float a = Dir * Mathf.Deg2Rad, b = (Dir + 180f) * Mathf.Deg2Rad;
            var shooterPos = new Vector2(origin.x + Mathf.Cos(a) * SusanooTiming.ShooterDist, origin.y + Mathf.Sin(a) * SusanooTiming.ShooterDist);
            var shooterChest = new Vector2(shooterPos.x, shooterPos.y + .06f);
            Vector2 MeleeAt(float u)
            {
                float d = Mathf.Lerp(SusanooTiming.MeleeFrom, SusanooTiming.MeleeStop, u);
                return new Vector2(origin.x + Mathf.Cos(b) * d, origin.y + Mathf.Sin(b) * d);
            }
            Vector2 meleeStop = MeleeAt(1f);
            float gA = Deg(guardC, shooterChest), gB = SusanooMirrorGuard.Cross(gA, Deg(guardC, new Vector2(meleeStop.x, meleeStop.y + .06f)));
            Vector2 MirrorFor(float time)
            {
                if (time < SusanooTiming.Aim) return SusanooGraphics.RestMirror;
                if (time < SusanooTiming.Aim + SusanooTiming.Face)
                    return Vector2.Lerp(SusanooGraphics.RestMirror, SusanooGraphics.MirrorAt(gA), Smooth((time - SusanooTiming.Aim) / SusanooTiming.Face));
                if (time < SusanooTiming.Cross0) return SusanooGraphics.MirrorAt(gA);
                if (time < SusanooTiming.Cross1)
                    return SusanooGraphics.MirrorAt(Mathf.Lerp(gA, gB, Smooth((time - SusanooTiming.Cross0) / (SusanooTiming.Cross1 - SusanooTiming.Cross0))));
                return Vector2.Lerp(SusanooGraphics.MirrorAt(gB), SusanooGraphics.RestMirror, Smooth((time - SusanooTiming.Settle0) / (SusanooTiming.Settle1 - SusanooTiming.Settle0)));
            }
            pose.mirror = MirrorFor(t);
            float flash = 0f;

            // The shooter's three rounds stop on the mirror's rim facing it.
            SusanooGraphics.MirrorWorld(F, t, SusanooGraphics.MirrorAt(gA), out Vector2 mA, out float rx, out float rz);
            float rifleAim = Deg(shooterChest, mA) * Mathf.Deg2Rad;
            var tip = new Vector2(shooterChest.x + Mathf.Cos(rifleAim) * .62f, shooterChest.y + Mathf.Sin(rifleAim) * .58f);
            for (int k = 0; k < SusanooTiming.Shots.Length; k++)
            {
                float t0 = SusanooTiming.Shots[k];
                SusanooGraphics.MirrorWorld(F, t0, SusanooGraphics.MirrorAt(gA), out Vector2 m, out rx, out rz);
                Vector2 rim = SusanooGraphics.RimToward(m, rx, rz, tip, out float u, out float v);
                float hitAt = t0 + (rim - tip).magnitude / SusanooTiming.RoundSpeed;
                if (t < hitAt) continue;
                float age = t - hitAt;
                pose.hits.Add(new SusanooMirrorHit { age = age, u = u, v = v });
                SusanooGraphics.Sparks(rim, Unit(rim, tip), age, 8, .35f, k);
                flash = Mathf.Max(flash, Mathf.Max(0f, 1f - age / .2f));
            }
            // The sword raider's blow stops on the mirror.
            var handAtBlow = new Vector2(meleeStop.x + Mathf.Cos(a) * .156f, meleeStop.y - .33f + .39f + Mathf.Sin(a) * .08f);
            SusanooGraphics.MirrorWorld(F, SusanooTiming.Blow, SusanooGraphics.MirrorAt(gB), out Vector2 mB, out rx, out rz);
            Vector2 rimB = SusanooGraphics.RimToward(mB, rx, rz, handAtBlow, out float ub, out float vb);
            if (t >= SusanooTiming.Blow)
            {
                float age = t - SusanooTiming.Blow;
                pose.hits.Add(new SusanooMirrorHit { age = age, u = ub, v = vb });
                SusanooGraphics.Sparks(rimB, Unit(rimB, handAtBlow), age, 12, .4f, 9);
                flash = Mathf.Max(flash, Mathf.Max(0f, 1f - age / .25f));
            }
            pose.mirrorFlash = flash;
            Shake(SusanooTiming.Blow, .08f);
        }

        private void Shake(float at, float size)
        {
            if (shaken == 0 && seconds >= at)
            {
                Find.CameraDriver.shaker.DoShake(size);
                shaken++;
            }
        }
    }
}
