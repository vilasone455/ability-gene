using System.Collections.Generic;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Every Unlimited Blade Works cast in the game (<see cref="UbwCast"/>): ticks their rules on game time,
    /// draws each one's home-map side (the chant, the fire, the ring left burning, the return) on the map on
    /// screen, and saves them. A cast stays here after the return until its picture has faded.
    /// </summary>
    public sealed class GameComponent_UnlimitedBladeWorks : GameComponent
    {
        private List<UbwCast> casts = new List<UbwCast>();

        public GameComponent_UnlimitedBladeWorks(Game game) { }

        public static GameComponent_UnlimitedBladeWorks Instance => Current.Game?.GetComponent<GameComponent_UnlimitedBladeWorks>();

        /// <summary>The cast that holds this pawn now (queued, chanting, released or standing), if any.</summary>
        public UbwCast For(Pawn pawn)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == pawn && casts[i].Busy) return casts[i];
            return null;
        }

        /// <summary>The ability fired, <paramref name="paid"/> charge taken for it: the chant is queued and begins when its job starts.</summary>
        public void Queue(Pawn caster, float paid)
        {
            if (For(caster) != null) return;
            casts.Add(new UbwCast(caster, Find.TickManager.TicksGame, paid));
        }

        /// <summary>For game tests: drops every cast and removes every world.</summary>
        public void ResetForTests()
        {
            casts.Clear();
            foreach (Map map in Find.Maps)
                if (map.GetComponent<MapComponent_UnlimitedBladeWorks>()?.IsWorld == true) UnlimitedBladeWorksMap.CloseLater(map);
        }

        /// <summary>The chant job started. False if no cast was waiting for it: the job then ends.</summary>
        public bool ChantStarted(Pawn caster)
        {
            UbwCast cast = For(caster);
            if (cast == null || !cast.Queued) return false;
            cast.StartChant(Find.TickManager.TicksGame);
            return true;
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            UbwRules rules = UbwRules.Of;
            UbwCastTiming.Configure(rules.radiusByVerse, rules.verseSeconds);
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = casts.Count - 1; i >= 0; i--)
                if (!casts[i].Tick(now)) casts.RemoveAt(i);
        }

        public override void GameComponentUpdate()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].home == map) casts[i].Draw();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref casts, "ubwCasts", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (casts == null) casts = new List<UbwCast>();
                casts.RemoveAll(c => c == null || c.caster == null);
            }
        }
    }
}
