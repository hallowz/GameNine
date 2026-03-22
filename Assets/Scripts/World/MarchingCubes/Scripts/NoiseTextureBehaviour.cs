/*
    Author: Gus Tahara-Edmonds (original)
    Modified for Unity 6.3 compatibility
    Purpose: Script that reads 2D noise texture instead of calculating it with math.
*/

using UnityEngine;

namespace Voidborne.World.MarchingCubes
{
    public class NoiseTextureBehaviour : DensityBehaviour
    {
        public float scale;
        public Texture2D noiseTex;

        public override void UpdateParams()
        {
            compute.SetTexture(0, "noiseTex", noiseTex);
            compute.SetInt("size", noiseTex.width);
            compute.SetFloat("scale", (float)noiseTex.width / (pointsPerAxis - 1) * scale);
        }
    }
}
