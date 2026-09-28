using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using G = RimArt.AmaterasuGraphics;
using T = RimArt.AmaterasuTiming;

namespace RimArt
{
    /// <summary>
    /// Amaterasu in the game: hands the black flames to AmaterasuGraphics (the port of rinnegan-amaterasu.js) every
    /// frame. The fires themselves are the rules' (<see cref="HediffComp_Amaterasu"/> on a pawn, <see cref="HeldWeapon"/>
    /// and <see cref="FlyingOn"/> in the air, <see cref="BlackFire"/> on the floor); this keeps only what is drawn after
    /// one goes out (the sink and smoke for a second, the stain for 20 s) and the one-off pictures.
    /// </summary>
    public static class AmaterasuPictures
    {
        private sealed class PawnOut
        {
            public Map map;
            public Pawn pawn;
            public int lit, outTick, seed;
            public AmaterasuCatch how;
            public Vector2 from, feet;
            public float scale;
        }

        private sealed class FloorOut
        {
            public Map map;
            public bool fuma;
            public Vector2 ground;
            public float turn;
            public int start, outTick, litTick, seed;
        }

        private sealed class Stopped
        {
            public Map map;
            public Vector2 from, stop;
            public float deg, speed, flown;
            public int stopTick, litTick, seed;
        }

        private static readonly List<PawnOut> pawnsOut = new List<PawnOut>();
        private static readonly List<FloorOut> floorsOut = new List<FloorOut>();
        private static readonly List<Stopped> stopped = new List<Stopped>();

        public static void Clear()
        {
            pawnsOut.Clear();
            floorsOut.Clear();
            stopped.Clear();
        }

        private static Vector2 At(Vector3 v) => new Vector2(v.x, v.z);
        private static Vector2 Ground(Vector3 drawn) => new Vector2(drawn.x, drawn.z - AmenoyodomiGraphics.HeldLift);
        private static float Scale(Pawn pawn) => Mathf.Clamp(Mathf.Sqrt(pawn.BodySize), 0.6f, 1.8f);
        private static int Seed(IntVec3 cell) => cell.x * 131 + cell.z * 7;

        // ---- Hooks from the rules --------------------------------------------------------------------------------

        public static void FlameOut(HediffComp_Amaterasu flame)
        {
            Pawn pawn = flame.Pawn;
            pawnsOut.Add(new PawnOut
            {
                map = pawn.Map,
                pawn = pawn,
                lit = flame.lit,
                outTick = Find.TickManager.TicksGame,
                seed = pawn.thingIDNumber,
                how = flame.how,
                from = VergilKit.Ground(flame.from),
                feet = VergilKit.Ground(pawn.DrawPos),
                scale = Scale(pawn)
            });
        }

        public static void FireOut(BlackFire fire)
        {
            if (fire.map == null) return;
            floorsOut.Add(new FloorOut
            {
                map = fire.map,
                fuma = fire.weapon != null,
                ground = FloorPoint(fire),
                turn = fire.turn,
                start = fire.startTick,
                outTick = Find.TickManager.TicksGame,
                litTick = fire.litTick,
                seed = Seed(fire.cell)
            });
        }

        /// <summary>A lit kunai stopped (hit or came down): its tail runs into it and the flecks along its path finish.</summary>
        public static void KunaiStopped(FlyingOn f, Map map, Vector3 stop)
        {
            if (map == null || f.shot == null) return;
            Vector3 heading = Rounds.Vanilla.Heading(f.shot);
            stopped.Add(new Stopped
            {
                map = map,
                from = Ground(f.from),
                stop = Ground(stop),
                deg = Mathf.Atan2(heading.z, heading.x) * Mathf.Rad2Deg,
                speed = f.shot.def.projectile.SpeedTilesPerTick * 60f,
                flown = (Find.TickManager.TicksGame - f.letGoTick) / 60f,
                stopTick = Find.TickManager.TicksGame,
                litTick = f.litTick,
                seed = f.shot.thingIDNumber
            });
        }

        /// <summary>A held weapon put out by Release: its flames sink for a second where it hangs.</summary>
        public static void HeldOut(HeldWeapon w)
        {
            int litTick = w.litTick, outTick = Find.TickManager.TicksGame, seed = w.shot?.thingIDNumber ?? 0;
            RinneganPictures.Add(w.shot?.MapHeld, T.OutDuration, s =>
            {
                if (GameComponent_Rinnegan.Instance?.HeldFor(w.shot) == null) return;
                G.HeldFlames(w.IsFuma, RinneganPictures.Ground(w), RinneganPictures.Degrees(w.heading), w.turned,
                    UbwClock.Since(litTick), s, seed);
            });
        }

        /// <summary>The burst where a held weapon catches.</summary>
        public static void Lit(HeldWeapon w)
        {
            bool fuma = w.IsFuma;
            Vector2 ground = RinneganPictures.Ground(w);
            float turn = w.turned;
            int seed = w.shot?.thingIDNumber ?? 0;
            RinneganPictures.Add(w.shot?.MapHeld, T.BurstLife + 0.11f, s => G.Lit(fuma, ground, turn, s, seed));
        }

        /// <summary>The cast: the screen dims for 0.18 s and the camera shakes.</summary>
        public static void Ignition(Pawn caster, Pawn target)
        {
            Map map = caster.Map;
            Vector2 at = target != null ? VergilKit.Ground(target.DrawPos) : At(caster.DrawPos);
            RinneganPictures.Add(map, T.DimLife, s => G.Dim(at, s));
            if (map == Find.CurrentMap) Find.CameraDriver.shaker.DoShake(T.Shake);
        }

        // ---- Every frame -----------------------------------------------------------------------------------------

        public static void Draw(Map map, GameComponent_Rinnegan rinnegan)
        {
            DrawCasters(map);
            DrawPawns(map, rinnegan);
            DrawHeld(map, rinnegan);
            DrawFlying(map, rinnegan);
            DrawFloors(map, rinnegan);
        }

        /// <summary>The red glint on the eye through Amaterasu's warmup, and the blood running while the eye bleeds.</summary>
        private static void DrawCasters(Map map)
        {
            IReadOnlyList<Pawn> pawns = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn.stances?.curStance is Stance_Warmup warmup && warmup.verb is Verb_CastAbility cast
                    && cast.ability?.def == SasukeDefOf.AG_SasukeAmaterasu)
                    G.Gaze(Eye(pawn), UbwClock.Since(warmup.startedTick), cast.verbProps.warmupTime);
                Hediff bleeding = pawn.health.hediffSet.GetFirstHediffOfDef(SasukeDefOf.AG_BleedingEye);
                // Seen from behind there is no eye to bleed from.
                if (bleeding != null && pawn.Rotation != Rot4.North) G.Blood(Eye(pawn), bleeding.ageTicks / 60f);
            }
        }

        /// <summary>The casting eye: the sketches' 0.04 cells beside the middle, toward the side the pawn faces.</summary>
        private static Vector2 Eye(Pawn pawn)
        {
            Vector2 eye = RinneganPictures.Eye(pawn);
            if (pawn.Rotation == Rot4.East) eye.x += 0.08f;
            else if (pawn.Rotation == Rot4.West) eye.x -= 0.16f;
            return eye;
        }

        private static void DrawPawns(Map map, GameComponent_Rinnegan rinnegan)
        {
            IReadOnlyList<HediffComp_Amaterasu> flames = rinnegan.Flames;
            for (int i = 0; i < flames.Count; i++)
            {
                HediffComp_Amaterasu flame = flames[i];
                Pawn pawn = flame.Pawn;
                if (pawn == null || !pawn.Spawned || pawn.Map != map || pawn.Dead || flame.ticksLeft <= 0) continue;
                Vector2 feet = VergilKit.Ground(pawn.DrawPos);
                float age = T.FireAge(UbwClock.Since(flame.lit), flame.how), sinceOut = -flame.ticksLeft / 60f;
                float scale = Scale(pawn);
                G.Pawn(feet, age, sinceOut, flame.how, pawn.thingIDNumber, VergilKit.Ground(flame.from), scale);
                G.PawnStain(feet, age, sinceOut, pawn.thingIDNumber, scale);
            }
            for (int i = pawnsOut.Count - 1; i >= 0; i--)
            {
                PawnOut o = pawnsOut[i];
                float sinceOut = UbwClock.Since(o.outTick);
                if (sinceOut >= T.StainDuration || Find.Maps.IndexOf(o.map) < 0)
                {
                    pawnsOut.RemoveAt(i);
                    continue;
                }
                if (o.map != map) continue;
                float age = T.FireAge(UbwClock.Since(o.lit), o.how);
                // A pawn that caught again burns under its new flame; only the stain of the old one is left.
                bool burning = Amaterasu.Burning(o.pawn);
                if (!burning && sinceOut < T.OutDuration && o.pawn.Spawned && o.pawn.Map == map)
                    G.Pawn(VergilKit.Ground(o.pawn.DrawPos), age, sinceOut, o.how, o.seed, o.from, o.scale);
                G.PawnStain(o.feet, age, sinceOut, o.seed, o.scale);
            }
        }

        /// <summary>Flames on the held weapons that are lit; order counts from the weapon furthest north.</summary>
        private static void DrawHeld(Map map, GameComponent_Rinnegan rinnegan)
        {
            List<HeldWeapon> lit = rinnegan.Held.Where(w => w.burning && w.shot != null && w.shot.Map == map)
                .OrderByDescending(w => w.at.z).ToList();
            for (int i = 0; i < lit.Count; i++)
            {
                HeldWeapon w = lit[i];
                G.HeldFlames(w.IsFuma, RinneganPictures.Ground(w), RinneganPictures.Degrees(w.heading), w.turned,
                    UbwClock.Since(w.litTick), float.NegativeInfinity, w.shot.thingIDNumber, i);
            }
        }

        /// <summary>Lit weapons after a let-go: the flames leaning back, a kunai's tail, the flecks along the path.</summary>
        private static void DrawFlying(Map map, GameComponent_Rinnegan rinnegan)
        {
            IReadOnlyList<FlyingOn> flying = rinnegan.Flying;
            for (int i = 0, order = 0; i < flying.Count; i++)
            {
                FlyingOn f = flying[i];
                if (!f.burning || f.shot == null || f.shot.Destroyed || f.shot.Map != map || rinnegan.HeldFor(f.shot) != null) continue;
                Vector2 ground = Ground(f.shot.ExactPosition);
                Vector3 heading = Rounds.Vanilla.Heading(f.shot);
                float deg = Mathf.Atan2(heading.z, heading.x) * Mathf.Rad2Deg;
                float age = UbwClock.Since(f.litTick), flown = UbwClock.Since(f.letGoTick);
                float speed = f.shot.def.projectile.SpeedTilesPerTick * 60f;
                bool fuma = f.shot is Projectile_Fuma;
                int seed = f.shot.thingIDNumber;
                G.FlyingFlames(fuma, ground, deg, age, float.NegativeInfinity, seed, order++);
                if (!fuma) G.Tail(ground, deg, speed, flown, 0f, age, float.NegativeInfinity, seed);
                G.PathFlecks(Ground(f.from), deg, speed, flown, flown, seed);
            }
            for (int i = stopped.Count - 1; i >= 0; i--)
            {
                Stopped s = stopped[i];
                float after = UbwClock.Since(s.stopTick);
                if (after > 0.5f || Find.Maps.IndexOf(s.map) < 0)
                {
                    stopped.RemoveAt(i);
                    continue;
                }
                if (s.map != map) continue;
                G.Tail(s.stop, s.deg, s.speed, s.flown, after, UbwClock.Since(s.litTick), float.NegativeInfinity, s.seed);
                G.PathFlecks(s.from, s.deg, s.speed, s.flown, s.flown + after, s.seed);
            }
        }

        private static Vector2 FloorPoint(BlackFire fire) =>
            fire.weapon != null && fire.weapon.Spawned ? At(fire.weapon.DrawPos) : new Vector2(fire.at.x, fire.at.z);

        /// <summary>Lit weapons lying on the floor and the patches lit conjured kunai leave, and their stains.</summary>
        private static void DrawFloors(Map map, GameComponent_Rinnegan rinnegan)
        {
            IReadOnlyList<BlackFire> fires = rinnegan.Fires;
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < fires.Count; i++)
            {
                BlackFire fire = fires[i];
                if (fire.map != map) continue;
                bool fuma = fire.weapon != null;
                Vector2 ground = FloorPoint(fire);
                float age = UbwClock.Since(fire.startTick), sinceOut = -(fire.endTick - now) / 60f;
                float litAge = fire.litTick >= 0 ? UbwClock.Since(fire.litTick) : -1f;
                int seed = Seed(fire.cell);
                if (fire.conjuredKunai) G.Lying(false, ground, fire.deg, 0f);
                G.OnFloor(fuma, ground, fire.turn, age, sinceOut, seed, litAge);
                G.WeaponStain(fuma, ground, age, sinceOut, seed);
            }
            for (int i = floorsOut.Count - 1; i >= 0; i--)
            {
                FloorOut o = floorsOut[i];
                float sinceOut = UbwClock.Since(o.outTick);
                if (sinceOut >= T.StainDuration || Find.Maps.IndexOf(o.map) < 0)
                {
                    floorsOut.RemoveAt(i);
                    continue;
                }
                if (o.map != map) continue;
                float age = UbwClock.Since(o.start), litAge = o.litTick >= 0 ? UbwClock.Since(o.litTick) : -1f;
                if (sinceOut < T.OutDuration) G.OnFloor(o.fuma, o.ground, o.turn, age, sinceOut, o.seed, litAge);
                G.WeaponStain(o.fuma, o.ground, age, sinceOut, o.seed);
            }
        }
    }
}
