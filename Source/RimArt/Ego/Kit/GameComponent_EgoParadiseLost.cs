using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using T = RimArt.EgoParadiseLostTiming;

namespace RimArt
{
    /// <summary>
    /// Every Paradise Lost picture in the game: the thorns round each thing a shot or a ring struck, the rings, the
    /// corroded look of each wielder (from the action's Begin to Exit s after its End, so the wings fold after the state),
    /// and the staff of every holder. Ticks them on game time and draws those on the map on screen. Nothing is saved: a
    /// save in the second a picture lasts loses it, and the next firing of a running corrosion starts a new look.
    ///
    /// Who holds a staff is kept in <see cref="holders"/> (GameComponent_LastPrism's shape): the comp registers on equip
    /// and unequip, and a rescan once a second catches pawns that arrive already holding one and drops the dead.
    /// </summary>
    public sealed class GameComponent_EgoParadiseLost : GameComponent
    {
        /// <summary>Ticks between rescans of who holds a staff.</summary>
        public const int RescanEvery = 60;

        private readonly List<EgoParadiseLostThorns> thorns = new List<EgoParadiseLostThorns>();
        private readonly List<EgoParadiseLostRingCast> rings = new List<EgoParadiseLostRingCast>();
        private readonly List<EgoParadiseLostLook> looks = new List<EgoParadiseLostLook>();
        /// <summary>The tick of each wielder's last shot and last ring, for the staff's flash.</summary>
        private readonly Dictionary<Pawn, int> lastShot = new Dictionary<Pawn, int>(), lastRing = new Dictionary<Pawn, int>();
        private readonly HashSet<Pawn> holders = new HashSet<Pawn>();
        private readonly List<Pawn> holderList = new List<Pawn>();
        private int seeds;

        public GameComponent_EgoParadiseLost(Game game) { }

        public static GameComponent_EgoParadiseLost Instance => Current.Game?.GetComponent<GameComponent_EgoParadiseLost>();

        public IReadOnlyList<EgoParadiseLostThorns> Thorns => thorns;
        public IReadOnlyList<EgoParadiseLostRingCast> Rings => rings;
        public IReadOnlyList<EgoParadiseLostLook> Looks => looks;

        /// <summary>The comp's Notify_Equipped: <paramref name="pawn"/> holds a staff from now.</summary>
        public void Register(Pawn pawn)
        {
            if (pawn != null) holders.Add(pawn);
        }

        /// <summary>The comp's Notify_Unequipped.</summary>
        public void Unregister(Pawn pawn)
        {
            if (pawn != null) holders.Remove(pawn);
        }

        public bool Holds(Pawn pawn) => holders.Contains(pawn);

        /// <summary>A shot by <paramref name="wielder"/> struck <paramref name="struck"/>: the staff flashes and thorns rise round each, HitDelay s later.</summary>
        public void Shot(Pawn wielder, List<Thing> struck)
        {
            int now = Find.TickManager.TicksGame;
            lastShot[wielder] = now;
            for (int i = 0; i < struck.Count; i++)
                thorns.Add(new EgoParadiseLostThorns(struck[i], now, T.HitDelay, seeds++ * 31, ring: false));
        }

        /// <summary>
        /// A ring of <paramref name="radius"/> cells fired round <paramref name="wielder"/> and struck <paramref name="struck"/>:
        /// the ring spreads from where the wielder stands, and each pawn's small thorns rise as its edge passes them.
        /// </summary>
        public void Ring(Pawn wielder, float radius, List<Pawn> struck, SoundDef sound)
        {
            int now = Find.TickManager.TicksGame;
            lastRing[wielder] = now;
            Vector2 centre = Ground(wielder.DrawPos);
            rings.Add(new EgoParadiseLostRingCast { map = wielder.Map, centre = centre, radius = radius, tick = now });
            for (int i = 0; i < struck.Count; i++)
            {
                float d = Mathf.Clamp((Ground(struck[i].DrawPos) - centre).magnitude, 0.4f, radius);
                thorns.Add(new EgoParadiseLostThorns(struck[i], now, T.RingHitAge(d, radius), seeds++ * 31, ring: true));
            }
            sound?.PlayOneShot(new TargetInfo(wielder.Position, wielder.Map));
        }

        /// <summary>A corrosion or an Overclock started: the star, the wings and the halo open on the wielder. One look per wielder.</summary>
        public void BeginLook(Pawn wielder, bool overclock)
        {
            EndLook(wielder);
            looks.Add(new EgoParadiseLostLook { wielder = wielder, map = wielder.Map, overclock = overclock, startTick = Find.TickManager.TicksGame });
        }

        /// <summary>The corrosion or the Overclock is over: the look goes over Exit s.</summary>
        public void EndLook(Pawn wielder) => LookOf(wielder)?.End();

        /// <summary>The wielder's running look, null when there is none.</summary>
        public EgoParadiseLostLook LookOf(Pawn wielder)
        {
            for (int i = looks.Count - 1; i >= 0; i--)
                if (looks[i].wielder == wielder && looks[i].Running) return looks[i];
            return null;
        }

        /// <summary>A test drops every picture and forgets every holder (its new pawns register as they equip).</summary>
        public void Clear()
        {
            thorns.Clear();
            rings.Clear();
            looks.Clear();
            lastShot.Clear();
            lastRing.Clear();
            holders.Clear();
        }

        /// <summary>Every spawned pawn on any map holding a staff: equipment loaded with a save sends no Notify_Equipped.</summary>
        private void Rescan()
        {
            holders.Clear();
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                IReadOnlyList<Pawn> pawns = maps[m].mapPawns.AllPawnsSpawned;
                for (int i = 0; i < pawns.Count; i++)
                    if (CompEgoParadiseLost.HeldBy(pawns[i]) != null) holders.Add(pawns[i]);
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            Rescan();
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = thorns.Count - 1; i >= 0; i--)
                if (!thorns[i].Tick(now)) thorns.RemoveAt(i);
            for (int i = rings.Count - 1; i >= 0; i--)
                if ((now - rings[i].tick) / 60f >= T.RingLength()) rings.RemoveAt(i);
            for (int i = looks.Count - 1; i >= 0; i--)
                if (!looks[i].Tick(now)) looks.RemoveAt(i);
            if (now % RescanEvery != 0) return;
            Rescan();
            Forget(lastShot, now);
            Forget(lastRing, now);
        }

        private readonly List<Pawn> stale = new List<Pawn>();

        /// <summary>Drops flash ticks older than a second, so dead pawns are not kept.</summary>
        private void Forget(Dictionary<Pawn, int> ticks, int now)
        {
            stale.Clear();
            foreach (KeyValuePair<Pawn, int> entry in ticks)
                if (now - entry.Value > 60) stale.Add(entry.Key);
            for (int i = 0; i < stale.Count; i++) ticks.Remove(stale[i]);
        }

        public override void GameComponentUpdate()
        {
            Map map = Find.CurrentMap;
            if (map == null) return;
            for (int i = 0; i < looks.Count; i++)
                if (looks[i].map == map) looks[i].Draw();
            for (int i = 0; i < rings.Count; i++)
            {
                EgoParadiseLostRingCast ring = rings[i];
                if (ring.map == map)
                    EgoParadiseLostRingGraphics.Draw(new EgoParadiseLostRingShot { Wielder = ring.centre, Radius = ring.radius }, PictureClock.Since(ring.tick), map);
            }
            for (int i = 0; i < thorns.Count; i++)
                if (thorns[i].map == map) thorns[i].Draw();
            // Copied first: a draw-time recache can unequip and change the set.
            holderList.Clear();
            holderList.AddRange(holders);
            for (int i = 0; i < holderList.Count; i++)
            {
                Pawn pawn = holderList[i];
                if (pawn.Spawned && pawn.Map == map && ShowsStaff(pawn))
                    EgoParadiseLostStaffGraphics.Draw(Ground(pawn.DrawPos), pawn.Rotation, Flash(pawn), map);
            }
        }

        /// <summary>
        /// Whether the staff is drawn in the pawn's hand: standing, and wherever Core would show a held weapon (drafted, or a
        /// job that shows it), aiming or cooling down from a shot, corroded or overclocking, or while its look is up.
        /// </summary>
        private bool ShowsStaff(Pawn pawn)
        {
            if (pawn.Dead || pawn.GetPosture() != PawnPosture.Standing || CompEgoParadiseLost.HeldBy(pawn) == null) return false;
            if (PawnRenderUtility.CarryWeaponOpenly(pawn)) return true;
            if (pawn.stances?.curStance is Stance_Busy busy && busy.focusTarg.IsValid) return true;
            if (pawn.MentalState is MentalState_EgoCorroded || pawn.jobs?.curDriver is JobDriver_EgoOverclock) return true;
            for (int i = 0; i < looks.Count; i++)
                if (looks[i].wielder == pawn) return true;
            return false;
        }

        /// <summary>The apple and the halo on the staff: the last shot's flash or the last ring's, whichever is brighter.</summary>
        private float Flash(Pawn pawn)
        {
            float flash = 0f;
            if (lastShot.TryGetValue(pawn, out int shot)) flash = T.ShotFlash(PictureClock.Since(shot));
            if (lastRing.TryGetValue(pawn, out int ring)) flash = Mathf.Max(flash, T.RingFlashAt(PictureClock.Since(ring)));
            return flash;
        }

        /// <summary>A map point as the pictures take it: (x, z).</summary>
        public static Vector2 Ground(Vector3 at) => new Vector2(at.x, at.z);
    }

    /// <summary>One ring as drawn: where it spreads from, its radius and the tick it fired.</summary>
    public sealed class EgoParadiseLostRingCast
    {
        public Map map;
        public Vector2 centre;
        public float radius;
        public int tick;
    }

    /// <summary>
    /// The thorns round one thing struck, rising <see cref="delay"/> s after the firing. They stand where the thing is and
    /// stay where it was last seen once it has died or left.
    /// </summary>
    public sealed class EgoParadiseLostThorns
    {
        public readonly Thing thing;
        public readonly Map map;
        public readonly int tick, seed;
        public readonly float delay;
        public readonly bool ring;
        private Vector2 at;

        public EgoParadiseLostThorns(Thing thing, int tick, float delay, int seed, bool ring)
        {
            this.thing = thing;
            this.tick = tick;
            this.delay = delay;
            this.seed = seed;
            this.ring = ring;
            map = thing.Map;
            at = GameComponent_EgoParadiseLost.Ground(thing.DrawPos);
        }

        private bool Here => thing.Spawned && thing.Map == map;

        /// <summary>One game tick; false once the thorns and their marks are gone.</summary>
        public bool Tick(int now)
        {
            if (Here) at = GameComponent_EgoParadiseLost.Ground(thing.DrawPos);
            return (now - tick) / 60f - delay < T.HitGone(ring ? T.RingThornLife : T.ThornLife);
        }

        public void Draw()
        {
            Vector2 pos = Here ? GameComponent_EgoParadiseLost.Ground(thing.DrawPos) : at;
            EgoParadiseLostThornGraphics.Draw(pos, PictureClock.Since(tick) - delay, seed, ring, map);
        }
    }

    /// <summary>
    /// One wielder's corroded or Overclock look, from the action's Begin to Exit s after its End. A running look also ends
    /// here when its wielder has died or left the map, or is neither corroded nor overclocking: Pawn.Kill does not end a
    /// mental state, so a wielder killed outright never gets the state's PostEnd.
    /// </summary>
    public sealed class EgoParadiseLostLook
    {
        /// <summary>The look's exit time while the corrosion still runs: longer than any state.</summary>
        private const float Open = 100000f;

        public Pawn wielder;
        public Map map;
        public bool overclock;
        public int startTick;
        /// <summary>The tick the corrosion or Overclock ended, -1 while it runs.</summary>
        public int endTick = -1;
        private Vector2 at;
        private Rot4 facing = Rot4.South;

        public bool Running => endTick < 0;
        private float ExitAt => Running ? Open : (endTick - startTick) / 60f;

        public void End()
        {
            if (Running) endTick = Find.TickManager.TicksGame;
        }

        /// <summary>One game tick; false once the look has gone.</summary>
        public bool Tick(int now)
        {
            bool here = wielder.Spawned && wielder.Map == map;
            if (here)
            {
                at = GameComponent_EgoParadiseLost.Ground(wielder.DrawPos);
                facing = wielder.Rotation;
            }
            if (Running && (!here || !(wielder.MentalState is MentalState_EgoCorroded || wielder.jobs?.curDriver is JobDriver_EgoOverclock))) End();
            return Running || (now - startTick) / 60f < ExitAt + T.Exit;
        }

        public void Draw()
        {
            bool here = wielder.Spawned && wielder.Map == map;
            float s = PictureClock.Since(startTick);
            EgoParadiseLostCorrodedGraphics.Draw(here ? GameComponent_EgoParadiseLost.Ground(wielder.DrawPos) : at, here ? wielder.Rotation : facing,
                T.Look(s, ExitAt), s, map);
        }
    }
}
