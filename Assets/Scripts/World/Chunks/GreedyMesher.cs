using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.World.Chunks
{
    /// <summary>
    /// Greedy mesher for LOD4 distant terrain. Runs on a background thread (Task.Run),
    /// so uses plain C# arrays instead of NativeArrays/Burst.
    /// Samples density at a coarse grid, classifies solid/air, then merges adjacent
    /// same-biome exposed faces into maximal rectangles.
    /// </summary>
    public static class GreedyMesher
    {
        private const int SIZE = ChunkData.SIZE; // 32

        public struct Result
        {
            public Vector3[] vertices;
            public int[] indices;
            public Vector3[] normals;
            public Color[] colors;
        }

        /// <summary>
        /// Generate a greedy mesh from density and biome data.
        /// Safe to call from any thread (no NativeArray/Allocator.Temp usage).
        /// </summary>
        public static Result Generate(float[] densityField, byte[] biomeField,
            int coarseStep, Color[] biomeColors256)
        {
            int cs = SIZE / coarseStep;
            if (cs < 1) cs = 1;

            // Build coarse solid/air + biome grid
            int coarseVol = cs * cs * cs;
            var solid = new bool[coarseVol];
            var biome = new byte[coarseVol];

            for (int cz = 0; cz < cs; cz++)
            for (int cy = 0; cy < cs; cy++)
            for (int cx = 0; cx < cs; cx++)
            {
                int sx = Mathf.Min(cx * coarseStep + coarseStep / 2, SIZE - 1);
                int sy = Mathf.Min(cy * coarseStep + coarseStep / 2, SIZE - 1);
                int sz = Mathf.Min(cz * coarseStep + coarseStep / 2, SIZE - 1);
                int sIdx = sx + sy * SIZE + sz * SIZE * SIZE;
                int cIdx = cx + cy * cs + cz * cs * cs;
                solid[cIdx] = densityField[sIdx] > 0f;
                biome[cIdx] = biomeField[sIdx];
            }

            var verts = new List<Vector3>(256);
            var idxs = new List<int>(512);
            var cols = new List<Color>(256);

            // 6 face directions: +X, -X, +Y, -Y, +Z, -Z
            int[][] axes = {
                new[]{0, 2, 1}, new[]{0, 2, 1},
                new[]{1, 0, 2}, new[]{1, 0, 2},
                new[]{2, 0, 1}, new[]{2, 0, 1}
            };
            int[] normals = { 1, -1, 1, -1, 1, -1 };

            for (int face = 0; face < 6; face++)
            {
                int axis = axes[face][0], u = axes[face][1], v = axes[face][2];
                bool positive = (face & 1) == 0;
                var merged = new bool[cs * cs];

                for (int d = 0; d < cs; d++)
                {
                    System.Array.Clear(merged, 0, merged.Length);

                    for (int vi = 0; vi < cs; vi++)
                    for (int ui = 0; ui < cs; ui++)
                    {
                        if (merged[ui + vi * cs]) continue;

                        int[] pos = new int[3];
                        pos[axis] = d; pos[u] = ui; pos[v] = vi;
                        int idx = pos[0] + pos[1] * cs + pos[2] * cs * cs;
                        if (!solid[idx]) continue;

                        // Check neighbor exposure
                        int[] npos = (int[])pos.Clone();
                        npos[axis] += positive ? 1 : -1;
                        if (npos[axis] >= 0 && npos[axis] < cs)
                        {
                            int nIdx = npos[0] + npos[1] * cs + npos[2] * cs * cs;
                            if (solid[nIdx]) continue; // hidden face
                        }

                        byte faceBiome = biome[idx];

                        // Greedy expand along U
                        int width = 1;
                        while (ui + width < cs)
                        {
                            if (merged[(ui + width) + vi * cs]) break;
                            int[] p2 = (int[])pos.Clone();
                            p2[u] = ui + width;
                            int i2 = p2[0] + p2[1] * cs + p2[2] * cs * cs;
                            if (!solid[i2] || biome[i2] != faceBiome) break;
                            int[] n2 = (int[])p2.Clone();
                            n2[axis] += positive ? 1 : -1;
                            if (n2[axis] >= 0 && n2[axis] < cs)
                            {
                                int nI2 = n2[0] + n2[1] * cs + n2[2] * cs * cs;
                                if (solid[nI2]) break;
                            }
                            width++;
                        }

                        // Greedy expand along V
                        int height = 1;
                        while (vi + height < cs)
                        {
                            bool rowOk = true;
                            for (int w = 0; w < width; w++)
                            {
                                if (merged[(ui + w) + (vi + height) * cs]) { rowOk = false; break; }
                                int[] pv = (int[])pos.Clone();
                                pv[u] = ui + w; pv[v] = vi + height;
                                int i3 = pv[0] + pv[1] * cs + pv[2] * cs * cs;
                                if (!solid[i3] || biome[i3] != faceBiome) { rowOk = false; break; }
                                int[] n3 = (int[])pv.Clone();
                                n3[axis] += positive ? 1 : -1;
                                if (n3[axis] >= 0 && n3[axis] < cs)
                                {
                                    int nI3 = n3[0] + n3[1] * cs + n3[2] * cs * cs;
                                    if (solid[nI3]) { rowOk = false; break; }
                                }
                            }
                            if (!rowOk) break;
                            height++;
                        }

                        // Mark merged
                        for (int dv = 0; dv < height; dv++)
                        for (int du = 0; du < width; du++)
                            merged[(ui + du) + (vi + dv) * cs] = true;

                        // Emit quad
                        float scale = coarseStep;
                        Vector3 origin = new Vector3(pos[0], pos[1], pos[2]) * scale;
                        if (positive)
                            origin[axis] += scale;

                        Vector3 du3 = Vector3.zero; du3[u] = scale;
                        Vector3 dv3 = Vector3.zero; dv3[v] = scale;

                        Vector3 p0 = origin;
                        Vector3 p1 = origin + du3 * width;
                        Vector3 p2q = origin + du3 * width + dv3 * height;
                        Vector3 p3 = origin + dv3 * height;

                        Color color = (faceBiome < biomeColors256.Length)
                            ? biomeColors256[faceBiome] : Color.gray;

                        int baseIdx = verts.Count;
                        if (positive)
                        { verts.Add(p0); verts.Add(p1); verts.Add(p2q); verts.Add(p3); }
                        else
                        { verts.Add(p0); verts.Add(p3); verts.Add(p2q); verts.Add(p1); }

                        cols.Add(color); cols.Add(color); cols.Add(color); cols.Add(color);
                        idxs.Add(baseIdx); idxs.Add(baseIdx + 1); idxs.Add(baseIdx + 2);
                        idxs.Add(baseIdx); idxs.Add(baseIdx + 2); idxs.Add(baseIdx + 3);
                    }
                }
            }

            // Compute face normals
            var nrms = new Vector3[verts.Count];
            for (int i = 0; i < nrms.Length; i++) nrms[i] = Vector3.up;
            for (int t = 0; t + 2 < idxs.Count; t += 3)
            {
                Vector3 a = verts[idxs[t]], b = verts[idxs[t + 1]], c = verts[idxs[t + 2]];
                Vector3 fn = Vector3.Cross(b - a, c - a).normalized;
                if (fn.sqrMagnitude < 0.01f) fn = Vector3.up;
                nrms[idxs[t]] = fn; nrms[idxs[t + 1]] = fn; nrms[idxs[t + 2]] = fn;
            }

            return new Result
            {
                vertices = verts.ToArray(),
                indices = idxs.ToArray(),
                normals = nrms,
                colors = cols.ToArray()
            };
        }
    }
}
