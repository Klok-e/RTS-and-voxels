using Scripts.World.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Scripts.World.Systems
{
    [UpdateBefore(typeof(ChunkSystem))]
    public class ApplyVoxelChangesSystem : JobComponentSystem
    {
        private ComponentGroup _chunksNeedApplyVoxelChanges;

        [Inject]
        private ApplyVoxelsBarrier _barrier;

        [UpdateAfter(typeof(ApplyVoxelChangesSystem))]
        private class ApplyVoxelsBarrier : BarrierSystem { }

        private struct ApplyChanges : IJob
        {
            public ComponentArray<RegularChunk> needApply;
            public BufferArray<Voxel> voxelBuffers;
            public EntityArray applEntitties;

            public EntityCommandBuffer commandBuffer;

            public void Execute()
            {
                for(int i = 0; i < needApply.Length; i++)
                {
                    ApplyChangesToChunk(needApply[i], applEntitties[i], voxelBuffers[i]);
                    commandBuffer.RemoveComponent<ChunkNeedApplyVoxelChanges>(applEntitties[i]);
                    commandBuffer.AddComponent(applEntitties[i], new ChunkDirtyComponent());
                }
            }

            private void ApplyChangesToChunk(RegularChunk chunk, Entity entity, DynamicBuffer<Voxel> buffer)
            {
                while(chunk.VoxelSetQuery.Count > 0)
                {
                    var x = chunk.VoxelSetQuery.Dequeue();

                    buffer.AtSet(x.Pos.x, x.Pos.y, x.Pos.z,
                        new Voxel
                        {
                            Type = x.NewVoxelType,
                        });
                }
            }
        }
        protected override void OnCreateManager()
        {
            _chunksNeedApplyVoxelChanges = EntityManager.CreateComponentGroup(typeof(RegularChunk), typeof(ChunkNeedApplyVoxelChanges), typeof(Voxel));
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            new ApplyChanges
            {
                needApply = _chunksNeedApplyVoxelChanges.GetComponentArray<RegularChunk>(),
                voxelBuffers = _chunksNeedApplyVoxelChanges.GetBufferArray<Voxel>(),
                applEntitties = _chunksNeedApplyVoxelChanges.GetEntityArray(),
                commandBuffer = _barrier.CreateCommandBuffer(),
            }.Schedule(inputDeps).Complete();
            return default;
        }
    }
}
