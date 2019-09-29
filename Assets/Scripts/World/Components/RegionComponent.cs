using Unity.Entities;
using Unity.Mathematics;

namespace World.Components
{
    public struct RegionPosComponent : IComponentData
    {
        public int3 Pos;
    }

    public struct RegionNeedLoadComponentTag : IComponentData
    {
    }

    public struct RegionNeedUnloadComponentTag : IComponentData
    {
    }

    public struct ChunkNeedAddToRegion : IComponentData
    {
        public Entity ParentRegion;
    }

    [InternalBufferCapacity(VoxConsts._regionSize * VoxConsts._regionSize * VoxConsts._regionSize)]
    public struct RegionChunks : IBufferElementData
    {
        public Entity Chunk;
    }
}