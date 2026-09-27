using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using static RimArt.SusanooDraw;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// The Susanoo picture in game, one per Host: it grows over the cast's warm-up (Begin, from the
    /// cast job), stands while the hediff lasts (Raised, from the comp), turns its mirror to every
    /// blocked hit (Blocked, from the Yata Mirror prefix), swings the Totsuka at each stab (Stab,
    /// from the command; the mechanic resolves at the pierce, in the comp), and breaks apart when
    /// the hediff goes (End). Drawn on the game clock, so it pauses with the game. The blood of a
    /// hit and of Itachi's coughs is real filth here. Picture only: nothing is saved.
    /// </summary>
    public sealed class MapComponent_Susanoo : MapComponent
    {
        private sealed class Show
        {
            public Pawn pawn;
            public int beginTick;
            public float warmUp;
            public bool up, cancelled;
            public int endTick = -1;
            public Vector2 feet;
            public readonly SusanooPose pose = new SusanooPose();
            public readonly SusanooMirrorGuard guard = new SusanooMirrorGuard();
            public readonly List<Hit> hits = new List<Hit>();
            public SusanooSword sword;
            public bool spurted;
            public readonly bool[] coughed = new bool[2];
        }

        private struct Hit
        {
            public float time, u, v;
            public Vector2 rim, back;
            public int seed;
        }

        private readonly List<Show> shows = new List<Show>();
        private int hitSeed;

        public MapComponent_Susanoo(Map map) : base(map) { }

        public static MapComponent_Susanoo For(Pawn pawn) => pawn?.MapHeld?.GetComponent<MapComponent_Susanoo>();

        private static float Now => Find.TickManager.TicksGame / 60f;

        private Show Of(Pawn pawn)
        {
            for (int i = 0; i < shows.Count; i++) if (shows[i].pawn == pawn) return shows[i];
            return null;
        }

        private Show Fresh(Pawn pawn)
        {
            Show old = Of(pawn);
            if (old != null) shows.Remove(old);
            var show = new Show { pawn = pawn, beginTick = Find.TickManager.TicksGame, warmUp = SusanooTiming.WarmUp, feet = Feet(pawn) };
            shows.Add(show);
            return show;
        }

        private static Vector2 Feet(Pawn pawn)
        {
            Vector3 p = pawn.DrawPos;
            return new Vector2(p.x, p.z);
        }

        /// <summary>The cast's warm-up has begun: the figure grows over it.</summary>
        public void Begin(Pawn pawn, float warmUp)
        {
            Show show = Fresh(pawn);
            show.warmUp = Mathf.Max(.2f, warmUp);
        }

        /// <summary>The hediff is on: the figure is complete (at once, when no cast preceded it).</summary>
        public void Raised(Pawn pawn)
        {
            Show show = Of(pawn);
            if (show == null || show.endTick >= 0)
            {
                show = Fresh(pawn);
                show.beginTick -= Mathf.RoundToInt(show.warmUp * 60f);
            }
            show.up = true;
        }

        /// <summary>The Mirror took a hit: it turns to the attacker, the hit ripples across its face.</summary>
        public void Blocked(Pawn pawn, DamageInfo dinfo)
        {
            Show show = Of(pawn);
            if (show == null || !show.up || show.endTick >= 0) return;
            Vector2 origin = Feet(pawn);
            Vector2 attacker;
            if (dinfo.Instigator != null && dinfo.Instigator.Spawned && dinfo.Instigator.Map == map)
            {
                Vector3 p = dinfo.Instigator.DrawPos;
                attacker = new Vector2(p.x, p.z + .06f);
            }
            else if (dinfo.Angle >= 0f)
            {
                Vector3 travel = Quaternion.AngleAxis(dinfo.Angle, Vector3.up) * Vector3.forward;
                attacker = new Vector2(origin.x - travel.x * 3f, origin.y - travel.z * 3f);
            }
            else attacker = new Vector2(origin.x, origin.y - 3f);
            float t = Now;
            var F = new SusanooFrame { Feet = new Vector2(origin.x, origin.y + SusanooGraphics.FeetZ), kx = 1f, kz = 1f };
            show.guard.Aim(Deg(F.At(SusanooGraphics.GuardC), attacker), t);
            Vector2 mirror = show.guard.At(t, out _);
            SusanooGraphics.MirrorWorld(F, t, mirror, out Vector2 centre, out float rx, out float rz);
            Vector2 rim = SusanooGraphics.RimToward(centre, rx, rz, attacker, out float u, out float v);
            show.hits.Add(new Hit { time = t, u = u, v = v, rim = rim, back = Unit(rim, attacker), seed = hitSeed++ % 64 });
        }

        /// <summary>The Totsuka is swung at a target where it stands now; seal says which ending plays.</summary>
        public void Stab(Pawn pawn, Pawn target, bool seal)
        {
            Show show = Of(pawn);
            if (show == null || target == null) return;
            show.sword = new SusanooSword(Now, Feet(pawn), Feet(target), seal);
            show.spurted = false;
        }

        /// <summary>The hediff is gone: it dims, breaks apart top first and Itachi coughs.</summary>
        public void End(Pawn pawn)
        {
            Show show = Of(pawn);
            if (show == null || show.endTick >= 0) return;
            show.endTick = Find.TickManager.TicksGame;
        }

        public override void MapComponentUpdate()
        {
            if (shows.Count == 0 || Find.CurrentMap != map) return;
            int now = Find.TickManager.TicksGame;
            float t = Now;
            for (int i = shows.Count - 1; i >= 0; i--)
            {
                Show show = shows[i];
                Pawn pawn = show.pawn;
                bool present = pawn != null && pawn.Spawned && pawn.Map == map;
                if (present) show.feet = Feet(pawn);
                else if (show.endTick < 0) show.endTick = now;
                Vector2 origin = show.feet;
                float since = (now - show.beginTick) / 60f;

                SusanooGrowth g;
                if (show.up) g = SusanooGrowth.Complete;
                else
                {
                    g = SusanooGraphics.Growth(since, show.warmUp);
                    // A cast that never fired: the half-formed figure fades.
                    if (since > show.warmUp + .3f && show.endTick < 0) { show.cancelled = true; show.endTick = now; }
                }
                var F = new SusanooFrame { Feet = new Vector2(origin.x, origin.y + SusanooGraphics.FeetZ), kx = 1f, kz = 1f };
                SusanooLook look = SusanooLook.Default;
                var head = new Vector2(origin.x, origin.y + SusanooGraphics.FeetZ + .58f * 1.3f);

                if (show.endTick >= 0)
                {
                    float te = (now - show.endTick) / 60f;
                    if (show.cancelled || !show.up)
                    {
                        g.alpha = 1f - Smooth(te / .5f);
                        if (te >= .55f) { shows.RemoveAt(i); continue; }
                    }
                    else
                    {
                        SusanooTiming.End T = SusanooTiming.EndTimes();
                        if (te >= T.end) { shows.RemoveAt(i); continue; }
                        float tb = te - SusanooTiming.BreakAt;
                        if (te < SusanooTiming.BreakAt) g.dim = Smooth((te - SusanooTiming.EndIdle) / SusanooTiming.DimTime);
                        else g = SusanooGraphics.Breaking(tb, SusanooTiming.BreakUp, 1f);
                        if (tb >= 0f && VfxDraw.Shown(origin, map)) SusanooGraphics.Embers(F, tb, SusanooTiming.BreakUp);
                        Coughs(show, T, te, origin, head, present);
                    }
                }
                if (!VfxDraw.Shown(origin, map)) continue;

                SusanooPose pose = show.pose;
                pose.Reset();
                pose.mirror = show.guard.At(t, out _);
                float flash = 0f;
                for (int h = show.hits.Count - 1; h >= 0; h--)
                {
                    Hit hit = show.hits[h];
                    float age = t - hit.time;
                    if (age > .8f) { show.hits.RemoveAt(h); continue; }
                    pose.hits.Add(new SusanooMirrorHit { age = age, u = hit.u, v = hit.v });
                    SusanooGraphics.Sparks(hit.rim, hit.back, age, 8, .35f, hit.seed);
                    flash = Mathf.Max(flash, Mathf.Max(0f, 1f - age / .2f));
                }
                pose.mirrorFlash = flash;
                if (show.sword != null)
                {
                    if (show.sword.Done(t)) show.sword = null;
                    else
                    {
                        show.sword.Apply(t, t, F, look, pose, false);
                        if (!show.sword.weak && !show.spurted && t - show.sword.start >= show.sword.T.pierce + .3f)
                        {
                            show.spurted = true;
                            for (int d = 0; d < 3; d++)
                                BloodAt(SusanooBlood.SpurtLanding(show.sword.pos, show.sword.outDir, d * 3, 40));
                        }
                    }
                }
                SusanooGraphics.Susanoo(F, t, look, g, pose);
            }
        }

        private void Coughs(Show show, in SusanooTiming.End T, float te, Vector2 origin, Vector2 head, bool present)
        {
            float hunch = Smooth((te - T.hunch) / .2f) * .8f;
            for (int j = 0; j < 2; j++)
            {
                float cough = j == 0 ? T.cough0 : T.cough1;
                if (te < cough) continue;
                hunch += .35f * Mathf.Exp(-(te - cough) * 9f);
                if (!show.coughed[j])
                {
                    show.coughed[j] = true;
                    if (present)
                        for (int d = 0; d < 2; d++)
                            BloodAt(new Vector2(origin.x + (Rand(j * 100 + d * 7 + 901) - .5f) * .5f, origin.y - .38f - Rand(j * 100 + d * 7 + 902) * .45f));
                }
            }
            var bowed = new Vector2(head.x, head.y - hunch * .17f * 1.5f);
            if (!VfxDraw.Shown(origin, map)) return;
            SusanooBlood.Cough(origin, bowed, te - T.cough0, 0, false);
            SusanooBlood.Cough(origin, bowed, te - T.cough1, 1, false);
        }

        private void BloodAt(Vector2 at)
        {
            IntVec3 cell = new Vector3(at.x, 0f, at.y).ToIntVec3();
            if (cell.InBounds(map)) FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_Blood);
        }
    }
}
