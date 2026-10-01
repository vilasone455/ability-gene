using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using static RimArt.UbwCommands;
using T = RimArt.UbwCommandTiming;

namespace RimArt
{
    /// <summary>One drawn sword: torn out of its hole and flying to the caster's hand, and the pawns it has cut.</summary>
    public sealed class UbwDrawFlight : IExposable
    {
        public int seed, order, launch, flying;
        /// <summary>Where it left the ground, cells from the world's middle corner.</summary>
        public Vector2 start;
        /// <summary>The caster's hand when the sword was last placed (the end of its line if the caster cannot catch it).</summary>
        public Vector2 hand;
        /// <summary>Set when the flight ended: caught, or broken into light because the caster could not catch it.</summary>
        public int ended = int.MinValue;
        public bool caught;
        public List<Pawn> cut = new List<Pawn>();
        /// <summary>The tick of the last cut, for its spark (not saved).</summary>
        public int lastCut = int.MinValue;
        public Vector2 lastCutAt;

        public void ExposeData()
        {
            Scribe_Values.Look(ref seed, "seed");
            Scribe_Values.Look(ref order, "order");
            Scribe_Values.Look(ref launch, "launch");
            Scribe_Values.Look(ref flying, "flying");
            Scribe_Values.Look(ref start, "start");
            Scribe_Values.Look(ref hand, "hand");
            Scribe_Values.Look(ref ended, "ended", int.MinValue);
            Scribe_Values.Look(ref caught, "caught");
            if (Scribe.mode == LoadSaveMode.Saving) cut.RemoveAll(p => p == null || p.Destroyed);
            Scribe_Collections.Look(ref cut, "cut", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (cut == null) cut = new List<Pawn>();
                cut.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>
    /// Draw (the caster's command). The sword nearest the clicked cell within drawPickRadius tears out of its hole 0.1 s
    /// after the order (swordCostSeconds of the world), turns flat in 0.1 s and flies to the caster's hand at drawSpeed,
    /// following the caster if it moves. Every pawn hostile to the caster within half the blade's length of its path
    /// (W.Length x W.Image x Size / 2, about 0.7 cells for a longsword) takes one hit of that sword's weapon, once. The
    /// caster catches it as a traced copy (<see cref="TraceCopies.Give"/>): a real weapon in hand goes to the inventory, a
    /// copy in hand breaks (<see cref="TraceCopies.ClearHands"/>). The catch is a Trace On copy: it stays in his hand after
    /// the world closes and breaks when it leaves it. Its quality follows Trace On's rule if the weapon is in his library
    /// (qualityBelow under the best studied), else Normal; default stuff. A caster who cannot catch it (down, gone) lets
    /// it break into light where it is.
    /// </summary>
    public sealed class UbwDraw : IExposable
    {
        public List<UbwDrawFlight> flights = new List<UbwDrawFlight>();

        /// <summary>The usable sword the Draw button would take for a click at <paramref name="cell"/>, or null.</summary>
        public static UbwSword PickAt(UbwCast cast, IntVec3 cell)
        {
            MapComponent_UnlimitedBladeWorks inside = cast.Inside;
            if (inside == null || !cell.InBounds(cast.world)) return null;
            return inside.Field.Nearest(inside.Local(cell.ToVector3Shifted()), UbwRules.Of.drawPickRadius);
        }

        public void Begin(UbwCast cast, UbwSword sw, int now)
        {
            MapComponent_UnlimitedBladeWorks inside = cast.Inside;
            if (inside == null || !cast.CommandsOpen || !UbwFieldState.Usable(sw)) return;
            inside.Field.Take(sw);
            cast.Spend(1);
            int launch = now + Ticks(T.Launch);
            var start = new Vector2((float)sw.X, (float)sw.Z);
            flights.Add(new UbwDrawFlight { seed = sw.Seed, order = now, launch = launch, flying = launch + Ticks(T.TearTime), start = start, hand = HandOf(cast, inside, start) });
        }

        /// <summary>The caster's weapon hand, cells from the world's middle corner.</summary>
        public static Vector2 HandOf(UbwCast cast, MapComponent_UnlimitedBladeWorks inside, Vector2 otherwise)
        {
            Pawn caster = cast.caster;
            if (!cast.InWorld(caster)) return otherwise;
            return inside.Local(caster.DrawPos) + new Vector2((float)T.Hand.X, (float)T.Hand.Z);
        }

        /// <summary>Share of the way to the hand <paramref name="seconds"/> into the flight, at <paramref name="speed"/> cells a second over <paramref name="length"/> cells.</summary>
        public static float Share(float seconds, float speed, float length) => length <= 1e-3f ? 1f : Mathf.Clamp01(seconds * speed / length);

        public void Tick(UbwCast cast, int now)
        {
            MapComponent_UnlimitedBladeWorks inside = cast.Inside;
            if (inside == null) return;
            UbwRules rules = UbwRules.Of;
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                UbwDrawFlight f = flights[i];
                if (f.ended != int.MinValue)
                {
                    if (now - f.ended > Ticks(T.Shatter + 0.6)) flights.RemoveAt(i);
                    continue;
                }
                if (now < f.flying) continue;
                UbwSword sw = inside.Field.BySeed(f.seed);
                if (sw == null)
                {
                    flights.RemoveAt(i);
                    continue;
                }
                bool canCatch = cast.InWorld(cast.caster) && !cast.caster.Downed;
                if (canCatch) f.hand = HandOf(cast, inside, f.hand);
                Vector2 line = f.hand - f.start;
                float length = line.magnitude;
                float before = Share((now - 1 - f.flying) / 60f, rules.drawSpeed, length), after = Share((now - f.flying) / 60f, rules.drawSpeed, length);
                Cut(cast, inside, f, sw, f.start + line * before, f.start + line * after);
                if (after < 1f) continue;
                f.ended = now;
                if (!canCatch) continue;
                Catch(cast.caster, sw);
                f.caught = true;
            }
        }

        /// <summary>Every hostile pawn within half the blade's length of the stretch flown this tick takes one hit, once per flight.</summary>
        private static void Cut(UbwCast cast, MapComponent_UnlimitedBladeWorks inside, UbwDrawFlight f, UbwSword sw, Vector2 a, Vector2 b)
        {
            float reach = (float)(sw.W.Length * sw.W.Image * sw.Size / 2);
            Vector2 heading = b - a;
            foreach (Pawn p in cast.world.mapPawns.AllPawnsSpawned.ToList())
            {
                if (p == cast.caster || p.Dead || f.cut.Contains(p) || !p.HostileTo(cast.caster)) continue;
                if (DistanceToSegment(inside.Local(p.DrawPos), a, b) > reach) continue;
                f.cut.Add(p);
                f.lastCut = Find.TickManager.TicksGame;
                f.lastCutAt = inside.Local(p.DrawPos);
                UbwSwordHit.Strike(cast.caster, p, UbwSwordHit.WeaponOf(sw.W), heading);
            }
        }

        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float l = ab.sqrMagnitude, t = l < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / l);
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>The caster takes the sword in hand as a traced copy of its weapon.</summary>
        private static void Catch(Pawn caster, UbwSword sw)
        {
            ThingDef def = UbwSwordHit.WeaponOf(sw.W);
            if (def == null || caster.equipment == null) return;
            TraceCopies.ClearHands(caster);
            // A hand that could not be emptied (no inventory, not spawned) keeps its weapon; AddEquipment would refuse.
            if (caster.equipment.Primary != null) return;
            TraceCopies.Give(caster, TraceCopies.Make(def, QualityOf(caster, def)));
        }

        /// <summary>Trace On's quality rule if the weapon is in the caster's library (its best entry of any stuff), else Normal.</summary>
        public static QualityCategory QualityOf(Pawn caster, ThingDef def)
        {
            TraceLibraryEntry best = null;
            foreach (TraceLibraryEntry e in TraceLibrary.Of(caster))
                if (e.blade == def.defName && (best == null || e.best > best.best)) best = e;
            return best != null ? TraceLibrary.CopyQuality(best, CompProperties_TraceOn.QualityBelow) : QualityCategory.Normal;
        }

        /// <summary>The targeter's highlight for the Draw button: the sword a click there would take, and the line from the caster to it.</summary>
        internal static void Highlight(UbwCast cast, LocalTargetInfo target)
        {
            MapComponent_UnlimitedBladeWorks inside = cast.Inside;
            if (inside == null || !target.IsValid || Find.CurrentMap != cast.world || !cast.InWorld(cast.caster)) return;
            UbwSword sw = PickAt(cast, target.Cell);
            if (sw == null) return;
            UbwCommandLook k = UbwCommandLook.For(inside.Origin, inside.Sun, MapComponent_UnlimitedBladeWorks.ShadowStrength);
            UbwCommandGraphics.Order(k, XZ(inside.Local(cast.caster.DrawPos)), new UbwXZ(sw.X, sw.Z), 0.32, 1f);
        }

        /// <summary>The order line, the lane the spinning blade covers, the sword tearing out and flying, the catch or the break, the cut sparks.</summary>
        internal void Draw(UbwCast cast, MapComponent_UnlimitedBladeWorks inside, in UbwCommandLook k)
        {
            float speed = UbwRules.Of.drawSpeed;
            int slot = 0;
            foreach (UbwDrawFlight f in flights)
            {
                UbwSword sw = inside.Field.BySeed(f.seed);
                if (sw == null) continue;
                string key = "ubw draw " + slot++;
                bool open = f.ended == int.MinValue;
                Vector2 hand2 = open ? HandOf(cast, inside, f.hand) : f.hand;
                UbwXZ hand = XZ(hand2), start = XZ(f.start);
                var from3 = new UbwV3(f.start.x, T.DrawHeight, f.start.y);
                var to3 = new UbwV3(hand.X, T.HandHeight, hand.Z);
                float length = (hand2 - f.start).magnitude, flying = UbwClock.Since(f.flying);
                double t = UbwClock.Since(f.launch), sinceOrder = UbwClock.Since(f.order);

                float lane = open ? VfxMath.Smooth((float)sinceOrder / 0.1f) : 1f - VfxMath.Smooth(UbwClock.Since(f.ended) / 0.25f);
                UbwCommandGraphics.Lane(k, start, hand, sw.W.Length * sw.W.Image * sw.Size / 2, lane);
                if (t < 0.15)
                {
                    float order = t < 0 ? VfxMath.Smooth((float)sinceOrder / 0.1f) : 1f - VfxMath.Smooth((float)t / 0.15f);
                    if (cast.InWorld(cast.caster)) UbwCommandGraphics.Order(k, XZ(inside.Local(cast.caster.DrawPos)), start, 0.32, order);
                }
                if (open)
                {
                    if (t < 0) UbwCommandGraphics.Standing(k, key, sw);
                    else UbwCommandGraphics.Drawing(k, key, sw, t, from3, to3, Share(flying, speed, length), lag => Share(flying - (float)lag, speed, length));
                }
                else if (f.caught) UbwCommandGraphics.Caught(k, hand, UbwClock.Since(f.ended));
                else UbwCommandGraphics.Shatter(k, key, T.Spinning(sw, from3, to3, 1, flying), UbwClock.Since(f.ended) / T.Shatter, sw.Seed, VfxDraw.Overhead + 0.01f);
                if (f.lastCut != int.MinValue)
                    UbwCommandGraphics.Spark(k, new UbwV3(f.lastCutAt.x, T.HitHeight + 0.1, f.lastCutAt.y), UbwClock.Since(f.lastCut), 0.15, 0.34f, Color.white);
            }
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref flights, "flights", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && flights == null) flights = new List<UbwDrawFlight>();
        }
    }
}
