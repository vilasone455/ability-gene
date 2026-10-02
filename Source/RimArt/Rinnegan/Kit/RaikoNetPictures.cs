using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using G = RimArt.RaikoKusariGraphics;
using T = RimArt.RaikoKusariTiming;

namespace RimArt
{
    /// <summary>
    /// Raikō Kusari in the game: hands a <see cref="RaikoNet"/> to RaikoKusariGraphics (the port of
    /// rinnegan-raiko-kusari.js) every frame. The net's clock starts at its fire tick, the sketch's leap; the charge
    /// in the hand is drawn from the caster's warmup stance before the net exists. An ended net is drawn on for its
    /// lines to break and fall, its caught pawns' crackle to fade, a mechanoid's EMP crackle and the scorches.
    /// Caught pawns are not shaken (the sketch shakes its stand-ins 0.02 cells a redraw).
    /// </summary>
    public static class RaikoNetPictures
    {
        /// <summary>A scorch stays this long after its net ends, then fades over <see cref="ScorchFade"/>.</summary>
        private const float ScorchStay = 8f, ScorchFade = 2f;

        private static readonly List<RaikoNet> ended = new List<RaikoNet>();
        private static readonly List<Vector2> heldScratch = new List<Vector2>();

        public static void Clear() => ended.Clear();

        public static void Formed(RaikoNet net)
        {
            if (net.map == Find.CurrentMap) Find.CameraDriver.shaker.DoShake(T.CameraShake);
        }

        public static void Ended(RaikoNet net)
        {
            if (!ended.Contains(net)) ended.Add(net);
        }

        /// <summary>The Chidori charging in the hand of every caster warming up Raikō Kusari on this map.</summary>
        public static void DrawCharges(Map map, GameComponent_Rinnegan rinnegan)
        {
            IReadOnlyList<Pawn> pawns = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (!(pawn.stances?.curStance is Stance_Warmup warmup) || !(warmup.verb is Verb_CastAbility cast)
                    || cast.ability?.def != SasukeDefOf.AG_SasukeRaikoKusari) continue;
                float age = PictureClock.Since(warmup.startedTick);
                Vector2 caster = At(pawn.DrawPos);
                G.Charge(T.Hand(caster, Aim(pawn, rinnegan)), caster, age, T.Charge, age);
            }
        }

        /// <summary>Degrees from the caster to his first held weapon, the way the charged hand points.</summary>
        private static float Aim(Pawn caster, GameComponent_Rinnegan rinnegan)
        {
            List<HeldWeapon> held = rinnegan.HeldBy(caster);
            if (held.Count == 0) return 0f;
            Vector3 d = held[0].at - caster.DrawPos;
            return Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg;
        }

        public static void Draw(Map map, GameComponent_Rinnegan rinnegan)
        {
            IReadOnlyList<RaikoNet> nets = rinnegan.Nets;
            for (int i = 0; i < nets.Count; i++)
                if (nets[i].map == map) DrawNet(nets[i]);
            for (int i = ended.Count - 1; i >= 0; i--)
            {
                RaikoNet net = ended[i];
                if (net.endedTick < 0 || Find.Maps.IndexOf(net.map) < 0 || PictureClock.Since(net.endedTick) > Linger(net))
                {
                    ended.RemoveAt(i);
                    continue;
                }
                if (net.map == map) DrawNet(net);
            }
        }

        private static float Linger(RaikoNet net) =>
            Mathf.Max(ScorchStay + ScorchFade, SasukeKit.NetProps.mechStunTicks / 60f, T.Afterglow);

        private static Vector2 At(Vector3 v) => new Vector2(v.x, v.z);
        private static Vector2 Ground(Vector3 heldAt) => new Vector2(heldAt.x, heldAt.z - AmenoyodomiGraphics.HeldLift);

        /// <summary>A corner's ground point now, whether it is the Fūma, its turn and how fast it drifts (cells/s).</summary>
        private static bool Corner(RaikoNet net, int i, out Vector2 ground, out bool fuma, out float turn, out Vector2 drift)
        {
            HeldWeapon w = net.Corner(i);
            ground = Vector2.zero;
            fuma = false;
            turn = 0f;
            drift = Vector2.zero;
            if (w == null) return false;
            ground = RinneganPictures.Ground(w);
            fuma = w.IsFuma;
            turn = w.turned;
            Ability_Amenoyodomi ability = SasukeKit.Amenoyodomi(w.caster);
            float share = ability != null ? SasukeKit.HoldProps.Share(ability.mode) : SasukeKit.HoldProps.hangShare;
            drift = new Vector2(w.heading.x, w.heading.z) * (w.speed * 60f * share);
            return true;
        }

        private static void DrawNet(RaikoNet net)
        {
            int count = net.corners.Count, links = net.LinkCount;
            if (count < 2 || links <= 0) return;
            float age = PictureClock.Since(net.startTick), clock = age + T.Charge;
            float netEnd = net.endedTick >= 0 ? (net.endedTick - net.startTick) / 60f : float.PositiveInfinity;

            var grounds = new Vector2[count];
            var fumas = new bool[count];
            var turns = new float[count];
            var drifts = new Vector2[count];
            var have = new bool[count];
            for (int i = 0; i < count; i++) have[i] = Corner(net, i, out grounds[i], out fumas[i], out turns[i], out drifts[i]);

            var surge = new float[count];
            var live = new bool[count];
            for (int k = 0; k < links; k++)
            {
                int i = net.LinkFrom(k), j = net.LinkTo(k);
                bool ended = k < net.linkEnds.Count && net.linkEnds[k] >= 0;
                float end = ended ? (net.linkEnds[k] - net.startTick) / 60f : float.PositiveInfinity;
                if (age > end + T.Afterglow) continue;
                Vector2 endA = ended ? Ground(net.endA[k]) : grounds[i], endB = ended ? Ground(net.endB[k]) : grounds[j];
                Vector2 a = have[i] ? grounds[i] : endA, b = have[j] ? grounds[j] : endB;
                if (have[i] && fumas[i]) a = T.Joint(grounds[i], turns[i], b);
                if (have[j] && fumas[j]) b = T.Joint(grounds[j], turns[j], a);

                heldScratch.Clear();
                if (!ended)
                    for (int p = 0; p < net.caught.Count && p < net.caughtLinks.Count; p++)
                        if (net.caughtLinks[p] == k && net.caught[p] != null && net.caught[p].Spawned) heldScratch.Add(At(net.caught[p].DrawPos));

                var link = new RaikoLink
                {
                    index = k,
                    links = links,
                    a = a,
                    b = b,
                    endA = endA,
                    endB = endB,
                    driftA = drifts[i],
                    driftB = drifts[j],
                    failed = k < net.failed.Count && net.failed[k],
                    end = end,
                    cut = k < net.cut.Count && net.cut[k],
                    held = heldScratch.Count > 0 ? new List<Vector2>(heldScratch) : null
                };
                G.Link(link, age, clock);

                if (!ended && !link.failed)
                {
                    float s = T.Surge(k, links, age, clock, end);
                    surge[i] = Mathf.Max(surge[i], s);
                    surge[j] = Mathf.Max(surge[j], s);
                    live[i] = live[j] = true;
                    if (have[i]) G.CornerSpark(fumas[i] ? a : grounds[i], i, s, clock);
                    if (have[j]) G.CornerSpark(fumas[j] ? b : grounds[j], j, s, clock);
                }
            }

            // Each weapon flashes as the run reaches it; a ring's closing weapon flashes larger.
            if (have[0]) G.Reached(grounds[0], age - T.Leap, false, 0);
            for (int k = 0, n = 1; k < links; k++)
            {
                if (k < net.failed.Count && net.failed[k]) continue;
                int j = net.LinkTo(k);
                if (have[j]) G.Reached(grounds[j], age - T.LinkLit(k), net.ring && k == links - 1, n);
                n++;
            }
            for (int i = 0; i < count; i++)
            {
                if (!have[i] || !live[i]) continue;
                G.CornerPool(grounds[i], i, surge[i], clock);
                if (fumas[i]) G.FumaArcs(grounds[i], turns[i], surge[i], clock);
            }

            // The leap from the hand, and the charge fading after it.
            Pawn caster = net.caster;
            if (caster != null && caster.Spawned && caster.Map == net.map && have[0] && age < T.Leap + T.LeapLinger + T.HandFade)
            {
                Vector2 casterAt = At(caster.DrawPos);
                Vector3 d = new Vector3(grounds[0].x, 0f, grounds[0].y) - caster.DrawPos;
                Vector2 hand = T.Hand(casterAt, Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg);
                G.Leap(hand, grounds[0], age, clock);
                G.Charge(hand, casterAt, T.Charge + age, T.Charge, clock);
            }

            // Caught pawns: the crackle while held and fading after, the scorch under them.
            float sinceEnd = net.endedTick >= 0 ? PictureClock.Since(net.endedTick) : -1f;
            float scorchAlpha = sinceEnd < ScorchStay ? 1f : 1f - Mathf.Clamp01((sinceEnd - ScorchStay) / ScorchFade);
            int mechTicks = SasukeKit.NetProps.mechStunTicks;
            for (int p = 0; p < net.caught.Count && p < net.caughtTicks.Count; p++)
            {
                Pawn pawn = net.caught[p];
                float caughtAt = (net.caughtTicks[p] - net.startTick) / 60f;
                if (p < net.caughtAt.Count) G.Scorch(At(net.caughtAt[p]), Mathf.Min(age, netEnd) - caughtAt, scorchAlpha);
                if (pawn == null || !pawn.Spawned || pawn.Map != net.map) continue;
                Vector2 at = At(pawn.DrawPos);
                if (sinceEnd < T.FadeOut) G.Caught(at, PictureClock.Since(net.caughtTicks[p]), sinceEnd, pawn.thingIDNumber % 97, clock);
                if (sinceEnd >= 0f && pawn.RaceProps.IsMechanoid && sinceEnd < mechTicks / 60f) G.Emp(at, sinceEnd, clock);
            }
        }

        // ---- Charged weapons after a let-go ----------------------------------------------------------------------

        /// <summary>Charged weapons in flight: the jagged tail, and the Fūma's arcs.</summary>
        public static void DrawFlights(Map map, GameComponent_Rinnegan rinnegan)
        {
            IReadOnlyList<FlyingOn> flying = rinnegan.Flying;
            float clock = PictureClock.Since(0);
            for (int i = 0; i < flying.Count; i++)
            {
                FlyingOn f = flying[i];
                if (!f.charged || f.shot == null || f.shot.Destroyed || f.shot.Map != map || rinnegan.HeldFor(f.shot) != null) continue;
                Vector3 pos = f.shot.ExactPosition;
                Vector2 ground = Ground(pos);
                Vector3 heading = Rounds.Vanilla.Heading(f.shot);
                float deg = Mathf.Atan2(heading.z, heading.x) * Mathf.Rad2Deg;
                if (f.shot is Projectile_Fuma) G.FlyingFuma(ground, deg, clock * AmenoyodomiGraphics.FumaSpin % 360f, clock);
                else G.FlyingKunai(ground, deg, f.shot.thingIDNumber % 7, clock);
            }
        }
    }
}
