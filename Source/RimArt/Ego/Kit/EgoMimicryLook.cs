using UnityEngine;
using Verse;
using static RimArt.VfxMath;
using T = RimArt.EgoMimicryTiming;

namespace RimArt
{
    /// <summary>
    /// What one wielder's picture shows apart from its swings: the arm's stage as drawn (it grows to a new stage in 0.35 s,
    /// loses one in 0.3 s, creeps over the hand in 0.9 s when the state starts and recedes in 1 s when it ends), the blade's
    /// eyes opened by the state (over 0.6 s, closed over 1 s), and the last heal. Fed from the rules every tick; not saved,
    /// so after a load it starts from the sword's saved stage with nothing growing.
    /// </summary>
    public sealed class EgoMimicryLook
    {
        public readonly Pawn wielder;
        /// <summary>The stage drawn moves from <see cref="from"/> to <see cref="to"/> over <see cref="duration"/> s from <see cref="changeTick"/>.</summary>
        private float from, to, duration = 1f;
        private int changeTick = int.MinValue;
        private int openTick = int.MinValue, closeTick = int.MinValue;
        /// <summary>The tick of the last hit that fed the wielder.</summary>
        public int feedTick = int.MinValue;
        /// <summary>Corroded or overclocking with the sword on the last tick.</summary>
        public bool special;

        public EgoMimicryLook(Pawn wielder, int stage, bool special)
        {
            this.wielder = wielder;
            this.special = special;
            from = to = special ? stage : 0;
            if (special) openTick = int.MinValue / 2;
        }

        /// <summary>One game tick with the sword's stage and whether the state runs now.</summary>
        public void Tick(int now, int stage, bool specialNow)
        {
            if (specialNow && !special)
            {
                openTick = now;
                closeTick = int.MinValue;
            }
            else if (!specialNow && special) closeTick = now;
            special = specialNow;
            float target = specialNow ? stage : 0f;
            if (target == to) return;
            float shown = Stage;
            duration = target > shown ? (shown <= 0.01f ? T.Enter : T.StageGrow) : (!specialNow && target <= 0f ? T.Exit : T.StageShrink);
            from = shown;
            to = target;
            changeTick = now;
        }

        /// <summary>The stage as drawn, 0 to 4, fractional while it changes.</summary>
        public float Stage => changeTick == int.MinValue ? to : Mathf.Lerp(from, to, Smooth(PictureClock.Since(changeTick) / duration));

        /// <summary>The blade's eyes opened wide, 0 to 1.</summary>
        public float Open
        {
            get
            {
                if (special) return openTick <= int.MinValue / 2 ? 1f : Smooth(PictureClock.Since(openTick) / 0.6f);
                return closeTick == int.MinValue ? 0f : 1f - Smooth(PictureClock.Since(closeTick) / T.Exit);
            }
        }

        /// <summary>Seconds since the last heal, or -1 for none.</summary>
        public float FeedAge => feedTick == int.MinValue ? -1f : PictureClock.Since(feedTick);

        /// <summary>Nothing left to draw: the state is over, the arm has receded, the eyes are shut and the heal has faded.</summary>
        public bool Gone(int now) =>
            !special && to <= 0f && (changeTick == int.MinValue || now - changeTick > EgoMimicryCast.Ticks(duration))
            && (closeTick == int.MinValue || now - closeTick > EgoMimicryCast.Ticks(T.Exit))
            && (feedTick == int.MinValue || now - feedTick > EgoMimicryCast.Ticks(T.FeedTime));
    }
}
