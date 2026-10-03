using UnityEngine;
using Verse;
using static RimArt.VfxMath;
using T = RimArt.EgoSolemnLamentTiming;

namespace RimArt
{
    /// <summary>
    /// One pawn carrying Butterfly stacks, as the picture draws it: an <see cref="EgoSolemnLamentMark"/> on its own clock
    /// (seconds since <see cref="baseTick"/>) holding one flight per stack, in the order the stacks went on. The rules own
    /// the count (the AG_EgoButterfly hediff); this follows it: a flight per stack put on, the last one dropped per stack
    /// that fades, the swarm at the cap and off again below it, and the funeral's darkening and lift-off. Not saved.
    /// </summary>
    public sealed class EgoSolemnLamentMarked
    {
        public readonly Pawn pawn;
        public readonly int baseTick;
        public readonly EgoSolemnLamentMark mark;
        /// <summary>The coffin whose cloud killed it: its mouth is where the cover flies.</summary>
        public EgoSolemnLamentCoffinCast funeralBy;
        /// <summary>Where it was last drawn and how far it had turned lying down, for the lift-off after death.</summary>
        private Vector2 lastPos;
        private float lastTurn;
        /// <summary>Gun stacks put on so far, the seed of each one's flight.</summary>
        private int shotSeed;

        public EgoSolemnLamentMarked(Pawn pawn, int cap)
        {
            this.pawn = pawn;
            baseTick = Find.TickManager.TicksGame;
            lastPos = Ground(pawn.DrawPos);
            mark = new EgoSolemnLamentMark(lastPos, cap, 90f, Rand(pawn.thingIDNumber));
        }

        /// <summary>A DrawPos as a ground point (x, z).</summary>
        internal static Vector2 Ground(Vector3 v) => new Vector2(v.x, v.z);

        /// <summary>The mark's clock at <paramref name="tick"/>.</summary>
        public float At(int tick) => (tick - baseTick) / 60f;

        /// <summary>
        /// A gun's hit put <paramref name="add"/> stacks on after <paramref name="had"/>: each stack's butterfly leaves the
        /// chest 0.07 s after the last, loops out along the shot and across it and lands in FlyTime s, as the sketch's
        /// burst. The round lands <paramref name="hitDelay"/> s after <paramref name="fireTick"/>.
        /// </summary>
        public void Shot(int fireTick, float hitDelay, int had, int add, bool white, Vector2 aim)
        {
            float hit = At(fireTick) + hitDelay;
            var across = new Vector2(-aim.y, aim.x);
            for (int j = 0; j < add; j++)
            {
                int n = shotSeed++;
                float out1 = 0.3f + 0.4f * Rand(n + 600), side = (Rand(n + 610) - 0.5f) * 1.2f;
                mark.Flights.Add(new EgoSolemnLamentFlight
                {
                    Count = hit, Launch = hit + j * 0.07f, Fly = T.FlyTime, Slot = Mathf.Min(had + j, T.TorsoSlots + T.HeadSlots - 1), Dark = !white, Seed = n,
                    FromGround = new Vector2(mark.Home.x, mark.Home.y + PawnBody.Ground), FromHeight = T.ChestH,
                    Via = aim * out1 + across * side, Arc = 0.45f + 0.3f * Rand(n + 620),
                });
            }
            if (had < mark.Cap && had + add >= mark.Cap) mark.Swarm(hit);
        }

        /// <summary>
        /// The coffin's cloud put one stack on after <paramref name="had"/>: a cloud butterfly dives from
        /// <paramref name="from"/> and lands in DiveTime s. Past the cap it is a dark one landing on the cover, which turns a
        /// share of the cover dark (<see cref="EgoSolemnLamentMark.Darken"/>); at the death count the pawn is dead.
        /// </summary>
        public void Dive(int tick, int had, int slotSeed, EgoSolemnLamentPoint from, int death)
        {
            float at = At(tick), lands = at + T.DiveTime;
            int slot = had < mark.Cap || death <= mark.Cap ? Mathf.Min(had, T.TorsoSlots + T.HeadSlots - 1) : mark.Darken(had + 1 - mark.Cap, death - mark.Cap, lands);
            mark.Flights.Add(new EgoSolemnLamentFlight
            {
                Count = lands, Launch = at, Fly = T.DiveTime, Slot = slot, Dark = had >= mark.Cap || slotSeed % 2 == 1, Seed = slotSeed,
                FromGround = from.Ground, FromHeight = from.Height, Via = Vector2.zero, Arc = 0.2f,
            });
            if (had + 1 == mark.Cap) mark.Swarm(lands);
            else if (death > mark.Cap && had + 1 >= death) mark.DeadAt = lands;
        }

        /// <summary>
        /// A stack faded and <paramref name="left"/> remain: the newest stack's butterfly goes, and below the cap the
        /// swarm's cover with it.
        /// </summary>
        public void Faded(int left)
        {
            for (int i = mark.Flights.Count - 1; i >= 0 && mark.Counted(float.MaxValue) > left; i--)
                if (!mark.Flights[i].Swarm) mark.Flights.RemoveAt(i);
            if (left < mark.Cap && mark.Down)
            {
                mark.Flights.RemoveAll(f => f.Swarm);
                mark.DownAt = -1f;
            }
        }

        /// <summary>Whether there is nothing left to draw: no stacks, or dead and the cover gone into the coffin.</summary>
        public bool Over(float s)
        {
            if (mark.Dead) return s > mark.DeadAt + 0.15f + T.LiftTime + 3f;
            return pawn.Dead || mark.Flights.Count == 0 || !pawn.SpawnedOrAnyParentSpawned;
        }

        /// <summary>
        /// The butterflies and pips this frame, on the pawn where it is drawn: standing, or lying at the game's body angle
        /// once down (the fall eased in over FallTime when the cap downed it). After the funeral, on the body.
        /// </summary>
        public void Draw(Map map)
        {
            float s = PictureClock.Since(baseTick);
            bool lying = pawn.Dead || pawn.Downed;
            if (!pawn.Dead && pawn.Spawned)
            {
                lastPos = Ground(pawn.DrawPos);
                lastTurn = pawn.Downed ? pawn.Drawer.renderer.BodyAngle(PawnRenderFlags.None) : 0f;
            }
            else if (pawn.Corpse?.Spawned == true) lastPos = Ground(pawn.Corpse.DrawPos);
            if (!pawn.Dead) mark.Home = lastPos;
            if (funeralBy != null) mark.Mouth = funeralBy.Mouth();
            float fall = !lying ? 0f : mark.Down && !mark.Dead ? mark.Fall(s) : 1f;
            EgoSolemnLamentButterflies.DrawMark(mark, s, lastPos, fall, lastTurn * fall, T.Span, map, (pawn.thingIDNumber % 97) * 0.00001f);
        }
    }
}
