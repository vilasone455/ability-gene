using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using static RimArt.ObitoGraphics;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// The pictures of the Obito kit, ported from the lab sketches (obito-kamui-phase/warp/store.js,
    /// obito-wood-release.js). The mechanics call in here when something happens; the pictures are drawn from
    /// that and from the gene's state on the tick clock, so they freeze with the game when it is paused.
    ///
    /// Obito's own body is drawn bent (<see cref="Patch_PawnRenderer_KamuiBend"/>): see-through while phased,
    /// a twist out of the right eye as Phase turns on and off, a twist round every hit that goes through him,
    /// and the vortex that winds him into the eye and out of it for Warp. Everything else is per map
    /// (<see cref="MapComponent_ObitoFX"/>).
    /// </summary>
    internal static class ObitoFX
    {
        /// <summary>From the release click until the thing stands at the cell: the swirl opens (0.2 s), then it unwinds out (0.45 s).</summary>
        internal const int ReleaseLandTicks = 39;

        // Decided looks (the sketches' sliders at their values).
        internal const float Ghost = 0.5f, EyeSwirl = 0.3f, SwirlSize = 0.55f;
        private const float RoundTwist = 0.35f, CutTwist = 0.26f;

        /// <summary>Every pawn with the gene the drawing has seen, so the render hook costs one lookup for everyone else.</summary>
        internal static readonly Dictionary<Pawn, Gene_Involute> Obitos = new Dictionary<Pawn, Gene_Involute>();

        internal sealed class Hit
        {
            public int tick;
            public Vector2 offset, dir;
            public bool ranged;
        }

        /// <summary>Hits that went through a pawn, drawn as twists on it and swirls at the point, while they last.</summary>
        internal static readonly Dictionary<Pawn, List<Hit>> Hits = new Dictionary<Pawn, List<Hit>>();
        /// <summary>When each pawn last came out of a Warp, for the unwind.</summary>
        internal static readonly Dictionary<Pawn, int> WarpOut = new Dictionary<Pawn, int>();

        internal sealed class Reach
        {
            public int tick;
            public Vector2 at;
            public bool counter;
        }

        /// <summary>Each Obito's last absorb, for the lean back after it.</summary>
        internal static readonly Dictionary<Pawn, Reach> Reaches = new Dictionary<Pawn, Reach>();

        /// <summary>
        /// Store's reach. Pawns have no arms: his right hand leaves its rest beside his body and moves at most
        /// <see cref="HandReach"/> toward the target, touching its body <see cref="TouchInset"/> short of its middle;
        /// the rest of the distance is his whole body leaning in (at most <see cref="MostLean"/>): about 0.3 cells to a
        /// pawn beside him, 0.7 to one on the diagonal.
        /// </summary>
        internal const float HandReach = 0.5f, TouchInset = 0.22f, MostLean = 0.7f;

        internal static int Now => Find.TickManager.TicksGame;

        internal static void Know(Gene_Involute gene)
        {
            if (gene?.pawn != null && !Obitos.ContainsKey(gene.pawn)) Obitos[gene.pawn] = gene;
        }

        /// <summary>A new game or a load: nothing from the last one is drawn or looked up.</summary>
        internal static void Reset()
        {
            foreach (Pawn pawn in Obitos.Keys) KamuiBend.ReleaseLive(pawn);
            Obitos.Clear();
            Hits.Clear();
            WarpOut.Clear();
            Reaches.Clear();
        }

        internal static void Forget(Pawn pawn)
        {
            if (pawn == null) return;
            Obitos.Remove(pawn);
            Hits.Remove(pawn);
            WarpOut.Remove(pawn);
            Reaches.Remove(pawn);
            KamuiBend.ReleaseLive(pawn);
        }

        private static MapComponent_ObitoFX On(Map map) => map?.GetComponent<MapComponent_ObitoFX>();

        // ---- events from the mechanics -----------------------------------------------------------

        internal static void PhaseChanged(Pawn pawn, bool on) { }

        /// <summary>
        /// A hit went through him. A round stops at a point on the body and runs into a small swirl there; a blow
        /// twists the body where the blade is, on the side it came from. Nothing comes out behind him.
        /// </summary>
        internal static void PassedThrough(Pawn pawn, DamageInfo dinfo)
        {
            if (pawn == null || !pawn.Spawned) return;
            float a = dinfo.Angle * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
            Thing by = dinfo.Instigator;
            if (by != null && by.Spawned && by.Map == pawn.Map)
            {
                Vector3 d = pawn.DrawPos - by.DrawPos;
                if (d.x * d.x + d.z * d.z > 0.01f) dir = new Vector2(d.x, d.z).normalized;
            }
            bool ranged = by == null || !by.Spawned || by.Map != pawn.Map || (by.Position - pawn.Position).LengthHorizontalSquared > 2.5f;
            if (!Hits.TryGetValue(pawn, out List<Hit> list)) Hits[pawn] = list = new List<Hit>();
            int n = list.Count + Now;
            // A round lands anywhere on the body (the sketch's spread); a blow on the near side at chest height.
            Vector2 offset = ranged
                ? new Vector2((Rand(n * 3 + 1) - 0.5f) * 0.28f, -0.22f + Rand(n * 3 + 2) * 0.55f)
                : new Vector2(-dir.x * 0.18f, 0.1f - dir.y * 0.12f);
            list.Add(new Hit { tick = Now, offset = offset, dir = dir, ranged = ranged });
            if (list.Count > 12) list.RemoveAt(0);
        }

        internal static void WarpedIn(Pawn pawn)
        {
            On(pawn.Map)?.AddShut(Eye(pawn, pawn.DrawPos, pawn.Rotation));
            KamuiBend.ReleaseLive(pawn);
        }

        internal static void ExitMarked(Pawn pawn, Map map, IntVec3 cell, int exitTick) => On(map)?.AddExit(pawn, cell, exitTick);

        internal static void WarpedOut(Pawn pawn) => WarpOut[pawn] = Now;

        internal static void Absorbed(Pawn obito, Thing target)
        {
            Vector3 at = target.DrawPos;
            Reaches[obito] = new Reach
            {
                tick = Now, at = new Vector2(at.x, at.z),
                counter = InvoluteUtility.GeneOf(obito)?.PassedThroughRecently(target) == true,
            };
            On(target.Map)?.AddAbsorb(obito, target);
        }

        /// <summary>
        /// How far Obito's body leans toward what he is reaching for, added to his draw position: in during Store's
        /// warm-up (0.2 s), held as the target starts to go, back over 0.25 s. The counter has no warm-up, so the
        /// lean comes in over 0.1 s after the absorb. Zero when he is not reaching.
        /// </summary>
        internal static Vector2 Lean(Pawn pawn, Vector3 from)
        {
            Vector2 to;
            float amount;
            if (WarmingUp(pawn, ObitoDefOf.AG_KamuiStore, out float s, out _) && pawn.stances.curStance is Stance_Warmup warmup && warmup.focusTarg.IsValid)
            {
                Vector3 target = warmup.focusTarg.CenterVector3;
                to = new Vector2(target.x, target.z);
                amount = Smooth(s / 0.2f);
            }
            else if (Reaches.TryGetValue(pawn, out Reach reach))
            {
                float a = (Now - reach.tick) / 60f;
                if (a < 0f || a > 0.6f)
                {
                    if (a > 0.6f) Reaches.Remove(pawn);
                    return Vector2.zero;
                }
                to = reach.at;
                amount = (reach.counter ? Smooth(a / 0.1f) : 1f) * (1f - Smooth((a - 0.15f) / 0.25f));
            }
            else return Vector2.zero;
            var d = new Vector2(to.x - from.x, to.y - from.z);
            float dist = d.magnitude;
            if (dist < 0.01f || amount <= 0f) return Vector2.zero;
            float lean = Mathf.Clamp(dist - TouchInset - HandReach, 0f, MostLean) * amount;
            return d / dist * lean;
        }

        internal static void ReleaseStarted(Pawn obito, Thing thing, Map map, IntVec3 cell) => On(map)?.AddRelease(obito, thing, cell);

        internal static void WoodRelease(Pawn caster, Vector2 aim, float reach, List<IntVec3> cells, CompProperties_AbilityWoodRelease props) =>
            On(caster.Map)?.AddWood(caster, aim, reach, cells, props);

        // ---- Obito's body ----------------------------------------------------------------------

        /// <summary>Warm-up progress of one of the kit's abilities: seconds in, and the whole warm-up; false if not warming it.</summary>
        internal static bool WarmingUp(Pawn pawn, AbilityDef def, out float seconds, out float total)
        {
            seconds = total = 0f;
            if (!(pawn.stances?.curStance is Stance_Warmup warmup) || !(warmup.verb is Verb_CastAbility cast) || cast.ability?.def != def) return false;
            seconds = (Now - warmup.startedTick) / 60f;
            total = Mathf.Max(0.01f, seconds + warmup.ticksLeft / 60f);
            return true;
        }

        /// <summary>
        /// How Obito's body is bent this frame: <paramref name="ph"/> is how phased he is (0 solid, 1 see-through at
        /// <see cref="Ghost"/>), <paramref name="warps"/> every twist and vortex in the order the sketch composes
        /// them, <paramref name="shadow"/> his shadow's share. False when nothing bends him.
        /// </summary>
        internal static bool Look(Pawn pawn, Gene_Involute gene, Vector3 drawPos, Rot4 facing, List<KamuiWarp> warps, out float ph, out float shadow)
        {
            warps.Clear();
            shadow = 1f;
            int now = Now;
            Vector2 eye = Eye(pawn, drawPos, facing);
            float age = (now - gene.PhaseChangedTick) / 60f;
            if (gene.Phase == KamuiPhaseState.Phased)
            {
                ph = Smooth(age / 0.3f);
                warps.Add(KamuiWarp.Twist(eye, 0.18f * Mathf.Sin(Mathf.PI * Clamp01(age / 0.45f)), 0.8f));
            }
            else
            {
                ph = age < 0.25f ? 1f - Smooth(age / 0.25f) : 0f;
                if (age < 0.3f) warps.Add(KamuiWarp.Twist(eye, -0.14f * Mathf.Sin(Mathf.PI * Clamp01(age / 0.3f)), 0.8f));
            }
            shadow = Mathf.Lerp(1f, 0.5f, ph);

            if (Hits.TryGetValue(pawn, out List<Hit> hits))
                foreach (Hit hit in hits)
                {
                    float t = (now - hit.tick) / 60f, life = hit.ranged ? RoundTwist : CutTwist;
                    if (t < 0f || t > life) continue;
                    var at = new Vector2(drawPos.x + hit.offset.x, drawPos.z + hit.offset.y);
                    warps.Add(hit.ranged
                        ? KamuiWarp.Twist(at, 0.45f * Mathf.Sin(Mathf.PI * Clamp01(t / RoundTwist)), 0.38f)
                        : KamuiWarp.Twist(at, 0.4f * Mathf.Sin(Mathf.PI * Clamp01(t / CutTwist)), 0.45f));
                }

            // Kamui: Warp. The warm-up winds him into the eye, slow at first and fastest at the end.
            if (WarmingUp(pawn, ObitoDefOf.AG_KamuiWarp, out float s, out float total))
            {
                float k = Mathf.Pow(Clamp01((s - 0.15f * total) / (0.85f * total)), 2f);
                warps.Add(KamuiWarp.Vortex(eye, k, 1.5f, 1.1f, 0.5f));
                shadow *= 1f - k;
            }
            if (WarpOut.TryGetValue(pawn, out int outTick))
            {
                float u = (now - outTick) / 60f;
                if (u >= 0f && u < 0.4f)
                {
                    float k = 1f - Smooth(u / 0.4f);
                    warps.Add(KamuiWarp.Vortex(eye, k, 1.5f, 1.1f, 0.5f));
                    shadow *= 1f - k;
                }
                else if (u >= 0.4f) WarpOut.Remove(pawn);
            }

            if (ph > 0.001f) return true;
            foreach (KamuiWarp warp in warps) if (warp.Active) return true;
            return false;
        }

        /// <summary>The faint slow swirl on the right eye while he is phased, so the state reads at a glance.</summary>
        internal static void PhaseEye(Pawn pawn, Gene_Involute gene, Vector3 drawPos, Rot4 facing)
        {
            float age = (Now - gene.PhaseChangedTick) / 60f, on, radius = EyeSwirl;
            if (gene.Phase == KamuiPhaseState.Phased)
            {
                on = Smooth(age / 0.15f);
                radius *= 1f + 0.7f * Mathf.Sin(Mathf.PI * Clamp01(age / 0.45f));
            }
            else
            {
                if (age >= 0.25f) return;
                on = 1f - Smooth(age / 0.25f);
                radius *= 1f - 0.6f * Smooth(age / 0.25f);
            }
            Swirl(Eye(pawn, drawPos, facing), radius, -Now / 60f * 2.2f, 0.55f * on, 3, haze: 0.4f);
        }

        private static readonly List<KamuiWarp> scratch = new List<KamuiWarp>();

        /// <summary>
        /// Draws Obito bent instead of the game's own draw of him, when anything bends him. The shadow, the aim
        /// pie, the path and ropes are still drawn; a held weapon and belts only while he is mostly solid and not
        /// winding into the eye (the sketch draws his body alone).
        /// </summary>
        internal static bool DrawBent(PawnRenderer renderer, Pawn pawn, Vector3 drawLoc, Rot4? rotOverride)
        {
            if (!Obitos.TryGetValue(pawn, out Gene_Involute gene) || gene.pawn != pawn) return false;
            if (!pawn.Spawned || pawn.Dead || pawn.GetPosture() != PawnPosture.Standing || pawn.IsHiddenFromPlayer())
            {
                KamuiBend.ReleaseLive(pawn);
                return false;
            }
            Rot4 facing = rotOverride ?? pawn.Rotation;
            if (!Look(pawn, gene, drawLoc, facing, scratch, out float ph, out float shadow))
            {
                KamuiBend.ReleaseLive(pawn);
                return false;
            }
            RenderTexture picture = KamuiBend.Live(pawn, facing);
            KamuiBend.Draw(picture, new Vector2(drawLoc.x, drawLoc.z), KamuiBend.PawnCells, drawLoc.y, new Color(1f, 1f, 1f, Mathf.Lerp(1f, Ghost, ph)), scratch);
            if (shadow > 0.99f) renderer.RenderShadowOnlyAt(drawLoc);
            else if (shadow > 0.02f)
                Sprite(new Vector2(drawLoc.x, drawLoc.z - 0.32f), 0.85f * shadow, 0.4f * shadow, new Color(0f, 0f, 0f, 0.35f * shadow), soft, LShadow);
            PhaseEye(pawn, gene, drawLoc, facing);
            bool vortex = false;
            foreach (KamuiWarp warp in scratch) if (warp.vortex && warp.Active) vortex = true;
            if (ph < 0.5f && !vortex)
                PawnRenderUtility.DrawEquipmentAndApparelExtras(pawn, drawLoc.WithYOffset(PawnRenderUtility.AltitudeForLayer(facing == Rot4.North ? -10f : 90f)),
                    facing, PawnRenderFlags.Clothes | PawnRenderFlags.Headgear);
            pawn.stances?.StanceTrackerDraw();
            pawn.pather?.PatherDraw();
            pawn.roping?.RopingDraw();
            return true;
        }
    }

    /// <summary>Store's reach: Obito's whole body leans toward what his hand is reaching for (<see cref="ObitoFX.Lean"/>).</summary>
    [HarmonyPatch(typeof(Pawn_DrawTracker), nameof(Pawn_DrawTracker.DrawPos), MethodType.Getter)]
    public static class Patch_PawnDrawTracker_KamuiReach
    {
        public static void Postfix(Pawn ___pawn, ref Vector3 __result)
        {
            if (ObitoFX.Obitos.Count == 0 || ___pawn == null || !ObitoFX.Obitos.ContainsKey(___pawn)) return;
            Vector2 lean = ObitoFX.Lean(___pawn, __result);
            __result.x += lean.x;
            __result.z += lean.y;
        }
    }

    /// <summary>
    /// Clears the kit's static tables when a game is made or loaded (vanilla makes every GameComponent then,
    /// before the maps load and the genes register themselves again).
    /// </summary>
    public class GameComponent_ObitoReset : GameComponent
    {
        public GameComponent_ObitoReset(Game game)
        {
            ObitoFX.Reset();
            KamuiPhaseRegistry.Clear();
        }
    }

    /// <summary>Obito's body, bent: see <see cref="ObitoFX.DrawBent"/>. Nobody else pays more than a dictionary count.</summary>
    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnAt))]
    public static class Patch_PawnRenderer_KamuiBend
    {
        public static bool Prefix(PawnRenderer __instance, Pawn ___pawn, Vector3 drawLoc, Rot4? rotOverride)
        {
            if (ObitoFX.Obitos.Count == 0) return true;
            return !ObitoFX.DrawBent(__instance, ___pawn, drawLoc, rotOverride);
        }
    }

    /// <summary>
    /// The kit's pictures on one map, on the tick clock: the swirls where hits went through Obito, the swirl
    /// shutting where he went in, the warning mark and the swirl where he comes out, a target streaming into his
    /// eye, a stored thing unwinding out at a cell, the counter's tell over an enemy whose attack went through
    /// him, the stun stars over a released enemy, the warm-ups' hand, and Wood Release (which also lands its hits
    /// here, as the front reaches each pawn).
    /// </summary>
    public class MapComponent_ObitoFX : MapComponent
    {
        private sealed class Shut { public Vector2 eye; public int tick; }
        private sealed class Exit { public Pawn pawn; public IntVec3 cell; public int markTick, outTick; public Rot4 facing; }
        private sealed class Absorb
        {
            public Pawn obito; public Texture picture; public bool owned; public float size;
            public Vector2 at; public int tick; public bool counter;
        }
        private sealed class Release
        {
            public Pawn obito; public Texture picture; public bool owned; public float size;
            public IntVec3 cell; public int tick; public bool hostile; public Pawn pawn;
        }

        private readonly List<Shut> shuts = new List<Shut>();
        private readonly List<Exit> exits = new List<Exit>();
        private readonly List<Absorb> absorbs = new List<Absorb>();
        private readonly List<Release> releases = new List<Release>();
        private readonly List<WoodStrike> woods = new List<WoodStrike>();

        public MapComponent_ObitoFX(Map map) : base(map) { }

        private static int Now => Find.TickManager.TicksGame;

        internal void AddShut(Vector2 eye) => shuts.Add(new Shut { eye = eye, tick = Now });

        internal void AddExit(Pawn pawn, IntVec3 cell, int exitTick) =>
            exits.Add(new Exit { pawn = pawn, cell = cell, markTick = Now, outTick = exitTick, facing = pawn.Rotation });

        internal void AddAbsorb(Pawn obito, Thing target)
        {
            Texture picture = KamuiBend.Still(target, out float size, out bool owned);
            Vector3 at = target.DrawPos;
            Gene_Involute gene = InvoluteUtility.GeneOf(obito);
            absorbs.Add(new Absorb
            {
                obito = obito, picture = picture, owned = owned, size = size, at = new Vector2(at.x, at.z), tick = Now,
                counter = gene != null && gene.PassedThroughRecently(target),
            });
        }

        internal void AddRelease(Pawn obito, Thing thing, IntVec3 cell)
        {
            Thing shown = thing is Pawn p && p.Dead && p.Corpse != null ? p.Corpse : thing;
            Texture picture = KamuiBend.Still(shown, out float size, out bool owned);
            releases.Add(new Release
            {
                obito = obito, picture = picture, owned = owned, size = size, cell = cell, tick = Now,
                pawn = thing as Pawn, hostile = thing is Pawn q && !q.Dead && q.HostileTo(Faction.OfPlayer),
            });
        }

        internal void AddWood(Pawn caster, Vector2 aim, float reach, List<IntVec3> cells, CompProperties_AbilityWoodRelease props) =>
            woods.Add(WoodStrike.Make(caster, aim, reach, cells, props, map));

        internal int WoodCount => woods.Count;

        public override void MapComponentTick()
        {
            for (int i = woods.Count - 1; i >= 0; i--)
            {
                woods[i].Tick(Now);
                if (woods[i].Done(Now)) woods.RemoveAt(i);
            }
        }

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map) return;
            int now = Now;
            float seconds = now / 60f;
            DrawObitos(now, seconds);
            DrawShuts(now);
            DrawExits(now);
            DrawAbsorbs(now, seconds);
            DrawReleases(now, seconds);
            foreach (WoodStrike wood in woods) wood.Draw(now);
        }

        // ---- per Obito: hit swirls, the counter's tell, the warm-ups ------------------------------------

        private static readonly Color Flash = new Color(1f, 0.85f, 0.45f);

        private void DrawObitos(int now, float seconds)
        {
            foreach (KeyValuePair<Pawn, Gene_Involute> entry in ObitoFX.Obitos)
            {
                Pawn pawn = entry.Key;
                if (!pawn.Spawned || pawn.Map != map || pawn.Dead) continue;
                Vector3 drawPos = pawn.DrawPos;
                Rot4 facing = pawn.Rotation;
                if (ObitoFX.Hits.TryGetValue(pawn, out List<ObitoFX.Hit> hits))
                {
                    for (int i = hits.Count - 1; i >= 0; i--)
                    {
                        ObitoFX.Hit hit = hits[i];
                        float age = (now - hit.tick) / 60f;
                        if (age > 0.6f) { hits.RemoveAt(i); continue; }
                        var at = new Vector2(drawPos.x + hit.offset.x, drawPos.z + hit.offset.y);
                        if (hit.ranged) RoundSinks(at, hit.dir, age);
                        else if (age <= 0.26f) Swirl(at, 0.14f, -seconds * 16f, 0.8f * (1f - Smooth((age - 0.18f) / 0.08f)), 3, 0.7f, haze: 0.5f);
                    }
                }
                // The counter's tell: this enemy's attack went through him, so Store takes it at a touch.
                if (pawn.Faction == Faction.OfPlayer)
                    foreach (Pawn attacker in entry.Value.RecentAttackers())
                    {
                        if (!attacker.Spawned || attacker.Map != map || !attacker.HostileTo(pawn)) continue;
                        Vector3 a = attacker.DrawPos;
                        int since = entry.Value.PassedThroughTick(attacker);
                        float left = ObitoRules.Store.counterSeconds - (now - since) / 60f;
                        Swirl(new Vector2(a.x, a.z - FeetBelowDrawPos + 0.98f), 0.12f, -seconds * 3f, 0.85f * Mathf.Min(1f, left / 0.5f), 3, haze: 0f);
                    }
                Vector2 ground = Ground(drawPos), eye = Eye(pawn, drawPos, facing);
                // Kamui: Warp's warm-up: the flash, then space turns round the eye as he winds in.
                if (ObitoFX.WarmingUp(pawn, ObitoDefOf.AG_KamuiWarp, out float s, out float total))
                {
                    Glint(eye, s - 0.05f);
                    float open = Smooth((s - 0.1f) / 0.35f);
                    float r = ObitoFX.SwirlSize * open * (0.35f + 0.65f * Mathf.Sqrt(Clamp01((s - 0.15f * total) / (0.85f * total))));
                    Swirl(eye, r, -(s * 4f + s * s * 5f), open);
                }
                // Kamui: Store's warm-up: the flash, his right hand goes to the target, the swirl opens on his eye.
                if (ObitoFX.WarmingUp(pawn, ObitoDefOf.AG_KamuiStore, out s, out total) && pawn.stances.curStance is Stance_Warmup w && w.focusTarg.IsValid)
                {
                    Glint(eye, s);
                    StoreHand(pawn, drawPos, facing, w.focusTarg.CenterVector3, Smooth(s / 0.25f));
                    float open = Smooth((s - 0.15f) / 0.3f);
                    Swirl(eye, ObitoFX.SwirlSize * open * 0.5f, -s * 4f, open);
                }
                // Wood Release's warm-up: his hand comes up to point down the line and turns to bark.
                if (ObitoFX.WarmingUp(pawn, ObitoDefOf.AG_WoodRelease, out s, out total) && pawn.stances.curStance is Stance_Warmup ww && ww.focusTarg.IsValid)
                {
                    Vector3 to = ww.focusTarg.CenterVector3;
                    var aim = new Vector2(to.x - pawn.DrawPos.x, to.z - pawn.DrawPos.z);
                    if (aim.sqrMagnitude > 1e-4f) WoodStrike.DrawWarmup(pawn, ground, aim.normalized, s, total);
                }
            }
        }

        /// <summary>A round stops at the point it hits: its tracer runs on into a small swirl opened there.</summary>
        private static void RoundSinks(Vector2 at, Vector2 dir, float age)
        {
            const float Tracer = 0.7f, Sink = 0.14f;
            if (age < Sink)
            {
                float k = age / Sink, len = Tracer * (1f - k);
                Rod(at - dir * len, at, 0.05f * (1f - 0.6f * k), Fade(Round, 1f - 0.5f * k), LFx + 0.01f);
                if (age < 0.06f) Sprite(at, 0.3f, 0.3f, Fade(Flash, 0.5f * (1f - age / 0.06f)), glow, LFx + 0.011f);
            }
            float open = Smooth((age + 0.03f) / 0.06f), close = 1f - Smooth((age - 0.18f) / 0.17f);
            if (close > 0f) Swirl(at, 0.2f * (0.6f + 0.4f * open), -age * 14f, open * close * 0.9f, 3, 0.7f, haze: 0.6f);
        }

        /// <summary>
        /// His right hand reaching from its rest toward <paramref name="target"/> (its middle), <paramref name="out"/>
        /// 0 at rest to 1 on the target's body. It moves at most <see cref="ObitoFX.HandReach"/>; his draw position
        /// already leans in (<see cref="ObitoFX.Lean"/>). No arm is drawn: pawns have none.
        /// </summary>
        private static void StoreHand(Pawn pawn, Vector3 drawPos, Rot4 facing, Vector3 target, float out_)
        {
            if (out_ <= 0.02f) return;
            Vector2 rest = HandRest(drawPos, facing);
            var f = new Vector2(target.x - drawPos.x, target.z - drawPos.z);
            if (f.sqrMagnitude < 1e-4f) f = new Vector2(facing.FacingCell.x, facing.FacingCell.z);
            f.Normalize();
            Vector2 touch = new Vector2(target.x, target.z) - f * ObitoFX.TouchInset;
            Vector2 reach = touch - rest;
            float length = Mathf.Min(reach.magnitude, ObitoFX.HandReach);
            Vector2 at = length < 0.01f ? rest : rest + reach.normalized * length * Smooth(out_);
            // Over both pawns (a pawn's own parts reach 0.037 above its altitude); facing north it is behind him.
            Hand(at, SkinOf(pawn), facing == Rot4.North ? LPawn - 0.01f : LPawn + 0.04f, 0f, Clamp01(out_ * 3f));
        }

        // ---- Warp: the swirl shutting behind him; the mark and the swirl where he comes out ---------------------

        private void DrawShuts(int now)
        {
            for (int i = shuts.Count - 1; i >= 0; i--)
            {
                float a = (now - shuts[i].tick) / 60f;
                if (a > 0.3f) { shuts.RemoveAt(i); continue; }
                float close = 1f - Smooth((a + 0.05f) / 0.2f);
                Swirl(shuts[i].eye, ObitoFX.SwirlSize * close, -(1f * 4f + 5f) - a * 12f, close);
                Shut(shuts[i].eye, a, ObitoFX.SwirlSize);
            }
        }

        private void DrawExits(int now)
        {
            for (int i = exits.Count - 1; i >= 0; i--)
            {
                Exit exit = exits[i];
                float m = (now - exit.markTick) / 60f, sinceOut = (now - exit.outTick) / 60f;
                if (sinceOut > 0.75f) { exits.RemoveAt(i); continue; }
                Vector3 centre = exit.cell.ToVector3Shifted();
                if (sinceOut >= 0f && exit.pawn.Spawned && exit.pawn.Map == map) centre = exit.pawn.DrawPos;
                Vector2 eye = Eye(exit.pawn, centre, exit.facing), ground = Ground(centre);
                float open = Smooth(m / 0.3f), fade = 1f - Smooth((sinceOut - 0.1f) / 0.65f);
                float r = ObitoFX.SwirlSize * (0.6f * open + 0.4f * Smooth(sinceOut / 0.4f));
                // Behind him as he comes out.
                Swirl(eye, r, -m * 5f, open * fade, layer: LPawn - 0.02f);
                Circle(ground, 0.42f + 0.08f * open, 0.65f * open * (1f - Smooth(sinceOut / 0.3f)), LFloor, Edge);
            }
        }

        // ---- Store --------------------------------------------------------------------------------------

        private static readonly List<KamuiWarp> bend = new List<KamuiWarp>();

        private void DrawAbsorbs(int now, float seconds)
        {
            const float Wind = 0.6f;
            for (int i = absorbs.Count - 1; i >= 0; i--)
            {
                Absorb ab = absorbs[i];
                float a = (now - ab.tick) / 60f;
                if (a > Wind + 0.3f || !ab.obito.Spawned || ab.obito.Map != map)
                {
                    if (ab.owned) KamuiBend.Free(ab.picture);
                    absorbs.RemoveAt(i);
                    continue;
                }
                Vector3 drawPos = ab.obito.DrawPos;
                Rot4 facing = ab.obito.Rotation;
                Vector2 eye = Eye(ab.obito, drawPos, facing);
                float k = Mathf.Pow(Clamp01(a / Wind), 1.15f);
                if (k < 1f)
                {
                    bend.Clear();
                    bend.Add(KamuiWarp.Vortex(eye, k, 1.6f, 1.7f, 1.2f));
                    Vector2 centre = ab.size >= KamuiBend.PawnCells - 0.01f ? ab.at : ab.at;
                    KamuiBend.Draw(ab.picture, centre, ab.size, LPawn + 0.005f, Color.white, bend);
                }
                // The hand holds while the target starts to go, then comes back (at once on the counter, which has no warm-up).
                float out_ = (ab.counter ? Smooth(a / 0.1f) : 1f) * (1f - Smooth((a - 0.15f) / 0.2f));
                StoreHand(ab.obito, drawPos, facing, new Vector3(ab.at.x, 0f, ab.at.y), out_);
                if (ab.counter) Glint(eye, a);
                float open = ab.counter ? Smooth(a / 0.1f) : 1f, close = 1f - Smooth((a - Wind + 0.05f) / 0.2f);
                Swirl(eye, ObitoFX.SwirlSize * open * (0.5f + 0.5f * k) * close, -(seconds * 4f + k * 8f), open);
                Shut(eye, a - Wind, ObitoFX.SwirlSize);
            }
        }

        private void DrawReleases(int now, float seconds)
        {
            const float Open = 0.1f, Out = 0.2f, Land = 0.65f;
            for (int i = releases.Count - 1; i >= 0; i--)
            {
                Release re = releases[i];
                float a = (now - re.tick) / 60f;
                float stun = re.hostile ? ObitoRules.Store.releaseStunSeconds : 0f;
                if (a > Land + Mathf.Max(0.35f, stun))
                {
                    if (re.owned) KamuiBend.Free(re.picture);
                    releases.RemoveAt(i);
                    continue;
                }
                if (re.obito.Spawned && re.obito.Map == map)
                {
                    Vector3 drawPos = re.obito.DrawPos;
                    Vector2 eye = Eye(re.obito, drawPos, re.obito.Rotation);
                    Glint(eye, a);
                    float eyeOpen = Smooth(a / 0.12f) * (1f - Smooth((a - 0.25f) / 0.2f));
                    Swirl(eye, 0.3f * eyeOpen, -seconds * 6f, eyeOpen, 3);
                }
                Vector3 cell = re.cell.ToVector3Shifted();
                var centre = new Vector2(cell.x, cell.z + 0.02f);
                // Behind the thing, so what comes out is in front of the hole; it fades as the thing comes out.
                float open = Smooth((a - Open) / 0.15f), fade = 1f - Smooth((a - Out - 0.05f) / (Land - Out + 0.15f));
                Swirl(centre, ObitoFX.SwirlSize * (0.6f + 0.4f * open) * (0.7f + 0.3f * fade), -(a - Open) * 6f, open * fade, layer: LPawn - 0.02f);
                if (a >= Out && a < Land)
                {
                    bend.Clear();
                    bend.Add(KamuiWarp.Vortex(centre, 1f - Smooth((a - Out) / (Land - Out)), 1.6f, 1f, 0.5f));
                    KamuiBend.Draw(re.picture, new Vector2(cell.x, cell.z), re.size, LPawn + 0.005f, Color.white, bend);
                }
                if (re.hostile && a >= Land && re.pawn != null && re.pawn.Spawned && re.pawn.Map == map)
                    StunMark(Ground(re.pawn.DrawPos), seconds);
            }
        }
    }
}
