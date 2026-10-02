using System.Collections.Generic;
using UnityEngine;
using Verse;
using T = RimArt.EgoSolemnLamentTiming;

namespace RimArt
{
    /// <summary>
    /// One coffin, the corroded (or Overclock) action's picture, from the coffin rising to it sinking. Its clock is 0 when
    /// the corrosion starts. The coffin stays where the wielder stood; the cloud and the floor ring follow the wielder
    /// while it lasts, and from where it ended fly back in. Each stack the cloud puts on is a dive from a circling
    /// butterfly (<see cref="Dive"/>), written as it happens. Not saved: after a load the next firing starts a new one.
    /// </summary>
    public sealed class EgoSolemnLamentCoffinCast
    {
        /// <summary>The cloud's seconds while the corrosion still runs: longer than any state, so it never closes early.</summary>
        private const float Open = 100000f;

        public Pawn wielder;
        public Map map;
        public bool overclock;
        public float radius;
        public int startTick;
        /// <summary>The tick the corrosion or Overclock ended, -1 while it runs.</summary>
        public int endTick = -1;
        /// <summary>Where the wielder stood when the coffin rose, and where it was when the cloud ended.</summary>
        public Vector2 coffin, endAt;
        private float endAim;
        private int dives;
        private readonly List<int> diveSlots = new List<int>();
        private readonly List<float> diveTimes = new List<float>(), diveFlys = new List<float>();

        public EgoSolemnLamentCoffinCast(Pawn wielder, float radius, bool overclock)
        {
            this.wielder = wielder;
            this.radius = radius;
            this.overclock = overclock;
            map = wielder.Map;
            startTick = Find.TickManager.TicksGame;
            coffin = endAt = EgoSolemnLamentMarked.Ground(wielder.DrawPos);
        }

        public bool Running => endTick < 0;
        public float Seconds(int tick) => (tick - startTick) / 60f;
        private Vector2 Foot => new Vector2(coffin.x + T.CoffinBackX, coffin.y + T.CoffinBackZ);
        private float Cloud => Running ? Open : Mathf.Max(0f, Seconds(endTick) - T.Open);
        private float Home => Running ? T.ReturnTime : T.FarFly((endAt - Foot).magnitude, T.ReturnTime);
        private Vector2 Centre => Running && wielder.Spawned && wielder.Map == map ? EgoSolemnLamentMarked.Ground(wielder.DrawPos) : endAt;
        private float Aim => Running ? 90f - wielder.Rotation.AsAngle : endAim;

        /// <summary>The corrosion ended now: the cloud flies back in from the wielder's point and the coffin closes and sinks.</summary>
        public void End()
        {
            if (!Running) return;
            endAt = Centre;
            endAim = Aim;
            endTick = Find.TickManager.TicksGame;
        }

        /// <summary>One game tick; false once the coffin has sunk.</summary>
        public bool Tick(int now) => Running || Seconds(now) < T.CoffinEnd(Cloud, Home, 0f);

        /// <summary>
        /// A stack the cloud puts on at <paramref name="tick"/>: a circling butterfly leaves for the pawn, picked as the
        /// sketch's script does (the first from (n x 11 + 3) mod 36 that is circling, stepping by 5), and the coffin
        /// sends a new one after it. Returns where the diving one starts; <paramref name="slot"/> is its index.
        /// </summary>
        public EgoSolemnLamentPoint Dive(int tick, out int slot)
        {
            float at = Seconds(tick);
            Vector2 centre = Centre;
            int i = (dives++ * 11 + 3) % T.CloudN;
            for (int c = 0; c < T.CloudN && !EgoSolemnLamentScript.Ready(i, at, diveSlots, diveTimes, diveFlys); c++) i = (i + 5) % T.CloudN;
            diveSlots.Add(i);
            diveTimes.Add(at);
            diveFlys.Add(T.FarFly((centre - Foot).magnitude, T.RefillFly));
            slot = i;
            return T.Orbit(i, at, centre, radius);
        }

        /// <summary>The coffin's mouth now, where a dead pawn's cover flies.</summary>
        public EgoSolemnLamentPoint Mouth()
        {
            float s = PictureClock.Since(startTick);
            return T.Mouth(Foot, T.CoffinRise(s, T.SinkAt(Cloud, Mathf.Max(T.ReturnTime, Home))));
        }

        public void Draw()
        {
            EgoSolemnLamentGraphics.DrawCoffin(new EgoSolemnLamentCoffin
            {
                Wielder = Centre, Aim = Aim, Coffin = coffin, Radius = radius, Cloud = Cloud, Home = Home, Face = !overclock,
                DiveSlots = diveSlots, DiveTimes = diveTimes, DiveFlys = diveFlys,
            }, PictureClock.Since(startTick), map);
        }
    }
}
