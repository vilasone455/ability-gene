using System.Collections.Generic;
using UnityEngine;
using static RimArt.VfxMath;
using T = RimArt.EgoSolemnLamentTiming;

namespace RimArt
{
    /// <summary>One lace butterfly's trip to a marked pawn: from where it starts to its resting place on the body.</summary>
    public struct EgoSolemnLamentFlight
    {
        /// <summary>When its stack counts (the pips fill), when it leaves, and how long it flies.</summary>
        public float Count, Launch, Fly;
        /// <summary>Its resting place on the body (<see cref="EgoSolemnLamentTiming.SlotLocal"/>).</summary>
        public int Slot;
        /// <summary>A dark one (the black shot's, The Departed) or a pale one (the white shot's, The Living).</summary>
        public bool Dark;
        /// <summary>Where it starts: a ground point and a height in cells.</summary>
        public Vector2 FromGround;
        public float FromHeight;
        /// <summary>Its path is bowed out by Via (a ground offset at mid-flight) and up by Arc cells.</summary>
        public Vector2 Via;
        public float Arc;
        /// <summary>The flutter's phase.</summary>
        public int Seed;
        /// <summary>The cap's cover over the downed pawn, not a stack: it fades in where it appears and fills no pip.</summary>
        public bool Swarm;
    }

    /// <summary>
    /// A pawn taking Butterfly stacks, as the picture needs it: the butterflies flying to it and resting on it
    /// (one per stack, in the order the stacks fall), the cap, and when it went down; for the funeral, when each
    /// pale butterfly of the cover turns dark, when it died and the coffin's mouth the cover flies into. In the previews the lab
    /// sketch's script fills it once from the planned shots or the coffin's ticks; in game the weapon adds a
    /// flight per stack it puts on.
    ///
    /// <see cref="Now"/>, <see cref="Fall"/> and the flinches are the preview's script for the sketch's
    /// stand-in (it sways with its stacks, flinches 0.07 cells from a black hit, falls backward at the cap). In
    /// game the pawn's own DrawPos and downed state take their place.
    /// </summary>
    public sealed class EgoSolemnLamentMark
    {
        /// <summary>The pawn's cell centre while it stands (DrawPos): the swarm gathers round it and the butterflies land toward its ground contact.</summary>
        public Vector2 Home;
        public int Cap;
        /// <summary>When the cap was reached (the swarm comes in), or negative while it was not.</summary>
        public float DownAt = -1f;
        /// <summary>The funeral: when the death count was reached (the cover lifts off into the coffin), or negative.</summary>
        public float DeadAt = -1f;
        /// <summary>The funeral: when the pale butterfly resting on each slot turns dark, or negative (<see cref="Darken"/>).</summary>
        public readonly float[] TurnAt = new float[T.TorsoSlots + T.HeadSlots];
        /// <summary>The funeral: the coffin's mouth now, where the cover flies when the pawn dies (<see cref="EgoSolemnLamentTiming.Mouth"/>).</summary>
        public EgoSolemnLamentPoint Mouth;
        /// <summary>Degrees the body turns clockwise as it falls backward: 90 when the shots came from the west, -90 from the east.</summary>
        public float FallTurn;
        /// <summary>The script's sway phase.</summary>
        public float Seed;
        public readonly List<EgoSolemnLamentFlight> Flights = new List<EgoSolemnLamentFlight>();
        /// <summary>The cover's pale slots in the order they turn dark, taken at the first stack past the cap.</summary>
        private List<int> pale;
        private readonly List<float> flinchAt = new List<float>();
        private readonly List<Vector2> flinchWay = new List<Vector2>();

        public EgoSolemnLamentMark(Vector2 home, int cap, float fallTurn, float seed)
        {
            Home = home;
            Cap = cap;
            FallTurn = fallTurn;
            Seed = seed;
            for (int i = 0; i < TurnAt.Length; i++) TurnAt[i] = -1f;
        }

        public bool Down => DownAt >= 0f;
        public bool Dead => DeadAt >= 0f;

        /// <summary>
        /// The funeral, stack <paramref name="m"/> past the cap of the <paramref name="left"/> to the death count,
        /// counted at <paramref name="at"/>: the m-th share of the cover's pale butterflies turns dark at that
        /// moment. The order is fixed and shuffled (by Rand(slot + 5000)), so the cover darkens evenly. Returns
        /// the slot the stack's own dark butterfly lands on: the first of that share.
        /// </summary>
        public int Darken(int m, int left, float at)
        {
            if (pale == null)
            {
                pale = new List<int>();
                foreach (EgoSolemnLamentFlight f in Flights)
                    if (!f.Dark && !pale.Contains(f.Slot)) pale.Add(f.Slot);
                pale.Sort((a, b) => Rand(a + 5000).CompareTo(Rand(b + 5000)));
            }
            int n = pale.Count, r0 = (m - 1) * n / left, r1 = m * n / left;
            for (int r = r0; r < r1; r++) TurnAt[pale[r]] = at;
            return n > 0 ? pale[Mathf.Min(n - 1, r0)] : (Cap + m) % TurnAt.Length;
        }

        /// <summary>Stacks counted by <paramref name="s"/>: flights that are not the swarm and whose stack has fallen.</summary>
        public int Counted(float s)
        {
            int n = 0;
            foreach (EgoSolemnLamentFlight f in Flights)
                if (!f.Swarm && f.Count <= s) n++;
            return n;
        }

        /// <summary>The script: a black hit at <paramref name="at"/> pushes the stand-in 0.07 cells along <paramref name="way"/> and back over 0.25 s.</summary>
        public void Flinch(float at, Vector2 way)
        {
            flinchAt.Add(at);
            flinchWay.Add(way);
        }

        /// <summary>The script: how far the stand-in has fallen, 0 to 1. It goes over halfway through the swarm's flight, in <see cref="EgoSolemnLamentTiming.FallTime"/> s.</summary>
        public float Fall(float s) => Down ? Smooth((s - DownAt - T.SwarmIn * 0.5f) / T.FallTime) : 0f;

        /// <summary>The script: where the stand-in is drawn at <paramref name="s"/>: it sways up to 0.045 cells with its share of the cap (consciousness) until it falls, and flinches from black hits.</summary>
        public Vector2 Now(float s)
        {
            float fall = Fall(s), sway = (1f - fall) * 0.045f * Counted(s) / Cap * Mathf.Sin(s * 6.2831855f * 0.8f + Seed);
            Vector2 at = Home + new Vector2(sway, 0f);
            for (int i = 0; i < flinchAt.Count; i++)
            {
                float a = s - flinchAt[i];
                if (a >= 0f && a < 0.25f) at += flinchWay[i] * (0.07f * T.Bump(a / 0.25f));
            }
            return at;
        }

        /// <summary>
        /// The cap reached at <paramref name="at"/>: one swarm butterfly for every slot past the cap (28 at cap 10),
        /// each from 1.1 to 1.8 cells round the pawn, swinging round it on the way in, out within 0.15 s.
        /// </summary>
        public void Swarm(float at)
        {
            DownAt = at;
            for (int k = Cap; k < T.TorsoSlots + T.HeadSlots; k++)
            {
                float a = Rand(k + 700) * 6.2831855f, r = 1.1f + 0.7f * Rand(k + 710);
                Flights.Add(new EgoSolemnLamentFlight
                {
                    Count = at, Launch = at + Rand(k + 720) * 0.15f, Fly = T.SwarmIn, Slot = k, Dark = k % 2 == 1, Seed = k, Swarm = true,
                    FromGround = new Vector2(Home.x + Mathf.Cos(a) * r, Home.y + PawnBody.Ground + Mathf.Sin(a) * r), FromHeight = 0.4f + Rand(k + 730),
                    Via = new Vector2(-Mathf.Sin(a) * 0.6f, Mathf.Cos(a) * 0.6f), Arc = 0.15f,
                });
            }
        }
    }
}
