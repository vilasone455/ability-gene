using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using static RimArt.UbwCommands;
using T = RimArt.UbwCommandTiming;

namespace RimArt
{
    /// <summary>One colonist taking a sword: which one, and when it starts to move.</summary>
    public sealed class UbwArming : IExposable
    {
        public Pawn pawn;
        public int seed, order, launch;
        /// <summary>The copy is in the hand; the record stays a moment longer for the glint.</summary>
        public bool given;

        public int Held => launch + Ticks(T.PullTime + T.ArcTime);

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref seed, "seed");
            Scribe_Values.Look(ref order, "order");
            Scribe_Values.Look(ref launch, "launch");
            Scribe_Values.Look(ref given, "given");
        }
    }

    /// <summary>
    /// Arm (a button on every other player colonist inside the world). The sword nearest the colonist slides up out of
    /// its hole 0.1 s after the order (swordCostSeconds of the world) and arcs to their hand in 0.4 s, no damage. Their
    /// weapon goes to their inventory (<see cref="WeaponStow"/>) and the sword is a traced copy of its weapon, Normal
    /// quality, default stuff, marked <see cref="TraceCopy.ubwArm"/>: the colonist has no Trace On, so the rule that
    /// breaks a copy held without it does not apply; the copy breaks when it leaves the hand or when the world closes
    /// (<see cref="BreakAll"/>, before anyone is moved home).
    /// </summary>
    public sealed class UbwArm : IExposable
    {
        public List<UbwArming> armings = new List<UbwArming>();
        /// <summary>Ticks the traced glint shows on the new copy (0.3 s).</summary>
        private const int Glint = 18;

        /// <summary>Why <paramref name="pawn"/> cannot take a sword now, or null.</summary>
        public string Refusal(UbwCast cast, Pawn pawn)
        {
            if (!cast.CommandsOpen) return "The world is closing.";
            if (pawn.Downed) return pawn.LabelShortCap + " is down.";
            if (pawn.equipment == null || pawn.WorkTagIsDisabled(WorkTags.Violent)) return pawn.LabelShortCap + " cannot use weapons.";
            EchoRecord forcing = EchoWeapon.Forcing(pawn);
            if (forcing != null) return EchoWeapon.Reason(forcing);
            if (armings.Exists(a => a.pawn == pawn && !a.given)) return "A sword is on its way.";
            return null;
        }

        public void Begin(UbwCast cast, Pawn pawn, int now)
        {
            MapComponent_UnlimitedBladeWorks inside = cast.Inside;
            if (inside == null || pawn == cast.caster || !cast.InWorld(pawn) || Refusal(cast, pawn) != null) return;
            UbwSword sw = inside.Field.Nearest(inside.Local(pawn.DrawPos));
            if (sw == null) return;
            inside.Field.Take(sw);
            cast.Spend(1);
            armings.Add(new UbwArming { pawn = pawn, seed = sw.Seed, order = now, launch = now + Ticks(T.Launch) });
        }

        public void Tick(UbwCast cast, int now)
        {
            MapComponent_UnlimitedBladeWorks inside = cast.Inside;
            for (int i = armings.Count - 1; i >= 0; i--)
            {
                UbwArming a = armings[i];
                if (a.given && now >= a.Held + Glint) armings.RemoveAt(i);
                if (a.given || now < a.Held) continue;
                a.given = true;
                UbwSword sw = inside?.Field.BySeed(a.seed);
                Pawn pawn = a.pawn;
                if (sw == null || !cast.InWorld(pawn) || pawn.Downed || pawn.equipment == null) continue;
                ThingDef def = UbwSwordHit.WeaponOf(sw.W);
                if (def == null) continue;
                WeaponStow.Stow(pawn);
                if (pawn.equipment.Primary != null) continue;
                TraceCopies.Give(pawn, TraceCopies.Make(def, QualityCategory.Normal), ubwArm: true);
            }
        }

        /// <summary>The world closes: every Arm copy held by a pawn in it breaks, and swords still on their way are lost.</summary>
        public void BreakAll(UbwCast cast)
        {
            armings.Clear();
            GameComponent_Trace trace = GameComponent_Trace.Instance;
            if (trace == null) return;
            foreach (TraceCopy copy in trace.ArmCopies())
                if (copy.holder == null || copy.holder.MapHeld == cast.world) TraceCopies.Break(copy.thing, false);
        }

        /// <summary>The order line from the colonist to the sword, the sword sliding up and arcing to the hand, the traced glint as it is held.</summary>
        internal void Draw(UbwCast cast, MapComponent_UnlimitedBladeWorks inside, in UbwCommandLook k)
        {
            int slot = 0;
            foreach (UbwArming a in armings)
            {
                UbwSword sw = inside.Field.BySeed(a.seed);
                if (sw == null || !cast.InWorld(a.pawn)) continue;
                string key = "ubw arm " + slot++;
                Vector2 m = inside.Local(a.pawn.DrawPos);
                var mid = new UbwXZ(m.x, m.y);
                double age = UbwClock.Since(a.launch);
                if (age < 0.15)
                {
                    float order = age < 0 ? VfxMath.Smooth(UbwClock.Since(a.order) / 0.1f) : 1f - VfxMath.Smooth((float)age / 0.15f);
                    UbwCommandGraphics.Order(k, mid, new UbwXZ(sw.X, sw.Z), 0.28, order);
                }
                if (!a.given)
                {
                    if (age < 0) UbwCommandGraphics.Standing(k, key, sw);
                    else UbwCommandGraphics.Arming(k, key, sw, age, mid);
                }
                else UbwCommandGraphics.Caught(k, new UbwXZ(mid.X + T.Hand.X, mid.Z + T.Hand.Z), UbwClock.Since(a.Held));
            }
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref armings, "armings", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (armings == null) armings = new List<UbwArming>();
                armings.RemoveAll(a => a?.pawn == null);
            }
        }
    }
}
