using System.Collections.Generic;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Runs every Reset in progress: takes a destroyed body off the map and stands him up when the delay ends.
    /// Doing it here, not in his own tick or inside a damage call, keeps vanilla code that is still working on
    /// him (an explosion's cell loop, a kill) from seeing him vanish.
    /// </summary>
    public class GameComponent_Sato : GameComponent
    {
        private List<Pawn> resetting = new List<Pawn>();

        public GameComponent_Sato(Game game) { }

        public static GameComponent_Sato Instance => Current.Game?.GetComponent<GameComponent_Sato>();

        public IReadOnlyList<Pawn> Resetting => resetting;

        public void Register(Pawn pawn)
        {
            if (pawn != null && !resetting.Contains(pawn)) resetting.Add(pawn);
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = resetting.Count - 1; i >= 0; i--)
            {
                Pawn pawn = resetting[i];
                Hediff_AjinReset reset = AjinReset.Resetting(pawn);
                if (pawn == null || pawn.Destroyed || pawn.Discarded || reset == null)
                {
                    resetting.RemoveAt(i);
                    continue;
                }
                if (reset.destroyPending) AjinReset.DestroyBody(pawn, reset);
                if (now >= reset.riseTick)
                {
                    AjinReset.Rise(pawn, reset);
                    resetting.RemoveAt(i);
                }
            }
        }

        internal void ResetForTests() => resetting.Clear();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref resetting, "resetting", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (resetting == null) resetting = new List<Pawn>();
                resetting.RemoveAll(p => p == null);
            }
        }
    }
}
