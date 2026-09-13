using Verse;

namespace RimArt
{
    // Drawing never advances or commits gameplay. All maps share the game-tick controller.
    public class MapComponent_ShinraCasts : MapComponent
    {
        public MapComponent_ShinraCasts(Map map) : base(map) { }
        public bool Running(Pawn pawn) => GameComponent_Shinra.Instance.For(pawn).active;
        public void Begin(Pawn pawn, ShinraCastAnimation.Handle animation) =>
            GameComponent_Shinra.Instance.Begin(pawn, animation);
        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map) return;
            foreach (var s in GameComponent_Shinra.Instance.States)
            {
                if (s.map != map || s.centre.ToIntVec3().Fogged(map)) continue;
                float time = s.tail >= 0f ? s.tail : s.active ? s.charge.time : -1f;
                if (time >= ShinraCharge.Burst) ShinraVfxGraphics.Draw(s.centre, time, map);
            }
        }
    }
}
