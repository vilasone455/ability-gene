using System.Collections.Generic;
using UnityEngine;
using Verse;
using F = RimArt.MarkFlick;

namespace RimArt
{
    /// <summary>
    /// Plays the Mark card flick for real casts. A placing cast is known from the moment its warmup
    /// starts (<see cref="Begin"/>, from JobDriver_CastMark), so the card can leave the hand before
    /// the mark exists and land as it is placed (<see cref="Placed"/>). A lifted mark flies the
    /// other way, from its target to the carrier's hand, starting when it is lifted
    /// (<see cref="Lifted"/>). The mark card itself is MapComponent_Anchors' to draw.
    ///
    /// The clock is game ticks, so the picture pauses and speeds up with the game. Nothing is
    /// saved: a game loaded in the middle of a flick has lost a tenth of a second of card.
    /// </summary>
    public class MapComponent_MarkFlicks : MapComponent
    {
        private sealed class Flick
        {
            public Pawn carrier;
            public LocalTargetInfo target;
            public bool lifting;
            public ClapMark kind;
            public int startTick, landTick = -1;
            public float warmup;
            /// <summary>Where a lifted mark was, which its pawn may since have walked away from.</summary>
            public Vector2 markWas;
        }

        /// <summary>A placing cast whose warmup ran out this long ago without placing was interrupted.</summary>
        private const float Overdue = 0.25f;

        private readonly List<Flick> flicks = new List<Flick>();
        /// <summary>
        /// Carriers whose mark (placed or lifted) has landed in a cast job that has not ended yet (<see cref="Fired"/>).
        /// Not saved: a game loaded during the hold ends that job, as before.
        /// </summary>
        private readonly HashSet<Pawn> fired = new HashSet<Pawn>();

        public MapComponent_MarkFlicks(Map map) : base(map) { }

        /// <summary>A placing cast's warmup has begun.</summary>
        public void Begin(Pawn carrier, LocalTargetInfo target, float warmup)
        {
            Ended(carrier);
            if (carrier == null || !target.IsValid) return;
            flicks.Add(new Flick
            {
                carrier = carrier, target = target, warmup = warmup, startTick = Find.TickManager.TicksGame,
                kind = target.Pawn != null ? ClapMark.Pawn : ClapMark.Tile,
            });
        }

        /// <summary>The mark now exists. A cast that was never begun still gets the sparkle.</summary>
        public void Placed(Pawn carrier, Anchor anchor)
        {
            Flick flick = flicks.Find(f => f.carrier == carrier && !f.lifting && f.landTick < 0);
            if (flick == null)
            {
                flick = new Flick { carrier = carrier, target = anchor.IsOnPawn ? anchor.pawn : new LocalTargetInfo(anchor.cell), kind = ClapEnds.Kind(anchor) };
                flicks.Add(flick);
            }
            flick.landTick = Find.TickManager.TicksGame;
            fired.Add(carrier);
        }

        /// <summary>The mark has been taken back; its card flies to the carrier's hand from where it was.</summary>
        public void Lifted(Pawn carrier, Anchor anchor)
        {
            Ended(carrier);
            flicks.Add(new Flick
            {
                carrier = carrier, lifting = true, kind = ClapEnds.Kind(anchor), landTick = Find.TickManager.TicksGame,
                markWas = F.MarkPoint(ClapEnds.Ground(anchor, true), ClapEnds.Kind(anchor)),
            });
            fired.Add(carrier);
        }

        /// <summary>The carrier's cast job is over. A card that never landed has nothing left to show.</summary>
        public void Ended(Pawn carrier)
        {
            flicks.RemoveAll(f => f.carrier == carrier && f.landTick < 0);
            fired.Remove(carrier);
        }

        /// <summary>Whether the carrier's mark has landed in the cast job still running: the job holds from here (CastJobFail).</summary>
        public bool Fired(Pawn carrier) => fired.Contains(carrier);

        public override void MapComponentUpdate()
        {
            if (flicks.Count == 0 || Find.CurrentMap != map) return;
            int now = Find.TickManager.TicksGame;
            for (int i = flicks.Count - 1; i >= 0; i--)
            {
                Flick flick = flicks[i];
                if (flick.carrier == null || !flick.carrier.Spawned || flick.carrier.Map != map) { flicks.RemoveAt(i); continue; }
                Vector3 stands = flick.carrier.DrawPos;
                var carrier = new Vector2(stands.x, stands.z);

                if (flick.lifting)
                {
                    float age = (now - flick.landTick) / 60f;
                    if (age >= F.CatchFlight + F.SparkleLife) { flicks.RemoveAt(i); continue; }
                    Vector2 hand = F.Hand(carrier, flick.markWas, true);
                    ClapTeleportGraphics.FlyingCard(flick.markWas, hand, F.MarkHeight(flick.kind), F.HandHeight,
                        ClapTeleport.MarkScale, 1f, age / F.CatchFlight, age, map);
                    ClapTeleportGraphics.FlickSparkle(hand, 0.16f, age - F.CatchFlight);
                    continue;
                }

                if (flick.target.Pawn != null && (!flick.target.Pawn.Spawned || flick.target.Pawn.Map != map)) { flicks.RemoveAt(i); continue; }
                Vector3 there = flick.target.Pawn != null ? flick.target.Pawn.DrawPos : flick.target.Cell.ToVector3Shifted();
                Vector2 mark = F.MarkPoint(new Vector2(there.x, there.z), flick.kind);
                if (flick.landTick >= 0)
                {
                    float age = (now - flick.landTick) / 60f;
                    if (age >= F.SparkleLife) { flicks.RemoveAt(i); continue; }
                    ClapTeleportGraphics.FlickSparkle(mark, 0.22f, age);
                    continue;
                }

                float seconds = (now - flick.startTick) / 60f;
                if (seconds > flick.warmup + Overdue) { flicks.RemoveAt(i); continue; }
                float flying = seconds - F.Release, flight = Mathf.Max(0.02f, flick.warmup - F.Release);
                ClapTeleportGraphics.FlyingCard(F.Hand(carrier, mark, false), mark, F.HandHeight, F.MarkHeight(flick.kind),
                    1f, ClapTeleport.MarkScale, flying / flight, flying, map);
            }
        }
    }
}
