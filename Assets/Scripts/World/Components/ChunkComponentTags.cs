using Unity.Entities;

namespace World.Components
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

    public struct ChunkUnloaded : IComponentData
    {
    }

    public struct ChunkNeedLoadFromDrive : IComponentData
    {
    }

    public struct ChunkQueuedForDeletionTag : IComponentData
    {
    }
}