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
        public Entity parentRegion;
    }

    [InternalBufferCapacity(VoxConsts.RegionSize * VoxConsts.RegionSize * VoxConsts.RegionSize)]
    public struct RegionChunks : IBufferElementData
    {
        public Entity chunk;
    }
}