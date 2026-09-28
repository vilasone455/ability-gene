using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using T = RimArt.AmaterasuTiming;

namespace RimArt
{
    /// <summary>
    /// Previews only: nobody burns, is hurt or moves, and no pawn is drawn. Each entry replays one scenario of the lab's
    /// rinnegan-amaterasu.js with its defaults round the clicked cell, which is the cell the sketch centres on, so the
    /// recorder can compare the port with the sketch: the gaze, the ignition at 0.5 s, the release at 4.0 s, the end at
    /// 5.0 s. The kit's real pictures are drawn by the game from RinneganPictures.
    /// </summary>
    public static class DebugActions_AmaterasuPreview
    {
        [RimArtDebug("Sasuke", "amaterasu: pawn")]
        public static void OnPawn() => Play(AmaterasuPreview.Pawn);

        [RimArtDebug("Sasuke", "amaterasu: pawn, spreads")]
        public static void Spreads() => Play(AmaterasuPreview.Spreads);

        [RimArtDebug("Sasuke", "amaterasu: held kunai")]
        public static void HeldKunai() => Play(AmaterasuPreview.HeldKunai);

        [RimArtDebug("Sasuke", "amaterasu: held Fūma")]
        public static void HeldFuma() => Play(AmaterasuPreview.HeldFuma);

        [RimArtDebug("Sasuke", "amaterasu: clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_AmaterasuPreview>();
            if (preview != null) preview.active = false;
        }

        private static void Play(AmaterasuPreview scenario) =>
            Find.CurrentMap.GetComponent<MapComponent_AmaterasuPreview>().Play(UI.MouseCell(), scenario);
    }

    /// <summary>The sketch's scenarios: one pawn lit by the cast; the same and its neighbour catching; held kunai; the held Fūma.</summary>
    public enum AmaterasuPreview { Pawn, Spreads, HeldKunai, HeldFuma }

    /// <summary>
    /// Replays one Amaterasu scenario on its own clock. The target (or the middle of the targets) is the clicked cell's
    /// centre and the caster stands 6 cells west. Drawn: the glint and blood on the caster's eye (at the sketch's
    /// stand-in eye, 0.04 east and 0.62 north of his feet), every fire, stain, burst, tail and fleck, the screen dim,
    /// and in the held scenes the weapons as lib/amenoyodomi.js draws them (held, let go, flying, lying, stuck in a
    /// raider) and the walls they stop against. Not drawn: the stand-in pawns.
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class MapComponent_AmaterasuPreview : MapComponent
    {
        public bool active;
        /// <summary>The light inside the black; violet, as the sketch's default. Read every frame.</summary>
        public AmaterasuLight light = AmaterasuLight.Violet;
        private AmaterasuPreview mode;
        private float seconds;
        private IntVec3 cell;
        private bool shaken;
        private AmaterasuHeldScene scene;

        // The weapons' look from lib/amenoyodomi.js: shadows along its default sun, the Fūma's two see-through copies in
        // flight, a kunai stuck in a raider (lib/flying-thunder-god.js stuckKunai), and the stand-in walls (lib/goku.js).
        private static readonly Vector2 Sun = new Vector2(-0.45f, -0.32f);
        private const float Strength = 0.32f;
        private static readonly Color ShadowInk = new Color(0.03f, 0.03f, 0.05f);
        private static readonly Color Stone = new Color(0.36f, 0.34f, 0.33f), StoneTop = new Color(0.5f, 0.48f, 0.46f);
        private static readonly Material KunaiMat = MaterialPool.MatFrom("RimArt/Kunai/Kunai", ShaderDatabase.Cutout);
        private static readonly Material FumaGhost = MaterialPool.MatFrom("RimArt/Fuma/Unfolded", ShaderDatabase.Transparent);
        private static readonly float ShadowLayer = AltitudeLayer.Shadows.AltitudeFor(), BuildingLayer = AltitudeLayer.Building.AltitudeFor();
        private static readonly float PawnLayer = AltitudeLayer.Pawn.AltitudeFor();

        public MapComponent_AmaterasuPreview(Map map) : base(map) { }

        public void Play(IntVec3 at, AmaterasuPreview scenario)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            mode = scenario;
            seconds = 0f;
            shaken = false;
            Vector3 centre = at.ToVector3Shifted();
            scene = scenario == AmaterasuPreview.HeldKunai || scenario == AmaterasuPreview.HeldFuma
                ? AmaterasuHeldScene.Build(new Vector2(centre.x, centre.z), scenario == AmaterasuPreview.HeldFuma)
                : null;
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            Vector3 centre = cell.ToVector3Shifted();
            var o = new Vector2(centre.x, centre.z);
            float s = seconds, sinceOut = s - T.Release;

            if (!shaken && s >= T.Ignite)
            {
                shaken = true;
                Find.CameraDriver.shaker.DoShake(T.Shake);
            }

            Vector2 caster = scene != null ? scene.caster : new Vector2(o.x - T.Distance, o.y);
            var eye = new Vector2(caster.x + 0.04f, caster.y + 0.62f);
            AmaterasuGraphics.Gaze(eye, s);
            AmaterasuGraphics.Blood(eye, s - T.Ignite);

            switch (mode)
            {
                case AmaterasuPreview.HeldKunai:
                    KunaiScene(s, sinceOut);
                    break;
                case AmaterasuPreview.HeldFuma:
                    FumaScene(s, sinceOut);
                    break;
                default:
                    AmaterasuGraphics.PawnStain(o, s - T.Ignite, sinceOut, 100);
                    AmaterasuGraphics.Pawn(o, s - T.Ignite, sinceOut, AmaterasuCatch.Cast, 100, light: light);
                    if (mode == AmaterasuPreview.Spreads)
                    {
                        // The neighbour 1 cell east shows fire 1.5 s after ignition, as in the sketch. Its hediff is
                        // added JumpTime earlier, while the flecks jump across from the burning pawn.
                        var neighbour = new Vector2(o.x + 1f, o.y);
                        float age = T.FireAge(s - (T.Spreads - T.JumpTime), AmaterasuCatch.Spread);
                        AmaterasuGraphics.PawnStain(neighbour, age, sinceOut, 300, T.NeighbourScale);
                        AmaterasuGraphics.Pawn(neighbour, age, sinceOut, AmaterasuCatch.Spread, 300, o, T.NeighbourScale, light);
                    }
                    break;
            }

            AmaterasuGraphics.Dim(o, s - T.Ignite);
            if (seconds >= T.Duration) active = false;
        }

        // Three kunai hang, catch 0.05 s apart, fly on when Amenoyodomi lets go. Two hit and their raiders catch; the
        // third stops at the wall and burns on the floor until a raider walking along the wall steps on it.
        private void KunaiScene(float s, float sinceOut)
        {
            AmaterasuHeldScene L = scene;
            Walls(L);
            for (int i = 0; i < L.kunai.Length; i++)
            {
                AmaterasuThrow K = L.kunai[i];
                int seed = 1000 + i * 100, order = L.order[i];
                float age = s - K.lit;
                bool lit = age >= 0f && K.lit < T.Release;
                DrawWeapon(K, s, i);
                if (s < K.stopAt)
                {
                    Vector2 q = K.InAir(s);
                    if (s > K.letGo) AmaterasuGraphics.FlyingFlames(false, q, K.deg, age, sinceOut, seed, order, 1f, light);
                    else AmaterasuGraphics.HeldFlames(false, q, K.deg, 0f, age, sinceOut, seed, order, 1f, light);
                }
                else if (K.hit)
                {
                    StuckKunai(K.end, K.deg);
                }
                else
                {
                    AmaterasuGraphics.WeaponStain(false, K.end, s - K.stopAt, sinceOut, seed);
                    AmaterasuGraphics.OnFloor(false, K.end, 0f, s - K.stopAt, sinceOut, seed, age, 1f, light);
                }
                if (lit) AmaterasuGraphics.Lit(false, K.InAir(K.lit), 0f, age, seed);
                if (lit && s > K.letGo)
                {
                    AmaterasuGraphics.Tail(K.InAir(s), K.deg, K.speed, Mathf.Min(s, K.stopAt) - K.letGo, Mathf.Max(0f, s - K.stopAt), age, sinceOut, seed);
                    AmaterasuGraphics.PathFlecks(K.Ground(K.letGo), K.deg, K.speed, Mathf.Min(s, Mathf.Min(K.stopAt, T.Release)) - K.letGo,
                        s - K.letGo, seed);
                }
                // A burning kunai that hits: a burst at the chest, and the raider catches from there outward.
                if (K.hit && K.stopAt < T.Release)
                {
                    AmaterasuGraphics.PawnStain(K.end, s - K.stopAt, sinceOut, 200 + i * 100);
                    AmaterasuGraphics.Pawn(K.end, s - K.stopAt, sinceOut, AmaterasuCatch.Hit, 200 + i * 100, light: light, burstSeed: 1500 + i * 50);
                }
            }
            AmaterasuGraphics.PawnStain(L.landed, s - L.step, sinceOut, 700);
            AmaterasuGraphics.Pawn(L.landed, s - L.step, sinceOut, AmaterasuCatch.Stepped, 700, light: light);
        }

        // The Fūma hangs and turns slowly, catches, then is let go: full spin along its line, every raider on the line
        // catches as it is cut, and it stops at the wall and lies burning.
        private void FumaScene(float s, float sinceOut)
        {
            AmaterasuHeldScene L = scene;
            AmaterasuThrow F = L.fuma;
            const int seed = 3000;
            float age = s - T.Ignite;
            bool lit = age >= 0f && T.Ignite < T.Release;
            Walls(L);
            for (int i = 0; i < L.line.Length; i++)
            {
                if (L.cuts[i] >= T.Release) continue;
                AmaterasuGraphics.PawnStain(L.line[i], s - L.cuts[i], sinceOut, 400 + i * 100);
                AmaterasuGraphics.Pawn(L.line[i], s - L.cuts[i], sinceOut, AmaterasuCatch.Hit, 400 + i * 100, light: light, burstSeed: 1600 + i * 50);
            }
            DrawWeapon(F, s, 9);
            if (s < L.landedAt)
            {
                Vector2 q = F.InAir(s);
                if (s > F.letGo) AmaterasuGraphics.FlyingFlames(true, q, F.deg, age, sinceOut, seed, 0, 1f, light);
                else AmaterasuGraphics.HeldFlames(true, q, F.deg, F.Turn(s), age, sinceOut, seed, 0, 1f, light);
                if (lit) AmaterasuGraphics.Lit(true, F.InAir(T.Ignite), F.Turn(T.Ignite), age, seed);
            }
            else
            {
                AmaterasuGraphics.WeaponStain(true, L.land, s - L.landedAt, sinceOut, seed);
                AmaterasuGraphics.OnFloor(true, L.land, F.Turn(L.landedAt), s - L.landedAt, sinceOut, seed, age, 1f, light);
            }
            if (lit && s > F.letGo)
                AmaterasuGraphics.PathFlecks(F.Ground(F.letGo), F.deg, F.speed, Mathf.Min(s, Mathf.Min(L.landedAt, T.Release)) - F.letGo, s - F.letGo, seed);
        }

        /// <summary>
        /// The weapon as lib/amenoyodomi.js's drawWeapon draws it with no streak: held (AmenoyodomiGraphics.Held), the
        /// let-go flash, flying (shadow, the Fūma's two copies 22 and 44 degrees behind, the weapon), lying with its
        /// landing dust. One stuck in a raider is <see cref="StuckKunai"/>.
        /// </summary>
        private static void DrawWeapon(in AmaterasuThrow w, float s, int seed)
        {
            if (s < w.letGo)
            {
                AmenoyodomiGraphics.Held(new HeldLook
                {
                    fuma = w.fuma, ground = w.Ground(s), deg = w.deg, turn = w.Turn(s), since = s, crept = w.U(s) - w.dist,
                    speed = w.speed * T.Hang, fullSpeed = w.speed, seed = seed,
                });
                return;
            }
            AmenoyodomiGraphics.LetGo(w.fuma, w.Ground(w.letGo), s - w.letGo);
            Vector2 g = w.Ground(s);
            float turn = w.Turn(s);
            if (s < w.stopAt)
            {
                float h = AmenoyodomiGraphics.Hold;
                Vector2 pos = AmenoyodomiGraphics.Up(g, h), shadow = g + Sun * h;
                if (w.fuma)
                {
                    Sprite(shadow, 1f, 1f, Fade(ShadowInk, Strength * 0.7f), soft, ShadowLayer, turn);
                    for (int k = 1; k <= 2; k++)
                        AmenoyodomiGraphics.Weapon(true, pos, w.deg, turn - 22f * k, 0.45f - 0.15f * k, FumaGhost, AmenoyodomiGraphics.ProjectileLayer - 0.001f * k);
                }
                else Sprite(shadow, 0.1f, 0.5f, Fade(ShadowInk, Strength * 0.8f), soft, ShadowLayer, 90f - w.deg);
                AmenoyodomiGraphics.Weapon(w.fuma, pos, w.deg, turn);
                return;
            }
            if (w.hit) return;
            AmaterasuGraphics.Lying(w.fuma, g, w.deg, turn);
            AmenoyodomiGraphics.Dust(g, s - w.stopAt, w.fuma);
        }

        /// <summary>A kunai stuck in a raider standing at <paramref name="victim"/>, pointing the way it flew.</summary>
        private static void StuckKunai(Vector2 victim, float deg)
        {
            Vector2 back = Turn(deg) * 0.1f;
            Sprite(new Vector2(victim.x - back.x, victim.y + 0.2f - back.y), 0.5f, 0.5f, Color.white, KunaiMat, PawnLayer + 0.01f, 90f - deg);
        }

        private static void Walls(AmaterasuHeldScene L)
        {
            for (int i = 0; i < L.walls.Length; i++)
            {
                Vector2 c = L.walls[i];
                DrawMesh(MeshPool.plane10, c, BuildingLayer, 1f, 1f, 0f, Stone, solid);
                DrawMesh(MeshPool.plane10, new Vector2(c.x, c.y + 0.06f), BuildingLayer + 0.002f, 0.9f, 0.78f, 0f, StoneTop, solid);
            }
        }
    }
}
