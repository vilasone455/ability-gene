using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// Previews only: nobody fights, nothing is torn off and no real pawn is drawn. Each entry plays one scenario of the
    /// Black Ghost v2 sketch (Tools/VfxLab/web/sketches/ajin-black-ghost-v2.js) with the chosen cell as Satō's cell, so the
    /// recorder can compare the port with the sketch. The sketch's stand-ins (Satō, the enemy) are drawn as the sketch
    /// draws them; in the Tear scenes the enemy takes TearGraphics.Enemy's offset and tilt. Not drawn: the enemy's rifle,
    /// the blood splats of the fight (the game's own). The game draws the ghost pawn through BlackGhostGraphics.DrawGhost.
    /// </summary>
    public static class DebugActions_BlackGhostPreview
    {
        [RimArtDebug("Sato", "black ghost: idle east")]
        public static void IdleEast() => Play(BlackGhostScene.Idle, Vector2.right, true);

        [RimArtDebug("Sato", "black ghost: idle south")]
        public static void IdleSouth() => Play(BlackGhostScene.Idle, Vector2.down, true);

        [RimArtDebug("Sato", "black ghost: idle north")]
        public static void IdleNorth() => Play(BlackGhostScene.Idle, Vector2.up, true);

        [RimArtDebug("Sato", "black ghost: fight east")]
        public static void FightEast() => Play(BlackGhostScene.Fight, Vector2.right, true);

        [RimArtDebug("Sato", "black ghost: fight west")]
        public static void FightWest() => Play(BlackGhostScene.Fight, Vector2.left, true);

        [RimArtDebug("Sato", "black ghost: fight south")]
        public static void FightSouth() => Play(BlackGhostScene.Fight, Vector2.down, true);

        [RimArtDebug("Sato", "black ghost: fight north")]
        public static void FightNorth() => Play(BlackGhostScene.Fight, Vector2.up, true);

        [RimArtDebug("Sato", "black ghost: tear arm east")]
        public static void TearArmEast() => Play(BlackGhostScene.Tear, Vector2.right, true);

        [RimArtDebug("Sato", "black ghost: tear leg east")]
        public static void TearLegEast() => Play(BlackGhostScene.Tear, Vector2.right, false);

        [RimArtDebug("Sato", "black ghost: tear arm south")]
        public static void TearArmSouth() => Play(BlackGhostScene.Tear, Vector2.down, true);

        [RimArtDebug("Sato", "black ghost: tear leg west")]
        public static void TearLegWest() => Play(BlackGhostScene.Tear, Vector2.left, false);

        [RimArtDebug("Sato", "black ghost: clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_BlackGhostPreview>();
            if (preview != null) preview.active = false;
        }

        private static void Play(BlackGhostScene scene, Vector2 toward, bool arm) =>
            Find.CurrentMap.GetComponent<MapComponent_BlackGhostPreview>().Play(UI.MouseCell(), scene, toward, arm);
    }

    /// <summary>The sketch's scenarios (its Relay scene is not ported).</summary>
    public enum BlackGhostScene { Idle, Fight, Tear }

    /// <summary>
    /// The sketch's scenarios on their own clock, with its defaults: Satō on the cell; the ghost forms 1.1 cells to one
    /// side of him (1.35 for north and south), idles 0.35 s, walks at 3.2 cells/s to 1 cell short of an enemy 2.6 cells
    /// on (0.6 short straight north or south), then swipes twice (0.5 s each) or tears. It dissolves 0.4 s after the
    /// swipes, 1.9 s after the rip, or after 6 s of life in the idle scene; the clip runs 0.8 s past the dissolve.
    /// </summary>
    public sealed class MapComponent_BlackGhostPreview : MapComponent
    {
        public const float Distance = 2.6f, Speed = 3.2f, IdleLife = 6f, Pause = 0.35f, Reach = 1f, ReachFrontBack = 0.6f, Tail = 0.8f;
        private const float Summon = BlackGhostGraphics.SummonSeconds, Swipe = BlackGhostGraphics.SwipeSeconds;

        public bool active;
        private float seconds;
        private BlackGhostScene scene;
        private Vector2 toward;
        private bool arm, shookHit1, shookHit2, shookRip;
        private IntVec3 cell;

        public MapComponent_BlackGhostPreview(Map map) : base(map) { }

        /// <summary>The sketch's plan(): the layout in cells from Satō's cell, and the timeline.</summary>
        public struct Plan
        {
            public Vector2 D, Spawn, Enemy, Stop;
            public bool NS;
            /// <summary>Walks at Tw; grabs at TG (Tear); swipes at Ts1 and Ts2 with the hits at Hit1 and Hit2 (Fight); dissolves at Td; ends at End.</summary>
            public float Tw, TG, Ts1, Ts2, Hit1, Hit2, Td, End;
        }

        public static Plan Make(BlackGhostScene scene, Vector2 d)
        {
            bool ns = Mathf.Abs(d.x) < 0.01f;
            Vector2 side90 = d.x > 0f ? new Vector2(-d.y, d.x) : new Vector2(d.y, -d.x);
            Vector2 perp = ns ? Vector2.right : Mathf.Abs(d.y) < 0.01f ? Vector2.up : side90;
            var L = new Plan { D = d, NS = ns, Spawn = perp * (ns ? 1.35f : 1.1f), Tw = Summon + Pause };
            if (scene == BlackGhostScene.Idle)
            {
                L.Td = Summon + IdleLife;
            }
            else
            {
                L.Enemy = L.Spawn + d * Distance;
                L.Stop = L.Enemy - d * (ns ? ReachFrontBack : Reach);
                float arrive = L.Tw + (L.Stop - L.Spawn).magnitude / Speed;
                if (scene == BlackGhostScene.Tear)
                {
                    L.TG = arrive;
                    L.Td = arrive + TearGraphics.RipAt + 1.9f;
                }
                else
                {
                    L.Ts1 = arrive;
                    L.Ts2 = arrive + Swipe;
                    L.Hit1 = L.Ts1 + BlackGhostGraphics.SwipeHitAt;
                    L.Hit2 = L.Ts2 + BlackGhostGraphics.SwipeHitAt;
                    L.Td = L.Ts2 + Swipe + 0.4f;
                }
            }
            L.End = L.Td + BlackGhostGraphics.DissolveSeconds + Tail;
            return L;
        }

        /// <summary>The game's rule for a walking pawn: any sideways part faces east or west.</summary>
        private static int FacingOf(Vector2 v) => Mathf.Abs(v.x) > 0.2f ? (v.x > 0f ? 1 : 3) : v.y >= 0f ? 0 : 2;

        /// <summary>The sketch's phase markers for a recording label ("black ghost: tear leg west").</summary>
        public static List<(string name, float seconds)> PhasesFor(string label)
        {
            BlackGhostScene scene = label.Contains("tear") ? BlackGhostScene.Tear : label.Contains("fight") ? BlackGhostScene.Fight : BlackGhostScene.Idle;
            Vector2 d = label.Contains("west") ? Vector2.left : label.Contains("north") ? Vector2.up : label.Contains("south") ? Vector2.down : Vector2.right;
            Plan L = Make(scene, d);
            var phases = new List<(string name, float seconds)> { ("Summon", 0f) };
            if (scene == BlackGhostScene.Idle) phases.Add(("Idle", Summon));
            else phases.Add(("Walk", L.Tw));
            if (scene == BlackGhostScene.Tear)
            {
                phases.Add(("Grab", L.TG));
                phases.Add(("Lift", L.TG + TearGraphics.Grab));
                phases.Add(("Rip", L.TG + TearGraphics.RipAt));
                phases.Add(("Throw", L.TG + TearGraphics.RipAt + 0.12f));
            }
            if (scene == BlackGhostScene.Fight)
            {
                phases.Add(("Swipe", L.Ts1));
                phases.Add(("Swipe 2", L.Ts2));
            }
            phases.Add(("Fraying", Summon + BlackGhostGraphics.FrayFrom * (L.Td - Summon)));
            phases.Add(("Dissolve", L.Td));
            phases.Sort((a, b) => a.seconds.CompareTo(b.seconds));
            return phases;
        }

        public void Play(IntVec3 at, BlackGhostScene play, Vector2 dir, bool tearArm)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            scene = play;
            toward = dir;
            arm = tearArm;
            seconds = 0f;
            shookHit1 = shookHit2 = shookRip = false;
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            Vector3 centre = cell.ToVector3Shifted();
            Vector2 sun = GenCelestial.GetLightSourceInfo(map, GenCelestial.LightType.Shadow).vector * SixPathsSlamGraphics.SunScale;
            float strength = 0.32f * GenCelestial.CurShadowStrength(map);
            Plan L = Make(scene, toward);
            Draw(new Vector2(centre.x, centre.z), seconds, scene, toward, arm, sun, strength);
            // The sketch's camera shakes: 0.05 at each claw hit, 0.07 at the rip.
            if (scene == BlackGhostScene.Fight)
            {
                Shake(ref shookHit1, L.Hit1, BlackGhostGraphics.HitShake);
                Shake(ref shookHit2, L.Hit2, BlackGhostGraphics.HitShake);
            }
            if (scene == BlackGhostScene.Tear) Shake(ref shookRip, L.TG + TearGraphics.RipAt, TearGraphics.RipShake);
            if (seconds >= L.End) active = false;
        }

        private void Shake(ref bool done, float at, float size)
        {
            if (done || seconds < at) return;
            done = true;
            Find.CameraDriver.shaker.DoShake(size);
        }

        private static Vector3 V3(Vector2 v) => new Vector3(v.x, 0f, v.y);

        /// <summary>One frame of a scenario at clip time <paramref name="t"/>: the sketch's draw(), with the ghost, the stream, the claw hits and the tear from the port.</summary>
        public static void Draw(Vector2 o, float t, BlackGhostScene scene, Vector2 d, bool arm, Vector2 sun, float strength)
        {
            Plan L = Make(scene, d);
            int face = FacingOf(d);
            Vector2 ghostAt = L.Spawn;
            var shot = BlackGhostShot.At(V3(o + L.Spawn), new Rot4(face), t);
            shot.Build = Mathf.Clamp01(t / Summon);
            shot.Dissolve = Mathf.Clamp01((t - L.Td) / BlackGhostGraphics.DissolveSeconds);
            shot.LifeFrac = Mathf.Clamp01((t - Summon) / (L.Td - Summon));
            shot.Sun = sun;
            shot.ShadowStrength = strength;
            if (scene != BlackGhostScene.Idle && t >= L.Tw)
            {
                float dist = (L.Stop - L.Spawn).magnitude, walkFor = dist / Speed;
                if (t < L.Tw + walkFor)
                {
                    // walking(): eased 35 % toward a smoothstep, so it sets off and stops a little softer.
                    float u = Mathf.Clamp01((t - L.Tw) / walkFor), e = Mathf.Lerp(u, Smooth(u), 0.35f);
                    ghostAt = Vector2.Lerp(L.Spawn, L.Stop, e);
                    shot.WalkCells = e * dist;
                    shot.Moving = t > L.Tw;
                    shot.WalkDir = V3(L.Stop - L.Spawn);
                }
                else
                {
                    ghostAt = L.Stop;
                    shot.WalkCells = dist;
                    if (scene == BlackGhostScene.Fight)
                    {
                        if (t < L.Ts2) shot.Swipe = t - L.Ts1;
                        else if (t < L.Ts2 + Swipe)
                        {
                            shot.Swipe = t - L.Ts2;
                            shot.SwipeLeft = true;
                        }
                        shot.SwipeTarget = V3(o + L.Enemy);
                    }
                    else
                    {
                        shot.TearSeconds = t - L.TG;
                        shot.TearTarget = V3(o + L.Enemy);
                        shot.TearArm = arm;
                    }
                }
            }
            shot.Pos = V3(o + ghostAt);

            // The stand-ins are drawn under the ghost when north of it or on its row, over it when south, as the sketch
            // sorts its actors (north first; on one row the ghost, pushed last, comes out on top). The torn enemy sorts
            // 0.01 south of its feet, so it covers the ghost on its own row.
            float ghostZ = o.y + ghostAt.y;
            if (shot.TearSeconds >= 0f)
            {
                TearGraphics.Step(o + ghostAt, o + L.Enemy, shot.TearSeconds, out Vector2 stepped, out _, out _);
                ghostZ = stepped.y;
            }
            float Layer(float z) => z >= ghostZ ? BlackGhostMatter.PawnLayer - 0.001f : BlackGhostMatter.PawnLayer + 0.015f;

            StandIn(o, BlackGhostMatter.Shirt, sun, strength, true, false, Layer(o.y));
            BlackGhostGraphics.DrawSummonStream(V3(o), V3(o + L.Spawn), t);

            Vector2 e0 = o + L.Enemy;
            if (scene == BlackGhostScene.Fight)
            {
                float flinch = 0.10f * BlackGhostMatter.Bump((t - L.Hit1) / 0.25f) + 0.06f * BlackGhostMatter.Bump((t - L.Hit2) / 0.2f);
                Vector2 e = e0 + d * flinch;
                StandIn(e, BlackGhostMatter.Enemy, sun, strength, false, t > L.Hit2 + 0.12f, Layer(e.y));
                BlackGhostGraphics.DrawClawHit(V3(e0), V3(d), new Rot4(face), t - L.Hit1, false);
                BlackGhostGraphics.DrawClawHit(V3(e0), V3(d), new Rot4(face), t - L.Hit2, true);
            }
            if (scene == BlackGhostScene.Tear)
            {
                float since = t - L.TG;
                TearEnemyPose pose = TearGraphics.Enemy(since, arm, V3(L.Enemy - L.Stop));
                Vector2 g = e0 + new Vector2(pose.FloorOffset.x, pose.FloorOffset.z);
                TearGraphics.DrawFloorShadow(V3(e0), pose, sun, strength);
                float layer = Layer(g.y - 0.01f);
                const float s = 1.3f, low = -0.33f;
                DrawMesh(disc, TearGraphics.OnEnemy(g, pose, new Vector2(0f, low + 0.18f * s)), layer, 0.22f * s, 0.32f * s, pose.Angle, BlackGhostMatter.Enemy, solid);
                DrawMesh(disc, TearGraphics.OnEnemy(g, pose, new Vector2(0f, low + 0.58f * s)), layer + 0.0001f, 0.16f * s, 0.17f * s, pose.Angle, BlackGhostMatter.Skin, solid);
                if (since >= TearGraphics.LimbLandsAt)
                    TearGraphics.DrawTornPiece(TearGraphics.LimbLanding(V3(o + L.Stop), V3(e0)), arm, TearGraphics.TornPieceAngle);
            }

            BlackGhostGraphics.DrawGhost(shot);
            if (shot.TearSeconds >= 0f) TearGraphics.DrawTear(new TearShot { Ghost = shot });
        }

        /// <summary>lib/ajin.js standIn(): a stand-in pawn at the real pawn's size, standing or downed, with a cap for Satō.</summary>
        private static void StandIn(Vector2 pos, Color body, Vector2 sun, float strength, bool cap, bool downed, float layer)
        {
            const float s = 1.3f, low = -0.33f;
            Sprite(new Vector2(pos.x + sun.x * 0.5f, pos.y + low + 0.05f + sun.y * 0.5f), 0.95f, 0.42f, Fade(BlackGhostMatter.ShadowBody, strength * 1.4f),
                soft, BlackGhostMatter.ShadowLayer);
            if (downed)
            {
                DrawMesh(disc, new Vector2(pos.x + 0.05f * s, pos.y + low + 0.15f), layer, 0.32f * s, 0.20f * s, 0f, body, solid);
                DrawMesh(disc, new Vector2(pos.x - 0.30f * s, pos.y + low + 0.17f), layer + 0.0001f, 0.16f * s, 0.16f * s, 0f, BlackGhostMatter.Skin, solid);
                return;
            }
            DrawMesh(disc, new Vector2(pos.x, pos.y + low + 0.18f * s), layer, 0.22f * s, 0.32f * s, 0f, body, solid);
            DrawMesh(disc, new Vector2(pos.x, pos.y + low + 0.58f * s), layer + 0.0001f, 0.16f * s, 0.17f * s, 0f, BlackGhostMatter.Skin, solid);
            if (!cap) return;
            DrawMesh(disc, new Vector2(pos.x, pos.y + low + 0.66f * s), layer + 0.0002f, 0.165f * s, 0.10f * s, 0f, BlackGhostMatter.Cap, solid);
            DrawMesh(disc, new Vector2(pos.x, pos.y + low + 0.60f * s), layer + 0.0003f, 0.17f * s, 0.035f * s, 0f, BlackGhostMatter.Cap, solid);
        }
    }
}
