using Unity.Entities;
using Unity.Mathematics;

namespace World.Components
{
    public struct ChunkPosComponent : IComponentData
    {
        public int3 Pos;
    }
}