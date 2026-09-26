using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Go = RimArt.NezukoBoxGoInTiming;
using Out = RimArt.NezukoBoxComeOutTiming;

namespace RimArt
{
    /// <summary>
    /// Everything the box draws on a map, and the timing of going in and coming out:
    /// - the worn box on every spawned standing wearer (asleep when full), drawn with the wearer's facing;
    /// - Go in: known from the warmup's start (<see cref="Begin"/>, from JobDriver_CastNezukoBox), fired at
    ///   <see cref="Fire"/>; the pawn goes in at the picture's Gone and the job holds until the latch;
    /// - Come out (<see cref="StartExit"/>): a strike rumbles, bursts and hands the pawn to a
    ///   PawnFlyer_NezukoBox at the picture's Leap, which strikes on landing (<see cref="Landed"/>); a calm
    ///   exit opens the door and lets the pawn out at the picture's Out.
    ///
    /// The clock is game ticks. Nothing here is saved: a game loaded mid-picture keeps the pawn in the box
    /// (or in its flyer) and loses the rest of the picture.
    /// </summary>
    public class MapComponent_NezukoBox : MapComponent
    {
        private sealed class GoInCast
        {
            public Pawn wearer, target;
            public int startTick, warmupTicks;
            public bool fired, taken, cancelled, latched;
            public float touchRange = 1.5f;

            /// <summary>Seconds on the picture's clock: the ability fires at Enter0, the warmup is the door opening before it.</summary>
            public float Seconds(int now) => Go.Enter0 + (now - startTick - warmupTicks) / 60f;
        }

        private sealed class ExitCast
        {
            public Pawn wearer, sleeper, enemy;
            public CompNezukoBox box;
            public NezukoExit kind;
            public int startTick;
            public IntVec3 land;
            public bool released;
            public float flight = Out.Flight;
            public Vector2 enemyAt;

            public float Seconds(int now) => (kind == NezukoExit.Strike ? Out.Rumble0 : Out.Open0) + (now - startTick) / 60f;
            /// <summary>In game the picture ends when the daze marks are gone (strike) or the door has shut; no extra hold.</summary>
            public float End => Out.End(kind, flight, 0f);
        }

        private readonly List<GoInCast> goIns = new List<GoInCast>();
        private readonly List<ExitCast> exits = new List<ExitCast>();
        private readonly List<Pawn> wearers = new List<Pawn>();
        private static readonly HashSet<CompNezukoBox> exiting = new HashSet<CompNezukoBox>();

        public MapComponent_NezukoBox(Map map) : base(map) { }

        /// <summary>A Come out of this box is playing and has not let the pawn out yet.</summary>
        public static bool Exiting(CompNezukoBox box) => exiting.Contains(box);

        // ------------------------------------------------------------------ Go in

        public void Begin(Pawn wearer, Pawn target, int warmupTicks)
        {
            goIns.RemoveAll(c => c.wearer == wearer);
            goIns.Add(new GoInCast { wearer = wearer, target = target, startTick = Find.TickManager.TicksGame, warmupTicks = warmupTicks });
            NezukoBoxSounds.Play(NezukoBoxSounds.DoorOpen, wearer.Position, map);
        }

        public void Fire(Pawn wearer, Pawn target, Ability ability, float touchRange)
        {
            GoInCast cast = goIns.Find(c => c.wearer == wearer && !c.fired);
            if (cast == null)
            {
                int warmup = Mathf.RoundToInt(ability.def.verbProperties.warmupTime * 60f);
                cast = new GoInCast { wearer = wearer, target = target, startTick = Find.TickManager.TicksGame - warmup, warmupTicks = warmup };
                goIns.Add(cast);
            }
            cast.fired = true;
            cast.target = target;
            cast.touchRange = touchRange;
        }

        public bool Fired(Pawn wearer) => goIns.Exists(c => c.wearer == wearer && c.fired);

        /// <summary>The job holds the wearer until the latch catches.</summary>
        public bool Holds(Pawn wearer)
        {
            GoInCast cast = goIns.Find(c => c.wearer == wearer);
            return cast != null && cast.fired && !cast.cancelled && cast.Seconds(Find.TickManager.TicksGame) < Go.Latch;
        }

        /// <summary>The job ended: a cast that never fired (interrupted in the warmup) is dropped.</summary>
        public void Ended(Pawn wearer) => goIns.RemoveAll(c => c.wearer == wearer && !c.fired);

        private void TickGoIn(GoInCast cast, int now)
        {
            if (cast.taken || cast.cancelled || !cast.fired) return;
            if (cast.Seconds(now) < Go.Gone) return;
            CompNezukoBox box = CompNezukoBox.WornBy(cast.wearer);
            Pawn target = cast.target;
            string why = box == null ? "the box is gone" : box.CannotTake(target, cast.wearer);
            if (why == null && (!cast.wearer.Spawned || target.Map != cast.wearer.Map || target.Position.DistanceTo(cast.wearer.Position) > cast.touchRange + 0.01f))
                why = "moved away";
            if (why != null || !box.Take(target))
            {
                cast.cancelled = true;
                return;
            }
            cast.taken = true;
            NezukoBoxSounds.Play(NezukoBoxSounds.In, cast.wearer.Position, map);
        }

        // ------------------------------------------------------------------ Come out

        /// <summary>Starts a Come out. <paramref name="land"/> is the strike's landing cell (ignored for the calm kinds).</summary>
        public void StartExit(Pawn wearer, CompNezukoBox box, NezukoExit kind, IntVec3 land)
        {
            if (box?.Sleeper == null || !wearer.Spawned || exiting.Contains(box)) return;
            if (kind == NezukoExit.Strike && !box.Leaps(box.Sleeper)) kind = box.Sleeper.Downed ? NezukoExit.Downed : NezukoExit.TimeUp;
            var cast = new ExitCast
            {
                wearer = wearer, box = box, sleeper = box.Sleeper, kind = kind, startTick = Find.TickManager.TicksGame,
                land = kind == NezukoExit.Strike ? land : CompNezukoBox.OutsideDoor(wearer),
                flight = NezukoBoxDefOf.AG_NezukoBoxLeap.pawnFlyer.flightDurationMin,
            };
            exits.Add(cast);
            exiting.Add(box);
            NezukoBoxSounds.Play(kind == NezukoExit.Strike ? NezukoBoxSounds.Burst : NezukoBoxSounds.DoorOpen, wearer.Position, map);
        }

        private void TickExit(ExitCast cast, int now)
        {
            if (cast.released) return;
            float s = cast.Seconds(now);
            // The wearer left the map or died before the pawn came out: the box's own tick lets it out.
            if (!cast.wearer.Spawned || cast.wearer.Dead || cast.box.Sleeper != cast.sleeper || cast.box.Wearer != cast.wearer)
            {
                cast.released = true;
                exiting.Remove(cast.box);
                return;
            }
            if (cast.kind == NezukoExit.Strike)
            {
                if (s < Out.Leap0) return;
                Leap(cast);
            }
            else
            {
                if (s < Out.Out0) return;
                Rot4 facing = NezukoBoxStrike.Toward(cast.wearer.Position, cast.land);
                Pawn pawn = cast.box.ReleaseAt(cast.land, map, facing);
                if (pawn != null) cast.land = pawn.Position;
            }
            cast.released = true;
            exiting.Remove(cast.box);
        }

        /// <summary>Takes the pawn out and sends it from the box's top to the landing cell in a PawnFlyer_NezukoBox.</summary>
        private void Leap(ExitCast cast)
        {
            Pawn wearer = cast.wearer;
            CompNezukoBox box = cast.box;
            if (!box.ValidLanding(wearer, cast.land, out _))
            {
                // The cell was taken since it was picked: the nearest good one within the range.
                if (!CellFinder.TryFindRandomCellNear(cast.land, map, 2, c => box.ValidLanding(wearer, c, out _), out IntVec3 near)) near = CompNezukoBox.OutsideDoor(wearer);
                cast.land = near;
            }
            // The flyer starts where the picture's box top is: the fitted feet, Back behind them along the facing.
            Vector2 top = BoxTop(wearer);
            Pawn pawn = box.TakeOut();
            if (pawn == null) return;
            pawn.Position = wearer.Position;
            pawn.Rotation = NezukoBoxStrike.Toward(wearer.Position, cast.land);
            var start = new Vector3(top.x, AltitudeLayer.Pawn.AltitudeFor(), top.y - NezukoBoxGraphics.FitDrop);
            var flyer = (PawnFlyer_NezukoBox)PawnFlyer.MakeFlyer(NezukoBoxDefOf.AG_NezukoBoxLeap, pawn, cast.land, null, null, false, start);
            flyer.Setup(box.Props.StunTicks, wearer.Drafted, NezukoBoxGraphics.H1 * NezukoBoxGraphics.FitScale, Out.Peak * NezukoBoxGraphics.FitScale);
            GenSpawn.Spawn(flyer, wearer.Position, map);
        }

        private static Vector2 BoxTop(Pawn wearer)
        {
            Vector2 feet = NezukoBoxGraphics.Feet(wearer.DrawPos);
            float a = Facing(wearer.Rotation) * Mathf.Deg2Rad;
            return feet + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (NezukoBoxGraphics.Back * NezukoBoxGraphics.FitScale);
        }

        /// <summary>The flyer has landed; <paramref name="enemy"/> is who it struck, or null.</summary>
        public void Landed(Pawn pawn, Pawn enemy)
        {
            ExitCast cast = exits.Find(c => c.sleeper == pawn && c.kind == NezukoExit.Strike);
            if (cast == null) return;
            cast.land = pawn.Position;
            cast.enemy = enemy;
            if (enemy != null) cast.enemyAt = NezukoBoxGraphics.Feet(enemy.DrawPos);
            Shake(0.04f);
            if (enemy != null) Shake(0.06f);
        }

        private void Shake(float size)
        {
            if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(size);
        }

        // ------------------------------------------------------------------ ticking and wearers

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            for (int i = goIns.Count - 1; i >= 0; i--)
            {
                GoInCast cast = goIns[i];
                TickGoIn(cast, now);
                float s = cast.Seconds(now);
                if (cast.taken && !cast.latched && s >= Go.Latch)
                {
                    cast.latched = true;
                    NezukoBoxSounds.Play(NezukoBoxSounds.Latch, cast.wearer.Position, map);
                }
                // Done: cancelled, asleep, the wearer gone, or a warmup that never fired (the job ended).
                bool done = cast.cancelled || !cast.wearer.Spawned || cast.fired && s >= Go.Asleep
                    || !cast.fired && now - cast.startTick > cast.warmupTicks + 60;
                if (done) goIns.RemoveAt(i);
            }
            for (int i = exits.Count - 1; i >= 0; i--)
            {
                ExitCast cast = exits[i];
                TickExit(cast, now);
                if (cast.Seconds(now) >= cast.End || !cast.wearer.Spawned)
                {
                    exiting.Remove(cast.box);
                    exits.RemoveAt(i);
                }
            }
            if (now % 60 == 0) Rescan();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            Rescan();
        }

        private void Rescan()
        {
            wearers.Clear();
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
                if (CompNezukoBox.WornBy(pawns[i]) != null) wearers.Add(pawns[i]);
        }

        /// <summary>A pawn put the box on: draw it from the next frame, not the next rescan.</summary>
        public void Register(Pawn wearer)
        {
            if (wearer != null && !wearers.Contains(wearer)) wearers.Add(wearer);
        }

        // ------------------------------------------------------------------ drawing

        internal static float Facing(Rot4 rotation)
        {
            if (rotation == Rot4.North) return 90f;
            if (rotation == Rot4.West) return 180f;
            if (rotation == Rot4.South) return 270f;
            return 0f;
        }

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map) return;
            int now = Find.TickManager.TicksGame;
            PowerPoleGraphics.Sun(map, out Vector2 sun, out float strength);
            float t = Time.realtimeSinceStartup;
            const float S = NezukoBoxGraphics.FitScale;

            for (int i = 0; i < wearers.Count; i++)
            {
                Pawn wearer = wearers[i];
                if (!Drawable(wearer)) continue;
                CompNezukoBox box = CompNezukoBox.WornBy(wearer);
                if (box == null || goIns.Exists(c => c.wearer == wearer && !c.cancelled) || exits.Exists(c => c.wearer == wearer)) continue;
                Vector2 feet = NezukoBoxGraphics.Feet(wearer.DrawPos);
                if (!VfxDraw.Shown(feet, map)) continue;
                VfxDraw.Begin(feet);
                var look = new NezukoBoxLook { Sleeping = box.Full ? 1f : 0f, T = t };
                NezukoBoxGraphics.Box(feet, Facing(wearer.Rotation), look, sun, strength, S, wearer.DrawPos.y);
            }

            for (int i = 0; i < goIns.Count; i++)
            {
                GoInCast cast = goIns[i];
                Pawn wearer = cast.wearer;
                if (!Drawable(wearer)) continue;
                float s = cast.fired ? cast.Seconds(now) : Mathf.Min(cast.Seconds(now), Go.Enter0 - 0.001f);
                if (cast.cancelled) continue;
                Vector2 feet = NezukoBoxGraphics.Feet(wearer.DrawPos);
                VfxDraw.Begin(feet);
                NezukoBoxPictures.GoIn(new NezukoGoInShot { Feet = feet, Facing = Facing(wearer.Rotation), Scale = S, PawnLayer = wearer.DrawPos.y, T = t }, s, sun, strength);
            }

            for (int i = 0; i < exits.Count; i++)
            {
                ExitCast cast = exits[i];
                Pawn wearer = cast.wearer;
                if (!Drawable(wearer)) continue;
                float s = cast.Seconds(now);
                Vector2 feet = NezukoBoxGraphics.Feet(wearer.DrawPos);
                VfxDraw.Begin(feet);
                Vector3 landCentre = cast.land.IsValid ? cast.land.ToVector3Shifted() : wearer.DrawPos;
                Vector2? enemy = null;
                if (cast.enemy != null)
                    enemy = cast.enemy.Spawned && cast.enemy.Map == map ? NezukoBoxGraphics.Feet(cast.enemy.DrawPos) : cast.enemyAt;
                NezukoBoxPictures.ComeOut(new NezukoComeOutShot
                {
                    Feet = feet, Facing = Facing(wearer.Rotation), Kind = cast.kind, Land = NezukoBoxGraphics.Feet(landCentre), Enemy = enemy,
                    Flight = cast.flight, Peak = Out.Peak, Scale = S, PawnLayer = wearer.DrawPos.y, T = t,
                }, s, sun, strength);
            }
        }

        /// <summary>The box is drawn on a spawned wearer that is standing (not lying down, downed or carried).</summary>
        private bool Drawable(Pawn wearer) =>
            wearer != null && wearer.Spawned && wearer.Map == map && !wearer.Dead && wearer.GetPosture() == PawnPosture.Standing;

        public override void MapRemoved()
        {
            foreach (ExitCast cast in exits) exiting.Remove(cast.box);
            exits.Clear();
            goIns.Clear();
        }
    }
}
