using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using static RimArt.CrossMapMove;
using Open = RimArt.UnlimitedVoidOpenTiming;
using Inside = RimArt.UnlimitedVoidInsideTiming;

namespace RimArt
{
    /// <summary>One pawn taken into the domain: where it stood, its lord, and what the domain is doing to it.</summary>
    public sealed class VoidTaken : IExposable
    {
        public Pawn pawn;
        /// <summary>The home-map cell it was taken from.</summary>
        public IntVec3 from;
        public Lord lord;
        /// <summary>Has a living brain: frozen for the whole domain and overloaded unless spared.</summary>
        public bool frozen;
        /// <summary>The overload it already had from an earlier domain when it was taken.</summary>
        public float startOverload;
        /// <summary>Ticks it has stood next to Gojo, added up.</summary>
        public int touchTicks;
        /// <summary>The tick it was spared, or -1.</summary>
        public int sparedTick = -1;

        public bool Spared => sparedTick >= 0;

        public void ExposeData()
        {
            // A lord that has ended is saved nowhere, and a reference to it would not resolve on load (as UbwTaken).
            if (Scribe.mode == LoadSaveMode.Saving && lord != null && (lord.Map == null || !lord.Map.lordManager.lords.Contains(lord))) lord = null;
            Scribe_References.Look(ref pawn, "pawn", true);
            Scribe_Values.Look(ref from, "from");
            Scribe_References.Look(ref lord, "lord");
            Scribe_Values.Look(ref frozen, "frozen");
            Scribe_Values.Look(ref startOverload, "startOverload");
            Scribe_Values.Look(ref touchTicks, "touchTicks");
            Scribe_Values.Look(ref sparedTick, "sparedTick", -1);
        }
    }

    /// <summary>
    /// One cast of Unlimited Void, from the end of the hand sign to the ball breaking. The rules (agreed 2026-09-23
    /// to 2026-09-30; the numbers are <see cref="CompProperties_AbilityUnlimitedVoid"/> and the AG_VoidOverload
    /// hediff, set in XML):
    ///
    /// - The ability fires at the end of the warm-up (the sign). The barrier then closes over Gojo for 0.3 s (the
    ///   open picture's Close) and everyone standing, lying or downed within the radius of Gojo's cell but Gojo is
    ///   taken with him into a pocket map made for this cast, each at its offset from him; he lands in the middle.
    ///   Walls do not matter. Pawns inside something (a casket, a pod, a flyer) are not taken; a carried pawn goes
    ///   with its carrier and is not frozen. If Gojo is downed, gone or out of hero form before the barrier
    ///   closes, nothing opens and the cooldown and charge stay spent.
    /// - Everyone taken with a living brain (flesh with a brain part) is frozen: a short stun renewed every tick
    ///   until the return. The rest (mechanoids and the like) act; the hostile ones get an assault lord inside.
    /// - A frozen pawn not hostile to Gojo that has stood next to him (8-way) for touchSeconds in all is spared:
    ///   its overload stops where it is. It stays frozen. Hostile pawns are never spared.
    /// - Every frozen pawn's overload (<see cref="Hediff_VoidOverload"/>) is overloadPerSecond x seconds from the
    ///   take to the end (or to its spare), on top of any it had.
    /// - The domain ends after domainSeconds, on Release, or when Gojo is downed, dies, leaves the pocket map,
    ///   loses the ability, leaves hero form, or the Echo pool empties. The overload stops building then; the
    ///   black hole collapses for 0.7 s (the inside picture's Collapse) and everyone comes back at the home cell
    ///   matching where it stands (the nearest free cell if that one is taken), items and corpses too; the pocket
    ///   map is removed. Every frozen pawn's overload starts to cap its consciousness.
    /// - Each non-hostile faction with a pawn that came out unspared loses neutralGoodwill once.
    ///
    /// The clocks: the home picture runs on <see cref="applyTick"/> (the open picture's OpenAt there), the inside
    /// picture on <see cref="takeTick"/>, and the break on <see cref="returnTick"/>.
    /// </summary>
    public sealed class UnlimitedVoidCast : IExposable
    {
        public Pawn caster;
        public Map home;
        /// <summary>Gojo's cell: where everyone is measured from, where the ball hangs, and what the return matches to.</summary>
        public IntVec3 centre;
        /// <summary>The Echo charge the cast took, given back only if the pocket map cannot be made.</summary>
        public float paid;
        /// <summary>Gojo was in hero form when he cast it, so the domain ends when he leaves it.</summary>
        public bool heroForm;
        public int applyTick, takeTick = -1, endTick = -1, returnTick = -1;
        public Map pocket;
        public bool fizzled;
        public string endReason;
        public List<VoidTaken> taken = new List<VoidTaken>();

        // The picture's side, not saved.
        internal CameraMove push;
        internal float reach = -1f;
        private bool shookOpen, shookBurst;
        /// <summary>The camera came home from the void's full white at the return, so the home map starts white too.</summary>
        private bool whiteHome;
        /// <summary>The return found no map to go to and is waiting for one (said once). Not saved.</summary>
        private bool waitingForMap;

        /// <summary>The frozen pawns' stun, renewed every tick (never StopStun, which would end other stuns).</summary>
        private const int StunRenew = 5;
        /// <summary>The white over the home map fading after the return, as the inside's arrival white does.</summary>
        private const float ReturnWhite = 0.25f;
        /// <summary>A shake is skipped if the home map comes on screen this long after its moment.</summary>
        private const float ShakeLate = 0.3f;

        private static CompProperties_AbilityUnlimitedVoid rules;
        internal static CompProperties_AbilityUnlimitedVoid Rules =>
            rules ?? (rules = VoidDefOf.AG_GojoUnlimitedVoid.comps?.OfType<CompProperties_AbilityUnlimitedVoid>().FirstOrDefault()
                              ?? new CompProperties_AbilityUnlimitedVoid());

        private static int CloseTicks => Mathf.RoundToInt(Open.Close * 60f);
        private static int CollapseTicks => Mathf.RoundToInt(Inside.Collapse * 60f);
        private static int BurstTicks => Mathf.RoundToInt(Open.BurstFor * 60f);

        /// <summary>The barrier is closing: Gojo is still on the home map.</summary>
        public bool Closing => !fizzled && takeTick < 0;
        /// <summary>Everyone is in the pocket map (the collapse included).</summary>
        public bool Standing => !fizzled && takeTick >= 0 && returnTick < 0;
        /// <summary>The cast still holds Gojo: closing or standing.</summary>
        public bool Busy => Closing || Standing;

        public UnlimitedVoidCast() { }

        public UnlimitedVoidCast(Pawn caster, int now, float paid, bool heroForm)
        {
            this.caster = caster;
            this.paid = paid;
            this.heroForm = heroForm;
            home = caster.Map;
            centre = caster.Position;
            applyTick = now;
        }

        public float SecondsLeft(int now) => takeTick < 0 ? Rules.domainSeconds : Mathf.Max(0f, (Rules.DomainTicks - (now - takeTick)) / 60f);

        /// <summary>The pocket map's middle, where Gojo lands.</summary>
        public IntVec3 Middle => pocket == null ? IntVec3.Invalid : new IntVec3(pocket.Size.x / 2, 0, pocket.Size.z / 2);

        public VoidTaken Record(Pawn pawn)
        {
            for (int i = 0; i < taken.Count; i++)
                if (taken[i].pawn == pawn) return taken[i];
            return null;
        }

        /// <summary>A frozen pawn's overload at <paramref name="now"/>: from the take to the end, or to its spare.</summary>
        public float OverloadOf(VoidTaken t, int now)
        {
            if (!t.frozen || takeTick < 0) return t.startOverload;
            int stop = t.Spared ? t.sparedTick : endTick >= 0 ? Mathf.Min(now, endTick) : now;
            return Mathf.Clamp01(t.startOverload + Mathf.Max(0, stop - takeTick) / 60f * Rules.overloadPerSecond);
        }

        private static bool LivingBrain(Pawn p) => p.RaceProps.IsFlesh && p.health?.hediffSet?.GetBrain() != null;

        private bool HasAbility => caster?.abilities?.GetAbility(VoidDefOf.AG_GojoUnlimitedVoid) != null;
        private bool InHeroForm => !heroForm || EchoUtility.ManifestedWith(caster, VoidDefOf.AG_GojoUnlimitedVoid) != null;

        // ---- the take ---------------------------------------------------------------------------------------------

        private void Take(int now)
        {
            if (caster == null || caster.Dead || caster.Downed || !caster.Spawned || caster.Map != home || !HasAbility || !InHeroForm)
            {
                fizzled = true;
                if (caster != null) Messages.Message("Unlimited Void: " + caster.LabelShortCap + " lost the domain before the barrier closed.", MessageTypeDefOf.NegativeEvent, false);
                return;
            }

            CompProperties_AbilityUnlimitedVoid r = Rules;
            centre = caster.Position;
            var pawns = new List<Pawn>();
            foreach (Pawn p in home.mapPawns.AllPawnsSpawned)
                if (p != caster && !p.Dead && (p.Position - centre).LengthHorizontalSquared <= r.radius * r.radius) pawns.Add(p);

            int size = Mathf.Max(r.mapSize, 2 * Mathf.CeilToInt(r.radius) + 6);
            pocket = UnlimitedVoidMap.Make(home, size);
            if (pocket == null)
            {
                fizzled = true;
                caster.abilities.GetAbility(VoidDefOf.AG_GojoUnlimitedVoid)?.ResetCooldown();
                GameComponent_Echoes.Get?.Refund(paid);
                paid = 0f;
                Messages.Message("Unlimited Void: the pocket map could not be made. No cooldown or charge spent.", caster, MessageTypeDefOf.NeutralEvent, false);
                return;
            }

            bool watching = Find.CurrentMap == home;
            var selected = new HashSet<Pawn>(Find.Selector.SelectedPawns);
            IntVec3 middle = Middle;
            var hostile = new List<Pawn>();
            Move(caster, FreeCellNear(pocket, middle), pocket);
            int frozen = 0;
            foreach (Pawn p in pawns)
            {
                var t = new VoidTaken { pawn = p, from = p.Position, lord = p.GetLord(), frozen = LivingBrain(p) };
                t.lord?.RemovePawn(p);
                taken.Add(t);
                Move(p, FreeCellNear(pocket, middle + (p.Position - centre)), pocket);
                if (t.frozen)
                {
                    frozen++;
                    Hediff_VoidOverload overload = OverloadHediff(p, true);
                    if (overload != null)
                    {
                        t.startOverload = overload.building ? 0f : overload.Severity;
                        overload.Build();
                    }
                    p.stances?.stunner?.StunFor(StunRenew, caster, false, false);
                }
                else if (p.Faction != null && p.HostileTo(Faction.OfPlayer)) hostile.Add(p);
            }
            takeTick = now;
            Assault(hostile, pocket);

            if (watching)
            {
                CameraJumper.TryJump(new GlobalTargetInfo(caster));
                foreach (Pawn p in pawns.Append(caster)) if (selected.Contains(p)) Find.Selector.Select(p, false, false);
                push = new CameraMove(Inside.CameraEvents);
                push.Begin();
            }
            Messages.Message("Unlimited Void: " + pawns.Count + " taken in with " + caster.LabelShortCap + " for " + r.domainSeconds.ToString("0.#")
                             + " s, " + frozen + " frozen.", caster, MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>The pawn's overload hediff; with <paramref name="add"/>, a new one at 0 if it has none.</summary>
        private static Hediff_VoidOverload OverloadHediff(Pawn p, bool add)
        {
            if (p?.health?.hediffSet == null) return null;
            var overload = p.health.hediffSet.GetFirstHediffOfDef(VoidDefOf.AG_VoidOverload) as Hediff_VoidOverload;
            if (overload != null || !add || p.Dead) return overload;
            overload = (Hediff_VoidOverload)HediffMaker.MakeHediff(VoidDefOf.AG_VoidOverload, p);
            overload.Severity = 0f;
            p.health.AddHediff(overload);
            return overload;
        }

        // ---- the domain -------------------------------------------------------------------------------------------

        /// <summary>Ends the domain now (Release, Gojo down, the pool emptied...): the overload stops and the collapse begins.</summary>
        public void End(string why)
        {
            if (!Standing || endTick >= 0) return;
            endTick = Find.TickManager.TicksGame;
            endReason = why;
        }

        private string EndCheck(int now)
        {
            if (now - takeTick >= Rules.DomainTicks) return "time";
            if (caster == null || caster.Dead) return "Gojo died";
            if (caster.Downed) return "Gojo went down";
            if (!caster.Spawned || caster.Map != pocket) return "Gojo left the domain";
            if (!HasAbility) return "Gojo lost the ability";
            if (!InHeroForm) return "Gojo left hero form";
            return null;
        }

        /// <summary>Every tick the domain stands: the freeze, the touches and the overload.</summary>
        private void Hold(int now)
        {
            bool open = endTick < 0;
            bool canTouch = open && caster != null && !caster.Dead && !caster.Downed && caster.Spawned && caster.Map == pocket;
            int touchTicks = Rules.TouchTicks;
            foreach (VoidTaken t in taken)
            {
                Pawn p = t.pawn;
                if (!t.frozen || p == null || p.Dead || p.Destroyed) continue;
                if (p.Spawned && p.Map == pocket) p.stances?.stunner?.StunFor(StunRenew, caster, false, false);
                if (canTouch && !t.Spared && p.Spawned && p.Map == pocket && !p.HostileTo(caster) && p.Position.AdjacentTo8WayOrInside(caster.Position)
                    && ++t.touchTicks >= touchTicks)
                    Spare(t, now);
                // Also during the collapse: OverloadOf stops counting at the end tick.
                Hediff_VoidOverload overload = OverloadHediff(p, true);
                if (overload == null) continue;
                overload.Build();
                overload.Severity = OverloadOf(t, now);
            }
        }

        private void Spare(VoidTaken t, int now)
        {
            t.sparedTick = now;
            if (t.pawn.Spawned) MoteMaker.ThrowText(t.pawn.DrawPos + new Vector3(0f, 0f, 0.5f), t.pawn.Map, "Spared", GojoGraphics.EyeBlue, 2f);
        }

        // ---- the return -------------------------------------------------------------------------------------------

        private void Return(int now)
        {
            if (pocket == null || !Find.Maps.Contains(pocket))
            {
                returnTick = now;
                return;
            }
            Map to = home != null && Find.Maps.Contains(home) ? home : Find.AnyPlayerHomeMap;
            if (to == null)
            {
                // The domain stays collapsed (white) with everyone in it; the cast asks again every tick, so they come
                // out as soon as the colony has a map again, and the void is never left with no cast to close it.
                if (!waitingForMap) Messages.Message("Unlimited Void: there is no map to return to; everyone waits in the void.", MessageTypeDefOf.NegativeEvent, false);
                waitingForMap = true;
                return;
            }
            returnTick = now;
            IntVec3 anchor = to == home ? centre : to.Center, middle = Middle;
            bool watching = Find.CurrentMap == pocket;
            var selected = new HashSet<Pawn>(Find.Selector.SelectedPawns);
            var moved = new List<Pawn>();
            var hostile = new List<Pawn>();
            var leaving = new List<Pawn>();
            IntVec3 Matching(IntVec3 cell) => anchor + (cell - middle);

            // Gojo, then everyone taken, then anyone else on the map, each at the home cell matching where it stands.
            if (caster != null && !caster.Dead && caster.Spawned && caster.Map == pocket)
            {
                Move(caster, FreeCellNear(to, Matching(caster.Position)), to);
                moved.Add(caster);
            }
            foreach (VoidTaken t in taken)
            {
                Pawn p = t.pawn;
                if (p == null || p.Destroyed || p.Dead || !p.Spawned || p.Map != pocket) continue;
                p.GetLord()?.RemovePawn(p);
                Move(p, FreeCellNear(to, Matching(p.Position)), to);
                moved.Add(p);
                // A lord whose toil now refuses new pawns would log an error and leave the pawn with no lord at all.
                if (t.lord != null && to.lordManager.lords.Contains(t.lord) && t.lord.CanAddPawn(p)) t.lord.AddPawn(p);
                else if (p.Faction != null && p.HostileTo(Faction.OfPlayer)) hostile.Add(p);
                else if (t.lord != null && p.Faction != null && p.Faction != Faction.OfPlayer) leaving.Add(p);
            }
            foreach (Pawn p in pocket.mapPawns.AllPawnsSpawned.ToList())
            {
                if (p.Dead) continue;
                p.GetLord()?.RemovePawn(p);
                Move(p, FreeCellNear(to, Matching(p.Position)), to);
                moved.Add(p);
                if (p.Faction != null && p.HostileTo(Faction.OfPlayer)) hostile.Add(p);
            }
            // Corpses, dropped weapons and everything else lying there, at the matching cells.
            foreach (Thing thing in pocket.listerThings.AllThings.ToList())
            {
                if (thing.Destroyed || !thing.Spawned || thing.def.category != ThingCategory.Item) continue;
                IntVec3 at = Matching(thing.Position);
                thing.DeSpawn();
                GenPlace.TryPlaceThing(thing, ClampInside(to, at), to, ThingPlaceMode.Near);
            }
            Assault(hostile, to);
            foreach (IGrouping<Faction, Pawn> group in leaving.GroupBy(p => p.Faction))
                LordMaker.MakeNewLord(group.Key, new LordJob_ExitMapBest(LocomotionUrgency.Walk), to, group);
            // The cap applies once everyone is home, so a pawn it downs falls (and drops what it holds) on the home map.
            foreach (VoidTaken t in taken)
                if (t.frozen && t.pawn != null && !t.pawn.Dead && !t.pawn.Destroyed) OverloadHediff(t.pawn, false)?.Release(now);
            CostGoodwill();

            if (watching)
            {
                // A push still running (a Release in the first 3 s) gives the zoom back first, while the camera is on the void.
                push?.Release();
                whiteHome = true;
                CameraJumper.TryJump(caster != null && caster.Spawned && caster.Map == to ? new GlobalTargetInfo(caster) : new GlobalTargetInfo(anchor, to));
                foreach (Pawn p in moved) if (selected.Contains(p)) Find.Selector.Select(p, false, false);
            }
            push = null;
            int overloaded = taken.Count(t => t.frozen && !t.Spared), spared = taken.Count(t => t.Spared);
            string ended = "Unlimited Void ended (" + (endReason ?? "time") + "): " + overloaded + " overloaded, " + spared + " spared.";
            if (caster != null && caster.Spawned) Messages.Message(ended, caster, MessageTypeDefOf.NeutralEvent, false);
            else Messages.Message(ended, MessageTypeDefOf.NeutralEvent, false);
            UnlimitedVoidMap.CloseLater(pocket);
        }

        /// <summary>Goodwill, as a harmful psycast costs it: once per non-hostile faction that has a pawn come out unspared.</summary>
        private void CostGoodwill()
        {
            int change = Rules.neutralGoodwill;
            if (change == 0 || caster == null || caster.Faction != Faction.OfPlayer) return;
            var factions = new HashSet<Faction>();
            foreach (VoidTaken t in taken)
            {
                Pawn p = t.pawn;
                if (!t.frozen || t.Spared || p == null || p.Dead || p.IsSlaveOfColony || p.IsQuestLodger() || p.IsQuestHelper()) continue;
                Faction faction = p.HomeFaction;
                if (faction != null && !faction.IsPlayer && !faction.HostileTo(Faction.OfPlayer)) factions.Add(faction);
            }
            foreach (Faction faction in factions)
                Faction.OfPlayer.TryAffectGoodwillWith(faction, change, true, true, HistoryEventDefOf.UsedHarmfulAbility);
        }

        // Moving pawns: Move, ClampInside, FreeCellNear and Assault are CrossMapMove's (Source/RimArt/Shared).

        // ---- the clock --------------------------------------------------------------------------------------------

        /// <summary>One game tick. False once the cast is over and the ball's break has played, so it can be dropped.</summary>
        public bool Tick(int now)
        {
            if (fizzled) return false;
            if (Closing)
            {
                if (now - applyTick >= CloseTicks) Take(now);
                return !fizzled;
            }
            if (Standing)
            {
                if (endTick < 0)
                {
                    string why = EndCheck(now);
                    if (why != null) End(why);
                }
                Hold(now);
                if (endTick >= 0 && now - endTick >= CollapseTicks) Return(now);
                return true;
            }
            return now - returnTick < BurstTicks;
        }

        // ---- the home side's picture --------------------------------------------------------------------------------

        /// <summary>
        /// The home map's side, on the home map only: the barrier closing, shrinking to the ball, the ball hanging
        /// over Gojo's cell while the domain stands, and at the return the ball breaking; if the camera came home
        /// from the void, the view starts white and clears in 0.25 s (the void's collapse ends in full white).
        /// </summary>
        public void DrawHome()
        {
            if (fizzled || home == null) return;
            var o = new Vector2(centre.x + 0.5f, centre.z + 0.5f);
            if (returnTick >= 0)
            {
                float age = UbwClock.Since(returnTick);
                UnlimitedVoidOpenGraphics.Draw(o, Open.BurstAt + age, home);
                if (whiteHome) GojoGraphics.WhiteView(1f - VfxMath.Smooth(age / ReturnWhite));
                if (!shookBurst && age >= Open.BurstShakeDelay)
                {
                    shookBurst = true;
                    if (age < Open.BurstShakeDelay + ShakeLate) Find.CameraDriver.shaker.DoShake(Open.BurstShake);
                }
                return;
            }
            if (!shookOpen)
            {
                // Only as the barrier closes: a player who looks at the home map later gets no shake.
                shookOpen = true;
                if (UbwClock.Since(applyTick) < ShakeLate) Find.CameraDriver.shaker.DoShake(Open.OpenShake);
            }
            float s = Open.OpenAt + UbwClock.Since(applyTick);
            // Past the picture's own hang (and its fading ring) the ball is drawn on its own for as long as the domain stands.
            if (s < Open.HangAt + Open.HangFor * 0.5f) UnlimitedVoidOpenGraphics.Draw(o, s, home);
            else if (VfxDraw.Shown(o, home))
            {
                PowerPoleGraphics.Sun(home, out Vector2 sun, out float shadow);
                UnlimitedVoidOpenGraphics.Ball(o, Open.BallHeight, Open.BallSize, s, 1f, sun, shadow);
            }
        }

        public void ExposeData()
        {
            // A void removed after the return (the break still playing) is saved nowhere and would not resolve on load.
            if (Scribe.mode == LoadSaveMode.Saving && pocket != null && !Find.Maps.Contains(pocket)) pocket = null;
            Scribe_References.Look(ref caster, "caster", true);
            Scribe_References.Look(ref home, "home");
            Scribe_Values.Look(ref centre, "centre");
            Scribe_Values.Look(ref paid, "paid");
            Scribe_Values.Look(ref heroForm, "heroForm");
            Scribe_Values.Look(ref applyTick, "applyTick");
            Scribe_Values.Look(ref takeTick, "takeTick", -1);
            Scribe_Values.Look(ref endTick, "endTick", -1);
            Scribe_Values.Look(ref returnTick, "returnTick", -1);
            Scribe_References.Look(ref pocket, "pocket");
            Scribe_Values.Look(ref fizzled, "fizzled");
            Scribe_Values.Look(ref endReason, "endReason");
            Scribe_Collections.Look(ref taken, "taken", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (taken == null) taken = new List<VoidTaken>();
                taken.RemoveAll(t => t == null || t.pawn == null);
                shookOpen = shookBurst = true;
            }
        }
    }
}
