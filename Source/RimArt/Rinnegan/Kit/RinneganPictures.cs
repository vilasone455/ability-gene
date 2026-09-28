using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// What Sasuke's kit draws in the game. The rules call the hooks below when something happens; <see cref="Draw"/>
    /// runs every frame for the map on screen and draws the held weapons, the let-go streaks and the one-off pictures
    /// (catch, let go, drop, swap), each on the game clock smoothed between ticks, so they stop when the game is paused.
    ///
    /// Where the game and the sketches meet: a projectile is drawn at the chest height of a pawn standing in its cell,
    /// so a held weapon's ExactPosition is its drawn point and the floor point the sketches call its ground point is
    /// <see cref="AmenoyodomiGraphics.HeldLift"/> south of it. A pawn's feet are VergilKit.FeetBelowDrawPos south of its
    /// DrawPos. The drawing itself is in AmenoyodomiGraphics and AmenotejikaraGraphics (ports of the lab sketches).
    /// </summary>
    public static class RinneganPictures
    {
        /// <summary>The Fūma's full spin, 720 degrees a second, per tick; held, it turns at the hold share of it.</summary>
        public const float FumaSpinPerTick = AmenoyodomiGraphics.FumaSpin / 60f;

        /// <summary>The caster's eye, from his DrawPos: the sketches' eye is 0.62 north of the feet and 0.04 east.</summary>
        private static readonly Vector2 EyeFromDrawPos = new Vector2(0.04f, 0.62f - VergilKit.FeetBelowDrawPos);

        private sealed class Picture
        {
            public Map map;
            public int start;
            public float duration;
            public Action<float> draw;
        }

        private static readonly List<Picture> pictures = new List<Picture>();
        private static readonly Dictionary<string, Material> weaponMats = new Dictionary<string, Material>();

        internal static Material WeaponMat(Projectile shot)
        {
            string path = shot?.def?.graphicData?.texPath;
            if (path == null) return null;
            if (!weaponMats.TryGetValue(path, out Material mat))
            {
                mat = MaterialPool.MatFrom(path, ShaderDatabase.Cutout);
                weaponMats[path] = mat;
            }
            return mat;
        }

        public static Vector2 Eye(Pawn pawn) => new Vector2(pawn.DrawPos.x, pawn.DrawPos.z) + EyeFromDrawPos;
        public static Vector2 Feet(Pawn pawn) => VergilKit.Ground(pawn.DrawPos);
        /// <summary>The floor point under a held weapon.</summary>
        public static Vector2 Ground(HeldWeapon w) => new Vector2(w.at.x, w.at.z - AmenoyodomiGraphics.HeldLift);
        public static float Degrees(Vector3 heading) => Mathf.Atan2(heading.z, heading.x) * Mathf.Rad2Deg;

        /// <summary>A one-off picture on <paramref name="map"/>: <paramref name="draw"/> gets the seconds since now, each frame for <paramref name="duration"/>.</summary>
        internal static void Add(Map map, float duration, Action<float> draw)
        {
            if (map == null) return;
            pictures.Add(new Picture { map = map, start = Find.TickManager.TicksGame, duration = duration, draw = draw });
        }

        public static void Clear()
        {
            pictures.Clear();
            RaikoNetPictures.Clear();
            AmaterasuPictures.Clear();
        }

        // ---- Amenoyodomi ------------------------------------------------------------------------------------------

        /// <summary>The violet star on Sasuke's eye at every Amenoyodomi command (on, drift, let go, off).</summary>
        public static void EyeStar(Pawn caster)
        {
            Add(caster.MapHeld, AmenotejikaraTiming.EyeLife,
                s => AmenoyodomiGraphics.EyeStar(Eye(caster), 0.22f, 1f - s / AmenotejikaraTiming.EyeLife));
        }

        public static void Caught(HeldWeapon w)
        {
            bool fuma = w.IsFuma;
            Vector2 ground = Ground(w);
            Add(w.shot.MapHeld, AmenoyodomiGraphics.CatchDuration, s => AmenoyodomiGraphics.Catch(fuma, ground, s));
        }

        public static void LetGo(HeldWeapon w, bool charged)
        {
            bool fuma = w.IsFuma;
            Vector2 ground = Ground(w);
            Add(w.shot.MapHeld, AmenoyodomiGraphics.LetGoDuration, s => AmenoyodomiGraphics.LetGo(fuma, ground, s));
        }

        /// <summary>
        /// Dropped: the marks fade and dust rises where it lands. The weapon itself is already the game's item on the
        /// floor, so the falling copy of the sketch is not drawn.
        /// </summary>
        public static void Dropped(HeldWeapon w, Map map)
        {
            bool fuma = w.IsFuma;
            Vector2 ground = Ground(w);
            float deg = Degrees(w.heading), turn = w.turned;
            Add(map, AmenoyodomiGraphics.FallDuration,
                s => AmenoyodomiGraphics.Falling(fuma, ground, deg, turn, s, null, false));
        }

        /// <summary>A conjured kunai comes to rest and is gone: a little lavender dust.</summary>
        public static void Vanish(Vector3 at, Map map)
        {
            var ground = new Vector2(at.x, at.z);
            Add(map, 0.35f, s =>
            {
                AmenoyodomiGraphics.Dust(ground, s, false);
                VfxDraw.Sprite(ground, 0.4f, 0.4f, VfxDraw.Fade(AmenoyodomiGraphics.Lavender, 0.6f * (1f - s / 0.35f)), VfxDraw.glow, VfxDraw.Overhead + 0.01f);
            });
        }

        // ---- Amenotejikara ----------------------------------------------------------------------------------------

        private static Vector2 EndPoint(SwapEnd end, out SwapShape shape, out float deg)
        {
            deg = 0f;
            if (end.held != null)
            {
                shape = SwapShape.Kunai;
                deg = Degrees(end.held.heading);
                return Ground(end.held);
            }
            if (end.item != null)
            {
                shape = SwapShape.Item;
                return new Vector2(end.item.DrawPos.x, end.item.DrawPos.z);
            }
            shape = SwapShape.Pawn;
            return Feet(end.pawn);
        }

        /// <summary>
        /// The swap, drawn from the two ends as they were just before it: the pattern at both, the ghosts crossing,
        /// the afterimages, the eye star and the negative flash.
        /// </summary>
        public static void Swapped(Pawn caster, SwapEnd a, SwapEnd b, Vector3 groundA, Vector3 groundB)
        {
            Vector2 pa = EndPoint(a, out SwapShape sa, out float da), pb = EndPoint(b, out SwapShape sb, out float db);
            Add(caster.Map, AmenotejikaraTiming.Duration, s =>
            {
                AmenotejikaraGraphics.Pattern(pa, s, 0);
                AmenotejikaraGraphics.Pattern(pb, s, 1);
                AmenotejikaraGraphics.Afterimage(pa, s, sa, da);
                AmenotejikaraGraphics.Afterimage(pb, s, sb, db);
                AmenotejikaraGraphics.Ghost(pa, pb, s, sa, da);
                AmenotejikaraGraphics.Ghost(pb, pa, s, sb, db);
                if (caster.Spawned) AmenotejikaraGraphics.Eye(Eye(caster), s);
                AmenotejikaraGraphics.Flash(s, false, pa, pb);
            });
        }

        // ---- Raikō Kusari and Amaterasu (drawn by RaikoNetPictures and AmaterasuPictures) -------------------------

        public static void NetFormed(RaikoNet net) => RaikoNetPictures.Formed(net);
        public static void LinkSnapped(RaikoNet net, int link, HeldWeapon a, HeldWeapon b) { }
        public static void PawnCaught(RaikoNet net, Pawn pawn) { }
        public static void NetEnded(RaikoNet net, int now, bool letGo) => RaikoNetPictures.Ended(net);

        /// <summary>A pawn hit by a charged kunai or cut by the charged Fūma: a burst, then crackle for the stun.</summary>
        public static void Struck(Pawn pawn, float stunSeconds)
        {
            int seed = pawn.thingIDNumber % 97;
            Add(pawn.MapHeld, stunSeconds, s =>
            {
                if (pawn.Spawned) RaikoKusariGraphics.Struck(new Vector2(pawn.DrawPos.x, pawn.DrawPos.z), s, stunSeconds, seed, 0, UbwClock.Since(0));
            });
        }

        /// <summary>The burst where the charged Fūma lands.</summary>
        public static void ChargedFumaLands(Thing weapon)
        {
            var ground = new Vector2(weapon.DrawPos.x, weapon.DrawPos.z);
            Add(weapon.MapHeld, RaikoKusariTiming.FumaLandLife, s => RaikoKusariGraphics.FumaLands(ground, s));
        }

        public static void Ignition(Pawn caster, Pawn target) => AmaterasuPictures.Ignition(caster, target);
        public static void WeaponLit(HeldWeapon w) => AmaterasuPictures.Lit(w);

        // ---- Every frame ------------------------------------------------------------------------------------------

        public static void Draw(Map map, GameComponent_Rinnegan rinnegan)
        {
            IReadOnlyList<HeldWeapon> held = rinnegan.Held;
            for (int i = 0; i < held.Count; i++)
            {
                HeldWeapon w = held[i];
                if (w.shot == null || w.shot.Map != map || w.caster == null) continue;
                DrawHeld(w, SasukeKit.Amenoyodomi(w.caster));
            }

            foreach (Projectile shot in rinnegan.LetGoShots)
            {
                if (shot == null || shot.Destroyed || shot.Map != map) continue;
                Vector3 origin = Rounds.Vanilla.Origin(shot), now = shot.ExactPosition;
                Vector3 heading = Rounds.Vanilla.Heading(shot);
                float flown = (SasukeKit.Flat(now) - SasukeKit.Flat(origin)).magnitude;
                AmenoyodomiGraphics.Streak(shot is Projectile_Fuma, new Vector2(now.x, now.z), Degrees(heading),
                    shot.def.projectile.SpeedTilesPerTick * 60f, flown);
            }

            for (int i = pictures.Count - 1; i >= 0; i--)
            {
                Picture picture = pictures[i];
                float s = UbwClock.Since(picture.start);
                if (s >= picture.duration || Find.Maps.IndexOf(picture.map) < 0)
                {
                    pictures.RemoveAt(i);
                    continue;
                }
                if (picture.map == map) picture.draw(Mathf.Max(0f, s));
            }

            RaikoNetPictures.DrawCharges(map, rinnegan);
            RaikoNetPictures.Draw(map, rinnegan);
            RaikoNetPictures.DrawFlights(map, rinnegan);
            AmaterasuPictures.Draw(map, rinnegan);
        }

        private static void DrawHeld(HeldWeapon w, Ability_Amenoyodomi ability)
        {
            CompProperties_Amenoyodomi props = SasukeKit.HoldProps;
            float share = ability != null ? props.Share(ability.mode) : props.hangShare;
            var look = new HeldLook
            {
                fuma = w.IsFuma,
                ground = Ground(w),
                deg = Degrees(w.heading),
                turn = w.turned,
                since = UbwClock.Since(w.caughtTick),
                crept = w.crept,
                speed = w.speed * share * 60f,
                fullSpeed = w.speed * 60f,
                seed = w.shot.thingIDNumber,
                material = WeaponMat(w.shot)
            };
            AmenoyodomiGraphics.Held(look);
        }
    }
}
