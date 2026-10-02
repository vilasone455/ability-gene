using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Solar Flare's picture: started with the warmup (hands to the face), the flash on the tick the
    /// ability fires, then the stunned pawns' stars until the stun ends. The pawns are the real ones,
    /// where they stand now: the blinded first, then the caster's own faction in sight (shadows only).
    /// The scripted wall is not drawn; the real map has real walls.
    /// </summary>
    public sealed class SolarFlarePicture : GokuPicture
    {
        private SolarFlarePlan plan;
        private readonly float radius;
        private IntVec3 centre;
        private readonly List<Pawn> pawns = new List<Pawn>();
        private int blinded;
        private Vector2[] pos = new Vector2[0];
        private int[] seeds = new int[0];

        public SolarFlarePicture(Pawn caster, int startTick, float warm, float stun, float radius)
        {
            this.caster = caster;
            home = caster.Map;
            this.startTick = startTick;
            this.radius = radius;
            centre = caster.Position;
            plan = GokuSolarFlareTiming.Plan(warm, stun);
        }

        private float Seconds(int now) => GokuSolarFlareTiming.Lead + (now - startTick) / 60f;

        /// <summary>The flash: who was blinded and who only threw a shadow.</summary>
        public void Fire(List<Pawn> hit, List<Pawn> others)
        {
            fired = true;
            centre = caster.Position;
            pawns.Clear();
            pawns.AddRange(hit);
            pawns.AddRange(others);
            blinded = hit.Count;
            pos = new Vector2[pawns.Count];
            seeds = new int[pawns.Count];
            for (int i = 0; i < pawns.Count; i++)
            {
                pos[i] = Vec(pawns[i].DrawPos);
                seeds[i] = i;
            }
        }

        public override bool Tick(int now)
        {
            float s = Seconds(now);
            return fired ? s < plan.End : s < plan.Flash + 0.2f;
        }

        public override void Draw()
        {
            if (home == null) return;
            float s = GokuSolarFlareTiming.Lead + PictureClock.Since(startTick);
            for (int i = 0; i < pawns.Count; i++)
                if (pawns[i].Spawned && pawns[i].Map == home) pos[i] = Vec(pawns[i].DrawPos);
            Vector2 o = fired ? Vec(centre) : (caster.Spawned ? Vec(caster.DrawPos) : Vec(centre));
            GokuSolarFlareGraphics.Draw(new SolarFlareShot
            {
                Centre = o, Seconds = s, Plan = plan, Radius = radius, Fade = GokuSolarFlareTiming.ScriptFade,
                Pawns = pos, Seeds = seeds, Count = pawns.Count, Blinded = blinded, Walled = false,
            }, home);
        }
    }

    /// <summary>
    /// Instant Transmission's picture: the forehead glint and the destination's ripples during the
    /// warmup, then the vanish where the caster stood and the re-forming where it landed. The real
    /// jump happens on the fire tick, which is the plan's Go.
    /// </summary>
    public sealed class TransmissionPicture : GokuPicture
    {
        private readonly TransmissionPlan plan;
        private IntVec3 from, dest;
        private bool carries, hostile, lying;
        private readonly List<Pawn> waiting = new List<Pawn>();
        private Vector2[] waitingPos;

        public TransmissionPicture(Pawn caster, int startTick, float warm, IntVec3 dest, Pawn passenger)
        {
            this.caster = caster;
            home = caster.Map;
            this.startTick = startTick;
            from = caster.Position;
            this.dest = dest;
            plan = GokuInstantTransmissionTiming.Plan(warm);
            Passenger(passenger);
            if (home != null && dest.InBounds(home))
                foreach (IntVec3 c in GenRadial.RadialCellsAround(dest, 2f, true))
                {
                    if (!c.InBounds(home)) continue;
                    Pawn p = c.GetFirstPawn(home);
                    if (p != null && p != caster && p != passenger && waiting.Count < 8) waiting.Add(p);
                }
            waitingPos = new Vector2[waiting.Count];
        }

        private void Passenger(Pawn passenger)
        {
            carries = passenger != null;
            hostile = passenger != null && passenger.HostileTo(caster);
            lying = passenger != null && passenger.Downed;
        }

        private float Seconds(int now) => GokuInstantTransmissionTiming.Lead + (now - startTick) / 60f;

        /// <summary>The jump happened: from <paramref name="home"/> to <paramref name="to"/>.</summary>
        public void Jumped(IntVec3 home, IntVec3 to, Pawn passenger)
        {
            fired = true;
            from = home;
            dest = to;
            Passenger(passenger);
        }

        public override bool Tick(int now)
        {
            float s = Seconds(now);
            return fired ? s < plan.End : s < plan.Go + 0.2f;
        }

        public override void Draw()
        {
            if (home == null) return;
            float s = GokuInstantTransmissionTiming.Lead + PictureClock.Since(startTick);
            for (int i = 0; i < waiting.Count; i++)
                if (waiting[i].Spawned && waiting[i].Map == home) waitingPos[i] = Vec(waiting[i].DrawPos);
            Vector2 a = Vec(from), b = Vec(dest), toward = b - a;
            toward = toward.sqrMagnitude < 0.01f ? Vector2.right : toward.normalized;
            GokuInstantTransmissionGraphics.Draw(new TransmissionShot
            {
                Home = a, Dest = b, Toward = toward, Seconds = s, Plan = plan,
                Carries = carries, Hostile = hostile, Lying = lying, Waiting = waitingPos, WaitingCount = waiting.Count,
            }, home);
        }
    }
}
