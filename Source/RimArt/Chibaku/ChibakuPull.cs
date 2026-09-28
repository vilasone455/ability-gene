using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimArt
{
    /// <summary>One pawn taken by the ball, from the moment the pull holds it until it lands again.</summary>
    public sealed class ChibakuHeld : IExposable
    {
        public const int Waiting = 0, Flying = 1, Held = 2, Falling = 3, Landed = 4;

        public Pawn pawn;
        public Lord lord;
        public int state;
        /// <summary>Seconds on the ball's timeline: when it leaves the ground, and when it lands after the burst.</summary>
        public float liftAt, landAt;
        /// <summary>Where it stood when it was lifted (its draw position), and where it lands.</summary>
        public Vector3 from;
        public IntVec3 landCell;
        /// <summary>Its offset under the ball as it falls out, in cells.</summary>
        public Vector2 drop;
        internal Texture picture;
        internal Material material;

        public void ExposeData()
        {
            // A lord that has ended is saved nowhere, and a reference to it would not resolve on load.
            if (Scribe.mode == LoadSaveMode.Saving && lord != null && (lord.Map == null || !lord.Map.lordManager.lords.Contains(lord))) lord = null;
            Scribe_References.Look(ref pawn, "pawn", true);
            Scribe_References.Look(ref lord, "lord");
            Scribe_Values.Look(ref state, "state");
        }
    }

    /// <summary>
    /// The rules of Chibaku Tensei's pull on pawns (proposed, placeholders; 2026-09-28):
    ///
    /// - From the start of the pull until the ball can still take them in, any pawn standing on a plate that
    ///   is pulled (not under a building or a roof) is caught: stunned where it stands, then lifted just
    ///   before its plate tears free and pulled into the core. Colonists are caught too. A pawn that walks
    ///   onto the circle during the pull is caught as well.
    /// - Lifting takes it off the map into the ball (held by the map component, so the game still counts it on
    ///   the map); it keeps ticking (bleeding, needs) and cannot act or be targeted. It leaves its raid group.
    /// - At the burst every pawn inside falls out round the spot under the ball and lands 0.6 s later: 2 blunt
    ///   for each second it was held plus 8 for the fall, dealt in hits of up to 8, then stunned 2.5 s. It goes
    ///   back to its raid group, or a hostile whose group has ended gets a new assault group.
    ///
    /// Pinned pawns (Black Receiver) are not on this branch yet; there is no caster in the preview.
    /// </summary>
    public sealed class ChibakuPull
    {
        public const float CrushPerSecond = 2f, FallDamage = 8f, HitSize = 8f, LeadOfPlate = .12f, FlySeconds = .75f;
        public const int StunTicks = 150;
        public static readonly float FallTime = Mathf.Sqrt(2f * (ChibakuBall.Height - .3f) / ChibakuBall.Gravity);

        private readonly MapComponent_ChibakuPlates owner;
        private readonly Map map;
        private readonly ChibakuBall ball;
        private readonly IntVec3 centre;
        public readonly List<ChibakuHeld> pawns = new List<ChibakuHeld>();
        private bool burst;

        public ChibakuPull(MapComponent_ChibakuPlates owner, ChibakuBall ball, IntVec3 centre)
        {
            this.owner = owner;
            map = owner.map;
            this.ball = ball;
            this.centre = centre;
        }

        /// <summary>The last moment a pawn can be lifted and still reach the ball before it is formed.</summary>
        public static float LastCatch => ChibakuBall.Formed - FlySeconds;

        public void Tick(float s)
        {
            if (s >= ChibakuBall.Pull && s < LastCatch) Catch(s);
            foreach (ChibakuHeld h in pawns)
            {
                if (h.state == ChibakuHeld.Waiting)
                {
                    if (h.pawn == null || !h.pawn.Spawned || h.pawn.Map != map) { h.state = ChibakuHeld.Landed; continue; }
                    if (s >= h.liftAt) Lift(h);
                }
                else if (h.state == ChibakuHeld.Flying && s >= h.liftAt + FlySeconds) h.state = ChibakuHeld.Held;
            }
            if (!burst && s >= ChibakuBall.Burst)
            {
                burst = true;
                if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(1.2f);
                int k = 0;
                foreach (ChibakuHeld h in pawns)
                {
                    if (h.state != ChibakuHeld.Flying && h.state != ChibakuHeld.Held) continue;
                    h.state = ChibakuHeld.Falling;
                    h.drop = new Vector2((float)ChibakuCut.Rand(k * 9 + 401) - .5f, (float)ChibakuCut.Rand(k * 9 + 402) - .5f) * 2.2f;
                    k++;
                    h.landAt = ChibakuBall.Burst + FallTime;
                    h.landCell = NezukoBoxStandable(new IntVec3(Mathf.RoundToInt(centre.x + h.drop.x), 0, Mathf.RoundToInt(centre.z + h.drop.y)));
                }
            }
            var landed = new List<ChibakuHeld>();
            foreach (ChibakuHeld h in pawns)
                if (h.state == ChibakuHeld.Falling && s >= h.landAt) landed.Add(h);
            if (landed.Count > 0) Land(landed, true);
        }

        /// <summary>Every spawned pawn on a pulled plate that is not caught yet is caught now.</summary>
        private void Catch(float s)
        {
            foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
            {
                if (pawns.Any(h => h.pawn == p) || !ball.TryLiftOf(p.Position, out float plateLift)) continue;
                var h = new ChibakuHeld { pawn = p, state = ChibakuHeld.Waiting, liftAt = Mathf.Max(s, plateLift - LeadOfPlate) };
                pawns.Add(h);
                int ticks = Mathf.CeilToInt((h.liftAt - s) * 60f) + 5;
                if (ticks > 0) p.stances?.stunner?.StunFor(ticks, null, false, false);
            }
        }

        private void Lift(ChibakuHeld h)
        {
            Pawn p = h.pawn;
            h.from = p.DrawPos;
            h.picture = KamuiBend.Render(p, p.Rotation);
            h.material = new Material(ShaderDatabase.Transparent) { mainTexture = h.picture, name = "RimArt Chibaku pawn" };
            h.lord = p.GetLord();
            h.lord?.RemovePawn(p);
            if (p.carryTracker?.CarriedThing != null) p.carryTracker.TryDropCarriedThing(p.Position, ThingPlaceMode.Near, out _);
            p.jobs?.StopAll();
            p.pather?.StopDead();
            p.DeSpawn(DestroyMode.WillReplace);
            if (!owner.Inner.TryAdd(p, false))
            {
                GenSpawn.Spawn(p, NezukoBoxStandable(p.PositionHeld), map);
                h.state = ChibakuHeld.Landed;
                Forget(h);
                return;
            }
            h.state = ChibakuHeld.Flying;
        }

        /// <summary>
        /// Puts pawns back on the map. <paramref name="hurt"/>: a landing from the burst (crush and fall damage,
        /// stun); otherwise a plain release (the preview stopped early, or a save was loaded mid-hold).
        /// </summary>
        public void Land(List<ChibakuHeld> which, bool hurt)
        {
            var assault = new List<Pawn>();
            foreach (ChibakuHeld h in which)
            {
                Pawn p = h.pawn;
                h.state = ChibakuHeld.Landed;
                Forget(h);
                if (p == null || !owner.Inner.Contains(p)) continue;
                owner.Inner.Remove(p);
                GenSpawn.Spawn(p, h.landCell.IsValid ? h.landCell : NezukoBoxStandable(centre), map);
                if (!hurt) { Rejoin(h, assault); continue; }
                float held = Mathf.Max(0f, ChibakuBall.Burst - (h.liftAt + FlySeconds));
                for (float left = CrushPerSecond * held + FallDamage; left > 0f && !p.Dead; left -= HitSize)
                    p.TakeDamage(new DamageInfo(DamageDefOf.Blunt, Mathf.Min(HitSize, left)));
                if (p.Dead) continue;
                p.stances?.stunner?.StunFor(StunTicks, null, false);
                Rejoin(h, assault);
            }
            foreach (IGrouping<Faction, Pawn> group in assault.GroupBy(p => p.Faction))
                LordMaker.MakeNewLord(group.Key, new LordJob_AssaultColony(group.Key, false, false, false, false, false), map, group);
            // Anything else inside (a pawn that died in there is a corpse now) is dropped under the ball.
            for (int i = owner.Inner.Count - 1; i >= 0; i--)
                if (!(owner.Inner[i] is Pawn held && pawns.Any(h => h.pawn == held && h.state != ChibakuHeld.Landed)))
                    owner.Inner.TryDrop(owner.Inner[i], NezukoBoxStandable(centre), map, ThingPlaceMode.Near, out _);
        }

        /// <summary>Everyone still up is put down now, unhurt (the preview was stopped).</summary>
        public void ReleaseAll()
        {
            var up = pawns.Where(h => h.state == ChibakuHeld.Flying || h.state == ChibakuHeld.Held || h.state == ChibakuHeld.Falling).ToList();
            foreach (ChibakuHeld h in up) h.landCell = NezukoBoxStandable(centre);
            Land(up, false);
            foreach (ChibakuHeld h in pawns) Forget(h);
        }

        private void Rejoin(ChibakuHeld h, List<Pawn> assault)
        {
            Pawn p = h.pawn;
            if (h.lord != null && map.lordManager.lords.Contains(h.lord)) h.lord.AddPawn(p);
            else if (p.Faction != null && p.Faction != Faction.OfPlayer && p.HostileTo(Faction.OfPlayer)) assault.Add(p);
        }

        private static void Forget(ChibakuHeld h)
        {
            if (h.material != null) Object.Destroy(h.material);
            if (h.picture != null) KamuiBend.Free(h.picture);
            h.material = null;
            h.picture = null;
        }

        private IntVec3 NezukoBoxStandable(IntVec3 cell) => CompNezukoBox.StandableNear(cell, map, null);
    }
}
