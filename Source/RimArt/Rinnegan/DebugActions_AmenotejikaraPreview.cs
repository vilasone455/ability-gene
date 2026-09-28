using UnityEngine;
using Verse;
using T = RimArt.AmenotejikaraTiming;

namespace RimArt
{
    /// <summary>
    /// Previews only: nobody moves and no pawn is drawn. Each entry plays one scenario of the Amenotejikara sketch
    /// (rinnegan-amenotejikara.js) round the chosen cell, the cell the sketch centres on, so the recorder can compare the
    /// port with the sketch. The real ability draws through RinneganPictures.Swapped.
    /// </summary>
    public static class DebugActions_AmenotejikaraPreview
    {
        [RimArtDebug("Sasuke", "amenotejikara: caster <-> enemy")]
        public static void CasterEnemy() => Play(AmenotejikaraPreview.CasterEnemy);

        [RimArtDebug("Sasuke", "amenotejikara: enemy <-> enemy")]
        public static void EnemyEnemy() => Play(AmenotejikaraPreview.EnemyEnemy);

        [RimArtDebug("Sasuke", "amenotejikara: enemy <-> held kunai")]
        public static void EnemyKunai() => Play(AmenotejikaraPreview.EnemyKunai);

        [RimArtDebug("Sasuke", "amenotejikara: clear preview", RimArtDebugKind.Now)]
        public static void Clear()
        {
            var preview = Find.CurrentMap?.GetComponent<MapComponent_AmenotejikaraPreview>();
            if (preview != null) preview.active = false;
        }

        private static void Play(AmenotejikaraPreview play) =>
            Find.CurrentMap.GetComponent<MapComponent_AmenotejikaraPreview>().Play(UI.MouseCell(), play);
    }

    public enum AmenotejikaraPreview { CasterEnemy, EnemyEnemy, EnemyKunai }

    /// <summary>
    /// The sketch's three scenarios on their own clock: the swap at <see cref="Lead"/> s, ends
    /// <see cref="Distance"/> cells apart east-west. In the held-kunai scene the kunai was thrown east from 4 cells west
    /// of its cell before the clip and creeps on at 1 %; the swap moves it onto the enemy's cell without turning it.
    /// </summary>
    public sealed class MapComponent_AmenotejikaraPreview : MapComponent
    {
        public const float Lead = 0.5f, Distance = 8f, KunaiThrow = 4f;
        public const float Duration = Lead + T.Duration;
        private const float HangShare = 0.01f;

        public bool active;
        private AmenotejikaraPreview mode;
        private float seconds;
        private IntVec3 cell;

        public MapComponent_AmenotejikaraPreview(Map map) : base(map) { }

        public void Play(IntVec3 at, AmenotejikaraPreview play)
        {
            if (!at.InBounds(map) || at.Fogged(map)) return;
            cell = at;
            mode = play;
            seconds = 0f;
            active = true;
        }

        public override void MapComponentUpdate()
        {
            if (!active || Find.CurrentMap != map || cell.Fogged(map)) return;
            // Unscaled: the preview runs at the same rate whether the game is paused or at 3x.
            seconds += Time.unscaledDeltaTime;
            Vector3 centre = cell.ToVector3Shifted();
            Draw(new Vector2(centre.x, centre.z), seconds, mode);
            if (seconds >= Duration) active = false;
        }

        /// <summary>One frame of a scenario at clip time <paramref name="s"/>, the sketch's draw().</summary>
        public static void Draw(Vector2 o, float s, AmenotejikaraPreview mode)
        {
            if (s < 0f || s >= Duration) return;
            float half = Distance / 2f, age = s - Lead;
            bool swapped = age >= 0f;
            Vector2 west = new Vector2(o.x - half, o.y), east = new Vector2(o.x + half, o.y);
            Vector2 casterHome = mode == AmenotejikaraPreview.CasterEnemy ? west : new Vector2(o.x, o.y - 1.6f);

            // The held kunai: caught over east at 0 s, creeping east at 1 % of 24 cells/s; the swap moves it to west.
            const float speed = AmenoyodomiGraphics.KunaiSpeed * HangShare;
            Vector2 kunaiAtSwap = east + new Vector2(speed * Lead, 0f);
            Vector2 KunaiAt(float t) => east + new Vector2(speed * t, 0f) + (t >= Lead ? west - kunaiAtSwap : Vector2.zero);
            bool kunai = mode == AmenotejikaraPreview.EnemyKunai;

            AmenotejikaraGraphics.Eye(new Vector2(casterHome.x + 0.04f, casterHome.y + 0.62f), s - Lead);
            if (kunai)
            {
                var look = new HeldLook
                {
                    ground = KunaiAt(s),
                    deg = 0f,
                    since = s,
                    crept = speed * s,
                    speed = speed,
                    fullSpeed = AmenoyodomiGraphics.KunaiSpeed,
                    seed = 3
                };
                AmenoyodomiGraphics.Held(look);
            }
            if (!swapped) return;

            // The ends: where each is, what stood there before, and where its pattern is centred after the swap.
            Vector2 endB = kunai ? kunaiAtSwap : east;
            SwapShape oldA = SwapShape.Pawn, oldB = kunai ? SwapShape.Kunai : SwapShape.Pawn;
            AmenotejikaraGraphics.Afterimage(west, age, oldA, 0f);
            AmenotejikaraGraphics.Afterimage(endB, age, oldB, 0f);
            AmenotejikaraGraphics.Ghost(west, endB, age, oldA, 0f);
            AmenotejikaraGraphics.Ghost(endB, west, age, oldB, 0f);
            AmenotejikaraGraphics.Pattern(kunai ? KunaiAt(s) : west, age, 0);
            AmenotejikaraGraphics.Pattern(endB, age, 1);
            AmenotejikaraGraphics.Flash(age, false, west, endB);
        }
    }
}
