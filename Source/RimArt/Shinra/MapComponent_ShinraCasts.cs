using Verse;

namespace RimArt
{
    // Drawing never advances or commits gameplay. All maps share the game-tick controller.
    public class MapComponent_ShinraCasts : MapComponent
    {
        public MapComponent_ShinraCasts(Map map) : base(map) { }
        public bool Running(Pawn pawn) => GameComponent_Shinra.Instance.For(pawn).active;
        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map) return;
            foreach (var s in GameComponent_Shinra.Instance.States)
            {
                if (s.map != map || s.centre.ToIntVec3().Fogged(map)) continue;
                if (s.active) ShinraSleeves.Draw(s.animation);
                // The picture runs on its own clock from the burst, sized to this cast.
                if (s.tail >= 0f) ShinraVfxGraphics.Draw(s.centre, s.tail, map, s.tailRadius);
                else if (s.active && s.charge.burst) ShinraVfxGraphics.Draw(s.centre, s.PictureTime, map, s.Radius);
            }
        }
    }
}
