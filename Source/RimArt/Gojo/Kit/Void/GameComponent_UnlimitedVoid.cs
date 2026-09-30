using System.Collections.Generic;
using UnityEngine;
using Verse;
using Open = RimArt.UnlimitedVoidOpenTiming;

namespace RimArt
{
    /// <summary>
    /// Every Unlimited Void cast in the game (<see cref="UnlimitedVoidCast"/>): ticks their rules on game time,
    /// draws the hand sign on a Gojo whose cast is warming up and each cast's home-map side on the map on screen,
    /// ends every domain when the Echo pool empties, and saves them. A cast stays here after the return until
    /// the ball's break has played.
    /// </summary>
    public sealed class GameComponent_UnlimitedVoid : GameComponent
    {
        private List<UnlimitedVoidCast> casts = new List<UnlimitedVoidCast>();
        /// <summary>Gojos warming the ability up, with its verb: the sign is drawn while the verb warms up. Not saved.</summary>
        private readonly Dictionary<Pawn, Verb> signing = new Dictionary<Pawn, Verb>();
        private static readonly List<Pawn> signDone = new List<Pawn>();

        static GameComponent_UnlimitedVoid()
        {
            EchoUtility.PoolEmptied += OnPoolEmptied;
        }

        public GameComponent_UnlimitedVoid(Game game) { }

        public static GameComponent_UnlimitedVoid Instance => Current.Game?.GetComponent<GameComponent_UnlimitedVoid>();

        /// <summary>The cast that holds this Gojo now (the barrier closing or the domain standing), if any.</summary>
        public UnlimitedVoidCast For(Pawn caster)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == caster && casts[i].Busy) return casts[i];
            return null;
        }

        /// <summary>The cast whose domain is (or was, until its map is removed) this pocket map, if any.</summary>
        public UnlimitedVoidCast ForPocket(Map map)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].pocket == map && !casts[i].fizzled) return casts[i];
            return null;
        }

        /// <summary>Whether a standing domain has this pawn frozen in it.</summary>
        public bool Holds(Pawn pawn)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].Standing && casts[i].Record(pawn)?.frozen == true) return true;
            return false;
        }

        /// <summary>The ability fired: the barrier starts to close over Gojo. <paramref name="paid"/> is the Echo charge it took.</summary>
        public UnlimitedVoidCast Begin(Pawn caster, float paid, bool heroForm)
        {
            if (For(caster) != null) return null;
            caster.pather?.StopDead();
            var cast = new UnlimitedVoidCast(caster, Find.TickManager.TicksGame, paid, heroForm);
            casts.Add(cast);
            signing.Remove(caster);
            return cast;
        }

        /// <summary>Called every tick the ability warms up.</summary>
        public void Signing(Pawn caster, Verb verb)
        {
            if (caster != null) signing[caster] = verb;
        }

        /// <summary>For game tests: drops every cast and removes every void.</summary>
        public void ResetForTests()
        {
            casts.Clear();
            signing.Clear();
            foreach (Map map in Find.Maps)
                if (map.GetComponent<MapComponent_UnlimitedVoid>()?.isVoid == true) UnlimitedVoidMap.CloseLater(map);
        }

        private static void OnPoolEmptied()
        {
            GameComponent_UnlimitedVoid instance = Instance;
            if (instance == null) return;
            foreach (UnlimitedVoidCast cast in instance.casts) cast.End("the Echo pool emptied");
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
                if (casts[i].home == map) casts[i].DrawHome();
            DrawSigns(map);
        }

        /// <summary>
        /// The hand sign on each Gojo warming the ability up: the open picture from the sign's start to its end,
        /// stretched over the verb's own warm-up (0.6 s in XML, longer with a slower aiming time).
        /// </summary>
        private void DrawSigns(Map map)
        {
            if (signing.Count == 0) return;
            signDone.Clear();
            int now = Find.TickManager.TicksGame;
            foreach (KeyValuePair<Pawn, Verb> pair in signing)
            {
                Pawn pawn = pair.Key;
                Stance_Warmup stance = pair.Value?.WarmupStance;
                if (pawn == null || !pawn.Spawned || stance == null)
                {
                    signDone.Add(pawn);
                    continue;
                }
                if (pawn.Map != map) continue;
                float total = Mathf.Max(1, now - stance.startedTick + stance.ticksLeft) / 60f;
                float s = Open.CastAt + Open.Warm * Mathf.Clamp01(UbwClock.Since(stance.startedTick) / total);
                Vector3 at = pawn.DrawPos;
                UnlimitedVoidOpenGraphics.Draw(new Vector2(at.x, at.z), Mathf.Min(s, Open.OpenAt - 0.001f), map);
            }
            foreach (Pawn pawn in signDone) signing.Remove(pawn);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref casts, "voidCasts", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (casts == null) casts = new List<UnlimitedVoidCast>();
                casts.RemoveAll(c => c == null || c.caster == null);
            }
        }
    }
}
