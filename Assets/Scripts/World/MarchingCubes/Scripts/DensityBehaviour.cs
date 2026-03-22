/*
    Author: Gus Tahara-Edmonds (original)
    Modified for Unity 6.3 compatibility
    Purpose: Template script with functionality to setup the desired compute shader which generates the world data for the
    marching cubes algorithm to polygonalize.
*/

using UnityEngine;

namespace Voidborne.World.MarchingCubes
{
    public abstract class DensityBehaviour : MonoBehaviour
    {
        public ComputeShader compute;
        [HideInInspector]
        public int pointsPerAxis;

        public void Init(RenderTexture densityData, int pointsPerAxis, float scale)
        {
            compute.SetTexture(0, "densityData", densityData);
            this.pointsPerAxis = pointsPerAxis;
            compute.SetInt("pointsPerAxis", pointsPerAxis);
            compute.SetFloat("scale", scale);
            UpdateParams();
        }

        public abstract void UpdateParams();

        public void Generate(int threads, Vector3 worldPos)
        {
            float[] positionArray = { worldPos.x, worldPos.y, worldPos.z };
            compute.SetFloats("worldPos", positionArray);
            compute.Dispatch(0, threads, threads, threads);
        }
    }
}
