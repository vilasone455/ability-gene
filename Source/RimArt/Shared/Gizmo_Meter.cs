using System;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// A weapon's meter in the command bar, 140 px wide: a label, the value on the right, a bar, and an optional line
    /// across the bar and a tick at each whole unit. The Flame Gauntlet's Heat and the Last Prism's sunlight use it.
    /// The kit makes its fill textures in a [StaticConstructorOnStartup] class: textures cannot be made off the main thread.
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class Gizmo_Meter : Gizmo
    {
        private static readonly Texture2D UnitTick = SolidColorMaterials.NewSolidColorTexture(new Color(0f, 0f, 0f, 0.45f));
        private readonly string label;
        private readonly Func<float> value;
        private readonly float max;
        private readonly Texture2D fill;

        /// <summary>The fill while this is true (the gauntlet overheating); null keeps <see cref="fill"/>.</summary>
        public Func<bool> hot;
        public Texture2D hotFill;
        /// <summary>A line across the bar at this value (the overheat line), drawn in <see cref="markTex"/>; below 0 none.</summary>
        public float mark = -1f;
        public Texture2D markTex;
        /// <summary>A thin tick at every whole unit, so a meter of 12 s of beam reads as 12 segments.</summary>
        public bool unitTicks;
        /// <summary>The value's text; null shows the whole units, "7 / 12".</summary>
        public Func<string> valueLabel;
        /// <summary>The tooltip, built only while the mouse is over the meter (a TipSignal with a getter).</summary>
        public Func<string> tip;

        public Gizmo_Meter(string label, Func<float> value, float max, Texture2D fill)
        {
            this.label = label;
            this.value = value;
            this.max = max;
            this.fill = fill;
            Order = -100f;
        }

        public override float GetWidth(float maxWidth) => 140f;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            var rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Widgets.DrawWindowBackground(rect);
            Rect inner = rect.ContractedBy(6f);
            float now = value(), share = max > 0f ? Mathf.Clamp01(now / max) : 0f;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), label);
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), valueLabel != null ? valueLabel() : Mathf.FloorToInt(now + 0.0001f) + " / " + max.ToString("0"));
            Text.Anchor = TextAnchor.UpperLeft;
            var bar = new Rect(inner.x, inner.y + 30f, inner.width, 22f);
            Widgets.FillableBar(bar, share, hot != null && hotFill != null && hot() ? hotFill : fill);
            if (unitTicks && max > 1f && max <= 60f)
                for (int k = 1; k < Mathf.CeilToInt(max); k++)
                    GUI.DrawTexture(new Rect(bar.x + bar.width * k / max - 0.5f, bar.y, 1f, bar.height), UnitTick);
            if (mark >= 0f && markTex != null)
            {
                float at = bar.x + bar.width * Mathf.Clamp01(mark / max);
                GUI.DrawTexture(new Rect(at - 1f, bar.y - 2f, 2f, bar.height + 4f), markTex);
            }
            if (tip != null) TooltipHandler.TipRegion(rect, new TipSignal(tip, label.GetHashCode()));
            return new GizmoResult(GizmoState.Clear);
        }
    }
}
