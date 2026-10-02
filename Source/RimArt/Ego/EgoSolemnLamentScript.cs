using System.Collections.Generic;
using UnityEngine;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>One planned shot of the burst: its number, white or black, when it fires and hits, and the stacks it puts on.</summary>
    public struct EgoSolemnLamentShot
    {
        public int K;
        public bool White;
        public float Fire, Hit;
        /// <summary>Stacks before this shot, and stacks it adds (fewer at the cap).</summary>
        public int From, Add;
    }

    /// <summary>
    /// The preview's script, the lab sketch's: the burst's planned shots and the target's flights, and the
    /// coffin's dives. Seconds and positions only; the picture classes draw from what it fills in.
    /// </summary>
    public static class EgoSolemnLamentScript
    {
        /// <summary>
        /// The burst: up to <paramref name="shots"/> shots from <paramref name="lead"/> s, <paramref name="interval"/>
        /// s apart, white first. A shot hits (dist - 0.4) / Speed s after it fires. It stops at the shot that
        /// reaches the cap. Returns how many shots there are; <paramref name="downAt"/> is that last hit, or -1.
        /// </summary>
        public static int Plan(EgoSolemnLamentShot[] into, int shots, float lead, float interval, float dist, int whiteStacks, int blackStacks, int cap,
            out float downAt)
        {
            int n = 0, stacks = 0;
            downAt = -1f;
            for (int k = 0; k < shots && k < into.Length; k++)
            {
                bool white = k % 2 == 0;
                float t = lead + k * interval, hit = t + (dist - 0.4f) / EgoSolemnLamentTiming.Speed;
                int add = Mathf.Min(white ? whiteStacks : blackStacks, cap - stacks);
                into[n++] = new EgoSolemnLamentShot { K = k, White = white, Fire = t, Hit = hit, From = stacks, Add = add };
                stacks += add;
                if (stacks >= cap)
                {
                    downAt = hit;
                    break;
                }
            }
            return n;
        }

        /// <summary>
        /// The target's flights for the planned shots: each stack's butterfly leaves the chest 0.07 s after the
        /// last, loops out along the shot (0.3 to 0.7 cells) and across it (up to 0.6 either side) and lands in
        /// <see cref="EgoSolemnLamentTiming.FlyTime"/> s. Black hits make the stand-in flinch along the aim.
        /// </summary>
        public static void Fill(EgoSolemnLamentMark mark, EgoSolemnLamentShot[] shots, int count, Vector2 aim, float downAt)
        {
            var across = new Vector2(-aim.y, aim.x);
            for (int s = 0; s < count; s++)
            {
                EgoSolemnLamentShot sh = shots[s];
                for (int j = 0; j < sh.Add; j++)
                {
                    int n = sh.K * 3 + j;
                    float out1 = 0.3f + 0.4f * Rand(n + 600), side = (Rand(n + 610) - 0.5f) * 1.2f;
                    mark.Flights.Add(new EgoSolemnLamentFlight
                    {
                        Count = sh.Hit, Launch = sh.Hit + j * 0.07f, Fly = EgoSolemnLamentTiming.FlyTime, Slot = sh.From + j, Dark = !sh.White, Seed = n,
                        FromGround = new Vector2(mark.Home.x, mark.Home.y + PawnBody.Ground), FromHeight = EgoSolemnLamentTiming.ChestH,
                        Via = aim * out1 + across * side, Arc = 0.45f + 0.3f * Rand(n + 620),
                    });
                }
                if (!sh.White) mark.Flinch(sh.Hit, aim);
            }
            if (downAt >= 0f) mark.Swarm(downAt);
        }

        /// <summary>
        /// The coffin's ticks: every <paramref name="tick"/> s from the opening, one cloud butterfly leaves for each of
        /// the first <paramref name="count"/> <paramref name="people"/> whose Home is within <paramref name="radius"/>
        /// of the wielder at that moment (<paramref name="wielderAt"/>), in that order, except those marked in
        /// <paramref name="skip"/> (Overclock's ally), and lands on it in <see cref="EgoSolemnLamentTiming.DiveTime"/>
        /// s. The slot is the first from (n x 11 + 3) mod 36 on whose butterfly is circling (out of the coffin, and
        /// its last refill landed DiveRest s ago), stepping by 5. A pawn at the cap goes down and takes no more. Each
        /// dive is written as (slot, time, refill flight): the refill flies from the coffin's foot at
        /// <paramref name="foot"/> to the cloud in <see cref="EgoSolemnLamentTiming.FarFly"/> of the wielder's
        /// distance from it at the dive.
        /// </summary>
        public static void Dives(EgoSolemnLamentMark[] people, bool[] skip, int count, float cloud, float tick, System.Func<float, Vector2> wielderAt,
            Vector2 foot, float radius, List<int> diveSlots, List<float> diveTimes, List<float> diveFlys)
        {
            diveSlots.Clear();
            diveTimes.Clear();
            diveFlys.Clear();
            int n = 0, ticks = EgoSolemnLamentTiming.Ticks(cloud, tick);
            for (int k = 1; k <= ticks; k++)
            {
                float at = EgoSolemnLamentTiming.TickAt(k, tick);
                Vector2 centre = wielderAt(at);
                float refill = EgoSolemnLamentTiming.FarFly((centre - foot).magnitude, EgoSolemnLamentTiming.RefillFly);
                for (int p = 0; p < count; p++)
                {
                    EgoSolemnLamentMark mark = people[p];
                    if (mark.Down || skip[p] || (mark.Home - centre).magnitude > radius) continue;
                    int i = (n++ * 11 + 3) % EgoSolemnLamentTiming.CloudN;
                    for (int c = 0; c < EgoSolemnLamentTiming.CloudN && !Ready(i, at, diveSlots, diveTimes, diveFlys); c++) i = (i + 5) % EgoSolemnLamentTiming.CloudN;
                    EgoSolemnLamentPoint from = EgoSolemnLamentTiming.Orbit(i, at, centre, radius);
                    diveSlots.Add(i);
                    diveTimes.Add(at);
                    diveFlys.Add(refill);
                    int slot = mark.Flights.Count;
                    mark.Flights.Add(new EgoSolemnLamentFlight
                    {
                        Count = at + EgoSolemnLamentTiming.DiveTime, Launch = at, Fly = EgoSolemnLamentTiming.DiveTime, Slot = slot, Dark = i % 2 == 1, Seed = i,
                        FromGround = from.Ground, FromHeight = from.Height, Via = Vector2.zero, Arc = 0.2f,
                    });
                    if (slot + 1 >= mark.Cap) mark.Swarm(at + EgoSolemnLamentTiming.DiveTime);
                }
            }
        }

        private static bool Ready(int i, float at, List<int> slots, List<float> times, List<float> flys)
        {
            if (EgoSolemnLamentTiming.CloudOut(i) + EgoSolemnLamentTiming.CloudFly(i) > at) return false;
            for (int d = 0; d < slots.Count; d++)
                if (slots[d] == i && at - times[d] < EgoSolemnLamentTiming.Refill + flys[d] + EgoSolemnLamentTiming.DiveRest) return false;
            return true;
        }

        /// <summary>
        /// The preview's corroded wielder at <paramref name="s"/>: from <see cref="EgoSolemnLamentTiming.WalkFrom"/> it
        /// walks <paramref name="walk"/> cells from <paramref name="o"/> along <paramref name="d"/>, eased at both ends
        /// over 1.5 x walk / WalkSpeed s (so its speed peaks at WalkSpeed), and stays where it got to 0.1 s after the
        /// cloud ends. In game the wielder's DrawPos takes its place.
        /// </summary>
        public static Vector2 WielderAt(Vector2 o, Vector2 d, float walk, float cloud, float s)
        {
            if (walk <= 0f) return o;
            float t = Mathf.Min(s, EgoSolemnLamentTiming.CloudEnd(cloud) + 0.1f);
            return o + d * (walk * Smooth((t - EgoSolemnLamentTiming.WalkFrom) / (1.5f * walk / EgoSolemnLamentTiming.WalkSpeed)));
        }

        /// <summary>The script: the wielder rocks back 0.025 cells over 0.2 s with each shot.</summary>
        public static float RockBack(EgoSolemnLamentShot[] shots, int count, float s)
        {
            float back = 0f;
            for (int i = 0; i < count; i++) back += 0.025f * EgoSolemnLamentTiming.Bump((s - shots[i].Fire) / 0.2f);
            return back;
        }
    }
}
