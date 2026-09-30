using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Every Infinity Castle cast in the game (<see cref="InfinityCastleCast"/>): ticks their rules on game
    /// time, draws each one's home-map side (the warm-up, the take, the return) on the map on screen, and
    /// saves them. A cast stays here after the return until its picture has faded.
    /// </summary>
    public sealed class GameComponent_InfinityCastle : GameComponent
    {
        private List<InfinityCastleCast> casts = new List<InfinityCastleCast>();

        public GameComponent_InfinityCastle(Game game) { }

        public static GameComponent_InfinityCastle Instance => Current.Game?.GetComponent<GameComponent_InfinityCastle>();

        /// <summary>The cast that holds this pawn now (taking, or its castle stands), if any.</summary>
        public InfinityCastleCast For(Pawn pawn)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].caster == pawn && casts[i].Busy) return casts[i];
            return null;
        }

        /// <summary>The standing cast whose castle is <paramref name="castle"/>, if any.</summary>
        public InfinityCastleCast ForCastle(Map castle)
        {
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].castle == castle && casts[i].Standing) return casts[i];
            return null;
        }

        /// <summary>Whether <paramref name="pawn"/> should be on the dais playing: its castle stands and it is in it.</summary>
        public bool PlayingFor(Pawn pawn)
        {
            InfinityCastleCast cast = For(pawn);
            return cast != null && cast.Standing && pawn.Map == cast.castle;
        }

        public void Add(InfinityCastleCast cast) => casts.Add(cast);

        /// <summary>For game tests: drops every cast and removes every castle.</summary>
        public void ResetForTests()
        {
            foreach (InfinityCastleCast cast in casts)
                foreach (CastleTaken t in cast.taken)
                    if (t.pawn != null) InfinityCastleRide.Release(t.pawn);
            casts.Clear();
            foreach (Map map in Find.Maps)
                if (map.GetComponent<MapComponent_InfinityCastle>()?.IsCastle == true) InfinityCastleMap.CloseLater(map);
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            // The drawing registry is static: nothing hidden or riding carries over from another game.
            InfinityCastleRide.Clear();
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
            if (map == null || !RimWorld.Planet.WorldRendererUtility.DrawingMap) return;
            for (int i = 0; i < casts.Count; i++)
                if (casts[i].home == map) casts[i].Draw();
            DrawWarmups(map);
        }

        /// <summary>A Host warming up the ability on this map: the radius and a mark under each pawn that would be taken now.</summary>
        private static void DrawWarmups(Map map)
        {
            AbilityDef def = InfinityCastleDefOf.AG_Nakime_InfinityCastle;
            if (def == null) return;
            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
            {
                if (!(pawn.stances?.curStance is Stance_Warmup warmup) || !(warmup.verb is Verb_CastAbility verb) || verb.ability?.def != def) continue;
                var comp = verb.ability.CompOfType<CompAbilityEffect_InfinityCastle>();
                if (comp == null || !warmup.focusTarg.IsValid) continue;
                IntVec3 cell = warmup.focusTarg.Cell;
                float total = Mathf.Max(0.05f, verb.verbProps.warmupTime);
                float s = Mathf.Clamp(total - warmup.ticksLeft / 60f, 0f, total - 0.001f);
                List<Pawn> pawns = comp.CandidatesAt(cell);
                CastleOpenPlan plan = InfinityCastleCast.TakePlan(pawn, cell, pawns, comp.Props.radius, total);
                InfinityCastleOpenGraphics.Draw(new Vector2(cell.x + 0.5f, cell.z + 0.5f), plan, s, map);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref casts, "infinityCastleCasts", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (casts == null) casts = new List<InfinityCastleCast>();
                casts.RemoveAll(c => c == null || c.caster == null);
            }
        }
    }
}
