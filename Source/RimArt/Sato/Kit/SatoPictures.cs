using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Where Satō's game code meets his pictures (Source/RimArt/Sato/*Graphics.cs, ported from the ajin-*.js
    /// sketches). Everything here turns ticks and pawns into the pictures' plain inputs; the pictures know
    /// nothing of the map. What outlives its thing (a rise after the anchor is used up, the shot after the cast)
    /// is kept in <see cref="MapComponent_SatoPictures"/>'s lists and drawn every frame from there.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SatoPictures
    {
        /// <summary>Height of the thrown piece's arc at its middle, in cells.</summary>
        private const float ThrowArc = 0.8f;

        /// <summary>How far under the pawn altitude the ghost is drawn while the pawn it grabs stands south of it.</summary>
        private const float GhostBehind = 0.04f;

        private static float Since(int tick) => (Find.TickManager.TicksGame - tick) / 60f;

        // ---- Tear timing (from TearGraphics) ----------------------------------------------------------------

        /// <summary>The limb comes off.</summary>
        public static int TearRipTicks => TearGraphics.RipAt.SecondsToTicks();
        /// <summary>The thrown limb lands and is left on the floor.</summary>
        public static int TearLandTicks => TearGraphics.LimbLandsAt.SecondsToTicks();
        /// <summary>The enemy is held (stunned) until it lands on its feet again.</summary>
        public static int TearHeldTicks => TearGraphics.DroppedAt.SecondsToTicks();
        /// <summary>The whole Tear, through the enemy's stagger or limp.</summary>
        public static int TearTotalTicks => TearGraphics.Duration.SecondsToTicks();

        // ---- events ---------------------------------------------------------------------------------------------

        public static void BodyDestroyed(Pawn pawn, IntVec3 spot, Map map) { }

        /// <summary>He is about to stand: keep what the peel and the pistol need after the holder and the hediff are gone.</summary>
        public static void Rising(Pawn pawn, Hediff_AjinReset reset, AjinAnchor holder)
        {
            Map map = holder?.MapHeld ?? pawn.MapHeld;
            MapComponent_SatoPictures pictures = MapComponent_SatoPictures.For(map);
            if (pictures == null) return;
            var record = new RiseRecord
            {
                pawn = pawn,
                tick = Find.TickManager.TicksGame,
                delay = reset.DelaySeconds,
                headFirst = reset.cause == AjinCause.Headshot,
                held = holder != null,
                anchor = holder?.DrawPos ?? Vector3.zero,
                piece = holder?.piece ?? AjinPiece.Body,
                pieceAngle = holder?.angle ?? 0f,
            };
            HeadshotRecord shot = pictures.shots.FirstOrDefault(r => r.pawn == pawn);
            if (shot != null && holder == null)
            {
                record.headshot = shot;
                shot.stoodTick = record.tick;
            }
            pictures.risen.Add(record);
        }

        /// <summary>The shot: keep the cast picture going (flash, smoke, spray) and put blood where the spray lands.</summary>
        public static void HeadshotFired(Pawn pawn)
        {
            MapComponent_SatoPictures pictures = MapComponent_SatoPictures.For(pawn.Map);
            if (pictures == null) return;
            pictures.shots.RemoveAll(r => r.pawn == pawn);
            pictures.shots.Add(new HeadshotRecord
            {
                pawn = pawn,
                firedTick = Find.TickManager.TicksGame,
                facing = pawn.Rotation,
                standingAt = pawn.DrawPos,
            });
            ThingDef blood = pawn.RaceProps.BloodDef;
            if (blood == null) return;
            int i = 0;
            foreach (Vector3 offset in HeadshotGraphics.SprayLandings(pawn.Rotation))
            {
                // The two patches, then every fourth drop.
                if (i++ >= 2 && i % 4 != 0) continue;
                IntVec3 cell = (pawn.DrawPos + offset).ToIntVec3();
                if (cell.InBounds(pawn.Map) && cell.Walkable(pawn.Map)) FilthMaker.TryMakeFilth(cell, pawn.Map, blood, pawn.LabelIndefinite());
            }
        }

        public static void GhostSummoned(Pawn owner, Pawn ghost)
        {
            if (ghost.Map == null) return;
            MapComponent_SatoPictures.For(ghost.Map)?.summons.Add(new SummonRecord { owner = owner, ghost = ghost, tick = Find.TickManager.TicksGame });
        }

        public static void SpawnTornLimb(Pawn ghost, Pawn victim, bool arm)
        {
            if (!ghost.Spawned) return;
            Vector3 landing = TearGraphics.LimbLanding(ghost.Position.ToVector3Shifted(), victim.Position.ToVector3Shifted());
            IntVec3 cell = landing.ToIntVec3();
            if (!cell.InBounds(ghost.Map) || !cell.Walkable(ghost.Map))
            {
                cell = ghost.Position;
                landing = cell.ToVector3Shifted();
            }
            var limb = (TornLimb)ThingMaker.MakeThing(SatoDefOf.AG_TornLimb);
            limb.arm = arm;
            limb.angle = TearGraphics.TornPieceAngle;
            limb.exact = landing;
            limb.madeTick = Find.TickManager.TicksGame;
            GenSpawn.Spawn(limb, cell, ghost.Map);
        }

        /// <summary>A pawn the ghost is tearing: its draw offset and tilt this frame.</summary>
        /// <remarks>
        /// Runs inside Pawn_DrawTracker.DrawPos for every pawn drawn, so it returns at once while no Tear is on, and it
        /// never reads a DrawPos itself (that would call back into here).
        /// </remarks>
        public static bool TearPose(Pawn pawn, out Vector3 offset, out float angle)
        {
            offset = Vector3.zero;
            angle = 0f;
            if (CompBlackGhost.Tearing.Count == 0 || pawn == null || !CompBlackGhost.Tearing.TryGetValue(pawn, out CompBlackGhost comp)) return false;
            if (comp.tearStart < 0 || !comp.Ghost.Spawned) return false;
            float seconds = Since(comp.tearStart);
            // Once it is back on its feet and walks off, the stagger or limp picture gives way to its own steps.
            if (seconds > TearGraphics.Duration || seconds > TearGraphics.DroppedAt && pawn.pather?.Moving == true) return false;
            TearEnemyPose pose = TearGraphics.Enemy(seconds, comp.tearArm, pawn.Position.ToVector3Shifted() - comp.Ghost.Position.ToVector3Shifted());
            offset = pose.Offset;
            angle = pose.Angle;
            return true;
        }

        // ---- things drawing themselves --------------------------------------------------------------------------

        /// <summary>
        /// An anchor or remains: in flight after the throw, lying with its ring, crumbling, or, while it holds him,
        /// the Reset at an anchor (seep, timer ring, the shell building up).
        /// </summary>
        public static void DrawPiece(AjinAnchor anchor, Vector3 drawLoc)
        {
            if (anchor.Crumbling)
            {
                AjinResetGraphics.DrawCrumble(drawLoc, anchor.piece, anchor.angle, Since(anchor.crumbleTick));
                return;
            }
            Pawn held = anchor.HeldPawn;
            Hediff_AjinReset reset = AjinReset.Resetting(held);
            if (reset != null)
            {
                AjinResetGraphics.DrawAtAnchor(AnchorShot(anchor.Map, drawLoc, anchor.piece, anchor.angle, reset.Seconds, reset.DelaySeconds));
                return;
            }
            if (anchor.Flying)
            {
                float u = Mathf.Clamp01(Since(anchor.madeTick) * 60f / CompAbilityEffect_Sever.FlightTicks);
                Vector3 at = Vector3.Lerp(anchor.thrownFrom, drawLoc, u);
                at.z += ThrowArc * 4f * u * (1f - u);
                at.y = drawLoc.y;
                AjinResetGraphics.DrawPiece(at, anchor.piece, anchor.angle + u * 540f);
                return;
            }
            AjinResetGraphics.DrawAnchorRing(drawLoc, 0.55f);
            AjinResetGraphics.DrawPiece(drawLoc, anchor.piece, anchor.angle);
        }

        private static ResetAnchorShot AnchorShot(Map map, Vector3 at, AjinPiece piece, float angle, float seconds, float delay)
        {
            Vector2 sun = map != null ? GenCelestial.GetLightSourceInfo(map, GenCelestial.LightType.Shadow).vector * SixPathsSlamGraphics.SunScale : Vector2.zero;
            return new ResetAnchorShot
            {
                Anchor = at,
                Piece = piece,
                PieceAngle = angle,
                Seconds = seconds,
                Delay = delay,
                Rebuild = AjinResetTiming.Rebuild,
                Rise = AjinResetTiming.Rise,
                Timer = true,
                Sun = sun,
                Shadow = map != null ? 0.32f * GenCelestial.CurShadowStrength(map) : 0f,
            };
        }

        public static void DrawTornLimb(TornLimb limb, Vector3 drawLoc)
        {
            Vector3 at = limb.exact == Vector3.zero ? drawLoc : new Vector3(limb.exact.x, drawLoc.y, limb.exact.z);
            // The same size it had in the ghost's claw.
            VfxDraw.BeginScale(new Vector2(at.x, at.z), CompProperties_BlackGhost.Scale);
            try
            {
                TearGraphics.DrawTornPiece(at, limb.arm, limb.angle);
            }
            finally
            {
                VfxDraw.EndScale();
            }
        }

        /// <summary>The ghost pawn this frame: its state from the comp and the pather, drawn in place of its sprite.</summary>
        public static void DrawGhost(Pawn ghost, CompBlackGhost comp, Vector3 drawLoc)
        {
            BlackGhostShot shot = BlackGhostShot.At(drawLoc, ghost.Rotation, comp.Seconds);
            shot.LifeFrac = comp.LifeFrac;
            shot.WalkCells = comp.walked;
            shot.Moving = ghost.pather?.Moving == true;
            if (shot.Moving && ghost.pather.nextCell.IsValid && ghost.pather.nextCell != ghost.Position)
                shot.WalkDir = (ghost.pather.nextCell - ghost.Position).ToVector3().normalized;
            float swipe = Since(comp.swipeTick);
            bool swiping = swipe < BlackGhostGraphics.SwipeSeconds;
            Pawn swipeTarget = comp.swipeTarget as Pawn;
            if (swiping)
            {
                shot.Swipe = swipe;
                shot.SwipeLeft = comp.swipes % 2 == 1;
                if (comp.swipeTarget != null && comp.swipeTarget.Spawned) shot.SwipeTarget = comp.swipeTarget.DrawPos;
            }
            shot.Build = Mathf.Clamp01(comp.Seconds / BlackGhostGraphics.SummonSeconds);
            if (comp.Dissolving) shot.Dissolve = Mathf.Clamp01(Since(comp.dissolveTick) / BlackGhostGraphics.DissolveSeconds);
            if (comp.tearStart >= 0 && comp.tearTarget != null)
            {
                shot.TearSeconds = Since(comp.tearStart);
                shot.TearTarget = comp.tearTarget.Position.ToVector3Shifted();
                shot.TearArm = comp.tearArm;
            }
            Map map = ghost.Map;
            if (map != null)
            {
                shot.Sun = GenCelestial.GetLightSourceInfo(map, GenCelestial.LightType.Shadow).vector * SixPathsSlamGraphics.SunScale;
                shot.ShadowStrength = 0.32f * GenCelestial.CurShadowStrength(map);
            }
            // Sorted by row as the sketch does: a pawn it grabs or swings at from the north stands in front of it. All
            // pawns share one altitude, so the ghost drops just under the pawn layer while that pawn is south of it.
            Pawn near = comp.tearTarget ?? swipeTarget;
            if (near != null && near.Spawned && near.Position.z < ghost.Position.z) shot.Pos.y -= GhostBehind;
            VfxDraw.BeginScale(new Vector2(drawLoc.x, drawLoc.z + BlackGhostGraphics.FeetDrop), CompProperties_BlackGhost.Scale);
            try
            {
                BlackGhostGraphics.DrawGhost(shot);
                if (shot.TearSeconds >= 0f) TearGraphics.DrawTear(new TearShot { Ghost = shot });
            }
            finally
            {
                VfxDraw.EndScale();
            }
            float hit = swipe - BlackGhostGraphics.SwipeHitAt;
            if (comp.swipeTarget != null && comp.swipeTarget.Spawned && hit >= BlackGhostGraphics.HitFrom && hit <= BlackGhostGraphics.HitUntil)
            {
                Vector3 target = comp.swipeTarget.DrawPos;
                BlackGhostGraphics.DrawClawHit(target, target - drawLoc, ghost.Rotation, hit, comp.swipes % 2 == 1);
            }
        }

        // ---- per-frame overlays -----------------------------------------------------------------------------

        internal static void DrawOverlays(MapComponent_SatoPictures pictures)
        {
            Map map = pictures.map;
            int now = Find.TickManager.TicksGame;

            // Headshot casts in their warmup: the gun arm comes up.
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.stances?.curStance is Stance_Warmup warmup && warmup.verb is Verb_CastAbility cast
                    && cast.ability?.def == SatoDefOf.AG_SatoHeadshotReset)
                {
                    float total = cast.verbProps.warmupTime.SecondsToTicks();
                    HeadshotGraphics.DrawCast(Cast(pawn, pawn.DrawPos, pawn.Rotation, (total - warmup.ticksLeft) / 60f));
                }
                Hediff_SatoGameMark mark = TheGame.MarkOn(pawn);
                if (mark != null) GenDraw.DrawTargetHighlightWithLayer(pawn.DrawPos, AltitudeLayer.MetaOverlays);
            }

            // Resets lying on the map: seep, timer ring, the cover growing; after a headshot the pistol by his hand.
            IReadOnlyList<Pawn> resetting = GameComponent_Sato.Instance?.Resetting;
            if (resetting != null)
                foreach (Pawn pawn in resetting)
                {
                    if (pawn == null || !pawn.Spawned || pawn.Map != map) continue;
                    Hediff_AjinReset reset = AjinReset.Resetting(pawn);
                    if (reset == null || reset.holder != null) continue;
                    float angle = pawn.Drawer.renderer.BodyAngle(PawnRenderFlags.None);
                    Vector3 body = pawn.DrawPos;
                    AjinResetGraphics.DrawInPlace(new ResetInPlaceShot
                    {
                        Body = body,
                        BodyAngle = angle,
                        Lying = true,
                        Seconds = reset.Seconds,
                        Delay = reset.DelaySeconds,
                        Rebuild = AjinResetTiming.Rebuild,
                        Rise = AjinResetTiming.Rise,
                        HeadFirst = reset.cause == AjinCause.Headshot,
                        Timer = true,
                        Wounds = Mathf.Min(3, pawn.health.hediffSet.hediffs.Count(h => h is Hediff_Injury)),
                    });
                    HeadshotRecord shot = pictures.shots.FirstOrDefault(r => r.pawn == pawn);
                    if (shot != null && reset.cause == AjinCause.Headshot)
                    {
                        shot.lyingAngle = angle;
                        shot.lyingBody = body;
                        HeadshotGraphics.DrawLyingPistol(body, angle, Since(shot.firedTick), shot.facing, shot.standingAt);
                    }
                }

            // The shot's flash, smoke and spray after the cast, where he stood.
            foreach (HeadshotRecord shot in pictures.shots)
            {
                float after = Since(shot.firedTick);
                if (after < HeadshotTiming.CastEnd - HeadshotTiming.ShotAt)
                    HeadshotGraphics.DrawCast(Cast(shot.pawn, shot.standingAt, shot.facing, HeadshotTiming.ShotAt + after));
            }

            // The black matter pouring out of Satō to where the ghost builds up.
            foreach (SummonRecord summon in pictures.summons)
            {
                float seconds = Since(summon.tick);
                if (seconds <= BlackGhostGraphics.StreamSeconds && summon.owner != null && summon.owner.Spawned && summon.ghost != null && summon.ghost.Spawned
                    && summon.owner.Map == map && summon.ghost.Map == map)
                    BlackGhostGraphics.DrawSummonStream(summon.owner.DrawPos, summon.ghost.DrawPos, seconds);
            }

            // Standing again: the cover or the shell flakes off, and the pistol comes back to his hand and goes.
            foreach (RiseRecord rise in pictures.risen)
            {
                Pawn pawn = rise.pawn;
                float after = Since(rise.tick);
                if (rise.held)
                    AjinResetGraphics.DrawAtAnchor(AnchorShot(map, rise.anchor, rise.piece, rise.pieceAngle, rise.delay + after, rise.delay));
                else if (pawn != null && pawn.Spawned && pawn.Map == map)
                    AjinResetGraphics.DrawInPlace(new ResetInPlaceShot
                    {
                        Body = pawn.DrawPos,
                        Lying = false,
                        Seconds = rise.delay + after,
                        Delay = rise.delay,
                        Rebuild = AjinResetTiming.Rebuild,
                        Rise = AjinResetTiming.Rise,
                        HeadFirst = rise.headFirst,
                        Timer = true,
                    });
                if (rise.headshot != null && pawn != null && pawn.Spawned && after < HeadshotTiming.RiseEnd)
                    HeadshotGraphics.DrawRisePistol(pawn.DrawPos, pawn.Rotation, after, rise.headshot.lyingAngle, rise.headshot.lyingBody,
                        pawn.story?.SkinColor, EchoCostume.WearsClothing(pawn) ? (Color?)null : pawn.story?.SkinColor);
            }

            pictures.shots.RemoveAll(r => r.stoodTick >= 0 && now - r.stoodTick > HeadshotTiming.RiseEnd * 60f
                || r.pawn == null || r.pawn.Destroyed || AjinReset.Resetting(r.pawn) == null && r.stoodTick < 0 && now - r.firedTick > 600);
            pictures.risen.RemoveAll(r => now - r.tick > Mathf.Max(AjinResetTiming.Rise, HeadshotTiming.RiseEnd) * 60f + 30f);
            pictures.summons.RemoveAll(r => now - r.tick > BlackGhostGraphics.StreamSeconds * 60f + 10f);
        }

        private static HeadshotCastShot Cast(Pawn pawn, Vector3 at, Rot4 facing, float seconds) => new HeadshotCastShot
        {
            Pawn = at,
            Facing = facing,
            Seconds = seconds,
            Skin = pawn?.story?.SkinColor ?? Color.clear,
            // Dressed, the costume's white shirt (the picture's own colour); naked, bare arm.
            Sleeve = pawn != null && !EchoCostume.WearsClothing(pawn) ? pawn.story?.SkinColor ?? Color.clear : Color.clear,
        };
    }

    public class RiseRecord
    {
        public Pawn pawn;
        public int tick;
        public float delay;
        public bool headFirst, held;
        public Vector3 anchor;
        public AjinPiece piece;
        public float pieceAngle;
        public HeadshotRecord headshot;
    }

    public class HeadshotRecord
    {
        public Pawn pawn;
        public int firedTick, stoodTick = -1;
        public Rot4 facing;
        public Vector3 standingAt, lyingBody;
        public float lyingAngle = 270f;
    }

    public class SummonRecord
    {
        public Pawn owner, ghost;
        public int tick;
    }

    /// <summary>Draws the short-lived Satō pictures every frame; nothing here is saved.</summary>
    public class MapComponent_SatoPictures : MapComponent
    {
        public readonly List<RiseRecord> risen = new List<RiseRecord>();
        public readonly List<HeadshotRecord> shots = new List<HeadshotRecord>();
        public readonly List<SummonRecord> summons = new List<SummonRecord>();

        public MapComponent_SatoPictures(Map map) : base(map) { }

        public static MapComponent_SatoPictures For(Map map) => map?.GetComponent<MapComponent_SatoPictures>();

        public override void MapComponentUpdate()
        {
            base.MapComponentUpdate();
            if (Find.CurrentMap != map) return;
            SatoPictures.DrawOverlays(this);
        }
    }

    [StaticConstructorOnStartup]
    public static class SatoTex
    {
        public static readonly Texture2D GhostGo = ContentFinder<Texture2D>.Get("RimArt/Sato/IconGhostGo");
        public static readonly Texture2D GhostAttack = ContentFinder<Texture2D>.Get("RimArt/Sato/IconGhostAttack");
        public static readonly Texture2D GhostTear = ContentFinder<Texture2D>.Get("RimArt/Sato/IconGhostTear");
    }
}
