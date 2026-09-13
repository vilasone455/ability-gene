using AM;
using AM.RendererWorkers;
using UnityEngine;

namespace RimArt.MeleeAnimation
{
    /// <summary>
    /// Turns a throw animation's throwing hand and held item to the exact target direction.
    ///
    /// Each throw style has three clips (east mirrored for west, north, south) authored for their
    /// own direction. <see cref="ThrowAim"/> holds how far the real target is from that direction,
    /// at most 45 degrees. Every part under PawnALift - the invisible PawnAHolding, HandA and the
    /// held item - is rotated by that angle around PawnALift's current position just before it is
    /// drawn. PawnALift carries the body's step and the hand's screen height, so the height stays
    /// vertical and only the ground-plane reach turns. The body, head and off hand are untouched.
    ///
    /// Named by every throw AnimDef in AG_Throw_Anims.xml as rendererWorker. Melee Animation makes
    /// one instance per playing animation and calls SetupRenderer once, then PreRenderPart for
    /// each drawn part every frame.
    ///
    /// This assembly references Melee Animation directly and sits in Patch_MeleeAnimation, which
    /// loads only when that mod is active, so RimArt.dll itself still loads without it.
    /// </summary>
    public class ThrowAimWorker : AnimationRendererWorker
    {
        public const string PivotPartName = "PawnALift";

        private AnimRenderer renderer;
        private AnimPartData pivot;

        public override void SetupRenderer(AnimRenderer renderer)
        {
            this.renderer = renderer;
            pivot = renderer?.GetPart(PivotPartName);
        }

        public override void PreRenderPart(in AnimPartSnapshot part, in AnimPartOverrideData overrideData,
                                           ref Mesh mesh, ref Matrix4x4 matrix, ref Material mat,
                                           ref MaterialPropertyBlock finalMpb)
        {
            if (pivot == null || renderer == null || !UnderPivot(part.Part)) return;
            if (!ThrowAim.TryGet(renderer, out float degrees) || Mathf.Approximately(degrees, 0f)) return;

            // matrix is RootTransform * WorldMatrix. The rotation belongs between the two, in the
            // animation's own space, where the pivot's WorldMatrix (mirroring already applied) is.
            Vector3 center = renderer.GetSnapshot(pivot).WorldMatrix.MultiplyPoint3x4(Vector3.zero);

            // Unity's rotation about +y turns +z towards +x, which is clockwise seen from above;
            // ThrowAim is counter-clockwise, so the sign flips.
            Matrix4x4 turn = Matrix4x4.Translate(center)
                             * Matrix4x4.Rotate(Quaternion.Euler(0f, -degrees, 0f))
                             * Matrix4x4.Translate(-center);

            Matrix4x4 root = renderer.RootTransform;
            matrix = root * turn * root.inverse * matrix;
        }

        private bool UnderPivot(AnimPartData data)
        {
            for (AnimPartData p = data?.Parent; p != null; p = p.Parent)
                if (p == pivot) return true;
            return false;
        }
    }
}
