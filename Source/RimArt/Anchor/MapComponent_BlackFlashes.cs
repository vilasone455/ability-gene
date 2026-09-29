using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using F = RimArt.BlackFlash;

namespace RimArt
{
    /// <summary>
    /// Plays Black Flash for real punches (BlackFlashGraphics). The hit is known when the punch lands
    /// (<see cref="Hit"/>, from CompAbilityEffect_BlackFlash); the black sparks on the fist before it
    /// are drawn from Todo's warmup stance, and only while the window is open, so a punch that will be
    /// ordinary never crackles. After a Black Flash the target shows stun stars for the stun and Todo
    /// crackles for as long as he is in the zone.
    ///
    /// The contact follows the target as it rocks back from the hit. The clock is game ticks.
    /// Nothing is saved: a game loaded in the middle of a punch has lost the picture, not the hit.
    /// </summary>
    public class MapComponent_BlackFlashes : MapComponent
    {
        private sealed class Strike
        {
            public Pawn caster;
            public Thing target;
            public bool flash, shaken, zone;
            public int hitTick;
            public float warmup;
            public Vector2 foe, aim;
            public HediffDef zoneDef;
        }

        private readonly List<Strike> strikes = new List<Strike>();

        public MapComponent_BlackFlashes(Map map) : base(map) { }

        /// <summary>
        /// The fist landed. <paramref name="flash"/> is a Black Flash, otherwise an ordinary punch;
        /// <paramref name="zone"/> is the hediff Todo's sparks last for.
        /// </summary>
        public void Hit(Pawn caster, Thing target, bool flash, float warmup, HediffDef zone)
        {
            if (caster == null || target == null) return;
            // A new Black Flash takes over the zone's sparks.
            if (flash) foreach (Strike old in strikes) if (old.caster == caster) old.zone = false;
            Vector3 at = target.DrawPos, from = caster.DrawPos;
            var aim = new Vector2(at.x - from.x, at.z - from.z);
            strikes.Add(new Strike
            {
                caster = caster, target = target, flash = flash, zone = flash && zone != null, zoneDef = zone, warmup = warmup, hitTick = Find.TickManager.TicksGame,
                foe = new Vector2(at.x, at.z), aim = aim.sqrMagnitude < 1e-6f ? Vector2.right : aim.normalized,
                shaken = !flash,
            });
            if (!flash && Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(F.PlainShake);
        }

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map) return;
            // Drawn on real pawns: heights on the body are fitted to them (see PawnFit).
            PawnFit.Begin();
            try
            {
                Draw();
            }
            finally
            {
                PawnFit.End();
            }
        }

        private void Draw()
        {
            DrawWarmups();
            if (strikes.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            for (int i = strikes.Count - 1; i >= 0; i--)
            {
                Strike strike = strikes[i];
                float age = (now - strike.hitTick) / 60f, seconds = strike.warmup + age;
                bool zone = strike.zone && strike.caster.Spawned && strike.caster.Map == map && strike.caster.health.hediffSet.HasHediff(strike.zoneDef);
                if (age >= F.After && !zone)
                {
                    strikes.RemoveAt(i);
                    continue;
                }
                if (strike.target.Spawned && strike.target.Map == map)
                {
                    Vector3 at = strike.target.DrawPos;
                    strike.foe = new Vector2(at.x, at.z);
                }
                Vector2 contact = F.Contact(strike.foe, strike.aim);
                if (!strike.flash)
                {
                    BlackFlashGraphics.Plain(contact, strike.aim, age, map);
                    continue;
                }
                if (!strike.shaken && age >= F.SparkTime)
                {
                    strike.shaken = true;
                    Find.CameraDriver.shaker.DoShake(F.Shake);
                }
                if (age < F.After) BlackFlashGraphics.Hit(contact, strike.aim, age, seconds, BlackFlashGraphics.Negative.FullScreen, map);
                if (strike.target is Pawn && VfxDraw.Shown(strike.foe, map)) BlackFlashGraphics.StunStars(strike.foe, seconds, age);
                if (zone)
                {
                    Vector3 stands = strike.caster.DrawPos;
                    BlackFlashGraphics.Zone(new Vector2(stands.x, stands.z), age - F.SparkTime, seconds);
                }
            }
        }

        // The black sparks on the fist while a Black Flash is winding up. Only the player's pawns can
        // be Todo, and the fist is worked out as the sketch moves it: back, then driven in to the target.
        private void DrawWarmups()
        {
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                if (!(pawn.stances?.curStance is Stance_Warmup warmup) || !(warmup.verb is Verb_CastAbility verb)) continue;
                CompAbilityEffect_BlackFlash comp = verb.ability?.CompOfType<CompAbilityEffect_BlackFlash>();
                if (comp == null || !comp.InWindow(AnchorUtility.GeneOf(pawn))) continue;
                Thing target = warmup.focusTarg.Thing;
                if (target == null || !target.Spawned || target.Map != map) continue;
                int elapsed = now - warmup.startedTick, total = elapsed + warmup.ticksLeft;
                if (total <= 0) continue;
                float length = verb.ability.def.verbProperties.warmupTime, seconds = length * elapsed / total;
                Vector3 from = pawn.DrawPos, at = target.DrawPos;
                var todo = new Vector2(from.x, from.z);
                var aim = new Vector2(at.x - from.x, at.z - from.z);
                aim = aim.sqrMagnitude < 1e-6f ? Vector2.right : aim.normalized;
                Vector2 contact = F.Contact(new Vector2(at.x, at.z), aim);
                if (!VfxDraw.Shown(todo, map)) continue;
                BlackFlashGraphics.FistCrackle(F.Fist(todo, aim, contact, seconds, length), seconds, length);
            }
        }
    }
}
