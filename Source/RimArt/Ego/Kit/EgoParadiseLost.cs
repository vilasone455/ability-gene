using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Paradise Lost's rules (docs/ego-weapons.md, Weapon 4): the room hit, WhiteNight's ring, the slow and Sanity. The damage
    /// is AG_EgoPale, which has no armour category, so armour takes nothing off it (the doc's reading of Pale), and it is not
    /// ranged, so a shield belt does not stop it.
    /// </summary>
    public static class EgoParadiseLost
    {
        private static readonly List<Thing> struck = new List<Thing>();
        private static readonly List<Pawn> ringed = new List<Pawn>();

        /// <summary>Outdoors for the room hit: no room, or the room touches the map edge (the open ground round every building).</summary>
        public static bool Outdoors(Room room) => room == null || room.TouchesMapEdge;

        /// <summary>
        /// Who one room hit at <paramref name="aimed"/> strikes, into <paramref name="into"/>, the aimed thing first: every other
        /// hostile of the wielder's that is not downed and stands in the aimed thing's room; outdoors, in that same outdoor
        /// room and within outdoorRadius cells of the aimed thing, so a wall still keeps them out. A thing with no room (a
        /// wall) is struck alone. The aimed thing is struck whatever it is: the player chose it.
        /// </summary>
        public static List<Thing> Targets(Pawn wielder, Thing aimed, CompProperties_EgoParadiseLost p, List<Thing> into)
        {
            into.Clear();
            into.Add(aimed);
            Room room = aimed.GetRoom();
            if (room == null) return into;
            bool outdoors = Outdoors(room);
            IReadOnlyList<Pawn> pawns = aimed.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == aimed || pawn == wielder || pawn.Dead || pawn.Downed || !pawn.HostileTo(wielder) || pawn.GetRoom() != room) continue;
                if (outdoors && !pawn.Position.InHorDistOf(aimed.Position, p.outdoorRadius)) continue;
                into.Add(pawn);
            }
            return into;
        }

        /// <summary>
        /// Damage to each of <paramref name="count"/> things struck by one room hit: the round's damage to one, damageFew each
        /// to 2 to fewMax, damageMany each to more. Each is times the weapon's ranged damage multiplier.
        /// </summary>
        public static float Damage(CompEgoParadiseLost staff, ThingDef round, int count)
        {
            if (count <= 1) return round.projectile.GetDamageAmount(staff.parent);
            CompProperties_EgoParadiseLost p = staff.Props;
            return (count <= p.fewMax ? p.damageFew : p.damageMany) * staff.parent.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier);
        }

        /// <summary>
        /// One shot at <paramref name="aimed"/>: every thing <see cref="Targets"/> finds takes <see cref="Damage"/> and every
        /// pawn among them is slowed; the wielder gains Sanity for each hostile struck. Returns how many things were struck.
        /// </summary>
        public static int RoomHit(Pawn wielder, CompEgoParadiseLost staff, ThingDef round, Thing aimed)
        {
            CompProperties_EgoParadiseLost p = staff.Props;
            Targets(wielder, aimed, p, struck);
            float amount = Damage(staff, round, struck.Count);
            int hostiles = 0;
            for (int i = 0; i < struck.Count; i++)
                if (struck[i] is Pawn pawn && pawn.HostileTo(wielder)) hostiles++;
            // The picture first: a pawn the damage kills leaves the map.
            GameComponent_EgoParadiseLost.Instance?.Shot(wielder, struck);
            Vector3 from = wielder.DrawPos;
            for (int i = 0; i < struck.Count; i++)
            {
                Thing victim = struck[i];
                if (victim.Destroyed) continue;
                Vector3 d = victim.DrawPos - from;
                EgoRound.Hit(wielder, staff.parent, round, victim, aimed, amount, new Vector2(d.x, d.z));
                if (victim is Pawn pawn) Slow(pawn, p.slowSeconds);
            }
            GainSanity(wielder, p, hostiles);
            int count = struck.Count;
            struck.Clear();
            return count;
        }

        /// <summary>
        /// One firing of WhiteNight's ring <paramref name="radius"/> cells round the wielder, through walls: every other living
        /// pawn in it takes ringDamage (times the ranged damage multiplier) and is slowed. Corroded it strikes every faction,
        /// downed pawns too; with <paramref name="hostilesOnly"/> (Overclock) only hostiles that are standing. No Sanity.
        /// Returns how many pawns it struck.
        /// </summary>
        public static int Ring(Pawn wielder, CompEgoParadiseLost staff, float radius, bool hostilesOnly)
        {
            CompProperties_EgoParadiseLost p = staff.Props;
            ringed.Clear();
            IReadOnlyList<Pawn> pawns = wielder.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == wielder || pawn.Dead || !pawn.Position.InHorDistOf(wielder.Position, radius)) continue;
                if (hostilesOnly && (pawn.Downed || !pawn.HostileTo(wielder))) continue;
                ringed.Add(pawn);
            }
            GameComponent_EgoParadiseLost.Instance?.Ring(wielder, radius, ringed, p.ringSound);
            ThingDef round = staff.PrimaryVerb.verbProps.defaultProjectile;
            float amount = p.ringDamage * staff.parent.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier);
            Vector3 from = wielder.DrawPos;
            // Copied first: a pawn the ring kills leaves the map's list.
            for (int i = 0; i < ringed.Count; i++)
            {
                Pawn pawn = ringed[i];
                if (pawn.Dead) continue;
                Vector3 d = pawn.DrawPos - from;
                EgoRound.Hit(wielder, staff.parent, round, pawn, pawn, amount, new Vector2(d.x, d.z));
                Slow(pawn, p.slowSeconds);
            }
            int count = ringed.Count;
            ringed.Clear();
            return count;
        }

        /// <summary>
        /// AG_EgoParadiseLostSlow for <paramref name="seconds"/>: a new one is set to exactly that (its def's time is a
        /// fallback), and a pawn still slowed keeps the longer of the two.
        /// </summary>
        public static void Slow(Pawn pawn, float seconds)
        {
            if (seconds <= 0f || pawn.Dead || pawn.health == null) return;
            int ticks = seconds.SecondsToTicks();
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(EgoDefOf.AG_EgoParadiseLostSlow);
            bool fresh = hediff == null;
            if (fresh) hediff = pawn.health.AddHediff(EgoDefOf.AG_EgoParadiseLostSlow);
            HediffComp_Disappears timer = hediff.TryGetComp<HediffComp_Disappears>();
            timer?.SetDuration(fresh ? ticks : Math.Max(timer.ticksToDisappear, ticks));
        }

        public static bool Slowed(Pawn pawn) => pawn?.health?.hediffSet.HasHediff(EgoDefOf.AG_EgoParadiseLostSlow) == true;

        /// <summary>The mood the wielder's Sanity memory gives now, 0 without one.</summary>
        public static int Sanity(Pawn pawn) =>
            (pawn?.needs?.mood?.thoughts.memories.GetFirstMemoryOfDef(EgoDefOf.AG_EgoParadiseLostSanity) as Thought_Memory)?.moodOffset ?? 0;

        /// <summary>
        /// Sanity for <paramref name="hostiles"/> hostiles struck: one AG_EgoParadiseLostSanity memory whose mood goes up
        /// sanityPerHit for each, never past sanityCap, and which lasts sanityHours from this hit. One memory, not stacked
        /// ones, so the cap is the weapon's number and the mood tab shows one line.
        /// </summary>
        public static void GainSanity(Pawn wielder, CompProperties_EgoParadiseLost p, int hostiles)
        {
            if (hostiles <= 0 || p.sanityPerHit <= 0 || wielder.needs?.mood == null) return;
            MemoryThoughtHandler memories = wielder.needs.mood.thoughts.memories;
            ThoughtDef def = EgoDefOf.AG_EgoParadiseLostSanity;
            int ticks = Mathf.RoundToInt(p.sanityHours * GenDate.TicksPerHour);
            var thought = memories.GetFirstMemoryOfDef(def) as Thought_Memory;
            int mood = Math.Min(p.sanityCap, (thought?.moodOffset ?? 0) + p.sanityPerHit * hostiles);
            if (thought != null)
            {
                thought.moodOffset = mood;
                thought.durationTicksOverride = ticks;
                thought.Renew();
                return;
            }
            thought = (Thought_Memory)ThoughtMaker.MakeThought(def);
            thought.moodOffset = mood;
            thought.durationTicksOverride = ticks;
            memories.TryGainMemory(thought);
        }
    }
}
