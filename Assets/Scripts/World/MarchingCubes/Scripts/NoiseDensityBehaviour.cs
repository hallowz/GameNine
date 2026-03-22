/*
    Author: Gus Tahara-Edmonds (original)
    Modified for Unity 6.3 compatibility
    Purpose: Inherits from DensityBehaviour. This script holds settings for making terrain from layers (octaves) of 3D noise.
*/

using UnityEngine;

namespace Voidborne.World.MarchingCubes
{
    public class NoiseDensityBehaviour : DensityBehaviour
    {
        [Header("Noise")]
        public float globalScale = 1;
        public NoiseOctave[] octaves;

        ComputeBuffer octavesBuffer;

        public override void UpdateParams()
        {
            octavesBuffer = new ComputeBuffer(octaves.Length, 20);
            octavesBuffer.SetData(octaves);
            compute.SetFloat("globalScale", globalScale);
            compute.SetBuffer(0, "noiseOctaves", octavesBuffer);
            compute.SetInt("length", octaves.Length);
        }

        private void OnDestroy()
        {
            if (octavesBuffer != null)
            {
                octavesBuffer.Release();
            }
        }

        [System.Serializable]
        public struct NoiseOctave
        {
            public float frequency;
            public float amplitude;
            public Vector3 offset;
        }
    }
}
