using Unity.Collections;
using Unity.Mathematics;
using Voidborne.World.Biomes;

namespace Voidborne.World.Generation
{
    /// <summary>
    /// K-d tree for nearest-neighbor biome lookup in 5D climate parameter space
    /// (temperature, moisture, continentalness, erosion, depth).
    ///
    /// Built once on the managed side from BiomeDefinition[], then flattened to
    /// NativeArrays for Burst job compatibility. With ~20-30 biomes the tree is
    /// only ~5 levels deep, so each lookup is ~10 comparisons.
    /// </summary>
    public class BiomeTree
    {
        private const int DIMS = 5;

        // Flat tree arrays for Burst compatibility
        private float[] _splitValues;    // split value at each node
        private int[]   _splitAxes;      // which axis (0-4) this node splits on
        private byte[]  _biomeIds;       // biome ID at leaf nodes (0 = internal)
        private int[]   _leftChild;      // index of left child (-1 = none)
        private int[]   _rightChild;     // index of right child (-1 = none)
        private float[][] _points;       // 5D point per node (for distance calc)
        private int _nodeCount;

        // Managed biome definition lookup
        private BiomeDefinition[] _biomes;

        /// <summary>
        /// Build the k-d tree from an array of biome definitions.
        /// Call once at startup.
        /// </summary>
        public void Build(BiomeDefinition[] biomes)
        {
            _biomes = biomes;
            int n = biomes.Length;

            // Pre-allocate arrays (at most 2*n-1 nodes, but n is small)
            int maxNodes = n * 2;
            _splitValues = new float[maxNodes];
            _splitAxes   = new int[maxNodes];
            _biomeIds    = new byte[maxNodes];
            _leftChild   = new int[maxNodes];
            _rightChild  = new int[maxNodes];
            _points      = new float[maxNodes][];
            _nodeCount   = 0;

            // Build index array
            int[] indices = new int[n];
            for (int i = 0; i < n; i++) indices[i] = i;

            BuildRecursive(indices, 0, n, 0);
        }

        private int BuildRecursive(int[] indices, int start, int end, int depth)
        {
            if (start >= end) return -1;

            int axis = depth % DIMS;
            int nodeIdx = _nodeCount++;

            if (end - start == 1)
            {
                // Leaf node
                int biomeIdx = indices[start];
                _splitAxes[nodeIdx] = axis;
                _splitValues[nodeIdx] = GetAxisValue(_biomes[biomeIdx], axis);
                _biomeIds[nodeIdx] = _biomes[biomeIdx].biomeIdByte;
                _leftChild[nodeIdx] = -1;
                _rightChild[nodeIdx] = -1;
                _points[nodeIdx] = GetPoint(_biomes[biomeIdx]);
                return nodeIdx;
            }

            // Sort by axis and split at median
            int mid = (start + end) / 2;
            int sortAxis = axis;
            System.Array.Sort(indices, start, end - start,
                System.Collections.Comparer.Default);
            // Custom sort by axis value
            SortByAxis(indices, start, end, sortAxis);

            int medianIdx = indices[mid];
            _splitAxes[nodeIdx] = axis;
            _splitValues[nodeIdx] = GetAxisValue(_biomes[medianIdx], axis);
            _biomeIds[nodeIdx] = _biomes[medianIdx].biomeIdByte;
            _points[nodeIdx] = GetPoint(_biomes[medianIdx]);

            _leftChild[nodeIdx] = BuildRecursive(indices, start, mid, depth + 1);
            _rightChild[nodeIdx] = BuildRecursive(indices, mid + 1, end, depth + 1);

            return nodeIdx;
        }

        private void SortByAxis(int[] indices, int start, int end, int axis)
        {
            // Simple insertion sort (n is tiny, ~13 biomes)
            for (int i = start + 1; i < end; i++)
            {
                int key = indices[i];
                float keyVal = GetAxisValue(_biomes[key], axis);
                int j = i - 1;
                while (j >= start && GetAxisValue(_biomes[indices[j]], axis) > keyVal)
                {
                    indices[j + 1] = indices[j];
                    j--;
                }
                indices[j + 1] = key;
            }
        }

        private static float GetAxisValue(BiomeDefinition b, int axis)
        {
            switch (axis)
            {
                case 0: return b.idealTemperature;
                case 1: return b.idealMoisture;
                case 2: return b.idealContinentalness;
                case 3: return b.idealErosion;
                case 4: return (b.depthMin + b.depthMax) * 0.5f; // depth center
                default: return 0f;
            }
        }

        private static float[] GetPoint(BiomeDefinition b)
        {
            return new float[]
            {
                b.idealTemperature,
                b.idealMoisture,
                b.idealContinentalness,
                b.idealErosion,
                (b.depthMin + b.depthMax) * 0.5f
            };
        }

        /// <summary>
        /// Find the nearest biome to the given climate parameters.
        /// Returns the biomeIdByte of the closest match.
        /// </summary>
        public byte FindClosest(ClimateParameters climate)
        {
            if (_nodeCount == 0) return 0;

            float[] query = new float[]
            {
                climate.temperature,
                climate.moisture,
                climate.continentalness,
                climate.erosion,
                climate.depth
            };

            float bestDist = float.MaxValue;
            byte bestId = 0;
            SearchNearest(0, query, ref bestDist, ref bestId);
            return bestId;
        }

        private void SearchNearest(int nodeIdx, float[] query, ref float bestDist, ref byte bestId)
        {
            if (nodeIdx < 0 || nodeIdx >= _nodeCount) return;

            // Compute distance to this node's point
            float dist = DistanceSquared(query, _points[nodeIdx]);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestId = _biomeIds[nodeIdx];
            }

            int axis = _splitAxes[nodeIdx];
            float diff = query[axis] - _splitValues[nodeIdx];

            // Search the closer subtree first
            int first  = diff < 0 ? _leftChild[nodeIdx] : _rightChild[nodeIdx];
            int second = diff < 0 ? _rightChild[nodeIdx] : _leftChild[nodeIdx];

            SearchNearest(first, query, ref bestDist, ref bestId);

            // Only search the other subtree if the splitting plane is closer than current best
            if (diff * diff < bestDist)
                SearchNearest(second, query, ref bestDist, ref bestId);
        }

        /// <summary>
        /// Find the top N nearest biomes. Returns arrays of biome IDs and weights
        /// (inverse distance, normalized).
        /// </summary>
        public void FindTopN(ClimateParameters climate, int n, out byte[] biomeIds, out float[] weights)
        {
            float[] query = new float[]
            {
                climate.temperature,
                climate.moisture,
                climate.continentalness,
                climate.erosion,
                climate.depth
            };

            // Collect all distances (biome count is small)
            var distances = new (byte id, float dist)[_nodeCount];
            int count = 0;
            CollectAll(0, query, distances, ref count);

            // Sort by distance
            System.Array.Sort(distances, 0, count, new DistComparer());

            int topN = math.min(n, count);
            biomeIds = new byte[topN];
            weights = new float[topN];

            float totalWeight = 0f;
            for (int i = 0; i < topN; i++)
            {
                biomeIds[i] = distances[i].id;
                float w = 1f / (distances[i].dist + 0.001f);
                weights[i] = w;
                totalWeight += w;
            }

            if (totalWeight > 0f)
            {
                for (int i = 0; i < topN; i++)
                    weights[i] /= totalWeight;
            }
        }

        private void CollectAll(int nodeIdx, float[] query, (byte id, float dist)[] result, ref int count)
        {
            if (nodeIdx < 0 || nodeIdx >= _nodeCount) return;

            float dist = DistanceSquared(query, _points[nodeIdx]);
            result[count++] = (_biomeIds[nodeIdx], dist);

            CollectAll(_leftChild[nodeIdx], query, result, ref count);
            CollectAll(_rightChild[nodeIdx], query, result, ref count);
        }

        private struct DistComparer : System.Collections.Generic.IComparer<(byte id, float dist)>
        {
            public int Compare((byte id, float dist) a, (byte id, float dist) b)
            {
                return a.dist.CompareTo(b.dist);
            }
        }

        private static float DistanceSquared(float[] a, float[] b)
        {
            float sum = 0f;
            for (int i = 0; i < DIMS; i++)
            {
                float d = a[i] - b[i];
                sum += d * d;
            }
            return sum;
        }

        // =========================================================================
        //  Burst-compatible flat representation for use in jobs
        // =========================================================================

        /// <summary>
        /// Flat representation of the BiomeTree for use in Burst jobs.
        /// All arrays should be allocated with Allocator.Persistent and disposed by the caller.
        /// </summary>
        public struct NativeBiomeTree : System.IDisposable
        {
            public NativeArray<float> SplitValues;
            public NativeArray<int>   SplitAxes;
            public NativeArray<byte>  BiomeIds;
            public NativeArray<int>   LeftChild;
            public NativeArray<int>   RightChild;
            public NativeArray<float> Points; // flattened: nodeIdx * DIMS + axis
            public int NodeCount;

            public void Dispose()
            {
                if (SplitValues.IsCreated) SplitValues.Dispose();
                if (SplitAxes.IsCreated) SplitAxes.Dispose();
                if (BiomeIds.IsCreated) BiomeIds.Dispose();
                if (LeftChild.IsCreated) LeftChild.Dispose();
                if (RightChild.IsCreated) RightChild.Dispose();
                if (Points.IsCreated) Points.Dispose();
            }

            /// <summary>
            /// Find nearest biome ID using iterative k-d tree search (Burst-safe).
            /// </summary>
            public byte FindClosest(ClimateParameters climate)
            {
                if (NodeCount == 0) return 0;

                float qt = climate.temperature;
                float qm = climate.moisture;
                float qc = climate.continentalness;
                float qe = climate.erosion;
                float qd = climate.depth;

                float bestDist = float.MaxValue;
                byte bestId = 0;

                // Iterative stack-based traversal (max tree depth ~8 for 30 biomes)
                const int MAX_STACK = 16;
                int stackTop = 0;
                var stack = new NativeArray<int>(MAX_STACK, Allocator.Temp);
                stack[stackTop++] = 0;

                while (stackTop > 0)
                {
                    int idx = stack[--stackTop];
                    if (idx < 0 || idx >= NodeCount) continue;

                    // Distance to this node
                    int pBase = idx * 5;
                    float d0 = qt - Points[pBase];
                    float d1 = qm - Points[pBase + 1];
                    float d2 = qc - Points[pBase + 2];
                    float d3 = qe - Points[pBase + 3];
                    float d4 = qd - Points[pBase + 4];
                    float dist = d0 * d0 + d1 * d1 + d2 * d2 + d3 * d3 + d4 * d4;

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestId = BiomeIds[idx];
                    }

                    int axis = SplitAxes[idx];
                    float queryVal;
                    switch (axis)
                    {
                        case 0: queryVal = qt; break;
                        case 1: queryVal = qm; break;
                        case 2: queryVal = qc; break;
                        case 3: queryVal = qe; break;
                        default: queryVal = qd; break;
                    }

                    float diff = queryVal - SplitValues[idx];
                    int first  = diff < 0 ? LeftChild[idx] : RightChild[idx];
                    int second = diff < 0 ? RightChild[idx] : LeftChild[idx];

                    // Push second branch first (lower priority)
                    if (second >= 0 && diff * diff < bestDist && stackTop < MAX_STACK)
                        stack[stackTop++] = second;
                    if (first >= 0 && stackTop < MAX_STACK)
                        stack[stackTop++] = first;
                }

                stack.Dispose();
                return bestId;
            }
        }

        /// <summary>
        /// Create a NativeBiomeTree for use in Burst jobs.
        /// Caller is responsible for disposing the returned struct.
        /// </summary>
        public NativeBiomeTree ToNative()
        {
            var native = new NativeBiomeTree
            {
                SplitValues = new NativeArray<float>(_nodeCount, Allocator.Persistent),
                SplitAxes   = new NativeArray<int>(_nodeCount, Allocator.Persistent),
                BiomeIds    = new NativeArray<byte>(_nodeCount, Allocator.Persistent),
                LeftChild   = new NativeArray<int>(_nodeCount, Allocator.Persistent),
                RightChild  = new NativeArray<int>(_nodeCount, Allocator.Persistent),
                Points      = new NativeArray<float>(_nodeCount * DIMS, Allocator.Persistent),
                NodeCount   = _nodeCount
            };

            for (int i = 0; i < _nodeCount; i++)
            {
                native.SplitValues[i] = _splitValues[i];
                native.SplitAxes[i]   = _splitAxes[i];
                native.BiomeIds[i]    = _biomeIds[i];
                native.LeftChild[i]   = _leftChild[i];
                native.RightChild[i]  = _rightChild[i];

                for (int d = 0; d < DIMS; d++)
                    native.Points[i * DIMS + d] = _points[i][d];
            }

            return native;
        }
    }
}
