using System.Collections.Generic;
using UnityEngine;
using Verse;
using T = RimArt.EgoSolemnLamentTiming;

namespace RimArt
{
    /// <summary>
    /// One burst of the pair as the picture draws it, from the warmup to the guns lowering after the last shot. Its clock
    /// is 0 when the warmup starts (the guns come up over Lead x 0.8 s); each shot is written when the verb fires it
    /// (<see cref="Verb_EgoSolemnLament"/>), with its hit (dist - 0.4) / 60 s later, as the sketch's script. Not saved.
    /// </summary>
    public sealed class EgoSolemnLamentBurstCast
    {
        public Pawn shooter;
        public Map map;
        public Verb verb;
        public LocalTargetInfo target;
        public int startTick;
        private readonly List<EgoSolemnLamentShot> shots = new List<EgoSolemnLamentShot>();
        private EgoSolemnLamentShot[] drawn = new EgoSolemnLamentShot[0];
        /// <summary>The last hit and the hit that reached the cap, on the burst's clock; negative before.</summary>
        private float last = -1f, downAt = -1f;

        public int ShotCount => shots.Count;
        public float Seconds(int tick) => (tick - startTick) / 60f;

        public EgoSolemnLamentBurstCast(Pawn shooter, Verb verb, LocalTargetInfo target)
        {
            this.shooter = shooter;
            this.verb = verb;
            this.target = target;
            map = shooter.Map;
            startTick = Find.TickManager.TicksGame;
        }

        /// <summary>A shot went off at <paramref name="tick"/>; it put <paramref name="add"/> stacks on after <paramref name="from"/>.</summary>
        public void Shot(int tick, bool white, float hitDelay, int from, int add, bool reachedCap)
        {
            float fire = Seconds(tick), hit = fire + hitDelay;
            shots.Add(new EgoSolemnLamentShot { K = shots.Count, White = white, Fire = fire, Hit = hit, From = from, Add = add });
            drawn = shots.ToArray();
            last = hit;
            if (reachedCap) downAt = hit;
        }

        /// <summary>
        /// One game tick; false once the burst is over: an aim whose warmup ended with no shot, or 1.3 s after the last hit
        /// once the verb has stopped bursting (the guns are down by then and the ink has gone).
        /// </summary>
        public bool Tick(int now)
        {
            if (shooter.Dead || !shooter.Spawned || shooter.Map != map) return false;
            if (shots.Count == 0) return shooter.stances.curStance is Stance_Warmup w && w.verb == verb;
            return verb.Bursting || Seconds(now) < last + 1.3f;
        }

        /// <summary>The guns and the shots this frame, aimed at the target where it is now.</summary>
        public void Draw()
        {
            Vector2 o = EgoSolemnLamentMarked.Ground(shooter.DrawPos);
            Vector2 at = target.HasThing && target.Thing.Spawned ? EgoSolemnLamentMarked.Ground(target.Thing.DrawPos)
                : EgoSolemnLamentMarked.Ground(target.Cell.ToVector3Shifted());
            Vector2 d = at - o;
            EgoSolemnLamentGraphics.DrawBurst(new EgoSolemnLamentBurst
            {
                Wielder = o, Aim = d.sqrMagnitude > 0.0001f ? Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg : 90f - shooter.Rotation.AsAngle, Target = at,
                Shots = drawn, ShotCount = drawn.Length, Last = last >= 0f ? last : float.MaxValue / 4f, DownAt = downAt,
            }, PictureClock.Since(startTick), map);
        }

        /// <summary>The round's flight from <paramref name="shooter"/> to <paramref name="target"/>, s: as the sketch, (distance - 0.4) / Speed.</summary>
        public static float HitDelay(Vector3 from, Vector3 to) =>
            Mathf.Max(0f, (EgoSolemnLamentMarked.Ground(to) - EgoSolemnLamentMarked.Ground(from)).magnitude - 0.4f) / T.Speed;
    }
}
