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
        private static readonly List<Room> hitRooms = new List<Room>(), pawnRooms = new List<Room>();

        /// <summary>
        /// Outdoors for the room hit: no room, a room that touches the map edge (the open ground round every building), or
        /// one Core counts as psychologically outdoors (300 or more unroofed cells), so a walled, unroofed base is not one
        /// room the hit fills from wall to wall.
        /// </summary>
        public static bool Outdoors(Room room) => room == null || room.TouchesMapEdge || room.PsychologicallyOutdoors;

        /// <summary>
        /// The rooms <paramref name="thing"/> stands in, into <paramref name="into"/>: its own, or for one in a doorway (a door
        /// cell is a room of its own) every room the door joins. None for a thing with no region (a wall).
        /// </summary>
        public static List<Room> RoomsOf(Thing thing, List<Room> into)
        {
            into.Clear();
            Region region = thing.GetRegion(RegionType.Set_All);
            if (region == null) return into;
            if (region.type != RegionType.Portal)
            {
                if (region.Room != null) into.Add(region.Room);
                return into;
            }
            foreach (Region next in region.Neighbors)
                if (next.Room != null && !into.Contains(next.Room)) into.Add(next.Room);
            return into;
        }

        /// <summary>
        /// Who one room hit at <paramref name="aimed"/> strikes, into <paramref name="into"/>, the aimed thing first: every other
        /// hostile of the wielder's that is not downed and stands in the aimed thing's room; outdoors, in that same outdoor
        /// room and within outdoorRadius cells of the aimed thing, so a wall still keeps them out. A pawn in a doorway stands
        /// in every room the door joins (<see cref="RoomsOf"/>), and so does an aimed thing in one. A thing with no room (a
        /// wall) is struck alone. The aimed thing is struck whatever it is: the player chose it.
        /// </summary>
        public static List<Thing> Targets(Pawn wielder, Thing aimed, CompProperties_EgoParadiseLost p, List<Thing> into)
        {
            into.Clear();
            into.Add(aimed);
            if (RoomsOf(aimed, hitRooms).Count == 0) return into;
            IReadOnlyList<Pawn> pawns = aimed.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == aimed || pawn == wielder || pawn.Dead || pawn.Downed || !pawn.HostileTo(wielder)) continue;
                if (InHitRooms(pawn, aimed, p.outdoorRadius)) into.Add(pawn);
            }
            return into;
        }

        /// <summary>Whether <paramref name="pawn"/> stands in one of the hit's rooms: anywhere in one indoors, within <paramref name="radius"/> cells of the aimed thing outdoors.</summary>
        private static bool InHitRooms(Pawn pawn, Thing aimed, float radius)
        {
            RoomsOf(pawn, pawnRooms);
            for (int i = 0; i < pawnRooms.Count; i++)
                if (hitRooms.Contains(pawnRooms[i]) && (!Outdoors(pawnRooms[i]) || pawn.Position.InHorDistOf(aimed.Position, radius))) return true;
            return false;
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
            EgoCorrosion.PawnsAround(wielder, radius, hostilesOnly, ringed);
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
            EgoCorrosion.SetTimedHediff(pawn, EgoDefOf.AG_EgoParadiseLostSlow, seconds.SecondsToTicks());
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
            ThoughtDef def = EgoDefOf.AG_EgoParadiseLostSanity;
            Thought_Memory thought = wielder.needs.mood.thoughts.memories.GetFirstMemoryOfDef(def);
            int mood = Math.Min(p.sanityCap, (thought?.moodOffset ?? 0) + p.sanityPerHit * hostiles);
            EgoCorrosion.SetMemory(wielder, def, thought, mood, Mathf.RoundToInt(p.sanityHours * GenDate.TicksPerHour));
        }
    }
}
