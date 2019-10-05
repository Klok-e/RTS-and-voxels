using Unity.Mathematics;

namespace World
{
    public static class VoxConsts
    {
        /// <summary>
        ///     Only even amount or else SetVoxel won't work at all
        /// </summary>
        public const int ChunkSize = 16;

        /// <summary>
        ///     Size in chunks
        /// </summary>
        public const int RegionSize = 2;

        /// <summary>
        ///     Size of a voxel
        /// </summary>
        public const float BlockSize = 0.5f;

        public static int3 ChunkIn(float3 pos)
        {
            pos = OffsetWorld(pos);

            var worldPos       = FromWorldToVoxelWorldCoords(pos);
            var loaderChunkInf = worldPos / ChunkSize;
            var loaderChunkIn  = math.int3(math.floor(loaderChunkInf));
            return loaderChunkIn;
        }

        public static int3 RegionIn(float3 pos)
        {
            return math.int3(math.floor(math.float3(ChunkIn(pos)) / RegionSize));
        }

        public static int3 VoxIndexInChunk(float3 pos, int3 chunkPos)
        {
            pos = OffsetWorld(pos);

            pos = FromWorldToVoxelWorldCoords(pos);

            return math.int3(math.floor(pos - math.float3(chunkPos * ChunkSize)));
        }

        public static float3 FromWorldToVoxelWorldCoords(float3 pos)
        {
            return pos / BlockSize;
        }

        public static int3 VoxIndexInChunk(float3 pos)
        {
            return VoxIndexInChunk(pos, ChunkIn(pos));
        }

        private static float3 OffsetWorld(float3 pos)
        {
            pos.x += BlockSize / 2f;
            pos.y += BlockSize / 2f;
            pos.z += BlockSize / 2f;
            return pos;
        }
    }
}