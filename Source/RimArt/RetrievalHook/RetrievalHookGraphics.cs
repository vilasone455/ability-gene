using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>
    /// Draws the tether and the padded net. A held item is drawn here too, because while it is in
    /// the pull's holder it is not on the map and nothing else draws it.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class RetrievalHookGraphics
    {
        private const float NetSize = 1.25f;
        private const float TetherWidth = 0.07f;

        private static readonly Material net = MaterialPool.MatFrom("RimArt/RetrievalHook/Net", ShaderDatabase.Transparent);
        private static readonly Material tether =
            SolidColorMaterials.NewSolidColorMaterial(new Color(0.18f, 0.16f, 0.13f), ShaderDatabase.Transparent);

        public static void Draw(RetrievalPull pull)
        {
            if (pull.caster == null || !pull.caster.Spawned) return;

            float netAltitude = AltitudeLayer.Projectile.AltitudeFor();
            Vector3 hand = pull.caster.DrawPos.WithY(netAltitude - 0.01f);
            Vector3 netPos = pull.NetPosition.WithY(netAltitude);

            if (pull.phase == RetrievalPhase.Drop)
            {
                Thing dropping = pull.HeldItem;
                if (dropping != null) dropping.DrawNowAt(netPos.WithY(AltitudeLayer.Item.AltitudeFor()));
                return;
            }

            Thing held = pull.HeldItem;
            if (held != null) held.DrawNowAt(netPos.WithY(AltitudeLayer.Item.AltitudeFor()));

            GenDraw.DrawLineBetween(hand, netPos, tether, TetherWidth);

            // The net opens on the way out and is full size once it lands.
            float size = pull.phase == RetrievalPhase.Launch
                ? Mathf.Lerp(0.45f, NetSize, pull.LaunchFraction)
                : NetSize;
            float spin = pull.phase == RetrievalPhase.Launch ? pull.LaunchFraction * 180f : 0f;
            Matrix4x4 matrix = Matrix4x4.TRS(netPos, Quaternion.Euler(0f, spin, 0f), new Vector3(size, 1f, size));
            Graphics.DrawMesh(MeshPool.plane10, matrix, net, 0);
        }

        /// <summary>The aiming preview: a tether line to a valid target.</summary>
        public static void DrawPreview(Pawn caster, LocalTargetInfo target)
        {
            if (caster == null || !caster.Spawned || !target.IsValid) return;
            float altitude = AltitudeLayer.MetaOverlays.AltitudeFor();
            GenDraw.DrawLineBetween(caster.DrawPos.WithY(altitude), target.CenterVector3.WithY(altitude), tether, TetherWidth);
        }
    }
}
