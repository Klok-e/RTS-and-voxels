using Unity.Entities;

namespace Scripts.World.Components
{
    public struct ChunkDirtyComponent : IComponentData
    {
    }
    public struct ChunkNeedApplyVoxelChanges : IComponentData
    {
    }
    public struct ChunkNeedTerrainGeneration : IComponentData
    {
    }
    public struct ChunkNeedMeshApply : IComponentData
    {
    }
}