using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Voidborne.World.Chunks
{
    /// <summary>
    /// Burst job that computes a 15-bit visibility graph for a chunk.
    /// Each bit represents whether two faces of the chunk are connected through
    /// air voxels (density &lt;= 0). Used by ChunkOcclusionCuller to skip rendering
    /// chunks fully enclosed in solid terrain (Minecraft's "Tommo algorithm").
    ///
    /// The 6 faces are: +X(0), -X(1), +Y(2), -Y(3), +Z(4), -Z(5).
    /// The 15 pairs are indexed as: pair(a,b) where a &lt; b, index = a*(11-a)/2 + b - a - 1.
    /// </summary>
    [BurstCompile]
    public struct ChunkVisibilityJob : IJob
    {
        private const int SIZE = ChunkData.SIZE; // 32
        private const int VOL = SIZE * SIZE * SIZE;

        [ReadOnly] public NativeArray<float> DensityField;
        public NativeArray<ushort> Result; // length 1

        // Face indices
        private const int FACE_POS_X = 0;
        private const int FACE_NEG_X = 1;
        private const int FACE_POS_Y = 2;
        private const int FACE_NEG_Y = 3;
        private const int FACE_POS_Z = 4;
        private const int FACE_NEG_Z = 5;

        public void Execute()
        {
            // Track which faces each voxel's connected component touches.
            // Flood fill from every unvisited air voxel, tracking face connectivity.

            var visited = new NativeArray<bool>(VOL, Allocator.Temp);
            var faceFlags = new NativeArray<byte>(VOL, Allocator.Temp); // which faces this component touches
            var stack = new NativeList<int>(4096, Allocator.Temp);

            ushort graph = 0;

            for (int startIdx = 0; startIdx < VOL; startIdx++)
            {
                if (visited[startIdx]) continue;
                if (DensityField[startIdx] > 0f) continue; // solid — skip

                // Flood fill this connected air component
                byte componentFaces = 0;
                stack.Clear();
                stack.Add(startIdx);
                visited[startIdx] = true;

                while (stack.Length > 0)
                {
                    int idx = stack[stack.Length - 1];
                    stack.RemoveAtSwapBack(stack.Length - 1);

                    int x = idx % SIZE;
                    int y = (idx / SIZE) % SIZE;
                    int z = idx / (SIZE * SIZE);

                    // Check if this voxel touches any face
                    if (x == 0)        componentFaces |= (1 << FACE_NEG_X);
                    if (x == SIZE - 1) componentFaces |= (1 << FACE_POS_X);
                    if (y == 0)        componentFaces |= (1 << FACE_NEG_Y);
                    if (y == SIZE - 1) componentFaces |= (1 << FACE_POS_Y);
                    if (z == 0)        componentFaces |= (1 << FACE_NEG_Z);
                    if (z == SIZE - 1) componentFaces |= (1 << FACE_POS_Z);

                    // Visit 6-connected neighbors
                    TryPush(stack, visited, x + 1, y, z);
                    TryPush(stack, visited, x - 1, y, z);
                    TryPush(stack, visited, x, y + 1, z);
                    TryPush(stack, visited, x, y - 1, z);
                    TryPush(stack, visited, x, y, z + 1);
                    TryPush(stack, visited, x, y, z - 1);
                }

                // Convert face flags to pair bits
                graph |= FaceFlagsToPairBits(componentFaces);

                // Early out: all 15 pairs connected
                if (graph == 0x7FFF) break;
            }

            Result[0] = graph;

            stack.Dispose();
            faceFlags.Dispose();
            visited.Dispose();
        }

        private void TryPush(NativeList<int> stack, NativeArray<bool> visited, int x, int y, int z)
        {
            if (x < 0 || x >= SIZE || y < 0 || y >= SIZE || z < 0 || z >= SIZE) return;
            int idx = x + y * SIZE + z * SIZE * SIZE;
            if (visited[idx]) return;
            if (DensityField[idx] > 0f) return; // solid
            visited[idx] = true;
            stack.Add(idx);
        }

        /// <summary>
        /// Converts a 6-bit face mask into the 15-bit pair mask.
        /// If a connected component touches faces A and B, the pair (A,B) is set.
        /// </summary>
        private static ushort FaceFlagsToPairBits(byte faces)
        {
            ushort bits = 0;
            for (int a = 0; a < 6; a++)
            {
                if ((faces & (1 << a)) == 0) continue;
                for (int b = a + 1; b < 6; b++)
                {
                    if ((faces & (1 << b)) == 0) continue;
                    bits |= (ushort)(1 << PairIndex(a, b));
                }
            }
            return bits;
        }

        /// <summary>
        /// Computes the pair index for two face indices a &lt; b.
        /// 15 total pairs: (0,1),(0,2),(0,3),(0,4),(0,5),(1,2),(1,3),(1,4),(1,5),(2,3),(2,4),(2,5),(3,4),(3,5),(4,5)
        /// </summary>
        public static int PairIndex(int a, int b)
        {
            // a*(11-a)/2 + b - a - 1
            return a * (11 - a) / 2 + b - a - 1;
        }

        /// <summary>
        /// Returns the opposite face index (e.g., +X → -X).
        /// </summary>
        public static int OppositeFace(int face)
        {
            // 0↔1, 2↔3, 4↔5
            return face ^ 1;
        }

        /// <summary>
        /// Checks if face 'entryFace' connects to any other face in the visibility graph.
        /// </summary>
        public static bool FaceConnectsToAny(ushort graph, int entryFace)
        {
            for (int other = 0; other < 6; other++)
            {
                if (other == entryFace) continue;
                int a = entryFace < other ? entryFace : other;
                int b = entryFace < other ? other : entryFace;
                int pairIdx = PairIndex(a, b);
                if ((graph & (1 << pairIdx)) != 0)
                    return true;
            }
            return false;
        }
    }
}
