using System.Collections.Generic;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Every Magic Bullet shot in the game (<see cref="EgoMagicBulletCast"/>): ticks the aims and the action firings on game
    /// time and draws the pictures of the map on screen. With a selected wielder whose next shot is the seventh it draws a
    /// red line to the pawn the seventh will hit. Nothing is saved (see <see cref="EgoMagicBulletCast"/>).
    /// </summary>
    public sealed class GameComponent_EgoMagicBullet : GameComponent
    {
        private readonly List<EgoMagicBulletCast> casts = new List<EgoMagicBulletCast>();

        public GameComponent_EgoMagicBullet(Game game) { }

        public static GameComponent_EgoMagicBullet Instance => Current.Game?.GetComponent<GameComponent_EgoMagicBullet>();

        public IReadOnlyList<EgoMagicBulletCast> Casts => casts;

        /// <summary>A new aim or action firing. An aim replaces one by the same verb that never fired.</summary>
        public void Begin(EgoMagicBulletCast cast)
        {
            if (cast.verb != null) casts.RemoveAll(c => c.verb == cast.verb && !c.Fired);
            casts.Add(cast);
        }

        /// <summary>
        /// The verb fires now: the aim begun with its warmup goes off. With no aim (no warmup, or a load in the middle of
        /// one) a new one goes off at once.
        /// </summary>
        public EgoMagicBulletCast Shoot(Pawn shooter, CompEgoMagicBullet gun, Verb verb, LocalTargetInfo target)
        {
            EgoMagicBulletCast cast = null;
            for (int i = casts.Count - 1; i >= 0 && cast == null; i--)
                if (casts[i].verb == verb && !casts[i].Fired) cast = casts[i];
            if (cast == null)
            {
                cast = EgoMagicBulletCast.Aim(shooter, gun, verb, target, 0);
                casts.Add(cast);
            }
            cast.target = target;
            cast.Fire(Find.TickManager.TicksGame);
            return cast;
        }

        /// <summary>Whether a shot's picture by <paramref name="pawn"/> is up, drawing its rifle.</summary>
        public bool Drawing(Pawn pawn)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].shooter == pawn) return true;
            return false;
        }

        /// <summary>A test drops every shot.</summary>
        public void Clear() => casts.Clear();

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = casts.Count - 1; i >= 0; i--)
                if (i < casts.Count && !casts[i].Tick(now)) casts.RemoveAt(i);
        }

        public override void GameComponentUpdate()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            // The newest shot by a shooter draws the rifle and the count; older ones only their lines.
            for (int i = 0; i < casts.Count; i++)
            {
                EgoMagicBulletCast cast = casts[i];
                cast.lineOnly = false;
                for (int j = i + 1; j < casts.Count && !cast.lineOnly; j++)
                    if (casts[j].shooter == cast.shooter) cast.lineOnly = true;
                if (cast.map == map) cast.Draw();
            }
            List<object> selected = Find.Selector.SelectedObjectsListForReading;
            for (int i = 0; i < selected.Count; i++)
            {
                if (!(selected[i] is Pawn pawn) || !pawn.Spawned || pawn.Map != map) continue;
                CompEgoMagicBullet gun = CompEgoMagicBullet.HeldBy(pawn);
                if (gun == null || !gun.NextIsSeventh || gun.CoolingDown) continue;
                Pawn beloved = EgoMagicBullet.Beloved(pawn);
                if (beloved != pawn) GenDraw.DrawLineBetween(pawn.DrawPos, beloved.DrawPos, SimpleColor.Red);
            }
        }
    }
}
