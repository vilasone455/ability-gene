using RimWorld;
using UnityEngine;
using Verse;

namespace RimArt
{
    /// <summary>Separate chassis and wheels keep all animation independent of navigation.</summary>
    [StaticConstructorOnStartup]
    internal static class ToyCarGraphics
    {
        private static Material tread;
        private static Material tyre;
        private static Material body;

        public static void Draw(Vector3 centre, float heading, float travel, float speed,
            float deployment, float steering, float brakeDip)
        {
            if (body == null)
            {
                body = MaterialPool.MatFrom("RimArt/ToyCar/Body", ShaderDatabase.Cutout);
                tread = SolidColorMaterials.NewSolidColorMaterial(
                    new Color(0.32f, 0.33f, 0.35f), ShaderDatabase.Cutout);
                tyre = SolidColorMaterials.NewSolidColorMaterial(
                    new Color(0.15f, 0.15f, 0.165f), ShaderDatabase.Cutout);
            }

            float unfold = Mathf.SmoothStep(0f, 1f, deployment);
            float scale = Mathf.Lerp(0.55f, 1f, unfold);
            float bounce = Mathf.Sin(deployment * Mathf.PI * 3f) * (1f - deployment) * 0.06f;
            Quaternion rotation = Quaternion.Euler(0f, heading, 0f);
            float phase = Mathf.Repeat(travel * 8f / (Mathf.PI * 2f), 1f);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int axle = 0; axle < 2; axle++)
                {
                    float axleZ = axle == 0 ? 0.0984f : -0.1828f;
                    Vector3 wheel = centre + rotation * new Vector3(
                        side * Mathf.Lerp(0.08f, 0.20f, unfold), -0.006f, axleZ * scale);
                    Quaternion wheelRotation = rotation * Quaternion.Euler(0f, axle == 0 ? steering : 0f, 0f);
                    DrawPlane(wheel, wheelRotation, 0.064f * scale, 0.112f * scale, tyre);
                    for (int stripe = 0; stripe < 3; stripe++)
                    {
                        float offset = (Mathf.Repeat((stripe + phase) / 3f, 1f) - 0.5f) * 0.095f;
                        Vector3 position = wheel + wheelRotation * new Vector3(0f, 0.001f, offset * scale);
                        DrawPlane(position, wheelRotation, 0.045f * scale, 0.009f * scale, tread);
                    }
                }
            }

            // A top-down pitch is conveyed by slight foreshortening and forward weight shift.
            Vector3 chassis = centre + rotation * new Vector3(0f, 0f, brakeDip * 0.025f);
            chassis.z += bounce + Mathf.Sin(travel * 24f) * 0.012f * speed;
            DrawPlane(chassis, rotation, 0.9f * scale, 0.9f * scale * (1f - brakeDip * 0.08f), body);
        }

        public static void DetonationFlash(Vector3 position, Map map)
        {
            if (position.ToIntVec3().Fogged(map)) return;
            FleckDef flash = DefDatabase<FleckDef>.GetNamedSilentFail("PlainFlash");
            if (flash == null) return;
            // Spawn before the blast, with no delay to damage. The fleck outlives the car.
            FleckCreationData data = FleckMaker.GetDataStatic(position, map, flash, 0.9f);
            data.instanceColor = new Color(1f, 0.12f, 0.08f);
            map.flecks.CreateFleck(data);
        }

        private static void DrawPlane(Vector3 position, Quaternion rotation, float width, float length, Material material)
        {
            Graphics.DrawMesh(MeshPool.plane10,
                Matrix4x4.TRS(position, rotation, new Vector3(width, 1f, length)), material, 0);
        }
    }
}
