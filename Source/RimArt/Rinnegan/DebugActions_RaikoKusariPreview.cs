using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using A = RimArt.AmenoyodomiGraphics;
using G = RimArt.RaikoKusariGraphics;
using S = RimArt.RaikoKusariScene;
using T = RimArt.RaikoKusariTiming;

namespace RimArt
{
    /// <summary>
    /// Previews only: nobody is caught, hurt or moved, and no pawn is drawn. Each entry plays one of the Raikō Kusari
    /// sketch's five scenarios round the chosen cell (the middle of the net; the caster stands 5 cells west), at the
    /// sketch's defaults: Dark Chidori, net 4 s, let go 2 s after it forms. It draws what the sketch draws except its
    /// stand-ins (the pawns, the mechanoid, the shield bubble, the shooter's gun and tracers): the Chidori through
    /// RaikoKusariGraphics, the held weapons through AmenoyodomiGraphics, and the weapons flying on, lying and stuck in
    /// pawns as lib/amenoyodomi.js does. The bodies it would shake, bend the line through and crawl over are where the
    /// sketch's stand-ins are, so the recorder can compare the port with the sketch.
    /// </summary>
    public static class DebugActions_RaikoKusariPreview
    {
        [RimArtDebug("Sasuke", "raiko kusari: fence")]
        public static void Fence() => Play(RaikoScenario.Fence);

        [RimArtDebug("Sasuke", "raiko kusari: ring")]
        public static void Ring() => Play(RaikoScenario.Ring);

        [RimArtDebug("Sasuke", "raiko kusari: drifting net")]
        public static void DriftingNet() => Play(RaikoScenario.DriftingNet);

        [RimArtDebug("Sasuke", "raiko kusari: let go")]
        public static void LetGo() => Play(RaikoScenario.LetGo);

        [RimArtDebug("Sasuke", "raiko kusari: fuma corner")]
        public static void FumaCorner() => Play(RaikoScenario.FumaCorner);

        [RimArtDebug("Sasuke", "raiko kusari: clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_RaikoKusariPreview>();
            if (preview != null) preview.active = false;
        }

        private static void Play(RaikoScenario scenario) =>
            Find.CurrentMap.GetComponent<MapComponent_RaikoKusariPreview>().Play(UI.MouseCell(), scenario);
    }

    /// <summary>What the preview draws of the weapons that AmenoyodomiGraphics has no public routine for.</summary>
    [StaticConstructorOnStartup]
    internal static class RaikoKusariPreviewArt
    {
        private static readonly Material KunaiMat = MaterialPool.MatFrom("RimArt/Kunai/Kunai", ShaderDatabase.Cutout);
        private static readonly Material FumaGhost = MaterialPool.MatFrom("RimArt/Fuma/Unfolded", ShaderDatabase.Transparent);
        private static readonly Color ShadowInk = new Color(0.03f, 0.03f, 0.05f);
        // lib/amenoyodomi.js's look defaults: the sun and the shadow strength.
        private static readonly Vector2 Sun = new Vector2(-0.45f, -0.32f);
        private const float Strength = 0.32f;
        private static readonly float ShadowLayer = AltitudeLayer.Shadows.AltitudeFor(), PawnLayer = AltitudeLayer.Pawn.AltitudeFor();

        /// <summary>A weapon flying at full speed, Hold cells up: its shadow, the Fūma's two afterimages, the weapon.</summary>
        internal static void Flying(bool fuma, Vector2 ground, float deg, float turn)
        {
            var c = new Vector2(ground.x + Sun.x * A.Hold, ground.y + Sun.y * A.Hold);
            Vector2 pos = A.Up(ground, A.Hold);
            if (fuma)
            {
                Sprite(c, 1f, 1f, Fade(ShadowInk, Strength * 0.7f), soft, ShadowLayer, turn);
                for (int k = 1; k <= 2; k++)
                    Sprite(pos, A.FumaSize, A.FumaSize, Fade(Color.white, 0.45f - 0.15f * k), FumaGhost, A.ProjectileLayer - 0.001f * k, turn - 22f * k);
            }
            else Sprite(c, 0.1f, 0.5f, Fade(ShadowInk, Strength * 0.8f), soft, ShadowLayer, 90f - deg);
            A.Weapon(fuma, pos, deg, turn);
        }

        /// <summary>A weapon lying where it came down at the end of its range, and its dust.</summary>
        internal static void Lying(bool fuma, Vector2 ground, float deg, float turn, float age)
        {
            if (fuma) A.Weapon(true, ground, deg, turn, 1f, null, Floor + 0.05f);
            else Sprite(ground, 0.62f, 0.62f, Color.white, KunaiMat, Floor + 0.06f, 90f - deg);
            A.Dust(ground, age, fuma);
        }

        /// <summary>A kunai stuck in a pawn (its feet), pointing the way it was thrown (flying-thunder-god.js stuckKunai).</summary>
        internal static void Stuck(Vector2 feet, float deg)
        {
            Vector2 at = feet - Turn(deg) * 0.1f + new Vector2(0f, 0.2f);
            Sprite(at, 0.5f, 0.5f, Color.white, KunaiMat, PawnLayer + 0.01f, 90f - deg);
        }
    }

    /// <summary>
    /// Replays one scenario (RaikoKusariScene, the sketch's script) round the chosen cell on its own clock, from
    /// the start of the charge to the sketch's clip end, and switches itself off. The camera shakes at the leap.
    /// </summary>
    public sealed class MapComponent_RaikoKusariPreview : MapComponent
    {
        private const RaikoColour Colour = RaikoColour.DarkChidori;

        public bool active;
        /// <summary>Seconds since the charge began: the sketch's s.</summary>
        public float seconds;
        // Summed in double so a frame that lands on a redraw boundary (every 1/12 s) steps as the sketch does.
        private double clock;
        private IntVec3 cell;
        private S scene;
        private bool shaken;
        private readonly List<Vector2> feet = new List<Vector2>(), pawnPos = new List<Vector2>();
        private readonly List<List<Vector2>> heldBy = new List<List<Vector2>>();
        private float[] surge = new float[0];

        public MapComponent_RaikoKusariPreview(Map map) : base(map) { }

        public void Play(IntVec3 at, RaikoScenario scenario)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            Vector3 centre = at.ToVector3Shifted();
            scene = S.Build(scenario, new Vector2(centre.x, centre.z));
            surge = new float[scene.weapons.Count];
            clock = 0.0;
            seconds = 0f;
            shaken = false;
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || scene == null || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            clock += Time.unscaledDeltaTime;
            seconds = (float)clock;
            if (seconds >= scene.end)
            {
                active = false;
                return;
            }
            if (!shaken && seconds >= scene.leap)
            {
                shaken = true;
                Find.CameraDriver.shaker.DoShake(T.CameraShake);
            }
            Draw(seconds);
        }

        private void Draw(float s)
        {
            float age = s - scene.leap;
            Bodies(s);

            // The caster: the Chidori charges in its hand, then leaps to the first weapon.
            Vector2 caster = new Vector2(scene.caster.x, scene.caster.y + T.FeetBelowDrawPos), hand = T.Hand(caster, 0f);
            G.Charge(hand, caster, s, scene.leap, s, Colour);
            G.Leap(hand, scene.weapons[0].At(s), age, s, Colour);

            // The lines, lit one after another; each weapon keeps the strongest flash of its live lines.
            for (int i = 0; i < surge.Length; i++) surge[i] = 0f;
            for (int i = 0; i < scene.links.Count; i++)
            {
                S.Link L = scene.links[i];
                if (s < L.start) continue;
                float end = scene.LinkEnd(L), endAge = float.IsInfinity(end) ? float.PositiveInfinity : end - scene.leap;
                var link = new RaikoLink
                {
                    index = i, links = scene.links.Count, failed = L.fails, end = endAge, cut = L.snap <= scene.netEnd || scene.letsGo,
                    a = scene.Joint(L.a, L.b, s), b = scene.Joint(L.b, L.a, s),
                    driftA = scene.weapons[L.a].Creep, driftB = scene.weapons[L.b].Creep, held = HeldBy(i, s),
                };
                if (!float.IsInfinity(end))
                {
                    link.endA = scene.Joint(L.a, L.b, end);
                    link.endB = scene.Joint(L.b, L.a, end);
                }
                G.Link(link, age, s, Colour);
                if (L.fails || s < L.lit || s - end >= T.CutOut) continue;
                float f = T.Surge(i, scene.links.Count, age, s, endAge);
                surge[L.a] = Mathf.Max(surge[L.a], f);
                surge[L.b] = Mathf.Max(surge[L.b], f);
            }
            G.Reached(scene.weapons[0].At(s), s - scene.reach0, false, 0, Colour);
            int reached = 1;
            foreach (S.Link L in scene.links)
                if (!L.fails) G.Reached(scene.weapons[L.b].At(s), s - L.lit, L.closing, reached++, Colour);

            for (int i = 0; i < scene.weapons.Count; i++) DrawWeapon(i, s);

            // The pawns: caught, held, let go; a scorch stays where each one was held.
            for (int k = 0; k < scene.pawns.Count; k++)
            {
                S.Pawn P = scene.pawns[k];
                if (s >= P.caught)
                {
                    Vector2 there = P.At(P.caught);
                    G.Scorch(new Vector2(there.x, there.y + T.FeetBelowDrawPos), Mathf.Min(s, scene.netEnd) - P.caught);
                    G.Caught(pawnPos[k], s - P.caught, s - scene.netEnd, k, s, Colour);
                    if (P.kind == S.Kind.Mech && s >= scene.netEnd && s < scene.netEnd + S.MechExtra) G.Emp(pawnPos[k], s - scene.netEnd, s);
                }
                for (int j = 0; j < P.hits.Count; j++) G.Struck(pawnPos[k], s - P.hits[j], S.HitStun, k, j, s, Colour);
            }
        }

        // Where each pawn is (feet, and DrawPos 0.3 north): a body the current runs through shakes on every redraw.
        private void Bodies(float s)
        {
            feet.Clear();
            pawnPos.Clear();
            for (int k = 0; k < scene.pawns.Count; k++)
            {
                S.Pawn P = scene.pawns[k];
                Vector2 q = P.At(s);
                bool held = s >= P.caught && s < scene.netEnd;
                bool emp = P.kind == S.Kind.Mech && !float.IsInfinity(P.caught) && s >= scene.netEnd && s < scene.netEnd + S.MechExtra;
                bool hit = P.hits.Exists(t => s >= t && s < t + S.HitStun);
                float size = held ? T.ShakeHeld : emp || hit ? T.ShakeHit : 0f;
                if (size > 0f) q += T.Shake(k, s, size);
                feet.Add(q);
                pawnPos.Add(new Vector2(q.x, q.y + T.FeetBelowDrawPos));
            }
        }

        // The pawns line i holds now, by DrawPos.
        private List<Vector2> HeldBy(int i, float s)
        {
            while (heldBy.Count <= i) heldBy.Add(new List<Vector2>());
            List<Vector2> list = heldBy[i];
            list.Clear();
            for (int k = 0; k < scene.pawns.Count; k++)
            {
                S.Pawn P = scene.pawns[k];
                if (P.link == i && s >= P.caught && s < scene.netEnd) list.Add(pawnPos[k]);
            }
            return list;
        }

        // One weapon: held (AmenoyodomiGraphics), let go, flying on charged, lying or stuck; lit while it is a corner.
        private void DrawWeapon(int i, float s)
        {
            S.Weapon w = scene.weapons[i];
            Vector2 g = w.At(s);
            float turn = w.Turn(s);
            bool flying = s >= scene.letGo && s < w.stopT, corner = false;
            foreach (S.Link L in scene.links) corner |= (L.a == i || L.b == i) && scene.Live(L, s);

            if (s < w.letGo)
                A.Held(new HeldLook
                {
                    fuma = w.fuma, ground = g, deg = w.deg, turn = turn, since = s, crept = w.U(s) - w.dist,
                    speed = w.share * w.speed, fullSpeed = w.speed, seed = w.seed,
                });
            else
            {
                A.LetGo(w.fuma, w.LetGoAt, s - w.letGo);
                if (flying) RaikoKusariPreviewArt.Flying(w.fuma, g, w.deg, turn);
                else if (w.hitPawn < 0) RaikoKusariPreviewArt.Lying(w.fuma, g, w.deg, turn, s - w.stopT);
            }

            if (corner) G.CornerPool(g, i, surge[i], s, Colour);
            if (w.fuma)
            {
                if (s >= w.stopT)
                {
                    G.FumaLands(g, s - w.stopT, Colour);
                    return;
                }
                if (flying) G.FlyingFuma(g, w.deg, turn, s, Colour);
                else if (corner)
                {
                    G.FumaArcs(g, turn, surge[i], s, Colour);
                    foreach (S.Link L in scene.links)
                    {
                        if (!scene.Live(L, s) || (L.a != i && L.b != i)) continue;
                        G.CornerSpark(scene.Joint(i, L.a == i ? L.b : L.a, s), i, surge[i], s, Colour);
                    }
                }
                return;
            }
            if (s < w.stopT)
            {
                if (corner) G.CornerSpark(g, i, surge[i], s, Colour);
                if (flying) G.FlyingKunai(g, w.deg, i, s, Colour);
            }
            else if (w.hitPawn >= 0) RaikoKusariPreviewArt.Stuck(feet[w.hitPawn], w.deg);
        }
    }
}
