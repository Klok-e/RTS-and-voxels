using Unity.Entities;
using Unity.Mathematics;

namespace Scripts.World.Components
{
    public struct ChunkPosComponent : IComponentData
    {
        public int3 Pos;
    }
}
