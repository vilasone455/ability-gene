using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    public class CompProperties_RaikoKusari : CompProperties_AbilityEffect
    {
        /// <summary>Held weapons needed within <see cref="reach"/> of Sasuke.</summary>
        public int minWeapons = 2;
        public float reach = 12f;
        /// <summary>Longest link. A link that stretches past it snaps.</summary>
        public float maxLink = 6f;
        public int durationTicks = 480;
        /// <summary>Damage each caught pawn takes every <see cref="burnIntervalTicks"/>.</summary>
        public float burnDamage = 3f;
        public int burnIntervalTicks = 60;
        public DamageDef damageDef;
        /// <summary>Mechanoids stay stunned this long after the net ends (EMP).</summary>
        public int mechStunTicks = 180;
        /// <summary>A charged weapon let go out of the net stuns what it hits this long.</summary>
        public int letGoStunTicks = 120;

        public CompProperties_RaikoKusari()
        {
            compClass = typeof(CompAbilityEffect_RaikoKusari);
        }
    }

    /// <summary>
    /// Raikō Kusari (rinnegan-raiko-kusari.js header): a Chidori strung through the weapons Amenoyodomi holds. The
    /// warmup is the charge in the hand; the net forms on the fire tick (<see cref="RaikoNet"/>).
    /// </summary>
    public class CompAbilityEffect_RaikoKusari : CompAbilityEffect
    {
        public new CompProperties_RaikoKusari Props => (CompProperties_RaikoKusari)props;

        public override bool GizmoDisabled(out string reason)
        {
            reason = Unavailable(parent.pawn, Props);
            return reason != null || base.GizmoDisabled(out reason);
        }

        /// <summary>Why no net can form now, or null.</summary>
        public static string Unavailable(Pawn caster, CompProperties_RaikoKusari props)
        {
            List<HeldWeapon> corners = RaikoNet.Corners(caster, GameComponent_Rinnegan.Instance?.HeldBy(caster), props);
            if (corners.Count < props.minWeapons)
                return "Needs " + props.minWeapons + " weapons held by Amenoyodomi within " + props.reach + " cells.";
            for (int i = 0; i + 1 < corners.Count; i++)
                if ((corners[i].at - corners[i + 1].at).magnitude <= props.maxLink) return null;
            return "The held weapons are more than " + props.maxLink + " cells apart.";
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (GameComponent_Rinnegan.Instance?.StartNet(parent.pawn) != true)
                Messages.Message(Unavailable(parent.pawn, Props) ?? "The net could not form.", parent.pawn, MessageTypeDefOf.RejectInput, false);
        }
    }

    /// <summary>
    /// One Raikō Kusari net. Its corners are held weapons in throw order; link k joins corner k and k + 1, and a ring
    /// adds a link from the last back to the first. Every tick each live link checks its length (past maxLink it snaps)
    /// and the cells its line crosses: a pawn in one is caught, never Sasuke. Caught pawns are stunned until the net
    /// ends and burn every second; a shield belt on a caught pawn breaks. The net ends when its time is up, when its
    /// last link is gone, or on Let go.
    /// </summary>
    public class RaikoNet : IExposable
    {
        private static readonly System.Reflection.MethodInfo BreakShield = AccessTools.Method(typeof(CompShield), "Break");

        public Pawn caster;
        public Map map;
        /// <summary>The held weapons, by projectile (a HeldWeapon is not a saved reference).</summary>
        public List<Projectile> corners = new List<Projectile>();
        public List<bool> alive = new List<bool>();
        public bool ring;
        public int startTick;
        public int endTick;
        public List<Pawn> caught = new List<Pawn>();
        public List<int> caughtTicks = new List<int>();

        // For the picture only, not saved (a loaded net draws its lines as if they had just formed):
        /// <summary>Links too long when the run reached them: the bolt fizzles a third of the way.</summary>
        public List<bool> failed = new List<bool>();
        /// <summary>Per link: the tick it ended (-1 while it lives), its ends then, and whether it was cut (a snap or a let-go).</summary>
        public List<int> linkEnds = new List<int>();
        public List<Vector3> endA = new List<Vector3>(), endB = new List<Vector3>();
        public List<bool> cut = new List<bool>();
        /// <summary>Per caught pawn: the link it was caught on and where it stood (its DrawPos), for the scorch.</summary>
        public List<int> caughtLinks = new List<int>();
        public List<Vector3> caughtAt = new List<Vector3>();
        /// <summary>The tick the whole net ended, -1 while it holds.</summary>
        public int endedTick = -1;

        private static CompProperties_RaikoKusari Props => SasukeKit.NetProps;

        public int LinkCount => corners.Count - 1 + (ring ? 1 : 0);
        public int LinkFrom(int k) => k;
        public int LinkTo(int k) => (k + 1) % corners.Count;

        /// <summary>The weapons a net can use: the caster's held weapons within reach, in throw order.</summary>
        public static List<HeldWeapon> Corners(Pawn caster, List<HeldWeapon> held, CompProperties_RaikoKusari props)
        {
            if (held == null || caster == null || !caster.Spawned) return new List<HeldWeapon>();
            Vector3 from = SasukeKit.Flat(caster.DrawPos);
            return held.Where(w => w.shot.Map == caster.Map && (w.at - from).magnitude <= props.reach).OrderBy(w => w.order).ToList();
        }

        public static RaikoNet Form(Pawn caster, List<HeldWeapon> held, int now)
        {
            CompProperties_RaikoKusari props = Props;
            List<HeldWeapon> weapons = Corners(caster, held, props);
            if (weapons.Count < props.minWeapons) return null;
            var net = new RaikoNet
            {
                caster = caster,
                map = caster.Map,
                startTick = now,
                endTick = now + props.durationTicks
            };
            foreach (HeldWeapon w in weapons) net.corners.Add(w.shot);
            int n = weapons.Count;
            net.ring = n >= 3 && (weapons[n - 1].at - weapons[0].at).magnitude <= props.maxLink;
            bool any = false;
            for (int k = 0; k < net.LinkCount; k++)
            {
                bool ok = (weapons[net.LinkFrom(k)].at - weapons[net.LinkTo(k)].at).magnitude <= props.maxLink;
                net.alive.Add(ok);
                net.failed.Add(!ok);
                any |= ok;
            }
            net.EnsurePictureLists();
            return any ? net : null;
        }

        public HeldWeapon Corner(int i) => GameComponent_Rinnegan.Instance?.HeldFor(corners[i]);

        private void EnsurePictureLists()
        {
            while (failed.Count < LinkCount) failed.Add(false);
            while (linkEnds.Count < LinkCount) linkEnds.Add(-1);
            while (endA.Count < LinkCount) endA.Add(Vector3.zero);
            while (endB.Count < LinkCount) endB.Add(Vector3.zero);
            while (cut.Count < LinkCount) cut.Add(false);
            while (caughtLinks.Count < caught.Count) caughtLinks.Add(0);
            while (caughtAt.Count < caught.Count) caughtAt.Add(caught[caughtAt.Count]?.DrawPos ?? Vector3.zero);
        }

        /// <summary>Link k ends now: remember where its ends were for the break.</summary>
        private void EndLink(int k, int now, bool wasCut, HeldWeapon a, HeldWeapon b)
        {
            if (k >= linkEnds.Count || linkEnds[k] >= 0) return;
            linkEnds[k] = now;
            if (a != null) endA[k] = a.at;
            if (b != null) endB[k] = b.at;
            cut[k] = wasCut;
        }

        /// <summary>The two ends of link k now, or false when it is gone.</summary>
        public bool Link(int k, out Vector3 a, out Vector3 b)
        {
            a = b = Vector3.zero;
            if (k >= alive.Count || !alive[k]) return false;
            HeldWeapon from = Corner(LinkFrom(k)), to = Corner(LinkTo(k));
            if (from == null || to == null) return false;
            a = from.at;
            b = to.at;
            return true;
        }

        public bool Caught(Pawn pawn) => caught.Contains(pawn);

        /// <summary>One game tick. False once the net is over.</summary>
        public bool Tick(int now)
        {
            CompProperties_RaikoKusari props = Props;
            if (now >= endTick || caster == null || caster.Dead || SasukeKit.Amenoyodomi(caster) == null)
            {
                End(now, false);
                return false;
            }
            EnsurePictureLists();
            bool any = false;
            for (int k = 0; k < alive.Count; k++)
            {
                if (!alive[k]) continue;
                HeldWeapon a = Corner(LinkFrom(k)), b = Corner(LinkTo(k));
                if (a == null || b == null || (a.at - b.at).magnitude > props.maxLink)
                {
                    alive[k] = false;
                    EndLink(k, now, true, a, b);
                    RinneganPictures.LinkSnapped(this, k, a, b);
                    continue;
                }
                any = true;
                // The Chidori runs down the chain from the leap, 0.05 s a link: a line catches once it is lit.
                if (now - startTick >= Mathf.CeilToInt(RaikoKusariTiming.LinkLit(k) * 60f)) CatchOn(k, a.at, b.at, now);
            }
            if (!any)
            {
                End(now, false);
                return false;
            }
            for (int i = caught.Count - 1; i >= 0; i--)
            {
                Pawn pawn = caught[i];
                if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map != map)
                {
                    caught.RemoveAt(i);
                    caughtTicks.RemoveAt(i);
                    if (i < caughtLinks.Count) caughtLinks.RemoveAt(i);
                    if (i < caughtAt.Count) caughtAt.RemoveAt(i);
                    continue;
                }
                pawn.stances?.stunner?.StunFor(endTick - now + 1, caster, false, true);
                if ((now - caughtTicks[i]) % props.burnIntervalTicks == props.burnIntervalTicks - 1)
                    pawn.TakeDamage(new DamageInfo(props.damageDef ?? DamageDefOf.Burn, props.burnDamage, 0f, -1f, caster));
            }
            return true;
        }

        /// <summary>The cells the line from a to b crosses (the Fūma's exact line, without its corner guards).</summary>
        public static IEnumerable<IntVec3> Cells(Vector3 a, Vector3 b)
        {
            foreach (FumaRules.Cell step in FumaRules.Trace(a.x, a.z, b.x, b.z))
                if (!step.Guard) yield return new IntVec3(step.X, 0, step.Z);
        }

        private void CatchOn(int link, Vector3 a, Vector3 b, int now)
        {
            foreach (IntVec3 cell in Cells(a, b))
            {
                if (!cell.InBounds(map)) continue;
                List<Thing> things = cell.GetThingList(map);
                for (int i = things.Count - 1; i >= 0; i--)
                {
                    if (!(things[i] is Pawn pawn) || pawn == caster || pawn.Dead || caught.Contains(pawn)) continue;
                    Catch(pawn, now, link);
                }
            }
        }

        private void Catch(Pawn pawn, int now, int link)
        {
            caught.Add(pawn);
            caughtTicks.Add(now);
            caughtLinks.Add(link);
            caughtAt.Add(pawn.DrawPos);
            List<Apparel> worn = pawn.apparel?.WornApparel;
            if (worn != null)
                for (int i = 0; i < worn.Count; i++)
                {
                    CompShield shield = worn[i].TryGetComp<CompShield>();
                    if (shield != null && shield.ShieldState == ShieldState.Active) BreakShield?.Invoke(shield, null);
                }
            pawn.stances?.stunner?.StunFor(endTick - now + 1, caster, false, true);
            RinneganPictures.PawnCaught(this, pawn);
        }

        /// <summary>
        /// The net is over: everyone caught is freed, except mechanoids, which stay stunned a while (EMP). On a let-go
        /// the weapons leave charged (the caller releases them).
        /// </summary>
        public void End(int now, bool letGo)
        {
            CompProperties_RaikoKusari props = Props;
            EnsurePictureLists();
            endedTick = now;
            for (int k = 0; k < alive.Count; k++)
                if (alive[k]) EndLink(k, now, letGo, Corner(LinkFrom(k)), Corner(LinkTo(k)));
            foreach (Pawn pawn in caught)
            {
                if (pawn == null || pawn.Dead || pawn.stances?.stunner == null) continue;
                if (pawn.RaceProps.IsMechanoid)
                {
                    pawn.stances.stunner.StopStun();
                    pawn.stances.stunner.StunFor(props.mechStunTicks, caster, false, true);
                }
                else pawn.stances.stunner.StopStun();
            }
            RinneganPictures.NetEnded(this, now, letGo);
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref caster, "caster");
            Scribe_References.Look(ref map, "map");
            Scribe_Collections.Look(ref corners, "corners", LookMode.Reference);
            Scribe_Collections.Look(ref alive, "alive", LookMode.Value);
            Scribe_Values.Look(ref ring, "ring");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref endTick, "endTick");
            Scribe_Collections.Look(ref caught, "caught", LookMode.Reference);
            Scribe_Collections.Look(ref caughtTicks, "caughtTicks", LookMode.Value);
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            corners ??= new List<Projectile>();
            alive ??= new List<bool>();
            caught ??= new List<Pawn>();
            caughtTicks ??= new List<int>();
            while (alive.Count < LinkCount) alive.Add(false);
            if (caughtTicks.Count != caught.Count)
            {
                caught.Clear();
                caughtTicks.Clear();
            }
            EnsurePictureLists();
        }
    }
}
