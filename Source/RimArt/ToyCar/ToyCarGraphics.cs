using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>Small moving tread marks aligned to the four tyres in the existing car sprite.</summary>
    internal static class ToyCarGraphics
    {
        private static Material tread;

        public static void DrawTreads(Vector3 centre, float heading, float travel)
        {
            if (tread == null)
                tread = SolidColorMaterials.NewSolidColorMaterial(
                    new Color(0.32f, 0.33f, 0.35f), ShaderDatabase.Cutout);

            Quaternion rotation = Quaternion.Euler(0f, heading, 0f);
            // The source sprite is 128 units wide, drawn at 0.9 cells.
            float phase = Mathf.Repeat(travel * 8f / (Mathf.PI * 2f), 1f);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int axle = 0; axle < 2; axle++)
                {
                    float axleZ = axle == 0 ? 0.0984f : -0.1828f;
                    for (int stripe = 0; stripe < 3; stripe++)
                    {
                        float offset = (Mathf.Repeat((stripe + phase) / 3f, 1f) - 0.5f) * 0.095f;
                        Vector3 position = centre + rotation * new Vector3(side * 0.1723f, 0.003f, axleZ + offset);
                        Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, new Vector3(0.045f, 1f, 0.009f));
                        Graphics.DrawMesh(MeshPool.plane10, matrix, tread, 0);
                    }
                }
            }
        }
    }
}
