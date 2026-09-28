using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using S = RimArt.StoneThrow;

namespace RimArt
{
    /// <summary>
    /// Plays Todo's stone throw for real casts (StoneThrowGraphics). A cast is known from the moment
    /// its warmup starts (<see cref="Begin"/>, from JobDriver_CastMark), so the stone can glow in the
    /// hand and fly before it exists, and land as it is placed (<see cref="Placed"/>). A stone taken
    /// back flares in its cell during the warmup and flies to the hand from the moment it is lifted
    /// (<see cref="Lifted"/>). The stone at rest is MapComponent_Anchors' to draw, except while it
    /// skids into its cell and settles, which is drawn here with the item hidden (<see cref="Sliding"/>).
    ///
    /// The magician's card flick (ClapTeleportGraphics) is not drawn for Todo; it stays for a later
    /// hero. The clock is game ticks, so the picture pauses and speeds up with the game. Nothing is
    /// saved: a game loaded in the middle of a throw has lost a second of picture.
    /// </summary>
    public class MapComponent_MarkFlicks : MapComponent
    {
        private sealed class Flick
        {
            public Pawn carrier;
            public IntVec3 cell;
            public bool lifting;
            public Anchor anchor;
            public Thing stone;
            public int startTick, landTick = -1;
            public float warmup;
        }

        /// <summary>A cast whose warmup ran out this long ago without placing or lifting was interrupted.</summary>
        private const float Overdue = 0.25f;

        private readonly List<Flick> flicks = new List<Flick>();
        /// <summary>
        /// Carriers whose stone (placed or lifted) has landed in a cast job that has not ended yet (<see cref="Fired"/>).
        /// Not saved: a game loaded during the hold ends that job, as before.
        /// </summary>
        private readonly HashSet<Pawn> fired = new HashSet<Pawn>();

        public MapComponent_MarkFlicks(Map map) : base(map) { }

        /// <summary>A cast's warmup has begun. <paramref name="lifting"/> is the stone being taken back, or null.</summary>
        public void Begin(Pawn carrier, IntVec3 cell, float warmup, Anchor lifting)
        {
            Ended(carrier);
            if (carrier == null || !cell.IsValid) return;
            flicks.Add(new Flick
            {
                carrier = carrier, cell = cell, warmup = warmup, startTick = Find.TickManager.TicksGame,
                lifting = lifting != null, anchor = lifting,
            });
        }

        /// <summary>The stone now lies in its cell. A cast that was never begun still gets the landing.</summary>
        public void Placed(Pawn carrier, Anchor anchor, float warmup)
        {
            Flick flick = Unlanded(carrier, false) ?? Add(carrier, anchor.CurrentCell, warmup, false);
            flick.landTick = Find.TickManager.TicksGame;
            flick.stone = anchor.stone;
            fired.Add(carrier);
        }

        /// <summary>The stone has been taken back; it flies to the carrier's hand from where it was.</summary>
        public void Lifted(Pawn carrier, Anchor anchor, float warmup)
        {
            Flick flick = Unlanded(carrier, true) ?? Add(carrier, anchor.CurrentCell, warmup, true);
            flick.cell = anchor.CurrentCell;
            flick.anchor = null;
            flick.landTick = Find.TickManager.TicksGame;
            fired.Add(carrier);
        }

        private Flick Unlanded(Pawn carrier, bool lifting) => flicks.Find(f => f.carrier == carrier && f.lifting == lifting && f.landTick < 0);

        private Flick Add(Pawn carrier, IntVec3 cell, float warmup, bool lifting)
        {
            var flick = new Flick
            {
                carrier = carrier, cell = cell, warmup = warmup, lifting = lifting,
                startTick = Find.TickManager.TicksGame - Mathf.RoundToInt(warmup * 60f),
            };
            flicks.Add(flick);
            return flick;
        }

        /// <summary>The carrier's cast job is over. A stone that never landed has nothing left to show.</summary>
        public void Ended(Pawn carrier)
        {
            flicks.RemoveAll(f => f.carrier == carrier && f.landTick < 0);
            fired.Remove(carrier);
        }

        /// <summary>Whether the carrier's stone has landed in the cast job still running: the job holds from here (CastJobFail).</summary>
        public bool Fired(Pawn carrier) => fired.Contains(carrier);

        /// <summary>Whether this stone is still skidding into its cell: the item is hidden and drawn here instead.</summary>
        public bool Sliding(Thing stone) => Landed(stone, S.SkidTime);

        /// <summary>Whether this stone landed so recently that its settling is drawn here, not by MapComponent_Anchors.</summary>
        public bool Landing(Anchor anchor) => anchor.stone != null && Landed(anchor.stone, S.LandLife);

        private bool Landed(Thing stone, float within)
        {
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < flicks.Count; i++)
                if (!flicks[i].lifting && flicks[i].stone == stone && flicks[i].landTick >= 0 && (now - flicks[i].landTick) / 60f < within) return true;
            return false;
        }

        /// <summary>How much this stone flares, 0 to 1: a cast is about to take it back.</summary>
        public float Flare(Anchor anchor)
        {
            for (int i = 0; i < flicks.Count; i++)
            {
                Flick flick = flicks[i];
                if (flick.lifting && flick.landTick < 0 && flick.anchor == anchor) return StoneThrowGraphics.FlareAmount(Seconds(flick), flick.warmup);
            }
            return 0f;
        }

        private static float Seconds(Flick flick)
        {
            int now = Find.TickManager.TicksGame;
            return flick.landTick >= 0 ? flick.warmup + (now - flick.landTick) / 60f : (now - flick.startTick) / 60f;
        }

        public override void MapComponentUpdate()
        {
            if (flicks.Count == 0 || Find.CurrentMap != map) return;
            Vector2 sun = GenCelestial.GetLightSourceInfo(map, GenCelestial.LightType.Shadow).vector * SixPathsSlamGraphics.SunScale;
            float strength = 0.32f * GenCelestial.CurShadowStrength(map);
            for (int i = flicks.Count - 1; i >= 0; i--)
            {
                Flick flick = flicks[i];
                float seconds = Seconds(flick);
                bool landed = flick.landTick >= 0;
                float end = flick.lifting ? S.CatchTime(flick.warmup) + S.Absorb + 0.14f : flick.warmup + S.LandLife;
                if (flick.carrier == null || !flick.carrier.Spawned || flick.carrier.Map != map
                    || (landed ? seconds >= end : seconds > flick.warmup + Overdue))
                {
                    flicks.RemoveAt(i);
                    continue;
                }
                if (flick.cell.Fogged(map)) continue;
                Vector3 stands = flick.carrier.DrawPos, there = flick.cell.ToVector3Shifted();
                var todo = new Vector2(stands.x, stands.z);
                var cell = new Vector2(there.x, there.z);
                if (flick.lifting) StoneThrowGraphics.TakeBack(todo, cell, seconds, flick.warmup, sun, strength, false);
                else StoneThrowGraphics.Throw(todo, cell, seconds, flick.warmup, sun, strength, false);
            }
        }
    }
}
