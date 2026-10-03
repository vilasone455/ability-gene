using System.Collections.Generic;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Every Solemn Lament picture in the game: the bursts (<see cref="EgoSolemnLamentBurstCast"/>), the coffins
    /// (<see cref="EgoSolemnLamentCoffinCast"/>) and the butterflies on each pawn with stacks
    /// (<see cref="EgoSolemnLamentMarked"/>). Ticks them on game time and draws those on the map on screen. Nothing is
    /// saved: the stacks themselves are the AG_EgoButterfly hediff.
    /// </summary>
    public sealed class GameComponent_EgoSolemnLament : GameComponent
    {
        private readonly List<EgoSolemnLamentBurstCast> bursts = new List<EgoSolemnLamentBurstCast>();
        private readonly List<EgoSolemnLamentCoffinCast> coffins = new List<EgoSolemnLamentCoffinCast>();
        private readonly Dictionary<Pawn, EgoSolemnLamentMarked> marks = new Dictionary<Pawn, EgoSolemnLamentMarked>();
        private readonly List<Pawn> markKeys = new List<Pawn>();

        public GameComponent_EgoSolemnLament(Game game) { }

        public static GameComponent_EgoSolemnLament Instance => Current.Game?.GetComponent<GameComponent_EgoSolemnLament>();

        public IReadOnlyList<EgoSolemnLamentBurstCast> Bursts => bursts;
        public IReadOnlyList<EgoSolemnLamentCoffinCast> Coffins => coffins;

        /// <summary>A burst's warmup began: it replaces the shooter's last burst, so one pair of guns is drawn.</summary>
        public void Begin(EgoSolemnLamentBurstCast burst)
        {
            bursts.RemoveAll(b => b.shooter == burst.shooter);
            bursts.Add(burst);
        }

        /// <summary>The burst <paramref name="verb"/> is firing, or a new one when it fires with no warmup.</summary>
        public EgoSolemnLamentBurstCast BurstFor(Pawn shooter, Verb verb, LocalTargetInfo target)
        {
            for (int i = bursts.Count - 1; i >= 0; i--)
                if (bursts[i].verb == verb) return bursts[i];
            var burst = new EgoSolemnLamentBurstCast(shooter, verb, target);
            Begin(burst);
            return burst;
        }

        /// <summary>The butterflies on <paramref name="pawn"/>, made the first time it takes a stack.</summary>
        public EgoSolemnLamentMarked Mark(Pawn pawn)
        {
            if (!marks.TryGetValue(pawn, out EgoSolemnLamentMarked marked) || marked.mark.Dead)
            {
                marked = new EgoSolemnLamentMarked(pawn, EgoButterflyExtension.Of.cap);
                marks[pawn] = marked;
            }
            return marked;
        }

        public EgoSolemnLamentMarked MarkOf(Pawn pawn) => marks.TryGetValue(pawn, out EgoSolemnLamentMarked marked) ? marked : null;

        /// <summary>A stack faded on <paramref name="pawn"/> and <paramref name="left"/> remain.</summary>
        public void Faded(Pawn pawn, int left) => MarkOf(pawn)?.Faded(left);

        /// <summary>A corrosion or Overclock starts: the coffin rises where the wielder stands. One running coffin per wielder.</summary>
        public EgoSolemnLamentCoffinCast BeginCoffin(Pawn wielder, float radius, bool overclock)
        {
            EndCoffin(wielder);
            var coffin = new EgoSolemnLamentCoffinCast(wielder, radius, overclock);
            coffins.Add(coffin);
            return coffin;
        }

        public EgoSolemnLamentCoffinCast CoffinOf(Pawn wielder)
        {
            for (int i = coffins.Count - 1; i >= 0; i--)
                if (coffins[i].wielder == wielder && coffins[i].Running) return coffins[i];
            return null;
        }

        public void EndCoffin(Pawn wielder) => CoffinOf(wielder)?.End();

        /// <summary>
        /// Whether a burst or a running coffin of <paramref name="pawn"/>'s is up, drawing its guns. A coffin that has ended
        /// and is sinking draws no guns, so Core draws them on the pawn again.
        /// </summary>
        public bool Drawing(Pawn pawn)
        {
            for (int i = 0; i < bursts.Count; i++)
                if (bursts[i].shooter == pawn) return true;
            for (int i = 0; i < coffins.Count; i++)
                if (coffins[i].wielder == pawn && coffins[i].Running) return true;
            return false;
        }

        /// <summary>A test drops every picture.</summary>
        public void Clear()
        {
            bursts.Clear();
            coffins.Clear();
            marks.Clear();
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = bursts.Count - 1; i >= 0; i--)
                if (!bursts[i].Tick(now)) bursts.RemoveAt(i);
            for (int i = coffins.Count - 1; i >= 0; i--)
                if (!coffins[i].Tick(now)) coffins.RemoveAt(i);
            if (now % 30 != 0) return;
            markKeys.Clear();
            markKeys.AddRange(marks.Keys);
            foreach (Pawn pawn in markKeys)
                if (marks[pawn].Over(marks[pawn].At(now))) marks.Remove(pawn);
        }

        public override void GameComponentUpdate()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            for (int i = 0; i < coffins.Count; i++)
                if (coffins[i].map == map) coffins[i].Draw();
            for (int i = 0; i < bursts.Count; i++)
                if (bursts[i].map == map) bursts[i].Draw();
            foreach (EgoSolemnLamentMarked marked in marks.Values)
                if (marked.pawn.MapHeld == map) marked.Draw(map);
        }
    }
}
