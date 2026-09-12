using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>Tracks real pawn casts separately from the free-running debug preview.</summary>
    public class MapComponent_ShinraCasts : MapComponent
    {
        private sealed class Cast
        {
            public Pawn pawn;
            public Vector3 centre;
            public ShinraCastAnimation.Handle animation;
            public float time;
            public int tailStartTick = -1;
            public bool released;
        }

        private readonly List<Cast> casts = new List<Cast>();
        public MapComponent_ShinraCasts(Map map) : base(map) { }

        public bool Running(Pawn pawn) => casts.Exists(cast => cast.pawn == pawn);

        public void Begin(Pawn pawn, ShinraCastAnimation.Handle animation)
        {
            casts.Add(new Cast { pawn = pawn, centre = pawn.Position.ToVector3Shifted(), animation = animation });
        }

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map) return;
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                Cast cast = casts[i];
                if (!cast.pawn.Spawned || cast.pawn.Map != map || cast.pawn.Dead || cast.pawn.Downed)
                { casts.RemoveAt(i); continue; }

                if (cast.tailStartTick < 0)
                {
                    if (!cast.animation.Read(out cast.time, out bool finished))
                    { casts.RemoveAt(i); continue; }
                    if (finished) cast.tailStartTick = Find.TickManager.TicksGame;
                }
                float time = cast.time + (cast.tailStartTick < 0 ? 0f :
                    (Find.TickManager.TicksGame - cast.tailStartTick) / 60f);
                if (time >= ShinraVfxTiming.Duration) { casts.RemoveAt(i); continue; }
                if (!cast.released && time >= ShinraVfxTiming.ChargeEnd)
                {
                    cast.released = true;
                    ShinraSound.Release(map, cast.centre.ToIntVec3());
                }
                if (!cast.centre.ToIntVec3().Fogged(map)) ShinraVfxGraphics.Draw(cast.centre, time, map);
            }
        }
    }
}
