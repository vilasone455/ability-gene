using System.Collections.Generic;
using UnityEngine;
using Verse;
using T = RimArt.ClapTeleport;

namespace RimArt
{
    /// <summary>
    /// Plays the clap teleport picture for real casts. A cast is known from the moment its warmup
    /// starts (<see cref="Begin"/>, from JobDriver_CastClap), so the cards can rise at both ends
    /// before the palms meet; <see cref="Land"/> is the swap itself and fixes the two ends to the
    /// cells the exchange used. A cast that lands without having been begun - a caster whose job
    /// is not the clap job - still gets everything from the contact on.
    ///
    /// The clock is game ticks, so the picture pauses and speeds up with the game. Nothing is
    /// saved: a game loaded in the middle of a clap has lost two seconds of cards and nothing else.
    /// </summary>
    public class MapComponent_ClapTeleports : MapComponent
    {
        private sealed class Cast
        {
            public Pawn carrier;
            public Anchor first, second;
            public int startTick, contactTick = -1;
            public float warmup;
            public bool twice, animated;
            public Vector2 palms;
            public ClapEnd end0, end1;
        }

        /// <summary>A cast whose warmup ran out this long ago without landing was interrupted.</summary>
        private const float Overdue = 0.25f;

        private readonly List<Cast> casts = new List<Cast>();
        /// <summary>
        /// Carriers whose clap has landed in a cast job that has not ended yet (<see cref="Fired"/>).
        /// Not saved: a game loaded during the hold ends that job, as before.
        /// </summary>
        private readonly HashSet<Pawn> fired = new HashSet<Pawn>();

        public MapComponent_ClapTeleports(Map map) : base(map) { }

        /// <summary><paramref name="second"/> is null for a clap, where the other end is the carrier.</summary>
        public void Begin(Pawn carrier, Anchor first, Anchor second, float warmup, bool twice, bool animated)
        {
            Ended(carrier);
            if (carrier == null || first == null) return;
            Vector3 stands = carrier.DrawPos;
            casts.Add(new Cast
            {
                carrier = carrier, first = first, second = second, warmup = warmup, twice = twice, animated = animated,
                startTick = Find.TickManager.TicksGame, palms = new Vector2(stands.x, stands.z + T.PalmsNorth),
            });
        }

        /// <summary>The exchange has happened. The ends are the cells it used, worked out before anyone moved.</summary>
        public void Land(Pawn carrier, ClapEnd end0, ClapEnd end1, float warmup, bool twice)
        {
            Cast cast = casts.Find(c => c.carrier == carrier && c.contactTick < 0);
            int now = Find.TickManager.TicksGame;
            if (cast == null)
            {
                cast = new Cast { carrier = carrier, twice = twice, startTick = now - Mathf.RoundToInt(warmup * 60f) };
                casts.Add(cast);
            }
            cast.warmup = warmup;
            cast.contactTick = now;
            fired.Add(carrier);
            cast.end0 = end0;
            cast.end1 = end1;
            if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(T.Shake);
            AnchorSound.Clap(carrier);
            AnchorSound.Puff(map, end0.ground);
            AnchorSound.Puff(map, end1.ground);
        }

        /// <summary>The carrier's cast job is over. A cast that never landed has nothing left to show.</summary>
        public void Ended(Pawn carrier)
        {
            casts.RemoveAll(c => c.carrier == carrier && c.contactTick < 0);
            fired.Remove(carrier);
        }

        /// <summary>Whether the carrier's clap has landed in the cast job still running: the job holds from here (CastJobFail).</summary>
        public bool Fired(Pawn carrier) => fired.Contains(carrier);

        /// <summary>
        /// Whether a clap against this mark is in its warmup, and how long its cards have been
        /// rising (negative before they start). MapComponent_Anchors draws the mark card with it.
        /// </summary>
        public bool Rising(Anchor anchor, out float rising)
        {
            for (int i = 0; i < casts.Count; i++)
            {
                Cast cast = casts[i];
                if (cast.contactTick >= 0 || (cast.first != anchor && cast.second != anchor)) continue;
                rising = Seconds(cast) - (cast.warmup - T.Rise);
                return true;
            }
            rising = -1f;
            return false;
        }

        public override void MapComponentUpdate()
        {
            if (casts.Count == 0 || Find.CurrentMap != map) return;
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                Cast cast = casts[i];
                float seconds = Seconds(cast);
                bool landed = cast.contactTick >= 0;
                if (landed ? seconds >= T.Duration(cast.warmup) : seconds > cast.warmup + Overdue || !Holds(cast))
                {
                    casts.RemoveAt(i);
                    continue;
                }

                if (!landed) ClapEnds.For(cast.carrier, cast.first, cast.second, true, out cast.end0, out cast.end1);
                // The mark cards are MapComponent_Anchors' to draw, before and after.
                ClapTeleportGraphics.DrawEnd(cast.end0, 0, seconds, cast.warmup, false, map);
                ClapTeleportGraphics.DrawEnd(cast.end1, 1, seconds, cast.warmup, false, map);
                if (!cast.animated) continue;
                // The clip's palms meet at its own times, which for the last one is the warmup's end.
                ClapTeleportGraphics.PalmStar(cast.palms, seconds - (cast.twice ? T.FirstContactAt(cast.warmup) : cast.warmup), 0);
                if (cast.twice) ClapTeleportGraphics.PalmStar(cast.palms, seconds - cast.warmup, 1);
            }
        }

        private static float Seconds(Cast cast)
        {
            int now = Find.TickManager.TicksGame;
            return cast.contactTick >= 0 ? cast.warmup + (now - cast.contactTick) / 60f : (now - cast.startTick) / 60f;
        }

        private bool Holds(Cast cast)
        {
            if (cast.carrier == null || !cast.carrier.Spawned || cast.carrier.Map != map) return false;
            return Stands(cast.first) && (cast.second == null || Stands(cast.second));
        }

        private bool Stands(Anchor anchor) => !anchor.IsOnPawn || (anchor.pawn.Spawned && anchor.pawn.Map == map);
    }

    /// <summary>The two ends of a clap, from who is marked and who moves.</summary>
    public static class ClapEnds
    {
        /// <summary>
        /// <paramref name="second"/> null is a clap: the ends are the carrier and the mark. Otherwise
        /// the ends are the two marks. An end gets the ring of cards when something arrives there;
        /// a cell that is only left - the carrier's on a tile-move, a pawn's when the other mark is a
        /// tile - gets the puff and one falling card. <paramref name="drawn"/> follows walking pawns
        /// for the warmup; the landing uses the cells themselves.
        /// </summary>
        public static void For(Pawn carrier, Anchor first, Anchor second, bool drawn, out ClapEnd end0, out ClapEnd end1)
        {
            if (second == null)
            {
                end0 = new ClapEnd { ground = Ground(carrier, carrier.Position, drawn), suit = first.suit, ring = first.IsOnPawn, mark = ClapMark.None };
                end1 = new ClapEnd { ground = Ground(first, drawn), suit = first.suit, ring = true, mark = Kind(first) };
                return;
            }
            end0 = new ClapEnd { ground = Ground(first, drawn), suit = first.suit, ring = second.IsOnPawn, mark = Kind(first) };
            end1 = new ClapEnd { ground = Ground(second, drawn), suit = second.suit, ring = first.IsOnPawn, mark = Kind(second) };
        }

        public static ClapMark Kind(Anchor anchor) => anchor.IsOnPawn ? ClapMark.Pawn : ClapMark.Tile;

        public static Vector2 Ground(Anchor anchor, bool drawn) => Ground(anchor.pawn, anchor.CurrentCell, drawn);

        private static Vector2 Ground(Pawn pawn, IntVec3 cell, bool drawn)
        {
            Vector3 at = drawn && pawn != null && pawn.Spawned ? pawn.DrawPos : cell.ToVector3Shifted();
            return new Vector2(at.x, at.z);
        }
    }
}
